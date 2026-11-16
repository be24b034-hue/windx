using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindX.Services;

namespace WindX.Controls;

/// <summary>The WindX mark: three streams of wind, each curling at its tip. One definition, used everywhere.</summary>
public static class Logo
{
    // Drawn on a 64x64 grid.
    public const string Stream1 = "M10 23 H36 A8 8 0 1 0 28 15";
    public const string Stream2 = "M5 33 H47 A9 9 0 1 1 38 42";
    public const string Stream3 = "M15 43 H25 A6 6 0 1 1 19 49";

    // Brand palette: ink, paper and one accent.
    public static readonly Color Ink = Color.FromRgb(0x0E, 0x0E, 0x0F);
    public static readonly Color Paper = Color.FromRgb(0xF4, 0xF1, 0xEA);
    public static readonly Color Ember = Color.FromRgb(0xFF, 0x5B, 0x1F);

    private static readonly Geometry[] Streams = { Parse(Stream1), Parse(Stream2), Parse(Stream3) };

    private static Geometry Parse(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }

    private static readonly Brush PaperBrush = Frozen(Paper);
    public static readonly Brush EmberBrush = Frozen(Ember);

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>Draws the mark into a square of the given size. progress (0..1) draws the streams on, like wind arriving.</summary>
    public static void Draw(DrawingContext dc, double size, bool tile, Brush? stroke = null, double progress = 1)
    {
        double k = size / 64.0;
        dc.PushTransform(new ScaleTransform(k, k));
        if (tile)
        {
            var bg = new LinearGradientBrush(Color.FromRgb(0x1A, 0x1A, 0x1C), Ink, new Point(0.2, 0), new Point(0.8, 1));
            dc.DrawRoundedRectangle(bg, null, new Rect(0, 0, 64, 64), 15, 15);
        }

        for (int i = 0; i < Streams.Length; i++)
        {
            // Two streams in paper white, the middle one in ember: the X-factor.
            var pen = new Pen(stroke ?? (i == 1 ? EmberBrush : PaperBrush), 5.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            // Stagger the three streams slightly.
            double p = Math.Clamp(progress * 1.5 - i * 0.25, 0, 1);
            if (p <= 0) continue;
            if (p >= 1)
            {
                dc.DrawGeometry(null, pen, Streams[i]);
                continue;
            }
            var partial = pen.Clone();
            partial.DashCap = PenLineCap.Round;
            // One long dash that reveals the path from its start.
            const double length = 110; // longer than any stream, in pen-width units below
            partial.DashStyle = new DashStyle(new[] { p * length / 5.2, 1000 }, 0);
            dc.DrawGeometry(null, partial, Streams[i]);
        }
        dc.Pop();
    }

    public static BitmapSource Render(int px, bool tile = true)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) Draw(dc, px, tile);
        var bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static byte[] Png(BitmapSource src)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Writes windx.ico (multi-size) and logo PNGs into a folder. Used by the build, not at runtime.</summary>
    public static void Export(string dir)
    {
        Directory.CreateDirectory(dir);
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var images = sizes.Select(s => Png(Render(s))).ToArray();

        using (var fs = File.Create(Path.Combine(dir, "windx.ico")))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }

        File.WriteAllBytes(Path.Combine(dir, "logo-512.png"), Png(Render(512)));
        File.WriteAllBytes(Path.Combine(dir, "logo-mark-512.png"), Png(Render(512, tile: false)));
    }
}

/// <summary>The logo as an element. Animate Progress 0→1 to "blow" it onto the screen.</summary>
public sealed class LogoMark : FrameworkElement
{
    public static readonly DependencyProperty TileProperty = DependencyProperty.Register(
        nameof(Tile), typeof(bool), typeof(LogoMark), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(LogoMark), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(LogoMark), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool Tile { get => (bool)GetValue(TileProperty); set => SetValue(TileProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override void OnRender(DrawingContext dc) => Logo.Draw(dc, Math.Min(ActualWidth, ActualHeight), Tile, Stroke, Progress);
}

/// <summary>Circular gauge (0..1).</summary>
public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingGauge), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(RingGauge), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(RingGauge), new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        const double thickness = 6;
        double size = Math.Min(ActualWidth, ActualHeight);
        double r = (size - thickness) / 2;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(null, new Pen(TrackBrush, thickness), c, r, r);

        double v = Math.Clamp(Value, 0, 1);
        if (v <= 0.002) return;
        var pen = new Pen(Fill, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (v >= 0.999)
        {
            dc.DrawEllipse(null, pen, c, r, r);
            return;
        }
        double a = v * 2 * Math.PI;
        var start = new Point(c.X, c.Y - r);
        var end = new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, v > 0.5, SweepDirection.Clockwise, true, false);
        }
        dc.DrawGeometry(null, pen, g);
    }
}

/// <summary>Hand-drawn, single-colour weather glyphs so they follow the theme.</summary>
public sealed class WeatherIcon : FrameworkElement
{
    public static readonly DependencyProperty SkyProperty = DependencyProperty.Register(
        nameof(Sky), typeof(Sky), typeof(WeatherIcon), new FrameworkPropertyMetadata(Sky.Clear, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsDayProperty = DependencyProperty.Register(
        nameof(IsDay), typeof(bool), typeof(WeatherIcon), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(WeatherIcon), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public Sky Sky { get => (Sky)GetValue(SkyProperty); set => SetValue(SkyProperty, value); }
    public bool IsDay { get => (bool)GetValue(IsDayProperty); set => SetValue(IsDayProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

    private void Sun(DrawingContext dc, Point c, double r)
    {
        dc.DrawEllipse(Fill, null, c, r, r);
        var pen = new Pen(Fill, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            dc.DrawLine(pen,
                new Point(c.X + Math.Cos(a) * (r + 1.8), c.Y + Math.Sin(a) * (r + 1.8)),
                new Point(c.X + Math.Cos(a) * (r + 3.6), c.Y + Math.Sin(a) * (r + 3.6)));
        }
    }

    private void Moon(DrawingContext dc, Point c, double r)
    {
        var crescent = new CombinedGeometry(GeometryCombineMode.Exclude,
            new EllipseGeometry(c, r, r),
            new EllipseGeometry(new Point(c.X + r * 0.55, c.Y - r * 0.35), r * 0.85, r * 0.85));
        dc.DrawGeometry(Fill, null, crescent);
    }

    private void Cloud(DrawingContext dc, double dx, double dy, double scale = 1)
    {
        dc.PushTransform(new TranslateTransform(dx, dy));
        dc.PushTransform(new ScaleTransform(scale, scale, 12, 14));
        var g = new GeometryGroup { FillRule = FillRule.Nonzero };
        g.Children.Add(new EllipseGeometry(new Point(8.5, 14.5), 4, 4));
        g.Children.Add(new EllipseGeometry(new Point(13.3, 12), 5.2, 5.2));
        g.Children.Add(new EllipseGeometry(new Point(17.8, 15), 3.5, 3.5));
        g.Children.Add(new RectangleGeometry(new Rect(8.5, 14.5, 9.3, 4)));
        dc.DrawGeometry(Fill, null, g);
        dc.Pop();
        dc.Pop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double k = Math.Min(ActualWidth, ActualHeight) / 24.0;
        dc.PushTransform(new ScaleTransform(k, k));
        var pen = new Pen(Fill, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        switch (Sky)
        {
            case Sky.Clear:
                if (IsDay) Sun(dc, new Point(12, 12), 4.6); else Moon(dc, new Point(12, 12), 7);
                break;
            case Sky.PartlyCloudy:
                if (IsDay) Sun(dc, new Point(8.5, 8.5), 3); else Moon(dc, new Point(8.5, 8.5), 4.5);
                Cloud(dc, 1.5, 2, 0.85);
                break;
            case Sky.Cloudy:
                Cloud(dc, -0.5, -1);
                break;
            case Sky.Fog:
                Cloud(dc, -0.5, -4.5, 0.9);
                dc.DrawLine(pen, new Point(5, 17.5), new Point(19, 17.5));
                dc.DrawLine(pen, new Point(7.5, 21), new Point(16.5, 21));
                break;
            case Sky.Rain:
                Cloud(dc, -0.5, -4.5, 0.9);
                for (int i = 0; i < 3; i++) dc.DrawLine(pen, new Point(9 + i * 4, 17.5), new Point(7.6 + i * 4, 21.2));
                break;
            case Sky.Snow:
                Cloud(dc, -0.5, -4.5, 0.9);
                for (int i = 0; i < 3; i++) dc.DrawEllipse(Fill, null, new Point(8 + i * 4, 19.5 + (i % 2) * 1.6), 1.1, 1.1);
                break;
            default: // storm
                Cloud(dc, -0.5, -4.5, 0.9);
                var bolt = new StreamGeometry();
                using (var ctx = bolt.Open())
                {
                    ctx.BeginFigure(new Point(13.2, 15.5), true, true);
                    ctx.LineTo(new Point(9.4, 20), true, false);
                    ctx.LineTo(new Point(12, 20), true, false);
                    ctx.LineTo(new Point(10.6, 23.6), true, false);
                    ctx.LineTo(new Point(15, 18.4), true, false);
                    ctx.LineTo(new Point(12.4, 18.4), true, false);
                }
                dc.DrawGeometry(Fill, null, bolt);
                break;
        }
        dc.Pop();
    }
}
