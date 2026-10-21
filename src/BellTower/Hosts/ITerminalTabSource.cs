namespace BellTower.Hosts;

public interface ITerminalTabSource
{
    IReadOnlyList<ITerminalTab> ReadTabs(long window);
}
