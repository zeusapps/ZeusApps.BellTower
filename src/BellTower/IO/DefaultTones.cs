using BellTower.Core;
using BellTower.Tray;

namespace BellTower.IO;

public static class DefaultTones
{
    private static readonly Dictionary<AttentionReason, Lazy<NotificationTone>> Tones = Enum.GetValues<AttentionReason>()
        .ToDictionary(reason => reason, reason => new Lazy<NotificationTone>(() => Load(reason)));

    public static NotificationTone For(AttentionReason reason) => Tones[reason].Value;

    private static NotificationTone Load(AttentionReason reason)
    {
        var name = reason switch
        {
            AttentionReason.Permission => "permission",
            AttentionReason.Question => "question",
            AttentionReason.Error => "error",
            _ => "done"
        };
        using var stream = typeof(DefaultTones).Assembly.GetManifestResourceStream($"BellTower.Assets.Tones.{name}.wav")
            ?? throw new InvalidOperationException("A bundled notification tone is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return PcmWave.Decode(buffer.ToArray());
    }
}
