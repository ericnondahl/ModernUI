#!/usr/bin/env bash
# A burst of screenshots of the navball, tiled into one contact sheet:
#   scripts/burst.sh <name> [count]  ->  screenshots/<name>-sheet.png
# Crops the navball from each frame (centre bottom of the test window).
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
name="$1"; count="${2:-24}"
dir="$REPO/screenshots/burst-$name-$(date +%H%M%S)"; mkdir -p "$dir"
for i in $(seq -w 1 "$count"); do
  "$REPO/scripts/dev.sh" call screenshot --data-urlencode "path=$dir/$i.png" >/dev/null
done
# The navball sits at the bottom centre: crop a square around it from each frame.
for f in "$dir"/[0-9]*.png; do
  ffmpeg -loglevel error -y -i "$f" -vf "crop=ih*0.30:ih*0.30:(iw-ih*0.30)/2:ih*0.70,scale=200:200" "${f%.png}-nav.png"
done
cols=8
ffmpeg -loglevel error -y -pattern_type glob -i "$dir/*-nav.png" -vf "tile=${cols}x$(( (count + cols - 1) / cols ))" "$REPO/screenshots/$name-sheet.png"
echo "$REPO/screenshots/$name-sheet.png"
