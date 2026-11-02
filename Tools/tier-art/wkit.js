// Weapon kit. Weapons are authored on their OWN grid, twice the body's density (GEAR.md "Weapons stay at their
// own density"): one weapon texel = 2 gear texels, and the auto-outline is ONE weapon texel. Swords 16 x 79
// (blade <= 8 wide, guard <= 14, grip 12 x 4 at cols 6-9), discs 28 tall and left-right symmetric in shape, bows 79 tall.
const W_OUT = 1;
const WL = d => { const l = new Layer(d); l.outR = W_OUT; return l; };

// per-row extents of a mask, cached on the mask
function spans(m) {
  if (m._sp) return m._sp;
  const map = new Map();
  for (const [x, y] of m.pts()) { const s = map.get(y) || [1e9, -1e9]; s[0] = Math.min(s[0], x); s[1] = Math.max(s[1], x); map.set(y, s); }
  return (m._sp = map);
}
// a blade from [y, halfWidth] points, symmetric about cx (continuous); half widths round to whole texels
function bladeMask(pts, cx = 8) {
  const m = new Mask();
  for (let i = 0; i < pts.length - 1; i++) {
    const [y0, h0] = pts[i], [y1, h1] = pts[i + 1];
    const last = i === pts.length - 2;
    for (let y = y0; y < y1 + (last ? 1 : 0); y++) {
      const t = y1 === y0 ? 0 : (y - y0) / (y1 - y0);
      const n = Math.max(1, Math.round(h0 + (h1 - h0) * t));
      for (let x = cx - n; x < cx + n; x++) m.add(x, y);
    }
  }
  return m;
}
// blade shading, across each row: 'ridge' (diamond section: lit left face, glint ridge, base right face),
// 'fuller' (a groove of width fw down the centre), 'flat' (lit left edge, flat face, dark right edge)
const bladeSh = (r, kind = 'ridge', o = {}) => (x, y, m) => {
  const sp = spans(m).get(y); const a = sp[0], b = sp[1], w = b - a + 1, cx = (a + b + 1) / 2;
  if (w <= 2) return tone(r, x === a ? 5 : 3);
  if (x === a) return tone(r, 5);
  if (x === b) return tone(r, 2);
  if (kind === 'flat') return tone(r, x - a < w * 0.4 ? 4 : 3);
  if (kind === 'fuller') {
    const fw = o.fw ?? 2, f0 = Math.round(cx - fw / 2);
    if (w >= fw + 4 && y >= (o.from ?? -1e9) && y <= (o.to ?? 1e9)) {
      if (x === f0) return tone(r, 1);
      if (x > f0 && x < f0 + fw - 1) return tone(r, 2);
      if (x === f0 + fw - 1) return tone(r, 4);
    }
    return tone(r, x < cx ? 4 : 3);
  }
  // ridge
  if (x === Math.floor(cx) - 1 && w >= 4) return tone(r, 5);
  return tone(r, x < cx ? 4 : 3);
};
// a cord/leather wrap: diagonal bands, lit left
const wrapSh = (r, period = 3, bias = 0) => (x, y, m) => {
  const sp = spans(m).get(y); const t = (x - sp[0]) / Math.max(1, sp[1] - sp[0]);
  const f = t < 0.3 ? 1 : t > 0.7 ? -1 : 0;
  return tone(r, (((x + y) % period + period) % period === 0 ? 1 : 3) + f + bias);
};
// rings round a grip: every `period` rows a band of r2
const bandsSh = (r, r2, period = 3, bias = 0) => (x, y, m) => {
  const sp = spans(m).get(y); const t = (x - sp[0]) / Math.max(1, sp[1] - sp[0]);
  const f = t < 0.3 ? 1 : t > 0.7 ? -1 : 0;
  return (((y % period) + period) % period === 0) ? tone(r2, 3 + f) : tone(r, 3 + f + bias);
};
// a round-ish metal part lit from the upper left
const ball = (r, m, bias = 0) => { const b = m.bbox(); return dome(r, b.x0 + b.w / 2, b.y0 + b.h / 2, b.w / 2, b.h / 2, bias); };
function mirrorX(m, W) { const o = new Mask(); for (const [x, y] of m.pts()) { o.add(x, y); o.add(W - 1 - x, y); } return o; }
// a thick stroke through points, stamped as discs of radius rr(t)
function stroke(pts, rr) {
  const m = new Mask(); const n = pts.length;
  for (let i = 0; i < n - 1; i++) {
    const [x0, y0] = pts[i], [x1, y1] = pts[i + 1]; const steps = Math.max(1, Math.ceil(Math.hypot(x1 - x0, y1 - y0) * 3));
    for (let s = 0; s <= steps; s++) {
      const t = (i + s / steps) / (n - 1), x = x0 + (x1 - x0) * s / steps, y = y0 + (y1 - y0) * s / steps, r = typeof rr === 'function' ? rr(t) : rr;
      m.union(ell(x, y, r, r));
    }
  }
  return m;
}
const ring = (cx, cy, ro, ri, ryo = ro, ryi = ri) => ell(cx, cy, ro, ryo).minus(ell(cx, cy, ri, ryi));

if (typeof module !== 'undefined') module.exports = { WL, spans, bladeMask, bladeSh, wrapSh, bandsSh, ball, mirrorX, stroke, ring };
