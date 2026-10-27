"""Round 7: Cornrows' back with its three scalp lines running from the top of the head to the nape
(meeting the turned view's three over the crown); Waves' turned view with arcs shaped like the back's."""
import canvas
from canvas import W, H, BX, BY, in_skull

def cornrows_back():
    g = [r[:] for r in [x for x in canvas.parse('round2.txt') if x['key'] == 'cornrows_r'][0]['back']]
    gaps = {10: (13, 14), 11: (9, 13, 14, 18), 12: (9, 13, 14, 18), 13: (9, 13, 14, 18), 14: (9, 13, 14, 18), 15: (9, 13, 14, 18),
            16: (9, 13, 14, 18), 17: (9, 13, 14, 18), 18: (9, 13, 14, 18), 19: (9, 13, 14, 18), 20: (10, 13, 14, 17),
            21: (10, 13, 14, 17), 22: (11, 13, 14, 16), 23: (11, 13, 14, 16), 24: (11, 13, 14, 16), 25: (13, 14)}
    for y, gs in gaps.items():
        for x in range(BX, BX + 16):
            if in_skull(x - BX, y - BY): g[y][x] = '.' if x in gs else 'L'
    return g

def block_rows(g):
    ys = [y for y in range(H) if any(c != '.' for c in g[y])]
    return f'{ys[0]}\n' + '\n'.join(''.join(g[y]) for y in range(ys[0], ys[-1] + 1))

if __name__ == '__main__':
    print("@cornrows_r7 Cornrows | round 6's turned view; from behind, the three scalp lines run from the top of the head to the nape, where the rows meet in two tails")
    side = [x for x in canvas.parse('round6.txt') if x['key'] == 'cornrows_r6c']
    print('back ' + block_rows(cornrows_back()))
    r3 = open('round3.txt').read()
    blk = r3.split('@waves_r3 ')[1].split('\n@')[0].split('\n')
    back = '\n'.join(blk[next(n for n, l in enumerate(blk) if l.startswith('back')):]).rstrip()
    print('''
@waves_r7 Waves | one arc in the turned view, the same shape as the back's: a short top stepping down twice each side
side
10: 7-20
11: 6-21
12: 6-21 K:12-15
13: 6-21 K:10-11 K:16-17
14: 6-21 K:9 K:18
15: 6-21
16: K:6-21
17: D:7
18: s:7
19: d:7
''' + back + '''

@waves_r7b Waves | two narrower arcs stacked, each a short top stepping down once each side
side
10: 7-20
11: 6-21
12: 6-21 K:12-15
13: 6-21 K:10-11 K:16-17
14: 6-21 K:12-15
15: 6-21 K:10-11 K:16-17
16: K:6-21
17: D:7
18: s:7
19: d:7
''' + back)
