using BellTower.Core;

namespace BellTower.Hosts;

public interface IConsoleTitleWriter
{
    bool Write(ProcessIdentity shell, ProcessIdentity agent, string title, string? background = null);

    /// <summary>
    /// The title currently shown by the verified shell console, or null when the console cannot be verified. An adopted native
    /// session is written only when this prior title can be read, because restoring it is part of the write's contract.
    /// </summary>
    string? ReadTitle(ProcessIdentity shell, ProcessIdentity agent) => null;

    /// <summary>
    /// Sets <paramref name="previous"/> on the shell's console only while it still shows <paramref name="expected"/>, the title
    /// Bell Tower wrote. The agent may already have exited, so only the shell identity is verified. Returns whether a verified
    /// console no longer holds Bell Tower's title (restored, or already changed by someone else).
    /// </summary>
    bool RestoreTitle(ProcessIdentity shell, string expected, string previous) => false;

    /// <summary>Resets foreground and background to the terminal default on the shell's console, verified by the shell identity alone.</summary>
    bool ResetColors(ProcessIdentity shell) => false;
}
