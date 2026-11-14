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

        // Where the pen stood at the start of the last word, and the ink of its letters, for where an MTEXT wraps; the last letter.
        private double _fitPen;
        private bool _fitInked;
        private double _fitLeft;
        private double _fitRight;
        private int _previous = ' ';

        // How far below and above the baseline the lines drawn under and over letters of an MTEXT reach, where any is.
        private bool _marked;
        private double _markLow;
        private double _markHigh;

        // Whether the line was drawn beside a big font, and how far the pen stood below and above the baseline in its letters.
        private bool _besideBig;

        // Whether the line holds a stack, whose line is as wide as the ink counted from where it begins even left of the start.
        private bool _stacked;
        private double _penLow;
        private double _penHigh;

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
        /// Arial's own stand-in. A letter of an MTEXT is given too the factor its pen's move is spread by and the lines drawn
        /// with it, as <see cref="DxfFonts.Letter"/> holds them.
        /// </summary>
        internal bool Add(int number, double widthFactor, double height, bool strict, double track = 1.0, int marks = 0)
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

            Add(_font, glyph, number, widthFactor, height, track, marks);
            return true;
        }

        /// <summary>
        /// Adds a letter of a number drawn from a font, the line's or the big font beside it, at a width factor and a height,
        /// scaled by the height over that font's height above its baseline: gbcbig.shx's 中 4 high by 4 over 8; a letter of no
        /// glyph taken in with no ink and no width. A letter of an MTEXT spreads the pen's move after it by a factor
        /// (<c>\T</c>), and is lined under, over or through (<c>\L</c>, <c>\O</c>, <c>\K</c>, marks 1, 2 and 4): the line under it a fifth of its
        /// height below the baseline, the line over it a fifth above its height, as AutoCAD 2021 measures an MTEXT (check F4).
        /// </summary>
        internal void Add(
            DxfFont font,
            DxfFont.Glyph glyph,
            int number,
            double widthFactor,
            double height,
            double track = 1.0,
            int marks = 0
        )
        {
            double scale = height / font.Above;
            double across = scale * widthFactor;

            if ((marks & 3) != 0)
            {
                _markLow = (marks & 1) != 0 ? Math.Min(_markLow, -height / 5.0) : _markLow;
                _markHigh = (marks & 2) != 0 ? Math.Max(_markHigh, height * 6.0 / 5.0) : _markHigh;
                _marked = true;
            }

            Height = Count == 0 ? height : Math.Max(Height, height);
            StartsWithSpace = Count == 0 ? number == ' ' : StartsWithSpace;
            EndsWithSpace = number == ' ';
            AllSpaces &= EndsWithSpace;
            Count++;

            // A word begins where a line of an MTEXT may break: after a space, and at most letters of Chinese, Japanese or Korean.
            if (!EndsWithSpace && (Count == 1 || DxfMTextLayout.Breaks(_previous, number)))
            {
                _fitPen = Advance;
                _fitInked = false;
            }

            _previous = number;

            if (glyph == null)
            {
                return;
            }

            for (int k = 0; k < glyph.Xs.Length; k++)
            {
                double x = Advance + (across * glyph.Xs[k]);
                Dot(x, scale * glyph.Ys[k]);

                if (!EndsWithSpace)
                {
                    _fitLeft = _fitInked ? Math.Min(_fitLeft, x) : x;
                    _fitRight = _fitInked ? Math.Max(_fitRight, x) : x;
                    _fitInked = true;
                }
            }

            if (font is DxfShxFont shx && shx.IsBig)
            {
                _penLow = Math.Min(_penLow, scale * glyph.PenBottom);
                _penHigh = Math.Max(_penHigh, scale * glyph.PenTop);
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

            Advance += track == 1.0 ? glyph.Advance * across : glyph.Advance * across * track;
        }

        /// <summary>How high the tallest stack of the line reaches above its baseline; nought where the line holds none.</summary>
        internal double StackReach { get; private set; }

        /// <summary>
        /// Adds a stack (<c>\S1/2;</c>) as <see cref="DxfStackShape"/> lays it out: its ink the box from the pen to the
        /// width of its ink and from the baseline to <see cref="DxfStackShape.Top"/>, the pen moved by its width and a third
        /// of its height. False where a letter of its halves is not measured.
        /// </summary>
        internal bool AddStack(DxfFonts.Letter letter)
        {
            DxfStackShape shape = DxfStackShape.Of(_font, letter);

            if (shape == null)
            {
                return false;
            }

            _stacked = true;
            Height = Count == 0 ? letter.Height : Math.Max(Height, letter.Height);
            StartsWithSpace = Count != 0 && StartsWithSpace;
            EndsWithSpace = false;
            AllSpaces = false;
            Count++;

            if (Count == 1 || DxfMTextLayout.Breaks(_previous, DxfFonts.Stacked))
            {
                _fitPen = Advance;
                _fitInked = false;
            }

            _previous = DxfFonts.Stacked;
            Dot(Advance, 0.0);
            Dot(Advance + shape.Ink, shape.Top);
            _fitLeft = _fitInked ? Math.Min(_fitLeft, Advance) : Advance;
            _fitRight = _fitInked ? Math.Max(_fitRight, Advance + shape.Ink) : Advance + shape.Ink;
            _fitInked = true;
            _wordsInked = true;
            _wordsLeft = InkLeft;
            _wordsRight = InkRight;

            // A TrueType line is as high as the ink of its words: the stack's reaches as high as the stack does.
            if (_font is DxfTrueTypeFont)
            {
                Words(0.0, shape.Top);
            }

            StackReach = Math.Max(StackReach, shape.Top);
            Advance += shape.Advance;
            return true;
        }

        /// <summary>Marks the line drawn beside a big font, which <see cref="AlignmentBox"/> boxes as AutoCAD 2021 boxes one (check F3).</summary>
        internal void BesideBig() => _besideBig = true;

        /// <summary>Takes a point, in the drawing's units along the line, into the box of the ink.</summary>
        internal void Dot(double x, double y)
        {
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
        /// of a TrueType font is boxed by <see cref="Box"/>, its ink from the start of its baseline to its last pen. A line drawn
        /// beside a big font takes in too, where it ends in a space, where the pen stood in its letters of the big font, so that
        /// gbcbig.shx's "中 " 4 high reaches 0.221 below the baseline, not its ink's 0.176; and where it has no ink and ends in no
        /// space it is boxed at the start of its baseline, as AutoCAD 2021 boxes a lone ideographic space (check F3).
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

                if (_besideBig)
                {
                    bottom = Math.Min(bottom, _penLow);
                    top = Math.Max(top, _penHigh);
                }

                return true;
            }

            if (!HasInk && _besideBig && Count > 0)
            {
                left = right = bottom = top = 0.0;
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
        /// How far the ink of the words reaches below the baseline, as <see cref="ActualHeight"/> counts it, a line under them
        /// taken in; nought where it does not.
        /// </summary>
        internal double Depth => _marked ? Math.Max(InkDepth, -_markLow) : InkDepth;

        private double InkDepth =>
            _font is DxfTrueTypeFont
                ? (_wordsHigh && _wordsBottom < 0.0 ? -_wordsBottom : 0.0)
                : (HasInk && InkBottom < 0.0 ? -InkBottom : 0.0);

        /// <summary>
        /// The width of an MTEXT of this one line, as AutoCAD 2021's <c>ActualWidth</c>: from the first ink, or the start of the
        /// line where it starts with a space, to the ink of its last letter that is no space, its spaces after it left out; a
        /// line of spaces alone as wide as they move the pen. romans "A" is 3.048 wide at 4 high, not its advance of 3.238, and
        /// txt "   ABC" 22.667. Of a TrueType font, ink left of the start of the line is not counted (check F1a): Arial "A" 4
        /// high is 3.735 wide, from 0 to its ink's right; nor of a line drawn beside a big font (check F3): whgdtxt.shx's
        /// "한국어" 4 high 14.083 wide, though inked from -0.208.
        /// </summary>
        internal double ActualWidth =>
            AllSpaces ? Advance
            : !_wordsInked ? 0.0
            : _wordsRight
                - (
                    StartsWithSpace ? 0.0
                    : (_font is DxfTrueTypeFont && !_stacked) || _besideBig ? Math.Max(0.0, _wordsLeft)
                    : _wordsLeft
                );

        /// <summary>
        /// The height of an MTEXT of this one line, as AutoCAD 2021's <c>ActualHeight</c>: from the lower of the baseline and the
        /// ink's bottom up to the higher of the letters' height and the ink's top; of a TrueType font, the ink of the letters from
        /// the first that is no space to the last (check F2), Arial "A B" 4 high 4.003 high, its space boxed a unit below the
        /// baseline, and "A " 4. A line drawn under or over letters is taken in: txt "\LABC" 4 high is 4.8 high.
        /// </summary>
        internal double ActualHeight =>
            _marked ? Top + Depth
            : _font is DxfTrueTypeFont
                ? Math.Max(Height, _wordsHigh ? _wordsTop : 0.0) - Math.Min(0.0, _wordsHigh ? _wordsBottom : 0.0)
            : Math.Max(Height, HasInk ? InkTop : 0.0) - Math.Min(0.0, HasInk ? InkBottom : 0.0);

        /// <summary>
        /// How high the line of an MTEXT reaches above its baseline, as <see cref="ActualHeight"/> counts it: the higher of the
        /// letters' height and the top of their ink, of a TrueType font of the ink of the letters from the first that is no
        /// space to the last, a line over them taken in.
        /// </summary>
        internal double Top => _marked ? Math.Max(Rise, _markHigh) : Rise;

        private double Rise =>
            _font is DxfTrueTypeFont
                ? Math.Max(Height, _wordsHigh ? _wordsTop : 0.0)
                : Math.Max(Height, HasInk ? InkTop : 0.0);

        /// <summary>
        /// How wide the line is as AutoCAD 2021 tells whether it fits the width of an MTEXT (check F4): from the start of the line
        /// to where the pen stands at its last word, and the ink of that word, as if it had no bearing on its left; a word the
        /// letters from where a line may break (<see cref="DxfMTextLayout.Breaks"/>), and of a TrueType font or beside a big font
        /// its ink left of that pen not counted. romans "H H t" 4 high is 17.905, not its ink's 17.524, and moves t down at a
        /// width of 17.9; a line of spaces alone is as wide as they move the pen.
        /// </summary>
        internal double FitWidth =>
            AllSpaces ? Advance
            : !_fitInked ? _fitPen
            : _fitRight - ((_font is DxfTrueTypeFont || _besideBig ? Math.Max(_fitPen, _fitLeft) : _fitLeft) - _fitPen);

        /// <summary>
        /// Where along the line its <see cref="ActualWidth"/> is measured from: its first ink, or the start of the line where it
        /// starts with a space or is of spaces alone, ink left of the start not counted of a TrueType font or beside a big font;
        /// romans "A" 4 high from 0.190, where AutoCAD 2021 puts the ink of a line of an MTEXT at its left edge.
        /// </summary>
        internal double WordsStart =>
            AllSpaces || !_wordsInked || StartsWithSpace ? 0.0
            : _font is DxfTrueTypeFont || _besideBig ? Math.Max(0.0, _wordsLeft)
            : _wordsLeft;
    }
}
