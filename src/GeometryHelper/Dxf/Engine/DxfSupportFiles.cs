using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// AutoCAD's files of support read for <see cref="DxfDatabase"/>: a file of linetypes (.lin), a file of multiline styles
    /// (.mln), and the names of the shapes of a compiled file of shapes (.shx) a complex linetype draws.
    /// </summary>
    internal static class DxfSupportFiles
    {
        /// <summary>The text of a file of support: UTF-8 where its bytes are, else read byte for byte as Latin-1, as AutoCAD's own are written in the code page of Windows.</summary>
        internal static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding("iso-8859-1").GetString(bytes);
            }
        }

        /// <summary>The lines of a text, their line ends taken off.</summary>
        private static string[] Lines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        #region Linetypes

        /// <summary>A linetype of a file of linetypes: its name, its description and its dashes, each its length and what it carries.</summary>
        internal sealed class Linetype
        {
            internal string Name { get; set; }

            internal string Description { get; set; }

            internal List<Dash> Dashes { get; } = new List<Dash>();

            /// <summary>Why the linetype cannot be read; null where it can.</summary>
            internal string Fault { get; set; }
        }

        /// <summary>
        /// A dash of a linetype of a file: its length, and the text or shape it carries, as the file writes it in brackets after
        /// it: the text in quotes or the shape by name or number, then the text style or the file of shapes, then the scale (S),
        /// the rotation (R or U from the line, A from the x axis, in degrees unless followed by r for radians or g for grads) and
        /// the offset (X, Y).
        /// </summary>
        internal sealed class Dash
        {
            internal double Length { get; set; }

            internal string Text { get; set; }

            internal string Shape { get; set; }

            internal string Style { get; set; }

            internal double? Scale { get; set; }

            internal double RotationDegrees { get; set; }

            internal bool Absolute { get; set; }

            internal double X { get; set; }

            internal double Y { get; set; }

            internal bool IsComplex => Text != null || Shape != null;
        }

        /// <summary>
        /// Reads the linetypes of a file of linetypes, in its order: each opened by <c>*NAME,description</c> on a line, its
        /// pattern on the next, <c>A,</c> and the lengths of its dashes, a dash's text or shape in brackets after it; a line that
        /// begins with a semicolon is a comment. A linetype whose pattern cannot be read is given with its fault.
        /// </summary>
        internal static List<Linetype> ReadLinetypes(string text)
        {
            var linetypes = new List<Linetype>();
            Linetype current = null;

            foreach (string raw in Lines(text))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line[0] == ';')
                {
                    continue;
                }

                if (line[0] == '*')
                {
                    int comma = line.IndexOf(',');
                    current = new Linetype
                    {
                        Name = (comma < 0 ? line.Substring(1) : line.Substring(1, comma - 1)).Trim(),
                        Description = comma < 0 ? string.Empty : line.Substring(comma + 1).Trim(),
                    };
                    linetypes.Add(current);
                    continue;
                }

                if (current == null || current.Dashes.Count > 0 || current.Fault != null)
                {
                    continue;
                }

                try
                {
                    Pattern(line, current.Dashes);
                }
                catch (FormatException e)
                {
                    current.Dashes.Clear();
                    current.Fault = e.Message;
                }
            }

            foreach (Linetype linetype in linetypes)
            {
                if (linetype.Fault == null && linetype.Dashes.Count == 0)
                {
                    linetype.Fault = "The linetype " + linetype.Name + " has no pattern after its name.";
                }
            }

            return linetypes;
        }

        /// <summary>Reads a pattern line into dashes, its alignment <c>A</c> passed over.</summary>
        private static void Pattern(string line, List<Dash> dashes)
        {
            List<object> tokens = Tokens(line);
            int at = tokens.Count > 0 && tokens[0] is string first && string.Equals(first.Trim(), "A", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            for (; at < tokens.Count; at++)
            {
                if (tokens[at] is List<string> complex)
                {
                    if (dashes.Count == 0 || dashes[dashes.Count - 1].IsComplex)
                    {
                        throw new FormatException("A text or shape in brackets follows the dash it is drawn on: '" + line + "' has one with no dash of its own before it.");
                    }

                    Complex(complex, dashes[dashes.Count - 1], line);
                    continue;
                }

                string token = ((string)tokens[at]).Trim();

                if (!Numbers.TryRead(token, out double length) || !Guard.IsFinite(length))
                {
                    throw new FormatException("A dash is a length: '" + token + "' of '" + line + "' is not one.");
                }

                dashes.Add(new Dash { Length = length });
            }
        }

        /// <summary>The fields of a pattern line, split at its commas: a text, or the fields of a part in brackets as a list, a comma within quotes kept.</summary>
        private static List<object> Tokens(string line)
        {
            var tokens = new List<object>();
            var token = new StringBuilder();
            List<string> bracket = null;
            bool quoted = false;

            void End()
            {
                if (bracket != null)
                {
                    bracket.Add(token.ToString().Trim());
                }
                else if (token.ToString().Trim().Length > 0)
                {
                    tokens.Add(token.ToString());
                }

                token.Clear();
            }

            foreach (char c in line)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    token.Append(c);
                }
                else if (quoted)
                {
                    token.Append(c);
                }
                else if (c == '[' && bracket == null)
                {
                    End();
                    bracket = new List<string>();
                }
                else if (c == ']' && bracket != null)
                {
                    End();
                    tokens.Add(bracket);
                    bracket = null;
                }
                else if (c == ',')
                {
                    End();
                }
                else
                {
                    token.Append(c);
                }
            }

            if (quoted || bracket != null)
            {
                throw new FormatException("The pattern '" + line + "' leaves a quote or a bracket open.");
            }

            End();
            return tokens;
        }

        /// <summary>Reads the part in brackets a dash carries: its text or shape, its style or file of shapes, and its transforms.</summary>
        private static void Complex(List<string> fields, Dash dash, string line)
        {
            if (fields.Count < 2 || fields[0].Length == 0 || fields[1].Length == 0)
            {
                throw new FormatException("A text or shape in brackets names what it draws and its style or file of shapes: '" + line + "' does not.");
            }

            string what = fields[0];

            if (what.Length >= 2 && what[0] == '"' && what[what.Length - 1] == '"')
            {
                dash.Text = what.Substring(1, what.Length - 2);
            }
            else
            {
                dash.Shape = what;
            }

            dash.Style = fields[1].Trim('"').Trim();

            for (int i = 2; i < fields.Count; i++)
            {
                int equals = fields[i].IndexOf('=');

                if (equals < 1)
                {
                    throw new FormatException("A transform of a text or shape is a letter, = and a value: '" + fields[i] + "' of '" + line + "' is not one.");
                }

                string key = fields[i].Substring(0, equals).Trim().ToUpperInvariant();
                string value = fields[i].Substring(equals + 1).Trim();

                switch (key)
                {
                    case "S":
                        dash.Scale = Number(value, line);
                        break;
                    case "X":
                        dash.X = Number(value, line);
                        break;
                    case "Y":
                        dash.Y = Number(value, line);
                        break;
                    case "R":
                    case "U":
                    case "A":
                        dash.RotationDegrees = Degrees(value, line);
                        dash.Absolute = key == "A";
                        break;
                    default:
                        throw new FormatException("A text or shape is transformed by S, R, U, A, X and Y: '" + key + "' of '" + line + "' is none of them.");
                }
            }
        }

        private static double Number(string value, string line)
        {
            if (!Numbers.TryRead(value, out double number) || !Guard.IsFinite(number))
            {
                throw new FormatException("'" + value + "' of '" + line + "' is not a number.");
            }

            return number;
        }

        /// <summary>A rotation in degrees: degrees as written or with d after it, radians with r, grads with g.</summary>
        private static double Degrees(string value, string line)
        {
            char unit = value.Length > 0 ? char.ToLowerInvariant(value[value.Length - 1]) : ' ';
            bool marked = unit == 'd' || unit == 'r' || unit == 'g';
            double number = Number(marked ? value.Substring(0, value.Length - 1) : value, line);
            return unit == 'r' ? number * 180.0 / Math.PI : unit == 'g' ? number * 0.9 : number;
        }

        #endregion

        #region Multiline styles

        /// <summary>
        /// Reads the multiline styles of a file of them, AutoCAD's acad.mln: each opened by <c>MLSTYLE</c> and given as the groups
        /// of an MLINESTYLE, a code on one line and its value on the next (2 its name, 70 its flags, 3 its description, 62 its
        /// fill, 51 and 52 its angles, 71 its count of elements, then each element's 49, 62 and 6), up to a group 0; a style after
        /// a group 0 of <c>MLSTYLE</c>.
        /// </summary>
        internal static List<List<DxfTypedValue>> ReadMlineStyles(string text)
        {
            var styles = new List<List<DxfTypedValue>>();
            List<DxfTypedValue> current = null;
            string[] lines = Lines(text);
            int at = 0;

            while (at < lines.Length && lines[at].Trim().Length == 0)
            {
                at++;
            }

            if (at < lines.Length && lines[at].Trim() == "MLSTYLE")
            {
                current = new List<DxfTypedValue>();
                styles.Add(current);
                at++;
            }

            while (at + 1 < lines.Length)
            {
                if (!int.TryParse(lines[at].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                {
                    throw new FormatException("A file of multiline styles gives a group code on line " + (at + 1).ToString(CultureInfo.InvariantCulture) + ", and '" + lines[at].Trim() + "' is not one.");
                }

                string value = lines[at + 1];
                at += 2;

                if (code == 0)
                {
                    current = value.Trim() == "MLSTYLE" ? new List<DxfTypedValue>() : null;

                    if (current == null)
                    {
                        break;
                    }

                    styles.Add(current);
                    continue;
                }

                // A description keeps its spaces; every other value is read as the reader reads a group's.
                current?.Add(DxfTypedValue.Read(code, code == 3 ? value : value.Trim()));
            }

            return styles;
        }

        #endregion

        #region Shapes

        /// <summary>
        /// The numbers of the shapes of a compiled file of shapes by their names, letters of either case the same: after its
        /// header, <c>AutoCAD-86 shapes 1.0</c> and the byte 0x1A, the first and last numbers and the count, then the number
        /// and length of each shape, then each shape's bytes, its name first, ended by a byte 0. Null where the bytes are not
        /// such a file.
        /// </summary>
        internal static Dictionary<string, int> ShapeNumbers(byte[] bytes)
        {
            int end = Array.IndexOf(bytes, (byte)0x1A, 0, Math.Min(bytes.Length, 64));

            if (end < 0 || !Encoding.ASCII.GetString(bytes, 0, end).StartsWith("AutoCAD-86 shapes", StringComparison.Ordinal) || end + 7 > bytes.Length)
            {
                return null;
            }

            int at = end + 1;
            int count = bytes[at + 4] | (bytes[at + 5] << 8);
            at += 6;

            if (at + count * 4 > bytes.Length)
            {
                return null;
            }

            var numbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int data = at + count * 4;

            for (int i = 0; i < count; i++)
            {
                int number = bytes[at + i * 4] | (bytes[at + i * 4 + 1] << 8);
                int length = bytes[at + i * 4 + 2] | (bytes[at + i * 4 + 3] << 8);

                if (data + length > bytes.Length)
                {
                    return null;
                }

                int zero = Array.IndexOf(bytes, (byte)0, data, length);
                string name = zero < 0 ? string.Empty : Encoding.ASCII.GetString(bytes, data, zero - data);

                if (name.Length > 0 && !numbers.ContainsKey(name))
                {
                    numbers.Add(name, number);
                }

                data += length;
            }

            return numbers;
        }

        #endregion
    }
}
