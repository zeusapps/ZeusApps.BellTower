using BellTower.IO;

namespace BellTower.Core;

/// <summary>
/// One adoption policy for both providers. Process evidence, not an inherited Bell Tower flag or attachment identifier,
/// decides whether a hook belongs to a direct client process. Anything that cannot be proven direct fails closed to the
/// shared-runtime branch, which keeps the conversation observable without claiming a process or terminal.
/// </summary>
public static class NativeAdoption
{
    public sealed record Outcome(Attachment Attachment, bool Dedicated);

    /// <summary>Capture-time decision from the classified mode. Provider facts (can it share a backend) come from the adapter.</summary>
    public static Outcome Apply(Attachment captured, string? sessionId, string mode, bool mayShareBackend, ProcessIdentity? agentParent = null)
    {
        var lifecycle = AttachmentEvidence.LifecycleFor(captured.Ownership, captured.Process is not null);
        var generation = captured.Process;
        if (mode == AgentModes.Headless)
        {
            // A headless run is observed, but it never claims a terminal, presentation or host.
            return new(captured with
            {
                Host = new("unknown"),
                ManagedTitle = false,
                ManagedBackground = false,
                Evidence = new() { Mode = mode, Generation = generation, LifecycleOwner = lifecycle, Terminal = new() { Binding = EvidenceStatus.None } }
            }, true);
        }
        var direct = mode is AgentModes.InteractiveClient or AgentModes.EmbeddedRuntime || mode == AgentModes.Unknown && !mayShareBackend;
        if (direct)
        {
            return new(captured with { Evidence = new() { Mode = mode, Generation = generation, LifecycleOwner = lifecycle } }, true);
        }
        if (mode == AgentModes.Unknown && captured.Ownership == "managed" && captured.Process is not null)
        {
            // The mode is unproven, but a Bell Tower launch record may prove this exact agent process. On the wire the
            // capture is the fail-closed shared shape, so any consumer that cannot verify it sees a shared conversation;
            // the direct candidate rides in evidence and only Promote plus Reconcile can turn it into an attachment.
            var shared = Shared(captured, sessionId, mode);
            return new(shared with
            {
                Evidence = shared.Evidence! with
                {
                    Provisional = true,
                    AgentParent = agentParent,
                    Candidate = new DirectCandidate
                    {
                        Id = captured.Id,
                        Process = captured.Process!,
                        Host = captured.Host,
                        ManagedTitle = captured.ManagedTitle,
                        ManagedBackground = captured.ManagedBackground
                    }
                }
            }, false);
        }
        return new(Shared(captured, sessionId, mode), false);
    }

    /// <summary>
    /// The direct form of a provisional wire capture, with the launch identity text it carried, or null when the capture is
    /// not provisional. The result must still pass <see cref="Reconcile"/>.
    /// </summary>
    public static (Attachment Attachment, string? Name, string? Change)? Promote(Attachment wire)
    {
        if (wire.Evidence is not { Provisional: true, Candidate: { } candidate } evidence)
        {
            return null;
        }
        return (wire with
        {
            Id = candidate.Id,
            Process = candidate.Process,
            Host = candidate.Host,
            ManagedTitle = candidate.ManagedTitle,
            ManagedBackground = candidate.ManagedBackground,
            Ownership = "managed",
            Evidence = evidence with
            {
                Candidate = null,
                Generation = candidate.Process,
                LifecycleOwner = LifecycleOwners.BellTower
            }
        }, candidate.Name, candidate.Change);
    }

    /// <summary>Fail-closed form: a conversation without a claimed process, host or presentation.</summary>
    public static Attachment Shared(Attachment captured, string? sessionId, string mode = AgentModes.Unknown) => captured with
    {
        Id = SharedId(captured.Provider, sessionId ?? throw new InvalidDataException("session")),
        Process = null,
        Host = new("unknown"),
        ManagedTitle = false,
        ManagedBackground = false,
        Ownership = "shared-runtime",
        Evidence = new() { Mode = mode, LifecycleOwner = LifecycleOwners.SharedBackend }
    };

    /// <summary>
    /// Adoption-time rule for inherited launch metadata, applied under the attachment lock. A provisional managed capture
    /// stays direct only while the recorded launch process is live and is the hooked agent. A managed identifier carried by a
    /// different live agent is a nested client: it keeps its own process identity and acquires no presentation consent,
    /// launch-derived shell or launch identity text.
    /// </summary>
    public static Attachment Reconcile(Attachment captured, Attachment? previous, string? sessionId, Func<ProcessIdentity, bool> live, ProcessIdentity? launchShell = null)
    {
        if (captured.Evidence is { Provisional: true } pending)
        {
            // Proof is either the launch record's own process (bell-tower launch) or the recorded launching shell of a profile
            // launch, which must be live with the same creation time and be the hooked agent's direct parent. A nested
            // client's parent is another shell, a stale record's shell is dead or reused, and neither proves anything.
            var recordedLaunch = previous is { Ownership: "managed", Process: { } owner } && captured.Process == owner && live(owner);
            var profile = previous is not { Process: { } other } || other == captured.Process;
            var shell = profile && launchShell is { } launch && pending.AgentParent == launch && live(launch);
            var verified = recordedLaunch || shell;
            return verified ? captured with { Evidence = pending with { Provisional = false } } : Shared(captured, sessionId, pending.Mode);
        }
        if (captured.Ownership == "managed" && previous is { Ownership: "managed", Process: { } recorded } &&
            captured.Process is { } agent && recorded != agent && live(recorded))
        {
            // Only a shell proven by this client's own ancestry survives; one named by an inherited variable belongs to the launcher.
            var host = captured.Host.ShellSource == "ancestry" ? captured.Host : new HostTarget("unknown");
            return captured with
            {
                Id = OwnId(captured.Provider, agent),
                Ownership = "observed",
                Host = host,
                ManagedTitle = false,
                ManagedBackground = false,
                Evidence = (captured.Evidence ?? AttachmentEvidence.Of(captured)) with
                {
                    LifecycleOwner = AttachmentEvidence.LifecycleFor("observed", true),
                    Generation = agent
                }
            };
        }
        return captured;
    }

    public static string SharedId(string provider, string sessionId) => provider + "-session-" + ContentHash.Compute(sessionId)[..24];

    /// <summary>The identifier of an unmanaged attachment: its provider and agent process generation.</summary>
    public static string OwnId(string provider, ProcessIdentity process) => $"{provider}-{process.Pid}-{process.StartedUtcTicks}";
}
