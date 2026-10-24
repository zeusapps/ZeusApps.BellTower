using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class WorkPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static SessionIdentity Identity() => new() { Key = "claude:one", Palette = Palette.Create() };

    private static WorkEvidence Work(string change, int seconds, long offset = -1, bool weak = false) =>
        new(change, "D:/root", offset >= 0 ? "history:edit" : "edit", weak, T0.AddSeconds(seconds), offset);

    [Fact]
    public void WeakEvidenceOnlyFillsAnEmptyAssociation()
    {
        var identity = Identity();
        Assert.True(WorkPolicy.Apply(identity, Work("b", 5, weak: true)));
        Assert.True(identity.WorkWeak);
        Assert.False(WorkPolicy.Apply(identity, Work("c", 9, weak: true)));
        Assert.True(WorkPolicy.Apply(identity, Work("a", 1)));
        Assert.False(identity.WorkWeak);
        Assert.False(WorkPolicy.Apply(identity, Work("b", 99, weak: true)));
        Assert.Equal("a", identity.ActiveWork);
        var pinned = Identity();
        pinned.Change = "pinned";
        Assert.False(WorkPolicy.Apply(pinned, Work("b", 5, weak: true)));
    }

    [Fact]
    public void HistoryNeverOverridesNewerLiveWorkOrNewerHistory()
    {
        var identity = Identity();
        Assert.True(WorkPolicy.Apply(identity, Work("live", 50)));
        Assert.False(WorkPolicy.Apply(identity, Work("old-history", 10, offset: 100)));
        Assert.True(WorkPolicy.Apply(identity, Work("newer-history", 60, offset: 500)));
        Assert.False(WorkPolicy.Apply(identity, Work("older-offset", 70, offset: 400)));
        Assert.True(WorkPolicy.Apply(identity, Work("later-live", 80)));
        Assert.Equal(-1, identity.HistoryWorkOffset);
        Assert.Equal("later-live", identity.ActiveWork);
    }

    [Fact]
    public void ChunkOrderDoesNotChangeTheOutcome()
    {
        var evidence = new[] { Work("a", 1, 10), Work("b", 2, 20), Work("c", 3, 30), Work("w", 4, 40, weak: true) };
        foreach (var order in new[] { new[] { 0, 1, 2, 3 }, new[] { 3, 2, 1, 0 }, new[] { 1, 3, 0, 2 }, new[] { 2, 0, 3, 1 } })
        {
            var identity = Identity();
            foreach (var index in order)
            { WorkPolicy.Apply(identity, evidence[index]); }
            Assert.Equal("c", identity.ActiveWork);
            Assert.False(identity.WorkWeak);
        }
    }

    [Fact]
    public async Task BranchChangeInvalidatesOnlyBranchBoundAutomaticWork()
    {
        using var workspace = new TestWorkspace();
        var repository = Path.Combine(workspace.Root, "repository");
        Directory.CreateDirectory(repository);
        GitHelper.Run(repository, "init", "--initial-branch", "feature-one");
        var store = new IdentityStore(workspace.Paths);
        var session = IdentityFixtures.Session("claude", "one", repository);
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        store.Update(session.Key, repository, identity =>
        {
            identity.ManualName = "Pinned";
            identity.Change = "pinned-change";
            WorkPolicy.Apply(identity, Work("auto", 1, offset: 5));
            return true;
        });
        Assert.Equal("feature-one", store.Get(session.Key, repository).WorkBranch);
        GitHelper.Run(repository, "symbolic-ref", "HEAD", "refs/heads/feature-two");
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var after = store.Get(session.Key, repository);
        Assert.Null(after.ActiveWork);
        Assert.False(after.WorkWeak);
        Assert.Equal(-1, after.HistoryWorkOffset);
        Assert.Equal("Pinned", after.ManualName);
        Assert.Equal("pinned-change", after.Change);
    }
}

public sealed class IdentityWriteTests
{
    [Fact]
    public void PresentationSnapshotsAreWrittenOnlyWhenContentChangesOrTheHeartbeatExpires()
    {
        using var workspace = new TestWorkspace();
        var store = new IdentityPresentationStore(workspace.Paths);
        var identity = new IdentityStore(workspace.Paths).Get("claude:one", workspace.Root);
        var git = new GitContext("main");
        var start = DateTimeOffset.UtcNow;
        Assert.True(store.Save(identity, git, start));
        Assert.False(store.Save(identity, git, start.AddSeconds(1)));
        Assert.False(store.Save(identity, git, start.AddSeconds(14)));
        Assert.True(store.Save(identity, new GitContext("feature"), start.AddSeconds(15)));
        identity.ActiveWork = "change-a";
        Assert.True(store.Save(identity, new GitContext("feature"), start.AddSeconds(16)));
        Assert.False(store.Save(identity, new GitContext("feature"), start.AddSeconds(17)));
        Assert.True(store.Save(identity, new GitContext("feature"), start.AddSeconds(40)));
        Assert.Equal("change-a", store.Read(identity.Key)?.Title);
    }

    [Fact]
    public void RepeatedWorkObservationsDoNotRewriteTheIdentity()
    {
        using var workspace = new TestWorkspace();
        var directory = IdentityFixtures.Change(workspace.Root, "change-a");
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        var line = JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd = workspace.Root,
            hook_event_name = "PostToolUse",
            tool_name = "Write",
            tool_use_id = "w1",
            tool_input = new { file_path = Path.Combine(directory, "proposal.md") },
            tool_response = new { }
        });
        using var hook = JsonDocument.Parse(line);
        observer.Observe("claude:one", workspace.Root, hook.RootElement);
        var file = Directory.GetFiles(workspace.Paths.Identities, "*.json").Single();
        var bytes = File.ReadAllBytes(file);
        observer.Observe("claude:one", workspace.Root, hook.RootElement);
        Assert.Equal(bytes, File.ReadAllBytes(file));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void HookIngestionBindsTheTranscriptOnceAndRestartsRecoveryOnlyWhenItChanges(string provider)
    {
        using var workspace = new TestWorkspace();
        var first = Path.Combine(workspace.Root, "first.jsonl");
        var second = Path.Combine(workspace.Root, "second.jsonl");
        File.WriteAllText(first, "");
        File.WriteAllText(second, "");
        string Start(object? transcript) => JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd = workspace.Root,
            hook_event_name = "SessionStart",
            source = "startup",
            transcript_path = transcript
        });
        var ingestor = new HookIngestor(workspace.Paths);
        // Codex documents transcript_path as nullable; null must be tolerated without binding anything.
        ingestor.Ingest(provider, new StringReader(Start(null)));
        var store = new IdentityStore(workspace.Paths);
        Assert.Null(store.Get(provider + ":one", workspace.Root).TranscriptPath);
        ingestor.Ingest(provider, new StringReader(Start(first)));
        var bound = store.Get(provider + ":one", workspace.Root);
        Assert.Equal(first, bound.TranscriptPath);
        store.Update(provider + ":one", workspace.Root, identity => { identity.HistoryOffset = 7; identity.HistoryLength = 9; identity.HistoryFingerprint = "x"; return true; });
        ingestor.Ingest(provider, new StringReader(Start(first)));
        Assert.Equal(9, store.Get(provider + ":one", workspace.Root).HistoryLength);
        ingestor.Ingest(provider, new StringReader(Start(second)));
        var rebound = store.Get(provider + ":one", workspace.Root);
        Assert.Equal(second, rebound.TranscriptPath);
        Assert.Equal(0, rebound.HistoryLength);
        Assert.Null(rebound.HistoryFingerprint);
    }
}
