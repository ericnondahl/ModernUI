using System.Text.Json;

namespace TelemetryHud;

/// <summary>
/// The player's choices, in TelemetryHud/settings.json in the game's profile folder
/// (Constants.DocumentsFolderPath: the Borea instance's folder, or My Games/Kitten Space
/// Agency). Not in the mod's own folder: updating or rebuilding the mod replaces it.
/// </summary>
public class Settings
{
	/// <summary>Draw the HUD in flight.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Skip the game's own gauge panels while the HUD shows.</summary>
	public bool HideStockGauges { get; set; } = true;

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
		_path = Path.Combine(KSA.Constants.DocumentsFolderPath, "TelemetryHud", "settings.json");
		try
		{
			if (File.Exists(_path))
				Current = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new();
		}
		catch (Exception e)
		{
			Console.WriteLine($"TelemetryHud: settings.json unreadable, using defaults ({e.Message})");
			Current = new();
		}
	}

	public void Save()
	{
		if (_path == null)
			return;
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
			File.WriteAllText(_path, JsonSerializer.Serialize(this, Json));
		}
		catch (Exception e) { Console.WriteLine($"TelemetryHud: could not save settings ({e.Message})"); }
	}
}
