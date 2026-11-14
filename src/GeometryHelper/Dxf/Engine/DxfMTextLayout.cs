using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The words of an MTEXT laid out in lines in a font as AutoCAD 2021 lays them out (check F4): broken at each new paragraph
    /// (<c>\P</c>), and where the MTEXT has a width, wrapped at it word by word, a word moving down where the line would reach
    /// the width, a word longer than it left whole on a line of its own; the lines spaced as the MTEXT's line spacing says, and
    /// each line broken into its runs of letters of one look, as AutoCAD explodes it. "The quick brown fox jumps over the lazy
    /// dog and keeps on running" of txt.shx 4 high, 41 wide, is laid out in seven lines, "The quick" to "running", 38 wide and
    /// 45.333 high, the baselines 6.667 apart.
    /// </summary>
    internal sealed class DxfMTextLayout
    {
        private DxfMTextLayout(List<Line> lines, double actualWidth, double actualHeight)
        {
            Lines = lines;
            ActualWidth = actualWidth;
            ActualHeight = actualHeight;
        }

        /// <summary>The lines, from the top down.</summary>
        internal IReadOnlyList<Line> Lines { get; }

        /// <summary>The width of the widest line, as AutoCAD 2021's <c>ActualWidth</c>.</summary>
        internal double ActualWidth { get; }

        /// <summary>The height from the top of the first line to the foot of the last, as AutoCAD 2021's <c>ActualHeight</c>.</summary>
        internal double ActualHeight { get; }

        /// <summary>A line of the layout: its words measured, their runs of one look, and where its baseline stands below the top.</summary>
        internal sealed class Line
        {
            // The runs, found when first asked for, as only an exploding asks for them.
            private readonly Func<List<Run>> _find;
            private List<Run> _runs;

            internal Line(
                DxfTextLine measure,
                Func<List<Run>> runs,
                double baseline,
                double start,
                Func<List<Strike>> strikes = null
            )
            {
                Measure = measure;
                _find = runs;
                _strikes = strikes;
                Baseline = baseline;
                Start = start;
            }

            private readonly Func<List<Strike>> _strikes;
            private List<Strike> _struck;

            /// <summary>The lines drawn through the letters struck through (<c>\K</c>) where the layout is made for R12, which draws them as LINEs of their own; none otherwise.</summary>
            internal IReadOnlyList<Strike> Strikes =>
                _struck ?? (_struck = _strikes == null ? new List<Strike>() : _strikes());

            /// <summary>The line's words measured; of no letters for an empty paragraph.</summary>
            internal DxfTextLine Measure { get; }

            /// <summary>The line's runs of letters of one height, width, spread, hold, lines and colour, each a TEXT where AutoCAD 2021 explodes the MTEXT.</summary>
            internal IReadOnlyList<Run> Runs => _runs ?? (_runs = _find());

            /// <summary>How far the line's baseline stands below the top of the first line.</summary>
            internal double Baseline { get; }

            /// <summary>
            /// Where along the line its place in the box begins: its <see cref="DxfTextLine.WordsStart"/>, so that its ink stands
            /// at the left of its place, but the start of the line for a line wrapped where its letters change their look, which
            /// AutoCAD 2021 sets at its place unmoved (check F4: romans "ABC {\H2x;DEF} GHI" 30 wide, DEF at the left edge).
            /// </summary>
            internal double Start { get; }
        }

        /// <summary>A line AutoCAD 2021 draws through letters struck through (<c>\K</c>) when it saves R12: from where it starts along the line, its length, and how far above the baseline of the line it stands.</summary>
        internal sealed class Strike
        {
            internal Strike(double pen, double length, double rise)
            {
                Pen = pen;
                Length = length;
                Rise = rise;
            }

            /// <summary>Where the line starts, from the start of its line of words.</summary>
            internal double Pen { get; }

            /// <summary>How long the line is; nought for the lines AutoCAD draws through the halves of a stack.</summary>
            internal double Length { get; }

            /// <summary>How far above the baseline of the line of words it stands.</summary>
            internal double Rise { get; }
        }

        /// <summary>
        /// A run of letters of one look in a line, as AutoCAD 2021 explodes it to a TEXT (check F4): its words, where the pen
        /// stands at its start along the line, how far its baseline stands above the line's, and its height and width factor.
        /// txt "A\H2x;B\H0.5x;C" 4 high explodes to A, B 8 high from 4 and C from 12.
        /// </summary>
        internal sealed class Run
        {
            internal Run(
                string words,
                double pen,
                double rise,
                double height,
                double width,
                DxfStackShape stack = null,
                int color = -1
            )
            {
                Stack = stack;
                Words = words;
                Pen = pen;
                Rise = rise;
                Height = height;
                Width = width;
                Color = color;
            }

            /// <summary>The run's words as a TEXT of it reads them: that of <c>\~</c> a space, the spaces at the end of the line left out, and <c>%%U</c>, <c>%%O</c> and <c>%%K</c> before it where it is lined under, over or through.</summary>
            internal string Words { get; }

            /// <summary>Where the pen stands at the run's start, from the start of the line.</summary>
            internal double Pen { get; }

            /// <summary>How far the run's baseline stands above the line's: up to the middle or the top of the line's height where it is held so (<c>\A1;</c>, <c>\A2;</c>).</summary>
            internal double Rise { get; }

            /// <summary>The height of the run's letters.</summary>
            internal double Height { get; }

            /// <summary>The width factor of the run's letters.</summary>
            internal double Width { get; }

            /// <summary>The colour index of the run's letters where a colour code gives them one (0 to 256); -1 where they keep the MTEXT's own.</summary>
            internal int Color { get; }

            /// <summary>The stack the run is, where it is one (<c>\S1/2;</c>), whose halves and LINE are the TEXTs and LINE AutoCAD 2021 explodes it to; null for letters.</summary>
            internal DxfStackShape Stack { get; }
        }

        /// <summary>
        /// The words of an MTEXT, its codes as <see cref="DxfFonts.MTextLetters"/> reads them, laid out in a font at a height,
        /// wrapped at a width (none at nought), the lines spaced by a factor of single spacing at least or exactly; null where
        /// a letter is not measured. AutoCAD 2021 lays them out so (check F4):
        /// <list type="bullet">
        /// <item>A line may break after the spaces between words, which are left at the end of the line it breaks, and before
        /// and after each Chinese, Japanese or Korean letter, as <see cref="Breaks"/> says; a word moves down where the line, as
        /// wide as <see cref="DxfTextLine.FitWidth"/> measures it, would reach the width, a word longer than the width left whole
        /// on a line of its own. Spaces before the first word are a word of their own: "  ABC DEF  GHI" of txt.shx 4 high, 15
        /// wide, begins with a line of its two spaces.</item>
        /// <item>At least: the first baseline stands the first line's <see cref="DxfTextLine.Top"/> below the top, and the top of
        /// each line the factor times the top of the line before, a third of that line's height and a third of the MTEXT's
        /// height below the top of the line before it, so that a line of taller letters is the more below the line before it;
        /// the MTEXT as high as from the top to the foot of the last line, its ink below its baseline counted. gdt.shx "x\Py" 4
        /// high has its second baseline 8.667 below its first, its y 2 higher than the height, and txt.shx
        /// "ABC\P{\H3x;g}\PDEF" its baselines 14.667 and 9.333 apart.</item>
        /// <item>Exactly: the baselines the MTEXT's height below the top and the factor times five thirds of it apart, the
        /// MTEXT as high as its height, the spacing of each line after the first and a third of its height, whatever the
        /// letters.</item>
        /// <item>An empty paragraph at the end is no line, but one more is; one before it is a line as high as the letters where
        /// it is.</item>
        /// <item>A stack (<c>\S1/2;</c>), where <paramref name="stacks"/> asks for it, is laid out by <see cref="DxfStackShape"/>:
        /// the line holding it is as high as the stack, 0.42 for letters 0.18 high, with its baseline that of the stack's
        /// denominator, and the line after it stands as far below it as the stack is high, not as the height of the letters and
        /// a third of it. txt.shx "A\S1/2;B" 0.18 high held at the top left has its A and its denominator 0.42 below the top
        /// and its numerator 0.24 above them.</item>
        /// <item>Where <paramref name="strikes"/> asks for it, letters struck through (<c>\K</c>) are no look of their own (they
        /// are one TEXT with the letters about them), and each line has the <see cref="Line.Strikes"/> AutoCAD 2021 draws
        /// through them when it saves R12.</item>
        /// </list>
        /// </summary>
        internal static DxfMTextLayout Of(
            DxfFont font,
            DxfFonts.Big big,
            string contents,
            double height,
            double width,
            double factor,
            bool exactly,
            bool stacks = false,
            bool strikes = false
        )
        {
            List<DxfFonts.Letter> letters = DxfFonts.MTextLetters(font, big, contents, height, stacks);

            if (letters == null)
            {
                return null;
            }

            var spans = new List<KeyValuePair<int, int>>();
            var measured = new List<DxfTextLine>();
            var empty = new List<double>();
            int begin = 0;

            for (int k = 0; k <= letters.Count; k++)
            {
                if (k < letters.Count && !letters[k].IsParagraph)
                {
                    continue;
                }

                int was = spans.Count;

                if (!Wrap(font, big, letters, begin, k, height, width, spans, measured))
                {
                    return null;
                }

                // The height an empty paragraph stands for: that of the letters where it ends.
                double force = k < letters.Count ? letters[k].Height : height;

                for (int s = was; s < spans.Count; s++)
                {
                    empty.Add(force);
                }

                begin = k + 1;
            }

            // An empty paragraph at the end is no line: "ABC\P" is one line 4 high, but "ABC\P\P" two, 10.667 high.
            if (spans.Count > 1 && spans[spans.Count - 1].Key == spans[spans.Count - 1].Value)
            {
                spans.RemoveAt(spans.Count - 1);
                measured.RemoveAt(measured.Count - 1);
                empty.RemoveAt(empty.Count - 1);
            }

            var lines = new List<Line>(spans.Count);
            double actualWidth = 0.0;
            double baseline = 0.0;
            double top = 0.0;
            double lastTop = 0.0;
            double lastHigh = 0.0;
            double lastStack = 0.0;

            for (int i = 0; i < spans.Count; i++)
            {
                DxfTextLine line = measured[i];
                double high = line.Count == 0 ? empty[i] : line.Height;
                double lineTop = line.Count == 0 ? high : line.Top;

                if (exactly)
                {
                    baseline = i == 0 ? -height : baseline - (factor * height * 5.0 / 3.0);
                }
                else
                {
                    // A line with a stack in it reaches as far as the stack is high (the letters' height and a third of it at
                    // least), where a line of letters reaches the top of the tallest and a third of its height.
                    double reach =
                        lastStack > 0.0 ? Math.Max(lastStack, lastHigh + (lastHigh / 3.0)) : lastTop + (lastHigh / 3.0);
                    top = i == 0 ? 0.0 : top - (factor * (reach + (height / 3.0)));
                    baseline = top - lineTop;
                }

                lastTop = lineTop;
                lastHigh = high;
                lastStack = line.StackReach;
                actualWidth = Math.Max(actualWidth, line.ActualWidth);
                int from = spans[i].Key;
                int to = spans[i].Value;
                double lineHigh =
                    exactly ? height
                    : line.Count == 0 ? high
                    : line.Top;
                bool restyled =
                    from > 0
                    && from < letters.Count
                    && !letters[from - 1].IsParagraph
                    && letters[from - 1].Height != letters[from].Height;
                lines.Add(
                    new Line(
                        line,
                        () => Runs(font, big, letters, from, to, lineHigh, height, strikes),
                        baseline,
                        restyled ? 0.0 : line.WordsStart,
                        strikes
                            ? () => Struck(font, big, letters, from, to, lineHigh, height)
                            : (Func<List<Strike>>)null
                    )
                );
            }

            DxfTextLine last = measured[measured.Count - 1];
            double actualHeight =
                exactly ? height + ((spans.Count - 1) * factor * height * 5.0 / 3.0) + (height / 3.0)
                : spans.Count == 1 && (last.Count > 0 || letters.Count == 0) ? last.ActualHeight
                : lastTop - top + last.Depth;
            return new DxfMTextLayout(lines, actualWidth, actualHeight);
        }

        /// <summary>
        /// Lays out the letters of a paragraph, from one place up to another, in lines wrapped at a width, adding each line's
        /// span and its letters measured; false where a letter is not measured.
        /// </summary>
        private static bool Wrap(
            DxfFont font,
            DxfFonts.Big big,
            List<DxfFonts.Letter> letters,
            int from,
            int to,
            double height,
            double width,
            List<KeyValuePair<int, int>> spans,
            List<DxfTextLine> measured
        )
        {
            int start = from;
            int kept = -1;
            DxfTextLine fits = null;

            for (int p = width > 0.0 ? from + 1 : to + 1; p <= to; p++)
            {
                if (p < to && !Breaks(letters[p - 1].Number, letters[p].Number))
                {
                    continue;
                }

                while (true)
                {
                    DxfTextLine line = DxfFonts.Measure(font, big, letters, start, p, height);

                    if (line == null)
                    {
                        return false;
                    }

                    if (width > 0.0 && kept > start && !(line.FitWidth < width))
                    {
                        spans.Add(new KeyValuePair<int, int>(start, kept));
                        measured.Add(fits);
                        start = kept;
                        kept = -1;
                        continue;
                    }

                    kept = p;
                    fits = line;
                    break;
                }
            }

            if (fits == null)
            {
                fits = DxfFonts.Measure(font, big, letters, from, to, height);

                if (fits == null)
                {
                    return false;
                }
            }

            spans.Add(new KeyValuePair<int, int>(start, to));
            measured.Add(fits);
            return true;
        }

        /// <summary>
        /// Whether a line may break between two letters of a paragraph, as AutoCAD 2021 breaks one (check F4): after spaces,
        /// before the first letter that is none; before or after a letter of Chinese, Japanese or Korean writing, but not before
        /// a space, nor before a sign a line does not begin with (，。、；：？！．…, the closing brackets and quotes, ・ ー 々) or
        /// after one it does not end with (the opening brackets （「『【〔〈《).
        /// </summary>
        internal static bool Breaks(int before, int after)
        {
            if (after == ' ')
            {
                return false;
            }

            if (before == ' ')
            {
                return true;
            }

            return (IsIdeographic(before) || IsIdeographic(after)) && !Closes(after) && !Opens(before);
        }

        /// <summary>Whether a letter is one of Chinese, Japanese or Korean writing, broken before and after: of the CJK blocks, kana, Hangul and the full-width forms.</summary>
        private static bool IsIdeographic(int c) =>
            (c >= 0x2E80 && c <= 0x9FFF)
            || (c >= 0xAC00 && c <= 0xD7AF)
            || (c >= 0xF900 && c <= 0xFAFF)
            || (c >= 0xFF00 && c <= 0xFFEF)
            || (c >= 0x20000 && c <= 0x3FFFF);

        /// <summary>Whether a letter is a sign a line does not begin with: commas, full stops, colons, the closing brackets and quotes, the ellipsis, the middle dot, the long vowel mark and the iteration mark.</summary>
        private static bool Closes(int c) =>
            c <= 0xFFFF && "、。，．：；？！）〉》」』】〕］｝“”‘’…・ー々".IndexOf((char)c) >= 0;

        /// <summary>Whether a letter is an opening bracket a line does not end with.</summary>
        private static bool Opens(int c) => c <= 0xFFFF && "（〈《「『【〔［｛".IndexOf((char)c) >= 0;

        /// <summary>
        /// The runs of letters of one look of a line, from one place up to another, each where the pen stands at its start; none
        /// where the letters before one are not measured. The spaces at the end of the line are left out of its last run.
        /// </summary>
        private static List<Run> Runs(
            DxfFont font,
            DxfFonts.Big big,
            List<DxfFonts.Letter> letters,
            int from,
            int to,
            double lineHigh,
            double height,
            bool strikes = false
        )
        {
            int end = to;

            while (end > from && letters[end - 1].IsSpace)
            {
                end--;
            }

            var runs = new List<Run>();
            int start = from;
            bool stacked = false;
            double lean = 0.0;

            for (int k = from + 1; k <= end; k++)
            {
                if (k < end && SameLook(letters[k - 1], letters[k], strikes))
                {
                    continue;
                }

                DxfFonts.Letter look = letters[start];
                double pen = 0.0;

                if (start > from)
                {
                    DxfTextLine before = DxfFonts.Measure(font, big, letters, from, start, height);

                    if (before == null)
                    {
                        return new List<Run>();
                    }

                    pen = before.Advance;

                    // A TrueType line sets a stack and what follows it where the ink of the letters before it ends where that is
                    // past their pen (Arial's A is inked to 1369 units and moves the pen 1366): the stack begins at the ink.
                    if (font is DxfTrueTypeFont && look.Stack != null && !stacked)
                    {
                        lean = Math.Max(0.0, before.InkRight - before.Advance);
                    }

                    if (font is DxfTrueTypeFont && (look.Stack != null || stacked))
                    {
                        pen += lean;
                    }
                }
                stacked |= look.Stack != null;

                DxfStackShape stack = look.Stack == null ? null : DxfStackShape.Of(font, look);

                // Letters after a stack begin where the stack ends and the gap after it, as far from the pen as their own ink
                // stands from it: romans 2.5 high "A\S1/2;B" has the B 0.476 before the end of the gap, where its ink begins.
                if (
                    start > from
                    && stack == null
                    && letters[start - 1].Stack != null
                    && !(font is DxfTrueTypeFont)
                    && !DxfFonts.IsBigLetter(font, big, letters, start)
                )
                {
                    DxfTextLine own = DxfFonts.Measure(font, big, letters, start, k, height);
                    pen -= own == null ? 0.0 : own.WordsStart;
                }

                double high = stack == null ? look.Height : stack.Top;
                double rise =
                    look.Align == 1 ? (lineHigh - high) / 2.0
                    : look.Align == 2 ? lineHigh - high
                    : 0.0;
                var words = new StringBuilder((k - start) + 9);

                if (stack == null)
                {
                    words
                        .Append((look.Marks & 1) != 0 ? "%%U" : string.Empty)
                        .Append((look.Marks & 2) != 0 ? "%%O" : string.Empty)
                        .Append((look.Marks & 4) != 0 && !strikes ? "%%K" : string.Empty);

                    for (int j = start; j < k; j++)
                    {
                        int c = letters[j].Number;
                        words.Append(c == 0xA0 ? " " : char.ConvertFromUtf32(c));
                    }
                }

                runs.Add(new Run(words.ToString(), pen, rise, look.Height, look.Width, stack, look.Color));
                start = k;
            }

            return runs;
        }

        /// <summary>
        /// The lines AutoCAD 2021 draws through the letters of a line struck through (<c>\K</c>) when it saves R12, found by
        /// probing its console: one for each stretch of struck letters of one height and hold, from the pen where the stretch
        /// begins to where it ends (the advances of its letters, a space inside it counted), half the letters' height above
        /// the baseline. A stretch that carries on from the paragraph before is a line of no length where it begins. A stack in
        /// a stretch is not lined through whole: where the stretch begins at it the numerator's words are, and the denominator
        /// has a line of no length, and where struck letters come before it the numerator has one of no length too (at the
        /// bar, as high as the numerator's baseline, or half the height above it for a stack with no denominator); the letters
        /// of the stretch after it have a line of no length at the denominator.
        /// </summary>
        private static List<Strike> Struck(
            DxfFont font,
            DxfFonts.Big big,
            List<DxfFonts.Letter> letters,
            int from,
            int to,
            double lineHigh,
            double height
        )
        {
            int end = to;

            while (end > from && letters[end - 1].IsSpace)
            {
                end--;
            }

            var strikes = new List<Strike>();
            int i = from;

            while (i < end)
            {
                if ((letters[i].Marks & 4) == 0)
                {
                    i++;
                    continue;
                }

                // A stretch of struck letters of one height and hold.
                DxfFonts.Letter head = letters[i];
                int stop = i + 1;

                while (
                    stop < end
                    && (letters[stop].Marks & 4) != 0
                    && letters[stop].Height == head.Height
                    && letters[stop].Align == head.Align
                )
                {
                    stop++;
                }

                bool carried = (head.Marks & 8) != 0;
                double half = head.Height / 2.0;
                double lift =
                    head.Align == 1 ? (lineHigh - head.Height) / 2.0
                    : head.Align == 2 ? lineHigh - head.Height
                    : 0.0;
                double afterX = 0.0;
                double afterRise = 0.0;
                bool stacked = false;
                int k = i;

                while (k < stop)
                {
                    if (letters[k].Stack == null)
                    {
                        int next = k;

                        while (next < stop && letters[next].Stack == null)
                        {
                            next++;
                        }

                        DxfTextLine before = DxfFonts.Measure(font, big, letters, from, k, height);
                        DxfTextLine within =
                            stacked || carried ? null : DxfFonts.Measure(font, big, letters, k, next, height);

                        if (before == null || (!stacked && !carried && within == null))
                        {
                            return new List<Strike>();
                        }

                        strikes.Add(
                            stacked
                                ? new Strike(afterX, 0.0, afterRise)
                                : new Strike(before.Advance, carried ? 0.0 : within.Advance, lift + half)
                        );
                        k = next;
                        continue;
                    }

                    DxfFonts.Letter letter = letters[k];
                    DxfStackShape shape = DxfStackShape.Of(font, letter);
                    DxfTextLine start = DxfFonts.Measure(font, big, letters, from, k, height);

                    if (shape == null || start == null)
                    {
                        return new List<Strike>();
                    }

                    double pen = start.Advance;
                    double stackLift =
                        head.Align == 1 ? (lineHigh - shape.Top) / 2.0
                        : head.Align == 2 ? lineHigh - shape.Top
                        : 0.0;
                    bool begins = !carried && !stacked && k == i && (k == from || (letters[k - 1].Marks & 4) == 0);
                    double denominatorRise = stackLift + half;

                    if (stacked)
                    {
                        // Every line after the first stack's stands at the place that one left the pen.
                        strikes.Add(
                            new Strike(
                                afterX,
                                0.0,
                                stackLift + shape.NumeratorY + (shape.Denominator.Length == 0 ? half : 0.0)
                            )
                        );
                        strikes.Add(new Strike(afterX, 0.0, denominatorRise));
                    }
                    else if (begins)
                    {
                        // A stretch that begins at the stack: the numerator's words are lined through and the denominator has a
                        // line of no length at its words; the pen then stands after the stack.
                        strikes.Add(
                            new Strike(
                                pen + shape.NumeratorX,
                                shape.NumeratorAdvance,
                                stackLift + shape.NumeratorY + half
                            )
                        );
                        strikes.Add(new Strike(pen + shape.DenominatorX, 0.0, denominatorRise));
                        afterX = pen + shape.Advance;
                    }
                    else
                    {
                        // Struck letters come before it: both halves have a line of no length, the numerator's at the stack's start,
                        // the denominator's there too but a third of the height on for a slash.
                        strikes.Add(
                            new Strike(
                                pen,
                                0.0,
                                stackLift + shape.NumeratorY + (shape.Denominator.Length == 0 ? half : 0.0)
                            )
                        );
                        afterX = pen + (shape.Kind == '#' ? letter.Height / 3.0 : 0.0);
                        strikes.Add(new Strike(afterX, 0.0, denominatorRise));
                    }

                    afterRise = denominatorRise;
                    stacked = true;
                    k++;
                }

                i = stop;
            }

            return strikes;
        }

        /// <summary>Whether two letters are of one look: of one height, width, spread, hold, lines and colour; the line through them is none where R12 draws it as LINEs of its own.</summary>
        private static bool SameLook(DxfFonts.Letter a, DxfFonts.Letter b, bool strikes) =>
            a.Stack == null
            && b.Stack == null
            && a.Height == b.Height
            && a.Width == b.Width
            && a.Track == b.Track
            && a.Align == b.Align
            && (a.Marks & (strikes ? 3 : 7)) == (b.Marks & (strikes ? 3 : 7))
            && a.Color == b.Color;
    }
}
