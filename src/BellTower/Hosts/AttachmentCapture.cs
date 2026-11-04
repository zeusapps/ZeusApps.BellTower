using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Hosts;

public static class AttachmentCapture
{
    /// <summary>
    /// Captures the hook's process context and applies the shared native adoption policy. The only extra work beyond
    /// ancestry is one bounded command-line read of the agent ancestor; no host discovery or runtime call happens here.
    /// </summary>
    public static NativeAdoption.Outcome Resolve(string provider, string directory, string? sessionId)
    {
        var attachment = Capture(provider, directory, out var agent, out var parent);
        var arguments = agent.Pid > 0 && ProcessProbe.CommandLine(agent.Pid) is { } commandLine ? AgentModeClassifier.Arguments(commandLine) : null;
        var outcome = NativeAdoption.Apply(attachment, sessionId, AgentModeClassifier.Classify(provider, agent.Pid > 0, arguments),
            AgentModeClassifier.MayShareBackend(provider), parent);
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

    public static Attachment Capture(string provider, string directory) => Capture(provider, directory, out _, out _);

    private static Attachment Capture(string provider, string directory, out (int Pid, string Name) agent, out ProcessIdentity? parent)
    {
        var ancestors = ProcessProbe.Ancestors(Environment.ProcessId);
        var found = ancestors.FirstOrDefault(a => a.Name.Equals(provider + ".exe", StringComparison.OrdinalIgnoreCase));
        agent = found;
        var agentIndex = found.Pid > 0 ? ancestors.FindIndex(a => a.Pid == found.Pid) : -1;
        // The direct parent (the next ancestor) is the only link that can prove a profile launch from its recorded shell.
        parent = agentIndex >= 0 && agentIndex + 1 < ancestors.Count ? ProcessProbe.Identify(ancestors[agentIndex + 1].Pid) : null;
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
            window = ProcessProbe.Ancestors(shell.Pid).FirstOrDefault(a => a.Name.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase));
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
