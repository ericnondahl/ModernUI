using Brutal.ImGuiApi;

namespace ModernUI;

/// <summary>
/// The HUD's ink: everything the HUD draws in white (text, rings, lines, lit buttons) is
/// drawn in this colour instead, at the same opacity. White by default; the player can pick
/// another in the Mods menu. Black (shades, backings, text on lit buttons) stays black.
/// </summary>
public static class Theme
{
	public static readonly (string Label, HudColor Value)[] Choices =
	[
		("White", HudColor.White), ("Amber", HudColor.Amber), ("Orange", HudColor.Orange),
		("Cyan", HudColor.Cyan), ("Green", HudColor.Green), ("Pink", HudColor.Pink),
	];

	/// <summary>The ink at the given opacity.</summary>
	public static ImColor8 Ink(byte alpha) => Settings.Current.Color switch
	{
		HudColor.Amber => new(255, 194, 64, alpha),
		HudColor.Orange => new(255, 140, 64, alpha),
		HudColor.Cyan => new(100, 222, 255, alpha),
		HudColor.Green => new(130, 240, 120, alpha),
		HudColor.Pink => new(255, 128, 192, alpha),
		_ => new(255, 255, 255, alpha),
	};
}
