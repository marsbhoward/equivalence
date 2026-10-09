import type { NextFunction, Request, Response } from 'express';
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
 * The session is stateless - `<address>.<expiry>.<hmac>` under `SESSION_SECRET` - so a redeploy
 * keeps everyone logged in, and rotating the secret logs everyone out.
 */
const CHALLENGE_MS = 5 * 60_000;
const SESSION_MS = 12 * 60 * 60_000;

const challenges = new Map<string, { address: string; expires: number }>();

export const loginMessage = (address: string, nonce: string) =>
  `Equivalence login\naddress: ${address}\nnonce: ${nonce}`;

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

  const nonce = randomBytes(16).toString('hex');
  challenges.set(nonce, { address, expires: now + CHALLENGE_MS });
  res.json({ nonce, message: loginMessage(address, nonce) });
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
    ok = verifyData(f.hex, f.keyHash, hexOf(loginMessage(address, nonce)), { signature, key });
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
    res.status(503).json({ error: (e as Error).message });
    return;
  }
  if (!address) {
    res.status(401).json({ error: 'no valid session - prove the address first' });
    return;
  }
  res.locals.address = address;
  next();
}
