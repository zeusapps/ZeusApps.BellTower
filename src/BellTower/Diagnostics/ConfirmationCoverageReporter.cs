using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Diagnostics;

// Honest human-wait confirmation coverage for doctor: what is configured, what was observed, what is unavailable. A
// provisional or unverified approval request is never reported as a confirmed human wait, and elapsed hook time never
// substitutes for a signal.
public sealed class ConfirmationCoverageReporter(DataPaths paths)
{
    public const string Policy = "approval-flow starts (PermissionRequest) are provisional and never attention by themselves; a supported human-wait signal confirms them; the shared five-second grace counts any provider delay already elapsed; timing-only evidence is reported as unverified and never alerts, but a session with no supported signal (Codex without a verified owning runtime, or Claude where no permission_prompt has been observed) shows a visibly unverified \"possibly waiting for approval\" chip after 15 seconds with no sound, reminder or escalation and is never labelled confirmed";

    public object Report()
    {
        var sessions = Sessions();
        return new
        {
            Policy,
            Claude = ClaudeReport(sessions),
            Codex = CodexReport(sessions)
        };
    }

    private object ClaudeReport(IReadOnlyList<SessionState> sessions)
    {
        var observed = ConfirmationEvidence.Read(paths, "claude");
        var active = sessions.Where(session => session.Provider == "claude" && !session.Ended).ToList();
        return new
        {
            Signal = ConfirmationEvidence.ClaudeSignal + " hook (PermissionRequest is the provisional precursor)",
            Configured = "setup registers PermissionRequest and Notification(permission_prompt) hooks; the user's Claude settings are not read by doctor, so a hand-edited removal is reported only as not observed",
            Observed = observed is null ? "not observed in this data directory" : $"last seen {observed.At:O}; permission modes seen: {(observed.Modes.Count == 0 ? "none reported" : string.Join(", ", observed.Modes))}",
            KnownUnavailable = $"the signal is unavailable when the provider's permission-prompt notification hooks are disabled (for example {ClaudeConfirmationSignal.DisablingSetting}); doctor does not read the environment, so the evidence is a session with no observed permission_prompt, whose request pending 15 seconds shows a visibly unverified \"possibly waiting for approval\" chip with no sound, reminder or escalation, never labelled confirmed",
            ActiveSessions = active.Count,
            SessionsWithoutObservedPrompt = active.Count(session => !session.PromptObserved),
            Status = IngressStatus("claude") ?? (observed is null ? "configured, not yet observed: permission requests stay provisional (unverified) until a prompt arrives; a session with none observed shows the unverified chip after 15 seconds" : "observed"),
            IngressImage = IngressImage("claude"),
            VerifiedVersionModes = CapabilityMatrix.ClaudeConfirmationEvidence,
            Waits = Count(sessions, "claude")
        };
    }

    private object CodexReport(IReadOnlyList<SessionState> sessions)
    {
        var observed = ConfirmationEvidence.Read(paths, "codex");
        var active = sessions.Where(session => session.Provider == "codex" && !session.Ended).ToList();
        var runtimes = new CodexRuntimeStore(paths);
        var verifiable = active.Count(session => runtimes.Get(session.AttachmentId) is not null);
        return new
        {
            Signal = ConfirmationEvidence.CodexSignal,
            Configured = "managed local sessions own a dedicated authenticated loopback runtime that supplies the status",
            Observed = observed is null ? "not observed in this data directory" : $"last verified {observed.At:O}",
            ActiveSessions = active.Count,
            WithVerifiableRuntime = verifiable,
            UnavailableWithoutRuntime = active.Count - verifiable,
            Status = active.Count - verifiable > 0
                ? "unavailable for sessions without a verified owning runtime (explicit --no-daemon, existing or remote sessions) or whose status read fails: a request pending 15 seconds shows a visibly unverified \"possibly waiting for approval\" chip with no sound, reminder or escalation, never labelled confirmed"
                : IngressStatus("codex") ?? (observed is null ? "configured, not yet observed" : "observed"),
            IngressImage = IngressImage("codex"),
            VerifiedVersionModes = CapabilityMatrix.CodexConfirmationEvidence,
            Waits = Count(sessions, "codex")
        };
    }

    // A hook ingress image provisioned before the human-wait fields still delivers permission_prompt in the legacy shape. The
    // consumer re-derives it, but doctor asks for a setup refresh so the image is current.
    private string? IngressStatus(string provider) => ConfirmationEvidence.ReadProducer(paths, provider) is { Legacy: true }
        ? "hook ingress image predates human-wait confirmation (legacy captures are re-derived, question approvals are not distinguished): rerun setup to refresh the image" : null;

    private string IngressImage(string provider) => ConfirmationEvidence.ReadProducer(paths, provider) switch
    {
        null => "no capture seen yet",
        { Legacy: true } => "outdated: rerun setup",
        _ => "current"
    };

    private static object Count(IReadOnlyList<SessionState> sessions, string provider)
    {
        var waits = sessions.Where(session => session.Provider == provider && !session.Ended)
            .SelectMany(session => session.Waits).Where(wait => wait.Reason == AttentionReason.Permission).ToList();
        return new
        {
            Confirmed = waits.Count(wait => wait.Confirmation == HumanConfirmation.Confirmed),
            Provisional = waits.Count(wait => wait.Confirmation == HumanConfirmation.Provisional),
            Unverified = waits.Count(wait => wait.Confirmation is null or HumanConfirmation.Unverified),
            Ambiguous = waits.Count(wait => wait.Ambiguous)
        };
    }

    private IReadOnlyList<SessionState> Sessions()
    {
        try
        {
            return [.. new StateStore(paths).Read().Sessions.Values];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
