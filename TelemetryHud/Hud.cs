using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace TelemetryHud;

/// <summary>
/// Launch-webcast style readouts: a thin ring per value with the number in its centre,
/// white on a transparent background, no chrome. Everything is drawn with ImGui's draw
/// list straight onto the screen, so there is no window frame to fight.
/// </summary>
public static class Hud
{
	static readonly ImColor8 White = new(255, 255, 255, 255);
	static readonly ImColor8 Track = new(255, 255, 255, 51); // 20% white
	static readonly ImColor8 Dim = new(255, 255, 255, 153);  // 60% white

	const float Radius = 56f;
	const float Thickness = 4f;
	// The ring is open at the bottom, like a speedometer: 270 degrees of sweep.
	const float StartAngle = MathF.PI * 0.75f;
	const float Sweep = MathF.PI * 1.5f;

	public static void Draw(Telemetry t)
	{
		ImDrawListPtr draw = ImGui.GetForegroundDrawList();
		float2 screen = ImGui.GetIO().DisplaySize;

		// Bottom-left corner, two gauges side by side.
		float y = screen.Y - Radius - 48f;
		Gauge(draw, new float2(48f + Radius, y), "SPEED", $"{t.SurfaceSpeed:0}", "M/S",
			fraction: t.SurfaceSpeed / 8000.0);
		Gauge(draw, new float2(48f + Radius * 3.5f, y), "ALTITUDE", FormatAltitude(t.AltitudeAsl), AltitudeUnit(t.AltitudeAsl),
			fraction: t.AltitudeAsl / 200_000.0);
	}

	static void Gauge(ImDrawListPtr draw, float2 centre, string label, string value, string unit, double fraction)
	{
		float f = (float)Math.Clamp(fraction, 0.0, 1.0);

		// Track, then the filled arc on top.
		draw.PathArcTo(centre, Radius, StartAngle, StartAngle + Sweep, 64);
		draw.PathStroke(Track, default, Thickness);
		if (f > 0f)
		{
			draw.PathArcTo(centre, Radius, StartAngle, StartAngle + Sweep * f, 64);
			draw.PathStroke(White, default, Thickness);
		}

		ImFontPtr font = ImGui.GetFont();
		float size = ImGui.GetFontSize();
		Centred(draw, font, size * 1.6f, centre + new float2(0f, -size * 0.6f), value, White);
		Centred(draw, font, size * 0.8f, centre + new float2(0f, size * 0.9f), unit, Dim);
		Centred(draw, font, size * 0.8f, centre + new float2(0f, Radius + size * 0.2f), label, Dim);
	}

	static void Centred(ImDrawListPtr draw, ImFontPtr font, float size, float2 pos, string text, ImColor8 col)
	{
		// Approximate width for a monospace font; good enough until we measure text.
		float width = text.Length * size * 0.6f;
		draw.AddText(font, size, pos - new float2(width / 2f, 0f), col, text.AsSpan());
	}

	static string FormatAltitude(double metres) =>
		metres >= 10_000 ? $"{metres / 1000.0:0.0}" : $"{metres:0}";

	static string AltitudeUnit(double metres) => metres >= 10_000 ? "KM" : "M";
}
