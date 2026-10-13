#!/usr/bin/env node
/**
 * Puts a Unity WebGL build where the service serves /play (src/routes/playUpload.ts).
 *
 *   node scripts/upload-play.mjs <build folder> [service url]
 *
 * The folder is Unity's WebGL output (index.html, Build/, TemplateData/, connector/...). Each file
 * goes up on its own, then one call swaps the staged build in - players get the old build or the
 * new one, never half of each. The previous build is kept: POST /op/play/rollback brings it back.
 *
 * OPERATOR only: WRITE_TOKEN comes from the environment or web/service/.write-token, never from
 * anything that ships to a browser.
 */
import { readFile, readdir, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const [, , folder, urlArg] = process.argv;
if (!folder) {
  console.error('usage: node scripts/upload-play.mjs <build folder> [service url]');
  process.exit(1);
}
const service = (urlArg || process.env.EQUIVALENCE_SERVICE || 'https://equivalence-service-production.up.railway.app')
  .replace(/\/$/, '');
const token = process.env.WRITE_TOKEN
  || (await readFile(path.join(here, '..', '.write-token'), 'utf8').catch(() => '')).trim();
if (!token) {
  console.error('no WRITE_TOKEN (environment or web/service/.write-token)');
  process.exit(1);
}

async function walk(dir, base = dir) {
  const out = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...await walk(full, base));
    else if (entry.isFile() && !entry.name.startsWith('.')) out.push(path.relative(base, full).split(path.sep).join('/'));
  }
  return out;
}

const root = path.resolve(folder);
if (!(await stat(path.join(root, 'index.html')).catch(() => null))) {
  console.error(`${root} has no index.html - is it a WebGL build folder?`);
  process.exit(1);
}

const build = new Date().toISOString().replace(/[:.]/g, '-');
const files = await walk(root);
let total = 0;
for (const rel of files) {
  const body = await readFile(path.join(root, rel));
  const r = await fetch(`${service}/op/play/${build}/file?path=${encodeURIComponent(rel)}`, {
    method: 'PUT',
    headers: { authorization: `Bearer ${token}`, 'content-type': 'application/octet-stream' },
    body,
  });
  if (!r.ok) {
    console.error(`${rel}: ${r.status} ${await r.text()}`);
    process.exit(1);
  }
  total += body.length;
  console.log(`  ${rel}  ${(body.length / 1048576).toFixed(2)} MB`);
}

const done = await fetch(`${service}/op/play/${build}/activate`, {
  method: 'POST',
  headers: { authorization: `Bearer ${token}` },
});
if (!done.ok) {
  console.error(`activate: ${done.status} ${await done.text()}`);
  process.exit(1);
}
console.log(`build ${build} live at ${service}/play/ - ${files.length} files, ${(total / 1048576).toFixed(1)} MB`);
