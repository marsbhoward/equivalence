// node: cat body.js kit.js sets.js <views.js without its sheet> bake8.js | node - <out.cs>   (regen8.sh)
// EIGHT DIRECTIONS, PHASE 3 (RIG.md): bakes the approved five-view mock (views.js) into the game - every piece that
// rides the torso (a cuirass, a belt, a skirt, a cape, pauldrons, a pack) in each of the five views, through the
// arena's 3/4 camera (Locomotion8.DepthY: a point toward the camera sits lower on the screen), WITH A DEPTH PER
// TEXEL. The game stacks whatever is worn with the limbs it draws on the skeleton (Body8): the nearest texel wins
// and each piece is outlined as compose() does, so any mix of slots is the mock's own picture.
// A piece that is not the same on both sides (a pouch on one hip, a sash knotted at one side) is baked again as
// its mirror image: facing left the rig mirrors the figure, so that is the picture it shows (the user's rule:
// what is not symmetrical shows its own other side). Helms are baked whole (one slot), outlined, per view.
const fs = require('fs');
const out = process.argv[2];
const SHEAR = 0.45;                         // Locomotion8.DepthY
const YAWS = [0, 45, 90, 135, 180];         // front, front 3/4, side, back 3/4, back - all facing right
const Y0 = -24, Y1 = 176;                   // heights rendered (texels above the soles)

// which slot each mock piece is; pieces left out are drawn on the limbs (3b) or are the head's
const SLOT_OF = {
  hoplite: { helm: 'Head', crest: 'Head', cuirass: 'Torso', pauldronL: 'Shoulders', pauldronR: 'Shoulders', belt: 'Belt', skirt: 'Legs', cape: 'Back' },
  wayfarer: { helm: 'Head', flapL: 'Head', flapR: 'Head', pauldronL: 'Shoulders', pauldronR: 'Shoulders', torso: 'Torso', skirts: 'Torso',
              belt: 'Belt', pouch: 'Belt', back: 'Back', backPouch: 'Back' },
  athanor: { helm: 'Head', goggleL: 'Head', goggleR: 'Head', pauldronL: 'Shoulders', pauldronR: 'Shoulders', torso: 'Torso', bib: 'Torso',
             apron: 'Legs', apronBack: 'Legs', seat: 'Legs', belt: 'Belt', vial0: 'Belt', vial1: 'Belt', vial2: 'Belt', pouch: 'Belt',
             back: 'Back', sidePouchL: 'Back', sidePouchR: 'Back', flask: 'Back', straps: 'Back' },
};
const SETS8 = { hoplite: hoplite3d, wayfarer: wayfarer3d, athanor: athanor3d };

const mx = p => v3(-p.x, p.y, p.z);
const mirrored = pr => ({ ...pr, sdf: p => pr.sdf(mx(p)), color: (p, n, nb) => pr.color(mx(p), n, nb && mx(nb)) });

// one ray per texel through the sheared camera: screen (vx, vy) sees view-space points (vx, vy + vz * SHEAR, vz)
function render(prims, yawDeg, shear) {
  const a = yawDeg * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
  const toBody = (vx, vy, vz) => v3(vx * ca - vz * sa, vy, vx * sa + vz * ca);
  const toView = n => v3(n.x * ca + n.z * sa, n.y, -n.x * sa + n.z * ca);
  const hits = new Map();
  for (let row = 0; row < Y1 - Y0; row++) for (let col = -HALFW; col <= HALFW; col++) {
    const sy = Y1 - row - 0.5, vx = col + 0.5;
    let t = 0, hit = null;
    while (t < 130) {
      const vz = 65 - t, p = toBody(vx, sy + vz * shear, vz);
      let best = 1e9, bp = null;
      for (const pr of prims) { const d = pr.sdf(p); if (d < best) { best = d; bp = pr; } }
      if (best < 0.02) { hit = { prim: bp, p, vz }; break; }
      t += Math.max(0.2, best * 0.8);
    }
    if (!hit) continue;
    const e = 0.25, pr = hit.prim, p = hit.p;
    const n0 = v3(pr.sdf(v3(p.x + e, p.y, p.z)) - pr.sdf(v3(p.x - e, p.y, p.z)), pr.sdf(v3(p.x, p.y + e, p.z)) - pr.sdf(v3(p.x, p.y - e, p.z)), pr.sdf(v3(p.x, p.y, p.z + e)) - pr.sdf(v3(p.x, p.y, p.z - e)));
    const nl = len3(n0) || 1, nb = v3(n0.x / nl, n0.y / nl, n0.z / nl);
    hits.set(K(col, row), { piece: pr.piece, outR: pr.outR, z: hit.vz, p, color: pr.color(p, toView(nb), nb) });
  }
  return hits;
}

const CH = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&*+-/:;<=>?@^_~';
const SUIT = { line: 'k', deep: 's', dark: 'd', base: 'b', light: 'l', glow: 'h' };   // the bare suit: the game's undersuit ramp

// a piece's picture in one view: box (cols from the centre line, rows from Y1 down), colours, depth bytes
function view(hits, palette) {
  if (!hits.size) return null;
  let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
  for (const k of hits.keys()) { x0 = Math.min(x0, KX(k)); x1 = Math.max(x1, KX(k)); y0 = Math.min(y0, KY(k)); y1 = Math.max(y1, KY(k)); }
  const w = x1 - x0 + 1, h = y1 - y0 + 1;
  let cols = '', depth = Buffer.alloc(w * h, 255);
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
    const hh = hits.get(K(x, y));
    if (!hh) { cols += '.'; continue; }
    if (palette.suit) cols += hh.color;
    else { if (!palette.map.has(hh.color)) { if (palette.map.size >= CH.length) throw new Error('too many colours'); palette.map.set(hh.color, CH[palette.map.size]); } cols += palette.map.get(hh.color); }
    depth[(y - y0) * w + (x - x0)] = Math.max(0, Math.min(254, Math.round((hh.z + 64) * 2)));
  }
  return { x0, y0: y0, w, h, cols, depth: depth.toString('base64') };
}
const eq = (a, b) => (!a && !b) || (a && b && a.x0 === b.x0 && a.y0 === b.y0 && a.w === b.w && a.h === b.h && a.cols === b.cols);

// the hits one piece wins, rendered alone
const only = (hits, name) => new Map([...hits].filter(([, h]) => h.piece === name));

// a helm's group, outlined and stacked as compose() does, straight on (it sits on the 2D head art)
// the bare head goes in as an occluder (dropped after): what the helm leaves open shows the game's own face
function helmView(prims, yaw, palette) {
  POSE.hands = {}; CARDS = [];
  const head = body3d().filter(p => p.piece === 'head').map(p => ({ ...p, piece: '#head' }));
  const hits = render([...prims, ...head], yaw, 0), layers = new Map();
  for (const [k, h] of hits) { if (h.piece === '#head') continue; if (!layers.has(h.piece)) { const l = new Layer(0); l.outR = h.outR; l.zs = []; layers.set(h.piece, l); } const l = layers.get(h.piece); l.c.set(k, h.color); l.zs.push(h.z); }
  for (const l of layers.values()) l.depth = l.zs.reduce((s, z) => s + z, 0) / l.zs.length;
  const g = compose([...layers.values()]), m = new Map();
  for (const [k, c] of g) m.set(k, { color: c, z: 0 });
  const v = view(m, palette); delete v.depth; return v;
}

const f = n => (Math.round(n * 1000) / 1000) + 'f';
const lines = [];
const piece = (set, slot, name, outR, pal, views, mviews) => {
  const vs = list => 'new[] { ' + list.map(v => v ? `V(${v.x0}, ${v.y0}, ${v.w}, ${v.h}, "${v.cols}", "${v.depth || ''}")` : 'null').join(',\n                ') + ' }';
  lines.push(`            Add("${set}", GearSlot.${slot}, "${name}", ${outR}, "${pal}",\n                ${vs(views)}${mviews ? ',\n                ' + vs(mviews) : ', null'});`);
};
const palStr = palette => palette.suit ? '@suit' : [...palette.map].map(([c, ch]) => ch + c.slice(1).toUpperCase()).join(' ');

// ---- the bare suit's torso
{
  POSE.hands = {}; CARDS = [];
  const torso = body3d().filter(p => p.piece === 'torso').map(p => ({ ...p, color: (q, n) => tone(SUIT, litTone(n.x, -n.y, n.z)) }));
  const palette = { suit: true };
  piece('bare', 'Torso', 'torso', 2, '@suit', YAWS.map(y => view(render(torso, y, SHEAR), palette)), null);
  process.stderr.write('bare\n');
}
// ---- each set
for (const [id, make] of Object.entries(SETS8)) {
  POSE.hands = {}; CARDS = [];
  const prims = make(), slots = SLOT_OF[id];
  // the helm, whole
  const head = prims.filter(p => slots[p.piece] === 'Head');
  {
    const palette = { map: new Map() }, views = YAWS.map(y => helmView(head, y, palette)), mv = YAWS.map(y => helmView(head.map(mirrored), y, palette));
    const asym = YAWS.some((_, i) => !eq(views[i], mv[i]));
    piece(id, 'Head', 'helm', 0, palStr(palette), views, asym ? mv : null);
  }
  // each torso piece: rendered alone (the depth decides the stack in the game). A slot is two-sided when its
  // mirror image (all its pieces at once - a pair of pauldrons is symmetric though each one is not) differs.
  const names = [...new Set(prims.map(p => p.piece))].filter(n => slots[n] && slots[n] !== 'Head');
  const baked = names.map(name => {
    const mine = prims.filter(p => p.piece === name), palette = { map: new Map() };
    const hits = YAWS.map(y => only(render(mine, y, SHEAR), name)), mh = YAWS.map(y => only(render(mine.map(mirrored), y, SHEAR), name));
    return { name, slot: slots[name], outR: mine[0].outR ?? OUT_R, palette, hits, mh };
  });
  const union = (list, key, i) => { const m = new Map(), z = new Map(); for (const b of list) for (const [k, h] of b[key][i]) if (!z.has(k) || h.z > z.get(k)) { z.set(k, h.z); m.set(k, h.color); } return m; };
  const sameMap = (a, b) => a.size === b.size && [...a].every(([k, c]) => b.get(k) === c);
  for (const b of baked) {
    const group = baked.filter(o => o.slot === b.slot);
    const asym = YAWS.some((_, i) => !sameMap(union(group, 'hits', i), union(group, 'mh', i)));
    const views = b.hits.map(h => view(h, b.palette)), mv = asym ? b.mh.map(h => view(h, b.palette)) : null;
    piece(id, b.slot, b.name, b.outR, palStr(b.palette), views, mv);
    process.stderr.write(id + ' ' + b.name + (asym ? ' (both sides)' : '') + '\n');
  }
}

// ---- PHASE 3b: the limb gear, as STRIPS along the limb (Body8 sweeps them along the posed skeleton). Each piece
// (its right-hand one; the left wears the same) is rendered alone straight on, front / side / back, and sampled along
// each limb's axis at rest: per step the radius of the piece there and STRIP_K colours across it (the foot: across its
// height, the axis on the sole). The segments are Locomotion8's / Body8's, the rest pose views.js's.
const STRIP_K = 7;
const LIMB_OF = {
  hoplite: { gloveR: 'Gloves', greaveR: 'Boots', sandalR: 'Boots' },
  wayfarer: { breechesR: 'Legs', wrapsR: 'Boots', bootR: 'Boots', gloveR: 'Gloves' },
  athanor: { trousersR: 'Legs', bootR: 'Boots', gloveR: 'Gloves' },
};
const STRIP_VIEWS = [['front', 0], ['side', 90], ['back', 180]];
function segments() {
  POSE.hands = {}; CARDS = [];
  const A = arm(-1);
  return [
    { name: 'Thigh', a: v3(-LEG_X, HIP_Y, 0), b: v3(-LEG_X, 33, 0), t0: -0.25, t1: 1.0 },
    { name: 'Shin', a: v3(-LEG_X, 33, 0), b: v3(-LEG_X, 4, 0), t0: -0.12, t1: (33 - 9.5) / 29 },
    { name: 'Fore', a: A.el, b: A.wr, t0: -0.3, t1: 1.3 },
    { name: 'Foot', foot: true, a: v3(-LEG_X, 1, -3), b: v3(-LEG_X, 1, 9), t0: -0.35, t1: 1.15 },
  ];
}
const enc = v => String.fromCharCode(97 + Math.max(0, Math.min(25, Math.round(v))));
function strip(hits, seg, yaw, palette) {
  const a = yaw * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
  const toView = p => v3(p.x * ca + p.z * sa, p.y, -p.x * sa + p.z * ca);
  const len = len3(v3(seg.b.x - seg.a.x, seg.b.y - seg.a.y, seg.b.z - seg.a.z)), n = Math.max(2, Math.round(len * (seg.t1 - seg.t0)));
  let rad = '', off = '', cols = '', any = false;
  // only what lies on this limb: surface points within 9 texels of its axis (in 3D)
  const ab = v3(seg.b.x - seg.a.x, seg.b.y - seg.a.y, seg.b.z - seg.a.z), L2 = ab.x * ab.x + ab.y * ab.y + ab.z * ab.z;
  const near = h => { const q = h.p, ap = v3(q.x - seg.a.x, q.y - seg.a.y, q.z - seg.a.z); const t = Math.max(seg.t0, Math.min(seg.t1, (ap.x * ab.x + ap.y * ab.y + ap.z * ab.z) / L2));
    return len3(v3(ap.x - ab.x * t, ap.y - ab.y * t, ap.z - ab.z * t)) < (seg.foot ? 11 : 9); };
  hits = new Map([...hits].filter(([, h]) => near(h)));
  for (let i = 0; i < n; i++) {
    const t = seg.t0 + (seg.t1 - seg.t0) * (i + 0.5) / n, P = toView(lerp3(seg.a, seg.b, t));
    const col = Math.floor(P.x), row = Math.floor(Y1 - P.y);
    let lo, hi, centre;
    if (seg.foot) {   // the column through the foot: its height from the sole up (to 10)
      const ys = []; for (let r = Math.floor(Y1 - 10.5); r <= Math.floor(Y1 + 0.5); r++) if (hits.has(K(col, r))) ys.push(r);
      if (!ys.length) { rad += 'a'; off += 'a'; cols += '.'.repeat(STRIP_K); continue; }
      lo = Math.min(...ys); hi = Math.max(...ys);
      const r = (hi - lo + 1) / 2, cy = Y1 - (lo + hi + 1) / 2;       // centre height
      rad += enc(r * 2); off += enc((cy - 1 + 5) * 2);
      for (let k = 0; k < STRIP_K; k++) { const y = Math.min(hi, Math.max(lo, Math.round(lo + (hi - lo) * k / (STRIP_K - 1)))); cols += palette(hits.get(K(col, y)).color); }
      any = true; continue;
    }
    if (!hits.has(K(col, row))) { let c2 = null; for (const d of [-1, 1, -2, 2]) if (hits.has(K(col + d, row))) { c2 = col + d; break; } if (c2 === null) { rad += 'a'; off += 'a'; cols += '.'.repeat(STRIP_K); continue; } }
    lo = col; while (hits.has(K(lo - 1, row))) lo--; hi = col; while (hits.has(K(hi + 1, row))) hi++;
    if (!hits.has(K(col, row))) { lo = hi = [-1, 1, -2, 2].map(d => col + d).find(c => hits.has(K(c, row))); while (hits.has(K(lo - 1, row))) lo--; while (hits.has(K(hi + 1, row))) hi++; }
    rad += enc((hi - lo + 1)); off += enc(10 + (lo + hi + 1 - 2 * P.x));   // radius * 2; the run's centre off the axis * 2 (+10)
    for (let k = 0; k < STRIP_K; k++) { const x = Math.round(lo + (hi - lo) * k / (STRIP_K - 1)); cols += palette(hits.get(K(x, row)).color); }
    any = true;
  }
  return any ? { n, rad, off, cols } : null;
}
const strips = [];
for (const [id, make] of Object.entries(SETS8)) {
  POSE.hands = {}; CARDS = [];
  const prims = make();
  for (const [name, slot] of Object.entries(LIMB_OF[id])) {
    const mine = prims.filter(p => p.piece === name), pal = { map: new Map() };
    const ch = c => { if (!pal.map.has(c)) pal.map.set(c, CH[pal.map.size]); return pal.map.get(c); };
    const renders = STRIP_VIEWS.map(([, y]) => render(mine, y, 0));
    for (const seg of segments().filter(g => (g.name === 'Fore') === (slot === 'Gloves'))) {
      const vs = STRIP_VIEWS.map(([, y], i) => strip(renders[i], seg, y, ch));
      if (!vs.some(Boolean)) continue;
      const v = x => x ? `S(${x.n}, "${x.rad}", "${x.off}", "${x.cols}")` : 'null';
      strips.push(`            AddStrip("${id}", GearSlot.${slot}, "${name}", Limb8.${seg.name}, ${f(seg.t0)}, ${f(seg.t1)}, ${mine[0].outR ?? OUT_R}, "${palStr(pal)}",\n                ${vs.map(v).join(',\n                ')});`);
      process.stderr.write(id + ' ' + name + ' ' + seg.name + '\n');
    }
  }
}
lines.push(...strips);

const cs = `using Convergence.Art.Gear;

namespace Convergence.Art.Gear
{
    // ====================================================== GENERATED: the eight-direction pictures (phase 3)
    //
    // Baked from the approved five-view mock (Tools/tier-art/views.js) by bake8.js - do not hand-edit: change
    // the mock and run Tools/tier-art/regen8.sh. Per piece and view (front, front 3/4, side, back 3/4, back,
    // all facing right): its box in texels (columns from the body's centre line, rows down from ${Y1} texels
    // above the soles), its colours, and a depth per texel (base64 bytes, (z + 64) * 2, 255 empty). A second
    // list, when a piece has one, is its MIRROR IMAGE's views: what it shows facing left.
    public static partial class Body8Art
    {
        public const int Top = ${Y1};

        static void Bake()
        {
${lines.join('\n\n')}
        }
    }
}
`;
fs.writeFileSync(out, cs);
console.log('baked', cs.length, 'chars');
