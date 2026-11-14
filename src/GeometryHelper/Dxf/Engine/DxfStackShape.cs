using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A stack of an MTEXT (<c>\S1/2;</c>) laid out as AutoCAD 2021 explodes it (probed in its console: <c>Database.DxfOut</c>
    /// at R12 of MTEXTs and dimensions, 70 of them): the TEXT of the numerator, a LINE where the stack has a bar (<c>/</c>) or a
    /// slash (<c>#</c>), and the TEXT of the denominator, each as high as the letters of the stack. For letters 0.18 high, from
    /// the pen's start and the baseline of the denominator, which is the baseline of the line:
    /// <list type="bullet">
    /// <item><c>1/2</c>: the numerator's baseline 0.24 up (a height and a third of it), the bar 0.21 up (a height and a sixth),
    /// the bar as wide as the wider of the two halves (the width of its ink, 0.12 for a 2), each half centred on it. A half
    /// with no words stands no height of its own: <c>/</c> alone is a numerator and a denominator of no words 0.06 apart.</item>
    /// <item><c>^</c> (a tolerance, <c>+0.1^-0.1</c>): the same, with no bar and both halves held at the left.</item>
    /// <item><c>#</c>: the numerator at the left and as high as the denominator is high, the denominator after the
    /// numerator's pen, a LINE at 55 degrees twice the height long through the numerator's height and 0.03 before the
    /// denominator.</item>
    /// <item>No sign (<c>\S12;</c>): the numerator over an empty denominator, as <c>1/2</c> with no bar.</item>
    /// </list>
    /// The stack moves the pen by its width and a third of its height, and is as high as <see cref="Top"/>: 0.42 for a bar
    /// (a height, a third of it, and a height).
    /// </summary>
    internal sealed class DxfStackShape
    {
        private DxfStackShape() { }

        /// <summary>The sign between the halves: <c>/</c>, <c>#</c>, <c>^</c>, or nought for none.</summary>
        internal char Kind { get; private set; }

        /// <summary>What a half's TEXT begins with where the stack is lined under or over: <c>%%O</c>, <c>%%U</c>, both (in that order) or none.</summary>
        internal string Prefix { get; private set; } = string.Empty;

        /// <summary>The words above, the TEXT of which is written even where they are none.</summary>
        internal string Numerator { get; private set; }

        /// <summary>The words below.</summary>
        internal string Denominator { get; private set; }

        /// <summary>How far the numerator's pen stands from the start of the stack along the line.</summary>
        internal double NumeratorX { get; private set; }

        /// <summary>How far the numerator's baseline stands above the denominator's.</summary>
        internal double NumeratorY { get; private set; }

        /// <summary>How far the denominator's pen stands from the start of the stack along the line.</summary>
        internal double DenominatorX { get; private set; }

        /// <summary>Whether the stack has a LINE: a bar or a slash.</summary>
        internal bool HasLine { get; private set; }

        /// <summary>The ends of the LINE as the distances along the line and above the denominator's baseline, from the start of the stack: from (x0, y0) to (x1, y1).</summary>
        internal double LineX0 { get; private set; }

        /// <summary>See <see cref="LineX0"/>.</summary>
        internal double LineY0 { get; private set; }

        /// <summary>See <see cref="LineX0"/>.</summary>
        internal double LineX1 { get; private set; }

        /// <summary>See <see cref="LineX0"/>.</summary>
        internal double LineY1 { get; private set; }

        /// <summary>The width of the stack's ink, from its start.</summary>
        internal double Ink { get; private set; }

        /// <summary>How far the pen moves for the numerator's words: the length of the line AutoCAD draws through them when they are struck through.</summary>
        internal double NumeratorAdvance { get; private set; }

        /// <summary>How far the pen moves for the stack: its ink and a third of its height.</summary>
        internal double Advance { get; private set; }

        /// <summary>How high the stack's ink reaches above its denominator's baseline, which is the baseline of the line it stands in.</summary>
        internal double Top { get; private set; }

        /// <summary>
        /// The shape of a stack in a font: the halves measured at the stack's own width factor, height and spread; null where a
        /// letter of them is not measured.
        /// </summary>
        internal static DxfStackShape Of(DxfFont font, DxfFonts.Letter letter)
        {
            DxfFonts.StackWords words = letter.Stack;
            DxfTextLine up = DxfFonts.StackLine(font, words.Numerator, letter);
            DxfTextLine down = DxfFonts.StackLine(font, words.Denominator, letter);

            if (up == null || down == null)
            {
                return null;
            }

            double h = letter.Height;
            bool noUp = words.Numerator.Length == 0;
            bool noDown = words.Denominator.Length == 0;
            bool trueType = font is DxfTrueTypeFont;

            // The halves are as high as the letters are (a TrueType font's ink from the lowest to the highest, a descender counted),
            // an SHX font's as the height of the letters, whatever they are.
            double highUp =
                noUp ? 0.0
                : trueType ? up.InkTop - up.InkBottom
                : h;
            double highDown =
                noDown ? 0.0
                : trueType ? down.InkTop - down.InkBottom
                : h;

            // Where the ink of a half begins from its pen: of a TrueType font its own, even where it is left of it.
            double leftUp = trueType ? up.InkLeft : up.WordsStart;
            double leftDown = trueType ? down.InkLeft : down.WordsStart;
            double wideUp = up.ActualWidth;
            double wideDown = down.ActualWidth;
            var shape = new DxfStackShape
            {
                Kind = words.Kind,
                Numerator = words.Numerator,
                Denominator = words.Denominator,
                NumeratorAdvance = up.Advance,
            };

            if (words.Kind == '#')
            {
                // The numerator keeps to the left and the denominator follows its pen, as far as a third of the height where there
                // is none.
                double pen =
                    noUp ? h / 3.0
                    : trueType ? wideUp + (h / 3.0)
                    : up.Advance;
                shape.NumeratorX = -leftUp;
                shape.NumeratorY = highDown;
                shape.DenominatorX = pen - leftDown;
                shape.Ink = pen + wideDown;
                shape.Top = highDown + highUp;
                shape.HasLine = true;

                // A LINE twice the height long at 55 degrees through the numerator's height, a sixth of the height before the
                // denominator.
                double x = pen - (h / 6.0);
                double turn = 55.0 * Math.PI / 180.0;
                shape.LineX0 = x - (h * Math.Cos(turn));
                shape.LineY0 = highDown - (h * Math.Sin(turn));
                shape.LineX1 = x + (h * Math.Cos(turn));
                shape.LineY1 = highDown + (h * Math.Sin(turn));
            }
            else
            {
                double wide = Math.Max(wideUp, wideDown);
                bool centred = words.Kind == '/';
                shape.NumeratorX = (centred ? (wide - wideUp) / 2.0 : 0.0) - leftUp;
                shape.DenominatorX = (centred ? (wide - wideDown) / 2.0 : 0.0) - leftDown;
                shape.NumeratorY = highDown + (h / 3.0);
                shape.Ink = wide;
                shape.Top = shape.NumeratorY + highUp;
                shape.HasLine = centred;
                shape.LineX0 = 0.0;
                shape.LineX1 = wide;
                shape.LineY0 = shape.LineY1 = highDown + (h / 6.0);
            }

            // A stack lined under or over, each line a fifth of the height of the letters, stands that much further apart for
            // each half that has words: the denominator's line lifts the numerator and the bar, the numerator's line lifts the
            // top, and both lines of a stack lined under and over count.
            if ((letter.Marks & 3) != 0)
            {
                double pad = ((letter.Marks & 1) != 0 ? 1.0 : 0.0) + ((letter.Marks & 2) != 0 ? 1.0 : 0.0);
                pad *= h / 5.0;
                shape.Prefix =
                    ((letter.Marks & 2) != 0 ? "%%O" : string.Empty) + ((letter.Marks & 1) != 0 ? "%%U" : string.Empty);

                if (!noDown)
                {
                    shape.NumeratorY += pad;
                    shape.LineY0 += pad;
                    shape.LineY1 += pad;
                    shape.Top += pad;
                }

                if (!noUp)
                {
                    shape.Top += pad;
                }
            }

            shape.Advance = shape.Ink + (trueType ? Bearing(font, letter) : h / 3.0);
            return shape;
        }

        /// <summary>
        /// The room a TrueType font leaves after a stack: the room its zero has right of its ink (Arial 2.5 high: 98 of 1466
        /// units, 0.167), probed in AutoCAD 2021 on 13 fonts, where an SHX font leaves a third of the height. The letters after
        /// the stack begin at that distance from the end of its ink, not further back by their own ink as an SHX font's do.
        /// </summary>
        private static double Bearing(DxfFont font, DxfFonts.Letter letter)
        {
            DxfFont.Glyph zero = font.GlyphOf('0');

            if (zero == null || !zero.HasInk)
            {
                return letter.Height / 3.0;
            }

            double right = zero.Xs[0];

            foreach (double x in zero.Xs)
            {
                right = Math.Max(right, x);
            }

            return (zero.Advance - right) * (letter.Height / font.Above) * letter.Width;
        }
    }
}
