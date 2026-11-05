using System.Net.Sockets;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Providers;

/// <summary>
/// Binds a connected local-socket server to a process. Windows exposes the peer process ID of a connected AF_UNIX socket through
/// the SIO_AF_UNIX_GETPEERPID ioctl (declared in the Windows SDK header afunix.h; value 0x58000100, WSAIoctl with a 4-byte output).
/// Where the ioctl is unavailable, returns no process, or the process cannot be read, the result is a distinct fail-closed refusal.
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

    /// <summary>The identity (PID plus creation time) of the process serving a connected socket.</summary>
    public static ProcessIdentity Identify(Socket connected, Func<Socket, int?>? peerOf = null)
    {
        var pid = (peerOf ?? PeerPid)(connected) ??
            throw new CodexTransportException(CodexTransportFailure.PeerUnavailable, "The server process of the local socket could not be determined.");
        return ProcessProbe.Identify(pid) ??
            throw new CodexTransportException(CodexTransportFailure.PeerUnavailable, "The server process of the local socket is not readable.");
    }
}
