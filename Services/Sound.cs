using System.IO;
using System.Media;
using System.Windows.Media;

namespace WindX.Services;

/// <summary>
/// Tiny interface sounds, synthesised in memory the first time each is needed (no audio files shipped):
/// a soft tick when the mouse touches the island, a rising whoosh when it opens, and so on.
/// </summary>
public static class Sfx
{
    private const int Rate = 22050;
    private static readonly Dictionary<string, byte[]> Cache = new();
    private static SoundPlayer? _player;

    public static void Play(string name)
    {
        if (!Settings.Current.Sounds) return;
        try
        {
            if (!Cache.TryGetValue(name, out var wav))
            {
                wav = name switch
                {
                    "touch" => Tone(1500, 1900, 38, 0.16, 0),
                    "open" => Tone(260, 760, 190, 0.22, 0.35),
                    "close" => Tone(700, 240, 150, 0.18, 0.3),
                    "tab" => Tone(880, 1180, 55, 0.18, 0),
                    "note" => Tone(520, 390, 120, 0.2, 0.1),
                    "alarm" => Tone(1175, 784, 900, 0.32, 0),
                    _ => Tone(1000, 1000, 40, 0.15, 0),
                };
                Cache[name] = wav;
            }
            _player?.Dispose();
            _player = new SoundPlayer(new MemoryStream(wav));
            _player.Play();   // asynchronous
        }
        catch { }
    }

    /// <summary>A sine sweep from f0 to f1 with a fast attack and smooth decay, plus optional breathy noise.</summary>
    private static byte[] Tone(double f0, double f1, int ms, double volume, double air)
    {
        int n = Rate * ms / 1000;
        var rnd = new Random(1);
        using var s = new MemoryStream();
        using var w = new BinaryWriter(s);
        w.Write("RIFF"u8); w.Write(36 + n * 2); w.Write("WAVEfmt "u8); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(n * 2);
        double phase = 0, low = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)n;
            phase += 2 * Math.PI * (f0 + (f1 - f0) * t) / Rate;
            double env = Math.Min(1, i / (Rate * 0.004)) * Math.Pow(1 - t, 2.2);
            low += (rnd.NextDouble() * 2 - 1 - low) * 0.25;                       // soft, low-passed noise
            double v = (Math.Sin(phase) * (1 - air) + low * air * 2.2) * env * volume;
            w.Write((short)(Math.Clamp(v, -1, 1) * short.MaxValue));
        }
        w.Flush();
        return s.ToArray();
    }
}

/// <summary>Optional theme music: any audio file the user picks from their own PC, looped.</summary>
public sealed class ThemeMusic
{
    private MediaPlayer? _player;
    private string _loaded = "";

    public void Update(bool islandOpen, bool suppressed)
    {
        var s = Settings.Current;
        bool want = !suppressed && s.MusicMode != "Off" && s.MusicFile.Length > 0 && File.Exists(s.MusicFile)
                    && (s.MusicMode == "Always" || islandOpen);
        try
        {
            if (!want)
            {
                _player?.Pause();
                return;
            }
            if (_player == null)
            {
                _player = new MediaPlayer();
                _player.MediaEnded += (_, _) => { _player.Position = TimeSpan.Zero; _player.Play(); };
            }
            if (_loaded != s.MusicFile)
            {
                _player.Open(new Uri(s.MusicFile));
                _loaded = s.MusicFile;
            }
            _player.Volume = s.MusicVolume;
            _player.Play();
        }
        catch { }
    }

    public void Stop()
    {
        try { _player?.Close(); } catch { }
        _player = null;
        _loaded = "";
    }
}
