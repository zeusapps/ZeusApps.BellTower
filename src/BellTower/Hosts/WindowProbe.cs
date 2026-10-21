using System.Runtime.InteropServices;

namespace BellTower.Hosts;

public static class WindowProbe
{
    public static long SingleVisibleWindow(int pid)
    {
        var windows = new List<long>();
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var owner);
            if (owner == pid && IsWindowVisible(handle))
            {
                windows.Add(handle.ToInt64());
            }

            return true;
        }, IntPtr.Zero);
        return windows.Count == 1 ? windows[0] : 0;
    }

    private delegate bool Callback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);
}
