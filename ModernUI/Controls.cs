using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace ModernUI;

/// <summary>
/// Clickable controls in the HUD's style. The HUD draws on ImGui's foreground list, which
/// takes no input, so each group of controls sits in an invisible ImGui window: clicks on
/// it go to the HUD, not to the game behind. Buttons are ImGui invisible buttons (hit
/// testing, hover, press) with our own drawing on top.
///
/// Every button's screen rectangle is recorded by its id each frame (Rects), so tests can
/// click a button by name (DevBridge /click?button=STAGE).
/// </summary>
public static class Controls
{
	/// <summary>Last frame's buttons: id to rectangle in ImGui display coordinates.</summary>
	public static readonly Dictionary<string, (float2 Min, float2 Max)> Rects = [];
	static readonly Dictionary<string, (float2 Min, float2 Max)> _building = [];

	const ImGuiWindowFlags RegionFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground
		| ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
		| ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse;

	public static void BeginFrame() => _building.Clear();

	public static void EndFrame()
	{
		Rects.Clear();
		foreach (var (id, rect) in _building)
			Rects[id] = rect;
	}

	/// <summary>An invisible window over [min, max] (design units); buttons go between this and EndRegion.</summary>
	public static void BeginRegion(Canvas c, string id, float2 min, float2 max)
	{
		ImGui.SetNextWindowPos(c.Px(min));
		ImGui.SetNextWindowSize(c.Px(max - min));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(0, 0));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new float2(1, 1));
		ImGui.Begin($"##ModernUI.{id}", RegionFlags);
		ImGui.PopStyleVar(3);
	}

	public static void EndRegion() => ImGui.End();

	static readonly ImColor8 Ink = new(0, 0, 0, 255);
	static readonly ImColor8 Paper = new(255, 255, 255, 235);
	static readonly ImColor8 Backing = new(0, 0, 0, 90);
	public const float TextSize = 15;
	const float Rounding = 3;

	/// <summary>
	/// A text button over [min, max] (design units). Lit: filled white with dark text (the
	/// state is on). Disabled: faint and unclickable. Returns true when clicked.
	/// </summary>
	public static bool Button(Canvas c, string id, string label, float2 min, float2 max, bool lit = false, bool disabled = false)
	{
		var pMin = c.Px(min);
		ImGui.SetCursorScreenPos(pMin);
		bool clicked = ImGui.InvisibleButton($"##{id}", c.Px(max - min));
		bool hovered = ImGui.IsItemHovered() && !disabled;
		bool held = ImGui.IsItemActive() && !disabled;
		_building[id] = (pMin, pMin + c.Px(max - min));

		var centre = (min + max) / 2;
		if (lit)
		{
			c.FillRect(min, max, held ? new ImColor8(255, 255, 255, 170) : Paper, Rounding);
			c.Text(Fonts.SemiBold, TextSize, centre, Align.Center, Ink, label, tracking: 1.4f, shadow: false);
		}
		else
		{
			byte edge = disabled ? (byte)30 : hovered ? (byte)210 : (byte)100;
			byte ink = disabled ? (byte)70 : hovered ? (byte)255 : (byte)220;
			c.FillRect(min, max, held ? new ImColor8(255, 255, 255, 60) : Backing, Rounding);
			c.Rect(min, max, new ImColor8(255, 255, 255, edge), 1.2f, Rounding);
			c.Text(Fonts.SemiBold, TextSize, centre, Align.Center, new ImColor8(255, 255, 255, ink), label, tracking: 1.4f);
		}
		return clicked && !disabled;
	}

	/// <summary>Width of a row of equal buttons.</summary>
	public static float RowWidth(int count, float width, float gap) => count * width + (count - 1) * gap;
}
