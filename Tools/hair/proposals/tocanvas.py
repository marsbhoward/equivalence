"""Print an existing style (from BodyLook.HairArt.cs) as a canvas block, to rework from today's art.
    python3 tocanvas.py <key> [newkey] [name]"""
import re, sys, os
from canvas import W, H, BX, BY
ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Assets', 'Scripts', 'Art', 'BodyLook.HairArt.cs')
def view(key, table):
    s = open(ART).read()
    seg = s[s.index(f'static Dictionary<string, HairView> {table} =>'):]
    seg = seg[:seg.index('\n        };')]
    a = seg.index(f'["{key}"]'); b = seg.find('\n            ["', a + 5); body = seg[a:b if b > 0 else len(seg)]
    body = re.sub(r'//[^\n]*', '', body)
    arrays = [re.findall(r'"([^"]*)"', x) for x in re.findall(r'new\[\]\s*\{([^}]*)\}', body)]
    top = re.search(r'maneTop:\s*(-?\d+)', body); top = int(top.group(1)) if top else 32
    g = [['.'] * W for _ in range(H)]
    if len(arrays) > 1:
        mane = arrays[1]; ox = 14 - len(mane[0]) // 2; oy = BY + (32 - top) // 2
        for y, r in enumerate(mane):
            for x, c in enumerate(r):
                if c != '.' and 0 <= oy + y < H and 0 <= ox + x < W: g[oy + y][ox + x] = c
    from canvas import in_skull
    for y, r in enumerate(arrays[0]):
        for x, c in enumerate(r):
            if c != '.': g[BY + y][BX + x] = c
            elif in_skull(x, y): g[BY + y][BX + x] = '.'   # face / nape: the mane behind it is hidden
    return g
if __name__ == '__main__':
    key = sys.argv[1]; new = sys.argv[2] if len(sys.argv) > 2 else key + '_r'; name = sys.argv[3] if len(sys.argv) > 3 else key
    print(f'@{new} {name} | ')
    for v, t in (('side', 'SideArt'), ('back', 'BackArt')):
        g = view(key, t); ys = [y for y in range(H) if any(c != '.' for c in g[y])]
        print(f'{v} {ys[0]}')
        for y in range(ys[0], ys[-1] + 1): print(''.join(g[y]))
