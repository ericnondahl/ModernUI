using HarmonyLib;

namespace ModernUI;

/// <summary>
/// What happens when a game update breaks something ModernUI relies on. KSA is in
/// pre-alpha and ModernUI reaches into its internals, so a new build can rename or remove
/// a method we patch or call. Instead of failing to load or throwing every frame:
/// - each patch is applied on its own, so a missing target loses only that patch;
/// - an error while drawing switches the HUD off for the session and brings the stock HUD
///   back, logged once, and said in the Mods menu, where turning ModernUI on tries again;
/// - a patch whose own work throws stops doing it and leaves the game's behaviour alone.
/// Everything is written to the game's log (logs/console.log) with a "ModernUI:" prefix.
/// </summary>
public static class Failsafe
{
	/// <summary>Why the HUD is off this session (an error while drawing), or null while it's fine.</summary>
	public static string? Tripped { get; private set; }

	/// <summary>The patches that could not be applied (their classes' names).</summary>
	public static IReadOnlyList<string> FailedPatches => Failed;

	static readonly List<string> Failed = [];
	static readonly HashSet<string> Reported = [];

	/// <summary>Applies each patch class on its own, as Harmony's PatchAll would, but one at a time.</summary>
	public static void ApplyPatches(Harmony harmony)
	{
		foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Failsafe).Assembly))
		{
			try
			{
				harmony.CreateClassProcessor(type).Patch();
			}
			catch (Exception e)
			{
				Failed.Add(type.Name);
				Console.WriteLine($"ModernUI: could not apply the {type.Name} patch, probably after a game update; " +
					$"the rest of ModernUI works without it. {Describe(e)}");
			}
		}
	}

	/// <summary>Switches the HUD off for the session after an error while drawing; logged once.</summary>
	public static void Trip(Exception e)
	{
		if (Tripped != null)
			return;
		Tripped = Describe(e);
		Console.WriteLine($"ModernUI: switched off after an error, probably after a game update; the stock HUD is back. " +
			$"Turn it on again in Mods → ModernUI. Please report this with the lines below.\n{e}");
	}

	/// <summary>Lets the HUD try again (Mods → ModernUI).</summary>
	public static void Reset() => Tripped = null;

	/// <summary>Logs a patch's error once per patch; the caller then leaves the game's behaviour alone.</summary>
	public static void Report(string patch, Exception e)
	{
		if (Reported.Add(patch))
			Console.WriteLine($"ModernUI: the {patch} patch failed and is off for this session, probably after a game update.\n{e}");
	}

	/// <summary>The innermost exception's type and message, on one line.</summary>
	static string Describe(Exception e)
	{
		while (e.InnerException != null)
			e = e.InnerException;
		return $"{e.GetType().Name}: {e.Message}";
	}
}
