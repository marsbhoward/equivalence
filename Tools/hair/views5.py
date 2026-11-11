"""
Eight-direction heads (RIG.md, 2026-11-09): the hair's two NEW views, side and back 3/4, drafted from each
style's own two views (turned, back) so nothing is redrawn from scratch (no drift). Works in the head's DESIGN
cells (16 x 18), as the art is written. Side (facing right, the back of the head at the left):
  back third    covered where the BACK view covers that side of the head (its edge columns)
  middle third  temple and ear: covered where the TURNED view frames the face (its near edge columns)
  front third   forehead: covered only where the turned view covers the brow (a fringe)
  over the top  each row's volume as wide as the back view's (round), or the full length for a RIDGE
                style (mohawk and kin), which runs front to back
  long hair     the turned view's mane, hung further behind
Back 3/4: the back view a step round - its far side tucked away, the near side's temple at the right edge.
Front and front 3/4 both keep today's turned head. First drafts; hand fixes go on top.

    /usr/bin/python3 Tools/hair/views5.py out.png key,key,...
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
from render import compose, render, sheet
from compare import views

RIDGE = {'mohawk', 'afrohawk', 'hightop', 'talltop'}
SKULL_TOP, SKULL_BOT = 2, 17          # skull rows, in cells
HEAD3Q = [l.rstrip('\n') for l in open(os.path.join(HERE, 'head_side.txt'))]
NO_EYES = [''.join('b' if c in '123456' else c for c in r) for r in HEAD3Q]
PROFILE = [list(r) for r in NO_EYES]
for y in range(20, 24):
    for x in (26, 27): PROFILE[y][x] = '4'
PROFILE = [''.join(r) for r in PROFILE]

cells = lambda tex: [r[::2] for r in tex[::2]]
double = lambda rows: [''.join(c * 2 for c in r) for r in rows for _ in (0, 1)]
hair = lambda c: c not in '.'

def side_cells(T, B, key):
    """T, B: turned and back caps in cells (18 x 16). The side cap, cells."""
    H = len(T); out = [['.'] * 16 for _ in range(H)]
    for y in range(H):
        t, b = T[y], B[y]
        back_cov = any(hair(b[x]) for x in (14, 15))
        temple = any(hair(t[x]) for x in (0, 1))
        brow = sum(hair(t[x]) for x in range(4, 12)) >= 4
        if y < SKULL_TOP:   # over the skull
            if key in RIDGE: wide = [x for x in range(16) if hair(t[x])]; span = (0, 15) if wide else None
            else:
                xs = [x for x in range(16) if hair(b[x])]
                span = (min(xs), max(xs)) if xs else None
            if span:
                for z in range(span[0], span[1] + 1): out[y][z] = 'L'
            continue
        ridge = key in RIDGE and sum(hair(t[x]) for x in range(5, 11)) >= 2 and y < 9
        for z in range(16):
            # each third takes its own source's characters, so textures (beads, partings, rows) carry over
            if ridge: src = t[7]
            elif z <= 5: src = b[10 + z] if back_cov else '.'
            elif z <= 10: src = (t[z - 6] if hair(t[z - 6]) else t[0]) if temple else (b[15] if back_cov and y < 12 else '.')
            else: src = t[z - 4] if brow else '.'
            if hair(src): out[y][z] = 'L' if src in 'Kbkdslhxw123456' else src   # outlines are re-made at the edge
    # over-the-top rows for the skull's own top rows too, when the back view's volume is wider than the head
    return [''.join(r) for r in out]

def outline(rows):
    """K on hair cells that face empty cells - the art's own outline convention."""
    g = [list(r) for r in rows]; H = len(g); W = len(g[0])
    for y in range(H):
        for x in range(W):
            if g[y][x] not in '.K' and y < SKULL_TOP and any(not (0 <= y + dy < H and 0 <= x + dx < W) or rows[y + dy][x + dx] == '.' for dy, dx in ((-1, 0), (0, -1), (0, 1))):
                g[y][x] = 'K'
    return [''.join(r) for r in g]

def back3q_cells(B, S):
    out = []
    for y in range(len(B)):
        out.append(B[y][3:16] + S[y][8:11].replace('K', 'L'))
    return out

def shift(rows, dx):
    if not rows: return rows
    w = len(rows[0])
    return [('.' * dx + r)[:w] if dx > 0 else (r[-dx:] + '.' * (-dx)) for r in rows]

def heads(key, V):
    c3, m3, t3 = V[key]['side']; cb, mb, tb = V[key]['back']
    T, B = cells(c3), cells(cb)
    S = outline(side_cells(T, B, key))
    Q = back3q_cells(B, S)
    side_mane = shift(m3, -6) if m3 else None
    q_mane = shift(mb, -4) if mb else None
    return [
        ('turned', render(compose(HEAD3Q, c3), m3, t3)),
        ('side', render(compose(PROFILE, double(S)), side_mane, t3)),
        ('back 3/4', render(compose(NO_EYES, double(Q)), q_mane, tb, mane_over_body=True)),
        ('back', render(compose(NO_EYES, cb), mb, tb, mane_over_body=True)),
    ]

if __name__ == '__main__':
    out = sys.argv[1]; keys = sys.argv[2].split(',')
    V = views(); imgs = []
    for k in keys: imgs += [im for _, im in heads(k, V)]
    sheet(imgs, cols=4).save(out); print(out, len(keys), 'styles')


def proposals(keys, V):
    """The drafts in the Editor's proposals format (HairViews5): cells, one character per design cell."""
    out = []
    for key in keys:
        c3, m3, t3 = V[key]['side']; cb, mb, tb = V[key]['back']
        T, B = cells(c3), cells(cb)
        S = outline(side_cells(T, B, key)); Q = back3q_cells(B, S)
        out += [f"@{key}", "side.cap"] + S
        if m3: out += [f"side.mane {t3}"] + cells(shift(m3, -6))
        out += ["q.cap"] + Q
        if mb: out += [f"q.mane {tb}"] + cells(shift(mb, -4))
        out.append("")
    return "\n".join(out)
