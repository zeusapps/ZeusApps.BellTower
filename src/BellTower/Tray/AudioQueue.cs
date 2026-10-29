using System.Collections.Concurrent;
using BellTower.Core;

namespace BellTower.Tray;

public sealed class AudioQueue : IDisposable
{
    private readonly ConcurrentQueue<AudioItem> queue = new();
    private readonly AutoResetEvent wake = new(false);
    private readonly Thread worker;
    private readonly IAudioSink sink;
    private readonly Func<AudioItem, bool> valid;
    private readonly Func<AppConfig> config;
    private readonly IMicrophoneActivitySource microphone;
    private volatile bool stopped;
    public AudioQueue(IAudioSink sink, Func<AudioItem, bool> valid, Func<AppConfig> config, IMicrophoneActivitySource? microphone = null)
    {
        this.sink = sink;
        this.valid = valid;
        this.config = config;
        this.microphone = microphone ?? new UnavailableMicrophoneActivitySource();
        worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "Bell Tower audio"
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    public void Enqueue(AudioItem item)
    {
        queue.Enqueue(item);
        wake.Set();
    }

    public static string? SelectPhrase(IReadOnlyList<AudioItem> items, bool voice) => !voice ? null :
        items.Count > 2 ? $"{items.Count} sessions {(items.All(item => item.Kind == AudioNotificationKind.Reminder) ? "still waiting" : "waiting")}" : string.Join(". ", items.Select(item => item.Phrase));
    private void Run()
    {
        while (!stopped)
        {
            wake.WaitOne(100);
            if (stopped)
            {
                break;
            }

            var items = new List<AudioItem>();
            while (queue.TryDequeue(out var item))
            {
                if (item.Test || valid(item))
                {
                    items.Add(item);
                }
            }

            if (items.Count == 0 || config().Muted)
            {
                continue;
            }

            if (items.Count > 2)
            {
                Play(items, SelectPhrase(items, SpeechAllowed(config())));
            }
            else
            {
                foreach (var item in items)
                {
                    if (item.Test || valid(item))
                    {
                        Play([item], SpeechAllowed(config()) ? item.Phrase : null);
                    }
                }
            }
        }

        sink.Dispose();
    }

    private void Play(List<AudioItem> items, string? phrase)
    {
        try
        {
            bool Cancelled() => stopped || config().Muted || !items.Any(item => item.Test || valid(item));
            bool SpeechCancelled() => !SpeechAllowed(config()) || items.Any(item => !item.Test && !valid(item));
            // Re-evaluate after dequeue and immediately before entering the sink. Suppressed phrases
            // are discarded with this queue batch and are never retained for later playback.
            sink.Play(SpeechAllowed(config()) ? phrase : null, items.Max(item => item.Reason), Cancelled, SpeechCancelled);
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private bool SpeechAllowed(AppConfig current)
    {
        if (!current.Voice)
        {
            return false;
        }

        if (!current.MicrophoneAwareVoice)
        {
            return true;
        }

        try
        {
            var snapshot = microphone.GetSnapshot();
            return snapshot.State == MicrophoneUsageState.Inactive &&
                snapshot.IsFresh(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            return false;
        }
    }

    public void Dispose()
    {
        stopped = true;
        wake.Set();
        if (worker.Join(3000))
        {
            wake.Dispose();
        }
    }
}
