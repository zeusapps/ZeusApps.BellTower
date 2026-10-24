using System.CommandLine;
using BellTower.Core;
using BellTower.IO;
using BellTower.Sessions;

namespace BellTower.Cli;

internal sealed class NativeSyncCommand : Command
{
    public NativeSyncCommand(Option<string?> dataDirectory) : base("native-sync", "Enable or pause Bell Tower's native chat-name updates (Codex; Claude Code's native title write is unfinished).")
    {
        var session = CommonOptions.Text("--session", "Provider-qualified session ID (claude:... or codex:...).", true);
        var mode = CommonOptions.Text("--mode", "enable takes ownership of the current native name; disable pauses updates.", true);
        mode.AcceptOnlyFromAmong("enable", "disable");
        Options.Add(session);
        Options.Add(mode);
        SetAction(result =>
        {
            var paths = CommonOptions.Paths(result, dataDirectory);
            if (!new StateStore(paths).Read().Sessions.TryGetValue(result.GetValue(session)!, out var state) || state.Ended)
            {
                throw new ArgumentException("A live registered Claude Code or Codex session is required.");
            }
            try
            { NativeNameControl.SetEnabled(paths, state, result.GetValue(mode) == "enable"); }
            catch (NotSupportedException error)
            { throw new ArgumentException(error.Message); }
            new CodexNameService(paths).Sync(state);
            result.InvocationConfiguration.Output.WriteLine(NativeNamePolicy.StatusFor(state.Provider, new IdentityStore(paths).Get(state.Key, state.Directory)));
        });
    }
}
