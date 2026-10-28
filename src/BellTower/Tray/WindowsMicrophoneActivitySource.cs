using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using BellTower.Core;

namespace BellTower.Tray;

internal sealed record MicrophoneEndpointProbeResult(bool Complete, int SessionCount, IReadOnlyList<MicrophoneUsageState> States);

internal sealed record MicrophoneProbeResult(
    bool PlatformSupported,
    bool RemoteSession,
    bool EndpointEnumerationComplete,
    int EndpointCount,
    IReadOnlyList<MicrophoneEndpointProbeResult> Endpoints,
    string? FailureReason = null,
    DateTimeOffset ObservedAt = default);

/// <summary>Best-effort snapshot of shared-mode capture streams on active capture endpoints.</summary>
public sealed class WindowsMicrophoneActivitySource : IMicrophoneActivitySource
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan QueryWait = TimeSpan.FromMilliseconds(150);
    private readonly object gate = new();
    private readonly Func<MicrophoneProbeResult> probe;
    private readonly Func<DateTimeOffset> utcNow;
    private Task<MicrophoneProbeResult>? query;
    private MicrophoneActivitySnapshot snapshot = MicrophoneActivitySnapshot.Unknown("No microphone activity snapshot has been collected yet.");
    private DateTimeOffset? snapshotTime;

    public WindowsMicrophoneActivitySource() : this(null, () => DateTimeOffset.UtcNow)
    {
    }

    internal WindowsMicrophoneActivitySource(Func<MicrophoneProbeResult> probe)
        : this(probe, () => DateTimeOffset.UtcNow)
    {
    }

    internal WindowsMicrophoneActivitySource(Func<MicrophoneProbeResult>? probe, Func<DateTimeOffset> utcNow)
    {
        this.utcNow = utcNow;
        this.probe = probe ?? Query;
    }

    public MicrophoneActivitySnapshot GetSnapshot()
    {
        lock (gate)
        {
            var now = utcNow();
            if (snapshotTime is { } collectedAt && now >= collectedAt && now - collectedAt <= CacheDuration &&
                (snapshot.State == MicrophoneUsageState.Unknown || snapshot.IsFresh(now, CacheDuration)))
            {
                return snapshot;
            }

            if (query is { IsCompleted: true })
            {
                try
                {
                    snapshot = Evaluate(query.GetAwaiter().GetResult(), utcNow());
                }
                catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
                {
                    snapshot = MicrophoneActivitySnapshot.Unknown("Windows audio-session query failed.");
                }

                snapshotTime = utcNow();
                query = null;
                return snapshot;
            }

            query ??= Task.Run(probe);
            var pending = query;
            bool completed;
            try
            {
                completed = pending.Wait(QueryWait);
            }
            catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
            {
                snapshot = MicrophoneActivitySnapshot.Unknown("Windows audio-session query failed.");
                snapshotTime = utcNow();
                query = null;
                return snapshot;
            }

            if (completed)
            {
                try
                {
                    snapshot = Evaluate(pending.GetAwaiter().GetResult(), utcNow());
                }
                catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
                {
                    snapshot = MicrophoneActivitySnapshot.Unknown("Windows audio-session query failed.");
                }

                snapshotTime = utcNow();
                query = null;
                return snapshot;
            }

            return MicrophoneActivitySnapshot.Unknown("Windows audio-session query exceeded its bounded wait.");
        }
    }

    private MicrophoneProbeResult Query()
    {
        var observedAt = utcNow();
        return Scan() with { ObservedAt = observedAt };
    }

    private static MicrophoneProbeResult Scan()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
        {
            return Failed("Windows audio-session inspection requires an MTA worker thread.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return Failed("Microphone activity is available only on Windows.");
        }

        if (GetSystemMetrics(SmRemoteSession) != 0)
        {
            return new(true, true, false, 0, [], "Microphone activity is not verified in a Remote Desktop session.");
        }

        var endpointResults = new List<MicrophoneEndpointProbeResult>();
        var endpointCount = 0;
        object? enumeratorObject = null;
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? endpoints = null;
        try
        {
            enumeratorObject = Activator.CreateInstance(Type.GetTypeFromCLSID(ClsidMmDeviceEnumerator, throwOnError: true)!)
                ?? throw new InvalidOperationException("Windows audio endpoint enumerator could not be created.");
            enumerator = (IMMDeviceEnumerator)enumeratorObject;
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceStateActive, out endpoints));
            Marshal.ThrowExceptionForHR(endpoints.GetCount(out var count));
            endpointCount = checked((int)count);
            if (count > 32)
            {
                return new(true, false, true, endpointCount, [], "Active capture endpoint count exceeded the probe limit.");
            }

            for (uint i = 0; i < count; i++)
            {
                var sessionStates = new List<MicrophoneUsageState>();
                var sessionCount = -1;
                IMMDevice? endpoint = null;
                IAudioSessionManager2? manager = null;
                IAudioSessionEnumerator? sessions = null;
                SessionNotification? notification = null;
                var registered = false;
                try
                {
                    Marshal.ThrowExceptionForHR(endpoints.Item(i, out endpoint));
                    var iid = IidAudioSessionManager2;
                    Marshal.ThrowExceptionForHR(endpoint.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var managerObject));
                    manager = (IAudioSessionManager2)managerObject;
                    notification = new SessionNotification();
                    Marshal.ThrowExceptionForHR(manager.RegisterSessionNotification(notification));
                    registered = true;
                    Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out sessions));
                    Marshal.ThrowExceptionForHR(sessions.GetCount(out sessionCount));
                    if (sessionCount > 256)
                    {
                        endpointResults.Add(new(false, sessionCount, sessionStates));
                        return new(true, false, false, endpointCount, endpointResults, "Audio-session count exceeded the probe limit.");
                    }

                    for (int sessionIndex = 0; sessionIndex < sessionCount; sessionIndex++)
                    {
                        IAudioSessionControl? control = null;
                        try
                        {
                            Marshal.ThrowExceptionForHR(sessions.GetSession(sessionIndex, out control));
                            sessionStates.Add(ReadSessionState(control));
                        }
                        finally
                        {
                            Release(control);
                        }
                    }

                    Marshal.ThrowExceptionForHR(manager.UnregisterSessionNotification(notification));
                    registered = false;
                    var callbackStates = notification.ReadStates();
                    if (notification.Overflowed)
                    {
                        endpointResults.Add(new(false, sessionCount + callbackStates.Length, sessionStates));
                        return new(true, false, false, endpointCount, endpointResults, "Audio-session notification buffer exceeded its limit.");
                    }
                    if (sessionCount + callbackStates.Length > 256)
                    {
                        endpointResults.Add(new(false, sessionCount + callbackStates.Length, sessionStates));
                        return new(true, false, false, endpointCount, endpointResults, "Audio-session notification count exceeded the probe limit.");
                    }

                    sessionStates.AddRange(callbackStates);

                    endpointResults.Add(new(true, sessionCount + callbackStates.Length, sessionStates));
                }
                catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
                {
                    endpointResults.Add(new(false, Math.Max(0, sessionCount), sessionStates));
                    return new(true, false, false, endpointCount, endpointResults, "Windows could not verify an active capture endpoint and its sessions.");
                }
                finally
                {
                    if (registered && manager is not null && notification is not null)
                    {
                        Marshal.ThrowExceptionForHR(manager.UnregisterSessionNotification(notification));
                    }

                    Release(sessions);
                    Release(manager);
                    Release(endpoint);
                    notification?.Dispose();
                }
            }

            return new(true, false, true, endpointCount, endpointResults);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            return new(true, false, false, endpointCount, endpointResults, "Windows could not enumerate active capture endpoints.");
        }
        finally
        {
            Release(endpoints);
            Release(enumerator);
            Release(enumeratorObject);
        }
    }

    internal static MicrophoneActivitySnapshot Evaluate(MicrophoneProbeResult result)
        => Evaluate(result, DateTimeOffset.UtcNow);

    internal static MicrophoneActivitySnapshot Evaluate(MicrophoneProbeResult result, DateTimeOffset now)
    {
        if (result.ObservedAt == default || result.ObservedAt > now || now - result.ObservedAt > CacheDuration)
        {
            return MicrophoneActivitySnapshot.Unknown("Windows microphone-use evidence is stale.");
        }

        var states = result.Endpoints.SelectMany(endpoint => endpoint.States).ToArray();
        if (states.Any(state => state == MicrophoneUsageState.Active))
        {
            return new(MicrophoneUsageState.Active, result.ObservedAt, "A shared-mode capture stream is active.");
        }

        if (!result.PlatformSupported || result.RemoteSession || !result.EndpointEnumerationComplete || result.EndpointCount is <= 0 or > 32 ||
            result.Endpoints.Count != result.EndpointCount || states.Any(state => state == MicrophoneUsageState.Unknown) ||
            result.Endpoints.Any(endpoint => !endpoint.Complete || endpoint.SessionCount is < 0 or > 256 || endpoint.States.Count != endpoint.SessionCount))
        {
            return MicrophoneActivitySnapshot.Unknown(result.FailureReason ?? "Windows could not verify all active capture endpoints and sessions.");
        }

        return new(MicrophoneUsageState.Inactive, result.ObservedAt, "No active shared-mode capture stream was observed on active capture endpoints.");
    }

    private static MicrophoneProbeResult Failed(string reason) => new(false, false, false, 0, [], reason);

    internal static MicrophoneUsageState MapNativeSessionState(int state) => state switch
    {
        (int)AudioSessionState.Active => MicrophoneUsageState.Active,
        (int)AudioSessionState.Inactive => MicrophoneUsageState.Inactive,
        _ => MicrophoneUsageState.Unknown
    };

    private static MicrophoneUsageState ReadSessionState(IAudioSessionControl control)
    {
        Marshal.ThrowExceptionForHR(control.GetState(out var state));
        return MapNativeSessionState((int)state);
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    private const int SmRemoteSession = 0x1000;
    private const int ClsctxAll = 23;
    private const uint DeviceStateActive = 0x00000001;
    private static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private enum DataFlow { Render, Capture, All }
    internal enum AudioSessionState { Inactive = 0, Active = 1, Expired = 2 }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(DataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(DataFlow dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice endpoint);
        [PreserveSig] int RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IMMNotificationClient client);
        [PreserveSig] int UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IMMNotificationClient client);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int classContext, IntPtr activationParameters, [MarshalAs(UnmanagedType.Interface)] out object activatedInterface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int createIfNotExists, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int createIfNotExists, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        [PreserveSig] int RegisterSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionNotification notification);
        [PreserveSig] int UnregisterSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionNotification notification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, [MarshalAs(UnmanagedType.Interface)] IAudioVolumeDuckNotification notification);
        [PreserveSig] int UnregisterDuckNotification([MarshalAs(UnmanagedType.Interface)] IAudioVolumeDuckNotification notification);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl control);
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out AudioSessionState state);
        [PreserveSig] int GetDisplayName(out IntPtr value);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr context);
        [PreserveSig] int GetIconPath(out IntPtr value);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, IntPtr context);
        [PreserveSig] int GetGroupingParam(out Guid value);
        [PreserveSig] int SetGroupingParam(ref Guid value, IntPtr context);
        [PreserveSig] int RegisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEvents notification);
        [PreserveSig] int UnregisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEvents notification);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08")]
    private sealed class SessionNotification : IAudioSessionNotification, IDisposable
    {
        private readonly CallbackSessionBuffer<IAudioSessionControl> sessions = new(Release);
        public bool Overflowed => sessions.Overflowed;
        public int OnSessionCreated(IAudioSessionControl newSession)
        {
            sessions.Add(newSession);
            return 0;
        }
        public MicrophoneUsageState[] ReadStates() => sessions.DrainAndMap(ReadSessionState);
        public void Dispose() => sessions.Dispose();
    }

    [ComVisible(true)]
    [Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionNotification
    {
        [PreserveSig] int OnSessionCreated(IAudioSessionControl newSession);
    }

    [ComImport]
    [Guid("C3B284D4-6D39-4359-B3CF-B56DDB3BB39C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioVolumeDuckNotification
    {
        [PreserveSig] int OnVolumeDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, uint countCommunicationSessions);
        [PreserveSig] int OnVolumeUnduckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId);
    }

    [ComImport]
    [Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEvents
    {
        [PreserveSig] int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string newDisplayName, IntPtr eventContext);
        [PreserveSig] int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string newIconPath, IntPtr eventContext);
        [PreserveSig] int OnSimpleVolumeChanged(float volume, [MarshalAs(UnmanagedType.Bool)] bool muted, IntPtr eventContext);
        [PreserveSig] int OnChannelVolumeChanged(uint channelCount, IntPtr newChannelVolumeArray, uint changedChannel, IntPtr eventContext);
        [PreserveSig] int OnGroupingParamChanged(ref Guid newGroupingParam, IntPtr eventContext);
        [PreserveSig] int OnStateChanged(AudioSessionState newState);
        [PreserveSig] int OnSessionDisconnected(int disconnectReason);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out Guid key);
        [PreserveSig] int GetValue(ref Guid key, IntPtr value);
        [PreserveSig] int SetValue(ref Guid key, IntPtr value);
        [PreserveSig] int Commit();
    }

    [ComImport]
    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        [PreserveSig] int OnDefaultDeviceChanged(DataFlow flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? defaultDeviceId);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr key);
    }
}

internal sealed class CallbackSessionBuffer<T>(Action<T> release) : IDisposable
{
    internal const int Capacity = 256;
    private readonly object gate = new();
    private readonly Queue<T> sessions = new();
    private bool disposed;
    public bool Overflowed { get; private set; }
    public int Count { get { lock (gate) { return sessions.Count; } } }

    public void Add(T session)
    {
        lock (gate)
        {
            if (!disposed && sessions.Count < Capacity)
            {
                sessions.Enqueue(session);
                return;
            }
            Overflowed = true;
        }

        release(session);
    }

    public TResult[] DrainAndMap<TResult>(Func<T, TResult> map)
    {
        T[] pending;
        lock (gate)
        {
            disposed = true;
            pending = sessions.ToArray();
            sessions.Clear();
        }

        var results = new List<TResult>(pending.Length);
        Exception? failure = null;
        foreach (var session in pending)
        {
            if (failure is null)
            {
                try
                { results.Add(map(session)); }
                catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) { failure = exception; }
            }

            try
            { release(session); }
            catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) { failure ??= exception; }
        }

        if (failure is not null)
        { ExceptionDispatchInfo.Capture(failure).Throw(); }
        return results.ToArray();
    }

    public void Dispose()
    {
        T[] pending;
        lock (gate)
        {
            disposed = true;
            pending = sessions.ToArray();
            sessions.Clear();
        }

        Exception? failure = null;
        foreach (var session in pending)
        {
            try
            { release(session); }
            catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) { failure ??= exception; }
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
