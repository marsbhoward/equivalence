"""Batch 6 by hand (2026-11-10): curtains, curltop, locs, waves, parted, sweptlocs, puffbuns."""
from b6lib import *
import os

# ---------------- Curtains: centre-parted curtains framing the face, rounded mane behind
tc, tm, tt = T('curtains'); bc, bm, bt = B('curtains')
side = pad(['................', '...KKKKKKKKK....', '.KKLLLLLLLLLKK..', 'KLLLLLLLLLLLLLK.',
            'LLLLLLLLLLLLLLLK', 'LLLLLLLLLLLLLLLK', 'LLLLLLLLLLLLLLLK', 'LLLLLLLLLLLLLL..',
            'LLLLLLLLLLLL....', 'LLLLLLLLLLL.....', 'LLLLLLLLLL......', 'LLLLLLLLLL......',
            'LLLLLLLLLL......', 'LLL.LLLLLL......', 'KL...LL.........'])
smane = cut(tm, tt, lambda h: 15 if h < 7 else 9)
q = cut(bc, 32, lambda h: 12 if 8 <= h <= 14 else None)
qm = cut(shift(bm, -1), bt, lambda h: 15 if h < 8 else 12)
write('curtains', 'Curtains', [('side.cap', side), ('side.mane', smane, tt), ('q.cap', q), ('q.mane', qm, bt)])

# ---------------- Curly Top: curls on top, faded sides and back (the back fades like Fade, as Tall Top)
tc, tm, tt = T('curltop'); bc, bm, bt = B('curltop')
side = pad(['L' * 16] * 3 + ['KLLLLLLLLLLLLLLK'] * 2 +
           ['.sssssssss......', '.sssssssss......', '.dddddddd.......', '.ddddddd........', '.dddddd.........'])
top = tm[:7]
q = fade_edges(cut(bc, 32, lambda h: 12 if h >= 5 else None))
write('curltop', 'Curly Top', [('turned.cap', fade_edges(tc)), ('turned.mane', tm, tt),
      ('side.cap', side), ('side.mane', top, tt),
      ('q.cap', q), ('q.mane', shift(bm[:8], -1), bt),
      ('back.cap', fade_edges(bc)), ('back.mane', bm[:8], bt)])

# ---------------- Locs: the back's locs over the crown and hanging behind, the face clear in front
tc, tm, tt = T('locs'); bc, bm, bt = B('locs')
# the crown as the turned view has it; behind the ear the back's locs; over the brow the turned view's bangs
back_w = lambda r: 10 if r <= 9 else (8 if r <= 13 else 7)
side = tc[:6] + [(bc[r][:back_w(r)] + '.' * 16)[:10] + (tc[r][8:14] if r <= 13 else '......') for r in range(6, 18)]
for r in range(13, 18): side[r] = side[r][:back_w(r)].replace('.', 'L') + side[r][back_w(r):]
w = len(bm[0]); off = (w - 16) // 2
smane = cut(bm, bt, lambda h: 15 if h < 7 else (9 if h < 11 else None))
# below the ear only the locs behind the head (no cut edge drawn down them)
smane = [row if r - (bt // 2 - 16) < 11 else row[:off + 6] + '.' * (w - off - 6) for r, row in enumerate(smane)]
q = cut(bc, 32, lambda h: 12 if 8 <= h <= 15 else None)
# a step round: the far side as the back, the near side opened to the cheek with one loc hanging past it
qm = shift(bm, -1); w = len(qm[0]); off = (w - 16) // 2
for r in range(len(qm)):
    h = r - (bt // 2 - 16)
    if 8 <= h <= 16:
        bangs = tc[h][8:12] if h <= 13 else '....'      # the side view's bangs, seen past the cheek
        qm[r] = (qm[r][:off + 11] + 'K' + bangs + '.' * w)[:w]
    elif h > 16:                                    # nothing left hanging below the cheek on the near side
        qm[r] = (qm[r][:off + 12] + '.' * w)[:w]
q = cut(bc, 32, lambda h: 11 if h >= 8 else None)
write('locs', 'Locs', [('side.cap', side), ('side.mane', smane, bt), ('q.cap', q), ('q.mane', qm, bt)])

# ---------------- Waves, reduced to Curly Top's structure (the user, 2026-11-10): the wave cap ends where
# Curly Top's hair does (row 4) and Curly Top's own fade sits under it, row for row, in every view.
def from_file(key, name):
    L = open(os.path.join(HERE, 'views5', f'{key}.txt')).read().split('\n')
    i = L.index(name) + 1
    return L[i:i + 18]
# two waves, nested: the band's top outline moves one cell up onto a one-row mane (the waves need a clear row
# under any outline, or the pass reads them as outline and fills them), the cap below holds both arcs
# The waves are rings circling the whole head with the top centre as their epicentre (the user, 2026-11-10):
# from any side a ring reads as one line running across the hair to both its ends, dipping in the middle.
# Two rings, the same in every view. (A line stops one cell short of the outline: touching it, the pass
# reads the line as outline and fills it in.)
def wave_rows(arcs, n=5):
    g = [list('K' + 'L' * 14 + 'K') for _ in range(n)]
    for y, xs in arcs.items():
        for x in xs: g[y][x] = 'Z'          # Z: a line in the hair the outline pass leaves as drawn
    return [''.join(r) for r in g]
# The ripples (the user, 2026-11-10): the flipped nested arcs, the same in every view (the back flipped too),
# each carried on up to the edge of the hair so the line reads as one ring round the head.
RINGS = {1: [1, 2, 3, 12, 13, 14], 2: range(4, 12), 3: [1, 2, 13, 14], 4: range(3, 13)}
top_row = ['.KKKKKKKKKKKKKK.']
parts = []
for v in ('turned', 'side', 'q', 'back'):
    parts.append((f'{v}.cap', wave_rows(RINGS) + from_file('curltop', f'{v}.cap')[5:]))
    parts.append((f'{v}.mane', top_row, 34))
write('waves', 'Waves', parts)

# ---------------- Parted Long: the tuft on the part, long straight hair hanging behind
tc, tm, tt = T('parted'); bc, bm, bt = B('parted')
side = pad(['......KKKK......', '....KKLLLLKK....', '..KKLLLLLLLLKK..', '.KLLLLLLLLLLLLKK',
            'KLLLLLLLLLLLLLLK', 'KLLLLLLLLLLLLLLK', 'KLLLLLLLLLLLLLLK', 'LLLLLLLLLLLLLL..',
            'LLLLLLLLLLLL....'] + ['LLLLLLLLLLLL....'] * 9)      # hair up to just behind the eye, as turned
smane = cut(bm, bt, lambda h: 15 if h < 7 else 11)
q = cut(bc, 32, lambda h: 13 if h >= 8 else None)
qm = cut(shift(bm, -1), bt, lambda h: 15 if h < 8 else 13)
# turned: today's long hair had a stepped silhouette (out at rows 9 and 12 on the far side, a shelf at row 14
# on the near side); now it falls straight on both sides like the back, a strand each side below the chin
W = len(tm[0])
def span(a, b, line=None):
    r = ['.'] * W
    r[a] = r[b] = 'K'
    for x in range(a + 1, b): r[x] = 'L'
    if line is not None: r[line] = 'K'
    return ''.join(r)
tmane = tm[:4] + [span(4, 21), span(3, 22)] + [span(2, 22)] * 11
for r in range(17, 31):
    left = span(2, 8, 5 if r >= 19 else None); right = span(18, 22)
    tmane.append(''.join(a if a != '.' else b for a, b in zip(left, right)))
tmane.append(span(2, 8).replace('L', 'K')[:9] + '.' * 9 + span(18, 22).replace('L', 'K')[18:])
# the cap's own side outline would now draw a line inside the hair: the mane is the edge there
tcap = [(('L' + r[1:]) if 4 <= y <= 8 and r[0] == 'K' else r) for y, r in enumerate(tc)]
tcap = [((r[:15] + 'L') if 4 <= y <= 16 and r[15] == 'K' else r) for y, r in enumerate(tcap)]
# the near side's hair cleared back a strand from the eye, to sit where the side view has it
tcap = [(r[:12] + '..' + r[14:]) if 7 <= y <= 17 else r for y, r in enumerate(tcap)]
write('parted', 'Parted Long', [('turned.cap', tcap), ('turned.mane', tmane, tt), ('side.cap', side), ('side.mane', smane, bt), ('q.cap', q), ('q.mane', qm, bt)])

# ---------------- Swept Locs: the locs swept back over the crown, one lock over the brow, the tail behind
tc, tm, tt = T('sweptlocs'); bc, bm, bt = B('sweptlocs')
side = pad(tc[:6] + ['LLLLLLLLLLLLLLL.', 'LLLLLLLLLLL..LL.', 'LLLLLLLLLL...LL.', 'LLLLLLLLL....LL.',
                     'LLLLLLLLL.......', 'LLLLLLLLL.......', 'LLLLLLLL........', 'LLLLLLLL........',
                     'LLLLLLL.........', 'KLLLLLK.........'])
smane = cut(tm, tt, lambda h: 15 if h < 4 else 6)
q = cut(bc, 32, lambda h: 12 if 8 <= h <= 16 else None)
qm = cut(shift(bm, -1), bt, lambda h: 15 if h < 8 else (12 if h <= 16 else None))
# LINE WORK for the tail (the user, 2026-11-11), in Z so the pass keeps it: where the gathered locs leave the
# head and hang, a line between them and the hair on the head, and the strand lines down the tail.
def put(rows, cells):
    rows = list(rows)
    for y, x in cells:
        if 0 <= y < len(rows) and rows[y][x] not in '.':
            rows[y] = rows[y][:x] + 'Z' + rows[y][x + 1:]
    return rows
# turned and side: the tail hangs beside the head (mane cols 0-6); its inner edge drawn over the cap's side,
# its two strand lines down its length
def front(cap, mane):
    cap = put(cap, [(y, 0) for y in range(4, 17)])                   # the tail's inner edge only
    mane = put(mane, [(y, 2) for y in range(9, 23)])                  # one strand line, as today's art has
    mane = [r[:4] + 'L' + r[5:] if 7 <= y <= 24 and r[4] == 'K' else r for y, r in enumerate(mane)]
    return cap, mane
tcap, tmane = front(tc, tm)
side, smane = front(side, smane)
# back and back 3/4: the tail gathered under a tie at mid-back, its edges carried up to it, strands down it
def behind(mane, dx):
    return put(mane, [(y, x + dx) for y in range(15, 19) for x in (5, 12)])   # only where it leaves the hair
qm = behind(qm, -1)
write('sweptlocs', 'Swept Locs', [('turned.cap', tcap), ('turned.mane', tmane, tt), ('side.cap', side),
      ('side.mane', smane, tt), ('q.cap', q), ('q.mane', qm, bt), ('back.cap', bc), ('back.mane', behind(bm, 0), bt)])

# ---------------- Puff Buns: one bun over the crown from the side (the far one behind it)
tc, tm, tt = T('puffbuns'); bc, bm, bt = B('puffbuns')
bun = [r[17:28] for r in tm[:10]]                 # the right bun, 11 cells
w = len(tm[0]); off = (w - 16) // 2
smane = []
for r, row in enumerate(tm):
    if r < 10:
        s = '.' * (off + 3) + bun[r]; s = (s + '.' * w)[:w]
    else:
        s = '.' * off + row[off:]
    smane.append(s)
smane = cut(smane, tt, lambda h: 15 if h < 7 else 5)
# the hair behind kept inside the crown's rounded top, so it doesn't show at the corners
round_top = {0: (5, 10), 1: (4, 11), 2: (2, 13), 3: (1, 14)}
w = len(smane[0]); off = (w - 16) // 2
for r in range(len(smane)):
    h = r - (tt // 2 - 16)
    if h in round_top and r >= 9:
        a, b = round_top[h]
        smane[r] = ''.join(c if a + off <= x <= b + off else '.' for x, c in enumerate(smane[r]))
# side (second pass): the crown to the hairline at row 6; below it the hair at the back of the head, the ear
# clear, and the turned view's wavy strand coming down beside the eye, a cell clear of it
side = pad(['................', '....KKKKKKKK....', '..KKLLLLLLLLKK..', '.KLLLLLLLLLLLLK.',
            'KLLLLLLLLLLLLLLK', 'KLLLLLLLLLLLLLLK', 'KLLLLLLLLLLLLLLK',
            'LLLLLL...LL.....', 'LLLLLL....LL....', 'LLLLLL....LL....', 'LLLLLL...LL.....',
            'LLLLLL...LLL....', 'LLLLLL....LL....', 'LLLLLL....LL....', 'LLLLL....LL.....',
            'LLLLL....LL.....', 'KLLLK....KLL....', '.KKK.....KKK....'])
q = cut(bc, 32, lambda h: 12 if h >= 8 else None)
qm = cut(shift(bm, -1), bt, lambda h: None if h < 8 else 12)
# the side view's wavy strand, seen past the near cheek: one cell wide down its outer edge to the jaw
# (kept inside the head's rounded jaw, ending above it)
wave = {8: 15, 9: 15, 10: 14, 11: 14, 12: 15, 13: 14, 14: 14}
q = [r[:x] + 'L' + r[x + 1:] if (x := wave.get(y)) is not None else r for y, r in enumerate(q)]
write('puffbuns', 'Puff Buns', [('side.cap', side), ('side.mane', smane, tt), ('q.cap', q), ('q.mane', qm, bt)])
print('ok')
