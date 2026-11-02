// node: cat body.js kit.js sets.js wkit.js swords.js discs.js bows.js wbake.js | node - <out> <setIds,comma>
// Bakes the approved tier weapons into the game's conventions: grids at weapon density (75, doubled to
// 150 by Pixels' upscale2x), pivoted where the game's own weapons are - a greatsword on its grip
// (GripCentre: the grip's top row + 6, the grip's second column), a disc on its centre (14, 14), a
// bow on its stave at the middle row with the string and nocks given separately (Strung).
const fs = require('fs');
const out = process.argv[2];
const want = (process.argv[3] || 'hoplite,wayfarer,athanor').split(',');
const TIER = { hoplite: 'Bronze', wayfarer: 'Bronze', athanor: 'Bronze', hauberk: 'Silver', kettle: 'Silver', sentinel: 'Silver', sol: 'Gold', cataphract: 'Gold', wingward: 'Gold' };
const CHARS = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&*+-/:;<=>?@^_~';

function flatten(layers) {
  const g = new Map();
  for (const l of [...layers].sort((a, b) => a.depth - b.depth)) for (const [k, c] of l.c) g.set(k, c);
  return g;
}
// a grid over at least [bx0..bx1] x [by0..by1], grown to hold everything, width even
function rowsOf(grid, bx0, by0, bx1, by1) {
  const b = gridBox(grid);
  let x0 = Math.min(bx0, b.x0), y0 = Math.min(by0, b.y0), x1 = Math.max(bx1, b.x1), y1 = Math.max(by1, b.y1);
  if ((x1 - x0 + 1) % 2) x1++;
  const pal = new Map(), rows = [];
  for (let y = y0; y <= y1; y++) {
    let s = '';
    for (let x = x0; x <= x1; x++) {
      const c = grid.get(K(x, y));
      if (!c) { s += '.'; continue; }
      if (!pal.has(c)) { if (pal.size >= CHARS.length) throw new Error('too many colours'); pal.set(c, CHARS[pal.size]); }
      s += pal.get(c);
    }
    rows.push(s);
  }
  return { rows, x0, y0, pal };
}
const palStr = pal => [...pal].map(([c, ch]) => ch + c.slice(1).toUpperCase()).join(' ');
// one palette over several grids (strung and unstrung share it)
function rowsShared(grids, box) {
  const all = new Map(); for (const g of grids) for (const [k, c] of g) all.set(k, c);
  const base = rowsOf(all, ...box);
  return grids.map(g => {
    const r = [];
    for (let y = base.y0; y < base.y0 + base.rows.length; y++) {
      let s = '';
      for (let x = base.x0; x < base.x0 + base.rows[0].length; x++) { const c = g.get(K(x, y)); s += c ? base.pal.get(c) : '.'; }
      r.push(s);
    }
    return r;
  }).concat([base]);
}
const q = s => `                    "${s}"`;
let cs = '';
for (const id of want) {
  const tier = TIER[id];
  // ---- the greatsword
  {
    const ls = SWORDS[id].draw(), grip = ls.find(l => { const b = l.mask().bbox(); return b.w === 4 && b.h === 12; });
    const gb = grip.mask().bbox();
    const g = rowsOf(flatten(ls), 0, 0, 15, 78);
    const px = gb.x0 + 1 - g.x0, py = gb.y0 + 6 - g.y0;
    cs += `            items.Add(TierSword("tier_${id}_greatsword", "${SWORDS[id].name}", LootTier.${tier}, ${px}, ${py}, "${palStr(g.pal)}",\n${g.rows.map(q).join(',\n')}));\n\n`;
  }
  // ---- the discs
  {
    const g = rowsOf(flatten(DISCS[id].draw()), 0, 0, 27, 27);
    cs += `            items.Add(TierDisc("tier_${id}_disc", "${DISCS[id].name}", LootTier.${tier}, ${DC - g.x0}, ${DC - g.y0}, "${palStr(g.pal)}",\n${g.rows.map(q).join(',\n')}));\n\n`;
  }
  // ---- the bow: strung and unstrung, one palette
  {
    const ls = BOWS[id].draw();
    const str = ls.find(l => l.outline === false);
    const body = ls.filter(l => l !== str);
    const strung = flatten(ls), bare = flatten(body);
    const [sRows, uRows, base] = rowsShared([strung, bare], [0, 0, 1, 78]);
    // the stave's centre on the middle row: the grip
    const stave = body.find(l => l.depth === 1); let a = 1e9, b = -1e9;   // every bow draws its stave at depth 1 let a = 1e9, b = -1e9;
    for (const [k] of stave.c) if (KY(k) === BOW_CY) { a = Math.min(a, KX(k)); b = Math.max(b, KX(k)); }
    const px = Math.floor((a + b + 1) / 2) - base.x0, py = BOW_CY - base.y0;
    // the string: its column and top row
    let sx = 1e9, sy = 1e9, col = null;
    for (const [k, c] of str.c) { sx = Math.min(sx, KX(k)); sy = Math.min(sy, KY(k)); col = c; }
    cs += `            items.Add(TierBow("tier_${id}_bow", "${BOWS[id].name}", LootTier.${tier}, ${px}, ${py}, ${sx - base.x0 + 0.5}f, ${sy - base.y0}f, "${col.toUpperCase()}", "${palStr(base.pal)}",\n` +
          `                new[]\n                {\n${sRows.map(s => '    ' + q(s)).join(',\n')}\n                },\n` +
          `                new[]\n                {\n${uRows.map(s => '    ' + q(s)).join(',\n')}\n                }));\n\n`;
  }
}
fs.writeFileSync(out, cs);
console.log('baked weapons', want.join(' '), cs.length);
