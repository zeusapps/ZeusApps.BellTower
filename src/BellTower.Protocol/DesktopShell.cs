using System.Runtime.InteropServices;

namespace BellTower.Protocol;

/// <summary>
/// Finds and verifies the desktop shell a hook starts the tray from (design decision 10). The shell is the process that owns the
/// desktop's shell window. It counts only when its image is the system shell, it belongs to this user and this sign-in session,
/// and it is the same process after it was opened. Anything else fails closed with a bounded reason and nothing starts.
/// </summary>
internal static class DesktopShell
{
    private const uint ProcessCreateProcess = 0x0080;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenUser = 1;

    /// <summary>What was read about a candidate shell. Null values mean the read failed.</summary>
    internal readonly record struct Facts(int ShellPid, string? Image, string WindowsDirectory, bool? SameUser, int? ShellSession, int OwnSession, int OpenedPid, int? OpenError = null);

    /// <summary>An opened, verified shell process handle (closed by <see cref="Close"/>) or the reason there is none.</summary>
    internal readonly record struct Opened(IntPtr Process, string? Problem)
    {
        public void Close()
        {
            if (Process != IntPtr.Zero)
            {
                CloseHandle(Process);
            }
        }
    }

    /// <summary>Pure verification of what was read. Null when the shell is acceptable.</summary>
    internal static string? Problem(Facts facts)
    {
        if (facts.ShellPid <= 0)
        {
            return "shell-none";
        }
        if (facts.OpenError is { } error)
        {
            // An access failure is not an unverified image: say what Windows answered.
            return "shell-open:" + error.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (facts.OpenedPid != facts.ShellPid)
        {
            return "shell-changed";
        }
        var expected = Path.Combine(facts.WindowsDirectory, "explorer.exe");
        if (facts.Image is null || !string.Equals(facts.Image, expected, StringComparison.OrdinalIgnoreCase))
        {
            return "shell-image-unverified";
        }
        if (facts.SameUser != true)
        {
            return "shell-other-user";
        }
        if (facts.ShellSession is null || facts.ShellSession != facts.OwnSession)
        {
            return "shell-other-session";
        }
        return null;
    }

    public static Opened Open() => Open(Read);

    internal static Opened Open(Func<(IntPtr Process, Facts Facts)> read)
    {
        var (process, facts) = read();
        var problem = Problem(facts);
        if (problem is not null)
        {
            if (process != IntPtr.Zero)
            {
                CloseHandle(process);
            }
            return new(IntPtr.Zero, problem);
        }
        return new(process, null);
    }

    private static (IntPtr Process, Facts Facts) Read()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        _ = GetWindowThreadProcessId(GetShellWindow(), out var shellPid);
        var own = ProcessIdToSessionId((uint)Environment.ProcessId, out var ownSession) ? (int)ownSession : -1;
        if (shellPid == 0)
        {
            return (IntPtr.Zero, new(0, null, windows, null, null, own, 0));
        }
        var process = OpenProcess(ProcessCreateProcess | ProcessQueryLimitedInformation, false, shellPid);
        if (process == IntPtr.Zero)
        {
            return (IntPtr.Zero, new((int)shellPid, null, windows, null, null, own, (int)shellPid, Marshal.GetLastWin32Error()));
        }
        _ = GetWindowThreadProcessId(GetShellWindow(), out var again);
        var session = ProcessIdToSessionId(shellPid, out var shellSession) ? (int)shellSession : (int?)null;
        return (process, new((int)shellPid, ImagePath(process), windows, SameUser(process), session, own, (int)again));
    }

    private static string? ImagePath(IntPtr process)
    {
        const int capacity = 1024;
        var buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
        try
        {
            var size = (uint)capacity;
            return QueryFullProcessImageNameW(process, 0, buffer, ref size) ? Marshal.PtrToStringUni(buffer, (int)size) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool? SameUser(IntPtr shell)
    {
        var shellToken = IntPtr.Zero;
        var ownToken = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(shell, TokenQuery, out shellToken) || !OpenProcessToken(GetCurrentProcess(), TokenQuery, out ownToken))
            {
                return null;
            }
            var first = UserSid(shellToken);
            var second = UserSid(ownToken);
            return first is null || second is null ? null : first.AsSpan().SequenceEqual(second);
        }
        finally
        {
            if (shellToken != IntPtr.Zero)
            {
                CloseHandle(shellToken);
            }
            if (ownToken != IntPtr.Zero)
            {
                CloseHandle(ownToken);
            }
        }
    }

    private static byte[]? UserSid(IntPtr token)
    {
        GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out var needed);
        if (needed == 0 || needed > 4096)
        {
            return null;
        }
        var buffer = Marshal.AllocHGlobal((int)needed);
        try
        {
            if (!GetTokenInformation(token, TokenUser, buffer, needed, out _))
            {
                return null;
            }
            // TOKEN_USER begins with a SID_AND_ATTRIBUTES whose first member is the pointer to the SID.
            var sid = Marshal.ReadIntPtr(buffer);
            var length = (int)GetLengthSid(sid);
            var bytes = new byte[length];
            Marshal.Copy(sid, bytes, 0, length);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, IntPtr name, ref uint size);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int information, IntPtr buffer, uint length, out uint returned);

    [DllImport("advapi32.dll")]
    private static extern uint GetLengthSid(IntPtr sid);
}
