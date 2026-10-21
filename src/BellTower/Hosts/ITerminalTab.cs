namespace BellTower.Hosts;

public interface ITerminalTab
{
    string Name { get; }
    bool Select();
}
