using System.CommandLine;
using BellTower.Setup;

namespace BellTower.Cli;

internal sealed class ImportCommand : Command
{
    public ImportCommand(Option<string?> dataDirectory) : base("import", "Copy explicitly selected legacy badge and preference files.")
    {
        var badge = CommonOptions.Text("--badge", "Legacy badge JSON file.");
        var provider = CommonOptions.Provider(false);
        var session = CommonOptions.Text("--session", "Native session ID for the imported badge.");
        var config = CommonOptions.Text("--config", "Legacy preference file.");
        var position = CommonOptions.Text("--position", "Legacy strip position file.");
        Options.Add(badge);
        Options.Add(provider);
        Options.Add(session);
        Options.Add(config);
        Options.Add(position);
        Validators.Add(result =>
        {
            if (result.GetValue(badge) is not null && (result.GetValue(provider) is null || result.GetValue(session) is null))
            {
                result.AddError("--badge requires --provider and --session.");
            }
        });
        SetAction(result =>
        {
            var paths = CommonOptions.Paths(result, dataDirectory);
            if (result.GetValue(badge) is { } file)
            {
                LegacyImport.ImportBadge(paths, file, result.GetValue(provider)!, result.GetValue(session)!, Environment.CurrentDirectory);
            }

            LegacyImport.ImportPreferences(paths, result.GetValue(config), result.GetValue(position));
        });
    }
}
