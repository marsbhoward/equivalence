// Arm accessory mocks (rounds 1-6, 2026-11-08/09). The user's final eleven: Salamander, Quicksilver, Salt, Crucible,
// Verdigris, Rubedo, Sanguis Crystal, Fulmen Cage, Penumbra Echo, Homunculus Plated and Homunculus Hydraulic: pieces that REPLACE the whole near arm (the weapon arm, the
// one the game calls ArmFront) - shoulder to fingertips. Loaded after body.js, kit.js, sets.js, the helm
// backs and accessories.js (strap, ease, lerp, backSetLayers). Each piece draws its own layers, tagged:
//   joint 'torso'  rides the torso (the shoulder piece, a chest strap, what rises off the shoulder)
//   joint 'arm'    rides the arm (upper arm, forearm, hand) - turned about the shoulder in the raised pose
//   rise           stands above the shoulder: from behind it draws over the cape, under the head
// While one is worn the near arm's body, sleeve, glove and pauldron are hidden (stripNearArm).
// st: { t, walk, el }

const AR = {
  obsidian: ramp('#2e2632', { sheen: '#a898c0', lift: 0.36 }),
  lava: ramp('#ff6418', { sheen: '#fff2a0', lift: 0.55, shade: 0.4 }),
  horn: ramp('#9a8060', { sheen: '#fff0d0', lift: 0.45, shade: 0.4 }),
  merc: ramp('#7f92ae', { sheen: '#ffffff', lift: 0.62, shade: 0.5 }),
  salt: ramp('#dcc8d0', { sheen: '#ffffff', lift: 0.55, shade: 0.3 }),
  rose: ramp('#d88aa2', { sheen: '#fff0f4', lift: 0.5, shade: 0.32 }),
  saltRock: ramp('#7c7078', { sheen: '#e0d4d8', lift: 0.34 }),
  iron: ramp('#4c4e58', { sheen: '#bcc4d4', lift: 0.32 }),
  ember: ramp('#ff7a22', { sheen: '#ffe28a', lift: 0.5, shade: 0.45 }),
  leather: ramp('#5e3c26', { sheen: '#c89868', lift: 0.3 }),
  brass: ramp('#a8823a', { sheen: '#ffe6a0', lift: 0.42 }),
  bark: ramp('#5c4030', { sheen: '#c8a078', lift: 0.3 }),
  moss: ramp('#56802e', { sheen: '#dcf08a', lift: 0.38 }),
  leaf: ramp('#3c8a3e', { sheen: '#d8ff9a', lift: 0.42 }),
  gold: ramp('#e0a82a', { sheen: '#fff6b8', lift: 0.5 }),
  silver: ramp('#b4bccc', { sheen: '#ffffff', lift: 0.55 }),
  bone: ramp('#e2dac2', { sheen: '#ffffff', lift: 0.42, shade: 0.3 }),
  shroud: ramp('#58303e', { sheen: '#d0a0b8', lift: 0.36 }),   // caput mortuum, the pigment
  soul: ramp('#4ad8c8', { sheen: '#f0fffa', lift: 0.55 }),
  smoke: ramp('#6c6872', { sheen: '#d4d0d8', lift: 0.42 }),
};
const hash2 = (x, y) => { let h = (x * 374761393 + y * 668265263) | 0; h = (h ^ (h >>> 13)) * 1274126177; return ((h ^ (h >>> 16)) >>> 0) / 4294967296; };
const clamp1 = v => v < -1 ? -1 : v > 1 ? 1 : v;
const cylI = (cx, rx, bias = 0) => x => { const nx = clamp1((x + 0.5 - cx) / rx); return litTone(nx, -0.15, Math.sqrt(1 - nx * nx), bias); };
const domeI = (cx, cy, rx, ry, bias = 0) => (x, y) => {
  let nx = (x + 0.5 - cx) / rx, ny = (y + 0.5 - cy) / ry, d = nx * nx + ny * ny;
  if (d > 1) { const s = 1 / Math.sqrt(d); nx *= s; ny *= s; d = 1; }
  return litTone(nx, ny, Math.sqrt(1 - d), bias);
};
function AL(depth, joint, o = {}) { const l = new Layer(depth); l.joint = joint; Object.assign(l, o); return l; }
// a thick line of texels from A to B, w wide (round brush)
function armStroke(l, ax, ay, bx, by, w, col) {
  const n = Math.max(1, Math.ceil(Math.hypot(bx - ax, by - ay) * 2)), r = w / 2;
  for (let i = 0; i <= n; i++) { const x = lerp(ax, bx, i / n), y = lerp(ay, by, i / n);
    for (let yy = Math.floor(y - r); yy <= Math.ceil(y + r); yy++) for (let xx = Math.floor(x - r); xx <= Math.ceil(x + r); xx++)
      if ((xx + 0.5 - x) ** 2 + (yy + 0.5 - y) ** 2 <= r * r + 0.15) l.set(xx, yy, typeof col === 'function' ? col(xx, yy, i / n) : col); }
}
// a crystal prism from its base (bx,by) along (dx,dy), len long, w wide, with a pointed tip
function prismMask(bx, by, dx, dy, len, w) {
  const L0 = Math.hypot(dx, dy); dx /= L0; dy /= L0; const nx = -dy * w / 2, ny = dx * w / 2, s = len - w * 0.8;
  return poly([[bx + nx, by + ny], [bx + nx + dx * s, by + ny + dy * s], [bx + dx * len, by + dy * len], [bx - nx + dx * s, by - ny + dy * s], [bx - nx, by - ny]]);
}
function prism(l, bx, by, dx, dy, len, w, r) {
  const L0 = Math.hypot(dx, dy), ux = dx / L0, uy = dy / L0;
  l.fill(prismMask(bx, by, dx, dy, len, w), (x, y) => {
    const px = x + 0.5 - bx, py = y + 0.5 - by, along = px * ux + py * uy, across = (px * -uy + py * ux) / (w / 2);
    const litSide = (-uy) * -0.6 + ux * -0.7 > 0 ? across > 0 : across < 0;   // the face turned to the upper left
    if (along > len - w * 0.8 - 0.5) return tone(r, litSide ? 5 : 3);
    if (Math.abs(across) < 0.25) return tone(r, 5);
    return tone(r, litSide ? 4 : 2);
  });
}

// ---------------------------------------------------------------- 1. SALAMANDER ARM (sulphur)
// Obsidian scales split by seams of lava that pulse down the arm; horns off the shoulder, fins down the
// forearm, three talons past the fist. Embers lift off the shoulder.
const salamander = {
  id: 'salamander', name: 'Salamander Arm', principle: 'Sulphur',
  draw(st, glows) {
    const t = st.t, arm = AL(12.6, 'arm'), fist = AL(13.1, 'arm'), claw = AL(13.0, 'arm'), sh = AL(14.2, 'shoulder'), horns = AL(14.1, 'shoulder', { rise: true }), sp = st.sp || ((x, y) => [x, y]);
    const scales = (cx, rx) => (x, y) => { let i = cylI(cx, rx, 0.25)(x); const v = ((y % 3) + 3) % 3, u = (((x + (Math.floor(y / 3) % 2 ? 1 : 0)) % 3) + 3) % 3;
      if (v === 2) i -= 1; else if (v === 0 && u === 1) i += 1; return tone(AR.obsidian, i); };
    arm.fill(poly([[-1, 37], [10, 36], [10, 49], [9, 50.5], [10, 52], [10, 61], [-1, 61], [-1, 52], [0, 50.5], [-1, 49]]), scales(4.5, 6.5));
    // fins down the outer forearm
    for (const [y0, y1, tip] of [[51, 56, -6], [56, 61, -5]]) arm.fill(poly([[0, y0], [0, y1], [tip, y1 + 0.5]]), (x, y) => tone(AR.horn, y < (y0 + y1) / 2 + 1 ? 3 : 2));
    fist.fill(ell(4.8, 65, 5.4, 4.6), (x, y) => tone(AR.obsidian, domeI(4, 64, 6, 5)(x, y)));
    for (const [x0, y0, x1, y1] of [[1.5, 67, -0.5, 76], [5, 68.5, 5, 78], [8.5, 67, 10.5, 75.5]]) armStroke(claw, x0, y0, x1, y1, 2.4, (x, y, k) => tone(AR.horn, k > 0.7 ? 5 : x <= Math.min(x0, x1) + 0.5 ? 4 : 2));
    // the cap: scales over a dome, three horns pointing up and out
    const cap = ell(4, 35, 7.8, 6.2);
    sh.fill(cap, (x, y) => { let i = domeI(3, 33, 8, 7, 0.25)(x, y); const v = ((y % 3) + 3) % 3, u = (((x + (Math.floor(y / 3) % 2 ? 1 : 0)) % 3) + 3) % 3;
      if (v === 2) i -= 1; else if (v === 0 && u === 1) i += 1; return tone(AR.obsidian, i); });
    for (const p of [[[-5, 35], [-1.5, 30.5], [-11, 23]], [[-1.5, 30], [3, 29], [-2, 19.5]], [[3.5, 29], [7.5, 29.5], [6, 21.5]]])
      horns.fill(poly(p), (x, y) => tone(AR.horn, x < (p[0][0] + p[1][0]) / 2 ? 4 : 2));
    // lava seams: a crack down the arm, one across the cap, the knuckles; brightness runs down them
    const seam = (l, pts) => { for (let i = 0; i < pts.length - 1; i++) {
      let [x0, y0] = pts[i], [x1, y1] = pts[i + 1]; const n = Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0)) * 2;
      for (let k = 0; k <= n; k++) { const x = Math.round(lerp(x0, x1, k / n)), y = Math.round(lerp(y0, y1, k / n));
        if (!l.has(x, y)) continue; const run = Math.sin(t * 3.2 - y * 0.35); l.set(x, y, tone(AR.lava, run > 0.5 ? 5 : run > -0.3 ? 4 : 3)); } } };
    seam(arm, [[3, 37], [5, 40], [3, 44], [6, 48], [4, 52], [6, 55], [3, 58], [5, 61]]);
    seam(arm, [[8, 41], [7, 45], [8, 49]]);
    seam(sh, [[-2, 36], [1, 33], [4, 35], [7, 32], [10, 34]]);
    for (const x of [2, 5, 8]) fist.dot(x, 63, tone(AR.lava, 4));
    // embers off the shoulder
    const em = AL(15, 'torso', { outline: false, rise: true });
    for (let i = 0; i < 4; i++) { const p = (t * 0.55 + i * 0.27) % 1, [ox, oy] = sp(-1 + i * 3, 29), x = Math.round(ox + Math.sin(t * 2 + i * 2) * 1.5), y = Math.round(oy - p * 22);
      if (p < 0.9) em.fill(rect(x, y, x + (p < 0.5 ? 1 : 0), y + (p < 0.5 ? 1 : 0)), tone(AR.lava, p < 0.4 ? 5 : 4)); }
    const pulse = 0.5 + 0.5 * Math.sin(t * 2.4);
    if (glows) glows.push({ x: 4, y: 46, r: 11, a: 0.18 + 0.14 * pulse, c: '#ff6418', joint: 'arm' }, { x: 5, y: 64, r: 7, a: 0.22, c: '#ff8a30', joint: 'arm' }, { x: 3, y: 33, r: 8, a: 0.15 + 0.1 * pulse, c: '#ff6418', joint: 'shoulder' });
    return [arm, fist, claw, sh, horns, em];
  },
};

// ---------------------------------------------------------------- 2. QUICKSILVER ARM (mercury)
// Liquid metal in the shape of an arm, mirror-bright. A ripple runs down it (the falling bead at the
// fingertips taken off, the user's call 2026-11-09).
const chrome = (cx, cy, rx, ry) => (x, y) => {   // a mirror: sky above the horizon, the dark floor under it
  const nx = clamp1((x + 0.5 - cx) / rx), ny = clamp1((y + 0.5 - cy) / ry), h = nx * 0.9 + ny * 0.35;
  return tone(AR.merc, h < -0.62 ? 5 : h < -0.28 ? 4 : h < 0.02 ? 1 : h < 0.42 ? 2 : h < 0.78 ? 3 : 1);
};
const quicksilver = {
  id: 'quicksilver', name: 'Quicksilver Arm', principle: 'Mercury',
  draw(st, glows) {
    const t = st.t, arm = AL(12.6, 'arm'), sh = AL(14.2, 'shoulder');
    const parts = [[4, 43, 5.6, 7.2], [4.5, 50.5, 4.6, 3.2], [4.6, 55.5, 5, 6.5], [5, 65, 5.2, 4.6]];
    for (const [cx, cy, rx, ry] of parts) arm.fill(ell(cx, cy, rx, ry), chrome(cx, cy, rx, ry));
    sh.fill(ell(4.5, 35, 7.4, 6.4), chrome(4.5, 35, 7.4, 6.4));
    // molten runs off the cap's rim and the elbow, lengthening and drawing back
    for (const [x, y, ph, n] of [[-1.5, 38, 0, 6], [1.5, 40.5, 2.1, 4], [9.5, 39, 4.2, 5], [0, 52, 1.3, 4]]) {
      const len = n * (0.55 + 0.45 * Math.sin(t * 1.6 + ph)); armStroke(y > 45 ? arm : sh, x, y, x, y + len, 2, (xx, yy) => tone(AR.merc, xx + 0.5 < x ? 5 : 2));
      sh.fill(ell(x, y + len, 1.4, 1.4), (xx, yy) => tone(AR.merc, xx + 0.5 < x && yy + 0.5 < y + len ? 5 : 2)); }
    // a ripple: a bright ring sliding down the arm
    const yr = 34 + ((t * 0.6) % 1) * 38;
    for (const l of [arm, sh]) for (const [k, c] of [...l.c]) { const y = KY(k); if (Math.abs(y + 0.5 - yr) < 1) l.c.set(k, tone(AR.merc, 5)); else if (Math.abs(y + 0.5 - yr) < 2) l.c.set(k, tone(AR.merc, 4)); }
    if (glows) glows.push({ x: 3, y: 40, r: 9, a: 0.1, c: '#e8f0ff', joint: 'arm' });
    return [arm, sh];
  },
};

// ---------------------------------------------------------------- 3. SALT ARM (salt)
// Grey rock salt grown through with rose and white crystals: a cluster off the shoulder, a few down the
// forearm, small points on the knuckles. A glint jumps between the crystal tips.
const salt = {
  id: 'salt', name: 'Salt Arm', principle: 'Salt',
  draw(st, glows) {
    const t = st.t, arm = AL(12.6, 'arm'), xa = AL(12.8, 'arm'), sh = AL(14.2, 'shoulder'), xs = AL(14.3, 'shoulder', { rise: true });
    const rock = cx => (x, y) => tone(AR.saltRock, cylI(cx, 6)(x) + (hash2(x >> 1, y >> 1) > 0.72 ? 1 : hash2(x >> 1, y >> 1) < 0.2 ? -1 : 0));
    arm.fill(poly([[-1, 37], [10, 37], [9, 50], [10, 61], [9, 69], [1, 69], [-1, 64], [0, 50]]), rock(4.5));
    for (const [x, y] of [[2, 62], [6, 63], [4, 66]]) arm.dot(x, y, tone(AR.saltRock, 1));
    sh.fill(poly([[-3, 37], [-2, 31], [3, 28], [9, 29], [12, 33], [11, 40], [-1, 41]]), rock(4));
    const tips = [];
    const P = (l, bx, by, dx, dy, len, w, r) => { prism(l, bx, by, dx, dy, len, w, r); const L0 = Math.hypot(dx, dy); tips.push([Math.round(bx + dx / L0 * (len - 1)), Math.round(by + dy / L0 * (len - 1))]); };
    P(xs, 1, 33, -0.45, -1, 13, 4, AR.salt);
    P(xs, 5, 31, 0.05, -1, 9, 3, AR.rose);
    P(xs, -2, 36, -1, -0.55, 9, 3, AR.rose);
    P(xs, 8, 31, 0.45, -1, 6, 3, AR.salt);
    P(xa, 0, 53, -1, 0.15, 6, 3, AR.salt);
    P(xa, 0, 57, -1, 0.5, 5, 3, AR.rose);
    P(xa, 0, 44, -1, -0.3, 4, 2, AR.salt);
    P(xa, 8, 66, 0.5, 1, 4, 2, AR.rose);
    P(xa, 4, 67, 0, 1, 4, 2, AR.salt);
    const gl = AL(15, 'torso', { outline: false }), i = Math.floor(t * 1.4) % tips.length, f = (t * 1.4) % 1;
    if (f < 0.45) { const [x, y] = tips[i]; gl.set(x, y, '#ffffff'); if (f < 0.3) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) gl.set(x + dx, y + dy, tone(AR.salt, 5)); gl.joint = i >= 4 ? 'arm' : 'shoulder'; }
    if (glows) glows.push({ x: 1, y: 25, r: 9, a: 0.12, c: '#ffd8e4', joint: 'shoulder' });
    return [arm, xa, sh, xs, gl];
  },
};

// ---------------------------------------------------------------- 4. CRUCIBLE ARM
// Black iron worked like a furnace: a box pauldron with a glowing grate (its stack and smoke taken off,
// the user's call 2026-11-09), a riveted arm
// on a brass elbow cog, a gauntlet with fire behind its slits. A leather strap across the chest holds it on.
const crucible = {
  id: 'crucible', name: 'Crucible Arm', principle: 'the furnace',
  draw(st, glows) {
    const t = st.t, arm = AL(12.6, 'arm'), cog = AL(12.9, 'arm'), gaunt = AL(13.0, 'arm'), sh = AL(14.2, 'shoulder');
    const strapL = AL(8.7, 'torso');
    const fl = k => { const v = Math.sin(t * 9 + k * 2.1) + Math.sin(t * 5.3 + k); return v > 0.6 ? 5 : v > -0.6 ? 4 : 3; };
    strap(strapL, 8, 33, 30, 60, 4, AR.leather);
    strapL.fill(rect(18, 45, 21, 48), (x, y) => tone(AR.brass, x === 18 || y === 45 ? 5 : 2)); strapL.fill(rect(19, 46, 20, 47), tone(AR.leather, 1));
    arm.fill(rect(-1, 40, 10, 50), x => tone(AR.iron, cylI(4.5, 6.5)(x)));
    for (const y of [43, 47]) arm.over(rect(-2, y, 11, y), x => tone(AR.iron, cylI(4.5, 6.5)(x) + 1));
    for (const [x, y] of [[1, 45], [8, 45]]) arm.dot(x, y, tone(AR.iron, 5));
    cog.fill(ell(4.5, 51, 4.6, 3.4), (x, y) => tone(AR.brass, domeI(4, 50, 5, 4)(x, y)));
    for (const x of [0, 9]) cog.fill(rect(x, 50, x, 51), tone(AR.brass, 2));
    gaunt.fill(poly([[-3, 53], [12, 53], [11, 62], [-1, 62]]), (x, y, m) => tone(AR.iron, cylI(4.5, 7.5)(x) + (y === 53 ? 1 : 0)));
    for (const x of [1, 4, 7]) for (let y = 55; y <= 60; y++) gaunt.set(x, y, y === 55 ? tone(AR.iron, 0) : tone(AR.ember, fl(x + y * 0.2)));
    gaunt.fill(rect(-1, 62, 10, 69), (x, y) => tone(AR.iron, (y - 62) % 3 === 0 ? 4 : cylI(4.5, 6.5)(x)));
    for (const x of [1, 4, 7]) gaunt.fill(rect(x, 63, x + 1, 64), (xx, y) => tone(AR.brass, y === 63 ? 5 : 3));
    // the pauldron: an iron box, chamfered, a brass rim, a grate of three slits
    const box = poly([[-5, 31], [-1, 26], [11, 26], [14, 31], [14, 42], [-5, 42]]);
    sh.fill(box, plate(AR.iron, 1));
    sh.over(rect(-6, 41, 15, 42), (x, y) => tone(AR.brass, y === 41 ? 4 : 2));
    for (const x of [0, 3, 6]) for (let y = 31; y <= 38; y++) sh.set(x, y, y === 31 ? tone(AR.iron, 0) : tone(AR.ember, fl(x)));
    for (const [x, y] of [[-3, 33], [11, 30], [11, 38], [-3, 39]]) sh.dot(x, y, tone(AR.iron, 5));
    const fk = 0.5 + 0.5 * Math.sin(t * 7);
    if (glows) glows.push({ x: 3.5, y: 35, r: 9, a: 0.2 + 0.1 * fk, c: '#ff7a22', joint: 'shoulder' }, { x: 4.5, y: 58, r: 8, a: 0.18 + 0.1 * fk, c: '#ff7a22', joint: 'arm' });
    return [strapL, arm, cog, gaunt, sh];
  },
};

const ARMS = [salamander, quicksilver, salt, crucible];


// ================================================================ ROUND 2 (2026-11-08): ten more, Verdigris and Rubedo kept
Object.assign(AR, {
  copper: ramp('#b0663a', { sheen: '#ffd0a0', lift: 0.38 }),
  patina: ramp('#4f9e88', { sheen: '#c8f4e0', lift: 0.36 }),
  snake: ramp('#6c8a2a', { sheen: '#f4e888', lift: 0.42 }),
  belly: ramp('#d8c070', { sheen: '#fff6c8', lift: 0.4 }),
  wrap: ramp('#4a4452', { sheen: '#b0a8c0', lift: 0.32 }),
  glass: ramp('#9ab4c4', { sheen: '#ffffff', lift: 0.55, shade: 0.35 }),
  fur: ramp('#b8582a', { sheen: '#ffc888', lift: 0.4 }),
  mane: ramp('#7a2a18', { sheen: '#f0905a', lift: 0.38 }),
  claw: ramp('#d8ccb0', { sheen: '#ffffff', lift: 0.4 }),
  crow: ramp('#2a2c40', { sheen: '#9ab0f0', lift: 0.4 }),
  moon: ramp('#b8c4d8', { sheen: '#ffffff', lift: 0.55, shade: 0.34 }),
  enamel: ramp('#2c4a8a', { sheen: '#9ac0ff', lift: 0.4 }),
  lacquer: ramp('#8a1424', { sheen: '#ff9a8a', lift: 0.4 }),
  stone: ramp('#d0142e', { sheen: '#ffb0a0', lift: 0.5 }),
  clay: ramp('#9a5a3a', { sheen: '#f0b890', lift: 0.34 }),
  sigil: ramp('#ffcf5a', { sheen: '#fffbe0', lift: 0.5 }),
  storm: ramp('#3a4458', { sheen: '#a8c0e8', lift: 0.34 }),
  arc: ramp('#5ad8ff', { sheen: '#ffffff', lift: 0.6 }),
  wax: ramp('#e4d8bc', { sheen: '#fffbea', lift: 0.45, shade: 0.26 }),
  flame: ramp('#ff9a2a', { sheen: '#fff6b0', lift: 0.55 }),
});
const SP0 = (x, y) => [x, y];
// the plain arm column most pieces start from, wider by `w`
const armCol = (w = 0) => poly([[-1 - w, 37], [10 + w, 37], [10 + w, 61], [-1 - w, 61]]);
const fistM = (r = 5.2) => ell(4.8, 65, r, r * 0.88);

// ---------------------------------------------------------------- 7. VERDIGRIS ARM
// Copper clockwork gone green: riveted plates crusted with patina, a piston down the outer elbow that pumps,
// and a cog on the shoulder that turns.
const verdigris = {
  id: 'verdigris', name: 'Verdigris Arm', principle: 'copper (Venus)',
  draw(st, glows) {
    const t = st.t, arm = AL(12.6, 'arm'), pis = AL(12.5, 'arm', { outR: 2 }), sh = AL(14.2, 'shoulder');
    const cu = (cx, rx) => (x, y) => { const pt = hash2(x >> 2, (y + 1) >> 2); return pt > 0.8 ? tone(AR.patina, cylI(cx, rx)(x)) : tone(AR.copper, cylI(cx, rx)(x)); };
    arm.fill(armCol(), cu(4.5, 6.5));
    for (const y of [44, 50, 56]) arm.over(rect(-3, y, 12, y), x => tone(AR.copper, 1));
    for (const [x, y] of [[0, 41], [8, 41], [0, 53], [8, 53], [0, 59], [8, 59]]) arm.dot(x, y, tone(AR.copper, 5));
    arm.fill(fistM(), (x, y) => tone(AR.copper, domeI(4, 64, 6, 5)(x, y)));
    for (const x of [2, 5, 8]) arm.dot(x, 62, tone(AR.patina, 4));
    const k = 0.5 + 0.5 * Math.sin(t * 4);   // the piston: a rod sliding in a sleeve
    pis.fill(rect(-4, 42, -3, 47 + Math.round(k * 3)), x => tone(AR.patina, x === -4 ? 4 : 2));
    pis.fill(rect(-5, 50 + Math.round(k * 3), -2, 58), x => tone(AR.copper, x === -4 ? 4 : x === -1 ? 2 : 3));
    // the cog: eight teeth round a patina hub, turning
    const rot = t * 1.2, cog = new Mask();
    for (let y = 24; y <= 44; y++) for (let x = -6; x <= 15; x++) { const dx = x + 0.5 - 4.5, dy = y + 0.5 - 34, r = Math.hypot(dx, dy), a = Math.atan2(dy, dx) + rot;
      if (r < 6.6 || (r < 9 && ((a / (Math.PI / 4)) % 1 + 1) % 1 < 0.5)) cog.add(x, y); }
    sh.fill(cog, (x, y) => tone(AR.copper, domeI(3, 32, 9, 9)(x, y)));
    sh.over(ell(4.5, 34, 3.6, 3.6), (x, y) => tone(AR.patina, domeI(3.5, 33, 4, 4)(x, y)));
    sh.over(rect(4, 33, 5, 34), tone(AR.copper, 5));
    return [arm, pis, sh];
  },
};

// ---------------------------------------------------------------- 13. RUBEDO ARM
// The reddening, the last stage: crimson lacquered plate traced in gold, and the Stone itself set in the back
// of the hand. Its light runs up the arm in thin veins.
const rubedo = {
  id: 'rubedo', name: 'Rubedo Arm', principle: 'the reddening',
  draw(st, glows) {
    const t = st.t, arm = AL(12.6, 'arm'), fist = AL(13.0, 'arm'), sh = AL(14.2, 'shoulder');
    arm.fill(armCol(), x => tone(AR.lacquer, cylI(4.5, 6.5)(x)));
    for (const y of [41, 49, 53]) arm.over(rect(-3, y, 12, y), (x) => tone(AR.gold, x < 2 ? 4 : 3));
    for (const y of [37, 61]) arm.over(rect(-3, y, 12, y), tone(AR.gold, 2));
    const pulse = (t * 0.8) % 1;
    for (const [x, ph] of [[3, 0], [6, 0.35]]) for (let y = 42; y <= 60; y++) { if (y === 49 || y === 53) continue; const run = Math.abs(((60 - y) / 18) - ((pulse + ph) % 1)) < 0.12;
      arm.set(x + (y % 4 < 2 ? 0 : 1), y, run ? tone(AR.stone, 5) : tone(AR.lacquer, 1)); }
    fist.fill(fistM(), (x, y) => tone(AR.lacquer, domeI(4, 64, 6, 5)(x, y)));
    fist.fill(ell(4.8, 64.5, 2.6, 2.4), (x, y) => tone(AR.gold, 3));
    fist.fill(ell(4.8, 64.5, 1.8, 1.7), (x, y) => tone(AR.stone, x < 5 && y < 64 ? 5 : 3 + Math.round(Math.sin(t * 3))));
    for (let i = 2; i >= 0; i--) { const cy = 31 + i * 3.4, rx = 8 - i * 0.6; sh.fill(ell(4.5, cy, rx, 4.2), (x, y) => { const m = ell(4.5, cy, rx, 4.2); return !m.has(x, y + 1) ? tone(AR.gold, 3) : tone(AR.lacquer, domeI(3, cy - 1, rx + 1, 5)(x, y)); }); }
    sh.fill(ell(4.5, 30.5, 2.4, 2.4), (x, y) => tone(AR.gold, x < 4 ? 5 : 3));
    const k = 0.5 + 0.5 * Math.sin(t * 3);
    if (glows) glows.push({ x: 4.8, y: 64.5, r: 8, a: 0.3 + 0.15 * k, c: '#ff2a3a', joint: 'arm' });
    return [arm, fist, sh];
  },
};

ARMS.push(verdigris, rubedo);


// ================================================================ ROUND 3 (2026-11-09): arms MADE of a substance
// Not armour on an arm: the arm itself is shadow, blood, smoke or lightning, holding the arm's shape.
Object.assign(AR, {
  shade: ramp('#1c1630', { sheen: '#8a6ad8', lift: 0.36 }),
  violet: ramp('#7a52d8', { sheen: '#e4d8ff', lift: 0.45 }),
  blood: ramp('#7c0c1c', { sheen: '#ff7070', lift: 0.45, shade: 0.42 }),
  bloodGlass: ramp('#b8142c', { sheen: '#ffc8c8', lift: 0.5 }),
  fume: ramp('#76707f', { sheen: '#ece8f4', lift: 0.46 }),
  volt: ramp('#2e62e0', { sheen: '#eefaff', lift: 0.58 }),
  voltDeep: ramp('#16245a', { sheen: '#6e9cff', lift: 0.42 }),
});
// a jagged bolt from A to B, re-rolled each `frame`; a white core with a blue fringe
function zap(l, ax, ay, bx, by, seed, frame, jag = 3, n = 5) {
  let px = ax, py = ay;
  for (let i = 1; i <= n; i++) { const k = i / n, x = lerp(ax, bx, k) + (i === n ? 0 : (hash2(frame * 13 + seed, i) - 0.5) * jag * 2), y = lerp(ay, by, k) + (i === n ? 0 : (hash2(seed * 7 + i, frame) - 0.5) * jag);
    l.line(px, py, x, y, '#ffffff', false); l.line(px + 1, py, x + 1, y, tone(AR.volt, 4), false); px = x; py = y; }
}

// ---------------------------------------------------------------- ROUND 3 STYLES (2026-11-09): three of each; the user kept
// Sanguis Crystal and Fulmen Cage (the base Sanguis, Sublimate and Fulmen and the first shadow styles came off)
Object.assign(AR, {
  voidK: ramp('#0a0812', { sheen: '#5a4a90', lift: 0.3 }),
  vessel: ramp('#9a1020', { sheen: '#ff8080', lift: 0.45 }),
  flesh: ramp('#2c0c14', { sheen: '#a04050', lift: 0.3 }),
  char: ramp('#3a3238', { sheen: '#a89aa0', lift: 0.4 }),
  incense: ramp('#c8c0d8', { sheen: '#ffffff', lift: 0.45, shade: 0.34 }),
  stormC: ramp('#3c4662', { sheen: '#aebee8', lift: 0.42 }),
});

// SANGUIS: CRYSTAL - blood frozen hard: a faceted ruby arm with angular plates, a shard jutting back off the
// elbow, shard fingers, and the shoulder a cluster of broad crystal plates. Light glints across the facets.
const sanguisCrystal = {
  id: 'sanguis-crystal', name: 'Sanguis Arm', style: 'Crystal', principle: 'blood',
  draw(st, glows) {
    const t = st.t, arm = AL(12.6, 'arm'), sh = AL(14.2, 'shoulder', { rise: true });
    const glint = ((t * 0.5) % 1) * 60 - 10;
    // irregular facets: the nearest of a scatter of seeds owns each texel, each facet lit by its own tilt
    const seeds = []; for (let k = 0; k < 26; k++) seeds.push([-4 + hash2(k, 3) * 17, 24 + hash2(5, k) * 50, hash2(k, k + 9)]);
    const own = (x, y) => { let b = 0, bd = 1e9; seeds.forEach(([sx, sy], k) => { const d = (x - sx) ** 2 + (y - sy) ** 2; if (d < bd) { bd = d; b = k; } }); return b; };
    const facet = (x, y) => { const k = own(x, y), edge = own(x + 1, y) !== k || own(x, y + 1) !== k;
      if (Math.abs((x + y) - glint - 40) < 1.2 && seeds[k][2] > 0.4) return tone(AR.bloodGlass, 5);
      return edge ? tone(AR.bloodGlass, 4) : tone(AR.blood, seeds[k][2] > 0.66 ? 4 : seeds[k][2] > 0.33 ? 3 : 2); };
    const m = U(poly([[-1, 37], [10, 36], [11, 43], [10, 49], [11, 56], [9.5, 61], [-1, 61], [-1.5, 55], [-0.5, 49], [-1.5, 43]]), poly([[0, 61], [9.5, 61], [10, 68], [5, 70], [0, 68]]),
      poly([[-1, 48], [-7, 44], [-1, 52]]));
    for (const [x, len] of [[1, 5], [3.8, 6], [6.6, 5.5], [9, 4]]) m.union(poly([[x - 1.1, 69], [x + 1.1, 69], [x, 69 + len]]));
    arm.fill(m, facet);
    const plates = U(poly([[-5, 38], [-3, 30], [3, 27], [5, 33], [0, 40]]), poly([[2, 33], [5, 25], [10, 27], [12, 34], [6, 38]]), poly([[-2, 36], [4, 31], [11, 33], [10, 40], [1, 41]]));
    sh.fill(plates, facet);
    if (glows) glows.push({ x: 4.5, y: 48, r: 11, a: 0.12, c: '#ff2a3a', joint: 'arm' }, { x: 3, y: 31, r: 9, a: 0.14, c: '#ff4a5a', joint: 'shoulder' });
    return [arm, sh];
  },
};

// FULMEN: CAGE - no body at all: the arm is only lightning in its shape, bolts down both edges with zigzags
// across, re-struck every frame, a ring of bolts for the shoulder and sparks off the fingers.
const fulmenCage = {
  id: 'fulmen-cage', name: 'Fulmen Arm', style: 'Cage', principle: 'lightning',
  draw(st, glows) {
    const t = st.t, frame = Math.floor(t * 12), arm = AL(12.6, 'arm', { outR: 2 }), sh = AL(14.2, 'shoulder', { outR: 2, rise: true });
    zap(arm, -0.5, 37, -0.5, 64, 1, frame, 1.2, 7); zap(arm, 9.5, 37, 9, 64, 2, frame, 1.2, 7);
    for (let i = 0; i < 5; i++) { const y = 39 + i * 6; zap(arm, i % 2 ? 9 : 0, y, i % 2 ? 0 : 9, y + 4, 3 + i, frame, 1, 3); }
    zap(arm, 0, 64, 9, 64, 9, frame, 1, 3);
    for (const [bx, ex, ey, sd] of [[0.5, -1.5, 73, 11], [3.5, 3, 75, 12], [6.5, 7, 75, 13], [9, 11, 72, 14]]) zap(arm, bx, 64, ex, ey, sd, frame, 1.2, 3);
    for (let i = 0; i < 8; i++) { const a0 = i / 8 * Math.PI * 2, a1 = (i + 1) / 8 * Math.PI * 2; zap(sh, 4.5 + Math.cos(a0) * 6.5, 33 + Math.sin(a0) * 6, 4.5 + Math.cos(a1) * 6.5, 33 + Math.sin(a1) * 6, 20 + i, frame, 0.8, 2); }
    const fk = hash2(frame, 7);
    if (glows) glows.push({ x: 4.5, y: 33, r: 11, a: 0.2 + 0.15 * fk, c: '#3a7aff', joint: 'shoulder' }, { x: 4.5, y: 52, r: 12, a: 0.18 + 0.15 * fk, c: '#3a7aff', joint: 'arm' });
    return [arm, sh];
  },
};


// ---------------------------------------------------------------- ROUND 4 (2026-11-09): the shadow as an AFTERIMAGE
// "Dark and translucent like an afterimage" (the user). These layers carry `alpha`: they blend over what is
// under them (composeA, and the page's painter), outline included - in the game a whole-sprite alpha on the
// arm's SpriteRenderers. An arm with `echo` is drawn again at earlier moments, fainter and behind it
// (withEchoes), so it trails itself whenever it moves.
Object.assign(AR, { ghost: ramp('#1c1828', { sheen: '#7a6aa8', lift: 0.36 }) });
const ghostArmMask = () => U(poly([[-0.5, 37], [9.5, 37], [10, 49], [9.5, 61], [-0.5, 61], [-1, 49]]), ell(4.8, 65, 5, 4.4));
const ghostFill = m => (x, y) => !m.has(x - 1, y) ? tone(AR.violet, 3) : tone(AR.ghost, cylI(4.5, 6)(x));
// the idle drift that separates an echo from the arm when it is otherwise still
const drift = t => [Math.sin(t * 1.6) * 2.4, Math.sin(t * 2.3) * 0.8];
function shifted(l, dx, dy) { const o = new Layer(l.depth); Object.assign(o, { joint: l.joint, outline: l.outline, far: l.far, outR: l.outR, rise: l.rise, alpha: l.alpha }); for (const [k, c] of l.c) o.c.set(K(KX(k) + Math.round(dx), KY(k) + Math.round(dy)), c); return o; }

// ECHO - a dark see-through arm that trails two fainter copies of itself wherever it has just been
const penumbraEcho = {
  id: 'penumbra-echo', name: 'Penumbra Arm', style: 'Echo', principle: 'shadow',
  echo: [{ dt: 0.12, alpha: 0.42 }, { dt: 0.24, alpha: 0.2 }],
  draw(st, glows) {
    const t = st.t, [dx, dy] = drift(t), m = ghostArmMask(), sm = ell(4.5, 34.5, 7.2, 5.8);
    const arm = AL(12.6, 'arm', { alpha: 0.72 }).fill(m, ghostFill(m)), sh = AL(14.2, 'shoulder', { alpha: 0.72 }).fill(sm, ghostFill(sm));
    for (const [x, y] of [[1.5, 70], [4, 71], [6.5, 70], [9, 69]]) armStroke(arm, x, 68, x + (x - 5) * 0.2, y + 2, 1.4, tone(AR.ghost, 3));
    return [shifted(arm, dx, dy), shifted(sh, dx * 0.5, dy * 0.5)];
  },
};


// ---------------------------------------------------------------- ROUND 6 (2026-11-09): HOMUNCULUS, the synthetic arm
// The engine arms (Perpetuum, round 5) missed: "more robotic - a mechanical, synthetic arm, steel with exposed
// black cables" (the user, with close-ups of a prop: plates with perforated mesh panels, bundles of ribbed black
// hoses between them, a piston with a spring, and a black hand with a spring tendon down each finger's back).
// The homunculus is the alchemists' made person. Everything inside the silhouette sits on ONE layer, so no part's
// outline eats another; only hoses that loop outside it get their own (thin-outlined) layer.
Object.assign(AR, {
  steel: ramp('#9aa6b4', { sheen: '#ffffff', lift: 0.52, shade: 0.36 }),
  cable: ramp('#2a2a32', { sheen: '#9c9cb4', lift: 0.5 }),
  synth: ramp('#2c2c34', { sheen: '#9a9ab0', lift: 0.42 }),
});
// a ribbed black cable as a column of rows (alternating), x0..x1 wide, y0..y1 long
function cableRun(l, x0, x1, y0, y1, ph = 0) { for (let y = Math.round(y0); y <= Math.round(y1); y++) for (let x = Math.round(x0); x <= Math.round(x1); x++) l.set(x, y, ((y + ph) % 2) ? tone(AR.cable, x === Math.round(x0) ? 5 : 4) : tone(AR.cable, 2)); }
// steel plate: lit-left cylinder, a bright top edge and a dark bottom edge
const steelPlate = (cx, rx) => (x, y, m) => !m.has(x, y - 1) ? tone(AR.steel, 5) : !m.has(x, y + 1) ? tone(AR.steel, 1) : tone(AR.steel, cylI(cx, rx)(x));
// a perforated mesh panel over what is already drawn
function mesh(l, x0, y0, x1, y1) { l.over(rect(x0, y0, x1, y1), (x, y) => (x === x0 || y === y0) ? tone(AR.steel, 1) : ((x + y) % 2 ? tone(AR.cable, 1) : tone(AR.steel, 2))); }
// the synthetic hand: a dark back with spring tendons from the wrist to steel knuckle caps, and long jointed
// fingers, each with its spring down the back (the reference's hand)
function synthHand(l, t, top = 62, long = 11, steelBack = false) {
  l.fill(poly([[-0.5, top], [9.5, top], [10.5, top + 6], [-0.5, top + 6]]), x => steelBack ? tone(AR.steel, cylI(5, 6)(x)) : tone(AR.synth, cylI(5, 6)(x)));
  const fx = [[-0.5, long - 2, -0.35], [2.5, long, -0.12], [5.5, long, 0.12], [8.5, long - 2, 0.35]];
  fx.forEach(([x0, len, lean], i) => {
    const kx = Math.round(x0 + 0.5), ky = top + 5;
    for (let y = top + 1; y < ky; y++) l.set(kx, y, y % 2 ? tone(AR.steel, 5) : tone(AR.steel, 2));   // the tendon spring
    l.fill(rect(kx - 1, ky, kx + 1, ky + 1), (x, y) => tone(AR.steel, y === ky ? 5 : 3));             // the knuckle cap
    const flex = Math.sin(t * 1.8 + i) * 0.15;
    for (let j = 0; j < len; j++) { const x = Math.round(x0 + (lean + flex * (j / len)) * j), y = ky + 2 + j;
      l.set(x, y, j % 4 === 3 ? tone(AR.steel, 3) : tone(AR.synth, 4)); l.set(x + 1, y, j % 2 && j < len - 2 ? tone(AR.steel, 4) : tone(AR.synth, 2)); }
  });
}

// PLATED - steel plates with gaps between them where bundles of black cable run; a mesh panel on each plate,
// a hinge disc at the elbow, a piston with a spring down the inside, cables arching off the shoulder.
const homunculusPlated = {
  id: 'homunculus-plated', name: 'Homunculus Arm', style: 'Plated', principle: 'the made man',
  draw(st, glows) {
    const t = st.t, k = 0.5 + 0.5 * Math.sin(t * 3), arm = AL(12.6, 'arm'), hose = AL(12.4, 'shoulder', { outR: 2 }), sh = AL(14.2, 'shoulder');
    const up = poly([[-1, 34], [10.5, 34], [10, 46], [-0.5, 46]]);
    arm.fill(up, steelPlate(4.5, 6.5)); mesh(arm, 2, 37, 7, 42);
    cableRun(arm, -0.5, 1.5, 46, 53, 0); cableRun(arm, 2.5, 5, 46, 53, 1); cableRun(arm, 6, 9.5, 46, 53, 0);
    arm.fill(ell(4.5, 49.5, 2.6, 2.6), (x, y) => Math.hypot(x + 0.5 - 4.5, y + 0.5 - 49.5) < 1 ? tone(AR.steel, 1) : tone(AR.steel, domeI(4, 49, 3, 3)(x, y)));
    const fo = poly([[-1.5, 53], [10.5, 53], [10, 61], [0, 61]]);
    arm.fill(fo, steelPlate(4.5, 7)); mesh(arm, 1, 55, 5, 59); for (const x of [7, 9]) arm.over(rect(x, 55, x, 59), tone(AR.cable, 1));
    // the piston down the inner side: a rod in a spring, compressing
    arm.fill(rect(10, 41, 11, 58), x => tone(AR.steel, x === 10 ? 4 : 2));
    for (let y = 47 + Math.round(k * 2); y <= 54; y++) arm.fill(rect(9.5, y, 11.5, y), y % 2 ? tone(AR.steel, 5) : tone(AR.steel, 1));
    synthHand(arm, t, 61);
    // the shoulder: a rounded steel cap with a mesh vent, two black cables arching off it into the upper arm
    const cap = poly([[-4, 33], [-4, 30], [-1, 26.5], [9, 26.5], [12, 30], [12, 33]]);
    sh.fill(cap, (x, y, m) => !m.has(x, y - 1) ? tone(AR.steel, 5) : tone(AR.steel, domeI(2, 30, 10, 8)(x, y))); mesh(sh, 2, 29, 8, 31);
    for (const [ax, bx, bulge, ph] of [[-3, -1, -7, 0], [-2, 0, -5, 1]]) for (let i = 0; i <= 14; i++) { const u = i / 14, x = lerp(ax, bx, u) + Math.sin(u * Math.PI) * (bulge - ax), y = lerp(34, 47, u);
      for (let w = 0; w < 2; w++) hose.set(Math.round(x) + w, Math.round(y), (i + ph) % 2 ? tone(AR.cable, w ? 3 : 5) : tone(AR.cable, 2)); }
    return [hose, arm, sh];
  },
};

// HYDRAULIC - a clean synthetic steel shell with panel lines, and thick ribbed black hoses looping off it from
// shoulder to elbow and elbow to wrist; a pivot at the elbow, small status lights, the synthetic hand.
const homunculusHydraulic = {
  id: 'homunculus-hydraulic', name: 'Homunculus Arm', style: 'Hydraulic', principle: 'the made man',
  draw(st, glows) {
    const t = st.t, arm = AL(12.6, 'arm'), hose = AL(12.4, 'arm', { outR: 2 }), shHose = AL(12.4, 'shoulder', { outR: 2 }), sh = AL(14.2, 'shoulder');
    const shell = poly([[-0.5, 34], [9.5, 34], [10, 47], [9, 49], [10, 51], [10.5, 61], [-0.5, 61], [-1, 51], [0, 49], [-1, 47]]);
    arm.fill(shell, (x, y) => tone(AR.steel, cylI(4.5, 6.5)(x)));
    for (const y of [41, 49, 50, 56]) arm.over(rect(-2, y, 12, y), tone(AR.steel, 1));
    arm.over(rect(6, 36, 6, 40), tone(AR.steel, 1)); arm.over(rect(3, 52, 3, 55), tone(AR.steel, 1));
    arm.fill(ell(-0.5, 49.5, 2, 2), (x, y) => tone(AR.steel, x < 0 ? 5 : 2));
    const blink = (t * 1.2) % 1 < 0.5; arm.dot(8, 44, blink ? '#7af0ff' : tone(AR.steel, 2)); arm.dot(8, 58, !blink ? '#7af0ff' : tone(AR.steel, 2));
    synthHand(arm, t, 61);
    const loop = (l, ay, by, bulge, ph) => { for (let i = 0; i <= 18; i++) { const u = i / 18, x = -1 - Math.sin(u * Math.PI) * bulge, y = lerp(ay, by, u);
      for (let w = 0; w < 3; w++) l.set(Math.round(x) - w + 1, Math.round(y), (i + ph) % 2 ? tone(AR.cable, w === 2 ? 5 : 4) : tone(AR.cable, 2)); } };
    loop(shHose, 34, 48, 6, 0); loop(hose, 51, 61, 5, 1);
    sh.fill(poly([[-3.5, 33], [-3.5, 30], [-0.5, 26.5], [9.5, 26.5], [12, 30], [12, 33]]), (x, y, m) => !m.has(x, y - 1) ? tone(AR.steel, 5) : tone(AR.steel, domeI(2, 30, 10, 8)(x, y)));
    for (const y of [30]) sh.over(rect(-4, y, 13, y), tone(AR.steel, 1));
    if (glows) glows.push({ x: 8, y: 44, r: 4, a: blink ? 0.4 : 0.1, c: '#7af0ff', joint: 'arm' });
    return [shHose, hose, arm, sh];
  },
};

// draw an arm posed, with its echoes (if it has any) at earlier moments, fainter and behind; Pof(t) gives the
// pose at time t (a moving pose trails, a still one only by the arm's own drift)
function withEchoes(arm, st, Pof) {
  const one = (t, alpha, depthLift) => { const P = Pof(t), g = [], s2 = Object.assign({}, st, { t, sp: shoulderPoint(P) });
    const [ls, gs] = poseArm(arm.draw(s2, g), g, P);
    if (alpha < 1) ls.forEach(l => { l.alpha = (l.alpha ?? 1) * alpha; l.depth -= depthLift; });
    return [ls, gs]; };
  const out = [], glows = [];
  for (const [i, e] of (arm.echo || []).entries()) { const [ls] = one(st.t - e.dt, e.alpha, 0.2 + i * 0.05); out.push(...ls); }
  const [ls, gs] = one(st.t, 1, 0); out.push(...ls); glows.push(...gs);
  return [out, glows];
}
// compose with see-through layers: a layer with `alpha` blends over what is under it (bg where nothing is)
function composeA(layers, bg = '#4b4e57') {
  const grid = new Map(), all = [...layers].sort((a, b) => a.depth - b.depth);
  for (const l of all) {
    const a = l.alpha ?? 1, put = (k, c) => grid.set(k, a >= 1 ? c : mix(grid.get(k) || bg, c, a));
    if (l.outline) { const m = l.mask(), R = l.outR ?? OUT_R, ring = new Set();
      for (const [x, y] of m.pts()) for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) { if (dx * dx + dy * dy > R * R + 2) continue; const k = K(x + dx, y + dy); if (!m.s.has(k)) ring.add(k); }
      for (const k of ring) put(k, OUTLINE); }
    for (const [k, c] of l.c) put(k, l.far ? scale(c, FAR_SHADE) : c);
  }
  return grid;
}

ARMS.push(sanguisCrystal, fulmenCage, penumbraEcho, homunculusPlated, homunculusHydraulic);

// ---------------------------------------------------------------- wearing one
// The near arm's body and the set's near sleeve, glove and pauldron come off while an arm piece is on.
const NEAR_ARM_DEPTHS = [DEPTH.nearSleeve, DEPTH.nearGlove, DEPTH.nearPauldron];
const isNearArm = l => (!l.far && NEAR_ARM_DEPTHS.includes(l.depth)) || (l.outline === false && l.depth === 12);
function stripNearArm(set) {
  if (!set) return null;
  const o = Object.assign({}, set);
  for (const slot of SLOTS) if (set[slot]) o[slot] = () => set[slot]().filter(l => !isNearArm(l));
  return o;
}
function wearArmFront(set) {   // wear(set) without its near arm
  const s = stripNearArm(set), out = Object.values(bodyLayers()).filter(l => !isNearArm(l));
  if (s) for (const slot of SLOTS) if (s[slot]) out.push(...s[slot]());
  return out;
}
function wearArmBack(set) { return backSetLayers(stripNearArm(set)).filter(l => !(l.outline === false && l.depth === 12)); }
// from behind: the arm with the gloves (16), the shoulder with the pauldrons (19), what rises over the cape
function armBackRank(layers) {
  layers.forEach((l, i) => { l.depth = l.rise ? 21 + i / 100 : l.joint === 'arm' ? 16.5 + i / 100 + (l.over ? 3 : 0) : l.depth < 10 ? 8.6 + i / 100 : 19.2 + i / 100; });
  return layers;
}
// ---------------------------------------------------------------- holding the weapons
// The rest carry of each class, read off the game's rig (2026-11-08, Hoplite, facing the camera), in body
// texels and on-screen degrees (clockwise; the rig's z angle negated). The greatsword is carried high: the
// upper arm turned up and out, the forearm back across, the hilt by the face and the blade laid down across
// the body. A disc sits in the near hand in front of the hip. The bow is in the FAR hand; the near arm hangs.
const HOLDS = {
  greatsword: { shoulder: [5, 29.7], elbow: [5, 49.7], upper: 120, fore: 200, at: [-7.7, 7], deg: 130, depth: 12.55, hand: 'near' },
  disc: { at: [5, 59.7], deg: 0, depth: 15, hand: 'near' },
  bow: { at: [33, 63.2], deg: 0, depth: 14.6, hand: 'far' },
};
// the weapon's own pivot on its grid: the greatsword's grip centre (the run of rows exactly cols 6-9), the
// disc's centre, the bow's grip row at the stave's middle
function weaponPivot(kind, grid) {
  if (kind === 'disc') return [DC, DC];
  const rows = new Map(); for (const k of grid.keys()) { const x = KX(k), y = KY(k), r = rows.get(y) || [1e9, -1e9]; r[0] = Math.min(r[0], x); r[1] = Math.max(r[1], x); rows.set(y, r); }
  if (kind === 'bow') { const r = rows.get(BOW_CY); return [(r[0] + r[1] + 1) / 2, BOW_CY + 0.5]; }
  let best = [0, 0], run = 0;
  for (let y = 0; y < 90; y++) { const r = rows.get(y); if (r && r[0] === 6 && r[1] === 9) { run++; if (run > best[1]) best = [y - run + 1, run]; } else run = 0; }
  return [8, best[0] + best[1] / 2];
}
// a weapon's layers at gear density (one weapon texel = 2 gear texels, its 1-texel outline 2), turned to
// `deg` about its pivot and placed with the pivot at `at`
function heldWeapon(kind, setId, hold) {
  const src = ({ greatsword: SWORDS, disc: DISCS, bow: BOWS })[kind];
  const ls = (src[setId] || src.hoplite).draw();
  const flat = new Map(); for (const l of [...ls].sort((a, b) => a.depth - b.depth)) for (const [k, c] of l.c) flat.set(k, c);
  const [px, py] = weaponPivot(kind, flat), a = hold.deg * Math.PI / 180, c = Math.cos(a), s = Math.sin(a);
  return ls.map(l => {
    const out = new Layer(hold.depth + l.depth / 100); out.outR = 2; out.outline = l.outline; out.joint = 'weapon';
    let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
    for (const k of l.c.keys()) { const wx = (KX(k) - px) * 2, wy = (KY(k) - py) * 2;
      for (const [cx, cy] of [[wx, wy], [wx + 2, wy], [wx, wy + 2], [wx + 2, wy + 2]]) { const X = cx * c - cy * s, Y = cx * s + cy * c; x0 = Math.min(x0, X); x1 = Math.max(x1, X); y0 = Math.min(y0, Y); y1 = Math.max(y1, Y); } }
    for (let y = Math.floor(y0); y <= Math.ceil(y1); y++) for (let x = Math.floor(x0); x <= Math.ceil(x1); x++) {
      const u = (x + 0.5) * c + (y + 0.5) * s, v = -(x + 0.5) * s + (y + 0.5) * c;   // back onto the weapon's grid
      const col = l.get(Math.floor(u / 2 + px), Math.floor(v / 2 + py)); if (col) out.set(Math.round(x + hold.at[0] - 0.5), Math.round(y + hold.at[1] - 0.5), col);
    }
    return out;
  });
}
// THE ARM POSED (the user's call, 2026-11-08: "be sure the shoulder rotates appropriately"). The arm splits at
// the elbow as PaintElbowSplit cuts the sprites: the upper half turns about the shoulder, the forearm about
// the elbow and follows it. Unlike a pauldron (which stays on the torso and caps the arm), an arm piece's
// shoulder IS the arm's. Its centre stays on the arm's centre line (it rides the arm's axis about the joint),
// and it turns about that centre by SHOULDER_SHARE of the upper arm's angle (0.8: the user asked for a wider
// arc than the first 0.5); whatever stands on it (horns, crystals, the branch, the candles) turns with it. Straps stay on the torso;
// particles rise from wherever the shoulder turned them (st.sp). Once the fist is above the shoulder line the
// forearm draws over the shoulder (the rig's ForearmOverShoulders).
// P: { upper, fore } in on-screen degrees, fore absolute (fore = upper for a straight arm)
const JOINT = { shoulder: [5, 29.7], elbow: [5, 49.7], cap: [4.5, 34] };   // cap: the shoulder piece's centre, on the arm's axis
let SHOULDER_SHARE = 0.8;
const rotP = (x, y, cx, cy, d) => { const a = d * Math.PI / 180, dx = x - cx, dy = y - cy; return [cx + dx * Math.cos(a) - dy * Math.sin(a), cy + dx * Math.sin(a) + dy * Math.cos(a)]; };
// where a point on the shoulder piece goes: turned about the cap's centre, the centre carried round the joint with the arm
function capMove(up) { const [cx, cy] = JOINT.cap, [nx, ny] = rotP(cx, cy, JOINT.shoulder[0], JOINT.shoulder[1], up); return { cx, cy, d: up * SHOULDER_SHARE, mx: nx - cx, my: ny - cy }; }
function shoulderPoint(P) { const c = capMove(P.upper || 0); return (x, y) => { const [a, b] = rotP(x, y, c.cx, c.cy, c.d); return [a + c.mx, b + c.my]; }; }
function poseArm(layers, glows, P) {
  const up = P.upper || 0, fo = P.fore ?? up, cap = capMove(up), capPt = shoulderPoint(P);
  if (!up && !fo) return [layers, glows];
  const [sx, sy] = JOINT.shoulder, [ex0, ey0] = JOINT.elbow, [ex, ey] = rotP(ex0, ey0, sx, sy, up), tx = ex - ex0, ty = ey - ey0;
  const foreOver = rotP(5, 65, ex0, ey0, fo)[1] + ty < sy + 4;
  const moved = (l, cx, cy, d, mx, my, depth) => { const t = turnAbout(l, cx, cy, d), o = new Layer(depth); Object.assign(o, { joint: l.joint, outline: l.outline, far: l.far, outR: l.outR, rise: l.rise, alpha: l.alpha });
    for (const [k, c] of t.c) o.c.set(K(KX(k) + Math.round(mx), KY(k) + Math.round(my)), c); return o; };
  const out = [];
  for (const l of layers) {
    if (l.joint === 'shoulder') { out.push(moved(l, cap.cx, cap.cy, cap.d, cap.mx, cap.my, l.depth)); continue; }
    if (l.joint !== 'arm') { out.push(l); continue; }
    const u = new Layer(l.depth), lo = new Layer(l.depth);
    for (const [k, c] of l.c) (KY(k) < ey0 ? u : lo).c.set(k, c);
    for (const h of [u, lo]) Object.assign(h, { joint: l.joint, outline: l.outline, far: l.far, outR: l.outR, alpha: l.alpha });
    out.push(moved(u, sx, sy, up, 0, 0, l.depth));
    const f = moved(lo, ex0, ey0, fo, tx, ty, l.depth + (foreOver ? 3 : 0)); f.over = foreOver; out.push(f);
  }
  const gs = glows.map(g => {
    let x = g.x, y = g.y;
    if (g.joint === 'shoulder') [x, y] = capPt(x, y);
    else if (g.joint === 'arm') { if (y < ey0) [x, y] = rotP(x, y, sx, sy, up); else { [x, y] = rotP(x, y, ex0, ey0, fo); x += tx; y += ty; } }
    return Object.assign({}, g, { x, y });
  });
  return [out, gs];
}
function turnAbout(l, cx, cy, deg) {
  const a = deg * Math.PI / 180, c = Math.cos(a), s = Math.sin(a), out = new Layer(l.depth);
  if (!l.c.size) return out;
  const m = l.mask().bbox(), R = Math.hypot(Math.max(Math.abs(m.x0 - cx), Math.abs(m.x1 + 1 - cx)), Math.max(Math.abs(m.y0 - cy), Math.abs(m.y1 + 1 - cy))) + 1;
  for (let y = Math.floor(cy - R); y <= Math.ceil(cy + R); y++) for (let x = Math.floor(cx - R); x <= Math.ceil(cx + R); x++) {
    const dx = x + 0.5 - cx, dy = y + 0.5 - cy, col = l.get(Math.floor(cx + dx * c + dy * s), Math.floor(cy - dx * s + dy * c)); if (col) out.set(x, y, col);
  }
  return out;
}

if (typeof module !== 'undefined') module.exports = { ARMS, wearArmFront, wearArmBack, armBackRank, HOLDS, heldWeapon, poseArm, shoulderPoint, withEchoes, composeA };
