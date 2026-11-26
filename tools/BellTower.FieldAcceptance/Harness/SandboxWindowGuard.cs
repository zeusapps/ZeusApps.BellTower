using System.Runtime.InteropServices;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// Keeps a sandbox installation's tray windows off the maintainer's desktop. The product has no supported setting for this: <c>--silent</c> and the
/// <c>muted</c> configuration silence audio only, and the reminder settings stop reminders, but the notification strip (the attention chips, including the
/// "possibly waiting" chip of a pending wait) is always shown by the tray. The guard therefore hides, a few times a second, every visible top-level window owned
/// by a process whose image lies under this instance's install directory, and nothing else (the user's own trays and every other process are never
/// touched). A window can be visible for up to one poll interval before it is hidden; <see cref="HiddenCount"/> reports how many shows it had to undo.
/// </summary>
public sealed class SandboxWindowGuard : IDisposable
{
    private const int HideCommand = 0;
    private readonly string installRoot;
    private readonly Func<int, string?> imageOf;
    private readonly CancellationTokenSource stop = new();
    private readonly Thread thread;
    private int hiddenCount;

    /// <param name="imageOf">Looks up the live image of a process id (a seam for tests); the default reads the real process.</param>
    public SandboxWindowGuard(string installRoot, Func<int, string?>? imageOf = null)
    {
        this.imageOf = imageOf ?? LiveImage;
        this.installRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot)) + Path.DirectorySeparatorChar;
        thread = new Thread(Loop) { IsBackground = true, Name = "sandbox-window-guard" };
        thread.Start();
    }

    /// <summary>How many times a visible sandbox window was hidden.</summary>
    public int HiddenCount => Volatile.Read(ref hiddenCount);

    /// <summary>
    /// Sandbox-owned windows that stayed visible across two reads 400 ms apart (what the check requires to be none): a window shown a moment before a read
    /// is hidden by the next poll, so only one that survives a full poll interval counts.
    /// </summary>
    public IReadOnlyList<string> VisibleNow()
    {
        var first = Snapshot();
        Thread.Sleep(400);
        return [.. Snapshot().Where(first.Contains)];
    }

    private List<string> Snapshot()
    {
        var found = new List<string>();
        EnumWindows((window, parameter) =>
        {
            if (IsWindowVisible(window) && Owned(window, out var pid))
            {
                found.Add("window " + window.ToInt64() + " of pid " + pid);
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private void Loop()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                EnumWindows((window, parameter) =>
                {
                    if (IsWindowVisible(window) && Owned(window, out _))
                    {
                        ShowWindow(window, HideCommand);
                        Interlocked.Increment(ref hiddenCount);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // A window or process vanished mid-enumeration; the next pass sees the rest.
            }
            stop.Token.WaitHandle.WaitOne(100);
        }
    }

    // Ownership is decided from the live image of the window's process on every poll, never cached: a sandbox tray that exited and whose process id a user
    // process now holds must not be touched (ANALYSIS-280). A process that cannot be read, or that has exited, owns nothing.
    private bool Owned(IntPtr window, out int pid)
    {
        GetWindowThreadProcessId(window, out var id);
        pid = (int)id;
        return IsSandboxImage(imageOf(pid), installRoot);
    }

    /// <summary>True only for an image that lies under the sandbox install directory.</summary>
    public static bool IsSandboxImage(string? image, string installRoot) =>
        image is not null && image.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>The full image path of a live process, or null when it cannot be opened (exited, protected).</summary>
    public static string? LiveImage(int pid)
    {
        var handle = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (handle == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            var text = new System.Text.StringBuilder(1024);
            var size = text.Capacity;
            return QueryFullProcessImageName(handle, 0, text, ref size) ? text.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        thread.Join(2000);
        stop.Dispose();
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, System.Text.StringBuilder name, ref int size);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
