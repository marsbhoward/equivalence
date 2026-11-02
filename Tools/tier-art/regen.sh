#!/bin/sh
# Rebake the tier looks into the game: Assets/Scripts/Art/Gear/DemoGear.Tier<Tier>.cs, one per tier.
# The sources are the approved mocks: sets.js (armour) and swords/discs/bows.js (weapons).
# Run from this folder: sh regen.sh
set -e
bake() {   # tier, set ids
  OUT=../../Assets/Scripts/Art/Gear/DemoGear.Tier$1.cs
  cat body.js kit.js sets.js bake.js | node - /tmp/tier-armour.cs.txt "$2"
  cat body.js kit.js sets.js wkit.js swords.js discs.js bows.js wbake.js | node - /tmp/tier-weapons.cs.txt "$2"
  { sed "s/@TIER@/$1/g; s/@SETS@/$3/g" header.cs.txt
    cat /tmp/tier-armour.cs.txt /tmp/tier-weapons.cs.txt | sed '$d'
    printf '        }\n    }\n}\n'; } > "$OUT"
}
bake Bronze hoplite,wayfarer,athanor "Hoplite, Wayfarer and Athanor"
bake Silver hauberk,kettle,sentinel "Hauberk, Kettle and Sentinel"
bake Gold sol,cataphract,wingward "Sol, Cataphract and Wingward"
