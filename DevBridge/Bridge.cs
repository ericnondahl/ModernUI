using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using StarMap.API;

namespace DevBridge;

/// <summary>
/// A local HTTP server inside the game. Requests arrive on a thread-pool thread; anything
/// that touches the game is queued and run on the game's thread after Program.OnFrame
/// (where the port's benchmark changes the world too), and the answer goes back as JSON.
///
/// Listens on 127.0.0.1 only. Port: KSA_DEVBRIDGE_PORT, default 7878.
/// </summary>
[StarMapMod]
public class Bridge
{
	static readonly ConcurrentQueue<(Func<object?> Work, TaskCompletionSource<object?> Done)> GameThread = new();
	static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

	HttpListener? _listener;
	HarmonyLib.Harmony? _harmony;
	public static long Frame;

	[StarMapAllModsLoaded]
	public void Start()
	{
		_harmony = new HarmonyLib.Harmony("DevBridge");
		SyntheticMouse.Patch(_harmony);
		var port = int.TryParse(Environment.GetEnvironmentVariable("KSA_DEVBRIDGE_PORT"), out var p) ? p : 7878;
		try
		{
			_listener = new HttpListener();
			_listener.Prefixes.Add($"http://127.0.0.1:{port}/");
			_listener.Start();
			_ = Task.Run(Listen);
			Console.WriteLine($"DevBridge: listening on http://127.0.0.1:{port}/");
		}
		catch (Exception e)
		{
			Console.WriteLine($"DevBridge: could not listen on port {port}: {e.Message}");
		}
	}

	[StarMapUnload]
	public void Stop()
	{
		_listener?.Close();
		_harmony?.UnpatchAll("DevBridge");
	}

	[StarMapAfterOnFrame]
	public void AfterOnFrame(double currentPlayerTime, double dtPlayer)
	{
		Frame++;
		while (GameThread.TryDequeue(out var job))
		{
			try { job.Done.SetResult(job.Work()); }
			catch (Exception e) { job.Done.SetException(e); }
		}
	}

	/// <summary>Runs work on the game's thread and waits for its result.</summary>
	public static Task<object?> OnGameThread(Func<object?> work)
	{
		var done = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		GameThread.Enqueue((work, done));
		return done.Task;
	}

	async Task Listen()
	{
		while (_listener is { IsListening: true })
		{
			HttpListenerContext context;
			try { context = await _listener.GetContextAsync(); }
			catch { return; }
			_ = Task.Run(() => Handle(context));
		}
	}

	static async Task Handle(HttpListenerContext context)
	{
		int status = 200;
		object? body;
		try
		{
			var query = context.Request.QueryString;
			string Arg(string name) => query[name] ?? throw new ArgumentException($"missing ?{name}=");
			body = context.Request.Url!.AbsolutePath.TrimEnd('/') switch
			{
				"" or "/help" => Commands.Help,
				"/status" => await OnGameThread(Commands.Status),
				"/screenshot" => await Commands.Screenshot(query["path"]),
				"/vehicles" => await OnGameThread(Commands.VehicleSaveNames),
				"/newgame" => await OnGameThread(Commands.NewGame),
				"/launch" => await OnGameThread(() => Commands.Launch(Arg("vehicle"), Arg("site"))),
				"/fly" => await OnGameThread(() => Commands.Fly(double.Parse(Arg("alt")), query["speed"] is { } s ? double.Parse(s) : null)),
				"/mouse" => await Commands.MouseProbe(float.Parse(Arg("x")), float.Parse(Arg("y"))),
				"/buttons" => await OnGameThread(Commands.ButtonNames),
				"/click" => await Commands.Click(Arg("button")),
				"/burn" => await OnGameThread(() => Commands.CreateBurn(double.Parse(Arg("dv")))),
				"/target" => await OnGameThread(() => Commands.SetTarget(query["id"] ?? "")),
				"/key" => await Commands.Key(Arg("name")),
				"/camera" => await OnGameThread(() => Commands.SetCamera(Arg("mode"))),
				"/set" => await OnGameThread(() => Commands.SetEnum(Arg("enum"), Arg("value"))),
				"/console" => await Commands.Console(Arg("cmd")),
				"/ui" =>await OnGameThread(() => Commands.ShowUi(Arg("show") != "0")),
				"/wait" => await Commands.WaitFrames(int.TryParse(query["frames"], out var n) ? n : 60),
				_ => throw new KeyNotFoundException($"no command {context.Request.Url.AbsolutePath}; GET / lists them"),
			};
		}
		catch (Exception e)
		{
			status = e is KeyNotFoundException ? 404 : e is ArgumentException or FormatException ? 400 : 500;
			body = new { error = e.Message, type = e.GetType().Name };
		}

		var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, Json) + "\n");
		context.Response.StatusCode = status;
		context.Response.ContentType = "application/json";
		await context.Response.OutputStream.WriteAsync(bytes);
		context.Response.Close();
	}
}
