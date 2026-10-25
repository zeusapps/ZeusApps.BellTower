namespace BellTower.Hosts;

public sealed record WindowFacts(long Handle, int OwnerPid, string ClassName, bool Visible);

public interface IWindowInspector
{
    /// <summary>Describes a live top-level window, or null when the handle no longer names one.</summary>
    WindowFacts? Inspect(long handle);

    /// <summary>Describes the current foreground window, or null when there is none.</summary>
    WindowFacts? Foreground();
}
