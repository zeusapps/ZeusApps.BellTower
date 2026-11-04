using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

// QA-1304 / ANALYSIS-902: replays saved native hook evidence through the real ingestion paths, one data-driven theory
// over Fixtures/native. Each file carries the payload, the redacted agent arguments the classifier reads, the hook
// ancestry and the expected outcome, so classification drift and payload drift both fail here. No provider is launched.
public sealed class NativeReplayTests
{
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var name in NativeFixture.Names())
        {
            data.Add(name);
        }
        return data;
    }

    [Fact]
    public void TheSavedFixturesCoverBothProvidersAndLabelTheirProvenance()
    {
        var all = NativeFixture.All();
        Assert.Contains("claude", all.Select(item => item.Provider));
        Assert.Contains("codex", all.Select(item => item.Provider));
        Assert.All(all, item =>
        {
            Assert.Equal($"{item.Provider}-{item.ProviderVersion}-{item.Event.ToLowerInvariant()}.json", item.Name);
            Assert.Contains(item.Source, new[] { "verbatim", "reconstructed" });
            Assert.Contains(item.Provider + ".exe", item.AncestorNames);
            Assert.Equal(item.Source == "reconstructed", item.Note.StartsWith("Reconstructed", StringComparison.Ordinal));
            // Redacted: no path, home folder or prompt text is kept in the classifier input.
            Assert.All(item.AgentArguments ?? [], argument => Assert.DoesNotMatch(@"[\/:]", argument));
        });
        // Each provider and version keeps a start event and at least one later event, so recovery is always replayable.
        foreach (var group in all.GroupBy(item => (item.Provider, item.ProviderVersion)))
        {
            Assert.Contains("SessionStart", group.Select(item => item.Event));
            Assert.True(group.Count() > 1, $"{group.Key} has no later event to replay.");
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void AFixtureClassifiesAndEstablishesTheConversationThroughBothIngestionPaths(string name)
    {
        var fixture = NativeFixture.Load(name);
        var mode = AgentModeClassifier.Classify(fixture.Provider, fixture.AgentArguments is not null, fixture.AgentArguments);
        Assert.Equal(fixture.ExpectedMode, mode);

        // The saved agent process no longer exists, and reconciliation rightly ends a conversation whose agent is gone,
        // so a fixture with a found agent is replayed with a live stand-in process. Classification and policy are unchanged.
        using var live = fixture.AgentArguments is null ? null : DisposableSleeper.Start();
        var agent = live is null ? null : ProcessProbe.Identify(live.Id);
        NativeAdoption.Outcome Classify(string session) =>
            NativeAdoption.Apply(NativeAdoptionTests.Captured(fixture.Provider, agent, "saved-" + fixture.Provider, "observed"), session, mode, AgentModeClassifier.MayShareBackend(fixture.Provider));
        Assert.Equal(fixture.ExpectedDedicated, Classify(fixture.SessionId).Dedicated);

        using var viaIngestor = new TestWorkspace();
        new HookIngestor(viaIngestor.Paths, (_, _, session) => Classify(session!)).Ingest(fixture.Provider, new StringReader(fixture.PayloadText));
        Verify(viaIngestor, fixture, agent);

        using var viaCapture = new TestWorkspace();
        NativeAdoptionTests.Capture(viaCapture, fixture.Provider, fixture.PayloadText, Classify(fixture.SessionId));
        Verify(viaCapture, fixture, agent);
    }

    // The same facts for both paths: a live conversation, no invented wait or attention, and the expected ownership.
    private static void Verify(TestWorkspace workspace, NativeFixture fixture, ProcessIdentity? agent)
    {
        var session = new StateStore(workspace.Paths).Drain().Sessions[fixture.Provider + ":" + fixture.SessionId];
        Assert.False(session.Ended);
        Assert.Empty(session.Waits);
        var attachment = new AttachmentStore(workspace.Paths).Get(session.AttachmentId)!;
        Assert.Equal(EvidenceStatus.HookObserved, attachment.Evidence!.ConversationHealth);
        Assert.Equal(fixture.ExpectedLifecycleOwner, attachment.Evidence.LifecycleOwner);
        if (fixture.ExpectedDedicated)
        {
            Assert.Equal(agent, attachment.Process);
            Assert.Equal(fixture.ExpectedMode, attachment.Evidence.Mode);
        }
        else
        {
            Assert.Null(attachment.Process);
            Assert.Equal("shared-runtime", attachment.Ownership);
        }
    }
}
