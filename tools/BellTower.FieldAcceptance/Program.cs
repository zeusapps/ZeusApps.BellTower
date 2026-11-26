using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;
using BellTower.FieldAcceptance.Isolation;
using BellTower.FieldAcceptance.Scenarios;

namespace BellTower.FieldAcceptance;

internal static class Program
{
    private const string Usage =
        "Usage: BellTower.FieldAcceptance --baseline <0.1.9 zip> --candidate <fixture-version candidate zip> --run-dir <dir> [--scenario <id>|all]\n" +
        "       [--baseline-provenance <json>] [--candidate-label <text>] [--candidate-build-command <text>] [--command-text <text>] [--evidence-out <md>] [--keep-sandbox] [--list]";

    private static int Main(string[] args)
    {
        // Evidence text and numbers read the same on every machine.
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        // The harness executable doubles as the fake provider process: copies named claude.exe and codex.exe never reach this parser.
        if (args.Length > 1 && args[0] == FakeAgent.ManagerArgument)
        {
            return FakeAgent.RunManager(args[1..]);
        }
        if (args.Length > 1 && args[0] == FakeAgent.SpawnDetachedArgument)
        {
            return FakeAgent.SpawnDetached(args[1..]);
        }
        if (FakeAgent.IsFakeImage(Environment.ProcessPath))
        {
            return FakeAgent.Run(args);
        }
        try
        {
            var options = Parse(args);
            if (options.ContainsKey("list"))
            {
                foreach (var scenario in ScenarioCatalog.All)
                {
                    Console.WriteLine(scenario.Id + "\t" + scenario.Title);
                }
                return 0;
            }
            return Execute(options, args);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or IsolationException or InvalidDataException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Unexpected argument '" + args[index] + "'.\n" + Usage);
            }
            var name = args[index][2..];
            if (name is "keep-sandbox" or "list")
            {
                options[name] = "true";
            }
            else if (index + 1 < args.Length)
            {
                options[name] = args[++index];
            }
            else
            {
                throw new ArgumentException("Missing value for --" + name + ".\n" + Usage);
            }
        }
        return options;
    }

    private static int Execute(Dictionary<string, string> options, string[] args)
    {
        string Required(string name) => options.TryGetValue(name, out var value) ? value : throw new ArgumentException("--" + name + " is required.\n" + Usage);
        var repository = FindRepositoryRoot();
        var runDirectory = Path.GetFullPath(Required("run-dir"));
        var baseline = Path.GetFullPath(Required("baseline"));
        var candidate = Path.GetFullPath(Required("candidate"));
        // One run at a time: a second run's preflight sweep would end this run's trays and its production comparison would count this run's task.
        using var runLock = RunLock.TryAcquire() ?? throw new IOException("Another field acceptance run is active on this machine (named mutex " + RunLock.DefaultName + "). Wait for it to finish; the runs cannot share the preflight sweep or the production comparison.");
        var production = ProductionLocations.Current();
        // Before anything is created: the run directory itself must be inside the harness artifacts and clear of every production location.
        ProductionGuard.Require(ProductionGuard.CheckLayout(new InstanceLayout("run", runDirectory), production, Path.Combine(repository, "artifacts", "field-acceptance")));
        var report = new RunReport { Command = options.GetValueOrDefault("command-text") ?? "BellTower.FieldAcceptance" };
        Directory.CreateDirectory(runDirectory);
        report.Baseline = VerifyBaseline(baseline, options.GetValueOrDefault("baseline-provenance"));
        report.Candidate = DescribeCandidate(candidate, options.GetValueOrDefault("candidate-label") ?? "fixture-version candidate", options.GetValueOrDefault("candidate-build-command") ?? "");
        var sanitizer = EvidenceSanitizer.ForCurrentUser(repository);
        if (!report.Baseline.Verified)
        {
            Finish(report, runDirectory, options, sanitizer, null);
            return 1;
        }
        var tools = SystemTools.Resolve();
        var selfTest = GuardSelfTest.Run(production, Path.Combine(repository, "artifacts", "field-acceptance"), tools.All);
        foreach (var probe in selfTest)
        {
            report.Add(new CheckResult("guard-selftest", probe.Id, ProviderNames.Harness, EvidenceLabel.Fixture, probe.Passed, probe.Detail));
        }
        if (selfTest.Any(probe => !probe.Passed))
        {
            report.Isolation = new([], [], [], [], [], false, "The guard self-test failed; no sandbox was created.");
            Finish(report, runDirectory, options, sanitizer, null);
            return 1;
        }
        var selected = options.GetValueOrDefault("scenario") ?? "upgrade-without-setup";
        var scenarios = selected == "all" ? ScenarioCatalog.All : ScenarioCatalog.All.Where(scenario => scenario.Id == selected).ToList();
        if (scenarios.Count == 0)
        {
            throw new ArgumentException("Unknown scenario '" + selected + "'.");
        }
        var harnessBase = Path.Combine(repository, "artifacts", "field-acceptance");
        // Preflight: remove only what an earlier interrupted run of this harness left behind (its tasks and processes), before the "before" snapshot,
        // so a leftover can neither pass silently as part of both snapshots nor pile up.
        var preflight = LeftoverSweep.Run(harnessBase, arguments => RunSchtasks(tools, arguments), Environment.ProcessId).Select(item => sanitizer.Sanitize(item)).ToList();
        foreach (var item in preflight)
        {
            Console.WriteLine("PREFLIGHT removed " + item);
        }
        var abort = RunAbort.Current;
        var context = new RunContext(new RunOptions(repository, runDirectory, baseline, candidate, options.ContainsKey("keep-sandbox")), report, production, tools, abort);
        var cleanup = new InstanceCleanup(context, report);
        var finished = new ManualResetEventSlim(false);
        var interruptedOptions = new Dictionary<string, string>(options);
        interruptedOptions.Remove("evidence-out"); // an interrupted run never writes the committed evidence file
        // Ctrl+C only signals: it ends the in-flight sandbox children so the main thread is not blocked on them and sets the stop request. The one
        // main thread then unwinds through the finally block below: one cleanup, the production comparison and result.json. A second Ctrl+C
        // takes the default action (the process ends; the next run's preflight sweep removes what it left).
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            if (abort.Requested)
            {
                return;
            }
            eventArgs.Cancel = true;
            abort.Request("The run was interrupted (Ctrl+C or break).");
            EndSandboxProcesses(context.SandboxBase);
        };
        // A closed console (CTRL_CLOSE_EVENT, which .NET 10 no longer handles by default) arrives as SIGHUP. Windows ends the process a few
        // seconds after the handler starts, so the handler does only bounded work: it signals like Ctrl+C, runs the one shared cleanup (under the
        // lock the main thread's cleanup takes too, so the two never overlap) and gives the main thread a moment to write result.json.
        // If the process is ended first, the next run's preflight sweep removes the leftovers.
        using var closeRegistration = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ =>
        {
            abort.Request("The console was closed.");
            EndSandboxProcesses(context.SandboxBase);
            cleanup.Run();
            finished.Wait(TimeSpan.FromSeconds(4));
        });
        // Last resort when the process exits some other way before the normal path ran: the same idempotent cleanup, no result file.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => cleanup.Run();
        var before = TakeSnapshot(production, tools);
        var layoutViolations = new List<string>();
        try
        {
            foreach (var scenario in scenarios)
            {
                Console.WriteLine("== " + scenario.Id + ": " + scenario.Title);
                try
                {
                    scenario.Run(context);
                }
                catch (RunInterruptedException)
                {
                    break;
                }
                catch (IsolationException exception)
                {
                    layoutViolations.AddRange(exception.Violations);
                    context.Check(scenario.Id, "isolation-guard", ProviderNames.Harness, false, "The guard refused: " + exception.Message);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    context.Check(scenario.Id, "scenario-completed", ProviderNames.Harness, false, "Scenario stopped: " + exception.GetType().Name + ": " + exception.Message);
                }
            }
        }
        finally
        {
            var ownTasks = context.Instances.Select(instance => instance.TaskPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            cleanup.Run();
            try
            {
                var after = TakeSnapshot(production, tools);
                var changes = ProductionSnapshot.Diff(before, after, ownTasks);
                var environmentViolations = context.Instances.SelectMany(instance => instance.EnvironmentViolations).ToList();
                var gating = changes.Where(change => change.Gating).ToList();
                report.Isolation = new IsolationReport(layoutViolations, environmentViolations, context.Instances.SelectMany(instance => instance.RemovedPathEntries).Distinct().Select(entry => sanitizer.Sanitize(entry)).ToList(),
                    production.Roots.Select(root => sanitizer.Sanitize(root)).ToList(), changes.Select(change => change with { Subject = sanitizer.Sanitize(change.Subject) }).ToList(),
                    layoutViolations.Count == 0 && environmentViolations.Count == 0 && gating.Count == 0,
                    "Before and after the run the harness compared the setup-owned provider, VS Code and Bell Tower files, the whole installation tree, the Bell Tower scheduled tasks and the running production Bell Tower processes (" +
                    before.Gated.Count + " files fingerprinted; a production process that disappeared or appeared during the run is a change); directory totals are informational because the user's live tray and agents write there.", preflight);
                context.Check("isolation", "production-unchanged", ProviderNames.Harness, report.Isolation.Passed,
                    report.Isolation.Passed ? "No guarded production file, scheduled task or process changed (" + before.Gated.Count + " files fingerprinted)." : "Production changed or the guard refused: see the isolation section.");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                report.Isolation ??= new([], [], [], [], [], false, "The production comparison failed: " + exception.GetType().Name + ": " + exception.Message, preflight);
                context.Check("isolation", "production-unchanged", ProviderNames.Harness, false, "The production comparison could not run: " + exception.GetType().Name + ": " + exception.Message);
            }
        }
        try
        {
            if (abort.Requested)
            {
                report.Add(new CheckResult("run", "interrupted", ProviderNames.Harness, EvidenceLabel.InstalledHarness, false, abort.Reason + " Cleanup ran and the production comparison was made; result.json holds what was collected, and no committed evidence was written."));
                report.Isolation ??= new([], [], [], [], [], false, "The run was interrupted before the production comparison; no result is claimed.", preflight);
                Finish(report, runDirectory, interruptedOptions, sanitizer, context);
                return 130;
            }
            Finish(report, runDirectory, options, sanitizer, context);
            return report.ExitCode;
        }
        finally
        {
            finished.Set();
        }
    }

    /// <summary>Ends every process whose image is in this run's sandbox directory (trays, fake sessions, hook shells), never the harness itself.</summary>
    private static void EndSandboxProcesses(string sandboxBase)
    {
        try
        {
            LeftoverSweep.KillProcessesUnder(sandboxBase, Environment.ProcessId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine("Could not end every sandbox process: " + exception.Message);
        }
    }

    /// <summary>
    /// Disposes every instance, each in its own try/catch so one failing cleanup cannot skip the others or the result file. Idempotent and
    /// serialized: the main thread, the console-close handler and the exit handler may all call it, and the later callers wait for the
    /// first and then find nothing left to do (an instance created in between is still cleaned by whoever runs last).
    /// </summary>
    private sealed class InstanceCleanup(RunContext context, RunReport report)
    {
        private readonly object gate = new();
        private readonly HashSet<string> reported = [];

        public void Run()
        {
            lock (gate)
            {
                foreach (var instance in context.Instances.ToArray())
                {
                    try
                    {
                        instance.Dispose();
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        Report("instance-" + instance.Layout.Name, "Cleanup was incomplete: " + exception.Message);
                    }
                }
                if (!context.Options.KeepSandbox)
                {
                    foreach (var instance in context.Instances.ToArray())
                    {
                        try
                        {
                            instance.DeleteFiles();
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                        {
                            Report("files-" + instance.Layout.Name, "The sandbox files could not be deleted: " + exception.Message);
                        }
                    }
                }
            }
        }

        private void Report(string id, string detail)
        {
            if (reported.Add(id))
            {
                report.Add(new CheckResult("cleanup", id, ProviderNames.Harness, EvidenceLabel.InstalledHarness, false, detail));
            }
        }
    }

    private static (int ExitCode, string Output) RunSchtasks(SystemTools tools, string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo(tools.Schtasks) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        process.WaitForExit(30000);
        return (process.ExitCode, output.GetAwaiter().GetResult());
    }

    private static ProductionSnapshot TakeSnapshot(ProductionLocations production, SystemTools tools) => ProductionSnapshot.Take(production,
        () => ProductionSnapshot.BellTowerTasks(arguments => RunSchtasks(tools, arguments)),
        () => ProductionSnapshot.ProductionProcesses(production).ToList());

    private static void Finish(RunReport report, string runDirectory, Dictionary<string, string> options, EvidenceSanitizer sanitizer, RunContext? context)
    {
        report.FinishedAt = DateTimeOffset.UtcNow;
        if (context is not null && context.Instances.Count > 0)
        {
            report.Notes.Add("Commands run through the installed launcher (sandbox-relative): " + string.Join("; ", context.Instances.SelectMany(instance => instance.CommandLog).Distinct().Take(40).Select(command => "`" + command + "`")));
        }
        File.WriteAllText(Path.Combine(runDirectory, "result.json"), report.ToJson());
        File.WriteAllText(Path.Combine(runDirectory, "summary.md"), report.ToMarkdown());
        if (options.TryGetValue("evidence-out", out var evidence))
        {
            var text = report.ToMarkdown(sanitizer.Sanitize);
            if (sanitizer.ContainsPrivateValue(text))
            {
                throw new InvalidDataException("The sanitized evidence still contains a private value; it was not written.");
            }
            File.WriteAllText(Path.GetFullPath(evidence), text);
        }
        Console.WriteLine("FIELD ACCEPTANCE " + report.Verdict + ": " + report.Checks.Count(check => check.Passed) + "/" + report.Checks.Count + " checks passed, " + report.UnfinishedItems + " unfinished parity items (not passes). Result: " + Path.Combine(runDirectory, "result.json"));
    }

    private static BaselineProvenance VerifyBaseline(string archive, string? provenancePath)
    {
        var name = Path.GetFileName(archive);
        var actual = HashOf(archive);
        string? published = null;
        var hashFile = archive + ".sha256";
        if (File.Exists(hashFile))
        {
            published = File.ReadAllText(hashFile).Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim().ToLowerInvariant();
        }
        string? descriptor = null;
        var descriptorFile = archive[..^4] + ".release.json";
        if (File.Exists(descriptorFile))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(descriptorFile));
            descriptor = document.RootElement.GetProperty("archiveHash").GetString()?.ToLowerInvariant();
        }
        string tag = "v0.1.9", source = "published release asset", note = "";
        string? apiDigest = null;
        if (provenancePath is not null)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(provenancePath));
            var root = document.RootElement;
            tag = root.TryGetProperty("tag", out var tagValue) ? tagValue.GetString() ?? tag : tag;
            source = root.TryGetProperty("source", out var sourceValue) ? sourceValue.GetString() ?? source : source;
            apiDigest = root.TryGetProperty("githubAssetDigest", out var digest) ? digest.GetString()?.Replace("sha256:", "").ToLowerInvariant() : null;
            note = root.TryGetProperty("note", out var noteValue) ? noteValue.GetString() ?? "" : "";
        }
        var matches = new[] { published, descriptor, apiDigest }.Where(value => value is not null).ToList();
        var verified = matches.Count >= 2 && matches.All(value => value == actual);
        return new(tag, source, name, actual, published ?? "", descriptor, verified,
            (verified ? "Independently matched the " + string.Join(", ", new[] { published is null ? null : "published .sha256", descriptor is null ? null : "release descriptor", apiDigest is null ? null : "GitHub asset digest" }.Where(item => item is not null)) + ". " : "SHA-256 verification FAILED or fewer than two published digests were available. ") + note);
    }

    private static CandidateProvenance DescribeCandidate(string archive, string label, string buildCommand)
    {
        var descriptorFile = archive[..^4] + ".release.json";
        using var document = JsonDocument.Parse(File.ReadAllText(descriptorFile));
        var version = document.RootElement.GetProperty("contract").GetProperty("version").GetString()!;
        return new(label, version, Path.GetFileName(archive), HashOf(archive), buildCommand);
    }

    private static string HashOf(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
            {
                return directory.FullName;
            }
        }
        return Directory.GetCurrentDirectory();
    }
}
