using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindX.Controls;
using WindX.Services;
using Microsoft.Win32;

namespace WindX;

public partial class MainWindow : Window
{
    private enum Toast { None, Volume, Power, Track, Alert }

    private const double PillHeight = 36;
    private const double TabBarHeight = 38;

    private static Settings S => Settings.Current;

    private readonly MediaService _media = new();
    private readonly AudioService _audio = new();
    private readonly BrightnessService _bright = new();
    private readonly RadioService _radios = new();
    private readonly WeatherService _weather = new();
    private readonly ThemeMusic _music = new();
    private string _picLoaded = "";
    private Tray? _tray;
    private SettingsWindow? _settingsWin;

    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _frameTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _toastTimer = new();
    private readonly DispatcherTimer _watchTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(320) };
    private readonly DispatcherTimer _fxTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(40) };
    private DateTime _fxUntil = DateTime.MinValue;
    private int _introStage;           // 0 = a dot dropping in, 1 = logo reveal
    private readonly DispatcherTimer _notesSave = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer _hitTimer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private bool _passThrough;
    private bool _noteShown;
    private int _quoteShift;

    private readonly SpringEase _spring = new();
    private readonly IEasingFunction _smooth = new CubicEase { EasingMode = EasingMode.EaseOut };

    private readonly FrameworkElement[] _views;
    private FrameworkElement? _currentView;
    private double _targetW = -1, _targetH, _targetR;

    // What the island is doing right now
    private bool _expanded;
    private int _page;                 // 0 home, 1 calendar + weather, 2 system
    private Toast _toast = Toast.None;
    private bool _intro;               // playing the launch animation
    private bool _dormant;             // tucked away by auto-hide
    private bool _hidden;              // fully hidden over a fullscreen app
    private bool _locked;              // PC locked: draw nothing at all
    private bool _overlay;             // fullscreen app + "Stats" mode: show CPU/RAM
    private DateTime _lastActive = DateTime.UtcNow;

    private IntPtr _hwnd;
    private int _watchTicks;

    private string? _trackKey;
    private bool _trackSeen;
    private bool? _plugged;
    private bool _hasBattery;          // battery present AND enabled in settings
    private bool _batteryHardware;
    private double _batteryLevel = 1;
    private double _volFrac;
    private double _controlsH = 112;
    private bool _brightLoaded;

    private readonly double[] _levels = new double[5];
    private readonly double[] _phase = { 0.0, 1.7, 3.1, 4.4, 5.9 };
    private readonly double[] _speed = { 7.3, 9.1, 6.2, 10.4, 8.0 };
    private double _smoothedPeak;
    private readonly DateTime _start = DateTime.UtcNow;

    private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0xE8, 0xE8, 0xEE));

    // Glow = stacked translucent pills: (how far each extends past the island, its opacity)
    private static readonly (double Spread, double Opacity)[] GlowLayers = Enumerable.Range(0, 14)
        .Select(i => (1.5 + i * 2.0, 0.085 * Math.Pow(1 - i / 14.0, 1.6)))
        .ToArray();
    private readonly SolidColorBrush _glowBrush = new(Colors.Black);

    // Calendar
    private DateTime _calMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private readonly Border[] _calDots = new Border[42];
    private readonly TextBlock[] _calText = new TextBlock[42];

    public static readonly DependencyProperty IslandRadiusProperty = DependencyProperty.Register(
        nameof(IslandRadius), typeof(double), typeof(MainWindow),
        new PropertyMetadata(18.0, (d, _) => ((MainWindow)d).UpdateClip()));

    public double IslandRadius
    {
        get => (double)GetValue(IslandRadiusProperty);
        set => SetValue(IslandRadiusProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();

        _views = new FrameworkElement[]
        {
            CompactView, ToastView, StatsCompact, IntroView, ExpandedMedia, ExpandedIdle, DayPage, SystemPage, FocusPage, NotesPage,
        };
        foreach (var v in _views)
        {
            v.RenderTransformOrigin = new Point(0.5, 0);
            v.RenderTransform = new ScaleTransform(1, 1);
        }
        _currentView = CompactView;

        CBars.Fill = TBars.Fill = EBars.Fill = _accentBrush;
        ProgFill.Background = _accentBrush;
        IntroX.SetResourceReference(TextElement.ForegroundProperty, "Accent");

        foreach (var (spread, opacity) in GlowLayers)
        {
            GlowHost.Children.Add(new Border
            {
                Background = _glowBrush,
                Opacity = opacity,
                Margin = new Thickness(-spread, -spread + 4, -spread, -spread - 4),
            });
        }
        BuildCalendar();

        ContentHost.SizeChanged += (_, _) => UpdateClip();
        VolTrack.SizeChanged += (_, _) => VolFill.Width = VolTrack.ActualWidth * _volFrac;

        Island.MouseEnter += Island_MouseEnter;
        Island.MouseLeave += Island_MouseLeave;
        Island.MouseLeftButtonUp += (_, _) => SetExpanded(!_expanded);
        Island.MouseRightButtonUp += (_, e) => { e.Handled = true; OpenSettings(); };
        Island.MouseWheel += (_, e) => { _audio.Nudge(e.Delta > 0 ? 0.02f : -0.02f); e.Handled = true; };
        WakeZone.MouseEnter += (_, _) => Wake();
        Island.SizeChanged += (_, _) => PositionNote();
        NoteHost.MouseLeftButtonDown += Note_Down;
        NoteHost.MouseMove += Note_Move;
        NoteHost.MouseLeftButtonUp += Note_Up;
        NoteHost.MouseEnter += (_, e) => { if (!NoteHost.IsMouseCaptured) SwingNote(e.GetPosition(NoteHost).X < NoteHost.ActualWidth / 2 ? 7 : -7); };
        _hitTimer.Tick += (_, _) => UpdateClickThrough();

        // Clicking around inside these pages shouldn't close the island.
        DayPage.MouseLeftButtonUp += (_, e) => e.Handled = true;
        SystemPage.MouseLeftButtonUp += (_, e) => e.Handled = true;
        FocusPage.MouseLeftButtonUp += (_, e) => e.Handled = true;
        NotesPage.MouseLeftButtonUp += (_, e) => e.Handled = true;
        BuildStickies();
        Island.SizeChanged += (_, _) => PlacePin();
        NotesBox.Text = S.Notes;
        NotesHint.Visibility = S.Notes.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        NotesBox.PreviewMouseLeftButtonDown += (_, _) => AllowTyping(true);
        NotesBox.TextChanged += (_, _) =>
        {
            NotesHint.Visibility = NotesBox.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
            NotesSaved.Text = "Saving…";
            _notesSave.Stop();
            _notesSave.Start();
        };
        _notesSave.Tick += (_, _) =>
        {
            _notesSave.Stop();
            S.Notes = NotesBox.Text;
            Settings.Save();
            NotesSaved.Text = "Saved";
            UpdatePin();
        };
        Deactivated += (_, _) =>
        {
            AllowTyping(false);
            if (_expanded && !Island.IsMouseOver) SetExpanded(false);
        };
        TabBar.MouseLeftButtonUp += (_, e) => e.Handled = true;

        VolSlider.UserChanged += v => _audio.SetVolume((float)v);
        BrightSlider.UserChanged += v => _bright.Set(v);
        _radios.Changed += UpdateRadioUi;
        _weather.Changed += () => { UpdateWeatherUi(); ApplyState(); };

        _fxTimer.Tick += (_, _) => FxFrame();
        _clockTimer.Tick += (_, _) => Tick();
        _frameTimer.Tick += (_, _) => Frame();
        _watchTimer.Tick += (_, _) => Watch();
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (Mouse.Captured != null || NotesBox.IsKeyboardFocusWithin) { _collapseTimer.Start(); return; } // dragging a slider or typing a note
            if (_expanded && !Island.IsMouseOver) SetExpanded(false);
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            _toast = Toast.None;
            _lastActive = DateTime.UtcNow;
            ApplyState();
        };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            if (S.ExpandOnHover && Island.IsMouseOver && !_expanded) SetExpanded(true);
        };

        _media.Changed += OnMediaChanged;
        _audio.VolumeChanged += () => { UpdateVolumeUi(); ShowToast(Toast.Volume, 1600); };
        Settings.Changed += ApplySettings;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    // ------------------------------------------------------------------ lifecycle

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // Tool window (no Alt+Tab entry) that never steals focus from what you're doing.
        long ex = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
        Reposition();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _tray = new Tray(OpenSettings, Close);
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        _intro = S.IntroAnimation;
        ApplySettings();
        UpdateVolumeUi();

        _clockTimer.Start();
        _watchTimer.Start();
        if (App.SnapshotDir == null) _hitTimer.Start();

        Animate(Root, OpacityProperty, 1, 350, _smooth);
        if (_intro) PlayIntro();

        await _media.StartAsync();
        _ = _weather.RefreshAsync();
        if (App.SnapshotDir is { } dir) await RunSnapshotsAsync(dir);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _settingsWin?.Close();
        _music.Stop();
        _tray?.Dispose();
        _audio.Dispose();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        bool locked = e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect;
        bool unlocked = e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect;
        if (!locked && !unlocked) return;
        Dispatcher.BeginInvoke(() =>
        {
            _locked = locked;
            if (locked)
            {
                _frameTimer.Stop();
                _fxTimer.Stop();
                ReleaseMemory();
            }
            else
            {
                EnsureFrameTimer();
                EnsureFx();
            }
        });
    }

    private double _wantHeight;

    /// <summary>
    /// Keep the (transparent) window only as tall as the island needs. Every animation frame redraws
    /// the whole window, so a pill-sized window costs a fraction of a full-height one.
    /// </summary>
    private void FitHeight(double need)
    {
        need = Math.Clamp(Math.Ceiling(need), 120, 470);
        _wantHeight = need;
        if (need >= Height) { Height = need; return; }
        // Shrink only after the collapse animation has finished, so nothing gets clipped mid-spring.
        After(750, () => { if (_wantHeight < Height) Height = _wantHeight; });
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Reposition);

    private void Reposition()
    {
        double at = S.Position == "Left" ? 0.22 : S.Position == "Right" ? 0.78 : 0.5;
        Left = Math.Clamp(SystemParameters.PrimaryScreenWidth * at - Width / 2, 0, Math.Max(0, SystemParameters.PrimaryScreenWidth - Width));
        Top = 0;
    }

    // ------------------------------------------------------------------ intro: wind blows the logo in

    private void PlayIntro()
    {
        // 1. A dot falls from above the screen and lands with a bounce.
        IslandDrop.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-90, 0, TimeSpan.FromMilliseconds(720)) { EasingFunction = new BounceEase { Bounces = 2, Bounciness = 2.6, EasingMode = EasingMode.EaseOut } });

        // 2. It stretches into a pill while the wind draws the logo.
        After(560, () => { _introStage = 1; ApplyState(); });
        IntroLogo.BeginAnimation(LogoMark.ProgressProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(950)) { BeginTime = TimeSpan.FromMilliseconds(760), EasingFunction = _smooth });
        IntroWord.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(1200), EasingFunction = _smooth });

        // Streaks of wind sweeping left to right, in the brand's paper white and ember.
        var rnd = new Random();
        Color[] colors = { Logo.Paper, Logo.Ember, Logo.Paper };
        for (int i = 0; i < 11; i++)
        {
            var c = colors[i % 3];
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(235, c.R, c.G, c.B), 0.78));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
            brush.Freeze();

            double w = 90 + rnd.NextDouble() * 190, h = 1.5 + rnd.NextDouble() * 2;
            var streak = new Rectangle { Width = w, Height = h, RadiusX = h / 2, RadiusY = h / 2, Fill = brush, Opacity = 0 };
            var move = new TranslateTransform(-w, 0);
            streak.RenderTransform = move;
            Canvas.SetTop(streak, 4 + rnd.NextDouble() * 52);
            WindLayer.Children.Add(streak);

            var delay = TimeSpan.FromMilliseconds(520 + i * 80 + rnd.Next(60));
            var dur = TimeSpan.FromMilliseconds(650 + rnd.Next(450));
            move.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(-w, Width + 40, dur) { BeginTime = delay, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
            var fade = new DoubleAnimationUsingKeyFrames { BeginTime = delay, Duration = dur };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.3)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            streak.BeginAnimation(OpacityProperty, fade);
        }

        // 3. A little pop, then it settles into the everyday pill.
        After(2500, () => ScaleIsland(1.1));
        After(2700, () =>
        {
            ScaleIsland(1);
            WindLayer.Children.Clear();
            _intro = false;
            _lastActive = DateTime.UtcNow;
            _fxUntil = DateTime.UtcNow.AddSeconds(4);
            ApplyState();
        });
    }

    private static void After(int ms, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); action(); };
        timer.Start();
    }

    // ------------------------------------------------------------------ state machine

    private void ApplyState()
    {
        FrameworkElement? view;
        double w, h, r;
        bool controls = false;

        if (_intro)
        {
            if (_introStage == 0) { view = null; w = PillHeight; }
            else { view = IntroView; w = 138; }
            h = PillHeight; r = PillHeight / 2;
        }
        else if (_expanded)
        {
            w = 400; r = 42;
            switch (_page)
            {
                case 1: view = DayPage; h = DayPage.Height; break;
                case 2: view = SystemPage; h = SystemPage.Height; break;
                case 3: view = FocusPage; h = FocusPage.Height; break;
                case 4: view = NotesPage; h = NotesPage.Height; break;
                default:
                    double top;
                    if (_media.HasMedia) { view = ExpandedMedia; top = ExpandedMedia.Height; }
                    else { view = ExpandedIdle; top = ExpandedIdle.Height; }
                    ControlsPanel.Margin = new Thickness(0, top, 0, 0);
                    h = top + _controlsH;
                    controls = _controlsH > 0;
                    break;
            }
            h += TabBarHeight;
        }
        else if (_toast != Toast.None)
        {
            view = ToastView;
            w = _toast is Toast.Track or Toast.Alert ? 340 : 280;
            h = PillHeight; r = PillHeight / 2;
        }
        else if (_overlay)
        {
            view = StatsCompact; w = 196; h = PillHeight; r = PillHeight / 2;
        }
        else if (_dormant)
        {
            // Tucked away: a thin sliver that wakes on hover.
            view = null; w = 76; h = 7; r = 3.5;
        }
        else
        {
            view = CompactView;
            w = _media.HasMedia ? 200 : 78 + (WeatherShown ? 54 : 0) + (_hasBattery ? 92 : 0);
            h = PillHeight; r = PillHeight / 2;
        }

        ShowView(view);
        BtnDismiss.Visibility = ReferenceEquals(view, ExpandedMedia) ? Visibility.Visible : Visibility.Collapsed;
        bool busy = Fx.Effect is IslandEffect.Overgrown or IslandEffect.Battleground or IslandEffect.Signal or IslandEffect.Race or IslandEffect.Cats or IslandEffect.Galaxy or IslandEffect.Synthwave;
        Animate(Fx, OpacityProperty, _expanded && busy ? 0.32 : 1, 300, _smooth);
        Fade(ControlsPanel, controls);
        Fade(TabBar, _expanded && !_intro);
        WakeZone.Visibility = _dormant && !_expanded ? Visibility.Visible : Visibility.Collapsed;
        UpdatePin();
        Animate(BgName, OpacityProperty, _expanded && !_intro && BgName.Text.Length > 0 ? 0.13 : 0, 350, _smooth);
        ShowNote();

        AnimateIsland(w, h, r);
        if (App.SnapshotDir == null) FitHeight(8 + h * S.Scale + 70 + (S.ShowNote && !_expanded ? 200 : 0) + (S.PinNote && !_expanded ? 40 : 0));
        UpdateGlow();
        EnsureFrameTimer();
        EnsureFx();
    }

    private void Fade(FrameworkElement el, bool on)
    {
        el.IsHitTestVisible = on;
        el.BeginAnimation(OpacityProperty, new DoubleAnimation(on ? 1 : 0, TimeSpan.FromMilliseconds(on ? 280 : 100))
        {
            BeginTime = TimeSpan.FromMilliseconds(on ? 140 : 0),
            EasingFunction = _smooth,
        });
    }

    private void ShowView(FrameworkElement? view)
    {
        if (ReferenceEquals(view, _currentView)) return;
        _currentView = view;

        foreach (var v in _views)
        {
            bool on = ReferenceEquals(v, view);
            v.IsHitTestVisible = on;
            if (on)
            {
                v.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260))
                {
                    BeginTime = TimeSpan.FromMilliseconds(90),
                    EasingFunction = _smooth,
                });
                var st = (ScaleTransform)v.RenderTransform;
                var grow = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = _smooth };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

                // The page's pieces rise into place one after another.
                if (ReferenceEquals(v, ExpandedMedia)) Stagger(MediaBody.Children);
                else if (ReferenceEquals(v, ExpandedIdle)) Stagger(IdleBody.Children);
                else if (ReferenceEquals(v, DayPage)) Stagger(DayBody.Children);
                else if (ReferenceEquals(v, SystemPage)) Stagger(RingRow.Children.Cast<UIElement>().Append(SysLine));
                else if (ReferenceEquals(v, FocusPage)) Stagger(FocusBody.Children);
                else if (ReferenceEquals(v, NotesPage)) Stagger(NotesBody.Children);
                if (ReferenceEquals(v, ExpandedMedia) || ReferenceEquals(v, ExpandedIdle)) Stagger(ControlsBody.Children, 300);
            }
            else
            {
                Animate(v, OpacityProperty, 0, 110, _smooth);
            }
        }
    }

    private void Stagger(System.Collections.IEnumerable items, int startMs = 150)
    {
        int i = 0;
        foreach (UIElement child in items)
        {
            if (child.Visibility != Visibility.Visible) continue;
            if (child.RenderTransform is not TranslateTransform shift)
                child.RenderTransform = shift = new TranslateTransform();
            var delay = TimeSpan.FromMilliseconds(startMs + i * 60);
            shift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(560)) { BeginTime = delay, EasingFunction = _smooth });
            child.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = delay });
            i++;
        }
    }

    private void AnimateIsland(double w, double h, double r)
    {
        if (w == _targetW && h == _targetH && r == _targetR) return;
        _targetW = w; _targetH = h; _targetR = r;

        Animate(Island, WidthProperty, w, 620, _spring);
        Animate(Island, HeightProperty, h, 620, _spring);
        Animate(this, IslandRadiusProperty, r, 400, _smooth);
    }

    private void UpdateClip()
    {
        double w = ContentHost.ActualWidth, h = ContentHost.ActualHeight;
        if (w <= 0 || h <= 0) return;
        double r = Math.Min(IslandRadius, Math.Min(w, h) / 2);
        Shell.CornerRadius = new CornerRadius(r);
        Sheen.CornerRadius = new CornerRadius(Math.Max(0, r - 1));
        for (int i = 0; i < GlowLayers.Length; i++)
            ((Border)GlowHost.Children[i]).CornerRadius = new CornerRadius(r + GlowLayers[i].Spread);
        ContentHost.Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
    }

    private void UpdateGlow()
    {
        // Album-art coloured glow while music plays; the theme's own shadow otherwise.
        bool lit = _media.HasMedia && _media.IsPlaying && !_dormant;
        var theme = Theme.Current;
        Color color = lit ? CurrentAccent() : theme.Glow != null ? Theme.C(theme.Glow) : Colors.Black;
        double strength = theme.IsLight ? (lit ? 0.8 : 0.4) : (lit || theme.Glow != null ? 0.8 : 1.0);
        if (_dormant && !_expanded && _toast == Toast.None) strength *= 0.35;

        _glowBrush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(color, TimeSpan.FromMilliseconds(600)) { EasingFunction = _smooth });
        Animate(GlowHost, OpacityProperty, strength, 600, _smooth);
    }

    // ------------------------------------------------------------------ living backdrop

    private bool FxWanted =>
        (Fx.Effect != IslandEffect.None || BgPic.Source != null) && !_hidden && !_locked && !(_dormant && !_expanded && _toast == Toast.None)
        && (_expanded || _intro || S.EffectWhenIdle || Island.IsMouseOver || DateTime.UtcNow < _fxUntil);

    private void EnsureFx()
    {
        if (FxWanted && !_fxTimer.IsEnabled) _fxTimer.Start();
    }

    private void FxFrame()
    {
        if (!FxWanted)
        {
            _fxTimer.Stop();
            return;
        }
        // Smooth while you're looking at it, a slow drift when it's just sitting there.
        bool active = _expanded || _intro || Island.IsMouseOver;
        var interval = TimeSpan.FromMilliseconds(active ? 40 : 250);   // 25 fps in use, 4 fps at rest   // 25 fps in use, ~5 fps at rest
        if (_fxTimer.Interval != interval) _fxTimer.Interval = interval;

        if (BgPic.Source != null && active)   // the picture only drifts while you're looking at the open island
        {
            // A slow push-in and drift, so a still picture feels like footage.
            double t = (DateTime.UtcNow - _start).TotalSeconds;
            BgPicScale.ScaleX = BgPicScale.ScaleY = 1.14 + 0.06 * Math.Sin(t * 0.13);
            BgPicShift.X = Math.Sin(t * 0.09) * 14;
            BgPicShift.Y = Math.Cos(t * 0.11) * 7;
        }

        // Fire leaps and water swells with the music.
        double energy = _media.HasMedia && _media.IsPlaying ? Math.Clamp(_smoothedPeak * 1.4, 0, 1) : 0.3;
        Fx.Step((DateTime.UtcNow - _start).TotalSeconds, energy);
    }

    /// <summary>Album-art colour when enabled and available, otherwise the theme's accent.</summary>
    private Color CurrentAccent()
    {
        var theme = Theme.Current;
        var neutral = Color.FromRgb(0xE8, 0xE8, 0xEE);
        if (S.AlbumAccent && _media.HasMedia && _media.Art != null && _media.Accent != neutral)
        {
            var c = _media.Accent;
            // Bright album colours wash out on a light island: deepen them.
            return theme.IsLight ? Color.FromRgb((byte)(c.R * 0.6), (byte)(c.G * 0.6), (byte)(c.B * 0.6)) : c;
        }
        return theme.AccentColor;
    }

    private void ShowToast(Toast kind, int ms)
    {
        if (_expanded || _hidden || _intro) return;
        if (kind == Toast.Volume && !S.VolumePopups) return;
        if (kind == Toast.Track && !S.TrackPopups) return;
        if (kind == Toast.Power && !S.PowerPopups) return;

        _toast = kind;
        TVolume.Visibility = kind == Toast.Volume ? Visibility.Visible : Visibility.Collapsed;
        TPower.Visibility = kind == Toast.Power ? Visibility.Visible : Visibility.Collapsed;
        TTrack.Visibility = kind == Toast.Track ? Visibility.Visible : Visibility.Collapsed;
        TAlert.Visibility = kind == Toast.Alert ? Visibility.Visible : Visibility.Collapsed;

        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromMilliseconds(ms);
        _toastTimer.Start();
        ApplyState();
    }

    private static void Animate(IAnimatable target, DependencyProperty dp, double to, int ms, IEasingFunction? ease = null)
    {
        target.BeginAnimation(dp, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
    }

    // ------------------------------------------------------------------ input

    private void Island_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        Wake();
        EnsureFx();
        if (!_expanded)
        {
            ScaleIsland(1.05);
            Sfx.Play("touch");
            if (S.ExpandOnHover) { _hoverTimer.Stop(); _hoverTimer.Start(); }
        }
    }

    private void Island_MouseLeave(object sender, MouseEventArgs e)
    {
        ScaleIsland(1);
        _hoverTimer.Stop();
        _lastActive = DateTime.UtcNow;
        if (_expanded)
        {
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }
        else
        {
            CheckAutoHide();
        }
    }

    private void ScaleIsland(double s)
    {
        Animate(IslandScale, ScaleTransform.ScaleXProperty, s, 380, _spring);
        Animate(IslandScale, ScaleTransform.ScaleYProperty, s, 380, _spring);
    }

    private void SetExpanded(bool on)
    {
        if (_intro) return;
        if (on != _expanded) Sfx.Play(on ? "open" : "close");
        _expanded = on;
        _music.Update(on, _hidden);
        _dormant = false;
        _lastActive = DateTime.UtcNow;
        _hoverTimer.Stop();
        _toastTimer.Stop();
        _toast = Toast.None;
        ScaleIsland(1);
        if (on)
        {
            _page = 0;
            TabHome.IsChecked = true;
            UpdateClockAndBattery();
            UpdateVolumeUi();
            _ = RefreshControlsAsync();
        }
        ApplyState();
        if (!on) AllowTyping(false);
        if (!on && !Island.IsMouseOver) CheckAutoHide();
        if (!on) After(4000, () => { if (!_expanded && _settingsWin == null) ReleaseMemory(); });
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        Sfx.Play("tab");
        SelectPage(int.Parse((string)((FrameworkElement)sender).Tag, CultureInfo.InvariantCulture));
    }

    private void SelectPage(int page)
    {
        _page = page;
        (page == 1 ? TabDay : page == 2 ? TabSystem : page == 3 ? TabFocus : page == 4 ? TabNotes : TabHome).IsChecked = true;
        if (page == 3) UpdateFocusUi();
        if (_page == 1)
        {
            _calMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            RenderCalendar();
            UpdateWeatherUi();
            _ = _weather.RefreshAsync();
        }
        else if (_page == 2)
        {
            SystemStats.Sample();
            UpdateStatsUi();
        }
        ApplyState();
    }

    private void Gear_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void BtnPrev_Click(object sender, RoutedEventArgs e) => _media.Previous();

    private void BtnDismiss_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _media.Dismiss();
        Sfx.Play("close");
    }

    /// <summary>
    /// The island normally never takes keyboard focus (so it can't steal it from your apps).
    /// The Notes box is the one exception: while you type there, the island accepts the keyboard.
    /// </summary>
    private void AllowTyping(bool on)
    {
        if (_hwnd == IntPtr.Zero) return;
        long ex = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
        long want = on ? ex & ~WS_EX_NOACTIVATE : ex | WS_EX_NOACTIVATE;
        if (want != ex) SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(want));
        if (on)
        {
            Activate();
            NotesBox.Focus();
        }
        else if (NotesBox.IsKeyboardFocusWithin)
        {
            Keyboard.ClearFocus();
            if (_notesSave.IsEnabled) { _notesSave.Stop(); S.Notes = NotesBox.Text; Settings.Save(); }
        }
    }
    private void BtnPlay_Click(object sender, RoutedEventArgs e) => _media.TogglePlay();
    private void BtnNext_Click(object sender, RoutedEventArgs e) => _media.Next();

    private void ProgHit_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // don't collapse the island
        double w = ProgHit.ActualWidth;
        if (w > 0) _media.Seek(e.GetPosition(ProgHit).X / w);
    }

    // ------------------------------------------------------------------ hanging note

    private static readonly string[] Quotes =
    {
        "Small steps every day.", "Done is better than perfect.", "Start where you are.", "One thing at a time.",
        "You've survived every bad day so far.", "Make it work, then make it better.", "Discipline beats motivation.",
        "Progress, not perfection.", "Future you is watching. Make them proud.", "Hard now, easy later.",
        "Breathe. Then begin.", "Dream big. Start small. Act now.", "Be the person you needed.",
        "Consistency compounds.", "You don't have to be great to start.", "Stay curious.", "Focus on the next right step.",
        "Rest is part of the work.", "Energy flows where attention goes.", "Do it tired. Do it anyway.",
        "Show up. That's most of it.", "Slow is smooth. Smooth is fast.", "Your only limit is you.", "Keep going.",
    };

    private bool NoteWanted =>
        S.ShowNote && !_expanded && !_intro && !_dormant && !_hidden && !_overlay && _toast == Toast.None;

    private void ShowNote()
    {
        bool on = NoteWanted;
        if (on == _noteShown) return;
        _noteShown = on;
        NoteHost.IsHitTestVisible = on;
        Animate(NoteHost, OpacityProperty, on ? 1 : 0, on ? 420 : 140, _smooth);
        if (on)
        {
            PositionNote();
            SwingNote();
        }
    }

    /// <summary>Text and material of the tag. Click the tag to see the next quote.</summary>
    private void UpdateNote()
    {
        var now = DateTime.Now;
        NoteText.Text = S.NoteMode == "Custom" && S.NoteText.Length > 0
            ? S.NoteText
            : Quotes[(now.DayOfYear * 24 + now.Hour + _quoteShift) % Quotes.Length];

        static Brush Wash(double angle, params (string Hex, double At)[] stops)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = angle == 0 ? new Point(1, 0) : angle == 90 ? new Point(0, 1) : new Point(1, 1) };
            foreach (var (hex, at) in stops) b.GradientStops.Add(new GradientStop(Theme.C(hex), at));
            return b;
        }

        // Material of the tag: plain stock, a little painting, or the island's own theme.
        (Brush Bg, string Edge, string Ink)? look = S.NoteStyle switch
        {
            "Sticky" => (new SolidColorBrush(Theme.C("#FFE566")), "#E3C73C", "#3A3300"),
            "Paper" => (new SolidColorBrush(Theme.C("#FBF8F1")), "#DDD6C8", "#22201C"),
            "Starry" => (Wash(45, ("#0B1E5B", 0), ("#1D4E9E", 0.5), ("#3F7FCC", 0.78), ("#F2C230", 1)), "#F2C230", "#FFF6C8"),
            "Wave" => (Wash(90, ("#F3E9D2", 0), ("#E6DCC2", 0.52), ("#2E6BB0", 0.6), ("#12305F", 1)), "#12305F", "#1B2B4A"),
            "Sunset" => (Wash(90, ("#FF7E5F", 0), ("#FEB47B", 0.55), ("#FFD8A8", 1)), "#E0603F", "#4A1A0A"),
            "Blueprint" => (Wash(45, ("#0E3A7A", 0), ("#15509F", 1)), "#9CC4FF", "#E4F0FF"),
            "Theme" => null,
            _ => (new SolidColorBrush(Theme.C("#C9A56E")), "#A6824C", "#3B2A14"),   // cardboard
        };
        if (look is { } l)
        {
            NoteCard.Background = l.Bg;
            NoteCard.BorderBrush = new SolidColorBrush(Theme.C(l.Edge));
            NoteText.Foreground = new SolidColorBrush(Theme.C(l.Ink));
        }
        else
        {
            NoteCard.SetResourceReference(Border.BackgroundProperty, "IslandBg");
            NoteCard.SetResourceReference(Border.BorderBrushProperty, "IslandBorder");
            NoteText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
        }
        ShowNote();
    }

    // Grab the tag and pull it sideways; let go and it swings back like it's really on a thread.
    private Point _noteGrab;
    private bool _noteDragged;

    private void Note_Down(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _noteGrab = e.GetPosition(this);
        _noteDragged = false;
        NoteHost.CaptureMouse();
    }

    private void Note_Move(object sender, MouseEventArgs e)
    {
        if (!NoteHost.IsMouseCaptured) return;
        double dx = e.GetPosition(this).X - _noteGrab.X;
        if (Math.Abs(dx) > 3) _noteDragged = true;
        if (!_noteDragged) return;
        NoteSwing.BeginAnimation(RotateTransform.AngleProperty, null);
        NoteSwing.Angle = Math.Clamp(-Math.Atan2(dx, 75) * 180 / Math.PI, -60, 60);
    }

    private void Note_Up(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (!NoteHost.IsMouseCaptured) return;
        NoteHost.ReleaseMouseCapture();
        if (_noteDragged)
        {
            SwingNote(NoteSwing.Angle);
        }
        else
        {
            Sfx.Play("note");
            _quoteShift++;   // a plain click turns to the next quote
            UpdateNote();
            SwingNote();
        }
    }

    private void PositionNote()
    {
        // Hangs from the island's lower-right corner.
        double right = Width / 2 + Island.ActualWidth * S.Scale / 2;
        NoteHost.Margin = new Thickness(Math.Max(0, right - 40 - NoteHost.Width / 2), 8 + Island.ActualHeight * S.Scale - 4, 0, 0);
    }

    /// <summary>A few seconds of swaying, then it hangs still (so it costs nothing at rest).</summary>
    private void SwingNote(double from = -9)
    {
        // A pendulum losing energy: each swing is smaller than the last, ending at a slight resting tilt.
        var sway = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1600 + Math.Abs(from) * 60) };
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        double[] swing = { 1, -0.72, 0.5, -0.33, 0.2, -0.1 };
        sway.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        for (int i = 1; i < swing.Length; i++)
            sway.KeyFrames.Add(new EasingDoubleKeyFrame(from * swing[i], KeyTime.FromPercent(i / (double)swing.Length), ease));
        sway.KeyFrames.Add(new EasingDoubleKeyFrame(2, KeyTime.FromPercent(1), ease));
        NoteSwing.BeginAnimation(RotateTransform.AngleProperty, sway);
    }

    // ------------------------------------------------------------------ sticky notes

    private readonly Border[] _stickies = new Border[4];
    private readonly TextBlock[] _stickyText = new TextBlock[4];
    private readonly RotateTransform[] _stickyTilt = new RotateTransform[4];
    private readonly bool[] _stickyOn = new bool[4];
    private bool _pinShown;
    private static readonly double[] StickyAngle = { -5, 4, 5, -4 };

    private static (string Paper, string Ink) StickyColors(string name) => name switch
    {
        "Pink" => ("#FFB3CF", "#4A1030"),
        "Blue" => ("#A8D8FF", "#0B2A4A"),
        "Green" => ("#B8F0A0", "#163A0C"),
        "Orange" => ("#FFC48A", "#4A2200"),
        _ => ("#FFE566", "#3A3300"),
    };

    private void BuildStickies()
    {
        PinNote.Visibility = Visibility.Collapsed;   // replaced by the four below
        int at = Root.Children.IndexOf(Island);
        for (int i = 0; i < 4; i++)
        {
            var text = new TextBlock { FontFamily = new FontFamily("Ink Free, Segoe Print, Segoe UI"), FontSize = 13, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 54, TextTrimming = TextTrimming.CharacterEllipsis };
            var tape = new Border { Width = 34, Height = 9, Background = new SolidColorBrush(Color.FromArgb(0x55, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -12, 0, 0), RenderTransform = new RotateTransform(i % 2 == 0 ? 8 : -10) };
            var inner = new Grid();
            inner.Children.Add(tape);
            inner.Children.Add(text);
            var tilt = new RotateTransform(StickyAngle[i]);
            var card = new Border
            {
                Width = 128, MinHeight = 34, CornerRadius = new CornerRadius(3),
                // Extra room on the side that tucks under the island, so no text hides behind it.
                Padding = i < 2 ? new Thickness(10, 7, 22, 8) : new Thickness(22, 7, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Opacity = 0, IsHitTestVisible = false, Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.1), RenderTransform = tilt, Child = inner,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 290, Opacity = 0.35 },
            };
            card.MouseLeftButtonUp += (_, e) => { e.Handled = true; OpenSettings(); _settingsWin?.Go(8); };
            _stickies[i] = card; _stickyText[i] = text; _stickyTilt[i] = tilt;
            Root.Children.Insert(at, card);
        }
    }

    private void UpdatePin()
    {
        var items = S.Stickies.Where(s => s.Text.Length > 0).Take(4).ToList();
        if (items.Count == 0)
        {
            // No stickies written yet: fall back to the first line of the Notes tab.
            string first = (S.Notes ?? "").Trim();
            int nl = first.IndexOfAny(new[] { '\r', '\n' });
            if (nl > 0) first = first[..nl];
            if (first.Length > 60) first = first[..60] + "…";
            if (first.Length > 0) items.Add(new StickyItem { Text = first });
        }
        bool show = S.PinNote && !_expanded && !_intro && !_dormant && !_hidden && !_overlay;
        _pinShown = false;
        for (int i = 0; i < 4; i++)
        {
            bool on = show && i < items.Count;
            if (on)
            {
                var (paper, ink) = StickyColors(items[i].Color);
                _stickies[i].Background = new SolidColorBrush(Theme.C(paper));
                _stickyText[i].Foreground = new SolidColorBrush(Theme.C(ink));
                _stickyText[i].Text = items[i].Text;
                _pinShown = true;
            }
            if (on == _stickyOn[i]) continue;
            _stickyOn[i] = on;
            _stickies[i].IsHitTestVisible = on;
            Animate(_stickies[i], OpacityProperty, on ? 1 : 0, on ? 380 : 140, _smooth);
            if (on)
            {
                // Slaps on with a little wobble, one after another.
                double rest = StickyAngle[i];
                var wob = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700), BeginTime = TimeSpan.FromMilliseconds(i * 90) };
                foreach (var (a, p) in new[] { (rest - 10, 0.0), (rest + 6, 0.35), (rest - 3, 0.6), (rest + 1, 0.8), (rest, 1.0) })
                    wob.KeyFrames.Add(new EasingDoubleKeyFrame(a, KeyTime.FromPercent(p), new SineEase()));
                _stickyTilt[i].BeginAnimation(RotateTransform.AngleProperty, wob);
            }
        }
        PlacePin();
    }

    /// <summary>First two stick to the island's left end, the next two to its right end.</summary>
    private void PlacePin()
    {
        if (_stickies[0] == null) return;
        double half = Island.ActualWidth * S.Scale / 2, w = _stickies[0].Width;
        for (int i = 0; i < 4; i++)
        {
            double x = i < 2
                ? Width / 2 - half - w + 16 - i * (w - 8)
                : Width / 2 + half - 16 + (i - 2) * (w - 8);
            _stickies[i].Margin = new Thickness(Math.Clamp(x, 2, Width - w - 2), 10 + (i % 2) * 4, 0, 0);
        }
    }
    // ------------------------------------------------------------------ click-through

    /// <summary>
    /// The window is wider than the pill (room to expand, plus the glow). Everything except the island
    /// itself must let clicks through to whatever is underneath, e.g. a browser's tab strip.
    /// </summary>
    private void UpdateClickThrough()
    {
        if (_hwnd == IntPtr.Zero || !GetCursorPos(out POINT p)) return;
        bool over = Mouse.Captured != null
            || (!_hidden && (Over(Island, p, 3) || (_noteShown && Over(NoteHost, p, 0)) || (_pinShown && _stickies.Any(s => s.Opacity > 0.5 && Over(s, p, 2))) || (_dormant && Over(WakeZone, p, 0))));
        if (over != _passThrough) return;
        _passThrough = !over;
        long ex = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
        ex = _passThrough ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT;
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(ex));
    }

    private static bool Over(FrameworkElement el, POINT p, double pad)
    {
        try
        {
            if (!el.IsVisible || el.ActualWidth <= 0) return false;
            var a = el.PointToScreen(new Point(-pad, -pad));
            var b = el.PointToScreen(new Point(el.ActualWidth + pad, el.ActualHeight + pad));
            return p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X) && p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ timer, pomodoro, reminders

    private bool _tmRunning, _tmPomodoro;
    private bool _tmBreak;
    private int _tmRounds;                                  // focus sessions finished in this pomodoro run
    private TimeSpan _tmTotal = TimeSpan.FromMinutes(25), _tmLeft = TimeSpan.FromMinutes(25);
    private DateTime _tmEnd;

    private void FocusPreset_Click(object sender, RoutedEventArgs e)
    {
        string tag = (string)((FrameworkElement)sender).Tag;
        _tmPomodoro = tag == "P";
        _tmBreak = false;
        _tmRounds = 0;
        _tmRunning = false;
        _tmTotal = _tmLeft = TimeSpan.FromMinutes(_tmPomodoro ? 25 : int.Parse(tag, CultureInfo.InvariantCulture));
        Sfx.Play("tab");
        UpdateFocusUi();
    }

    private void FocusStart_Click(object sender, RoutedEventArgs e)
    {
        if (_tmLeft <= TimeSpan.Zero) _tmLeft = _tmTotal;
        _tmRunning = !_tmRunning;
        if (_tmRunning) _tmEnd = DateTime.UtcNow + _tmLeft;
        Sfx.Play("tab");
        UpdateFocusUi();
        UpdateClockAndBattery();
    }

    private void FocusReset_Click(object sender, RoutedEventArgs e)
    {
        _tmRunning = false;
        _tmBreak = false;
        _tmRounds = 0;
        _tmTotal = _tmLeft = TimeSpan.FromMinutes(_tmPomodoro ? 25 : _tmTotal.TotalMinutes);
        UpdateFocusUi();
        UpdateClockAndBattery();
    }

    private void FocusTick()
    {
        if (!_tmRunning) return;
        _tmLeft = _tmEnd - DateTime.UtcNow;
        if (_tmLeft > TimeSpan.Zero)
        {
            if (_expanded && _page == 3) UpdateFocusUi();
            return;
        }

        _tmLeft = TimeSpan.Zero;
        _tmRunning = false;
        if (_tmPomodoro)
        {
            // Focus 25 → break 5, and a long 15-minute break after every fourth focus session. Runs on by itself.
            if (!_tmBreak) _tmRounds++;
            _tmBreak = !_tmBreak;
            bool longBreak = _tmBreak && _tmRounds % 4 == 0;
            _tmTotal = _tmLeft = TimeSpan.FromMinutes(_tmBreak ? (longBreak ? 15 : 5) : 25);
            _tmEnd = DateTime.UtcNow + _tmLeft;
            _tmRunning = true;
            Alert(_tmBreak ? (longBreak ? "Long break: 15 minutes. You earned it." : "Break time: 5 minutes.") : "Back to focus: 25 minutes.");
        }
        else
        {
            Alert("Time's up!");
        }
        UpdateFocusUi();
    }

    private void UpdateFocusUi()
    {
        FocusTime.Text = Fmt(_tmLeft);
        FocusRing.Value = _tmTotal.TotalSeconds > 0 ? 1 - _tmLeft.TotalSeconds / _tmTotal.TotalSeconds : 0;
        FocusMode.Text = _tmPomodoro ? (_tmBreak ? "Break" : $"Focus {_tmRounds + 1}") : "Timer";
        FocusStart.Content = _tmRunning ? "\uE769" : "\uE768";
        FocusHint.Text = _tmPomodoro ? "25 min focus, 5 min break, long break after 4." : _tmRunning ? "Counting down in the pill too." : "Pick a length, then press play.";
    }

    /// <summary>Something the user must notice: the island becomes a banner, pops, and chimes.</summary>
    private void Alert(string text)
    {
        if (_expanded) SetExpanded(false);
        _dormant = false;
        TAlertText.Text = text;
        Sfx.Play("alarm");
        ShowToast(Toast.Alert, 10000);
        ScaleIsland(1.12);
        After(450, () => ScaleIsland(1));
        _fxUntil = DateTime.UtcNow.AddSeconds(10);
    }

    private string _reminderMinute = "";

    private void CheckReminders()
    {
        var now = DateTime.Now;
        string minute = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (minute == _reminderMinute || S.Reminders.Count == 0) return;   // look once per minute
        _reminderMinute = minute;
        string today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var due = S.Reminders.Where(r => r.Time == minute && r.LastFired != today).ToList();
        if (due.Count == 0) return;
        foreach (var r in due)
        {
            r.LastFired = today;
            if (!r.Repeat) S.Reminders.Remove(r);
        }
        Settings.Save();
        Alert(string.Join("  ·  ", due.Select(r => r.Text)));
    }

    // ------------------------------------------------------------------ auto-hide

    private void Wake()
    {
        _lastActive = DateTime.UtcNow;
        if (!_dormant) return;
        _dormant = false;
        ApplyState();
    }

    private void CheckAutoHide()
    {
        if (S.AutoHideSeconds < 0 || _dormant || _expanded || _intro || _overlay || _hidden) return;
        if (_toast != Toast.None || Island.IsMouseOver) return;
        if ((DateTime.UtcNow - _lastActive).TotalSeconds < S.AutoHideSeconds) return;
        _dormant = true;
        ApplyState();
    }

    // ------------------------------------------------------------------ quick controls

    private async Task RefreshControlsAsync()
    {
        bool before = BrightnessShown();
        await _bright.LoadAsync();
        _brightLoaded = true;
        if (!BrightSlider.IsDragging) BrightSlider.Value = _bright.Level;
        if (BrightnessShown() != before)
        {
            UpdateControlsLayout();
            if (_expanded) ApplyState();
        }

        await _radios.LoadAsync();
        UpdateRadioUi();
    }

    // Until WMI has answered, assume a laptop (has a battery) can dim its screen.
    private bool BrightnessShown() => S.ShowBrightness && (_brightLoaded ? _bright.Supported : _batteryHardware);

    /// <summary>Shows only the sliders/buttons enabled in Settings and resizes the panel to fit.</summary>
    private void UpdateControlsLayout()
    {
        static Visibility Vis(bool on) => on ? Visibility.Visible : Visibility.Collapsed;
        var star = new GridLength(1, GridUnitType.Star);
        var zero = new GridLength(0);

        bool bright = BrightnessShown(), vol = S.ShowVolume;
        BrightPanel.Visibility = Vis(bright);
        VolPanel.Visibility = Vis(vol);
        BrightCol.Width = bright ? star : zero;
        VolCol.Width = vol ? star : zero;
        SliderGap.Width = new GridLength(bright && vol ? 18 : 0);

        ChipWifi.Visibility = Vis(S.ShowWifi);
        ChipBt.Visibility = Vis(S.ShowBluetooth);
        ChipMute.Visibility = Vis(S.ShowMute);
        ChipSnip.Visibility = Vis(S.ShowSnip);
        int chips = (S.ShowWifi ? 1 : 0) + (S.ShowBluetooth ? 1 : 0) + (S.ShowMute ? 1 : 0) + (S.ShowSnip ? 1 : 0);
        ChipGrid.Columns = Math.Max(chips, 1);

        bool sliders = bright || vol;
        SliderRow.Height = new GridLength(sliders ? 24 : 0);
        GapRow.Height = new GridLength(sliders && chips > 0 ? 12 : 0);
        ChipRow.Height = new GridLength(chips > 0 ? 38 : 0);

        // Bottom padding is small because the tab bar sits right underneath.
        _controlsH = sliders || chips > 0
            ? 14 + SliderRow.Height.Value + GapRow.Height.Value + ChipRow.Height.Value + 10
            : 0;
        ControlsPanel.Height = Math.Max(_controlsH, 1);
        ControlsPanel.Visibility = Vis(_controlsH > 0);
    }

    private void UpdateRadioUi()
    {
        ChipWifi.IsEnabled = _radios.WifiOn != null;
        ChipWifi.IsChecked = _radios.WifiOn == true;
        ChipBt.IsEnabled = _radios.BluetoothOn != null;
        ChipBt.IsChecked = _radios.BluetoothOn == true;
    }

    private async void ChipWifi_Click(object sender, RoutedEventArgs e) => await _radios.ToggleWifiAsync();
    private async void ChipBt_Click(object sender, RoutedEventArgs e) => await _radios.ToggleBluetoothAsync();

    private void ChipMute_Click(object sender, RoutedEventArgs e)
    {
        _audio.ToggleMute();
        UpdateVolumeUi();
    }

    private void ChipSnip_Click(object sender, RoutedEventArgs e)
    {
        ChipSnip.IsChecked = false;
        SetExpanded(false);
        try
        {
            // Windows' own screen-snipping overlay.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
        }
        catch { }
    }

    // ------------------------------------------------------------------ settings

    public void OpenSettings()
    {
        if (_settingsWin == null)
        {
            _settingsWin = new SettingsWindow(_weather);
            _settingsWin.Closed += (_, _) =>
            {
                _settingsWin = null;
                ReleaseMemory();
            };
        }
        if (_expanded) SetExpanded(false);
        _settingsWin.Show();
        if (_settingsWin.WindowState == WindowState.Minimized) _settingsWin.WindowState = WindowState.Normal;
        // Windows won't let a background app take focus; a brief topmost flip brings it forward anyway.
        _settingsWin.Topmost = true;
        _settingsWin.Activate();
        _settingsWin.Topmost = false;
    }

    /// <summary>Re-applies theme, size and visibility choices. Runs at startup and after every change.</summary>
    private void ApplySettings()
    {
        Theme.Apply(S.Theme);
        Island.LayoutTransform = new ScaleTransform(S.Scale, S.Scale);
        Fx.Effect = Enum.TryParse<IslandEffect>(S.Effect, out var fx) ? fx : IslandEffect.None;
        Fx.Livery = S.Car.Length > 0 ? Theme.Resolve(S.Car).Livery : -1;
        _fxUntil = DateTime.UtcNow.AddSeconds(3);
        if (_picLoaded != S.BgImage)
        {
            _picLoaded = S.BgImage;
            System.Windows.Media.Imaging.BitmapImage? pic = null;
            try
            {
                if (S.BgImage.Length > 0 && System.IO.File.Exists(S.BgImage) && new System.IO.FileInfo(S.BgImage).Length < 30_000_000)
                {
                    pic = new System.Windows.Media.Imaging.BitmapImage();
                    pic.BeginInit();
                    pic.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    pic.DecodePixelWidth = 640;      // plenty for a 400 px island, and keeps memory small
                    pic.UriSource = new Uri(S.BgImage);
                    pic.EndInit();
                    pic.Freeze();
                }
            }
            catch { pic = null; }
            BgPic.Source = pic;
            RenderOptions.SetBitmapScalingMode(BgPic, BitmapScalingMode.LowQuality);   // far cheaper to redraw
        }
        BgPic.Opacity = BgPic.Source != null ? 1 : 0;
        BgScrim.Opacity = BgPic.Source != null ? S.BgImageDim : 0;
        Shell.Opacity = S.IslandOpacity;
        TabNotes.Visibility = S.ShowNotesTab ? Visibility.Visible : Visibility.Collapsed;
        TabFocus.Visibility = S.ShowFocusTab ? Visibility.Visible : Visibility.Collapsed;
        if ((_page == 4 && !S.ShowNotesTab) || (_page == 3 && !S.ShowFocusTab)) SelectPage(0);
        BgName.Text = (S.BgText.Length > 0 ? S.BgText : Theme.Current.Group == "Teams" ? Theme.Current.Name : "").ToUpperInvariant();
        BgName.FontSize = BgName.Text.Length > 12 ? 34 : 50;
        Fx.Label = BgName.Text.Length > 0 ? BgName.Text : Theme.Current.Name.ToUpperInvariant();
        _music.Update(_expanded, _hidden);
        Reposition();
        UpdateNote();
        if (S.AutoHideSeconds < 0) _dormant = false;
        _lastActive = DateTime.UtcNow;
        UpdateClockAndBattery();
        UpdateControlsLayout();
        UpdateMediaUi();
        UpdateWeatherUi();
        RenderCalendar();
        _ = _weather.RefreshAsync();
        _targetW = -1; // force a re-measure
        ApplyState();
    }

    // ------------------------------------------------------------------ media

    private void OnMediaChanged()
    {
        UpdateMediaUi();

        string? key = _media.HasMedia ? _media.Title + "|" + _media.Artist : null;
        if (key != _trackKey)
        {
            _trackKey = key;
            // Announce new tracks, but not whatever was already playing at launch.
            if (key != null && _trackSeen && _media.IsPlaying) ShowToast(Toast.Track, 3200);
        }
        _trackSeen = true;

        ApplyState();
    }

    private void UpdateMediaUi()
    {
        bool has = _media.HasMedia;

        CArt.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        CClock.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        CBars.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        CBattery.Visibility = !has && _hasBattery ? Visibility.Visible : Visibility.Collapsed;
        CWeather.Visibility = !has && WeatherShown ? Visibility.Visible : Visibility.Collapsed;

        CArtBrush.ImageSource = TArtBrush.ImageSource = EArtBrush.ImageSource = _media.Art;

        ETitle.Text = _media.Title;
        EArtist.Text = string.IsNullOrWhiteSpace(_media.Artist) ? "Unknown artist" : _media.Artist;
        TTitle.Text = string.IsNullOrWhiteSpace(_media.Artist) ? _media.Title : $"{_media.Title} — {_media.Artist}";

        BtnPlay.Content = _media.IsPlaying ? "" : "";
        BtnPrev.IsEnabled = _media.CanPrevious;
        BtnNext.IsEnabled = _media.CanNext;
        ProgRow.Visibility = _media.Duration > TimeSpan.Zero ? Visibility.Visible : Visibility.Hidden;
        ProgHit.Cursor = _media.CanSeek ? Cursors.Hand : Cursors.Arrow;

        _accentBrush.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(CurrentAccent(), TimeSpan.FromMilliseconds(500)));
        Fx.Accent = CurrentAccent();
    }

    private void EnsureFrameTimer()
    {
        if (!_frameTimer.IsEnabled && !_hidden && !_locked && !_dormant && _media.HasMedia) _frameTimer.Start();
    }

    /// <summary>~30fps while music is on screen: visualizer bars + progress bar. Stops itself when idle.</summary>
    private void Frame()
    {
        if (_hidden || !_media.HasMedia || (_dormant && !_expanded && _toast == Toast.None))
        {
            _frameTimer.Stop();
            return;
        }
        if (!_media.IsPlaying && !_expanded && _smoothedPeak < 0.005 && _levels.Max() < 0.01)
        {
            // Paused and the bars have settled: go fully idle.
            CBars.SetLevels(_levels);
            _frameTimer.Stop();
            return;
        }

        // Full frame rate only while the island is open; the small pill is fine at 20 fps.
        var pace = TimeSpan.FromMilliseconds(_expanded ? 33 : 50);
        if (_frameTimer.Interval != pace) _frameTimer.Interval = pace;

        bool playing = _media.IsPlaying;
        double peak = playing ? Math.Pow(Math.Clamp(_audio.Peak * 1.6, 0, 1), 0.6) : 0;
        _smoothedPeak += (peak - _smoothedPeak) * (peak > _smoothedPeak ? 0.55 : 0.18);

        double t = (DateTime.UtcNow - _start).TotalSeconds;
        for (int i = 0; i < _levels.Length; i++)
        {
            double wobble = 0.5 + 0.5 * Math.Sin(t * _speed[i] + _phase[i]);
            double target = _smoothedPeak * (0.3 + 0.7 * wobble);
            _levels[i] += (target - _levels[i]) * 0.4;
        }

        if (_expanded) EBars.SetLevels(_levels);
        else if (_toast == Toast.Track) TBars.SetLevels(_levels);
        else CBars.SetLevels(_levels);

        if (_expanded && _page == 0 && _media.Duration > TimeSpan.Zero)
        {
            var pos = _media.Position;
            double frac = Math.Clamp(pos.TotalSeconds / _media.Duration.TotalSeconds, 0, 1);
            ProgFill.Width = ProgTrack.ActualWidth * frac;
            EPos.Text = Fmt(pos);
            ERem.Text = "-" + Fmt(_media.Duration - pos);
        }
    }

    private static string Fmt(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    // ------------------------------------------------------------------ volume

    private void UpdateVolumeUi()
    {
        bool muted = _audio.Muted || _audio.Volume < 0.005f;
        _volFrac = muted ? 0 : _audio.Volume;
        int pct = (int)Math.Round(_audio.Volume * 100);

        string glyph = muted ? "" : pct < 34 ? "" : pct < 67 ? "" : "";
        TVolIcon.Text = IVolIcon.Text = VolSliderIcon.Text = glyph;
        TVolText.Text = muted ? "Mute" : pct + "%";
        IVolText.Text = !_audio.Available ? "No audio" : muted ? "Muted" : pct + "%";

        VolFill.Width = VolTrack.ActualWidth * _volFrac;
        ChipMute.IsChecked = _audio.Muted;
        if (!VolSlider.IsDragging) VolSlider.Value = _audio.Volume;
    }

    // ------------------------------------------------------------------ once-a-second tick

    private void Tick()
    {
        UpdateClockAndBattery();
        if (!_hidden && ((_expanded && _page == 2) || _overlay))
        {
            SystemStats.Sample();
            UpdateStatsUi();
        }
        FocusTick();
        CheckReminders();
        CheckAutoHide();
        if (DateTime.Now.Second == 0) _ = _weather.RefreshAsync(); // self-throttled to once per 30 min
    }

    private void UpdateClockAndBattery()
    {
        var now = DateTime.Now;
        string time = now.ToString(S.Use24Hour ? "HH:mm" : "h:mm");
        CClock.Text = _tmRunning ? Fmt(_tmLeft) : time;   // a running timer takes over the pill's clock
        EClock.Text = S.Use24Hour ? time : now.ToString("h:mm tt");
        IClock.Text = time;
        IDate.Text = now.ToString("dddd, d MMMM");
        IGreeting.Text = now.Hour < 5 ? "GOOD NIGHT" : now.Hour < 12 ? "GOOD MORNING" : now.Hour < 17 ? "GOOD AFTERNOON" : "GOOD EVENING";

        var (hasBattery, plugged, level) = Power.Read();
        string pct = (int)Math.Round(level * 100) + "%";
        _batteryLevel = level;

        Brush fill = plugged ? (Brush)FindResource("Green")
            : level <= 0.2 ? (Brush)FindResource("Red")
            : (Brush)FindResource("TextPrimary");

        foreach (var g in new[] { CBatteryGlyph, TPowerGlyph, EBatteryGlyph, IBatteryGlyph })
        {
            g.Level = level;
            g.Fill = fill;
        }
        CBatteryText.Text = EBatteryText.Text = IBatteryText.Text = TPowerPct.Text = pct;
        CBatteryText.Foreground = plugged || level <= 0.2 ? fill : (Brush)FindResource("TextPrimary");

        _batteryHardware = hasBattery;
        bool shown = hasBattery && S.ShowBattery;
        var vis = shown ? Visibility.Visible : Visibility.Collapsed;
        EBatteryPanel.Visibility = IBatteryPanel.Visibility = vis;

        bool layoutChanged = shown != _hasBattery;
        _hasBattery = shown;
        if (layoutChanged)
        {
            UpdateMediaUi();
            ApplyState();
        }

        if (hasBattery && _plugged is { } was && was != plugged)
        {
            TPowerText.Text = plugged ? "Charging" : "On battery";
            TPowerIcon.Text = plugged ? "" : "";
            var tone = plugged ? (Brush)FindResource("Green") : (Brush)FindResource("TextPrimary");
            TPowerIcon.Foreground = TPowerPct.Foreground = tone;
            ShowToast(Toast.Power, 2800);
        }
        _plugged = plugged;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.StatusChange) Dispatcher.BeginInvoke(UpdateClockAndBattery);
    }

    // ------------------------------------------------------------------ system stats

    private void UpdateStatsUi()
    {
        static string Pct(double v) => (int)Math.Round(v * 100) + "%";

        SCpu.Text = Pct(SystemStats.Cpu);
        SMem.Text = Pct(SystemStats.Memory);
        if (!_expanded || _page != 2) return;

        RingCpu.Value = SystemStats.Cpu; TxtCpu.Text = Pct(SystemStats.Cpu);
        RingMem.Value = SystemStats.Memory; TxtMem.Text = Pct(SystemStats.Memory);
        RingDisk.Value = SystemStats.Disk; TxtDisk.Text = Pct(SystemStats.Disk);

        BatCell.Visibility = _batteryHardware ? Visibility.Visible : Visibility.Collapsed;
        RingRow.Columns = _batteryHardware ? 4 : 3;
        RingBat.Value = _batteryLevel; TxtBat.Text = Pct(_batteryLevel);

        long own;
        using (var me = System.Diagnostics.Process.GetCurrentProcess()) own = me.PrivateMemorySize64 / (1024 * 1024);
        SysLine.Text = $"{SystemStats.MemoryUsedGb:0.0} of {SystemStats.MemoryTotalGb:0} GB in use  ·  up {SystemStats.Uptime()}  ·  WindX {own} MB";
    }

    // ------------------------------------------------------------------ weather

    private bool WeatherShown => WeatherService.Enabled && _weather.HasData;

    private void UpdateWeatherUi()
    {
        bool on = WeatherShown;
        string temp = on ? Math.Round(_weather.Temperature) + "°" : "";

        CWeather.Visibility = on && !_media.HasMedia ? Visibility.Visible : Visibility.Collapsed;
        IWeather.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        WPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        WEmpty.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        WEmpty.Text = WeatherService.Enabled
            ? "Weather is unavailable right now. WindX will try again shortly."
            : "Add your city in Settings to see the weather here.";
        if (!on) return;

        foreach (var icon in new[] { CWeatherIcon, IWeatherIcon, WIcon })
        {
            icon.Sky = _weather.Sky;
            icon.IsDay = _weather.IsDay;
        }
        string desc = WeatherService.Describe(_weather.Sky);
        CWeatherText.Text = temp;
        IWeatherText.Text = $"{temp}  {desc}";
        WTemp.Text = temp;
        WDesc.Text = desc;
        WCity.Text = S.WeatherCity;

        WForecast.Children.Clear();
        foreach (var day in _weather.Days.Skip(1).Take(3))
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var name = new TextBlock { Text = day.Date.ToString("ddd"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center };
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            var icon = new WeatherIcon { Sky = day.Sky, Width = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center };
            icon.SetResourceReference(WeatherIcon.FillProperty, "TextPrimary");
            var range = new TextBlock
            {
                Text = $"{Math.Round(day.High)}° {Math.Round(day.Low)}°",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            range.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");

            Grid.SetColumn(icon, 1);
            Grid.SetColumn(range, 2);
            row.Children.Add(name);
            row.Children.Add(icon);
            row.Children.Add(range);
            WForecast.Children.Add(row);
        }
    }

    // ------------------------------------------------------------------ calendar

    private void BuildCalendar()
    {
        var first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        for (int i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)first + i) % 7);
            var head = new TextBlock
            {
                Text = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day)[..1].ToUpper(CultureInfo.CurrentCulture),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            head.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            CalGrid.Children.Add(head);
        }
        for (int i = 0; i < 42; i++)
        {
            var dot = new Border { Width = 25, Height = 25, CornerRadius = new CornerRadius(12.5) };
            var text = new TextBlock
            {
                FontSize = 12.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var cell = new Grid();
            cell.Children.Add(dot);
            cell.Children.Add(text);
            _calDots[i] = dot;
            _calText[i] = text;
            CalGrid.Children.Add(cell);
        }
    }

    private void RenderCalendar()
    {
        CalTitle.Text = _calMonth.ToString("MMMM yyyy");
        var first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int lead = ((int)_calMonth.DayOfWeek - (int)first + 7) % 7;
        var start = _calMonth.AddDays(-lead);
        var today = DateTime.Today;

        for (int i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            bool inMonth = d.Month == _calMonth.Month;
            bool isToday = d == today;
            _calText[i].Text = d.Day.ToString(CultureInfo.CurrentCulture);
            _calText[i].FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal;
            _calText[i].Opacity = inMonth ? 1 : 0.28;
            _calText[i].SetResourceReference(TextBlock.ForegroundProperty, isToday ? "OnAccent" : "TextPrimary");
            if (isToday) _calDots[i].SetResourceReference(Border.BackgroundProperty, "Accent");
            else _calDots[i].Background = Brushes.Transparent;
        }
    }

    private void CalPrev_Click(object sender, RoutedEventArgs e) { _calMonth = _calMonth.AddMonths(-1); RenderCalendar(); }
    private void CalNext_Click(object sender, RoutedEventArgs e) { _calMonth = _calMonth.AddMonths(1); RenderCalendar(); }

    private void CalTitle_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _calMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        RenderCalendar();
    }

    // ------------------------------------------------------------------ self-test snapshots

    /// <summary>Walks through every state and saves a picture of each. Lets the UI be checked without a screen.</summary>
    private async Task RunSnapshotsAsync(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        void Shot(string name)
        {
            const double k = 2;
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(Width * k), (int)(Height * k), 96 * k, 96 * k, PixelFormats.Pbgra32);
            rtb.Render(Root);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var bg = new LinearGradientBrush(Color.FromRgb(0x2B, 0x32, 0x4A), Color.FromRgb(0x4A, 0x3B, 0x5E), 30);
                if (Environment.GetEnvironmentVariable("WINDX_SNAP_CLEAR") != "1") dc.DrawRectangle(bg, null, new Rect(0, 0, Width, Height));
                dc.DrawImage(rtb, new Rect(0, 0, Width, Height));
            }
            var flat = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(Width * k), (int)(Height * k), 96 * k, 96 * k, PixelFormats.Pbgra32);
            flat.Render(visual);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(flat));
            using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, name + ".png"));
            enc.Save(fs);
        }

        // Test-only overrides (never saved): WINDX_SNAP_THEME / WINDX_SNAP_EFFECT, and every control switched on.
        if (Environment.GetEnvironmentVariable("WINDX_SNAP_THEME") is { Length: > 0 } th) S.Theme = Theme.Resolve(th).Name;
        if (Environment.GetEnvironmentVariable("WINDX_SNAP_EFFECT") is { Length: > 0 } ef) S.Effect = ef;
        if (Environment.GetEnvironmentVariable("WINDX_SNAP_CAR") is { Length: > 0 } car) S.Car = car;
        S.Sounds = false; S.MusicMode = "Off"; S.ShowNote = false; S.BgImage = ""; S.BgText = "";
        if (Environment.GetEnvironmentVariable("WINDX_SNAP_PIN") is { Length: > 0 } pin) { S.PinNote = true; S.Stickies = pin.Split('|').Select((x, i) => new StickyItem { Text = x, Color = new[] { "Yellow", "Pink", "Blue", "Green" }[i % 4] }).ToList(); }
        S.ShowBrightness = S.ShowVolume = S.ShowWifi = S.ShowBluetooth = S.ShowMute = S.ShowSnip = false;
        if (Environment.GetEnvironmentVariable("WINDX_SNAP_NOTE") is { Length: > 0 } ns) { S.ShowNote = true; S.NoteStyle = ns; }
        ApplySettings();

        await Task.Delay(1900); Shot("0-intro");
        await Task.Delay(2400); Shot("1-compact");
        SetExpanded(true); await Task.Delay(1500); Shot("2-home");
        SelectPage(1); await Task.Delay(1200); Shot("3-calendar");
        SelectPage(2); await Task.Delay(1300); SystemStats.Sample(); UpdateStatsUi(); await Task.Delay(300); Shot("4-system");
        SelectPage(3); await Task.Delay(1100); Shot("4b-focus"); SelectPage(4); await Task.Delay(1100); Shot("4c-notes");
        SetExpanded(false); _overlay = true; SystemStats.Sample(); UpdateStatsUi(); ApplyState(); await Task.Delay(1200); Shot("5-game-overlay");
        _overlay = false; _dormant = true; ApplyState(); await Task.Delay(1200); Shot("6-auto-hidden");
        _dormant = false; UpdateVolumeUi(); _toast = Toast.Volume; TVolume.Visibility = Visibility.Visible; ApplyState(); await Task.Delay(1200); Shot("7-volume");

        // The Settings window, rendered the same way.
        var win = new SettingsWindow(_weather);
        win.Show();
        await Task.Delay(2900);
        if (win.Content is FrameworkElement body)
        {
            var shot = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(body.ActualWidth * 1.5), (int)(body.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
            var back = new DrawingVisual();
            using (var dc = back.RenderOpen())
            {
                dc.DrawRectangle(win.Background, null, new Rect(0, 0, body.ActualWidth, body.ActualHeight));
                dc.DrawRectangle(new VisualBrush(body), null, new Rect(0, 0, body.ActualWidth, body.ActualHeight));
            }
            shot.Render(back);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
            using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, "8-settings.png"));
            enc.Save(fs);
        }
        win.Close();
        Close();
    }

    // ------------------------------------------------------------------ housekeeping

    private void Watch()
    {
        if (App.SnapshotDir != null) return; // the self-test drives every state itself

        bool fullscreen = IsForegroundFullscreen();
        bool hide = fullscreen && S.FullscreenMode == "Hide";
        bool overlay = fullscreen && S.FullscreenMode == "Stats";

        if (hide != _hidden)
        {
            // Get out of the way of fullscreen video / games.
            _hidden = hide;
            _music.Update(_expanded, hide);
            Root.IsHitTestVisible = !hide;
            Animate(Root, OpacityProperty, hide ? 0 : 1, 250, _smooth);
            if (hide && _expanded) SetExpanded(false);
        }
        if (overlay != _overlay)
        {
            // In-game overlay: the pill turns into a live CPU / RAM readout.
            _overlay = overlay;
            if (overlay)
            {
                _dormant = false;
                SystemStats.Sample();
                UpdateStatsUi();
            }
            _lastActive = DateTime.UtcNow;
            ApplyState();
        }

        if (!_hidden)
        {
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            EnsureFrameTimer();
        }

        // Hand unused pages back to Windows shortly after launch and then periodically while idle.
        if (_watchTicks == 8 || (_watchTicks % 30 == 0 && !_expanded && _toast == Toast.None))
            TrimMemory();

        if (++_watchTicks % 3 == 0)
        {
            _audio.Acquire();
            _media.Rescan();
        }
    }

    /// <summary>After the island has been used, squeeze the managed heap and hand the memory back to Windows.</summary>
    private static void ReleaseMemory()
    {
        try
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        }
        catch { }
        TrimMemory();
    }

    private static void TrimMemory()
    {
        try
        {
            using var me = System.Diagnostics.Process.GetCurrentProcess();
            SetProcessWorkingSetSize(me.Handle, -1, -1);
        }
        catch { }
    }

    private bool IsForegroundFullscreen()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == _hwnd) return false;

        var sb = new StringBuilder(64);
        GetClassName(fg, sb, sb.Capacity);
        string cls = sb.ToString();
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;

        if (!GetWindowRect(fg, out RECT r)) return false;
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST), ref mi)) return false;
        if ((mi.dwFlags & MONITORINFOF_PRIMARY) == 0) return false;

        // Exact match = true fullscreen. Maximized windows overhang by a few px and are ignored.
        return r.Left == mi.rcMonitor.Left && r.Top == mi.rcMonitor.Top
            && r.Right == mi.rcMonitor.Right && r.Bottom == mi.rcMonitor.Bottom;
    }

    // ------------------------------------------------------------------ win32

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x80;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private const long WS_EX_TRANSPARENT = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint MONITORINFOF_PRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint min, nint max);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int max);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
}
