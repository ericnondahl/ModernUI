using System.Runtime.InteropServices;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace TelemetryHud;

public enum Align { Left, Center, Right }

/// <summary>
/// One frame of drawing on the whole screen, in design units: the layout is made for a
/// 1080-pixel-high screen and scales with the real height, so it keeps its proportions on
/// a laptop window and on an ultrawide alike. All sizes and positions here are design
/// units; Canvas turns them into pixels.
/// </summary>
public readonly struct Canvas
{
	public const float DesignHeight = 1080f;

	public readonly ImDrawListPtr Draw;
	/// <summary>Screen size in design units (height is always 1080).</summary>
	public readonly float2 Size;
	readonly float _scale;

	Canvas(ImDrawListPtr draw, float2 pixels)
	{
		Draw = draw;
		_scale = pixels.Y / DesignHeight;
		Size = new float2(pixels.X / _scale, DesignHeight);
	}

	public static Canvas ForScreen() => new(ImGui.GetForegroundDrawList(), ImGui.GetIO().DisplaySize);

	public float Px(float units) => units * _scale;
	public float2 Px(float2 units) => units * _scale;

	// ---- shapes ----

	/// <summary>An arc from angle a0 to a1 (radians, 0 = right, clockwise on screen).</summary>
	public void Arc(float2 centre, float radius, float a0, float a1, ImColor8 color, float thickness)
	{
		int segments = Math.Max(8, (int)(Px(radius) * Math.Abs(a1 - a0) / 3f));
		Draw.PathArcTo(Px(centre), Px(radius), a0, a1, segments);
		Draw.PathStroke(color, default, Px(thickness));
	}

	public void Circle(float2 centre, float radius, ImColor8 color, float thickness) =>
		Draw.AddCircle(Px(centre), Px(radius), color, Segments(radius), Px(thickness));

	public void FillCircle(float2 centre, float radius, ImColor8 color) =>
		Draw.AddCircleFilled(Px(centre), Px(radius), color, Segments(radius));

	int Segments(float radius) => Math.Clamp((int)(Px(radius) * 0.6f), 12, 128);

	public void Rect(float2 min, float2 max, ImColor8 color, float thickness) =>
		Draw.AddRect(Px(min), Px(max), color, 0, default, Px(thickness));

	public void Triangle(float2 a, float2 b, float2 c, ImColor8 color, float thickness) =>
		Draw.AddTriangle(Px(a), Px(b), Px(c), color, Px(thickness));

	/// <summary>A closed outline through the points.</summary>
	public void Polygon(ReadOnlySpan<float2> points, ImColor8 color, float thickness)
	{
		foreach (var p in points)
			Draw.PathLineTo(Px(p));
		Draw.PathStroke(color, ImDrawFlags.Closed, Px(thickness));
	}

	/// <summary>A filled polygon; it may be concave.</summary>
	public void FillPolygon(IReadOnlyList<float2> points, ImColor8 color)
	{
		if (points.Count < 3)
			return;
		foreach (var p in points)
			Draw.PathLineTo(Px(p));
		Draw.PathFillConcave(color);
	}

	/// <summary>A straight line with square ends.</summary>
	public void Line(float2 from, float2 to, ImColor8 color, float thickness) =>
		Draw.AddLine(Px(from), Px(to), color, Px(thickness));

	/// <summary>A vertical gradient across the full width, from top colour to bottom colour.</summary>
	public void VerticalGradient(float top, float bottom, ImColor8 topColor, ImColor8 bottomColor)
	{
		uint t = Pack(topColor), b = Pack(bottomColor);
		Draw.AddRectFilledMultiColor(Px(new float2(0, top)), Px(new float2(Size.X, bottom)), t, t, b, b);
	}

	// ImColor8 is ImGui's packed 32-bit colour (ImU32) with its bytes private.
	static uint Pack(ImColor8 c) => System.Runtime.CompilerServices.Unsafe.As<ImColor8, uint>(ref c);

	// ---- text ----

	/// <summary>
	/// Text laid out a character at a time: digits in equal cells when tabular (numbers
	/// don't jitter as they change), and extra space between letters (tracking, in design
	/// units) for small uppercase labels. Returns the width in design units.
	/// </summary>
	public float MeasureText(string fontName, float size, string text, float tracking = 0, bool tabular = false)
	{
		var font = Fonts.Get(fontName);
		float px = Px(size), width = 0, digit = tabular ? Advance(font, px, '0') : 0;
		for (int i = 0; i < text.Length; i++)
		{
			width += tabular && char.IsAsciiDigit(text[i]) ? digit : Advance(font, px, text[i]);
			if (i < text.Length - 1)
				width += Px(tracking);
		}
		return width / _scale;
	}

	/// <summary>
	/// Draws text whose cap height is centred on y: numbers and capitals sit visually in the
	/// middle of whatever they are centred on, which ImGui's top-of-line position doesn't do.
	/// </summary>
	public void Text(string fontName, float size, float2 at, Align align, ImColor8 color, string text,
		float tracking = 0, bool tabular = false, bool shadow = true)
	{
		var font = Fonts.Get(fontName);
		float px = Px(size);
		float width = Px(MeasureText(fontName, size, text, tracking, tabular));
		float x = Px(at.X) - align switch { Align.Center => width / 2, Align.Right => width, _ => 0 };
		// ImGui places text by the top of its line; the caps' middle is the baseline minus
		// half the cap height (Barlow's caps are 0.7 em).
		var baked = font.GetFontBaked(px);
		float y = Px(at.Y) - baked.Ascent + px * CapHeight / 2;

		float digit = tabular ? Advance(font, px, '0') : 0;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			float advance = Advance(font, px, c);
			float cell = tabular && char.IsAsciiDigit(c) ? digit : advance;
			var glyphAt = new float2(x + (cell - advance) / 2, y);
			// A soft shadow a little below each glyph keeps text legible over bright cloud.
			if (shadow)
				Draw.AddText(font, px, glyphAt + new float2(0, Px(ShadowOffset)), Shadow, Span(ref c));
			Draw.AddText(font, px, glyphAt, color, Span(ref c));
			x += cell + Px(tracking);
		}
	}

	public const float CapHeight = 0.7f;
	const float ShadowOffset = 1.5f;
	static readonly ImColor8 Shadow = new(0, 0, 0, 120);

	static float Advance(ImFontPtr font, float px, char c) =>
		font.CalcTextSizeA(px, float.MaxValue, 0, Span(ref c)).X;

	static ReadOnlySpan<char> Span(ref char c) => MemoryMarshal.CreateReadOnlySpan(ref c, 1);
}
