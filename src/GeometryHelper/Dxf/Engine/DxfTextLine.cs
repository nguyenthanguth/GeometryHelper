using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A line of words measured in an SHX font, along its own x from the start of its baseline, in the drawing's units and
    /// upright: the pen's last place and the box of the ink, each letter's points placed as AutoCAD 2021 places them (check
    /// F1a), x the pen plus the width factor times the scale times the point's x, y the scale times its y, the scale the
    /// height over the font's height above its baseline. txt "ABC" 4 high is inked from 0 to 10.667 and moves the pen 12.
    /// </summary>
    internal sealed class DxfTextLine
    {
        private readonly DxfShxFont _font;

        // The ink up to the last letter that is no space, for the width of an MTEXT, which leaves the spaces after it out.
        private bool _wordsInked;
        private double _wordsLeft;
        private double _wordsRight;

        internal DxfTextLine(DxfShxFont font)
        {
            _font = font;
        }

        /// <summary>The pen's place after the last letter.</summary>
        internal double Advance { get; private set; }

        /// <summary>Whether any letter leaves ink.</summary>
        internal bool HasInk { get; private set; }

        /// <summary>The least x of the ink.</summary>
        internal double InkLeft { get; private set; }

        /// <summary>The greatest x of the ink.</summary>
        internal double InkRight { get; private set; }

        /// <summary>The least y of the ink.</summary>
        internal double InkBottom { get; private set; }

        /// <summary>The greatest y of the ink.</summary>
        internal double InkTop { get; private set; }

        /// <summary>The greatest height of the letters, that of the line where no code changes it.</summary>
        internal double Height { get; private set; }

        /// <summary>How many letters the line holds.</summary>
        internal int Count { get; private set; }

        /// <summary>Whether the first letter is a space (U+0020).</summary>
        internal bool StartsWithSpace { get; private set; }

        /// <summary>Whether the last letter is a space (U+0020).</summary>
        internal bool EndsWithSpace { get; private set; }

        /// <summary>Whether every letter is a space (U+0020).</summary>
        internal bool AllSpaces { get; private set; } = true;

        /// <summary>
        /// Adds a letter of a number at a width factor and a height, the font's question mark where it has no letter of it, as
        /// a TEXT draws one; strict, false where it has none instead, for a letter another font would draw: a big font beside
        /// it, or the font an MTEXT draws such a letter from.
        /// </summary>
        internal bool Add(int number, double widthFactor, double height, bool strict)
        {
            DxfShxFont.Glyph glyph = _font.GlyphOf(number);

            if (glyph == null)
            {
                if (strict)
                {
                    return false;
                }

                glyph = _font.GlyphOf('?');
            }

            double scale = height / _font.Above;
            double across = scale * widthFactor;
            Height = Count == 0 ? height : Math.Max(Height, height);
            StartsWithSpace = Count == 0 ? number == ' ' : StartsWithSpace;
            EndsWithSpace = number == ' ';
            AllSpaces &= EndsWithSpace;
            Count++;

            if (glyph == null)
            {
                return true;
            }

            for (int k = 0; k < glyph.Xs.Length; k++)
            {
                double x = Advance + (across * glyph.Xs[k]);
                double y = scale * glyph.Ys[k];

                if (!HasInk)
                {
                    InkLeft = InkRight = x;
                    InkBottom = InkTop = y;
                    HasInk = true;
                }
                else
                {
                    InkLeft = Math.Min(InkLeft, x);
                    InkRight = Math.Max(InkRight, x);
                    InkBottom = Math.Min(InkBottom, y);
                    InkTop = Math.Max(InkTop, y);
                }
            }

            if (!EndsWithSpace && HasInk)
            {
                _wordsInked = true;
                _wordsLeft = InkLeft;
                _wordsRight = InkRight;
            }

            Advance += glyph.Advance * across;
            return true;
        }

        /// <summary>
        /// The box a TEXT is justified by, as AutoCAD 2021's <c>AdjustAlignment</c> takes it: the ink's, but for a line whose last
        /// letter is a space, whose box reaches from the start of its baseline to its last pen and takes in the baseline, so that
        /// romans "A " is centred on 0 to 7.429 and not 0.190 to 7.429; false for a line of no ink that ends in no space.
        /// </summary>
        internal bool AlignmentBox(out double left, out double right, out double bottom, out double top)
        {
            left = InkLeft;
            right = InkRight;
            bottom = InkBottom;
            top = InkTop;

            if (Count > 0 && EndsWithSpace)
            {
                left = HasInk ? Math.Min(0.0, InkLeft) : 0.0;
                right = HasInk ? Math.Max(Advance, InkRight) : Advance;
                bottom = HasInk ? Math.Min(0.0, InkBottom) : 0.0;
                top = HasInk ? Math.Max(0.0, InkTop) : 0.0;
                return true;
            }

            return HasInk;
        }

        /// <summary>
        /// The width of an MTEXT of this one line, as AutoCAD 2021's <c>ActualWidth</c>: from the first ink, or the start of the
        /// line where it starts with a space, to the ink of its last letter that is no space, its spaces after it left out; a
        /// line of spaces alone as wide as they move the pen. romans "A" is 3.048 wide at 4 high, not its advance of 3.238, and
        /// txt "   ABC" 22.667.
        /// </summary>
        internal double ActualWidth => AllSpaces ? Advance : !_wordsInked ? 0.0 : _wordsRight - (StartsWithSpace ? 0.0 : _wordsLeft);

        /// <summary>The height of an MTEXT of this one line, as AutoCAD 2021's <c>ActualHeight</c>: from the lower of the baseline and the ink's bottom up to the higher of the letters' height and the ink's top.</summary>
        internal double ActualHeight => Math.Max(Height, HasInk ? InkTop : 0.0) - Math.Min(0.0, HasInk ? InkBottom : 0.0);
    }
}
