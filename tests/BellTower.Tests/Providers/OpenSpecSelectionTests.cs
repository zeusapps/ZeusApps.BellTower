using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

public sealed class OpenSpecSelectionTests
{
    private static JsonDocument Hook(string command, string operation = "call-1", string hook = "PostToolUse",
        string? agent = null, object? response = null, string? cwd = null) => JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd,
            hook_event_name = hook,
            tool_name = "Bash",
            tool_use_id = operation,
            agent_id = agent,
            tool_input = new { command },
            tool_response = response
        }));

    private static string Success(string root, string change) => JsonSerializer.Serialize(new
    {
        changeName = change,
        schemaName = "spec-driven",
        root = new { path = root }
    });

    private static void Select(TestWorkspace workspace, string key, string change, string operation)
    {
        var command = "openspec status --change " + change + " --json";
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        using var pre = Hook(command, operation, "PreToolUse");
        using var post = Hook(command, operation, response: Success(workspace.Root, change));
        observer.Observe(key, workspace.Root, pre.RootElement);
        observer.Observe(key, workspace.Root, post.RootElement);
    }

    [Theory]
    [InlineData("openspec status --change 'change-one' --json", "change-one", null)]
    [InlineData("openspec instructions apply --change=change-two --store='my-store' --json", "change-two", "my-store")]
    [InlineData("openspec new change change-three", "change-three", null)]
    [InlineData("\"D:/Tools/Open Spec/openspec.cmd\" status --json --change change-four", "change-four", null)]
    [InlineData("& 'D:/Tools/Open Spec/openspec.ps1' status --json --change change-four --store Team.Store_1", "change-four", "Team.Store_1")]
    public void LiteralSelectionsAreRecognized(string command, string change, string? store)
    {
        using var hook = Hook(command);
        var selected = OpenSpecSelectionReader.Read(hook.RootElement);
        Assert.NotNull(selected);
        Assert.Equal(change, selected.Change);
        Assert.Equal(store, selected.Store);
    }

    [Theory]
    [InlineData("openspec list --json")]
    [InlineData("openspec show session-identity --type spec")]
    [InlineData("echo openspec status --change change-one")]
    [InlineData("openspec status --change $selected")]
    [InlineData("openspec status --change change-one; openspec status --change change-two")]
    [InlineData("openspec status --change change-one | Out-String")]
    [InlineData("openspec status --change change-one --change change-two")]
    [InlineData("openspec status --change '../escape'")]
    [InlineData("openspec status --change 'change-one")]
    public void AmbiguousOrUnsupportedProgramsDoNotSelect(string command)
    {
        using var hook = Hook(command);
        Assert.Null(OpenSpecSelectionReader.Read(hook.RootElement));
    }

    [Fact]
    public void ParallelSessionsSwitchIndependentlyAndManualNameWins()
    {
        using var workspace = new TestWorkspace();
        var identities = new IdentityStore(workspace.Paths);
        var first = identities.Get("codex:one", workspace.Root, "Manual title");
        Select(workspace, first.Key, "change-a", "call-a");
        Select(workspace, "codex:two", "change-b", "call-b");
        Select(workspace, first.Key, "change-c", "call-c");
        var updated = identities.Get(first.Key, workspace.Root);
        Assert.Equal("Manual title", updated.Name);
        Assert.Equal("change-c", updated.Change);
        Assert.Equal(first.Palette, updated.Palette);
        Assert.Equal("change-b", identities.Get("codex:two", workspace.Root).Name);
        identities.Update(first.Key, workspace.Root, identity => { identity.ManualName = null; return true; });
        Assert.Equal("change-c", identities.Get(first.Key, workspace.Root, initialChange: "old-launch").Name);
    }

    [Fact]
    public void LatestStartedSelectionRejectsDelayedCompletionAndReplay()
    {
        using var workspace = new TestWorkspace();
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        using var oldPre = Hook("openspec status --change old --json", "old", "PreToolUse");
        observer.Observe("claude:one", workspace.Root, oldPre.RootElement);
        Select(workspace, "claude:one", "new", "new");
        using var oldPost = Hook("openspec status --change old --json", "old", response: Success(workspace.Root, "old"));
        observer.Observe("claude:one", workspace.Root, oldPost.RootElement);
        observer.Observe("claude:one", workspace.Root, oldPre.RootElement);
        observer.Observe("claude:one", workspace.Root, oldPost.RootElement);
        Assert.Equal("new", new IdentityStore(workspace.Paths).Get("claude:one", workspace.Root).Name);
    }

    [Fact]
    public async Task AtomicUpdatesPreserveManualFieldsDuringConcurrentSelections()
    {
        using var workspace = new TestWorkspace();
        var store = new IdentityStore(workspace.Paths);
        var original = store.Get("codex:one", workspace.Root);
        await Task.WhenAll(Task.Run(() => Select(workspace, original.Key, "change-one", "one")),
            Task.Run(() => store.Update(original.Key, workspace.Root, identity =>
            {
                identity.ManualName = "Manual";
                return true;
            })));
        var result = store.Get(original.Key, workspace.Root);
        Assert.Equal("Manual", result.Name);
        Assert.Equal("change-one", result.Change);
        Assert.Equal(original.Palette, result.Palette);
    }

    [Fact]
    public void LegacyIdentityAndLocalScaffoldSelectionRemainUsable()
    {
        using var workspace = new TestWorkspace();
        var key = "claude:one";
        var palette = Palette.Create();
        var file = Path.Combine(workspace.Paths.Identities, ContentHash.Compute(key) + ".json");
        File.WriteAllText(file, Json.Encode(new { key, defaultName = "Project", change = "old", palette }));
        var scaffold = Path.Combine(workspace.Root, "openspec", "changes", "new-change");
        Directory.CreateDirectory(scaffold);
        File.WriteAllText(Path.Combine(scaffold, ".openspec.yaml"), "schema: spec-driven\n");
        var command = "openspec new change new-change";
        using var pre = Hook(command, "new", "PreToolUse", cwd: workspace.Root);
        using var post = Hook(command, "new", response: "Created change 'new-change' at openspec/changes/new-change/", cwd: workspace.Root);
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        observer.Observe(key, workspace.Root, pre.RootElement);
        observer.Observe(key, workspace.Root, post.RootElement);
        var identity = new IdentityStore(workspace.Paths).Get(key, workspace.Root);
        Assert.Equal("new-change", identity.Name);
        Assert.Equal(palette, identity.Palette);
    }

    [Fact]
    public void FailedAndBackgroundOperationsCannotChangeAssociation()
    {
        using var workspace = new TestWorkspace();
        Select(workspace, "codex:one", "original", "original");
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        var command = "openspec status --change other --json";
        using var pre = Hook(command, "bad", "PreToolUse");
        using var failed = Hook(command, "bad", response: new { exit_code = 1, stdout = Success(workspace.Root, "other") });
        using var error = Hook(command, "bad", response: "{\"error\":\"not found\"}");
        using var background = Hook(command, "background", "PreToolUse", "agent-1");
        observer.Observe("codex:one", workspace.Root, pre.RootElement);
        observer.Observe("codex:one", workspace.Root, failed.RootElement);
        observer.Observe("codex:one", workspace.Root, error.RootElement);
        observer.Observe("codex:one", workspace.Root, background.RootElement);
        Assert.Equal("original", new IdentityStore(workspace.Paths).Get("codex:one", workspace.Root).Name);
        Assert.Null(OpenSpecSelectionReader.Read(background.RootElement));
    }

    [Fact]
    public void ExternalPlanningRootIsNotSessionCheckoutAndNoRawPayloadIsSaved()
    {
        using var workspace = new TestWorkspace();
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        var command = "openspec instructions apply --change task-one --store private-store --json";
        using var pre = Hook(command, "op", "PreToolUse");
        var root = Path.Combine(workspace.Root, "planning");
        using var post = Hook(command, "op", response: new { stdout = Success(root, "task-one"), stderr = "private content", exit_code = 0 });
        observer.Observe("claude:one", workspace.Root, pre.RootElement);
        observer.Observe("claude:one", workspace.Root, post.RootElement);
        var identity = new IdentityStore(workspace.Paths).Get("claude:one", workspace.Root);
        Assert.Equal(root, identity.SelectionRoot);
        Assert.Equal("private-store", identity.SelectionStore);
        Assert.Equal("task-one", identity.Name);
        var saved = string.Join("", Directory.GetFiles(workspace.Paths.Identities).Select(File.ReadAllText));
        Assert.DoesNotContain(command, saved);
        Assert.DoesNotContain("private content", saved);
        Assert.Empty(Directory.GetFiles(workspace.Paths.Inbox));
    }

    [Fact]
    public void MetadataOnlyUpdatesPreserveWaitAndConcurrentManualEdit()
    {
        using var workspace = new TestWorkspace();
        var events = new EventStore(workspace.Paths);
        events.Publish(TestWorkspace.Event(EventKind.Permission) with { Directory = workspace.Root });
        var store = new StateStore(workspace.Paths);
        var session = store.Drain().Sessions["claude:session-1"];
        store.MarkViewed(session.Key, session.Episode!);
        store.Acknowledge(session.Key);
        var before = Json.Encode(store.Read());
        var catalog = new SessionCatalog(workspace.Paths);
        Select(workspace, session.Key, "change-one", "one");
        catalog.Rename(session.Key, "My task", null, null);
        Select(workspace, session.Key, "change-two", "two");
        Assert.Equal(before, Json.Encode(store.Read()));
        Assert.Equal("My task", catalog.List().Single().Identity.Name);
        catalog.Rename(session.Key, "", null, null);
        Assert.Equal("change-two", catalog.List().Single().Identity.Name);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ProviderIngestionFollowsConfirmedSelection(string provider)
    {
        using var workspace = new TestWorkspace();
        var command = "openspec status --change task-one --json";
        using var pre = Hook(command, "one", "PreToolUse", cwd: workspace.Root);
        using var post = Hook(command, "one", response: Success(workspace.Root, "task-one"), cwd: workspace.Root);
        var ingestor = new HookIngestor(workspace.Paths);
        ingestor.Ingest(provider, new StringReader(pre.RootElement.GetRawText()));
        ingestor.Ingest(provider, new StringReader(post.RootElement.GetRawText()));
        var identity = new IdentityStore(workspace.Paths).Get(provider + ":one", workspace.Root);
        Assert.Equal("task-one", identity.Name);
        Assert.Equal(workspace.Root, new StateStore(workspace.Paths).Drain().Sessions[provider + ":one"].Directory);
    }

    [Theory]
    [InlineData("claude", null)]
    [InlineData("codex", null)]
    [InlineData("claude", "Manual title")]
    [InlineData("codex", "Manual title")]
    public void SessionResumeRestoresSelectedChangeWithoutAnotherSelection(string provider, string? manualName)
    {
        using var workspace = new TestWorkspace();
        var key = provider + ":one";
        var original = new IdentityStore(workspace.Paths).Get(key, workspace.Root, initialName: manualName);
        Select(workspace, key, "saved-change", "saved-selection");
        new HookIngestor(workspace.Paths).Ingest(provider, new StringReader(JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd = workspace.Root,
            hook_event_name = "SessionEnd"
        })));
        Assert.True(new StateStore(workspace.Paths).Drain().Sessions[key].Ended);

        new HookIngestor(workspace.Paths).Ingest(provider, new StringReader(JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd = workspace.Root,
            hook_event_name = "SessionStart",
            source = "resume"
        })));
        Assert.False(new StateStore(workspace.Paths).Drain().Sessions[key].Ended);
        var restored = new IdentityStore(workspace.Paths).Get(key, workspace.Root,
            initialName: "stale launch name", initialChange: "stale-launch-change");
        Assert.Equal("saved-change", restored.Change);
        Assert.Equal(manualName ?? "saved-change", restored.Name);
        Assert.Equal(original.Palette, restored.Palette);
        Assert.Contains(ContentHash.Compute("saved-selection"), restored.SelectionReceipts);
    }
}
