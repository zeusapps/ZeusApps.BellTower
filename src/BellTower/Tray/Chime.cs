namespace BellTower.Tray;

public static class Chime
{
    public const int Frequency = 880;
    public const int Milliseconds = 400;
    public static byte[] Wave()
    {
        const int rate = 22050;
        var count = rate * Milliseconds / 1000;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + count * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(count * 2);
        for (var i = 0; i < count; i++)
        {
            var envelope = Math.Min(1, Math.Min(i / (rate * .01), (count - i) / (rate * .02)));
            writer.Write((short)(Math.Sin(i * 2 * Math.PI * Frequency / rate) * 7000 * envelope));
        }

        return stream.ToArray();
    }
}
