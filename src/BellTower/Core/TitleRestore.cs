namespace BellTower.Core;

/// <summary>
/// What Bell Tower changed on an adopted native shell console, kept until the title is restored: the shell identity the title
/// belongs to, the title found before the first write and the last title Bell Tower wrote. Restoration runs only against this
/// exact shell (process identifier plus creation time) and only while the console still shows the written title.
/// </summary>
public sealed record TitleRestore(string AttachmentId, ProcessIdentity Shell, string Previous, string Written)
{
    /// <summary>True when Bell Tower wrote the session palette to this shell, so the colors are reset to the terminal default on restore. The prior color is never read.</summary>
    public bool Colored { get; init; }
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
