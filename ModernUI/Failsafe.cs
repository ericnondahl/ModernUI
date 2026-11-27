using HarmonyLib;

namespace ModernUI;

/// <summary>
/// What happens when a game update breaks something ModernUI relies on. KSA is in
/// pre-alpha and ModernUI reaches into its internals, so a new build can rename or remove
/// a method we patch or call. Instead of failing to load or throwing every frame:
/// - each patch is applied on its own, so a missing target loses only that patch;
/// - an error while drawing that a game update causes (a missing method, field or type)
///   switches the HUD off for the session and brings the stock HUD back, logged once, and
///   said in the Mods menu, where turning ModernUI on tries again. Any other error skips
///   that frame, logged once per message; only a run of them switches the HUD off;
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

	/// <summary>Applies each patch class on its own: Harmony's PatchAll, one class at a time.</summary>
	public static void ApplyPatches(Harmony harmony)
	{
		foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Failsafe).Assembly))
		{
			try
			{
				if (type.HasHarmonyAttribute())
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

	/// <summary>Errors in a row (skipped frames) before the HUD switches off: about a second.</summary>
	const int FramesBeforeTrip = 60;
	static int _failedFrames;

	public static void FrameSucceeded() => _failedFrames = 0;

	/// <summary>
	/// An error while drawing. One a game update causes switches the HUD off at once; any
	/// other (a vehicle gone mid-frame, say) skips the frame, and only a run of them does.
	/// </summary>
	public static void FrameFailed(Exception e)
	{
		if (BrokenByUpdate(e) || ++_failedFrames >= FramesBeforeTrip)
		{
			Trip(e);
			return;
		}
		if (Reported.Count < 100 && Reported.Add("frame: " + Describe(e)))
			Console.WriteLine($"ModernUI: skipped a frame after an error (logged once).\n{e}");
	}

	/// <summary>The errors a game that no longer matches ModernUI's build throws.</summary>
	static bool BrokenByUpdate(Exception? e)
	{
		for (; e != null; e = e.InnerException)
			if (e is MissingMemberException or TypeLoadException or TypeInitializationException
				or BadImageFormatException or FileNotFoundException or FileLoadException)
				return true;
		return false;
	}

	/// <summary>Switches the HUD off for the session; logged once.</summary>
	static void Trip(Exception e)
	{
		if (Tripped != null)
			return;
		Tripped = Describe(e);
		Console.WriteLine($"ModernUI: switched off after an error, probably after a game update; the stock HUD is back. " +
			$"Turn it on again in Mods → ModernUI. Please report this with the lines below.\n{e}");
	}

	/// <summary>Lets the HUD try again (Mods → ModernUI).</summary>
	public static void Reset()
	{
		Tripped = null;
		_failedFrames = 0;
	}

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
