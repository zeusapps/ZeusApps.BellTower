using BellTower.Core;
using BellTower.Providers;
using Xunit;
using static BellTower.Tests.Providers.NativeAdoptionTests;

namespace BellTower.Tests.Providers;

// ANALYSIS-1502, policy level: a provisional managed Codex capture stays direct through an owned runtime record only when
// the record is for this attachment, names the backend's direct parent as its manager and its shell, and both are live.
public sealed class OwnedRuntimeReconcileTests
{
    private static readonly ProcessIdentity Backend = new(100, 1000);
    private static readonly ProcessIdentity Manager = new(200, 900);
    private static readonly ProcessIdentity Shell = new(300, 800);
    private static readonly ProcessIdentity Inherited = new(400, 700);
    private const string Id = "owned-attachment";

    private static Attachment Provisional(ProcessIdentity? parent, Attachment? previous = null, NativeAdoption.OwnedRuntime? runtime = null, Func<ProcessIdentity, bool>? live = null,
        IReadOnlyList<ProcessIdentity>? lineage = null, bool parentIsAgent = false)
    {
        var outcome = NativeAdoption.Apply(Captured("codex", Backend, Id, "managed", Inherited, "launch-env"), "owned-session", AgentModes.Unknown,
            AgentModeClassifier.MayShareBackend("codex"), parent, lineage, parentIsAgent);
        Assert.False(outcome.Dedicated);
        var promoted = NativeAdoption.Promote(outcome.Attachment)!.Value.Attachment;
        return NativeAdoption.Reconcile(promoted, previous, "owned-session", live ?? (_ => true), launchShell: null, runtime);
    }

    private static void AssertShared(Attachment result)
    {
        Assert.Equal(NativeAdoption.SharedId("codex", "owned-session"), result.Id);
        Assert.Null(result.Process);
        Assert.Null(result.Host.Shell);
        Assert.Equal("shared-runtime", result.Ownership);
    }

    [Fact]
    public void ABackendUnderTheLiveManagerOfItsRecordIsDirectWithTheRecordsShell()
    {
        var result = Provisional(Manager, runtime: new(Id, Manager, Shell));
        Assert.Equal(Id, result.Id);
        Assert.Equal(Backend, result.Process);
        Assert.Equal("managed", result.Ownership);
        Assert.Equal(Shell, result.Host.Shell);
        Assert.Equal("launch-record", result.Host.ShellSource);
        Assert.False(result.Evidence!.Provisional);
        Assert.Equal(LifecycleOwners.BellTower, result.Evidence.LifecycleOwner);
        AssertProofInputsCleared(result);
    }

    // ANALYSIS-1700: a proven capture keeps none of the inputs that proved it.
    private static void AssertProofInputsCleared(Attachment result)
    {
        Assert.Null(result.Evidence!.AgentParent);
        Assert.Null(result.Evidence.AgentAncestors);
        Assert.False(result.Evidence.AgentParentIsAgent);
    }

    [Fact]
    public void ARepeatedHookOfTheSameBackendStaysDirect()
    {
        var first = Provisional(Manager, runtime: new(Id, Manager, Shell));
        Assert.Equal(Id, Provisional(Manager, first, new(Id, Manager, Shell)).Id);
    }

    [Fact]
    public void AnotherProcessAlreadyRecordedForTheAttachmentFailsClosed() =>
        AssertShared(Provisional(Manager, Captured("codex", new ProcessIdentity(150, 1), Id, "managed"), new(Id, Manager, Shell)));

    public static TheoryData<string> Failures => new() { "no-record", "other-attachment", "other-manager", "manager-reused", "parent-unknown", "manager-dead", "shell-dead" };

    [Theory]
    [MemberData(nameof(Failures))]
    public void AnythingLessThanTheFullProofFailsClosed(string failure)
    {
        var parent = failure switch
        {
            "other-manager" => new ProcessIdentity(Manager.Pid + 1, Manager.StartedUtcTicks),
            "manager-reused" => Manager with { StartedUtcTicks = Manager.StartedUtcTicks + 1 },
            "parent-unknown" => null,
            _ => Manager
        };
        var runtime = failure switch
        {
            "no-record" => null,
            "other-attachment" => new NativeAdoption.OwnedRuntime("another", Manager, Shell),
            _ => new NativeAdoption.OwnedRuntime(Id, Manager, Shell)
        };
        Func<ProcessIdentity, bool> live = failure switch
        {
            "manager-dead" => process => process != Manager,
            "shell-dead" => process => process != Shell,
            _ => _ => true
        };
        AssertShared(Provisional(parent, runtime: runtime, live: live));
    }

    // QA-2400: the manager may sit behind launcher processes (npm: node; script shim: powershell, then node), found by process
    // identifier and creation time in the hooked agent's ancestry, within NativeAdoption.MaxOwnedDepth hops.
    private static ProcessIdentity Hop(int level) => new(500 + level, 950 - level * 10);

    private static IReadOnlyList<ProcessIdentity> Behind(int hops) =>
        [.. Enumerable.Range(1, hops).Select(Hop), Manager];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AManagerBehindLaunchersWithinTheBoundBindsTheBackend(int launchers)
    {
        var chain = Behind(launchers);
        Assert.Equal(launchers + 1, chain.Count);
        var result = Provisional(chain[0], runtime: new(Id, Manager, Shell), lineage: chain);
        Assert.Equal(Id, result.Id);
        Assert.Equal(Backend, result.Process);
        Assert.Equal(Shell, result.Host.Shell);
        Assert.Equal("launch-record", result.Host.ShellSource);
        AssertProofInputsCleared(result);
    }

    [Fact]
    public void TheBoundIsTheSharedConstant()
    {
        Assert.Equal(3, NativeAdoption.MaxOwnedDepth);
        AssertShared(Provisional(Hop(1), runtime: new(Id, Manager, Shell), lineage: Behind(NativeAdoption.MaxOwnedDepth)));
    }

    public static TheoryData<string> LineageFailures => new()
    {
        "no-record", "other-attachment", "manager-pid", "manager-reused", "manager-dead", "shell-dead",
        "hop-older-than-agent-child", "manager-younger-than-hop", "independent", "empty", "null-hop"
    };

    [Theory]
    [MemberData(nameof(LineageFailures))]
    public void AnythingLessThanTheFullLineageProofFailsClosed(string failure)
    {
        IReadOnlyList<ProcessIdentity> chain = failure switch
        {
            "manager-pid" => [Hop(1), new ProcessIdentity(Manager.Pid + 1, Manager.StartedUtcTicks)],
            "manager-reused" => [Hop(1), Manager with { StartedUtcTicks = Manager.StartedUtcTicks + 1 }],
            // A hop that started after the agent it launched is a reused process identifier.
            "hop-older-than-agent-child" => [new ProcessIdentity(Hop(1).Pid, Backend.StartedUtcTicks + 1), Manager],
            // The manager starting after the hop it supposedly launched is a reused manager identifier.
            "manager-younger-than-hop" => [Hop(1) with { StartedUtcTicks = Manager.StartedUtcTicks - 1 }, Manager],
            // An independently started backend: the recorded manager is nowhere in its ancestry.
            "independent" => [Hop(1), Hop(2), Inherited],
            "empty" => [],
            // ANALYSIS-1701: a null hop from a malformed spool capture ends the chain instead of throwing.
            "null-hop" => [null!, Manager],
            _ => Behind(1)
        };
        var runtime = failure switch
        {
            "no-record" => null,
            "other-attachment" => new NativeAdoption.OwnedRuntime("another", Manager, Shell),
            _ => new NativeAdoption.OwnedRuntime(Id, Manager, Shell)
        };
        Func<ProcessIdentity, bool> live = failure switch
        {
            "manager-dead" => process => process != Manager,
            "shell-dead" => process => process != Shell,
            _ => _ => true
        };
        AssertShared(Provisional(chain.Count > 0 ? chain[0] : null, runtime: runtime, live: live, lineage: chain));
    }

    // QA-2500: a nested app-server whose direct parent is itself an agent of the provider is never the owned backend, however the
    // manager sits above it; the real backend (parent is the manager or a launcher) still binds.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AHookedAgentWhoseDirectParentIsAnAgentIsRejectedEvenWithTheManagerAbove(int launchers)
    {
        var chain = Behind(launchers);
        AssertShared(Provisional(chain[0], runtime: new(Id, Manager, Shell), lineage: chain, parentIsAgent: true));
        Assert.Equal(Id, Provisional(chain[0], runtime: new(Id, Manager, Shell), lineage: chain, parentIsAgent: false).Id);
    }

    [Fact]
    public void AManagerBeyondTheBoundIsNotReachedEvenWhenTheChainIsLonger() =>
        AssertShared(Provisional(Hop(1), runtime: new(Id, Manager, Shell), lineage: [Hop(1), Hop(2), Hop(3), Manager]));
}
