using System.Runtime.InteropServices;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace ModernUI;

public enum Align { Left, Center, Right }

/// <summary>
/// One frame of drawing on the whole screen, in design units: the layout is made for a
/// 1080-pixel-high screen and scales with the real height, so it keeps its proportions on
/// a laptop window and on an ultrawide alike. A screen narrower than the layout's minimum
/// width (a 4:3 window, half an ultrawide) scales with its width instead: everything gets
/// smaller rather than overlapping, and the screen is more than 1080 units high. All sizes
/// and positions here are design units; Canvas turns them into pixels.
///
/// The game runs ImGui with multiple viewports, so ImGui positions are the desktop's: the
/// game window's top left is the main viewport's position, which is (0, 0) only when the
/// game fills the primary monitor (full screen, borderless). In a window it is wherever the
/// window is. Positions here are from the window's top left; Canvas adds the window's place.
/// </summary>
public readonly struct Canvas
{
	public const float DesignHeight = 1080f;

	public readonly ImDrawListPtr Draw;
	/// <summary>Screen size in design units: 1080 high, or more on a narrow screen.</summary>
	public readonly float2 Size;
	/// <summary>The game window's top left in ImGui coordinates (the main viewport's position).</summary>
	readonly float2 _origin;
	readonly float _scale;
	// Physical pixels per ImGui unit (2 on a Retina display): text snaps to physical pixels.
	readonly float _pixelsPerPoint;

	Canvas(ImDrawListPtr draw, float2 origin, float2 pixels, float pixelsPerPoint, float minWidth)
	{
		Draw = draw;
		_origin = origin;
		_pixelsPerPoint = pixelsPerPoint > 0 ? pixelsPerPoint : 1;
		_scale = Math.Min(pixels.Y / DesignHeight, pixels.X / minWidth);
		Size = pixels / _scale;
	}

	/// <summary>
	/// The game's window, on its viewport's background draw list: under every ImGui window,
	/// so the game's menu bar, its open menus and its windows draw over the HUD. (The
	/// foreground list would draw over all of them.) minWidth: the narrowest the layout
	/// fits in, in design units; a narrower screen scales the layout down to fit it.
	/// </summary>
	public static Canvas ForScreen(float minWidth)
	{
		var viewport = ImGui.GetMainViewport();
		return new(ImGui.GetBackgroundDrawList(viewport), viewport.Pos, viewport.Size, ImGui.GetIO().DisplayFramebufferScale.Y, minWidth);
	}

	/// <summary>The viewport the HUD is on (its regions go there too).</summary>
	public static ImGuiViewportPtr Viewport => ImGui.GetMainViewport();

	/// <summary>Rounds an ImGui coordinate to the nearest physical pixel.</summary>
	float Snap(float v) => MathF.Round(v * _pixelsPerPoint) / _pixelsPerPoint;

	/// <summary>A length in design units to ImGui units.</summary>
	public float Px(float units) => units * _scale;

	/// <summary>Design units to ImGui units.</summary>
	public float Scale => _scale;

	/// <summary>An ImGui length in design units.</summary>
	public float Units(float imgui) => imgui / _scale;

	/// <summary>A point in design units to ImGui coordinates (on the desktop, see above).</summary>
	public float2 Px(float2 units) => _origin + units * _scale;

	/// <summary>A size in design units to ImGui units.</summary>
	public float2 PxSize(float2 units) => units * _scale;

	/// <summary>A point in ImGui coordinates (the mouse, say) in design units.</summary>
	public float2 Units(float2 imgui) => (imgui - _origin) / _scale;

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

	int Segments(float radius) => Math.Clamp((int)(Px(radius) * 1.2f), 32, 128);

	public void Rect(float2 min, float2 max, ImColor8 color, float thickness, float rounding = 0) =>
		Draw.AddRect(Px(min), Px(max), color, Px(rounding), default, Px(thickness));

	public void FillRect(float2 min, float2 max, ImColor8 color, float rounding = 0) =>
		Draw.AddRectFilled(Px(min), Px(max), color, Px(rounding));

	public void FillTriangle(float2 a, float2 b, float2 c, ImColor8 color) =>
		Draw.AddTriangleFilled(Px(a), Px(b), Px(c), color);

	public void Triangle(float2 a, float2 b, float2 c, ImColor8 color, float thickness) =>
		Draw.AddTriangle(Px(a), Px(b), Px(c), color, Px(thickness));

	/// <summary>A closed outline through the points.</summary>
	public void Polygon(ReadOnlySpan<float2> points, ImColor8 color, float thickness)
	{
		foreach (var p in points)
			Draw.PathLineTo(Px(p));
		Draw.PathStroke(color, ImDrawFlags.Closed, Px(thickness));
	}

	/// <summary>A filled convex quadrilateral (two triangles; may be degenerate).</summary>
	public void FillQuad(float2 a, float2 b, float2 c, float2 d, ImColor8 color)
	{
		Draw.AddTriangleFilled(Px(a), Px(b), Px(c), color);
		Draw.AddTriangleFilled(Px(a), Px(c), Px(d), color);
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
				width += Snap(Px(tracking));
		}
		return width / _scale;
	}

	/// <summary>
	/// The text, shortened with "..." if it is wider than maxWidth (design units) as Text
	/// would lay it out.
	/// </summary>
	public string Fit(string fontName, float size, string text, float maxWidth, float tracking = 0, bool tabular = false)
	{
		if (MeasureText(fontName, size, text, tracking, tabular) <= maxWidth)
			return text;
		for (int n = text.Length - 1; n > 0; n--)
		{
			string shorter = text[..n].TrimEnd(' ', '·') + "...";
			if (MeasureText(fontName, size, shorter, tracking, tabular) <= maxWidth)
				return shorter;
		}
		return "...";
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
		var point = Px(at);
		float x = point.X - align switch { Align.Center => width / 2, Align.Right => width, _ => 0 };
		// ImGui places text by the top of its line; the caps' middle is the baseline minus
		// half the cap height (Barlow's caps are 0.7 em).
		var baked = font.GetFontBaked(px);
		float y = point.Y - baked.Ascent + px * CapHeight / 2;

		float digit = tabular ? Advance(font, px, '0') : 0;
		// Whole physical pixels (not ImGui units, which are 2 pixels on a Retina display):
		// glyphs at fractional positions get snapped unevenly, which shows as random gaps in
		// small tracked text.
		float trackingPx = Snap(Px(tracking));
		y = Snap(y);
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			float advance = Advance(font, px, c);
			float cell = tabular && char.IsAsciiDigit(c) ? digit : advance;
			var glyphAt = new float2(Snap(x + (cell - advance) / 2), y);
			// A soft shadow a little below each glyph keeps text legible over bright cloud.
			if (shadow)
				Draw.AddText(font, px, glyphAt + new float2(0, Px(ShadowOffset)), Shadow, Span(ref c));
			Draw.AddText(font, px, glyphAt, color, Span(ref c));
			x += cell + trackingPx;
		}
	}

	public const float CapHeight = 0.7f;
	const float ShadowOffset = 1.5f;
	static readonly ImColor8 Shadow = new(0, 0, 0, 120);

	static float Advance(ImFontPtr font, float px, char c) =>
		font.CalcTextSizeA(px, float.MaxValue, 0, Span(ref c)).X;

	static ReadOnlySpan<char> Span(ref char c) => MemoryMarshal.CreateReadOnlySpan(ref c, 1);
}
