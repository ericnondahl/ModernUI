#!/usr/bin/env bash
# Records a demo video of the HUD through a flight's phases: on the pad, liftoff, ascent
# (CLEAN mode), upper atmosphere, orbit (autopilot), planning a burn (map view), rendezvous,
# EVA. Each phase is a clip recorded from the screen while DevBridge flies it; title cards
# go between them. Output: demo/ModernUI-demo.mp4 (1920x1080, 30 fps).
#
# Needs: Screen Recording permission for the app running this; the game window unobstructed
# (it is put in a centred always-on-top window). Keep the mouse off the game window while it
# records: hover would light buttons.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
D="$REPO/scripts/dev.sh"
OUT="$REPO/demo"
CLIPS="$OUT/clips"
FONT="$REPO/ModernUI/Assets/Fonts/Barlow-SemiBold.ttf"
FONT_LIGHT="$REPO/ModernUI/Assets/Fonts/Barlow-Medium.ttf"
WINDOW="${KSA_WINDOW:-1600x900}"
rm -rf "$CLIPS"; mkdir -p "$CLIPS"
# A trace of every step, for when a run stops early.
exec 3>"$OUT/demo-trace.log"; BASH_XTRACEFD=3; set -x

call() { "$D" call "$@" >/dev/null; }

# ensure_closed <setting> <button>: clicks the panel's toggle until its saved state says
# closed (a single click right after a recording sometimes doesn't land).
SETTINGS=""
ensure_closed() {
  for _ in 1 2 3 4; do
    [[ -z $SETTINGS ]] && SETTINGS=$(ls -d "$HOME/Library/Application Support/Borea/Instances/"*/ModernUI/settings.json 2>/dev/null | head -1)
    if [[ -n $SETTINGS ]] && python3 -I -c "import json,sys; sys.exit(0 if not json.load(open(sys.argv[1])).get(sys.argv[2]) else 1)" "$SETTINGS" "$1"; then
      return 0
    fi
    click "$2" || true
    sleep 0.5
  done
}
click() { call click --data-urlencode "button=$1"; }
key() { call key --data-urlencode "name=$1"; }

# ---- the game, in a recording-sized window ------------------------------------------------
KSA_WINDOW="$WINDOW" "$D" restart >/dev/null
# Where the window's content is on screen (window-crop.swift).
pid=$(python3 -I -c "import json,os;print(json.load(open(os.path.expanduser('~/Library/Application Support/Borea/running-games.json')))['games'][0]['processId'])")
crop=$(swift "$REPO/scripts/window-crop.swift" "$pid" "$WINDOW")
echo "capturing $crop"

# rec <name> <seconds>: records the window in the background.
REC_PID=""
rec() {
  ffmpeg -hide_banner -loglevel error -y -f avfoundation -framerate 30 -capture_cursor 0 -pixel_format uyvy422 \
    -i "Capture screen 0:none" -t "$2" -vf "crop=$crop,scale=1920:1080:flags=lanczos,fps=30" \
    -c:v h264_videotoolbox -b:v 16M -pix_fmt yuv420p "$CLIPS/$1.mp4" 2>/dev/null &
  REC_PID=$!
  sleep 1   # the capture takes a moment to start
}
done_rec() { wait "$REC_PID"; }

# ---- the flight ------------------------------------------------------------------------------
KSA_VEHICLE=Rocket "$D" flight >/dev/null
sleep 8   # the game's start-up messages fade

rec 1-pad 10
sleep 1.5; click STAGING
sleep 2; click CREW
done_rec

rec 2-liftoff 15
sleep 1; click STAGE
sleep 1.5; click IGNITE
done_rec
ensure_closed StagingOpen STAGING
ensure_closed CrewOpen CREW

rec 3-ascent 10
sleep 1; key P; sleep 0.15; key P          # FLIGHT -> FULL -> CLEAN
done_rec
key P                                       # back to FLIGHT

call fly --data-urlencode alt=45000 --data-urlencode speed=1500
sleep 1
rec 4-upper 9
sleep 3; click "WARP+"; sleep 0.6; click "WARP+"
sleep 2; click WARP1
done_rec

# Engines off before orbit: they would keep burning through the warp to the burn.
click SHUTDOWN
call fly --data-urlencode alt=300000
sleep 2
rec 5-orbit 12
sleep 1; click AUTOPILOT
sleep 2; click "AP VELOCITY PROGRADE"
sleep 5; click AUTOPILOT
done_rec

rec 6-burn 17
sleep 0.5; call burn --data-urlencode dv=100
sleep 1.5; call camera --data-urlencode mode=Map
sleep 2
for b in "EDIT PROGRADE +" "EDIT PROGRADE +" "EDIT PROGRADE +" "EDIT NORMAL +"; do click "$b"; sleep 0.5; done
sleep 1.5; call camera --data-urlencode mode=Orbit
sleep 2; click "BURN WARP"
sleep 2.5; click "BURN WARP"
done_rec
click "BURN DELETE" || true

rec 7-rendezvous 10
sleep 1; call target --data-urlencode id=Luna
sleep 2.5; click "TARGET TOWARD"
done_rec
call target --data-urlencode id= || true
call set --data-urlencode enum=FlightComputerAttitudeMode --data-urlencode value=Manual || true

# EVA from the game's own start (a crewed rocket in orbit): the pad rocket's test kittens
# can't always get out.
KSA_WINDOW="$WINDOW" "$D" restart >/dev/null
sleep 6
rec 8-eva 12
sleep 1; click CREW
eva_button=$("$D" call buttons | python3 -I -c "import json,sys; b=[x for x in json.load(sys.stdin)['buttons'] if x.startswith('EVA ') and x[4:] not in ('DIRECT','VIEW','RCS','MANUAL','RATE')]; print(b[0] if b else '')")
sleep 1.5; [[ -n $eva_button ]] && click "$eva_button" || echo "no EVA button" >&2
sleep 3; click "EVA DIRECT" || echo "no EVA DIRECT button" >&2
done_rec

# ---- title cards and the cut ------------------------------------------------------------------
card() { # card <name> <seconds> <title> <subtitle>
  ffmpeg -hide_banner -loglevel error -y -f lavfi -i "color=c=black:s=1920x1080:r=30:d=$2" -vf \
    "drawtext=fontfile=$FONT:text='$3':fontcolor=white:fontsize=84:x=(w-text_w)/2:y=(h-text_h)/2-30,\
drawtext=fontfile=$FONT_LIGHT:text='$4':fontcolor=white@0.65:fontsize=34:x=(w-text_w)/2:y=(h/2)+50,\
fade=t=in:st=0:d=0.3,fade=t=out:st=$(echo "$2 - 0.3" | bc):d=0.3" \
    -c:v h264_videotoolbox -b:v 8M -pix_fmt yuv420p "$CLIPS/$1.mp4"
}
card 0-intro 3 "MODERNUI" "A flight HUD for Kitten Space Agency"
card 1-card 1.6 "ON THE PAD" "Staging, crew, systems"
card 2-card 1.6 "LIFTOFF" "Stage and ignite from the HUD"
card 3-card 1.6 "ASCENT" "Clean mode"
card 4-card 1.6 "UPPER ATMOSPHERE" "45 km, 1,500 m/s"
card 5-card 1.6 "ORBIT" "Autopilot"
card 6-card 1.6 "PLANNING A BURN" "Map view editor, burn card, warp"
card 7-card 1.6 "TARGET" "Distance, closest approach, pointing"
card 8-card 1.6 "EVA" "Kitten controls"
card 9-outro 3 "MODERNUI" "Built with StarMap on the Mac port"

list="$CLIPS/list.txt"; : > "$list"
for n in 0-intro 1-card 1-pad 2-card 2-liftoff 3-card 3-ascent 4-card 4-upper 5-card 5-orbit 6-card 6-burn 7-card 7-rendezvous 8-card 8-eva 9-outro; do
  echo "file '$CLIPS/$n.mp4'" >> "$list"
done
ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i "$list" -c:v h264_videotoolbox -b:v 14M -pix_fmt yuv420p -r 30 "$OUT/ModernUI-demo.mp4"
echo "$OUT/ModernUI-demo.mp4"
