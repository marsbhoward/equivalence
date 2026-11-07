# The Equivalence store card (C2, chosen 2026-11-06): a tower of the places the run passes through,
# rearranging itself under the Stone's red light, rifts torn about its summit, the sigil door at its
# foot. Writes c/cw/ci .svg (tall 1200x1600, Epic wide 2560x1440, itch cover at 2x), each with an
# _ov.svg of the details pix2.py paints by hand and a .json of the door. Run: python3 gen.py
import random, math, sys
F = lambda v: f"{v:.1f}"

def scene(W, H, cx, ground, top, baseW, seed=7, tiers=8, rifts=None, name="a", sc=None, ref_w=None, rel=False):
    # sc: the drawing's scale, when the tower should keep another card's size in px-per-unit.
    # ref_w: another card's width in units - the spires then draw from their own stream and the
    #        main stream is spent exactly as that card spent it, so its tower comes out the same.
    # rel: rifts given as (dx, dy) units from the summit rather than fractions of the frame.
    r = random.Random(seed)
    rb = random.Random(seed + 1000)
    s = sc if sc else H / 1080.0
    def wob(ax, ay, bx, by, c, n=28, j=1.6, chip=0.12):
        pts = []
        for k in range(n + 1):
            t = k / n
            px = ax + (bx - ax) * t
            py = ay + (by - ay) * t + 2 * c * t * (1 - t) * 2 * 0.5 * 2
            py += r.uniform(-j, j) * s
            if 0 < k < n and r.random() < chip:
                py += r.uniform(3, 9) * s
            pts.append((px, py))
        return pts
    def sketch(pts, closed=True, op=1.0):
        if closed: pts = pts + [pts[0]]
        segs = []
        for (ax, ay), (bx_, by_) in zip(pts, pts[1:]):
            L = math.hypot(bx_ - ax, by_ - ay)
            if L < 1e-6: continue
            ux, uy = (bx_ - ax) / L, (by_ - ay) / L
            d = r.uniform(0, 4) * s
            while d < L:
                dl = min(r.uniform(6, 13) * s, L - d)
                j = lambda: r.uniform(-1.1, 1.1) * s
                segs.append((ax + ux * d + j(), ay + uy * d + j(), ax + ux * (d + dl) + j(), ay + uy * (d + dl) + j()))
                d += dl + r.uniform(4, 8) * s
        for (a, b, c, e) in segs:
            o.append(f'<path class="ov" d="M{F(a)} {F(b)} L{F(c)} {F(e)}" stroke="#c8f6ff" stroke-width="{F(r.uniform(1.8,2.8)*s)}" stroke-linecap="round" opacity="{op*r.uniform(0.55,0.95):.2f}"/>')
    def rough(bx, by, bw, bh, cut):
        c = lambda: r.uniform(0.2, 1) * cut * min(bw, bh)
        pts = [(bx + c(), by), (bx + bw * r.uniform(0.4, 0.6), by + r.uniform(-1, 2) * s), (bx + bw - c(), by),
               (bx + bw, by + c()), (bx + bw + r.uniform(-2, 1) * s, by + bh * 0.5), (bx + bw, by + bh - c()),
               (bx + bw - c(), by + bh), (bx + bw * 0.5, by + bh + r.uniform(-1, 2) * s), (bx + c(), by + bh),
               (bx, by + bh - c()), (bx + r.uniform(-1, 2) * s, by + bh * 0.5), (bx, by + c())]
        return 'M' + ' L'.join(f'{F(a)} {F(b)}' for a, b in pts) + ' Z'
    o = []
    o.append(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">')
    o.append('<defs>')
    o.append('<linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#050307"/><stop offset="0.55" stop-color="#10070c"/><stop offset="1" stop-color="#1c0b0e"/></linearGradient>')
    o.append('<radialGradient id="glow"><stop offset="0" stop-color="#fff1e6" stop-opacity="1"/><stop offset="0.06" stop-color="#ff6a4a" stop-opacity="0.95"/><stop offset="0.22" stop-color="#e01e14" stop-opacity="0.55"/><stop offset="0.5" stop-color="#7a0a10" stop-opacity="0.22"/><stop offset="1" stop-color="#3a0408" stop-opacity="0"/></radialGradient>')
    o.append('<linearGradient id="beam" x1="0" y1="1" x2="0" y2="0"><stop offset="0" stop-color="#ff3a22" stop-opacity="0.85"/><stop offset="1" stop-color="#ff3a22" stop-opacity="0"/></linearGradient>')
    o.append('<linearGradient id="stone" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#4a2427"/><stop offset="0.35" stop-color="#2a171b"/><stop offset="1" stop-color="#110a0e"/></linearGradient>')
    o.append('<linearGradient id="mist" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1a0b10" stop-opacity="0"/><stop offset="1" stop-color="#0c0609" stop-opacity="1"/></linearGradient>')
    o.append('<linearGradient id="gem" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffffff"/><stop offset="0.4" stop-color="#ff8a6a"/><stop offset="1" stop-color="#c8100c"/></linearGradient>')
    for b in (3, 8, 18, 40, 70):
        o.append(f'<filter id="b{b}" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="{F(b*s)}"/></filter>')
    o.append('<linearGradient id="cylR" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#000" stop-opacity="0.35"/><stop offset="0.38" stop-color="#000" stop-opacity="0"/><stop offset="0.7" stop-color="#000" stop-opacity="0.3"/><stop offset="1" stop-color="#000" stop-opacity="0.75"/></linearGradient>')
    o.append('<linearGradient id="cylL" x1="1" y1="0" x2="0" y2="0"><stop offset="0" stop-color="#000" stop-opacity="0.35"/><stop offset="0.38" stop-color="#000" stop-opacity="0"/><stop offset="0.7" stop-color="#000" stop-opacity="0.3"/><stop offset="1" stop-color="#000" stop-opacity="0.75"/></linearGradient>')
    for k, (a, b, c) in enumerate([("#5a5c67", "#3a3c45", "#15161a"), ("#46503c", "#2c3527", "#0f140d"),
                                   ("#a49d8c", "#6b665a", "#24221e"), ("#c4b8b2", "#857a77", "#2e2929"),
                                   ("#94784f", "#5e4a32", "#211a12")]):
        o.append(f'<linearGradient id="m{k}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{a}"/><stop offset="0.35" stop-color="{b}"/><stop offset="1" stop-color="{c}"/></linearGradient>')
    o.append('<linearGradient id="doorL" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#3a3c46"/><stop offset="1" stop-color="#5a5d6b"/></linearGradient>')
    o.append('<linearGradient id="doorR" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#5a5d6b"/><stop offset="1" stop-color="#3a3c46"/></linearGradient>')
    o.append('<filter id="grain" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="2" seed="3"/><feColorMatrix values="0 0 0 0 0.5  0 0 0 0 0.45  0 0 0 0 0.42  0 0 0 0.9 -0.35"/></filter>')
    o.append('<radialGradient id="vig" cx="0.5" cy="0.45" r="0.75"><stop offset="0.55" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="0.6"/></radialGradient>')
    o.append('</defs>')
    o.append(f'<rect width="{W}" height="{H}" fill="url(#sky)"/>')
    # stars
    for _ in range(70):
        x, y = r.uniform(0, W), r.uniform(0, ground * 0.8)
        o.append(f'<circle cx="{F(x)}" cy="{F(y)}" r="{F(r.uniform(0.6,1.6)*s)}" fill="#e8dccf" opacity="{r.uniform(0.08,0.45):.2f}"/>')
    # the glow over the sky
    o.append(f'<circle cx="{F(cx)}" cy="{F(top)}" r="{F(H*0.95)}" fill="url(#glow)" opacity="0.75"/>')
    # far clouds lit from beneath
    for _ in range(14):
        y = r.uniform(top + 60*s, ground - 120*s); x = cx + r.uniform(-W*0.6, W*0.6)
        d = max(0.0, 1 - abs(y - top) / (ground - top))
        o.append(f'<ellipse cx="{F(x)}" cy="{F(y)}" rx="{F(r.uniform(180,420)*s)}" ry="{F(r.uniform(14,34)*s)}" fill="#3a1418" opacity="{0.12+0.35*d:.2f}" filter="url(#b40)"/>')
    # distant ruined spires on the horizon
    sr = rb if ref_w else r
    x = 0
    while x < W:
        w = sr.uniform(18, 60) * s; h = sr.uniform(20, 120) * s
        if abs(x - cx) > baseW * 0.7:
            o.append(f'<path d="M{F(x)} {F(ground)} L{F(x+w*0.2)} {F(ground-h)} L{F(x+w*0.5)} {F(ground-h*1.1)} L{F(x+w*0.75)} {F(ground-h*0.85)} L{F(x+w)} {F(ground)} Z" fill="#160a0e"/>')
        x += w + sr.uniform(30, 180) * s
    if ref_w:
        x = 0
        while x < ref_w * s:
            w = r.uniform(18, 60) * s; r.uniform(20, 120)
            x += w + r.uniform(30, 180) * s
    o.append(f'<rect x="0" y="{F(ground)}" width="{W}" height="{F(H-ground)}" fill="#0b0609"/>')

    # the tower
    T = ground - top
    ws = [0.88 ** i for i in range(tiers)]; ws[0] *= 1.9; tot = sum(ws)
    gi = set(r.sample(range(1, tiers), 3))
    gaps = [0.0] + [((baseW * (1 - 0.8 * k / tiers)) * 0.055 + r.uniform(10, 22) * s if k in gi else 0.0) for k in range(1, tiers)]
    hs = [(T * 0.93 - sum(gaps)) * w / tot for w in ws]
    bag = [1, 2, 3, 4]; r.shuffle(bag)
    mats = [0] + bag
    while len(mats) < tiers:
        mats.append(r.choice([m for m in range(5) if m != mats[-1]]))
    mats = mats[:tiers]
    sh = [0.0] + [r.choice([-1, 1]) * r.uniform(0.02, 0.085) for _ in range(tiers - 1)]
    sh[-1] *= 0.3
    tilt = [0.0] + [(r.uniform(-2.2, 2.2) if g > 0 else r.uniform(-0.5, 0.5)) for g in gaps[1:]]
    notched = set(r.sample(range(1, tiers - 1), 2))
    pulled = []
    frames = []
    door_h = hs[0] * 0.86; door_w = door_h * 0.5
    door_clear = door_w * 0.95
    y = ground
    lit_side = 1 if (rifts and sum(1 for rf in rifts if rf[0] > cx) >= len(rifts) / 2) else -1
    for i in range(tiers):
        h = hs[i]
        y -= gaps[i]
        g0 = len(o)
        f0 = i / tiers; f1 = (i + 1) / tiers
        w0 = baseW * (1 - 0.8 * f0); w1 = baseW * (1 - 0.8 * f1) * 1.03
        wt = w0 * 0.96
        x0, x1 = cx - w0/2, cx + w0/2
        t0, t1 = cx - wt/2, cx + wt/2
        yt = y - h
        heat = (i + 1) / tiers
        cb = w0 * 0.055; ct = wt * 0.055
        bow = lambda px, yy, half, c: yy + c * (1 - min(1, ((px - cx) / half) ** 2))
        last = i == tiers - 1
        if last:
            # unfinished: a broken top edge
            pts = [(x0, y)]
            n = 9
            for k in range(n + 1):
                px = t0 + (t1 - t0) * k / n
                py = yt + r.uniform(-0.1, 0.55) * h * (0.4 + 0.6 * abs(k - n/2) / (n/2))
                pts.append((px, py))
            pts.append((x1, y))
            o.append('<path d="M' + ' L'.join(f'{F(a)} {F(b)}' for a, b in pts) + ' Z" fill="url(#m{mats[i]})"/>')
        else:
            top_e = wob(t0, yt, t1, yt, ct)
            bot_e = wob(x1, y, x0, y, cb, j=0.8, chip=0.0)
            o.append('<path d="M' + ' L'.join(f'{F(a)} {F(b)}' for a, b in [(x0, y)] + top_e + bot_e) + f' Z" fill="url(#m{mats[i]})"/>')
        # shade the far side
        sx = x1 if lit_side < 0 else x0
        st = t1 if lit_side < 0 else t0
        mx = cx + (w0 * 0.22) * (1 if lit_side < 0 else -1)
        mt = cx + (wt * 0.22) * (1 if lit_side < 0 else -1)
        o.append(f'<path d="M{F(x0)} {F(y)} L{F(t0)} {F(yt)} Q{F(cx)} {F(yt+2*ct)} {F(t1)} {F(yt)} L{F(x1)} {F(y)} Q{F(cx)} {F(y+2*cb)} {F(x0)} {F(y)} Z" fill="url(#cyl{"L" if lit_side > 0 else "R"})"/>')
        # masonry: courses following the curve, joints staggered between them
        ch = 13 * s * (1 - 0.35 * f0)
        cl, jl = [], []
        yc = y - ch
        row = 0
        while yc > yt + ch * 0.5:
            fr = (y - yc) / h
            lx = x0 + (t0 - x0) * fr; rx = x1 + (t1 - x1) * fr
            cc = cb + (ct - cb) * fr
            cl.append(f'M{F(lx)} {F(yc)} Q{F(cx)} {F(yc+2*cc)} {F(rx)} {F(yc)}')
            bwid = 34 * s * (1 - 0.35 * f0)
            jx = lx + (row % 2) * bwid * 0.5 + r.uniform(0, bwid * 0.3)
            while jx < rx - 4 * s:
                jy = bow(jx, yc, (rx - lx) / 2 + 1e-3, cc)
                jl.append(f'M{F(jx)} {F(jy)} v{F(ch)}')
                jx += bwid * r.uniform(0.8, 1.2)
            yc -= ch; row += 1
        o.append(f'<path d="{" ".join(cl)}" fill="none" stroke="#0d0e12" stroke-width="{F(1.3*s)}" opacity="0.55"/>')
        o.append(f'<path d="{" ".join(jl)}" fill="none" stroke="#0d0e12" stroke-width="{F(1.1*s)}" opacity="0.4"/>')
        # pilasters
        np_ = max(3, int(w0 / (70 * s)))
        for k in range(1, np_):
            a = x0 + (x1 - x0) * k / np_; b = t0 + (t1 - t0) * k / np_
            o.append(f'<path d="M{F(a)} {F(y)} L{F(b)} {F(yt + h*0.12)}" stroke="#0a0609" stroke-width="{F(3*s)}" opacity="0.55"/>')
        # arcades
        rows = max(1, int(h / (34 * s)))
        aw = 9 * s * (1 - 0.3 * f0); gap = 22 * s * (1 - 0.3 * f0)
        for rw in range(rows):
            ry = y - h * (rw + 0.75) / (rows + 0.4)
            if ry < yt + h * 0.22: continue
            fr = (y - ry) / h
            lx = x0 + (t0 - x0) * fr + gap; rx = x1 + (t1 - x1) * fr - gap
            ah = min(22 * s, h / rows * 0.5)
            path = []
            ax = lx
            while ax + aw < rx:
                if i == 0 and abs(ax + aw / 2 - cx) < door_clear:
                    ax += gap; continue
                if r.random() < 0.09:
                    ax += gap * r.uniform(0.9, 1.15); continue
                half = (rx - lx) / 2 + gap; cc = cb + (ct - cb) * fr
                ay_ = bow(ax + aw/2, ry, half, cc)
                ah_ = ah * r.uniform(0.85, 1.1)
                path.append(f'M{F(ax)} {F(ay_)} v{F(-ah_)} a{F(aw/2)} {F(aw/2)} 0 0 1 {F(aw)} 0 v{F(ah_)} Z')
                if r.random() < 0.05 + 0.08 * heat:
                    o.append(f'<rect x="{F(ax+aw*0.2)}" y="{F(ay_-ah*0.8)}" width="{F(aw*0.6)}" height="{F(ah*0.8)}" fill="#ff7a3a" opacity="{r.uniform(0.5,0.9):.2f}"/>')
                ax += gap * r.uniform(0.94, 1.06)
            o.append(f'<path d="{" ".join(path)}" fill="#07040a" opacity="0.85"/>')
        # the ramp across the face (the spiral)
        if not last and i > 0:
            rx0, ry0 = x0 + (x1 - x0) * 0.04, y - h * 0.05
            rx1, ry1 = t1 - (t1 - t0) * 0.04, yt + h * 0.18
            if i % 2: rx0, rx1 = x1 - (x1 - x0) * 0.04, t0 + (t1 - t0) * 0.04
            qx, qy = cx, (ry0 + ry1) / 2 + (cb + ct)
            o.append(f'<path d="M{F(rx0)} {F(ry0)} Q{F(qx)} {F(qy)} {F(rx1)} {F(ry1)}" fill="none" stroke="#0a0508" stroke-width="{F(9*s)}" opacity="0.8"/>')
            o.append(f'<path d="M{F(rx0)} {F(ry0-5*s)} Q{F(qx)} {F(qy-5*s)} {F(rx1)} {F(ry1-5*s)}" fill="none" stroke="#a83a2c" stroke-width="{F(2*s)}" opacity="{0.25+0.5*heat:.2f}"/>')
            # ledge lit by the Stone
            o.append(f'<path d="M{F(t0-4*s)} {F(yt)} Q{F(cx)} {F(yt+2*ct)} {F(t1+4*s)} {F(yt)}" fill="none" stroke="#ff4a2e" stroke-width="{F(6*s)}" opacity="{0.15+0.6*heat:.2f}"/>')
            o.append(f'<path d="M{F(t0-4*s)} {F(yt+7*s)} Q{F(cx)} {F(yt+7*s+2*ct)} {F(t1+4*s)} {F(yt+7*s)}" fill="none" stroke="#000" stroke-width="{F(5*s)}" opacity="0.5"/>')
        m = mats[i]
        if m == 1:
            # drowned forest: moss hanging off the ledge, roots down the face
            drips = []
            for k in range(int(wt / (14 * s))):
                px = t0 + r.uniform(0, wt); py = bow(px, yt, wt / 2, ct)
                dl = r.uniform(5, 30) * s; dw = r.uniform(4, 11) * s
                drips.append(f'M{F(px-dw/2)} {F(py-2*s)} L{F(px+dw/2)} {F(py-2*s)} L{F(px+dw*0.2)} {F(py+dl)} L{F(px-dw*0.2)} {F(py+dl*0.8)} Z')
            o.append(f'<path class="ov" d="{" ".join(drips)}" fill="#3f5e2c" opacity="0.95"/>')
            o.append(f'<path class="ov" d="M{F(t0)} {F(yt)} Q{F(cx)} {F(yt+2*ct)} {F(t1)} {F(yt)}" fill="none" stroke="#5b7f3a" stroke-width="{F(6*s)}"/>')
        elif m == 2:
            # sky ruin: ivy wound down the pale stone
            for k in range(r.randint(3, 5)):
                px = t0 + r.uniform(0.05, 0.95) * wt; py = bow(px, yt, wt / 2, ct)
                ln = h * r.uniform(0.35, 0.95); sway = r.uniform(-1, 1) * 18 * s
                o.append(f'<path class="ov" d="M{F(px)} {F(py)} q{F(sway)} {F(ln*0.5)} 0 {F(ln)}" fill="none" stroke="#3d5e2c" stroke-width="{F(2.5*s)}"/>')
                for q in range(int(ln / (9 * s))):
                    tt = q / max(1, int(ln / (9 * s)))
                    lx_ = px + sway * 2 * tt * (1 - tt) * 2 + r.uniform(-5, 5) * s
                    o.append(f'<circle class="ov" cx="{F(lx_)}" cy="{F(py+ln*tt)}" r="{F(r.uniform(2,4.2)*s)}" fill="{r.choice(["#3d5e2c", "#56813a"])}"/>')
        elif m == 3:
            # salt flats: pink and cream strata across the crust
            for k in range(4):
                fy = r.uniform(0.25, 0.92); yy = y - h * fy
                fr_ = (y - yy) / h
                lx_ = x0 + (t0 - x0) * fr_; rx_ = x1 + (t1 - x1) * fr_
                cc = cb + (ct - cb) * fr_
                o.append(f'<path d="M{F(lx_)} {F(yy)} Q{F(cx)} {F(yy+2*cc)} {F(rx_)} {F(yy)}" fill="none" stroke="{r.choice(["#d4a29c", "#e9dcc8", "#c58e8c"])}" stroke-width="{F(r.uniform(3,8)*s)}" opacity="0.45"/>')
            o.append(f'<path d="M{F(t0)} {F(yt)} Q{F(cx)} {F(yt+2*ct)} {F(t1)} {F(yt)}" fill="none" stroke="#f4f0ec" stroke-width="{F(4*s)}" opacity="0.8"/>')
        elif m == 4:
            # sandstone: wind-worn streaks and a sand drift along the ledge
            for k in range(5):
                yy = y - h * r.uniform(0.2, 0.9); px = x0 + r.uniform(0.05, 0.7) * w0
                o.append(f'<path d="M{F(px)} {F(yy)} h{F(r.uniform(40,140)*s)}" stroke="#c9a46e" stroke-width="{F(r.uniform(1.5,3.5)*s)}" opacity="0.3" stroke-linecap="round"/>')
            o.append(f'<path d="M{F(t0)} {F(yt)} Q{F(cx)} {F(yt+2*ct)} {F(t1)} {F(yt)}" fill="none" stroke="#d2ad72" stroke-width="{F(5*s)}" opacity="0.75"/>')
        # cold rim from the rifts
        ex0, et0 = (x1, t1) if lit_side > 0 else (x0, t0)
        o.append(f'<path d="M{F(ex0)} {F(y)} L{F(et0)} {F(yt)}" stroke="#7fe8ff" stroke-width="{F(2.5*s)}" opacity="{0.08+0.35*heat:.2f}"/>')
        dx = sh[i] * w0
        if i in notched:
            bw = w0 * r.uniform(0.11, 0.16); bh = h * 0.62
            bx = cx + r.choice([-1, 1]) * r.uniform(0.1, 0.3) * w0 - bw / 2
            by = y - h * 0.08 - bh
            hole = rough(bx, by, bw, bh, 0.12)
            o.append(f'<path d="{hole}" fill="#0a050b"/>')
            o.append(f'<path d="M{F(bx)} {F(by+bh)} h{F(bw)}" stroke="#ff4a2e" stroke-width="{F(2*s)}" opacity="0.35"/>')
            m_ = 3 * s
            sketch([(bx - m_, by - m_), (bx + bw + m_, by - m_), (bx + bw + m_, by + bh + m_), (bx - m_, by + bh + m_)])
            pulled.append((bx, by, bw, bh, mats[i], dx, i))
        body = o[g0:]
        del o[g0:]
        if gaps[i]:
            gy0 = y + gaps[i]
            # chains and ropes still holding the floor to the one below, slack and taut
            for k in range(r.randint(2, 4)):
                px = cx + r.uniform(-0.4, 0.4) * w0
                sag = r.uniform(-6, 6) * s
                o.append(f'<path d="M{F(px+dx)} {F(y+cb*0.6)} q{F(sag)} {F(gaps[i]*0.5)} {F(-dx*0.6)} {F(gaps[i]+cb*0.3)}" fill="none" stroke="#120b0e" stroke-width="{F(r.uniform(1.6,3)*s)}"/>')
            # grit and stones falling out of the seam
            for _ in range(r.randint(8, 16)):
                px = cx + dx * 0.5 + r.uniform(-0.48, 0.48) * w0; py = y + r.uniform(0, gaps[i] + cb)
                z = r.uniform(1.5, 5) * s
                o.append(f'<path d="M{F(px)} {F(py)} l{F(z)} {F(z*0.3)} l{F(-z*0.4)} {F(z*0.8)} Z" fill="#1a1013"/>')
            # the faintest cold light in the seam, uneven
            o.append(f'<path d="M{F(cx+dx*0.5-w0*0.42)} {F(y+gaps[i]*0.6)} Q{F(cx+dx*0.5)} {F(y+gaps[i]*0.6+2*cb)} {F(cx+dx*0.5+w0*0.3)} {F(y+gaps[i]*0.6)}" fill="none" stroke="#7fe8ff" stroke-width="{F(10*s)}" opacity="0.10" filter="url(#b8)"/>')
        frames.append((x0 + dx, x1 + dx, t0 + dx, t1 + dx, y, yt, h, cb, ct, mats[i]))
        o.append(f'<g transform="translate({F(dx)} 0) rotate({tilt[i]:.2f} {F(cx)} {F(y-h/2)})">')
        o.extend(body)
        o.append('</g>')
        y = yt
    # the sigil door: two stone leaves, the four marks cut across the seam, parted on white light
    cb0 = baseW * 0.055
    db = ground + cb0 * 0.92          # the base's curve at the centre
    dh, dw = door_h, door_w
    dt = db - dh
    gap = dw * 0.13                    # how far the leaves have parted
    lw = (dw - gap) / 2
    fr = 7 * s
    door = dict(cx=cx, dt=dt, db=db, dw=dw, dh=dh, gap=gap, fr=fr, s=s)
    # light spilling across the ground in front, fanning rays
    o.append(f'<ellipse cx="{F(cx)}" cy="{F(db+8*s)}" rx="{F(dw*2.2)}" ry="{F(34*s)}" fill="#ffffff" opacity="0.28" filter="url(#b18)"/>')
    rays = []
    for k in range(-4, 5):
        spread = k * dw * 0.55
        rays.append(f'M{F(cx-gap*0.45)} {F(db)} L{F(cx+gap*0.45)} {F(db)} L{F(cx+spread+dw*0.18)} {F(H)} L{F(cx+spread-dw*0.18)} {F(H)} Z')
    o.append(f'<path d="{" ".join(rays)}" fill="#f4f8ff" opacity="0.10" filter="url(#b8)"/>')
    # frame and lintel
    o.append(f'<rect x="{F(cx-dw/2-fr)}" y="{F(dt-fr)}" width="{F(dw+2*fr)}" height="{F(dh+fr)}" fill="#202128"/>')
    o.append(f'<rect x="{F(cx-dw/2-fr*2.2)}" y="{F(dt-fr*2.6)}" width="{F(dw+fr*4.4)}" height="{F(fr*1.8)}" fill="#4a4c56"/>')
    o.append(f'<rect x="{F(cx-dw/2-fr*2.2)}" y="{F(dt-fr*2.6)}" width="{F(dw+fr*4.4)}" height="{F(2*s)}" fill="#ff4a2e" opacity="0.35"/>')
    # the blinding white opening, glow first
    o.append(f'<rect x="{F(cx-gap)}" y="{F(dt-gap*0.5)}" width="{F(gap*2)}" height="{F(dh+gap*0.5)}" fill="#ffffff" opacity="0.8" filter="url(#b18)"/>')
    o.append(f'<rect x="{F(cx-gap/2)}" y="{F(dt)}" width="{F(gap)}" height="{F(dh)}" fill="#ffffff"/>')
    o.append(f'<clipPath id="leafL"><rect x="{F(cx-dw/2)}" y="{F(dt)}" width="{F(lw)}" height="{F(dh)}"/></clipPath>')
    o.append(f'<clipPath id="leafR"><rect x="{F(cx+gap/2)}" y="{F(dt)}" width="{F(lw)}" height="{F(dh)}"/></clipPath>')
    # the marks as if the door were shut (centred on the seam), each leaf showing its half
    ms = min(dh / 4 * 0.62, dw * 0.62)
    marks = []
    for k, el in enumerate(["fire", "water", "air", "earth"]):
        my = dt + dh * (k + 0.5) / 4
        hw, hh = ms * 0.5, ms * 0.45
        if el in ("fire", "air"):
            tri = f'M{F(cx)} {F(my-hh)} L{F(cx+hw)} {F(my+hh)} L{F(cx-hw)} {F(my+hh)} Z'
        else:
            tri = f'M{F(cx)} {F(my+hh)} L{F(cx+hw)} {F(my-hh)} L{F(cx-hw)} {F(my-hh)} Z'
        bar = ''
        if el == "air": bar = f' M{F(cx-hw*0.62)} {F(my+hh*0.2)} L{F(cx+hw*0.62)} {F(my+hh*0.2)}'
        if el == "earth": bar = f' M{F(cx-hw*0.62)} {F(my-hh*0.2)} L{F(cx+hw*0.62)} {F(my-hh*0.2)}'
        marks.append((el, tri + bar))
    sw = ms * 0.11
    for side, clip, shift in (("L", "leafL", -(gap / 2)), ("R", "leafR", gap / 2)):
        lx = cx - dw / 2 if side == "L" else cx + gap / 2
        o.append(f'<rect x="{F(lx)}" y="{F(dt)}" width="{F(lw)}" height="{F(dh)}" fill="url(#door{side})"/>')
        o.append(f'<g clip-path="url(#{clip})"><g transform="translate({F(shift)} 0)">')
        for el, d in marks:
            if el == "fire":
                o.append(f'<path d="{d}" fill="none" stroke="#ff8c38" stroke-width="{F(sw*2.4)}" opacity="0.55" filter="url(#b8)"/>')
                o.append(f'<path d="{d}" fill="none" stroke="#ffb070" stroke-width="{F(sw)}" stroke-linejoin="miter"/>')
            else:
                o.append(f'<path d="{d}" fill="none" stroke="#757887" stroke-width="{F(sw)}" transform="translate({F(1.6*s)} {F(1.6*s)})"/>')
                o.append(f'<path d="{d}" fill="none" stroke="#1d1f26" stroke-width="{F(sw)}"/>')
        o.append('</g></g>')
        # the seam edge catches the white light
        ex = cx - gap / 2 if side == "L" else cx + gap / 2
        o.append(f'<rect x="{F(ex - (2.5*s if side == "L" else 0))}" y="{F(dt)}" width="{F(2.5*s)}" height="{F(dh)}" fill="#e8f0ff" opacity="0.7"/>')
    # steps up to it
    for k in range(3):
        sw2 = dw + fr * 4 + k * 26 * s
        o.append(f'<rect x="{F(cx-sw2/2)}" y="{F(db+k*7*s)}" width="{F(sw2)}" height="{F(7*s)}" fill="{["#4a4c56","#3d3f48","#30323a"][k]}"/>')
        o.append(f'<rect x="{F(cx-sw2/2)}" y="{F(db+k*7*s)}" width="{F(sw2)}" height="{F(1.5*s)}" fill="#ffffff" opacity="{0.35-0.1*k:.2f}"/>')
    # the pulled blocks, lifted out and turning
    def carry(px, py, wd, fromx, fromy):
        # the cold light it rides on, and the dust it leaves along its way in
        o.append(f'<ellipse cx="{F(px)}" cy="{F(py)}" rx="{F(wd*0.62)}" ry="{F(wd*0.16)}" fill="#3ee8ff" opacity="0.45" filter="url(#b18)"/>')
        o.append(f'<ellipse cx="{F(px)}" cy="{F(py)}" rx="{F(wd*0.4)}" ry="{F(wd*0.05)}" fill="#bff8ff" opacity="0.55" filter="url(#b3)"/>')
        for k in range(r.randint(9, 16)):
            t = r.uniform(0.1, 1.0)
            qx = px + (fromx - px) * t + r.uniform(-1, 1) * wd * 0.25 * t
            qy = py + (fromy - py) * t + r.uniform(-1, 1) * wd * 0.12 * t
            z = r.uniform(1.2, 3.6) * s * (1.1 - t * 0.6)
            o.append(f'<circle cx="{F(qx)}" cy="{F(qy)}" r="{F(z)}" fill="{r.choice(["#2a1a1e", "#3a2a2c", "#9fe9f4"])}" opacity="{0.85 - 0.6*t:.2f}"/>')
    def block(bx, by, bw, bh, m, rot):
        aw = bw / 5
        o.append(f'<g transform="rotate({rot:.1f} {F(bx+bw/2)} {F(by+bh/2)})">')
        o.append(f'<path d="{rough(bx, by, bw, bh, 0.16)}" fill="url(#m{m})"/>')
        o.append(f'<path d="M{F(bx+bw*0.08)} {F(by+1.5*s)} h{F(bw*0.84)}" stroke="#ff4a2e" stroke-width="{F(2.5*s)}" opacity="0.55"/>')
        ah = bh * 0.45
        for k in range(2):
            ax = bx + aw * (1 + 2 * k) + r.uniform(-2, 2) * s
            o.append(f'<path d="M{F(ax)} {F(by+bh*0.85)} v{F(-ah)} a{F(aw/2)} {F(aw/2)} 0 0 1 {F(aw)} 0 v{F(ah)} Z" fill="#07040a" opacity="0.85"/>')
        o.append(f'<path d="M{F(bx)} {F(by+bh)} h{F(bw)}" stroke="#000" stroke-width="{F(3*s)}" opacity="0.45"/>')
        o.append('</g>')
        for _ in range(r.randint(3, 7)):
            px = bx + r.uniform(0.1, 0.9) * bw; py = by + bh + r.uniform(4, 40) * s
            z = r.uniform(1.5, 4) * s
            o.append(f'<path d="M{F(px)} {F(py)} l{F(z)} {F(z*0.4)} l{F(-z*0.5)} {F(z*0.7)} Z" fill="#1a1013"/>')
    # blocks sliding back into their holes, nearly home
    for (bx, by, bw, bh, m, dx, i) in pulled:
        side = 1 if bx + bw / 2 > cx else -1
        ox = side * r.uniform(0.3, 0.45) * bw + dx; oy = -r.uniform(0.25, 0.45) * bh
        nx, ny = bx + ox, by + oy
        carry(nx + bw / 2, ny + bh + 4 * s, bw, nx + bw / 2 + side * bw * 1.6, ny + bh - bh * 1.2)
        block(nx, ny, bw, bh, m, r.uniform(-5, 5))
    # pieces of floor flying in from either side toward the upper tiers
    def piece(px, py, pw, ph, m, rot, flip):
        c = pw * 0.07
        top_e = wob(px, py, px + pw, py, c, n=14, j=1.2, chip=0.15)
        bot_e = wob(px + pw, py + ph, px, py + ph, c, n=14, j=0.8, chip=0.1)
        o.append(f'<g transform="rotate({rot:.1f} {F(px+pw/2)} {F(py+ph/2)})">')
        o.append('<path d="M' + ' L'.join(f'{F(a)} {F(b)}' for a, b in top_e + bot_e) + f' Z" fill="url(#m{m})"/>')
        o.append('<path d="M' + ' L'.join(f'{F(a)} {F(b)}' for a, b in top_e) + f'" fill="none" stroke="#ff4a2e" stroke-width="{F(3*s)}" opacity="0.5"/>')
        # its arcade, following the curve
        aw = 8 * s; gap = 20 * s; ax = px + gap * 0.6
        arch = []
        while ax + aw < px + pw - gap * 0.4:
            t = (ax - px) / pw
            ay = py + ph * 0.8 + 4 * c * t * (1 - t)
            arch.append(f'M{F(ax)} {F(ay)} v{F(-ph*0.45)} a{F(aw/2)} {F(aw/2)} 0 0 1 {F(aw)} 0 v{F(ph*0.45)} Z')
            ax += gap * r.uniform(0.95, 1.05)
        o.append(f'<path d="{" ".join(arch)}" fill="#07040a" opacity="0.85"/>')
        # the broken ends, where it parted from the rest of its floor
        ex = px if flip else px + pw
        o.append(f'<path d="M{F(ex)} {F(py)} l{F((4 if flip else -4)*s)} {F(ph*0.3)} l{F((-3 if flip else 3)*s)} {F(ph*0.3)} l{F((5 if flip else -5)*s)} {F(ph*0.4)}" fill="none" stroke="#000" stroke-width="{F(3*s)}" opacity="0.5"/>')
        o.append('</g>')
    fx0, fx1, ft0, ft1, fy, fyt, fh, fcb, fct, fm = frames[-1]
    gw = (ft1 - ft0) * 0.92; gl = (ft0 + ft1) / 2 - gw / 2; gh = fh * 0.85
    gb = fyt + fh * 0.18
    base = [(gl + gw * k / 16, gb + 4 * fct * 0.9 * (k / 16) * (1 - k / 16)) for k in range(17)]
    topl = [(gl + gw * 0.03 + gw * 0.94 * k / 16, gb - gh + 4 * fct * 0.8 * (k / 16) * (1 - k / 16)) for k in range(16, -1, -1)]
    g_at = len(o)
    sketch(base + topl, op=1.0)
    for k in (1, 2, 3):
        t = k / 4
        sketch([(gl + gw * t, gb + 4 * fct * 0.9 * t * (1 - t)), (gl + gw * 0.03 + gw * 0.94 * t, gb - gh + 4 * fct * 0.8 * t * (1 - t))], closed=False, op=0.8)
    ghost = [e.replace('stroke="#c8f6ff"', 'stroke="#e6fcff"') for e in o[g_at:]]
    del o[g_at:]
    upper = frames[len(frames) // 2: -1]
    for k, side in enumerate(r.sample([-1, 1], 2)):
        if not upper: break
        fx0, fx1, ft0, ft1, fy, fyt, fh, fcb, fct, fm = upper[min(k * 2, len(upper) - 1)]
        pw = (fx1 - fx0) * r.uniform(0.42, 0.5); ph = fh * 0.95
        edge = fx1 if side > 0 else fx0
        px = edge + side * r.uniform(0.12, 0.3) * pw - (0 if side > 0 else pw)
        py = fyt - r.uniform(0.3, 0.9) * fh
        carry(px + pw / 2, py + ph + 6 * s, pw, px + pw / 2 + side * pw * 1.8, py + ph + fh * 0.7)
        piece(px, py, pw, ph, r.choice([m for m in range(5) if m != fm]), side * r.uniform(4, 9), side < 0)
    # blocks rising toward the Stone, still to be laid
    for k in range(4):
        bw = baseW * r.uniform(0.1, 0.15); bh = bw * r.uniform(0.45, 0.6)
        ang = math.radians(r.uniform(200, 340)); d = r.uniform(150, 290) * s
        bx = cx + math.cos(ang) * d * 1.3 - bw / 2; by = top + math.sin(ang) * d * 0.5 - bh / 2 - 20 * s
        carry(bx + bw / 2, by + bh + 4 * s, bw, bx + bw / 2 + (bx + bw / 2 - cx) * 0.5, by + bh + bh * 2.2)
        block(bx, by, bw, bh, r.randrange(5), r.uniform(-14, 14))
    # scaffolding on the unfinished top
    ytop = y
    wt = baseW * 0.2
    for k in range(7):
        px = cx - wt*0.55 + wt*1.1*k/6
        hh = r.uniform(40, 110) * s
        o.append(f'<path d="M{F(px)} {F(ytop+hs[-1]*0.4)} L{F(px+r.uniform(-6,6)*s)} {F(ytop-hh)}" stroke="#1a0c0f" stroke-width="{F(2.5*s)}"/>')
    for k in range(3):
        yy = ytop - k * 30 * s
        o.append(f'<path d="M{F(cx-wt*0.6)} {F(yy)} L{F(cx+wt*0.6)} {F(yy-r.uniform(-8,8)*s)}" stroke="#1a0c0f" stroke-width="{F(2*s)}"/>')

    # the Stone: beam, glow, crystal
    gy = top
    o.append(f'<rect x="{F(cx-26*s)}" y="0" width="{F(52*s)}" height="{F(gy)}" fill="url(#beam)" filter="url(#b40)" opacity="0.45"/>')

    o.append(f'<circle cx="{F(cx)}" cy="{F(gy)}" r="{F(260*s)}" fill="#ff2a18" opacity="0.45" filter="url(#b70)"/>')
    o.append(f'<circle cx="{F(cx)}" cy="{F(gy)}" r="{F(90*s)}" fill="#ff3a22" opacity="0.8" filter="url(#b40)"/>')
    o.append(f'<circle cx="{F(cx)}" cy="{F(gy)}" r="{F(34*s)}" fill="#ffd6c8" filter="url(#b18)"/>')
    gh, gw = 46 * s, 20 * s
    o.append(f'<path d="M{F(cx)} {F(gy-gh)} L{F(cx+gw)} {F(gy-gh*0.15)} L{F(cx+gw*0.55)} {F(gy+gh*0.6)} L{F(cx-gw*0.6)} {F(gy+gh*0.55)} L{F(cx-gw)} {F(gy-gh*0.2)} Z" fill="url(#gem)"/>')
    o.append(f'<path d="M{F(cx)} {F(gy-gh)} L{F(cx+gw*0.15)} {F(gy+gh*0.58)} M{F(cx-gw)} {F(gy-gh*0.2)} L{F(cx+gw)} {F(gy-gh*0.15)}" stroke="#ffffff" stroke-width="{F(1.5*s)}" opacity="0.7"/>')

    # rifts: long jagged seams, white-cyan rim, cyan glow, violet halo, the dark beyond
    for (rx, ry, ln, ang, wd) in (rifts or []):
        if rel: rx, ry = cx + rx * s, top + ry * s
        else: rx *= W; ry *= H
        ln *= s; wd *= s
        ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        n = 26
        axis = []
        bend = r.uniform(-1, 1) * ln * 0.08
        for k in range(n + 1):
            t = k / n - 0.5
            off = bend * math.sin(math.pi * (t + 0.5)) + r.uniform(-1, 1) * ln * 0.012
            axis.append((rx + ca * t * ln - sa * off, ry + sa * t * ln + ca * off))
        def poly(scale, jit):
            L, R = [], []
            for k, (ax, ay) in enumerate(axis):
                t = k / n
                hw = wd * scale * math.sin(math.pi * t) ** 1.3 * r.uniform(1 - jit, 1 + jit)
                L.append((ax - sa * hw, ay + ca * hw)); R.append((ax + sa * hw, ay - ca * hw))
            pts = L + R[::-1]
            return 'M' + ' L'.join(f'{F(a)} {F(b)}' for a, b in pts) + ' Z'
        o.append(f'<path d="{poly(6.0,0.2)}" fill="#7a3cff" opacity="0.38" filter="url(#b40)"/>')
        o.append(f'<path d="{poly(1.8,0.35)}" fill="#3ee8ff" opacity="0.7" filter="url(#b8)"/>')
        o.append(f'<path d="{poly(1.0,0.45)}" fill="#e6fdff"/>')
        o.append(f'<path d="{poly(0.7,0.5)}" fill="#1a0b2e"/>')
        for _ in range(int(ln / (70*s))):
            t = r.uniform(-0.45, 0.45); d = r.uniform(-1, 1) * wd * 6
            px, py = rx + ca*t*ln - sa*d, ry + sa*t*ln + ca*d
            z = r.uniform(3, 9) * s; a = r.uniform(0, 6.28)
            o.append(f'<path d="M{F(px+math.cos(a)*z)} {F(py+math.sin(a)*z)} L{F(px+math.cos(a+2.4)*z*0.5)} {F(py+math.sin(a+2.4)*z*0.5)} L{F(px+math.cos(a+3.8)*z*0.6)} {F(py+math.sin(a+3.8)*z*0.6)} Z" fill="#9ff4ff" opacity="{r.uniform(0.5,0.95):.2f}"/>')
    # debris lifting off the top
    for _ in range(22):
        a = r.uniform(0, math.pi * 2); d = r.uniform(60, 330) * s
        px, py = cx + math.cos(a) * d * 1.5, top + math.sin(a) * d * 0.7 - 30*s
        z = r.uniform(4, 15) * s
        o.append(f'<rect x="{F(px-z/2)}" y="{F(py-z/2)}" width="{F(z)}" height="{F(z*0.7)}" fill="#1c0e11" transform="rotate({r.uniform(0,90):.0f} {F(px)} {F(py)})"/>')
        o.append(f'<rect x="{F(px-z/2)}" y="{F(py-z/2)}" width="{F(z)}" height="{F(z*0.18)}" fill="#ff4a2e" opacity="0.7" transform="rotate({r.uniform(-10,10):.0f} {F(px)} {F(py)})"/>')
    # embers rising
    for _ in range(140):
        px = cx + r.gauss(0, 220 * s); py = top + r.uniform(-300, 520) * s
        o.append(f'<circle cx="{F(px)}" cy="{F(py)}" r="{F(r.uniform(0.8,2.6)*s)}" fill="#ff6a3a" opacity="{r.uniform(0.25,0.9):.2f}"/>')
    o.extend(ghost)
    # mist over the plain
    o.append(f'<rect x="0" y="{F(ground-80*s)}" width="{W}" height="{F(H-ground+80*s)}" fill="url(#mist)"/>')
    o.append(f'<rect width="{W}" height="{H}" fill="url(#vig)"/>')
    o.append(f'<rect width="{W}" height="{H}" filter="url(#grain)" opacity="0.5"/>')
    o.append('</svg>')
    open(f'{name}.svg', 'w').write('\n'.join(o))
    # the same picture with only the hand-painted details showing, on magenta, for pix.py
    di = next(i for i, e in enumerate(o) if e == '</defs>')
    import re as _re
    keep = lambda e: _re.fullmatch(r'(</?g[^>]*>)+', e) or 'class="ov"' in e or e.startswith('</svg')
    ov = o[:di + 1] + [f'<rect width="{W}" height="{H}" fill="#ff00ff"/>'] + [e for e in o[di + 1:] if keep(e)]
    open(f'{name}_ov.svg', 'w').write('\n'.join(ov))
    import json
    json.dump(dict(W=W, H=H, door=door), open(f'{name}.json', 'w'))

# C family: the same tower (seed, tiers, scale in units) cut to Epic wide and the itch.io cover
_cs = 1600 / 1080.0
_crifts = [((0.24*1200-600)/_cs, (0.12*1600-330)/_cs, 320, -28, 18), ((0.75*1200-600)/_cs, (0.17*1600-330)/_cs, 360, 26, 20),
           ((0.82*1200-600)/_cs, (0.38*1600-330)/_cs, 170, -72, 9)]
_cw = 1200 / _cs
_ch = (1180 - 330) / _cs
_cb = 760 / _cs
# wide: tower centred under a centred mark (Epic's offer images centre the product logo)
_ws = 1.15
scene(2560, 1440, 1280, 1360, 1360 - _ch*_ws, _cb*_ws, seed=5, tiers=9, name="cw", sc=_ws, ref_w=_cw, rel=True, rifts=_crifts)
_is = 640 / _ch
scene(1260, 1000, 480, 900, 900 - _ch*_is, _cb*_is, seed=5, tiers=9, name="ci", sc=_is, ref_w=_cw, rel=True, rifts=_crifts)
# C: portrait 1200x1600, tower centred, logo low
scene(1200, 1600, 600, 1180, 330, 760, seed=5, tiers=9, name="c",
      rifts=[(0.24, 0.12, 320, -28, 18), (0.75, 0.17, 360, 26, 20), (0.82, 0.38, 170, -72, 9)])
