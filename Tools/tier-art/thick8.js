// WEAPON THICKNESS MOCK (2026-11-12): the Bronze weapons with a thickness, against today's paper-thin cards.
// Each weapon keeps its approved flat art; every texel gets a half-thickness from the PART it belongs to (the kit's
// layers: blade, guard, grip, pommel, wrap, cloth, disc plate, grip bar, stave, string), and the card is drawn as a
// slab of texel columns that deep, ray-marched through the game's oblique camera (Locomotion8: screen y = y - 0.45 z).
// Faces show the art; walls show the part's own colour (an outline texel takes the colour inside it), lit from the
// kit's upper left; a 1-texel outline goes round what the art's own outline does not already edge.
// node: cat body.js kit.js sets.js wkit.js swords.js discs.js bows.js thick8.js | node - <outDir>
const v3 = (x, y, z) => ({ x, y, z }), norm3 = q => { const l = Math.hypot(q.x, q.y, q.z) || 1; return v3(q.x / l, q.y / l, q.z / l); };
const cross3 = (a, b) => v3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
const DEPTH_Y = 0.45, OUT_DIR = process.argv[2] || '.';

// ---------------------------------------------------------------- variants (half-thickness, weapon texels)
const VARIANTS = {
  flat: { name: 'Today: flat card' },
  lean: { name: 'A: lean (as mocked)', blade: [0.3, 1.0], guard: 1.25, plate: 1.0, round: 0.8, stave: 0.7, grip: 0.85, wrap: 0.5, cloth: 0.35, string: 0.3 },
  thin: { name: 'A, thinner grip', blade: [0.3, 1.0], guard: 1.25, plate: 1.0, round: 0.8, stave: 0.7, grip: 0.55, wrap: 0.5, cloth: 0.35, string: 0.3 },
};

// ---------------------------------------------------------------- parts
// compose() as the kit does it, remembering which layer last wrote each texel (an outline ring belongs to its layer)
function composeRoles(layers, roleOf) {
  const grid = new Map(), role = new Map();
  const all = [...layers].sort((a, b) => a.depth - b.depth);
  for (const l of all) {
    const r = roleOf(l);
    if (l.outline) {
      const m = l.mask(), R = l.outR ?? 1;
      for (const [x, y] of m.pts()) for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) {
        if (dx * dx + dy * dy > R * R + 2) continue;
        const k = K(x + dx, y + dy); if (!m.s.has(k)) { grid.set(k, OUTLINE); role.set(k, r); }
      }
    }
    for (const [k, c] of l.c) { grid.set(k, c); role.set(k, r); }
  }
  return { grid, role };
}
const bboxOf = l => l.mask().bbox();
function swordRoles(id) {
  const ls = SWORDS[id].draw(), grip = ls.find(l => { const b = bboxOf(l); return b.w === 4 && b.h === 12; });
  const gy = bboxOf(grip).y0;
  return { ls, roleOf: l => l === ls[0] ? 'blade' : l === grip ? 'grip' : l.depth === 2 ? (bboxOf(l).y0 > gy ? 'pommel' : 'wrap')
    : l.depth === 4 ? 'wrap' : l.depth === 4.5 ? 'cloth' : (id === 'athanor' ? 'pommel' : 'guard') };
}
function discRoles(id) { const ls = DISCS[id].draw(); return { ls, roleOf: l => l.depth === 0.5 ? 'barX' : l.depth === 1 ? 'plate' : 'pommel' }; }
function bowRoles(id) { const ls = BOWS[id].draw(); return { ls, roleOf: l => l.depth === 1 ? 'stave' : l.depth === 5 ? 'string' : l.depth === 1.5 ? 'cloth' : 'pommel' }; }

// the weapon as texels: colour, part, and per variant a half-thickness
function build(kind, id) {
  const { ls, roleOf } = kind === 'sword' ? swordRoles(id) : kind === 'disc' ? discRoles(id) : bowRoles(id);
  const { grid, role } = composeRoles(ls, roleOf);
  let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
  for (const k of grid.keys()) { x0 = Math.min(x0, KX(k)); x1 = Math.max(x1, KX(k)); y0 = Math.min(y0, KY(k)); y1 = Math.max(y1, KY(k)); }
  const has = (x, y) => grid.has(K(x, y)), roleAt = (x, y) => role.get(K(x, y));
  // distance from a texel's centre to the nearest texel outside `inside`
  const dt = (x, y, inside) => { let best = 9; for (let dy = -8; dy <= 8; dy++) for (let dx = -8; dx <= 8; dx++) { if (inside(x + dx, y + dy)) continue; const d = Math.hypot(dx, dy) - 0.5; if (d < best) best = d; } return Math.max(0.5, best); };
  const span = (x, y, r, alongY) => { let a = alongY ? y : x, b = a; const at = (s) => alongY ? roleAt(x, s) === r : roleAt(s, y) === r; while (at(a - 1)) a--; while (at(b + 1)) b++; return [a, b + 1]; };
  const parts = {};
  for (const k of grid.keys()) {
    const x = KX(k), y = KY(k), r = role.get(k);
    const inPart = (i, j) => roleAt(i, j) === r;
    const d = dt(x, y, inPart), dAll = dt(x, y, has);
    // the ridge: the deepest point of this part nearby
    let R = d; for (let dy = -4; dy <= 4; dy++) for (let dx = -4; dx <= 4; dx++) if (inPart(x + dx, y + dy) && Math.abs(dx) + Math.abs(dy) <= 5) R = Math.max(R, dt(x + dx, y + dy, inPart));
    parts[k] = { r, d, dAll, R, rowSpan: span(x, y, r, false), colSpan: span(x, y, r, true), x, y };
  }
  const H = {};
  for (const vk of Object.keys(VARIANTS)) {
    const v = VARIANTS[vk], h = new Map();
    for (const k of grid.keys()) {
      if (vk === 'flat') { h.set(k, 0.5); continue; }
      const p = parts[k], cx = p.x + 0.5;
      let t;
      switch (p.r) {
        case 'blade': { const [a, b] = p.rowSpan, hw = (b - a) / 2, u = Math.min(1, Math.abs(cx - (a + b) / 2) / hw); t = v.blade[0] + (v.blade[1] - v.blade[0]) * (1 - u); break; }
        case 'grip': { const [a, b] = p.rowSpan, hw = (b - a) / 2, u = Math.min(0.95, Math.abs(cx - (a + b) / 2) / hw); t = v.grip * hw * Math.sqrt(1 - u * u); break; }
        case 'barX': { const [a, b] = p.colSpan, hw = (b - a) / 2, u = Math.min(0.95, Math.abs(p.y + 0.5 - (a + b) / 2) / hw); t = Math.max(v.plate + 0.4, hw * Math.sqrt(1 - u * u)); break; }
        case 'guard': t = v.guard * Math.min(1, 0.55 + 0.3 * p.d); break;
        case 'plate': t = v.plate * Math.min(1, Math.sqrt(p.dAll / 2)); break;
        case 'wrap': t = v.blade[1] + v.wrap * Math.min(1, 0.4 + 0.4 * p.dAll); break;
        case 'cloth': t = v.cloth; break;
        case 'string': t = v.string; break;
        default: { const d = Math.min(p.d, p.R); t = (p.r === 'stave' ? v.stave : v.round) * Math.sqrt(Math.max(0.1, d * (2 * p.R - d))); }   // pommel, stave, bulbs: round
      }
      h.set(k, Math.max(0.3, t));
    }
    H[vk] = h;
  }
  return { grid, role, H, box: { x0, y0, x1, y1 } };
}

// ---------------------------------------------------------------- render
const vadd = (a, b) => v3(a.x + b.x, a.y + b.y, a.z + b.z), vmul = (a, s) => v3(a.x * s, a.y * s, a.z * s), dot = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
const LIGHT_V = norm3(v3(-0.55, 0.65, 0.55)), F0 = LIGHT_V.z;
const rgbOf = h => [1, 3, 5].map(i => parseInt(h.substr(i, 2), 16));
const hexOf = (r, g, b) => '#' + [r, g, b].map(c => Math.max(0, Math.min(255, Math.round(c))).toString(16).padStart(2, '0')).join('');
function shade(c, f) {
  f = Math.round(Math.max(0.6, Math.min(1.2, f)) * 10) / 10;       // quantised (the GIF palette)
  if (f === 1) return c;
  const [r, g, b] = rgbOf(c);
  return f < 1 ? hexOf(r * f, g * f, b * f) : hexOf(r + (255 - r) * (f - 1) * 1.2, g + (255 - g) * (f - 1) * 1.2, b + (255 - b) * (f - 1) * 1.2);
}
// pose: the card's grip `pivot` (continuous art coordinates) at the origin; `along` = toward the art's top, `across`
// = toward the art's +x (both world, x right, y up, z toward the viewer); one weapon texel = 2 body texels
function render(w, vk, pivot, along, across) {
  along = norm3(along); across = norm3(vadd(across, vmul(along, -dot(across, along))));
  const Uh = across, Vh = vmul(along, -1), Nh = norm3(cross3(Uh, Vh)), K2 = 2;
  const h = w.H[vk], grid = w.grid, { x0, y0, x1, y1 } = w.box;
  const hAt = (tx, ty) => grid.has(K(tx, ty)) ? h.get(K(tx, ty)) : 0;
  let hmax = 0; for (const v of h.values()) hmax = Math.max(hmax, v);
  // world bbox of the slab
  let sx0 = 1e9, sx1 = -1e9, sy0 = 1e9, sy1 = -1e9;
  for (const X of [x0, x1 + 1]) for (const Y of [y0, y1 + 1]) for (const W of [-hmax, hmax]) {
    const p = vadd(vadd(vmul(Uh, (X - pivot[0]) * K2), vmul(Vh, (Y - pivot[1]) * K2)), vmul(Nh, W * K2));
    const sx = p.x, sy = p.y - DEPTH_Y * p.z;
    sx0 = Math.min(sx0, sx); sx1 = Math.max(sx1, sx); sy0 = Math.min(sy0, sy); sy1 = Math.max(sy1, sy);
  }
  const loc = q => v3(dot(q, Uh) / K2, dot(q, Vh) / K2, dot(q, Nh) / K2);
  const D = v3(0, DEPTH_Y, 1), Dl = loc(D);
  const step = 0.2 / Math.max(Math.abs(Dl.x), Math.abs(Dl.y), Math.abs(Dl.z), 1e-6);
  const out = new Map();   // key (screen col, row DOWN) -> colour
  for (let sy = Math.floor(sy0) - 1; sy <= Math.ceil(sy1) + 1; sy++) for (let sx = Math.floor(sx0) - 1; sx <= Math.ceil(sx1) + 1; sx++) {
    const A = loc(v3(sx + 0.5, sy + 0.5, 0));
    // t range where the ray is inside the slab's box (local coords)
    let tlo = -1e4, thi = 1e4;
    const clip = (a0, da, lo, hi) => { if (Math.abs(da) < 1e-9) { if (a0 < lo || a0 > hi) { tlo = 1; thi = 0; } return; } let t0 = (lo - a0) / da, t1 = (hi - a0) / da; if (t0 > t1) [t0, t1] = [t1, t0]; tlo = Math.max(tlo, t0); thi = Math.min(thi, t1); };
    clip(A.x, Dl.x, x0 - pivot[0], x1 + 1 - pivot[0]); clip(A.y, Dl.y, y0 - pivot[1], y1 + 1 - pivot[1]); clip(A.z, Dl.z, -hmax, hmax);
    if (tlo > thi) continue;
    let prev = null, hit = null;
    for (let t = thi + step * 0.5; t >= tlo - step * 0.5; t -= step) {
      const a = A.x + Dl.x * t, b = A.y + Dl.y * t, wz = A.z + Dl.z * t;
      const tx = Math.floor(pivot[0] + a), ty = Math.floor(pivot[1] + b);
      const ht = hAt(tx, ty);
      if (ht > 0 && Math.abs(wz) <= ht) { hit = { tx, ty, wz, prev }; break; }
      prev = { tx, ty, wz };
    }
    if (!hit) continue;
    let nl, wall = false;
    // a wall only where the ray was already within this column's depth before it reached the column
    if (hit.prev && (hit.prev.tx !== hit.tx || hit.prev.ty !== hit.ty) && Math.abs(hit.prev.wz) <= hAt(hit.tx, hit.ty)) { wall = true; nl = norm3(v3(hit.prev.tx - hit.tx, hit.prev.ty - hit.ty, 0)); }
    else { const gx = (hAt(hit.tx + 1, hit.ty) - hAt(hit.tx - 1, hit.ty)) / 2, gy = (hAt(hit.tx, hit.ty + 1) - hAt(hit.tx, hit.ty - 1)) / 2, s = Math.sign(hit.wz) || 1; nl = norm3(v3(-gx * s * 0.6, -gy * s * 0.6, s)); }
    let c = grid.get(K(hit.tx, hit.ty));
    if (c === OUTLINE && (wall || Math.abs(nl.z) < 0.6)) {
      // a wall of the outline: the part's own colour just inside it
      const r = w.role.get(K(hit.tx, hit.ty)); let best = null, bd = 9;
      for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) { const k = K(hit.tx + dx, hit.ty + dy), cc = grid.get(k); if (!cc || cc === OUTLINE) continue; const dd = Math.hypot(dx, dy) + (w.role.get(k) === r ? 0 : 1.5); if (dd < bd) { bd = dd; best = cc; } }
      if (best) c = best;
    }
    const nw = norm3(vadd(vadd(vmul(Uh, nl.x), vmul(Vh, nl.y)), vmul(Nh, nl.z)));
    const nv = Math.abs(nl.z) > 0.99 && !wall ? 1 : 1 + 0.55 * (dot(nw, LIGHT_V) - F0);
    out.set(K(sx, -sy - 1), shade(c, nv));
  }
  // an outline where the art's own does not edge the picture
  const ring = new Map();
  for (const [k, c] of out) { if (c === OUTLINE) continue; const x = KX(k), y = KY(k); for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const kk = K(x + dx, y + dy); if (!out.has(kk)) ring.set(kk, OUTLINE); } }
  for (const [k, c] of ring) out.set(k, c);
  return out;
}

// ---------------------------------------------------------------- poses

const turned = a => v3(Math.cos(deg(a)), 0, -Math.sin(deg(a)));
// the views mock's body frame (x the character's LEFT, y up, z forward) into the camera's, front and side (facing right)
const FRONT = p => v3(p.x, p.y, p.z), SIDE = p => v3(p.z, p.y, -p.x);
const swordTip = norm3(v3(0.72, -0.62, -0.3)), swordU = norm3(cross3(swordTip, v3(0, 0, 1)));
const bowRestDown = vmul(norm3(v3(0, 1, 0.65)), -1), bowRestU = (() => { const o = deg(25); let u = v3(Math.sin(o), 0, Math.cos(o)); return norm3(vadd(u, vmul(bowRestDown, -dot(u, bowRestDown)))); })();
const bowReadyDown = vmul(norm3(v3(0, 0.85, 0.5)), -1), bowReadyU = norm3(vadd(v3(0, 0, 1), vmul(bowReadyDown, -dot(v3(0, 0, 1), bowReadyDown))));
const POSES = {
  sword: [
    ['Face-on', v3(0, 1, 0), turned(0)], ['Turned 45°', v3(0, 1, 0), turned(45)], ['Turned 70°', v3(0, 1, 0), turned(70)], ['Edge-on', v3(0, 1, 0), turned(90)],
    ['Rest carry, front', FRONT(vmul(swordTip, -1)), FRONT(swordU)], ['Rest carry, side', SIDE(vmul(swordTip, -1)), SIDE(swordU)],
    ['Chop toward you', v3(0, 0.7, 0.7), v3(0, 0.7, -0.7)], ['Level sweep across', v3(1, 0, 0), v3(0, 0, 1)], ['Level sweep toward you', v3(0.3, 0, 1), v3(1, 0, -0.3)],
  ],
  disc: [
    ['Face-on', v3(0, 1, 0), turned(0)], ['Turned 45°', v3(0, 1, 0), turned(45)], ['Turned 70°', v3(0, 1, 0), turned(70)], ['Edge-on (Wings, front)', v3(0, 1, 0), turned(90)],
    ['Thrown level', v3(0, 0, -1), v3(1, 0, 0)], ['Thrown, banked 30°', v3(0, Math.sin(deg(30)), -Math.cos(deg(30))), v3(1, 0, 0)],
  ],
  bow: [
    ['Face-on', v3(0, 1, 0), turned(0)], ['Turned 45°', v3(0, 1, 0), turned(45)], ['Turned 70°', v3(0, 1, 0), turned(70)], ['Edge-on (aimed up/down)', v3(0, 1, 0), turned(90)],
    ['Rest O1, front', FRONT(vmul(bowRestDown, -1)), FRONT(bowRestU)], ['Ready C1, side', SIDE(vmul(bowReadyDown, -1)), SIDE(bowReadyU)],
  ],
};
const SETS3 = ['hoplite', 'wayfarer', 'athanor'];
function pivotOf(kind, id, w) {
  if (kind === 'disc') return [DC, DC];
  if (kind === 'sword') { const ls = SWORDS[id].draw(), g = ls.find(l => { const b = bboxOf(l); return b.w === 4 && b.h === 12; }); const b = bboxOf(g); return [b.x0 + 2, b.y0 + 6]; }
  let xs = []; for (const k of w.grid.keys()) if (KY(k) === BOW_CY && w.grid.get(k) !== OUTLINE && w.role.get(k) === 'stave') xs.push(KX(k));
  return [(Math.min(...xs) + Math.max(...xs) + 1) / 2, BOW_CY + 0.5];
}

// ---------------------------------------------------------------- output
const io = require('/private/tmp/claude-501/-Users-marshoward-Coalescence/608fafe2-eefa-4ba2-97ff-090f4e6992e1/scratchpad/pngio.js');
const fsx = require('fs');
const BGC = '#4a4e57', VK = Object.keys(VARIANTS);
function boxOf(m) { let a = 1e9, b = 1e9, c = -1e9, d = -1e9; for (const k of m.keys()) { a = Math.min(a, KX(k)); c = Math.max(c, KX(k)); b = Math.min(b, KY(k)); d = Math.max(d, KY(k)); } return { a, b, c, d }; }
function writePng(file, W, H, img) { const buf = Buffer.alloc(W * H * 3); img.forEach((c, i) => { const [r, g, b] = rgbOf(c); buf[i * 3] = r; buf[i * 3 + 1] = g; buf[i * 3 + 2] = b; }); io.write(file, W, H, buf); }
function blit(img, W, S, m, ox, oy) { for (const [k, c] of m) { const x = KX(k) + ox, y = KY(k) + oy; for (let dy = 0; dy < S; dy++) for (let dx = 0; dx < S; dx++) { const px = x * S + dx, py = y * S + dy; if (px >= 0 && py >= 0 && px < W && py * W + px < img.length) img[py * W + px] = c; } } }
const manifest = { variants: VK.map(k => VARIANTS[k].name), sheets: {}, gifs: {} };
for (const kind of ['sword', 'disc', 'bow']) for (const id of SETS3) {
  const w = build(kind, id), pv = pivotOf(kind, id, w), poses = POSES[kind];
  const cells = poses.map(([, al, ac]) => VK.map(vk => render(w, vk, pv, al, ac)));
  // column widths from the widest variant; each column centred on its flat render's box
  const pad = 6, cols = cells.map(col => { const bs = col.map(boxOf); const cw = Math.max(...bs.map(b => b.c - b.a + 1)) + pad * 2, ch = Math.max(...bs.map(b => b.d - b.b + 1)); return { cw, ch, bs }; });
  const CH = Math.max(...cols.map(c => c.ch)) + pad * 2, S = 2;
  const W = cols.reduce((s, c) => s + c.cw, 0) * S, H = CH * VK.length * S, img = new Array(W * H).fill(BGC);
  let x = 0;
  cols.forEach((c, ci) => {
    const f = c.bs[0];
    VK.forEach((vk, ri) => blit(img, W, S, cells[ci][ri], x + Math.round(c.cw / 2 - (f.a + f.c) / 2), ri * CH + Math.round(CH / 2 - (f.b + f.d) / 2)));
    x += c.cw;
  });
  const file = `${kind}-${id}.png`; writePng(`${OUT_DIR}/${file}`, W, H, img);
  manifest.sheets[`${kind}-${id}`] = { file, W, H, cols: cols.map((c, i) => ({ label: poses[i][0], w: c.cw * S })), rowH: CH * S };
  // turntable: round the long axis, the three variants side by side
  const FR = 36, turns = [];
  for (let fi = 0; fi < FR; fi++) turns.push(VK.map(vk => render(w, vk, pv, v3(0, 1, 0), turned(fi * 360 / FR))));
  const tb = boxOf(turns[0][0]); const TW = (tb.c - tb.a + 1) + 2 * pad + 8, TH = (tb.d - tb.b + 1) + 2 * pad, GS = 2;
  const GW = TW * VK.length * GS, GH = TH * GS, frames = turns.map(fr => { const g = new Array(GW * GH).fill(BGC); fr.forEach((m, i) => blit(g, GW, GS, m, i * TW + Math.round(TW / 2 - (tb.a + tb.c) / 2), Math.round(TH / 2 - (tb.b + tb.d) / 2))); return g; });
  const gf = `${kind}-${id}-turn.gif`; gif(frames, GW, GH, 8, `${OUT_DIR}/${gf}`);
  manifest.gifs[`${kind}-${id}`] = { file: gf, W: GW, H: GH };
  process.stderr.write(`${kind} ${id}\n`);
}
fsx.writeFileSync(`${OUT_DIR}/manifest.json`, JSON.stringify(manifest, null, 1));

function gif(frames, W, H, delay, file) {
  const pal = new Map(); for (const f of frames) for (const c of f) if (!pal.has(c)) pal.set(c, pal.size);
  if (pal.size > 256) throw new Error('palette ' + pal.size + ' ' + file);
  const cols = [...pal.keys()]; while (cols.length < 256) cols.push('#000000');
  const o = [], w16 = v => [v & 255, v >> 8];
  o.push(...Buffer.from('GIF89a'), ...w16(W), ...w16(H), 0xF7, 0, 0); for (const c of cols) o.push(...rgbOf(c));
  o.push(0x21, 0xFF, 0x0B, ...Buffer.from('NETSCAPE2.0'), 3, 1, 0, 0, 0);
  for (const f of frames) {
    o.push(0x21, 0xF9, 4, 0, ...w16(delay), 0, 0, 0x2C, 0, 0, 0, 0, ...w16(W), ...w16(H), 0);
    let size = 9, next = 258, dict = new Map(), bits = 0, acc = 0; const bytes = [];
    const emit = code => { acc |= code << bits; bits += size; while (bits >= 8) { bytes.push(acc & 255); acc >>>= 8; bits -= 8; } };
    emit(256); let cur = pal.get(f[0]);
    for (let i = 1; i < f.length; i++) { const k = pal.get(f[i]), kk = cur * 4096 + k;
      if (dict.has(kk)) { cur = dict.get(kk); continue; } emit(cur);
      if (next < 4096) { dict.set(kk, next++); if (next > (1 << size) && size < 12) size++; } else { emit(256); dict = new Map(); size = 9; next = 258; }
      cur = k; }
    emit(cur); emit(257); if (bits > 0) bytes.push(acc & 255);
    o.push(8); for (let i = 0; i < bytes.length; i += 255) { const ch = bytes.slice(i, i + 255); o.push(ch.length, ...ch); } o.push(0);
  }
  o.push(0x3B); fsx.writeFileSync(file, Buffer.from(o));
}
