import { assetInfo } from './blockfrost.js';
import { hasDatabase, q } from './db.js';

/**
 * Blockfrost's `/assets/<unit>`, cached in Postgres.
 *
 * Every call the service makes to Blockfrost comes from ONE IP, and Blockfrost's rate limit is per
 * IP (10 a second, bursts of 500) - so uncached, one wallet's inventory refresh (a call per token
 * held, up to 60) spent the whole service's budget for six seconds. A token's metadata rarely
 * changes, so a hit costs nothing and a refresh drops to the one call that lists what is held.
 *
 * Kept for `ASSET_CACHE_HOURS` (6). Metadata the SERVICE changes - a dye rewriting a design's
 * reference datum - must call `forgetAsset` for the user token, or the old picture shows until the
 * entry ages out. Without DATABASE_URL it passes straight through.
 */
const MAX_AGE_HOURS = Number(process.env.ASSET_CACHE_HOURS ?? 6);

type Info = Awaited<ReturnType<typeof assetInfo>>;

export async function cachedAssetInfo(unit: string): Promise<Info> {
  if (!hasDatabase()) return assetInfo(unit);
  const hit = await q<{ info: Info }>(
    `SELECT info FROM asset_cache WHERE unit = $1 AND fetched_at > now() - make_interval(hours => $2)`,
    [unit, MAX_AGE_HOURS]).catch(() => []);
  if (hit.length) return hit[0].info;

  const info = await assetInfo(unit);
  if (info)
    await q(
      `INSERT INTO asset_cache (unit, info) VALUES ($1, $2)
       ON CONFLICT (unit) DO UPDATE SET info = $2, fetched_at = now()`, [unit, info]).catch(() => {});
  return info;
}

/** Drops a unit's cached metadata - call it after the service rewrites that token's datum. */
export async function forgetAsset(unit: string): Promise<void> {
  if (hasDatabase()) await q('DELETE FROM asset_cache WHERE unit = $1', [unit]).catch(() => {});
}
