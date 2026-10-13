import type { Request, Response } from 'express';
import { existsSync } from 'node:fs';
import { mkdir, rename, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { playDir } from '../play.js';

/**
 * Putting a WebGL build where /play serves it - OPERATOR only (`/op`, WRITE_TOKEN), so builds
 * never ride in git or in a code deploy. scripts/upload-play.mjs drives it:
 *
 *   PUT  /op/play/:build/file?path=Build/x.wasm.br   raw bytes -> staged under <PLAY_DIR>.staging/<build>
 *   POST /op/play/:build/activate                    the staged build becomes PLAY_DIR; the one it
 *                                                    replaces is kept as <PLAY_DIR>.previous
 *   POST /op/play/rollback                           the previous build back
 *
 * The swap is two renames on one volume, so players never load half a build: they get the old one
 * or the new one. File names inside Build/ are content hashes, so an old page still loading finds
 * its own files only until the swap - a reload picks up the new index.html.
 */
const BUILD_ID = /^[A-Za-z0-9_.-]{1,64}$/;
const FILE_PATH = /^[A-Za-z0-9_./-]{1,200}$/;

const stagingFor = (build: string) => path.join(`${playDir()}.staging`, build);

export async function putPlayFile(req: Request, res: Response): Promise<void> {
  const build = String(req.params.build ?? '');
  const rel = String(req.query.path ?? '');
  if (!BUILD_ID.test(build)) return void res.status(400).json({ error: 'bad build id' });
  if (!FILE_PATH.test(rel) || rel.split('/').some(p => p === '..' || p === '' || p === '.'))
    return void res.status(400).json({ error: 'bad path' });
  if (!Buffer.isBuffer(req.body)) return void res.status(400).json({ error: 'send the file as application/octet-stream' });

  const root = stagingFor(build);
  const file = path.join(root, rel);
  if (!file.startsWith(root + path.sep)) return void res.status(400).json({ error: 'bad path' });
  try {
    await mkdir(path.dirname(file), { recursive: true });
    await writeFile(file, req.body);
    res.json({ ok: true, path: rel, bytes: req.body.length });
  } catch (e) {
    res.status(500).json({ error: (e as Error).message });
  }
}

export async function activatePlay(req: Request, res: Response): Promise<void> {
  const build = String(req.params.build ?? '');
  if (!BUILD_ID.test(build)) return void res.status(400).json({ error: 'bad build id' });
  const staged = stagingFor(build);
  if (!existsSync(path.join(staged, 'index.html')))
    return void res.status(409).json({ error: 'the staged build has no index.html' });

  const live = playDir();
  const previous = `${live}.previous`;
  try {
    await rm(previous, { recursive: true, force: true });
    if (existsSync(live)) await rename(live, previous);
    await rename(staged, live);
    await rm(`${live}.staging`, { recursive: true, force: true });   // abandoned uploads go too
    console.log(`[play] build ${build} is live`);
    res.json({ ok: true, live: build });
  } catch (e) {
    res.status(500).json({ error: (e as Error).message });
  }
}

export async function rollbackPlay(_req: Request, res: Response): Promise<void> {
  const live = playDir();
  const previous = `${live}.previous`;
  if (!existsSync(previous)) return void res.status(409).json({ error: 'no previous build kept' });
  try {
    const swap = `${live}.swap`;
    await rm(swap, { recursive: true, force: true });
    if (existsSync(live)) await rename(live, swap);
    await rename(previous, live);
    if (existsSync(swap)) await rename(swap, previous);
    res.json({ ok: true });
  } catch (e) {
    res.status(500).json({ error: (e as Error).message });
  }
}
