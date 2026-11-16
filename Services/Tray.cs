using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;

namespace WindX.Services;

/// <summary>
/// Tray icon: double-click opens Settings, right-click shows a small menu.
/// Talks to the shell directly (Shell_NotifyIcon) so WindX never has to load Windows Forms,
/// which alone would add well over 10 MB of memory.
/// </summary>
public sealed class Tray : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "WindX";
    private const string LegacyRunName = "DynamicIsland";

    /// <summary>If autostart was on under the app's old name or folder, point it at this exe.</summary>
    public static void MigrateAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            bool legacy = key.GetValue(LegacyRunName) != null;
            if (legacy) key.DeleteValue(LegacyRunName, throwOnMissingValue: false);
            if ((legacy || key.GetValue(RunName) != null) && Environment.ProcessPath is { } exe)
                key.SetValue(RunName, $"\"{exe}\"");
        }
        catch { }
    }

    public static bool IsAutostart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunName) != null;
        }
        catch { return false; }
    }

    public static void SetAutostart(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (on && Environment.ProcessPath is { } exe) key.SetValue(RunName, $"\"{exe}\"");
            else key.DeleteValue(RunName, throwOnMissingValue: false);
        }
        catch { }
    }

    // ------------------------------------------------------------------ the icon itself

    private const int WM_APP_TRAY = 0x8000 + 1;
    private const int WM_LBUTTONDBLCLK = 0x0203, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B;
    private const int NIM_ADD = 0, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const int NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;

    private readonly HwndSource _sink;
    private readonly Action _openSettings, _exit;
    private readonly IntPtr _hIcon;
    private readonly int _taskbarCreated;
    private ContextMenu? _menu;

    public Tray(Action openSettings, Action exit)
    {
        _openSettings = openSettings;
        _exit = exit;
        // An invisible message-only window receives the icon's clicks.
        _sink = new HwndSource(new HwndSourceParameters("WindXTray") { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = new IntPtr(-3) });
        _sink.AddHook(Hook);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _hIcon = Environment.ProcessPath is { } exe ? ExtractIcon(IntPtr.Zero, exe, 0) : IntPtr.Zero;
        Add();
    }

    private void Add()
    {
        var data = Data();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        Shell_NotifyIcon(NIM_ADD, ref data);
        data.uVersionOrTimeout = 4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    private NOTIFYICONDATA Data() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _sink.Handle,
        uID = 1,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _hIcon,
        szTip = "WindX — double-click for settings",
    };

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _taskbarCreated) Add();   // Explorer restarted: put the icon back
        if (msg != WM_APP_TRAY) return IntPtr.Zero;
        int evt = (int)(lParam.ToInt64() & 0xFFFF);
        if (evt == WM_LBUTTONDBLCLK) _openSettings();
        else if (evt is WM_RBUTTONUP or WM_CONTEXTMENU) ShowMenu();
        handled = true;
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        if (_menu == null)
        {
            _menu = new ContextMenu();
            var settings = new MenuItem { Header = "Settings…" };
            settings.Click += (_, _) => _openSettings();
            var quit = new MenuItem { Header = "Quit WindX" };
            quit.Click += (_, _) => _exit();
            _menu.Items.Add(settings);
            _menu.Items.Add(new Separator());
            _menu.Items.Add(quit);
        }
        // The menu needs a foreground window or it won't close when you click away.
        SetForegroundWindow(_sink.Handle);
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    public void Dispose()
    {
        var data = Data();
        Shell_NotifyIcon(NIM_DELETE, ref data);
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
        _sink.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string file, int index);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}

/// <summary>Battery / charger state straight from Windows (replaces Windows Forms' PowerStatus).</summary>
public static class Power
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    /// <summary>(has a battery, plugged in, level 0..1)</summary>
    public static (bool HasBattery, bool Plugged, double Level) Read()
    {
        if (!GetSystemPowerStatus(out var s)) return (false, true, 1);
        bool hasBattery = (s.BatteryFlag & 128) == 0 && s.BatteryFlag != 255 && s.BatteryLifePercent <= 100;
        return (hasBattery, s.ACLineStatus == 1, hasBattery ? s.BatteryLifePercent / 100.0 : 1);
    }
}
