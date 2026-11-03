// What the showcase's players did (src/routes/telemetry.ts) - the operator's summary.
//
//   node scripts/telemetry.mjs            the last 7 days, on the showcase
//   node scripts/telemetry.mjs 30         the last 30
//
// Operator only - WRITE_TOKEN from .env.showcase (or ./.write-token with SERVICE_URL set).
import { existsSync, readFileSync } from 'node:fs';

const env = new URL('../.env.showcase', import.meta.url);
const vars = existsSync(env)
  ? Object.fromEntries(readFileSync(env, 'utf8').split('\n').map(l => l.match(/^([A-Z_]+)=(.*)$/)).filter(Boolean).map(m => [m[1], m[2].trim()]))
  : {};
const url = process.env.SERVICE_URL ?? vars.SERVICE_URL ?? 'https://equivalence-showcase-production.up.railway.app';
const token = process.env.WRITE_TOKEN ?? vars.WRITE_TOKEN ?? readFileSync(new URL('../.write-token', import.meta.url), 'utf8').trim();
const days = Number(process.argv[2] ?? 7) || 7;

const r = await fetch(`${url}/op/telemetry?days=${days}`, { headers: { authorization: `Bearer ${token}` } });
if (!r.ok) { console.error(r.status, await r.text()); process.exit(1); }
const s = await r.json();
console.log(`last ${s.days} days - ${s.runs.runs} runs by ${s.runs.characters} characters (${s.runs.new_characters} new)`);
for (const [name, rows] of Object.entries(s)) {
  if (!Array.isArray(rows) || rows.length === 0) continue;
  console.log(`\n${name}`);
  console.table(rows);
}
console.log('\ntiming bar', s.timing);
