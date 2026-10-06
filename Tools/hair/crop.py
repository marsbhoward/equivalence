"""Cut the heads and shoulders out of a HairSheet capture: one row per view, one column per style."""
import sys
from PIL import Image
src, dst, ncols = sys.argv[1], sys.argv[2], int(sys.argv[3])
nrows = int(sys.argv[4]) if len(sys.argv) > 4 else 3
im = Image.open(src)
cw, ch = im.width / ncols, im.height / nrows
y0, y1 = 0.09, 0.78            # fraction of a cell: above the tallest hair to below mid-back
x0, x1 = 0.12, 0.88
tiles = []
for r in range(nrows):
    for c in range(ncols):
        tiles.append(im.crop((int(c * cw + x0 * cw), int(r * ch + y0 * ch),
                              int(c * cw + x1 * cw), int(r * ch + y1 * ch))))
tw, th = tiles[0].size
out = Image.new('RGB', (tw * ncols, th * nrows), (40, 40, 48))
for i, t in enumerate(tiles): out.paste(t, ((i % ncols) * tw, (i // ncols) * th))
out.save(dst)
print(out.size)
