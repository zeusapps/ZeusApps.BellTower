using BellTower.Core;

namespace BellTower.Providers;

// Claude adapter fact for the shared unverified-chip policy: a Claude session in which no permission_prompt notification has
// been observed has no demonstrated human-wait signal (the provider can switch these notifications off, for example with
// CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS). Bell Tower does not read the user's environment; the absence of any
// observed prompt in the session is the evidence. The policy itself (15 s, silent, never confirmed) stays in Core.
public static class ClaudeConfirmationSignal
{
    public const string DisablingSetting = "CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS";

    public static void Apply(IEnumerable<SessionState> sessions)
    {
        foreach (var session in sessions.Where(session => session.Provider == "claude"))
        {
            session.ConfirmationSignalUnavailable = !session.PromptObserved;
        }
    }
}
