using System.Text.Json.Serialization;

namespace BellTower.FieldAcceptance.Evidence;

/// <summary>
/// What kind of evidence a check is. Fixture: a synthetic payload or fake provider process exercised in-process or through a unit
/// test. InstalledHarness: the real packaged binaries (launcher, payload, native ingress) installed under disposable roots and
/// driven through their commands and installed hook commands with fake agents. Live: a real provider on a real machine, which the
/// harness never produces.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceLabel
{
    Fixture,
    InstalledHarness,
    Live
}

public static class ProviderNames
{
    public const string Claude = "claude";
    public const string Codex = "codex";
    public const string Mixed = "mixed";
    public const string Harness = "harness";
}

/// <summary>
/// One check. <paramref name="ExpectedFailure"/> marks a check whose predicate is "the known defect was observed": it is reported as an
/// expected failure, separate from harness failures, and counts as passed only when the failure showed up as predicted.
/// <paramref name="Unfinished"/> marks a parity item that is not performed or not finished (an open gap, not a proven restriction): it is
/// never a pass (<c>Passed</c> is false), it does not fail the run, and the report counts and lists it as its own outcome.
/// </summary>
public sealed record CheckResult(string Scenario, string Id, string Provider, EvidenceLabel Label, bool Passed, string Detail, string? Command = null, bool ExpectedFailure = false, bool Unfinished = false);
