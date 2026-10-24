using System.Text.Json;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

// Live hook adapters for Claude Code and Codex feeding the shared work policy.
public sealed class WorkEvidenceTests
{
    private static JsonDocument Hook(string root, string tool, object input, string operation = "op", string hook = "PostToolUse",
        object? response = null, string? agent = null, string? cwd = null) => JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "one",
            cwd = cwd ?? root,
            hook_event_name = hook,
            tool_name = tool,
            tool_use_id = operation,
            agent_id = agent,
            tool_input = input,
            tool_response = hook == "PostToolUse" ? response ?? new { } : null
        }));

    private static string ChangeFile(string root, string change, string file = "proposal.md") =>
        IdentityFixtures.Slashed(Path.Combine(root, "openspec", "changes", change, file));

    [Theory]
    [InlineData("Write", "file_path")]
    [InlineData("Edit", "file_path")]
    [InlineData("MultiEdit", "file_path")]
    [InlineData("NotebookEdit", "notebook_path")]
    public void ClaudeEditToolsSelectTheNonArchiveChangeAndKeepItsRoot(string tool, string property)
    {
        using var workspace = new TestWorkspace();
        IdentityFixtures.Change(workspace.Root, "change-a");
        using var hook = Hook(workspace.Root, tool, new Dictionary<string, string> { [property] = ChangeFile(workspace.Root, "change-a") });
        var selection = OpenSpecSelectionReader.Read(hook.RootElement);
        Assert.NotNull(selection);
        Assert.Equal("change-a", selection.Change);
        Assert.Equal("edit", selection.Verb);
        Assert.Equal(IdentityFixtures.Slashed(workspace.Root), selection.WorkRoot);
        Assert.Equal(selection.WorkRoot, OpenSpecSelectionReader.SuccessfulRoot(hook.RootElement, selection));
    }

    [Fact]
    public void RelativeEditPathsResolveAgainstTheHookDirectory()
    {
        using var workspace = new TestWorkspace();
        using var hook = Hook(workspace.Root, "Edit", new { file_path = "openspec/changes/change-a/tasks.md" });
        Assert.Equal("change-a", OpenSpecSelectionReader.Read(hook.RootElement)?.Change);
        using var unrooted = JsonDocument.Parse("""{"hook_event_name":"PostToolUse","tool_name":"Edit","tool_use_id":"x","tool_input":{"file_path":"openspec/changes/a/x.md"},"tool_response":{}}""");
        Assert.Null(OpenSpecSelectionReader.Read(unrooted.RootElement));
    }

    [Theory]
    [InlineData("Update File")]
    [InlineData("Add File")]
    [InlineData("Delete File")]
    [InlineData("Move to")]
    public void CodexPatchFormsSelectTheSameEvidenceAsClaudeEdits(string header)
    {
        using var workspace = new TestWorkspace();
        var patch = "*** Begin Patch\n*** " + header + ": " + ChangeFile(workspace.Root, "change-a", "tasks.md") + "\n+x\n*** End Patch";
        using var hook = Hook(workspace.Root, "apply_patch", new { command = patch }, response: "Success.");
        var selection = OpenSpecSelectionReader.Read(hook.RootElement);
        Assert.NotNull(selection);
        Assert.Equal("change-a", selection.Change);
        Assert.Equal("edit", selection.Verb);
        Assert.Equal(IdentityFixtures.Slashed(workspace.Root), selection.WorkRoot);
    }

    [Fact]
    public void CodexRelativePatchPathsUseTheHookDirectoryAndUnrelatedFilesAreIgnored()
    {
        using var workspace = new TestWorkspace();
        var patch = "*** Begin Patch\n*** Update File: src/other.cs\n+x\n*** Update File: openspec/changes/change-a/design.md\n+y\n*** End Patch";
        using var hook = Hook(workspace.Root, "apply_patch", new { command = patch });
        Assert.Equal("change-a", OpenSpecSelectionReader.Read(hook.RootElement)?.Change);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ArchiveAmbiguousAndNonChangePathsNeverSelect(string provider)
    {
        using var workspace = new TestWorkspace();
        JsonDocument Edit(params string[] files) => provider == "claude"
            ? Hook(workspace.Root, "Write", new { file_path = files[0] })
            : Hook(workspace.Root, "apply_patch", new { command = "*** Begin Patch\n" + string.Join("\n", files.Select(f => "*** Update File: " + f)) + "\n*** End Patch" });
        using var archived = Edit(ChangeFile(workspace.Root, "archive", "2026-10-01-old/proposal.md"));
        using var specs = Edit(IdentityFixtures.Slashed(Path.Combine(workspace.Root, "openspec", "specs", "x", "spec.md")));
        using var bad = Edit(ChangeFile(workspace.Root, "Not_A_Name"));
        Assert.Null(OpenSpecSelectionReader.Read(archived.RootElement));
        Assert.Null(OpenSpecSelectionReader.Read(specs.RootElement));
        Assert.Null(OpenSpecSelectionReader.Read(bad.RootElement));
        if (provider == "codex")
        {
            using var two = Edit(ChangeFile(workspace.Root, "change-a"), ChangeFile(workspace.Root, "change-b"));
            Assert.Null(OpenSpecSelectionReader.Read(two.RootElement));
            using var twice = Edit(ChangeFile(workspace.Root, "change-a"), ChangeFile(workspace.Root, "change-a", "tasks.md"));
            Assert.Equal("change-a", OpenSpecSelectionReader.Read(twice.RootElement)?.Change);
        }
    }

    [Theory]
    [InlineData("opsx:apply", false)]
    [InlineData("opsx:propose", false)]
    [InlineData("openspec-apply-change", false)]
    [InlineData("opsx:explore", true)]
    [InlineData("openspec-explore", true)]
    [InlineData("explain-change", true)]
    public void WorkflowSkillsNamingAnExistingChangeAreWorkUnlessExplanatory(string skill, bool weak)
    {
        using var workspace = new TestWorkspace();
        IdentityFixtures.Change(workspace.Root, "change-a");
        var nested = Path.Combine(workspace.Root, "src", "deep");
        Directory.CreateDirectory(nested);
        using var hook = Hook(workspace.Root, "Skill", new { skill, args = "change-a" }, cwd: nested);
        var selection = OpenSpecSelectionReader.Read(hook.RootElement);
        Assert.NotNull(selection);
        Assert.Equal("change-a", selection.Change);
        Assert.Equal("skill", selection.Verb);
        Assert.Equal(weak, selection.Weak);
        Assert.Equal(workspace.Root, selection.WorkRoot);
    }

    [Theory]
    [InlineData("opsx:apply", "missing-change")]
    [InlineData("opsx:apply", "change-a extra words")]
    [InlineData("opsx:apply", "../change-a")]
    [InlineData("commit", "change-a")]
    public void SkillsWithoutALiteralExistingChangeDoNotSelect(string skill, string args)
    {
        using var workspace = new TestWorkspace();
        IdentityFixtures.Change(workspace.Root, "change-a");
        using var hook = Hook(workspace.Root, "Skill", new { skill, args });
        Assert.Null(OpenSpecSelectionReader.Read(hook.RootElement));
    }

    [Fact]
    public void CustomMainAgentIsMainAndSubagentFixtureIsExcluded()
    {
        using var workspace = new TestWorkspace();
        using var main = JsonDocument.Parse(IdentityFixtures.Load("claude-2.1.306-main-posttooluse.json", workspace.Root));
        using var subagent = JsonDocument.Parse(IdentityFixtures.Load("claude-2.1.306-subagent-posttooluse.json", workspace.Root));
        Assert.Equal("change-a", OpenSpecSelectionReader.Read(main.RootElement)?.Change);
        Assert.Null(OpenSpecSelectionReader.Read(subagent.RootElement));
        using var shellAgent = Hook(workspace.Root, "Bash", new { command = "openspec new change change-a" }, agent: "agent-sub");
        Assert.Null(OpenSpecSelectionReader.Read(shellAgent.RootElement));
    }

    [Theory]
    [InlineData("{\"is_error\":true}")]
    [InlineData("{\"exit_code\":1}")]
    [InlineData("{\"exit_code\":\"1\"}")]
    [InlineData("{\"interrupted\":true}")]
    public void FailedOrInterruptedResultsDoNotProveWork(string response)
    {
        using var workspace = new TestWorkspace();
        using var hook = Hook(workspace.Root, "Write", new { file_path = ChangeFile(workspace.Root, "change-a") }, response: JsonDocument.Parse(response).RootElement);
        var selection = OpenSpecSelectionReader.Read(hook.RootElement)!;
        Assert.Null(OpenSpecSelectionReader.SuccessfulRoot(hook.RootElement, selection));
    }

    private static readonly string[] ShellShapes =
    [
        "openspec new change shape-change",
        "cd \"{W}\" && openspec new change shape-change",
        "cd '{W}'; openspec new change shape-change",
        "Set-Location '{W}'; openspec new change shape-change",
        "cd \"{W}\" && openspec new change shape-change | cat",
        "pwsh -Command \"Set-Location '{W}'; openspec new change shape-change | Out-String\"",
        "powershell -NoProfile -Command \"cd '{W}' && openspec new change shape-change\"",
        "cd \"{W}\" && openspec new change shape-change > \"{O}\""
    ];

    private static void Observe(TestWorkspace workspace, string provider, string key, string cwd, string command, object response, string operation = "shell")
    {
        // Claude reports a Bash/PowerShell tool_response object; Codex reports a plain string.
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        var tool = provider == "claude" && command.StartsWith("Set-Location", StringComparison.Ordinal) ? "PowerShell" : "Bash";
        using var pre = Hook(workspace.Root, tool, new { command }, operation, "PreToolUse", cwd: cwd);
        using var post = Hook(workspace.Root, tool, new { command }, operation, response: response, cwd: cwd);
        observer.Observe(key, cwd, pre.RootElement);
        observer.Observe(key, cwd, post.RootElement);
    }

    [Theory]
    [InlineData("claude", 0)]
    [InlineData("claude", 1)]
    [InlineData("claude", 2)]
    [InlineData("claude", 3)]
    [InlineData("claude", 4)]
    [InlineData("claude", 5)]
    [InlineData("claude", 6)]
    [InlineData("claude", 7)]
    [InlineData("codex", 0)]
    [InlineData("codex", 1)]
    [InlineData("codex", 2)]
    [InlineData("codex", 3)]
    [InlineData("codex", 4)]
    [InlineData("codex", 5)]
    [InlineData("codex", 6)]
    [InlineData("codex", 7)]
    public void SupportedShellShapesUseTheLiteralDirectoryAsIndependentRootEvidence(string provider, int shape)
    {
        using var workspace = new TestWorkspace();
        var launch = Path.Combine(workspace.Root, "launch");
        var worktree = Path.Combine(workspace.Root, "work tree");
        Directory.CreateDirectory(launch);
        IdentityFixtures.Change(worktree, "shape-change", scaffold: true);
        var key = provider + ":one";
        var directory = shape == 0 ? worktree : launch;
        var command = ShellShapes[shape].Replace("{W}", worktree).Replace("{O}", Path.Combine(workspace.Root, "out.txt"));
        var redirected = shape == 7;
        object response = redirected ? new { stdout = "" } : provider == "codex"
            ? "Created change 'shape-change' at openspec/changes/shape-change/"
            : new { stdout = "Created change 'shape-change' at openspec/changes/shape-change/", stderr = "", interrupted = false };
        Observe(workspace, provider, key, directory, command, response);
        var identity = new IdentityStore(workspace.Paths).Get(key, launch);
        Assert.Equal("shape-change", identity.ActiveWork);
        Assert.Equal(worktree, identity.SelectionRoot);
        Assert.Equal("new", identity.WorkSource);
    }

    [Theory]
    [InlineData("cd \"{W}\" && openspec new change shape-change && echo done")]
    [InlineData("cd \"{W}\" && cd \"{L}\" && openspec new change shape-change")]
    [InlineData("cd $(Get-Location) && openspec new change shape-change")]
    [InlineData("cd `pwd` && openspec new change shape-change")]
    [InlineData("cd $target && openspec new change shape-change")]
    [InlineData("cd \"{W}\" && openspec new change $name")]
    [InlineData("cd \"{W}\" && openspec new change shape-change 2>&1")]
    [InlineData("cd \"{W}\" && openspec new change shape-change | Out-File x")]
    [InlineData("openspec new change shape-change")]
    public void SubstitutionAmbiguousAndRootlessShapesDoNotSelect(string template)
    {
        using var workspace = new TestWorkspace();
        var launch = Path.Combine(workspace.Root, "launch");
        var worktree = Path.Combine(workspace.Root, "work tree");
        Directory.CreateDirectory(launch);
        IdentityFixtures.Change(worktree, "shape-change", scaffold: true);
        Observe(workspace, "claude", "claude:one", launch, template.Replace("{W}", worktree).Replace("{L}", launch),
            new { stdout = "Created change 'shape-change' at openspec/changes/shape-change/" });
        Assert.Null(new IdentityStore(workspace.Paths).Get("claude:one", launch).ActiveWork);
    }

    [Theory]
    [InlineData("{\"is_error\":true,\"stdout\":\"Created change 'shape-change' at x\"}")]
    [InlineData("{\"exit_code\":1,\"stdout\":\"Created change 'shape-change' at x\"}")]
    [InlineData("{\"interrupted\":true,\"stdout\":\"Created change 'shape-change' at x\"}")]
    [InlineData("{\"stdout\":\"Error: change already exists\"}")]
    public void FailedShellCommandsDoNotSelect(string response)
    {
        using var workspace = new TestWorkspace();
        var worktree = Path.Combine(workspace.Root, "work tree");
        IdentityFixtures.Change(worktree, "shape-change", scaffold: true);
        Observe(workspace, "claude", "claude:one", workspace.Root, "cd \"" + worktree + "\" && openspec new change shape-change", JsonDocument.Parse(response).RootElement);
        Assert.Null(new IdentityStore(workspace.Paths).Get("claude:one", workspace.Root).ActiveWork);
    }

    [Fact]
    public void RedirectedCreationNeedsAScaffoldCreatedAtTheObservedTime()
    {
        using var workspace = new TestWorkspace();
        var directory = IdentityFixtures.Change(workspace.Root, "shape-change", scaffold: true);
        File.SetCreationTimeUtc(Path.Combine(directory, ".openspec.yaml"), DateTime.UtcNow.AddHours(-2));
        Observe(workspace, "codex", "codex:one", workspace.Root, "openspec new change shape-change > out.txt", new { stdout = "" });
        Assert.Null(new IdentityStore(workspace.Paths).Get("codex:one", workspace.Root).ActiveWork);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void IngressCaptureCarriesTheLiteralDirectoryToTheConsumer(string provider)
    {
        using var workspace = new TestWorkspace();
        var launch = Path.Combine(workspace.Root, "launch");
        var worktree = Path.Combine(workspace.Root, "work tree");
        Directory.CreateDirectory(launch);
        IdentityFixtures.Change(worktree, "shape-change", scaffold: true);
        using var input = Hook(launch, "Bash", new { command = "cd \"" + worktree + "\" && openspec new change shape-change" }, "capture",
            response: provider == "codex" ? "Created change 'shape-change' at openspec/changes/shape-change/" : new { stdout = "Created change 'shape-change' at x" });
        var envelope = CaptureFactory.Create(provider, input.RootElement, new("attachment", provider, null, new(), launch, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow)!;
        Assert.True(envelope.CreatedScaffold);
        Assert.Equal(worktree, envelope.Selection!.Directory);
        CaptureSpool.Write(workspace.Paths.Root, envelope);
        new StateStore(workspace.Paths).Drain();
        var identity = new IdentityStore(workspace.Paths).Get(provider + ":one", launch);
        Assert.Equal("shape-change", identity.ActiveWork);
        Assert.Equal(worktree, identity.SelectionRoot);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void InspectingAnotherChangeNeverReplacesEstablishedWork(string provider)
    {
        using var workspace = new TestWorkspace();
        var key = provider + ":one";
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b", scaffold: true);
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        var work = provider == "claude"
            ? Hook(workspace.Root, "Write", new { file_path = ChangeFile(workspace.Root, "change-a") }, "work-a")
            : Hook(workspace.Root, "apply_patch", new { command = "*** Begin Patch\n*** Update File: " + ChangeFile(workspace.Root, "change-a") + "\n*** End Patch" }, "work-a");
        observer.Observe(key, workspace.Root, work.RootElement);
        var inspections = new List<JsonDocument>
        {
            Hook(workspace.Root, "Bash", new { command = "openspec status --change change-b --json" }, "i1"),
            Hook(workspace.Root, "Bash", new { command = "cd \"" + workspace.Root + "\" && openspec instructions apply --change change-b --json | cat" }, "i2"),
            Hook(workspace.Root, "Read", new { file_path = ChangeFile(workspace.Root, "change-b") }, "i5"),
            Hook(workspace.Root, "Bash", new { command = "openspec new change change-b" }, "i6", response: new { exit_code = 1, stdout = "exists" })
        };
        if (provider == "claude")
        {
            // Skills are a Claude Code tool; Codex workflow forms are not part of its verified hook contract.
            inspections.Add(Hook(workspace.Root, "Skill", new { skill = "opsx:explore", args = "change-b" }, "i3"));
            inspections.Add(Hook(workspace.Root, "Skill", new { skill = "explain-change", args = "change-b" }, "i4"));
        }
        foreach (var hook in inspections)
        {
            observer.Observe(key, workspace.Root, hook.RootElement);
            hook.Dispose();
        }
        var identity = new IdentityStore(workspace.Paths).Get(key, workspace.Root);
        Assert.Equal("change-a", identity.ActiveWork);
        Assert.False(identity.WorkWeak);
        Assert.Equal("change-a", identity.Name);
    }

    [Fact]
    public void WeakContextFillsAnEmptyAssociationAndIsReplacedByRealWork()
    {
        using var workspace = new TestWorkspace();
        var key = "claude:one";
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        IdentityFixtures.Change(workspace.Root, "change-c");
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        using var status = Hook(workspace.Root, "Bash", new { command = "openspec status --change change-b" }, "s1");
        observer.Observe(key, workspace.Root, status.RootElement);
        Assert.Null(new IdentityStore(workspace.Paths).Get(key, workspace.Root).ActiveWork);
        using var explore = Hook(workspace.Root, "Skill", new { skill = "opsx:explore", args = "change-b" }, "e1");
        observer.Observe(key, workspace.Root, explore.RootElement);
        var weak = new IdentityStore(workspace.Paths).Get(key, workspace.Root);
        Assert.Equal("change-b", weak.ActiveWork);
        Assert.True(weak.WorkWeak);
        using var other = Hook(workspace.Root, "Skill", new { skill = "explain-change", args = "change-c" }, "e2");
        observer.Observe(key, workspace.Root, other.RootElement);
        Assert.Equal("change-b", new IdentityStore(workspace.Paths).Get(key, workspace.Root).ActiveWork);
        using var work = Hook(workspace.Root, "Write", new { file_path = ChangeFile(workspace.Root, "change-a") }, "w1");
        observer.Observe(key, workspace.Root, work.RootElement);
        var strong = new IdentityStore(workspace.Paths).Get(key, workspace.Root);
        Assert.Equal("change-a", strong.ActiveWork);
        Assert.False(strong.WorkWeak);
        using var again = Hook(workspace.Root, "Skill", new { skill = "opsx:explore", args = "change-c" }, "e3");
        observer.Observe(key, workspace.Root, again.RootElement);
        Assert.Equal("change-a", new IdentityStore(workspace.Paths).Get(key, workspace.Root).ActiveWork);
    }

    [Fact]
    public void MixedClaudeAndCodexSessionsInOneRepositoryKeepIndependentState()
    {
        using var workspace = new TestWorkspace();
        IdentityFixtures.Change(workspace.Root, "change-a");
        IdentityFixtures.Change(workspace.Root, "change-b");
        var observer = new OpenSpecSelectionObserver(workspace.Paths);
        var store = new IdentityStore(workspace.Paths);
        var claude = store.Get("claude:one", workspace.Root);
        var codex = store.Get("codex:one", workspace.Root);
        using var claudeWork = Hook(workspace.Root, "Write", new { file_path = ChangeFile(workspace.Root, "change-a") }, "c1");
        using var codexWork = Hook(workspace.Root, "apply_patch", new { command = "*** Begin Patch\n*** Add File: " + ChangeFile(workspace.Root, "change-b") + "\n*** End Patch" }, "x1");
        observer.Observe(claude.Key, workspace.Root, claudeWork.RootElement);
        observer.Observe(codex.Key, workspace.Root, codexWork.RootElement);
        using var claudeInspectsB = Hook(workspace.Root, "Bash", new { command = "openspec status --change change-b" }, "c2");
        using var codexInspectsA = Hook(workspace.Root, "Bash", new { command = "openspec instructions apply --change change-a --json" }, "x2");
        observer.Observe(claude.Key, workspace.Root, claudeInspectsB.RootElement);
        observer.Observe(codex.Key, workspace.Root, codexInspectsA.RootElement);
        Assert.Equal("change-a", store.Get(claude.Key, workspace.Root).Name);
        Assert.Equal("change-b", store.Get(codex.Key, workspace.Root).Name);
        Assert.NotEqual(claude.Palette, store.Get(codex.Key, workspace.Root).Palette);
    }
}
