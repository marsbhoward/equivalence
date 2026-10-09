// Pins rendered art under its art key: POST /op/art with the two PNGs that
// EditorTools.NftArt.Render wrote (<key>.png at 3200x3200, <key>.1x.png native).
//
//   node scripts/pin-art.mjs <artKey> <dir> [serviceUrl]
//
// The write token is read from ./.write-token (gitignored) and never printed.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const [artKey, dir, url = 'https://equivalence-service-production.up.railway.app'] = process.argv.slice(2);
if (!artKey || !dir) {
  console.error('usage: node scripts/pin-art.mjs <artKey> <dir> [serviceUrl]');
  process.exit(1);
}

const file = artKey.replace('@', '_');
const token = readFileSync(new URL('../.write-token', import.meta.url), 'utf8').trim();
const body = {
  artKey,
  image: readFileSync(join(dir, `${file}.png`)).toString('base64'),
  native: readFileSync(join(dir, `${file}.1x.png`)).toString('base64'),
};

// Blockfrost answers 425 "Pin Queue Full" when too many pins are waiting - a throttle, not a
// refusal. Back off and try again; adding the same bytes again is harmless (same CID).
for (let attempt = 1; ; attempt++) {
  const res = await fetch(`${url}/op/art`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  if (res.status === 502 && text.includes('(425)') && attempt < 8) {
    const wait = 30 * attempt;
    console.error(`pin queue full - retrying ${artKey} in ${wait}s`);
    await new Promise((r) => setTimeout(r, wait * 1000));
    continue;
  }
  console.log(res.status, text);
  process.exit(res.ok ? 0 : 1);
}
