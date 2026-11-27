# Third-party notices

ModernUI is MIT-licensed (see `LICENSE`). It includes or adapts the following.

## In the released mod

### Barlow (fonts)

`ModernUI/Assets/Fonts/Barlow-{Regular,Medium,SemiBold}.ttf`, version 1.408, unmodified,
from https://github.com/jpt/barlow (as distributed by Google Fonts).
Copyright 2017 The Barlow Project Authors. Licensed under the SIL Open Font License,
Version 1.1 (no Reserved Font Name); the license text ships beside the fonts as
`Assets/Fonts/OFL.txt`.

### KSA-ReGUI

https://github.com/wide-frog/KSA-ReGUI, by wide-frog, at commit `faef93a` (2026-07-30).
The flight values in `ModernUI/Telemetry.cs` (surface velocity from the orbit's state
vectors and the body's rotation) follow ReGUI's `FlightData.cs`.

```
MIT License

Copyright (c) 2026

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## In the repository only (not in the released mod)

### Kitten Space Agency Mac Port

https://github.com/renancamm/ksa-macos-port. The development tool `DevBridge/Commands.cs`
adapts the port's benchmark (`src/game/benchmark`: `World.NewWorld`, `Vehicles.Launch`,
`Vehicles.Fly` and its crew seating) for a new world, a vehicle on a launch pad and a
teleport to a trajectory, and uses the port's driver export `ksaMtlScreenshot`.

```
MIT License

Copyright (c) 2026 KSA macOS Port Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### KSAModTools (build tooling)

`tooling/` is a git submodule of https://github.com/AMPW-german/KSAModTools, licensed
GPL-3.0. It is MSBuild configuration used to build the mod; none of it is compiled into
or shipped with ModernUI. The project started from KSAModTemplate
(https://github.com/AMPW-german/KSAModTemplate), released under the Unlicense.

## Used at run time, not included

ModernUI runs inside Kitten Space Agency (RocketWerkz) and is loaded by StarMap 0.5.4 or
later (https://github.com/StarMapLoader/StarMap, MIT), which provides Harmony 2.4
(https://github.com/pardeike/Harmony, MIT). Neither is distributed with ModernUI.
