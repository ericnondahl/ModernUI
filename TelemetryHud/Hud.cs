using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace TelemetryHud;

/// <summary>
/// Launch-webcast style readouts: a thin ring per value with the number in its centre,
/// white on a soft dark band along the bottom of the screen, no chrome. Sizes are design
/// units (a 1080-high screen); see Canvas.
/// </summary>
public static class Hud
{
	/// <summary>Whether the HUD drew last frame; the stock gauges hide while it does.</summary>
	public static bool Showing { get; private set; }

	static readonly ImColor8 White = new(255, 255, 255, 255);
	static readonly ImColor8 Track = new(255, 255, 255, 46);   // 18%
	static readonly ImColor8 Dim = new(255, 255, 255, 166);    // 65%
	static readonly ImColor8 Clear = new(0, 0, 0, 0);
	static readonly ImColor8 Shade = new(0, 0, 0, 140);        // 55% black at the bottom edge

	// Layout, in design units.
	const float Margin = 56;        // from the screen's left and bottom edges
	const float Band = 260;         // height of the shaded band
	const float Radius = 66;
	const float Stroke = 4.5f;
	const float Gap = 44;           // between rings
	const float ValueSize = 40;
	const float UnitSize = 17;
	const float LabelSize = 17;
	const float LabelTracking = 2.6f;

	// The ring is open at the bottom, like a speedometer: 270 degrees of sweep.
	const float StartAngle = MathF.PI * 0.75f;
	const float Sweep = MathF.PI * 1.5f;

	public static void Hide() => Showing = false;

	public static void Draw(Telemetry t)
	{
		Showing = true;
		var c = Canvas.ForScreen();

		c.VerticalGradient(c.Size.Y - Band, c.Size.Y, Clear, Shade);

		// Ring centres: the label sits under each ring, inside the margin.
		float y = c.Size.Y - Margin - LabelSize - 16 - Radius;
		float x = Margin + Radius;
		Gauge(c, new float2(x, y), "SPEED", $"{t.SurfaceSpeed:0}", "M/S", t.SurfaceSpeed / 8000.0);
		Gauge(c, new float2(x + 2 * Radius + Gap, y), "ALTITUDE", FormatAltitude(t.AltitudeAsl),
			AltitudeUnit(t.AltitudeAsl), t.AltitudeAsl / 200_000.0);
	}

	static void Gauge(Canvas c, float2 centre, string label, string value, string unit, double fraction)
	{
		float f = (float)Math.Clamp(fraction, 0.0, 1.0);
		c.Arc(centre, Radius, StartAngle, StartAngle + Sweep, Track, Stroke);
		if (f > 0.001f)
			c.Arc(centre, Radius, StartAngle, StartAngle + Sweep * f, White, Stroke);

		c.Text(Fonts.SemiBold, ValueSize, centre + new float2(0, -6), Align.Center, White, value, tabular: true);
		c.Text(Fonts.Medium, UnitSize, centre + new float2(0, 27), Align.Center, Dim, unit, tracking: 1.6f);
		// Under the ring's open bottom.
		c.Text(Fonts.Medium, LabelSize, centre + new float2(0, Radius + 16), Align.Center, Dim, label, tracking: LabelTracking);
	}

	static string FormatAltitude(double metres) =>
		metres >= 10_000 ? $"{metres / 1000.0:0.0}" : $"{metres:0}";

	static string AltitudeUnit(double metres) => metres >= 10_000 ? "KM" : "M";
}
