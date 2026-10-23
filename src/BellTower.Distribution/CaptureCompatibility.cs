using System.Text.Json;

namespace BellTower.Distribution;

public static class CaptureCompatibility
{
    public static void Ensure(string dataDirectory, int consumer, int attachmentHealth = 0)
    {
        AttachmentHealthCompatibility.Ensure(dataDirectory, attachmentHealth);
        var marker = Path.Combine(dataDirectory, "capture-protocol.json");
        if (!File.Exists(marker))
        {
            return;
        }
        var document = DistributionJson.Read<JsonElement>(marker, 4096);
        if (document.ValueKind != JsonValueKind.Object || !document.TryGetProperty("protocol", out var protocol) ||
            protocol.ValueKind != JsonValueKind.Number || !protocol.TryGetInt32(out var version) || version != 1 || consumer != 1)
        {
            throw new InvalidDataException("Enabled fast hooks require a capture-v1 consumer; keep a compatible tray selected. Captures and agent sessions were preserved.");
        }
    }

    public static void EnsureTrayInvocation(string[] args, int consumer, int attachmentHealth = 0)
    {
        if (!StartsTray(args))
        {
            return; // Legacy normalized event helpers remain compatible.
        }
        var index = Array.IndexOf(args, "--data-dir");
        var inline = args.FirstOrDefault(arg => arg.StartsWith("--data-dir=", StringComparison.Ordinal));
        var directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : inline is not null ? inline[11..] : Environment.GetEnvironmentVariable("BELLTOWER_DATA_DIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeusApps.BellTower");
        Ensure(directory, consumer, attachmentHealth);
    }

    private static bool StartsTray(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument == "--data-dir")
            {
                index++;
                continue;
            }
            if (argument.StartsWith("--data-dir=", StringComparison.Ordinal) || argument.StartsWith("--silent=", StringComparison.Ordinal))
            {
                continue;
            }
            if (argument == "--silent")
            {
                if (index + 1 < args.Length && args[index + 1] is "true" or "false")
                {
                    index++;
                }
                continue;
            }
            return argument is "tray" or "start";
        }
        return true;
    }
}
