using System.Globalization;
using System.Runtime.InteropServices;

namespace BellTower.Protocol;

/// <summary>
/// Direct process creation for the hook-started tray. The Windows APIs are called directly (no reflection, NativeAOT safe)
/// because the managed process class cannot say "inherit nothing, leave the provider's job, use the user's environment".
/// </summary>
internal static class TrayProcess
{
    internal enum LaunchTokenChoice { Refuse, Current, LinkedLimited }

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
    private const uint CreateBreakawayFromJob = 0x01000000;
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint StartfUseShowWindow = 0x00000001;
    private const int ErrorAccessDenied = 5;

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string applicationName, IntPtr commandLine, IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory,
        ref StartupInfo startupInfo, out ProcessInformation processInformation);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessWithTokenW(IntPtr token, uint logonFlags, string applicationName, IntPtr commandLine,
        uint creationFlags, IntPtr environment, string currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(IntPtr existingToken, uint desiredAccess, IntPtr tokenAttributes, int impersonationLevel,
        int tokenType, out IntPtr newToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool result);

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

    internal static LaunchTokenChoice SelectLaunchToken(int? elevationType, bool? elevated) => (elevationType, elevated) switch
    {
        (1, false) or (3, false) => LaunchTokenChoice.Current,
        (2, true) => LaunchTokenChoice.LinkedLimited,
        _ => LaunchTokenChoice.Refuse
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

    /// <summary>
    /// Starts the launcher suspended with no inherited handles, then verifies it is outside every job before resuming it. Uses no
    /// window, the launcher's folder as the working directory and a fresh environment block rather than the hook's. Unsafe token,
    /// job or resume results terminate the owned suspended process and leave the capture queued.
    /// </summary>
    public static (HookStartOutcome Outcome, string Detail) Start(string launcher, string root)
    {
        var token = IntPtr.Zero;
        var linked = IntPtr.Zero;
        var launchToken = IntPtr.Zero;
        var elevated = false;
        var environment = IntPtr.Zero;
        var command = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery | TokenDuplicate | TokenImpersonate, out token))
            {
                return (HookStartOutcome.Failed, "environment-token:" + Marshal.GetLastPInvokeError());
            }
            var elevationType = ReadTokenElevationType(token);
            if (elevationType is null)
            {
                return (HookStartOutcome.Failed, "token-elevation-type-query:" + Marshal.GetLastPInvokeError());
            }
            var tokenElevated = ReadTokenIsElevated(token);
            if (tokenElevated is null)
            {
                return (HookStartOutcome.Failed, "token-elevation-query:" + Marshal.GetLastPInvokeError());
            }
            var choice = SelectLaunchToken(elevationType.Value, tokenElevated.Value);
            if (choice == LaunchTokenChoice.Refuse)
            {
                var reason = elevationType == 1 && tokenElevated == true ? "elevated-token-no-limited-counterpart" : "token-elevation-inconsistent";
                return (HookStartOutcome.Failed, reason);
            }
            elevated = choice == LaunchTokenChoice.LinkedLimited;
            launchToken = token;
            if (elevated)
            {
                if (!TryGetLinkedToken(token, out linked) || linked == IntPtr.Zero || ReadTokenElevationType(linked) != 3 || ReadTokenIsElevated(linked) != false)
                {
                    return (HookStartOutcome.Failed, "linked-token-not-verified-limited");
                }
                if (!DuplicateTokenEx(linked, TokenQuery | TokenDuplicate | TokenAssignPrimary, IntPtr.Zero, SecurityImpersonation,
                    TokenTypePrimary, out launchToken))
                {
                    return (HookStartOutcome.Failed, "duplicate-limited-token:" + Marshal.GetLastPInvokeError());
                }
            }
            // Build the normal-user environment for an elevated hook, and never inherit provider or Bell Tower variables.
            if (!CreateEnvironmentBlock(out environment, launchToken, false))
            {
                return (HookStartOutcome.Failed, "environment-block:" + Marshal.GetLastPInvokeError());
            }
            var line = Quote(launcher) + " tray --data-dir " + Quote(root);
            command = Marshal.StringToHGlobalUni(line);
            var directory = Path.GetDirectoryName(launcher)!;
            var info = new StartupInfo { Flags = StartfUseShowWindow, ShowWindow = 0 };
            info.Size = (uint)Marshal.SizeOf<StartupInfo>();
            var flags = CreateNewProcessGroup | CreateUnicodeEnvironment | CreateNoWindow | CreateBreakawayFromJob | CreateSuspended;
            var created = elevated
                ? CreateProcessWithTokenW(launchToken, 0, launcher, command, flags, environment, directory, ref info, out var process)
                : CreateProcessW(launcher, command, IntPtr.Zero, IntPtr.Zero, false, flags, environment, directory, ref info, out process);
            if (created)
            {
                try
                {
                    if (!IsProcessInJob(process.Process, IntPtr.Zero, out var childInJob))
                    {
                        return StopSuspended(process, "child-job-check:" + Marshal.GetLastPInvokeError());
                    }
                    if (childInJob)
                    {
                        return StopSuspended(process, "provider-job-breakaway-incomplete");
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
                    return (HookStartOutcome.Started, elevated ? "started-with-linked-limited-token" : "started");
                }
                finally
                {
                    CloseHandle(process.Process);
                    CloseHandle(process.Thread);
                }
            }
            var error = Marshal.GetLastPInvokeError();
            if (error == ErrorAccessDenied && IsProcessInJob(GetCurrentProcess(), IntPtr.Zero, out var inJob) && inJob)
            {
                // Never fall back inside a provider job: a kill-on-close job would kill the tray with its hook.
                return (HookStartOutcome.Failed, "provider-job-breakaway-refused");
            }
            return (HookStartOutcome.Failed, "create-process:" + error);
        }
        finally
        {
            if (command != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(command);
            }
            if (environment != IntPtr.Zero)
            {
                DestroyEnvironmentBlock(environment);
            }
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
            if (launchToken != IntPtr.Zero && launchToken != token)
            {
                CloseHandle(launchToken);
            }
            if (linked != IntPtr.Zero)
            {
                CloseHandle(linked);
            }
        }
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
