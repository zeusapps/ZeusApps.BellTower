using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BellTower.FieldAcceptance.Isolation;

namespace BellTower.FieldAcceptance.Evidence;

/// <summary>How the baseline archive was obtained and verified, so evidence never rests on a rebuilt stand-in.</summary>
public sealed record BaselineProvenance(string Tag, string Source, string ArchiveName, string Sha256, string PublishedSha256, string? PublishedDescriptorSha256, bool Verified, string Note);

public sealed record CandidateProvenance(string Label, string Version, string ArchiveName, string Sha256, string BuildCommand);

public sealed record IsolationReport(
    IReadOnlyList<string> LayoutViolations, IReadOnlyList<string> EnvironmentViolations, IReadOnlyList<string> RemovedPathEntries,
    IReadOnlyList<string> ProductionLocations, IReadOnlyList<SnapshotChange> ProductionChanges, bool Passed, string Note, IReadOnlyList<string>? PreflightRemovals = null);

public sealed class RunReport
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string Harness { get; init; } = "BellTower.FieldAcceptance 1";
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public string Os { get; init; } = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
    public string Command { get; set; } = "";
    public BaselineProvenance? Baseline { get; set; }
    public CandidateProvenance? Candidate { get; set; }
    public IsolationReport? Isolation { get; set; }
    /// <summary>Safe to add to from one thread while another reads (every read is a snapshot).</summary>
    public Harness.LockedList<CheckResult> Checks { get; } = new();
    public List<string> Notes { get; } = [];
    /// <summary>Raw measurements a scenario wants kept next to the checks (for example every timed hook), keyed by scenario id. Written to result.json only.</summary>
    public Dictionary<string, object> Measurements { get; } = [];
    public int ExpectedFailures => Checks.Count(check => check.ExpectedFailure && check.Passed);
    /// <summary>Parity items not performed or not finished: counted on their own, never as passes, and they do not fail the run.</summary>
    public int UnfinishedItems => Checks.Count(check => check.Unfinished);
    public int HarnessFailures => Checks.Count(check => !check.Passed && !check.Unfinished) + (Isolation is { Passed: false } ? 1 : 0);
    public bool Passed => Checks.Count > 0 && Checks.All(check => check.Passed || check.Unfinished) && Isolation is { Passed: true };
    /// <summary>Process exit code of a completed run: 0 every check passed, 1 a failure, 3 no failure but unfinished parity items remain (open release work, never acceptance).</summary>
    public const int ExitPassedWithUnfinished = 3;
    public int ExitCode => !Passed ? 1 : UnfinishedItems > 0 ? ExitPassedWithUnfinished : 0;
    public string Verdict => !Passed ? "FAILED" : UnfinishedItems > 0 ? "PASSED WITH UNFINISHED ITEMS" : "PASSED";

    public void Add(CheckResult result)
    {
        Checks.Add(result);
        Console.WriteLine((result.Unfinished ? "UNFINISHED " : result.Passed ? "PASS " : "FAIL ") + result.Scenario + "/" + result.Id + " [" + result.Provider + "] " + result.Detail);
    }

    public string ToJson() => JsonSerializer.Serialize(new
    {
        harness = Harness,
        startedAt = StartedAt,
        finishedAt = FinishedAt,
        os = Os,
        command = Command,
        passed = Passed,
        verdict = Verdict,
        exitCode = ExitCode,
        harnessFailures = HarnessFailures,
        expectedFailures = ExpectedFailures,
        unfinishedItems = UnfinishedItems,
        baseline = Baseline,
        candidate = Candidate,
        isolation = Isolation,
        checks = Checks,
        notes = Notes,
        measurements = Measurements
    }, JsonOptions);

    public string ToMarkdown(Func<string, string>? sanitize = null)
    {
        sanitize ??= text => text;
        var text = new StringBuilder();
        text.AppendLine("# Field acceptance harness result");
        text.AppendLine();
        text.AppendLine("- Result: **" + Verdict + "** (" + Checks.Count(check => check.Passed) + " of " + Checks.Count + " checks passed; " + HarnessFailures + " harness failures; " + ExpectedFailures +
            " expected-failure checks observed the known defect as predicted; " + UnfinishedItems + " unfinished parity items, not passes)");
        text.AppendLine("- Harness: " + Harness + "; started " + StartedAt.ToString("u") + (FinishedAt is { } finished ? ", finished " + finished.ToString("u") : ""));
        text.AppendLine("- Operating system: " + Os);
        text.AppendLine("- Command: `" + Command + "`");
        if (Baseline is { } baseline)
        {
            text.AppendLine("- Historical baseline: published release `" + baseline.Tag + "` archive `" + baseline.ArchiveName + "`, SHA-256 `" + baseline.Sha256 +
                "` (published `.sha256`: `" + baseline.PublishedSha256 + "`" + (baseline.PublishedDescriptorSha256 is { } descriptor ? "; release descriptor: `" + descriptor + "`" : "") +
                "; verified: " + (baseline.Verified ? "yes" : "NO") + "). " + baseline.Note);
        }
        if (Candidate is { } candidate)
        {
            text.AppendLine("- Candidate: **" + candidate.Label + "** version `" + candidate.Version + "`, archive `" + candidate.ArchiveName + "`, SHA-256 `" + candidate.Sha256 + "`, built with `" + candidate.BuildCommand + "`.");
        }
        text.AppendLine("- Evidence labels: **Fixture** is a guard rule or synthetic payload evaluated without installed packages; **InstalledHarness** is the real packaged launcher, payload and native ingress installed under disposable roots and driven through their commands and installed hook commands with fake provider processes; **Live** is a real provider on a real machine, which this harness never produces (none in this run).");
        text.AppendLine();
        text.AppendLine("## Production isolation");
        text.AppendLine();
        if (Isolation is { } isolation)
        {
            text.AppendLine("- Result: **" + (isolation.Passed ? "no production change observed" : "FAILED") + "**. " + isolation.Note);
            text.AppendLine("- Guarded locations: " + string.Join("; ", isolation.ProductionLocations.Select(location => "`" + location + "`")));
            text.AppendLine("- Layout violations: " + (isolation.LayoutViolations.Count == 0 ? "none" : string.Join("; ", isolation.LayoutViolations)));
            text.AppendLine("- Environment violations: " + (isolation.EnvironmentViolations.Count == 0 ? "none" : string.Join("; ", isolation.EnvironmentViolations)));
            text.AppendLine("- PATH entries removed from child environments: " + isolation.RemovedPathEntries.Count);
            text.AppendLine("- Preflight sweep of harness-owned leftovers from earlier interrupted runs: " + (isolation.PreflightRemovals is { Count: > 0 } removals ? string.Join("; ", removals) : "none found"));
            foreach (var change in isolation.ProductionChanges)
            {
                text.AppendLine("- " + (change.Gating ? "CHANGED" : "info") + " " + change.Kind + ": `" + change.Subject + "` " + change.Detail);
            }
        }
        else
        {
            text.AppendLine("- Not recorded.");
        }
        text.AppendLine();
        text.AppendLine("## Checks");
        foreach (var scenario in Checks.GroupBy(check => check.Scenario))
        {
            text.AppendLine();
            text.AppendLine("### " + scenario.Key);
            text.AppendLine();
            text.AppendLine("| Check | Provider | Evidence | Result | Detail |");
            text.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var check in scenario)
            {
                text.AppendLine("| " + check.Id + " | " + check.Provider + " | " + check.Label + " | " + (check.Unfinished ? "UNFINISHED (open parity gap, not a pass)" : check.Passed ? (check.ExpectedFailure ? "expected failure (observed)" : "pass") : check.ExpectedFailure ? "FAIL (expected failure not observed)" : "FAIL") + " | " + check.Detail.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ") + " |");
            }
        }
        if (Notes.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Notes");
            text.AppendLine();
            foreach (var note in Notes)
            {
                text.AppendLine("- " + note);
            }
        }
        return sanitize(text.ToString());
    }
}
