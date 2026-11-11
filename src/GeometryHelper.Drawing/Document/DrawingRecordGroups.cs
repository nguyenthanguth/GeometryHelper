using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// The groups of the drawing document's records: how a text, a whole number, a real, a point, links and named values
    /// are written into an XRECORD and read back exactly, and the checks and comparisons the record types share.
    /// </summary>
    /// <remarks>
    /// A text is a group 1 holding its first 250 characters as written, then a group 3 for each further 250; null is a
    /// lone <c>%</c>. As written, a per cent sign, a backslash and control characters are <c>%</c> and two hexadecimal
    /// digits, a lone surrogate <c>%u</c> and four, as <see cref="GeometryHelper.Dxf.Viewer.CadOwnerTag"/> writes its
    /// texts; so a line feed is <c>%0A</c> and nothing else is changed. A whole number is a group 90; a point the groups
    /// 10, 20 and 30, each coordinate in the invariant culture with the fewest digits that bring it back to the bit.
    /// </remarks>
    internal static class DrawingRecordGroups
    {
        /// <summary>The most characters of a text one group holds.</summary>
        internal const int Chunk = 250;

        /// <summary>A null text as written.</summary>
        private const string NullText = "%";

        internal static string Named(string value, string parameter, string message)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (value.Length == 0)
            {
                throw new ArgumentException(message, parameter);
            }

            return value;
        }

        internal static KeyValuePair<string, string>[] Values(
            IEnumerable<KeyValuePair<string, string>> values,
            string parameter
        )
        {
            var list = new List<KeyValuePair<string, string>>();

            foreach (KeyValuePair<string, string> value in values ?? new KeyValuePair<string, string>[0])
            {
                if (value.Key == null)
                {
                    throw new ArgumentException("A named value has a name.", parameter);
                }

                list.Add(value);
            }

            return list.ToArray();
        }

        internal static bool SameBits(double a, double b) =>
            BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        internal static bool SameValues(KeyValuePair<string, string>[] a, KeyValuePair<string, string>[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (
                    !string.Equals(a[i].Key, b[i].Key, StringComparison.Ordinal)
                    || !string.Equals(a[i].Value, b[i].Value, StringComparison.Ordinal)
                )
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool SameGroups(IReadOnlyList<DxfTypedValue> a, IReadOnlyList<DxfTypedValue> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].TypeCode != b[i].TypeCode || !string.Equals(a[i].Value, b[i].Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A real in the fewest digits that read back to the same bits; negative zero as "-0".</summary>
        internal static string Number(double value)
        {
            if (value == 0.0)
            {
                return BitConverter.DoubleToInt64Bits(value) < 0 ? "-0" : "0";
            }

            string text = value.ToString("R", CultureInfo.InvariantCulture);

            // "R" misses the last bit of a few numbers under .NET Framework; 17 digits never do.
            return
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double read)
                && SameBits(read, value)
                ? text
                : value.ToString("G17", CultureInfo.InvariantCulture);
        }

        /// <summary>A real as <see cref="Number"/> writes it; "-0" as negative zero, which .NET Framework reads as zero.</summary>
        internal static double ParseNumber(string text)
        {
            string trimmed = text.Trim();
            double value = double.Parse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture);

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new FormatException("The real " + trimmed + " is not a finite number.");
            }

            return value == 0.0 && trimmed.StartsWith("-", StringComparison.Ordinal) ? -0.0 : value;
        }

        /// <summary>A text as written: see the remarks of the class.</summary>
        internal static string Escape(string text)
        {
            if (text == null)
            {
                return NullText;
            }

            StringBuilder written = null;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string escape = null;

                if (c == '%' || c == '\\' || c < ' ' || c == '\u007F')
                {
                    escape = "%" + ((int)c).ToString("X2", CultureInfo.InvariantCulture);
                }
                else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    written?.Append(c).Append(text[i + 1]);
                    i++;
                    continue;
                }
                else if (char.IsSurrogate(c))
                {
                    escape = "%u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
                }

                if (escape != null && written == null)
                {
                    written = new StringBuilder(text, 0, i, text.Length + 16);
                }

                if (escape != null)
                {
                    written.Append(escape);
                }
                else
                {
                    written?.Append(c);
                }
            }

            return written?.ToString() ?? text;
        }

        /// <summary>A text as it was before <see cref="Escape"/>.</summary>
        /// <exception cref="FormatException">Thrown when a per cent sign begins no escape.</exception>
        internal static string Unescape(string written)
        {
            if (written == NullText)
            {
                return null;
            }

            if (written.IndexOf('%') < 0)
            {
                return written;
            }

            var read = new StringBuilder(written.Length);

            for (int i = 0; i < written.Length; i++)
            {
                if (written[i] != '%')
                {
                    read.Append(written[i]);
                    continue;
                }

                bool wide = i + 1 < written.Length && written[i + 1] == 'u';
                int start = wide ? i + 2 : i + 1;
                int digits = wide ? 4 : 2;

                if (
                    start + digits > written.Length
                    || !int.TryParse(
                        written.Substring(start, digits),
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out int code
                    )
                )
                {
                    throw new FormatException(
                        "A per cent sign at "
                            + i.ToString(CultureInfo.InvariantCulture)
                            + " of a text begins no escape."
                    );
                }

                read.Append((char)code);
                i = start + digits - 1;
            }

            return read.ToString();
        }

        /// <summary>Writes the groups of one record.</summary>
        internal sealed class Writer
        {
            private readonly List<DxfTypedValue> _groups;

            internal Writer(List<DxfTypedValue> groups)
            {
                _groups = groups;
            }

            internal void Text(string text)
            {
                string written = Escape(text);
                int at = 0;
                int code = 1;

                do
                {
                    int length = Math.Min(Chunk, written.Length - at);

                    // A surrogate pair stays in one group, which is written as one letter.
                    if (at + length < written.Length && char.IsHighSurrogate(written[at + length - 1]))
                    {
                        length--;
                    }

                    _groups.Add(new DxfTypedValue(code, written.Substring(at, length)));
                    at += length;
                    code = 3;
                } while (at < written.Length);
            }

            internal void Integer(int value) =>
                _groups.Add(new DxfTypedValue(90, value.ToString(CultureInfo.InvariantCulture)));

            internal void Point(GeoPoint3 point)
            {
                _groups.Add(new DxfTypedValue(10, Number(point.X)));
                _groups.Add(new DxfTypedValue(20, Number(point.Y)));
                _groups.Add(new DxfTypedValue(30, Number(point.Z)));
            }

            internal void Links(DrawingLinks links)
            {
                Integer(links.ObjectIds.Count);

                foreach (string id in links.ObjectIds)
                {
                    Text(id);
                }

                Integer(links.Points.Count);

                foreach (DrawingLinkPoint3 point in links.Points)
                {
                    Text(point.ObjectId);
                    Point(point.Point);
                }
            }

            internal void Values(IReadOnlyList<KeyValuePair<string, string>> values)
            {
                Integer(values.Count);

                foreach (KeyValuePair<string, string> value in values)
                {
                    Text(value.Key);
                    Text(value.Value);
                }
            }
        }

        /// <summary>Reads the groups of one record, from its first group after the opening 102 to the closing one.</summary>
        internal sealed class Reader
        {
            private readonly IReadOnlyList<DxfTypedValue> _groups;
            private readonly int _end;
            private int _at;

            internal Reader(IReadOnlyList<DxfTypedValue> groups, int start, int end)
            {
                _groups = groups;
                _at = start;
                _end = end;
            }

            internal string Text()
            {
                string first = Take(1);

                if (_at >= _end || _groups[_at].TypeCode != 3)
                {
                    return Unescape(first);
                }

                var written = new StringBuilder(first);

                while (_at < _end && _groups[_at].TypeCode == 3)
                {
                    written.Append(_groups[_at].Value);
                    _at++;
                }

                return Unescape(written.ToString());
            }

            internal int Integer() => int.Parse(Take(90).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);

            /// <summary>A count of items each at least one group long: not below nought, nor above the groups left.</summary>
            internal int Count()
            {
                int count = Integer();

                if (count < 0 || count > _end - _at)
                {
                    throw new FormatException(
                        "A count of " + count.ToString(CultureInfo.InvariantCulture) + " does not fit the groups left."
                    );
                }

                return count;
            }

            internal GeoPoint3 Point()
            {
                double x = ParseNumber(Take(10));
                double y = ParseNumber(Take(20));
                double z = ParseNumber(Take(30));
                return new GeoPoint3(x, y, z);
            }

            internal DrawingLinks Links()
            {
                var ids = new string[Count()];

                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = Text();
                }

                var points = new DrawingLinkPoint3[Count()];

                for (int i = 0; i < points.Length; i++)
                {
                    string objectId = Text();
                    points[i] = new DrawingLinkPoint3(objectId, Point());
                }

                return ids.Length == 0 && points.Length == 0 ? DrawingLinks.Empty : new DrawingLinks(ids, points);
            }

            internal KeyValuePair<string, string>[] Values()
            {
                var values = new KeyValuePair<string, string>[Count()];

                for (int i = 0; i < values.Length; i++)
                {
                    string name = Text();
                    values[i] = new KeyValuePair<string, string>(name, Text());
                }

                return values;
            }

            /// <summary>Checks that every group of the record was read: a record with more is not of this layout.</summary>
            internal void End()
            {
                if (_at != _end)
                {
                    throw new FormatException(
                        "The record has "
                            + (_end - _at).ToString(CultureInfo.InvariantCulture)
                            + " groups more than its layout."
                    );
                }
            }

            private string Take(int code)
            {
                if (_at >= _end)
                {
                    throw new FormatException(
                        "The record ends where a group " + code.ToString(CultureInfo.InvariantCulture) + " was due."
                    );
                }

                DxfTypedValue group = _groups[_at];

                if (group.TypeCode != code)
                {
                    throw new FormatException(
                        "A group "
                            + group.TypeCode.ToString(CultureInfo.InvariantCulture)
                            + " stands where a group "
                            + code.ToString(CultureInfo.InvariantCulture)
                            + " was due."
                    );
                }

                _at++;
                return group.Value;
            }
        }
    }
}
