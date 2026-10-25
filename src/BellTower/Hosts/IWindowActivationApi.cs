namespace BellTower.Hosts;

public interface IWindowActivationApi
{
    bool IsMinimized(IntPtr window);
    void Restore(IntPtr window);
    bool SetForeground(IntPtr window);
    bool IsForeground(IntPtr window);
}
