// EIGHT DIRECTIONS (RIG.md, adopted 2026-11-09): every piece is defined ONCE as 3D shapes and rendered in
// five views - front, front 3/4, side, back 3/4, back (the left side mirrors the right). Shapes are signed
// distance functions in the body's frame: x the character's left (screen right in the front view), y up
// from the soles, z forward. A view turns the body by its yaw; each texel is a ray straight into the
// screen, shaded by the kit's own light (litTone) in the set's ramps, and every piece becomes one of the
// kit's Layers - so compose() outlines and stacks them exactly as the front art is.
// node: cat body.js kit.js sets.js views.js | node - <out.png> [scale]
const VIEWS = [['front', 0], ['front3q', 45], ['side', 90], ['back3q', 135], ['back', 180]];
let TOP = 156, HALFW = 48;   // texel row 0 is this height above the soles

// ---------------------------------------------------------------- shapes
const v3 = (x, y, z) => ({ x, y, z });
const len3 = p => Math.hypot(p.x, p.y, p.z);
const sdEllipsoid = (p, c, r) => { const q = v3((p.x - c.x) / r.x, (p.y - c.y) / r.y, (p.z - c.z) / r.z), k0 = len3(q), k1 = len3(v3(q.x / r.x, q.y / r.y, q.z / r.z)); return k1 === 0 ? -Math.min(r.x, r.y, r.z) : k0 * (k0 - 1) / k1; };
const sdRoundBox = (p, c, b, rad) => { const q = v3(Math.abs(p.x - c.x) - b.x + rad, Math.abs(p.y - c.y) - b.y + rad, Math.abs(p.z - c.z) - b.z + rad); return len3(v3(Math.max(q.x, 0), Math.max(q.y, 0), Math.max(q.z, 0))) + Math.min(Math.max(q.x, q.y, q.z), 0) - rad; };
const sdCapsule = (p, a, b, r) => { const pa = v3(p.x - a.x, p.y - a.y, p.z - a.z), ba = v3(b.x - a.x, b.y - a.y, b.z - a.z); const h = Math.max(0, Math.min(1, (pa.x * ba.x + pa.y * ba.y + pa.z * ba.z) / (ba.x * ba.x + ba.y * ba.y + ba.z * ba.z))); return len3(v3(pa.x - ba.x * h, pa.y - ba.y * h, pa.z - ba.z * h)) - r; };
// an upright elliptic cylinder from y0 to y1 whose half-widths rx(y), rz(y) may vary with height, rounded at the rims
const sdTube = (p, cx, cz, y0, y1, rx, rz, round = 2) => {
  const ax = typeof rx === 'function' ? rx(p.y) : rx, az = typeof rz === 'function' ? rz(p.y) : rz;
  const e = (Math.hypot((p.x - cx) / ax, (p.z - cz) / az) - 1) * Math.min(ax, az);
  const h = Math.max(y0 - p.y, p.y - y1);
  const qx = Math.max(e + round, 0), qy = Math.max(h + round, 0);
  return Math.hypot(qx, qy) + Math.min(Math.max(e + round, h + round), 0) - round;
};
const smooth = t => { t = Math.max(0, Math.min(1, t)); return t * t * (3 - 2 * t); };
const lit = (r, n, bias = 0) => tone(r, litTone(n.x, -n.y, n.z) + bias);

// ---------------------------------------------------------------- the bare body (62-texel legs)
const BODY_M = {
  skin: ramp('#EBC7A8', { sheen: '#fff0e2', lift: 0.30 }), hair: ramp('#BDBFC5', { sheen: '#ffffff', lift: 0.40 }),
  suit: ramp('#3D3D44', { sheen: '#9a9aa8', lift: 0.30 }), eye: '#1F242B', eyeLit: '#FFFFFF',
};
const HIP_Y = 62, SHOULDER_Y = 103, HEAD = v3(0, 121, 0), LEG_X = 8, ARM_X = 16.5;
const pieceDefs = {};
// ---- the arms: shoulder, elbow, wrist; a hand with no target hangs. Set POSE.hands[s] (s = 1 the character's
// left, -1 the right) to a point in the body's frame and the arm reaches it, elbow out and back.
const ARM_UP = 12, ARM_FORE = 25;
const POSE = { hands: {} };
function arm(s) {
  const sh = v3(s * ARM_X, 101, 0);
  let hand = POSE.hands[s] || v3(s * (ARM_X + 1.5), 66, 1);
  let d = v3(hand.x - sh.x, hand.y - sh.y, hand.z - sh.z), L = len3(d);
  const reach = ARM_UP + ARM_FORE - 0.01;
  if (L > reach) { d = v3(d.x * reach / L, d.y * reach / L, d.z * reach / L); L = reach; hand = v3(sh.x + d.x, sh.y + d.y, sh.z + d.z); }
  const a = (ARM_UP * ARM_UP - ARM_FORE * ARM_FORE + L * L) / (2 * L), h = Math.sqrt(Math.max(0, ARM_UP * ARM_UP - a * a));
  const dn = v3(d.x / L, d.y / L, d.z / L);
  const hint = v3(s * 0.6, -0.1, -1); const k = hint.x * dn.x + hint.y * dn.y + hint.z * dn.z;
  let pole = v3(hint.x - dn.x * k, hint.y - dn.y * k, hint.z - dn.z * k); const pl = len3(pole) || 1; pole = v3(pole.x / pl, pole.y / pl, pole.z / pl);
  const el = v3(sh.x + dn.x * a + pole.x * h, sh.y + dn.y * a + pole.y * h, sh.z + dn.z * a + pole.z * h);
  return { sh, el, wr: hand };
}
const lerp3 = (a, b, t) => v3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
// how far along the forearm a point lies, 0 at the elbow, 1 at the wrist
const alongFore = (p, A) => { const ab = v3(A.wr.x - A.el.x, A.wr.y - A.el.y, A.wr.z - A.el.z), ap = v3(p.x - A.el.x, p.y - A.el.y, p.z - A.el.z); return (ap.x * ab.x + ap.y * ab.y + ap.z * ab.z) / (ab.x * ab.x + ab.y * ab.y + ab.z * ab.z); };

function body3d() {
  return [
    { piece: 'head', depthBias: 0, outR: 2, sdf: p => sdRoundBox(p, HEAD, v3(15.5, 14, 13.5), 6),
      color: (p, n) => {
        const front = p.z > 3, hairLine = 124 - Math.max(0, -p.z) * 0.9;
        if (p.y > hairLine || p.z < -7 && p.y > 110) return lit(BODY_M.hair, n, 1);
        for (const ex of [-6.5, 6.5]) if (front && Math.abs(p.x - ex) < 2.2 && p.y > 114.5 && p.y < 119.5) return (p.x - ex < -0.6 && p.y > 117.5) ? BODY_M.eyeLit : BODY_M.eye;
        return lit(BODY_M.skin, n, 1);
      } },
    { piece: 'torso', outR: 2, sdf: p => sdTube(p, 0, 0, HIP_Y - 2, SHOULDER_Y + 3, y => 11 + 2.5 * smooth((y - 70) / 30), 8, 4), color: (p, n) => lit(BODY_M.suit, n, 1) },
    ...[1, -1].map(s => { const A = arm(s); return { piece: s > 0 ? 'armL' : 'armR', outR: 2, sdf: p => Math.min(sdCapsule(p, A.sh, A.el, 4.2), sdCapsule(p, A.el, A.wr, 4.1)), color: (p, n) => lit(BODY_M.suit, n, 1) }; }),
    ...[1, -1].map(s => ({ piece: s > 0 ? 'legL' : 'legR', outR: 2, sdf: p => sdCapsule(p, v3(s * LEG_X, HIP_Y, 0), v3(s * LEG_X, 4, 0), 4.2), color: (p, n) => lit(BODY_M.suit, n, 1) })),
    ...[1, -1].map(s => ({ piece: s > 0 ? 'legL' : 'legR', outR: 2, sdf: p => sdRoundBox(p, v3(s * LEG_X, 2.5, 2), v3(4, 2.5, 6), 2), color: (p, n) => lit(BODY_M.suit, n, 0) })),
  ];
}

// ---------------------------------------------------------------- Bronze I - Hoplite
const HOP = {
  bronze: ramp('#B87333', { sheen: '#ffd98a', lift: 0.36 }), bronzeDk: ramp('#7f4c22', { sheen: '#e9a65a' }),
  leather: ramp('#7a4a2a', { sheen: '#d9a070', lift: 0.28 }), leatherDk: ramp('#4a2e1c', { sheen: '#a8774f', lift: 0.28 }),
  red: ramp('#9b2a24', { sheen: '#ff9a72', lift: 0.30 }),
};
function hoplite3d() {
  const B = HOP;
  const helmC = v3(0, 122, 0.5), helmR = v3(19, 16, 17.5);
  return [
    // the helm: a dome with cheek guards, the face open in a T (the eyes' band and a slot down the middle)
    { piece: 'helm', outR: 4, sdf: p => {
        const shell = sdRoundBox(p, v3(0, 122.5, 0.5), v3(19, 16, 17), 9.5);
        const below = 104 - p.y;                                          // nothing under the jaw
        // open-faced: the cheek guards come down either side, the face between them is clear
        const face = p.z > 0 && p.y < 126 && Math.abs(p.x) < 13.5 ? 0.8 : -99;
        const cut = face;
        return Math.max(shell, below, cut);
      },
      color: (p, n) => {
        if (p.y > 126 && p.y < 128.5 && p.z > -4) return tone(B.bronze, p.y > 127.5 ? 5 : 4);     // the brow band
        if (p.z > 6 && Math.abs(p.x) < 1 && p.y > 127 && p.y < 136) return tone(B.bronze, 2);      // the ridge
        return lit(B.bronze, n, 1);
      } },
    { piece: 'crest', outR: 4, depthBias: -40, sdf: p => Math.max(sdEllipsoid(p, v3(0, 135, -1), v3(20, 15, 2.8)), 135 - p.y),
      color: (p, n) => { const a = Math.atan2(p.y - 135, p.x), k = Math.floor(a / Math.PI * 22); return tone(B.red, (k % 2 ? 2 : 3) + (p.y > 147 ? 1 : 0)); } },
    { piece: 'crest', outR: 4, sdf: p => sdRoundBox(p, v3(0, 138.5, -1), v3(3, 2.5, 3), 1), color: (p, n) => lit(B.bronzeDk, n, 1) },
    // the cuirass: the torso's tube a texel and a half out, pecs at the front
    { piece: 'cuirass', outR: 4, sdf: p => {
        const t = sdTube(p, 0, 0.3, 73, SHOULDER_Y + 4, y => 12.5 + 2.5 * smooth((y - 70) / 30), 9.5, 4);
        const pecs = Math.min(sdEllipsoid(p, v3(-5.5, 94, 7.6), v3(6.5, 5.5, 3)), sdEllipsoid(p, v3(5.5, 94, 7.6), v3(6.5, 5.5, 3)));
        return Math.min(t, pecs);
      },
      color: (p, n) => {
        if (p.y > SHOULDER_Y + 2) return tone(B.bronze, 5);                                   // the collar
        if (p.z > 5 && Math.abs(p.x) < 0.8 && p.y > 70 && p.y < 101) return tone(B.bronze, 1); // the sternum
        if (p.z > 5 && Math.abs(p.y - (88 + Math.abs(p.x) * 0.25)) < 0.6 && Math.abs(p.x) > 1.5) return tone(B.bronze, 1);
        if (p.z > 5 && [80, 74].some(y => Math.abs(p.y - (y + Math.abs(p.x) * 0.15)) < 0.5) && Math.abs(p.x) > 1.5 && Math.abs(p.x) < 7) return tone(B.bronze, 2);
        if (p.z < -6 && Math.abs(p.x) < 0.8 && p.y > 68) return tone(B.bronze, 2);            // the spine, behind
        return lit(B.bronze, n, 1);
      } },
    ...[1, -1].map(s => ({ piece: s > 0 ? 'pauldronL' : 'pauldronR', outR: 4, sdf: p => sdEllipsoid(p, v3(s * 15.5, 101.5, 0), v3(9, 7.5, 9)),
      color: (p, n) => { const r = Math.hypot((p.x - s * 15.5) / 9, (p.z) / 9, (p.y - 101.5) / 7.5 * 0.6); if (r > 0.86) return lit(B.bronzeDk, n, 1); if (Math.abs(p.z) < 1 && Math.abs(p.y - 103) < 1 && Math.abs(p.x - s * 20) < 1.5) return tone(B.bronze, 5); return lit(B.bronze, n, 1); } })),
    // the belt and the leather strips hanging from it
    { piece: 'belt', outR: 4, sdf: p => sdTube(p, 0, 0.3, 71.5, 77.5, 13.8, 10.6, 1.5),
      color: (p, n) => { const a = Math.atan2(p.z, p.x), k = Math.round(a / (Math.PI / 9)); const ax = 13.5 * Math.cos(k * Math.PI / 9), az = 10.5 * Math.sin(k * Math.PI / 9); return Math.hypot(p.x - ax, p.z - az) < 1.3 && Math.abs(p.y - 74.5) < 1.2 ? tone(B.bronze, 5) : lit(B.leatherDk, n, 1); } },
    { piece: 'skirt', outR: 4, sdf: p => {
        const a = Math.atan2(p.z, p.x), k = Math.round(a / (Math.PI / 8)), mid = k * Math.PI / 8;
        const cx = 13.2 * Math.cos(mid), cz = 10.2 * Math.sin(mid);
        const tx = -Math.sin(mid), tz = Math.cos(mid);                          // along the ring
        const along = (p.x - cx) * tx + (p.z - cz) * tz, out = (p.x - cx) * Math.cos(mid) + (p.z - cz) * Math.sin(mid);
        const side = Math.abs(Math.sin(mid)) < 0.35;                           // the strips at the hips are shorter
        const y0 = side ? 46 : 41;
        return Math.max(Math.abs(along) - 2.1, Math.abs(out) - 1.1, y0 - p.y, p.y - 72);
      },
      color: (p, n) => { const side = Math.abs(p.z) < 5; return p.y < (side ? 49 : 44) ? lit(B.bronze, n, 1) : lit(B.leather, n, 1); } },
    // the cape: a curved sheet down the back, flaring
    { piece: 'cape', outR: 4, sdf: p => {
        if (p.y > SHOULDER_Y + 1 || p.y < 30) return Math.max(p.y - (SHOULDER_Y + 1), 30 - p.y, 0.5);
        const t = (SHOULDER_Y + 1 - p.y) / (SHOULDER_Y + 1 - 30), w = 17 + 9 * t;
        const zc = -11 - 2.5 * t + 0.012 * p.x * p.x;
        return Math.max(Math.abs(p.z - zc) - 1.2, Math.abs(p.x) - w);
      },
      color: (p, n, nb) => { const fold = Math.abs(((p.x + 30) % 8) - 4) < 0.7 && p.y < 80; return nb.z < 0 ? tone(B.red, litTone(n.x, -n.y, n.z) + 1 - (fold ? 1 : 0)) : tone(B.red, 3 - (fold ? 1 : 0)); } },
    // gloves on the forearms, greaves and sandals on the shins
    ...[1, -1].map(s => { const A = arm(s); return { piece: s > 0 ? 'gloveL' : 'gloveR', outR: 4, sdf: p => sdCapsule(p, lerp3(A.el, A.wr, 0.08), lerp3(A.el, A.wr, 0.68), 5.2),
      color: (p, n) => [0.16, 0.36, 0.56].some(t => Math.abs(alongFore(p, A) - t) < 0.032) ? tone(B.bronze, 4) : lit(B.leather, n, 1) }; }),
    ...[1, -1].map(s => ({ piece: s > 0 ? 'greaveL' : 'greaveR', outR: 4, sdf: p => Math.min(sdCapsule(p, v3(s * LEG_X, 31, 0.5), v3(s * LEG_X, 10, 0.5), 5.2), sdEllipsoid(p, v3(s * LEG_X, 31, 1.5), v3(5.6, 4, 5))),
      color: (p, n) => Math.abs(p.x - s * LEG_X + 0.5) < 0.6 && p.z > 3 && p.y < 28 ? tone(B.bronze, 2) : lit(B.bronze, n, 1) })),
    ...[1, -1].map(s => ({ piece: s > 0 ? 'sandalL' : 'sandalR', outR: 4, sdf: p => sdRoundBox(p, v3(s * LEG_X, 4, 2), v3(4.6, 4, 6.5), 2),
      color: (p, n) => Math.round(p.y) % 3 === 0 ? tone(HOP.leather, 4) : lit(HOP.leatherDk, n, 1) })),
  ];
}

// ---------------------------------------------------------------- shared for the sets
// front-art row -> height, for everything above the hips (the torso keeps its size; the legs grew)
const Y = r => 135 - r;
// quilting on an upright surface: seams every `gap` texels round it, stitches between
const quiltAt = (r, p, n, gap = 4, bias = 0) => {
  const arc = Math.atan2(p.z, p.x) * 12, u = ((arc % gap) + gap) % gap;
  if (u < 0.8) return tone(r, 1 + bias);
  if (u < 1.6) return tone(r, 4 + bias);
  return tone(r, litTone(n.x, -n.y, n.z) + (Math.round(p.y) % 6 === 0 ? 0 : 1) + bias);
};
const M3 = {
  bronze: ramp('#B87333', { sheen: '#ffd98a', lift: 0.36 }), bronzeDk: ramp('#7f4c22', { sheen: '#e9a65a' }),
  leather: ramp('#7a4a2a', { sheen: '#d9a070', lift: 0.28 }), leatherDk: ramp('#4a2e1c', { sheen: '#a8774f', lift: 0.28 }),
  linen: ramp('#c2ad7e', { sheen: '#fff6dc', lift: 0.30 }), olive: ramp('#56602f', { sheen: '#d8e09a', lift: 0.30 }),
  teal: ramp('#2a6a68', { sheen: '#9fe8d8' }), red: ramp('#9b2a24', { sheen: '#ff9a72', lift: 0.30 }),
};
const bothSides = (name, make) => [1, -1].map(s => ({ ...make(s), piece: name + (s > 0 ? 'L' : 'R') }));

// ---------------------------------------------------------------- Bronze II - Wayfarer
function wayfarer3d() {
  const B = M3;
  return [
    // the padded cap down to the brow, quilted, a bronze-studded band; ear flaps either side
    { piece: 'helm', outR: 4, sdf: p => Math.max(sdRoundBox(p, v3(0, 124, 0), v3(18, 13.5, 16), 9), 125.5 - p.y),
      color: (p, n) => { if (p.y < 128.5) { const a = Math.atan2(p.z, p.x) * 18; return Math.round(a) % 5 === 0 && p.y > 126.5 ? tone(B.bronze, 5) : tone(B.bronze, p.y > 127.5 ? 4 : 2); }
        const a = Math.atan2(p.z, p.x) * 10; return ((a % 6) + 6) % 6 < 0.8 ? tone(B.leather, 1) : lit(B.leather, n, 1); } },
    ...bothSides('flap', s => ({ outR: 4, sdf: p => sdRoundBox(p, v3(s * 17.5, 118, 1), v3(3.2, 10, 7), 2.5),
      color: (p, n) => p.y < 110 ? tone(B.linen, 4) : lit(B.leather, n, 1) })),
    // stepped spaulders down each arm
    ...bothSides('pauldron', s => ({ outR: 4, sdf: p => Math.min(...[0, 1, 2].map(i => sdEllipsoid(p, v3(s * (16.5 + i * 0.6), Y(32 + i * 5), 0), v3(8 + i * 0.4, 4, 8.5)))),
      color: (p, n) => { const i = Math.round((Y(32) - p.y) / 5), yc = Y(32 + i * 5); if (p.y < yc - 2.6) return tone(B.leatherDk, 2); if (Math.abs(p.z) < 1 && Math.abs(p.y - (yc + 2)) < 0.8 && Math.abs(p.x - s * 19) < 1) return tone(B.bronze, 5); return lit(B.leather, n, 1); } })),
    // the quilted gambeson, a raised collar, a lace down the front
    { piece: 'torso', outR: 4, sdf: p => sdTube(p, 0, 0.3, Y(61), Y(30) + 1, y => 12.5 + 2.5 * smooth((y - 75) / 25), 9.5, 4),
      color: (p, n) => { if (p.y > Y(31)) return tone(B.linen, 5); if (p.z > 6 && Math.abs(p.x) < 1.3 && p.y < Y(33) && p.y > Y(57)) return Math.round(p.y) % 3 === 0 ? tone(B.leatherDk, 3) : tone(B.linen, 1); return quiltAt(B.linen, p, n); } },
    // the sash, knotted at the near hip, its ends hanging; a pouch on the far hip
    { piece: 'belt', outR: 4, sdf: p => Math.min(sdTube(p, 0, 0.3, Y(62), Y(57), 13.8, 10.6, 1.5), sdEllipsoid(p, v3(-9, Y(59.5), 9.5), v3(3.5, 3.5, 2.5)),
        sdCapsule(p, v3(-9.5, Y(62), 9.6), v3(-12, 45, 8.6), 1.7), sdCapsule(p, v3(-6.5, Y(62), 10), v3(-4.5, 49, 9.8), 1.7)),
      color: (p, n) => p.y < 51 && p.y < Y(62) - 4 && (p.y < 47.5 || p.x > -8) && p.y < 51 ? tone(B.olive, Math.round(p.y) % 2 ? 2 : 4) : lit(B.olive, n, 1) },
    { piece: 'pouch', outR: 4, sdf: p => sdRoundBox(p, v3(10, Y(65), 9.5), v3(3.4, 4.2, 2.2), 1), color: (p, n) => p.y > Y(63) ? tone(B.leatherDk, 3) : Math.hypot(p.x - 10, p.y - Y(62)) < 1 ? tone(B.bronze, 5) : lit(B.leather, n, 1) },
    // quilted breeches over the thighs, and their seat
    // the gambeson's skirts: below the sash to mid-thigh, as the approved front has them, split front and back
    { piece: 'skirts', outR: 4, sdf: p => sdTube(p, 0, 0.3, 50, Y(61) + 1, y => 13.4 + (Y(61) - y) * 0.07, y => 10 + (Y(61) - y) * 0.05, 2),
      color: (p, n) => Math.abs(p.x) < 0.9 && Math.abs(p.z) > 6 ? tone(B.linen, 0) : quiltAt(B.linen, p, n, 4, Math.abs(p.z) > 6 && p.x > 0 ? -1 : 0) },
    ...bothSides('breeches', s => ({ outR: 4, sdf: p => sdCapsule(p, v3(s * LEG_X, HIP_Y + 8, 0), v3(s * LEG_X, 46, 0.3), 5.8), color: (p, n) => quiltAt(B.linen, v3(p.x - s * LEG_X, p.y, p.z), n, 4, s > 0 ? -1 : 0) })),
    // wrapped shins, leather boots
    ...bothSides('wraps', s => ({ outR: 4, sdf: p => sdCapsule(p, v3(s * LEG_X, 36, 0.3), v3(s * LEG_X, 11, 0.3), 4.9), color: (p, n) => ((p.y - p.x * 0.5 - p.z * 0.5) % 3 + 3) % 3 < 1 ? tone(B.linen, 1) : lit(B.linen, n, 1) })),
    ...bothSides('boot', s => ({ outR: 4, sdf: p => Math.min(sdRoundBox(p, v3(s * LEG_X, 4.5, 2), v3(5.2, 4.5, 7), 2), sdCapsule(p, v3(s * LEG_X, 11, 0.3), v3(s * LEG_X, 6, 0.3), 5.4)),
      color: (p, n) => p.y > 10.5 ? tone(B.leatherDk, 3) : Math.abs(p.y - 10) < 1 && Math.abs(p.x - s * LEG_X) < 1 && p.z > 3 ? tone(B.bronze, 5) : lit(B.leather, n, 1) })),
    // gloves: wrapped forearms, leather fists
    ...bothSides('glove', s => { const A = arm(s); return { outR: 4, sdf: p => sdCapsule(p, lerp3(A.el, A.wr, 0.08), A.wr, 5.0),
      color: (p, n) => { const t = alongFore(p, A); return t < 0.56 ? ((Math.round(p.x + p.y + p.z) % 4) === 0 ? tone(B.linen, 2) : lit(B.linen, n, 1)) : t < 0.62 ? tone(B.leatherDk, 4) : lit(B.leather, n, 1); } }; }),
    // the bedroll across the shoulders, strapped; a round pouch at the back of the far hip
    { piece: 'back', outR: 4, sdf: p => sdCapsule(p, v3(-28, Y(26), -12), v3(28, Y(26), -12), 5.2),
      color: (p, n) => { if (Math.abs(p.x) > 27.5) return Math.hypot(p.y - Y(26), p.z + 12) < 2.2 ? tone(B.olive, 1) : tone(B.linen, 4); if ([-22, 22].some(x => Math.abs(p.x - x) < 0.9)) return tone(B.leatherDk, 3); return lit(B.olive, n, 1); } },
    { piece: 'backPouch', outR: 4, sdf: p => sdEllipsoid(p, v3(13, Y(72), -8), v3(4, 5, 3.5)), color: (p, n) => p.y > Y(68) ? tone(B.leatherDk, 3) : lit(B.leather, n, 1) },
  ];
}

// ---------------------------------------------------------------- Bronze III - Athanor
function athanor3d() {
  const B = M3;
  return [
    // a leather cap, a bronze band, the goggles pushed up on a strap
    { piece: 'helm', outR: 4, sdf: p => Math.max(sdRoundBox(p, v3(0, 124, 0), v3(17.8, 13.5, 15.8), 9), 125.5 - p.y),
      color: (p, n) => p.y < 128.5 ? tone(B.bronzeDk, p.y > 127.5 ? 4 : 2) : Math.abs(p.y - 133) < 0.8 ? tone(B.leatherDk, 1) : lit(B.leather, n, 1) },
    ...bothSides('goggle', s => ({ outR: 3, sdf: p => sdEllipsoid(p, v3(s * 7, 132, 13.2), v3(4, 4, 2.6)),
      color: (p, n) => { const r = Math.hypot(p.x - s * 7, p.y - 132); return r < 2.4 ? tone(B.teal, (p.x - s * 7 < 0 && p.y > 132) ? 5 : 2) : lit(B.bronze, n, 1); } })),
    // leather pauldrons, riveted along the top
    ...bothSides('pauldron', s => ({ outR: 4, sdf: p => sdEllipsoid(p, v3(s * 16.5, Y(34), 0), v3(8, 5.6, 8.5)),
      color: (p, n) => p.y < Y(34) - 2.5 ? tone(B.leatherDk, 2) : (p.y > Y(34) + 3.5 && Math.round(p.x * 0.4 + p.z * 0.4) % 2 === 0) ? tone(B.bronze, 5) : lit(B.leather, n, 1) })),
    // a linen shirt, the leather bib of the apron over the front on straps
    { piece: 'torso', outR: 4, sdf: p => sdTube(p, 0, 0.3, Y(61), Y(30) + 1, y => 12.5 + 2.5 * smooth((y - 75) / 25), 9.5, 4), color: (p, n) => lit(B.linen, n, 1) },
    { piece: 'bib', outR: 3, sdf: p => {
        const zf = 9.6 + 0.3 * Math.cos(p.x / 9), bib = Math.max(Math.abs(p.z - zf) - 1.0, Math.abs(p.x) - (8.5 + (Y(34) - p.y) * 0.04), p.y - Y(34), Y(61) - p.y);
        const straps = Math.min(...[-1, 1].map(s => sdCapsule(p, v3(s * 8, Y(34), 9.8), v3(s * 7, Y(29) + 2, 6), 1.2)));
        return Math.min(bib, straps); },
      color: (p, n) => { if (p.y > Y(34) - 1.5 && Math.abs(Math.abs(p.x) - 8) < 1.2) return tone(B.bronze, 5); if ([[-4, 44], [3, 51], [-1, 57]].some(([x, r]) => Math.hypot(p.x - x, p.y - Y(r)) < 1)) return tone(B.leatherDk, 0); return lit(B.leatherDk, n, 1); } },
    // the apron below the belt, to the knee, its hem scorched ragged
    { piece: 'apron', outR: 4, sdf: p => { const zf = 10.3 + 0.002 * (Y(61) - p.y) ** 1.6, hem = 37 + ((Math.round(p.x) * 5) % 3); return Math.max(Math.abs(p.z - zf) - 1.0, Math.abs(p.x) - 10, p.y - Y(61), hem - p.y); },
      color: (p, n) => p.y < 41 ? tone(B.leatherDk, 1) : [[-5, 78], [4, 83], [0, 70]].some(([x, r]) => Math.hypot(p.x - x, p.y - (HIP_Y - (r - 72) * 1.29)) < 1) ? tone(B.leatherDk, 0) : lit(B.leatherDk, n, 1) },
    // the back panel, mirroring the apron: to the knee, the same scorched hem
    { piece: 'apronBack', outR: 4, sdf: p => { const zf = -(10.3 + 0.002 * (Y(61) - p.y) ** 1.6), hem = 37 + ((Math.round(-p.x) * 5) % 3 + 3) % 3; return Math.max(Math.abs(p.z - zf) - 1.0, Math.abs(p.x) - 10, p.y - Y(61), hem - p.y); },
      color: (p, n) => p.y < 41 ? tone(B.leatherDk, 1) : [[5, 80], [-3, 85], [1, 74]].some(([x, r]) => Math.hypot(p.x - x, p.y - (HIP_Y - (r - 72) * 1.29)) < 1) ? tone(B.leatherDk, 0) : lit(B.leatherDk, n, 1) },
    // the belt, its three vials, a pouch
    { piece: 'belt', outR: 4, sdf: p => sdTube(p, 0, 0.3, Y(61), Y(57), 13.8, 10.6, 1.5), color: (p, n) => lit(B.leather, n, 1) },
    ...[[-8, B.red], [-4, B.teal], [4, B.olive]].map(([x, g], i) => ({ piece: 'vial' + i, outR: 3, sdf: p => Math.min(sdRoundBox(p, v3(x, Y(65), 11.4), v3(1.6, 2.6, 1.4), 0.7), sdRoundBox(p, v3(x, Y(61.5), 11.2), v3(1.2, 1, 1.2), 0.4)),
      color: (p, n) => p.y > Y(62) ? tone(B.leatherDk, 3) : tone(g, (p.x < x ? 5 : 3)) })),
    { piece: 'pouch', outR: 4, sdf: p => sdRoundBox(p, v3(10.5, Y(63), 9.5), v3(3, 4.2, 2.2), 1), color: (p, n) => Math.hypot(p.x - 10.5, p.y - Y(61)) < 1 ? tone(B.bronze, 5) : lit(B.leather, n, 1) },
    // linen trousers and their seat, leather boots with bronze toe caps
    { piece: 'seat', outR: 4, sdf: p => sdTube(p, 0, 0, HIP_Y - 4, Y(61) + 1, 13, 9.6, 4), color: (p, n) => lit(B.linen, n, 0) },
    ...bothSides('trousers', s => ({ outR: 4, sdf: p => sdCapsule(p, v3(s * LEG_X, HIP_Y + 6, 0), v3(s * LEG_X, 28, 0.3), 4.9), color: (p, n) => lit(B.linen, n, 1) })),
    ...bothSides('boot', s => ({ outR: 4, sdf: p => Math.min(sdCapsule(p, v3(s * LEG_X, 29, 0.3), v3(s * LEG_X, 7, 0.3), 5.0), sdRoundBox(p, v3(s * LEG_X, 3.5, 2.2), v3(5.3, 3.5, 7.2), 2)),
      color: (p, n) => { if (p.y < 6 && p.z > 0) return tone(B.bronze, p.y > 5 ? 5 : p.x - s * LEG_X < -2 ? 4 : 3); if (Math.abs(p.y - 22.5) < 1) return Math.abs(p.x - s * LEG_X - 1) < 0.8 && p.z > 3 ? tone(B.bronze, 5) : tone(B.leather, p.y > 22.5 ? 4 : 2); return lit(B.leatherDk, n, 1); } })),
    // gauntlets: a flared cuff below the elbow, dark gloves to the fist
    ...bothSides('glove', s => { const A = arm(s); return { outR: 4, sdf: p => Math.min(sdCapsule(p, lerp3(A.el, A.wr, 0.1), A.wr, 5.0), sdCapsule(p, lerp3(A.el, A.wr, -0.12), lerp3(A.el, A.wr, 0.12), 6.4)),
      color: (p, n) => { const t = alongFore(p, A); return t < 0.12 ? lit(B.leather, n, 1) : t < 0.17 ? tone(B.bronze, t < 0.145 ? 5 : 2) : lit(B.leatherDk, n, 1); } }; }),
    // the backpack: tall, its flap, side pouches, the flask standing out of the top; straps over the shoulders
    { piece: 'back', outR: 4, sdf: p => Math.min(sdRoundBox(p, v3(0, Y(40), -16.5), v3(21.5, 19.5, 7), 2.5), sdRoundBox(p, v3(0, Y(21.5), -16.3), v3(22.5, 4.8, 7.6), 2)),
      color: (p, n) => p.y > Y(26) ? (Math.abs(p.y - Y(26)) < 0.9 ? tone(B.leatherDk, 1) : lit(B.leatherDk, n, 1)) : lit(B.leather, n, 1) },
    ...bothSides('sidePouch', s => ({ outR: 4, sdf: p => sdRoundBox(p, v3(s * 23.5, Y(46), -15), v3(3, 8, 5), 1.5), color: (p, n) => lit(B.leatherDk, n, 1) })),
    { piece: 'flask', outR: 3, sdf: p => Math.min(sdCapsule(p, v3(17.5, Y(17), -15), v3(17.5, Y(9), -15), 2), sdRoundBox(p, v3(17.5, Y(6.5), -15), v3(2.6, 1.8, 2.6), 0.8)),
      color: (p, n) => p.y > Y(8) ? lit(B.leather, n, 1) : tone(B.teal, p.x < 17 ? 5 : 3) },
    { piece: 'straps', outR: 3, sdf: p => Math.min(...[-1, 1].map(s => Math.min(sdCapsule(p, v3(s * 9.5, Y(29), 9.8), v3(s * 9.5, Y(52), 10.4), 1.4), sdCapsule(p, v3(s * 9.5, Y(29), 9.8), v3(s * 10.5, Y(26), -10), 1.4)))),
      color: (p, n) => Math.abs(p.y - Y(45)) < 1.2 && p.z > 5 ? tone(B.bronze, p.y > Y(45) ? 5 : 2) : lit(B.leatherDk, n, 1) },
  ];
}

// ---------------------------------------------------------------- weapons: flat cards in the hand
// A weapon keeps its approved flat art. It is a card in 3D: its texel (u, v) sits at origin + U*(u - pu) + V*(v - pv),
// U and V are ONE weapon texel each, two body texels long (weapons are at twice the body's density, as the game draws them). Each view projects the card; seen
// edge-on it is drawn as its centre column, two texels wide.
let CARDS = [];
function card(grid, pivot, origin, U, V) {
  let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
  for (const k of grid.keys()) { x0 = Math.min(x0, KX(k)); x1 = Math.max(x1, KX(k)); y0 = Math.min(y0, KY(k)); y1 = Math.max(y1, KY(k)); }
  const mid = Math.round((x0 + x1) / 2);
  return { at(vx, vy, ca, sa) {
    // the card's origin and axes in this view (screen x right, screen y up, z toward the viewer)
    const tv = q => v3(q.x * ca + q.z * sa, q.y, -q.x * sa + q.z * ca);
    const O = tv(origin), Uu = tv(U), Vv = tv(V);
    const det = Uu.x * Vv.y - Uu.y * Vv.x, px = vx - O.x, py = vy - O.y;
    if (Math.abs(det) > 0.35) {
      const a = (px * Vv.y - py * Vv.x) / det, b = (Uu.x * py - Uu.y * px) / det;
      const u = Math.floor(pivot[0] + a + 0.5), v = Math.floor(pivot[1] + b + 0.5), c = grid.get(K(u, v));   // a, b count whole axes: one weapon texel each
      if (!c) return null;
      return { color: c, vz: O.z + Uu.z * a + Vv.z * b };
    }
    // edge-on: walk down the card's length
    const vl = Math.hypot(Vv.x, Vv.y) || 1, b = (px * Vv.x + py * Vv.y) / (vl * vl), side = (px * Vv.y - py * Vv.x) / vl;
    if (Math.abs(side) > 1.2) return null;
    const v = Math.floor(pivot[1] + b + 0.5);
    for (let u = mid; u >= x0; u--) { const c = grid.get(K(u, v)); if (c) return { color: c, vz: O.z + Vv.z * b }; }
    return null;
  } };
}
const norm3 = q => { const l = len3(q) || 1; return v3(q.x / l, q.y / l, q.z / l); };
const cross3 = (a, b) => v3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
const weaponGrid = layers => compose(layers);
// the rest poses: where the hands go and how each weapon lies (body frame)
function restPose(kind, setId) {
  POSE.hands = {}; CARDS = [];
  if (kind === 'sword') {
    // the rest carry, as the game has it: the fist up by the shoulder, the blade hanging diagonally down behind the back
    const draw = SWORDS[setId].draw, grip = { hoplite: 60, wayfarer: 62, athanor: 62 }[setId] ?? 62;
    const hand = v3(21, 118, 3); POSE.hands[1] = hand;
    const tip = norm3(v3(-0.72, -0.62, -0.3)), V = v3(-tip.x * 2, -tip.y * 2, -tip.z * 2);
    const U = (q => v3(q.x * 2, q.y * 2, q.z * 2))(norm3(cross3(tip, v3(0, 0, 1))));
    CARDS.push(card(weaponGrid(draw()), [7, grip + 6], v3(hand.x, hand.y, hand.z - 3), U, V));
  } else if (kind === 'discs') {
    // wings: the fists up before the chest, a disc standing behind each side of the body, facing out
    const draw = DISCS[setId].draw;
    for (const s of [1, -1]) {
      POSE.hands[s] = v3(s * 8.5, 95, 10);
      CARDS.push(card(weaponGrid(draw()), [14, 14], v3(s * 29, 84, -7), v3(2, 0, 0), v3(0, -2, 0)));
    }
  } else if (kind === 'bow') {
    const draw = BOWS[setId].draw, g = weaponGrid(draw());
    // the stave at the middle row: the grip
    let xs = []; for (const k of g.keys()) if (KY(k) === 39 && g.get(k) !== OUTLINE) xs.push(KX(k));
    const px = xs.length ? (Math.min(...xs) + Math.max(...xs)) / 2 : 8;
    const hand = v3(20, 70, 7); POSE.hands[1] = hand;
    const out = 25 * Math.PI / 180, U = v3(2 * Math.sin(out), 0, 2 * Math.cos(out));
    CARDS.push(card(g, [px, 39], v3(hand.x + 1.5, hand.y, hand.z), U, v3(0, -2, 0)));
  }
}

// ---------------------------------------------------------------- render
function renderView(prims, yawDeg) {
  const a = yawDeg * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
  const toBody = (vx, vy, vz) => v3(vx * ca - vz * sa, vy, vx * sa + vz * ca);
  const toView = n => v3(n.x * ca + n.z * sa, n.y, -n.x * sa + n.z * ca);
  const hits = new Map();   // key -> { prim, z }
  for (let row = 0; row < TOP + 2; row++) for (let col = -HALFW; col <= HALFW; col++) {
    const vy = TOP - row - 0.5, vx = col + 0.5;
    let t = 0, hit = null;
    while (t < 120) {
      const vz = 60 - t, p = toBody(vx, vy, vz);
      let best = 1e9, bp = null;
      for (const pr of prims) { const d = pr.sdf(p); if (d < best) { best = d; bp = pr; } }
      if (best < 0.02) { hit = { prim: bp, p, vz }; break; }
      t += Math.max(0.2, best * 0.8);
    }
    // a weapon card nearer than the body wins the texel
    let card = null;
    for (const c of CARDS) { const w = c.at(vx, vy, ca, sa); if (w && (!hit || w.vz > hit.vz) && (!card || w.vz > card.vz)) card = w; }
    if (card) { hits.set(K(col, row), { prim: { piece: 'weapon', outR: 0 }, z: 999, color: card.color }); continue; }
    if (!hit) continue;
    const e = 0.25, pr = hit.prim, p = hit.p;
    const n0 = v3(pr.sdf(v3(p.x + e, p.y, p.z)) - pr.sdf(v3(p.x - e, p.y, p.z)), pr.sdf(v3(p.x, p.y + e, p.z)) - pr.sdf(v3(p.x, p.y - e, p.z)), pr.sdf(v3(p.x, p.y, p.z + e)) - pr.sdf(v3(p.x, p.y, p.z - e)));
    const nl = len3(n0) || 1, nb = v3(n0.x / nl, n0.y / nl, n0.z / nl), nv = toView(nb);
    hits.set(K(col, row), { prim: pr, z: hit.vz, color: pr.color(p, nv, nb) });
  }
  // one Layer per piece, stacked by how near it is in this view
  const layers = new Map();
  for (const [k, h] of hits) {
    const id = h.prim.piece;
    if (!layers.has(id)) { const l = new Layer(0); l.outR = h.prim.outR; if (!h.prim.outR) l.outline = false; l.zs = []; layers.set(id, l); }
    const l = layers.get(id); l.c.set(k, h.color); l.zs.push(h.z);
  }
  for (const l of layers.values()) l.depth = l.zs.reduce((s, z) => s + z, 0) / l.zs.length;
  return [...layers.values()];
}

// ---------------------------------------------------------------- sheet
if (require.main === module || typeof module === 'undefined' || !module.parent) {
  const fs = require('fs'), zlib = require('zlib');
  const out = process.argv[2] || 'views.png', S = +(process.argv[3] || 4);
  const WEAPONS_SHEET = process.env.SHEET === 'weapons';
  if (WEAPONS_SHEET) { TOP = 178; HALFW = 56; }
  const plain = make => () => { POSE.hands = {}; CARDS = []; return make(); };
  const SETS3D = { hoplite: hoplite3d, wayfarer: wayfarer3d, athanor: athanor3d };
  const armed = (kind, id) => () => { restPose(kind, id); return [...body3d(), ...SETS3D[id]()]; };
  const figures = WEAPONS_SHEET
    ? ['sword', 'discs', 'bow'].flatMap(kind => ['hoplite', 'wayfarer', 'athanor'].map(id => [id + ' ' + kind, armed(kind, id)]))
    : [['bare', plain(() => body3d())], ['hoplite', plain(() => [...body3d(), ...hoplite3d()])], ['wayfarer', plain(() => [...body3d(), ...wayfarer3d()])], ['athanor', plain(() => [...body3d(), ...athanor3d()])]];
  const CW = WEAPONS_SHEET ? 118 : 100, CH = TOP + 12, OXC = CW / 2;
  const cols = VIEWS.length + (WEAPONS_SHEET ? 0 : 1), W = cols * CW * S, H = figures.length * CH * S;
  const buf = Buffer.alloc(W * H * 4);
  for (let i = 0; i < W * H; i++) { buf[i * 4] = 0x4b; buf[i * 4 + 1] = 0x4e; buf[i * 4 + 2] = 0x57; buf[i * 4 + 3] = 255; }
  const draw = (grid, col, row, ox, oy) => { for (const [k, c] of grid) { const x = KX(k) + ox + col * CW, y = KY(k) + oy + row * CH; const [r, g, b] = hex2rgb(c);
    for (let dy = 0; dy < S; dy++) for (let dx = 0; dx < S; dx++) { const px = x * S + dx, py = y * S + dy; if (px < 0 || py < 0 || px >= W || py >= H) continue; const i = (py * W + px) * 4; buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; } } };
  // column 0: today's front art (48-texel legs), soles on the same line
  if (!WEAPONS_SHEET) {
  draw(compose(Object.values(bodyLayers())), 0, 0, 31, TOP + 4 - 122);
  ['hoplite', 'wayfarer', 'athanor'].forEach((id, i) => draw(compose(wear(SETS.find(s => s.id === id))), 0, i + 1, 31, TOP + 4 - 122));
  }
  figures.forEach(([name, make], r) => VIEWS.forEach(([v, yaw], c) => {
    const prims = make();
    // a piece the set covers hides the body's own: the suit under the cuirass is never seen
    draw(compose(renderView(prims, yaw)), c + (WEAPONS_SHEET ? 0 : 1), r, OXC, 4);
    process.stderr.write(name + ' ' + v + '\n');
  }));
  const crcT = []; for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; crcT[n] = c >>> 0; }
  const crc = b => { let c = 0xffffffff; for (const x of b) c = crcT[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const chunk = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ih = Buffer.alloc(13); ih.writeUInt32BE(W, 0); ih.writeUInt32BE(H, 4); ih[8] = 8; ih[9] = 6;
  const raw = Buffer.alloc((W * 4 + 1) * H); for (let y = 0; y < H; y++) buf.copy(raw, y * (W * 4 + 1) + 1, y * W * 4, (y + 1) * W * 4);
  fs.writeFileSync(out, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ih), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]));
  console.log(out, W + 'x' + H);
}
