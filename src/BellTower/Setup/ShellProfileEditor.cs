namespace BellTower.Setup;

public static class ShellProfileEditor
{
    private const string Start = "# BELLTOWER:START";
    private const string End = "# BELLTOWER:END";

    public static string Update(string original, string? previous, string? replacement)
    {
        var start = original.IndexOf(Start, StringComparison.Ordinal);
        var end = original.IndexOf(End, StringComparison.Ordinal);
        if (start >= 0 || end >= 0)
        {
            if (start < 0 || end < start || previous is null || original[start..(end + End.Length)] != previous)
            {
                throw new InvalidOperationException("A PowerShell Bell Tower block was changed or belongs to another installation. Review it before rerunning setup.");
            }
            return original[..start] + (replacement ?? "") + original[(end + End.Length)..];
        }
        return replacement is null ? original : original + (original.EndsWith('\n') || original.Length == 0 ? "" : "\r\n") + replacement + "\r\n";
    }

    public static string Block(string module) => Start + "\r\nImport-Module " + PowerShellModule.Literal(module) + " -Global -NoClobber\r\n" + End;
}
