using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public static class AttachmentCapture
{
    public static Attachment Capture(string provider, string directory)
    {
        var ancestors = ProcessProbe.Ancestors(Environment.ProcessId);
        var agent = ancestors.FirstOrDefault(a => a.Name.Equals(provider + ".exe", StringComparison.OrdinalIgnoreCase));
        var agentIndex = agent.Pid > 0 ? ancestors.FindIndex(a => a.Pid == agent.Pid) : -1;
        var shell = ancestors.Skip(agentIndex >= 0 ? agentIndex + 1 : 0).FirstOrDefault(ancestor =>
            ancestor.Name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
            ancestor.Name.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) ||
            ancestor.Name.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase));
        if (int.TryParse(Environment.GetEnvironmentVariable("BELLTOWER_SHELL_PID"), out var configuredShell))
        {
            shell = (configuredShell, "shell");
        }

        var kind = Environment.GetEnvironmentVariable("TERM_PROGRAM") == "vscode" ? "vscode" :
            Environment.GetEnvironmentVariable("WT_SESSION") is not null ? "windows-terminal" : "unknown";
        var window = ancestors.FirstOrDefault(a => a.Name.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase));
        if (window.Pid == 0 && shell.Pid > 0)
        {
            window = ProcessProbe.Ancestors(shell.Pid).FirstOrDefault(a => a.Name.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase));
        }
        var agentIdentity = ProcessProbe.Identify(agent.Pid);
        var id = Environment.GetEnvironmentVariable("BELLTOWER_ATTACHMENT") ??
            (agentIdentity is { } process ? $"{provider}-{process.Pid}-{process.StartedUtcTicks}" : Guid.NewGuid().ToString("N"));
        long handle = 0;
        if (window.Pid > 0)
        {
            handle = WindowProbe.SingleVisibleWindow(window.Pid);
        }

        var host = new HostTarget(kind, ProcessProbe.Identify(shell.Pid), ProcessProbe.Identify(window.Pid),
            handle, Environment.GetEnvironmentVariable("WT_SESSION"));
        return new(id, provider, agentIdentity, host, directory, DateTimeOffset.UtcNow,
            ManagedTitle: Environment.GetEnvironmentVariable("BELLTOWER_MANAGED_TITLE") == "1",
            ManagedBackground: Environment.GetEnvironmentVariable("BELLTOWER_MANAGED_BACKGROUND") == "1");
    }
}
