using System.Windows;
using System.Windows.Media;

namespace WindX.Controls;

/// <summary>
/// The power pack: original anime-style energy effects. No characters, just the moments that make
/// those shows look incredible: colliding energy, breathing-style sword arcs, a spiralling sphere,
/// a gathering of light, a domain opening, black lightning, a charged beam, a crimson moon.
/// Every one loops on its own cycle and hits harder when music plays.
/// </summary>
public sealed partial class EffectLayer
{
    // Effects redraw up to 30 times a second; building brushes each time keeps the garbage collector busy.
    // Alpha is rounded to 1/32 steps, so a handful of cached brushes cover every fade.
    private static readonly Dictionary<(uint, int, int), Brush> RadialCache = new();
    private static readonly Dictionary<(uint, int, int), Pen> PenCache = new();

    private static uint Key(Color c) => (uint)(c.R << 16 | c.G << 8 | c.B);

    private static Brush Radial(Color c, double alpha, double hold = 0)
    {
        int a = (int)Math.Round(Math.Clamp(alpha, 0, 1) * 32), hk = (int)Math.Round(hold * 20);
        var key = (Key(c), a, hk);
        if (RadialCache.TryGetValue(key, out var hit)) return hit;
        if (RadialCache.Count > 600) RadialCache.Clear();
        var made = MakeRadial(c, a / 32.0, hk / 20.0);
        RadialCache[key] = made;
        return made;
    }

    private static Brush MakeRadial(Color c, double alpha, double hold)
    {
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * Math.Clamp(alpha, 0, 1)), c.R, c.G, c.B), 0));
        if (hold > 0) b.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * Math.Clamp(alpha, 0, 1)), c.R, c.G, c.B), hold));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
        b.Freeze();
        return b;
    }

    private static void Glow(DrawingContext dc, Point p, double r, Color c, double alpha, double hold = 0)
    {
        if (r > 0.5 && alpha > 0.01) dc.DrawEllipse(Radial(c, alpha, hold), null, p, r, r);
    }

    private static Pen Line(Color c, double alpha, double thickness)
    {
        int a = (int)Math.Round(Math.Clamp(alpha, 0, 1) * 32), tk = (int)Math.Round(thickness * 4);
        var key = (Key(c), a, tk);
        if (PenCache.TryGetValue(key, out var hit)) return hit;
        if (PenCache.Count > 800) PenCache.Clear();
        var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * a / 32), c.R, c.G, c.B)), tk / 4.0)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        PenCache[key] = pen;
        return pen;
    }

    private static double Ease(double x) => 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 3);
    private static double Band(double p, double a, double b) => Math.Clamp((p - a) / (b - a), 0, 1);

    /// <summary>A jagged lightning path from a to b.</summary>
    private static StreamGeometry Bolt(Point a, Point b, int segs, double jitter, int seed, bool branches = false)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(a, false, false);
            var dx = b.X - a.X; var dy = b.Y - a.Y; var len = Math.Sqrt(dx * dx + dy * dy) + 0.001;
            var nx = -dy / len; var ny = dx / len;
            Point prev = a;
            for (int s = 1; s <= segs; s++)
            {
                double f = s / (double)segs, off = s == segs ? 0 : (H(seed + s, 7) - 0.5) * 2 * jitter;
                var p = new Point(a.X + dx * f + nx * off, a.Y + dy * f + ny * off);
                ctx.LineTo(p, true, true);
                if (branches && s % 3 == 1 && s < segs - 1)
                {
                    var bp = new Point(p.X + (dx / segs) * 1.6 + nx * jitter * (H(seed + s, 9) - 0.3) * 3, p.Y + (dy / segs) * 1.6 + ny * jitter * (H(seed + s, 10) - 0.3) * 3);
                    ctx.LineTo(bp, true, true);
                    ctx.LineTo(p, false, false);
                }
                prev = p;
            }
        }
        return g;
    }

    private void Powers(DrawingContext dc, double w, double h)
    {
        switch (_effect)
        {
            case IslandEffect.Infinity: Infinity(dc, w, h); break;
            case IslandEffect.WaterBlade: Blade(dc, w, h, 0); break;
            case IslandEffect.FlameBlade: Blade(dc, w, h, 1); break;
            case IslandEffect.ThunderStep: ThunderStep(dc, w, h); break;
            case IslandEffect.Spiral: Spiral(dc, w, h); break;
            case IslandEffect.SpiritOrb: SpiritOrb(dc, w, h); break;
            case IslandEffect.Domain: Domain(dc, w, h); break;
            case IslandEffect.BlackFlash: BlackFlash(dc, w, h); break;
            case IslandEffect.Beam: Beam(dc, w, h); break;
            case IslandEffect.CrimsonMoon: CrimsonMoon(dc, w, h); break;
            case IslandEffect.Glass: Glass(dc, w, h); break;
        }
    }

    // ------------------------------------------------------------------ infinity: blue and red collide into purple

    private static readonly Color Azure = Color.FromRgb(64, 156, 255), Scarlet = Color.FromRgb(255, 48, 64), Violet = Color.FromRgb(168, 72, 255);

    private void Infinity(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double cycle = 5.5 - _intensity * 1.5, p = (_time % cycle) / cycle, unit = Math.Min(w * 0.5, h);
        var c = new Point(w * 0.5, h * 0.5);
        double orb = unit * (pill ? 0.3 : 0.12);

        // Space bends toward the centre: faint rings being pulled in the whole time.
        for (int k = 0; k < 4; k++)
        {
            double f = 1 - ((_time * 0.35 + k * 0.25) % 1);
            dc.DrawEllipse(null, Line(Violet, 0.18 * (1 - f), 1), c, w * 0.55 * f, h * 0.9 * f);
        }

        if (p < 0.62)
        {
            // Two orbs spiral in, each dragging a comet trail.
            double q = p / 0.62, radius = (w * 0.34) * (1 - Ease(q) * 0.92), spin = _time * (2.4 + q * 6);
            foreach (var (col, side) in new[] { (Azure, 0.0), (Scarlet, Math.PI) })
            {
                for (int k = 10; k >= 0; k--)
                {
                    double a = spin + side - k * 0.07, rr = radius * (1 + k * 0.012);
                    var tp = new Point(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr * (h / w) * 1.6);
                    Glow(dc, tp, orb * (1 - k * 0.07), col, 0.35 * (1 - k / 11.0));
                }
                var op = new Point(c.X + Math.Cos(spin + side) * radius, c.Y + Math.Sin(spin + side) * radius * (h / w) * 1.6);
                Glow(dc, op, orb * 2.6, col, 0.35);
                Glow(dc, op, orb, Color.FromRgb(255, 255, 255), 0.95, 0.35);
                // Sparks thrown off the orb.
                for (int s = 0; s < 5; s++)
                {
                    double life = (_time * 3 + H(s, 1)) % 1, ang = H(s + (int)(_time * 3), 2) * 6.28;
                    dc.DrawEllipse(new SolidColorBrush(col), null, new Point(op.X + Math.Cos(ang) * orb * (1 + life * 2.5), op.Y + Math.Sin(ang) * orb * (1 + life * 2.5)), 1 - life * 0.6, 1 - life * 0.6);
                }
            }
        }
        else
        {
            // Collision: a white flash, then the purple sphere swells and a shockwave tears outward.
            double q = Band(p, 0.62, 1);
            double flash = Math.Max(0, 1 - q * 6);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(70 * flash), 236, 214, 255)), null, new Rect(0, 0, w, h));
            double grow = Ease(q * 1.6), fade = 1 - Band(q, 0.55, 1);
            Glow(dc, c, unit * (0.25 + grow * 1.2), Violet, 0.75 * fade);
            Glow(dc, c, unit * (0.12 + grow * 0.45), Color.FromRgb(236, 210, 255), 0.95 * fade, 0.4);
            for (int r = 0; r < 3; r++)
            {
                double s = Band(q, r * 0.08, 0.7 + r * 0.08);
                dc.DrawEllipse(null, Line(r == 1 ? Color.FromRgb(255, 255, 255) : Violet, (1 - s) * 0.9, (pill ? 1.4 : 3) * (1 - s) + 0.5), c, w * 0.7 * Ease(s), h * 1.2 * Ease(s));
            }
            // Debris streaking away from the impact.
            for (int s = 0; s < (pill ? 14 : 36); s++)
            {
                double ang = H(s, 3) * 6.283, d = Ease(q) * (w * 0.6) * (0.4 + H(s, 4) * 0.8);
                var a = new Point(c.X + Math.Cos(ang) * d * 0.7, c.Y + Math.Sin(ang) * d * 0.35);
                var b = new Point(c.X + Math.Cos(ang) * d, c.Y + Math.Sin(ang) * d * 0.5);
                dc.DrawLine(Line(s % 2 == 0 ? Violet : Color.FromRgb(255, 255, 255), fade * 0.8, 1.2), a, b);
            }
        }
    }

    // ------------------------------------------------------------------ breathing-style sword arcs: water and flame

    private void Blade(DrawingContext dc, double w, double h, int kind)
    {
        bool pill = h < 60;
        double cycle = 2.6 - _intensity * 0.8, t = _time / cycle, p = t - Math.Floor(t);
        int strike = (int)Math.Floor(t);
        bool flip = strike % 2 == 1;
        Color edge = kind == 0 ? Color.FromRgb(120, 210, 255) : Color.FromRgb(255, 120, 30);
        Color core = kind == 0 ? Color.FromRgb(235, 250, 255) : Color.FromRgb(255, 236, 160);
        Color deep = kind == 0 ? Color.FromRgb(20, 90, 200) : Color.FromRgb(210, 40, 10);

        // The arc: a crescent from one lower corner sweeping over to the other side.
        // A wide arc through the middle of the island, like a full swing seen from the side.
        double cx = w * 0.5, cy = h * (pill ? 2.6 : 1.12), R = h * (pill ? 2.15 : 0.82);
        double aMid = -Math.PI / 2, span = Math.Asin(Math.Min(1, w * 0.46 / R));
        double a0 = flip ? aMid + span : aMid - span, a1 = flip ? aMid - span : aMid + span;
        double head = Ease(Band(p, 0, 0.32)), tail = Ease(Band(p, 0.22, 0.75)), fade = 1 - Band(p, 0.6, 1);
        if (head <= tail) return;
        double thick = (pill ? 6 : 18) * (0.8 + _intensity * 0.5);

        // Between swings the element lingers: a current of water, or drifting embers.
        for (int s = 0; s < (pill ? 6 : 18); s++)
        {
            double life = (_time * (0.18 + H(s, 31) * 0.2) + H(s, 32)) % 1;
            var pt = new Point(H(s, 33) * w + Math.Sin(_time * 1.5 + s) * 8, kind == 0 ? h * (0.3 + 0.6 * H(s, 34)) + Math.Sin(_time * 2 + s) * 4 : h - life * h);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(150 * Math.Sin(life * Math.PI)), edge.R, edge.G, edge.B)), null, pt, 1.2, 1.2);
        }

        Point At(double f, double r) { double a = a0 + (a1 - a0) * f; return new Point(cx + Math.Cos(a) * r, cy + Math.Sin(a) * r); }
        var crescent = new StreamGeometry();
        using (var ctx = crescent.Open())
        {
            const int N = 40;
            ctx.BeginFigure(At(tail, R), true, true);
            for (int i = 1; i <= N; i++) { double f = tail + (head - tail) * i / N; ctx.LineTo(At(f, R + Math.Sin(Math.PI * (f - tail) / (head - tail + 1e-6)) * thick), true, true); }
            for (int i = N; i >= 0; i--) { double f = tail + (head - tail) * i / N; ctx.LineTo(At(f, R - Math.Sin(Math.PI * (f - tail) / (head - tail + 1e-6)) * thick * 0.25), true, true); }
        }
        var fill = new LinearGradientBrush { StartPoint = new Point(flip ? 1 : 0, 0.5), EndPoint = new Point(flip ? 0 : 1, 0.5) };
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(0, deep.R, deep.G, deep.B), 0));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(200 * fade), edge.R, edge.G, edge.B), 0.6));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * fade), core.R, core.G, core.B), 1));
        // Soft bloom under the stroke, then the stroke itself.
        dc.PushOpacity(0.45 * fade);
        dc.DrawGeometry(null, Line(edge, 0.5, thick * 1.6), crescent);
        dc.Pop();
        dc.DrawGeometry(fill, null, crescent);

        if (kind == 0)
        {
            // Water: a ribbon of current coiling around the blade's path, with foam and spray.
            var coil = new StreamGeometry();
            using (var ctx = coil.Open())
            {
                const int N = 60;
                for (int i = 0; i <= N; i++)
                {
                    double f = tail + (head - tail) * i / N, wave = Math.Sin(f * 26 - _time * 9) * thick * 0.75;
                    var pt = At(f, R + wave);
                    if (i == 0) ctx.BeginFigure(pt, false, false); else ctx.LineTo(pt, true, true);
                }
            }
            dc.DrawGeometry(null, Line(core, 0.85 * fade, pill ? 1.2 : 2.2), coil);
            for (int s = 0; s < (pill ? 8 : 26); s++)
            {
                double f = tail + (head - tail) * H(s, 5), life = (p * 3 + H(s, 6)) % 1;
                var pt = At(f, R + thick * (0.4 + life * 2.2) * (H(s, 7) > 0.5 ? 1 : -0.6));
                double r = (pill ? 0.9 : 1.8) * (1 - life * 0.6);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(230 * fade * (1 - life)), 240, 252, 255)), null, pt, r, r);
            }
        }
        else
        {
            // Flame: tongues licking off the outer edge and embers thrown along the swing.
            for (int s = 0; s < (pill ? 10 : 28); s++)
            {
                double f = tail + (head - tail) * H(s, 11);
                double len = thick * (0.8 + 1.6 * H(s, 12)) * (0.6 + 0.4 * Math.Sin(_time * 18 + s));
                var root = At(f, R + thick * 0.6); var tip = At(f + 0.02 * (flip ? -1 : 1), R + thick * 0.6 + len);
                var tongue = new StreamGeometry();
                using (var ctx = tongue.Open())
                {
                    var l = At(f - 0.012, R + thick * 0.4); var r = At(f + 0.012, R + thick * 0.4);
                    ctx.BeginFigure(l, true, true);
                    ctx.QuadraticBezierTo(root, tip, false, true);
                    ctx.QuadraticBezierTo(root, r, false, true);
                }
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb((byte)(200 * fade), 255, (byte)(120 + 100 * H(s, 13)), 30)), null, tongue);
            }
            for (int s = 0; s < (pill ? 8 : 24); s++)
            {
                double life = (p * 2.5 + H(s, 14)) % 1, f = tail + (head - tail) * H(s, 15);
                var pt = At(f, R + thick * (1 + life * 4)); pt.Y -= life * h * 0.2;
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(255 * fade * (1 - life)), 255, 214, 120)), null, pt, 1.3 * (1 - life * 0.5), 1.3 * (1 - life * 0.5));
            }
        }
        Glow(dc, At(head, R), thick * 2, core, 0.8 * fade);   // the blade's leading edge
    }

    // ------------------------------------------------------------------ thunder step: a flash-dash across the island

    private void ThunderStep(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double cycle = 2.4 - _intensity * 0.8, t = _time / cycle, p = t - Math.Floor(t);
        int n = (int)Math.Floor(t);
        Color gold = Color.FromRgb(255, 214, 40), white = Color.FromRgb(255, 252, 230);
        double y = h * (0.35 + 0.35 * H(n, 1)), dash = Ease(Band(p, 0.05, 0.22)), fade = 1 - Band(p, 0.3, 0.9);
        bool rtl = n % 2 == 1;
        double x0 = rtl ? w + 10 : -10, x1 = x0 + (rtl ? -1 : 1) * (w + 20) * dash;

        // Static crackle while charging.
        if (p < 0.08)
            for (int s = 0; s < 4; s++)
                dc.DrawGeometry(null, Line(gold, 0.7, 1), Bolt(new Point(x0, y), new Point(x0 + (rtl ? -1 : 1) * 30, y + (H(s + n * 5, 2) - 0.5) * 40), 5, 6, s + n * 31));
        if (dash <= 0) return;

        // Screen flash at the moment of the dash.
        double flash = Math.Max(0, 1 - Band(p, 0.05, 0.2) * 1.4);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(90 * flash), 255, 240, 170)), null, new Rect(0, 0, w, h));

        // The dash streak: a hot core band with a gold bloom.
        var band = new Rect(Math.Min(x0, x1), y - (pill ? 2 : 4), Math.Abs(x1 - x0), pill ? 4 : 8);
        var streak = new LinearGradientBrush { StartPoint = new Point(rtl ? 1 : 0, 0), EndPoint = new Point(rtl ? 0 : 1, 0) };
        streak.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 214, 40), 0));
        streak.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(230 * fade), 255, 230, 120), 0.85));
        streak.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * fade), 255, 255, 255), 1));
        dc.DrawRoundedRectangle(streak, null, band, band.Height / 2, band.Height / 2);
        // Lightning wrapping the path, branching off it.
        for (int b = 0; b < 3; b++)
        {
            var g = Bolt(new Point(x0, y + (b - 1) * 3), new Point(x1, y + (b - 1) * 3), 14, (pill ? 5 : 14) * (1 + _intensity), n * 97 + b * 13 + (int)(_time * 20), branches: !pill);
            dc.DrawGeometry(null, Line(gold, 0.35 * fade, pill ? 3 : 6), g);
            dc.DrawGeometry(null, Line(white, 0.95 * fade, pill ? 0.8 : 1.4), g);
        }
        Glow(dc, new Point(x1, y), pill ? 14 : 34, white, 0.9 * fade);
        // Sparks kicked up behind.
        for (int s = 0; s < (pill ? 6 : 18); s++)
        {
            double life = Band(p, 0.1, 0.9), fx = x0 + (x1 - x0) * H(s, 3);
            var pt = new Point(fx + (H(s, 4) - 0.5) * 20 * life, y + (H(s, 5) - 0.5) * h * 0.9 * life);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(255 * fade), 255, 236, 140)), null, pt, 1, 1);
        }
    }

    // ------------------------------------------------------------------ spiral: a sphere of compressed wind

    private void Spiral(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        var c = new Point(w * (pill ? 0.5 : 0.72), h * 0.5);
        double r = Math.Min(h * (pill ? 0.42 : 0.36), 70) * (1 + _intensity * 0.18 + 0.04 * Math.Sin(_time * 9));
        Color sky = Color.FromRgb(110, 196, 255), pale = Color.FromRgb(220, 244, 255);

        // Wind being dragged in from everywhere.
        for (int s = 0; s < (pill ? 12 : 40); s++)
        {
            double life = 1 - ((_time * (0.6 + H(s, 1) * 0.8) + H(s, 2)) % 1), ang = H(s, 3) * 6.283 + life * 2.5;
            double d = r * (1.1 + life * 3.2);
            var a = new Point(c.X + Math.Cos(ang) * d, c.Y + Math.Sin(ang) * d * 0.8);
            var b = new Point(c.X + Math.Cos(ang + 0.25) * (d - r * 0.5), c.Y + Math.Sin(ang + 0.25) * (d - r * 0.5) * 0.8);
            dc.DrawLine(Line(pale, 0.5 * (1 - life), 1), a, b);
        }
        Glow(dc, c, r * 2.6, sky, 0.35);
        Glow(dc, c, r * 1.25, sky, 0.75, 0.55);

        // Dozens of fast arcs whipping around inside the sphere, on different tilts.
        for (int k = 0; k < 14; k++)
        {
            double tilt = H(k, 4) * 180, spin = _time * (500 + H(k, 5) * 700) * (k % 2 == 0 ? 1 : -1), rr = r * (0.55 + 0.45 * H(k, 6));
            dc.PushTransform(new RotateTransform(tilt, c.X, c.Y));
            dc.PushTransform(new ScaleTransform(1, 0.35 + 0.6 * H(k, 7), c.X, c.Y));
            dc.PushTransform(new RotateTransform(spin, c.X, c.Y));
            var arc = new StreamGeometry();
            using (var ctx = arc.Open())
            {
                ctx.BeginFigure(new Point(c.X + rr, c.Y), false, false);
                ctx.ArcTo(new Point(c.X - rr * 0.5, c.Y + rr * 0.866), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true, true);
            }
            dc.DrawGeometry(null, Line(k % 3 == 0 ? Colors.White : pale, 0.85, pill ? 0.8 : 1.4), arc);
            dc.Pop(); dc.Pop(); dc.Pop();
        }
        Glow(dc, c, r * 0.55, Colors.White, 1, 0.45);   // the dense core

        // Orbit rings of wind blades around it.
        if (!pill)
            for (int k = 0; k < 2; k++)
            {
                dc.PushTransform(new RotateTransform(k == 0 ? -18 : 22, c.X, c.Y));
                dc.DrawEllipse(null, Line(pale, 0.45, 1.2), c, r * (1.7 + k * 0.4), r * (0.42 + k * 0.1));
                double a = _time * (6 + k * 2);
                Glow(dc, new Point(c.X + Math.Cos(a) * r * (1.7 + k * 0.4), c.Y + Math.Sin(a) * r * (0.42 + k * 0.1)), 6, Colors.White, 0.9);
                dc.Pop();
            }
    }

    // ------------------------------------------------------------------ spirit orb: light gathering into a giant sphere

    private void SpiritOrb(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double cycle = 7 - _intensity * 2, p = (_time % cycle) / cycle;
        var c = new Point(w * 0.5, pill ? h * 0.5 : h * 0.32);
        double grow = Ease(Band(p, 0, 0.85)), launch = Band(p, 0.85, 1);
        double r = Math.Min(w, h * 2) * (pill ? 0.05 : 0.04) + grow * Math.Min(w * 0.22, h * (pill ? 0.45 : 0.38));
        Color glow = Color.FromRgb(140, 210, 255);

        // Streams of light rising from the whole island toward the sphere.
        int n = pill ? 18 : 60;
        for (int s = 0; s < n; s++)
        {
            double life = (_time * (0.35 + H(s, 1) * 0.5) + H(s, 2)) % 1;
            var from = new Point(H(s, 3) * w, h + 4);
            var pt = new Point(from.X + (c.X - from.X) * Ease(life), from.Y + (c.Y - from.Y) * Ease(life) - Math.Sin(life * Math.PI) * h * 0.1);
            var prev = new Point(from.X + (c.X - from.X) * Ease(life - 0.06), from.Y + (c.Y - from.Y) * Ease(life - 0.06));
            dc.DrawLine(Line(Color.FromRgb(210, 240, 255), 0.7 * Math.Sin(life * Math.PI) * (1 - launch), 1.2), prev, pt);
        }

        double fade = 1 - launch;
        c.Y -= launch * h * 0.9;   // released: it rises away
        Glow(dc, c, r * 2.4, glow, 0.4 * fade);
        Glow(dc, c, r * 1.15, glow, 0.85 * fade, 0.6);
        Glow(dc, c, r * 0.7, Colors.White, 0.95 * fade, 0.5);
        // Shimmer bands rolling over the surface.
        for (int k = 0; k < 3; k++)
        {
            double y = c.Y + Math.Sin(_time * 1.7 + k * 2.1) * r * 0.7;
            double half = Math.Sqrt(Math.Max(0, r * r - (y - c.Y) * (y - c.Y))) * 0.9;
            dc.DrawLine(Line(Colors.White, 0.35 * fade, 1), new Point(c.X - half, y), new Point(c.X + half, y));
        }
    }

    // ------------------------------------------------------------------ domain: a void opens and the island falls into it

    private void Domain(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        var c = new Point(w * 0.5, h * 0.5);
        double open = 0.75 + 0.25 * Math.Sin(_time * 0.6);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 2, 0, 10)), null, new Rect(0, 0, w, h));

        // Stars rushing outward from the centre: the feeling of being pulled inside.
        int n = pill ? 26 : 90;
        for (int s = 0; s < n; s++)
        {
            double life = (_time * (0.25 + H(s, 1) * 0.35) * (1 + _intensity) + H(s, 2)) % 1, ang = H(s, 3) * 6.283;
            double d0 = Math.Pow(life, 2.2) * w * 0.7, d1 = Math.Pow(Math.Max(0, life - 0.05), 2.2) * w * 0.7;
            var a = new Point(c.X + Math.Cos(ang) * d0, c.Y + Math.Sin(ang) * d0 * (h / w) * 1.8);
            var b = new Point(c.X + Math.Cos(ang) * d1, c.Y + Math.Sin(ang) * d1 * (h / w) * 1.8);
            dc.DrawLine(Line(s % 4 == 0 ? _accent : Colors.White, life * 0.9, 0.6 + life * 1.4), b, a);
        }

        // The eye of the domain: rotating rings of light around a black pupil.
        double R = Math.Min(h * 0.46, w * 0.16) * open;
        Glow(dc, c, R * 2.4, _accent, 0.35);
        for (int k = 0; k < 4; k++)
        {
            dc.PushTransform(new RotateTransform(_time * (25 + k * 18) * (k % 2 == 0 ? 1 : -1), c.X, c.Y));
            var pen = Line(k == 0 ? Colors.White : _accent, 0.75 - k * 0.12, pill ? 0.8 : 1.4).Clone();
            pen.DashStyle = new DashStyle(new[] { 2.0 + k * 3, 3.0 + k * 2 }, 0);
            dc.DrawEllipse(null, pen, c, R * (0.7 + k * 0.22), R * (0.7 + k * 0.22));
            dc.Pop();
        }
        Glow(dc, c, R * 0.62, Colors.White, 0.9, 0.7);
        dc.DrawEllipse(Brushes.Black, null, c, R * 0.42, R * 0.42);
        Glow(dc, new Point(c.X - R * 0.12, c.Y - R * 0.12), R * 0.12, Colors.White, 0.8);
    }

    // ------------------------------------------------------------------ black flash: black lightning with red sparks

    private void BlackFlash(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double cycle = 2.2 - _intensity * 0.7, t = _time / cycle, p = t - Math.Floor(t);
        int n = (int)Math.Floor(t);
        var c = new Point(w * (0.2 + 0.6 * H(n, 1)), h * (0.3 + 0.4 * H(n, 2)));
        double hit = 1 - Band(p, 0, 0.45), ring = Ease(Band(p, 0, 0.6));
        Color red = Color.FromRgb(255, 26, 48);

        // Cursed sparks crackle the whole time, waiting for the next hit.
        for (int s = 0; s < (pill ? 2 : 5); s++)
        {
            int f = (int)(_time * 12) + s * 17;
            if (H(f, 41) > 0.6) continue;
            var a = new Point(H(f, 42) * w, H(f, 43) * h);
            dc.DrawGeometry(null, Line(red, 0.55, 1), Bolt(a, new Point(a.X + (H(f, 44) - 0.5) * 60, a.Y + (H(f, 45) - 0.5) * 30), 5, 5, f));
        }
        Glow(dc, new Point(w * 0.5, h), w * 0.5, Color.FromRgb(120, 0, 16), 0.35);

        // A red pulse of impact, then the world inverts for a split second.
        if (p < 0.06) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(120, 255, 30, 40)), null, new Rect(0, 0, w, h));
        Glow(dc, c, (pill ? 30 : 90) * (0.4 + ring), red, 0.6 * hit);
        for (int k = 0; k < 2; k++)
            dc.DrawEllipse(null, Line(k == 0 ? red : Colors.Black, (1 - ring) * 0.9, (pill ? 1.5 : 4) * (1 - ring) + 0.4), c, w * 0.35 * ring * (1 + k * 0.15), h * 0.7 * ring * (1 + k * 0.15));

        // Black bolts outlined in red, crackling outward from the impact.
        int bolts = pill ? 5 : 9;
        for (int b = 0; b < bolts; b++)
        {
            double ang = H(n * 13 + b, 3) * 6.283, len = (pill ? h * 0.9 : w * 0.28) * (0.5 + 0.5 * H(n * 13 + b, 4)) * (0.6 + 0.4 * ring);
            var end = new Point(c.X + Math.Cos(ang) * len, c.Y + Math.Sin(ang) * len * 0.6);
            var g = Bolt(c, end, 8, pill ? 4 : 10, n * 131 + b * 7 + (int)(_time * 24), branches: !pill);
            dc.DrawGeometry(null, Line(red, 0.9 * hit, pill ? 2.6 : 5), g);
            dc.DrawGeometry(null, Line(Colors.Black, hit, pill ? 1.2 : 2.4), g);
        }
        dc.DrawEllipse(Brushes.Black, Line(red, hit, 1.5), c, (pill ? 3 : 7) * hit, (pill ? 3 : 7) * hit);
        for (int s = 0; s < (pill ? 8 : 22); s++)
        {
            double ang = H(s + n * 7, 5) * 6.283, d = Ease(p) * (pill ? 30 : 110) * (0.4 + H(s, 6));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(255 * hit), 255, 80, 90)), null, new Point(c.X + Math.Cos(ang) * d, c.Y + Math.Sin(ang) * d * 0.6), 1.1, 1.1);
        }
    }

    // ------------------------------------------------------------------ beam: charge, then release

    private void Beam(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double cycle = 4.2 - _intensity * 1.2, p = (_time % cycle) / cycle;
        var src = new Point(w * 0.12, h * 0.55);
        Color cyan = Color.FromRgb(90, 210, 255);
        double charge = Ease(Band(p, 0, 0.45)), fire = Band(p, 0.45, 0.9), fade = 1 - Band(p, 0.8, 1);

        // Charging: energy pulled into the hands.
        Glow(dc, src, (pill ? 10 : 26) * (0.4 + charge) * (1 + 0.1 * Math.Sin(_time * 30)), cyan, 0.9 * fade);
        Glow(dc, src, (pill ? 5 : 12) * (0.4 + charge), Colors.White, fade, 0.5);
        if (p < 0.45)
            for (int s = 0; s < (pill ? 10 : 30); s++)
            {
                double life = 1 - ((_time * 1.8 + H(s, 1)) % 1), ang = H(s, 2) * 6.283, d = (pill ? 12 : 40) + life * (pill ? 20 : 70);
                var a = new Point(src.X + Math.Cos(ang) * d, src.Y + Math.Sin(ang) * d * 0.7);
                var b = new Point(src.X + Math.Cos(ang) * (d - 10), src.Y + Math.Sin(ang) * (d - 10) * 0.7);
                dc.DrawLine(Line(Colors.White, 0.8 * (1 - life), 1), a, b);
            }
        if (fire <= 0) return;

        // Release: a beam that punches across the island, pulsing in thickness.
        double reach = src.X + (w - src.X + 20) * Ease(fire * 3), thick = (pill ? 9 : 26) * (0.9 + 0.12 * Math.Sin(_time * 40)) * fade;
        var beam = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        beam.GradientStops.Add(new GradientStop(Color.FromArgb(0, cyan.R, cyan.G, cyan.B), 0));
        beam.GradientStops.Add(new GradientStop(Color.FromArgb(220, cyan.R, cyan.G, cyan.B), 0.25));
        beam.GradientStops.Add(new GradientStop(Colors.White, 0.5));
        beam.GradientStops.Add(new GradientStop(Color.FromArgb(220, cyan.R, cyan.G, cyan.B), 0.75));
        beam.GradientStops.Add(new GradientStop(Color.FromArgb(0, cyan.R, cyan.G, cyan.B), 1));
        dc.DrawRectangle(beam, null, new Rect(src.X, src.Y - thick, reach - src.X, thick * 2));
        Glow(dc, new Point(reach, src.Y), thick * 2.4, cyan, 0.8 * fade);
        // Rings travelling along the beam.
        for (int k = 0; k < 5; k++)
        {
            double x = src.X + ((_time * 600 + k * 120) % Math.Max(1, reach - src.X));
            dc.DrawEllipse(null, Line(Colors.White, 0.6 * fade, 1.2), new Point(x, src.Y), thick * 0.35, thick * 1.25);
        }
    }

    // ------------------------------------------------------------------ liquid glass: light moving through a lens

    private static readonly Brush GlassTop = MakeGlassTop();

    private static Brush MakeGlassTop()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(95, 255, 255, 255), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(18, 255, 255, 255), 0.42));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.55));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.82));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(40, 255, 255, 255), 1));
        b.Freeze();
        return b;
    }

    private void Glass(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        // Glossy top half and a faint lit lower lip, like a thick piece of curved glass.
        dc.DrawRectangle(GlassTop, null, new Rect(0, 0, w, h));

        // Caustics: soft pools of light drifting under the surface.
        for (int k = 0; k < 4; k++)
        {
            var p = new Point(w * (0.5 + 0.42 * Math.Sin(_time * (0.11 + k * 0.03) + k * 1.9)), h * (0.6 + 0.3 * Math.Cos(_time * (0.13 + k * 0.02) + k)));
            Glow(dc, p, Math.Max(w * 0.14, h * 0.8), k % 2 == 0 ? Color.FromRgb(170, 210, 255) : Color.FromRgb(255, 200, 235), 0.16 + 0.1 * _intensity);
        }

        // A bright diagonal glint sweeps across every few seconds.
        double cycle = 6.5, p01 = (_time % cycle) / cycle;
        if (p01 < 0.35)
        {
            double x = -w * 0.3 + (w * 1.6) * (p01 / 0.35), band = pill ? 26 : 60;
            var glint = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            glint.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
            glint.GradientStops.Add(new GradientStop(Color.FromArgb(90, 255, 255, 255), 0.5));
            glint.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            dc.PushTransform(new SkewTransform(-28, 0, x, h / 2));
            dc.DrawRectangle(glint, null, new Rect(x - band / 2, -h, band, h * 3));
            dc.Pop();
        }

        // Specular highlights near the top corners.
        Glow(dc, new Point(w * 0.18, h * (pill ? 0.18 : 0.08)), pill ? 12 : 34, Colors.White, 0.38);
        Glow(dc, new Point(w * 0.86, h * (pill ? 0.82 : 0.9)), pill ? 8 : 22, Colors.White, 0.18);
    }

    // ------------------------------------------------------------------ crimson moon: a night that belongs to demons

    private void CrimsonMoon(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        var c = new Point(w * 0.72, pill ? h * 0.5 : h * 0.42);
        double r = pill ? h * 0.36 : Math.Min(h * 0.32, 64);
        Color moon = Color.FromRgb(255, 70, 60), deep = Color.FromRgb(140, 10, 20);

        Glow(dc, c, r * 3.2, deep, 0.55);
        var disc = new RadialGradientBrush { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.4, 0.38) };
        disc.GradientStops.Add(new GradientStop(Color.FromRgb(255, 190, 160), 0));
        disc.GradientStops.Add(new GradientStop(moon, 0.5));
        disc.GradientStops.Add(new GradientStop(deep, 1));
        dc.DrawEllipse(disc, null, c, r, r);
        foreach (var (dx, dy, cr) in new[] { (-0.3, -0.2, 0.18), (0.25, 0.15, 0.12), (0.05, 0.42, 0.09), (-0.4, 0.3, 0.07) })
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, 90, 0, 10)), null, new Point(c.X + dx * r, c.Y + dy * r), cr * r, cr * r);

        // Thin clouds sliding across the face of the moon.
        for (int k = 0; k < 3; k++)
        {
            double x = (_time * (8 + k * 5) + k * 140) % (w + 200) - 100, y = c.Y + (k - 1) * r * 0.5;
            Glow(dc, new Point(x, y), r * 1.3, Color.FromRgb(20, 4, 10), 0.6);
        }
        // Mist along the bottom, and dark blossom petals drifting through.
        var mist = new LinearGradientBrush(Color.FromArgb(0, 120, 10, 20), Color.FromArgb(120, 120, 10, 20), 90);
        dc.DrawRectangle(mist, null, new Rect(0, h * 0.6, w, h * 0.4));
        for (int s = 0; s < (pill ? 8 : 28); s++)
        {
            double life = (_time * (0.05 + H(s, 1) * 0.07) * (1 + _intensity) + H(s, 2)) % 1;
            double x = (H(s, 3) * (w + 40) + _time * 22 + Math.Sin(_time + s) * 10) % (w + 40) - 20, y = life * (h + 10) - 5;
            double size = (pill ? 1.8 : 3.2) * (0.6 + H(s, 4) * 0.7), flipF = 0.35 + 0.65 * Math.Abs(Math.Sin(_time * 1.8 + s));
            dc.PushTransform(new RotateTransform(_time * 80 + s * 40, x, y));
            dc.DrawEllipse(new SolidColorBrush(s % 3 == 0 ? Color.FromRgb(255, 120, 140) : Color.FromRgb(200, 30, 50)), null, new Point(x, y), size, size * 0.5 * flipF);
            dc.Pop();
        }
    }
}
