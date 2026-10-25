using BellTower.Distribution;

namespace BellTower.Updates;

public sealed record ReleaseOffer(ReleaseContract Contract, string ArchiveHash)
{
    public const string Repository = "zeusapps/ZeusApps.BellTower";
    public const string Releases = "https://github.com/" + Repository + "/releases";
    public string Version => Contract.Version;
    public string Page => Releases + "/tag/v" + Version;
    public string Url => Releases + "/download/v" + Version + "/" + Contract.PackageName;
    public string MetadataUrl => Url[..^4] + ".release.json";
    /// <summary>Published offers never use the reserved dev range; local dev packages are installed by scripts/install-dev.ps1 instead.</summary>
    public void EnsureStable()
    {
        Validate();
        if (Contract.IsDev)
        { throw new InvalidDataException("Development builds are not offered as releases."); }
    }
    public void Validate()
    {
        if (Contract is null || ArchiveHash is null)
        { throw new InvalidDataException("Missing release metadata."); }
        Contract.Validate();
        if (ArchiveHash.Length != 64 || !ArchiveHash.All(Uri.IsHexDigit))
        { throw new InvalidDataException("Invalid release hash."); }
    }
}
