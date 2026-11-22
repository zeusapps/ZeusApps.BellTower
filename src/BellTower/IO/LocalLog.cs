namespace BellTower.IO;

public sealed class LocalLog(DataPaths paths)
{
    public void Write(string code) => Write(code, null);

    /// <summary>Writes a code with a short reason. The caller sanitizes paths out of the reason; control characters are removed and the reason is capped at 200 characters here.</summary>
    public void Write(string code, string? reason)
    {
        try
        {
            using var gate = new DataLock(paths, "log");
            Directory.CreateDirectory(paths.Root);
            var path = Path.Combine(paths.Root, "bell-tower.log");
            // Codes are filtered here. A reason is accepted only after its caller sanitized it (RefreshHealth.Describe); never pass raw exception messages or original event payloads.
            var safe = new string(code.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ' ').Take(120).ToArray());
            if (File.Exists(path) && new FileInfo(path).Length > 250 * 1024)
            {
                File.Delete(path);
            }

            var detail = reason is null ? "" : ": " + new string(reason.Where(c => !char.IsControl(c)).Take(200).ToArray());
            File.AppendAllText(path, DateTimeOffset.UtcNow.ToString("O") + " " + safe + detail + Environment.NewLine);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
