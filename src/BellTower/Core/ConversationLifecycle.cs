using BellTower.IO;

namespace BellTower.Core;

/// <summary>What the exact conversation is doing, from runtime status.</summary>
public enum WorkSignal
{
    Unknown,
    Active,
    Idle
}

/// <summary>
/// Terminal-side facts for a shared (detached backend) conversation, produced by the provider binder adapter (task 4.2).
/// The binder reports one explicit <see cref="Outcome"/> (<see cref="EvidenceStatus.Connected"/>, <see cref="EvidenceStatus.Disconnected"/>,
/// <see cref="EvidenceStatus.Demoted"/> or <see cref="EvidenceStatus.Unverified"/>) with a <see cref="Reason"/>, so an ambiguous
/// probe is reported as unverified and never as a false disconnect. <see cref="ReportedRuntimeStatus"/> is carried for
/// diagnostics only and is never read by the policy: the native probe showed it still reporting connected after the client died.
/// </summary>
public sealed record SharedTerminalFacts(
    DateTimeOffset ObservedAt,
    ProcessIdentity? OwnerClient,
    ProcessIdentity? Shell,
    string Outcome,
    string Reason,
    string? OrderingKey = null,
    string? ReportedRuntimeStatus = null)
{
    /// <summary>The shared mapping from endpoint observations to an outcome. Anything incomplete is unverified, not disconnected.</summary>
    public static SharedTerminalFacts FromEndpoint(
        DateTimeOffset observedAt,
        ProcessIdentity? owner,
        ProcessIdentity? shell,
        bool? listenerPresent,
        bool? backendConnected,
        bool demoted = false,
        string? orderingKey = null,
        string? reportedRuntimeStatus = null)
    {
        var (outcome, reason) =
            listenerPresent == false ? (EvidenceStatus.Disconnected, "listener-gone")
            : backendConnected == false ? (EvidenceStatus.Disconnected, "backend-disconnected")
            : demoted ? (EvidenceStatus.Demoted, "latest-only")
            : listenerPresent == true && backendConnected == true ? (EvidenceStatus.Connected, "client-endpoint")
            : (EvidenceStatus.Unverified, "incomplete-endpoint");
        return new(observedAt, owner, shell, outcome, reason, orderingKey, reportedRuntimeStatus);
    }
}

/// <summary>Exact-conversation runtime status (thread/read for Codex). Never backend liveness, loaded-thread lists or helper processes.</summary>
public sealed record RuntimeWorkFacts(WorkSignal Signal, DateTimeOffset ObservedAt);

/// <summary>Everything observed outside the locks about one conversation. A missing part is no observation and changes nothing.</summary>
public sealed record ConversationFacts(SharedTerminalFacts? Terminal, RuntimeWorkFacts? Work);

/// <summary>
/// A read-only snapshot of observations keyed by session key (<c>provider:sessionId</c>), filled by the provider adapters before
/// the drain takes the state and presentation locks. The drain and the policy perform no I/O to obtain it.
/// </summary>
public sealed class ConversationEvidenceSnapshot(IReadOnlyDictionary<string, ConversationFacts>? facts = null)
{
    public static readonly ConversationEvidenceSnapshot Empty = new();
    private readonly IReadOnlyDictionary<string, ConversationFacts> facts = facts ?? new Dictionary<string, ConversationFacts>();

    public ConversationFacts? Find(string sessionKey) => facts.GetValueOrDefault(sessionKey);
}

/// <summary>
/// One shared rule for terminal disconnect versus continuing work (design decision 2), reached through
/// <see cref="AttachmentHealth"/>. A shared-backend conversation has no process of its own: the terminal side comes from the
/// owner client and shell identities plus the snapshot terminal outcome, the work side from the latest of its hooks and fresh
/// exact-conversation runtime status, and neither ever ends the conversation. The legacy fields it writes (Health,
/// UnverifiedSince) keep their old meaning, so an older tray reads the result safely.
/// </summary>
public static class ConversationLifecycle
{
    /// <summary>Exact-conversation runtime status older than this is stale and is not work evidence.</summary>
    public static readonly TimeSpan RuntimeFreshness = TimeSpan.FromMinutes(2);
    /// <summary>A stored connected observation older than this is no longer a connection; it ages to unverified.</summary>
    public static readonly TimeSpan TerminalFreshness = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public static bool IsSharedConversation(Attachment attachment) =>
        attachment.Process is null && AttachmentEvidence.Of(attachment).LifecycleOwner == LifecycleOwners.SharedBackend;

    /// <summary>
    /// Reduces a shared conversation. The evidence time only moves forward: it is the latest of the last hook, the stored
    /// unverified anchor, registration and fresh active runtime work, so every source of evidence gets the same configured
    /// grace. A later hook or active status revives an <c>unavailable</c> shared conversation to <c>unverified</c>, never to
    /// <c>verified-live</c>. Stored evidence with an unusable revision is left as it is.
    /// </summary>
    internal static Attachment Reduce(Attachment attachment, ConversationFacts? facts, DateTimeOffset now, TimeSpan grace, Func<ProcessIdentity, ProcessLiveness> inspect)
    {
        var evidence = AttachmentEvidence.Of(attachment);
        var terminal = ReduceTerminal(evidence.TerminalBinding, facts?.Terminal, now, inspect);
        var evidenceAt = new[]
        {
            evidence.ConversationObservedAt, attachment.UnverifiedSince, attachment.RegisteredAt, ActiveWorkAt(facts?.Work, now)
        }.Max()!.Value;
        return attachment with
        {
            Health = now - evidenceAt >= grace ? "unavailable" : "unverified",
            UnverifiedSince = evidenceAt,
            Evidence = AttachmentEvidence.IsUnusableRevision(attachment) ? attachment.Evidence : evidence with { Terminal = terminal }
        };
    }

    private static TerminalEvidence ReduceTerminal(TerminalEvidence stored, SharedTerminalFacts? facts, DateTimeOffset now, Func<ProcessIdentity, ProcessLiveness> inspect)
    {
        // Late facts older than the state already recorded never overwrite it.
        if (facts is not null && stored.ObservedAt is { } known && facts.ObservedAt < known)
        {
            facts = null;
        }
        // Non-late facts are the current binding observation: their owner (possibly none) replaces the stored client, and the
        // shell is replaced together with it. The same owner keeps the recorded shell unless the facts name one.
        var owner = facts is null ? stored.Client : facts.OwnerClient;
        var shell = facts is null ? stored.Shell
            : owner is not null && owner == stored.Client ? facts.Shell ?? stored.Shell : facts.Shell;
        var result = stored with { Shell = shell };
        var liveness = owner is null ? ProcessLiveness.Unverified : inspect(owner);
        if (liveness == ProcessLiveness.Ended)
        {
            result = Changed(result, EvidenceStatus.Disconnected, "owner-exited", owner, shell, facts, now);
        }
        else if (facts is not null)
        {
            // An unreadable or missing owner never blocks the reported outcome, but cannot back a claimed connection.
            var (outcome, reason) = facts.Outcome == EvidenceStatus.Connected && liveness != ProcessLiveness.Live
                ? (EvidenceStatus.Unverified, "owner-unverifiable") : (facts.Outcome, facts.Reason);
            result = Changed(result, outcome, reason, owner, shell, facts, now);
        }
        else if (result.Binding == EvidenceStatus.Connected && result.ObservedAt is { } observed && now - observed > TerminalFreshness)
        {
            // Nobody re-confirmed the connection: it is no longer known to hold.
            result = result with { Binding = EvidenceStatus.Unverified, Source = "stale-connection" };
        }
        if (result.Binding != EvidenceStatus.Disconnected && shell is { } recorded && inspect(recorded) == ProcessLiveness.Ended)
        {
            result = Changed(result, EvidenceStatus.Disconnected, "shell-exited", owner, shell, null, now);
        }
        return result;
    }

    private static TerminalEvidence Changed(TerminalEvidence stored, string binding, string reason, ProcessIdentity? owner, ProcessIdentity? shell,
        SharedTerminalFacts? facts, DateTimeOffset now)
    {
        if (facts is null && stored.Binding == binding && stored.Source == reason && stored.Client == owner && stored.Shell == shell)
        {
            return stored;
        }
        return stored with
        {
            Binding = binding,
            Source = reason,
            Client = owner,
            Shell = shell,
            OrderingKey = facts?.OrderingKey ?? stored.OrderingKey,
            ObservedAt = facts?.ObservedAt ?? now
        };
    }

    // Only fresh active runtime status is work evidence. An idle or merely loaded thread extends nothing.
    private static DateTimeOffset? ActiveWorkAt(RuntimeWorkFacts? work, DateTimeOffset now) =>
        work is { Signal: WorkSignal.Active } active && active.ObservedAt <= now + ClockSkew && now - active.ObservedAt <= RuntimeFreshness
            ? (active.ObservedAt < now ? active.ObservedAt : now) : null;
}
