namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// Cross-conversation checks over what each conversation shows (ANALYSIS-162): conversations that share one launch directory must show
/// different tabs, chip details and spoken phrases. The tab, chip details and speech are product functions evaluated in-process with the
/// harness build over the installed state (not installed outputs); only the badge is the installed command's output. Nothing here derives a
/// name or phrase itself, and no conversation is exempt: a conversation that falls back to the launch checkout's name fails.
/// </summary>
public static class SurfaceDistinctness
{
    public sealed record Outcome(string Surface, IReadOnlyList<string> Collisions)
    {
        public bool Passed => Collisions.Count == 0;
    }

    public static Outcome Tabs(IReadOnlyCollection<SurfaceSnapshot> snapshots) => Collide("tab", snapshots, snapshot => snapshot.Tab);

    /// <summary>The chip's inputs are the identity name and the git details string; together they must tell two conversations apart.</summary>
    public static Outcome Chips(IReadOnlyCollection<SurfaceSnapshot> snapshots) => Collide("chip", snapshots, snapshot => snapshot.ChipName + " | " + snapshot.ChipDetails);

    public static Outcome Speech(IReadOnlyCollection<SurfaceSnapshot> snapshots) => Collide("speech", snapshots, snapshot => snapshot.Speech);

    private static Outcome Collide(string surface, IReadOnlyCollection<SurfaceSnapshot> snapshots, Func<SurfaceSnapshot, string> value) =>
        new(surface, [.. snapshots.GroupBy(value, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => string.Join(" and ", group.Select(snapshot => snapshot.Key)) + " both show '" + group.Key + "'")]);
}
