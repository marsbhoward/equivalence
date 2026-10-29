// Pause or reopen one feature on the live service (src/switches.ts) - no redeploy, no downtime.
//
//   node scripts/switch.mjs                         what is paused now
//   node scripts/switch.mjs minting off "message"   pause minting, with the message players see
//   node scripts/switch.mjs minting on              reopen it (deferred extractions mint now)
//
// Switches: runs (no new runs), minting (nothing to the chain; extraction defers), saves (read-only).
// Operator only - WRITE_TOKEN from ./.write-token. SERVICE_URL overrides the Railway URL.
import { readFileSync } from 'node:fs';

const url = process.env.SERVICE_URL ?? 'https://equivalence-service-production.up.railway.app';
const [name, state, ...words] = process.argv.slice(2);

if (!name) {
  const r = await fetch(`${url}/status`);
  console.log(JSON.stringify(await r.json(), null, 2));
  process.exit(0);
}
if (state !== 'on' && state !== 'off') {
  console.error('usage: node scripts/switch.mjs <runs|minting|saves> <on|off> [message]');
  process.exit(1);
}
const token = readFileSync(new URL('../.write-token', import.meta.url), 'utf8').trim();
const r = await fetch(`${url}/op/switches`, {
  method: 'POST',
  headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
  body: JSON.stringify({ name, paused: state === 'off', message: words.join(' ') }),
});
console.log(r.status, await r.text());
process.exit(r.ok ? 0 : 1);
