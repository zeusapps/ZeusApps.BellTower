using BellTower.Core;
using BellTower.IO;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Sessions;

// Bounded incremental recovery for Claude JSONL and the verified subset of Codex rollouts.
public sealed class IdentityHistoryTests : IDisposable
{
    private const string Id = "fixture-session";
    private readonly TestWorkspace workspace = new();
    private readonly string transcript;
    private readonly IdentityStore store;
    private readonly IdentityHistoryReader reader;

    public IdentityHistoryTests()
    {
        transcript = Path.Combine(workspace.Root, "transcript.jsonl");
        store = new(workspace.Paths);
        reader = new(workspace.Paths);
    }

    public void Dispose() => workspace.Dispose();

    private SessionState Bind(string provider = "claude", string? path = null, string id = Id)
    {
        var session = IdentityFixtures.Session(provider, id, workspace.Root);
        store.Update(session.Key, workspace.Root, identity => HistoryState.Bind(identity, path ?? transcript));
        return session;
    }

    private SessionIdentity Get(SessionState session) => store.Get(session.Key, workspace.Root);

    private int Tick(SessionState session, int limit = 40)
    {
        var ticks = 0;
        while (ticks < limit && reader.Refresh(session))
        { ticks++; }
        return ticks;
    }

    [Fact]
    public void ClaudeCustomTitleFixtureIsRecoveredAsTheVerifiedNativeTitle()
    {
        File.WriteAllText(transcript, IdentityFixtures.Load("claude-2.1.306-custom-title.jsonl", workspace.Root));
        var session = Bind();
        Assert.True(reader.Refresh(session));
        var identity = Get(session);
        Assert.Equal("Isolated SDK identity rename", identity.NativeTitle);
        Assert.Equal("claude-custom-title", identity.NativeTitleSource);
        Assert.Equal("Isolated SDK identity rename", identity.Name);
        Assert.False(reader.Refresh(session));
    }

    [Fact]
    public void BellTowerWrittenNamesAreNotReadBackAsUserOverrides()
    {
        IdentityFixtures.Write(transcript, IdentityFixtures.Claude.Title(Id, "User title"));
        var session = Bind();
        store.Update(session.Key, workspace.Root, identity => { identity.NativeLastName = "PM: change-a"; return true; });
        Tick(session);
        Assert.Equal("User title", Get(session).NativeTitle);
        IdentityFixtures.Append(transcript, IdentityFixtures.Claude.Title(Id, "PM: change-a"));
        Tick(session);
        var identity = Get(session);
        Assert.Null(identity.NativeTitle);
        Assert.Null(identity.NativeTitleSource);
        Assert.Equal("PM: change-a", identity.NativeLastName);
    }

    [Fact]
    public void LatestTitleWinsAcrossResumeAndOlderChunksCannotOverrideIt()
    {
        var lines = new List<string> { IdentityFixtures.Claude.Title(Id, "Old title") };
        lines.AddRange(Enumerable.Range(0, 12).Select(i => IdentityFixtures.Claude.Chatter(Id, i, 60_000)));
        lines.Add(IdentityFixtures.Claude.Title(Id, "New title"));
        lines.Add(IdentityFixtures.Claude.Chatter(Id, 100, 100));
        IdentityFixtures.Write(transcript, lines.ToArray());
        var session = Bind();
        Tick(session);
        Assert.Equal("New title", Get(session).NativeTitle);
        // Resume appends a newer title to the same conversation.
        IdentityFixtures.Append(transcript, IdentityFixtures.Claude.Title(Id, "Resumed title"));
        Tick(session);
        Assert.Equal("Resumed title", Get(session).NativeTitle);
        Assert.Equal(0, Get(session).HistoryOffset);
    }

    [Fact]
    public void RotationAndTruncationRestartRecoveryAndDropDerivedTitleAndWork()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Write(transcript,
            IdentityFixtures.Claude.Title(Id, "First file title"),
            IdentityFixtures.Claude.Write(Id, "w1", workspace.Root, "change-a", 1),
            IdentityFixtures.Claude.Result(Id, "w1", 2));
        var session = Bind();
        Tick(session);
        var first = Get(session);
        Assert.Equal("First file title", first.NativeTitle);
        Assert.Equal("change-a", first.ActiveWork);
        Assert.StartsWith("history:", first.WorkSource);

        // Truncation: the file becomes shorter than the saved cursor and carries different content.
        IdentityFixtures.Write(transcript, IdentityFixtures.Claude.Chatter(Id, 1, 10));
        Tick(session);
        var truncated = Get(session);
        Assert.Null(truncated.NativeTitle);
        Assert.Null(truncated.ActiveWork);
        Assert.Equal(-1, truncated.NativeTitleOffset);

        // Rotation: same conversation, replaced file with a larger body and a new title.
        IdentityFixtures.Write(transcript, IdentityFixtures.Claude.Chatter(Id, 1, 5_000), IdentityFixtures.Claude.Title(Id, "Rotated title"),
            IdentityFixtures.Claude.Chatter(Id, 2, 5_000));
        Tick(session);
        Assert.Equal("Rotated title", Get(session).NativeTitle);
    }

    [Fact]
    public void ClearAndForkBindingsDoNotInheritAnotherConversationsEvidence()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var original = Path.Combine(workspace.Root, "original.jsonl");
        IdentityFixtures.Write(original,
            IdentityFixtures.Claude.Title(Id, "Original"),
            IdentityFixtures.Claude.Write(Id, "w1", workspace.Root, "change-a", 1),
            IdentityFixtures.Claude.Result(Id, "w1", 2));
        var session = Bind(path: original);
        Tick(session);
        Assert.Equal("change-a", Get(session).ActiveWork);

        // /clear gives the conversation a new transcript: derived state restarts, pins and palette survive.
        store.Update(session.Key, workspace.Root, identity => { identity.ManualName = "Pinned"; return true; });
        var palette = Get(session).Palette;
        var cleared = Path.Combine(workspace.Root, "cleared.jsonl");
        IdentityFixtures.Write(cleared, IdentityFixtures.Claude.Chatter(Id, 1, 10));
        store.Update(session.Key, workspace.Root, identity => HistoryState.Bind(identity, cleared));
        Tick(session);
        var after = Get(session);
        Assert.Null(after.ActiveWork);
        Assert.Null(after.NativeTitle);
        Assert.Equal("Pinned", after.Name);
        Assert.Equal(palette, after.Palette);

        // A forked transcript copies another conversation's records; they are not this session's evidence.
        var fork = Path.Combine(workspace.Root, "fork.jsonl");
        IdentityFixtures.Write(fork, File.ReadAllLines(original));
        var forked = Bind(path: fork, id: "forked-session");
        Tick(forked);
        Assert.Null(Get(forked).ActiveWork);
        Assert.Null(Get(forked).NativeTitle);
    }

    [Fact]
    public void FirstInstallRecoveryWalksBackBeyondTheFirstChunkAcrossTicks()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var lines = new List<string>
        {
            IdentityFixtures.Claude.Write(Id, "w1", workspace.Root, "change-a", 1),
            IdentityFixtures.Claude.Result(Id, "w1", 2)
        };
        // About 1.4 MB of unrelated conversation follows the only work evidence: far more than one chunk.
        lines.AddRange(Enumerable.Range(0, 24).Select(i => IdentityFixtures.Claude.Chatter(Id, 10 + i, 60_000)));
        IdentityFixtures.Write(transcript, lines.ToArray());
        var session = Bind();
        Assert.True(reader.Refresh(session));
        Assert.Null(Get(session).ActiveWork);
        Assert.True(Get(session).HistoryOffset > 0);
        var ticks = Tick(session);
        Assert.InRange(ticks, 1, 12);
        var identity = Get(session);
        Assert.Equal("change-a", identity.ActiveWork);
        Assert.Equal("history:edit", identity.WorkSource);
        Assert.Equal(0, identity.HistoryOffset);
        Assert.False(reader.Refresh(session));
    }

    [Fact]
    public void GrowthLargerThanTheTickBudgetLeavesNoUnreadGap()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        IdentityFixtures.Write(transcript,
            IdentityFixtures.Claude.Write(Id, "w1", workspace.Root, "change-a", 1), IdentityFixtures.Claude.Result(Id, "w1", 2),
            IdentityFixtures.Claude.Chatter(Id, 3, 100));
        var session = Bind();
        Tick(session);
        Assert.Equal("change-a", Get(session).ActiveWork);

        // 1.5 MB arrives between two ticks and the only new work sits in the middle of it.
        var growth = new List<string>();
        growth.AddRange(Enumerable.Range(0, 8).Select(i => IdentityFixtures.Claude.Chatter(Id, 10 + i, 60_000)));
        growth.Add(IdentityFixtures.Claude.Write(Id, "w2", workspace.Root, "change-b", 30));
        growth.Add(IdentityFixtures.Claude.Result(Id, "w2", 31));
        growth.AddRange(Enumerable.Range(0, 14).Select(i => IdentityFixtures.Claude.Chatter(Id, 40 + i, 60_000)));
        IdentityFixtures.Append(transcript, growth.ToArray());
        var length = new FileInfo(transcript).Length;
        Assert.True(length - Get(session).HistoryLength > IdentityHistoryReader.ByteBudget * 3);

        Tick(session);
        var identity = Get(session);
        Assert.Equal("change-b", identity.ActiveWork);
        Assert.InRange(length - identity.HistoryLength, 0, IdentityHistoryReader.CheckpointBytes);
    }

    [Fact]
    public void IncompleteTrailingLineIsDeferredUntilItsRecordIsComplete()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        var call = IdentityFixtures.Claude.Write(Id, "w1", workspace.Root, "change-a", 1);
        var result = IdentityFixtures.Claude.Result(Id, "w1", 2);
        File.WriteAllText(transcript, call + "\n" + result[..(result.Length / 2)]);
        var session = Bind();
        Tick(session);
        Assert.Null(Get(session).ActiveWork);
        File.AppendAllText(transcript, result[(result.Length / 2)..] + "\n");
        Tick(session);
        Assert.Equal("change-a", Get(session).ActiveWork);
    }

    [Fact]
    public void CallsRequireTheirOwnSuccessfulResultAndIgnoreSubagentRecords()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        IdentityFixtures.Change(workspace.Root, "change-c");
        IdentityFixtures.Write(transcript,
            IdentityFixtures.Claude.Write(Id, "failed", workspace.Root, "change-a", 1), IdentityFixtures.Claude.Result(Id, "failed", 2, error: true),
            IdentityFixtures.Claude.Write(Id, "interrupted", workspace.Root, "change-b", 3),
            IdentityFixtures.Claude.Use(Id, "sub", "Write", new { file_path = IdentityFixtures.Slashed(Path.Combine(workspace.Root, "openspec", "changes", "change-c", "x.md")) }, workspace.Root, 4, sidechain: true),
            IdentityFixtures.Claude.Result(Id, "sub", 5, sidechain: true),
            IdentityFixtures.Claude.Result(Id, "unrelated-result", 6));
        var session = Bind();
        Tick(session);
        Assert.Null(Get(session).ActiveWork);
        IdentityFixtures.Append(transcript, IdentityFixtures.Claude.Result(Id, "interrupted", 7));
        Tick(session);
        Assert.Equal("change-b", Get(session).ActiveWork);
    }

    [Fact]
    public void ClaudeToolPairFixtureResolvesOnlyTheMainSessionSuccess()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        IdentityFixtures.Change(workspace.Root, "change-c");
        File.WriteAllText(transcript, IdentityFixtures.Load("claude-tool-pairs.jsonl", workspace.Root));
        var session = Bind();
        Tick(session);
        Assert.Equal("change-a", Get(session).ActiveWork);
        Assert.Empty(Get(session).HistoryCalls);
    }

    [Fact]
    public void ShellCreationNeedsItsSuccessMarkerAndSkillsAndPowerShellAreRecovered()
    {
        var worktree = Path.Combine(workspace.Root, "work tree");
        IdentityFixtures.Change(worktree, "change-a", scaffold: true);
        IdentityFixtures.Change(worktree, "change-b", scaffold: true);
        IdentityFixtures.Write(transcript,
            IdentityFixtures.Claude.Use(Id, "n1", "Bash", new { command = "cd \"" + worktree + "\" && openspec new change change-a" }, workspace.Root, 1),
            IdentityFixtures.Claude.Result(Id, "n1", 2, text: "no marker here"),
            IdentityFixtures.Claude.Use(Id, "n2", "PowerShell", new { command = "Set-Location '" + worktree + "'; openspec new change change-b | Out-String" }, workspace.Root, 3),
            IdentityFixtures.Claude.Result(Id, "n2", 4, text: "Created change 'change-b' at openspec/changes/change-b/"));
        var session = Bind();
        Tick(session);
        var identity = Get(session);
        Assert.Equal("change-b", identity.ActiveWork);
        Assert.Equal(worktree, identity.SelectionRoot);

        IdentityFixtures.Change(worktree, "change-s");
        IdentityFixtures.Append(transcript,
            IdentityFixtures.Claude.Use(Id, "s1", "Skill", new { skill = "opsx:apply", args = "change-s" }, worktree, 5),
            IdentityFixtures.Claude.Result(Id, "s1", 6));
        Tick(session);
        Assert.Equal("change-s", Get(session).ActiveWork);
    }

    [Fact]
    public void ReadOnlyInspectionAfterWorkNeverReplacesItLiveOrDuringRecovery()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b", scaffold: true);
        string[] Inspect(int at) =>
        [
            IdentityFixtures.Claude.Use(Id, "i" + at, "Skill", new { skill = "opsx:explore", args = "change-b" }, workspace.Root, at),
            IdentityFixtures.Claude.Result(Id, "i" + at, at + 1),
            IdentityFixtures.Claude.Use(Id, "st" + at, "Bash", new { command = "openspec status --change change-b --json" }, workspace.Root, at + 2),
            IdentityFixtures.Claude.Result(Id, "st" + at, at + 3, text: "{\"changeName\":\"change-b\"}"),
            IdentityFixtures.Claude.Use(Id, "rd" + at, "Read", new { file_path = IdentityFixtures.Slashed(Path.Combine(workspace.Root, "openspec", "changes", "change-b", "design.md")) }, workspace.Root, at + 4),
            IdentityFixtures.Claude.Result(Id, "rd" + at, at + 5)
        ];
        var lines = new List<string> { IdentityFixtures.Claude.Write(Id, "w1", workspace.Root, "change-a", 1), IdentityFixtures.Claude.Result(Id, "w1", 2) };
        lines.AddRange(Inspect(10));
        // Inspection of B is also the newest chunk, so a time-ordered or newest-first reader would pick it.
        lines.AddRange(Enumerable.Range(0, 12).Select(i => IdentityFixtures.Claude.Chatter(Id, 30 + i, 60_000)));
        lines.AddRange(Inspect(60));
        IdentityFixtures.Write(transcript, lines.ToArray());
        var session = Bind();
        Tick(session);
        var identity = Get(session);
        Assert.Equal("change-a", identity.ActiveWork);
        Assert.False(identity.WorkWeak);
    }

    [Fact]
    public void WeakHistoricalContextOnlyFillsAnEmptyAssociationRegardlessOfChunkOrder()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        var explore = new[]
        {
            IdentityFixtures.Claude.Use(Id, "e1", "Skill", new { skill = "explain-change", args = "change-b" }, workspace.Root, 50),
            IdentityFixtures.Claude.Result(Id, "e1", 51)
        };
        // Strong work is older than a whole chunk; the weak record is the newest and is read first.
        var lines = new List<string> { IdentityFixtures.Claude.Write(Id, "w1", workspace.Root, "change-a", 1), IdentityFixtures.Claude.Result(Id, "w1", 2) };
        lines.AddRange(Enumerable.Range(0, 10).Select(i => IdentityFixtures.Claude.Chatter(Id, 10 + i, 60_000)));
        lines.AddRange(explore);
        IdentityFixtures.Write(transcript, lines.ToArray());
        var session = Bind();
        Assert.True(reader.Refresh(session));
        var early = Get(session);
        Assert.Equal("change-b", early.ActiveWork);
        Assert.True(early.WorkWeak);
        Tick(session);
        var final = Get(session);
        Assert.Equal("change-a", final.ActiveWork);
        Assert.False(final.WorkWeak);
    }

    [Fact]
    public void UnchangedHistoryAndSmallTailGrowthCauseNoIdentityWrites()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Write(transcript,
            IdentityFixtures.Claude.Write(Id, "w1", workspace.Root, "change-a", 1), IdentityFixtures.Claude.Result(Id, "w1", 2),
            IdentityFixtures.Claude.Title(Id, "Title"));
        var session = Bind();
        Tick(session);
        var file = Directory.GetFiles(workspace.Paths.Identities, "*.json").Single();
        var written = File.GetLastWriteTimeUtc(file);
        var bytes = File.ReadAllBytes(file);
        Thread.Sleep(30);
        for (var i = 0; i < 3; i++)
        { Assert.False(reader.Refresh(session)); }
        IdentityFixtures.Append(transcript, IdentityFixtures.Claude.Chatter(Id, 9, 200));
        Assert.False(reader.Refresh(session));
        Assert.Equal(written, File.GetLastWriteTimeUtc(file));
        Assert.Equal(bytes, File.ReadAllBytes(file));
        // Relevant new evidence still writes.
        IdentityFixtures.Append(transcript, IdentityFixtures.Claude.Title(Id, "Newer"));
        Assert.True(reader.Refresh(session));
        Assert.Equal("Newer", Get(session).NativeTitle);
    }

    [Fact]
    public void CodexRolloutRecoversVerifiedShellCreationAndIgnoresUnrecognizedPatchResults()
    {
        var worktree = Path.Combine(workspace.Root, "work tree");
        IdentityFixtures.Change(worktree, "change-a", scaffold: true);
        IdentityFixtures.Change(worktree, "change-b", scaffold: true);
        IdentityFixtures.Change(workspace.Root, "change-p");
        var rollout = IdentityFixtures.Load("codex-rollout-exec-command.jsonl", workspace.Root).Replace("cd " + IdentityFixtures.Slashed(workspace.Root) + " &&", "cd \\\\\\\"" + IdentityFixtures.Slashed(worktree) + "\\\\\\\" &&");
        File.WriteAllText(transcript, rollout);
        // The failed creation of change-b ("Process exited with code 1") and the patch of change-p with a redacted, unrecognized result must not select.
        var session = Bind("codex");
        Tick(session);
        var identity = Get(session);
        Assert.Equal("change-a", identity.ActiveWork);
        Assert.Equal("history:new", identity.WorkSource);
        Assert.Contains("unverified", IdentityHistoryReader.Capabilities);
        Assert.Contains("apply_patch", IdentityHistoryReader.Capabilities);
    }

    [Fact]
    public void CodexExecWorkdirAndOutputContentArraysAreHonored()
    {
        var worktree = Path.Combine(workspace.Root, "work tree");
        IdentityFixtures.Change(worktree, "change-a", scaffold: true);
        IdentityFixtures.Write(transcript,
            IdentityFixtures.Codex.Exec("call-1", "openspec new change change-a", 1, worktree),
            IdentityFixtures.Codex.ExecResult("call-1", 2, output: "Created change 'change-a' at openspec/changes/change-a/", items: true),
            IdentityFixtures.Codex.Patch("call-p", workspace.Root, "change-a", 3),
            IdentityFixtures.Codex.PatchResult("call-p", 4));
        var session = Bind("codex");
        Tick(session);
        Assert.Equal("change-a", Get(session).ActiveWork);
        Assert.Equal(worktree, Get(session).SelectionRoot);
    }

    [Fact]
    public void CodexApplyPatchHistoryRequiresTheExactVerifiedSuccessResult()
    {
        foreach (var change in new[] { "change-a", "change-b", "change-c", "change-d" })
        { IdentityFixtures.Change(workspace.Root, change); }
        IdentityFixtures.Write(transcript,
            IdentityFixtures.Codex.Patch("ok", workspace.Root, "change-a", 1),
            IdentityFixtures.Codex.PatchResult("ok", 2),
            // A verification failure, a non-zero exit, the marker without the exit header and an unrecognized shape never select.
            IdentityFixtures.Codex.Patch("bad1", workspace.Root, "change-b", 3),
            IdentityFixtures.Codex.PatchResult("bad1", 4, "apply_patch verification failed: Failed to find expected lines in x"),
            IdentityFixtures.Codex.Patch("bad2", workspace.Root, "change-c", 5),
            IdentityFixtures.Codex.PatchResult("bad2", 6, "Exit code: 1\nWall time: 0.1 seconds\nOutput:\nSuccess. Updated the following files:\nA x\n"),
            IdentityFixtures.Codex.Patch("bad3", workspace.Root, "change-d", 7),
            IdentityFixtures.Codex.PatchResult("bad3", 8, "Success. Updated the following files:\nA x"));
        var session = Bind("codex");
        Tick(session);
        Assert.Equal("change-a", Get(session).ActiveWork);
        Assert.Equal("history:edit", Get(session).WorkSource);
    }

    [Fact]
    public void CodexApplyPatchResultsInContentArrayFormAndWithTruncationLinesAreRecovered()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        IdentityFixtures.Write(transcript,
            IdentityFixtures.Codex.Patch("one", workspace.Root, "change-a", 1),
            IdentityFixtures.Codex.PatchResult("one", 2, items: true),
            IdentityFixtures.Codex.Patch("two", workspace.Root, "change-b", 3),
            IdentityFixtures.Codex.PatchResult("two", 4, "Exit code: 0\r\nWall time: 12 seconds\r\nTotal output lines: 90\r\nOutput:\r\nSuccess. Updated the following files:\r\nM a\r\n"));
        var session = Bind("codex");
        Tick(session);
        Assert.Equal("change-b", Get(session).ActiveWork);
    }

    [Fact]
    public void ASdkForkedClaudeTranscriptCarriesInheritedEvidenceUnderItsRewrittenSessionId()
    {
        // claude-sdk-0.3.309-fork.jsonl is a real SDK forkSession result on a disposable transcript: every record has the
        // fork's sessionId, uuids are remapped and the title gains " (fork)". The parent's id is still not the fork's.
        IdentityFixtures.Change(workspace.Root, "change-a");
        File.WriteAllText(transcript, IdentityFixtures.Load("claude-sdk-0.3.309-fork.jsonl", workspace.Root));
        var fork = Bind(id: "FORK-SESSION");
        Tick(fork);
        Assert.Equal("change-a", Get(fork).ActiveWork);
        // The SDK's generated "(fork)" title is not a user override.
        Assert.Null(Get(fork).NativeTitle);
        Assert.Null(Get(fork).NativeTitleSource);
        Assert.Equal("PARENT-SESSION", Get(fork).ForkParent);
        var parent = Bind(id: "PARENT-SESSION");
        Tick(parent);
        Assert.Null(Get(parent).ActiveWork);
        Assert.Null(Get(parent).NativeTitle);
    }

    [Fact]
    public void CodexRecoveryWalksBackAcrossChunksAndHonorsFailedExitCodes()
    {
        var worktree = Path.Combine(workspace.Root, "work tree");
        IdentityFixtures.Change(worktree, "change-a", scaffold: true);
        IdentityFixtures.Change(worktree, "change-b", scaffold: true);
        var lines = new List<string>
        {
            IdentityFixtures.Codex.Exec("ok", "openspec new change change-a", 1, worktree),
            IdentityFixtures.Codex.ExecResult("ok", 2, output: "Created change 'change-a' at x"),
            IdentityFixtures.Codex.Exec("bad", "openspec new change change-b", 3, worktree),
            IdentityFixtures.Codex.ExecResult("bad", 4, exit: 1, output: "Created change 'change-b' at x")
        };
        lines.AddRange(Enumerable.Range(0, 12).Select(i => IdentityFixtures.Codex.Chatter(10 + i, 60_000)));
        IdentityFixtures.Write(transcript, lines.ToArray());
        var session = Bind("codex");
        Tick(session);
        Assert.Equal("change-a", Get(session).ActiveWork);
    }

    [Fact]
    public void MixedClaudeAndCodexSessionsRecoverIndependentlyWithoutSharingState()
    {
        var worktree = Path.Combine(workspace.Root, "work tree");
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(worktree, "change-b", scaffold: true);
        var claudePath = Path.Combine(workspace.Root, "claude.jsonl");
        var codexPath = Path.Combine(workspace.Root, "codex.jsonl");
        IdentityFixtures.Write(claudePath,
            IdentityFixtures.Claude.Title("claude-session", "Claude title"),
            IdentityFixtures.Claude.Write("claude-session", "w1", workspace.Root, "change-a", 1), IdentityFixtures.Claude.Result("claude-session", "w1", 2));
        IdentityFixtures.Write(codexPath,
            IdentityFixtures.Codex.Exec("c1", "openspec new change change-b", 1, worktree),
            IdentityFixtures.Codex.ExecResult("c1", 2, output: "Created change 'change-b' at x", items: true));
        var claude = Bind("claude", claudePath, "claude-session");
        var codex = Bind("codex", codexPath, "codex-session");
        Tick(claude);
        Tick(codex);
        Assert.Equal("change-a", Get(claude).ActiveWork);
        Assert.Equal("Claude title", Get(claude).NativeTitle);
        Assert.Equal("change-b", Get(codex).ActiveWork);
        Assert.Null(Get(codex).NativeTitle);
        Assert.NotEqual(Get(claude).TranscriptPath, Get(codex).TranscriptPath);
        Assert.NotEqual(Get(claude).Palette, Get(codex).Palette);
    }

    [Fact]
    public void BackwardWalkStopsOnceWorkAndTitleAreResolvedFromNewerHistory()
    {
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-old");
        var lines = new List<string>
        {
            IdentityFixtures.Claude.Write(Id, "old", workspace.Root, "change-old", 1), IdentityFixtures.Claude.Result(Id, "old", 2),
            IdentityFixtures.Claude.Title(Id, "Old title")
        };
        lines.AddRange(Enumerable.Range(0, 16).Select(i => IdentityFixtures.Claude.Chatter(Id, 10 + i, 60_000)));
        lines.Add(IdentityFixtures.Claude.Write(Id, "new", workspace.Root, "change-a", 40));
        lines.Add(IdentityFixtures.Claude.Result(Id, "new", 41));
        lines.Add(IdentityFixtures.Claude.Title(Id, "New title"));
        IdentityFixtures.Write(transcript, lines.ToArray());
        var session = Bind();
        Tick(session);
        var identity = Get(session);
        Assert.Equal("change-a", identity.ActiveWork);
        Assert.Equal("New title", identity.NativeTitle);
        Assert.True(identity.HistoryOffset > 0);
        Assert.False(reader.Refresh(session));
    }
}
