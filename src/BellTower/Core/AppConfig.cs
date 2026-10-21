namespace BellTower.Core;

public sealed class AppConfig
{
    public bool Voice { get; set; } = true;
    public bool Muted { get; set; }
    public string? ToneFile { get; set; }
    public string? ToneName { get; set; }
}
