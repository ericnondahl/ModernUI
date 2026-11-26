#!/usr/bin/env bash
# Dev loop for TelemetryHud: rebuild, restart the game in the Borea test instance, set up a
# flight and take screenshots, through the DevBridge mod's HTTP server.
#
#   scripts/dev.sh restart            quit the game, build both mods, start the game, wait for the bridge
#   scripts/dev.sh flight [alt] [v]   teleport the controlled vehicle to alt (m), climbing at v (m/s),
#                                     or into orbit without v; KSA_VEHICLE=<save> starts it on a pad first
#   scripts/dev.sh shot [name]        screenshot into screenshots/<name>-<time>.png, prints the path
#   scripts/dev.sh gallery [name]     shots of fixed states: orbit, 45 km and 12 km climbing, on the pad
#   scripts/dev.sh call <cmd> [--data-urlencode k=v]  any bridge command: status, vehicles, console, ...
#   scripts/dev.sh console <line>     a game console command, e.g. "simspeed 10", "defaultvehiclesavefiles"
#   scripts/dev.sh quit               quit the game
#   scripts/dev.sh window             (restart does this) the test instance's game in a small
#                                     always-on-top window, so macOS keeps drawing it
#
# Env: KSA_INSTANCE (Borea instance name, default Test), KSA_VEHICLE (vehicle save for
# flight), KSA_SITE (launch site, default "CCSFS LC-39A"), KSA_DEVBRIDGE_PORT (default 7878).
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BOREA=/Applications/Borea.app/Contents/MacOS/borea
INSTANCE="${KSA_INSTANCE:-Test}"
SITE="${KSA_SITE:-CCSFS LC-39A}"
PORT="${KSA_DEVBRIDGE_PORT:-7878}"
URL="http://127.0.0.1:$PORT"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" PATH="$HOME/.dotnet:$PATH"

die() { echo "dev.sh: $*" >&2; exit 1; }

# Calls one bridge command; prints its JSON, fails on an HTTP error.
call() {
  local out code
  out=$(curl -sS -G -w '\n%{http_code}' "$URL/${1#/}" "${@:2}") || die "no bridge at $URL (is the game running? scripts/dev.sh restart)"
  code=${out##*$'\n'}
  out=${out%$'\n'*}
  echo "$out"
  [[ $code == 200 ]] || exit 1
}

# The PID of the game Borea started (Borea records it), if it still runs.
game_pid() {
  python3 -I -c '
import json, os, sys
p = os.path.expanduser("~/Library/Application Support/Borea/running-games.json")
try:
    games = json.load(open(p)).get("games", [])
except Exception:
    games = []
for g in games:
    try:
        os.kill(g["processId"], 0)
        print(g["processId"])
    except Exception:
        pass
'
}

quit_game() {
  local pid
  for pid in $(game_pid); do
    # Only ever the KSA process Borea recorded, checked by its command line.
    ps -o command= -p "$pid" | grep -q "Kitten Space Agency.app/Contents/MacOS/KSA" || continue
    echo "quitting the game (pid $pid)"
    kill "$pid"
    for _ in $(seq 1 100); do kill -0 "$pid" 2>/dev/null || break; sleep 0.1; done
    if kill -0 "$pid" 2>/dev/null; then kill -9 "$pid"; fi
  done
}

wait_for_bridge() {
  echo -n "waiting for the game"
  for _ in $(seq 1 180); do
    if curl -sf "$URL/status" >/dev/null 2>&1; then echo " ready"; return; fi
    echo -n .; sleep 1
  done
  echo; die "the bridge did not answer within 3 minutes. A locked screen or a sleeping display stalls the game (it gets no frames); otherwise see the instance's logs/console.log"
}

# macOS gives a game window that no one can see almost no frames, so a full-screen game in
# another Space stalls (loading too). The test instance plays in a window on top instead:
# half the display's size, its shape kept, bottom right. Only that instance's settings.
dev_window() {
  local id settings screen
  id=$("$BOREA" instance show "$INSTANCE" --json | python3 -I -c 'import json,sys; print(json.load(sys.stdin)["id"])')
  settings="$HOME/Library/Application Support/Borea/Instances/$id/settings.toml"
  [[ -f $settings ]] || die "no settings.toml for instance '$INSTANCE' yet; start it once from Borea"
  # The main screen's size in points (AppKit; needs no permission).
  screen=$(osascript -l JavaScript -e 'ObjC.import("AppKit"); const s = $.NSScreen.mainScreen.frame.size; s.width + " " + s.height' 2>/dev/null || true)
  python3 -I - "$settings" ${screen:-3440 1440} <<'PY'
import re, sys
path, sw, sh = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
text = open(path).read()
head, sep, rest = text.partition("[display]\n")
body, nxt, tail = rest.partition("\n[")
w, h = sw // 2, sh // 2  # half the display, its shape kept, so the HUD lays out as in play
values = {"mode": '"Windowed"', "alwaysOnTop": "true", "width": w, "height": h,
          "posX": sw - w - 20, "posY": sh - h - 60}  # bottom right
for key, value in values.items():
    body, n = re.subn(rf"^{key} = .*$", f"{key} = {value}", body, flags=re.M)
    if n == 0:
        body += f"\n{key} = {value}"
open(path, "w").write(head + sep + body + nxt + tail)
print(f"test instance: {w}x{h} window on top, bottom right of the {sw}x{sh} display")
PY
  # Straight into the game: no CONFIGURATION screen (its "Always Show" box), no changelog,
  # no update check. The game starts with its own start settings (a rocket in orbit).
  sed -i '' -E 's/^(selectSystemOnStart|displayChangelogOnChange|checkForUpdates) = true/\1 = false/' "$settings"
}

cmd="${1:-help}"; shift || true
case "$cmd" in
  restart)
    quit_game
    dev_window
    # A failed build stops here: starting the game would test the previous build.
    if ! out=$(cd "$REPO" && dotnet build TelemetryHud.slnx -c Release -v quiet -nologo 2>&1); then
      echo "$out" | grep -E "error" | sed 's| \[.*||' | sort -u >&2
      die "build failed; the game was not started"
    fi
    echo "$out" | grep -E "warning CS" | sed 's| \[.*||' | sort -u || true
    "$BOREA" enable TelemetryHud >/dev/null 2>&1 || true
    "$BOREA" enable DevBridge >/dev/null 2>&1 || true
    # Borea can take a moment to notice the old game is gone; retry the launch.
    for try in 1 2 3 4 5; do
      "$BOREA" launch "$INSTANCE" >/tmp/ksa-dev-launch.log 2>&1 && break
      [[ $try == 5 ]] && { cat /tmp/ksa-dev-launch.log >&2; die "borea could not start '$INSTANCE'"; }
      sleep 2
    done
    # The game gets no frames while the display sleeps; keep it awake while the game runs.
    for pid in $(game_pid); do caffeinate -d -w "$pid" >/dev/null 2>&1 & done
    wait_for_bridge
    call wait --data-urlencode frames=120 >/dev/null
    ;;
  flight)
    # With KSA_VEHICLE (a vehicle save): a new game and that vehicle on KSA_SITE's pad.
    # Without: the vehicle the game starts with (a rocket in orbit, in a Testing game).
    if [[ -n ${KSA_VEHICLE:-} ]]; then
      call newgame >/dev/null
      call wait --data-urlencode frames=30 >/dev/null
      call launch --data-urlencode "vehicle=$KSA_VEHICLE" --data-urlencode "site=$SITE"
    fi
    if [[ $# -ge 1 ]]; then
      args=(--data-urlencode "alt=$1")
      [[ $# -ge 2 ]] && args+=(--data-urlencode "speed=$2")
      call fly "${args[@]}"
    fi
    call wait --data-urlencode frames=90 >/dev/null
    call status
    ;;
  shot)
    mkdir -p "$REPO/screenshots"
    path="$REPO/screenshots/${1:-shot}-$(date +%Y%m%d-%H%M%S).png"
    call screenshot --data-urlencode "path=$path" >/dev/null
    echo "$path"
    ;;
  gallery)
    # The same flight states every time, to compare before and after a change:
    # screenshots/<name>-<state>-<time>.png. Run after restart (it starts in orbit).
    name="${1:-gallery}"
    "$0" flight 300000 >/dev/null && "$0" shot "$name-orbit"
    "$0" flight 45000 1500 >/dev/null && "$0" shot "$name-45km"
    "$0" flight 12000 800 >/dev/null && "$0" shot "$name-12km"
    KSA_VEHICLE=Hunter "$0" flight >/dev/null && "$0" shot "$name-pad"
    ;;
  call) call "$@" ;;
  console) call console --data-urlencode "cmd=$*" ;;
  quit) quit_game ;;
  window) quit_game; dev_window ;;
  *) sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//' ;;
esac
