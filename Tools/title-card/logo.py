# Rebuild the Equivalence mark at any scale from measurements of Assets/Resources/Title/logo.png
# (492x461): an ellipse ring with an up arrow on the left and a down arrow on the right, the word
# over its upside-down reflection, two bars between. Source Code Pro (weight 700) is the closest
# match found for the lettering.
import sys
from PIL import Image, ImageDraw, ImageFont
import numpy as np
COL = (236, 232, 222, 255)
import os
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fonts", "SourceCodePro[wght].ttf")

def mark(scale=4, ss=4, ring=True, word=True):
    k = scale * ss
    W, H = 492 * k, 461 * k
    im = Image.new("L", (W, H), 0); d = ImageDraw.Draw(im)
    if ring:
        # outer bounds x 8..483, y 0..459, stroke 6
        d.ellipse([8 * k, 0 * k, 484 * k, 460 * k], outline=255, width=6 * k)
        # arrows: left points up (apex 19.5,211; base y 247, x 1..30); right points down (top y 213, x 461..491; apex 473,248)
        d.polygon([(19.5 * k, 211 * k), (30.5 * k, 247.5 * k), (0.5 * k, 247.5 * k)], fill=255)
        d.polygon([(460.5 * k, 212.5 * k), (491.5 * k, 212.5 * k), (473.5 * k, 249 * k)], fill=255)
    if word:
        # the word: caps 188..209, Q tail to 217, x 120..371; bars x 118..373 at y 220..224 and 235..239
        ft = ImageFont.truetype(FONT, 100); ft.set_variation_by_axes([700])
        probe = Image.new("L", (300, 300)); ImageDraw.Draw(probe).text((20, 20), "E", font=ft, fill=255)
        bb = probe.getbbox(); size = 100 * (22 * k) / (bb[3] - bb[1])
        ft = ImageFont.truetype(FONT, int(round(size))); ft.set_variation_by_axes([700])
        def lay(track):
            t = Image.new("L", (W, 120 * k)); td = ImageDraw.Draw(t); x = 0
            for c in "EQUIVALENCE":
                td.text((x, 20 * k), c, font=ft, fill=255); x += ft.getlength(c) + track
            return t
        t0 = lay(0); b0 = t0.getbbox()
        track = ((371 - 120 + 1) * k - (b0[2] - b0[0])) / 10
        t = lay(track); bb = t.getbbox()
        # cap top: the E's top
        ecap = Image.new("L", (W, 120 * k)); ImageDraw.Draw(ecap).text((0, 20 * k), "E", font=ft, fill=255)
        captop = ecap.getbbox()[1]
        word_img = t.crop((bb[0], captop, bb[2], bb[3]))
        im.paste(255, (0, 0), None) if False else None
        im.paste(word_img, (120 * k, 188 * k), word_img)
        flipped = word_img.transpose(Image.FLIP_TOP_BOTTOM)
        # reflect about y = 229.5 (between the bars)
        top_f = int(round(229.5 * 2 * k - (188 * k + word_img.size[1])))
        im.paste(flipped, (120 * k, top_f), flipped)
        d.rectangle([118 * k, 220 * k, 374 * k - 1, 225 * k - 1], fill=255)
        d.rectangle([118 * k, 235 * k, 374 * k - 1, 240 * k - 1], fill=255)
    a = im.resize((492 * scale, 461 * scale), Image.LANCZOS)
    out = Image.new("RGBA", a.size, COL[:3] + (0,)); out.putalpha(a)
    return out

if __name__ == "__main__":
    s = int(sys.argv[1]) if len(sys.argv) > 1 else 4
    mark(s).save(f"logo_{s}x.png")
    mark(s, word=False).save(f"logo_ring_{s}x.png")
    mark(s, ring=False).save(f"logo_word_{s}x.png")
    # check against the original at 1x
    m = np.array(mark(1, ss=8))[:, :, 3] > 128
    o = np.array(Image.open("/Users/marshoward/Coalescence/Assets/Resources/Title/logo.png").convert("RGBA"))[:, :, 3] > 128
    print("IoU vs original", round((m & o).sum() / (m | o).sum(), 3))
