using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace ModernUI;

/// <summary>
/// While the HUD shows, the game's gauge panels skip drawing. This only skips the draw
/// call: their enabled flags and the player's saved HUD layouts stay as they were, and
/// the panels come back as soon as the HUD hides (map view, editor, or turned off).
/// </summary>
[HarmonyPatch(typeof(GaugeCanvas), nameof(GaugeCanvas.OnDrawUi))]
static class HideStockGauges
{
	static bool Prefix() => !(Hud.Showing && Settings.Current.HideStockGauges);
}

/// <summary>
/// The Mods menu in the game's menu bar. The game calls Program.DrawProgramMenusHook,
/// an empty method, after drawing its own menus.
/// </summary>
[HarmonyPatch(typeof(Program), nameof(Program.DrawProgramMenusHook))]
static class ModsMenu
{
	static void Postfix()
	{
		if (!ImGui.BeginMenu("Mods"))
			return;
		var s = Settings.Current;
		bool enabled = s.Enabled, hide = s.HideStockGauges;
		if (ImGui.MenuItem("ModernUI", default, ref enabled))
		{
			s.Enabled = enabled;
			s.Save();
		}
		if (ImGui.MenuItem("ModernUI: hide stock gauges", default, ref hide))
		{
			s.HideStockGauges = hide;
			s.Save();
		}
		if (ImGui.BeginMenu("ModernUI mode"))
		{
			foreach (var (label, mode) in ((string, HudMode)[])[("Clean", HudMode.Clean), ("Flight", HudMode.Flight), ("Full", HudMode.Full)])
				if (ImGui.MenuItem(label, "P", s.Mode == mode))
					Hud.SetMode(mode);
			ImGui.EndMenu();
		}
		ImGui.EndMenu();
	}
}

/// <summary>
/// The game only loads .ttf files under its own Content folder, and a mod can live
/// elsewhere (a Borea instance). After the game rebuilds its fonts (at start, and when
/// font settings change) this adds ours through its own loader, so they get the game's
/// pixel snapping and density.
/// </summary>
[HarmonyPatch(typeof(FontManager), nameof(FontManager.RegenerateFonts))]
public static class Fonts
{
	public const string Regular = "Barlow-Regular";
	public const string Medium = "Barlow-Medium";
	public const string SemiBold = "Barlow-SemiBold";
	static readonly string[] All = [Regular, Medium, SemiBold];

	public static string ModDirectory = "";

	static void Postfix() => Load();

	public static void Load()
	{
		foreach (var name in All)
		{
			if (FontManager.Fonts.ContainsKey(name))
				continue;
			var path = Path.Combine(ModDirectory, "Assets", "Fonts", name + ".ttf");
			if (!File.Exists(path))
			{
				Console.WriteLine($"ModernUI: missing font {path}");
				continue;
			}
			FontManager.Fonts[name] = FontManager.LoadFont(path, ImGui.GetIO().Fonts, GameSettings.GetFontSize(),
				GameSettings.GetFontPixelSnap(), GameSettings.GetFontDensity() / 100f);
		}
	}

	/// <summary>Our font, or the game's while ours isn't loaded.</summary>
	public static ImFontPtr Get(string name) =>
		FontManager.Fonts.TryGetValue(name, out var font) ? font : ImGui.GetFont();
}

/// <summary>
/// The game's console shows its latest messages fading at the bottom of the screen, over
/// the HUD's band. While the HUD shows and the console is closed, it gets a viewport that
/// stops above the band, so the messages fade in above it instead. An open console (the \
/// key) is left as it is.
/// </summary>
[HarmonyPatch(typeof(Brutal.ImGuiApi.Abstractions.ConsoleWindow), nameof(Brutal.ImGuiApi.Abstractions.ConsoleWindow.Draw),
	[typeof(Brutal.Numerics.float2), typeof(Brutal.Numerics.float2), typeof(float), typeof(bool)])]
static class ConsoleAboveHud
{
	static void Prefix(Brutal.ImGuiApi.Abstractions.ConsoleWindow __instance, ref Brutal.Numerics.float2 viewportSize)
	{
		if (Hud.Showing && !__instance.IsOpen)
			viewportSize.Y = Math.Max(viewportSize.Y - Hud.ReservedPixels, 0);
	}
}
