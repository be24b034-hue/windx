using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindX.Controls;

/// <summary>
/// Realistic backdrops. Instead of drawing shapes, these simulate the thing itself on a small grid of
/// cells and paint the result as an image: heat rising and cooling (fire), a rippling height-field (water),
/// drifting turbulence (aurora, mist). The grid is ~1/2.4 of the island's size, so a frame is a few
/// thousand cells: cheap, and smooth once it is scaled up.
/// </summary>
public sealed partial class EffectLayer
{
    private WriteableBitmap? _bmp;
    private byte[] _px = Array.Empty<byte>();
    private float[] _a = Array.Empty<float>(), _b = Array.Empty<float>();
    private int _pw, _ph;
    private IslandEffect _simFor = IslandEffect.None;
    private readonly Random _rng = new(7);
    private static readonly uint[] FirePalette = BuildFirePalette();

    private void Pixel(DrawingContext dc, double w, double h)
    {
        int pw = Math.Clamp((int)(w / 2.4), 24, 220), ph = Math.Clamp((int)(h / 2.4), 10, 160);
        if (_bmp == null || pw != _pw || ph != _ph || _simFor != _effect)
        {
            _pw = pw; _ph = ph; _simFor = _effect;
            _bmp = new WriteableBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32, null);
            _px = new byte[pw * ph * 4];
            _a = new float[pw * (ph + 3)];
            _b = new float[pw * (ph + 3)];
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);
        }

        switch (_effect)
        {
            case IslandEffect.Inferno: SimFire(); break;
            case IslandEffect.Pool: SimWater(); break;
            case IslandEffect.Lights: SimAurora(); break;
            default: SimMist(); break;
        }
        _bmp.WritePixels(new Int32Rect(0, 0, _pw, _ph), _px, _pw * 4, 0);
        dc.DrawImage(_bmp, new Rect(0, 0, w, h));

        if (_effect == IslandEffect.Inferno) Sparks(dc, w, h);
    }

    private void Put(int i, int a, int r, int g, int b)
    {
        // Premultiplied BGRA.
        _px[i] = (byte)(b * a / 255); _px[i + 1] = (byte)(g * a / 255); _px[i + 2] = (byte)(r * a / 255); _px[i + 3] = (byte)a;
    }

    // ------------------------------------------------------------------ fire: heat rises, spreads and cools

    private static uint[] BuildFirePalette()
    {
        // Black-body-ish ramp: transparent, deep red, orange, yellow, white-hot.
        (double At, int A, int R, int G, int B)[] stops =
        {
            (0, 0, 40, 0, 0), (0.18, 70, 130, 8, 0), (0.36, 190, 215, 40, 4), (0.55, 235, 255, 120, 10),
            (0.75, 250, 255, 205, 60), (0.9, 255, 255, 240, 170), (1, 255, 255, 255, 240),
        };
        var p = new uint[256];
        for (int i = 0; i < 256; i++)
        {
            double t = i / 255.0;
            int k = 0;
            while (k < stops.Length - 2 && t > stops[k + 1].At) k++;
            var (a0, s0, r0, g0, b0) = stops[k];
            var (a1, s1, r1, g1, b1) = stops[k + 1];
            double f = Math.Clamp((t - a0) / (a1 - a0), 0, 1);
            int a = (int)(s0 + (s1 - s0) * f), r = (int)(r0 + (r1 - r0) * f), g = (int)(g0 + (g1 - g0) * f), b = (int)(b0 + (b1 - b0) * f);
            p[i] = (uint)(a << 24 | r << 16 | g << 8 | b);
        }
        return p;
    }

    private void SimFire()
    {
        int w = _pw, h = _ph;
        float power = 0.72f + (float)_intensity * 0.5f;
        // Fuel bed: two hidden rows below the picture, hot spots that wander and flicker.
        for (int x = 0; x < w; x++)
        {
            float bed = 0.55f + 0.3f * (float)Math.Sin(x * 0.21 + _time * 2.3) + 0.25f * (float)Math.Sin(x * 0.07 - _time * 1.1);
            float v = Math.Clamp(bed * power + (float)_rng.NextDouble() * 0.55f, 0, 1.25f);
            _a[(h + 1) * w + x] = v;
            _a[(h + 2) * w + x] = v;
            _a[h * w + x] = v * 0.92f;
        }
        // Each cell takes the heat of the cells beneath it, drifts a little sideways, and cools.
        float cooling = 2.05f / h;
        for (int y = 0; y < h; y++)
        {
            int row = y * w, below = row + w, below2 = below + w;
            for (int x = 0; x < w; x++)
            {
                int drift = _rng.Next(3) - 1;
                int xs = Math.Clamp(x + drift, 1, w - 2);
                float heat = (_a[below + xs - 1] + _a[below + xs] * 2 + _a[below + xs + 1] + _a[below2 + xs]) * 0.2f;
                heat -= cooling * (0.35f + (float)_rng.NextDouble());
                _a[row + x] = heat > 0 ? heat : 0;
            }
        }
        for (int i = 0, n = w * h; i < n; i++)
        {
            uint c = FirePalette[(int)Math.Min(255, _a[i] * 255)];
            Put(i * 4, (int)(c >> 24), (int)(c >> 16 & 255), (int)(c >> 8 & 255), (int)(c & 255));
        }
    }

    private void Sparks(DrawingContext dc, double w, double h)
    {
        int n = h < 60 ? 5 : 20;
        for (int i = 0; i < n; i++)
        {
            double life = (_time * (0.22 + H(i, 21) * 0.36) + H(i, 22)) % 1;
            double x = H(i, 23) * w + Math.Sin(_time * 2.4 + i * 1.4 + life * 6) * (5 + life * 12);
            double r = (0.5 + H(i, 24) * 1.0) * (1 - life * 0.5);
            dc.PushOpacity((1 - life) * 0.9);
            dc.DrawEllipse(EmberBrush, null, new Point(x, h - life * h * 0.95), r, r);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ water: a rippling surface lit from above

    private void SimWater()
    {
        int w = _pw, h = _ph;
        // Raindrops and a wandering disturbance keep the surface alive; louder music, more of them.
        if (_rng.NextDouble() < 0.10 + _intensity * 0.35)
            Drop(1 + _rng.Next(w - 2), 1 + _rng.Next(h - 2), 2.2f + (float)_rng.NextDouble() * 2.5f);
        Drop((int)(w * (0.5 + 0.42 * Math.Sin(_time * 0.53))), (int)(h * (0.5 + 0.38 * Math.Cos(_time * 0.71))), 0.55f);

        // Classic two-buffer wave equation.
        for (int y = 1; y < h - 1; y++)
        {
            int row = y * w;
            for (int x = 1; x < w - 1; x++)
            {
                int i = row + x;
                _b[i] = ((_a[i - 1] + _a[i + 1] + _a[i - w] + _a[i + w]) * 0.5f - _b[i]) * 0.986f;
            }
        }
        (_a, _b) = (_b, _a);

        // Shade by the slope of the surface: facets tilted toward the light glint, the rest stays deep.
        int ar = _accent.R, ag = _accent.G, ab = _accent.B;
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            float depth = 0.55f + 0.45f * y / h;                   // a touch brighter toward the top
            for (int x = 0; x < w; x++)
            {
                int i = row + x;
                float sx = x > 0 && x < w - 1 ? _a[i - 1] - _a[i + 1] : 0;
                float sy = y > 0 && y < h - 1 ? _a[i - w] - _a[i + w] : 0;
                float light = Math.Clamp(0.42f + (sx * 0.9f + sy * 1.3f) * 0.55f, 0, 1);
                float spec = light > 0.78f ? (light - 0.78f) * 4.5f : 0;
                float r = 8 + 40 * light + ar * 0.12f, g = 46 + 120 * light + ag * 0.12f, b = 110 + 130 * light + ab * 0.12f;
                r = (r + (255 - r) * spec) * depth; g = (g + (255 - g) * spec) * (0.75f + 0.25f * depth); b += (255 - b) * spec;
                Put(i * 4, 205, (int)Math.Min(255, r), (int)Math.Min(255, g), (int)Math.Min(255, b));
            }
        }
    }

    private void Drop(int cx, int cy, float strength)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x > 0 && x < _pw - 1 && y > 0 && y < _ph - 1) _a[y * _pw + x] += strength * (dx == 0 && dy == 0 ? 1 : 0.5f);
            }
    }

    // ------------------------------------------------------------------ smooth noise (for aurora and mist)

    private static float Hash(int x, int y)
    {
        int n = x * 374761393 + y * 668265263;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    private static float Noise(float x, float y)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float top = Hash(xi, yi) + (Hash(xi + 1, yi) - Hash(xi, yi)) * fx;
        float bot = Hash(xi, yi + 1) + (Hash(xi + 1, yi + 1) - Hash(xi, yi + 1)) * fx;
        return top + (bot - top) * fy;
    }

    private static float Fbm(float x, float y) => Noise(x, y) * 0.55f + Noise(x * 2.1f, y * 2.1f) * 0.3f + Noise(x * 4.3f, y * 4.3f) * 0.15f;

    // ------------------------------------------------------------------ aurora: curtains of light

    private void SimAurora()
    {
        int w = _pw, h = _ph;
        float t = (float)_time, glow = 0.75f + (float)_intensity * 0.6f;
        for (int x = 0; x < w; x++)
        {
            // Each column is a ray: its brightness, how far it hangs down, and its colour drift over time.
            float u = x / (float)w * 5.2f;
            float fold = Noise(u * 0.7f + t * 0.06f, t * 0.05f) * 2.4f;                 // the curtain folds on itself
            float band = Fbm(u + fold, t * 0.11f);
            float rays = 0.6f + 0.4f * Noise(x * 0.9f + fold * 6, t * 0.5f);            // fine vertical striations
            float strength = MathF.Pow(Math.Clamp((band - 0.28f) * 1.9f, 0, 1), 1.6f) * rays * glow;
            float hang = 0.35f + 0.6f * Noise(u * 1.3f - t * 0.04f, 3.7f);              // lower edge of the curtain
            float hue = Noise(u * 0.5f + 9, t * 0.07f);

            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                // Sharp bright hem at the bottom of the curtain, long soft fade upward.
                float shape = v < hang ? MathF.Pow(v / hang, 2.2f) : MathF.Max(0, 1 - (v - hang) * 9);
                float a = Math.Clamp(strength * shape, 0, 1);
                float top = 1 - v / MathF.Max(hang, 0.01f);                              // 0 at the hem, 1 at the top
                // Green hem, shading to teal and violet higher up.
                float r = 40 + 150 * hue * Math.Clamp(top, 0, 1), g = 255 - 120 * Math.Clamp(top, 0, 1) * hue, b = 120 + 135 * Math.Clamp(top, 0, 1);
                Put((y * w + x) * 4, (int)(a * 215), (int)r, (int)g, (int)Math.Min(255, b));
            }
        }
    }

    // ------------------------------------------------------------------ mist / clouds

    private void SimMist()
    {
        int w = _pw, h = _ph;
        float t = (float)_time * (0.5f + (float)_intensity * 0.5f);
        int ar = (_accent.R + 510) / 3, ag = (_accent.G + 510) / 3, ab = (_accent.B + 510) / 3;   // accent, mostly white
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)h;
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)h;
                float warp = Noise(u * 1.3f + t * 0.05f, v * 1.3f) * 1.4f;
                float n = Fbm(u * 1.8f + t * 0.16f + warp, v * 2.6f + warp * 0.6f - t * 0.03f);
                float a = Math.Clamp((n - 0.42f) * 2.6f, 0, 1);
                a *= a;
                float shade = 0.7f + 0.3f * Noise(u * 3 + t * 0.2f, v * 3);               // soft self-shadowing
                Put((y * w + x) * 4, (int)(a * 170), (int)(ar * shade), (int)(ag * shade), (int)(ab * shade));
            }
        }
    }
}
