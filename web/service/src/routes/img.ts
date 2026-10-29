import type { Request, Response } from 'express';
import { createHash } from 'node:crypto';
import { existsSync } from 'node:fs';
import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
import { lookup } from 'node:dns/promises';
import { isIP } from 'node:net';
import https from 'node:https';
import path from 'node:path';
import sharp from 'sharp';
import { cachedAssetInfo } from '../assetCache.js';

/**
 * GET /img/:unit?v=<version> - a wallet token's picture, fetched ONCE by the service, shrunk and
 * cached, so the game never loads art from a host it does not know (2026-10-29).
 *
 * WHY THROUGH HERE. The page's Content-Security-Policy (play.ts) lets it talk to its own origin
 * and nothing else; a picture from an arbitrary gateway would need `connect-src *`, which is also
 * a door for anything injected into the page to send what it reads somewhere. And it is faster:
 * an NFT original can be several MB at 4K and the game only ever keeps 512 px of it
 * (WalletShowcaseSource.MaxEdge), so the shrink happens once here instead of on every download -
 * and once for every player, instead of every player hitting a public IPFS gateway that rate-limits.
 *
 * THIS FETCHES URLS NAMED IN SOMEONE ELSE'S METADATA, which is the classic server-side request
 * forgery shape - anyone can mint a token whose image is `http://169.254.169.254/...`. So: only
 * https, IPFS (through our own gateway), Arweave and inline `data:` images; every host resolved
 * and refused if any address is private, the connection PINNED to the address that was checked (no
 * re-resolution between check and connect), no redirects, an 8 MB cap, ten seconds, and the
 * result must decode as an image.
 *
 * The version is a hash of the image source, so a dye that rewrites a design's picture is a new
 * URL - cached for a year under each version, and never stale.
 */

const MAX_EDGE = 512;
const MAX_BYTES = 8 * 1024 * 1024;
const TIMEOUT_MS = 10_000;
/** A source that failed is left alone this long, so one bad token cannot be retried every repaint. */
const FAIL_MS = 10 * 60_000;
const IPFS_GATEWAY = process.env.IPFS_GATEWAY || 'https://gateway.pinata.cloud/ipfs/';
const UNIT = /^[0-9a-f]{56,120}$/;

const dir = () => path.resolve(process.env.IMG_DIR
  || (process.env.RAILWAY_VOLUME_MOUNT_PATH ? path.join(process.env.RAILWAY_VOLUME_MOUNT_PATH, 'img') : './img-cache'));

/** The raw image field of a token's metadata - CIP-25 allows a split string. */
function imageField(m: Record<string, unknown>): string {
  const raw = Array.isArray(m.image) ? m.image.join('') : m.image;
  return typeof raw === 'string' ? raw.trim() : '';
}

/** The version a picture URL carries - changes whenever the token's image source does. */
export const imageVersion = (source: string) => createHash('sha256').update(source).digest('hex').slice(0, 12);

/** The game's URL for a token's picture, or '' when it has none. Relative: same origin as the page. */
export function imagePath(unit: string, metadata: Record<string, unknown>): string {
  const source = imageField(metadata);
  return source ? `/img/${unit}?v=${imageVersion(source)}` : '';
}

/** Where an image source can actually be fetched from, or null when it is not a kind we fetch. */
function resolve(source: string): { url: URL } | { data: Buffer } | null {
  if (source.startsWith('data:')) {
    const m = /^data:image\/[a-z0-9.+-]+(;[a-z0-9=-]+)*;base64,(.*)$/is.exec(source);
    return m ? { data: Buffer.from(m[2], 'base64') } : null;
  }
  const cid = /^ipfs:\/\/(?:ipfs\/)?(.+)$/i.exec(source)?.[1]
    ?? (/^(Qm[1-9A-HJ-NP-Za-km-z]{44}|b[a-z2-7]{50,})$/.test(source) ? source : undefined)
    // Any gateway's /ipfs/<cid> is the same bytes - fetched from ours.
    ?? /^https?:\/\/[^/]+\/ipfs\/(.+)$/i.exec(source)?.[1];
  try {
    if (cid) return { url: new URL(IPFS_GATEWAY + cid) };
    const ar = /^ar:\/\/(.+)$/i.exec(source)?.[1];
    if (ar) return { url: new URL(`https://arweave.net/${ar}`) };
    const url = new URL(source);
    return url.protocol === 'https:' ? { url } : null;
  } catch {
    return null;
  }
}

/** Loopback, private, link-local, carrier NAT, multicast, reserved - anything not the public internet. */
function isPrivate(ip: string): boolean {
  if (isIP(ip) === 4) {
    const [a, b] = ip.split('.').map(Number);
    return a === 0 || a === 10 || a === 127 || (a === 100 && b >= 64 && b < 128) || (a === 169 && b === 254)
      || (a === 172 && b >= 16 && b < 32) || (a === 192 && b === 168) || (a === 192 && b === 0)
      || (a === 198 && (b === 18 || b === 19)) || a >= 224;
  }
  const v6 = ip.toLowerCase();
  if (v6.startsWith('::ffff:')) return isPrivate(v6.slice(7));
  return v6 === '::' || v6 === '::1' || v6.startsWith('fc') || v6.startsWith('fd') || v6.startsWith('fe8')
    || v6.startsWith('fe9') || v6.startsWith('fea') || v6.startsWith('feb') || v6.startsWith('ff');
}

/** GET over https, pinned to a public address, capped in size and time, no redirects. */
async function fetchPublic(url: URL): Promise<Buffer> {
  const host = url.hostname.replace(/^\[|\]$/g, '');
  const addrs = isIP(host) ? [{ address: host, family: isIP(host) }] : await lookup(host, { all: true });
  if (!addrs.length || addrs.some(a => isPrivate(a.address))) throw new Error(`refused host ${host}`);
  const pinned = addrs[0];

  return new Promise((resolveBody, reject) => {
    const req = https.get(url, {
      timeout: TIMEOUT_MS,
      headers: { accept: 'image/*', 'user-agent': 'equivalence-service' },
      // The address that was checked is the one connected to - a second lookup could answer differently.
      lookup: (_h, opts, cb) => (opts as { all?: boolean }).all
        ? (cb as unknown as (e: null, a: { address: string; family: number }[]) => void)(null, [pinned])
        : cb(null, pinned.address, pinned.family),
    }, res => {
      if (res.statusCode !== 200) {
        res.resume();
        reject(new Error(`${host} answered ${res.statusCode}`));
        return;
      }
      const chunks: Buffer[] = [];
      let size = 0;
      res.on('data', (c: Buffer) => {
        size += c.length;
        if (size > MAX_BYTES) { req.destroy(new Error('image too large')); return; }
        chunks.push(c);
      });
      res.on('end', () => resolveBody(Buffer.concat(chunks)));
      res.on('error', reject);
    });
    req.on('timeout', () => req.destroy(new Error('image fetch timed out')));
    req.on('error', reject);
  });
}

/** PNG keeps transparency; anything opaque goes out as JPEG, a fraction of the size for a painting
 *  or a photo. Both are what Unity's texture loader reads. Told apart by their first bytes. */
const typeOf = (b: Buffer) => (b[0] === 0x89 && b[1] === 0x50 ? 'image/png' : 'image/jpeg');

/** The shrunk picture for a source - from the cache, or fetched, shrunk and cached now. */
const inFlight = new Map<string, Promise<Buffer>>();
const failed = new Map<string, number>();

async function picture(unit: string, source: string): Promise<Buffer> {
  const file = path.join(dir(), `${unit}.${imageVersion(source)}.img`);
  if (existsSync(file)) return readFile(file);
  const until = failed.get(file);
  if (until && until > Date.now()) throw new Error('recently failed');

  let p = inFlight.get(file);
  if (!p) {
    p = (async () => {
      const where = resolve(source);
      if (!where) throw new Error('not an image source this service fetches');
      const raw = 'data' in where ? where.data : await fetchPublic(where.url);
      if (raw.length > MAX_BYTES) throw new Error('image too large');
      const img = sharp(raw, { limitInputPixels: 64_000_000, animated: false })
        .resize(MAX_EDGE, MAX_EDGE, { fit: 'inside', withoutEnlargement: true });
      const out = (await img.metadata()).hasAlpha
        ? await img.png({ compressionLevel: 9, palette: true, quality: 90 }).toBuffer()
        : await img.flatten().jpeg({ quality: 86, mozjpeg: true }).toBuffer();
      await mkdir(dir(), { recursive: true });
      const tmp = `${file}.${process.pid}.tmp`;
      await writeFile(tmp, out);
      await rename(tmp, file);
      return out;
    })();
    inFlight.set(file, p);
    p.catch(() => failed.set(file, Date.now() + FAIL_MS)).finally(() => inFlight.delete(file));
  }
  return p;
}

export async function getImage(req: Request, res: Response): Promise<void> {
  const unit = String(req.params.unit ?? '').toLowerCase();
  if (!UNIT.test(unit)) {
    res.status(400).json({ error: 'not an asset unit' });
    return;
  }
  try {
    const info = await cachedAssetInfo(unit);
    const metadata = ((info?.onchain_metadata ?? info?.metadata ?? {}) as Record<string, unknown>);
    const source = imageField(metadata);
    if (!source) {
      res.status(404).json({ error: 'this token has no picture' });
      return;
    }
    const pic = await picture(unit, source);
    res.setHeader('Content-Type', typeOf(pic));
    // Versioned URL (the source's hash): a new picture is a new URL, so this one never changes.
    res.setHeader('Cache-Control', req.query.v === imageVersion(source)
      ? 'public, max-age=31536000, immutable' : 'public, max-age=3600');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.send(pic);
  } catch (e) {
    console.warn(`[img] ${unit}: ${(e as Error).message}`);
    res.status(502).json({ error: 'picture_unavailable' });
  }
}
