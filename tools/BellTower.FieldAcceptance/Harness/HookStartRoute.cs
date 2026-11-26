namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// The route name the product records in <c>hook-start/last-attempt.txt</c> when the shell start succeeded. The product constant is
/// internal (<c>TrayProcess.RouteShell</c>); a unit test pins this copy to it.
/// </summary>
public static class HookStartRoute
{
    public const string Shell = "started-via-shell";
}
