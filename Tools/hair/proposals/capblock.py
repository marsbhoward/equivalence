"""Today's CAP alone (no mane) as canvas rows from row 8 - a base to draw a new mane onto."""
import sys
from tocanvas import view
from canvas import BX, BY
g = view(sys.argv[1], 'SideArt' if sys.argv[2] == 'side' else 'BackArt')
for y in range(BY, BY + 18):
    print(''.join(g[y][x] if BX <= x < BX + 16 else '.' for x in range(28)))
