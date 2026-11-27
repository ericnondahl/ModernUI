# Draft: Ahwoo forums thread (Mod Releases, prefix "User Interface")

Post this yourself: the content index ties the listing to your forums account through this
thread. Upload the screenshots from `images/` into the post, and attach or link the demo
video. Then put the thread's URL into `docs/publishing/ModernUI.toml` (`links.forums`).

---

**Title:** ModernUI 0.1.0 – a launch-webcast style flight HUD

**Body:**

ModernUI replaces the stock flight HUD with one in the style of a launch webcast: flat,
black and white, thin rings and big numbers. Every stock flight gauge has a replacement,
controls included, and the HUD stays out of the way when nothing needs you.

[flight.jpg]

**What's in it**
- Speed and altitude rings (speed fills towards orbit), apoapsis and periapsis with warp-to
- A line-drawn navball with every marker, heading/pitch/roll and their rates
- Engine deck: delta-v, TWR, stage; ignite, shutdown, stage, RCS
- Staging list with each stage's delta-v, TWR, Isp and burn time
- Your crew as little wireframe cats (they blink, and squint when they're unwell), with
  life support, power, radiation, and every vessel's next crew deadline
- Autopilot, burn control, a map-view burn editor, a target card, EVA controls
- Three modes on the P key: CLEAN (just the webcast look), FLIGHT (panels fold to one
  line), FULL

[clean.jpg] [autopilot.jpg] [burn-editor.jpg] [eva.jpg]

**Demo video:** [link to the video]

**Install:** through Borea (search ModernUI), or download ModernUI.zip from the GitHub
release and unzip it into your mods folder. Needs StarMap 0.5.4 or later.

**Compatibility:** built for KSA 2026.11.25.5949. Tested on macOS (the community Mac port);
it should work on Windows and Linux but I haven't tested them yet, so reports are very
welcome. It will overlap with other mods that replace the stock HUD.

**Source and issues:** https://github.com/ericnondahl/ModernUI (MIT)

Feedback welcome, especially on readability and anything the stock HUD did that you miss.
