using System.Media;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Tray;

public sealed class WindowsAudioSink(ToneLibrary tones) : IAudioSink
{
    public void Play(string? phrase, AttentionReason reason, Func<bool> cancelled)
    {
        if (cancelled())
        {
            return;
        }

        var tone = tones.Current(reason);
        using (var stream = new MemoryStream(tone.Wave))
        using (var player = new SoundPlayer(stream))
        {
            player.Load();
            player.Play();
            for (var elapsed = 0; elapsed < tone.Milliseconds; elapsed += 20)
            {
                if (cancelled())
                {
                    player.Stop();
                    return;
                }

                Thread.Sleep(20);
            }

            player.Stop();
        }

        if (phrase is null || cancelled())
        {
            return;
        }

        dynamic? voice = null;
        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (type is null)
            {
                return;
            }

            voice = Activator.CreateInstance(type);
            if (voice is null)
            {
                return;
            }

            dynamic voices = voice.GetVoices("", "");
            for (var i = 0; i < (int)voices.Count; i++)
            {
                dynamic token = voices.Item(i);
                if (((string)token.GetDescription()).Contains("Zira", StringComparison.OrdinalIgnoreCase))
                {
                    voice.Voice = token;
                    break;
                }
            }

            voice.Speak(phrase, 1);
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!(bool)voice.WaitUntilDone(30))
            {
                if (cancelled() || DateTime.UtcNow > deadline)
                {
                    voice.Speak("", 3);
                    break;
                }
            }
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
        }
        finally
        {
            if (voice is not null)
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(voice);
            }
        }
    }

    public void Dispose()
    {
    }
}
