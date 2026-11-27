using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace ModernUI;

/// <summary>
/// The game's crew clock (CrewClock: the soonest life support or radiation deadline of every
/// crewed vessel), in the HUD's style, in three places instead of one box always on screen:
/// - the crew line: the soonest deadline after ALL WELL;
/// - under the time: a tag only when a deadline is near (under 12 hours, or past "low"),
///   and BACK TO the vessel you switched away from (an EVA, say);
/// - the open crew card: every vessel's next deadline, each a click away.
/// Clicking a deadline does what the stock clock's rows do (CrewClock.Activate): your own
/// vessel's life support window, or a switch to that vessel. The game's own clock toggle
/// (Ctrl+F6) still turns it off.
/// </summary>
public static class FleetClock
{
	const double UrgentSeconds = 12 * 3600;
	const float SmallSize = 14;

	static readonly List<CrewEvent> Events = new(16);

	static readonly ImColor8 White = new(255, 255, 255, 255);
	static readonly ImColor8 Dim = new(255, 255, 255, 166);
	static readonly ImColor8 Ink = new(0, 0, 0, 255);

	/// <summary>Whether there is anything to show: the game's clock is on and hazards are in play.</summary>
	static bool Active => CrewClock.IsShown && CrewClock.HasHazards;

	/// <summary>Gathers every vessel's next crew event, soonest first, as CrewClock.Draw does.</summary>
	public static void Update()
	{
		Events.Clear();
		if (!Active || Universe.CurrentSystem is not { } system)
			return;
		foreach (var astronomical in system.All.AsSpan())
			if (astronomical is Vehicle { NextCrewEvent: { } next })
				Events.Add(next);
		Events.Sort(CrewCountdown.Comparison);
	}

	public static IReadOnlyList<CrewEvent> All => Events;

	static bool IsUrgent(in CrewEvent e) => e.Stage != CrewStage.Low || e.Remaining.Seconds() < UrgentSeconds;

	/// <summary>"FOOD 2.0D · ROCKET": the soonest deadline, short, for the crew line.</summary>
	public static string? Soonest()
	{
		if (Events.Count == 0)
			return null;
		var e = Events[0];
		return $"{Hazard(e.Hazard)} {Duration(e.Remaining.Seconds())} · {Vessel(e)}";
	}

	/// <summary>"TEST KITTEN 1 LOW IN 2.0D · FOOD · ROCKET": one deadline in full.</summary>
	public static string Describe(in CrewEvent e)
	{
		string name = e.Kitten.Name.ToUpperInvariant();
		string time = Duration(e.Remaining.Seconds());
		if (e.Hazard == CrewHazard.Radiation && e.Stage != CrewStage.Dead)
			return $"{name} RADIATION SICK IN {time} · {Vessel(e)}";
		string verb = e.Stage switch
		{
			CrewStage.Dead => "DIES IN",
			CrewStage.Incapacitated => "INCAPACITATED IN",
			CrewStage.Degraded => "IMPAIRED IN",
			_ => "LOW IN",
		};
		return $"{name} {verb} {time} · {Hazard(e.Hazard)} · {Vessel(e)}";
	}

	/// <summary>Under the time block: the nearest urgent deadline and BACK TO; returns the bottom.</summary>
	public static float DrawUnderClock(Canvas c, float centreX, float top)
	{
		float y = top;
		const float height = 30, pad = 14;
		int urgent = Events.FindIndex(e => IsUrgent(e));
		var returnTo = CrewClock.FindReturnVessel();
		if (urgent < 0 && returnTo == null)
			return y;

		float regionWidth = 900;
		Controls.BeginRegion(c, "fleetclock", new float2(centreX - regionWidth / 2, y), new float2(centreX + regionWidth / 2, y + 2 * (height + 6)));
		if (urgent >= 0)
		{
			var e = Events[urgent];
			string text = Describe(e);
			string more = Events.Count(x => IsUrgent(x)) is var n && n > 1 ? $"  +{n - 1}" : "";
			float w = c.MeasureText(Fonts.SemiBold, SmallSize + 1, text + more, tracking: 1.4f) + 2 * pad;
			var min = new float2(centreX - w / 2, y);
			// Dying or incapacitated: filled; impaired or low: outlined.
			bool severe = e.Stage is CrewStage.Dead or CrewStage.Incapacitated;
			if (Controls.Button(c, "FLEET URGENT", "", min, min + new float2(w, height), lit: severe))
				CrewClock.Activate(e);
			c.Text(Fonts.SemiBold, SmallSize + 1, new float2(centreX, y + height / 2), Align.Center, severe ? Ink : White, text + more,
				tracking: 1.4f, shadow: !severe);
			y += height + 6;
		}
		if (returnTo != null)
		{
			string label = $"BACK TO {(returnTo is KittenEva ? returnTo.Id.ToUpperInvariant() + " (EVA)" : returnTo.Id.ToUpperInvariant())}";
			float w = c.MeasureText(Fonts.SemiBold, Controls.TextSize, label, tracking: 1.4f) + 2 * pad;
			var min = new float2(centreX - w / 2, y);
			if (Controls.Button(c, "FLEET BACK", label, min, min + new float2(w, height)))
				Universe.FollowInHoveredViewport(returnTo);
			y += height + 6;
		}
		Controls.EndRegion();
		return y;
	}

	/// <summary>In the open crew card: every vessel's next deadline, each clickable; returns the bottom.</summary>
	public static float DrawList(Canvas c, float left, float top, float width, int maxRows)
	{
		if (Events.Count == 0)
			return top;
		float y = top;
		c.Text(Fonts.Medium, SmallSize - 3, new float2(left, y + 8), Align.Left, Dim, "FLEET · NEXT DEADLINES", tracking: 1.6f);
		y += 18;
		for (int i = 0; i < Events.Count && i < maxRows; i++)
		{
			var e = Events[i];
			var min = new float2(left, y);
			if (Controls.Button(c, $"FLEET {i}", "", min, min + new float2(width, RowHeight)))
				CrewClock.Activate(e);
			c.Text(Fonts.SemiBold, SmallSize - 1, new float2(left + 10, y + RowHeight / 2), Align.Left, IsUrgent(e) ? White : Dim,
				c.Fit(Fonts.SemiBold, SmallSize - 1, Describe(e), width - 20, tracking: 1f), tracking: 1f);
			y += RowHeight + 4;
		}
		return y;
	}

	public const float RowHeight = 24;

	public static int ListHeightRows(int maxRows) => Math.Min(Events.Count, maxRows);

	static string Vessel(in CrewEvent e) => e.Vessel is KittenEva ? "EVA" : e.Vessel?.Id.ToUpperInvariant() ?? "—";

	static string Hazard(CrewHazard h) => h switch
	{
		CrewHazard.Air => "AIR",
		CrewHazard.Scrubber => "CO2",
		CrewHazard.Water => "WATER",
		CrewHazard.Food => "FOOD",
		_ => "RADIATION",
	};

	static string Duration(double s) =>
		!double.IsFinite(s) ? "—"
		: s >= 2 * 86400 ? $"{s / 86400:0.0}D"
		: s >= 3600 ? $"{(int)(s / 3600)}H {(int)(s % 3600 / 60)}M"
		: $"{(int)(s / 60)}M";
}
