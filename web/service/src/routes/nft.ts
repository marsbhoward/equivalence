import type { Request, Response } from 'express';
import { createHash } from 'node:crypto';
import { existsSync } from 'node:fs';
import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { cachedAssetInfo } from '../assetCache.js';
import { dir, fetchPublic } from './img.js';

/**
 * GET /nft/:unit?v=<version> - a piece's LIVE HTML, for our own collections only (2026-10-31).
 *
 * The DONADA collection's artwork is an HTML page (CIP-25 `files`, mediaType text/html); `image` is
 * only a still of it. The game shows the still on the wall and, in the web build's close-up view
 * alone, this page in a sandboxed iframe over the canvas (Hub/NftOverlay). It never runs in the hub
 * itself - a frame on the wall is a sprite in the canvas.
 *
 * ONLY ALLOWLISTED POLICIES (`HTML_POLICIES`, comma-separated policy ids): ours, whose pages we know
 * are self-contained. Anyone else's HTML is not served at all.
 *
 * ISOLATED BY THE RESPONSE ITSELF, not by trusting the markup:
 *   - `sandbox allow-scripts` in this response's own CSP: the page runs in an OPAQUE origin even if
 *     opened directly - it cannot read the game's origin, its storage or the wallet session. The
 *     iframe carries the same sandbox (belt and braces).
 *   - `default-src 'none'` with inline script and style only, images from data:/blob: - no network
 *     at all. A DONADA piece is one file (one inline script, canvases, pointer input), so nothing
 *     it needs is refused.
 *   - framed only by our own pages (`frame-ancestors 'self'`).
 *
 * Fetched once through an IPFS gateway that serves HTML (Pinata's public one refuses it), capped at
 * 2 MB and ten seconds by the same pinned, private-address-refusing fetch the image proxy uses, and
 * cached on disk under the source's hash - a new source is a new URL.
 */

const MAX_BYTES = 2 * 1024 * 1024;
const FAIL_MS = 10 * 60_000;
const UNIT = /^[0-9a-f]{56,120}$/;
// Pinata's public gateway answers HTML with a refusal page; Blockfrost's serves it.
const GATEWAY = process.env.IPFS_HTML_GATEWAY || 'https://ipfs.blockfrost.dev/ipfs/';

const CSP = [
  'sandbox allow-scripts',
  "default-src 'none'",
  "script-src 'unsafe-inline'",
  "style-src 'unsafe-inline'",
  'img-src data: blob:',
  'font-src data:',
  'media-src data: blob:',
  "base-uri 'none'",
  "form-action 'none'",
  "frame-ancestors 'self'",
].join('; ');

/** The policies whose HTML is served, read per call so a changed variable needs only a restart. */
export function htmlPolicies(): Set<string> {
  return new Set((process.env.HTML_POLICIES ?? '').split(',').map(p => p.trim().toLowerCase()).filter(Boolean));
}

const join = (v: unknown) => (Array.isArray(v) ? v.join('') : typeof v === 'string' ? v : '').trim();

/** The HTML file's source in a token's CIP-25 metadata, or ''. */
function htmlSource(m: Record<string, unknown>): string {
  const files = Array.isArray(m.files) ? m.files as Record<string, unknown>[] : [];
  const f = files.find(x => x && join(x.mediaType).toLowerCase() === 'text/html');
  return f ? join(f.src) : '';
}

const version = (source: string) => createHash('sha256').update(source).digest('hex').slice(0, 12);

/** The game's URL for a piece's live HTML, or '' - only for an allowlisted policy with an HTML file. */
export function htmlPath(unit: string, policy: string, metadata: Record<string, unknown>): string {
  if (!htmlPolicies().has(policy.toLowerCase())) return '';
  const source = htmlSource(metadata);
  return source && where(source) ? `/nft/${unit}?v=${version(source)}` : '';
}

/** IPFS (through the HTML gateway) or an inline data: page. Nothing else. */
function where(source: string): { url: URL } | { data: Buffer } | null {
  const data = /^data:text\/html(;[a-z0-9=-]+)*?(;base64)?,(.*)$/is.exec(source);
  if (data) {
    try {
      return { data: data[2] ? Buffer.from(data[3], 'base64') : Buffer.from(decodeURIComponent(data[3]), 'utf8') };
    } catch {
      return null;
    }
  }
  const cid = /^ipfs:\/\/(?:ipfs\/)?([A-Za-z0-9]+(?:\/[^?#]*)?)$/i.exec(source)?.[1]
    ?? (/^(Qm[1-9A-HJ-NP-Za-km-z]{44}|b[a-z2-7]{50,})$/.test(source) ? source : undefined);
  try {
    return cid ? { url: new URL(GATEWAY + cid) } : null;
  } catch {
    return null;
  }
}

const inFlight = new Map<string, Promise<Buffer>>();
const failed = new Map<string, number>();

async function page(unit: string, source: string): Promise<Buffer> {
  const file = path.join(dir(), `${unit}.${version(source)}.html`);
  if (existsSync(file)) return readFile(file);
  const until = failed.get(file);
  if (until && until > Date.now()) throw new Error('recently failed');

  let p = inFlight.get(file);
  if (!p) {
    p = (async () => {
      const at = where(source);
      if (!at) throw new Error('not an HTML source this service fetches');
      const body = 'data' in at ? at.data : await fetchPublic(at.url, 'text/html', MAX_BYTES);
      if (body.length > MAX_BYTES) throw new Error('page too large');
      if (!/<(html|head|body|script|canvas|div|svg|title|style)\b/i.test(body.subarray(0, 4096).toString('utf8')))
        throw new Error('not an HTML page');
      await mkdir(dir(), { recursive: true });
      const tmp = `${file}.${process.pid}.tmp`;
      await writeFile(tmp, body);
      await rename(tmp, file);
      return body;
    })();
    inFlight.set(file, p);
    p.catch(() => failed.set(file, Date.now() + FAIL_MS)).finally(() => inFlight.delete(file));
  }
  return p;
}

export async function getNftPage(req: Request, res: Response): Promise<void> {
  const unit = String(req.params.unit ?? '').toLowerCase();
  if (!UNIT.test(unit)) {
    res.status(400).json({ error: 'not an asset unit' });
    return;
  }
  if (!htmlPolicies().has(unit.slice(0, 56))) {
    res.status(404).json({ error: 'not a collection shown live' });
    return;
  }
  try {
    const info = await cachedAssetInfo(unit);
    const metadata = (info?.onchain_metadata ?? info?.metadata ?? {}) as Record<string, unknown>;
    const source = htmlSource(metadata);
    if (!source) {
      res.status(404).json({ error: 'this token has no page' });
      return;
    }
    const body = await page(unit, source);
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    res.setHeader('Content-Security-Policy', CSP);
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Referrer-Policy', 'no-referrer');
    res.setHeader('Cache-Control', req.query.v === version(source)
      ? 'public, max-age=31536000, immutable' : 'public, max-age=3600');
    res.send(body);
  } catch (e) {
    console.warn(`[nft] ${unit}: ${(e as Error).message}`);
    res.status(502).json({ error: 'page_unavailable' });
  }
}

/**
 * The CARD a piece's page draws, as numbers the game can draw natively - the spinning card on the
 * hub's wall (Hub/NftCard). Read from the page's own style line (the generator writes each piece's
 * orb and glass colours there as custom properties) and its labels. Effects are NOT here: they are
 * the close-up's, through the live page.
 */
export type Orbit = { Tilt: number; Roll: number; Phase: number };

export type Card = {
  Glass: string;            // #rrggbb
  GlassAlpha: number;
  Orbs: { Core: string; Color: string; Bloom: string; Trail: string }[];
  FrontOrbit: Orbit;            // each face has its own orbit (the template's .orbit.front / .orbit.back)
  BackOrbit: Orbit;
  Front: string[];          // the front label's lines: collection, policy, number
  Back: string[];           // the back's: name, the meanings
};

const cards = new Map<string, Card | null>();

export async function cardOf(unit: string, metadata: Record<string, unknown>): Promise<Card | null> {
  const source = htmlSource(metadata);
  if (!source || !htmlPolicies().has(unit.slice(0, 56))) return null;
  const key = `${unit}.${version(source)}`;
  if (cards.has(key)) return cards.get(key)!;
  const html = (await page(unit, source)).toString('utf8');
  const card = parseCard(html);
  cards.set(key, card);
  if (cards.size > 2000) cards.delete(cards.keys().next().value!);
  return card;
}

export function parseCard(html: string): Card | null {
  const vars = new Map<string, string>();
  for (const block of html.matchAll(/:root\s*\{([^}]*)\}/g))
    for (const decl of block[1].split(';')) {
      const m = /^\s*(--[a-z0-9-]+)\s*:\s*(.+?)\s*$/i.exec(decl);
      if (m) vars.set(m[1].toLowerCase(), m[2]);
    }
  if (!vars.size) return null;

  const orbs: Card['Orbs'] = [];
  for (const p of ['a', 'b', 'c', 'd']) {
    const color = hex(vars.get(`--orb-${p}-color`));
    if (!color) continue;
    orbs.push({
      Core: hex(vars.get(`--orb-${p}-core`)) || color,
      Color: color,
      Bloom: hex(vars.get(`--orb-${p}-bloom`)) || color,
      Trail: hex(vars.get(`--orb-${p}-trail`)) || color,
    });
  }
  const glass = rgba(vars.get('--glass')) ?? { hex: '#000000', a: 0.8 };
  return {
    Glass: glass.hex,
    GlassAlpha: glass.a,
    Orbs: orbs,
    FrontOrbit: orbitOf(html, 'front', { Tilt: 44, Roll: -14, Phase: 0 }),
    BackOrbit: orbitOf(html, 'back', { Tilt: 38, Roll: 21, Phase: 63 }),
    Front: labelOf(html, 'front'),
    Back: labelOf(html, 'back'),
  };
}

/** A face's orbit from the template's `.orbit.<side> { --orbit-tilt: ..; }` rule, else its defaults. */
function orbitOf(html: string, side: 'front' | 'back', dflt: Orbit): Orbit {
  const rule = new RegExp(`\\.orbit\\.${side}\\s*\\{([^}]*)\\}`, 'i').exec(html)?.[1] ?? '';
  const num = (name: string, d: number) => {
    const v = parseFloat(new RegExp(`--orbit-${name}\\s*:\\s*(-?[\\d.]+)`, 'i').exec(rule)?.[1] ?? '');
    return Number.isFinite(v) ? v : d;
  };
  return { Tilt: num('tilt', dflt.Tilt), Roll: num('roll', dflt.Roll), Phase: num('phase', dflt.Phase) };
}

/** The text of a face's label, one line per innermost span. The "+" between the meanings is its own
 *  span (`plus`), so it is folded into its line first ("Harmony + Compassion" stays one line). */
function labelOf(html: string, side: 'front' | 'back'): string[] {
  const face = new RegExp(`<div class="face ${side}">[\\s\\S]*?<div class="label">([\\s\\S]*?)</div>`, 'i').exec(html);
  if (!face) return [];
  const body = face[1].replace(/<!--[\s\S]*?-->/g, '').replace(/<span class="plus">([^<]*)<\/span>/gi, '$1');
  const lines = [...body.matchAll(/<span[^>]*>([^<]*)<\/span>/gi)].map(m => m[1].replace(/\s+/g, ' ').trim());
  return lines.filter(Boolean).slice(0, 6);
}

function hex(v?: string): string {
  const m = /^#([0-9a-f]{6}|[0-9a-f]{3})$/i.exec((v ?? '').trim());
  if (!m) return '';
  const h = m[1].length === 3 ? m[1].split('').map(c => c + c).join('') : m[1];
  return '#' + h.toLowerCase();
}

function rgba(v?: string): { hex: string; a: number } | null {
  const h = hex(v);
  if (h) return { hex: h, a: 1 };
  const m = /^rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+)\s*)?\)$/i.exec((v ?? '').trim());
  if (!m) return null;
  const c = (n: string) => Math.max(0, Math.min(255, Number(n))).toString(16).padStart(2, '0');
  return { hex: `#${c(m[1])}${c(m[2])}${c(m[3])}`, a: m[4] === undefined ? 1 : Math.max(0, Math.min(1, Number(m[4]))) };
}
