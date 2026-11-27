using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace ModernUI;

/// <summary>
/// The stock AutopilotSettings panel, in the HUD's style, at the top right: one line with
/// the autopilot's state that opens into every setting, grouped as the stock panel groups
/// them. Each button sets what the stock button sets, lit and greyed as the stock button.
/// </summary>
public static class Autopilot
{
	readonly record struct Item(string Label, Enum Value);
	readonly record struct Group(string Label, int Columns, Item[] Items);

	static readonly Group[] Groups =
	[
		new("MODE", 1, [new("MANUAL", FlightComputerAttitudeMode.Manual)]),
		// Holding the current attitude still in a frame (the stock RATE buttons).
		new("HOLD", 4, [
			new("STAR", VehicleReferenceFrame.EclBody), new("SURF", VehicleReferenceFrame.EnuBody),
			new("ORBT", VehicleReferenceFrame.Lvlh), new("OVEL", VehicleReferenceFrame.VlfBody)]),
		new("SURFACE", 2, [
			new("FORWARD", FlightComputerAttitudeTrackTarget.Forward), new("BACKWARD", FlightComputerAttitudeTrackTarget.Backward),
			new("UP", FlightComputerAttitudeTrackTarget.Up), new("DOWN", FlightComputerAttitudeTrackTarget.Down)]),
		new("ORBIT", 2, [
			new("AHEAD", FlightComputerAttitudeTrackTarget.Ahead), new("BEHIND", FlightComputerAttitudeTrackTarget.Behind),
			new("RADIAL OUT", FlightComputerAttitudeTrackTarget.RadialOut), new("RADIAL IN", FlightComputerAttitudeTrackTarget.RadialIn)]),
		new("VELOCITY", 2, [
			new("PROGRADE", FlightComputerAttitudeTrackTarget.Prograde), new("RETROGRADE", FlightComputerAttitudeTrackTarget.Retrograde),
			new("NORMAL", FlightComputerAttitudeTrackTarget.Normal), new("ANTINORMAL", FlightComputerAttitudeTrackTarget.AntiNormal),
			new("OUTWARD", FlightComputerAttitudeTrackTarget.Outward), new("INWARD", FlightComputerAttitudeTrackTarget.Inward)]),
		new("THRUST", 2, [
			new("DIRECT", FlightComputerManualThrustMode.Direct), new("PULSE", FlightComputerManualThrustMode.Pulse)]),
		new("PROFILE", 3, [
			new("STRICT", FlightComputerAttitudeProfile.Strict), new("BALANCED", FlightComputerAttitudeProfile.Balanced),
			new("RELAXED", FlightComputerAttitudeProfile.Relaxed)]),
		new("ROLL", 3, [
			new("ANY", FlightComputerRollMode.Decoupled), new("UPRIGHT", FlightComputerRollMode.Up),
			new("INVERTED", FlightComputerRollMode.Down)]),
	];

	const float LabelColumn = 84;
	const float ButtonsWidth = 280;
	const float RowHeight = 28;
	const float Gap = 5;
	const float GroupGap = 10;
	const float SmallSize = 15;

	static ImColor8 Dim => Theme.Ink(166);
	static ImColor8 White => Theme.Ink(255);
	static readonly ImColor8 Backing = new(0, 0, 0, 120);

	public static float Width => LabelColumn + ButtonsWidth;

	/// <summary>The panel with its top right corner at (right, top), design units.</summary>
	public static float Draw(Canvas c, float right, float top, Vehicle v, bool forceOpen)
	{
		float left = right - Width;
		bool open = forceOpen || Settings.Current.AutopilotOpen;
		float height = RowHeight + (open ? PanelHeight() : 0);
		Controls.BeginRegion(c, "autopilot", new float2(left, top), new float2(right, top + height));

		// A dark backing, so labels read over bright cloud and sunlit ground.
		const float pad = 10;
		c.FillRect(new float2(left - pad, top - pad), new float2(right + pad, top + height + pad), Backing, 6);

		// The header: what the autopilot is doing, and the button that opens the panel.
		Icons.Label(c, Icon.Autopilot, new float2(left, top + RowHeight / 2), Align.Left, Dim, Fonts.Medium, SmallSize, "AUTOPILOT", 2.4f);
		c.Text(Fonts.SemiBold, SmallSize + 2, new float2(right - 64 - 12, top + RowHeight / 2), Align.Right, White, State(v), tracking: 1.4f);
		if (!forceOpen && Controls.Button(c, "AUTOPILOT", open ? "CLOSE" : "OPEN", new float2(right - 64, top), new float2(right, top + RowHeight), lit: open))
		{
			Settings.Current.AutopilotOpen = !open;
			Settings.Current.Save();
		}

		if (open)
		{
			float y = top + RowHeight + GroupGap;
			foreach (var group in Groups)
				y = DrawGroup(c, left, y, group, v) + GroupGap;
		}
		Controls.EndRegion();
		return top + height + 2 * pad + 8;
	}

	static float PanelHeight()
	{
		float h = GroupGap;
		foreach (var g in Groups)
			h += Rows(g) * RowHeight + (Rows(g) - 1) * Gap + GroupGap;
		return h;
	}

	static int Rows(Group g) => (g.Items.Length + g.Columns - 1) / g.Columns;

	// The group's label on its first row, its buttons in a grid; returns the bottom.
	static float DrawGroup(Canvas c, float left, float top, Group g, Vehicle v)
	{
		c.Text(Fonts.Medium, SmallSize - 2, new float2(left, top + RowHeight / 2), Align.Left, Dim, g.Label, tracking: 2f);
		float x0 = left + LabelColumn;
		float width = (ButtonsWidth - (g.Columns - 1) * Gap) / g.Columns;
		for (int i = 0; i < g.Items.Length; i++)
		{
			var item = g.Items[i];
			int row = i / g.Columns, column = i % g.Columns;
			var min = new float2(x0 + column * (width + Gap), top + row * (RowHeight + Gap));
			if (Controls.Button(c, $"AP {g.Label} {item.Label}", item.Label, min, min + new float2(width, RowHeight),
					GameActions.IsOn(v, item.Value), GameActions.IsDisabled(v, item.Value)))
				GameActions.Apply(v, item.Value);
		}
		return top + Rows(g) * RowHeight + (Rows(g) - 1) * Gap;
	}

	/// <summary>"MANUAL", "HOLD SURF", "PROGRADE"...: the attitude mode in a word or two.</summary>
	static string State(Vehicle v)
	{
		var fc = v.FlightComputer;
		if (fc.AttitudeMode == FlightComputerAttitudeMode.Manual)
			return "MANUAL";
		if (fc.AttitudeTrackTarget == FlightComputerAttitudeTrackTarget.None)
			return "HOLD " + FrameName(fc.AttitudeFrame);
		foreach (var g in Groups)
			foreach (var item in g.Items)
				if (item.Value is FlightComputerAttitudeTrackTarget t && t == fc.AttitudeTrackTarget)
					return item.Label;
		return fc.AttitudeTrackTarget.ToString().ToUpperInvariant();
	}

	public static string FrameName(VehicleReferenceFrame frame) => frame switch
	{
		VehicleReferenceFrame.EclBody => "STAR",
		VehicleReferenceFrame.EnuBody => "SURF",
		VehicleReferenceFrame.Lvlh => "ORBT",
		VehicleReferenceFrame.VlfBody => "OVEL",
		VehicleReferenceFrame.BurnBody => "BURN",
		VehicleReferenceFrame.Dock => "TGT",
		_ => frame.ToString().ToUpperInvariant(),
	};
}
