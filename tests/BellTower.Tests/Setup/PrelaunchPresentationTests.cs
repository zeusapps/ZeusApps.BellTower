using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class PrelaunchPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColorPrecedesTheNativeAgentAndItsFirstHookUsesTheSamePalette(bool noBackground)
    {
        using var workspace = new TestWorkspace();
        var module = Path.Combine(workspace.Root, "BellTower.psm1");
        File.WriteAllText(module, PowerShellModule.Render(BuiltApplication.Executable, workspace.Paths.Root, ["claude"]), new UTF8Encoding(true));
        var fake = Path.Combine(workspace.Root, "fake.ps1");
        File.WriteAllText(fake, $$"""
            [Console]::WriteLine('AGENT-STARTED')
            @{session_id='prelaunch'; hook_event_name='SessionStart'; cwd=(Get-Location).Path} | ConvertTo-Json -Compress | & {{PowerShellModule.Literal(BuiltApplication.Executable)}} event --provider claude --data-dir $env:BELLTOWER_DATA_DIR | Out-Null
            Start-Sleep -Milliseconds 1000
            """, new UTF8Encoding(true));
        var script = Path.Combine(workspace.Root, "launch.ps1");
        File.WriteAllText(script, $$"""
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            Import-Module {{PowerShellModule.Literal(module)}}
            Start-ClaudeCode -Directory {{PowerShellModule.Literal(workspace.Root)}} -Name 'Task name' -Silent {{(noBackground ? "-NoBackground" : "")}} -Executable powershell.exe -AgentArguments @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',{{PowerShellModule.Literal(fake)}})
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

        start.Environment["TERM_PROGRAM"] = "vscode";
        start.Environment.Remove("WT_SESSION");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            Assert.True(process.WaitForExit(15000));
            Assert.Equal(0, process.ExitCode);
            Assert.Empty(await error);
            var text = await output;
            Assert.Contains("AGENT-STARTED", text);
            var color = Regex.Match(text, "\u001b\\]11;(#[0-9a-fA-F]{6})");
            Assert.Equal(!noBackground, color.Success);
            if (!noBackground)
            {
                Assert.True(color.Index < text.IndexOf("AGENT-STARTED", StringComparison.Ordinal));
                var identity = new IdentityStore(workspace.Paths).Get("claude:prelaunch", workspace.Root);
                Assert.Equal(color.Groups[1].Value, identity.Palette.Background);
                Assert.Contains("\u001b]110\u001b\\\u001b]111\u001b\\", text);
            }
            Assert.DoesNotContain('\a', text);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            var tray = new TrayController(workspace.Paths);
            if (tray.IsRunning())
            {
                tray.RequestExit();
            }

            var deadline = DateTime.UtcNow.AddSeconds(4);
            while (tray.IsRunning() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
        }
    }
}
