using System.CommandLine;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class RenameCommand : Command
{
    public RenameCommand(Option<string?> dataDirectory) : base("rename", "Edit a session's name or explicit change association.")
    {
        var session = CommonOptions.Text("--session", "Provider-qualified session ID.", true);
        var name = CommonOptions.Text("--name", "Display name; empty clears the override.");
        var change = CommonOptions.Text("--change", "Explicit change association; empty clears it.");
        // Deprecated: Bell Tower has no session role. Accepted and ignored so existing scripts keep working.
        var role = CommonOptions.Text("--role", "Deprecated and ignored.");
        role.Hidden = true;
        var checkout = CommonOptions.Text("--checkout", "Working checkout; empty clears the pin.");
        var color = CommonOptions.Text("--color", "Named color, #RRGGBB, or random.");
        var reset = new Option<bool>("--reset") { Description = "Clear Bell Tower title/change/checkout/color pins and choose a new palette." };
        var automatic = new Option<bool>("--use-bell-tower-name") { Description = "Drop the native user title and return to the automatic or Bell Tower name (Claude Code and Codex)." };
        Options.Add(session);
        Options.Add(name);
        Options.Add(change);
        Options.Add(role);
        Options.Add(checkout);
        Options.Add(color);
        Options.Add(reset);
        Options.Add(automatic);
        SetAction(result =>
        {
            var catalog = new SessionCatalog(CommonOptions.Paths(result, dataDirectory));
            var identity = catalog.Rename(result.GetValue(session)!, result.GetValue(name), result.GetValue(change));
            if (result.GetValue(automatic))
            { identity = catalog.UseBellTowerName(result.GetValue(session)!); }
            if (result.GetValue(checkout) is not null || result.GetValue(color) is not null || result.GetValue(reset))
            { identity = catalog.Configure(result.GetValue(session)!, result.GetValue(checkout), result.GetValue(color), result.GetValue(reset)); }
            result.InvocationConfiguration.Output.WriteLine(identity.Name);
        });
    }
}
