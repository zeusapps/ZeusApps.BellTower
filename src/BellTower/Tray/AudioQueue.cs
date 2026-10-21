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
    private volatile bool stopped;
    public AudioQueue(IAudioSink sink, Func<AudioItem, bool> valid, Func<AppConfig> config)
    {
        this.sink = sink;
        this.valid = valid;
        this.config = config;
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
        items.Count > 2 ? $"{items.Count} sessions waiting" : string.Join(". ", items.Select(item => item.Phrase));
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
                Play(items, SelectPhrase(items, config().Voice));
            }
            else
            {
                foreach (var item in items)
                {
                    if (item.Test || valid(item))
                    {
                        Play([item], config().Voice ? item.Phrase : null);
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
            sink.Play(phrase, items.Max(item => item.Reason), () => stopped || config().Muted || !items.Any(item => item.Test || valid(item)));
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
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
