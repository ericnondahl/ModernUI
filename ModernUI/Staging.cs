using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace ModernUI;

/// <summary>
/// The stock Sequence panel, in the HUD's style, at the top left: the vehicle's stages (the
/// game's sequences) with each one's delta-v, TWR, Isp and burn time, the next stage marked
/// and fired ones dimmed; per stage, the ATM/VAC switch the stock panel has; the total
/// delta-v; and the stock RESOURCES button (the game's resource groups window).
///
/// Under each stage's numbers, what it does: a chip per action, as the stock panel's cells
/// (engines, solid motors, stack and radial decouplers, parachutes), with a count for a
/// symmetry group and a fuel bar on engines. Hovering a chip or a stage highlights its parts
/// on the vehicle. Closed, the panel still shows the next stage's chips.
///
/// Drag a chip onto another stage to fire it there, or drag a stage by its name to reorder
/// the stages; fired stages take neither. Both go through the game's SequenceChangeBuffer,
/// as the stock panel's drags do.
///
/// The game only computes the per-stage numbers while its Sequence or Engine gauge is
/// enabled (PhysicsBubble), which they stay: the HUD skips drawing them, it doesn't
/// disable them.
/// </summary>
public static class Staging
{
	const float Width = 440;
	const float RowHeight = 30;
	const float ChipHeight = 26;
	const float ChipGap = 6;
	const float IconSize = 18;
	const float Gap = 4;
	const float SmallSize = 15;
	const int MaxRows = 8;

	static ImColor8 White => Theme.Ink(255);
	static ImColor8 Dim => Theme.Ink(166);
	static ImColor8 Faint => Theme.Ink(80);
	static ImColor8 Ghost => Theme.Ink(40);
	static readonly ImColor8 Backing = new(0, 0, 0, 120);
	static ImColor8 NextFill => Theme.Ink(34);
	static ImColor8 HoverFill => Theme.Ink(18);
	static ImColor8 DropFill => Theme.Ink(40);
	static readonly ImColor8 ChipFill = new(0, 0, 0, 90);
	static readonly ImColor8 CaptionFill = new(0, 0, 0, 210);

	// Column offsets in a row, from its left edge.
	const float ColStage = 12, ColDeltaV = 190, ColTwr = 246, ColIsp = 302, ColBurn = 362, ColEnv = 392;

	/// <summary>What a chip shows: the stock panel's icon for the action.</summary>
	enum Kind { LiquidEngine, SolidEngine, StackDecoupler, RadialDecoupler, Parachute, Drogue, ChuteCut, Other }

	/// <summary>
	/// One action in a stage, as the stock panel's cells: a part (or a symmetry group of the
	/// same part) and the module that fires. Not the primary module: a single action of the
	/// part fired in another stage (a decoupler on an engine, say).
	/// </summary>
	sealed record Item(Part Leader, ISequenced Module, List<Part>? Group, bool IsPrimary, Kind Kind)
	{
		public int Count => Group?.Count ?? 1;
	}

	/// <summary>A stage as laid out this frame. Chip rectangles are relative to the row's top left.</summary>
	sealed class Row
	{
		public required Sequence Sequence;
		public int Index;
		public SequencePerformance? Performance;
		public readonly List<(Item Item, float2 Min, float2 Max)> Chips = [];
		public float Height;
		public float Top;
	}

	/// <summary>A drag in progress: a chip (Item) or, without one, the whole stage.</summary>
	sealed class DragState
	{
		public required Vehicle Vehicle;
		public required Sequence From;
		public Item? Item;
		public float2 ChipSize;
	}

	static DragState? _drag;
	static readonly List<Part> _highlighted = [];
	static (Item Item, float2 Min, float2 Max, string Caption)? _hovered;

	/// <summary>
	/// Drops what the panel set this frame: the parts it highlighted, a hover caption. A drag
	/// ends too, unless keepDrag (a frame that draws the panel again).
	/// </summary>
	public static void Reset(bool keepDrag = false)
	{
		foreach (var part in _highlighted)
			part.HighlightedForSequence = false;
		_highlighted.Clear();
		_hovered = null;
		if (!keepDrag)
			_drag = null;
	}

	public static float Draw(Canvas c, float left, float top, Vehicle v, bool forceOpen)
	{
		Reset(keepDrag: true);
		if (v is KittenEva)
		{
			_drag = null;
			return top;
		}
		var list = v.Parts.SequenceList;
		var sequences = list.Sequences;
		var performance = v.Parts.PerformanceSequences.PerformanceSequences;
		int next = list.GetNextSequenceNumber();
		int nextIndex = -1;
		for (int i = 0; i < sequences.Length; i++)
			if (sequences[i].Number == next)
				nextIndex = i;
		bool open = forceOpen || Settings.Current.StagingOpen;
		if (_drag != null && (_drag.Vehicle != v || !open))
			_drag = null;

		// Open: every stage, the last at the top as the stock panel lists them (a window of
		// MaxRows that keeps the next stage in view). Closed: the next stage, on one line.
		var rows = new List<Row>();
		if (open)
		{
			var (lo, hi) = Window(sequences.Length, nextIndex);
			for (int i = hi; i >= lo; i--)
				rows.Add(Layout(c, sequences[i], i, i < performance.Length ? performance[i] : null, ColStage, RowHeight - 2, rowGapBelow: 6));
		}
		else if (nextIndex >= 0)
		{
			var s = sequences[nextIndex];
			float chipsLeft = ColStage + c.MeasureText(Fonts.SemiBold, SmallSize, RowName(s, next, list.ActiveSequence), tracking: 1.4f) + 16;
			rows.Add(Layout(c, s, nextIndex, null, chipsLeft, (RowHeight - ChipHeight) / 2, rowGapBelow: (RowHeight - ChipHeight) / 2));
		}

		float listTop = top + RowHeight + 8 + (open && rows.Count > 0 ? RowHeight : 0);
		float height = rows.Count == 0 ? RowHeight : listTop - top + rows.Sum(r => r.Height + Gap) - Gap;
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
		if (!forceOpen && Controls.Button(c, "STAGING", open ? "CLOSE" : "OPEN", new float2(right - 64, top), new float2(right, top + RowHeight), lit: open))
		{
			Settings.Current.StagingOpen = !open;
			Settings.Current.Save();
		}

		if (open && rows.Count > 0)
		{
			// Column heads.
			float headY = top + RowHeight + 8 + RowHeight / 2;
			c.Text(Fonts.Medium, SmallSize - 3, new float2(left + ColDeltaV, headY), Align.Right, Faint, "Δ-V M/S", tracking: 1.2f);
			c.Text(Fonts.Medium, SmallSize - 3, new float2(left + ColTwr, headY), Align.Right, Faint, "TWR", tracking: 1.2f);
			c.Text(Fonts.Medium, SmallSize - 3, new float2(left + ColIsp, headY), Align.Right, Faint, "ISP S", tracking: 1.2f);
			c.Text(Fonts.Medium, SmallSize - 3, new float2(left + ColBurn, headY), Align.Right, Faint, "BURN", tracking: 1.2f);
		}

		float y = listTop;
		foreach (var row in rows)
		{
			row.Top = y;
			DrawRow(c, left, row, next, list, v, open);
			y += row.Height + Gap;
		}

		if (_drag != null)
			UpdateDrag(c, left, rows, sequences, list, v.Parts);
		Controls.EndRegion();
		return top + height + 2 * pad + 8;
	}

	/// <summary>
	/// What goes over everything else, so the crew card under the panel doesn't cover it:
	/// the hovered chip's caption, and the chip or stage being dragged, at the mouse.
	/// </summary>
	public static void DrawOverlay(Canvas c)
	{
		if (_drag is { } drag)
		{
			var mouse = c.Units(1) * (float2)ImGui.GetMousePos();
			if (drag.Item is { } item)
			{
				var min = mouse - drag.ChipSize / 2;
				c.FillRect(min - new float2(2, 2), min + drag.ChipSize + new float2(2, 2), CaptionFill, 5);
				DrawChip(c, item, min, min + drag.ChipSize, White, 230, -1);
			}
			else
			{
				string label = $"STAGE {drag.From.Number}";
				float w = c.MeasureText(Fonts.SemiBold, SmallSize, label, tracking: 1.4f) + 24;
				var min = mouse - new float2(w / 2, RowHeight / 2);
				c.FillRect(min, min + new float2(w, RowHeight), CaptionFill, 4);
				c.Rect(min, min + new float2(w, RowHeight), Dim, 1.2f, 4);
				c.Text(Fonts.SemiBold, SmallSize, min + new float2(w / 2, RowHeight / 2), Align.Center, White, label, tracking: 1.4f);
			}
			return;
		}
		if (_hovered is { } h)
		{
			const float size = SmallSize - 3;
			float w = c.MeasureText(Fonts.Medium, size, h.Caption, tracking: 1.2f) + 16;
			var min = new float2(h.Min.X, h.Max.Y + 4);
			c.FillRect(min, min + new float2(w, 22), CaptionFill, 4);
			c.Text(Fonts.Medium, size, min + new float2(8, 11), Align.Left, White, h.Caption, tracking: 1.2f);
		}
	}

	// ---- layout --------------------------------------------------------------------------------

	/// <summary>The rows shown out of count: all, or MaxRows from the stage under the next one up.</summary>
	static (int Lo, int Hi) Window(int count, int nextIndex)
	{
		if (count <= MaxRows)
			return (0, count - 1);
		int lo = nextIndex < 0 ? count - MaxRows : Math.Min(Math.Max(0, nextIndex - 1), count - MaxRows);
		return (lo, lo + MaxRows - 1);
	}

	/// <summary>A stage's chips from (chipsLeft, chipsTop) in the row, wrapping at the row's width.</summary>
	static Row Layout(Canvas c, Sequence s, int index, SequencePerformance? perf, float chipsLeft, float chipsTop, float rowGapBelow)
	{
		var row = new Row { Sequence = s, Index = index, Performance = perf };
		float x = chipsLeft, y = chipsTop;
		foreach (var item in Items(s))
		{
			float w = ChipWidth(c, item);
			if (x > chipsLeft && x + w > Width)
			{
				x = chipsLeft;
				y += ChipHeight + ChipGap;
			}
			row.Chips.Add((item, new float2(x, y), new float2(x + w, y + ChipHeight)));
			x += w + ChipGap;
		}
		row.Height = row.Chips.Count == 0 ? RowHeight : Math.Max(RowHeight, y + ChipHeight + rowGapBelow);
		return row;
	}

	/// <summary>The stage's actions, grouped as the stock panel groups them (SequenceList.BuildSequenceActions).</summary>
	static List<Item> Items(Sequence s)
	{
		var items = new List<Item>();
		var grouped = new HashSet<Part>();
		var parts = s.Parts;
		for (int i = 0; i < parts.Length; i++)
		{
			var part = parts[i];
			if (grouped.Contains(part))
				continue;
			List<Part>? group = null;
			if (part.SequenceGroup > 0)
				for (int j = 0; j < parts.Length; j++)
				{
					var other = parts[j];
					if (other != part && !grouped.Contains(other) && other.SequenceGroup == part.SequenceGroup && other.Template == part.Template)
					{
						group ??= [part];
						group.Add(other);
						grouped.Add(other);
					}
				}
			bool any = false;
			foreach (var module in part.GetSubtreeSequencedModules())
			{
				if (module.Sequence != s.Number || module is IEnable { IsEnabled: false } || module is not ModuleBase)
					continue;
				items.Add(new Item(part, module, group, module == part.PrimarySequenceModule, KindOf(module, part)));
				any = true;
			}
			if (!any && part.PrimarySequenceModule is { } primary && primary is ModuleBase)
				items.Add(new Item(part, primary, group, true, KindOf(primary, part)));
		}
		return items;
	}

	// SequenceList.GetActionIcon and GetPartIcon.
	static Kind KindOf(ISequenced module, Part part) => module switch
	{
		EngineController engine => HasSolidRocket(engine) ? Kind.SolidEngine : Kind.LiquidEngine,
		Decoupler => part.Template.HasEditorTag(EditorTag.Radial) ? Kind.RadialDecoupler : Kind.StackDecoupler,
		ParachuteDeploy chute => chute.IsDrogue ? Kind.Drogue : Kind.Parachute,
		ParachuteCut => Kind.ChuteCut,
		_ => PartKind(part),
	};

	static Kind PartKind(Part part)
	{
		var t = part.Template;
		if (t.HasEditorTag(EditorTag.Radial))
			return Kind.RadialDecoupler;
		if (t.HasEditorTag(EditorTag.Interstage) || t.HasEditorTag(EditorTag.Coupling) || t.HasEditorTag(EditorTag.Cargo))
			return Kind.StackDecoupler;
		if (t.HasEditorTag(EditorTag.Engines))
			return Kind.LiquidEngine;
		if (t.HasEditorTag(EditorTag.Booster))
			return Kind.SolidEngine;
		return Kind.Other;
	}

	static bool HasSolidRocket(EngineController engine)
	{
		foreach (var rocket in engine.Rockets)
			if (rocket is SolidRocket)
				return true;
		return false;
	}

	static string Verb(ISequenced module) => module switch
	{
		EngineController => "IGNITE",
		Decoupler => "DECOUPLE",
		ParachuteDeploy => "DEPLOY",
		ParachuteCut => "CUT",
		_ => "",
	};

	static string KindName(Kind kind) => kind switch
	{
		Kind.LiquidEngine => "ENGINE",
		Kind.SolidEngine => "SOLID MOTOR",
		Kind.StackDecoupler => "STACK DECOUPLER",
		Kind.RadialDecoupler => "RADIAL DECOUPLER",
		Kind.Parachute => "PARACHUTE",
		Kind.Drogue => "DROGUE CHUTE",
		Kind.ChuteCut => "PARACHUTE",
		_ => "PART",
	};

	static string Code(Item item)
	{
		string name = item.Leader.DisplayName ?? "";
		return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
	}

	const float CountSize = SmallSize - 2;

	static float ChipWidth(Canvas c, Item item)
	{
		float w = 6 + (item.Kind == Kind.Other ? c.MeasureText(Fonts.SemiBold, CountSize, Code(item), tracking: 1) : IconSize) + 6;
		if (item.Count > 1)
			w += 2 + c.MeasureText(Fonts.SemiBold, CountSize, $"×{item.Count}", tabular: true);
		return w;
	}

	// ---- drawing -------------------------------------------------------------------------------

	static string RowName(Sequence s, int next, int active) =>
		$"STAGE {s.Number}" + (s.Number == next ? "  NEXT" : s.Number == active ? "  ACTIVE" : "");

	static void DrawRow(Canvas c, float left, Row row, int next, SequenceList list, Vehicle v, bool open)
	{
		var s = row.Sequence;
		float top = row.Top;
		bool isNext = s.Number == next;
		bool fired = s.Activated;
		bool draggingThis = _drag is { Item: null } d && d.From == s;
		var ink = fired || draggingThis ? Faint : White;
		float cy = top + RowHeight / 2;

		if (isNext)
		{
			c.FillRect(new float2(left, top), new float2(left + Width, top + row.Height), NextFill, 3);
			c.FillRect(new float2(left, top), new float2(left + 3, top + row.Height), White);
		}

		// The stage's name: hover highlights its parts; drag it to reorder (open, not fired).
		bool canDrag = open && !fired;
		float nameRight = open ? ColEnv - 6 : (row.Chips.Count > 0 ? row.Chips[0].Min.X - 8 : Width);
		Controls.Area(c, $"STAGE {s.Number} ROW", new float2(left, top), new float2(left + nameRight, top + RowHeight), out bool hovered, out bool held);
		if (hovered && _drag == null)
		{
			if (!isNext)
				c.FillRect(new float2(left, top), new float2(left + Width, top + row.Height), HoverFill, 3);
			foreach (var part in s.Parts)
				Highlight(part);
			if (canDrag)
				ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
		}
		if (held && canDrag && _drag == null && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
			_drag = new DragState { Vehicle = v, From = s };
		c.Text(Fonts.SemiBold, SmallSize, new float2(left + ColStage, cy), Align.Left, ink, RowName(s, next, list.ActiveSequence), tracking: 1.4f);

		if (open)
		{
			// The stock panel shows numbers only for stages that do something.
			if (row.Performance is { } p && p.DeltaV > 1 && (p.Twr > 0 || p.Isp > 0))
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
					PartTree = v.Parts,
					SequenceList = list,
					Sequence = s,
					Environment = vacuum ? PerformanceEnvironment.Atmospheric : PerformanceEnvironment.Vacuum,
				});
		}

		// The chips: hover for what it is (and its parts lit); drag one to another stage.
		for (int k = 0; k < row.Chips.Count; k++)
		{
			var (item, rmin, rmax) = row.Chips[k];
			var min = new float2(left, top) + rmin;
			var max = new float2(left, top) + rmax;
			Controls.Area(c, $"STAGE {s.Number} ITEM {k}", min, max, out bool chipHovered, out bool chipHeld);
			bool dragged = _drag?.Item is { } di && di.Module == item.Module && di.Leader == item.Leader && _drag.From == s;
			float fuel = item.Module is EngineController ? SequenceList.GetEngineFuelVolumeFraction(item.Leader, v) : -1;
			if (chipHovered && _drag == null)
			{
				HighlightItem(item);
				_hovered = (item, min, max, Caption(item, fuel));
				if (canDrag)
					ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
			}
			if (dragged)
				HighlightItem(item);
			if (chipHeld && canDrag && _drag == null && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
				_drag = new DragState { Vehicle = v, From = s, Item = item, ChipSize = max - min };
			byte edge = dragged ? (byte)40 : chipHovered && _drag == null ? (byte)210 : (byte)90;
			DrawChip(c, item, min, max, dragged ? Ghost : fired || draggingThis ? Faint : White, edge, fuel);
		}
	}

	static string Caption(Item item, float fuel)
	{
		// A part without a display name in the game's data shows its id
		// (COREFAIRINGA_PREFAB_INTERSTAGE...): say what it is instead.
		string name = item.Leader.DisplayName ?? "";
		string text = name.Length == 0 || name.Contains('_') ? KindName(item.Kind) : name.ToUpperInvariant();
		if (item.Count > 1)
			text += $" ×{item.Count}";
		string verb = Verb(item.Module);
		if (verb.Length > 0)
			text += $" · {verb}";
		if (fuel >= 0)
			text += $" · {fuel * 100:0}% FUEL";
		return text;
	}

	/// <summary>A chip: its icon (or a two-letter code), the group's count, and an engine's fuel along the bottom.</summary>
	static void DrawChip(Canvas c, Item item, float2 min, float2 max, ImColor8 ink, byte edge, float fuel)
	{
		c.FillRect(min, max, ChipFill, 3);
		c.Rect(min, max, Theme.Ink(edge), 1.2f, 3);
		float x = min.X + 6;
		float cy = (min.Y + max.Y) / 2 - (fuel >= 0 ? 1 : 0);
		if (item.Kind == Kind.Other)
		{
			string code = Code(item);
			c.Text(Fonts.SemiBold, CountSize, new float2(x, cy), Align.Left, ink, code, tracking: 1);
			x += c.MeasureText(Fonts.SemiBold, CountSize, code, tracking: 1);
		}
		else
		{
			DrawIcon(c, item.Kind, new float2(x + IconSize / 2, cy), ink);
			x += IconSize;
		}
		if (item.Count > 1)
			c.Text(Fonts.SemiBold, CountSize, new float2(x + 2, cy), Align.Left, ink, $"×{item.Count}", tabular: true);
		if (fuel >= 0)
		{
			float barY = max.Y - 4, x0 = min.X + 4, x1 = max.X - 4;
			c.Line(new float2(x0, barY), new float2(x1, barY), Theme.Ink(46), 2);
			if (fuel > 0)
				c.Line(new float2(x0, barY), new float2(x0 + (x1 - x0) * Math.Clamp(fuel, 0, 1), barY), ink, 2);
		}
	}

	/// <summary>The action's icon, line-drawn in an 18-unit box centred on at.</summary>
	static void DrawIcon(Canvas c, Kind kind, float2 at, ImColor8 ink)
	{
		const float t = 1.3f;
		float2 P(float x, float y) => at + new float2(x, y);
		switch (kind)
		{
			case Kind.LiquidEngine:
				// Combustion chamber over a bell.
				c.Rect(P(-3.5f, -8.5f), P(3.5f, -3.5f), ink, t, 1);
				c.Polygon([P(-2.5f, -3.5f), P(2.5f, -3.5f), P(7, 8), P(-7, 8)], ink, t);
				break;
			case Kind.SolidEngine:
				// A tall casing on a small nozzle.
				c.Rect(P(-4, -9), P(4, 4), ink, t, 1.5f);
				c.Polygon([P(-2.5f, 4), P(2.5f, 4), P(4.5f, 9), P(-4.5f, 9)], ink, t);
				break;
			case Kind.StackDecoupler:
				// Two plates parting up and down.
				c.Rect(P(-8, -4.5f), P(8, -1.5f), ink, t);
				c.Rect(P(-8, 1.5f), P(8, 4.5f), ink, t);
				c.FillTriangle(P(-3, -6.5f), P(3, -6.5f), P(0, -9.5f), ink);
				c.FillTriangle(P(-3, 6.5f), P(0, 9.5f), P(3, 6.5f), ink);
				break;
			case Kind.RadialDecoupler:
				// Two plates parting sideways.
				c.Rect(P(-4.5f, -8), P(-1.5f, 8), ink, t);
				c.Rect(P(1.5f, -8), P(4.5f, 8), ink, t);
				c.FillTriangle(P(-6.5f, -3), P(-9.5f, 0), P(-6.5f, 3), ink);
				c.FillTriangle(P(6.5f, -3), P(6.5f, 3), P(9.5f, 0), ink);
				break;
			case Kind.Parachute:
			case Kind.Drogue:
			case Kind.ChuteCut:
				// A canopy and its lines; a drogue's canopy is smaller; a cut is struck through.
				float r = kind == Kind.Drogue ? 5.5f : 8;
				c.Arc(P(0, -1), r, MathF.PI, 2 * MathF.PI, ink, t);
				c.Line(P(-r, -1), P(r, -1), ink, t);
				c.Line(P(-r, -1), P(0, 8), ink, 1);
				c.Line(P(r, -1), P(0, 8), ink, 1);
				if (kind == Kind.ChuteCut)
					c.Line(P(-8, 8), P(8, -9), ink, t);
				break;
		}
	}

	static void Highlight(Part part)
	{
		part.HighlightedForSequence = true;
		_highlighted.Add(part);
	}

	static void HighlightItem(Item item)
	{
		if (item.Group != null)
			foreach (var part in item.Group)
				Highlight(part);
		else
			Highlight(item.Leader);
	}

	// ---- dragging ------------------------------------------------------------------------------

	/// <summary>
	/// While a drag runs: marks where it would land (a stage for a chip; a line between stages
	/// for a stage), and on release hands the move to the game. Fired stages take nothing, and a
	/// stage can't move under one.
	/// </summary>
	static void UpdateDrag(Canvas c, float left, List<Row> rows, ReadOnlySpan<Sequence> sequences, SequenceList list, PartTree parts)
	{
		var drag = _drag!;
		ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
		var mouse = c.Units(1) * (float2)ImGui.GetMousePos();
		bool released = !ImGui.IsMouseDown(ImGuiMouseButton.Left);
		Row? over = null;
		if (mouse.X >= left - 10 && mouse.X <= left + Width + 10)
			foreach (var row in rows)
				if (mouse.Y >= row.Top - Gap / 2 && mouse.Y < row.Top + row.Height + Gap / 2)
					over = row;

		if (drag.Item is { } item)
		{
			bool valid = over != null && over.Sequence != drag.From && !over.Sequence.Activated && list.Contains(drag.From);
			if (valid)
			{
				var min = new float2(left, over!.Top);
				var max = new float2(left + Width, over.Top + over.Height);
				c.FillRect(min, max, DropFill, 3);
				c.Rect(min, max, White, 1.5f, 3);
			}
			if (released)
			{
				if (valid)
					PlaceItem(item, drag.From, over!.Sequence, list, parts);
				_drag = null;
			}
			return;
		}

		// A stage: above or below the row under the mouse (the rows run from the last stage
		// at the top), as SequenceList.MoveSequence takes it: the stage to insert before.
		int from = IndexOf(sequences, drag.From);
		int insert = -1;
		float lineY = 0;
		if (over != null && from >= 0)
		{
			bool lowerHalf = mouse.Y >= over.Top + over.Height / 2;
			insert = lowerHalf ? over.Index : over.Index + 1;
			lineY = lowerHalf ? over.Top + over.Height + Gap / 2 : over.Top - Gap / 2;
			int lastFired = -1;
			for (int i = 0; i < sequences.Length; i++)
				if (sequences[i].Activated)
					lastFired = i;
			// Nowhere to go (its own place), or under a fired stage.
			if (insert == from || insert == from + 1 || insert <= lastFired)
				insert = -1;
		}
		if (insert >= 0)
		{
			c.Line(new float2(left - 4, lineY), new float2(left + Width + 4, lineY), White, 2.5f);
			c.FillCircle(new float2(left - 4, lineY), 3.5f, White);
		}
		if (released)
		{
			if (insert >= 0)
				InputEvents.SequenceChangeBuffer.Add(new InputEvents.SequenceChangeData
				{
					Operation = InputEvents.SequenceOp.Reorder,
					PartTree = parts,
					SequenceList = list,
					Sequence = drag.From,
					InsertBefore = insert < sequences.Length ? sequences[insert] : null,
				});
			_drag = null;
		}
	}

	/// <summary>An action into another stage, as the stock panel's drop on a stage: last in it.</summary>
	static void PlaceItem(Item item, Sequence from, Sequence to, SequenceList list, PartTree parts)
	{
		var change = new InputEvents.SequenceChangeData
		{
			PartTree = parts,
			SequenceList = list,
			Sequence = to,
			Part = item.Leader,
			FromSequence = from,
			Mode = InputEvents.SequencePlaceMode.Last,
		};
		if (!item.IsPrimary)
		{
			// One action of a part whose other actions stay where they are.
			change.Operation = InputEvents.SequenceOp.PlaceModule;
			change.Module = item.Module;
		}
		else if (item.Group is { Count: > 1 } group)
		{
			change.Operation = InputEvents.SequenceOp.PlaceGroup;
			change.Group = new List<Part>(group);
		}
		else
			change.Operation = InputEvents.SequenceOp.PlacePart;
		InputEvents.SequenceChangeBuffer.Add(change);
	}

	static int IndexOf(ReadOnlySpan<Sequence> sequences, Sequence s)
	{
		for (int i = 0; i < sequences.Length; i++)
			if (sequences[i] == s)
				return i;
		return -1;
	}

	static string FormatBurn(float seconds) =>
		seconds < 60 ? $"{seconds:0}S" : seconds < 3600 ? $"{(int)seconds / 60}:{(int)seconds % 60:00}" : $"{seconds / 3600:0.0}H";
}
