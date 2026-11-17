using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// How an underlay's colours are turned for display, which depends on the background they are drawn on and so is done where
    /// the item is painted: the colours adjusted for the background (a dark background shows a PDF's black as white and its white as
    /// black, the lightness of every colour turned over and its hue kept), monochrome (grey), the contrast (the colour taken
    /// towards the background as it falls below 100) and the fade (taken towards the background as it grows above 0). The items an
    /// underlay draws share the look through their style.
    /// </summary>
    internal sealed class DisplayUnderlayLook
    {
        /// <summary>Makes the look of an underlay.</summary>
        /// <param name="fade">The fade, 0 to 100.</param>
        /// <param name="contrast">The contrast, 0 to 100, 100 as it is.</param>
        /// <param name="monochrome">Whether the colours are made grey.</param>
        /// <param name="adjust">Whether the colours are adjusted for the background.</param>
        public DisplayUnderlayLook(int fade, int contrast, bool monochrome, bool adjust)
        {
            Fade = Math.Max(0, Math.Min(100, fade));
            Contrast = Math.Max(0, Math.Min(100, contrast));
            Monochrome = monochrome;
            AdjustForBackground = adjust;
        }

        public int Fade { get; }

        public int Contrast { get; }

        public bool Monochrome { get; }

        public bool AdjustForBackground { get; }

        /// <summary>Whether the look changes no colour at all.</summary>
        public bool IsPlain => Fade == 0 && Contrast == 100 && !Monochrome && !AdjustForBackground;

        /// <summary>Gives the colour a PDF colour is shown in on a background, both as 0xRRGGBB.</summary>
        public int Apply(int rgb, int background)
        {
            double r = ((rgb >> 16) & 0xFF) / 255.0;
            double g = ((rgb >> 8) & 0xFF) / 255.0;
            double b = (rgb & 0xFF) / 255.0;
            double br = ((background >> 16) & 0xFF) / 255.0;
            double bg = ((background >> 8) & 0xFF) / 255.0;
            double bb = (background & 0xFF) / 255.0;

            if (AdjustForBackground && Luminance(br, bg, bb) < 0.5)
            {
                Invert(ref r, ref g, ref b);
            }

            if (Monochrome)
            {
                double grey = Luminance(r, g, b);
                r = g = b = grey;
            }

            double keep = Contrast / 100.0;
            r = br + (r - br) * keep;
            g = bg + (g - bg) * keep;
            b = bb + (b - bb) * keep;
            double fade = Fade / 100.0;
            r += (br - r) * fade;
            g += (bg - g) * fade;
            b += (bb - b) * fade;
            return (Byte(r) << 16) | (Byte(g) << 8) | Byte(b);
        }

        private static int Byte(double v) => (int)Math.Max(0.0, Math.Min(255.0, Math.Floor(v * 255.0 + 0.5)));

        private static double Luminance(double r, double g, double b) => 0.299 * r + 0.587 * g + 0.114 * b;

        /// <summary>Turns the lightness of a colour over, keeping its hue and its saturation: black to white, white to black, a pure red still red.</summary>
        private static void Invert(ref double r, ref double g, ref double b)
        {
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2.0;
            double d = max - min;

            if (d < 1E-9)
            {
                r = g = b = 1.0 - l;
                return;
            }

            double s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            double h;

            if (max == r)
            {
                h = (g - b) / d + (g < b ? 6.0 : 0.0);
            }
            else if (max == g)
            {
                h = (b - r) / d + 2.0;
            }
            else
            {
                h = (r - g) / d + 4.0;
            }

            h /= 6.0;
            double lightness = 1.0 - l;
            double q = lightness < 0.5 ? lightness * (1.0 + s) : lightness + s - lightness * s;
            double p = 2.0 * lightness - q;
            r = Channel(p, q, h + 1.0 / 3.0);
            g = Channel(p, q, h);
            b = Channel(p, q, h - 1.0 / 3.0);
        }

        private static double Channel(double p, double q, double t)
        {
            if (t < 0.0)
            {
                t += 1.0;
            }

            if (t > 1.0)
            {
                t -= 1.0;
            }

            if (t < 1.0 / 6.0)
            {
                return p + (q - p) * 6.0 * t;
            }

            if (t < 0.5)
            {
                return q;
            }

            return t < 2.0 / 3.0 ? p + (q - p) * (2.0 / 3.0 - t) * 6.0 : p;
        }
    }
}
