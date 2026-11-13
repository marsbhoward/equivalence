#!/bin/sh
# Rebake the eight-direction EXTRAS (scarf collar, accessories, arm pieces) from the approved 3D mocks
# (views-extras.js): Assets/Scripts/Art/Gear/Body8Art.Extras.cs and Assets/Resources/Body8/ArmPieces8.bytes.
# Run from this folder: sh regen8x.sh   (regen8.sh still bakes the Bronze armour on its own)
set -e
mkdir -p ../../Assets/Resources/Body8
{ cat body.js kit.js sets.js; sed -n 72,189p bake.js; cat accessories.js arms.js; sed '/^\/\/ -* sheet/,$d' views.js; sed '/^\/\/ -* main/,$d' views-extras.js; cat bake8x.js; } \
  | node - ../../Assets/Scripts/Art/Gear/Body8Art.Extras.cs ../../Assets/Resources/Body8/ArmPieces8.bytes
