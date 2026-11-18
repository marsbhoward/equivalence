// QUARTZ BACK PIECES in eight directions (GEAR.md "Quartz", stage 4): every cape and back piece a Quartz box
// can hold, modelled once as 3D shapes in the body's frame (views.js) and rendered in its five views.
// Adapted from each piece's own front art (DemoGear.<Set>.cs) - its colours, its outline, its hem - not redrawn
// from nothing (the user's no-drift rule). Each returns the prims worn over body3d().
// sheet: cat body.js kit.js sets.js views.js(no sheet) quartz8.js | node - <out.png> [scale] [ids,comma]

// ---------------------------------------------------------------- a cape, parametric
// A sheet hung from the shoulders: `top` and `bottom` heights, its half-width from w0 at the shoulders to w1
// at the hem, its depth behind the back curving round the shoulders, a hem cut by `hem(x)` (the height the
// cloth reaches at x - lower = longer), the sides cut by `side(y)` (texels taken off each side at y), and
// `holes(p)` the distance to what is cut out of it - negative inside a hole or a split.
// a hooded cloak's cloth over each shoulder, part of the cape itself (no outline between): from the front the cloak is
// seen to sit ON the shoulders, the hood rising from between them (the user, 2026-11-17: hood and cloak read as two)
const shoulderCaps = p => Math.min(...[1, -1].map(s => 0.8 * sdEllipsoid(p, v3(s * 15.5, 100, 0), v3(7.6, 9.5, 9))));   // fuller: they hold the arm's top in every view (the user: the 3/4 lost its near shoulder)
function capeSdf(o) {
  // The top LIES ON THE SHOULDERS (the user's note, 2026-11-17: it sat too low): up at the shoulder line out past the
  // game's shoulder joints (~107.6 high, ~19 out), dipping at the neck, falling away beyond - and near the top the cloth
  // curves forward over them (wrap). It hangs SOFTLY (same note: it read stiff): folds that deepen toward the hem
  // (ripple) and, where no hem is cut, a slight wave along it.
  const top = o.top ?? SHOULDER_Y + 6, bottom = o.bottom, w0 = Math.max(o.w0 ?? 19, o.hooded ? 21 : 0), w1 = o.w1 ?? 26;
  const back = o.back ?? -11, sweep = o.sweep ?? 3.5, bow = o.bow ?? 0.012, thick = o.thick ?? 1.2;
  const wrap = o.wrap ?? 0.75, ripple = o.ripple ?? 2.8;
  const tOf = y => Math.max(0, Math.min(1, (top - y) / (top - bottom)));
  const half = y => w0 + (w1 - w0) * tOf(y) - (o.side ? o.side(y) : 0);
  // a hooded cloak wraps forward over the SHOULDERS only: its middle stays behind the neck, up under the hood
  const wrapAt = x => o.hooded ? wrap * Math.min(1, Math.abs(x) / 13) : wrap;
  const zc = (x, y) => { const t = tOf(y); const z = back - sweep * t + bow * x * x + wrapAt(x) * Math.max(0, y - (SHOULDER_Y - 7)) + ripple * t * Math.sin(x * 0.34 + 0.7); return o.hooded ? Math.min(z, 5) : z; };
  const topAt = o.hooded
    ? x => { const ax = Math.abs(x); return ax <= 17 ? top + 2 - ax * 0.12 : top - (ax - 17) * 1.3; }   // up to the neckline under the hood
    : x => { const ax = Math.abs(x); return ax < 7 ? top - 5 + ax * 5 / 7 : ax <= 18 ? top : top - (ax - 18) * 1.2; };
  const hemAt = x => (o.hem ? o.hem(x) : bottom + 1.2 * Math.sin(x * 0.55));
  return {
    half, zc, topAt,
    sdf: p => {
      if (p.y > top + 3 || p.y < bottom - 4) return Math.min(Math.max(p.y - top - 2, bottom - 4 - p.y, 0.5), o.hooded ? shoulderCaps(p) : 99);
      const h = half(p.y);
      // the z term over-states the distance where the sheet bends (wrap, ripple): scaled so the bake's steps stay short
      const d = Math.max((Math.abs(p.z - zc(p.x, p.y)) - thick) / 1.5, Math.abs(p.x) - h, p.y - topAt(p.x), hemAt(p.x) - p.y);
      const dd = o.hooded ? Math.min(d, shoulderCaps(p)) : d;
      return o.holes ? Math.max(dd, -o.holes(p)) : dd;
    },
  };
}
// broad fan folds across a cape: -1 in a fold's shade, +1 on its lit face, 0 between
const fanFold = (x, y, every = 9, from = SHOULDER_Y) => {
  const u = ((x + 40) % every) / every;
  const reach = Math.max(0, Math.min(1, (from - y) / 50));
  return u < 0.18 ? -reach : u > 0.82 ? reach : 0;
};

// ---------------------------------------------------------------- Nocturne Cape (DemoGear.Nocturne.cs)
// Grey, shoulders to the floor, flaring a little to a near-straight hem; a thin light line a couple of texels
// in down each side (the reference's edge); broad fan folds and two long creases out and down from the collar.
// A cape's six tones EXACTLY as its dye channel holds them (DyeChannel.Tones: line, deep, dark, base, light,
// glow - read from the game), so a dye repaints the eight-direction picture tone for tone (GearDye.RepaintPalette).
const exactRamp = ([line, deep, dark, base, light, glow]) => ({ line, deep, dark, base, light, glow });
const QZ = {
  nocturne: exactRamp(['#23222D', '#2F3036', '#474952', '#666975', '#858791', '#B2B4BA']),
  tepes: exactRamp(['#141327', '#1F202D', '#34354B', '#333670', '#474C90', '#5B62B0']),
  hellspawn: exactRamp(['#420D15', '#42030A', '#75080F', '#B21217', '#E63329', '#E3AAAB']),
  orichalc: exactRamp(['#56461E', '#6F5A17', '#AA8924', '#F2C433', '#F5D160', '#F9E39D']),
  copper: exactRamp(['#42261F', '#402313', '#6B3A20', '#B26136', '#D38E61', '#EAAE81']),
  talon: exactRamp(['#181620', '#272729', '#3C3C3E', '#47474C', '#737377', '#ABABAD']),
  talonFur: exactRamp(['#242025', '#322D28', '#4F473E', '#786B5E', '#A0978F', '#C9C4BF']),
  pilgrim: exactRamp(['#262937', '#2A323B', '#404C5A', '#5C6C80', '#939EAB', '#C4CAD1']),
  panels: exactRamp(['#2D191C', '#341B14', '#4E291F', '#703A2C', '#A17D74', '#CCB8B3']),
  iron: exactRamp(['#393740', '#454549', '#69696F', '#96969E', '#BABABF', '#D9D9DC']),
  regent: exactRamp(['#221632', '#241534', '#37204E', '#4E2E70', '#8A75A1', '#BFB4CC']),
  lining: exactRamp(['#47403E', '#5A5245', '#897D69', '#C4B296', '#D8CCBA', '#EAE3D9']),
  gold: exactRamp(['#3A2A12', '#5A421A', '#8A6A2A', '#C49A3A', '#E2BE62', '#F4DE9A']),
  wood: exactRamp(['#30221E', '#382717', '#553B23', '#7A5432', '#A78E78', '#CFC1B5']),
  shieldIron: exactRamp(['#32313A', '#3B3C3F', '#5A5B61', '#80828A', '#ABADB2', '#D1D2D5']),
  strap: exactRamp(['#241919', '#281A11', '#3C2719', '#563824', '#8F7C6E', '#C2B7B0']),
  bone: exactRamp(['#4D4847', '#625E53', '#968F7E', '#D6CCB4', '#E4DDCE', '#F0EDE4']),
  lacquer: exactRamp(['#17141D', '#141216', '#1F1C22', '#2C2830', '#747176', '#B3B2B4']),
  wrap: exactRamp(['#4D494D', '#62605A', '#969289', '#D6D0C4', '#E4E0D8', '#F0EEEA']),
  guard: exactRamp(['#473A28', '#5A4A26', '#897039', '#C4A052', '#D8C08D', '#EADDC1']),
  cord: exactRamp(['#3F151B', '#4E1412', '#771F1C', '#AA2C28', '#C77471', '#E0B3B2']),
  moss: exactRamp(['#10120C', '#1B240F', '#2D3F18', '#486321', '#688827', '#92B042']),
  shade: exactRamp(['#14121C', '#100F14', '#19171E', '#24212B', '#6E6D73', '#B0AFB3']),
  wander: exactRamp(['#2B1E1B', '#312113', '#4B321D', '#6B4729', '#9D8672', '#CABDB2']),
  wraith: exactRamp(['#280C15', '#2D070B', '#440B10', '#610F17', '#976166', '#C6A9AB']),
  dagged: exactRamp(['#41311F', '#513D18', '#7B5C24', '#B08434', '#CBAE79', '#E3D3B6']),
};
// a stable hash in [0, 1) - no Math.random, so every bake is the same picture
const hash01 = (a, b = 0) => { const v = Math.sin(a * 127.1 + b * 311.7) * 43758.5453; return v - Math.floor(v); };
// a tattered hem: long tatters every ~3 texels, dropping between `short` and `long` below `line`
const tatters = (seed, line, short, long, every = 3) => x => {
  const i = Math.floor((x + 60) / every), f = ((x + 60) % every) / every;
  const drop = hash01(i, seed) < 0.5 ? long * (0.6 + 0.4 * hash01(i, seed + 1)) : short * (0.5 + 0.5 * hash01(i, seed + 2));
  return line - drop * (1 - Math.abs(f - 0.5) * 1.6);
};
function nocturne3d() {
  const c = capeSdf({ bottom: 2, w0: 18, w1: 24, back: -11, sweep: 3, bow: 0.010 });
  return [
    { piece: 'cape', outR: 4, sdf: c.sdf,
      color: (p, n, nb) => {
        const r = QZ.nocturne, h = c.half(p.y), ax = Math.abs(p.x);
        if (nb.z > 0) return tone(r, Math.abs(p.x) > c.half(p.y) - 2 ? 4 : 3);   // the inside, seen past the body - its edge catching the light
        if (Math.abs(ax - (h - 2)) < 0.6 && p.y < SHOULDER_Y - 4) return tone(r, 4);   // the light line down each side
        // the two creases out and down from the collar
        if (p.y > 78 && p.y < SHOULDER_Y - 2 && Math.abs(ax - (5 + (SHOULDER_Y - 2 - p.y) * 0.45)) < 0.6) return tone(r, 1);
        // the fan folds of the lower third: pale tongues rising to a point from the hem
        if (p.y < 34) {
          const u = ((p.x + 40) % 10) - 5, tip = 30 - Math.abs(u) * 5;
          if (Math.abs(u) < 1.2 && p.y < tip) return tone(r, 4);
          if (Math.abs(u) < 2.4 && p.y < tip) return tone(r, 3);
          if (Math.abs(u) > 3.6 && p.y < 22) return tone(r, 1);
        }
        return tone(r, Math.max(2, Math.min(3, Math.round(litTone(n.x, -n.y, n.z)))));
      } },
  ];
}

// ---------------------------------------------------------------- Tepes Cape (DemoGear.Tepes.cs)
// Navy, shoulders to the floor, flaring to about one and a half times the body's width. Solid to the shins, then
// long tatters dragging to the floor and ragged sides - NO HOLES (the user's call, 2026-11-16: holes are the
// Hellspawn's). The FUR MANTLE over the off shoulder (the character's left, +x): a lumpy cap past the arm, and
// a panel of the cape's cloth falling outside the arm to the hip, torn at its end.
function tepes3d() {
  const r = QZ.tepes;
  const c = capeSdf({ bottom: 0, w0: 19, w1: 30, back: -11, sweep: 3.5, bow: 0.010,
    hem: tatters(11, 30, 10, 30), side: y => (y < 50 ? Math.max(0, Math.sin(y * 0.9) * 1.6 + (50 - y) * 0.02) : 0) });
  return [
    { piece: 'cape', outR: 4, sdf: c.sdf,
      color: (p, n, nb) => {
        if (nb.z > 0) return tone(r, Math.abs(p.x) > c.half(p.y) - 2 ? 3 : 2);
        if (p.y < 32) return tone(r, Math.max(2, Math.min(3, Math.round(litTone(n.x, -n.y, n.z) - (p.y < 10 ? 1 : 0)))));   // the torn length, a step darker
        return tone(r, Math.max(2, Math.min(4, Math.round(litTone(n.x, -n.y, n.z) + fanFold(p.x, p.y, 11)))));
      } },
    { piece: 'mantle', outR: 4,
      sdf: p => Math.min(
        sdEllipsoid(p, v3(13, 104, -1), v3(11, 5.5, 10)) - 0.6 * Math.abs(Math.sin(p.x * 1.3) * Math.sin(p.z * 1.1)),
        Math.max(sdRoundBox(p, v3(22.5, 82, -1), v3(3, 20, 6), 2), tatters(5, 66, 4, 9, 2.5)(p.z * 1.5) - p.y)),
      color: (p, n) => {
        if (p.y > 97) { const tuft = Math.abs(Math.sin(p.x * 1.7 + p.z * 1.3)) > 0.82; return tone(r, tuft ? 4 : Math.max(1, Math.min(3, Math.round(litTone(n.x, -n.y, n.z) - 0.5)))); }
        return tone(r, Math.max(2, Math.min(3, Math.round(litTone(n.x, -n.y, n.z)))));
      } },
  ];
}

// ---------------------------------------------------------------- Hellspawn Cape (DemoGear.Hellspawn.cs)
// Red, wider than any other, a HIGH COLLAR standing round the back of the head, the hem cut in spikes and the cloth
// torn through in holes (its own look - Tepes lost its holes to keep this one). Broad fan folds in whole planes.
function hellspawn3d() {
  const r = QZ.hellspawn;
  const spikes = x => { const u = ((x + 60) % 6) / 6; return 8 + Math.abs(u - 0.5) * 22 + hash01(Math.floor((x + 60) / 6), 3) * 8; };
  const holes = p => {
    const hs = [[-11, 40, 3, 5], [9, 30, 2.8, 4.5], [1, 18, 2.6, 4], [-18, 20, 2.4, 4], [17, 46, 2.2, 3.6]];
    let d = 1e9; for (const [x, y, rx, ry] of hs) d = Math.min(d, Math.hypot((p.x - x) / rx, (p.y - y) / ry) - 1);
    return d * 2.5;                                                            // the holes' own distance: negative inside one
  };
  const c = capeSdf({ bottom: 0, w0: 20, w1: 34, back: -11, sweep: 5, bow: 0.012, hem: spikes, holes });
  // The HIGH COLLAR (the user's call, 2026-11-17 - not horns): one standing sheet curving round the back of the
  // head from the neck, open in front, flaring a little as it rises, its top highest at its two edges - so from
  // the front only those edges show, either side of the head, and from behind it is a wall of the cape's cloth.
  const collarTop = x => 124 + Math.max(0, Math.abs(x) - 11.5) * 2.0;   // flat across the back, the tips rising at the sides (the mock)
  const collarSdf = p => {
    if (p.y < 101 || p.y > 140) return Math.max(101 - p.y, p.y - 140, 0.5);
    const rise = Math.max(0, p.y - 101), rx = 15.5 + rise * 0.16, rz = 13 + rise * 0.12;
    const ring = (Math.hypot(p.x / rx, (p.z + 1) / rz) - 1) * Math.min(rx, rz);
    return Math.max(Math.abs(ring) - 1.1, p.z - 3, p.y - collarTop(p.x));
  };
  return [
    { piece: 'cape', outR: 4, sdf: c.sdf,
      color: (p, n, nb) => {
        if (nb.z > 0) return tone(r, Math.abs(p.x) > c.half(p.y) - 2 ? 3 : 2);
        const plane = Math.sin((p.x + 3) * 0.33 + (SHOULDER_Y - p.y) * 0.015);
        const t = 3 + (plane > 0.55 ? 1 : plane < -0.55 ? -1 : 0) - (p.y < 14 ? 1 : 0);
        return tone(r, t);
      } },
    { piece: 'collar', outR: 4, sdf: collarSdf,
      color: (p, n, nb) => {
        const out = nb.x * p.x + nb.z * (p.z + 1) > 0;                         // the outside, away from the head
        if (!out) return tone(r, p.y > collarTop(p.x) - 2 ? 3 : 2);
        return tone(r, p.y > collarTop(p.x) - 2 ? 4 : Math.max(2, Math.min(3, Math.round(litTone(n.x, -n.y, n.z)))));
      } },
  ];
}

// ---------------------------------------------------------------- Orichalc Long Coat (DemoGear.Orichalc.cs)
// Golden cloth from the shoulders to the floor round the back and sides, OPEN AT THE FRONT; split up the back
// from the floor to the hips so the legs show between its halves; edged in the set's copper.
function orichalc3d() {
  const r = QZ.orichalc, cu = QZ.copper;
  const rx = y => (y > 64 ? 15.5 : 15.5 + (64 - y) * 0.11), rz = y => (y > 64 ? 11 : 11 + (64 - y) * 0.08);
  const shell = p => Math.abs(sdTube(p, 0, -1, 1, SHOULDER_Y + 1, rx, rz, 1)) - 1.0;
  const front = p => Math.min(p.z - 2.5, 0) * -1 + 0 > 0 ? 0 : 0;
  const open = p => (p.z > 3 && Math.abs(p.x) < 13) || (p.z < -4 && Math.abs(p.x) < 4.5 - (p.y - 58) * 0.0 && p.y < 58);
  const sdf = p => { const d = shell(p); return open(p) ? Math.max(d, 1.2) : d; };
  const edge = p => (p.z > 1.5 && Math.abs(p.x) > 11 && Math.abs(p.x) < 14.5) || (p.z < -3 && p.y < 60 && Math.abs(p.x) < 6.5) || p.y < 3.5;
  return [
    { piece: 'coat', outR: 4, sdf,
      color: (p, n, nb) => {
        if (edge(p)) return tone(cu, p.y < 3.5 ? 2 : 3);
        if (nb.x * p.x + nb.z * (p.z + 1) < 0) return tone(r, 2);              // the inside
        return tone(r, Math.max(2, Math.min(4, Math.round(litTone(n.x, -n.y, n.z) + 0.5))));
      } },
  ];
}

// ---------------------------------------------------------------- Talon Coat (DemoGear.Talon.cs)
// A grey coat - not a cape: the back panel to the calf, split into two tails from the waist, ragged at the end,
// and the FUR standing up across the shoulders behind the neck. The tails sway, they do not stream.
function talon3d() {
  const r = QZ.talon, f = QZ.talonFur;
  const hem = x => tatters(7, 26, 3, 7, 2.5)(x);
  const c = capeSdf({ bottom: 18, w0: 17, w1: 21, back: -10.5, sweep: 2.5, bow: 0.014, hem,
    holes: p => (p.y < 60 ? Math.abs(p.x) - (1.6 + (60 - p.y) * 0.02) : 1) });       // the split between the tails
  return [
    { piece: 'coat', outR: 4, sdf: c.sdf,
      color: (p, n, nb) => {
        if (nb.z > 0) return tone(r, 2);
        if (Math.abs(Math.abs(p.x) - 7) < 0.6 && p.y < 96 && p.y > 30) return tone(r, 2);   // seams down the back
        return tone(r, Math.max(2, Math.min(4, Math.round(litTone(n.x, -n.y, n.z) + 0.5))));
      } },
    { piece: 'fur', outR: 4,
      sdf: p => sdRoundBox(p, v3(0, 106, -6), v3(18, 4.5, 6), 4) - 0.8 * Math.abs(Math.sin(p.x * 1.4) * Math.cos(p.y * 1.2)),
      color: (p, n) => { const tip = Math.abs(Math.sin(p.x * 1.4 + p.y)) > 0.85; return tone(f, tip ? 5 : Math.max(2, Math.min(4, Math.round(litTone(n.x, -n.y, n.z) + 0.5)))); } },
  ];
}

// ---------------------------------------------------------------- Pilgrim's Mantle (a Quartz draft, 2026-11-17)
// A round shoulder cape CLOSED all the way round, to the elbow, its edge scalloped, clasped in gold at the throat,
// over a second layer hanging behind to the knee, scalloped too. The only two-layer back.
const scallops = (line, r = 3, every = 6) => x => { const u = ((x + 60) % every) - every / 2; return line - Math.sqrt(Math.max(0, r * r - u * u)); };
function pilgrim3d() {
  const r = QZ.pilgrim;
  const long = capeSdf({ bottom: 30, w0: 17, w1: 25, back: -11, sweep: 3, bow: 0.010, hem: scallops(32) });
  const rx = y => 22.5 + Math.max(0, 104 - y) * 0.16, rz = y => 15 + Math.max(0, 104 - y) * 0.12;   // clear of the arms
  const capeletHem = p => { const arc = Math.atan2(p.x, p.z) * 16; const u = (((arc + 600) % 6) + 6) % 6 - 3; return 80 - Math.sqrt(Math.max(0, 9 - u * u)); };
  // up to 110: the game's shoulder joint reaches ~108 (Body8: shoulder 103, radius 5) and must stay under it
  const capelet = p => Math.max(Math.abs(sdTube(p, 0, -0.5, 70, 110, rx, rz, 5)) - 1.2, capeletHem(p) - p.y, Math.hypot(p.x, p.z) < 10 ? p.y - 103 : -1);
  return [
    { piece: 'cape', outR: 4, sdf: long.sdf,
      color: (p, n, nb) => nb.z > 0 ? tone(r, 3) : tone(r, Math.max(2, Math.min(3, Math.round(litTone(n.x, -n.y, n.z) - 0.3)))) },
    { piece: 'capelet', outR: 4, sdf: capelet,
      color: (p, n) => tone(r, p.y > 101 ? 4 : Math.max(2, Math.min(4, Math.round(litTone(n.x, -n.y, n.z) + 0.6)))) },
    { piece: 'clasp', outR: 3, sdf: p => sdEllipsoid(p, v3(0, 101, 16.4), v3(1.8, 1.8, 1.2)),
      color: (p, n) => tone(QZ.gold, p.y > 101 ? 4 : 3) },
  ];
}

// ---------------------------------------------------------------- War Panels (a Quartz draft)
// A riveted iron YOKE round the shoulders and four long leather panels hung from it round the back and sides,
// gaps between, each cut to a point - cloth worn like armour.
function panels3d() {
  const r = QZ.panels, fe = QZ.iron;
  const PANELS = [[-1.0, 0.24, 24], [-0.38, 0.26, 17], [0.38, 0.26, 17], [1.0, 0.24, 24]];   // centre angle from the back, half-width, tip height
  const R = y => 14.5 + Math.max(0, 93 - y) * 0.055;
  const panel = ([c, half, tip]) => p => {
    const phi = Math.atan2(p.x, -p.z), u = (phi - c) / half;
    const rr = Math.hypot(p.x, p.z * 1.18);
    const bottom = tip + Math.abs(u) * 7;
    return Math.max(Math.abs(rr - R(p.y)) - 1.1, (Math.abs(phi - c) - half) * R(p.y), p.y - 94, bottom - p.y);
  };
  // SOLID, not a shell: from the camera's height a hollow ring shows the arm inside it past its top edge (probed in
  // the game, 2026-11-17). A thick collar with only the neck open, so the shoulders are under it and the arms hang
  // out below. Full width to 108 - the game's shoulder joint tops out at ~107.6 (probed) - then rounded.
  // Halved: the steep taper makes sdTube over-estimate the distance, and the bake's ray stepped clean over the
  // shoulders into the neck opening (traced 2026-11-17) - a conservative distance keeps the steps short.
  const yoke = p => 0.5 * Math.max(sdTube(p, 0, -0.5, 91, 112, y => 21.5 - Math.max(0, y - 108) * 2.5, 14.5, 2.5), 10.5 - Math.hypot(p.x, p.z + 0.5));
  return [
    ...PANELS.map((pn, i) => ({ piece: 'panel' + i, outR: 4, sdf: panel(pn),
      color: (p, n) => tone(r, Math.max(2, Math.min(4, Math.round(litTone(n.x, -n.y, n.z) + (i === 0 || i === 3 ? 0.4 : 0))))) })),
    { piece: 'yoke', outR: 4, sdf: yoke,
      color: (p, n) => {
        const a = Math.atan2(p.x, p.z) * 4.5, rivet = Math.abs(p.y - 95.5) < 0.9 && Math.abs(((a % 3) + 3) % 3 - 1.5) < 0.45;
        if (rivet) return tone(fe, 5);
        return tone(fe, p.y > 107 ? 4 : Math.max(2, Math.min(4, Math.round(litTone(n.x, -n.y, n.z)))));
      } },
  ];
}

// ---------------------------------------------------------------- Regent's Mantle (a Quartz draft)
// A round collar FANNED UP behind the head, lined in a pale cloth facing forward, over a short cape to the hip with a
// rounded hem. The only standing fan collar.
function regent3d() {
  const r = QZ.regent, li = QZ.lining;
  const c = capeSdf({ bottom: 56, w0: 19, w1: 25, back: -11, sweep: 2.5, bow: 0.010,
    hem: x => 56 + (x / 25) * (x / 25) * 8 });
  const fan = p => {
    const zc = -11.5 + Math.max(0, p.y - 104) * -0.12;                        // leaning back a little as it rises
    return Math.max(Math.abs(p.z - zc) - 1.0, Math.hypot(p.x, p.y - 110) - 27, 101 - p.y);
  };
  return [
    { piece: 'cape', outR: 4, sdf: c.sdf,
      color: (p, n, nb) => nb.z > 0 ? tone(li, 2) : tone(r, Math.max(2, Math.min(4, Math.round(litTone(n.x, -n.y, n.z) + 0.3)))) },
    { piece: 'fan', outR: 4, sdf: fan,
      color: (p, n, nb) => {
        const rim = Math.hypot(p.x, p.y - 110) > 24.6;
        if (nb.z > 0) return rim ? tone(r, 3) : tone(li, Math.abs(p.x) < 6 ? 3 : 4);          // the lining, toward the head
        return tone(r, rim ? 4 : Math.max(2, Math.min(3, Math.round(litTone(n.x, -n.y, n.z)))));
      } },
  ];
}

// ---------------------------------------------------------------- the back pieces that are not cloaks
// A bandolier on the body: a band of the torso's own surface (body3d's torso, lifted a texel) cut by a tilted plane
// through the shoulder and the far hip - so it lies on the chest and the back and goes over the shoulder.
const torsoSdf = p => sdTube(p, 0, 0, HIP_Y - 2, SHOULDER_Y + 3, y => 11 + 2.5 * smooth((y - 70) / 30), 8, 4);
const bandolier = (sx, width = 1.6) => {
  const a = v3(sx * 13, 106, 0), b = v3(-sx * 13, 60, 0);            // shoulder to far hip, in the body's plane
  const dx = b.x - a.x, dy = b.y - a.y, L = Math.hypot(dx, dy), nx = dy / L, ny = -dx / L;
  return p => 0.6 * Math.max(Math.abs(torsoSdf(p) - 1.2) - 0.7, Math.abs((p.x - a.x) * nx + (p.y - a.y) * ny) - width);
};

// ---------------------------------------------------------------- Slung Shield (a Quartz draft)
// A round wooden shield on the back - vertical planks, an iron rim and boss - its leather strap across the chest.
function shield3d() {
  const wd = QZ.wood, fe = QZ.shieldIron, st = QZ.strap;
  const C = v3(0, 82, -14.5);
  const disc = p => Math.max(Math.hypot(p.x - C.x, p.y - C.y) - 22, Math.abs(p.z - C.z) - 1.4);
  return [
    { piece: 'shield', outR: 4, sdf: disc,
      color: (p, n) => {
        const rr = Math.hypot(p.x - C.x, p.y - C.y);
        if (rr > 19.8) return tone(fe, rr > 21 ? 2 : 3);
        if (p.z > C.z) return tone(wd, 2);                                       // its face toward the back
        const plank = Math.floor((p.x + 22) / 6);
        if (Math.abs(((p.x + 22) % 6)) < 0.6) return tone(wd, 1);                // the seam between planks
        return tone(wd, plank % 2 ? 2 : 3);
      } },
    { piece: 'boss', outR: 3, sdf: p => sdEllipsoid(p, v3(C.x, C.y, C.z - 1.6), v3(5, 5, 2.8)),
      color: (p, n) => tone(fe, p.y > C.y + 1 && p.x < 1 ? 5 : 3) },
    { piece: 'strap', outR: 3, sdf: bandolier(1),
      color: (p, n) => tone(st, (Math.round(p.y) % 5 === 0) ? 4 : 3) },
  ];
}

// ---------------------------------------------------------------- Bone Wings (a Quartz draft)
// Two small folded wings of BARE BONE from the shoulder blades - no membrane, the gaps open (the user's call): the
// joint above the shoulder, five finger bones fanning down past the hips, braced by thin struts.
function wings3d() {
  const b = QZ.bone;
  const prims = [];
  for (const s of [1, -1]) {
    const root = v3(s * 6, 100, -9), top = v3(s * 23.5, 127, -13), knuckle = v3(s * 27, 117, -15);
    const tips = [[41, 58], [34, 47], [26, 49], [18, 57], [12, 68]].map(([x, y]) => v3(s * x, y, -14));
    const fingers = tips.map(t => [knuckle, t]);
    const struts = [];
    for (const k of [0.62]) for (let i = 0; i + 1 < tips.length; i++)
      struts.push([lerp3(knuckle, tips[i], k), lerp3(knuckle, tips[i + 1], k)]);
    const sdf = p => Math.min(sdCapsule(p, root, top, 2.0), sdCapsule(p, top, knuckle, 1.7),
      ...fingers.map(([a, c]) => sdCapsule(p, a, c, 1.1)), ...struts.map(([a, c]) => sdCapsule(p, a, c, 0.6)));
    prims.push({ piece: s > 0 ? 'wingL' : 'wingR', outR: 3, sdf,
      color: (p, n) => tone(b, p.y > 112 ? 4 : p.y > 80 ? 3 : 2) });
  }
  return prims;
}

// ---------------------------------------------------------------- Crossed Katanas (a Quartz draft)
// Two sheathed katanas crossed on the back: wrapped hilts and round gilt guards over the shoulders, black lacquered
// scabbards to the hips, a red cord at each mouth, held by one red harness strap across the chest (the user's call).
function katanas3d() {
  const lq = QZ.lacquer, wr = QZ.wrap, gd = QZ.guard, cd = QZ.cord;
  const sword = (s, z) => {
    const A = v3(-s * 23.5, 137, z), B = v3(s * 18.5, 50, z);
    const along = p => { const ab = v3(B.x - A.x, B.y - A.y, B.z - A.z), ap = v3(p.x - A.x, p.y - A.y, p.z - A.z); return (ap.x * ab.x + ap.y * ab.y + ap.z * ab.z) / (ab.x * ab.x + ab.y * ab.y + ab.z * ab.z); };
    const at = k => lerp3(A, B, k);
    return { piece: s > 0 ? 'katanaL' : 'katanaR', outR: 3,
      sdf: p => Math.min(sdCapsule(p, A, at(0.22), 1.8), sdCapsule(p, at(0.22), B, 2.2),
                         sdCapsule(p, at(0.225), at(0.245), 4.2)),
      color: (p, n) => {
        const k = along(p);
        if (k < 0.22) return (Math.round(p.x + p.y) % 4 < 2) ? tone(wr, 3) : tone(lq, 1);   // the wrapped hilt
        if (k < 0.25) return tone(gd, p.y > at(0.235).y ? 4 : 3);                          // the guard
        if (k < 0.29) return tone(cd, 3);                                                   // the cord at the mouth
        if (k > 0.95) return tone(gd, 3);                                                   // the end cap
        return tone(lq, n.x < 0 ? 4 : 3);
      } };
  };
  return [sword(1, -12.5), sword(-1, -14.5),
    { piece: 'harness', outR: 3, sdf: bandolier(-1, 1.5), color: (p, n) => tone(cd, (Math.round(p.y) % 6 === 0) ? 2 : 3) }];
}

// ---------------------------------------------------------------- Peaked Hood (a Quartz draft)
// The shared hood up (the user's call, 2026-11-17, after a spike, a dome, a faceted box and a back fold), cloth wrapped
// under the chin to a point on the chest, and a full cape across both shoulders to the floor, its hem cut on a slant.
// THE SET'S CLOTH RULE (the audit, 2026-11-17): two tones in broad vertical fold bands, the darker one in each band's
// shade - never per-texel from the surface's angle, which the hood's copy of its cloak (drawn unsheared) and the cloak
// (in the body's sheared picture) disagree on, so the shade flipped in a staircase along the join
// Above CLOTH_JOIN (the shoulders, where the hood's copy of its cloak overlies the cloak) the cloth is ONE tone, inside
// and out, so the two pictures cannot disagree there; the fold bands start below it, off the middle (|x| 7-13)
const CLOTH_JOIN = 92;
const clothTone = (r, p) => tone(r, p.y > CLOTH_JOIN ? 3 : Math.sin(p.x * 0.32 - 1.57) > 0.6 ? 2 : 3);
// the shared hood's shell: the round crown, and its sides flaring a little to the shoulders
// (2026-11-17, the user: hood and cloak read as two pieces) the hood GATHERS INTO THE NECK - narrowing below the jaw
// rather than flaring into a shelf as wide as the shoulders - so the cloak's shoulders show either side of it
const hoodShell = p => {
  const g = Math.max(0, 108 - p.y);   // falls straight past the face (the original's silhouette), gathers only at the bottom
  return Math.max(sdRoundBox(p, v3(0, 121.5, -1), v3(18, 16.5, 16.5), 9),
    0.8 * (Math.abs(p.x) - (18 - g * 0.55)), 0.8 * (-p.z - (17.5 - g * 0.25)));
};
// The hood and its cloak are ONE item in two pictures (the hood over the head, the cloak in the body's): so the hood's
// picture carries the TOP of its own cloak - the same surface, the same colours - down to SKIRT_Y, lying over the cloak
// itself. Where they overlap they are the same cloth; no seam can open between them (the user, 2026-11-17).
const SKIRT_Y = 96;
const hoodSkirt = cloth => ({ piece: 'hood', outR: 4, sdf: p => Math.max(cloth.sdf(p), SKIRT_Y - p.y), color: cloth.color });
// a hooded item with its skirt: every prim of its cloak (named `cloth`) copied into the hood above SKIRT_Y
const withSkirt = (make, cloth = 'cape') => () => { const ps = make(); return [...ps, ...ps.filter(q => q.piece === cloth).map(hoodSkirt)]; };
// the hood's back laid onto the cloak: a soft point down the upper back (a BODY piece, so the torso hides it from the
// front), on the cloak's own surface
const hoodDrape = (r, c) => ({ piece: 'cape', outR: 4,   // drawn as the cape: no outline between them
  sdf: p => { const zc = c.zc(p.x, p.y) - 1.6; const half = Math.max(0, (p.y - 93) * 0.85);
    return 0.7 * Math.max(Math.abs(p.z - zc) - 1.2, Math.abs(p.x) - half, p.y - 110, 93 - p.y); },
  color: (p, n, nb) => clothTone(r, p) });
function peaked3d() {
  const r = QZ.moss;
  const c = capeSdf({ bottom: 2, w0: 20, w1: 33, back: -12, sweep: 4.5, bow: 0.010, hem: x => 5 + x * 0.16, hooded: true, wrap: 1.3 });
  const bibW = y => Math.min(20, 20 * (y - 89) / 17);   // the mock: shoulder-wide from the face opening, one point at mid-chest   // as the mock: shoulder to shoulder at the chin, to a point on the chest; lying close (the user: it stuck out)
  return [
    hoodUp(r), hoodDrape(r, c),   // the same hood as the others (the user's call, 2026-11-17)
    { piece: 'bib', outR: 4,
      sdf: p => 0.7 * Math.max(Math.abs(p.z - (10 + Math.max(0, p.y - 100) * 0.3 - Math.max(0, Math.abs(p.x) - 7) * 0.55)) - 1.3, Math.abs(p.x) - bibW(p.y), p.y - 112.5, 89 - p.y),
      color: (p, n) => tone(r, 3) },   // a flat, rigid plane (the mock), in the cloak's lit tone
    { piece: 'cape', outR: 4, sdf: c.sdf,
      color: (p, n, nb) => {
        if (nb.z > 0 && p.y <= CLOTH_JOIN) return tone(r, 2);                              // the inside
        const ax = Math.abs(p.x), h = c.half(p.y);
        return clothTone(r, p);
      } },
  ];
}

// ---------------------------------------------------------------- the round hood, worn up (Hooded Cloak, Wanderer's
// Hood, Wraithguard Cloak - their shared four-view cowl): a round crown with a seam down its middle, the face open, the
// sides falling to the shoulders. A HEAD piece ("hood"), drawn over the head and any helm (the rig's hood order).
function hoodUp(r, o = {}) {
  const opening = p => {
    if (p.z < 1 || p.y < 108) return -99;
    const half = 13.5 + Math.max(0, p.z - 1) * 0.35, brim = 128.5 - 1.5 * Math.max(0, 1 - Math.abs(p.x) / 8);   // not hugging the face; its side still wraps past it
    return Math.abs(p.x) < half && p.y < brim ? 0.8 : -99;
  };
  const shell = hoodShell;
  return { piece: 'hood', outR: 4, sdf: p => Math.max(shell(p), 103 - p.y, opening(p)),
    color: (p, n) => {
      if (p.y > 128 && Math.abs(p.x) < 0.8 && p.z < 8) return tone(r, 1);                                  // the seam over the crown
      if (p.z > 5 && p.y > 122 && p.y < 129 && Math.abs(p.x) < 15) return tone(r, o.brim ?? 3);              // the rim over the brow
      if (p.y < 114) return clothTone(r, p);                                                               // the rim: the cloak's own cloth, no lip
      return tone(r, Math.max(2, Math.min(o.lit ?? 3, Math.round(litTone(n.x, -n.y, n.z)))));
    } };
}

// ---------------------------------------------------------------- Hooded Cloak (DemoGear black_hood)
// From its own art: the near-black cowl up, and a straight full-length cape - the character's whole height (the user's
// call, 2026-11-16) - falling as one column behind, barely flaring.
function blackhood3d() {
  const r = QZ.shade;
  const c = capeSdf({ bottom: 2, w0: 19, w1: 21, back: -11, sweep: 2.5, bow: 0.010, ripple: 2.2, hooded: true, wrap: 1.3 });
  return [hoodUp(r), hoodDrape(r, c),
    { piece: 'cape', outR: 4, sdf: c.sdf,
      color: (p, n, nb) => {
        if (nb.z > 0 && p.y <= CLOTH_JOIN) return tone(r, 2);
        return clothTone(r, p);
      } }];
}

// ---------------------------------------------------------------- Wanderer's Hood (DemoGear wanderers_hood)
// From its own art: the brown cowl up over a PONCHO - boxy, over both shoulders and the arms, to the hip, split up the
// front over the chest, its shoulders rounded off.
function wanderer3d() {
  const r = QZ.wander;
  const rx = y => 23 - Math.max(0, y - 96) * 0.4, rz = y => 17 - Math.max(0, y - 96) * 0.35;
  const poncho = p => {
    if (p.y > 108 || p.y < 60) return Math.max(p.y - 108, 60 - p.y, 0.5);
    const ring = (Math.hypot(p.x / rx(p.y), (p.z + 0.5) / rz(p.y)) - 1) * Math.min(rx(p.y), rz(p.y));
    const shell = 0.8 * Math.max(Math.abs(ring + 0.8) - 2.0, 0);   // thicker inward: no pinholes where the arm runs close
    const split = p.z > 0 && Math.abs(p.x) < 3.5 + Math.max(0, 92 - p.y) * 0.25 && p.y < 96 ? 0.8 : -99;   // open up the front
    return Math.max(shell, split, 61 + 1.2 * Math.sin(p.x * 0.4) - p.y, p.y - 107.5);
  };
  return [hoodUp(r),   // one lighting rule for the set: two tones and a lit fold (the audit, 2026-11-17)
    { piece: 'poncho', outR: 4, sdf: poncho,
      color: (p, n, nb) => {
        if (nb.x * p.x + nb.z * (p.z + 0.5) < 0) return tone(r, 2);                        // the inside (no dark slit at the neck)
        if (p.y < 64) return tone(r, 2);                                                    // the hem's shadow
        return tone(r, n.x < -0.35 ? 3 : n.x > 0.35 ? 2 : 3);                               // flat cloth, two tones, as the original
      } }];
}

// ---------------------------------------------------------------- Wraithguard Cloak (DemoGear.WraithCloak.cs)
// From its own art (the user kept: the chest and the texture): the crimson cowl up; two rolls of cloth bunched round
// the throat and across the chest to one shoulder; a long cape torn into points at the calf, its back falling in dark
// arched folds.
function wraith3d() {
  const r = QZ.wraith;
  const torn = x => 22 + 4.5 * Math.abs(Math.sin(x * 0.9 + 1.3)) + 3 * Math.abs(Math.sin(x * 0.37));
  const c = capeSdf({ bottom: 20, w0: 19, w1: 25, back: -11, sweep: 3, bow: 0.010, hem: x => torn(x) - 6, hooded: true, wrap: 1.3 });
  const rolls = [[v3(-7, 104.5, 9.5), v3(15, 100, 6.5), 2.6], [v3(-5, 100, 11), v3(16, 95.5, 7.5), 2.3]];
  return [hoodUp(r), hoodDrape(r, c),   // no sheen: the hood in the cape's own tones, one garment (the user's call)
    { piece: 'cape', outR: 4, sdf: c.sdf,
      color: (p, n, nb) => {
        if (nb.z > 0 && p.y <= CLOTH_JOIN) return tone(r, 1);
        // the arched folds down the back: dark tongues rising from the hem
        const u = ((p.x + 40) % 11) - 5.5, arch = 84 - u * u * 2.2;   // rising well up the cape, as the original's
        if (p.y < arch && Math.abs(u) < 3.2) return tone(r, 1);
        return clothTone(r, p);
      } },
    // the original's DRAPE: the cloak falls over the cloaked shoulder (the character's left, +x) and down the front past
    // the arm to the hip, in broad diagonal planes, its hem torn - so the cloak reads as one garment from the front
    { piece: 'cape', outR: 4,
      sdf: p => {
        const zc = 10.5 - Math.max(0, p.x - 12) * 1.15 + Math.max(0, 9 - p.x) * 0.25;   // round the shoulder and back to the cape
        const top = 107.5 - Math.max(0, p.x - 9) * 0.35, hem = 60 + 3 * Math.abs(Math.sin(p.x * 0.9 + 0.4)) + (20 - p.x) * 0.25;
        return 0.6 * Math.max(Math.abs(p.z - zc) - 1.3, 5 - p.x, p.x - 21, p.y - top, hem - p.y);
      },
      color: (p, n) => { const plane = Math.sin((p.x * 0.55) + (108 - p.y) * 0.09); return tone(r, p.y > CLOTH_JOIN || plane <= 0.45 ? 3 : 2); } },
    // the rolls are the cloak's own cloth (one piece, no outline between; its plain tones - the user, 2026-11-17)
    { piece: 'cape', outR: 4, sdf: p => Math.min(...rolls.map(([a, b, rad]) => sdCapsule(p, a, b, rad))),
      color: (p, n) => tone(r, 3) }];
}

// ---------------------------------------------------------------- Dagged Hood (a Quartz draft)
// The hood THROWN BACK (the mock): a short capelet over the shoulders to the chest, its hem cut into square dags, the
// fallen hood a fold behind the neck, and its long tail - a liripipe - hanging down the back to the knee. No head piece.
function dagged3d() {
  const r = QZ.dagged;
  const rx = y => 23.5 - Math.max(0, y - 98) * 0.6, rz = y => 18 - Math.max(0, y - 98) * 0.45;   // wide enough that the game's arm never pokes through (the user's black dots)
  const dag = x => (((Math.floor((x + 40) / 3)) % 2) ? 86 : 83.5);
  const capelet = p => {
    if (p.y > 110 || p.y < 80) return Math.max(p.y - 110, 80 - p.y, 0.5);
    const ring = (Math.hypot(p.x / rx(p.y), (p.z + 0.5) / rz(p.y)) - 1) * Math.min(rx(p.y), rz(p.y));
    return Math.max(0.8 * (ring - 1.2), dag(p.x) - p.y, p.y - 108);   // SOLID (no pinholes where the arm runs close inside it - the user's two black dots)
  };
  // the FALLEN HOOD (the mock; the user: from the side it read as no hood at all): the hood lies thrown back on the
  // upper back - a bunched, rounded shield from behind the neck to a point, a seam down its middle, its rolled opening
  // across the nape - and the liripipe grows from its point
  const hoodHalf = y => y > 105 ? 13 : 13 * (y - 88) / 17;
  const hoodZ = (x, y) => -14.5 - 6 * Math.max(0, 1 - Math.abs(x) / 14) * Math.max(0, Math.min(1, (y - 88) / 12));   // bunched, bulging out
  const fallen = p => Math.min(
    0.6 * Math.max(Math.abs(p.z - hoodZ(p.x, p.y)) - 3, Math.abs(p.x) - hoodHalf(p.y), p.y - 118, 88 - p.y),
    0.8 * sdCapsule(p, v3(-11.5, 114.5, -12.5), v3(11.5, 114.5, -12.5), 3.6));   // the rolled opening across the nape, up behind the head
  const liripipe = fallen;   // the trailing tail is gone (the user, 2026-11-17): the fallen hood alone, its point on the back
  return [
    { piece: 'capelet', outR: 4, sdf: capelet,
      color: (p, n, nb) => {
        if (nb.x * p.x + nb.z * (p.z + 0.5) < 0) return tone(r, 1);
        if (p.y < 89) return tone(r, 2);
        return tone(r, Math.max(2, Math.min(3, Math.round(litTone(n.x, -n.y, n.z)))));
      } },
    { piece: 'liripipe', outR: 4, sdf: liripipe,
      color: (p, n) => {
        if (p.y > 88 && Math.abs(p.x) < 0.8 && p.y < 111) return tone(r, 2);                   // the seam down the fallen hood
        if (p.y > 111.5) return tone(r, n.y > 0.3 ? 3 : 2);                                     // the rolled opening
        return tone(r, Math.max(2, Math.min(3, Math.round(litTone(n.x, -n.y, n.z)))));
      } },
  ];
}

const QUARTZ8 = { nocturne: nocturne3d, tepes: tepes3d, hellspawn: hellspawn3d, orichalc: orichalc3d, talon: talon3d, pilgrim: pilgrim3d, panels: panels3d, regent: regent3d, shield: shield3d, wings: wings3d, katanas: katanas3d, peaked: withSkirt(peaked3d), blackhood: withSkirt(blackhood3d), wanderer: withSkirt(wanderer3d, 'poncho'), wraith: withSkirt(wraith3d), dagged: dagged3d };

// ---------------------------------------------------------------- sheet
if (require.main === module || typeof module === 'undefined' || !module.parent) {
  const fs = require('fs'), zlib = require('zlib');
  const out = process.argv[2] || 'quartz8.png', S = +(process.argv[3] || 4);
  const want = (process.argv[4] || '').split(',').filter(Boolean);
  const ids = want.length ? want : Object.keys(QUARTZ8);
  const figures = [['bare', () => { POSE.hands = {}; return body3d(); }],
    ...ids.map(id => [id, () => { POSE.hands = {}; return [...body3d(), ...QUARTZ8[id]()]; }])];
  const CW = 100, CH = TOP + 12, OXC = CW / 2;
  const cols = VIEWS.length, W = cols * CW * S, H = figures.length * CH * S;
  const buf = Buffer.alloc(W * H * 4);
  for (let i = 0; i < W * H; i++) { buf[i * 4] = 0x4b; buf[i * 4 + 1] = 0x4e; buf[i * 4 + 2] = 0x57; buf[i * 4 + 3] = 255; }
  const draw = (grid, col, row, ox, oy) => { for (const [k, c] of grid) { const x = KX(k) + ox + col * CW, y = KY(k) + oy + row * CH; const [r, g, b] = hex2rgb(c);
    for (let dy = 0; dy < S; dy++) for (let dx = 0; dx < S; dx++) { const px = x * S + dx, py = y * S + dy; if (px < 0 || py < 0 || px >= W || py >= H) continue; const i = (py * W + px) * 4; buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; } } };
  figures.forEach(([name, make], r) => VIEWS.forEach(([v, yaw], c) => {
    draw(compose(renderView(make(), yaw)), c, r, OXC, 4);
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
