using System.Text.RegularExpressions;

namespace BellTower.Core;

// Pure path and skill classification shared by hook ingress and history recovery (no identity or disk access).
public static partial class WorkClassifier
{
    public const string ChangeMarker = "/openspec/changes/";

    public static bool IsChangeName(string value) => ChangeName().IsMatch(value);

    // Classifies a file path as an edit of a non-archive change folder.
    public static (string Change, string Root)? ChangeFromPath(string? value, string? cwd)
    {
        if (value is null || value.Length > 32768 || value.Contains('$') || value.Contains('`'))
        {
            return null;
        }
        try
        {
            if (!Path.IsPathFullyQualified(value) && (cwd is null || !Path.IsPathFullyQualified(cwd)))
            {
                return null;
            }
            var full = Path.GetFullPath(value, cwd ?? "").Replace('\\', '/');
            var marker = full.IndexOf(ChangeMarker, StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                return null;
            }
            var change = full[(marker + ChangeMarker.Length)..].Split('/')[0];
            return change != "archive" && IsChangeName(change) ? (change, full[..marker]) : null;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    // Workflow skills that only explain or explore stay weak: they may supply initial context
    // but never replace established work.
    public static bool IsWeakSkill(string skill) =>
        skill.Contains("explore", StringComparison.Ordinal) || skill == "explain-change";

    public static bool IsWorkflowSkill(string skill) =>
        skill.StartsWith("opsx:", StringComparison.Ordinal) || skill.StartsWith("openspec-", StringComparison.Ordinal) || skill == "explain-change";

    [GeneratedRegex("\\A[a-z0-9]+(?:-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ChangeName();
}
