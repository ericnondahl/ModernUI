using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace TelemetryHud;

/// <summary>What the navball shows, copied from the game's own navball data each frame.</summary>
public readonly record struct NavballState(
	VehicleReferenceFrame Frame,
	/// <summary>The navball's frame to the vehicle's helm frame (Vehicle.NavBallData.Navball2Helm).</summary>
	doubleQuat Navball2Helm,
	double3? Velocity, double3? Normal, double3? RadialOut, double3? Target, double3? Burn)
{
	public static NavballState Read(Vehicle vehicle)
	{
		ref readonly var ball = ref vehicle.NavBallData;
		ref readonly var m = ref vehicle.NavBallMarkerData;
		// Below walking pace the velocity's direction is noise (a vehicle on the pad).
		bool moving = ball.Speed > 0.5;
		return new NavballState(ball.Frame, ball.Navball2Helm,
			m.VelocityValid && moving ? m.VelocityDirHelm : null,
			m.NormalValid ? m.NormalDirHelm : null,
			m.RadialValid ? m.RadialOutDirHelm : null,
			m.TargetValid ? m.TargetDirHelm : null,
			m.BurnValid ? m.BurnDirHelm : null);
	}
}

/// <summary>
/// A navball drawn as lines on a real sphere, in the HUD's flat style: an orthographic
/// view of the ball with its far side hidden, the ground half a flat dark fill bounded by
/// the curved horizon, grid lines that fade towards the edge, and flat marker glyphs.
///
/// Frames, as the game defines them (Vehicle, VehicleReferenceFrameEx, NavBallRenderer):
/// - Helm: the vehicle's control frame. X is the nose, Y the right wing, Z down. Seen from
///   behind the vehicle, helm Y and Z are screen right and down, and X points into the
///   screen's depth, so a direction is on the ball's visible face when its X is positive.
/// - Navball frame: Navball2Helm rotates it into the helm frame. It is the selected
///   reference frame (SURF: east-north-up) turned by Vehicle.BODY2UPFRAME, which keeps x
///   and negates y and z: navball (x, y, z) = frame (x, -y, -z), so frame "up" is navball -z.
/// </summary>
public static class Navball
{
	static readonly ImColor8 White = new(255, 255, 255, 255);
	static readonly ImColor8 Under = new(0, 0, 0, 90);      // under the ball, against bright scenery
	static readonly ImColor8 Sky = new(255, 255, 255, 38);  // 15%
	static readonly ImColor8 Ground = new(0, 0, 0, 150);    // 60%
	static readonly ImColor8 Rim = new(255, 255, 255, 90);

	const float Stroke = 1.2f;
	const float MarkerSize = 8.5f;
	const float MarkerStroke = 2f;

	// Directions in the reference frame (x east, y north, z up) to the helm frame and back.
	static double3 FrameToHelm(double3 frame, doubleQuat q) => new double3(frame.X, -frame.Y, -frame.Z).Transform(q);
	static double3 HelmToFrame(double3 helm, doubleQuat qInverse)
	{
		var n = helm.Transform(qInverse);
		return new double3(n.X, -n.Y, -n.Z);
	}

	static double3 Direction(double headingDeg, double elevationDeg)
	{
		double h = headingDeg * Math.PI / 180, e = elevationDeg * Math.PI / 180;
		// Heading from north (y) towards east (x).
		return new double3(Math.Cos(e) * Math.Sin(h), Math.Cos(e) * Math.Cos(h), Math.Sin(e));
	}

	/// <summary>Testing: the same markers on a ball turning about all three axes at once.</summary>
	public static NavballState Tumble(NavballState s)
	{
		double t = Environment.TickCount64 / 1000.0;
		var q = QuaternionEx.CreateFromRollYawPitchRadians(new double3(0.29 * t, 1.1 * t, 0.43 * t));
		return s with { Navball2Helm = q };
	}

	public static void Draw(Canvas c, float2 centre, float radius, NavballState s)
	{
		var q = s.Navball2Helm;
		var qInverse = q.Inverse();

		// The ball: dark underlay, sky, then the ground half on top.
		c.FillCircle(centre, radius + 1, Under);
		c.FillCircle(centre, radius, Sky);
		FillGround(c, centre, radius, q);

		// Grid: elevation circles every 30 degrees, headings every 30 degrees.
		foreach (double el in (double[])[-60, -30, 30, 60])
			Curve(c, centre, radius, q, t => Direction(t * 360, el), 0.30f, Stroke);
		for (int h = 0; h < 360; h += 30)
			Curve(c, centre, radius, q, t => Direction(h, -85 + t * 170), h % 90 == 0 ? 0.30f : 0.18f, Stroke);
		// The horizon, brighter.
		Curve(c, centre, radius, q, t => Direction(t * 360, 0), 0.85f, Stroke * 1.6f);

		// Compass letters and elevation numbers, on the visible face only.
		foreach (var (h, name) in ((int, string)[])[(0, "N"), (90, "E"), (180, "S"), (270, "W")])
			Label(c, centre, radius, FrameToHelm(Direction(h, 0), q), name, 15);
		foreach (int el in (int[])[30, 60, -30, -60])
			Label(c, centre, radius, FrameToHelm(Direction(NoseHeading(q, qInverse), el), q), $"{Math.Abs(el)}", 12);

		c.Circle(centre, radius, Rim, Stroke * 1.5f);

		// Markers, where the game puts them. Each has its opposite (retrograde and so on).
		Marker(c, centre, radius, s.Velocity, MarkerKind.Prograde);
		Marker(c, centre, radius, -s.Velocity, MarkerKind.Retrograde);
		Marker(c, centre, radius, s.Normal, MarkerKind.Normal);
		Marker(c, centre, radius, -s.Normal, MarkerKind.Antinormal);
		Marker(c, centre, radius, s.RadialOut, MarkerKind.RadialOut);
		Marker(c, centre, radius, -s.RadialOut, MarkerKind.RadialIn);
		Marker(c, centre, radius, s.Target, MarkerKind.Target);
		Marker(c, centre, radius, s.Burn, MarkerKind.Burn);

		// The nose: fixed in the centre.
		float w = radius * 0.28f, g = radius * 0.08f;
		c.Line(centre - new float2(w, 0), centre - new float2(g, 0), White, 2.5f);
		c.Line(centre + new float2(g, 0), centre + new float2(w, 0), White, 2.5f);
		c.FillCircle(centre, 2.6f, White);
	}

	/// <summary>The nose's heading in the reference frame, for placing the elevation numbers.</summary>
	static double NoseHeading(doubleQuat q, doubleQuat qInverse)
	{
		var nose = HelmToFrame(double3.UnitX, qInverse);
		return Math.Atan2(nose.X, nose.Y) * 180 / Math.PI;
	}

	/// <summary>Heading, pitch and roll of the nose in the reference frame, in degrees.</summary>
	public static (double Heading, double Pitch, double Roll) Attitude(NavballState s)
	{
		var qInverse = s.Navball2Helm.Inverse();
		var nose = HelmToFrame(double3.UnitX, qInverse);
		var top = HelmToFrame(-double3.UnitZ, qInverse); // the vehicle's up: helm -Z
		double pitch = Math.Asin(Math.Clamp(nose.Z, -1, 1)) * 180 / Math.PI;
		double heading = (Math.Atan2(nose.X, nose.Y) * 180 / Math.PI + 360) % 360;
		// Roll: the vehicle's top against the frame's up, both seen along the nose.
		var up = double3.UnitZ - nose * nose.Z;
		double roll = 0;
		if (up.Length() > 1e-6)
		{
			up = up.Normalized();
			var right = double3.Cross(nose, up);
			roll = Math.Atan2(double3.Dot(top, right), double3.Dot(top, up)) * 180 / Math.PI;
		}
		return (heading, pitch, roll);
	}

	// A curve on the sphere (a function of t in 0..1 giving reference-frame directions),
	// drawn in short segments whose opacity fades towards the ball's edge and whose hidden
	// parts are skipped.
	static void Curve(Canvas c, float2 centre, float radius, doubleQuat q, Func<double, double3> at, float alpha, float stroke)
	{
		const int steps = 96;
		var prev = FrameToHelm(at(0), q);
		for (int i = 1; i <= steps; i++)
		{
			var next = FrameToHelm(at((double)i / steps), q);
			double depth = (prev.X + next.X) / 2;
			if (depth > 0)
			{
				float a = alpha * (float)Math.Clamp(0.25 + depth * 0.9, 0, 1);
				c.Line(centre + new float2((float)prev.Y, (float)prev.Z) * radius,
					centre + new float2((float)next.Y, (float)next.Z) * radius, new ImColor8(255, 255, 255, (byte)(a * 255)), stroke);
			}
			prev = next;
		}
	}

	static void Label(Canvas c, float2 centre, float radius, double3 helm, string text, float size)
	{
		if (helm.X < 0.15)
			return;
		var at = centre + new float2((float)helm.Y, (float)helm.Z) * radius;
		byte a = (byte)(255 * Math.Clamp(helm.X * 1.4, 0, 0.85));
		c.Text(Fonts.SemiBold, size, at, Align.Center, new ImColor8(255, 255, 255, a), text, shadow: false);
	}

	// The visible part of the ground hemisphere (frame up < 0), filled exactly.
	//
	// A point at (x, y) on the ball's face (radius 1) is the helm direction
	// (sqrt(1 - x² - y²), x, y). It is ground when that direction has a negative "up"
	// component: dot(U, helm) < 0, with U the frame's up in helm coordinates. Along a ray
	// from the centre at angle a, at distance s = sin(p) (p from 0 to 90 degrees), that is
	// U.X cos p + k sin p with k = U.Y cos a + U.Z sin a: a sinusoid over a quarter turn,
	// so it changes sign at most once, at s* = |U.X| / sqrt(U.X² + k²). Each ray is then
	// all ground, all sky, ground out to s*, or ground from s* to the rim, and the ground
	// fills as thin slices between neighbouring rays. No polygon to assemble, so nothing
	// to get wrong as the ball turns.
	static void FillGround(Canvas c, float2 centre, float radius, doubleQuat q)
	{
		const int rays = 180;
		var up = FrameToHelm(double3.UnitZ, q);
		Span<float2> inner = stackalloc float2[rays + 1], outer = stackalloc float2[rays + 1];
		Span<bool> any = stackalloc bool[rays + 1];
		for (int i = 0; i <= rays; i++)
		{
			double a = 2 * Math.PI * i / rays;
			var dir = new float2((float)Math.Cos(a), (float)Math.Sin(a));
			double k = up.Y * Math.Cos(a) + up.Z * Math.Sin(a);
			bool centreGround = up.X < 0, rimGround = k < 0;
			double crossing = Math.Abs(up.X) / Math.Sqrt(up.X * up.X + k * k + 1e-12);
			(double from, double to) = (centreGround, rimGround) switch
			{
				(true, true) => (0d, 1d),
				(false, false) => (0d, 0d),
				(true, false) => (0d, crossing),
				(false, true) => (crossing, 1d),
			};
			any[i] = to > from;
			inner[i] = centre + dir * (float)(from * radius);
			outer[i] = centre + dir * (float)(to * radius);
		}
		// Slices drawn without anti-aliased edges, so neighbours don't overlap into seams;
		// the rim and horizon lines drawn over the fill's edges keep it crisp.
		ref var flags = ref c.Draw.Flags;
		var saved = flags;
		flags &= ~ImDrawListFlags.AntiAliasedFill;
		for (int i = 0; i < rays; i++)
			if (any[i] || any[i + 1])
				c.FillQuad(inner[i], outer[i], outer[i + 1], inner[i + 1], Ground);
		flags = saved;
	}

		enum MarkerKind { Prograde, Retrograde, Normal, Antinormal, RadialOut, RadialIn, Target, Burn }

	// Flat glyphs: circles, triangles and squares, each with a small difference for its opposite.
	static void Marker(Canvas c, float2 centre, float radius, double3? helm, MarkerKind kind)
	{
		if (helm is not { } h || h.X <= 0.02)
			return;
		h = h.Normalized();
		var p = centre + new float2((float)h.Y, (float)h.Z) * radius;
		float r = MarkerSize, s = MarkerStroke;
		switch (kind)
		{
			case MarkerKind.Prograde:
				c.Circle(p, r, White, s);
				c.Line(p - new float2(r, 0), p - new float2(r * 1.8f, 0), White, s);
				c.Line(p + new float2(r, 0), p + new float2(r * 1.8f, 0), White, s);
				c.Line(p - new float2(0, r), p - new float2(0, r * 1.8f), White, s);
				c.FillCircle(p, 1.8f, White);
				break;
			case MarkerKind.Retrograde:
				// Prograde's ticks, with a cross instead of the dot.
				c.Circle(p, r, White, s);
				c.Line(p - new float2(r, 0), p - new float2(r * 1.8f, 0), White, s);
				c.Line(p + new float2(r, 0), p + new float2(r * 1.8f, 0), White, s);
				c.Line(p + new float2(0, r), p + new float2(0, r * 1.8f), White, s);
				float d = r * 0.6f;
				c.Line(p - new float2(d, d), p + new float2(d, d), White, s);
				c.Line(p + new float2(-d, d), p + new float2(d, -d), White, s);
				break;
			case MarkerKind.Normal:
			case MarkerKind.Antinormal:
				float dir = kind == MarkerKind.Normal ? -1 : 1;
				c.Triangle(p + new float2(0, dir * r * 1.1f), p + new float2(-r, -dir * r * 0.7f), p + new float2(r, -dir * r * 0.7f), White, s);
				if (kind == MarkerKind.Antinormal)
					c.FillCircle(p, 1.8f, White);
				break;
			case MarkerKind.RadialOut:
			case MarkerKind.RadialIn:
				c.Circle(p, r * 0.75f, White, s);
				for (int i = 0; i < 4; i++)
				{
					double a = Math.PI / 4 + i * Math.PI / 2;
					var u = new float2((float)Math.Cos(a), (float)Math.Sin(a));
					// Out: spokes outwards; in: spokes pointing at the centre.
					if (kind == MarkerKind.RadialOut)
						c.Line(p + u * r * 0.75f, p + u * r * 1.5f, White, s);
					else
						c.Line(p + u * r * 0.3f, p + u * r * 0.75f, White, s);
				}
				break;
			case MarkerKind.Target:
				c.Rect(p - new float2(r, r), p + new float2(r, r), White, s);
				c.FillCircle(p, 1.8f, White);
				break;
			case MarkerKind.Burn:
				c.Polygon([p + new float2(0, -r * 1.2f), p + new float2(r * 1.2f, 0), p + new float2(0, r * 1.2f), p + new float2(-r * 1.2f, 0)], White, s);
				c.FillCircle(p, 1.8f, White);
				break;
		}
	}
}
