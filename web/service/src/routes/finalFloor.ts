import type { Request, Response } from 'express';
import { q, tx } from '../db.js';
import {
  FINAL_FLOOR, FinalDecision, MIN_SECONDS_PER_FLOOR, rollFinal, weekOf, weekStarts,
} from '../finalFloor.js';

/**
 * Floor 100's box (finalFloor.ts has the rules), for every browser player - guest or wallet, the
 * owner from the identity like every save:
 *
 *   POST /saves/final-floor             { profileId, runId }
 *        -> { tier, count, guaranteed, week, resetsAt, repeat }
 *   GET  /saves/final-floor/:profileId  -> { available, week, resetsAt }   the weekly guarantee
 *
 * Only the character's CURRENT run may claim - the latest run-start save (ServiceProfileStore
 * writes one at every run start) - and only once it is FINAL_FLOOR x MIN_SECONDS_PER_FLOOR old.
 * Without both, a modified client could start runs side by side, or claim seconds after a start,
 * and turn the 2% roll into a farm. One decision per run: a repeat answers the stored one.
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

const answer = (d: FinalDecision, repeat: boolean) =>
  ({ ...d, resetsAt: weekStarts(d.week + 1).toISOString(), repeat });

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
        'SELECT tier, count, guaranteed, week FROM final_floor WHERE profile_id = $1 AND run_id = $2',
        [profileId, runId]);
      if (prior.rowCount) return { status: 200, decision: prior.rows[0], repeat: true } as const;

      const start = await c.query<{ run_id: string | null; saved_at: Date }>(
        `SELECT run_id, saved_at FROM character_history
          WHERE owner = $1 AND profile_id = $2 AND reason = 'run-start' ORDER BY id DESC LIMIT 1`,
        [owner, profileId]);
      if (!start.rowCount || start.rows[0].run_id !== runId) return { status: 409, error: 'not_current_run' } as const;
      const elapsed = (Date.now() - new Date(start.rows[0].saved_at).getTime()) / 1000;
      const need = FINAL_FLOOR * MIN_SECONDS_PER_FLOOR;
      if (elapsed < need)
        return { status: 409, error: 'too_soon', extra: { elapsed: Math.floor(elapsed), need } } as const;

      const week = weekOf();
      const used = await c.query(
        'SELECT 1 FROM final_floor WHERE profile_id = $1 AND week = $2 AND guaranteed', [profileId, week]);
      const decision = rollFinal(!used.rowCount, week);
      await c.query(
        `INSERT INTO final_floor (profile_id, run_id, owner, week, guaranteed, tier, count)
         VALUES ($1, $2, $3, $4, $5, $6, $7)`,
        [profileId, runId, owner, week, decision.guaranteed, decision.tier, decision.count]);
      return { status: 200, decision, repeat: false } as const;
    });

    if (out.status !== 200) return fail(res, out.status, out.error, 'extra' in out ? out.extra : {});
    if (!out.repeat)
      console.log(`[final-floor] ${profileId} run ${runId}: ${out.decision.count} ${out.decision.tier}` +
        (out.decision.guaranteed ? ' (weekly guarantee)' : ''));
    res.json(answer(out.decision, out.repeat));
  } catch (e) {
    fail(res, 500, (e as Error).message);
  }
}

export async function finalFloorStatus(req: Request, res: Response): Promise<void> {
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    const ch = await q('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2', [res.locals.owner, profileId]);
    if (!ch.length) return fail(res, 404, 'not_found');
    const week = weekOf();
    const used = await q('SELECT 1 FROM final_floor WHERE profile_id = $1 AND week = $2 AND guaranteed',
      [profileId, week]);
    res.json({ available: used.length === 0, week, resetsAt: weekStarts(week + 1).toISOString() });
  } catch (e) {
    fail(res, 500, (e as Error).message);
  }
}
