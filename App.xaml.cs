using System.IO;
using System.Windows;
using WindX.Controls;
using WindX.Services;

namespace WindX;

/// <summary>Where WindX lives online. Shown in Settings → About.</summary>
public static class Links
{
    public const string Version = "1.0.0";
    public const string Site = "https://be24b034-hue.github.io/windx/";
    public const string Repo = "https://github.com/be24b034-hue/windx";
    public const string Privacy = "https://be24b034-hue.github.io/windx/privacy.html";

    /// <summary>Opens one of the fixed links above in the default browser. Nothing else can be opened through here.</summary>
    public static void Open(string url)
    {
        if (url != Site && url != Repo && url != Privacy) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }
}

public partial class App : System.Windows.Application
{
    /// <summary>Set by "--snapshot <folder>": render every island state to PNGs, then exit (used for testing).</summary>
    public static string? SnapshotDir { get; private set; }

    private static Mutex? _single;
    private static EventWaitHandle? _openSettings;

    private static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindX");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Build-time helper: "WindX.exe --export-assets <folder>" writes the icon + logo images and exits.
        if (e.Args.Length == 2 && e.Args[0] == "--export-assets")
        {
            try { Logo.Export(e.Args[1]); } catch { Environment.ExitCode = 1; }
            Shutdown();
            return;
        }

        if (e.Args.Length == 2 && e.Args[0] == "--snapshot") SnapshotDir = e.Args[1];

        // One island is enough. Launching the app again just opens Settings on the running one.
        _single = new Mutex(true, SnapshotDir == null ? "WindX.SingleInstance" : "WindX.Snapshot", out bool first);
        _openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, "WindX.OpenSettings");
        if (!first)
        {
            _openSettings.Set();
            Shutdown();
            return;
        }

        ThreadPool.RegisterWaitForSingleObject(_openSettings,
            (_, _) => Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.OpenSettings()),
            null, Timeout.Infinite, executeOnlyOnce: false);

        // A transparent window this small gains nothing from the GPU pipeline, and skipping it
        // avoids loading Direct3D and its buffers (set WINDX_GPU=1 to compare).
        if (Environment.GetEnvironmentVariable("WINDX_GPU") != "1")
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        DispatcherUnhandledException += (_, args) =>
        {
            // Keep the island alive on non-fatal hiccups (e.g. a media app closing mid-query),
            // but leave a trace in %AppData%\WindX\error.log. The log never leaves this PC.
            args.Handled = true;
            Log(args.Exception);
        };

        try
        {
            Settings.Load();
            Theme.Apply(Settings.Current.Theme);
            Tray.MigrateAutostart();
        }
        catch (Exception ex)
        {
            Log(ex);
        }

        base.OnStartup(e);
    }

    private static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            string log = Path.Combine(DataDir, "error.log");
            if (!File.Exists(log) || new FileInfo(log).Length < 200_000)
                File.AppendAllText(log, $"{DateTime.Now:s}  {ex}\n\n");
        }
        catch { }
    }
}
