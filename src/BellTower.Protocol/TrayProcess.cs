using System.Globalization;
using System.Runtime.InteropServices;

namespace BellTower.Protocol;

/// <summary>
/// Direct process creation for the hook-started tray. The Windows APIs are called directly (no reflection, NativeAOT safe)
/// because the managed process class cannot say "inherit nothing, leave the provider's job, use the user's environment".
/// </summary>
internal static class TrayProcess
{
    private const uint TokenQuery = 0x0008;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenImpersonate = 0x0004;
    private const int TokenStatistics = 10;
    private const int TokenElevationType = 18;
    private const int TokenLinkedToken = 19;
    private const int TokenElevation = 20;
    private const int TokenTypePrimary = 1;
    private const int SecurityImpersonation = 2;
    private const uint TokenAssignPrimary = 0x0001;
    private const uint CreateNewProcessGroup = 0x00000200;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private static readonly IntPtr ProcThreadAttributeParentProcess = (IntPtr)0x00020000;
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint StartfUseShowWindow = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct LinkedToken { public IntPtr Token; }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public uint Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public ushort ShowWindow, Reserved2Size;
        public IntPtr Reserved2, StandardInput, StandardOutput, StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo Startup;
        public IntPtr Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessExW(string applicationName, IntPtr commandLine, IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory,
        ref StartupInfoEx startupInfo, out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token, int information, IntPtr buffer, uint length, out uint returned);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("secur32.dll")]
    private static extern uint LsaGetLogonSessionData(ref long logonId, out IntPtr data);

    [DllImport("secur32.dll")]
    private static extern uint LsaFreeReturnBuffer(IntPtr buffer);

    public static string? SignInToken()
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
        {
            return null;
        }
        var token = IntPtr.Zero;
        var linked = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token))
            {
                return null;
            }
            var elevationType = ReadTokenElevationType(token);
            var tokenElevated = ReadTokenIsElevated(token);
            if (elevationType is null || tokenElevated is null)
            {
                return null;
            }
            var currentIdentity = TokenIdentity(token);
            if (currentIdentity is null)
            {
                return null;
            }

            string? linkedIdentity = null;
            if (elevationType == 2 && tokenElevated == true)
            {
                if (!TryGetLinkedToken(token, out linked) || linked == IntPtr.Zero || ReadTokenElevationType(linked) != 3 ||
                    ReadTokenIsElevated(linked) != false || (linkedIdentity = TokenIdentity(linked)) is null)
                {
                    return null;
                }
            }
            return SelectSignInIdentity(elevationType.Value, tokenElevated.Value, currentIdentity, linkedIdentity);
        }
        finally
        {
            if (linked != IntPtr.Zero)
            {
                CloseHandle(linked);
            }
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
        }
    }

    internal static string? SelectSignInIdentity(int? elevationType, bool? elevated, string currentIdentity, string? linkedIdentity) => (elevationType, elevated) switch
    {
        (1, false) or (1, true) or (3, false) => currentIdentity,
        (2, true) => linkedIdentity,
        _ => null
    };

    private static string? TokenIdentity(IntPtr token)
    {
        var buffer = Marshal.AllocHGlobal(256);
        try
        {
            if (!GetTokenInformation(token, TokenStatistics, buffer, 256, out _))
            {
                return null;
            }
            // TOKEN_STATISTICS: TokenId (LUID, offset 0), AuthenticationId (LUID, offset 8) = the logon session.
            var logon = Marshal.ReadInt64(buffer, 8);
            var text = "luid:" + logon.ToString("x", CultureInfo.InvariantCulture);
            // A logon session id restarts per boot, so its logon time makes a reused id from another sign-in stale.
            if (LsaGetLogonSessionData(ref logon, out var data) == 0 && data != IntPtr.Zero)
            {
                try
                {
                    // SECURITY_LOGON_SESSION_DATA (x64): Size 0, LogonId 4, strings at 16/32/48, LogonType 64, Session 68, Sid 72, LogonTime 80.
                    if (Marshal.ReadInt32(data, 0) >= 88)
                    {
                        text += "/" + Marshal.ReadInt64(data, 80).ToString(CultureInfo.InvariantCulture);
                    }
                }
                finally
                {
                    LsaFreeReturnBuffer(data);
                }
            }
            return text;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>The route recorded when the tray was created as a child of the desktop shell.</summary>
    public const string RouteShell = "started-via-shell";

    /// <summary>
    /// Starts the launcher as a child of the verified desktop shell (design decision 10). PROC_THREAD_ATTRIBUTE_PARENT_PROCESS
    /// makes the child inherit the shell's jobs and token, never the provider's. No handle of this process is inherited, there is
    /// no window, the launcher folder is the working directory, and the environment block is built for the shell's token instead
    /// of copied from the hook. The child is created suspended and resumed only when its token is not elevated while the shell's
    /// is not. An unverifiable shell starts nothing. There is no job check: on Windows build 26300 every process is in the session
    /// job, so "outside every job" cannot be satisfied.
    /// </summary>
    public static (HookStartOutcome Outcome, string Detail) Start(string launcher, string root) => Start(launcher, root, DesktopShell.Open);

    internal static (HookStartOutcome Outcome, string Detail) Start(string launcher, string root, Func<DesktopShell.Opened> openShell)
    {
        var shell = openShell();
        var token = IntPtr.Zero;
        var environment = IntPtr.Zero;
        var command = IntPtr.Zero;
        var attributes = IntPtr.Zero;
        var parentValue = IntPtr.Zero;
        var marked = IntPtr.Zero;
        try
        {
            if (shell.Problem is not null)
            {
                return (HookStartOutcome.Failed, shell.Problem);
            }
            if (!OpenProcessToken(shell.Process, TokenQuery | TokenDuplicate | TokenImpersonate, out token))
            {
                return (HookStartOutcome.Failed, "shell-token:" + Marshal.GetLastPInvokeError());
            }
            var shellElevated = ReadTokenIsElevated(token);
            if (shellElevated is null)
            {
                return (HookStartOutcome.Failed, "shell-token-elevation-query:" + Marshal.GetLastPInvokeError());
            }
            if (!CreateEnvironmentBlock(out environment, token, false))
            {
                return (HookStartOutcome.Failed, "environment-block:" + Marshal.GetLastPInvokeError());
            }
            var line = Quote(launcher) + " tray --data-dir " + Quote(root);
            marked = WithVariable(environment, HookTrayStart.HookStartVariable, "1");
            command = Marshal.StringToHGlobalUni(line);
            var directory = Path.GetDirectoryName(launcher)!;

            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size))
            {
                Marshal.FreeHGlobal(attributes);
                attributes = IntPtr.Zero;
                return (HookStartOutcome.Failed, "attribute-list:" + Marshal.GetLastPInvokeError());
            }
            parentValue = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(parentValue, shell.Process);
            if (!UpdateProcThreadAttribute(attributes, 0, ProcThreadAttributeParentProcess, parentValue, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            {
                return (HookStartOutcome.Failed, "attribute-parent:" + Marshal.GetLastPInvokeError());
            }
            var info = new StartupInfoEx { Attributes = attributes };
            info.Startup.Flags = StartfUseShowWindow;
            info.Startup.ShowWindow = 0;
            info.Startup.Size = (uint)Marshal.SizeOf<StartupInfoEx>();
            var flags = CreateNewProcessGroup | CreateUnicodeEnvironment | CreateNoWindow | ExtendedStartupInfoPresent | CreateSuspended;
            if (!CreateProcessExW(launcher, command, IntPtr.Zero, IntPtr.Zero, false, flags, marked, directory, ref info, out var process))
            {
                return (HookStartOutcome.Failed, "create-process:" + Marshal.GetLastPInvokeError());
            }
            try
            {
                if (!OpenProcessToken(process.Process, TokenQuery, out var childToken))
                {
                    return StopSuspended(process, "child-token:" + Marshal.GetLastPInvokeError());
                }
                bool? childElevated;
                try
                {
                    childElevated = ReadTokenIsElevated(childToken);
                }
                finally
                {
                    CloseHandle(childToken);
                }
                if (childElevated is null || childElevated == true && shellElevated == false)
                {
                    return StopSuspended(process, "child-elevated");
                }
                var previousSuspendCount = ResumeThread(process.Thread);
                if (previousSuspendCount == uint.MaxValue)
                {
                    return StopSuspended(process, "child-resume:" + Marshal.GetLastPInvokeError());
                }
                if (previousSuspendCount != 1)
                {
                    return StopSuspended(process, "child-resume-count:" + previousSuspendCount.ToString(CultureInfo.InvariantCulture));
                }
                return (HookStartOutcome.Started, RouteShell);
            }
            finally
            {
                CloseHandle(process.Process);
                CloseHandle(process.Thread);
            }
        }
        finally
        {
            if (attributes != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributes);
                Marshal.FreeHGlobal(attributes);
            }
            if (parentValue != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(parentValue);
            }
            if (command != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(command);
            }
            if (marked != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(marked);
            }
            if (environment != IntPtr.Zero)
            {
                DestroyEnvironmentBlock(environment);
            }
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
            shell.Close();
        }
    }

    /// <summary>
    /// A copy of an environment block with one variable added, sorted the way Windows requires (case-insensitive by name) and ended by two
    /// terminators. The caller frees the copy with <see cref="Marshal.FreeHGlobal"/>.
    /// </summary>
    internal static IntPtr WithVariable(IntPtr block, string name, string value)
    {
        var entries = new List<string>();
        for (var offset = 0; ;)
        {
            var entry = Marshal.PtrToStringUni(block + offset * sizeof(char)) ?? "";
            if (entry.Length == 0)
            {
                break;
            }
            offset += entry.Length + 1;
            if (!entry.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(entry);
            }
        }
        entries.Add(name + "=" + value);
        entries.Sort((left, right) => string.Compare(left[..Math.Max(1, left.IndexOf('=', 1))], right[..Math.Max(1, right.IndexOf('=', 1))], StringComparison.OrdinalIgnoreCase));
        var text = string.Concat(entries.Select(entry => entry + "\0")) + "\0";
        return Marshal.StringToHGlobalUni(text);
    }

    private static (HookStartOutcome Outcome, string Detail) StopSuspended(ProcessInformation process, string reason)
    {
        if (!TerminateProcess(process.Process, 1))
        {
            return (HookStartOutcome.Failed, reason + ";terminate:" + Marshal.GetLastPInvokeError());
        }
        return (HookStartOutcome.Failed, reason);
    }

    private static int? ReadTokenElevationType(IntPtr token)
    {
        var buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            return GetTokenInformation(token, TokenElevationType, buffer, sizeof(int), out _) ? Marshal.ReadInt32(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool? ReadTokenIsElevated(IntPtr token)
    {
        var buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            if (!GetTokenInformation(token, TokenElevation, buffer, sizeof(int), out _))
            {
                return null;
            }
            return Marshal.ReadInt32(buffer) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool TryGetLinkedToken(IntPtr token, out IntPtr linked)
    {
        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<LinkedToken>());
        try
        {
            if (!GetTokenInformation(token, TokenLinkedToken, buffer, (uint)Marshal.SizeOf<LinkedToken>(), out _))
            {
                linked = IntPtr.Zero;
                return false;
            }
            linked = Marshal.PtrToStructure<LinkedToken>(buffer).Token;
            return linked != IntPtr.Zero;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string Quote(string value)
    {
        var trailing = value.Length - value.TrimEnd('\\').Length;
        return "\"" + value + new string('\\', trailing) + "\"";
    }
}
