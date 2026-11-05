using System.Net.Sockets;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Providers;

/// <summary>
/// Binds a connected local-socket server to a process. Windows exposes the peer process ID of a connected AF_UNIX socket through
/// the SIO_AF_UNIX_GETPEERPID ioctl (declared in the Windows SDK header afunix.h; value 0x58000100, WSAIoctl with a 4-byte output).
/// Where the ioctl is unsupported, returns no process, or the process cannot be read, the result is a distinct fail-closed refusal.
/// </summary>
public static class CodexSharedPeer
{
    private const int GetPeerPid = 0x58000100;

    /// <summary>The peer process ID of a connected socket, or null when the ioctl is unavailable or reports no process.</summary>
    public static int? PeerPid(Socket connected)
    {
        try
        {
            var output = new byte[sizeof(int)];
            connected.IOControl(GetPeerPid, null, output);
            var pid = BitConverter.ToInt32(output);
            return pid > 0 ? pid : null;
        }
        catch (Exception exception) when (exception is SocketException or PlatformNotSupportedException or NotSupportedException or ObjectDisposedException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The identity (PID plus creation time) of the process serving a connected socket. Distinct refusals: the platform cannot
    /// report the peer (<see cref="CodexTransportFailure.PeerUnsupported"/>), the peer ended before it was read
    /// (<see cref="CodexTransportFailure.BackendNotVerified"/>) or exists but cannot be read (<see cref="CodexTransportFailure.PeerUnreadable"/>).
    /// </summary>
    public static ProcessIdentity Identify(Socket connected, Func<Socket, int?>? peerOf = null, Func<int, ProcessIdentity?>? identify = null)
    {
        var pid = (peerOf ?? PeerPid)(connected) ??
            throw new CodexTransportException(CodexTransportFailure.PeerUnsupported, "The server process of the local socket could not be determined on this system.");
        return (identify ?? ProcessProbe.Identify)(pid) ?? throw (Exists(pid)
            ? new CodexTransportException(CodexTransportFailure.PeerUnreadable, "The server process of the local socket cannot be read.")
            : new CodexTransportException(CodexTransportFailure.BackendNotVerified, "The server process of the local socket ended before it was verified."));
    }

    private static bool Exists(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }
}
