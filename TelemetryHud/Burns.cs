using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace TelemetryHud;

/// <summary>
/// The stock BurnControl and Burn (editor) gauges in the HUD's style.
///
/// Burn control, whenever a burn is planned: delta-v to go, time to the burn, ignition and
/// duration, and AUTO (execute it), WARP (to it), DELETE, POS/NEG (point along or against
/// it), HOLD (still in the burn's frame) - as the stock buttons.
///
/// Burn editor, in map view for the burn being edited (Program.ActiveBurn): its prograde,
/// normal and radial delta-v, its time and whole orbits, each nudged by the step (with
/// Shift ten steps, with Ctrl a tenth), the step itself, DELETE, TRANSFER (a departure
/// burn), CLOSE - as GaugeButtonBurn does them.
/// </summary>
public static class Burns
{
	const float Width = 364;
	const float RowHeight = 28;
	const float Gap = 5;
	const float SmallSize = 15;
	const float Pad = 10;

	static readonly ImColor8 White = new(255, 255, 255, 255);
	static readonly ImColor8 Dim = new(255, 255, 255, 166);
	static readonly ImColor8 Faint = new(255, 255, 255, 80);
	static readonly ImColor8 Backing = new(0, 0, 0, 120);

	// ---- burn control ------------------------------------------------------------------

	/// <summary>Draws the card with its top right at (right, top) if a burn is planned; returns its bottom.</summary>
	public static float DrawControl(Canvas c, float right, float top, Vehicle v, double now)
	{
		if (!v.BurnPlan.HasActiveBurns)
			return top;
		var burn = v.FlightComputer.Burn;
		float left = right - Width;
		float height = RowHeight * 3 + Gap * 2 + 8 + RowHeight * 2 + Gap;
		Controls.BeginRegion(c, "burncontrol", new float2(left, top), new float2(right, top + height));
		c.FillRect(new float2(left - Pad, top - Pad), new float2(right + Pad, top + height + Pad), Backing, 6);

		// Delta-v to go, big; when, small.
		float hy = top + RowHeight / 2;
		c.Text(Fonts.Medium, SmallSize, new float2(left, hy), Align.Left, Dim, "NEXT BURN", tracking: 2.4f);
		string dv = burn != null ? $"{burn.DeltaVToGoCci.Length():N1}" : "—";
		c.Text(Fonts.SemiBold, 22, new float2(right - 40, hy), Align.Right, White, dv, tabular: true);
		c.Text(Fonts.Medium, SmallSize - 3, new float2(right, hy + 2), Align.Right, Dim, "M/S", tracking: 1.2f);

		(string Label, string Value)[] times =
		[
			("NODE IN", burn != null ? Countdown(burn.ImpulsiveInstant.Seconds() - now) : "—"),
			("IGNITION IN", burn != null ? Countdown(burn.IgnitionTime.Seconds() - now) : "—"),
			("DURATION", burn != null ? Duration(burn.BurnDuration) : "—"),
		];
		float colWidth = Width / 3;
		for (int i = 0; i < times.Length; i++)
		{
			float x = left + i * colWidth;
			c.Text(Fonts.Medium, SmallSize - 3, new float2(x, top + RowHeight + 10), Align.Left, Dim, times[i].Label, tracking: 1.6f);
			c.Text(Fonts.SemiBold, SmallSize + 1, new float2(x, top + RowHeight + 30), Align.Left, White, times[i].Value, tabular: true);
		}

		// AUTO WARP DELETE / POS NEG HOLD
		(string Label, Enum Value, bool Toggle)[] buttons =
		[
			("AUTO", FlightComputerBurnMode.Auto, true),
			("WARP", FlightComputerAction.WarpToNextBurn, true),
			("DELETE", FlightComputerAction.DeleteNextBurn, false),
			("POS", FlightComputerAttitudeTrackTarget.PositiveDv, false),
			("NEG", FlightComputerAttitudeTrackTarget.NegativeDv, false),
			("HOLD", VehicleReferenceFrame.BurnBody, false),
		];
		float bw = (Width - 2 * Gap) / 3;
		float by = top + RowHeight * 2 + Gap * 2 + 8;
		for (int i = 0; i < buttons.Length; i++)
		{
			var (label, value, toggle) = buttons[i];
			var min = new float2(left + (i % 3) * (bw + Gap), by + (i / 3) * (RowHeight + Gap));
			if (Controls.Button(c, $"BURN {label}", label, min, min + new float2(bw, RowHeight),
					GameActions.IsOn(v, value), GameActions.IsDisabled(v, value)))
				GameActions.Apply(v, value, toggle);
		}
		Controls.EndRegion();
		return top + height + 2 * Pad + 8;
	}

	// ---- burn editor ---------------------------------------------------------------------

	enum Edit { ProgradeMinus, ProgradePlus, NormalMinus, NormalPlus, OutwardMinus, OutwardPlus, TimeMinus, TimePlus, PeriodMinus, PeriodPlus, StepMinus, StepPlus }

	/// <summary>
	/// In map view, the card for the burn being edited, top right at (right, top). (The left
	/// side is the game's map view panel's.)
	/// </summary>
	public static void DrawEditor(Canvas c, float right, float top, double now)
	{
		var b = Program.ActiveBurn;
		if (b == null || Program.GetCameraMode() != CameraMode.Map)
			return;
		var fc = b.Vehicle.FlightComputer;
		float left = right - Width;
		const int rows = 6;
		float height = RowHeight + 8 + rows * (RowHeight + Gap) + 4 + RowHeight;
		Controls.BeginRegion(c, "burneditor", new float2(left, top), new float2(right, top + height));
		c.FillRect(new float2(left - Pad, top - Pad), new float2(right + Pad, top + height + Pad), Backing, 6);

		var time = b.ParentDepartureBurn ? b.ParentDepartureTime : b.Time;
		float hy = top + RowHeight / 2;
		c.Text(Fonts.Medium, SmallSize, new float2(left, hy), Align.Left, Dim, "EDIT BURN", tracking: 2.4f);
		c.Text(Fonts.SemiBold, 22, new float2(right - 40, hy), Align.Right, White, $"{b.DeltaVVlf.Length():N1}", tabular: true);
		c.Text(Fonts.Medium, SmallSize - 3, new float2(right, hy + 2), Align.Right, Dim, "M/S", tracking: 1.2f);

		// Shift: ten steps; Ctrl: a tenth of one (the stock buttons' modifiers).
		var io = ImGui.GetIO();
		double multiplier = io.KeyShift ? 10 : io.KeyCtrl ? 0.1 : 1;
		var dv = b.ParentDepartureBurn ? b.ParentDepartureVelocityVlf : b.DeltaVVlf;
		float y = top + RowHeight + 8;
		Row(c, left, ref y, "PROGRADE", $"{dv.X:N1} M/S", Edit.ProgradeMinus, Edit.ProgradePlus, b, fc, multiplier);
		Row(c, left, ref y, "NORMAL", $"{dv.Y:N1} M/S", Edit.NormalMinus, Edit.NormalPlus, b, fc, multiplier);
		Row(c, left, ref y, "RADIAL", $"{dv.Z:N1} M/S", Edit.OutwardMinus, Edit.OutwardPlus, b, fc, multiplier);
		Row(c, left, ref y, "TIME", Countdown(time.Seconds() - now), Edit.TimeMinus, Edit.TimePlus, b, fc, multiplier);
		Row(c, left, ref y, "ORBITS", b.HasPeriod ? Duration(b.Patch.Orbit.Period) : "—", Edit.PeriodMinus, Edit.PeriodPlus, b, fc, multiplier, disabled: !b.HasPeriod);
		Row(c, left, ref y, "STEP", $"{Burn.Multiplier:0.#}", Edit.StepMinus, Edit.StepPlus, b, fc, multiplier);

		// DELETE TRANSFER CLOSE
		y += 4;
		float bw = (Width - 2 * Gap) / 3;
		if (Controls.Button(c, "EDIT DELETE", "DELETE", new float2(left, y), new float2(left + bw, y + RowHeight)))
			InputEvents.BurnUpdateBuffer.Add(new InputEvents.BurnUpdateData { Burn = b, FlightComputer = fc, DeleteBurn = true });
		if (Controls.Button(c, "EDIT TRANSFER", "TRANSFER", new float2(left + bw + Gap, y), new float2(left + 2 * bw + Gap, y + RowHeight),
				disabled: !b.ParentDepartureBurn))
			b.MoveClicked(b.Vehicle);
		if (Controls.Button(c, "EDIT CLOSE", "CLOSE", new float2(right - bw, y), new float2(right, y + RowHeight)))
			Program.ActiveBurn = null;
		Controls.EndRegion();
	}

	// "PROGRADE   12.0 M/S   [-] [+]"
	static void Row(Canvas c, float left, ref float y, string label, string value, Edit minus, Edit plus, Burn b, FlightComputer fc, double multiplier, bool disabled = false)
	{
		float cy = y + RowHeight / 2;
		c.Text(Fonts.Medium, SmallSize - 2, new float2(left, cy), Align.Left, disabled ? Faint : Dim, label, tracking: 2f);
		c.Text(Fonts.SemiBold, SmallSize + 1, new float2(left + Width - 2 * 44 - 12, cy), Align.Right, disabled ? Faint : White, value, tabular: true);
		var minusMin = new float2(left + Width - 2 * 44 - Gap, y);
		if (Controls.Button(c, $"EDIT {label} -", "-", minusMin, minusMin + new float2(44, RowHeight), disabled: disabled))
			Apply(minus, b, fc, multiplier);
		var plusMin = new float2(left + Width - 44, y);
		if (Controls.Button(c, $"EDIT {label} +", "+", plusMin, plusMin + new float2(44, RowHeight), disabled: disabled))
			Apply(plus, b, fc, multiplier);
		y += RowHeight + Gap;
	}

	// As GaugeButtonBurn.OnActivated.
	static void Apply(Edit edit, Burn b, FlightComputer fc, double multiplier)
	{
		double step = Burn.Multiplier * multiplier;
		switch (edit)
		{
			case Edit.ProgradeMinus: AddDeltaV(b, fc, -step, 0, 0); break;
			case Edit.ProgradePlus: AddDeltaV(b, fc, step, 0, 0); break;
			case Edit.NormalMinus: AddDeltaV(b, fc, 0, -step, 0); break;
			case Edit.NormalPlus: AddDeltaV(b, fc, 0, step, 0); break;
			case Edit.OutwardMinus: AddDeltaV(b, fc, 0, 0, -step); break;
			case Edit.OutwardPlus: AddDeltaV(b, fc, 0, 0, step); break;
			case Edit.TimeMinus:
			case Edit.TimePlus:
				b.Time = new UniverseTime(b.Time.Seconds() + (edit == Edit.TimePlus ? step : -step));
				b.Update(fc);
				break;
			case Edit.PeriodMinus:
				for (int i = 0; i < Math.Max(1, (int)multiplier) && b.MinusClicked(fc); i++) { }
				break;
			case Edit.PeriodPlus:
				for (int i = 0; i < Math.Max(1, (int)multiplier) && b.PlusClicked(fc); i++) { }
				break;
			case Edit.StepMinus:
				Burn.Multiplier = multiplier > 1 ? PreviousOrderOfMagnitude(Burn.Multiplier) : StepByOrderOfMagnitude(Burn.Multiplier, -1);
				break;
			case Edit.StepPlus:
				Burn.Multiplier = multiplier > 1 ? NextOrderOfMagnitude(Burn.Multiplier) : StepByOrderOfMagnitude(Burn.Multiplier, 1);
				break;
		}
	}

	static void AddDeltaV(Burn b, FlightComputer fc, double x, double y, double z)
	{
		if (b.ParentDepartureBurn)
			b.SetParentDepartureVelocityVlf(b.ParentDepartureVelocityVlf + new double3(x, y, z));
		else
		{
			b.DeltaVVlf += new double3(x, y, z);
			b.Update(fc);
		}
	}

	// The stock step rules (GaugeButtonBurn): the leading digit up or down, or (Shift) a
	// whole power of ten; never below 0.1.
	static double NextOrderOfMagnitude(double value) =>
		Math.Pow(10.0, Math.Floor(Math.Log10(Math.Max(Math.Abs(value), 0.1)) + 1E-09) + 1.0);

	static double PreviousOrderOfMagnitude(double value) =>
		Math.Max(Math.Pow(10.0, Math.Ceiling(Math.Log10(Math.Max(Math.Abs(value), 0.1)) - 1E-09) - 1.0), 0.1);

	static double StepByOrderOfMagnitude(double value, int sign)
	{
		double v = Math.Max(Math.Abs(value), 0.1);
		double magnitude = Math.Pow(10.0, Math.Floor(Math.Log10(v) + 1E-09));
		double digit = sign > 0 ? Math.Floor(v / magnitude + 1E-09) + 1.0 : Math.Ceiling(v / magnitude - 1E-09) - 1.0;
		return digit <= 0.0 ? Math.Max(magnitude * 0.9, 0.1) : Math.Max(Math.Round(digit * magnitude, 10), 0.1);
	}

	// ---- formatting ------------------------------------------------------------------------

	static string Countdown(double seconds)
	{
		if (!double.IsFinite(seconds))
			return "—";
		string sign = seconds < 0 ? "T+" : "T-";
		return $"{sign}{Duration(Math.Abs(seconds))}";
	}

	static string Duration(double seconds)
	{
		if (!double.IsFinite(seconds))
			return "—";
		var s = TimeSpan.FromSeconds(Math.Min(seconds, 1e9));
		return s.TotalDays >= 1 ? $"{(int)s.TotalDays}D {s.Hours:00}:{s.Minutes:00}:{s.Seconds:00}"
			: s.TotalHours >= 1 ? $"{(int)s.TotalHours}:{s.Minutes:00}:{s.Seconds:00}"
			: $"{s.Minutes:00}:{s.Seconds:00}";
	}
}
