using System.Text.Json;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Setup;

namespace BellTower.Diagnostics;

public sealed class HookDeliveryReporter(DataPaths paths, string? claudeConfigDirectory = null, string? codexHome = null)
{
    public object Report() => new
    {
        Claude = Provider("claude", ClaudeSettingsPath()),
        Codex = Provider("codex", CodexSettingsPath())
    };

    private object Provider(string provider, string settingsPath)
    {
        var ownership = OwnedCommands(settingsPath);
        var selectedShell = provider == "codex" ? ReadSelectedCodexShell() : null;
        var (readable, configuredHooks, brokenHooks) = ConfiguredHooks(settingsPath, provider, ownership, selectedShell);
        var deliveredAt = LastDeliveredAt(provider);
        var status = deliveredAt is not null
            ? configuredHooks > 0 ? "delivered" : "observed; configuration not detected"
            : !readable ? "unverified; settings unreadable" : configuredHooks > 0 ? "unverified" : "not configured";
        var codexShell = provider == "codex" && configuredHooks > 0 ? CodexShellStatus() : null;
        return new
        {
            Configured = configuredHooks > 0,
            ConfiguredHookCount = configuredHooks,
            KnownBrokenOwnedCommandCount = brokenHooks,
            MigrationAction = brokenHooks > 0
                ? "Preview: bell-tower setup --clients codex --migrate-edited-hooks; review the changes, then add --apply to write backups."
                : null,
            SelectedShell = codexShell,
            SettingsReadable = readable,
            Delivered = deliveredAt is not null,
            LastDeliveredAt = deliveredAt,
            Status = status
        };
    }

    private object CodexShellStatus()
    {
        var expectedShell = ReadSelectedCodexShell();
        if (expectedShell is null)
        {
            return new { Expected = (string?)null, Current = (string?)null, Status = "unverified; rerun setup to record a valid shell selection" };
        }
        var expected = expectedShell.Value.ToString();
        if (!OperatingSystem.IsWindows())
        {
            return new { Expected = expected, Current = (string?)null, Status = "unverified; Windows shell selection applies only on Windows" };
        }

        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return new { Expected = expected, Current = (string?)null, Status = "unverified; installed payload unavailable" };
            }
            var processDirectory = Path.GetDirectoryName(Path.GetFullPath(processPath))!;
            var installation = File.Exists(Path.Combine(processDirectory, "installation.json"))
                ? new Installation(processDirectory)
                : Installation.ForPayload(processPath);
            if (installation is null)
            {
                return new { Expected = expected, Current = (string?)null, Status = "unverified; installed payload unavailable" };
            }
            var payload = installation.Payload(installation.Selection().Version);
            var ingress = Path.Combine(payload, "bell-tower-ingress.exe");
            var contract = installation.Contract(installation.Selection().Version);
            if (contract.Files?.GetValueOrDefault("bell-tower-ingress.exe") is not { } hash ||
                !File.Exists(ingress) || !Installation.Hash(ingress).Equals(hash, StringComparison.OrdinalIgnoreCase))
            {
                return new { Expected = expected, Current = (string?)null, Status = "unverified; packaged ingress unavailable" };
            }
            var executable = Path.Combine(payload, "bell-tower.exe");
            var currentShell = ProviderHookProbe.SelectCodexShell(executable, ingress);
            return new
            {
                Expected = expected,
                Current = currentShell.ToString(),
                Status = currentShell == expectedShell.Value ? "matches" : "drift; preview setup --clients codex to refresh"
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.ComponentModel.Win32Exception)
        {
            return new { Expected = expected, Current = (string?)null, Status = "unverified; isolated native shell probe did not complete" };
        }
    }

    private HashSet<string> OwnedCommands(string settingsPath)
    {
        var manifest = Path.Combine(paths.Root, "integration.json");
        if (!File.Exists(manifest) || new FileInfo(manifest).Length > 1024 * 1024)
        {
            return new(StringComparer.Ordinal);
        }
        try
        {
            var ownership = Json.Decode<IntegrationOwnership>(File.ReadAllText(manifest));
            var fullPath = Path.GetFullPath(settingsPath);
            var match = ownership.Commands.FirstOrDefault(pair =>
            {
                try
                { return Path.GetFullPath(pair.Key).Equals(fullPath, StringComparison.OrdinalIgnoreCase); }
                catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException) { return false; }
            });
            return match.Value?.ToHashSet(StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new(StringComparer.Ordinal);
        }
    }

    private (bool Readable, int Count, int Broken) ConfiguredHooks(string path, string provider, HashSet<string> owned, CodexHookShell? selectedShell)
    {
        if (!File.Exists(path))
        {
            return (true, 0, 0);
        }
        if (new FileInfo(path).Length > 1024 * 1024)
        {
            return (false, 0, 0);
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 32 });
            var commands = OwnedCommandsIn(document.RootElement, owned);
            var broken = provider == "codex" ? commands.Count(command => IsKnownBrokenCodexCommand(command, selectedShell)) : 0;
            return (true, commands.Count, broken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return (false, 0, 0);
        }
    }

    private static List<string> OwnedCommandsIn(JsonElement value, HashSet<string> owned)
    {
        var commands = new List<string>();
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (property.NameEquals("command") && property.Value.ValueKind == JsonValueKind.String)
                {
                    var command = property.Value.GetString() ?? "";
                    if (owned.Contains(command))
                    {
                        commands.Add(command);
                    }
                }
                commands.AddRange(OwnedCommandsIn(property.Value, owned));
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                commands.AddRange(OwnedCommandsIn(item, owned));
            }
        }
        return commands;
    }

    private static bool IsKnownBrokenCodexCommand(string command, CodexHookShell? selectedShell) =>
        command.StartsWith('"') && selectedShell != CodexHookShell.Cmd;

    private CodexHookShell? ReadSelectedCodexShell()
    {
        var manifest = Path.Combine(paths.Root, "integration.json");
        if (!File.Exists(manifest) || new FileInfo(manifest).Length > 1024 * 1024)
        {
            return null;
        }
        try
        {
            var value = Json.Decode<IntegrationOwnership>(File.ReadAllText(manifest)).SelectedCodexHookShell;
            return Enum.TryParse<CodexHookShell>(value, ignoreCase: false, out var shell) && Enum.IsDefined(shell) ? shell : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return null;
        }
    }

    private DateTimeOffset? LastDeliveredAt(string provider)
    {
        var path = Path.Combine(paths.Root, provider + "-observed.json");
        if (!File.Exists(path) || new FileInfo(path).Length > 4096)
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            if (root.TryGetProperty("at", out var at) || root.TryGetProperty("At", out at))
            {
                return at.ValueKind == JsonValueKind.String && at.TryGetDateTimeOffset(out var timestamp) ? timestamp : null;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
        return null;
    }

    private string ClaudeSettingsPath()
    {
        var root = claudeConfigDirectory ?? Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        return Path.Combine(root, "settings.json");
    }

    private string CodexSettingsPath()
    {
        var root = codexHome ?? Environment.GetEnvironmentVariable("CODEX_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        return Path.Combine(root, "hooks.json");
    }
}
