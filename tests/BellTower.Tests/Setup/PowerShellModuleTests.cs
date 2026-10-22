using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class PowerShellModuleTests
{
    [Theory]
    [InlineData("claude", false, false)]
    [InlineData("claude", true, false)]
    [InlineData("codex", false, false)]
    [InlineData("codex", true, false)]
    [InlineData("claude", false, true)]
    [InlineData("claude", true, true)]
    [InlineData("codex", false, true)]
    [InlineData("codex", true, true)]
    public async Task SessionFunctionsLaunchNativeChildrenAndRestoreShellEnvironment(string provider, bool powerShellCore, bool legacyRole)
    {
        using var workspace = new TestWorkspace();
        var module = Path.Combine(workspace.Root, "BellTower.psm1");
        File.WriteAllText(module, PowerShellModule.Render(BuiltApplication.Executable, workspace.Paths.Root, [provider]), new UTF8Encoding(true));
        var snapshot = Path.Combine(workspace.Root, "agent.json");
        var release = Path.Combine(workspace.Root, "release");
        var projectDirectory = Path.Combine(workspace.Root, "Project Ω's");
        Directory.CreateDirectory(projectDirectory);
        var fake = Path.Combine(workspace.Root, "fake-agent.ps1");
        var fakeBody = $$"""
            param([string]$SnapshotPath, [string]$ReleasePath)
            $savedArguments = @($args)
            $hook = @{ session_id = 'native-test'; hook_event_name = 'SessionStart'; cwd = (Get-Location).Path } | ConvertTo-Json -Compress
            $hook | & {{PowerShellModule.Literal(BuiltApplication.Executable)}} event --provider {{provider}} --data-dir $env:BELLTOWER_DATA_DIR | Out-Null
            $details = @{ pid = $PID; shell = $env:BELLTOWER_SHELL_PID; name = $env:BELLTOWER_NAME; role = $env:BELLTOWER_ROLE; dedicated = $env:BELLTOWER_DEDICATED; arguments = $savedArguments; cwd = (Get-Location).Path }
            [IO.File]::WriteAllText($SnapshotPath + '.tmp', ($details | ConvertTo-Json -Compress))
            Move-Item -LiteralPath ($SnapshotPath + '.tmp') -Destination $SnapshotPath
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (!(Test-Path -LiteralPath $ReleasePath) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
            exit 7
            """;
        File.WriteAllText(fake, fakeBody, new UTF8Encoding(true));
        var fakeExecutable = "powershell.exe";
        string[] nativeArguments = ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", fake, snapshot, release, "two words", "--flag"];
        if (provider == "codex")
        {
            fakeExecutable = Path.Combine(workspace.Root, "fake-agent.cmd");
            File.WriteAllText(fakeExecutable, $"@echo off\r\npowershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{fake}\" \"{snapshot}\" \"{release}\" %*\r\nexit /b %errorlevel%\r\n");
            // Exercise the explicit standalone path with this non-server fake agent.
            nativeArguments = ["--no-daemon", "two words", "--flag"];
        }
        var script = Path.Combine(workspace.Root, "launch.ps1");
        var function = (provider == "claude" ? "Start-ClaudeCode" : "Start-Codex") + (legacyRole ? "PM" : "");
        File.WriteAllText(script, $$"""
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            Import-Module {{PowerShellModule.Literal(module)}} -Force
            $beforeDirectory = (Get-Location).Path
            {{function}} -Name 'Task Ω' -Directory {{PowerShellModule.Literal(projectDirectory)}} -Silent -Executable {{PowerShellModule.Literal(fakeExecutable)}} -AgentArguments @({{string.Join(", ", nativeArguments.Select(PowerShellModule.Literal))}})
            @{ code = $LASTEXITCODE; restored = $env:BELLTOWER_NAME; directoryRestored = ((Get-Location).Path -eq $beforeDirectory) } | ConvertTo-Json -Compress
            """, new UTF8Encoding(true));
        var shell = powerShellCore ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe") : "powershell.exe";
        var start = new ProcessStartInfo(shell)
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
        start.Environment["BELLTOWER_NAME"] = "outer value";
        start.Environment.Remove("TERM_PROGRAM");
        start.Environment.Remove("WT_SESSION");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!File.Exists(snapshot) && !process.HasExited && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
            Assert.True(File.Exists(snapshot), process.HasExited ? await error : "The disposable agent did not report startup.");
            using var agent = JsonDocument.Parse(File.ReadAllText(snapshot));
            var ancestors = ProcessProbe.Ancestors(agent.RootElement.GetProperty("pid").GetInt32());
            Assert.Contains(ancestors, ancestor => ancestor.Pid == process.Id);
            Assert.DoesNotContain(ancestors.TakeWhile(ancestor => ancestor.Pid != process.Id),
                ancestor => ancestor.Name.Equals("bell-tower.exe", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(process.Id.ToString(), agent.RootElement.GetProperty("shell").GetString());
            Assert.Equal("Task Ω", agent.RootElement.GetProperty("name").GetString());
            Assert.Equal(projectDirectory, agent.RootElement.GetProperty("cwd").GetString());
            var role = agent.RootElement.GetProperty("role").GetString();
            Assert.True(legacyRole ? role == "PM" : string.IsNullOrEmpty(role));
            string[] expectedArguments = provider == "codex" ? ["--no-daemon", "two words", "--flag"] : ["two words", "--flag"];
            Assert.Equal(expectedArguments, agent.RootElement.GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
            File.WriteAllText(release, "exit");
            Assert.True(process.WaitForExit(10000));
            var errors = await error;
            Assert.True(string.IsNullOrEmpty(errors), errors);
            using var result = JsonDocument.Parse(await output);
            Assert.Equal(7, result.RootElement.GetProperty("code").GetInt32());
            Assert.Equal("outer value", result.RootElement.GetProperty("restored").GetString());
            Assert.True(result.RootElement.GetProperty("directoryRestored").GetBoolean());
            var finalState = new StateStore(workspace.Paths).Drain();
            Assert.True(finalState.Sessions.ContainsKey(provider + ":native-test"),
                (File.Exists(Path.Combine(workspace.Paths.Root, "bell-tower.log")) ? File.ReadAllText(Path.Combine(workspace.Paths.Root, "bell-tower.log")) : "no log"));
            Assert.True(finalState.Sessions[provider + ":native-test"].Ended);
            Assert.Equal("Task Ω", new IdentityStore(workspace.Paths).Get(provider + ":native-test", workspace.Root).Name);
        }
        finally
        {
            File.WriteAllText(release, "exit");
            if (!process.HasExited && !process.WaitForExit(12000))
            {
                process.Kill(entireProcessTree: true);
            }
            var tray = new TrayController(workspace.Paths);
            if (tray.IsRunning())
            {
                tray.RequestExit();
            }
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (tray.IsRunning() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
        }
    }
}
