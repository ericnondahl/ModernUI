namespace ModernUI;

/// <summary>
/// What the game's patches need from the HUD, as of last frame. Only primitives, on purpose:
/// Hud's own statics hold game and Brutal types (reference frames, colours), and if a game
/// update breaks one of those, every use of Hud throws. The patches run inside the game's
/// methods, and the failsafe in ModernUIMod.AfterGui must work then too, so they read this
/// class instead (see Failsafe).
/// </summary>
public static class HudState
{
	/// <summary>Whether the HUD drew last frame; the stock gauges hide while it does.</summary>
	public static bool Showing;

	/// <summary>Where the game's alerts start, under the time block (ImGui units).</summary>
	public static float AlertsTop;

	/// <summary>Height of the screen's bottom strip the HUD uses, in pixels.</summary>
	public static float ReservedPixels;
}
