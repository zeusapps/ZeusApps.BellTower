using BellTower.Core;

namespace BellTower.Hosts;

/// <summary>
/// The stated reasons a click produces when it cannot focus the exact destination. One text per cause for both clients, so a
/// click always explains itself (design decision 8, task 4.1).
/// </summary>
public static class NavigationReasons
{
    /// <summary>The click named no process Bell Tower can verify.</summary>
    public const string ProcessUnverified = "The session process cannot be verified.";

    /// <summary>
    /// A conversation on a shared provider backend (plain default Codex): tracking, alerts and name sync work, but no terminal is
    /// bound to it. Names the limitation and the two supported routes, as the capability matrix does.
    /// </summary>
    public const string SharedBackend =
        "This session runs on the shared Codex backend, so Bell Tower cannot tell which terminal it is in. " +
        "Alerts still work. To click through to a terminal, start Codex with the Codex profile or with codex --no-daemon.";

    /// <summary>A conversation with no process of its own whose client mode is not recognized: it has no verified terminal.</summary>
    public const string TerminalUnverified =
        "Bell Tower could not verify a terminal for this session, so a click cannot reach it. Alerts and tracking still work.";

    /// <summary>Windows Terminal was focused but the tab is not selected because Bell Tower is not allowed to manage the tab title.</summary>
    public const string WindowOnlyPresentationOff =
        "Windows Terminal focused, but the tab was not selected because \"Title and color native sessions\" is off. " +
        "Turn it on in the Bell Tower tray menu, or select the tab manually.";

    /// <summary>Windows Terminal was focused but the tab was not selected: the provider writes its own tab title, and Bell Tower writes none until the opt-in step stops that.</summary>
    public const string WindowOnlyProviderTitle =
        "Windows Terminal focused, but the tab was not selected because the provider writes its own tab title in this session. " +
        "Run setup --provider-titles (or set the provider's own setting) and start a new session, or select the tab manually.";

    /// <summary>The stated reason for a conversation that has no process of its own (a shared conversation), or null for any other attachment.</summary>
    public static string? ForSharedConversation(Attachment? attachment) =>
        attachment is { EndedAt: null } && ConversationLifecycle.IsSharedConversation(attachment)
            ? AttachmentEvidence.Of(attachment).Mode == AgentModes.SharedBackend ? SharedBackend : TerminalUnverified
            : null;
}
