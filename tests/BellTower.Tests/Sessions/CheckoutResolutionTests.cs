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
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
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
            new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
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
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
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
            new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
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

    // Task 2.8 (ANALYSIS-163): the shared policy keeps the checkout a unique work mapping verified when that same checkout changes
    // branch, refreshes its displayed context and clears the branch-bound subject; both providers take the same path.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ABranchChangeInTheVerifiedCheckoutKeepsItAsTheWorkingCheckoutAndRefreshesItsContext(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login", "dep-7070-fix");
        var eight = family.Add("DEP-8080", "docs", "dep-8080-docs");
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        var palette = store.Get(session.Key, family.Main).Palette;
        Work(workspace, session, family.Main, "dep-7070-fix");
        var resolver = Resolver(workspace);
        var git = await resolver.RefreshAsync(session);
        Assert.Equal("DEP-7070-login", git.Branch);
        Assert.Equal(CheckoutPolicy.UniqueWork, store.Get(session.Key, family.Main).CheckoutSource);

        GitHelper.Run(seven, "symbolic-ref", "HEAD", "refs/heads/hotfix-branch");
        git = await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var cleared = store.Get(session.Key, family.Main);
        Assert.Null(cleared.ActiveWork);
        Assert.Null(cleared.WorkSubject);
        Assert.NotNull(cleared.WorkFloorAt);
        Assert.True(WorktreeFamily.Same(seven, cleared.WorkingCheckout));
        Assert.Equal(CheckoutPolicy.Retained, cleared.CheckoutSource);
        Assert.Equal("hotfix-branch", git.Branch);
        Assert.Equal("DEP-7070", git.Worktree);
        // The subject is re-resolved from the new branch; it never keeps pointing at the old change.
        Assert.Equal("hotfix-branch", cleared.AutomaticSubject);
        Assert.Equal("hotfix-branch", cleared.Subject);
        Assert.Equal(palette, cleared.Palette);

        // Later refreshes (a fresh resolver, so nothing is cached) keep the checkout and follow its next branch change.
        GitHelper.Run(seven, "symbolic-ref", "HEAD", "refs/heads/second-hotfix");
        git = await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var later = store.Get(session.Key, family.Main);
        Assert.True(WorktreeFamily.Same(seven, later.WorkingCheckout));
        Assert.Equal(CheckoutPolicy.Retained, later.CheckoutSource);
        Assert.Equal("second-hotfix", git.Branch);
        Assert.Null(later.ActiveWork);

        // New work evidence elsewhere rebinds the checkout; the retained one never outranks work.
        Work(workspace, session, family.Main, "dep-8080-docs");
        git = await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var rebound = store.Get(session.Key, family.Main);
        Assert.True(WorktreeFamily.Same(eight, rebound.WorkingCheckout));
        Assert.Equal(CheckoutPolicy.UniqueWork, rebound.CheckoutSource);
        Assert.Equal("DEP-8080-docs", git.Branch);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ALaunchFallbackAndAmbiguityAreNeverRetained(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var store = new IdentityStore(workspace.Paths);

        // No work: the launch fallback stays the launch checkout.
        await Resolver(workspace).RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.Launch, store.Get(session.Key, family.Main).CheckoutSource);

        // A branch change in the launch checkout never marks a retained checkout (no work mapped to it uniquely).
        GitHelper.Run(family.Main, "switch", "-c", "other-main");
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var launch = store.Get(session.Key, family.Main);
        Assert.Equal(CheckoutPolicy.Launch, launch.CheckoutSource);
        Assert.True(WorktreeFamily.Same(family.Main, launch.WorkingCheckout));

        // Ambiguity is still reported, not hidden behind a retained checkout.
        family.AddNamed("alpha", "alpha-branch", "shared-change");
        family.AddNamed("beta", "beta-branch", "shared-change");
        Work(workspace, session, family.Main, "shared-change");
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.Ambiguous, store.Get(session.Key, family.Main).CheckoutSource);
    }

    [Fact]
    public async Task APinAndProviderEvidenceKeepWinningOverARetainedCheckout()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login", "dep-7070-fix");
        var eight = family.Add("DEP-8080", "docs", "dep-8080-docs");
        var session = IdentityFixtures.Session("codex", "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        Work(workspace, session, family.Main, "dep-7070-fix");
        await Resolver(workspace).RefreshAsync(session);
        GitHelper.Run(seven, "symbolic-ref", "HEAD", "refs/heads/hotfix-branch");
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.Retained, store.Get(session.Key, family.Main).CheckoutSource);

        store.Update(session.Key, family.Main, value => { value.ProviderCwd = eight; return true; });
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var provider = store.Get(session.Key, family.Main);
        Assert.True(WorktreeFamily.Same(eight, provider.WorkingCheckout));
        Assert.Equal(CheckoutPolicy.Provider, provider.CheckoutSource);

        store.Update(session.Key, family.Main, value => { value.CheckoutPin = seven; return true; });
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var pinned = store.Get(session.Key, family.Main);
        Assert.True(WorktreeFamily.Same(seven, pinned.WorkingCheckout));
        Assert.Equal(CheckoutPolicy.User, pinned.CheckoutSource);
    }

    [Fact]
    public void CheckoutPolicyRetainsOnlyInPlaceOfTheLaunchFallbackAndOnlyForAListedWorktree()
    {
        string[] roots = ["C:/repo", "C:/repo-wt/DEP-1", "C:/repo-wt/DEP-2"];
        bool Has(string root) => root.EndsWith("DEP-2", StringComparison.Ordinal);
        bool Exists(string path) => path != "C:/repo-wt/gone";
        Assert.Equal(new CheckoutPolicy.Choice("C:/repo-wt/DEP-1", CheckoutPolicy.Retained), CheckoutPolicy.Choose(null, null, null, roots, Has, Exists, "C:/repo-wt/DEP-1"));
        Assert.Equal(new CheckoutPolicy.Choice("C:/repo-wt/DEP-2", "unique-work"), CheckoutPolicy.Choose(null, null, "feature", roots, Has, Exists, "C:/repo-wt/DEP-1"));
        Assert.Equal(new CheckoutPolicy.Choice(null, CheckoutPolicy.Ambiguous), CheckoutPolicy.Choose(null, null, "feature", roots, _ => true, Exists, "C:/repo-wt/DEP-1"));
        Assert.Equal(new CheckoutPolicy.Choice("C:/repo-wt/DEP-2", "provider"), CheckoutPolicy.Choose(null, "C:/repo-wt/DEP-2", null, roots, Has, Exists, "C:/repo-wt/DEP-1"));
        Assert.Equal(new CheckoutPolicy.Choice("C:/pin", "user"), CheckoutPolicy.Choose("C:/pin", null, null, roots, Has, Exists, "C:/repo-wt/DEP-1"));
        Assert.Equal(new CheckoutPolicy.Choice(null, "launch"), CheckoutPolicy.Choose(null, null, null, roots, Has, Exists, "C:/repo-wt/gone"));
        Assert.Equal("C:/repo-wt/DEP-1", CheckoutPolicy.RetainedCheckout("C:/repo-wt/DEP-1", roots));
        Assert.Null(CheckoutPolicy.RetainedCheckout("C:/elsewhere", roots));
        Assert.Null(CheckoutPolicy.RetainedCheckout(null, roots));
        // A failed listing (null) is not evidence: the checkout is kept while it exists and released once it is gone.
        Assert.Null(CheckoutPolicy.RetainedCheckout("C:/repo-wt/DEP-1-not-on-disk", null));
        Assert.Null(CheckoutPolicy.RetainedCheckout(null, null));
    }

    [Fact]
    public void TheRetainedSourceIsAStablePersistedTokenAndOnlyThePresentationIsWorded()
    {
        // DATA CONTRACT: "retained" is persisted as CheckoutSource and read back by the resolver; changing it needs a migration.
        Assert.Equal("retained", CheckoutPolicy.Retained);
        Assert.Equal(CheckoutPolicy.RetainedLabel, CheckoutPolicy.Describe(CheckoutPolicy.Retained));
        Assert.NotEqual(CheckoutPolicy.Retained, CheckoutPolicy.RetainedLabel);
        Assert.Equal(CheckoutPolicy.UniqueWork, CheckoutPolicy.Describe(CheckoutPolicy.UniqueWork));
        Assert.Contains("\"retained\"", Json.Encode(new SessionIdentity { Key = "claude:one", Palette = new("#101c27", "#4b91bb", "#000000"), CheckoutSource = CheckoutPolicy.Retained }));
    }

    private static async Task<string> Retain(TestWorkspace workspace, WorktreeFamily family, SessionState session)
    {
        var seven = family.Add("DEP-7070", "login", "dep-7070-fix");
        Work(workspace, session, family.Main, "dep-7070-fix");
        await Resolver(workspace).RefreshAsync(session);
        GitHelper.Run(seven, "symbolic-ref", "HEAD", "refs/heads/hotfix-branch");
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.Retained, WorktreeFamily.Identity(workspace.Paths, session, family.Main).CheckoutSource);
        return seven;
    }

    private static SessionCatalog Register(TestWorkspace workspace, WorktreeFamily family, string provider, SessionState session)
    {
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
        { Provider = provider, SessionId = "one", AttachmentId = session.AttachmentId, Directory = family.Main });
        new StateStore(workspace.Paths).Drain();
        return new SessionCatalog(workspace.Paths);
    }

    // ANALYSIS-180: clearing the checkout (CLI/tray) and Reset release a retained checkout; it must not come back on the next pass.
    [Theory]
    [InlineData("claude", false)]
    [InlineData("codex", false)]
    [InlineData("claude", true)]
    [InlineData("codex", true)]
    public async Task ClearCheckoutAndResetReleaseARetainedCheckoutForGood(string provider, bool reset)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var catalog = Register(workspace, family, provider, session);
        var seven = await Retain(workspace, family, session);
        Assert.Equal(CheckoutPolicy.Retained, catalog.List().Single().CheckoutSource);

        if (reset)
        { catalog.Configure(session.Key, null, null, true); }
        else
        { catalog.Configure(session.Key, "", null, false); }
        Assert.Equal(CheckoutPolicy.Launch, WorktreeFamily.Identity(workspace.Paths, session, family.Main).CheckoutSource);
        for (var pass = 0; pass < 2; pass++)
        {
            await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
            var after = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
            Assert.Equal(CheckoutPolicy.Launch, after.CheckoutSource);
            Assert.True(WorktreeFamily.Same(family.Main, after.WorkingCheckout));
            Assert.False(WorktreeFamily.Same(seven, after.WorkingCheckout));
        }
    }

    // ANALYSIS-201: a Clear that lands while a pass awaits Git must win; the pass may not write retained back over it.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task AClearThatLandsMidPassIsNotUndoneByThePassThatStartedBeforeIt(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var catalog = Register(workspace, family, provider, session);
        var seven = await Retain(workspace, family, session);

        var cleared = false;
        var racing = new CheckoutCache(read: directory =>
        {
            if (!cleared)
            { cleared = true; catalog.Configure(session.Key, "", null, false); }
            return new GitReader().ReadUncached(directory);
        });
        await new IdentityResolver(workspace.Paths, racing).RefreshAsync(session);
        Assert.True(cleared);
        Assert.Equal(CheckoutPolicy.Launch, WorktreeFamily.Identity(workspace.Paths, session, family.Main).CheckoutSource);
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var after = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
        Assert.Equal(CheckoutPolicy.Launch, after.CheckoutSource);
        Assert.True(WorktreeFamily.Same(family.Main, after.WorkingCheckout));
        Assert.False(WorktreeFamily.Same(seven, after.WorkingCheckout));
    }

    // ANALYSIS-202: a failed listing (a launch directory outside Git, a timeout) is cached as a failure for a bounded time.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task AFailedListingIsCachedAsAFailureForABoundedLife(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var now = DateTimeOffset.UtcNow;
        var listings = 0;
        var resolver = new IdentityResolver(workspace.Paths, new CheckoutCache(), () => now, _ => { listings++; return null; });
        await resolver.RefreshAsync(session);
        now += TimeSpan.FromSeconds(2);
        await resolver.RefreshAsync(session);
        Assert.Equal(1, listings);
        now += TimeSpan.FromSeconds(9);
        await resolver.RefreshAsync(session);
        Assert.Equal(2, listings);
    }

    // ANALYSIS-203: only the work that verified the checkout may be cleared by its branch change; other new work rebinds to the launch checkout.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task UnrelatedWorkObservedAfterTheSwitchIsNotClearedByTheVerifiedCheckoutsBranchChange(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var alpha = family.AddNamed("alpha", "alpha-branch");
        IdentityFixtures.Change(alpha, "dep-7070-fix", scaffold: true);
        GitHelper.Run(alpha, "add", "-A");
        GitHelper.Run(alpha, "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "-m", "change");
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        Work(workspace, session, family.Main, "dep-7070-fix");
        await Resolver(workspace).RefreshAsync(session);
        var verified = store.Get(session.Key, family.Main);
        Assert.Equal(CheckoutPolicy.UniqueWork, verified.CheckoutSource);
        Assert.Equal("dep-7070-fix", verified.CheckoutWork);

        GitHelper.Run(alpha, "switch", "-c", "hotfix", "main");
        Work(workspace, session, family.Main, "external-store-change");
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var after = store.Get(session.Key, family.Main);
        Assert.Equal("external-store-change", after.ActiveWork);
        Assert.Equal(CheckoutPolicy.Launch, after.CheckoutSource);
        Assert.True(WorktreeFamily.Same(family.Main, after.WorkingCheckout));
    }

    [Fact]
    public void TheVerifyingWorkFieldIsAdditiveAndKeptByAnOlderReaderThroughAdditionalData()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new SessionIdentity { Key = "k", Palette = new("#101c27", "#4b91bb", "#000000"), CheckoutWork = "w" });
        var older = System.Text.Json.JsonSerializer.Deserialize<SessionIdentity>(json.Replace("\"CheckoutWork\"", "\"FutureField\""))!;
        Assert.NotNull(older.AdditionalData);
        Assert.Contains("FutureField", older.AdditionalData!.Keys);
        Assert.Contains("FutureField", System.Text.Json.JsonSerializer.Serialize(older));
    }

    private static void SetLaunch(TestWorkspace workspace, SessionState session, string current, string launch) =>
        new IdentityStore(workspace.Paths).Update(session.Key, current, value => { value.LaunchDirectory = launch; return true; });

    // ANALYSIS-181: a failed listing or an unreachable launch repository is not evidence; only a successful listing without
    // the checkout, or a missing directory, releases it.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task AFailedWorktreeListingKeepsTheRetainedCheckoutUntilThereIsPositiveEvidence(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var seven = await Retain(workspace, family, session);

        // The launch directory is unreachable, so every Git listing fails without saying anything about the worktrees.
        SetLaunch(workspace, session, family.Main, family.Main + "-unreachable");
        for (var pass = 0; pass < 2; pass++)
        {
            await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
            var kept = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
            Assert.Equal(CheckoutPolicy.Retained, kept.CheckoutSource);
            Assert.True(WorktreeFamily.Same(seven, kept.WorkingCheckout));
        }
        SetLaunch(workspace, session, family.Main, family.Main);

        // A successful listing that still has it keeps it; a successful listing without it releases it.
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.Retained, WorktreeFamily.Identity(workspace.Paths, session, family.Main).CheckoutSource);
        GitHelper.Run(family.Main, "worktree", "remove", "--force", seven);
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var released = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
        Assert.Equal(CheckoutPolicy.Launch, released.CheckoutSource);
        Assert.True(WorktreeFamily.Same(family.Main, released.WorkingCheckout));
    }

    [Fact]
    public async Task AFailedListingStillReleasesARetainedCheckoutWhoseDirectoryIsGone()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var session = IdentityFixtures.Session("claude", "one", family.Main);
        var seven = await Retain(workspace, family, session);
        SetLaunch(workspace, session, family.Main, family.Main + "-unreachable");
        Directory.Delete(seven, true);
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var after = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
        Assert.NotEqual(CheckoutPolicy.Retained, after.CheckoutSource);
        Assert.False(WorktreeFamily.Same(seven, after.WorkingCheckout));
    }

    // ANALYSIS-182: any new work supersedes the retained checkout, including work that maps to no checkout (the launch checkout
    // shows again, as before the retention existed).
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task AnyNewWorkReleasesTheRetainedCheckout(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var seven = await Retain(workspace, family, session);

        Work(workspace, session, family.Main, "unmapped-new-change");
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var after = WorktreeFamily.Identity(workspace.Paths, session, family.Main);
        Assert.Equal(CheckoutPolicy.Launch, after.CheckoutSource);
        Assert.True(WorktreeFamily.Same(family.Main, after.WorkingCheckout));
        Assert.False(WorktreeFamily.Same(seven, after.WorkingCheckout));
        Assert.Equal("main", after.WorkBranch);
    }

    // QA-181: the switch itself may remove the verifying change folder (committed on the old branch) in a worktree not named for
    // the ticket; the previously selected unique-work checkout is still kept.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ABranchSwitchThatRemovesTheVerifyingChangeFolderStillRetainsTheCheckout(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var alpha = family.AddNamed("alpha", "alpha-branch");
        IdentityFixtures.Change(alpha, "dep-7070-fix", scaffold: true);
        GitHelper.Run(alpha, "add", "-A");
        GitHelper.Run(alpha, "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "-m", "change");
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        Work(workspace, session, family.Main, "dep-7070-fix");
        await Resolver(workspace).RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.UniqueWork, store.Get(session.Key, family.Main).CheckoutSource);

        GitHelper.Run(alpha, "switch", "-c", "hotfix", "main");
        Assert.False(Directory.Exists(Path.Combine(alpha, "openspec", "changes", "dep-7070-fix")));
        for (var pass = 0; pass < 3; pass++)
        {
            var git = await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
            var after = store.Get(session.Key, family.Main);
            Assert.True(WorktreeFamily.Same(alpha, after.WorkingCheckout));
            Assert.Equal(CheckoutPolicy.Retained, after.CheckoutSource);
            Assert.Equal("hotfix", git.Branch);
            Assert.Equal("alpha", git.Worktree);
            Assert.Null(after.ActiveWork);
        }
    }

    [Fact]
    public async Task AChangeFolderThatVanishesWithoutABranchChangeStillFallsBackToTheLaunchCheckout()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var alpha = family.AddNamed("alpha", "alpha-branch", "dep-7070-fix");
        var session = IdentityFixtures.Session("codex", "one", family.Main);
        Work(workspace, session, family.Main, "dep-7070-fix");
        await Resolver(workspace).RefreshAsync(session);
        Directory.Delete(Path.Combine(alpha, "openspec", "changes", "dep-7070-fix"));
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        Assert.Equal(CheckoutPolicy.Launch, WorktreeFamily.Identity(workspace.Paths, session, family.Main).CheckoutSource);
    }

    // ANALYSIS-184: a pinned change that still verifies the checkout keeps the unique-work label; no one-pass flip to retained.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task APinnedChangeThatStillVerifiesTheCheckoutNeverFlipsTheLabelToRetained(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login", "dep-7070-fix");
        var session = IdentityFixtures.Session(provider, "one", family.Main);
        var store = new IdentityStore(workspace.Paths);
        Work(workspace, session, family.Main, "dep-7070-fix");
        store.Update(session.Key, family.Main, value => { ChangeOrigin.Pin(value, "dep-7070-fix"); return true; });
        await Resolver(workspace).RefreshAsync(session);
        GitHelper.Run(seven, "symbolic-ref", "HEAD", "refs/heads/hotfix-branch");
        for (var pass = 0; pass < 2; pass++)
        {
            await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
            var after = store.Get(session.Key, family.Main);
            Assert.Null(after.ActiveWork);
            Assert.Equal("dep-7070-fix", after.WorkSubject);
            Assert.Equal(CheckoutPolicy.UniqueWork, after.CheckoutSource);
            Assert.True(WorktreeFamily.Same(seven, after.WorkingCheckout));
        }
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
