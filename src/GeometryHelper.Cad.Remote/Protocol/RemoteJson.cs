using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// A JSON object: its members in the order they were set, so that a message is written the same way every time. A
    /// member is a string, a number (<see cref="long"/> or <see cref="double"/>), a bool, null, a <see cref="JsonArray"/> or
    /// another <see cref="JsonObject"/>.
    /// </summary>
    internal sealed class JsonObject
    {
        private readonly List<KeyValuePair<string, object>> _members = new List<KeyValuePair<string, object>>();

        /// <summary>Sets a member, replacing the one of the name; returns the object, so that a message is built in one chain.</summary>
        public JsonObject Set(string name, object value)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].Key, name, StringComparison.Ordinal))
                {
                    _members[i] = new KeyValuePair<string, object>(name, value);
                    return this;
                }
            }

            _members.Add(new KeyValuePair<string, object>(name, value));
            return this;
        }

        /// <summary>Gets the members in the order they were set.</summary>
        public IReadOnlyList<KeyValuePair<string, object>> Members => _members;

        /// <summary>Gets a member, or null where there is none.</summary>
        public object Get(string name)
        {
            foreach (KeyValuePair<string, object> member in _members)
            {
                if (string.Equals(member.Key, name, StringComparison.Ordinal))
                {
                    return member.Value;
                }
            }

            return null;
        }

        /// <summary>Gets a string member; null where there is none or it is no string.</summary>
        public string GetString(string name) => Get(name) as string;

        /// <summary>Gets a whole-number member; the fallback where there is none.</summary>
        public long GetLong(string name, long fallback = 0)
        {
            object value = Get(name);
            return value is long whole ? whole
                : value is double real ? (long)real
                : fallback;
        }

        /// <summary>Gets a bool member; the fallback where there is none.</summary>
        public bool GetBool(string name, bool fallback = false) => Get(name) is bool flag ? flag : fallback;

        /// <summary>Gets an array member; an empty array where there is none.</summary>
        public JsonArray GetArray(string name) => Get(name) as JsonArray ?? new JsonArray();

        /// <summary>Gets an object member; null where there is none.</summary>
        public JsonObject GetObject(string name) => Get(name) as JsonObject;
    }

    /// <summary>A JSON array.</summary>
    internal sealed class JsonArray : List<object>
    {
        /// <summary>Makes an empty array.</summary>
        public JsonArray() { }

        /// <summary>Makes an array of the items.</summary>
        public JsonArray(IEnumerable<object> items)
            : base(items) { }
    }

    /// <summary>
    /// The JSON the remote channel speaks, written and read here and by nothing else: objects, arrays, strings, whole
    /// numbers and doubles, true, false and null, with no comments and no trailing commas. Written without spaces, in
    /// UTF-8; a double is written in the round-trip form, so that a coordinate comes back as it went.
    /// </summary>
    internal static class RemoteJson
    {
        /// <summary>Writes a value as JSON text.</summary>
        public static string Write(object value)
        {
            var text = new StringBuilder();
            WriteValue(text, value);
            return text.ToString();
        }

        /// <summary>Reads JSON text into a value.</summary>
        /// <exception cref="FormatException">Thrown when the text is not JSON this reader takes.</exception>
        public static object Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            int at = 0;
            object value = ReadValue(text, ref at);
            Skip(text, ref at);

            if (at != text.Length)
            {
                throw new FormatException("Unexpected text after the JSON value, at " + at + ".");
            }

            return value;
        }

        private static void WriteValue(StringBuilder text, object value)
        {
            switch (value)
            {
                case null:
                    text.Append("null");
                    break;
                case string s:
                    WriteString(text, s);
                    break;
                case bool b:
                    text.Append(b ? "true" : "false");
                    break;
                case int i:
                    text.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    text.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d))
                    {
                        throw new ArgumentException("A number in a message has to be finite.");
                    }

                    text.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonObject o:
                    text.Append('{');

                    for (int m = 0; m < o.Members.Count; m++)
                    {
                        if (m > 0)
                        {
                            text.Append(',');
                        }

                        WriteString(text, o.Members[m].Key);
                        text.Append(':');
                        WriteValue(text, o.Members[m].Value);
                    }

                    text.Append('}');
                    break;
                case JsonArray a:
                    text.Append('[');

                    for (int n = 0; n < a.Count; n++)
                    {
                        if (n > 0)
                        {
                            text.Append(',');
                        }

                        WriteValue(text, a[n]);
                    }

                    text.Append(']');
                    break;
                default:
                    throw new ArgumentException("A " + value.GetType().Name + " has no JSON form.");
            }
        }

        private static void WriteString(StringBuilder text, string value)
        {
            text.Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        text.Append("\\\"");
                        break;
                    case '\\':
                        text.Append("\\\\");
                        break;
                    case '\n':
                        text.Append("\\n");
                        break;
                    case '\r':
                        text.Append("\\r");
                        break;
                    case '\t':
                        text.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            text.Append(c);
                        }

                        break;
                }
            }

            text.Append('"');
        }

        private static void Skip(string text, ref int at)
        {
            while (at < text.Length && (text[at] == ' ' || text[at] == '\n' || text[at] == '\r' || text[at] == '\t'))
            {
                at++;
            }
        }

        private static object ReadValue(string text, ref int at)
        {
            Skip(text, ref at);

            if (at >= text.Length)
            {
                throw new FormatException("The JSON text ends where a value is expected.");
            }

            char c = text[at];

            if (c == '{')
            {
                at++;
                var o = new JsonObject();
                Skip(text, ref at);

                if (at < text.Length && text[at] == '}')
                {
                    at++;
                    return o;
                }

                while (true)
                {
                    Skip(text, ref at);
                    string name = ReadString(text, ref at);
                    Skip(text, ref at);
                    Expect(text, ref at, ':');
                    o.Set(name, ReadValue(text, ref at));
                    Skip(text, ref at);

                    if (at < text.Length && text[at] == ',')
                    {
                        at++;
                        continue;
                    }

                    Expect(text, ref at, '}');
                    return o;
                }
            }

            if (c == '[')
            {
                at++;
                var a = new JsonArray();
                Skip(text, ref at);

                if (at < text.Length && text[at] == ']')
                {
                    at++;
                    return a;
                }

                while (true)
                {
                    a.Add(ReadValue(text, ref at));
                    Skip(text, ref at);

                    if (at < text.Length && text[at] == ',')
                    {
                        at++;
                        continue;
                    }

                    Expect(text, ref at, ']');
                    return a;
                }
            }

            if (c == '"')
            {
                return ReadString(text, ref at);
            }

            if (Matches(text, at, "true"))
            {
                at += 4;
                return true;
            }

            if (Matches(text, at, "false"))
            {
                at += 5;
                return false;
            }

            if (Matches(text, at, "null"))
            {
                at += 4;
                return null;
            }

            return ReadNumber(text, ref at);
        }

        private static bool Matches(string text, int at, string word) =>
            string.CompareOrdinal(text, at, word, 0, word.Length) == 0;

        private static void Expect(string text, ref int at, char c)
        {
            if (at >= text.Length || text[at] != c)
            {
                throw new FormatException("Expected '" + c + "' at " + at + " of the JSON text.");
            }

            at++;
        }

        private static object ReadNumber(string text, ref int at)
        {
            int start = at;
            bool real = false;

            while (
                at < text.Length
                && (
                    char.IsDigit(text[at])
                    || text[at] == '-'
                    || text[at] == '+'
                    || text[at] == '.'
                    || text[at] == 'e'
                    || text[at] == 'E'
                )
            )
            {
                real |= text[at] == '.' || text[at] == 'e' || text[at] == 'E';
                at++;
            }

            string word = text.Substring(start, at - start);

            if (
                !real
                && long.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole)
            )
            {
                return whole;
            }

            if (
                word.Length > 0
                && double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            )
            {
                return number;
            }

            throw new FormatException("Unexpected text at " + start + " of the JSON text.");
        }

        private static string ReadString(string text, ref int at)
        {
            Expect(text, ref at, '"');
            var value = new StringBuilder();

            while (at < text.Length)
            {
                char c = text[at++];

                if (c == '"')
                {
                    return value.ToString();
                }

                if (c != '\\')
                {
                    value.Append(c);
                    continue;
                }

                if (at >= text.Length)
                {
                    break;
                }

                char escape = text[at++];

                switch (escape)
                {
                    case 'n':
                        value.Append('\n');
                        break;
                    case 'r':
                        value.Append('\r');
                        break;
                    case 't':
                        value.Append('\t');
                        break;
                    case 'b':
                        value.Append('\b');
                        break;
                    case 'f':
                        value.Append('\f');
                        break;
                    case 'u':
                        if (
                            at + 4 > text.Length
                            || !int.TryParse(
                                text.Substring(at, 4),
                                NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture,
                                out int code
                            )
                        )
                        {
                            throw new FormatException("A bad \\u escape at " + at + " of the JSON text.");
                        }

                        value.Append((char)code);
                        at += 4;
                        break;
                    default:
                        value.Append(escape);
                        break;
                }
            }

            throw new FormatException("A string is not closed in the JSON text.");
        }
    }
}
