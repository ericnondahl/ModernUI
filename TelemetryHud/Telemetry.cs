using Brutal.Numerics;
using KSA;

namespace TelemetryHud;

/// <summary>One propellant, summed over every tank on the vehicle.</summary>
public readonly record struct Propellant(string Name, float Mass, float Fraction);

/// <summary>
/// The numbers the HUD shows, read once per frame. Keeping game-API calls in this one
/// file means a KSA update that renames something breaks one place, not every gauge.
/// </summary>
public readonly record struct Telemetry(
	string VehicleId,
	double SurfaceSpeed,
	double AltitudeAsl,
	/// <summary>Circular orbital speed at the vehicle's distance from its body's centre.</summary>
	double OrbitalSpeedHere,
	/// <summary>Height of the body's atmosphere above its mean radius; 0 without one.</summary>
	double AtmosphereHeight,
	double MissionSeconds,
	bool BeforeLiftoff,
	float Throttle,
	bool EnginesOn,
	IReadOnlyList<Propellant> Propellants,
	NavballState Navball,
	/// <summary>Delta-v left and thrust-to-weight, as the stock engine gauge shows them.</summary>
	double DeltaV,
	/// <summary>Delta-v of every stage left, as the stock staging panel's total.</summary>
	double TotalDeltaV,
	double ThrustToWeight,
	/// <summary>Apoapsis and periapsis heights above the surface; null where the stock gauge blanks them.</summary>
	double? Apoapsis,
	double? Periapsis,
	UniverseTime NextApoapsis,
	UniverseTime NextPeriapsis,
	UniverseTime Now,
	double SimSpeed,
	bool AutoWarp,
	int NextStage)
{
	public static Telemetry Read(Vehicle vehicle)
	{
		// Speeds, as wide-frog/KSA-ReGUI's FlightData.cs computes them.
		double3 positionCci = vehicle.Orbit.StateVectors.PositionCci;
		double3 orbitalVelocity = vehicle.Orbit.StateVectors.VelocityCci;
		// Surface speed = orbital velocity minus the planet's rotation under the vehicle.
		double3 rotation = double3.Cross(vehicle.Orbit.Parent.GetAngularVelocityCci(), positionCci);
		double3 surfaceVelocity = orbitalVelocity - rotation;

		double radius = positionCci.Length();
		double circular = Math.Sqrt(vehicle.Orbit.Parent.Mu / Math.Max(radius, 1));
		double meanRadius = vehicle.Orbit.Parent is Celestial body ? body.MeanRadius : radius;
		double atmosphere = Math.Max(vehicle.PhysicsEnvironment.AtmosphereRadius.Value() - meanRadius, 0);

		var (missionSeconds, beforeLiftoff) = MissionClock.Update(vehicle);

		return new Telemetry(
			VehicleId: vehicle.Id,
			SurfaceSpeed: surfaceVelocity.Length(),
			AltitudeAsl: vehicle.GetBarometricAltitude(),
			OrbitalSpeedHere: circular,
			AtmosphereHeight: atmosphere,
			MissionSeconds: missionSeconds,
			BeforeLiftoff: beforeLiftoff,
			Throttle: vehicle.GetManualThrottle(),
			EnginesOn: vehicle.IsAnyEngineActive(),
			Propellants: ReadPropellants(vehicle.Parts),
			Navball: NavballState.Read(vehicle),
			DeltaV: vehicle.NavBallData.DeltaV,
			TotalDeltaV: vehicle.Parts.PerformanceSequences.TotalDeltaV,
			ThrustToWeight: vehicle.NavBallData.ThrustWeightRatio,
			Apoapsis: ReadApoapsis(vehicle),
			Periapsis: ReadPeriapsis(vehicle),
			NextApoapsis: vehicle.NextApoapsisTime,
			NextPeriapsis: vehicle.NextPeriapsisTime,
			Now: Universe.GetElapsedTime(),
			SimSpeed: Universe.GetSimulationSpeed(),
			AutoWarp: Universe.IsAutoWarpActive,
			NextStage: GameActions.NextStage(vehicle));
	}

	// As Program's gauge update blanks them: apoapsis at or below 1 m, periapsis at or below
	// 1 m or on a trajectory that ends in an impact.
	static double? ReadApoapsis(Vehicle v) => v.Orbit.ApoapsisAltitude is var a && a > 1 ? a : null;

	static double? ReadPeriapsis(Vehicle v)
	{
		double p = v.Orbit.PeriapsisAltitude;
		bool impact = v.FlightPlan.FirstPatch?.EndTransition == PatchTransition.Impact;
		return p > 1 && !impact ? p : null;
	}

	// As the game's Resources panel gathers its rows (ResourcesPanel.Gather): every
	// substance in the vehicle's tanks, its stored and container volumes summed, without
	// the life-support tanks and the habitats' food and scrubber canisters.
	static readonly HashSet<Mole> LifeSupport = [];
	static readonly Dictionary<SubstancePhase, (float Mass, float Stored, float Capacity)> Sums = [];

	static List<Propellant> ReadPropellants(PartTree parts)
	{
		LifeSupport.Clear();
		Sums.Clear();
		foreach (Tank tank in parts.Tanks.Modules)
			if (tank.RoleAffinity.IsLifeSupportOnly())
				foreach (Mole mole in tank.Moles)
					LifeSupport.Add(mole);
		foreach (Habitat habitat in parts.Modules.Get<Habitat>())
		{
			LifeSupport.Add(habitat.Food);
			LifeSupport.Add(habitat.Canisters);
		}

		var moles = parts.Moles.ModulesAndStates.GetEnumerator();
		while (moles.MoveNext())
		{
			var entry = moles.Current;
			if (LifeSupport.Contains(entry.Module))
				continue;
			var phase = entry.Module.SubstancePhase;
			float mass = entry.State.Mass;
			Sums.TryGetValue(phase, out var sum);
			Sums[phase] = (sum.Mass + mass, sum.Stored + entry.Module.GetStoredVolume(mass), sum.Capacity + entry.Module.ContainerVolume);
		}

		var result = new List<Propellant>(Sums.Count);
		foreach (var (phase, sum) in Sums)
			if (sum.Capacity > 0)
				result.Add(new Propellant(phase.Substance.ShortName.ToUpperInvariant(), sum.Mass, Math.Clamp(sum.Stored / sum.Capacity, 0, 1)));
		// The biggest tanks first: the main propellants before RCS and the like.
		result.Sort((a, b) => b.Mass.CompareTo(a.Mass));
		return result;
	}
}

/// <summary>
/// T+ for the controlled vehicle: from the moment it last left the ground (its situation
/// lost all contact). A vehicle that hasn't touched the ground since we started watching
/// it counts from when it was made (Vehicle.LaunchGameTime), as the game's own clock does.
/// </summary>
static class MissionClock
{
	static string? _vehicle;
	static double _lastNow = double.NegativeInfinity;
	static bool _grounded;
	static double? _liftoff;

	public static (double Seconds, bool BeforeLiftoff) Update(Vehicle vehicle)
	{
		double now = Universe.GetElapsedSeconds();
		bool grounded = vehicle.Situation.HasAnyContact();
		// Another vehicle, or the clock went back (a new game, a loaded save): start over.
		if (vehicle.Id != _vehicle || now < _lastNow)
		{
			_vehicle = vehicle.Id;
			_liftoff = null;
			_grounded = grounded;
		}
		_lastNow = now;
		if (grounded)
			_liftoff = null;
		else if (_grounded)
			_liftoff = now;
		_grounded = grounded;

		if (grounded)
			return (0, true);
		return (now - (_liftoff ?? vehicle.LaunchGameTime.Seconds()), false);
	}
}
