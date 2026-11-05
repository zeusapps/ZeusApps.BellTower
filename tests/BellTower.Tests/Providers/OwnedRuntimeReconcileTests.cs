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

    private static Attachment Provisional(ProcessIdentity? parent, Attachment? previous = null, NativeAdoption.OwnedRuntime? runtime = null, Func<ProcessIdentity, bool>? live = null)
    {
        var outcome = NativeAdoption.Apply(Captured("codex", Backend, Id, "managed", Inherited, "launch-env"), "owned-session", AgentModes.Unknown,
            AgentModeClassifier.MayShareBackend("codex"), parent);
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
}
