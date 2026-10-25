using System.Diagnostics;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tray;

namespace BellTower.Updates;

public static class UpdateTransaction
{
    internal static Action<string, string>? PayloadRemovalTestCheckpoint { get; set; }
    public static string Marker(DataPaths paths) => Path.Combine(paths.Root, "updates", "restart.json");
    public static string Presence(DataPaths paths) => Path.Combine(paths.Root, "updates", "tray.json");
    public static string PlanPath(Installation installation, string id) => Path.Combine(installation.Root, "updates", id, "plan.json");
    public static string OutcomePath(Installation installation) => Path.Combine(installation.Root, "updates", "outcome.json");
    public static bool RestartPending(DataPaths paths)
    {
        try
        {
            if (!File.Exists(Marker(paths)))
            { return false; }
            var plan = DistributionJson.Read<UpdatePlan>(Marker(paths), 16384);
            return DateTimeOffset.UtcNow - plan.Created < TimeSpan.FromMinutes(10) && ProcessProbe.IsAlive(plan.Owner);
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException) { return false; }
    }
    public static bool CanStartTray(DataPaths paths)
    {
        if (!RestartPending(paths))
        { return true; }
        var plan = DistributionJson.Read<UpdatePlan>(Marker(paths), 16384);
        return Environment.GetEnvironmentVariable("BELLTOWER_UPDATE_ACTIVATION") == plan.Id;
    }
    public static UpdatePlan Stage(Installation installation, DataPaths paths, string archive, ReleaseOffer offer)
    {
        offer.EnsureStable();
        Installation.EnsurePlainPath(paths.Root);
        if (Contains(installation.Root, paths.Root) || Contains(paths.Root, installation.Root))
        { throw new IOException("Keep installation and data directories separate."); }
        using var mutation = new InstallationGate(installation.Root, "update", 0);
        ValidateData(paths);
        CaptureCompatibility.Ensure(paths.Root, offer.Contract.Capture, offer.Contract.AttachmentHealth);
        EnsureNoCoordinator(installation);
        var selected = installation.Selection();
        // A dev selection counts as its stable base version, so a stable release can replace it.
        if (ReleaseContract.Parse(offer.Version) <= installation.Contract(selected.Version).EffectiveVersion)
        { throw new IOException("This version is already selected or superseded."); }
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(installation.Root, "updates", id);
        Installation.EnsurePlainPath(staging);
        Directory.CreateDirectory(staging);
        var payload = ReleasePackage.Extract(archive, Path.Combine(staging, "package"), offer);
        ReleasePackage.Probe(payload, offer.Version);
        // Every retained payload can still write to the shared state. Refuse unknown contracts before selecting anything.
        foreach (var existing in Directory.EnumerateDirectories(Path.Combine(installation.Root, "versions")))
        { DistributionJson.Read<ReleaseContract>(Path.Combine(existing, "release.json")).Validate(); }
        var target = Path.Combine(installation.Root, "versions", offer.Version);
        using (var launch = new InstallationGate(installation.Root))
        {
            if (Directory.Exists(target))
            { throw new IOException("Candidate version already exists; inspect retained recovery files before retrying."); }
            Directory.Move(payload, target);
        }
        var plan = new UpdatePlan(id, Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), installation.Root,
            paths.Root, selected.Version, offer.Version, Installation.Hash(Path.Combine(target, "release.json")), DateTimeOffset.UtcNow,
            ProcessProbe.Identify(Environment.ProcessId)!, new TrayController(paths).IsRunning());
        DistributionJson.Write(PlanPath(installation, id), plan);
        DistributionJson.Write(Path.Combine(installation.Root, "updates", "pending.json"), plan);
        WriteOutcome(installation, plan, "staged", "Verified payload staged; activation and monitoring recovery are not complete.");
        return plan;
    }
    public static void Start(UpdatePlan plan)
    {
        var start = new ProcessStartInfo(Path.Combine(plan.Root, "versions", plan.Target, "bell-tower.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "__apply-update", PlanPath(new Installation(plan.Root), plan.Id), plan.Token })
        { start.ArgumentList.Add(arg); }
        using var helper = Process.Start(start) ?? throw new IOException("Update coordinator could not start.");
    }
    public static int Apply(string path, string token)
    {
        UpdatePlan? plan = null;
        Installation? installation = null;
        var switched = false;
        var paths = default(DataPaths);
        try
        {
            var candidate = DistributionJson.Read<UpdatePlan>(path, 16384);
            if (!Guid.TryParseExact(candidate.Id, "N", out _) || candidate.Token.Length != 64 || candidate.Token != token ||
                candidate.Created > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - candidate.Created > TimeSpan.FromMinutes(10))
            { throw new InvalidDataException("Invalid or expired update plan."); }
            installation = new Installation(candidate.Root);
            if (Path.GetFullPath(path) != PlanPath(installation, candidate.Id) ||
                !string.Equals(Environment.ProcessPath, Path.Combine(installation.Payload(candidate.Target), "bell-tower.exe"), StringComparison.OrdinalIgnoreCase))
            { throw new InvalidDataException("Coordinator does not match staged payload."); }
            using var mutation = new InstallationGate(installation.Root, "update", 0);
            var pending = DistributionJson.Read<UpdatePlan>(Path.Combine(installation.Root, "updates", "pending.json"), 16384);
            if (pending.Id != candidate.Id || pending.Token != token)
            { throw new IOException("Update plan was superseded."); }
            plan = candidate with { Owner = ProcessProbe.Identify(Environment.ProcessId)! };
            paths = new DataPaths(plan.DataDirectory);
            paths.Ensure();
            ValidateData(paths);
            var targetContract = DistributionJson.Read<ReleaseContract>(Path.Combine(installation.Payload(plan.Target), "release.json"));
            CaptureCompatibility.Ensure(paths.Root, targetContract.Capture, targetContract.AttachmentHealth);
            Installation.EnsurePlainPath(paths.Root);
            if (Installation.Hash(Path.Combine(installation.Payload(plan.Target), "release.json")) != plan.ContractHash)
            { throw new InvalidDataException("Staged metadata changed."); }
            Installation.VerifyFiles(installation.Payload(plan.Target), DistributionJson.Read<ReleaseContract>(Path.Combine(installation.Payload(plan.Target), "release.json")));
            if (installation.Selection().Version != plan.Original)
            { throw new IOException("Selection changed after staging; update superseded."); }
            DistributionJson.Write(Path.Combine(installation.Root, "updates", "pending.json"), plan);
            DistributionJson.Write(Marker(paths), plan);
            if (plan.HadTray)
            {
                var controller = new TrayController(paths);
                if (controller.IsRunning())
                {
                    controller.RequestExit();
                    WaitUntil(() => !controller.IsRunning(), "Tray did not stop; sessions were left running.");
                }
            }
            using (var launch = new InstallationGate(installation.Root))
            {
                if (installation.Selection().Version != plan.Original)
                { throw new IOException("Selection changed before activation."); }
                DistributionJson.Write(installation.SelectionPath, new VersionSelection(plan.Target, plan.Original, plan.Id));
            }
            switched = true;
            WriteOutcome(installation, plan, "activated", "New invocations use this version. Existing session helpers continue on their original version.");
            var integration = RefreshIntegrations(paths, installation);
            if (plan.HadTray)
            {
                StartTray(installation, paths, plan.Id);
                WriteOutcome(installation, plan, "monitoring-restored", "Tray reconnected; agent sessions were preserved. " + integration);
            }
            else
            { WriteOutcome(installation, plan, "activated", "Version activated; tray remains stopped. " + integration); }
            File.Delete(Marker(paths));
            File.Delete(Path.Combine(installation.Root, "updates", "pending.json"));
            try
            { Cleanup(installation); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { new LocalLog(paths).Write("update-cleanup-deferred"); }
            return 0;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (installation is not null && plan is not null)
            {
                try
                {
                    using var mutation = new InstallationGate(installation.Root, "update", 1000);
                    // A failed candidate can have missing/corrupt metadata; recovery reads the pointer without trying to launch it.
                    var selected = DistributionJson.Read<VersionSelection>(installation.SelectionPath, 4096);
                    if (switched && selected.Activation == plan.Id)
                    {
                        if (paths is not null && new TrayController(paths).IsRunning())
                        {
                            var presence = DistributionJson.Read<TrayPresence>(Presence(paths) + ".starting", 4096);
                            if (presence.Activation != plan.Id)
                            { throw new IOException("Another tray now owns monitoring; recovery requires inspection."); }
                            new TrayController(paths).RequestExit();
                            WaitUntil(() => !new TrayController(paths).IsRunning(), "Candidate tray could not exit for recovery.");
                        }
                        using (var launch = new InstallationGate(installation.Root))
                        {
                            var originalContract = DistributionJson.Read<ReleaseContract>(Path.Combine(installation.Payload(plan.Original), "release.json"));
                            CaptureCompatibility.Ensure(paths!.Root, originalContract.Capture, originalContract.AttachmentHealth);
                            DistributionJson.Write(installation.SelectionPath, new VersionSelection(plan.Original, plan.Target, plan.Id));
                        }
                    }
                    if (plan.HadTray && paths is not null && !new TrayController(paths).IsRunning())
                    { StartTray(installation, paths, plan.Id); }
                    WriteOutcome(installation, plan, switched ? "rolled-back" : "failed", error.Message + " Prior selection/monitoring retained or restored; state was not rolled back.");
                    if (paths is not null)
                    { File.Delete(Marker(paths)); }
                    var pendingPath = Path.Combine(installation.Root, "updates", "pending.json");
                    if (File.Exists(pendingPath) && DistributionJson.Read<UpdatePlan>(pendingPath).Id == plan.Id)
                    { File.Delete(pendingPath); }
                }
                catch (Exception recovery) when (recovery is not OutOfMemoryException)
                { WriteOutcome(installation, plan, "failed", error.Message + " Recovery needs attention: " + recovery.Message); }
            }
            Console.Error.WriteLine("bell-tower update: " + error.Message);
            return 1;
        }
    }
    public static void Cleanup(Installation installation)
    {
        using var mutation = new InstallationGate(installation.Root, "update");
        var retained = RetainedVersions(installation);
        foreach (var directory in Directory.EnumerateDirectories(Path.Combine(installation.Root, "versions")))
        { _ = TryRemovePayload(installation, Path.GetFileName(directory), retained, false); }
    }
    /// <summary>Removes one dev-marked payload under the same retention rules as <see cref="Cleanup"/>. Returns null when removed, otherwise the reason it was kept.</summary>
    public static string? Retire(Installation installation, string version)
    {
        _ = ReleaseContract.Parse(version);
        using var mutation = new InstallationGate(installation.Root, "update");
        return TryRemovePayload(installation, version, RetainedVersions(installation), true);
    }
    /// <summary>Versions that must stay, each with the reason: the selected version, its previous version, and versions an unfinished update may need to recover.</summary>
    public static Dictionary<string, string> RetainedVersions(Installation installation)
    {
        var selected = installation.Selection();
        var retained = new Dictionary<string, string>(StringComparer.Ordinal) { [selected.Version] = "the selected version" };
        if (selected.Previous is not null)
        { retained.TryAdd(selected.Previous, "the recorded previous version (needed for rollback)"); }
        var updatesRoot = Path.Combine(installation.Root, "updates");
        if (Directory.Exists(updatesRoot))
        {
            foreach (var folder in Directory.GetDirectories(updatesRoot))
            {
                Installation.EnsurePlainPath(folder);
                var planFile = Path.Combine(folder, "plan.json");
                if (!File.Exists(planFile))
                { continue; }
                var outcomeFile = Path.Combine(folder, "outcome.json");
                if (!File.Exists(outcomeFile) || DistributionJson.Read<UpdateOutcome>(outcomeFile).State is "failed" or "rolled-back" or "staged")
                {
                    var recovery = DistributionJson.Read<UpdatePlan>(planFile);
                    retained.TryAdd(recovery.Original, "retained by an unfinished update's recovery material");
                    retained.TryAdd(recovery.Target, "retained by an unfinished update's recovery material");
                }
            }
        }
        return retained;
    }
    /// <summary>The one removal rule: not retained, valid contract, no lease, only owned files with matching hashes. The caller holds the update gate; this method keeps the launch gate only while checking selection and acquiring an exclusive payload lease.</summary>
    public static string? TryRemovePayload(Installation installation, string version, IReadOnlyDictionary<string, string> retained, bool requireDevMarker)
    {
        FileStream lease;
        using (var launch = new InstallationGate(installation.Root))
        {
            var selection = installation.Selection();
            if (selection.Version == version)
            { return $"{version} is the selected version."; }
            if (selection.Previous == version)
            { return $"{version} is the recorded previous version (needed for rollback)."; }
            if (retained.TryGetValue(version, out var why))
            { return $"{version} is {why}."; }
            try
            { lease = installation.Lease(version, true); }
            catch (IOException)
            { return $"{version} is leased by a running Bell Tower process."; }
        }
        var directory = Path.Combine(installation.Root, "versions", version);
        using (lease)
        {
            try
            {
                var contract = DistributionJson.Read<ReleaseContract>(Path.Combine(directory, "release.json"));
                contract.Validate();
                if (requireDevMarker && contract.Build is not { Channel: ReleaseBuild.DevChannel })
                { return $"{version} has no dev marker, so it is not retired here; the tray's automatic cleanup reclaims unmarked payloads once they are neither selected nor retained."; }
                Installation.EnsurePlainPath(directory);
                var files = CollectOwnedFiles(directory).ToArray();
                // Preserve unowned or modified files, including recovery material.
                if (files.FirstOrDefault(file => Path.GetRelativePath(directory, file).Replace('\\', '/') is var name && name != "release.json" && contract.Files?.ContainsKey(name) != true) is { } unowned)
                { return $"{version} holds a file Bell Tower did not install ({Path.GetRelativePath(directory, unowned)}); inspect it privately."; }
                PayloadRemovalTestCheckpoint?.Invoke(installation.Root, version);
                Installation.VerifyFiles(directory, contract);
                foreach (var file in files)
                { Installation.EnsurePlainPath(file); }
                Directory.Delete(directory, true);
                return null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            { return $"{version} could not be removed: {error.Message}"; }
        }
    }
    private static IEnumerable<string> CollectOwnedFiles(string directory)
    {
        Installation.EnsurePlainPath(directory);
        foreach (var file in Directory.GetFiles(directory))
        { Installation.EnsurePlainPath(file); yield return file; }
        foreach (var child in Directory.GetDirectories(directory))
        {
            foreach (var file in CollectOwnedFiles(child))
            { yield return file; }
        }
    }
    private static void ValidateData(DataPaths paths)
    {
        if (!File.Exists(paths.State))
        { return; }
        var state = DistributionJson.Read<System.Text.Json.JsonElement>(paths.State, 16 * 1024 * 1024);
        if (!state.TryGetProperty("schemaVersion", out var schema) || !schema.TryGetInt32(out var version) || version != 1)
        { throw new InvalidDataException("Shared state requires maintenance; activation was deferred."); }
    }
    private static string RefreshIntegrations(DataPaths paths, Installation installation)
    {
        try
        { return new IntegrationRefresh(paths, installation.Launcher).Apply(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { return "Integration refresh pending: " + error.Message; }
    }
    private static void StartTray(Installation installation, DataPaths paths, string activation)
    {
        var start = new ProcessStartInfo(installation.Launcher) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "tray", "--data-dir", paths.Root })
        { start.ArgumentList.Add(arg); }
        start.Environment["BELLTOWER_UPDATE_ACTIVATION"] = activation;
        using var child = Process.Start(start) ?? throw new IOException("Tray restart could not start.");
        WaitUntil(() =>
        {
            if (!File.Exists(Presence(paths)))
            { return false; }
            var presence = DistributionJson.Read<TrayPresence>(Presence(paths), 4096);
            return presence.Activation == activation && presence.Version == installation.Selection().Version &&
                presence.DataDirectory == paths.Root && ProcessProbe.IsAlive(presence.Process) && new TrayController(paths).IsRunning();
        }, "Updated tray did not confirm monitoring readiness.");
    }
    private static void WaitUntil(Func<bool> ready, string message)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(20))
        {
            if (ready())
            { return; }
            Thread.Sleep(50);
        }
        throw new IOException(message);
    }
    public static void EnsureNoCoordinator(Installation installation) => EnsureNoCoordinator(installation, null);
    public static void EnsureNoCoordinator(Installation installation, UpdatePlan? expectedRecovery)
    {
        var pending = Path.Combine(installation.Root, "updates", "pending.json");
        if (!File.Exists(pending))
        { return; }
        var plan = DistributionJson.Read<UpdatePlan>(pending, 16384);
        if (expectedRecovery is not null && plan == expectedRecovery && Path.GetFullPath(plan.Root).Equals(installation.Root, StringComparison.OrdinalIgnoreCase) &&
            ProcessProbe.Inspect(plan.Owner) == ProcessLiveness.Ended)
        { return; }
        if (ProcessProbe.IsAlive(plan.Owner) && DateTimeOffset.UtcNow - plan.Created < TimeSpan.FromMinutes(10))
        { throw new IOException("A staged update is active; wait and inspect update --status."); }
        throw new IOException("An interrupted update needs inspection. Retain its recovery directory; see update --status and releases documentation.");
    }
    private static bool Contains(string parent, string child) => Path.GetFullPath(parent).Equals(Path.GetFullPath(child), StringComparison.OrdinalIgnoreCase) ||
        Path.GetFullPath(child).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static void WriteOutcome(Installation installation, UpdatePlan plan, string state, string message)
    {
        var outcome = new UpdateOutcome(plan.Id, state, plan.Target, message, DateTimeOffset.UtcNow);
        Installation.EnsurePlainPath(Path.Combine(installation.Root, "updates", plan.Id, "outcome.json"));
        DistributionJson.Write(Path.Combine(installation.Root, "updates", plan.Id, "outcome.json"), outcome);
        var path = OutcomePath(installation);
        if (File.Exists(path))
        {
            var existing = DistributionJson.Read<UpdateOutcome>(path);
            if (existing.Id != plan.Id && existing.At > plan.Created)
            { return; }
        }
        DistributionJson.Write(path, outcome);
    }
}
