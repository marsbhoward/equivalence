import express, { type Response } from 'express';
import { existsSync } from 'node:fs';
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
    },
  });
}
