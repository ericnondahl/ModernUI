using KSA;
using StarMap.API;

namespace TelemetryHud;

/// <summary>
/// Entry point. StarMap finds this class by its [StarMapMod] attribute and calls the
/// attributed methods at each stage of the game's life.
/// </summary>
[StarMapMod]
public class TelemetryHud
{
	[StarMapAfterGui]
	public void AfterGui(double dt)
	{
		// Only draw in flight view: not in the editor, not in map view.
		Vehicle? vehicle = Program.ControlledVehicle;
		bool inFlight = vehicle != null
			&& !Program.EditorFlag
			&& Program.GetController() != Program.GetMapController();
		if (!inFlight)
			return;

		Telemetry t = Telemetry.Read(vehicle!);
		Hud.Draw(t);
	}
}
