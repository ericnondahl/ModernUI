using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace ModernUI;

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
	/// <summary>Whether the HUD drew last frame (kept in HudState, which the patches read).</summary>
	public static bool Showing { get => HudState.Showing; private set => HudState.Showing = value; }

	/// <summary>Where the game's alerts start, under the time block (ImGui units), as of last frame.</summary>
	public static float AlertsTop { get => HudState.AlertsTop; private set => HudState.AlertsTop = value; }

	/// <summary>Height of the screen's bottom strip the HUD uses, in pixels, as of last frame.</summary>
	public static float ReservedPixels { get => HudState.ReservedPixels; private set => HudState.ReservedPixels = value; }

	static ImColor8 White => Theme.Ink(255);
	static ImColor8 Track => Theme.Ink(46);   // 18%
	static ImColor8 Dim => Theme.Ink(166);    // 65%
	static ImColor8 Faint => Theme.Ink(102);  // 40%
	static readonly ImColor8 Clear = new(0, 0, 0, 0);
	static readonly ImColor8 Shade = new(0, 0, 0, 178);        // 70% black at the bottom edge
	static readonly ImColor8 ShadeTop = new(0, 0, 0, 140);     // behind the time, at the top edge

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

	public static void Hide()
	{
		Showing = false;
		Staging.Reset();
	}

	// Orbit block (apoapsis and periapsis) and the engine deck, between the rings, the
	// navball and the bars.
	const float OrbitWidth = 180;
	const float ButtonHeight = 30;
	const float ButtonGap = 8;

	public static void Draw(Telemetry t, KSA.Vehicle vehicle)
	{
		Showing = true;
		var c = Canvas.ForScreen();
		Controls.BeginFrame();
		var mode = Settings.Current.Mode;
		bool clean = mode == HudMode.Clean, full = mode == HudMode.Full;
		FleetClock.Update();
		c.VerticalGradient(c.Size.Y - Band, c.Size.Y, Clear, Shade);

		// Everything lines up on two rows: the rings' centre line and the labels' line.
		float labelsY = c.Size.Y - Margin - SmallSize / 2;
		float centreY = labelsY - SmallSize / 2 - 16 - Radius;

		Ring[] rings = [SpeedRing(t), AltitudeRing(t)];
		for (int i = 0; i < rings.Length; i++)
			DrawRing(c, new float2(Margin + Radius + i * (2 * Radius + RingGap), centreY), labelsY, rings[i]);
		float ringsRight = Margin + rings.Length * 2 * Radius + (rings.Length - 1) * RingGap;

		// Apoapsis and periapsis right of the rings.
		float orbitLeft = ringsRight + 36;
		if (!clean)
			DrawOrbit(c, orbitLeft, centreY, labelsY, t, vehicle);

		// The navball in the centre, its bottom on the same line as the rings' bottoms.
		float ballY = centreY + Radius - NavballRadius;
		var ball = Settings.Current.DebugNavballTumble ? Navball.Tumble(t.Navball) : t.Navball;
		Navball.Draw(c, new float2(c.Size.X / 2, ballY), NavballRadius, ball);
		if (!clean)
		{
			DrawAttitude(c, new float2(c.Size.X / 2, ballY), ball);
			DrawFrames(c, c.Size.X / 2, labelsY, t, vehicle);
		}

		float ballRight = c.Size.X / 2 + NavballRadius;

		// Time at the top centre: T+, universal time, the warp control.
		DrawTime(c, c.Size.X / 2, t, clean);
		// Under it, in every mode: a near crew deadline, and the way back to a vessel left.
		float underTime = TimeTop + ClockSize + (clean ? 16 : 72);
		float below = FleetClock.DrawUnderClock(c, c.Size.X / 2, underTime);
		float toastY = Math.Max(TimeTop + ClockSize + 104, below + 22);
		DrawModeToast(c, c.Size.X / 2, mode, toastY);
		// The game's alerts start under all that and the mode label's place.
		AlertsTop = c.Px(toastY + 24);

		// Staging at the top left, the autopilot at the top right, below the game's menu bar
		// (a window above ours: it would take their clicks).
		// (None of it in CLEAN mode; all of it open in FULL.)
		float panelsTop = c.Units(ImGui.GetFrameHeight()) + 24;
		if (clean)
			Staging.Reset();
		else
		{
			float leftColumn = Staging.Draw(c, Margin, panelsTop, vehicle, full);
			Crew.Draw(c, Margin, leftColumn, vehicle, t.Now, full);
			float rightColumn = Autopilot.Draw(c, c.Size.X - Margin, panelsTop, vehicle, full);

			// Cards that appear when they apply, under the autopilot: the next burn, the
			// target, and in map view the burn being edited.
			rightColumn = Burns.DrawControl(c, c.Size.X - Margin, rightColumn, vehicle, t.Now.Seconds());
			rightColumn = Rendezvous.Draw(c, c.Size.X - Margin, rightColumn, vehicle, t.Now);
			Burns.DrawEditor(c, c.Size.X - Margin, rightColumn, t.Now.Seconds());
		}

		var bars = new List<Bar> { ThrottleBar(t) };
		foreach (var p in t.Propellants.Take(MaxPropellantBars))
			bars.Add(new Bar(p.Name, $"{p.Fraction * 100:0}%", p.Fraction));
		float right = c.Size.X - Margin;
		for (int i = 0; i < bars.Count; i++)
			DrawBar(c, right, labelsY - (bars.Count - 1 - i) * BarRowGap, bars[i]);

		// The engine deck between the navball and the bars.
		if (!clean)
			DrawEngine(c, (ballRight + right - BarWidth) / 2, centreY, labelsY, t, vehicle);

		// Over everything: a staging drag in progress, a staging chip's caption.
		if (!clean)
			Staging.DrawOverlay(c);

		Controls.EndFrame();

		// The strip the band's items take, from the tallest (rings or the bar stack) down,
		// with a little air: the game's console messages fade in above it.
		float barsTop = labelsY - (bars.Count - 1) * BarRowGap - 16 - SmallSize;
		ReservedPixels = c.Px(c.Size.Y - Math.Min(Math.Min(centreY - Radius, ballY - NavballRadius), barsTop) + 20);
	}

	// ---- the orbit block: apoapsis and periapsis, time to each, warp to each ---------------

	static void DrawOrbit(Canvas c, float left, float centreY, float labelsY, Telemetry t, KSA.Vehicle v)
	{
		Controls.BeginRegion(c, "orbit", new float2(left, centreY - Radius), new float2(left + OrbitWidth, labelsY + SmallSize));
		OrbitRow(c, left, centreY - 40, "AP", t.Apoapsis, t.NextApoapsis, t, v);
		OrbitRow(c, left, centreY + 22, "PE", t.Periapsis, t.NextPeriapsis, t, v);
		Controls.EndRegion();
		c.Text(Fonts.Medium, SmallSize, new float2(left + OrbitWidth / 2, labelsY), Align.Center, Dim, "ORBIT", tracking: Tracking);
	}

	// "AP" over the height; under them the time to it and a WARP button.
	static void OrbitRow(Canvas c, float left, float y, string label, double? height, KSA.UniverseTime at, Telemetry t, KSA.Vehicle v)
	{
		c.Text(Fonts.Medium, SmallSize, new float2(left, y), Align.Left, Dim, label, tracking: Tracking);
		var (value, unit) = height is { } h ? SplitDistance(h) : ("—", "");
		float valueWidth = c.MeasureText(Fonts.SemiBold, 26, value, tabular: true);
		c.Text(Fonts.SemiBold, 26, new float2(left + 38, y), Align.Left, height != null ? White : Faint, value, tabular: true);
		c.Text(Fonts.Medium, SmallSize - 2, new float2(left + 44 + valueWidth, y + 3), Align.Left, Dim, unit, tracking: 1.2f);

		double seconds = at.Seconds() - t.Now.Seconds();
		bool exists = height != null && double.IsFinite(seconds) && seconds >= 0;
		c.Text(Fonts.Medium, SmallSize - 2, new float2(left + 38, y + 24), Align.Left, exists ? Dim : Faint,
			exists ? $"T- {FormatDuration(seconds)}" : "", tracking: 1, tabular: true);
		bool can = exists && GameActions.CanWarpTo(v, at);
		if (Controls.Button(c, $"WARP {label}", "WARP", new float2(left + OrbitWidth - 56, y + 13), new float2(left + OrbitWidth, y + 35),
				lit: GameActions.IsWarpingTo(at), disabled: !can))
			GameActions.ToggleWarpTo(at);
	}

	// ---- the engine deck: delta-v, TWR, next stage; ignite, shutdown, stage, RCS ----------

	static void DrawEngine(Canvas c, float centreX, float centreY, float labelsY, Telemetry t, KSA.Vehicle v)
	{
		// Readouts: three columns.
		(string Label, string Value)[] readouts =
		[
			("DELTA-V  M/S", t.TotalDeltaV > 0 ? $"{t.TotalDeltaV:N0}" : "—"),
			("TWR", t.ThrustToWeight > 0 ? $"{t.ThrustToWeight:0.0}" : "—"),
			("STAGE", t.NextStage >= 0 ? $"{t.NextStage}" : "—"),
		];
		const float column = 120;
		for (int i = 0; i < readouts.Length; i++)
		{
			float x = centreX + (i - 1) * column;
			c.Text(Fonts.Medium, SmallSize, new float2(x, centreY - 34), Align.Center, Dim, readouts[i].Label, tracking: Tracking);
			c.Text(Fonts.SemiBold, 30, new float2(x, centreY + 4), Align.Center, White, readouts[i].Value, tabular: true);
		}

		// On EVA the deck is the stock KittenFlightControl: control modes and trim.
		if (v is KSA.KittenEva)
		{
			DrawEvaDeck(c, centreX, labelsY, v);
			return;
		}

		// Buttons along the labels' line.
		const float width = 100;
		string[] labels = ["IGNITE", "SHUTDOWN", "STAGE", "RCS"];
		float rowWidth = Controls.RowWidth(labels.Length, width, ButtonGap);
		float left = centreX - rowWidth / 2;
		float top = labelsY - ButtonHeight / 2 - 4;
		Controls.BeginRegion(c, "engine", new float2(left, top), new float2(left + rowWidth, top + ButtonHeight));
		float x0 = left;
		float2 Min(int i) => new(x0 + i * (width + ButtonGap), top);
		float2 Max(int i) => Min(i) + new float2(width, ButtonHeight);

		var ignite = KSA.VehicleEngine.MainIgnite;
		var shutdown = KSA.VehicleEngine.MainShutdown;
		var rcs = KSA.FlightComputerRCSMode.Enabled;
		if (Controls.Button(c, "IGNITE", "IGNITE", Min(0), Max(0), GameActions.IsOn(v, ignite), GameActions.IsDisabled(v, ignite)))
			GameActions.Apply(v, ignite);
		if (Controls.Button(c, "SHUTDOWN", "SHUTDOWN", Min(1), Max(1), false, GameActions.IsDisabled(v, shutdown)))
			GameActions.Apply(v, shutdown);
		if (Controls.Button(c, "STAGE", "STAGE", Min(2), Max(2), false, t.NextStage < 0))
			GameActions.ActivateNextStage(v);
		if (Controls.Button(c, "RCS", "RCS", Min(3), Max(3), GameActions.IsOn(v, rcs), GameActions.IsDisabled(v, rcs)))
			GameActions.Apply(v, rcs, toggle: true);
		Controls.EndRegion();
	}

	static void DrawEvaDeck(Canvas c, float centreX, float labelsY, KSA.Vehicle v)
	{
		(string Label, KSA.KittenEvaAction Value)[] buttons =
		[
			("DIRECT", KSA.KittenEvaAction.DirectMode), ("VIEW", KSA.KittenEvaAction.ViewMode),
			("RCS", KSA.KittenEvaAction.Rcs), ("MANUAL", KSA.KittenEvaAction.ManualTrim), ("RATE", KSA.KittenEvaAction.RateTrim),
		];
		const float width = 84;
		float rowWidth = Controls.RowWidth(buttons.Length, width, ButtonGap);
		float left = centreX - rowWidth / 2, top = labelsY - ButtonHeight / 2 - 4;
		Controls.BeginRegion(c, "eva", new float2(left, top), new float2(left + rowWidth, top + ButtonHeight));
		for (int i = 0; i < buttons.Length; i++)
		{
			var (label, value) = buttons[i];
			var min = new float2(left + i * (width + ButtonGap), top);
			if (Controls.Button(c, $"EVA {label}", label, min, min + new float2(width, ButtonHeight),
					GameActions.IsOn(v, value), GameActions.IsDisabled(v, value)))
				GameActions.Apply(v, value);
		}
		Controls.EndRegion();
	}

	// ---- formatting -----------------------------------------------------------------------

	public static string FormatLength(double metres)
	{
		var (value, unit) = SplitDistance(metres);
		return $"{value} {unit}";
	}

	public static string FormatCountdown(double seconds) =>
		!double.IsFinite(seconds) ? "—" : (seconds < 0 ? "T+" : "T-") + FormatDuration(Math.Abs(seconds));

	static (string Value, string Unit) SplitDistance(double metres) => metres switch
	{
		< 10_000 => ($"{metres:0}", "M"),
		< 10_000_000 => ($"{metres / 1000:0.0}", "KM"),
		< 1e12 => ($"{metres / 1000:N0}", "KM"),
		_ => ($"{metres / 1.495978707e11:0.00}", "AU"),
	};

	static string FormatDuration(double seconds)
	{
		var s = TimeSpan.FromSeconds(Math.Min(Math.Max(seconds, 0), TimeSpan.MaxValue.TotalSeconds - 1));
		return s.TotalDays >= 1 ? $"{(int)s.TotalDays}D {s.Hours:00}:{s.Minutes:00}:{s.Seconds:00}" : $"{s.Hours:00}:{s.Minutes:00}:{s.Seconds:00}";
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
	// Time, at the top centre of the screen on a soft shade: "T+ 00:01:23", universal time
	// and the vehicle's name under it, then the warp control (slower, the speed, faster; the
	// speed itself goes back to real time).
	const float TimeTop = 58;
	const float TimeShade = 210;

	static void DrawTime(Canvas c, float centreX, Telemetry t, bool clockOnly)
	{
		c.VerticalGradient(0, TimeShade, ShadeTop, Clear);

		float clockY = TimeTop + ClockSize / 2;
		var s = TimeSpan.FromSeconds(Math.Max(t.MissionSeconds, 0));
		string digits = $"{(int)s.TotalHours:00}:{s.Minutes:00}:{s.Seconds:00}";
		const string prefix = "T+";
		const float prefixSize = 26, space = 12;
		float prefixWidth = c.MeasureText(Fonts.Medium, prefixSize, prefix, tracking: 1);
		float digitsWidth = c.MeasureText(Fonts.SemiBold, ClockSize, digits, tabular: true);
		float left = centreX - (prefixWidth + space + digitsWidth) / 2;
		var colour = t.BeforeLiftoff ? Faint : White;
		c.Text(Fonts.Medium, prefixSize, new float2(left, clockY), Align.Left, t.BeforeLiftoff ? Faint : Dim, prefix, tracking: 1);
		c.Text(Fonts.SemiBold, ClockSize, new float2(left + prefixWidth + space, clockY), Align.Left, colour, digits, tabular: true);

		if (clockOnly)
			return;

		// Universal time as the stock clock counts it (years, days of 365, h:m:s), and the vehicle.
		var now = t.Now;
		string ut = $"UT {(int)now.Years}Y {(int)now.Days % 365:000}D {(int)now.Hours % 24:00}:{(int)now.Minutes % 60:00}:{(int)now.Seconds() % 60:00}";
		float lineY = clockY + ClockSize / 2 + 16;
		c.Text(Fonts.Medium, SmallSize, new float2(centreX - 12, lineY), Align.Right, Dim, ut, tracking: 1.2f, tabular: true);
		c.Text(Fonts.Medium, SmallSize, new float2(centreX + 12, lineY), Align.Left, Dim, t.VehicleId.ToUpperInvariant(), tracking: Tracking);

		// Warp: [-] [speed] [+]
		const float side = 32, middle = 110, gap = 4;
		float rowWidth = 2 * side + middle + 2 * gap;
		float rowLeft = centreX - rowWidth / 2, top = lineY + 18;
		Controls.BeginRegion(c, "warp", new float2(rowLeft, top), new float2(rowLeft + rowWidth, top + ButtonHeight));
		if (Controls.Button(c, "WARP-", "-", new float2(rowLeft, top), new float2(rowLeft + side, top + ButtonHeight), disabled: t.SimSpeed <= 0))
			GameActions.WarpSlower();
		float mid = rowLeft + side + gap;
		string speed = t.AutoWarp ? $"AUTO ×{FormatSpeed(t.SimSpeed)}" : t.SimSpeed <= 0 ? "PAUSED" : $"×{FormatSpeed(t.SimSpeed)}";
		if (Controls.Button(c, "WARP1", speed, new float2(mid, top), new float2(mid + middle, top + ButtonHeight), lit: t.SimSpeed != 1 || t.AutoWarp))
			GameActions.WarpRealtime();
		float plus = mid + middle + gap;
		if (Controls.Button(c, "WARP+", "+", new float2(plus, top), new float2(plus + side, top + ButtonHeight)))
			GameActions.WarpFaster();
		Controls.EndRegion();
	}

	// ---- modes --------------------------------------------------------------------------------

	static long _modeChangedAt = long.MinValue;

	/// <summary>The next mode (the P key, and the Mods menu): CLEAN, FLIGHT, FULL, CLEAN...</summary>
	public static void SetMode(HudMode mode)
	{
		Settings.Current.Mode = mode;
		Settings.Current.Save();
		_modeChangedAt = Environment.TickCount64;
	}

	public static void CycleMode() => SetMode(Settings.Current.Mode switch
	{
		HudMode.Clean => HudMode.Flight,
		HudMode.Flight => HudMode.Full,
		_ => HudMode.Clean,
	});

	// "FLIGHT MODE" under the clock for a moment after a change, fading out.
	static void DrawModeToast(Canvas c, float centreX, HudMode mode, float y)
	{
		double age = (Environment.TickCount64 - _modeChangedAt) / 1000.0;
		if (age is < 0 or > 1.8)
			return;
		byte alpha = (byte)(255 * Math.Clamp((1.8 - age) / 0.5, 0, 1));
		string text = $"{mode.ToString().ToUpperInvariant()} MODE";
		float w = c.MeasureText(Fonts.SemiBold, SmallSize + 1, text, tracking: 3);
		c.FillRect(new float2(centreX - w / 2 - 14, y - 16), new float2(centreX + w / 2 + 14, y + 16), Theme.Ink((byte)(alpha * 0.9)), 4);
		c.Text(Fonts.SemiBold, SmallSize + 1, new float2(centreX, y), Align.Center, new ImColor8(0, 0, 0, alpha), text, tracking: 3, shadow: false);
	}

	static string FormatSpeed(double speed) => speed switch
	{
		< 1 => $"{speed:0.0}",
		< 10_000 => $"{speed:0}",
		< 10_000_000 => $"{speed / 1000:0}K",
		_ => $"{speed / 1_000_000:0}M",
	};

	// Heading above the ball, pitch left of it, roll right of it, like an instrument's
	// scales, each with its rate of turn; the speed in the ball's frame under the pitch.
	static void DrawAttitude(Canvas c, float2 ball, NavballState s)
	{
		var (heading, pitch, roll) = Navball.Attitude(s);
		float r = NavballRadius;
		var rates = s.RatesDeg;
		c.Text(Fonts.SemiBold, SmallSize, ball + new float2(0, -r - 14), Align.Center, White, $"{heading:000}", tabular: true);
		c.Text(Fonts.Medium, SmallSize - 4, ball + new float2(34, -r - 14), Align.Left, Dim, Rate(rates.Z), tabular: true);

		c.Text(Fonts.Medium, SmallSize - 3, ball + new float2(-r - 12, -30), Align.Right, Dim, "PITCH", tracking: 1.4f);
		c.Text(Fonts.SemiBold, SmallSize, ball + new float2(-r - 12, -10), Align.Right, White, $"{pitch:0}", tabular: true);
		c.Text(Fonts.Medium, SmallSize - 4, ball + new float2(-r - 12, 8), Align.Right, Dim, Rate(rates.Y), tabular: true);
		c.Text(Fonts.Medium, SmallSize - 3, ball + new float2(-r - 12, 34), Align.Right, Dim, $"{Autopilot.FrameName(s.Frame)} M/S", tracking: 1.4f);
		c.Text(Fonts.SemiBold, SmallSize, ball + new float2(-r - 12, 54), Align.Right, White, s.FrameSpeed < 100 ? $"{s.FrameSpeed:N1}" : $"{s.FrameSpeed:N0}", tabular: true);

		c.Text(Fonts.Medium, SmallSize - 3, ball + new float2(r + 12, -30), Align.Left, Dim, "ROLL", tracking: 1.4f);
		c.Text(Fonts.SemiBold, SmallSize, ball + new float2(r + 12, -10), Align.Left, White, $"{roll:0}", tabular: true);
		c.Text(Fonts.Medium, SmallSize - 4, ball + new float2(r + 12, 8), Align.Left, Dim, Rate(rates.X), tabular: true);
	}

	// A turn rate: "+0.4°/S"; blank when still.
	static string Rate(double degPerSecond) => Math.Abs(degPerSecond) < 0.05 ? "0.0°/S" : $"{degPerSecond:+0.0;-0.0}°/S";

	// The navball's reference frame, as the stock navball's buttons: STAR SURF ORBT OVEL TGT
	// BURN, the current one lit; TGT needs a target and BURN a planned burn.
	static readonly (string Label, KSA.VehicleReferenceFrame Frame)[] Frames =
	[
		("STAR", KSA.VehicleReferenceFrame.EclBody), ("SURF", KSA.VehicleReferenceFrame.EnuBody),
		("ORBT", KSA.VehicleReferenceFrame.Lvlh), ("OVEL", KSA.VehicleReferenceFrame.VlfBody),
		("TGT", KSA.VehicleReferenceFrame.Dock), ("BURN", KSA.VehicleReferenceFrame.BurnBody),
	];

	static void DrawFrames(Canvas c, float centreX, float labelsY, Telemetry t, KSA.Vehicle v)
	{
		const float width = 42, gap = 3;
		float rowWidth = Controls.RowWidth(Frames.Length, width, gap);
		float left = centreX - rowWidth / 2, top = labelsY - ButtonHeight / 2 - 4;
		Controls.BeginRegion(c, "frames", new float2(left, top), new float2(left + rowWidth, top + ButtonHeight));
		for (int i = 0; i < Frames.Length; i++)
		{
			var (label, frame) = Frames[i];
			bool disabled = frame switch
			{
				KSA.VehicleReferenceFrame.Dock => v.Target == null,
				KSA.VehicleReferenceFrame.BurnBody => !v.FlightComputer.BurnPlan.HasActiveBurns,
				_ => false,
			};
			float x = left + i * (width + gap);
			if (Controls.Button(c, $"FRAME {label}", label, new float2(x, top), new float2(x + width, top + ButtonHeight),
					lit: t.Navball.Frame == frame, disabled: disabled))
				v.SetNavBallFrame(frame);
		}
		Controls.EndRegion();
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
