using BellTower.IO;

namespace BellTower.Core;

/// <summary>
/// One adoption policy for both providers. Process evidence, not an inherited Bell Tower flag or attachment identifier,
/// decides whether a hook belongs to a direct client process. Anything that cannot be proven direct fails closed to the
/// shared-runtime branch, which keeps the conversation observable without claiming a process or terminal.
/// </summary>
public static class NativeAdoption
{
    /// <summary>
    /// How many hops above the hooked backend an owned runtime's manager may sit. <c>CodexRuntimeHost</c> starts the backend as
    /// a native image (manager, backend: 1), through <c>node codex.js</c> (manager, node, backend: 2) or through a PowerShell
    /// shim that may itself start node (manager, powershell, node, backend: 3). A deeper chain is not a Bell Tower launch shape.
    /// </summary>
    public const int MaxOwnedDepth = 3;

    public sealed record Outcome(Attachment Attachment, bool Dedicated);

    /// <summary>What an owned Codex runtime record proves: the attachment it serves, its live manager and the shell it belongs to.</summary>
    public sealed record OwnedRuntime(string Attachment, ProcessIdentity Manager, ProcessIdentity Shell);

    /// <summary>A same-provider process next to the agent, with the mode its own command line classifies as.</summary>
    public sealed record RelatedProcess(ProcessIdentity Identity, string Mode);

    /// <summary>The agent's direct same-provider parent and children; <c>Complete</c> is false when children were not all listed.</summary>
    public sealed record ProcessRelations(RelatedProcess? Parent, IReadOnlyList<RelatedProcess> Children, bool Complete = true);

    /// <summary>Which relationships the cross-check reads for a mode.</summary>
    [Flags]
    public enum Relation
    {
        None = 0,
        Parent = 1,
        Children = 2
    }

    /// <summary>
    /// The relationships <see cref="CrossCheck"/> needs, so a caller reads only those. A provider without a shared backend and a
    /// mode no rule judges need none; a shared backend is judged by its parent alone unless its invocation is launcher independent
    /// (then it needs none); a client by parent and children.
    /// </summary>
    public static Relation Needs(string mode, bool mayShareBackend, bool launcherIndependent = false) => !mayShareBackend ? Relation.None : mode switch
    {
        AgentModes.SharedBackend => launcherIndependent ? Relation.None : Relation.Parent,
        AgentModes.InteractiveClient or AgentModes.EmbeddedRuntime => Relation.Parent | Relation.Children,
        _ => Relation.None
    };

    /// <summary>
    /// Cross-checks a command-line mode against process relationships (design decision 7). The result is either the given
    /// mode or unknown, never a stronger mode, so an unreadable command line is never promoted by relationships alone.
    /// A provider without a shared backend has no relationship rule and keeps its mode. For a provider with one: a shared
    /// backend must be started by a same-provider client; a client (interactive or embedded) must not be started by a
    /// same-provider process, which is where a backend sits, nor start same-provider backends or clients of its own.
    /// <paramref name="relations"/> is called at most once, and only with the relationships <see cref="Needs"/> names; a null
    /// result (unreadable) fails a backend to unknown and leaves a client's mode. A backend that the provider documents as a
    /// detached, relaunched daemon (<paramref name="launcherIndependent"/>, an adapter fact about the command line) is not judged
    /// by its launcher: its parent may be an exited client, an updater or a lifecycle command.
    /// </summary>
    public static string CrossCheck(string mode, bool mayShareBackend, Func<Relation, ProcessRelations?> relations, bool launcherIndependent = false)
    {
        var needs = Needs(mode, mayShareBackend, launcherIndependent);
        if (needs == Relation.None)
        {
            return mode;
        }
        static bool Client(string value) => value is AgentModes.InteractiveClient or AgentModes.EmbeddedRuntime;
        var found = relations(needs);
        if (mode == AgentModes.SharedBackend)
        {
            return found?.Parent is { } launcher && Client(launcher.Mode) ? mode : AgentModes.Unknown;
        }
        if (found is null)
        {
            return mode;
        }
        var contradicted = !found.Complete || found.Parent is not null ||
            found.Children.Any(child => child.Mode == AgentModes.SharedBackend || Client(child.Mode));
        return contradicted ? AgentModes.Unknown : mode;
    }

    /// <summary>Capture-time decision from the classified mode. Provider facts (can it share a backend) come from the adapter.</summary>
    public static Outcome Apply(Attachment captured, string? sessionId, string mode, bool mayShareBackend, ProcessIdentity? agentParent = null,
        IReadOnlyList<ProcessIdentity>? agentAncestors = null)
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
                    AgentAncestors = agentAncestors,
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
    public static Attachment Reconcile(Attachment captured, Attachment? previous, string? sessionId, Func<ProcessIdentity, bool> live,
        ProcessIdentity? launchShell = null, OwnedRuntime? runtime = null)
    {
        if (captured.Evidence is { Provisional: true } pending)
        {
            // Proof is the launch record's own process (bell-tower launch), the recorded launching shell of a profile launch, or
            // the runtime record of an owned Codex runtime (below); each must be live with the same creation time and be the
            // hooked agent's direct parent (the owned runtime's manager may sit a few launcher hops higher). A nested
            // client's parent is another shell, a stale record's shell is dead or reused, and neither proves anything.
            var recordedLaunch = previous is { Ownership: "managed", Process: { } owner } && captured.Process == owner && live(owner);
            var profile = previous is not { Process: { } other } || other == captured.Process;
            var shell = profile && launchShell is { } launch && pending.AgentParent == launch && live(launch);
            // A Bell Tower owned Codex runtime: its own record names the manager that started this backend and the shell it
            // serves. The backend's direct parent must be exactly that live manager (same creation time) and the shell live.
            // The host shell comes from the record, never from the inherited variable, which only located the record.
            // A nested agent never reaches the manager through another agent of its provider: Hosts ends the lineage at the first
            // ancestor that is an image of the provider, so the manager is then absent from AgentAncestors.
            if (profile && !recordedLaunch && !shell && runtime is { } owned && owned.Attachment == captured.Id &&
                live(owned.Manager) && live(owned.Shell) && ManagerBehindAgent(captured.Process, pending, owned.Manager))
            {
                return captured with
                {
                    Host = captured.Host with { Shell = owned.Shell, ShellSource = "launch-record" },
                    Evidence = Verified(pending)
                };
            }
            var verified = recordedLaunch || shell;
            return verified ? captured with { Evidence = Verified(pending) } : Shared(captured, sessionId, pending.Mode);
        }
        if (captured.Ownership == "managed" && previous is { Ownership: "managed", Process: { } recorded } &&
            captured.Process is { } agent && recorded != agent && live(recorded))
        {
            // Only a shell proven by this client's own ancestry survives; one named by an inherited variable belongs to the launcher.
            var host = captured.Host.ShellSource == "ancestry" ? captured.Host : new HostTarget("unknown");
            // The live launch presents, or opted out of presenting, on its own shell. A nested client in that same shell fails closed:
            // it keeps the shell for navigation but marks it shared, so it is never granted a title or colors there and its exit resets nothing.
            if (host.Shell is { } nestedShell && previous.Host.Shell == nestedShell)
            {
                host = host with { ShellSource = HostTarget.LaunchShared };
            }
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

    /// <summary>The evidence of a proven capture: no longer provisional and without the inputs that proved it.</summary>
    private static AttachmentEvidence Verified(AttachmentEvidence pending) => pending with
    {
        Provisional = false,
        AgentParent = null,
        AgentAncestors = null
    };

    /// <summary>
    /// True when the manager is one of the hooked agent's nearest <see cref="MaxOwnedDepth"/> ancestors and every hop is
    /// consistent in time: an ancestor never starts after its child, which rejects a reused process identifier at any hop. The
    /// manager must match by process identifier and creation time. Never decided by an image name.
    /// </summary>
    private static bool ManagerBehindAgent(ProcessIdentity? agent, AttachmentEvidence pending, ProcessIdentity manager)
    {
        var chain = pending.AgentAncestors ?? (pending.AgentParent is { } parent ? [parent] : []);
        var child = agent;
        foreach (var hop in chain.Take(MaxOwnedDepth))
        {
            if (hop is null)
            {
                // A malformed list ends the chain: fail closed.
                return false;
            }
            if (child is { } below && hop.StartedUtcTicks > below.StartedUtcTicks)
            {
                return false;
            }
            if (hop == manager)
            {
                return true;
            }
            child = hop;
        }
        return false;
    }

    public static string SharedId(string provider, string sessionId) => provider + "-session-" + ContentHash.Compute(sessionId)[..24];

    /// <summary>The identifier of an unmanaged attachment: its provider and agent process generation.</summary>
    public static string OwnId(string provider, ProcessIdentity process) => $"{provider}-{process.Pid}-{process.StartedUtcTicks}";
}
