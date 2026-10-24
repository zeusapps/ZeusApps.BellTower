using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Sessions;

// Working checkout resolution (tasks 3.1-3.3): launch directory, planning root/store and working checkout are
// separate facts. All repositories are real, isolated Git repositories with linked worktrees.
public sealed class CheckoutResolutionTests
{
    private static IdentityResolver Resolver(TestWorkspace workspace) => new(workspace.Paths, new CheckoutCache());

    private static void Work(TestWorkspace workspace, SessionState session, string launch, string change) =>
        new IdentityStore(workspace.Paths).Update(session.Key, launch, identity => { identity.ActiveWork = change; return true; });

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task LaunchInTheMainCheckoutShowsTheTicketWorktreeForUniqueWorkWithoutMovingOwnership(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var ticket = family.Add("DEP-7070", "login", "dep-7070-fix");
        family.Add("DEP-8080", "docs", "dep-8080-docs");
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission) with
        { Provider = provider, SessionId = "one", AttachmentId = session.AttachmentId, Directory = family.Main });
        var state = new StateStore(workspace.Paths);
        var before = state.Drain().Sessions[session.Key];
        Work(workspace, session, family.Main, "dep-7070-fix");

        var git = await Resolver(workspace).RefreshAsync(session);

        Assert.Equal("DEP-7070-login", git.Branch);
        Assert.Equal("DEP-7070", git.Worktree);
        var identity = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
        Assert.True(WorktreeFamily.Same(ticket, identity.WorkingCheckout));
        Assert.True(WorktreeFamily.Same(family.Main, identity.LaunchDirectory));
        Assert.Equal(CheckoutPolicy.UniqueWork, identity.CheckoutSource);
        // Navigation still targets the owning terminal: attachment and launch directory are untouched.
        var after = state.Read().Sessions[session.Key];
        Assert.Equal(before.AttachmentId, after.AttachmentId);
        Assert.Equal(before.Directory, after.Directory);
        Assert.Equal(before.Episode, after.Episode);
    }

    [Fact]
    public async Task WorkMappingPrefersTheTicketNamedWorktreeAndStaysAmbiguousOtherwise()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        // Every worktree of a repository holds the committed change folders, so the ticket-named folder is decisive.
        var seven = family.Add("DEP-7070", "login", "dep-7070-fix");
        IdentityFixtures.Change(family.Add("DEP-8080", "docs"), "dep-7070-fix");
        var session = IdentityFixtures.Session("claude", "one", family.Main);
        Work(workspace, session, family.Main, "dep-7070-fix");
        await Resolver(workspace).RefreshAsync(session);
        var identity = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
        Assert.True(WorktreeFamily.Same(seven, identity.WorkingCheckout));
        Assert.Equal(CheckoutPolicy.UniqueWork, identity.CheckoutSource);

        // No ticket in the change name and two candidate worktrees: explicit, honest, never arbitrary.
        var alpha = family.AddNamed("alpha", "alpha-branch", "shared-change");
        family.AddNamed("beta", "beta-branch", "shared-change");
        var other = IdentityFixtures.Session("codex", "two", family.Main);
        Work(workspace, other, family.Main, "shared-change");
        var git = await Resolver(workspace).RefreshAsync(other);
        var ambiguous = WorktreeFamily.Identity(workspace.Paths, other, family.Main);
        Assert.Equal(CheckoutPolicy.Ambiguous, ambiguous.CheckoutSource);
        Assert.True(WorktreeFamily.Same(family.Main, ambiguous.WorkingCheckout));
        Assert.Equal("main", git.Branch);

        // The user attaches one explicitly; clearing restores the honest ambiguity.
        new IdentityStore(workspace.Paths).Update(other.Key, family.Main, value => { value.CheckoutPin = alpha; return true; });
        var attached = await Resolver(workspace).RefreshAsync(other);
        Assert.Equal("alpha-branch", attached.Branch);
        Assert.Equal(CheckoutPolicy.User, WorktreeFamily.Identity(workspace.Paths, other, family.Main).CheckoutSource);
        new IdentityStore(workspace.Paths).Update(other.Key, family.Main, value => { value.CheckoutPin = null; return true; });
        await Resolver(workspace).RefreshAsync(other);
        Assert.Equal(CheckoutPolicy.Ambiguous, WorktreeFamily.Identity(workspace.Paths, other, family.Main).CheckoutSource);
    }

    [Fact]
    public async Task OnlyWorktreesOfTheLaunchRepositoryAreAcceptedFromProviderCwdAndUnrelatedRepositoriesNeedAnExplicitAttachForBothClients()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login");
        var unrelated = Path.Combine(workspace.Root, "unrelated-project");
        Directory.CreateDirectory(unrelated);
        GitHelper.Run(unrelated, "init", "--initial-branch", "elsewhere");
        var store = new IdentityStore(workspace.Paths);
        var catalog = new SessionCatalog(workspace.Paths);
        var resolver = Resolver(workspace);
        var sessions = new[] { "claude", "codex" }.Select(provider => IdentityFixtures.Session(provider, "one", family.Main)).ToArray();
        foreach (var session in sessions)
        {
            new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission) with
            { Provider = session.Provider, SessionId = "one", AttachmentId = session.AttachmentId, Directory = family.Main });
        }
        new StateStore(workspace.Paths).Drain();

        foreach (var session in sessions)
        {
            // A cwd in an unrelated repository is ignored: the badge keeps describing the launch checkout.
            store.Update(session.Key, family.Main, value => CheckoutEvidence.Observe(value, unrelated, true));
            var git = await resolver.RefreshAsync(session);
            var identity = store.Get(session.Key, family.Main);
            Assert.Equal(CheckoutPolicy.Launch, identity.CheckoutSource);
            Assert.True(WorktreeFamily.Same(family.Main, identity.WorkingCheckout));
            Assert.NotEqual("elsewhere", git.Branch);

            // A cwd in a worktree of the launch repository is accepted as provider evidence.
            store.Update(session.Key, family.Main, value => CheckoutEvidence.Observe(value, seven, true));
            Assert.Equal("DEP-7070-login", (await resolver.RefreshAsync(session)).Branch);
            Assert.Equal(CheckoutPolicy.Provider, store.Get(session.Key, family.Main).CheckoutSource);

            // The unrelated repository is reachable only through an explicit attach, which outranks provider evidence.
            catalog.Configure(session.Key, unrelated, null, false);
            Assert.Equal("elsewhere", (await resolver.RefreshAsync(session)).Branch);
            Assert.Equal(CheckoutPolicy.User, store.Get(session.Key, family.Main).CheckoutSource);
            catalog.Configure(session.Key, "", null, false);
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task PriorityIsUserPinThenVerifiedProviderEvidenceThenUniqueWorkThenLaunch(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login", "dep-7070-fix");
        var eight = family.Add("DEP-8080", "docs");
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        Work(workspace, session, family.Main, "dep-7070-fix");
        var resolver = Resolver(workspace);

        // Launch cwd reported by the provider is not evidence of a different checkout.
        Directory.CreateDirectory(Path.Combine(family.Main, "src"));
        store.Update(session.Key, family.Main, value => CheckoutEvidence.Observe(value, Path.Combine(family.Main, "src"), true));
        await resolver.RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.UniqueWork, store.Get(session.Key, family.Main).CheckoutSource);

        // An unrelated repository is not a checkout of this repository family and proves nothing.
        var unrelated = Path.Combine(workspace.Root, "unrelated");
        Directory.CreateDirectory(unrelated);
        GitHelper.Run(unrelated, "init", "--initial-branch", "elsewhere");
        store.Update(session.Key, family.Main, value => CheckoutEvidence.Observe(value, unrelated, true));
        await resolver.RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.UniqueWork, store.Get(session.Key, family.Main).CheckoutSource);

        // A verified worktree cwd (even in a subdirectory) outranks the work mapping.
        var deep = Directory.CreateDirectory(Path.Combine(eight, "src", "deep")).FullName;
        store.Update(session.Key, family.Main, value => CheckoutEvidence.Observe(value, deep, true));
        var git = await resolver.RefreshAsync(session);
        var identity = store.Get(session.Key, family.Main);
        Assert.Equal(CheckoutPolicy.Provider, identity.CheckoutSource);
        Assert.True(WorktreeFamily.Same(eight, identity.WorkingCheckout));
        Assert.Equal("DEP-8080-docs", git.Branch);

        // The user's pin outranks everything; clearing it returns to the provider evidence.
        store.Update(session.Key, family.Main, value => { value.CheckoutPin = seven; return true; });
        Assert.Equal("DEP-7070-login", (await resolver.RefreshAsync(session)).Branch);
        Assert.Equal(CheckoutPolicy.User, store.Get(session.Key, family.Main).CheckoutSource);
        store.Update(session.Key, family.Main, value => { value.CheckoutPin = null; return true; });
        await resolver.RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.Provider, store.Get(session.Key, family.Main).CheckoutSource);

        // Back in the launch checkout the provider evidence disappears and the work mapping decides again.
        store.Update(session.Key, family.Main, value => { value.ProviderCwd = family.Main; return true; });
        await resolver.RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.UniqueWork, store.Get(session.Key, family.Main).CheckoutSource);
    }

    [Fact]
    public void OnlyMainThreadAbsoluteProviderDirectoriesAreRecordedAndRepeatsAreIdempotent()
    {
        var identity = new SessionIdentity { Key = "claude:one", Palette = Palette.Create() };
        Assert.False(CheckoutEvidence.Observe(identity, Path.Combine(Path.GetTempPath(), "sub"), mainThread: false));
        Assert.False(CheckoutEvidence.Observe(identity, "relative/path", true));
        Assert.False(CheckoutEvidence.Observe(identity, "", true));
        Assert.Null(identity.ProviderCwd);
        var directory = Path.Combine(Path.GetTempPath(), "main-thread");
        Assert.True(CheckoutEvidence.Observe(identity, directory, true));
        Assert.False(CheckoutEvidence.Observe(identity, directory, true));
        Assert.Equal(directory, identity.ProviderCwd);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task HookIngestionRecordsTheProviderCwdAndIgnoresSubagents(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var eight = family.Add("DEP-8080", "docs");
        string Start(string cwd, string? agent = null) => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["session_id"] = "one",
            ["cwd"] = cwd,
            ["hook_event_name"] = "SessionStart",
            ["source"] = "startup",
            ["agent_id"] = agent
        }.Where(pair => pair.Value is not null).ToDictionary());
        var ingestor = new HookIngestor(workspace.Paths);
        ingestor.Ingest(provider, new StringReader(Start(family.Main)));
        var store = new IdentityStore(workspace.Paths);
        var key = provider + ":one";
        Assert.True(WorktreeFamily.Same(family.Main, store.Get(key, family.Main).LaunchDirectory));
        ingestor.Ingest(provider, new StringReader(Start(eight, agent: "sub-1")));
        Assert.True(WorktreeFamily.Same(family.Main, store.Get(key, family.Main).ProviderCwd));
        ingestor.Ingest(provider, new StringReader(Start(eight)));
        var identity = store.Get(key, family.Main);
        Assert.True(WorktreeFamily.Same(eight, identity.ProviderCwd));
        Assert.True(WorktreeFamily.Same(family.Main, identity.LaunchDirectory));
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var git = await Resolver(workspace).RefreshAsync(session);
        Assert.Equal("DEP-8080", git.Worktree);
        Assert.Equal(CheckoutPolicy.Provider, store.Get(key, family.Main).CheckoutSource);
    }

    [Fact]
    public async Task MissingPinAndMissingProviderCheckoutFallBackHonestlyToTheLaunchDirectory()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login");
        var session = IdentityFixtures.Session("claude", "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        store.Update(session.Key, family.Main, value => { value.CheckoutPin = seven; return true; });
        GitHelper.Run(family.Main, "worktree", "remove", "--force", seven);
        var git = await Resolver(workspace).RefreshAsync(session);
        Assert.Equal("main", git.Branch);
        var identity = store.Get(session.Key, family.Main);
        Assert.Equal(CheckoutPolicy.PinMissing, identity.CheckoutSource);
        Assert.True(WorktreeFamily.Same(family.Main, identity.WorkingCheckout));
        Assert.True(WorktreeFamily.Same(seven, identity.CheckoutPin), "the pin is kept so the user decides when to clear it");

        store.Update(session.Key, family.Main, value => { value.CheckoutPin = null; value.ProviderCwd = seven; return true; });
        await Resolver(workspace).RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.Launch, store.Get(session.Key, family.Main).CheckoutSource);
    }

    [Fact]
    public async Task DetachedHeadWorktreeReportsAShortCommitAndTheWorktreeName()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var detached = Path.Combine(family.Worktrees, "DEP-9090");
        Directory.CreateDirectory(family.Worktrees);
        GitHelper.Run(family.Main, "worktree", "add", "--detach", detached);
        IdentityFixtures.Change(detached, "dep-9090-detached");
        var session = IdentityFixtures.Session("codex", "one", family.Main);
        Work(workspace, session, family.Main, "dep-9090-detached");
        var git = await Resolver(workspace).RefreshAsync(session);
        Assert.Equal("DEP-9090", git.Worktree);
        Assert.Matches("^[0-9a-f]{7,}$", git.Branch!);
        var identity = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
        Assert.Equal(git.Branch, identity.WorkBranch);
        Assert.Equal("dep-9090-detached", identity.ActiveWork);
        // The process-free reader agrees with Git for detached HEAD.
        Assert.Equal(git, GitHeadReader.Read(detached) with { Branch = git.Branch });
        Assert.StartsWith(GitHeadReader.Read(detached).Branch!, git.Branch, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ExternalPlanningStoresNeverBecomeGitContextOrCheckouts(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        // A planning store outside every repository: it holds the change folder but is not a working checkout.
        var store = Path.Combine(workspace.Root, "planning-store");
        IdentityFixtures.Change(store, "external-change", scaffold: true);
        // The workspace lives inside this repository; a broken gitdir marker makes the store a non-checkout.
        File.WriteAllText(Path.Combine(store, ".git"), "gitdir: missing");
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var identities = new IdentityStore(workspace.Paths);
        identities.Update(session.Key, family.Main, value =>
        {
            value.ActiveWork = "external-change";
            value.SelectionRoot = store;
            value.SelectionStore = store;
            return true;
        });
        var git = await Resolver(workspace).RefreshAsync(session);
        var identity = identities.Get(session.Key, family.Main);
        Assert.Equal("main", git.Branch);
        Assert.True(WorktreeFamily.Same(family.Main, identity.WorkingCheckout));
        Assert.True(WorktreeFamily.Same(family.Main, identity.LaunchDirectory));
        Assert.Equal(CheckoutPolicy.Launch, identity.CheckoutSource);
        Assert.Equal(store, identity.SelectionRoot);

        // An explicit attach cannot target a non-Git directory either.
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission) with
        { Provider = provider, SessionId = "one", AttachmentId = session.AttachmentId, Directory = family.Main });
        new StateStore(workspace.Paths).Drain();
        var catalog = new SessionCatalog(workspace.Paths);
        Assert.Throws<ArgumentException>(() => catalog.Configure(session.Key, store, null, false));
        Assert.Throws<ArgumentException>(() => catalog.Configure(session.Key, "relative", null, false));
        Assert.Null(identities.Get(session.Key, family.Main).CheckoutPin);
    }

    [Fact]
    public void AttachAndClearThroughTheCatalogUseTheWorktreeRootAndMeanTheSameForBothProviders()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login");
        Directory.CreateDirectory(Path.Combine(seven, "src"));
        foreach (var provider in new[] { "claude", "codex" })
        {
            new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission) with
            { Provider = provider, SessionId = "one", AttachmentId = "attachment-" + provider, Directory = family.Main });
        }
        new StateStore(workspace.Paths).Drain();
        var catalog = new SessionCatalog(workspace.Paths);
        foreach (var provider in new[] { "claude", "codex" })
        {
            var key = provider + ":one";
            var pinned = catalog.Configure(key, Path.Combine(seven, "src"), null, false);
            Assert.True(WorktreeFamily.Same(seven, pinned.CheckoutPin));
            var summary = catalog.List().Single(row => row.Key == key);
            Assert.Equal(CheckoutPolicy.User, summary.CheckoutSource);
            Assert.True(WorktreeFamily.Same(seven, summary.Checkout));
            Assert.Null(catalog.Configure(key, "", null, false).CheckoutPin);
            Assert.NotEqual(CheckoutPolicy.User, catalog.List().Single(row => row.Key == key).CheckoutSource);
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task BranchChangeInvalidatesOnlyBranchBoundWorkAndKeepsPinsTitlesAndPalette(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login", "dep-7070-fix");
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        var palette = store.Get(session.Key, family.Main).Palette;
        var resolver = Resolver(workspace);
        await resolver.RefreshAsync(session);
        Assert.Equal("main", store.Get(session.Key, family.Main).ObservedBranch);
        store.Update(session.Key, family.Main, value =>
        {
            value.ManualName = "Pinned";
            value.Change = "pinned-change";
            value.NativeTitle = "Native";
            value.NativeTitleSource = "verified-user-name";
            WorkPolicy.Apply(value, new("dep-7070-fix", family.Main, "write", false, DateTimeOffset.UtcNow, -1));
            return true;
        });
        // Pinned change means the work mapping uses the pin; clear it so the active work drives the checkout here.
        store.Update(session.Key, family.Main, value => { value.Change = null; return true; });
        await resolver.RefreshAsync(session);
        var moved = store.Get(session.Key, family.Main);
        // Moving to the mapped checkout rebinds the evidence instead of discarding it.
        Assert.True(WorktreeFamily.Same(seven, moved.WorkingCheckout));
        Assert.Equal("dep-7070-fix", moved.ActiveWork);
        Assert.Equal("DEP-7070-login", moved.WorkBranch);
        Assert.Equal("DEP-7070-login", moved.ObservedBranch);
        store.Update(session.Key, family.Main, value => { value.Change = "pinned-change"; value.CheckoutPin = seven; return true; });

        // The same checkout switching branch invalidates the branch-bound automatic evidence only.
        GitHelper.Run(seven, "symbolic-ref", "HEAD", "refs/heads/DEP-7070-other");
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var after = store.Get(session.Key, family.Main);
        Assert.Null(after.ActiveWork);
        Assert.Null(after.WorkBranch);
        Assert.Equal("Pinned", after.ManualName);
        Assert.Equal("pinned-change", after.Change);
        Assert.Equal("Native", after.NativeTitle);
        Assert.Equal(palette, after.Palette);
        Assert.Equal("DEP-7070-other", after.ObservedBranch);
    }

    [Fact]
    public async Task AFailedBranchReadNeverInvalidatesWork()
    {
        using var workspace = new TestWorkspace();
        var directory = Path.Combine(workspace.Root, "plain");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".git"), "gitdir: missing");
        var session = IdentityFixtures.Session("claude", "one", directory);
        var store = new IdentityStore(workspace.Paths);
        store.Update(session.Key, directory, value =>
        {
            value.ActiveWork = "kept";
            value.WorkBranch = "feature";
            value.ObservedBranch = "feature";
            return true;
        });
        await Resolver(workspace).RefreshAsync(session);
        var identity = store.Get(session.Key, directory);
        Assert.Equal("kept", identity.ActiveWork);
        Assert.Equal("feature", identity.WorkBranch);
    }

    [Fact]
    public async Task MixedSessionsInOneLaunchDirectoryResolveIndependentlyWithoutCrossReuse()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login", "dep-7070-fix");
        var eight = family.Add("DEP-8080", "docs", "dep-8080-docs");
        var claude = IdentityFixtures.Session("claude", "one", family.Main);
        var codex = IdentityFixtures.Session("codex", "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        Work(workspace, claude, family.Main, "dep-7070-fix");
        Work(workspace, codex, family.Main, "dep-8080-docs");
        var resolver = Resolver(workspace);
        var claudeGit = await resolver.RefreshAsync(claude);
        var codexGit = await resolver.RefreshAsync(codex);
        Assert.Equal("DEP-7070", claudeGit.Worktree);
        Assert.Equal("DEP-8080", codexGit.Worktree);
        Assert.True(WorktreeFamily.Same(seven, store.Get(claude.Key, family.Main).WorkingCheckout));
        Assert.True(WorktreeFamily.Same(eight, store.Get(codex.Key, family.Main).WorkingCheckout));

        var codexFile = Directory.GetFiles(workspace.Paths.Identities, "*.json").Single(file => File.ReadAllText(file).Contains("codex:one"));
        var bytes = File.ReadAllBytes(codexFile);
        store.Update(claude.Key, family.Main, value => { value.ManualName = "Claude only"; return true; });
        await resolver.RefreshAsync(claude);
        Assert.Equal(bytes, File.ReadAllBytes(codexFile));
        Assert.Equal("dep-8080-docs", store.Get(codex.Key, family.Main).Name);
    }

    [Fact]
    public void CheckoutPolicyChoosesInPriorityOrder()
    {
        string[] roots = ["C:/repo", "C:/repo-wt/DEP-1", "C:/repo-wt/DEP-2"];
        bool Has(string root) => root.EndsWith("DEP-2", StringComparison.Ordinal);
        bool Exists(string _) => true;
        Assert.Equal(new CheckoutPolicy.Choice("C:/pin", "user"), CheckoutPolicy.Choose("C:/pin", "C:/repo-wt/DEP-1", "x", roots, Has, Exists));
        Assert.Equal(new CheckoutPolicy.Choice("C:/repo-wt/DEP-1", "provider"), CheckoutPolicy.Choose(null, "C:/repo-wt/DEP-1", "x", roots, Has, Exists));
        Assert.Equal(new CheckoutPolicy.Choice("C:/repo-wt/DEP-2", "unique-work"), CheckoutPolicy.Choose(null, null, "feature", roots, Has, Exists));
        Assert.Equal(new CheckoutPolicy.Choice("C:/repo-wt/DEP-1", "unique-work"), CheckoutPolicy.Choose(null, null, "dep-1-work", roots, Has, Exists));
        Assert.Equal(new CheckoutPolicy.Choice(null, "ambiguous: attach a checkout"), CheckoutPolicy.Choose(null, null, "feature", roots, _ => true, Exists));
        Assert.Equal(new CheckoutPolicy.Choice(null, "launch"), CheckoutPolicy.Choose(null, null, null, roots, Has, Exists));
        Assert.Equal(new CheckoutPolicy.Choice(null, CheckoutPolicy.PinMissing), CheckoutPolicy.Choose("C:/gone", "C:/repo-wt/DEP-1", "x", roots, Has, _ => false));
    }
}
