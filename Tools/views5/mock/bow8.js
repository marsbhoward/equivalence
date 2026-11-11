// BOW MOCK (2026-11-11): how the bow is held out of combat and in combat on the eight-direction body. Built on mock8.js's
// figure (its arms replaced by bow8.arms.js). The bow in the LEFT hand, the string hand the right, the quiver on the
// back with its mouth over the left shoulder - as the game has them. Rows, walking in the eight directions:
//   O1 in the hand, lowered at the side        O2 slung across the back, the string across the chest
//   O3 on the back beside the quiver           C1 low ready: nocked, angled down ahead
//   C2 high ready: raised before the chest, half drawn at the aim      C3 canted across the body, half drawn
// node bow8.js <outDir>  -> bow-walk.gif, bow-stand.png
const fs = require('fs');
let src = fs.readFileSync(__dirname + '/mock8.js', 'utf8');
src = src.replace('const CW = 100, CH = 172, OX = 50, OY = 162', 'const CW = 176, CH = 240, OX = 88, OY = 214');
src = src.replace('function frame(dirDeg, gait, phase) {', 'function frame(dirDeg, gait, phase, atk) {');
const a0 = src.indexOf('  // ---- arms'), a1 = src.indexOf('  // ---- head');
src = src.slice(0, a0) + fs.readFileSync(__dirname + '/bow8.arms.js', 'utf8') + src.slice(a1);
src = src.slice(0, src.indexOf('const outDir'));
src += '\nmodule.exports = { frame, GAITS, V, add, mul, sub, len, UP, CW, CH, gif, put };\n';
const m = { exports: {} };
new Function('require', 'module', 'exports', '__dirname', src)(require, m, m.exports, __dirname);
const { frame, GAITS, V, add, mul, sub, len, UP, CW, CH, gif, put } = m.exports;
GAITS.stand = { frames: 1, foot: [[0, 0.1, 0], [0.5, -0.1, 0]], pitch: [[0, 0]], bob: () => 0, base: 58, lean: 0, arm: 0, elbow: 0, trail: 0, hipDrop: 0 };

const norm = v => { const l = len(v) || 1; return mul(v, 1 / l); };
const dot = (p, q) => p.x * q.x + p.y * q.y + p.z * q.z;
const perp = (B, A) => norm(sub(B, mul(A, dot(B, A))));
const drawn = (grip, A, B, draw) => { const pull = add(grip, mul(B, 13 + draw)), dir = norm(sub(grip, pull)); return { pull, arrow: { from: pull, to: add(grip, mul(dir, 14)) } }; };
const OPTIONS = {
  O1: (f, r, up) => { const L = mul(r, -1), grip = add(add(up(4), mul(L, 17)), mul(f, 3)), A = norm(add(UP, mul(f, 0.5))), B = perp(mul(f, -1), A);
    return { hand: { '-1': grip }, bow: { grip, A, B } }; },
  O2: (f, r, up) => { const L = mul(r, -1), grip = add(up(16), mul(f, -13)), A = norm(add(UP, mul(r, 0.75))), B = perp(f, A);
    return { hand: {}, bow: { grip, A, B, slung: [add(add(up(36), mul(r, 9)), mul(f, 9)), add(add(up(0), mul(L, 13)), mul(f, 10))] } }; },
  O3: (f, r, up) => { const grip = add(add(up(14), mul(f, -15)), mul(r, 4)), A = norm(add(UP, mul(r, 0.2))), B = perp(mul(f, -1), A);
    return { hand: {}, bow: { grip, A, B } }; },
  C1: (f, r, up) => { const L = mul(r, -1), grip = add(add(up(6), mul(f, 15)), mul(L, 4)), A = norm(add(mul(UP, 0.85), mul(f, 0.5))), B = perp(mul(f, -1), A), d = drawn(grip, A, B, 4);
    return { hand: { '-1': grip, 1: d.pull }, bow: { grip, A, B, pull: d.pull }, arrow: d.arrow }; },
  C2: (f, r, up) => { const L = mul(r, -1), grip = add(add(up(30), mul(f, 20)), mul(L, 2)), A = UP, B = perp(mul(f, -1), A), d = drawn(grip, A, B, 13);
    return { hand: { '-1': grip, 1: d.pull }, bow: { grip, A, B, pull: d.pull }, arrow: d.arrow }; },
  C3: (f, r, up) => { const grip = add(up(24), mul(f, 18)), A = norm(add(UP, mul(r, 0.9))), B = perp(mul(f, -1), A), d = drawn(grip, A, B, 12);
    return { hand: { '-1': grip, 1: d.pull }, bow: { grip, A, B, pull: d.pull }, arrow: d.arrow }; },
};

const outDir = process.argv[2] || '.';
const AIMS = [90, 45, 0, 315, 270, 225, 180, 135];      // down, down-right, right, up-right, up, up-left, left, down-left
const ROWS = Object.keys(OPTIONS), BG = '#4a4e57', SHADOW = '#3f434b';
const W = CW * AIMS.length, H = CH * ROWS.length;
const draw = (gait, ph) => {
  const img = new Array(W * H).fill(BG);
  ROWS.forEach((k, ri) => AIMS.forEach((aim, ci) => {
    const cx = ci * CW, cy = ri * CH;
    for (let y = -3; y <= 3; y++) for (let x = -18; x <= 18; x++) if ((x / 18) ** 2 + (y / 4) ** 2 <= 1) put(img, W, 1, cx + 88 + x, cy + 214 + y, SHADOW);
    for (const [kk, v] of frame(aim, gait, ph, OPTIONS[k])) put(img, W, 1, cx + kk % 1000 - 200, cy + Math.floor(kk / 1000) - 200, v);
  }));
  return img;
};
const frames = []; for (let i = 0; i < 24; i++) frames.push(draw('walk', i / 24));
gif(frames, W, H, 4, `${outDir}/bow-walk.gif`);
const io = require('/private/tmp/claude-501/-Users-marshoward-Coalescence/608fafe2-eefa-4ba2-97ff-090f4e6992e1/scratchpad/pngio.js');
const st = draw('stand', 0), b = Buffer.alloc(W * H * 3); st.forEach((c, i) => { for (let j = 0; j < 3; j++) b[i * 3 + j] = parseInt(c.substr(1 + j * 2, 2), 16); });
io.write(`${outDir}/bow-stand.png`, W, H, b);
console.log('bow', W + 'x' + H);
