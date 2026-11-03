"""Round 9: two styles from the user's references.
  Swept Locs - locs swept back off the face into a tail at the back of the crown, a few locs left
               hanging to frame the face.
  Puff Buns  - a centre part, braided back into two big puffs high on each side, two beaded braids
               hanging to the cheeks.
Canvas as canvas.py: face to the right in the turned view; the head box cols 6-21, rows 8-25."""
import canvas
from canvas import W, H

def grid(): return [['.'] * W for _ in range(H)]
def run(g, y, a, b, ch='L'):
    for x in range(a, b + 1): g[y][x] = ch
def disc(g, cx, cy, r, squash=1.0):
    for y in range(H):
        for x in range(W):
            if ((x - cx) ** 2 + ((y - cy) / squash) ** 2) <= r * r + r * 0.6: g[y][x] = 'L'
def put(g, cells, ch='K'):
    for x, y in cells: g[y][x] = ch

# ---------------------------------------------------------------- Swept Locs
def locs_side(v):
    g = grid()
    # a lumpy top of ropes, swept back: the brow stays clear
    run(g, 8, 10, 12); run(g, 8, 14, 16)
    run(g, 9, 8, 19); run(g, 10, 7, 20)
    for y in range(11, 14): run(g, y, 6, 21)
    # the rope ends at the hairline, skin between
    run(g, 14, 6, 10); run(g, 14, 12, 13); run(g, 14, 15, 16); run(g, 14, 18, 19)
    run(g, 15, 6, 9); run(g, 16, 6, 8)
    # ropes running back and up toward the tie, one per rope end
    put(g, [(x, 13) for x in (11, 14, 17)] + [(x, 12) for x in (10, 13, 16, 19)])
    put(g, [(x, 11) for x in range(8, 12)] + [(x, 12) for x in (12,)] )
    put(g, [(x, 10) for x in range(13, 18)])
    # the tie, high at the back of the crown, and a thick tail of ropes falling behind
    run(g, 6, 4, 7); run(g, 7, 3, 8); run(g, 8, 2, 8); run(g, 9, 1, 7); run(g, 10, 0, 6)
    g[8][5] = 'K'
    bottom = 31 if v == 'a' else 27
    for y in range(11, bottom): run(g, y, 0, 6)
    run(g, bottom, 0, 1); run(g, bottom, 3, 4); run(g, bottom + 1, 3, 4); run(g, bottom - 1, 6, 6, '.')
    for y in range(13, bottom - 1): g[y][2] = 'K'
    for y in range(17, bottom - 2): g[y][4] = 'K'
    # the hanging locs: two from the front falling past the near eye to the chin, one down the far cheek
    end = 25 if v == 'a' else 23
    for y in range(15, end): run(g, y, 12, 13)
    for y in range(15, end - 2): run(g, y, 15, 16)
    for y in range(15, end - 1): run(g, y, 19, 20)
    return g

def locs_back(v):
    g = grid()
    # the tie and its knot of rope ends
    run(g, 5, 12, 15); run(g, 6, 11, 16); run(g, 7, 11, 16); run(g, 8, 12, 15)
    run(g, 9, 9, 18); run(g, 10, 7, 20)
    for y in range(11, 19): run(g, y, 6, 21)
    run(g, 19, 7, 20)
    # ropes converging on the tie
    for i, y in enumerate(range(17, 11, -1)):
        g[y][8 + i // 2] = 'K'; g[y][19 - i // 2] = 'K'
    for y in range(11, 18): g[y][13] = 'K'
    # side locs hanging past the ears
    for y in range(19, 24 if v == 'a' else 22): run(g, y, 5, 6); run(g, y, 21, 22)
    # the tail: four ropes down the back
    bottom = 35 if v == 'a' else 31
    for y in range(20, bottom): run(g, y, 10, 17)
    for y in range(22, bottom - 1):
        for x in (12, 15): g[y][x] = 'K'
    run(g, bottom, 10, 11); run(g, bottom, 13, 14); run(g, bottom, 16, 17)
    return g

# ---------------------------------------------------------------- Puff Buns
def buns_side(v):
    g = grid()
    r = 5 if v == 'a' else 4
    # the puffs: near one high at the back, far one high over the far side
    disc(g, 5, 6, r); disc(g, 22, 5, r)
    # the braided cap, close to the head
    run(g, 9, 9, 18); run(g, 10, 7, 20)
    for y in range(11, 15): run(g, y, 6, 21)
    # the part, scalp showing, from the brow back to the crown
    for y in range(11, 15): g[y][15] = '.'
    # braid rows running up into each puff
    put(g, [(13, 13), (12, 12), (11, 11), (10, 10)])
    put(g, [(18, 13), (19, 12), (20, 11)] if v == 'a' else [(18, 13), (19, 12)])
    # puff marks: shallow cups
    put(g, [(3, 4), (4, 5), (5, 5), (6, 4), (6, 8), (7, 9)])
    put(g, [(20, 3), (21, 4), (22, 4), (23, 3), (22, 7), (23, 8)])
    # the beaded braids, in front of each ear: a bead halfway and one at the end
    def braid(x0, top, end, lean):
        # a plait: two cells wide, stepping a cell aside every two rows; beads as knobs
        for y in range(top, end):
            o = lean if (y - top) // 2 % 2 else 0
            run(g, y, x0 + o, x0 + o + 1)
        mid = (top + end) // 2
        for y in (mid, end, end + 1): run(g, y, x0 - 1 + max(lean, 0), x0 + 1 + max(lean, 0))
    braid(8, 14, 24 if v == 'a' else 22, 1)
    braid(21, 14, 23 if v == 'a' else 21, -1)
    g[14][20] = '.'
    return g

def buns_back(v):
    g = grid()
    r = 5 if v == 'a' else 4
    disc(g, 6, 7, r); disc(g, 21, 7, r)
    run(g, 9, 9, 18); run(g, 10, 7, 20)
    for y in range(11, 19): run(g, y, 6, 21)
    run(g, 19, 7, 20)
    # the part down the back, and braid rows up into each puff
    for y in range(10, 19): g[y][13] = '.'
    for i, y in enumerate(range(17, 11, -1)):
        g[y][10 - i // 3] = 'K'; g[y][17 + i // 3] = 'K'
    put(g, [(4, 5), (5, 6), (6, 6), (7, 5), (19, 5), (20, 6), (21, 6), (22, 5), (5, 9), (22, 9)])
    return g

def block(name, g):
    ys = [y for y in range(H) if any(c != '.' for c in g[y])]
    return f'{name} {ys[0]}\n' + '\n'.join(''.join(g[y]) for y in range(ys[0], ys[-1] + 1))

if __name__ == '__main__':
    for key, name, note, side, back in (
            ('sweptlocs_a3', 'Swept Locs A', 'locs swept back into a high tail, three locs framing the face', locs_side, locs_back),
            ('sweptlocs_b3', 'Swept Locs B', 'as A, a shorter tail and shorter framing locs', locs_side, locs_back),
            ('puffbuns_a3', 'Puff Buns A', 'a centre part into two big puffs, beaded braids to the cheeks', buns_side, buns_back),
            ('puffbuns_b3', 'Puff Buns B', 'as A, smaller puffs', buns_side, buns_back)):
        v = key[-2]
        print(f'@{key} {name} | {note}')
        print(block('side', side(v))); print(block('back', back(v))); print()
