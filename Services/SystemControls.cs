using System.Management;
using System.Windows.Threading;
using Windows.Devices.Radios;

namespace WindX.Services;

/// <summary>Laptop screen brightness through WMI. Loaded lazily and always off the UI thread (WMI is slow).</summary>
public sealed class BrightnessService
{
    private readonly object _gate = new();
    private double? _pending;
    private bool _busy;

    public bool Supported { get; private set; }
    public double Level { get; private set; }

    public Task LoadAsync() => Task.Run(() =>
    {
        try
        {
            using var s = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementBaseObject o in s.Get())
            {
                Level = Convert.ToInt32(o["CurrentBrightness"]) / 100.0;
                Supported = true;
                o.Dispose();
                break;
            }
        }
        catch
        {
            Supported = false;
        }
    });

    /// <summary>Coalesces rapid slider drags into the latest value.</summary>
    public void Set(double level)
    {
        Level = Math.Clamp(level, 0, 1);
        lock (_gate)
        {
            _pending = Level;
            if (_busy) return;
            _busy = true;
        }

        Task.Run(() =>
        {
            while (true)
            {
                double v;
                lock (_gate)
                {
                    if (_pending is not { } p) { _busy = false; return; }
                    v = p;
                    _pending = null;
                }
                try
                {
                    using var s = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                    foreach (ManagementBaseObject o in s.Get())
                    {
                        using var mo = (ManagementObject)o;
                        mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)Math.Round(v * 100) });
                        break;
                    }
                }
                catch { }
            }
        });
    }
}

/// <summary>Wi-Fi and Bluetooth on/off switches.</summary>
public sealed class RadioService
{
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    private Radio? _wifi, _bluetooth;
    private bool _loaded;

    public event Action? Changed;

    /// <summary>null = this PC has no such radio (or Windows refused access).</summary>
    public bool? WifiOn => _wifi == null ? null : _wifi.State == RadioState.On;
    public bool? BluetoothOn => _bluetooth == null ? null : _bluetooth.State == RadioState.On;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) return;
            foreach (var r in await Radio.GetRadiosAsync())
            {
                if (r.Kind == RadioKind.WiFi && _wifi == null) _wifi = r;
                else if (r.Kind == RadioKind.Bluetooth && _bluetooth == null) _bluetooth = r;
                else continue;
                r.StateChanged += (_, _) => _ui.BeginInvoke(() => Changed?.Invoke());
            }
        }
        catch { }
        Changed?.Invoke();
    }

    public Task ToggleWifiAsync() => ToggleAsync(_wifi);
    public Task ToggleBluetoothAsync() => ToggleAsync(_bluetooth);

    private async Task ToggleAsync(Radio? r)
    {
        if (r == null) return;
        try
        {
            await r.SetStateAsync(r.State == RadioState.On ? RadioState.Off : RadioState.On);
        }
        catch { }
        Changed?.Invoke();
    }
}
