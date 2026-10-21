using System.Runtime.InteropServices;

namespace BellTower.Hosts;

public sealed class NativeWindowActivationApi : IWindowActivationApi
{
    private const int RestoreMinimizedWindow = 9;
    public bool IsMinimized(IntPtr window) => IsIconic(window);
    public void Restore(IntPtr window) => ShowWindow(window, RestoreMinimizedWindow);
    public bool SetForeground(IntPtr window) => GetForegroundWindow() == window || SetForegroundWindow(window);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
