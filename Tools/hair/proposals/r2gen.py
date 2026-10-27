"""Round 2's two derived styles: today's Afro with curl texture, today's Cornrows converging at the nape."""
import subprocess
from canvas import W, H, BX, BY, in_skull
def grid(key, view):
    out = subprocess.run(['python3', 'tocanvas.py', key], capture_output=True, text=True).stdout.splitlines()
    g = [['.'] * W for _ in range(H)]; cur = None; row = 0
    for line in out[1:]:
        if line.startswith(('side', 'back')):
            cur = line.split()[0]; row = int(line.split()[1]); continue
        if cur == view:
            for x, c in enumerate(line): g[row][x] = c
            row += 1
    return g
def block(g, start=0):
    ys = [y for y in range(H) if any(c != '.' for c in g[y])]
    return f'{ys[0]}\n' + '\n'.join(''.join(g[y]) for y in range(ys[0], ys[-1] + 1))
HAIR = set('LHDK')
def afro(view):
    g = grid('afro', view)
    hair = lambda x, y: 0 <= x < W and 0 <= y < H and g[y][x] in HAIR
    # scallop the rim: every other edge cell steps in, so the outline reads as curls
    for y in range(H):
        xs = [x for x in range(W) if g[y][x] in HAIR]
        if not xs or (BY <= y < BY + 18 and view == 'side' and False): continue
        if y % 4 == 0:
            for x in (xs[0], xs[-1]):
                if not (BX <= x < BX + 16 and BY <= y < BY + 18): g[y][x] = '.'
    for x in range(W):
        ys = [y for y in range(H) if g[y][x] in HAIR]
        if ys and x % 4 == 0 and ys[0] < BY: g[ys[0]][x] = '.'
    # curl marks: short curved hooks scattered by a hash (a lattice read as hatching), kept apart
    taken = set()
    for y in range(H):
        for x in range(W):
            if ((x * 73856093) ^ (y * 19349663)) % 11: continue
            if any((x + dx, y + dy) in taken for dx in range(-2, 3) for dy in range(-2, 3)): continue
            if not all(hair(x + dx, y + dy) for dx in range(-2, 3) for dy in range(-2, 3)): continue
            g[y][x] = 'K'; taken.add((x, y))
            shape = ((x * 7 + y * 3) % 3)
            if shape == 0: g[y][x + 1] = 'K'           # a flat dash
            elif shape == 1: g[y + 1][x + 1] = 'K'     # a hook down
    return g
def cornrows():
    g = grid('cornrows', 'back')
    for cy in range(10, 18):            # cap rows: the four rows bend in toward the nape
        y = BY + cy
        for x in range(W): g[y][x] = '.' if in_skull(x - BX, cy) else g[y][x]
        t = (cy - 9) / 8
        for c0, c1 in ((1, 4), (5, 6.5), (10, 8.5), (14, 11)):
            c = round(c0 + (c1 - c0) * t)
            for dx, ch in ((-1, 'L'), (0, 'H'), (1, 'L')):
                if in_skull(c + dx, cy): g[y][BX + c + dx] = ch
    for y, xs in ((26, (11, 12, 15, 16)), (27, (11, 12, 15, 16)), (28, (12, 15)), (29, (12, 15))):
        for x in xs: g[y][x] = 'L'
    return g
if __name__ == '__main__':
    print('@afro_r2 Afro | today\'s Afro, the rim broken into curls and small curl marks through the mass')
    print('side ' + block(afro('side'))); print('back ' + block(afro('back')))
    side = grid('cornrows', 'side')
    print('\n@cornrows_r Cornrows | today\'s rows, bending in toward the nape from behind, two short tails')
    print('side ' + block(side)); print('back ' + block(cornrows()))
