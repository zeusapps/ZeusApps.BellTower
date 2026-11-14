namespace BellTower.Core;

public sealed class AppConfig
{
    public UpdateSettings Updates { get; set; } = new();
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
    public bool Voice { get; set; } = true;
    public bool Muted { get; set; }
    public bool MicrophoneAwareVoice { get; set; } = true;
    public string? ToneFile { get; set; }
    public string? ToneName { get; set; }
    public int AttachmentEvidenceGraceMinutes { get; set; } = 10;
    public ReminderSettings Reminders { get; set; } = new();
    /// <summary>Opt-in: give plain native direct sessions (Claude Code, standalone Codex) the managed tab title and identity palette background. Off by default.</summary>
    public bool NativePresentation { get; set; }
}
