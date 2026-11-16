using System.Windows;
using System.Windows.Media;

namespace WindX.Controls;

/// <summary>Showpiece backdrops: one F1 car you choose, a battleground drop, flowing ribbons and a spiral galaxy.</summary>
public sealed partial class EffectLayer
{
    /// <summary>Which colour scheme the Race car wears: an index into Liveries, or -1 for the theme's own colour.</summary>
    public int Livery { get; set; } = -1;

    private void Extra(DrawingContext dc, double w, double h)
    {
        switch (_effect)
        {
            case IslandEffect.Battleground: Battleground(dc, w, h); break;
            case IslandEffect.Ribbons: Ribbons(dc, w, h); break;
            case IslandEffect.Galaxy: Galaxy(dc, w, h); break;
            case IslandEffect.Overgrown or IslandEffect.Signal or IslandEffect.Sunbeams: Scenes(dc, w, h); break;
            default: Powers(dc, w, h); break;
            case IslandEffect.Inferno or IslandEffect.Pool or IslandEffect.Lights or IslandEffect.Mist: Pixel(dc, w, h); break;
        }
    }

    // ------------------------------------------------------------------ race: one car, holding the racing line

    // Each scheme paints the car in regions, the way the real cars are colour-blocked:
    // front bodywork, engine cover, nose tip, lower body / floor edge, pinstripes, wings, small details.
    private static readonly (int Body, int Engine, int Nose, int Lower, int Stripe, int Wing, int Detail)[] Schemes =
    {
        (0x16224F, 0x16224F, 0xE1061C, 0x0C1433, 0xFFC906, 0xE1061C, 0xFFC906),   // navy, red nose, yellow flashes
        (0xDC0000, 0xDC0000, 0xDC0000, 0x161616, 0xFFF200, 0x161616, 0xFFFFFF),   // scarlet over black
        (0xC8CCCE, 0x121214, 0xC8CCCE, 0x121214, 0x00D2BE, 0x121214, 0x00D2BE),   // silver into black, teal lines
        (0xFF8000, 0x1C1C1E, 0xFF8000, 0x1C1C1E, 0x47C7FC, 0xFF8000, 0x47C7FC),   // papaya, black engine cover, blue
        (0x00594F, 0x00594F, 0x00594F, 0x0A0A0A, 0xCEDC00, 0x00594F, 0xCEDC00),   // racing green, lime lines
        (0x0F4FB5, 0xFF87BC, 0x0F4FB5, 0x101014, 0xFFFFFF, 0xFF87BC, 0xFFFFFF),   // blue into pink
        (0x00205B, 0x00205B, 0x64C4FF, 0x000B25, 0x64C4FF, 0x00205B, 0xFFFFFF),   // dark blue, light-blue nose
        (0xF1F3F4, 0x1534CC, 0xF1F3F4, 0x1534CC, 0xE1061C, 0x1534CC, 0xE1061C),   // white over blue, red line
        (0x0C0C0E, 0x52E252, 0x0C0C0E, 0x0C0C0E, 0x52E252, 0x52E252, 0xFFFFFF),   // black and neon green
        (0xE6E6E6, 0x121214, 0xE6002B, 0x121214, 0xE6002B, 0x121214, 0xE6002B),   // white, black, red nose
    };

    private static readonly Geometry EngineZone = Geometry.Parse("M0,0 L21.5,0 L24.5,5.3 L13,9 L0,9 Z");
    private static readonly Geometry NoseZone = Geometry.Parse("M35,0 L48,0 L48,9 L37.5,9 Z");
    private static readonly Geometry Bargeboard = Geometry.Parse("M28.5,6.3 L33,6.9 L33.4,8.7 L28,8.7 Z");
    private static readonly Brush Gloss = Solid(70, 255, 255, 255);
    private static readonly Brush Shadow = Solid(110, 0, 0, 0);

    private void Race(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double s = pill ? 1.35 : 3.1, ground = h - (pill ? 2.5 : 46);   // open island: drive above the tab bar
        double pace = 0.7 + _intensity * 0.9;

        Brush body, engine, nose, lower, stripe, wing, detail;
        if (Livery >= 0)
        {
            var l = Schemes[Livery % Schemes.Length];
            body = Rgb(l.Body); engine = Rgb(l.Engine); nose = Rgb(l.Nose); lower = Rgb(l.Lower); stripe = Rgb(l.Stripe); wing = Rgb(l.Wing); detail = Rgb(l.Detail);
        }
        else
        {
            body = Tone(0); engine = Tone(-0.45); nose = Tone(0); lower = Tone(-0.7); stripe = Tone(0.8); wing = Tone(-0.45); detail = Tone(0.6);
        }

        // The track streams past: kerb line, lane dashes, and a blur of trackside lights.
        var road = Stroke(Tone(0.2, 100), pill ? 0.8 : 1.3);
        dc.DrawLine(road, new Point(0, ground + 0.6 * s), new Point(w, ground + 0.6 * s));
        if (!pill)
        {
            for (double x = -(_time * 420 * pace % 46); x < w; x += 46)
                dc.DrawLine(road, new Point(x, ground + 3.2 * s), new Point(x + 20, ground + 3.2 * s));
            for (int i = 0; i < 7; i++)
            {
                double span = w + 160, x = w - (_time * (300 + H(i, 1) * 260) * pace + H(i, 2) * span) % span + 60;
                double y = h * (0.12 + H(i, 3) * 0.5), len = 30 + H(i, 4) * 70;
                dc.PushOpacity(0.07 + 0.12 * H(i, 5));
                dc.DrawLine(Stroke(stripe, 1 + H(i, 6)), new Point(x, y), new Point(x + len, y));
                dc.Pop();
            }
        }

        // The car stays in shot, surging forward and dropping back like a TV tracking shot.
        double cx = w * 0.5 - 24 * s + Math.Sin(_time * 0.42) * w * 0.15 + Math.Sin(_time * 1.27) * w * 0.03;
        double cy = ground - 9.8 * s + Math.Sin(_time * 38) * 0.22;

        // Slipstream and sparks from the floor.
        for (int k = 0; k < 6; k++)
        {
            double len = (16 + H(k, 9) * 30 + 10 * Math.Sin(_time * 9 + k)) * s, ly = cy + (2.2 + k * 1.25) * s;
            dc.PushOpacity(0.10 + 0.07 * k);
            dc.DrawLine(Stroke(k % 2 == 0 ? stripe : body, 0.9), new Point(cx - len - 2, ly), new Point(cx - 2, ly));
            dc.Pop();
        }
        for (int k = 0; k < (pill ? 3 : 9); k++)
        {
            double life = (_time * (2.2 + H(k, 11) * 2) + H(k, 12)) % 1, r = 0.45 * s * (1 - life * 0.5);
            dc.PushOpacity((1 - life) * 0.9);
            dc.DrawEllipse(EmberBrush, null, new Point(cx + 6 * s - life * (26 + H(k, 13) * 30) * s, cy + 9.3 * s - life * H(k, 14) * 5 * s), r, r);
            dc.Pop();
        }

        dc.PushTransform(new TranslateTransform(cx, cy));
        dc.PushTransform(new ScaleTransform(s, s));
        dc.DrawEllipse(Shadow, null, new Point(23, 9.9), 25, 0.9);          // contact shadow
        dc.DrawGeometry(Carbon, null, CarFloor);

        // Rear wing: endplate in the wing colour with a stripe, two-element flap, DRS gap.
        dc.DrawRectangle(wing, null, new Rect(0.1, 1.1, 2.7, 6.6));
        dc.DrawRectangle(stripe, null, new Rect(0.1, 3.3, 2.7, 0.5));
        dc.DrawRectangle(Carbon, null, new Rect(-0.6, 0.8, 4.8, 0.9));
        dc.DrawRectangle(body, null, new Rect(-0.4, 2.0, 4.4, 0.8));
        // Front wing: multi-element main plane and a coloured endplate.
        dc.DrawRectangle(Carbon, null, new Rect(41.4, 8.3, 6.6, 0.5));
        dc.DrawRectangle(wing, null, new Rect(42, 7.6, 5.6, 0.5));
        dc.DrawRectangle(stripe, null, new Rect(47, 6.7, 0.9, 2.7));

        // Bodywork, colour-blocked region by region inside the car's outline.
        dc.DrawGeometry(body, null, CarBody);
        dc.PushClip(CarBody);
        dc.DrawGeometry(engine, null, EngineZone);                           // engine cover and airbox
        dc.DrawGeometry(nose, null, NoseZone);                               // nose tip
        dc.DrawRectangle(lower, null, new Rect(0, 7.7, 48, 2));              // lower body
        dc.DrawGeometry(stripe, null, Geometry.Parse("M12,6.1 L27,6.1 L27.8,6.6 L11.6,6.6 Z"));   // sidepod line
        dc.DrawGeometry(stripe, null, Geometry.Parse("M4,4.7 L15,3.2 L15.6,3.7 L4.6,5.3 Z"));     // engine-cover flash
        dc.DrawRectangle(Gloss, null, new Rect(3, 2.2, 42, 0.5));            // light catching the top edge
        dc.Pop();
        dc.DrawGeometry(Carbon, null, Bargeboard);
        dc.DrawGeometry(lower, null, CarSidepod);
        dc.DrawLine(new Pen(stripe, 0.4), new Point(12.4, 7.0), new Point(29.6, 7.0));
        dc.DrawRectangle(Carbon, null, new Rect(17.6, 2.5, 2.6, 0.9));       // airbox intake
        dc.DrawRectangle(detail, null, new Rect(20.3, 1.2, 0.3, 1.1));       // T-cam
        dc.DrawLine(new Pen(Carbon, 0.3), new Point(27.6, 4.7), new Point(29.2, 4.2));   // mirror stalk
        dc.DrawRectangle(body, null, new Rect(28.8, 3.8, 1.5, 0.7));
        dc.DrawLine(new Pen(Carbon, 0.3), new Point(34, 6.2), new Point(36.6, 5.6));     // front suspension
        dc.DrawLine(new Pen(Carbon, 0.3), new Point(33.5, 6.9), new Point(36.6, 6.6));
        dc.DrawLine(new Pen(Carbon, 0.3), new Point(11, 5.0), new Point(7.6, 5.6));      // rear suspension
        dc.DrawEllipse(detail, null, new Point(23.2, 4.2), 1.25, 1.25);      // helmet
        dc.DrawRectangle(Carbon, null, new Rect(23.4, 3.9, 1.1, 0.5));       // visor
        dc.DrawGeometry(null, new Pen(Carbon, 0.8), CarHalo);

        foreach (double wx in new[] { 7.6, 36.6 })
        {
            double r = wx < 20 ? 3.75 : 3.45;
            var hub = new Point(wx, 9.8 - r);
            dc.DrawEllipse(Tyre, null, hub, r, r);
            dc.DrawEllipse(null, new Pen(stripe, 0.4), hub, r - 0.7, r - 0.7);   // tyre compound band
            dc.DrawEllipse(Carbon, new Pen(detail, 0.35), hub, 1.9, 1.9);        // wheel cover
            for (int sp = 0; sp < 5; sp++)                                        // spokes, so the wheels visibly spin
            {
                double a = _time * 30 * pace + sp * Math.PI * 2 / 5;
                dc.DrawLine(new Pen(detail, 0.28), hub, new Point(hub.X + Math.Cos(a) * 1.8, hub.Y + Math.Sin(a) * 1.8));
            }
            dc.DrawEllipse(null, new Pen(Gloss, 0.3), hub, r - 0.2, r - 0.2);    // sidewall sheen
        }
        dc.Pop();
        dc.Pop();
    }

    // ------------------------------------------------------------------ battleground: the drop

    private static readonly Geometry Plane = Geometry.Parse("M0,3 L6,1.6 L22,1.4 Q27,1.6 30,3 Q27,4.4 22,4.6 L15,4.6 L9,9 L6.5,9 L9.5,4.6 L4,4.4 L1.5,6.6 L0,6.6 L1.2,3.6 Z M2,3 L0.4,0 L2.4,0 L5.4,2.6 Z");

    private void Battleground(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        // Rolling hills along the bottom.
        for (int layer = 0; layer < 2; layer++)
        {
            var hill = new StreamGeometry();
            using (var ctx = hill.Open())
            {
                ctx.BeginFigure(new Point(0, h), true, true);
                for (double x = 0; x <= w + 10; x += 10)
                    ctx.LineTo(new Point(x, h - (pill ? 4 : 13) * (1 - layer * 0.35) - Math.Sin(x * 0.018 + layer * 2 + 1) * (pill ? 2 : 7) - Math.Sin(x * 0.047 + layer) * (pill ? 1 : 3)), false, true);
                ctx.LineTo(new Point(w, h), false, false);
            }
            dc.DrawGeometry(Tone(-0.55 - layer * 0.2, (byte)(150 + layer * 60)), null, hill);
        }

        // The play zone closing in.
        double zone = 1 - (_time * 0.05 % 1);
        dc.PushOpacity(0.5);
        dc.DrawEllipse(null, Stroke(Solid(255, 90, 170, 255), pill ? 1 : 1.6), new Point(w * 0.5, h * 1.05), w * (0.22 + 0.5 * zone), h * (0.5 + 0.7 * zone));
        dc.Pop();

        // The transport crossing the sky.
        double ps = pill ? 0.75 : 1.5, span = w + 120 * ps;
        double px = (_time * 46 * (1 + _intensity * 0.5)) % span - 60 * ps, py = h * (pill ? 0.12 : 0.1);
        dc.PushTransform(new TranslateTransform(px, py));
        dc.PushTransform(new ScaleTransform(ps, ps));
        dc.DrawGeometry(Tone(0.35, 230), null, Plane);
        dc.Pop(); dc.Pop();

        // Parachutes drifting down; every third one carries a supply crate trailing red smoke.
        int chutes = pill ? 2 : 6;
        for (int i = 0; i < chutes; i++)
        {
            double life = (_time * (0.07 + H(i, 1) * 0.05) + H(i, 2)) % 1;
            double x = w * (0.08 + 0.84 * H(i, 3)) + Math.Sin(_time * 0.9 + i * 2) * 9, y = h * 0.14 + life * h * (pill ? 0.6 : 0.72);
            double s = (pill ? 0.6 : 1.15) * (0.8 + H(i, 4) * 0.5);
            bool crate = i % 3 == 0;
            dc.PushOpacity(Math.Min(1, life * 8) * Math.Min(1, (1 - life) * 6));
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(Math.Sin(_time * 1.1 + i) * 7));
            dc.PushTransform(new ScaleTransform(s, s));
            var canopy = new StreamGeometry();
            using (var ctx = canopy.Open())
            {
                ctx.BeginFigure(new Point(-9, 0), true, true);
                ctx.ArcTo(new Point(9, 0), new Size(9, 8), 0, false, SweepDirection.Clockwise, true, true);
                for (int k = 2; k >= -3; k--) ctx.QuadraticBezierTo(new Point(k * 3 + 1.5, -1.6), new Point(k * 3, 0), true, true);
            }
            dc.DrawGeometry(crate ? Solid(235, 214, 48, 40) : Tone(0), null, canopy);
            var cord = new Pen(Tone(0.6, 170), 0.35);
            foreach (double cxp in new[] { -9.0, -3, 3, 9 }) dc.DrawLine(cord, new Point(cxp, 0), new Point(0, 11));
            if (crate)
            {
                dc.DrawRectangle(Solid(255, 122, 92, 48), new Pen(Solid(255, 70, 52, 26), 0.5), new Rect(-3.2, 11, 6.4, 5));
                for (int k = 0; k < 4; k++)
                {
                    double sl = (_time * 0.9 + k * 0.25) % 1;
                    dc.PushOpacity((1 - sl) * 0.5);
                    dc.DrawEllipse(Solid(255, 220, 50, 44), null, new Point(Math.Sin(k * 2.1 + _time) * 2, 10 - sl * 16), 1.5 + sl * 3, 1.5 + sl * 3);
                    dc.Pop();
                }
            }
            else
            {
                dc.DrawEllipse(Tone(0.7), null, new Point(0, 11.6), 1.2, 1.2);            // head
                dc.DrawLine(Stroke(Tone(0.7), 1.4), new Point(0, 12.6), new Point(0, 16.2)); // body
            }
            dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
        }
    }

    // ------------------------------------------------------------------ ribbons: silk in the wind

    private void Ribbons(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        double energy = 0.55 + _intensity * 0.9;
        Color[] tint = { _accent, Mix(Colors.White, 0.45), Mix(Color.FromRgb(120, 80, 255), 0.5), Mix(Color.FromRgb(0, 220, 200), 0.5), Mix(Colors.White, 0.75) };
        int n = pill ? 3 : 5;
        for (int r = 0; r < n; r++)
        {
            double mid = h * (pill ? 0.55 : 0.5 + (r - (n - 1) / 2.0) * 0.07);
            double amp = h * (pill ? 0.3 : 0.2) * energy * (1 - r * 0.08);
            double k1 = 0.012 + r * 0.0025, k2 = 0.026 - r * 0.002, s1 = 0.9 + r * 0.27, s2 = 0.55 + r * 0.19;

            // Each ribbon is a band between two curves that cross over each other, so it twists like fabric.
            double Top(double x) => mid + amp * Math.Sin(x * k1 + _time * s1 + r * 1.3) * Math.Sin(Math.PI * x / w)
                                        + amp * 0.35 * Math.Sin(x * k2 - _time * s2 + r);
            double Bottom(double x) => mid + amp * Math.Sin(x * k1 + _time * s1 + r * 1.3 + 0.9) * Math.Sin(Math.PI * x / w)
                                           + amp * 0.35 * Math.Sin(x * k2 - _time * s2 + r + 1.4) + (pill ? 1.5 : 3);

            var band = new StreamGeometry();
            using (var ctx = band.Open())
            {
                ctx.BeginFigure(new Point(0, Top(0)), true, true);
                for (double x = 4; x <= w; x += 4) ctx.LineTo(new Point(x, Top(x)), true, true);
                for (double x = w; x >= 0; x -= 4) ctx.LineTo(new Point(x, Bottom(x)), true, true);
            }
            var c = tint[r % tint.Length];
            var fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0));
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(150, c.R, c.G, c.B), 0.3 + 0.2 * Math.Sin(_time * 0.5 + r)));
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(110, c.R, c.G, c.B), 0.7));
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
            dc.DrawGeometry(fill, null, band);

            // A bright thread along the ribbon's edge.
            var edge = new StreamGeometry();
            using (var ctx = edge.Open())
            {
                ctx.BeginFigure(new Point(0, Top(0)), false, false);
                for (double x = 4; x <= w; x += 4) ctx.LineTo(new Point(x, Top(x)), true, true);
            }
            var line = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            line.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
            line.GradientStops.Add(new GradientStop(Color.FromArgb(210, (byte)((c.R + 510) / 3), (byte)((c.G + 510) / 3), (byte)((c.B + 510) / 3)), 0.5));
            line.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            dc.DrawGeometry(null, new Pen(line, pill ? 0.8 : 1.2), edge);
        }
    }

    // ------------------------------------------------------------------ galaxy: a slowly turning spiral

    private void Galaxy(DrawingContext dc, double w, double h)
    {
        bool pill = h < 60;
        var c = new Point(w * 0.5, h * (pill ? 0.5 : 0.52));
        double radius = Math.Max(w * 0.5, h * 0.9), tilt = pill ? 0.2 : 0.42;
        double spin = _time * (0.1 + _intensity * 0.12);

        // Glowing core.
        var core = new RadialGradientBrush();
        core.GradientStops.Add(new GradientStop(Color.FromArgb(200, 255, 255, 255), 0));
        core.GradientStops.Add(new GradientStop(Color.FromArgb(110, _accent.R, _accent.G, _accent.B), 0.25));
        core.GradientStops.Add(new GradientStop(Color.FromArgb(0, _accent.R, _accent.G, _accent.B), 1));
        dc.DrawEllipse(core, null, c, radius * 0.34, radius * 0.34 * Math.Max(tilt * 1.5, 0.4));

        Brush[] star = { Tone(0), Tone(0.5), Tone(0.9), new SolidColorBrush(Mix(Color.FromRgb(150, 110, 255), 0.5)) };
        int n = pill ? 90 : 420;
        const int arms = 3;
        for (int i = 0; i < n; i++)
        {
            double t = H(i, 1);                                  // distance along the arm
            double r = Math.Pow(t, 0.75) * radius;
            // Inner stars orbit faster: that shear is what keeps the arms looking alive.
            double a = (i % arms) * (2 * Math.PI / arms) + t * 5.2 + spin / (0.25 + t) + (H(i, 2) - 0.5) * (0.9 - t * 0.5);
            double x = c.X + Math.Cos(a) * r, y = c.Y + Math.Sin(a) * r * tilt + (H(i, 3) - 0.5) * 6 * (1 - t);
            if (x < -2 || x > w + 2 || y < -2 || y > h + 2) continue;
            double twinkle = 0.55 + 0.45 * Math.Sin(_time * (1 + H(i, 4) * 3) + i);
            double size = (0.35 + H(i, 5) * 1.05) * (pill ? 0.8 : 1);
            dc.PushOpacity((0.25 + 0.75 * (1 - t)) * twinkle);
            dc.DrawEllipse(star[i % 4], null, new Point(x, y), size, size);
            dc.Pop();
        }
    }
}
