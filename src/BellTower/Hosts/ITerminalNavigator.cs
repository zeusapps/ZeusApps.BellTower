using BellTower.Core;

namespace BellTower.Hosts;

public interface ITerminalNavigator
{
    string HostKind { get; }
    string Precision(Attachment attachment);
    Task<FocusResult> FocusAsync(Attachment attachment);
}
