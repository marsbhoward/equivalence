// Device links end to end (src/link.ts) against a running service - no chain needed:
//   node scripts/smoke-link.mjs [service url]
// A "game" (a session client, the way the connector holds one) and a "page" (a Lucid wallet on an
// emulator, standing in for the wallet app's browser): sign-in by code, then a transaction signed
// by the page and collected by the game.
import { Emulator, Lucid, generateEmulatorAccount } from '@lucid-evolution/lucid';
import { sessionClient } from './session-client.mjs';

const url = process.argv[2] ?? 'http://localhost:8787';
const hex = (s) => Buffer.from(s, 'utf8').toString('hex');
let failed = 0;
const check = (name, ok, extra = '') => { if (!ok) failed++; console.log(`${ok ? 'ok  ' : 'FAIL'} ${name} ${extra}`); };

const account = generateEmulatorAccount({ lovelace: 50_000_000n });
const lucid = await Lucid(new Emulator([account]), 'Custom');
lucid.selectWallet.fromSeed(account.seedPhrase);
const address = await lucid.wallet().address();

const game = await sessionClient(url);
const page = await sessionClient(url);   // only its plain calls are used

// 1. the game asks for a code
const start = await game.call('/link/start', { method: 'POST', body: { key: game.key } });
check('start', start.status === 200 && /^[A-Z2-9]{6}$/.test(start.json.code), JSON.stringify(start.json).slice(0, 80));
const { code, poll } = start.json;
check('poll waiting', (await game.call(`/link/poll?code=${code}&poll=${poll}`)).json.state === 'waiting');
check('poll, wrong secret', (await game.call(`/link/poll?code=${code}&poll=nope`)).json.state === 'expired');

// 2. the page signs the game in
const req = await page.call(`/link/req?code=${code.toLowerCase().slice(0, 3)}-${code.slice(3)}`);
check('page reads the code (typed loosely)', req.status === 200 && req.json.kind === 'signin');
const ch = await page.call(`/link/challenge?code=${code}&address=${address}`);
check('challenge names the link', ch.status === 200 && ch.json.message.includes(`Device link: ${code}`));
const sig = await lucid.wallet().signMessage(address, hex(ch.json.message));
// A link's challenge cannot be traded at the ordinary prove for a session the page keeps.
const stolen = await page.call('/auth/prove', { method: 'POST', body: { address, nonce: ch.json.nonce, ...sig } });
check('link challenge refused at /auth/prove', stolen.status === 401);
const ch2 = await page.call(`/link/challenge?code=${code}&address=${address}`);
const sig2 = await lucid.wallet().signMessage(address, hex(ch2.json.message));
const proved = await page.call('/link/prove', { method: 'POST', body: { code, address, nonce: ch2.json.nonce, ...sig2 } });
check('page proves', proved.status === 200 && !proved.json.session, JSON.stringify(proved.json));

// 3. the game collects its session - bound to the GAME's key
const done = await game.call(`/link/poll?code=${code}&poll=${poll}`);
check('game collects the session', done.json.state === 'done' && !!done.json.session);
check('collected once', (await game.call(`/link/poll?code=${code}&poll=${poll}`)).json.state === 'expired');
game.session = done.json.session;
check('session works for the game', (await game.call('/auth/session', { auth: game.session })).status === 200);
const other = await page.call('/auth/session', { auth: game.session });
check('session useless with another key', other.status === 401, other.json?.code);

// 4. a transaction, signed on the page
const tx = await lucid.newTx().pay.ToAddress(address, { lovelace: 2_000_000n }).complete();
const cbor = tx.toCBOR();
const sign = await game.call('/link/sign', { method: 'POST', auth: game.session, body: { cbor, what: 'open-boxes' } });
check('game posts a transaction', sign.status === 200 && !!sign.json.code, JSON.stringify(sign.json).slice(0, 80));
check('unknown what refused', (await game.call('/link/sign', { method: 'POST', auth: game.session, body: { cbor, what: 'x' } })).status === 400);
const sreq = await page.call(`/link/req?code=${sign.json.code}`);
check('page reads the transaction', sreq.json.kind === 'sign' && sreq.json.cbor === cbor);
check('junk witness refused', (await page.call('/link/witness', { method: 'POST', body: { code: sign.json.code, witness: 'a10080' } })).status === 400);
const witness = await lucid.fromTx(sreq.json.cbor).partialSign.withWallet();
const posted = await page.call('/link/witness', { method: 'POST', body: { code: sign.json.code, witness } });
check('real witness accepted', posted.status === 200, JSON.stringify(posted.json));
const got = await game.call(`/link/signed?code=${sign.json.code}`, { auth: game.session });
check('game collects the witness', got.json.state === 'signed' && got.json.witness === witness);

// 5. the ordinary sign-in still works
const plain = await page.call(`/auth/challenge?address=${address}&key=${page.key}`);
const psig = await lucid.wallet().signMessage(address, hex(plain.json.message));
const pp = await page.call('/auth/prove', { method: 'POST', body: { address, nonce: plain.json.nonce, ...psig } });
check('ordinary prove unchanged', pp.status === 200 && !!pp.json.session && !plain.json.message.includes('Device link'));

// 6. the page itself
const html = await fetch(`${url}/link/?c=${code}`);
check('page served with its CSP', html.status === 200 && (html.headers.get('content-security-policy') ?? '').includes("script-src 'self'"));

console.log(failed ? `${failed} FAILED` : 'all passed');
process.exit(failed ? 1 : 0);
