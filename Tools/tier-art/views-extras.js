// EIGHT DIRECTIONS - MOCKS FOR THE GEAR STILL HIDDEN IN THE NEW VIEWS (RIG.md, phase 3 "not done"): scarves,
// accessories, arm pieces. Each piece is defined ONCE as 3D shapes in the body's frame (views.js's frame: x the
// character's left, y up from the soles, z forward), adapted from its own approved flat art (DemoGear.cs scarves,
// accessories.js, arms.js) at the same density, and rendered in the five views THROUGH THE ARENA TILT, as
// bake8.js render() does (SHEAR 0.45: a point toward the camera sits lower). What is not symmetric also renders
// facing LEFT: the mirror-image model, its picture flipped (what the game shows), in front 3/4, side, back 3/4.
// A piece that rests on the body hugs WHATEVER IS WORN under it (the wearer's own surface), so the same shape sits
// on the bare suit and on a cuirass.
// node:
//   { cat body.js kit.js sets.js; sed -n 72,189p bake.js; cat accessories.js arms.js; sed '/^\/\/ -* sheet/,$d' views.js; cat views-extras.js; } \
//     | node - <outdir> [scarves|accessories|arms]   (or acc:<id,...> / arm:<id,...>)
// One colourway per item (the user's call): colour variants share the shape and are only listed on the review page.
const XSHEAR = 0.45, XY1 = 196, XY0 = -12;
let XHALF = 52;

// ---------------------------------------------------------------- render (bake8.js render(), returning compose()-able layers)
function xrender(prims, yawDeg) {
  const a = yawDeg * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
  const toBody = (vx, vy, vz) => v3(vx * ca - vz * sa, vy, vx * sa + vz * ca);
  const toView = n => v3(n.x * ca + n.z * sa, n.y, -n.x * sa + n.z * ca);
  const hits = new Map();
  for (let row = 0; row < XY1 - XY0; row++) for (let col = -XHALF; col <= XHALF; col++) {
    const sy = XY1 - row - 0.5, vx = col + 0.5;
    let t = 0, hit = null;
    while (t < 140) {
      const vz = 70 - t, p = toBody(vx, sy + vz * XSHEAR, vz);
      let best = 1e9, bp = null;
      for (const pr of prims) { const d = pr.sdf(p); if (d < best) { best = d; bp = pr; } }
      if (best < 0.02) { hit = { prim: bp, p, vz }; break; }
      t += Math.max(0.2, best * 0.8);
    }
    if (!hit) continue;
    const e = 0.25, pr = hit.prim, p = hit.p;
    const n0 = v3(pr.sdf(v3(p.x + e, p.y, p.z)) - pr.sdf(v3(p.x - e, p.y, p.z)), pr.sdf(v3(p.x, p.y + e, p.z)) - pr.sdf(v3(p.x, p.y - e, p.z)), pr.sdf(v3(p.x, p.y, p.z + e)) - pr.sdf(v3(p.x, p.y, p.z - e)));
    const nl = len3(n0) || 1, nb = v3(n0.x / nl, n0.y / nl, n0.z / nl);
    hits.set(K(col, row), { prim: pr, z: hit.vz, color: pr.color(p, toView(nb), nb) });
  }
  // splatted pieces (the arm pieces: the flat art lathed into texel-sized voxels): each voxel's sample points
  // projected into this view, nearest wins against the ray-marched figure
  for (const sp of (prims.splats || [])) for (const v of sp.pts) for (const o of SPLAT_SUB) {
    const p = v3(v.p.x + o[0], v.p.y + o[1], v.p.z + o[2]), vx = p.x * ca + p.z * sa, vz = -p.x * sa + p.z * ca, sy = p.y - vz * XSHEAR;
    const col = Math.floor(vx), row = Math.floor(XY1 - sy); if (row < 0 || row >= XY1 - XY0 || col < -XHALF || col > XHALF) continue;
    const k = K(col, row), h = hits.get(k);
    if (h && h.z >= vz) continue;
    hits.set(k, { prim: sp.prim, z: vz, color: v.color });
  }
  const layers = new Map();
  for (const [k, h] of hits) {
    const id = h.prim.piece;
    if (!layers.has(id)) { const l = new Layer(0); l.outR = h.prim.outR; if (!h.prim.outR) l.outline = false; l.zs = []; layers.set(id, l); }
    const l = layers.get(id); l.c.set(k, h.color); l.zs.push(h.z);
  }
  for (const l of layers.values()) l.depth = l.zs.reduce((s, z) => s + z, 0) / l.zs.length + (l.bias || 0);
  // a piece may ask to sort as if nearer or farther than its mean depth (a strap round the body's mean is behind it)
  for (const [id, l] of layers) { const pr = prims.find(q => q.piece === id); if (pr && pr.depthBias) l.depth += pr.depthBias; if (pr && pr.alpha) l.alpha = pr.alpha; }
  return [...layers.values()];
}
const xmx = p => v3(-p.x, p.y, p.z);
const SPLAT_SUB = []; for (const a of [-0.34, 0, 0.34]) for (const b of [-0.34, 0, 0.34]) for (const c of [-0.34, 0, 0.34]) SPLAT_SUB.push([a, b, c]);
const xmirrored = pr => ({ ...pr, sdf: p => pr.sdf(xmx(p)), color: (p, n, nb) => pr.color(xmx(p), n, nb && xmx(nb)) });
const flipGrid = g => { const o = new Map(); for (const [k, c] of g) o.set(K(-KX(k) - 1, KY(k)), c); return o; };
// a bounded shape: outside its box (plus a margin) the box's distance is a safe step
const sdBox3 = (p, lo, hi) => { const dx = Math.max(lo.x - p.x, p.x - hi.x, 0), dy = Math.max(lo.y - p.y, p.y - hi.y, 0), dz = Math.max(lo.z - p.z, p.z - hi.z, 0); return Math.hypot(dx, dy, dz); };
const bounded = (lo, hi, f) => p => { const d = sdBox3(p, lo, hi); return d > 1.5 ? d : f(p); };

// ---------------------------------------------------------------- the wearer's surface
// The pieces an accessory rests on (torso and what covers it): their union's distance. `withCape` adds the cape.
const TORSO_PIECES = ['torso', 'cuirass', 'belt', 'bib'];
function wearerShell(prims, withCape) {
  const list = prims.filter(p => TORSO_PIECES.includes(p.piece) || (withCape && p.piece === 'cape'));
  return p => { let d = 1e9; for (const q of list) d = Math.min(d, q.sdf(p)); return d; };
}
// the surface's z at (x, y), marching in from the front (dir 1) or the back (dir -1); null if the ray misses
function surfZ(shell, x, y, dir) {
  let z = dir * 40;
  for (let i = 0; i < 200; i++) { const d = shell(v3(x, y, z)); if (d < 0.05) return z; z -= dir * Math.max(0.1, d * 0.8); if (dir * z < -40) return null; }
  return null;
}
// a table of surface z per integer (x, y), built once per wearer and side
function surfTable(shell, dir, x0, x1, y0, y1) {
  const t = new Map();
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) t.set(x + ',' + y, surfZ(shell, x, y, dir));
  return (x, y) => {   // bilinear, skipping misses
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi; let s = 0, w = 0;
    for (const [dx, dy, ww] of [[0, 0, (1 - fx) * (1 - fy)], [1, 0, fx * (1 - fy)], [0, 1, (1 - fx) * fy], [1, 1, fx * fy]]) { const v = t.get((xi + dx) + ',' + (yi + dy)); if (v != null) { s += v * ww; w += ww; } }
    return w > 0 ? s / w : null;
  };
}
// a band lying ON the shell: points within `off` +- `th` of the surface, inside the region `inside(p)` (<= 0)
const onShell = (shell, off, th, inside) => p => Math.max(Math.abs(shell(p) - off) - th, inside(p));

// ---------------------------------------------------------------- the wearers
const WEARERS = {
  bare: { name: 'bare', make: () => { POSE.hands = {}; CARDS = []; return body3d(); }, flat: () => Object.values(bodyLayers()), flatBack: () => backSetLayers(null) },
  wayfarer: { name: 'Wayfarer', make: () => { POSE.hands = {}; CARDS = []; return [...body3d(), ...wayfarer3d()]; }, flat: () => wear(SETS.find(s => s.id === 'wayfarer')), flatBack: () => backSetLayers(SETS.find(s => s.id === 'wayfarer')) },
  athanor: { name: 'Athanor', make: () => { POSE.hands = {}; CARDS = []; return [...body3d(), ...athanor3d()]; }, flat: () => wear(SETS.find(s => s.id === 'athanor')), flatBack: () => backSetLayers(SETS.find(s => s.id === 'athanor')) },
  hoplite: { name: 'Hoplite', make: () => { POSE.hands = {}; CARDS = []; return [...body3d(), ...hoplite3d()]; }, flat: () => wear(SETS.find(s => s.id === 'hoplite')), flatBack: () => backSetLayers(SETS.find(s => s.id === 'hoplite')) },
};

// ================================================================ 1. SCARVES
// DemoGear.cs: ScarfCollarRows (10 x 3 doubled: glow, light, then the narrower base - the knot; top edge on the chin)
// and ScarfTailRowsData (18 rows doubled: 7 wide narrowing to 1 by t^1.4, drifting 3 to the +x side by t^2, dark
// for the top 65 % then deep), the tail behind the body, its knot 3.5 cells under the collar's centre, 2 cells to +x.
const SCARF = {
  scarlet: ramp('#BD2930'), azure: ramp('#336BBD'), verdant: ramp('#388F4D'), amber: ramp('#D18C29'),
  ashen: ramp('#45454D', { lift: 0.24 }),
};
// the flat art, as the game places it (kit grid: the chin on row 29, the body's centre x 19)
function scarfFlat(r, back) {
  const collar = L(8.9), tail = L(back ? 2.9 : 0.4);
  const span = (l, c, half, y0, col) => { for (let x = c - half; x <= c + half; x++) for (const dy of [0, 1]) for (const dx of [0, 1]) l.set(9 + x * 2 + dx, y0 + dy, col); };
  if (!back) { span(collar, 4, 4, 29, tone(r, 5)); span(collar, 4, 4, 31, tone(r, 4)); span(collar, 4, 3, 33, tone(r, 3)); }
  for (let i = 0; i < 18; i++) {
    const t = i / 17, half = Math.max(0, Math.round(3 * (1 - Math.pow(t, 1.4)))), drift = Math.round(3 * t * t);
    // the tail's grid sits 2 cells to +x (screen right in the front art; from behind the game mirrors nothing)
    const ys = 39 + i * 2;
    for (let x = 4 + drift - half; x <= 4 + drift + half; x++) for (const dy of [0, 1]) for (const dx of [0, 1]) tail.set(13 + x * 2 + dx, ys + dy, tone(r, t < 0.65 ? 2 : 1));
  }
  return [collar, tail];
}
// 3D, REVISED (the user's answers, 2026-11-11: "over the cape, and VERY free-flowing"; a thick wrapped collar high round
// the neck, a long tail - about the body's length - that hangs with a gentle curve standing and streams out behind in loose
// waves when moving, the most animated thing on the figure). The approved scarf's MATERIAL is kept: its ramp, the
// collar's banding (glow on top, light, base below), the tail in the dark tones, outline 4. Only the length and the flow
// change, as asked.
// The collar: a thick roll (9 tall, 7 deep) round the neck, its top up under the chin, a little lower at the front.
// The tail: a ribbon from the back of the collar, 80 long (the flat tail's 36 was "short on purpose" for the flat rig;
// the user asked for about the body's length), 14 wide with a slight swell and pinch, tapering over its last quarter to
// 2, drifting a little to +x (the flat tail's side). Over the cape in every view: it never sinks into the cape or body.
// o.gait: 'stand' | 'walk' | 'run'; o.phase 0..1 through one loop. The tail turns back from hanging by the gait's lift
// (stand 3, walk 38, run 68 degrees at the knot, the hem lagging up to 45 % more), and a wave travels down it toward the
// hem (stand: a slow drift; walk and run: bigger, quicker, two waves along it).
const SCARF_GAITS = {
  stand: { lift: 3, amp: 1.4, cycles: 1, lambda: 70, side: 1.2 },
  walk: { lift: 38, amp: 6.0, cycles: 1, lambda: 42, side: 2.6 },
  run: { lift: 68, amp: 8.5, cycles: 2, lambda: 34, side: 3.4 },
};
function scarf3d(r, wearerPrims, o = {}) {
  const g = SCARF_GAITS[o.gait || 'stand'], ph = (o.phase || 0) * Math.PI * 2;
  const RX = 9.4, RZ = 9.8, YC = 102, TR = 3.6, TY = 4.5, DIP = 3;   // its front stays under the face (the head sprite draws over it in the game anyway)
  const ycAt = p => YC - Math.max(0, p.z / RZ) ** 2 * DIP;
  const collarSdf = p => {
    const e = Math.pow(Math.pow(Math.abs(p.x / RX), 4) + Math.pow(Math.abs(p.z / RZ), 4), 0.25) || 1e-6, rr = Math.hypot(p.x, p.z) / e;
    return (Math.hypot((e - 1) * rr / TR, (p.y - ycAt(p)) / TY) - 1) * Math.min(TR, TY);
  };
  const collar = { piece: 'scarfCollar', outR: 4, sdf: bounded(v3(-15, 95, -16), v3(15, 109, 16), collarSdf),
    color: (p, n) => { const yc = ycAt(p), b = p.y > yc + 1.5 ? 5 : p.y > yc - 1.5 ? 4 : 3, l = litTone(n.x, -n.y, n.z); return tone(r, b - (l <= 1 ? 2 : l <= 2 ? 1 : 0)); } };
  // the ribbon's centre line: from the knot (in the collar's back) down and out behind
  const shell = wearerShell(wearerPrims, false), cape = wearerPrims.filter(p => p.piece === 'cape'), legs = wearerPrims.filter(p => /^(leg|greave|sandal|boot|skirt)/.test(p.piece));
  // (the cape's distance is only true between its top and hem: outside them it ignores depth)
  const behind = p => { let d = shell(p); for (const q of legs) d = Math.min(d, q.sdf(p)); if (p.y > 31 && p.y < 103) for (const q of cape) d = Math.min(d, q.sdf(p)); return d; };
  const N = 40, LEN = 80, step = LEN / N, knot = v3(2, 101, -RZ + 0.5), pts = [];
  let P = knot;
  for (let i = 0; i <= N; i++) {
    const s = i * step, t = s / LEN;
    if (i > 0) {
      // the direction down the ribbon: hanging, turned back by the lift (more toward the hem), waved
      const lift = (g.lift + (g.lift > 10 ? 0.45 * g.lift * t : 0)) * Math.PI / 180;
      const wave = g.amp * Math.pow(t, 1.1) * Math.sin(Math.PI * 2 * s / g.lambda - ph * g.cycles) / 10;
      const a = Math.min(lift, 85 * Math.PI / 180) + wave, sideA = g.side * Math.pow(t, 1.2) * Math.sin(Math.PI * 2 * s / (g.lambda * 1.3) - ph * g.cycles + 1.1) / 12;
      let d = v3(Math.sin(sideA) + 0.035, -Math.cos(a), -Math.sin(a)); d = norm3(d);
      let Q = v3(P.x + d.x * step, P.y + d.y * step, P.z + d.z * step);
      // over the cape and clear of the body and legs: pushed back out until it is 1.1 behind them
      for (let k = 0; k < 30 && behind(Q) < 1.1; k++) Q = v3(Q.x, Q.y, Q.z - 0.4);
      P = Q;
    }
    const w = t < 0.75 ? 14 * (1 + 0.08 * Math.sin(s * 0.13 + 0.6)) : 14 - 12 * Math.pow((t - 0.75) / 0.25, 1.2);
    pts.push({ ...P, w, t });
  }
  let lo = v3(1e9, 1e9, 1e9), hi = v3(-1e9, -1e9, -1e9);
  for (const q of pts) { lo = v3(Math.min(lo.x, q.x - q.w / 2 - 1), Math.min(lo.y, q.y - 2), Math.min(lo.z, q.z - 2)); hi = v3(Math.max(hi.x, q.x + q.w / 2 + 1), Math.max(hi.y, q.y + 2), Math.max(hi.z, q.z + 2)); }
  const tailSdf = p => {   // nearest segment in the y-z plane, then the ribbon's width across x
    let best = 1e9, bt = 0;
    for (let i = 0; i < N; i++) {
      const A = pts[i], B = pts[i + 1], dy = B.y - A.y, dz = B.z - A.z, L2 = dy * dy + dz * dz || 1e-6;
      const h = Math.max(0, Math.min(1, ((p.y - A.y) * dy + (p.z - A.z) * dz) / L2));
      const d = Math.hypot(p.y - (A.y + dy * h), p.z - (A.z + dz * h));
      if (d < best) { best = d; bt = i + h; }
    }
    const i0 = Math.min(N - 1, Math.floor(bt)), f = bt - i0, A = pts[i0], B = pts[i0 + 1];
    const cx = A.x + (B.x - A.x) * f, hw = (A.w + (B.w - A.w) * f) / 2;
    return Math.max(best - 0.8, Math.abs(p.x - cx) - hw, -bt, bt - N);
  };
  const tail = { piece: 'scarfTail', outR: 4, sdf: bounded(lo, hi, tailSdf),
    color: (p, n) => { const l = litTone(n.x, -n.y, n.z); return tone(r, Math.min(4, Math.max(1, l))); } };
  return [collar, tail];
}

// ================================================================ 2. ACCESSORIES (accessories.js, round 2 - the approved mock)
// Front-art texel (ax, row) -> the body frame: x = ax + 0.5 - 19 (the front view looks at the character, its left on
// screen right), height Y(row) = 135 - row (views.js's rule above the hips). The back art is mirrored: x = 19 - ax - 0.5.
const FX = ax => ax + 0.5 - 19, BX = ax => 19 - ax - 0.5;
const ACC_ST = { t: 0.4, walk: false, el: 'Water', charges: 3, anim: null, cracked: [], run: 0.4, pal: null, dash: 0, relic: 'off' };
const nrm2 = (x, y) => { const l = Math.hypot(x, y) || 1; return [x / l, y / l]; };
// shade a flat-art tone by the light: a tone up where the surface faces the light, down where it turns away
const shadeBy = (n, base) => { const l = litTone(n.x, -n.y, n.z); return base + (l >= 5 ? 1 : l <= 1 ? -1 : 0); };
// a strap lying on the wearer: the shell's band within w/2 of the plane through A and B (both in x-y; the plane is
// upright in z, so the strap runs over the front and round the back alike), the strap's own shading from strapShade:
// its upper edge lit, the lower dark, a stitch every 4
function strapOn(shell, A, B, w, r, extra = () => -1, piece = 'strap', outR = 4) {
  const [dx, dy] = nrm2(B[0] - A[0], B[1] - A[1]), nx = dy, ny = -dx;            // the plane's normal (in x-y)
  const dist = p => (p.x - A[0]) * nx + (p.y - A[1]) * ny;
  return { piece, outR, sdf: onShell(shell, 1.0, 0.9, p => Math.max(Math.abs(dist(p)) - w / 2, extra(p))),
    color: (p, n) => {
      const v = dist(p) / (w / 2), u = (p.x - A[0]) * dx + (p.y - A[1]) * dy, up = ny < 0 ? v > 0.45 : v < -0.45, dn = ny < 0 ? v < -0.45 : v > 0.45;
      if (up) return tone(r, shadeBy(n, 4) > 4 ? 4 : shadeBy(n, 4)); if (dn) return tone(r, Math.min(2, shadeBy(n, 2)));
      return tone(r, shadeBy(n, ((Math.floor(u) % 4) + 4) % 4 === 0 ? 2 : 3) - (shadeBy(n, 3) > 3 ? 1 : 0));
    } };
}
// a phial (accessories.js phial(): 4 wide - cork 2 rows, neck 1, body 7, corners cut; the lower 5 rows of the body
// liquid when full), standing upright with its body's bottom at yb, centred on (x, z)
function phial3d(x, yb, z, st, idx) {
  const liq = liquid(st), cyl = (y0, y1, r) => p => sdTube(p, x, z, y0, y1, r, r, 0.6);
  const glassS = cyl(yb, yb + 7, 2.05), neckS = cyl(yb + 7, yb + 8.2, 1.1), corkS = cyl(yb + 8, yb + 10, 1.1);
  const ring = p => Math.max(Math.abs(Math.hypot(p.x - x, p.z - z) - 2.35) - 0.45, Math.abs(p.y - (yb + 5.5)) - 0.5);
  return [
    { piece: 'phial' + idx, outR: 1, sdf: bounded(v3(x - 3, yb - 1, z - 3), v3(x + 3, yb + 11, z + 3), p => Math.min(glassS(p), neckS(p), corkS(p))),
      color: (p, n) => {
        const l = litTone(n.x, -n.y, n.z);
        if (p.y > yb + 8) return tone(AM.cork, p.y > yb + 9.3 ? 4 : l >= 4 ? 3 : 2);
        if (p.y < yb + 5 && p.y > yb) return tone(liq, p.y > yb + 4 ? 5 : l >= 4 ? 4 : l <= 2 ? 2 : 3);
        return tone(AM.glass, l >= 5 ? 5 : l >= 4 ? 4 : l <= 2 ? 1 : 2);
      } },
    // the loop that holds it: a leather band round the glass
    { piece: 'phialLoop' + idx, outR: 1, sdf: bounded(v3(x - 4, yb + 4, z - 4), v3(x + 4, yb + 7, z + 4), ring), color: (p, n) => tone(M.leather, shadeBy(n, 3)) },
  ];
}
// the wearer's front / back surface at (x, y)
const frontZ = (shell, x, y) => surfZ(shell, x, y, 1), backZ = (shell, x, y) => surfZ(shell, x, y, -1);

// ---------------------------------------------------------------- 1. BANDOLIER (under the cape)
// accessories.js: a 4-wide dark strap from the left shoulder (31, 31) to the right hip (10, 63), three phials on it at
// 0.3 / 0.5 / 0.7 of the way, each in a leather loop; from behind the strap alone, a bronze buckle at the hip end.
function bandolier3d(w, st = ACC_ST) {
  const shell = wearerShell(w, false), A = [FX(31), Y(31)], B = [FX(10), Y(63)];
  const out = [strapOn(shell, A, B, 4, M.leatherDk, p => 60 - p.y, 'bandolier')];
  [0.30, 0.50, 0.70].forEach((s, i) => {
    const px = 31 + (10 - 31) * s, py = 31 + 32 * s, cx = Math.round(px), top = Math.round(py) - 4;
    const x = cx - 19, yb = Y(top + 9) - 0.5, zs = frontZ(shell, x, yb + 5) ?? 9;
    out.push(...phial3d(x, yb, zs + 1.9 + 1.6, st, i));
  });
  const bx = BX(26.5), by = Y(61.5), bz = backZ(shell, bx, by) ?? -9;
  out.push({ piece: 'buckle', outR: 4, sdf: p => sdRoundBox(p, v3(bx, by, bz - 1.6), v3(2, 2, 0.8), 0.3), color: (p, n) => tone(M.bronze, p.y > by + 1.2 ? 5 : shadeBy(n, 3)) });
  return out;
}

// ---------------------------------------------------------------- 4. VIAL HARNESS (under the cape)
// accessories.js: two 3-wide straps crossed on the chest, shoulder to opposite hip, a round glass flask on the sternum in a
// brass cage (a band round its waist, a collar at the neck), corked; from behind the straps cross under a bronze boss.
function harness3d(w, st = ACC_ST) {
  const shell = wearerShell(w, false), liq = liquid(st);
  const out = [-1, 1].map(s => strapOn(shell, [s * 10.5, Y(31)], [-s * 11.5, Y(62)], 3, M.leather, p => 60 - p.y, 'harness' + (s > 0 ? 'L' : 'R')));
  const cx = 0.5, cy = Y(47), R = 5, zf = (frontZ(shell, cx, cy) ?? 9) + 1.8 + R - 1.2;
  const bulb = p => sdEllipsoid(p, v3(cx, cy, zf), v3(R, R, R));
  const neck = p => sdTube(p, cx, zf, cy + R - 1, cy + R + 2.5, 1.05, 1.05, 0.4), collarS = p => sdTube(p, cx, zf, cy + R + 0.6, cy + R + 1.6, 2.05, 2.05, 0.4);
  const cork = p => sdTube(p, cx, zf, cy + R + 2.5, cy + R + 4.5, 1.05, 1.05, 0.5);
  out.push({ piece: 'flask', outR: 4, sdf: bounded(v3(cx - 6, cy - 6, zf - 6), v3(cx + 6, cy + R + 5, zf + 6), p => Math.min(bulb(p), neck(p), collarS(p), cork(p))),
    color: (p, n) => {
      const l = litTone(n.x, -n.y, n.z);
      if (p.y > cy + R + 2.5) return tone(AM.cork, p.y > cy + R + 3.6 ? 4 : 3);
      if (collarS(p) < 0.15) return tone(M.bronze, l >= 4 ? 5 : 2);
      if (p.y > cy + R - 1) return tone(AM.glass, l >= 4 ? 4 : 1);
      if (Math.abs(p.y - cy) < 0.55) return tone(M.bronze, l >= 3 ? 5 : 2);              // the cage's band round the waist
      const rim = Math.abs(n.z) < 0.45 && Math.hypot(n.x, n.y) > 0.9;                     // the glass at the silhouette
      if (rim || p.y > cy + R - 1.6) return tone(AM.glass, l >= 4 ? 4 : l <= 2 ? 1 : 2);
      if (l >= 5 && p.y > cy + 1) return tone(AM.glass, 5);
      return tone(liq, p.y > cy + R - 2.4 ? 5 : l >= 4 ? 4 : l <= 2 ? 2 : 3);
    } });
  const bz = backZ(shell, 0, Y(46)) ?? -9;
  out.push({ piece: 'boss', outR: 4, sdf: p => sdEllipsoid(p, v3(0, Y(46), bz - 0.6), v3(2.6, 2.6, 1.6)), color: (p, n) => tone(M.bronze, shadeBy(n, 3) + (litTone(n.x, -n.y, n.z) >= 4 ? 1 : 0)) });
  return out;
}

// ---------------------------------------------------------------- 5. FEATHER COLLAR (over the cape and pauldrons)
// accessories.js shingles(): feathers 5.5 x 2.7 (half), 5 apart, rows shingled down from the shoulder line, each row's
// roots under the row above, tips hanging down and leaning out from the centre. Front: a piece on each shoulder, two
// rows, the chest open between them. Back: one band shoulder to shoulder, three rows, longest at the spine, raised to
// under the head. In 3D one mantle: roots on the wearer's surface (over pauldrons and cape), the front open, two rows
// in front turning to three behind, the length growing toward the spine.
function collar3d(w, st = ACC_ST) {
  const pal = FEATHERS[st.pal || 'Crow'], r = pal.r;
  // ONE shape for every Bronze set (the user's call, 2026-11-11: it sits ON TOP of the pauldrons, the bare body is not worn):
  // it drapes over the wearer's torso and cape AND the three Bronze sets' pauldrons together, so no plate shows through
  POSE.hands = {}; CARDS = [];
  const bronzePauldrons = [hoplite3d, wayfarer3d, athanor3d].flatMap(make => make().filter(p => /^pauldron/.test(p.piece)));
  const shell = (() => { const list = [...w.filter(p => ['torso', 'cuirass', 'bib', 'cape', 'armL', 'armR', 'back'].includes(p.piece)), ...bronzePauldrons]; return p => { let d = 1e9; for (const q of list) d = Math.min(d, q.sdf(p)); return d; }; })();
  const feathers = [];
  const topAt = th => { const x = Math.cos(th) * 15.5, z = Math.sin(th) * 9; for (let y = 122; y > 90; y -= 0.25) if (shell(v3(x, y, z)) < 0.3) return y; return 0; };
  const rootAt = (th, y) => {   // march in from outside along the azimuth to the wearer's surface
    const cx = Math.cos(th), cz = Math.sin(th);
    for (let rr = 34; rr > 2; rr -= 0.25) { const p = v3(cx * rr, y, cz * rr); if (shell(p) < 0.4) return { p, rr }; }
    return null;
  };
  // rows from the hem up; row -1 is the cap over the shoulder: rooted at the neck, above the plate, laid over its top
  for (let row = 2; row >= -1; row--) {
    // 5 apart ALONG THE WEARER (the flat art's spacing): out over a wide plate there are more of them than at the neck
    const stagger = (row + 2) % 2 ? 2.5 : 0, ths = []; let acc = stagger, prev = null;
    for (let j = 0; j < 720; j++) { const th = j / 720 * Math.PI * 2, top = topAt(th), y = row < 0 ? top + 1.5 : Math.min(Math.max(104.5, top - 1) - Math.max(0, row) * 5, top - 1.5);
      const h = row < 0 ? { p: v3(Math.cos(th) * 10, 0, Math.sin(th) * 8.5) } : (rootAt(th, y) || { p: v3(Math.cos(th) * 12, 0, Math.sin(th) * 9) });
      if (prev) acc += Math.hypot(h.p.x - prev.x, h.p.z - prev.z); prev = h.p; if (acc >= 4.6) { acc -= 4.6; ths.push(th); } }
    for (const th of ths) {
      const front = Math.sin(th), side = Math.cos(th);
      if (front > 0.5 && Math.abs(side) < 0.6) continue;                    // the chest left open
      const back = Math.max(0, -front), rows = front > 0.15 ? 2 : 3;
      if (row >= rows) continue;
      const k = (front > 0 ? 1 - 0.12 * (1 - Math.abs(side)) : 0.85 + 0.32 * back * back), ry = 5.5 * k;
      const top = topAt(th), yTop = Math.max(104.5 + 3.5 * Math.pow(back, 1.5), top - 1), yRoot = row < 0 ? Math.max(yTop + 2, top + 1.5) : yTop - row * 5 * k;
      const hit = row < 0 ? { p: v3(Math.cos(th) * 10, yRoot, Math.sin(th) * 8.5) } : (rootAt(th, Math.min(yRoot, yTop - 0.5)) || rootAt(th, 104));
      if (!hit) continue;
      const out = v3(Math.cos(th), 0, Math.sin(th)); const SPLAY = 0.42 + 0.4 * Math.max(0, front) * Math.abs(side) + 0.35 * side * side;
      const lift = row < 0 ? 0.8 : 1.2 + (rows - 1 - row) * 0.9;
      const root = v3(hit.p.x + out.x * lift, yRoot, hit.p.z + out.z * lift);
      const tg = v3(-Math.sin(th), 0, Math.cos(th));
      const lean = Math.max(-0.55, Math.min(0.55, side * 0.6)) * (side * tg.x >= 0 ? 1 : -1) * Math.sign(-tg.x || 1);
      // follow the wearer down: toward where the surface is 1.2 feather-lengths lower (a dome is draped, not pierced);
      // the cap row falls toward the plate's top at mid-distance, lying on it
      const below = rootAt(th, yRoot - ry * 1.2), here = row < 0 ? 10 : Math.hypot(hit.p.x, hit.p.z);
      const fall = below ? Math.max(SPLAY, (Math.hypot(below.p.x, below.p.z) - here + (row < 0 ? 1.5 : 0.6)) / (ry * 1.2)) : SPLAY;
      let u = v3(out.x * fall + tg.x * lean * 0.6, -1, out.z * fall + tg.z * lean * 0.6); u = norm3(u);
      const nv = norm3(cross3(tg, u)), tv = norm3(cross3(u, nv));
      const c = v3(root.x + u.x * ry * 0.85, root.y + u.y * ry * 0.85, root.z + u.z * ry * 0.85);
      // keep it over the plate: pushed out along its normal until clear of the wearer
      let cc = c; for (let t = 0; t < 12 && shell(cc) < 1.2; t++) cc = v3(cc.x + out.x * 0.5, cc.y + 0.2, cc.z + out.z * 0.5);
      feathers.push({ c: cc, u, tv, nv, ry, last: row === rows - 1, lo: v3(cc.x - ry - 1, cc.y - ry - 1, cc.z - ry - 1), hi: v3(cc.x + ry + 1, cc.y + ry + 1, cc.z + ry + 1) });
    }
  }
  const local = (f, p) => { const d = v3(p.x - f.c.x, p.y - f.c.y, p.z - f.c.z); return [d.x * f.tv.x + d.y * f.tv.y + d.z * f.tv.z, d.x * f.u.x + d.y * f.u.y + d.z * f.u.z, d.x * f.nv.x + d.y * f.nv.y + d.z * f.nv.z]; };
  const one = (f, p) => { const [a, b, c] = local(f, p); return sdEllipsoid(v3(a, b, c), v3(0, 0, 0), v3(2.7, f.ry, 1.0)); };
  let lo = v3(1e9, 1e9, 1e9), hi = v3(-1e9, -1e9, -1e9); for (const f of feathers) { lo = v3(Math.min(lo.x, f.lo.x), Math.min(lo.y, f.lo.y), Math.min(lo.z, f.lo.z)); hi = v3(Math.max(hi.x, f.hi.x), Math.max(hi.y, f.hi.y), Math.max(hi.z, f.hi.z)); }
  const nearest = p => { let best = 1e9, bf = null; for (const f of feathers) { const d = sdBox3(p, f.lo, f.hi); if (d > best) continue; const e = one(f, p); if (e < best) { best = e; bf = f; } } return [best, bf]; };
  return [{ piece: 'featherCollar', outR: 4, sdf: bounded(lo, hi, p => nearest(p)[0]),
    color: (p, n) => {
      const [, f] = nearest(p); const [a, b] = local(f, p), uu = b / f.ry, vv = a / 2.7, l = litTone(n.x, -n.y, n.z);
      if (uu > 0.82 || Math.abs(vv) > 0.86 && uu > -0.2) return tone(r, 0);                    // the seam round its lower half
      if (pal.eye && f.last && uu > 0.3) return uu > 0.62 && Math.abs(vv) < 0.45 ? tone(M.navy, 2) : uu > 0.48 ? tone(M.gold, 4) : tone(M.teal, 4);
      if (pal.tip && f.last && uu > 0.25) return tone(pal.tip, l >= 4 ? 5 : l <= 2 ? 3 : 4);
      if (Math.abs(vv) < 0.22 && uu < 0.6) return tone(r, 2);                                  // the quill
      if (vv < -0.25 && l >= 2 || l >= 5) return tone(r, uu > 0.5 && l >= 3 ? 5 : 4);                                            // the lit vane
      return tone(r, vv > 0.5 || l <= 1 ? 2 : 3);
    } }];
}

// ---------------------------------------------------------------- 6. BANNER POLE (under the cape; what stands above the shoulders over the head)
// accessories.js: a 2-wide wooden pole behind the far shoulder from the back of the hip (row 58) to row -40, a bronze
// spearhead finial over it, a crossbar out to the side at the top, a 12 x 27 banner hung from it, swallow-tailed, its free
// edge rippling, a gold circumpunct and a gold stripe; on the back two bronze collars and straps to the other shoulder
// and hip. The pole stands at x 14 (the flat art's 15.5-16.5, pulled in a little to stay under the cape's sides).
function banner3d(w, st = ACC_ST) {
  const r = CLOTH[st.pal || 'Crimson'], shell = wearerShell(w, false), PX = 14, PZ = -8.4;
  const out = [];
  out.push({ piece: 'pole', outR: 4, sdf: p => Math.min(sdTube(p, PX, PZ, Y(58), Y(-40), 1, 1, 0.3), sdCapsule(p, v3(PX, Y(-38.5), PZ), v3(PX + 14, Y(-38.5), PZ), 1)),
    color: (p, n) => tone(AM.wood, litTone(n.x, -n.y, n.z) >= 4 ? 4 : 2) });
  // the spearhead: a bronze double cone, widest 5 across at row -44
  out.push({ piece: 'finial', outR: 4, sdf: p => { const y = p.y, rr = y > Y(-44) ? 2.5 * (Y(-49) - y) / (Y(-49) - Y(-44)) : 2.5 * (y - Y(-40)) / (Y(-44) - Y(-40)); return Math.max(Math.hypot(p.x - PX, p.z - PZ) - Math.max(0.05, rr), Y(-40) - y, y - Y(-49)); },
    color: (p, n) => tone(M.bronze, litTone(n.x, -n.y, n.z) >= 3 ? 5 : 2) });
  for (const row of [33, 53]) out.push({ piece: 'collar' + row, outR: 4, sdf: p => sdTube(p, PX, PZ, Y(row + 2), Y(row), 2, 2, 0.4), color: (p, n) => tone(M.bronze, p.y > Y(row) - 0.8 ? 5 : 2) });
  // the straps from the pole round the back to the other shoulder and the other hip
  const seg = (A, B) => p => { const dx = B[0] - A[0], dy = B[1] - A[1], L2 = dx * dx + dy * dy, h = Math.max(0, Math.min(1, ((p.x - A[0]) * dx + (p.y - A[1]) * dy) / L2)); return Math.hypot(p.x - A[0] - dx * h, p.y - A[1] - dy * h); };
  const s1 = seg([PX, Y(34.5)], [BX(33), Y(30.5)]), s2 = seg([PX, Y(54.5)], [BX(30), Y(60.5)]);
  out.push({ piece: 'poleStraps', outR: 4, sdf: onShell(shell, 1.0, 0.9, p => Math.max(Math.min(s1(p), s2(p)) - 1.5, p.z + 2)), color: (p, n) => tone(M.leatherDk, shadeBy(n, 3)) });
  // the banner: hung from the crossbar at row -37, 12 wide out from the pole, 27 tall; the last 5 rows notched
  const x0 = PX + 2, top = Y(-37), H = 27;
  const ripple = y => Math.round(Math.sin(st.t * 3.4 - (top - y) * 0.32) * 1.0);
  out.push({ piece: 'banner', outR: 4, sdf: p => {
      const i = p.x - x0, dyy = top - p.y, ext = 12 + (dyy > 3 ? ripple(p.y) : 0), notch = dyy > H - 5 ? dyy - (H - 5) : -1;
      let d = Math.max(-i, i - ext, -dyy, dyy - H, Math.abs(p.z - PZ) - 0.6);
      if (notch >= 0) d = Math.max(d, notch - Math.abs(i - 6));
      return d;
    },
    color: (p, n) => {
      const i = Math.floor(p.x - x0), y = top - p.y, gx = 6, gy = 11, d = Math.hypot(p.x - x0 - gx, y - gy);
      if (d > 3.2 && d < 4.4) return tone(M.gold, 4);
      if (d < 1.2) return tone(M.gold, 5);
      if (Math.abs(y - (H - 9) - 0.5) < 0.5) return tone(M.gold, 3);
      const sl = Math.sin(st.t * 3.4 - y * 0.32 + 1.2);
      return tone(r, i === 0 ? 2 : i === 1 ? 4 : sl > 0.4 ? 4 : sl < -0.4 ? 2 : 3);
    } });
  return out;
}

// ---------------------------------------------------------------- 8. HOURGLASS PACK (over the cape and the helm's back)
// accessories.js: an hourglass 20 x 34 between the shoulder blades - bronze plates top and bottom, two posts, two glass
// bulbs, the sand running from the top (the run's progress, 0.4 here) into a mound; a bronze yoke across the shoulders,
// a finial post and ball at each end standing up behind the shoulders; two straps over the shoulders down the chest with
// bronze buckles. In 3D the frame is 20 wide and 14 deep, four posts, the bulbs round.
function hourglass3d(w, st = ACC_ST) {
  const shellC = wearerShell(w, true), shell = wearerShell(w, false), f = st.run;
  const zb = Math.min(backZ(shellC, 0, Y(48)) ?? -9, backZ(shellC, 0, Y(36)) ?? -9), cz = zb - 7.6;
  const top = 32, H = 34, yT = Y(top) , yB = Y(top + H) ;     // the frame from y 103 to 69
  const out = [];
  const frame = p => Math.min(sdRoundBox(p, v3(0, yT - 1.5, cz), v3(10, 1.5, 7), 0.5), sdRoundBox(p, v3(0, yB + 1.5, cz), v3(10, 1.5, 7), 0.5),
      ...[[-8.5, -5.5], [8.5, -5.5], [-8.5, 5.5], [8.5, 5.5]].map(([x, z]) => sdTube(p, x, cz + z, yB + 2, yT - 2, 1, 1, 0.2)));
  const frameColor = (p, n) => { const l = litTone(n.x, -n.y, n.z); if (p.y > yT - 3.2 || p.y < yB + 3.2) return tone(M.bronze, n.y > 0.7 ? 5 : l >= 4 ? 4 : l <= 2 ? 2 : 3); return tone(M.bronzeDk, l >= 4 ? 4 : 2); };
  const tB = v3(0, Y(top + 9.5), cz), bB = v3(0, Y(top + H - 10.5), cz);
  const bulbs = p => Math.min(Math.max(sdEllipsoid(p, tB, v3(6, 7, 6)), p.y - (yT - 3), Y(top + 15) - p.y), Math.max(sdEllipsoid(p, bB, v3(6, 7, 6)), p.y - Y(top + 17), (yB + 3) - p.y), sdTube(p, 0, cz, Y(top + 18), Y(top + 14), 1.05, 1.05, 0.2));
  const sTop = Y(top + 15 - (1 - f) * 11), pile = Y(top + H - 3 - f * 11);
  // one piece, as the flat art is one layer: the frame's outline never cuts into the glass
  out.push({ piece: 'hourglass', outR: 4, depthBias: 12, sdf: bounded(v3(-11, yB - 1, cz - 8), v3(11, yT + 1, cz + 8), p => Math.min(frame(p), bulbs(p))),
    color: (p, n) => {
      if (frame(p) < bulbs(p)) return frameColor(p, n);
      const l = litTone(n.x, -n.y, n.z), dx = Math.abs(p.x);
      if (p.y > Y(top + 16) && p.y < sTop - Math.max(0, 2.2 - dx) * 0.9 && dx < 5.5) return tone(AM.sand, l >= 4 ? 4 : 3);
      if (p.y < Y(top + 16) && p.y < pile - dx * 0.55 && dx < 5.5) return tone(AM.sand, p.y > pile - dx * 0.55 - 1.2 ? 4 : 3);
      if (p.y < Y(top + 16) && p.y > pile && dx < 1.2 && Math.round(p.y) % 2) return tone(AM.sand, 5);   // the falling stream
      return tone(AM.glass, l >= 5 ? 5 : l >= 4 ? 4 : l <= 2 ? 1 : 2);
    } });
  // the yoke across the backs of the shoulders, its finials
  // behind the helm's back too (the flat art draws the yoke over it)
  const helmed = w.filter(p => ['torso', 'cuirass', 'cape', 'helm'].includes(p.piece)), shellH = p => { let d = 1e9; for (const q of helmed) d = Math.min(d, q.sdf(p)); return d; };
  const zy = Math.min(backZ(shellC, 0, Y(30)) ?? -9, backZ(shellH, 0, Y(30) + 1.5) ?? -9) - 1.6;
  out.push({ piece: 'yoke', outR: 4, depthBias: 12, sdf: p => Math.min(sdCapsule(p, v3(-22.5, Y(30), zy), v3(22.5, Y(30), zy), 1.5),
      ...[-1, 1].map(s => Math.min(sdTube(p, s * 22.5, zy, Y(28), Y(16), 1, 1, 0.3), sdEllipsoid(p, v3(s * 22.5, Y(14), zy), v3(3, 3, 3))))),
    color: (p, n) => { const l = litTone(n.x, -n.y, n.z); if (p.y > Y(17) || p.y < Y(28.5)) return tone(M.bronze, l >= 5 ? 5 : l >= 4 ? 4 : l <= 2 ? 2 : 3); return tone(M.bronzeDk, l >= 4 ? 4 : 2); } });
  // the two straps from the yoke over the shoulders and down the chest to their buckles
  out.push({ piece: 'hgStraps', outR: 4, sdf: onShell(shell, 1.0, 0.9, p => Math.max(Math.min(Math.abs(p.x - 9), Math.abs(p.x + 9)) - 1, p.z > 0 ? Y(46) - p.y : Y(31) - p.y)),
    color: (p, n) => tone(M.leatherDk, litTone(n.x, -n.y, n.z) >= 4 ? 4 : 2) });
  for (const s of [-1, 1]) { const by = Y(44.5), bz = (frontZ(shell, s * 9, by) ?? 9) + 1.4;
    out.push({ piece: 'hgBuckle' + (s > 0 ? 'L' : 'R'), outR: 4, sdf: p => sdRoundBox(p, v3(s * 9, by, bz), v3(2, 1, 0.7), 0.2), color: (p, n) => tone(M.bronze, p.y > by ? 5 : 2) }); }
  return out;
}

// ---------------------------------------------------------------- 9. CHAINED TOME (the relic's hip - the far one, +x - under the cape)
// accessories.js tomeLayer(): a steel chain of 8 links from the belt, the book 16 x 19 hanging from it - a dark red cover,
// gold corners, a gold ring sigil with a centre dot and a bronze clasp; the pages show along the fore-edge, the spine dark.
// In 3D the book is 3 thick and hangs beside the far hip, its cover turned 35 degrees from the front toward the side.
function tome3d(w, st = ACC_ST) {
  const shell = wearerShell(w, false), ay = Y(63), ax = 14.2, az = 2.5;
  const PHI = 35 * Math.PI / 180, tg = v3(Math.cos(PHI), 0, -Math.sin(PHI)), nn = v3(Math.sin(PHI), 0, Math.cos(PHI));   // across the cover, its outward normal
  const bc = v3(19, ay - 8 - 9.5, 5.2);                         // the book's centre: its top 8 under the anchor
  const loc = p => { const d = v3(p.x - bc.x, p.y - bc.y, p.z - bc.z); return [d.x * tg.x + d.z * tg.z, d.y, d.x * nn.x + d.z * nn.z]; };
  const out = [];
  const topC = v3(bc.x, bc.y + 9.5, bc.z);
  out.push({ piece: 'chain', outR: 2, sdf: p => { let d = 1e9; for (let i = 0; i < 8; i++) { const c = lerp3(v3(ax, ay, az), topC, (i + 0.5) / 8); d = Math.min(d, i % 2 ? sdEllipsoid(p, c, v3(0.5, 0.9, 0.9)) : sdEllipsoid(p, c, v3(0.9, 0.9, 0.5))); } return Math.min(d, sdRoundBox(p, v3(ax, ay, az), v3(1.4, 1.4, 1.4), 0.4)); },
    color: (p, n) => tone(M.steel, litTone(n.x, -n.y, n.z) >= 4 ? 4 : 2) });
  out.push({ piece: 'tome', outR: 4, sdf: p => { const [a, b, c] = loc(p); return sdRoundBox(v3(a, b, c), v3(0, 0, 0), v3(8, 9.5, 1.6), 0.4); },
    color: (p, n) => {
      const [a, b, c] = loc(p), lx = a, ly = 9.5 - b + 8;           // the flat art's own (lx, ly): lx -8..8, ly 8..27
      const front = c > 1.2, backC = c < -1.2;
      if (!front && !backC) {                                        // the edges: pages on the fore-edge (+lx), the spine (-lx)
        if (lx > 7.2 || (ly > 8.8 && ly < 26.2 && lx > -7.2)) return tone(AM.paper, (Math.floor(ly) % 2) ? 4 : 2);
        return tone(AM.tome, 1);
      }
      const fore = front ? lx >= 6.5 : lx < -6.5, spine = front ? lx < -6.6 : lx >= 6.6, corner = (lx < -4.5 || lx >= 4) && (ly < 11 || ly >= 24);
      if (fore) return tone(AM.paper, (Math.floor(ly) % 2) ? 4 : 2);
      if (corner) return tone(M.gold, lx < 0 && ly < 15 ? 5 : 2);
      if (spine) return tone(AM.tome, 1);
      if (front) {
        const d = Math.hypot(lx + 0.5, ly - 17.5);
        if (d > 3.2 && d < 4.5) return tone(M.gold, lx < 0 ? 5 : 3);
        if (d < 1.5) return tone(M.gold, 4);
        if (lx >= 3.5 && lx < 6.5 && ly >= 16 && ly < 19) return tone(M.bronze, ly < 17 ? 5 : 3);
      }
      return tone(AM.tome, lx < -3 ? 4 : lx > 3 ? 2 : 3) ;
    } });
  return out;
}

// ---------------------------------------------------------------- 10. WRIST WRAPS (over the gloves, under the cape)
// accessories.js wrapsFor(): cloth wound round the lower forearm (front-art rows 50-60), a diagonal hatch, and two tails
// from the outer edge of the wrist hanging down, 24 and 18 long, 3 wide narrowing to 2, the second a tone darker.
function wraps3d(w, st = ACC_ST) {
  const r = st.pal === 'Element' ? ramp(ELEMENT[st.el], { lift: 0.3, shade: 0.4 }) : CLOTH[st.pal || 'Crimson'];
  const out = [];
  for (const s of [1, -1]) {
    const A = arm(s), arms = w.filter(p => p.piece === (s > 0 ? 'armL' : 'armR') || p.piece === (s > 0 ? 'gloveL' : 'gloveR'));
    const shell = p => { let d = 1e9; for (const q of arms) d = Math.min(d, q.sdf(p)); return d; };
    const t0 = 0.36, t1 = 0.9;   // the flat wrap covers the lower two thirds of the forearm (rows 50-60 of 45-60), on the approved 3D arm
    out.push({ piece: 'wrap' + s, outR: 4, sdf: bounded(v3(Math.min(A.el.x, A.wr.x) - 8, A.wr.y - 4, -10), v3(Math.max(A.el.x, A.wr.x) + 8, A.el.y + 2, 10),
        onShell(shell, 0.9, 0.8, p => { const t = alongFore(p, A); return Math.max(t0 - t, t - t1) * 23; })),
      color: (p, n) => { const t = alongFore(p, A), ang = Math.atan2(p.z - A.wr.z, (p.x - A.wr.x) * s) * 3.2, k = ((Math.floor(ang + t * 23) % 4) + 4) % 4, l = litTone(n.x, -n.y, n.z);
        return tone(r, k < 2 ? (l >= 4 ? 4 : 3) : (l <= 2 ? 1 : 2)); } });
    // the tails, from the outer edge of the wrist
    const wq = lerp3(A.el, A.wr, 0.82), top = v3(wq.x + s * 4.6, wq.y, wq.z);
    [[24, 0], [18, 1.7]].forEach(([len, ph], ti) => {
      const pts = []; for (let k = 0; k <= len; k += 1) { const wob = Math.sin(st.t * 3.2 - k * 0.3 + ph) * Math.min(1, k / 6) * 1.3; pts.push(v3(top.x + s * (k * 0.18 + ti * 1.5) + wob, top.y - k, top.z - ti * 0.8)); }
      out.push({ piece: 'tail' + s + ti, outR: 4, sdf: p => { let d = 1e9; for (let k = 0; k < pts.length - 1; k++) { const half = k < len - 6 ? 1.5 : 1.0;
            const a = pts[k], b = pts[k + 1], h = Math.max(0, Math.min(1, (p.y - a.y) / (b.y - a.y))), cx = a.x + (b.x - a.x) * h, cy = a.y + (b.y - a.y) * h;
            d = Math.min(d, Math.max(Math.abs(p.x - cx) - half, Math.abs(p.z - a.z) - 0.8, Math.abs(p.y - cy) - 0.5)); } return d; },
        color: (p, n) => tone(r, Math.min(4, litTone(n.x, -n.y, n.z)) - ti) });
    });
  }
  return out;
}

// ---------------------------------------------------------------- 19. ORBITING STONES
// accessories.js stones(): three red stones on an orbit round the waist (28 out, centred on row 46), each a cut
// stone 8 above its centre to a point, shoulders 1 above, 5.6 below to a narrower base, up to 9 wide as it spins;
// lit on top. In 3D the orbit is a level circle of radius 28 (the flat art's 28 x 7 ellipse was drawn without the
// camera; through the arena tilt a level circle reads 28 x 12.6) and each stone a four-faced crystal turning on its axis.
function stones3d(w, st = ACC_ST) {
  const out = [];
  const prof = (u, y, hw) => {    // the pentagon (u across, y up from the centre): top (0, 8), shoulders (+-hw, 1), base (+-0.6hw, -5.6)
    const P = [[0, 8], [hw, 1], [hw * 0.6, -5.6], [-hw * 0.6, -5.6], [-hw, 1]]; let d = -1e9;
    for (let i = 0; i < P.length; i++) { const [x0, y0] = P[i], [x1, y1] = P[(i + 1) % P.length], ex = x1 - x0, ey = y1 - y0, L = Math.hypot(ex, ey); d = Math.max(d, ((y - y0) * ex - (u - x0) * ey) / L); }
    return d;
  };
  for (let i = 0; i < 3; i++) {
    const a = st.t * 0.95 + i * Math.PI * 2 / 3, c = v3(Math.cos(a) * 28, Y(46) + Math.sin(st.t * 2 + i * 2) * 1.5, Math.sin(a) * 28), spin = st.t * 2.2 + i;
    const cs = Math.cos(spin), sn = Math.sin(spin);
    out.push({ piece: 'stone' + i, outR: 2, sdf: bounded(v3(c.x - 6, c.y - 7, c.z - 6), v3(c.x + 6, c.y + 9, c.z + 6), p => { const dx = p.x - c.x, dz = p.z - c.z, u1 = dx * cs + dz * sn, u2 = -dx * sn + dz * cs, y = p.y - c.y; return Math.max(prof(u1, y, 4.5), prof(u2, y, 4.5)); }),
      color: (p, n) => { const l = litTone(n.x, -n.y, n.z); return p.y > c.y + 5 ? tone(AM.stone, 5) : tone(AM.stone, l >= 4 ? 4 : l >= 3 ? 3 : 2); } });
  }
  return out;
}

const ACC3D = [
  { acc: bandolier, make: bandolier3d, mirror: true }, { acc: harness, make: harness3d, mirror: false },
  { acc: collar, make: collar3d, mirror: false }, { acc: banner, make: banner3d, mirror: true },
  { acc: hourglassPack, make: hourglass3d, mirror: false }, { acc: tome, make: tome3d, mirror: true },
  { acc: wraps, make: wraps3d, mirror: false }, { acc: orbit, make: stones3d, mirror: false },
];
const accFlat = (acc, wr, st = ACC_ST) => [...WEARERS[wr].flat(), ...acc.front(st, [])];
const accFlatBack = (acc, wr, st = ACC_ST) => [...WEARERS[wr].flatBack(), ...backRank(acc, acc.back(st, []))];

// ================================================================ 3. ARM PIECES (arms.js - the eleven, each the whole near arm)
// A FIRST LOOK, GENERIC: each piece's own approved flat layers (draw(), at rest) turned into 3D texel by texel, so
// nothing is redrawn. The art's near arm hangs straight at x 5 (shoulder row 29.7, fist row 65); the 3D near arm
// (the character's right, -x) runs from its shoulder (y 101) to the hand (y 66) - the same 35 texels - so a flat texel
// (ax, ay) sits at height Y(ay) - 4.3, across the arm by ax - 5 (screen right in the front art = +x).
//   - a layer riding the arm or the shoulder: each row's CORE run (the stretch over the arm's axis) is LATHED round the
//     axis - a ring of that run's half-width - its colours wrapped on: the front and back halves show the flat art as
//     drawn (seen from behind, mirrored), the sides the run's outer 30 %, spread round the turn;
//   - what stands off the arm (horns, crystals, fins, talons, a cage's bars beyond the core): slabs as thick as the run is
//     wide (1.6 to 5), centred on the arm's depth;
//   - a layer riding the torso (a chest strap): laid on the wearer's front; particles (no outline) float at the arm's depth.
// The near arm, its glove and its pauldron come off the wearer (arms.js stripNearArm). Glows, embers' motion and the
// Penumbra's see-through alpha are not drawn.
const ARM_ST = { t: 0.4, walk: false, el: 'Fire', run: 0 };
function armPiece3d(piece, w) {
  POSE.hands = {}; CARDS = [];
  const A = arm(-1), DY = 4.3;
  const axisAt = y => { const t = Math.max(0, Math.min(1, (A.sh.y - y) / (A.sh.y - A.wr.y))); return v3(A.sh.x + (A.wr.x - A.sh.x) * t, y, A.sh.z + (A.wr.z - A.sh.z) * t); };
  const shell = wearerShell(w, false);
  const layers = piece.draw({ ...ARM_ST, sp: (x, y) => [x, y] }, []);
  const splats = [];
  layers.forEach((l, li) => {
    if (!l.c.size) return;
    const pts = [], outR = l.outline === false ? 0 : (l.outR ?? OUT_R);
    const rows = new Map(); for (const k of l.c.keys()) { const y = KY(k); if (!rows.has(y)) rows.set(y, []); rows.get(y).push(KX(k)); }
    if (l.joint === 'torso' && l.outline !== false) {
      for (const [k, c] of l.c) { const x = FX(KX(k)), y = Y(KY(k) + 0.5), z = frontZ(shell, x, y); if (z != null) pts.push({ p: v3(x, y, z + 0.9), color: c }); }
    } else {
      for (const [ay, xs] of rows) {
        xs.sort((a, b) => a - b);
        const runs = []; for (const x of xs) { const r = runs[runs.length - 1]; if (r && x === r[1] + 1) r[1] = x; else runs.push([x, x]); }
        const y = Y(ay + 0.5) - (l.joint === 'torso' ? 0 : DY), ax0 = axisAt(y);
        // an arm layer's core is the arm's own column (-1..10); what reaches past it (fins, talons) stands off as slabs
        const split = []; for (const [r0, r1] of runs) { if (l.joint !== 'arm' || r1 < 3 || r0 > 6) { split.push([r0, r1, l.joint !== 'torso' && r0 <= 6 && r1 >= 3]); continue; }
          const c0 = Math.max(r0, -1), c1 = Math.min(r1, 10); if (r0 < c0) split.push([r0, c0 - 1, false]); split.push([c0, c1, true]); if (r1 > c1) split.push([c1 + 1, r1, false]); }
        for (const [x0, x1, core] of split) {
          const m = (x0 + x1 + 1) / 2, h = (x1 - x0 + 1) / 2;
          if (core) {
            const steps = Math.max(16, Math.ceil(Math.PI * 2 * h / 0.45));
            for (let i = 0; i < steps; i++) {
              const th = -Math.PI + (i + 0.5) / steps * Math.PI * 2, s = Math.sin(th), a = Math.abs(th) > Math.PI / 2 ? Math.PI - Math.abs(th) : Math.abs(th);
              // across the art: the front-exact sine over the middle, linear round the sides to the run's edge
              const f = a <= Math.PI / 4 ? Math.sin(a) : 0.7071 + (1 - 0.7071) * (a - Math.PI / 4) / (Math.PI / 4);
              const col = Math.max(x0, Math.min(x1, Math.floor(m + Math.sign(s) * f * h - (Math.sign(s) < 0 ? 0 : 0.0001))));
              const c = l.c.get(K(col, ay)); if (!c) continue;
              pts.push({ p: v3(ax0.x + (m - 5) + s * h, y, ax0.z + Math.cos(th) * h), color: c });
            }
          } else {
            const hz = Math.max(0.8, Math.min(2.5, h));
            for (let x = x0; x <= x1; x++) { const c = l.c.get(K(x, ay)); for (let z = -hz; z <= hz + 0.01; z += 0.7) pts.push({ p: v3(l.joint === 'torso' ? FX(x) : ax0.x + (x + 0.5 - 5), y, ax0.z + z), color: c }); }
          }
        }
      }
    }
    splats.push({ prim: { piece: 'armpiece' + li, outR }, pts });
  });
  return splats;
}
const withoutNearArm = prims => prims.filter(p => !['armR', 'gloveR', 'pauldronR'].includes(p.piece));

// ================================================================ 3b. ARM PIECES: 3D BUILD (A) vs SIDE ART (B), two pieces
// The user's question: how to build the arm pieces. Two pieces, both ways, on Hoplite's RIGHT arm (decided), at rest and with
// the arm swung forward and back as mock8.js's run poses it (upper arm +-34 degrees, the elbow 88 more).
// The arm here has the flat art's proportions: shoulder at art row 29.7, elbow 49.7, fist 65 (upper 20, fore 15.3),
// the shoulder at (-19, 101, 0) (2.5 out from the bare arm: the pieces are thicker than it and stand clear of the cuirass). Art (X, Y) in a segment's frame: X across (5 = the axis, + toward the body's +x),
// Y down the arm, n toward the arm's front. The shoulder piece turns 0.8 of the upper arm (arms.js SHOULDER_SHARE).
//   A - a 3D build: each part a shaped solid in its segment's frame (ellipsoids, a tapered column, hexagonal crystals with
//       points), lit by the kit's light per view and pose, rendered with the figure (depth per texel).
//   B - side art: fixed flat pictures per view drawn with the piece's OWN 2D brushes (chrome(), the rock shader, prism(),
//       the drip strokes) at the flat art's density - the approved front and back as they are, front 3/4 / side / back 3/4
//       drawn from the same parts seen from there - then posed the way the rig poses sprites: each segment's picture
//       turned and stretched in the screen plane (it cannot foreshorten or turn in depth), stacked as one layer.
const deg = d => d * Math.PI / 180;
function armFrames(sw, b) {
  const S = v3(-19, 101, 0), d1 = v3(0, -Math.cos(sw), Math.sin(sw)), E = v3(S.x, S.y + d1.y * 20, S.z + d1.z * 20);
  const d2 = v3(0, -Math.cos(b), Math.sin(b)), cs = 0.8 * sw;
  return {
    // the shoulder piece sits where the sets' pauldrons do (views.js places those by height, Y(row)), 4.3 above the arm's
    // own mapping: its art row 34 is at the shoulder joint
    cap: { J: S, d: v3(0, -Math.cos(cs), Math.sin(cs)), N: v3(0, Math.sin(cs), Math.cos(cs)), Y0: 34 },
    upper: { J: S, d: d1, N: v3(0, Math.sin(sw), Math.cos(sw)), Y0: 29.7 },
    fore: { J: E, d: d2, N: v3(0, Math.sin(b), Math.cos(b)), Y0: 49.7 },
  };
}
const toArt = (p, f) => { const r = v3(p.x - f.J.x, p.y - f.J.y, p.z - f.J.z); return [5 + r.x, f.Y0 + r.x * f.d.x + r.y * f.d.y + r.z * f.d.z, r.x * f.N.x + r.y * f.N.y + r.z * f.N.z]; };
const fromArt = (X, Y, n, f) => v3(f.J.x + (X - 5) + f.d.x * (Y - f.Y0) + f.N.x * n, f.J.y + f.d.y * (Y - f.Y0) + f.N.y * n, f.J.z + f.d.z * (Y - f.Y0) + f.N.z * n);
// a part: { seg: 'cap'|'upper'|'fore', sdf(X, Y, n) } - the piece's prim takes the min over its parts in their own frames
function armPrim(piece, outR, parts, F, color) {
  const sdf = p => { let d = 1e9; for (const q of parts) { const [X, Y, n] = toArt(p, F[q.seg]); d = Math.min(d, q.sdf(X, Y, n)); } return d; };
  return { piece, outR, sdf, color: (p, nv) => { let best = 1e9, bq = null, a = null; for (const q of parts) { const L = toArt(p, F[q.seg]), d = q.sdf(...L); if (d < best) { best = d; bq = q; a = L; } } return (bq.color || color)(a[0], a[1], a[2], nv, bq); } };
}
const ell3 = (X, Y, n, cx, cy, rx, ry, rz) => sdEllipsoid(v3(X, Y, n), v3(cx, cy, 0), v3(rx, ry, rz));
const segOf = y => y < 49.7 ? 'upper' : 'fore';

// ---------------------------------------------------------------- QUICKSILVER: the plainest wrap
// arms.js: four chrome ellipses down the arm (upper, elbow, forearm, fist) and one over the shoulder, molten runs off the
// cap's rim and the elbow, a bright ripple ring sliding down (here at its t = 0.4 place, row 43.1).
const QS = {
  arm: [[4, 43, 5.6, 7.2], [4.5, 50.5, 4.6, 3.2], [4.6, 55.5, 5, 6.5], [5, 65, 5.2, 4.6]], cap: [4.5, 35, 7.4, 6.4],
  drips: [[-1.5, 38, 0, 6], [1.5, 40.5, 2.1, 4], [9.5, 39, 4.2, 5], [0, 52, 1.3, 4]], rippleY: 34 + ((0.4 * 0.6) % 1) * 38, t: 0.4,
  depth: rx => rx * 0.92,
};
const qsDripLen = (n, ph) => n * (0.55 + 0.45 * Math.sin(QS.t * 1.6 + ph));
// where a run leaves the surface: on the cap (or the elbow) under its art point, toward the front
const qsDripN = (x, y) => { const [cx, cy, rx, ry] = y > 45 ? QS.arm[1] : QS.cap, rz = QS.depth(rx), k = 1 - ((x - cx) / rx) ** 2 - ((y - cy) / ry) ** 2; return rz * Math.sqrt(Math.max(0.04, k)) - 0.6; };
function quicksilverA(F) {
  const chromeN = nv => { const h = nv.x * 0.9 - nv.y * 0.35; return h < -0.62 ? 5 : h < -0.28 ? 4 : h < 0.02 ? 1 : h < 0.42 ? 2 : h < 0.78 ? 3 : 1; };
  const ripple = (Y, i) => Math.abs(Y - QS.rippleY) < 1 ? 5 : Math.abs(Y - QS.rippleY) < 2 ? 4 : i;
  const armParts = QS.arm.map(([cx, cy, rx, ry]) => ({ seg: segOf(cy), sdf: (X, Y, n) => ell3(X, Y, n, cx, cy, rx, ry, QS.depth(rx)) }));
  const capParts = [{ seg: 'cap', sdf: (X, Y, n) => ell3(X, Y, n, ...QS.cap, QS.depth(QS.cap[2])) }];
  for (const [x, y, ph, nn] of QS.drips) { const len = qsDripLen(nn, ph), n0 = qsDripN(x, y), seg = y > 45 ? 'fore' : 'cap';
    const part = { seg, drip: x, sdf: (X, Y, n) => Math.min(sdCapsule(v3(X, Y, n), v3(x, y, n0), v3(x, y + len, n0), 1), sdEllipsoid(v3(X, Y, n), v3(x, y + len, n0), v3(1.4, 1.4, 1.4))) };
    (y > 45 ? armParts : capParts).push(part); }
  const col = (X, Y, n, nv, q) => q.drip !== undefined ? tone(AR.merc, litTone(nv.x, -nv.y, nv.z) >= 3 ? 5 : 2) : tone(AR.merc, ripple(Y, chromeN(nv)));
  return [armPrim('qsArm', 4, armParts, F, col), armPrim('qsCap', 4, capParts, F, col)];
}
// B: the flat pictures, per view - the same ellipses seen from there (each as wide as the solid it stands for), shaded
// by arms.js chrome() exactly as the front is; the runs where they show
function quicksilverB(ca, sa) {
  const arm = new Layer(12.6), cap = new Layer(14.2);
  const proj = (cx, rx) => [5 + (cx - 5) * ca, Math.sqrt((rx * ca) ** 2 + (QS.depth(rx) * sa) ** 2)];
  for (const [cx, cy, rx, ry] of QS.arm) { const [c, r] = proj(cx, rx); arm.fill(ell(c, cy, r, ry), chrome(c, cy, r, ry)); }
  { const [cx, cy, rx, ry] = QS.cap, [c, r] = proj(cx, rx); cap.fill(ell(c, cy, r, ry), chrome(c, cy, r, ry)); }
  for (const [x, y, ph, nn] of QS.drips) {
    const n0 = qsDripN(x, y), xv = 5 + (x - 5) * ca + n0 * sa, vz = -(x - 5) * sa + n0 * ca; if (vz < -1.5) continue;   // round the far side
    const len = qsDripLen(nn, ph), l = y > 45 ? arm : cap;
    armStroke(l, xv, y, xv, y + len, 2, (xx) => tone(AR.merc, xx + 0.5 < xv ? 5 : 2));
    l.fill(ell(xv, y + len, 1.4, 1.4), (xx, yy) => tone(AR.merc, xx + 0.5 < xv && yy + 0.5 < y + len ? 5 : 2));
  }
  for (const l of [arm, cap]) for (const [k] of [...l.c]) { const y = KY(k); if (Math.abs(y + 0.5 - QS.rippleY) < 1) l.c.set(k, tone(AR.merc, 5)); else if (Math.abs(y + 0.5 - QS.rippleY) < 2) l.c.set(k, tone(AR.merc, 4)); }
  return { arm: [arm], cap: [cap] };
}

// ---------------------------------------------------------------- SALT: the most parts off the arm
// arms.js: a column of grey rock salt (the fist inside it), a rock lump over the shoulder, and nine crystals (white and rose,
// hexagonal, pointed): four off the shoulder, five down the arm and the knuckles. A glint jumps between the tips (not drawn).
// In 3D each crystal keeps its front-view direction and length; it also leans toward or away from the camera (the n
// column below), lengthened so its front-view length is the approved one.
const SALT = {
  left: [[-1, 37], [0, 50], [-1, 61], [-1, 64], [1, 69]], right: [[10, 37], [9, 50], [10, 61], [9, 69]],
  capPoly: [[-3, 37], [-2, 31], [3, 28], [9, 29], [12, 33], [11, 40], [-1, 41]], cap: [4.5, 34.5, 7.4, 6.5, 6.6],
  //       base x, y, dx, dy, len, w, ramp, lean (n per unit of front length), on the shoulder?
  xs: [[1, 33, -0.45, -1, 13, 4, 'salt', 0.35, 1], [5, 31, 0.05, -1, 9, 3, 'rose', -0.45, 1], [-2, 36, -1, -0.55, 9, 3, 'rose', 0.3, 1], [8, 31, 0.45, -1, 6, 3, 'salt', -0.35, 1],
       [0, 53, -1, 0.15, 6, 3, 'salt', 0.45, 0], [0, 57, -1, 0.5, 5, 3, 'rose', -0.45, 0], [0, 44, -1, -0.3, 4, 2, 'salt', 0.5, 0], [8, 66, 0.5, 1, 4, 2, 'rose', 0.4, 0], [4, 67, 0, 1, 4, 2, 'salt', -0.4, 0]],
  knuckles: [[2, 62], [6, 63], [4, 66]],
};
const lerpEdge = (pts, y) => { for (let i = 0; i < pts.length - 1; i++) { const [x0, y0] = pts[i], [x1, y1] = pts[i + 1]; if (y <= y1) return x0 + (x1 - x0) * Math.max(0, (y - y0) / (y1 - y0)); } return pts[pts.length - 1][0]; };
const saltCol = Y => { const L = lerpEdge(SALT.left, Y), R = lerpEdge(SALT.right, Y) + 1; return [(L + R) / 2, (R - L) / 2]; };
const saltRockTone = (X, Y, base) => base + (hash2(Math.floor(X) >> 1, Math.floor(Y) >> 1) > 0.72 ? 1 : hash2(Math.floor(X) >> 1, Math.floor(Y) >> 1) < 0.2 ? -1 : 0);
// a crystal's frame: base in art (x, y, n 0), its 3D direction (dx, dy, lean), the front-view length kept
function saltCrystal(c) {
  const [bx, by, dx, dy, len, w, rr, lean, onCap] = c, L0 = Math.hypot(dx, dy), u = norm3(v3(dx / L0, dy / L0, lean)), len3d = len * Math.hypot(1, lean);
  const s = norm3(cross3(u, Math.abs(u.z) < 0.9 ? v3(0, 0, 1) : v3(1, 0, 0))), t = cross3(u, s);
  // its base just under the rock's front at that point: the approved front shows each crystal laid over the rock
  const bn = onCap ? (() => { const [cx, cy, rx, ry, rz] = SALT.cap; return rz * Math.sqrt(Math.max(0, 1 - ((bx - cx) / rx) ** 2 - ((by - cy) / ry) ** 2)); })()
                   : (() => { const [c, hw] = saltCol(Math.max(37, Math.min(69, by))); return hw * 0.9 * Math.sqrt(Math.max(0, 1 - ((bx - c) / hw) ** 2)); })();
  return { bx, by, bn: bn * 0.75, u, s, t, len: len3d, R: w / 2, w, ramp: AR[rr], seg: onCap ? 'cap' : segOf(by), onCap };
}
const SALT_X = SALT.xs.map(saltCrystal);
const hexD = (a, b, R) => { let m = -1e9, m2 = -1e9; for (let i = 0; i < 3; i++) { const an = i * Math.PI / 3, v = Math.abs(a * Math.cos(an) + b * Math.sin(an)); if (v > m) { m2 = m; m = v; } else if (v > m2) m2 = v; } return [m - R, m - m2]; };
function saltA(F) {
  const armParts = ['upper', 'fore'].map(seg => ({ seg, sdf: (X, Y, n) => {
    const y = Math.max(37, Math.min(69, Y)), [c, hw] = saltCol(y), hz = hw * 0.9;
    const e = (Math.hypot((X - c) / hw, n / hz) - 1) * Math.min(hw, hz), cut = seg === 'upper' ? Math.max(37 - Y, Y - 50.5) : Math.max(49 - Y, Y - 69);
    return Math.max(e, cut);
  } }));
  const rock = (X, Y, n, nv, q) => {
    if (!q.cap) for (const [kx, ky] of SALT.knuckles) if (Math.abs(X - kx - 0.5) < 0.7 && Math.abs(Y - ky - 0.5) < 0.7 && n > 2.5) return tone(AR.saltRock, 1);
    return tone(AR.saltRock, saltRockTone(X, Y, litTone(nv.x, -nv.y, nv.z)));
  };
  const capParts = [{ seg: 'cap', cap: true, sdf: (X, Y, n) => ell3(X, Y, n, ...SALT.cap) }];
  const xtal = cs => cs.map(c => ({ seg: c.seg, c, sdf: (X, Y, n) => {
    const r = v3(X - c.bx, Y - c.by, n - c.bn), a = r.x * c.u.x + r.y * c.u.y + r.z * c.u.z, p = r.x * c.s.x + r.y * c.s.y + r.z * c.s.z, q = r.x * c.t.x + r.y * c.t.y + r.z * c.t.z;
    const tip = c.len - c.w * 0.8, R = a > tip ? c.R * Math.max(0, (c.len - a) / (c.len - tip)) : c.R;
    return Math.max(hexD(p, q, R)[0], -1.5 - a, a - c.len);
  } }));
  const xcol = (X, Y, n, nv, qq) => {
    const c = qq.c, r = v3(X - c.bx, Y - c.by, n), a = r.x * c.u.x + r.y * c.u.y + r.z * c.u.z, p = r.x * c.s.x + r.y * c.s.y + r.z * c.s.z, q = r.x * c.t.x + r.y * c.t.y + r.z * c.t.z, l = litTone(nv.x, -nv.y, nv.z);
    if (a > c.len - c.w * 0.8 - 0.5) return tone(c.ramp, l >= 3 ? 5 : 3);
    if (hexD(p, q, c.R)[1] < 0.18 * c.R && l >= 3) return tone(c.ramp, 5);
    return tone(c.ramp, l >= 3 ? 4 : 2);
  };
  const over = p => Object.assign(p, { depthBias: 8 });   // the crystals stack over their rock, as the flat layers do (12.8 over 12.6, 14.3 over 14.2)
  return [armPrim('saltArm', 4, armParts, F, rock), over(armPrim('saltArmX', 4, xtal(SALT_X.filter(c => !c.onCap)), F, xcol)),
          armPrim('saltCap', 4, capParts, F, rock), over(armPrim('saltCapX', 4, xtal(SALT_X.filter(c => c.onCap)), F, xcol))];
}
function saltB(ca, sa) {
  const arm = new Layer(12.6), xa = new Layer(12.8), xaBack = new Layer(12.5), sh = new Layer(14.2), xs = new Layer(14.3), xsBack = new Layer(14.1);
  // the column, row by row: as wide as the solid seen from here, shaded by the flat rock shader across that width
  for (let y = 37; y < 69; y++) { const [c, hw] = saltCol(y + 0.5), w = Math.sqrt((hw * ca) ** 2 + (hw * 0.9 * sa) ** 2), cc = 5 + (c - 5) * ca;
    for (let x = Math.floor(cc - w); x <= Math.ceil(cc + w); x++) if (Math.abs(x + 0.5 - cc) <= w) arm.set(x, y, tone(AR.saltRock, cylI(cc, w + 0.5)(x) + (hash2(x >> 1, y >> 1) > 0.72 ? 1 : hash2(x >> 1, y >> 1) < 0.2 ? -1 : 0))); }
  if (ca > 0.3) for (const [x, y] of SALT.knuckles) arm.dot(Math.round(5 + (x - 5) * ca + 3.5 * sa), y, tone(AR.saltRock, 1));
  // the shoulder lump: the approved outline, squeezed to its width from here
  const k = Math.sqrt(ca * ca + (SALT.cap[4] / SALT.cap[2]) ** 2 * sa * sa);
  sh.fill(poly(SALT.capPoly.map(([x, y]) => [4.5 + (x - 4.5) * k * Math.sign(ca || 1) + 0, y])), (x, y) => tone(AR.saltRock, cylI(4, 6 * k)(x) + (hash2(x >> 1, y >> 1) > 0.72 ? 1 : hash2(x >> 1, y >> 1) < 0.2 ? -1 : 0)));
  // the crystals: each drawn by prism() along its direction as seen from here, behind the rock when it points away
  for (const c of SALT_X) {
    const bxv = 5 + (c.bx - 5) * ca + c.bn * sa, dxv = c.u.x * ca + c.u.z * sa, dyv = c.u.y, vz = -(c.bx - 5) * sa + c.bn * ca + (-c.u.x * sa + c.u.z * ca) * c.len * 0.5;
    const flen = Math.hypot(dxv, dyv) * c.len; if (flen < 1.5) { (c.onCap ? xs : xa).fill(ell(bxv, c.by, c.R, c.R), (x, y) => tone(c.ramp, x + 0.5 < bxv ? 5 : 3)); continue; }
    prism(vz < -1 ? (c.onCap ? xsBack : xaBack) : (c.onCap ? xs : xa), bxv, c.by, dxv, dyv, Math.max(c.w * 0.8 + 1, flen), c.w, c.ramp);
  }
  return { arm: [xaBack, arm, xa], cap: [xsBack, sh, xs] };
}

// B posed: each segment's picture (its art rows) mapped onto the posed segment as the screen sees it - turned and
// stretched along the arm, never foreshortened across it
function armB(make, F, yaw, mirror) {
  const a = deg(yaw), ca = Math.cos(a), sa = Math.sin(a);
  // the picture: from the view's own angle round the arm (the mirrored model sees the arm from the other side)
  const pics = make(mirror ? -ca : ca, sa);
  const P = p => { const q = mirror ? v3(-p.x, p.y, p.z) : p, vx = q.x * ca + q.z * sa, vz = -q.x * sa + q.z * ca; return { c: vx, r: XY1 - (q.y - vz * XSHEAR), vz }; };
  const out = [];
  for (const [key, segs] of [['cap', ['cap']], ['arm', ['upper', 'fore']]]) {
    for (const src of pics[key]) {
      const l = new Layer(0); l.outR = 4; let zs = 0, nz = 0;
      for (const seg of segs) {
        const f = F[seg], J = P(f.J), D = P(v3(f.J.x + f.d.x, f.J.y + f.d.y, f.J.z + f.d.z)), ax = v3(D.c - J.c, D.r - J.r, 0), L = Math.max(0.12, Math.hypot(ax.x, ax.y)), ux = ax.x / L, uy = ax.y / L;
        const yr = seg === 'upper' ? [0, 49.7] : seg === 'fore' ? [49.7, 200] : [-50, 200];
        // screen across = the picture's +X: the arm's own +x as the screen shows it (mirror flips the art)
        const px = uy, py = -ux;
        for (const [k, col] of src.c) { const X = KX(k) + 0.5, Y = KY(k) + 0.5; if (Y < yr[0] || Y >= yr[1]) continue;
          // forward-map each picture texel's corners so the stretched picture has no holes
          const sx = 1;
          for (let sy = 0; sy < Math.max(1, Math.ceil(L)); sy++) { const yy = Y - 0.5 + (sy + 0.5) / Math.max(1, Math.ceil(L));
            const c = J.c + (yy - f.Y0) * L * ux + (X - 5) * px * sx, r = J.r + (yy - f.Y0) * L * uy + (X - 5) * py * sx;
            l.c.set(K(Math.floor(c), Math.floor(r)), col); }
          zs += J.vz; nz++; }
      }
      l.depth = (nz ? zs / nz : 0) + src.depth / 100; out.push(l);
    }
  }
  return out;
}
const ARMCMP = { quicksilver: { A: quicksilverA, B: quicksilverB }, salt: { A: saltA, B: saltB } };
const ARM_POSES = [['rest', 0, 0], ['forward', 34, 34 + 88], ['back', -34, -34 + 88]];

// ================================================================ 3c. ARM PIECES BUILT IN 3D (the user's choice, 2026-11-11: method A)
// Every piece of arms.js as shaped solids on the right arm (the frames above: armFrames, art X/Y/n per segment). The
// approved layers (piece.draw at rest) are the source: what wraps the arm is LATHED from them row by row (each row's run
// over the arm becomes a ring of that half-width), what stands off it is built as its own solid per piece. Colour is
// the approved texel at that point of the art (front and back show the art as drawn; seen from behind it is the same
// picture, mirrored), RE-TONED for the light: the flat art's tone index plus the difference between the 3D surface's
// light and the light the flat art assumed there (a cylinder facing the viewer), so bands, rivets, veins, panel lines
// and glints stay where they are drawn and the wrap shades round its sides.
const RAMP_OF = (() => { const m = new Map(); for (const r of [...Object.values(AR), ...Object.values(M)]) for (let i = 0; i < 6; i++) { const c = tone(r, i); if (!m.has(c)) m.set(c, [r, i]); } return m; })();
const artLayers = piece => piece.draw({ ...ARM_ST, sp: (x, y) => [x, y] }, []);
// the art colour nearest (X, Y) in a layer (exact texel first, then up to `reach` across the row)
function artAt(l, X, Y, reach = 3) {
  const x = Math.floor(X), y = Math.floor(Y); let c = l.c.get(K(x, y)); if (c) return c;
  for (let d = 1; d <= reach; d++) for (const s of [-1, 1]) { c = l.c.get(K(x + s * d, y)); if (c) return c; }
  for (let d = 1; d <= 2; d++) for (const s of [-1, 1]) { c = l.c.get(K(x, y + s * d)); if (c) return c; }
  return null;
}
// re-tone a flat colour for a 3D normal: base = the light the flat art drew it under
function retone(c, nv, base) {
  const e = RAMP_OF.get(c); if (!e) return c;
  const [r, i] = e; if (i === 0) return c;
  const l = litTone(nv.x, -nv.y, nv.z);
  return tone(r, Math.max(1, Math.min(5, i + l - base)));
}
const cylBase = sx => { sx = Math.max(-1, Math.min(1, sx)); return litTone(sx, -0.15, Math.sqrt(1 - sx * sx)); };
// a lathe from one layer: rows' runs over [lo, hi] (the arm's own column); rows y0..y1; depth = half-width * dz
function latheRows(l, o = {}) {
  const lo = o.lo ?? -1.5, hi = o.hi ?? 10.5, rows = new Map();
  for (const k of l.c.keys()) { const x = KX(k), y = KY(k); if (o.y0 != null && (y < o.y0 || y > o.y1)) continue; if (o.skip && o.skip(x, y)) continue; if (!rows.has(y)) rows.set(y, []); rows.get(y).push(x); }
  const R = new Map();
  for (const [y, xs] of rows) { xs.sort((a, b) => a - b); const runs = []; for (const x of xs) { const r = runs[runs.length - 1]; if (r && x <= r[1] + 1) r[1] = x; else runs.push([x, x]); }
    // the run over the axis (x 4-5), clipped to the column
    let best = null; for (const r of runs) if (r[0] <= (o.ax ?? 5) + 1 && r[1] >= (o.ax ?? 5) - 1) best = r; if (!best) best = runs.reduce((a, b) => (b[1] - b[0] > a[1] - a[0] ? b : a));
    const a = Math.max(best[0], lo), b = Math.min(best[1], hi); if (b < a) continue;
    R.set(y, { m: (a + b + 1) / 2, h: (b - a + 1) / 2 }); }
  const ys = [...R.keys()].sort((a, b) => a - b);
  return { R, y0: ys[0], y1: ys[ys.length - 1], dz: o.dz ?? 0.9 };
}
// its distance in a segment's art space; rows interpolate between centres; capped above and below
function latheSdf(L, X, Y, n, cut) {
  const yc = Math.max(L.y0 + 0.5, Math.min(L.y1 + 0.5, Y)), ya = Math.floor(yc - 0.5), f = yc - 0.5 - ya;
  const A = L.R.get(ya) || L.R.get(ya + 1), B = L.R.get(ya + 1) || A; if (!A) return 1;
  const m = A.m + (B.m - A.m) * f, h = Math.max(0.5, A.h + (B.h - A.h) * f), hz = h * L.dz;
  const e = (Math.hypot((X - m) / h, n / hz) - 1) * Math.min(h, hz);
  let d = Math.max(e, L.y0 - Y, Y - (L.y1 + 1));
  if (cut) d = Math.max(d, cut[0] - Y, Y - cut[1]);
  return d;
}
const latheRow = (L, Y) => { const y = Math.max(L.y0, Math.min(L.y1, Math.floor(Y))); return L.R.get(y) || { m: 5, h: 5 }; };
// parts for armPrim: a lathe split at the elbow into the upper arm and forearm frames (or all on one segment)
const ELBOW_Y = 49.7;
function latheParts(L, seg) {
  if (seg) return [{ seg, L, sdf: (X, Y, n) => latheSdf(L, X, Y, n) }];
  const out = [];
  if (L.y0 < ELBOW_Y) out.push({ seg: 'upper', L, sdf: (X, Y, n) => latheSdf(L, X, Y, n, [-99, ELBOW_Y + 0.6]) });
  if (L.y1 + 1 > ELBOW_Y) out.push({ seg: 'fore', L, sdf: (X, Y, n) => latheSdf(L, X, Y, n, [ELBOW_Y - 0.6, 999]) });
  return out;
}
// the colour of a lathed point: the art at (X, Y) - on the sides a texel in from the edge - re-toned
const latheColor = l => (X, Y, n, nv, q) => {
  const row = latheRow(q.L, Y), sx = (X - row.m) / row.h, Xs = Math.abs(sx) > 0.8 ? row.m + Math.sign(sx) * (row.h - 1.2) : X;
  const c = artAt(l, Xs, Y); return c ? retone(c, nv, cylBase(sx)) : tone(AR.steel, 3);
};
// a standing-off part's colour: the art where the flat draws it, re-toned against a flat mid light
const commonOf = l => { if (l._common) return l._common; const k = new Map(); for (const c of l.c.values()) k.set(c, (k.get(c) || 0) + 1); return l._common = [...k].sort((a, b) => b[1] - a[1])[0][0]; };
const artColor = (l, base = 3) => (X, Y, n, nv) => { const c = artAt(l, X, Y, 6) || commonOf(l); return retone(c, nv, base); };
// shapes in art space (p = v3(X, Y, n))
const sdCone = (p, a, b, ra, rb) => {   // a capped cone from a (radius ra) to b (radius rb)
  const ba = v3(b.x - a.x, b.y - a.y, b.z - a.z), pa = v3(p.x - a.x, p.y - a.y, p.z - a.z), L2 = ba.x * ba.x + ba.y * ba.y + ba.z * ba.z;
  const h = Math.max(0, Math.min(1, (pa.x * ba.x + pa.y * ba.y + pa.z * ba.z) / L2)), r = ra + (rb - ra) * h;
  return len3(v3(pa.x - ba.x * h, pa.y - ba.y * h, pa.z - ba.z * h)) - r;
};
// a convex polygon (art x, y) extruded through n0 +- t/2
const sdPoly2 = (P, x, y) => { let d = -1e9; for (let i = 0; i < P.length; i++) { const [x0, y0] = P[i], [x1, y1] = P[(i + 1) % P.length], ex = x1 - x0, ey = y1 - y0, L = Math.hypot(ex, ey) || 1; d = Math.max(d, ((x - x0) * ey - (y - y0) * ex) / L); } return d; };
const polySign = P => { let a = 0; for (let i = 0; i < P.length; i++) { const [x0, y0] = P[i], [x1, y1] = P[(i + 1) % P.length]; a += x0 * y1 - x1 * y0; } return a; };
const slab = (P, n0, t) => { const cx = P.reduce((a, p) => a + p[0], 0) / P.length, cy = P.reduce((a, p) => a + p[1], 0) / P.length, Q = sdPoly2(P, cx, cy) < 0 ? P : [...P].reverse();
  return (X, Y, n) => Math.max(sdPoly2(Q, X, Y), Math.abs(n - n0) - t / 2); };
// a tube along art-space points (each [X, Y, n]), radius r
const tube = (pts, r) => p => { let d = 1e9; for (let i = 0; i < pts.length - 1; i++) d = Math.min(d, sdCapsule(p, v3(...pts[i]), v3(...pts[i + 1]), r)); return d; };
// the arm's surface depth at (X, Y) for a lathe (front, n > 0)
const latheFront = (L, X, Y) => { const r = latheRow(L, Y), s = (X - r.m) / r.h; return r.h * L.dz * Math.sqrt(Math.max(0, 1 - s * s)); };
// ---------------------------------------------------------------- the pieces
// Each builder: (F, layers) -> prims. `layers` are the approved draw() layers in order; parts carry their own colour.
const P3 = (X, Y, n) => v3(X, Y, n);
const ARM3D = {};
// RUBEDO: crimson lacquer traced in gold, the Stone in a gold setting on the back of the hand, three stacked plates on
// the shoulder. All of it wraps; nothing stands off. Glow (not drawn): the Stone, red, pulsing.
ARM3D.rubedo = (F, [arm, fist, sh]) => {
  const La = latheRows(arm), Lf = latheRows(fist, { lo: -2, hi: 12 }), Ls = latheRows(sh, { lo: -10, hi: 20, dz: 0.95 });
  return [armPrim('rbArm', 4, latheParts(La), F, latheColor(arm)), armPrim('rbFist', 4, latheParts(Lf, 'fore'), F, latheColor(fist)),
          armPrim('rbCap', 4, latheParts(Ls, 'cap'), F, latheColor(sh))];
};
// VERDIGRIS: riveted copper crusted with patina; the piston down the outer elbow stands off it (a rod on the upper arm
// sliding into a sleeve on the forearm); the shoulder is a cog, eight teeth round a patina hub, its faces gently domed.
ARM3D.verdigris = (F, [arm, pis, sh]) => {
  const La = latheRows(arm);
  const rod = { seg: 'upper', sdf: (X, Y, n) => sdCapsule(P3(X, Y, n), P3(-3, 42.5, 0.5), P3(-3, 49.5, 0.5), 1), color: artColor(pis) };
  const sleeve = { seg: 'fore', sdf: (X, Y, n) => sdTube(v3(X, Y, n), -3, 0.5, 50.5, 59, 2, 2, 0.6), color: artColor(pis) };
  const rot = ARM_ST.t * 1.2;
  const cog = { seg: 'cap', sdf: (X, Y, n) => { const dx = X - 4.5, dy = Y - 34, r = Math.hypot(dx, dy), a = Math.atan2(dy, dx) + rot, tooth = ((a / (Math.PI / 4)) % 1 + 1) % 1 < 0.5;
      const rim = r - (tooth ? 9 : 6.6), face = 3.2 + 1.6 * Math.max(0, 1 - (r / 9) ** 2); return Math.max(rim, Math.abs(n) - face); },
    color: (X, Y, n, nv) => { const c = artAt(sh, X, Y, 3); return c ? retone(c, nv, 4) : tone(AR.copper, 3); } };
  return [armPrim('vgPiston', 2, [rod, sleeve], F), armPrim('vgArm', 4, latheParts(La), F, latheColor(arm)), armPrim('vgCog', 4, [cog], F)];
};
// SALAMANDER: obsidian scales split by lava seams; horns off the shoulder (cones, leaning a little back), two fins down
// the outer forearm (blades in the arm's own plane, edge-on from the side), three talons past the fist (cones, curling
// a little forward). Glows (not drawn): the lava, the fist, the shoulder; the embers rising off it are not drawn either.
ARM3D.salamander = (F, [arm, fist, claw, sh, horns]) => {
  const La = latheRows(arm, { lo: -1, hi: 10 }), Lf = latheRows(fist, { lo: -2, hi: 12 }), Ls = latheRows(sh, { lo: -10, hi: 20, dz: 0.95 });
  const fins = [[[0, 51], [0, 56], [-6, 56.5]], [[0, 56], [0, 61], [-5, 61.5]]].map(P => ({ seg: 'fore', sdf: slab(P.map(([x, y]) => [x + 0.5, y + 0.5]), 0, 1.4), color: artColor(arm) }));
  const talons = [[1.5, 67, -0.5, 76], [5, 68.5, 5, 78], [8.5, 67, 10.5, 75.5]].map(([x0, y0, x1, y1]) => ({ seg: 'fore', sdf: (X, Y, n) => sdCone(P3(X, Y, n), P3(x0, y0, 1.5), P3(x1, y1, 3.2), 1.3, 0.35), color: artColor(claw) }));
  const hornDefs = [[[-5, 35], [-1.5, 30.5], [-11, 23]], [[-1.5, 30], [3, 29], [-2, 19.5]], [[3.5, 29], [7.5, 29.5], [6, 21.5]]];
  const hornParts = hornDefs.map(([a, b, tip], i) => { const bx = (a[0] + b[0]) / 2, by = (a[1] + b[1]) / 2, r = Math.hypot(a[0] - b[0], a[1] - b[1]) / 2;
    return { seg: 'cap', sdf: (X, Y, n) => sdCone(P3(X, Y, n), P3(bx, by + 1.5, 0), P3(tip[0], tip[1], -1.5 + i), r, 0.3), color: artColor(horns) }; });
  return [armPrim('slFins', 4, fins, F), armPrim('slArm', 4, latheParts(La), F, latheColor(arm)), armPrim('slFist', 4, latheParts(Lf, 'fore'), F, latheColor(fist)),
          armPrim('slTalons', 4, talons, F), armPrim('slCap', 4, latheParts(Ls, 'cap'), F, latheColor(sh)), armPrim('slHorns', 4, hornParts, F)];
};
// CRUCIBLE: black iron worked like a furnace. A box pauldron (the approved outline, chamfered, 15 deep) with a brass rim
// and a glowing grate front and back; the riveted upper arm; a brass cog ring at the elbow; a gauntlet with fire behind
// its slits. The leather strap that holds it on runs across the chest and round the back (on the torso, under the cape),
// with its brass buckle. Glows (not drawn): the grate, the gauntlet's slits.
ARM3D.crucible = (F, [strapL, arm, cog, gaunt, sh], wearer) => {
  const La = latheRows(arm), Lc = latheRows(cog, { lo: -1, hi: 11, dz: 1.1 }), Lg = latheRows(gaunt, { lo: -3.5, hi: 12.5 });
  const box = [[-5, 31], [-1, 26], [11, 26], [14, 31], [14, 42], [-5, 42]].map(([x, y]) => [x + 0.5, y + 0.5]);
  const pauldron = { seg: 'cap', sdf: slab(box, 0, 15), color: (X, Y, n, nv) => { const side = Math.abs(n) > 7, c = artAt(sh, side ? Math.max(-4, Math.min(13, X)) : X, Y, 3); if (!c) return tone(AR.iron, 3);
      // the grate's slits are on the faces only; on the sides and top the plate
      if (side && RAMP_OF.get(c) && RAMP_OF.get(c)[0] === AR.ember) return retone(tone(AR.iron, 3), nv, 3);
      return retone(c, nv, 3); } };
  const shell = wearerShell(wearer, false), out = [armPrim('cbArm', 4, latheParts(La), F, latheColor(arm)), armPrim('cbCog', 4, latheParts(Lc), F, latheColor(cog)),
    armPrim('cbGauntlet', 4, latheParts(Lg, 'fore'), F, latheColor(gaunt)), armPrim('cbPauldron', 4, [pauldron], F)];
  out.push(strapOn(shell, [FX(8), Y(33)], [FX(30), Y(60)], 4, AR.leather, p => 62 - p.y, 'cbStrap'));
  const bx = FX(19.5), by = Y(46.5), bz = (frontZ(shell, bx, by) ?? 9) + 1.6;
  out.push({ piece: 'cbBuckle', outR: 4, sdf: p => sdRoundBox(p, v3(bx, by, bz), v3(2, 2, 0.7), 0.3), color: (p, n) => p.y > by + 1.2 || p.x < bx - 1.2 ? tone(AR.brass, 5) : Math.abs(p.x - bx) < 0.8 && Math.abs(p.y - by) < 0.8 ? tone(AR.leather, 1) : tone(AR.brass, 2) });
  return out;
};
// SANGUIS (Crystal): blood frozen hard - the faceted ruby arm and fist wrap; a shard juts back and out off the elbow,
// four shard fingers point down past the fist, and the shoulder is three broad crystal plates at different depths
// (front, outer, back), together the approved cluster from the front. Glows (not drawn): arm and shoulder, red.
ARM3D['sanguis-crystal'] = (F, [arm, sh]) => {
  const La = latheRows(arm, { lo: -1.5, hi: 11, y0: 36, y1: 69 });
  const shard = { seg: 'fore', sdf: (X, Y, n) => sdCone(P3(X, Y, n), P3(-0.5, 50.5, -0.5), P3(-7, 44, -4.5), 2.2, 0.2), color: artColor(arm) };
  const fingers = [[1, 5], [3.8, 6], [6.6, 5.5], [9, 4]].map(([x, len]) => ({ seg: 'fore', sdf: (X, Y, n) => sdCone(P3(X, Y, n), P3(x + 0.5, 68.5, 1), P3(x + 0.5, 69.5 + len, 1.8), 1.2, 0.15), color: artColor(arm) }));
  const plates = [[[[-5, 38], [-3, 30], [3, 27], [5, 33], [0, 40]], 0.5], [[[2, 33], [5, 25], [10, 27], [12, 34], [6, 38]], -2.5], [[[-2, 36], [4, 31], [11, 33], [10, 40], [1, 41]], 3]]
    .map(([P, n0]) => ({ seg: 'cap', sdf: slab(P.map(([x, y]) => [x + 0.5, y + 0.5]), n0, 4.2), color: artColor(sh) }));
  return [armPrim('sgArm', 4, [...latheParts(La).map(q => ({ ...q, color: latheColor(arm) })), shard, ...fingers], F), armPrim('sgCap', 4, plates, F)];
};
// the synthetic hand's fingers (arms.js synthHand): four jointed fingers down from the knuckle caps, the outer two
// shorter and leaning out, curling a little forward
const synthFingers = (l, top = 61, long = 11) => [[-0.5, long - 2, -0.35], [2.5, long, -0.12], [5.5, long, 0.12], [8.5, long - 2, 0.35]].map(([x0, len, lean], i) => {
  const flex = Math.sin(ARM_ST.t * 1.8 + i) * 0.15, ky = top + 5, pts = [];
  for (let j = 0; j <= len; j += 2) pts.push([x0 + 1 + (lean + flex * (j / len)) * j, ky + 2 + j, 1 + j * 0.12]);
  return { seg: 'fore', sdf: (X, Y, n) => tube(pts, 1.05)(P3(X, Y, n)), color: artColor(l) };
});
// HOMUNCULUS (Plated): steel plates with mesh panels, black cable bundles in the gap, a hinge disc at the elbow; the
// piston and its spring run down the inner side (a rod standing off it); the synthetic hand; a rounded steel cap with a
// mesh vent, two black cables arching off it out over the upper arm.
ARM3D['homunculus-plated'] = (F, [hose, arm, sh]) => {
  const La = latheRows(arm, { lo: -1.5, hi: 10, y0: 34, y1: 67 }), Ls = latheRows(sh, { lo: -10, hi: 20, dz: 1.0 });
  const rod = { seg: 'fore', sdf: (X, Y, n) => Math.min(sdCapsule(P3(X, Y, n), P3(11, 41.5, 0), P3(11, 58.5, 0), 1), sdTube(v3(X, Y, n), 11, 0, 47, 54.5, 1.7, 1.7, 0.3)), color: artColor(arm) };
  const cables = [[-3, -1, -7, -1], [-2, 0, -5, 1.2]].map(([ax, bx, bulge, n0]) => { const pts = []; for (let i = 0; i <= 14; i++) { const u = i / 14; pts.push([lerp(ax, bx, u) + Math.sin(u * Math.PI) * (bulge - ax) + 0.5, lerp(34, 47, u), n0]); }
    return { seg: 'upper', sdf: (X, Y, n) => tube(pts, 1.05)(P3(X, Y, n)), color: artColor(hose) }; });
  return [armPrim('hpHose', 2, cables, F), armPrim('hpArm', 4, [...latheParts(La).map(q => ({ ...q, color: latheColor(arm) })), rod, ...synthFingers(arm)], F), armPrim('hpCap', 4, latheParts(Ls, 'cap'), F, latheColor(sh))];
};
// HOMUNCULUS (Hydraulic): a clean steel shell with panel lines and status lights, a pivot pin on the outer elbow, the
// synthetic hand, and two thick ribbed hoses looping out off the outer side, shoulder to elbow and elbow to wrist.
// Glow (not drawn): the status lights.
ARM3D['homunculus-hydraulic'] = (F, [shHose, hose, arm, sh]) => {
  const La = latheRows(arm, { lo: -1.5, hi: 10.5, y0: 34, y1: 67, skip: (x, y) => x < -0.5 && y > 46 && y < 53 }), Ls = latheRows(sh, { lo: -10, hi: 20, dz: 1.0 });
  const pin = { seg: 'fore', sdf: (X, Y, n) => sdCapsule(P3(X, Y, n), P3(-3, 50, 0), P3(0.5, 50, 0), 2), color: artColor(arm) };
  const loop = (l, ay, by, bulge, seg) => { const pts = []; for (let i = 0; i <= 18; i++) { const u = i / 18; pts.push([-1 - Math.sin(u * Math.PI) * bulge + 0.5, lerp(ay, by, u) + 0.5, 0]); } return { seg, sdf: (X, Y, n) => tube(pts, 1.5)(P3(X, Y, n)), color: artColor(l) }; };
  return [armPrim('hhShHose', 2, [loop(shHose, 34, 48, 6, 'upper')], F), armPrim('hhHose', 2, [loop(hose, 51, 61, 5, 'fore')], F),
          armPrim('hhArm', 4, [...latheParts(La).map(q => ({ ...q, color: latheColor(arm) })), pin, ...synthFingers(arm)], F), armPrim('hhCap', 4, latheParts(Ls, 'cap'), F, latheColor(sh))];
};
// FULMEN (Cage): no body - lightning in the arm's shape. The approved bolts (the same frame of zap()) laid on a cage
// round the arm: the two down the edges at the arm's sides, the cross zigzags over the front and again over the back,
// sparks off the fingers; the shoulder a ring of bolts in the front plane and a second through the side, so it is a cage
// from every view. White core, blue fringe, thin outline. Glows (not drawn): shoulder and arm, blue.
function zapPts(ax, ay, bx, by, seed, frame, jag = 3, n = 5) {
  const pts = [[ax, ay]]; for (let i = 1; i <= n; i++) { const k = i / n; pts.push([lerp(ax, bx, k) + (i === n ? 0 : (hash2(frame * 13 + seed, i) - 0.5) * jag * 2), lerp(ay, by, k) + (i === n ? 0 : (hash2(seed * 7 + i, frame) - 0.5) * jag)]); } return pts;
}
ARM3D['fulmen-cage'] = (F, [arm, sh]) => {
  const frame = Math.floor(ARM_ST.t * 12), R = 5.2, onCage = (x, back) => { const s = Math.max(-1, Math.min(1, (x + 0.5 - 4.75) / R)); return (back ? -1 : 1) * R * Math.sqrt(1 - s * s) * 0.92; };
  const bolts = [];   // [points [X,Y,n], seg]
  const add = (pts2, n, seg) => bolts.push([pts2.map(([x, y]) => [x + 0.5, y + 0.5, typeof n === 'function' ? n(x) : n]), seg]);
  const split = (pts2, nf) => { const up = pts2.filter(([, y]) => y < ELBOW_Y), lo = pts2.filter(([, y]) => y >= ELBOW_Y - 1); if (up.length > 1) add(up, nf, 'upper'); if (lo.length > 1) add(lo, nf, 'fore'); };
  split(zapPts(-0.5, 37, -0.5, 64, 1, frame, 1.2, 7), 0); split(zapPts(9.5, 37, 9, 64, 2, frame, 1.2, 7), 0);
  for (let i = 0; i < 5; i++) { const y = 39 + i * 6, p = zapPts(i % 2 ? 9 : 0, y, i % 2 ? 0 : 9, y + 4, 3 + i, frame, 1, 3); split(p, x => onCage(x, false)); split(p, x => onCage(x, true)); }
  add(zapPts(0, 64, 9, 64, 9, frame, 1, 3), x => onCage(x, false), 'fore'); add(zapPts(0, 64, 9, 64, 9, frame, 1, 3), x => onCage(x, true), 'fore');
  for (const [bx, ex, ey, sd] of [[0.5, -1.5, 73, 11], [3.5, 3, 75, 12], [6.5, 7, 75, 13], [9, 11, 72, 14]]) add(zapPts(bx, 64, ex, ey, sd, frame, 1.2, 3), 1.5, 'fore');
  const ring = [], ring2 = [];
  for (let i = 0; i < 8; i++) { const a0 = i / 8 * Math.PI * 2, a1 = (i + 1) / 8 * Math.PI * 2, p = zapPts(4.5 + Math.cos(a0) * 6.5, 33 + Math.sin(a0) * 6, 4.5 + Math.cos(a1) * 6.5, 33 + Math.sin(a1) * 6, 20 + i, frame, 0.8, 2);
    ring.push(p.map(([x, y]) => [x + 0.5, y + 0.5, 0]));
    ring2.push(p.map(([x, y]) => [4.75, y + 0.5, (x - 4.5) * 0.95])); }
  const boltColor = (X, Y, n, nv) => nv.z > 0.55 ? '#ffffff' : tone(AR.volt, 4);
  const armParts = bolts.map(([pts, seg]) => ({ seg, sdf: (X, Y, n) => tube(pts, 0.75)(P3(X, Y, n)) }));
  const capParts = [...ring, ...ring2].map(pts => ({ seg: 'cap', sdf: (X, Y, n) => tube(pts, 0.75)(P3(X, Y, n)) }));
  return [armPrim('fcArm', 2, armParts, F, boltColor), armPrim('fcCap', 2, capParts, F, boltColor)];
};
// PENUMBRA (Echo): a dark see-through arm. The ghost column and fist wrap, the shoulder a dome, four faint finger
// strokes; the arm keeps the approved idle drift (the shoulder half of it). Drawn at the approved 0.72 alpha over
// the figure. Its two fainter echoes trail it only when it moves - not shown in these stills.
ARM3D['penumbra-echo'] = (F, [arm, sh]) => {
  const [dx, dy] = drift(ARM_ST.t), sx = Math.round(dx), sy = Math.round(dy), hx = Math.round(dx * 0.5), hy = Math.round(dy * 0.5);
  const La = latheRows(arm, { lo: -1.5 + sx, hi: 10.5 + sx, ax: 5 + sx, y1: 68 + sy }), Ls = latheRows(sh, { lo: -10, hi: 20, dz: 0.95, ax: 5 + hx });
  const strokes = [[1.5, 70], [4, 71], [6.5, 70], [9, 69]].map(([x, y]) => ({ seg: 'fore', sdf: (X, Y, n) => sdCapsule(P3(X, Y, n), P3(x + 0.5 + sx, 68.5 + sy, 0.5), P3(x + (x - 5) * 0.2 + 0.5 + sx, y + 2.5 + sy, 1.5), 0.75), color: () => tone(AR.ghost, 3) }));
  const ghostColor = l => (X, Y, n, nv, q) => { const row = latheRow(q.L, Y), sxx = (X - row.m) / row.h, lt = litTone(nv.x, -nv.y, nv.z);
    // the violet rim: the flat art's lit left edge, here the silhouette's lit side
    if (Math.abs(nv.z) < 0.35 && nv.x < -0.2) return tone(AR.violet, 3);
    return tone(AR.ghost, Math.max(1, Math.min(5, lt))); };
  const out = [armPrim('peArm', 4, [...latheParts(La).map(q => ({ ...q, color: ghostColor(arm) })), ...strokes], F), armPrim('peCap', 4, latheParts(Ls, 'cap'), F, ghostColor(sh))];
  out.forEach(p => p.alpha = 0.72);
  return out;
};
ARM3D.quicksilver = F => quicksilverA(F);
ARM3D.salt = F => saltA(F);
// simpler wraps first, the costly ones (cage, hoses, echo) last
const ARM3D_ORDER = ['quicksilver', 'rubedo', 'verdigris', 'salamander', 'salt', 'crucible', 'sanguis-crystal', 'homunculus-plated', 'homunculus-hydraulic', 'fulmen-cage', 'penumbra-echo'];
const ARM3D_GLOWS = { quicksilver: 'a faint white sheen on the upper arm', rubedo: 'the Stone in the hand, red, pulsing', verdigris: 'none', salamander: 'the lava seams, the fist and the shoulder (orange); embers rising off the shoulder',
  salt: 'a pink haze over the shoulder crystals; a glint jumping between crystal tips', crucible: 'the grate and the gauntlet slits (orange, flickering)', 'sanguis-crystal': 'arm and shoulder (red); a glint sweeping the facets',
  'homunculus-plated': 'none', 'homunculus-hydraulic': 'the status lights (cyan, blinking)', 'fulmen-cage': 'shoulder and arm (blue, flickering); the bolts re-strike every frame', 'penumbra-echo': 'none (alpha and echoes instead)' };

const xcompose = ls => ls.some(l => l.alpha != null && l.alpha < 1) ? composeA(ls) : compose(ls);
// ---------------------------------------------------------------- sheet
// rows: { flat: grid|null, flatBack: grid|null, make: () => prims, mirror: bool }
// columns: the approved front art, its back art, then front, front 3/4, side, back 3/4, back (facing right), then,
// for an asymmetric row, front 3/4, side, back 3/4 FACING LEFT (the mirror-image model, flipped as the game draws it).
const XVIEWS = [0, 45, 90, 135, 180], XLEFT = [45, 90, 135];
function xsheet(file, rows, o = {}) {
  const Sc = o.scale || 3, CW = o.cw || 106, CH = XY1 - XY0 + 6, OXC = CW / 2;
  const cols = 2 + XVIEWS.length + (rows.some(r => r.mirror) ? XLEFT.length : 0) + (o.extraCols || 0);
  const W = cols * CW * Sc, H = rows.length * CH * Sc, buf = Buffer.alloc(W * H * 4);
  for (let i = 0; i < W * H; i++) { buf[i * 4] = 0x4b; buf[i * 4 + 1] = 0x4e; buf[i * 4 + 2] = 0x57; buf[i * 4 + 3] = 255; }
  // the left-facing columns on a slightly cooler ground, so they read as their own group
  const ground = (col, row, rgb) => { for (let y = row * CH * Sc; y < (row + 1) * CH * Sc; y++) for (let x = col * CW * Sc; x < (col + 1) * CW * Sc; x++) { const i = (y * W + x) * 4; buf[i] = rgb[0]; buf[i + 1] = rgb[1]; buf[i + 2] = rgb[2]; } };
  const draw = (grid, col, row, ox, oy) => { for (const [k, c] of grid) { const x = KX(k) + ox + col * CW, y = KY(k) + oy + row * CH; if (KX(k) + ox < 0 || KX(k) + ox >= CW) continue; const [r, g, b] = hex2rgb(c);
    for (let dy = 0; dy < Sc; dy++) for (let dx = 0; dx < Sc; dx++) { const px = x * Sc + dx, py = y * Sc + dy; if (px < 0 || py < 0 || px >= W || py >= H) continue; const i = (py * W + px) * 4; buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; } } };
  rows.forEach((r, ri) => {
    for (let c = 0; c < 2; c++) ground(c, ri, [0x44, 0x46, 0x4e]);
    if (r.mirror) for (let c = 0; c < XLEFT.length; c++) ground(2 + XVIEWS.length + c, ri, [0x45, 0x4a, 0x58]);
    // the flat art: its soles on the views' sole line (row XY1 of the render)
    const soleOy = XY1 + 3 - 120;
    if (r.flat) draw(r.flat instanceof Map ? r.flat : compose(r.flat), 0, ri, OXC - 19, soleOy);
    if (r.flatBack) draw(r.flatBack instanceof Map ? r.flatBack : compose(r.flatBack), 1, ri, OXC - 19, soleOy);
    const prims = r.make();
    XVIEWS.forEach((yaw, c) => { draw(xcompose([...xrender(prims, yaw), ...(prims.extra ? prims.extra(yaw, false) : [])]), 2 + c, ri, OXC, 3); });
    if (r.mirror) { const mp = prims.map(xmirrored); if (prims.splats) mp.splats = prims.splats.map(sp => ({ ...sp, pts: sp.pts.map(v => ({ ...v, p: xmx(v.p) })) })); XLEFT.forEach((yaw, c) => draw(flipGrid(xcompose([...xrender(mp, yaw), ...(prims.extra ? prims.extra(yaw, true) : [])])), 2 + XVIEWS.length + c, ri, OXC, 3)); }
    process.stderr.write(file + ' row ' + ri + '\n');
  });
  const crcT = []; for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; crcT[n] = c >>> 0; }
  const crc = b => { let c = 0xffffffff; for (const x of b) c = crcT[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const chunk = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ih = Buffer.alloc(13); ih.writeUInt32BE(W, 0); ih.writeUInt32BE(H, 4); ih[8] = 8; ih[9] = 6;
  const raw = Buffer.alloc((W * 4 + 1) * H); for (let y = 0; y < H; y++) buf.copy(raw, y * (W * 4 + 1) + 1, y * W * 4, (y + 1) * W * 4);
  require('fs').writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ih), chunk('IDAT', require('zlib').deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]));
  console.log(file, W + 'x' + H);
}

// a GIF89a writer (one global palette, LZW), looping
function writeGif(file, W, H, frames, delayCs) {
  const pal = new Map(), idx = [];
  for (const fr of frames) { const a = new Uint8Array(W * H); for (let k = 0; k < W * H; k++) { const key = (fr[k * 3] << 16) | (fr[k * 3 + 1] << 8) | fr[k * 3 + 2]; let v = pal.get(key);
      if (v === undefined) { if (pal.size < 256) { v = pal.size; pal.set(key, v); } else { let bd = 1e9; for (const [kk, vv] of pal) { const d = Math.abs((kk >> 16) - fr[k * 3]) + Math.abs(((kk >> 8) & 255) - fr[k * 3 + 1]) + Math.abs((kk & 255) - fr[k * 3 + 2]); if (d < bd) { bd = d; v = vv; } } } }
      a[k] = v; } idx.push(a); }
  const out = []; const b = (...x) => out.push(...x), w16 = v => b(v & 255, v >> 8);
  b(...Buffer.from('GIF89a')); w16(W); w16(H); b(0xf7, 0, 0);
  const ct = Buffer.alloc(768); for (const [k, v] of pal) { ct[v * 3] = k >> 16; ct[v * 3 + 1] = (k >> 8) & 255; ct[v * 3 + 2] = k & 255; } out.push(...ct);
  b(0x21, 0xff, 11, ...Buffer.from('NETSCAPE2.0'), 3, 1, 0, 0, 0);
  for (const a of idx) {
    b(0x21, 0xf9, 4, 0, delayCs & 255, delayCs >> 8, 0, 0); b(0x2c); w16(0); w16(0); w16(W); w16(H); b(0); b(8);
    // LZW, 8-bit minimum code size
    const bytes = []; let cur = 0, nb = 0; const emit = (code, size) => { cur |= code << nb; nb += size; while (nb >= 8) { bytes.push(cur & 255); cur >>>= 8; nb -= 8; } };
    let dict = new Map(), next = 258, size = 9; emit(256, size);
    let pre = a[0];
    for (let k = 1; k < a.length; k++) { const c = a[k], key = pre * 4096 + c, hit = dict.get(key);
      if (hit !== undefined) { pre = hit; continue; }
      emit(pre, size); if (next < 4096) { dict.set(key, next++); if (next > (1 << size) && size < 12) size++; } else { emit(256, size); dict = new Map(); next = 258; size = 9; }
      pre = c; }
    emit(pre, size); emit(257, size); if (nb > 0) bytes.push(cur & 255);
    for (let k = 0; k < bytes.length; k += 255) { const ch = bytes.slice(k, k + 255); b(ch.length, ...ch); } b(0);
  }
  b(0x3b); require('fs').writeFileSync(file, Buffer.from(out)); console.log(file, W + 'x' + H, frames.length + ' frames');
}

// ---------------------------------------------------------------- main
{
  const outDir = process.argv[2] || '.', what = process.argv[3] || 'scarves', path = require('path');
  const on = (wr, extra) => () => { const base = WEARERS[wr].make(); return [...base, ...extra(base)]; };
  if (what === 'scarves') {
    // one colourway (azure: scarlet vanishes on Hoplite's red cape), on Hoplite (the bare body is not worn in play)
    const r = SCARF.azure; XHALF = 102;   // the streaming tail reaches past the usual cell
    xsheet(path.join(outDir, 'scarf-flow.png'), ['stand', 'walk', 'run'].map((gait, i) => ({
      flat: i ? null : [...WEARERS.hoplite.flat(), ...scarfFlat(r)], flatBack: i ? null : [...WEARERS.hoplite.flatBack(), ...scarfFlat(r, true)],
      make: on('hoplite', b => scarf3d(r, b, { gait, phase: 0.25 })), mirror: true })), { cw: 208 });
  }
  if (what === 'scarfgif') {
    XHALF = 102;   // the streaming tail reaches past the usual cell
    // the eight directions (five facing right, three facing left) x stand / walk / run, one loop of FRAMES frames
    const r = SCARF.azure, FRAMES = +(process.argv[4] || 12), Sc = 2, CW = 206, Y0C = 22, Y1C = XY1 - XY0, CH = Y1C - Y0C;
    const cols = [...XVIEWS.map(y => [y, false]), ...XLEFT.map(y => [y, true])], gaits = ['stand', 'walk', 'run'];
    const W = cols.length * CW * Sc, H = gaits.length * CH * Sc, frames = [];
    for (let fi = 0; fi < FRAMES; fi++) {
      const buf = Buffer.alloc(W * H * 3);
      for (let k = 0; k < W * H; k++) { buf[k * 3] = 0x4b; buf[k * 3 + 1] = 0x4e; buf[k * 3 + 2] = 0x57; }
      gaits.forEach((gait, ri) => {
        const base = WEARERS.hoplite.make(), prims = [...base, ...scarf3d(r, base, { gait, phase: fi / FRAMES })], mp = prims.map(xmirrored);
        cols.forEach(([yaw, left], ci) => {
          let grid = compose(xrender(left ? mp : prims, yaw)); if (left) grid = flipGrid(grid);
          for (const [k, c] of grid) { const x = KX(k) + CW / 2, y = KY(k) - Y0C; if (x < 0 || x >= CW || y < 0 || y >= CH) continue; const [cr, cg, cb] = hex2rgb(c);
            for (let dy = 0; dy < Sc; dy++) for (let dx = 0; dx < Sc; dx++) { const px = (ci * CW + x) * Sc + dx, py = (ri * CH + y) * Sc + dy, o = (py * W + px) * 3; buf[o] = cr; buf[o + 1] = cg; buf[o + 2] = cb; } }
        });
      });
      frames.push(buf); process.stderr.write('frame ' + fi + '\n');
    }
    writeGif(path.join(outDir, 'scarf-flow.gif'), W, H, frames, 8);
  }
  if (what === 'armcmp' || what.startsWith('armcmp:')) {
    // the two arm pieces, 3D build (A) against side art (B), on Hoplite's right arm: at rest, swung forward, swung back
    const ids = what.startsWith('armcmp:') ? what.slice(7).split(',') : Object.keys(ARMCMP), hop = SETS.find(s => s.id === 'hoplite');
    for (const id of ids) {
      const piece = ARMS.find(a => a.id === id), rows = [];
      for (const [pose, sw, b] of ARM_POSES) for (const how of ['A', 'B']) {
        const F = armFrames(deg(sw), deg(b)), rest = pose === 'rest';
        rows.push({ flat: rest ? [...wearArmFront(hop), ...piece.draw({ ...ARM_ST, sp: (x, y) => [x, y] }, [])] : null,
          flatBack: rest ? [...wearArmBack(hop), ...armBackRank(piece.draw({ ...ARM_ST, sp: (x, y) => [x, y] }, []))] : null,
          make: () => { const base = withoutNearArm(WEARERS.hoplite.make()); if (how === 'A') return [...base, ...ARMCMP[id].A(F)]; base.extra = (yaw, mirror) => armB(ARMCMP[id].B, F, yaw, mirror); return base; },
          mirror: true });
      }
      xsheet(path.join(outDir, 'armcmp-' + id + '.png'), rows);
    }
  }
  if (what === 'arm3d' || what.startsWith('arm3d:')) {
    // each arm piece built in 3D, on Hoplite's right arm: at rest, swung forward, swung back (mock8's run pose)
    const ids = what.startsWith('arm3d:') ? what.slice(6).split(',') : ARM3D_ORDER, hop = SETS.find(s => s.id === 'hoplite');
    for (const id of ids) {
      const piece = ARMS.find(a => a.id === id), layers = artLayers(piece), alpha = layers.some(l => l.alpha != null);
      const flatF = [...wearArmFront(hop), ...artLayers(piece)], flatB = [...wearArmBack(hop), ...armBackRank(artLayers(piece))];
      const rows = ARM_POSES.map(([pose, sw, b]) => ({
        flat: pose === 'rest' ? (alpha ? composeA(flatF) : flatF) : null, flatBack: pose === 'rest' ? (alpha ? composeA(flatB) : flatB) : null,
        make: () => { const base = withoutNearArm(WEARERS.hoplite.make()), parts = ARM3D[id](armFrames(deg(sw), deg(b)), artLayers(piece), base);
          // the arm stacks over the torso's pieces where both show (the flat rig's ArmFront over TorsoArmor): only outlines are at stake, texels go by depth
          for (const p of parts) if (!/^cb(Strap|Buckle)/.test(p.piece)) p.depthBias = (p.depthBias || 0) + 6;
          return [...base, ...parts]; }, mirror: true }));
      xsheet(path.join(outDir, 'arm3d-' + id + '.png'), rows);
    }
  }
  if (what === 'accessories' || what.startsWith('acc:')) {
    const only = what.startsWith('acc:') ? what.slice(4).split(',') : null;
    for (const a of ACC3D) {
      if (only && !only.includes(a.acc.id)) continue;
      xsheet(path.join(outDir, 'acc-' + a.acc.id + '.png'), (a.acc.id === 'collar' ? ['bare', 'hoplite', 'wayfarer', 'athanor'] : ['bare', 'hoplite']).map(wr => ({
        flat: accFlat(a.acc, wr), flatBack: accFlatBack(a.acc, wr), make: on(wr, b => a.make(b)), mirror: a.mirror })));
    }
  }
  if (what === 'arms' || what.startsWith('arm:')) {
    const only = what.startsWith('arm:') ? what.slice(4).split(',') : null;
    const hop = SETS.find(s => s.id === 'hoplite');
    const armRow = (a, wr) => ({
      flat: [...wearArmFront(wr === 'bare' ? null : hop), ...a.draw({ ...ARM_ST, sp: (x, y) => [x, y] }, [])],
      flatBack: [...wearArmBack(wr === 'bare' ? null : hop), ...armBackRank(a.draw({ ...ARM_ST, sp: (x, y) => [x, y] }, []))],
      make: () => { const b = withoutNearArm(WEARERS[wr].make()); b.splats = armPiece3d(a, b); return b; }, mirror: true });
    const list = ARMS.filter(a => !only || only.includes(a.id));
    // one sheet per four pieces, bare; and the first piece on Hoplite too
    for (let i = 0; i < list.length; i += 4) xsheet(path.join(outDir, 'arms-' + (i / 4 + 1) + '.png'), list.slice(i, i + 4).map(a => armRow(a, 'bare')));
    if (!only) xsheet(path.join(outDir, 'arms-hoplite.png'), list.slice(0, 4).map(a => armRow(a, 'hoplite')));
  }
}
