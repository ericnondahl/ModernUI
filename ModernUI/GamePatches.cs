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
	static bool Prefix() => !(HudState.Showing && Settings.Current.HideStockGauges);
}

/// <summary>
/// The Mods menu in the game's menu bar. The game calls Program.DrawProgramMenusHook,
/// an empty method, after drawing its own menus.
/// </summary>
[HarmonyPatch(typeof(Program), nameof(Program.DrawProgramMenusHook))]
static class ModsMenu
{
	static bool _broken;

	static void Postfix()
	{
		if (_broken)
			return;
		try
		{
			Menu();
		}
		catch (Exception e)
		{
			_broken = true;
			Failsafe.Report(nameof(ModsMenu), e);
		}
	}

	// Every ImGui call is in here, so a change in the game's ImGui bindings is caught above.
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	static void Menu()
	{
		if (!ImGui.BeginMenu("Mods"))
			return;
		try
		{
			Items();
		}
		finally
		{
			ImGui.EndMenu();
		}
	}

	static void Items()
	{
		var s = Settings.Current;
		// Off after an error (see Failsafe): unticked, and ticking it tries again.
		bool enabled = s.Enabled && Failsafe.Tripped == null, hide = s.HideStockGauges;
		if (ImGui.MenuItem("ModernUI", default, ref enabled))
		{
			if (Failsafe.Tripped != null)
			{
				Failsafe.Reset();
				enabled = true;
			}
			s.Enabled = enabled;
			s.Save();
		}
		if (Failsafe.Tripped != null)
			ImGui.TextDisabled($"  Off after an error, probably a game update (see logs/console.log)");
		else if (Failsafe.FailedPatches.Count > 0)
			ImGui.TextDisabled($"  Partly working after a game update: {string.Join(", ", Failsafe.FailedPatches)} off");
		if (ImGui.MenuItem("ModernUI: hide stock gauges", default, ref hide))
		{
			s.HideStockGauges = hide;
			s.Save();
		}
		if (ImGui.BeginMenu("ModernUI mode"))
		{
			try
			{
				foreach (var (label, mode) in ((string, HudMode)[])[("Clean", HudMode.Clean), ("Flight", HudMode.Flight), ("Full", HudMode.Full)])
					if (ImGui.MenuItem(label, "P", s.Mode == mode))
						Hud.SetMode(mode);
			}
			finally
			{
				ImGui.EndMenu();
			}
		}
		if (ImGui.BeginMenu("ModernUI colour"))
		{
			try
			{
				foreach (var (label, value) in Theme.Choices)
					if (ImGui.MenuItem(label, default, s.Color == value))
					{
						s.Color = value;
						s.Save();
					}
			}
			finally
			{
				ImGui.EndMenu();
			}
		}
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

	static void Postfix() => LoadGuarded();

	/// <summary>Load, but an error (a changed font loader) leaves the game's fonts in use.</summary>
	public static void LoadGuarded()
	{
		try
		{
			Load();
		}
		catch (Exception e)
		{
			Failsafe.Report(nameof(Fonts), e);
		}
	}

	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	static void Load()
	{
		// Not before the game has built its own fonts (the patch adds ours after it does).
		if (FontManager.Fonts.Count == 0)
			return;
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
	static bool _broken;

	static void Prefix(Brutal.ImGuiApi.Abstractions.ConsoleWindow __instance, ref Brutal.Numerics.float2 viewportSize)
	{
		if (_broken || !HudState.Showing)
			return;
		try
		{
			if (!IsOpen(__instance))
				viewportSize.Y = Math.Max(viewportSize.Y - HudState.ReservedPixels, 0);
		}
		catch (Exception e)
		{
			_broken = true;
			Failsafe.Report(nameof(ConsoleAboveHud), e);
		}
	}

	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	static bool IsOpen(Brutal.ImGuiApi.Abstractions.ConsoleWindow console) => console.IsOpen;
}

/// <summary>
/// The game draws its alerts (low supplies, loading, warnings) from a fixed 150 units down
/// the screen, which is where the HUD's time block is on most screens. While the HUD shows
/// they start under the time block instead. (Patched on each list rather than on
/// Alert.DrawAll, which is small enough for the JIT to inline, out of Harmony's reach.)
/// </summary>
[HarmonyPatch]
static class AlertsBelowTime
{
	// Whichever of the three lists still exist, looked up by name so a missing one is skipped
	// (and logged) rather than taking the others with it.
	static IEnumerable<System.Reflection.MethodBase> TargetMethods()
	{
		foreach (var type in (string[])["KSA.LoadAlert", "KSA.FixedAlert", "KSA.TimedAlert"])
		{
			var method = AccessTools.TypeByName(type) is { } t ? AccessTools.Method(t, "DrawList") : null;
			if (method != null)
				yield return method;
			else
				Console.WriteLine($"ModernUI: {type}.DrawList not found, probably after a game update; those alerts keep their place");
		}
	}

	static void Prefix(ref float cursorY)
	{
		if (HudState.Showing && cursorY < HudState.AlertsTop)
			cursorY = HudState.AlertsTop;
	}
}

/// <summary>
/// The game's crew clock box hides while the HUD shows: FleetClock shows the same deadlines
/// in the HUD's style (the crew line, a tag under the time when one is near, the crew card).
/// </summary>
[HarmonyPatch(typeof(CrewClock), nameof(CrewClock.Draw))]
static class HideCrewClock
{
	static bool Prefix() => !(HudState.Showing && Settings.Current.HideStockGauges);
}
