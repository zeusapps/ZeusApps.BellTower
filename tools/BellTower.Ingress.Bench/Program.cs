using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Setup;

try
{
    await Execute(args);
    return 0;
}
catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
{
    Console.Error.WriteLine("Isolated benchmark failed: " + exception.GetType().Name + ". No performance pass is claimed.");
    return 1;
}

static async Task Execute(string[] args)
{
    if (args.Length != 2)
    {
        throw new ArgumentException("Supply an isolated package root and a new trial directory under artifacts.");
    }
    var packageRoot = Path.GetFullPath(args[0]);
    var trialRoot = Path.GetFullPath(args[1]);
    var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var artifacts = Path.Combine(repository, "artifacts") + Path.DirectorySeparatorChar;
    if (!trialRoot.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase) || Directory.Exists(trialRoot))
    {
        throw new IOException("Use a new isolated artifact directory.");
    }
    var paths = new DataPaths(Path.Combine(trialRoot, "Δ O'Brien", "data"));
    paths.Ensure();
    var home = Path.Combine(trialRoot, "home");
    var installation = new Installation(packageRoot);
    var plan = new IntegrationSetup(paths, installation.Launcher, home).Plan("both");
    plan.Apply(paths);
    var provision = IngressProvision.Plan(installation.Launcher, paths)!;
    provision.Apply(paths);
    var report = new List<object>();
    foreach (var provider in new[] { "claude", "codex" })
    {
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(home, "." + provider, provider == "claude" ? "settings.json" : "hooks.json")))!;
        var direct = settings["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]!.GetValue<string>();
        var legacy = new HookCommandBuilder(installation.Launcher, paths).Build("event", provider);
        foreach (var transport in new[] { (Name: "legacy-chain", Command: legacy, Samples: 24), (Name: "native-ingress", Command: direct, Samples: 120) })
        {
            var results = new ConcurrentBag<(double Milliseconds, bool Success)>();
            var cold = await Run(provider, transport.Command, 0, transport.Name == "native-ingress");
            await Parallel.ForEachAsync(Enumerable.Range(1, transport.Samples), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (sample, _) =>
            {
                results.Add(await Run(provider, transport.Command, sample, transport.Name == "native-ingress"));
            });
            results.Add(cold);
            var ordered = results.Select(r => r.Milliseconds).Order().ToArray();
            double Percentile(double quantile) => ordered[Math.Max(0, (int)Math.Ceiling(quantile * ordered.Length) - 1)];
            report.Add(new
            {
                Provider = provider,
                Transport = transport.Name,
                Samples = results.Count,
                Parallelism = 8,
                FirstCaptureMilliseconds = cold.Milliseconds,
                MedianMilliseconds = Percentile(0.5),
                P95Milliseconds = Percentile(0.95),
                P99Milliseconds = Percentile(0.99),
                MaxMilliseconds = ordered[^1],
                Failed = results.Count(r => !r.Success),
                OverThreeSeconds = results.Count(r => r.Milliseconds >= 3000)
            });
            if (results.Any(r => !r.Success))
            {
                throw new IOException("Hook dependency or neutral-output check failed; no successful performance claim was recorded.");
            }
            if (transport.Name == "native-ingress" && (Percentile(0.5) >= 300 || ordered[^1] >= 3000))
            {
                throw new IOException("Native capture latency budget failed.");
            }
        }
        Console.WriteLine(provider + " provider-shell samples complete.");
    }
    var inbox = Path.Combine(paths.Root, CaptureProtocol.InboxName);
    var captures = Directory.GetFiles(inbox, "*.json");
    if (captures.Length != 242 || Directory.GetFiles(paths.Inbox, "*.json").Length != 50)
    {
        throw new IOException("Durable event count mismatch.");
    }
    // Retain one committed receipt to reproduce a consumer crash between commit and deletion.
    var retainedPath = captures[0];
    var retained = File.ReadAllBytes(retainedPath);
    var store = new StateStore(paths);
    while (Directory.GetFiles(inbox, "*.json").Length > 0 || Directory.GetFiles(paths.Inbox, "*.json").Length > 0)
    {
        store.Drain();
    }
    var committed = IOJson(store.Read().Sessions);
    File.WriteAllBytes(retainedPath, retained);
    store.Drain();
    if (IOJson(store.Read().Sessions) != committed)
    {
        throw new IOException("Committed-capture replay changed state.");
    }
    var resultPath = Path.Combine(trialRoot, "results.json");
    File.WriteAllText(resultPath, IOJson(new
    {
        At = DateTimeOffset.UtcNow,
        Mode = "isolated generated provider-shell commands; synthetic hooks; no authenticated provider trial",
        Package = packageRoot,
        HelperBytes = new FileInfo(provision.Target).Length,
        DurableCaptures = captures.Length,
        DurableLegacyEvents = 50,
        SessionsAfterDrain = store.Read().Sessions.Count,
        CrashReplay = "passed",
        Reports = report
    }));
    Console.WriteLine(resultPath);

    string IOJson<T>(T value) => BellTower.IO.Json.Encode(value);

    async Task<(double Milliseconds, bool Success)> Run(string provider, string command, int sample, bool requireSilent)
    {
        var start = new ProcessStartInfo(provider == "claude" ? "C:/Program Files/Git/bin/bash.exe" : "cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            WorkingDirectory = trialRoot
        };
        if (provider == "claude")
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(command);
        }
        else
        {
            // Match Codex's Windows raw /C argument construction.
            start.Arguments = "/C \"" + command + "\"";
        }
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("BELLTOWER_", StringComparison.Ordinal)).ToArray())
        {
            start.Environment.Remove(key);
        }
        start.Environment["BELLTOWER_ATTACHMENT"] = "fixture-" + provider + "-" + sample % 4;
        start.Environment["BELLTOWER_DEDICATED"] = "1";
        var clock = Stopwatch.StartNew();
        using var process = Process.Start(start) ?? throw new IOException("Fixture hook shell could not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new
        {
            session_id = provider + "-fixture-" + sample % 4,
            hook_event_name = "Stop",
            cwd = paths.Root,
            last_assistant_message = "private-fixture-message"
        }));
        process.StandardInput.Close();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true); // Only this disposable fixture hook tree.
            throw new IOException("Fixture hook timed out; no latency pass is claimed.");
        }
        clock.Stop();
        var stdout = await output;
        var stderr = await error;
        // Windows PowerShell emits first-use progress CLIXML on stderr in the legacy control.
        // Native ingress must be silent; both paths must return neutral JSON and persist every event.
        var success = process.ExitCode == 0 && stdout.Trim() == "{}" && (!requireSilent || stderr.Length == 0);
        if (!success)
        {
            File.WriteAllText(Path.Combine(trialRoot, provider + "-fixture-failure-" + sample + ".json"), IOJson(new
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr
            }));
        }
        return (clock.Elapsed.TotalMilliseconds, success);
    }
}
