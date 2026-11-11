  // ---- arms + the bow + the quiver (bow8.js). The bow is in the LEFT hand (side -1), the string hand the right (side 1),
  // as the game holds it. atk(f, r, up, phase) -> { hand: { 1: point|null, '-1': point|null }, bow: { grip, A, B, pull }, arrow }
  const WOOD = ramp('#8a5a32', { sheen: '#e0b07a', lift: 0.32 }), STRING = '#e8e2cc', FEATHER = ramp('#c8c0a8', { sheen: '#ffffff' });
  const B8 = atk ? atk(f, r, up, phase) : null;
  const norm = v => { const l = len(v) || 1; return mul(v, 1 / l); };
  const dot = (p, q) => p.x * q.x + p.y * q.y + p.z * q.z;
  const armSwing8 = ph => G.arm * Math.sin(2 * Math.PI * ph);
  for (const side of [1, -1]) {
    const sh = add(up(shoulderH - 3), mul(r, side * 15));
    const target = B8 && B8.hand[side];
    let el, hand, d2;
    if (target) {
      const U1 = 21, U2 = 20;
      let d = sub(target, sh), D = len(d); hand = target;
      if (D > U1 + U2 - 0.01) { d = mul(d, (U1 + U2 - 0.01) / D); D = U1 + U2 - 0.01; hand = add(sh, d); }
      const n = norm(d), x = (U1 * U1 - U2 * U2 + D * D) / (2 * D), h = Math.sqrt(Math.max(0, U1 * U1 - x * x));
      const hint = add(add(mul(UP, -1), mul(f, -0.4)), mul(r, side * 0.8));
      const pole = norm(sub(hint, mul(n, dot(hint, n))));
      el = add(add(sh, mul(n, x)), mul(pole, h));
      d2 = norm(sub(hand, el));
    } else {
      const sw = (side === 1 ? 1 : -1) * armSwing8(phase) * Math.PI / 180;
      const d1 = add(mul(UP, -Math.cos(sw)), mul(f, Math.sin(sw)));
      el = add(sh, add(mul(d1, 21), mul(r, side * 1.5)));
      const b = sw + G.elbow * Math.PI / 180;
      d2 = add(mul(UP, -Math.cos(b)), mul(f, Math.sin(b)));
      hand = add(add(el, add(mul(d2, 17), mul(r, -side * 2.5))), mul(d2, 3));
    }
    const wr = sub(hand, mul(d2, 3));
    part((sh.z + el.z) / 2 + 0.05, c => capsule(c, P(sh), P(el), 4.2, (u, s) => tone(M.under, shadeIdx(s))));
    part((el.z + wr.z) / 2 + 0.06, c => {
      capsule(c, P(el), P(wr), 3.8, (u, s) => tone(M.leather, shadeIdx(s) + ([0.2, 0.55, 0.85].some(q => Math.abs(u - q) < 0.06) ? 2 : 0)));
      disc(c, P(hand), 4.2, 4.2, (dx, dy) => tone(M.leatherDk, shadeIdx(dx / 4) + (dy < -1 ? 1 : 0)));
    });
    part(sh.z + 0.2, c => disc(c, P(add(sh, V(0, 2, 0))), 8, 6.5, (dx, dy) => { const q = (dx / 8) ** 2 + (dy / 6.5) ** 2; return tone(q > 0.62 ? M.bronzeDk : M.bronze, q > 0.62 ? (dy < 0 ? 4 : 2) : dy < -2 && dx < 1 ? 5 : shadeIdx(dx / 8) + 1); }));
  }
  // the quiver on the back, its mouth over the left shoulder, fletchings out of it (as the game wears it)
  {
    const mouth = add(add(up(38), mul(r, -8)), mul(f, -10)), foot = add(add(up(6), mul(r, 7)), mul(f, -11));
    const ax = norm(sub(mouth, foot));
    part((mouth.z + foot.z) / 2 - 0.4, c => capsule(c, P(foot), P(mouth), 4, (u, s) => tone(M.leatherDk, shadeIdx(s) + (u > 0.9 ? 1 : 0))));
    for (const k of [-2.5, 0, 2.5]) { const t0 = add(add(mouth, mul(r, k)), mul(ax, 1)), t1 = add(t0, mul(ax, 9));
      part(mouth.z - 0.39, c => capsule(c, P(t0), P(t1), 1.6, (u, s) => tone(FEATHER, u > 0.4 ? 4 : 2))); }
  }
  // the bow: a stave arcing away from its string in the bow's plane (A along it, B toward the string), the string
  // straight tip to tip or pulled to the string hand; length 140 as the game's longbows; a nocked arrow when drawn
  if (B8 && B8.bow) {
    const { grip, A, B, pull, slung } = B8.bow, h = 70, c = 13;
    const at = s => add(add(grip, mul(A, s)), mul(B, c * (s / h) ** 2));
    const N = 10;
    for (let i = 0; i < N; i++) { const s0 = -h + 2 * h * i / N, s1 = -h + 2 * h * (i + 1) / N, p0 = at(s0), p1 = at(s1), w = Math.abs((s0 + s1) / 2) < 10 ? 2.6 : 1.9;
      part((p0.z + p1.z) / 2 + 0.3, cc => capsule(cc, P(p0), P(p1), w, (u, s) => tone(WOOD, shadeIdx(s) + (Math.abs((s0 + s1) / 2) < 10 ? -1 : 0)))); }
    const top = at(h), bot = at(-h);
    const pts = slung ? [top, ...slung, bot] : pull ? [top, pull, bot] : [top, bot];
    for (let i = 1; i < pts.length; i++) { const a = pts[i - 1], b = pts[i];
      part((a.z + b.z) / 2 + 0.31, cc => { const A2 = P(a), B2 = P(b), n = Math.ceil(Math.hypot(B2.x - A2.x, B2.y - A2.y)) || 1;
        for (let k = 0; k <= n; k++) cc.set(key(Math.round(A2.x + (B2.x - A2.x) * k / n), Math.round(A2.y + (B2.y - A2.y) * k / n)), STRING); }); }
    if (B8.arrow) { const { from, to } = B8.arrow;
      part((from.z + to.z) / 2 + 0.32, cc => capsule(cc, P(from), P(to), 1.1, (u, s) => u > 0.92 ? tone(M.bronze, 4) : u < 0.12 ? tone(FEATHER, 4) : tone(WOOD, 3))); }
  }

