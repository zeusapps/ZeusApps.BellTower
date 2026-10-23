using System.Security.Cryptography;

namespace BellTower.Distribution;

public sealed class Installation
{
    public string Root { get; }
    public string Launcher => Path.Combine(Root, "bell-tower.exe");
    public string SelectionPath => Path.Combine(Root, "current.json");
    public Installation(string root)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        EnsurePlainPath(Root);
        EnsurePlainPath(Path.Combine(Root, "installation.json"));
        var manifest = DistributionJson.Read<InstallationManifest>(Path.Combine(Root, "installation.json"), 4096);
        if (manifest.Layout != 1 || manifest.Bootstrap != 1 || !File.Exists(Launcher))
        { throw new InvalidDataException("Unsupported installation. See manual migration instructions."); }
    }
    public VersionSelection Selection()
    {
        EnsurePlainPath(SelectionPath);
        var selection = DistributionJson.Read<VersionSelection>(SelectionPath, 4096);
        _ = Payload(selection.Version);
        if (selection.Previous is not null)
        { _ = ReleaseContract.Parse(selection.Previous); }
        return selection;
    }
    public string Payload(string version)
    {
        _ = ReleaseContract.Parse(version);
        var path = Path.Combine(Root, "versions", version);
        EnsurePlainPath(path);
        EnsurePlainPath(Path.Combine(path, "release.json"));
        var contract = DistributionJson.Read<ReleaseContract>(Path.Combine(path, "release.json"));
        contract.Validate();
        if (contract.Version != version || !File.Exists(Path.Combine(path, "bell-tower.exe")))
        { throw new InvalidDataException("Incomplete version payload."); }
        return path;
    }
    public FileStream Lease(string version, bool exclusive = false)
    {
        _ = ReleaseContract.Parse(version);
        var folder = Path.Combine(Root, "leases");
        EnsurePlainPath(folder);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, version + ".lock");
        EnsurePlainPath(path);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, exclusive ? FileShare.None : FileShare.ReadWrite);
    }
    public static Installation? ForPayload(string executable)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        var parent = Directory.GetParent(directory);
        if (parent?.Name != "versions")
        { return null; }
        var install = new Installation(parent.Parent!.FullName);
        var version = Path.GetFileName(directory);
        if (!install.Payload(version).Equals(directory, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidDataException("Invalid payload path."); }
        return install;
    }
    public static string StableExecutable(string executable) => ForPayload(executable)?.Launcher ?? executable;
    public static void EnsurePlainPath(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            { throw new IOException("Installation/update paths must not traverse reparse points."); }
        }
    }
    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    public static void VerifyFiles(string directory, ReleaseContract contract)
    {
        contract.Validate();
        if (contract.Files is not { Count: > 0 and < 512 } files || !files.ContainsKey("bell-tower.exe") || !files.ContainsKey($"bell-tower-focus-{contract.Version}.vsix") ||
            contract.Capture == 1 && !files.ContainsKey("bell-tower-ingress.exe"))
        { throw new InvalidDataException("Missing payload file inventory."); }
        foreach (var item in files)
        {
            var path = OwnedPath(directory, item.Key);
            EnsurePlainPath(path);
            if (item.Value is null || item.Value.Length != 64 || !Hash(path).Equals(item.Value, StringComparison.OrdinalIgnoreCase))
            { throw new InvalidDataException("Payload file hash mismatch: " + item.Key); }
        }
    }
    public static string OwnedPath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') || relative.Split('/').Any(p => p is "" or "." or ".."))
        { throw new InvalidDataException("Unsafe package path."); }
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidDataException("Package path escapes its root."); }
        return path;
    }
}
