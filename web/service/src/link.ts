import type { Request, Response } from 'express';
import { randomBytes, timingSafeEqual } from 'node:crypto';
import { CML } from '@lucid-evolution/lucid';
import { forms, openChallenge, proveChallenge, type Proved } from './session.js';

/**
 * DEVICE LINKS (2026-11-01) - a wallet that lives in ANOTHER browser signs for this one.
 *
 * On a phone the game runs in Safari (or from the Home Screen), where no Cardano wallet can inject
 * itself; the wallet is an app with its own browser. So the game never talks to the wallet: it asks
 * the service for a short CODE, the player opens our small /link page in the wallet's browser
 * (CIP-158 `web+cardano://browse/v1?uri=` on Eternl and VESPR, or the link copied, or the code typed
 * at /link on a computer with an extension), and that page does the one wallet call the game needs:
 *
 *   SIGN-IN   the game makes its session key as always (connector), and /link/start records its
 *             PUBLIC half under the code. The page signs the ordinary login message for THAT key
 *             (session.ts, with a "Device link" line naming the code), and the session it earns is
 *             held here until the game's poll collects it. Bound to the game's key, it is useless
 *             to the page - and the page never receives it.
 *
 *   SIGNING   a transaction the service built (open boxes, the PFP, a burn) is posted here by the
 *             game's SESSION; the page fetches it, checks the wallet is the session's own address,
 *             and has the wallet sign it (partial). The witness comes back here and its signatures are
 *             checked against the transaction before the game collects it.
 *
 *   POST /link/start    { key }                     -> { code, poll, expiresAt }       the game
 *   GET  /link/poll     ?code&poll                  -> { state, session?, address?, expiresAt? }
 *   POST /link/sign     { cbor, what }   (session)  -> { code, expiresAt }             the game
 *   GET  /link/signed   ?code            (session)  -> { state, witness? }
 *   GET  /link/req      ?code                       -> what the code asks for          the page
 *   GET  /link/challenge ?code&address              -> { nonce, message }
 *   POST /link/prove    { code, address, nonce, signature, key }
 *   POST /link/witness  { code, witness } | { code, declined: true }
 *
 * WHAT A CODE IS WORTH. Six characters of 32 (~10^9), alive for minutes, looked up under a per-IP
 * limit. A sign-in code can only ever sign someone IN TO the device that made it, and the message
 * the wallet shows says so; the phishing case (a stranger's code sent to a victim) is the device-code
 * flow's known one, answered by that message and the code shown on both screens. A signing code
 * carries a transaction anyone could read on chain once submitted, and only signatures over that very
 * transaction are ever accepted for it.
 *
 * Held in memory, like the login challenges: a restart drops links in progress and the game asks again.
 */
const SIGN_IN_MS = 10 * 60_000;
const SIGN_MS = 10 * 60_000;
const MAX_LINKS = 20_000;
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

/** What a signing request may say it is for - the page shows its own words, never the game's. */
const WHAT = new Set(['open-boxes', 'pfp', 'pfp-renew', 'dyes', 'break-boxes']);

interface SignIn {
  kind: 'signin';
  key: string;
  poll: string;
  expires: number;
  done?: Proved;
}
interface SignTx {
  kind: 'sign';
  address: string;          // bech32 - the session's
  hex: string;              // the same address as CIP-30 hands it out
  cbor: string;
  what: string;
  expires: number;
  witness?: string;
  declined?: boolean;
}
const links = new Map<string, SignIn | SignTx>();

function sweep() {
  const now = Date.now();
  for (const [c, l] of links) if (l.expires < now) links.delete(c);
}

function newCode(): string | null {
  sweep();
  if (links.size >= MAX_LINKS) return null;
  for (;;) {
    const bytes = randomBytes(6);
    const code = Array.from(bytes, b => ALPHABET[b & 31]).join('');
    if (!links.has(code)) return code;
  }
}

/** Typed by hand: case, spaces and the dash between the halves don't matter. */
const norm = (code: unknown) => String(code ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 12);

function live(code: string): SignIn | SignTx | null {
  const l = links.get(code);
  if (!l) return null;
  if (l.expires < Date.now()) { links.delete(code); return null; }
  return l;
}

const busy = (res: Response) => {
  res.status(503).setHeader('Retry-After', '60');
  res.json({ error: 'too many links in progress - try again shortly' });
};

// ---- the game's side ---------------------------------------------------------------------------

export function startLink(req: Request, res: Response): void {
  const key = String(req.body?.key ?? '');
  if (!/^[A-Za-z0-9_-]{43}\.[A-Za-z0-9_-]{43}$/.test(key)) {
    res.status(400).json({ error: 'key must be a P-256 public key, <x>.<y> in base64url' });
    return;
  }
  const code = newCode();
  if (!code) return busy(res);
  const poll = randomBytes(24).toString('base64url');
  const expires = Date.now() + SIGN_IN_MS;
  links.set(code, { kind: 'signin', key, poll, expires });
  res.json({ code, poll, expiresAt: expires });
}

export function pollLink(req: Request, res: Response): void {
  const l = live(norm(req.query.code));
  const poll = Buffer.from(String(req.query.poll ?? ''));
  if (!l || l.kind !== 'signin' || poll.length !== l.poll.length || !timingSafeEqual(poll, Buffer.from(l.poll))) {
    res.json({ state: 'expired' });
    return;
  }
  if (!l.done) {
    res.json({ state: 'waiting', expiresAt: l.expires });
    return;
  }
  links.delete(norm(req.query.code));   // collected once
  res.json({ state: 'done', ...l.done });
}

export function signLink(req: Request, res: Response): void {
  const cbor = String(req.body?.cbor ?? '');
  const what = String(req.body?.what ?? '');
  const f = forms(res.locals.address as string);
  if (!/^[0-9a-f]+$/i.test(cbor) || cbor.length > 64_000 || !WHAT.has(what) || !f) {
    res.status(400).json({ error: 'cbor (hex) and a known what are required' });
    return;
  }
  try {
    CML.Transaction.from_cbor_hex(cbor).free();
  } catch {
    res.status(400).json({ error: 'not a transaction' });
    return;
  }
  const code = newCode();
  if (!code) return busy(res);
  const expires = Date.now() + SIGN_MS;
  links.set(code, { kind: 'sign', address: f.bech32, hex: f.hex, cbor, what, expires });
  res.json({ code, expiresAt: expires });
}

export function signedLink(req: Request, res: Response): void {
  const l = live(norm(req.query.code));
  if (!l || l.kind !== 'sign' || l.address !== res.locals.address) {
    res.json({ state: 'expired' });
    return;
  }
  if (l.witness) {
    links.delete(norm(req.query.code));
    res.json({ state: 'signed', witness: l.witness });
  } else res.json({ state: l.declined ? 'declined' : 'waiting', expiresAt: l.expires });
}

// ---- the page's side ---------------------------------------------------------------------------

export function linkRequest(req: Request, res: Response): void {
  const code = norm(req.query.code);
  const l = live(code);
  if (!l) {
    res.status(404).json({ error: 'that code has expired or was never made - ask the game for a new one' });
    return;
  }
  if (l.kind === 'signin') res.json({ kind: 'signin', code, expiresAt: l.expires, done: !!l.done });
  else res.json({ kind: 'sign', code, expiresAt: l.expires, what: l.what, address: l.hex, cbor: l.cbor,
    done: !!l.witness || !!l.declined });
}

export function linkChallenge(req: Request, res: Response): void {
  const code = norm(req.query.code);
  const l = live(code);
  if (!l || l.kind !== 'signin' || l.done) {
    res.status(404).json({ error: 'that code has expired or was already used' });
    return;
  }
  openChallenge(req, res, String(req.query.address ?? ''), l.key, code);
}

export async function linkProve(req: Request, res: Response): Promise<void> {
  const code = norm(req.body?.code);
  const l = live(code);
  if (!l || l.kind !== 'signin' || l.done) {
    res.status(404).json({ error: 'that code has expired or was already used' });
    return;
  }
  const r = await proveChallenge((req.body ?? {}) as Record<string, string>, code);
  if (!('session' in r)) {
    res.status(r.status).json({ error: r.error });
    return;
  }
  l.done = r;
  // The page learns only that it worked - the session is the game's, bound to the game's key.
  res.json({ ok: true });
}

export function linkWitness(req: Request, res: Response): void {
  const code = norm(req.body?.code);
  const l = live(code);
  if (!l || l.kind !== 'sign' || l.witness) {
    res.status(404).json({ error: 'that code has expired or was already signed' });
    return;
  }
  if (req.body?.declined === true) {
    l.declined = true;
    res.json({ ok: true });
    return;
  }
  const witness = String(req.body?.witness ?? '');
  if (!witnessFits(l, witness)) {
    res.status(400).json({ error: 'that signature is not from the wallet signed in to the game' });
    return;
  }
  l.witness = witness;
  l.declined = false;
  res.json({ ok: true });
}

/**
 * Every signature in the witness set is over THIS transaction's body. Not tied to the session's
 * address: a wallet spends from any of its addresses, so its keys need not include that one. A
 * stranger who guessed the code can only add signatures that make the submit fail, never a junk
 * witness the game then carries to the service.
 */
function witnessFits(l: SignTx, witness: string): boolean {
  if (!/^[0-9a-f]+$/i.test(witness) || witness.length > 8_000) return false;
  try {
    const tx = CML.Transaction.from_cbor_hex(l.cbor);
    const body = CML.hash_transaction(tx.body()).to_raw_bytes();
    const set = CML.TransactionWitnessSet.from_cbor_hex(witness);
    const vkeys = set.vkeywitnesses();
    if (!vkeys || vkeys.len() === 0) return false;
    for (let i = 0; i < vkeys.len(); i++) {
      const w = vkeys.get(i);
      if (!w.vkey().verify(body, w.ed25519_signature())) return false;
    }
    return true;
  } catch {
    return false;
  }
}
