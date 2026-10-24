using System.Diagnostics;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tray;

namespace BellTower.Sessions;

public sealed class AgentLauncher(DataPaths paths)
{
    public int Launch(LaunchOptions options)
    {
        var attachments = new AttachmentStore(paths);
        var attachment = AttachmentCapture.Capture(options.Provider, Environment.CurrentDirectory) with
        {
            Id = Guid.NewGuid().ToString("N"),
            Process = null,
            Ownership = "managed"
        };
        attachments.Save(attachment);
        new TrayController(paths).Start(options.Silent);
        var start = new ProcessStartInfo(options.Executable ?? options.Provider + ".exe")
        {
            UseShellExecute = false
        };
        foreach (var argument in options.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["BELLTOWER_DEDICATED"] = options.Provider == "codex" && options.Arguments.Contains("--no-daemon") ? "1" : null;
        start.Environment["BELLTOWER_ATTACHMENT"] = attachment.Id;
        start.Environment["BELLTOWER_DATA_DIR"] = paths.Root;
        start.Environment["BELLTOWER_NAME"] = options.Name;
        start.Environment["BELLTOWER_CHANGE"] = options.Change;
        start.Environment["BELLTOWER_SHELL_PID"] = attachment.Host.Shell?.Pid.ToString();
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start agent.");
        attachment = attachment with
        {
            Process = ProcessProbe.Identify(child.Id)
        };
        attachments.Save(attachment);
        child.WaitForExit();
        attachments.Save(attachment with
        {
            EndedAt = DateTimeOffset.UtcNow
        });
        // End observed conversations after our child exits; never signal an agent process.
        var sessions = new StateStore(paths).Read().Sessions.Values.Where(session => session.AttachmentId == attachment.Id && !session.Ended);
        var events = new EventStore(paths);
        foreach (var session in sessions)
        {
            events.Publish(new Observation { Provider = options.Provider, SessionId = session.SessionId, AttachmentId = attachment.Id, Kind = EventKind.End });
        }

        return child.ExitCode;
    }
}
