// SWEEP MOCK (2026-11-11): alternatives for the greatsword's sweep on the eight-direction body, built on attack8.js
// (its figure, held sword, trail and hit arc). Rows, each aimed in the eight directions:
//   S1 today's, as built (option D: the game's on-screen arc at 3D depth; the windup lifts over the shoulder)
//   S2 level: wound back low at the right hip, flat round the front at waist-to-chest height, out to the left
//   S3 descending diagonal: from over the right shoulder down across the body to the left hip
//   S4 rising diagonal: from low on the right up across the body to high on the left
//   S5 level with a body turn: S2, the body wound right at the start and turned left at the end
// node sweep8.js <outDir>  -> sweep-alt.gif + sweep-alt-f<n>.png
const fs = require('fs');
let src = fs.readFileSync(__dirname + '/attack8.js', 'utf8');
src = src.slice(0, src.lastIndexOf('\nconst outDir'));
src += '\nreturn { frame, swing, MOTIONS, keys, rad, V, add, mul, sub, len, UP, CW, CH, gif, put };\n';
const A = new Function('require', '__dirname', src)(require, __dirname);
const { frame, swing, MOTIONS, keys, rad, add, mul, len, UP, CW, CH, gif, put } = A;
const norm = v => mul(v, 1 / (len(v) || 1));

// one sweep in a swing plane: e1 the facing, e2 the character's right tilted up by `tilt` degrees; the blade's angle
// in that plane (90 = out to the right, -90 = out to the left) over the swing's time
const PHI = [[0, 30], [0.3, 140], [0.58, -100], [1, -110]];
const plane = (tilt, drop = 0) => (aim, t) => (f, r, up) => {
  const e2 = norm(add(mul(r, Math.cos(rad(tilt))), mul(UP, Math.sin(rad(tilt)))));
  const ph = rad(keys(PHI, t));
  const dir = norm(add(add(mul(f, Math.cos(ph)), mul(e2, Math.sin(ph))), mul(UP, -drop)));
  const pivot = add(add(up(26), mul(r, 3)), mul(f, 4));
  return { grip: add(pivot, mul(dir, 14)), dir };
};
const ease = t => t * t * (3 - 2 * t);
const ROWS = {
  S1: { make: (aim, t) => swing('D', MOTIONS.sweep, aim, t) },
  S2: { make: plane(0, 0.12) },
  S3: { make: plane(40) },
  S4: { make: plane(-40) },
  S5: { make: plane(0, 0.12), twist: t => 40 - 80 * ease(Math.min(1, Math.max(0, (t - 0.2) / 0.45))) },
};

const outDir = process.argv[2] || '.';
const AIMS = [90, 45, 0, 315, 270, 225, 180, 135];      // down, down-right, right, up-right, up, up-left, left, down-left
const FRAMES = 22, BG = '#4a4e57', SHADOW = '#3f434b', TRAIL = '#e8e4d8', HIT = '#707684', OXC = 88, OYC = 214;
const names = Object.keys(ROWS), W = CW * AIMS.length, H = CH * names.length, frames = [];
const tips = names.map(() => AIMS.map(() => []));
const io = require('/private/tmp/claude-501/-Users-marshoward-Coalescence/608fafe2-eefa-4ba2-97ff-090f4e6992e1/scratchpad/pngio.js');
for (let fi = 0; fi < FRAMES; fi++) {
  const t = fi / (FRAMES - 1), img = new Array(W * H).fill(BG);
  names.forEach((nm, ri) => AIMS.forEach((aim, ci) => {
    const row = ROWS[nm], cx = ci * CW, cy = ri * CH;
    for (let a = -85; a <= 85; a += 1.5) { const g = rad(aim + a); put(img, W, 1, cx + OXC + Math.round(Math.cos(g) * 70), cy + OYC - 92 + Math.round(Math.sin(g) * 70), HIT); }
    for (let y = -3; y <= 3; y++) for (let x = -18; x <= 18; x++) if ((x / 18) ** 2 + (y / 4) ** 2 <= 1) put(img, W, 1, cx + OXC + x, cy + OYC + y, SHADOW);
    const facing = aim + (row.twist ? row.twist(t) : 0);
    const out = frame(facing, 'stand', 0, row.make(facing, t));
    const tipNow = globalThis.__tip; if (tipNow) tips[ri][ci].push(tipNow);
    const tr = tips[ri][ci].slice(-6);
    for (let i = 1; i < tr.length; i++) { const p0 = tr[i - 1], p1 = tr[i], n = Math.ceil(Math.hypot(p1.x - p0.x, p1.y - p0.y)) || 1;
      for (let k = 0; k <= n; k++) { const x = Math.round(p0.x + (p1.x - p0.x) * k / n), y = Math.round(p0.y + (p1.y - p0.y) * k / n); for (const [dx, dy] of [[0, 0], [1, 0], [0, 1]]) put(img, W, 1, cx + x + dx, cy + y + dy, TRAIL); } }
    for (const [k, v] of out) put(img, W, 1, cx + k % 1000 - 200, cy + Math.floor(k / 1000) - 200, v);
  }));
  frames.push(img);
  if ([5, 8, 10, 13].includes(fi)) {
    const b = Buffer.alloc(W * H * 3); img.forEach((c, i) => { for (let j = 0; j < 3; j++) b[i * 3 + j] = parseInt(c.substr(1 + j * 2, 2), 16); });
    io.write(`${outDir}/sweep-alt-f${fi}.png`, W, H, b);
  }
}
for (let i = 0; i < 6; i++) frames.push(frames[frames.length - 1]);
gif(frames, W, H, 5, `${outDir}/sweep-alt.gif`);
console.log('sweep', W + 'x' + H);
