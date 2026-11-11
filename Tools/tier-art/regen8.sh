#!/bin/sh
# Rebake the eight-direction pictures (phase 3) from the five-view mock: Assets/Scripts/Art/Gear/Body8Art.Bronze.cs.
# Run from this folder: sh regen8.sh
set -e
{ cat body.js kit.js sets.js; sed '/^\/\/ -* sheet/,$d' views.js; cat bake8.js; } | node - ../../Assets/Scripts/Art/Gear/Body8Art.Bronze.cs
