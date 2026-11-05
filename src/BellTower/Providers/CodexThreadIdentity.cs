using System.Text.Json;

namespace BellTower.Providers;

/// <summary>
/// The explicit mapping from a hook session identifier to a protocol thread (design decision 4). What is actually proven is
/// narrow: the thread the runtime returns carries exactly the id that was requested, and on a shared backend the runtime was
/// verified as the socket's peer process. That shows the read and any write go to the thread Bell Tower named; it does not show
/// which thread a fork's or a delegated thread's hook names, because nobody has observed which session id those hooks carry
/// (task 6.1 fork hook probe).
/// On a runtime Bell Tower does not own, a thread that reports a parent, a fork origin, an agent role or nickname, or a session
/// other than its own id is therefore refused as a conservative policy, not as a proof of a wrong mapping, and it is reported
/// with <see cref="CodexTransportFailure.ThreadUnverified"/>, distinct from a true id mismatch
/// (<see cref="CodexTransportFailure.ThreadUnmapped"/>). Evidence 011 shows a fork also reports sessionId equal to its id, so
/// the session field cannot tell a fork from a root; the fork fields do. A missing id is refused instead of guessed; absent fork and session fields read as a root.
/// Only identity fields are read; the thread's preview and other content are never touched here.
/// </summary>
public static class CodexThreadIdentity
{
    public static void Require(JsonElement thread, string requested, bool proven)
    {
        if (thread.ValueKind != JsonValueKind.Object)
        {
            throw Unmapped("The runtime returned no thread for the conversation.");
        }
        var id = Text(thread, "id");
        if (id is not null && !string.Equals(id, requested, StringComparison.OrdinalIgnoreCase))
        {
            throw Unmapped("The runtime returned a different thread than the one requested.");
        }
        if (!proven)
        {
            return;
        }
        if (id is null)
        {
            throw Unmapped("The runtime did not report the thread identifier, so the conversation is not proven.");
        }
        if (thread.TryGetProperty("sessionId", out var session) && session.ValueKind != JsonValueKind.Null &&
            !(session.ValueKind == JsonValueKind.String && string.Equals(session.GetString(), id, StringComparison.OrdinalIgnoreCase)))
        {
            throw Unverified("The thread reports another session tree; its mapping to the hook's session is unverified.");
        }
        foreach (var field in new[] { "parentThreadId", "forkedFromId", "agentRole", "agentNickname" })
        {
            if (thread.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null)
            {
                throw Unverified("A forked or delegated thread is refused: its mapping to the hook's session is unverified.");
            }
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static CodexTransportException Unverified(string message) => new(CodexTransportFailure.ThreadUnverified, message);

    private static CodexTransportException Unmapped(string message) => new(CodexTransportFailure.ThreadUnmapped, message);
}
