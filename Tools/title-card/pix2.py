# The pixel pass for the store cards: the scene brought down to a 4px grid, then the fine details
# painted at that grid instead of averaged away - the chalk slots, moss and ivy from the overlay
# render, and the sigil door drawn pixel by pixel from its geometry (gen.py writes <card>.json).
import re, json, subprocess, sys
import numpy as np
from PIL import Image

BLOCK = 4

def render(svg, w, h):
    s = open(svg).read()
    n = max(w, h)
    m = re.search(r'width="(\d+)" height="(\d+)"', s)
    s = s.replace(m[0], f'width="{n}" height="{n}" viewBox="{-(n-w)//2} {-(n-h)//2} {n} {n}"', 1).replace(f'viewBox="0 0 {w} {h}"', '', 1)
    tmp = "sq_" + svg
    open(tmp, "w").write(s)
    subprocess.run(["qlmanage", "-t", "-s", str(n), "-o", ".", tmp], capture_output=True)
    im = Image.open(tmp + ".png").convert("RGB")
    k = im.width / n
    return im.crop((round((n-w)//2*k), round((n-h)//2*k), round(((n-w)//2+w)*k), round(((n-h)//2+h)*k))).resize((w, h), Image.LANCZOS)

def hexc(h): return np.array([int(h[i:i+2], 16) for i in (1, 3, 5)], float)

def paint_door(g, door):
    X = lambda v: v / BLOCK
    cx, dt, db, dw, gap, fr = (door[k] for k in ("cx", "dt", "db", "dw", "gap", "fr"))
    L, R = int(round(X(cx - dw / 2))), int(round(X(cx + dw / 2)))
    T, B = int(round(X(dt))), int(round(X(db)))
    f = max(1, int(round(X(fr))))
    gp = max(2, int(round(X(gap))))
    if (R - L - gp) % 2: R += 1
    c0 = L + (R - L - gp) // 2               # first seam column
    def rect(x0, y0, x1, y1, col):
        g[max(0, y0):y1, max(0, x0):x1] = hexc(col)
    # lintel, frame
    rect(L - 2 * f, T - 2 * f - 1, R + 2 * f, T - f, "#4a4c56")
    rect(L - 2 * f, T - 2 * f - 1, R + 2 * f, T - 2 * f, "#7a4a46")
    rect(L - f, T - f, R + f, B, "#202128")
    # leaves
    lw = c0 - L
    for side, x0 in (("L", L), ("R", c0 + gp)):
        rect(x0, T, x0 + lw, B, "#4d505c")
        rect(x0, T, x0 + lw, T + 1, "#5d6070")
        outer = x0 if side == "L" else x0 + lw - 1
        inner = x0 + lw - 1 if side == "L" else x0
        rect(outer, T, outer + 1, B, "#3a3c46")
        rect(inner, T, inner + 1, B, "#a9b0c6")      # the seam edge catching the white light
    # the white opening
    rect(c0, T, c0 + gp, B, "#ffffff")
    # the four marks, cut across the seam: Fire (lit), Water, Air, Earth
    slot = (B - T) / 4
    th = max(4, min(int(slot * 0.72), lw + 1))
    tw = min(2 * th - 1, 2 * (lw - 1) + 1)
    hw = (tw - 1) // 2
    for k, el in enumerate(("fire", "water", "air", "earth")):
        top = int(round(T + slot * (k + 0.5) - th / 2))
        pts = set()
        for yy in range(th):
            t = yy / (th - 1)
            row = yy if el in ("fire", "air") else th - 1 - yy
            h = int(round(t * hw))
            pts.add((-h, row)); pts.add((h, row))
        base_row = th - 1 if el in ("fire", "air") else 0
        for dx in range(-hw, hw + 1): pts.add((dx, base_row))
        if el in ("air", "earth"):
            br = int(round(th * (0.62 if el == "air" else 0.38)))
            t = (br if el == "air" else th - 1 - br) / (th - 1)
            h = int(round(t * hw))
            for dx in range(-h, h + 1): pts.add((dx, br))
        col = "#ffb070" if el == "fire" else "#1d1f26"
        for dx, row in pts:
            if dx == 0: continue                      # torn: the centre column went with the seam
            x = (c0 - 1) + dx + 1 if dx < 0 else (c0 + gp) + dx - 1
            y = top + row
            if el != "fire" and (dx, row + 1) not in pts:
                g[y + 1, x] = hexc("#6e7180")         # the lit lip under a cut
            g[y, x] = hexc(col)
        if el == "fire":
            for dx, row in pts:
                if dx == 0: continue
                x = (c0 - 1) + dx + 1 if dx < 0 else (c0 + gp) + dx - 1
                for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    if (dx + ox, row + oy) not in pts and L <= x + ox < R:
                        g[top + row + oy, x + ox] = g[top + row + oy, x + ox] * 0.45 + hexc("#ff7a30") * 0.55
    # steps
    for k, col in enumerate(("#4a4c56", "#3d3f48", "#30323a")):
        sw = (R - L) + 4 * f + k * 4
        x0 = (L + R) // 2 - sw // 2
        rect(x0, B + k * 2, x0 + sw, B + k * 2 + 2, col)
        rect(x0, B + k * 2, x0 + sw, B + k * 2 + 1, ["#9a9cab", "#7b7d8a", "#5f616c"][k])

def card(name, w, h):
    meta = json.load(open(name + ".json"))
    gw, gh = w // BLOCK, h // BLOCK
    base = render(name + ".svg", w, h).resize((gw, gh), Image.BOX)
    g = np.array(base.quantize(colors=64, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).convert("RGB")).astype(float)
    ov = np.array(render(name + "_ov.svg", w, h)).astype(float)
    mask = (np.abs(ov - np.array([255, 0, 255])).sum(2) > 90).astype(float)
    cov = np.array(Image.fromarray((mask * 255).astype(np.uint8)).resize((gw, gh), Image.BOX)).astype(float) / 255
    pre = np.stack([np.array(Image.fromarray((ov[:, :, i] * mask).astype(np.uint8)).resize((gw, gh), Image.BOX)).astype(float) for i in range(3)], 2)
    col = pre / np.maximum(cov[:, :, None], 1e-3)
    # each detail pixel takes the nearest of the details' own colours, so no magenta bleeds in
    pal = np.array([hexc(c) for c in ("#c8f6ff", "#e6fcff", "#3f5e2c", "#5b7f3a", "#3d5e2c", "#56813a")])
    col = pal[np.argmin(((col[:, :, None, :] - pal[None, None]) ** 2).sum(3), axis=2)]
    on = cov > 0.3
    g[on] = col[on] * 0.85 + g[on] * 0.15
    paint_door(g, meta["door"])
    out = Image.fromarray(np.clip(g, 0, 255).astype(np.uint8)).resize((w, h), Image.NEAREST)
    out.save(name + "_px2.png")
    return out

if __name__ == "__main__":
    for name, w, h in (("c", 1200, 1600), ("cw", 2560, 1440), ("ci", 1260, 1000)):
        card(name, w, h); print("done", name)
