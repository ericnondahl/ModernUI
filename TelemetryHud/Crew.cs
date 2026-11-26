using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace TelemetryHud;

/// <summary>
/// The stock CrewPortraits, Radiation and Resources (power, life support) gauges as one
/// card in the HUD's style, under staging. Each kitten is a wireframe cat in the HUD's
/// line style (the stock gauge shows live face cameras): its name, its acute and lifetime
/// radiation doses, and its face for its state - eyes open (and blinking) when well,
/// squinting when sick or short of air, water or food, crossed out when lost. Small
/// differences from the kitten's name (ears, eyes, stripes) tell the crew apart.
/// Under them: battery charge, the life support forecast, and the dose rate here and
/// averaged over the orbit, as the stock gauges work them out.
/// </summary>
public static class Crew
{
	const float Width = 440;
	const float Pad = 10;
	const float Cell = 108;
	const float FaceSize = 64;
	const float SmallSize = 14;
	const int MaxCrew = 4;

	static readonly ImColor8 White = new(255, 255, 255, 255);
	static readonly ImColor8 Line = new(255, 255, 255, 220);
	static readonly ImColor8 Dim = new(255, 255, 255, 166);
	static readonly ImColor8 Faint = new(255, 255, 255, 70);
	static readonly ImColor8 Backing = new(0, 0, 0, 120);

	// The stock dose gauge's working state (Program.UpdateDoseGauges keeps its own).
	static readonly List<DoseTerm> DoseTerms = [];
	static readonly OrbitDoseAverageCache OrbitDoseCache = new();

	enum Face { Well, Unwell, Lost }

	public static float Draw(Canvas c, float left, float top, Vehicle v, UniverseTime now)
	{
		var kittens = new List<KittenRosterEntryData>();
		foreach (var seat in v.Crew)
			if (seat.AssignedKittenHash != KeyHash.Zero && Universe.KittenRoster.Find(seat.AssignedKittenHash) is { } k)
				kittens.Add(k);
		if (v is KittenEva eva && Universe.KittenRoster.Find(eva.Id) is { } self)
			kittens.Add(self);

		bool lifeSupport = Universe.Rules.LifeSupport;
		var forecast = v.LifeSupportForecast;
		int crew = Math.Min(kittens.Count, MaxCrew);
		float facesHeight = crew > 0 ? FaceSize + 52 : 0;
		const float systemsHeight = 2 * 44;
		float height = 28 + facesHeight + systemsHeight;
		float right = left + Width;
		c.FillRect(new float2(left - Pad, top - Pad), new float2(right + Pad, top + height + Pad), Backing, 6);

		c.Text(Fonts.Medium, SmallSize + 1, new float2(left, top + 14), Align.Left, Dim, "CREW", tracking: 2.4f);
		c.Text(Fonts.SemiBold, SmallSize + 1, new float2(left + 70, top + 14), Align.Left, White, $"{kittens.Count}", tabular: true);

		// The cats.
		float y = top + 28;
		bool worried = lifeSupport && forecast.Kittens > 0 && forecast.Level != LifeSupportLevel.Ok;
		for (int i = 0; i < crew; i++)
		{
			var k = kittens[i];
			var face = k.Kia ? Face.Lost : k.HasRadiationSickness(now) || worried ? Face.Unwell : Face.Well;
			float cx = left + Cell / 2 + i * Cell;
			DrawCat(c, new float2(cx, y + FaceSize / 2 + 4), FaceSize, k.Name, face);
			string name = k.Name.ToUpperInvariant();
			if (name.Length > 12)
				name = name[..12];
			c.Text(Fonts.SemiBold, SmallSize - 1, new float2(cx, y + FaceSize + 16), Align.Center, k.Kia ? Faint : White, name, tracking: 1.2f);
			string doses = $"{k.GetAcuteDose(now).Value() * 1000:0.##} / {k.LifetimeDose.Value() * 1000:0.##} MSV";
			c.Text(Fonts.Medium, SmallSize - 3, new float2(cx, y + FaceSize + 34), Align.Center, Dim, k.Kia ? "LOST" : doses, tracking: 0.8f, tabular: true);
		}
		y += facesHeight;

		// Systems: power, life support, radiation.
		var systems = new List<(string Label, string Value, bool Alert)> { ("POWER", Power(v), false) };
		if (lifeSupport && forecast.Kittens > 0)
		{
			systems.Add(("AIR", Remaining(forecast.AirRemaining, now), forecast.GetSupplyLevel(forecast.AirRemaining) != LifeSupportLevel.Ok));
			systems.Add(("WATER", Remaining(forecast.WaterRemaining, now), forecast.GetSupplyLevel(forecast.WaterRemaining) != LifeSupportLevel.Ok));
			systems.Add(("FOOD", Remaining(forecast.FoodRemaining, now), forecast.GetSupplyLevel(forecast.FoodRemaining) != LifeSupportLevel.Ok));
			systems.Add(("CO2", Remaining(forecast.ScrubberRemaining, now), forecast.GetSupplyLevel(forecast.ScrubberRemaining) != LifeSupportLevel.Ok));
		}
		if (Universe.Rules.Radiation)
		{
			var (here, orbit) = DoseRates(v);
			systems.Add(("DOSE MSV/H", here, false));
			systems.Add(("ORBIT AVG", orbit, false));
		}
		const int columns = 4;
		float colWidth = Width / columns;
		for (int i = 0; i < systems.Count; i++)
		{
			float x = left + (i % columns) * colWidth;
			float ry = y + (i / columns) * 44;
			var (label, value, alert) = systems[i];
			c.Text(Fonts.Medium, SmallSize - 3, new float2(x, ry + 10), Align.Left, alert ? White : Dim, label, tracking: 1.6f);
			if (alert)
				c.FillRect(new float2(x - 4, ry + 20), new float2(x + colWidth - 12, ry + 40), new ImColor8(255, 255, 255, 230), 3);
			c.Text(Fonts.SemiBold, SmallSize + 2, new float2(x, ry + 30), Align.Left, alert ? new ImColor8(0, 0, 0, 255) : White, value, tabular: true, shadow: !alert);
		}
		return top + height + 2 * Pad + 8;
	}

	// ---- the cats ---------------------------------------------------------------------------

	/// <summary>A cat's head in lines, `size` across, varied by its name.</summary>
	static void DrawCat(Canvas c, float2 at, float size, string name, Face face)
	{
		float s = size / 64f;
		uint seed = StableHash(name);
		float earTilt = ((int)(seed % 7) - 3) * 0.9f; // signed: uint arithmetic would wrap
		float eyeSpread = 7.5f + seed / 7 % 3 * 0.8f;
		int stripes = (int)(seed / 21 % 4);
		float2 P(float x, float y) => at + new float2(x, y) * s;
		float stroke = 1.6f;

		// Helmet.
		c.Circle(at, 31 * s, new ImColor8(255, 255, 255, 60), 1.2f);

		// Head: a slightly wide oval, a little low in the helmet.
		var head = new float2[36];
		for (int i = 0; i < head.Length; i++)
		{
			double a = 2 * Math.PI * i / head.Length;
			head[i] = P((float)Math.Cos(a) * 21, 4 + (float)Math.Sin(a) * 17);
		}
		c.Polygon(head, Line, stroke);

		// Ears: from the top of the head, tilted per cat; a line inside each.
		foreach (float side in (float[])[-1, 1])
		{
			var baseOut = P(side * 19, -4);
			var tip = P(side * (16 + earTilt), -24);
			var baseIn = P(side * 6, -12);
			c.Line(baseOut, tip, Line, stroke);
			c.Line(tip, baseIn, Line, stroke);
			c.Line(P(side * (15 + earTilt * 0.6f), -18), P(side * 11, -11), Faint, 1.1f);
		}

		// Forehead stripes.
		for (int i = 0; i < stripes; i++)
		{
			float x = (i - (stripes - 1) / 2f) * 4;
			c.Line(P(x, -12), P(x, -7), Faint, 1.2f);
		}

		// Eyes, by state; a blink every few seconds, out of step between cats.
		double t = Environment.TickCount64 / 1000.0 + seed % 1000 / 250.0;
		bool blink = face == Face.Well && t % 4.7 < 0.13;
		foreach (float side in (float[])[-1, 1])
		{
			var eye = P(side * eyeSpread, 1);
			switch (face)
			{
				case Face.Well when !blink:
					c.FillCircle(eye, 2.4f * s, White);
					break;
				case Face.Well:
					c.Line(eye - new float2(2.8f, 0) * s, eye + new float2(2.8f, 0) * s, Line, 1.4f);
					break;
				case Face.Unwell:
					// Squinting: > <
					c.Line(eye + new float2(-side * 3, -2.5f) * s, eye + new float2(side * 2, 0) * s, Line, 1.4f);
					c.Line(eye + new float2(side * 2, 0) * s, eye + new float2(-side * 3, 2.5f) * s, Line, 1.4f);
					break;
				case Face.Lost:
					c.Line(eye - new float2(3, 3) * s, eye + new float2(3, 3) * s, Line, 1.4f);
					c.Line(eye + new float2(-3, 3) * s, eye + new float2(3, -3) * s, Line, 1.4f);
					break;
			}
		}

		// Nose, mouth, whiskers.
		c.FillTriangle(P(-1.8f, 7.5f), P(1.8f, 7.5f), P(0, 9.6f), White);
		c.Line(P(0, 9.6f), P(-2.2f, 12), Line, 1.1f);
		c.Line(P(0, 9.6f), P(2.2f, 12), Line, 1.1f);
		foreach (float side in (float[])[-1, 1])
		{
			c.Line(P(side * 11, 9), P(side * 23, 7), Faint, 1f);
			c.Line(P(side * 11, 11), P(side * 23, 12), Faint, 1f);
		}
	}

	// FNV-1a: the same kitten looks the same every session (string.GetHashCode is
	// randomised per process).
	static uint StableHash(string s)
	{
		uint h = 2166136261;
		foreach (char ch in s)
			h = (h ^ ch) * 16777619;
		return h;
	}

	// ---- systems ----------------------------------------------------------------------------

	// Battery charge, as ResourcesPanel sums it.
	static string Power(Vehicle v)
	{
		double charge = 0, capacity = 0;
		var batteries = v.Parts.Batteries.ModulesAndStates.GetEnumerator();
		while (batteries.MoveNext())
		{
			charge += batteries.Current.State.Charge.Value();
			capacity += batteries.Current.Module.MaximumCapacity.Value();
		}
		return capacity > 0 ? $"{charge / capacity * 100:0}%" : "—";
	}

	// Time left of a supply: "3.0D", "5.2H", "12M".
	static string Remaining(UniverseTime remaining, UniverseTime now)
	{
		double s = remaining.Seconds();
		if (!double.IsFinite(s) || s > 1e12)
			return "∞";
		return s >= 2 * 86400 ? $"{s / 86400:0.0}D" : s >= 3600 ? $"{s / 3600:0.0}H" : s > 0 ? $"{s / 60:0}M" : "OUT";
	}

	// The dose rate here and averaged over the orbit, as Program.UpdateDoseGauges (mSv/h).
	static (string Here, string Orbit) DoseRates(Vehicle v)
	{
		var parent = v.Parent;
		var positionEcl = v.GetPositionEcl();
		double seconds = Universe.GetElapsedSeconds();
		var rates = Radiation.ComputeDoseRates(parent, v.GetPositionCci(), positionEcl);
		var here = rates.Total + ParticleEvents.ComputeDoseRate(positionEcl, parent, rates.StarExposure, seconds, DoseTerms);
		var orbit = Radiation.ComputeOrbitDoseRates(v, OrbitDoseCache);
		string orbitText = orbit.Status == OrbitDoseStatus.Averaged
			? MilliSievertsPerHour(orbit.Rates.Total + ParticleEvents.ComputeDoseRate(positionEcl, null, orbit.Rates.StarExposure, seconds, DoseTerms))
			: "—";
		return (MilliSievertsPerHour(here), orbitText);
	}

	static string MilliSievertsPerHour(SievertsPerSecond rate)
	{
		double mSvPerHour = rate.Value() * 3.6e6;
		return mSvPerHour < 10 ? $"{mSvPerHour:0.00}" : $"{mSvPerHour:N0}";
	}
}
