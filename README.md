# TelemetryHud

A Kitten Space Agency UI mod: flight telemetry as flat, black-and-white gauges in the
style of a launch webcast. Thin rings, big numbers, small caps labels. Nothing
skeuomorphic.

Status: scaffold. It has one speed ring and one altitude ring, drawn bottom-left. It has
not been compiled yet, because compiling needs the game's DLLs (see Setup).

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
- **Build:** [KSAModTools](https://github.com/AMPW-german/KSAModTools) (the `tooling/`
  submodule) resolves the game's DLLs, writes `mod.toml` and copies the build into the
  game's mods folder. `Directory.Build.props` points it at the Mac app.

## Setup on this Mac

1. Game: download the **Linux** build from https://ksa.ahwoo.com into `~/Downloads`, then
   run `./port.sh` in `~/Downloads/ksa-macos-port`. That makes
   `~/Applications/Kitten Space Agency.app`.
2. Mod loader: install [Borea](https://github.com/KSAModding/Borea) (0.6.16 or newer). It
   finds the app and installs StarMap.
3. Build: `dotnet build -c Release`. The output goes to `Release/TelemetryHud/` and is
   copied to `~/Documents/My Games/Kitten Space Agency/mods/TelemetryHud/`.
4. Enable the mod in Borea (or in the mods folder's `manifest.toml`) and start the game
   through Borea, not directly; starting the app directly skips StarMap.

If the build can't find `StarMap.API.dll` because Borea keeps mods in a per-instance
folder, set `KSA_MODS_DIR` to that instance's mods folder.

## References

- `~/ksa-refs/` has local clones of ReGUI, the mod template, the Mac port, BRUTAL's ImGui
  bindings and the imgui-editor. Read them; don't run them.
- KSA Modding Wiki: https://modding.kittenspaceagency.wiki/
- ImGui Editor (design windows in a browser, get StarMap C#):
  https://safeshows.github.io/imgui-editor/
