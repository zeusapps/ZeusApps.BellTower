using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;
using BellTower.Tools;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

/// <summary>Cross-conversation surface checks, the unfinished-parity outcome and the three-leg rotation (DEV-007).</summary>
public sealed class SurfaceAndGateTests
{
    private static SurfaceSnapshot Snapshot(string key, string tab, string details, string speech) =>
        new SurfaceSnapshot(key, key, "#000000", "", "", "", "", "", "", "", "", "", "badge", tab, key, details, speech, "work") with { ChipName = tab };

    [Fact]
    public void ConversationsNamedAlikeInTheFirstFourWordsSpeakTheSamePhrase()
    {
        var snapshots = new[]
        {
            Snapshot("a", "Login flow review DEP-1 [x]", "branch: a", "Login flow review DEP, working"),
            Snapshot("b", "Login flow review DEP-2 [y]", "branch: b", "Login flow review DEP, working")
        };
        Assert.True(SurfaceDistinctness.Tabs(snapshots).Passed);
        Assert.True(SurfaceDistinctness.Chips(snapshots).Passed);
        var speech = SurfaceDistinctness.Speech(snapshots);
        Assert.False(speech.Passed);
        Assert.Contains("a and b", Assert.Single(speech.Collisions));
    }

    [Fact]
    public void ConversationsCollapsedOntoOneCheckoutFailTabAndChipDistinctness()
    {
        var snapshots = new[] { Snapshot("a", "main [ main ]", "branch: main", "main, working"), Snapshot("b", "main [ main ]", "branch: main", "main, working") };
        Assert.False(SurfaceDistinctness.Tabs(snapshots).Passed);
        Assert.False(SurfaceDistinctness.Chips(snapshots).Passed);
    }

    [Fact]
    public void AnUnfinishedItemIsCountedAsItsOwnOutcomeAndIsNeverAPass()
    {
        var report = new RunReport();
        report.Isolation = new([], [], [], [], [], true, "note");
        report.Checks.Add(new("s", "ok", ProviderNames.Claude, EvidenceLabel.InstalledHarness, true, "fine"));
        report.Checks.Add(new("s", "gap", ProviderNames.Codex, EvidenceLabel.InstalledHarness, false, "open", Unfinished: true));
        Assert.Equal(1, report.UnfinishedItems);
        Assert.Equal(0, report.HarnessFailures);
        Assert.True(report.Passed);
        // QA-180: the verdict and the exit code say that parity items are unfinished; the run is never a plain PASSED.
        Assert.Equal("PASSED WITH UNFINISHED ITEMS", report.Verdict);
        Assert.Equal(3, report.ExitCode);
        // ANALYSIS-204: the machine-readable result carries the verdict and the exit code, not only `passed`.
        using (var json = System.Text.Json.JsonDocument.Parse(report.ToJson()))
        {
            Assert.Equal("PASSED WITH UNFINISHED ITEMS", json.RootElement.GetProperty("verdict").GetString());
            Assert.Equal(3, json.RootElement.GetProperty("exitCode").GetInt32());
        }
        var text = report.ToMarkdown();
        Assert.Contains("**PASSED WITH UNFINISHED ITEMS**", text);
        Assert.Contains("1 of 2 checks passed", text);
        Assert.Contains("1 unfinished parity items", text);
        Assert.Contains("UNFINISHED (open parity gap, not a pass)", text);
        report.Checks.Add(new("s", "bad", ProviderNames.Codex, EvidenceLabel.InstalledHarness, false, "x"));
        Assert.False(report.Passed);
        Assert.Equal("FAILED", report.Verdict);
        Assert.Equal(1, report.ExitCode);
        Assert.Equal(1, report.HarnessFailures);
    }

    [Fact]
    public void ARunWithoutUnfinishedItemsIsAPlainPassWithExitCodeZero()
    {
        var report = new RunReport();
        report.Isolation = new([], [], [], [], [], true, "note");
        report.Checks.Add(new("s", "ok", ProviderNames.Claude, EvidenceLabel.InstalledHarness, true, "fine"));
        Assert.Equal("PASSED", report.Verdict);
        Assert.Equal(0, report.ExitCode);
    }

    // ANALYSIS-183: a missing or rejected hook run fails the contract gate even when the mean looks fast.
    [Fact]
    public void TheContractGateFailsWhenAnyPlannedHookRunIsMissingOrNotAccepted()
    {
        Assert.True(BellTower.FieldAcceptance.Scenarios.PackagedHookBenchmarkScenario.ContractGate(true, 121, 121, 121, 50, 40));
        Assert.True(BellTower.FieldAcceptance.Scenarios.PackagedHookBenchmarkScenario.ContractGate(true, 121, 121, 121, -190, -20));
        Assert.False(BellTower.FieldAcceptance.Scenarios.PackagedHookBenchmarkScenario.ContractGate(true, 121, 121, 120, 5, 5));
        Assert.False(BellTower.FieldAcceptance.Scenarios.PackagedHookBenchmarkScenario.ContractGate(true, 121, 120, 120, 5, 5));
        Assert.False(BellTower.FieldAcceptance.Scenarios.PackagedHookBenchmarkScenario.ContractGate(false, 121, 121, 121, 5, 5));
        Assert.False(BellTower.FieldAcceptance.Scenarios.PackagedHookBenchmarkScenario.ContractGate(true, 121, 121, 121, 300, 5));
        Assert.False(BellTower.FieldAcceptance.Scenarios.PackagedHookBenchmarkScenario.ContractGate(true, 121, 121, 121, 5, double.NaN));
    }

    [Fact]
    public void ThreeLegsRotateSoEveryLegTakesEveryPosition()
    {
        string[] legs = ["echo", "neutral", "native"];
        var firsts = Enumerable.Range(0, 3).Select(batch => HookTiming.Rotate(legs, batch, 0)[0]).ToList();
        Assert.Equal(legs, firsts);
        Assert.Equal(legs.Order(), HookTiming.Rotate(legs, 4, 1).Order());
    }

    [Fact]
    public void TheNeutralControlsStartANativeNoOpThroughEachProvidersShell()
    {
        Assert.Equal("/usr/bin/printf 1", ProviderModel.Claude.NeutralCommand);
        Assert.Equal("[Console]::Out.Write('1')", ProviderModel.Codex.NeutralCommand);
    }
}
