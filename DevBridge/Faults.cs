using System.Reflection;
using HarmonyLib;

namespace DevBridge;

/// <summary>
/// Injects an error into ModernUI, to test its failsafe (what a game update that breaks
/// something does): the next call of the chosen method throws once. /fault with no
/// `at` only reports ModernUI's failsafe state; at=reset lets the HUD try again.
/// </summary>
static class Faults
{
	static readonly Dictionary<string, (string Type, string Method)> Targets = new()
	{
		["draw"] = ("ModernUI.Hud", "Draw"),         // before any region is open
		["button"] = ("ModernUI.Controls", "Button"), // inside a region (an ImGui window)
	};

	static readonly Harmony Harmony = new("DevBridge.Faults");
	static readonly HashSet<string> Patched = [];
	static string? _armed;

	public static object? Run(string? at)
	{
		if (at == "reset")
		{
			// What ticking Mods → ModernUI does after an error.
			ModernUI("ModernUI.Failsafe").GetMethod("Reset")!.Invoke(null, null);
			return State();
		}
		if (!string.IsNullOrEmpty(at))
		{
			if (!Targets.TryGetValue(at, out var target))
				throw new ArgumentException($"at must be one of {string.Join(", ", Targets.Keys)}");
			if (Patched.Add(at))
			{
				var method = AccessTools.Method(ModernUI(target.Type), target.Method)
					?? throw new MissingMemberException(target.Type, target.Method);
				Harmony.Patch(method, prefix: new HarmonyMethod(typeof(Faults), nameof(Throw)));
			}
			_armed = at;
		}
		return State();
	}

	static void Throw(MethodBase __originalMethod)
	{
		if (_armed == null || !Targets.TryGetValue(_armed, out var t) || t.Method != __originalMethod.Name)
			return;
		_armed = null;
		throw new InvalidOperationException($"DevBridge: injected fault in {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
	}

	public static object State()
	{
		var failsafe = ModernUI("ModernUI.Failsafe");
		return new
		{
			armed = _armed,
			tripped = failsafe.GetProperty("Tripped")!.GetValue(null),
			failedPatches = failsafe.GetProperty("FailedPatches")!.GetValue(null),
			showing = ModernUI("ModernUI.Hud").GetProperty("Showing")!.GetValue(null),
		};
	}

	static Type ModernUI(string name) =>
		AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null)
		?? throw new TypeLoadException($"{name} not loaded (is ModernUI enabled?)");
}
