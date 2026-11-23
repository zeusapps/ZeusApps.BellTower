using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BellTower.Providers;

/// <summary>
/// Bounded reads of the end of a provider transcript, the evidence the shared wait rule uses where the provider's hooks say nothing
/// (design decision 12). Reads are capped in size and line length, never follow a path that is not an absolute regular file, never
/// block the hook (they run in the tray), and no result carries a path, prompt or message text.
/// </summary>
public static class TranscriptTail
{
    public const int MaxBytes = 1 << 20;
    public const int MaxLineBytes = 256 * 1024;

    /// <summary>The complete lines in the last <paramref name="maxBytes"/> of the file; empty when the path is not a readable absolute file.</summary>
    public static IReadOnlyList<string> Read(string? path, int maxBytes = MaxBytes)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || path.Length > 1024 || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
                !File.Exists(path) || (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.Device | FileAttributes.ReparsePoint)) != 0)
            {
                return [];
            }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var start = Math.Max(0, stream.Length - maxBytes);
            stream.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[(int)Math.Min(stream.Length - start, maxBytes)];
            var read = 0;
            int count;
            while (read < buffer.Length && (count = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += count;
            }
            var lines = Encoding.UTF8.GetString(buffer, 0, read).Split('\n').ToList();
            // A read that starts inside the file begins with a partial line; the last line may still be being written.
            if (start > 0 && lines.Count > 0)
            {
                lines.RemoveAt(0);
            }
            if (lines.Count > 0 && read > 0 && buffer[read - 1] != (byte)'\n')
            {
                lines.RemoveAt(lines.Count - 1);
            }
            return lines.Where(line => line.Length is > 0 and <= MaxLineBytes).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }
}

/// <summary>
/// Codex adapter: whether a held approval needs a person. Codex records the turn's approval policy and reviewer in a
/// <c>turn_context</c> record before any tool runs. <c>approvals_reviewer</c> is <c>user</c> when the person decides and
/// <c>auto_review</c> when Codex reviews automatically. The <c>PermissionRequest</c> hook payload (Codex 0.171.0) names no reviewer, so
/// the transcript is the only per-session evidence there is for a direct <c>--no-daemon</c> session.
/// </summary>
public static class CodexApprovalEvidence
{
    /// <summary>
    /// True: the reviewer is the user, so a pending approval is a human wait. False: another reviewer decides, so it is not.
    /// Null: no turn record was read (nothing is confirmed or suppressed). A turn id selects that turn's record; without one the
    /// latest record is used.
    /// </summary>
    public static bool? HumanMustDecide(IReadOnlyList<string> lines, string? turnId)
    {
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            var line = lines[index];
            if (!line.Contains("\"turn_context\"", StringComparison.Ordinal))
            {
                continue;
            }
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || Text(root, "type") != "turn_context" ||
                    !root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                if (turnId is not null && Text(payload, "turn_id") is { } recorded && recorded != turnId)
                {
                    // A later turn's record is not this turn's evidence; keep looking for an earlier match only.
                    continue;
                }
                return Text(payload, "approvals_reviewer") switch
                {
                    "user" => true,
                    null => null,
                    _ => false
                };
            }
            catch (JsonException)
            {
                // A damaged line is skipped, never guessed.
            }
        }
        return null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// Claude Code adapter: whether the user declined the prompt of an outstanding permission wait. Claude Code fires no hook for a manual
/// decline (2.1.330 probe), but records the rejected tool result at once: a user record whose <c>tool_result</c> is an error for the
/// tool use, with <c>toolDenialKind</c> <c>user-rejected</c>.
/// </summary>
public static class ClaudeDeclineEvidence
{
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(5);

    /// <summary>
    /// True when a user rejection for one of <paramref name="toolUseIds"/> (or, when the wait names none, any user rejection) is recorded
    /// at or after <paramref name="since"/> less a small clock skew.
    /// </summary>
    public static bool Declined(IReadOnlyList<string> lines, IReadOnlyCollection<string> toolUseIds, DateTimeOffset since) =>
        DeclinedAt(lines, toolUseIds, since) is not null;

    /// <summary>The time the rejection was recorded, or null. The decline is applied at that time, so a prompt typed right after it is never cleared by it.</summary>
    public static DateTimeOffset? DeclinedAt(IReadOnlyList<string> lines, IReadOnlyCollection<string> toolUseIds, DateTimeOffset since)
    {
        // Newest first: the decline of the wait that is outstanding now is the latest rejection, never an earlier prompt's.
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            var line = lines[index];
            if (!line.Contains("user-rejected", StringComparison.Ordinal))
            {
                continue;
            }
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || Text(root, "toolDenialKind") != "user-rejected" ||
                    !DateTimeOffset.TryParse(Text(root, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
                {
                    continue;
                }
                if (root.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in content.EnumerateArray())
                    {
                        if (Text(item, "type") != "tool_result" || !item.TryGetProperty("is_error", out var error) || error.ValueKind != JsonValueKind.True)
                        {
                            continue;
                        }
                        // A matched tool use identifier proves which prompt it was, so a small clock skew is tolerated. Without one the
                        // rejection must be recorded at or after the wait started, or it belongs to an earlier prompt.
                        var matched = toolUseIds.Count != 0 && Text(item, "tool_use_id") is { } id && toolUseIds.Contains(id);
                        if (matched && at >= since - Skew || toolUseIds.Count == 0 && at >= since)
                        {
                            return at;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // A damaged line is skipped, never guessed.
            }
        }
        return null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
