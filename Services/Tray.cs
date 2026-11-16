using Microsoft.Win32;
using D = System.Drawing;
using WF = System.Windows.Forms;

namespace WindX.Services;

/// <summary>Tray icon: opens Settings, or quits.</summary>
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

    private readonly WF.NotifyIcon _icon;

    public Tray(Action openSettings, Action exit)
    {
        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => openSettings());
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Quit WindX", null, (_, _) => exit());

        _icon = new WF.NotifyIcon
        {
            Icon = AppIcon(),
            Text = "WindX — double-click for settings",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => openSettings();
    }

    private static D.Icon AppIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && D.Icon.ExtractAssociatedIcon(exe) is { } icon) return icon;
        }
        catch { }
        return MakeIcon();
    }

    private static D.Icon MakeIcon()
    {
        using var bmp = new D.Bitmap(32, 32);
        using (var g = D.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = D.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(D.Color.Transparent);
            using var path = new D.Drawing2D.GraphicsPath();
            var r = new D.Rectangle(2, 9, 28, 14);
            int d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            path.CloseFigure();
            using var fill = new D.SolidBrush(D.Color.FromArgb(12, 12, 14));
            using var pen = new D.Pen(D.Color.FromArgb(235, 235, 240), 2f);
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
        return D.Icon.FromHandle(bmp.GetHicon());
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

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
