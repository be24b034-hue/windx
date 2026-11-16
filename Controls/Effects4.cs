using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WindX.Controls;

/// <summary>Two scene backdrops: an overgrown battlefield at dawn, and a glowing curved signal wall.</summary>
public sealed partial class EffectLayer
{
    /// <summary>Lettering shown by the Signal wall (the island's background name).</summary>
    public string Label { get; set; } = "WINDX";

    private Geometry? _labelGeo;
    private string _labelFor = "";

    private void Scenes(DrawingContext dc, double w, double h)
    {
        if (_effect == IslandEffect.Overgrown) Overgrown(dc, w, h, false);
        else if (_effect == IslandEffect.Sunbeams) Overgrown(dc, w, h, true);
        else if (_effect == IslandEffect.Signal) Signal(dc, w, h);
    }

    // ------------------------------------------------------------------ overgrown: a helmet reclaimed by the field

    private static readonly Brush Steel = MakeSteel();
    private static readonly Brush VisorBrush = Solid(255, 38, 46, 48);
    private static readonly Brush Vine = Solid(255, 74, 122, 52);
    private static readonly Brush Leaf = Solid(255, 104, 160, 66);
    private static readonly Brush Bloom = Solid(255, 240, 150, 170);
    private static readonly Brush Ray = MakeRay();

    private static Brush MakeSteel()
    {
        var b = new RadialGradientBrush { GradientOrigin = new Point(0.35, 0.25), Center = new Point(0.45, 0.4), RadiusX = 0.75, RadiusY = 0.85 };
        b.GradientStops.Add(new GradientStop(Color.FromRgb(196, 208, 210), 0));
        b.GradientStops.Add(new GradientStop(Color.FromRgb(120, 136, 140), 0.5));
        b.GradientStops.Add(new GradientStop(Color.FromRgb(58, 70, 74), 1));
        b.Freeze();
        return b;
    }

    private static Brush MakeRay()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0.7, 1) };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 255, 236), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 236), 1));
        b.Freeze();
        return b;
    }

    private void Overgrown(DrawingContext dc, double w, double h, bool bare)
    {
        bool pill = h < 60;
        double ground = h - (pill ? 5 : 44);

        // Shafts of light breaking through the cloud, slowly breathing.
        for (int i = 0; i < (pill ? 3 : 5); i++)
        {
            double x = w * (0.08 + i * 0.13), spread = w * (0.07 + 0.03 * H(i, 1));
            var ray = new StreamGeometry();
            using (var ctx = ray.Open())
            {
                ctx.BeginFigure(new Point(x, -2), true, true);
                ctx.LineTo(new Point(x + spread * 0.5, -2), true, false);
                ctx.LineTo(new Point(x + w * 0.42 + spread * 2.2, h), true, false);
                ctx.LineTo(new Point(x + w * 0.42, h), true, false);
            }
            dc.PushOpacity(0.2 + 0.16 * Math.Sin(_time * 0.5 + i * 1.7) + _intensity * 0.12);
            dc.DrawGeometry(Ray, null, ray);
            dc.Pop();
        }

        if (bare)
        {
            // Dust caught in the light, rising slowly.
            for (int i = 0; i < (pill ? 8 : 34); i++)
            {
                double life = (_time * (0.03 + H(i, 7) * 0.05) + H(i, 8)) % 1;
                double mx = H(i, 9) * w + Math.Sin(_time * 0.6 + i) * 10, my = h - life * h, mr = 0.6 + H(i, 10) * 1.2;
                dc.PushOpacity(Math.Sin(life * Math.PI) * (0.35 + 0.5 * H(i, 11)));
                dc.DrawEllipse(Solid(255, 255, 250, 225), null, new Point(mx, my), mr, mr);
                dc.Pop();
            }
        }
        if (!pill && !bare)
        {
            // Far hills and two broken pylons in the haze.
            var hills = new StreamGeometry();
            using (var ctx = hills.Open())
            {
                ctx.BeginFigure(new Point(0, h), true, true);
                for (double x = 0; x <= w + 12; x += 12)
                    ctx.LineTo(new Point(x, ground - 16 - Math.Sin(x * 0.012 + 0.6) * 10 - Math.Sin(x * 0.031) * 4), false, true);
                ctx.LineTo(new Point(w, h), false, false);
            }
            dc.DrawGeometry(Solid(120, 40, 62, 66), null, hills);
            var steelLine = Stroke(Solid(150, 30, 44, 48), 0.9);
            foreach (double px in new[] { w * 0.07, w * 0.93 })
            {
                double top = ground - 62, bw = 7;
                dc.DrawLine(steelLine, new Point(px - bw, ground - 8), new Point(px - 2, top));
                dc.DrawLine(steelLine, new Point(px + bw, ground - 8), new Point(px + 2, top));
                for (int k = 0; k < 5; k++)
                {
                    double y0 = ground - 8 - k * 11, y1 = y0 - 11, half0 = bw - k * 1.05, half1 = bw - (k + 1) * 1.05;
                    dc.DrawLine(steelLine, new Point(px - half0, y0), new Point(px + half1, y1));
                    dc.DrawLine(steelLine, new Point(px + half0, y0), new Point(px - half1, y1));
                }
                dc.DrawLine(steelLine, new Point(px - 11, top + 8), new Point(px + 11, top + 8));
            }
        }

        if (bare) goto moths;

        // The helmet, half-sunk in the grass.
        double s = pill ? 0.42 : 1.25, cx = w * (pill ? 0.5 : 0.56), cy = ground + (pill ? 3 : 4);
        dc.PushTransform(new TranslateTransform(cx, cy));
        dc.PushTransform(new ScaleTransform(s, s));
        var dome = new StreamGeometry();
        using (var ctx = dome.Open())
        {
            ctx.BeginFigure(new Point(-30, 0), true, true);
            ctx.BezierTo(new Point(-34, -30), new Point(-14, -48), new Point(4, -48), true, true);
            ctx.BezierTo(new Point(24, -48), new Point(36, -30), new Point(32, 0), true, true);
        }
        dc.DrawGeometry(Steel, null, dome);
        var visor = Geometry.Parse("M-33,-22 L8,-26 Q14,-26 14,-21 L13,-9 Q13,-5 8,-5 L-32,-3 Z");
        dc.DrawGeometry(VisorBrush, new Pen(Solid(255, 150, 164, 166), 1.2), visor);
        dc.DrawGeometry(Solid(60, 255, 255, 255), null, Geometry.Parse("M-28,-20 L4,-23 L3,-19 L-28,-16 Z"));   // glint on the visor
        dc.DrawGeometry(Solid(255, 86, 100, 104), null, Geometry.Parse("M-34,-3 L12,-5 L18,0 L-32,0 Z"));       // brim
        dc.DrawEllipse(Solid(255, 70, 84, 88), null, new Point(22, -14), 2.2, 2.2);                             // rivet
        // Vines winding over the steel, with leaves and a few blossoms.
        var vinePen = new Pen(Vine, 2.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, vinePen, Geometry.Parse("M-34,-4 C-22,-14 -6,-2 6,-30 S26,-44 34,-18"));
        dc.DrawGeometry(null, vinePen, Geometry.Parse("M36,0 C30,-12 20,-6 14,-36"));
        dc.DrawGeometry(null, new Pen(Vine, 1.4), Geometry.Parse("M-12,-46 C-4,-52 8,-50 16,-44"));
        (double X, double Y)[] leaves = { (-22, -11), (-6, -8), (5, -27), (18, -38), (28, -26), (22, -10), (-2, -49), (10, -47) };
        for (int i = 0; i < leaves.Length; i++)
        {
            dc.PushTransform(new RotateTransform(i * 47 + Math.Sin(_time * 1.3 + i) * 8, leaves[i].X, leaves[i].Y));
            dc.DrawEllipse(Leaf, null, new Point(leaves[i].X, leaves[i].Y), 3.6, 1.7);
            dc.Pop();
        }
        foreach (var (bx, by) in new[] { (-17.0, -45.0), (12.0, -49.0), (30.0, -31.0) })
        {
            for (int p = 0; p < 5; p++)
                dc.DrawEllipse(Bloom, null, new Point(bx + Math.Cos(p * 1.257) * 2.3, by + Math.Sin(p * 1.257) * 2.3), 1.7, 1.7);
            dc.DrawEllipse(Solid(255, 250, 220, 120), null, new Point(bx, by), 1.1, 1.1);
        }
        dc.Pop();
        dc.Pop();

        // Grass in the wind, two depths, in front of everything.
        for (int layer = 0; layer < 2; layer++)
        {
            var blades = new StreamGeometry();
            using (var ctx = blades.Open())
            {
                double step = pill ? 3.2 : 4.4;
                for (double x = -4; x < w + 4; x += step)
                {
                    int id = (int)(x * 7) + layer * 991;
                    double bh = (pill ? 5 : 14) * (0.5 + H(id, 1)) * (1 + layer * 0.5);
                    double sway = Math.Sin(_time * (1.3 + _intensity) + x * 0.045 + layer) * bh * 0.35 + (H(id, 2) - 0.5) * 4;
                    double bx = x + H(id, 3) * step;
                    ctx.BeginFigure(new Point(bx - 0.9, h + 1), true, true);
                    ctx.QuadraticBezierTo(new Point(bx, h - bh * 0.6), new Point(bx + sway, h - bh - (pill ? 1 : 6) * layer), true, true);
                    ctx.QuadraticBezierTo(new Point(bx + 0.6, h - bh * 0.5), new Point(bx + 0.9, h + 1), true, true);
                }
            }
            dc.DrawGeometry(layer == 0 ? Solid(235, 46, 92, 44) : Solid(245, 92, 150, 60), null, blades);
        }

        moths:
        // Pale moths drifting through the light.
        int moths = pill ? 2 : 6;
        for (int i = 0; i < moths; i++)
        {
            double x = w * (0.5 + 0.46 * Math.Sin(_time * (0.16 + H(i, 5) * 0.14) + i * 2.3));
            double y = h * (0.45 + 0.32 * Math.Cos(_time * (0.21 + H(i, 6) * 0.17) + i));
            double flap = Math.Abs(Math.Sin(_time * 9 + i * 2)) * (pill ? 1.6 : 3.2), sz = pill ? 1.6 : 3;
            var wings = new StreamGeometry();
            using (var ctx = wings.Open())
            {
                ctx.BeginFigure(new Point(x, y), true, true); ctx.LineTo(new Point(x - sz, y - flap), true, false); ctx.LineTo(new Point(x - sz * 0.8, y + flap * 0.5), true, false);
                ctx.BeginFigure(new Point(x, y), true, true); ctx.LineTo(new Point(x + sz, y - flap), true, false); ctx.LineTo(new Point(x + sz * 0.8, y + flap * 0.5), true, false);
            }
            dc.PushOpacity(0.85);
            dc.DrawGeometry(Solid(255, 252, 252, 240), null, wings);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ signal: a curved wall of light with lettering sliding across it

    private void Signal(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double pulse = 0.82 + 0.1 * Math.Sin(_time * 1.7) + _intensity * 0.25;
        Color hot = _accent;

        // The wall recedes to the left: short far edge, tall near edge.
        double x0 = pill ? 0 : w * 0.22, floor = h * (pill ? 0.92 : 0.70);
        Point a = new(x0, floor - h * (pill ? 0.62 : 0.20)), b = new(w, floor - h * (pill ? 0.80 : 0.60));
        Point c = new(w, floor + (pill ? 0 : h * 0.06)), d = new(x0, floor);
        Geometry Wall(double grow)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(a.X - grow, a.Y - grow), true, true);
                ctx.QuadraticBezierTo(new Point(w * 0.62, a.Y - h * 0.04 - grow), new Point(b.X + grow, b.Y - grow), true, true);
                ctx.LineTo(new Point(c.X + grow, c.Y + grow), true, false);
                ctx.QuadraticBezierTo(new Point(w * 0.62, d.Y + h * 0.015 + grow), new Point(d.X - grow, d.Y + grow), true, true);
            }
            return g;
        }

        // Bloom around the wall, then the wall itself.
        for (int k = 4; k >= 1; k--)
        {
            dc.PushOpacity(0.07 * pulse);
            dc.DrawGeometry(new SolidColorBrush(hot), null, Wall(k * (pill ? 2.5 : 6)));
            dc.Pop();
        }
        var face = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        face.GradientStops.Add(new GradientStop(Mix(Colors.Black, 0.35), 0));
        face.GradientStops.Add(new GradientStop(hot, 0.45));
        face.GradientStops.Add(new GradientStop(Mix(Colors.White, 0.18), 1));
        var wall = Wall(0);
        dc.PushOpacity(Math.Min(1, pulse));
        dc.DrawGeometry(face, null, wall);
        dc.Pop();

        // Heavy black lettering marching along the wall, leaning with its perspective.
        string text = string.IsNullOrWhiteSpace(Label) ? "WINDX" : Label;
        if (_labelGeo == null || _labelFor != text)
        {
            var ft = new FormattedText(text + "   ", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial Black, Segoe UI Black, Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Expanded),
                100, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            _labelGeo = ft.BuildGeometry(new Point(0, 0));
            _labelGeo.Freeze();
            _labelFor = text;
        }
        var box = _labelGeo.Bounds;
        if (box.Width > 1)
        {
            double tall = (c.Y - b.Y) * (pill ? 0.62 : 0.4), k = tall / box.Height;
            double slope = Math.Atan2((b.Y + c.Y) / 2 - (a.Y + d.Y) / 2, b.X - a.X) * 180 / Math.PI;
            double run = box.Width * k, shift = _time * (pill ? 26 : 46) * (0.8 + _intensity * 0.8) % run;
            dc.PushClip(wall);
            for (double x = w - run * 0.25 + shift - run * 4; x < w + run; x += run)
            {
                var m = new TransformGroup();
                m.Children.Add(new TranslateTransform(-box.X, -box.Y - box.Height / 2));
                m.Children.Add(new ScaleTransform(k, k));
                m.Children.Add(new SkewTransform(0, slope));
                m.Children.Add(new TranslateTransform(x, (b.Y + c.Y) / 2 - (w - x) * Math.Tan(slope * Math.PI / 180)));
                dc.PushTransform(m);
                dc.DrawGeometry(Brushes.Black, null, _labelGeo);
                dc.Pop();
            }
            dc.Pop();
        }

        if (pill) return;
        // Polished floor: the wall's light smeared across it, and one figure looking up.
        var spill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        spill.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(120 * Math.Min(1, pulse)), hot.R, hot.G, hot.B), 0));
        spill.GradientStops.Add(new GradientStop(Color.FromArgb(0, hot.R, hot.G, hot.B), 1));
        var pool = new StreamGeometry();
        using (var ctx = pool.Open())
        {
            ctx.BeginFigure(d, true, true);
            ctx.LineTo(c, true, false);
            ctx.LineTo(new Point(w, h), true, false);
            ctx.LineTo(new Point(x0 - w * 0.2, h), true, false);
        }
        dc.DrawGeometry(spill, null, pool);
        double fx = w * 0.36, fy = floor + 3;
        dc.DrawEllipse(Brushes.Black, null, new Point(fx, fy - 21), 2.6, 2.8);
        dc.DrawRoundedRectangle(Brushes.Black, null, new Rect(fx - 3.2, fy - 18, 6.4, 12), 2, 2);
        dc.DrawRectangle(Brushes.Black, null, new Rect(fx - 2.6, fy - 7, 2, 8));
        dc.DrawRectangle(Brushes.Black, null, new Rect(fx + 0.6, fy - 7, 2, 8));
        dc.PushOpacity(0.3);
        dc.DrawRoundedRectangle(Brushes.Black, null, new Rect(fx - 3.2, fy + 1, 6.4, 16), 2, 2);   // reflection
        dc.Pop();
    }
}
