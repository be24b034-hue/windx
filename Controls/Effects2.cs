using System.Windows;
using System.Windows.Media;

namespace WindX.Controls;

/// <summary>Second set of backdrops. All of these take their colours from the theme's accent.</summary>
public sealed partial class EffectLayer
{
    private Color Mix(Color to, double t) => Color.FromRgb(
        (byte)(_accent.R + (to.R - _accent.R) * t), (byte)(_accent.G + (to.G - _accent.G) * t), (byte)(_accent.B + (to.B - _accent.B) * t));

    private Brush Tone(double lighten, byte alpha = 255)
    {
        var c = lighten >= 0 ? Mix(Colors.White, lighten) : Mix(Colors.Black, -lighten);
        return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
    }

    private void More(DrawingContext dc, double w, double h)
    {
        switch (_effect)
        {
            case IslandEffect.Race: Race(dc, w, h); break;
            case IslandEffect.Cats: Cats(dc, w, h); break;
            case IslandEffect.Fireflies: Fireflies(dc, w, h); break;
            case IslandEffect.Matrix: dc.PushOpacity(0.55); Matrix(dc, w, h); dc.Pop(); break;
            case IslandEffect.Hearts: Hearts(dc, w, h); break;
            case IslandEffect.Confetti: Confetti(dc, w, h); break;
            case IslandEffect.Meteors: Meteors(dc, w, h); break;
            case IslandEffect.Leaves: Leaves(dc, w, h); break;
            case IslandEffect.Ripples: Ripples(dc, w, h); break;
            case IslandEffect.Synthwave: Synthwave(dc, w, h); break;
            case IslandEffect.Clouds: Clouds(dc, w, h); break;
            case IslandEffect.Pulse: Pulse(dc, w, h); break;
            default: Extra(dc, w, h); break;
        }
    }

    // ------------------------------------------------------------------ race / grid: F1 cars

    // Side view of a modern F1 car on a 48 x 9 grid (nose to the right).
    private static readonly Geometry CarBody = Geometry.Parse("M46.5,7.9 L38,6.4 L30,5.4 L26.4,5.3 L25,3.3 L21,2.2 L17.2,2.2 L12.5,4.2 L5.5,5.2 L2.4,5.6 L2.4,8.7 L46.5,8.7 Z");
    private static readonly Geometry CarSidepod = Geometry.Parse("M12.5,6.1 L26.5,6.1 L30.5,7.7 L11.5,7.7 Z");
    private static readonly Geometry CarFloor = Geometry.Parse("M9,8.7 L40,8.7 L40,9.5 L9,9.5 Z");
    private static readonly Geometry CarHalo = Geometry.Parse("M27.2,5.2 Q25.4,3.3 22.2,3.5");
    private static readonly Brush Tyre = Solid(255, 16, 16, 18);
    private static readonly Brush Carbon = Solid(255, 28, 28, 32);

    private static SolidColorBrush Rgb(int hex) => new(Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex));

    // Ten colour schemes in the spirit of the current grid (body, stripe, detail). Colours only: no logos or sponsors.
    private static readonly (int Body, int Stripe, int Detail)[] Liveries =
    {
        (0x1B2A5E, 0xE1061C, 0xFFC906), (0xDC0000, 0xFFF200, 0xFFFFFF), (0x1A1A1C, 0x00D2BE, 0xC8CCCE), (0xFF8000, 0x1E1E1E, 0x47C7FC),
        (0x00594F, 0xCEDC00, 0xFFFFFF), (0x0F4FB5, 0xFF87BC, 0xFFFFFF), (0x00205B, 0x64C4FF, 0xFFFFFF), (0xF1F3F4, 0x1534CC, 0xE1061C),
        (0x0C0C0E, 0x52E252, 0xFFFFFF), (0xE6E6E6, 0xE6002B, 0x111111),
    };

    // ------------------------------------------------------------------ cats strolling along the bottom

    private void Cats(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double s = pill ? 0.8 : 1.5, ground = h - (pill ? 2 : 6);
        int cats = pill ? 1 : 2;
        for (int i = 0; i < cats; i++)
        {
            bool left = i % 2 == 1;
            double speed = 26 + i * 9, span = w + 80 * s;
            double x = (_time * speed + i * span * 0.55) % span - 40 * s;
            if (left) x = w - x;
            Brush fur = Tone(i == 0 ? 0 : 0.35);
            var limb = Stroke(fur, 1.7);
            double step = _time * (7 + i);

            dc.PushTransform(new TranslateTransform(x, ground));
            dc.PushTransform(new ScaleTransform(left ? -s : s, s));
            // Legs swing in pairs.
            for (int leg = 0; leg < 4; leg++)
            {
                double lx = leg < 2 ? -6 + leg * 2.4 : 5 + (leg - 2) * 2.4;
                double swing = Math.Sin(step + (leg % 2) * Math.PI + (leg < 2 ? 0 : 1.2)) * 2.6;
                dc.DrawLine(limb, new Point(lx, -5.5), new Point(lx + swing, 0));
            }
            dc.DrawEllipse(fur, null, new Point(0, -8), 9, 4.2);                                     // body
            dc.DrawEllipse(fur, null, new Point(10.5, -12.5 + Math.Sin(step * 2) * 0.3), 3.9, 3.5);   // head
            var ears = new StreamGeometry();
            using (var ctx = ears.Open())
            {
                ctx.BeginFigure(new Point(7.6, -14.5), true, true); ctx.LineTo(new Point(8.4, -19), true, false); ctx.LineTo(new Point(10.6, -15.6), true, false);
                ctx.BeginFigure(new Point(10.8, -15.6), true, true); ctx.LineTo(new Point(13.2, -19), true, false); ctx.LineTo(new Point(13.8, -14.2), true, false);
            }
            dc.DrawGeometry(fur, null, ears);
            // Tail curls and flicks.
            var tail = new StreamGeometry();
            using (var ctx = tail.Open())
            {
                double flick = Math.Sin(_time * 2.4 + i) * 4;
                ctx.BeginFigure(new Point(-8.5, -9), false, false);
                ctx.BezierTo(new Point(-14, -10), new Point(-15 + flick * 0.4, -17), new Point(-11 + flick, -20), true, true);
            }
            dc.DrawGeometry(null, Stroke(fur, 2), tail);
            dc.DrawEllipse(Tone(0.9), null, new Point(11.8, -13), 0.6, 0.6);                          // eye
            dc.Pop();
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ fireflies

    private void Fireflies(DrawingContext dc, double w, double h)
    {
        var glow = new RadialGradientBrush(Color.FromArgb(150, _accent.R, _accent.G, _accent.B), Color.FromArgb(0, _accent.R, _accent.G, _accent.B));
        var core = Tone(0.6);
        int n = Math.Clamp((int)(w * h / 2400), 5, 30);
        for (int i = 0; i < n; i++)
        {
            double x = w * (0.5 + 0.48 * Math.Sin(_time * (0.13 + H(i, 1) * 0.2) + H(i, 2) * 6.28));
            double y = h * (0.5 + 0.44 * Math.Cos(_time * (0.17 + H(i, 3) * 0.22) + H(i, 4) * 6.28));
            double blink = Math.Pow(Math.Sin(_time * (0.8 + H(i, 5) * 1.4) + i) * 0.5 + 0.5, 2) * (0.6 + _intensity * 0.6);
            dc.PushOpacity(Math.Min(1, 0.15 + blink));
            dc.DrawEllipse(glow, null, new Point(x, y), 7, 7);
            dc.DrawEllipse(core, null, new Point(x, y), 1.1, 1.1);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ matrix rain

    private void Matrix(DrawingContext dc, double w, double h)
    {
        Brush lead = Tone(0.7), trail = Tone(0);
        const double cell = 6.5;
        int cols = (int)(w / 9);
        for (int c = 0; c < cols; c++)
        {
            double head = (_time * (5 + H(c, 1) * 9) * (1 + _intensity * 0.6) + H(c, 2) * 40) % (h / cell + 14);
            for (int k = 0; k < 11; k++)
            {
                double y = (Math.Floor(head) - k) * cell;
                if (y < -cell || y > h) continue;
                // Glyphs are suggested by little blocks of different widths.
                double gw = 2 + H(c * 31 + (int)(head - k), 3) * 3;
                dc.PushOpacity(k == 0 ? 0.95 : 0.6 * (1 - k / 11.0));
                dc.DrawRectangle(k == 0 ? lead : trail, null, new Rect(c * 9 + 2, y, gw, cell - 2));
                dc.Pop();
            }
        }
    }

    // ------------------------------------------------------------------ hearts

    private static readonly Geometry HeartShape = Geometry.Parse("M0,-1.2 C-1.2,-3.4 -4.6,-2.6 -4.6,0.2 C-4.6,2.6 -1.6,4.2 0,5.6 C1.6,4.2 4.6,2.6 4.6,0.2 C4.6,-2.6 1.2,-3.4 0,-1.2 Z");

    private void Hearts(DrawingContext dc, double w, double h)
    {
        int n = Math.Clamp((int)(w * h / 3000), 4, 26);
        for (int i = 0; i < n; i++)
        {
            double life = (_time * (0.07 + H(i, 1) * 0.1) * (1 + _intensity) + H(i, 2)) % 1;
            double x = H(i, 3) * w + Math.Sin(_time * 1.4 + i) * 7, y = h + 6 - life * (h + 14);
            double s = (0.6 + H(i, 4) * 1.1) * (1 + 0.12 * Math.Sin(_time * 6 + i));   // heartbeat
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new ScaleTransform(s, s));
            dc.PushOpacity(Math.Sin(life * Math.PI) * 0.85);
            dc.DrawGeometry(Tone(H(i, 5) * 0.5), null, HeartShape);
            dc.Pop(); dc.Pop(); dc.Pop();
        }
    }

    // ------------------------------------------------------------------ confetti

    private void Confetti(DrawingContext dc, double w, double h)
    {
        Brush[] paint = { Tone(0), Tone(0.5), Tone(0.9), Tone(-0.35) };
        int n = Math.Clamp((int)(w * h / 1300), 8, 70);
        for (int i = 0; i < n; i++)
        {
            double life = (_time * (0.1 + H(i, 1) * 0.16) * (1 + _intensity) + H(i, 2)) % 1;
            double x = H(i, 3) * w + Math.Sin(_time * 2 + i) * 9, y = life * (h + 10) - 5;
            double flip = Math.Abs(Math.Sin(_time * (3 + H(i, 4) * 4) + i));
            dc.PushTransform(new RotateTransform(_time * (90 + H(i, 5) * 200) + i * 40, x, y));
            dc.PushOpacity(0.85);
            dc.DrawRectangle(paint[i % 4], null, new Rect(x - 2.4, y - 1.2 * flip, 4.8, 2.4 * flip + 0.4));
            dc.Pop(); dc.Pop();
        }
    }

    // ------------------------------------------------------------------ meteors

    private void Meteors(DrawingContext dc, double w, double h)
    {
        var tail = new LinearGradientBrush(Color.FromArgb(0, _accent.R, _accent.G, _accent.B), Color.FromArgb(255, _accent.R, _accent.G, _accent.B), new Point(0, 0), new Point(1, 0));
        Brush head = Tone(0.8);
        int n = Math.Clamp((int)(w / 60), 3, 9);
        for (int i = 0; i < n; i++)
        {
            double life = (_time * (0.25 + H(i, 1) * 0.35) * (1 + _intensity * 0.6) + H(i, 2)) % 1;
            double len = 30 + H(i, 3) * 50;
            double x = -len + life * (w + len * 2) + H(i, 4) * 40 - 20, y = H(i, 5) * h * 0.7 + life * h * 0.45;
            dc.PushTransform(new RotateTransform(22, x, y));
            dc.PushOpacity(Math.Sin(life * Math.PI));
            dc.DrawRoundedRectangle(tail, null, new Rect(x - len, y - 0.8, len, 1.6), 0.8, 0.8);
            dc.DrawEllipse(head, null, new Point(x, y), 1.7, 1.7);
            dc.Pop(); dc.Pop();
        }
    }

    // ------------------------------------------------------------------ leaves

    private static readonly Geometry LeafShape = Geometry.Parse("M-5,0 C-2,-3.4 2,-3.4 5,0 C2,3.4 -2,3.4 -5,0 Z M-5,0 L5,0");

    private void Leaves(DrawingContext dc, double w, double h)
    {
        int n = Math.Clamp((int)(w * h / 2200), 5, 36);
        for (int i = 0; i < n; i++)
        {
            double life = (_time * (0.06 + H(i, 1) * 0.08) * (1 + _intensity) + H(i, 2)) % 1;
            double x = (H(i, 3) * (w + 40) + _time * (6 + H(i, 4) * 12) + Math.Sin(_time * 1.2 + i) * 14) % (w + 40) - 20;
            double y = life * (h + 14) - 7, s = 0.7 + H(i, 5) * 0.8;
            Brush fill = Tone(H(i, 6) * 0.6 - 0.25);
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(Math.Sin(_time * (1 + H(i, 7)) + i) * 70 + i * 50));
            dc.PushTransform(new ScaleTransform(s, s * (0.4 + 0.6 * Math.Abs(Math.Cos(_time * 1.7 + i)))));
            dc.PushOpacity(0.8);
            dc.DrawGeometry(fill, new Pen(Tone(-0.5, 160), 0.5), LeafShape);
            dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
        }
    }

    // ------------------------------------------------------------------ ripples

    private void Ripples(DrawingContext dc, double w, double h)
    {
        var ring = Stroke(Tone(0.2), 1.2);
        int n = Math.Clamp((int)(w / 70), 3, 8);
        for (int i = 0; i < n; i++)
        {
            double cycle = _time * (0.28 + H(i, 1) * 0.2) * (1 + _intensity * 0.7) + H(i, 2);
            int drop = (int)Math.Floor(cycle);
            double life = cycle - drop;
            double x = H(i * 17 + drop, 3) * w, y = h * (0.35 + 0.6 * H(i * 17 + drop, 4));
            for (int k = 0; k < 3; k++)
            {
                double p = life - k * 0.16;
                if (p <= 0) continue;
                dc.PushOpacity((1 - p) * 0.6);
                dc.DrawEllipse(null, ring, new Point(x, y), p * 46, p * 15);
                dc.Pop();
            }
        }
    }

    // ------------------------------------------------------------------ synthwave grid

    private void Synthwave(DrawingContext dc, double w, double h)
    {
        var line = Stroke(Tone(0.1, 190), 1);
        double horizon = h * (h < 60 ? 0.42 : 0.58);
        // Sun on the horizon.
        var sun = new LinearGradientBrush(Color.FromArgb(210, _accent.R, _accent.G, _accent.B), Color.FromArgb(40, _accent.R, _accent.G, _accent.B), 90);
        double r = Math.Min(h * 0.3, 40);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, horizon)));
        dc.DrawEllipse(sun, null, new Point(w / 2, horizon), r, r);
        dc.Pop();
        dc.DrawLine(line, new Point(0, horizon), new Point(w, horizon));
        // Floor: lines rushing toward you, and rays fanning out from the vanishing point.
        for (int i = 0; i < 9; i++)
        {
            double p = (i + _time * (0.7 + _intensity * 0.8) % 1) / 9;
            double y = horizon + (h - horizon) * p * p;
            dc.PushOpacity(0.25 + 0.6 * p);
            dc.DrawLine(line, new Point(0, y), new Point(w, y));
            dc.Pop();
        }
        dc.PushOpacity(0.45);
        for (int i = -9; i <= 9; i++)
            dc.DrawLine(line, new Point(w / 2 + i * 6, horizon), new Point(w / 2 + i * w * 0.14, h));
        dc.Pop();
    }

    // ------------------------------------------------------------------ clouds

    private void Clouds(DrawingContext dc, double w, double h)
    {
        var puff = new RadialGradientBrush(Color.FromArgb(70, _accent.R, _accent.G, _accent.B), Color.FromArgb(0, _accent.R, _accent.G, _accent.B));
        int n = Math.Clamp((int)(w / 55), 3, 9);
        for (int i = 0; i < n; i++)
        {
            double span = w + 220, size = 34 + H(i, 1) * 50;
            double x = (_time * (5 + H(i, 2) * 9) * (1 + _intensity) + H(i, 3) * span) % span - 110, y = h * (0.2 + 0.65 * H(i, 4));
            for (int k = 0; k < 4; k++)
                dc.DrawEllipse(puff, null, new Point(x + (k - 1.5) * size * 0.42, y + Math.Sin(k * 2.1 + i) * size * 0.12), size * (0.55 + 0.2 * H(i + k, 5)), size * 0.34);
        }
    }

    // ------------------------------------------------------------------ pulse: an equaliser skyline

    private void Pulse(DrawingContext dc, double w, double h)
    {
        var fill = new LinearGradientBrush(Color.FromArgb(40, _accent.R, _accent.G, _accent.B), Color.FromArgb(200, _accent.R, _accent.G, _accent.B), 90);
        const double bar = 5, gap = 3;
        int n = (int)(w / (bar + gap));
        double energy = 0.35 + _intensity * 0.9;
        for (int i = 0; i < n; i++)
        {
            double v = 0.5 + 0.3 * Math.Sin(_time * (2.2 + H(i, 1) * 3) + i * 0.6) + 0.2 * Math.Sin(_time * (5 + H(i, 2) * 4) + i);
            double bh = Math.Max(2, h * (h < 60 ? 0.8 : 0.42) * v * energy);
            dc.DrawRoundedRectangle(fill, null, new Rect(i * (bar + gap) + 2, h - bh, bar, bh), 2, 2);
        }
    }
}
