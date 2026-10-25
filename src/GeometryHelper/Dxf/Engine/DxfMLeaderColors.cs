using System;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A colour as AutoCAD packs one into a whole number of a record (an <c>AcCmEntityColor</c>): its method in the top byte,
    /// 0xC0 BYLAYER, 0xC1 BYBLOCK, 0xC2 a true colour with its red, green and blue below, 0xC3 a colour of the index with the
    /// index below, 0xC5 the foreground and 0xC8 none. A MULTILEADER writes its colours so (groups 91, 92 and 93 and those of
    /// its context), and a HATCH its background (1071 of HATCHBACKGROUNDCOLOR): -1056964608, 0xC1000000, is BYBLOCK.
    /// </summary>
    internal static class DxfMLeaderColors
    {
        private const int ByLayer = unchecked((int)0xC0000000);
        private const int ByBlock = unchecked((int)0xC1000000);
        private const int ByColor = unchecked((int)0xC2000000);
        private const int ByAci = unchecked((int)0xC3000000);
        private const int Foreground = unchecked((int)0xC5000000);
        private const int None = unchecked((int)0xC8000000);

        /// <summary>The colour of a packed number; BYBLOCK for one not read as a whole number.</summary>
        internal static DxfColor Of(string text)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw) ? Of(raw) : DxfColor.ByBlock;

        /// <summary>The colour of a packed number: by its method, a true colour with its index the nearest, a colour of the index with its own.</summary>
        internal static DxfColor Of(int raw)
        {
            switch ((raw >> 24) & 0xFF)
            {
                case 0xC0: return DxfColor.ByLayer;
                case 0xC1: return DxfColor.ByBlock;
                case 0xC2: return DxfColor.OfRgb(raw & 0xFFFFFF, null);
                case 0xC3: return DxfColor.OfIndex(Math.Max(1, Math.Min(255, raw & 0xFFFF)));
                case 0xC5: return DxfColor.FromColorIndex(DxfColorMethod.Foreground, 7);
                case 0xC8: return DxfColor.FromColorIndex(DxfColorMethod.None, 0);
                default: return DxfColor.ByBlock;
            }
        }

        /// <summary>The packed number of a colour, as AutoCAD writes it.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is by pen, or a layer's off or frozen, which a record does not hold.</exception>
        internal static int Raw(DxfColor color, string parameter)
        {
            if (color == null)
            {
                throw new ArgumentNullException(parameter);
            }

            switch (color.ColorMethod)
            {
                case DxfColorMethod.ByLayer: return ByLayer;
                case DxfColorMethod.ByBlock: return ByBlock;
                case DxfColorMethod.ByColor: return ByColor | (color.ColorValue & 0xFFFFFF);
                case DxfColorMethod.ByAci: return ByAci | (color.ColorIndex & 0xFF);
                case DxfColorMethod.Foreground: return Foreground;
                case DxfColorMethod.None: return None;
                default: throw new ArgumentException("A colour of a record is by layer, by block, a true colour, one of the index, the foreground or none: " + color.ColorMethod + " is none of them.", parameter);
            }
        }

        /// <summary>The text of the packed number of a colour.</summary>
        internal static string Text(DxfColor color, string parameter) => Raw(color, parameter).ToString(CultureInfo.InvariantCulture);
    }
}
