using KSA;
using HarmonyLib;

namespace DevBridge;

/// <summary>
/// While a test click runs, the game's ImGui platform backend (KSA.ImGuiBackendGlfwImpl) stops
/// feeding the real mouse to ImGui: it polls the cursor every frame while the window has
/// focus, and its GLFW callbacks push real moves and presses, either of which would
/// overwrite the synthetic events. Outside a test click, nothing changes.
/// </summary>
public static class SyntheticMouse
{
	public static volatile bool Active;

	internal static void Patch(Harmony harmony)
	{
		var skip = new HarmonyMethod(typeof(SyntheticMouse), nameof(SkipWhileActive));
		foreach (var name in (string[])["UpdateMouseData", "OnCursorPos", "OnMouseButton", "OnCursorEnter"])
		{
			var method = AccessTools.Method(typeof(ImGuiBackendGlfwImpl), name);
			if (method == null)
				Console.WriteLine($"DevBridge: ImGuiBackendGlfwImpl.{name} not found; synthetic clicks may be overridden by the real mouse");
			else
				harmony.Patch(method, prefix: skip);
		}
	}

	static bool SkipWhileActive() => !Active;
}
