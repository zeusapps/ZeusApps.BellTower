using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;
using BellTower.FieldAcceptance.Isolation;

namespace BellTower.FieldAcceptance.Scenarios;

public sealed record RunOptions(
    string RepositoryRoot, string RunDirectory, string BaselineArchive, string CandidateArchive, bool KeepSandbox);

/// <summary>Everything a scenario needs: isolated instances, the two packages and the report it records checks in.</summary>
public sealed class RunContext(RunOptions options, RunReport report, ProductionLocations production, SystemTools tools, RunAbort? abort = null)
{
    public RunOptions Options { get; } = options;
    public RunReport Report { get; } = report;
    public ProductionLocations Production { get; } = production;
    public SystemTools Tools { get; } = tools;
    public LockedList<Instance> Instances { get; } = new();

    /// <summary>The run-wide stop request (Ctrl+C or a closed console); no instance starts new work once it is set.</summary>
    public RunAbort Abort { get; } = abort ?? RunAbort.Current;

    // A hook-started tray and a shared-backend Codex never share an instance; each instance tracks that itself (see RealEnvironmentExposure),
    // so separate instances in one run, such as under --scenario all, stay independent.

    public string ArtifactsBase => Path.Combine(Options.RepositoryRoot, "artifacts", "field-acceptance");
    public string SandboxBase => Path.Combine(Options.RunDirectory, "sandbox");

    /// <summary>A fresh disposable installation (own install, data, fake home, fake providers). Disposed with the run.</summary>
    public Instance CreateInstance(string name)
    {
        var instance = Instance.Create(name, SandboxBase, ArtifactsBase, Production, Tools, Abort);
        Instances.Add(instance);
        return instance;
    }

    public void Check(string scenario, string id, string provider, bool passed, string detail, EvidenceLabel label = EvidenceLabel.InstalledHarness, string? command = null, bool expectedFailure = false) =>
        Report.Add(new CheckResult(scenario, id, provider, label, passed, detail, command, expectedFailure));

    /// <summary>Records a parity item that is not performed or not finished as its own outcome (never a pass; see <see cref="CheckResult.Unfinished"/>).</summary>
    public void Unfinished(string scenario, string id, string provider, string detail, EvidenceLabel label = EvidenceLabel.InstalledHarness) =>
        Report.Add(new CheckResult(scenario, id, provider, label, false, detail, Unfinished: true));
}

public interface IScenario
{
    string Id { get; }
    string Title { get; }
    void Run(RunContext context);
}

/// <summary>The scenarios the harness offers. Later field-acceptance tasks (2.4 to 2.7) add their scenarios here.</summary>
public static class ScenarioCatalog
{
    public static IReadOnlyList<IScenario> All { get; } = [new UpgradeWithoutSetupScenario(), new FiveConversationsScenario(), new PackagedHookBenchmarkScenario(), new FaultsAndRollbackScenario()];
}
