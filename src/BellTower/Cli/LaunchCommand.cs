using System.CommandLine;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class LaunchCommand : Command
{
    public LaunchCommand(Option<string?> dataDirectory, IReadOnlyList<string> agentArguments) : base("launch", "Launch an agent. Put agent arguments after --.")
    {
        var provider = new Argument<string?>("provider")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "claude or codex."
        };
        provider.AcceptOnlyFromAmong("claude", "codex");
        var providerOption = CommonOptions.Provider(false);
        var executable = CommonOptions.Text("--executable", "Agent executable path.");
        var name = CommonOptions.Text("--name", "Initial session display name.");
        var change = CommonOptions.Text("--change", "Explicit change association.");
        var role = CommonOptions.Text("--role", "Role used in announcements.");
        var silent = CommonOptions.Silent();
        Arguments.Add(provider);
        Options.Add(providerOption);
        Options.Add(executable);
        Options.Add(name);
        Options.Add(change);
        Options.Add(role);
        Options.Add(silent);
        Validators.Add(result =>
        {
            var positional = result.GetValue(provider);
            var named = result.GetValue(providerOption);
            if (positional is null && named is null)
            {
                result.AddError("Use launch claude|codex.");
            }

            if (positional is not null && named is not null && positional != named)
            {
                result.AddError("The provider argument and --provider must agree.");
            }
        });
        SetAction(result => new AgentLauncher(CommonOptions.Paths(result, dataDirectory)).Launch(new LaunchOptions(
            result.GetValue(provider) ?? result.GetValue(providerOption)!, result.GetValue(executable),
            result.GetValue(name), result.GetValue(change), result.GetValue(role), result.GetValue(silent), agentArguments)));
    }
}
