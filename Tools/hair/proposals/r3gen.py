"""Round 3's generated styles: the Afro (today's silhouette, a puff pattern inside), Locs (thick ropes
radiating from a centre part, after the user's second reference), Cornrows (a front hairline)."""
from r2gen import grid, block
from canvas import W, H, BX, BY, in_skull
HAIR = set('LHDK')

def afro(view):
    g = grid('afro', view)
    hair = lambda x, y: 0 <= x < W and 0 <= y < H and g[y][x] in HAIR and g[y][x] != 'K'
    taken = []
    # puffs: the underside of each clump is a shallow cup of outline, so the light pools on top of it
    for y in range(H):
        for x in range(W):
            if ((x * 2654435761) ^ (y * 40503)) % 9: continue
            if any(abs(x - a) < 4 and abs(y - b) < 3 for a, b in taken): continue
            cells = [(x, y - 1), (x + 1, y), (x + 2, y), (x + 3, y - 1)]
            if not all(hair(cx + dx, cy + dy) for cx, cy in cells for dx in (-1, 0, 1) for dy in (-1, 0, 1)): continue
            for cx, cy in cells: g[cy][cx] = 'K'
            taken.append((x, y))
    return g

def rope(g, a, c, b, w=2, edge=True):
    """A rope along a quadratic curve root a -> control c -> tip b. Where it crosses hair already drawn
    (a rope behind it), its border is lined, so each rope reads as its own."""
    pts = []
    for i in range(41):
        t = i / 40
        x = (1 - t) ** 2 * a[0] + 2 * (1 - t) * t * c[0] + t * t * b[0]
        y = (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * c[1] + t * t * b[1]
        for dx in range(w):
            for dy in range(w):
                p = (round(x) + dx, round(y) + dy)
                if 0 <= p[0] < W and 0 <= p[1] < H and p not in pts: pts.append(p)
    mine = set(pts)
    if edge:
        for x, y in pts:
            for nx, ny in ((x+1,y),(x-1,y),(x,y+1),(x,y-1)):
                if (nx, ny) not in mine and 0 <= nx < W and 0 <= ny < H and g[ny][nx] == 'L': g[ny][nx] = 'K'
    for x, y in pts: g[y][x] = 'L'

def locs(view):
    g = [['.'] * W for _ in range(H)]
    if view == 'side':
        for y in range(8, 14):
            for x in range(6, 22):
                if y > 9 or 8 <= x <= 19: g[y][x] = 'L'
        back = [((14,8),(9,5),(2,14)), ((13,9),(8,7),(1,18)), ((12,10),(7,9),(2,23)), ((11,11),(6,12),(4,27)),
                ((10,9),(5,4),(0,9)), ((15,8),(14,3),(11,2)), ((17,8),(20,3),(24,7)), ((18,9),(22,6),(26,12)),
                ((19,10),(23,10),(25,18)), ((19,12),(22,15),(23,24))]
        front = [((16,10),(13,14),(12,17)), ((17,10),(17,15),(15,18)), ((18,11),(20,15),(21,19))]
        strands = [((15,12),(15,17),(14,21)), ((16,12),(17,17),(17,21))]
    else:
        for y in range(7, 21):
            for x in range(5, 23):
                if y > 8 or 8 <= x <= 19: g[y][x] = 'L'
        left = [((13,7),(8,4),(2,10)), ((13,8),(7,7),(1,15)), ((12,9),(6,10),(2,21)), ((12,10),(7,13),(4,26)),
                ((12,11),(9,16),(8,29)), ((13,12),(12,18),(11,31)), ((12,7),(11,3),(9,2))]
        back = []
        for i, (p, c, t) in enumerate(left):
            back.append((p, c, t)); back.append(((26 - p[0], p[1]), (26 - c[0], c[1]), (26 - t[0], t[1])))
        front, strands = [], []
    for r in back + front: rope(g, *r)
    for r in strands: rope(g, *r, w=1, edge=False)
    if view == 'back':
        for y in (7, 8): g[y][13] = '.'
    return g

def cornrows_side():
    g = grid('cornrows', 'side')
    limit = {14: 20, 15: 12, 16: 9}
    for y, lim in limit.items():
        for x in range(W):
            if x > lim and BX <= x < BX + 16: g[y][x] = '.' if in_skull(x - BX, y - BY) else g[y][x]
    return g

if __name__ == '__main__':
    import subprocess
    print("@afro_r3 Afro | today's silhouette exactly; inside it, puffs - each clump's underside a shallow cup")
    print('side ' + block(afro('side'))); print('back ' + block(afro('back')))
    print("\n@locs_r3 Locs | thick ropes from a centre part, flaring out and up, falling round the face, thin strands across it")
    print('side ' + block(locs('side'))); print('back ' + block(locs('back')))
    back = [l for l in open('round2.txt').read().split('@cornrows_r')[1].split('\n@')[0].split('\n') ]
    bi = back.index(next(l for l in back if l.startswith('back')))
    print("\n@cornrows_r3 Cornrows | round 2's back; from the side the rows stop at a hairline above the brow")
    print('side ' + block(cornrows_side())); print('\n'.join(back[bi:]).rstrip())
