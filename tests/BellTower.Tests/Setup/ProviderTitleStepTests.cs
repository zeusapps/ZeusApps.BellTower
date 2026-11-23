using System.Text.Json.Nodes;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

/// <summary>Task 4.7 (design decision 11): the opt-in provider title step, and Bell Tower writing a title only when it is applied.</summary>
public sealed class ProviderTitleStepTests
{
    private sealed record Fixture(TestWorkspace Workspace, string Home, IntegrationSetup Setup)
    {
        public string ClaudeSettings => Path.Combine(Home, ".claude", "settings.json");
        public string CodexConfig => Path.Combine(Home, ".codex", "config.toml");
    }

    private static Fixture Create(TestWorkspace workspace)
    {
        var home = Path.Combine(workspace.Root, "home");
        // The title state reads the same disposable profile the setup edits, never a real one.
        ProviderTitleFixture.Use(provider => Path.Combine(home, "." + provider));
        return new(workspace, home, new IntegrationSetup(workspace.Paths, "C:/Bell Tower/bell-tower.exe", home));
    }

    // True for a session of the provider that starts a minute from now, i.e. after any setting written during the test.
    private static bool Titled(TestWorkspace workspace, string provider) => ProviderTitleFixture.DisabledFor(workspace.Paths, provider, TimeSpan.FromSeconds(-60));

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    // ---- Claude Code ----------------------------------------------------------------------------------------------------

    [Fact]
    public void ClaudePreviewApplyRepeatAndRemoveKeepUnrelatedSettingsAndTheOtherProvider()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        var original = "{\n  \"model\": \"x\",\n  \"env\": {\n    \"KEEP\": \"yes\"\n  },\n  \"hooks\": {}\n}\n";
        Write(f.ClaudeSettings, original);
        Write(f.CodexConfig, "model = \"y\"\n");

        var preview = f.Setup.PlanProviderTitles("claude");
        Assert.Equal(original, File.ReadAllText(f.ClaudeSettings));
        Assert.Contains(preview.Notes, note => note.Contains("CLAUDE_CODE_DISABLE_TERMINAL_TITLE=1"));
        Assert.Contains(preview.Notes, note => note.Contains("whole provider profile") || note.Contains("every session of that provider profile"));
        Assert.DoesNotContain(preview.Changes, change => change.Path == f.CodexConfig);

        preview.Apply(workspace.Paths);
        var applied = JsonNode.Parse(File.ReadAllText(f.ClaudeSettings))!;
        Assert.Equal("1", applied["env"]!["CLAUDE_CODE_DISABLE_TERMINAL_TITLE"]!.GetValue<string>());
        Assert.Equal("yes", applied["env"]!["KEEP"]!.GetValue<string>());
        Assert.Equal("x", applied["model"]!.GetValue<string>());
        Assert.Equal("model = \"y\"\n", File.ReadAllText(f.CodexConfig));
        Assert.True(Directory.Exists(Path.Combine(workspace.Paths.Root, "integration-backups")));
        Assert.True(Titled(workspace, "claude"));
        Assert.False(Titled(workspace, "codex"));

        // Repeating changes nothing.
        var repeat = f.Setup.PlanProviderTitles("claude");
        Assert.DoesNotContain(repeat.Changes, change => change.Path == f.ClaudeSettings);
        var textBefore = File.ReadAllText(f.ClaudeSettings);
        repeat.Apply(workspace.Paths);
        Assert.Equal(textBefore, File.ReadAllText(f.ClaudeSettings));

        f.Setup.PlanProviderTitles("claude", remove: true).Apply(workspace.Paths);
        Assert.Equal(JsonNode.Parse(original)!.ToJsonString(), JsonNode.Parse(File.ReadAllText(f.ClaudeSettings))!.ToJsonString());
        Assert.False(Titled(workspace, "claude"));
    }

    [Fact]
    public void ClaudeSetupCreatesAndRemovesTheEnvBlockItAdded()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        f.Setup.PlanProviderTitles("claude").Apply(workspace.Paths);
        Assert.Equal("1", JsonNode.Parse(File.ReadAllText(f.ClaudeSettings))!["env"]!["CLAUDE_CODE_DISABLE_TERMINAL_TITLE"]!.GetValue<string>());
        f.Setup.PlanProviderTitles("claude", remove: true).Apply(workspace.Paths);
        Assert.Null(JsonNode.Parse(File.ReadAllText(f.ClaudeSettings))!["env"]);
    }

    [Fact]
    public void ClaudeRemovalRestoresAPreviousValueAndKeepsAUserEdit()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        Write(f.ClaudeSettings, "{\"env\":{\"CLAUDE_CODE_DISABLE_TERMINAL_TITLE\":\"0\"}}");
        f.Setup.PlanProviderTitles("claude").Apply(workspace.Paths);
        f.Setup.PlanProviderTitles("claude", remove: true).Apply(workspace.Paths);
        Assert.Equal("0", JsonNode.Parse(File.ReadAllText(f.ClaudeSettings))!["env"]!["CLAUDE_CODE_DISABLE_TERMINAL_TITLE"]!.GetValue<string>());

        f.Setup.PlanProviderTitles("claude").Apply(workspace.Paths);
        var edited = File.ReadAllText(f.ClaudeSettings).Replace("\"1\"", "\"2\"", StringComparison.Ordinal);
        File.WriteAllText(f.ClaudeSettings, edited);
        var removal = f.Setup.PlanProviderTitles("claude", remove: true);
        Assert.Contains(removal.Notes, note => note.StartsWith("Kept user-edited Claude title setting", StringComparison.Ordinal));
        removal.Apply(workspace.Paths);
        Assert.Equal(edited, File.ReadAllText(f.ClaudeSettings));
    }

    [Fact]
    public void ASettingTheUserAlreadyMadeIsLeftUnownedAndNeverRemoved()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        var text = "{\"env\":{\"CLAUDE_CODE_DISABLE_TERMINAL_TITLE\":\"1\"}}";
        Write(f.ClaudeSettings, text);
        var plan = f.Setup.PlanProviderTitles("claude");
        Assert.Contains(plan.Notes, note => note.Contains("left unowned"));
        plan.Apply(workspace.Paths);
        Assert.Equal(text, File.ReadAllText(f.ClaudeSettings));
        // Whoever set it, the setting counts: sessions started after it get a title, and removal never touches the user's value.
        Assert.True(Titled(workspace, "claude"));
        Assert.False(ProviderTitleFixture.DisabledFor(workspace.Paths, "claude", TimeSpan.FromDays(3650)));
        f.Setup.PlanProviderTitles("claude", remove: true).Apply(workspace.Paths);
        Assert.Equal(text, File.ReadAllText(f.ClaudeSettings));
    }

    // ---- Codex ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void CodexPreviewApplyRepeatAndRemoveKeepUnrelatedSettingsAndTheOtherProvider()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        var original = "model = \"y\"\n\n[tui]\nstatus_line = [\"thread-name\"]\n\n[other]\nvalue = 'user'\n";
        Write(f.CodexConfig, original);
        var claude = "{\"model\":\"x\"}";
        Write(f.ClaudeSettings, claude);

        var preview = f.Setup.PlanProviderTitles("codex");
        Assert.Equal(original, File.ReadAllText(f.CodexConfig));
        Assert.Contains(preview.Notes, note => note.Contains("terminal_title = []"));
        preview.Apply(workspace.Paths);
        var applied = File.ReadAllText(f.CodexConfig);
        Assert.Contains("terminal_title = []", applied);
        Assert.Contains("status_line = [\"thread-name\"]", applied);
        Assert.Contains("value = 'user'", applied);
        Assert.Equal(claude, File.ReadAllText(f.ClaudeSettings));
        Assert.True(Titled(workspace, "codex"));
        Assert.False(Titled(workspace, "claude"));

        var repeat = f.Setup.PlanProviderTitles("codex");
        Assert.DoesNotContain(repeat.Changes, change => change.Path == f.CodexConfig);
        repeat.Apply(workspace.Paths);
        Assert.Equal(applied, File.ReadAllText(f.CodexConfig));

        f.Setup.PlanProviderTitles("codex", remove: true).Apply(workspace.Paths);
        Assert.Equal(original, File.ReadAllText(f.CodexConfig));
        Assert.False(Titled(workspace, "codex"));
    }

    [Fact]
    public void CodexSetupCreatesTheTuiTableAndRemovalLeavesTheFileAsItWas()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        Write(f.CodexConfig, "model = \"y\"\n");
        f.Setup.PlanProviderTitles("codex").Apply(workspace.Paths);
        Assert.Contains("[tui]", File.ReadAllText(f.CodexConfig));
        f.Setup.PlanProviderTitles("codex", remove: true).Apply(workspace.Paths);
        Assert.Equal("model = \"y\"\n", File.ReadAllText(f.CodexConfig));
    }

    [Fact]
    public void CodexRemovalRestoresAPreviousListAndKeepsAUserEdit()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        var original = "[tui]\nterminal_title = [\"activity\", \"project-name\"]\n";
        Write(f.CodexConfig, original);
        f.Setup.PlanProviderTitles("codex").Apply(workspace.Paths);
        Assert.Contains("terminal_title = []", File.ReadAllText(f.CodexConfig));
        f.Setup.PlanProviderTitles("codex", remove: true).Apply(workspace.Paths);
        Assert.Equal(original, File.ReadAllText(f.CodexConfig));

        f.Setup.PlanProviderTitles("codex").Apply(workspace.Paths);
        var edited = File.ReadAllText(f.CodexConfig).Replace("[]", "[\"activity\"]", StringComparison.Ordinal);
        File.WriteAllText(f.CodexConfig, edited);
        var removal = f.Setup.PlanProviderTitles("codex", remove: true);
        Assert.Contains(removal.Notes, note => note.StartsWith("Kept user-edited Codex title setting", StringComparison.Ordinal));
        removal.Apply(workspace.Paths);
        Assert.Equal(edited, File.ReadAllText(f.CodexConfig));
    }

    [Fact]
    public void InvalidProviderSettingsFailBeforeAnyWrite()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        Write(f.CodexConfig, "[tui]\nterminal_title = [");
        Assert.Throws<InvalidDataException>(() => f.Setup.PlanProviderTitles("codex"));
        Write(f.ClaudeSettings, "{\"env\": 3}");
        Assert.Throws<InvalidDataException>(() => f.Setup.PlanProviderTitles("claude"));
    }

    [Fact]
    public void BothProvidersAreOneStepForEachAndAUnverifiedProviderWouldNotBeOffered()
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        var plan = f.Setup.PlanProviderTitles("both");
        Assert.Contains(plan.Changes, change => change.Path == f.ClaudeSettings);
        Assert.Contains(plan.Changes, change => change.Path == f.CodexConfig);
        Assert.Contains("claude", ProviderTitleSettings.Verified);
        Assert.Contains("codex", ProviderTitleSettings.Verified);
        Assert.Throws<ArgumentException>(() => f.Setup.PlanProviderTitles("neither"));
    }

    // ---- Bell Tower writes a title only while the step is applied for that provider ----------------------------------------

    private static ProcessIdentity Self => ProcessProbe.Identify(Environment.ProcessId)!;

    private static Attachment Adopt(TestWorkspace workspace, string provider, string mode, string id)
    {
        var captured = new Attachment(id, provider, Self,
            new("windows-terminal", Self, Self, 1234, "terminal-session") { ShellSource = "ancestry" }, workspace.Root, DateTimeOffset.UtcNow)
        { Ownership = "observed" };
        var adopted = NativeAdoption.Apply(captured, "session-" + id, mode, AgentModeClassifier.MayShareBackend(provider)).Attachment;
        new AttachmentStore(workspace.Paths).Save(adopted);
        return adopted;
    }

    public static IEnumerable<object[]> DirectClients() =>
    [
        ["claude", AgentModes.InteractiveClient],
        ["codex", AgentModes.EmbeddedRuntime]
    ];

    [Theory]
    [MemberData(nameof(DirectClients))]
    public void WithoutTheStepASessionKeepsColorOnlyAndWithItTheTitleToo(string provider, string mode)
    {
        var adopted = NativeAdoption.Apply(new Attachment("a", provider, Self, new("windows-terminal", Self, Self, 1234) { ShellSource = "ancestry" },
            "D:/x", DateTimeOffset.UtcNow)
        { Ownership = "observed" }, "s", mode, AgentModeClassifier.MayShareBackend(provider)).Attachment;
        var colorOnly = NativePresentation.Effective(adopted, true, providerTitleDisabled: false);
        Assert.False(colorOnly.ManagedTitle);
        Assert.True(colorOnly.ManagedBackground);
        var both = NativePresentation.Effective(adopted, true, providerTitleDisabled: true);
        Assert.True(both.ManagedTitle);
        Assert.True(both.ManagedBackground);
        Assert.False(NativePresentation.Effective(adopted, false, providerTitleDisabled: true).ManagedBackground);
    }

    private sealed class Console : IConsoleTitleWriter
    {
        public List<(string Title, string? Background)> Writes { get; } = [];
        public int TitleRestores;
        public int ColorResets;
        public bool ResetColors(ProcessIdentity shell) { ColorResets++; return true; }
        public bool Write(ProcessIdentity shell, ProcessIdentity agent, string title, string? background = null) { Writes.Add((title, background)); return true; }
        public string? ReadTitle(ProcessIdentity shell, ProcessIdentity agent) => "shell title";
        public bool RestoreTitle(ProcessIdentity shell, string expected, string previous) { TitleRestores++; return true; }
    }

    [Theory]
    [MemberData(nameof(DirectClients))]
    public void ResolverAndWriterGiveColorOnlyWithoutTheStepAndTitleWithIt(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = true);
        var attachment = Adopt(workspace, provider, mode, "a");
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
        {
            Provider = provider,
            SessionId = "session-a",
            AttachmentId = attachment.Id,
            Directory = workspace.Root
        });
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-a"];
        var resolver = new TerminalPresentationResolver(workspace.Paths);

        var without = resolver.Resolve(session, new())!;
        Assert.Equal("", without.Title);
        Assert.NotNull(without.Background);
        var console = new Console();
        Assert.True(new TerminalTitleService(workspace.Paths, console).Apply(without));
        Assert.Equal([("", without.Background)], console.Writes);
        // Giving the presentation back resets the colors and never restores a title it did not write.
        Assert.True(new TerminalTitleService(workspace.Paths, console).Restore(attachment.Id));
        Assert.Equal(1, console.ColorResets);
        Assert.Equal(0, console.TitleRestores);

        ProviderTitleFixture.Apply(workspace.Paths, provider);
        var with = resolver.Resolve(session, new())!;
        Assert.NotEqual("", with.Title);
        Assert.True(new TerminalTitleService(workspace.Paths, console).Apply(with));
        Assert.Equal(with.Title, console.Writes[^1].Title);
        // A color-only request is refused once the title is owned by the provider again is not possible here; a titled one is always accepted.
    }

    [Fact]
    public void ATitleRequestForAProviderWhoseTitleIsNotDisabledIsRefusedByTheWriter()
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = true);
        var attachment = Adopt(workspace, "codex", AgentModes.EmbeddedRuntime, "a");
        var console = new Console();
        var request = new DesiredTerminalPresentation(attachment.Id, "codex", attachment.RegisteredAt, attachment.Process!, attachment.Host.Shell!, "Bell Tower title", "#101820");
        Assert.False(new TerminalTitleService(workspace.Paths, console).Apply(request));
        Assert.Empty(console.Writes);
    }

    [Fact]
    public void MixedSessionsOneProviderWithTheStepAndOneWithoutEachFollowTheirOwnState()
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = true);
        ProviderTitleFixture.Apply(workspace.Paths, "claude");
        var claude = Adopt(workspace, "claude", AgentModes.InteractiveClient, "claude-a");
        var codex = Adopt(workspace, "codex", AgentModes.EmbeddedRuntime, "codex-a");
        Assert.True(NativePresentation.Effective(claude, true, Titled(workspace, "claude")).ManagedTitle);
        Assert.False(NativePresentation.Effective(codex, true, Titled(workspace, "codex")).ManagedTitle);
    }

    private sealed class NoTabs : ITerminalTabSource
    {
        public IReadOnlyList<ITerminalTab> ReadTabs(long window) => [];
    }

    private sealed class Windows : IWindowActivationApi
    {
        public bool IsMinimized(IntPtr window) => false;
        public void Restore(IntPtr window) { }
        public bool SetForeground(IntPtr window) => true;
        public bool IsForeground(IntPtr window) => true;
    }

    private sealed class ConsoleSession : ISessionConsole
    {
        public string ReadTitle() => "shell";
        public bool SetTitle(string title) => true;
        public bool Write(string text) => true;
        public void Dispose() { }
    }

    [Theory]
    [MemberData(nameof(DirectClients))]
    public void WindowOnlyNavigationWithoutTheStepStatesTheProviderOwnsTheTitle(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = true);
        Adopt(workspace, provider, mode, "a");
        var focus = new WindowsTerminalFocusService(workspace.Paths, new(new NoTabs()), new(new Windows()), (_, _) => new ConsoleSession(), _ => true);
        var result = focus.Focus("a");
        Assert.True(result.Success);
        Assert.Equal("window only", result.Precision);
        Assert.Equal(NavigationReasons.WindowOnlyProviderTitle, result.Message);
    }

    // ---- DEV-060: ANALYSIS-4703 removal keeps ownership until it succeeded, ANALYSIS-4704, ANALYSIS-4705 -----------------------

    private static IntegrationOwnership Manifest(TestWorkspace workspace) =>
        AtomicFile.Read<IntegrationOwnership>(Path.Combine(workspace.Paths.Root, "integration.json")) ?? new([], []);

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AFailedSettingsWriteDuringRemovalKeepsOwnershipAndARetryRemovesTheSetting(string provider)
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        var path = provider == "claude" ? f.ClaudeSettings : f.CodexConfig;
        f.Setup.PlanProviderTitles(provider).Apply(workspace.Paths);
        Assert.Single(Manifest(workspace).ProviderTitles);
        var applied = File.ReadAllText(path);

        // The provider (or anything else) holds the file read-only, so restoring it fails after the plan began.
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert.ThrowsAny<Exception>(() => f.Setup.PlanProviderTitles(provider, remove: true).Apply(workspace.Paths));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
        // Ownership is still recorded, so the retry knows the setting is Bell Tower's and removes it.
        Assert.Single(Manifest(workspace).ProviderTitles);
        Assert.Equal(applied, File.ReadAllText(path));
        f.Setup.PlanProviderTitles(provider, remove: true).Apply(workspace.Paths);
        Assert.Empty(Manifest(workspace).ProviderTitles);
        Assert.DoesNotContain("terminal_title", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.DoesNotContain("CLAUDE_CODE_DISABLE_TERMINAL_TITLE", File.ReadAllText(path), StringComparison.Ordinal);
    }

    private static SetupAction Command(TestWorkspace workspace, int exitCode) =>
        new("test removal", "cmd.exe", ["/c", "exit", exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)])
        {
            Failure = "The on-demand tray start task could not be removed.",
            NodeEnvironment = false,
            OnSuccess = () => BellTower.Protocol.HookStartTask.ClearRecord(workspace.Paths.Root)
        };

    [Fact]
    public void AFailedTaskRemovalKeepsTheRecordAndAnyRetrySucceeds()
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");
        var task = BellTower.Protocol.HookStartTask.TaskPath(workspace.Paths.Root, "BellTowerTest-DEV060");
        BellTower.Protocol.HookStartTask.Record(workspace.Paths.Root, task);
        var failing = new IntegrationSetup(workspace.Paths, "C:/Bell Tower/bell-tower.exe", home) { RemoveTaskAction = _ => Command(workspace, 3) };
        var plan = failing.Plan("both", remove: true);
        Assert.Contains(plan.Actions, action => action.Description == "test removal");
        var error = Assert.Throws<IOException>(() => plan.Apply(workspace.Paths));
        Assert.Contains("could not be removed", error.Message);
        Assert.Equal(task, BellTower.Protocol.HookStartTask.Recorded(workspace.Paths.Root));

        var working = new IntegrationSetup(workspace.Paths, "C:/Bell Tower/bell-tower.exe", home) { RemoveTaskAction = _ => Command(workspace, 0) };
        working.Plan("both", remove: true).Apply(workspace.Paths);
        Assert.Equal("none", BellTower.Protocol.HookStartTask.RecordState(workspace.Paths.Root));
    }

    [Fact]
    public void ARemovalWithoutATaskOrWithAnInvalidRecordPlansNoTaskAction()
    {
        using var workspace = new TestWorkspace();
        var setup = new IntegrationSetup(workspace.Paths, "C:/Bell Tower/bell-tower.exe", Path.Combine(workspace.Root, "home"));
        Assert.DoesNotContain(setup.Plan("both", remove: true).Actions, action => action.Description.Contains("tray start task", StringComparison.Ordinal));
        Directory.CreateDirectory(BellTower.Protocol.HookTrayStart.StateDirectory(workspace.Paths.Root));
        File.WriteAllText(BellTower.Protocol.HookStartTask.RecordPath(workspace.Paths.Root), "\\Microsoft\\Windows\\Defrag\\ScheduledDefrag");
        var plan = setup.Plan("both", remove: true);
        Assert.DoesNotContain(plan.Actions, action => action.Description.Contains("tray start task", StringComparison.Ordinal));
        Assert.Contains(plan.Notes, note => note.Contains("not a Bell Tower task path", StringComparison.Ordinal));
        plan.Apply(workspace.Paths);
        Assert.Equal("none", BellTower.Protocol.HookStartTask.RecordState(workspace.Paths.Root));
    }

    [Fact]
    public void RemovingATaskThatIsNotThereCountsAsRemoved()
    {
        using var workspace = new TestWorkspace();
        Assert.True(HookStartTaskSetup.Unregister(workspace.Paths.Root, "BellTowerTest-DEV060"));
    }

    // 4704 in the setup flow: a setting applied by the step takes effect for sessions that start after it.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void TheStepAppliedWhileASessionRunsTitlesOnlyLaterSessions(string provider)
    {
        using var workspace = new TestWorkspace();
        var f = Create(workspace);
        var runningSince = DateTime.UtcNow.AddMinutes(-10).Ticks;
        f.Setup.PlanProviderTitles(provider).Apply(workspace.Paths);
        var state = new ProviderTitleState(workspace.Paths);
        Assert.False(state.DisabledFor(provider, runningSince));
        Assert.True(state.DisabledFor(provider, DateTime.UtcNow.AddMinutes(1).Ticks));
        // Removing it by the same step ends the title for every session.
        f.Setup.PlanProviderTitles(provider, remove: true).Apply(workspace.Paths);
        Assert.False(state.DisabledFor(provider, DateTime.UtcNow.AddMinutes(1).Ticks));
    }

    // 4705: a color-only restore record must not later restore a blank title.
    private sealed class RestoringConsole : IConsoleTitleWriter
    {
        public List<(string Title, string? Background)> Writes { get; } = [];
        public List<(string Expected, string Previous)> Restores { get; } = [];
        public int ColorResets;
        public bool ResetColors(ProcessIdentity shell) { ColorResets++; return true; }
        public bool Write(ProcessIdentity shell, ProcessIdentity agent, string title, string? background = null) { Writes.Add((title, background)); return true; }
        public string? ReadTitle(ProcessIdentity shell, ProcessIdentity agent) => "the shell's own title";
        public bool RestoreTitle(ProcessIdentity shell, string expected, string previous) { Restores.Add((expected, previous)); return true; }
    }

    [Theory]
    [MemberData(nameof(DirectClients))]
    public void AColorOnlyWriteFollowedByATitledOneStillGivesBackTheRealTitle(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = true);
        var attachment = Adopt(workspace, provider, mode, "a");
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
        {
            Provider = provider,
            SessionId = "session-a",
            AttachmentId = attachment.Id,
            Directory = workspace.Root
        });
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-a"];
        var resolver = new TerminalPresentationResolver(workspace.Paths);
        var console = new RestoringConsole();
        var service = new TerminalTitleService(workspace.Paths, console);

        // The provider's title is still in effect (the setting was applied after this session started): color only.
        ProviderTitleFixture.Apply(workspace.Paths, TimeSpan.Zero, provider);
        var colorOnly = resolver.Resolve(session, new())!;
        Assert.Equal("", colorOnly.Title);
        Assert.True(service.Apply(colorOnly));

        // The same shell later takes a titled write (a session that started after the setting took effect): the first sighting is stored in
        // the state record, so forgetting it stands for the setting having been there before this session started.
        File.Delete(Path.Combine(workspace.Paths.Root, "provider-titles.json"));
        ProviderTitleFixture.Apply(workspace.Paths, TimeSpan.FromDays(365), provider);
        var titled = resolver.Resolve(session, new())!;
        Assert.NotEqual("", titled.Title);
        Assert.True(service.Apply(titled));
        Assert.True(service.Restore(attachment.Id));
        Assert.Equal([(titled.Title, "the shell's own title")], console.Restores);
        Assert.Equal(1, console.ColorResets);
    }

    // ---- DEV-061: ANALYSIS-4801 -----------------------------------------------------------------------------------------------

    // Runs the real unregister script with Get-ScheduledTask replaced by a function that fails the way the test says.
    private static (int ExitCode, string Output) RunUnregister(TestWorkspace workspace, string lookup)
    {
        var task = BellTower.Protocol.HookStartTask.TaskPath(workspace.Paths.Root, "BellTowerTest-DEV061");
        var script = lookup + "\n" + HookStartTaskSetup.UnregisterScript(task);
        var file = Path.Combine(workspace.Root, "unregister-" + Guid.NewGuid().ToString("N") + ".ps1");
        File.WriteAllText(file, script);
        var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", file })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(60000));
        return (process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    [Fact]
    public void OnlyAGenuineNotFoundCountsAsRemovedAndAnyOtherLookupFailureExitsNonZero()
    {
        using var workspace = new TestWorkspace();
        // A missing task: the lookup's own not-found error.
        var notFound = "function Get-ScheduledTask { [CmdletBinding()] param($TaskName, $TaskPath) if (-not $TaskName) { return } $PSCmdlet.ThrowTerminatingError([System.Management.Automation.ErrorRecord]::new([Exception]::new('none'), 'NotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $TaskName)) }";
        Assert.Equal(0, RunUnregister(workspace, notFound).ExitCode);

        // Access denied, a service that is not running, a broken scheduler: not a missing task, so the script must not claim it is gone.
        var denied = "function Get-ScheduledTask { [CmdletBinding()] param($TaskName, $TaskPath) if (-not $TaskName) { return } $PSCmdlet.ThrowTerminatingError([System.Management.Automation.ErrorRecord]::new([UnauthorizedAccessException]::new('denied'), 'Denied', [System.Management.Automation.ErrorCategory]::PermissionDenied, $TaskName)) }";
        var failed = RunUnregister(workspace, denied);
        Assert.NotEqual(0, failed.ExitCode);
        var broken = "function Get-ScheduledTask { [CmdletBinding()] param($TaskName, $TaskPath) if (-not $TaskName) { return } throw 'the scheduler is not available' }";
        Assert.NotEqual(0, RunUnregister(workspace, broken).ExitCode);
    }

    [Fact]
    public void ALookupFailureOtherThanNotFoundKeepsTheRecordAndARetryAfterItClears()
    {
        using var workspace = new TestWorkspace();
        var task = BellTower.Protocol.HookStartTask.TaskPath(workspace.Paths.Root, "BellTowerTest-DEV061");
        BellTower.Protocol.HookStartTask.Record(workspace.Paths.Root, task);
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        SetupAction Action(string lookup)
        {
            var script = lookup + "\n" + HookStartTaskSetup.UnregisterScript(task);
            return new("test removal", powershell, ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script))])
            {
                Failure = "The on-demand tray start task could not be removed.",
                NodeEnvironment = false,
                OnSuccess = () => BellTower.Protocol.HookStartTask.ClearRecord(workspace.Paths.Root)
            };
        }
        var setup = new IntegrationSetup(workspace.Paths, "C:/Bell Tower/bell-tower.exe", Path.Combine(workspace.Root, "home"))
        {
            RemoveTaskAction = _ => Action("function Get-ScheduledTask { [CmdletBinding()] param($TaskName, $TaskPath) if (-not $TaskName) { return } throw 'the scheduler is not available' }")
        };
        Assert.Throws<IOException>(() => setup.Plan("both", remove: true).Apply(workspace.Paths));
        Assert.Equal(task, BellTower.Protocol.HookStartTask.Recorded(workspace.Paths.Root));

        var retry = new IntegrationSetup(workspace.Paths, "C:/Bell Tower/bell-tower.exe", Path.Combine(workspace.Root, "home"))
        {
            RemoveTaskAction = _ => Action("function Get-ScheduledTask { [CmdletBinding()] param($TaskName, $TaskPath) if (-not $TaskName) { return } $PSCmdlet.ThrowTerminatingError([System.Management.Automation.ErrorRecord]::new([Exception]::new('none'), 'NotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $TaskName)) }")
        };
        retry.Plan("both", remove: true).Apply(workspace.Paths);
        Assert.Equal("none", BellTower.Protocol.HookStartTask.RecordState(workspace.Paths.Root));
    }
}
