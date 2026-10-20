import type { Request, Response } from 'express';
import { internal } from '../errors.js';
import type pg from 'pg';
import { q, tx } from '../db.js';
import { recordBeat, recordSnapshot } from '../runProgress.js';

/**
 * RUNS IN FLIGHT - a run survives a closed tab, a crash or a lost connection, and a player can
 * leave one and come back to it (CONTINUATION.md). One row per character: the run it is on, if any.
 * The game's ServiceRunStore is the only caller.
 *
 *   GET  /saves/runs/:profileId          the run waiting, or 404       { runId, seq, floor, stage, snapshot, beat }
 *   PUT  /saves/runs/:profileId          { runId, session, seq, snapshot }   a SNAPSHOT
 *   PUT  /saves/runs/:profileId/beat     { runId, session, seq, beat }       a HEARTBEAT
 *   POST /saves/runs/:profileId/claim    { runId, session }  -> the row     take the run over
 *   POST /saves/runs/:profileId/end      { runId, session }                  the run ended
 *
 * A SNAPSHOT is the whole run at a floor's start or its clear (RunSnapshot.cs), ~10-20 KB and a
 * few a floor; a HEARTBEAT is the health and armour since that snapshot, ~200 bytes every few
 * seconds while they change. A resume replays the floor from its snapshot at the LOWER of the
 * two healths, so closing the tab on a losing fight buys a replay of the floor, never a heal.
 *
 * ONE LIVE SESSION PER RUN. Every write carries the session that started or claimed the run; a
 * claim hands the run to a new session, and the old one's next write is refused (409
 * superseded) - the game then leaves the run without settling it. A snapshot must also be newer
 * than the stored one (its seq), so two in flight can never land out of order.
 *
 * A run must be the one the service SAW START: its id is the character's latest run-start save
 * (ServiceProfileStore writes one at every run start - finalFloor.ts reads the same row). A
 * resume writes no run-start, so the floor-100 clock and TotalRuns are untouched by it.
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;
const SESSION = /^[A-Za-z0-9-]{8,64}$/;
/** RunStage, in its declared order - JsonUtility writes an enum as its ordinal. */
const STAGES = ['Entering', 'Cleared'];
const stageOf = (v: unknown) => (typeof v === 'number' ? STAGES[v] : STAGES.includes(String(v)) ? String(v) : undefined);

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

/** A JSON body field the game sends as a string (JsonUtility.ToJson); an object is accepted too. */
function objectOf(raw: unknown): Record<string, unknown> | null {
  try {
    const v = typeof raw === 'string' ? JSON.parse(raw) : raw;
    return v && typeof v === 'object' && !Array.isArray(v) ? (v as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

const intOf = (v: unknown) => (Number.isInteger(Number(v)) ? Number(v) : NaN);

interface Row {
  run_id: string;
  session: string;
  seq: number;
  floor: number;
  stage: string;
  snapshot: unknown;
  beat: unknown;
  beat_seq: number | null;
  saved_at: Date;
}

/** The character's latest run start - the only run a snapshot may be written for. */
async function latestRunStart(c: pg.PoolClient, owner: string, profileId: string): Promise<string | null> {
  const r = await c.query<{ run_id: string | null }>(
    `SELECT run_id FROM character_history
      WHERE owner = $1 AND profile_id = $2 AND reason = 'run-start' ORDER BY id DESC LIMIT 1`,
    [owner, profileId]);
  return r.rows[0]?.run_id ?? null;
}

const reply = (r: Row) => ({
  runId: r.run_id,
  seq: r.seq,
  floor: r.floor,
  stage: r.stage,
  snapshot: r.snapshot,
  // A beat is only the floor's since ITS snapshot - an older one says nothing about this one.
  beat: r.beat_seq === r.seq ? r.beat : null,
  savedAt: r.saved_at,
});

export async function loadRun(req: Request, res: Response): Promise<void> {
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    const rows = await q<Row>('SELECT * FROM runs WHERE owner = $1 AND profile_id = $2',
      [res.locals.owner, profileId]);
    if (!rows.length) return fail(res, 404, 'not_found');
    res.json(reply(rows[0]));
  } catch (e) {
    internal(res, e, 'runs');
  }
}

export async function saveRun(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  const runId = String(req.body?.runId ?? '');
  const session = String(req.body?.session ?? '');
  const seq = intOf(req.body?.seq);
  const snapshot = objectOf(req.body?.snapshot);
  if (!ID.test(profileId) || !ID.test(runId)) return fail(res, 400, 'profileId and runId are required');
  if (!SESSION.test(session)) return fail(res, 400, 'bad session');
  if (!(seq >= 0)) return fail(res, 400, 'seq must be a whole number');
  if (!snapshot) return fail(res, 400, 'snapshot must be the RunSnapshot JSON');
  // The key and the body win over whatever the snapshot claims, as for every save.
  snapshot.ProfileId = profileId;
  snapshot.RunId = runId;
  snapshot.Seq = seq;
  const floor = Math.max(0, Math.floor(Number(snapshot.Floor ?? 0)));
  const stage = stageOf(snapshot.Stage);
  if (!stage) return fail(res, 400, `stage must be one of ${STAGES.join(', ')}`);

  try {
    const out = await tx(async c => {
      const ch = await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE',
        [owner, profileId]);
      if (!ch.rowCount) return { status: 404, error: 'not_found' } as const;
      const cur = await c.query<Row>('SELECT * FROM runs WHERE owner = $1 AND profile_id = $2', [owner, profileId]);
      const row = cur.rows[0];

      if (row && row.run_id === runId) {
        if (row.session !== session) return { status: 409, error: 'superseded' } as const;
        if (seq <= row.seq) return { status: 409, error: 'stale_seq', extra: { stored: row.seq } } as const;
      }
      // A run the service never saw start - or an older one, once a newer run has begun.
      if (await latestRunStart(c, owner, profileId) !== runId)
        return { status: 409, error: 'not_current_run' } as const;

      await c.query(
        `INSERT INTO runs (owner, profile_id, run_id, session, seq, floor, stage, snapshot)
         VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
         ON CONFLICT (owner, profile_id) DO UPDATE SET
           run_id = $3, session = $4, seq = $5, floor = $6, stage = $7, snapshot = $8,
           beat = NULL, beat_seq = NULL, beat_at = NULL, saved_at = now(),
           started_at = CASE WHEN runs.run_id = $3 THEN runs.started_at ELSE now() END`,
        [owner, profileId, runId, session, seq, floor, stage, snapshot]);
      // How far the run has got, as the service saw it - what its end is minted against.
      await recordSnapshot(c, owner, profileId, runId, floor, stage, session);
      return { status: 200 } as const;
    });
    if (out.status !== 200) return fail(res, out.status, out.error, 'extra' in out ? out.extra : {});
    res.json({ ok: true, seq });
  } catch (e) {
    internal(res, e, 'runs');
  }
}

export async function beatRun(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  const runId = String(req.body?.runId ?? '');
  const session = String(req.body?.session ?? '');
  const seq = intOf(req.body?.seq);
  const beat = objectOf(req.body?.beat);
  if (!ID.test(profileId) || !ID.test(runId)) return fail(res, 400, 'profileId and runId are required');
  if (!SESSION.test(session) || !(seq >= 0)) return fail(res, 400, 'session and seq are required');
  if (!beat) return fail(res, 400, 'beat must be the RunBeat JSON');

  try {
    const done = await q(
      `UPDATE runs SET beat = $5, beat_seq = $4, beat_at = now()
        WHERE owner = $1 AND profile_id = $2 AND run_id = $3 AND session = $6 AND seq = $4
        RETURNING 1 AS hit`,
      [owner, profileId, runId, seq, beat, session]);
    if (done.length === 0) {
      // Nothing matched - ask which refusal it was.
      const rows = await q<Row>('SELECT run_id, session, seq FROM runs WHERE owner = $1 AND profile_id = $2',
        [owner, profileId]);
      const row = rows[0];
      if (!row || row.run_id !== runId) return fail(res, 404, 'not_found');
      if (row.session !== session) return fail(res, 409, 'superseded');
      if (row.seq !== seq) return void res.json({ ok: false, stale: true, stored: row.seq });
    } else {
      // The run was still being played now - where an abandoned floor attempt's clock stops.
      await recordBeat(owner, profileId, runId);
    }
    res.json({ ok: true });
  } catch (e) {
    internal(res, e, 'runs');
  }
}

export async function claimRun(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  const runId = String(req.body?.runId ?? '');
  const session = String(req.body?.session ?? '');
  if (!ID.test(profileId) || !ID.test(runId)) return fail(res, 400, 'profileId and runId are required');
  if (!SESSION.test(session)) return fail(res, 400, 'bad session');
  try {
    const rows = await q<Row>(
      `UPDATE runs SET session = $4 WHERE owner = $1 AND profile_id = $2 AND run_id = $3 RETURNING *`,
      [owner, profileId, runId, session]);
    if (!rows.length) return fail(res, 404, 'not_found');
    res.json(reply(rows[0]));
  } catch (e) {
    internal(res, e, 'runs');
  }
}

export async function endRun(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  const runId = String(req.body?.runId ?? '');
  const session = String(req.body?.session ?? '');
  if (!ID.test(profileId) || !ID.test(runId)) return fail(res, 400, 'profileId and runId are required');
  if (!SESSION.test(session)) return fail(res, 400, 'bad session');
  try {
    const out = await tx(async c => {
      const cur = await c.query<Row>(
        'SELECT run_id, session FROM runs WHERE owner = $1 AND profile_id = $2 FOR UPDATE', [owner, profileId]);
      const row = cur.rows[0];
      if (!row || row.run_id !== runId) return { status: 200, ended: false } as const;
      if (row.session !== session) return { status: 409 } as const;
      await c.query('DELETE FROM runs WHERE owner = $1 AND profile_id = $2', [owner, profileId]);
      return { status: 200, ended: true } as const;
    });
    if (out.status === 409) return fail(res, 409, 'superseded');
    res.json({ ended: out.ended });
  } catch (e) {
    internal(res, e, 'runs');
  }
}
