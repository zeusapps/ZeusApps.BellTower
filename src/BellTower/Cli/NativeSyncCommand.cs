using System.CommandLine;
using BellTower.IO;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class NativeSyncCommand : Command
{
    public NativeSyncCommand(Option<string?> dataDirectory) : base("native-sync", "Enable or pause Bell Tower's native Codex chat-name updates.")
    {
        var session = CommonOptions.Text("--session", "Provider-qualified Codex session ID.", true);
        var mode = CommonOptions.Text("--mode", "enable takes ownership of the current native name; disable pauses updates.", true);
        mode.AcceptOnlyFromAmong("enable", "disable");
        Options.Add(session);
        Options.Add(mode);
        SetAction(result =>
        {
            var paths = CommonOptions.Paths(result, dataDirectory);
            if (!new StateStore(paths).Read().Sessions.TryGetValue(result.GetValue(session)!, out var state) || state.Ended)
            {
                throw new ArgumentException("A live registered Codex session is required.");
            }
            var service = new CodexNameService(paths);
            service.SetEnabled(state, result.GetValue(mode) == "enable");
            service.Sync(state);
            result.InvocationConfiguration.Output.WriteLine(new IdentityStore(paths).Get(state.Key, state.Directory).NativeSyncStatus);
        });
    }
}
