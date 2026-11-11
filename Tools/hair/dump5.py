import sys; sys.path.insert(0, '.')
from compare import views
V = views(); cells = lambda t: [r[::2] for r in t[::2]] if t else []
for key in sys.argv[1].split(','):
    for view in ('side', 'back'):
        cap, mane, top = V[key][view]
        c, m = cells(cap), cells(mane)
        print(f'== {key} {"TURNED" if view == "side" else "BACK"}  maneTop {top}  mane {len(m[0]) if m else 0} wide')
        for i in range(max(len(c), len(m))):
            print(f'{i:2} {c[i] if i < len(c) else " " * 16}   {m[i] if i < len(m) else ""}')
