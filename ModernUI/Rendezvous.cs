using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace ModernUI;

/// <summary>
/// The stock RendezvousControl gauge in the HUD's style, whenever a target is set: the
/// target, its distance and relative speed, the next closest approach (how close, how
/// soon), and TOWARD / ANTI-VEL / AWAY / ALIGN / HOLD as the stock buttons. CLEAR drops the
/// target the way the game's own target clicks do (ChangeTargetBuffer).
/// </summary>
public static class Rendezvous
{
	const float Width = 364;
	const float RowHeight = 28;
	const float Gap = 5;
	const float SmallSize = 15;
	const float Pad = 10;

	static ImColor8 White => Theme.Ink(255);
	static ImColor8 Dim => Theme.Ink(166);
	static readonly ImColor8 Backing = new(0, 0, 0, 120);

	/// <summary>Draws the card with its top right at (right, top) if a target is set; returns its bottom.</summary>
	public static float Draw(Canvas c, float right, float top, Vehicle v, UniverseTime now)
	{
		var target = v.Target;
		if (target == null)
			return top;
		float left = right - Width;
		float height = RowHeight + 8 + 44 + 8 + RowHeight * 2 + Gap;
		Controls.BeginRegion(c, "rendezvous", new float2(left, top), new float2(right, top + height));
		c.FillRect(new float2(left - Pad, top - Pad), new float2(right + Pad, top + height + Pad), Backing, 6);

		float hy = top + RowHeight / 2;
		c.Text(Fonts.Medium, SmallSize, new float2(left, hy), Align.Left, Dim, "TARGET", tracking: 2.4f);
		// Between the TARGET label and CLEAR: a long name shortens.
		string name = c.Fit(Fonts.SemiBold, SmallSize + 2, target.Id.ToUpperInvariant(), Width - 72 - 96, tracking: 1.4f);
		c.Text(Fonts.SemiBold, SmallSize + 2, new float2(right - 72, hy), Align.Right, White, name, tracking: 1.4f);
		if (Controls.Button(c, "TARGET CLEAR", "CLEAR", new float2(right - 64, top), new float2(right, top + RowHeight)))
			InputEvents.ChangeTargetBuffer.Add(new InputEvents.ChangeTargetData { Vehicle = v, Target = null });

		// As Program's gauge update works them out.
		double distance = (target.GetPositionEcl() - v.GetPositionEcl()).Length();
		double speed = (target.GetVelocityEcl() - v.GetVelocityEcl()).Length();
		var encounter = v.FlightPlan.TryFindNextClosestApproach(target, now);
		(string Label, string Value)[] values =
		[
			("DISTANCE", Hud.FormatLength(distance)),
			("REL M/S", speed < 100 ? $"{speed:N1}" : $"{speed:N0}"),
			("CLOSEST", encounter is { } e1 ? Hud.FormatLength(e1.ClosestDistance) : "—"),
			("IN", encounter is { } e2 ? Hud.FormatCountdown(e2.GameTime.Seconds() - now.Seconds()) : "—"),
		];
		float colWidth = Width / values.Length;
		float vy = top + RowHeight + 8;
		for (int i = 0; i < values.Length; i++)
		{
			float x = left + i * colWidth;
			c.Text(Fonts.Medium, SmallSize - 3, new float2(x, vy + 8), Align.Left, Dim, values[i].Label, tracking: 1.6f);
			c.Text(Fonts.SemiBold, SmallSize + 1, new float2(x, vy + 30), Align.Left, White, values[i].Value, tabular: true);
		}

		(string Label, Enum Value)[] buttons =
		[
			("TOWARD", FlightComputerAttitudeTrackTarget.Toward),
			("ANTI-VEL", FlightComputerAttitudeTrackTarget.Antivel),
			("AWAY", FlightComputerAttitudeTrackTarget.Away),
			("ALIGN", FlightComputerAttitudeTrackTarget.Align),
			("HOLD", VehicleReferenceFrame.Dock),
		];
		float by = vy + 44 + 8;
		float bw = (Width - 2 * Gap) / 3;
		for (int i = 0; i < buttons.Length; i++)
		{
			var (label, value) = buttons[i];
			var min = new float2(left + (i % 3) * (bw + Gap), by + (i / 3) * (RowHeight + Gap));
			if (Controls.Button(c, $"TARGET {label}", label, min, min + new float2(bw, RowHeight),
					GameActions.IsOn(v, value), GameActions.IsDisabled(v, value)))
				GameActions.Apply(v, value);
		}
		Controls.EndRegion();
		return top + height + 2 * Pad + 8;
	}
}
