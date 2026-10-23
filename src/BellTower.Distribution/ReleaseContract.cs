using System.Text.RegularExpressions;

namespace BellTower.Distribution;

public sealed record ReleaseContract(string Version, string Runtime = "win-x64", bool SelfContained = true,
    int Layout = 1, int Bootstrap = 1, int State = 1, int RuntimeRecords = 1, int Shell = 1, int Bridge = 1,
    Dictionary<string, string>? Files = null, int Capture = 0, int AttachmentHealth = 0)
{
    public static System.Version Parse(string version) => !string.IsNullOrEmpty(version) && version.Length <= 64 && Regex.IsMatch(version, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")
        && System.Version.TryParse(version, out var parsed) ? parsed : throw new InvalidDataException("Expected MAJOR.MINOR.PATCH.");
    public void Validate()
    {
        _ = Parse(Version);
        if (Runtime != "win-x64" || !SelfContained || Layout != 1 || Bootstrap != 1 || State != 1 || RuntimeRecords != 1 || Shell != 1 || Bridge != 1 || Capture is not (0 or 1) || AttachmentHealth is not (0 or 1))
        {
            throw new InvalidDataException("This release needs incompatible installation/data/integration maintenance; selection was not changed.");
        }
    }
    public string PackageName => $"bell-tower-{Version}-win-x64.zip";
}
