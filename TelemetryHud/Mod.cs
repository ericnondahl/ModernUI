using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;
using StarMap.API;

namespace TelemetryHud;

/// <summary>
/// Entry point. StarMap finds this class by its [StarMapMod] attribute and calls the
/// attributed methods at each stage of the game's life.
/// </summary>
[StarMapMod]
public class TelemetryHud
{
	Harmony? _harmony;

	[StarMapImmediateLoad]
	public void ImmediateLoad(Mod mod)
	{
		Fonts.ModDirectory = mod.DirectoryPath;
		Settings.Load();
	}

	[StarMapAllModsLoaded]
	public void AllModsLoaded()
	{
		_harmony = new Harmony("TelemetryHud");
		_harmony.PatchAll();
		// If the game built its fonts before we patched its font manager, add ours now;
		// later rebuilds go through the patch.
		if (FontManager.Fonts.Count > 0)
			Fonts.Load();
	}

	[StarMapUnload]
	public void Unload() => _harmony?.UnpatchAll("TelemetryHud");

	[StarMapAfterGui]
	public void AfterGui(double dt)
	{
		// In flight and in map view (the stock gauges show in both); not in the editor, and not
		// while the game's UI is hidden (F2).
		Vehicle? vehicle = Program.ControlledVehicle;
		bool inFlight = vehicle != null && !Program.EditorFlag && Program.DrawUI;

		// P: the next mode (CLEAN, FLIGHT, FULL); not while typing (the console, a text field).
		if (inFlight && Settings.Current.Enabled && !ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey.P, false))
			Hud.CycleMode();
		if (!inFlight || !Settings.Current.Enabled)
		{
			Hud.Hide();
			return;
		}
		Hud.Draw(Telemetry.Read(vehicle!), vehicle!);
	}
}
