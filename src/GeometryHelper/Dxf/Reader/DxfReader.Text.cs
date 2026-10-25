using System;
using System.Collections.Generic;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public static partial class DxfReader
    {
        private sealed partial class Reading
        {
            private readonly List<DxfLayer> _layerTable = new List<DxfLayer>();
            private readonly HashSet<string> _tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            #region The layer table

            /// <summary>
            /// Takes a LAYER record of the layer table: its name for <see cref="DxfDocument.Layers"/>, and the layer for
            /// <see cref="DxfDocument.LayerTable"/> where no record before it has the name in another case.
            /// </summary>
            private void TableLayer(Record r)
            {
                string name = Decode((r.Text(2) ?? string.Empty).Trim());

                if (name.Length == 0)
                {
                    return;
                }

                string known = LayerName(name);

                if (!_tableNames.Add(known))
                {
                    return;
                }

                // The colour is the index whether the layer is on or off, which its sign says; one that cannot be read is none.
                int? colour = null;
                bool off = false;

                if (r.TryInt(62, out int index))
                {
                    off = index < 0;
                    int absolute = index == int.MinValue ? 0 : Math.Abs(index);
                    colour = absolute >= 1 && absolute <= 255 ? absolute : (int?)null;
                }

                int? trueColour = r.TryInt(420, out int rgb) ? rgb & 0xFFFFFF : (int?)null;
                int? lineweight = r.TryInt(370, out int weight) && weight >= 0 && DxfShape.IsLineweight(weight) ? weight : (int?)null;
                string linetype = Decode((r.Text(6) ?? string.Empty).Trim());
                int flags = r.Int(70, 0);
                bool plotted = !r.TryInt(290, out int plot) || plot != 0;

                _layerTable.Add(new DxfLayer(known, colour, trueColour, linetype.Length == 0 ? null : linetype, lineweight, off, (flags & 1) != 0, (flags & 4) != 0, plotted));
            }

            #endregion

            #region Texts

            /// <summary>
            /// Reads a TEXT or an MTEXT. A TEXT stands in the system of its extrusion's plane, at its first alignment point
            /// (10) where it is aligned left on its baseline (72 and 73 nought) or aligned or fitted between its two points (72
            /// 3 or 5, 73 nought), which start at the first, and at its second (11) otherwise, turned by its group 50 in degrees,
            /// its letters widened by its group 41. An MTEXT stands at its insertion point (10) in world
            /// coordinates, turned as its direction (11) says, or by its group 50 in degrees where it has none, its paragraphs
            /// wrapping at its width (41). Placed by an INSERT, its x and y axes are carried: it stands where its point lands,
            /// faces the way the carried axes span, turned to the carried x axis, its height stretched as the y axis is and a
            /// TEXT's letters widened as the x axis is against it. The rotation is brought into a turn, from nought up to 2 pi.
            /// </summary>
            private void ReadText(Record record)
            {
                bool multiline = record.Type == "MTEXT";

                // An MTEXT of R2018 with columns embeds an object of its own (101) whose groups 10, 11, 40 and 41 are not the
                // text's: its own are read from those before it.
                Record r = multiline ? record.Before(101) : record;

                if (!TryPoint(r, 0, out GeoPoint3 at) || !r.TryReal(40, out double height))
                {
                    Skip(r.Type, Incomplete);
                    return;
                }

                height = Scaled(height);

                if (!(height > 0.0))
                {
                    Skip(r.Type, "of no height");
                    return;
                }

                GeoVector3 normal = Extrusion(r);
                var ocs = new Ocs(normal);
                double rotation;
                double widthFactor = 1.0;
                double width = 0.0;
                string value;

                if (multiline)
                {
                    rotation = TryDirection(r, 1, out GeoVector3 direction) && direction.Length > 0.0
                        ? Math.Atan2(direction.DotProduct(ocs.V), direction.DotProduct(ocs.U))
                        : r.Real(50, 0.0) * Math.PI / 180.0;
                    width = Math.Max(0.0, Scaled(r.Real(41, 0.0)));
                    value = MText(r);
                }
                else
                {
                    // ALIGNED (3) and FIT (5) on the baseline run from the first point to the second, so they start at the first.
                    int horizontal = r.Int(72, 0);
                    int vertical = r.Int(73, 0);
                    bool fromFirst = vertical == 0 && (horizontal == 0 || horizontal == 3 || horizontal == 5);

                    if (!fromFirst && TryPoint(r, 1, out GeoPoint3 aligned))
                    {
                        at = aligned;
                    }

                    at = ocs.World(at);
                    rotation = r.Real(50, 0.0) * Math.PI / 180.0;
                    double given = r.Real(41, 1.0);
                    widthFactor = given > 0.0 ? given : 1.0;
                    value = OneLine(Signs(Decode(Carets(r.Text(1) ?? string.Empty))));
                }

                if (_place != null)
                {
                    var frame = new Frame(ocs, _place);

                    if (frame.Collapsed)
                    {
                        Skip(r.Type, Flattened);
                        return;
                    }

                    double cos = Math.Cos(rotation);
                    double sin = Math.Sin(rotation);
                    GeoVector3 x = frame.U.Multiply(cos).Add(frame.V.Multiply(sin));
                    GeoVector3 y = frame.V.Multiply(cos).Subtract(frame.U.Multiply(sin));
                    double along = x.Length;
                    double up = y.Length;
                    var placed = new Ocs(frame.Normal);

                    at = _place.Apply(at);
                    normal = frame.Normal;
                    rotation = Math.Atan2(x.DotProduct(placed.V), x.DotProduct(placed.U));
                    height *= up;

                    if (multiline)
                    {
                        width *= along;
                    }
                    else
                    {
                        widthFactor *= along / up;
                    }

                    if (!(height > 0.0) || double.IsInfinity(height) || double.IsInfinity(width) || !(widthFactor > 0.0) || double.IsInfinity(widthFactor))
                    {
                        throw new NotFinite();
                    }
                }

                Add(record, DxfText.Read(at, normal, height, DxfText.Turn(rotation), value, multiline, widthFactor, width));
            }

            /// <summary>The words of an MTEXT: its pieces of 250 characters (3) and its last (1), its formatting taken out.</summary>
            private static string MText(Record r)
            {
                var text = new StringBuilder();

                for (int i = 0; i < r.Count; i++)
                {
                    if (r.CodeAt(i) == 3)
                    {
                        text.Append(r.ValueAt(i));
                    }
                }

                text.Append(r.Text(1) ?? string.Empty);
                return Signs(Decode(PlainMText(Carets(text.ToString())))).Replace("\r", string.Empty);
            }

            #endregion
        }

        /// <summary>
        /// A text with its special characters read as AutoCAD reads them, from the left: %%c, %%d and %%p, of either case, the
        /// diameter, degree and plus-minus signs, %%% a per cent sign, and %%nnn, three digits, the character of that code;
        /// any other %% is kept as written, with the letter after it.
        /// </summary>
        private static string Signs(string text)
        {
            if (text.IndexOf("%%", StringComparison.Ordinal) < 0)
            {
                return text;
            }

            var read = new StringBuilder(text.Length);
            int i = 0;

            while (i < text.Length)
            {
                if (text[i] != '%' || i + 2 >= text.Length || text[i + 1] != '%')
                {
                    read.Append(text[i]);
                    i++;
                    continue;
                }

                char code = text[i + 2];

                switch (char.ToLowerInvariant(code))
                {
                    case 'c': read.Append('\u2300'); i += 3; continue;
                    case 'd': read.Append('\u00B0'); i += 3; continue;
                    case 'p': read.Append('\u00B1'); i += 3; continue;
                    case '%': read.Append('%'); i += 3; continue;
                }

                if (i + 4 < text.Length && IsDigit(code) && IsDigit(text[i + 3]) && IsDigit(text[i + 4]))
                {
                    read.Append((char)(((code - '0') * 100) + ((text[i + 3] - '0') * 10) + (text[i + 4] - '0')));
                    i += 5;
                    continue;
                }

                read.Append(text, i, 3);
                i += 3;
            }

            return read.ToString();
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        /// <summary>
        /// A text with AutoCAD's caret form of a control character read as the character: ^J a line feed, ^M a carriage
        /// return, ^I a tab, a caret and any letter of @ to _ the code 64 below it, and ^ followed by a space a caret.
        /// </summary>
        private static string Carets(string text)
        {
            if (text.IndexOf('^') < 0)
            {
                return text;
            }

            var read = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '^' && i + 1 < text.Length && (text[i + 1] == ' ' || (text[i + 1] >= '@' && text[i + 1] <= '_')))
                {
                    char next = text[++i];
                    read.Append(next == ' ' ? '^' : (char)(next - 64));
                    continue;
                }

                read.Append(c);
            }

            return read.ToString();
        }

        /// <summary>The words of a TEXT on one line: a line feed a space, a carriage return left out.</summary>
        private static string OneLine(string text)
            => text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0 ? text : text.Replace("\r", string.Empty).Replace('\n', ' ');

        /// <summary>
        /// The words of an MTEXT without its formatting: <c>\P</c> is a new line and <c>\N</c>, a new column, a space, the
        /// codes that end in a semicolon (font, height, colour, width, stacking, paragraph and the rest) go with their
        /// arguments, those that stand alone (<c>\L</c>, <c>\l</c>, <c>\O</c>, <c>\o</c>, <c>\K</c>, <c>\k</c>) go alone,
        /// <c>\~</c> is a space, <c>\\</c>, <c>\{</c> and <c>\}</c> are the character, a stack <c>\S1/2;</c> is read as 1/2, a
        /// <c>\U+</c> escape is left for the decoding that follows, and the braces that group are taken off. A code with no
        /// semicolon after it is kept as written, as ezdxf keeps it, <c>\M+1A4A1</c> among them, unless a later
        /// semicolon ends it, as it ends it in ezdxf too.
        /// </summary>
        internal static string PlainMText(string text)
        {
            var plain = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '{' || c == '}')
                {
                    continue;
                }

                if (c != '\\' || i + 1 >= text.Length)
                {
                    plain.Append(c);
                    continue;
                }

                char code = text[++i];

                switch (code)
                {
                    case 'P':
                    case 'X':
                        plain.Append('\n');
                        break;

                    case 'N':
                        plain.Append(' ');
                        break;

                    case '~':
                        plain.Append(' ');
                        break;

                    case '\\':
                    case '{':
                    case '}':
                        plain.Append(code);
                        break;

                    case 'L':
                    case 'l':
                    case 'O':
                    case 'o':
                    case 'K':
                    case 'k':
                        break;

                    case 'U':
                        // \U+00E9 is a character, decoded afterwards with the other escapes of a name.
                        plain.Append('\\').Append('U');
                        break;

                    case 'S':
                    {
                        int end = text.IndexOf(';', i);

                        if (end < 0)
                        {
                            plain.Append('\\').Append(code);
                            break;
                        }

                        plain.Append(text.Substring(i + 1, end - i - 1).Replace('^', '/').Replace('#', '/'));
                        i = end;
                        break;
                    }

                    default:
                        if (char.IsLetter(code))
                        {
                            // A code with arguments: they run to the semicolon, and with none it is kept as written.
                            int end = text.IndexOf(';', i);

                            if (end < 0)
                            {
                                plain.Append('\\').Append(code);
                            }
                            else
                            {
                                i = end;
                            }
                        }
                        else
                        {
                            plain.Append(code);
                        }

                        break;
                }
            }

            return plain.ToString();
        }
    }
}
