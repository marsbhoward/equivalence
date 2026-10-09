import type { Request, Response } from 'express';
import { ART_KEY, ArtEntry, forget, lookup, record } from '../artIndex.js';
import { addAndPin, isPinned } from '../ipfs.js';

/**
 * The art half of a mint. A mint names its ART KEY (see artIndex.ts); a key already pinned is
 * used as it is, and a miss answers 409 `art_needed` - the caller (Unity, the only thing that can
 * draw the pixel art) renders the two PNGs, posts them here, and retries the mint. So the common
 * case sends no image at all, and an image is uploaded once per picture, not once per piece.
 */

/** The NFT image spec (CLAUDE.md, "NFT item images"): menu art x10 on a square 3200 canvas. */
const CANVAS = 3200;
/** Menu art past 320 texels no longer fits that canvas at x10. */
const NATIVE_MAX = 320;

const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

function pngSize(bytes: Buffer): { width: number; height: number } | null {
  if (bytes.length < 24 || !bytes.subarray(0, 8).equals(PNG_SIGNATURE)) return null;
  if (bytes.toString('ascii', 12, 16) !== 'IHDR') return null;
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
}

/**
 * The pinned art for a key, or undefined on a miss. A key whose pin Blockfrost no longer holds is
 * dropped from the index and reported as a miss, so it is re-pinned rather than minted pointing
 * at bytes nobody is keeping.
 */
export async function resolveArt(artKey: string): Promise<ArtEntry | undefined> {
  const entry = await lookup(artKey);
  if (!entry) return undefined;
  const [image, native] = await Promise.all([isPinned(entry.image.cid), isPinned(entry.native.cid)]);
  if (image && native) return entry;
  await forget(artKey);
  return undefined;
}

/** One upload per key at a time: two misses racing for one picture share the first's pin. */
const inFlight = new Map<string, Promise<ArtEntry>>();

async function pinArt(artKey: string, image: Buffer, native: Buffer): Promise<ArtEntry> {
  const imageSize = pngSize(image)!;
  const nativeSize = pngSize(native)!;
  const safe = artKey.replace(/[^a-z0-9_.-]/gi, '_');
  const [imageCid, nativeCid] = await Promise.all([
    addAndPin(image, `${safe}.png`, 'image/png'),
    addAndPin(native, `${safe}.1x.png`, 'image/png'),
  ]);
  const entry: ArtEntry = {
    image: { cid: imageCid, bytes: image.length, ...imageSize },
    native: { cid: nativeCid, bytes: native.length, ...nativeSize },
    pinnedAt: new Date().toISOString(),
  };
  await record(artKey, entry);
  return entry;
}

/**
 * POST /write/art  { artKey, image: base64 PNG 3200x3200, native: base64 PNG <= 320 a side }
 * -> { artKey, entry, reused }
 *
 * Idempotent: a key that is already pinned answers with what it has and uploads nothing, so a
 * caller that lost the first response can simply post again.
 */
export async function postArt(req: Request, res: Response): Promise<void> {
  const { artKey, image, native } = (req.body ?? {}) as Record<string, unknown>;
  if (typeof artKey !== 'string' || !ART_KEY.test(artKey)) {
    res.status(400).json({ error: 'artKey must be a design id, optionally @ a dye tag' });
    return;
  }
  if (typeof image !== 'string' || typeof native !== 'string') {
    res.status(400).json({ error: 'image and native are required, as base64 PNG' });
    return;
  }

  const imageBytes = Buffer.from(image, 'base64');
  const nativeBytes = Buffer.from(native, 'base64');
  const imageSize = pngSize(imageBytes);
  const nativeSize = pngSize(nativeBytes);
  if (!imageSize || imageSize.width !== CANVAS || imageSize.height !== CANVAS) {
    res.status(400).json({ error: `image must be a ${CANVAS}x${CANVAS} PNG`, got: imageSize });
    return;
  }
  if (!nativeSize || nativeSize.width > NATIVE_MAX || nativeSize.height > NATIVE_MAX) {
    res.status(400).json({ error: `native must be a PNG at most ${NATIVE_MAX} a side`, got: nativeSize });
    return;
  }

  try {
    const existing = await resolveArt(artKey);
    if (existing) {
      res.json({ artKey, entry: existing, reused: true });
      return;
    }
    let pending = inFlight.get(artKey);
    if (!pending) {
      pending = pinArt(artKey, imageBytes, nativeBytes).finally(() => inFlight.delete(artKey));
      inFlight.set(artKey, pending);
    }
    res.json({ artKey, entry: await pending, reused: false });
  } catch (err) {
    res.status(502).json({ error: err instanceof Error ? err.message : String(err) });
  }
}

/** GET /art/:artKey - whether a picture is pinned yet. Read-only, so ungated. */
export async function getArt(req: Request, res: Response): Promise<void> {
  const artKey = req.params.artKey;
  if (!ART_KEY.test(artKey)) {
    res.status(400).json({ error: 'not an art key' });
    return;
  }
  try {
    const entry = await resolveArt(artKey);
    if (entry) res.json({ artKey, entry });
    else res.status(404).json({ artKey, error: 'not pinned' });
  } catch (err) {
    res.status(502).json({ error: err instanceof Error ? err.message : String(err) });
  }
}
