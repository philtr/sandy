using System.Runtime.InteropServices;

namespace Sandy.Agent.Enforcement;

internal sealed class SystemAudioMute : IDisposable
{
    private IAudioEndpointVolume? _endpoint;
    private bool _restoreToUnmuted;

    public void Mute()
    {
        if (_endpoint is not null)
            return;

        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioEndpointVolume? endpoint = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(
                EDataFlow.Render,
                ERole.Multimedia,
                out device));

            var interfaceId = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(
                ref interfaceId,
                ClsContext.All,
                nint.Zero,
                out var endpointObject));

            endpoint = (IAudioEndpointVolume)endpointObject;
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out var wasMuted));
            _restoreToUnmuted = !wasMuted;

            if (!wasMuted)
            {
                var eventContext = Guid.Empty;
                Marshal.ThrowExceptionForHR(endpoint.SetMute(true, ref eventContext));
            }

            _endpoint = endpoint;
            endpoint = null;
        }
        catch (COMException)
        {
            // Audio devices can disappear. Continue expiration enforcement even
            // when Windows cannot mute the current endpoint.
            _restoreToUnmuted = false;
        }
        finally
        {
            Release(endpoint);
            Release(device);
            Release(enumerator);
        }
    }

    public void Restore()
    {
        if (_endpoint is null)
            return;

        try
        {
            if (_restoreToUnmuted)
            {
                var eventContext = Guid.Empty;
                Marshal.ThrowExceptionForHR(_endpoint.SetMute(false, ref eventContext));
            }
        }
        catch (COMException)
        {
            // The endpoint may have been unplugged while the overlay was active.
        }
        finally
        {
            Release(_endpoint);
            _endpoint = null;
            _restoreToUnmuted = false;
        }
    }

    public void Dispose() => Restore();

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }

    private enum EDataFlow
    {
        Render,
        Capture,
        All
    }

    private enum ERole
    {
        Console,
        Multimedia,
        Communications
    }

    private enum ClsContext : uint
    {
        All = 0x17
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out nint devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(nint client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(nint client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, ClsContext classContext, nint activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);

        [PreserveSig]
        int OpenPropertyStore(uint accessMode, out nint properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(nint notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(nint notify);

        [PreserveSig]
        int GetChannelCount(out uint channelCount);

        [PreserveSig]
        int SetMasterVolumeLevel(float level, ref Guid eventContext);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolumeLevel(out float level);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float level, ref Guid eventContext);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float level);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
    }
}
