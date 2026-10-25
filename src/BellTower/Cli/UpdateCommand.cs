using System.CommandLine;
using BellTower.Distribution;
using BellTower.Updates;

namespace BellTower.Cli;

internal sealed class UpdateCommand : Command
{
    public UpdateCommand(Option<string?> dataDirectory) : base("update", "Check for or explicitly install a session-preserving stable Windows update.")
    {
        var check = new Option<bool>("--check") { Description = "Check release metadata without installing a package." };
        var status = new Option<bool>("--status") { Description = "Read the last local update outcome without network access." };
        Options.Add(check);
        Options.Add(status);
        Validators.Add(result => { if (result.GetValue(check) && result.GetValue(status)) { result.AddError("--check and --status cannot be combined."); } });
        SetAction(async (result, token) =>
        {
            var output = result.InvocationConfiguration.Output;
            var installation = Installation.ForPayload(Environment.ProcessPath!);
            if (result.GetValue(status))
            {
                var path = installation is null ? null : UpdateTransaction.OutcomePath(installation);
                var outcome = path is not null && File.Exists(path) ? DistributionJson.Read<UpdateOutcome>(path, 16384) : null;
                output.WriteLine($"Running {ApplicationInfo.Display}; selected {(installation is null ? "unpackaged" : installation.DisplayVersion(installation.Selection().Version))}.");
                output.WriteLine(outcome is null ? "No update outcome recorded." : $"{outcome.State}: {outcome.Version}. {outcome.Message}");
                return outcome?.State == "failed" ? 1 : 0;
            }
            if (!result.GetValue(check) && installation is null)
            { throw new InvalidOperationException("Self-update requires the versioned Windows ZIP layout. Extract a release into a stable separate installation and preview setup --desktop. Existing legacy sessions may require a one-time maintenance window; no sessions were stopped. See " + ReleaseOffer.Releases); }
            var paths = CommonOptions.Paths(result, dataDirectory);
            if (!result.GetValue(check))
            { Installation.EnsurePlainPath(paths.Root); }
            using var http = ReleaseClient.CreateClient();
            var client = new ReleaseClient(http);
            var discovery = new UpdateDiscovery(paths, client);
            var release = await discovery.CheckAsync(true, token);
            if (release?.Error is { } error)
            { throw new IOException(error); }
            var selected = installation is null ? ApplicationInfo.Display : installation.DisplayVersion(installation.Selection().Version);
            var effective = installation?.EffectiveVersion() ?? ReleaseContract.Effective(ApplicationInfo.Version, null);
            var basis = VersionDisplay.IsDev(selected) ? $" Stable releases are compared with its base version {effective}." : "";
            if (release?.Offer is not { } offer || ReleaseContract.Parse(offer.Version) <= effective)
            { output.WriteLine($"Running {ApplicationInfo.Display}; selected {selected}. No newer compatible stable release available.{basis}"); return 0; }
            output.WriteLine($"Running {ApplicationInfo.Display}; selected {selected}; available {offer.Version}. {offer.Page}");
            if (result.GetValue(check))
            { return 0; }
            var download = Path.Combine(paths.Root, "updates", "downloads", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(download);
            Installation.EnsurePlainPath(download);
            var archive = Path.Combine(download, offer.Contract.PackageName);
            await client.DownloadAsync(offer.Url, archive, ReleasePackage.MaximumBytes, token);
            await client.DownloadAsync(offer.Url + ".sha256", archive + ".sha256", 4096, token);
            var plan = UpdateTransaction.Stage(installation!, paths, archive, offer);
            try
            { UpdateTransaction.Start(plan); }
            catch (Exception startError)
            {
                UpdateTransaction.WriteOutcome(installation!, plan, "failed", "Coordinator could not start: " + startError.Message);
                throw;
            }
            output.WriteLine("staged: " + offer.Version + ". Wait for activation/monitoring recovery, then use bell-tower update --status. Running agent sessions remain open.");
            return 0;
        });
    }
}
