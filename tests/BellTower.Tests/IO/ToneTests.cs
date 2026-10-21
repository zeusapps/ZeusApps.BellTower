using System.Text;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.IO;

public sealed class ToneTests
{
    [Theory]
    [InlineData(8, false)]
    [InlineData(16, false)]
    [InlineData(24, false)]
    [InlineData(32, false)]
    [InlineData(24, true)]
    public void PcmAndExtensiblePcmAreNormalizedToPlayable16BitSamples(int bits, bool extensible)
    {
        var frame = bits == 8 ? new byte[] { 0, 128, 255 } : Enumerable.Range(0, 3 * (bits / 8)).Select(index => (byte)(index * 17)).ToArray();
        var wave = Wave(bits, extensible, frame);
        var tone = PcmWave.Decode(wave);
        Assert.Equal((ushort)1, BitConverter.ToUInt16(tone.Wave, 20));
        Assert.Equal((ushort)16, BitConverter.ToUInt16(tone.Wave, 34));
        Assert.Equal(6, BitConverter.ToInt32(tone.Wave, 40));
        Assert.Equal(1, tone.Milliseconds);
        var expected = bits == 8 ? new byte[] { 0, 128, 0, 0, 0, 127 } :
            Enumerable.Range(0, 3).SelectMany(index => frame.Skip(index * bits / 8 + bits / 8 - 2).Take(2)).ToArray();
        Assert.Equal(expected, tone.Wave[44..]);
        Assert.Equal(tone.Wave, PcmWave.Decode(tone.Wave).Wave);
    }

    [Fact]
    public void InvalidTruncatedAndOverlongAudioIsRejectedWithoutPlayback()
    {
        Assert.Throws<InvalidDataException>(() => PcmWave.Decode("not a wave"u8));
        Assert.Throws<InvalidDataException>(() => PcmWave.Decode(Chime.Wave()[..50]));
        var compressed = Chime.Wave();
        compressed[20] = 3;
        Assert.Throws<InvalidDataException>(() => PcmWave.Decode(compressed));
        Assert.Throws<InvalidDataException>(() => PcmWave.Decode(Wave(16, false, new byte[8000 * 31 * 2])));
    }

    [Fact]
    public void SelectionCopiesTheToneAndKeepsMuteAndVoicePreferences()
    {
        using var workspace = new TestWorkspace();
        var source = Path.Combine(workspace.Root, "download.wav");
        File.WriteAllBytes(source, Chime.Wave());
        var configuration = new ConfigStore(workspace.Paths);
        configuration.Update(config => { config.Muted = true; config.Voice = false; });
        var library = new ToneLibrary(workspace.Paths);
        library.Select(source);
        File.Delete(source);
        Assert.Equal(Chime.Wave(), library.Current().Wave);
        var selected = configuration.Read();
        Assert.True(selected.Muted);
        Assert.False(selected.Voice);
        Assert.Equal("download.wav", selected.ToneName);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.Root, "tones", selected.ToneFile!)));
        library.Reset();
        Assert.Null(configuration.Read().ToneFile);
        Assert.Equal(DefaultTones.For(AttentionReason.Done).Wave, library.Current().Wave);
        library.UseClassicChime();
        Assert.Equal(400, library.Current().Milliseconds);
        Assert.True(configuration.Read().Muted);
        Assert.False(configuration.Read().Voice);
    }

    [Fact]
    public void MissingOrInvalidOwnedTonesFallBackToTheBundledEventTone()
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.ToneFile = "../outside.wav");
        Assert.Equal(DefaultTones.For(AttentionReason.Done).Wave, new ToneLibrary(workspace.Paths).Current().Wave);
        new ConfigStore(workspace.Paths).Update(config => config.ToneFile = new string('a', 64) + ".wav");
        Assert.Equal(DefaultTones.For(AttentionReason.Permission).Wave, new ToneLibrary(workspace.Paths).Current(AttentionReason.Permission).Wave);
    }

    [Theory]
    [InlineData(AttentionReason.Permission, 1583)]
    [InlineData(AttentionReason.Question, 3012)]
    [InlineData(AttentionReason.Done, 2156)]
    [InlineData(AttentionReason.Error, 1950)]
    public void CleanInstallationUsesTheBundledToneForEachReason(AttentionReason reason, int milliseconds)
    {
        using var workspace = new TestWorkspace();
        var tone = new ToneLibrary(workspace.Paths).Current(reason);
        Assert.Equal(milliseconds, tone.Milliseconds);
        Assert.Equal((ushort)16, BitConverter.ToUInt16(tone.Wave, 34));
        Assert.Null(new ConfigStore(workspace.Paths).Read().ToneFile);
        Assert.Equal(4, Enum.GetValues<AttentionReason>().Select(value => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(DefaultTones.For(value).Wave))).Distinct().Count());
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(30, false)]
    public void AcceptedEightBitTonesRemainReadableAfterConversion(int seconds, bool accepted)
    {
        using var workspace = new TestWorkspace();
        var source = Path.Combine(workspace.Root, "high-rate.wav");
        File.WriteAllBytes(source, Wave(8, false, new byte[192000 * 2 * seconds], rate: 192000, channels: 2));
        var library = new ToneLibrary(workspace.Paths);
        if (accepted)
        {
            var selected = library.Select(source);
            Assert.Equal(selected.Wave, new ToneLibrary(workspace.Paths).Current().Wave);
            Assert.Equal(seconds * 1000, selected.Milliseconds);
        }
        else
        {
            var error = Assert.Throws<InvalidDataException>(() => library.Select(source));
            Assert.Contains("converted", error.Message);
            Assert.Null(new ConfigStore(workspace.Paths).Read().ToneFile);
        }
    }

    private static byte[] Wave(int bits, bool extensible, byte[] samples, int rate = 8000, int channels = 1)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var formatLength = extensible ? 40 : 16;
        writer.Write("RIFF"u8);
        writer.Write(20 + formatLength + samples.Length + (samples.Length & 1));
        writer.Write("WAVEfmt "u8);
        writer.Write(formatLength);
        writer.Write((ushort)(extensible ? 65534 : 1));
        writer.Write((ushort)channels);
        writer.Write(rate);
        writer.Write(rate * channels * bits / 8);
        writer.Write((ushort)(channels * bits / 8));
        writer.Write((ushort)bits);
        if (extensible)
        {
            writer.Write((ushort)22);
            writer.Write((ushort)bits);
            writer.Write(0);
            writer.Write(new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray());
        }
        writer.Write("data"u8);
        writer.Write(samples.Length);
        writer.Write(samples);
        if ((samples.Length & 1) != 0)
        {
            writer.Write((byte)0);
        }

        return stream.ToArray();
    }
}
