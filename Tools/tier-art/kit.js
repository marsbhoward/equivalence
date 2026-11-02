// Texel kit for the tier-armour mock. Body coords: the bare body grid (BODY_ROWS) is 38 x 120 GEAR
// texels (150 ppu); x grows right, y grows DOWN. Each gear layer gets the game's auto-outline: one body
// art pixel = 4 gear texels, drawn outside the layer's own shape (PixelSprite.OutlineCanvasPadFor).
const OUTLINE = '#14121C';
const OUT_R = 4;

function hex2rgb(h) { h = h.replace('#', ''); return [0, 2, 4].map(i => parseInt(h.substr(i, 2), 16)); }
function rgb2hex(c) { return '#' + c.map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join(''); }
function mix(a, b, t) { const A = hex2rgb(a), B = hex2rgb(b); return rgb2hex(A.map((v, i) => v + (B[i] - v) * t)); }
function scale(a, f) { return rgb2hex(hex2rgb(a).map(v => v * f)); }

// Palette.Ramp: Glow, Light, Base, Dark, Deep, Line. `sheen` lifts warm metals toward a hue, not white.
function ramp(base, o = {}) {
  const lift = o.lift ?? 0.34, shade = o.shade ?? 0.30, sheen = o.sheen ?? '#ffffff';
  return {
    glow: mix(base, sheen, Math.min(1, lift + 0.30)), light: mix(base, sheen, lift), base,
    dark: mix(base, '#000000', shade), deep: mix(base, '#000000', Math.min(1, shade + 0.24)),
    line: mix(base, '#0d0a14', 0.68),
  };
}
const T = r => [r.line, r.deep, r.dark, r.base, r.light, r.glow];   // tone index 0..5
function tone(r, i) { const t = T(r); return t[Math.max(0, Math.min(5, i))]; }

// ---------------------------------------------------------------- masks and layers
const K = (x, y) => (y + 200) * 1000 + (x + 200);
const KX = k => (k % 1000) - 200, KY = k => Math.floor(k / 1000) - 200;

class Mask {
  constructor() { this.s = new Set(); }
  add(x, y) { this.s.add(K(x, y)); return this; }
  has(x, y) { return this.s.has(K(x, y)); }
  del(x, y) { this.s.delete(K(x, y)); }
  *pts() { for (const k of this.s) yield [KX(k), KY(k)]; }
  union(m) { for (const k of m.s) this.s.add(k); return this; }
  minus(m) { for (const k of m.s) this.s.delete(k); return this; }
  clip(m) { for (const k of [...this.s]) if (!m.s.has(k)) this.s.delete(k); return this; }
  bbox() { let a = 1e9, b = 1e9, c = -1e9, d = -1e9; for (const [x, y] of this.pts()) { a = Math.min(a, x); b = Math.min(b, y); c = Math.max(c, x); d = Math.max(d, y); } return { x0: a, y0: b, x1: c, y1: d, w: c - a + 1, h: d - b + 1 }; }
  get size() { return this.s.size; }
}
function rect(x0, y0, x1, y1) { const m = new Mask(); for (let y = Math.round(y0); y <= Math.round(y1); y++) for (let x = Math.round(x0); x <= Math.round(x1); x++) m.add(x, y); return m; }
function poly(p) {   // texel centres inside the polygon (even-odd)
  const m = new Mask(); let y0 = 1e9, y1 = -1e9, x0 = 1e9, x1 = -1e9;
  for (const [x, y] of p) { y0 = Math.min(y0, y); y1 = Math.max(y1, y); x0 = Math.min(x0, x); x1 = Math.max(x1, x); }
  for (let y = Math.floor(y0); y <= Math.ceil(y1); y++) for (let x = Math.floor(x0); x <= Math.ceil(x1); x++) {
    const px = x + 0.5, py = y + 0.5; let inside = false;
    for (let i = 0, j = p.length - 1; i < p.length; j = i++) {
      const [xi, yi] = p[i], [xj, yj] = p[j];
      if ((yi > py) !== (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) inside = !inside;
    }
    if (inside) m.add(x, y);
  }
  return m;
}
function ell(cx, cy, rx, ry) { const m = new Mask(); for (let y = Math.floor(cy - ry); y <= Math.ceil(cy + ry); y++) for (let x = Math.floor(cx - rx); x <= Math.ceil(cx + rx); x++) { const dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry; if (dx * dx + dy * dy <= 1) m.add(x, y); } return m; }
// an ellipse rotated by `deg` (0 = long axis vertical; positive leans the top to the right)
function rell(cx, cy, rx, ry, deg) { const a = deg * Math.PI / 180, c = Math.cos(a), sn = Math.sin(a), R = Math.max(rx, ry) + 1, m = new Mask();
  for (let y = Math.floor(cy - R); y <= Math.ceil(cy + R); y++) for (let x = Math.floor(cx - R); x <= Math.ceil(cx + R); x++) {
    const dx = x + 0.5 - cx, dy = y + 0.5 - cy; const u = dx * c + dy * sn, v = -dx * sn + dy * c; if ((u / rx) ** 2 + (v / ry) ** 2 <= 1) m.add(x, y); }
  return m; }
// keep only texels within `d` of the mask's edge (a border band), or erode it by d
function band(m, d) { const o = new Mask(); for (const [x, y] of m.pts()) { let edge = false; for (let dy = -d; dy <= d && !edge; dy++) for (let dx = -d; dx <= d; dx++) if (!m.has(x + dx, y + dy)) { edge = true; break; } if (edge) o.add(x, y); } return o; }
function U(...ms) { const m = new Mask(); for (const a of ms) m.union(a); return m; }

class Layer {
  constructor(depth) { this.depth = depth; this.c = new Map(); this.outline = true; this.far = false; }
  set(x, y, col) { if (col) this.c.set(K(x, y), col); }
  get(x, y) { return this.c.get(K(x, y)); }
  has(x, y) { return this.c.has(K(x, y)); }
  mask() { const m = new Mask(); for (const k of this.c.keys()) m.s.add(k); return m; }
  fill(m, shader) { for (const [x, y] of m.pts()) this.set(x, y, typeof shader === 'string' ? shader : shader(x, y, m)); return this; }
  // paint only over texels this layer already has
  over(m, shader) { for (const [x, y] of m.pts()) if (this.has(x, y)) this.set(x, y, typeof shader === 'string' ? shader : shader(x, y, m)); return this; }
  line(x0, y0, x1, y1, col, onlyOver = true) {
    x0 = Math.round(x0); y0 = Math.round(y0); x1 = Math.round(x1); y1 = Math.round(y1);
    let dx = Math.abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.abs(y1 - y0), sy = y0 < y1 ? 1 : -1, e = dx + dy;
    for (; ;) { if (!onlyOver || this.has(x0, y0)) this.set(x0, y0, col); if (x0 === x1 && y0 === y1) break; const e2 = 2 * e; if (e2 >= dy) { e += dy; x0 += sx; } if (e2 <= dx) { e += dx; y0 += sy; } }
    return this;
  }
  dot(x, y, col, onlyOver = true) { if (!onlyOver || this.has(x, y)) this.set(x, y, col); return this; }
}

// ---------------------------------------------------------------- shaders (light from the upper left)
const LIGHT = (() => { const v = [-0.55, -0.65, 0.55]; const n = Math.hypot(...v); return v.map(a => a / n); })();
function litTone(nx, ny, nz, bias = 0) {
  const d = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2] + bias;
  return d > 0.86 ? 5 : d > 0.62 ? 4 : d > 0.30 ? 3 : d > 0.02 ? 2 : 1;
}
// a sphere-ish dome over the ellipse (cx,cy,rx,ry)
const dome = (r, cx, cy, rx, ry, bias = 0) => (x, y) => {
  let nx = (x + 0.5 - cx) / rx, ny = (y + 0.5 - cy) / ry; let d = nx * nx + ny * ny;
  if (d > 1) { const s = 1 / Math.sqrt(d); nx *= s; ny *= s; d = 1; }
  return tone(r, litTone(nx, ny, Math.sqrt(1 - d), bias));
};
// a vertical cylinder centred on cx, half-width rx
const cyl = (r, cx, rx, bias = 0) => (x) => {
  let nx = Math.max(-1, Math.min(1, (x + 0.5 - cx) / rx));
  return tone(r, litTone(nx, -0.15, Math.sqrt(1 - nx * nx), bias));
};
// a flat plate with a bevelled rim of `w`: lit on top/left edges, dark on bottom/right
const bevel = (r, w = 1, face = 3) => (x, y, m) => {
  let up = false, lf = false, rt = false, dn = false;
  for (let i = 1; i <= w; i++) { up ||= !m.has(x, y - i); lf ||= !m.has(x - i, y); rt ||= !m.has(x + i, y); dn ||= !m.has(x, y + i); }
  if ((up && lf)) return tone(r, face + 2);
  if (up || lf) return tone(r, face + 1);
  if (dn || rt) return tone(r, face - 1);
  return tone(r, face);
};
// shade a mask with a horizontal gradient across its own bbox (left lit, right dark) plus a bevel
const plate = (r, w = 1, bias = 0) => (x, y, m) => {
  const b = m.bbox(); const t = (x + 0.5 - b.x0) / b.w;
  const face = t < 0.22 ? 4 : t < 0.7 ? 3 : 2;
  return bevel(r, w, face + bias)(x, y, m);
};

// ---------------------------------------------------------------- body
const BODY = { nearArm: null, farArm: null, legs: null, torso: null, head: null };
function bodyLayers() {
  const L = { nearArm: new Layer(12), farArm: new Layer(1), legs: new Layer(3), torso: new Layer(6), head: new Layer(10) };
  for (const l of Object.values(L)) l.outline = false;
  BODY_ROWS.forEach((row, y) => [...row].forEach((ch, x) => {
    if (ch === '.') return;
    const c = BODY_PAL[ch];
    let part = y <= 29 ? 'head' : y >= 75 ? 'legs' : (x <= 9 && y <= 70) ? 'nearArm' : (x >= 30 && y <= 70) ? 'farArm' : 'torso';
    if (y >= 71 && y <= 74) part = 'torso';
    L[part].set(x, y, c);
  }));
  return L;
}

// ---------------------------------------------------------------- composition
// depth: back 0, farArm 1, farSleeve 1.5, farGlove 2, legs(body) 3, boots 4, legs 5, torso(body) 6,
// torso 7, belt 8, backOver 8.5, farPauldron 9, helmBack 9.5, head(body) 10, helm 11, nearArm 12,
// nearSleeve 12.5, nearGlove 13, nearPauldron 14
const DEPTH = { back: 0, farSleeve: 1.5, farGlove: 2, boots: 4, legs: 6.5, torso: 7, belt: 8, backOver: 8.5, farPauldron: 9, helmBack: 9.5, helm: 11, nearSleeve: 12.5, nearGlove: 13, nearPauldron: 14, front: 15 };
const FAR_SHADE = 0.85;

function compose(layers, hide = {}) {
  const grid = new Map();
  const all = [...layers].sort((a, b) => a.depth - b.depth);
  for (const l of all) {
    if (l.outline) {
      const m = l.mask();
      const ring = new Map();
      const R = l.outR ?? OUT_R;
      for (const [x, y] of m.pts()) for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) {
        if (dx * dx + dy * dy > R * R + 2) continue;
        const k = K(x + dx, y + dy); if (!m.s.has(k)) ring.set(k, OUTLINE);
      }
      for (const [k, c] of ring) grid.set(k, c);
    }
    for (const [k, c] of l.c) grid.set(k, l.far ? scale(c, FAR_SHADE) : c);
  }
  return grid;
}
function gridBox(grid) { let a = 1e9, b = 1e9, c = -1e9, d = -1e9; for (const k of grid.keys()) { const x = KX(k), y = KY(k); a = Math.min(a, x); b = Math.min(b, y); c = Math.max(c, x); d = Math.max(d, y); } return { x0: a, y0: b, x1: c, y1: d }; }

// mirror helper: arms. near arm spans x 0..9, far 28..37 -> X(x) = 37 - x
const NEAR = { X: x => x, s: 1, side: 'near' };
const FAR = { X: x => 37 - x, s: -1, side: 'far' };
const sidePoly = (S, pts) => poly(pts.map(([x, y]) => [S === NEAR ? x : 38 - x, y]));
const sideRect = (S, x0, y0, x1, y1) => S === NEAR ? rect(x0, y0, x1, y1) : rect(37 - x1, y0, 37 - x0, y1);
const sideEll = (S, cx, cy, rx, ry) => ell(S === NEAR ? cx : 38 - cx, cy, rx, ry);
const sx = (S, x) => S === NEAR ? x : 37 - x;     // a texel column
const scx = (S, x) => S === NEAR ? x : 38 - x;    // a continuous coordinate

if (typeof module !== 'undefined') module.exports = { rell, band, OUTLINE, ramp, tone, mix, scale, Mask, rect, poly, ell, U, Layer, dome, cyl, bevel, plate, bodyLayers, compose, gridBox, DEPTH, NEAR, FAR, sidePoly, sideRect, sideEll, sx, scx, K, KX, KY };
