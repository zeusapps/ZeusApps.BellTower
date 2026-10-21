using BellTower.Core;

namespace BellTower.Hosts;

public interface IConsoleTitleWriter
{
    bool Write(ProcessIdentity shell, ProcessIdentity agent, string title, string? background = null);
}
