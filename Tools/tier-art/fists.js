// FIST SHAPES (mock, 2026-11-09). The user asked for a round on the fists after a review of the body: in the
// game a fist is a square box, and from behind on the discs it reads as a brick. Each variant reshapes the
// hand region (rows HAND_TOP and down) of whatever is drawn there - the bare hand or a set's glove - so every
// set takes the same fist: a silhouette change (taper, rounded knuckle end) and one-texel details (a thumb on
// the inner side, finger lines on the knuckle face). The inner side faces the body: +x for the near hand,
// -x for the far one.
const HAND_TOP = 59;
const BODY_OUTLINES = new Set(['#13111B', '#0F0E16', '#13111b', '#0f0e16']);
const isNearX = x => x <= 13, isFarX = x => x >= 25;
// gauntlets with plate bands (lames) across the fist: the finger lines fight the bands, so they keep only the notches
const PLATED_FISTS = new Set(['kettle', 'sentinel', 'sol']);

// the hand's fill, per side: Map k -> colour, from the near (or far) arm and glove layers, the glove on top
function handFill(layers, near) {
  const out = new Map();
  const mine = layers.filter(l => l.depth === 12 || l.depth === 1 || l.depth === DEPTH.nearGlove || l.depth === DEPTH.farGlove)
    .sort((a, b) => a.depth - b.depth);
  for (const l of mine) for (const [k, c] of l.c) {
    const x = KX(k), y = KY(k);
    if (y < HAND_TOP || !(near ? isNearX(x) : isFarX(x)) || BODY_OUTLINES.has(c)) continue;
    out.set(k, l.far ? scale(c, FAR_SHADE) : c);
  }
  return out;
}
function stripHands(layers) {
  return layers.map(l => {
    if (!(l.depth === 12 || l.depth === 1 || l.depth === DEPTH.nearGlove || l.depth === DEPTH.farGlove)) return l;
    const o = new Layer(l.depth); Object.assign(o, { outline: l.outline, far: l.far, outR: l.outR });
    for (const [k, c] of l.c) { const x = KX(k), y = KY(k); if (y >= HAND_TOP && (isNearX(x) || isFarX(x))) continue; o.c.set(k, c); }
    return o;
  });
}
// rows of a fill: y -> [x0, x1]
function rowsOf(F) { const r = new Map(); for (const k of F.keys()) { const x = KX(k), y = KY(k), s = r.get(y) || [1e9, -1e9]; s[0] = Math.min(s[0], x); s[1] = Math.max(s[1], x); r.set(y, s); } return r; }

// the variants: (F, inner) -> F'. `inner` is +1 or -1.
const FISTS = [
  { id: 'current', name: 'Current', desc: 'Today\'s fist: a square box under its outline.', fn: F => F },
  { id: 'tapered', name: 'Tapered', desc: 'Narrower than the forearm and rounded at the knuckle end: the bottom corners come in one and two texels, the outer side in one.', fn: tapered },
  { id: 'thumb', name: 'Thumb', desc: 'Tapered, with a thumb wrapped over the inner side: a light one-texel ridge with a dark crease behind it.', fn: (F, s) => thumb(tapered(F, s), s) },
  { id: 'knuckles', name: 'Knuckles', desc: 'Tapered, with the fingers drawn on the knuckle face: dark lines between them and notches in the bottom edge, so the end reads as four knuckles. On plated gauntlets, the notches only.', fn: (F, s, set) => knuckles(tapered(F, s), s, PLATED_FISTS.has(set)) },
  { id: 'both', name: 'Thumb and knuckles', desc: 'Tapered, with both the thumb and the knuckles.', fn: (F, s) => knuckles(thumb(tapered(F, s), s), s) },
];

function tapered(F, inner) {
  const R = rowsOf(F), ys = [...R.keys()].sort((a, b) => a - b), bot = ys[ys.length - 1], top = ys[0], out = new Map();
  for (const [k, c] of F) {
    const x = KX(k), y = KY(k), [a, b] = R.get(y), outerEdge = inner > 0 ? a : b, innerEdge = inner > 0 ? b : a;
    const fromBot = bot - y;
    if (y >= top + 2 && x === outerEdge) continue;                       // the outer side in one
    if (fromBot === 0 && (Math.abs(x - outerEdge) <= 2 || Math.abs(x - innerEdge) <= 1)) continue;
    if (fromBot === 1 && (Math.abs(x - outerEdge) <= 1 || x === innerEdge)) continue;
    if (fromBot === 2 && x === outerEdge + inner) continue;
    out.set(k, c);
  }
  return out;
}
function thumb(F, inner) {
  const R = rowsOf(F), ys = [...R.keys()].sort((a, b) => a - b), top = ys[0], out = new Map(F);
  for (let y = top + 2; y <= top + 5; y++) {
    const s = R.get(y); if (!s) continue;
    const edge = inner > 0 ? s[1] : s[0], c = F.get(K(edge, y));
    out.set(K(edge + inner, y), mix(c, '#ffffff', 0.12));                // the thumb's ridge, proud of the side
    out.set(K(edge, y), mix(c, '#ffffff', 0.06));
    out.set(K(edge - inner, y), scale(c, 0.7));                           // the crease behind it
  }
  return out;
}
function knuckles(F, inner, notchesOnly = false) {
  const R = rowsOf(F), ys = [...R.keys()].sort((a, b) => a - b), bot = ys[ys.length - 1], out = new Map(F);
  const [a, b] = R.get(bot - 2), lines = [];
  for (let x = a + 2; x < b; x += 2) lines.push(x);
  for (const x of lines) for (let y = bot - 3; y <= bot; y++) {
    const k = K(x, y); if (!out.has(k)) continue;
    if (y === bot) out.delete(k); else if (!notchesOnly) out.set(k, scale(F.get(k), 0.68));   // notch the edge, line the face
  }
  return out;
}

// compose a figure with both fists reshaped: (layers, variant) -> Map k -> colour
function composeFists(layers, variant, setId = null) {
  const under = compose(stripHands(layers));
  const grid = new Map(under);
  for (const near of [true, false]) {
    const F0 = handFill(layers, near); if (!F0.size) continue;
    const gloved = layers.some(l => (l.depth === DEPTH.nearGlove || l.depth === DEPTH.farGlove)
                                    && [...l.c.keys()].some(k => KY(k) >= HAND_TOP && (near ? isNearX(KX(k)) : isFarX(KX(k)))));
    const F = variant.fn(F0, near ? 1 : -1, setId), R = gloved ? OUT_R : 2;
    // the outline ring, then the fill; the far hand is shaded as far layers are, and stays under the body
    const ring = new Set();
    for (const k of F.keys()) { const x = KX(k), y = KY(k);
      for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) { if (dx * dx + dy * dy > R * R + 2) continue; const kk = K(x + dx, y + dy); if (!F.has(kk)) ring.add(kk); } }
    if (near) { for (const k of ring) grid.set(k, OUTLINE); for (const [k, c] of F) grid.set(k, c); }
    else {
      const body = new Set(compose(stripHands(layers.filter(l => !l.far && l.depth >= 3 && l.depth !== 12 && l.depth !== DEPTH.nearGlove))).keys());
      for (const k of ring) if (!body.has(k)) grid.set(k, OUTLINE);
      for (const [k, c] of F) if (!body.has(k)) grid.set(k, c);
    }
  }
  return grid;
}

if (typeof module !== 'undefined') module.exports = { FISTS, composeFists };
