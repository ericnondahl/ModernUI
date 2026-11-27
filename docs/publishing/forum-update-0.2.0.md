# Draft: forums update for 0.2.0

A reply in the ModernUI thread (Mod Releases). Upload `images/staging.jpg` and
`images/colours.jpg` into the post, and link the staging video
(`demo/ModernUI-staging.mp4`, shared outside the repository).

Then edit the first post so it stays current: the title (ModernUI 0.2.0 – a
launch-webcast style flight HUD), Version: 0.2.0, the download link
(https://github.com/ericnondahl/ModernUI/releases/latest), the staging line in "What's in
it", and `images/flight-v2.jpg` in place of `flight.jpg`.

---

ModernUI 0.2.0 is out: a staging list you can rearrange in flight, and HUD colours.

See what each stage fires. Every stage now lists its actions: engines with their fuel,
decouplers and parachutes. Hover one and its parts light up on the rocket. With the panel
closed, the header still shows what the next stage will fire.

Restage in flight. Drag an action to another stage, or drag a stage by its name to
reorder it. It goes through the game's own staging code, just like the stock panel. Stages
that have already fired stay put.

[staging.jpg]

Long rockets scroll. The list shows about six stages and scrolls past that: the strips
above and below name the hidden stages, and the mouse wheel works too. Hold a dragged action
over a strip to reach a hidden stage. Before, anything past the eighth stage simply
disappeared.

HUD colours. Mods → ModernUI colour draws the whole HUD in amber, orange, cyan, green
or pink instead of white.

[colours.jpg]

Parts the game leaves unnamed (escape towers, heatshields, interstages and so on) now get a
name in the staging list.

Staging video: [link to the staging video]

Download: https://github.com/ericnondahl/ModernUI/releases/tag/v0.2.0 (ModernUI.zip),
or update it in Borea.
Compatibility: built for KSA 2026.11.25.5949, StarMap 0.5.4 or later.

As always, feedback is welcome. I'd especially like to hear about rockets with lots of
stages and how the staging list handles them.
