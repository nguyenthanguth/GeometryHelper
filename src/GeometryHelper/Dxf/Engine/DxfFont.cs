namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A font the words of a drawing are measured in, an SHX font (<see cref="DxfShxFont"/>) or a TrueType one
    /// (<see cref="DxfTrueTypeFont"/>): the height a text's height stands for, the depth below the baseline a text held at its
    /// bottom stands on, and each letter's advance and ink, in the font's units. txt.shx is 6 high and 2 deep, its A inked from
    /// 0 to 4 and moving the pen 6.
    /// </summary>
    internal abstract class DxfFont
    {
        /// <summary>The height, in the font's units, that a text's height stands for: how far its capitals reach above the baseline.</summary>
        internal abstract double Above { get; }

        /// <summary>The depth, in the font's units, below the baseline that a text held at its bottom stands on; NaN where the font gives none.</summary>
        internal abstract double Below { get; }

        /// <summary>The letter of a number, read once; null where the font has none.</summary>
        internal abstract Glyph GlyphOf(int number);

        /// <summary>A letter as it is drawn: the pen's last x, and the points of its ink, in the font's units.</summary>
        internal sealed class Glyph
        {
            internal Glyph(double advance, double[] xs, double[] ys, bool inksOrigin = false, bool isWhole = true, double penBottom = 0.0, double penTop = 0.0)
            {
                Advance = advance;
                Xs = xs;
                Ys = ys;
                InksOrigin = inksOrigin;
                IsWhole = isWhole;
                PenBottom = penBottom;
                PenTop = penTop;
            }

            /// <summary>How far the letter moves the pen along x: its last x.</summary>
            internal double Advance { get; }

            /// <summary>The x of each point of the letter's ink.</summary>
            internal double[] Xs { get; }

            /// <summary>The y of each point of the letter's ink.</summary>
            internal double[] Ys { get; }

            /// <summary>Whether the letter leaves any ink.</summary>
            internal bool HasInk => Xs.Length > 0;

            /// <summary>Whether the letter puts its pen down where it starts before it moves it, as txt's A does.</summary>
            internal bool InksOrigin { get; }

            /// <summary>Whether the letter is drawn whole: false for a letter of a big font that draws a shape the font has not, or an arc in a shape it stretches, which AutoCAD 2021 draws otherwise.</summary>
            internal bool IsWhole { get; }

            /// <summary>The least y the pen stands at, its ink or not, from nought down: gbcbig.shx's 中 -5 times 9/102.</summary>
            internal double PenBottom { get; }

            /// <summary>The greatest y the pen stands at, its ink or not, from nought up.</summary>
            internal double PenTop { get; }
        }
    }
}
