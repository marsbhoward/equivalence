"""Round 6: Cornrows' turned view rotated (rows fan back over the head instead of standing vertical),
Waves (two humps stacked, not nested), Locs (the part filled, the bangs single strands), Backswept
(every spike a flame tongue pointing up)."""
import subprocess
import canvas
from r2gen import grid, block
from r3gen import rope, locs as locs_r3
from canvas import W, H, BX, BY, in_skull

def cornrows_side():
    g = grid('cornrows', 'side')
    for y in range(11, 16):                      # close today's vertical rows...
        xs = [x for x in range(W) if g[y][x] not in '.']
        for x in range(xs[0], xs[-1] + 1):
            if g[y][x] == '.': g[y][x] = 'L'
    # ...and part new ones on one slant, rising back over the head - parallel, a cell of scalp each
    for y, gaps in ((11, (9, 14, 19)), (12, (10, 15, 20)), (13, (11, 16)), (14, (12, 17)), (15, (13, 18))):
        for x in gaps:
            if g[y][x] in 'LH': g[y][x] = '.'
    return g

def locs_side():
    g = [['.'] * W for _ in range(H)]
    for y in range(8, 14):
        for x in range(6, 22):
            if y > 8 or 9 <= x <= 19: g[y][x] = 'L'
    ropes = [
        ((14, 9), (10, 3), (3, 8)), ((14, 10), (8, 4), (1, 13)), ((13, 11), (7, 7), (1, 18)),
        ((13, 12), (6, 10), (2, 23)), ((12, 13), (7, 13), (4, 26)), ((11, 13), (8, 17), (6, 24)),
        ((16, 9), (20, 3), (25, 8)), ((17, 10), (22, 6), (27, 13)), ((18, 11), (23, 11), (25, 19)),
        ((19, 12), (22, 16), (23, 24)),
        ((12, 9), (11, 4), (12, 1)), ((10, 9), (8, 4), (7, 1)),
    ]
    for r in ropes: rope(g, *r)
    # the bangs: single strands, as in the reference, from the part down over the face
    for a, c, b in (((14, 12), (13, 16), (12, 18)), ((15, 12), (15, 17), (14, 21)), ((16, 12), (16, 16), (16, 21)),
                    ((17, 12), (18, 15), (19, 17))):
        rope(g, a, c, b, w=1, edge=False)
    return g

def locs_back():
    g = locs_r3('back')
    for y in (7, 8): g[y][13] = 'L'              # the part filled in
    return g

FLAME = {1: '14', 2: '14', 3: '13-14', 4: '13-15 19', 5: '8 12-15 19', 6: '8 12-16 18-19', 7: '8 11-16 18-19',
         8: '8-9 11-16 17-20 22', 9: '8-9 10-20 21-22', 10: '9-19 21-22', 11: '9-22', 12: '8-21',
         13: '4 8-21 24', 14: '4-5 7-21 23-24', 15: '5-6 7-24', 16: '5-22', 17: '6-21 23', 18: '22-23', 19: '22-23'}

def mirror(spec):
    out = []
    for seg in spec.split():
        a, _, b = seg.partition('-'); b = b or a
        out.append(f'{27 - int(b)}-{27 - int(a)}')
    return ' '.join(out)

def backswept():
    out = ["@backswept_r6 Backswept | a flame: every spike a tongue pointing up, the low ones leaning out, none jutting sideways; today's bang and front hairline"]
    for v in ('side', 'back'):
        out.append(v)
        for y, s in FLAME.items(): out.append(f'{y}: ' + (s if v == 'side' else mirror(s)))
        cr = subprocess.run(['python3', 'capblock.py', 'backswept', v], capture_output=True, text=True).stdout.rstrip('\n').split('\n')
        out.append(f'{v}_cap 14'); out += [r.replace('K', 'L') if i < 5 else r for i, r in enumerate(cr)]
    return '\n'.join(out)

if __name__ == '__main__':
    import sys
    if sys.argv[1:] == ['tall']:
        print(backswept()); sys.exit()
    print("@locs_r6 Locs | round 4 with the part filled in and the bangs as single strands over the face")
    print('side ' + block(locs_side())); print('back ' + block(locs_back()))
    print('''
@waves_r6 Waves | two humps stacked, not nested, like the back
side
10: 7-20
11: 6-21
12: 6-21 K:10-16
13: 6-21 K:9 K:17
14: 6-21 K:10-16
15: 6-21 K:9 K:17
16: K:6-21
17: D:7
18: s:7
19: d:7''')
    r3 = open('round3.txt').read()
    blk = r3.split('@waves_r3 ')[1].split('\n@')[0].split('\n')
    print('\n'.join(blk[next(n for n, l in enumerate(blk) if l.startswith('back')):]).rstrip())
    print("\n@cornrows_r6c Cornrows | the turned view rotated: the rows run back over the head on a slant; today's straight line at the brow")
    print('side ' + block(cornrows_side()))
