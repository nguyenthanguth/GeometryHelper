using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A line of words measured in an SHX or TrueType font, along its own x from the start of its baseline, in the drawing's
    /// units and upright: the pen's last place and the box of the ink, each letter's points placed as AutoCAD 2021 places them
    /// (checks F1a and F2), x the pen plus the width factor times the scale times the point's x, y the scale times its y, the
    /// scale the height over the font's height above its baseline. txt "ABC" 4 high is inked from 0 to 10.667 and moves the
    /// pen 12.
    /// </summary>
    internal sealed class DxfTextLine
    {
        private readonly DxfFont _font;

        // The ink up to the last letter that is no space, for the width of an MTEXT, which leaves the spaces after it out.
        private bool _wordsInked;
        private double _wordsLeft;
        private double _wordsRight;

        // The ink from the first letter that is no space to the last, the spaces between them counted, for the height of an
        // MTEXT of a TrueType font, which leaves the spaces before and after them out; and that of the spaces since the last.
        private bool _wordsHigh;
        private double _wordsBottom;
        private double _wordsTop;
        private bool _gapInked;
        private double _gapBottom;
        private double _gapTop;

        internal DxfTextLine(DxfFont font)
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
        /// it, or the font an MTEXT draws such a letter from. False too for a letter a TrueType font has not, which Windows may
        /// draw from another font (check F2): Arial "中" 4 high is 5.514 wide in a TEXT of AutoCAD 2021, not the 4.191 of
        /// Arial's own stand-in.
        /// </summary>
        internal bool Add(int number, double widthFactor, double height, bool strict)
        {
            DxfFont.Glyph glyph = _font.GlyphOf(number);

            if (glyph == null)
            {
                if (strict || _font is DxfTrueTypeFont)
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

            if (_font is DxfTrueTypeFont && glyph.HasInk)
            {
                Words(scale * glyph.Ys[0], scale * glyph.Ys[1]);
            }

            Advance += glyph.Advance * across;
            return true;
        }

        /// <summary>Takes a letter's ink, from its bottom to its top, into the ink of the words: a space's into that of the spaces since the last letter, which the next letter that is no space takes in.</summary>
        private void Words(double bottom, double top)
        {
            if (EndsWithSpace)
            {
                if (_wordsHigh)
                {
                    _gapBottom = _gapInked ? Math.Min(_gapBottom, bottom) : bottom;
                    _gapTop = _gapInked ? Math.Max(_gapTop, top) : top;
                    _gapInked = true;
                }

                return;
            }

            if (!_wordsHigh)
            {
                _wordsBottom = bottom;
                _wordsTop = top;
                _wordsHigh = true;
                return;
            }

            if (_gapInked)
            {
                bottom = Math.Min(bottom, _gapBottom);
                top = Math.Max(top, _gapTop);
                _gapInked = false;
            }

            _wordsBottom = Math.Min(_wordsBottom, bottom);
            _wordsTop = Math.Max(_wordsTop, top);
        }

        /// <summary>
        /// The box a TEXT is justified by, as AutoCAD 2021's <c>AdjustAlignment</c> takes it: the ink's, but for a line whose last
        /// letter is a space, whose box reaches from the start of its baseline to its last pen and takes in the baseline, so that
        /// romans "A " is centred on 0 to 7.429 and not 0.190 to 7.429; false for a line of no ink that ends in no space. A line
        /// of a TrueType font is boxed by <see cref="Box"/>, its ink from the start of its baseline to its last pen.
        /// </summary>
        internal bool AlignmentBox(out double left, out double right, out double bottom, out double top)
        {
            if (_font is DxfTrueTypeFont)
            {
                return Box(out left, out right, out bottom, out top);
            }

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
        /// The box of a TEXT's words, as AutoCAD 2021's <c>GeometricExtents</c> takes it, upright: the ink's for an SHX font; for
        /// a TrueType font the ink's up and down, and across from the start of the baseline, or the ink left of it, to the last
        /// pen, or the ink past it (check F2), Arial "H" 4 high from 0 to 4.035, its advance, though inked from 0.447 to 3.585;
        /// false for a line of no ink.
        /// </summary>
        internal bool Box(out double left, out double right, out double bottom, out double top)
        {
            bool trueType = _font is DxfTrueTypeFont;
            left = trueType ? Math.Min(0.0, InkLeft) : InkLeft;
            right = trueType ? Math.Max(Advance, InkRight) : InkRight;
            bottom = InkBottom;
            top = InkTop;
            return HasInk;
        }

        /// <summary>
        /// How far the ink of the words reaches below the baseline, as <see cref="ActualHeight"/> counts it; nought where it does
        /// not.
        /// </summary>
        internal double Depth => _font is DxfTrueTypeFont ? (_wordsHigh && _wordsBottom < 0.0 ? -_wordsBottom : 0.0) : (HasInk && InkBottom < 0.0 ? -InkBottom : 0.0);

        /// <summary>
        /// The width of an MTEXT of this one line, as AutoCAD 2021's <c>ActualWidth</c>: from the first ink, or the start of the
        /// line where it starts with a space, to the ink of its last letter that is no space, its spaces after it left out; a
        /// line of spaces alone as wide as they move the pen. romans "A" is 3.048 wide at 4 high, not its advance of 3.238, and
        /// txt "   ABC" 22.667. Of a TrueType font, ink left of the start of the line is not counted (check F1a): Arial "A" 4
        /// high is 3.735 wide, from 0 to its ink's right.
        /// </summary>
        internal double ActualWidth => AllSpaces ? Advance : !_wordsInked ? 0.0 : _wordsRight - (StartsWithSpace ? 0.0 : _font is DxfTrueTypeFont ? Math.Max(0.0, _wordsLeft) : _wordsLeft);

        /// <summary>
        /// The height of an MTEXT of this one line, as AutoCAD 2021's <c>ActualHeight</c>: from the lower of the baseline and the
        /// ink's bottom up to the higher of the letters' height and the ink's top; of a TrueType font, the ink of the letters from
        /// the first that is no space to the last (check F2), Arial "A B" 4 high 4.003 high, its space boxed a unit below the
        /// baseline, and "A " 4.
        /// </summary>
        internal double ActualHeight => _font is DxfTrueTypeFont
            ? Math.Max(Height, _wordsHigh ? _wordsTop : 0.0) - Math.Min(0.0, _wordsHigh ? _wordsBottom : 0.0)
            : Math.Max(Height, HasInk ? InkTop : 0.0) - Math.Min(0.0, HasInk ? InkBottom : 0.0);
    }
}
