using System.Diagnostics;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Cli;

public sealed class CliTests
{
    [Theory]
    [InlineData("rename", "--session")]
    [InlineData("update", "--check", "--status")]
    [InlineData("rename", "--provider", "codex")]
    [InlineData("process-info", "--pid", "not-a-number")]
    [InlineData("setup", "--clients", "unknown")]
    [InlineData("import", "--badge", "badge.json")]
    [InlineData("launch", "codex", "--slient")]
    [InlineData("launch", "codex", "--provider", "claude")]
    public void InvalidCommandOptionsFailBeforeChangingConfiguration(params string[] arguments)
    {
        using var workspace = new TestWorkspace();
        var result = Run(workspace, null, arguments);
        Assert.NotEqual(0, result.Code);
        Assert.NotEmpty(result.Error);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "integration.json")));
        Assert.Empty(Directory.GetFiles(workspace.Paths.Attachments));
    }

    [Theory]
    [InlineData("event")]
    [InlineData("event", "--provider", "unknown")]
    [InlineData("event", "--provider", "codex", "--unexpected")]
    public void HookParserErrorsRemainNeutralAndSilent(params string[] arguments)
    {
        using var workspace = new TestWorkspace();
        var result = Run(workspace, "private payload", arguments);
        Assert.Equal(0, result.Code);
        Assert.Equal("{}", result.Output.Trim());
        Assert.Empty(result.Error);
        Assert.Empty(Directory.GetFiles(workspace.Paths.Inbox));
    }

    [Fact]
    public void CommandHelpDescribesOnlyThatCommandsOptions()
    {
        using var workspace = new TestWorkspace();
        var result = Run(workspace, null, "rename", "--help");
        Assert.Equal(0, result.Code);
        Assert.Contains("--session", result.Output);
        Assert.Contains("--name", result.Output);
        Assert.DoesNotContain("--clients", result.Output);
        Assert.Empty(Directory.GetFiles(workspace.Paths.Identities));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LaunchForwardsAgentArgumentsAndKeepsOwnMetadataSeparate(bool namedProvider)
    {
        using var workspace = new TestWorkspace();
        var script = Path.Combine(workspace.Root, "fake-agent.ps1");
        File.WriteAllText(script, """
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            @{ arguments = @($args); name = $env:BELLTOWER_NAME; role = $env:BELLTOWER_ROLE; dedicated = $env:BELLTOWER_DEDICATED } | ConvertTo-Json -Compress
            """);
        string[] forwarded = ["two words", "--name", "agent name", "--no-daemon", "--help", "@literal.rsp", "--", "last"];
        try
        {
            string[] provider = namedProvider ? ["--provider", "codex"] : ["codex"];
            var result = Run(workspace, null, ["launch", .. provider, "--executable", "powershell.exe", "--name", "Bell Tower name", "--role", "PM", "--silent", "--",
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, .. forwarded]);
            Assert.True(result.Code == 0, result.Error + result.Output);
            Assert.Empty(result.Error);
            using var report = JsonDocument.Parse(result.Output);
            Assert.Equal(forwarded, report.RootElement.GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal("Bell Tower name", report.RootElement.GetProperty("name").GetString());
            // --role is deprecated: accepted for existing scripts, ignored, and never exported to the agent.
            Assert.True(string.IsNullOrEmpty(report.RootElement.GetProperty("role").GetString()));
            Assert.Equal("1", report.RootElement.GetProperty("dedicated").GetString());
            var attachment = Directory.GetFiles(workspace.Paths.Attachments).Single();
            Assert.NotNull(AtomicFile.Read<Attachment>(attachment)!.EndedAt);
        }
        finally
        {
            var controller = new BellTower.Tray.TrayController(workspace.Paths);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!controller.IsRunning() && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }

            if (controller.IsRunning())
            {
                controller.RequestExit();
            }

            while (controller.IsRunning() && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static string Executable
    {
        get
        {
            var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
            var directory = testOutput;
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
            {
                directory = directory.Parent;
            }

            return Path.Combine(directory!.FullName, "src", "BellTower", "bin", testOutput.Parent!.Name, testOutput.Name, "bell-tower.exe");
        }
    }

    private static (int Code, string Output, string Error) Run(TestWorkspace workspace, string? input, params string[] args)
    {
        var info = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add("--data-dir");
        info.ArgumentList.Add(workspace.Paths.Root);
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        info.Environment.Remove("BELLTOWER_ATTACHMENT");
        using var process = Process.Start(info)!;
        if (input is not null)
        {
            process.StandardInput.Write(input);
        }

        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000))
        {
            process.Kill();
            throw new TimeoutException("Disposable Bell Tower process timed out.");
        }

        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    [Fact]
    public void HelpAndDiagnosticsAreSilentAndVersionIsUsable()
    {
        using var w = new TestWorkspace();
        Assert.Contains("Bell Tower", Run(w, null, "--help").Output);
        var expectedVersion = typeof(BellTower.Program).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+')[0];
        Assert.Equal(expectedVersion, Run(w, null, "--version").Output.Trim());
        Assert.Equal(0, Run(w, null, "doctor").Code);
        Assert.Equal(1, Run(w, null, "unknown-command").Code);
    }

    [Fact]
    public void HooksPersistOnlyNormalizedDataAndNeverSteerTheAgent()
    {
        using var w = new TestWorkspace();
        var result = Run(w, """{"session_id":"cli","hook_event_name":"Stop","cwd":"D:/test","last_assistant_message":"private prompt content"}""", "event", "--provider", "codex");
        Assert.Equal(0, result.Code);
        Assert.Equal("{}", result.Output.Trim());
        var files = Directory.GetFiles(w.Paths.Inbox);
        Assert.Single(files);
        Assert.DoesNotContain("private prompt content", File.ReadAllText(files[0]));
        Assert.Single(new StateStore(w.Paths).Drain().Sessions);
    }

    [Fact]
    public void MalformedHookStillExitsSuccessfullyWithoutPayloadLogging()
    {
        using var w = new TestWorkspace();
        var result = Run(w, "malformed secret payload", "event", "--provider", "codex");
        Assert.Equal(0, result.Code);
        Assert.Equal("{}", result.Output.Trim());
        Assert.Empty(Directory.GetFiles(w.Paths.Inbox));
        Assert.DoesNotContain("secret", File.ReadAllText(Path.Combine(w.Paths.Root, "bell-tower.log")));
    }

    [Fact]
    public void MetadataEditingDoesNotChangeWaitEpisode()
    {
        using var w = new TestWorkspace();
        var store = new StateStore(w.Paths);
        new EventStore(w.Paths).Publish(TestWorkspace.ConfirmedPermission());
        var episode = store.Drain().Sessions["claude:session-1"].Episode;
        var result = Run(w, null, "rename", "--session", "claude:session-1", "--name", "New task", "--change", "change-one");
        Assert.Equal(0, result.Code);
        Assert.Equal("New task", result.Output.Trim());
        Assert.Equal(episode, store.Read().Sessions["claude:session-1"].Episode);
        Assert.Equal(0, Run(w, null, "rename", "--session", "claude:session-1", "--name", "").Code);
        var identity = new IdentityStore(w.Paths).Get("claude:session-1", w.Root);
        Assert.Null(identity.ManualName);
        Assert.Equal("change-one", identity.Change);
        Assert.Equal(episode, store.Read().Sessions["claude:session-1"].Episode);
    }

    [Fact]
    public void TrayProcessesInboxWithoutSoundAndStopsCleanly()
    {
        using var w = new TestWorkspace();
        new EventStore(w.Paths).Publish(TestWorkspace.ConfirmedPermission());
        var result = Run(w, null, "tray", "--silent", "--duration-ms", "700");
        Assert.Equal(0, result.Code);
        Assert.Equal("", result.Error);
        Assert.Single(new StateStore(w.Paths).Read().Sessions);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ActualConsumerStartsAndRestartsWithCommittedHealthCapability(string provider)
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => { config.Muted = true; config.Voice = false; config.Updates.Enabled = false; });
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        new AttachmentStore(workspace.Paths).Save(new("attachment-1", provider, process, new("unknown", process), workspace.Root, DateTimeOffset.UtcNow));
        var observation = TestWorkspace.ConfirmedPermission() with { Provider = provider, Directory = workspace.Root };
        new EventStore(workspace.Paths).Publish(observation);
        var before = new StateStore(workspace.Paths).Drain().Sessions[observation.Key];
        var palette = new IdentityStore(workspace.Paths).Get(observation.Key, workspace.Root).Palette;
        Assert.Equal(1, new StateStore(workspace.Paths).Read().AttachmentHealthVersion);
        for (var restart = 0; restart < 2; restart++)
        {
            var result = Run(workspace, null, "tray", "--silent", "--duration-ms", "700");
            Assert.Equal(0, result.Code);
            Assert.Empty(result.Error);
            var state = new StateStore(workspace.Paths).Read();
            Assert.Equal(1, state.AttachmentHealthVersion);
            Assert.Equal("verified-live", state.Sessions[observation.Key].AttachmentHealth);
            Assert.Equal(before.Episode, state.Sessions[observation.Key].Episode);
            Assert.Equal(before.Since, state.Sessions[observation.Key].Since);
            Assert.Equal(palette, new IdentityStore(workspace.Paths).Get(observation.Key, workspace.Root).Palette);
        }
    }

    [Fact]
    public void DetachedTraySurvivesStartingProcessAndManagementIsScoped()
    {
        using var w = new TestWorkspace();
        Assert.Equal(0, Run(w, null, "start", "--silent").Code);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        bool running = false;
        while (DateTime.UtcNow < deadline)
        {
            using var report = JsonDocument.Parse(Run(w, null, "doctor").Output);
            if (report.RootElement.GetProperty("trayRunning").GetBoolean())
            {
                running = true;
                break;
            }

            Thread.Sleep(50);
        }

        try
        {
            Assert.True(running);
            Assert.Equal(0, Run(w, null, "toggle-voice").Code);
            Assert.False(new ConfigStore(w.Paths).Read().Voice);
        }
        finally
        {
            Run(w, null, "exit");
            while (DateTime.UtcNow < deadline)
            {
                using var report = JsonDocument.Parse(Run(w, null, "doctor").Output);
                if (!report.RootElement.GetProperty("trayRunning").GetBoolean())
                {
                    break;
                }

                Thread.Sleep(50);
            }
        }
    }

    [Fact]
    public void StatusLineAndPaletteWorkOnACleanCodexOnlyDataDirectory()
    {
        using var w = new TestWorkspace();
        var result = Run(w, """{"session_id":"new","cwd":"D:/a/project"}""", "statusline", "--provider", "codex", "--no-color");
        Assert.Equal(0, result.Code);
        Assert.Contains("[ project ]", result.Output);
        Assert.False(Directory.Exists(Path.Combine(w.Root, "home", ".claude")));
    }

    [Fact]
    public async Task GeneratedPowerShellHookCommandPreservesUnicodeAndFinishesWithinItsTimeout()
    {
        using var w = new TestWorkspace();
        var setup = new BellTower.Setup.IntegrationSetup(w.Paths, Executable, Path.Combine(w.Root, "home"));
        setup.Plan("codex").Apply(w.Paths);
        var settings = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(w.Root, "home", ".codex", "hooks.json")))!;
        var command = settings["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]!.GetValue<string>();
        var encoded = command.Split(' ').Last();
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[]
        {
            "-NoProfile",
            "-NonInteractive",
            "-EncodedCommand",
            encoded
        })
        {
            start.ArgumentList.Add(arg);
        }

        var clock = Stopwatch.StartNew();
        using var process = Process.Start(start)!;
        process.StandardInput.Write(JsonSerializer.Serialize(new
        {
            session_id = "pipeline",
            hook_event_name = "Stop",
            cwd = "D:/unicode-Δ"
        }));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(3000));
        Assert.Equal(0, process.ExitCode);
        Assert.Equal("{}", (await output).Trim());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), await errors);
        var state = new StateStore(w.Paths).Drain();
        Assert.Equal("D:/unicode-Δ", state.Sessions["codex:pipeline"].Directory);
    }
}
