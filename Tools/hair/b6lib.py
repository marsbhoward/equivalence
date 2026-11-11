"""Helpers for hand-building the eight-direction views (batch 6, 2026-11-10)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
from compare import views
V = views()
cells = lambda t: [r[::2] for r in t[::2]] if t else []

def T(k):  return cells(V[k]['side'][0]), cells(V[k]['side'][1]), V[k]['side'][2]
def B(k):  return cells(V[k]['back'][0]), cells(V[k]['back'][1]), V[k]['back'][2]

def pad(rows, n=18, w=16): return [(r + '.' * w)[:w] for r in rows] + ['.' * w] * (n - len(rows))

def cut(rows, top, maxcol):
    """Blank each row right of head column maxcol(headrow) (None = keep), closing a cut through hair with K."""
    if not rows: return rows
    w = len(rows[0]); off = (w - 16) // 2; out = []
    for r, row in enumerate(rows):
        h = r - (top // 2 - 16); m = maxcol(h)
        if m is not None:
            e = m + off + 1
            if 0 <= e < w and any(c != '.' for c in row[e:]):
                row = row[:e] + '.' * (w - e)
                if row[e - 1] in 'LD': row = row[:e - 1] + 'K' + row[e:]
        out.append(row)
    return out

def shift(rows, dx):
    return [(r[-dx:] + '.' * (-dx)) if dx < 0 else ('.' * dx + r[:len(r) - dx]) for r in rows]

FIX = {'D': 'K', 's': '.', 'd': '.'}
def fade_edges(rows):
    out = []
    for r in rows:
        r = list(r)
        if r and r[0] in FIX: r[0] = FIX[r[0]]
        if r and r[-1] in FIX: r[-1] = FIX[r[-1]]
        out.append(''.join(r))
    return out

def write(key, title, parts):
    """parts: list of (name, rows) or (name, rows, top)."""
    out = [f'# {title} - side and back 3/4, by hand (2026-11-10)', f'@{key}']
    for p in parts:
        name, rows = p[0], p[1]
        if name.endswith('.cap'):
            assert len(rows) == 18 and all(len(r) == 16 for r in rows), (key, name, [len(r) for r in rows])
            out += [name] + rows
        else:
            w = len(rows[0]); assert all(len(r) == w for r in rows), (key, name)
            out += [f'{name} {p[2]}'] + rows
    open(os.path.join(HERE, 'views5', f'{key}.txt'), 'w').write('\n'.join(out) + '\n')
