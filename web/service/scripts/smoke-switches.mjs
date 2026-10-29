// The pause switches and the client-build gate (src/switches.ts) against a running service with a
// database. Operator token from WRITE_TOKEN or ./.write-token - point it at a LOCAL service; it
// pauses and reopens features.
//   BLOCKFROST_API_KEY=preview... WRITE_TOKEN=... node scripts/smoke-switches.mjs http://localhost:8787
import { readFileSync } from 'node:fs';
import { Blockfrost, Lucid, generatePrivateKey } from '@lucid-evolution/lucid';
import { sessionClient } from './session-client.mjs';

const url = process.argv[2] ?? 'http://localhost:8787';
if (url.includes('railway.app') && !process.argv.includes('--live')) throw new Error('this pauses features - pass --live to run it against the live service');
const op = process.env.WRITE_TOKEN ?? readFileSync(new URL('../.write-token', import.meta.url), 'utf8').trim();
const bfKey = process.env.BLOCKFROST_API_KEY;
const lucid = await Lucid(new Blockfrost(`https://cardano-preview.blockfrost.io/api/v0`, bfKey), 'Preview');
lucid.selectWallet.fromPrivateKey(generatePrivateKey());
const address = await lucid.wallet().address();

let failed = 0;
const expect = (name, r, status, code) => {
  const ok = r.status === status && (!code || r.json?.code === code);
  if (!ok) failed++;
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${name.padEnd(36)} ${r.status} ${JSON.stringify(r.json).slice(0, 130)}`);
};
const c = await sessionClient(url);
const flip = (name, paused, message = '') => c.call('/op/switches', { method: 'POST', body: { name, paused, message },
  headers: { authorization: `Bearer ${op}` } });

expect('status, nothing paused', await c.call('/status'), 200);
expect('an outdated build', await c.call('/saves/characters', { headers: { 'X-Client-Build': '0' } }), 426, 'client_outdated');
expect('switch without the operator token', await c.call('/op/switches', { method: 'POST', body: { name: 'runs', paused: true } }), 401);
expect('sign in', await c.login(lucid, address), 200);

const profileId = `sw-${Date.now()}`;
const save = (reason, runId) => c.call(`/saves/characters/${profileId}`, { method: 'PUT', auth: c.session,
  body: { reason, ...(runId ? { runId, format: 'short' } : {}), datum: JSON.stringify({ ProfileId: profileId, TotalRuns: 0 }) } });

expect('a save', await save('unlock'), 200);
expect('pause runs', await flip('runs', true, 'Back in five minutes.'), 200);
const st = await c.call('/status');
expect('status names it', st, 200);
console.log('     paused:', JSON.stringify(st.json.paused));
expect('run-start while runs paused', await save('run-start', `r-${Date.now()}`), 503, 'paused');
expect('begin-run while runs paused', await c.call('/write/begin-run', { method: 'POST', auth: c.session,
  body: { profileId, runId: 'x' } }), 503, 'paused');
expect('other saves still land', await save('unlock'), 200);
expect('reopen runs', await flip('runs', false), 200);
expect('run-start once reopened', await save('run-start', `r-${Date.now()}`), 200);

expect('pause saves', await flip('saves', true), 200);
expect('a save while read-only', await save('unlock'), 503, 'paused');
expect('a read while read-only', await c.call('/saves/characters', { auth: c.session }), 200);
expect('reopen saves', await flip('saves', false), 200);

expect('pause minting', await flip('minting', true), 200);
expect('open boxes while minting paused', await c.call('/write/open-boxes', { method: 'POST', auth: c.session,
  body: { profileId, tier: 'Diamond' } }), 503, 'paused');
expect('reopen minting', await flip('minting', false), 200);
expect('status, all open again', await c.call('/status'), 200);

console.log(failed ? `\n${failed} FAILED` : '\nall passed');
process.exit(failed ? 1 : 0);
