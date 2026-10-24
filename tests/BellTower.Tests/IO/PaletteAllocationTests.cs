using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

// Shared palette allocation under the identity lock (task 4.2, intake I-8).
public sealed class PaletteAllocationTests
{
    private static readonly Palette First = Palette.FromColor("#d84b4b");
    private static readonly Palette Second = Palette.FromColor("#438bd1");

    private static double MinimumDistance(IReadOnlyList<string> accents) => accents.SelectMany((left, index) => accents.Skip(index + 1)
        .Select(right => Palette.Distance(left, right))).DefaultIfEmpty(double.MaxValue).Min();

    [Fact]
    public async Task ConcurrentCreationFromBothProvidersObservesTheOtherAllocation()
    {
        // Each creation proposes the same candidates in the same order. Without the shared lock both readers would
        // see an empty store at the barrier and both would take the first candidate.
        using var workspace = new TestWorkspace();
        var arrived = 0;
        Func<Palette> Candidates()
        {
            var calls = 0;
            return () =>
            {
                if (calls++ == 0)
                {
                    Interlocked.Increment(ref arrived);
                    SpinWait.SpinUntil(() => Volatile.Read(ref arrived) >= 2, TimeSpan.FromMilliseconds(500));
                }
                return calls % 2 == 1 ? First : Second;
            };
        }
        var claude = Task.Run(() => new IdentityStore(workspace.Paths, Candidates()).Get("claude:a", workspace.Root).Palette);
        var codex = Task.Run(() => new IdentityStore(workspace.Paths, Candidates()).Get("codex:b", workspace.Root).Palette);
        var palettes = await Task.WhenAll(claude, codex);
        Assert.NotEqual(palettes[0].Accent, palettes[1].Accent);
        Assert.True(Palette.Distance(palettes[0].Accent, palettes[1].Accent) > 100);
    }

    [Fact]
    public void ManyConcurrentSessionsOfBothProvidersGetDistinguishableAccents()
    {
        using var workspace = new TestWorkspace();
        var keys = Enumerable.Range(0, 8).Select(index => (index % 2 == 0 ? "claude:" : "codex:") + "s" + index).ToArray();
        var accents = new string[keys.Length];
        Parallel.For(0, keys.Length, new ParallelOptions { MaxDegreeOfParallelism = keys.Length }, index =>
            accents[index] = new IdentityStore(workspace.Paths).Get(keys[index], workspace.Root).Palette.Accent);
        Assert.Equal(keys.Length, accents.Distinct().Count());
        Assert.True(MinimumDistance(accents) >= 20, "minimum accent distance " + MinimumDistance(accents));
        // Resume reads the saved palette back unchanged.
        for (var index = 0; index < keys.Length; index++)
        { Assert.Equal(accents[index], new IdentityStore(workspace.Paths).Get(keys[index], workspace.Root).Palette.Accent); }
    }

    [Fact]
    public void PreparedLaunchPalettesAreReservedAgainstEachOtherAndAgainstNewIdentities()
    {
        using var workspace = new TestWorkspace();
        var shell = ProcessProbe.Identify(Environment.ProcessId)!;
        var launches = new LaunchPresentationStore(workspace.Paths);
        var prepared = Enumerable.Range(0, 4).AsParallel().WithDegreeOfParallelism(4).Select(index => launches.Prepare("attachment-" + index, shell).Palette.Accent).ToArray();
        Assert.Equal(4, prepared.Distinct().Count());
        Assert.True(MinimumDistance(prepared) >= 20);
        var identity = new IdentityStore(workspace.Paths).Get("codex:late", workspace.Root).Palette.Accent;
        Assert.All(prepared, accent => Assert.True(Palette.Distance(accent, identity) >= 20, accent + " vs " + identity));
    }

    [Fact]
    public void ResumeKeepsSavedPalettesAndMetadataEditsNeverRecolor()
    {
        using var workspace = new TestWorkspace();
        var store = new IdentityStore(workspace.Paths);
        var one = store.Get("claude:one", workspace.Root).Palette;
        var two = store.Get("codex:two", workspace.Root).Palette;
        store.Update("claude:one", workspace.Root, value => { value.ManualName = "Name"; value.Change = "change"; return true; });
        store.Update("claude:one", workspace.Root, value => { value.ActiveWork = "work"; return true; });
        Assert.Equal(one, store.Get("claude:one", workspace.Root, initialPalette: two).Palette);
    }

    [Fact]
    public void ExplicitNearColorsAreHonoredAndRandomClearsThePin()
    {
        using var workspace = new TestWorkspace();
        var store = new IdentityStore(workspace.Paths);
        var other = store.SetColor("claude:other", workspace.Root, "#438bd1").Palette;
        var near = store.SetColor("codex:near", workspace.Root, "#438bd2");
        Assert.Equal("#438bd2", near.Palette.Accent);
        Assert.Equal("#438bd2", near.ColorPin);
        Assert.True(Palette.Distance(near.Palette.Accent, other.Accent) < 2, "the user asked for it, so no spreading applies");
        var named = store.SetColor("codex:near", workspace.Root, "Red");
        Assert.Equal("#d84b4b", named.Palette.Accent);
        Assert.Equal("red", named.ColorPin);
        var random = store.SetColor("codex:near", workspace.Root, "random");
        Assert.Null(random.ColorPin);
        Assert.True(Palette.Distance(random.Palette.Accent, other.Accent) >= 20);
        Assert.Throws<ArgumentException>(() => store.SetColor("codex:near", workspace.Root, "chartreuse-ish"));
    }

    [Fact]
    public void ResetClearsPinsChoosesADistinguishablePaletteAndKeepsNativeTitlesAndWork()
    {
        using var workspace = new TestWorkspace();
        var store = new IdentityStore(workspace.Paths);
        var neighbour = store.Get("claude:neighbour", workspace.Root).Palette;
        store.Update("codex:one", workspace.Root, value =>
        {
            value.ManualName = "Mine";
            value.Change = "pinned";
            value.CheckoutPin = workspace.Root;
            value.NativeTitle = "Native user title";
            value.NativeTitleSource = "verified-user-name";
            value.ActiveWork = "work";
            value.Role = "PM";
            return true;
        });
        store.SetColor("codex:one", workspace.Root, neighbour.Accent);
        var reset = store.SetColor("codex:one", workspace.Root, null, reset: true);
        Assert.Null(reset.ManualName);
        Assert.Null(reset.Change);
        Assert.Null(reset.CheckoutPin);
        Assert.Null(reset.ColorPin);
        Assert.True(Palette.Distance(reset.Palette.Accent, neighbour.Accent) >= 20);
        Assert.Equal("Native user title", reset.NativeTitle);
        Assert.Equal("work", reset.ActiveWork);
        Assert.Equal("PM", reset.Role);
        Assert.Equal("Native user title", reset.Name);
    }
}
