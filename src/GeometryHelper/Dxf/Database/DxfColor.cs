using System;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How a <see cref="DxfColor"/> gives its colour, numbered as AutoCAD's <c>ColorMethod</c>.
    /// </summary>
    public enum DxfColorMethod
    {
        /// <summary>The colour of the entity's layer, BYLAYER: the colour index 256.</summary>
        ByLayer = 192,

        /// <summary>The colour of the INSERT that places the block the entity is in, BYBLOCK: the colour index 0.</summary>
        ByBlock = 193,

        /// <summary>A colour of red, green and blue, a true colour (group 420).</summary>
        ByColor = 194,

        /// <summary>A colour of AutoCAD's index, 1 to 255 (group 62).</summary>
        ByAci = 195,

        /// <summary>A pen, which DXF does not write; as AutoCAD numbers it.</summary>
        ByPen = 196,

        /// <summary>The colour drawn in front of the screen's, black on white and white on black: the colour index 7.</summary>
        Foreground = 197,

        /// <summary>The colour of a layer turned off, as AutoCAD numbers it.</summary>
        LayerOff = 198,

        /// <summary>The colour of a layer frozen, as AutoCAD numbers it.</summary>
        LayerFrozen = 199,

        /// <summary>No colour.</summary>
        None = 200,
    }

    /// <summary>
    /// A colour of a DXF file, as AutoCAD's <c>Autodesk.AutoCAD.Colors.Color</c>: BYLAYER, BYBLOCK, a colour of AutoCAD's index
    /// (group 62), or one of red, green and blue (group 420), with the name of a colour book where the file gives one (430).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1)</c> is red, index 1, red 255, green 0 and blue 0;
    /// <c>DxfColor.FromRgb(255, 128, 0)</c> is the true colour 0xFF8000, its <see cref="ColorIndex"/> the index nearest it,
    /// 30. The red, green and blue of an index are those AutoCAD 2021 gives it: 7 is white, 12 is 0xCC0000, and the greys
    /// 250 to 255 run 0x333333, 0x5B5B5B, 0x848484, 0xADADAD, 0xD6D6D6 and white.
    /// </para>
    /// <para>
    /// A colour cannot be changed once made, and two colours are equal where their method, index, red, green, blue and name are.
    /// </para>
    /// </remarks>
    public sealed class DxfColor : IEquatable<DxfColor>
    {
        // AutoCAD's colours by index, 1 to 255, as AutoCAD 2021 gives them (Color.FromColorIndex(ColorMethod.ByAci, i).ColorValue,
        // the same as EntityColor.LookUpRgb); 0, BYBLOCK, has none and is held as black.
        private static readonly int[] Palette =
        {
            0x000000,
            0xFF0000,
            0xFFFF00,
            0x00FF00,
            0x00FFFF,
            0x0000FF,
            0xFF00FF,
            0xFFFFFF,
            0x808080,
            0xC0C0C0,
            0xFF0000,
            0xFF7F7F,
            0xCC0000,
            0xCC6666,
            0x990000,
            0x994C4C,
            0x7F0000,
            0x7F3F3F,
            0x4C0000,
            0x4C2626,
            0xFF3F00,
            0xFF9F7F,
            0xCC3300,
            0xCC7F66,
            0x992600,
            0x995F4C,
            0x7F1F00,
            0x7F4F3F,
            0x4C1300,
            0x4C2F26,
            0xFF7F00,
            0xFFBF7F,
            0xCC6600,
            0xCC9966,
            0x994C00,
            0x99724C,
            0x7F3F00,
            0x7F5F3F,
            0x4C2600,
            0x4C3926,
            0xFFBF00,
            0xFFDF7F,
            0xCC9900,
            0xCCB266,
            0x997200,
            0x99854C,
            0x7F5F00,
            0x7F6F3F,
            0x4C3900,
            0x4C4226,
            0xFFFF00,
            0xFFFF7F,
            0xCCCC00,
            0xCCCC66,
            0x999900,
            0x99994C,
            0x7F7F00,
            0x7F7F3F,
            0x4C4C00,
            0x4C4C26,
            0xBFFF00,
            0xDFFF7F,
            0x99CC00,
            0xB2CC66,
            0x729900,
            0x85994C,
            0x5F7F00,
            0x6F7F3F,
            0x394C00,
            0x424C26,
            0x7FFF00,
            0xBFFF7F,
            0x66CC00,
            0x99CC66,
            0x4C9900,
            0x72994C,
            0x3F7F00,
            0x5F7F3F,
            0x264C00,
            0x394C26,
            0x3FFF00,
            0x9FFF7F,
            0x33CC00,
            0x7FCC66,
            0x269900,
            0x5F994C,
            0x1F7F00,
            0x4F7F3F,
            0x134C00,
            0x2F4C26,
            0x00FF00,
            0x7FFF7F,
            0x00CC00,
            0x66CC66,
            0x009900,
            0x4C994C,
            0x007F00,
            0x3F7F3F,
            0x004C00,
            0x264C26,
            0x00FF3F,
            0x7FFF9F,
            0x00CC33,
            0x66CC7F,
            0x009926,
            0x4C995F,
            0x007F1F,
            0x3F7F4F,
            0x004C13,
            0x264C2F,
            0x00FF7F,
            0x7FFFBF,
            0x00CC66,
            0x66CC99,
            0x00994C,
            0x4C9972,
            0x007F3F,
            0x3F7F5F,
            0x004C26,
            0x264C39,
            0x00FFBF,
            0x7FFFDF,
            0x00CC99,
            0x66CCB2,
            0x009972,
            0x4C9985,
            0x007F5F,
            0x3F7F6F,
            0x004C39,
            0x264C42,
            0x00FFFF,
            0x7FFFFF,
            0x00CCCC,
            0x66CCCC,
            0x009999,
            0x4C9999,
            0x007F7F,
            0x3F7F7F,
            0x004C4C,
            0x264C4C,
            0x00BFFF,
            0x7FDFFF,
            0x0099CC,
            0x66B2CC,
            0x007299,
            0x4C8599,
            0x005F7F,
            0x3F6F7F,
            0x00394C,
            0x26424C,
            0x007FFF,
            0x7FBFFF,
            0x0066CC,
            0x6699CC,
            0x004C99,
            0x4C7299,
            0x003F7F,
            0x3F5F7F,
            0x00264C,
            0x26394C,
            0x003FFF,
            0x7F9FFF,
            0x0033CC,
            0x667FCC,
            0x002699,
            0x4C5F99,
            0x001F7F,
            0x3F4F7F,
            0x00134C,
            0x262F4C,
            0x0000FF,
            0x7F7FFF,
            0x0000CC,
            0x6666CC,
            0x000099,
            0x4C4C99,
            0x00007F,
            0x3F3F7F,
            0x00004C,
            0x26264C,
            0x3F00FF,
            0x9F7FFF,
            0x3300CC,
            0x7F66CC,
            0x260099,
            0x5F4C99,
            0x1F007F,
            0x4F3F7F,
            0x13004C,
            0x2F264C,
            0x7F00FF,
            0xBF7FFF,
            0x6600CC,
            0x9966CC,
            0x4C0099,
            0x724C99,
            0x3F007F,
            0x5F3F7F,
            0x26004C,
            0x39264C,
            0xBF00FF,
            0xDF7FFF,
            0x9900CC,
            0xB266CC,
            0x720099,
            0x854C99,
            0x5F007F,
            0x6F3F7F,
            0x39004C,
            0x42264C,
            0xFF00FF,
            0xFF7FFF,
            0xCC00CC,
            0xCC66CC,
            0x990099,
            0x994C99,
            0x7F007F,
            0x7F3F7F,
            0x4C004C,
            0x4C264C,
            0xFF00BF,
            0xFF7FDF,
            0xCC0099,
            0xCC66B2,
            0x990072,
            0x994C85,
            0x7F005F,
            0x7F3F6F,
            0x4C0039,
            0x4C2642,
            0xFF007F,
            0xFF7FBF,
            0xCC0066,
            0xCC6699,
            0x99004C,
            0x994C72,
            0x7F003F,
            0x7F3F5F,
            0x4C0026,
            0x4C2639,
            0xFF003F,
            0xFF7F9F,
            0xCC0033,
            0xCC667F,
            0x990026,
            0x994C5F,
            0x7F001F,
            0x7F3F4F,
            0x4C0013,
            0x4C262F,
            0x333333,
            0x5B5B5B,
            0x848484,
            0xADADAD,
            0xD6D6D6,
            0xFFFFFF,
        };

        private readonly int _rgb;

        private DxfColor(DxfColorMethod method, short index, int rgb, string name)
        {
            ColorMethod = method;
            ColorIndex = index;
            _rgb = rgb;
            ColorName = name;
        }

        /// <summary>Gets the colour BYLAYER, the colour index 256.</summary>
        public static DxfColor ByLayer { get; } = new DxfColor(DxfColorMethod.ByLayer, 256, 0, null);

        /// <summary>Gets the colour BYBLOCK, the colour index 0.</summary>
        public static DxfColor ByBlock { get; } = new DxfColor(DxfColorMethod.ByBlock, 0, 0, null);

        /// <summary>Gets how the colour is given, as AutoCAD's <c>ColorMethod</c>.</summary>
        public DxfColorMethod ColorMethod { get; }

        /// <summary>
        /// Gets the colour index, as AutoCAD's <c>ColorIndex</c>: 1 to 255 of a colour of the index, 256 for BYLAYER, 0 for
        /// BYBLOCK, 7 for the foreground; for a true colour the index nearest it, which AutoCAD writes beside it: 30 for orange,
        /// 0xFF8000, whether made by <see cref="FromRgb"/> or read from an entity or a layer.
        /// </summary>
        public short ColorIndex { get; }

        /// <summary>Gets the red of the colour, 0 to 255: its own for a true colour, the index's for one of the index, nought for BYLAYER and BYBLOCK.</summary>
        public byte Red => (byte)(_rgb >> 16);

        /// <summary>Gets the green of the colour, 0 to 255.</summary>
        public byte Green => (byte)(_rgb >> 8);

        /// <summary>Gets the blue of the colour, 0 to 255.</summary>
        public byte Blue => (byte)_rgb;

        /// <summary>
        /// Gets the colour as red, green and blue, 0xRRGGBB, as AutoCAD's <c>ColorValue</c> gives them: BYLAYER is white,
        /// 0xFFFFFF, while its <see cref="Red"/>, <see cref="Green"/> and <see cref="Blue"/> stay nought; BYBLOCK is black.
        /// </summary>
        public int ColorValue => IsByLayer ? 0xFFFFFF : _rgb;

        /// <summary>Gets the name of the colour in its colour book (group 430), as AutoCAD's <c>ColorName</c>; null where it has none.</summary>
        public string ColorName { get; }

        /// <summary>Gets whether the colour is BYLAYER, as AutoCAD's <c>IsByLayer</c>.</summary>
        public bool IsByLayer => ColorMethod == DxfColorMethod.ByLayer;

        /// <summary>Gets whether the colour is BYBLOCK, as AutoCAD's <c>IsByBlock</c>.</summary>
        public bool IsByBlock => ColorMethod == DxfColorMethod.ByBlock;

        /// <summary>Gets whether the colour is one of AutoCAD's index, as AutoCAD's <c>IsByAci</c>.</summary>
        public bool IsByAci => ColorMethod == DxfColorMethod.ByAci;

        /// <summary>Gets whether the colour is a true colour, as AutoCAD's <c>IsByColor</c>.</summary>
        public bool IsByColor => ColorMethod == DxfColorMethod.ByColor;

        /// <summary>Gets whether the colour is the foreground, as AutoCAD's <c>IsForeground</c>.</summary>
        public bool IsForeground => ColorMethod == DxfColorMethod.Foreground;

        /// <summary>Gets whether the colour is none, as AutoCAD's <c>IsNone</c>.</summary>
        public bool IsNone => ColorMethod == DxfColorMethod.None;

        /// <summary>
        /// Gets the colour of a method and an index, as AutoCAD's <c>FromColorIndex</c>: <see cref="DxfColorMethod.ByAci"/> with
        /// 1 to 255, <see cref="DxfColorMethod.ByLayer"/>, <see cref="DxfColorMethod.ByBlock"/>,
        /// <see cref="DxfColorMethod.Foreground"/> or <see cref="DxfColorMethod.None"/>, whose index is theirs whatever is given.
        /// </summary>
        /// <param name="colorMethod">The method.</param>
        /// <param name="colorIndex">The index, 1 to 255 for a colour of the index; 0 and 256 by the index give BYBLOCK and BYLAYER.</param>
        /// <returns>The colour.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside 0 to 256 for a colour of the index, or the method is one DXF does not write.</exception>
        public static DxfColor FromColorIndex(DxfColorMethod colorMethod, short colorIndex)
        {
            switch (colorMethod)
            {
                case DxfColorMethod.ByLayer:
                    return ByLayer;
                case DxfColorMethod.ByBlock:
                    return ByBlock;
                case DxfColorMethod.Foreground:
                    return new DxfColor(DxfColorMethod.Foreground, 7, Palette[7], null);
                case DxfColorMethod.None:
                    return new DxfColor(DxfColorMethod.None, 0, 0, null);
                case DxfColorMethod.ByAci:
                    if (colorIndex < 0 || colorIndex > 256)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(colorIndex),
                            colorIndex,
                            "A colour of AutoCAD's index runs from 1 to 255."
                        );
                    }

                    return OfIndex(colorIndex);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(colorMethod),
                        colorMethod,
                        "A colour of an index is by layer, by block, by the index, the foreground or none."
                    );
            }
        }

        /// <summary>
        /// Gets the true colour of a red, a green and a blue, as AutoCAD's <c>FromRgb</c>; its <see cref="ColorIndex"/> the
        /// index nearest it, the first of the nearest.
        /// </summary>
        /// <param name="red">The red, 0 to 255.</param>
        /// <param name="green">The green, 0 to 255.</param>
        /// <param name="blue">The blue, 0 to 255.</param>
        /// <returns>The colour.</returns>
        public static DxfColor FromRgb(byte red, byte green, byte blue) =>
            OfRgb((red << 16) | (green << 8) | blue, null);

        /// <summary>The colour of an index: BYBLOCK for 0, BYLAYER for 256 and beyond, the index's below; turned off, as a layer's below nought, the same.</summary>
        internal static DxfColor OfIndex(int index)
        {
            index = Math.Abs(index);

            if (index == 0)
            {
                return ByBlock;
            }

            if (index >= 256)
            {
                return ByLayer;
            }

            return new DxfColor(DxfColorMethod.ByAci, (short)index, Palette[index], null);
        }

        /// <summary>
        /// The true colour of 0xRRGGBB, with the name of its book where it has one, and the index a record writes with it (its
        /// 62), so that the colour read agrees with the record's index; the index nearest it where none is given.
        /// </summary>
        internal static DxfColor OfRgb(int rgb, string name, int? index = null)
        {
            rgb &= 0xFFFFFF;

            if (index.HasValue && Math.Abs(index.Value) <= 256)
            {
                return new DxfColor(DxfColorMethod.ByColor, (short)Math.Abs(index.Value), rgb, name);
            }

            int nearest = 1;
            long best = long.MaxValue;

            for (int i = 1; i <= 255; i++)
            {
                long dr = ((Palette[i] >> 16) & 0xFF) - ((rgb >> 16) & 0xFF);
                long dg = ((Palette[i] >> 8) & 0xFF) - ((rgb >> 8) & 0xFF);
                long db = (Palette[i] & 0xFF) - (rgb & 0xFF);
                long distance = dr * dr + dg * dg + db * db;

                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return new DxfColor(DxfColorMethod.ByColor, (short)nearest, rgb, name);
        }

        /// <summary>Gets whether this colour is another, by method, index, red, green, blue and name.</summary>
        /// <param name="other">The other colour.</param>
        /// <returns>Whether they are equal.</returns>
        public bool Equals(DxfColor other) =>
            other != null
            && ColorMethod == other.ColorMethod
            && ColorIndex == other.ColorIndex
            && _rgb == other._rgb
            && string.Equals(ColorName, other.ColorName, StringComparison.Ordinal);

        /// <summary>Gets whether this colour is another object, a colour equal to it.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether they are equal.</returns>
        public override bool Equals(object obj) => obj is DxfColor other && Equals(other);

        /// <summary>Gets a hash of the colour, the same for equal colours.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode() => unchecked(((int)ColorMethod * 397) ^ (ColorIndex * 31) ^ _rgb);

        /// <summary>Gives the colour as AutoCAD names it: <c>BYLAYER</c>, <c>BYBLOCK</c>, the index, or the red, green and blue.</summary>
        /// <returns>The colour as a line, such as <c>1</c> or <c>255,128,0</c>.</returns>
        public override string ToString()
        {
            switch (ColorMethod)
            {
                case DxfColorMethod.ByLayer:
                    return "BYLAYER";
                case DxfColorMethod.ByBlock:
                    return "BYBLOCK";
                case DxfColorMethod.ByColor:
                    return ColorName
                        ?? Red.ToString(CultureInfo.InvariantCulture)
                            + ","
                            + Green.ToString(CultureInfo.InvariantCulture)
                            + ","
                            + Blue.ToString(CultureInfo.InvariantCulture);
                case DxfColorMethod.None:
                    return "None";
                default:
                    return ColorIndex.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
