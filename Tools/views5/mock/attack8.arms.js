  // ---- arms + a held greatsword (attack8.js): both hands on the grip, each arm a two-bone reach from its shoulder
  const STEEL = ramp('#b9bdc6', { sheen: '#ffffff', lift: 0.40 });
  const A = atk ? atk(f, r, up) : { grip: add(up(10), mul(r, 15)), dir: V(0, -1, 0) };
  const norm = v => { const l = len(v) || 1; return mul(v, 1 / l); };
  const dot = (p, q) => p.x * q.x + p.y * q.y + p.z * q.z;
  const cross = (p, q) => V(p.y * q.z - p.z * q.y, p.z * q.x - p.x * q.z, p.x * q.y - p.y * q.x);
  for (const side of [1, -1]) {
    const sh = add(up(shoulderH - 3), mul(r, side * 15));
    let hand = side === 1 ? A.grip : add(A.grip, mul(A.dir, -6));
    const U1 = 21, U2 = 20;
    let d = sub(hand, sh), D = len(d);
    if (D > U1 + U2 - 0.01) { d = mul(d, (U1 + U2 - 0.01) / D); D = U1 + U2 - 0.01; hand = add(sh, d); }
    const n = norm(d), x = (U1 * U1 - U2 * U2 + D * D) / (2 * D), h = Math.sqrt(Math.max(0, U1 * U1 - x * x));
    const hint = add(add(mul(UP, -1), mul(f, -0.5)), mul(r, side * 0.7));
    const pole = norm(sub(hint, mul(n, dot(hint, n))));
    const el = add(add(sh, mul(n, x)), mul(pole, h));
    const fd = norm(sub(hand, el)), wr = sub(hand, mul(fd, 3));
    part((sh.z + el.z) / 2 + 0.05, c => capsule(c, P(sh), P(el), 4.2, (u, s) => tone(M.under, shadeIdx(s))));
    part((el.z + wr.z) / 2 + 0.06, c => {
      capsule(c, P(el), P(wr), 3.8, (u, s) => tone(M.leather, shadeIdx(s) + ([0.2, 0.55, 0.85].some(q => Math.abs(u - q) < 0.06) ? 2 : 0)));
      disc(c, P(hand), 4.2, 4.2, (dx, dy) => tone(M.leatherDk, shadeIdx(dx / 4) + (dy < -1 ? 1 : 0)));
    });
    part(sh.z + 0.2, c => disc(c, P(add(sh, V(0, 2, 0))), 8, 6.5, (dx, dy) => { const q = (dx / 8) ** 2 + (dy / 6.5) ** 2; return tone(q > 0.62 ? M.bronzeDk : M.bronze, q > 0.62 ? (dy < 0 ? 4 : 2) : dy < -2 && dx < 1 ? 5 : shadeIdx(dx / 8) + 1); }));
  }
  // the sword: pommel, grip, guard across, the blade in short lengths (each its own depth)
  {
    const g = A.grip, dir = norm(A.dir);
    let across = cross(dir, UP); if (len(across) < 0.2) across = cross(dir, f); across = norm(across);
    const BL = A.len || 64, pommel = add(g, mul(dir, -10)), guard = add(g, mul(dir, 4)), tip = add(g, mul(dir, BL));
    part((pommel.z + guard.z) / 2 + 0.3, c => { capsule(c, P(pommel), P(guard), 1.8, (u, s) => tone(M.leatherDk, shadeIdx(s))); disc(c, P(pommel), 2.4, 2.4, () => tone(M.bronzeDk, 3)); });
    part(guard.z + 0.31, c => capsule(c, P(add(guard, mul(across, -7))), P(add(guard, mul(across, 7))), 1.7, (u, s) => tone(M.bronzeDk, shadeIdx(s) + 1)));
    const N = 8;
    for (let i = 0; i < N; i++) {
      const p0 = add(guard, mul(dir, (BL - 4) * i / N)), p1 = add(guard, mul(dir, (BL - 4) * (i + 1) / N)), w = 2.6 - 1.4 * (i / N);
      part((p0.z + p1.z) / 2 + 0.32, c => capsule(c, P(p0), P(p1), w, (u, s) => tone(STEEL, shadeIdx(s) + (s < -0.2 ? 1 : 0))));
    }
    globalThis.__tip = P(tip);
  }

