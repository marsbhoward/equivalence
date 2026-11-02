// ================================================================ materials
const M = {
  bronze: ramp('#B87333', { sheen: '#ffd98a', lift: 0.36 }),
  bronzeDk: ramp('#7f4c22', { sheen: '#e9a65a' }),
  verdigris: ramp('#4f8f7a', { sheen: '#bff0d8' }),
  leather: ramp('#7a4a2a', { sheen: '#d9a070', lift: 0.28 }),
  leatherDk: ramp('#4a2e1c', { sheen: '#a8774f', lift: 0.28 }),
  red: ramp('#9b2a24', { sheen: '#ff9a72', lift: 0.30 }),
  linen: ramp('#c2ad7e', { sheen: '#fff6dc', lift: 0.30 }),
  olive: ramp('#56602f', { sheen: '#d8e09a', lift: 0.30 }),
  steel: ramp('#a3a9b5', { sheen: '#f4f7ff', lift: 0.40 }),
  steelDk: ramp('#5d626d', { sheen: '#c9d0de' }),
  wool: ramp('#575d66', { sheen: '#c8ccd4', lift: 0.28 }),
  forest: ramp('#2f4f37', { sheen: '#a8d8a8', lift: 0.30 }),
  navy: ramp('#2c3a5c', { sheen: '#8fa8d8' }),
  blue: ramp('#2f5d8f', { sheen: '#a8d0ff' }),
  white: ramp('#d4d0c4', { sheen: '#ffffff', lift: 0.40 }),
  gold: ramp('#dba73a', { sheen: '#fff2b0', lift: 0.40 }),
  goldDk: ramp('#9a6a1c', { sheen: '#f2c86a' }),
  crimson: ramp('#8a1a2a', { sheen: '#ff8a8a' }),
  teal: ramp('#2a6a68', { sheen: '#9fe8d8' }),
  fur: ramp('#6b543c', { sheen: '#e0c8a0', lift: 0.30 }),
};
const L = d => new Layer(d);
const farOf = l => { l.far = true; return l; };
const sides = (fn, nearDepth, farDepth) => [NEAR, FAR].map(S => { const l = fn(S, L(S === NEAR ? nearDepth : farDepth)); if (S === FAR) l.far = true; return l; });
const LEGX = { near: 11, far: 27 };          // shin centres (continuous x)
const legC = S => S === NEAR ? 11 : 27;

// ---------------------------------------------------------------- shared motifs
// a lit-left gradient across the mask's own box, and a bevel
const grad = (r, bias = 0) => (x, y, m) => { const b = m.bbox(); const t = (x + 0.5 - b.x0) / b.w; return tone(r, (t < 0.22 ? 4 : t < 0.7 ? 3 : 2) + bias); };
// mail: rings in a staggered weave, lit left
const mail = (r, bias = 0) => (x, y, m) => {
  const b = m.bbox(); const t = (x + 0.5 - b.x0) / b.w; const f = t < 0.25 ? 1 : t > 0.72 ? -1 : 0;
  const ring = ((x + (y % 2 ? 1 : 0)) % 2) === 0;
  return tone(r, (ring ? 3 : 1) + f + bias);
};
// scales: w x h, rows staggered by half a scale, each with a lit top-left and a dark lower edge
const SCALE4 = [[3, 5, 4, 3], [2, 4, 3, 2], [1, 3, 3, 1], [0, 1, 1, 0]];
const SCALE5 = [[3, 5, 4, 3], [2, 4, 4, 2], [2, 3, 3, 2], [1, 3, 2, 1], [0, 1, 1, 0]];
const scales = (r, w = 4, h = 4, tint = null, bias = 0) => (x, y, m) => {
  const tile = h === 5 ? SCALE5 : SCALE4;
  const row = Math.floor(y / h), ry = ((y % h) + h) % h, off = (row % 2) * 2, rx = (((x + off) % 4) + 4) % 4;
  const b = m.bbox(); const t = (x + 0.5 - b.x0) / b.w; const f = (t < 0.25 ? 1 : t > 0.72 ? -1 : 0) + bias;
  const v = tile[ry][rx];
  const pat = tint && ry >= h - 2 && v > 0 && ((Math.floor((x + off) / 4) * 5 + row * 3) % 7 === 0);
  return tone(pat ? tint : r, v === 0 ? 0 : v + f);
};
// a horizontal seam: a dark row with a lit row under it (a lame's lower edge over the next)
function lame(l, y, x0 = -40, x1 = 80, r) { l.over(rect(x0, y, x1, y), tone(r, 1)); l.over(rect(x0, y + 1, x1, y + 1), tone(r, 4)); }
function rivet(l, x, y, r) { l.dot(x, y, tone(r, 5)); l.dot(x + 1, y + 1, tone(r, 1)); }
// a quilted face: seams every `gap`, stitch dots between
const quilt = (r, gap = 4, bias = 0) => (x, y, m) => {
  const b = m.bbox(); const t = (x + 0.5 - b.x0) / b.w; const f = (t < 0.22 ? 1 : t > 0.7 ? -1 : 0) + bias;
  if ((x - b.x0) % gap === 0) return tone(r, 1 + f);
  if ((x - b.x0) % gap === 1) return tone(r, 4 + f);
  return tone(r, ((y % 6) === 0 ? 2 : 3) + f);
};
// a cuirass-shaped field over the torso
const chestPoly = (top = 29, bot = 61) => poly([[8, top], [31, top], [32, 40], [31, bot], [8, bot], [7, 40]]);
// rounded-bottom box for a glove over the fist
function fist(S, y0, y1, x0 = -1, x1 = 10) { const m = sideRect(S, x0, y0, x1, y1); m.del(sx(S, x0), y1); m.del(sx(S, x1), y1); return m; }

// ================================================================ BRONZE I - HOPLITE
const hoplite = {
  id: 'hoplite', tier: 'Bronze', name: 'Hoplite', blurb: 'A bronze muscle cuirass under a crested helm: round shoulder discs, leather strips at the hip, greaves over sandals, a short red cape.',
  Head() {
    const back = L(DEPTH.helmBack), helm = L(DEPTH.helm);
    const crest = ell(17.5, -1, 19, 12).clip(rect(-30, -30, 60, -1));
    back.fill(crest, dome(M.red, 17.5, -1, 19, 12));
    for (let x = -1; x <= 36; x += 3) back.line(17.5, 1, x, -10, tone(M.red, 1));
    const domeM = ell(17.5, 11, 18.5, 14.5).clip(rect(-30, -30, 60, 10));
    const cheekN = poly([[-2, 9], [7, 9], [7, 19], [5, 25], [1, 28], [-2, 23]]);
    const cheekF = poly([[38, 9], [34, 9], [34, 19], [35, 24], [38, 26]]);
    helm.fill(U(domeM, cheekN, cheekF), dome(M.bronze, 17.5, 8, 20, 18));
    helm.over(cheekN, grad(M.bronze)); helm.over(cheekF, grad(M.bronze, -1));
    helm.over(rect(-2, 8, 38, 10), (x, y) => tone(M.bronze, y === 8 ? 5 : y === 9 ? 4 : 2));
    helm.fill(rect(15, -4, 20, -2), (x) => tone(M.bronzeDk, x < 17 ? 4 : 3));
    return [back, helm];
  },
  Shoulders() {
    return sides((S, l) => {
      const cx = scx(S, 4.5), m = ell(cx, 35, 7.5, 6);
      l.fill(m, dome(M.bronze, cx - 1, 33, 8, 7));
      for (const [x, y] of m.pts()) { const dx = (x + .5 - cx) / 7.5, dy = (y + .5 - 35) / 6; if (dx * dx + dy * dy > 0.6) l.set(x, y, tone(M.bronzeDk, dy < 0 ? 4 : 2)); }
      rivet(l, sx(S, 4), 34, M.bronze);
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const l = L(DEPTH.torso);
    l.fill(poly([[8, 30], [31, 30], [32, 40], [30, 52], [30, 60], [20, 63], [9, 60], [9, 52], [7, 40]]), grad(M.bronze));
    for (const cx of [14.5, 25.5]) l.over(ell(cx, 39, 6.5, 5.5), dome(M.bronze, cx, 38, 7, 6.5, cx > 20 ? -0.12 : 0));
    l.line(10, 44, 14, 45, tone(M.bronze, 1)); l.line(15, 45, 19, 43, tone(M.bronze, 1));
    l.line(21, 43, 25, 45, tone(M.bronze, 1)); l.line(26, 45, 30, 44, tone(M.bronze, 1));
    l.line(20, 33, 20, 57, tone(M.bronze, 1));
    for (const y of [49, 54]) { l.line(14, y, 19, y + 1, tone(M.bronze, 2)); l.line(21, y + 1, 26, y, tone(M.bronze, 2)); }
    l.fill(rect(10, 30, 29, 31), (x, y) => y === 30 ? tone(M.bronze, 5) : tone(M.bronzeDk, 3));
    return [l];
  },
  Gloves() {
    return sides((S, l) => {
      l.fill(sideRect(S, -1, 46, 10, 59), cyl(M.leather, scx(S, 4.5), 6));
      for (const y of [47, 52, 57]) l.over(rect(-10, y, 50, y + 1), (x, yy) => tone(M.bronze, yy === y ? 4 : 2));
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 58, 31, 63), cyl(M.leatherDk, 18, 16));
    for (let x = 10; x <= 30; x += 4) rivet(l, x, 60, M.bronze);
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    for (let i = 0; i < 5; i++) {
      const x0 = 7 + i * 5, edge = i === 0 || i === 4, bot = edge ? 82 : 86;
      l.fill(rect(x0, 62, x0 + 3, bot), (x, y) => y >= bot - 2 ? tone(M.bronze, x === x0 ? 5 : x === x0 + 3 ? 2 : 3) : tone(M.leather, x === x0 ? 4 : x === x0 + 3 ? 2 : 3));
    }
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S);
      const knee = ell(cx, 92, 5, 3.5);
      const m = U(knee, poly([[cx - 4, 92], [cx + 4, 92], [cx + 4.5, 99], [cx + 3.5, 110], [cx - 3.5, 110], [cx - 4.5, 99]]));
      l.fill(m, cyl(M.bronze, cx, 5));
      l.over(knee, dome(M.bronze, cx - 1, 91, 5, 4));
      l.line(cx - 0.5, 96, cx - 0.5, 108, tone(M.bronze, 2));
      l.fill(rect(cx - 4, 112, cx + 3, 120), (x, y) => (y - 112) % 3 === 0 ? tone(M.leather, 4) : tone(M.leatherDk, 2));
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    const l = L(DEPTH.back);
    l.fill(poly([[0, 30], [37, 30], [43, 70], [45, 98], [38, 96], [30, 100], [20, 97], [10, 100], [0, 96], [-7, 98], [-5, 70]]), (x) => { const t = (x + 7) / 52; return tone(M.red, t < 0.18 ? 3 : t > 0.8 ? 1 : 2); });
    for (const x of [-2, 6, 32, 40]) l.line(x, 50, x + (x < 19 ? -1 : 1), 94, tone(M.red, 1));
    return [l];
  },
};

// ================================================================ BRONZE II - WAYFARER
const wayfarer = {
  id: 'wayfarer', tier: 'Bronze', name: 'Wayfarer', blurb: 'Quilted linen and worn leather: a padded cap with ear flaps, stepped leather spaulders, a knotted sash, wrapped shins and a bedroll across the shoulders.',
  Head() {
    const helm = L(DEPTH.helm);
    const cap = ell(18, 10, 18, 12.5).clip(rect(-30, -30, 60, 9));
    const flapN = poly([[-1, 6], [7, 6], [7, 21], [4, 25], [-1, 23]]);
    // the far flap sits out past the far eye: the head is turned, so that side is narrower
    const flapF = poly([[35, 6], [39, 6], [39, 23], [35, 21]]);
    helm.fill(U(cap, flapN, flapF), dome(M.leather, 16, 6, 22, 18));
    for (const x of [6, 12, 20.5, 27, 32]) helm.line(x + (x - 20.5) * 0.1, -2, x + (x - 20.5) * 0.18, 6, tone(M.leather, 1));
    helm.over(rect(-2, 7, 39, 9), (x, y) => tone(M.bronze, y === 7 ? 4 : 2));
    for (let x = 2; x <= 37; x += 5) helm.dot(x, 8, tone(M.bronze, 5));
    helm.dot(2, 23, tone(M.linen, 4)); helm.dot(3, 24, tone(M.linen, 3));
    return [helm];
  },
  Shoulders() {
    return sides((S, l) => {
      const cx = scx(S, 4.5);
      // the near spaulder steps OUT down the arm; the far one, seen past the chest, stacks
      // straight down and a little narrower - stepped outward too it read as turned away
      const step = S === NEAR ? -0.5 : 0, wide = S === NEAR ? 1 : 0.9;
      for (let i = 2; i >= 0; i--) {
        const m = ell(cx + step * i, 32 + i * 5, (7.5 + i * 0.4) * wide, 4);
        l.fill(m, dome(M.leather, cx - 2, 30 + i * 5, 9, 6));
        l.over(band(m, 1).clip(rect(-40, 32 + i * 5 + 1, 80, 60)), tone(M.leatherDk, 2));
        rivet(l, Math.round(cx) - 1, 31 + i * 5, M.bronze);
      }
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const l = L(DEPTH.torso);
    l.fill(chestPoly(30, 61), quilt(M.linen, 4));
    l.fill(rect(10, 27, 29, 31), (x, y) => tone(M.linen, y === 27 ? 5 : y === 28 ? 4 : y === 31 ? 2 : 3));
    for (let y = 33; y <= 57; y += 3) { l.dot(19, y, tone(M.leatherDk, 3)); l.dot(20, y, tone(M.leatherDk, 3)); l.dot(19, y + 1, tone(M.linen, 1)); l.dot(20, y + 1, tone(M.linen, 1)); }
    return [l];
  },
  Gloves() {
    return sides((S, l) => {
      l.fill(sideRect(S, -1, 46, 10, 58), (x, y, m) => ((x + y) % 4 === 0 ? tone(M.linen, 2) : cyl(M.linen, scx(S, 4.5), 6)(x)));
      l.fill(fist(S, 58, 69), (x, y) => y <= 59 ? tone(M.leatherDk, y === 58 ? 4 : 2) : cyl(M.leather, scx(S, 4.5), 6)(x));
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 57, 31, 62), (x, y) => tone(M.olive, y === 57 ? 4 : y === 62 ? 2 : 3));
    l.fill(ell(10, 59.5, 3.5, 3.5), dome(M.olive, 9, 58, 4, 4));
    l.fill(poly([[8, 62], [11, 62], [10, 76], [7, 75]]), grad(M.olive));
    l.fill(poly([[11, 62], [14, 62], [15, 73], [12, 74]]), grad(M.olive, -1));
    l.fill(rect(26, 61, 32, 69), (x, y, m) => bevel(M.leather, 1, 3)(x, y, m));
    l.fill(rect(26, 61, 32, 63), tone(M.leatherDk, 3));
    rivet(l, 29, 62, M.bronze);
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    l.fill(poly([[7, 61], [19, 61], [19, 82], [6, 80]]), quilt(M.linen, 4));
    l.fill(poly([[21, 61], [32, 61], [33, 80], [21, 82]]), quilt(M.linen, 4, -1));
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S);
      l.fill(rect(cx - 4, 86, cx + 3, 110), (x, y) => ((y - x * 0.5) % 3 < 1 ? tone(M.linen, 1) : cyl(M.linen, cx, 4.5)(x)));
      l.fill(rect(cx - 5, 110, cx + 4, 120), (x, y, m) => bevel(M.leather, 1, 3)(x, y, m));
      l.fill(rect(cx - 5, 110, cx + 4, 111), tone(M.leatherDk, 3));
      rivet(l, Math.round(cx), 110, M.bronze);
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    const l = L(DEPTH.back);
    const roll = rect(-12, 21, 49, 31);
    l.fill(roll, (x, y) => tone(M.olive, y === 21 ? 5 : y <= 23 ? 4 : y >= 30 ? 1 : y >= 28 ? 2 : 3));
    for (const x of [-12, 49]) { l.fill(ell(x, 26, 2.5, 5.5), (xx, y) => tone(M.linen, y < 24 ? 4 : 3)); l.fill(ell(x, 26, 1.2, 2.5), tone(M.olive, 1)); }
    for (const x of [-6, 42]) l.over(rect(x, 21, x + 1, 31), (xx) => tone(M.leatherDk, xx === x ? 4 : 2));
    l.fill(ell(36, 72, 4, 5), dome(M.leather, 35, 70, 5, 6));
    l.fill(rect(35, 64, 37, 67), tone(M.leatherDk, 3));
    return [l];
  },
};

// ================================================================ BRONZE III - MALACHITE SCALE
// ================================================================ SILVER I - HAUBERK
const hauberk = {
  id: 'hauberk', tier: 'Silver', name: 'Hauberk', blurb: 'Steel mail from the coif down, a skullcap riveted over it, a blue surcoat split to the knee, steel knee caps and a long grey cloak on a chain.',
  Head() {
    const helm = L(DEPTH.helm);
    const coif = ell(17.5, 13, 20, 18).clip(rect(-30, -30, 60, 34)).minus(poly([[7, 10], [34, 10], [34, 28], [7, 28]]));
    helm.fill(coif, mail(M.steel));
    helm.fill(ell(17.5, 7, 17.5, 11).clip(rect(-30, -30, 60, 5)), dome(M.steel, 14, 1, 18, 12));
    helm.over(rect(-30, 4, 60, 5), (x, y) => tone(M.steelDk, y === 4 ? 4 : 2));
    for (let x = 3; x <= 33; x += 6) rivet(helm, x, 4, M.steel);
    return [helm];
  },
  Shoulders() {
    return sides((S, l) => {
      const cx = scx(S, 4.5);
      const low = ell(cx, 39, 7, 3.5), cap = ell(cx, 33, 7.5, 5);
      l.fill(low, dome(M.steel, cx - 1, 38, 8, 5));
      l.fill(cap, dome(M.steel, cx - 1, 32, 8, 6));
      l.over(band(cap, 1).clip(rect(-40, 34, 80, 60)), tone(M.steel, 1));
      rivet(l, Math.round(cx) - 1, 32, M.steel);
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const t = L(DEPTH.torso);
    t.fill(chestPoly(30, 61), mail(M.steel));
    t.fill(poly([[11, 31], [28, 31], [29, 61], [10, 61]]), (x, y, m) => bevel(M.blue, 1, 3)(x, y, m));
    t.over(rect(10, 31, 29, 31), tone(M.white, 4));
    const sleeves = sides((S, l) => l.fill(sideRect(S, -1, 31, 10, 50), mail(M.steel)), DEPTH.nearSleeve, DEPTH.farSleeve);
    return [t, ...sleeves];
  },
  Gloves() {
    return sides((S, l) => {
      l.fill(fist(S, 50, 69), mail(M.steel));
      l.over(sideRect(S, -1, 53, 10, 54), (x, y) => tone(M.leather, y === 53 ? 4 : 2));
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 57, 31, 62), (x, y) => tone(M.leatherDk, y === 57 ? 4 : y === 62 ? 2 : 3));
    const buckle = rect(16, 56, 21, 63); l.fill(buckle, (x, y, m) => bevel(M.steel, 1, 3)(x, y, m));
    l.fill(rect(18, 58, 19, 61), tone(M.leatherDk, 2));
    l.fill(rect(22, 62, 24, 70), grad(M.leatherDk));
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    l.fill(poly([[7, 61], [32, 61], [33, 80], [6, 80]]), mail(M.steel));
    l.fill(poly([[10, 61], [18, 61], [18, 92], [9, 90]]), (x, y, m) => bevel(M.blue, 1, 3)(x, y, m));
    l.fill(poly([[21, 61], [29, 61], [30, 90], [21, 92]]), (x, y, m) => bevel(M.blue, 1, 2)(x, y, m));
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S);
      l.fill(rect(cx - 4, 92, cx + 3, 112), mail(M.steel));
      l.fill(ell(cx - 0.5, 93, 5, 4), dome(M.steel, cx - 1.5, 92, 5, 4.5));
      l.fill(rect(cx - 5, 112, cx + 4, 120), (x, y, m) => bevel(M.leatherDk, 1, 3)(x, y, m));
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    const l = L(DEPTH.back), over = L(DEPTH.backOver);
    l.fill(poly([[1, 30], [36, 30], [42, 80], [44, 110], [-7, 110], [-5, 80]]), (x) => tone(M.wool, x < 0 ? 3 : x > 38 ? 1 : 2));
    for (const x of [-3, 41]) l.line(x, 60, x + (x < 19 ? -1 : 1), 108, tone(M.wool, 1));
    over.fill(ell(10, 33, 2.5, 2.5), dome(M.steel, 9, 32, 3, 3));
    over.fill(ell(29, 33, 2.5, 2.5), dome(M.steel, 28, 32, 3, 3));
    for (let x = 12; x <= 27; x++) over.set(x, 33 + Math.round(Math.sin((x - 12) / 15 * Math.PI) * 3), tone(M.steel, x % 2 ? 4 : 2));
    return [l, over];
  },
};

// ================================================================ SILVER II - KETTLE
const kettle = {
  id: 'kettle', tier: 'Silver', name: 'Kettle', blurb: 'A broad-brimmed kettle hat, a green brigandine studded with steel rivets over a mail collar, stepped spaulders, half gauntlets, a dagger on the hip and a dagged capelet.',
  Head() {
    const helm = L(DEPTH.helm);
    const d = ell(17.5, 4, 15, 11).clip(rect(-30, -30, 60, 5));
    helm.fill(d, dome(M.steel, 14, -1, 16, 12));
    helm.line(17, -6, 17, 4, tone(M.steel, 5)); helm.line(18, -6, 18, 4, tone(M.steel, 2));
    const brim = poly([[-10, 5], [46, 5], [47, 9], [43, 10], [-7, 10], [-11, 9]]);
    helm.fill(brim, (x, y) => tone(M.steel, y <= 5 ? 5 : y <= 6 ? 4 : y >= 9 ? 1 : 3));
    helm.fill(rect(4, 10, 5, 26), (x) => tone(M.leatherDk, x === 4 ? 3 : 2));
    return [helm];
  },
  Shoulders() {
    return sides((S, l) => {
      const cx = scx(S, 4.5);
      for (let i = 2; i >= 0; i--) {
        const m = ell(cx + (S === NEAR ? -0.6 : 0.6) * i, 31 + i * 4.5, 8 - i * 0.3, 4);
        l.fill(m, dome(M.steel, cx - 2, 29 + i * 4.5, 9, 6));
        l.over(band(m, 1).clip(rect(-40, 32 + i * 4.5, 80, 60)), tone(M.steel, 1));
        rivet(l, Math.round(cx) - 2, 30 + i * 4.5, M.steel);
      }
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const l = L(DEPTH.torso);
    const m = chestPoly(30, 61);
    l.fill(m, (x, y) => ((x - 8) % 6 === 0 ? tone(M.forest, 1) : grad(M.forest)(x, y, m)));
    for (let y = 33; y <= 58; y += 3) for (let x = 10 + ((y / 3) % 2 ? 0 : 1); x <= 30; x += 3) if ((x - 8) % 6 !== 0) rivet(l, x, y, M.steel);
    l.fill(rect(9, 26, 30, 31), mail(M.steel));
    return [l];
  },
  Gloves() {
    return sides((S, l) => {
      const cuff = sidePoly(S, [[-2, 52], [11, 52], [10, 58], [0, 58]]);
      l.fill(cuff, (x, y, m) => bevel(M.steel, 1, 2)(x, y, m));
      lame(l, 55, -40, 80, M.steel);
      l.fill(fist(S, 59, 68, 0, 9), cyl(M.steel, scx(S, 4.5), 5.5));
      for (const y of [61, 64]) lame(l, y, -40, 80, M.steel);
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 57, 31, 61), (x, y) => tone(M.leatherDk, y === 57 ? 4 : y === 61 ? 2 : 3));
    l.fill(rect(17, 56, 21, 62), (x, y, m) => bevel(M.steel, 1, 3)(x, y, m));
    l.fill(rect(29, 54, 30, 62), (x) => tone(M.leatherDk, x === 29 ? 4 : 2));
    l.fill(ell(29.5, 63, 3, 1.6), dome(M.steel, 29, 62.5, 3, 2));
    l.fill(rect(29, 64, 30, 78), (x) => tone(M.leather, x === 29 ? 4 : 2));
    l.dot(29, 78, tone(M.steel, 4)); l.dot(30, 78, tone(M.steel, 2));
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    const skirt = U(poly([[7, 61], [19, 61], [19, 71], [6, 70]]), poly([[21, 61], [33, 61], [33, 70], [21, 71]]));
    l.fill(skirt, grad(M.forest));
    for (const y of [64, 67]) for (let x = 9 + (y === 67 ? 1 : 0); x <= 31; x += 3) if (l.has(x, y)) rivet(l, x, y, M.steel);
    for (const S of [NEAR, FAR]) {
      const cx = legC(S);
      // padded hose: quilted green over the thigh, a small steel cop at the knee
      l.fill(rect(cx - 4, 72, cx + 3, 92), quilt(M.forest, 3, S === NEAR ? 0 : -1));
      const cop = ell(cx - 0.5, 93, 4.5, 3.5);
      l.fill(cop, dome(M.steel, cx - 1.5, 92, 5, 4));
      l.fill(sideEll(S, 6, 93, 2, 3), dome(M.steel, scx(S, 5.5), 92, 2.5, 3.5));
    }
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S);
      // tall riding boots: a turned-down cuff in the lighter leather, two buckled straps
      l.fill(rect(cx - 4, 98, cx + 3, 118), cyl(M.leather, cx, 5));
      l.fill(rect(cx - 5, 117, cx + 4, 120), (x, y) => tone(M.leatherDk, y === 117 ? 3 : 2));
      const cuff = new Mask();
      for (let x = cx - 5; x <= cx + 4; x++) { const bot = 101 + ((x - cx + 10) % 3 === 0 ? 1 : 0); for (let y = 97; y <= bot; y++) cuff.add(x, y); }
      l.fill(cuff, (x, y) => tone(M.linen, y === 97 ? 4 : y >= 101 ? 2 : 3));
      for (const y of [106, 112]) {
        l.over(rect(cx - 4, y, cx + 3, y + 1), (x, yy) => tone(M.leatherDk, yy === y ? 3 : 1));
        l.dot(cx + 1, y, tone(M.steel, 5)); l.dot(cx + 2, y + 1, tone(M.steel, 2));
      }
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    const l = L(DEPTH.back);
    const m = new Mask();
    for (let x = -5; x <= 42; x++) { const bot = 70 + Math.round(Math.abs(Math.sin((x + 5) * Math.PI / 6)) * 4); for (let y = 29; y <= bot; y++) m.add(x, y); }
    l.fill(m, (x) => tone(M.forest, x < 0 ? 3 : x > 38 ? 1 : 2));
    return [l];
  },
};

// ================================================================ SILVER III - SENTINEL
const sentinel = {
  id: 'sentinel', tier: 'Silver', name: 'Sentinel', blurb: 'Half plate: a sallet whose tail flares past the shoulders and a bevor to the chin, big lamed pauldrons, a ridged breastplate with a plackart, tassets, fan-winged knees and a long navy cape.',
  Head() {
    const helm = L(DEPTH.helm);
    const sallet = ell(17.5, 8, 18.5, 13).clip(rect(-30, -30, 60, 10));
    const tailN = poly([[-2, 3], [4, 3], [3, 12], [-2, 19], [-7, 21], [-5, 12]]);
    const tailF = poly([[34, 3], [39, 3], [42, 12], [43, 21], [38, 19], [35, 12]]);
    helm.fill(U(tailN, tailF), (x, y, m) => tone(M.steel, x < 0 ? 4 : x > 36 ? 2 : 3));
    helm.fill(sallet, dome(M.steel, 14, 3, 19, 14));
    helm.line(17, -5, 17, 9, tone(M.steel, 5)); helm.line(18, -5, 18, 9, tone(M.steel, 1));
    helm.over(rect(-30, 9, 60, 10), (x, y) => tone(M.steel, y === 9 ? 4 : 1));
    const bevor = poly([[4, 26], [33, 26], [34, 30], [28, 35], [10, 35], [3, 30]]);
    helm.fill(bevor, dome(M.steel, 14, 26, 20, 10));
    lame(helm, 30, 3, 34, M.steel);
    return [helm];
  },
  Shoulders() {
    return sides((S, l) => {
      const cx = scx(S, 4.5);
      for (let i = 3; i >= 1; i--) {
        const m = ell(cx + (S === NEAR ? -1 : 1) * 0.6 * i, 39 + i * 3.5, 7.5 - i * 0.4, 3);
        l.fill(m, dome(M.steel, cx - 2, 38 + i * 3.5, 8, 5));
        l.over(band(m, 1).clip(rect(-40, 40 + i * 3.5, 80, 60)), tone(M.steel, 1));
      }
      const main = sidePoly(S, [[-6, 31], [6, 28], [11, 31], [11, 39], [4, 42], [-4, 41], [-7, 36]]);
      l.fill(main, dome(M.steel, scx(S, 1), 31, 10, 10));
      l.over(band(main, 1).clip(rect(-40, 38, 80, 60)), tone(M.steel, 1));
      l.fill(sidePoly(S, [[5, 24], [9, 25], [11, 31], [7, 30]]), (x, y, m) => bevel(M.steel, 1, 3)(x, y, m));
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const l = L(DEPTH.torso);
    const bp = poly([[8, 29], [31, 29], [32, 38], [31, 58], [20, 61], [8, 58], [7, 38]]);
    l.fill(bp, dome(M.steel, 17, 38, 16, 22));
    l.line(19, 31, 19, 47, tone(M.steel, 5)); l.line(20, 31, 20, 47, tone(M.steel, 2));
    const pl = poly([[9, 51], [20, 45], [30, 51], [31, 58], [20, 61], [8, 58]]);
    l.over(pl, dome(M.steel, 17, 50, 16, 14, -0.05));
    l.over(band(pl, 1).clip(rect(-40, 40, 80, 53)), tone(M.steel, 1));
    l.fill(rect(10, 29, 29, 30), (x, y) => tone(M.steel, y === 29 ? 5 : 3));
    return [l];
  },
  Gloves() {
    return sides((S, l) => {
      l.fill(sidePoly(S, [[-3, 47], [12, 47], [10, 56], [0, 56]]), (x, y, m) => bevel(M.steel, 1, 3)(x, y, m));
      l.fill(fist(S, 57, 68, 0, 9), cyl(M.steel, scx(S, 4.5), 5.5));
      for (const y of [59, 62, 65]) lame(l, y, -40, 80, M.steel);
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 58, 31, 61), (x, y) => tone(M.leatherDk, y === 58 ? 4 : 2));
    for (let x = 10; x <= 30; x += 5) l.fill(rect(x, 58, x + 1, 61), (xx) => tone(M.steel, xx === x ? 5 : 2));
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    l.fill(poly([[7, 61], [32, 61], [33, 68], [6, 68]]), grad(M.steel));
    lame(l, 64, -40, 80, M.steel);
    const tN = poly([[5, 67], [17, 67], [17, 80], [11, 83], [5, 80]]), tF = poly([[22, 67], [34, 67], [34, 80], [28, 83], [22, 80]]);
    l.fill(tN, dome(M.steel, 9, 70, 9, 14)); l.fill(tF, dome(M.steel, 26, 70, 9, 14, -0.1));
    for (const y of [71, 75, 79]) lame(l, y, -40, 80, M.steel);
    for (const S of [NEAR, FAR]) {
      const cx = legC(S);
      l.fill(rect(cx - 4, 84, cx + 3, 90), cyl(M.steel, cx, 5));
      l.fill(sideEll(S, 6, 92, 3, 4), dome(M.steel, scx(S, 5), 91, 3.5, 4.5));
      l.fill(ell(cx - 0.5, 92, 4.5, 3.5), dome(M.steel, cx - 1.5, 91, 5, 4));
    }
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S);
      l.fill(rect(cx - 4, 96, cx + 3, 112), cyl(M.steel, cx, 5));
      l.fill(rect(cx - 5, 112, cx + 4, 120), cyl(M.steel, cx, 6));
      for (const y of [114, 117]) lame(l, y, -40, 80, M.steel);
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    const l = L(DEPTH.back);
    l.fill(poly([[0, 29], [37, 29], [43, 70], [46, 116], [-9, 116], [-6, 70]]), (x) => tone(M.navy, x < -2 ? 4 : x > 40 ? 2 : 3));
    for (const x of [-5, 43]) l.line(x, 70, x + (x < 19 ? -2 : 2), 114, tone(M.navy, 1));
    return [l];
  },
};

// ================================================================ GOLD I - SOL
const sol = {
  id: 'sol', tier: 'Gold', name: 'Sol', blurb: 'Gilded plate and white cloth: a Corinthian helm in gold with the eyes showing through, a pointed nasal and cheek guards, crested with gold feathers on a stem; tiered gold-and-white pauldrons, a gold disc at the sash, a white tabard edged in gold, and a white cape.',
  Head() {
    const back = L(DEPTH.helmBack), helm = L(DEPTH.helm), eyes = L(DEPTH.helm + 0.05);
    eyes.outline = false;
    const fx = 20.5;   // the face's centre on the turned head
    for (const deg of [-24, -12, 0, 12, 24]) {
      const a = deg * Math.PI / 180, len = 11 - Math.abs(deg) * 0.1, bx = 19.5, by = -8;
      feather(back, bx + Math.sin(a) * len, by - Math.cos(a) * len, 2.4, len, deg, M.gold);
    }
    back.fill(rect(18, -9, 21, -1), (x) => tone(M.goldDk, x < 19 ? 4 : 2));
    // a size down from round 3, and turned with the head: the near side wider than the far
    const shell = U(ell(18.5, 10, 18, 13.5).clip(rect(-30, -30, 60, 12)),
      poly([[-2, 8], [37, 8], [37.5, 19], [35, 27], [26, 34], [23, 24], [18, 24], [15, 34], [0, 27], [-2.5, 19]]));
    // rectangular slits, as in the reference (a cross-cut through the far one) - the wearer's
    // eyes show through them
    const holes = U(rect(8, 15, 18, 20), rect(24, 15, 32, 20), rect(29, 12, 29, 23));
    shell.minus(holes);
    helm.fill(shell, dome(M.gold, 15, 8, 20, 20));
    helm.over(band(shell, 1).clip(rect(-40, 22, 80, 40)), tone(M.gold, 4));
    helm.line(-1, 9, fx - 0.5, 2, tone(M.gold, 5)); helm.line(-1, 10, fx - 0.5, 3, tone(M.gold, 2));
    helm.line(fx + 0.5, 2, 36, 9, tone(M.gold, 4)); helm.line(fx + 0.5, 3, 36, 10, tone(M.gold, 1));
    helm.line(fx - 0.5, 3, fx - 0.5, 23, tone(M.gold, 5)); helm.line(fx + 0.5, 3, fx + 0.5, 23, tone(M.gold, 2));
    for (const [x, y] of [[5, 4], [11, 1], [26, 2], [31, 6], [2, 21], [5, 25], [33, 20], [32, 25], [9, 9], [28, 10], [12, 28], [27, 29]]) helm.dot(x, y, tone(M.gold, 2));
    // the wearer's eyes, seen through the openings
    for (let y = 16; y <= 20; y++) for (const [x0, x1] of [[11, 16], [26, 30]]) for (let x = x0; x <= x1; x++) {
      const ch = BODY_ROWS[y][x]; if (ch !== '.') eyes.set(x, y, scale(BODY_PAL[ch], 0.42));   // in the helm's shadow
    }
    return [back, helm, eyes];
  },
  Shoulders() {
    return sides((S, l) => {
      const cx = scx(S, 4.5);
      l.fill(ell(cx, 43, 7, 3), dome(M.gold, cx - 1, 42, 8, 4));
      l.fill(ell(cx, 39, 8, 3.5), dome(M.white, cx - 1, 38, 9, 4.5));
      const cap = ell(cx, 33, 8.5, 6);
      l.fill(cap, dome(M.gold, cx - 1.5, 31, 9, 7));
      l.over(band(cap, 1).clip(rect(-40, 34, 80, 60)), tone(M.gold, 1));
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const l = L(DEPTH.torso);
    const bp = poly([[8, 29], [31, 29], [32, 38], [31, 58], [20, 61], [8, 58], [7, 38]]);
    l.fill(bp, dome(M.gold, 16, 38, 17, 22));
    for (let i = 0; i < 8; i++) { const a = i * Math.PI / 4 + 0.39; l.line(20 + Math.cos(a) * 4, 45 + Math.sin(a) * 4, 20 + Math.cos(a) * 9, 45 + Math.sin(a) * 9, tone(M.gold, 2)); }
    l.over(ell(20, 45, 2.5, 2.5), dome(M.gold, 19, 44, 3, 3, 0.2));
    l.fill(rect(10, 29, 29, 30), (x, y) => tone(M.white, y === 29 ? 5 : 3));
    return [l];
  },
  Gloves() {
    return sides((S, l) => {
      l.fill(sidePoly(S, [[-3, 48], [12, 48], [10, 57], [0, 57]]), (x, y, m) => bevel(M.gold, 1, 3)(x, y, m));
      l.over(sideRect(S, -3, 48, 12, 49), tone(M.white, 4));
      l.fill(fist(S, 58, 68, 0, 9), cyl(M.gold, scx(S, 4.5), 5.5));
      for (const y of [60, 63, 66]) lame(l, y, -40, 80, M.gold);
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 57, 31, 62), (x, y) => tone(M.white, y === 57 ? 5 : y === 62 ? 2 : 3));
    l.fill(ell(20, 59.5, 4, 4), dome(M.gold, 19, 58, 4.5, 4.5));
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    l.fill(poly([[5, 62], [12, 62], [12, 79], [5, 76]]), (x, y, m) => bevel(M.gold, 1, 3)(x, y, m));
    l.fill(poly([[27, 62], [34, 62], [34, 76], [27, 79]]), (x, y, m) => bevel(M.gold, 1, 2)(x, y, m));
    for (const y of [67, 72]) lame(l, y, -40, 80, M.gold);
    const tab = poly([[12, 61], [27, 61], [27, 92], [19.5, 98], [12, 92]]);
    l.fill(tab, (x, y) => tone(M.white, x < 15 ? 4 : x > 24 ? 2 : 3));
    l.over(band(tab, 1), tone(M.gold, 4));
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S);
      l.fill(U(ell(cx, 96, 5, 4), rect(cx - 4, 96, cx + 3, 112)), cyl(M.gold, cx, 5));
      l.over(ell(cx, 96, 5, 4), dome(M.gold, cx - 1, 95, 5, 4.5));
      l.fill(rect(cx - 5, 112, cx + 4, 120), cyl(M.gold, cx, 6));
      lame(l, 115, -40, 80, M.gold);
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    const l = L(DEPTH.back);
    const m = poly([[0, 29], [37, 29], [44, 72], [47, 112], [-10, 112], [-7, 72]]);
    l.fill(m, (x) => tone(M.white, x < -4 ? 4 : x > 42 ? 2 : 3));
    l.over(band(m, 2).clip(rect(-40, 40, 80, 130)), (x, y) => tone(M.gold, y > 108 ? 3 : 4));
    return [l];
  },
};

// ================================================================ GOLD II - CATAPHRACT
const cataphract = {
  id: 'cataphract', tier: 'Gold', name: 'Cataphract', blurb: 'Gold lamellar over crimson: a solid gold face mask with closed, engraved eyes in a crimson hood, lamellar wings at the shoulders, a cord tied high across the chest, crimson boots and a cloak pinned at one shoulder.',
  Head() {
    const helm = L(DEPTH.helm);
    // a crimson hood framing the face, falling to the shoulders
    const hood = U(ell(17.5, 11, 20, 16).clip(rect(-30, -30, 60, 20)), poly([[-3, 18], [38, 18], [39, 32], [31, 37], [6, 37], [-3, 32]]));
    helm.fill(hood, dome(M.crimson, 13, 6, 24, 22));
    helm.over(rect(-30, 5, 60, 7), (x, y) => (y === 6 && x % 4 === 1 ? tone(M.gold, 5) : tone(M.goldDk, y === 5 ? 4 : 3)));
    // the gold mask: a calm face, solid - its eyes are engraved, closed
    const mask = poly([[6, 10], [32, 10], [34, 18], [32, 26], [26, 31], [12, 31], [6, 26], [4, 18]]);
    helm.fill(mask, dome(M.gold, 16, 15, 17, 17));
    for (const [x0, x1] of [[11, 16], [25, 30]]) {
      helm.line(x0, 18, x1, 18, tone(M.goldDk, 1)); helm.line(x0 + 1, 19, x1 - 1, 19, tone(M.gold, 5));
      helm.dot(x0 - 1, 17, tone(M.goldDk, 2)); helm.dot(x1 + 1, 17, tone(M.goldDk, 2));
    }
    helm.line(9, 13, 16, 12, tone(M.gold, 5)); helm.line(24, 12, 31, 13, tone(M.gold, 4));
    helm.line(20, 17, 20, 24, tone(M.gold, 5)); helm.line(21, 18, 21, 24, tone(M.gold, 2));
    helm.line(18, 25, 22, 25, tone(M.gold, 1));
    helm.line(16, 28, 23, 28, tone(M.goldDk, 1));
    return [helm];
  },
  Shoulders() {
    return sides((S, l) => {
      for (let k = 0; k < 6; k++) {
        const x0 = -4 + k * 3, top = 31 + Math.abs(k - 2.5) * 0.6, bot = 46 - Math.abs(k - 2.5) * 1.2;
        l.fill(sideRect(S, x0, top, x0 + 1, bot), (x, y) => tone(M.gold, (x === sx(S, x0)) === (S === NEAR) ? (y === Math.round(top) ? 5 : 4) : 2));
        l.over(sideRect(S, x0, bot - 1, x0 + 1, bot), tone(M.goldDk, 2));
      }
      const cap = ell(scx(S, 4.5), 31, 8, 3.5); l.fill(cap, dome(M.gold, scx(S, 3), 30, 8, 4));
      l.over(band(cap, 1).clip(rect(-40, 32, 80, 40)), tone(M.goldDk, 1));
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const l = L(DEPTH.torso);
    const m = chestPoly(30, 61);
    l.fill(m, (x, y) => {
      const row = Math.floor(y / 5), ry = y % 5, cx = ((x + (row % 2)) % 2 + 2) % 2;
      const b = 8, t = (x - b) / 24, f = t < 0.25 ? 1 : t > 0.72 ? -1 : 0;
      if (ry === 4) return tone(M.gold, 0); if (cx === 1) return tone(M.gold, 2 + f); return tone(M.gold, (ry === 0 ? 5 : 3) + f);
    });
    l.fill(rect(9, 43, 30, 45), (x, y) => tone(M.crimson, y === 43 ? 4 : y === 45 ? 2 : 3));
    l.fill(ell(20, 44, 3, 2.5), dome(M.crimson, 19, 43, 3.5, 3));
    l.fill(rect(10, 29, 29, 30), tone(M.goldDk, 4));
    return [l];
  },
  Gloves() {
    return sides((S, l) => {
      l.fill(sideRect(S, -1, 46, 10, 58), cyl(M.leatherDk, scx(S, 4.5), 6));
      for (const x of [0, 3, 6]) l.over(sideRect(S, x, 47, x + 1, 57), (xx, y) => tone(M.gold, ((xx === sx(S, x)) === (S === NEAR)) ? (y === 47 ? 5 : 4) : 2));
      l.over(sideRect(S, -1, 46, 10, 46), tone(M.crimson, 3)); l.over(sideRect(S, -1, 58, 10, 58), tone(M.crimson, 2));
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 57, 31, 62), (x, y) => tone(M.crimson, y === 57 ? 4 : y === 62 ? 2 : 3));
    for (let x = 10; x <= 30; x += 4) l.fill(rect(x, 58, x + 1, 61), (xx, y) => tone(M.gold, xx === x ? (y === 58 ? 5 : 4) : 2));
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    l.fill(poly([[6, 61], [33, 61], [34, 88], [5, 88]]), (x, y) => tone(M.crimson, x < 9 ? 4 : x > 30 ? 2 : 3));
    for (let x = 7; x <= 32; x += 3) l.fill(rect(x, 62, x + 1, 77), (xx, y) => tone(M.gold, y >= 76 ? 2 : xx === x ? (y === 62 ? 5 : 4) : 3));
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S);
      l.fill(rect(cx - 4, 90, cx + 3, 120), cyl(M.crimson, cx, 5));
      l.fill(rect(cx - 2, 92, cx + 1, 108), cyl(M.gold, cx - 0.5, 2.5));
      l.over(rect(cx - 4, 116, cx + 3, 120), (x, y) => tone(M.gold, y === 116 ? 5 : 3));
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    const l = L(DEPTH.back), pin = L(DEPTH.front);
    l.fill(poly([[0, 29], [37, 29], [44, 70], [46, 112], [12, 114], [-9, 106], [-6, 70]]), (x) => tone(M.crimson, x < -3 ? 4 : x > 41 ? 2 : 3));
    for (const x of [-4, 42]) l.line(x, 70, x + (x < 19 ? -2 : 2), 108, tone(M.crimson, 1));
    pin.fill(ell(11, 33, 2.5, 2.5), dome(M.gold, 10, 32, 3, 3, 0.2));
    return [l, pin];
  },
};

// ================================================================ GOLD III - WINGWARD
const feather = (l, cx, cy, rx, ry, deg, r, quill = true) => {
  const m = rell(cx, cy, rx, ry, deg); l.fill(m, (x, y, mm) => bevel(r, 1, 3)(x, y, mm));
  if (quill) { const a = deg * Math.PI / 180; l.line(cx + Math.sin(a) * ry * 0.8, cy - Math.cos(a) * ry * 0.8, cx - Math.sin(a) * ry * 0.6, cy + Math.cos(a) * ry * 0.6, tone(r, 2)); }
  return m;
};
const wingward = {
  id: 'wingward', tier: 'Gold', name: 'Wingward', blurb: 'Gold feathers over teal: a wreath of two gold wings, their tips meeting over the brow and their feathers fanning up at the temples; feather-plate pauldrons and skirt, finned vambraces and ankles, and a teal mantle hemmed in gold feathers.',
  Head() {
    // a CIRCLET of two wings round the head (the user's wreath reference, tips at the front): a
    // band four texels tall across the brow, its ends turning down at the temples as it goes
    // round the sides, so the hair shows above it - worn round the head, not on it. Each wing's
    // feathers are saw-teeth along the band, every tip pointing forward to where the two meet.
    const helm = L(DEPTH.helm);
    // a thin piece carries the BODY's two-texel edge, as the hair does, not gear's four: with
    // four, the outline alone covered the hair and the circlet read as a hat brim
    helm.outR = 2;
    const fx = 20.5;
    const halves = [{ from: 1, to: fx, n: 4, near: true }, { from: fx, to: 35, n: 3, near: false }];
    for (const h of halves) {
      const w = (h.to - h.from) / h.n;
      for (let x = Math.floor(h.from); x <= Math.ceil(h.to) - 1; x++) {
        const along = h.near ? (x + 0.5 - h.from) : (h.to - (x + 0.5));     // 0 at the temple
        const f = Math.min(h.n - 1, Math.floor(along / w)), u = (along - f * w) / w;   // u: 0 trailing edge, 1 tip
        const edgeDist = Math.min(x - 1, 35 - x);                          // the band turns down near the temples
        const top = 9 - Math.round(u * 1.5), bot = edgeDist < 4 ? 14 : 12;
        const r = f % 2 ? M.goldDk : M.gold;
        for (let y = top; y <= bot; y++) {
          let t = y === top ? 5 : y === top + 1 ? 4 : y === bot ? 2 : 3;
          if (u < 0.16) t = 1;                                             // the feather behind's edge
          helm.set(x, y, tone(r, t));
        }
      }
    }
    // the two tips meet in a small notch at the front
    helm.c.delete(K(20, 7)); helm.c.delete(K(21, 7)); helm.c.delete(K(20, 8)); helm.c.delete(K(21, 8));
    for (const [k] of [...helm.c]) { const x = KX(k), y = KY(k); if (y > 12 && x >= 6 && x <= 34) helm.c.delete(k); }
    return [helm];
  },
  Shoulders() {
    return sides((S, l) => {
      const f = S === NEAR ? 1 : -1;
      feather(l, scx(S, -1), 45, 2.8, 7, f * 40, M.gold);
      feather(l, scx(S, 1), 41, 3, 7.5, f * 28, M.gold);
      feather(l, scx(S, 3), 38, 3.2, 7.5, f * 14, M.gold);
      const cap = ell(scx(S, 4.5), 32, 8, 4.5); l.fill(cap, dome(M.gold, scx(S, 3), 30, 9, 6));
      l.over(band(cap, 1).clip(rect(-40, 33, 80, 60)), tone(M.gold, 1));
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const l = L(DEPTH.torso);
    l.fill(chestPoly(30, 61), scales(M.gold, 4, 5));
    l.fill(rect(10, 27, 29, 31), (x, y) => tone(M.teal, y === 27 ? 4 : 3));
    return [l];
  },
  Gloves() {
    return sides((S, l) => {
      const f = S === NEAR ? 1 : -1;
      feather(l, scx(S, -3), 52, 2, 6, -f * 20, M.gold, false);
      l.fill(sideRect(S, -1, 47, 10, 58), cyl(M.gold, scx(S, 4.5), 6));
      lame(l, 52, -40, 80, M.gold);
      l.fill(fist(S, 59, 68, 0, 9), cyl(M.teal, scx(S, 4.5), 5.5));
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 57, 31, 62), (x, y) => tone(M.teal, y === 57 ? 4 : y === 62 ? 2 : 3));
    l.fill(poly([[17, 56], [22, 56], [23, 59.5], [19.5, 64], [16, 59.5]]), dome(M.gold, 19, 58, 4, 4));
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    l.fill(poly([[8, 61], [31, 61], [31, 80], [8, 80]]), grad(M.teal));
    const xs = [7, 11.5, 16, 20.5, 25, 29.5], len = [20, 25, 28, 28, 25, 20];
    xs.forEach((x, i) => l.fill(poly([[x, 61], [x + 4, 61], [x + 4, 61 + len[i] - 4], [x + 2, 61 + len[i]], [x, 61 + len[i] - 4]]), (xx, y, m) => bevel(M.gold, 1, i < 2 ? 4 : i > 3 ? 2 : 3)(xx, y, m)));
    xs.forEach((x, i) => l.line(x + 2, 64, x + 2, 61 + len[i] - 3, tone(M.gold, 2)));
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S), f = S === NEAR ? 1 : -1;
      feather(l, cx - f * 6, 108, 1.8, 5, -f * 35, M.gold, false);
      l.fill(U(ell(cx, 92, 5, 4), rect(cx - 4, 92, cx + 3, 112)), cyl(M.gold, cx, 5));
      l.over(ell(cx, 92, 5, 4), dome(M.gold, cx - 1, 91, 5, 4.5));
      l.fill(rect(cx - 5, 112, cx + 4, 120), cyl(M.gold, cx, 6));
      lame(l, 115, -40, 80, M.gold);
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    const l = L(DEPTH.back);
    const m = poly([[0, 29], [37, 29], [44, 70], [46, 100], [-9, 100], [-6, 70]]);
    l.fill(m, (x) => tone(M.teal, x < -3 ? 4 : x > 41 ? 2 : 3));
    for (let x = -9; x <= 44; x += 4.5) feather(l, x + 2, 103, 2.4, 6, 0, M.gold);
    return [l];
  },
};

// OPTION B: the same set, its wings rising from the temples and sweeping in until their tips
// meet over the head - a crown made of the two wings, again with no band or helm
const wingwardB = Object.assign({}, wingward, {
  id: 'wingwardB', name: 'Wingward (wings meet)',
  blurb: wingward.blurb,
  Head() {
    // Option A's wings at the temples, tilted IN: the longest feather of each leans over the head
    // until the two tips meet, the rest fanning out below it
    const helm = L(DEPTH.helm);
    for (const S of [NEAR, FAR]) {
      const X = x => S === NEAR ? x : 35 - x, f = S === NEAR ? 1 : -1;
      const root = [X(1), 7];
      const fan = [[-22, 9], [-6, 12.5], [10, 16], [25, 20], [39, 23.5]];
      for (const [deg, len] of fan) {
        const a = f * deg * Math.PI / 180;
        feather(helm, root[0] + Math.sin(a) * len * 0.52, root[1] - Math.cos(a) * len * 0.52, 2.8, len * 0.55, f * deg, M.gold);
      }
      helm.fill(ell(root[0], root[1] + 1, 4, 3.5), dome(M.gold, root[0] - 1, root[1], 4.5, 4, 0.1));
    }
    return [helm];
  },
});

// ================================================================ BRONZE III - ATHANOR
const athanor = {
  id: 'athanor', tier: 'Bronze', name: 'Athanor', blurb: "An alchemist's apprentice: a leather cap with bronze goggles pushed up, a scorched leather apron to the knee, gauntlet gloves to the elbow, vials on the belt, bronze-capped boots and a leather backpack with a flask standing out of it.",
  Head() {
    const helm = L(DEPTH.helm);
    const cap = ell(17.5, 10, 18, 12.5).clip(rect(-30, -30, 60, 9));
    helm.fill(cap, dome(M.leather, 14, 4, 20, 15));
    helm.over(rect(-30, 7, 60, 9), (x, y) => tone(M.bronzeDk, y === 7 ? 4 : 2));
    helm.over(rect(-30, 2, 60, 3), (x, y) => tone(M.leatherDk, y === 2 ? 3 : 1));
    for (const gx of [10.5, 24.5]) {
      const lens = ell(gx, 3, 4, 4);
      helm.fill(lens, dome(M.bronze, gx - 1, 2, 4.5, 4.5));
      helm.fill(ell(gx, 3, 2.4, 2.4), (x, y) => tone(M.teal, x < gx - 0.5 && y < 3 ? 5 : 2));
      helm.dot(Math.floor(gx) - 1, 2, tone(M.teal, 5));
    }
    return [helm];
  },
  Shoulders() {
    return sides((S, l) => {
      const cx = scx(S, 4.5), m = ell(cx, 34, 7.5 * (S === NEAR ? 1 : 0.9), 5.5);
      l.fill(m, dome(M.leather, cx - 2, 32, 8.5, 7));
      l.over(band(m, 1).clip(rect(-40, 35, 80, 60)), tone(M.leatherDk, 2));
      for (const dx of [-3, 0, 3]) rivet(l, Math.round(cx + dx), 31, M.bronze);
      return l;
    }, DEPTH.nearPauldron, DEPTH.farPauldron);
  },
  Torso() {
    const l = L(DEPTH.torso);
    l.fill(chestPoly(30, 61), grad(M.linen));
    const bib = poly([[12, 34], [27, 34], [28, 61], [11, 61]]);
    l.fill(bib, (x, y, m) => bevel(M.leatherDk, 1, 3)(x, y, m));
    for (let y = 37; y <= 59; y += 3) { l.dot(12, y, tone(M.leather, 4)); l.dot(27, y, tone(M.leather, 4)); }
    for (const [x, y] of [[15, 44], [16, 45], [23, 51], [22, 52], [24, 51], [18, 57]]) l.dot(x, y, tone(M.leatherDk, 0));
    l.line(12, 34, 14, 29, tone(M.leatherDk, 3), false); l.line(27, 34, 25, 29, tone(M.leatherDk, 3), false);
    for (const x of [12, 27]) { l.dot(x, 34, tone(M.bronze, 5)); l.dot(x + 1, 35, tone(M.bronze, 2)); }
    return [l];
  },
  Gloves() {
    return sides((S, l) => {
      l.fill(sidePoly(S, [[-3, 40], [12, 40], [10, 46], [-1, 46]]), (x, y, m) => bevel(M.leather, 1, 3)(x, y, m));
      l.fill(fist(S, 46, 69), cyl(M.leatherDk, scx(S, 4.5), 6));
      l.over(sideRect(S, -1, 47, 10, 48), (x, y) => tone(M.bronze, y === 47 ? 5 : 2));
      return l;
    }, DEPTH.nearGlove, DEPTH.farGlove);
  },
  Belt() {
    const l = L(DEPTH.belt);
    l.fill(rect(8, 57, 31, 61), (x, y) => tone(M.leather, y === 57 ? 4 : y === 61 ? 2 : 3));
    for (const [x, g] of [[11, M.red], [15, M.teal], [23, M.olive]]) {
      l.fill(rect(x, 60, x + 2, 61), tone(M.leatherDk, 3));
      l.fill(U(rect(x, 62, x + 2, 66), rect(x - 1, 64, x + 3, 67)), (xx, y) => tone(g, xx <= x ? 5 : 3));
    }
    l.fill(rect(27, 59, 32, 67), (x, y, m) => bevel(M.leather, 1, 3)(x, y, m));
    rivet(l, 29, 61, M.bronze);
    return [l];
  },
  Legs() {
    const l = L(DEPTH.legs);
    for (const S of [NEAR, FAR]) { const cx = legC(S); l.fill(rect(cx - 4, 62, cx + 3, 98), cyl(M.linen, cx, 5)); }
    const apron = new Mask();
    for (let x = 10; x <= 29; x++) { const bot = 90 + ((x * 5) % 3); for (let y = 61; y <= bot; y++) apron.add(x, y); }
    l.fill(apron, (x, y) => tone(M.leatherDk, y > 86 ? 1 : x < 12 ? 4 : x > 27 ? 2 : 3));
    for (const [x, y] of [[14, 78], [15, 79], [24, 83], [25, 84], [20, 70]]) l.dot(x, y, tone(M.leatherDk, 0));
    return [l];
  },
  Boots() {
    return sides((S, l) => {
      const cx = legC(S);
      l.fill(rect(cx - 4, 98, cx + 3, 114), cyl(M.leatherDk, cx, 5));
      l.fill(rect(cx - 5, 114, cx + 4, 120), (x, y, m) => bevel(M.leatherDk, 1, 3)(x, y, m));
      l.over(rect(cx - 5, 116, cx + 4, 120), (x, y) => tone(M.bronze, y === 116 ? 5 : x < cx - 2 ? 4 : 3));
      l.over(rect(cx - 4, 104, cx + 3, 105), (x, y) => tone(M.leather, y === 104 ? 4 : 2));
      l.dot(cx + 1, 104, tone(M.bronze, 5));
      return l;
    }, DEPTH.boots, DEPTH.boots);
  },
  Back() {
    // an apprentice's leather backpack: from the front its top corners rise beside the head and
    // its sides show past the arms; the straps come over the shoulders and down the chest, and a
    // stoppered flask stands out of the top
    const l = L(DEPTH.back), over = L(DEPTH.backOver);
    const pack = poly([[-4, 21], [41, 21], [42, 60], [-5, 60]]);
    l.fill(pack, (x, y, m) => bevel(M.leather, 1, x < 0 ? 4 : x > 38 ? 2 : 3)(x, y, m));
    l.fill(poly([[-5, 17], [42, 17], [43, 26], [-6, 26]]), (x, y) => tone(M.leatherDk, y === 17 ? 4 : y >= 25 ? 1 : x < 0 ? 3 : 2));
    for (const x of [-3, 40]) { l.over(rect(x, 26, x + 1, 30), tone(M.leatherDk, 3)); l.dot(x, 29, tone(M.bronze, 5)); }
    // side pouches past the arms
    l.fill(rect(-8, 38, -3, 54), (x, y, m) => bevel(M.leatherDk, 1, 3)(x, y, m));
    l.fill(rect(40, 38, 45, 54), (x, y, m) => bevel(M.leatherDk, 1, 2)(x, y, m));
    // the flask, its neck out of the top
    l.fill(rect(36, 8, 39, 17), (x) => tone(M.teal, x === 36 ? 5 : 3));
    l.fill(rect(35, 5, 40, 8), (x, y) => tone(M.leather, y === 5 ? 4 : 2));
    // the straps, over the shoulders and down the chest
    for (const x0 of [10, 27]) over.fill(rect(x0, 29, x0 + 2, 52), (x) => tone(M.leatherDk, x === x0 ? 4 : 2));
    for (const x0 of [10, 27]) { over.fill(rect(x0 - 1, 44, x0 + 3, 46), (x, y) => tone(M.bronze, y === 44 ? 5 : 2)); }
    return [l, over];
  },
};

const SETS = [hoplite, wayfarer, athanor, hauberk, kettle, sentinel, sol, cataphract, wingward];
const ROUND = {
  hoplite: { state: 'kept', note: 'Kept.' },
  wayfarer: { state: 'kept', note: 'Kept.' },
  athanor: { state: 'kept', note: 'Kept, with the backpack.' },
  hauberk: { state: 'kept', note: 'Kept.' },
  kettle: { state: 'kept', note: 'Kept.' },
  sentinel: { state: 'kept', note: 'Kept.' },
  sol: { state: 'reworked', note: 'Each cheek guard tapers to its own point either side of the gap; the nasal ends flat, with no point.' },
  cataphract: { state: 'kept', note: 'Kept.' },
  wingward: { state: 'kept', note: 'Kept.' },
};
const SLOTS = ['Head', 'Shoulders', 'Torso', 'Gloves', 'Belt', 'Legs', 'Boots', 'Back'];

// picks: { slot: setId|null } (null = nothing); default = the set's own pieces
function wear(set, picks) {
  const layers = Object.values(bodyLayers());
  for (const slot of SLOTS) {
    const id = picks ? picks[slot] : set.id;
    if (!id) continue;
    const src = SETS.find(s => s.id === id);
    if (src && src[slot]) layers.push(...src[slot]());
  }
  return layers;
}
