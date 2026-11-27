using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace ModernUI;

/// <summary>What a section of the HUD is about, as a small icon before its label.</summary>
public enum Icon { Speed, Altitude, Orbit, DeltaV, Twr, Stages, Crew, Autopilot, Burn, Target }

/// <summary>
/// Small line-drawn icons before the HUD's section labels (SPEED, STAGING, CREW...), drawn
/// like the staging chips' icons: thin strokes in an 18-unit box, in the label's ink. The
/// player can turn them off in the Mods menu; then every label sits where it did without.
/// </summary>
public static class Icons
{
	/// <summary>The icon's box, design units.</summary>
	public const float Size = 18;
	const float Gap = 7;
	const float Stroke = 1.4f;

	public static bool On => Settings.Current.SectionIcons;

	/// <summary>How far an icon pushes its label right: the box and a gap, or nothing when off.</summary>
	public static float Offset => On ? Size + Gap : 0;

	/// <summary>
	/// A label with its icon before it, the pair aligned on at as the text alone would be
	/// (Left: the icon starts at at.X; Center: the pair is centred; Right: the text ends at
	/// at.X). Returns the pair's width.
	/// </summary>
	public static float Label(Canvas c, Icon icon, float2 at, Align align, ImColor8 ink, string font, float size, string text,
		float tracking = 0)
	{
		float textWidth = c.MeasureText(font, size, text, tracking);
		float width = Offset + textWidth;
		float left = align switch { Align.Center => at.X - width / 2, Align.Right => at.X - width, _ => at.X };
		if (On)
			Draw(c, icon, new float2(left + Size / 2, at.Y), ink);
		c.Text(font, size, new float2(left + Offset, at.Y), Align.Left, ink, text, tracking: tracking);
		return width;
	}

	/// <summary>The icon, centred on at.</summary>
	public static void Draw(Canvas c, Icon icon, float2 at, ImColor8 ink)
	{
		const float t = Stroke;
		float2 P(float x, float y) => at + new float2(x, y);
		switch (icon)
		{
			case Icon.Speed:
				// A speedometer: the rings' open arc and a needle.
				c.Arc(P(0, 1.5f), 8, MathF.PI * 0.75f, MathF.PI * 2.25f, ink, t);
				c.Line(P(0, 1.5f), P(4.5f, -3.5f), ink, t);
				c.FillCircle(P(0, 1.5f), 1.8f, ink);
				break;
			case Icon.Altitude:
				// An arrow up off the ground.
				c.Line(P(-8, 8), P(8, 8), ink, t);
				c.Line(P(0, 5), P(0, -4), ink, t);
				c.FillTriangle(P(-4.5f, -3.5f), P(0, -9), P(4.5f, -3.5f), ink);
				break;
			case Icon.Orbit:
				// A tilted ellipse round a planet, a craft on it.
				Ellipse(c, at, 9, 4.5f, -0.45f, ink, t);
				c.FillCircle(at, 2.6f, ink);
				c.FillCircle(at + Rotate(new float2(9 * MathF.Cos(-1.1f), 4.5f * MathF.Sin(-1.1f)), -0.45f), 1.8f, ink);
				break;
			case Icon.DeltaV:
				// Δ, as in the staging list's Δ-V column.
				c.Polygon([P(0, -8), P(8, 7), P(-8, 7)], ink, t);
				break;
			case Icon.Twr:
				// Thrust up against weight down.
				c.Line(P(-4, 8), P(-4, -3), ink, t);
				c.FillTriangle(P(-8, -3), P(-4, -9), P(0, -3), ink);
				c.Line(P(4, -8), P(4, 3), ink, t);
				c.FillTriangle(P(0, 3), P(8, 3), P(4, 9), ink);
				break;
			case Icon.Stages:
				// A rocket's stages, stacked: narrow on top.
				c.FillRect(P(-2.5f, -9), P(2.5f, -5.5f), ink, 1);
				c.FillRect(P(-5, -1.75f), P(5, 1.75f), ink, 1);
				c.FillRect(P(-7.5f, 5.5f), P(7.5f, 9), ink, 1);
				break;
			case Icon.Crew:
				// A cat's head, as the crew card's cats.
				c.Polygon([P(-7.5f, 3.5f), P(-7.5f, -2), P(-6.5f, -9), P(-2, -5), P(2, -5), P(6.5f, -9), P(7.5f, -2), P(7.5f, 3.5f),
					P(4, 8), P(-4, 8)], ink, t);
				c.FillCircle(P(-3, 1), 1.2f, ink);
				c.FillCircle(P(3, 1), 1.2f, ink);
				break;
			case Icon.Autopilot:
				// The navball's prograde marker: what the autopilot holds.
				c.Circle(at, 4.5f, ink, t);
				c.Line(P(-9, 0), P(-4.5f, 0), ink, t);
				c.Line(P(4.5f, 0), P(9, 0), ink, t);
				c.Line(P(0, -4.5f), P(0, -9), ink, t);
				break;
			case Icon.Burn:
				// A flame, a tongue licking up its left side.
				c.Polygon([P(1, -9), P(4, -4.5f), P(6.5f, -0.5f), P(6.5f, 4), P(4, 7.5f), P(0, 9), P(-4, 7.5f), P(-6.5f, 4),
					P(-6.5f, -0.5f), P(-5, -5.5f), P(-3, -2), P(-1.5f, -5)], ink, t);
				c.Polygon([P(0, 0), P(2.5f, 4), P(1.5f, 7), P(-1.5f, 7), P(-2.5f, 4)], ink, 1);
				break;
			case Icon.Target:
				// A bullseye.
				c.Circle(at, 8, ink, t);
				c.Circle(at, 4.2f, ink, t);
				c.FillCircle(at, 1.4f, ink);
				break;
		}
	}

	static void Ellipse(Canvas c, float2 centre, float rx, float ry, float tilt, ImColor8 ink, float thickness)
	{
		Span<float2> points = stackalloc float2[28];
		for (int i = 0; i < points.Length; i++)
		{
			float a = i * 2 * MathF.PI / points.Length;
			points[i] = centre + Rotate(new float2(rx * MathF.Cos(a), ry * MathF.Sin(a)), tilt);
		}
		c.Polygon(points, ink, thickness);
	}

	static float2 Rotate(float2 p, float angle) =>
		new(p.X * MathF.Cos(angle) - p.Y * MathF.Sin(angle), p.X * MathF.Sin(angle) + p.Y * MathF.Cos(angle));
}
