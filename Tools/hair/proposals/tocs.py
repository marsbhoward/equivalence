"""Write canvas styles into BodyLook.HairArt.cs - replacing a key's entry or adding it.
    python3 tocs.py <canvas file> <srckey>=<gamekey> [...] [--fill] [--tall]"""
import os, re, sys
import canvas
ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Assets', 'Scripts', 'Art', 'BodyLook.HairArt.cs')
I = ' ' * 12
def entry(key, cap, mane, top):
    rows = lambda g: ''.join(f'{I}        "{r}",\n' for r in g)
    s = f'{I}["{key}"] = new HairView(\n{I}    new[]\n{I}    {{\n{rows(cap)}{I}    }}'
    if mane:
        s += f',\n{I}    new[]\n{I}    {{\n{rows(mane)}{I}    }},\n{I}    maneTop: {top})'
    else:
        s += ')'
    return s + ',\n'
def put(text, table, key, block):
    a = text.index(f'static Dictionary<string, HairView> {table} =>')
    end = text.index('\n        };', a)
    seg = text[a:end]
    m = re.search(r'\n' + re.escape(I) + r'\["' + re.escape(key) + r'"\] = new HairView\(', seg)
    if m:
        s0 = a + m.start() + 1
        nxt = re.search(r'\n' + re.escape(I) + r'(\["|//)', text[s0 + 10:end])
        s1 = s0 + 10 + nxt.start() + 1 if nxt else end + 1
        return text[:s0] + block + text[s1:]
    return text[:end + 1] + block + text[end + 1:]
if __name__ == '__main__':
    args = sys.argv[1:]
    if '--fill' in args: canvas.FILL = True; args.remove('--fill')
    if '--tall' in args: canvas.BY, canvas.H = 14, 50; args.remove('--tall')   # round4_tall.txt's canvas
    styles = {s['key']: s for s in canvas.parse(args[0])}
    text = open(ART).read()
    for pair in args[1:]:
        src, _, key = pair.partition('=')
        for view, table in (('side', 'SideArt'), ('back', 'BackArt')):
            text = put(text, table, key, entry(key, *canvas.split(styles[src][view])))
        print('wrote', key, 'from', src)
    open(ART, 'w').write(text)
