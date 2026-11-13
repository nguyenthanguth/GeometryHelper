using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The ACIS data of a REGION, 3DSOLID or BODY of a DWG of R2004 to R2010, which AutoCAD keeps as SAB, binary, and
    /// writes in DXF as SAT, text: the SAB turned into the SAT AutoCAD writes of it, line for line. SAB gives each value
    /// a tag of its kind (a whole number, a real, a position, a reference, a word of the record's own), where SAT gives
    /// the words of a flag (<c>forward</c> or <c>reversed</c>, <c>I</c> or <c>F</c> before a bound) and breaks the data
    /// of a spline over lines; which word a flag takes and where a line ends depend on the record and on the subtype a
    /// spline holds, and the grammars below give them for the records and subtypes AutoCAD writes for its solids, as
    /// AutoCAD 2021 writes them in DXF of R2004, R2007 and R2010. A value the grammars do not place, or a record or
    /// subtype they do not know, refuses the data, so that no SAT is written that is not AutoCAD's.
    /// </summary>
    /// <remarks>
    /// A real is written to 19 significant digits, the shortest of them, as C's <c>%.19g</c> does: 25.4 is
    /// 25.39999999999999858 and 1E-6 is 9.999999999999999547e-07; -0 keeps its sign. The normal of a plane, an ellipse,
    /// a cone and a torus is divided by its length first, as AutoCAD writes it. The head gives the version, the count of
    /// records, the count of bodies and the flags of the SAB, the count followed by spaces to a width of 12; then the
    /// product and the ACIS version, each by its length, and an empty date, as AutoCAD writes it; then the unit and the
    /// two tolerances of the SAB.
    /// </remarks>
    internal static partial class DwgSat
    {
        /// <summary>The records of the SAB whose flags and enums take words, each by its name: the grammar of its values.</summary>
        private static readonly Dictionary<string, string> Records = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["face"] = "* b(forward,reversed) b(single,double) o(out,in)",
            ["coedge"] = "* b(forward,reversed) *",
            ["tcoedge-coedge"] = "* b(forward,reversed) *",
            ["edge"] = "* b(forward,!) *",
            ["tedge-edge"] = "* b(forward,!) *",
            ["transform"] = "* b(no_rotate,!) b(no_reflect,!) b(no_shear,!)",
            ["plane-surface"] = "r i r P U V b(forward_v,reverse_v) I I I I",
            ["sphere-surface"] = "* b(forward_v,reverse_v) I I I I",
            ["torus-surface"] = "r i r P U d d V b(forward_v,reverse_v) I I I I",
            ["cone-surface"] = "r i r P U V d I I d d d b(forward,!) I I I I",
            ["straight-curve"] = "* I I",
            ["ellipse-curve"] = "r i r P U V d I I",
            ["intcurve-curve"] = "* b(forward,reversed) {} I I",
            ["spline-surface"] = "* b(forward,reversed) {} I I I I",
            ["pcurve"] = "* o(forward,reversed) o{} *",
            ["int64_attrib-name_attrib-gen-attrib"] = "* e(2=copy) e(4=keep_all) e(1=ignore) e(2=copy) *",
            ["asmheader"] = "*",
            ["body"] = "*",
            ["lump"] = "*",
            ["shell"] = "*",
            ["loop"] = "*",
            ["vertex"] = "*",
            ["point"] = "*",
            ["tvertex-vertex"] = "*",
            ["persubent-acadSolidHistory-attrib"] = "*",
            ["color-adesk-attrib"] = "*",
            ["truecolor-adesk-attrib"] = "*",
        };

        /// <summary>The curve of intersection the subtypes of an intcurve share: its spline, its surfaces and parameter curves, its range and its discontinuities.</summary>
        private const string IntCurve = "e(0=full) BS3C d / / SURF / SURF / PCUR / PCUR / I I / A / A / A /";

        /// <summary>The surface of a cylinder swept along a curve the two names of its subtype share.</summary>
        private const string CylinderSurface =
            "/ CURVE / V / P / e(2=none) I I I I e(2=PERIODIC) e(0=OPEN) e(0=NON_SINGULAR) e(0=NON_SINGULAR) "
            + "/ A / A / A / A / A / A / o(F,T)";

        /// <summary>The subtypes a spline curve or surface holds between braces, each by its name: AutoCAD's names of R2004 and those of R2007 on.</summary>
        private static readonly Dictionary<string, string> Subtypes = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["ref"] = "i",
            ["exactcur"] = IntCurve + " I I",
            ["exact_int_cur"] = "oi " + IntCurve + " / i I I o(0=UNEXTENDED) o(0=UNEXTENDED)",
            ["surfintcur"] = IntCurve,
            ["int_int_cur"] = "oi " + IntCurve + " / i",
            ["parcur"] = IntCurve + " b(surf2,surf1)",
            ["par_int_cur"] = "oi " + IntCurve + " / i b(surf2,surf1)",
            ["exppc"] = "BS2C d / SURF /",
            ["exp_par_cur"] = "BS2C d / SURF /",
            ["cylsur"] = CylinderSurface,
            ["cyl_spl_sur"] = "i " + CylinderSurface,
            ["exactsur"] = "e(0=full) BS3S d / / A / A / A / A / A / A / I I I I",
            ["exact_spl_sur"] = "i e(0=full) BS3S d / / A / A / A / A / A / A / I I I I o(0=UNEXTENDED)",
            ["sweep_sur"] =
                "i b(not_on,on_xy) / i CURVE / I I b(!,is_planar) P V P V V V / i b(has_z,no_z) / CURVE / I I d b(no_scale,!) "
                + "/ s / i / s / CURVE / d / d / e(0=full) BS3S d / / A / A / A / A / A / A / b(F,T)",
            ["loft_spl_sur"] =
                "i i / [ d / i i CURVE SURF PCUR b(F,T) i i i d d d d d d b(F,T) CURVE i i / ] i / I I I I b(F,T) "
                + "b(F,T) b(F,T) b(F,T) i e(0=full) BS3S d / / A / A / A / A / A / A / b(F,T)",
        };

        /// <summary>The surfaces a subtype holds in line, each by its name.</summary>
        private static readonly Dictionary<string, string> Surfaces = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["null_surface"] = "",
            ["plane"] = "P U V b(forward_v,reverse_v) I I I I",
            ["cone"] = "P U V d I I d d d b(forward,!) I I I I",
            ["sphere"] = "P d V V b(forward_v,reverse_v) I I I I",
            ["torus"] = "P U d d V b(forward_v,reverse_v) I I I I",
            ["spline"] = "b(forward,reversed) {} I I I I",
        };

        /// <summary>The curves a subtype holds in line, each by its name.</summary>
        private static readonly Dictionary<string, string> Curves = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["null_curve"] = "",
            ["intcurve"] = "b(forward,reversed) {} I I",
            ["ellipse"] = "P U V d I I",
        };

        private static readonly Dictionary<string, string[]> Parsed = new Dictionary<string, string[]>(
            StringComparer.Ordinal
        );

        private static readonly BigInteger[] Tens = TensUpTo(40);

        /// <summary>The bytes that start every SAB.</summary>
        private static readonly byte[] Head = Encoding.ASCII.GetBytes("ACIS BinaryFile");

        /// <summary>
        /// Reads SAB from <paramref name="data"/> where it stands, to the record that ends it (<c>End-of-ACIS-data</c> or
        /// <c>End-of-ASM-data</c>), byte by byte as the DWG keeps it; null where it is not SAB or holds a tag that is not
        /// known, the reader then standing past what it read.
        /// </summary>
        internal static byte[] Read(DwgBitReader data)
        {
            var bytes = new List<byte>();

            for (int i = 0; i < Head.Length + 16; i++)
            {
                bytes.Add(data.RawChar());

                if (i < Head.Length && bytes[i] != Head[i])
                {
                    return null;
                }
            }

            var name = new StringBuilder();

            while (true)
            {
                byte tag = data.RawChar();
                bytes.Add(tag);
                int size = Size(tag);

                if (size == -2)
                {
                    return null;
                }

                if (size == -1)
                {
                    // A text of a length of 1, 2 or 4 bytes before it.
                    int lengthBytes =
                        tag == 8 ? 2
                        : tag == 9 ? 4
                        : 1;
                    long length = 0;

                    for (int i = 0; i < lengthBytes; i++)
                    {
                        byte one = data.RawChar();
                        bytes.Add(one);
                        length |= (long)one << (8 * i);
                    }

                    if (length * 8 > data.Remaining)
                    {
                        return null;
                    }

                    byte[] text = data.Bytes((int)length);
                    bytes.AddRange(text);

                    if (tag == 0x0E)
                    {
                        name.Append(Encoding.ASCII.GetString(text)).Append('-');
                    }
                    else if (tag == 0x0D)
                    {
                        string whole = name.Append(Encoding.ASCII.GetString(text)).ToString();
                        name.Clear();

                        if (whole == "End-of-ACIS-data" || whole == "End-of-ASM-data")
                        {
                            return bytes.ToArray();
                        }
                    }

                    continue;
                }

                for (int i = 0; i < size; i++)
                {
                    bytes.Add(data.RawChar());
                }
            }
        }

        /// <summary>
        /// The number of bytes after a tag of SAB: -1 for a text whose length comes first, -2 for a tag not known.
        /// </summary>
        private static int Size(byte tag)
        {
            switch (tag)
            {
                case 0x02:
                    return 1;
                case 0x03:
                    return 2;
                case 0x04:
                case 0x0C:
                case 0x15:
                    return 4;
                case 0x06:
                case 0x17:
                    return 8;
                case 0x07:
                case 0x08:
                case 0x09:
                case 0x0D:
                case 0x0E:
                    return -1;
                case 0x0A:
                case 0x0B:
                case 0x0F:
                case 0x10:
                case 0x11:
                    return 0;
                case 0x13:
                case 0x14:
                    return 24;
                default:
                    return -2;
            }
        }

        /// <summary>
        /// The SAT AutoCAD writes in DXF of the SAB in <paramref name="sab"/>, its lines each ended by a line feed; null
        /// where the SAB holds a record, a subtype or a value the grammars do not place.
        /// </summary>
        internal static string ToSat(byte[] sab) => ToSat(sab, null);

        /// <summary>
        /// The SAT of the SAB in <paramref name="sab"/> in the form of <paramref name="form"/>: its head's version and flags
        /// those of the form, and the records given the numbers the form has, a coedge's of the binary form out and a vertex's
        /// in or out. A form of null leaves the SAB as it is (the SAT AutoCAD writes of a DWG's). Null where the SAB holds a
        /// record the grammars do not place or is of a version whose records are not known.
        /// </summary>
        internal static string ToSat(byte[] sab, AcisFormat form)
        {
            List<List<Token>> records;

            try
            {
                records = Tokens(sab);
            }
            catch (FormatException)
            {
                return null;
            }

            if (records.Count < 2)
            {
                return null;
            }

            List<Token> head = records[0];
            int first = head.FindIndex(token => token.Kind == 'N' || token.Kind == 'n');

            if (first < 0)
            {
                return null;
            }

            var strings = new List<string>();
            var reals = new List<double>();

            for (int i = 0; i < first; i++)
            {
                if (head[i].Kind == 's')
                {
                    strings.Add(head[i].Text);
                }
                else if (head[i].Kind == 'd')
                {
                    reals.Add(head[i].Real);
                }
                else
                {
                    return null;
                }
            }

            if (strings.Count < 2 || reals.Count != 3)
            {
                return null;
            }

            records[0] = head.GetRange(first, head.Count - first);
            List<Token> last = records[records.Count - 1];

            // The last record is End-of-ACIS-data (or End-of-ASM-data), which DXF does not write.
            if (
                last.Count != 4
                || last[0].Text != "End"
                || last[1].Text != "of"
                || (last[2].Text != "ACIS" && last[2].Text != "ASM")
                || last[3].Kind != 'N'
                || last[3].Text != "data"
            )
            {
                return null;
            }

            if (form != null && !Adjust(records, Int(sab, 15), form))
            {
                return null;
            }

            return Print(
                records,
                records.Count - 1,
                form == null ? Int(sab, 15) : form.Version,
                Int(sab, 23),
                form == null ? Int(sab, 27) : form.Flags,
                strings,
                reals
            );
        }

        /// <summary>The SAT of the first <paramref name="count"/> records (the end of the data after them is not written) under the head of the version, roots and flags given.</summary>
        private static string Print(
            List<List<Token>> records,
            int count,
            int version,
            int roots,
            int flags,
            List<string> strings,
            List<double> reals
        )
        {
            var text = new StringBuilder();
            string counted = count.ToString(CultureInfo.InvariantCulture);
            text.Append(version)
                .Append(' ')
                .Append(counted)
                .Append(' ')
                .Append(roots)
                .Append(' ')
                .Append(flags)
                .Append(' ')
                .Append(' ', Math.Max(0, 11 - counted.Length))
                .Append('\n');

            for (int i = 0; i < 2; i++)
            {
                text.Append(strings[i].Length.ToString(CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(strings[i])
                    .Append(' ');
            }

            text.Append("0  \n");

            foreach (double real in reals)
            {
                text.Append(Number(real)).Append(' ');
            }

            text.Append('\n');

            for (int i = 0; i < count; i++)
            {
                var writer = new Writer(records[i], text);

                if (!writer.Record())
                {
                    return null;
                }

                text.Append("#\n");
            }

            return text.ToString();
        }

        private static int Int(byte[] bytes, int at) => BitConverter.ToInt32(bytes, at);

        /// <summary>The records of SAB, the head's values before the first; throws <see cref="FormatException"/> where it is cut short or holds a tag not known.</summary>
        private static List<List<Token>> Tokens(byte[] sab)
        {
            var records = new List<List<Token>>();
            var record = new List<Token>();
            int at = Head.Length + 16;

            if (sab.Length < at)
            {
                throw new FormatException("The SAB is cut short.");
            }

            while (at < sab.Length)
            {
                byte tag = sab[at++];
                var token = new Token();

                switch (tag)
                {
                    case 0x02:
                        Need(sab, at, 1);
                        token.Kind = 'i';
                        token.Whole = sab[at];
                        at += 1;
                        break;
                    case 0x03:
                        Need(sab, at, 2);
                        token.Kind = 'i';
                        token.Whole = BitConverter.ToInt16(sab, at);
                        at += 2;
                        break;
                    case 0x04:
                    case 0x0C:
                    case 0x15:
                        Need(sab, at, 4);
                        token.Kind =
                            tag == 0x04 ? 'i'
                            : tag == 0x0C ? 'r'
                            : 'e';
                        token.Whole = BitConverter.ToInt32(sab, at);
                        at += 4;
                        break;
                    case 0x17:
                        Need(sab, at, 8);
                        token.Kind = 'L';
                        token.Whole = BitConverter.ToInt64(sab, at);
                        at += 8;
                        break;
                    case 0x06:
                        Need(sab, at, 8);
                        token.Kind = 'd';
                        token.Real = BitConverter.ToDouble(sab, at);
                        at += 8;
                        break;
                    case 0x07:
                    case 0x08:
                    case 0x09:
                    case 0x0D:
                    case 0x0E:
                    {
                        int lengthBytes =
                            tag == 8 ? 2
                            : tag == 9 ? 4
                            : 1;
                        Need(sab, at, lengthBytes);
                        long length =
                            lengthBytes == 1 ? sab[at]
                            : lengthBytes == 2 ? BitConverter.ToUInt16(sab, at)
                            : BitConverter.ToUInt32(sab, at);
                        at += lengthBytes;
                        Need(sab, at, length);
                        token.Kind =
                            tag == 0x0D ? 'N'
                            : tag == 0x0E ? 'n'
                            : 's';
                        token.Text = Encoding.ASCII.GetString(sab, at, (int)length);

                        for (int i = 0; i < length; i++)
                        {
                            // SAT is plain ASCII as AutoCAD writes it; a letter beyond it is not placed.
                            if (sab[at + i] < 0x20 || sab[at + i] > 0x7E)
                            {
                                throw new FormatException("The SAB holds a text beyond ASCII.");
                            }
                        }

                        at += (int)length;
                        break;
                    }
                    case 0x0A:
                    case 0x0B:
                        token.Kind = 'b';
                        token.Whole = tag == 0x0A ? 1 : 0;
                        break;
                    case 0x0F:
                        token.Kind = '{';
                        break;
                    case 0x10:
                        token.Kind = '}';
                        break;
                    case 0x11:
                        records.Add(record);
                        record = new List<Token>();
                        continue;
                    case 0x13:
                    case 0x14:
                        Need(sab, at, 24);
                        token.Kind = tag == 0x13 ? 'P' : 'V';
                        token.Triple = new[]
                        {
                            BitConverter.ToDouble(sab, at),
                            BitConverter.ToDouble(sab, at + 8),
                            BitConverter.ToDouble(sab, at + 16),
                        };
                        at += 24;
                        break;
                    default:
                        throw new FormatException("The SAB holds a tag that is not known.");
                }

                record.Add(token);
            }

            if (record.Count > 0)
            {
                records.Add(record);
            }

            return records;
        }

        private static void Need(byte[] bytes, int at, long count)
        {
            if (at + count > bytes.Length)
            {
                throw new FormatException("The SAB is cut short.");
            }
        }

        /// <summary>
        /// A real as C's <c>%.19g</c> writes it: 19 significant digits of its exact value, rounded half to even, the
        /// zeros at the end left off; in the form with an exponent (of at least two digits) where that exponent is under
        /// -4 or 19 and over.
        /// </summary>
        internal static string Number(double value)
        {
            if (value == 0.0)
            {
                return 1.0 / value < 0 ? "-0" : "0";
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new FormatException("The SAB holds a real that is no number.");
            }

            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;

            if (exponent == 0)
            {
                exponent = 1;
            }
            else
            {
                mantissa |= 1L << 52;
            }

            // The value is mantissa times 2 to the power two: its digits are those of mantissa times 2^two times 10^shift.
            int two = exponent - 1075;
            int ten = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            BigInteger digits;

            while (true)
            {
                digits = Scaled(mantissa, two, 18 - ten);

                if (digits >= Tens[19])
                {
                    ten++;
                }
                else if (digits < Tens[18])
                {
                    ten--;
                }
                else
                {
                    break;
                }
            }

            string text = digits.ToString(CultureInfo.InvariantCulture);
            var written = new StringBuilder(negative ? "-" : string.Empty);

            if (ten < -4 || ten >= 19)
            {
                written.Append(text[0]);
                string rest = text.Substring(1).TrimEnd('0');

                if (rest.Length > 0)
                {
                    written.Append('.').Append(rest);
                }

                written
                    .Append(ten < 0 ? "e-" : "e+")
                    .Append(Math.Abs(ten).ToString("00", CultureInfo.InvariantCulture));
            }
            else if (ten >= 0)
            {
                written.Append(text, 0, ten + 1);
                string rest = text.Substring(ten + 1).TrimEnd('0');

                if (rest.Length > 0)
                {
                    written.Append('.').Append(rest);
                }
            }
            else
            {
                written.Append("0.").Append('0', -ten - 1).Append(text.TrimEnd('0'));
            }

            return written.ToString();
        }

        /// <summary>mantissa times 2^two times 10^shift, rounded half to even to a whole number.</summary>
        private static BigInteger Scaled(long mantissa, int two, int shift)
        {
            BigInteger top = mantissa;
            BigInteger bottom = BigInteger.One;

            if (two >= 0)
            {
                top <<= two;
            }
            else
            {
                bottom <<= -two;
            }

            if (shift >= 0)
            {
                top *= Ten(shift);
            }
            else
            {
                bottom *= Ten(-shift);
            }

            BigInteger whole = BigInteger.DivRem(top, bottom, out BigInteger rest);
            int half = (rest * 2).CompareTo(bottom);

            if (half > 0 || (half == 0 && !whole.IsEven))
            {
                whole += 1;
            }

            return whole;
        }

        private static BigInteger Ten(int power) => power < Tens.Length ? Tens[power] : BigInteger.Pow(10, power);

        private static BigInteger[] TensUpTo(int most)
        {
            var tens = new BigInteger[most + 1];
            tens[0] = BigInteger.One;

            for (int i = 1; i <= most; i++)
            {
                tens[i] = tens[i - 1] * 10;
            }

            return tens;
        }

        private static string[] Items(string grammar)
        {
            lock (Parsed)
            {
                if (!Parsed.TryGetValue(grammar, out string[] items))
                {
                    items = grammar.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    Parsed.Add(grammar, items);
                }

                return items;
            }
        }

        /// <summary>A value of SAB: its kind (as the grammars name them) and the value of that kind.</summary>
        private struct Token
        {
            /// <summary>
            /// i a whole number, L one of 64 bits, d a real, r a reference, e an enum, b a flag, s a text, N a name or the
            /// last part of one, n a part before it, P a position, V a vector, { and } a subtype's bounds.
            /// </summary>
            public char Kind;
            public long Whole;
            public double Real;
            public double[] Triple;
            public string Text;

            /// <summary>Whether a vector is already the unit vector SAT writes (read from SAT), which is not divided by its length again.</summary>
            public bool Exact;
        }

        /// <summary>Writes one record of SAT by its grammar; false where its values do not follow it.</summary>
        private sealed class Writer
        {
            private readonly List<Token> _tokens;
            private readonly StringBuilder _text;
            private int _at;
            private long _last;

            public Writer(List<Token> tokens, StringBuilder text)
            {
                _tokens = tokens;
                _text = text;
            }

            private char Next => _at < _tokens.Count ? _tokens[_at].Kind : '#';

            /// <summary>The record: its name, its parts joined by hyphens, then its values by the grammar of that name.</summary>
            public bool Record()
            {
                var name = new StringBuilder();

                while (Next == 'n')
                {
                    name.Append(_tokens[_at++].Text).Append('-');
                }

                if (Next != 'N')
                {
                    return false;
                }

                name.Append(_tokens[_at++].Text);

                if (!Records.TryGetValue(name.ToString(), out string grammar))
                {
                    return false;
                }

                Emit(name.ToString());
                return Run(Items(grammar), 0, Items(grammar).Length) && _at == _tokens.Count;
            }

            private void Emit(string value) => _text.Append(value).Append(' ');

            private bool Take(string kinds, out Token token)
            {
                token = default(Token);

                if (kinds.IndexOf(Next) < 0)
                {
                    return false;
                }

                token = _tokens[_at++];
                return true;
            }

            /// <summary>A value written as it is, of any kind but a flag, an enum or a bound.</summary>
            private bool Plain(Token token)
            {
                switch (token.Kind)
                {
                    case 'r':
                        Emit("$" + token.Whole.ToString(CultureInfo.InvariantCulture));
                        return true;
                    case 'i':
                    case 'L':
                        _last = token.Whole;
                        Emit(token.Whole.ToString(CultureInfo.InvariantCulture));
                        return true;
                    case 'd':
                        Emit(Number(token.Real));
                        return true;
                    case 'P':
                    case 'V':
                        Emit(Number(token.Triple[0]));
                        Emit(Number(token.Triple[1]));
                        Emit(Number(token.Triple[2]));
                        return true;
                    case 's':
                        Emit("@" + token.Text.Length.ToString(CultureInfo.InvariantCulture) + " " + token.Text);
                        return true;
                    case 'N':
                        Emit(token.Text);
                        return true;
                    default:
                        return false;
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

                    // [ ... ] repeats what it holds as many times as the whole number before it says.
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

                    if (times < 0 || times > _tokens.Count)
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

            /// <summary>The word for a flag or an enum of <paramref name="value"/>: "w0,w1" by place, or "2=w,4=v" by value; "!" or none is not known.</summary>
            private static string Word(string spec, long value)
            {
                foreach (string part in spec.Split(','))
                {
                    int equals = part.IndexOf('=');

                    if (equals < 0)
                    {
                        continue;
                    }

                    if (long.Parse(part.Substring(0, equals), CultureInfo.InvariantCulture) == value)
                    {
                        return part.Substring(equals + 1);
                    }
                }

                if (spec.IndexOf('=') >= 0)
                {
                    return null;
                }

                string[] words = spec.Split(',');
                return value >= 0 && value < words.Length && words[value] != "!" ? words[value] : null;
            }

            private bool Worded(string spec, string kinds)
            {
                if (!Take(kinds, out Token token))
                {
                    return false;
                }

                string word = Word(spec, token.Whole);

                if (word == null)
                {
                    return false;
                }

                Emit(word);
                return true;
            }

            private bool Item(string item)
            {
                Token token;

                switch (item)
                {
                    case "*":
                        while ("riLdPVsN".IndexOf(Next) >= 0)
                        {
                            Plain(_tokens[_at++]);
                        }

                        return true;
                    case "r":
                    case "d":
                    case "P":
                    case "V":
                    case "s":
                        return Take(item, out token) && Plain(token);
                    case "i":
                        return Take("i", out token) && Plain(token);
                    case "oi":
                        return Next != 'i' || (Take("i", out token) && Plain(token));
                    case "/":
                        _text.Append("\n ");
                        return true;
                    case "U":
                        if (!Take("V", out token))
                        {
                            return false;
                        }

                        double[] v = token.Triple;
                        double length = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);

                        // A normal that is a unit but for rounding (a length of 0.9999999999999999) is written as it is.
                        if (token.Exact || Math.Abs(length - 1.0) < 1E-12)
                        {
                            length = 1.0;
                        }

                        if (!(length > 0))
                        {
                            return false;
                        }

                        Emit(Number(v[0] / length));
                        Emit(Number(v[1] / length));
                        Emit(Number(v[2] / length));
                        return true;
                    case "I":
                        // A bound: I where there is none, F and the bound where there is.
                        if (!Take("b", out token))
                        {
                            return false;
                        }

                        if (token.Whole == 0)
                        {
                            Emit("I");
                            return true;
                        }

                        Emit("F");
                        return Take("d", out token) && Plain(token);
                    case "{}":
                        return Subtype();
                    case "o{}":
                        return Next != '{' || Subtype();
                    case "SURF":
                        return InLine(Surfaces);
                    case "CURVE":
                        return InLine(Curves);
                    case "PCUR":
                        if (Next == 'N' && _tokens[_at].Text == "nullbs")
                        {
                            Emit("nullbs");
                            _at++;
                            return true;
                        }

                        return Spline(2);
                    case "A":
                        // A count and that many reals, on one line.
                        if (!Take("i", out token) || token.Whole < 0 || token.Whole > _tokens.Count)
                        {
                            return false;
                        }

                        Plain(token);

                        for (long n = 0; n < token.Whole; n++)
                        {
                            if (!Take("d", out Token real))
                            {
                                return false;
                            }

                            Plain(real);
                        }

                        return true;
                    case "BS3C":
                        return Spline(3);
                    case "BS2C":
                        return Spline(2);
                    case "BS3S":
                        return SplineSurface();
                }

                if (item.StartsWith("b(", StringComparison.Ordinal))
                {
                    return Worded(item.Substring(2, item.Length - 3), "b");
                }

                if (item.StartsWith("e(", StringComparison.Ordinal))
                {
                    return Worded(item.Substring(2, item.Length - 3), "e");
                }

                if (item.StartsWith("o(", StringComparison.Ordinal))
                {
                    return (Next != 'b' && Next != 'e') || Worded(item.Substring(2, item.Length - 3), "be");
                }

                throw new InvalidOperationException("The grammar of SAT names an item that is not known: " + item);
            }

            private bool Subtype()
            {
                if (!Take("{", out Token _) || !Take("N", out Token name))
                {
                    return false;
                }

                if (!Subtypes.TryGetValue(name.Text, out string grammar))
                {
                    return false;
                }

                Emit("{");
                Emit(name.Text);
                string[] items = Items(grammar);

                if (!Run(items, 0, items.Length) || !Take("}", out Token _))
                {
                    return false;
                }

                Emit("}");
                return true;
            }

            private bool InLine(Dictionary<string, string> kinds)
            {
                if (!Take("N", out Token name) || !kinds.TryGetValue(name.Text, out string grammar))
                {
                    return false;
                }

                Emit(name.Text);
                string[] items = Items(grammar);
                return Run(items, 0, items.Length);
            }

            /// <summary>
            /// The knots of a spline, each a value and its multiplicity, five to a line; the sum of the multiplicities.
            /// </summary>
            private bool Knots(long count, out long sum)
            {
                sum = 0;

                for (long i = 0; i < count; i++)
                {
                    if (!Take("d", out Token knot) || !Take("i", out Token multiplicity) || multiplicity.Whole < 0)
                    {
                        return false;
                    }

                    Plain(knot);
                    Plain(multiplicity);
                    sum += multiplicity.Whole;

                    if (i % 5 == 4 || i == count - 1)
                    {
                        _text.Append("\n ");
                    }
                }

                return true;
            }

            private bool Head(out bool rational)
            {
                rational = false;

                if (!Take("N", out Token kind))
                {
                    return false;
                }

                Emit(kind.Text);
                rational = kind.Text == "nurbs";
                return kind.Text == "nubs" || rational;
            }

            /// <summary>
            /// A spline curve of <paramref name="dimension"/> (3 in space, 2 on a surface): nullbs, or nubs or nurbs, its
            /// degree, closure and count of knots, then its knots, then its control points one to a line, a weight after
            /// each of a nurbs. A spline of n knots of multiplicities summing to m and of degree d has m - d + 1 points.
            /// </summary>
            private bool Spline(int dimension)
            {
                if (Next == 'N' && _tokens[_at].Text == "nullbs")
                {
                    Emit("nullbs");
                    _at++;
                    return true;
                }

                if (!Head(out bool rational) || !Take("i", out Token degree))
                {
                    return false;
                }

                Plain(degree);

                if (
                    !Worded("0=open,1=closed,2=periodic", "e")
                    || !Take("i", out Token count)
                    || count.Whole < 0
                    || count.Whole > _tokens.Count
                )
                {
                    return false;
                }

                Plain(count);
                _text.Append("\n ");
                return Knots(count.Whole, out long sum)
                    && Points(sum - degree.Whole + 1, dimension + (rational ? 1 : 0));
            }

            /// <summary>
            /// A spline surface: nullbs, or nubs or nurbs, its degrees in u and v, closures, singularities and counts of
            /// knots, then the knots in u and in v, then the control points one to a line.
            /// </summary>
            private bool SplineSurface()
            {
                if (Next == 'N' && _tokens[_at].Text == "nullbs")
                {
                    Emit("nullbs");
                    _at++;
                    return true;
                }

                if (!Head(out bool rational) || !Take("i", out Token u) || !Take("i", out Token v))
                {
                    return false;
                }

                Plain(u);
                Plain(v);

                if (
                    !Worded("0=open,1=closed,2=periodic", "e")
                    || !Worded("0=open,1=closed,2=periodic", "e")
                    || !Worded("0=none", "e")
                    || !Worded("0=none", "e")
                    || !Take("i", out Token us)
                    || !Take("i", out Token vs)
                    || us.Whole < 0
                    || vs.Whole < 0
                    || us.Whole + vs.Whole > _tokens.Count
                )
                {
                    return false;
                }

                Plain(us);
                Plain(vs);
                _text.Append("\n ");

                if (!Knots(us.Whole, out long uSum) || !Knots(vs.Whole, out long vSum))
                {
                    return false;
                }

                return Points((uSum - u.Whole + 1) * (vSum - v.Whole + 1), 3 + (rational ? 1 : 0));
            }

            private bool Points(long count, int size)
            {
                if (count < 0 || count * size > _tokens.Count - _at)
                {
                    return false;
                }

                for (long i = 0; i < count; i++)
                {
                    for (int j = 0; j < size; j++)
                    {
                        if (!Take("d", out Token real))
                        {
                            return false;
                        }

                        Plain(real);
                    }

                    _text.Append("\n ");
                }

                return true;
            }
        }
    }
}
