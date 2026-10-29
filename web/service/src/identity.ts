import type { NextFunction, Request, Response } from 'express';
import { internal } from './errors.js';
import { createHmac, randomBytes, timingSafeEqual } from 'node:crypto';
import { q } from './db.js';
import { bearer, verifySession } from './session.js';

/**
 * WHO A SAVE BELONGS TO - a guest, or a proven wallet.
 *
 * Every browser player starts as a GUEST (the user's call, 2026-10-13): the service issues an id,
 * the browser keeps the token, and every save from the first run on is held here under
 * `guest:<id>`. Nothing is ever copied up from the browser, so when the player proves a wallet the
 * guest's characters can GRADUATE onto it with nothing unverified in them (routes/saves.ts).
 *
 *   POST /guest   -> { guest: "guest.<id>.<hmac>", id }     rate limited per IP
 *
 * The token is stateless like a session (`SESSION_SECRET`), but never expires: a guest is a save
 * slot, not a login. Losing it loses the guest's characters, exactly as clearing site data loses a
 * local save. A GRADUATED guest's token is refused (410) - its records belong to the wallet now.
 */
const GUEST_PREFIX = 'guest.';

function secret(): string {
  const s = process.env.SESSION_SECRET;
  if (!s || s.length < 32) throw new Error('SESSION_SECRET is not set (32+ chars)');
  return s;
}

const mac = (body: string) => createHmac('sha256', secret()).update(`guest:${body}`).digest('base64url');

/** The guest id a token names, or null if the token is not a genuine guest token. */
export function guestId(token: string): string | null {
  if (!token.startsWith(GUEST_PREFIX)) return null;
  const [, id, sig] = token.split('.');
  if (!id || !sig) return null;
  const want = Buffer.from(mac(id));
  const got = Buffer.from(sig);
  if (want.length !== got.length || !timingSafeEqual(want, got)) return null;
  return id;
}

export const guestOwner = (id: string) => `guest:${id}`;

// ---- guest issuance, rate limited

const PER_IP_PER_HOUR = Number(process.env.GUESTS_PER_IP_PER_HOUR ?? 20);
const issued = new Map<string, number[]>();

/** In-memory and per replica - a brake on a bot filling the table, not an accounting system. */
function allow(ip: string): boolean {
  const now = Date.now();
  const recent = (issued.get(ip) ?? []).filter(t => now - t < 3_600_000);
  if (recent.length >= PER_IP_PER_HOUR) {
    issued.set(ip, recent);
    return false;
  }
  recent.push(now);
  issued.set(ip, recent);
  if (issued.size > 50_000) issued.clear();   // never a memory leak, at the cost of a brief reset
  return true;
}

export async function newGuest(req: Request, res: Response): Promise<void> {
  const ip = req.ip ?? 'unknown';
  if (!allow(ip)) {
    res.status(429).json({ error: 'too many new guests from this address - try again later' });
    return;
  }
  try {
    const id = randomBytes(12).toString('base64url');
    await q('INSERT INTO guests (id, created_ip) VALUES ($1, $2)', [id, ip]);
    res.json({ guest: `${GUEST_PREFIX}${id}.${mac(id)}`, id });
  } catch (e) {
    internal(res, e, 'guest');
  }
}

/**
 * Save routes: `Authorization: Bearer <guest token | wallet session>`. Puts `res.locals.owner`
 * (the key every save is stored under) and `res.locals.kind` ('guest' | 'wallet').
 */
export async function requireIdentity(req: Request, res: Response, next: NextFunction) {
  const token = bearer(req);
  try {
    const gid = token ? guestId(token) : null;
    if (gid) {
      const rows = await q<{ graduated_to: string | null }>('SELECT graduated_to FROM guests WHERE id = $1', [gid]);
      if (!rows.length) return void res.status(401).json({ error: 'unknown guest' });
      if (rows[0].graduated_to)
        return void res.status(410).json({ error: 'graduated', wallet: rows[0].graduated_to });
      res.locals.owner = guestOwner(gid);
      res.locals.kind = 'guest';
      res.locals.guestId = gid;
      return next();
    }
    if (token) {
      const v = await verifySession(req, token);
      if ('refused' in v) return void res.status(401).json({ error: v.refused, code: v.code });
      res.locals.owner = v.address;
      res.locals.kind = 'wallet';
      res.locals.address = v.address;
      res.locals.sessionExpires = v.expiresAt;
      return next();
    }
  } catch (e) {
    return void internal(res, e, 'identity', 503, 'unavailable');
  }
  res.status(401).json({ error: 'no identity - start a guest, or prove a wallet' });
}
