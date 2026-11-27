# Developing ModernUI

## How it works

- **Loader:** [StarMap](https://github.com/StarMapLoader/StarMap), the community code-mod
  loader. KSA has no official modding API yet. StarMap calls methods marked
  `[StarMapAfterGui]` every frame, after the game's own UI.
- **Drawing:** the game's UI is Dear ImGui, through RocketWerkz's
  [BRUTAL](https://github.com/ahwootech/BRUTAL) bindings (`Brutal.ImGuiApi`). Gauges are
  drawn on ImGui's foreground draw list (`PathArcTo`, `AddText`), with no window frames.
- **Data:** `Telemetry.cs` reads the flight's game state; its speed calculations follow
  [KSA-ReGUI](https://github.com/wide-frog/KSA-ReGUI)'s `FlightData.cs`. The values the
  stock gauges show are read where the game computes them (`Program`'s gauge update), and
  every control goes through the stock button's path (input buffers, `Vehicle.SetEnum`).
- **Layout:** `Canvas.cs` works in design units for a 1080-pixel-high screen and scales
  with the real height. Text is measured and laid out a character at a time: tabular
  digits (numbers don't jitter), tracking for small capitals, caps centred vertically.
- **Stock gauges:** a Harmony prefix on `GaugeCanvas.OnDrawUi` skips their drawing while
  the HUD shows. Their enabled flags and saved HUD layouts are untouched.
- **Fonts:** Barlow (SIL OFL 1.1, `ModernUI/Assets/Fonts`), added to the game's font
  manager after each font rebuild, since the game only scans its own `Content` folder.
- **Game updates:** ModernUI patches and calls the game's internals, which a new build can
  rename or remove. `Failsafe.cs` keeps that from breaking the game: each Harmony patch
  class is applied on its own (a missing target loses only that patch); an error in the
  per-frame HUD that a game update causes (a missing method, field or type), even a
  failure to compile `Mod.Frame` against the new game, switches the HUD off for the
  session and brings the stock HUD back; any other error skips the frame (60 in a row
  switch it off); a patch whose own work throws stops doing it. The patches and the
  failsafe read `HudState`, which holds no game types, so they work even if `Hud` can't load. All of it goes to `logs/console.log` (`ModernUI:` lines), and the Mods
  menu says so. Code that touches the game from a patch goes in a separate
  `[MethodImpl(NoInlining)]` method called inside the `try`, so a missing member is caught.
- **Settings:** `ModernUI/settings.json` in the profile folder (the Borea instance's,
  or My Games/Kitten Space Agency), not the mod folder, which updates replace.
- **Build:** [KSAModTools](https://github.com/AMPW-german/KSAModTools) (the `tooling/`
  submodule) resolves the game's DLLs, writes `mod.toml` and copies the build into the
  game's mods folder. `Directory.Build.props` points it at the Mac app.

## Building

The project builds with the .NET 10 SDK against a local copy of the game and StarMap.
`tooling/` is a submodule ([KSAModTools](https://github.com/AMPW-german/KSAModTools)):
clone with `git clone --recursive`, or run `git submodule update --init`.

On Windows, KSAModTools finds the game in `C:/Program Files/Kitten Space Agency` and the
mods folder in `Documents/My Games/Kitten Space Agency/mods`; override them with the
`KSA_DIR` and `KSA_MODS_DIR` environment variables. `dotnet build -c Release` builds
`Release/ModernUI/` and copies it into the mods folder; `scripts/release.sh` makes the
release zip.

## Setup on a Mac (the Mac port and Borea)

1. Game: download the **Linux** build from https://ksa.ahwoo.com into `~/Downloads`, then
   run `./port.sh` in `~/Downloads/ksa-macos-port`. That makes
   `~/Applications/Kitten Space Agency.app`.
2. Mod loader: install [Borea](https://github.com/KSAModding/Borea), create an instance,
   and install StarMap in it.
3. Paths: copy `Directory.Build.local.props.example` to `Directory.Build.local.props`
   and fill in the instance ID (a folder name under
   `~/Library/Application Support/Borea/Instances/`) and the StarMap version (under
   `.../Borea/Loaders/StarMap/`). The file is gitignored.
4. Build: `dotnet build -c Release`. The build copies the mod into the instance's `mods`
   folder.
5. The first time only, enable it: `/Applications/Borea.app/Contents/MacOS/borea enable ModernUI`.
6. Run it: `/Applications/Borea.app/Contents/MacOS/borea launch <instance name>`, or
   press Play in Borea. Starting the app directly skips StarMap.

Each start's log is in the instance's `logs/console.log`. Look for
`Loaded mod: ModernUI`. Quit the game before rebuilding, because the game keeps
the DLL loaded.

## Dev loop (DevBridge)

`DevBridge/` is a second mod, for development only. It runs an HTTP server on
`127.0.0.1:7878` inside the game, so scripts can drive it. Enable it only in a test
instance. `scripts/dev.sh` wraps it:

```sh
scripts/dev.sh restart                    # quit, build both mods, start the test instance (~5 s warm, ~1 min cold)
scripts/dev.sh flight 12000 800           # the start rocket to 12 km, climbing at 800 m/s
scripts/dev.sh flight 300000              # ...or into a 300 km orbit
KSA_VEHICLE=Hunter scripts/dev.sh flight  # new game, the Hunter on the pad (KSA_SITE, default "CCSFS LC-39A")
scripts/dev.sh shot hud                   # PNG of the next frame, UI included -> screenshots/hud-<time>.png
scripts/dev.sh gallery v3                 # the same four states every time: orbit, 45 km, 12 km, pad
scripts/dev.sh call status                # flight state, altitude, speed
scripts/smoke.sh                          # a real launch through the HUD's buttons: stage, ignite, climb, shutdown
scripts/burst.sh tumble 32                # 32 quick shots of the navball, tiled into one contact sheet
scripts/dev.sh call vehicles              # vehicles to launch (yours and the defaults)
scripts/dev.sh call click --data-urlencode button=STAGE   # click a HUD button through ImGui
scripts/dev.sh call drag --data-urlencode "from=STAGE 2 ITEM 0" --data-urlencode "to=STAGE 3 ROW"
                                          # drag one HUD control onto another (at=top|middle|bottom of it)
scripts/dev.sh call hover --data-urlencode "button=STAGE 1 ITEM 1"   # hold the mouse over a control
scripts/dev.sh call burn --data-urlencode dv=100          # a test burn at the next apoapsis
scripts/dev.sh call camera --data-urlencode mode=Map      # map view (Orbit: back to flight)
scripts/dev.sh call target --data-urlencode id=Gemini7    # set a target (empty id clears it)
scripts/dev.sh call key --data-urlencode name=P           # press a key through ImGui (P: next mode)
scripts/dev.sh call fault --data-urlencode at=button   # make ModernUI throw once mid-frame (draw, button, glitch; reset: back on)
scripts/dev.sh console simspeed 10        # any game console command, with its log output
scripts/dev.sh call set --data-urlencode enum=FlightComputerAttitudeTrackTarget --data-urlencode value=Prograde
                                          # flight computer settings, as the stock buttons set them
```

- **Screenshots** use the Mac port's driver (`ksaMtlScreenshot`): the frame as presented,
  HUD included. Each one gets a new file name. Takes about 0.2 s.
- **World commands** (`newgame`, `launch`, `fly`) are adapted from the port's benchmark
  (MIT). They run on the game's thread, after `Program.OnFrame`.
- **Console** runs the game's own console commands (`[TerminalAction]`s: `simspeed`,
  `refill`, `control`, `loadvehicle`, `exit`, ...) and returns what they logged.
- **The test instance's settings**, which `restart` sets: a half-size, always-on-top window
  at the bottom right of the main screen; no CONFIGURATION screen, changelog or update
  check at start, so the game goes straight to its start situation (a Testing game, a
  rocket in orbit). Your own game's settings don't change.
- **Why the window:** macOS gives a window nobody can see almost no frames, so a
  full-screen game in another Space stalls, and so does a game behind a **locked screen
  or a sleeping display**. `restart` keeps the display awake while the game runs.

**Demo video:** `scripts/demo.sh` flies a mission through the HUD (pad, liftoff, ascent in
CLEAN mode, upper atmosphere, orbit with the autopilot, a burn planned in map view, a
target, an EVA), records each phase from the screen (ffmpeg, avfoundation) with title
cards between them, and writes `demo/ModernUI-demo.mp4` (1080p30). It needs Screen
Recording permission and the screen unlocked; keep the mouse off the game window.

**Staging video:** `scripts/staging-demo.sh` records the staging list the same way: the
chips and hover on the pad, fuel draining after liftoff, an action dragged to another
stage and a stage reordered in flight. It writes `demo/ModernUI-staging.mp4`.

**Navball test:** set `"DebugNavballTumble": true` in the profile's
`ModernUI/settings.json` and restart: the navball turns through every orientation
over time, so `scripts/burst.sh` covers hundreds of attitudes in a few seconds.

## References

- KSA Modding Wiki: https://modding.kittenspaceagency.wiki/
- ImGui Editor (design windows in a browser, get StarMap C#):
  https://safeshows.github.io/imgui-editor/
