using System.Text.RegularExpressions;

namespace BellTower.FieldAcceptance.Evidence;

/// <summary>
/// Rewrites text for committable evidence: the repository, the user's profile and application-data folders, the temp folder, the
/// user and computer names become placeholders. Longer (more specific) values are replaced first, paths in every slash form.
/// </summary>
public sealed class EvidenceSanitizer
{
    private readonly List<(Regex Pattern, string Value, string Placeholder)> replacements = [];

    public EvidenceSanitizer(string repositoryRoot, string profile, string localAppData, string roamingAppData, string temp, string userName, string machineName)
    {
        AddPath(repositoryRoot, "<repo>");
        AddPath(localAppData, "%LOCALAPPDATA%");
        AddPath(roamingAppData, "%APPDATA%");
        AddPath(temp, "%TEMP%");
        AddPath(profile, "%USERPROFILE%");
        AddWord(userName, "<user>");
        AddWord(machineName, "<host>");
        replacements.Sort((left, right) => right.Value.Length.CompareTo(left.Value.Length));
    }

    public static EvidenceSanitizer ForCurrentUser(string repositoryRoot) => new(
        repositoryRoot,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Path.GetTempPath(), Environment.UserName, Environment.MachineName);

    private void AddPath(string value, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 3)
        {
            return;
        }
        var trimmed = value.TrimEnd('\\', '/');
        foreach (var form in new[] { trimmed, trimmed.Replace('\\', '/'), trimmed.Replace("\\", "\\\\") }.Distinct(StringComparer.Ordinal))
        {
            replacements.Add((new Regex(Regex.Escape(form), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), form, placeholder));
        }
    }

    private void AddWord(string value, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 3)
        {
            return;
        }
        replacements.Add((new Regex("(?<![A-Za-z0-9])" + Regex.Escape(value) + "(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), value, placeholder));
    }

    public string Sanitize(string text)
    {
        foreach (var (pattern, _, placeholder) in replacements)
        {
            text = pattern.Replace(text, placeholder);
        }
        return text;
    }

    /// <summary>True when the text still holds any private value this sanitizer knows (the final check before a file is committable).</summary>
    public bool ContainsPrivateValue(string text) => replacements.Any(entry => entry.Pattern.IsMatch(text));
}
