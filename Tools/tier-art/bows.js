// ================================================================ bows: 79 rows, the grip at row 39, the string on the
// LEFT (the archer's side) and the stave bowing out to the right, as the Hunting Bow
const BOW_H = 79, BOW_CY = 39;
const U_OF = y => (y - BOW_CY) / BOW_CY;
// a stave from its centreline xc(u) and thickness th(u); returns the mask plus a per-row table
function stave(xc, th, u0 = -1, u1 = 1) {
  const pts = []; for (let y = 0; y < BOW_H; y++) { const u = U_OF(y); if (u < u0 || u > u1) continue; pts.push([xc(u), y + 0.5]); }
  const m = new Mask();
  for (let i = 0; i < pts.length; i++) {
    const [x, y] = pts[i], u = U_OF(y - 0.5), r = th(u) / 2;
    m.union(ell(x, y, r, 0.75));
    if (i) { const [px, py] = pts[i - 1]; const st = Math.ceil(Math.abs(x - px) * 2); for (let s = 1; s < st; s++) m.union(ell(px + (x - px) * s / st, py + (y - py) * s / st, r, 0.75)); }
  }
  return m;
}
// the string: a chord down the stave's leftmost reach
function bowString(m) {
  const sp = spans(m); let sx = 1e9;
  for (const [y, [a]] of sp) if (Math.abs(U_OF(y)) > 0.5) sx = Math.min(sx, a);
  const ys = [...sp].filter(([y, [a]]) => a === sx && Math.abs(U_OF(y)) > 0.5).map(([y]) => y);
  const yT = Math.max(...ys.filter(y => y < BOW_CY)), yB = Math.min(...ys.filter(y => y > BOW_CY));
  const s = new Layer(5); s.outline = false;
  for (let y = yT; y <= yB; y++) if (!m.has(sx - 1, y)) s.set(sx - 1, y, '#e9e2cc');
  return s;
}
const staveSh = (r, bias = 0) => (x, y, m) => { const sp = spans(m).get(y); const w = sp[1] - sp[0] + 1; const t = (x - sp[0]) / Math.max(1, w - 1); return tone(r, (w <= 2 ? (x === sp[0] ? 4 : 2) : t < 0.34 ? 4 : t < 0.7 ? 3 : 2) + bias); };
const rowsWhere = (m, test) => { const o = new Mask(); for (const [x, y] of m.pts()) if (test(U_OF(y), x, y)) o.add(x, y); return o; };

function hopliteBow() {
  // the Scythian double curve: limbs bow out, the grip sits back between them, the tips flick forward
  const xc = u => 3 + 7 * (1 - u * u) + 4.5 * Math.sin(Math.PI * u) ** 2 - 3 * Math.exp(-((u / 0.15) ** 2)) + 4 * (Math.max(0, Math.abs(u) - 0.86) / 0.14) ** 2;
  const m = stave(xc, u => 3.6 - 1.6 * Math.abs(u));
  const l = WL(1).fill(m, staveSh(MW.horn));
  l.over(rowsWhere(m, u => Math.abs(u) > 0.84), staveSh(M.bronze));
  l.over(rowsWhere(m, u => Math.abs(u) < 0.09), (x, y, mm) => ((y % 3) ? staveSh(M.red)(x, y, mm) : tone(M.red, 1)));
  return [l, bowString(m)];
}
function wayfarerBow() {
  const xc = u => 2.5 + 8 * (1 - u * u);
  const m = stave(xc, u => 3.4 - 1.4 * Math.abs(u));
  const l = WL(1).fill(m, (x, y, mm) => { const sp = spans(mm).get(y); return x === sp[0] ? tone(M.linen, 3) : staveSh(MW.yew)(x, y, mm); });   // pale sapwood on the belly
  l.over(rowsWhere(m, u => Math.abs(u) > 0.95), tone(MW.horn, 3));
  l.over(rowsWhere(m, u => Math.abs(u) < 0.1), wrapSh(M.linen, 3));
  const tail = WL(1.5).fill(poly([[12, 40], [14, 40], [15.5, 50], [13.5, 51]]), (x, y) => tone(M.linen, y % 3 ? 3 : 2));
  return [l, tail, bowString(m)];
}
function athanorBow() {
  const xc = u => 2.5 + 9 * (1 - u * u);
  const m = stave(xc, u => 3.6 - 1.4 * Math.abs(u));
  const l = WL(1).fill(m, staveSh(M.bronze));
  // copper coil wound round the limbs
  l.over(rowsWhere(m, (u, x, y) => Math.abs(u) > 0.18 && Math.abs(u) < 0.8 && y % 5 === 0), tone(M.bronzeDk, 2));
  l.over(rowsWhere(m, u => Math.abs(u) < 0.1), wrapSh(M.leatherDk, 3));
  // a glass vial of elixir strapped to the back of the grip
  const vial = ell(15.5, 39, 2.6, 4.2);
  const v = WL(2).fill(vial, dome(MW.elixir, 15.5, 39, 2.6, 4.2)); v.fill(rect(15, 33, 16, 34), tone(M.bronze, 4));
  return [l, v, bowString(m)];
}
function hauberkBow() {
  // a steel warbow: steel limbs on a thin dark-wood belly, bright caps, a blue grip between steel bands
  const xc = u => 2.5 + 8.5 * (1 - u * u);
  const m = stave(xc, u => 3.6 - 1.2 * Math.abs(u));
  const l = WL(1).fill(m, (x, y, mm) => { const sp = spans(mm).get(y); return x === sp[0] ? tone(MW.wood, 2) : staveSh(M.steel)(x, y, mm); });
  l.over(rowsWhere(m, u => Math.abs(u) > 0.9), (x, y, mm) => tone(M.steel, x === spans(mm).get(y)[0] ? 4 : 5));
  for (let y = 4; y < BOW_H; y += 9) { if (Math.abs(U_OF(y)) < 0.16 || Math.abs(U_OF(y)) > 0.88) continue; l.over(rowsWhere(m, (u, x, yy) => yy === y), tone(M.steel, 1)); }
  l.over(rowsWhere(m, u => Math.abs(u) < 0.11), wrapSh(M.blue, 3));
  l.over(rowsWhere(m, u => Math.abs(Math.abs(u) - 0.13) < 0.025), tone(M.steel, 5));
  return [l, bowString(m)];
}
function kettleBow() {
  // a steel recurve: the tips curl forward past the string
  const xc = u => 3 + 9 * (1 - u * u) + 9 * Math.max(0, Math.abs(u) - 0.78) ** 2 / 0.048;
  const m = stave(xc, u => 3.6 - 1.4 * Math.abs(u));
  const l = WL(1).fill(m, staveSh(M.steel));
  l.over(rowsWhere(m, u => Math.abs(u) < 0.1), bandsSh(M.forest, M.steel, 4));
  for (let y = 6; y < BOW_H; y += 6) { if (Math.abs(U_OF(y)) < 0.14) continue; const sp = spans(m).get(y); if (sp) l.dot(Math.round((sp[0] + sp[1]) / 2), y, tone(M.steel, 5)); }
  return [l, bowString(m)];
}
function sentinelBow() {
  // A: lamed limbs ending in fan-winged tips (three lames each, as the knees), a navy grip with a steel boss
  const xc = u => 2.5 + 9 * (1 - u * u);
  const m = stave(xc, u => 4 - 1.6 * Math.abs(u));
  const l = WL(1).fill(m, staveSh(M.steel));
  for (let y = 7; y < BOW_H - 7; y += 5) { if (Math.abs(U_OF(y)) < 0.12) continue; const sp = spans(m).get(y); if (sp) { l.line(sp[0], y, sp[1], y, tone(M.steel, 1)); l.line(sp[0], y + 1, sp[1], y + 1, tone(M.steel, 4)); } }
  l.over(rowsWhere(m, u => Math.abs(u) < 0.1), wrapSh(M.navy, 3));
  const fanT = U(...[0, 1, 2].map(i => poly([[4, 4 + 4.2 * i], [17 - 2.2 * i, 0 + 4.6 * i], [17 - 2.2 * i, 2.8 + 4.6 * i], [4, 7 + 4.2 * i]])));
  const fanB = new Mask(); for (const [x, y] of fanT.pts()) fanB.add(x, BOW_H - 1 - y);
  const f = WL(0.5).fill(U(fanT, fanB), plate(M.steel));
  const boss = ell(13.5, 39.5, 2.6, 2.6); const b = WL(2).fill(boss, ball(M.steel, boss)); b.dot(13, 39, tone(M.navy, 4));
  return [f, l, b, bowString(m)];
}
function sentinelBowB() {
  // B: heavy lamed limbs with a raised ridge, and a navy-enamelled steel heater plate on the back of the grip
  const xc = u => 2.5 + 9.5 * (1 - u * u);
  const m = stave(xc, u => 5 - 2.4 * Math.abs(u));
  const l = WL(1).fill(m, staveSh(M.steel));
  for (let y = 3; y < BOW_H; y += 6) { if (Math.abs(U_OF(y)) < 0.14) continue; const sp = spans(m).get(y); if (sp) { l.line(sp[0], y, sp[1], y, tone(M.steel, 1)); l.line(sp[0], y + 1, sp[1], y + 1, tone(M.steel, 5)); } }
  l.over(rowsWhere(m, u => Math.abs(u) < 0.11), wrapSh(M.navy, 3));
  const plateM = poly([[12, 32], [20, 32], [20, 40], [16, 47], [12, 40]]);
  const p = WL(2).fill(plateM, plate(M.steel));
  p.over(poly([[13.5, 33.5], [18.5, 33.5], [18.5, 39.5], [16, 44], [13.5, 39.5]]), (x, y) => tone(M.navy, x < 16 ? 4 : 3));
  return [l, p, bowString(m)];
}
function solBow() {
  // A: a plume of five gold feathers rising from each tip, as the helm's crest; a rayed sun on the back of the grip
  const xc = u => 2.5 + 9 * (1 - u * u);
  const m = stave(xc, u => 3.6 - 1.4 * Math.abs(u));
  const l = WL(1).fill(m, staveSh(M.gold));
  l.over(rowsWhere(m, u => Math.abs(u) < 0.1), bandsSh(M.white, M.gold, 4));
  const crestT = U(...[0, 1, 2].map(i => poly([[4, 5 + 4 * i], [11, 1 + 4.3 * i], [17 - 2 * i, 0 + 4.6 * i], [11.5, 3.6 + 4.3 * i], [4, 8 + 4 * i]])));
  const crestB = new Mask(); for (const [x, y] of crestT.pts()) crestB.add(x, BOW_H - 1 - y);
  const c = WL(0.5).fill(U(crestT, crestB), (x, y) => tone(M.gold, ((x * 2 + y) % 4) ? 4 : 2));
  const rays = polar0(15, 39.5, (r, a) => r < 3.2 + 2.2 * (1 - Math.abs(((a * 8 / (2 * Math.PI)) % 1) - 0.5) * 2));
  const s = WL(2).fill(rays, dome(M.gold, 15, 39.5, 5.4, 5.4)); s.fill(ell(15, 39.5, 1.8, 1.8), tone(M.white, 4));
  return [c, l, s, bowString(m)];
}
function solBowB() {
  // B: the bow as a sunrise: short gold rays all down the back of the limbs, a white grip and a gold sun disc
  const xc = u => 2.5 + 9 * (1 - u * u);
  const m = stave(xc, u => 3.6 - 1.4 * Math.abs(u));
  const l = WL(1).fill(m, staveSh(M.gold));
  l.over(rowsWhere(m, u => Math.abs(u) < 0.1), bandsSh(M.white, M.gold, 4));
  const rays = new Mask();
  for (let y = 6; y < BOW_H - 6; y += 6) { const u = U_OF(y); if (Math.abs(u) < 0.16) continue; const x = xc(u) + 0.8, len = 8 * (1 - Math.abs(u)) + 3; rays.union(poly([[x, y - 2.2], [x + len, y + 0.5], [x, y + 2.2]])); }
  const r = WL(0.5).fill(rays, (x, y) => tone(M.gold, (y % 6) < 3 ? 4 : 2));
  const sun = ell(15, 39.5, 4.2, 4.2);
  const sd = WL(2).fill(sun, ball(M.gold, sun)); sd.fill(ell(15, 39.5, 2, 2), tone(M.white, 4));
  return [r, l, sd, bowString(m)];
}
// polar about an arbitrary centre (a = 0 up, clockwise)
function polar0(cx, cy, test, R = 8) {
  const mm = new Mask();
  for (let y = Math.floor(cy - R); y < cy + R; y++) for (let x = Math.floor(cx - R); x < cx + R; x++) {
    const dx = x + 0.5 - cx, dy = y + 0.5 - cy; let a = Math.atan2(dx, -dy); if (a < 0) a += 2 * Math.PI;
    if (test(Math.hypot(dx, dy), a)) mm.add(x, y);
  }
  return mm;
}
function cataphractBow() {
  // a horse-archer's reflex bow: stiff gold ears angled forward at the tips
  const xc = u => { const a = Math.abs(u); return a < 0.72 ? 4 + 8 * Math.sin(Math.PI * (0.72 - a) / 1.44 + 0.2) - 3 * Math.exp(-((u / 0.16) ** 2)) : 4 + 8 * Math.sin(0.2) - 0.0 + (a - 0.72) / 0.28 * 9; };
  const m = stave(xc, u => (Math.abs(u) > 0.72 ? 2.6 : 3.8 - 1.2 * Math.abs(u)));
  const l = WL(1).fill(m, staveSh(M.crimson));
  l.over(rowsWhere(m, u => Math.abs(u) > 0.7), staveSh(M.gold));
  for (const u0 of [0.2, 0.35, 0.5]) for (const sgn of [-1, 1]) l.over(rowsWhere(m, u => Math.abs(u - sgn * u0) < 0.02), tone(M.gold, 4));
  l.over(rowsWhere(m, u => Math.abs(u) < 0.08), bandsSh(M.crimson, M.gold, 3));
  // crimson tassels hanging from the grip
  const tas = WL(0.5).fill(U(rect(4, 42, 5, 52), rect(6, 42, 7, 49)), (x, y) => tone(M.crimson, (x % 2) ? 2 : 3));
  return [tas, l, bowString(m)];
}
function wingwardBow() {
  // each limb a gold wing: feathers along its back, longest at the shoulder of the limb, teal beneath
  const xc = u => 2.5 + 9 * (1 - u * u);
  const m = stave(xc, u => 3.4 - 1.2 * Math.abs(u));
  const l = WL(1).fill(m, staveSh(M.gold));
  l.over(rowsWhere(m, u => Math.abs(u) < 0.1), bandsSh(M.teal, M.gold, 4));
  const f = WL(0.5);
  for (const sgn of [-1, 1]) for (let i = 9; i >= 0; i--) {
    const u = sgn * (0.12 + i * 0.075); const y = BOW_CY + u * BOW_CY; const x = xc(u) + 0.5; const len = 11 - i * 0.95;
    const fm = stroke([[x, y], [x + len * 0.8, y + sgn * len * 0.75]], 1.25);
    f.fill(fm, (px, py) => { const d = Math.hypot(px + 0.5 - x, py + 0.5 - y) / len; return d < 0.35 ? tone(M.teal, 3) : tone(M.gold, sgn < 0 ? 4 : 3); });
    const tipSide = new Mask(); for (const [px, py] of fm.pts()) if (!fm.has(px, py + sgn)) tipSide.add(px, py);
    f.fill(tipSide, (px, py) => Math.hypot(px + 0.5 - x, py + 0.5 - y) / len < 0.35 ? tone(M.teal, 1) : tone(M.gold, 1));
  }
  return [f, l, bowString(m)];
}

const BOWS = {
  hoplite: { name: 'Hoplite Toxon', draw: hopliteBow, blurb: 'A double-curved horn bow: the limbs bow out, the grip sits back between them in red wrap, and bronze tips flick forward.' },
  wayfarer: { name: 'Wayfarer Longbow', draw: wayfarerBow, blurb: 'A plain yew longbow, pale sapwood on the belly, horn nocks, and a linen grip with its tail hanging.' },
  athanor: { name: 'Athanor Coil', draw: athanorBow, blurb: 'Bronze limbs wound with copper coil, and a glass vial of elixir strapped to the back of the grip.' },
  hauberk: { name: 'Hauberk Warbow', draw: hauberkBow, blurb: 'Now steel: steel limbs over a thin dark-wood belly, banded every few rows, bright caps, a blue grip between steel bands.' },
  kettle: { name: 'Kettle Recurve', draw: kettleBow, blurb: 'A steel recurve whose tips curl forward past the string, rivets down the limbs and a green grip in steel rings.' },
  sentinel: { name: 'Sentinel Lames', draw: sentinelBowB, blurb: 'Heavy limbs of overlapping steel lames, and a steel heater plate enamelled navy on the back of the navy grip.' },  sol: { name: 'Sol Arc', draw: solBowB, blurb: 'A sunrise: short gold rays all down the back of both limbs, a white grip and a gold sun disc.' },  cataphract: { name: 'Cataphract Reflex', draw: cataphractBow, blurb: 'A horse-archer\'s reflex bow: crimson limbs banded in gold, stiff gold ears angled forward, crimson tassels at the grip.' },
  wingward: { name: 'Wingward Pinion', draw: wingwardBow, blurb: 'Each limb a gold wing, its feathers swept back along the limb, longest near the grip, teal beneath; a teal grip.' },
};
