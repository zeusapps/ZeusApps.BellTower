using BellTower.Cli;
using BellTower.Core;
using BellTower.Distribution;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Providers;
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
    private readonly IMicrophoneActivitySource microphone = new WindowsMicrophoneActivitySource();
    private readonly IUserIdleSource userIdle;
    private readonly string reminderRunId = Guid.NewGuid().ToString("N");
    private UserIdleSnapshot idleSnapshot = UserIdleSnapshot.Unknown("No input inactivity observation yet.", DateTimeOffset.UtcNow);
    // Isolated for deterministic reminder playback checks; production keeps UTC wall-clock behavior.
    internal Func<DateTimeOffset> ReminderValidityUtcNow { get; set; } = static () => DateTimeOffset.UtcNow;
    private readonly object idleGate = new();
    private AppState state = new();
    private AppConfig config = new();
    private DateTimeOffset nextOverview;
    private bool refreshing;
    private readonly RefreshHealth refreshHealth;
    private bool disposed;
    private readonly Dictionary<string, GitContext> contexts = [];
    private readonly CancellationTokenSource updateCancellation = new();
    private readonly HttpClient updateHttp = ReleaseClient.CreateClient();
    private readonly UpdateDiscovery updates;
    private bool ready;
    public TrayApplication(DataPaths paths, bool silent, IUserIdleSource? userIdle = null)
    {
        this.paths = paths;
        refreshHealth = new(paths);
        this.userIdle = userIdle ?? new WindowsUserIdleSource();
        store = new(paths);
        attachments = new(paths);
        identities = new(paths);
        configuration = new(paths);
        navigation = new(paths);
        views = new(identities, attachments, navigation);
        identityResolver = new(paths, checkout);
        history = new(paths);
        titles = new(paths, checkout);
        // One runtime opener: both services use the same access selection and share the shared-backend backoff.
        var codexRuntime = new CodexRuntimeOpener(paths);
        nativeNames = new(paths, checkout, opener: codexRuntime);
        permissions = new(paths, opener: codexRuntime);
        config = configuration.Read();
        updates = new(paths, new ReleaseClient(updateHttp));
        if (Installation.ForPayload(Environment.ProcessPath!) is not null)
        { _ = Task.Run(() => updates.RunAsync(updateCancellation.Token)); }
        overview.Icon = appIcon;
        audio = new(silent ? new SilentAudioSink() : new WindowsAudioSink(new ToneLibrary(paths)), IsCurrentAlert, () => config, microphone);
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
        var nativePresentation = new ToolStripMenuItem("Title and color native sessions")
        {
            CheckOnClick = true,
            ToolTipText = "On by default; switch it off here. Applies to running sessions without a restart and lets a click select the Windows Terminal tab. Applies to plain claude and codex --no-daemon (not plain default codex): the session title and palette color. Turning it off restores the previous title and resets colors to the terminal default."
        };
        nativePresentation.Click += (_, _) =>
        {
            configuration.Update(c => c.NativePresentation = nativePresentation.Checked);
            config = configuration.Read();
        };
        var microphoneAwareVoice = new ToolStripMenuItem("Suppress voice while microphone is active") { CheckOnClick = true };
        microphoneAwareVoice.Click += (_, _) => configuration.Update(c => c.MicrophoneAwareVoice = microphoneAwareVoice.Checked);
        var reminderMenu = new ToolStripMenuItem("Attention reminders");
        var remindersEnabled = new ToolStripMenuItem("Enabled") { CheckOnClick = true };
        remindersEnabled.Click += (_, _) =>
        {
            configuration.Update(c => c.Reminders.Enabled = remindersEnabled.Checked);
            config = configuration.Read();
        };
        var reminderSettings = new ToolStripMenuItem("Settings…");
        reminderSettings.Click += (_, _) =>
        {
            using var settings = new ReminderSettingsForm(configuration);
            settings.ShowDialog(overview);
            config = configuration.Read();
        };
        reminderMenu.DropDownItems.AddRange([remindersEnabled, reminderSettings]);
        menu.Items.AddRange([mute, voice, microphoneAwareVoice, nativePresentation, reminderMenu]);
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
            microphoneAwareVoice.Checked = config.MicrophoneAwareVoice;
            nativePresentation.Checked = config.NativePresentation;
            remindersEnabled.Checked = config.Reminders.Enabled;
            microphoneAwareVoice.ToolTipText = microphone.GetSnapshot().Reason;
            tones.Text = "Notification tone: " + (config.ToneName ?? "Event tones");
        };
        menu.Items.Add("Test sound", null, (_, _) =>
        {
            audio.Enqueue(new("test", Guid.NewGuid().ToString("N"), "Bell Tower, test sound", true));
        });
        menu.Items.Add(CreateExitMenuItem(paths, ExitThread));
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
        strip.Acknowledge += key =>
        {
            store.Acknowledge(key);
            if (state.Sessions.TryGetValue(key, out var acknowledged))
            {
                acknowledged.AcknowledgedEpisode = acknowledged.Episode;
            }
        };
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

    internal static ToolStripMenuItem CreateExitMenuItem(DataPaths paths, Action exitThread)
    {
        var item = new ToolStripMenuItem("Exit");
        item.Click += (_, _) =>
        {
            // The user's own stop: hooks leave the tray stopped until it is started by hand or the user signs in again.
            BellTower.Protocol.HookTrayStart.RecordUserStop(paths.Root);
            exitThread();
        };
        return item;
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
            lock (idleGate)
            {
                idleSnapshot = userIdle.GetSnapshot();
            }
            var live = state.Sessions.Values.Where(s => !s.Ended).ToArray();
            permissions.Update(live);
            permissions.Apply(live, now);
            ClaudeConfirmationSignal.Apply(live);
            strip.UpdateChips(views.Chips(live, now, contexts));
            foreach (var s in policy.Collect(live, now, config.Muted))
            {
                var identity = identities.Get(s.Key, s.Directory);
                audio.Enqueue(new(s.Key, s.Episode!, AlertPolicy.Phrase(s, identity), Reason: s.Reason!.Value));
            }

            foreach (var reminder in store.AdvanceReminders(live, now, idleSnapshot.IdleFor, config.Reminders, reminderRunId))
            {
                var session = live.First(value => value.Key == reminder.Key && value.Episode == reminder.Episode);
                var identity = identities.Get(session.Key, session.Directory);
                audio.Enqueue(new(session.Key, reminder.Episode, "Still waiting: " + AlertPolicy.Phrase(session, identity),
                    Reason: reminder.Reason, Kind: AudioNotificationKind.Reminder, ReminderOrdinal: reminder.Ordinal));
            }

            if (now >= nextOverview && !refreshing)
            {
                nextOverview = now.AddSeconds(2);
                titles.Update(live);
                nativeNames.Update(live);
                RefreshOverview(live);
            }
            refreshHealth.Succeeded();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            refreshHealth.Failed(e);
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

    private bool IsCurrentAlert(AudioItem item)
    {
        var now = item.Kind == AudioNotificationKind.Alert ? DateTimeOffset.UtcNow : ReminderValidityUtcNow();
        if (!state.Sessions.TryGetValue(item.Key, out var session))
        {
            return false;
        }

        if (item.Kind == AudioNotificationKind.Alert)
        {
            return session.Episode == item.Episode && session.RequiresAttention(now) && session.NotificationDue <= now;
        }

        var idle = CurrentIdle();
        // The bounded fresh probe may complete after the initial session check timestamp.
        now = ReminderValidityUtcNow();
        if (!config.Reminders.Enabled || item.ReminderOrdinal > config.Reminders.MaxPerEpisode || !idle.IsFresh(now, TimeSpan.FromMilliseconds(500)) || idle.IdleFor is not { } idleFor ||
            idleFor < TimeSpan.FromMinutes(config.Reminders.IdleThresholdMinutes) || session.AttachmentHealth != "verified-live" ||
            session.Episode != item.Episode || session.Reason != item.Reason || !session.RequiresAttention(now))
        {
            return false;
        }

        return session.AcknowledgedEpisode != item.Episode;
    }

    private UserIdleSnapshot CurrentIdle()
    {
        lock (idleGate)
        {
            // A positive sample must come from a bounded probe started for this playback check;
            // neither the tray cache nor an older background poll can authorize queued work.
            idleSnapshot = userIdle.GetFreshSnapshot();
            return idleSnapshot;
        }
    }

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
        if (NavigationOutcome.NeedsNotice(result))
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
