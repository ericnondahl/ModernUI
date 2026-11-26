#!/usr/bin/env bash
# A real launch flown through the HUD's own buttons, with screenshots: the Rocket on the pad
# (crewed, so it's controllable), STAGE, IGNITE, a climb, SHUTDOWN. Then warp controls.
# Run after scripts/dev.sh restart. Prints the state after each step.
set -euo pipefail
D="$(cd "$(dirname "$0")" && pwd)/dev.sh"
st() { "$D" call status | python3 -I -c "import json,sys; d=json.load(sys.stdin); v=d.get('vehicle') or {}; print(f\"  engines={v.get('enginesOn')} stage={v.get('nextStage')} alt={v.get('altitudeAsl',0):.0f} m v={v.get('surfaceSpeed',0):.1f} m/s warp={d['simSpeed']:g} rcs={v.get('rcs')} frame={v.get('navballFrame')}\")"; }
click() { "$D" call click --data-urlencode "button=$1" >/dev/null; }

KSA_VEHICLE=Rocket "$D" flight >/dev/null; echo "on the pad:"; st; "$D" shot smoke-pad
click STAGE; sleep 1; echo "STAGE:"; st
click IGNITE; sleep 4; echo "IGNITE, 4 s:"; st; "$D" shot smoke-liftoff
sleep 6; echo "10 s:"; st; "$D" shot smoke-climb
click SHUTDOWN; sleep 1; echo "SHUTDOWN:"; st
click "WARP+"; echo "WARP+:"; st
click WARP1; echo "WARP1:"; st
click "FRAME ORBT"; echo "FRAME ORBT:"; st
