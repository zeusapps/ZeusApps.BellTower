using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

/// <summary>Task 4.4: managed launches stay unchanged beside native sessions of both providers, with the setting on or off.</summary>
public sealed class MixedLaunchAndNativeTests
{
    private sealed class Recording : ITerminalPresentationWriter
    {
        public List<DesiredTerminalPresentation> Writes { get; } = [];
        public Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation)
        {
            Writes.Add(desired);
            return Task.FromResult(true);
        }
    }

    private static ProcessIdentity Self => ProcessProbe.Identify(Environment.ProcessId)!;

    private static SessionState Publish(TestWorkspace workspace, string provider, string session, string attachment, DateTimeOffset at)
    {
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission(at) with
        {
            Provider = provider,
            SessionId = session,
            AttachmentId = attachment,
            Directory = workspace.Root
        });
        return new StateStore(workspace.Paths).Drain().Sessions[provider + ":" + session];
    }

    [Fact]
    public async Task ManagedAndNativeSessionsOfBothProvidersStayIndependentWhateverTheSetting()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        var attachments = new AttachmentStore(workspace.Paths);
        var launches = new LaunchPresentationStore(workspace.Paths);
        var sessions = new List<SessionState>();
        var managedIds = new List<string>();
        var nativeIds = new List<string>();
        foreach (var provider in new[] { "claude", "codex" })
        {
            var managedId = provider + "-managed";
            var launch = launches.Prepare(managedId, Self);
            attachments.Save(new(managedId, provider, Self, new("windows-terminal", Self, Self, 1234, "terminal-session") { ShellSource = "launch-env" },
                workspace.Root, now, ManagedTitle: true, ManagedBackground: true)
            { Ownership = "managed", Evidence = AttachmentEvidence.ForManagedLaunch(Self) });
            sessions.Add(Publish(workspace, provider, "managed", managedId, now.AddMinutes(-1)));
            managedIds.Add(managedId);

            var mode = provider == "codex" ? AgentModes.EmbeddedRuntime : AgentModes.InteractiveClient;
            var native = NativeAdoption.Apply(new(provider + "-native", provider, Self,
                new("windows-terminal", Self, Self, 1234, "terminal-session") { ShellSource = "ancestry" }, workspace.Root, now)
            { Ownership = "observed" }, "native", mode, AgentModeClassifier.MayShareBackend(provider)).Attachment;
            attachments.Save(native);
            sessions.Add(Publish(workspace, provider, "native", native.Id, now.AddMinutes(-1)));
            nativeIds.Add(native.Id);

            // A native session never inherits the managed launch's prepared palette or pending launch record.
            Assert.False(native.ManagedTitle);
            Assert.False(native.ManagedBackground);
            Assert.Null(launches.Read(native.Id, Self));
            var identities = new IdentityStore(workspace.Paths);
            Assert.NotEqual(launch.Palette, identities.Get(provider + ":native", workspace.Root).Palette);
        }
        var before = sessions.ToDictionary(session => session.Key, session => (session.Episode, session.Since));
        var policy = new AlertPolicy(now.AddMinutes(-2));
        Assert.Equal(4, policy.Collect(sessions, now, false).Count);
        var palettes = sessions.ToDictionary(session => session.Key, session => new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).Palette);

        var writer = new Recording();
        using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => new()), writer, () => now);
        foreach (var enabled in new[] { false, true, false, true })
        {
            new ConfigStore(workspace.Paths).Update(c => c.NativePresentation = enabled);
            await pump.RefreshAsync(sessions);
            var drained = new StateStore(workspace.Paths).Drain().Sessions;
            // Toggling the setting never resets a waiting episode, replays an alert or re-allocates a palette.
            Assert.Empty(policy.Collect(drained.Values, now.AddSeconds(30), false));
            foreach (var session in sessions)
            {
                Assert.Equal(before[session.Key], (drained[session.Key].Episode, drained[session.Key].Since));
                Assert.Equal(palettes[session.Key], new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).Palette);
            }
        }
        // Managed launches keep their title and background throughout; native sessions get a title and their own palette background.
        Assert.All(writer.Writes.Where(write => managedIds.Contains(write.AttachmentId)), write => Assert.NotNull(write.Background));
        Assert.Equal(managedIds.Count, writer.Writes.Where(write => managedIds.Contains(write.AttachmentId)).Select(write => write.AttachmentId).Distinct().Count());
        var nativeWrites = writer.Writes.Where(write => nativeIds.Contains(write.AttachmentId)).ToArray();
        Assert.Equal(nativeIds.Order(), nativeWrites.Select(write => write.AttachmentId).Distinct().Order());
        Assert.All(nativeWrites, write => Assert.NotNull(write.Background));
    }
}
