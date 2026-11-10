using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A sheet of paper a plot is laid on, as the Paper size list of AutoCAD's Plot dialog offers it: its name, its size in
    /// millimetres as the name gives it, width then height, and the margins its plotter cannot print in. ISO A3 is 420 by 297
    /// with margins of 5 all round, a printable area of 410 by 287.
    /// </summary>
    public sealed class CadPaper
    {
        /// <summary>Makes a sheet.</summary>
        /// <param name="name">The name shown, as "ISO A3".</param>
        /// <param name="width">The width in millimetres, as the name gives it.</param>
        /// <param name="height">The height in millimetres.</param>
        /// <param name="marginLeft">The margin at the left, in millimetres.</param>
        /// <param name="marginBottom">The margin at the bottom.</param>
        /// <param name="marginRight">The margin at the right.</param>
        /// <param name="marginTop">The margin at the top.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is blank, a size is not a finite number above nought, a
        /// margin is not a finite number of nought or more, or the margins leave no room on the sheet.</exception>
        public CadPaper(string name, double width, double height, double marginLeft, double marginBottom, double marginRight, double marginTop)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Trim().Length == 0)
            {
                throw new ArgumentException("A sheet has a name.", nameof(name));
            }

            if (!(width > 0.0) || !(height > 0.0) || double.IsInfinity(width) || double.IsInfinity(height))
            {
                throw new ArgumentException("A sheet is of finite sizes above nought: " + width.ToString(CultureInfo.InvariantCulture) + " by " + height.ToString(CultureInfo.InvariantCulture) + ".", nameof(width));
            }

            foreach (double margin in new[] { marginLeft, marginBottom, marginRight, marginTop })
            {
                if (!(margin >= 0.0) || double.IsInfinity(margin))
                {
                    throw new ArgumentException("A margin is a finite number of millimetres, nought or more.", nameof(marginLeft));
                }
            }

            if (!(marginLeft + marginRight < width) || !(marginBottom + marginTop < height))
            {
                throw new ArgumentException("The margins leave no room on a sheet " + width.ToString(CultureInfo.InvariantCulture) + " by " + height.ToString(CultureInfo.InvariantCulture) + ".", nameof(marginLeft));
            }

            Name = name.Trim();
            Width = width;
            Height = height;
            MarginLeft = marginLeft;
            MarginBottom = marginBottom;
            MarginRight = marginRight;
            MarginTop = marginTop;
        }

        /// <summary>The fewest pixels a side of an image may have: 16.</summary>
        public const int MinPixels = 16;

        /// <summary>The most pixels a side of an image may have: 8 000.</summary>
        public const int MaxPixels = 8000;

        // The words after the size of an image in the list, "Full HD"; null for a sheet in millimetres.
        private readonly string _note;

        /// <summary>A size of an image: its sides in pixels, no margins.</summary>
        private CadPaper(int width, int height, string note)
            : this(width.ToString(CultureInfo.InvariantCulture) + " x " + height.ToString(CultureInfo.InvariantCulture) + " Pixels", width, height, 0.0, 0.0, 0.0, 0.0)
        {
            IsPixels = true;
            _note = note;
        }

        /// <summary>Gets the name shown, as "ISO A3", or for an image "1920 x 1080 Pixels".</summary>
        public string Name { get; }

        /// <summary>
        /// Gets whether this is the size of an image for the raster devices, GeometryHelper PNG and JPG: its width and height
        /// are then pixels rather than millimetres, and it has no margins, the whole image being printable.
        /// </summary>
        public bool IsPixels { get; }

        /// <summary>Gets the width in millimetres, as the name gives it: 420 for ISO A3.</summary>
        public double Width { get; }

        /// <summary>Gets the height in millimetres: 297 for ISO A3.</summary>
        public double Height { get; }

        /// <summary>Gets the margin at the left of the sheet as named, in millimetres.</summary>
        public double MarginLeft { get; }

        /// <summary>Gets the margin at the bottom, in millimetres.</summary>
        public double MarginBottom { get; }

        /// <summary>Gets the margin at the right, in millimetres.</summary>
        public double MarginRight { get; }

        /// <summary>Gets the margin at the top, in millimetres.</summary>
        public double MarginTop { get; }

        /// <summary>Gets the name as the Paper size list shows it: <c>ISO A3 (420.00 x 297.00 MM)</c>, or for an image <c>1920 x 1080 Pixels (Full HD)</c>.</summary>
        public string Label => _custom ? "Custom…" : IsPixels ? Name + " (" + _note + ")" : Name + " (" + Mm(Width) + " x " + Mm(Height) + " MM)";

        /// <summary>
        /// Gets the name as a DXF keeps it in a layout's group 4, AutoCAD's <c>CanonicalMediaName</c>: the name's spaces as
        /// underscores, then the size, <c>ISO_A3_(420.00_x_297.00_MM)</c>. An image is named the same way,
        /// <c>1920_x_1080_Pixels_(1920.00_x_1080.00_MM)</c>, the DXF layer taking a name that ends in a size alone; the layout
        /// keeps its units as pixels (group 72 = 2), which <see cref="CadPlotSettings.Read"/> reads back as the image's size.
        /// </summary>
        public string CanonicalName => Name.Replace(' ', '_') + "_(" + Mm(Width) + "_x_" + Mm(Height) + "_MM)";

        /// <summary>
        /// Gets the sizes of an image the raster devices offer, in the order of the list: 1280 by 720 (HD), 1600 by 1200
        /// (UXGA), 1920 by 1080 (Full HD), 2560 by 1440 (QHD) and 3840 by 2160 (4K UHD).
        /// </summary>
        public static IReadOnlyList<CadPaper> PixelSizes { get; } = Array.AsReadOnly(new[]
        {
            new CadPaper(1280, 720, "HD"),
            new CadPaper(1600, 1200, "UXGA"),
            new CadPaper(1920, 1080, "Full HD"),
            new CadPaper(2560, 1440, "QHD"),
            new CadPaper(3840, 2160, "4K UHD"),
        });

        /// <summary>Gets the size of an image a raster device starts on: 1920 by 1080 pixels, Full HD.</summary>
        public static CadPaper DefaultPixels => PixelSizes[2];

        /// <summary>Gets the last entry of the Paper size list of a raster device, "Custom…", which asks for a width and a height; it is no size to plot on.</summary>
        public static CadPaper CustomPixelsChoice { get; } = new CadPaper(MinPixels, MinPixels, null) { _custom = true };

        // Whether this is the Custom… entry.
        private bool _custom;

        /// <summary>Gets whether this is <see cref="CustomPixelsChoice"/>.</summary>
        public bool IsCustomChoice => _custom;

        /// <summary>
        /// Makes a size of an image of the user's own, as Custom Image Size makes it: <c>2480 x 1754 Pixels (Custom)</c>.
        /// </summary>
        /// <param name="width">The width in pixels, <see cref="MinPixels"/> to <see cref="MaxPixels"/>.</param>
        /// <param name="height">The height in pixels, as the width.</param>
        /// <returns>The size: the listed one where it is one of <see cref="PixelSizes"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a side is outside 16 to 8 000.</exception>
        public static CadPaper Pixels(int width, int height)
        {
            if (width < MinPixels || width > MaxPixels)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A side of an image is 16 to 8 000 pixels.");
            }

            if (height < MinPixels || height > MaxPixels)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "A side of an image is 16 to 8 000 pixels.");
            }

            foreach (CadPaper listed in PixelSizes)
            {
                if ((int)listed.Width == width && (int)listed.Height == height)
                {
                    return listed;
                }
            }

            return new CadPaper(width, height, "Custom");
        }

        /// <summary>
        /// Gets the sheets the built-in PDF plotter offers, in the order of the list: ISO A0 to A4 (1 189 by 841 to 297 by 210)
        /// and ANSI A to E (279.4 by 215.9 to 1 117.6 by 863.6), each on its side, with margins of 5 all round.
        /// </summary>
        public static IReadOnlyList<CadPaper> Standard { get; } = Array.AsReadOnly(new[]
        {
            Sheet("ISO A0", 1189.0, 841.0),
            Sheet("ISO A1", 841.0, 594.0),
            Sheet("ISO A2", 594.0, 420.0),
            Sheet("ISO A3", 420.0, 297.0),
            Sheet("ISO A4", 297.0, 210.0),
            Sheet("ANSI A", 279.4, 215.9),
            Sheet("ANSI B", 431.8, 279.4),
            Sheet("ANSI C", 558.8, 431.8),
            Sheet("ANSI D", 863.6, 558.8),
            Sheet("ANSI E", 1117.6, 863.6),
        });

        /// <summary>Gets the sheet of a plot when nothing else is given: ISO A3.</summary>
        public static CadPaper Default => Standard[3];

        /// <summary>
        /// Gets whether another sheet is this one: the same name, letters of either case the same, and the same size and margins
        /// to a thousandth of a millimetre.
        /// </summary>
        /// <param name="other">The other sheet.</param>
        /// <returns>True when it is.</returns>
        public bool IsSameAs(CadPaper other)
        {
            return other != null && IsPixels == other.IsPixels && _custom == other._custom && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
                && Near(Width, other.Width) && Near(Height, other.Height) && Near(MarginLeft, other.MarginLeft)
                && Near(MarginBottom, other.MarginBottom) && Near(MarginRight, other.MarginRight) && Near(MarginTop, other.MarginTop);
        }

        /// <inheritdoc/>
        public override string ToString() => Label;

        /// <summary>
        /// The sheet a layout names: the standard one of its name and margins, else one made of what the layout says, its name
        /// read off the group 4 it keeps (<c>ISO_full_bleed_A3_(420.00_x_297.00_MM)</c> as "ISO full bleed A3"); null where the
        /// layout gives no size.
        /// </summary>
        internal static CadPaper FromLayout(string canonical, double width, double height, double left, double bottom, double right, double top)
        {
            if (!(width > 0.0) || !(height > 0.0))
            {
                return null;
            }

            string name = canonical ?? string.Empty;
            int bracket = name.LastIndexOf("_(", StringComparison.Ordinal);
            name = (bracket > 0 ? name.Substring(0, bracket) : name).Replace('_', ' ').Trim();

            try
            {
                var paper = new CadPaper(name.Length == 0 ? "Custom" : name, width, height, left, bottom, right, top);

                foreach (CadPaper standard in Standard)
                {
                    if (standard.IsSameAs(paper))
                    {
                        return standard;
                    }
                }

                return paper;
            }
            catch (ArgumentException)
            {
                // Margins that leave no room, as a layout of another plotter may give: the sheet's own margins of 5.
                return width > 10.0 && height > 10.0 ? new CadPaper(name.Length == 0 ? "Custom" : name, width, height, 5.0, 5.0, 5.0, 5.0) : null;
            }
        }

        private static CadPaper Sheet(string name, double width, double height) => new CadPaper(name, width, height, 5.0, 5.0, 5.0, 5.0);

        private static string Mm(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private static bool Near(double a, double b) => Math.Abs(a - b) <= 1E-3;
    }
}
