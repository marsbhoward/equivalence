"""Flat preview renders: the dumped bare head with hair composed over it and the mane behind."""
import os
from PIL import Image

HERE = os.path.dirname(__file__)
HEAD_W, HEAD_H = 32, 36

def load_head(name='head_side_detail.txt'):
    return [l.rstrip('\n') for l in open(os.path.join(HERE, name))]

SKIN = {'k': (70, 45, 40), 's': (150, 105, 85), 'd': (190, 140, 115), 'b': (235, 199, 168),
        'l': (245, 215, 190), 'h': (250, 230, 210), 'x': (240, 240, 245), 'w': (250, 250, 255)}
EYE = {'1': (20, 20, 30), '2': (60, 75, 95), '3': (80, 95, 115), '4': (92, 107, 128), '5': (140, 150, 170), '6': (220, 230, 240)}

def hair_colours(base=(107, 71, 46)):
    L = base
    D = tuple(int(c * 0.86) for c in base)
    S = tuple(int(c * 0.71) for c in base)
    K = tuple(int(c * 0.32 + 5) for c in base)
    return {'L': L, 'H': L, 'B': L, 'D': D, 'S': S, 'K': K}

def expand(grid, solid=True):
    """BodyLook.Expand: 28 authored rows (or 32 with overhead) -> 36 composed rows."""
    w = len(grid[0]); blank = '.' * w
    tall = len(grid) >= 32
    above = grid[:4] if tall else [blank] * 4
    body = grid[4:] if tall else grid
    rows = list(above)
    for y, r in enumerate(body):
        if y == 22:
            rows += [body[21] if solid else blank] * 4
        rows.append(r)
    return rows

def compose(under, over):
    out = [list(r) for r in under]
    for y, r in enumerate(over):
        if y >= len(out): break
        for x, c in enumerate(r):
            if c != '.' and x < len(out[y]): out[y][x] = c
    return [''.join(r) for r in out]

def render(head, mane=None, mane_top=32, px=8, bg=(76, 80, 90), hair=None, pad=12, body=True,
           mane_over_body=False):
    """head: 36x32 rows (skin/eye/hair chars). mane: rows, top at mane_top texels above neck."""
    hc = hair or hair_colours()
    pal = {**SKIN, **EYE, **hc}
    W = HEAD_W + 2 * pad; H = HEAD_H + 2 * pad + 20
    img = Image.new('RGB', (W * px, H * px), bg)
    P = img.load()
    def put(x, y, col):
        X, Y = x + pad, y + pad
        if 0 <= X < W and 0 <= Y < H:
            for dy in range(px):
                for dx in range(px): P[X * px + dx, Y * px + dy] = col
    def draw_mane():
        if not mane: return
        mw = len(mane[0]); ox = (HEAD_W - mw) // 2; oy = 32 - mane_top
        for y, r in enumerate(mane):
            for x, c in enumerate(r):
                if c != '.': put(x + ox, y + oy, pal.get(c, (255, 0, 255)))
    if not mane_over_body: draw_mane()
    if body:   # a rough torso and arms: turned, hanging hair draws BEHIND these; from behind, over
        for y in range(34, 34 + 22):
            for x in range(3, 29): put(x, y, (40, 42, 52))
        for y in range(36, 36 + 22):
            for x in list(range(-4, 3)) + list(range(29, 36)): put(x, y, (52, 54, 66))
    if mane_over_body: draw_mane()
    for y, r in enumerate(head):
        for x, c in enumerate(r):
            if c != '.': put(x, y, pal.get(c, (255, 0, 255)))
    return img

def sheet(imgs, cols=5, label=None):
    w, h = imgs[0].size
    rows = (len(imgs) + cols - 1) // cols
    out = Image.new('RGB', (w * cols, h * rows), (40, 40, 48))
    for i, im in enumerate(imgs): out.paste(im, ((i % cols) * w, (i // cols) * h))
    return out
