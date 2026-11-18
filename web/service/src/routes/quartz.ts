import type { Request, Response } from 'express';
import { internal } from '../errors.js';
import { tx } from '../db.js';
import { currentRunAt, runFormatOf } from '../runProgress.js';
import { enemyBand, quartzFloorChance } from '../runFormat.js';
import { chance, credit } from '../cosmetics.js';

/**
 * A FLOOR'S QUARTZ ROLL (GEAR.md "Quartz"): the last enemy of a cleared floor rolls a Quartz box at
 * its enemy band's rate (runFormat.ts quartzBandRates) - asked by the game at the clear, decided and
 * credited HERE, once per run per floor (a retry gets the same answer), so a player cannot roll it
 * twice or name the outcome. The game never asks on a puzzle floor (no box there). The showcase's
 * format rolls none.
 *
 *   POST /saves/quartz-floor   { profileId, runId, floor } -> { floor, boxes, repeat }
 *   POST /saves/quartz-maze    { profileId, runId, floor, take } -> { present } | { boxes }
 *
 * THE LABYRINTH'S BOX: decided once per run, when the game enters the first maze it may stand in
 * (the last band's, or band 3's - the game reads its own forecast; the service holds the band
 * floor and the one-a-run rule); credited only when taken, and only once.
 *
 * Credited at once, like an avatar's rare dye: the box is the character's whatever becomes of the run.
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

export async function claimQuartzFloor(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.body?.profileId ?? '');
  const runId = String(req.body?.runId ?? '');
  const floor = Number(req.body?.floor);
  if (!ID.test(profileId) || !ID.test(runId)) return fail(res, 400, 'profileId and runId are required');
  if (!Number.isInteger(floor) || floor <= 0) return fail(res, 400, 'floor must be a floor number');

  try {
    const out = await tx(async (c) => {
      const ch = await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE', [owner, profileId]);
      if (!ch.rowCount) return { status: 404, error: 'not_found' } as const;
      const format = await runFormatOf(c, owner, profileId, runId);
      if (!format) return { status: 409, error: 'not_current_run' } as const;

      const prior = await c.query<{ boxes: number }>(
        'SELECT boxes FROM quartz_floors WHERE owner = $1 AND profile_id = $2 AND run_id = $3 AND floor = $4',
        [owner, profileId, runId, floor]);
      if (prior.rowCount) return { status: 200, boxes: prior.rows[0].boxes, repeat: true } as const;

      const p = quartzFloorChance(format, floor);
      if (p <= 0 || floor > format.finalFloor) return { status: 200, boxes: 0, repeat: false, none: true } as const;
      const run = await currentRunAt(c, owner, profileId, runId, floor);
      if (!run.ok) return { status: run.status, error: run.error, extra: run.extra } as const;

      const boxes = chance(p) ? 1 : 0;
      await c.query('INSERT INTO quartz_floors (owner, profile_id, run_id, floor, boxes) VALUES ($1, $2, $3, $4, $5)',
        [owner, profileId, runId, floor, boxes]);
      if (boxes) await credit(c, owner, profileId, 'quartzbox', '', boxes, `floor:${runId}:${floor}`);
      return { status: 200, boxes, repeat: false } as const;
    });
    if (out.status !== 200) return fail(res, out.status, out.error, 'extra' in out ? out.extra ?? {} : {});
    if (out.boxes && !out.repeat) console.log(`[quartz] ${profileId} run ${runId} floor ${floor}: a Quartz box`);
    res.json({ floor, boxes: out.boxes, repeat: out.repeat });
  } catch (e) {
    internal(res, e, 'quartz-floor');
  }
}

export async function claimQuartzMaze(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.body?.profileId ?? '');
  const runId = String(req.body?.runId ?? '');
  const floor = Number(req.body?.floor);
  const take = req.body?.take === true;
  if (!ID.test(profileId) || !ID.test(runId)) return fail(res, 400, 'profileId and runId are required');
  if (!Number.isInteger(floor) || floor <= 0) return fail(res, 400, 'floor must be a floor number');

  try {
    const out = await tx(async (c) => {
      const ch = await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE', [owner, profileId]);
      if (!ch.rowCount) return { status: 404, error: 'not_found' } as const;
      const format = await runFormatOf(c, owner, profileId, runId);
      if (!format) return { status: 409, error: 'not_current_run' } as const;

      const prior = await c.query<{ floor: number; present: boolean; taken: boolean }>(
        'SELECT floor, present, taken FROM quartz_maze WHERE owner = $1 AND profile_id = $2 AND run_id = $3 FOR UPDATE',
        [owner, profileId, runId]);
      const row = prior.rows[0];

      if (take) {
        if (!row || !row.present || row.taken || row.floor !== floor) return { status: 200, boxes: 0 } as const;
        await c.query('UPDATE quartz_maze SET taken = true WHERE owner = $1 AND profile_id = $2 AND run_id = $3',
          [owner, profileId, runId]);
        await credit(c, owner, profileId, 'quartzbox', '', 1, `maze:${runId}:${floor}`);
        return { status: 200, boxes: 1 } as const;
      }

      // One decision a run: any later maze, or this one asked again, answers what was decided.
      if (row) return { status: 200, present: row.present && !row.taken && row.floor === floor } as const;
      if (format.quartzMazeChance <= 0 || enemyBand(format, floor) < format.quartzMazeMinBand || floor > format.finalFloor)
        return { status: 200, present: false } as const;
      const run = await currentRunAt(c, owner, profileId, runId, floor);
      if (!run.ok) return { status: run.status, error: run.error, extra: run.extra } as const;
      const present = chance(format.quartzMazeChance);
      await c.query('INSERT INTO quartz_maze (owner, profile_id, run_id, floor, present) VALUES ($1, $2, $3, $4, $5)',
        [owner, profileId, runId, floor, present]);
      return { status: 200, present } as const;
    });
    if (out.status !== 200) return fail(res, out.status, out.error, 'extra' in out ? out.extra ?? {} : {});
    if ('boxes' in out) {
      if (out.boxes) console.log(`[quartz] ${profileId} run ${runId} floor ${floor}: the labyrinth's box taken`);
      return void res.json({ floor, boxes: out.boxes });
    }
    res.json({ floor, present: out.present });
  } catch (e) {
    internal(res, e, 'quartz-maze');
  }
}
