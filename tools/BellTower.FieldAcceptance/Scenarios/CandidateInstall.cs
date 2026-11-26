using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;
using BellTower.IO;

namespace BellTower.FieldAcceptance.Scenarios;

/// <summary>
/// A disposable instance with the candidate installed the way a user gets it: the archive extracted under the sandbox install root,
/// <c>setup --host-bridge --apply</c> run in the sandbox home (native hooks and the provisioned ingress image for both clients), a muted
/// configuration with update checks off, and the tray running silently. Shared by the scenarios that need a running installed candidate.
/// </summary>
public static class CandidateInstall
{
    public static Instance Prepare(RunContext context, string scenario, string name)
    {
        var instance = context.CreateInstance(name);
        instance.ExtractInstallation(context.Options.CandidateArchive);
        var version = instance.Tower("--version").Output.Trim();
        var setup = instance.TowerWithHome(300000, "setup", "--host-bridge", "--apply");
        // Silent by construction: no sound and no update check (so no network), for every tray this instance ever starts.
        new ConfigStore(new DataPaths(instance.Layout.Data)).Update(config => { config.Muted = true; config.Updates.Enabled = false; });
        var start = instance.Tower("start", "--silent");
        var running = TowerReader.WaitFor(() => TowerReader.TrayRunning(instance));
        var installed = setup.Succeeded && version == context.Report.Candidate!.Version && start.Succeeded && running;
        context.Check(scenario, name + "/candidate-installed", ProviderNames.Harness, installed,
            "Extracted the candidate archive (version " + version + ") under the disposable install root, ran setup --host-bridge --apply in the sandbox home (exit " + setup.ExitCode +
            (setup.Succeeded ? "" : ": " + setup.Error.Trim()) + ") and started the tray silently (running: " + running + ").");
        if (!installed)
        {
            throw new IOException("The candidate could not be installed for scenario " + scenario + ".");
        }
        return instance;
    }

    /// <summary>The first owned hook command the provider's settings hold for an event.</summary>
    public static string Command(Instance instance, ProviderModel model, string hookEvent) =>
        model.OwnedCommand(instance.Layout, hookEvent) ?? throw new InvalidDataException("No owned " + hookEvent + " hook for " + model.Name);
}
