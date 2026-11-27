# Draft: Ahwoo forums thread (Mod Releases, prefix "User Interface")

Post this yourself: the content index ties the listing to your forums account through this
thread. Upload the screenshots from `images/` into the post, and link the demo video. Then
put the thread's URL into `docs/publishing/ModernUI.toml` (`links.forums`).

It covers what the forum's mod rules ask of a post (section 3): name and version, license,
download link, source link, dependencies with links, a description, and the credits for
others' work (authors, licenses, versions, links).

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

**Version:** 0.1.0
**License:** MIT (the bundled Barlow fonts: SIL Open Font License 1.1)
**Download:** https://github.com/ericnondahl/ModernUI/releases/tag/v0.1.0 (ModernUI.zip)
**Source code:** https://github.com/ericnondahl/ModernUI
**Dependencies:** StarMap 0.5.4 or later (https://github.com/StarMapLoader/StarMap), the
code mod loader. Borea (https://github.com/KSAModding/Borea) installs it for you.

**Install:** through Borea (search ModernUI), or unzip ModernUI.zip into your mods folder,
enable ModernUI and start the game through StarMap.

**Compatibility:** built for KSA 2026.11.25.5949. Tested on Windows and macOS (the community
Mac port); Linux reports are very welcome. It will overlap with other mods that replace
the stock HUD.

**Credits**
- Barlow typeface, version 1.408, by The Barlow Project Authors, SIL Open Font License 1.1,
  bundled unmodified with its license: https://github.com/jpt/barlow
- KSA-ReGUI by wide-frog (commit faef93a, 2026-07-30), MIT: some of ModernUI's flight values
  follow its FlightData.cs: https://github.com/wide-frog/KSA-ReGUI
- Full notices: THIRD_PARTY_NOTICES.md in the download and the repository.

Feedback welcome, especially on readability and anything the stock HUD did that you miss.
