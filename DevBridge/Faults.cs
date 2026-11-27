using System.Reflection;
using HarmonyLib;

namespace DevBridge;

/// <summary>
/// Injects an error into ModernUI, to test its failsafe: the next call of the chosen method
/// throws once. draw and button throw what a game update that removed a method does
/// (MissingMethodException: the HUD switches off); glitch throws an ordinary error in
/// Hud.Draw (the frame is skipped, the HUD stays). /fault with no `at` only reports
/// ModernUI's failsafe state; at=reset turns the HUD back on, as the Mods menu does.
/// </summary>
static class Faults
{
	static readonly Dictionary<string, (string Type, string Method)> Targets = new()
	{
		["draw"] = ("ModernUI.Hud", "Draw"),         // before any region is open
		["button"] = ("ModernUI.Controls", "Button"), // inside a region (an ImGui window)
		["glitch"] = ("ModernUI.Hud", "Draw"),
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
			var settings = ModernUI("ModernUI.Settings");
			var current = settings.GetProperty("Current")!.GetValue(null)!;
			settings.GetProperty("Enabled")!.SetValue(current, true);
			settings.GetMethod("Save")!.Invoke(current, null);
			return State();
		}
		if (!string.IsNullOrEmpty(at))
		{
			if (!Targets.TryGetValue(at, out var target))
				throw new ArgumentException($"at must be one of {string.Join(", ", Targets.Keys)}, reset");
			string key = $"{target.Type}.{target.Method}";
			if (!Patched.Contains(key))
			{
				var method = AccessTools.Method(ModernUI(target.Type), target.Method)
					?? throw new MissingMemberException(target.Type, target.Method);
				Harmony.Patch(method, prefix: new HarmonyMethod(typeof(Faults), nameof(Throw)));
				Patched.Add(key);
			}
			_armed = at;
		}
		return State();
	}

	static void Throw(MethodBase __originalMethod)
	{
		if (_armed == null || !Targets.TryGetValue(_armed, out var t) || t.Method != __originalMethod.Name)
			return;
		string where = $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}";
		bool glitch = _armed == "glitch";
		_armed = null;
		if (glitch)
			throw new InvalidOperationException($"DevBridge: injected glitch in {where}");
		throw new MissingMethodException($"DevBridge: injected missing method in {where}");
	}

	public static object State()
	{
		var failsafe = ModernUI("ModernUI.Failsafe");
		return new
		{
			armed = _armed,
			tripped = failsafe.GetProperty("Tripped")!.GetValue(null),
			failedPatches = failsafe.GetProperty("FailedPatches")!.GetValue(null),
			showing = ModernUI("ModernUI.HudState").GetField("Showing")!.GetValue(null),
		};
	}

	static Type ModernUI(string name) =>
		AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null)
		?? throw new TypeLoadException($"{name} not loaded (is ModernUI enabled?)");
}
