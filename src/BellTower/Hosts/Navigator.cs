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
        // A shared-backend conversation never has a terminal of its own; say so and name the supported routes instead of the
        // generic process message, for every click and both hosts.
        if (NavigationReasons.ForSharedConversation(attachment) is { } shared)
        {
            return Task.FromResult(new FocusResult(false, "unavailable", shared));
        }
        if (attachment is null || attachment.EndedAt is not null || attachment.Health == "unavailable" ||
            attachment.Process is not { } registered || ProcessProbe.Identify(registered.Pid) != registered)
        {
            return Task.FromResult(new FocusResult(false, "unavailable", NavigationReasons.ProcessUnverified));
        }

        return hosts.TryGetValue(attachment.Host.Kind, out var host)
            ? host.FocusAsync(attachment)
            : Task.FromResult(new FocusResult(false, "unavailable", "No verified terminal destination is registered."));
    }

    public string Precision(Attachment? attachment) => attachment is { EndedAt: null } && attachment.Health != "unavailable" &&
        attachment.Process is { } process && ProcessProbe.Identify(process.Pid) == process && hosts.TryGetValue(attachment.Host.Kind, out var host)
        ? host.Precision(attachment) : "unavailable";
}
