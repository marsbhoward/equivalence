"""Round 4: Locs' turned view after the reference (a centre part on the head's midline, ropes rising
from it and drooping out, curtains over the brow, strands across the face), Cornrows (today's side
cut straight across at round 1's hairline), Waves (one long hump in the turned view)."""
from r2gen import grid, block
from r3gen import rope, locs as locs_r3
from canvas import W, H, BX, BY, in_skull

def locs_side():
    g = [['.'] * W for _ in range(H)]
    for y in range(9, 14):                      # the scalp under the ropes, open at the part
        for x in range(6, 22):
            if not (x in (15, 16) and y < 10): g[y][x] = 'L'
    ropes = [
        # near side, from the part out over the crown and down past the ear
        ((14, 9), (10, 3), (3, 8)), ((14, 10), (8, 4), (1, 13)), ((13, 11), (7, 7), (1, 18)),
        ((13, 12), (6, 10), (2, 23)), ((12, 13), (7, 13), (4, 26)), ((11, 13), (8, 17), (6, 24)),
        # the far side, foreshortened
        ((16, 9), (20, 3), (25, 8)), ((17, 10), (22, 6), (27, 13)), ((18, 11), (23, 11), (25, 19)),
        ((19, 12), (22, 16), (23, 24)),
        # the two that stand up behind the part
        ((12, 9), (11, 4), (12, 1)), ((10, 9), (8, 4), (7, 1)),
        # curtains from the part over the brow
        ((15, 11), (13, 14), (12, 17)), ((16, 11), (18, 14), (19, 17)),
    ]
    for r in ropes: rope(g, *r)
    for a, c, b in (((15, 12), (15, 17), (14, 21)), ((16, 12), (16, 16), (16, 20))):
        rope(g, a, c, b, w=1, edge=False)
    for y in (8, 9): g[y][15] = g[y][16] = '.'   # the part, seen from the front
    return g

def cornrows_side():
    g = grid('cornrows', 'side')
    s_row = g[16][:]                              # today's straight bottom line
    for y in (14, 15, 16):
        for x in range(BX, BX + 16):
            if in_skull(x - BX, y - BY): g[y][x] = '.'
    for x in range(5, BX + 16):
        if s_row[x] != '.': g[14][x] = s_row[x]
    for y in (15, 16): g[y][6] = g[y][7] = 'L'
    return g

if __name__ == '__main__':
    r3 = open('round3.txt').read()
    def view_of(text, key, v):
        blk = text.split('@' + key + ' ')[1].split('\n@')[0].split('\n')
        i = next(n for n, l in enumerate(blk) if l.startswith(v))
        j = next((n for n, l in enumerate(blk) if n > i and l.startswith(('side', 'back'))), len(blk))
        return '\n'.join(blk[i:j]).rstrip()
    print("@locs_r4b Locs | after the reference: the part on the head's midline, matching the back; ropes rise from it and droop, curtains fall over the brow, thin strands hang across the face")
    print('side ' + block(locs_side())); print('back ' + block(locs_r3('back')))
    print("\n@cornrows_r4 Cornrows | round 2's rows ending on round 1's straight line across the front; the back unchanged")
    print('side ' + block(cornrows_side())); print(view_of(r3, 'cornrows_r3', 'back'))
    print('''
@waves_r4 Waves | the turned view's waves as one long hump, like the back
side
10: 7-20
11: 6-21
12: 6-21 K:11-16
13: 6-21 K:10 K:17
14: 6-21
15: K:6-21
16: D:7
17: s:7
18: d:7''')
    print(view_of(r3, 'waves_r3', 'back'))
