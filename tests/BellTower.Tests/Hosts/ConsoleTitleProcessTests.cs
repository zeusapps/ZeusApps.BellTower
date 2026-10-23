using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class ConsoleTitleProcessTests
{
    [Theory]
    [InlineData("claude", false)]
    [InlineData("claude", true)]
    [InlineData("codex", false)]
    [InlineData("codex", true)]
    public async Task HelperChangesOnlyADisposablePrivateConsoleAndRejectsDetachedAttachments(string provider, bool background)
    {
        using var workspace = new TestWorkspace();
        var ready = Path.Combine(workspace.Root, "ready.json");
        var observed = Path.Combine(workspace.Root, "title.txt");
        var finish = Path.Combine(workspace.Root, "finish");
        var restore = Path.Combine(workspace.Root, "restore");
        var script = Path.Combine(workspace.Root, "console.ps1");
        File.WriteAllText(script, $$"""
            Add-Type -TypeDefinition @'
            using System;
            using System.Runtime.InteropServices;
            using System.Text;
            public static class PrivateConsole {
                [DllImport("kernel32.dll")] public static extern bool AllocConsole();
                [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
                [DllImport("kernel32.dll")] public static extern uint GetConsoleProcessList([Out] uint[] processes, uint count);
                [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
                [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern bool SetConsoleTitle(string title);
                [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern uint GetConsoleTitle(StringBuilder text, uint count);
            }
            '@
            [void][PrivateConsole]::AllocConsole()
            $members = New-Object 'uint32[]' 64
            $count = [PrivateConsole]::GetConsoleProcessList($members, 64)
            if ($count -eq 0 -or @($members | Where-Object { $_ -ne 0 -and $_ -ne $PID }).Count -ne 0) { throw 'The test console is not private.' }
            [void][PrivateConsole]::ShowWindow([PrivateConsole]::GetConsoleWindow(), 0)
            [void][PrivateConsole]::SetConsoleTitle('private-original')
            [IO.File]::WriteAllText({{PowerShellModule.Literal(ready)}}, ('{"pid":' + $PID + '}'))
            $deadline = [DateTime]::UtcNow.AddSeconds(20)
            while (!(Test-Path -LiteralPath {{PowerShellModule.Literal(finish)}}) -and [DateTime]::UtcNow -lt $deadline) {
                if (Test-Path -LiteralPath {{PowerShellModule.Literal(restore)}}) { [void][PrivateConsole]::SetConsoleTitle('private-restored') }
                $title = New-Object Text.StringBuilder 1024
                [void][PrivateConsole]::GetConsoleTitle($title, 1024)
                [IO.File]::WriteAllText({{PowerShellModule.Literal(observed)}}, $title.ToString())
                Start-Sleep -Milliseconds 50
            }
            """, new UTF8Encoding(true));
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script })
        {
            start.ArgumentList.Add(argument);
        }
        using var console = Process.Start(start)!;
        var error = console.StandardError.ReadToEndAsync();
        var output = console.StandardOutput.ReadToEndAsync();
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!File.Exists(ready) && !console.HasExited && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
            Assert.True(File.Exists(ready), console.HasExited ? await error : "Private console startup timed out.");
            using var report = JsonDocument.Parse(File.ReadAllText(ready));
            var identity = ProcessProbe.Identify(report.RootElement.GetProperty("pid").GetInt32())!;
            var attachment = new Attachment("attachment-1", provider, identity, new("windows-terminal", identity), workspace.Root, DateTimeOffset.UtcNow, ManagedTitle: true, ManagedBackground: background);
            new AttachmentStore(workspace.Paths).Save(attachment);
            new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Register) with { Provider = provider, Directory = workspace.Root });
            var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-1"];
            var sessionIdentity = new IdentityStore(workspace.Paths).Get(provider + ":session-1", workspace.Root, "shared-session-name");
            var desired = new DesiredTerminalPresentation(attachment.Id, provider, attachment.RegisteredAt, identity, identity,
                "shared-session-name", background ? sessionIdentity.Palette.Background : null);
            File.Move(workspace.Paths.State, workspace.Paths.State + ".held");
            using (var helper = StartSynchronization(workspace, desired))
            {
                Assert.Equal(0, await Complete(helper));
            }
            File.Move(workspace.Paths.State + ".held", workspace.Paths.State);
            deadline = DateTime.UtcNow.AddSeconds(3);
            string? observedTitle = null;
            while (DateTime.UtcNow < deadline && (observedTitle = ReadObserved(observed)) != "shared-session-name")
            {
                await Task.Delay(50);
            }
            Assert.Equal("shared-session-name", observedTitle);
            var now = DateTimeOffset.UtcNow;
            var realGit = new GitReader();
            var cache = new CheckoutCache(realGit.ReadUncached, () => now);
            var isolated = new IsolatedTerminalPresentationWriter(workspace.Paths, BuiltApplication.Executable);
            using (var pump = new TerminalTitlePump(workspace.Paths, cache, isolated, () => now))
            {
                await pump.RefreshAsync([session]);
                var gitChildren = realGit.ChildLaunchCount;
                Assert.True(gitChildren > 0);
                Assert.Equal(1, isolated.LaunchCount);
                for (var tick = 0; tick < 100; tick++)
                {
                    await pump.RefreshAsync([session]);
                    await cache.ReadAsync(workspace.Root);
                }
                Assert.Equal(1, isolated.LaunchCount);
                Assert.Equal(gitChildren, realGit.ChildLaunchCount);
                sessionIdentity.ManualName = "changed-private-name";
                new IdentityStore(workspace.Paths).Save(sessionIdentity);
                await pump.RefreshAsync([session]);
                await pump.RefreshAsync([session]);
                Assert.Equal(2, isolated.LaunchCount);
                Assert.Equal(gitChildren, realGit.ChildLaunchCount);
            }
            Process queued;
            // Queue a request while detach owns the presentation mutex. It must revalidate
            // the ended attachment after acquiring the lock, even if it started beforehand.
            using (var gate = new DataLock(workspace.Paths, "presentation"))
            {
                queued = StartSynchronization(workspace, desired with { Title = "late-write-must-not-win" });
                new BellTower.Sessions.AttachmentLifecycle(workspace.Paths).Detach("attachment-1");
                File.WriteAllText(restore, "restore");
            }
            using (queued)
            {
                Assert.Equal(1, await Complete(queued));
            }
            deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline && (observedTitle = ReadObserved(observed)) != "private-restored")
            {
                await Task.Delay(50);
            }
            Assert.Equal("private-restored", observedTitle);
            Assert.Equal(ProcessLiveness.Live, ProcessProbe.Inspect(identity));
        }
        finally
        {
            File.WriteAllText(finish, "exit");
            if (!console.WaitForExit(5000))
            {
                console.Kill(entireProcessTree: true);
            }
            await Task.WhenAll(output, error);
        }
    }

    private static string? ReadObserved(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null; // The disposable console publishes repeatedly; retry within the deadline.
        }
    }

    private static Process StartSynchronization(TestWorkspace workspace, DesiredTerminalPresentation desired)
    {
        var start = new ProcessStartInfo(BuiltApplication.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        foreach (var argument in new[] { "sync-title", "--desired", "--data-dir", workspace.Paths.Root })
        {
            start.ArgumentList.Add(argument);
        }
        var helper = Process.Start(start)!;
        helper.StandardInput.Write(Json.Encode(desired));
        helper.StandardInput.Close();
        return helper;
    }

    private static async Task<int> Complete(Process helper)
    {
        var output = helper.StandardOutput.ReadToEndAsync();
        var error = helper.StandardError.ReadToEndAsync();
        Assert.True(helper.WaitForExit(5000));
        Assert.Equal("", await error);
        Assert.Equal("", await output);
        return helper.ExitCode;
    }
}
