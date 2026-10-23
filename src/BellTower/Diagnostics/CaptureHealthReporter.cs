using BellTower.IO;
using BellTower.Protocol;

namespace BellTower.Diagnostics;

public sealed class CaptureHealthReporter(DataPaths paths)
{
    public object Report()
    {
        try
        {
            return ReadCore();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
        {
            return new { Status = "unavailable", Category = CaptureSpool.FailureCategory(exception) };
        }
    }

    private object ReadCore()
    {
        var inbox = Path.Combine(paths.Root, CaptureProtocol.InboxName);
        var files = Directory.Exists(inbox) ? Directory.EnumerateFiles(inbox, "*.json").Take(4097).ToArray() : [];
        DateTimeOffset? oldest = null;
        foreach (var file in files.Take(4096))
        {
            var name = Path.GetFileName(file);
            if (name.Length >= 19 && long.TryParse(name.AsSpan(0, 19), out var ticks) && ticks > 0 && ticks <= DateTimeOffset.MaxValue.Ticks)
            {
                var at = new DateTimeOffset(ticks, TimeSpan.Zero);
                oldest = oldest is null || at < oldest ? at : oldest;
            }
        }
        var health = Path.Combine(paths.Root, "capture-health");
        var rejected = new Dictionary<string, string?>();
        foreach (var category in new[] { "json", "encoding", "contract-or-limit", "access", "io", "arguments", "internal" })
        {
            var path = Path.Combine(health, category + ".txt");
            if (File.Exists(path) && new FileInfo(path).Length < 128 &&
                DateTimeOffset.TryParse(File.ReadAllText(path), out var at))
            {
                rejected[category] = at.ToString("O");
            }
        }
        return new
        {
            Protocol = CaptureProtocol.Version,
            Pending = Math.Min(files.Length, 4096),
            PendingCountLimited = files.Length > 4096,
            OldestIngressAt = oldest,
            OldestPendingSeconds = oldest is null ? (double?)null : Math.Max(0, (DateTimeOffset.UtcNow - oldest.Value).TotalSeconds),
            LatestRejectionByCategory = rejected,
            LatestConsumptionLagSeconds = new { Claude = Lag("claude"), Codex = Lag("codex") },
            UnverifiedActiveSessions = new StateStore(paths).Read().Sessions.Values.Count(s => !s.Ended &&
                new AttachmentStore(paths).Get(s.AttachmentId)?.Process is null),
            AttachmentEvidenceGraceMinutes = new ConfigStore(paths).Read().AttachmentEvidenceGraceMinutes,
            AttachmentHealth = new StateStore(paths).Read().Sessions.Values.GroupBy(session => session.AttachmentHealth)
                .ToDictionary(group => group.Key, group => group.Count()),
            Ownership = new StateStore(paths).Read().Sessions.Values.Where(session => !session.Ended)
                .Select(session => new AttachmentStore(paths).Get(session.AttachmentId)?.Ownership ?? "unverified")
                .GroupBy(mode => mode).ToDictionary(group => group.Key, group => group.Count()),
            TranscriptTimeoutInspection = "opt-in doctor --transcript; Claude 2.1.306 hook_cancelled records supported; Codex 0.164.0 cli/exec does not persist structured timeout records in chat transcripts; capture health remains available",
            NativeLatencyAcceptance = "requires packaged provider-shell benchmark"
        };
    }

    private double? Lag(string provider)
    {
        var path = Path.Combine(paths.Root, provider + "-observed.json");
        if (!File.Exists(path) || new FileInfo(path).Length > 4096)
        {
            return null;
        }
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return root.TryGetProperty("at", out var at) && at.TryGetDateTimeOffset(out var ingress) &&
                root.TryGetProperty("processedAt", out var processed) && processed.TryGetDateTimeOffset(out var consumed)
                ? Math.Max(0, (consumed - ingress).TotalSeconds) : null;
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
