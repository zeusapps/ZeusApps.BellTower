using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

// Allocation against the sessions that are live now and unconsumed launch palettes (distinct-session-colors, tasks 2.1 and 2.2).
public sealed class LiveSessionColorAllocationTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly string[] Accents = Palette.Series.Select(entry => entry.Accent).ToArray();

    private static ProcessIdentity Self => ProcessProbe.Identify(Environment.ProcessId)!;

    // Saves each identity with the given palette and lists it in the monitoring state, ended or live.
    private static void Seed(TestWorkspace workspace, params (string Key, Palette Palette, bool Ended)[] sessions)
    {
        var identities = new IdentityStore(workspace.Paths);
        var snapshots = new StateSnapshotStore(workspace.Paths);
        var state = snapshots.Read();
        foreach (var (key, palette, ended) in sessions)
        {
            identities.Get(key, workspace.Root, initialPalette: palette);
            var parts = key.Split(':');
            state.Sessions[key] = new SessionState { Key = key, Provider = parts[0], SessionId = parts[1], AttachmentId = "attachment-" + parts[1], Ended = ended };
        }
        snapshots.Save(state);
    }

    private static (string, Palette, bool) Live(string key, int series) => (key, Palette.Series[series], false);

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void EndedSessionsDoNotBlockColors(string provider)
    {
        using var workspace = new TestWorkspace();
        var sessions = Enumerable.Range(0, 80).Select(index => ((index % 2 == 0 ? "claude:" : "codex:") + "ended" + index, Palette.Series[index % 12], true)).ToList();
        sessions.Add(Live("claude:live-a", 0));
        sessions.Add(Live("codex:live-b", 1));
        Seed(workspace, [.. sessions]);
        var created = new IdentityStore(workspace.Paths).Get(provider + ":third", workspace.Root).Palette;
        Assert.Equal(Palette.Series[2], created);
    }

    [Fact]
    public void TwelveLiveSessionsOfBothProvidersGetTwelveDifferentColorsInSeriesOrder()
    {
        using var workspace = new TestWorkspace();
        var store = new IdentityStore(workspace.Paths);
        var snapshots = new StateSnapshotStore(workspace.Paths);
        var state = new AppState();
        var accents = new List<string>();
        for (var index = 0; index < 12; index++)
        {
            var key = (index % 2 == 0 ? "claude:" : "codex:") + "s" + index;
            accents.Add(store.Get(key, workspace.Root).Palette.Accent);
            var parts = key.Split(':');
            state.Sessions[key] = new SessionState { Key = key, Provider = parts[0], SessionId = parts[1], AttachmentId = "attachment-" + parts[1] };
            snapshots.Save(state);
        }
        Assert.Equal(Accents, accents);
    }

    [Fact]
    public void AThirteenthSessionGetsTheFarthestColorDeterministically()
    {
        // Eleven series colors plus a custom accent very close to the twelfth entry (but not the same): every entry is in use, and the
        // twelfth is the only one with a non-zero distance, so it is the farthest.
        var custom = Palette.Series[11].Accent[..5] + (Convert.ToInt32(Palette.Series[11].Accent[5..], 16) + 3).ToString("x2");
        var results = new List<Palette>();
        for (var run = 0; run < 2; run++)
        {
            using var workspace = new TestWorkspace();
            var sessions = Enumerable.Range(0, 11).Select(index => Live((index % 2 == 0 ? "claude:" : "codex:") + "s" + index, index)).ToList();
            sessions.Add(("codex:custom", Palette.FromColor(custom), false));
            Seed(workspace, [.. sessions]);
            results.Add(new IdentityStore(workspace.Paths).Get("claude:thirteenth", workspace.Root).Palette);
        }
        Assert.Equal(Palette.Series[11], results[0]);
        Assert.Equal(results[0], results[1]);
    }

    [Fact]
    public void AFullSeriesTiesBreakInSeriesOrder()
    {
        using var workspace = new TestWorkspace();
        Seed(workspace, [.. Enumerable.Range(0, 12).Select(index => Live("codex:s" + index, index))]);
        Assert.Equal(Palette.Series[0], new IdentityStore(workspace.Paths).Get("claude:extra", workspace.Root).Palette);
    }

    [Fact]
    public void ALiveSessionWithACustomHexAccentCountsByDistance()
    {
        using var workspace = new TestWorkspace();
        var near = Palette.Series[0].Accent[..5] + (Convert.ToInt32(Palette.Series[0].Accent[5..], 16) + 1).ToString("x2");
        Seed(workspace, ("claude:custom", Palette.FromColor(near), false));
        Assert.Equal(Palette.Series[1], new IdentityStore(workspace.Paths).Get("codex:next", workspace.Root).Palette);
        // A custom accent far from every series entry blocks none of them.
        using var other = new TestWorkspace();
        Seed(other, ("claude:custom", Palette.FromColor("#00ff00"), false));
        Assert.Equal(Palette.Series[0], new IdentityStore(other.Paths).Get("codex:next", other.Root).Palette);
    }

    [Fact]
    public void ConcurrentCreationsFromBothProvidersGetDifferentSeriesColors()
    {
        for (var round = 0; round < 10; round++)
        {
            using var workspace = new TestWorkspace();
            var keys = new[] { "claude:a", "codex:b", "claude:c", "codex:d" };
            var accents = new string[keys.Length];
            Parallel.For(0, keys.Length, new ParallelOptions { MaxDegreeOfParallelism = keys.Length }, index =>
                accents[index] = new IdentityStore(workspace.Paths).Get(keys[index], workspace.Root).Palette.Accent);
            Assert.Equal(keys.Length, accents.Distinct().Count());
            Assert.All(accents, accent => Assert.Contains(accent, Accents));
            Assert.Equal(Accents.Take(keys.Length).Order(), accents.Order());
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void RandomColorAndResetExcludeTheSessionsOwnColorAndPickAnUnusedOne(string provider)
    {
        using var workspace = new TestWorkspace();
        var key = provider + ":mine";
        Seed(workspace, Live("claude:other", 0), Live(key, 1));
        var store = new IdentityStore(workspace.Paths);
        var random = store.SetColor(key, workspace.Root, "random");
        Assert.Null(random.ColorPin);
        Assert.Equal(Palette.Series[2], random.Palette);
        var reset = store.SetColor(key, workspace.Root, null, reset: true);
        Assert.Equal(Palette.Series[1], reset.Palette);
        // Another reset skips the color it holds now.
        Assert.Equal(Palette.Series[2], store.SetColor(key, workspace.Root, null, reset: true).Palette);
        // An explicit color is exact and pinned even when another live session uses nearly the same one.
        var exact = store.SetColor(key, workspace.Root, Palette.Series[0].Accent);
        Assert.Equal(Palette.Series[0].Accent, exact.Palette.Accent);
        Assert.Equal(Palette.Series[0].Accent, exact.ColorPin);
    }

    [Fact]
    public void ManagedAndNativeSessionsOfBothProvidersShareOneAllocationPath()
    {
        using var workspace = new TestWorkspace();
        var launches = new LaunchPresentationStore(workspace.Paths);
        var identities = new IdentityStore(workspace.Paths);
        // Two prepared managed launches (one per provider) reserve their colors before either session registers.
        var claudeLaunch = launches.Prepare("claude-managed", Self).Palette;
        var codexLaunch = launches.Prepare("codex-managed", Self).Palette;
        Assert.Equal(Palette.Series[0], claudeLaunch);
        Assert.Equal(Palette.Series[1], codexLaunch);
        // Native sessions of both providers register in between and take the next free colors.
        var claudeNative = identities.Get("claude:native", workspace.Root).Palette;
        var codexNative = identities.Get("codex:native", workspace.Root).Palette;
        Assert.Equal([Palette.Series[2], Palette.Series[3]], new[] { claudeNative, codexNative });
        // The managed sessions then register and adopt their launch palettes.
        Assert.Equal(claudeLaunch, identities.Get("claude:managed", workspace.Root, initialPalette: launches.Consume("claude-managed", Self)).Palette);
        Assert.Equal(codexLaunch, identities.Get("codex:managed", workspace.Root, initialPalette: launches.Consume("codex-managed", Self)).Palette);
        // A fifth session sees all four live colors, whether the launches were consumed or not.
        Assert.Equal(Palette.Series[4], identities.Get("codex:fifth", workspace.Root).Palette);
        // Saved palettes are unchanged by later refreshes and resume.
        identities.Update("claude:native", workspace.Root, value => { value.ActiveWork = "work"; return true; });
        Assert.Equal(claudeNative, new IdentityStore(workspace.Paths).Get("claude:native", workspace.Root, initialPalette: Palette.Series[9]).Palette);
    }

    private static void WriteLaunch(TestWorkspace workspace, string id, ProcessIdentity shell, Palette palette, TimeSpan age, bool consumed = false)
    {
        var path = Path.Combine(workspace.Paths.Root, "launches", ContentHash.Compute(id) + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, Json.Encode(new LaunchPresentation(id, shell, palette, DateTimeOffset.UtcNow - age) { PaletteConsumed = consumed }));
    }

    private static ProcessIdentity Dead => Self with { StartedUtcTicks = Self.StartedUtcTicks + 1 };

    [Fact]
    public void ConsumedAndExpiredLaunchPalettesDoNotReserveColors()
    {
        using var workspace = new TestWorkspace();
        var launches = new LaunchPresentationStore(workspace.Paths);
        launches.Prepare("consumed", Self);
        Assert.NotNull(launches.Consume("consumed", Self));
        // Older than the validity the launch reader uses: it can no longer be adopted, so it reserves nothing.
        WriteLaunch(workspace, "expired", Self, Palette.Series[1], LaunchPresentationStore.Validity + TimeSpan.FromHours(1));
        Assert.Equal(Palette.Series[0], new IdentityStore(workspace.Paths).Get("claude:one", workspace.Root).Palette);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AnUnconsumedLaunchKeepsItsColorBeyondAnHourWhileItsShellIsLive(string provider)
    {
        using var workspace = new TestWorkspace();
        WriteLaunch(workspace, provider + "-waiting", Self, Palette.Series[0], TimeSpan.FromHours(2));
        Assert.Equal(Palette.Series[1], new IdentityStore(workspace.Paths).Get(provider + ":new", workspace.Root).Palette);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AnUnconsumedLaunchWhoseShellIsGoneReservesNothing(string provider)
    {
        using var workspace = new TestWorkspace();
        WriteLaunch(workspace, provider + "-crashed", Dead, Palette.Series[0], TimeSpan.FromHours(2));
        Assert.Equal(Palette.Series[0], new IdentityStore(workspace.Paths).Get(provider + ":new", workspace.Root).Palette);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AnUnavailableSessionCountsOnlyWhileItWasObservedRecently(string provider)
    {
        foreach (var (observedAgo, expected) in new[] { (TimeSpan.FromHours(2), 0), (TimeSpan.FromMinutes(5), 1) })
        {
            using var workspace = new TestWorkspace();
            var key = provider + ":gone";
            Seed(workspace, Live(key, 0), Live("claude:verified", 2));
            var snapshots = new StateSnapshotStore(workspace.Paths);
            var state = snapshots.Read();
            state.Sessions[key].AttachmentHealth = "unavailable";
            state.Sessions[key].LastObserved = DateTimeOffset.UtcNow - observedAgo;
            // Verified and unverified sessions count whatever their age.
            state.Sessions["claude:verified"].AttachmentHealth = "unverified";
            state.Sessions["claude:verified"].LastObserved = DateTimeOffset.UtcNow.AddDays(-3);
            snapshots.Save(state);
            Assert.Equal(Palette.Series[expected], new IdentityStore(workspace.Paths).Get(provider + ":new", workspace.Root).Palette);
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void FourteenthSessionSpreadsRepeatsThroughTheRealAllocation(string provider)
    {
        using var workspace = new TestWorkspace();
        Seed(workspace, [.. Enumerable.Range(0, 13).Select(index => Live((index % 2 == 0 ? "claude:" : "codex:") + "s" + index, index % 12))]);
        Assert.Equal(Palette.Series[1], new IdentityStore(workspace.Paths).Get(provider + ":fourteenth", workspace.Root).Palette);
    }

    [Fact]
    public void AllocationStaysFarBelowTheIdentityLockTimeoutWithManySavedIdentities()
    {
        using var workspace = new TestWorkspace();
        var store = new IdentityStore(workspace.Paths);
        var snapshots = new StateSnapshotStore(workspace.Paths);
        var state = new AppState();
        for (var index = 0; index < 212; index++)
        {
            var key = (index % 2 == 0 ? "claude:" : "codex:") + "s" + index;
            var ended = index >= 12;
            store.Get(key, workspace.Root, initialPalette: Palette.Series[index % 12]);
            state.Sessions[key] = new SessionState { Key = key, Provider = key.Split(':')[0], SessionId = "s" + index, AttachmentId = "a" + index, Ended = ended };
        }
        snapshots.Save(state);
        // Thousands of older saved identity files that nothing prunes.
        var template = Json.Encode(store.Find("claude:s0")!);
        for (var index = 0; index < 5000; index++)
        {
            var path = Path.Combine(workspace.Paths.Identities, ContentHash.Compute("old:" + index) + ".json");
            File.WriteAllText(path, template);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-10));
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var palette = store.AllocatePalette("codex:new");
        watch.Stop();
        output.WriteLine("AllocatePalette with 200 ended, 12 live and 5000 identity files: " + watch.ElapsedMilliseconds + " ms");
        // Twelve live sessions use the whole series; ended ones and the old files do not matter beyond that.
        Assert.Contains(palette, Palette.Series);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), "allocation took " + watch.Elapsed);
    }

    [Fact]
    public void OldSavedPalettesStayReadableAndUnchanged()
    {
        using var workspace = new TestWorkspace();
        var legacy = new Palette("#1f0d2e", "#a02cc4", "#ffffff");
        var store = new IdentityStore(workspace.Paths);
        store.Get("claude:old", workspace.Root, initialPalette: legacy);
        var path = Path.Combine(workspace.Paths.Identities, ContentHash.Compute("claude:old") + ".json");
        var before = File.ReadAllBytes(path);
        Assert.Equal(legacy, store.Get("claude:old", workspace.Root).Palette);
        store.Get("codex:new", workspace.Root);
        Assert.Equal(legacy, new IdentityStore(workspace.Paths).Find("claude:old")!.Palette);
        Assert.Equal(before, File.ReadAllBytes(path));
    }
}
