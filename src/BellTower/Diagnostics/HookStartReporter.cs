using BellTower.IO;
using BellTower.Protocol;

namespace BellTower.Diagnostics;

/// <summary>Doctor's view of design decision 9: whether a hook can start a stopped tray, the latest attempt and any user stop.</summary>
public sealed class HookStartReporter(DataPaths paths)
{
    public object Report()
    {
        var launcher = HookTrayStart.ValidLauncher(paths.Root, out var reason);
        var attempt = HookTrayStart.LastAttempt(paths.Root);
        var stopRecorded = File.Exists(HookTrayStart.UserStopRecord(paths.Root));
        var suppressed = HookTrayStart.UserStopActive(paths.Root);
        return new
        {
            Available = launcher is not null,
            Launcher = launcher,
            LauncherProblem = launcher is null ? reason : null,
            LatestAttempt = attempt is null ? null : new { At = attempt.Value.At.ToString("O"), Result = attempt.Value.Outcome, Detail = attempt.Value.Detail },
            UserStopSuppressesInThisSignIn = suppressed,
            UserStopRecord = !stopRecorded ? "none" : suppressed ? "this sign-in" : "from another sign-in (stale, ignored)",
            UpdateOrSwitchPending = HookTrayStart.UpdatePending(paths.Root),
            MinimumSecondsBetweenAttempts = (int)HookTrayStart.AttemptInterval.TotalSeconds,
            Scope = "after a capture is saved, a hook starts a stopped tray once per interval from the recorded stable launcher, for both providers; tray Exit or the exit command keeps it stopped until the tray is started by hand or the next sign-in; start the tray (bell-tower start) to turn it back on"
        };
    }
}
