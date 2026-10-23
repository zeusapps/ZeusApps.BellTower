using System.Text.Json;
using BellTower.Providers;

namespace BellTower.Diagnostics;

public sealed class TranscriptTimeoutReader
{
    public const int MaximumBytes = 1024 * 1024;
    public const int MaximumLineBytes = 64 * 1024;
    public const int MaximumRecords = 2000;

    public object Read(string provider, string path)
    {
        if (provider is not ("claude" or "codex"))
        {
            throw new ArgumentException("Provider must be claude or codex.");
        }
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            var clipped = length > MaximumBytes;
            stream.Position = Math.Max(0, length - MaximumBytes);
            var bytes = new byte[(int)Math.Min(length, MaximumBytes)];
            var read = 0;
            while (read < bytes.Length)
            {
                var count = stream.Read(bytes, read, bytes.Length - read);
                if (count == 0)
                {
                    break;
                }
                read += count;
            }
            var lines = new Queue<(int Start, int Length)>();
            var start = 0;
            for (var index = 0; index < read; index++)
            {
                if (bytes[index] != (byte)'\n')
                {
                    continue;
                }
                if (!(clipped && start == 0) && index - start <= MaximumLineBytes)
                {
                    lines.Enqueue((start, index - start));
                    if (lines.Count > MaximumRecords)
                    {
                        lines.Dequeue();
                    }
                }
                start = index + 1;
            }
            var timeouts = 0;
            DateTimeOffset? from = null;
            DateTimeOffset? to = null;
            string? verifiedCodexMode = null;
            foreach (var line in lines)
            {
                try
                {
                    using var document = JsonDocument.Parse(bytes.AsMemory(line.Start, line.Length), new() { MaxDepth = 32 });
                    var root = document.RootElement;
                    if (provider == "codex")
                    {
                        if (HookInput.Text(root, "type") == "session_meta" && root.TryGetProperty("payload", out var metadata) &&
                            HookInput.Text(metadata, "cli_version") == "0.164.0" && HookInput.Text(metadata, "source") is "cli" or "exec")
                        {
                            verifiedCodexMode = HookInput.Text(metadata, "source");
                        }
                        continue;
                    }
                    // Retained capture: Claude 2.1.306 SDK CLI, attachment/hook_cancelled.
                    if (HookInput.Text(root, "type") != "attachment" || !root.TryGetProperty("attachment", out var attachment) ||
                        HookInput.Text(attachment, "type") != "hook_cancelled" ||
                        !attachment.TryGetProperty("timedOut", out var timedOut) || timedOut.ValueKind != JsonValueKind.True ||
                        !attachment.TryGetProperty("timeoutMs", out var timeout) || timeout.ValueKind != JsonValueKind.Number ||
                        !timeout.TryGetInt64(out var milliseconds) || milliseconds <= 0 ||
                        HookInput.Text(root, "timestamp") is not { } timestamp || !DateTimeOffset.TryParse(timestamp, out var at))
                    {
                        continue;
                    }
                    timeouts++;
                    from = from is null || at < from ? at : from;
                    to = to is null || at > to ? at : to;
                }
                catch (JsonException) { }
            }
            if (provider == "codex")
            {
                return new
                {
                    Provider = provider,
                    Status = verifiedCodexMode is null ? "unverified-transcript-schema" : "timeout-records-unavailable-in-verified-cli-mode",
                    Timeouts = (int?)null,
                    VerifiedVersion = verifiedCodexMode is null ? null : "0.164.0",
                    VerifiedMode = verifiedCodexMode,
                    Fallback = "capture health and native hook status",
                    ScannedBytes = read
                };
            }
            return new
            {
                Provider = provider,
                Status = "claude-hook-cancelled-schema",
                Timeouts = timeouts,
                From = from,
                To = to,
                ScannedBytes = read,
                ScannedRecords = lines.Count,
                TailLimited = clipped,
                TrailingPartialLineIgnored = start < read
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new { Provider = provider, Status = "unavailable-" + BellTower.Protocol.CaptureSpool.FailureCategory(exception), Timeouts = (int?)null };
        }
    }
}
