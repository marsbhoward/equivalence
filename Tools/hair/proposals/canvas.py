"""Canvas authoring for hair proposals.

A view is drawn on one canvas, 28 cells wide, rows counted from CANVAS ROW 0. The head's 16x18
cell box sits at columns 6-21, rows 8-25 (cap rows 0-17; the skull starts at canvas row 10).
Chars: L hair, K explicit line/outline, . empty (a gap inside the cap shows skin), s/d/b skin
tones kept as drawn (fades). Edges are outlined automatically (K) where hair meets background.

File format:
    @key Display Name
    side 4            <- following rows start at canvas row 4
    ......LLLL....
    back 6
    ...
"""
import re, sys
W, H = 28, 44
BX, BY = 6, 8          # head box origin on the canvas
HAIR = set('LKDH')

# skull occupancy in cap cells (rows 2..17): (row, x0, x1) inclusive
SKULL = {2: (3, 12), 3: (1, 14), 15: (1, 14), 16: (2, 13), 17: (4, 11)}
for r in range(4, 15): SKULL[r] = (0, 15)

def in_skull(cx, cy):
    s = SKULL.get(cy)
    return s is not None and s[0] <= cx <= s[1]

def parse(path):
    styles, cur, view, row = [], None, None, 0
    for raw in open(path):
        line = raw.rstrip('\n')
        if not line.strip() or line.lstrip().startswith('#'): continue
        if line.startswith('@'):
            k, _, name = line[1:].partition(' ')
            name, _, note = name.partition('|')
            cur = {'key': k, 'name': name.strip(), 'note': note.strip(), 'side': None, 'back': None}
            styles.append(cur); continue
        m = re.match(r'^(side|back)(_cap)?\s*(-?\d*)\s*$', line.strip())
        if m:
            view, row = m.group(1), int(m.group(3) or 0)
            if not m.group(2): cur[view] = [['.'] * W for _ in range(H)]
            continue
        m = re.match(r'^\s*(\d+)\s*:(.*)$', line)
        if m:   # run form - "12: 4-9 11 s:6-11" (L unless a char is given), later runs win
            y = int(m.group(1))
            for seg in m.group(2).split():
                ch, _, rng = seg.rpartition(':')
                a, _, b = rng.partition('-')
                for x in range(int(a), int(b or a) + 1): cur[view][y][x] = ch or 'L'
            continue
        cells = line.strip()
        for x, c in enumerate(cells[:W]):
            if c != '.': cur[view][row][x] = c
        row += 1
    return styles

def outline(grid, cap_rule):
    """Hair edge cells become K where they face the background (4-neighbour)."""
    h, w = len(grid), len(grid[0])
    out = [r[:] for r in grid]
    for y in range(h):
        for x in range(w):
            if grid[y][x] not in HAIR: continue
            for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
                nx, ny = x+dx, y+dy
                empty = not (0 <= nx < w and 0 <= ny < h) or grid[ny][nx] not in HAIR and not grid[ny][nx].islower()
                if empty and cap_rule(nx, ny):
                    out[y][x] = 'K'; break
    return out

FILL = False   # round 2 on: the mane runs on behind the skull (hidden), so no outline rings the face

def split(canvas):
    """-> (cap 16x18 rows, mane rows or None, maneTop)"""
    if canvas is None: return None
    # cap: hair inside the head box; edges outlined only where they face the background (not the face)
    def bg_cap(nx, ny):
        cx, cy = nx - BX, ny - BY
        return not in_skull(cx, cy)
    full = outline(canvas, bg_cap)
    cap = []
    for cy in range(18):
        cap.append(''.join(full[BY+cy][BX+cx] if full[BY+cy][BX+cx] in HAIR or full[BY+cy][BX+cx].islower() else '.'
                           for cx in range(16)))
    outside = any(canvas[y][x] in HAIR for y in range(H) for x in range(W)
                  if not (BX <= x < BX+16 and BY <= y < BY+18))
    if not outside: return cap, None, 32
    src = [r[:] for r in canvas]
    if FILL:
        for cy in range(18):
            for cx in range(16):
                if in_skull(cx, cy) and src[BY+cy][BX+cx] not in HAIR: src[BY+cy][BX+cx] = 'L'
    m = outline(src, lambda nx, ny: True)
    ys = [y for y in range(H) if any(m[y][x] in HAIR for x in range(W))]
    xs = [x for x in range(W) if any(m[y][x] in HAIR for y in range(H))]
    ext = max(14 - min(xs), max(xs) + 1 - 14, 8)
    rows = []
    for y in range(ys[0], ys[-1] + 1):
        rows.append(''.join(m[y][x] if 0 <= x < W and m[y][x] in HAIR else '.' for x in range(14-ext, 14+ext)))
    mane_top = 32 - 2 * (ys[0] - BY)
    return cap, rows, mane_top

def emit(styles, path):
    """The HairSheet.LoadProposals format."""
    with open(path, 'w') as f:
        for s in styles:
            f.write(f"@{s['key']} {s['name']}\n")
            for view in ('side', 'back'):
                r = split(s[view])
                if r is None: continue
                cap, mane, top = r
                f.write(f"{view}.cap\n" + '\n'.join(cap) + '\n')
                if mane: f.write(f"{view}.mane {top}\n" + '\n'.join(mane) + '\n')
            f.write('\n')

if __name__ == '__main__':
    if '--fill' in sys.argv: FILL = True; sys.argv.remove('--fill')
    emit(parse(sys.argv[1]), sys.argv[2])
