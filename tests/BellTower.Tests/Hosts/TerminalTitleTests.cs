using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class TerminalTitleTests
{
    private sealed class RecordingWriter : IConsoleTitleWriter
    {
        public List<string> Titles { get; } = [];
        public List<string?> Backgrounds { get; } = [];
        public bool Write(ProcessIdentity shell, ProcessIdentity agent, string title, string? background = null)
        {
            Titles.Add(title);
            Backgrounds.Add(background);
            return true;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManagedBackgroundUsesThePersistedIdentityPaletteOnlyWhenEnabled(bool enabled)
    {
        using var workspace = new TestWorkspace();
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        new AttachmentStore(workspace.Paths).Save(new("attachment-1", "claude", process, new("vscode", process), workspace.Root,
            DateTimeOffset.UtcNow, ManagedTitle: true, ManagedBackground: enabled));
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Register));
        new StateStore(workspace.Paths).Drain();
        var identity = new IdentityStore(workspace.Paths).Get("claude:session-1", workspace.Root);
        var writer = new RecordingWriter();
        Assert.True(new TerminalTitleService(workspace.Paths, writer).Apply(identity.Key));
        Assert.Equal(enabled ? identity.Palette.Background : null, writer.Backgrounds.Single());
        Assert.True(Palette.Contrast(identity.Palette.Background, TerminalColors.Foreground) >= 4.5);
        Assert.DoesNotContain('\a', TerminalColors.Apply(identity.Palette.Background));
    }

    [Theory]
    [InlineData("#112233\u001b]0;injected")]
    [InlineData("invalid")]
    public void BackgroundSequencesRejectAnythingExceptHexColors(string value)
    {
        Assert.Throws<ArgumentException>(() => TerminalColors.Apply(value));
    }

    [Fact]
    public void ManagedTitleUsesSharedIdentityAndRenameDoesNotChangePaletteOrWait()
    {
        using var workspace = new TestWorkspace();
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        new AttachmentStore(workspace.Paths).Save(new("attachment-1", "claude", process, new("vscode", process), workspace.Root, DateTimeOffset.UtcNow, ManagedTitle: true));
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with { Directory = workspace.Root });
        var state = new StateStore(workspace.Paths).Drain();
        var episode = state.Sessions["claude:session-1"].Episode;
        var identity = new IdentityStore(workspace.Paths).Get("claude:session-1", workspace.Root, "First name");
        var palette = identity.Palette;
        var writer = new RecordingWriter();
        var title = new TerminalTitleService(workspace.Paths, writer);
        Assert.True(title.Apply(identity.Key));
        identity.ManualName = "Next name\u001b\n";
        new IdentityStore(workspace.Paths).Save(identity);
        Assert.True(title.Apply(identity.Key));
        var git = new GitReader().Read(workspace.Root);
        Assert.Equal(new[] { SessionPresentation.Title("First name", git), SessionPresentation.Title("Next name", git) }, writer.Titles);
        Assert.Equal(palette, new IdentityStore(workspace.Paths).Get(identity.Key, workspace.Root).Palette);
        Assert.Equal(episode, new StateStore(workspace.Paths).Read().Sessions[identity.Key].Episode);
        new AttachmentLifecycle(workspace.Paths).Detach("attachment-1");
        Assert.False(title.Apply(identity.Key));
        Assert.Equal(2, writer.Titles.Count);
    }

    [Theory]
    [InlineData(false, "vscode", false)]
    [InlineData(true, "unknown", false)]
    [InlineData(true, "windows-terminal", true)]
    public void UnownedUnknownOrReusedShellsAreNeverDecorated(bool managed, string host, bool reused)
    {
        using var workspace = new TestWorkspace();
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var shell = reused ? process with { StartedUtcTicks = 1 } : process;
        new AttachmentStore(workspace.Paths).Save(new("attachment-1", "claude", process, new(host, shell), workspace.Root, DateTimeOffset.UtcNow, ManagedTitle: managed));
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Register));
        new StateStore(workspace.Paths).Drain();
        var writer = new RecordingWriter();
        Assert.False(new TerminalTitleService(workspace.Paths, writer).Apply("claude:session-1"));
        Assert.Empty(writer.Titles);
    }
}
