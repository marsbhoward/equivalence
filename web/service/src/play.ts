import express, { type Response } from 'express';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';

/**
 * The game itself, served at `/play` - the WebGL build (Unity's output folder) from `PLAY_DIR`
 * (default ./play). Same origin as the API, so the game's saves and the wallet connector need no
 * CORS at all (index.html sets `window.EQUIVALENCE_SERVICE = ""`).
 *
 * The build is BROTLI-compressed with no decompression fallback (PlayerSettings.WebGL), so every
 * `*.br` file must go out with `Content-Encoding: br` and the content type of what is INSIDE it -
 * a browser handed `game.wasm.br` as octet-stream refuses to compile it. File names are content
 * hashes (nameFilesAsHashes), so a Build/ file never changes under its name: cached for a year.
 * index.html is not, so a new build is picked up on the next load.
 */
const TYPES: Record<string, string> = {
  '.js': 'application/javascript',
  '.wasm': 'application/wasm',
  '.data': 'application/octet-stream',
  '.json': 'application/json',
  '.symbols.json': 'application/json',
};

function typeOf(file: string): string | undefined {
  const inner = file.replace(/\.(br|gz)$/, '');
  if (inner.endsWith('.symbols.json')) return TYPES['.symbols.json'];
  return TYPES[path.extname(inner)];
}

/** Where the live build sits - `/data/play` on Railway's volume. Uploaded there by the operator
 * routes (routes/playUpload.ts), never shipped with a code deploy. */
export const playDir = () => path.resolve(process.env.PLAY_DIR || './play');

/**
 * THE PAGE'S CONTENT-SECURITY-POLICY (2026-10-29): only our own scripts run, and the page talks to
 * nothing but its own origin. A wallet session is bound to a key the page cannot export
 * (session.ts), but a script running IN the page can still use it - so the defence is that no
 * script but ours runs, and that whatever did could not send what it read anywhere.
 *
 *   script-src   'self' + the hashes of index.html's own inline scripts (computed here, per build -
 *                Unity writes the template's), 'wasm-unsafe-eval' for the game itself, and the
 *                extension schemes, so a wallet that injects its provider by script tag still can
 *   connect-src  'self' - saves, the connector, Unity's own downloads, and wallet pictures through
 *                /img (routes/img.ts), never an outside gateway
 *   media-src / img-src  blob: - Unity hands audio and downloads over as blob URLs
 *   frame-ancestors 'none' - nobody can frame the page and steer clicks into the wallet
 *
 * `CSP_MODE=report-only` sends it as Report-Only (violations still reach /csp-report) - the switch
 * for a wallet that turns out to need something, without a rebuild. `off` sends none.
 */
let cspFor: { file: string; mtime: number; header: string } | null = null;
function csp(file: string): string {
  const mtime = statSync(file).mtimeMs;
  if (cspFor?.file === file && cspFor.mtime === mtime) return cspFor.header;
  const html = readFileSync(file, 'utf8');
  const hashes = [...html.matchAll(/<script(?![^>]*\ssrc=)[^>]*>([\s\S]*?)<\/script>/gi)]
    .map(m => `'sha256-${createHash('sha256').update(m[1]).digest('base64')}'`);
  const header = [
    "default-src 'self'",
    `script-src 'self' 'wasm-unsafe-eval' ${hashes.join(' ')} chrome-extension: moz-extension: safari-web-extension:`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "media-src 'self' blob:",
    "connect-src 'self'",
    "worker-src 'self' blob:",
    "object-src 'none'",
    "base-uri 'none'",
    "form-action 'none'",
    "frame-ancestors 'none'",
    'report-uri /csp-report',
  ].join('; ');
  cspFor = { file, mtime, header };
  return header;
}

function pageHeaders(res: Response, file: string) {
  const mode = (process.env.CSP_MODE ?? 'enforce').toLowerCase();
  if (mode !== 'off') {
    res.setHeader(mode === 'report-only' ? 'Content-Security-Policy-Report-Only' : 'Content-Security-Policy', csp(file));
  }
  res.setHeader('X-Frame-Options', 'DENY');
  res.setHeader('Referrer-Policy', 'same-origin');
}

export function playRouter(): express.RequestHandler {
  const dir = playDir();
  // Served whether or not a build is there yet: an upload swaps one in under a running service.
  console.log(`[play] serving the game from ${dir}${existsSync(path.join(dir, 'index.html')) ? '' : ' (no build yet)'}`);
  return express.static(dir, {
    index: 'index.html',
    setHeaders(res: Response, file: string) {
      if (file.endsWith('.br')) res.setHeader('Content-Encoding', 'br');
      else if (file.endsWith('.gz')) res.setHeader('Content-Encoding', 'gzip');
      const type = typeOf(file);
      if (type) res.setHeader('Content-Type', type);
      res.setHeader('Cache-Control',
        file.includes(`${path.sep}Build${path.sep}`) ? 'public, max-age=31536000, immutable' : 'no-cache');
      res.setHeader('X-Content-Type-Options', 'nosniff');
      if (file.endsWith('.html')) pageHeaders(res, file);
    },
  });
}
