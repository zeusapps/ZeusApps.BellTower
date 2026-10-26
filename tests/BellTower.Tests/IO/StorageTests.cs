using BellTower.Core;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

public sealed class StorageTests
{
    [Fact]
    public void InboxSurvivesMissingTrayAndRestoresState()
    {
        using var w = new TestWorkspace();
        var e = TestWorkspace.ConfirmedPermission(DateTimeOffset.UtcNow.AddMinutes(-1));
        new EventStore(w.Paths).Publish(e);
        Assert.Single(Directory.GetFiles(w.Paths.Inbox));
        var state = new StateStore(w.Paths).Drain();
        Assert.Equal(AttentionReason.Permission, state.Sessions[e.Key].Reason);
        Assert.Empty(Directory.GetFiles(w.Paths.Inbox));
        Assert.Equal(e.Id, new StateStore(w.Paths).Read().Sessions[e.Key].Episode);
        new EventStore(w.Paths).Publish(e);
        Assert.Single(new StateStore(w.Paths).Drain().Sessions[e.Key].Waits);
    }

    [Fact]
    public void BadSnapshotFallsBackToLastGoodBackup()
    {
        using var w = new TestWorkspace();
        var store = new StateStore(w.Paths);
        new EventStore(w.Paths).Publish(TestWorkspace.ConfirmedPermission());
        store.Drain();
        new EventStore(w.Paths).Publish(TestWorkspace.Event(EventKind.Prompt));
        store.Drain();
        File.WriteAllText(w.Paths.State, "not json");
        Assert.Single(store.Read().Sessions);
    }

    [Fact]
    public void MalformedInboxDoesNotBreakOtherSessions()
    {
        using var w = new TestWorkspace();
        File.WriteAllText(Path.Combine(w.Paths.Inbox, "bad.json"), "{invalid");
        new EventStore(w.Paths).Publish(TestWorkspace.ConfirmedPermission());
        Assert.Single(new StateStore(w.Paths).Drain().Sessions);
        Assert.Empty(Directory.GetFiles(w.Paths.Inbox));
    }

    [Fact]
    public void DeadAttachmentIsRemovedWithoutInactivityExpiry()
    {
        using var w = new TestWorkspace();
        var store = new StateStore(w.Paths);
        new AttachmentStore(w.Paths).Save(new("attachment-1", "claude", new(int.MaxValue, 1), new(), w.Root, DateTimeOffset.UtcNow));
        new EventStore(w.Paths).Publish(TestWorkspace.ConfirmedPermission());
        Assert.True(store.Drain().Sessions["claude:session-1"].Ended);
    }

    [Fact]
    public void QuietLiveProcessIsNotExpired()
    {
        using var w = new TestWorkspace();
        var store = new StateStore(w.Paths);
        new AttachmentStore(w.Paths).Save(new("attachment-1", "claude", ProcessProbe.Identify(Environment.ProcessId), new(), w.Root, DateTimeOffset.UtcNow));
        new EventStore(w.Paths).Publish(TestWorkspace.ConfirmedPermission(DateTimeOffset.UtcNow.AddDays(-2)));
        Assert.False(store.Drain().Sessions["claude:session-1"].Ended);
    }

    [Fact]
    public void CreationTimeProtectsAgainstPidReuse()
    {
        var actual = ProcessProbe.Identify(Environment.ProcessId)!;
        Assert.True(ProcessProbe.IsAlive(actual));
        Assert.False(ProcessProbe.IsAlive(actual with
        {
            StartedUtcTicks = 1
        }));
    }

    [Fact]
    public void IdentityPersistsWithoutOpenSpecAndCanBeEdited()
    {
        using var w = new TestWorkspace();
        var store = new IdentityStore(w.Paths);
        var identity = store.Get("codex:x", w.Root);
        var palette = identity.Palette;
        identity.Change = "add-codex";
        store.Save(identity);
        Assert.Equal("add-codex", store.Get("codex:x", w.Root).Name);
        Assert.Equal(palette, store.Get("codex:x", w.Root).Palette);
        identity.ManualName = "My task";
        store.Save(identity);
        Assert.Equal("My task", store.Get("codex:x", w.Root).Name);
    }

    [Fact]
    public void LegacyImportCopiesDataWithoutDependingOnSource()
    {
        using var w = new TestWorkspace();
        var file = Path.Combine(w.Root, "badge.json");
        File.WriteAllText(file, """{"title":"PM task","background":"#102030","accent":"#4080c0"}""");
        LegacyImport.ImportBadge(w.Paths, file, "codex", "session", w.Root);
        File.Delete(file);
        var identity = new IdentityStore(w.Paths).Get("codex:session", w.Root);
        Assert.Equal("PM task", identity.Name);
        Assert.Equal("#4080c0", identity.Palette.Accent);
    }

    [Fact]
    public void ConfigReloadAndInvalidConfigUseLastGood()
    {
        using var w = new TestWorkspace();
        var config = new ConfigStore(w.Paths);
        config.Update(c => c.Voice = false);
        Assert.False(config.Read().Voice);
        File.WriteAllText(w.Paths.Config, "broken");
        Assert.False(config.Read().Voice);
    }

    [Fact]
    public void LogsAreBoundedAndDoNotContainPayloads()
    {
        using var w = new TestWorkspace();
        var log = new LocalLog(w.Paths);
        for (var i = 0; i < 1800; i++)
        {
            log.Write(new string('x', 200));
        }

        Assert.True(new FileInfo(Path.Combine(w.Paths.Root, "bell-tower.log")).Length < 256 * 1024);
    }
}
