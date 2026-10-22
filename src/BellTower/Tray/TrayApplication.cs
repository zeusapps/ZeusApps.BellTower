using BellTower.Cli;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Sessions;

namespace BellTower.Tray;

public sealed class TrayApplication : ApplicationContext
{
    private readonly DataPaths paths;
    private readonly StripForm strip = new();
    private readonly OverviewForm overview = new();
    private readonly NotifyIcon tray;
    private readonly Icon appIcon = AppIcon.Load();
    private readonly System.Windows.Forms.Timer timer = new()
    {
        Interval = 250
    };
    private readonly StateStore store;
    private readonly AttachmentStore attachments;
    private readonly IdentityStore identities;
    private readonly ConfigStore configuration;
    private readonly GitReader git = new();
    private readonly Navigator navigation;
    private readonly SessionViewFactory views;
    private readonly TerminalTitlePump titles;
    private readonly CodexNamePump nativeNames;
    private readonly CodexPermissionPump permissions;
    private readonly AlertPolicy policy = new(DateTimeOffset.UtcNow);
    private readonly AudioQueue audio;
    private AppState state = new();
    private AppConfig config = new();
    private DateTimeOffset nextOverview;
    private bool refreshing;
    private bool disposed;
    private readonly Dictionary<string, GitContext> contexts = [];
    public TrayApplication(DataPaths paths, bool silent)
    {
        this.paths = paths;
        store = new(paths);
        attachments = new(paths);
        identities = new(paths);
        configuration = new(paths);
        navigation = new(paths);
        views = new(identities, attachments, navigation);
        titles = new(paths);
        nativeNames = new(paths);
        permissions = new(paths);
        config = configuration.Read();
        overview.Icon = appIcon;
        audio = new(silent ? new SilentAudioSink() : new WindowsAudioSink(new ToneLibrary(paths)), IsCurrentAlert, () => config);
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Bell Tower v" + ApplicationInfo.Version) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sessions", null, (_, _) =>
        {
            overview.Show();
            overview.Activate();
        });
        menu.Items.Add(new ToolStripSeparator());
        var mute = new ToolStripMenuItem("Mute")
        {
            CheckOnClick = true
        };
        mute.Click += (_, _) => configuration.Update(c => c.Muted = mute.Checked);
        var voice = new ToolStripMenuItem("Voice")
        {
            CheckOnClick = true
        };
        voice.Click += (_, _) => configuration.Update(c => c.Voice = voice.Checked);
        menu.Items.AddRange([mute, voice]);
        var tones = new ToolStripMenuItem("Notification tone");
        tones.DropDownItems.Add("Default event tones", null, (_, _) => new ToneLibrary(paths).Reset());
        tones.DropDownItems.Add("Classic chime", null, (_, _) => new ToneLibrary(paths).UseClassicChime());
        tones.DropDownItems.Add("Choose WAV…", null, (_, _) =>
        {
            using var picker = new OpenFileDialog { Title = "Choose notification tone", Filter = "WAV tones (*.wav)|*.wav", CheckFileExists = true };
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "tones");
            if (Directory.Exists(downloads))
            {
                picker.InitialDirectory = downloads;
            }
            if (picker.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            try
            {
                new ToneLibrary(paths).Select(picker.FileName);
                overview.SetStatus("Notification tone selected. Use Test sound when ready to listen.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                overview.SetStatus("The notification tone could not be selected: " + exception.Message);
                overview.Show();
            }
        });
        menu.Items.Add(tones);
        menu.Opening += (_, _) =>
        {
            mute.Checked = config.Muted;
            voice.Checked = config.Voice;
            tones.Text = "Notification tone: " + (config.ToneName ?? "Event tones");
        };
        menu.Items.Add("Test sound", null, (_, _) =>
        {
            audio.Enqueue(new("test", Guid.NewGuid().ToString("N"), "Bell Tower, test sound", true));
        });
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        tray = new NotifyIcon
        {
            Icon = appIcon,
            Text = "Bell Tower",
            Visible = true,
            ContextMenuStrip = menu
        };
        tray.DoubleClick += (_, _) =>
        {
            overview.Show();
            overview.Activate();
        };
        strip.Acknowledge += key => store.Acknowledge(key);
        strip.Navigate += key => Navigate(key, hideTimer: true);
        overview.Navigate += key => Navigate(key, hideTimer: false);
        overview.SaveIdentity += (key, name, change, role) => new SessionCatalog(paths).Rename(key, name, change, role);
        overview.SetNativeSync += (key, enabled) =>
        {
            if (state.Sessions.TryGetValue(key, out var session))
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        new CodexNameService(paths).SetEnabled(session, enabled);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                    {
                        new LocalLog(paths).Write("native-name-control-failed");
                    }
                });
            }
        };
        var position = AtomicFile.Read<StripPosition>(paths.Position);
        strip.Location = position is null ? new Point(30, 30) : new Point(position.X, position.Y);
        strip.PositionChanged += p => AtomicFile.Write(paths.Position, Json.Encode(p));
        _ = strip.Handle;
        strip.Clamp();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    private void Tick()
    {
        try
        {
            if (File.Exists(Path.Combine(paths.Root, "exit.request")))
            {
                File.Delete(Path.Combine(paths.Root, "exit.request"));
                ExitThread();
                return;
            }

            if (File.Exists(Path.Combine(paths.Root, "sound.request")))
            {
                File.Delete(Path.Combine(paths.Root, "sound.request"));
                audio.Enqueue(new("test", Guid.NewGuid().ToString("N"), "Bell Tower, test sound", true));
            }

            config = configuration.Read();
            state = store.Drain();
            var now = DateTimeOffset.UtcNow;
            var live = state.Sessions.Values.Where(s => !s.Ended).ToArray();
            permissions.Update(live);
            permissions.Apply(live, now);
            strip.UpdateChips(views.Chips(live, now, contexts));
            foreach (var s in policy.Collect(live, now, config.Muted))
            {
                var identity = identities.Get(s.Key, s.Directory);
                audio.Enqueue(new(s.Key, s.Episode!, AlertPolicy.Phrase(s, identity), Reason: s.Reason!.Value));
            }

            if (now >= nextOverview && !refreshing)
            {
                nextOverview = now.AddSeconds(2);
                titles.Update(live);
                nativeNames.Update(live);
                RefreshOverview(live);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            new LocalLog(paths).Write("tray-refresh-failed");
            overview.SetStatus("Local state could not be refreshed. Check diagnostics.");
        }
    }

    private async void RefreshOverview(SessionState[] live)
    {
        refreshing = true;
        try
        {
            var details = await Task.Run(() => live.Select(s => (s.Key, Git: git.Read(s.Directory))).ToArray());
            if (disposed)
            {
                return;
            }

            foreach (var entry in details)
            {
                contexts[entry.Key] = entry.Git;
            }

            overview.RefreshRows(views.Overview(live, contexts), DateTimeOffset.UtcNow);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            new LocalLog(paths).Write("overview-refresh-failed");
        }
        finally
        {
            refreshing = false;
        }
    }

    private bool IsCurrentAlert(AudioItem item) => state.Sessions.TryGetValue(item.Key, out var session) &&
        session.Episode == item.Episode && session.RequiresAttention(DateTimeOffset.UtcNow) &&
        session.NotificationDue <= DateTimeOffset.UtcNow;

    private async void Navigate(string key, bool hideTimer)
    {
        if (!state.Sessions.TryGetValue(key, out var s))
        {
            return;
        }

        var episode = s.Episode;
        var result = await navigation.FocusAsync(attachments.Get(s.AttachmentId));
        if (disposed)
        {
            return;
        }

        overview.SetStatus(result.Message);
        if (result.Success && hideTimer && episode is not null)
        {
            try
            {
                store.MarkViewed(key, episode);
                Tick();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                new LocalLog(paths).Write("viewed-wait-save-failed");
                overview.SetStatus("Session focused; the timer preference could not be saved.");
            }
        }
        if (!result.Success)
        {
            tray.ShowBalloonTip(3000, "Bell Tower", result.Message, ToolTipIcon.Info);
        }
    }

    private void DisplayChanged(object? sender, EventArgs e)
    {
        if (!disposed && strip.IsHandleCreated)
        {
            strip.BeginInvoke(() => strip.Clamp());
        }
    }

    protected override void ExitThreadCore()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        audio.Dispose();
        titles.Dispose();
        nativeNames.Dispose();
        permissions.Dispose();
        tray.Visible = false;
        tray.Dispose();
        strip.Dispose();
        overview.Dispose();
        appIcon.Dispose();
        timer.Dispose();
        base.ExitThreadCore();
    }
}
