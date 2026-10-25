import type { Request, Response } from 'express';
import { internal } from '../errors.js';
import { q, tx } from '../db.js';
import { FinalDecision, rollFinal, weekOf, weekStarts } from '../finalFloor.js';
import { currentRunAt, runClock, runFormatOf } from '../runProgress.js';
import { credit, posesOwned, rollPose } from '../cosmetics.js';

/**
 * The final floor's box (finalFloor.ts has the rules; the run's format names the floor), for every
 * browser player - guest or wallet, the
 * owner from the identity like every save:
 *
 *   POST /saves/final-floor             { profileId, runId }
 *        -> { tier, count, guaranteed, week, resetsAt, repeat }
 *   GET  /saves/final-floor/:profileId  -> { available, week, resetsAt }   the weekly guarantee
 *
 * Only the character's CURRENT run may claim - the latest run-start save (ServiceProfileStore
 * writes one at every run start) - once its SNAPSHOTS show it on its format's final floor and the
 * server's clock allows that floor at the format's seconds a floor (runProgress.currentRunAt).
 * Without these, a modified client could start runs side by side, or claim without playing, and
 * farm the guarantee. The Short Descent's guarantee also needs the final floor reached inside the
 * hour on the service's own run clock (runProgress.runClock). One decision per run: a repeat answers the stored one.
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

const answer = (d: FinalDecision, repeat: boolean) =>
  ({ tier: d.tier, count: d.count, guaranteed: d.guaranteed, week: d.week,
     dye: d.dye ?? '', pose: d.pose ?? '', poseGold: d.pose_gold ?? 0,
     resetsAt: weekStarts(d.week + 1).toISOString(), repeat });

export async function claimFinalFloor(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.body?.profileId ?? '');
  const runId = String(req.body?.runId ?? '');
  if (!ID.test(profileId) || !ID.test(runId)) return fail(res, 400, 'profileId and runId are required');

  try {
    const out = await tx(async c => {
      // The character's row is the lock: two claims for one character are decided one at a time.
      const ch = await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE',
        [owner, profileId]);
      if (!ch.rowCount) return { status: 404, error: 'not_found' } as const;

      const prior = await c.query<FinalDecision>(
        'SELECT tier, count, guaranteed, week, dye, pose, pose_gold FROM final_floor WHERE owner = $1 AND profile_id = $2 AND run_id = $3',
        [owner, profileId, runId]);
      if (prior.rowCount) return { status: 200, decision: prior.rows[0], repeat: true } as const;

      // The run's format names its final floor (Classic 100, the Short Descent 50); then the current
      // run, its snapshots on that floor, and the server's clock allowing it.
      const format = await runFormatOf(c, owner, profileId, runId);
      if (!format) return { status: 409, error: 'not_current_run', extra: {} } as const;
      const run = await currentRunAt(c, owner, profileId, runId, format.finalFloor);
      if (!run.ok) return { status: run.status, error: run.error, extra: run.extra ?? {} } as const;

      const week = weekOf();
      const used = await c.query(
        'SELECT 1 FROM final_floor WHERE owner = $1 AND profile_id = $2 AND week = $3 AND guaranteed',
        [owner, profileId, week]);
      // The Short Descent's guarantee is for a clear before the Gate Collapse - inside the hour on
      // the RUN CLOCK the service measured itself (runProgress.runClock). Past it, the clear takes
      // the later-clear rolls and the week's guarantee stays unspent.
      const clock = format.guaranteeClockSeconds > 0 ? await runClock(c, owner, profileId, runId) : 0;
      const inTime = format.guaranteeClockSeconds <= 0 || clock <= format.guaranteeClockSeconds;
      const decision = rollFinal(format, !used.rowCount && inTime, week);
      // Every final clear is an avatar kill: a pose roll (cosmetics.ts). Cosmetics are credited
      // HERE, database first - the character's from this moment, whatever becomes of the run.
      const pose = rollPose(await posesOwned(c, owner, profileId));
      decision.pose = pose.pose;
      decision.pose_gold = pose.gold;
      await c.query(
        `INSERT INTO final_floor (profile_id, run_id, owner, week, guaranteed, tier, count, dye, pose, pose_gold)
         VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)`,
        [profileId, runId, owner, week, decision.guaranteed, decision.tier, decision.count,
         decision.dye ?? null, decision.pose, decision.pose_gold]);
      if (decision.dye) await credit(c, owner, profileId, 'dye', decision.dye, 1, `final:${runId}`);
      if (decision.pose) await credit(c, owner, profileId, 'pose', decision.pose, 1, `final:${runId}`);
      return { status: 200, decision, repeat: false } as const;
    });

    if (out.status !== 200) return fail(res, out.status, out.error, 'extra' in out ? out.extra : {});
    if (!out.repeat)
      console.log(`[final-floor] ${profileId} run ${runId}: ${out.decision.count} ${out.decision.tier}` +
        (out.decision.guaranteed ? ' (weekly guarantee)' : ''));
    res.json(answer(out.decision, out.repeat));
  } catch (e) {
    internal(res, e, 'final-floor');
  }
}

export async function finalFloorStatus(req: Request, res: Response): Promise<void> {
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    const ch = await q('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2', [res.locals.owner, profileId]);
    if (!ch.length) return fail(res, 404, 'not_found');
    const week = weekOf();
    const used = await q('SELECT 1 FROM final_floor WHERE owner = $1 AND profile_id = $2 AND week = $3 AND guaranteed',
      [res.locals.owner, profileId, week]);
    res.json({ available: used.length === 0, week, resetsAt: weekStarts(week + 1).toISOString() });
  } catch (e) {
    internal(res, e, 'final-floor');
  }
}
