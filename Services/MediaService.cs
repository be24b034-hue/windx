using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;

namespace WindX.Services;

using Session = GlobalSystemMediaTransportControlsSession;
using Manager = GlobalSystemMediaTransportControlsSessionManager;

/// <summary>Tracks whatever is playing on the system (Spotify, browsers, etc.) via Windows media sessions.</summary>
public sealed class MediaService
{
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    private Manager? _mgr;
    private Session? _session;
    private string? _sessionId;
    private int _version;

    private TypedEventHandler<Session, MediaPropertiesChangedEventArgs>? _onProps;
    private TypedEventHandler<Session, PlaybackInfoChangedEventArgs>? _onPlayback;
    private TypedEventHandler<Session, TimelinePropertiesChangedEventArgs>? _onTimeline;

    private TimeSpan _position;
    private DateTime _positionStamp = DateTime.UtcNow;

    public event Action? Changed;

    private bool _has, _dismissed;
    private string _dismissTitle = "";

    /// <summary>Something is loaded, and the user hasn't closed it from the island.</summary>
    public bool HasMedia => _has && !_dismissed;
    public bool IsPlaying { get; private set; }
    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public ImageSource? Art { get; private set; }
    public Color Accent { get; private set; } = Color.FromRgb(0xE8, 0xE8, 0xEE);
    public TimeSpan Duration { get; private set; }
    public bool CanPrevious { get; private set; }
    public bool CanNext { get; private set; }
    public bool CanSeek { get; private set; }

    /// <summary>Playback position, extrapolated between the sparse updates apps give us.</summary>
    public TimeSpan Position
    {
        get
        {
            var p = _position;
            if (IsPlaying) p += DateTime.UtcNow - _positionStamp;
            if (Duration > TimeSpan.Zero && p > Duration) p = Duration;
            return p < TimeSpan.Zero ? TimeSpan.Zero : p;
        }
    }

    public async Task StartAsync()
    {
        try
        {
            _mgr = await Manager.RequestAsync();
        }
        catch
        {
            return;
        }

        _mgr.CurrentSessionChanged += (_, _) => Post(Attach);
        _mgr.SessionsChanged += (_, _) => Post(Attach);
        Attach();
    }

    /// <summary>Cheap periodic check: switch to another app if it has started playing.</summary>
    public void Rescan()
    {
        if (_mgr == null) return;
        try
        {
            var pick = Pick();
            if (pick?.SourceAppUserModelId != _sessionId) Attach();
        }
        catch { }
    }

    private void Post(Action a) => _ui.BeginInvoke(a);

    private Session? Pick()
    {
        if (_mgr == null) return null;
        try
        {
            foreach (var s in _mgr.GetSessions())
            {
                if (s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    return s;
            }
            return _mgr.GetCurrentSession();
        }
        catch
        {
            return null;
        }
    }

    private void Attach()
    {
        if (_session != null)
        {
            try
            {
                _session.MediaPropertiesChanged -= _onProps;
                _session.PlaybackInfoChanged -= _onPlayback;
                _session.TimelinePropertiesChanged -= _onTimeline;
            }
            catch { }
        }

        _session = Pick();
        _sessionId = _session?.SourceAppUserModelId;

        if (_session != null)
        {
            _onProps = (_, _) => Post(() => _ = RefreshAsync());
            _onPlayback = (_, _) => Post(() => { ReadPlayback(); Changed?.Invoke(); });
            _onTimeline = (_, _) => Post(() => { ReadTimeline(); Changed?.Invoke(); });
            try
            {
                _session.MediaPropertiesChanged += _onProps;
                _session.PlaybackInfoChanged += _onPlayback;
                _session.TimelinePropertiesChanged += _onTimeline;
            }
            catch { }
        }

        _ = RefreshAsync();
    }

    private void ReadPlayback()
    {
        var s = _session;
        if (s == null) return;
        try
        {
            // Freeze the extrapolated position before the playing flag flips.
            _position = Position;
            _positionStamp = DateTime.UtcNow;

            var info = s.GetPlaybackInfo();
            bool was = IsPlaying;
            IsPlaying = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (_dismissed && IsPlaying && !was && DateTime.UtcNow > _dismissedAt.AddSeconds(2)) _dismissed = false;   // played again
            CanPrevious = info.Controls.IsPreviousEnabled;
            CanNext = info.Controls.IsNextEnabled;
            CanSeek = info.Controls.IsPlaybackPositionEnabled;
        }
        catch { }
    }

    private void ReadTimeline()
    {
        var s = _session;
        if (s == null) return;
        try
        {
            var t = s.GetTimelineProperties();
            Duration = t.EndTime - t.StartTime;
            _position = t.Position - t.StartTime;
            var stamp = t.LastUpdatedTime.UtcDateTime;
            // Some apps never fill in LastUpdatedTime; fall back to "now".
            _positionStamp = stamp.Year < 2000 || stamp > DateTime.UtcNow ? DateTime.UtcNow : stamp;
        }
        catch
        {
            Duration = TimeSpan.Zero;
        }
    }

    private async Task RefreshAsync()
    {
        int v = ++_version;
        var s = _session;
        if (s == null)
        {
            Clear();
            Changed?.Invoke();
            return;
        }

        try
        {
            ReadPlayback();
            ReadTimeline();

            var props = await s.TryGetMediaPropertiesAsync();
            if (v != _version) return;

            Title = props?.Title ?? "";
            Artist = props?.Artist ?? "";
            if (string.IsNullOrWhiteSpace(Artist)) Artist = props?.AlbumArtist ?? "";
            _has = !string.IsNullOrWhiteSpace(Title);
            if (_dismissed && Title != _dismissTitle) _dismissed = false;   // a new track brings it back

            ImageSource? art = null;
            var thumb = props?.Thumbnail;
            if (thumb != null)
            {
                try
                {
                    using var ras = await thumb.OpenReadAsync();
                    using var stream = ras.AsStreamForRead();
                    var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    ms.Position = 0;

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 192;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    art = bmp;
                }
                catch { }
            }
            if (v != _version) return;

            Art = art;
            Accent = art is BitmapSource bs ? ExtractAccent(bs) : Color.FromRgb(0xE8, 0xE8, 0xEE);
        }
        catch
        {
            if (v != _version) return;
            Clear();
        }

        Changed?.Invoke();
    }

    private void Clear()
    {
        _has = false;
        IsPlaying = false;
        Title = Artist = "";
        Art = null;
        Duration = TimeSpan.Zero;
        _position = TimeSpan.Zero;
    }

    private DateTime _dismissedAt;

    /// <summary>Pause and hide the player until the user plays something again.</summary>
    public void Dismiss()
    {
        if (IsPlaying) Run(s => s.TryPauseAsync());
        _dismissed = true;
        _dismissTitle = Title;
        _dismissedAt = DateTime.UtcNow;
        Changed?.Invoke();
    }

    public void TogglePlay() => Run(s => s.TryTogglePlayPauseAsync());
    public void Next() => Run(s => s.TrySkipNextAsync());
    public void Previous() => Run(s => s.TrySkipPreviousAsync());

    public void Seek(double fraction)
    {
        if (!CanSeek || Duration <= TimeSpan.Zero) return;
        long ticks = (long)(Duration.Ticks * Math.Clamp(fraction, 0, 1));
        _position = TimeSpan.FromTicks(ticks);
        _positionStamp = DateTime.UtcNow;
        Run(s => s.TryChangePlaybackPositionAsync(ticks));
    }

    private void Run(Func<Session, IAsyncOperation<bool>> op)
    {
        var s = _session;
        if (s == null) return;
        try { _ = op(s); } catch { }
    }

    /// <summary>Pulls a vivid, readable colour out of the album art.</summary>
    private static Color ExtractAccent(BitmapSource src)
    {
        var neutral = Color.FromRgb(0xE8, 0xE8, 0xEE);
        try
        {
            var small = new TransformedBitmap(src, new ScaleTransform(16.0 / src.PixelWidth, 16.0 / src.PixelHeight));
            var conv = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight;
            if (w == 0 || h == 0) return neutral;
            var px = new byte[w * h * 4];
            conv.CopyPixels(px, w * 4, 0);

            double r = 0, g = 0, b = 0, total = 0;
            for (int i = 0; i < px.Length; i += 4)
            {
                double pb = px[i], pg = px[i + 1], pr = px[i + 2];
                double max = Math.Max(pr, Math.Max(pg, pb));
                double min = Math.Min(pr, Math.Min(pg, pb));
                double sat = max <= 0 ? 0 : (max - min) / max;
                double weight = 0.02 + sat * sat * (max / 255.0);
                r += pr * weight; g += pg * weight; b += pb * weight; total += weight;
            }
            if (total <= 0) return neutral;
            r /= total; g /= total; b /= total;

            double mx = Math.Max(r, Math.Max(g, b));
            double mn = Math.Min(r, Math.Min(g, b));
            if (mx < 1 || (mx - mn) / mx < 0.15) return neutral;

            // Lift to a bright value and soften toward white so it reads on black.
            double k = 240.0 / mx;
            r = r * k; g = g * k; b = b * k;
            const double soften = 0.18;
            r += (255 - r) * soften; g += (255 - g) * soften; b += (255 - b) * soften;
            return Color.FromRgb((byte)r, (byte)g, (byte)b);
        }
        catch
        {
            return neutral;
        }
    }
}
