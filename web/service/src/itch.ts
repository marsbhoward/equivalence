import express, { type Request, type Response } from 'express';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { internal } from './errors.js';
import { pageHeaders } from './play.js';
import { issueSession, validKey } from './session.js';

/**
 * SIGN IN WITH ITCH.IO (2026-11-04, the user's call) - a sign-in without a wallet, for the players
 * who found the game on itch. It earns the same key-bound session a wallet does (session.ts), under
 * the identity `itch:<user id>`: saves, graduation from a guest and the "was there" title all work
 * the same; nothing on chain does (`requireSession` refuses it - no boxes, no PFP, no minting).
 *
 * itch's browser flow is OAuth's IMPLICIT grant: the game sends the player to itch.io/user/oauth,
 * itch sends them back to `/itch/callback#access_token=...` (the app's registered callback - one
 * URL, so only the instance named by ITCH_CLIENT_ID offers it), the page hands the token to the game
 * in localStorage (read once) and returns to /play/, and the game trades it here:
 *
 *   GET  /auth/itch/config                 -> { clientId }   404 where itch sign-in is not set up
 *   POST /auth/itch { token, key }         -> { session, address: "itch:<id>", name, expiresAt }
 *
 * The token is checked with itch (GET api.itch.io/profile, scope profile:me) and never stored: only
 * the user id it names is kept, as the identity. `key` is this browser's session key, as at /auth/prove.
 */
const PROFILE = 'https://api.itch.io/profile';
const FETCH_MS = 6000;
const TOKEN = /^[A-Za-z0-9._~+/=-]{8,512}$/;

export const ITCH_PREFIX = 'itch:';
export const isItch = (address: string) => address.startsWith(ITCH_PREFIX);

const clientId = () => (process.env.ITCH_CLIENT_ID ?? '').trim();

export function itchConfig(_req: Request, res: Response): void {
  const id = clientId();
  if (!id) return void res.status(404).json({ error: 'itch sign-in is not set up here' });
  res.json({ clientId: id });
}

export async function itchSignIn(req: Request, res: Response): Promise<void> {
  if (!clientId()) return void res.status(404).json({ error: 'itch sign-in is not set up here' });
  const { token, key } = (req.body ?? {}) as { token?: string; key?: string };
  if (!token || !TOKEN.test(token)) return void res.status(400).json({ error: 'token is required' });
  if (!key || !validKey(key)) return void res.status(400).json({ error: 'key must be a P-256 public key, <x>.<y> in base64url' });

  let user: { id?: number; username?: string; display_name?: string } | undefined;
  try {
    const r = await fetch(PROFILE, {
      headers: { authorization: `Bearer ${token}` },
      signal: AbortSignal.timeout(FETCH_MS),
    });
    if (r.status === 401 || r.status === 403) return void res.status(401).json({ error: 'itch.io did not accept the sign-in - try again' });
    if (!r.ok) return void res.status(502).json({ error: `itch.io answered ${r.status}` });
    user = ((await r.json()) as { user?: typeof user }).user;
  } catch (e) {
    return void internal(res, e, 'itch-profile', 502, 'itch.io could not be reached');
  }
  if (!user || !Number.isSafeInteger(user.id)) return void res.status(502).json({ error: 'itch.io sent no user' });

  try {
    const address = `${ITCH_PREFIX}${user.id}`;
    const issued = await issueSession(address, key);
    res.json({ ...issued, name: String(user.display_name || user.username || '').slice(0, 64) });
  } catch (e) {
    internal(res, e, 'itch-session', 503, 'unavailable');
  }
}

/** The page itch sends the player back to (web/service/itch) - one script, under the game's CSP. */
const dir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'itch');

export function itchCallbackPage(): express.Router {
  const r = express.Router();
  r.get('/callback', (_req, res) => {
    const file = path.join(dir, 'index.html');
    res.setHeader('Cache-Control', 'no-cache');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    pageHeaders(res, file);
    res.sendFile(file);
  });
  r.get('/callback.js', (_req, res) => {
    res.setHeader('Cache-Control', 'no-cache');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.type('application/javascript').sendFile(path.join(dir, 'callback.js'));
  });
  return r;
}
