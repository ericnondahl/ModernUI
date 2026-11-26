using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace TelemetryHud;

/// <summary>
/// The stock Sequence panel, in the HUD's style, at the top left: the vehicle's stages (the
/// game's sequences) with each one's delta-v, TWR, Isp and burn time, the next stage marked
/// and fired ones dimmed; per stage, the ATM/VAC switch the stock panel has; the total
/// delta-v; and the stock RESOURCES button (the game's resource groups window).
///
/// The game only computes the per-stage numbers while its Sequence or Engine gauge is
/// enabled (PhysicsBubble), which they stay: the HUD skips drawing them, it doesn't
/// disable them.
/// </summary>
public static class Staging
{
	const float Width = 440;
	const float RowHeight = 30;
	const float Gap = 4;
	const float SmallSize = 15;
	const int MaxRows = 8;

	static readonly ImColor8 White = new(255, 255, 255, 255);
	static readonly ImColor8 Dim = new(255, 255, 255, 166);
	static readonly ImColor8 Faint = new(255, 255, 255, 80);
	static readonly ImColor8 Backing = new(0, 0, 0, 120);
	static readonly ImColor8 NextFill = new(255, 255, 255, 34);

	// Column offsets in a row, from its left edge.
	const float ColStage = 12, ColDeltaV = 190, ColTwr = 246, ColIsp = 302, ColBurn = 362, ColEnv = 392;

	public static void Draw(Canvas c, float left, float top, Vehicle v)
	{
		if (v is KittenEva)
			return;
		var list = v.Parts.SequenceList;
		var sequences = list.Sequences;
		var performance = v.Parts.PerformanceSequences.PerformanceSequences;
		int next = list.GetNextSequenceNumber();
		bool open = Settings.Current.StagingOpen;
		int rows = Math.Min(sequences.Length, MaxRows);
		float height = RowHeight + (open && rows > 0 ? 8 + RowHeight + rows * (RowHeight + Gap) : 0);
		float right = left + Width;

		Controls.BeginRegion(c, "staging", new float2(left, top), new float2(right, top + height));
		const float pad = 10;
		c.FillRect(new float2(left - pad, top - pad), new float2(right + pad, top + height + pad), Backing, 6);

		// Header: total delta-v; the resource groups window; open/close.
		float hy = top + RowHeight / 2;
		c.Text(Fonts.Medium, SmallSize, new float2(left, hy), Align.Left, Dim, "STAGING", tracking: 2.4f);
		float total = v.Parts.PerformanceSequences.TotalDeltaV;
		c.Text(Fonts.SemiBold, SmallSize + 2, new float2(left + 110, hy), Align.Left, White,
			total > 0 ? $"{total:N0}" : "—", tabular: true);
		c.Text(Fonts.Medium, SmallSize - 3, new float2(left + 110 + c.MeasureText(Fonts.SemiBold, SmallSize + 2, total > 0 ? $"{total:N0}" : "—", tabular: true) + 6, hy + 1),
			Align.Left, Dim, "M/S TOTAL", tracking: 1.2f);
		if (Controls.Button(c, "RESOURCES", "RESOURCES", new float2(right - 64 - 6 - 100, top), new float2(right - 64 - 6, top + RowHeight),
				lit: ResourceGroupsPanel.IsOpen))
			ResourceGroupsPanel.Toggle();
		if (Controls.Button(c, "STAGING", open ? "CLOSE" : "SET", new float2(right - 64, top), new float2(right, top + RowHeight), lit: open))
		{
			Settings.Current.StagingOpen = !open;
			Settings.Current.Save();
		}

		if (open && rows > 0)
		{
			float y = top + RowHeight + 8;
			// Column heads.
			float headY = y + RowHeight / 2;
			c.Text(Fonts.Medium, SmallSize - 3, new float2(left + ColDeltaV, headY), Align.Right, Faint, "Δ-V M/S", tracking: 1.2f);
			c.Text(Fonts.Medium, SmallSize - 3, new float2(left + ColTwr, headY), Align.Right, Faint, "TWR", tracking: 1.2f);
			c.Text(Fonts.Medium, SmallSize - 3, new float2(left + ColIsp, headY), Align.Right, Faint, "ISP S", tracking: 1.2f);
			c.Text(Fonts.Medium, SmallSize - 3, new float2(left + ColBurn, headY), Align.Right, Faint, "BURN", tracking: 1.2f);
			y += RowHeight;

			// The last stage at the top, as the stock panel lists them; stage 1 at the bottom.
			int shown = 0;
			for (int i = sequences.Length - 1; i >= 0 && shown < MaxRows; i--, shown++)
			{
				var s = sequences[i];
				SequencePerformance? perf = i < performance.Length ? performance[i] : null;
				DrawRow(c, left, y, s, perf, next, list.ActiveSequence, list, v.Parts);
				y += RowHeight + Gap;
			}
		}
		Controls.EndRegion();
	}

	static void DrawRow(Canvas c, float left, float top, Sequence s, SequencePerformance? perf, int next, int active, SequenceList list, PartTree parts)
	{
		bool isNext = s.Number == next;
		bool fired = s.Activated;
		var ink = fired ? Faint : White;
		float cy = top + RowHeight / 2;

		if (isNext)
		{
			c.FillRect(new float2(left, top), new float2(left + Width, top + RowHeight), NextFill, 3);
			c.FillRect(new float2(left, top), new float2(left + 3, top + RowHeight), White);
		}
		string name = $"STAGE {s.Number}" + (isNext ? "  NEXT" : s.Number == active ? "  ACTIVE" : "");
		c.Text(Fonts.SemiBold, SmallSize, new float2(left + ColStage, cy), Align.Left, ink, name, tracking: 1.4f);

		// The stock panel shows numbers only for stages that do something.
		if (perf is { } p && p.DeltaV > 1 && (p.Twr > 0 || p.Isp > 0))
		{
			float burn = 0;
			if (p.Phases != null)
				foreach (var phase in p.Phases)
					burn += phase.Duration;
			c.Text(Fonts.SemiBold, SmallSize, new float2(left + ColDeltaV, cy), Align.Right, ink, $"{p.DeltaV:N0}", tabular: true);
			c.Text(Fonts.SemiBold, SmallSize, new float2(left + ColTwr, cy), Align.Right, ink, $"{p.Twr:0.0}", tabular: true);
			c.Text(Fonts.SemiBold, SmallSize, new float2(left + ColIsp, cy), Align.Right, ink, $"{p.Isp:0}", tabular: true);
			c.Text(Fonts.SemiBold, SmallSize, new float2(left + ColBurn, cy), Align.Right, ink, burn > 0 ? FormatBurn(burn) : "—", tabular: true);
		}

		// ATM / VAC: which conditions the stage's numbers assume (the stock panel's switch).
		bool vacuum = s.Environment == PerformanceEnvironment.Vacuum;
		var envMin = new float2(left + ColEnv, top + 3);
		if (Controls.Button(c, $"STAGE {s.Number} ENV", vacuum ? "VAC" : "ATM", envMin, envMin + new float2(Width - ColEnv, RowHeight - 6), disabled: fired))
			InputEvents.SequenceChangeBuffer.Add(new InputEvents.SequenceChangeData
			{
				Operation = InputEvents.SequenceOp.SetEnvironment,
				PartTree = parts,
				SequenceList = list,
				Sequence = s,
				Environment = vacuum ? PerformanceEnvironment.Atmospheric : PerformanceEnvironment.Vacuum,
			});
	}

	static string FormatBurn(float seconds) =>
		seconds < 60 ? $"{seconds:0}S" : seconds < 3600 ? $"{(int)seconds / 60}:{(int)seconds % 60:00}" : $"{seconds / 3600:0.0}H";
}
