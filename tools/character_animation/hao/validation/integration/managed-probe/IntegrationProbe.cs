using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;

public partial class IntegrationProbe : Node
{
    private object? _controller;
    private object? _animator;
    private Node? _visuals;
    private string _stage = "managed_ready";
    private int _mainThread;
    private string _resultPath = "";
    private readonly Dictionary<string, object?> _report = new()
    {
        ["ok"] = false,
        ["probe_kind"] = "actual_compiled_mod_factory_and_game_animator",
        ["gameplay_bootstrapped"] = false,
        ["main_file_initialize_called"] = false,
        ["evidence_scope"] = "actual compiled factory, NCreatureVisuals._Ready, MegaSpine wrappers and native track playback; no NGame/NCreature/ModelDb"
    };

    public override void _Ready()
    {
        _mainThread = System.Environment.CurrentManagedThreadId;
        Callable.From(async () => await Run()).CallDeferred();
    }

    private async Task Run()
    {
        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(ProjectSettings.GlobalizePath("res://probe-config.json")))
                ?? throw new InvalidOperationException("Missing configuration");
            _resultPath = config["result"];
            _report["engine"] = Engine.GetVersionInfo().ToString();
            _report["main_thread"] = _mainThread;
            _report["mod_dll_sha256"] = Hash(config["mod_dll"]);
            _report["mod_pck_sha256"] = Hash(config["mod_pck"]);
            _stage = "mount_compiled_mod_pack";
            if (!ProjectSettings.LoadResourcePack(config["mod_pck"], true))
                throw new InvalidOperationException("Cannot mount compiled mod PCK");
            var resourcePath = config["resource"];
            _report["resource_path"] = resourcePath;
            _report["resource_exists"] = ResourceLoader.Exists(resourcePath);
            if (!ResourceLoader.Exists(resourcePath))
                throw new InvalidOperationException("Pack resource path missing");
            using (var resource = ResourceLoader.Load<Resource>(resourcePath))
            {
                if (resource == null) throw new InvalidOperationException("Resource returned null");
                _report["native_resource_class"] = resource.GetClass().ToString();
                if (resource.GetClass() != "SpineSkeletonDataResource")
                    throw new InvalidOperationException("Not a native SpineSkeletonDataResource");
            }

            _stage = "load_assemblies";
            var directories = new[]
            {
                config["game_data"],
                Path.GetDirectoryName(config["baselib_dll"])!,
                Path.GetDirectoryName(config["mod_dll"])!
            };
            AssemblyLoadContext.Default.Resolving += (_, name) =>
            {
                foreach (var directory in directories)
                {
                    var file = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
                }
                return null;
            };
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(config["mod_dll"]);
            var factory = assembly.GetType(config["factory_type"], true)!;
            _report["factory_type"] = factory.FullName;
            _stage = "invoke_actual_factory";
            _visuals = InvokeStatic(factory, "Create") as Node
                ?? throw new InvalidOperationException("Actual factory did not return a Godot Node");
            _report["managed_visuals_type"] = _visuals.GetType().FullName;
            if (_visuals.GetType().FullName != "MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals")
                throw new InvalidOperationException("Factory did not return real NCreatureVisuals");
            AddChild(_visuals);
            await Frame();
            var required = new Dictionary<string, string>
            {
                ["%Visuals"] = "SpineSprite", ["%Bounds"] = "Control",
                ["%IntentPos"] = "Marker2D", ["%CenterPos"] = "Marker2D", ["%FormVfx"] = "Control"
            };
            var markers = new List<Dictionary<string, object?>>();
            foreach (var (path, expectedClass) in required)
            {
                var node = _visuals.GetNodeOrNull<Node>(path)
                    ?? throw new InvalidOperationException("Unique node missing: " + path);
                var actualClass = node.GetClass().ToString();
                markers.Add(new() { ["path"] = path, ["class"] = actualClass,
                    ["unique_name_in_owner"] = node.UniqueNameInOwner,
                    ["owner_is_visuals"] = node.Owner == _visuals });
                if (actualClass != expectedClass)
                    throw new InvalidOperationException(path + " has wrong native class");
            }
            _report["unique_nodes"] = markers;
            _report["visuals_in_tree"] = _visuals.IsInsideTree();
            _report["visuals_ready"] = _visuals.IsNodeReady();
            _stage = "actual_visuals_ready";
            var hasSpine = (bool)(Property(_visuals, "HasSpineAnimation") ?? false);
            _report["has_spine_animation"] = hasSpine;
            if (!hasSpine)
                throw new InvalidOperationException("Real NCreatureVisuals._Ready disabled SpineBody");
            _controller = Property(_visuals, "SpineBody")
                ?? throw new InvalidOperationException("SpineBody missing");
            for (var frame = 0; frame < 120; frame++)
            {
                if ((bool)Invoke(_controller, "IsAnimationStateReady")!) break;
                await Frame();
            }
            if (!(bool)Invoke(_controller, "IsAnimationStateReady")!)
                throw new InvalidOperationException("Native animation state did not become ready");
            foreach (var id in new[] { "idle_loop", "attack", "cast", "hurt", "die" })
                if (!(bool)Invoke(_controller, "HasAnimation", id)!)
                    throw new InvalidOperationException("Native animation missing: " + id);

            _stage = "create_actual_animator";
            _animator = InvokeStatic(factory, "CreateAnimator", _controller)
                ?? throw new InvalidOperationException("CreateAnimator returned null");
            _report["managed_animator_type"] = _animator.GetType().FullName;
            if (_animator.GetType().FullName != "MegaCrit.Sts2.Core.Animation.CreatureAnimator")
                throw new InvalidOperationException("Not real game CreatureAnimator");
            foreach (var trigger in new[] { "Attack", "Cast", "PowerUp", "Idle" })
                if (!(bool)Invoke(_animator, "HasTrigger", trigger)!)
                    throw new InvalidOperationException("Game trigger missing: " + trigger);
            ResetTrack();
            var initial = Snapshot();
            _report["initial_track"] = initial;
            if ((string?)initial["animation"] != "idle_loop")
                throw new InvalidOperationException("Initial animation is not idle_loop");

            _stage = "actual_runtime_playback";
            var playback = new List<Dictionary<string, object?>>();
            foreach (var (trigger, expected) in new[]
                     { ("Attack", "attack"), ("Cast", "cast"), ("PowerUp", "cast") })
            {
                Invoke(_animator, "SetTrigger", trigger);
                ResetTrack();
                var start = Snapshot();
                if ((string?)start["animation"] != expected)
                    throw new InvalidOperationException("Trigger mapped to wrong native animation: " + trigger);
                var duration = Convert.ToDouble(start["duration"]);
                if (!(duration > 0 && duration < 5))
                    throw new InvalidOperationException("Invalid native animation duration");
                await Seconds(Math.Min(0.20, duration * 0.25));
                var advanced = Snapshot();
                if ((string?)advanced["animation"] != expected || Convert.ToDouble(advanced["track_time"]) <= 0.01)
                    throw new InvalidOperationException("Native track did not actually advance");
                await Seconds(duration + 0.25);
                var returned = Snapshot();
                if ((string?)returned["animation"] != "idle_loop")
                    throw new InvalidOperationException("Actual animator failed to return to idle_loop");
                playback.Add(new() { ["trigger"] = trigger, ["expected"] = expected,
                    ["start"] = start, ["advanced"] = advanced, ["returned"] = returned });
            }
            _report["playback"] = playback;
            Invoke(_animator, "SetTrigger", "Idle");
            if ((string?)Snapshot()["animation"] != "idle_loop")
                throw new InvalidOperationException("Idle trigger mapping failed");
            _report["ok"] = true;
            _stage = "complete";
            WriteReport();
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            var error = exception is TargetInvocationException { InnerException: not null } wrapper
                ? wrapper.InnerException! : exception;
            _report["error"] = error.ToString();
            GD.PushError("FACTORY_PROBE_FAILED " + _stage + ": " + error.Message);
            WriteReport();
            GetTree().Quit(1);
        }
    }

    private object State() => Invoke(_controller!, "GetAnimationState")
        ?? throw new InvalidOperationException("No animation state");

    private void ResetTrack()
    {
        var state = State();
        try
        {
            var track = Invoke(state, "GetCurrent", 0)
                ?? throw new InvalidOperationException("No current native track");
            try { Invoke(track, "SetTimeScale", 1f); Invoke(track, "SetTrackTime", 0f); }
            finally { (track as IDisposable)?.Dispose(); }
        }
        finally { (state as IDisposable)?.Dispose(); }
    }

    private Dictionary<string, object?> Snapshot()
    {
        if (System.Environment.CurrentManagedThreadId != _mainThread)
            throw new InvalidOperationException("Probe continued off Godot main thread");
        var state = State();
        try
        {
            var track = Invoke(state, "GetCurrent", 0)
                ?? throw new InvalidOperationException("No native track");
            try
            {
                var queued = Invoke(state, "GetQueuedAnimationNames", 0) as IEnumerable<string>;
                return new()
                {
                    ["animation"] = Invoke(track, "GetAnimationName"),
                    ["duration"] = Invoke(track, "GetAnimationDuration"),
                    ["track_time"] = Invoke(track, "GetTrackTime"),
                    ["loop"] = Invoke(track, "IsLoop"),
                    ["queue"] = queued?.ToArray() ?? []
                };
            }
            finally { (track as IDisposable)?.Dispose(); }
        }
        finally { (state as IDisposable)?.Dispose(); }
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Seconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static object? Property(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(target);
    private static object? Invoke(object target, string name, params object?[] args) =>
        Method(target.GetType(), name, false, args.Length).Invoke(target, args);
    private static object? InvokeStatic(Type type, string name, params object?[] args) =>
        Method(type, name, true, args.Length).Invoke(null, args);
    private static MethodInfo Method(Type type, string name, bool isStatic, int count) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
            (isStatic ? BindingFlags.Static : BindingFlags.Instance))
            .Single(m => m.Name == name && m.GetParameters().Length == count);
    private static string Hash(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    private void WriteReport()
    {
        _report["stage"] = _stage;
        var path = string.IsNullOrEmpty(_resultPath)
            ? ProjectSettings.GlobalizePath("user://factory-result.json") : _resultPath;
        File.WriteAllText(path, JsonSerializer.Serialize(_report,
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
        GD.Print("FACTORY_PROBE_RESULT " + JsonSerializer.Serialize(_report));
    }
}
