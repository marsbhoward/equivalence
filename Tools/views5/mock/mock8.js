// 8-direction locomotion mock: a 3D skeleton projected through the arena's 3/4 camera, drawn as
// outlined pixel parts in the Hoplite's materials. One model, every direction.
const fs = require('fs');
const kit = require('/Users/marshoward/Coalescence/Tools/tier-art/kit.js');
const { ramp, tone, OUTLINE } = kit;
const M = {
  bronze: ramp('#B87333', { sheen: '#ffd98a', lift: 0.36 }), bronzeDk: ramp('#7f4c22', { sheen: '#e9a65a' }),
  leather: ramp('#7a4a2a', { sheen: '#d9a070', lift: 0.28 }), leatherDk: ramp('#4a2e1c', { sheen: '#a8774f', lift: 0.28 }),
  red: ramp('#9b2a24', { sheen: '#ff9a72', lift: 0.30 }), skin: ramp('#d9a47e', { sheen: '#ffe2c8', lift: 0.30 }),
  under: ramp('#3d3d44', { sheen: '#9a9aa8', lift: 0.28 }),
};
const EYE = '#1F242B';
const V = (x, y, z) => ({ x, y, z });
const add = (a, b) => V(a.x + b.x, a.y + b.y, a.z + b.z), mul = (a, k) => V(a.x * k, a.y * k, a.z * k);
const sub = (a, b) => V(a.x - b.x, a.y - b.y, a.z - b.z), len = a => Math.hypot(a.x, a.y, a.z);
const UP = V(0, 1, 0);
const CW = 100, CH = 172, OX = 50, OY = 162, DEPTH_Y = 0.45;
const P = p => ({ x: OX + p.x, y: OY - p.y + p.z * DEPTH_Y, z: p.z });

// ---------------------------------------------------------------- gaits
const lerpKeys = (keys, p) => {   // keys [[p, a, b]] wrapped, smooth
  p = ((p % 1) + 1) % 1;
  for (let i = 0; i < keys.length; i++) {
    const k0 = keys[i], k1 = keys[(i + 1) % keys.length], p1 = i + 1 < keys.length ? k1[0] : 1 + k1[0];
    if (p >= k0[0] && p <= p1) { const t = (p - k0[0]) / (p1 - k0[0]), s = t * t * (3 - 2 * t); return k0.slice(1).map((v, j) => v + (k1[j + 1] - v) * s); }
  }
  return keys[0].slice(1);
};
const GAITS = {
  run: { frames: 32, foot: [[0, 0.30, 0], [0.33, -0.36, 0], [0.48, -0.55, 0.42], [0.66, -0.08, 0.55], [0.84, 0.46, 0.24]],
         pitch: [[0, 18], [0.12, 0], [0.33, -40], [0.5, -55], [0.7, -20], [0.86, 10]],
         bob: p => 3.2 * Math.cos(4 * Math.PI * (p - 0.42)) - 1.5, lean: 11, arm: 34, elbow: 88, trail: 11, hipDrop: 0, base: 54.4 },
  walk: { frames: 48, foot: [[0, 0.30, 0], [0.6, -0.30, 0], [0.72, -0.18, 0.10], [0.86, 0.12, 0.13], [0.96, 0.32, 0.03]],
          pitch: [[0, 16], [0.1, 0], [0.55, -8], [0.62, -30], [0.75, -10], [0.92, 8]],
          bob: p => 1.3 * Math.cos(4 * Math.PI * (p - 0.30)), base: 56.3, lean: 3, arm: 14, elbow: 28, trail: 4, hipDrop: 1 },
};
const L = 58, THIGH = 29, SHIN = 29, ANKLE = 4, HIP_W = 6;

// ---------------------------------------------------------------- canvas
function frame(dirDeg, gait, phase) {
  const G = GAITS[gait];
  const a = dirDeg * Math.PI / 180, f = V(Math.cos(a), 0, Math.sin(a)), r = V(-f.z, 0, f.x);
  const H0 = G.base;
  const hipY = ANKLE + H0 + G.bob(phase);
  const leanT = Math.tan(G.lean * Math.PI / 180);
  const pelvis = V(0, hipY + 4, 0);
  const up = h => add(add(pelvis, mul(UP, h)), mul(f, h * leanT));   // a point on the body's axis, leaning
  const parts = [];
  const part = (z, draw) => parts.push({ z, draw });

  // ---- legs
  const legs = [];
  for (const side of [1, -1]) {
    const p = phase + (side === 1 ? 0 : 0.5);
    const [fw, lift] = lerpKeys(G.foot, p), [pitch] = lerpKeys(G.pitch, p);
    const drop = G.hipDrop * Math.max(0, Math.sin(2 * Math.PI * (p - 0.6)));
    const hip = add(V(0, hipY - drop, 0), mul(r, side * HIP_W));
    let ankle = add(add(V(hip.x, ANKLE + lift * L, hip.z), mul(f, fw * L)), V(0, 0, 0));
    let v = sub(ankle, hip); let d = len(v);
    if (d > THIGH + SHIN - 0.01) { v = mul(v, (THIGH + SHIN - 0.01) / d); ankle = add(hip, v); d = THIGH + SHIN - 0.01; }
    // knee: in the plane of f and up, bent forward
    const vf = v.x * f.x + v.z * f.z, vu = v.y;
    const h = Math.sqrt(Math.max(0, THIGH * THIGH - (d / 2) ** 2));
    const pf = -vu / d, pu = vf / d;   // perpendicular in (f, up); choose the forward one
    const sgn = pf >= 0 ? 1 : -1;
    const knee = add(add(hip, mul(v, 0.5)), add(mul(f, pf * h * sgn), mul(UP, pu * h * sgn)));
    const pr = pitch * Math.PI / 180, toeDir = add(mul(f, Math.cos(pr)), mul(UP, Math.sin(pr)));
    const sole = add(ankle, V(0, -3, 0));
    legs.push({ side, hip, knee, ankle, heel: add(sole, mul(toeDir, -3)), toe: add(sole, mul(toeDir, 9)) });
  }
  for (const g of legs) {
    part((g.hip.z + g.knee.z) / 2, c => capsule(c, P(g.hip), P(g.knee), 5, (u, s) => tone(M.skin, shadeIdx(s) + (u < 0.15 ? -1 : 0))));
    part((g.knee.z + g.ankle.z) / 2 + 0.01, c => {
      capsule(c, P(g.knee), P(g.ankle), 4.6, (u, s) => u > 0.9 ? tone(M.leather, shadeIdx(s)) : tone(M.bronze, shadeIdx(s) + (u < 0.2 ? 1 : 0)));
      disc(c, P(g.knee), 5, 4, (dx, dy) => tone(M.bronze, dy < -1 && dx < 1 ? 5 : shadeIdx(dx / 5) + 1));
    });
    part((g.heel.z + g.toe.z) / 2 + 0.02, c => capsule(c, P(g.heel), P(g.toe), 2.6, (u, s) => tone(M.leatherDk, (u > 0.4 && Math.round(u * 6) % 2 ? 4 : 2) + (s < 0 ? 1 : 0))));
  }

  // ---- skirt (leather strips round the waist) - back half and front half
  for (const front of [false, true]) part(front ? 3 : -3, c => {
    for (let i = 0; i < 16; i++) {
      const t = (i / 16) * 2 * Math.PI, n = add(mul(r, Math.cos(t)), mul(f, Math.sin(t)));
      const facing = n.z; if ((facing >= 0) !== front) continue;
      const top = add(add(V(0, hipY + 9, 0), mul(r, 12.5 * Math.cos(t))), mul(f, 9 * Math.sin(t)));
      const swingOut = Math.max(0, n.x * f.x + n.z * f.z) * 0;
      const tan = V(-n.z, 0, n.x), wpx = Math.max(1.2, 3.4 * Math.abs(tan.x) + 0.6);
      const bot = add(top, V(0, -19, 0)), A = P(top), B = P(bot);
      for (let y = Math.round(A.y); y <= Math.round(B.y); y++) for (let x = Math.round(A.x - wpx / 2); x <= Math.round(A.x + wpx / 2); x++) {
        const tip = y >= Math.round(B.y) - 1, lit = n.x < -0.3 ? 4 : n.x > 0.4 ? 2 : 3;
        c.set(key(x, y), tip ? tone(M.bronze, lit + 1) : tone(M.leather, lit + (x === Math.round(A.x - wpx / 2) ? 1 : 0)));
      }
    }
  });

  // ---- torso: an elliptic cylinder, waist to shoulders
  const shoulderH = 40;
  part(0, c => {
    const B0 = P(up(2)), steps = shoulderH;
    for (let i = 0; i <= steps; i++) {
      const h = 2 + i, t = i / steps, rx = 10.5 + 3.5 * Math.sin(Math.min(1, t * 1.3) * Math.PI / 2), rz = 7.5;
      const hw = Math.sqrt((rx * r.x) ** 2 + (rz * f.x) ** 2), C = P(up(h));
      const vz = DEPTH_Y * Math.sqrt((rx * r.z) ** 2 + (rz * f.z) ** 2);
      for (let x = Math.round(C.x - hw); x <= Math.round(C.x + hw); x++) {
        const s = (x + 0.5 - C.x) / hw, cap = i === steps ? Math.round(vz * Math.sqrt(Math.max(0, 1 - s * s))) : 0;
        for (let y = Math.round(C.y) - cap; y <= Math.round(C.y); y++) c.set(key(x, y), tone(M.bronze, shadeIdx(s) + (y < Math.round(C.y) ? 1 : 0)));
      }
    }
    // the front: pecs and a sternum, where the chest faces the camera
    if (f.z > -0.3) {
      const vis = Math.abs(r.x);
      for (const sd of [-1, 1]) {
        const pc = P(add(add(up(30), mul(f, 7.5)), mul(r, sd * 5)));
        const rx = 4.8 * vis + 0.6; if (rx < 1.4) continue;
        for (let dy = -4; dy <= 4; dy++) for (let dx = -Math.ceil(rx); dx <= Math.ceil(rx); dx++) {
          if ((dx / rx) ** 2 + (dy / 4.2) ** 2 > 1) continue; const k = key(Math.round(pc.x + dx), Math.round(pc.y + dy));
          if (c.has(k)) c.set(k, tone(M.bronze, dy < -1 && dx < 0 ? 5 : dy > 2 ? 2 : 4));
        }
      }
      const s0 = P(add(up(36), mul(f, 7.5))), s1 = P(add(up(8), mul(f, 7.5)));
      for (let y = Math.round(s0.y); y <= Math.round(s1.y); y++) { const k = key(Math.round(s0.x + (s1.x - s0.x) * (y - s0.y) / Math.max(1, s1.y - s0.y)), y); if (c.has(k)) c.set(k, tone(M.bronze, 1)); }
    } else {
      const s0 = P(add(up(38), mul(f, -7.5))), s1 = P(add(up(6), mul(f, -7.5)));
      for (let y = Math.round(s0.y); y <= Math.round(s1.y); y++) { const k = key(Math.round(s0.x + (s1.x - s0.x) * (y - s0.y) / Math.max(1, s1.y - s0.y)), y); if (c.has(k)) c.set(k, tone(M.bronze, 2)); }
    }
  });

  // ---- cape: from the shoulders' back down past the hips, trailing with the gait
  const flutter = Math.sin(phase * 4 * Math.PI) * 1.5;
  const cTop = s => add(add(up(shoulderH - 1), mul(r, s * 12)), mul(f, -7.5));
  const cBot = s => add(add(V(0, hipY - 6, 0), mul(r, s * 16)), mul(f, -9 - G.trail - flutter * (s > 0 ? 1 : 0.6)));
  const quad = [cTop(-1), cTop(1), cBot(1), cBot(-1)];
  const capeZ = (quad[0].z + quad[1].z + quad[2].z + quad[3].z) / 4;
  part(capeZ - (f.z > 0 ? 0.5 : -0.5), c => {
    const pts = quad.map(P), outer = f.z < 0;
    fillPoly(c, pts, (x, y, u, v) => tone(M.red, (outer ? 2 : 1) + (u < 0.18 ? 1 : 0) - (u > 0.82 ? 1 : 0) + (Math.round(u * 7) % 3 === 0 && v > 0.3 ? -1 : 0)));
  });

  // ---- arms
  const armSwing = phase => G.arm * Math.sin(2 * Math.PI * phase);
  for (const side of [1, -1]) {
    const sw = (side === 1 ? 1 : -1) * armSwing(phase) * Math.PI / 180;   // opposite its own side's leg
    const sh = add(up(shoulderH - 3), mul(r, side * 15));
    const d1 = add(mul(UP, -Math.cos(sw)), mul(f, Math.sin(sw)));
    const el = add(sh, add(mul(d1, 21), mul(r, side * 1.5)));
    const b = sw + G.elbow * Math.PI / 180;
    const d2 = add(mul(UP, -Math.cos(b)), mul(f, Math.sin(b)));
    const wr = add(el, add(mul(d2, 17), mul(r, -side * 2.5)));
    const zArm = (sh.z + el.z + wr.z) / 3 + 0.3 * Math.sign(side * r.z || 1) * 0;
    part((sh.z + el.z) / 2 + 0.05, c => capsule(c, P(sh), P(el), 4.2, (u, s) => tone(M.under, shadeIdx(s))));
    part((el.z + wr.z) / 2 + 0.06, c => {
      capsule(c, P(el), P(wr), 3.8, (u, s) => tone(M.leather, shadeIdx(s) + ([0.2, 0.55, 0.85].some(q => Math.abs(u - q) < 0.06) ? 2 : 0)));
      disc(c, P(add(wr, mul(d2, 3))), 4.2, 4.2, (dx, dy) => tone(M.leatherDk, shadeIdx(dx / 4) + (dy < -1 ? 1 : 0)));
    });
    part(sh.z + 0.2, c => disc(c, P(add(sh, V(0, 2, 0))), 8, 6.5, (dx, dy) => { const q = (dx / 8) ** 2 + (dy / 6.5) ** 2; return tone(q > 0.62 ? M.bronzeDk : M.bronze, q > 0.62 ? (dy < 0 ? 4 : 2) : dy < -2 && dx < 1 ? 5 : shadeIdx(dx / 8) + 1); }));
  }

  // ---- head: a crested helm; the face opening where it faces the camera
  const head = add(up(shoulderH + 14), mul(f, 1));
  part(0.4, c => {
    const H = P(head);
    // crest: a transverse fan over the top, drawn first so the dome covers its base
    for (let t = 0; t <= 1.0001; t += 1 / 28) {
      const base = add(add(head, mul(UP, 12)), mul(r, -13 + 26 * t)), tip = add(add(head, mul(UP, 19 + 7 * Math.sin(Math.PI * t))), mul(r, (-13 + 26 * t) * 1.25));
      for (const fo of [-1.5, 0, 1.5]) line(c, P(add(base, mul(f, fo))), P(add(tip, mul(f, fo))), (k, i, n) => tone(M.red, fo === 0 && i > n - 3 ? 4 : t < 0.2 ? 3 : t > 0.8 ? 1 : 2));
    }
    disc(c, P(head), 16, 15, (dx, dy) => tone(M.bronze, (dy < -9 && dx < -2 ? 5 : shadeIdx(dx / 16) + (dy < -4 ? 1 : 0))));
    // the brow band
    for (let dx = -15; dx <= 15; dx++) { const k = key(Math.round(H.x + dx), Math.round(H.y + 2)); if (c.has(k)) c.set(k, tone(M.bronze, 5)); }
    if (f.z > -0.35) {
      const vis = Math.abs(r.x), F = P(add(head, mul(f, 15))); F.y = H.y - 1 + (F.y - H.y) * 0.25;
      const ew = Math.max(2.5, 7 * vis), toward = -Math.sign(f.x) || 0;
      const cx = F.x + (vis < 0.5 ? toward * ew * 0.5 : 0);
      for (let dy = 0; dy <= 3; dy++) for (let dx = -Math.round(ew); dx <= Math.round(ew); dx++) { const k = key(Math.round(cx + dx), Math.round(F.y + 3 + dy)); if (c.has(k)) c.set(k, tone(M.skin, 3 + (dy === 0 ? 1 : 0))); }
      const eyes = vis > 0.45 ? [-0.5, 0.5] : [toward * 0.35];
      for (const e of eyes) { const ex = Math.round(cx + e * ew); for (const [ox, oy] of [[0, 1], [1, 1], [0, 2], [1, 2]]) { const k = key(ex + ox - 1, Math.round(F.y + 3) + oy); if (c.has(k)) c.set(k, EYE); } }
      const mw = Math.max(1, Math.round(2 * vis));
      for (let dy = 4; dy <= 11; dy++) for (let dx = -mw; dx <= mw; dx++) { const k = key(Math.round(cx + dx), Math.round(F.y + 3 + dy)); if (c.has(k)) c.set(k, tone(M.skin, dy > 9 ? 2 : 3)); }
      for (let dy = 0; dy <= 7; dy++) { const k = key(Math.round(cx), Math.round(F.y + 2 + dy)); if (c.has(k)) c.set(k, tone(M.bronze, 4)); }
    }
  });

  // ---- composite, nearest last, each part outlined
  parts.sort((p, q) => p.z - q.z);
  const out = new Map();
  for (const pt of parts) {
    const c = new Map(); pt.draw(c);
    for (const k of c.keys()) { const x = k % 1000 - 200, y = Math.floor(k / 1000) - 200;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const n = key(x + dx, y + dy); if (!c.has(n)) out.set(n, OUTLINE); } }
    for (const [k, v] of c) out.set(k, v);
  }
  return out;
}
const key = (x, y) => (y + 200) * 1000 + (x + 200);
const shadeIdx = s => s < -0.45 ? 4 : s > 0.5 ? 2 : 3;
function capsule(c, A, B, rad, col) {
  const dx = B.x - A.x, dy = B.y - A.y, l2 = dx * dx + dy * dy || 1e-6, L1 = Math.sqrt(l2);
  for (let y = Math.floor(Math.min(A.y, B.y) - rad); y <= Math.ceil(Math.max(A.y, B.y) + rad); y++)
    for (let x = Math.floor(Math.min(A.x, B.x) - rad); x <= Math.ceil(Math.max(A.x, B.x) + rad); x++) {
      const px = x + 0.5 - A.x, py = y + 0.5 - A.y, u = Math.max(0, Math.min(1, (px * dx + py * dy) / l2));
      const qx = px - u * dx, qy = py - u * dy, d = Math.hypot(qx, qy); if (d > rad) continue;
      const sx = (x + 0.5 - (A.x + u * dx)) / rad;   // across, in screen x: lit from the left
      c.set(key(x, y), col(u, sx));
    }
}
function disc(c, C, rx, ry, col) {
  for (let y = Math.floor(C.y - ry); y <= Math.ceil(C.y + ry); y++) for (let x = Math.floor(C.x - rx); x <= Math.ceil(C.x + rx); x++) {
    const dx = x + 0.5 - C.x, dy = y + 0.5 - C.y; if ((dx / rx) ** 2 + (dy / ry) ** 2 <= 1) c.set(key(x, y), col(dx, dy)); }
}
function line(c, A, B, col) {
  const n = Math.max(1, Math.ceil(Math.hypot(B.x - A.x, B.y - A.y)));
  for (let i = 0; i <= n; i++) { const x = Math.round(A.x + (B.x - A.x) * i / n), y = Math.round(A.y + (B.y - A.y) * i / n); c.set(key(x, y), col(key(x, y), i, n)); }
}
function fillPoly(c, pts, col) {
  const ys = pts.map(p => p.y), y0 = Math.floor(Math.min(...ys)), y1 = Math.ceil(Math.max(...ys));
  const xs = pts.map(p => p.x), x0 = Math.floor(Math.min(...xs)), x1 = Math.ceil(Math.max(...xs));
  for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
    let inside = false; const px = x + 0.5, py = y + 0.5;
    for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) { const a = pts[i], b = pts[j]; if ((a.y > py) !== (b.y > py) && px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x) inside = !inside; }
    if (inside) c.set(key(x, y), col(x, y, (x - x0) / Math.max(1, x1 - x0), (y - y0) / Math.max(1, y1 - y0)));
  }
}

// ---------------------------------------------------------------- sheet + gif
const hexRGB = h => [1, 3, 5].map(i => parseInt(h.substr(i, 2), 16));
const BG = '#4a4e57', SHADOW = '#3f434b';
const DIRS = [[270, 'N'], [315, 'NE'], [0, 'E'], [45, 'SE'], [90, 'S'], [135, 'SW'], [180, 'W'], [225, 'NW']];
function sheetFrames(gait, enl, cols = 4) {
  const G = GAITS[gait], rows = Math.ceil(DIRS.length / cols), W = CW * cols * enl, H = CH * rows * enl, frames = [];
  for (let fi = 0; fi < G.frames; fi++) {
    const img = new Array(W * H).fill(BG);
    DIRS.forEach(([deg], i) => {
      const cx = (i % cols) * CW, cy = Math.floor(i / cols) * CH;
      for (let y = -3; y <= 3; y++) for (let x = -18; x <= 18; x++) if ((x / 18) ** 2 + (y / 4) ** 2 <= 1) put(img, W, enl, cx + OX + x, cy + OY + y, SHADOW);
      for (const [k, v] of frame(deg, gait, fi / G.frames)) put(img, W, enl, cx + k % 1000 - 200, cy + Math.floor(k / 1000) - 200, v);
    });
    frames.push(img);
  }
  return { frames, W, H };
}
function put(img, W, enl, x, y, col) { for (let dy = 0; dy < enl; dy++) for (let dx = 0; dx < enl; dx++) { const px = x * enl + dx, py = y * enl + dy; if (px >= 0 && px < W && py >= 0 && py * W + px < img.length) img[py * W + px] = col; } }
function gif(frames, W, H, delay, file) {
  const pal = new Map(); for (const f of frames) for (const c of f) if (!pal.has(c)) pal.set(c, pal.size);
  if (pal.size > 256) throw new Error('palette ' + pal.size);
  const cols = [...pal.keys()]; while (cols.length < 256) cols.push('#000000');
  const o = [], w16 = v => [v & 255, v >> 8];
  o.push(...Buffer.from('GIF89a'), ...w16(W), ...w16(H), 0xF7, 0, 0); for (const c of cols) o.push(...hexRGB(c));
  o.push(0x21, 0xFF, 0x0B, ...Buffer.from('NETSCAPE2.0'), 3, 1, 0, 0, 0);
  for (const f of frames) {
    o.push(0x21, 0xF9, 4, 0, ...w16(delay), 0, 0, 0x2C, 0, 0, 0, 0, ...w16(W), ...w16(H), 0);
    let size = 9, next = 258, dict = new Map(), bits = 0, acc = 0; const bytes = [];
    const emit = code => { acc |= code << bits; bits += size; while (bits >= 8) { bytes.push(acc & 255); acc >>>= 8; bits -= 8; } };
    emit(256); let cur = pal.get(f[0]);
    for (let i = 1; i < f.length; i++) { const k = pal.get(f[i]), kk = cur * 4096 + k;
      if (dict.has(kk)) { cur = dict.get(kk); continue; } emit(cur);
      if (next < 4096) { dict.set(kk, next++); if (next > (1 << size) && size < 12) size++; } else { emit(256); dict = new Map(); size = 9; next = 258; }
      cur = k; }
    emit(cur); emit(257); if (bits > 0) bytes.push(acc & 255);
    o.push(8); for (let i = 0; i < bytes.length; i += 255) { const ch = bytes.slice(i, i + 255); o.push(ch.length, ...ch); } o.push(0);
  }
  o.push(0x3B); fs.writeFileSync(file, Buffer.from(o));
}
const outDir = process.argv[2] || '.';
if (!process.env.NOGIF) for (const gait of ['run', 'walk']) {
  const s = sheetFrames(gait, 2); gif(s.frames, s.W, s.H, 2, `${outDir}/${gait}-8dir.gif`);
  const t = sheetFrames(gait, 1, 8); gif(t.frames, t.W, t.H, 2, `${outDir}/${gait}-8dir-fight.gif`);
  console.log(gait, s.W + 'x' + s.H);
}
// stills for review: run, 4 phases x 8 directions
module.exports = { frame, sheetFrames };
if (process.env.STILL) {
  const zlib = require('zlib'); const gait = process.env.STILL, G = GAITS[gait];
  const sel = [0, 0.125, 0.25, 0.375].map(p => Math.round(p * G.frames));
  const sh = sheetFrames(gait, 2, 8); const W = sh.W, H0 = sh.H, H = H0 * sel.length; const rgb = Buffer.alloc(W * H * 3);
  sel.forEach((fi, r) => sh.frames[fi].forEach((c, i) => { const [a, b, d] = hexRGB(c); const j = (r * H0 * W + i) * 3; rgb[j] = a; rgb[j + 1] = b; rgb[j + 2] = d; }));
  const T = []; for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; T[n] = c >>> 0; }
  const crc = b => { let c = 0xffffffff; for (const x of b) c = T[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
  const ch = (t, d) => { const l = Buffer.alloc(4); l.writeUInt32BE(d.length); const td = Buffer.concat([Buffer.from(t), d]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); };
  const ih = Buffer.alloc(13); ih.writeUInt32BE(W, 0); ih.writeUInt32BE(H, 4); ih[8] = 8; ih[9] = 2;
  const raw = Buffer.alloc((W * 3 + 1) * H); for (let y = 0; y < H; y++) rgb.copy(raw, y * (W * 3 + 1) + 1, y * W * 3, (y + 1) * W * 3);
  fs.writeFileSync(outDir + '/still-' + gait + '.png', Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), ch('IHDR', ih), ch('IDAT', zlib.deflateSync(raw)), ch('IEND', Buffer.alloc(0))]));
}
