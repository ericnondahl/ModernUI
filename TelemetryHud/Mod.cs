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
		// Only in flight view: not in the editor, not in map view.
		Vehicle? vehicle = Program.ControlledVehicle;
		bool inFlight = vehicle != null
			&& !Program.EditorFlag
			&& Program.GetController() != Program.GetMapController();
		if (!inFlight || !Settings.Current.Enabled)
		{
			Hud.Hide();
			return;
		}
		Hud.Draw(Telemetry.Read(vehicle!));
	}
}
