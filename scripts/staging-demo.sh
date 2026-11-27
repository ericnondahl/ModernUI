#!/usr/bin/env bash
# Records the staging list in flight: what each stage does (chips, a hovered chip's parts lit
# on the rocket, fuel draining), a chip dragged to another stage, a stage dragged to a new
# place and back. One clip per step, title cards between them. Output:
# demo/ModernUI-staging.mp4 (1920x1080, 30 fps).
#
# Needs what scripts/demo.sh needs: Screen Recording permission, the screen unlocked, the
# mouse off the game window. The drags go through DevBridge (/drag), not the real mouse.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
D="$REPO/scripts/dev.sh"
OUT="$REPO/demo"
CLIPS="$OUT/staging-clips"
FONT="$REPO/ModernUI/Assets/Fonts/Barlow-SemiBold.ttf"
FONT_LIGHT="$REPO/ModernUI/Assets/Fonts/Barlow-Medium.ttf"
WINDOW="${KSA_WINDOW:-1600x900}"
rm -rf "$CLIPS"; mkdir -p "$CLIPS"
exec 3>"$OUT/staging-trace.log"; BASH_XTRACEFD=3; set -x

call() { "$D" call "$@" >/dev/null; }
click() { call click --data-urlencode "button=$1"; }
drag() { call drag --data-urlencode "from=$1" --data-urlencode "to=$2" --data-urlencode "at=${3:-middle}" --data-urlencode "steps=${4:-45}"; }
hover() { call hover --data-urlencode "button=$1" --data-urlencode "seconds=$2"; }

# FULL mode, so the staging list is open.
KSA_WINDOW="$WINDOW" "$D" restart >/dev/null
settings=$(ls -d "$HOME/Library/Application Support/Borea/Instances/"*/ModernUI/settings.json | head -1)
pid=$(python3 -I -c "import json,os;print(json.load(open(os.path.expanduser('~/Library/Application Support/Borea/running-games.json')))['games'][0]['processId'])")
# Where the window's content is, measured again before each clip: the window can be moved
# between clips (by hand, or by macOS between displays). Compiled once, so it's quick.
CROP_BIN="$(mktemp -d)/window-crop"
swiftc -O "$REPO/scripts/window-crop.swift" -o "$CROP_BIN"

REC_PID=""
rec() {
  crop=$("$CROP_BIN" "$pid" "$WINDOW")
  echo "capturing $1 at $crop"
  ffmpeg -hide_banner -loglevel error -y -f avfoundation -framerate 30 -capture_cursor 0 -pixel_format uyvy422 \
    -i "Capture screen 0:none" -t "$2" -vf "crop=$crop,scale=1920:1080:flags=lanczos,fps=30" \
    -c:v h264_videotoolbox -b:v 16M -pix_fmt yuv420p "$CLIPS/$1.mp4" 2>/dev/null &
  REC_PID=$!
  sleep 1
}
done_rec() { wait "$REC_PID"; }

KSA_VEHICLE=Rocket "$D" flight >/dev/null
for _ in 1 2 3; do
  mode=$(python3 -I -c "import json,sys; print(json.load(open(sys.argv[1])).get('Mode'))" "$settings")
  [[ $mode == Full ]] && break
  call key --data-urlencode name=P; sleep 0.3
done
sleep 7   # the game's start-up messages fade

# 1. On the pad: what each stage does; hover lights the parts.
rec 1-pad 11
sleep 1.5; hover "STAGE 1 ITEM 1" 2.5
sleep 0.4; hover "STAGE 3 ITEM 0" 2
sleep 0.4; hover "STAGE 5 ITEM 1" 2
done_rec

# 2. Liftoff: stage, ignite; the engines' fuel bars start to fall.
rec 2-liftoff 10
sleep 1; click "AP SURFACE UP"            # hold straight up, so the climb stays tidy
sleep 0.5; click STAGE
sleep 1.2; click IGNITE
sleep 2.5; hover "STAGE 1 ITEM 0" 3
done_rec

# 3. In flight: the stage 2 decoupler into stage 3 (decouple and light the upper stage at once).
rec 3-move 10
sleep 1.5; drag "STAGE 2 ITEM 0" "STAGE 3 ROW"
sleep 1.5; hover "STAGE 3 ITEM 1" 2.5
done_rec

# 4. In flight: the parachutes' stage (5) dropped above stage 3, so it becomes stage 4; then
# dragged back to the top. (A drop lands above or below the row by which half it is in.)
rec 4-reorder 13
sleep 1.5; drag "STAGE 5 ROW" "STAGE 3 ROW" top
sleep 2; drag "STAGE 4 ROW" "STAGE 5 ROW" top
sleep 2.5
done_rec

card() { # card <name> <seconds> <title> <subtitle>
  ffmpeg -hide_banner -loglevel error -y -f lavfi -i "color=c=black:s=1920x1080:r=30:d=$2" -vf \
    "drawtext=fontfile=$FONT:text='$3':fontcolor=white:fontsize=84:x=(w-text_w)/2:y=(h-text_h)/2-30,\
drawtext=fontfile=$FONT_LIGHT:text='$4':fontcolor=white@0.65:fontsize=34:x=(w-text_w)/2:y=(h/2)+50,\
fade=t=in:st=0:d=0.3,fade=t=out:st=$(echo "$2 - 0.3" | bc):d=0.3" \
    -c:v h264_videotoolbox -b:v 8M -pix_fmt yuv420p "$CLIPS/$1.mp4"
}
card 0-intro 2.5 "STAGING" "What every stage does, and drag to change it"
card 1-card 1.8 "EVERY STAGE AT A GLANCE" "Engines, decouplers, parachutes. Hover to light the parts"
card 2-card 1.8 "LIFTOFF" "Fuel per engine, as it burns"
card 3-card 1.8 "DRAG AN ACTION" "The stage 2 decoupler into stage 3, mid-flight"
card 4-card 1.8 "DRAG A STAGE" "Reorder by its name. Fired stages stay put"

list="$CLIPS/list.txt"; : > "$list"
for n in 0-intro 1-card 1-pad 2-card 2-liftoff 3-card 3-move 4-card 4-reorder; do
  echo "file '$CLIPS/$n.mp4'" >> "$list"
done
ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i "$list" -c:v h264_videotoolbox -b:v 14M -pix_fmt yuv420p -r 30 "$OUT/ModernUI-staging.mp4"
echo "$OUT/ModernUI-staging.mp4"
