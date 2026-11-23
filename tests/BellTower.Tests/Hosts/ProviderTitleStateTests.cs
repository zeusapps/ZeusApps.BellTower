using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

/// <summary>
/// ANALYSIS-4704 and QA-5400: a title is written only when the provider's own title is in effect disabled for the session: the disabling
/// setting is in the provider's actual settings file (whoever set it) and the session started after it took effect.
/// </summary>
public sealed class ProviderTitleStateTests
{
    private static ProcessIdentity Self => ProcessProbe.Identify(Environment.ProcessId)!;

    public static IEnumerable<object[]> Providers() => [["claude"], ["codex"]];

    public static IEnumerable<object[]> DirectClients() =>
    [
        ["claude", AgentModes.InteractiveClient],
        ["codex", AgentModes.EmbeddedRuntime]
    ];

    private static void Settings(string provider, string text, DateTime written)
    {
        var file = ProviderTitleState.SettingsFile(provider);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
        File.SetLastWriteTimeUtc(file, written);
    }

    private static string Enabled(string provider) => provider == "claude" ? "{\"env\":{\"CLAUDE_CODE_DISABLE_TERMINAL_TITLE\":\"1\"}}" : "[tui]\nterminal_title = []\n";
    private static string Absent(string provider) => provider == "claude" ? "{\"env\":{\"OTHER\":\"1\"}}" : "[tui]\nstatus_line = [\"thread-name\"]\n";

    [Theory]
    [MemberData(nameof(Providers))]
    public void TheSettingAppliedWhileASessionRunsTitlesOnlyLaterSessions(string provider)
    {
        using var workspace = new TestWorkspace();
        ProviderTitleFixture.Use(name => Path.Combine(ProviderTitleFixture.Home(workspace.Paths), name));
        var applied = DateTime.UtcNow;
        Settings(provider, Enabled(provider), applied);
        var state = new ProviderTitleState(workspace.Paths);
        Assert.False(state.DisabledFor(provider, applied.AddMinutes(-10).Ticks));
        Assert.True(state.DisabledFor(provider, applied.AddSeconds(1).Ticks));
        Assert.False(state.DisabledFor(provider, null));
        var report = state.Report(provider);
        Assert.True(report.Present);
        Assert.Equal(applied, report.Since!.Value.UtcDateTime);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void AUserResetEndsTheTitleAndALaterReapplyStartsANewEffectiveTime(string provider)
    {
        using var workspace = new TestWorkspace();
        ProviderTitleFixture.Use(name => Path.Combine(ProviderTitleFixture.Home(workspace.Paths), name));
        var first = DateTime.UtcNow.AddDays(-2);
        Settings(provider, Enabled(provider), first);
        var state = new ProviderTitleState(workspace.Paths);
        Assert.True(state.DisabledFor(provider, first.AddHours(1).Ticks));
        // An unrelated edit of the file keeps the original effective time.
        Settings(provider, Enabled(provider) + (provider == "claude" ? "" : "[other]\nx = 1\n"), DateTime.UtcNow.AddDays(-1));
        Assert.True(state.DisabledFor(provider, first.AddHours(1).Ticks));

        // The user sets it back: no session is titled any more and the report says provider-owned.
        var reset = DateTime.UtcNow.AddHours(-5);
        Settings(provider, Absent(provider), reset);
        Assert.False(state.DisabledFor(provider, DateTime.UtcNow.Ticks));
        Assert.False(state.Report(provider).Present);

        // Applying it again takes effect from that write, not from before.
        var again = DateTime.UtcNow.AddHours(-1);
        Settings(provider, Enabled(provider), again);
        Assert.False(state.DisabledFor(provider, again.AddMinutes(-30).Ticks));
        Assert.True(state.DisabledFor(provider, again.AddMinutes(1).Ticks));
        // A missing file is simply not set.
        File.Delete(ProviderTitleState.SettingsFile(provider));
        Assert.False(state.DisabledFor(provider, DateTime.UtcNow.Ticks));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ASettingTheUserMadeByHandCountsWithoutAnyOwnership(string provider)
    {
        using var workspace = new TestWorkspace();
        ProviderTitleFixture.Use(name => Path.Combine(ProviderTitleFixture.Home(workspace.Paths), name));
        Settings(provider, Enabled(provider), DateTime.UtcNow.AddDays(-30));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "integration.json")));
        Assert.True(new ProviderTitleState(workspace.Paths).DisabledFor(provider, DateTime.UtcNow.Ticks));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void AnOwnershipEntryForAnotherHomeNeverCounts(string provider)
    {
        using var workspace = new TestWorkspace();
        ProviderTitleFixture.Use(name => Path.Combine(ProviderTitleFixture.Home(workspace.Paths), name));
        // The manifest says the step was applied, but to a different profile; the profile in use holds no setting.
        var ownership = new IntegrationOwnership([], []);
        ownership.ProviderTitles[Path.Combine(workspace.Root, "another-home", ".config")] = new(provider, null, true);
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "integration.json"), Json.Encode(ownership));
        Settings(provider, Absent(provider), DateTime.UtcNow.AddDays(-30));
        Assert.False(new ProviderTitleState(workspace.Paths).DisabledFor(provider, DateTime.UtcNow.Ticks));
    }

    [Fact]
    public void MixedProvidersEachFollowTheirOwnFile()
    {
        using var workspace = new TestWorkspace();
        ProviderTitleFixture.Use(name => Path.Combine(ProviderTitleFixture.Home(workspace.Paths), name));
        Settings("claude", Enabled("claude"), DateTime.UtcNow.AddDays(-1));
        Settings("codex", Absent("codex"), DateTime.UtcNow.AddDays(-1));
        var state = new ProviderTitleState(workspace.Paths);
        Assert.True(state.DisabledFor("claude", DateTime.UtcNow.Ticks));
        Assert.False(state.DisabledFor("codex", DateTime.UtcNow.Ticks));
    }

    // ---- The same per-session answer drives presentation, navigation and doctor ----------------------------------------------

    private static Attachment Adopt(TestWorkspace workspace, string provider, string mode)
    {
        var captured = new Attachment("a", provider, Self, new("windows-terminal", Self, Self, 1234, "terminal-session") { ShellSource = "ancestry" },
            workspace.Root, DateTimeOffset.UtcNow)
        { Ownership = "observed" };
        var adopted = NativeAdoption.Apply(captured, "session-a", mode, AgentModeClassifier.MayShareBackend(provider)).Attachment;
        new AttachmentStore(workspace.Paths).Save(adopted);
        return adopted;
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
    public void ASessionThatStartedBeforeTheSettingKeepsWindowOnlyNavigationAndColorOnlyAndALaterOneIsTitled(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        ProviderTitleFixture.Use(name => Path.Combine(ProviderTitleFixture.Home(workspace.Paths), name));
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = true);
        var attachment = Adopt(workspace, provider, mode);
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
        {
            Provider = provider,
            SessionId = "session-a",
            AttachmentId = attachment.Id,
            Directory = workspace.Root
        });
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-a"];
        FocusResult Click() => new WindowsTerminalFocusService(workspace.Paths, new(new NoTabs()), new(new Windows()), (_, _) => new ConsoleSession(), _ => true).Focus("a");

        // Applied now, after this session's process started: the provider still writes its own title in it.
        Settings(provider, Enabled(provider), DateTime.UtcNow);
        var running = new TerminalPresentationResolver(workspace.Paths).Resolve(session, new())!;
        Assert.Equal("", running.Title);
        Assert.NotNull(running.Background);
        var click = Click();
        Assert.Equal("window only", click.Precision);
        Assert.Equal(NavigationReasons.WindowOnlyProviderTitle, click.Message);

        // The same setting written before this session started: it is titled and the click looks for its tab.
        Settings(provider, Enabled(provider), DateTime.UtcNow.AddDays(-365));
        File.Delete(Path.Combine(workspace.Paths.Root, "provider-titles.json"));
        var later = new TerminalPresentationResolver(workspace.Paths).Resolve(session, new())!;
        Assert.NotEqual("", later.Title);
        Assert.NotEqual(NavigationReasons.WindowOnlyProviderTitle, Click().Message);

        // The user resets it: provider-owned again, for navigation and for presentation.
        Settings(provider, Absent(provider), DateTime.UtcNow);
        Assert.Equal("", new TerminalPresentationResolver(workspace.Paths).Resolve(session, new())!.Title);
        Assert.Equal(NavigationReasons.WindowOnlyProviderTitle, Click().Message);
    }

    [Fact]
    public void DoctorReportsTheSettingStatePerProviderFromTheActualFiles()
    {
        using var workspace = new TestWorkspace();
        ProviderTitleFixture.Use(name => Path.Combine(ProviderTitleFixture.Home(workspace.Paths), name));
        Settings("claude", Enabled("claude"), DateTime.UtcNow.AddDays(-1));
        Settings("codex", Absent("codex"), DateTime.UtcNow.AddDays(-1));
        var text = Json.Encode(new BellTower.Diagnostics.DiagnosticsReporter(workspace.Paths).Report());
        Assert.Contains("Bell Tower title for sessions started after", text, StringComparison.Ordinal);
        Assert.Contains("provider-owned", text, StringComparison.Ordinal);
    }

    // ---- DEV-061: ANALYSIS-4800 -----------------------------------------------------------------------------------------------

    private static string RecordText(TestWorkspace workspace)
    {
        var path = Path.Combine(workspace.Paths.Root, "provider-titles.json");
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static void Home(TestWorkspace workspace, string name) =>
        ProviderTitleFixture.Use(provider => Path.Combine(ProviderTitleFixture.Home(workspace.Paths), name, provider));

    [Theory]
    [MemberData(nameof(Providers))]
    public void DoctorResolvingAnotherHomeLeavesTheTraysAnswerAndTheRecordUnchanged(string provider)
    {
        using var workspace = new TestWorkspace();
        var since = DateTime.UtcNow.AddDays(-2);
        Home(workspace, "tray");
        Settings(provider, Enabled(provider), since);
        var tray = new ProviderTitleState(workspace.Paths);
        Assert.True(tray.DisabledFor(provider, since.AddHours(1).Ticks));
        var before = RecordText(workspace);
        Assert.NotEqual("", before);

        // Doctor runs in a terminal whose provider home differs: first with the setting absent there, then present with a later time.
        Home(workspace, "doctor");
        Settings(provider, Absent(provider), DateTime.UtcNow.AddHours(-1));
        Assert.False(tray.Report(provider).Present);
        Assert.Equal(before, RecordText(workspace));
        Settings(provider, Enabled(provider), DateTime.UtcNow.AddMinutes(-5));
        Assert.True(tray.Report(provider).Present);
        Assert.Equal(before, RecordText(workspace));

        // Back in the tray's home nothing changed: same answer, same record, and sessions that started before the setting stay untitled.
        Home(workspace, "tray");
        Assert.True(tray.DisabledFor(provider, since.AddHours(1).Ticks));
        Assert.False(tray.DisabledFor(provider, since.AddHours(-1).Ticks));
        Assert.Equal(before, RecordText(workspace));
    }

    [Fact]
    public void ReportNeverCreatesARecordAndAMixedPairKeepsEachProvidersOwnRecord()
    {
        using var workspace = new TestWorkspace();
        Home(workspace, "tray");
        Settings("claude", Enabled("claude"), DateTime.UtcNow.AddDays(-2));
        Settings("codex", Enabled("codex"), DateTime.UtcNow.AddDays(-1));
        var state = new ProviderTitleState(workspace.Paths);
        Assert.True(state.Report("claude").Present);
        Assert.True(state.Report("codex").Present);
        Assert.Equal("", RecordText(workspace));

        Assert.True(state.DisabledFor("claude", DateTime.UtcNow.AddDays(-1).AddHours(-1).Ticks));
        Assert.False(state.DisabledFor("codex", DateTime.UtcNow.AddDays(-1).AddHours(-1).Ticks));
        var both = RecordText(workspace);
        Assert.Contains("CLAUDE", both, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CODEX", both, StringComparison.OrdinalIgnoreCase);
        Home(workspace, "doctor");
        Settings("claude", Absent("claude"), DateTime.UtcNow);
        _ = state.Report("claude");
        _ = state.Report("codex");
        Assert.Equal(both, RecordText(workspace));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ARemovalAndReAddSeenByTheTickGivesNoTitleToASessionStartedInTheGap(string provider)
    {
        using var workspace = new TestWorkspace();
        Home(workspace, "tray");
        var state = new ProviderTitleState(workspace.Paths);
        var first = DateTime.UtcNow.AddHours(-5);
        Settings(provider, Enabled(provider), first);
        Assert.True(state.DisabledFor(provider, first.AddHours(1).Ticks));

        // The setting is removed; the tray's tick looks at both files and sees it absent.
        Settings(provider, Absent(provider), DateTime.UtcNow.AddHours(-3));
        state.ObserveBoth();
        var gap = DateTime.UtcNow.AddHours(-2).Ticks;
        // It is added back later. The session that started in the gap read a configuration without it.
        var again = DateTime.UtcNow.AddHours(-1);
        Settings(provider, Enabled(provider), again);
        Assert.False(state.DisabledFor(provider, gap));
        Assert.True(state.DisabledFor(provider, again.AddMinutes(1).Ticks));
    }

    [Fact]
    public void ARemovalAndReAddNobodySawIsTheDocumentedLimitation()
    {
        using var workspace = new TestWorkspace();
        Home(workspace, "tray");
        var state = new ProviderTitleState(workspace.Paths);
        var first = DateTime.UtcNow.AddHours(-5);
        Settings("claude", Enabled("claude"), first);
        Assert.True(state.DisabledFor("claude", first.AddHours(1).Ticks));
        // Removed and re-added while nothing observed (the tray was stopped): the old effective time survives. docs/compatibility.md says so.
        Settings("claude", Absent("claude"), DateTime.UtcNow.AddHours(-3));
        Settings("claude", Enabled("claude"), DateTime.UtcNow.AddHours(-1));
        Assert.True(state.DisabledFor("claude", DateTime.UtcNow.AddHours(-2).Ticks));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void APartlyWrittenFileNeitherDropsTheRecordNorWithdrawsAGrantedTitle(string provider)
    {
        using var workspace = new TestWorkspace();
        Home(workspace, "tray");
        var since = DateTime.UtcNow.AddHours(-4);
        Settings(provider, Enabled(provider), since);
        var state = new ProviderTitleState(workspace.Paths);
        var started = since.AddHours(1).Ticks;
        Assert.True(state.DisabledFor(provider, started));
        var before = RecordText(workspace);

        // The provider is in the middle of rewriting its file: a truncated document that does not parse.
        Settings(provider, provider == "claude" ? "{\"env\":{\"CLAUDE_CODE_DISABLE_" : "[tui\nterminal_title = [", DateTime.UtcNow);
        Assert.True(state.DisabledFor(provider, started));
        state.ObserveBoth();
        Assert.True(state.DisabledFor(provider, started));
        Assert.Equal(before, RecordText(workspace));
        Assert.True(state.Report(provider).Present);

        // The finished write still holds the setting: the original time is kept. One that dropped it is seen as absent.
        Settings(provider, Enabled(provider), DateTime.UtcNow);
        Assert.True(state.DisabledFor(provider, started));
        Settings(provider, Absent(provider), DateTime.UtcNow);
        Assert.False(state.DisabledFor(provider, started));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ALockedSettingsFileIsUnknownNotAbsent(string provider)
    {
        using var workspace = new TestWorkspace();
        Home(workspace, "tray");
        var since = DateTime.UtcNow.AddHours(-4);
        Settings(provider, Enabled(provider), since);
        var state = new ProviderTitleState(workspace.Paths);
        var started = since.AddHours(1).Ticks;
        Assert.True(state.DisabledFor(provider, started));
        var before = RecordText(workspace);
        var file = ProviderTitleState.SettingsFile(provider);
        // Another writer holds the file exclusively and its write time moved, so the cached reading cannot answer.
        using (var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            held.WriteByte((byte)' ');
            held.Flush();
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
            Assert.True(state.DisabledFor(provider, started));
            state.ObserveBoth();
            Assert.True(state.DisabledFor(provider, started));
        }
        Assert.Equal(before, RecordText(workspace));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void AnOlderProviderKeyedRecordIsReadAsUnknownAndTheSettingIsSightedAgain(string provider)
    {
        using var workspace = new TestWorkspace();
        Home(workspace, "tray");
        Directory.CreateDirectory(workspace.Paths.Root);
        File.WriteAllText(Path.Combine(workspace.Paths.Root, "provider-titles.json"), "{\"" + provider + "\":\"2020-01-01T00:00:00+00:00\"}");
        var written = DateTime.UtcNow.AddHours(-1);
        Settings(provider, Enabled(provider), written);
        var state = new ProviderTitleState(workspace.Paths);
        // The 2020 time of the old shape is not trusted: the setting counts from the file's own write time.
        Assert.False(state.DisabledFor(provider, written.AddHours(-1).Ticks));
        Assert.True(state.DisabledFor(provider, written.AddMinutes(1).Ticks));
    }

    [Theory]
    [InlineData("claude", false)]
    [InlineData("codex", false)]
    [InlineData("claude", true)]
    [InlineData("codex", true)]
    public void AMalformedRecordFileIsEmptyAndTheNextSightingRewritesIt(string provider, bool oversize)
    {
        using var workspace = new TestWorkspace();
        Home(workspace, "tray");
        Directory.CreateDirectory(workspace.Paths.Root);
        var record = Path.Combine(workspace.Paths.Root, "provider-titles.json");
        if (oversize)
        {
            File.WriteAllText(record, new string(' ', 17 * 1024 * 1024));
        }
        else
        {
            File.WriteAllText(record, "null");
        }
        var written = DateTime.UtcNow.AddHours(-1);
        Settings(provider, Enabled(provider), written);
        var state = new ProviderTitleState(workspace.Paths);
        state.ObserveBoth();
        Assert.True(state.Report(provider).Present);
        Assert.False(state.DisabledFor(provider, written.AddHours(-1).Ticks));
        Assert.True(state.DisabledFor(provider, written.AddMinutes(1).Ticks));
        var rewritten = File.ReadAllText(record);
        Assert.Contains(provider, rewritten);
        Assert.True(rewritten.Length < 4096);
    }
}
