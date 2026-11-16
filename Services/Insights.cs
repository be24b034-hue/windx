using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace WindX.Services;

/// <summary>CPU / memory / disk readings straight from the kernel. Nothing runs unless something asks.</summary>
public static class SystemStats
{
    private static ulong _lastIdle, _lastTotal;

    public static double Cpu { get; private set; }
    public static double Memory { get; private set; }
    public static double Disk { get; private set; }
    public static double MemoryUsedGb { get; private set; }
    public static double MemoryTotalGb { get; private set; }

    /// <summary>Call about once a second while a stats view is on screen.</summary>
    public static void Sample()
    {
        if (GetSystemTimes(out long idle, out long kernel, out long user))
        {
            ulong i = (ulong)idle, total = (ulong)kernel + (ulong)user; // kernel time already includes idle
            ulong di = i - _lastIdle, dt = total - _lastTotal;
            if (_lastTotal != 0 && dt > 0) Cpu = Math.Clamp(1.0 - (double)di / dt, 0, 1);
            _lastIdle = i;
            _lastTotal = total;
        }

        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref ms) && ms.ullTotalPhys > 0)
        {
            Memory = 1.0 - (double)ms.ullAvailPhys / ms.ullTotalPhys;
            MemoryTotalGb = ms.ullTotalPhys / 1073741824.0;
            MemoryUsedGb = (ms.ullTotalPhys - ms.ullAvailPhys) / 1073741824.0;
        }

        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            if (GetDiskFreeSpaceEx(root, out ulong free, out ulong size, out _) && size > 0)
                Disk = 1.0 - (double)free / size;
        }
        catch { }
    }

    public static string Uptime()
    {
        var t = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h" : $"{t.Hours}h {t.Minutes}m";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetDiskFreeSpaceEx(string dir, out ulong freeToCaller, out ulong total, out ulong free);
}

public enum Sky { Clear, PartlyCloudy, Cloudy, Fog, Rain, Snow, Storm }

public sealed record DayForecast(DateTime Date, Sky Sky, double High, double Low);

/// <summary>
/// Weather from Open-Meteo (free, no account, no API key). This is the ONLY thing in WindX that touches the
/// internet, and it stays completely off until the user types a city in Settings. Only that city's rounded
/// coordinates are sent; nothing about the user or the PC.
/// </summary>
public sealed class WeatherService
{
    private static readonly HttpClient Http = CreateClient();
    private DateTime _fetched = DateTime.MinValue;
    private bool _busy;

    public event Action? Changed;

    public bool HasData { get; private set; }
    public double Temperature { get; private set; }
    public Sky Sky { get; private set; }
    public bool IsDay { get; private set; } = true;
    public IReadOnlyList<DayForecast> Days { get; private set; } = Array.Empty<DayForecast>();

    private static Settings S => Settings.Current;
    public static bool Enabled => !string.IsNullOrWhiteSpace(S.WeatherCity) && S.WeatherLat is not null && S.WeatherLon is not null;

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = 256 * 1024 };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WindX/1.0");
        return c;
    }

    public static string Describe(Sky sky) => sky switch
    {
        Sky.Clear => "Clear",
        Sky.PartlyCloudy => "Partly cloudy",
        Sky.Cloudy => "Cloudy",
        Sky.Fog => "Fog",
        Sky.Rain => "Rain",
        Sky.Snow => "Snow",
        _ => "Thunderstorm",
    };

    // WMO weather interpretation codes
    private static Sky FromCode(int code) => code switch
    {
        0 or 1 => Sky.Clear,
        2 => Sky.PartlyCloudy,
        3 => Sky.Cloudy,
        45 or 48 => Sky.Fog,
        >= 51 and <= 67 => Sky.Rain,
        >= 71 and <= 77 => Sky.Snow,
        >= 80 and <= 82 => Sky.Rain,
        85 or 86 => Sky.Snow,
        >= 95 => Sky.Storm,
        _ => Sky.Cloudy,
    };

    /// <summary>Looks a city name up. Returns null when nothing matches or the network is unavailable.</summary>
    public static async Task<(string Name, double Lat, double Lon)?> FindCityAsync(string query)
    {
        query = query.Trim();
        if (query.Length is < 2 or > 80) return null;
        try
        {
            string url = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=en&format=json&name=" + Uri.EscapeDataString(query);
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
            var r = results[0];
            string name = r.GetProperty("name").GetString() ?? query;
            if (r.TryGetProperty("country_code", out var cc) && cc.GetString() is { Length: > 0 } code) name += ", " + code;
            return (name, Math.Round(r.GetProperty("latitude").GetDouble(), 2), Math.Round(r.GetProperty("longitude").GetDouble(), 2));
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        HasData = false;
        Days = Array.Empty<DayForecast>();
        _fetched = DateTime.MinValue;
        Changed?.Invoke();
    }

    /// <summary>Refreshes at most every 30 minutes (or immediately when forced). Safe to call often.</summary>
    public async Task RefreshAsync(bool force = false)
    {
        if (!Enabled)
        {
            if (HasData) Clear();
            return;
        }
        if (_busy || (!force && DateTime.UtcNow - _fetched < TimeSpan.FromMinutes(30))) return;

        _busy = true;
        try
        {
            var inv = CultureInfo.InvariantCulture;
            string url = "https://api.open-meteo.com/v1/forecast"
                + "?latitude=" + S.WeatherLat!.Value.ToString("0.##", inv)
                + "&longitude=" + S.WeatherLon!.Value.ToString("0.##", inv)
                + "&current=temperature_2m,weather_code,is_day"
                + "&daily=weather_code,temperature_2m_max,temperature_2m_min"
                + "&timezone=auto&forecast_days=4"
                + (S.Fahrenheit ? "&temperature_unit=fahrenheit" : "");

            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var cur = doc.RootElement.GetProperty("current");
            Temperature = cur.GetProperty("temperature_2m").GetDouble();
            Sky = FromCode(cur.GetProperty("weather_code").GetInt32());
            IsDay = cur.GetProperty("is_day").GetInt32() == 1;

            var daily = doc.RootElement.GetProperty("daily");
            var dates = daily.GetProperty("time");
            var codes = daily.GetProperty("weather_code");
            var highs = daily.GetProperty("temperature_2m_max");
            var lows = daily.GetProperty("temperature_2m_min");
            var days = new List<DayForecast>();
            for (int i = 0; i < Math.Min(dates.GetArrayLength(), 4); i++)
            {
                days.Add(new DayForecast(
                    DateTime.Parse(dates[i].GetString()!, inv),
                    FromCode(codes[i].GetInt32()),
                    highs[i].GetDouble(), lows[i].GetDouble()));
            }
            Days = days;
            HasData = true;
            _fetched = DateTime.UtcNow;
        }
        catch
        {
            // Offline or the service is down: keep showing the last reading and retry in a few minutes.
            _fetched = DateTime.UtcNow - TimeSpan.FromMinutes(25);
        }
        finally
        {
            _busy = false;
        }
        Changed?.Invoke();
    }
}
