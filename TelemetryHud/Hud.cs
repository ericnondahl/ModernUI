using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace TelemetryHud;

/// <summary>A ring gauge: a number in a thin open ring, filled to Fraction (0..1).</summary>
public readonly record struct Ring(string Label, string Value, string Unit, double Fraction);

/// <summary>A bar gauge: a label and value over a thin bar. Inactive bars draw dimmed.</summary>
public readonly record struct Bar(string Label, string Value, double Fraction, bool Active = true);

/// <summary>
/// Launch-webcast style band along the bottom of the screen: ring gauges on the left, the
/// T+ clock in the centre, bars for the vehicle's state on the right. White on a soft dark
/// gradient, no chrome. Sizes are design units (a 1080-high screen); see Canvas.
///
/// Gauges are data: Draw builds the lists from telemetry, and one routine draws each kind,
/// so a new gauge is a new list entry.
/// </summary>
public static class Hud
{
	/// <summary>Whether the HUD drew last frame; the stock gauges hide while it does.</summary>
	public static bool Showing { get; private set; }

	/// <summary>Height of the screen's bottom strip the HUD uses, in pixels, as of last frame.</summary>
	public static float ReservedPixels { get; private set; }

	static readonly ImColor8 White = new(255, 255, 255, 255);
	static readonly ImColor8 Track = new(255, 255, 255, 46);   // 18%
	static readonly ImColor8 Dim = new(255, 255, 255, 166);    // 65%
	static readonly ImColor8 Faint = new(255, 255, 255, 102);  // 40%
	static readonly ImColor8 Clear = new(0, 0, 0, 0);
	static readonly ImColor8 Shade = new(0, 0, 0, 178);        // 70% black at the bottom edge

	// Layout, in design units.
	const float Margin = 56;        // from the screen's edges
	const float Band = 320;         // height of the shaded band
	const float SmallSize = 17;     // units and labels
	const float Tracking = 2.6f;    // letter spacing of small capitals

	const float Radius = 66;
	const float Stroke = 4.5f;
	const float RingGap = 44;
	const float RingValueSize = 40;
	// The ring is open at the bottom, like a speedometer: 270 degrees of sweep.
	const float StartAngle = MathF.PI * 0.75f;
	const float Sweep = MathF.PI * 1.5f;

	const float ClockSize = 48;
	const float NavballRadius = 96;
	const float BarWidth = 250;
	const float BarStroke = 4;
	const float BarRowGap = 42;
	const int MaxPropellantBars = 4;

	public static void Hide() => Showing = false;

	public static void Draw(Telemetry t)
	{
		Showing = true;
		var c = Canvas.ForScreen();
		c.VerticalGradient(c.Size.Y - Band, c.Size.Y, Clear, Shade);

		// Everything lines up on two rows: the rings' centre line and the labels' line.
		float labelsY = c.Size.Y - Margin - SmallSize / 2;
		float centreY = labelsY - SmallSize / 2 - 16 - Radius;

		Ring[] rings = [SpeedRing(t), AltitudeRing(t)];
		for (int i = 0; i < rings.Length; i++)
			DrawRing(c, new float2(Margin + Radius + i * (2 * Radius + RingGap), centreY), labelsY, rings[i]);

		// The navball in the centre, its bottom on the same line as the rings' bottoms.
		float ballY = centreY + Radius - NavballRadius;
		Navball.Draw(c, new float2(c.Size.X / 2, ballY), NavballRadius, t.Navball);
		DrawAttitude(c, new float2(c.Size.X / 2, labelsY), t.Navball);

		// The clock between the rings and the navball.
		float ringsRight = Margin + rings.Length * 2 * Radius + (rings.Length - 1) * RingGap;
		float ballLeft = c.Size.X / 2 - NavballRadius;
		DrawClock(c, new float2((ringsRight + ballLeft) / 2, centreY), labelsY, t);

		var bars = new List<Bar> { ThrottleBar(t) };
		foreach (var p in t.Propellants.Take(MaxPropellantBars))
			bars.Add(new Bar(p.Name, $"{p.Fraction * 100:0}%", p.Fraction));
		float right = c.Size.X - Margin;
		for (int i = 0; i < bars.Count; i++)
			DrawBar(c, right, labelsY - (bars.Count - 1 - i) * BarRowGap, bars[i]);

		// The strip the band's items take, from the tallest (rings or the bar stack) down,
		// with a little air: the game's console messages fade in above it.
		float barsTop = labelsY - (bars.Count - 1) * BarRowGap - 16 - SmallSize;
		ReservedPixels = c.Px(c.Size.Y - Math.Min(Math.Min(centreY - Radius, ballY - NavballRadius), barsTop) + 20);
	}

	// ---- the gauges -------------------------------------------------------------------

	// Speed fills towards orbit: circular orbital speed at the vehicle's height.
	static Ring SpeedRing(Telemetry t) =>
		new("SPEED", $"{t.SurfaceSpeed:0}", "M/S", t.SurfaceSpeed / t.OrbitalSpeedHere);

	// Altitude fills towards the top of the atmosphere while inside it, then against the
	// next round height above it.
	static Ring AltitudeRing(Telemetry t)
	{
		double alt = Math.Max(t.AltitudeAsl, 0);
		double full = t.AtmosphereHeight > 0 && alt < t.AtmosphereHeight ? t.AtmosphereHeight : NiceCeiling(alt, 100_000);
		var (value, unit) = alt switch
		{
			< 10_000 => ($"{alt:0}", "M"),
			< 10_000_000 => ($"{alt / 1000:0.0}", "KM"),
			_ => ($"{alt / 1000:0}", "KM"),
		};
		return new("ALTITUDE", value, unit, alt / full);
	}

	static Bar ThrottleBar(Telemetry t) => t.EnginesOn
		? new("THROTTLE", $"{t.Throttle * 100:0}%", t.Throttle)
		: new("THROTTLE", "OFF", t.Throttle, Active: false);

	/// <summary>The smallest of 1, 2, 5 × 10^n times unit that is at least value.</summary>
	static double NiceCeiling(double value, double unit)
	{
		for (double step = unit; ; step *= 10)
			foreach (var m in (double[])[1, 2, 5])
				if (value <= m * step)
					return m * step;
	}

	// ---- drawing ------------------------------------------------------------------------

	static void DrawRing(Canvas c, float2 centre, float labelsY, Ring r)
	{
		float f = (float)Math.Clamp(r.Fraction, 0.0, 1.0);
		c.Arc(centre, Radius, StartAngle, StartAngle + Sweep, Track, Stroke);
		if (f > 0.001f)
			c.Arc(centre, Radius, StartAngle, StartAngle + Sweep * f, White, Stroke);

		c.Text(Fonts.SemiBold, RingValueSize, centre + new float2(0, -6), Align.Center, White, r.Value, tabular: true);
		c.Text(Fonts.Medium, SmallSize, centre + new float2(0, 27), Align.Center, Dim, r.Unit, tracking: 1.6f);
		c.Text(Fonts.Medium, SmallSize, new float2(centre.X, labelsY), Align.Center, Dim, r.Label, tracking: Tracking);
	}

	// "T+ 00:01:23" with the vehicle's name under it, centred on the screen.
	static void DrawClock(Canvas c, float2 centre, float labelsY, Telemetry t)
	{
		var s = TimeSpan.FromSeconds(Math.Max(t.MissionSeconds, 0));
		string digits = $"{(int)s.TotalHours:00}:{s.Minutes:00}:{s.Seconds:00}";
		const string prefix = "T+";
		const float prefixSize = 26, space = 12;

		float prefixWidth = c.MeasureText(Fonts.Medium, prefixSize, prefix, tracking: 1);
		float digitsWidth = c.MeasureText(Fonts.SemiBold, ClockSize, digits, tabular: true);
		float left = centre.X - (prefixWidth + space + digitsWidth) / 2;
		var colour = t.BeforeLiftoff ? Faint : White;
		c.Text(Fonts.Medium, prefixSize, new float2(left, centre.Y), Align.Left, t.BeforeLiftoff ? Faint : Dim, prefix, tracking: 1);
		c.Text(Fonts.SemiBold, ClockSize, new float2(left + prefixWidth + space, centre.Y), Align.Left, colour, digits, tabular: true);

		c.Text(Fonts.Medium, SmallSize, new float2(centre.X, labelsY), Align.Center, Dim, t.VehicleId.ToUpperInvariant(), tracking: Tracking);
	}

	// "SURF   HDG 090   PITCH 45   ROLL 0" under the navball: the frame, then the nose's
	// attitude in it.
	static void DrawAttitude(Canvas c, float2 at, NavballState s)
	{
		var (heading, pitch, roll) = Navball.Attitude(s);
		string frame = s.Frame switch
		{
			KSA.VehicleReferenceFrame.EclBody => "STAR",
			KSA.VehicleReferenceFrame.EnuBody => "SURF",
			KSA.VehicleReferenceFrame.Lvlh => "ORBT",
			KSA.VehicleReferenceFrame.VlfBody => "OVEL",
			KSA.VehicleReferenceFrame.BurnBody => "BURN",
			KSA.VehicleReferenceFrame.Dock => "TGT",
			_ => "",
		};
		string text = $"{frame}   HDG {heading:000}   PITCH {pitch:0}   ROLL {roll:0}";
		c.Text(Fonts.Medium, SmallSize, at, Align.Center, Dim, text, tracking: Tracking, tabular: true);
	}

	// Label left and value right on one line, the bar under them; right edge at `right`.
	static void DrawBar(Canvas c, float right, float barY, Bar b)
	{
		float left = right - BarWidth;
		float textY = barY - 16;
		var colour = b.Active ? White : Faint;
		c.Text(Fonts.Medium, SmallSize, new float2(left, textY), Align.Left, b.Active ? Dim : Faint, b.Label, tracking: Tracking);
		c.Text(Fonts.SemiBold, SmallSize + 2, new float2(right, textY), Align.Right, colour, b.Value, tabular: true);

		float f = (float)Math.Clamp(b.Fraction, 0.0, 1.0);
		c.Line(new float2(left, barY), new float2(right, barY), Track, BarStroke);
		if (f > 0.001f)
			c.Line(new float2(left, barY), new float2(left + BarWidth * f, barY), colour, BarStroke);
	}
}
