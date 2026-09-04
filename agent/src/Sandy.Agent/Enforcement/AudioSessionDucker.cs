using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sandy.Agent.Enforcement;

internal sealed class AudioSessionDucker : IDisposable
{
    private const float RestoreTolerance = 0.005f;
    private readonly List<DuckedSession> _sessions;
    private bool _restored;

    private AudioSessionDucker(List<DuckedSession> sessions, bool succeeded)
    {
        _sessions = sessions;
        Succeeded = succeeded;
    }

    public bool Succeeded { get; }
    public int DuckedSessionCount => _sessions.Count;

    public static AudioSessionDucker DuckOtherSessions(float multiplier)
    {
        var sessions = new List<DuckedSession>();
        var succeeded = true;
        IMMDeviceEnumerator? deviceEnumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? sessionManager = null;
        IAudioSessionEnumerator? sessionEnumerator = null;

        try
        {
            deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(deviceEnumerator.GetDefaultAudioEndpoint(
                EDataFlow.Render, ERole.Multimedia, out device));

            var interfaceId = typeof(IAudioSessionManager2).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(
                ref interfaceId, ClsContext.All, nint.Zero, out var sessionManagerObject));
            sessionManager = (IAudioSessionManager2)sessionManagerObject;
            Marshal.ThrowExceptionForHR(sessionManager.GetSessionEnumerator(out sessionEnumerator));
            Marshal.ThrowExceptionForHR(sessionEnumerator.GetCount(out var count));

            for (var index = 0; index < count; index++)
                DuckSession(sessionEnumerator, index, multiplier, sessions);
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            Trace.TraceWarning($"Could not duck system audio: {exception.Message}");
            succeeded = false;
            Restore(sessions);
            sessions.Clear();
        }
        finally
        {
            Release(sessionEnumerator);
            Release(sessionManager);
            Release(device);
            Release(deviceEnumerator);
        }

        return new AudioSessionDucker(sessions, succeeded);
    }

    public void Dispose()
    {
        if (_restored)
            return;
        _restored = true;
        Restore(_sessions);
        _sessions.Clear();
    }

    private static void DuckSession(
        IAudioSessionEnumerator sessionEnumerator,
        int index,
        float multiplier,
        ICollection<DuckedSession> sessions)
    {
        IAudioSessionControl? control = null;
        ISimpleAudioVolume? volume = null;

        try
        {
            Marshal.ThrowExceptionForHR(sessionEnumerator.GetSession(index, out control));
            var control2 = (IAudioSessionControl2)control;
            Marshal.ThrowExceptionForHR(control2.GetState(out var state));
            Marshal.ThrowExceptionForHR(control2.GetProcessId(out var processId));
            if (state != AudioSessionState.Active || processId == Environment.ProcessId)
                return;

            volume = (ISimpleAudioVolume)control;
            Marshal.ThrowExceptionForHR(volume.GetMasterVolume(out var originalVolume));
            var duckedVolume = Math.Clamp(originalVolume * multiplier, 0, 1);
            var eventContext = Guid.NewGuid();
            Marshal.ThrowExceptionForHR(volume.SetMasterVolume(duckedVolume, ref eventContext));
            sessions.Add(new DuckedSession(control, originalVolume, duckedVolume));
            volume = null;
            control = null;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            // Audio sessions can disappear or change while they are enumerated.
        }
        finally
        {
            Release(volume);
            Release(control);
        }
    }

    private static void Restore(IEnumerable<DuckedSession> sessions)
    {
        foreach (var session in sessions)
        {
            ISimpleAudioVolume? volume = null;
            try
            {
                volume = (ISimpleAudioVolume)session.Control;
                Marshal.ThrowExceptionForHR(volume.GetMasterVolume(out var currentVolume));
                if (Math.Abs(currentVolume - session.DuckedVolume) <= RestoreTolerance)
                {
                    var eventContext = Guid.NewGuid();
                    Marshal.ThrowExceptionForHR(volume.SetMasterVolume(session.OriginalVolume, ref eventContext));
                }
            }
            catch (Exception exception) when (exception is COMException or InvalidCastException)
            {
                // Do not restore a session that was removed or changed by the user.
            }
            finally
            {
                Release(session.Control);
            }
        }
    }

    private static void Release(object? value)
    {
        try
        {
            if (value is not null && Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }
        catch (InvalidComObjectException)
        {
            // A queried interface can share a runtime wrapper with an interface
            // that has already been released.
        }
    }

    private sealed record DuckedSession(IAudioSessionControl Control, float OriginalVolume, float DuckedVolume);

    private enum EDataFlow
    {
        Render
    }

    private enum ERole
    {
        Console,
        Multimedia
    }

    private enum ClsContext : uint
    {
        All = 0x17
    }

    private enum AudioSessionState
    {
        Inactive,
        Active,
        Expired
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(nint client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(nint client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, ClsContext classContext, nint activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid sessionGuid, uint streamFlags, out IAudioSessionControl sessionControl);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid sessionGuid, uint streamFlags, out ISimpleAudioVolume audioVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
        [PreserveSig] int RegisterSessionNotification(nint sessionNotification);
        [PreserveSig] int UnregisterSessionNotification(nint sessionNotification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string? sessionId, nint duckNotification);
        [PreserveSig] int UnregisterDuckNotification(nint duckNotification);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl sessionControl);
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out AudioSessionState state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingParameter);
        [PreserveSig] int SetGroupingParam(ref Guid groupingParameter, ref Guid eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(nint sessionEvents);
        [PreserveSig] int UnregisterAudioSessionNotification(nint sessionEvents);
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2 : IAudioSessionControl
    {
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionIdentifier);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionInstanceIdentifier);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}
