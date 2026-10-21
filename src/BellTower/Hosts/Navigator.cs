using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class Navigator
{
    private readonly IReadOnlyDictionary<string, ITerminalNavigator> hosts;

    public Navigator(DataPaths paths) : this([new VsCodeNavigator(paths), new WindowsTerminalNavigator(paths)]) { }

    public Navigator(IEnumerable<ITerminalNavigator> hosts) => this.hosts = hosts.ToDictionary(host => host.HostKind, StringComparer.Ordinal);

    public FocusResult Focus(Attachment? attachment) => FocusAsync(attachment).GetAwaiter().GetResult();

    public Task<FocusResult> FocusAsync(Attachment? attachment)
    {
        if (attachment is null || attachment.Process is not { } registered || ProcessProbe.Identify(registered.Pid) != registered)
        {
            return Task.FromResult(new FocusResult(false, "unavailable", "The session process cannot be verified."));
        }

        return hosts.TryGetValue(attachment.Host.Kind, out var host)
            ? host.FocusAsync(attachment)
            : Task.FromResult(new FocusResult(false, "unavailable", "No verified terminal destination is registered."));
    }

    public string Precision(Attachment? attachment) => attachment is not null && hosts.TryGetValue(attachment.Host.Kind, out var host)
        ? host.Precision(attachment) : "unavailable";
}
