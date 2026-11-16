using System.Windows;
using System.Windows.Media;

namespace WindX.Controls;

public enum IslandEffect
{
    None, Wind, Water, Fire, Thunder, Rain, Snow, Sakura, Bubbles, Aurora, Stars,
    Race, Cats, Fireflies, Matrix, Hearts, Confetti, Meteors, Leaves, Ripples, Synthwave, Clouds, Pulse,
    Battleground, Ribbons, Galaxy,
    Inferno, Pool, Lights, Mist,   // realistic simulations
    Overgrown, Signal, Sunbeams,   // scenes
    Infinity, WaterBlade, FlameBlade, ThunderStep, Spiral, SpiritOrb, Domain, BlackFlash, Beam, CrimsonMoon,   // power pack
    Glass,
}

/// <summary>
/// Living backdrop drawn inside the island. Fully procedural (no images, no video), so it costs a few
/// kilobytes of memory, and it only redraws when the island asks (MainWindow's effect timer).
/// Water and fire are drawn in a woodblock-print style: bold bands, white foam curls, hooked flame tips.
/// </summary>
public sealed partial class EffectLayer : FrameworkElement
{
    private IslandEffect _effect = IslandEffect.None;
    private Color _accent = Colors.White;
    private double _time, _intensity = 0.5;

    private Brush? _windBrush;
    private readonly Brush?[] _aurora = new Brush?[3];

    public EffectLayer() => IsHitTestVisible = false;

    public IslandEffect Effect
    {
        get => _effect;
        set { _effect = value; InvalidateVisual(); }
    }

    /// <summary>Theme accent; wind, aurora and stars pick it up. The elemental effects keep their natural colours.</summary>
    public Color Accent
    {
        get => _accent;
        set
        {
            if (_accent == value) return;
            _accent = value;
            _windBrush = null;
            _aurora[0] = _aurora[1] = _aurora[2] = null;
            InvalidateVisual();
        }
    }

    /// <summary>Advance to a point in time. intensity (0..1) follows the music when something is playing.</summary>
    public void Step(double time, double intensity)
    {
        _time = time;
        _intensity += (Math.Clamp(intensity, 0, 1) - _intensity) * 0.25;
        if (_effect != IslandEffect.None) InvalidateVisual();
    }

    // Cheap repeatable pseudo-random number in 0..1 for element i.
    private static double H(int i, int salt)
    {
        double v = Math.Sin(i * 127.1 + salt * 311.7) * 43758.5453;
        return v - Math.Floor(v);
    }

    private static Brush Solid(byte a, byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        return br;
    }

    private static Pen Stroke(Brush brush, double thickness)
    {
        var p = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        p.Freeze();
        return p;
    }

    // Vertical fade: transparent at the top of the shape, solid at its base.
    private static Brush Fade(byte a, byte r, byte g, byte b, double mid = 0.4)
    {
        var br = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        br.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(a * 0.35), r, g, b), 0));
        br.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(a * 0.8), r, g, b), mid));
        br.GradientStops.Add(new GradientStop(Color.FromArgb(a, r, g, b), 1));
        br.Freeze();
        return br;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 8 || h < 4) return;
        switch (_effect)
        {
            case IslandEffect.Wind: Wind(dc, w, h); break;
            case IslandEffect.Water: Water(dc, w, h); break;
            case IslandEffect.Fire: Fire(dc, w, h); break;
            case IslandEffect.Thunder: Thunder(dc, w, h); break;
            case IslandEffect.Rain: Rain(dc, w, h); break;
            case IslandEffect.Snow: Snow(dc, w, h); break;
            case IslandEffect.Sakura: Sakura(dc, w, h); break;
            case IslandEffect.Bubbles: Bubbles(dc, w, h); break;
            case IslandEffect.Aurora: Aurora(dc, w, h); break;
            case IslandEffect.Stars: Stars(dc, w, h); break;
            default: More(dc, w, h); break;
        }
    }

    // ------------------------------------------------------------------ wind

    private void Wind(DrawingContext dc, double w, double h)
    {
        if (_windBrush == null)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0, _accent.R, _accent.G, _accent.B), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(255, _accent.R, _accent.G, _accent.B), 0.78));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0, _accent.R, _accent.G, _accent.B), 1));
            b.Freeze();
            _windBrush = b;
        }

        int n = Math.Clamp((int)(w * h / 5200) + 4, 5, 18);
        for (int i = 0; i < n; i++)
        {
            double speed = (46 + H(i, 1) * 95) * (0.8 + _intensity * 0.9);
            double len = 34 + H(i, 2) * 105;
            double span = w + len;
            double x = (_time * speed + H(i, 4) * span) % span - len;
            double y = 3 + H(i, 3) * (h - 6) + Math.Sin(_time * 0.9 + i) * 2.2;
            double t = 1 + H(i, 5) * 1.3;
            dc.PushOpacity(0.10 + 0.24 * H(i, 6));
            dc.DrawRoundedRectangle(_windBrush, null, new Rect(x, y, len, t), t / 2, t / 2);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ water (woodblock waves)

    private static readonly Brush[] WaveFill =
    {
        Fade(215, 10, 38, 104, 0.3), Fade(225, 16, 66, 158, 0.3), Fade(235, 26, 104, 214, 0.3),
    };
    private static readonly Brush FoamBrush = Solid(240, 255, 255, 255);
    private static readonly Brush StriaBrush = Solid(150, 150, 212, 255);

    private void Water(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double swell = 1 + _intensity * 0.7;

        for (int layer = 0; layer < 3; layer++)
        {
            // Peaked crests (not a soft sine): each wave rears up and leans forward.
            double amp = (pill ? 4.5 : 13) * swell * (0.7 + layer * 0.18);
            double level = h * (pill ? 0.72 : 0.80) + layer * (pill ? 2.5 : 7);
            double k = (pill ? 0.085 : 0.05) * (1.25 - layer * 0.16);
            double speed = 0.9 + layer * 0.55, off = layer * 2.1;

            double Y(double x)
            {
                double s = 0.5 + 0.5 * Math.Sin(k * x - _time * speed + off);
                return level - amp * Math.Pow(s, 2.6) + amp * 0.18 * Math.Sin(x * 0.021 + _time * 0.6 + layer);
            }

            var body = new StreamGeometry();
            using (var ctx = body.Open())
            {
                ctx.BeginFigure(new Point(-2, h + 2), true, true);
                for (double x = -2; x <= w + 4; x += 3) ctx.LineTo(new Point(x, Y(x)), true, true);
                ctx.LineTo(new Point(w + 2, h + 2), false, false);
            }
            body.Freeze();
            dc.DrawGeometry(WaveFill[layer], null, body);

            // White foam line along the crest, thicker on the front wave.
            var crest = new StreamGeometry();
            using (var ctx = crest.Open())
            {
                ctx.BeginFigure(new Point(-2, Y(-2)), false, false);
                for (double x = 1; x <= w + 4; x += 3) ctx.LineTo(new Point(x, Y(x)), true, true);
            }
            crest.Freeze();
            dc.PushOpacity(0.55 + layer * 0.2);
            dc.DrawGeometry(null, Stroke(FoamBrush, (pill ? 1.0 : 1.5) + layer * 0.35), crest);
            dc.Pop();

            if (layer == 2)
            {
                // Pale striations following the swell, like carved lines in a print.
                var stria = Stroke(StriaBrush, pill ? 0.8 : 1.1);
                for (int s = 1; s <= (pill ? 1 : 3); s++)
                {
                    dc.PushTransform(new TranslateTransform(0, s * (pill ? 4 : 7)));
                    dc.PushOpacity(0.75 - s * 0.18);
                    dc.DrawGeometry(null, stria, crest);
                    dc.Pop();
                    dc.Pop();
                }
            }

            // Foam claws curling off every peak, with spray flying ahead of them.
            double period = 2 * Math.PI / k;
            double first = (Math.PI / 2 + _time * speed - off) / k;
            first -= Math.Floor(first / period) * period + period;
            double r = (pill ? 2.6 : 6.5) * (0.75 + layer * 0.2) * swell;
            var curlPen = Stroke(FoamBrush, (pill ? 1.0 : 1.6) + layer * 0.3);
            int n = 0;
            for (double px = first; px < w + period; px += period, n++)
            {
                double py = Y(px);
                var curl = new StreamGeometry();
                using (var ctx = curl.Open())
                {
                    ctx.BeginFigure(new Point(px - r * 0.6, py + r * 0.15), false, false);
                    ctx.ArcTo(new Point(px + r * 1.25, py + r * 0.55), new Size(r, r * 0.95), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.ArcTo(new Point(px + r * 0.55, py + r * 0.95), new Size(r * 0.5, r * 0.5), 0, false, SweepDirection.Clockwise, true, true);
                }
                curl.Freeze();
                dc.PushOpacity(0.6 + layer * 0.2);
                dc.DrawGeometry(null, curlPen, curl);
                for (int d = 0; d < 3; d++)
                {
                    double life = (_time * 1.1 + H(n + layer * 7, d) * 3) % 1;
                    double dx = px + r * (1.4 + d * 0.55) + life * r * 1.2;
                    double dy = py + r * 0.2 - Math.Sin(life * Math.PI) * r * (0.9 + d * 0.3);
                    double dr = (pill ? 0.7 : 1.3) * (1 - life * 0.6);
                    dc.DrawEllipse(FoamBrush, null, new Point(dx, dy), dr, dr);
                }
                dc.Pop();
            }
        }
    }

    // ------------------------------------------------------------------ fire (hooked, layered flames)

    private static readonly Brush[] Flame =
    {
        Fade(210, 150, 16, 8), Fade(225, 232, 52, 12), Fade(235, 255, 128, 16), Fade(245, 255, 208, 64), Fade(250, 255, 246, 205),
    };
    private static readonly Brush FlameLine = Solid(150, 255, 236, 170);
    private static readonly Brush EmberBrush = Solid(255, 255, 214, 120);
    private static readonly Brush HeatGlow = MakeHeat();

    private static Brush MakeHeat()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 70, 20), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 84, 20), 1));
        b.Freeze();
        return b;
    }

    private void Fire(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double glowTop = h * (pill ? 0.05 : 0.42);
        dc.DrawRectangle(HeatGlow, null, new Rect(0, glowTop, w, h - glowTop));

        double power = 0.78 + _intensity * 0.6;
        for (int layer = 0; layer < Flame.Length; layer++)
        {
            double shrink = 1 - layer * 0.17;                       // inner layers are shorter and hotter
            int count = Math.Max(3, (int)(w / (34 - layer * 3.5)));
            double cell = w / count;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                for (int i = -1; i <= count; i++)
                {
                    int id = i + layer * 31;
                    // Slow breathing plus fast flicker, different for every tongue.
                    double flick = 0.62 + 0.24 * Math.Sin(_time * (3.4 + H(id, 1) * 3.2) + i * 2.3 + layer)
                                        + 0.14 * Math.Sin(_time * (9 + H(id, 2) * 7) + i * 1.1);
                    double fh = h * (pill ? 0.96 : 0.50) * (0.5 + 0.5 * H(id, 3)) * flick * power * shrink;
                    double fw = cell * (1.5 - layer * 0.12);
                    double cx = (i + 0.5 + (layer % 2) * 0.5) * cell + Math.Sin(_time * 1.6 + i * 1.7 + layer) * 3;
                    // The tip whips sideways and hooks over, like a brush stroke.
                    double lean = Math.Sin(_time * (2.2 + H(id, 4)) + i * 1.3 + layer * 2) * fw * 0.5 + fw * 0.18;
                    var tip = new Point(cx + lean, h - fh);

                    ctx.BeginFigure(new Point(cx - fw / 2, h + 2), true, true);
                    ctx.BezierTo(new Point(cx - fw * 0.75, h - fh * 0.38), new Point(cx - fw * 0.05, h - fh * 0.62), tip, layer >= 2, true);
                    ctx.BezierTo(new Point(cx + lean * 0.55 + fw * 0.12, h - fh * 0.80), new Point(cx + fw * 0.78, h - fh * 0.34), new Point(cx + fw / 2, h + 2), false, true);
                }
            }
            g.Freeze();
            // Middle layers get a thin bright edge so each tongue reads as a shape, not a blur.
            dc.DrawGeometry(Flame[layer], layer is 2 or 3 ? Stroke(FlameLine, pill ? 0.6 : 0.9) : null, g);
        }

        // Sparks spiralling up out of the fire.
        int embers = pill ? 6 : 22;
        for (int i = 0; i < embers; i++)
        {
            double life = (_time * (0.2 + H(i, 21) * 0.34) + H(i, 22)) % 1;
            double x = H(i, 23) * w + Math.Sin(_time * 2.4 + i * 1.4 + life * 6) * (6 + life * 10);
            double y = h - life * h * (pill ? 1.0 : 0.9);
            double r = (0.6 + H(i, 24) * 1.2) * (1 - life * 0.5);
            dc.PushOpacity((1 - life) * 0.9);
            dc.DrawEllipse(EmberBrush, null, new Point(x, y), r, r);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ thunder

    private static readonly Brush BoltCore = Solid(255, 255, 250, 205);
    private static readonly Brush BoltGlow = Solid(90, 255, 214, 40);
    private static readonly Brush FlashBrush = Solid(255, 255, 236, 150);

    private void Thunder(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double period = 1.9 - _intensity * 0.7;
        double cycle = _time / period;
        int strike = (int)Math.Floor(cycle);
        double p = cycle - strike;

        // Always-on crackle: short arcs skittering along the bottom edge.
        var arcPen = Stroke(BoltGlow, pill ? 0.9 : 1.2);
        int arcs = pill ? 3 : 7;
        for (int i = 0; i < arcs; i++)
        {
            int frame = (int)(_time * 14) + i * 17;
            if (H(frame, 61) > 0.55) continue;
            double x = H(frame, 62) * w, y = h - H(frame, 63) * h * 0.3;
            var a = new StreamGeometry();
            using (var ctx = a.Open())
            {
                ctx.BeginFigure(new Point(x, y), false, false);
                for (int s = 1; s <= 4; s++)
                    ctx.LineTo(new Point(x + s * (pill ? 4 : 7), y + (H(frame + s, 64) - 0.5) * (pill ? 7 : 14)), true, true);
            }
            a.Freeze();
            dc.DrawGeometry(null, arcPen, a);
        }

        if (p > 0.26) return;
        // A strike flickers twice, then fades.
        bool lit = p < 0.07 || (p > 0.11 && p < 0.26);
        double fade = 1 - p / 0.26;
        dc.PushOpacity(0.16 * fade);
        dc.DrawRectangle(FlashBrush, null, new Rect(0, 0, w, h));
        dc.Pop();
        if (!lit) return;

        int bolts = pill ? 1 : 2;
        for (int b = 0; b < bolts; b++)
        {
            int id = strike * 13 + b * 101;
            double x = w * (0.12 + 0.76 * H(id, 51));
            var bolt = new StreamGeometry();
            using (var ctx = bolt.Open())
            {
                int segs = pill ? 5 : 9;
                double px = x, py = -2;
                ctx.BeginFigure(new Point(px, py), false, false);
                for (int s = 1; s <= segs; s++)
                {
                    px += (H(id + s, 52) - 0.5) * w * 0.13;
                    py = h * s / segs;
                    ctx.LineTo(new Point(px, py), true, true);
                    // Side branch
                    if (!pill && s is 3 or 6)
                    {
                        double bx = px + (H(id + s, 53) - 0.5) * w * 0.22, by = py + h * 0.16;
                        ctx.LineTo(new Point(bx, by), true, true);
                        ctx.LineTo(new Point(px, py), false, false);
                    }
                }
            }
            bolt.Freeze();
            dc.PushOpacity(fade);
            dc.DrawGeometry(null, Stroke(BoltGlow, pill ? 4 : 7), bolt);
            dc.DrawGeometry(null, Stroke(BoltCore, pill ? 1.2 : 1.9), bolt);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ rain

    private static readonly Brush RainBrush = Solid(255, 190, 222, 255);

    private void Rain(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        var pen = Stroke(RainBrush, pill ? 0.9 : 1.2);
        int n = Math.Clamp((int)(w * h / 900), 10, 90);
        double fall = 1.5 + _intensity * 1.2;
        for (int i = 0; i < n; i++)
        {
            double life = (_time * fall * (0.7 + H(i, 71) * 0.6) + H(i, 72)) % 1;
            double len = (pill ? 5 : 9) + H(i, 73) * 7;
            double x = H(i, 74) * (w + 30) - 15 - life * 14, y = life * (h + len) - len;
            dc.PushOpacity(0.18 + 0.4 * H(i, 75));
            dc.DrawLine(pen, new Point(x, y), new Point(x - len * 0.22, y + len));
            dc.Pop();

            // Splash ring where it lands.
            if (life > 0.9 && !pill)
            {
                double s = (life - 0.9) / 0.1;
                dc.PushOpacity((1 - s) * 0.5);
                dc.DrawEllipse(null, pen, new Point(x - 3, h - 2), 2 + s * 6, 0.8 + s * 1.6);
                dc.Pop();
            }
        }
    }

    // ------------------------------------------------------------------ snow

    private static readonly Brush SnowBrush = Solid(255, 255, 255, 255);

    private void Snow(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        // A soft drift settled along the bottom.
        var drift = new StreamGeometry();
        using (var ctx = drift.Open())
        {
            ctx.BeginFigure(new Point(0, h), true, true);
            for (double x = 0; x <= w + 8; x += 8)
                ctx.LineTo(new Point(x, h - (pill ? 3 : 8) - Math.Sin(x * 0.03 + 1) * (pill ? 1.5 : 4) - Math.Sin(x * 0.011) * 3), false, true);
            ctx.LineTo(new Point(w, h), false, false);
        }
        drift.Freeze();
        dc.PushOpacity(0.22);
        dc.DrawGeometry(SnowBrush, null, drift);
        dc.Pop();

        int n = Math.Clamp((int)(w * h / 700), 10, 110);
        for (int i = 0; i < n; i++)
        {
            double depth = H(i, 81);                                   // near flakes are bigger and faster
            double life = (_time * (0.05 + depth * 0.11) * (1 + _intensity) + H(i, 82)) % 1;
            double x = H(i, 83) * w + Math.Sin(_time * (0.5 + H(i, 84)) + i) * (5 + depth * 9);
            double r = (pill ? 0.6 : 0.8) + depth * (pill ? 1.0 : 1.9);
            dc.PushOpacity(0.25 + depth * 0.65);
            dc.DrawEllipse(SnowBrush, null, new Point(x, life * (h + 8) - 4), r, r);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ sakura

    private static readonly Brush[] Petal = { Solid(235, 255, 190, 210), Solid(235, 255, 150, 182), Solid(235, 255, 222, 232) };

    private void Sakura(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        int n = Math.Clamp((int)(w * h / 1500), 6, 60);
        double breeze = 1 + _intensity * 1.2;
        for (int i = 0; i < n; i++)
        {
            double life = (_time * (0.06 + H(i, 91) * 0.09) * breeze + H(i, 92)) % 1;
            double span = w + 40;
            double x = (H(i, 93) * span + _time * (10 + H(i, 94) * 18) * breeze + Math.Sin(_time * 1.3 + i) * 12) % span - 20;
            double y = life * (h + 12) - 6 + Math.Sin(_time * 2 + i * 0.7) * 3;
            double size = (pill ? 2.2 : 3.4) * (0.7 + H(i, 95) * 0.7);
            // Petals tumble: spin plus a flip that squashes them edge-on.
            double flip = 0.35 + 0.65 * Math.Abs(Math.Sin(_time * (1.2 + H(i, 96) * 1.6) + i));
            dc.PushTransform(new RotateTransform(_time * (40 + H(i, 97) * 90) + H(i, 98) * 360, x, y));
            dc.PushOpacity(0.55 + 0.4 * H(i, 99));
            dc.DrawEllipse(Petal[i % 3], null, new Point(x, y), size, size * 0.55 * flip);
            dc.Pop();
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ bubbles

    private static readonly Brush BubbleEdge = Solid(170, 190, 235, 255);
    private static readonly Brush BubbleFill = Solid(26, 150, 215, 255);

    private void Bubbles(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        var pen = Stroke(BubbleEdge, pill ? 0.8 : 1.1);
        int n = Math.Clamp((int)(w * h / 2600), 5, 34);
        for (int i = 0; i < n; i++)
        {
            double life = (_time * (0.07 + H(i, 101) * 0.12) * (1 + _intensity) + H(i, 102)) % 1;
            double r = ((pill ? 1.6 : 2.5) + H(i, 103) * (pill ? 2.5 : 7)) * (0.7 + life * 0.5);
            double x = H(i, 104) * w + Math.Sin(_time * (0.9 + H(i, 105)) + i) * 8;
            double y = h + r - life * (h + r * 2);
            dc.PushOpacity(Math.Sin(life * Math.PI) * 0.9);
            dc.DrawEllipse(BubbleFill, pen, new Point(x, y), r, r);
            dc.DrawEllipse(SnowBrush, null, new Point(x - r * 0.35, y - r * 0.35), r * 0.18, r * 0.18); // highlight
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ aurora

    private void Aurora(DrawingContext dc, double w, double h)
    {
        if (_aurora[0] == null)
        {
            Color[] colors = { _accent, Color.FromRgb(45, 212, 191), Color.FromRgb(139, 92, 246) };
            for (int i = 0; i < 3; i++)
            {
                var c = colors[i];
                var b = new RadialGradientBrush(Color.FromArgb(96, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
                b.Freeze();
                _aurora[i] = b;
            }
        }
        double flow = 0.5 + _intensity * 0.5;
        // Tall curtains of light swaying across the island.
        for (int i = 0; i < 6; i++)
        {
            double cx = w * (0.5 + 0.48 * Math.Sin(_time * (0.19 + i * 0.045) * (1 + flow) + i * 1.7));
            double cy = h * (0.45 + 0.25 * Math.Cos(_time * (0.15 + i * 0.03) + i * 1.3));
            dc.DrawEllipse(_aurora[i % 3], null, new Point(cx, cy), w * (0.16 + 0.05 * (i % 3)), Math.Max(h * 0.95, 34));
        }
    }

    // ------------------------------------------------------------------ stars

    private void Stars(DrawingContext dc, double w, double h)
    {
        var brush = new SolidColorBrush(_accent);
        int n = Math.Clamp((int)(w * h / 820), 8, 70);
        for (int i = 0; i < n; i++)
        {
            double tw = Math.Sin(_time * (0.6 + H(i, 31) * 1.7) + H(i, 32) * 6.283) * 0.5 + 0.5;
            double r = 0.5 + H(i, 33) * 0.95;
            dc.PushOpacity(0.12 + 0.72 * tw * tw);
            dc.DrawEllipse(brush, null, new Point(H(i, 34) * w, H(i, 35) * h), r, r);
            dc.Pop();
        }

        // A shooting star every few seconds.
        double cycle = _time / 5.5, p = cycle - Math.Floor(cycle);
        if (p < 0.14 && h > 30)
        {
            int k = (int)Math.Floor(cycle);
            double t = p / 0.14;
            double x0 = H(k, 41) * w * 0.6, y0 = H(k, 42) * h * 0.4;
            double x = x0 + t * w * 0.5, y = y0 + t * h * 0.42;
            dc.PushOpacity(Math.Sin(t * Math.PI) * 0.9);
            dc.DrawLine(Stroke(brush, 1.2), new Point(x - w * 0.07, y - h * 0.06), new Point(x, y));
            dc.Pop();
        }
    }
}
