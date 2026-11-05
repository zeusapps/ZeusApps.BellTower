using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Hosts;

public static class AttachmentCapture
{
    /// <summary>
    /// Captures the hook's process context and applies the shared native adoption policy. The only extra work beyond
    /// ancestry is one bounded command-line read of the agent ancestor, plus the relationship reads Core asks for from the same
    /// process table; no host discovery or runtime call happens here.
    /// </summary>
    public static NativeAdoption.Outcome Resolve(string provider, string directory, string? sessionId) =>
        Resolve(provider, directory, sessionId, ProcessTable.Take());

    /// <summary>The same decision over a process table the caller already took, so one hook reads the table once.</summary>
    public static NativeAdoption.Outcome Resolve(string provider, string directory, string? sessionId, ProcessTable table)
    {
        var attachment = Capture(provider, directory, table, out var agent, out var parent, out var lineage, out var parentIsAgent);
        var arguments = agent.Pid > 0 && ProcessProbe.CommandLine(agent.Pid) is { } commandLine ? AgentModeClassifier.Arguments(commandLine) : null;
        var mode = CrossChecked(provider, AgentModeClassifier.Classify(provider, agent.Pid > 0, arguments), attachment.Process, table);
        var outcome = NativeAdoption.Apply(attachment, sessionId, mode, AgentModeClassifier.MayShareBackend(provider), parent, lineage, parentIsAgent);
        if (outcome.Attachment.Evidence is { Candidate: { } candidate } evidence)
        {
            // The identity text of a provisional launch rides with its candidate, never in the shared-shaped envelope fields.
            static string? Text(string? value) => value is { Length: > 200 } ? value[..200] : value;
            outcome = outcome with
            {
                Attachment = outcome.Attachment with
                {
                    Evidence = evidence with
                    {
                        Candidate = candidate with
                        {
                            Name = Text(Environment.GetEnvironmentVariable("BELLTOWER_NAME")),
                            Change = Text(Environment.GetEnvironmentVariable("BELLTOWER_CHANGE"))
                        }
                    }
                }
            };
        }
        return outcome;
    }

    // Reads, from the hook's one process table, only the relationships Core says the mode needs (one bounded command-line read
    // per related process). A related process whose command line cannot be read classifies as unknown, which never satisfies a rule.
    private static string CrossChecked(string provider, string mode, ProcessIdentity? agent, ProcessTable table)
    {
        if (agent is null)
        {
            return mode;
        }
        NativeAdoption.RelatedProcess Related(ProcessIdentity identity) => new(identity, AgentModeClassifier.Classify(provider, true,
            ProcessProbe.CommandLine(identity.Pid) is { } text ? AgentModeClassifier.Arguments(text) : null));
        return NativeAdoption.CrossCheck(mode, AgentModeClassifier.MayShareBackend(provider), needs =>
            table.Neighbors(agent, provider + ".exe", needs.HasFlag(NativeAdoption.Relation.Children)) is { } neighbors
                ? new NativeAdoption.ProcessRelations(neighbors.Parent is { } parent ? Related(parent) : null,
                    neighbors.Children.Select(Related).ToList(), neighbors.Complete) : null);
    }

    public static Attachment Capture(string provider, string directory) => Capture(provider, directory, ProcessTable.Take(), out _, out _, out _, out _);

    private static Attachment Capture(string provider, string directory, ProcessTable table, out (int Pid, string Name) agent, out ProcessIdentity? parent,
        out IReadOnlyList<ProcessIdentity> lineage, out bool parentIsAgent)
    {
        var ancestors = table.Ancestors(Environment.ProcessId);
        var found = ancestors.FirstOrDefault(a => a.Name.Equals(provider + ".exe", StringComparison.OrdinalIgnoreCase));
        agent = found;
        var agentIndex = found.Pid > 0 ? ancestors.FindIndex(a => a.Pid == found.Pid) : -1;
        // The nearest few ancestors with their creation times, for an owned runtime whose manager sits behind a launcher; Core
        // checks every hop. The walk stops at the first ancestor that cannot be identified.
        var chain = new List<ProcessIdentity>();
        foreach (var hop in agentIndex >= 0 ? ancestors.Skip(agentIndex + 1).Take(NativeAdoption.MaxOwnedDepth) : [])
        {
            if (ProcessProbe.Identify(hop.Pid) is not { } identified)
            {
                break;
            }
            chain.Add(identified);
        }
        lineage = chain;
        // Only ever used to reject the owned-runtime proof: the direct parent is itself an image of this provider.
        parentIsAgent = agentIndex >= 0 && agentIndex + 1 < ancestors.Count &&
            ancestors[agentIndex + 1].Name.Equals(provider + ".exe", StringComparison.OrdinalIgnoreCase);
        // The direct parent (the first hop) is the only link that can prove a profile launch from its recorded shell.
        parent = chain.Count > 0 ? chain[0] : null;
        var shell = ancestors.Skip(agentIndex >= 0 ? agentIndex + 1 : 0).FirstOrDefault(ancestor =>
            ancestor.Name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
            ancestor.Name.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) ||
            ancestor.Name.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase));
        var shellSource = shell.Pid > 0 ? "ancestry" : null;
        if (int.TryParse(Environment.GetEnvironmentVariable("BELLTOWER_SHELL_PID"), out var configuredShell))
        {
            if (configuredShell != shell.Pid)
            {
                shellSource = "launch-env";
            }
            shell = (configuredShell, "shell");
        }

        var kind = Environment.GetEnvironmentVariable("TERM_PROGRAM") == "vscode" ? "vscode" :
            Environment.GetEnvironmentVariable("WT_SESSION") is not null ? "windows-terminal" : "unknown";
        var window = ancestors.FirstOrDefault(a => a.Name.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase));
        if (window.Pid == 0 && shell.Pid > 0)
        {
            window = table.Ancestors(shell.Pid).FirstOrDefault(a => a.Name.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase));
        }
        var agentIdentity = ProcessProbe.Identify(found.Pid);
        var id = Environment.GetEnvironmentVariable("BELLTOWER_ATTACHMENT") ??
            (agentIdentity is { } process ? NativeAdoption.OwnId(provider, process) : Guid.NewGuid().ToString("N"));
        long handle = 0;
        if (window.Pid > 0)
        {
            handle = WindowProbe.SingleVisibleWindow(window.Pid);
        }

        var host = new HostTarget(kind, ProcessProbe.Identify(shell.Pid), ProcessProbe.Identify(window.Pid),
            handle, Environment.GetEnvironmentVariable("WT_SESSION"))
        {
            ShellSource = shellSource
        };
        return new Attachment(id, provider, agentIdentity, host, directory, DateTimeOffset.UtcNow,
            ManagedTitle: Environment.GetEnvironmentVariable("BELLTOWER_MANAGED_TITLE") == "1",
            ManagedBackground: Environment.GetEnvironmentVariable("BELLTOWER_MANAGED_BACKGROUND") == "1")
        {
            Ownership = Environment.GetEnvironmentVariable("BELLTOWER_ATTACHMENT") is not null ? "managed" : "observed"
        };
    }
}
