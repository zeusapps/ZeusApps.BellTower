namespace BellTower.Core;

/// <summary>
/// The single decision for terminal presentation of an adopted native session (design decisions 5 and 8). Ingress stays neutral:
/// a hook never sets presentation flags. Consumers resolve the effective consent here from the stored attachment and the one
/// global native-presentation setting, so turning the setting on or off applies to live sessions without a restart.
/// The tab title and the session palette background are granted together. A session Bell Tower did not launch has no recorded prior
/// color, so exit and switch-off reset the colors to the terminal default instead of restoring one (user decision, 2026-11-13).
/// </summary>
public static class NativePresentation
{
    /// <summary>
    /// A direct client Bell Tower did not launch: an observed attachment with a verified agent process and shell, its lifecycle
    /// owned by that agent process and a mode classified as a direct client. A shared-backend conversation, a headless run and a
    /// session without a verified agent or shell never qualify. A managed attachment keeps its own explicit controls.
    /// </summary>
    public static bool IsAdoptedDirect(Attachment attachment) =>
        attachment is { Ownership: "observed", EndedAt: null, Process: { } agent, Host.Shell: { } shell } &&
        agent.Pid > 0 && shell.Pid > 0 && attachment.Host.ShellSource != HostTarget.LaunchShared &&
        attachment.Evidence is { Revision: AttachmentEvidence.CurrentRevision, LifecycleOwner: LifecycleOwners.AgentProcess, Mode: not (AgentModes.SharedBackend or AgentModes.Headless), Provisional: false } stamped &&
        stamped.Generation == agent;

    /// <summary>True when the setting grants the managed title and palette background to this attachment that carries no managed title of its own.</summary>
    public static bool GrantsPresentation(Attachment attachment, bool enabled) => enabled && !attachment.ManagedTitle && IsAdoptedDirect(attachment);

    /// <summary>
    /// The attachment as the presentation and navigation consumers must see it. The setting grants the palette background; the managed title
    /// is granted only while the provider's own terminal title is disabled through Bell Tower's opt-in setup step
    /// (<paramref name="providerTitleDisabled"/>), because a provider that writes its own title overwrites Bell Tower's within a frame
    /// (design decision 11). Without the step the session keeps color only and Windows Terminal navigation stays at the window.
    /// </summary>
    public static Attachment Effective(Attachment attachment, bool enabled, bool providerTitleDisabled) =>
        GrantsPresentation(attachment, enabled) ? attachment with { ManagedTitle = providerTitleDisabled, ManagedBackground = true } : attachment;
}
