using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModernUI;

/// <summary>How much of the HUD shows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HudMode>))]
public enum HudMode
{
	/// <summary>The webcast look: rings, navball, throttle and propellant, T+. No controls.</summary>
	Clean,
	/// <summary>Everything, with the side panels as one-line headers that open on a click.</summary>
	Flight,
	/// <summary>Everything, every panel open.</summary>
	Full,
}

/// <summary>
/// The player's choices, in ModernUI/settings.json in the game's profile folder
/// (Constants.DocumentsFolderPath: the Borea instance's folder, or My Games/Kitten Space
/// Agency). Not in the mod's own folder: updating or rebuilding the mod replaces it.
/// </summary>
public class Settings
{
	/// <summary>Draw the HUD in flight.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Skip the game's own gauge panels while the HUD shows.</summary>
	public bool HideStockGauges { get; set; } = true;

	/// <summary>How much of the HUD shows; the P key cycles it.</summary>
	public HudMode Mode { get; set; } = HudMode.Flight;

	/// <summary>Whether the crew card is open (in FLIGHT mode; closed, it is one line).</summary>
	public bool CrewOpen { get; set; }

	/// <summary>Whether the staging list is open (its header is always shown).</summary>
	public bool StagingOpen { get; set; }

	/// <summary>Whether the autopilot panel is open (its header is always shown).</summary>
	public bool AutopilotOpen { get; set; }

	/// <summary>
	/// Testing only, not in the menu: the navball tumbles through every orientation over
	/// time instead of following the vehicle, so a burst of screenshots covers them all.
	/// </summary>
	public bool DebugNavballTumble { get; set; }

	public static Settings Current { get; private set; } = new();

	static string? _path;
	static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

	public static void Load()
	{
		try
		{
			_path = ProfilePath();
			if (File.Exists(_path))
				Current = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new();
		}
		catch (Exception e)
		{
			Console.WriteLine($"ModernUI: settings.json unreadable, using defaults ({e.Message})");
			Current = new();
		}
	}

	// The game's profile folder; in a method of its own so a renamed member is caught in Load.
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	static string ProfilePath() => Path.Combine(KSA.Constants.DocumentsFolderPath, "ModernUI", "settings.json");

	public void Save()
	{
		if (_path == null)
			return;
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
			File.WriteAllText(_path, JsonSerializer.Serialize(this, Json));
		}
		catch (Exception e) { Console.WriteLine($"ModernUI: could not save settings ({e.Message})"); }
	}
}
