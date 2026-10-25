using System;
using System.IO;
using System.Threading;

// QA-only callback for UpdateTransaction.PayloadRemovalTestCheckpoint.
// Unlike a PowerShell scriptblock delegate, this callback is safe when the
// checkpoint is invoked on the maintenance worker thread.
public static class Qa007FileBarrier
{
    public static Action<string, string> Create(string installationRoot, string payloadVersion, string phase, string directory, int timeoutSeconds)
    {
        var expectedRoot = Path.GetFullPath(installationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var entered = Path.Combine(directory, phase + ".entered");
        var release = Path.Combine(directory, phase + ".release");
        return (root, version) =>
        {
            var actualRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!String.Equals(actualRoot, expectedRoot, StringComparison.OrdinalIgnoreCase) || !String.Equals(version, payloadVersion, StringComparison.Ordinal))
                return;

            using (var stream = new FileStream(entered, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(DateTimeOffset.UtcNow.ToString("O"));
                writer.Flush();
                stream.Flush(true);
            }

            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (!File.Exists(release))
            {
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException("QA007 file barrier release deadline expired.");
                Thread.Sleep(20);
            }
        };
    }
}
