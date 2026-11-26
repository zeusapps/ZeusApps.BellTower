using BellTower.FieldAcceptance.Harness;
using BellTower.Protocol;

namespace BellTower.FieldAcceptance.Isolation;

/// <summary>
/// Two things can reach the user's real profile environment even though every child the harness starts gets a sandbox one: the
/// tray a hook starts runs as a child of the desktop shell with the shell's own environment (<c>USERPROFILE</c>, <c>CODEX_HOME</c> and
/// the rest), and a plain shared-backend Codex resolves the Codex home from that environment. The two together would let a tray reach
/// the user's live Codex backend, so one instance may not have both.
/// <para>
/// This is a mechanism, not a convention: <see cref="Instance"/> reports every session start and every hook to its own exposure, so a
/// scenario cannot forget to opt in. It is scoped per instance and sticky for the instance's whole life; the same pair in two
/// instances (separate data directories) is allowed.
/// </para>
/// </summary>
public sealed class RealEnvironmentExposure
{
    private readonly object gate = new();
    private bool sharedBackendCodex;
    private bool hookStartedTray;

    public bool HookStartedTrayUsed { get { lock (gate) { return hookStartedTray; } } }
    public bool SharedBackendCodexUsed { get { lock (gate) { return sharedBackendCodex; } } }

    /// <summary>
    /// Runs a report to this exposure. When it refuses, <paramref name="endTrays"/> runs first (detecting a hook start cannot undo it, so the
    /// trays must end at once) and the refusal propagates.
    /// </summary>
    public void Guard(Action<RealEnvironmentExposure> report, Action endTrays)
    {
        try
        {
            report(this);
        }
        catch (IsolationException)
        {
            endTrays();
            throw;
        }
    }

    /// <summary>A session is about to start: a Codex started without <c>--no-daemon</c> uses the shared backend.</summary>
    public void NoteSessionStart(ProviderModel model, IReadOnlyList<string> arguments)
    {
        if (model.UsesSharedBackend(arguments))
        {
            Mark(shared: true);
        }
    }

    /// <summary>A hook ran: compares the recorded tray start attempt before and after it.</summary>
    public void NoteHook((DateTimeOffset At, string Outcome, string Detail)? before, (DateTimeOffset At, string Outcome, string Detail)? after)
    {
        if (HookStartedATray(before, after))
        {
            Mark(shared: false);
        }
    }

    /// <summary>True when the record changed to a <c>Started</c> outcome, i.e. this hook (not an earlier one) started a tray.</summary>
    public static bool HookStartedATray((DateTimeOffset At, string Outcome, string Detail)? before, (DateTimeOffset At, string Outcome, string Detail)? after) =>
        after is { } attempt && !Equals(before, after) && attempt.Outcome == nameof(HookStartOutcome.Started);

    private void Mark(bool shared)
    {
        bool both;
        lock (gate)
        {
            if (shared)
            {
                sharedBackendCodex = true;
            }
            else
            {
                hookStartedTray = true;
            }
            both = sharedBackendCodex && hookStartedTray;
        }
        if (both)
        {
            throw new IsolationException(["a hook-started tray (real profile environment) and a shared-backend Codex (plain codex without --no-daemon) cannot share one instance; use separate instances"]);
        }
    }
}
