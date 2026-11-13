// node (regen8x.sh): cat body.js kit.js sets.js <bake.js 72-189> accessories.js arms.js <views.js without its sheet>
//                    <views-extras.js without its main> bake8x.js | node - <out.cs> <out.bytes>
// EIGHT DIRECTIONS, THE EXTRAS (RIG.md "Scarves, accessories, arm pieces"): bakes the approved 3D mocks of
// views-extras.js (the user's yes, 2026-11-11) into the game, as bake8.js does the Bronze armour:
//   - the scarf's COLLAR and every static ACCESSORY piece per view (front .. back, facing right; a mirror image's
//     views where the piece is one-sided), through the arena tilt, WITH A DEPTH PER TEXEL -> Body8Art.Extras.cs.
//     Colours that change per item (the scarf's cloth, the collar's bird, the banner's and the wraps' cloth, the
//     phials' liquid) are written as ROLES ("@scarf3": the scarf ramp's tone 3), resolved by the rig per item;
//   - the wrist wraps' wrap as a limb STRIP on the forearm (their tails, the scarf's tail and the orbiting stones
//     are drawn live: they move);
//   - the eleven ARM PIECES as point clouds in their segment's own frame (shoulder cap, upper arm, forearm), each
//     point with its normal and its colour rule (the tone it takes for the light it gets), so Body8 poses them on
//     the drawn arm, any swing, and lights them per view -> Assets/Resources/Body8/ArmPieces8.bytes (binary).
// GENERATED OUTPUTS - do not hand-edit them: change the mocks and run Tools/tier-art/regen8x.sh.
const fs = require('fs');
const outCs = process.argv[2], outBytes = process.argv[3];
const SHEAR8 = 0.45, YAWS8 = [0, 45, 90, 135, 180], TOP8 = 176, RY1 = 200, RY0 = -24, RHALF = 64;

// ---------------------------------------------------------------- render (bake8.js render(), a taller box)
function render8(prims, yawDeg, shear = SHEAR8) {
  const a = yawDeg * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
  const toBody = (vx, vy, vz) => v3(vx * ca - vz * sa, vy, vx * sa + vz * ca);
  const toView = n => v3(n.x * ca + n.z * sa, n.y, -n.x * sa + n.z * ca);
  const hits = new Map();
  for (let row = 0; row < RY1 - RY0; row++) for (let col = -RHALF; col <= RHALF; col++) {
    const sy = RY1 - row - 0.5, vx = col + 0.5;
    let t = 0, hit = null;
    while (t < 140) {
      const vz = 70 - t, p = toBody(vx, sy + vz * shear, vz);
      let best = 1e9, bp = null;
      for (const pr of prims) { const d = pr.sdf(p); if (d < best) { best = d; bp = pr; } }
      if (best < 0.02) { hit = { prim: bp, p, vz }; break; }
      t += Math.max(0.2, best * 0.8);
    }
    if (!hit) continue;
    const e = 0.25, pr = hit.prim, p = hit.p;
    const n0 = v3(pr.sdf(v3(p.x + e, p.y, p.z)) - pr.sdf(v3(p.x - e, p.y, p.z)), pr.sdf(v3(p.x, p.y + e, p.z)) - pr.sdf(v3(p.x, p.y - e, p.z)), pr.sdf(v3(p.x, p.y, p.z + e)) - pr.sdf(v3(p.x, p.y, p.z - e)));
    const nl = len3(n0) || 1, nb = v3(n0.x / nl, n0.y / nl, n0.z / nl);
    hits.set(K(col, row), { piece: pr.piece, z: hit.vz, p, color: pr.color(p, toView(nb), nb) });
  }
  return hits;
}

// ---------------------------------------------------------------- roles: colours that are the item's, not the mock's
// A sentinel ramp per role; every colour of it the bake meets is written "@<role><tone>".
const ROLE_OF = new Map();
function roleRamp(role, base, o) { const r = ramp(base, o); T(r).forEach((c, i) => { if (!ROLE_OF.has(c)) ROLE_OF.set(c, role + i); }); return r; }
const CHARS8 = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&*+-/:;<=>?^_~';
const palStr8 = pal => [...pal.map].map(([c, ch]) => ROLE_OF.has(c) ? ch + '@' + ROLE_OF.get(c) : ch + c.slice(1).toUpperCase()).join(' ');
const charOf = (pal, c) => { if (!pal.map.has(c)) { if (pal.map.size >= CHARS8.length) throw new Error('too many colours'); pal.map.set(c, CHARS8[pal.map.size]); } return pal.map.get(c); };

// a piece's picture in one view (rows from TOP8 down; may start above it)
function view8(hits, pal) {
  if (!hits.size) return null;
  let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
  for (const k of hits.keys()) { x0 = Math.min(x0, KX(k)); x1 = Math.max(x1, KX(k)); y0 = Math.min(y0, KY(k)); y1 = Math.max(y1, KY(k)); }
  const w = x1 - x0 + 1, h = y1 - y0 + 1;
  let cols = ''; const depth = Buffer.alloc(w * h, 255);
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
    const hh = hits.get(K(x, y));
    if (!hh) { cols += '.'; continue; }
    cols += charOf(pal, hh.color);
    depth[(y - y0) * w + (x - x0)] = Math.max(0, Math.min(254, Math.round((hh.z + 64) * 2)));
  }
  return { x0, y0: y0 - (RY1 - TOP8), w, h, cols, depth: depth.toString('base64') };
}
const only8 = (hits, name) => new Map([...hits].filter(([, h]) => h.piece === name));
const sameMap8 = (a, b) => a.size === b.size && [...a].every(([k, h]) => b.has(k) && b.get(k).color === h.color);

const f8 = n => (Math.round(n * 1000) / 1000) + 'f';
const LIVE_CS = [];
const lines = [];
const vs8 = list => 'new[] { ' + list.map(v => v ? `V(${v.x0}, ${v.y0}, ${v.w}, ${v.h}, "${v.cols}", "${v.depth}")` : 'null').join(',\n                ') + ' }';
// every piece of `prims` named in `names` (default: all), each rendered alone; mirror views where the whole set differs
function bakePieces(set, slot, prims, opt = {}) {
  const names = [...new Set(prims.map(p => p.piece))].filter(n => !opt.skip || !opt.skip(n));
  const baked = names.map(name => {
    const mine = prims.filter(p => p.piece === name), pal = { map: new Map() };
    const hits = YAWS8.map(y => only8(render8(mine, y), name));
    const mh = opt.mirror ? YAWS8.map(y => only8(render8(mine.map(xmirrored), y), name)) : null;
    return { name, mine, pal, hits, mh };
  });
  for (const b of baked) {
    const asym = opt.mirror && YAWS8.some((_, i) => !sameMap8(b.hits[i], b.mh[i]));
    const views = b.hits.map(h => view8(h, b.pal)), mv = asym ? b.mh.map(h => view8(h, b.pal)) : null;
    const outR = b.mine[0].outR ?? OUT_R, bias = b.mine[0].depthBias || 0;
    lines.push(`            AddX("${set}", GearSlot.${slot}, "${b.name}", ${outR}, ${f8(bias)}, ${opt.overHead ? 'true' : 'false'}, "${palStr8(b.pal)}",\n                ${vs8(views)}${mv ? ',\n                ' + vs8(mv) : ', null'});`);
    process.stderr.write(set + ' ' + b.name + (asym ? ' (both sides)' : '') + '\n');
  }
}

// ---------------------------------------------------------------- the wearer: ONE shape for all of Bronze (the user's call)
// The pieces rest on the union of the three Bronze sets' torsos (and Hoplite's cape), so they clear the thickest
// cuirass; the bare body is not worn in play. Packs are left out (the collar and the hourglass sit on a cape).
POSE.hands = {}; CARDS = [];
const UNION = [...body3d(), ...hoplite3d(), ...wayfarer3d(), ...athanor3d()].filter(p => p.piece !== 'back');

// ================================================================ 1. the scarf's collar
{
  const r = roleRamp('scarf', '#123401');
  const [collar] = scarf3d(r, UNION, { gait: 'stand', phase: 0 });
  bakePieces('scarf', 'Neck', [collar]);
}

// ================================================================ 2. the accessories
ELEMENT.__S = '#123402'; roleRamp('liq', '#123402', { lift: 0.42, shade: 0.34 });
const PAL_R = roleRamp('pal', '#123403', { sheen: '#ffffff', lift: 0.40 }), TIP_R = roleRamp('tip', '#123404', { sheen: '#fff2b0', lift: 0.4 });
FEATHERS.__S = { r: PAL_R }; FEATHERS.__SE = { r: PAL_R, eye: true }; FEATHERS.__ST = { r: PAL_R, tip: TIP_R };
CLOTH.__S = PAL_R;
const ST8 = pal => ({ ...ACC_ST, el: '__S', pal });
// What moves with the game is drawn live (Body8.Live.cs): the bandolier's phials (they empty, uncork, crack), the
// harness's flask (its level and slosh), the hourglass (the run's sand) and the banner's cloth (its ripple). Not baked:
// the mocks record where they sit (LIVE8) and the colours they use are written for the game.
globalThis.LIVE8 = { phials: [] };
const LIVE_PIECES = new Set(['phial0', 'phial1', 'phial2', 'flask', 'hourglass', 'banner']);
const KIND_OF = { bandolier: 'Bandolier', harness: 'VialHarness', collar: 'FeatherCollar', banner: 'BannerPole', hourglass: 'HourglassPack', tome: 'ChainedTome' };
for (const a of ACC3D) {
  const id = a.acc.id;
  if (id === 'wraps' || id === 'stones') continue;          // the wrap is a strip below; the stones are drawn live
  if (id === 'collar') {
    // the birds: Crow and Swan share one picture (the ramp), the Peacock's eye and the Phoenix's tips their own
    for (const [variant, pal] of [['', '__S'], ['Peacock', '__SE'], ['Phoenix', '__ST']])
      bakePieces('acc_' + KIND_OF[id] + (variant ? '_' + variant : ''), 'Ring', a.make(UNION, ST8(pal)), { mirror: a.mirror, overHead: true });
    continue;
  }
  bakePieces('acc_' + KIND_OF[id], 'Ring', a.make(UNION, ST8('__S')), { mirror: a.mirror, overHead: true, skip: n => LIVE_PIECES.has(n) });
}
{
  const v = a => `new Vector3(${a.map(f8).join(', ')})`, ramp32 = r => 'new Color32[] { ' + T(r).map(c => { const h = c.slice(1); return `new Color32(0x${h.slice(0, 2)}, 0x${h.slice(2, 4)}, 0x${h.slice(4, 6)}, 0xFF)`; }).join(', ') + ' }';
  LIVE_CS.push(`        /// <summary>The bandolier's phials (x, the glass's bottom, z) on the wearer, front to back as views-extras.js has them.</summary>
        public static readonly Vector3[] LivePhials = { ${LIVE8.phials.map(v).join(', ')} };
        /// <summary>The harness's flask: its centre (x, y, z) and radius.</summary>
        public static readonly Vector4 LiveFlask = new Vector4(${LIVE8.flask.map(f8).join(', ')});
        /// <summary>The hourglass's frame centre, z.</summary>
        public const float LiveHourglassZ = ${f8(LIVE8.hourglass)};
        /// <summary>The mocks' ramps (line .. glow) the live pieces are coloured in.</summary>
        public static readonly Color32[] RampGlass = ${ramp32(AM.glass)}, RampCork = ${ramp32(AM.cork)}, RampSand = ${ramp32(AM.sand)},
            RampBronze = ${ramp32(M.bronze)}, RampBronzeDk = ${ramp32(M.bronzeDk)}, RampGold = ${ramp32(M.gold)};`);
}

// ---------------------------------------------------------------- the wraps' wrap as a forearm strip (bake8.js strip())
const STRIP_K8 = 7;
function strip8(hits, seg, yaw, ch) {
  const a = yaw * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
  const toView = p => v3(p.x * ca + p.z * sa, p.y, -p.x * sa + p.z * ca);
  const len = len3(v3(seg.b.x - seg.a.x, seg.b.y - seg.a.y, seg.b.z - seg.a.z)), n = Math.max(2, Math.round(len * (seg.t1 - seg.t0)));
  let rad = '', off = '', cols = '', any = false;
  const ab = v3(seg.b.x - seg.a.x, seg.b.y - seg.a.y, seg.b.z - seg.a.z), L2 = ab.x * ab.x + ab.y * ab.y + ab.z * ab.z;
  const near = h => { const q = h.p, ap = v3(q.x - seg.a.x, q.y - seg.a.y, q.z - seg.a.z); const t = Math.max(seg.t0, Math.min(seg.t1, (ap.x * ab.x + ap.y * ab.y + ap.z * ab.z) / L2));
    return len3(v3(ap.x - ab.x * t, ap.y - ab.y * t, ap.z - ab.z * t)) < 9; };
  hits = new Map([...hits].filter(([, h]) => near(h)));
  const enc = v => String.fromCharCode(97 + Math.max(0, Math.min(25, Math.round(v))));
  const Y1s = RY1;
  for (let i = 0; i < n; i++) {
    const t = seg.t0 + (seg.t1 - seg.t0) * (i + 0.5) / n, P = toView(lerp3(seg.a, seg.b, t));
    const col = Math.floor(P.x), row = Math.floor(Y1s - P.y);
    if (!hits.has(K(col, row))) { let c2 = null; for (const d of [-1, 1, -2, 2]) if (hits.has(K(col + d, row))) { c2 = col + d; break; } if (c2 === null) { rad += 'a'; off += 'a'; cols += '.'.repeat(STRIP_K8); continue; } }
    let lo = col; while (hits.has(K(lo - 1, row))) lo--; let hi = col; while (hits.has(K(hi + 1, row))) hi++;
    if (!hits.has(K(col, row))) { lo = hi = [-1, 1, -2, 2].map(d => col + d).find(c => hits.has(K(c, row))); while (hits.has(K(lo - 1, row))) lo--; while (hits.has(K(hi + 1, row))) hi++; }
    rad += enc(hi - lo + 1); off += enc(10 + (lo + hi + 1 - 2 * P.x));
    for (let k = 0; k < STRIP_K8; k++) { const x = Math.round(lo + (hi - lo) * k / (STRIP_K8 - 1)); cols += ch(hits.get(K(x, row)).color); }
    any = true;
  }
  return any ? { n, rad, off, cols } : null;
}
{
  POSE.hands = {}; CARDS = [];
  const prims = wraps3d(UNION, ST8('__S')).filter(p => p.piece === 'wrap-1');
  const A = arm(-1), seg = { a: A.el, b: A.wr, t0: -0.3, t1: 1.3 }, pal = { map: new Map() };
  const vs = [0, 90, 180].map(y => strip8(render8(prims, y, 0), seg, y, c => charOf(pal, c)));
  const v = x => x ? `S(${x.n}, "${x.rad}", "${x.off}", "${x.cols}")` : 'null';
  lines.push(`            AddStripX("acc_WristWraps", GearSlot.Ring, "wrap", Limb8.Fore, ${f8(seg.t0)}, ${f8(seg.t1)}, ${prims[0].outR ?? OUT_R}, "${palStr8(pal)}",\n                ${vs.map(v).join(',\n                ')});`);
  process.stderr.write('acc_WristWraps wrap (strip)\n');
}

// ================================================================ 3. the arm pieces
// Each armPrim's parts are kept (seg + an SDF in that segment's art space), sampled on a half-texel grid near the
// surface, and written with the normal there and the COLOUR RULE: the colours the piece's own colour code gives for
// each band of a light value (the kit's litTone, or the chrome's and the cage's own), checked against a second
// normal with the same value (a colour that depends on more than that is reported).
const _armPrim = armPrim;
armPrim = function (piece, outR, parts, F, color) { const o = _armPrim(piece, outR, parts, F, color); o._parts = parts; o._color = color; return o; };
const LT = v3(LIGHT[0], -LIGHT[1], LIGHT[2]);                       // litTone(n.x, -n.y, n.z) = n . LT
const RULE_LIT = { D: LT, T: [0.02, 0.30, 0.62, 0.86] };
const RULE_CHROME = { D: v3(0.9, -0.35, 0), T: [-0.62, -0.28, 0.02, 0.42, 0.78] };   // arms.js chrome() bands
const RULE_FACE = { D: v3(0, 0, 1), T: [0.55] };                                       // the cage's white core
const unit = v => { const l = len3(v) || 1; return v3(v.x / l, v.y / l, v.z / l); };
const perpOf = D => unit(Math.abs(D.z) < 0.9 ? cross3(D, v3(0, 0, 1)) : cross3(D, v3(1, 0, 0)));
// normals whose value along the rule's D is mid-band
function bandNormals(rule, which) {
  const Dl = len3(rule.D), Dn = unit(rule.D), P = which ? unit(cross3(Dn, perpOf(Dn))) : perpOf(Dn), out = [];
  const lo = -Dl, edges = [lo, ...rule.T, Dl];
  for (let i = 0; i + 1 < edges.length; i++) {
    const h = (Math.max(edges[i], -Dl * 0.98) + Math.min(edges[i + 1], Dl * 0.98)) / 2, a = Math.max(-0.999, Math.min(0.999, h / Dl)), b = Math.sqrt(1 - a * a);
    out.push(v3(Dn.x * a + P.x * b, Dn.y * a + P.y * b, Dn.z * a + P.z * b));
  }
  return out;
}
const RULES = new Map(), ruleList = [];
let mismatches = 0;
function ruleIndex(rule, colors, rim) {
  const key = [rule.D.x, rule.D.y, rule.D.z, ...rule.T, ...colors, rim || ''].join(',');
  if (!RULES.has(key)) { RULES.set(key, ruleList.length); ruleList.push({ D: rule.D, T: rule.T, C: colors, rim }); }
  return RULES.get(key);
}
const SEGS = ['cap', 'upper', 'fore'];
function armCloud(id) {
  POSE.hands = {}; CARDS = [];
  const piece = ARMS.find(a => a.id === id), F = armFrames(0, 0);
  const base = withoutNearArm([...body3d(), ...hoplite3d()]);
  const prims = ARM3D[id](F, artLayers(piece), base).filter(p => p._parts);
  const pts = [], primInfo = [];
  prims.forEach((pr, pi) => {
    // the mock sorts the arm over the torso's pieces (+6); its own biases kept (Salt's crystals over their rock)
    primInfo.push({ outR: pr.outR, bias: (pr.depthBias || 0) + 6, alpha: pr.alpha ?? 1 });
    for (const seg of SEGS) {
      const parts = pr._parts.filter(q => q.seg === seg); if (!parts.length) continue;
      const D = (X, Y, n) => { let d = 1e9; for (const q of parts) d = Math.min(d, q.sdf(X, Y, n)); return d; };
      // coarse cells that may hold surface, then the half-texel grid in them
      const cand = new Set();
      for (let X = -22; X <= 32; X += 1) for (let Y = 8; Y <= 92; Y += 1) for (let n = -22; n <= 22; n += 1) {
        const d = D(X, Y, n); if (Math.abs(d) < 1.3) cand.add(X + ',' + Y + ',' + n);
      }
      const seen = new Set();
      for (const c of cand) {
        const [cx, cy, cn] = c.split(',').map(Number);
        for (let i = -1; i <= 1; i++) for (let j = -1; j <= 1; j++) for (let k = -1; k <= 1; k++) {
          const X = cx + i * 0.5, Y = cy + j * 0.5, n = cn + k * 0.5, key = X + ',' + Y + ',' + n;
          if (seen.has(key)) continue; seen.add(key);
          let best = 1e9, bq = null; for (const q of parts) { const d = q.sdf(X, Y, n); if (d < best) { best = d; bq = q; } }
          if (Math.abs(best) > 0.4) continue;
          const e = 0.2, g = v3(D(X + e, Y, n) - D(X - e, Y, n), D(X, Y + e, n) - D(X, Y - e, n), D(X, Y, n + e) - D(X, Y, n - e)), nl = len3(g);
          if (!(nl > 1e-6)) continue;
          const N = v3(g.x / nl, g.y / nl, g.z / nl);
          // the surface point: back along the normal by the distance
          const P = [X - N.x * best, Y - N.y * best, n - N.z * best];
          const col = bq.color || pr._color;
          // the art frame's (X, Y, n) normal is the view's normal at rest, facing front (x right = art X, y up = -Y)
          const at = nv => col(P[0], P[1], P[2], v3(nv.x, nv.y, nv.z), bq);
          let rule = RULE_LIT, rim = null;
          if (id === 'quicksilver' && bq.drip === undefined) rule = RULE_CHROME;
          if (id === 'fulmen-cage') rule = RULE_FACE;
          let colors;
          if (id === 'penumbra-echo' && bq.L) {
            // the ghost: lit in its ramp, the violet rim at the silhouette's lit side (arms.js ghostColor)
            colors = [1, 2, 3, 4, 5].map(i => tone(AR.ghost, i)); rim = tone(AR.violet, 3);
          } else {
            const a = bandNormals(rule, 0).map(at), b = bandNormals(rule, 1).map(at);
            if (a.some((c, i) => c !== b[i])) mismatches++;
            colors = a;
          }
          pts.push({ seg: SEGS.indexOf(seg), prim: pi, P, N, rule: ruleIndex(rule, colors, rim) });
        }
      }
    }
  });
  process.stderr.write('arm ' + id + ': ' + pts.length + ' points, ' + prims.length + ' prims\n');
  return { id, primInfo, pts };
}

// the crucible's chest strap and buckle ride the torso: baked like the accessories
{
  POSE.hands = {}; CARDS = [];
  const piece = ARMS.find(a => a.id === 'crucible');
  const prims = ARM3D.crucible(armFrames(0, 0), artLayers(piece), withoutNearArm(UNION)).filter(p => /^cb(Strap|Buckle)/.test(p.piece));
  bakePieces('arm_crucible', 'Ring', prims, { mirror: true });
}

const clouds = ARM3D_ORDER.map(armCloud);
// FULMEN's bolts re-strike every frame (the mock's zap() is re-rolled 12 times a second): five more strikes, the game
// cycling the six (ArmPieces8.FulmenStrikes), "fulmen-cage#1" .. "#5"
for (let k = 1; k < 6; k++) {
  const t0 = ARM_ST.t; ARM_ST.t = t0 + k / 12;
  const c = armCloud('fulmen-cage'); c.id = 'fulmen-cage#' + k; clouds.push(c);
  ARM_ST.t = t0;
}
if (mismatches) process.stderr.write('arm colours that depend on more than their rule\'s light value: ' + mismatches + ' points\n');

// ---------------------------------------------------------------- the binary (little-endian)
{
  const bufs = [], u8 = v => { const b = Buffer.alloc(1); b.writeUInt8(v); bufs.push(b); }, u16 = v => { const b = Buffer.alloc(2); b.writeUInt16LE(v); bufs.push(b); },
    u32 = v => { const b = Buffer.alloc(4); b.writeUInt32LE(v >>> 0); bufs.push(b); }, f32 = v => { const b = Buffer.alloc(4); b.writeFloatLE(v); bufs.push(b); },
    str = s => { const b = Buffer.from(s, 'utf8'); u8(b.length); bufs.push(b); };
  const rgba = c => { const [r, g, b] = hex2rgb(c); return ((r << 24) | (g << 16) | (b << 8) | 255) >>> 0; };
  bufs.push(Buffer.from('B8A1'));
  u16(ruleList.length);
  for (const r of ruleList) { f32(r.D.x); f32(r.D.y); f32(r.D.z); u8(r.T.length); r.T.forEach(f32); r.C.forEach(c => u32(rgba(c))); u32(r.rim ? rgba(r.rim) : 0); }
  u8(clouds.length);
  for (const c of clouds) {
    str(c.id); u8(c.primInfo.length);
    for (const p of c.primInfo) { u8(p.outR); f32(p.bias); f32(p.alpha); }
    u32(c.pts.length);
    const q8 = v => Math.max(-32768, Math.min(32767, Math.round(v * 8)));
    const b = Buffer.alloc(c.pts.length * 13);
    c.pts.forEach((p, i) => { const o = i * 13; b.writeUInt8(p.seg, o); b.writeUInt8(p.prim, o + 1); b.writeInt16LE(q8(p.P[0]), o + 2); b.writeInt16LE(q8(p.P[1]), o + 4); b.writeInt16LE(q8(p.P[2]), o + 6);
      b.writeInt8(Math.round(p.N.x * 127), o + 8); b.writeInt8(Math.round(p.N.y * 127), o + 9); b.writeInt8(Math.round(p.N.z * 127), o + 10); b.writeUInt16LE(p.rule, o + 11); });
    bufs.push(b);
  }
  fs.writeFileSync(outBytes, Buffer.concat(bufs));
  console.log('arm pieces', Buffer.concat(bufs).length, 'bytes,', ruleList.length, 'rules');
}

const cs = `using Convergence.Art.Gear;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== GENERATED: the eight-direction extras
    //
    // Baked from the approved 3D mocks of the scarves and accessories (Tools/tier-art/views-extras.js) by
    // bake8x.js - do not hand-edit: change the mocks and run Tools/tier-art/regen8x.sh. As Body8Art.Bronze.cs:
    // per piece and view (front .. back, facing right) its box, colours and a depth per texel; a second list,
    // when a piece has one, is its MIRROR IMAGE's views. A palette entry "x@role3" is a ROLE's tone 3 (the
    // scarf's cloth, the collar's bird, the cloth, the liquid), resolved per worn item by the rig.
    public static partial class Body8Art
    {
${LIVE_CS.join('\n')}

        static void BakeExtras()
        {
${lines.join('\n\n')}
        }
    }
}
`;
fs.writeFileSync(outCs, cs);
console.log('baked extras', cs.length, 'chars');
