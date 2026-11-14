using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

/// <summary>Native-presentation setting (task 4.3): one decision, title and palette color, restored or reset, never for a shared backend.</summary>
public sealed class NativePresentationTests
{
    private sealed class Console : IConsoleTitleWriter
    {
        public Dictionary<int, string> Titles { get; } = [];
        public string Initial = "shell title";
        public List<(string Title, string? Background)> Writes { get; } = [];
        public List<(ProcessIdentity Shell, string Expected, string Previous)> Restores { get; } = [];
        public List<ProcessIdentity> ColorResets { get; } = [];
        public HashSet<int> FailRestoreShells { get; } = [];
        public bool ResetColors(ProcessIdentity shell) { ColorResets.Add(shell); return true; }
        public bool Write(ProcessIdentity shell, ProcessIdentity agent, string title, string? background = null)
        {
            Writes.Add((title, background));
            Titles[shell.Pid] = title;
            return true;
        }

        public string? ReadTitle(ProcessIdentity shell, ProcessIdentity agent) => Titles.TryGetValue(shell.Pid, out var title) ? title : Initial;

        public bool RestoreTitle(ProcessIdentity shell, string expected, string previous)
        {
            if (FailRestoreShells.Contains(shell.Pid))
            {
                return false;
            }
            Restores.Add((shell, expected, previous));
            if (ReadTitle(shell, shell) == expected)
            {
                Titles[shell.Pid] = previous;
            }
            return true;
        }
    }

    private sealed class RecordingPresentation : ITerminalPresentationWriter
    {
        public List<DesiredTerminalPresentation> Writes { get; } = [];
        public List<string> Restores { get; } = [];
        public Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation)
        {
            Writes.Add(desired);
            return Task.FromResult(true);
        }

        public Task<bool> RestoreAsync(string attachmentId, CancellationToken cancellation)
        {
            Restores.Add(attachmentId);
            return Task.FromResult(true);
        }
    }

    private static ProcessIdentity Self => ProcessProbe.Identify(Environment.ProcessId)!;

    private static void Setting(TestWorkspace workspace, bool enabled) => new ConfigStore(workspace.Paths).Update(c => c.NativePresentation = enabled);

    private static Attachment Captured(string provider, string id, ProcessIdentity agent, ProcessIdentity shell, string ownership = "observed") =>
        new(id, provider, agent, new("windows-terminal", shell, Self, 1234, "terminal-session") { ShellSource = "ancestry" }, "D:/example",
            DateTimeOffset.UtcNow)
        { Ownership = ownership };

    /// <summary>Adopts through the one ingress policy and stores the result, then registers a session on that attachment.</summary>
    private static SessionState Adopt(TestWorkspace workspace, string provider, string mode, ProcessIdentity? shell = null, string? sessionId = null,
        ProcessIdentity? agent = null)
    {
        sessionId ??= "session-1";
        var outcome = NativeAdoption.Apply(Captured(provider, provider + "-" + sessionId, agent ?? Self, shell ?? Self), sessionId, mode,
            AgentModeClassifier.MayShareBackend(provider));
        new AttachmentStore(workspace.Paths).Save(outcome.Attachment);
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
        {
            Provider = provider,
            SessionId = sessionId,
            AttachmentId = outcome.Attachment.Id,
            Directory = workspace.Root
        });
        return new StateStore(workspace.Paths).Drain().Sessions[provider + ":" + sessionId];
    }

    public static IEnumerable<object[]> DirectModes() =>
    [
        ["claude", AgentModes.InteractiveClient],
        ["claude", AgentModes.Unknown],
        ["codex", AgentModes.EmbeddedRuntime]
    ];

    [Theory]
    [MemberData(nameof(DirectModes))]
    public void OnlyAnAdoptedDirectSessionWithAVerifiedAgentAndShellQualifies(string provider, string mode)
    {
        var outcome = NativeAdoption.Apply(Captured(provider, "a", Self, Self), "s", mode, AgentModeClassifier.MayShareBackend(provider));
        Assert.True(NativePresentation.IsAdoptedDirect(outcome.Attachment));
        Assert.True(NativePresentation.GrantsPresentation(outcome.Attachment, enabled: true));
        Assert.False(NativePresentation.GrantsPresentation(outcome.Attachment, enabled: false));
        // The consumer's view adds the title and background consent; ingress never sets it.
        Assert.False(outcome.Attachment.ManagedTitle);
        var effective = NativePresentation.Effective(outcome.Attachment, true);
        Assert.True(effective.ManagedTitle);
        Assert.True(effective.ManagedBackground);
        Assert.False(NativePresentation.Effective(outcome.Attachment, false).ManagedTitle);
        Assert.False(NativePresentation.IsAdoptedDirect(outcome.Attachment with { Process = null }));
        Assert.False(NativePresentation.IsAdoptedDirect(outcome.Attachment with { Host = outcome.Attachment.Host with { Shell = null } }));
        Assert.False(NativePresentation.IsAdoptedDirect(outcome.Attachment with { Evidence = null }));
        Assert.False(NativePresentation.IsAdoptedDirect(outcome.Attachment with { EndedAt = DateTimeOffset.UtcNow }));
    }

    [Theory]
    [InlineData("claude", AgentModes.SharedBackend)]
    [InlineData("codex", AgentModes.SharedBackend)]
    [InlineData("codex", AgentModes.Unknown)]
    [InlineData("claude", AgentModes.Headless)]
    [InlineData("codex", AgentModes.Headless)]
    public void SharedBackendUnknownCodexAndHeadlessNeverQualify(string provider, string mode)
    {
        var outcome = NativeAdoption.Apply(Captured(provider, "a", Self, Self), "s", mode, AgentModeClassifier.MayShareBackend(provider));
        Assert.False(NativePresentation.IsAdoptedDirect(outcome.Attachment));
        Assert.False(NativePresentation.GrantsPresentation(outcome.Attachment, enabled: true));
        // A shared-backend record that somehow keeps a process and shell is still excluded by its own mode and lifecycle evidence.
        var forged = Captured(provider, "a", Self, Self) with
        {
            Evidence = new AttachmentEvidence { Mode = AgentModes.SharedBackend, Generation = Self, LifecycleOwner = LifecycleOwners.SharedBackend }
        };
        Assert.False(NativePresentation.IsAdoptedDirect(forged));
        Assert.False(NativePresentation.GrantsPresentation(forged, enabled: true));
    }

    [Fact]
    public void ManagedLaunchesKeepTheirOwnControlsAndAnOptOutWins()
    {
        var evidence = new AttachmentEvidence { Mode = AgentModes.InteractiveClient, Generation = Self, LifecycleOwner = LifecycleOwners.BellTower };
        var optedOut = Captured("claude", "m", Self, Self, "managed") with { Evidence = evidence };
        Assert.False(NativePresentation.GrantsPresentation(optedOut, enabled: true));
        Assert.False(NativePresentation.Effective(optedOut, true).ManagedTitle);
        var managed = optedOut with { ManagedTitle = true, ManagedBackground = true };
        Assert.Equal(managed, NativePresentation.Effective(managed, true));
        Assert.Equal(managed, NativePresentation.Effective(managed, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettingDefaultsOffAndRoundTripsThroughConfiguration(bool enabled)
    {
        using var workspace = new TestWorkspace();
        Assert.False(new ConfigStore(workspace.Paths).Read().NativePresentation);
        Setting(workspace, enabled);
        Assert.Equal(enabled, new ConfigStore(workspace.Paths).Read().NativePresentation);
    }

    [Fact]
    public void OffLeavesEveryNativeSessionUntouchedAndOnPresentsOnlyTheTitleOfBothProviders()
    {
        using var workspace = new TestWorkspace();
        var claude = Adopt(workspace, "claude", AgentModes.InteractiveClient, sessionId: "c");
        var codex = Adopt(workspace, "codex", AgentModes.EmbeddedRuntime, sessionId: "d");
        var shared = Adopt(workspace, "codex", AgentModes.SharedBackend, sessionId: "s");
        var resolver = new TerminalPresentationResolver(workspace.Paths);
        var console = new Console();
        var service = new TerminalTitleService(workspace.Paths, console);
        Assert.All(new[] { claude, codex, shared }, session => Assert.Null(resolver.Resolve(session, new())));
        Assert.Empty(console.Writes);
        Assert.False(service.Apply(claude.Key));

        Setting(workspace, true);
        var first = resolver.Resolve(claude, new());
        var second = resolver.Resolve(codex, new());
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(new IdentityStore(workspace.Paths).Get(claude.Key, workspace.Root).Palette.Background, first.Background);
        Assert.Equal(new IdentityStore(workspace.Paths).Get(codex.Key, workspace.Root).Palette.Background, second.Background);
        Assert.NotEqual(first.Background, second.Background);
        // Plain default Codex on a shared backend gets nothing even with the setting on.
        Assert.Null(resolver.Resolve(shared, new()));
        Assert.True(service.Apply(first));
        Assert.True(service.Apply(second));
        Assert.False(service.Apply(shared.Key));
        Assert.Equal(2, console.Writes.Count);
        Assert.All(console.Writes, write => Assert.NotNull(write.Background));
        Assert.Equal(new[] { first.Background, second.Background }, console.Writes.Select(write => write.Background));
    }

    [Fact]
    public void TurningTheSettingOffStopsWritesEvenForARequestComputedEarlier()
    {
        using var workspace = new TestWorkspace();
        var session = Adopt(workspace, "claude", AgentModes.InteractiveClient);
        Setting(workspace, true);
        var desired = new TerminalPresentationResolver(workspace.Paths).Resolve(session, new())!;
        var console = new Console();
        var service = new TerminalTitleService(workspace.Paths, console);
        Setting(workspace, false);
        Assert.False(service.Apply(desired));
        Assert.Empty(console.Writes);
    }

    [Fact]
    public void ANativeBackgroundRequestIsWrittenAndInvalidColorsAreRefused()
    {
        using var workspace = new TestWorkspace();
        var session = Adopt(workspace, "claude", AgentModes.InteractiveClient);
        Setting(workspace, true);
        var desired = new TerminalPresentationResolver(workspace.Paths).Resolve(session, new())!;
        var console = new Console();
        Assert.False(new TerminalTitleService(workspace.Paths, console).Apply(desired with { Background = "#112233\u001b]0;x" }));
        Assert.Empty(console.Writes);
        Assert.True(new TerminalTitleService(workspace.Paths, console).Apply(desired with { Background = "#112233" }));
        Assert.Equal("#112233", Assert.Single(console.Writes).Background);
    }

    [Fact]
    public void NoWriteFollowsAChangedAgentOrShellIdentity()
    {
        using var workspace = new TestWorkspace();
        var session = Adopt(workspace, "claude", AgentModes.InteractiveClient);
        Setting(workspace, true);
        var desired = new TerminalPresentationResolver(workspace.Paths).Resolve(session, new())!;
        var console = new Console();
        var service = new TerminalTitleService(workspace.Paths, console);
        Assert.False(service.Apply(desired with { Agent = desired.Agent with { StartedUtcTicks = 1 } }));
        Assert.False(service.Apply(desired with { Shell = desired.Shell with { StartedUtcTicks = 1 } }));
        Assert.Empty(console.Writes);
        Assert.Empty(new TitleRestoreStore(workspace.Paths).List());

        // A reused shell (same process identifier, different creation time) is never resolved at all.
        using var reusedWorkspace = new TestWorkspace();
        var reused = Adopt(reusedWorkspace, "codex", AgentModes.EmbeddedRuntime, shell: Self with { StartedUtcTicks = 1 });
        Setting(reusedWorkspace, true);
        Assert.Null(new TerminalPresentationResolver(reusedWorkspace.Paths).Resolve(reused, new()));
        Assert.False(new TerminalTitleService(reusedWorkspace.Paths, console).Apply(reused.Key));
        Assert.Empty(console.Writes);
    }

    [Fact]
    public void AWriteNeedsAReadablePriorTitleSoRestorationIsAlwaysPossible()
    {
        using var workspace = new TestWorkspace();
        var session = Adopt(workspace, "claude", AgentModes.InteractiveClient);
        Setting(workspace, true);
        var desired = new TerminalPresentationResolver(workspace.Paths).Resolve(session, new())!;
        var blind = new BlindConsole();
        Assert.False(new TerminalTitleService(workspace.Paths, blind).Apply(desired));
        Assert.Equal(0, blind.Writes);
    }

    private sealed class BlindConsole : IConsoleTitleWriter
    {
        public int Writes;
        public bool Write(ProcessIdentity shell, ProcessIdentity agent, string title, string? background = null) { Writes++; return true; }
    }

    [Theory]
    [InlineData("claude", AgentModes.InteractiveClient)]
    [InlineData("codex", AgentModes.EmbeddedRuntime)]
    public void ExitRestoresOnlyWhatBellTowerChangedAndOnlyOnTheRecordedShell(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        var session = Adopt(workspace, provider, mode);
        Setting(workspace, true);
        var console = new Console { Initial = "PowerShell" };
        var service = new TerminalTitleService(workspace.Paths, console);
        var desired = new TerminalPresentationResolver(workspace.Paths).Resolve(session, new())!;
        Assert.True(service.Apply(desired));
        Assert.True(service.Apply(desired with { Title = "Renamed" }));
        var record = Assert.Single(new TitleRestoreStore(workspace.Paths).List());
        Assert.Equal("PowerShell", record.Previous);
        Assert.Equal("Renamed", record.Written);
        Assert.True(record.Colored);
        Assert.Equal(Self, record.Shell);

        Assert.True(service.Restore(desired.AttachmentId));
        var restore = Assert.Single(console.Restores);
        Assert.Equal((Self, "Renamed", "PowerShell"), restore);
        Assert.Equal(Self, Assert.Single(console.ColorResets));
        Assert.Equal("PowerShell", console.Titles[Self.Pid]);
        Assert.Empty(new TitleRestoreStore(workspace.Paths).List());
        // Nothing left to restore a second time.
        Assert.True(service.Restore(desired.AttachmentId));
        Assert.Single(console.Restores);
        Assert.Single(console.ColorResets);
    }

    [Fact]
    public void TheResetSequenceRestoresTheTerminalDefaultsAndIsTheOneManagedLaunchesUse()
    {
        Assert.Equal("\u001b]110\u001b\\" + "\u001b]111\u001b\\", TerminalColors.Reset);
        Assert.StartsWith("\u001b]11;#112233", TerminalColors.Apply("#112233"), StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordWithoutColorNeverResetsColors()
    {
        using var workspace = new TestWorkspace();
        var console = new Console();
        new TitleRestoreStore(workspace.Paths).Save(new TitleRestore("plain", Self, "before", "written"));
        Assert.True(new TerminalTitleService(workspace.Paths, console).Restore("plain"));
        Assert.Single(console.Restores);
        Assert.Empty(console.ColorResets);
    }

    [Fact]
    public void ARecordedShellThatIsGoneOrReusedIsDiscardedWithoutAnyWrite()
    {
        using var workspace = new TestWorkspace();
        var console = new Console();
        var records = new TitleRestoreStore(workspace.Paths);
        records.Save(new TitleRestore("gone", Self with { StartedUtcTicks = 1 }, "before", "managed") { Colored = true });
        records.Save(new TitleRestore("dead", new ProcessIdentity(int.MaxValue, 1), "before", "managed") { Colored = true });
        var service = new TerminalTitleService(workspace.Paths, console);
        Assert.True(service.Restore("gone"));
        Assert.True(service.Restore("dead"));
        Assert.Empty(console.Restores);
        Assert.Empty(console.ColorResets);
        Assert.Empty(records.List());
    }

    [Fact]
    public void ASecondSessionInTheSameShellSharesTheShellRecordAndTheFirstExitLeavesItAlone()
    {
        using var workspace = new TestWorkspace();
        var first = Adopt(workspace, "claude", AgentModes.InteractiveClient, sessionId: "one");
        var second = Adopt(workspace, "codex", AgentModes.EmbeddedRuntime, sessionId: "two");
        Setting(workspace, true);
        var console = new Console { Initial = "PowerShell" };
        var service = new TerminalTitleService(workspace.Paths, console);
        var resolver = new TerminalPresentationResolver(workspace.Paths);
        var one = resolver.Resolve(first, new())!;
        var two = resolver.Resolve(second, new())!;
        Assert.True(service.Apply(one));
        Assert.True(service.Apply(two));
        var records = new TitleRestoreStore(workspace.Paths);
        var shared = Assert.Single(records.List());
        Assert.Equal("PowerShell", shared.Previous);
        Assert.Equal(two.AttachmentId, shared.AttachmentId);

        new AttachmentLifecycle(workspace.Paths).Detach(one.AttachmentId);
        Assert.True(service.Restore(one.AttachmentId));
        Assert.Empty(console.Restores);
        Assert.Empty(console.ColorResets);
        Assert.Equal(two.AttachmentId, Assert.Single(records.List()).AttachmentId);
        new AttachmentLifecycle(workspace.Paths).Detach(two.AttachmentId);
        Assert.True(service.Restore(two.AttachmentId));
        Assert.Equal("PowerShell", Assert.Single(console.Restores).Previous);
        Assert.Equal(Self, Assert.Single(console.ColorResets));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwitchingOffTwoLiveSessionsInOneShellRestoresThePriorTitleOnceWhicheverWroteLast(bool codexWritesLast)
    {
        using var workspace = new TestWorkspace();
        var claude = Adopt(workspace, "claude", AgentModes.InteractiveClient, sessionId: "c");
        var codex = Adopt(workspace, "codex", AgentModes.EmbeddedRuntime, sessionId: "d");
        Setting(workspace, true);
        var console = new Console { Initial = "PowerShell" };
        var service = new TerminalTitleService(workspace.Paths, console);
        var resolver = new TerminalPresentationResolver(workspace.Paths);
        var first = resolver.Resolve(codexWritesLast ? claude : codex, new())!;
        var last = resolver.Resolve(codexWritesLast ? codex : claude, new())!;
        Assert.True(service.Apply(first));
        Assert.True(service.Apply(last));
        Assert.True(service.Apply(first with { Title = "Rewritten" }) && service.Apply(last));
        Assert.Equal(last.Title, console.Titles[Self.Pid]);
        var record = Assert.Single(new TitleRestoreStore(workspace.Paths).List());
        Assert.Equal(last.AttachmentId, record.AttachmentId);
        Assert.Equal("PowerShell", record.Previous);
        Assert.True(record.Colored);

        // Switching the setting off leaves both sessions unpresented; the pump asks for every record owner, here for both sessions.
        Setting(workspace, false);
        Assert.True(service.Restore(first.AttachmentId));
        Assert.True(service.Restore(last.AttachmentId));
        Assert.Equal((Self, last.Title, "PowerShell"), Assert.Single(console.Restores));
        Assert.Equal("PowerShell", console.Titles[Self.Pid]);
        Assert.Equal(Self, Assert.Single(console.ColorResets));
        Assert.Empty(new TitleRestoreStore(workspace.Paths).List());
    }

    [Fact]
    public void AShellRecordIsKeyedByShellIdentityAndNeverByAttachment()
    {
        using var workspace = new TestWorkspace();
        var records = new TitleRestoreStore(workspace.Paths);
        var other = Self with { StartedUtcTicks = Self.StartedUtcTicks + 1 };
        records.Save(new TitleRestore("a", Self, "one", "w"));
        records.Save(new TitleRestore("b", Self, "two", "w"));
        records.Save(new TitleRestore("c", other, "three", "w"));
        Assert.Equal(2, records.List().Count);
        Assert.Equal("b", records.Get(Self)!.AttachmentId);
        Assert.Equal("three", records.Get(other)!.Previous);
        Assert.Empty(records.OwnedBy("a"));
        records.Remove(Self);
        Assert.Null(records.Get(Self));
        Assert.NotNull(records.Get(other));
    }

    // Runs the real title service behind the pump's writer boundary, so the pump's restore and rewrite behavior is exercised end to end.
    private sealed class ServiceWriter(TerminalTitleService service) : ITerminalPresentationWriter
    {
        public Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation) => Task.FromResult(service.Apply(desired));
        public Task<bool> RestoreAsync(string attachmentId, CancellationToken cancellation) => Task.FromResult(service.Restore(attachmentId));
    }

    [Theory]
    [InlineData("claude", "codex")]
    [InlineData("codex", "claude")]
    public async Task WhenTheRecordOwnerEndsASurvivorInTheSameShellWritesAgainAndTakesTheRestoredTitleAsPrior(string survivorProvider, string ownerProvider)
    {
        using var workspace = new TestWorkspace();
        var survivor = Adopt(workspace, survivorProvider, survivorProvider == "codex" ? AgentModes.EmbeddedRuntime : AgentModes.InteractiveClient, sessionId: "survivor");
        var owner = Adopt(workspace, ownerProvider, ownerProvider == "codex" ? AgentModes.EmbeddedRuntime : AgentModes.InteractiveClient, sessionId: "owner");
        Setting(workspace, true);
        var console = new Console { Initial = "PowerShell" };
        var service = new TerminalTitleService(workspace.Paths, console);
        var now = DateTimeOffset.UtcNow;
        using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => new()), new ServiceWriter(service), () => now);
        await pump.RefreshAsync([survivor, owner]);
        var records = new TitleRestoreStore(workspace.Paths);
        Assert.Equal(owner.AttachmentId, Assert.Single(records.List()).AttachmentId);
        var writesBefore = console.Writes.Count;

        new AttachmentLifecycle(workspace.Paths).Detach(owner.AttachmentId);
        var ended = new StateStore(workspace.Paths).Drain().Sessions[owner.Key];
        await pump.RefreshAsync([survivor, ended]);
        Assert.Equal((Self, console.Writes[^1].Title, "PowerShell"), Assert.Single(console.Restores));
        Assert.Empty(records.List());
        // The shell's title now is whatever the restore left; the survivor must read that as its prior title, not reuse a stale one.
        console.Titles[Self.Pid] = "after restore";

        await pump.RefreshAsync([survivor, ended]);
        Assert.True(console.Writes.Count > writesBefore);
        var record = Assert.Single(records.List());
        Assert.Equal(survivor.AttachmentId, record.AttachmentId);
        Assert.Equal("after restore", record.Previous);
        Assert.Equal(record.Written, console.Titles[Self.Pid]);
    }

    [Theory]
    [InlineData("claude", AgentModes.InteractiveClient)]
    [InlineData("codex", AgentModes.EmbeddedRuntime)]
    public async Task AFailedRestoreKeepsTheRecordAndALaterSuccessRetriesAndRemovesIt(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        var session = Adopt(workspace, provider, mode);
        Setting(workspace, true);
        var console = new Console { Initial = "PowerShell" };
        var now = DateTimeOffset.UtcNow;
        using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => new()), new ServiceWriter(new(workspace.Paths, console)), () => now);
        await pump.RefreshAsync([session]);
        var records = new TitleRestoreStore(workspace.Paths);
        Assert.Single(records.List());

        Setting(workspace, false);
        console.FailRestoreShells.Add(Self.Pid);
        await pump.RefreshAsync([session]);
        Assert.Single(records.List());
        Assert.Empty(console.Restores);
        // Within the retry delay nothing is attempted, even once the failure clears.
        console.FailRestoreShells.Clear();
        await pump.RefreshAsync([session]);
        Assert.Single(records.List());
        Assert.Empty(console.Restores);

        now = now.AddSeconds(31);
        await pump.RefreshAsync([session]);
        Assert.Empty(records.List());
        Assert.Equal("PowerShell", Assert.Single(console.Restores).Previous);
        Assert.Equal(Self, Assert.Single(console.ColorResets));
    }

    [Theory]
    [InlineData("claude", AgentModes.InteractiveClient)]
    [InlineData("codex", AgentModes.EmbeddedRuntime)]
    public void AResumedAttachmentThatOwnsRecordsInTwoShellsRestoresEachIndependently(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        var session = Adopt(workspace, provider, mode);
        using var otherShell = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1 >nul")
        { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            var other = ProcessProbe.Identify(otherShell.Id)!;
            var records = new TitleRestoreStore(workspace.Paths);
            records.Save(new TitleRestore(session.AttachmentId, Self, "first prior", "w1") { Colored = true });
            records.Save(new TitleRestore(session.AttachmentId, other, "second prior", "w2") { Colored = true });
            var console = new Console();
            console.FailRestoreShells.Add(other.Pid);
            var service = new TerminalTitleService(workspace.Paths, console);

            // The failure on one shell keeps only that record and does not block the other.
            Assert.False(service.Restore(session.AttachmentId));
            Assert.Equal((Self, "w1", "first prior"), Assert.Single(console.Restores));
            Assert.Equal(other, Assert.Single(records.List()).Shell);

            console.FailRestoreShells.Clear();
            Assert.True(service.Restore(session.AttachmentId));
            Assert.Equal(2, console.Restores.Count);
            Assert.Equal((other, "w2", "second prior"), console.Restores[1]);
            Assert.Equal(new[] { Self, other }, console.ColorResets.ToArray());
            Assert.Empty(records.List());
        }
        finally
        {
            otherShell.Kill();
            otherShell.WaitForExit();
        }
    }

    [Theory]
    [InlineData("claude", "codex")]
    [InlineData("codex", "claude")]
    [InlineData("claude", "claude")]
    [InlineData("codex", "codex")]
    public async Task APartialRestoreFailureRewritesTheSurvivorInTheRestoredShellOnlyAndRetriesTheFailedShell(string firstProvider, string secondProvider)
    {
        using var workspace = new TestWorkspace();
        using var otherShell = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 60 127.0.0.1 >nul")
        { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            var other = ProcessProbe.Identify(otherShell.Id)!;
            static string Mode(string provider) => provider == "codex" ? AgentModes.EmbeddedRuntime : AgentModes.InteractiveClient;
            var first = Adopt(workspace, firstProvider, Mode(firstProvider), sessionId: "first");
            var second = Adopt(workspace, secondProvider, Mode(secondProvider), shell: other, sessionId: "second", agent: other);
            Setting(workspace, true);
            // An ended attachment still owns the title records of both shells; only the second shell's restore fails.
            var records = new TitleRestoreStore(workspace.Paths);
            var console = new Console { Initial = "PowerShell" };
            var now = DateTimeOffset.UtcNow;
            using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => new()), new ServiceWriter(new(workspace.Paths, console)), () => now);
            // Both survivors present first and hold receipts; then the ended attachment's records replace theirs.
            await pump.RefreshAsync([first, second]);
            records.Save(new TitleRestore("ended", Self, "first prior", console.Titles[Self.Pid]) { Colored = true });
            records.Save(new TitleRestore("ended", other, "second prior", console.Titles[other.Pid]) { Colored = true });
            var written = (First: console.Titles[Self.Pid], Second: console.Titles[other.Pid]);
            console.FailRestoreShells.Add(other.Pid);

            await pump.RefreshAsync([first, second]);
            Assert.Equal((Self, written.First, "first prior"), Assert.Single(console.Restores));
            Assert.Equal(other, Assert.Single(records.List()).Shell);
            Assert.Equal("ended", Assert.Single(records.List()).AttachmentId);
            var writes = console.Writes.Count;
            console.Titles[Self.Pid] = "after restore";

            // The survivor in the restored shell writes again and records the restored title; the failed shell's survivor is left alone.
            await pump.RefreshAsync([first, second]);
            Assert.Equal(writes + 1, console.Writes.Count);
            Assert.Equal(console.Titles[Self.Pid], console.Writes[^1].Title);
            Assert.Equal("after restore", records.Get(Self)!.Previous);
            Assert.Equal(first.AttachmentId, records.Get(Self)!.AttachmentId);
            Assert.Equal("ended", records.Get(other)!.AttachmentId);
            Assert.Equal(written.Second, console.Titles[other.Pid]);

            // Within the retry delay the failed shell is not attempted; afterwards it is restored and its survivor writes again.
            console.FailRestoreShells.Clear();
            await pump.RefreshAsync([first, second]);
            Assert.Single(console.Restores);
            now = now.AddSeconds(31);
            await pump.RefreshAsync([first, second]);
            Assert.Equal((other, written.Second, "second prior"), console.Restores[1]);
            Assert.Null(records.Get(other)?.AttachmentId is "ended" ? records.Get(other) : null);
            await pump.RefreshAsync([first, second]);
            Assert.Equal(second.AttachmentId, records.Get(other)!.AttachmentId);
        }
        finally
        {
            otherShell.Kill();
            otherShell.WaitForExit();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PumpAppliesToLiveSessionsWithoutRestartAndRestoresWhenSwitchedOffOrEnded(bool endSession)
    {
        using var workspace = new TestWorkspace();
        var claude = Adopt(workspace, "claude", AgentModes.InteractiveClient, sessionId: "c");
        var shared = Adopt(workspace, "codex", AgentModes.SharedBackend, sessionId: "s");
        var writer = new RecordingPresentation();
        var now = DateTimeOffset.UtcNow;
        using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => new()), writer, () => now);
        await pump.RefreshAsync([claude, shared]);
        Assert.Empty(writer.Writes);

        Setting(workspace, true);
        await pump.RefreshAsync([claude, shared]);
        Assert.Equal(claude.AttachmentId, Assert.Single(writer.Writes).AttachmentId);
        Assert.NotNull(writer.Writes[0].Background);
        // The writer stub does not create a record, so one stands in for the helper's own.
        new TitleRestoreStore(workspace.Paths).Save(new TitleRestore(claude.AttachmentId, Self, "before", writer.Writes[0].Title));
        await pump.RefreshAsync([claude, shared]);
        Assert.Empty(writer.Restores);

        if (endSession)
        {
            new AttachmentLifecycle(workspace.Paths).Detach(claude.AttachmentId);
            var ended = new StateStore(workspace.Paths).Drain().Sessions[claude.Key];
            await pump.RefreshAsync([ended, shared]);
        }
        else
        {
            Setting(workspace, false);
            await pump.RefreshAsync([claude, shared]);
        }
        Assert.Equal(claude.AttachmentId, Assert.Single(writer.Restores));
        Assert.DoesNotContain(writer.Writes, write => write.AttachmentId == shared.AttachmentId);
    }

    [Fact]
    public async Task ManagedLaunchesAreNeverRestoredByThePumpAndKeepTheirBackground()
    {
        using var workspace = new TestWorkspace();
        var outcome = Captured("claude", "managed-1", Self, Self, "managed") with { ManagedTitle = true, ManagedBackground = true };
        new AttachmentStore(workspace.Paths).Save(outcome with { Evidence = AttachmentEvidence.ForManagedLaunch(Self) });
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with { AttachmentId = "managed-1", Directory = workspace.Root });
        var session = new StateStore(workspace.Paths).Drain().Sessions["claude:session-1"];
        var writer = new RecordingPresentation();
        using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => new()), writer, () => DateTimeOffset.UtcNow);
        await pump.RefreshAsync([session]);
        Assert.NotNull(Assert.Single(writer.Writes).Background);
        Setting(workspace, true);
        await pump.RefreshAsync([session]);
        Assert.Empty(writer.Restores);
        Assert.NotNull(Assert.Single(writer.Writes).Background);
    }
}
