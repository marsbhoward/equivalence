"""Backswept, round 4: the top as a tall flame after the user's Vegeta reference - the tallest spike in
the middle, the others angled out and down the sides - on a TALL canvas (the head moved down to row 14).
Today's cap (the bang and the front hairline) is kept as is."""
import subprocess
import canvas
canvas.BY, canvas.H = 14, 50
W, H, BY = canvas.W, canvas.H, canvas.BY

def tri(g, t, a, b):
    def side(p, q, r): return (p[0] - r[0]) * (q[1] - r[1]) - (q[0] - r[0]) * (p[1] - r[1])
    for y in range(H):
        for x in range(W):
            p = (x + 0.5, y + 0.5)
            d1, d2, d3 = side(p, t, a), side(p, a, b), side(p, b, t)
            if not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0)): g[y][x] = 'L'

def flame(view):
    g = [['.'] * W for _ in range(H)]
    def spike(tip, base, w):
        dx, dy = tip[0] - base[0], tip[1] - base[1]; n = (dx * dx + dy * dy) ** 0.5
        px, py = -dy / n * w / 2, dx / n * w / 2
        return tip, (base[0] + px, base[1] + py), (base[0] - px, base[1] - py)
    if view == 'side':
        spikes = [spike((12, 0), (12.5, 15), 17), spike((2, 4), (9, 13), 10), spike((0, 11), (7, 15), 8),
                  spike((21, 2), (15, 12), 10), spike((25, 8), (18, 14), 8), spike((6, 1), (10, 12), 8)]
        rows = range(12, 17)
    else:
        left = [((13.5, 0), (13.5, 15), 17), ((4, 3), (10, 13), 10), ((0.5, 9), (8, 15), 8), ((8, 1), (11, 12), 8)]
        spikes = []
        for t, c, w in left:
            spikes.append(spike(t, c, w)); spikes.append(spike((27 - t[0], t[1]), (27 - c[0], c[1]), w))
        rows = range(12, 17)
    for s in spikes: tri(g, *s)
    for y in rows:
        for x in range(6, 22): g[y][x] = 'L'
    return g

def cap(k, v):
    return subprocess.run(['python3', 'capblock.py', k, v], capture_output=True, text=True).stdout.rstrip('\n')

def rows(g):
    ys = [y for y in range(H) if any(c != '.' for c in g[y])]
    return ys[0], [''.join(g[y]) for y in range(ys[0], ys[-1] + 1)]

if __name__ == '__main__':
    out = ["@backswept_r4b Backswept | the top a tall flame after Vegeta: the tallest spike in the middle, the rest angled out and down; today's bang and front hairline"]
    for v in ('side', 'back'):
        y0, fl = rows(flame(v))
        out.append(f'{v} {y0}'); out += fl
        out.append(f'{v}_cap {BY}')
        cr = cap('backswept', v).split('\n')
        # the cap's old top outline now sits inside the flame - it would draw as two marks
        cr = [r.replace('K', 'L') if i < 5 else r for i, r in enumerate(cr)]
        out += cr
    open('round4_tall.txt', 'w').write('\n'.join(out) + '\n')
