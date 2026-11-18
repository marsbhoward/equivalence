#!/bin/sh
# Bake the QUARTZ back pieces (quartz8.js) for eight directions: Assets/Scripts/Art/Gear/Body8Art.Quartz.cs.
# Run from this folder: sh regen8q.sh      (preview first: see quartz8.js's own sheet)
set -e
{ cat body.js kit.js sets.js; sed '/^\/\/ -* sheet/,$d' views.js; sed '/^\/\/ -* sheet/,$d' quartz8.js; echo 'const QUARTZ_BAKE = true;'; cat bake8.js; } | node - ../../Assets/Scripts/Art/Gear/Body8Art.Quartz.cs
