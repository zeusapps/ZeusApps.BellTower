using System.CommandLine;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class RenameCommand : Command
{
    public RenameCommand(Option<string?> dataDirectory) : base("rename", "Edit a session's name, explicit change association, or role.")
    {
        var session = CommonOptions.Text("--session", "Provider-qualified session ID.", true);
        var name = CommonOptions.Text("--name", "Display name; empty clears the override.");
        var change = CommonOptions.Text("--change", "Explicit change association; empty clears it.");
        var role = CommonOptions.Text("--role", "Role used in announcements; empty clears it.");
        Options.Add(session);
        Options.Add(name);
        Options.Add(change);
        Options.Add(role);
        SetAction(result =>
        {
            var catalog = new SessionCatalog(CommonOptions.Paths(result, dataDirectory));
            var identity = catalog.Rename(result.GetValue(session)!, result.GetValue(name), result.GetValue(change), result.GetValue(role));
            result.InvocationConfiguration.Output.WriteLine(identity.Name);
        });
    }
}
