import type { Request, Response } from 'express';
import { internal } from '../errors.js';
import { tx } from '../db.js';
import { currentRunAt, runFormatOf } from '../runProgress.js';
import {
  CHARGES_PER_BOX, Change, credit, itemsOf, milestoneAt, recolourCost,
} from '../cosmetics.js';

/**
 * WHAT THE DATABASE HOLDS FOR A CHARACTER (cosmetics.ts has the rules), for every browser player -
 * guest or wallet, the owner from the identity like every save:
 *
 *   GET  /saves/items/:profileId                  -> Items
 *   POST /saves/items/:profileId/recolour         { changes: [{ piece, swatches }] } -> Items
 *   POST /saves/items/:profileId/break-box        -> Items     a Diamond box into 7 charges
 *   POST /saves/milestone   { profileId, runId, floor } -> { milestone, diamond, dye, repeat } | { none }
 *
 * The SAVE never carries any of this with authority - the game's copy (CharacterProfile.Items) is
 * overwritten by these answers. Withdrawing dyes to the wallet, depositing dye tokens and breaking
 * box TOKENS are chain transactions (routes/dyes.ts).
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;
const PIECE = /^[a-z0-9_]{1,64}$/;
const SWATCH = /^dye\.[a-z0-9_]{1,32}$/;
const MAX_CHANGES = 32;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

export async function loadItems(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    const out = await tx(async (c) => {
      const ch = await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2', [owner, profileId]);
      return ch.rowCount ? itemsOf(c, owner, profileId) : null;
    });
    if (!out) return fail(res, 404, 'not_found');
    res.json(out);
  } catch (e) {
    internal(res, e, 'items');
  }
}

/** A recolour session: every change priced together, spent and written at once - or nothing. */
export async function recolour(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  const raw = req.body?.changes;
  if (!Array.isArray(raw) || raw.length === 0 || raw.length > MAX_CHANGES)
    return fail(res, 400, `changes must be 1-${MAX_CHANGES} entries`);
  const changes: Change[] = [];
  for (const r of raw) {
    const piece = String(r?.piece ?? '');
    const sw = Array.isArray(r?.swatches) ? r.swatches : null;
    if (!PIECE.test(piece) || !sw || sw.length > 4) return fail(res, 400, 'each change is { piece, swatches[] }');
    const swatches = sw.map((x: unknown) => (x ? String(x) : null));
    if (swatches.some((x: string | null) => x !== null && !SWATCH.test(x))) return fail(res, 400, 'bad swatch id');
    changes.push({ piece, swatches });
  }

  try {
    const out = await tx(async (c) => {
      // The character's row is the lock: one session for a character at a time.
      const ch = await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE', [owner, profileId]);
      if (!ch.rowCount) return { status: 404, error: 'not_found' } as const;

      const pieces = [...new Set(changes.map((x) => x.piece))];
      const cur = await c.query<{ piece: string; swatches: (string | null)[] }>(
        'SELECT piece, swatches FROM dyed_pieces WHERE owner = $1 AND profile_id = $2 AND piece = ANY($3)',
        [owner, profileId, pieces]);
      const current = new Map(cur.rows.map((r) => [r.piece, r.swatches]));
      const price = recolourCost(current, changes);
      if (price.refusal) return { status: 400, error: 'refused', extra: { reason: price.refusal } } as const;

      const held = await itemsOf(c, owner, profileId);
      if (held.charges < price.charges)
        return { status: 402, error: 'not_enough_charges', extra: { have: held.charges, need: price.charges } } as const;
      for (const [swatch, n] of Object.entries(price.dyes)) {
        const have = held.dyes.find((d) => d.swatch === swatch)?.count ?? 0;
        if (have < n) return { status: 402, error: 'not_enough_dye', extra: { swatch, have, need: n } } as const;
      }

      const reason = 'recolour';
      if (price.charges) await credit(c, owner, profileId, 'charge', '', -price.charges, reason);
      for (const [swatch, n] of Object.entries(price.dyes)) await credit(c, owner, profileId, 'dye', swatch, -n, reason);
      // The FINAL colours of each piece (the last change to it wins, as priced).
      const final = new Map<string, (string | null)[]>();
      for (const ch2 of changes) final.set(ch2.piece, ch2.swatches);
      for (const [piece, swatches] of final) {
        if (swatches.every((x) => !x))
          await c.query('DELETE FROM dyed_pieces WHERE owner = $1 AND profile_id = $2 AND piece = $3', [owner, profileId, piece]);
        else
          await c.query(
            `INSERT INTO dyed_pieces (owner, profile_id, piece, swatches) VALUES ($1, $2, $3, $4)
             ON CONFLICT (owner, profile_id, piece) DO UPDATE SET swatches = $4, updated_at = now()`,
            [owner, profileId, piece, JSON.stringify(swatches.map((x) => x ?? ''))]);
      }
      return { status: 200, items: await itemsOf(c, owner, profileId), price } as const;
    });
    if (out.status !== 200) return fail(res, out.status, out.error, 'extra' in out ? out.extra : {});
    res.json(out.items);
  } catch (e) {
    internal(res, e, 'items');
  }
}

/**
 * A DIAMOND BOX BROKEN INTO CHARGES - it mints nothing (~0.2 ADA where a design costs ~3.6). The box
 * spent is one the SERVICE holds for the character (held_boxes - earned without a PFP) when there is
 * one, else one the GUEST's save counts (a guest's boxes have only ever lived in its save - the
 * same trust as a guest's own Forge opens; the save's count is lowered here so it cannot be spent
 * twice). A box TOKEN in a wallet is burned by a transaction instead (routes/dyes.ts).
 */
export async function breakBox(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    const out = await tx(async (c) => {
      const ch = await c.query<{ save: { Boxes?: { Diamond?: number } } }>(
        'SELECT save FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE', [owner, profileId]);
      if (!ch.rowCount) return { status: 404, error: 'not_found' } as const;

      const held = await c.query<{ id: string; count: number }>(
        `SELECT id, count FROM held_boxes WHERE owner = $1 AND profile_id = $2 AND tier = 'Diamond'
           AND released_at IS NULL AND count > 0 ORDER BY id LIMIT 1 FOR UPDATE`, [owner, profileId]);
      let source: string;
      if (held.rowCount) {
        const row = held.rows[0];
        if (row.count > 1) await c.query('UPDATE held_boxes SET count = count - 1 WHERE id = $1', [row.id]);
        else await c.query('UPDATE held_boxes SET count = 0, released_at = now() WHERE id = $1', [row.id]);
        source = 'held';
      } else if (owner.startsWith('guest:') && Number(ch.rows[0].save?.Boxes?.Diamond ?? 0) > 0) {
        await c.query(
          `UPDATE characters SET save = jsonb_set(save, '{Boxes,Diamond}', to_jsonb((save->'Boxes'->>'Diamond')::int - 1)),
                  updated_at = now() WHERE owner = $1 AND profile_id = $2`, [owner, profileId]);
        source = 'save';
      } else {
        return { status: 402, error: 'no_diamond_box' } as const;
      }
      await credit(c, owner, profileId, 'charge', '', CHARGES_PER_BOX, `break-box:${source}`);
      return { status: 200, items: await itemsOf(c, owner, profileId), source } as const;
    });
    if (out.status !== 200) return fail(res, out.status, out.error);
    res.json({ ...out.items, source: out.source });
  } catch (e) {
    internal(res, e, 'items');
  }
}

/**
 * A MILESTONE, asked at a floor's clear (GameBootstrap.ClaimMilestone): the first clear of each of
 * the run format's milestone floors, ONCE PER CHARACTER. Only the current run, its snapshots on that
 * floor, the clock allowing it (runProgress.currentRunAt). The dye is credited here; the first
 * milestone's Diamond box joins the RUN's loot and commit-run mints it like an avatar's box.
 */
export async function claimMilestone(req: Request, res: Response): Promise<void> {
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
      const roll = milestoneAt(format.id, floor);
      if (!roll) return { status: 200, none: true } as const;

      const prior = await c.query<{ run_id: string; diamond: number; dye: string | null }>(
        'SELECT run_id, diamond, dye FROM milestones WHERE owner = $1 AND profile_id = $2 AND milestone = $3',
        [owner, profileId, roll.milestone]);
      if (prior.rowCount) {
        const p = prior.rows[0];
        // Asked again by the run that earned it: the same answer. Any other run: passed already.
        if (p.run_id !== runId) return { status: 200, none: true } as const;
        return { status: 200, milestone: roll.milestone, diamond: p.diamond, dye: p.dye, repeat: true } as const;
      }

      const run = await currentRunAt(c, owner, profileId, runId, floor);
      if (!run.ok) return { status: run.status, error: run.error, extra: run.extra } as const;

      await c.query(
        `INSERT INTO milestones (owner, profile_id, milestone, run_id, floor, diamond, dye)
         VALUES ($1, $2, $3, $4, $5, $6, $7)`,
        [owner, profileId, roll.milestone, runId, floor, roll.diamond, roll.dye]);
      if (roll.dye) await credit(c, owner, profileId, 'dye', roll.dye, 1, `milestone:${roll.milestone}`);
      return { status: 200, milestone: roll.milestone, diamond: roll.diamond, dye: roll.dye, repeat: false } as const;
    });
    if (out.status !== 200) return fail(res, out.status, out.error, 'extra' in out ? out.extra ?? {} : {});
    if ('none' in out) return void res.json({ none: true, floor });
    if (!out.repeat) console.log(`[milestone] ${profileId} run ${runId} floor ${floor}: ${out.milestone}` +
      (out.diamond ? ', a Diamond box' : '') + (out.dye ? `, dye ${out.dye}` : ''));
    res.json({ floor, milestone: out.milestone, diamond: out.diamond, dye: out.dye ?? '', repeat: out.repeat });
  } catch (e) {
    internal(res, e, 'milestone');
  }
}

/** Every milestone Diamond box a run decided - what commit-run may mint from (allowance). */
export async function milestoneBoxes(c: Pick<import('pg').PoolClient, 'query'>, owner: string, profileId: string,
                                      runId: string): Promise<{ floor: number; diamond: number }[]> {
  const r = await c.query<{ floor: number; diamond: number }>(
    'SELECT floor, diamond FROM milestones WHERE owner = $1 AND profile_id = $2 AND run_id = $3 AND diamond > 0',
    [owner, profileId, runId]);
  return r.rows;
}
