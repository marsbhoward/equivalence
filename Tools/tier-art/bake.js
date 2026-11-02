// node: cat body.js kit.js sets.js bake.js | node - <out.cs> <setIds,comma>
// Bakes the approved tier-armour mock into the game's rig layers: each piece's mock layers become
// grids at the body's density (150), placed against the joint the layer hangs from. Mock coords
// are the bare body's capture: the rig root at (19, 56), y down. Pivots measured from the rig.
const fs = require('fs');
const out = process.argv[2];
const want = process.argv[3].split(',');

const PIVOT = {
  torso: [19, 72], head: [19, 28], armNear: [5, 30], armFar: [33, 30], legBack: [11, 72], legFront: [27, 72],
};
const ELBOW_ART = 44, ELBOW_EXTRA = 6;   // mock rows 44..49 are the rig's own stretch of the upper arm

// ---- the legs: a skirt hangs from the hips (the torso's Tasset, still while the legs move); a
// design that is two legs (hose, cuisses) splits down the middle, each half riding its own leg ----
const tasset = set => [{ rig: 'Tasset', frame: 'torso', layers: set.Legs() }];
const perLeg = set => {
  const n = L(DEPTH.legs), f = L(DEPTH.legs);
  for (const l of set.Legs()) for (const [k, c] of l.c) (KX(k) < 19 ? n : f).c.set(k, l.far ? scale(c, FAR_SHADE) : c);
  return [{ rig: 'LegsBack', frame: 'legBack', layers: [n] }, { rig: 'LegsFront', frame: 'legFront', layers: [f] }];
};
const LEGS = {
  hoplite: tasset, hauberk: tasset, sol: tasset, cataphract: tasset, wingward: tasset,
  kettle: perLeg, sentinel: perLeg,
  wayfarer: () => {
    const n = L(DEPTH.legs), f = L(DEPTH.legs);
    n.fill(poly([[7, 61], [19, 61], [19, 82], [6, 80]]), quilt(M.linen, 4));
    f.fill(poly([[21, 61], [32, 61], [33, 80], [21, 82]]), quilt(M.linen, 4, -1));
    return [{ rig: 'LegsBack', frame: 'legBack', layers: [n] }, { rig: 'LegsFront', frame: 'legFront', layers: [f] }];
  },
  athanor: () => {
    const legs = [NEAR, FAR].map(S => { const l = L(DEPTH.legs), cx = legC(S); l.fill(rect(cx - 4, 62, cx + 3, 98), cyl(M.linen, cx, 5)); return l; });
    const apron = L(DEPTH.legs), m = new Mask();
    for (let x = 10; x <= 29; x++) { const bot = 90 + ((x * 5) % 3); for (let y = 61; y <= bot; y++) m.add(x, y); }
    apron.fill(m, (x, y) => tone(M.leatherDk, y > 86 ? 1 : x < 12 ? 4 : x > 27 ? 2 : 3));
    for (const [x, y] of [[14, 78], [15, 79], [24, 83], [25, 84], [20, 70]]) apron.dot(x, y, tone(M.leatherDk, 0));
    // The thighs go into the apron's layer too: the mock draws apron and trousers as one layer
    // with one outline, and an apron outlined alone hid the trousers down its sides.
    const thighs = legs.map(l => { const t = L(DEPTH.legs); for (const [k, c] of l.c) if (KY(k) <= 92) t.c.set(k, c); return t; });
    return [{ rig: 'LegsBack', frame: 'legBack', layers: [legs[0]] }, { rig: 'LegsFront', frame: 'legFront', layers: [legs[1]] },
            { rig: 'Tasset', frame: 'torso', layers: [...thighs, apron] }];
  },
};

// which rig layer each of a slot's mock layers becomes
const isSleeve = l => l.depth === DEPTH.nearSleeve || l.depth === DEPTH.farSleeve;
function pieces(set, slot) {
  let ls = set[slot] ? (slot === 'Legs' ? null : set[slot]()) : null;
  // SLEEVES go with the gloves: the rig has no sleeve layer, and a torso's arm layer would be the
  // gloves' own (one sprite each) - so Hauberk's mail sleeves are its gloves' upper half
  if (slot === 'Torso') ls = ls.filter(l => !isSleeve(l));
  if (slot === 'Gloves') ls = [...set.Torso().filter(isSleeve), ...ls];
  switch (slot) {
    case 'Head': return [{ rig: 'HeadArmor', frame: 'head', layers: ls, helm: true }];
    case 'Shoulders': return [{ rig: 'Shoulders', frame: 'torso', layers: ls.filter(l => !l.far) },
                              { rig: 'ShouldersBack', frame: 'torso', layers: ls.filter(l => l.far) }];
    case 'Torso': return [{ rig: 'TorsoArmor', frame: 'torso', layers: ls }];
    case 'Gloves': return [{ rig: 'GlovesFront', frame: 'armNear', layers: ls.filter(l => !l.far), elbow: true },
                           { rig: 'GlovesBack', frame: 'armFar', layers: ls.filter(l => l.far), elbow: true }];
    case 'Belt': return [{ rig: 'Belt', frame: 'torso', layers: ls }];
    case 'Legs': return LEGS[set.id](set);
    // the boots: the mock's near leg (x 11) is the rig's BACK leg, its far one (x 27) the FRONT
    case 'Boots': return [{ rig: 'BootsBack', frame: 'legBack', layers: ls.filter(l => !l.far) },
                          { rig: 'BootsFront', frame: 'legFront', layers: ls.filter(l => l.far) }];
    case 'Back': return [{ rig: 'Back', frame: 'torso', layers: ls.filter(l => l.depth === DEPTH.back) },
                         { rig: 'BackOver', frame: 'torso', layers: ls.filter(l => l.depth !== DEPTH.back) }];
  }
}

// flatten a piece's mock layers (depth order, far shade) into one colour grid - no outline: the
// game strokes its own, the same 4 texels the mock draws
function flatten(layers) {
  const g = new Map();
  for (const l of [...layers].sort((a, b) => a.depth - b.depth))
    for (const [k, c] of l.c) g.set(k, l.far ? scale(c, FAR_SHADE) : c);
  return g;
}

const CHARS = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&*+-/:;<=>?@^_~';

// A layer drawn with a thinner edge than gear's (Wingward's circlet takes the hair's 2): the game's
// outline is one weight, so the ring is baked into the grid and the game's own is switched off.
function ringed(grid, R) {
  const out = new Map(grid);
  for (const k of grid.keys()) {
    const x = KX(k), y = KY(k);
    for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) {
      if (dx * dx + dy * dy > R * R + 2) continue;
      const kk = K(x + dx, y + dy); if (!grid.has(kk)) out.set(kk, OUTLINE);
    }
  }
  return out;
}

// THE HELM SEEN FROM BEHIND (GearItem.HelmBack): the same silhouette with nothing of the FACE in it.
// Inside the face (FACE, the bare head's face in mock coords) a row the helm spans past both sides
// is closed - the back of the helm - and anything else there (a mask, a bevor, a chin strap, eyes
// seen through slits) is dropped, so the hair shows. The fill is the helm's main colour outside the
// face, a step darker toward the nape.
const FACE = { x0: 4, x1: 33, y0: 12, y1: 29 };
const inFace = (x, y) => x >= FACE.x0 && x <= FACE.x1 && y >= FACE.y0 && y <= FACE.y1;
function backOf(layers) {
  const front = flatten(layers.filter(l => l.outline !== false));
  const count = new Map();
  for (const [k, c] of front) if (!inFace(KX(k), KY(k))) count.set(c, (count.get(c) || 0) + 1);
  const main = [...count].sort((a, b) => b[1] - a[1])[0][0];
  const g = new Map(), closed = [];
  for (const [k, c] of front) if (!inFace(KX(k), KY(k))) g.set(k, c);
  const span = new Map();
  for (const k of front.keys()) { const y = KY(k), x = KX(k), r = span.get(y) || [1e9, -1e9]; r[0] = Math.min(r[0], x); r[1] = Math.max(r[1], x); span.set(y, r); }
  for (const [y, [a, b]] of span) {
    if (y < FACE.y0 || y > FACE.y1 || a >= FACE.x0 || b <= FACE.x1) continue;
    for (let x = a; x <= b; x++) if (inFace(x, y) || !g.has(K(x, y))) { if (x > a && x < b) closed.push([x, y]); }
  }
  // rows above the face the helm already spans: close any gap there too
  for (const [y, [a, b]] of span) if (y < FACE.y0) for (let x = a + 1; x < b; x++) if (!g.has(K(x, y))) closed.push([x, y]);
  if (closed.length) {
    // shaded as the mock's plates are (grad): lit on the left third, darker on the right
    const ys = closed.map(p => p[1]), mid = (Math.min(...ys) + Math.max(...ys)) / 2;
    const xs = closed.map(p => p[0]), xa = Math.min(...xs), xw = Math.max(...xs) - xa + 1;
    for (const [x, y] of closed) {
      const t = (x + 0.5 - xa) / xw, col = t < 0.22 ? 1.12 : t > 0.7 ? 0.86 : 1;
      g.set(K(x, y), scale(main, col * (y > mid ? 0.86 : 1)));
    }
  }
  return g;
}

function gridRows(grid, x0, y0, w, h, what) {
  const pal = new Map(), rows = [];
  for (let y = y0; y < y0 + h; y++) {
    let s = '';
    for (let x = x0; x < x0 + w; x++) {
      const c = grid.get(K(x, y));
      if (!c) { s += '.'; continue; }
      if (!pal.has(c)) { if (pal.size >= CHARS.length) throw new Error('too many colours in ' + what); pal.set(c, CHARS[pal.size]); }
      s += pal.get(c);
    }
    rows.push(s);
  }
  return { rows, pal: [...pal].map(([c, ch]) => ch + c.slice(1).toUpperCase()).join(' ') };
}

function bakeLayer(p) {
  let grid = flatten(p.layers);
  if (!grid.size) return null;
  if (p.elbow) {   // undo the stretch the mock was drawn on
    const g2 = new Map();
    for (const [k, c] of grid) {
      const x = KX(k), y = KY(k);
      if (y >= ELBOW_ART && y < ELBOW_ART + ELBOW_EXTRA) continue;
      g2.set(K(x, y >= ELBOW_ART + ELBOW_EXTRA ? y - ELBOW_EXTRA : y), c);
    }
    grid = g2;
  }
  let back = p.helm ? backOf(p.layers) : null;
  const thin = p.layers.length && p.layers.every(l => l.outR && l.outR < OUT_R) ? p.layers[0].outR : 0;
  if (thin) { grid = ringed(grid, thin); if (back) back = ringed(back, thin); }
  const box = gridBox(back ? new Map([...grid, ...back]) : grid);
  let { x0, y0, x1, y1 } = box;
  let w = x1 - x0 + 1, h = y1 - y0 + 1;
  if (w % 2) w++;     // widths and heights even (the rig's grid rule)
  if (h % 2) h++;
  const front = gridRows(grid, x0, y0, w, h, p.rig);
  const [px, py] = PIVOT[p.frame];
  return { rig: p.rig, rows: front.rows, pal: front.pal, back: back && gridRows(back, x0, y0, w, h, p.rig + ' back'),
           bare: p.rig === 'BackOver' || !!thin,
           offX: (x0 + w / 2 - px) / 2, offY: (py - (y0 + h / 2)) / 2 };
}

const SLOT_NAMES = {
  hoplite: { Head: 'Helm', Shoulders: 'Shoulder Discs', Torso: 'Cuirass', Gloves: 'Bracers', Belt: 'Belt', Legs: 'Pteruges', Boots: 'Greaves', Back: 'Cape' },
  wayfarer: { Head: 'Cap', Shoulders: 'Spaulders', Torso: 'Gambeson', Gloves: 'Wraps', Belt: 'Sash', Legs: 'Breeches', Boots: 'Puttees', Back: 'Bedroll' },
  athanor: { Head: 'Goggled Cap', Shoulders: 'Spaulders', Torso: 'Apron Bib', Gloves: 'Gauntlets', Belt: 'Vial Belt', Legs: 'Apron', Boots: 'Capped Boots', Back: 'Pack' },
  hauberk: { Head: 'Coif', Shoulders: 'Spaulders', Torso: 'Surcoat', Gloves: 'Mail Sleeves', Belt: 'Belt', Legs: 'Skirt', Boots: 'Chausses', Back: 'Cloak' },
  kettle: { Head: 'Hat', Shoulders: 'Spaulders', Torso: 'Brigandine', Gloves: 'Half Gauntlets', Belt: 'Dagger Belt', Legs: 'Hose', Boots: 'Riding Boots', Back: 'Capelet' },
  sentinel: { Head: 'Sallet', Shoulders: 'Pauldrons', Torso: 'Breastplate', Gloves: 'Gauntlets', Belt: 'Belt', Legs: 'Tassets', Boots: 'Sabatons', Back: 'Cape' },
  sol: { Head: 'Corinthian', Shoulders: 'Pauldrons', Torso: 'Breastplate', Gloves: 'Gauntlets', Belt: 'Sash', Legs: 'Tabard', Boots: 'Greaves', Back: 'Cape' },
  cataphract: { Head: 'Mask', Shoulders: 'Lamellar Wings', Torso: 'Lamellar', Gloves: 'Vambraces', Belt: 'Belt', Legs: 'Skirt', Boots: 'Boots', Back: 'Cloak' },
  wingward: { Head: 'Circlet', Shoulders: 'Pauldrons', Torso: 'Scale', Gloves: 'Vambraces', Belt: 'Belt', Legs: 'Feather Skirt', Boots: 'Greaves', Back: 'Mantle' },
};
// helms that wrap the jaw: no hair escapes them (GearItem.SealsHead)
const SEALED = { hauberk: true, cataphract: true };

const f = n => (Math.round(n * 1000) / 1000).toString() + 'f';
let cs = '';
for (const id of want) {
  const set = SETS.find(s => s.id === id);
  for (const slot of SLOTS) {
    const layers = pieces(set, slot).map(bakeLayer).filter(Boolean);
    if (!layers.length) continue;
    const itemId = `tier_${id}_${slot.toLowerCase()}`;
    const still = slot === 'Back' && (id === 'wayfarer' || id === 'athanor');   // a bedroll, a pack: they do not swing
    const grid = (l, rows, pal, key) => `                ${l.bare ? 'GridBare' : 'Grid'}(RigLayer.${l.rig}, "${key}", ${f(l.offX)}, ${f(l.offY)}, "${pal}",\n` +
      rows.map(r => `                    "${r}"`).join(',\n') + ')';
    const look = `Look("${itemId}", "${set.name} ${SLOT_NAMES[id][slot]}", GearSlot.${slot}, LootTier.${set.tier},\n` +
      layers.map(l => grid(l, l.rows, l.pal, `${itemId}.${l.rig.toLowerCase()}`)).join(',\n') + ')';
    const helm = layers[0].back;
    cs += `            items.Add(${still ? `Still(${look})` : helm
      ? `Helm(${look},\n${grid(layers[0], helm.rows, helm.pal, itemId + '.back')}, seals: ${!!SEALED[id]})` : look});\n\n`;
  }
}
fs.writeFileSync(out, cs);
console.log('baked', want.join(' '), cs.length, 'chars');
