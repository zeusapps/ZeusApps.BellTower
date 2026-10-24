using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class IdentityPrecedenceTests
{
    private static SessionIdentity Identity(Action<SessionIdentity>? configure = null)
    {
        var identity = new SessionIdentity { Key = "claude:one", DefaultName = "Directory", Palette = Palette.Create() };
        configure?.Invoke(identity);
        return identity;
    }

    [Fact]
    public void ExplicitLayersApplyInPrecedenceOrder()
    {
        var identity = Identity(i =>
        {
            i.ManualName = "Bell Tower title";
            i.NativeTitle = "Native title";
            i.Role = "PM"; // legacy saved value: preserved, never shown
            i.Change = "pinned-change";
            i.ActiveWork = "active-work";
            i.AutomaticSubject = "DEP-7070";
        });
        Assert.Equal("Bell Tower title", identity.Name);
        identity.ManualName = null;
        Assert.Equal("Native title", identity.Name);
        identity.NativeTitle = null;
        Assert.Equal("pinned-change", identity.Name);
        identity.Change = null;
        Assert.Equal("active-work", identity.Name);
        identity.ActiveWork = null;
        Assert.Equal("DEP-7070", identity.Name);
        identity.AutomaticSubject = null;
        Assert.Equal("Directory", identity.Name);
        identity.Role = null;
        Assert.Equal("Directory", identity.Name);
    }

    [Fact]
    public void NoRoleIsShownOrSpokenAndSpeechIsNormalizedOnce()
    {
        var identity = Identity(i => { i.Role = "PM"; i.Change = "ttyd-download-chips"; });
        var session = new SessionState { Key = identity.Key, Provider = "claude", SessionId = "one", AttachmentId = "a" };
        Assert.Equal("ttyd-download-chips", identity.Name);
        Assert.StartsWith("ttyd download chips", AlertPolicy.Phrase(session, identity), StringComparison.Ordinal);
        identity.NativeTitle = "dep_7070 title";
        Assert.Equal("dep_7070 title", identity.Name);
        Assert.StartsWith("dep 7070 title", AlertPolicy.Phrase(session, identity), StringComparison.Ordinal);
    }

    private static string Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
        { start.ArgumentList.Add(arg); }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("DEP-7070-login", new[] { "dep-7070-fix" }, "dep-7070-fix")]
    [InlineData("DEP-7070-login", new[] { "dep-7070-fix", "dep-7070-docs" }, "DEP-7070")]
    [InlineData("DEP-7070-login", new string[0], "DEP-7070")]
    [InlineData("feature/plain", new[] { "dep-7070-fix" }, "feature/plain")]
    public async Task AutomaticSubjectUsesUniqueBranchChangeThenTicketThenBranch(string branch, string[] changes, string expected)
    {
        using var workspace = new TestWorkspace();
        var repository = Path.Combine(workspace.Root, "repository");
        Directory.CreateDirectory(repository);
        Git(repository, "init", "--initial-branch", branch);
        foreach (var change in changes)
        { IdentityFixtures.Change(repository, change); }
        IdentityFixtures.Change(repository, "archive");
        var session = IdentityFixtures.Session("claude", "one", repository);
        var resolver = new IdentityResolver(workspace.Paths, new CheckoutCache());
        await resolver.RefreshAsync(session);
        var identity = new IdentityStore(workspace.Paths).Get(session.Key, repository);
        Assert.Equal(expected, identity.AutomaticSubject);
        Assert.Equal(expected, identity.Name);
        // Pinned change, active work and titles all outrank the automatic subject.
        identity.ActiveWork = "other-work";
        Assert.Equal("other-work", identity.Name);
    }

    [Fact]
    public async Task DirectoryIsTheFinalFallbackOutsideGit()
    {
        using var workspace = new TestWorkspace();
        var directory = Path.Combine(workspace.Root, "Plain Folder");
        Directory.CreateDirectory(directory);
        // The test workspace lives inside this repository; a broken gitdir marker makes the folder a non-checkout.
        File.WriteAllText(Path.Combine(directory, ".git"), "gitdir: missing");
        var session = IdentityFixtures.Session("codex", "one", directory);
        await new IdentityResolver(workspace.Paths, new CheckoutCache()).RefreshAsync(session);
        var identity = new IdentityStore(workspace.Paths).Get(session.Key, directory);
        Assert.Null(identity.AutomaticSubject);
        Assert.Equal("Plain Folder", identity.Name);
    }

    [Fact]
    public void PersistedDerivedHistoryNeverReplacesAnExplicitTitleOrPin()
    {
        var identity = Identity(i => { i.ManualName = "Mine"; i.NativeTitle = "Native"; i.Change = "pinned"; });
        WorkPolicy.Apply(identity, new("history-change", "D:/root", "history:edit", false, DateTimeOffset.UtcNow, 10));
        Assert.Equal("Mine", identity.Name);
        identity.ManualName = null;
        identity.NativeTitle = null;
        Assert.Equal("pinned", identity.Name);
        Assert.Equal("history-change", identity.ActiveWork);
    }
}
