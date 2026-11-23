using System.Collections.Concurrent;
using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;

namespace BellTower.IO;

/// <summary>
/// Whether a provider's own terminal title is in effect disabled for a session (design decision 11, session-identity "Provider-owned
/// terminal titles"). Bell Tower writes a title only when the disabling setting is present in the provider's actual settings file, whoever
/// put it there (its opt-in step or the user), and the session started after the setting took effect: a provider reads its configuration when
/// it starts, so a session that was already running keeps writing its own title.
/// <para>
/// The time a setting took effect is kept per resolved settings file, so a process that resolves another home (a doctor run from a terminal
/// with a different <c>CODEX_HOME</c>) never touches the record of the file the tray uses, and doctor only reads. A read or parse failure (a
/// sharing violation while the provider rewrites its file, a partly written file) is "unknown": it is neither cached nor forgotten, and the
/// answer stays what the record says. The tray observes both files on its tick, so a removal is seen while it runs; a removal and re-add of
/// the setting while the tray is stopped is not seen (a documented limitation).
/// </para>
/// </summary>
public sealed class ProviderTitleState(DataPaths paths)
{
    public const string ClaudeKey = "CLAUDE_CODE_DISABLE_TERMINAL_TITLE";
    private const int MaxBytes = 1 << 20;

    // A test seam: where a provider's home is. The default is the real resolution (the provider's own variable, then the user profile).
    internal static Func<string, string?>? DefaultHome { get; set; }
    internal static readonly AsyncLocal<Func<string, string?>?> ScopedHome = new();

    private enum Reading { Present, Absent, Unknown }

    private sealed record Cached(long Stamp, Reading Reading);

    /// <summary>The effective time of a setting at one resolved file and the file stamp it was last observed at.</summary>
    public sealed record Entry(DateTimeOffset Since, long Stamp);

    private static readonly ConcurrentDictionary<string, Cached> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The provider's actual settings file that holds the disabling setting.</summary>
    public static string SettingsFile(string provider)
    {
        var home = ScopedHome.Value?.Invoke(provider) ?? DefaultHome?.Invoke(provider) ?? ProviderHome(provider);
        return Path.Combine(home, provider == "claude" ? "settings.json" : "config.toml");
    }

    private static string ProviderHome(string provider) =>
        Environment.GetEnvironmentVariable(provider == "claude" ? "CLAUDE_CONFIG_DIR" : "CODEX_HOME") is { Length: > 0 } configured
            ? configured : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + provider);

    private string RecordPath => Path.Combine(paths.Root, "provider-titles.json");

    /// <summary>True when a session of <paramref name="provider"/> whose process started at <paramref name="startedUtcTicks"/> runs with the provider's own title disabled.</summary>
    public bool DisabledFor(string provider, long? startedUtcTicks) =>
        startedUtcTicks is { } started && Observe(provider, record: true) is { } since && started >= since.UtcTicks;

    /// <summary>
    /// What the tray's tick does for both providers: look at both resolved files so a removal is seen while the tray runs. It is the only
    /// regular observer besides a title write; nothing else forgets a record.
    /// </summary>
    public void ObserveBoth()
    {
        _ = Observe("claude", record: true);
        _ = Observe("codex", record: true);
    }

    /// <summary>
    /// The state doctor reports: whether the setting is present now and since when sessions started after it have it. Read-only: it never
    /// writes or forgets a record, whatever home this process resolves.
    /// </summary>
    public (bool Present, DateTimeOffset? Since, string File) Report(string provider)
    {
        var since = Observe(provider, record: false);
        return (since is not null, since, SettingsFile(provider));
    }

    // Null when the setting is absent (or unknown with no record). Otherwise the time it took effect: the file's write time when this was
    // first seen present, kept while it stays present so unrelated later edits do not move it. With record false nothing is written.
    private DateTimeOffset? Observe(string provider, bool record)
    {
        string file;
        FileInfo info;
        try
        {
            file = Path.GetFullPath(SettingsFile(provider));
            info = new FileInfo(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
        var key = provider + "|" + file.ToUpperInvariant();
        Reading reading;
        long stamp;
        try
        {
            if (!info.Exists)
            {
                reading = Reading.Absent;
                stamp = 0;
            }
            else if (info.Length > MaxBytes)
            {
                reading = Reading.Unknown;
                stamp = 0;
            }
            else
            {
                stamp = info.LastWriteTimeUtc.Ticks ^ info.Length;
                var cacheKey = paths.Root + "|" + key;
                if (!Cache.TryGetValue(cacheKey, out var cached) || cached.Stamp != stamp)
                {
                    cached = new(stamp, Read(provider, file));
                    // An unknown reading is never cached: the next look reads the file again.
                    if (cached.Reading == Reading.Unknown)
                    {
                        Cache.TryRemove(cacheKey, out _);
                    }
                    else
                    {
                        Cache[cacheKey] = cached;
                    }
                }
                reading = cached.Reading;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            reading = Reading.Unknown;
            stamp = 0;
        }

        var records = ReadRecords();
        records.TryGetValue(key, out var entry);
        switch (reading)
        {
            case Reading.Absent:
                if (record && records.Remove(key))
                {
                    WriteRecords(records);
                }
                return null;
            case Reading.Unknown:
                // Not seen absent and not seen present: what is on record stays true, and nothing is recorded or forgotten.
                return entry?.Since;
            default:
                if (entry is not null)
                {
                    if (record && entry.Stamp != stamp)
                    {
                        records[key] = entry with { Stamp = stamp };
                        WriteRecords(records);
                    }
                    return entry.Since;
                }
                // First seen present: it took effect no later than the file's last write.
                var since = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
                if (record)
                {
                    records[key] = new(since, stamp);
                    WriteRecords(records);
                }
                return since;
        }
    }

    // A record in the older provider-keyed shape fails to read as this shape and is treated as empty (unknown), so the setting is re-sighted.
    private Dictionary<string, Entry> ReadRecords()
    {
        try
        {
            var values = AtomicFile.Read<Dictionary<string, Entry>>(RecordPath);
            return values is null ? [] : new(values, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidDataException)
        {
            // Includes a null document or an oversize file: unreadable records only withhold a title, and the next sighting rewrites them.
            return [];
        }
    }

    private void WriteRecords(Dictionary<string, Entry> records)
    {
        try
        {
            Directory.CreateDirectory(paths.Root);
            AtomicFile.Write(RecordPath, Json.Encode(records));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The answer for this call still holds; the record is only what keeps it stable across calls and restarts.
        }
    }

    private static Reading Read(string provider, string file)
    {
        try
        {
            var text = File.ReadAllText(file);
            if (provider == "claude")
            {
                using var document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("env", out var env) &&
                    env.ValueKind == JsonValueKind.Object && env.TryGetProperty(ClaudeKey, out var value) &&
                    (value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()) == "1" ? Reading.Present : Reading.Absent;
            }
            var model = TomlSerializer.Deserialize<TomlTable>(text);
            return model is not null && model.TryGetValue("tui", out var tui) && tui is TomlTable table &&
                table.TryGetValue("terminal_title", out var title) && title is TomlArray { Count: 0 } ? Reading.Present : Reading.Absent;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or TomlException)
        {
            // A locked or partly written file says nothing about the setting.
            return Reading.Unknown;
        }
    }
}
