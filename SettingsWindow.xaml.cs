using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindX.Controls;
using WindX.Services;

namespace WindX;

/// <summary>The control app: appearance, controls, island behaviour, weather, privacy. Every change applies live.</summary>
public partial class SettingsWindow : Window
{
    private static Settings S => Settings.Current;

    private static readonly Brush CardBg = Hex("#161618");
    private static readonly Brush Line = Hex("#222225");
    private static readonly Brush TextMain = Hex("#F4F1EA");
    private static readonly Brush TextDim = Hex("#8C887F");
    private static readonly Brush Ember = Hex("#FF5B1F");
    private static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    private static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly IEasingFunction Smooth = new CubicEase { EasingMode = EasingMode.EaseOut };

    private readonly WeatherService _weather;
    private readonly List<(Border Card, Theme Theme)> _themeCards = new();
    private readonly DispatcherTimer _previewTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly DateTime _start = DateTime.UtcNow;
    private EffectLayer? _previewFx;
    private int _groupId;
    private int _section;

    private static readonly (string Glyph, string Label, string About)[] Sections =
    {
        ("\uE80F", "Home", "Your island at a glance."),
        ("\uE790", "Appearance", "Themes, living effects and the look of the island."),
        ("\uE9E9", "Quick controls", "Choose what appears when you open the island."),
        ("\uE7F4", "Island", "Where it sits, how see-through it is, and when it hides."),
        ("\uE753", "Weather", "Off until you add a city."),
        ("\uE945", "Behaviour", "How the island reacts."),
        ("\uE767", "Sound", "Touch sounds and your own theme music."),
        ("\uEA8F", "Reminders", "Pop-ups at the time you choose."),
        ("\uE70B", "Hanging note", "A quote or a reminder on a tag that swings."),
        ("\uE72E", "Privacy & about", "What WindX does, and doesn't do, with your data."),
    };

    // The anime-style power pack didn't meet the bar; it stays out of the picker until it's redone.
    private static readonly string[] HiddenEffects = { "Infinity", "WaterBlade", "FlameBlade", "ThunderStep", "Spiral", "SpiritOrb", "Domain", "BlackFlash", "Beam", "CrimsonMoon" };

    public const string Author = "Bharath";
    public const string Institute = "IIT Madras";

    public SettingsWindow(WeatherService weather)
    {
        _weather = weather;
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            // Dark title bar to match the window.
            int on = 1;
            var hwnd = new WindowInteropHelper(this).Handle;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));
            int round = 2;                                   // Windows 11 rounded corners
            DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
            int caption = 0x000F0E0E;                       // title bar the same ink as the app (COLORREF is BGR)
            DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
        };
        try { Icon = Logo.Render(64); } catch { }

        // Sidebar
        BrandRow.Children.Add(new LogoMark { Width = 34, Height = 34, Tile = true });
        var word = new TextBlock { FontSize = 21, FontWeight = FontWeights.Bold, FontFamily = Display, Foreground = TextMain, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 0, 0, 1) };
        word.Inlines.Add("Wind");
        word.Inlines.Add(new System.Windows.Documents.Run("X") { Foreground = Ember });
        BrandRow.Children.Add(word);
        VersionText.Text = $"Version {Links.Version}\nCreated by {Author} · {Institute}";
        QuitButton.Click += (_, _) => Application.Current.MainWindow?.Close();

        for (int i = 0; i < Sections.Length; i++)
        {
            int index = i;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = Sections[i].Glyph, FontFamily = Icons, FontSize = 15, Width = 22, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new TextBlock { Text = Sections[i].Label, Margin = new Thickness(8, 0, 0, 1), VerticalAlignment = VerticalAlignment.Center });
            var item = new RadioButton { Style = (Style)FindResource("NavItem"), Content = content, IsChecked = i == 0 };
            item.Click += (_, _) => Show(index);
            Nav.Children.Add(item);
        }

        _previewTimer.Tick += (_, _) =>
        {
            if (_previewFx is { IsVisible: true } fx) fx.Step((DateTime.UtcNow - _start).TotalSeconds, 0.45);
        };
        Host.RequestBringIntoView += (_, e) => e.Handled = true;
        Closed += (_, _) => _previewTimer.Stop();

        Show(0, animate: false);
        PlaySplash();
        Closing += FadeOutOnClose;
    }

    private static bool _splashSeen;

    /// <summary>
    /// Launch screen: wind streaks blow across, the logo draws itself on, the name rises in, then the
    /// whole thing lifts away to reveal the app. Full version the first time per session, a quick one after.
    /// </summary>
    private void PlaySplash()
    {
        bool full = !_splashSeen;
        _splashSeen = true;

        var shell = (UIElement)Content;
        Content = null;
        var root = new Grid();
        root.Children.Add(shell);

        var logoScale = new ScaleTransform(0.8, 0.8);
        var logo = new LogoMark { Width = 96, Height = 96, Tile = true, Progress = full ? 0 : 1, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = logoScale };
        var wordShift = new TranslateTransform(0, 16);
        var word = new TextBlock { FontSize = 38, FontWeight = FontWeights.Bold, FontFamily = Display, Foreground = TextMain, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0), Opacity = 0, RenderTransform = wordShift };
        word.Inlines.Add("Wind");
        word.Inlines.Add(new System.Windows.Documents.Run("X") { Foreground = Ember });
        var tag = new TextBlock { Text = $"A Dynamic Island for Windows  ·  by {Author}, {Institute}", Foreground = TextDim, FontSize = 14.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0), Opacity = 0 };
        var stackScale = new ScaleTransform(1, 1);
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = stackScale };
        stack.Children.Add(logo);
        stack.Children.Add(word);
        stack.Children.Add(tag);
        var streaks = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        var glow = new RadialGradientBrush(Color.FromArgb(46, 0xFF, 0x5B, 0x1F), Color.FromArgb(0, 0xFF, 0x5B, 0x1F));
        var splash = new Grid { Background = Hex("#0E0E0F") };
        splash.Children.Add(new Border { Background = glow, Opacity = 0 });
        splash.Children.Add(streaks);
        splash.Children.Add(stack);
        root.Children.Add(splash);
        Content = root;

        Loaded += (_, _) =>
        {
            int t0 = full ? 0 : -600;   // the quick version skips the drawing-on
            TimeSpan At(int ms) => TimeSpan.FromMilliseconds(Math.Max(0, t0 + ms));
            ((Border)splash.Children[0]).BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(900)) { BeginTime = At(150), EasingFunction = Smooth });

            if (full)
            {
                var rnd = new Random(3);
                double width = ActualWidth > 0 ? ActualWidth : Width, height = ActualHeight > 0 ? ActualHeight : Height;
                for (int i = 0; i < 12; i++)
                {
                    var c = i % 3 == 1 ? Color.FromRgb(0xFF, 0x5B, 0x1F) : Color.FromRgb(0xF4, 0xF1, 0xEA);
                    var fill = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
                    fill.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0));
                    fill.GradientStops.Add(new GradientStop(Color.FromArgb(220, c.R, c.G, c.B), 0.8));
                    fill.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
                    double len = 140 + rnd.NextDouble() * 320, thick = 1.2 + rnd.NextDouble() * 2.2;
                    var move = new TranslateTransform(-len, 0);
                    var streak = new Rectangle { Width = len, Height = thick, RadiusX = thick / 2, RadiusY = thick / 2, Fill = fill, Opacity = 0, RenderTransform = move };
                    Canvas.SetTop(streak, height * (0.28 + rnd.NextDouble() * 0.44));
                    streaks.Children.Add(streak);
                    var begin = TimeSpan.FromMilliseconds(60 + i * 65 + rnd.Next(80));
                    var dur = TimeSpan.FromMilliseconds(700 + rnd.Next(500));
                    move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-len, width + 40, dur) { BeginTime = begin, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
                    var fade = new DoubleAnimationUsingKeyFrames { BeginTime = begin, Duration = dur };
                    fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
                    fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromPercent(0.35)));
                    fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
                    streak.BeginAnimation(OpacityProperty, fade);
                }
                logo.BeginAnimation(LogoMark.ProgressProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(950)) { BeginTime = At(300), EasingFunction = Smooth });
            }

            var pop = new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut };
            logoScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.8, 1, TimeSpan.FromMilliseconds(700)) { BeginTime = At(250), EasingFunction = pop });
            logoScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.8, 1, TimeSpan.FromMilliseconds(700)) { BeginTime = At(250), EasingFunction = pop });
            word.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450)) { BeginTime = At(800) });
            wordShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(600)) { BeginTime = At(800), EasingFunction = Smooth });
            tag.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450)) { BeginTime = At(1000) });

            // Lift the splash away and bring the app in underneath it.
            var lift = At(1750);
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = lift, EasingFunction = Smooth };
            fadeOut.Completed += (_, _) => root.Children.Remove(splash);
            splash.BeginAnimation(OpacityProperty, fadeOut);
            stackScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(420)) { BeginTime = lift, EasingFunction = Smooth });
            stackScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(420)) { BeginTime = lift, EasingFunction = Smooth });

            Shell.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { BeginTime = lift, EasingFunction = Smooth });
            HostShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(26, 0, TimeSpan.FromMilliseconds(620)) { BeginTime = lift, EasingFunction = Smooth });
            int n = 0;
            foreach (UIElement item in Nav.Children)
            {
                var shift = new TranslateTransform(-18, 0);
                item.RenderTransform = shift;
                item.Opacity = 0;
                var b = lift + TimeSpan.FromMilliseconds(120 + n * 45);
                shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-18, 0, TimeSpan.FromMilliseconds(480)) { BeginTime = b, EasingFunction = Smooth });
                item.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = b });
                n++;
            }
        };
    }

    private static Brush Hex(string hex)
    {
        var b = new SolidColorBrush(Theme.C(hex));
        b.Freeze();
        return b;
    }

    // ------------------------------------------------------------------ navigation

    private void Show(int section, bool animate = true)
    {
        _section = section;
        _previewTimer.Stop();
        _previewFx = null;
        _themeCards.Clear();
        Host.Children.Clear();

        if (section != 0) Host.Children.Add(PageHeader(section));
        switch (section)
        {
            case 0: BuildHome(); break;
            case 1: BuildAppearance(); break;
            case 2: BuildControls(); break;
            case 3: BuildIsland(); break;
            case 4: BuildWeather(); break;
            case 5: BuildBehaviour(); break;
            case 6: BuildSound(); break;
            case 7: BuildReminders(); break;
            case 8: BuildNote(); break;
            default: BuildAbout(); break;
        }
        Scroller.ScrollToTop();

        if (animate)
        {
            // Each block of the page rises into place one after another.
            int i = 0;
            foreach (UIElement child in Host.Children)
            {
                var shift = new TranslateTransform();
                child.RenderTransform = shift;
                var delay = TimeSpan.FromMilliseconds(Math.Min(i, 9) * 45);
                shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(22, 0, TimeSpan.FromMilliseconds(460)) { BeginTime = delay, EasingFunction = Smooth });
                child.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { BeginTime = delay });
                i++;
            }
        }
    }

    private void Note(string text) =>
        Host.Children.Add(new TextBlock { Text = text, Foreground = TextDim, Margin = new Thickness(1, 4, 0, 18), TextWrapping = TextWrapping.Wrap });

    // ------------------------------------------------------------------ sections

    private void BuildAppearance()
    {
        Host.Children.Add(Preview());

        Host.Children.Add(Heading("Theme"));
        foreach (var (group, label) in new[] { ("Classic", (string?)null), ("Gradient", "Gradients"), ("Teams", "Team islands  ·  each comes with its own car or drop") })
        {
            if (label != null) Host.Children.Add(new TextBlock { Text = label, Foreground = TextDim, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 14, 0, 6) });
            var themes = new UniformGrid { Columns = 5, Margin = new Thickness(-5, 0, -5, 0) };
            foreach (var theme in Theme.All.Where(t => t.Group == group)) themes.Children.Add(ThemeCard(theme));
            Host.Children.Add(themes);
        }
        RefreshThemeSelection();

        Host.Children.Add(Heading("Living backdrop", "An effect that moves inside the island. They all react to your music: flames leap, waves swell, lightning strikes faster."));
        Host.Children.Add(Card(
            ChoiceRow("Effect", null,
                Enum.GetNames<IslandEffect>().Except(HiddenEffects).Select(n => (n, n)).ToArray(),
                v => S.Effect == v, v => S.Effect = v, stacked: true),
            ChoiceRow("Race car", "The single car shown by the Race effect.",
                new[] { ("Theme colour", "") }.Concat(Theme.All.Where(t => t.Livery >= 0).Select(t => (t.Name, t.Name))).ToArray(),
                v => S.Car == v, v => S.Car = v, stacked: true),
            SwitchRow("Keep moving in the pill", "Off (recommended): the effect moves only while the island is open or under your mouse, so it costs nothing at rest. On uses a little CPU all the time.",
                () => S.EffectWhenIdle, v => S.EffectWhenIdle = v)));

        var nameBox = new TextBox { Style = (Style)FindResource("Field"), Text = S.BgText, MaxLength = 24 };
        var nameSave = PlainButton("Save", () => { S.BgText = nameBox.Text.Trim(); Settings.Save(); });
        nameSave.Margin = new Thickness(8, 0, 0, 0);
        var nameLine = new DockPanel();
        DockPanel.SetDock(nameSave, Dock.Right);
        nameLine.Children.Add(nameSave);
        nameLine.Children.Add(nameBox);
        var nameRow = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        nameRow.Children.Add(new TextBlock { Text = "Name in the background", Foreground = TextMain, FontSize = 14.5 });
        nameRow.Children.Add(new TextBlock { Text = "Big faint lettering behind the open island: your team, your name, anything. Leave empty and a team island shows its own name.", Foreground = TextDim, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 9), TextWrapping = TextWrapping.Wrap });
        nameRow.Children.Add(nameLine);
        var picName = new TextBlock
        {
            Text = S.BgImage.Length > 0 ? System.IO.Path.GetFileName(S.BgImage) : "No picture",
            Foreground = S.BgImage.Length > 0 ? TextMain : TextDim, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var picRemove = PlainButton("Remove", () => { S.BgImage = ""; Settings.Save(); Show(_section, animate: false); });
        picRemove.Margin = new Thickness(8, 0, 0, 0);
        var picChoose = PlainButton("Choose a picture…", () =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose a background picture", Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog(this) != true) return;
            S.BgImage = dlg.FileName;
            Settings.Save();
            Show(_section, animate: false);
        });
        picChoose.Margin = new Thickness(12, 0, 0, 0);
        var picLine = new DockPanel { Margin = new Thickness(18, 13, 18, 4) };
        DockPanel.SetDock(picRemove, Dock.Right);
        DockPanel.SetDock(picChoose, Dock.Right);
        picLine.Children.Add(picRemove);
        picLine.Children.Add(picChoose);
        picLine.Children.Add(picName);
        var picRow = new StackPanel();
        picRow.Children.Add(new TextBlock { Text = "Background picture", Foreground = TextMain, FontSize = 14.5, Margin = new Thickness(18, 14, 18, 0) });
        picRow.Children.Add(new TextBlock { Text = "Any picture from your PC, shown inside the island with a slow camera drift. Your chosen effect animates on top of it (try Sunbeams, Fireflies or Rain).", Foreground = TextDim, FontSize = 12.5, Margin = new Thickness(18, 2, 18, 0), TextWrapping = TextWrapping.Wrap });
        picRow.Children.Add(picLine);

        Host.Children.Add(new Border { Height = 14 });
        Host.Children.Add(Card(
            picRow,
            ChoiceRow("Picture darkness", "Darker keeps the clock and text easier to read.",
                new[] { ("None", 0.0), ("Light", 0.25), ("Medium", 0.45), ("Dark", 0.65) },
                v => Math.Abs(S.BgImageDim - v) < 0.01, v => S.BgImageDim = v, stacked: true),
            nameRow));

        Host.Children.Add(Heading("Look"));
        Host.Children.Add(Card(
            ChoiceRow("Island size", null,
                new[] { ("Small", 0.88), ("Normal", 1.0), ("Large", 1.15) },
                v => Math.Abs(S.Scale - v) < 0.01, v => S.Scale = v),
            SwitchRow("Match album art", "Tint the visualizer, progress bar and glow with the colours of what's playing",
                () => S.AlbumAccent, v => S.AlbumAccent = v),
            SwitchRow("Opening animation", "Play the WindX reveal when the island starts",
                () => S.IntroAnimation, v => S.IntroAnimation = v)));
    }

    // ------------------------------------------------------------------ home

    private void Go(int section)
    {
        if (Nav.Children[section] is RadioButton r) r.IsChecked = true;
        Show(section);
    }

    private UIElement PageHeader(int section)
    {
        var tile = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(13), Background = Hex("#1E1A18"), BorderBrush = Hex("#3A2418"), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = Sections[section].Glyph, FontFamily = Icons, FontSize = 20, Foreground = Ember, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var text = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = Sections[section].Label, FontSize = 28, FontWeight = FontWeights.SemiBold, FontFamily = Display, Foreground = TextMain });
        text.Children.Add(new TextBlock { Text = Sections[section].About, FontSize = 13.5, Foreground = TextDim, Margin = new Thickness(1, 1, 0, 0) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 22) };
        row.Children.Add(tile);
        row.Children.Add(text);
        return row;
    }

    private void BuildHome()
    {
        int hour = DateTime.Now.Hour;
        string greet = hour < 5 ? "Good night" : hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";

        // Hero: greeting, status, and the live island preview.
        var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        status.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = Hex("#30D158"), VerticalAlignment = VerticalAlignment.Center });
        status.Children.Add(new TextBlock { Text = $"WindX is running  ·  {Theme.Current.Name} theme  ·  {S.Effect} effect", Foreground = TextDim, FontSize = 13.5, Margin = new Thickness(8, 0, 0, 1) });
        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        head.Children.Add(new TextBlock { Text = greet + ".", FontSize = 34, FontWeight = FontWeights.SemiBold, FontFamily = Display, Foreground = TextMain });
        head.Children.Add(status);
        Host.Children.Add(head);
        Host.Children.Add(Preview());

        // One-tap effects.
        Host.Children.Add(Heading("Pick an effect", "Tap one. The island changes instantly."));
        var fx = new UniformGrid { Columns = 6, Margin = new Thickness(-5, 0, -5, 0) };
        foreach (var (name, glyph, tint) in new[] { ("Fire", "\uECAD", "#FF5B1F"), ("Water", "drop", "#3CC8E0"), ("Galaxy", "\uE734", "#A78BFA"), ("Ribbons", "\uE790", "#FF8FA9"), ("Race", "\uE804", "#FFD60A"), ("Thunder", "\uE945", "#C6FF3D") })
        {
            bool on = S.Effect == name;
            var body = new StackPanel { Margin = new Thickness(0, 16, 0, 14) };
            body.Children.Add(glyph == "drop"
                ? new Path { Data = Geometry.Parse("M12,2 C12,2 5,10 5,14.5 A7,7 0 0 0 19,14.5 C19,10 12,2 12,2 Z"), Fill = Hex(tint), Width = 20, Height = 24, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center }
                : new TextBlock { Text = glyph, FontFamily = Icons, FontSize = 24, Foreground = Hex(tint), HorizontalAlignment = HorizontalAlignment.Center });
            body.Children.Add(new TextBlock { Text = name, Foreground = TextMain, FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 9, 0, 0) });
            var tile = HoverCard(body, on);
            tile.Margin = new Thickness(5);
            tile.MouseLeftButtonUp += (_, _) => { S.Effect = name; Settings.Save(); Show(0, animate: false); };
            fx.Children.Add(tile);
        }
        Host.Children.Add(fx);

        // One-tap themes: a swatch for every theme.
        Host.Children.Add(Heading("Pick a theme", "Team themes bring their own animation and car."));
        var sw = new WrapPanel { Margin = new Thickness(-4, 0, 0, 0) };
        foreach (var theme in Theme.All)
        {
            bool on = theme.Name == S.Theme;
            var dot = new Grid { Width = 40, Height = 40, Margin = new Thickness(4), Cursor = Cursors.Hand, ToolTip = theme.Name };
            dot.Children.Add(new Ellipse { Fill = theme.BackgroundBrush(), Stroke = on ? Ember : Hex("#2C2C30"), StrokeThickness = on ? 2.5 : 1 });
            dot.Children.Add(new Ellipse { Width = 13, Height = 13, Fill = new SolidColorBrush(theme.AccentColor) });
            dot.MouseLeftButtonUp += (_, _) =>
            {
                S.Theme = theme.Name;
                if (theme.Fx != null) { S.Effect = theme.Fx; if (theme.Livery >= 0) S.Car = theme.Name; }
                Settings.Save();
                Show(0, animate: false);
            };
            sw.Children.Add(dot);
        }
        Host.Children.Add(sw);

        // Shortcuts into the rest of the app.
        Host.Children.Add(Heading("Shortcuts"));
        var grid = new UniformGrid { Columns = 2, Margin = new Thickness(-6, 0, -6, 0) };
        (string Glyph, string Title, string Value, int Section)[] cards =
        {
            ("\uEA8F", "Reminders", S.Reminders.Count == 0 ? "None yet. Add one" : $"{S.Reminders.Count} set", 7),
            ("\uE916", "Timer & Pomodoro", "Open the island, tap the stopwatch", 0),
            ("\uE753", "Weather", WeatherService.Enabled ? S.WeatherCity : "Off. Add your city", 4),
            ("\uE767", "Sound", (S.Sounds ? "Touch sounds on" : "Touch sounds off") + (S.MusicMode != "Off" ? " · music on" : ""), 6),
        };
        foreach (var c in cards)
        {
            var row = new DockPanel { Margin = new Thickness(18, 16, 16, 16) };
            var go = new TextBlock { Text = "\uE76C", FontFamily = Icons, FontSize = 12, Foreground = TextDim, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(go, Dock.Right);
            row.Children.Add(go);
            row.Children.Add(new TextBlock { Text = c.Glyph, FontFamily = Icons, FontSize = 20, Foreground = Ember, VerticalAlignment = VerticalAlignment.Center, Width = 34 });
            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txt.Children.Add(new TextBlock { Text = c.Title, Foreground = TextMain, FontSize = 14.5, FontWeight = FontWeights.SemiBold });
            txt.Children.Add(new TextBlock { Text = c.Value, Foreground = TextDim, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis });
            row.Children.Add(txt);
            var card = HoverCard(row, false);
            card.Margin = new Thickness(6);
            int target = c.Section;
            card.MouseLeftButtonUp += (_, _) => { if (target > 0) Go(target); };
            grid.Children.Add(card);
        }
        Host.Children.Add(grid);

        Host.Children.Add(new Border { Height = 22 });
        Host.Children.Add(Credit());
    }

    /// <summary>A card that lifts its border on hover. selected = ember outline.</summary>
    private Border HoverCard(UIElement child, bool selected)
    {
        var idle = selected ? Ember : Line;
        var card = new Border { Background = CardBg, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(selected ? 2 : 1), BorderBrush = idle, Child = child, Cursor = Cursors.Hand };
        card.MouseEnter += (_, _) => { if (!selected) card.BorderBrush = Hex("#3A3A3F"); card.Background = Hex("#1A1A1D"); };
        card.MouseLeave += (_, _) => { card.BorderBrush = idle; card.Background = CardBg; };
        return card;
    }

    /// <summary>The maker's card.</summary>
    private UIElement Credit()
    {
        var bg = new LinearGradientBrush(Theme.C("#1C1410"), Theme.C("#120F0E"), new Point(0, 0), new Point(1, 1));
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        text.Children.Add(new TextBlock { Text = "CREATED BY", Foreground = Ember, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(1, 0, 0, 2) });
        text.Children.Add(new TextBlock { Text = Author, Foreground = TextMain, FontSize = 26, FontWeight = FontWeights.SemiBold, FontFamily = Display });
        text.Children.Add(new TextBlock { Text = Institute, Foreground = TextDim, FontSize = 14 });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 20, 22, 20) };
        row.Children.Add(new LogoMark { Width = 58, Height = 58, Tile = true });
        row.Children.Add(text);
        return new Border { Background = bg, CornerRadius = new CornerRadius(20), BorderBrush = Hex("#3A2418"), BorderThickness = new Thickness(1), Child = row };
    }

    private bool _closing;

    /// <summary>Fade and sink slightly instead of vanishing.</summary>
    private void FadeOutOnClose(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closing || App.SnapshotDir != null) return;
        e.Cancel = true;
        _closing = true;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = Smooth };
        fade.Completed += (_, _) => Dispatcher.BeginInvoke(Close);
        BeginAnimation(OpacityProperty, fade);
    }

    private void BuildControls()
    {
        Host.Children.Add(Card(
            SwitchRow("Brightness slider", "Laptop screens only", () => S.ShowBrightness, v => S.ShowBrightness = v),
            SwitchRow("Volume slider", null, () => S.ShowVolume, v => S.ShowVolume = v),
            SwitchRow("Wi-Fi button", null, () => S.ShowWifi, v => S.ShowWifi = v),
            SwitchRow("Bluetooth button", null, () => S.ShowBluetooth, v => S.ShowBluetooth = v),
            SwitchRow("Mute button", null, () => S.ShowMute, v => S.ShowMute = v),
            SwitchRow("Screenshot button", "Opens the Windows snipping overlay", () => S.ShowSnip, v => S.ShowSnip = v)));
    }

    private void BuildIsland()
    {
        Host.Children.Add(Card(
            ChoiceRow("Position", "Move it left or right if it sits on top of something you need, like a browser's new-tab button.",
                new[] { ("Left", "Left"), ("Centre", "Center"), ("Right", "Right") },
                v => S.Position == v, v => S.Position = v, stacked: true),
            ChoiceRow("Transparency", "How see-through the island's background is. Text stays solid.",
                new[] { ("Solid", 1.0), ("85%", 0.85), ("70%", 0.7), ("55%", 0.55), ("40%", 0.4) },
                v => Math.Abs(S.IslandOpacity - v) < 0.01, v => S.IslandOpacity = v, stacked: true),
            ChoiceRow("Auto-hide", "Tuck the island away when you're not using it. It returns when you touch the top of the screen.",
                new[] { ("Never", -1), ("Instantly", 0), ("5 s", 5), ("10 s", 10), ("30 s", 30), ("60 s", 60) },
                v => S.AutoHideSeconds == v, v => S.AutoHideSeconds = v, stacked: true),
            ChoiceRow("In fullscreen apps", "Over games and fullscreen video: hide, show a live CPU / RAM overlay, or stay as normal.",
                new[] { ("Hide", "Hide"), ("CPU / RAM overlay", "Stats"), ("Show", "Show") },
                v => S.FullscreenMode == v, v => S.FullscreenMode = v, stacked: true),
            SwitchRow("Show battery", null, () => S.ShowBattery, v => S.ShowBattery = v),
            SwitchRow("24-hour clock", null, () => S.Use24Hour, v => S.Use24Hour = v),
            SwitchRow("Timer & Pomodoro tab", "The stopwatch page inside the open island", () => S.ShowFocusTab, v => S.ShowFocusTab = v),
            SwitchRow("Notes tab", "A page inside the island where you can type notes", () => S.ShowNotesTab, v => S.ShowNotesTab = v),
            SwitchRow("Pin my note on the island", "A sticky note on the island showing the first line of your note. Click it to open your notes.", () => S.PinNote, v => S.PinNote = v)));
    }

    private void BuildWeather()
    {
        Note("Off until you add a city. Only that city's coordinates are sent to Open-Meteo, nothing about you.");
        Host.Children.Add(Card(
            WeatherRow(),
            SwitchRow("Fahrenheit", "Show temperatures in °F instead of °C", () => S.Fahrenheit, v =>
            {
                S.Fahrenheit = v;
                _ = _weather.RefreshAsync(force: true);
            })));
    }

    private void BuildBehaviour()
    {
        Host.Children.Add(Card(
            SwitchRow("Open on hover", "Expand when the mouse rests on the island, instead of needing a click",
                () => S.ExpandOnHover, v => S.ExpandOnHover = v),
            SwitchRow("Volume pop-up", "Morph into a volume bar when the volume changes",
                () => S.VolumePopups, v => S.VolumePopups = v),
            SwitchRow("Now-playing pop-up", "Announce each new track",
                () => S.TrackPopups, v => S.TrackPopups = v),
            SwitchRow("Charging pop-up", "Show when the charger is plugged in or removed",
                () => S.PowerPopups, v => S.PowerPopups = v),
            SwitchRow("Start with Windows", "Launch the island automatically when you sign in",
                Tray.IsAutostart, Tray.SetAutostart)));

        var reset = PlainButton("Reset everything to defaults", ResetAll);
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        reset.Margin = new Thickness(0, 22, 0, 0);
        Host.Children.Add(reset);
    }

    private void BuildSound()
    {
        Note("The touch sounds are generated by WindX itself. Theme music is any audio file you choose from your own PC; WindX doesn't ship with music.");
        var name = new TextBlock
        {
            Text = S.MusicFile.Length > 0 ? System.IO.Path.GetFileName(S.MusicFile) : "No track chosen yet",
            Foreground = S.MusicFile.Length > 0 ? TextMain : TextDim, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var pick = PlainButton("Choose a track…", () =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose your theme music", Filter = "Audio files|*.mp3;*.wav;*.m4a;*.wma;*.aac;*.flac" };
            if (dlg.ShowDialog(this) != true) return;
            S.MusicFile = dlg.FileName;
            if (S.MusicMode == "Off") S.MusicMode = "Open";
            Settings.Save();
            Show(_section, animate: false);
        });
        pick.Margin = new Thickness(12, 0, 0, 0);
        var file = new DockPanel { Margin = new Thickness(18, 13, 18, 13) };
        DockPanel.SetDock(pick, Dock.Right);
        file.Children.Add(pick);
        file.Children.Add(name);

        Host.Children.Add(Card(
            SwitchRow("Touch sounds", "A soft tick when your mouse touches the island, a whoosh when it opens and closes",
                () => S.Sounds, v => S.Sounds = v),
            ChoiceRow("Theme music", "Loops the track you choose below.",
                new[] { ("Off", "Off"), ("While the island is open", "Open"), ("Always", "Always") },
                v => S.MusicMode == v, v => S.MusicMode = v, stacked: true),
            file,
            ChoiceRow("Music volume", null,
                new[] { ("25%", 0.25), ("50%", 0.5), ("75%", 0.75), ("100%", 1.0) },
                v => Math.Abs(S.MusicVolume - v) < 0.01, v => S.MusicVolume = v)));
    }

    private void BuildReminders()
    {
        Note("At the time you set, the island turns into a banner with your text and chimes. The timer and Pomodoro live in the island itself: open it and tap the stopwatch tab.");

        var time = new TextBox { Style = (Style)FindResource("Field"), Text = DateTime.Now.AddMinutes(5).ToString("HH:mm"), Width = 84, MaxLength = 8 };
        var text = new TextBox { Style = (Style)FindResource("Field"), MaxLength = 80, Margin = new Thickness(8, 0, 0, 0) };
        var daily = new CheckBox { Style = (Style)FindResource("Switch"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var status = new TextBlock { Foreground = TextDim, FontSize = 12.5, Margin = new Thickness(1, 9, 0, 0), Text = "Time like 18:30 or 6:30 PM, then what to remind you about. Switch on for every day." };
        var add = PlainButton("Add", () =>
        {
            if (!DateTime.TryParse(time.Text.Trim(), out var when)) { status.Text = "I couldn't read that time. Try 18:30 or 6:30 PM."; return; }
            if (text.Text.Trim().Length == 0) { status.Text = "Type what the reminder should say."; return; }
            S.Reminders.Add(new ReminderItem { Time = when.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), Text = text.Text.Trim(), Repeat = daily.IsChecked == true });
            Settings.Save();
            Show(_section, animate: false);
        });
        add.Margin = new Thickness(10, 0, 0, 0);
        var line = new DockPanel();
        DockPanel.SetDock(time, Dock.Left);
        DockPanel.SetDock(add, Dock.Right);
        DockPanel.SetDock(daily, Dock.Right);
        line.Children.Add(time);
        line.Children.Add(add);
        line.Children.Add(daily);
        line.Children.Add(text);
        var form = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        form.Children.Add(new TextBlock { Text = "New reminder", Foreground = TextMain, FontSize = 14.5, Margin = new Thickness(0, 0, 0, 9) });
        form.Children.Add(line);
        form.Children.Add(status);
        Host.Children.Add(Card(form));

        if (S.Reminders.Count == 0) return;
        Host.Children.Add(Heading("Upcoming"));
        var rows = new List<UIElement>();
        foreach (var r in S.Reminders.OrderBy(r => r.Time).ToList())
        {
            var remove = PlainButton("Remove", () => { S.Reminders.Remove(r); Settings.Save(); Show(_section, animate: false); });
            var row = new DockPanel { Margin = new Thickness(18, 11, 12, 11) };
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(new TextBlock { Text = r.Time, Foreground = Ember, FontSize = 16, FontWeight = FontWeights.SemiBold, FontFamily = Display, Width = 62, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(Labels(r.Text, r.Repeat ? "Every day" : "Once"));
            rows.Add(row);
        }
        Host.Children.Add(Card(rows.ToArray()));
    }

    private void BuildNote()
    {
        Note("A little tag that hangs from the island on a thread. It sways when it appears, then hangs still.");
        var box = new TextBox { Style = (Style)FindResource("Field"), Text = S.NoteText, MaxLength = 140 };
        var save = PlainButton("Save", () =>
        {
            S.NoteText = box.Text.Trim();
            if (S.NoteText.Length > 0) S.NoteMode = "Custom";
            Settings.Save();
            Show(_section, animate: false);
        });
        save.Margin = new Thickness(8, 0, 0, 0);
        var line = new DockPanel();
        DockPanel.SetDock(save, Dock.Right);
        line.Children.Add(save);
        line.Children.Add(box);
        var own = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        own.Children.Add(new TextBlock { Text = "Your reminder", Foreground = TextMain, FontSize = 14.5 });
        own.Children.Add(new TextBlock { Text = "Something you must not forget (up to 140 characters).", Foreground = TextDim, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 9) });
        own.Children.Add(line);

        Host.Children.Add(Card(
            SwitchRow("Show the hanging note", null, () => S.ShowNote, v => S.ShowNote = v),
            ChoiceRow("What it says", "Quotes change every hour; click the tag for the next one.",
                new[] { ("Motivational quotes", "Quote"), ("My reminder", "Custom") },
                v => S.NoteMode == v, v => S.NoteMode = v, stacked: true),
            own,
            ChoiceRow("Material", null,
                new[] { ("Match island theme", "Theme"), ("Cardboard", "Cardboard"), ("Sticky note", "Sticky"), ("Paper", "Paper"), ("Starry night", "Starry"), ("Great wave", "Wave"), ("Sunset wash", "Sunset"), ("Blueprint", "Blueprint") },
                v => S.NoteStyle == v, v => S.NoteStyle = v, stacked: true)));
    }

    private void BuildAbout()
    {
        Host.Children.Add(Credit());
        Host.Children.Add(new Border { Height = 16 });
        Host.Children.Add(Card(AboutRow()));
    }

    private void ResetAll()
    {
        var fresh = new Settings();
        foreach (var p in typeof(Settings).GetProperties().Where(p => p.CanWrite && p.GetIndexParameters().Length == 0 && !p.GetMethod!.IsStatic))
            p.SetValue(S, p.GetValue(fresh));
        Settings.Save();
        _weather.Clear();
        Show(_section);
    }

    // ------------------------------------------------------------------ live preview

    /// <summary>A miniature island drawn with the real theme brushes and the real effect, so choices can be judged before closing.</summary>
    private UIElement Preview()
    {
        var fx = new EffectLayer();
        void Sync()
        {
            fx.Effect = Enum.TryParse<IslandEffect>(S.Effect, out var e) ? e : IslandEffect.None;
            fx.Accent = Theme.Current.AccentColor;
            fx.Livery = S.Car.Length > 0 ? Theme.Resolve(S.Car).Livery : -1;
            fx.Label = (S.BgText.Length > 0 ? S.BgText : Theme.Current.Name).ToUpperInvariant();
        }
        Sync();
        Settings.Changed += Sync;
        fx.Unloaded += (_, _) => Settings.Changed -= Sync;
        _previewFx = fx;

        TextBlock Text(string s, double size, FontWeight weight, string brushKey, Thickness margin = default)
        {
            var t = new TextBlock { Text = s, FontSize = size, FontWeight = weight, FontFamily = Display, Margin = margin };
            t.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            return t;
        }

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
        int hour = DateTime.Now.Hour;
        left.Children.Add(Text(hour < 5 ? "GOOD NIGHT" : hour < 12 ? "GOOD MORNING" : hour < 17 ? "GOOD AFTERNOON" : "GOOD EVENING", 10.5, FontWeights.Bold, "Accent"));
        left.Children.Add(Text(DateTime.Now.ToString(S.Use24Hour ? "HH:mm" : "h:mm"), 38, FontWeights.SemiBold, "TextPrimary", new Thickness(-1, -3, 0, 0)));
        left.Children.Add(Text(DateTime.Now.ToString("dddd, d MMMM"), 12.5, FontWeights.Normal, "TextSecondary"));

        var bars = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 26, 0) };
        foreach (double h in new[] { 9.0, 20, 13, 26, 16, 8 })
        {
            var bar = new Rectangle { Width = 3.5, Height = h, RadiusX = 1.75, RadiusY = 1.75, Margin = new Thickness(1.6, 0, 1.6, 0), VerticalAlignment = VerticalAlignment.Center };
            bar.SetResourceReference(Shape.FillProperty, "Accent");
            bars.Children.Add(bar);
        }

        var inner = new Grid();
        inner.Children.Add(fx);
        inner.Children.Add(left);
        inner.Children.Add(bars);

        var island = new Border { Width = 360, Height = 116, CornerRadius = new CornerRadius(36), BorderThickness = new Thickness(1), Child = inner, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        island.SetResourceReference(Border.BackgroundProperty, "IslandBg");
        island.SetResourceReference(Border.BorderBrushProperty, "IslandBorder");
        inner.Clip = new RectangleGeometry(new Rect(0, 0, 358, 114), 35, 35);

        // A warm, neutral "desktop" behind it so both dark and light themes can be judged.
        var desk = new LinearGradientBrush(Theme.C("#3A342D"), Theme.C("#1B1917"), new Point(0, 0), new Point(1, 1));
        desk.Freeze();
        _previewTimer.Start();
        return new Border { Height = 196, CornerRadius = new CornerRadius(20), Background = desk, Child = island, BorderBrush = Line, BorderThickness = new Thickness(1) };
    }

    // ------------------------------------------------------------------ building blocks

    private Button PlainButton(string text, Action onClick)
    {
        var b = new Button { Content = text, Style = (Style)FindResource("PlainButton") };
        b.Click += (_, _) => onClick();
        return b;
    }

    private UIElement Heading(string title, string? sub = null)
    {
        var sp = new StackPanel { Margin = new Thickness(2, 28, 0, 12) };
        sp.Children.Add(new TextBlock { Text = title, FontSize = 16.5, FontWeight = FontWeights.SemiBold, Foreground = TextMain });
        if (sub != null) sp.Children.Add(new TextBlock { Text = sub, Foreground = TextDim, FontSize = 13, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    private Border Card(params UIElement[] rows)
    {
        var sp = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0) sp.Children.Add(new Border { Height = 1, Background = Line, Margin = new Thickness(18, 0, 18, 0) });
            sp.Children.Add(rows[i]);
        }
        return new Border { Background = CardBg, CornerRadius = new CornerRadius(16), Child = sp, BorderBrush = Line, BorderThickness = new Thickness(1) };
    }

    private static StackPanel Labels(string title, string? sub)
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock { Text = title, Foreground = TextMain, FontSize = 14.5 });
        if (sub != null)
            sp.Children.Add(new TextBlock { Text = sub, Foreground = TextDim, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    private UIElement SwitchRow(string title, string? sub, Func<bool> get, Action<bool> set)
    {
        var grid = new Grid { Margin = new Thickness(18, 13, 18, 13), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var sw = new CheckBox { Style = (Style)FindResource("Switch"), IsChecked = get(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        void Commit()
        {
            set(sw.IsChecked == true);
            Settings.Save();
        }
        sw.Click += (_, _) => Commit();
        // Clicking anywhere on the row flips the switch.
        grid.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && IsInside(d, sw)) return;
            sw.IsChecked = sw.IsChecked != true;
            Commit();
        };

        grid.Children.Add(Labels(title, sub));
        Grid.SetColumn(sw, 1);
        grid.Children.Add(sw);
        return grid;
    }

    private static bool IsInside(DependencyObject d, DependencyObject ancestor)
    {
        for (DependencyObject? x = d; x != null; x = x is Visual ? VisualTreeHelper.GetParent(x) : null)
            if (ReferenceEquals(x, ancestor)) return true;
        return false;
    }

    /// <summary>A row with a segmented picker. stacked puts the picker under the label (for long pickers).</summary>
    private UIElement ChoiceRow<T>(string title, string? sub, (string Label, T Value)[] options, Func<T, bool> isCurrent, Action<T> set, bool stacked = false)
    {
        var seg = new WrapPanel { Orientation = Orientation.Horizontal };
        string group = "choice" + _groupId++;
        foreach (var (label, value) in options)
        {
            var rb = new RadioButton
            {
                Content = label,
                GroupName = group,
                Style = (Style)FindResource("Segment"),
                IsChecked = isCurrent(value),
                Margin = new Thickness(1, 0, 1, 0),
            };
            // Click, not Checked: only a real user choice should ever write a setting.
            rb.Click += (_, _) =>
            {
                set(value);
                Settings.Save();
            };
            seg.Children.Add(rb);
        }
        var pill = new Border { Background = Hex("#1E1E21"), CornerRadius = new CornerRadius(11), Padding = new Thickness(2), Child = seg, HorizontalAlignment = HorizontalAlignment.Left };

        if (stacked)
        {
            var sp = new StackPanel { Margin = new Thickness(18, 13, 18, 14) };
            sp.Children.Add(Labels(title, sub));
            pill.Margin = new Thickness(-2, 10, 0, 0);
            sp.Children.Add(pill);
            return sp;
        }

        var grid = new Grid { Margin = new Thickness(18, 11, 12, 11) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Labels(title, sub));
        Grid.SetColumn(pill, 1);
        grid.Children.Add(pill);
        return grid;
    }

    private UIElement WeatherRow()
    {
        var box = new TextBox { Style = (Style)FindResource("Field"), Text = S.WeatherCity, MinWidth = 220 };
        var status = new TextBlock { Foreground = TextDim, FontSize = 12.5, Margin = new Thickness(1, 9, 0, 0), TextWrapping = TextWrapping.Wrap };
        status.Text = WeatherService.Enabled ? $"Showing weather for {S.WeatherCity}." : "Type your city and press Set.";

        Button? setBtn = null;
        async void Apply()
        {
            string query = box.Text.Trim();
            if (query.Length < 2)
            {
                status.Text = "Type at least two letters of a city name.";
                return;
            }
            setBtn!.IsEnabled = false;
            status.Text = "Looking up…";
            var hit = await WeatherService.FindCityAsync(query);
            setBtn.IsEnabled = true;
            if (hit is not { } city)
            {
                status.Text = "Couldn't find that city (or there's no internet). Check the spelling and try again.";
                return;
            }
            S.WeatherCity = city.Name;
            S.WeatherLat = city.Lat;
            S.WeatherLon = city.Lon;
            box.Text = city.Name;
            Settings.Save();
            await _weather.RefreshAsync(force: true);
            status.Text = _weather.HasData ? $"Showing weather for {city.Name}." : "Saved, but the forecast couldn't be loaded yet. WindX will retry.";
        }

        setBtn = PlainButton("Set", Apply);
        setBtn.Margin = new Thickness(8, 0, 0, 0);
        var off = PlainButton("Turn off", () =>
        {
            S.WeatherCity = "";
            S.WeatherLat = S.WeatherLon = null;
            box.Text = "";
            Settings.Save();
            _weather.Clear();
            status.Text = "Weather is off. WindX makes no internet requests.";
        });
        off.Margin = new Thickness(8, 0, 0, 0);
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Apply(); };

        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(off, Dock.Right);
        DockPanel.SetDock(setBtn, Dock.Right);
        line.Children.Add(off);
        line.Children.Add(setBtn);
        line.Children.Add(box);

        var sp = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
        sp.Children.Add(new TextBlock { Text = "City", Foreground = TextMain, FontSize = 14.5, Margin = new Thickness(0, 0, 0, 9) });
        sp.Children.Add(line);
        sp.Children.Add(status);
        return sp;
    }

    private UIElement AboutRow()
    {
        var sp = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
        string[] points =
        {
            "No account, no ads, no tracking, no analytics. WindX never phones home.",
            "Your settings stay in a small file on this PC (%AppData%\\WindX).",
            "The only internet request is the optional weather forecast, and only after you add a city.",
            "Runs without administrator rights and changes nothing in Windows except what you press.",
            "Open source under the MIT licence: anyone can read exactly what it does.",
            "Team islands are unofficial fan themes. WindX is not affiliated with or endorsed by any F1 team, Formula 1, PUBG or Krafton.",
        };
        foreach (var p in points)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var tick = new TextBlock { Text = "", FontFamily = Icons, Foreground = Ember, FontSize = 12, Margin = new Thickness(0, 3, 10, 0) };
            DockPanel.SetDock(tick, Dock.Left);
            row.Children.Add(tick);
            row.Children.Add(new TextBlock { Text = p, Foreground = TextMain, FontSize = 13.5, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(row);
        }

        var links = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var (label, url) in new[] { ("Website", Links.Site), ("Source code", Links.Repo), ("Privacy policy", Links.Privacy) })
        {
            var b = PlainButton(label, () => Links.Open(url));
            b.Margin = new Thickness(0, 0, 8, 0);
            links.Children.Add(b);
        }
        sp.Children.Add(links);
        return sp;
    }

    // ------------------------------------------------------------------ theme cards

    private UIElement ThemeCard(Theme theme)
    {
        // A miniature island drawn in the theme's real colours.
        var accent = new SolidColorBrush(theme.AccentColor);
        var bars = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 11, 0) };
        foreach (double h in new[] { 6.0, 12, 8, 14, 7 })
            bars.Children.Add(new Rectangle { Width = 2.5, Height = h, RadiusX = 1.25, RadiusY = 1.25, Fill = accent, Margin = new Thickness(1, 0, 1, 0), VerticalAlignment = VerticalAlignment.Center });

        var inner = new Grid();
        inner.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = accent, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 0, 0, 0) });
        inner.Children.Add(new Border { Width = 24, Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.C(theme.Text)), Opacity = 0.9, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(29, 0, 0, 0) });
        inner.Children.Add(bars);

        var pill = new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = theme.BackgroundBrush(),
            BorderBrush = new SolidColorBrush(Theme.C(theme.Border)),
            BorderThickness = new Thickness(1),
            Child = inner,
        };

        var content = new StackPanel { Margin = new Thickness(10, 14, 10, 10) };
        content.Children.Add(pill);
        content.Children.Add(new TextBlock { Text = theme.Name, Foreground = TextMain, FontSize = 12.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });

        var card = new Border
        {
            Background = Hex("#1B1B1E"),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Margin = new Thickness(5),
            Cursor = Cursors.Hand,
            Child = content,
        };
        card.MouseLeftButtonUp += (_, _) =>
        {
            S.Theme = theme.Name;
            // A team island brings its animation (and its car) with it.
            if (theme.Fx != null)
            {
                S.Effect = theme.Fx;
                if (theme.Livery >= 0) S.Car = theme.Name;
            }
            Settings.Save();
            if (theme.Fx != null)
            {
                double at = Scroller.VerticalOffset;
                Show(_section, animate: false);
                Scroller.ScrollToVerticalOffset(at);
            }
            else RefreshThemeSelection();
        };
        _themeCards.Add((card, theme));
        return card;
    }

    private void RefreshThemeSelection()
    {
        foreach (var (card, theme) in _themeCards)
            card.BorderBrush = theme.Name == S.Theme ? Ember : Brushes.Transparent;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
