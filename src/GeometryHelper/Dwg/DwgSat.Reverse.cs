using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The other way: the SAT of a body read back into the SAB it came from. A SAT value says what it is no better than its
    /// look does (0 is a whole number or a real, a vector is three numbers), so the records are read by the grammars of
    /// <see cref="ToSat(byte[])"/> turned round, and the records whose values the grammars leave to "*" (the transform, the
    /// edges and the tolerant ones, the point, the name attribute) by a layout of their own that gives each value its kind.
    /// A record, a subtype or a value that fits no grammar refuses the data, so that no SAB is written that is not AutoCAD's.
    /// </summary>
    internal static partial class DwgSat
    {
        /// <summary>The records whose values the grammars leave to "*": each value with its kind, as the SAB gives it.</summary>
        private static readonly Dictionary<string, string> Layouts = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["transform"] = "r i V V V V d b(no_rotate,!) b(no_reflect,!) b(no_shear,!)",
            ["point"] = "r i r P",
            ["straight-curve"] = "r i r P V I I",
            ["sphere-surface"] = "r i r P d V V b(forward_v,reverse_v) I I I I",
            ["edge"] = "r i r r d r d r r b(forward,!) s",
            ["tedge-edge"] = "r i r r d r d r r b(forward,!) s d",
            ["tcoedge-coedge"] = "r i r r r r r b(forward,reversed) r oi r d d or",
            ["tvertex-vertex"] = "r i r r oi r d",
            ["int64_attrib-name_attrib-gen-attrib"] = "r i r r r e(2=copy) e(4=keep_all) e(1=ignore) e(2=copy) s L",
        };

        /// <summary>The versions of ASM data whose records the grammars know: the SAT of R2004, R2007 and R2010 and the SAB of R2013 and R2018.</summary>
        private static bool IsKnownVersion(int version) =>
            version == 20800 || version == 21200 || version == 21500 || version == 21800 || version == 22300;

        /// <summary>
        /// The SAB of the SAT in <paramref name="lines"/> (the lines of the data of a body, as the groups of a DXF hold them,
        /// deciphered) in the form of <paramref name="form"/>, its date <paramref name="date"/>; null where the SAT holds a
        /// record, a subtype or a value the grammars do not place, or is of a version whose records are not known.
        /// </summary>
        internal static byte[] ToSab(IReadOnlyList<string> lines, AcisFormat form, string date)
        {
            SatBody sat = ParseSat(lines);

            if (sat == null || !Adjust(sat.Records, sat.Version, form))
            {
                return null;
            }

            return Encode(sat.Records, form, sat.Roots, sat.Strings[0], sat.Strings[1], date, sat.Reals);
        }

        /// <summary>
        /// The SAT in <paramref name="lines"/> in the form of <paramref name="form"/>, another version of the modeler's text:
        /// the head's version and flags the form's, a vertex's number in or out. Null where the SAT holds what the grammars do
        /// not place, or is of a version whose records are not known (that of R2000, ACIS 4.00, is).
        /// </summary>
        internal static string ToSat(IReadOnlyList<string> lines, AcisFormat form)
        {
            SatBody sat = ParseSat(lines);

            if (sat == null || !Adjust(sat.Records, sat.Version, form))
            {
                return null;
            }

            return Print(sat.Records, sat.Records.Count, form.Version, sat.Roots, form.Flags, sat.Strings, sat.Reals);
        }

        /// <summary>The head and the records of a SAT, the records as the values of SAB.</summary>
        private sealed class SatBody
        {
            public int Version;
            public int Roots;
            public readonly List<string> Strings = new List<string>();
            public readonly List<double> Reals = new List<double>();
            public readonly List<List<Token>> Records = new List<List<Token>>();
        }

        /// <summary>Reads the SAT of a body into its head and records; null where it holds a record, a subtype or a value the grammars do not place.</summary>
        private static SatBody ParseSat(IReadOnlyList<string> lines)
        {
            var text = new StringBuilder();

            foreach (string line in lines)
            {
                text.Append(line).Append('\n');
            }

            string sat = text.ToString();
            int at = 0;
            var read = new SatBody();

            if (
                !WholeOf(sat, ref at, out read.Version)
                || !WholeOf(sat, ref at, out int _)
                || !WholeOf(sat, ref at, out read.Roots)
                || !WholeOf(sat, ref at, out int _)
                || !IsKnownVersion(read.Version)
            )
            {
                return null;
            }

            for (int i = 0; i < 3; i++)
            {
                if (!WholeOf(sat, ref at, out int length) || length < 0)
                {
                    return null;
                }

                if (length > 0)
                {
                    at++;

                    if (at + length > sat.Length)
                    {
                        return null;
                    }

                    read.Strings.Add(sat.Substring(at, length));
                    at += length;
                }
                else
                {
                    read.Strings.Add(string.Empty);
                }
            }

            if (read.Strings[0].Length == 0 || read.Strings[1].Length == 0)
            {
                return null;
            }

            for (int i = 0; i < 3; i++)
            {
                string word = WordOf(sat, ref at);

                if (word == null || !Real(word, out double real))
                {
                    return null;
                }

                read.Reals.Add(real);
            }

            while (true)
            {
                List<RawToken> raw = RecordOf(sat, ref at, out bool ended);

                if (raw == null)
                {
                    return null;
                }

                if (!ended)
                {
                    break;
                }

                var reader = new Reader(raw);

                if (!reader.Record())
                {
                    return null;
                }

                read.Records.Add(reader.Result);
            }

            return read.Records.Count == 0 ? null : read;
        }

        #region Reading the SAT

        /// <summary>A value of SAT before the grammars give it its kind: a text (<c>@7 unknown</c>) or the word between two spaces.</summary>
        private readonly struct RawToken
        {
            internal RawToken(bool text, string value)
            {
                IsText = text;
                Value = value;
            }

            internal bool IsText { get; }

            internal string Value { get; }
        }

        private static void SkipSpace(string text, ref int at)
        {
            while (at < text.Length && (text[at] == ' ' || text[at] == '\n' || text[at] == '\r' || text[at] == '\t'))
            {
                at++;
            }
        }

        /// <summary>The next word, up to the space after it; null at the end.</summary>
        private static string WordOf(string text, ref int at)
        {
            SkipSpace(text, ref at);

            if (at >= text.Length)
            {
                return null;
            }

            int start = at;

            while (at < text.Length && text[at] != ' ' && text[at] != '\n' && text[at] != '\r' && text[at] != '\t')
            {
                at++;
            }

            return text.Substring(start, at - start);
        }

        private static bool WholeOf(string text, ref int at, out int value)
        {
            string word = WordOf(text, ref at);
            value = 0;
            return word != null
                && int.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// The values of the next record, up to its hash: <paramref name="ended"/> false and an empty list where the data ends
        /// with none; null where a text runs past the end.
        /// </summary>
        private static List<RawToken> RecordOf(string text, ref int at, out bool ended)
        {
            var values = new List<RawToken>();
            ended = false;

            while (true)
            {
                SkipSpace(text, ref at);

                if (at >= text.Length)
                {
                    return values.Count == 0 ? values : null;
                }

                if (text[at] == '#')
                {
                    at++;
                    ended = true;
                    return values;
                }

                if (text[at] == '@')
                {
                    int digits = at + 1;

                    while (digits < text.Length && text[digits] >= '0' && text[digits] <= '9')
                    {
                        digits++;
                    }

                    if (
                        digits == at + 1
                        || !int.TryParse(
                            text.Substring(at + 1, digits - at - 1),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int length
                        )
                        || digits + 1 + length > text.Length
                    )
                    {
                        return null;
                    }

                    values.Add(new RawToken(true, text.Substring(digits + 1, length)));
                    at = digits + 1 + length;
                    continue;
                }

                values.Add(new RawToken(false, WordOf(text, ref at)));
            }
        }

        /// <summary>A real as the SAT writes it; -0 keeps its sign, which the framework's parser drops.</summary>
        private static bool Real(string word, out double value)
        {
            if (
                !double.TryParse(
                    word,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture,
                    out value
                )
                || double.IsNaN(value)
                || double.IsInfinity(value)
            )
            {
                return false;
            }

            if (value == 0.0 && word[0] == '-')
            {
                value = -0.0;
            }

            return true;
        }

        private static bool IsWhole(string word) =>
            word.Length > 0
            && int.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int _);

        /// <summary>The value a word names in the spec of a flag or an enum ("w0,w1" by place, "2=w,4=v" by value); false where it names none.</summary>
        private static bool ValueOf(string spec, string word, out long value, out bool byPlace)
        {
            value = 0;
            byPlace = spec.IndexOf('=') < 0;
            string[] parts = spec.Split(',');

            for (int i = 0; i < parts.Length; i++)
            {
                if (byPlace)
                {
                    if (parts[i] == word && word != "!")
                    {
                        value = i;
                        return true;
                    }

                    continue;
                }

                int equals = parts[i].IndexOf('=');

                if (equals > 0 && parts[i].Substring(equals + 1) == word)
                {
                    value = long.Parse(parts[i].Substring(0, equals), CultureInfo.InvariantCulture);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Reads one record of SAT by its grammar into the values of SAB: <see cref="Writer"/> turned round.</summary>
        private sealed class Reader
        {
            private readonly List<RawToken> _raw;
            private int _at;
            private long _last;

            public Reader(List<RawToken> raw)
            {
                _raw = raw;
                Result = new List<Token>();
            }

            /// <summary>The values of the record, its name first, as the SAB holds them.</summary>
            public List<Token> Result { get; }

            private bool Done => _at >= _raw.Count;

            /// <summary>The next value if it is a word, not a text.</summary>
            private string Peek => Done || _raw[_at].IsText ? null : _raw[_at].Value;

            public bool Record()
            {
                string name = Peek;

                if (name == null || name.Length == 0)
                {
                    return false;
                }

                _at++;
                string[] parts = name.Split('-');

                for (int i = 0; i < parts.Length; i++)
                {
                    Result.Add(new Token { Kind = i < parts.Length - 1 ? 'n' : 'N', Text = parts[i] });
                }

                if (!Layouts.TryGetValue(name, out string grammar) && !Records.TryGetValue(name, out grammar))
                {
                    return false;
                }

                string[] items = Items(grammar);
                return Run(items, 0, items.Length) && Done;
            }

            private bool Pointer()
            {
                string word = Peek;

                if (word == null || word.Length < 2 || word[0] != '$' || !IsWhole(word.Substring(1)))
                {
                    return false;
                }

                _at++;
                Result.Add(
                    new Token { Kind = 'r', Whole = long.Parse(word.Substring(1), CultureInfo.InvariantCulture) }
                );
                return true;
            }

            private bool Whole(char kind)
            {
                string word = Peek;

                if (
                    word == null
                    || !IsWhole(word)
                    || !long.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
                )
                {
                    return false;
                }

                _at++;
                _last = value;
                Result.Add(new Token { Kind = kind, Whole = value });
                return true;
            }

            private bool Number(char kind)
            {
                string word = Peek;

                if (word == null || !Real(word, out double value))
                {
                    return false;
                }

                _at++;
                Result.Add(new Token { Kind = kind, Real = value });
                return true;
            }

            private bool Triple(char kind, bool unit)
            {
                var values = new double[3];

                for (int i = 0; i < 3; i++)
                {
                    string word = Peek;

                    if (word == null || !Real(word, out values[i]))
                    {
                        return false;
                    }

                    _at++;
                }

                Result.Add(
                    new Token
                    {
                        Kind = kind,
                        Triple = values,
                        Exact = unit,
                    }
                );
                return true;
            }

            private bool Text()
            {
                if (Done || !_raw[_at].IsText)
                {
                    return false;
                }

                Result.Add(new Token { Kind = 's', Text = _raw[_at++].Value });
                return true;
            }

            /// <summary>A word of the next value, taken as a flag or an enum of the spec; false (nothing taken) where it is not one.</summary>
            private bool Worded(string spec, char kind, bool optional)
            {
                string word = Peek;

                if (word != null && ValueOf(spec, word, out long value, out bool byPlace))
                {
                    _at++;
                    Result.Add(new Token { Kind = byPlace ? 'b' : 'e', Whole = value });
                    return true;
                }

                return optional;
            }

            /// <summary>Whatever of a pointer, a whole number, a real or a text stands next, until a word of another kind or the end.</summary>
            private void Any()
            {
                while (!Done)
                {
                    if (_raw[_at].IsText)
                    {
                        Text();
                        continue;
                    }

                    string word = _raw[_at].Value;

                    if (word.Length > 1 && word[0] == '$')
                    {
                        if (!Pointer())
                        {
                            return;
                        }
                    }
                    else if (IsWhole(word))
                    {
                        Whole('i');
                    }
                    else if (!Number('d'))
                    {
                        return;
                    }
                }
            }

            private bool Run(string[] items, int from, int to)
            {
                for (int i = from; i < to; i++)
                {
                    if (items[i] != "[")
                    {
                        if (!Item(items[i]))
                        {
                            return false;
                        }

                        continue;
                    }

                    int depth = 1;
                    int end = i + 1;

                    while (depth > 0)
                    {
                        depth +=
                            items[end] == "[" ? 1
                            : items[end] == "]" ? -1
                            : 0;
                        end++;
                    }

                    long times = _last;

                    if (times < 0 || times > _raw.Count)
                    {
                        return false;
                    }

                    for (long n = 0; n < times; n++)
                    {
                        if (!Run(items, i + 1, end - 1))
                        {
                            return false;
                        }
                    }

                    i = end - 1;
                }

                return true;
            }

            private bool Item(string item)
            {
                switch (item)
                {
                    case "*":
                        Any();
                        return true;
                    case "r":
                        return Pointer();
                    case "d":
                        return Number('d');
                    case "P":
                        return Triple('P', false);
                    case "V":
                        return Triple('V', false);
                    case "U":
                        return Triple('V', true);
                    case "s":
                        return Text();
                    case "i":
                        return Whole('i');
                    case "L":
                        return Whole('L');
                    case "oi":
                        return Peek == null || !IsWhole(Peek) || Whole('i');
                    case "or":
                        return Peek == null || !Peek.StartsWith("$", StringComparison.Ordinal) || Pointer();
                    case "/":
                        return true;
                    case "I":
                        // A bound: I where there is none, F and the bound where there is.
                        if (Peek == "I")
                        {
                            _at++;
                            Result.Add(new Token { Kind = 'b', Whole = 0 });
                            return true;
                        }

                        if (Peek != "F")
                        {
                            return false;
                        }

                        _at++;
                        Result.Add(new Token { Kind = 'b', Whole = 1 });
                        return Number('d');
                    case "{}":
                        return Subtype();
                    case "o{}":
                        return Peek != "{" || Subtype();
                    case "SURF":
                        return InLine(Surfaces);
                    case "CURVE":
                        return InLine(Curves);
                    case "PCUR":
                        return Spline(2);
                    case "A":
                        if (!Whole('i'))
                        {
                            return false;
                        }

                        long count = _last;

                        for (long n = 0; n < count; n++)
                        {
                            if (!Number('d'))
                            {
                                return false;
                            }
                        }

                        return true;
                    case "BS3C":
                        return Spline(3);
                    case "BS2C":
                        return Spline(2);
                    case "BS3S":
                        return SplineSurface();
                }

                if (item.StartsWith("b(", StringComparison.Ordinal) || item.StartsWith("e(", StringComparison.Ordinal))
                {
                    return Worded(item.Substring(2, item.Length - 3), item[0], false);
                }

                if (item.StartsWith("o(", StringComparison.Ordinal))
                {
                    return Worded(item.Substring(2, item.Length - 3), 'o', true);
                }

                throw new InvalidOperationException("The grammar of SAT names an item that is not known: " + item);
            }

            private bool Name(out string name)
            {
                name = Peek;

                if (name == null)
                {
                    return false;
                }

                _at++;
                Result.Add(new Token { Kind = 'N', Text = name });
                return true;
            }

            private bool Subtype()
            {
                if (Peek != "{")
                {
                    return false;
                }

                _at++;
                Result.Add(new Token { Kind = '{' });

                if (!Name(out string name) || !Subtypes.TryGetValue(name, out string grammar))
                {
                    return false;
                }

                string[] items = Items(grammar);

                if (!Run(items, 0, items.Length) || Peek != "}")
                {
                    return false;
                }

                _at++;
                Result.Add(new Token { Kind = '}' });
                return true;
            }

            private bool InLine(Dictionary<string, string> kinds)
            {
                if (!Name(out string name) || !kinds.TryGetValue(name, out string grammar))
                {
                    return false;
                }

                string[] items = Items(grammar);
                return Run(items, 0, items.Length);
            }

            private bool Knots(long count, out long sum)
            {
                sum = 0;

                for (long i = 0; i < count; i++)
                {
                    if (!Number('d') || !Whole('i') || _last < 0)
                    {
                        return false;
                    }

                    sum += _last;
                }

                return true;
            }

            private bool Points(long count, int size)
            {
                if (count < 0 || count * size > _raw.Count - _at)
                {
                    return false;
                }

                for (long i = 0; i < count * size; i++)
                {
                    if (!Number('d'))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>The name of a spline: nullbs (taken, true, and no more), nubs or nurbs (taken, <paramref name="rational"/> for the second), false where it is none.</summary>
            private bool SplineName(out bool rational, out bool none)
            {
                rational = false;
                none = false;

                if (Peek == "nullbs")
                {
                    none = true;
                    return Name(out string _);
                }

                if ((Peek != "nubs" && Peek != "nurbs") || !Name(out string kind))
                {
                    return false;
                }

                rational = kind == "nurbs";
                return true;
            }

            private bool Spline(int dimension)
            {
                if (!SplineName(out bool rational, out bool none))
                {
                    return false;
                }

                if (none)
                {
                    return true;
                }

                if (!Whole('i'))
                {
                    return false;
                }

                long degree = _last;

                if (
                    !Worded("0=open,1=closed,2=periodic", 'e', false)
                    || !Whole('i')
                    || _last < 0
                    || _last > _raw.Count
                    || !Knots(_last, out long sum)
                )
                {
                    return false;
                }

                return Points(sum - degree + 1, dimension + (rational ? 1 : 0));
            }

            private bool SplineSurface()
            {
                if (!SplineName(out bool rational, out bool none))
                {
                    return false;
                }

                if (none)
                {
                    return true;
                }

                if (!Whole('i'))
                {
                    return false;
                }

                long u = _last;

                if (!Whole('i'))
                {
                    return false;
                }

                long v = _last;

                if (
                    !Worded("0=open,1=closed,2=periodic", 'e', false)
                    || !Worded("0=open,1=closed,2=periodic", 'e', false)
                    || !Worded("0=none", 'e', false)
                    || !Worded("0=none", 'e', false)
                    || !Whole('i')
                )
                {
                    return false;
                }

                long us = _last;

                if (!Whole('i'))
                {
                    return false;
                }

                long vs = _last;

                if (us < 0 || vs < 0 || us + vs > _raw.Count || !Knots(us, out long uSum) || !Knots(vs, out long vSum))
                {
                    return false;
                }

                return Points((uSum - u + 1) * (vSum - v + 1), 3 + (rational ? 1 : 0));
            }
        }

        #endregion

        #region Between the forms

        /// <summary>The index of a record's first value: past the parts of its name.</summary>
        private static int FirstValue(List<Token> record)
        {
            int at = 0;

            while (at < record.Count && record[at].Kind == 'n')
            {
                at++;
            }

            return at + 1;
        }

        private static string NameOf(List<Token> record)
        {
            var name = new StringBuilder();
            int first = FirstValue(record);

            for (int i = 0; i < first && i < record.Count; i++)
            {
                name.Append(i > 0 ? "-" : string.Empty).Append(record[i].Text);
            }

            return name.ToString();
        }

        /// <summary>
        /// Gives the records the numbers the form has and the version they come from has not, or takes out those it has not: a
        /// coedge's (a whole number after the face, in the SAB of R2013) and a vertex's (a whole number after its edge, from
        /// R2007). The number of a vertex that has none is that of the end it stands at, 0 at the start of its edge and 1 at the
        /// end of it, which AutoCAD writes for most; a vertex of a closed edge has 0, where AutoCAD gives 2 to the seam of a swept
        /// surface. False where the version is one whose records are not known.
        /// </summary>
        private static bool Adjust(List<List<Token>> records, int from, AcisFormat to)
        {
            if (!IsKnownVersion(from))
            {
                return false;
            }

            bool hadCoedge = from >= 21800;
            bool hadVertex = from >= 21200;
            int fromForm =
                from >= 21500 ? 2
                : from >= 21200 ? 1
                : 0;
            int toForm =
                to.Version >= 21500 ? 2
                : to.Version >= 21200 ? 1
                : 0;

            for (int i = 0; i < records.Count; i++)
            {
                List<Token> record = records[i];
                string name = NameOf(record);
                int v = FirstValue(record);

                if (fromForm != toForm && !Splines(record, fromForm, toForm))
                {
                    return false;
                }

                // The tolerance of a tolerant vertex of R2004 is -1 from R2007 on, where AutoCAD leaves it to be worked out again.
                if (name == "tvertex-vertex" && (fromForm == 0) != (toForm == 0))
                {
                    if (toForm == 0 || record[record.Count - 1].Kind != 'd')
                    {
                        return false;
                    }

                    record[record.Count - 1] = new Token { Kind = 'd', Real = -1.0 };
                }

                // A tolerant coedge of R2007 and later ends with a pointer more than that of R2004.
                if (name == "tcoedge-coedge" && (fromForm == 0) != (toForm == 0))
                {
                    if (toForm == 0)
                    {
                        if (record[record.Count - 1].Kind != 'r')
                        {
                            return false;
                        }

                        record.RemoveAt(record.Count - 1);
                    }
                    else
                    {
                        record.Add(new Token { Kind = 'r', Whole = -1 });
                    }
                }

                if ((name == "coedge" || name == "tcoedge-coedge") && hadCoedge != to.CoedgeNumber)
                {
                    // r i r r r r r b r [i] r: the number stands after the face (the pointer after the flag).
                    if (record.Count <= v + 9 || record[v + 7].Kind != 'b' || record[v + 8].Kind != 'r')
                    {
                        return false;
                    }

                    if (hadCoedge)
                    {
                        if (record[v + 9].Kind != 'i')
                        {
                            return false;
                        }

                        record.RemoveAt(v + 9);
                    }
                    else
                    {
                        record.Insert(v + 9, new Token { Kind = 'i', Whole = 0 });
                    }
                }
                else if ((name == "vertex" || name == "tvertex-vertex") && hadVertex != to.VertexNumber)
                {
                    // r i r r [i] r: the number stands after the edge.
                    if (record.Count <= v + 4 || record[v + 3].Kind != 'r')
                    {
                        return false;
                    }

                    if (hadVertex)
                    {
                        if (record[v + 4].Kind != 'i')
                        {
                            return false;
                        }

                        record.RemoveAt(v + 4);
                    }
                    else
                    {
                        record.Insert(v + 4, new Token { Kind = 'i', Whole = EndOf(records, i, record[v + 3].Whole) });
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Gives the splines of a record the form of another version of the modeler, one step at a time: R2004 (0), R2007 (1) and
        /// R2010 and later (2) each name and hold the curves and surfaces of a spline their own way. False where a spline of the
        /// record has no form in the other version that is known (a curve of intersection of two surfaces, a cylinder swept along
        /// a curve).
        /// </summary>
        private static bool Splines(List<Token> record, int from, int to)
        {
            if (!record.Exists(t => t.Kind == '{'))
            {
                return true;
            }

            // AutoCAD 2021 reads an exact spline surface of R2004 and R2007 in no data of R2010 or later (a modeling error, a solid
            // of no volume): it writes the surface in another form there (a lofted one), which no grammar of ours gives.
            if (
                to == 2
                && from < 2
                && record.Exists(t => t.Kind == 'N' && (t.Text == "exact_spl_sur" || t.Text == "exactsur"))
            )
            {
                return false;
            }

            for (; from < to; from++)
            {
                if (from == 0 ? !Step(record, true) : !Numbers(record, true))
                {
                    return false;
                }
            }

            for (; from > to; from--)
            {
                if (from == 2 ? !Numbers(record, false) : !Step(record, false))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The index of the closing brace of the subtype that opens at <paramref name="open"/>; -1 where it has none.</summary>
        private static int Close(List<Token> record, int open)
        {
            int depth = 0;

            for (int i = open; i < record.Count; i++)
            {
                depth +=
                    record[i].Kind == '{' ? 1
                    : record[i].Kind == '}' ? -1
                    : 0;

                if (depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The index where the last <paramref name="bounds"/> bounds before <paramref name="close"/> begin (an I, or an F and its value each); -1 where they are not there.</summary>
        private static int BoundsAt(List<Token> record, int close, int bounds)
        {
            int at = close;

            for (int n = 0; n < bounds; n++)
            {
                if (at >= 2 && record[at - 1].Kind == 'd' && record[at - 2].Kind == 'b' && record[at - 2].Whole == 1)
                {
                    at -= 2;
                }
                else if (at >= 1 && record[at - 1].Kind == 'b' && record[at - 1].Whole == 0)
                {
                    at -= 1;
                }
                else
                {
                    return -1;
                }
            }

            return at;
        }

        /// <summary>
        /// Between the forms of R2004 and R2007: the names of the exact curve and surface and the parametric curve of intersection
        /// change (exactcur, exactsur, parcur, exppc to exact_int_cur, exact_spl_sur, par_int_cur, exp_par_cur), the first and the
        /// third hold a whole number of nought before what they ended with, and the surface the number 22600 at its head and a
        /// word that says it is unextended at its tail. False for the others, whose forms are not known.
        /// </summary>
        private static bool Step(List<Token> record, bool up)
        {
            for (int i = 0; i + 1 < record.Count; i++)
            {
                if (record[i].Kind != '{' || record[i + 1].Kind != 'N')
                {
                    continue;
                }

                int close = Close(record, i);
                string name = record[i + 1].Text;

                if (close < 0)
                {
                    return false;
                }

                // The form of R2004 is the one named in either direction: its own going up, the one of R2007 turned back going down.
                switch (up ? name : Old(name))
                {
                    case "exactcur":
                    {
                        int at = BoundsAt(record, close, 2);

                        if (at < 0 || (!up && (at < 1 || record[at - 1].Kind != 'i')))
                        {
                            return false;
                        }

                        if (up)
                        {
                            record.Insert(at, new Token { Kind = 'i', Whole = 0 });
                        }
                        else
                        {
                            record.RemoveAt(at - 1);
                        }

                        break;
                    }
                    case "parcur":
                    {
                        int at = close - 1;

                        if (record[at].Kind != 'b' || (!up && (at < 1 || record[at - 1].Kind != 'i')))
                        {
                            return false;
                        }

                        if (up)
                        {
                            record.Insert(at, new Token { Kind = 'i', Whole = 0 });
                        }
                        else
                        {
                            record.RemoveAt(at - 1);
                        }

                        break;
                    }
                    case "exactsur":
                        if (up)
                        {
                            record.Insert(close, new Token { Kind = 'e', Whole = 0 });
                            record.Insert(i + 2, new Token { Kind = 'i', Whole = 22600 });
                        }
                        else if (record[i + 2].Kind != 'i' || record[close - 1].Kind != 'e')
                        {
                            return false;
                        }
                        else
                        {
                            record.RemoveAt(close - 1);
                            record.RemoveAt(i + 2);
                        }

                        break;
                    case "exppc":
                        break;
                    case "surfintcur":
                    case "cylsur":
                    case "int_int_cur":
                    case "cyl_spl_sur":
                        return false;
                }

                record[i + 1] = new Token { Kind = 'N', Text = up ? Newer(name) : Old(name) };
            }

            return true;
        }

        /// <summary>The name of a subtype of R2004 in R2007 and later; the name itself where it has no other.</summary>
        private static string Newer(string name)
        {
            switch (name)
            {
                case "exactcur":
                    return "exact_int_cur";
                case "exactsur":
                    return "exact_spl_sur";
                case "parcur":
                    return "par_int_cur";
                case "exppc":
                    return "exp_par_cur";
                default:
                    return name;
            }
        }

        /// <summary>The name of a subtype of R2007 and later in R2004; the name itself where it has no other.</summary>
        private static string Old(string name)
        {
            switch (name)
            {
                case "exact_int_cur":
                    return "exactcur";
                case "exact_spl_sur":
                    return "exactsur";
                case "par_int_cur":
                    return "parcur";
                case "exp_par_cur":
                    return "exppc";
                default:
                    return name;
            }
        }

        /// <summary>
        /// Between the forms of R2007 and R2010: the curves of intersection of the exact, interpolated and parametric kinds carry
        /// the number of their version after their name (22600), and the exact curve two words that say it is unextended before its
        /// closing brace.
        /// </summary>
        private static bool Numbers(List<Token> record, bool put)
        {
            for (int i = 0; i + 1 < record.Count; i++)
            {
                if (record[i].Kind != '{' || record[i + 1].Kind != 'N')
                {
                    continue;
                }

                string name = record[i + 1].Text;

                if (name != "exact_int_cur" && name != "int_int_cur" && name != "par_int_cur")
                {
                    continue;
                }

                bool has = i + 2 < record.Count && record[i + 2].Kind == 'i';

                if (put && !has)
                {
                    record.Insert(i + 2, new Token { Kind = 'i', Whole = 22600 });
                }
                else if (!put && has)
                {
                    record.RemoveAt(i + 2);
                }

                if (name != "exact_int_cur")
                {
                    continue;
                }

                int close = Close(record, i);

                if (close < 0)
                {
                    return false;
                }

                if (put)
                {
                    if (record[close - 1].Kind != 'e')
                    {
                        record.Insert(close, new Token { Kind = 'e', Whole = 0 });
                        record.Insert(close, new Token { Kind = 'e', Whole = 0 });
                    }
                }
                else
                {
                    for (int n = 0; n < 2 && record[close - 1].Kind == 'e'; n++)
                    {
                        record.RemoveAt(close - 1);
                        close--;
                    }
                }
            }

            return true;
        }

        /// <summary>The number of a vertex: 0 where it opens the edge its record names, 1 where it closes it.</summary>
        private static long EndOf(List<List<Token>> records, int vertex, long edge)
        {
            if (edge < 0 || edge >= records.Count)
            {
                return 0;
            }

            List<Token> record = records[(int)edge];
            int v = FirstValue(record);
            string name = NameOf(record);

            if ((name != "edge" && name != "tedge-edge") || record.Count <= v + 5)
            {
                return 0;
            }

            return record[v + 3].Whole != vertex && record[v + 5].Whole == vertex ? 1 : 0;
        }

        #endregion

        #region Writing the SAB

        /// <summary>The SAB of the records and the head given: the tag of each value, the end of each record, the name of the end of the data.</summary>
        private static byte[] Encode(
            List<List<Token>> records,
            AcisFormat form,
            int roots,
            string product,
            string modeler,
            string date,
            List<double> reals
        )
        {
            var bytes = new List<byte>(
                Encoding.ASCII.GetBytes(form.Version >= 22000 ? "ASM BinaryFile4" : "ACIS BinaryFile")
            );
            Put(bytes, form.Version);
            Put(bytes, 0);
            Put(bytes, roots);
            Put(bytes, form.Flags);
            PutText(bytes, product);
            PutText(bytes, modeler);
            PutText(bytes, date);

            foreach (double real in reals)
            {
                bytes.Add(0x06);
                bytes.AddRange(BitConverter.GetBytes(real));
            }

            foreach (List<Token> record in records)
            {
                foreach (Token token in record)
                {
                    switch (token.Kind)
                    {
                        case 'i':
                            bytes.Add(0x04);
                            Put(bytes, (int)token.Whole);
                            break;
                        case 'L':
                            bytes.Add(0x17);
                            bytes.AddRange(BitConverter.GetBytes(token.Whole));
                            break;
                        case 'r':
                            bytes.Add(0x0C);
                            Put(bytes, (int)token.Whole);
                            break;
                        case 'e':
                            bytes.Add(0x15);
                            Put(bytes, (int)token.Whole);
                            break;
                        case 'd':
                            bytes.Add(0x06);
                            bytes.AddRange(BitConverter.GetBytes(token.Real));
                            break;
                        case 'P':
                        case 'V':
                            bytes.Add(token.Kind == 'P' ? (byte)0x13 : (byte)0x14);

                            foreach (double value in token.Triple)
                            {
                                bytes.AddRange(BitConverter.GetBytes(value));
                            }

                            break;
                        case 's':
                            PutText(bytes, token.Text);
                            break;
                        case 'N':
                        case 'n':
                            bytes.Add(token.Kind == 'N' ? (byte)0x0D : (byte)0x0E);
                            bytes.Add((byte)token.Text.Length);
                            bytes.AddRange(Encoding.ASCII.GetBytes(token.Text));
                            break;
                        case 'b':
                            bytes.Add(token.Whole != 0 ? (byte)0x0A : (byte)0x0B);
                            break;
                        case '{':
                            bytes.Add(0x0F);
                            break;
                        case '}':
                            bytes.Add(0x10);
                            break;
                    }
                }

                bytes.Add(0x11);
            }

            foreach (string part in new[] { "End", "of", "ASM", "data" })
            {
                bytes.Add(part == "data" ? (byte)0x0D : (byte)0x0E);
                bytes.Add((byte)part.Length);
                bytes.AddRange(Encoding.ASCII.GetBytes(part));
            }

            return bytes.ToArray();
        }

        private static void Put(List<byte> bytes, int value) => bytes.AddRange(BitConverter.GetBytes(value));

        /// <summary>A text: the tag 0x07 and a length of a byte, or 0x08 and a length of two bytes past 255.</summary>
        private static void PutText(List<byte> bytes, string text)
        {
            if (text.Length < 256)
            {
                bytes.Add(0x07);
                bytes.Add((byte)text.Length);
            }
            else
            {
                bytes.Add(0x08);
                bytes.AddRange(BitConverter.GetBytes((ushort)text.Length));
            }

            bytes.AddRange(Encoding.ASCII.GetBytes(text));
        }

        #endregion
    }
}
