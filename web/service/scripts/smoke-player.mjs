// End-to-end smoke test of the PLAYER path on a testnet, as the browser build would drive it:
//   a throwaway key -> /auth/challenge -> signData -> /auth/prove (session) -> a stand-in
//   character minted to it (operator) -> a run the service SEES (the run-start save, snapshots
//   floor by floor) -> commit-run (EXTRACTION mints the boxes to the wallet) -> open-boxes (the
//   server rolls, the wallet co-signs, boxes burn, designs mint).
//
//   BLOCKFROST_API_KEY=preview... node scripts/smoke-player.mjs [serviceUrl]
//
// SLOW ON PURPOSE: commit-run only mints what the service's own clock supports - at least
// MIN_SECONDS_PER_FLOOR (20 s on the service) a floor since the run started - so the minting run
// waits ~25 minutes for floor 75. Set MIN_SECONDS_PER_FLOOR here to the service's value if it differs.
//
// Testnet only. The operator step reads ./.write-token; nothing secret is printed.
import { readFileSync } from 'node:fs';
import { Blockfrost, Lucid, generatePrivateKey } from '@lucid-evolution/lucid';

const url = process.argv[2] ?? 'https://equivalence-service-production.up.railway.app';
const bfKey = process.env.BLOCKFROST_API_KEY;
if (!bfKey?.startsWith('preview') && !bfKey?.startsWith('preprod')) throw new Error('testnet BLOCKFROST_API_KEY required');
const network = bfKey.startsWith('preview') ? 'Preview' : 'Preprod';
const op = readFileSync(new URL('../.write-token', import.meta.url), 'utf8').trim();

const lucid = await Lucid(new Blockfrost(`https://cardano-${network.toLowerCase()}.blockfrost.io/api/v0`, bfKey), network);
lucid.selectWallet.fromPrivateKey(generatePrivateKey());
const address = await lucid.wallet().address();
const profileId = `smoke-${Date.now()}`;
const runId = `run-${Date.now()}`;
console.log('player', address.slice(0, 24) + '...', 'profile', profileId);

async function call(path, { method = 'GET', body, auth } = {}) {
  const r = await fetch(url + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(auth ? { Authorization: `Bearer ${auth}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await r.text();
  let json;
  try { json = JSON.parse(text); } catch { json = text; }
  return { status: r.status, json };
}
const step = (name, r) => console.log(`${name.padEnd(22)} ${r.status} ${JSON.stringify(r.json).slice(0, 260)}`);
const hex = (s) => Buffer.from(s, 'utf8').toString('hex');

// 1. log in
const ch = await call(`/auth/challenge?address=${address}`);
step('challenge', ch);
const sig = await lucid.wallet().signMessage(address, hex(ch.json.message));
const replay = { address, nonce: ch.json.nonce, ...sig };
const proved = await call('/auth/prove', { method: 'POST', body: replay });
step('prove', proved);
step('prove (replayed)', await call('/auth/prove', { method: 'POST', body: replay }));
const session = proved.json.session;

// 2. refusals before anything exists
step('open, no PFP', await call('/write/open-boxes', { method: 'POST', auth: session, body: { profileId, tier: 'Diamond' } }));
step('commit, unknown run', await call('/write/commit-run', { method: 'POST', auth: session,
  body: { profileId, datum: '{}', summary: JSON.stringify({ RunId: runId, Floors: 100, SecuredDiamondBoxes: 1 }) } }));

// 3. a stand-in character (its PFP) for this wallet - operator route
const charMint = await call('/op/dev/mint-character', { method: 'POST', auth: op, body: { address, profileId, name: 'Smoke Test' } });
step('mint character', charMint);
if (charMint.status !== 200) process.exit(1);
console.log('waiting for the character to confirm...');
await lucid.awaitTx(charMint.json.hash);
// Blockfrost's per-address view lags confirmation by a few seconds - the PFP gate reads it.
await new Promise((r) => setTimeout(r, 20_000));

// 4. runs the service SEES: the character saved under this wallet, a run-start save (the service's
//    clock starts), begin-run, then a snapshot entering and clearing every floor to 75 - three
//    avatars cleared, so the cap is 3 of each tier.
const minPerFloor = Number(process.env.MIN_SECONDS_PER_FLOOR ?? 20);
const datum = (runs) => JSON.stringify({ ProfileId: profileId, TotalRuns: runs });
step('save character', await call(`/saves/characters/${profileId}`, { method: 'PUT', auth: session, body: { reason: 'create', datum: datum(0) } }));
async function seenRun(id, n, floors) {
  await call(`/saves/characters/${profileId}`, { method: 'PUT', auth: session, body: { reason: 'run-start', runId: id, datum: datum(n) } });
  step(`begin-run ${id}`, await call('/write/begin-run', { method: 'POST', auth: session, body: { profileId, runId: id } }));
  let seq = 0;
  for (let f = 1; f <= floors; f++)
    for (const Stage of [0, 1])
      await call(`/saves/runs/${profileId}`, { method: 'PUT', auth: session,
        body: { runId: id, session: 'smoke-session', seq: ++seq, snapshot: JSON.stringify({ Floor: f, Stage }) } });
}
const summaryOf = (id) => JSON.stringify({ RunId: id, Floors: 75, Survived: true, SecuredDiamondBoxes: 5, SecuredBlackDiamondBoxes: 3 });
const commitOf = (id) => call('/write/commit-run', { method: 'POST', auth: session, body: { profileId, summary: summaryOf(id) } });

//    (a) committed at once: faster than the clock allows, so everything is clamped and nothing mints
const fast = `${runId}-fast`;
await seenRun(fast, 1, 75);
step('commit-run (too fast)', await commitOf(fast));

//    (b) committed once the clock supports floor 75. Every box a run holds is the SERVICE's
//        decision (avatarBoxes.ts) - a real avatar pays a Diamond 2% of the time - so the three
//        avatars' decisions are set by hand (a testnet-only operator route): 3 of each tier, and
//        the 5 Diamond claimed are clamped to 3.
await seenRun(runId, 2, 75);
for (const floor of [25, 50, 75])
  step(`decide avatar ${floor}`, await call('/op/dev/avatar-box', { method: 'POST', auth: op,
    body: { owner: proved.json.address, profileId, runId, floor, diamond: 1, blackDiamond: 1 } }));
const wait = Math.ceil(76 * minPerFloor);
console.log(`waiting ${wait} s for the service's clock to support floor 75...`);
await new Promise((r) => setTimeout(r, wait * 1000));
const commit = await commitOf(runId);
step('commit-run (mint)', commit);
step('commit-run (again)', await commitOf(runId));
console.log('waiting for the boxes to land...');
await lucid.awaitTx(commit.json.hash);
await new Promise((r) => setTimeout(r, 20_000));
step('boxes in wallet', await call(`/boxes/${profileId}`, { auth: session }));

// 5. open 2 Diamond boxes - first DECLINE (build, never sign), then open AGAIN: the second must
//    show the same designs (else cancelling is a free reroll), and the first must be dead
const declined = await call('/write/open-boxes', { method: 'POST', auth: session, body: { profileId, tier: 'Diamond', count: 2 } });
step('open (declined)', declined);
const again = await call('/write/open-boxes', { method: 'POST', auth: session, body: { profileId, tier: 'Diamond', count: 2 } });
step('open (again)', again);
const designsSeen = declined.json.pieces?.map((p) => p.design).join(',');
console.log('  same designs after declining:', again.json.pieces?.map((p) => p.design).join(',') === designsSeen);
const oldWitness = await lucid.fromTx(declined.json.cbor).partialSign.withWallet();
step('submit superseded open', await call('/write/open-boxes/submit', { method: 'POST', auth: session, body: { openId: declined.json.openId, witness: oldWitness } }));
const witness = await lucid.fromTx(again.json.cbor).partialSign.withWallet();
const opened = await call('/write/open-boxes/submit', { method: 'POST', auth: session, body: { openId: again.json.openId, witness } });
step('open submit', opened);
console.log('  revealed what was shown:', opened.json.pieces?.map((p) => p.design).join(',') === designsSeen);
step('replay submit', await call('/write/open-boxes/submit', { method: 'POST', auth: session, body: { openId: again.json.openId, witness } }));

// 6. a Black Diamond box: a weapon comes with its relic
await lucid.awaitTx(opened.json.hash);
await new Promise((r) => setTimeout(r, 20_000));
const bd = await call('/write/open-boxes', { method: 'POST', auth: session, body: { profileId, tier: 'BlackDiamond', count: 1 } });
step('open Black Diamond', bd);
if (bd.status === 200) {
  const w = await lucid.fromTx(bd.json.cbor).partialSign.withWallet();
  const done = await call('/write/open-boxes/submit', { method: 'POST', auth: session, body: { openId: bd.json.openId, witness: w } });
  step('BD submit', done);
  for (const p of done.json.pieces ?? []) console.log(`  ${p.slot.padEnd(8)} ${p.design.padEnd(22)} ${p.unit.slice(0, 20)}...`);
  await lucid.awaitTx(done.json.hash);
  await new Promise((r) => setTimeout(r, 20_000));
}
// Blockfrost's per-address view lags the last burn - give it longer before reading the balance.
await new Promise((r) => setTimeout(r, 30_000));
step('boxes after', await call(`/boxes/${profileId}`, { auth: session }));
// 3 Diamond minted, 2 opened: asking for 3 must be refused (402), and nothing built.
step('open, too many', await call('/write/open-boxes', { method: 'POST', auth: session, body: { profileId, tier: 'Diamond', count: 3 } }));
// No targeted open for design boxes - a slot is refused outright (400).
step('open, targeted', await call('/write/open-boxes', { method: 'POST', auth: session, body: { profileId, tier: 'Diamond', count: 1, slot: 'Weapon' } }));
