using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

// Consuming a managed launch and writing the session's identity are one step under the identity lock (ANALYSIS-100), and reset
// and random color never return the session's own color (ANALYSIS-101).
public sealed class LaunchConsumptionAllocationTests
{
    private static ProcessIdentity Self => ProcessProbe.Identify(Environment.ProcessId)!;

    private static bool Consumed(TestWorkspace workspace, string id) =>
        AtomicFile.Read<LaunchPresentation>(Path.Combine(workspace.Paths.Root, "launches", ContentHash.Compute(id) + ".json"))!.PaletteConsumed;

    // Runs the consuming step on another thread while this thread holds the identity lock, gives it time to reach the lock, then
    // allocates for a second session from here, exactly the interleaving a concurrent allocation would produce.
    private static Palette AllocateWhileConsuming(TestWorkspace workspace, string launchId, Action consume)
    {
        Palette second;
        Task consumer;
        using (new DataLock(workspace.Paths, "identity"))
        {
            consumer = Task.Run(consume);
            // Before the fix the launch is marked consumed before the identity lock is requested; after it, never while the lock is held.
            SpinWait.SpinUntil(() => Consumed(workspace, launchId), TimeSpan.FromMilliseconds(1500));
            second = new IdentityStore(workspace.Paths).AllocatePalette("other:second");
        }
        consumer.GetAwaiter().GetResult();
        return second;
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ADrainedCaptureConsumesTheLaunchUnderTheIdentityLockSoNoOtherSessionTakesItsColor(string provider)
    {
        using var workspace = new TestWorkspace();
        var launch = new LaunchPresentationStore(workspace.Paths).Prepare("fixture-attachment", Self);
        using var document = JsonDocument.Parse("{\"session_id\":\"fixture\",\"cwd\":" + JsonSerializer.Serialize(workspace.Root) + ",\"hook_event_name\":\"Stop\"}");
        var envelope = CaptureFactory.Create(provider, document.RootElement,
            new("fixture-attachment", provider, null, new(), workspace.Root, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow)!;
        CaptureSpool.Write(workspace.Paths.Root, envelope with { Attachment = envelope.Attachment with { Host = new("unknown", Self) } });
        var second = AllocateWhileConsuming(workspace, "fixture-attachment", () => new StateStore(workspace.Paths).Drain());
        Assert.Equal(launch.Palette, new IdentityStore(workspace.Paths).Get(provider + ":fixture", workspace.Root).Palette);
        Assert.NotEqual(launch.Palette.Accent, second.Accent);
        Assert.True(Consumed(workspace, "fixture-attachment"));
    }

    private static HookIngestor Ingestor(TestWorkspace workspace, string provider) => new(workspace.Paths, (_, _, _) =>
        new NativeAdoption.Outcome(new("hook-launch", provider, null, new("unknown", Self), workspace.Root, DateTimeOffset.UtcNow), true));

    private static string Payload(TestWorkspace workspace) =>
        "{\"session_id\":\"hooked\",\"cwd\":" + JsonSerializer.Serialize(workspace.Root) + ",\"hook_event_name\":\"Stop\"}";

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AHookConsumesTheLaunchUnderTheIdentityLockSoNoOtherSessionTakesItsColor(string provider)
    {
        using var workspace = new TestWorkspace();
        var launch = new LaunchPresentationStore(workspace.Paths).Prepare("hook-launch", Self);
        var second = AllocateWhileConsuming(workspace, "hook-launch", () => Ingestor(workspace, provider).Ingest(provider, new StringReader(Payload(workspace))));
        Assert.Equal(launch.Palette, new IdentityStore(workspace.Paths).Get(provider + ":hooked", workspace.Root).Palette);
        Assert.NotEqual(launch.Palette.Accent, second.Accent);
        Assert.True(Consumed(workspace, "hook-launch"));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AnExistingIdentityLeavesTheLaunchUnconsumedAndItsPaletteUnchanged(string provider)
    {
        using var workspace = new TestWorkspace();
        var saved = new IdentityStore(workspace.Paths).Get(provider + ":hooked", workspace.Root, initialPalette: Palette.Series[7]).Palette;
        new LaunchPresentationStore(workspace.Paths).Prepare("hook-launch", Self);
        Ingestor(workspace, provider).Ingest(provider, new StringReader(Payload(workspace)));
        Assert.Equal(saved, new IdentityStore(workspace.Paths).Get(provider + ":hooked", workspace.Root).Palette);
        // A resumed conversation does not adopt the launch palette, so it does not consume it; the launch keeps reserving its color.
        Assert.False(Consumed(workspace, "hook-launch"));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ResetAndRandomNeverReturnTheOwnColorWhenTheWholeSeriesIsInUse(string provider)
    {
        foreach (var extra in new[] { false, true })
        {
            using var workspace = new TestWorkspace();
            var mine = provider + ":mine";
            // Twelve live sessions use the full series, the session under test on Series[0]; the extra case adds a second session on
            // Series[0] so the session under test is alone on Series[1] instead.
            var own = extra ? 1 : 0;
            var identities = new IdentityStore(workspace.Paths);
            var snapshots = new StateSnapshotStore(workspace.Paths);
            var state = new AppState();
            var keys = Enumerable.Range(0, 12).Select(index => index == own ? mine : "claude:s" + index).Append("codex:extra").Take(extra ? 13 : 12).ToArray();
            for (var index = 0; index < keys.Length; index++)
            {
                var series = index == 12 ? 0 : index;
                identities.Get(keys[index], workspace.Root, initialPalette: Palette.Series[series]);
                var parts = keys[index].Split(':');
                state.Sessions[keys[index]] = new SessionState { Key = keys[index], Provider = parts[0], SessionId = parts[1], AttachmentId = "a" + index };
            }
            snapshots.Save(state);
            var current = identities.Get(mine, workspace.Root).Palette;
            Assert.Equal(Palette.Series[own], current);
            Assert.NotEqual(current, identities.SetColor(mine, workspace.Root, "random").Palette);
            // Back on the original color, then reset.
            identities.SetColor(mine, workspace.Root, current.Accent);
            Assert.NotEqual(current.Accent, identities.SetColor(mine, workspace.Root, null, reset: true).Palette.Accent);
        }
    }

    [Fact]
    public void AllocationNeverReturnsAnExcludedColorEvenWhenItIsTheLeastUsed()
    {
        var all = Palette.Series.Select(entry => entry.Accent).ToArray();
        Assert.NotEqual(Palette.Series[0], Palette.Allocate(all, Palette.Series[0].Accent));
        Assert.Equal(Palette.Series[1], Palette.Allocate(all, Palette.Series[0].Accent));
        Assert.Equal(Palette.Series[0], Palette.Allocate([], null));
        Assert.Equal(Palette.Series[1], Palette.Allocate([], Palette.Series[0].Accent));
    }
}
