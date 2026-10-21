using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

public sealed class ReliabilityTests
{
    [Fact]
    public void EventReceiptsDoNotGrowWithoutBoundAcrossCommittedBatches()
    {
        using var w = new TestWorkspace();
        var events = new EventStore(w.Paths);
        var state = new StateStore(w.Paths);
        events.Publish(TestWorkspace.Event(EventKind.Register));
        state.Drain();
        var initial = new FileInfo(w.Paths.State).Length;
        for (var i = 0; i < 100; i++)
        {
            events.Publish(TestWorkspace.Event(EventKind.Register));
            state.Drain();
        }

        Assert.True(new FileInfo(w.Paths.State).Length < initial * 2);
    }

    [Fact]
    public void TrailingDirectorySeparatorsDoNotCreateAnotherTrayNamespace()
    {
        using var w = new TestWorkspace();
        var equivalent = new DataPaths(w.Paths.Root + Path.DirectorySeparatorChar);
        Assert.Equal(w.Paths.LockName("tray-instance"), equivalent.LockName("tray-instance"));
    }

    [Fact]
    public void CorruptIdentityUsesAnExistingValidPaletteBackup()
    {
        using var w = new TestWorkspace();
        var identities = new IdentityStore(w.Paths);
        var identity = identities.Get("claude:test", w.Root);
        var palette = identity.Palette;
        identity.ManualName = "Edited";
        identities.Save(identity);
        var file = Path.Combine(w.Paths.Identities, ContentHash.Compute(identity.Key) + ".json");
        File.WriteAllText(file, "broken");
        Assert.Equal(palette, identities.Get(identity.Key, w.Root).Palette);
    }
}
