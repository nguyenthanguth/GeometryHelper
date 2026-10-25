using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The properties of a drawing, as AutoCAD's <c>DatabaseSummaryInfo</c>: its title, subject, author, keywords, comments,
    /// who saved it last, its revision, the base of its hyperlinks and its custom properties, as
    /// <see cref="DxfDatabase.SummaryInfo"/> reads them from DWGPROPS. It does not change: a
    /// <see cref="DxfDatabaseSummaryInfoBuilder"/> makes another.
    /// </summary>
    public sealed class DxfDatabaseSummaryInfo : IEquatable<DxfDatabaseSummaryInfo>
    {
        // The marker DWGPROPS begins with.
        private const string Cookie = "DWGPROPS COOKIE";

        private static readonly int[] TextCodes = { 2, 3, 4, 6, 7, 8, 9 };

        internal static readonly DxfDatabaseSummaryInfo Empty = new DxfDatabaseSummaryInfo(new string[7], string.Empty, new List<KeyValuePair<string, string>>());

        private readonly string[] _texts;

        /// <summary>Properties of the texts in the order of their codes 2, 3, 4, 6, 7, 8, 9, the base of the hyperlinks and the custom properties.</summary>
        internal DxfDatabaseSummaryInfo(string[] texts, string hyperlinkBase, List<KeyValuePair<string, string>> custom)
        {
            _texts = new string[7];

            for (int i = 0; i < 7; i++)
            {
                _texts[i] = texts[i] ?? string.Empty;
            }

            HyperlinkBase = hyperlinkBase ?? string.Empty;
            CustomProperties = custom.AsReadOnly();
        }

        /// <summary>Gets the title of the drawing (group 2 of DWGPROPS), as AutoCAD's <c>Title</c>; empty where it has none.</summary>
        public string Title => _texts[0];

        /// <summary>Gets the subject of the drawing (group 3), as AutoCAD's <c>Subject</c>; empty where it has none.</summary>
        public string Subject => _texts[1];

        /// <summary>Gets the author of the drawing (group 4), as AutoCAD's <c>Author</c>; empty where it has none.</summary>
        public string Author => _texts[2];

        /// <summary>Gets the comments on the drawing (group 6), as AutoCAD's <c>Comments</c>; empty where it has none.</summary>
        public string Comments => _texts[3];

        /// <summary>Gets the keywords of the drawing (group 7), as AutoCAD's <c>Keywords</c>; empty where it has none.</summary>
        public string Keywords => _texts[4];

        /// <summary>Gets who saved the drawing last (group 8), as AutoCAD's <c>LastSavedBy</c>; empty where it says none.</summary>
        public string LastSavedBy => _texts[5];

        /// <summary>Gets the revision of the drawing (group 9), as AutoCAD's <c>RevisionNumber</c>; empty where it has none.</summary>
        public string RevisionNumber => _texts[6];

        /// <summary>Gets the base of the drawing's relative hyperlinks (the group 1 after the dates), as AutoCAD's <c>HyperlinkBase</c>; empty where it has none.</summary>
        public string HyperlinkBase { get; }

        /// <summary>
        /// Gets the custom properties of the drawing, each its name and value, in the order of DWGPROPS (groups 300 to 309, each
        /// <c>name=value</c>), as AutoCAD's <c>CustomProperties</c> gives them; empty where it has none.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> CustomProperties { get; }

        /// <summary>Gets whether two sets of properties are the same, text for text and custom property for custom property in their order.</summary>
        /// <param name="other">The other properties.</param>
        /// <returns>Whether they are.</returns>
        public bool Equals(DxfDatabaseSummaryInfo other)
        {
            if (other is null)
            {
                return false;
            }

            for (int i = 0; i < 7; i++)
            {
                if (!string.Equals(_texts[i], other._texts[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (!string.Equals(HyperlinkBase, other.HyperlinkBase, StringComparison.Ordinal) || CustomProperties.Count != other.CustomProperties.Count)
            {
                return false;
            }

            for (int i = 0; i < CustomProperties.Count; i++)
            {
                if (!string.Equals(CustomProperties[i].Key, other.CustomProperties[i].Key, StringComparison.Ordinal) || !string.Equals(CustomProperties[i].Value, other.CustomProperties[i].Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Gets whether an object is properties the same as these.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => Equals(obj as DxfDatabaseSummaryInfo);

        /// <summary>Gets a hash of the properties, the same for properties that are the same.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode()
        {
            int hash = StringComparer.Ordinal.GetHashCode(HyperlinkBase);

            foreach (string text in _texts)
            {
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(text);
            }

            return hash * 31 + CustomProperties.Count;
        }

        /// <summary>Gets whether two sets of properties are the same, as AutoCAD's <c>==</c>.</summary>
        /// <param name="a">The one.</param>
        /// <param name="b">The other.</param>
        /// <returns>Whether they are.</returns>
        public static bool operator ==(DxfDatabaseSummaryInfo a, DxfDatabaseSummaryInfo b) => a is null ? b is null : a.Equals(b);

        /// <summary>Gets whether two sets of properties differ, as AutoCAD's <c>!=</c>.</summary>
        /// <param name="a">The one.</param>
        /// <param name="b">The other.</param>
        /// <returns>Whether they do.</returns>
        public static bool operator !=(DxfDatabaseSummaryInfo a, DxfDatabaseSummaryInfo b) => !(a == b);

        /// <summary>Gives the properties as a line for debugging: the title and the author.</summary>
        /// <returns>The line, such as <c>Plate B-12 by J. Doe</c>.</returns>
        public override string ToString() => Title + " by " + Author;

        /// <summary>The texts in the order of their codes, for the builder.</summary>
        internal string[] Texts => (string[])_texts.Clone();

        /// <summary>
        /// The properties of the data of a DWGPROPS: the first group of each of the codes 2, 3, 4, 6, 7, 8 and 9, the group 1
        /// after the marker, and the custom properties: each group 300 to 309 of a name, split at its first equals sign, then
        /// those of the list after the group 90 that follows the base of the hyperlinks, its count, each a name and a value in two
        /// groups 1; a name of both kept once, as first found, letters of either case the same.
        /// </summary>
        internal static DxfDatabaseSummaryInfo Read(IReadOnlyList<DxfTypedValue> data)
        {
            var texts = new string[7];
            string hyperlinkBase = null;
            int ones = 0;
            var custom = new List<KeyValuePair<string, string>>();
            int listAt = ListAt(data, out int count);

            for (int i = 0; i < data.Count && (listAt < 0 || i < listAt); i++)
            {
                DxfTypedValue group = data[i];
                int at = Array.IndexOf(TextCodes, (int)group.TypeCode);

                if (at >= 0 && texts[at] == null)
                {
                    texts[at] = group.Value;
                }
                else if (group.TypeCode == 1 && ++ones == 2)
                {
                    hyperlinkBase = group.Value;
                }
                else if (group.TypeCode >= 300 && group.TypeCode <= 309)
                {
                    int equals = group.Value.IndexOf('=');
                    string name = equals < 0 ? group.Value : group.Value.Substring(0, equals);
                    Add(custom, name, equals < 0 ? string.Empty : group.Value.Substring(equals + 1));
                }
            }

            for (int k = 0; listAt >= 0 && k < count; k++)
            {
                Add(custom, data[listAt + 1 + 2 * k].Value, data[listAt + 2 + 2 * k].Value);
            }

            return new DxfDatabaseSummaryInfo(texts, hyperlinkBase, custom);
        }

        /// <summary>
        /// Where the list of custom properties of a DWGPROPS begins: the index of its group 90, the first after the base of the
        /// hyperlinks (the second group 1), with the count it gives where that many pairs of groups 1 follow it; -1 where there is
        /// no such list.
        /// </summary>
        internal static int ListAt(IReadOnlyList<DxfTypedValue> data, out int count)
        {
            count = 0;
            int ones = 0;

            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].TypeCode == 1)
                {
                    ones++;
                }
                else if (data[i].TypeCode == 90 && ones >= 2)
                {
                    if (!int.TryParse(data[i].Value.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out count) || count < 0)
                    {
                        count = 0;
                    }

                    // As many pairs as the count says and the data holds, each two groups 1.
                    int pairs = 0;

                    while (pairs < count && i + 2 + 2 * pairs < data.Count && data[i + 1 + 2 * pairs].TypeCode == 1 && data[i + 2 + 2 * pairs].TypeCode == 1)
                    {
                        pairs++;
                    }

                    count = pairs;
                    return i;
                }
            }

            return -1;
        }

        private static void Add(List<KeyValuePair<string, string>> custom, string name, string value)
        {
            if (name.Trim().Length > 0 && !custom.Exists(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)))
            {
                custom.Add(new KeyValuePair<string, string>(name, value));
            }
        }

        /// <summary>
        /// The data of a DWGPROPS of these properties, as AutoCAD writes it: the marker, the texts, the ten groups of custom
        /// properties 300 to 309, the time spent and the two dates (those of the data kept where it has them, else the header's),
        /// the base of the hyperlinks; where <paramref name="list"/> says so, the custom properties in the list after a group 90
        /// instead, the ten groups each <c>=</c>, so that the record holds one set of them; then the groups the data kept has
        /// after those.
        /// </summary>
        internal List<DxfTypedValue> Written(IReadOnlyList<DxfTypedValue> kept, string editing, string created, string updated, bool list)
        {
            string[] times = { editing ?? "0.0", created ?? "0.0", updated ?? "0.0" };
            var rest = new List<DxfTypedValue>();

            if (kept != null)
            {
                int listAt = ListAt(kept, out int count);
                int ones = 0;
                bool past = false;

                for (int i = 0; i < kept.Count; i++)
                {
                    DxfTypedValue group = kept[i];

                    if (i == listAt)
                    {
                        i += 2 * count;
                        continue;
                    }

                    if (past)
                    {
                        rest.Add(group);
                    }
                    else if (group.TypeCode >= 40 && group.TypeCode <= 42)
                    {
                        times[group.TypeCode - 40] = group.Value;
                    }
                    else if (group.TypeCode == 1 && ++ones == 2)
                    {
                        past = true;
                    }
                }
            }

            var data = new List<DxfTypedValue> { new DxfTypedValue(1, Cookie) };

            for (int i = 0; i < 7; i++)
            {
                data.Add(new DxfTypedValue(TextCodes[i], _texts[i]));
            }

            for (int i = 0; i < 10; i++)
            {
                data.Add(new DxfTypedValue(300 + i, !list && i < CustomProperties.Count ? CustomProperties[i].Key + "=" + CustomProperties[i].Value : "="));
            }

            for (int i = 0; i < 3; i++)
            {
                data.Add(new DxfTypedValue(40 + i, times[i]));
            }

            data.Add(new DxfTypedValue(1, HyperlinkBase));

            if (list)
            {
                data.Add(new DxfTypedValue(90, CustomProperties.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));

                foreach (KeyValuePair<string, string> property in CustomProperties)
                {
                    data.Add(new DxfTypedValue(1, property.Key));
                    data.Add(new DxfTypedValue(1, property.Value));
                }
            }

            data.AddRange(rest);
            return data;
        }

        /// <summary>A text of the properties, one line.</summary>
        internal static string Line(string text, string parameter)
        {
            if (text == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A property of the drawing is one line, with no line break in it.", parameter);
            }

            return text;
        }
    }
}
