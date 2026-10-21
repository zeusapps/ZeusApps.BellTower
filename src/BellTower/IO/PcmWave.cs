using System.Buffers.Binary;
using BellTower.Tray;

namespace BellTower.IO;

public static class PcmWave
{
    public const int MaximumBytes = 16 * 1024 * 1024;
    private static readonly Guid PcmSubtype = new("00000001-0000-0010-8000-00aa00389b71");

    public static NotificationTone Load(string file)
    {
        using var stream = File.OpenRead(file);
        if (stream.Length > MaximumBytes || stream.Length < 44)
        {
            throw new InvalidDataException("Choose a WAV notification tone smaller than 16 MB.");
        }
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return Decode(bytes);
    }

    public static NotificationTone Decode(ReadOnlySpan<byte> input)
    {
        if (input.Length < 44 || input.Length > MaximumBytes || !input[..4].SequenceEqual("RIFF"u8) || !input.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("The file is not a supported WAV tone.");
        }
        var declared = (long)BinaryPrimitives.ReadUInt32LittleEndian(input[4..]) + 8;
        if (declared > input.Length || declared < 44)
        {
            throw new InvalidDataException("The WAV file is incomplete.");
        }
        ReadOnlySpan<byte> format = default;
        ReadOnlySpan<byte> data = default;
        for (var offset = 12; offset + 8 <= declared;)
        {
            var size = BinaryPrimitives.ReadUInt32LittleEndian(input[(offset + 4)..]);
            var end = (long)offset + 8 + size;
            if (end > declared)
            {
                throw new InvalidDataException("A WAV chunk is incomplete.");
            }
            var chunk = input.Slice(offset + 8, (int)size);
            if (input.Slice(offset, 4).SequenceEqual("fmt "u8))
            {
                format = chunk;
            }
            if (input.Slice(offset, 4).SequenceEqual("data"u8))
            {
                data = chunk;
            }
            offset = checked((int)end + ((int)size & 1));
        }
        if (format.Length < 16 || data.IsEmpty)
        {
            throw new InvalidDataException("The WAV tone has no audio format or samples.");
        }
        var codec = BinaryPrimitives.ReadUInt16LittleEndian(format);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(format[2..]);
        var rate = BinaryPrimitives.ReadUInt32LittleEndian(format[4..]);
        var alignment = BinaryPrimitives.ReadUInt16LittleEndian(format[12..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(format[14..]);
        var extensiblePcm = codec == 65534 && format.Length >= 40 && new Guid(format.Slice(24, 16)) == PcmSubtype;
        if ((codec != 1 && !extensiblePcm) || channels is not (1 or 2) || bits is not (8 or 16 or 24 or 32) ||
            rate is < 8000 or > 192000 || alignment != channels * bits / 8 || data.Length % alignment != 0)
        {
            throw new InvalidDataException("Choose a mono or stereo PCM WAV tone (8, 16, 24 or 32 bit).");
        }
        var duration = (double)data.Length / (rate * alignment);
        if (duration <= 0 || duration > 30)
        {
            throw new InvalidDataException("Choose a notification tone lasting at most 30 seconds.");
        }
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        var bytesPerSample = bits / 8;
        var outputLength = data.Length / bytesPerSample * 2;
        if (outputLength > MaximumBytes - 44)
        {
            throw new InvalidDataException("The converted WAV tone must be smaller than 16 MB. Choose a shorter clip or a lower sample rate.");
        }
        writer.Write("RIFF"u8);
        writer.Write(36 + outputLength);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write(channels);
        writer.Write(rate);
        writer.Write(rate * channels * 2);
        writer.Write((ushort)(channels * 2));
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(outputLength);
        for (var offset = 0; offset < data.Length; offset += bytesPerSample)
        {
            var sample = bits == 8 ? (short)((data[offset] - 128) << 8) :
                BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset + bytesPerSample - 2, 2));
            writer.Write(sample);
        }
        return new(output.ToArray(), (int)Math.Ceiling(duration * 1000));
    }
}
