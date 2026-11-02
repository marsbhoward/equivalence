#!/bin/sh
# Rebake the tier looks into the game (Assets/Scripts/Art/Gear/DemoGear.TierBronze.cs).
# The sources are the approved mocks: sets.js (armour) and swords/discs/bows.js (weapons).
# Run from this folder: sh regen.sh
set -e
OUT=../../Assets/Scripts/Art/Gear/DemoGear.TierBronze.cs
cat body.js kit.js sets.js bake.js | node - /tmp/tier-armour.cs.txt
cat body.js kit.js sets.js wkit.js swords.js discs.js bows.js wbake.js | node - /tmp/tier-weapons.cs.txt
{ cat header.cs.txt; cat /tmp/tier-armour.cs.txt /tmp/tier-weapons.cs.txt | sed '$d'; printf '        }\n    }\n}\n'; } > "$OUT"
