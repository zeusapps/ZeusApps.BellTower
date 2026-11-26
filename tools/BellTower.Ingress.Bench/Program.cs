using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Setup;
using BellTower.Tools;

try
{
    await Execute(args);
    return 0;
}
catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
{
    var detail = exception.Message
        .Replace(Path.GetFullPath(args.Length > 1 ? args[1] : "<trial>"), "<trial>", StringComparison.OrdinalIgnoreCase)
        .Replace(Path.GetFullPath(args.Length > 0 ? args[0] : "<package>"), "<package>", StringComparison.OrdinalIgnoreCase)
        .Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "<user>", StringComparison.OrdinalIgnoreCase);
    Console.Error.WriteLine("Isolated benchmark failed: " + exception.GetType().Name + ": " + detail + ". No performance pass is claimed.");
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
    var comparisonFailures = new List<string>();
    foreach (var provider in new[] { "claude", "codex" })
    {
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(home, "." + provider, provider == "claude" ? "settings.json" : "hooks.json")))!;
        var direct = settings["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]!.GetValue<string>();
        var legacy = new HookCommandBuilder(installation.Launcher, paths).Build("event", provider);
        const int parallelSamples = 120;
        const int parallelism = 8;
        var baseline = new ConcurrentBag<TimingSample>();
        var native = new ConcurrentBag<TimingSample>();

        // Keep one serial first sample plus 120 concurrent samples for both legs.
        // Run matched eight-sample batches, alternating which leg goes first, so
        // long-term machine drift is less likely to favor one measurement.
        baseline.Add(await RunNoop(provider, 0));
        native.Add(await Run(provider, direct, 0, true));
        var batches = HookTiming.Batches(parallelSamples, parallelism);
        for (var batch = 0; batch < batches.Count; batch++)
        {
            var sampleNumbers = batches[batch];
            async Task<TimingSample[]> RunBatch(bool isNative)
            {
                var results = new ConcurrentBag<TimingSample>();
                await Parallel.ForEachAsync(sampleNumbers, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, async (sample, _) =>
                {
                    results.Add(isNative
                        ? await Run(provider, direct, sample, true)
                        : await RunNoop(provider, sample));
                });
                return results.OrderBy(item => item.Sample).ToArray();
            }

            if (HookTiming.BaselineFirst(batch, provider == "codex" ? 1 : 0))
            {
                foreach (var item in await RunBatch(false))
                {
                    baseline.Add(item);
                }

                foreach (var item in await RunBatch(true))
                {
                    native.Add(item);
                }
            }
            else
            {
                foreach (var item in await RunBatch(true))
                {
                    native.Add(item);
                }

                foreach (var item in await RunBatch(false))
                {
                    baseline.Add(item);
                }
            }
        }

        var legacyResults = new ConcurrentBag<TimingSample>();
        legacyResults.Add(await Run(provider, legacy, 0, false));
        await Parallel.ForEachAsync(Enumerable.Range(1, 24), new ParallelOptions { MaxDegreeOfParallelism = parallelism }, async (sample, _) =>
        {
            legacyResults.Add(await Run(provider, legacy, sample, false));
        });

        var baselineStats = Statistics(baseline, "echo 1", parallelism, parallelSamples);
        var nativeStats = Statistics(native, "native-ingress", parallelism, parallelSamples);
        var legacyStats = Statistics(legacyResults, "legacy-chain", parallelism, 24);
        var estimate = nativeStats.MeanMilliseconds - baselineStats.MeanMilliseconds;
        var comparisonValid = baselineStats.FailedCalls == 0 && nativeStats.FailedCalls == 0;
        var estimatedMeanPass = comparisonValid && estimate < 300;
        var rawMaximumPass = nativeStats.MaximumMilliseconds < 3000;
        report.Add(new
        {
            Provider = provider,
            Shell = provider == "claude" ? "configured Git Bash or standard Git for Windows Bash" : "PowerShell selected by benchmark resolver",
            Mode = "one serial first sample; 120 matched samples in alternating concurrent batches; same shell args, environment and event input",
            Baseline = baselineStats,
            NativeIngress = nativeStats,
            LegacyControl = legacyStats,
            EstimatedAddedMeanMilliseconds = estimate,
            ComparisonValid = comparisonValid,
            EstimatedMeanBelow300Milliseconds = estimatedMeanPass,
            NativeRawMaximumBelow3000Milliseconds = rawMaximumPass
        });
        Console.WriteLine(provider + " echo 1: n=" + baselineStats.Samples + ", mean=" + baselineStats.MeanMilliseconds.ToString("F2") + " ms, median=" + baselineStats.MedianMilliseconds.ToString("F2") + " ms, p95=" + baselineStats.P95Milliseconds.ToString("F2") + " ms, p99=" + baselineStats.P99Milliseconds.ToString("F2") + " ms, max=" + baselineStats.MaximumMilliseconds.ToString("F2") + " ms, failed=" + baselineStats.FailedCalls + ".");
        Console.WriteLine(provider + " native ingress: n=" + nativeStats.Samples + ", mean=" + nativeStats.MeanMilliseconds.ToString("F2") + " ms, median=" + nativeStats.MedianMilliseconds.ToString("F2") + " ms, p95=" + nativeStats.P95Milliseconds.ToString("F2") + " ms, p99=" + nativeStats.P99Milliseconds.ToString("F2") + " ms, max=" + nativeStats.MaximumMilliseconds.ToString("F2") + " ms, estimated added mean=" + estimate.ToString("F2") + " ms, failed=" + nativeStats.FailedCalls + ".");
        Console.WriteLine(provider + " legacy control: n=" + legacyStats.Samples + ", mean=" + legacyStats.MeanMilliseconds.ToString("F2") + " ms, median=" + legacyStats.MedianMilliseconds.ToString("F2") + " ms, p95=" + legacyStats.P95Milliseconds.ToString("F2") + " ms, p99=" + legacyStats.P99Milliseconds.ToString("F2") + " ms, max=" + legacyStats.MaximumMilliseconds.ToString("F2") + " ms, failed=" + legacyStats.FailedCalls + ".");
        File.WriteAllText(Path.Combine(trialRoot, "latency-report.json"), IOJson(report));

        if (!comparisonValid)
        {
            comparisonFailures.Add(provider + " echo-1/hook comparison contains failed calls");
        }

        if (!estimatedMeanPass)
        {
            comparisonFailures.Add(provider + " estimated added mean is not below 300 ms");
        }

        if (!rawMaximumPass)
        {
            comparisonFailures.Add(provider + " raw hook maximum is not below 3000 ms");
        }

        if (legacyStats.FailedCalls != 0)
        {
            comparisonFailures.Add(provider + " legacy control contains failed calls");
        }

        if (nativeStats.OverThreeSeconds != 0)
        {
            comparisonFailures.Add(provider + " native hook duration reached or exceeded 3000 ms");
        }

        Console.WriteLine(provider + " matched benchmark samples complete.");
    }
    var inbox = Path.Combine(paths.Root, CaptureProtocol.InboxName);
    var captures = Directory.GetFiles(inbox, "*.json");
    var legacyCaptureCount = Directory.GetFiles(paths.Inbox, "*.json").Length;
    if (captures.Length != 242 || legacyCaptureCount != 50)
    {
        throw new IOException("Durable event count mismatch: native=" + captures.Length + "/242, legacy=" + legacyCaptureCount + "/50.");
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
        PackageVersion = installation.Selection().Version,
        HelperBytes = new FileInfo(provision.Target).Length,
        DurableCaptures = captures.Length,
        DurableLegacyEvents = legacyCaptureCount,
        SessionsAfterDrain = store.Read().Sessions.Count,
        CrashReplay = "passed",
        Reports = report,
        ComparisonFailures = comparisonFailures
    }));
    Console.WriteLine(resultPath);
    if (comparisonFailures.Count > 0)
    {
        throw new IOException("Matched provider-shell latency or neutral-output gate failed: " + string.Join("; ", comparisonFailures));
    }

    string IOJson<T>(T value) => BellTower.IO.Json.Encode(value);

    TimingSummary Statistics(IEnumerable<TimingSample> samples, string mode, int parallelism, int parallelSamples) =>
        HookTiming.Summarize(samples, mode, parallelism, parallelSamples);

    string ResolveClaudeBash()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CODE_GIT_BASH_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var fullPath = Path.GetFullPath(configured);
            if (!File.Exists(fullPath))
            {
                throw new IOException("Configured Claude Git Bash path is unavailable.");
            }

            return fullPath;
        }
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe");
        if (File.Exists(standard))
        {
            return standard;
        }

        throw new IOException("Claude Git Bash is unavailable for the provider-shell benchmark.");
    }

    string ResolveCodexPowerShell()
    {
        var standardPwsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        if (File.Exists(standardPwsh))
        {
            return standardPwsh;
        }

        foreach (var name in new[] { "pwsh.exe", "powershell.exe" })
        {
            foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(entry, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        throw new IOException("Codex PowerShell is unavailable for the provider-shell benchmark.");
    }

    ProcessStartInfo ShellStart(string provider)
    {
        var start = new ProcessStartInfo(provider == "claude" ? ResolveClaudeBash() : ResolveCodexPowerShell())
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
        }
        else
        {
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
        }
        return start;
    }

    ProcessStartInfo AsProviderAgent(ProcessStartInfo inner, string provider)
    {
        var directory = Path.Combine(trialRoot, "fixture-agents");
        Directory.CreateDirectory(directory);
        var agent = Path.Combine(directory, provider + ".exe");
        if (!File.Exists(agent))
        {
            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), agent);
        }
        static string Quote(string value) => value.Length > 0 && value.IndexOfAny([' ', '	', '"']) < 0 ? value : "\"" + value.Replace("\"", "\\\"") + "\"";
        return new ProcessStartInfo(agent)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = inner.StandardInputEncoding,
            WorkingDirectory = inner.WorkingDirectory,
            Arguments = "/d /s /c \"" + string.Join(' ', new[] { inner.FileName }.Concat(inner.ArgumentList).Select(Quote)) + "\""
        };
    }

    async Task<TimingSample> RunNoop(string provider, int sample)
    {
        var start = ShellStart(provider);
        start.ArgumentList.Add("echo 1");
        return await RunStarted(start, provider, sample, "1", true);
    }

    async Task<TimingSample> Run(string provider, string command, int sample, bool requireSilent)
    {
        var start = ShellStart(provider);
        start.ArgumentList.Add(command);
        return await RunStarted(start, provider, sample, "{}", requireSilent);
    }

    async Task<TimingSample> RunStarted(ProcessStartInfo start, string provider, int sample, string expectedOutput, bool requireSilent)
    {
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("BELLTOWER_", StringComparison.Ordinal)).ToArray())
        {
            start.Environment.Remove(key);
        }
        // The hook runs under a process image named like the provider, so adoption uses real process evidence (ancestry
        // and the agent's command-line read) instead of a Bell Tower flag.
        start = AsProviderAgent(start, provider);
        start.Environment["BELLTOWER_ATTACHMENT"] = "fixture-" + provider + "-" + sample % 4;
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
        }) + "\n");
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
        var success = process.ExitCode == 0 && stdout.Trim() == expectedOutput && (!requireSilent || stderr.Length == 0);
        if (!success)
        {
            Console.Error.WriteLine(provider + " provider-shell fixture failed: exit=" + process.ExitCode + ", stdoutLength=" + stdout.Length + ", stderrLength=" + stderr.Length + ".");
            File.WriteAllText(Path.Combine(trialRoot, provider + "-fixture-failure-" + sample + ".json"), IOJson(new
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr
            }));
        }
        return new TimingSample(sample, clock.Elapsed.TotalMilliseconds, success);
    }
}
