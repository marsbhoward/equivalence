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

export function playRouter(): express.RequestHandler | null {
  const dir = path.resolve(process.env.PLAY_DIR || './play');
  if (!existsSync(path.join(dir, 'index.html'))) {
    console.log(`[play] no build at ${dir} - /play is not served`);
    return null;
  }
  console.log(`[play] serving the game from ${dir}`);
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
