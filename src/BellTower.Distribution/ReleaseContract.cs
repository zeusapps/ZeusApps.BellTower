using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BellTower.Distribution;

public sealed record ReleaseBuild(string Channel, string Commit, bool Dirty, DateTimeOffset BuiltAt, string BaseVersion)
{
    public const string DevChannel = "dev";
    public void Validate(string version)
    {
        if (Channel != DevChannel || !ReleaseContract.IsDevVersion(version) || string.IsNullOrEmpty(Commit) || Commit.Length > 40 || !Commit.All(Uri.IsHexDigit))
        { throw new InvalidDataException("Invalid development build metadata."); }
        if (ReleaseContract.IsDevVersion(BaseVersion))
        { throw new InvalidDataException("A development build must be based on a stable version."); }
        _ = ReleaseContract.Parse(BaseVersion);
    }
}

public sealed record ReleaseContract(string Version, string Runtime = "win-x64", bool SelfContained = true,
    int Layout = 1, int Bootstrap = 1, int State = 1, int RuntimeRecords = 1, int Shell = 1, int Bridge = 1,
    Dictionary<string, string>? Files = null, int Capture = 0, int AttachmentHealth = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReleaseBuild? Build = null)
{
    /// <summary>Development builds use PATCH numbers from this value up; stable releases never do.</summary>
    public const int DevPatchStart = 9000;
    public static System.Version Parse(string version) => !string.IsNullOrEmpty(version) && version.Length <= 64 && Regex.IsMatch(version, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")
        && System.Version.TryParse(version, out var parsed) ? parsed : throw new InvalidDataException("Expected MAJOR.MINOR.PATCH.");
    public static bool IsDevVersion(string version) => Parse(version).Build >= DevPatchStart;
    /// <summary>The stable version that update checks compare against: the base of a dev build, otherwise the version itself.</summary>
    public static System.Version Effective(string version, ReleaseBuild? build)
    {
        var parsed = Parse(version);
        if (parsed.Build < DevPatchStart)
        { return parsed; }
        return build?.BaseVersion is { } baseVersion && Regex.IsMatch(baseVersion, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$") ? Parse(baseVersion) : new System.Version(parsed.Major, parsed.Minor, 0);
    }
    public System.Version EffectiveVersion => Effective(Version, Build);
    public bool IsDev => IsDevVersion(Version);
    public void Validate()
    {
        _ = Parse(Version);
        if (Runtime != "win-x64" || !SelfContained || Layout != 1 || Bootstrap != 1 || State != 1 || RuntimeRecords != 1 || Shell != 1 || Bridge != 1 || Capture is not (0 or 1) || AttachmentHealth is not (0 or 1))
        {
            throw new InvalidDataException("This release needs incompatible installation/data/integration maintenance; selection was not changed.");
        }
        Build?.Validate(Version);
    }
    public string PackageName => $"bell-tower-{Version}-win-x64.zip";
}
