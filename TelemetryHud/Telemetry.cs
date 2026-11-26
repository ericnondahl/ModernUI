using Brutal.Numerics;
using KSA;

namespace TelemetryHud;

/// <summary>
/// The numbers the HUD shows, read once per frame. Keeping game-API calls in this one
/// file means a KSA update that renames something breaks one place, not every gauge.
/// Calls mirror wide-frog/KSA-ReGUI's FlightData.cs, which is known to work.
/// </summary>
public readonly record struct Telemetry(double SurfaceSpeed, double AltitudeAsl)
{
	public static Telemetry Read(Vehicle vehicle)
	{
		double3 positionCci = vehicle.Orbit.StateVectors.PositionCci;
		double3 orbitalVelocity = vehicle.Orbit.StateVectors.VelocityCci;
		// Surface speed = orbital velocity minus the planet's rotation under the vehicle.
		double3 rotation = double3.Cross(vehicle.Orbit.Parent.GetAngularVelocityCci(), positionCci);
		double3 surfaceVelocity = orbitalVelocity - rotation;

		return new Telemetry(
			SurfaceSpeed: surfaceVelocity.Length(),
			AltitudeAsl: vehicle.GetBarometricAltitude());
	}
}
