// PHASE 4 MOCK (2026-11-11): how today's attack motions map onto the eight-direction body. Built on mock8.js's figure
// (its source, with the arms replaced by a two-bone reach to a held greatsword). Three mappings, one row each:
//   A  picture plane - today's rule: the authored 2D arc (facing right) turned on screen to the aim, centred when steep
//   B  body plane    - every arc swung in the body's own forward/up plane
//   C  per motion    - overhead cuts in the body plane; sweeps, spins and thrusts level round the body (the hit's shape)
// node attack8.js <outDir>    -> attack-<motion>.gif (rows A B C, columns the eight aims) + attack-stills.png
const fs = require('fs');
let src = fs.readFileSync(__dirname + '/mock8.js', 'utf8');
src = src.replace('const CW = 100, CH = 172, OX = 50, OY = 162', 'const CW = 176, CH = 250, OX = 88, OY = 214');
src = src.replace('function frame(dirDeg, gait, phase) {', 'function frame(dirDeg, gait, phase, atk) {');
const a0 = src.indexOf('  // ---- arms'), a1 = src.indexOf('  // ---- head');
src = src.slice(0, a0) + fs.readFileSync(__dirname + '/attack8.arms.js', 'utf8') + src.slice(a1);
src = src.slice(0, src.indexOf('const outDir'));
src += '\nmodule.exports = { frame, GAITS, M, V, add, mul, sub, len, UP, P, CW, CH, OX, OY, gif, put };\n';
const m = { exports: {} };
new Function('require', 'module', 'exports', '__dirname', src)(require, m, m.exports, __dirname);
const { frame, GAITS, V, add, mul, sub, len, UP, CW, CH, gif, put } = m.exports;

GAITS.stand = { frames: 1, foot: [[0, 0.1, 0], [0.5, -0.1, 0]], pitch: [[0, 0]], bob: () => 0, base: 58, lean: 0, arm: 0, elbow: 0, trail: 0, hipDrop: 0 };

// ---- the motions, authored as today's are: facing right, the blade's angle in the picture (0 forward, 90 up),
// how far the grip is from the pivot, over the swing's time (0 cock, ~0.3 strike starts, ~0.6 lands, then held)
const keys = (ks, t) => { for (let i = 0; i < ks.length - 1; i++) if (t <= ks[i + 1][0]) { const u = (t - ks[i][0]) / (ks[i + 1][0] - ks[i][0]), s = u * u * (3 - 2 * u); return ks[i][1] + (ks[i + 1][1] - ks[i][1]) * s; } return ks[ks.length - 1][1]; };
const MOTIONS = {
  chop: { kind: 'overhead', theta: [[0, 40], [0.3, 118], [0.6, -55], [1, -55]], reach: [[0, 13], [1, 15]] },
  sweep: { kind: 'level', theta: [[0, 60], [0.3, 140], [0.62, -75], [1, -75]], reach: [[0, 15], [1, 17]] },
  thrust: { kind: 'level', theta: [[0, 0], [1, 0]], reach: [[0, 8], [0.3, 0], [0.5, 24], [1, 24]] },
  spin: { kind: 'level', spin: true, theta: [[0, 60], [0.2, 120], [0.85, -240], [1, -240]], reach: [[0, 16], [1, 16]] },
};
const range = mo => { const ts = mo.theta.filter(k => k[0] >= 0.3 && k[0] <= 0.62).map(k => k[1]); return ts.length ? (Math.max(...ts) + Math.min(...ts)) / 2 : 0; };

const rad = d => d * Math.PI / 180;
// where the grip is and which way the blade points, for one mapping, aim (mock8 degrees: 0 E, 90 S toward the camera)
function swing(mode, mo, aimDeg, t) {
  if (mode === 'D') return (f, r, up) => {
    // A's picture on screen, C's depth: the blade lies where the screen arc puts it, at the depth the 3D swing has
    const a = swing('A', mo, aimDeg, t)(f, r, up), c = swing('C', mo, aimDeg, t)(f, r, up);
    const L = 64, zc = c.dir.z * L, zg = c.grip.z;
    const tip = V(a.grip.x + a.dir.x * L, a.grip.y + a.dir.y * L + 0.45 * (zc + zg - a.grip.z) , zg + zc);
    const grip = V(a.grip.x, a.grip.y + 0.45 * (zg - a.grip.z), zg);
    const d = sub(tip, grip), l = len(d);
    return { grip, dir: mul(d, 1 / l), len: l };
  };
  return (f, r, up) => {
    let th = keys(mo.theta, t); const reach = keys(mo.reach, t);
    const shoulder = add(up(37), mul(r, 15)), chest = add(add(up(28), mul(r, 4)), mul(f, 3));
    if (mode === 'A') {
      // the picture: forward is the aim on screen, "up" the side of it nearer the screen's up
      const ax = Math.cos(rad(aimDeg)), ay = -Math.sin(rad(aimDeg));          // screen, y UP
      const steep = Math.abs(ay) > Math.sin(rad(50));
      if (steep && !mo.spin && !(mo.kind === 'overhead' && ay < 0)) th -= range(mo);   // centred (DecideStaging)
      const left = ax < -1e-3 || (Math.abs(ax) <= 1e-3 && false);
      const ux = left ? ay : -ay, uy = left ? -ax : ax;                       // perpendicular toward screen-up
      const sx = Math.cos(rad(th)) * ax + Math.sin(rad(th)) * ux, sy = Math.cos(rad(th)) * ay + Math.sin(rad(th)) * uy;
      const dir = V(sx, sy, 0);                                                // in the picture: no depth
      return { grip: add(shoulder, mul(dir, reach)), dir };
    }
    const vertical = mode === 'B' || mo.kind === 'overhead';
    if (vertical) { const dir = add(mul(f, Math.cos(rad(th))), mul(UP, Math.sin(rad(th)))); return { grip: add(shoulder, mul(dir, reach)), dir }; }
    let dir = add(add(mul(f, Math.cos(rad(th))), mul(r, Math.sin(rad(th)))), mul(UP, -0.12));
    dir = mul(dir, 1 / len(dir));
    return { grip: add(chest, mul(dir, reach)), dir };
  };
}

const outDir = process.argv[2] || '.';
const AIMS = [90, 45, 0, 315, 270, 225, 180, 135];      // down, down-right, right, up-right, up, up-left, left, down-left
const MODES = ['A', 'B', 'C', 'D'], FRAMES = 22, BG = '#4a4e57', SHADOW = '#3f434b', TRAIL = '#e8e4d8', HIT = '#707684';
const OXC = 88, OYC = 214;
const stills = [];
for (const [name, mo] of Object.entries(MOTIONS)) {
  const W = CW * AIMS.length, H = CH * MODES.length, frames = [];
  const tips = MODES.map(() => AIMS.map(() => []));
  for (let fi = 0; fi < FRAMES; fi++) {
    const t = fi / (FRAMES - 1), img = new Array(W * H).fill(BG);
    MODES.forEach((mode, ri) => AIMS.forEach((aim, ci) => {
      const cx = ci * CW, cy = ri * CH;
      // the hit's shape (gameplay is flat on the screen): a faint arc round the chest toward the aim
      const width = name === 'thrust' ? 25 : name === 'chop' ? 55 : name === 'spin' ? 180 : 85, R = 70;
      for (let a = -width; a <= width; a += 1.5) { const g = rad(aim + a); put(img, W, 1, cx + OXC + Math.round(Math.cos(g) * R), cy + OYC - 92 + Math.round(Math.sin(g) * R), HIT); }
      for (let y = -3; y <= 3; y++) for (let x = -18; x <= 18; x++) if ((x / 18) ** 2 + (y / 4) ** 2 <= 1) put(img, W, 1, cx + OXC + x, cy + OYC + y, SHADOW);
      const out = frame(aim, 'stand', 0, swing(mode, mo, aim, t));
      // the tip's trail over the last frames, under the figure
      const tipNow = globalThis.__tip; if (tipNow) tips[ri][ci].push(tipNow);
      const tr = tips[ri][ci].slice(-6);
      for (let i = 1; i < tr.length; i++) { const p0 = tr[i - 1], p1 = tr[i], n = Math.ceil(Math.hypot(p1.x - p0.x, p1.y - p0.y)) || 1;
        for (let k = 0; k <= n; k++) { const x = Math.round(p0.x + (p1.x - p0.x) * k / n), y = Math.round(p0.y + (p1.y - p0.y) * k / n); for (const [dx, dy] of [[0, 0], [1, 0], [0, 1]]) put(img, W, 1, cx + x + dx, cy + y + dy, TRAIL); } }
      for (const [k, v] of out) put(img, W, 1, cx + k % 1000 - 200, cy + Math.floor(k / 1000) - 200, v);
    }));
    frames.push(img);
    if ([4, 8, 11, 14].includes(fi)) {
      const io = require('/private/tmp/claude-501/-Users-marshoward-Coalescence/608fafe2-eefa-4ba2-97ff-090f4e6992e1/scratchpad/pngio.js');
      const b = Buffer.alloc(W * H * 3); img.forEach((c, i) => { b[i * 3] = parseInt(c.substr(1, 2), 16); b[i * 3 + 1] = parseInt(c.substr(3, 2), 16); b[i * 3 + 2] = parseInt(c.substr(5, 2), 16); });
      io.write(`${outDir}/attack-${name}-f${fi}.png`, W, H, b);
    }
  }
  // hold the end a beat
  for (let i = 0; i < 6; i++) frames.push(frames[frames.length - 1]);
  gif(frames, W, H, 5, `${outDir}/attack-${name}.gif`);
  console.log(name, W + 'x' + H);
}
