import { primaryFormat } from './runFormat.js';
import type { NextFunction, Request, Response } from 'express';
import { internal } from './errors.js';
import { hasDatabase, q } from './db.js';

/**
 * PAUSE SWITCHES (2026-10-29) - one feature off while the rest of the game plays on, instead of
 * taking the whole server down when something goes wrong. Kept in Postgres, so flipping one is an
 * operator call, not a redeploy:
 *
 *   POST /op/switches { name, paused, message }     (WRITE_TOKEN - scripts/switch.mjs)
 *   GET  /status  -> { paused: { <name>: <message> }, minClient, format }   public; the game shows them
 *
 *   runs     no NEW run starts (the run-start save, /write/begin-run). Runs already going carry on.
 *   minting  nothing goes to the chain: opening boxes, dyes in and out, box tokens into charges, the
 *            portrait. EXTRACTION is not refused - the run is judged and its boxes recorded as
 *            DEFERRED (routes/player.ts), and minted when minting resumes. Nothing a player earned
 *            is lost to a pause.
 *   saves    read-only: no save writes at all. The emergency one - writes made meanwhile are lost.
 *
 * A paused route answers 503 `{ code: 'paused', feature, message }`, which the game does NOT retry.
 */
export const SWITCHES = ['runs', 'minting', 'saves'] as const;
export type SwitchName = typeof SWITCHES[number];

/** The oldest game build the service still talks to (X-Client-Build). Bumped with a breaking
 *  change; a game older than this is told to reload (clientBuild below). */
export const MIN_CLIENT_BUILD = 1;

const CACHE_MS = 10_000;
let cache: { at: number; paused: Map<SwitchName, string> } | null = null;
/** Without a database (local dev) the switches live here. */
const local = new Map<SwitchName, string>();

export async function pausedSwitches(): Promise<Map<SwitchName, string>> {
  if (!hasDatabase()) return local;
  if (cache && Date.now() - cache.at < CACHE_MS) return cache.paused;
  const rows = await q<{ name: SwitchName; message: string }>('SELECT name, message FROM switches WHERE paused');
  cache = { at: Date.now(), paused: new Map(rows.map(r => [r.name, r.message])) };
  return cache.paused;
}

export async function isPaused(name: SwitchName): Promise<boolean> {
  return (await pausedSwitches()).has(name);
}

const DEFAULT_MESSAGE: Record<SwitchName, string> = {
  runs: 'New runs are paused for a moment. Runs already going carry on.',
  minting: 'Minting is paused for a moment. Anything a run earns is kept and minted when it resumes.',
  saves: 'Saving is paused for a moment.',
};

/** The 503 a paused feature answers with. */
export async function refusePaused(res: Response, name: SwitchName): Promise<void> {
  const message = (await pausedSwitches()).get(name) || DEFAULT_MESSAGE[name];
  res.status(503).json({ error: message, code: 'paused', feature: name, message });
}

/** A route that only runs while `name` is not paused. */
export function requireOpen(name: SwitchName, when: (req: Request) => boolean = () => true) {
  return async (req: Request, res: Response, next: NextFunction) => {
    try {
      if (when(req) && await isPaused(name)) return void await refusePaused(res, name);
    } catch (e) {
      return void internal(res, e, 'switches', 503, 'unavailable');
    }
    next();
  };
}

/** Called when a switch changes - the deferred extraction mints when `minting` reopens. */
let onReopen: ((name: SwitchName) => void) | null = null;
export const whenReopened = (f: (name: SwitchName) => void) => { onReopen = f; };

/** POST /op/switches - operator only (WRITE_TOKEN, mounted under /op). */
export async function setSwitch(req: Request, res: Response): Promise<void> {
  const { name, paused, message } = (req.body ?? {}) as { name?: string; paused?: boolean; message?: string };
  if (!SWITCHES.includes(name as SwitchName) || typeof paused !== 'boolean') {
    res.status(400).json({ error: `name must be one of ${SWITCHES.join(', ')}; paused a boolean` });
    return;
  }
  const n = name as SwitchName;
  const text = String(message ?? '').slice(0, 300);
  try {
    if (hasDatabase()) {
      await q(`INSERT INTO switches (name, paused, message) VALUES ($1, $2, $3)
               ON CONFLICT (name) DO UPDATE SET paused = $2, message = $3, changed_at = now()`, [n, paused, text]);
      cache = null;
    } else if (paused) local.set(n, text);
    else local.delete(n);
    console.log(`[switches] ${n} ${paused ? 'PAUSED' : 'open'}${text ? ` - ${text}` : ''}`);
    if (!paused) onReopen?.(n);
    res.json({ name: n, paused, message: text });
  } catch (e) {
    internal(res, e, 'switches');
  }
}

/** GET /status - public: what is paused, and the oldest game build still served. */
export async function status(_req: Request, res: Response): Promise<void> {
  try {
    const paused = Object.fromEntries([...(await pausedSwitches())].map(([k, v]) => [k, v || DEFAULT_MESSAGE[k]]));
    res.setHeader('Cache-Control', 'no-store');
    // `format`: the run format this instance plays (runFormat.ts) - `showcase` switches the game into it.
    res.json({ paused, minClient: MIN_CLIENT_BUILD, format: primaryFormat() });
  } catch (e) {
    internal(res, e, 'status', 503, 'unavailable');
  }
}

/**
 * The game names its build on every request (X-Client-Build - Assets/Scripts/Chain/Service/
 * ServiceClient.cs and the connector). Older than MIN_CLIENT_BUILD: 426, and the game reloads the
 * page. A request that names no build (an older game, or a script) is let through.
 */
export function clientBuild(req: Request, res: Response, next: NextFunction) {
  const named = req.headers['x-client-build'];
  if (named !== undefined && Number(named) < MIN_CLIENT_BUILD) {
    res.status(426).json({ error: 'the game has been updated - reload the page', code: 'client_outdated' });
    return;
  }
  next();
}
