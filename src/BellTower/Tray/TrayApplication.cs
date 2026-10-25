using BellTower.Cli;
using BellTower.Core;
using BellTower.Distribution;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Sessions;
using BellTower.Updates;

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
    private readonly CheckoutCache checkout = new();
    private readonly Navigator navigation;
    private readonly SessionViewFactory views;
    private readonly IdentityResolver identityResolver;
    private readonly IdentityHistoryReader history;
    private readonly TerminalTitlePump titles;
    private readonly NativeNamePump nativeNames;
    private readonly CodexPermissionPump permissions;
    private readonly AlertPolicy policy = new(DateTimeOffset.UtcNow);
    private readonly AudioQueue audio;
    private AppState state = new();
    private AppConfig config = new();
    private DateTimeOffset nextOverview;
    private bool refreshing;
    private bool disposed;
    private readonly Dictionary<string, GitContext> contexts = [];
    private readonly CancellationTokenSource updateCancellation = new();
    private readonly HttpClient updateHttp = ReleaseClient.CreateClient();
    private readonly UpdateDiscovery updates;
    private bool ready;
    public TrayApplication(DataPaths paths, bool silent)
    {
        this.paths = paths;
        store = new(paths);
        attachments = new(paths);
        identities = new(paths);
        configuration = new(paths);
        navigation = new(paths);
        views = new(identities, attachments, navigation);
        identityResolver = new(paths, checkout);
        history = new(paths);
        titles = new(paths, checkout);
        nativeNames = new(paths, checkout);
        permissions = new(paths);
        config = configuration.Read();
        updates = new(paths, new ReleaseClient(updateHttp));
        if (Installation.ForPayload(Environment.ProcessPath!) is not null)
        { _ = Task.Run(() => updates.RunAsync(updateCancellation.Token)); }
        overview.Icon = appIcon;
        audio = new(silent ? new SilentAudioSink() : new WindowsAudioSink(new ToneLibrary(paths)), IsCurrentAlert, () => config);
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Bell Tower v" + ApplicationInfo.Display) { Enabled = false });
        menu.Items.Add("Check for updates", null, async (_, _) => await CheckUpdateAsync(false));
        var installUpdate = new ToolStripMenuItem("Install update");
        installUpdate.Click += async (_, _) => await CheckUpdateAsync(true);
        menu.Items.Add(installUpdate);
        menu.Opening += (_, _) =>
        {
            var offer = updates.Hint();
            installUpdate.Enabled = offer is not null;
            installUpdate.Text = offer is null ? "Install update" : "Install update " + offer.Version;
        };
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
            Text = ApplicationInfo.TrayText,
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
        overview.SaveIdentity += (key, name, change) => new SessionCatalog(paths).Rename(key, name, change);
        overview.ConfigureIdentity += (key, checkoutPath, color, reset) => new SessionCatalog(paths).Configure(key, checkoutPath, color, reset);
        overview.SetNativeSync += (key, enabled) =>
        {
            if (state.Sessions.TryGetValue(key, out var session))
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        NativeNameControl.SetEnabled(paths, session, enabled);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
                    {
                        new LocalLog(paths).Write("native-name-control-failed");
                    }
                });
            }
        };
        overview.UseBellTowerName += key => new SessionCatalog(paths).UseBellTowerName(key);
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
            if (!ready)
            {
                DistributionJson.Write(UpdateTransaction.Presence(paths), new TrayPresence(ProcessProbe.Identify(Environment.ProcessId)!, ApplicationInfo.Version,
                    paths.Root, Environment.GetEnvironmentVariable("BELLTOWER_UPDATE_ACTIVATION"), Capture: 1));
                ready = true;
            }
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

    private async Task CheckUpdateAsync(bool install)
    {
        try
        {
            if (install)
            {
                var start = new System.Diagnostics.ProcessStartInfo(Installation.StableExecutable(Environment.ProcessPath!))
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { "update", "--data-dir", paths.Root })
                { start.ArgumentList.Add(argument); }
                using var process = System.Diagnostics.Process.Start(start) ?? throw new IOException("Update command could not start.");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(updateCancellation.Token);
                if (!disposed)
                { overview.SetStatus((await output) + (await error)); }
            }
            else
            {
                var result = await updates.CheckAsync(true, updateCancellation.Token);
                if (!disposed)
                { overview.SetStatus(result?.Error ?? (updates.Hint() is { } offer ? "Update available: " + offer.Version + ". Use Install update in the tray menu." : "No newer compatible stable release available.")); }
            }
            if (!disposed)
            { overview.Show(); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException or System.ComponentModel.Win32Exception)
        { if (!disposed) { overview.SetStatus("Update could not complete: " + error.Message); overview.Show(); } }
    }

    private async void RefreshOverview(SessionState[] live)
    {
        refreshing = true;
        try
        {
            var details = new List<(string Key, GitContext Git)>();
            foreach (var session in live)
            {
                await Task.Run(() => history.Refresh(session), updateCancellation.Token);
                var git = await identityResolver.RefreshAsync(session, updateCancellation.Token);
                new IdentityPresentationStore(paths).Save(identities.Get(session.Key, session.Directory), git);
                details.Add((session.Key, git));
            }
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
        catch (OperationCanceledException) when (updateCancellation.IsCancellationRequested) { }
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
        try
        {
            // NavigationOutcome is the single decision point: only a confirmed chip-click success hides the timer.
            if (NavigationOutcome.ApplyViewed(store, key, episode, result, hideTimer))
            {
                Tick();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            new LocalLog(paths).Write("viewed-wait-save-failed");
            overview.SetStatus("Session focused; the timer preference could not be saved.");
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
        updateCancellation.Cancel();
        updateHttp.Dispose();
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
