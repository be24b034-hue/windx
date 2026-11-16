using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace WindX.Services;

/// <summary>Everything the user can change, saved to %AppData%\WindX\settings.json.</summary>
public sealed class Settings
{
    public string Theme { get; set; } = "Obsidian";
    public double Scale { get; set; } = 1.0;
    public bool AlbumAccent { get; set; } = true;

    /// <summary>Living backdrop inside the island: "None", "Wind", "Water", "Fire", "Aurora" or "Stars".</summary>
    public string Effect { get; set; } = "Wind";
    /// <summary>Keep the effect moving in the small pill too (otherwise it only moves while the island is open or hovered).</summary>
    public bool EffectWhenIdle { get; set; }
    /// <summary>Which team's car the Race effect shows (a team theme's name), or empty for the theme's own colour.</summary>
    public string Car { get; set; } = "";
    /// <summary>Big faint lettering behind the open island. Empty = a team island shows its own name, others show nothing.</summary>
    public string BgText { get; set; } = "";
    /// <summary>A picture from the user's own PC shown inside the island, behind everything. Empty = none.</summary>
    public string BgImage { get; set; } = "";
    /// <summary>How much the picture is darkened so text stays readable (0 - 0.8).</summary>
    public double BgImageDim { get; set; } = 0.45;

    /// <summary>How solid the island's background is (0.4 - 1).</summary>
    public double IslandOpacity { get; set; } = 1.0;
    /// <summary>"Left", "Center" or "Right": where along the top edge the island sits.</summary>
    public string Position { get; set; } = "Center";

    // A tag hanging from the island on a thread: a rotating quote, or your own reminder.
    public bool ShowNote { get; set; }
    public string NoteMode { get; set; } = "Quote";
    public string NoteText { get; set; } = "";
    public string NoteStyle { get; set; } = "Cardboard";

    // Quick controls shown when the island is open
    public bool ShowBrightness { get; set; } = true;
    public bool ShowVolume { get; set; } = true;
    public bool ShowWifi { get; set; } = true;
    public bool ShowBluetooth { get; set; } = true;
    public bool ShowMute { get; set; } = true;
    public bool ShowSnip { get; set; } = true;

    // Island content
    public bool ShowBattery { get; set; } = true;
    public bool Use24Hour { get; set; }

    // Behaviour
    public bool ExpandOnHover { get; set; }
    /// <summary>Seconds of inactivity before the island tucks away. -1 = never, 0 = as soon as the mouse leaves.</summary>
    public int AutoHideSeconds { get; set; } = -1;
    /// <summary>What to do over fullscreen games/videos: "Hide", "Stats" (CPU/RAM overlay) or "Show".</summary>
    public string FullscreenMode { get; set; } = "Hide";
    public bool IntroAnimation { get; set; } = true;

    // Weather (off until a city is set; see WeatherService)
    public string WeatherCity { get; set; } = "";
    public double? WeatherLat { get; set; }
    public double? WeatherLon { get; set; }
    public bool Fahrenheit { get; set; }

    public bool ShowNotesTab { get; set; } = true;
    public bool ShowFocusTab { get; set; } = true;
    /// <summary>Show sticky notes stuck onto the island.</summary>
    public bool PinNote { get; set; }
    /// <summary>Up to four sticky notes, each with its own colour.</summary>
    public List<StickyItem> Stickies { get; set; } = new();

    /// <summary>Free text from the island's Notes tab.</summary>
    public string Notes { get; set; } = "";

    /// <summary>Reminders that pop up on the island at a set time.</summary>
    public List<ReminderItem> Reminders { get; set; } = new();

    // Sound
    public bool Sounds { get; set; } = true;
    /// <summary>"Off", "Open" (only while the island is open) or "Always".</summary>
    public string MusicMode { get; set; } = "Off";
    /// <summary>An audio file the user chose from their own PC.</summary>
    public string MusicFile { get; set; } = "";
    public double MusicVolume { get; set; } = 0.5;

    public bool VolumePopups { get; set; } = true;
    public bool TrackPopups { get; set; } = true;
    public bool PowerPopups { get; set; } = true;

    public static Settings Current { get; private set; } = new();

    /// <summary>Raised after every Save so the island can re-apply live.</summary>
    public static event Action? Changed;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindX", "settings.json");

    public static void Load()
    {
        try
        {
            // A settings file is a few hundred bytes; refuse anything suspiciously large.
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length < 64 * 1024)
                Current = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch
        {
            Current = new Settings();
        }

        // Never trust a hand-edited file: clamp and whitelist everything that drives layout or requests.
        Current.Theme = Services.Theme.Resolve(Current.Theme).Name;
        Current.Scale = double.IsFinite(Current.Scale) ? Math.Clamp(Current.Scale, 0.85, 1.2) : 1.0;
        if (Current.Effect == "Grid") Current.Effect = "Race";
        if (!Enum.TryParse<WindX.Controls.IslandEffect>(Current.Effect, out _)) Current.Effect = "Wind";
        if (Current.Effect is "Infinity" or "WaterBlade" or "FlameBlade" or "ThunderStep" or "Spiral" or "SpiritOrb" or "Domain" or "BlackFlash" or "Beam" or "CrimsonMoon") Current.Effect = "Wind";
        if (string.IsNullOrEmpty(Current.Car) || Services.Theme.Resolve(Current.Car).Livery < 0 || Services.Theme.Resolve(Current.Car).Name != Current.Car) Current.Car = "";
        if (Current.AutoHideSeconds is not (-1 or 0 or 5 or 10 or 30 or 60)) Current.AutoHideSeconds = -1;
        Current.IslandOpacity = double.IsFinite(Current.IslandOpacity) ? Math.Clamp(Current.IslandOpacity, 0.4, 1.0) : 1.0;
        if (Current.Position is not ("Left" or "Center" or "Right")) Current.Position = "Center";
        if (Current.NoteMode is not ("Quote" or "Custom")) Current.NoteMode = "Quote";
        if (Current.NoteStyle is not ("Cardboard" or "Sticky" or "Paper" or "Theme" or "Starry" or "Wave" or "Sunset" or "Blueprint")) Current.NoteStyle = "Cardboard";
        if (Current.MusicMode is not ("Off" or "Open" or "Always")) Current.MusicMode = "Off";
        Current.MusicVolume = double.IsFinite(Current.MusicVolume) ? Math.Clamp(Current.MusicVolume, 0.05, 1) : 0.5;
        Current.MusicFile ??= "";
        string ext = Current.MusicFile.Length is > 3 and < 260 ? Path.GetExtension(Current.MusicFile).ToLowerInvariant() : "";
        if (ext is not (".mp3" or ".wav" or ".m4a" or ".wma" or ".aac" or ".flac") || !Path.IsPathFullyQualified(Current.MusicFile)) Current.MusicFile = "";
        Current.BgImage ??= "";
        string pic = Current.BgImage.Length is > 3 and < 260 ? Path.GetExtension(Current.BgImage).ToLowerInvariant() : "";
        if (pic is not (".png" or ".jpg" or ".jpeg" or ".bmp") || !Path.IsPathFullyQualified(Current.BgImage)) Current.BgImage = "";
        Current.BgImageDim = double.IsFinite(Current.BgImageDim) ? Math.Clamp(Current.BgImageDim, 0, 0.8) : 0.45;
        Current.Notes ??= "";
        if (Current.Notes.Length > 4000) Current.Notes = Current.Notes[..4000];
        Current.Stickies ??= new();
        Current.Stickies.RemoveAll(s => s == null || string.IsNullOrWhiteSpace(s.Text));
        if (Current.Stickies.Count > 4) Current.Stickies.RemoveRange(4, Current.Stickies.Count - 4);
        foreach (var s in Current.Stickies)
        {
            s.Text = s.Text.Trim();
            if (s.Text.Length > 80) s.Text = s.Text[..80];
            if (s.Color is not ("Yellow" or "Pink" or "Blue" or "Green" or "Orange")) s.Color = "Yellow";
        }
        Current.Reminders ??= new();
        Current.Reminders.RemoveAll(r => r == null || string.IsNullOrWhiteSpace(r.Text)
            || !TimeSpan.TryParseExact(r.Time, "hh\\:mm", System.Globalization.CultureInfo.InvariantCulture, out _));
        if (Current.Reminders.Count > 30) Current.Reminders.RemoveRange(30, Current.Reminders.Count - 30);
        foreach (var r in Current.Reminders) if (r.Text.Length > 80) r.Text = r.Text[..80];
        Current.BgText = (Current.BgText ?? "").Trim();
        if (Current.BgText.Length > 24) Current.BgText = Current.BgText[..24];
        Current.NoteText = (Current.NoteText ?? "").Trim();
        if (Current.NoteText.Length > 140) Current.NoteText = Current.NoteText[..140];
        if (Current.FullscreenMode is not ("Hide" or "Stats" or "Show")) Current.FullscreenMode = "Hide";
        Current.WeatherCity = (Current.WeatherCity ?? "").Trim();
        if (Current.WeatherCity.Length > 80) Current.WeatherCity = Current.WeatherCity[..80];
        if (Current.WeatherLat is { } lat && (!double.IsFinite(lat) || Math.Abs(lat) > 90)) Current.WeatherLat = null;
        if (Current.WeatherLon is { } lon && (!double.IsFinite(lon) || Math.Abs(lon) > 180)) Current.WeatherLon = null;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
        Changed?.Invoke();
    }
}

/// <summary>One sticky note on the island.</summary>
public sealed class StickyItem
{
    public string Text { get; set; } = "";
    public string Color { get; set; } = "Yellow";
}

/// <summary>One reminder: a time of day (24-hour "HH:mm"), what to say, and whether it repeats daily.</summary>
public sealed class ReminderItem
{
    public string Time { get; set; } = "09:00";
    public string Text { get; set; } = "";
    public bool Repeat { get; set; }
    public string LastFired { get; set; } = "";
}

/// <summary>A colour scheme for the island.</summary>
public sealed class Theme
{
    public required string Name { get; init; }
    public required string Bg { get; init; }
    public string? Bg2 { get; init; }           // set = soft top-to-bottom gradient
    public required string Border { get; init; }
    public required string Text { get; init; }
    public required string SubText { get; init; }
    public required string Track { get; init; }
    public required string Chip { get; init; }
    public required string ChipHover { get; init; }
    public required string Accent { get; init; }
    public required string OnAccent { get; init; }
    public string? Glow { get; init; }          // coloured halo even when nothing plays
    public bool IsLight { get; init; }
    public bool Vivid { get; init; }            // bold corner-to-corner gradient
    public string Group { get; init; } = "Classic";   // Classic, Gradient or Teams
    public int Livery { get; init; } = -1;       // team themes: which car colours go with it
    public string? Fx { get; init; }             // effect switched on together with this theme

    // Each palette is built from one hue family: a near-black (or near-white) base tinted toward the accent,
    // one confident accent, and neutrals that carry the same tint. No rainbow gradients.
    private static readonly Theme[] _all =
    {
        new() { Name = "Obsidian", Bg = "#050505", Bg2 = "#0B0B0C", Border = "#1D1D1F", Text = "#F5F5F7", SubText = "#8E8E93",
                Track = "#2A2A2D", Chip = "#18181A", ChipHover = "#232326", Accent = "#F5F5F7", OnAccent = "#050505" },
        new() { Name = "Ember", Bg = "#0F0806", Bg2 = "#1B0D08", Border = "#3A1D12", Text = "#FFF3EC", SubText = "#BF9E91",
                Track = "#3A2118", Chip = "#24120C", ChipHover = "#311910", Accent = "#FF5B1F", OnAccent = "#170802" },
        new() { Name = "Tide", Bg = "#03111A", Bg2 = "#06212C", Border = "#103A47", Text = "#E9FBFF", SubText = "#84ACB6",
                Track = "#0F3542", Chip = "#082733", ChipHover = "#0C3340", Accent = "#3CC8E0", OnAccent = "#021016" },
        new() { Name = "Moss", Bg = "#090F0A", Bg2 = "#121D14", Border = "#243829", Text = "#F0F7EE", SubText = "#9AB19D",
                Track = "#223527", Chip = "#16261A", ChipHover = "#1D3222", Accent = "#9BE28A", OnAccent = "#0A1409" },
        new() { Name = "Rosé", Bg = "#170D10", Bg2 = "#26131A", Border = "#45242F", Text = "#FFF0F3", SubText = "#C6A0AA",
                Track = "#42242F", Chip = "#2E1720", ChipHover = "#3B1E29", Accent = "#FF8FA9", OnAccent = "#1E0A10" },
        new() { Name = "Ultraviolet", Bg = "#0B0819", Bg2 = "#150E33", Border = "#2D2163", Text = "#F1EEFF", SubText = "#A39BD3",
                Track = "#291E59", Chip = "#191240", ChipHover = "#231953", Accent = "#A78BFA", OnAccent = "#0D0A1F" },
        new() { Name = "Graphite", Bg = "#1C1C1E", Bg2 = "#232326", Border = "#36363B", Text = "#FFFFFF", SubText = "#A1A1AA",
                Track = "#3C3C42", Chip = "#2C2C31", ChipHover = "#38383E", Accent = "#FFD60A", OnAccent = "#1C1C1E" },
        new() { Name = "Laser", Bg = "#030304", Border = "#B6F03A", Text = "#F4FFE0", SubText = "#9BB07C",
                Track = "#1A2210", Chip = "#0F1509", ChipHover = "#171F0D", Accent = "#C6FF3D", OnAccent = "#0A0F02",
                Glow = "#B6F03A" },
        new() { Name = "Sand", Bg = "#F5F0E7", Bg2 = "#EBE4D7", Border = "#D7CEBD", Text = "#1B1A17", SubText = "#777164",
                Track = "#D8CFBF", Chip = "#E5DDCE", ChipHover = "#DAD1C0", Accent = "#1B1A17", OnAccent = "#F5F0E7",
                IsLight = true },
        new() { Name = "Porcelain", Bg = "#FFFFFF", Bg2 = "#F3F3F6", Border = "#DCDCE2", Text = "#111113", SubText = "#6E6E76",
                Track = "#DADAE0", Chip = "#EDEDF1", ChipHover = "#E1E1E7", Accent = "#FF5B1F", OnAccent = "#FFFFFF",
                IsLight = true },

        // Liquid Glass: a translucent island your desktop shows through, with live highlights (Glass effect).
        new() { Name = "Liquid Glass", Fx = "Glass", Bg = "#8A10131A", Bg2 = "#6A262C3A", Border = "#99FFFFFF", Text = "#FFFFFF", SubText = "#D5E2E6EE",
                Track = "#40FFFFFF", Chip = "#2EFFFFFF", ChipHover = "#47FFFFFF", Accent = "#FFFFFF", OnAccent = "#0B0D12", Glow = "#9DB8FF" },

        // Gradient themes: a two-colour sweep with frosted (translucent) controls on top.
        new() { Name = "Sunrise", Vivid = true, Group = "Gradient", Bg = "#4A0E4E", Bg2 = "#E2572B", Border = "#55FFFFFF", Text = "#FFFFFF", SubText = "#E8D5DC",
                Track = "#4D000000", Chip = "#38000000", ChipHover = "#55000000", Accent = "#FFD166", OnAccent = "#3A1205" },
        new() { Name = "Lagoon", Vivid = true, Group = "Gradient", Bg = "#0B2A6F", Bg2 = "#0FA3A0", Border = "#55FFFFFF", Text = "#FFFFFF", SubText = "#D2ECEF",
                Track = "#4D000000", Chip = "#38000000", ChipHover = "#55000000", Accent = "#B8FFF2", OnAccent = "#06303A" },
        new() { Name = "Candy", Vivid = true, Group = "Gradient", Bg = "#5B1A8A", Bg2 = "#E63E7B", Border = "#55FFFFFF", Text = "#FFFFFF", SubText = "#F1D6E8",
                Track = "#4D000000", Chip = "#38000000", ChipHover = "#55000000", Accent = "#FFE3F0", OnAccent = "#4A0F3A" },
        new() { Name = "Jungle", Vivid = true, Group = "Gradient", Bg = "#06302A", Bg2 = "#5AA02C", Border = "#55FFFFFF", Text = "#FFFFFF", SubText = "#D9EDD3",
                Track = "#4D000000", Chip = "#38000000", ChipHover = "#55000000", Accent = "#E9FF70", OnAccent = "#10300A" },
        new() { Name = "Dusk", Vivid = true, Group = "Gradient", Bg = "#141E5A", Bg2 = "#8E2D6B", Border = "#55FFFFFF", Text = "#FFFFFF", SubText = "#DBD3EE",
                Track = "#4D000000", Chip = "#38000000", ChipHover = "#55000000", Accent = "#FFB3C7", OnAccent = "#2A0F3A" },
        new() { Name = "Molten", Vivid = true, Group = "Gradient", Bg = "#3B0A0A", Bg2 = "#F59E0B", Border = "#55FFFFFF", Text = "#FFFFFF", SubText = "#F2DFC9",
                Track = "#4D000000", Chip = "#38000000", ChipHover = "#55000000", Accent = "#FFF1B8", OnAccent = "#3B1205" },

        // Team islands: each is a full colour scheme plus its own car. Names and colours only (no logos or sponsor artwork); not affiliated with the teams.
        new() { Name = "Red Bull", Group = "Teams", Livery = 0, Fx = "Race", Bg = "#0A1233", Bg2 = "#1B2A5E", Border = "#E1061C", Text = "#FFFFFF", SubText = "#B5BEE3",
                Track = "#2A3875", Chip = "#172250", ChipHover = "#223069", Accent = "#FFC906", OnAccent = "#0A1233", Glow = "#E1061C" },
        new() { Name = "Ferrari", Group = "Teams", Livery = 1, Fx = "Race", Vivid = true, Bg = "#6E0000", Bg2 = "#DC0000", Border = "#66FFFFFF", Text = "#FFFFFF", SubText = "#F5CCCC",
                Track = "#4D000000", Chip = "#3D000000", ChipHover = "#5A000000", Accent = "#FFF200", OnAccent = "#5A0000" },
        new() { Name = "Mercedes", Group = "Teams", Livery = 2, Fx = "Race", Bg = "#0C0D0E", Bg2 = "#22262A", Border = "#3B4247", Text = "#F2F5F6", SubText = "#A9B2B7",
                Track = "#333A3F", Chip = "#1B1F22", ChipHover = "#272C30", Accent = "#00D2BE", OnAccent = "#04201D" },
        new() { Name = "McLaren", Group = "Teams", Livery = 3, Fx = "Race", Bg = "#141210", Bg2 = "#3B2206", Border = "#5A340A", Text = "#FFF6EC", SubText = "#C9B49B",
                Track = "#4A300F", Chip = "#261A0C", ChipHover = "#352410", Accent = "#FF8000", OnAccent = "#1A0E00" },
        new() { Name = "Aston Martin", Group = "Teams", Livery = 4, Fx = "Race", Bg = "#002A25", Bg2 = "#00594F", Border = "#0B7366", Text = "#F1FBF8", SubText = "#9CC9C0",
                Track = "#0A6156", Chip = "#063E37", ChipHover = "#0A4E45", Accent = "#CEDC00", OnAccent = "#0A2200" },
        new() { Name = "Alpine", Group = "Teams", Livery = 5, Fx = "Race", Bg = "#081F52", Bg2 = "#0F4FB5", Border = "#3A6FCC", Text = "#FFFFFF", SubText = "#BCCDF0",
                Track = "#2452A8", Chip = "#0F3684", ChipHover = "#16449E", Accent = "#FF87BC", OnAccent = "#3A0A22" },
        new() { Name = "Williams", Group = "Teams", Livery = 6, Fx = "Race", Bg = "#000F2E", Bg2 = "#00205B", Border = "#123A85", Text = "#F1F7FF", SubText = "#9DB4DB",
                Track = "#10336F", Chip = "#061C4B", ChipHover = "#0B2860", Accent = "#64C4FF", OnAccent = "#001A33" },
        new() { Name = "Racing Bulls", Group = "Teams", Livery = 7, Fx = "Race", Bg = "#FFFFFF", Bg2 = "#DFE6F7", Border = "#C3CEEA", Text = "#101A4A", SubText = "#5E6893",
                Track = "#C9D3EC", Chip = "#E3E9F7", ChipHover = "#D3DCF2", Accent = "#1534CC", OnAccent = "#FFFFFF", IsLight = true },
        new() { Name = "Kick Sauber", Group = "Teams", Livery = 8, Fx = "Race", Bg = "#040504", Bg2 = "#0E1B0E", Border = "#1F3A1F", Text = "#F1FFF1", SubText = "#97B597",
                Track = "#1C331C", Chip = "#0E190E", ChipHover = "#162616", Accent = "#52E252", OnAccent = "#041204", Glow = "#52E252" },
        new() { Name = "Haas", Group = "Teams", Livery = 9, Fx = "Race", Bg = "#19191B", Bg2 = "#2C2C30", Border = "#44444A", Text = "#FFFFFF", SubText = "#B3B3BA",
                Track = "#3E3E44", Chip = "#26262A", ChipHover = "#333338", Accent = "#E6002B", OnAccent = "#FFFFFF" },
        new() { Name = "PUBG", Group = "Teams", Fx = "Overgrown", Bg = "#14222A", Bg2 = "#2C4638", Border = "#4A6658", Text = "#F2F6EC", SubText = "#A9BDB0",
                Track = "#3A5548", Chip = "#1F3238", ChipHover = "#2A4248", Accent = "#B7E07A", OnAccent = "#10200A" },
        new() { Name = "Krafton", Group = "Teams", Fx = "Signal", Bg = "#060000", Bg2 = "#2A0502", Border = "#FF3B2F", Text = "#FFF1EE", SubText = "#C99A94",
                Track = "#4A1410", Chip = "#1E0705", ChipHover = "#2E0B08", Accent = "#FF3B2F", OnAccent = "#0A0000", Glow = "#FF3B2F" },
    };

    // Names used by earlier builds, so an old settings file keeps a sensible look.
    private static readonly Dictionary<string, string> Legacy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Midnight"] = "Obsidian", ["Aurora"] = "Tide", ["Sunset"] = "Ember", ["Ocean"] = "Tide", ["Sakura"] = "Rosé",
        ["Emerald"] = "Moss", ["Junior Bulls"] = "Racing Bulls", ["Bulls"] = "Red Bull", ["Scarlet"] = "Ferrari", ["Silver Arrows"] = "Mercedes", ["Papaya"] = "McLaren", ["Racing Green"] = "Aston Martin", ["Azure"] = "Alpine", ["Royal"] = "Williams", ["Kick"] = "Kick Sauber", ["Stripes"] = "Haas", ["Battleground"] = "PUBG", ["Gold"] = "Ember", ["Neon"] = "Laser", ["Snow"] = "Porcelain",
    };

    private static Theme? _current;
    public static Theme Current { get => _current ?? _all[0]; private set => _current = value; }

    public static IReadOnlyList<Theme> All => _all;

    public static Theme Resolve(string? name)
    {
        name ??= "";
        if (Legacy.TryGetValue(name, out var mapped)) name = mapped;
        return _all.FirstOrDefault(x => x.Name == name) ?? _all[0];
    }

    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public Color AccentColor => C(Accent);

    public Brush BackgroundBrush()
    {
        Brush b = Bg2 == null
            ? new SolidColorBrush(C(Bg))
            : Vivid ? new LinearGradientBrush(C(Bg), C(Bg2), new Point(0, 0), new Point(1, 1))
            : new LinearGradientBrush(C(Bg), C(Bg2), new Point(0.3, 0), new Point(0.7, 1));
        b.Freeze();
        return b;
    }

    private static SolidColorBrush Solid(string hex)
    {
        var b = new SolidColorBrush(C(hex));
        b.Freeze();
        return b;
    }

    /// <summary>Swaps the app-wide brushes; everything bound with DynamicResource updates instantly.</summary>
    public static void Apply(string name)
    {
        var t = Resolve(name);
        Current = t;

        var r = Application.Current.Resources;
        r["IslandBg"] = t.BackgroundBrush();
        r["IslandBorder"] = Solid(t.Border);
        r["TextPrimary"] = Solid(t.Text);
        r["TextSecondary"] = Solid(t.SubText);
        r["TrackBg"] = Solid(t.Track);
        r["ChipBg"] = Solid(t.Chip);
        r["ChipHover"] = Solid(t.ChipHover);
        r["Accent"] = Solid(t.Accent);
        r["OnAccent"] = Solid(t.OnAccent);
    }
}
