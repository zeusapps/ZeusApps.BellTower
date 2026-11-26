using System.Collections.Concurrent;
using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Tools;

namespace BellTower.FieldAcceptance.Scenarios;

/// <summary>
/// Task 2.3 and ledger row F1. Runs the installed provider hook command (the ingress image the candidate's setup wrote into the fake
/// provider settings) through each provider's own shell (Git Bash for Claude Code, the discovered PowerShell for Codex) under load, as
/// children of fake provider processes, while the sandbox tray runs and consumes the captures. Per provider: one serial first sample and
/// 120 samples in concurrent batches of eight, with two controls in rotating legs of the same batches (the sampling plan and statistics
/// are the ones <c>tools/BellTower.Ingress.Bench</c> uses, compiled from one shared source): the contract control <c>echo 1</c> and an
/// informational neutral leg (the shell starting a native no-op). A mixed run follows in which both providers' hooks and both controls
/// run in the same batches, each through its own provider's shell.
/// The gate is the ingress contract (standalone-setup spec): mean full hook minus mean <c>echo 1</c> below 300 ms; negative estimates stay
/// visible; raw median, p95, p99 and max are retained and reported but the raw median is informational. Every hook records in-agent milliseconds, round-trip milliseconds and the moment its capture file became
/// visible in the inbox; the raw samples go to <c>result.json</c>. The numbers are installed-harness measurements on one machine.
/// </summary>
public sealed class PackagedHookBenchmarkScenario : IScenario
{
    public string Id => "packaged-hook-benchmark";
    public string Title => "Packaged provider-shell hook command plus ingress under load: latency, durable completion and exact event counts";

    private const int Parallelism = 8;
    private const int ParallelSamples = 120;
    private const double AddedLimitMilliseconds = 300;
    private const double DurableLimitMilliseconds = 3000;

    private const string NativeLeg = "native";
    private const string EchoLeg = "echo";
    private const string NeutralLeg = "neutral";
    private static readonly string[] Legs = [EchoLeg, NeutralLeg, NativeLeg];

    private sealed record HookRecord(string Phase, string Provider, string Leg, int Sample, int Worker, DateTime StartedUtc, DateTime EndedUtc,
        double RoundTripMilliseconds, double InAgentMilliseconds, bool Accepted, int ExitCode)
    {
        public string? CaptureId { get; set; }
        public DateTime? CaptureObservedUtc { get; set; }
        public double? DurableMilliseconds { get; set; }
    }

    private sealed class Worker(AgentSession session, string payload)
    {
        public AgentSession Session { get; } = session;
        public string Payload { get; } = payload;
    }

    public void Run(RunContext context)
    {
        // The raised minimums serve this scenario's parallel hooks only; later scenarios of a combined run get the original pool (ANALYSIS-171).
        ThreadPool.GetMinThreads(out var workerThreads, out var completionThreads);
        ThreadPool.SetMinThreads(Math.Max(Environment.ProcessorCount, 32), 32);
        try
        {
            Measure(context);
        }
        finally
        {
            ThreadPool.SetMinThreads(workerThreads, completionThreads);
        }
    }

    private void Measure(RunContext context)
    {
        var instance = CandidateInstall.Prepare(context, Id, "bench");
        var work = Path.Combine(instance.Layout.Root, "work", "bench");
        Directory.CreateDirectory(work);
        var paths = new DataPaths(instance.Layout.Data);
        var inbox = Path.Combine(instance.Layout.Data, CaptureProtocol.InboxName);
        var trayAtStart = TowerReader.TrayRunning(instance);
        var loadBefore = MachineLoad.Sample(TimeSpan.FromSeconds(3));

        var records = new ConcurrentBag<HookRecord>();
        using var observer = new CaptureObserver(inbox);
        var models = ProviderModel.All;
        var workers = new Dictionary<string, Worker[]>();
        foreach (var model in models)
        {
            workers[model.Name] = Enumerable.Range(0, Parallelism).Select(index => StartWorker(instance, model, "bench-" + model.Name + "-" + index, work)).ToArray();
        }
        foreach (var model in models)
        {
            RunProvider(instance, model, workers[model.Name], records, models.ToList().IndexOf(model));
        }
        var loadBetween = MachineLoad.Sample(TimeSpan.FromSeconds(3));
        // Mixed run: both providers' hooks in the same concurrent batches (four workers of each).
        var mixedWorkers = models.SelectMany(model => Enumerable.Range(0, Parallelism / 2).Select(index => (Model: model, Worker: StartWorker(instance, model, "bench-mixed-" + model.Name + "-" + index, work)))).ToArray();
        RunMixed(instance, mixedWorkers, records);
        var drained = TowerReader.WaitFor(() => !Directory.EnumerateFiles(inbox, "*.json").Any(), 30000);
        var delivered = WaitForDelivery(paths, observer, records);
        var loadAfter = MachineLoad.Sample(TimeSpan.FromSeconds(3));
        var trayAtEnd = TowerReader.TrayRunning(instance);

        var all = records.ToArray();
        // A capture the tray consumed before its writer could be read is identified from the tray's own journal (QA-242): matching no longer races the tray.
        observer.ResolveFrom(new StateStore(paths).Read().Sessions.Values.SelectMany(session => session.CausalEvents.Select(observation => (observation.Id, Writer: (observation.Provider, observation.SessionId))))
            .GroupBy(pair => pair.Id).ToDictionary(group => group.Key, group => group.First().Writer, StringComparer.Ordinal));
        Attribute(all, observer);
        var rejections = Directory.Exists(Path.Combine(instance.Layout.Data, "capture-health")) ? Directory.EnumerateFiles(Path.Combine(instance.Layout.Data, "capture-health")).Select(Path.GetFileName).ToList() : [];
        var attempt = HookTrayStart.LastAttempt(instance.Layout.Data);
        var load = MachineLoad.Classify(loadBefore, loadBetween, loadAfter);
        var loadText = "before " + loadBefore.Describe() + "; between provider runs and the mixed run " + loadBetween.Describe() + "; after " + loadAfter.Describe() + "; classified " + load;

        context.Check(Id, "tray-ran-throughout", ProviderNames.Harness, trayAtStart && trayAtEnd && attempt is null,
            "The sandbox tray was running before and after the load (" + trayAtStart + " and " + trayAtEnd + ") and no hook recorded a tray start attempt (" + (attempt is null ? "none recorded" : "attempt " + attempt.Value.Outcome) + "), so no hook started a tray; the tray consumed the captures while the hooks ran.");
        foreach (var model in models)
        {
            Evaluate(context, instance, model.Name, model.Name, all.Where(record => record.Phase == "provider" && record.Provider == model.Name).ToList(), observer, delivered, drained, rejections, loadText, ParallelSamples + 1);
        }
        Evaluate(context, instance, ProviderNames.Mixed, ProviderNames.Mixed, all.Where(record => record.Phase == "mixed").ToList(), observer, delivered, drained, rejections, loadText, ParallelSamples);

        context.Report.Measurements[Id] = new
        {
            label = "installed-harness measurements on this machine; the numbers are machine-specific",
            parallelism = Parallelism,
            samplesPerProviderLeg = ParallelSamples + 1,
            mixedHooks = ParallelSamples,
            machineLoad = new { before = loadBefore, between = loadBetween, after = loadAfter, classification = load },
            captureObserverWatcherErrors = observer.WatcherErrors,
            rawSamples = all.OrderBy(record => record.Phase).ThenBy(record => record.Provider).ThenBy(record => record.Leg).ThenBy(record => record.Sample).Select(record => new
            {
                record.Phase,
                record.Provider,
                record.Leg,
                record.Sample,
                record.Worker,
                startedAt = record.StartedUtc,
                roundTripMilliseconds = record.RoundTripMilliseconds,
                inAgentMilliseconds = record.InAgentMilliseconds,
                durableMilliseconds = record.DurableMilliseconds,
                captureId = record.CaptureId,
                record.Accepted,
                record.ExitCode
            }).ToArray()
        };
        context.Report.Notes.Add("packaged-hook-benchmark, machine: these are installed-harness measurements on this machine, not release performance claims. Machine load (no process names): " + loadText + ". " +
            "Hook command: the owned UserPromptSubmit command in the candidate's own setup output, run as a child of a fake claude.exe through Git Bash (Claude Code) and of a fake codex.exe through " +
            ShellDescription(ProviderModel.Codex, instance) + " (Codex), with the native capture's field set as input. The contract control leg runs `echo 1` through the same shell, session and input path; the informational neutral leg runs a native no-op (`" +
            ProviderModel.Claude.NeutralCommand + "` for Git Bash, `" + ProviderModel.Codex.NeutralCommand + "` for PowerShell) the same way; the three legs rotate inside the same batches, in the provider runs and in the mixed run.");
        context.Report.Notes.Add("packaged-hook-benchmark, Codex shell arguments: the harness runs the Codex PowerShell hook as `-NoProfile -NonInteractive -Command <command>`. This argument vector is MODELED and UNVERIFIED for Codex 0.164.0: the archived evidence pins Codex's shell selection, not its argument vector. " +
            "If Codex does not pass -NoProfile, field hooks also pay for loading the user's profile and the contract estimate here understates the field cost of the shell, not of the ingress. Closing evidence: the Codex source line that builds the PowerShell hook command line for the pinned release, or a probe that records the arguments a real Codex hook runner passes.");
    }

    private static string ShellDescription(ProviderModel model, Instance instance) => model.RuntimeShell(instance).Name;

    private static Worker StartWorker(Instance instance, ProviderModel model, string sessionId, string directory)
    {
        var session = instance.StartSession(model.Name, sessionId, directory, "att-" + sessionId);
        return new Worker(session, model.Payload("UserPromptSubmit", sessionId, directory, instance.Layout));
    }

    private static HookRecord Fire(Instance instance, ProviderModel model, Worker worker, int workerIndex, string phase, string leg, int sample, string command)
    {
        var shell = model.RuntimeShell(instance);
        var started = DateTime.UtcNow;
        var result = worker.Session.Hook(command, worker.Payload, shell);
        var ended = DateTime.UtcNow;
        var accepted = leg == NativeLeg ? model.Accepted(result) : result.Succeeded && result.Output.Trim() == "1" && result.Error.Length == 0;
        return new HookRecord(phase, model.Name, leg, sample, workerIndex, started, ended, result.Elapsed.TotalMilliseconds, result.AgentMilliseconds, accepted, result.ExitCode);
    }

    private static string CommandOf(Instance instance, ProviderModel model, string leg) => leg switch
    {
        EchoLeg => "echo 1",
        NeutralLeg => model.NeutralCommand,
        _ => CandidateInstall.Command(instance, model, "UserPromptSubmit")
    };

    private static void RunProvider(Instance instance, ProviderModel model, Worker[] workers, ConcurrentBag<HookRecord> records, int legOffset)
    {
        // One serial first sample per leg, then matched concurrent batches whose three legs rotate (the Ingress.Bench plan with a third leg).
        foreach (var leg in Legs)
        {
            records.Add(Fire(instance, model, workers[0], 0, "provider", leg, 0, CommandOf(instance, model, leg)));
        }
        var batches = HookTiming.Batches(ParallelSamples, Parallelism);
        for (var batch = 0; batch < batches.Count; batch++)
        {
            foreach (var leg in HookTiming.Rotate(Legs, batch, legOffset))
            {
                var command = CommandOf(instance, model, leg);
                Parallel.ForEach(batches[batch], new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, sample =>
                    records.Add(Fire(instance, model, workers[sample % Parallelism], sample % Parallelism, "provider", leg, sample, command)));
            }
        }
    }

    private static void RunMixed(Instance instance, (ProviderModel Model, Worker Worker)[] workers, ConcurrentBag<HookRecord> records)
    {
        var batches = HookTiming.Batches(ParallelSamples, Parallelism);
        for (var batch = 0; batch < batches.Count; batch++)
        {
            foreach (var leg in HookTiming.Rotate(Legs, batch, ProviderModel.All.Count))
            {
                Parallel.ForEach(batches[batch], new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, sample =>
                {
                    var (model, worker) = workers[sample % workers.Length];
                    records.Add(Fire(instance, model, worker, sample % workers.Length, "mixed", leg, sample, CommandOf(instance, model, leg)));
                });
            }
        }
    }

    // Gives the tray time to commit the last captures, then returns the envelope ids it applied (read from each session's causal journal).
    private static HashSet<string> WaitForDelivery(DataPaths paths, CaptureObserver observer, ConcurrentBag<HookRecord> records)
    {
        var expected = records.Count(record => record.Leg == NativeLeg && record.Accepted);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        while (DateTime.UtcNow < deadline)
        {
            ids = AppliedIds(paths);
            if (observer.Captures.Count >= expected && observer.Captures.All(capture => ids.Contains(capture.Id)))
            {
                break;
            }
            Thread.Sleep(500);
        }
        return ids;
    }

    private static HashSet<string> AppliedIds(DataPaths paths) =>
        new StateStore(paths).Read().Sessions.Values.SelectMany(session => session.CausalEvents.Select(observation => observation.Id)).ToHashSet(StringComparer.Ordinal);

    // Matches each native hook to the capture its own session wrote while it ran: hooks of one worker never overlap, so the n-th hook of a
    // worker pairs with the n-th capture of that session, and the capture's ingress time must fall inside the hook's own window.
    private static void Attribute(HookRecord[] records, CaptureObserver observer)
    {
        var captures = observer.Captures;
        foreach (var group in records.Where(record => record.Leg == NativeLeg && record.Accepted).GroupBy(record => (record.Phase, record.Provider, record.Worker)))
        {
            var hooks = group.OrderBy(record => record.StartedUtc).ToList();
            var sessionId = SessionOf(group.Key.Phase, group.Key.Provider, group.Key.Worker);
            var mine = captures.Where(capture => capture.Provider == group.Key.Provider && capture.SessionId == sessionId).OrderBy(capture => capture.IngressTicks).ToList();
            for (var index = 0; index < Math.Min(hooks.Count, mine.Count); index++)
            {
                var ingress = new DateTime(mine[index].IngressTicks, DateTimeKind.Utc);
                if (ingress >= hooks[index].StartedUtc.AddMilliseconds(-50) && ingress <= hooks[index].EndedUtc.AddMilliseconds(50))
                {
                    hooks[index].CaptureId = mine[index].Id;
                    hooks[index].CaptureObservedUtc = mine[index].ObservedUtc;
                    hooks[index].DurableMilliseconds = (mine[index].ObservedUtc - hooks[index].StartedUtc).TotalMilliseconds;
                }
            }
        }
    }

    private static string SessionOf(string phase, string provider, int worker) => phase == "mixed"
        ? "bench-mixed-" + provider + "-" + (provider == ProviderNames.Claude ? worker : worker - Parallelism / 2)
        : "bench-" + provider + "-" + worker;

    private static string Stats(string name, TimingSummary summary) =>
        name + ": n=" + summary.Samples + ", median " + summary.MedianMilliseconds.ToString("F1") + " ms, p95 " + summary.P95Milliseconds.ToString("F1") + " ms, p99 " + summary.P99Milliseconds.ToString("F1") +
        " ms, max " + summary.MaximumMilliseconds.ToString("F1") + " ms, mean " + summary.MeanMilliseconds.ToString("F1") + " ms, first (serial) " + summary.FirstSampleMilliseconds.ToString("F1") + " ms";

    /// <summary>The ingress contract: every planned hook run exists and was accepted, the controls are valid, and both added-mean estimates are below the limit.</summary>
    public static bool ContractGate(bool controlsValid, int plannedNative, int nativeCount, int acceptedNative, double estimate, double estimateInAgent) =>
        controlsValid && nativeCount == plannedNative && acceptedNative == nativeCount && estimate < AddedLimitMilliseconds && estimateInAgent < AddedLimitMilliseconds;

    private void Evaluate(RunContext context, Instance instance, string scope, string provider, List<HookRecord> records, CaptureObserver observer, HashSet<string> delivered, bool drained,
        List<string?> rejections, string loadText, int plannedNative)
    {
        var native = records.Where(record => record.Leg == NativeLeg).ToList();
        var echo = records.Where(record => record.Leg == EchoLeg).ToList();
        var neutral = records.Where(record => record.Leg == NeutralLeg).ToList();
        var mixed = scope == ProviderNames.Mixed;
        TimingSummary Summarize(IEnumerable<HookRecord> set, Func<HookRecord, double?> value, string mode, Func<HookRecord, bool>? success = null) =>
            HookTiming.Summarize(set.Select(record => new TimingSample(record.Sample, value(record) ?? double.NaN, (success ?? (r => r.Accepted))(record))), mode, Parallelism, ParallelSamples);
        var roundTrip = Summarize(native, record => record.RoundTripMilliseconds, "round trip");
        var inAgent = Summarize(native, record => record.InAgentMilliseconds, "in-agent");
        var matched = native.Where(record => record.DurableMilliseconds is not null).ToList();
        var durable = matched.Count > 0 ? Summarize(matched, record => record.DurableMilliseconds, "durable completion") : null;
        var echoRoundTrip = echo.Count > 0 ? Summarize(echo, record => record.RoundTripMilliseconds, "echo 1 control") : null;
        var echoInAgent = echo.Count > 0 ? Summarize(echo, record => record.InAgentMilliseconds, "echo 1 control in-agent") : null;
        var neutralRoundTrip = neutral.Count > 0 ? Summarize(neutral, record => record.RoundTripMilliseconds, "neutral control") : null;

        var accepted = native.Count(record => record.Accepted);
        var controlsValid = echo.Count == native.Count && echo.All(record => record.Accepted);
        context.Check(Id, scope + "/hooks-accepted", provider, native.Count == plannedNative && accepted == native.Count && controlsValid,
            accepted + " of " + native.Count + " provider-shell hook runs exited 0 with {} on standard output and nothing on standard error (planned " + plannedNative + "); the " + echo.Count + " echo control runs all printed 1: " + controlsValid +
            " (a failed baseline cannot produce a passing comparison).");

        // Gate: the ingress contract. Mean full hook minus mean echo 1 through the same shell, session, input path, sample count and concurrency.
        // A negative estimate stays visible and passes; it is never clamped or relabeled.
        var estimate = echoRoundTrip is null ? double.NaN : roundTrip.MeanMilliseconds - echoRoundTrip.MeanMilliseconds;
        var estimateInAgent = echoInAgent is null ? double.NaN : inAgent.MeanMilliseconds - echoInAgent.MeanMilliseconds;
        context.Check(Id, scope + "/added-mean-below-300ms", provider, ContractGate(controlsValid, plannedNative, native.Count, accepted, estimate, estimateInAgent),
            "GATE (ingress contract, standalone-setup spec): mean full hook minus mean `echo 1` through the same shell, session, input path, sample count and concurrency, below " + AddedLimitMilliseconds + " ms. " +
            "It fails when any planned hook run is missing or not accepted (" + accepted + " of " + native.Count + " accepted, " + plannedNative + " planned) or a control run failed, as a failed hook or baseline cannot produce a passing comparison. " +
            "Round trip: hook mean " + roundTrip.MeanMilliseconds.ToString("F1") + " ms, echo 1 mean " + (echoRoundTrip?.MeanMilliseconds ?? double.NaN).ToString("F1") + " ms, estimated added " + estimate.ToString("F1") + " ms" + (estimate < 0 ? " (negative, shown as measured)" : "") + ". " +
            "In-agent: hook mean " + inAgent.MeanMilliseconds.ToString("F1") + " ms, echo 1 mean " + (echoInAgent?.MeanMilliseconds ?? double.NaN).ToString("F1") + " ms, estimated added " + estimateInAgent.ToString("F1") + " ms. " +
            "Installed-harness measurement on this machine; machine: " + loadText + ".");
        context.Check(Id, scope + "/raw-max-below-3s", provider, roundTrip.MaximumMilliseconds < DurableLimitMilliseconds && roundTrip.OverThreeSeconds == 0,
            "GATE (three-second raw-duration guard): no full hook run took " + DurableLimitMilliseconds + " ms or more; round trip max " + roundTrip.MaximumMilliseconds.ToString("F1") + " ms, " + roundTrip.OverThreeSeconds + " run(s) over three seconds.");
        context.Check(Id, scope + "/raw-distribution-informational", provider, double.IsFinite(roundTrip.MedianMilliseconds + roundTrip.P95Milliseconds + roundTrip.P99Milliseconds + roundTrip.MaximumMilliseconds) && echoRoundTrip is not null,
            "INFORMATIONAL, not a gate (raw values, not relabeled as the contract estimate; the earlier raw-median rule of 300 ms is superseded by the contract above): " + Stats("Round trip", roundTrip) + ". " + Stats("In-agent", inAgent) + ". " +
            (echoRoundTrip is null ? "" : Stats("Echo 1 contract control", echoRoundTrip) + ". raw median hook minus raw median echo 1: " + (roundTrip.MedianMilliseconds - echoRoundTrip.MedianMilliseconds).ToString("F1") + " ms.") + " The raw samples of every hook are in result.json under measurements.");
        // Informational neutral leg: the shell starting a native no-op. Under PowerShell the contract control (echo, an alias of Write-Output that auto-loads a module) is slower than the hook's native call,
        // so hook-minus-neutral is the like-for-like estimate of what the ingress adds on top of starting the shell.
        var neutralEstimate = neutralRoundTrip is null ? double.NaN : roundTrip.MeanMilliseconds - neutralRoundTrip.MeanMilliseconds;
        context.Check(Id, scope + "/neutral-control-informational", provider, neutral.Count == native.Count && neutral.All(record => record.Accepted),
            "INFORMATIONAL, not a gate and not the contract baseline: the neutral leg (" + (mixed ? "each provider's own shell starting a native no-op: `" + ProviderModel.Claude.NeutralCommand + "` and `" + ProviderModel.Codex.NeutralCommand + "`" : "`" + ProviderModel.For(provider).NeutralCommand + "`") +
            ") ran " + neutral.Count + " times in the same batches (all printed 1: " + neutral.All(record => record.Accepted) + "). " +
            (neutralRoundTrip is null ? "No neutral sample." : Stats("Neutral control", neutralRoundTrip) + ". Hook mean minus neutral mean: " + neutralEstimate.ToString("F1") + " ms" + (neutralEstimate < 0 ? " (negative, shown as measured)" : "") + "; hook mean minus echo 1 mean (the contract estimate): " + estimate.ToString("F1") + " ms."));
        context.Check(Id, scope + "/durable-within-3s", provider, durable is not null && matched.Count == native.Count && durable.MaximumMilliseconds < DurableLimitMilliseconds,
            "GATE: every capture visible in the inbox within " + DurableLimitMilliseconds + " ms of its hook starting (the capture file's first appearance, observed through the inbox, matched to its own hook by session and ingress time). " +
            (durable is null ? "No capture could be matched." : Stats("Durable completion", durable)) + "; " + matched.Count + " of " + native.Count + " hooks matched to their capture file.");
        context.Check(Id, scope + "/tail-latency-reported", provider, durable is not null && new[] { roundTrip, inAgent, durable }.All(summary => double.IsFinite(summary.MedianMilliseconds + summary.P95Milliseconds + summary.P99Milliseconds + summary.MaximumMilliseconds)),
            "Median, p95, p99 and max recorded for round trip, in-agent and durable completion (see the checks above); the raw samples of every hook are in result.json under measurements.");

        var captures = observer.Captures.Where(capture => mixed
            ? capture.SessionId?.StartsWith("bench-mixed-", StringComparison.Ordinal) == true
            : capture.Provider == provider && capture.SessionId?.StartsWith("bench-" + provider + "-", StringComparison.Ordinal) == true).ToList();
        var unknown = observer.Captures.Count(capture => capture.Provider is null);
        var acceptedIds = native.Where(record => record.CaptureId is not null).Select(record => record.CaptureId!).ToList();
        var deliveredCount = captures.Count(capture => delivered.Contains(capture.Id));
        context.Check(Id, scope + "/event-counts-exact", provider,
            accepted == captures.Count && captures.Count == acceptedIds.Count && deliveredCount == captures.Count && drained && rejections.Count == 0 && unknown == 0 && observer.WatcherErrors == 0,
            accepted + " hooks accepted, " + captures.Count + " capture files became durable for them, " + acceptedIds.Count + " matched one-to-one to their hook, " + deliveredCount + " delivered (their envelope ids are in the running tray's applied session journals); inbox drained: " + drained +
            "; rejected captures recorded by the product: " + rejections.Count + "; captures whose writer is still unknown after the tray's own journal was consulted: " + unknown + "; inbox watcher errors: " + observer.WatcherErrors + ". No accepted envelope was lost when accepted equals durable equals delivered.");
    }
}
