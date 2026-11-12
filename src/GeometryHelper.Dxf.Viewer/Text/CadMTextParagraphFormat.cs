using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A tab stop of an MTEXT paragraph, as AutoCAD 2021's ruler and Paragraph dialog set one: where it stands from the left
    /// of the frame (or of the column), and how the word after a tab is set on it.
    /// </summary>
    /// <remarks>
    /// A left stop 10 from the left of an MTEXT 2.5 high is written <c>\pxt4;</c>, a centre one <c>c4</c>, a right one
    /// <c>r4</c>, a decimal one <c>d4</c>; a decimal one on a comma <c>D,4</c> and on a space <c>D 4</c>, as AutoCAD writes them.
    /// </remarks>
    public sealed class CadMTextTabStop : IEquatable<CadMTextTabStop>
    {
        /// <summary>Makes a tab stop.</summary>
        /// <param name="position">Where it stands, in drawing units from the left of the frame, above nought.</param>
        /// <param name="kind">How the word after a tab is set on it: l its start, c its middle, r its end, d its decimal separator.</param>
        /// <param name="decimalSeparator">The separator a decimal stop is set on: a period, a comma or a space.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position is not a finite number above nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the kind is none of l, c, r and d, or the separator none of the three.</exception>
        public CadMTextTabStop(double position, char kind = 'l', char decimalSeparator = '.')
        {
            Guard.Positive(
                position,
                nameof(position),
                "A tab stop stands a finite distance above nought from the left."
            );

            if ("lcrd".IndexOf(kind) < 0)
            {
                throw new ArgumentException("A tab stop is l, c, r or d.", nameof(kind));
            }

            if (". ,".IndexOf(decimalSeparator) < 0)
            {
                throw new ArgumentException(
                    "A decimal separator is a period, a comma or a space.",
                    nameof(decimalSeparator)
                );
            }

            Position = position;
            Kind = kind;
            DecimalSeparator = kind == 'd' ? decimalSeparator : '.';
        }

        /// <summary>Gets where the stop stands, in drawing units from the left of the frame.</summary>
        public double Position { get; }

        /// <summary>Gets how the word after a tab is set on it: l, c, r or d.</summary>
        public char Kind { get; }

        /// <summary>Gets the separator a decimal stop is set on: a period, a comma or a space; a period for the other kinds.</summary>
        public char DecimalSeparator { get; }

        /// <inheritdoc/>
        public bool Equals(CadMTextTabStop other) =>
            other != null
            && Math.Abs(Position - other.Position) <= Tolerance.Default.EqualPoint
            && Kind == other.Kind
            && DecimalSeparator == other.DecimalSeparator;

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CadMTextTabStop);

        /// <inheritdoc/>
        public override int GetHashCode() => (Kind * 31) + DecimalSeparator;

        /// <inheritdoc/>
        public override string ToString() =>
            Kind.ToString() + Position.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// How a paragraph of an MTEXT is set, as AutoCAD 2021's ruler and Paragraph dialog set it: its indents, tab stops,
    /// paragraph spacing and line spacing, all in drawing units. Read from and written to the paragraph codes AutoCAD writes
    /// (<c>\pxi-3.2,l4,r1.2,b0.8,a1.2,sm2,t6,c8;</c>), which hold from the paragraph they are set in on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first line starts at <see cref="FirstLineIndent"/>, the second and later at <see cref="HangingIndent"/>, both from
    /// the left of the frame: AutoCAD writes the hanging indent as <c>l</c> and the first line as <c>i</c>, the first line less
    /// the hanging. In an MTEXT 2.5 high a first line at 2 and the rest at 10 write <c>\pxi-3.2,l4;</c>: after <c>x</c>
    /// every value is a number of the MTEXT's heights.
    /// </para>
    /// <para>
    /// Written after the format of the paragraph before, only what differs is written, in AutoCAD's order (i, l, r, b, a, s, t),
    /// and what goes back to none is written as AutoCAD resets it: <c>\pi0,l0,r0,b0,a0,s*,tz;</c>.
    /// </para>
    /// </remarks>
    public sealed class CadMTextParagraphFormat : IEquatable<CadMTextParagraphFormat>
    {
        private CadMTextParagraphFormat(
            double first,
            double hanging,
            double right,
            IReadOnlyList<CadMTextTabStop> tabs,
            double before,
            double after,
            char lineSpacing,
            double lineSpacingValue
        )
        {
            FirstLineIndent = first;
            HangingIndent = hanging;
            RightIndent = right;
            TabStops = tabs;
            SpaceBefore = before;
            SpaceAfter = after;
            LineSpacing = lineSpacing;
            LineSpacingValue = lineSpacing == '\0' ? 1.0 : lineSpacingValue;
        }

        /// <summary>Gets the format of a paragraph with no codes: no indents, no stops, no spacing of its own.</summary>
        public static CadMTextParagraphFormat Default { get; } =
            new CadMTextParagraphFormat(0.0, 0.0, 0.0, Array.Empty<CadMTextTabStop>(), 0.0, 0.0, '\0', 1.0);

        /// <summary>Gets where the first line starts, from the left of the frame: the ruler's upper triangle.</summary>
        public double FirstLineIndent { get; }

        /// <summary>Gets where the second and later lines start, from the left of the frame: the ruler's lower triangle.</summary>
        public double HangingIndent { get; }

        /// <summary>Gets how far the lines stop short of the right of the frame.</summary>
        public double RightIndent { get; }

        /// <summary>Gets the tab stops, by position; past the last, a tab goes to the next multiple of four heights.</summary>
        public IReadOnlyList<CadMTextTabStop> TabStops { get; }

        /// <summary>Gets the space before the paragraph; nought for none.</summary>
        public double SpaceBefore { get; }

        /// <summary>Gets the space after the paragraph; nought for none.</summary>
        public double SpaceAfter { get; }

        /// <summary>Gets the paragraph's own line spacing: m multiple, e exactly, a at least; nought for the MTEXT's own.</summary>
        public char LineSpacing { get; }

        /// <summary>
        /// Gets the value of the line spacing: the factor of a multiple (1.5 for 1.5x), the distance of exactly and at least
        /// (2.5 spaces the lines of an MTEXT 2.5 high as 1x does); 1 where the paragraph has none of its own.
        /// </summary>
        public double LineSpacingValue { get; }

        /// <summary>Gets the format with other indents.</summary>
        /// <param name="first">Where the first line starts.</param>
        /// <param name="hanging">Where the later lines start.</param>
        /// <param name="right">How far the lines stop short of the right.</param>
        /// <returns>The format.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an indent is below nought or not a finite number.</exception>
        public CadMTextParagraphFormat WithIndents(double first, double hanging, double right)
        {
            Guard.NonNegative(first, nameof(first), "An indent is a finite number of nought or more.");
            Guard.NonNegative(hanging, nameof(hanging), "An indent is a finite number of nought or more.");
            Guard.NonNegative(right, nameof(right), "An indent is a finite number of nought or more.");
            return new CadMTextParagraphFormat(
                first,
                hanging,
                right,
                TabStops,
                SpaceBefore,
                SpaceAfter,
                LineSpacing,
                LineSpacingValue
            );
        }

        /// <summary>Gets the format with other tab stops, sorted by position; of two at one place the later is kept, as AutoCAD keeps it.</summary>
        /// <param name="stops">The stops.</param>
        /// <returns>The format.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stops or one of them is null.</exception>
        public CadMTextParagraphFormat WithTabStops(IEnumerable<CadMTextTabStop> stops)
        {
            if (stops == null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            var kept = new List<CadMTextTabStop>();

            foreach (CadMTextTabStop stop in stops)
            {
                if (stop == null)
                {
                    throw new ArgumentNullException(nameof(stops), "A tab stop is null.");
                }

                kept.RemoveAll(s => Same(s.Position, stop.Position));
                kept.Add(stop);
            }

            kept.Sort((a, b) => a.Position.CompareTo(b.Position));
            return new CadMTextParagraphFormat(
                FirstLineIndent,
                HangingIndent,
                RightIndent,
                kept.ToArray(),
                SpaceBefore,
                SpaceAfter,
                LineSpacing,
                LineSpacingValue
            );
        }

        /// <summary>Gets the format with other paragraph spacing.</summary>
        /// <param name="before">The space before, nought for none.</param>
        /// <param name="after">The space after, nought for none.</param>
        /// <returns>The format.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a space is below nought or not a finite number.</exception>
        public CadMTextParagraphFormat WithSpacing(double before, double after)
        {
            Guard.NonNegative(before, nameof(before), "A space is a finite number of nought or more.");
            Guard.NonNegative(after, nameof(after), "A space is a finite number of nought or more.");
            return new CadMTextParagraphFormat(
                FirstLineIndent,
                HangingIndent,
                RightIndent,
                TabStops,
                before,
                after,
                LineSpacing,
                LineSpacingValue
            );
        }

        /// <summary>Gets the format with a line spacing of its own, or none.</summary>
        /// <param name="mode">m multiple, e exactly, a at least; nought for the MTEXT's own.</param>
        /// <param name="value">The factor of a multiple, the distance of the others.</param>
        /// <returns>The format.</returns>
        /// <exception cref="ArgumentException">Thrown when the mode is none of m, e, a and nought.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a finite number above nought.</exception>
        public CadMTextParagraphFormat WithLineSpacing(char mode, double value)
        {
            if (mode != '\0' && "mea".IndexOf(mode) < 0)
            {
                throw new ArgumentException("A line spacing is m, e, a or nought.", nameof(mode));
            }

            if (mode != '\0')
            {
                Guard.Positive(value, nameof(value), "A line spacing is a finite number above nought.");
            }

            return new CadMTextParagraphFormat(
                FirstLineIndent,
                HangingIndent,
                RightIndent,
                TabStops,
                SpaceBefore,
                SpaceAfter,
                mode,
                value
            );
        }

        /// <summary>
        /// Reads the paragraph codes of a paragraph, in drawing units, over the format of the paragraph before it: a value
        /// after <c>x</c> is a number of heights, one without is a distance; <c>*</c> and <c>tz</c> reset; other codes are
        /// left alone. <c>\pxi-3.2,l4;</c> of an MTEXT 2.5 high starts the first line at 2 and the rest at 10.
        /// </summary>
        /// <param name="before">The format of the paragraph before; null for <see cref="Default"/>.</param>
        /// <param name="codes">The codes, any number of <c>\p...;</c>; null for none.</param>
        /// <param name="height">The MTEXT's height.</param>
        /// <returns>The format.</returns>
        public static CadMTextParagraphFormat Read(CadMTextParagraphFormat before, string codes, double height)
        {
            CadMTextParagraphFormat format = before ?? Default;

            if (string.IsNullOrEmpty(codes))
            {
                return format;
            }

            double first = format.FirstLineIndent;
            double hanging = format.HangingIndent;
            double indent = first - hanging;
            double right = format.RightIndent;
            List<CadMTextTabStop> tabs = format.TabStops.ToList();
            double spaceBefore = format.SpaceBefore;
            double spaceAfter = format.SpaceAfter;
            char mode = format.LineSpacing;
            double value = format.LineSpacingValue;

            foreach (string group in Groups(codes))
            {
                bool times = group.StartsWith("x", StringComparison.Ordinal);
                double unit = times ? height : 1.0;
                bool tabbing = false;

                foreach (string raw in Items(times ? group.Substring(1) : group))
                {
                    string item = raw;

                    if (item.Length == 0)
                    {
                        continue;
                    }

                    if (item[0] == 't' && !tabbing)
                    {
                        tabbing = true;
                        tabs.Clear();
                        item = item.Substring(1);
                    }

                    if (tabbing)
                    {
                        if (TryStop(item, unit, out CadMTextTabStop stop))
                        {
                            tabs.RemoveAll(s => Same(s.Position, stop.Position));
                            tabs.Add(stop);
                        }

                        continue;
                    }

                    string rest = item.Substring(1);
                    bool reset = rest == "*";
                    double number = 0.0;
                    bool known = reset || Number(rest, out number);

                    switch (item[0])
                    {
                        case 'i':
                            indent = known ? number * unit : indent;
                            break;
                        case 'l':
                            hanging = known ? number * unit : hanging;
                            break;
                        case 'r':
                            right = known ? number * unit : right;
                            break;
                        case 'b':
                            spaceBefore = known ? number * unit : spaceBefore;
                            break;
                        case 'a':
                            spaceAfter = known ? number * unit : spaceAfter;
                            break;
                        case 's':
                            if (reset || rest.Length == 0 || "mea".IndexOf(rest[0]) < 0)
                            {
                                mode = '\0';
                                value = 1.0;
                            }
                            else if (Number(rest.Substring(1), out double factor) && factor > 0.0)
                            {
                                mode = rest[0];
                                value = rest[0] == 'm' ? factor : factor * unit;
                            }

                            break;
                    }
                }
            }

            tabs.Sort((a, b) => a.Position.CompareTo(b.Position));
            return new CadMTextParagraphFormat(
                Math.Max(0.0, hanging + indent),
                Math.Max(0.0, hanging),
                Math.Max(0.0, right),
                tabs.ToArray(),
                Math.Max(0.0, spaceBefore),
                Math.Max(0.0, spaceAfter),
                mode,
                value
            );
        }

        /// <summary>
        /// Writes the codes that make this format of the format of the paragraph before, as AutoCAD writes them: only what
        /// differs, in the order i, l, r, b, a, s, t, after <c>x</c> in heights of the MTEXT where a value is not nought.
        /// A tab stop at 10 after a paragraph of none, in an MTEXT 2.5 high, writes <c>\pxt4;</c>; going back to none,
        /// <c>\ptz;</c>; the same format writes nothing.
        /// </summary>
        /// <param name="before">The format of the paragraph before; null for <see cref="Default"/>.</param>
        /// <param name="height">The MTEXT's height, above nought.</param>
        /// <returns>The codes; empty for none.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is not a finite number above nought.</exception>
        public string WriteAfter(CadMTextParagraphFormat before, double height)
        {
            Guard.Positive(height, nameof(height), "The height of an MTEXT is a finite number above nought.");
            before = before ?? Default;
            var items = new List<string>();
            bool any = false;

            void Add(char code, double distance)
            {
                double number = distance / height;
                any |= Math.Abs(number) > 1E-12;
                items.Add(code + Number(number));
            }

            double indent = FirstLineIndent - HangingIndent;

            if (!Same(indent, before.FirstLineIndent - before.HangingIndent))
            {
                Add('i', indent);
            }

            if (!Same(HangingIndent, before.HangingIndent))
            {
                Add('l', HangingIndent);
            }

            if (!Same(RightIndent, before.RightIndent))
            {
                Add('r', RightIndent);
            }

            if (!Same(SpaceBefore, before.SpaceBefore))
            {
                Add('b', SpaceBefore);
            }

            if (!Same(SpaceAfter, before.SpaceAfter))
            {
                Add('a', SpaceAfter);
            }

            if (LineSpacing != before.LineSpacing || !Same(LineSpacingValue, before.LineSpacingValue))
            {
                if (LineSpacing == '\0')
                {
                    items.Add("s*");
                }
                else
                {
                    double number = LineSpacing == 'm' ? LineSpacingValue : LineSpacingValue / height;
                    any = true;
                    items.Add("s" + LineSpacing + Number(number));
                }
            }

            if (!SameStops(TabStops, before.TabStops))
            {
                if (TabStops.Count == 0)
                {
                    items.Add("tz");
                }
                else
                {
                    var tabs = new StringBuilder("t");

                    for (int i = 0; i < TabStops.Count; i++)
                    {
                        CadMTextTabStop stop = TabStops[i];
                        tabs.Append(i == 0 ? string.Empty : ",");

                        if (stop.Kind == 'd' && stop.DecimalSeparator != '.')
                        {
                            tabs.Append('D').Append(stop.DecimalSeparator);
                        }
                        else if (stop.Kind != 'l')
                        {
                            tabs.Append(stop.Kind);
                        }

                        tabs.Append(Number(stop.Position / height));
                    }

                    any = true;
                    items.Add(tabs.ToString());
                }
            }

            return items.Count == 0 ? string.Empty : "\\p" + (any ? "x" : string.Empty) + string.Join(",", items) + ";";
        }

        /// <inheritdoc/>
        public bool Equals(CadMTextParagraphFormat other) =>
            other != null
            && Same(FirstLineIndent, other.FirstLineIndent)
            && Same(HangingIndent, other.HangingIndent)
            && Same(RightIndent, other.RightIndent)
            && Same(SpaceBefore, other.SpaceBefore)
            && Same(SpaceAfter, other.SpaceAfter)
            && LineSpacing == other.LineSpacing
            && Same(LineSpacingValue, other.LineSpacingValue)
            && SameStops(TabStops, other.TabStops);

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CadMTextParagraphFormat);

        /// <inheritdoc/>
        public override int GetHashCode() => TabStops.Count * 31 + LineSpacing;

        /// <summary>The <c>\p...;</c> groups of codes, each without its <c>\p</c> and <c>;</c>.</summary>
        internal static IEnumerable<string> Groups(string codes)
        {
            int at = 0;

            while (true)
            {
                int start = codes.IndexOf("\\p", at, StringComparison.Ordinal);
                int end = start < 0 ? -1 : codes.IndexOf(';', start);

                if (end < 0)
                {
                    yield break;
                }

                yield return codes.Substring(start + 2, end - start - 2);
                at = end + 1;
            }
        }

        /// <summary>
        /// The items of a group, split at commas but for the separator of a decimal stop, which AutoCAD writes after a capital
        /// D: <c>tD,16,20</c> is a decimal stop on a comma at 16 and a left one at 20.
        /// </summary>
        internal static List<string> Items(string group)
        {
            var items = new List<string>();
            int start = 0;

            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] == 'D' && i + 1 < group.Length)
                {
                    i++;
                    continue;
                }

                if (group[i] == ',')
                {
                    items.Add(group.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            items.Add(group.Substring(start).Trim());
            return items;
        }

        /// <summary>A tab stop of an item after the t: <c>4</c>, <c>c8</c>, <c>r12</c>, <c>d16</c>, <c>D,16</c>.</summary>
        internal static bool TryStop(string item, double unit, out CadMTextTabStop stop)
        {
            stop = null;
            char kind = 'l';
            char separator = '.';

            if (item.Length > 1 && item[0] == 'D')
            {
                kind = 'd';
                separator = item[1];
                item = item.Substring(2);
            }
            else if (item.Length > 0 && "lcrd".IndexOf(item[0]) >= 0)
            {
                kind = item[0];
                item = item.Substring(1);
            }

            if (!Number(item, out double place) || !(place > 0.0) || ". ,".IndexOf(separator) < 0)
            {
                return false;
            }

            stop = new CadMTextTabStop(place * unit, kind, separator);
            return true;
        }

        private static bool SameStops(IReadOnlyList<CadMTextTabStop> a, IReadOnlyList<CadMTextTabStop> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Same(double a, double b) => Math.Abs(a - b) <= Tolerance.Default.EqualPoint;

        private static string Number(double value) =>
            Math.Abs(value) < 5E-6 ? "0" : value.ToString("0.#####", CultureInfo.InvariantCulture);

        private static bool Number(string text, out double number) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && !double.IsNaN(number)
            && !double.IsInfinity(number);
    }
}
