using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class TerminalTabSelectionTests
{
    private sealed class ConsoleSession : ISessionConsole
    {
        public string Title = "Duplicate friendly session name";
        public bool Disposed;
        public string ReadTitle() => Title;
        public bool SetTitle(string title) { Title = title; return true; }
        public bool Write(string text) => true;
        public void Dispose() => Disposed = true;
    }

    private sealed class Tab(Func<string> name) : ITerminalTab
    {
        public int Selections;
        public string Name => name();
        public bool Select() { Selections++; return true; }
    }

    private sealed class Tabs : ITerminalTabSource
    {
        public Func<IReadOnlyList<ITerminalTab>> Read = () => [];
        public List<long> Windows = [];
        public IReadOnlyList<ITerminalTab> ReadTabs(long window) { Windows.Add(window); return Read(); }
    }

    private sealed class Windows : IWindowActivationApi
    {
        public int Activations;
        public bool IsMinimized(IntPtr window) => false;
        public void Restore(IntPtr window) => throw new InvalidOperationException("A visible window must not be restored.");
        public bool SetForeground(IntPtr window) { Activations++; return true; }
        public bool IsForeground(IntPtr window) => true;
    }

    private static void SaveAttachment(TestWorkspace workspace, bool ended = false)
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        new AttachmentStore(workspace.Paths).Save(new("attachment", "codex", process,
            new("windows-terminal", process, process, 1234, "terminal-session"), workspace.Root, DateTimeOffset.UtcNow,
            ended ? DateTimeOffset.UtcNow : null, ManagedTitle: true));
    }

    [Fact]
    public void FreshMarkerSelectsTheSameLiveTabAfterReorderAndRestoresItsTitle()
    {
        using var workspace = new TestWorkspace();
        SaveAttachment(workspace);
        var console = new ConsoleSession();
        var target = new Tab(() => console.Title);
        var other = new Tab(() => "Duplicate friendly session name");
        var tabs = new Tabs { Read = () => [other, target] };
        var windows = new Windows();
        var focus = new WindowsTerminalFocusService(workspace.Paths, new(tabs), new(windows), (_, _) => console, _ => true);
        Assert.Equal("exact tab", focus.Focus("attachment").Precision);
        Assert.Equal("Duplicate friendly session name", console.Title);
        tabs.Read = () => [target, other];
        Assert.Equal("exact tab", focus.Focus("attachment").Precision);
        Assert.Equal(2, target.Selections);
        Assert.Equal(0, other.Selections);
        Assert.All(tabs.Windows, window => Assert.Equal(1234, window));
        Assert.True(console.Disposed);
        Assert.Equal(2, windows.Activations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrAmbiguousMarkersFallBackWithoutSelectingAnotherTab(bool ambiguous)
    {
        using var workspace = new TestWorkspace();
        SaveAttachment(workspace);
        var console = new ConsoleSession();
        var first = new Tab(() => ambiguous ? console.Title : "Other tab");
        var second = new Tab(() => ambiguous ? console.Title : "Another tab");
        var tabs = new Tabs { Read = () => [first, second] };
        var windows = new Windows();
        var result = new WindowsTerminalFocusService(workspace.Paths, new(tabs), new(windows), (_, _) => console, _ => true).Focus("attachment");
        Assert.True(result.Success);
        Assert.Equal("window only", result.Precision);
        Assert.Equal(0, first.Selections + second.Selections);
        Assert.Equal("Duplicate friendly session name", console.Title);
    }

    [Fact]
    public void AWindowThatChangesDuringResolutionIsNeverSelectedOrActivated()
    {
        using var workspace = new TestWorkspace();
        SaveAttachment(workspace);
        var console = new ConsoleSession();
        var target = new Tab(() => console.Title);
        var owned = true;
        var tabs = new Tabs { Read = () => { owned = false; return [target]; } };
        var windows = new Windows();
        var result = new WindowsTerminalFocusService(workspace.Paths, new(tabs), new(windows), (_, _) => console, _ => owned).Focus("attachment");
        Assert.False(result.Success);
        Assert.Equal(0, target.Selections);
        Assert.Equal(0, windows.Activations);
        Assert.Equal("Duplicate friendly session name", console.Title);
    }

    private static void SaveNative(TestWorkspace workspace, string provider, string mode)
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var outcome = BellTower.Core.NativeAdoption.Apply(new("attachment", provider, process,
            new("windows-terminal", process, process, 1234, "terminal-session") { ShellSource = "ancestry" }, workspace.Root, DateTimeOffset.UtcNow)
        { Ownership = "observed" }, "session", mode, BellTower.Providers.AgentModeClassifier.MayShareBackend(provider));
        new AttachmentStore(workspace.Paths).Save(outcome.Attachment);
        ProviderTitleFixture.Apply(workspace.Paths, "claude", "codex");
    }

    [Theory]
    [InlineData("claude", "interactive-client")]
    [InlineData("codex", "embedded-runtime")]
    public void AdoptedDirectSessionsSelectTheirTabOnlyWhileTheNativePresentationSettingIsOn(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        SaveNative(workspace, provider, mode);
        var console = new ConsoleSession();
        var target = new Tab(() => console.Title);
        var tabs = new Tabs { Read = () => [target] };
        var windows = new Windows();
        var focus = new WindowsTerminalFocusService(workspace.Paths, new(tabs), new(windows), (_, _) => console, _ => true);
        new ConfigStore(workspace.Paths).Update(c => c.NativePresentation = false);
        var off = focus.Focus("attachment");
        Assert.True(off.Success);
        Assert.Equal("window only", off.Precision);
        Assert.Equal(0, target.Selections);

        new ConfigStore(workspace.Paths).Update(c => c.NativePresentation = true);
        var on = focus.Focus("attachment");
        Assert.Equal("exact tab", on.Precision);
        Assert.Equal(1, target.Selections);
        Assert.Equal("Duplicate friendly session name", console.Title);
    }

    // The default setting is on. A nested client in a live launch's shell carries the shared-shell marker: the click brings the window
    // forward, no tab is selected or retitled, and the result is window only with the normal text, not "no longer registered".
    [Theory]
    [InlineData("claude", "interactive-client")]
    [InlineData("codex", "embedded-runtime")]
    public void ANestedClientInALaunchShellFocusesTheWindowOnlyUnderTheDefault(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        // The record is what the reconcile rule produces for a nested client in a live launch's shell, not a hand-built marker.
        var launcher = new BellTower.Core.ProcessIdentity(process.Pid + 1, process.StartedUtcTicks);
        var launch = new BellTower.Core.Attachment("attachment", provider, launcher, new("windows-terminal", process, process, 1234, "terminal-session") { ShellSource = "launch-env" },
            workspace.Root, DateTimeOffset.UtcNow, ManagedTitle: true, ManagedBackground: true)
        { Ownership = "managed" };
        var nested = BellTower.Core.NativeAdoption.Apply(launch with
        {
            Process = process,
            Host = launch.Host with { ShellSource = "ancestry" }
        }, "session", mode, BellTower.Providers.AgentModeClassifier.MayShareBackend(provider)).Attachment;
        var reconciled = BellTower.Core.NativeAdoption.Reconcile(nested, launch, "session", _ => true);
        Assert.Equal(BellTower.Core.HostTarget.LaunchShared, reconciled.Host.ShellSource);
        new AttachmentStore(workspace.Paths).Save(reconciled with { Id = "attachment" });
        var console = new ConsoleSession();
        var target = new Tab(() => console.Title);
        var windows = new Windows();
        var focus = new WindowsTerminalFocusService(workspace.Paths, new(new Tabs { Read = () => [target] }), new(windows), (_, _) => console, _ => true);

        var result = focus.Focus("attachment");

        Assert.True(result.Success);
        Assert.Equal("window only", result.Precision);
        Assert.Contains("tab could not be identified", result.Message);
        Assert.Equal(0, target.Selections);
        Assert.Equal(1, windows.Activations);
        Assert.Equal("Duplicate friendly session name", console.Title);
    }

    [Fact]
    public void ASharedBackendConversationNeverSelectsATabEvenWithTheSettingOn()
    {
        using var workspace = new TestWorkspace();
        SaveNative(workspace, "codex", "shared-backend");
        new ConfigStore(workspace.Paths).Update(c => c.NativePresentation = true);
        var tabs = new Tabs();
        var result = new WindowsTerminalFocusService(workspace.Paths, new(tabs), new(new Windows()),
            (_, _) => throw new Exception("Unexpected console access"), _ => true).Focus("attachment");
        Assert.False(result.Success);
        Assert.Empty(tabs.Windows);
    }

    [Fact]
    public void ClosedAttachmentsNeverOpenAConsoleOrReadTabs()
    {
        using var workspace = new TestWorkspace();
        SaveAttachment(workspace, ended: true);
        var tabs = new Tabs();
        var windows = new Windows();
        var result = new WindowsTerminalFocusService(workspace.Paths, new(tabs), new(windows), (_, _) => throw new Exception("Unexpected console access"), _ => true).Focus("attachment");
        Assert.False(result.Success);
        Assert.Empty(tabs.Windows);
        Assert.Equal(0, windows.Activations);
    }
}
