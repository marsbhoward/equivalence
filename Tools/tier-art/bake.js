// node: cat body.js kit.js sets.js bake.js | node - <out.cs> <setIds,comma>
// Bakes the approved tier-armour mock into the game's rig layers: each piece's mock layers become
// grids at the body's density (150), placed against the joint the layer hangs from. Mock coords
// are the bare body's capture: the rig root at (19, 56), y down. Pivots measured from the rig.
const fs = require('fs');
const out = process.argv[2];
const want = (process.argv[3] || 'hoplite,wayfarer,athanor').split(',');

const PIVOT = {
  torso: [19, 72], head: [19, 28], armNear: [5, 30], armFar: [33, 30], legBack: [11, 72], legFront: [27, 72],
};
const ELBOW_ART = 44, ELBOW_EXTRA = 6;   // mock rows 44..49 are the rig's own stretch of the upper arm

// ---- per-set overrides where the mock's one layer is two rig layers in the game ----
const LEGS = {
  hoplite: set => [{ rig: 'Tasset', frame: 'torso', layers: set.Legs() }],
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
function pieces(set, slot) {
  const ls = set[slot] ? (slot === 'Legs' ? null : set[slot]()) : null;
  switch (slot) {
    case 'Head': return [{ rig: 'HeadArmor', frame: 'head', layers: ls }];
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
  let { x0, y0, x1, y1 } = gridBox(grid);
  let w = x1 - x0 + 1, h = y1 - y0 + 1;
  if (w % 2) w++;     // widths and heights even (the rig's grid rule)
  if (h % 2) h++;
  const pal = new Map();
  const rows = [];
  for (let y = y0; y < y0 + h; y++) {
    let s = '';
    for (let x = x0; x < x0 + w; x++) {
      const c = grid.get(K(x, y));
      if (!c) { s += '.'; continue; }
      if (!pal.has(c)) { if (pal.size >= CHARS.length) throw new Error('too many colours in ' + p.rig); pal.set(c, CHARS[pal.size]); }
      s += pal.get(c);
    }
    rows.push(s);
  }
  const [px, py] = PIVOT[p.frame];
  return { rig: p.rig, rows, bare: p.rig === 'BackOver', pal: [...pal].map(([c, ch]) => ch + c.slice(1).toUpperCase()).join(' '),
           offX: (x0 + w / 2 - px) / 2, offY: (py - (y0 + h / 2)) / 2 };
}

const SLOT_NAMES = {
  hoplite: { Head: 'Helm', Shoulders: 'Shoulder Discs', Torso: 'Cuirass', Gloves: 'Bracers', Belt: 'Belt', Legs: 'Pteruges', Boots: 'Greaves', Back: 'Cape' },
  wayfarer: { Head: 'Cap', Shoulders: 'Spaulders', Torso: 'Gambeson', Gloves: 'Wraps', Belt: 'Sash', Legs: 'Breeches', Boots: 'Puttees', Back: 'Bedroll' },
  athanor: { Head: 'Goggled Cap', Shoulders: 'Spaulders', Torso: 'Apron Bib', Gloves: 'Gauntlets', Belt: 'Vial Belt', Legs: 'Apron', Boots: 'Capped Boots', Back: 'Pack' },
};

const f = n => (Math.round(n * 1000) / 1000).toString() + 'f';
let cs = '';
for (const id of want) {
  const set = SETS.find(s => s.id === id);
  for (const slot of SLOTS) {
    const layers = pieces(set, slot).map(bakeLayer).filter(Boolean);
    if (!layers.length) continue;
    const itemId = `tier_${id}_${slot.toLowerCase()}`;
    const still = slot === 'Back' && id !== 'hoplite';   // a bedroll, a pack: they do not swing
    cs += `            items.Add(${still ? 'Still(' : ''}Look("${itemId}", "${set.name} ${SLOT_NAMES[id][slot]}", GearSlot.${slot}, LootTier.${set.tier},\n`;
    cs += layers.map(l => `                ${l.bare ? 'GridBare' : 'Grid'}(RigLayer.${l.rig}, "${itemId}.${l.rig.toLowerCase()}", ${f(l.offX)}, ${f(l.offY)}, "${l.pal}",\n` +
      l.rows.map(r => `                    "${r}"`).join(',\n')).join('),\n') + `))${still ? ')' : ''});\n\n`;
  }
}
fs.writeFileSync(out, cs);
console.log('baked', want.join(' '), cs.length, 'chars');
