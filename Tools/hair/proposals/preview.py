import sys
from PIL import Image, ImageDraw
from canvas import parse, split, SKULL, W, H, BX, BY
PX = 6
SK, SKO, EYE, HL, HK, BG, BODY = (196,150,120), (90,60,50), (30,30,40), (120,82,52), (40,26,20), (76,80,90), (40,42,52)
def view_img(canvas, back):
    img = Image.new('RGB', (W*PX, 36*PX), BG); d = ImageDraw.Draw(img)
    def cell(x, y, col):
        if 0 <= y < 36: d.rectangle([x*PX, y*PX, x*PX+PX-1, y*PX+PX-1], fill=col)
    r = split(canvas); cap, mane, top = r
    # body
    for y in range(BY+18, 36):
        for x in range(BX+1, BX+15): cell(x, y, BODY)
    if mane and not back:
        oy = BY + (32-top)//2; ox = 14 - len(mane[0])//2
        for y, row in enumerate(mane):
            for x, c in enumerate(row):
                if c != '.': cell(ox+x, oy+y, HK if c == 'K' else HL)
    for cy, (a, b) in SKULL.items():
        for cx in range(a, b+1):
            edge = cx in (a, b) or cy in (2, 17)
            cell(BX+cx, BY+cy, SKO if edge else SK)
    if not back:
        for cx in (4, 5, 12):
            for cy in (10, 11): cell(BX+cx, BY+cy, EYE)
    if mane and back:
        oy = BY + (32-top)//2; ox = 14 - len(mane[0])//2
        for y, row in enumerate(mane):
            for x, c in enumerate(row):
                if c != '.': cell(ox+x, oy+y, HK if c == 'K' else HL)
    for cy, row in enumerate(cap):
        for cx, c in enumerate(row):
            if c in 'LKDH': cell(BX+cx, BY+cy, HK if c == 'K' else HL)
            elif c.islower(): cell(BX+cx, BY+cy, (150,105,85))
    return img
styles = parse(sys.argv[1]); keys = sys.argv[3].split(',') if len(sys.argv) > 3 else None
styles = [s for s in styles if not keys or s['key'] in keys]
cols = 4
w, h = W*PX*2 + 10, 36*PX + 16
out = Image.new('RGB', (w*cols, h*((len(styles)+cols-1)//cols)), (30,30,36)); d = ImageDraw.Draw(out)
for i, s in enumerate(styles):
    x0, y0 = (i % cols) * w, (i // cols) * h
    d.text((x0+4, y0+2), s['key'], fill=(230,230,230))
    if s['side']: out.paste(view_img(s['side'], False), (x0, y0+14))
    if s['back']: out.paste(view_img(s['back'], True), (x0+W*PX+6, y0+14))
out.save(sys.argv[2])
