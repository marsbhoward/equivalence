// ================================================================ discs: 28 tall, shape symmetric left-right (the
// off-hand copy is the same sprite, unmirrored), a hole with a grip bar across it
const DC = 14, DISC_H = 28;
// texels whose centre (polar about the disc centre; a = 0 up, clockwise) passes test(r, a)
function polar(test, R = 16) {
  const m = new Mask();
  for (let y = DC - R; y < DC + R; y++) for (let x = DC - R; x < DC + R; x++) {
    const dx = x + 0.5 - DC, dy = y + 0.5 - DC; const r = Math.hypot(dx, dy); let a = Math.atan2(dx, -dy); if (a < 0) a += 2 * Math.PI;
    if (test(r, a, dx, dy)) m.add(x, y);
  }
  return m;
}
const HOLE = () => ell(DC, DC, 6, 5);
const gripBar = (r = M.leather) => WL(0.5).fill(rect(DC - 6, DC - 1, DC + 5, DC), (x, y) => tone(r, y === DC - 1 ? 4 : 2));
// whole-disc light: a dome over the disc's full radius, so every band shades as one surface
const discLit = (r, R = 14, bias = 0) => dome(r, DC, DC, R, R, bias);
const deg = d => d * Math.PI / 180;
const angDist = (a, b) => { const d = Math.abs(a - b) % (2 * Math.PI); return Math.min(d, 2 * Math.PI - d); };

function hopliteDisc() {
  const all = ell(DC, DC, 14, 14).minus(HOLE());
  const l = WL(1).fill(all, discLit(M.bronze));
  l.over(ring(DC, DC, 11.5, 8.5), discLit(M.red, 14, 0.1));       // the painted field
  l.over(ring(DC, DC, 12.5, 11.5), discLit(M.bronze, 14, -0.25));   // a groove inside the rim
  return [gripBar(), l];
}
function wayfarerDisc() {
  const hole = ell(DC, DC, 7, 6);
  const all = ell(DC, DC, 14, 14).minus(hole);
  const l = WL(1).fill(all, (x, y) => { const r = Math.hypot(x + 0.5 - DC, y + 0.5 - DC); const v = discLit(MW.wood)(x, y); return (Math.round(r * 1.3) % 3 === 0) ? scale(v, 0.88) : v; });
  l.over(ring(DC, DC, 14, 12.8), discLit(MW.iron));                // an iron tyre
  // four linen bindings, N/E/S/W
  const bind = polar((r, a) => r > 6 && r < 13.2 && [0, 90, 180, 270].some(d => angDist(a, deg(d)) < 0.22));
  l.over(bind, (x, y) => tone(M.linen, ((x + y) % 2) ? 4 : 3));
  return [gripBar(M.linen), l];
}
function athanorDisc() {
  // a bronze ring with a glass coil of elixir, three stoppered flasks set into it (top, and either side below)
  const band = ring(DC, DC, 11, 7.5);
  const l = WL(1).fill(band, discLit(M.bronze));
  l.over(ring(DC, DC, 9.8, 8.8), discLit(MW.elixir, 14, 0.2));
  const v = WL(1.5);
  for (const d of [0, 120, 240]) {
    const a = deg(d), cx = DC + Math.sin(a) * 10.6, cy = DC - Math.cos(a) * 10.6;
    const bulb = ell(cx, cy, 3.1, 3.1);
    v.fill(bulb, dome(MW.elixir, cx, cy, 3.1, 3.1));
    const ncx = DC + Math.sin(a) * 13.4, ncy = DC - Math.cos(a) * 13.4;
    v.fill(ell(ncx, ncy, 1.3, 1.3), tone(M.bronze, 4));
  }
  return [gripBar(M.leatherDk), l, v];
}
function hauberkDisc() {
  const all = ell(DC, DC, 14, 14).minus(HOLE());
  const l = WL(1).fill(all, discLit(M.steel));
  const mailBand = ring(DC, DC, 12, 8.5);
  l.over(mailBand, (x, y) => { const v = discLit(M.steel)(x, y); return ((x + (y % 2)) % 2) ? scale(v, 0.68) : v; });
  l.over(ring(DC, DC, 8.5, 6.5, 8.5, 5.5), discLit(M.blue, 14, 0.15));
  return [gripBar(M.blue), l];
}
function kettleDisc() {
  // a scalloped rim of ten lobes, like the capelet's dags
  const body = polar((r, a) => { const k = 10; const lobe = Math.abs(((a * k / (2 * Math.PI)) % 1) - 0.5) * 2; return r < 11 + 3.1 * Math.sqrt(Math.max(0, 1 - lobe * lobe)); }).minus(HOLE());
  const l = WL(1).fill(body, discLit(M.steel));
  l.over(ring(DC, DC, 10.5, 7), discLit(M.forest, 14, 0.15));
  for (let i = 0; i < 8; i++) { const a = deg(22.5 + 45 * i); const x = Math.floor(DC + Math.sin(a) * 8.8), y = Math.floor(DC - Math.cos(a) * 8.8); l.dot(x, y, tone(M.steel, 5)); }
  return [gripBar(M.forest), l];
}
function sentinelDisc() {
  // an octagon of lames with a fan point at each quarter
  const oct = polar((r, a, dx, dy) => Math.max(Math.abs(dx), Math.abs(dy), (Math.abs(dx) + Math.abs(dy)) / Math.SQRT2) < 11.6);
  const pts = polar((r, a) => [0, 90, 180, 270].some(d => { const t = angDist(a, deg(d)); return r < 14.2 - t * 9 && r > 9; }));
  const all = U(oct, pts).minus(HOLE());
  const l = WL(1).fill(all, discLit(M.steel));
  l.over(polar((r) => r > 9.2 && r < 10.2), discLit(M.steel, 14, -0.35));
  l.over(ring(DC, DC, 8.5, 6.5, 8.5, 5.5), discLit(M.navy, 14, 0.2));
  return [gripBar(M.navy), l];
}
function solDisc() {
  const rays = polar((r, a) => { const k = 12; const f = Math.abs(((a * k / (2 * Math.PI)) % 1) - 0.5) * 2; return r < 10.5 + 3.8 * (1 - f); });
  const all = rays.minus(HOLE());
  const l = WL(1).fill(all, discLit(M.gold));
  l.over(ring(DC, DC, 10, 9), discLit(M.goldDk));
  l.over(ring(DC, DC, 8.5, 6.5, 8.5, 5.5), discLit(M.white, 14, 0.15));
  return [gripBar(M.white), l];
}
function cataphractDisc() {
  const all = ell(DC, DC, 13.5, 14).minus(HOLE());
  // gold lamellae in rings, lit as one dome
  const l = WL(1).fill(all, (x, y) => { const v = discLit(M.gold)(x, y); const r = Math.hypot(x + 0.5 - DC, y + 0.5 - DC); let a = Math.atan2(x + 0.5 - DC, -(y + 0.5 - DC)); const row = Math.floor(r / 2.4); const col = Math.floor((a + Math.PI) * (6 + row * 2) / Math.PI + row * 0.5); return (Math.floor(r * 10) % 24 < 4) || (col % 2 && (Math.floor(r * 10) % 24 < 9)) ? scale(v, 0.72) : v; });
  l.over(ring(DC, DC, 8.5, 6.5, 8.5, 5.5), discLit(M.crimson, 14, 0.2));
  // a gold mask at the crown of the ring, eyes closed
  const face = poly([[DC - 3, 0], [DC + 3, 0], [DC + 3, 4], [DC + 1.5, 6.5], [DC - 1.5, 6.5], [DC - 3, 4]]);
  const f = WL(2).fill(face, ball(M.gold, face));
  for (const [x, y] of [[11, 2], [12, 3], [16, 2], [15, 3], [13, 5], [14, 5]]) f.dot(x, y, tone(M.gold, 1));
  return [gripBar(M.crimson), l, f];
}
function wingwardDisc() {
  // two wings curled into a ring: tips meet at the top, roots cross at the bottom; feather tips serrate the rim
  const body = polar((r, a) => {
    const side = a < Math.PI ? a : 2 * Math.PI - a;           // 0 at the top, PI at the bottom, either wing
    const tipGap = side < 0.16;
    const outer = 12 + 2.2 * (side / Math.PI) - 4 * Math.max(0, (((side * 9 / Math.PI) % 1) - 0.55));
    return !tipGap && r < outer && r > 7.6;
  });
  const knot = polar((r, a) => angDist(a, Math.PI) < 0.32 && r > 6.5 && r < 14.2);
  const all = U(body, knot).minus(HOLE());
  const l = WL(1).fill(all, (x, y) => {
    const dx = x + 0.5 - DC, dy = y + 0.5 - DC, r = Math.hypot(dx, dy); let a = Math.atan2(dx, -dy); if (a < 0) a += 2 * Math.PI;
    const side = a < Math.PI ? a : 2 * Math.PI - a; const v = discLit(M.gold)(x, y);
    if (r < 9.4) return discLit(M.teal, 14, 0.15)(x, y);
    return ((side * 9 / Math.PI) % 1) < 0.14 ? scale(v, 0.7) : v;
  });
  return [gripBar(M.teal), l];
}

const DISCS = {
  hoplite: { name: 'Hoplite Aspides', draw: hopliteDisc, blurb: 'A pair of small round shields: a bronze rim, a groove, a red painted field and a bronze collar round the grip.' },
  wayfarer: { name: 'Wayfarer Hoops', draw: wayfarerDisc, blurb: 'Bent-wood hoops shod with an iron tyre, bound at the four quarters with linen; linen round the grip.' },
  athanor: { name: 'Athanor Alembics', draw: athanorDisc, blurb: 'A bronze ring with a glass coil of elixir inside it and four stoppered vials set into the edge.' },
  hauberk: { name: 'Hauberk Rings', draw: hauberkDisc, blurb: 'Steel rings faced with riveted mail between a bright rim and a blue enamel collar.' },
  kettle: { name: 'Kettle Wheels', draw: kettleDisc, blurb: 'Steel wheels with a scalloped rim of ten lobes, as the capelet, over a green band studded with rivets.' },
  sentinel: { name: 'Sentinel Wards', draw: sentinelDisc, blurb: 'Eight-sided rings of lames with a fan point at each quarter, as the knees, round a navy collar.' },
  sol: { name: 'Sol Rays', draw: solDisc, blurb: 'Gold suns of twelve rays round a white collar.' },
  cataphract: { name: 'Cataphract Lamellae', draw: cataphractDisc, blurb: 'Rings of gold lamellae round a crimson collar, with the set\'s gold mask, eyes closed, at the top.' },
  wingward: { name: 'Wingward Pinions', draw: wingwardDisc, blurb: 'Two gold wings curled into a ring as the circlet: tips meeting at the top, roots crossed below, teal under the feathers.' },
};
