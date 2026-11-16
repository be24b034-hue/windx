using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WindX.Controls;

/// <summary>Damped spring: overshoots a little, then settles. Gives the island its "liquid" feel.</summary>
public sealed class SpringEase : EasingFunctionBase
{
    public SpringEase() => EasingMode = EasingMode.EaseIn;

    protected override double EaseInCore(double t) => 1 - Math.Exp(-8 * t) * Math.Cos(10 * t);

    protected override Freezable CreateInstanceCore() => new SpringEase();
}

/// <summary>Row of rounded bars that dance with the audio level.</summary>
public sealed class VisualizerBars : FrameworkElement
{
    private const int Count = 5;
    private const double BarWidth = 3;
    private const double Gap = 2.5;
    private const double MinBar = 3;

    private readonly double[] _levels = new double[Count];

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(VisualizerBars),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public int BarCount => Count;

    public void SetLevels(double[] levels)
    {
        bool changed = false;
        for (int i = 0; i < Count; i++)
        {
            if (Math.Abs(_levels[i] - levels[i]) > 0.004) changed = true;
            _levels[i] = levels[i];
        }
        if (changed) InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Count * BarWidth + (Count - 1) * Gap, double.IsInfinity(availableSize.Height) ? 16 : availableSize.Height);

    protected override void OnRender(DrawingContext dc)
    {
        double h = ActualHeight;
        for (int i = 0; i < Count; i++)
        {
            double bar = MinBar + (h - MinBar) * Math.Clamp(_levels[i], 0, 1);
            double x = i * (BarWidth + Gap);
            dc.DrawRoundedRectangle(Fill, null, new Rect(x, (h - bar) / 2, BarWidth, bar), BarWidth / 2, BarWidth / 2);
        }
    }
}

/// <summary>Thumb-less pill slider (0..1) that you click or drag anywhere on.</summary>
public sealed class MiniSlider : FrameworkElement
{
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(MiniSlider),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(MiniSlider),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(MiniSlider),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Raised only for changes made with the mouse, not programmatic updates.</summary>
    public event Action<double>? UserChanged;

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public bool IsDragging => IsMouseCaptured;

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width, 24);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // whole area is clickable
        double th = IsMouseOver || IsMouseCaptured ? 9 : 7;
        double y = (h - th) / 2;
        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(0, y, w, th), th / 2, th / 2);
        double fw = Math.Max(th, w * Math.Clamp(Value, 0, 1));
        dc.DrawRoundedRectangle(Fill, null, new Rect(0, y, fw, th), th / 2, th / 2);
    }

    private void Update(System.Windows.Input.MouseEventArgs e)
    {
        if (ActualWidth <= 0) return;
        double v = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1);
        Value = v;
        UserChanged?.Invoke(v);
    }

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        CaptureMouse();
        Update(e);
        e.Handled = true;
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (IsMouseCaptured) Update(e);
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e) => InvalidateVisual();
    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e) => InvalidateVisual();
}

/// <summary>Tiny battery icon with a proportional fill.</summary>
public sealed class BatteryGlyph : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(double), typeof(BatteryGlyph),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(BatteryGlyph),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(BatteryGlyph),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }
    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(26, 12);

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(null, new Pen(Stroke, 1), new Rect(0.5, 0.5, 22, 11), 3.5, 3.5);
        dc.DrawRoundedRectangle(Stroke, null, new Rect(23.5, 4, 2, 4), 1, 1);
        double w = 18 * Math.Clamp(Level, 0.06, 1);
        dc.DrawRoundedRectangle(Fill, null, new Rect(2.5, 2.5, w, 7), 2, 2);
    }
}
