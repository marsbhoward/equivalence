import type pg from 'pg';
import { q } from './db.js';
import type { FinalDecision } from './finalFloor.js';
import { RunFormat, recordedFormat } from './runFormat.js';
import type { AvatarDecision } from './avatarBoxes.js';

/**
 * WHAT THE SERVICE SAW OF A RUN - the one authority a run's rewards are judged against.
 *
 * Everything in a run is played on the client, so what the client CLAIMS at the run's end (the
 * floor it reached, the boxes it secured) is only a claim. What the service can stand behind is
 * what it recorded itself, on its own clock:
 *
 *   format      the rules the run started under (runFormat.ts) - Classic or the Short Descent
 *   started_at  the server's time when the run-start save arrived (routes/saves.ts)
 *   reached     the deepest floor a run SNAPSHOT showed the run on (routes/runs.ts)
 *   cleared     the deepest floor a snapshot showed CLEARED - entering floor N means N-1 was
 *
 * commit-run (routes/player.ts) mints against these, never against the summary alone. A claim the
 * record does not support is CLAMPED, never refused: the run still ends, its boxes are cut back to
 * what the service saw - the plausibility check agreed in MONETIZATION.md.
 *
 * One row per (owner, character, run), kept after the run ends (unlike the `runs` row, which goes
 * when the run does) - it is also where a run's commit is recorded, so it mints once.
 */

/** A run-start save arrived: the run's clock starts on the SERVER's time, under its format. A
 *  resume writes none; a repeated run-start keeps the first's clock and format. */
export async function recordRunStart(
  c: pg.PoolClient, owner: string, profileId: string, runId: string, format: RunFormat,
) {
  await c.query(
    `INSERT INTO run_progress (owner, profile_id, run_id, format) VALUES ($1, $2, $3, $4)
     ON CONFLICT (owner, profile_id, run_id) DO NOTHING`,
    [owner, profileId, runId, format.id]);
}

/** The format a recorded run started under, or null when the service never saw it start. */
export async function runFormatOf(
  c: pg.PoolClient, owner: string, profileId: string, runId: string,
): Promise<RunFormat | null> {
  const r = await c.query<{ format: string }>(
    'SELECT format FROM run_progress WHERE owner = $1 AND profile_id = $2 AND run_id = $3',
    [owner, profileId, runId]);
  return r.rowCount ? recordedFormat(r.rows[0].format) : null;
}

/**
 * THE RUN CLOCK, MEASURED BY THE SERVICE. A run's clock runs while a floor is being fought and
 * stops at its clear, so the menus after a clear are off it (SHORT-DESCENT.md). The service sees
 * both ends of every floor - the snapshot ENTERING it and the one CLEARING it - and stamps them on
 * its own clock, so it times each floor ATTEMPT itself:
 *
 *   entering a floor   an attempt opens at the server's time (less the format's walk-in seconds)
 *   clearing it        the attempt closes; its seconds join the clock
 *   entering it AGAIN  from the same session (a puzzle given up, a Collapsing Rift run out) the
 *                      attempt carries on; from another (a resume replaying the floor) the old
 *                      attempt closed at the LAST time the service heard from it - a snapshot or a
 *                      heartbeat - so time played is never refunded and time away is never charged
 *
 * Nothing the client reports about time is trusted: a modified client cannot run this clock slow.
 * The pause menu and a hidden tab DURING a fight are on it (the service cannot tell them from
 * fighting); the game's own clock matches (Core/RunFormat).
 */
interface Attempt {
  attempt_floor: number | null;
  attempt_at: Date | null;
  attempt_session: string | null;
  last_seen: Date | null;
  now: Date;
  format: string;
}

const secondsBetween = (from: Date, to: Date) => Math.max(0, (to.getTime() - from.getTime()) / 1000);

/** A snapshot arrived, accepted as the run's newest: how far it shows the run (only ever deeper),
 *  and the run clock's floor attempts. */
export async function recordSnapshot(
  c: pg.PoolClient, owner: string, profileId: string, runId: string, floor: number, stage: string, session: string,
) {
  const cleared = stage === 'Cleared' ? floor : Math.max(0, floor - 1);
  const r = await c.query<Attempt>(
    `SELECT attempt_floor, attempt_at, attempt_session, last_seen, now() AS now, format
       FROM run_progress WHERE owner = $1 AND profile_id = $2 AND run_id = $3 FOR UPDATE`,
    [owner, profileId, runId]);
  const a = r.rows[0];
  if (!a) return;
  const walkIn = recordedFormat(a.format).walkInSeconds;
  const played = (from: Date, to: Date) => Math.max(0, secondsBetween(from, to) - walkIn);

  let add = 0;
  let open: { floor: number | null; at: Date | null; session: string | null } =
    { floor: a.attempt_floor, at: a.attempt_at, session: a.attempt_session };
  if (stage === 'Entering') {
    const carriesOn = a.attempt_at && a.attempt_floor === floor && a.attempt_session === session;
    if (!carriesOn) {
      // An attempt left open was abandoned (a resume replays its floor): it counts to the last
      // moment the service heard from it.
      if (a.attempt_at) add = played(a.attempt_at, a.last_seen ?? a.attempt_at);
      open = { floor, at: a.now, session };
    }
  } else if (stage === 'Cleared' && a.attempt_at && a.attempt_floor === floor) {
    add = played(a.attempt_at, a.now);
    open = { floor: null, at: null, session: null };
  }

  await c.query(
    `UPDATE run_progress SET reached = GREATEST(reached, $4), cleared = GREATEST(cleared, $5),
            clock_seconds = clock_seconds + $6, attempt_floor = $7, attempt_at = $8, attempt_session = $9,
            last_seen = now(), updated_at = now()
      WHERE owner = $1 AND profile_id = $2 AND run_id = $3`,
    [owner, profileId, runId, floor, cleared, add, open.floor, open.at, open.session]);
}

/** A heartbeat arrived: the run was still being played at this moment. */
export async function recordBeat(owner: string, profileId: string, runId: string) {
  await q('UPDATE run_progress SET last_seen = now() WHERE owner = $1 AND profile_id = $2 AND run_id = $3',
    [owner, profileId, runId]);
}

/** The run clock now: every closed attempt, plus the one open (to this moment). */
export async function runClock(c: pg.PoolClient, owner: string, profileId: string, runId: string): Promise<number> {
  const r = await c.query<{ clock_seconds: number; attempt_at: Date | null; now: Date; format: string }>(
    `SELECT clock_seconds, attempt_at, now() AS now, format FROM run_progress
      WHERE owner = $1 AND profile_id = $2 AND run_id = $3`,
    [owner, profileId, runId]);
  const row = r.rows[0];
  if (!row) return Infinity;
  const walkIn = recordedFormat(row.format).walkInSeconds;
  const open = row.attempt_at ? Math.max(0, secondsBetween(row.attempt_at, row.now) - walkIn) : 0;
  return Number(row.clock_seconds) + open;
}

export type BoxTier = 'Diamond' | 'BlackDiamond';
export const BOX_TIERS: BoxTier[] = ['Diamond', 'BlackDiamond'];

export interface Seen {
  reached: number;
  cleared: number;
  elapsedSeconds: number;
}

export interface Allowance {
  /** Boxes the run may have, per tier. */
  allowed: Partial<Record<BoxTier, number>>;
  /** Claimed past what the record supports, per tier - reported, never minted. */
  clamped: Partial<Record<BoxTier, number>>;
  /** The floor the record supports: the claim, the snapshots and the clock, whichever is least. */
  floor: number;
  /** The floor the CLOCK alone supports - for the log, when it is what clamped. */
  timeFloor: number;
}

/**
 * What a run's end may mint, from the claim and what the service saw and DECIDED. Pure - the rules
 * in one place.
 *
 * - The deepest floor counted is the LEAST of the floor claimed, the floor the snapshots reached,
 *   and the floor the clock allows (server time since the run started / the format's seconds a floor).
 *   Snapshots alone are not enough - a modified client can send them as fast as it likes - and the
 *   clock alone is not either; together, a fake run costs its full real time.
 * - Every Diamond and Black Diamond box a run can hold was DECIDED by the service: the avatars'
 *   (avatarBoxes.ts) on floors within that depth, and the final floor's (finalFloor.ts) when the
 *   depth reaches the format's final floor. The claim only ever LOWERS that - a box found but lost before it was secured (a
 *   death between the avatar and a Rift) is honestly claimed as fewer.
 */
export function allowance(
  claimedFloors: number,
  claimed: Record<BoxTier, number>,
  seen: Seen,
  avatars: AvatarDecision[],
  final: FinalDecision | null,
  format: RunFormat,
): Allowance {
  const per = format.minSecondsPerFloor;
  const timeFloor = per > 0 ? Math.floor(seen.elapsedSeconds / per) : Infinity;
  const floor = Math.max(0, Math.min(claimedFloors, seen.reached, timeFloor));

  const decided: Record<BoxTier, number> = { Diamond: 0, BlackDiamond: 0 };
  for (const a of avatars)
    if (a.floor <= floor) {
      decided.Diamond += a.diamond;
      decided.BlackDiamond += a.black_diamond;
    }
  if (final && floor >= format.finalFloor && (final.tier === 'Diamond' || final.tier === 'BlackDiamond'))
    decided[final.tier] += final.count;

  const allowed: Partial<Record<BoxTier, number>> = {};
  const clamped: Partial<Record<BoxTier, number>> = {};
  for (const tier of BOX_TIERS) {
    const n = Math.min(Math.max(0, claimed[tier]), decided[tier]);
    if (n > 0) allowed[tier] = n;
    if (n < claimed[tier]) clamped[tier] = claimed[tier] - n;
  }
  return { allowed, clamped, floor, timeFloor: Number.isFinite(timeFloor) ? timeFloor : -1 };
}

export type RunCheck =
  | { ok: true; reached: number; cleared: number; elapsed: number; format: RunFormat }
  | { ok: false; status: 409; error: string; extra?: Record<string, unknown> };

/**
 * Whether a run may be paid for reaching `floor` NOW - the rule every box the service decides
 * (an avatar's, floor 100's) is held to, on top of its own:
 *
 * - it is the character's CURRENT run: its latest run-start save (a modified client must not run
 *   several side by side, nor claim for an old one);
 * - its snapshots show it on `floor` - the service saw it get there;
 * - the server's time since it started allows `floor` at its format's seconds a floor.
 *
 * Call inside the caller's transaction, after locking the character's row.
 */
export async function currentRunAt(
  c: pg.PoolClient, owner: string, profileId: string, runId: string, floor: number,
): Promise<RunCheck> {
  const start = await c.query<{ run_id: string | null }>(
    `SELECT run_id FROM character_history
      WHERE owner = $1 AND profile_id = $2 AND reason = 'run-start' ORDER BY id DESC LIMIT 1`,
    [owner, profileId]);
  if (!start.rowCount || start.rows[0].run_id !== runId) return { ok: false, status: 409, error: 'not_current_run' };

  const p = await c.query<{ reached: number; cleared: number; elapsed: number; format: string }>(
    `SELECT reached, cleared, EXTRACT(EPOCH FROM now() - started_at)::float AS elapsed, format
       FROM run_progress WHERE owner = $1 AND profile_id = $2 AND run_id = $3`,
    [owner, profileId, runId]);
  const row = p.rows[0];
  if (!row) return { ok: false, status: 409, error: 'not_current_run' };
  if (row.reached < floor)
    return { ok: false, status: 409, error: 'not_reached', extra: { reached: row.reached, floor } };
  const format = recordedFormat(row.format);
  const need = floor * format.minSecondsPerFloor;
  if (row.elapsed < need)
    return { ok: false, status: 409, error: 'too_soon', extra: { elapsed: Math.floor(row.elapsed), need } };
  return { ok: true, reached: row.reached, cleared: row.cleared, elapsed: row.elapsed, format };
}
