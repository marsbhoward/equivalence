#!/bin/sh
# Rebuild the store images into out/. Needs macOS (Quick Look renders the SVGs) and a Python with
# Pillow + numpy (/usr/bin/python3 has both).
set -e
cd "$(dirname "$0")"
work=$(mktemp -d)
cp gen.py pix2.py logo.py export.py "$work"/ && cp -R fonts "$work"/ && mkdir -p "$work/out"
( cd "$work" && /usr/bin/python3 gen.py && /usr/bin/python3 pix2.py && /usr/bin/python3 export.py )
for f in epic_offer_tall_1200x1600 epic_offer_wide_2560x1440 itch_cover_630x500 epic_product_logo_960x540 equivalence_mark_1968x1844; do
  cp "$work/out/$f.png" out/
done
rm -rf "$work"
