using System.Diagnostics;
using BellTower.Distribution;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>What a manual version switch did, each part observed.</summary>
public sealed record SwitchResult(bool Stopped, bool Selected, string Reported, bool TrayStarted, string Detail)
{
    public bool Passed => Stopped && Selected && TrayStarted;
}

/// <summary>
/// The documented manual selection of a retained payload (docs/releases.md, "restore a known-good current.json selection"; the update
/// coordinator's own recovery does the same two steps): the tray is exited first, the capture and attachment-health compatibility of the
/// payload being selected is checked with the product's own check (<see cref="CaptureCompatibility.Ensure"/>, so enabled fast hooks need a
/// capture-v1 consumer), the pointer <c>current.json</c> is rewritten under the installation gate, and the tray is started again through the
/// stable launcher. Neither the data directory nor any agent session is touched, so a rollback never rolls user data back. It is the harness's
/// stand-in for a rollback, which the product has no command for; the payload being left behind stays on disk as <c>previous</c>.
/// </summary>
public static class InstallSwitch
{
    /// <param name="startTray">Whether to start the tray under the newly selected version (false leaves it stopped, for reads that must precede any tray pass).</param>
    public static SwitchResult Select(Instance instance, string version, string previous, string activation, bool startTray = true)
    {
        instance.Tower("exit");
        var stopped = TowerReader.WaitFor(() => !TowerReader.TrayRunning(instance), 30000);
        var installation = new Installation(instance.Layout.Install);
        var contract = installation.Contract(version);
        var detail = "";
        var selected = false;
        try
        {
            CaptureCompatibility.Ensure(instance.Layout.Data, contract.Capture, contract.AttachmentHealth);
            using (var gate = new InstallationGate(installation.Root))
            {
                DistributionJson.Write(installation.SelectionPath, new VersionSelection(version, previous, activation));
            }
            selected = installation.Selection().Version == version;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            detail = "The selection was refused: " + exception.Message;
        }
        var reported = instance.Tower("--version").Output.Trim();
        var started = false;
        if (startTray && selected)
        {
            instance.Tower("start", "--silent");
            started = TowerReader.WaitFor(() => TowerReader.TrayRunning(instance), 40000);
        }
        else if (!startTray)
        {
            started = true;
        }
        return new(stopped, selected && reported == version, reported, started, detail);
    }

    /// <summary>The version folder each running Bell Tower process of this instance was started from (for example <c>0.1.9</c>), read from the process image path.</summary>
    public static IReadOnlyList<string> RunningVersions(Instance instance)
    {
        var versions = new List<string>();
        var root = Path.Combine(instance.Layout.Install, "versions") + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcessesByName("bell-tower"))
        {
            try
            {
                if (process.MainModule?.FileName is { } image && image.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    versions.Add(image[root.Length..].Split(Path.DirectorySeparatorChar)[0]);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // A process that exits while it is being read is not this instance's running tray.
            }
            finally
            {
                process.Dispose();
            }
        }
        return versions;
    }
}
