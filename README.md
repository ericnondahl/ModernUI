# TelemetryHud

A Kitten Space Agency UI mod: flight telemetry as flat, black-and-white gauges in the
style of a launch webcast. Thin rings, big numbers, small caps labels. Nothing
skeuomorphic.

Status: early. A shaded band along the bottom of the screen in flight:

- **Rings** (left): speed, filling towards circular orbital speed at the current height;
  altitude, filling towards the top of the atmosphere, then against round heights above.
- **Navball** (centre): a line-drawn sphere in the HUD's style: far side hidden, ground
  half a flat dark fill bounded by the curved horizon, grid fading towards the edge, flat
  glyphs for prograde/retrograde, normal/antinormal, radial out/in, target and burn, all
  from the game's own navball data. Under it: frame, heading, pitch and roll.
- **T+ clock** (between the rings and the navball): from liftoff (the vehicle losing ground contact); a vehicle that
  never touched the ground counts from when it was made. Held at 00:00:00 on the pad.
- **Bars** (right): throttle (OFF while no engine burns) and each propellant's fill,
  summed over the vehicle's tanks as the game's Resources panel does.

While the HUD shows, the game's own gauge panels hide and its console messages fade in
above the band; **Mods → Telemetry HUD** in the game's menu bar turns either off. Gauges are
data (`Ring`, `Bar` in `Hud.cs`), so a new one is a list entry.

## How it works

- **Loader:** [StarMap](https://github.com/StarMapLoader/StarMap), the community code-mod
  loader. KSA has no official modding API yet. StarMap calls methods marked
  `[StarMapAfterGui]` every frame, after the game's own UI.
- **Drawing:** the game's UI is Dear ImGui, through RocketWerkz's
  [BRUTAL](https://github.com/ahwootech/BRUTAL) bindings (`Brutal.ImGuiApi`). Gauges are
  drawn on ImGui's foreground draw list (`PathArcTo`, `AddText`), with no window frames.
- **Data:** `Telemetry.cs` is the only file that reads game state. Its calls are copied
  from [KSA-ReGUI](https://github.com/wide-frog/KSA-ReGUI)'s `FlightData.cs`, which has
  many more values (apoapsis, vertical speed, burn timing and so on).
- **Layout:** `Canvas.cs` works in design units for a 1080-pixel-high screen and scales
  with the real height. Text is measured and laid out a character at a time: tabular
  digits (numbers don't jitter), tracking for small capitals, caps centred vertically.
- **Stock gauges:** a Harmony prefix on `GaugeCanvas.OnDrawUi` skips their drawing while
  the HUD shows. Their enabled flags and saved HUD layouts are untouched.
- **Fonts:** Barlow (SIL OFL 1.1, `TelemetryHud/Assets/Fonts`), added to the game's font
  manager after each font rebuild, since the game only scans its own `Content` folder.
- **Settings:** `TelemetryHud/settings.json` in the profile folder (the Borea instance's,
  or My Games/Kitten Space Agency), not the mod folder, which updates replace.
- **Build:** [KSAModTools](https://github.com/AMPW-german/KSAModTools) (the `tooling/`
  submodule) resolves the game's DLLs, writes `mod.toml` and copies the build into the
  game's mods folder. `Directory.Build.props` points it at the Mac app.

## Setup on this Mac

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
5. The first time only, enable it: `/Applications/Borea.app/Contents/MacOS/borea enable TelemetryHud`.
6. Run it: `/Applications/Borea.app/Contents/MacOS/borea launch <instance name>`, or
   press Play in Borea. Starting the app directly skips StarMap.

Each start's log is in the instance's `logs/console.log`. Look for
`Loaded mod: TelemetryHud`. Quit the game before rebuilding, because the game keeps
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
scripts/dev.sh call vehicles              # vehicles to launch (yours and the defaults)
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

## References

- `~/ksa-refs/` has local clones of ReGUI, the mod template, the Mac port, BRUTAL's ImGui
  bindings and the imgui-editor. Read them; don't run them.
- KSA Modding Wiki: https://modding.kittenspaceagency.wiki/
- ImGui Editor (design windows in a browser, get StarMap C#):
  https://safeshows.github.io/imgui-editor/
