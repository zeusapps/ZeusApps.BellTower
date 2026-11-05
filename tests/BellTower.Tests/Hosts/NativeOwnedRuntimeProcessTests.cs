using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

// ANALYSIS-1502: a managed `Start-Codex` runtime runs hooks in an owned `codex app-server` whose parent is the Bell Tower
// runtime-host manager. Stand-in images are copies of cmd.exe named like the real images (manager `bell-tower.exe`, backend
// `codex.exe`), so the process tree, the CodexRuntime record and both hook entry points are real; no provider is launched.
// The backend waits for a go flag, so the test can write the runtime record (it needs the manager's identity) before the
// hook runs, and keeps running after the hook so the attachment is still live when it is inspected.
public sealed class NativeOwnedRuntimeProcessTests
{
    private const string Event = NativeAgentResolveProcessTests.Event;
    private const string Ingress = NativeAgentResolveProcessTests.Ingress;
    private const string Managed = "owned-runtime-attachment";
    private const string Direct = "own";
    private const string SharedUnknown = "shared:unknown";

    private sealed class FakeCodex : ICodexNameClient
    {
        public int Writes { get; private set; }
        public Task ConnectAsync(string endpoint, string token, CancellationToken cancellation) => Task.CompletedTask;
        public Task<string?> ReadNameAsync(string thread, CancellationToken cancellation) => Task.FromResult<string?>(null);
        public Task SetNameAsync(string thread, string name, CancellationToken cancellation)
        {
            Writes++;
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    // (provider, backend arguments, images between the manager and the backend joined by '+' or "", record variant, outcome).
    // The record variant says what the runtime record claims; "independent" starts the backend under a plain shell with no
    // manager above it while a live decoy manager and shell are recorded.
    private static readonly (string Provider, string Arguments, string Between, string Record, string Outcome)[] Rows =
    [
        // The owned backend under its manager with a live record is a managed direct attachment (the pre-2.2 result).
        ("codex", "app-server", "", "valid", Direct),
        // QA-2400: an npm launch (manager, node, backend) and a script-shim launch (manager, powershell, backend) are bound
        // again, and so is the deepest shape CodexRuntimeHost can produce (manager, powershell, node, backend).
        ("codex", "app-server", "node", "valid", Direct),
        ("codex", "app-server", "powershell", "valid", Direct),
        ("codex", "app-server", "powershell+node", "valid", Direct),
        // Beyond the bound the manager is no longer a launch shape.
        ("codex", "app-server", "powershell+node+cmd", "valid", SharedUnknown),
        // Every missing or wrong proof fails closed to the shared runtime, direct child or behind a launcher.
        ("codex", "app-server", "", "none", SharedUnknown),
        ("codex", "app-server", "", "wrong-pid", SharedUnknown),
        ("codex", "app-server", "", "wrong-time", SharedUnknown),
        ("codex", "app-server", "", "dead-manager", SharedUnknown),
        ("codex", "app-server", "", "dead-shell", SharedUnknown),
        ("codex", "app-server", "", "other-attachment", SharedUnknown),
        ("codex", "app-server", "node", "none", SharedUnknown),
        ("codex", "app-server", "node", "wrong-pid", SharedUnknown),
        ("codex", "app-server", "powershell", "wrong-time", SharedUnknown),
        ("codex", "app-server", "node", "dead-manager", SharedUnknown),
        ("codex", "app-server", "powershell", "dead-shell", SharedUnknown),
        ("codex", "app-server", "node", "other-attachment", SharedUnknown),
        // An independently started shared backend has a live record elsewhere but no manager above it.
        ("codex", "app-server", "", "independent", SharedUnknown),
        ("codex", "app-server", "node", "independent", SharedUnknown),
        // A backend started by a Codex client keeps its shared-backend result even when a live record exists.
        ("codex", "app-server", "codex", "valid", "shared:shared-backend"),
        // Claude managed launches never depend on a runtime record.
        ("claude", "", "", "valid", Direct),
        ("claude", "", "", "none", Direct)
    ];

    public static TheoryData<string, string, string, string, string, string> Data()
    {
        var data = new TheoryData<string, string, string, string, string, string>();
        var index = 0;
        foreach (var (provider, arguments, between, record, outcome) in Rows)
        {
            // Each row uses one entry point (alternating); the positive and the Claude rows use both.
            var both = outcome == Direct;
            foreach (var entry in new[] { Event, Ingress })
            {
                if (both || (index % 2 == 0) == (entry == Event))
                {
                    data.Add(entry, provider, arguments, between, record, outcome);
                }
            }
            index++;
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Data))]
    public async Task AnOwnedRuntimeBackendIsBoundOnlyThroughItsLiveRuntimeRecord(string entry, string provider, string arguments, string between, string record, string outcome)
    {
        using var workspace = new TestWorkspace();
        var fixture = NativeFixture.SessionStart(provider);
        var backendImage = StandIns.CopyImage(workspace, provider, "backend");
        var manager = StandIns.CopyImage(workspace, "bell-tower", "manager");
        var shellImage = StandIns.CopyImage(workspace, "pwsh", "shell");
        var go = Path.Combine(workspace.Root, "go.flag");
        var started = Path.Combine(workspace.Root, "started.flag");
        var marker = Path.Combine(workspace.Root, "done.flag");
        var backendScript = Path.Combine(workspace.Root, "backend.cmd");
        File.WriteAllText(backendScript,
            "@echo off\r\n" +
            $"echo started>\"{started}\"\r\n" +
            ":wait\r\n" +
            $"if exist \"{go}\" goto go\r\n" +
            "ping -n 1 127.0.0.1 >nul\r\n" +
            "goto wait\r\n" +
            ":go\r\n" +
            StandIns.Hook(entry, provider, workspace) + "\r\n" +
            $"echo done>\"{marker}\"\r\n" +
            "ping -n 60 127.0.0.1 >nul\r\n");
        var line = $"\"{backendImage}\" {StandIns.Under(arguments + (arguments.Length > 0 ? " --listen ws://127.0.0.1:1" : ""), $"\"{backendScript}\"")}";
        var launchers = between.Length == 0 ? [] : between.Split('+');
        for (var level = launchers.Length - 1; level >= 0; level--)
        {
            // Each launcher image runs a batch that starts the next one, so the images form a chain of direct parents.
            var image = StandIns.CopyImage(workspace, launchers[level], "between" + level);
            var script = Path.Combine(workspace.Root, "between" + level + ".cmd");
            File.WriteAllText(script, "@echo off\r\n" + line + "\r\n");
            line = $"\"{image}\" {StandIns.Under("", $"\"{script}\"")}";
        }
        var managerScript = Path.Combine(workspace.Root, "manager.cmd");
        File.WriteAllText(managerScript, "@echo off\r\n" + line + "\r\n");

        using var shell = Process.Start(new ProcessStartInfo(shellImage, "/s /c \"ping -n 120 127.0.0.1 >nul\"") { UseShellExecute = false, CreateNoWindow = true })!;
        // An independent backend sits under a plain shell; the manager image only exists as a recorded decoy.
        var root = record == "independent" ? shellImage : manager;
        var start = new ProcessStartInfo(root)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = StandIns.Under("", $"\"{managerScript}\"")
        };
        StandIns.ScrubEnvironment(start);
        // What the Start-Codex module exports, with a shell that deliberately differs from the runtime record's shell.
        var inherited = ProcessProbe.Identify(Environment.ProcessId)!;
        start.Environment["BELLTOWER_ATTACHMENT"] = Managed;
        start.Environment["BELLTOWER_DEDICATED"] = "1";
        start.Environment["BELLTOWER_SHELL_PID"] = inherited.Pid.ToString();
        using var process = Process.Start(start)!;
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var decoy = record == "independent"
                ? Process.Start(new ProcessStartInfo(manager, "/s /c \"ping -n 120 127.0.0.1 >nul\"") { UseShellExecute = false, CreateNoWindow = true })
                : null;
            var managerIdentity = ProcessProbe.Identify((decoy ?? process).Id)!;
            var shellIdentity = ProcessProbe.Identify(shell.Id)!;
            process.StandardInput.Write(fixture.PayloadText);
            process.StandardInput.Close();
            Assert.True(await StandIns.WaitFor(started), "The backend stand-in did not start.");
            var backend = Assert.Single(StandIns.Find(backendImage));

            var store = new CodexRuntimeStore(workspace.Paths);
            var runtime = record switch
            {
                // wrong-pid: a live process that is not an ancestor (the stand-in shell).
                "wrong-pid" => new CodexRuntime(Managed, "ws://127.0.0.1:1", shellIdentity, shellIdentity),
                "wrong-time" => new CodexRuntime(Managed, "ws://127.0.0.1:1", managerIdentity with { StartedUtcTicks = managerIdentity.StartedUtcTicks + 1 }, shellIdentity),
                "other-attachment" => new CodexRuntime("another-attachment", "ws://127.0.0.1:1", managerIdentity, shellIdentity),
                _ => new CodexRuntime(Managed, "ws://127.0.0.1:1", managerIdentity, shellIdentity)
            };
            if (record != "none")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(store.RecordPath(runtime.Attachment))!);
                AtomicFile.Write(store.RecordPath(runtime.Attachment), Json.Encode(runtime));
                File.WriteAllText(store.TokenPath(runtime.Attachment), "fixture-token");
            }
            if (record == "dead-shell")
            {
                shell.Kill(entireProcessTree: true);
                shell.WaitForExit();
            }
            if (record == "dead-manager")
            {
                // The backend outlives its manager; its recorded parent is gone.
                process.Kill();
                process.WaitForExit();
            }
            File.WriteAllText(go, "go");
            Assert.True(await StandIns.WaitFor(marker), "The hook did not finish.");
            var message = $"manager {(process.HasExited ? "exited" : "running")}; record={record}";
            var state = new StateStore(workspace.Paths).Drain();
            var attachments = new AttachmentStore(workspace.Paths);
            var session = state.Sessions[provider + ":" + fixture.SessionId];
            Assert.False(session.Ended);
            Assert.Empty(session.Waits);

            if (outcome == Direct)
            {
                var attachment = attachments.Get(Managed);
                Assert.True(attachment is not null, message);
                Assert.Equal(Managed, session.AttachmentId);
                Assert.Equal(backend.Pid, attachment.Process!.Pid);
                Assert.Equal("managed", attachment.Ownership);
                Assert.Equal(LifecycleOwners.BellTower, attachment.Evidence!.LifecycleOwner);
                Assert.DoesNotContain(attachments.List(), item => item.Id != Managed && item.Process is not null);
                if (provider == "codex")
                {
                    // The host shell is the record's, not the inherited variable's; the name service and the permission pump
                    // both resolve the runtime through this same lookup.
                    Assert.Equal(runtime.Shell, attachment.Host.Shell);
                    Assert.NotEqual(inherited, attachment.Host.Shell);
                    Assert.Equal(AgentModes.Unknown, attachment.Evidence.Mode);
                    var resolved = store.Get(session.AttachmentId);
                    Assert.NotNull(resolved);
                    Assert.Equal(resolved.Shell, attachments.Get(session.AttachmentId)!.Host.Shell);
                    var codex = new FakeCodex();
                    new IdentityStore(workspace.Paths).Update(session.Key, session.Directory, value => { value.ManualName = "Owned"; return true; });
                    new CodexNameService(workspace.Paths, () => codex).Sync(session, new GitContext());
                    var status = new IdentityStore(workspace.Paths).Get(session.Key, session.Directory).NativeSyncStatus;
                    Assert.True(codex.Writes == 1, "name sync status: " + status);
                }
                else
                {
                    Assert.Equal(AgentModes.InteractiveClient, attachment.Evidence.Mode);
                }
                return;
            }

            var mode = outcome.Split(':')[1];
            Assert.Null(attachments.Get(Managed));
            Assert.DoesNotContain(attachments.List(), item => item.Process is not null);
            var shared = attachments.Get(NativeAdoption.SharedId(provider, fixture.SessionId));
            Assert.True(shared is not null, message);
            Assert.Null(shared.Process);
            Assert.Null(shared.Host.Shell);
            Assert.Equal("shared-runtime", shared.Ownership);
            Assert.Equal(mode, shared.Evidence!.Mode);
            Assert.Equal(shared.Id, session.AttachmentId);
            // No verified runtime: the native-name path stays unavailable and writes nothing.
            var unavailable = new FakeCodex();
            new IdentityStore(workspace.Paths).Update(session.Key, session.Directory, current => { current.ManualName = "Owned"; return true; });
            new CodexNameService(workspace.Paths, () => unavailable).Sync(session, new GitContext());
            Assert.Equal(0, unavailable.Writes);
            Assert.StartsWith("unavailable", new IdentityStore(workspace.Paths).Get(session.Key, session.Directory).NativeSyncStatus);
        }
        finally
        {
            StandIns.KillUnder(workspace.Root);
        }
    }
}
