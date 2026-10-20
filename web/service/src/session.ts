import type { NextFunction, Request, Response } from 'express';
import { internal } from './errors.js';
import { createHmac, randomBytes, timingSafeEqual } from 'node:crypto';
import { getAddressDetails, verifyData } from '@lucid-evolution/lucid';

/**
 * PLAYER authority - how the browser build proves who it is, since it can never hold WRITE_TOKEN.
 *
 *   GET  /auth/challenge?address=<hex|bech32>  -> { nonce, message }   one-time, 5 minutes
 *   POST /auth/prove { address, nonce, signature, key }                 CIP-30 signData output
 *        -> { session, address, expiresAt }
 *
 * The nonce is ISSUED here, never chosen by the client, and spent on first use - so a captured
 * signature can't be replayed. The message is exactly what the connector's `proveOwnership` signs.
 * `signData` moves nothing and costs nothing: a player never approves a transaction to log in.
 *
 * THE MESSAGE NAMES THE SITE (CIP-8 / "Sign-In With" style: domain, URI, issued-at, expiry). A
 * phishing page can fetch a real nonce for a victim's address and ask them to sign it; Cardano
 * wallets show the message but do not check it against the page, so the only defence is a message
 * the player can READ - the site it belongs to, what signing does, and that it is only to be signed
 * there. `LOGIN_DOMAIN` names the site (the host the game is served from); unset, the request's own
 * host is used - the address the client reached this service at. What a stolen session can do is
 * bounded anyway: it moves no token (opening boxes needs the wallet's transaction signature), and a
 * save it overwrites is kept in `character_history`.
 *
 * The session is stateless - `<address>.<expiry>.<hmac>` under `SESSION_SECRET` - so a redeploy
 * keeps everyone logged in, and rotating the secret logs everyone out.
 */
const CHALLENGE_MS = 5 * 60_000;
const SESSION_MS = 12 * 60 * 60_000;

interface Challenge {
  address: string;
  domain: string;
  issuedAt: number;
  expires: number;
}
const challenges = new Map<string, Challenge>();
/** Outstanding challenges at most - a ceiling on memory under a flood the per-IP limit spreads out. */
const MAX_CHALLENGES = 20_000;

/** The text the wallet shows and signs - rebuilt at /auth/prove from what the challenge recorded. */
export const loginMessage = (address: string, nonce: string, c: Omit<Challenge, 'address'>) =>
  `${c.domain} wants you to sign in to Equivalence with your Cardano address:\n` +
  `${address}\n\n` +
  `Only sign this on https://${c.domain}. It proves this address is yours: it moves nothing, ` +
  `costs nothing and approves no transaction.\n\n` +
  `URI: https://${c.domain}\n` +
  `Nonce: ${nonce}\n` +
  `Issued At: ${new Date(c.issuedAt).toISOString()}\n` +
  `Expiration Time: ${new Date(c.expires).toISOString()}`;

/** The site a login belongs to - LOGIN_DOMAIN, else the host this request reached. */
const DOMAIN = /^[a-z0-9.-]{1,253}(:\d{1,5})?$/i;
function domainOf(req: Request): string {
  const d = (process.env.LOGIN_DOMAIN || req.get('host') || '').trim().toLowerCase();
  return DOMAIN.test(d) ? d : 'unknown-host';
}

const hexOf = (s: string) => Buffer.from(s, 'utf8').toString('hex');

/** Both forms of an address - CIP-30 hands out hex, Blockfrost and the ledger want bech32. */
function forms(address: string): { bech32: string; hex: string; keyHash: string } | null {
  try {
    const d = getAddressDetails(address);
    const keyHash = d.paymentCredential?.type === 'Key' ? d.paymentCredential.hash : '';
    return keyHash ? { bech32: d.address.bech32, hex: d.address.hex, keyHash } : null;
  } catch {
    return null;
  }
}

function secret(): string {
  const s = process.env.SESSION_SECRET;
  if (!s || s.length < 32) throw new Error('SESSION_SECRET is not set (32+ chars)');
  return s;
}

const mac = (body: string) => createHmac('sha256', secret()).update(body).digest('base64url');

export function challenge(req: Request, res: Response): void {
  const address = String(req.query.address ?? '');
  if (!forms(address)) {
    res.status(400).json({ error: 'address must be a key-based Cardano address' });
    return;
  }
  const now = Date.now();
  for (const [n, c] of challenges) if (c.expires < now) challenges.delete(n);
  if (challenges.size >= MAX_CHALLENGES) {
    res.status(503).setHeader('Retry-After', '60');
    res.json({ error: 'too many logins in progress - try again shortly' });
    return;
  }

  const nonce = randomBytes(16).toString('hex');
  const c: Challenge = { address, domain: domainOf(req), issuedAt: now, expires: now + CHALLENGE_MS };
  challenges.set(nonce, c);
  res.json({ nonce, message: loginMessage(address, nonce, c), expiresAt: c.expires });
}

export function prove(req: Request, res: Response): void {
  const { address, nonce, signature, key } = (req.body ?? {}) as Record<string, string>;
  const c = nonce ? challenges.get(nonce) : undefined;
  // Spent whatever happens next: one nonce, one attempt.
  if (nonce) challenges.delete(nonce);
  if (!c || c.expires < Date.now() || c.address !== address) {
    res.status(401).json({ error: 'unknown or expired challenge - ask for a new one' });
    return;
  }
  const f = forms(address);
  if (!f || !signature || !key) {
    res.status(400).json({ error: 'address, nonce, signature and key are required' });
    return;
  }

  let ok = false;
  try {
    ok = verifyData(f.hex, f.keyHash, hexOf(loginMessage(address, nonce, c)), { signature, key });
  } catch {
    ok = false;
  }
  if (!ok) {
    res.status(401).json({ error: 'signature does not prove this address' });
    return;
  }

  const expiresAt = Date.now() + SESSION_MS;
  const body = `${f.bech32}.${expiresAt}`;
  res.json({ session: `${body}.${mac(body)}`, address: f.bech32, expiresAt });
}

/** The proven address of a session, or null. */
export function sessionAddress(token: string): string | null {
  const parts = token.split('.');
  if (parts.length !== 3) return null;
  const [address, expires, sig] = parts;
  const want = Buffer.from(mac(`${address}.${expires}`));
  const got = Buffer.from(sig);
  if (want.length !== got.length || !timingSafeEqual(want, got)) return null;
  if (Number(expires) < Date.now()) return null;
  return address;
}

/** Player routes: `Authorization: Bearer <session>`; the proven address lands on res.locals. */
export function requireSession(req: Request, res: Response, next: NextFunction) {
  const header = req.headers.authorization ?? '';
  const token = header.startsWith('Bearer ') ? header.slice(7) : '';
  let address: string | null = null;
  try {
    address = token ? sessionAddress(token) : null;
  } catch (e) {
    internal(res, e, 'session', 503, 'unavailable');
    return;
  }
  if (!address) {
    res.status(401).json({ error: 'no valid session - prove the address first' });
    return;
  }
  res.locals.address = address;
  next();
}
