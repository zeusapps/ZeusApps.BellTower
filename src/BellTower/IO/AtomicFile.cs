using System.Text;

namespace BellTower.IO;

public static class AtomicFile
{
    public static void Write(string path, string content, bool backup = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
                stream.Flush(true);
            }

            if (backup && File.Exists(path))
            {
                File.Replace(temporary, path, path + ".bak", true);
            }
            else
            {
                File.Move(temporary, path, true);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static T? Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var info = new FileInfo(path);
        if (info.Length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException("State file exceeds limit.");
        }

        return Json.Decode<T>(File.ReadAllText(path));
    }
}
