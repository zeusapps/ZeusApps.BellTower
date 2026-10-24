using BellTower.Core;
using BellTower.IO;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Sessions;

// Tasks 4.1 and 4.4: identity metadata edits, resolver passes and a deliberate reset never touch provider wait
// timestamps, episodes, acknowledgment, viewed timers or sound receipts; palettes change only on explicit colour/reset.
public sealed class MetadataPreservationTests
{
    private static readonly string[] Providers = ["claude", "codex"];

    private static (WorktreeFamily Family, string Seven) Setup(TestWorkspace workspace)
    {
        var family = WorktreeFamily.Create(workspace.Root);
        return (family, family.Add("DEP-7070", "login", "dep-7070-fix"));
    }

    private static string Wait(TestWorkspace workspace, string provider, string id, string directory, DateTimeOffset at)
    {
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission, at) with
        { Provider = provider, SessionId = id, AttachmentId = "attachment-" + provider + id, Directory = directory });
        var state = new StateStore(workspace.Paths).Drain();
        return state.Sessions[provider + ":" + id].Episode!;
    }

    [Fact]
    public async Task EveryMetadataUpdateAndAResetWhileWaitingPreserveAttentionAndSoundReceiptsForBothClients()
    {
        using var workspace = new TestWorkspace();
        var (family, seven) = Setup(workspace);
        var now = DateTimeOffset.UtcNow;
        var episodes = Providers.ToDictionary(provider => provider, provider => Wait(workspace, provider, "one", family.Main, now.AddSeconds(-60)));
        var states = new StateStore(workspace.Paths);
        foreach (var provider in Providers)
        { states.MarkViewed(provider + ":one", episodes[provider]); }
        var baseline = Providers.ToDictionary(provider => provider, provider => Json.Encode(states.Read().Sessions[provider + ":one"]));
        var policy = new AlertPolicy(now.AddMinutes(-5));
        var live = states.Read().Sessions.Values.ToArray();
        Assert.Equal(2, policy.Collect(live, now, false).Count);

        var catalog = new SessionCatalog(workspace.Paths);
        var identities = new IdentityStore(workspace.Paths);
        var resolver = new IdentityResolver(workspace.Paths, new CheckoutCache());
        var palettes = Providers.ToDictionary(provider => provider, provider => identities.Get(provider + ":one", family.Main).Palette);
        foreach (var provider in Providers)
        {
            var key = provider + ":one";
            var session = states.Read().Sessions[key];
            catalog.Rename(key, "Manual title", "dep-7070-fix");
            catalog.Configure(key, seven, null, false);
            if (provider == "codex")
            {
                catalog.SetNativeNames(key, false);
                catalog.SetNativeNames(key, true);
            }
            else
            {
                Assert.Throws<NotSupportedException>(() => catalog.SetNativeNames(key, true));
            }
            catalog.UseBellTowerName(key);
            await resolver.RefreshAsync(session);
            new IdentityPresentationStore(workspace.Paths).Save(identities.Get(key, family.Main), new GitContext("DEP-7070-login", "DEP-7070"));
            Assert.Equal(palettes[provider], identities.Get(key, family.Main).Palette);
        }

        // A deliberate colour change and then a reset replace only the palette and the Bell Tower pins.
        var claudeKey = "claude:one";
        var codexKey = "codex:one";
        var codexFile = Directory.GetFiles(workspace.Paths.Identities, "*.json").Single(file => File.ReadAllText(file).Contains(codexKey));
        var codexBytes = File.ReadAllBytes(codexFile);
        var colored = catalog.Configure(claudeKey, null, "#12ab34", false);
        Assert.Equal("#12ab34", colored.Palette.Accent);
        var reset = catalog.Configure(claudeKey, null, null, true);
        Assert.NotEqual("#12ab34", reset.Palette.Accent);
        Assert.Null(reset.ManualName);
        Assert.Null(reset.Change);
        Assert.Null(reset.CheckoutPin);
        Assert.Null(reset.ColorPin);
        Assert.Equal(codexBytes, File.ReadAllBytes(codexFile));
        Assert.Equal(palettes["codex"], identities.Get(codexKey, family.Main).Palette);

        // Waits, episodes, acknowledgment, viewed timers and sound receipts are exactly as before.
        foreach (var provider in Providers)
        {
            var session = states.Read().Sessions[provider + ":one"];
            Assert.Equal(baseline[provider], Json.Encode(session));
            Assert.Equal(episodes[provider], session.Episode);
            Assert.Equal(episodes[provider], session.ViewedEpisode);
        }
        Assert.Empty(policy.Collect(states.Read().Sessions.Values.ToArray(), now.AddSeconds(30), false));
    }

    [Fact]
    public void ResetReResolvesAutomaticIdentityWithoutErasingExplicitNativeTitles()
    {
        using var workspace = new TestWorkspace();
        var store = new IdentityStore(workspace.Paths);
        store.Update("claude:one", workspace.Root, value =>
        {
            value.ManualName = "Mine";
            value.Change = "pinned";
            value.ActiveWork = "dep-7070-fix";
            value.AutomaticSubject = "DEP-7070";
            value.NativeLastName = "Bell Tower name";
            return true;
        });
        Assert.Equal("Mine", store.Get("claude:one", workspace.Root).Name);
        var reset = store.SetColor("claude:one", workspace.Root, null, reset: true);
        Assert.Equal("dep-7070-fix", reset.Name);
        Assert.Equal("Bell Tower name", reset.NativeLastName);
        store.Update("claude:one", workspace.Root, value => { value.NativeTitle = "User native title"; value.NativeTitleSource = "claude-custom-title"; return true; });
        Assert.Equal("User native title", store.SetColor("claude:one", workspace.Root, null, reset: true).Name);
    }
}
