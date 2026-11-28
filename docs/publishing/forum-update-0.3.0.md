# Draft: forums update for 0.3.0

A reply in the ModernUI thread (Mod Releases). Upload `images/flight-v3.jpg` into the post.

Then edit the first post so it stays current: the title (ModernUI 0.3.0 – a
launch-webcast style flight HUD), Version: 0.3.0, and `images/flight-v3.jpg` in place of
`flight-v2.jpg`.

---

ModernUI 0.3.0 is out: it works in a window now, and the sections have icons.

Windowed mode. In a window (not borderless), the HUD drew as if the game filled the
screen: it landed off the window, and the staging, crew and autopilot panels showed up as
black windows of their own. Now it draws inside the game window wherever you put it, and
its buttons click where they are. Thanks for the report!

Narrow windows. On a window narrower than 16:9, such as half an ultrawide or 4:3, the
bottom row used to squeeze together until things overlapped. Now the whole HUD gets a
little smaller instead, and nothing overlaps.

Section icons. Each section's label has a small line-drawn icon now: speed, altitude,
orbit, delta-v, TWR, stage, staging, crew, autopilot, burns and target. Mods → ModernUI:
section icons turns them off.

[flight-v3.jpg]

Smaller things: delta-v's M/S sits after the number now, and the universal time and vessel
line is centred under the clock.

Download: https://github.com/ericnondahl/ModernUI/releases/tag/v0.3.0 (ModernUI.zip),
or update it in Borea.
Compatibility: built for KSA 2026.11.25.5949, StarMap 0.5.4 or later.

Feedback is welcome, especially from anyone playing in a window or on an unusual screen
shape.
