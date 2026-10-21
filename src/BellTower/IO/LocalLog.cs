namespace BellTower.IO;

public sealed class LocalLog(DataPaths paths)
{
    public void Write(string code)
    {
        try
        {
            using var gate = new DataLock(paths, "log");
            Directory.CreateDirectory(paths.Root);
            var path = Path.Combine(paths.Root, "bell-tower.log");
            // Deliberately accept codes, never exceptions/messages or original event payloads.
            var safe = new string(code.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ' ').Take(120).ToArray());
            if (File.Exists(path) && new FileInfo(path).Length > 250 * 1024)
            {
                File.Delete(path);
            }

            File.AppendAllText(path, DateTimeOffset.UtcNow.ToString("O") + " " + safe + Environment.NewLine);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
