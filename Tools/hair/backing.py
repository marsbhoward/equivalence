"""An outline-backing mane (the style's mane is only a K ring round the cap, past the head's edge):
built from a cap the way the existing views draw it. Cells in, cells out."""
def backing(cap, width=18):
    off = (width - 16) // 2
    rows = [['.'] * width for _ in cap]
    for y, r in enumerate(cap):
        for x, c in enumerate(r):
            if c != '.': rows[y][x + off] = c
    out = [row[:] for row in rows]
    for y in range(len(rows)):
        for x in range(width):
            if rows[y][x] != '.': continue
            for dx in (-1, 1):
                if 0 <= x + dx < width and rows[y][x + dx] not in '.K' and (x + dx - off in (0, 15)):
                    out[y][x] = 'K'
    return [''.join(r) for r in out]
