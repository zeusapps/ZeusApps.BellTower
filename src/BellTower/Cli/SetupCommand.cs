using System.CommandLine;
using BellTower.Setup;

namespace BellTower.Cli;

internal sealed class SetupCommand : Command
{
    public SetupCommand(Option<string?> dataDirectory) : base("setup", "Plan reversible agent configuration. Writes only with --apply.")
    {
        var clients = new Option<string>("--clients")
        {
            Description = "Agents to configure.",
            DefaultValueFactory = _ => "both",
            Arity = ArgumentArity.ExactlyOne
        };
        clients.AcceptOnlyFromAmong("both", "claude", "codex");
        var home = CommonOptions.Text("--home", "Alternate home directory for isolated setup.");
        var project = CommonOptions.Text("--project", "Add project-specific profiles starting in this directory; --remove removes only these project profiles.");
        var statusLine = new Option<bool>("--status-line")
        {
            Description = "Enable the Claude name chip and native Codex name/branch footer fields."
        };
        var remove = new Option<bool>("--remove")
        {
            Description = "Remove only Bell Tower-owned entries."
        };
        var apply = new Option<bool>("--apply")
        {
            Description = "Apply the displayed plan."
        };
        var desktop = new Option<bool>("--desktop")
        {
            Description = "Set up session functions, detected terminal profiles, Claude badge, and bundled VS Code focus bridge."
        };
        var hostBridge = new Option<bool>("--host-bridge")
        {
            Description = "Configure native hooks and the optional VS Code focus bridge without terminal profiles or shell imports."
        };
        var providerTitles = new Option<bool>("--provider-titles")
        {
            Description = "Opt-in step: stop the provider writing its own terminal title (whole provider profile) so Bell Tower's session title can persist; --remove undoes it."
        };
        var migrate = new Option<bool>("--migrate-edited-hooks")
        {
            Description = "Explicitly replace edited Bell Tower hook handlers after preview; --apply writes backups."
        };
        Options.Add(clients);
        Options.Add(home);
        Options.Add(project);
        Options.Add(statusLine);
        Options.Add(remove);
        Options.Add(apply);
        Options.Add(desktop);
        Options.Add(hostBridge);
        Options.Add(migrate);
        Options.Add(providerTitles);
        SetAction(result =>
        {
            var paths = CommonOptions.Paths(result, dataDirectory);
            var setup = new IntegrationSetup(paths, BellTower.Distribution.Installation.StableExecutable(Environment.ProcessPath!), result.GetValue(home));
            var plan = result.GetValue(providerTitles)
                ? setup.PlanProviderTitles(result.GetValue(clients)!, result.GetValue(remove))
                : setup.Plan(result.GetValue(clients)!, result.GetValue(remove), result.GetValue(statusLine), result.GetValue(desktop), result.GetValue(project),
                    result.GetValue(migrate), result.GetValue(hostBridge));
            var output = result.InvocationConfiguration.Output;
            foreach (var file in plan.Changes)
            {
                output.WriteLine((result.GetValue(remove) ? "Remove owned entries: " : "Configure: ") + file.Path);
            }
            foreach (var action in plan.Actions)
            {
                output.WriteLine(action.Description);
            }
            foreach (var note in plan.Notes)
            {
                output.WriteLine(note);
            }

            if (result.GetValue(apply))
            {
                plan.Apply(paths);
                output.WriteLine("Applied. Restart agents and review provider hook trust. Codex: /hooks.");
            }
            else
            {
                output.WriteLine("Dry run. Add --apply to change these files.");
            }
        });
    }
}
