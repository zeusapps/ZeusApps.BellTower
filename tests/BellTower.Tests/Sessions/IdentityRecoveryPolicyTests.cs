using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Sessions;

// Fix round 1: generated titles are never user titles, the way back from a native title, fork inheritance,
// history recovery that survives weak evidence and branch invalidation, and legacy Change migration.
// Fixtures only (labelled isolated data); both clients, plus mixed simultaneous sessions.
public sealed class IdentityRecoveryPolicyTests : IDisposable
{
    private readonly TestWorkspace workspace = new();
    private readonly IdentityStore store;
    private readonly IdentityHistoryReader reader;

    public IdentityRecoveryPolicyTests()
    {
        store = new(workspace.Paths);
        reader = new(workspace.Paths);
    }

    public void Dispose() => workspace.Dispose();

    private string Transcript(string name) => Path.Combine(workspace.Root, name + ".jsonl");

    private SessionState Bind(string provider, string id, string path)
    {
        var session = IdentityFixtures.Session(provider, id, workspace.Root);
        store.Update(session.Key, workspace.Root, identity => HistoryState.Bind(identity, path));
        return session;
    }

    private SessionIdentity Get(SessionState session) => store.Get(session.Key, workspace.Root);

    private void Tick(SessionState session, int limit = 60)
    {
        for (var ticks = 0; ticks < limit && reader.Refresh(session); ticks++)
        { }
    }

    // ---- QA-001: generated titles never become NativeTitle, win precedence or pause sync ----

    [Fact]
    public void AGeneratedClaudeTitleRecordIsNeverAUserTitleAndAnExplicitOneIs()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var generated = Transcript("generated");
        IdentityFixtures.Write(generated,
            "{\"type\":\"ai-title\",\"aiTitle\":\"Generated summary title\",\"sessionId\":\"claude-one\"}",
            IdentityFixtures.Claude.Write("claude-one", "w1", workspace.Root, "change-a", 1),
            IdentityFixtures.Claude.Result("claude-one", "w1", 2));
        var claude = Bind("claude", "claude-one", generated);
        Tick(claude);
        var identity = Get(claude);
        Assert.Null(identity.NativeTitle);
        Assert.True(identity.NativeSyncEnabled);
        Assert.Equal("change-a", identity.Name);

        // A real custom-title record that is not Bell Tower's own is the verified explicit native title.
        IdentityFixtures.Append(generated, IdentityFixtures.Claude.Title("claude-one", "Mine"));
        Tick(claude);
        Assert.Equal("Mine", Get(claude).Name);
    }

    [Fact]
    public void GeneratedNamesNeverWinPrecedenceOrPauseSyncInAMixedClaudeAndCodexPair()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var claudePath = Transcript("claude");
        IdentityFixtures.Write(claudePath,
            "{\"type\":\"ai-title\",\"aiTitle\":\"Generated\",\"sessionId\":\"claude-one\"}",
            IdentityFixtures.Claude.Write("claude-one", "w1", workspace.Root, "change-a", 1),
            IdentityFixtures.Claude.Result("claude-one", "w1", 2));
        var claude = Bind("claude", "claude-one", claudePath);
        var codex = IdentityFixtures.Session("codex", "codex-one", workspace.Root);
        store.Update(codex.Key, workspace.Root, identity => { identity.ActiveWork = "change-a"; return true; });
        // A Codex name set outside Bell Tower pauses writes (shipped) but is not promoted to a user title.
        store.Update(codex.Key, workspace.Root, identity => { NativeNamePolicy.Pause(identity, "Generated codex name"); return true; });
        Tick(claude);
        foreach (var session in new[] { claude, codex })
        {
            var identity = Get(session);
            Assert.Null(identity.NativeTitle);
            Assert.Equal("change-a", identity.Name);
        }
        Assert.False(Get(codex).NativeSyncEnabled);
        Assert.True(Get(claude).NativeSyncEnabled);
    }

    // ---- QA-002: fork inherits the parent identity and gets its own palette ----

    [Fact]
    public void ASdkForkInheritsTheParentIdentityOnceWithItsOwnPaletteAndTheForkTitleIsGenerated()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var parent = IdentityFixtures.Session("claude", "PARENT-SESSION", workspace.Root);
        store.Update(parent.Key, workspace.Root, identity =>
        {
            identity.ManualName = "Parent pin";
            identity.Role = "PM";
            ChangeOrigin.Pin(identity, "pinned-change");
            identity.ActiveWork = "change-b";
            identity.WorkSource = "Write";
            identity.SelectionRoot = workspace.Root;
            identity.SelectionAt = DateTimeOffset.UtcNow;
            identity.ColorPin = "red";
            identity.NativeTitle = "Parent native title";
            return true;
        });
        var parentPalette = Get(parent).Palette;
        var path = Transcript("fork");
        File.WriteAllText(path, IdentityFixtures.Load("claude-sdk-0.3.309-fork.jsonl", workspace.Root));
        var fork = Bind("claude", "FORK-SESSION", path);
        Tick(fork);
        var identity = Get(fork);
        Assert.Equal("PARENT-SESSION", identity.ForkParent);
        Assert.Equal("PARENT-SESSION", identity.ForkInheritedFrom);
        Assert.Equal("Parent pin", identity.ManualName);
        // Bell Tower has no session role: a saved legacy value on the parent is neither shown nor inherited.
        Assert.Null(identity.Role);
        Assert.Equal("pinned-change", identity.PinnedChange);
        Assert.Equal("Parent pin", identity.Name);
        // Own distinguishable palette; colour pin, native title and the generated fork title are not inherited.
        Assert.NotEqual(parentPalette.Accent, identity.Palette.Accent);
        Assert.Null(identity.ColorPin);
        Assert.Null(identity.NativeTitle);

        // Inheritance is a one-time copy: later changes to either conversation do not flow again.
        store.Update(fork.Key, workspace.Root, value => { value.ManualName = "Fork's own"; return true; });
        store.Update(parent.Key, workspace.Root, value => { value.ManualName = "Parent renamed"; return true; });
        IdentityFixtures.Append(path, IdentityFixtures.Claude.Chatter("FORK-SESSION", 99, 10));
        Tick(fork);
        Assert.Equal("Fork's own", Get(fork).ManualName);
    }

    [Fact]
    public void AForkCopiesTheParentsWorkEvidenceWhenTheForkHasNoneOfItsOwn()
    {
        IdentityFixtures.Change(workspace.Root, "change-b");
        var parent = IdentityFixtures.Session("claude", "PARENT-SESSION", workspace.Root);
        store.Update(parent.Key, workspace.Root, identity =>
        {
            identity.ActiveWork = "change-b";
            identity.WorkSource = "Write";
            identity.SelectionAt = DateTimeOffset.UtcNow.AddDays(-1);
            return true;
        });
        // A fork transcript without any tool records: only the forkedFrom proof and the generated title.
        var path = Transcript("fork-empty");
        IdentityFixtures.Write(path,
            "{\"type\":\"user\",\"sessionId\":\"FORK-SESSION\",\"message\":{\"role\":\"user\",\"content\":\"x\"},\"forkedFrom\":{\"sessionId\":\"PARENT-SESSION\",\"messageUuid\":\"m\"}}",
            IdentityFixtures.Claude.Title("FORK-SESSION", "Whatever (fork)"));
        var fork = Bind("claude", "FORK-SESSION", path);
        Tick(fork);
        var identity = Get(fork);
        Assert.Equal("change-b", identity.ActiveWork);
        Assert.Equal("fork:Write", identity.WorkSource);
        Assert.Equal("change-b", identity.Name);
        Assert.Null(identity.NativeTitle);
    }

    [Fact]
    public void AnUnverifiedForkShapeFallsBackToAutomaticResolutionWithTheForkTitleIgnored()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        // Interactive --fork-session or /branch shapes carry no forkedFrom proof that Bell Tower has verified.
        var parent = IdentityFixtures.Session("claude", "PARENT-SESSION", workspace.Root);
        store.Update(parent.Key, workspace.Root, identity => { identity.ManualName = "Parent pin"; return true; });
        var path = Transcript("interactive-fork");
        IdentityFixtures.Write(path,
            IdentityFixtures.Claude.Write("INTERACTIVE", "w1", workspace.Root, "change-a", 1),
            IdentityFixtures.Claude.Result("INTERACTIVE", "w1", 2),
            IdentityFixtures.Claude.Title("INTERACTIVE", "Parent pin (fork)"));
        var fork = Bind("claude", "INTERACTIVE", path);
        Tick(fork);
        var identity = Get(fork);
        Assert.Null(identity.ForkParent);
        Assert.Null(identity.ManualName);
        Assert.Null(identity.NativeTitle);
        Assert.Equal("change-a", identity.Name);
    }

    [Fact]
    public void AForkWhoseParentIsNotKnownToBellTowerStillIgnoresTheTitleAndKeepsRetrying()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var path = Transcript("fork-orphan");
        File.WriteAllText(path, IdentityFixtures.Load("claude-sdk-0.3.309-fork.jsonl", workspace.Root));
        var fork = Bind("claude", "FORK-SESSION", path);
        Tick(fork);
        var identity = Get(fork);
        Assert.Equal("PARENT-SESSION", identity.ForkParent);
        Assert.Null(identity.ForkInheritedFrom);
        Assert.Null(identity.NativeTitle);
        Assert.Equal("change-a", identity.Name);
        // Registering the parent later lets the next history pass inherit it.
        var parent = IdentityFixtures.Session("claude", "PARENT-SESSION", workspace.Root);
        store.Update(parent.Key, workspace.Root, value => { value.ManualName = "Late parent pin"; return true; });
        IdentityFixtures.Append(path, IdentityFixtures.Claude.Chatter("FORK-SESSION", 99, 10));
        Tick(fork);
        Assert.Equal("Late parent pin", Get(fork).ManualName);
    }

    // ---- QA-003: a path back from a native user title ----

    [Fact]
    public void UseBellTowerNameReleasesAClaudeNativeTitleAndOnlyANewerDifferentTitleReplacesIt()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var path = Transcript("title");
        IdentityFixtures.Write(path,
            IdentityFixtures.Claude.Title("claude-one", "Native user title"),
            IdentityFixtures.Claude.Write("claude-one", "w1", workspace.Root, "change-a", 1),
            IdentityFixtures.Claude.Result("claude-one", "w1", 2));
        var claude = Bind("claude", "claude-one", path);
        Tick(claude);
        Assert.Equal("Native user title", Get(claude).Name);

        // Reset keeps native explicit user titles (spec); it must not be the only way out.
        store.SetColor(claude.Key, workspace.Root, null, reset: true);
        Assert.Equal("Native user title", Get(claude).Name);

        NativeNameControl.UseBellTowerName(workspace.Paths, claude);
        var released = Get(claude);
        Assert.Null(released.NativeTitle);
        Assert.Equal("Native user title", released.NativeTitleReleased);
        Assert.Equal("change-a", released.Name);

        // The same title is not applied again, not by a later pass and not after the transcript restarts.
        IdentityFixtures.Append(path, IdentityFixtures.Claude.Chatter("claude-one", 5, 10));
        Tick(claude);
        Assert.Equal("change-a", Get(claude).Name);
        var rotated = Transcript("rotated");
        File.Copy(path, rotated);
        store.Update(claude.Key, workspace.Root, identity => HistoryState.Bind(identity, rotated));
        Tick(claude);
        Assert.Equal("change-a", Get(claude).Name);

        // A newer explicit title takes effect again.
        IdentityFixtures.Append(rotated, IdentityFixtures.Claude.Title("claude-one", "A newer title"));
        Tick(claude);
        Assert.Equal("A newer title", Get(claude).Name);
        Assert.Null(Get(claude).NativeTitleReleased);
    }

    [Fact]
    public void UseBellTowerNameWorksForBothClientsInAMixedPair()
    {
        var path = Transcript("mixed");
        IdentityFixtures.Write(path, IdentityFixtures.Claude.Title("claude-one", "Claude native"));
        var claude = Bind("claude", "claude-one", path);
        var codex = IdentityFixtures.Session("codex", "codex-one", workspace.Root);
        store.Update(codex.Key, workspace.Root, identity => { NativeNamePolicy.Pause(identity, "Codex native"); return true; });
        Tick(claude);
        var catalogClaude = Get(claude);
        Assert.Equal("Claude native", catalogClaude.Name);
        Assert.False(Get(codex).NativeSyncEnabled);

        // Neither control needs a live registered session store here: both resolve the same shared policy.
        NativeNameControl.UseBellTowerName(workspace.Paths, claude);
        NativeNameControl.UseBellTowerName(workspace.Paths, codex);
        Assert.Equal(Get(claude).DefaultName, Get(claude).Name);
        Assert.True(Get(codex).NativeSyncEnabled);
        Assert.True(Get(codex).NativeSyncTakeOwnership);
        Assert.Null(Get(claude).NativeTitle);
    }

    // ---- QA-004 + ANALYSIS-001: recovery survives weak evidence and branch invalidation ----

    [Fact]
    public void AWeakNewestSkillWithACustomTitleDoesNotEndTheWalkBeforeOlderStrongWorkBeyondOneChunk()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        var path = Transcript("weak");
        var lines = new List<string> { IdentityFixtures.Claude.Write("claude-one", "w1", workspace.Root, "change-a", 1), IdentityFixtures.Claude.Result("claude-one", "w1", 2) };
        lines.AddRange(Enumerable.Range(0, 12).Select(i => IdentityFixtures.Claude.Chatter("claude-one", 10 + i, 60_000)));
        lines.Add(IdentityFixtures.Claude.Title("claude-one", "My title"));
        lines.Add(IdentityFixtures.Claude.Use("claude-one", "e1", "Skill", new { skill = "explain-change", args = "change-b" }, workspace.Root, 60));
        lines.Add(IdentityFixtures.Claude.Result("claude-one", "e1", 61));
        IdentityFixtures.Write(path, lines.ToArray());
        var claude = Bind("claude", "claude-one", path);
        Assert.True(reader.Refresh(claude));
        Assert.Equal("change-b", Get(claude).ActiveWork);
        Assert.True(Get(claude).WorkWeak);
        Tick(claude);
        var identity = Get(claude);
        Assert.Equal("change-a", identity.ActiveWork);
        Assert.False(identity.WorkWeak);
        Assert.Equal("My title", identity.NativeTitle);
        Assert.Equal(0, identity.HistoryOffset);
    }

    private static string[] Edit(string provider, string session, string id, string root, string change, int at) => provider == "claude"
        ? [IdentityFixtures.Claude.Write(session, id, root, change, at), IdentityFixtures.Claude.Result(session, id, at + 1)]
        : [IdentityFixtures.Codex.Patch(id, root, change, at), IdentityFixtures.Codex.PatchResult(id, at + 1)];

    private static string Chatter(string provider, string session, int at, int bytes) =>
        provider == "claude" ? IdentityFixtures.Claude.Chatter(session, at, bytes) : IdentityFixtures.Codex.Chatter(at, bytes);

    private static DateTimeOffset FixtureTime(int seconds) => DateTimeOffset.Parse(IdentityFixtures.Timestamp(seconds), System.Globalization.CultureInfo.InvariantCulture);

    // ANALYSIS-100 (QA-150): the branch-invalidation floor is a provider-clock time, so it holds however the history
    // cursors move. Shape (a): live work duplicated by a history record the reader keeps re-reading while the tail grows
    // by less than the checkpoint, then a branch switch.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ABranchSwitchAfterLiveWorkAndSmallTailGrowthNeverReselectsPreSwitchHistory(string provider)
    {
        IdentityFixtures.Change(workspace.Root, "old-z");
        IdentityFixtures.Change(workspace.Root, "fresh-c");
        var id = provider + "-one";
        var path = Transcript(provider + "-tail");
        IdentityFixtures.Write(path, Chatter(provider, id, 0, 2000));
        var session = Bind(provider, id, path);
        Tick(session);
        IdentityFixtures.Append(path, [.. Edit(provider, id, "w1", workspace.Root, "old-z", 1), Chatter(provider, id, 3, 30_000)]);
        // Live hook evidence arrived after the record, so the history copy is a duplicate and derives nothing new.
        store.Update(session.Key, workspace.Root, identity =>
        {
            identity.ObservedBranch = "main";
            return WorkPolicy.Apply(identity, new("old-z", workspace.Root, "Write", false, FixtureTime(5)));
        });
        Tick(session);
        Assert.Equal("old-z", Get(session).ActiveWork);

        store.Update(session.Key, workspace.Root, identity => CheckoutPolicy.ApplyBranch(identity, workspace.Root, workspace.Root, "other-branch", FixtureTime(10)));
        Assert.Null(Get(session).ActiveWork);
        Assert.Equal(FixtureTime(10), Get(session).WorkFloorAt);
        Tick(session);
        Assert.Null(Get(session).ActiveWork);

        // Work recorded after the switch is accepted again.
        IdentityFixtures.Append(path, Edit(provider, id, "w3", workspace.Root, "fresh-c", 90));
        Tick(session);
        Assert.Equal("fresh-c", Get(session).ActiveWork);
    }

    // Shape (b): the switch lands before the first history read, and every fresh pass resets the cursors.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ABranchSwitchBeforeTheFirstHistoryReadStillExcludesPreSwitchHistory(string provider)
    {
        IdentityFixtures.Change(workspace.Root, "old-z");
        IdentityFixtures.Change(workspace.Root, "fresh-c");
        var id = provider + "-one";
        var path = Transcript(provider + "-first");
        IdentityFixtures.Write(path, Edit(provider, id, "w1", workspace.Root, "old-z", 1));
        var session = Bind(provider, id, path);
        store.Update(session.Key, workspace.Root, identity =>
        {
            identity.ObservedBranch = "main";
            return WorkPolicy.Apply(identity, new("old-z", workspace.Root, "Write", false, FixtureTime(5)));
        });
        store.Update(session.Key, workspace.Root, identity => CheckoutPolicy.ApplyBranch(identity, workspace.Root, workspace.Root, "other-branch", FixtureTime(10)));
        Assert.Equal(0, Get(session).HistoryLength);
        Tick(session);
        Assert.Null(Get(session).ActiveWork);

        // The floor survives a transcript restart of the same conversation; later work is still accepted.
        var rotated = Transcript(provider + "-rotated");
        File.Copy(path, rotated);
        IdentityFixtures.Append(rotated, Edit(provider, id, "w3", workspace.Root, "fresh-c", 90));
        store.Update(session.Key, workspace.Root, identity => HistoryState.Bind(identity, rotated));
        Assert.Equal(FixtureTime(10), Get(session).WorkFloorAt);
        Tick(session);
        Assert.Equal("fresh-c", Get(session).ActiveWork);
    }

    [Fact]
    public void AnUnparseableHistoryTimestampFallsBelowTheFloorAndTheFloorOnlyMovesForward()
    {
        var identity = new SessionIdentity { Key = "claude:x", Palette = Palette.Create(), ObservedBranch = "main" };
        Assert.True(WorkPolicy.Apply(identity, new("a", "root", "Write", false, FixtureTime(1))));
        WorkPolicy.Clear(identity, WorkClear.Branch, FixtureTime(10));
        WorkPolicy.Clear(identity, WorkClear.Branch, FixtureTime(5));
        Assert.Equal(FixtureTime(10), identity.WorkFloorAt);
        Assert.False(WorkPolicy.Apply(identity, new("b", "root", "history:Write", false, DateTimeOffset.MinValue, 0)));
        Assert.False(WorkPolicy.Apply(identity, new("b", "root", "history:Write", false, FixtureTime(9), 1)));
        Assert.True(WorkPolicy.Apply(identity, new("b", "root", "history:Write", false, FixtureTime(11), 2)));
        // Live evidence is ordered causally by the observer, not by the history floor.
        Assert.True(WorkPolicy.Apply(identity, new("c", "root", "Write", false, FixtureTime(0))));
    }

    [Fact]
    public void ClearingWorkEvidenceGoesThroughOneHelperWithBranchAndHistoryScopes()
    {
        SessionIdentity Seed() => new()
        {
            Key = "claude:x",
            Palette = Palette.Create(),
            ActiveWork = "change-a",
            WorkBranch = "main",
            WorkSource = "history:edit",
            WorkWeak = true,
            HistoryWorkOffset = 5,
            SelectionAt = DateTimeOffset.UtcNow,
            SelectionRoot = "root",
            HistoryLength = 1234,
            WorkFloorAt = FixtureTime(3)
        };

        var branch = Seed();
        WorkPolicy.Clear(branch, WorkClear.Branch, FixtureTime(20));
        Assert.Null(branch.ActiveWork);
        Assert.Null(branch.WorkBranch);
        Assert.Null(branch.WorkSource);
        Assert.False(branch.WorkWeak);
        Assert.Equal(-1, branch.HistoryWorkOffset);
        Assert.Null(branch.SelectionAt);
        Assert.Equal("root", branch.SelectionRoot);
        Assert.Equal(FixtureTime(20), branch.WorkFloorAt);

        // A transcript restart clears the cursors and the root but keeps the conversation's floor.
        var history = Seed();
        WorkPolicy.Clear(history, WorkClear.History);
        Assert.Null(history.ActiveWork);
        Assert.Null(history.SelectionRoot);
        Assert.Equal(FixtureTime(3), history.WorkFloorAt);
    }

    // ---- QA-151: an ignored title has no effect ----

    [Fact]
    public void AnIgnoredForkTitleNeverClearsAnEarlierValidNativeTitleOrMovesTheCursor()
    {
        var path = Transcript("ignored-title");
        IdentityFixtures.Write(path, IdentityFixtures.Claude.Title("claude-one", "Mine"), IdentityFixtures.Claude.Title("claude-one", "Mine (fork)"));
        var claude = Bind("claude", "claude-one", path);
        Tick(claude);
        var identity = Get(claude);
        Assert.Equal("Mine", identity.NativeTitle);
        Assert.Equal(HistoryState.ClaudeTitleSource, identity.NativeTitleSource);

        // A later ignored title after the valid one has been recorded changes nothing either.
        IdentityFixtures.Append(path, IdentityFixtures.Claude.Title("claude-one", "Another (fork)"));
        Tick(claude);
        Assert.Equal("Mine", Get(claude).NativeTitle);
        Assert.Equal("Mine", Get(claude).Name);

        // An ignored title that is alone leaves no title, as before.
        var only = Transcript("only-fork");
        IdentityFixtures.Write(only, IdentityFixtures.Claude.Title("claude-two", "Solo (fork)"));
        var second = Bind("claude", "claude-two", only);
        Tick(second);
        Assert.Null(Get(second).NativeTitle);
    }

    // ---- ANALYSIS-002: legacy automatically selected Change ----

    private string IdentityPath(string key) => Path.Combine(workspace.Paths.Identities, ContentHash.Compute(key) + ".json");

    // Baseline (0.1.8 / d307c34) identity shape: no ChangeSource marker, automatic selections carry SelectionRoot and
    // SelectionEvent, and an unknown future field must survive every rewrite.
    private void WriteBaseline(string key, string change, bool automatic, string? marker = null, string? markerValue = null)
    {
        var node = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["defaultName"] = "repo",
            ["change"] = change,
            ["role"] = "PM",
            ["palette"] = new { background = "#101c27", accent = "#4b91bb", foreground = "#ffffff" },
            ["selectionEvidence"] = Array.Empty<object>(),
            ["futureField"] = new { keep = "me" }
        };
        if (automatic)
        {
            node["selectionRoot"] = workspace.Root;
            node["selectionEvent"] = "event-1";
            node["selectionAt"] = "2026-10-01T10:00:00Z";
        }
        if (marker is not null)
        {
            node["changeSource"] = marker;
            node["changeSourceValue"] = markerValue;
        }
        Directory.CreateDirectory(workspace.Paths.Identities);
        AtomicFile.Write(IdentityPath(key), JsonSerializer.Serialize(node));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ALegacyAutomaticChangeYieldsToNewerVerifiedWorkWhileUserAndLaunchValuesStayPins(string provider)
    {
        IdentityFixtures.Change(workspace.Root, "old-auto");
        IdentityFixtures.Change(workspace.Root, "new-work");
        var auto = provider + ":auto";
        var pinned = provider + ":pinned";
        WriteBaseline(auto, "old-auto", automatic: true);
        WriteBaseline(pinned, "mine", automatic: false);

        var legacy = store.Get(auto, workspace.Root);
        Assert.Equal(ChangeOrigin.LegacySelection, legacy.ChangeSource);
        Assert.Null(legacy.PinnedChange);
        Assert.Equal("old-auto", legacy.Subject);
        Assert.Equal("old-auto", legacy.Name);
        var pin = store.Get(pinned, workspace.Root);
        Assert.Equal(ChangeOrigin.User, pin.ChangeSource);
        Assert.Equal("mine", pin.PinnedChange);

        // Newer verified work replaces the legacy selection; the pin ignores it.
        foreach (var key in new[] { auto, pinned })
        {
            store.Update(key, workspace.Root, identity => WorkPolicy.Apply(identity, new("new-work", workspace.Root, "Write", false, DateTimeOffset.UtcNow)));
        }
        Assert.Equal("new-work", store.Get(auto, workspace.Root).Subject);
        Assert.Equal("mine", store.Get(pinned, workspace.Root).Subject);

        // Unknown fields survive the rewrite and the classification is persisted.
        var text = File.ReadAllText(IdentityPath(auto));
        Assert.Contains("futureField", text, StringComparison.Ordinal);
        // Strong newer work retired the legacy selection; the marker is persisted and the Change stays for rollback.
        Assert.Contains("legacy-superseded", text, StringComparison.Ordinal);
        Assert.Contains("old-auto", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void HistoryOlderThanTheLegacySelectionDoesNotReplaceItButNewerHistoryDoes(string provider)
    {
        IdentityFixtures.Change(workspace.Root, "old-auto");
        IdentityFixtures.Change(workspace.Root, "older-work");
        IdentityFixtures.Change(workspace.Root, "newer-work");
        var key = provider + ":one";
        WriteBaseline(key, "old-auto", automatic: true);
        var path = Transcript("legacy-" + provider);
        // The legacy selection is at 2026-10-01T10:00Z; fixture timestamps are 2026-10-23, so build both sides explicitly.
        var older = provider == "claude"
            ? new[] { IdentityFixtures.Claude.Write("one", "w1", workspace.Root, "older-work", 1), IdentityFixtures.Claude.Result("one", "w1", 2) }
            : new[] { IdentityFixtures.Codex.Patch("p1", workspace.Root, "older-work", 1), IdentityFixtures.Codex.PatchResult("p1", 2) };
        IdentityFixtures.Write(path, older.Select(line => line.Replace("2026-10-23T12:00", "2026-09-23T12:00")).ToArray());
        var session = Bind(provider, "one", path);
        Tick(session);
        Assert.Null(Get(session).ActiveWork);
        Assert.Equal("old-auto", Get(session).Subject);

        var newer = provider == "claude"
            ? new[] { IdentityFixtures.Claude.Write("one", "w2", workspace.Root, "newer-work", 5), IdentityFixtures.Claude.Result("one", "w2", 6) }
            : new[] { IdentityFixtures.Codex.Patch("p2", workspace.Root, "newer-work", 5), IdentityFixtures.Codex.PatchResult("p2", 6) };
        IdentityFixtures.Append(path, newer);
        Tick(session);
        Assert.Equal("newer-work", Get(session).Subject);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AnAutomaticChangeWrittenByTheOlderBuildAfterARollbackIsClassifiedAgain(string provider)
    {
        var key = provider + ":roll";
        // The new build pinned "mine" (marker bound to that value).
        WriteBaseline(key, "mine", automatic: false, marker: ChangeOrigin.User, markerValue: "mine");
        Assert.Equal("mine", store.Get(key, workspace.Root).PinnedChange);

        // Rolled back: 0.1.8 selects "auto-from-inspection" automatically (SelectionRoot and SelectionEvent set) and the
        // user marker is left behind by the older build; rolling forward must not turn it into a pin.
        WriteBaseline(key, "auto-from-inspection", automatic: true, marker: ChangeOrigin.User, markerValue: "mine");
        var forward = store.Get(key, workspace.Root);
        Assert.Equal(ChangeOrigin.LegacySelection, ChangeOrigin.Of(forward));
        Assert.Null(forward.PinnedChange);

        // A rename by the older build (user association: SelectionRoot cleared, SelectionEvent kept) stays a pin.
        var node = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(IdentityPath(key)))!;
        var renamed = node.Where(pair => pair.Key != "selectionRoot").ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
        renamed["change"] = "renamed-by-user";
        AtomicFile.Write(IdentityPath(key), JsonSerializer.Serialize(renamed));
        Assert.Equal("renamed-by-user", store.Get(key, workspace.Root).PinnedChange);
    }

    [Fact]
    public void AnExplicitAssociationReplacesALegacySelectionAndALaunchChangeIsAPin()
    {
        WriteBaseline("claude:auto", "old-auto", automatic: true);
        Assert.Null(store.Get("claude:auto", workspace.Root).PinnedChange);
        store.Update("claude:auto", workspace.Root, identity => { ChangeOrigin.Pin(identity, "chosen"); return true; });
        Assert.Equal("chosen", store.Get("claude:auto", workspace.Root).PinnedChange);

        var launched = store.Get("codex:launch", workspace.Root, initialChange: "launch-change");
        Assert.Equal(ChangeOrigin.Launch, launched.ChangeSource);
        Assert.Equal("launch-change", launched.PinnedChange);
    }

    // ---- ANALYSIS-101: supersession of a legacy selection is one-way ----

    private void AssertFallsBackToAutomatic(SessionIdentity identity, string retired)
    {
        Assert.Null(identity.WorkSubject);
        Assert.NotEqual(retired, identity.Subject);
        Assert.Equal(identity.AutomaticSubject ?? identity.DefaultName, identity.Subject);
        Assert.Equal("fallback", new IdentityPresentationStore(workspace.Paths).Save(identity, new GitContext("main")) ? ReadProvenance(identity) : "fallback");
    }

    private string ReadProvenance(SessionIdentity identity) => new IdentityPresentationStore(workspace.Paths).Read(identity.Key)!.Provenance;

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ASupersededLegacyChangeStaysOnDiskButNeverReturnsAfterBranchInvalidation(string provider)
    {
        IdentityFixtures.Change(workspace.Root, "legacy-x");
        IdentityFixtures.Change(workspace.Root, "change-a");
        var key = provider + ":one";
        WriteBaseline(key, "legacy-x", automatic: true);
        Assert.Equal("legacy-x", store.Get(key, workspace.Root).WorkSubject);

        store.Update(key, workspace.Root, identity =>
        {
            identity.ObservedBranch = "main";
            return WorkPolicy.Apply(identity, new("change-a", workspace.Root, "Write", false, DateTimeOffset.UtcNow));
        });
        var superseded = store.Get(key, workspace.Root);
        Assert.Equal("change-a", superseded.WorkSubject);
        Assert.Equal(ChangeOrigin.Superseded, superseded.ChangeSource);
        Assert.Equal("legacy-x", superseded.Change);
        Assert.Null(superseded.PinnedChange);

        store.Update(key, workspace.Root, identity => CheckoutPolicy.ApplyBranch(identity, workspace.Root, workspace.Root, "other-branch", DateTimeOffset.UtcNow));
        var cleared = store.Get(key, workspace.Root);
        Assert.Null(cleared.ActiveWork);
        Assert.Equal("legacy-x", cleared.Change);
        AssertFallsBackToAutomatic(cleared, "legacy-x");

        // The retired value is not weak-fillable either; rollback keeps seeing the stored Change, and unknown fields survive.
        var text = File.ReadAllText(IdentityPath(key));
        Assert.Contains("legacy-superseded", text, StringComparison.Ordinal);
        Assert.Contains("futureField", text, StringComparison.Ordinal);
        Assert.True(WorkPolicy.Apply(cleared, new("weak-w", workspace.Root, "status", true, DateTimeOffset.UtcNow)));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ASupersededLegacyChangeDoesNotReturnAfterATranscriptRestartOrInheritToAFork(string provider)
    {
        IdentityFixtures.Change(workspace.Root, "legacy-x");
        IdentityFixtures.Change(workspace.Root, "newer-work");
        var key = provider + ":one";
        WriteBaseline(key, "legacy-x", automatic: true);
        var path = Transcript("supersede-" + provider);
        IdentityFixtures.Write(path, Edit(provider, "one", "w1", workspace.Root, "newer-work", 5));
        var session = Bind(provider, "one", path);
        Tick(session);
        var superseded = Get(session);
        Assert.Equal("newer-work", superseded.WorkSubject);
        Assert.Equal(ChangeOrigin.Superseded, superseded.ChangeSource);

        // A fork never inherits the retired value.
        var child = new SessionIdentity { Key = provider + ":child", Palette = Palette.Create() };
        IdentityInheritance.FromParent(child, superseded, "one");
        Assert.Null(child.Change);

        var restarted = Transcript("restart-" + provider);
        File.WriteAllText(restarted, "");
        store.Update(key, workspace.Root, identity => HistoryState.Bind(identity, restarted));
        var after = Get(session);
        Assert.Null(after.ActiveWork);
        Assert.Equal("legacy-x", after.Change);
        AssertFallsBackToAutomatic(after, "legacy-x");
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ALegacyValueRewrittenByTheOlderBuildAfterSupersessionIsClassifiedAgain(string provider)
    {
        var key = provider + ":roll";
        WriteBaseline(key, "next-auto", automatic: true, marker: ChangeOrigin.Superseded, markerValue: "old-auto");
        var reread = store.Get(key, workspace.Root);
        Assert.Equal(ChangeOrigin.LegacySelection, ChangeOrigin.Of(reread));
        Assert.Equal("next-auto", reread.WorkSubject);
        // Weak evidence, like any association, is held by a live legacy value but not by a retired one.
        WriteBaseline(key, "old-auto", automatic: true, marker: ChangeOrigin.Superseded, markerValue: "old-auto");
        Assert.Null(store.Get(key, workspace.Root).WorkSubject);
    }

    // ---- Codex fork inheritance (parity): session_meta.forked_from_id, rust-v0.164.0 sources; no live capture ----

    private void RegisterCodexParent()
    {
        var parent = IdentityFixtures.Session("codex", "codex-parent-thread", workspace.Root);
        store.Update(parent.Key, workspace.Root, identity =>
        {
            identity.ManualName = "Parent pin";
            ChangeOrigin.Pin(identity, "pinned-change");
            identity.ColorPin = "red";
            return true;
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void ACodexForkInheritsTheParentIdentityOnceFromItsOwnSessionMetaWithItsOwnPalette(int chatterRecords)
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        RegisterCodexParent();
        var parentPalette = store.Get("codex:codex-parent-thread", workspace.Root).Palette;
        var path = Transcript("codex-fork");
        File.WriteAllText(path, IdentityFixtures.Load("codex-rollout-0.164.0-fork.jsonl", workspace.Root));
        // A long fork transcript pushes the first line out of the newest history window.
        IdentityFixtures.Append(path, Enumerable.Range(0, chatterRecords).Select(i => IdentityFixtures.Codex.Chatter(10 + i, 60_000)).ToArray());
        var fork = Bind("codex", "codex-fork-thread", path);
        Tick(fork);
        var identity = Get(fork);
        Assert.Equal("codex-parent-thread", identity.ForkParent);
        Assert.Equal("codex-parent-thread", identity.ForkInheritedFrom);
        Assert.Equal("Parent pin", identity.ManualName);
        Assert.Equal("pinned-change", identity.PinnedChange);
        Assert.Equal("Parent pin", identity.Name);
        Assert.NotEqual(parentPalette.Accent, identity.Palette.Accent);
        Assert.Null(identity.ColorPin);

        // One-time copy: a later parent edit and further passes do not change the fork.
        store.Update("codex:codex-parent-thread", workspace.Root, value => { value.ManualName = "Parent renamed"; return true; });
        IdentityFixtures.Append(path, IdentityFixtures.Codex.Chatter(99, 10));
        Tick(fork);
        Assert.Equal("Parent pin", Get(fork).ManualName);
    }

    [Fact]
    public void ACodexForkWithoutAKnownParentRetriesAndAnUnrelatedSessionMetaIsNotForkEvidence()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var path = Transcript("codex-fork-orphan");
        File.WriteAllText(path, IdentityFixtures.Load("codex-rollout-0.164.0-fork.jsonl", workspace.Root));
        var fork = Bind("codex", "codex-fork-thread", path);
        Tick(fork);
        Assert.Equal("codex-parent-thread", Get(fork).ForkParent);
        Assert.Null(Get(fork).ForkInheritedFrom);
        Assert.Equal("change-a", Get(fork).Name);
        RegisterCodexParent();
        IdentityFixtures.Append(path, IdentityFixtures.Codex.Chatter(99, 10));
        Tick(fork);
        Assert.Equal("Parent pin", Get(fork).Name);

        // A conversation whose own session_meta has no forked_from_id (or only another thread's) has no parent.
        var plain = Transcript("codex-plain");
        IdentityFixtures.Write(plain,
            "{\"timestamp\":\"2026-10-23T12:00:00.000Z\",\"type\":\"session_meta\",\"payload\":{\"id\":\"plain-thread\",\"parent_thread_id\":\"codex-parent-thread\",\"source\":\"cli\"}}",
            "{\"timestamp\":\"2026-10-23T12:00:01.000Z\",\"type\":\"session_meta\",\"payload\":{\"id\":\"other-thread\",\"forked_from_id\":\"codex-parent-thread\"}}");
        var second = Bind("codex", "plain-thread", plain);
        Tick(second);
        Assert.Null(Get(second).ForkParent);
        Assert.Null(Get(second).ManualName);
    }

    [Fact]
    public void AMixedClaudeAndCodexForkPairInheritsIndependently()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        RegisterCodexParent();
        var claudeParent = IdentityFixtures.Session("claude", "PARENT-SESSION", workspace.Root);
        store.Update(claudeParent.Key, workspace.Root, identity => { identity.ManualName = "Claude parent"; return true; });
        var claudePath = Transcript("mixed-claude-fork");
        File.WriteAllText(claudePath, IdentityFixtures.Load("claude-sdk-0.3.309-fork.jsonl", workspace.Root));
        var codexPath = Transcript("mixed-codex-fork");
        File.WriteAllText(codexPath, IdentityFixtures.Load("codex-rollout-0.164.0-fork.jsonl", workspace.Root));
        var claude = Bind("claude", "FORK-SESSION", claudePath);
        var codex = Bind("codex", "codex-fork-thread", codexPath);
        Tick(claude);
        Tick(codex);
        Assert.Equal("Claude parent", Get(claude).Name);
        Assert.Equal("Parent pin", Get(codex).Name);
        Assert.Equal("PARENT-SESSION", Get(claude).ForkInheritedFrom);
        Assert.Equal("codex-parent-thread", Get(codex).ForkInheritedFrom);
    }

    // ---- ANALYSIS-150: a proven fork never re-derives the parent's branch-invalidated work ----

    private SessionState InvalidatedFork(string provider, string change = "change-a")
    {
        IdentityFixtures.Change(workspace.Root, change);
        var parentId = provider == "claude" ? "PARENT-SESSION" : "codex-parent-thread";
        var parent = IdentityFixtures.Session(provider, parentId, workspace.Root);
        store.Update(parent.Key, workspace.Root, identity =>
        {
            identity.ObservedBranch = "main";
            WorkPolicy.Apply(identity, new(change, workspace.Root, "Write", false, FixtureTime(5)));
            CheckoutPolicy.ApplyBranch(identity, workspace.Root, workspace.Root, "feature", FixtureTime(10));
            return true;
        });
        Assert.Null(store.Get(parent.Key, workspace.Root).ActiveWork);
        var path = Transcript(provider + "-invalidated-fork");
        File.WriteAllText(path, IdentityFixtures.Load(provider == "claude" ? "claude-sdk-0.3.309-fork.jsonl" : "codex-rollout-0.164.0-fork-boundary.jsonl", workspace.Root));
        return Bind(provider, provider == "claude" ? "FORK-SESSION" : "codex-fork-thread", path);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AForkOfAConversationWhoseWorkWasInvalidatedByABranchSwitchDoesNotRederiveThatWork(string provider)
    {
        var fork = InvalidatedFork(provider);
        Tick(fork);
        var identity = Get(fork);
        Assert.NotNull(identity.ForkInheritedFrom);
        Assert.Null(identity.ActiveWork);
        Assert.Equal(FixtureTime(10), identity.WorkFloorAt);

        // Work the fork itself does afterwards is accepted.
        IdentityFixtures.Change(workspace.Root, "change-b");
        IdentityFixtures.Append(Transcript(provider + "-invalidated-fork"), Edit(provider, fork.SessionId, "w-own", workspace.Root, "change-b", 60));
        Tick(fork);
        Assert.Equal("change-b", Get(fork).ActiveWork);
    }

    [Fact]
    public void AMixedClaudeAndCodexPairOfInvalidatedForksStaysIndependentOfTheParentsWork()
    {
        var claude = InvalidatedFork("claude");
        var codex = InvalidatedFork("codex");
        Tick(claude);
        Tick(codex);
        Assert.Null(Get(claude).ActiveWork);
        Assert.Null(Get(codex).ActiveWork);
        Assert.Equal("PARENT-SESSION", Get(claude).ForkInheritedFrom);
        Assert.Equal("codex-parent-thread", Get(codex).ForkInheritedFrom);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AForkInheritsTheParentsLiveWorkAndRecordsThatForkBoundaryAndFloorNeverLower(string provider)
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var parent = IdentityFixtures.Session(provider, ParentId(provider), workspace.Root);
        store.Update(parent.Key, workspace.Root, identity =>
        {
            identity.ObservedBranch = "main";
            return WorkPolicy.Apply(identity, new("change-a", workspace.Root, "Write", false, FixtureTime(5)));
        });
        var path = Transcript(provider + "-boundary-ordinary");
        File.WriteAllText(path, IdentityFixtures.Load(ForkFixture(provider), workspace.Root));
        var fork = Bind(provider, ForkId(provider), path);
        Tick(fork);
        var identity = Get(fork);
        Assert.Equal("change-a", identity.ActiveWork);
        Assert.Null(identity.WorkFloorAt);
        if (provider == "codex")
        { Assert.True(identity.ForkBoundaryOffset > 0); }

        var child = new SessionIdentity { Key = "codex:floor", Palette = Palette.Create(), WorkFloorAt = FixtureTime(30) };
        IdentityInheritance.FromParent(child, new SessionIdentity { Key = "codex:p", Palette = Palette.Create(), WorkFloorAt = FixtureTime(10) }, "p");
        Assert.Equal(FixtureTime(30), child.WorkFloorAt);
    }

    // ---- ANALYSIS-200 / QA-250: the Codex boundary exclusion needs a known invalidation, and is retracted when found late ----

    private static string ForkFixture(string provider) => provider == "claude" ? "claude-sdk-0.3.309-fork.jsonl" : "codex-rollout-0.164.0-fork-boundary.jsonl";

    private static string ForkId(string provider) => provider == "claude" ? "FORK-SESSION" : "codex-fork-thread";

    private static string ParentId(string provider) => provider == "claude" ? "PARENT-SESSION" : "codex-parent-thread";

    private string[] ForkLines(string provider) => IdentityFixtures.Load(ForkFixture(provider), workspace.Root).Split((char)10, StringSplitOptions.RemoveEmptyEntries);

    // The parent's identity exists but its history was never walked: no work, no floor.
    private void RegisterUnreadParent(string provider) =>
        store.Update(IdentityFixtures.Session(provider, ParentId(provider), workspace.Root).Key, workspace.Root, _ => true);

    private void RegisterInvalidatedParent(string provider)
    {
        var parent = IdentityFixtures.Session(provider, ParentId(provider), workspace.Root);
        store.Update(parent.Key, workspace.Root, identity =>
        {
            identity.ObservedBranch = "main";
            WorkPolicy.Apply(identity, new("change-a", workspace.Root, "Write", false, FixtureTime(5)));
            CheckoutPolicy.ApplyBranch(identity, workspace.Root, workspace.Root, "feature", FixtureTime(10));
            return true;
        });
    }

    [Theory]
    [InlineData("claude", false)]
    [InlineData("codex", false)]
    [InlineData("claude", true)]
    [InlineData("codex", true)]
    public void AForkWithNoKnownInvalidationUsesItsCopiedWorkForBothClients(string provider, bool parentIdentityExistsUnread)
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        if (parentIdentityExistsUnread)
        { RegisterUnreadParent(provider); }
        var path = Transcript(provider + "-uninvalidated-fork");
        File.WriteAllText(path, IdentityFixtures.Load(ForkFixture(provider), workspace.Root));
        var fork = Bind(provider, ForkId(provider), path);
        Tick(fork);
        var identity = Get(fork);
        Assert.Equal("change-a", identity.ActiveWork);
        Assert.StartsWith("history:", identity.WorkSource);
        Assert.Null(identity.WorkFloorAt);
        Assert.Equal("change-a", identity.Name);
    }

    [Fact]
    public void AMixedClaudeAndCodexPairOfForksWithNoKnownInvalidationBothResolveTheCopiedWork()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var forks = new[] { "claude", "codex" }.Select(provider =>
        {
            var path = Transcript(provider + "-mixed-uninvalidated");
            File.WriteAllText(path, IdentityFixtures.Load(ForkFixture(provider), workspace.Root));
            return Bind(provider, ForkId(provider), path);
        }).ToArray();
        foreach (var fork in forks)
        { Tick(fork); }
        Assert.All(forks, fork => Assert.Equal("change-a", Get(fork).ActiveWork));
    }

    // Copied work becomes visible to a read window before the fork's own boundary exists (file growth between ticks).
    private SessionState GrowingFork(string provider, bool large, out string path)
    {
        RegisterInvalidatedParent(provider);
        var lines = ForkLines(provider);
        var prefix = provider == "claude" ? 3 : 4;
        path = Transcript(provider + (large ? "-growth-large" : "-growth"));
        var filler = large ? Enumerable.Range(0, 6).Select(i => Chatter(provider, ForkId(provider), 40 + i, 60_000)).ToArray() : [];
        IdentityFixtures.Write(path, [.. lines.Take(prefix), .. filler]);
        var fork = Bind(provider, ForkId(provider), path);
        Tick(fork);
        return fork;
    }

    [Theory]
    [InlineData("claude", false)]
    [InlineData("codex", false)]
    [InlineData("claude", true)]
    [InlineData("codex", true)]
    public void AForkWhoseBoundaryAppearsAfterItsCopiedPrefixWasReadEndsWithNoPreSwitchWork(string provider, bool large)
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var fork = GrowingFork(provider, large, out var path);
        IdentityFixtures.Append(path, ForkLines(provider).Skip(provider == "claude" ? 3 : 4).ToArray());
        Tick(fork);
        var identity = Get(fork);
        Assert.Null(identity.ActiveWork);
        Assert.Null(identity.WorkSource);
        Assert.Equal(FixtureTime(10), identity.WorkFloorAt);
        if (provider == "codex")
        { Assert.True(identity.ForkBoundaryOffset > 0); }

        // Work the fork does afterwards is accepted.
        IdentityFixtures.Change(workspace.Root, "change-b");
        IdentityFixtures.Append(path, Edit(provider, ForkId(provider), "w-own", workspace.Root, "change-b", 90));
        Tick(fork);
        Assert.Equal("change-b", Get(fork).ActiveWork);
    }

    [Fact]
    public void AMixedPairOfGrowingForksRetractsOnlyTheCodexCopiedWorkAndKeepsLiveWork()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var claude = GrowingFork("claude", false, out var claudePath);
        var codex = GrowingFork("codex", false, out var codexPath);
        // Live work on the fork must survive the late boundary.
        IdentityFixtures.Append(claudePath, ForkLines("claude").Skip(3).ToArray());
        IdentityFixtures.Append(codexPath, ForkLines("codex").Skip(4).ToArray());
        Tick(claude);
        Tick(codex);
        Assert.Null(Get(claude).ActiveWork);
        Assert.Null(Get(codex).ActiveWork);
        store.Update(codex.Key, workspace.Root, identity => WorkPolicy.Apply(identity, new("change-a", workspace.Root, "Write", false, FixtureTime(95))));
        IdentityFixtures.Append(codexPath, IdentityFixtures.Codex.Chatter(96, 10));
        Tick(codex);
        Assert.Equal("change-a", Get(codex).ActiveWork);
        Assert.Equal("Write", Get(codex).WorkSource);
    }

    // ---- ANALYSIS-250 / QA-300: one precedence rule for inherited parent work and copied history on both clients ----

    // Older than every copied record of both fork fixtures (Claude copies keep their 2026-10-23T11:09 stamps).
    private static readonly DateTimeOffset BeforeTheCopies = DateTimeOffset.Parse("2026-10-23T11:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private void RegisterLiveParent(string provider, string change, int at, bool weak)
    {
        var parent = IdentityFixtures.Session(provider, ParentId(provider), workspace.Root);
        store.Update(parent.Key, workspace.Root, identity =>
        {
            identity.ObservedBranch = "main";
            return WorkPolicy.Apply(identity, new(change, workspace.Root, weak ? "skill" : "Write", weak, at < 0 ? BeforeTheCopies : FixtureTime(at)));
        });
    }

    [Theory]
    [InlineData("claude", false)]
    [InlineData("codex", false)]
    [InlineData("claude", true)]
    [InlineData("codex", true)]
    public void AnUnreadParentWithOlderLiveWorkOnAnotherChangeYieldsToTheNewerCopiedHistory(string provider, bool weak)
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        RegisterLiveParent(provider, "change-b", weak ? 30 : -1, weak);
        var path = Transcript(provider + "-stale-parent");
        File.WriteAllText(path, IdentityFixtures.Load(ForkFixture(provider), workspace.Root));
        var fork = Bind(provider, ForkId(provider), path);
        Tick(fork);
        Assert.Equal("change-a", Get(fork).ActiveWork);
    }

    [Fact]
    public void AMixedPairOfForksOfStaleAndWeakParentsResolvesTheCopiedHistoryForBothClients()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        RegisterLiveParent("claude", "change-b", -1, false);
        RegisterLiveParent("codex", "change-b", 30, true);
        var forks = new[] { "claude", "codex" }.Select(provider =>
        {
            var path = Transcript(provider + "-mixed-stale");
            File.WriteAllText(path, IdentityFixtures.Load(ForkFixture(provider), workspace.Root));
            return Bind(provider, ForkId(provider), path);
        }).ToArray();
        foreach (var fork in forks)
        { Tick(fork); }
        Assert.All(forks, fork => Assert.Equal("change-a", Get(fork).ActiveWork));
    }

    // QA-300: the copied prefix is read before the boundary exists and the parent's live work (change-b at t=5) differs from the
    // copied history, no floor. The outcome must not depend on read timing: it equals the outcome after the boundary arrives, per client.
    // Claude's copies keep their original (older) stamps, so the parent's newer live work stays. Codex copies are restamped with the
    // fork time, so they are newer and take precedence under the shared WorkPolicy rule. This window (parent work newer than the original
    // stamps, older than the fork time, no copied twin) is unverified, unfinished parity, not a proven restriction; see docs/compatibility.md.
    [Theory]
    [InlineData("claude", false)]
    [InlineData("codex", false)]
    [InlineData("claude", true)]
    [InlineData("codex", true)]
    public void ALateBoundaryDoesNotChangeTheOutcomeWhenTheParentsLiveWorkDiffersFromTheCopiedHistory(string provider, bool large)
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        RegisterLiveParent(provider, "change-b", 5, false);
        var expected = provider == "claude" ? "change-b" : "change-a";
        var lines = ForkLines(provider);
        var prefix = provider == "claude" ? 3 : 4;
        var path = Transcript(provider + (large ? "-late-large" : "-late"));
        var filler = large ? Enumerable.Range(0, 6).Select(i => Chatter(provider, ForkId(provider), 40 + i, 60_000)).ToArray() : [];
        IdentityFixtures.Write(path, [.. lines.Take(prefix), .. filler]);
        var fork = Bind(provider, ForkId(provider), path);
        Tick(fork);
        Assert.Equal(expected, Get(fork).ActiveWork);
        IdentityFixtures.Append(path, lines.Skip(prefix).ToArray());
        Tick(fork);
        Assert.Equal(expected, Get(fork).ActiveWork);

    }

    // Residual shape: the parent kept working after the fork, on another change, newer than every copied record.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ParentLiveWorkNewerThanEveryCopiedRecordOnAnotherChangeIsKeptByBothClients(string provider)
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        RegisterLiveParent(provider, "change-b", 90, false);
        var path = Transcript(provider + "-newer-parent");
        File.WriteAllText(path, IdentityFixtures.Load(ForkFixture(provider), workspace.Root));
        var fork = Bind(provider, ForkId(provider), path);
        Tick(fork);
        Assert.Equal("change-b", Get(fork).ActiveWork);
    }
}
