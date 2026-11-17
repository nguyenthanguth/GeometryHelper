using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A colour space of a PDF, reduced to what a drawing needs: how many numbers a colour has and which red, green and blue it
    /// is. Device spaces, the calibrated ones (taken as their device kin), ICC profiles (by their number of components),
    /// Indexed, Separation and DeviceN (through their tint transform) are read. A Pattern has no colour of its own.
    /// </summary>
    internal abstract class PdfColorSpace
    {
        /// <summary>Gets how many numbers a colour of the space has.</summary>
        public abstract int Components { get; }

        /// <summary>Gets the colour of a new paint in the space: black, or the full tint of a spot.</summary>
        public virtual double[] Initial => new double[Components];

        /// <summary>Gets whether the space is a Pattern.</summary>
        public virtual bool IsPattern => false;

        /// <summary>Gets whether the space is an Indexed one, whose samples are positions in a table and not 0..1 amounts.</summary>
        public virtual bool IsIndexed => false;

        /// <summary>Gives the red, green and blue of a colour as 0xRRGGBB.</summary>
        public abstract int ToRgb(double[] colour);

        /// <summary>Packs 0..1 red, green and blue into 0xRRGGBB.</summary>
        public static int Pack(double r, double g, double b) => (Byte(r) << 16) | (Byte(g) << 8) | Byte(b);

        private static int Byte(double v) => (int)Math.Max(0.0, Math.Min(255.0, Math.Floor(v * 255.0 + 0.5)));

        public static readonly PdfColorSpace Gray = new GraySpace();
        public static readonly PdfColorSpace Rgb = new RgbSpace();
        public static readonly PdfColorSpace Cmyk = new CmykSpace();

        /// <summary>Reads a colour space object, by name from the resources or in full; the device grey where it cannot be read.</summary>
        public static PdfColorSpace Parse(PdfFile file, object spec, PdfDict resources, int depth = 0)
        {
            if (depth > 8)
            {
                return Gray;
            }

            object resolved = file.Resolve(spec);

            if (resolved is PdfName name)
            {
                switch (name.Value)
                {
                    case "DeviceGray":
                    case "G":
                    case "CalGray":
                        return Gray;
                    case "DeviceRGB":
                    case "RGB":
                    case "CalRGB":
                        return Rgb;
                    case "DeviceCMYK":
                    case "CMYK":
                        return Cmyk;
                    case "Pattern":
                        return new PatternSpace();
                }

                PdfDict spaces = file.Dict(file.Get(resources, "ColorSpace"));
                object named = spaces != null && spaces.TryGetValue(name.Value, out object found) ? found : null;
                return named == null ? Gray : Parse(file, named, resources, depth + 1);
            }

            if (!(resolved is List<object> array) || array.Count == 0)
            {
                return Gray;
            }

            switch (file.Name(array[0]))
            {
                case "DeviceGray":
                case "G":
                case "CalGray":
                    return Gray;
                case "DeviceRGB":
                case "RGB":
                case "CalRGB":
                    return Rgb;
                case "DeviceCMYK":
                case "CMYK":
                    return Cmyk;
                case "Lab":
                    return array.Count > 1 ? LabSpace.Make(file, file.Dict(array[1])) : Rgb;
                case "ICCBased":
                {
                    PdfDict profile = array.Count > 1 ? file.Dict(array[1]) : null;
                    int n = (int)file.Number(file.Get(profile, "N"), 3);
                    return n == 1 ? Gray
                        : n == 4 ? Cmyk
                        : Rgb;
                }
                case "Indexed":
                case "I":
                    return array.Count >= 4 ? IndexedSpace.Make(file, array, resources, depth) : Gray;
                case "Separation":
                    return array.Count >= 4 ? TintSpace.Make(file, array[2], array[3], 1, resources, depth) : Gray;
                case "DeviceN":
                {
                    List<object> names = array.Count > 1 ? file.Array(array[1]) : null;
                    return array.Count >= 4 && names != null
                        ? TintSpace.Make(file, array[2], array[3], names.Count, resources, depth)
                        : Gray;
                }
                case "Pattern":
                    return new PatternSpace();
                default:
                    return Gray;
            }
        }

        private sealed class GraySpace : PdfColorSpace
        {
            public override int Components => 1;

            public override int ToRgb(double[] c)
            {
                double g = c.Length > 0 ? c[0] : 0.0;
                return Pack(g, g, g);
            }
        }

        private sealed class RgbSpace : PdfColorSpace
        {
            public override int Components => 3;

            public override int ToRgb(double[] c) => c.Length >= 3 ? Pack(c[0], c[1], c[2]) : 0;
        }

        private sealed class CmykSpace : PdfColorSpace
        {
            public override int Components => 4;

            public override double[] Initial => new[] { 0.0, 0.0, 0.0, 1.0 };

            public override int ToRgb(double[] c)
            {
                if (c.Length < 4)
                {
                    return 0;
                }

                double k = 1.0 - Math.Max(0.0, Math.Min(1.0, c[3]));
                return Pack(
                    (1.0 - Math.Max(0.0, Math.Min(1.0, c[0]))) * k,
                    (1.0 - Math.Max(0.0, Math.Min(1.0, c[1]))) * k,
                    (1.0 - Math.Max(0.0, Math.Min(1.0, c[2]))) * k
                );
            }
        }

        private sealed class PatternSpace : PdfColorSpace
        {
            public override int Components => 0;

            public override bool IsPattern => true;

            public override int ToRgb(double[] c) => 0x808080;
        }

        private sealed class LabSpace : PdfColorSpace
        {
            private double[] _white = { 0.9505, 1.0, 1.089 };
            private double[] _range = { -100, 100, -100, 100 };

            public static LabSpace Make(PdfFile file, PdfDict dictionary)
            {
                var lab = new LabSpace();
                double[] white = file.Numbers(file.Get(dictionary, "WhitePoint"));
                double[] range = file.Numbers(file.Get(dictionary, "Range"));

                if (white != null && white.Length >= 3)
                {
                    lab._white = white;
                }

                if (range != null && range.Length >= 4)
                {
                    lab._range = range;
                }

                return lab;
            }

            public override int Components => 3;

            public override int ToRgb(double[] c)
            {
                if (c.Length < 3)
                {
                    return 0;
                }

                double l = (Math.Max(0.0, Math.Min(100.0, c[0])) + 16.0) / 116.0;
                double m = l + Math.Max(_range[0], Math.Min(_range[1], c[1])) / 500.0;
                double n = l - Math.Max(_range[2], Math.Min(_range[3], c[2])) / 200.0;
                double x = _white[0] * G(m);
                double y = _white[1] * G(l);
                double z = _white[2] * G(n);
                double r = 3.2406 * x - 1.5372 * y - 0.4986 * z;
                double g = -0.9689 * x + 1.8758 * y + 0.0415 * z;
                double b = 0.0557 * x - 0.2040 * y + 1.0570 * z;
                return Pack(Gamma(r), Gamma(g), Gamma(b));
            }

            private static double G(double x) => x >= 6.0 / 29.0 ? x * x * x : 108.0 / 841.0 * (x - 4.0 / 29.0);

            private static double Gamma(double v)
            {
                v = Math.Max(0.0, Math.Min(1.0, v));
                return v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
            }
        }

        private sealed class IndexedSpace : PdfColorSpace
        {
            private PdfColorSpace _base;
            private int _high;
            private byte[] _table;

            public static IndexedSpace Make(PdfFile file, List<object> array, PdfDict resources, int depth)
            {
                PdfColorSpace baseSpace = Parse(file, array[1], resources, depth + 1);
                int high = (int)file.Number(array[2]);
                object lookup = file.Resolve(array[3]);
                byte[] table =
                    lookup is PdfString s ? s.Bytes
                    : lookup is PdfStream stream ? file.Decode(stream)
                    : new byte[0];
                return new IndexedSpace
                {
                    _base = baseSpace,
                    _high = Math.Max(0, Math.Min(255, high)),
                    _table = table,
                };
            }

            public override int Components => 1;

            public override bool IsIndexed => true;

            public override int ToRgb(double[] c)
            {
                int index = Math.Max(0, Math.Min(_high, (int)Math.Floor((c.Length > 0 ? c[0] : 0.0) + 0.5)));
                int n = _base.Components;
                var base1 = new double[n];

                for (int i = 0; i < n; i++)
                {
                    int at = index * n + i;
                    base1[i] = at < _table.Length ? _table[at] / 255.0 : 0.0;
                }

                // A Lab base is scaled by its range, not by 255; the device kin are 0..1.
                return _base.ToRgb(base1);
            }
        }

        private sealed class TintSpace : PdfColorSpace
        {
            private PdfColorSpace _alternate;
            private PdfFunction _function;
            private int _n;

            public static TintSpace Make(
                PdfFile file,
                object alternate,
                object function,
                int n,
                PdfDict resources,
                int depth
            )
            {
                return new TintSpace
                {
                    _alternate = Parse(file, alternate, resources, depth + 1),
                    _function = PdfFunction.Parse(file, function),
                    _n = Math.Max(1, n),
                };
            }

            public override int Components => _n;

            public override double[] Initial
            {
                get
                {
                    var tint = new double[_n];

                    for (int i = 0; i < tint.Length; i++)
                    {
                        tint[i] = 1.0;
                    }

                    return tint;
                }
            }

            public override int ToRgb(double[] c)
            {
                if (_function == null)
                {
                    // No tint transform to read: a tint of 1 is black, of 0 is white, as a grey spot is.
                    double t = c.Length > 0 ? Math.Max(0.0, Math.Min(1.0, c[0])) : 0.0;
                    return Pack(1.0 - t, 1.0 - t, 1.0 - t);
                }

                return _alternate.ToRgb(_function.Evaluate(c));
            }
        }
    }
}
