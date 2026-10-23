namespace BellTower.Protocol;

public static class CaptureSpool
{
    public static string Write(string dataDirectory, CaptureEnvelope envelope)
    {
        var bytes = CaptureProtocol.Encode(envelope);
        var inbox = Path.Combine(dataDirectory, CaptureProtocol.InboxName);
        Directory.CreateDirectory(inbox);
        var path = Path.Combine(inbox, envelope.IngressAt.UtcTicks.ToString("D19") + "-" + envelope.Id + ".json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path);
            return path;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static string FailureCategory(Exception exception) => exception switch
    {
        System.Text.Json.JsonException => "json",
        System.Text.DecoderFallbackException => "encoding",
        InvalidDataException => "contract-or-limit",
        UnauthorizedAccessException => "access",
        IOException => "io",
        ArgumentException => "arguments",
        _ => "internal"
    };

    public static void RecordRejection(string? dataDirectory, Exception exception)
    {
        if (dataDirectory is null)
        {
            return;
        }
        try
        {
            // One small atomic slot per category bounds diagnostic storage without locks.
            var directory = Path.Combine(dataDirectory, "capture-health");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FailureCategory(exception) + ".txt");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, DateTimeOffset.UtcNow.ToString("O"));
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
    }
}
