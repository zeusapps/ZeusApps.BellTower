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
        string? agent = null, object? response = null, string? cwd = null, string tool = "Bash") => JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd,
            hook_event_name = hook,
            tool_name = tool,
            tool_use_id = operation,
            agent_id = agent,
            tool_input = new { command },
            tool_response = response
        }));

    private static JsonDocument Edit(string root, string change, string operation, string hook = "PostToolUse", string? agent = null,
        object? response = null) => JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd = root,
            hook_event_name = hook,
            tool_name = "Write",
            tool_use_id = operation,
            agent_id = agent,
            tool_input = new { file_path = Path.Combine(root, "openspec", "changes", change, "proposal.md") },
            tool_response = hook == "PostToolUse" ? response ?? new { type = "update" } : null
        }));

    private static void Select(TestWorkspace workspace, string key, string change, string operation)
    {
        Directory.CreateDirectory(Path.Combine(workspace.Root, "openspec", "changes", change));
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        using var pre = Edit(workspace.Root, change, operation, "PreToolUse");
        using var post = Edit(workspace.Root, change, operation);
        observer.Observe(key, workspace.Root, pre.RootElement);
        observer.Observe(key, workspace.Root, post.RootElement);
    }

    [Theory]
    [InlineData("openspec status --change 'change-one' --json", "status", "change-one", null)]
    [InlineData("openspec instructions apply --change=change-two --store='my-store' --json", "instructions", "change-two", "my-store")]
    [InlineData("openspec new change change-three", "new", "change-three", null)]
    [InlineData("\"D:/Tools/Open Spec/openspec.cmd\" status --json --change change-four", "status", "change-four", null)]
    [InlineData("& 'D:/Tools/Open Spec/openspec.ps1' status --json --change change-four --store Team.Store_1", "status", "change-four", "Team.Store_1")]
    public void LiteralOperationsAreClassifiedAndOnlyCreationCanBeWork(string command, string verb, string change, string? store)
    {
        using var hook = Hook(command);
        var operation = OpenSpecSelectionReader.Classify(hook.RootElement);
        Assert.NotNull(operation);
        Assert.Equal(verb, operation.Verb);
        Assert.Equal(change, operation.Change);
        Assert.Equal(store, operation.Store);
        Assert.Equal(verb != "new", operation.Inspection);
        Assert.Equal(verb == "new", OpenSpecSelectionReader.Read(hook.RootElement) is not null);
    }

    [Theory]
    [InlineData("openspec list --json")]
    [InlineData("openspec show session-identity --type spec")]
    [InlineData("echo openspec status --change change-one")]
    [InlineData("openspec status --change $selected")]
    [InlineData("openspec status --change change-one; openspec status --change change-two")]
    [InlineData("openspec status --change change-one | Out-File x")]
    [InlineData("openspec status --change change-one --change change-two")]
    [InlineData("openspec status --change '../escape'")]
    [InlineData("openspec status --change 'change-one")]
    public void AmbiguousOrUnsupportedProgramsAreNeitherWorkNorInspection(string command)
    {
        using var hook = Hook(command);
        Assert.Null(OpenSpecSelectionReader.Classify(hook.RootElement));
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
        Assert.Equal("change-c", updated.ActiveWork);
        Assert.Equal(first.Palette, updated.Palette);
        Assert.Equal("change-b", identities.Get("codex:two", workspace.Root).Name);
        identities.Update(first.Key, workspace.Root, identity => { identity.ManualName = null; return true; });
        Assert.Equal("change-c", identities.Get(first.Key, workspace.Root, initialChange: "ignored-for-existing-identity").Name);
    }

    [Fact]
    public void LatestStartedSelectionRejectsDelayedCompletionAndReplay()
    {
        using var workspace = new TestWorkspace();
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        Directory.CreateDirectory(Path.Combine(workspace.Root, "openspec", "changes", "old"));
        using var oldPre = Edit(workspace.Root, "old", "old", "PreToolUse");
        observer.Observe("claude:one", workspace.Root, oldPre.RootElement);
        Select(workspace, "claude:one", "new", "new");
        using var oldPost = Edit(workspace.Root, "old", "old");
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
        Assert.Equal("change-one", result.ActiveWork);
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
        // A legacy saved change stays an explicit association; the created change is recorded as work.
        Assert.Equal("old", identity.Change);
        Assert.Equal("new-change", identity.ActiveWork);
        Assert.Equal(palette, identity.Palette);
    }

    [Fact]
    public void FailedBackgroundAndInspectionOperationsCannotChangeAssociation()
    {
        using var workspace = new TestWorkspace();
        Select(workspace, "codex:one", "original", "original");
        Directory.CreateDirectory(Path.Combine(workspace.Root, "openspec", "changes", "other"));
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        using var pre = Edit(workspace.Root, "other", "bad", "PreToolUse");
        using var failed = Edit(workspace.Root, "other", "bad", response: new { is_error = true });
        using var interrupted = Edit(workspace.Root, "other", "bad", response: new { interrupted = true });
        using var background = Edit(workspace.Root, "other", "background", "PreToolUse", "agent-1");
        var inspect = "openspec status --change other --json";
        using var inspectPre = Hook(inspect, "inspect", "PreToolUse", cwd: workspace.Root);
        using var inspectPost = Hook(inspect, "inspect", cwd: workspace.Root, response: "{\"changeName\":\"other\"}");
        foreach (var hook in new[] { pre, failed, interrupted, background, inspectPre, inspectPost })
        {
            observer.Observe("codex:one", workspace.Root, hook.RootElement);
        }
        Assert.Equal("original", new IdentityStore(workspace.Paths).Get("codex:one", workspace.Root).Name);
        Assert.Null(OpenSpecSelectionReader.Read(background.RootElement));
    }

    [Fact]
    public void ExternalPlanningRootIsNotSessionCheckoutAndNoRawPayloadIsSaved()
    {
        using var workspace = new TestWorkspace();
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        var root = Path.Combine(workspace.Root, "planning");
        Directory.CreateDirectory(Path.Combine(root, "openspec", "changes", "task-one"));
        var input = JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd = workspace.Root,
            hook_event_name = "PostToolUse",
            tool_name = "Write",
            tool_use_id = "op",
            tool_input = new { file_path = Path.Combine(root, "openspec", "changes", "task-one", "tasks.md"), content = "private content" },
            tool_response = new { stderr = "private content" }
        });
        using var post = JsonDocument.Parse(input);
        observer.Observe("claude:one", workspace.Root, post.RootElement);
        var identity = new IdentityStore(workspace.Paths).Get("claude:one", workspace.Root);
        Assert.Equal(root.Replace('\\', '/'), identity.SelectionRoot);
        Assert.Equal(workspace.Root, identity.LaunchDirectory);
        Assert.Equal("task-one", identity.Name);
        var saved = string.Join("", Directory.GetFiles(workspace.Paths.Identities).Select(File.ReadAllText));
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
        catalog.Rename(session.Key, "My task", null);
        Select(workspace, session.Key, "change-two", "two");
        Assert.Equal(before, Json.Encode(store.Read()));
        Assert.Equal("My task", catalog.List().Single().Identity.Name);
        catalog.Rename(session.Key, "", null);
        Assert.Equal("change-two", catalog.List().Single().Identity.Name);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ProviderIngestionFollowsConfirmedWork(string provider)
    {
        using var workspace = new TestWorkspace();
        var change = Path.Combine(workspace.Root, "openspec", "changes", "task-one");
        Directory.CreateDirectory(change);
        var target = Path.Combine(change, "tasks.md");
        // Claude reports Write.file_path; Codex reports apply_patch with the patch in tool_input.command.
        object input = provider == "codex"
            ? new { command = "*** Begin Patch\n*** Update File: " + target.Replace('\\', '/') + "\n+x\n*** End Patch" }
            : new { file_path = target };
        string Hook(string name) => JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd = workspace.Root,
            hook_event_name = name,
            tool_name = provider == "codex" ? "apply_patch" : "Write",
            tool_use_id = "one",
            tool_input = input,
            tool_response = name != "PostToolUse" ? null : provider == "codex" ? (object)"Success." : new { type = "update" }
        });
        var ingestor = new HookIngestor(workspace.Paths);
        ingestor.Ingest(provider, new StringReader(Hook("PreToolUse")));
        ingestor.Ingest(provider, new StringReader(Hook("PostToolUse")));
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
        Assert.Equal("saved-change", restored.ActiveWork);
        Assert.Equal(manualName ?? "saved-change", restored.Name);
        Assert.Equal(original.Palette, restored.Palette);
        Assert.Contains(ContentHash.Compute("saved-selection"), restored.SelectionReceipts);
    }
}
