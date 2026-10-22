using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using BellTower.Core;
using BellTower.Distribution;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Updates;

namespace BellTower.Smoke;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.FirstOrDefault() == "app-server")
            { FixtureServer.RunAsync(args).GetAwaiter().GetResult(); return 0; }
            if (args is ["--fixture-agent", var finish])
            {
                DistributionJson.Write(finish + ".start.json", new { directory = Environment.CurrentDirectory, marker = Environment.GetEnvironmentVariable("BELLTOWER_FIXTURE_MARKER") });
                Wait(() => File.Exists(finish), 180000);
                return 7;
            }
            if (args is ["--fixture-shell", var root])
            {
                _ = AllocConsole();
                _ = ShowWindow(GetConsoleWindow(), 0);
                File.WriteAllText(Path.Combine(root, "shell.ready"), Environment.ProcessId.ToString());
                Wait(() => File.Exists(Path.Combine(root, "finish")), 180000);
                return 0;
            }
            if (args.Length != 2)
            { throw new ArgumentException("Expected baseline ZIP and candidate ZIP."); }
            Run(Path.GetFullPath(args[0]), Path.GetFullPath(args[1]));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Run(string baseline, string candidate)
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "update-acceptance", "run-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var installationRoot = Path.Combine(root, "installation");
        ZipFile.ExtractToDirectory(baseline, installationRoot);
        var installation = new Installation(installationRoot);
        var original = installation.Selection().Version;
        var offer = DistributionJson.Read<ReleaseOffer>(candidate[..^4] + ".release.json");
        Assert(original != offer.Version, "Acceptance requires distinct compiled versions.");
        var paths = new DataPaths(Path.Combine(root, "custom-data"));
        paths.Ensure();
        new ConfigStore(paths).Update(c => { c.Muted = true; c.Updates.Enabled = false; });
        var own = Environment.ProcessPath!;
        using var shell = Start(own, ["--fixture-shell", root]);
        Process? manager = null;
        Process? launched = null;
        try
        {
            Wait(() => File.Exists(Path.Combine(root, "shell.ready")));
            var shellIdentity = ProcessProbe.Identify(int.Parse(File.ReadAllText(Path.Combine(root, "shell.ready"))))!;
            var oldPayload = Path.Combine(installation.Payload(original), "bell-tower.exe");
            var start = Info(oldPayload, ["codex-runtime-host", "--attachment", "fixture", "--executable", own, "--data-dir", paths.Root]);
            start.Environment["BELLTOWER_SHELL_PID"] = shellIdentity.Pid.ToString();
            start.Environment["BELLTOWER_FIXTURE_ROOT"] = root;
            manager = Process.Start(start)!;
            var runtimes = new CodexRuntimeStore(paths);
            Wait(() => runtimes.Get("fixture") is not null || manager.HasExited);
            Assert(!manager.HasExited, "Dedicated runtime fixture did not become ready.");
            var runtime = runtimes.Get("fixture")!;
            new AttachmentStore(paths).Save(new("fixture", "codex", shellIdentity, new HostTarget("vscode", shellIdentity), root, DateTimeOffset.UtcNow));
            AtomicFile.Write(Path.Combine(paths.Hosts, "fixture.json"), Json.Encode(new HostRegistration("fixture", shellIdentity, DateTimeOffset.UtcNow, [shellIdentity.Pid])));
            var identities = new IdentityStore(paths);
            var identity = identities.Get("codex:fixture-session", root, "fixture original");
            var events = new EventStore(paths);
            events.Publish(new Observation { Provider = "codex", SessionId = "fixture-session", AttachmentId = "fixture", Kind = EventKind.Register, Directory = root });
            events.Publish(new Observation { Provider = "codex", SessionId = "fixture-session", AttachmentId = "fixture", Kind = EventKind.Permission, Directory = root, At = DateTimeOffset.UtcNow.AddSeconds(-30), OperationId = "permission" });
            var initial = new StateStore(paths).Drain().Sessions["codex:fixture-session"];
            new StateStore(paths).MarkViewed(initial.Key, initial.Episode!);
            events.Publish(new Observation { Provider = "claude", SessionId = "dismissed", AttachmentId = "fixture", Kind = EventKind.Question, Directory = root });
            var dismissed = new StateStore(paths).Drain().Sessions["claude:dismissed"];
            new StateStore(paths).Acknowledge(dismissed.Key);
            RunCommand(installation.Launcher, ["rename", "--session", initial.Key, "--name", "literal $() Ω's \"quoted\" path\\", "--data-dir", paths.Root]);
            Assert(identities.Get(initial.Key, root).ManualName == "literal $() Ω's \"quoted\" path\\", "Launcher argument forwarding changed text.");
            RunCommand(installation.Launcher, ["start", "--silent", "--data-dir", paths.Root]);
            Wait(() => File.Exists(UpdateTransaction.Presence(paths)));
            Wait(() => Directory.GetFiles(root, "read-*").Length > 0);
            Wait(() => identities.Get(initial.Key, root).NativeSyncStatus == "synchronized");
            File.WriteAllText(Path.Combine(root, "native-name.txt"), "native user override");
            Wait(() => !identities.Get(initial.Key, root).NativeSyncEnabled);
            var beforeReads = Directory.GetFiles(root, "read-*").Length;
            var agentFinish = Path.Combine(root, "agent.finish");
            var launchStart = Info(installation.Launcher, ["launch", "claude", "--executable", own, "--data-dir", paths.Root, "--silent", "--", "--fixture-agent", agentFinish]);
            launchStart.WorkingDirectory = root;
            launchStart.Environment["BELLTOWER_FIXTURE_MARKER"] = "inherited-fixture-value";
            launched = Process.Start(launchStart)!;
            Assert(!launched.WaitForExit(200), "Pinned launch fixture exited early.");
            Wait(() => File.Exists(agentFinish + ".start.json"));
            var launchReport = DistributionJson.Read<System.Text.Json.JsonElement>(agentFinish + ".start.json");
            Assert(launchReport.GetProperty("directory").GetString() == root && launchReport.GetProperty("marker").GetString() == "inherited-fixture-value", "Bootstrap did not preserve working directory/environment.");
            var plan = UpdateTransaction.Stage(installation, paths, candidate, offer);
            UpdateTransaction.Start(plan);
            Wait(() => File.Exists(UpdateTransaction.Marker(paths)) && !new BellTower.Tray.TrayController(paths).IsRunning());
            events.Publish(new Observation { Provider = "claude", SessionId = "during-gap", AttachmentId = "fixture", Kind = EventKind.Question, Directory = root });
            using var racingStart = Start(installation.Launcher, ["start", "--silent", "--data-dir", paths.Root]);
            var statusStart = Info(installation.Launcher, ["update", "--status"]);
            statusStart.RedirectStandardOutput = true;
            statusStart.RedirectStandardError = true;
            using var statusReader = Process.Start(statusStart)!;
            // Real new-version hooks run through the stable entry point while activation is in progress.
            var hookStart = Info(installation.Launcher, ["event", "--provider", "codex", "--data-dir", paths.Root]);
            hookStart.RedirectStandardInput = true;
            hookStart.RedirectStandardOutput = true;
            hookStart.RedirectStandardError = true;
            hookStart.Environment["BELLTOWER_ATTACHMENT"] = "fixture";
            hookStart.Environment["BELLTOWER_DEDICATED"] = "1";
            using (var hook = Process.Start(hookStart)!)
            {
                hook.StandardInput.Write("{\"session_id\":\"fixture-session\",\"hook_event_name\":\"PermissionRequest\",\"tool_use_id\":\"permission\",\"cwd\":" + System.Text.Json.JsonSerializer.Serialize(root) + "}");
                hook.StandardInput.Close();
                Assert(hook.WaitForExit(15000) && hook.ExitCode == 0, "Hook delivery failed during activation.");
                Assert(hook.StandardOutput.ReadToEnd().Trim() == "{}", "Hook protocol output changed.");
            }
            Wait(() => File.Exists(UpdateTransaction.OutcomePath(installation)) && DistributionJson.Read<UpdateOutcome>(UpdateTransaction.OutcomePath(installation)).State is "monitoring-restored" or "failed" or "rolled-back", 60000);
            var outcome = DistributionJson.Read<UpdateOutcome>(UpdateTransaction.OutcomePath(installation));
            Assert(outcome.State == "monitoring-restored", outcome.Message);
            Assert(installation.Selection().Version == offer.Version, "New version not selected.");
            Assert(!manager.HasExited && !launched.HasExited && runtimes.Get("fixture") == runtime, "Existing manager/agent/runtime identity changed.");
            Assert(RunCommand(installation.Launcher, ["--version"]).Trim() == offer.Version, "New invocations use wrong version.");
            Wait(() => Directory.GetFiles(root, "read-*").Length > beforeReads);
            var restored = new StateStore(paths).Read().Sessions[initial.Key];
            Assert(new StateStore(paths).Read().Sessions.ContainsKey("claude:during-gap"), "Event queued while tray was stopped was lost.");
            Assert(!restored.Ended && restored.Episode == initial.Episode && restored.ViewedEpisode == initial.Episode, "Attention episode/viewed state changed.");
            Assert(identities.Get(initial.Key, root).Palette == identity.Palette, "Palette changed.");
            Assert(!identities.Get(initial.Key, root).NativeSyncEnabled, "Native user-name ownership changed across update.");
            Assert(new StateStore(paths).Read().Sessions[dismissed.Key].AcknowledgedEpisode == dismissed.Episode, "Dismissed state changed across update.");
            AtomicFile.Write(Path.Combine(paths.Hosts, "fixture.json"), Json.Encode(new HostRegistration("fixture", shellIdentity, DateTimeOffset.UtcNow, [shellIdentity.Pid])));
            Assert(new Navigator(paths).Precision(new AttachmentStore(paths).Get("fixture")) == "exact terminal", "Verified terminal ownership was not retained.");
            Assert(statusReader.WaitForExit(15000) && statusReader.ExitCode == 0, "Status reading interfered with activation.");
            Assert(racingStart.WaitForExit(15000) && racingStart.ExitCode == 0, "Racing start failed during the update.");
            var competingRefused = false;
            try
            { UpdateTransaction.Stage(installation, paths, candidate, offer); }
            catch (IOException) { competingRefused = true; }
            Assert(competingRefused, "Superseded update was not refused.");
            Task.Run(() => UpdateTransaction.Cleanup(installation)).GetAwaiter().GetResult();
            Assert(!manager.HasExited && Directory.Exists(installation.Payload(original)), "Cleanup interfered with the old runtime.");
            Assert(Directory.Exists(installation.Payload(original)), "Live old payload was deleted.");
            RunCommand(installation.Launcher, ["update", "--status"]);
            // A second disposable installation proves stopped-tray activation and direct payload use without touching the user's install.
            var stoppedRoot = Path.Combine(root, "stopped-installation");
            ZipFile.ExtractToDirectory(baseline, stoppedRoot);
            var stopped = new Installation(stoppedRoot);
            var stoppedPaths = new DataPaths(Path.Combine(root, "stopped-data"));
            stoppedPaths.Ensure();
            var stoppedPlan = UpdateTransaction.Stage(stopped, stoppedPaths, candidate, offer);
            UpdateTransaction.Start(stoppedPlan);
            Wait(() => File.Exists(UpdateTransaction.OutcomePath(stopped)) && DistributionJson.Read<UpdateOutcome>(UpdateTransaction.OutcomePath(stopped)).State is "activated" or "failed", 60000);
            Assert(stopped.Selection().Version == offer.Version && !new BellTower.Tray.TrayController(stoppedPaths).IsRunning(), "Stopped tray activation started a tray or failed.");
            VerifyRollback(root, baseline, candidate, offer);
            Console.WriteLine("PACKAGED UPDATE PASSED: old " + original + " -> new " + offer.Version + "; real dedicated manager/backend, live agent/launch, authenticated reconnect, hook delivery, viewed state, custom data, stopped tray. Evidence: " + root);
            File.WriteAllText(Path.Combine(root, "agent.finish"), "done");
            Assert(launched.WaitForExit(10000) && launched.ExitCode == 7, "Launcher did not preserve agent exit status.");
        }
        finally
        {
            var controller = new BellTower.Tray.TrayController(paths);
            if (controller.IsRunning())
            { controller.RequestExit(); Wait(() => !controller.IsRunning()); }
            File.WriteAllText(Path.Combine(root, "agent.finish"), "done");
            new CodexRuntimeStore(paths).StopAndWait("fixture");
            File.WriteAllText(Path.Combine(root, "finish"), "done");
            foreach (var child in new[] { manager, launched, shell })
            {
                if (child is not null && !child.HasExited && !child.WaitForExit(5000))
                { child.Kill(true); child.WaitForExit(); }
            }
            manager?.Dispose();
            launched?.Dispose();
        }
    }
    private static void VerifyRollback(string root, string baseline, string candidate, ReleaseOffer offer)
    {
        var directory = Path.Combine(root, "rollback-installation");
        ZipFile.ExtractToDirectory(baseline, directory);
        var installation = new Installation(directory);
        var old = installation.Selection().Version;
        var paths = new DataPaths(Path.Combine(root, "rollback-data"));
        paths.Ensure();
        new ConfigStore(paths).Update(c => { c.Muted = true; c.Updates.Enabled = false; });
        RunCommand(installation.Launcher, ["start", "--silent", "--data-dir", paths.Root]);
        Wait(() => File.Exists(UpdateTransaction.Presence(paths)));
        try
        {
            var sentinel = File.ReadAllText(paths.Config);
            var plan = UpdateTransaction.Stage(installation, paths, candidate, offer);
            var sabotage = Task.Run(() =>
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromSeconds(30))
                {
                    if (DistributionJson.Read<VersionSelection>(installation.SelectionPath).Version == offer.Version)
                    {
                        File.Move(Path.Combine(directory, "versions", offer.Version, "release.json"), Path.Combine(directory, "updates", plan.Id, "candidate-metadata.saved"));
                        return;
                    }
                    Thread.Sleep(1);
                }
                throw new TimeoutException("Could not inject candidate-readiness failure.");
            });
            UpdateTransaction.Start(plan);
            sabotage.GetAwaiter().GetResult();
            Wait(() => File.Exists(UpdateTransaction.OutcomePath(installation)) && DistributionJson.Read<UpdateOutcome>(UpdateTransaction.OutcomePath(installation)).State is "rolled-back" or "failed", 60000);
            var outcome = DistributionJson.Read<UpdateOutcome>(UpdateTransaction.OutcomePath(installation));
            Assert(outcome.State == "rolled-back", "Readiness rollback failed: " + outcome.Message);
            Assert(installation.Selection().Version == old && File.ReadAllText(paths.Config) == sentinel && new BellTower.Tray.TrayController(paths).IsRunning(), "Rollback changed data or failed to recover old monitoring.");
            Console.WriteLine("PACKAGED ROLLBACK PASSED: candidate metadata/readiness failure restored prior selection and tray; data preserved.");
        }
        finally
        {
            var controller = new BellTower.Tray.TrayController(paths);
            if (controller.IsRunning())
            { controller.RequestExit(); Wait(() => !controller.IsRunning()); }
        }
    }
    private static ProcessStartInfo Info(string exe, string[] args)
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in args)
        { start.ArgumentList.Add(arg); }
        return start;
    }
    private static Process Start(string exe, string[] args) => Process.Start(Info(exe, args)) ?? throw new IOException("Fixture could not start.");
    private static string RunCommand(string exe, string[] args)
    {
        var start = Info(exe, args);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        Assert(child.WaitForExit(20000), "Fixture command timed out.");
        Assert(child.ExitCode == 0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
    private static void Assert(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
    private static void Wait(Func<bool> condition, int milliseconds = 20000)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        { if (condition()) { return; } Thread.Sleep(50); }
        throw new TimeoutException("Packaged fixture timed out.");
    }
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
}
