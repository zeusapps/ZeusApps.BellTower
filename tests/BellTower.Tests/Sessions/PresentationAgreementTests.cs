using System.Text.Json;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Sessions;

// One derived identity feeds the badge, tab title, chip and speech for both clients (task 3.4), including mixed
// simultaneous sessions launched from the same directory with zero cross-session reuse.
public sealed class PresentationAgreementTests
{
    [Fact]
    public async Task BadgeTitleChipAndSpeechAgreeForMixedSessionsLaunchedInTheSameDirectory()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        family.Add("DEP-7070", "login", "dep-7070-fix");
        family.Add("DEP-8080", "docs", "dep-8080-docs");
        var now = DateTimeOffset.UtcNow;
        var plan = new[] { ("claude", "dep-7070-fix", "DEP-7070", "DEP-7070-login"), ("codex", "dep-8080-docs", "DEP-8080", "DEP-8080-docs") };
        foreach (var (provider, _, _, _) in plan)
        {
            new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission(now.AddSeconds(-60)) with
            { Provider = provider, SessionId = "one", AttachmentId = "attachment-" + provider, Directory = family.Main });
        }
        var state = new StateStore(workspace.Paths).Drain();
        var identities = new IdentityStore(workspace.Paths);
        var resolver = new IdentityResolver(workspace.Paths, new CheckoutCache());
        var presentations = new IdentityPresentationStore(workspace.Paths);
        var contexts = new Dictionary<string, GitContext>();
        foreach (var (provider, change, _, _) in plan)
        {
            var key = provider + ":one";
            identities.Update(key, family.Main, value => { value.Role = "PM"; /* legacy saved value, ignored */ value.Change = change; return true; });
            var git = await resolver.RefreshAsync(state.Sessions[key]);
            contexts[key] = git;
            presentations.Save(identities.Get(key, family.Main), git);
        }
        var views = new SessionViewFactory(identities, new AttachmentStore(workspace.Paths), new Navigator(workspace.Paths));
        var chips = views.Chips(state.Sessions.Values, now, contexts).ToDictionary(chip => chip.Key);
        Assert.Equal(2, chips.Count);
        foreach (var (provider, change, ticket, branch) in plan)
        {
            var key = provider + ":one";
            var identity = identities.Get(key, family.Main);
            var name = change;
            Assert.Equal(name, identity.Name);
            // Badge (status line fed by the derived snapshot).
            var input = JsonSerializer.Serialize(new { session_id = "one", cwd = family.Main });
            var badge = new StatusLineReader(workspace.Paths).Render(new StringReader(input), provider, color: false);
            Assert.Equal($"[ {name} ]  branch: {branch}  worktree: {ticket}", badge);
            // Tab title, chip and overview all derive from the same name and checkout context.
            Assert.Equal($"{name} | {branch} | worktree: {ticket}", SessionPresentation.Title(identity.Name, contexts[key]));
            Assert.Equal(name, chips[key].Name);
            Assert.Equal(identity.Palette, chips[key].Palette);
            Assert.Contains("branch: " + branch, chips[key].Details);
            Assert.Contains("worktree: " + ticket, chips[key].Details);
            Assert.Equal(contexts[key].ToString(), views.Overview([state.Sessions[key]], contexts).Single().Git.ToString());
            // Speech.
            Assert.Equal("" + change.Replace('-', ' ') + ", needs permission", AlertPolicy.Phrase(state.Sessions[key], identity));
            // The derived record carries its own provenance and checkout.
            var snapshot = presentations.Read(key)!;
            Assert.Equal("pinned-change", snapshot.Provenance);
            Assert.Equal(key, snapshot.Key);
        }
        Assert.NotEqual(chips["claude:one"].Palette, chips["codex:one"].Palette);
        Assert.DoesNotContain("DEP-8080", chips["claude:one"].Details);
        Assert.DoesNotContain("DEP-7070", chips["codex:one"].Details);
    }
}
