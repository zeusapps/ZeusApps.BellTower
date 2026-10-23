namespace BellTower.Core;

public sealed record LaunchPresentation(string AttachmentId, ProcessIdentity Shell, Palette Palette, DateTimeOffset CreatedAt)
{
    public bool PaletteConsumed { get; init; }
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
