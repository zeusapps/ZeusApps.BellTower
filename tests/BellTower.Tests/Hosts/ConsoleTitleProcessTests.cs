using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class ConsoleTitleProcessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HelperChangesOnlyADisposablePrivateConsoleAndRejectsDetachedAttachments(bool background)
    {
        using var workspace = new TestWorkspace();
        var ready = Path.Combine(workspace.Root, "ready.json");
        var observed = Path.Combine(workspace.Root, "title.txt");
        var finish = Path.Combine(workspace.Root, "finish");
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
            new AttachmentStore(workspace.Paths).Save(new("attachment-1", "claude", identity, new("windows-terminal", identity), workspace.Root, DateTimeOffset.UtcNow, ManagedTitle: true, ManagedBackground: background));
            new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Register));
            new StateStore(workspace.Paths).Drain();
            new IdentityStore(workspace.Paths).Get("claude:session-1", workspace.Root, "shared-session-name");
            Assert.Equal(0, await Synchronize(workspace));
            deadline = DateTime.UtcNow.AddSeconds(3);
            while ((!File.Exists(observed) || File.ReadAllText(observed) != "shared-session-name") && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
            Assert.Equal("shared-session-name", File.ReadAllText(observed));
            new BellTower.Sessions.AttachmentLifecycle(workspace.Paths).Detach("attachment-1");
            Assert.Equal(1, await Synchronize(workspace));
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

    private static async Task<int> Synchronize(TestWorkspace workspace)
    {
        var start = new ProcessStartInfo(BuiltApplication.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "sync-title", "--session", "claude:session-1", "--data-dir", workspace.Paths.Root })
        {
            start.ArgumentList.Add(argument);
        }
        using var helper = Process.Start(start)!;
        var output = helper.StandardOutput.ReadToEndAsync();
        var error = helper.StandardError.ReadToEndAsync();
        Assert.True(helper.WaitForExit(5000));
        Assert.Equal("", await error);
        Assert.Equal("", await output);
        return helper.ExitCode;
    }
}
