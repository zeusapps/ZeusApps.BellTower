using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed record WindowVerdict(long Handle, string Reason)
{
    public bool Verified => Handle != 0;
    public static WindowVerdict Unverified(string reason) => new(0, reason);
}

/// <summary>
/// Decides whether a bridge registration carries a mapping from its extension instance to exactly
/// one live top-level editor window. Anything that cannot be proven is reported as unverified so that
/// no native window call is made.
/// </summary>
public sealed class VsCodeWindowOwnership(IWindowInspector windows, Func<int, ProcessIdentity?> identify, Func<int, IReadOnlyList<int>> ancestors,
    string editorClass = VsCodeWindowOwnership.EditorWindowClass)
{
    public const int WindowProtocol = 2;
    public const string MultipleWindows = "multiple";
    public const string EditorWindowClass = "Chrome_WidgetWin_1";

    public WindowVerdict Verify(HostRegistration host, IReadOnlyList<HostRegistration> fresh, bool checkLineage)
    {
        if (host.Protocol is not { } protocol || protocol < WindowProtocol)
        {
            return WindowVerdict.Unverified("This VS Code window runs an older Bell Tower bridge. Reload the window after updating the extension.");
        }

        if (host.Window is not { } evidence || evidence.Handle == 0)
        {
            if (string.Equals(host.WindowUnavailable, MultipleWindows, StringComparison.Ordinal))
            {
                return WindowVerdict.Unverified("More than one top-level window was observed for this VS Code window (for example a floating editor), so it cannot be verified. Run Developer: Reload Window in VS Code to re-verify it.");
            }

            return WindowVerdict.Unverified("This VS Code window has not been seen in the foreground since its bridge loaded. Focus it once so Bell Tower can verify it.");
        }

        if (!string.Equals(evidence.ClassName, editorClass, StringComparison.Ordinal) || identify(evidence.Owner.Pid) != evidence.Owner)
        {
            return WindowVerdict.Unverified("The VS Code window owner changed; focus the window again to re-verify it.");
        }

        if (fresh.Any(other => other.InstanceId != host.InstanceId && other.Window?.Handle == evidence.Handle))
        {
            return WindowVerdict.Unverified("More than one VS Code bridge claims this window, so it cannot be verified.");
        }

        var facts = windows.Inspect(evidence.Handle);
        if (facts is null || !facts.Visible || facts.OwnerPid != evidence.Owner.Pid || facts.ClassName != evidence.ClassName)
        {
            return WindowVerdict.Unverified("The recorded VS Code window no longer exists; focus the window again to re-verify it.");
        }

        if (checkLineage && !ancestors(host.Process.Pid).Contains(evidence.Owner.Pid))
        {
            return WindowVerdict.Unverified("The recorded window is not owned by this bridge's editor process.");
        }

        return new(evidence.Handle, "verified");
    }

    public static VsCodeWindowOwnership Native() => new(new NativeWindowInspector(), ProcessProbe.Identify,
        pid => ProcessProbe.Ancestors(pid).Select(entry => entry.Pid).ToList());
}
