namespace BellTower.FieldAcceptance.Isolation;

/// <summary>
/// Proves, against the real production locations of the user running the harness, that the guard would have stopped the mistakes it
/// exists for. It only evaluates the pure rules (nothing is created, started or read), and runs at the start of every run.
/// </summary>
public static class GuardSelfTest
{
    public sealed record Probe(string Id, bool Passed, string Detail);

    public static IReadOnlyList<Probe> Run(ProductionLocations production, string artifactsBase, IReadOnlyCollection<string> systemTools)
    {
        var probes = new List<Probe>();
        var sandbox = new InstanceLayout("self-test", Path.Combine(artifactsBase, "runs", "self-test", "sandbox", "self-test"));
        var insideBase = ProductionGuard.CheckLayout(sandbox, production, artifactsBase);
        probes.Add(new("legitimate-sandbox-accepted", insideBase.Count == 0, "A sandbox under the harness artifacts base is accepted (" + insideBase.Count + " violations)."));

        var locations = new List<(string Id, string Root)> { ("data-root", production.DataRoot), ("installation-root", production.InstallationRoot) };
        locations.AddRange(production.Roots.Where(root => root.EndsWith(".claude", StringComparison.OrdinalIgnoreCase) || root.EndsWith(".codex", StringComparison.OrdinalIgnoreCase))
            .Select(root => (Path.GetFileName(root), root)));
        foreach (var (id, root) in locations)
        {
            var layout = new InstanceLayout("self-test", Path.Combine(root, "harness-self-test"));
            var violations = ProductionGuard.CheckLayout(layout, production, root);
            probes.Add(new("sandbox-in-" + id, violations.Any(violation => violation.Contains("overlaps a production location", StringComparison.Ordinal)),
                "A sandbox placed inside the production " + id.Replace("-", " ") + " is refused."));
        }

        var launcher = Path.Combine(production.InstallationRoot, "bell-tower.exe");
        probes.Add(new("command-production-launcher", ProductionGuard.CheckCommand(launcher, ["doctor", "--data-dir", sandbox.Data], sandbox, production, systemTools).Count > 0,
            "Running the production installation's launcher is refused."));
        probes.Add(new("command-production-data", ProductionGuard.CheckCommand(sandbox.Launcher, ["doctor", "--data-dir", production.DataRoot], sandbox, production, systemTools).Count > 0,
            "A sandbox command pointed at the production data directory is refused."));
        probes.Add(new("command-missing-data-dir", ProductionGuard.CheckCommand(sandbox.Launcher, ["doctor"], sandbox, production, systemTools).Count > 0,
            "A Bell Tower command without an explicit --data-dir is refused (it would default to the production data directory)."));
        probes.Add(new("command-production-profile", ProductionGuard.CheckCommand(sandbox.Launcher, ["setup", "--home", production.Homes[0], "--apply", "--data-dir", sandbox.Data], sandbox, production, systemTools).Count > 0,
            "setup --home pointed at the user's own profile (which holds ~/.claude and ~/.codex) is refused."));
        probes.Add(new("environment-production-data", ProductionGuard.CheckEnvironment(new Dictionary<string, string> { ["BELLTOWER_DATA_DIR"] = production.DataRoot }, production).Count > 0,
            "A child environment naming the production data directory is refused."));
        probes.Add(new("environment-production-path", ProductionGuard.CheckEnvironment(new Dictionary<string, string> { ["PATH"] = production.InstallationRoot + Path.PathSeparator + @"C:\Windows" }, production).Count > 0,
            "A PATH entry inside the production installation is refused (and BuildEnvironment drops such entries from child environments)."));
        return probes;
    }
}
