// The session rules (src/session.ts) against a running service - no chain writes, no operator token.
//   BLOCKFROST_API_KEY=preview... node scripts/smoke-session.mjs [serviceUrl]
import { Blockfrost, Lucid, generatePrivateKey } from '@lucid-evolution/lucid';
import { sessionClient } from './session-client.mjs';

const url = process.argv[2] ?? 'http://localhost:8787';
const bfKey = process.env.BLOCKFROST_API_KEY;
if (!bfKey?.startsWith('preview') && !bfKey?.startsWith('preprod')) throw new Error('testnet BLOCKFROST_API_KEY required');
const network = bfKey.startsWith('preview') ? 'Preview' : 'Preprod';
const lucid = await Lucid(new Blockfrost(`https://cardano-${network.toLowerCase()}.blockfrost.io/api/v0`, bfKey), network);
lucid.selectWallet.fromPrivateKey(generatePrivateKey());
const address = await lucid.wallet().address();

let failed = 0;
// /auth/* is limited to a burst of 10 per IP (rateLimit.ts) - paced under it.
const pace = () => new Promise(r => setTimeout(r, 2100));
const expect = (name, r, status, code) => {
  const ok = r.status === status && (!code || r.json?.code === code);
  if (!ok) failed++;
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${name.padEnd(34)} ${r.status} ${JSON.stringify(r.json).slice(0, 140)}`);
  return pace();
};

const a = await sessionClient(url);
await expect('challenge without a key', await a.call(`/auth/challenge?address=${address}`), 400);
const login = await a.login(lucid, address);
await expect('login', login, 200);
console.log('     message names the key:', /Session key: \S+/.exec(login.challenge.message)?.[0]);
const s = a.session;

await expect('session, signed', await a.call('/auth/session', { auth: s }), 200);
await expect('session, unsigned', await a.call('/auth/session', { auth: s, prove: false }), 401, 'proof_missing');

const proof = await a.proof('GET', '/auth/session', '');
await expect('same proof twice (1st)', await a.call('/auth/session', { auth: s, prove: false, headers: { 'X-Session-Proof': proof } }), 200);
await expect('same proof twice (2nd)', await a.call('/auth/session', { auth: s, prove: false, headers: { 'X-Session-Proof': proof } }), 401, 'proof_replayed');

const bodyProof = await a.proof('POST', '/write/begin-run', JSON.stringify({ profileId: 'x' }));
await expect('proof for another body', await a.call('/write/begin-run', { method: 'POST', auth: s, prove: false,
  body: { profileId: 'y' }, headers: { 'X-Session-Proof': bodyProof } }), 401, 'proof_invalid');

const b = await sessionClient(url);
await expect("token used with another key", await b.call('/auth/session', { auth: s }), 401, 'proof_invalid');
await expect('stale nonce', await a.call('/auth/session', { auth: s, prove: false,
  headers: { 'X-Session-Proof': `${Date.now()}.notanonce.${'A'.repeat(86)}` } }), 401, 'proof_nonce');
await expect('clock far off', await a.call('/auth/session', { auth: s, prove: false,
  headers: { 'X-Session-Proof': `${Date.now() - 10 * 60_000}.${a.nonce}.${'A'.repeat(86)}` } }), 401, 'proof_clock');

const second = await sessionClient(url);
await second.login(lucid, address);
await expect('second device signed in', await second.call('/auth/session', { auth: second.session }), 200);
await expect('sign out everywhere', await a.call('/auth/signout-everywhere', { method: 'POST', auth: s, body: {} }), 200);
await expect('first session after sign-out', await a.call('/auth/session', { auth: s }), 401, 'session_revoked');
await expect('second device after sign-out', await second.call('/auth/session', { auth: second.session }), 401, 'session_revoked');
const again = await a.login(lucid, address);
await expect('signing in again', await a.call('/auth/session', { auth: a.session }), 200);

console.log(failed ? `\n${failed} FAILED` : '\nall passed');
process.exit(failed ? 1 : 0);
