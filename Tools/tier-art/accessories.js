// THE SOURCE of the game's accessories: Assets/Scripts/Art/Gear/Accessories.cs is a number-for-number port of
// this file (TexelKit.cs of kit.js), drawn live by the rig - change a piece here, then there (ART-NOTES.md, "Accessories").
// Accessory-slot mocks (round 2, 2026-11-08). Loaded after body.js, kit.js, sets.js and bake.js lines 72-189
// (flatten, backOf, BACKS - the helm backs). Each accessory draws FRONT layers and BACK layers; both views
// wear the full set (see backSetLayers for the game's turned-away order).
// st: { t, walk, el, charges, anim:{kind,t0,idx}|null, cracked:[], run (0..1), pal, dash (0..1) }

const ELEMENT = { Fire: '#FF6B2E', Water: '#409EFF', Earth: '#8CC75C', Air: '#D9D9F2' };
const AM = {
  glass: ramp('#7f97a6', { sheen: '#ffffff', lift: 0.5 }),
  cork: ramp('#9a7048', { sheen: '#f0c890', lift: 0.32 }),
  wood: ramp('#5b3d26', { sheen: '#c89868', lift: 0.30 }),
  sand: ramp('#d9b46a', { sheen: '#fff2c8', lift: 0.40 }),
  stone: ramp('#b3122e', { sheen: '#ff9a7a', lift: 0.45 }),
  tome: ramp('#5a1e2a', { sheen: '#d88a8a', lift: 0.30 }),
  paper: ramp('#cdbb90', { sheen: '#fffbe8', lift: 0.40 }),
  hair: ramp('#3a2a22', { sheen: '#a07a5a', lift: 0.30 }),
};
const FEATHERS = {
  Crow: { r: ramp('#363a50', { sheen: '#a8bcf4', lift: 0.40 }) },
  Swan: { r: ramp('#cfccc4', { sheen: '#ffffff', lift: 0.40 }) },
  Peacock: { r: ramp('#1f5f5a', { sheen: '#8fe8c8', lift: 0.34 }), eye: true },
  Phoenix: { r: ramp('#a8261c', { sheen: '#ffd070', lift: 0.40 }), tip: ramp('#e8a02a', { sheen: '#fff2b0', lift: 0.4 }) },
};
const CLOTH = {
  Crimson: ramp('#8a1a2a', { sheen: '#ff8a8a' }),
  Linen: ramp('#c2ad7e', { sheen: '#fff6dc', lift: 0.30 }),
  Ink: ramp('#2a2c3a', { sheen: '#8a90b0', lift: 0.30 }),
  Navy: ramp('#2c3a5c', { sheen: '#8fa8d8' }),
};
const liquid = st => ramp(ELEMENT[st.el], { lift: 0.42, shade: 0.34 });
const ease = p => p < 0 ? 0 : p > 1 ? 1 : p * p * (3 - 2 * p);
const lerp = (a, b, k) => a + (b - a) * k;
const mirrorX = x => 37 - x;   // the back view sees the body from behind: screen left and right swap

// a strap: the quad from A to B, w wide, lit along its upper edge, a stitch mark every 4
function strapMask(ax, ay, bx, by, w) {
  const dx = bx - ax, dy = by - ay, L = Math.hypot(dx, dy), nx = -dy / L * w / 2, ny = dx / L * w / 2;
  return poly([[ax + nx, ay + ny], [bx + nx, by + ny], [bx - nx, by - ny], [ax - nx, ay - ny]]);
}
const strapShade = (r, ax, ay, bx, by, w) => (x, y) => {
  const dx = bx - ax, dy = by - ay, L = Math.hypot(dx, dy);
  const u = ((x + .5 - ax) * dx + (y + .5 - ay) * dy) / L, v = ((x + .5 - ax) * -dy + (y + .5 - ay) * dx) / L / (w / 2);
  const lit = (dx * dy < 0) ? v > 0.45 : v < -0.45, shade = (dx * dy < 0) ? v < -0.45 : v > 0.45;
  if (lit) return tone(r, 4); if (shade) return tone(r, 2);
  return tone(r, ((Math.floor(u) % 4) + 4) % 4 === 0 ? 2 : 3);
};
function strap(l, ax, ay, bx, by, w, r) { l.fill(strapMask(ax, ay, bx, by, w), strapShade(r, ax, ay, bx, by, w)); }

// ---------------------------------------------------------------- the phial drink (shared)
// 0.9 s: the cork lifts at once, the liquid drains 0.15-0.45 (the heal lands at 0.45), a flash.
// A spill: a blow before 0.45 - the glass cracks at 0.2 and the liquid sprays out.
const DRINK_S = 0.9;
function drinkPhase(st) { return st.anim ? (st.t - st.anim.t0) / DRINK_S : -1; }

// a phial 4 wide: cork 2 rows, neck 1, body 7 (corners cut). level 0..1. Returns its mouth.
function phial(l, cx, top, level, st, o = {}) {
  const x0 = cx - 2, x1 = cx + 1, liq = liquid(st), BOT = top + 9;
  const body = rect(x0, top + 3, x1, BOT); body.del(x0, BOT); body.del(x1, BOT);
  const neck = rect(x0 + 1, top + 2, x1 - 1, top + 2), glass = U(body, neck);
  for (const [x, y] of band(glass, 1).pts()) l.set(x, y, OUTLINE);   // a dark rim parts it from armour of its own colour
  for (let y = top + 2; y <= BOT + 1; y++) { l.set(x0 - 1, y, OUTLINE); l.set(x1 + 1, y, OUTLINE); }
  l.fill(rect(x0, BOT + 1, x1, BOT + 1), OUTLINE);
  l.fill(glass, (x, y) => tone(AM.glass, x === x0 ? 4 : x === x1 ? 1 : 2));
  const rows = Math.round(level * 5);
  for (let y = BOT - rows + 1; y <= BOT; y++) for (let x = x0; x <= x1; x++) {
    if (!body.has(x, y)) continue;
    l.set(x, y, tone(liq, y === BOT - rows + 1 ? 5 : x === x0 ? 4 : x === x1 ? 2 : 3));
  }
  l.dot(x0, top + 4, tone(AM.glass, 5));
  if (o.cracked) for (const [dx, dy] of [[1, 4], [2, 5], [1, 6], [2, 7]]) l.dot(x0 + dx, top + dy, OUTLINE);
  if (o.cork !== false) {
    const cy = top + (o.corkDy || 0), ccx = cx + (o.corkDx || 0);
    l.fill(rect(ccx - 1, cy, ccx, cy + 1), (x, y) => tone(AM.cork, y === cy ? 4 : x === ccx ? 2 : 3));
  }
  return { x: cx - 0.5, y: top + 2 };
}
function sprayDrops(l, mx, my, p, st, seed) {   // the spilled liquid, flung out of the mouth and falling
  const liq = liquid(st);
  for (let i = 0; i < 7; i++) {
    const q = (p - 0.2 - i * 0.02) / 0.6; if (q <= 0 || q >= 1) continue;
    const vx = Math.sin(seed + i * 2.4) * 14, vy = -10 - (i % 3) * 4;
    const x = Math.round(mx + vx * q), y = Math.round(my + vy * q + 46 * q * q);
    l.set(x, y, tone(liq, 4)); if (i % 2) l.set(x, y + 1, tone(liq, 2));
  }
}
function heldPhialState(st, i, n) {   // which of n phials are full, and the one being drunk
  const p = drinkPhase(st), active = st.anim && st.anim.idx === i;
  let level = i < st.charges ? 1 : 0, cork = i < st.charges, corkDy = 0, corkDx = 0, cracked = !!st.cracked[i];
  if (active) {
    level = st.anim.kind === 'drink' ? 1 - ease((p - 0.15) / 0.3) : 1 - ease((p - 0.2) / 0.15);
    if (st.anim.kind === 'drink') { corkDy = -Math.round(ease(p / 0.35) * 10); corkDx = Math.round(ease(p / 0.35) * 4); cork = p < 0.55; }
    else { cracked = p > 0.2; cork = true; }
  }
  return { level, cork, corkDy, corkDx, cracked, active, p };
}

// ---------------------------------------------------------------- 1. BANDOLIER
const bandolier = {
  id: 'bandolier', backKind: 'under', name: 'Bandolier', where: 'Torso, shoulder to opposite hip',
  front(st, glows) {
    const l = L(14.2), A = [31, 31], B = [10, 63];
    strap(l, A[0], A[1], B[0], B[1], 4, M.leatherDk);
    const S = [0.30, 0.50, 0.70];
    S.forEach((s, i) => {
      const px = A[0] + (B[0] - A[0]) * s, py = A[1] + (B[1] - A[1]) * s;
      const sway = st.walk ? Math.round(Math.sin(st.t * 7 + i * 1.9) * 1.2) : 0;
      const ps = heldPhialState(st, i, 3), cx = Math.round(px) + sway, top = Math.round(py) - 4;
      const mouth = phial(l, cx, top, ps.level, st, { cork: ps.cork, corkDy: ps.corkDy, corkDx: ps.corkDx, cracked: ps.cracked });
      // the loop that holds it, a leather band across the strap and the glass
      l.fill(rect(cx - 3, top + 4, cx + 2, top + 4), tone(M.leather, 3));
      if (ps.active) {
        if (st.anim.kind === 'drink') {
          const k = Math.sin(Math.PI * Math.min(1, ps.p / 0.5));
          glows.push({ x: cx, y: top + 7, r: 4 + 8 * k, c: ELEMENT[st.el], a: 0.55 * k });
          if (ps.p > 0.45 && ps.p < 0.8) { const q = (ps.p - 0.45) / 0.35; glows.push({ x: 19, y: 52, r: 18 + 40 * q, c: ELEMENT[st.el], a: 0.35 * (1 - q), ring: true }); }
        } else sprayDrops(l, mouth.x, mouth.y + 4, ps.p, st, i);
      }
    });
    return [l];
  },
  back(st) {   // the strap alone, from the other shoulder's side
    const l = L(15);
    strap(l, mirrorX(31), 31, mirrorX(10), 63, 4, M.leatherDk);
    l.fill(rect(mirrorX(10) - 2, 60, mirrorX(10) + 1, 63), (x, y, m) => bevel(M.bronze, 1, 3)(x, y, m));
    return [l];
  },
};

// ---------------------------------------------------------------- 4. VIAL HARNESS
const harness = {
  id: 'harness', backKind: 'under', name: 'Vial Harness', where: 'Torso, crossed straps, flask on the sternum',
  front(st, glows) {
    const straps = L(8.6), flask = L(14.2), liq = liquid(st);
    strap(straps, 9, 31, 30, 62, 3, M.leather); strap(straps, 30, 31, 9, 62, 3, M.leather);
    const cx = 19.5, cy = 47, R = 5, p = drinkPhase(st);
    const active = !!st.anim, kind = st.anim && st.anim.kind;
    // the liquid: thirds, the surface tilting with the motion
    let level = st.charges / 3;
    if (active) level = (st.charges - (kind === 'drink' ? ease((p - 0.15) / 0.3) : ease((p - 0.2) / 0.15))) / 3;
    const bulb = ell(cx, cy, R, R);
    flask.fill(bulb, dome(AM.glass, cx - 2, cy - 2, R + 1, R + 1, -0.35));
    flask.fill(rect(19, cy - R - 2, 20, cy - R + 1), (x) => tone(AM.glass, x === 19 ? 4 : 1));
    const tilt = Math.sin(st.t * (st.walk ? 5.2 : 2.4)) * (st.walk ? 0.32 : 0.14) + (active && kind === 'spill' ? 0.4 : 0);
    const inner = ell(cx, cy, R - 1, R - 1), surfY0 = cy + (R - 1) - level * 2 * (R - 1);
    for (const [x, y] of inner.pts()) {
      const s = surfY0 + (x + 0.5 - cx) * tilt;
      if (level > 0.01 && y + 0.5 >= s) flask.set(x, y, tone(liq, y + 0.5 - s < 1.2 ? 5 : x < cx - 2 ? 4 : x > cx + 2 ? 2 : 3));
    }
    flask.dot(Math.floor(cx) - 2, cy - 2, tone(AM.glass, 5));
    // the brass cage: a band round the waist of the flask and the collar at the neck
    flask.over(rect(-40, cy, 80, cy), (x) => tone(M.bronze, x < cx ? 5 : 2));
    flask.fill(rect(18, cy - R - 1, 21, cy - R - 1), (x) => tone(M.bronze, x < 20 ? 5 : 2));
    // the cork lifts and reseats on a drink
    let cdy = 0; if (active && kind === 'drink') cdy = -Math.round(Math.sin(Math.PI * Math.min(1, p / 0.6)) * 4);
    flask.fill(rect(19, cy - R - 4 + cdy, 20, cy - R - 3 + cdy), (x, y) => tone(AM.cork, y === cy - R - 4 + cdy ? 4 : 3));
    if (st.cracked[0] || (active && kind === 'spill' && p > 0.2)) for (const [dx, dy] of [[-1, -2], [0, -1], [-1, 0], [0, 1]]) flask.dot(Math.round(cx) + dx, cy + dy, OUTLINE);
    if (active) {
      if (kind === 'drink') {
        // bubbles rising through what is left, then the flash as the heal lands
        for (let i = 0; i < 4; i++) { const q = (p * 3 + i * 0.27) % 1; const bx = Math.round(cx - 1 + (i * 1.7) % 3), by = Math.round(cy + R - 1 - q * 7); if (inner.has(bx, by) && by + 0.5 >= surfY0 - 1) flask.set(bx, by, tone(liq, 5)); }
        const k = Math.sin(Math.PI * Math.min(1, p / 0.5)); glows.push({ x: cx, y: cy, r: 5 + 7 * k, c: ELEMENT[st.el], a: 0.6 * k });
        if (p > 0.45 && p < 0.8) { const q = (p - 0.45) / 0.35; glows.push({ x: 19, y: 52, r: 18 + 40 * q, c: ELEMENT[st.el], a: 0.35 * (1 - q), ring: true }); }
      } else sprayDrops(flask, cx, cy - R - 3, p, st, 3);
    }
    return [straps, flask];
  },
  back(st) {
    const l = L(15);
    strap(l, 7, 31, 30, 62, 3, M.leather); strap(l, 30, 31, 7, 62, 3, M.leather);
    l.fill(ell(18.5, 46, 2.5, 2.5), dome(M.bronze, 17, 45, 3, 3));
    return [l];
  },
};

// ---------------------------------------------------------------- 5. FEATHER COLLAR
// Rows of feathers SHINGLED down from the shoulder line, tips hanging down and out, never up past the
// chin: the lower rows are drawn first so each row covers the roots of the one below. Front: one piece
// on each shoulder with the chest open between them (the far one narrower and in shadow). Back: one
// band from shoulder to shoulder, its top under the head (the head and helm draw over it).
function shingles(l, st, pal, xs, topY, rows, lenAt, side = 1) {
  const r = pal.r, list = [], STEP = 5;
  for (let row = rows - 1; row >= 0; row--) {
    const stagger = row % 2 ? STEP / 2 : 0;
    for (let fx = xs[0] + stagger; fx <= xs[1] + 0.01; fx += STEP) list.push({ row, fx: Math.round(fx) });
  }
  for (const fe of list) {
    const k = lenAt(fe.fx); if (k <= 0) continue;
    const lean = Math.max(-30, Math.min(30, (19.5 - fe.fx) * 1.4)) * side;
    const ruff = Math.sin(st.t * (st.walk ? 6 : 2.2) + fe.fx * 0.7 + fe.row) * (st.walk ? 4 : 2) * (0.6 + fe.row * 0.4);
    const deg = lean + ruff, a = deg * Math.PI / 180;
    const ry = 5.5 * k, rx = 2.7;
    const rootY = topY(fe.fx) + fe.row * 5 * k;
    const ux = -Math.sin(a), uy = Math.cos(a);   // root -> tip
    const cx = fe.fx + ux * ry * 0.85, cy = rootY + uy * ry * 0.85;
    const m = rell(cx, cy, rx, ry, deg);
    const last = fe.row === rows - 1;
    const at = (x, y) => { const dx = x + .5 - cx, dy = y + .5 - cy; return [(dx * ux + dy * uy) / ry, (dx * uy - dy * ux) / rx]; };
    for (const [x, y] of m.pts()) {
      const [u, v] = at(x, y);
      let c;
      if (pal.eye && last && u > 0.3) c = u > 0.62 && Math.abs(v) < 0.45 ? tone(M.navy, 2) : u > 0.48 ? tone(M.gold, 4) : tone(M.teal, 4);
      else if (pal.tip && last && u > 0.25) c = tone(pal.tip, v < -0.3 ? 5 : v > 0.3 ? 3 : 4);
      else if (Math.abs(v) < 0.22 && u < 0.6) c = tone(r, 2);                 // the quill
      else if (v < -0.25) c = tone(r, u > 0.5 ? 5 : 4);                       // the lit vane, its tip catching the sheen
      else c = tone(r, v > 0.5 ? 2 : 3);
      l.set(x, y, c);
    }
    // a dark seam round the lower half and the shaded side, so each feather stands apart from its neighbours
    for (const [x, y] of band(m, 1).pts()) {
      const [u, v] = at(x, y);
      if ((u > 0 && !m.has(x, y + 1)) || (v > 0.4 && !m.has(x + 1, y))) l.set(x, y, tone(r, 0));
    }
  }
}
function featherLayer(st, back) {
  const pal = FEATHERS[st.pal || 'Crow'];
  if (back) {
    // shoulder to shoulder and out over the pauldrons (the sets' shoulders span x -5..42); the top raised to
    // where the armour begins (the pauldrons' tops, y 27-28, behind the neck up under the head), the hem
    // kept where round 4 put it, dipping to a point at the spine: each column's feathers stretch to fill
    const l = L(14.6);
    const top = x => 22 + Math.min(5, Math.abs(x - 19) * 0.24);
    const hem = x => 26 + Math.abs(x - 19) * 0.12 + 20.2 * (1.15 - Math.abs(x - 19) / 21 * 0.4);
    shingles(l, st, pal, [-6, 44], top, 3, x => (hem(x) - top(x)) / 20.2, -1);
    return [l];
  }
  // one piece on each shoulder from the side of the neck out past the pauldron, the chest left open;
  // its top just under the shoulder line, its outline clear of the chin; longest over the shoulder's point
  const near = L(14.6), far = L(14.5); far.far = true;
  shingles(near, st, pal, [-3, 11], () => 31, 2, x => 1 - Math.abs(x - 4) / 12 * 0.35);
  shingles(far, st, pal, [27, 40], () => 31, 2, x => 1 - Math.abs(x - 33) / 10 * 0.35);
  return [near, far];
}
const collar = {
  id: 'collar', backKind: 'over', name: 'Feather Collar', where: 'Neck and shoulders, over the pauldrons',
  front(st) { return featherLayer(st, false); },
  back(st) { return featherLayer(st, true); },
};

// ---------------------------------------------------------------- 6. BANNER POLE
function bannerFlag(l, st, x0, dir, top) {   // a vertical banner hung from a crossbar, its free edge rippling
  const r = CLOTH[st.pal || 'Crimson'], w = 12, h = 27, m = new Mask();
  const ripple = y => Math.round(Math.sin(st.t * (st.walk ? 6.5 : 3.4) - (y - top) * 0.32) * (st.walk ? 1.6 : 1.0));
  for (let y = top; y < top + h; y++) {
    const ext = w + (y - top > 3 ? ripple(y) : 0), notch = y > top + h - 5 ? (y - (top + h - 5)) : -1;
    for (let i = 0; i < ext; i++) { if (notch >= 0 && Math.abs(i - w / 2) < notch) continue; m.add(x0 + dir * i, y); }
  }
  l.fill(m, (x, y) => { const i = (x - x0) * dir; const sl = Math.sin(st.t * (st.walk ? 6.5 : 3.4) - (y - top) * 0.32 + 1.2); return tone(r, i === 0 ? 2 : i === 1 ? 4 : sl > 0.4 ? 4 : sl < -0.4 ? 2 : 3); });
  // the sigil (a circumpunct standing in for the wearer's own)
  const gx = x0 + dir * 6, gy = top + 11, ring = band(ell(gx, gy, 4.2, 4.2), 1);
  l.over(ring, tone(M.gold, 4)); l.over(rect(gx - 1, gy - 1, gx, gy), tone(M.gold, 5));
  l.over(rect(x0 - 40, top + h - 9, x0 + 40, top + h - 9), tone(M.gold, 3));
}
const banner = {
  id: 'banner', backKind: 'pole', name: 'Banner Pole', where: 'Back, behind the far shoulder',
  front(st) {
    const l = L(0.5), X = 34;
    l.fill(rect(X, -40, X + 1, 40), (x) => tone(AM.wood, x === X ? 4 : 2));
    l.fill(rect(X, -39, X + 14, -38), (x, y) => tone(AM.wood, y === -39 ? 4 : 2));
    l.fill(poly([[X + 0.5, -49], [X + 2.5, -44], [X + 1.5, -40], [X - 0.5, -40], [X - 1.5, -44]]), (x, y) => tone(M.bronze, x <= X ? 5 : 2));
    bannerFlag(l, st, X + 2, 1, -37);
    return [l];
  },
  back(st) {
    const l = L(15), X = 2;
    l.fill(rect(X, -40, X + 1, 58), (x) => tone(AM.wood, x === X ? 4 : 2));
    l.fill(rect(X - 13, -39, X + 1, -38), (x, y) => tone(AM.wood, y === -39 ? 4 : 2));
    l.fill(poly([[X + 0.5, -49], [X + 2.5, -44], [X + 1.5, -40], [X - 0.5, -40], [X - 1.5, -44]]), (x, y) => tone(M.bronze, x <= X ? 5 : 2));
    bannerFlag(l, st, X - 1, -1, -37);
    // its socket on the back: two bronze collars and a strap to the other shoulder
    strap(l, X, 34, 33, 30, 3, M.leatherDk); strap(l, X, 54, 30, 60, 3, M.leatherDk);
    for (const y of [33, 53]) l.fill(rect(X - 1, y, X + 2, y + 2), (x, yy) => tone(M.bronze, yy === y ? 5 : 2));
    return [l];
  },
};

// ---------------------------------------------------------------- 8. HOURGLASS PACK
function hourglass(l, st, cx, top) {
  const f = Math.max(0, Math.min(1, st.run)), H = 34;
  const plate = (y) => l.fill(rect(cx - 10, y, cx + 9, y + 2), (x, yy, m) => bevel(M.bronze, 1, 3)(x, yy, m));
  plate(top); plate(top + H - 2);
  for (const x0 of [cx - 9, cx + 7]) l.fill(rect(x0, top + 3, x0 + 1, top + H - 3), (x) => tone(M.bronzeDk, x === x0 ? 4 : 2));
  const topB = ell(cx, top + 9.5, 6, 7).clip(rect(-40, top + 3, 80, top + 15));
  const botB = ell(cx, top + H - 10.5, 6, 7).clip(rect(-40, top + 17, 80, top + H - 3));
  const neck = rect(cx - 1, top + 15, cx, top + 17);
  l.fill(U(topB, botB, neck), (x, y, m) => tone(AM.glass, x <= cx - 4 ? 4 : x >= cx + 3 ? 1 : 2));
  // sand: the top drains toward the neck in a funnel, the bottom heaps into a mound
  const sTop = top + 15 - (1 - f) * 11;
  for (const [x, y] of topB.pts()) if (y + 0.5 > sTop + Math.max(0, 2.2 - Math.abs(x + 0.5 - cx)) * (f > 0.02 ? 0.9 : 0) && Math.abs(x + 0.5 - cx) < 5.5) l.set(x, y, tone(AM.sand, x < cx - 1 ? 4 : 3));
  const pile = top + H - 3 - f * 11;
  for (const [x, y] of botB.pts()) if (y + 0.5 > pile + Math.abs(x + 0.5 - cx) * 0.55 && Math.abs(x + 0.5 - cx) < 5.5) l.set(x, y, tone(AM.sand, y + 0.5 < pile + Math.abs(x + 0.5 - cx) * 0.55 + 1.2 ? 4 : 3));
  if (f < 1 && f > 0) for (let y = top + 17; y < pile; y++) { const flick = Math.sin(st.t * 23 + y * 2.7) > -0.3; if (flick) l.set(y % 2 ? cx - 1 : cx, y, tone(AM.sand, 5)); }
  l.dot(cx - 4, top + 6, tone(AM.glass, 5)); l.dot(cx - 4, top + 7, tone(AM.glass, 5)); l.dot(cx - 4, top + H - 13, tone(AM.glass, 5));
}
const hourglassPack = {
  id: 'hourglass', backKind: 'overTop', name: 'Hourglass Pack', where: 'Back, between the shoulder blades',
  front() {
    // from the front: the brass yoke's finials stand up behind the shoulders, the straps come down the chest
    const back = L(0.6), over = L(8.6);
    for (const X of [-4, 41]) {
      back.fill(rect(X, 16, X + 1, 31), (x) => tone(M.bronzeDk, x === X ? 4 : 2));
      back.fill(ell(X + 1, 14, 3, 3), dome(M.bronze, X, 13, 3.5, 3.5));
    }
    for (const x0 of [10, 27]) over.fill(rect(x0, 29, x0 + 1, 46), (x) => tone(M.leatherDk, x === x0 ? 4 : 2));
    for (const x0 of [10, 27]) over.fill(rect(x0 - 1, 44, x0 + 2, 45), (x, y) => tone(M.bronze, y === 44 ? 5 : 2));
    return [back, over];
  },
  back(st) {
    const l = L(15);
    l.fill(rect(-4, 29, 42, 31), (x, y) => tone(M.bronze, y === 29 ? 5 : 2));   // the yoke over the shoulders
    for (const X of [-4, 41]) { l.fill(rect(X, 16, X + 1, 28), (x) => tone(M.bronzeDk, x === X ? 4 : 2)); l.fill(ell(X + 1, 14, 3, 3), dome(M.bronze, X, 13, 3.5, 3.5)); }
    hourglass(l, st, 19, 32);
    return [l];
  },
};

// ---------------------------------------------------------------- 9. CHAINED TOME
function tomeLayer(st, ax, ay, side, back) {
  const l = L(back ? 15 : 8.4);
  const swing = Math.sin(st.t * (st.walk ? 5.5 : 2.2)) * (st.walk ? 10 : 4) * side;
  const bob = st.walk ? Math.round(Math.abs(Math.sin(st.t * 5.5)) * 1.5) : 0;
  const a = swing * Math.PI / 180, c = Math.cos(a), s = Math.sin(a);
  const toW = (lx, ly) => [ax + lx * c - ly * s, ay + bob + lx * s + ly * c];
  const toL = (x, y) => { const dx = x + .5 - ax, dy = y + .5 - ay - bob; return [dx * c + dy * s, -dx * s + dy * c]; };
  // the chain: alternating links from the belt to the book's top corner
  for (let i = 0; i <= 7; i++) { const [x, y] = toW(i % 2 ? 0 : -0.5, i + 0.5); l.set(Math.floor(x), Math.floor(y), tone(M.steel, i % 2 ? 4 : 2)); if (i % 2 === 0) l.set(Math.floor(x) + 1, Math.floor(y), tone(M.steel, 1)); }
  l.fill(rect(ax - 1, ay - 1, ax + 1, ay + 1), (x, y) => tone(M.steel, x === ax - 1 ? 5 : 2));
  // the book: 12 x 14, top centre hanging from the chain
  const m = new Mask();
  for (let y = ay - 4; y <= ay + 36; y++) for (let x = ax - 18; x <= ax + 18; x++) { const [lx, ly] = toL(x, y); if (lx >= -8 && lx < 8 && ly >= 8 && ly < 27) m.add(x, y); }
  l.fill(m, (x, y) => {
    const [lx, ly] = toL(x, y), fore = back ? lx < -6.5 : lx >= 6.5, spine = back ? lx >= 6.6 : lx < -6.6;
    const corner = (lx < -4.5 || lx >= 4) && (ly < 11 || ly >= 24);
    if (fore) return tone(AM.paper, (Math.floor(ly) % 2) ? 4 : 2);
    if (corner) return tone(M.gold, lx < 0 && ly < 15 ? 5 : 2);
    if (spine) return tone(AM.tome, 1);
    if (!back) {
      const d = Math.hypot(lx + 0.5, ly - 17.5);
      if (d > 3.2 && d < 4.5) return tone(M.gold, lx < 0 ? 5 : 3);
      if (d < 1.5) return tone(M.gold, 4);
      if (lx >= 3.5 && lx < 6.5 && ly >= 16 && ly < 19) return tone(M.bronze, ly < 17 ? 5 : 3);   // the clasp
    }
    return tone(AM.tome, lx < -3 ? 4 : lx > 3 ? 2 : 3);
  });
  return l;
}
// THE ALCHEMIST'S BOOK RELIC (DemoGear.Relics relic_book), as HipRelic hangs it: body density (1:1 with this
// grid), torso-local +RelicHipX (6 cells = 12 texels off the torso's centre x 19) - the FAR hip, the side the
// Gemini Saya's far sheath takes (+x), screen right here - drawn over the body (Trinket: over the belt; the
// far arm is behind the body, so nothing covers it). Its outlined top on the pouch's (6 cells over the hip
// at y 72, so the grid's top row is y 64). Palette.Of(main, second, third): letters k s d b l h = line..glow,
// upper case the second ramp, digits 1-6 the third.
// Every look a rolled relic can wear (DemoGear.StandardRelicIds), grids and palettes copied from
// DemoGear.Relics.cs and the pouch (DemoGear.PouchRows). Ramps as Palette.Ramp builds them (kit ramp = the same
// lerps with a white sheen); Bronze's highlights lean warm, as WithHighlightsToward does.
const RR = {
  leather: ramp('#75523d'), bronze: ramp('#9e662b', { sheen: '#ffe094', lift: 0.42, shade: 0.42 }),
  iron: ramp('#6b6b75'), ironLt: ramp('#999aa3'), glass: ramp('#9eccdb'), gold: ramp('#e6b84d'), cream: ramp('#f0e6cc'),
};
function ofPal(a, b, c) {
  const m = {}, rs = [a, b, c];
  'ksdblh'.split('').forEach((ch, i) => { m[ch] = tone(a, i); if (b) m[ch.toUpperCase()] = tone(b, i); if (c) m[String(i + 1)] = tone(c, i); });
  return m;
}
const RELICS = [
  { id: 'leather_pouch', name: "Wayfarer's Pouch", pal: ofPal(RR.leather), rows: ["..kkkkkk..", ".llllllll.", "bbbbbbbbbb", "bbbbbbbbbb", "dddddddddd", ".ssssssss.", ".bbbbbbbb.", ".bbbbbbbb.", ".bbbbbbbb.", ".dddddddd.", "..ssssss..", "..ssssss.."] },
  { id: 'relic_hoop', name: "Courier's Hoop", pal: ofPal(RR.bronze, RR.leather), rows: ["....SBBS....", "....SBBS....", "...hllllb...", "..hlbbbbbd..", ".hlb....bdd.", "hlb......bds", "lb........ds", "lb........ds", "lb........ds", "bd.......dds", ".bd.....dds.", "..ddddddss..", "...ssssss..."] },
  { id: 'relic_book', name: "Alchemist's Book", pal: ofPal(ramp('#802b26'), RR.gold, RR.cream), rows: ["....dd....", "....dd....", "Hhllllllb5", "Lhbbbbbbd5", "lbbbLLbbd5", "lbbLBBLbd5", "lbLBBBBLd5", "lbbLBBLbd5", "lbbbLLbbd5", "lbbbbbbbd5", "Ldddddddd4", "Ssssssss44"] },
  { id: 'relic_vial', name: 'Elixir Vial', pal: ofPal(RR.glass, ramp('#4d94e6'), RR.leather), rows: ["..4444..", "..3333..", "..lbbd..", ".hl..bd.", ".l....d.", ".lhhhhd.", ".lHLLLd.", ".lLhLBd.", ".lLLLBd.", ".lLLhBd.", ".lLLBBd.", "..LBBd..", "...dd..."] },
  { id: 'relic_sprig', name: 'Bottled Sprig', pal: ofPal(RR.glass, ramp('#66b352'), ramp('#61422e')), rows: ["...4444...", "...3333...", "..lbbbbd..", ".l..H...d.", "l..HLB..bd", "l.HL.BS.bd", "l..LBS..bd", "l...BS..bd", "l...S...bd", "l1111111bd", ".b22222bd.", "..dddddd.."] },
  { id: 'relic_hourglass', name: 'Hourglass', pal: ofPal(RR.bronze, RR.glass, ramp('#ebcc85')), rows: ["....ss....", "hllllllllb", "bddddddddd", ".BL5555LB.", ".BL5555LB.", "..BL55LB..", "...B55B...", "...B55B...", "..BL..LB..", ".BL.55.LB.", ".BL5555LB.", "hllllllllb", "bddddddddd"] },
  { id: 'relic_mortar', name: 'Mortar and Pestle', pal: ofPal(ramp('#b8b3a8'), ramp('#9e7047')), rows: [".......HH.", "......HLB.", ".....HLB..", "....HLB...", "...HLB....", "hllLBllllb", "bddSSddddd", "lbbbbbbbbd", ".lbbbbbbd.", "..lbbbbd..", "...dddd...", "..lbbbdd.."] },
  { id: 'relic_fork', name: 'Tuning Fork', pal: ofPal(ramp('#c7ccdb'), RR.leather), rows: ["..hl..hl..", "..hb..lb..", "..lb..lb..", "..lb..lb..", "..lb..bd..", "..lb..bd..", "..lbbbbd..", "...lbbd...", "....bd....", "...SBBS...", "....bd....", "....bd....", "...lbbd...", "....dd...."] },
  { id: 'relic_lodestone', name: 'Lodestone', pal: ofPal(ramp('#70667a'), ramp('#b8b8c2')), rows: ["....HH....HL.", "....LB...HL..", "....LBhl.LB..", "...hllllbbd..", "..hlllllbbbd.", "HLhllllbbbbd.", "HBlllbbbbbdd.", "..lllbbbbddLH", "..lbbbbbdddBH", "...bbbbddds..", "....bdLBss...", "......LB.....", "......HH....."] },
  { id: 'relic_salt', name: 'Salt Phial', pal: ofPal(RR.glass, RR.leather, ramp('#edebe0')), rows: ["...SBBS...", "...SBBS...", "...lbbd...", "..hlbbbd..", ".hl5555bd.", "hl566665bd", "hl566665bd", "l55555554d", "l44444443d", "b44444443s", ".bdddddds.", "..ssssss.."] },
  { id: 'relic_sulphur', name: 'Sulphur Tin', pal: ofPal(RR.iron, ramp('#ebc733')), rows: ["....SS....", ".llllllll.", "hlllllllld", "dddddddddd", "bbbbLLbbbd", "bbbLBBLbbd", "bbLBBBBLbd", "bbbbBBbbbd", "sbbBBBBbbs", "sbbbBBbbbs", "sbbbbbbbbs", ".ssssssss."] },
  { id: 'relic_mercury', name: 'Mercury Flask', pal: ofPal(RR.glass, ramp('#ccd1e0'), RR.leather), rows: ["....44....", "....33....", "....lb....", "...lbbd...", "..lb..bd..", ".lb....bd.", "lb......bd", "lHHLLLLLBd", "lLLBBBBBSd", ".bLBBBBSd.", "..bdSSdd..", "...dddd..."] },
  { id: 'relic_key', name: 'Iron Key', pal: ofPal(RR.ironLt), rows: ["..hlllb..", ".hlbbbbd.", "hlb...bds", "lb.....ds", "hlb...bds", ".lbbbbdd.", "...lbd...", "...lbd...", "...lbdbb.", "...lbdds.", "...lbd...", "...lbdbb.", "...lbddd.", "....ss..."] },
];
// HipRelic: centred on torso-local +RelicHipX (x 31 here; an odd grid gets a transparent column on the right),
// its outlined top on the pouch's, so every grid's top row is y 64 and a taller one hangs lower. Returns layers:
// a Black Diamond relic comes from relics-bd.js (the game's own sprites, outline baked in), the Gemini Saya as
// two - the near sheath over the body (Trinket), the far one behind it and over the cape (TrinketBack).
function relicLayers(id) {
  const bd = (typeof BD_RELICS !== 'undefined' ? BD_RELICS : []).filter(r => r.id === id);
  if (bd.length) return bd.map(r => {
    const l = L(r.layer === 'TrinketBack' ? 0.5 : 8.3); l.outline = false;
    r.rows.forEach((row, y) => { for (let x = 0; x * 6 < row.length; x++) { const h = row.substr(x * 6, 6); if (h !== '......') l.set(r.x0 + x, r.y0 + y, '#' + h); } });
    return l;
  });
  const r = RELICS.find(x => x.id === id) || RELICS[2], l = L(8.3);
  const w = r.rows[0].length + (r.rows[0].length % 2), x0 = 31 - w / 2;
  r.rows.forEach((row, y) => [...row].forEach((ch, x) => { if (ch !== '.') l.set(x0 + x, 64 + y, r.pal[ch]); }));
  return [l];
}
const BD_RELIC_LIST = () => { const seen = new Set(), out = []; for (const r of (typeof BD_RELICS !== 'undefined' ? BD_RELICS : [])) if (!seen.has(r.id)) { seen.add(r.id); out.push({ id: r.id, name: r.name }); } return out; };
// NOT WORN WITH A SHEATH (the user's call, 2026-11-08): a saya hangs from the same hip (Zanmato covers the tome;
// Gemini's far sheath sits behind the body, flipping the stack), so the tome and the sayas are incompatible.
// Every other relic, standard or Black Diamond, stacks with it.
const TOME_EXCLUDES = ['zanmato_saya', 'gemini_saya'];
const tomeAllowed = relic => !TOME_EXCLUDES.includes(relic);
const tome = {
  id: 'tome', backKind: 'under', name: 'Chained Tome', where: "Belt, the relic's hip, behind the relic",
  // ON THE RELIC'S HIP (the user's call, 2026-11-08: the only hip accessory so far): the far hip, hung behind
  // the relic, lower and further out, so its outer edge and bottom fan out from behind whichever relic is worn
  front(st) {
    const relic = st.relic && st.relic !== 'off' ? relicLayers(st.relic) : [];
    if (!tomeAllowed(st.relic)) return relic;
    const t = tomeLayer(st, 36, 63, 1, false); t.depth = 8.25;
    return [t, ...relic];
  },
  back(st) { return tomeAllowed(st.relic) ? [tomeLayer(st, mirrorX(36), 63, -1, true)] : []; },
};

// ---------------------------------------------------------------- 10. WRIST WRAPS
function wrapsFor(S, st, depthWrap) {
  const r = st.pal === 'Element' ? ramp(ELEMENT[st.el], { lift: 0.3, shade: 0.4 }) : CLOTH[st.pal || 'Crimson'];
  const l = L(depthWrap);
  const band = sideRect(S, -1, 50, 10, 60);
  l.fill(band, (x, y) => { const xx = S === NEAR ? x : 37 - x; return tone(r, ((xx + y) % 4 + 4) % 4 < 2 ? (xx < 2 ? 4 : 3) : (xx > 7 ? 1 : 2)); });
  // two tails from the outer edge of the wrist: hanging and swaying, or streaming out behind on a dash
  const x0 = scx(S, -0.5), y0 = 58, dirOut = S === NEAR ? -1 : 1;
  [[24, 0], [18, 1.7]].forEach(([len, ph], ti) => {
    for (let s = 0; s <= len; s += 0.5) {
      const wob = Math.sin(st.t * (st.walk ? 7 : 3.2) - s * 0.3 + ph) * Math.min(1, s / 6) * (st.walk ? 2.2 : 1.3);
      const hx = x0 + dirOut * (s * 0.18 + ti * 1.5) + wob, hy = y0 + s;
      const sx2 = x0 + dirOut * (s + 1 + ti), sy2 = y0 + s * 0.2 + Math.sin(st.t * 14 - s * 0.5 + ph) * Math.min(1, s / 5) * 2 - ti * 2;
      const x = lerp(hx, sx2, st.dash), y = lerp(hy, sy2, st.dash), w = s < len - 6 ? 3 : 2;
      for (let k = 0; k < w; k++) { const px = Math.floor(x) + k * (st.dash > 0.5 ? 0 : 1), py = Math.floor(y) + k * (st.dash > 0.5 ? 1 : 0); l.set(px, py, tone(r, k === 0 ? 4 - ti : 2 - ti)); }
    }
  });
  if (S === FAR) l.far = true;
  return l;
}
const wraps = {
  id: 'wraps', backKind: 'arm', name: 'Wrist Wraps', where: 'Both forearms, over the gloves',
  front(st) { return [wrapsFor(NEAR, st, 13.5), wrapsFor(FAR, st, 2.5)]; },
  back(st) { return [wrapsFor(NEAR, st, 13.5), wrapsFor(FAR, st, 2.5)]; },
};

// ---------------------------------------------------------------- 19. ORBITING STONES
function stones(st, glows, back) {
  const front = L(back ? 99 : 16), behind = L(-1); behind.far = true; front.outR = 2; behind.outR = 2;
  for (let i = 0; i < 3; i++) {
    const a = st.t * 0.95 + i * Math.PI * 2 / 3, nearCam = Math.sin(a) > 0;
    const cx = 19 + Math.cos(a) * 28 * (back ? -1 : 1), cy = 46 + Math.sin(a) * 7 + Math.sin(st.t * 2 + i * 2) * 1.5;
    const spin = Math.abs(Math.cos(st.t * 2.2 + i)), hw = Math.max(1, Math.round(spin * 4.5)), H = 8;
    const l = nearCam ? front : behind;
    const m = poly([[cx, cy - H], [cx + hw, cy - 1], [cx + hw * 0.6, cy + H * 0.7], [cx - hw * 0.6, cy + H * 0.7], [cx - hw, cy - 1]]);
    l.fill(m, (x, y) => tone(AM.stone, y < cy - H + 3 ? 5 : x + 0.5 < cx ? 4 : 2));
    glows.push({ x: cx, y: cy, r: 12, c: '#ff3a3a', a: nearCam ? 0.42 : 0.18 });
  }
  return [front, behind];
}
const orbit = {
  id: 'stones', backKind: 'fixed', name: 'Orbiting Stones', where: 'Around the waist',
  front(st, glows) { return stones(st, glows, false); },
  back(st, glows) { return stones(st, glows, true); },
};

const ACCESSORIES = [bandolier, harness, collar, banner, hourglassPack, tome, wraps, orbit];

// THE BACK VIEW WITH THE SET ON, in the game's turned-away order (PrimitiveCharacterRig.FacingAwayOrder):
// torso, belt and leg pieces still draw their FRONT art (no armour has a back view yet); the arms and
// gloves, then both pauldrons, then the CAPE over all of them (Back, rank 22), the drape over the cape
// (BackOver), the head with the hair's back, and the helm's back (GearItem.HelmBack: bake.js BACKS/backOf).
const BACK_RANK = { cape: 20, drape: 20.5, head: 22, helm: 23 };
function backBodyLayers() {
  const Lb = bodyLayers(), head = Lb.head;
  for (const [k, c] of [...head.c]) {
    const x = KX(k), y = KY(k);
    if (y < 26 && c !== BODY_PAL.a) head.c.set(k, dome(AM.hair, 14, 4, 22, 20)(x, y));
  }
  head.depth = BACK_RANK.head;
  return Object.values(Lb);
}
function backSetLayers(set) {
  const out = backBodyLayers();
  if (!set) return out;
  for (const slot of SLOTS) {
    if (!set[slot] || slot === 'Head') continue;
    for (const l of set[slot]()) {
      if (slot === 'Back') l.depth = l.depth === DEPTH.back ? BACK_RANK.cape : BACK_RANK.drape;
      else if (slot === 'Shoulders') l.depth = l.far ? 18 : 19;
      else if (slot === 'Gloves' || l.depth === DEPTH.nearSleeve || l.depth === DEPTH.farSleeve) l.depth = 15 + (l.far ? 0 : 1) + l.depth / 100;
      out.push(l);
    }
  }
  if (set.Head) {
    const ls = set.Head(), back = BACKS[set.id] ? flatten(BACKS[set.id]()) : backOf(ls);
    const helm = L(BACK_RANK.helm); for (const [k, c] of back) helm.c.set(k, c);
    if (ls.every(l => l.outR && l.outR < OUT_R)) helm.outR = ls[0].outR;
    out.push(helm);
  }
  return out;
}
// WHERE EACH SITS FROM BEHIND (the user's call, 2026-11-08): only the collar, the hourglass and the stones
// are worn over a cape or cloak; everything else goes under it. backKind:
//   under    with the torso, under the cape (straps, the tome)
//   arm      over the gloves, still under the cape (the wraps)
//   over     over the cape, under the head and helm (the collar)
//   overTop  over the cape AND the helm's back (the hourglass and its finials)
//   pole     under the cape on the back, but what stands above the shoulders draws over the head (the banner)
//   fixed    sorts itself (the stones: past the head on the near half of the orbit, behind the body on the far)
const BACK_KIND = { under: 8.6, arm: 16.5, over: 21, overTop: 24 };
function backRank(acc, layers) {
  if (acc.backKind === 'fixed') return layers;
  if (acc.backKind === 'pole') {
    const out = [];
    for (const l of layers) {
      const lo = L(8.6), hi = L(24);
      for (const [k, c] of l.c) (KY(k) < 29 ? hi : lo).c.set(k, c);
      out.push(lo, hi);
    }
    return out;
  }
  const d = BACK_KIND[acc.backKind];
  layers.forEach((l, i) => { if (l.depth >= 0) l.depth = d + i / 100; });
  return layers;
}
if (typeof module !== 'undefined') module.exports = { TOME_EXCLUDES, tomeAllowed, RELICS, relicLayers, BD_RELIC_LIST, tomeLayer, ACCESSORIES, backBodyLayers, backSetLayers, backRank };
