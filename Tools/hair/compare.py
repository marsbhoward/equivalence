"""
Before-and-after sheet: every hairstyle's old face-on design (from legacy/BodyLook_faceon.cs) next
to its turned and back views (from Assets/Scripts/Art/BodyLook.HairArt.cs). Flat colours, no hair
pass - this compares SHAPES.

    python3 Tools/hair/compare.py [out.png] [keys]
"""
import os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from render import load_head, compose, expand, render
from PIL import Image, ImageDraw, ImageFont

ART = os.path.join(HERE, '..', '..', 'Assets', 'Scripts', 'Art', 'BodyLook.HairArt.cs')


def double(rows):
    """Cells -> texels, the doubling HairView does when it loads."""
    return [''.join(c * 2 for c in r) for r in rows for _ in (0, 1)] if rows else rows


def views():
    """key -> {'side'|'back': (cap texel rows, mane texel rows or None, maneTop)} from the C#."""
    text = open(ART).read()
    out = {}
    for view, table in (('side', 'SideArt'), ('back', 'BackArt')):
        seg = text[text.index(f'static Dictionary<string, HairView> {table} =>'):]
        seg = seg[:seg.index('\n        };')] + '\n'
        for m in re.finditer(r'\["(\w+)"\] = new HairView\((.*?)\)\s*,\s*\n', seg, re.S):
            key, body = m.group(1), m.group(2)
            arrays = [re.findall(r'"([^"]*)"', a) for a in re.findall(r'new\[\]\s*\{([^}]*)\}', body)]
            top = re.search(r'maneTop:\s*(-?\d+)', body)
            out.setdefault(key, {})[view] = (double(arrays[0]), double(arrays[1]) if len(arrays) > 1 else None,
                                             int(top.group(1)) if top else 32)
    return out


def legacy():
    """key -> (cap 36 rows, mane or None, maneTop): the face-on originals at their own density
    (the grid, not Shock's and High Tail's later finer menu versions)."""
    text = open(os.path.join(HERE, 'legacy', 'BodyLook_faceon.cs')).read()
    start = text.index('public static readonly Style[] HairStyles')
    end = text.index('};\n', text.index('new("shaved"', start))
    seg = re.sub(r'//[^\n]*', '', text[start:end])   # comments quote strings too
    parts = re.split(r'\n\s*new\("(\w+)", "[^"]+",', seg)[1:]
    out = {}
    for key, body in zip(parts[0::2], parts[1::2]):
        def arr(src): return re.findall(r'"([^"]*)"', src)
        blocks = re.findall(r'(menuGrid:|menuMane:)?\s*new\[\]\s*\{([^}]*)\}', body)
        plain = [arr(b) for tag, b in blocks if not tag]
        named = {tag[:-1]: arr(b) for tag, b in blocks if tag}
        m = re.search(r'maneTop:\s*(-?\d+)', body)
        top = int(m.group(1)) if m else 32
        cap = expand(plain[0])
        mane = plain[1] if len(plain) > 1 else None
        out[key] = (cap, mane, top)
    return out


def sheet(path, keys, px=6, pad=18, per_row=2):
    old = legacy()
    front = load_head('head_front.txt')
    side = load_head('head_side.txt')
    back = [''.join('b' if c in '123456xw' else c for c in r) for r in side]
    try:
        font = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 26)
        big = ImageFont.truetype('/System/Library/Fonts/Helvetica.ttc', 34)
    except Exception:
        font = big = ImageFont.load_default()

    rows = []
    new = views()
    for key in keys:
        cap, mane, top = old[key]
        v = new[key]
        s_cap, s_mane, s_top = v['side']
        b_cap, b_mane, b_top = v['back']
        rows.append((key, [
            render(compose(front, cap), mane, top, px=px, pad=pad),
            render(compose(side, s_cap), s_mane, s_top, px=px, pad=pad),
            render(compose(back, b_cap), b_mane, b_top, px=px, pad=pad, mane_over_body=True),
        ]))

    pw, ph = rows[0][1][0].size
    title, gap, head = 56, 40, 70
    block_w = pw * 3
    W = block_w * per_row + gap * (per_row - 1)
    n_rows = (len(rows) + per_row - 1) // per_row
    H = head + n_rows * (title + ph)
    out = Image.new('RGB', (W, H), (34, 35, 42))
    d = ImageDraw.Draw(out)
    for c in range(per_row):
        x0 = c * (block_w + gap)
        for i, lab in enumerate(('BEFORE  face-on', 'NOW  turned', 'NOW  behind')):
            d.text((x0 + i * pw + 16, 20), lab, fill=(150, 152, 165), font=font)
    for n, (key, imgs) in enumerate(rows):
        r, c = divmod(n, per_row)
        x0, y0 = c * (block_w + gap), head + r * (title + ph)
        d.text((x0 + 16, y0 + 10), hair_name(key), fill=(235, 235, 240), font=big)
        for i, im in enumerate(imgs):
            out.paste(im, (x0 + i * pw, y0 + title))
    out.save(path)
    return out.size


def hair_name(key):
    names = {'twintail': 'Twin Tails', 'hightail': 'High Tail', 'hightop': 'High Top', 'wolf': 'Wolf Cut'}
    return names.get(key, key.capitalize())


if __name__ == '__main__':
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'out', 'compare_all.png')
    keys = sys.argv[2].split(',') if len(sys.argv) > 2 else list(legacy())
    print(path, sheet(path, keys))
