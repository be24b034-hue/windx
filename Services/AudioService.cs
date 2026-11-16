using System.Windows.Threading;
using NAudio.CoreAudioApi;

namespace WindX.Services;

/// <summary>System volume + live output level (drives the visualizer) for the default speaker.</summary>
public sealed class AudioService : IDisposable
{
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private string? _deviceId;

    public event Action? VolumeChanged;

    public float Volume { get; private set; }
    public bool Muted { get; private set; }
    public bool Available => _device != null;

    public AudioService() => Acquire();

    /// <summary>Re-binds if the default output device changed (headphones plugged in, etc.).</summary>
    public void Acquire()
    {
        try
        {
            if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
            {
                Release();
                return;
            }

            var dev = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (dev.ID == _deviceId)
            {
                dev.Dispose();
                return;
            }

            Release();
            _device = dev;
            _deviceId = dev.ID;
            Volume = dev.AudioEndpointVolume.MasterVolumeLevelScalar;
            Muted = dev.AudioEndpointVolume.Mute;
            dev.AudioEndpointVolume.OnVolumeNotification += OnVolume;
        }
        catch
        {
            Release();
        }
    }

    private void OnVolume(AudioVolumeNotificationData data)
    {
        float vol = data.MasterVolume;
        bool muted = data.Muted;
        _ui.BeginInvoke(() =>
        {
            if (Math.Abs(vol - Volume) < 0.0005f && muted == Muted) return;
            Volume = vol;
            Muted = muted;
            VolumeChanged?.Invoke();
        });
    }

    /// <summary>Current output loudness, 0..1.</summary>
    public float Peak
    {
        get
        {
            try { return _device?.AudioMeterInformation.MasterPeakValue ?? 0f; }
            catch { return 0f; }
        }
    }

    public void Nudge(float delta)
    {
        try
        {
            if (_device == null) return;
            var v = _device.AudioEndpointVolume;
            v.MasterVolumeLevelScalar = Math.Clamp(v.MasterVolumeLevelScalar + delta, 0f, 1f);
        }
        catch { }
    }

    public void SetVolume(float value)
    {
        try
        {
            if (_device != null) _device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
        }
        catch { }
    }

    public void ToggleMute()
    {
        try
        {
            if (_device != null) _device.AudioEndpointVolume.Mute = !_device.AudioEndpointVolume.Mute;
        }
        catch { }
    }

    private void Release()
    {
        try
        {
            if (_device != null)
            {
                _device.AudioEndpointVolume.OnVolumeNotification -= OnVolume;
                _device.Dispose();
            }
        }
        catch { }
        _device = null;
        _deviceId = null;
    }

    public void Dispose()
    {
        Release();
        _enumerator.Dispose();
    }
}
