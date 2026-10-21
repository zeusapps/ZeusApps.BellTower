namespace BellTower.Hosts;

public interface ISessionConsole : IDisposable
{
    string ReadTitle();
    bool SetTitle(string title);
    bool Write(string text);
}
