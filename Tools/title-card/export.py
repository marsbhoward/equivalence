# The store images: each card's pixel scene (pix2.py) with its glow baked in on the pixel grid,
# the Equivalence mark (logo.py, drawn sharp at size) laid over it, and Epic's product logo.
# Sizes are Epic's Storefront Media Guide (2026-11-06): offer images 2560x1440 and 1200x1600
# (.jpg/.png), product logo 960x540 .png under 1 MB; itch.io's recommended cover 630x500.
import numpy as np
from PIL import Image, ImageFilter
from logo import mark
BLOCK = 4

def glow(img, cx, cy, rad, block, op=0.5):
    """The Stone's light, screen-blended on the pixel grid so it stays pixel art."""
    g = np.array(img.resize((img.width // block, img.height // block), Image.NEAREST)).astype(float) / 255
    h, w = g.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.hypot(xx - cx / block, yy - cy / block) / (rad / block)
    stops = [(0.0, (255, 70, 40), 0.9), (0.30, (255, 40, 24), 0.35), (0.65, (255, 30, 20), 0.0)]
    a = np.interp(d, [s[0] for s in stops], [s[2] for s in stops]) * op
    c = np.stack([np.interp(d, [s[0] for s in stops], [s[1][i] for s in stops]) for i in range(3)], 2) / 255
    a = np.round(a * 8) / 8                                   # in steps, like the rest of the light
    out = 1 - (1 - g) * (1 - c * a[:, :, None])
    return Image.fromarray((out * 255).astype(np.uint8)).resize(img.size, Image.NEAREST)

def place_mark(img, x, y, w, shadow=10):
    m = mark(scale=4)
    m = m.resize((w, round(w * m.height / m.width)), Image.LANCZOS)
    sh = Image.new("RGBA", m.size, (0, 0, 0, 0)); sh.putalpha(m.getchannel("A").point(lambda v: int(v * 0.9)))
    pad = shadow * 3
    shl = Image.new("RGBA", (m.width + 2 * pad, m.height + 2 * pad), (0, 0, 0, 0)); shl.alpha_composite(sh, (pad, pad))
    shl = shl.filter(ImageFilter.GaussianBlur(shadow))
    base = img.convert("RGBA")
    base.alpha_composite(shl, (x - pad, y - pad + 2)); base.alpha_composite(shl, (x - pad, y - pad + 2))
    base.alpha_composite(m, (x, y))
    return base.convert("RGB")

def cards():
    tall = glow(Image.open("c_px2.png").convert("RGB"), 600, 330, 550, BLOCK)
    tall.save("out/bg_tall.png")
    place_mark(tall, 430, 1262, 340).save("out/epic_offer_tall_1200x1600.png")
    # wide: the mark centred over the tower, as Epic's offer images centre the product logo
    wide = glow(Image.open("cw_px2.png").convert("RGB"), 1280, 700, 560, BLOCK)
    wide.save("out/bg_wide.png")
    place_mark(wide, 1280 - 320, 30, 640, shadow=12).save("out/epic_offer_wide_2560x1440.png")
    itch = Image.open("ci_px2.png").convert("RGB").resize((630, 500), Image.NEAREST)
    itch = glow(itch, 240, 130, 275, BLOCK // 2)
    itch.save("out/bg_itch.png")
    place_mark(itch, 376, 222, 240, shadow=5).save("out/itch_cover_630x500.png")
    # Epic's product logo: the mark alone on transparency, centred
    m = mark(scale=4); h = 500; m = m.resize((round(h * m.width / m.height), h), Image.LANCZOS)
    pl = Image.new("RGBA", (960, 540), (0, 0, 0, 0)); pl.alpha_composite(m, ((960 - m.width) // 2, 20))
    pl.save("out/epic_product_logo_960x540.png", optimize=True)
    mark(scale=4).save("out/equivalence_mark_1968x1844.png")

if __name__ == "__main__":
    cards()
    import os
    for f in sorted(os.listdir("out")): print(f, Image.open("out/" + f).size, os.path.getsize("out/" + f) // 1024, "KB")
