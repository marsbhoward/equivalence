"""Round 5: Backswept traced from the user's Vegeta reference (its outline, scaled so ear-to-ear is the
head's 16 cells), Locs with the part running down to the brow, Waves with a second wave."""
import subprocess
import canvas
from r2gen import block
from r4gen import locs_side
from r3gen import locs as locs_r3

# The reference's outline in its own pixels (1320 x 1000; ears at x 165 / 1150, temples' hairline y 770),
# clockwise from the low spike beside the near ear. Closed along the brow; today's cap gives the bang.
VEGETA = [(150, 960), (250, 820), (185, 690), (300, 670), (372, 330), (410, 480), (700, 20), (860, 380),
          (870, 215), (900, 450), (1060, 335), (1010, 560), (1140, 710), (1080, 850), (1150, 980)]
CELL = (1150 - 165) / 16

def flame(view, BY):
    W, H = canvas.W, canvas.H
    pts = [(6 + (x - 165) / CELL, (BY + 6) - (770 - y) / CELL * 1.5) for x, y in VEGETA]   # stretched up: at true scale the spikes were single cells
    pts += [(21.0, BY + 4.0), (7.0, BY + 4.0)]
    if view == 'back': pts = [(28 - x, y) for x, y in pts]
    g = [['.'] * W for _ in range(H)]
    for y in range(H):
        for x in range(W):
            px, py, inside = x + 0.5, y + 0.5, False
            for i in range(len(pts)):
                (x1, y1), (x2, y2) = pts[i], pts[i - 1]
                if (y1 > py) != (y2 > py) and px < x1 + (py - y1) * (x2 - x1) / (y2 - y1): inside = not inside
            if inside: g[y][x] = 'L'
    return g

def tall_backswept():
    canvas.BY, canvas.H = 14, 50
    out = ["@backswept_r5 Backswept | traced from the Vegeta reference: one tall apex, a spike cut into the near side, spikes stepping down the far side; today's bang and front hairline"]
    for v in ('side', 'back'):
        g = flame(v, 14)
        ys = [y for y in range(canvas.H) if any(c != '.' for c in g[y])]
        out.append(f'{v} {ys[0]}'); out += [''.join(g[y]) for y in range(ys[0], ys[-1] + 1)]
        cr = subprocess.run(['python3', 'capblock.py', 'backswept', v], capture_output=True, text=True).stdout.rstrip('\n').split('\n')
        out.append(f'{v}_cap 14'); out += [r.replace('K', 'L') if i < 5 else r for i, r in enumerate(cr)]
    return '\n'.join(out)

def locs_part():
    g = locs_side()
    for y in range(8, 14): g[y][15] = '.'          # the part, from the crown down to the brow
    for y in (8, 9, 10): g[y][16] = '.'
    return g

if __name__ == '__main__':
    import sys
    if sys.argv[1:] == ['tall']:
        print(tall_backswept())
    else:
        print("@locs_r5 Locs | round 4, with the part running from the crown down to the brow as in the reference")
        print('side ' + block(locs_part())); print('back ' + block(locs_r3('back')))
        r3 = open('round3.txt').read()
        blk = r3.split('@waves_r3 ')[1].split('\n@')[0].split('\n')
        back = '\n'.join(blk[next(n for n, l in enumerate(blk) if l.startswith('back')):]).rstrip()
        print('''
@waves_r5 Waves | two waves nested in the turned view; the Fade line drops one more row to make room
side
10: 7-20
11: 6-21
12: 6-21 K:11-16
13: 6-21 K:9-10 K:13-14 K:17-18
14: 6-21 K:8 K:12 K:15 K:19
15: 6-21
16: K:6-21
17: D:7
18: s:7
19: d:7''')
        print(back)
