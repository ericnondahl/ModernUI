using System.Reflection;
using ImGui = Brutal.ImGuiApi.ImGui;
using Brutal.ImGuiApi;
using System.Runtime.InteropServices;
using Brutal.Numerics;
using KSA;

namespace DevBridge;

/// <summary>
/// What the bridge can do. The world-changing commands are adapted from the Mac port's
/// benchmark (renancamm/ksa-macos-port, src/game/benchmark, MIT): World.NewWorld,
/// Vehicles.Launch and Vehicles.Fly.
/// </summary>
public static class Commands
{
	public static readonly object Help = new
	{
		commands = new Dictionary<string, string>
		{
			["/status"] = "frame, flight state, controlled vehicle, altitude, speed",
			["/screenshot?path=<png>"] = "PNG of the next frame, UI included (Mac port only); path optional",
			["/vehicles"] = "vehicle save names, for /launch",
			["/newgame"] = "a fresh world: clock at zero, no vehicles",
			["/launch?vehicle=<save>&site=<id>"] = "put a vehicle save on a launch site of the home body and control it, e.g. site=CCSFS LC-39A",
			["/fly?alt=<m>&speed=<m/s>"] = "teleport the controlled vehicle onto a vertical climb through that altitude; without speed, a circular orbit",
			["/console?cmd=<line>"] = "run a game console command (simspeed, refill, loadvehicle, control, exit, ...) and return its output",
			["/set?enum=<KSA enum>&value=<name>"] = "a flight computer setting as the stock buttons set it, e.g. enum=FlightComputerAttitudeTrackTarget&value=Prograde, enum=VehicleEngine&value=MainIgnite",
			["/buttons"] = "TelemetryHud's buttons by id",
			["/click?button=<id>"] = "click a TelemetryHud button through ImGui's input, as the mouse would",
			["/burn?dv=<m/s>"] = "a prograde burn at the next apoapsis, selected for editing",
			["/target?id=<vehicle or body>"] = "set the controlled vehicle's target (empty id clears it)",
			["/camera?mode=Orbit|Map|Free|IVA|Fixed"] = "switch the view (map view, flight view...)",
			["/ui?show=0|1"] = "hide or show the game's whole UI (mods' too)",
			["/wait?frames=<n>"] = "return after n more frames (default 60)",
		},
	};

	// ---- status ----------------------------------------------------------------------

	public static object? Status()
	{
		var v = Program.ControlledVehicle;
		bool map = Program.GetController() == Program.GetMapController();
		object? vehicle = null;
		if (v != null)
		{
			double3 position = v.Orbit.StateVectors.PositionCci;
			double3 surface = v.Orbit.StateVectors.VelocityCci - double3.Cross(v.Orbit.Parent.GetAngularVelocityCci(), position);
			vehicle = new
			{
				id = v.Id,
				body = v.Orbit.Parent is Astronomical a ? a.Id : null,
				altitudeAsl = v.GetBarometricAltitude(),
				surfaceSpeed = surface.Length(),
				orbitalSpeed = v.Orbit.StateVectors.VelocityCci.Length(),
				rcs = v.FlightComputer.RCSMode.ToString(),
				navballFrame = v.NavBallData.Frame.ToString(),
				enginesOn = v.IsAnyEngineActive(),
				attitude = $"{v.FlightComputer.AttitudeMode} {v.FlightComputer.AttitudeTrackTarget}",
				nextStage = v.Parts.SequenceList.GetNextSequenceNumber(),
			};
		}
		return new
		{
			frame = Bridge.Frame,
			editor = Program.EditorFlag,
			map,
			inFlight = v != null && !Program.EditorFlag && !map,
			simSpeed = Universe.GetSimulationSpeed(),
			autoWarp = Universe.IsAutoWarpActive,
			resourceGroupsOpen = ResourceGroupsPanel.IsOpen,
			gameSeconds = Universe.GetElapsedSeconds(),
			vehicle,
		};
	}

	// ---- screenshots -----------------------------------------------------------------

	[DllImport("libvulkan.1.dylib", EntryPoint = "ksaMtlScreenshot")]
	static extern void KsaMtlScreenshot([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

	public static async Task<object?> Screenshot(string? path)
	{
		path ??= Path.Combine(KSA.Constants.DocumentsFolderPath, "devbridge", $"shot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
		path = Path.GetFullPath(path);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		if (File.Exists(path))
			throw new ArgumentException($"{path} exists; screenshots get a new name each time");

		try { KsaMtlScreenshot(path); }
		catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
		{
			throw new PlatformNotSupportedException("screenshots need the Mac port's KSA Metal driver", e);
		}

		// The driver encodes the PNG on a queue of its own after the next present, and
		// writes it whole; wait until it is there and its size holds.
		var deadline = DateTime.UtcNow.AddSeconds(15);
		long last = -1;
		while (DateTime.UtcNow < deadline)
		{
			await Task.Delay(100);
			long size = File.Exists(path) ? new FileInfo(path).Length : -1;
			if (size > 0 && size == last)
				return new { path, bytes = size };
			last = size;
		}
		throw new TimeoutException($"no screenshot at {path} after 15 s (is the game window minimised?)");
	}

	public static async Task<object?> WaitFrames(int frames)
	{
		long until = Bridge.Frame + frames;
		var deadline = DateTime.UtcNow.AddSeconds(60);
		while (Bridge.Frame < until && DateTime.UtcNow < deadline)
			await Task.Delay(16);
		return new { frame = Bridge.Frame };
	}

	// ---- world ---------------------------------------------------------------------------

	// The benchmark's World.NewWorld: a new game's world, its clock at the start, no vehicles.
	public static object? NewGame()
	{
		JobSystems.WaitAll();
		var fresh = UniverseData.Create();
		fresh.GameTime = new UniverseTimeReference(UniverseTime.Zero);
		foreach (var system in fresh.CelestialSystems)
		{
			system.Vehicles.Clear();
			system.GroundClutter.Clear();
		}
		fresh.KittenRoster = KittenRosterData.Create();
		Universe.DeserializeSave(fresh);
		Program.OnGameLoaded();
		return new { ok = true };
	}

	// The player's vehicle saves and the game's default ones (Content/Core/defaultvehicles):
	// both collections are internal or private, hence reflection.
	static LookupCollection<VehicleSave> SavesOf(Type owner)
		=> owner.GetProperty("Saves", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
			?.GetValue(null) as LookupCollection<VehicleSave>
			?? throw new MissingMemberException($"{owner.Name}.Saves is gone; KSA changed");

	static IEnumerable<VehicleSave> Saves()
	{
		VehicleSaves.Refresh();
		foreach (var save in SavesOf(typeof(VehicleSaves)).AsSpan().ToArray())
			yield return save;
		foreach (var save in SavesOf(typeof(DefaultVehicleSaves)).AsSpan().ToArray())
			yield return save;
	}

	public static object? VehicleSaveNames()
	{
		var names = new List<string>();
		foreach (var save in Saves())
			names.Add(save.Id);
		return new { vehicles = names };
	}

	// The benchmark's Vehicles.Launch: a vehicle save on a launch site of the home body, as
	// the game's launch menu makes it, then given control and the cameras.
	public static object? Launch(string name, string siteId)
	{
		var system = Universe.CurrentSystem ?? throw new InvalidOperationException("no game loaded");
		if (system.HomeBody is not Celestial body)
			throw new InvalidOperationException("no home body");
		var site = body.GetLocation(siteId) ?? throw new ArgumentException($"'{body.Id}' has no site '{siteId}'");
		var save = Saves().FirstOrDefault(s => s.Id == name)
			?? throw new ArgumentException($"no vehicle save '{name}'; see /vehicles");

		var tree = save.Load(Program.MainViewport) ?? throw new InvalidOperationException($"'{name}' did not load");
		var mass = tree.ComputeInertMassPropertiesAsmb() + tree.ComputePropellantMassPropertiesAsmb();
		var (min, max) = tree.ComputeBoundingBoxAsmb();
		var state = Vehicle.GetInitialKinematicStateForLocation(body, Universe.GetNextSimStep().NextTime,
			site.Latitude.ToDegrees(), site.Longitude.ToDegrees(), min, max, double3.Unpack(in mass.Offset), new Random(1).RandomColor());
		var vehicle = Vehicle.CreateVehicle(system, state.Body2Cce, state.BodyRates, system.HomeBody, name, tree.Root, state.Orbit);
		state.Orbit.Parent!.Children.Add(vehicle);
		vehicle.UpdatePerFrameData();

		// Crew, so the vehicle is controllable (the game greys out engine controls otherwise):
		// a kitten in every seat, as the benchmark's @crew seats them.
		int seated = 0;
		for (int i = 0; i < 8; i++)
		{
			string kittenName = $"Test Kitten {i + 1}";
			var kitten = Universe.KittenRoster.Find(kittenName);
			if (kitten == null)
			{
				kitten = new KittenRosterEntryData(kittenName, "HunterKitten");
				Universe.KittenRoster.Kittens.Add(kitten);
			}
			if (vehicle.AddCrewToFirstAvailableSeat(kitten.NameHash) != CrewSeatResult.Seated)
				break;
			kitten.StartMission(vehicle.Id);
			seated++;
		}

		Program.ControlledVehicle = vehicle;
		Program.MainViewport.MapCamera.SetFollow(vehicle, tidalLocking: false, changeControl: true, alert: false);
		Program.MainViewport.BaseCamera.SetFollow(vehicle, tidalLocking: false, changeControl: true, alert: false);
		return new { vehicle = vehicle.Id, site = siteId, crew = seated };
	}

	// The benchmark's Vehicles.Fly: the controlled vehicle onto a vertical climb through
	// that altitude (the ground's turning included), or with no speed a circular orbit
	// going east, through the game's own teleport.
	public static object? Fly(double altitude, double? climb)
	{
		if (Program.ControlledVehicle is not { Parent: Celestial body } v)
			throw new InvalidOperationException("no controlled vehicle near a body");
		var upCce = double3.Normalize(v.GetPositionEcl() - body.GetPositionEcl());
		var upCci = upCce.Transform(body.GetCci2Cce().Inverse());
		var positionCci = upCci * (body.MeanRadius + altitude);
		var spin = ((IParentBody)body).GetAngularVelocityCci();
		if (climb == null && double3.Cross(spin, upCci).Length() < 1e-9 * spin.Length())
			throw new InvalidOperationException("no eastward orbit over a pole");
		var velocityCci = climb is { } speed
			? double3.Cross(spin, positionCci) + upCci * speed
			: double3.Normalize(double3.Cross(spin, upCci)) * Math.Sqrt(((IParentBody)body).Mu / positionCci.Length());
		var orbit = Orbit.CreateFromStateCci(body, Universe.GetNextSimStep().NextTime, positionCci, velocityCci, new Random(1).RandomColor());
		InputEvents.TeleportInputBuffer.Add(new InputEvents.TeleportInputData { Vehicle = v, Orbit = orbit });
		return new { vehicle = v.Id, altitude, speed = climb };
	}

	// ---- the game's own console ------------------------------------------------------

	// Runs a line in the game's console (the ~ window): every [TerminalAction] the game
	// registers, e.g. "simspeed 10", "refill Rocket", "loadvehicle <name>", "control <id>",
	// "defaultvehiclesavefiles", "exit". Commands print to the game's log, so the answer is
	// what the log gained while the command ran.
	public static async Task<object?> Console(string line)
	{
		string? log = GameLog();
		long start = log != null ? new FileInfo(log).Length : 0;
		var ok = await Bridge.OnGameThread(() => Program.TerminalInterface.Execute(line));
		await WaitFrames(10);
		await Task.Delay(250); // the log writer runs on a thread of its own
		var output = new List<string>();
		if (log != null)
		{
			using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			stream.Seek(start, SeekOrigin.Begin);
			using var reader = new StreamReader(stream);
			while (reader.ReadLine() is { } l)
				output.Add(Ansi.Replace(l, ""));
		}
		return new { ok, output };
	}

	static readonly System.Text.RegularExpressions.Regex Ansi = new(@"\x1b\[[0-9;]*m");

	// The newest KittenSpaceAgency.*.log in the profile's logs folder: this run's.
	static string? GameLog()
	{
		var dir = Path.Combine(KSA.Constants.DocumentsFolderPath, "logs");
		return Directory.Exists(dir)
			? new DirectoryInfo(dir).GetFiles("KittenSpaceAgency.*.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName
			: null;
	}

	// The flight computer's settings, as the stock buttons set them (Vehicle.SetEnum):
	// set?enum=FlightComputerAttitudeTrackTarget&value=Prograde, VehicleReferenceFrame=EnuBody,
	// VehicleEngine=MainIgnite, FlightComputerAttitudeMode=Manual, ...
	public static object? SetEnum(string typeName, string value)
	{
		var vehicle = Program.ControlledVehicle ?? throw new InvalidOperationException("no controlled vehicle");
		var type = typeof(Vehicle).Assembly.GetType("KSA." + typeName);
		if (type is not { IsEnum: true })
			throw new ArgumentException($"no enum KSA.{typeName}");
		var e = (Enum)Enum.Parse(type, value, ignoreCase: true);
		vehicle.SetEnum(e);
		return new { set = $"{type.Name}.{e}", attitude = vehicle.FlightComputer.AttitudeMode.ToString(),
			track = vehicle.FlightComputer.AttitudeTrackTarget.ToString(), frame = vehicle.NavBallData.Frame.ToString() };
	}

	// ---- clicking HUD buttons -----------------------------------------------------------

	// TelemetryHud records each button's rectangle by id every frame (Controls.Rects).
	static Dictionary<string, (float2 Min, float2 Max)> HudButtons()
	{
		var type = AppDomain.CurrentDomain.GetAssemblies()
			.Select(a => a.GetType("TelemetryHud.Controls")).FirstOrDefault(t => t != null)
			?? throw new InvalidOperationException("TelemetryHud isn't loaded");
		return type.GetField("Rects", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as Dictionary<string, (float2 Min, float2 Max)>
			?? throw new MissingMemberException("TelemetryHud.Controls.Rects");
	}

	public static async Task<object?> MouseProbe(float x, float y)
	{
		await Bridge.OnGameThread(() => { ImGui.GetIO().AddMousePosEvent(x, y); return null; });
		await WaitFrames(3);
		return await Bridge.OnGameThread(() =>
		{
			var io = ImGui.GetIO();
			return new
			{
				mouse = new[] { io.MousePos.X, io.MousePos.Y },
				display = new[] { io.DisplaySize.X, io.DisplaySize.Y },
				wantCaptureMouse = io.WantCaptureMouse,
				anyWindowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow),
			};
		});
	}

	public static object? ButtonNames() => new { buttons = HudButtons().Keys.OrderBy(k => k).ToArray() };

	// A real click through ImGui's input queue: move, press, release, a few frames apart,
	// so the HUD sees it as the player's mouse would make it.
	public static async Task<object?> Click(string id)
	{
		var rect = (ValueTuple<float2, float2>)(await Bridge.OnGameThread(() =>
			HudButtons().TryGetValue(id, out var r) ? r : throw new ArgumentException($"no button '{id}'; see /buttons")))!;
		var (min, max) = rect;
		var at = (min + max) / 2;
		SyntheticMouse.Active = true;
		try
		{
			await Bridge.OnGameThread(() => { ImGui.GetIO().AddMousePosEvent(at.X, at.Y); return null; });
			await WaitFrames(3);
			await Bridge.OnGameThread(() => { ImGui.GetIO().AddMouseButtonEvent(0, true); return null; });
			await WaitFrames(3);
			await Bridge.OnGameThread(() => { ImGui.GetIO().AddMouseButtonEvent(0, false); return null; });
			await WaitFrames(3);
			// Off the window, so the button isn't left hovered in later screenshots.
			await Bridge.OnGameThread(() => { ImGui.GetIO().AddMousePosEvent(-float.MaxValue, -float.MaxValue); return null; });
			await WaitFrames(2);
		}
		finally
		{
			SyntheticMouse.Active = false;
		}
		return new { clicked = id, at = new[] { at.X, at.Y } };
	}

	// A burn at the next apoapsis, prograde dv m/s, made as the map's context menu makes one
	// (BurnContextMenu.CreateBurn), and selected for editing.
	public static object? CreateBurn(double prograde)
	{
		var v = Program.ControlledVehicle ?? throw new InvalidOperationException("no controlled vehicle");
		var patch = v.FlightPlan.Patches.Count > 0 ? v.FlightPlan.Patches[0] : throw new InvalidOperationException("no flight plan");
		var now = Universe.GetElapsedTime();
		var start = patch.StartTime > now ? patch.StartTime : now;
		var at = patch.Orbit.GetNextApoapsisTime(start) ?? throw new InvalidOperationException("no apoapsis ahead");
		var burn = Burn.Create(patch.Orbit.GetPointAt(at), at.Seconds(), new double3(prograde, 0, 0), patch, v);
		InputEvents.BurnUpdateBuffer.Add(new InputEvents.BurnUpdateData { FlightComputer = v.FlightComputer, Burn = burn, AddBurn = true });
		Program.ActiveBurn = burn;
		return new { burnAt = at.Seconds(), inSeconds = at.Seconds() - now.Seconds(), prograde };
	}

	// The controlled vehicle's target, by id (another vehicle or a body); empty clears it.
	public static object? SetTarget(string id)
	{
		var v = Program.ControlledVehicle ?? throw new InvalidOperationException("no controlled vehicle");
		IOrbiter? target = null;
		if (id.Length > 0)
			target = Universe.CurrentSystem?.Get(id) as IOrbiter ?? throw new ArgumentException($"no orbiter '{id}'");
		InputEvents.ChangeTargetBuffer.Add(new InputEvents.ChangeTargetData { Vehicle = v, Target = target });
		return new { target = target?.Id };
	}

	public static object? SetCamera(string mode)
	{
		var m = Enum.Parse<CameraMode>(mode, ignoreCase: true);
		Program.SetCameraMode(m);
		return new { camera = m.ToString() };
	}

	public static object? ShowUi(bool show)
	{
		Program.DrawUI = show;
		return new { drawUi = show };
	}
}
