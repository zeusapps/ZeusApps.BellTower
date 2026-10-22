using System.Diagnostics;
using System.IO.Compression;
using BellTower.Distribution;

namespace BellTower.Updates;

public static class ReleasePackage
{
    public const long MaximumBytes = 350L * 1024 * 1024;
    public static string Extract(string archivePath, string destination, ReleaseOffer offer)
    {
        offer.Validate();
        if (new FileInfo(archivePath).Length > MaximumBytes || Installation.Hash(archivePath) != offer.ArchiveHash.ToLowerInvariant())
        { throw new InvalidDataException("Release package checksum mismatch."); }
        var checksum = File.ReadAllText(archivePath + ".sha256").Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (checksum.Length != 2 || checksum[1] != offer.Contract.PackageName || !checksum[0].Equals(offer.ArchiveHash, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidDataException("Checksum file mismatch."); }
        Installation.EnsurePlainPath(destination);
        Directory.CreateDirectory(destination);
        if (Directory.EnumerateFileSystemEntries(destination).Any())
        { throw new IOException("Staging must be empty."); }
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count is < 6 or > 512)
        { throw new InvalidDataException("Unexpected archive entry count."); }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long size = 0;
        var prefix = "versions/" + offer.Version + "/";
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            _ = Installation.OwnedPath(destination, name.TrimEnd('/'));
            if (name.EndsWith('/'))
            { continue; }
            if (!names.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
            { throw new InvalidDataException("Unsafe archive entry."); }
            var relative = name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : null;
            if (name is not ("bell-tower.exe" or "installation.json" or "current.json") &&
                (relative is null || relative != "release.json" && offer.Contract.Files?.ContainsKey(relative) != true))
            { throw new InvalidDataException("Unexpected archive file: " + name); }
            size = checked(size + entry.Length);
            if (entry.Length < 0 || size > 900L * 1024 * 1024)
            { throw new InvalidDataException("Expanded release exceeds limit."); }
            var path = Installation.OwnedPath(destination, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path);
        }
        var installation = new Installation(destination);
        if (installation.Selection().Version != offer.Version)
        { throw new InvalidDataException("Archive selection/version mismatch."); }
        var payload = installation.Payload(offer.Version);
        var contract = DistributionJson.Read<ReleaseContract>(Path.Combine(payload, "release.json"));
        if (System.Text.Json.JsonSerializer.Serialize(contract, DistributionJson.Options) != System.Text.Json.JsonSerializer.Serialize(offer.Contract, DistributionJson.Options))
        { throw new InvalidDataException("Release sidecar and payload disagree."); }
        Installation.VerifyFiles(payload, contract);
        return payload;
    }
    public static void Probe(string payload, string version)
    {
        var start = new ProcessStartInfo(Path.Combine(payload, "bell-tower.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--distribution-info");
        using var process = Process.Start(start) ?? throw new IOException("Release probe could not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(20000))
        { process.Kill(); throw new IOException("Release probe timed out."); }
        var contract = System.Text.Json.JsonSerializer.Deserialize<ReleaseContract>(output.GetAwaiter().GetResult(), DistributionJson.Options) ?? throw new InvalidDataException("Invalid release probe.");
        contract.Validate();
        if (process.ExitCode != 0 || contract.Version != version)
        { throw new InvalidDataException("Release probe version mismatch: " + error.GetAwaiter().GetResult()); }
    }
}
