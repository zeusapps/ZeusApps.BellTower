using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Providers;

/// <summary>
/// What makes a process an acceptable shared Codex backend to observe: live (PID plus creation time), a codex image, a command
/// line that classifies as a shared backend, and not elevated. Each requirement refuses with its own reason, in that order, so a
/// renamed binary, a different launch shape and a privilege boundary stay distinguishable in diagnostics. Identity is checked
/// before elevation so a refusal names the first thing that is wrong with the process. The managed package root is not a
/// requirement (it differs between installs and shims).
/// </summary>
/// <param name="accept">Replaces all requirements with one predicate (tests that serve from the test process).</param>
public sealed class CodexBackendCheck(Func<ProcessIdentity, bool>? accept = null, Func<ProcessIdentity, ProcessLiveness>? liveness = null,
    Func<int, bool>? isCodexImage = null, Func<int, string?>? commandLine = null, Func<int, bool?>? isElevated = null)
{
    /// <summary>Accepts every process the predicate accepts and applies no further requirement.</summary>
    public static CodexBackendCheck Accepting(Func<ProcessIdentity, bool>? predicate = null) => new(predicate ?? (_ => true));

    private readonly Func<ProcessIdentity, ProcessLiveness> liveness = liveness ?? ProcessProbe.Inspect;
    private readonly Func<int, bool> isCodexImage = isCodexImage ?? CodexImage;
    private readonly Func<int, string?> commandLine = commandLine ?? ProcessProbe.CommandLine;
    private readonly Func<int, bool?> isElevated = isElevated ?? ProcessProbe.IsElevated;

    /// <summary>Liveness of the verified backend, rechecked before every request.</summary>
    public bool IsLive(ProcessIdentity backend) => accept?.Invoke(backend) ?? liveness(backend) == ProcessLiveness.Live;

    public void Verify(ProcessIdentity backend)
    {
        if (accept is not null)
        {
            if (!accept(backend))
            {
                throw new CodexTransportException(CodexTransportFailure.BackendNotVerified, "The process serving the control socket is not a live Codex shared backend.");
            }
            return;
        }
        if (liveness(backend) != ProcessLiveness.Live)
        {
            throw new CodexTransportException(CodexTransportFailure.BackendNotVerified, "The process serving the control socket is not live.");
        }
        if (!isCodexImage(backend.Pid))
        {
            throw new CodexTransportException(CodexTransportFailure.ImageMismatch, "The process serving the control socket is not a codex image.");
        }
        var text = commandLine(backend.Pid) ??
            throw new CodexTransportException(CodexTransportFailure.PeerUnreadable, "The command line of the process serving the control socket cannot be read.");
        if (AgentModeClassifier.Classify("codex", true, AgentModeClassifier.Arguments(text)) != AgentModes.SharedBackend)
        {
            throw new CodexTransportException(CodexTransportFailure.CommandLineMismatch, "The command line of the process serving the control socket is not a shared backend.");
        }
        switch (isElevated(backend.Pid))
        {
            case true:
                throw new CodexTransportException(CodexTransportFailure.ElevatedPeer, "The shared Codex backend runs elevated and is not observed across the privilege boundary.");
            case null:
                throw new CodexTransportException(CodexTransportFailure.PeerUnreadable, "The privilege level of the process serving the control socket cannot be read.");
        }
    }

    // The image name only: the managed package root differs between installs and shims and is not a requirement.
    private static bool CodexImage(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return string.Equals(process.ProcessName, "codex", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
