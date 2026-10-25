import type { Request, Response } from 'express';
import { internal } from '../errors.js';
import { q, tx } from '../db.js';
import { guestId, guestOwner } from '../identity.js';
import { recordRunStart } from '../runProgress.js';
import { accepts, formatOf } from '../runFormat.js';

/**
 * SAVES - every browser player's characters and hub room, held by the service from the first run.
 * The game's ServiceProfileStore / ServiceAccountStore are the only callers.
 *
 *   GET  /saves/characters              the owner's roster        {"items":[CharacterProfile...]}
 *   GET  /saves/characters/:profileId   one save, or 404 - and ONLY 404 means "no such character"
 *   PUT  /saves/characters/:profileId   { datum, reason, runId? } -> { hash }
 *   GET  /saves/account                 the hub room, or 404
 *   PUT  /saves/account                 { datum } -> { hash }
 *   POST /saves/graduate                { guest } under a WALLET session -> { moved }
 *
 * A run in flight is routes/runs.ts - a separate row per character, written several times a floor.
 *
 * The owner comes from the identity (identity.ts), never from the body, so nobody writes into a
 * save that is not theirs. Saves are keyed (owner, profileId): two guests may both have a
 * "local-dev-0" without colliding.
 *
 * THE SAVE NEVER GOES BACKWARDS. A save with fewer TotalRuns than the stored one is refused (409
 * stale_save). The store a save comes from loads a FRESH character when a read fails, and without
 * this the next checkpoint would write that fresh character over the real one.
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;
const REASONS = new Set(['run-start', 'run-end', 'unlock', 'create']);
const MAX_CHARACTERS = Number(process.env.MAX_CHARACTERS_PER_OWNER ?? 64);
const HISTORY_KEPT = 20;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

/** The body's datum - the game sends it as a JSON string (JsonUtility.ToJson); an object is accepted too. */
function datumOf(body: unknown): Record<string, unknown> | null {
  const raw = (body as { datum?: unknown } | undefined)?.datum;
  try {
    const v = typeof raw === 'string' ? JSON.parse(raw) : raw;
    return v && typeof v === 'object' && !Array.isArray(v) ? (v as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

export async function roster(_req: Request, res: Response): Promise<void> {
  try {
    const rows = await q<{ save: unknown }>(
      'SELECT save FROM characters WHERE owner = $1 ORDER BY profile_id', [res.locals.owner]);
    res.json({ items: rows.map(r => r.save) });
  } catch (e) {
    internal(res, e, 'saves');
  }
}

export async function loadCharacter(req: Request, res: Response): Promise<void> {
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    const rows = await q<{ save: unknown }>(
      'SELECT save FROM characters WHERE owner = $1 AND profile_id = $2', [res.locals.owner, profileId]);
    if (!rows.length) return fail(res, 404, 'not_found');
    res.json(rows[0].save);
  } catch (e) {
    internal(res, e, 'saves');
  }
}

export async function saveCharacter(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  const reason = String(req.body?.reason ?? '');
  const runId = req.body?.runId ? String(req.body.runId) : null;
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  if (!REASONS.has(reason)) return fail(res, 400, `reason must be one of ${[...REASONS].join(', ')}`);
  if (runId && !ID.test(runId)) return fail(res, 400, 'bad run id');
  const datum = datumOf(req.body);
  if (!datum) return fail(res, 400, 'datum must be the CharacterProfile JSON');
  datum.ProfileId = profileId;   // the key wins over whatever the datum claims, as in every store
  const totalRuns = Math.max(0, Math.floor(Number(datum.TotalRuns ?? 0)));
  // A run starts under a FORMAT (runFormat.ts) - none named is Classic, every build before formats -
  // and only under one this service accepts (RUN_FORMATS).
  const format = formatOf(req.body?.format);
  if (reason === 'run-start') {
    if (!format) return fail(res, 400, 'unknown run format');
    if (!accepts(format)) return fail(res, 409, 'format_not_accepted', { format: format.id });
  }

  try {
    const out = await tx(async c => {
      const cur = await c.query<{ total_runs: number }>(
        'SELECT total_runs FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE', [owner, profileId]);
      if (cur.rowCount && totalRuns < cur.rows[0].total_runs)
        return { status: 409 as const, stored: cur.rows[0].total_runs };
      if (!cur.rowCount) {
        const n = await c.query<{ n: string }>('SELECT count(*) AS n FROM characters WHERE owner = $1', [owner]);
        if (Number(n.rows[0].n) >= MAX_CHARACTERS) return { status: 403 as const, stored: 0 };
      }
      await c.query(
        `INSERT INTO characters (owner, profile_id, save, total_runs) VALUES ($1, $2, $3, $4)
         ON CONFLICT (owner, profile_id) DO UPDATE SET save = $3, total_runs = $4, updated_at = now()`,
        [owner, profileId, datum, totalRuns]);
      const h = await c.query<{ id: string }>(
        `INSERT INTO character_history (owner, profile_id, reason, run_id, save) VALUES ($1, $2, $3, $4, $5)
         RETURNING id`, [owner, profileId, reason, runId, datum]);
      // A run's clock starts HERE, on the server's time - what its end is judged against.
      if (reason === 'run-start' && runId && format) await recordRunStart(c, owner, profileId, runId, format);
      await c.query(
        `DELETE FROM character_history WHERE owner = $1 AND profile_id = $2 AND id NOT IN (
           SELECT id FROM character_history WHERE owner = $1 AND profile_id = $2 ORDER BY id DESC LIMIT $3)`,
        [owner, profileId, HISTORY_KEPT]);
      return { status: 200 as const, version: h.rows[0].id };
    });
    if (out.status === 409) return fail(res, 409, 'stale_save', { storedRuns: out.stored, sentRuns: totalRuns });
    if (out.status === 403) return fail(res, 403, 'too_many_characters', { max: MAX_CHARACTERS });
    // Not a transaction hash and not pretending to be one: TxLog shows it, and the day a save is
    // also recorded on chain, the chain write carries its own.
    res.json({ hash: `save:${reason}:${out.version}` });
  } catch (e) {
    internal(res, e, 'saves');
  }
}

export async function loadAccount(_req: Request, res: Response): Promise<void> {
  try {
    const rows = await q<{ save: unknown }>('SELECT save FROM accounts WHERE owner = $1', [res.locals.owner]);
    if (!rows.length) return fail(res, 404, 'not_found');
    res.json(rows[0].save);
  } catch (e) {
    internal(res, e, 'saves');
  }
}

export async function saveAccount(req: Request, res: Response): Promise<void> {
  const datum = datumOf(req.body);
  if (!datum) return fail(res, 400, 'datum must be the AccountProfile JSON');
  try {
    await q(
      `INSERT INTO accounts (owner, save) VALUES ($1, $2)
       ON CONFLICT (owner) DO UPDATE SET save = $2, updated_at = now()`, [res.locals.owner, datum]);
    res.json({ hash: 'save:account' });
  } catch (e) {
    internal(res, e, 'saves');
  }
}

/**
 * A guest GRADUATES onto the wallet it just proved: its characters and hub room move to the
 * address, and the guest token is spent. The service saw every one of those runs, so nothing
 * unverified comes with them.
 *
 * Only into a wallet with NO records (the hub room's rule, GameBootstrap.ConnectWallet): adopting
 * into a wallet that already has characters would let one browser's saves be absorbed by another
 * player's wallet. Then nothing moves (409 wallet_has_records) and the guest stays as it was.
 */
export async function graduate(req: Request, res: Response): Promise<void> {
  if (res.locals.kind !== 'wallet') return fail(res, 401, 'graduating needs a proven wallet session');
  const wallet = res.locals.owner as string;
  const gid = guestId(String(req.body?.guest ?? ''));
  if (!gid) return fail(res, 400, 'guest must be a guest token');
  const from = guestOwner(gid);

  try {
    const out = await tx(async c => {
      const g = await c.query<{ graduated_to: string | null }>(
        'SELECT graduated_to FROM guests WHERE id = $1 FOR UPDATE', [gid]);
      if (!g.rowCount) return { status: 404 as const };
      if (g.rows[0].graduated_to)
        return g.rows[0].graduated_to === wallet ? { status: 200 as const, moved: 0, repeat: true }
                                                  : { status: 409 as const, error: 'guest_already_graduated' };

      const has = await c.query(
        `SELECT 1 FROM characters WHERE owner = $1 UNION ALL SELECT 1 FROM accounts WHERE owner = $1 LIMIT 1`,
        [wallet]);
      if (has.rowCount) return { status: 409 as const, error: 'wallet_has_records' };

      const moved = await c.query('UPDATE characters SET owner = $1, updated_at = now() WHERE owner = $2', [wallet, from]);
      await c.query('UPDATE character_history SET owner = $1 WHERE owner = $2', [wallet, from]);
      // A run left waiting goes with its character (routes/runs.ts).
      await c.query('UPDATE runs SET owner = $1 WHERE owner = $2', [wallet, from]);
      // And what the service saw of its runs, and any boxes held for it (runProgress.ts).
      await c.query('UPDATE run_progress SET owner = $1 WHERE owner = $2', [wallet, from]);
      await c.query('UPDATE held_boxes SET owner = $1 WHERE owner = $2', [wallet, from]);
      await c.query('UPDATE avatar_boxes SET owner = $1 WHERE owner = $2', [wallet, from]);
      // A guarantee the guest used this week stays used - else graduating would hand out a second.
      await c.query('UPDATE final_floor SET owner = $1 WHERE owner = $2', [wallet, from]);
      // What the database holds for each character - dyes, charges, poses, milestones (cosmetics.ts).
      for (const table of ['character_items', 'dyed_pieces', 'milestones', 'item_log'])
        await c.query(`UPDATE ${table} SET owner = $1 WHERE owner = $2`, [wallet, from]);
      // The room's own record names its owner (AccountProfile.AccountId) - it follows the move.
      await c.query(
        `UPDATE accounts SET owner = $1, save = jsonb_set(save, '{AccountId}', to_jsonb($1::text)), updated_at = now()
         WHERE owner = $2`, [wallet, from]);
      await c.query('UPDATE guests SET graduated_to = $1, graduated_at = now() WHERE id = $2', [wallet, gid]);
      return { status: 200 as const, moved: moved.rowCount ?? 0, repeat: false };
    });
    if (out.status === 404) return fail(res, 404, 'unknown guest');
    if (out.status === 409) return fail(res, 409, out.error);
    res.json({ moved: out.moved, repeat: out.repeat });
  } catch (e) {
    internal(res, e, 'saves');
  }
}
