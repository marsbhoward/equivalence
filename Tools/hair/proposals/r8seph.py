"""Round 8: a Sephiroth-like cut - very long, straight, centre-parted, two strands falling between
the eyes, the back a straight curtain to the waist. Turned, the mane hangs wide of the arm (the body
is ~20 cells across) so the length shows; inner splits stop short of the outline (ThinOutline)."""
import canvas
from canvas import W, H, BX, BY, in_skull

def grid(): return [['.'] * W for _ in range(H)]
def run(g, y, a, b, ch='L'):
    for x in range(a, b + 1): g[y][x] = ch

def side(variant):
    g = grid()
    bottom = 39 if variant == 'b' else 42
    # crown, closed to the skull's edge; forehead open in the middle
    run(g, 10, 9, 18); run(g, 11, 7, 21)
    for y in (12, 13, 14): run(g, y, 6, 21)
    run(g, 15, 6, 11); run(g, 15, 14, 15); run(g, 15, 18, 21)
    run(g, 16, 6, 9); run(g, 16, 14, 15); run(g, 16, 19, 21)
    # the strands: a lift over the crown, falling between the eyes
    if variant == 'c':   # an open arch: up from the crown, over, and down the front
        run(g, 5, 15, 18); run(g, 6, 14, 19); run(g, 7, 13, 20); run(g, 8, 12, 14); run(g, 8, 18, 21); run(g, 9, 12, 14); run(g, 9, 19, 21)
    else:
        run(g, 8, 14, 16); run(g, 9, 13, 17)
    for y in (17, 18): run(g, y, 14, 15)
    run(g, 19, 15, 15)
    # behind: the back of the head, then a curtain hanging wide of the arm
    for y in range(17, 20): run(g, y, 4, 8)
    for y in range(20, bottom):
        run(g, y, 1 if y < bottom - 4 else 2, 9)
    run(g, bottom, 2, 4); run(g, bottom, 6, 8)
    for y in range(24, bottom - 2): g[y][5] = 'K'
    # far side: framing the cheek, then down behind the far shoulder
    for y in range(17, 22): run(g, y, 19, 21)
    lock = 37 if variant == 'b' else 33
    for y in range(22, lock): run(g, y, 19, 25 if y < lock - 2 else 24)
    run(g, lock, 21, 23)
    return g

def back(variant):
    g = grid()
    bottom = 39 if variant == 'b' else 42
    run(g, 8, 13, 15); run(g, 9, 12, 16)
    run(g, 10, 9, 18); run(g, 11, 7, 20)
    for y in range(12, 22): run(g, y, 6, 21)
    for y in range(22, bottom):
        x0, x1 = (5, 22) if y < bottom - 8 else (6, 21) if y < bottom - 3 else (7, 20)
        run(g, y, x0, x1)
    # tips: separate falls along the hem
    run(g, bottom, 7, 9); run(g, bottom, 11, 13); run(g, bottom, 15, 17); run(g, bottom, 19, 20)
    # straight falls: splits that stop short of the hem and the outline
    # (a row of hair under each, or the tip gaps' outline reaches them; none above row 27, the head sprite's edge)
    for x, top in ((10, 28), (14, 27), (18, 28)):
        for y in range(top, bottom - 2): g[y][x] = 'K'
    for x, top in ((12, 32), (16, 32)):
        for y in range(top, bottom - 1): g[y][x] = 'K'
    # a centre part down the crown
    for y in range(11, 15): g[y][14] = 'K'
    return g

def block(name, g):
    ys = [y for y in range(H) if any(c != '.' for c in g[y])]
    return f'{name} {ys[0]}\n' + '\n'.join(''.join(g[y]) for y in range(ys[0], ys[-1] + 1))

if __name__ == '__main__':
    for v, name, note in (('a', 'Sephiroth A', 'to the waist, strands between the eyes'),
                          ('b', 'Sephiroth B', 'a shorter back, longer front lock'),
                          ('c', 'Sephiroth C', 'as A, the strands an open arch over the crown')):
        print(f'@seph_{v} {name} | {note}')
        print(block('side', side(v))); print(block('back', back(v))); print()
