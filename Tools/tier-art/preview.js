// node: cat body.js kit.js sets.js preview.js | node - <out.png> [setIds,comma] [scale]
const zlib = require('zlib'), fs = require('fs');
function png(w, h, rgba) {
  const crcT = []; for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; crcT[n] = c >>> 0; }
  const crc = b => { let c = 0xffffffff; for (const x of b) c = crcT[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const chunk = (t, d) => { const len = Buffer.alloc(4); len.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([len, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  const raw = Buffer.alloc((w * 4 + 1) * h); for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; rgba.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4); }
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]);
}
const out = process.argv[2] || 'sheet.png';
const want = (process.argv[3] || '').split(',').filter(Boolean);
const S = +(process.argv[4] || 4);
const list = want.length ? SETS.filter(s => want.includes(s.id)) : SETS;
const CW = 80, CH = 164, OX = 21, OY = 36;   // cell in texels; body origin inside the cell
const cols = list.length + 1;
const W = cols * CW * S, H = CH * S;
const buf = Buffer.alloc(W * H * 4);
const bg = [0x4b, 0x4e, 0x57];
for (let i = 0; i < W * H; i++) { buf[i * 4] = bg[0]; buf[i * 4 + 1] = bg[1]; buf[i * 4 + 2] = bg[2]; buf[i * 4 + 3] = 255; }
function draw(grid, col) {
  for (const [k, c] of grid) {
    const x = KX(k) + OX + col * CW, y = KY(k) + OY; const [r, g, b] = hex2rgb(c);
    for (let dy = 0; dy < S; dy++) for (let dx = 0; dx < S; dx++) { const px = x * S + dx, py = y * S + dy; if (px < 0 || py < 0 || px >= W || py >= H) continue; const i = (py * W + px) * 4; buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; }
  }
}
draw(compose(Object.values(bodyLayers())), 0);
list.forEach((set, i) => draw(compose(wear(set)), i + 1));
fs.writeFileSync(out, png(W, H, buf));
console.log(out, W + 'x' + H, list.map(s => s.id).join(' '));
