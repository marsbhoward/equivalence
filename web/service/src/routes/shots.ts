import type { Request, Response } from 'express';
import { internal } from '../errors.js';
import { q, tx } from '../db.js';
import { BOOTH_SHOTS_KEPT, BOOTH_SHOT_MAX_BYTES } from '../cosmetics.js';

/**
 * BOOTH SHOTS - pictures a character took in the portrait booth, kept to hang in the hub's frames
 * (the user's calls, 2026-11-13: the moment as taken, kept with the character, twelve each). The
 * owner comes from the identity like every save; a guest's move to a wallet carries them (graduate).
 *
 *   GET    /saves/shots                          -> { items: [{ profileId, shotId, name, png, takenAt }] }
 *   PUT    /saves/shots/:profileId/:shotId       { png, replaces? } -> { ok }   409 'full' at the cap;
 *                                                 REPLACES names the shot it takes the place of
 *
 * PNG is base64. Only a real PNG of at most BOOTH_SHOT_MAX_BYTES is kept - the game sends the picture
 * at the art's own pixel size, a few KB.
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;
const SHOT = /^[a-z0-9]{8,40}$/;
const PNG = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

export async function listShots(_req: Request, res: Response): Promise<void> {
  try {
    const rows = await q<{ profile_id: string; shot_id: string; png: Buffer; taken_at: Date; name: string | null }>(
      `SELECT s.profile_id, s.shot_id, s.png, s.taken_at, c.save->>'Name' AS name
         FROM booth_shots s
         LEFT JOIN characters c ON c.owner = s.owner AND c.profile_id = s.profile_id
        WHERE s.owner = $1
        ORDER BY s.profile_id, s.taken_at`, [res.locals.owner]);
    res.json({
      items: rows.map(r => ({
        profileId: r.profile_id, shotId: r.shot_id, name: r.name ?? '',
        png: r.png.toString('base64'), takenAt: r.taken_at.toISOString(),
      })),
    });
  } catch (e) {
    internal(res, e, 'shots');
  }
}

export async function putShot(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.params.profileId ?? '');
  const shotId = String(req.params.shotId ?? '');
  const replaces = req.body?.replaces ? String(req.body.replaces) : null;
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  if (!SHOT.test(shotId) || (replaces && !SHOT.test(replaces))) return fail(res, 400, 'bad shot id');
  const raw = typeof req.body?.png === 'string' ? req.body.png : '';
  const png = Buffer.from(raw, 'base64');
  if (png.length < PNG.length || !png.subarray(0, PNG.length).equals(PNG)) return fail(res, 400, 'png must be a PNG');
  if (png.length > BOOTH_SHOT_MAX_BYTES) return fail(res, 413, 'too_big', { max: BOOTH_SHOT_MAX_BYTES });

  try {
    const out = await tx(async c => {
      const who = await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2', [owner, profileId]);
      if (!who.rowCount) return 404 as const;
      // Every shot of this character, locked: two taps at once cannot both take the last place.
      await c.query('SELECT 1 FROM booth_shots WHERE owner = $1 AND profile_id = $2 FOR UPDATE', [owner, profileId]);
      if (replaces)
        await c.query('DELETE FROM booth_shots WHERE owner = $1 AND profile_id = $2 AND shot_id = $3',
          [owner, profileId, replaces]);
      const n = await c.query<{ n: string }>(
        'SELECT count(*) AS n FROM booth_shots WHERE owner = $1 AND profile_id = $2 AND shot_id <> $3',
        [owner, profileId, shotId]);
      if (Number(n.rows[0].n) >= BOOTH_SHOTS_KEPT) return 409 as const;
      await c.query(
        `INSERT INTO booth_shots (owner, profile_id, shot_id, png) VALUES ($1, $2, $3, $4)
         ON CONFLICT (owner, profile_id, shot_id) DO UPDATE SET png = $4, taken_at = now()`,
        [owner, profileId, shotId, png]);
      return 200 as const;
    });
    if (out === 404) return fail(res, 404, 'no such character');
    if (out === 409) return fail(res, 409, 'full', { max: BOOTH_SHOTS_KEPT });
    res.json({ ok: true });
  } catch (e) {
    internal(res, e, 'shots');
  }
}
