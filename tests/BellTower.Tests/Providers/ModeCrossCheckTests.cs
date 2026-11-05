using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

// Task 2.5 / design decision 7: one shared cross-check policy, driven here with fake process relationships. The provider
// adapter supplies only whether the provider can share a backend (Claude cannot, so it has no relationship rule). The
// cross-check can only keep a mode or return unknown; it never produces a stronger mode.
public sealed class ModeCrossCheckTests
{
    private static string Check(string mode, bool mayShareBackend, NativeAdoption.ProcessRelations? relations) =>
        NativeAdoption.CrossCheck(mode, mayShareBackend, _ => relations);

    private static NativeAdoption.RelatedProcess Related(string mode, int pid = 50) => new(new ProcessIdentity(pid, 1000), mode);

    private static NativeAdoption.ProcessRelations With(string? parent = null, string[]? children = null, bool complete = true) =>
        new(parent is null ? null : Related(parent), (children ?? []).Select((mode, index) => Related(mode, 60 + index)).ToList(), complete);

    public static TheoryData<string, string, string?, string[], bool, string> Rows => new()
    {
        // Codex shared backend: started by a client, not by a shell, another backend, a headless run or an unknown process.
        { "codex", AgentModes.SharedBackend, AgentModes.EmbeddedRuntime, [], true, AgentModes.SharedBackend },
        { "codex", AgentModes.SharedBackend, AgentModes.InteractiveClient, [], true, AgentModes.SharedBackend },
        { "codex", AgentModes.SharedBackend, null, [], true, AgentModes.Unknown },
        { "codex", AgentModes.SharedBackend, AgentModes.SharedBackend, [], true, AgentModes.Unknown },
        { "codex", AgentModes.SharedBackend, AgentModes.Headless, [], true, AgentModes.Unknown },
        { "codex", AgentModes.SharedBackend, AgentModes.Unknown, [], true, AgentModes.Unknown },
        // Codex clients: no same-provider parent, no backend or client children, a complete child list.
        { "codex", AgentModes.EmbeddedRuntime, null, [], true, AgentModes.EmbeddedRuntime },
        { "codex", AgentModes.EmbeddedRuntime, null, [AgentModes.Headless], true, AgentModes.EmbeddedRuntime },
        { "codex", AgentModes.EmbeddedRuntime, null, [AgentModes.Unknown], true, AgentModes.EmbeddedRuntime },
        { "codex", AgentModes.EmbeddedRuntime, AgentModes.EmbeddedRuntime, [], true, AgentModes.Unknown },
        { "codex", AgentModes.EmbeddedRuntime, AgentModes.SharedBackend, [], true, AgentModes.Unknown },
        { "codex", AgentModes.EmbeddedRuntime, AgentModes.Headless, [], true, AgentModes.Unknown },
        { "codex", AgentModes.EmbeddedRuntime, null, [AgentModes.SharedBackend], true, AgentModes.Unknown },
        { "codex", AgentModes.EmbeddedRuntime, null, [AgentModes.EmbeddedRuntime], true, AgentModes.Unknown },
        { "codex", AgentModes.EmbeddedRuntime, null, [AgentModes.Headless, AgentModes.SharedBackend], true, AgentModes.Unknown },
        { "codex", AgentModes.EmbeddedRuntime, null, [], false, AgentModes.Unknown },
        { "codex", AgentModes.InteractiveClient, AgentModes.EmbeddedRuntime, [], true, AgentModes.Unknown },
        // Modes the cross-check does not judge are returned as they are.
        { "codex", AgentModes.Headless, AgentModes.SharedBackend, [AgentModes.SharedBackend], true, AgentModes.Headless },
        { "codex", AgentModes.Unknown, AgentModes.EmbeddedRuntime, [], true, AgentModes.Unknown },
        // Claude cannot share a backend: no relationship rule, the same relationships change nothing.
        { "claude", AgentModes.InteractiveClient, null, [], true, AgentModes.InteractiveClient },
        { "claude", AgentModes.InteractiveClient, AgentModes.InteractiveClient, [AgentModes.InteractiveClient], true, AgentModes.InteractiveClient },
        { "claude", AgentModes.InteractiveClient, AgentModes.Headless, [AgentModes.SharedBackend], false, AgentModes.InteractiveClient },
        { "claude", AgentModes.Headless, AgentModes.Headless, [], true, AgentModes.Headless },
        { "claude", AgentModes.Unknown, AgentModes.InteractiveClient, [], true, AgentModes.Unknown }
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void ModeAgreesWithRelationshipsOrFailsClosedToUnknown(string provider, string mode, string? parent, string[] children, bool complete, string expected) =>
        Assert.Equal(expected, Check(mode, AgentModeClassifier.MayShareBackend(provider), With(parent, children, complete)));

    [Theory]
    [InlineData(AgentModes.SharedBackend, AgentModes.Unknown)]
    [InlineData(AgentModes.EmbeddedRuntime, AgentModes.EmbeddedRuntime)]
    [InlineData(AgentModes.InteractiveClient, AgentModes.InteractiveClient)]
    [InlineData(AgentModes.Headless, AgentModes.Headless)]
    [InlineData(AgentModes.Unknown, AgentModes.Unknown)]
    public void UnreadableRelationshipsNeverStrengthenAMode(string mode, string expected) =>
        Assert.Equal(expected, Check(mode, true, null));

    // An unreadable command line stays unknown through the classifier, and no relationship can upgrade it.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AnUnreadableCommandLineIsNeverUpgradedByRelationships(string provider)
    {
        var mode = AgentModeClassifier.Classify(provider, true, null);
        Assert.Equal(AgentModes.Unknown, mode);
        Assert.Equal(AgentModes.Unknown, Check(mode, AgentModeClassifier.MayShareBackend(provider), With(AgentModes.EmbeddedRuntime)));
    }

    // Unchanged known shapes: every command-line row of the classifier, with the relationships of an ordinary launch (a
    // backend under a client, everything else under a non-provider process), keeps its classification.
    [Theory]
    [MemberData(nameof(NativeAdoptionTests.Modes), MemberType = typeof(NativeAdoptionTests))]
    public void KnownShapesWithOrdinaryRelationshipsKeepTheirClassification(string provider, string[]? arguments, string mode)
    {
        var classified = AgentModeClassifier.Classify(provider, true, arguments);
        Assert.Equal(mode, classified);
        var launcher = classified == AgentModes.SharedBackend ? Related(AgentModes.EmbeddedRuntime) : null;
        Assert.Equal(classified, Check(classified, AgentModeClassifier.MayShareBackend(provider), new(launcher, [])));
    }

    // A disagreement removes the process claim and host actions but never the conversation: the same live, wait-free
    // state results from the agreed and the contradicted mode. Claude has no rule, so it stays direct.
    [Theory]
    [InlineData("claude", AgentModes.InteractiveClient)]
    [InlineData("codex", AgentModes.EmbeddedRuntime)]
    public void ADisagreementKeepsTrackingAndRemovesOnlyTheProcessClaim(string provider, string agreed)
    {
        using var workspace = new TestWorkspace();
        using var agent = DisposableSleeper.Start();
        var identity = ProcessProbe.Identify(agent.Id)!;
        var mayShare = AgentModeClassifier.MayShareBackend(provider);
        var mode = Check(agreed, mayShare, With(AgentModes.EmbeddedRuntime));
        Assert.Equal(mayShare ? AgentModes.Unknown : agreed, mode);

        var outcome = NativeAdoption.Apply(NativeAdoptionTests.Captured(provider, identity, "ignored", "observed"), "nested-session", mode, mayShare);
        NativeAdoptionTests.Capture(workspace, provider, NativeAdoptionTests.Start, outcome);
        NativeAdoptionTests.Capture(workspace, provider, NativeAdoptionTests.Prompt, outcome);
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":nested-session"];
        Assert.False(session.Ended);
        Assert.Empty(session.Waits);
        var attachment = new AttachmentStore(workspace.Paths).Get(session.AttachmentId)!;
        Assert.Equal(EvidenceStatus.HookObserved, attachment.Evidence!.ConversationHealth);
        if (mayShare)
        {
            Assert.False(outcome.Dedicated);
            Assert.Null(attachment.Process);
            Assert.Equal("unknown", attachment.Host.Kind);
            Assert.Null(attachment.Host.Shell);
            Assert.Equal("shared-runtime", attachment.Ownership);
        }
        else
        {
            Assert.True(outcome.Dedicated);
            Assert.Equal(identity, attachment.Process);
        }
    }

    // ANALYSIS-1500: Core, not the caller, decides which relationships a mode needs; nothing is read for a mode or provider no
    // rule judges, and a shared backend never reads children.
    [Theory]
    [InlineData("codex", AgentModes.SharedBackend, NativeAdoption.Relation.Parent)]
    [InlineData("codex", AgentModes.InteractiveClient, NativeAdoption.Relation.Parent | NativeAdoption.Relation.Children)]
    [InlineData("codex", AgentModes.EmbeddedRuntime, NativeAdoption.Relation.Parent | NativeAdoption.Relation.Children)]
    [InlineData("codex", AgentModes.Headless, NativeAdoption.Relation.None)]
    [InlineData("codex", AgentModes.Unknown, NativeAdoption.Relation.None)]
    [InlineData("claude", AgentModes.SharedBackend, NativeAdoption.Relation.None)]
    [InlineData("claude", AgentModes.InteractiveClient, NativeAdoption.Relation.None)]
    [InlineData("claude", AgentModes.EmbeddedRuntime, NativeAdoption.Relation.None)]
    [InlineData("claude", AgentModes.Headless, NativeAdoption.Relation.None)]
    [InlineData("claude", AgentModes.Unknown, NativeAdoption.Relation.None)]
    public void OnlyTheRelationshipsAModeNeedsAreRead(string provider, string mode, NativeAdoption.Relation expected)
    {
        var mayShare = AgentModeClassifier.MayShareBackend(provider);
        Assert.Equal(expected, NativeAdoption.Needs(mode, mayShare));
        var requests = new List<NativeAdoption.Relation>();
        NativeAdoption.CrossCheck(mode, mayShare, needs =>
        {
            requests.Add(needs);
            return With(AgentModes.EmbeddedRuntime);
        });
        Assert.Equal(expected == NativeAdoption.Relation.None ? [] : [expected], requests);
    }
}
