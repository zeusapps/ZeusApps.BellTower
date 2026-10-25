using System.Runtime.InteropServices;
using System.Text;

namespace BellTower.Hosts;

public sealed class NativeWindowInspector : IWindowInspector
{
    public WindowFacts? Inspect(long handle) => Describe(new IntPtr(handle));

    public WindowFacts? Foreground() => Describe(GetForegroundWindow());

    private static WindowFacts? Describe(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindow(window))
        {
            return null;
        }

        GetWindowThreadProcessId(window, out var owner);
        var name = new StringBuilder(256);
        return GetClassName(window, name, name.Capacity) > 0 && owner != 0
            ? new(window.ToInt64(), (int)owner, name.ToString(), IsWindowVisible(window)) : null;
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
}
