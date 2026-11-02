// ================================================================ tier weapons: materials beyond the armour's
const MW = {
  iron: ramp('#7b8089', { sheen: '#e6ebf4', lift: 0.38 }),
  blade: ramp('#b4bac6', { sheen: '#ffffff', lift: 0.45 }),
  elixir: ramp('#3fae7a', { sheen: '#e0ffe8', lift: 0.45 }),
  wood: ramp('#8a5a32', { sheen: '#f0c890', lift: 0.30 }),
  yew: ramp('#a8733e', { sheen: '#ffe0a8', lift: 0.30 }),
  horn: ramp('#4a3a2c', { sheen: '#c8a888', lift: 0.30 }),
};
const SWORD_W = 16, SWORD_H = 79;
const GRIP = (y0) => rect(6, y0, 9, y0 + 11);   // every greatsword grip: 12 rows x 4

// ---------------------------------------------------------------- BRONZE
function hopliteSword() {
  const b = WL(1).fill(bladeMask([[0, 1], [10, 2], [24, 3], [38, 4], [47, 4], [53, 3], [56, 2]]), bladeSh(M.bronze, 'ridge'));
  const guard = U(rect(2, 57, 13, 59), rect(2, 56, 3, 56), rect(12, 56, 13, 56));
  const g = WL(3).fill(guard, plate(M.bronze));
  const grip = WL(2).fill(GRIP(60), bandsSh(M.red, M.bronze, 4));
  const pm = U(ell(8, 75.5, 4, 2.6), rect(6, 72, 9, 73));
  const p = WL(2).fill(pm, ball(M.bronze, pm));
  return [b, grip, p, g];
}
function wayfarerSword() {
  const bm = bladeMask([[0, 1], [2, 3], [5, 4], [58, 4]]);
  const b = WL(1).fill(bm, bladeSh(MW.iron, 'fuller', { from: 8, to: 44 }));
  // a linen rag wound round the ricasso, its tail hanging past the guard
  const rag = WL(4).fill(U(rect(3, 46, 12, 57), rect(4, 45, 11, 45)), wrapSh(M.linen, 3));
  const tail = WL(4.5).fill(poly([[11, 52], [13.5, 51], [15, 64], [13, 66]]), (x, y) => tone(M.linen, y % 3 ? 3 : 2));
  const g = WL(3).fill(U(rect(1, 59, 14, 60), rect(0, 59, 0, 60).minus(new Mask()), rect(2, 61, 13, 61)), plate(MW.iron));
  const grip = WL(2).fill(GRIP(62), wrapSh(M.leather, 4));
  const pm = ell(8, 76, 3, 2.6);
  const p = WL(2).fill(pm, ball(MW.iron, pm));
  return [b, grip, p, g, tail, rag];
}
function athanorSword() {
  const bm = bladeMask([[0, 1], [5, 3], [10, 4], [54, 4]]);
  const b = WL(1).fill(bm, bladeSh(M.bronze, 'flat'));
  // a glass channel of elixir down the blade, bubbles rising
  b.over(rect(7, 12, 8, 53), (x, y) => tone(MW.elixir, x === 7 ? 4 : 3));
  b.over(rect(6, 12, 6, 53), tone(M.bronze, 1)); b.over(rect(9, 12, 9, 53), tone(M.bronze, 4));
  for (const [x, y] of [[7, 20], [8, 27], [7, 33], [8, 41], [7, 47], [8, 15]]) b.dot(x, y, tone(MW.elixir, 5));
  // the guard is a retort: a bronze ring round a glass bulb
  const ringM = ring(8, 58, 6, 4, 3.6, 2.4);
  const g = WL(3).fill(ringM, ball(M.bronze, ringM));
  const bulb = ell(8, 58, 4, 2.4);
  g.fill(bulb, (x, y) => tone(MW.elixir, y < 57 ? 4 : y > 58 ? 2 : 3));
  g.dot(6, 57, tone(MW.elixir, 5)); g.dot(7, 57, tone(MW.elixir, 5));
  const grip = WL(2).fill(GRIP(62), bandsSh(M.leatherDk, M.bronze, 4));
  // a stoppered flask for a pommel
  const p = WL(2).fill(rect(7, 74, 8, 74), tone(M.bronze, 3));
  const fl = ell(8, 76.5, 3, 2.2); p.fill(fl, (x, y) => tone(MW.elixir, y < 76 ? 4 : 3)); p.dot(6, 76, tone(MW.elixir, 5));
  return [b, grip, p, g];
}
// ---------------------------------------------------------------- SILVER
function hauberkSword() {
  const b = WL(1).fill(bladeMask([[0, 1], [4, 3], [8, 4], [59, 4]]), bladeSh(M.steel, 'fuller', { from: 10, to: 56 }));
  const gm = U(rect(1, 60, 14, 62), rect(1, 63, 2, 63), rect(13, 63, 14, 63));
  const g = WL(3).fill(gm, plate(M.steel));
  const grip = WL(2).fill(GRIP(63), wrapSh(M.blue, 3));
  const pm = ell(8, 77, 3, 2.1);
  const p = WL(2).fill(pm, ball(M.steel, pm)); p.over(rect(7, 76, 8, 77), tone(M.blue, 4)); p.dot(8, 77, tone(M.blue, 2));
  return [b, grip, p, g];
}
function kettleSword() {
  // a flamberge: both edges wave together, the blade snaking a texel either way of the centre
  const fm = new Mask();
  for (let y = 0; y <= 52; y++) {
    const hw = y < 2 ? 1 : y < 5 ? 2 : 3, sh = y < 6 ? 0 : Math.round(Math.sin((y - 6) * Math.PI / 5));
    for (let x = 8 - hw + sh; x < 8 + hw + sh; x++) fm.add(x, y);
  }
  const b = WL(1).fill(fm, bladeSh(M.steel, 'ridge'));
  // a leather ricasso with parrying lugs
  const ric = WL(2).fill(rect(6, 53, 9, 58), wrapSh(M.leather, 3));
  const lugs = WL(3).fill(U(poly([[6, 53], [6, 56], [3, 54]]), poly([[10, 53], [10, 56], [13, 54]])), plate(M.steel));
  // a crossbar whose ends curl the opposite ways (an S)
  const gm = U(rect(2, 59, 13, 61), rect(1, 56, 2, 59), rect(13, 61, 14, 64));
  const g = WL(3).fill(gm, plate(M.steel));
  const grip = WL(2).fill(GRIP(62).minus(rect(0, 62, 15, 61)), bandsSh(M.forest, M.steel, 4));
  const pm = ell(8, 76, 3, 2.8);
  const p = WL(2).fill(pm, ball(M.steel, pm)); p.dot(7, 75, tone(M.steel, 5)); p.dot(8, 76, tone(M.steel, 1));
  return [b, ric, grip, p, lugs, g];
}
function sentinelSword() {
  const b = WL(1).fill(bladeMask([[0, 1], [3, 3], [7, 4], [56, 4]]), bladeSh(M.steel, 'ridge'));
  // fan-winged guard: two arms of three lames sweeping up and out, as the knees
  const armL = poly([[1, 51], [3, 51], [7.5, 58], [7.5, 63], [5, 63], [1, 56]]);
  const gm = mirrorX(armL, SWORD_W);
  const g = WL(3).fill(gm, plate(M.steel));
  for (const y of [54, 57, 60]) { g.line(1, y, 7, y + 2, tone(M.steel, 1)); g.line(14, y, 8, y + 2, tone(M.steel, 1)); }
  const boss = ell(8, 60.5, 2.5, 2.5); g.fill(boss, ball(M.navy, boss));
  const grip = WL(2).fill(GRIP(63), wrapSh(M.navy, 3));
  const pm = poly([[8, 74.5], [11, 77], [8, 79.5], [5, 77]]);
  const p = WL(2).fill(pm, (x, y) => tone(M.steel, x < 8 ? (y < 77 ? 5 : 3) : (y < 77 ? 3 : 2)));
  return [b, grip, p, g];
}
// ---------------------------------------------------------------- GOLD
function solSword() {
  const b = WL(1).fill(bladeMask([[0, 1], [5, 3], [9, 4], [55, 4]]), bladeSh(MW.blade, 'ridge'));
  // a gold langet up the blade from the guard, ending in a chevron
  b.over(poly([[4, 55.5], [4, 47], [8, 43], [12, 47], [12, 55.5]]), (x, y, m) => tone(M.gold, x < 8 ? 4 : 3));
  b.over(poly([[6, 55.5], [6, 49], [8, 47], [10, 49], [10, 55.5]]), (x) => tone(MW.blade, x < 8 ? 4 : 3));
  // gold feathers fanning from a sun disc
  const fL = U(stroke([[6, 60], [1.5, 55]], 1), stroke([[6, 61], [1.5, 59.5]], 0.9), stroke([[6, 62], [2.5, 63]], 0.8));
  const fm = mirrorX(fL, SWORD_W);
  const g = WL(3).fill(fm, (x, y, m) => tone(M.gold, (x < 8 ? 4 : 3) - ((x + y) % 3 === 0 ? 1 : 0)));
  const sun = ell(8, 59.5, 3.5, 3.5);
  const s = WL(3.5).fill(sun, ball(M.gold, sun)); s.fill(ell(8, 59.5, 1.6, 1.6), tone(M.white, 4));
  const grip = WL(2).fill(GRIP(63), bandsSh(M.white, M.gold, 4));
  const pm = poly([[8, 74.5], [10.5, 76.5], [8, 79.4], [5.5, 76.5]]);
  const p = WL(2).fill(pm, ball(M.gold, pm));
  return [b, grip, p, g, s];
}
function cataphractSword() {
  const b = WL(1).fill(bladeMask([[0, 2], [2, 3], [5, 4], [56, 4]]), bladeSh(MW.blade, 'fuller', { from: 8, to: 48 }));
  // gold lamellar sleeve at the blade's base
  b.over(rect(4, 49, 11, 56), (x, y) => { const r = ((y - 49) % 3), c = ((x + (Math.floor((y - 49) / 3) % 2)) % 2); return tone(M.gold, r === 2 ? 1 : c ? 3 : 4); });
  // crimson-lacquered bar behind a gold mask with closed, engraved eyes
  const bar = WL(2.5).fill(rect(1, 58, 14, 60), (x, y) => (x <= 2 || x >= 13) ? tone(M.gold, 3) : tone(M.crimson, y === 58 ? 4 : 3));
  const face = poly([[5, 57], [11, 57], [11, 61], [9.5, 63.5], [6.5, 63.5], [5, 61]]);
  const f = WL(3).fill(face, ball(M.gold, face));
  f.dot(6, 59, tone(M.gold, 1)); f.dot(7, 60, tone(M.gold, 1)); f.dot(9, 59, tone(M.gold, 1)); f.dot(8, 60, tone(M.gold, 1));
  f.dot(8, 61, tone(M.gold, 2)); f.dot(7, 62, tone(M.gold, 1)); f.dot(8, 62, tone(M.gold, 1));
  const grip = WL(2).fill(GRIP(64), bandsSh(M.crimson, M.gold, 3));
  const pm = ring(8, 77.5, 2.5, 1.2, 1.8, 0.6);
  const p = WL(2).fill(U(pm, rect(7, 76, 8, 76)), tone(M.gold, 4)); p.over(rect(9, 76, 15, 79), tone(M.gold, 2));
  return [b, grip, p, bar, f];
}
function wingwardSword() {
  const b = WL(1).fill(bladeMask([[0, 1], [6, 3], [12, 4], [55, 4]]), bladeSh(MW.blade, 'fuller', { from: 14, to: 50 }));
  b.over(rect(7, 15, 8, 49), (x) => tone(M.teal, x === 7 ? 2 : 3));
  // two gold wings swept up the blade from the guard, teal under the feathers
  const wingL = poly([[7.5, 64], [7.5, 58], [5, 54], [2, 48], [0.5, 47], [1, 52], [2, 57], [4, 61.5]]);
  const wm = mirrorX(wingL, SWORD_W);
  const g = WL(3).fill(wm, (x, y, m) => {
    const L = x < 8, d = L ? x : 15 - x;
    const stripe = ((y + d * 2) % 4) === 0;
    return stripe ? tone(M.teal, 2) : tone(M.gold, (L ? 4 : 3) - (d < 2 ? 1 : 0));
  });
  const grip = WL(2).fill(GRIP(64), bandsSh(M.teal, M.gold, 4));
  const pm = poly([[5.5, 76], [10.5, 76], [8, 79.6]]);
  const p = WL(2).fill(U(pm, rect(6, 76, 9, 76)), ball(M.gold, U(pm, rect(6, 76, 9, 76))));
  return [b, grip, p, g];
}

const SWORDS = {
  hoplite: { name: 'Hoplite Xiphos', draw: hopliteSword, blurb: 'A leaf-shaped bronze blade, widest two thirds of the way down, on a short flared bar; a red grip ringed in bronze and a flat disc pommel.' },
  wayfarer: { name: "Wayfarer's Blade", draw: wayfarerSword, blurb: 'A plain iron blade with a squared tip and a fuller, a linen rag wound round its base with the tail hanging loose, a straight bar and a leather grip.' },
  athanor: { name: 'Athanor Retort', draw: athanorSword, blurb: 'Bronze with a glass channel of green elixir down the middle; the guard is a bronze ring round a glass bulb and the pommel a stoppered flask.' },
  hauberk: { name: 'Hauberk Longsword', draw: hauberkSword, blurb: 'The knightly cross: a fullered steel blade, a straight guard whose ends turn down, a blue grip and a wheel pommel with a blue boss.' },
  kettle: { name: 'Kettle Flamberge', draw: kettleSword, blurb: 'A wave-edged two-hander with a leather ricasso and parrying lugs, a crossbar whose ends curl opposite ways, a green grip in steel rings.' },
  sentinel: { name: 'Sentinel Bastion', draw: sentinelSword, blurb: 'A broad ridged blade on a fan-winged guard of three lames a side, as the knees, with a navy boss, navy grip and a faceted pommel.' },
  sol: { name: 'Sol Gladius', draw: solSword, blurb: 'A bright blade in a gold langet that ends in a chevron; gold feathers fan from a sun disc at the guard, over a white grip in gold bands.' },
  cataphract: { name: 'Cataphract Spatha', draw: cataphractSword, blurb: 'A round-tipped spatha in a gold lamellar sleeve; the guard is a gold mask with closed eyes on a crimson bar; crimson grip, ring pommel.' },
  wingward: { name: 'Wingward Blade', draw: wingwardSword, blurb: 'Two gold wings sweep up the blade from the guard, teal under the feathers; a teal fuller, a teal grip and a feather-tip pommel.' },
};
