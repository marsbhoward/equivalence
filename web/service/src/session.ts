import type { NextFunction, Request, Response } from 'express';
import { internal } from './errors.js';
import { createHash, createHmac, createPublicKey, randomBytes, timingSafeEqual, verify, type KeyObject } from 'node:crypto';
import { getAddressDetails, verifyData } from '@lucid-evolution/lucid';
import { hasDatabase, q } from './db.js';

/**
 * PLAYER authority - how the browser build proves who it is, since it can never hold WRITE_TOKEN.
 *
 *   GET  /auth/challenge?address=<hex|bech32>&key=<x>.<y>  -> { nonce, message }   one-time, 5 min
 *   POST /auth/prove { address, nonce, signature, key }    CIP-30 signData output
 *        -> { session, address, expiresAt }
 *   GET  /auth/session                                     -> { address, expiresAt }   still good?
 *   POST /auth/signout-everywhere                          -> every session of this address ends
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
 * host is used - the address the client reached this service at.
 *
 * THE SESSION IS BOUND TO A KEY THAT NEVER LEAVES THE BROWSER (2026-10-29). The connector makes a
 * non-extractable P-256 key, names its public half in the challenge (so the wallet's signature
 * vouches for it - the message says "Session key"), and signs every request with it:
 *
 *   X-Session-Proof: <ts>.<proof nonce>.<sig>    over  METHOD \n url \n ts \n nonce \n sha256(body)
 *
 * The token alone is worthless - a script that reads it out of browser storage cannot use it from
 * anywhere else, because the private key cannot be exported. The PROOF NONCE rotates every five
 * minutes (sent on every response as X-Proof-Nonce), so proofs signed ahead of time by a script in
 * the page go stale within ten; an exact proof seen before is refused, and the body hash ties each
 * proof to the one request it was made for.
 *
 * SESSIONS CAN BE ENDED. The token carries the address's EPOCH (`session_epochs`); signing out
 * everywhere bumps it and every older session of that address stops at once. What a stolen session
 * could do was bounded anyway: it moves no token (opening boxes needs the wallet's transaction
 * signature), and a save it overwrites is kept in `character_history`.
 *
 * The session is otherwise stateless - `v2.<address>.<expiry>.<epoch>.<x>.<y>.<hmac>` under
 * `SESSION_SECRET` - so a redeploy keeps everyone logged in, and rotating the secret logs everyone out.
 */
const CHALLENGE_MS = 5 * 60_000;
const SESSION_MS = 12 * 60 * 60_000;
/** How far a proof's clock may sit from ours - a phone's clock drifts, a request queues. */
const PROOF_SKEW_MS = 2 * 60_000;
/** One proof nonce window; the current and the previous one are accepted. */
const NONCE_WINDOW_MS = 5 * 60_000;
/** How long an epoch read is trusted before the database is asked again (other replicas' bumps). */
const EPOCH_CACHE_MS = 30_000;

interface Challenge {
  address: string;
  domain: string;
  issuedAt: number;
  expires: number;
  /** The session key's public half, `<x>.<y>` - what the session will be bound to. */
  key: string;
}
const challenges = new Map<string, Challenge>();
/** Outstanding challenges at most - a ceiling on memory under a flood the per-IP limit spreads out. */
const MAX_CHALLENGES = 20_000;

/** A short, readable name for a session key - the line the wallet shows. */
const thumbprint = (key: string) => createHash('sha256').update(key).digest('base64url').slice(0, 16);

/** The text the wallet shows and signs - rebuilt at /auth/prove from what the challenge recorded. */
export const loginMessage = (address: string, nonce: string, c: Omit<Challenge, 'address'>) =>
  `${c.domain} wants you to sign in to Equivalence with your Cardano address:\n` +
  `${address}\n\n` +
  `Only sign this on https://${c.domain}. It proves this address is yours: it moves nothing, ` +
  `costs nothing and approves no transaction.\n\n` +
  `URI: https://${c.domain}\n` +
  `Session key: ${thumbprint(c.key)}\n` +
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

/** A P-256 public key from its two coordinates (`<x>.<y>`, base64url), or null. */
const COORD = /^[A-Za-z0-9_-]{43}$/;
const keys = new Map<string, KeyObject>();
function publicKey(key: string): KeyObject | null {
  const hit = keys.get(key);
  if (hit) return hit;
  const [x, y, extra] = key.split('.');
  if (extra !== undefined || !COORD.test(x ?? '') || !COORD.test(y ?? '')) return null;
  try {
    const k = createPublicKey({ key: { kty: 'EC', crv: 'P-256', x, y }, format: 'jwk' });
    if (keys.size > 5000) keys.clear();
    keys.set(key, k);
    return k;
  } catch {
    return null;   // not a point on the curve
  }
}

function secret(): string {
  const s = process.env.SESSION_SECRET;
  if (!s || s.length < 32) throw new Error('SESSION_SECRET is not set (32+ chars)');
  return s;
}

const mac = (body: string) => createHmac('sha256', secret()).update(body).digest('base64url');

// ---- proof nonces ------------------------------------------------------------------------------

const nonceFor = (window: number) => mac(`proof-nonce.${window}`).slice(0, 22);
/** The nonce a proof signed now must name. */
export const proofNonce = () => nonceFor(Math.floor(Date.now() / NONCE_WINDOW_MS));
function nonceCurrent(n: string): boolean {
  const w = Math.floor(Date.now() / NONCE_WINDOW_MS);
  return n === nonceFor(w) || n === nonceFor(w - 1);
}

/** On every response, so the client always holds the current one. */
export function sendProofNonce(_req: Request, res: Response, next: NextFunction) {
  try {
    res.setHeader('X-Proof-Nonce', proofNonce());
  } catch {
    // No SESSION_SECRET - no sessions either; the routes that need one say so themselves.
  }
  next();
}

/** Proofs already used, until they could no longer pass the clock check anyway. */
const seen = new Map<string, number>();
const MAX_SEEN = 200_000;
function firstUse(sig: string): boolean {
  const now = Date.now();
  if (seen.size > MAX_SEEN) for (const [k, t] of seen) if (t < now) seen.delete(k);
  if (seen.has(sig)) return false;
  seen.set(sig, now + 2 * PROOF_SKEW_MS);
  return true;
}

// ---- epochs (signing out everywhere) ---------------------------------------------------------

/** Without a database (local dev) epochs live here and every restart resets them. */
const epochs = new Map<string, { epoch: number; at: number }>();

async function epochOf(address: string): Promise<number> {
  const hit = epochs.get(address);
  if (hit && (!hasDatabase() || Date.now() - hit.at < EPOCH_CACHE_MS)) return hit.epoch;
  if (!hasDatabase()) return 0;
  const rows = await q<{ epoch: number }>('SELECT epoch FROM session_epochs WHERE address = $1', [address]);
  const epoch = rows[0]?.epoch ?? 0;
  epochs.set(address, { epoch, at: Date.now() });
  return epoch;
}

async function bumpEpoch(address: string): Promise<number> {
  let epoch = ((epochs.get(address)?.epoch) ?? 0) + 1;
  if (hasDatabase()) {
    const rows = await q<{ epoch: number }>(
      `INSERT INTO session_epochs (address, epoch) VALUES ($1, 1)
       ON CONFLICT (address) DO UPDATE SET epoch = session_epochs.epoch + 1, bumped_at = now()
       RETURNING epoch`, [address]);
    epoch = rows[0].epoch;
  }
  epochs.set(address, { epoch, at: Date.now() });
  return epoch;
}

// ---- routes ------------------------------------------------------------------------------------

export function challenge(req: Request, res: Response): void {
  const address = String(req.query.address ?? '');
  const key = String(req.query.key ?? '');
  if (!forms(address)) {
    res.status(400).json({ error: 'address must be a key-based Cardano address' });
    return;
  }
  if (!publicKey(key)) {
    res.status(400).json({ error: 'key must be a P-256 public key, <x>.<y> in base64url' });
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
  const c: Challenge = { address, domain: domainOf(req), issuedAt: now, expires: now + CHALLENGE_MS, key };
  challenges.set(nonce, c);
  res.json({ nonce, message: loginMessage(address, nonce, c), expiresAt: c.expires });
}

export async function prove(req: Request, res: Response): Promise<void> {
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

  try {
    const expiresAt = Date.now() + SESSION_MS;
    const body = `v2.${f.bech32}.${expiresAt}.${await epochOf(f.bech32)}.${c.key}`;
    res.json({ session: `${body}.${mac(body)}`, address: f.bech32, expiresAt });
  } catch (e) {
    internal(res, e, 'prove', 503, 'unavailable');
  }
}

/** GET /auth/session - whether a remembered session still holds, before the game leans on it. */
export function sessionStatus(_req: Request, res: Response): void {
  res.json({ address: res.locals.address, expiresAt: res.locals.sessionExpires });
}

/** POST /auth/signout-everywhere - this address's every session, on every device, ends now. */
export async function signOutEverywhere(_req: Request, res: Response): Promise<void> {
  try {
    await bumpEpoch(res.locals.address as string);
    res.json({ ok: true });
  } catch (e) {
    internal(res, e, 'signout', 503, 'unavailable');
  }
}

// ---- checking a request ------------------------------------------------------------------------

type Verdict = { address: string; expiresAt: number } | { refused: string; code: string };

/**
 * A request's session, checked end to end: the token's MAC, its expiry and epoch, and the proof
 * that the request was signed by the key the token names.
 */
export async function verifySession(req: Request, token: string): Promise<Verdict> {
  const parts = token.split('.');
  if (parts.length !== 7 || parts[0] !== 'v2') return { refused: 'not a session - sign in again', code: 'session_invalid' };
  const [, address, expires, epoch, x, y, sig] = parts;
  const want = Buffer.from(mac(parts.slice(0, 6).join('.')));
  const got = Buffer.from(sig);
  if (want.length !== got.length || !timingSafeEqual(want, got)) return { refused: 'not a session - sign in again', code: 'session_invalid' };
  if (Number(expires) < Date.now()) return { refused: 'the session has expired - sign in again', code: 'session_expired' };
  if (Number(epoch) !== await epochOf(address)) return { refused: 'the session was signed out', code: 'session_revoked' };

  const proof = String(req.headers['x-session-proof'] ?? '');
  const [ts, nonce, proofSig, extra] = proof.split('.');
  if (!ts || !proofSig || extra !== undefined) return { refused: 'the request carries no session proof', code: 'proof_missing' };
  // No nonce yet is a client's first request: it reads X-Proof-Nonce off this answer and retries.
  if (!nonce) return { refused: 'stale proof nonce', code: 'proof_nonce' };
  if (Math.abs(Date.now() - Number(ts)) > PROOF_SKEW_MS) return { refused: "the request's clock is off", code: 'proof_clock' };
  if (!nonceCurrent(nonce)) return { refused: 'stale proof nonce', code: 'proof_nonce' };

  const key = publicKey(`${x}.${y}`);
  if (!key) return { refused: 'not a session - sign in again', code: 'session_invalid' };
  const raw: Buffer | undefined = (req as Request & { rawBody?: Buffer }).rawBody;
  const bodyHash = createHash('sha256').update(raw ?? Buffer.alloc(0)).digest('base64url');
  const signed = `${req.method}\n${req.originalUrl}\n${ts}\n${nonce}\n${bodyHash}`;
  let ok = false;
  try {
    ok = verify('sha256', Buffer.from(signed), { key, dsaEncoding: 'ieee-p1363' }, Buffer.from(proofSig, 'base64url'));
  } catch {
    ok = false;
  }
  if (!ok) return { refused: 'the session proof does not match', code: 'proof_invalid' };
  if (!firstUse(proofSig)) return { refused: 'that request was already made', code: 'proof_replayed' };
  return { address, expiresAt: Number(expires) };
}

/** The Bearer token on a request, or ''. */
export const bearer = (req: Request) => {
  const header = req.headers.authorization ?? '';
  return header.startsWith('Bearer ') ? header.slice(7) : '';
};

/** Player routes: `Authorization: Bearer <session>` + `X-Session-Proof`; the address lands on res.locals. */
export async function requireSession(req: Request, res: Response, next: NextFunction) {
  const token = bearer(req);
  let v: Verdict;
  try {
    v = token ? await verifySession(req, token) : { refused: 'no valid session - prove the address first', code: 'session_missing' };
  } catch (e) {
    internal(res, e, 'session', 503, 'unavailable');
    return;
  }
  if ('refused' in v) {
    res.status(401).json({ error: v.refused, code: v.code });
    return;
  }
  res.locals.address = v.address;
  res.locals.sessionExpires = v.expiresAt;
  next();
}
