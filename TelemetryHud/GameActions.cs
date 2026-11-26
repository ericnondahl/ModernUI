using Brutal.GlfwApi;
using KSA;

namespace TelemetryHud;

/// <summary>
/// What the HUD's controls do, through the same paths as the game's own buttons and keys,
/// so they keep the game's checks (control lockouts, alerts, sounds) and its timing.
/// </summary>
public static class GameActions
{
	// ---- flight computer settings: autopilot, engines, RCS, burns, frames ------------
	// As GaugeButtonFlightComputer does: queued, then Vehicle.SetEnum or ToggleEnum.

	public static void Apply(Vehicle v, Enum value, bool toggle = false) =>
		InputEvents.FlightComputerInputBuffer.Add(new InputEvents.FlightComputerInputData
		{
			Vehicle = v,
			Toggle = toggle,
			EnumValue = value,
		});

	/// <summary>Whether the stock button for this setting would be lit.</summary>
	public static bool IsOn(Vehicle v, Enum value) => v.IsSet(value, false);

	/// <summary>Whether the stock button for this setting would be greyed out.</summary>
	public static bool IsDisabled(Vehicle v, Enum value) => v.IsFlightComputerDisabled(value);

	// ---- staging: the stock key (Space, on release) ------------------------------------

	public static void ActivateNextStage(Vehicle v) =>
		InputEvents.VehicleInputBuffer.Add(new InputEvents.VehicleInputData
		{
			Vehicle = v,
			Action = InputAction.CameraUp,
			KeyAction = GlfwKeyAction.Release,
		});

	/// <summary>The next stage's number, or -1 when every stage has fired.</summary>
	public static int NextStage(Vehicle v) => v.Parts.SequenceList.GetNextSequenceNumber();

	// ---- time warp: the stock keys and menu ----------------------------------------------

	public static void WarpFaster() => Warp(InputAction.SimSpeedIncrease);
	public static void WarpSlower() => Warp(InputAction.SimSpeedDecrease);
	public static void WarpRealtime() => Warp(InputAction.SimSpeedDefault);

	static void Warp(InputAction action) =>
		InputEvents.SimSpeedBuffer.Add(new InputEvents.SimSpeedData { Action = action });

	/// <summary>
	/// Warps to a moment, or stops if already warping there, as the stock warp-to buttons
	/// (GaugeButtonWarpTo) do.
	/// </summary>
	public static void ToggleWarpTo(UniverseTime t) =>
		InputEvents.AutoWarpBuffer.Add(new InputEvents.AutoWarpData { StopWarp = IsWarpingTo(t), WarpToTime = t });

	/// <summary>
	/// Whether the auto-warp is heading for this moment. Event times (the next apoapsis, say)
	/// are worked out again each frame and drift a little, so "the same moment" is within a
	/// minute, or 1% of the time left, of where the warp is heading.
	/// </summary>
	public static bool IsWarpingTo(UniverseTime t)
	{
		if (!Universe.IsAutoWarpActive || Universe.AutoWarpTime is not { } target)
			return false;
		double now = Universe.GetElapsedSeconds();
		double tolerance = Math.Max(60, 0.01 * Math.Abs(t.Seconds() - now));
		return Math.Abs(target.Seconds() - t.Seconds()) < tolerance;
	}

	/// <summary>
	/// Whether a warp-to button for this moment is usable, as GaugeButtonWarpTo decides: not
	/// landed, and the moment exists.
	/// </summary>
	public static bool CanWarpTo(Vehicle v, UniverseTime t) =>
		v.Situation != Situation.Landed && !double.IsInfinity(t.Seconds()) && t < UniverseTime.EndOfTime && t.Seconds() > 60.0;
}
