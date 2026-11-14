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
            double highUp = noUp ? 0.0 : h;
            double highDown = noDown ? 0.0 : h;
            double wideUp = up.ActualWidth;
            double wideDown = down.ActualWidth;
            var shape = new DxfStackShape
            {
                Kind = words.Kind,
                Numerator = words.Numerator,
                Denominator = words.Denominator,
            };

            if (words.Kind == '#')
            {
                // The numerator keeps to the left and the denominator follows its pen, as far as a third of the height where there
                // is none.
                double pen = noUp ? h / 3.0 : up.Advance;
                shape.NumeratorX = -up.WordsStart;
                shape.NumeratorY = highDown;
                shape.DenominatorX = pen - down.WordsStart;
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
                shape.NumeratorX = (centred ? (wide - wideUp) / 2.0 : 0.0) - up.WordsStart;
                shape.DenominatorX = (centred ? (wide - wideDown) / 2.0 : 0.0) - down.WordsStart;
                shape.NumeratorY = highDown + (h / 3.0);
                shape.Ink = wide;
                shape.Top = shape.NumeratorY + highUp;
                shape.HasLine = centred;
                shape.LineX0 = 0.0;
                shape.LineX1 = wide;
                shape.LineY0 = shape.LineY1 = highDown + (h / 6.0);
            }

            shape.Advance = shape.Ink + (h / 3.0);
            return shape;
        }
    }
}
