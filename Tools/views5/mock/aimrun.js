// The same fight three ways: run at a target, circle it, back off, circle back - aim held on it.
const { frame, gif, put } = require('./aimcmp.js');
const SPEED = 416, FPS = 50, CYC = 1.56;            // texels a second (3.7 world units), stride cycles a second
const DEPTH = 0.45, R1 = 85, R2 = 175;
// the path as segments: [kind, ...]; positions in ground texels, z toward the camera
const segs = [
  { len: 90, at: u => [0, 175 - 90 * u] },                                         // toward the target
  { len: Math.PI * R1, at: u => [R1 * Math.sin(Math.PI * u), R1 * Math.cos(Math.PI * u)] },   // round it, the near side first
  { len: 90, at: u => [0, -R1 - 90 * u] },                                         // straight away
  { len: Math.PI * R2, at: u => [-R2 * Math.sin(Math.PI * u), -R2 * Math.cos(Math.PI * u)] }, // round it wide, back to the start
];
const total = segs.reduce((s, g) => s + g.len, 0), frames = Math.round(total / SPEED * FPS);
const pos = d => { for (const g of segs) { if (d <= g.len) return g.at(d / g.len); d -= g.len; } return segs[0].at(0); };
const MODES = [['travel', 'Faces travel, turns to aim to swing'], ['aim', 'Always faces the aim'], ['twist', 'Legs travel, torso aims']];
const PW = 420, PH = 400, OX = PW / 2, OY = 235, ENL = 2;
const W = PW * ENL, H = PH * ENL, outs = MODES.map(() => []), out = outs[0];
for (let fi = 0; fi < frames; fi++) {
  const d = fi / frames * total, [x, z] = pos(d), [x2, z2] = pos((d + 2) % total);
  const travel = Math.atan2(z2 - z, x2 - x) * 180 / Math.PI, aim = Math.atan2(-z, -x) * 180 / Math.PI;
  const swinging = [[38, 56], [92, 110]].some(([a, b]) => fi >= a && fi < b);
  MODES.forEach(([mode], m) => {
    const img = new Array(W * H).fill('#4a4e57'); outs[m].push(img);
    const ox = 0;
    // the floor: the path, faintly
    for (let k = 0; k < 400; k++) { const [px, pz] = pos(k / 400 * total); put(img, W, ENL, ox + Math.round(OX + px), Math.round(OY + pz * DEPTH), '#454950'); }
    const target = () => { for (let yy = -26; yy <= 0; yy++) for (let xx = -7; xx <= 7; xx++) { const edge = Math.abs(xx) === 7 || yy === -26; if (Math.abs(xx) <= 7 - Math.max(0, -yy - 20)) put(img, W, ENL, ox + OX + xx, OY + yy, edge ? '#14121C' : '#7a2a2a'); } };
    const shadow = () => { for (let yy = -3; yy <= 3; yy++) for (let xx = -16; xx <= 16; xx++) if ((xx / 16) ** 2 + (yy / 3.5) ** 2 <= 1) put(img, W, ENL, ox + Math.round(OX + x + xx), Math.round(OY + z * DEPTH + yy), '#3f434b'); };
    const fig = frame(travel, 'run', (fi / FPS * CYC) % 1, { aimDeg: aim, mode: mode === 'travel' && swinging ? 'aim' : mode });
    // the aim: a faint line from the figure toward the target
    { const sx = OX + x, sy = OY + z * DEPTH - 2, tx = OX, ty = OY - 2, n = Math.hypot(tx - sx, ty - sy); for (let k = 26; k < n - 10; k += 3) put(img, W, ENL, ox + Math.round(sx + (tx - sx) * k / n), Math.round(sy + (ty - sy) * k / n), '#6b707a'); }
    const slash = () => { if (!swinging) return; const a0 = Math.atan2(-z, -x); for (let a = -0.9; a <= 0.9; a += 0.02) for (const rr of [40, 41, 42]) { const ax = Math.cos(a0 + a) * rr, az = Math.sin(a0 + a) * rr; put(img, W, ENL, ox + Math.round(OX + x + ax), Math.round(OY + z * DEPTH + az * DEPTH - 40), rr === 41 ? '#ffffff' : '#c9d6ff'); } };
    const draw = () => { shadow(); for (const [k, v] of fig) put(img, W, ENL, ox + Math.round(OX + x) + (k % 1000 - 200) - 50, Math.round(OY + z * DEPTH) + (Math.floor(k / 1000) - 200) - 162, v); };
    if (z < 0) { draw(); target(); } else { target(); draw(); }
    slash();
  });
}
MODES.forEach(([mode], m) => gif(outs[m], W, H, 2, 'aim-' + mode + '.gif'));

{
  const zlib = require('zlib'), fs = require('fs');
  const pick = [30, 46, 66, 100], Hs = H * pick.length, W3 = W * 3, rgb = Buffer.alloc(W3 * Hs * 3);
  pick.forEach((fi, r) => outs.forEach((o, m) => o[fi].forEach((c, i) => { const xx = i % W, yy = Math.floor(i / W); const j = ((r * H + yy) * W3 + m * W + xx) * 3; rgb[j] = parseInt(c.substr(1, 2), 16); rgb[j + 1] = parseInt(c.substr(3, 2), 16); rgb[j + 2] = parseInt(c.substr(5, 2), 16); })));
  const T = []; for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; T[n] = c >>> 0; }
  const crc = b => { let c = 0xffffffff; for (const x of b) c = T[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const ch = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ih = Buffer.alloc(13); ih.writeUInt32BE(W3, 0); ih.writeUInt32BE(Hs, 4); ih[8] = 8; ih[9] = 2;
  const raw = Buffer.alloc((W3 * 3 + 1) * Hs); for (let y = 0; y < Hs; y++) rgb.copy(raw, y * (W3 * 3 + 1) + 1, y * W3 * 3, (y + 1) * W3 * 3);
  fs.writeFileSync('aim-stills.png', Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), ch('IHDR', ih), ch('IDAT', zlib.deflateSync(raw)), ch('IEND', Buffer.alloc(0))]));
}
