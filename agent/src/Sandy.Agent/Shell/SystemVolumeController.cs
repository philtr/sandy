using System.Runtime.InteropServices;

namespace Sandy.Agent.Shell;

public sealed class SystemVolumeController : IDisposable
{
    private IAudioEndpointVolume? _endpoint;

    public SystemVolumeController()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out var device));
            try
            {
                var interfaceId = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref interfaceId, 0x17, nint.Zero, out var endpoint));
                _endpoint = (IAudioEndpointVolume)endpoint;
            }
            finally
            {
                Marshal.FinalReleaseComObject(device);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(enumerator);
        }
    }

    public float Volume
    {
        get
        {
            EnsureEndpoint();
            Marshal.ThrowExceptionForHR(_endpoint!.GetMasterVolumeLevelScalar(out var value));
            return value;
        }
        set
        {
            EnsureEndpoint();
            var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(_endpoint!.SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), ref context));
        }
    }

    public bool Muted
    {
        get
        {
            EnsureEndpoint();
            Marshal.ThrowExceptionForHR(_endpoint!.GetMute(out var value));
            return value;
        }
        set
        {
            EnsureEndpoint();
            var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(_endpoint!.SetMute(value, ref context));
        }
    }

    public void Dispose()
    {
        if (_endpoint is not null)
        {
            Marshal.FinalReleaseComObject(_endpoint);
            _endpoint = null;
        }
    }

    private void EnsureEndpoint()
    {
        if (_endpoint is null)
            throw new InvalidOperationException("No audio output is available.");
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8B3A-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(nint callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(nint callback);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, uint classContext, nint activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(nint notify);
        [PreserveSig] int UnregisterControlChangeNotify(nint notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}
