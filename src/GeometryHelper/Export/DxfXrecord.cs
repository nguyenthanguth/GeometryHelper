using System;
using System.Collections;
using System.Collections.Generic;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An XRECORD of a DXF file, as AutoCAD's <c>Xrecord</c>: a list of groups an application keeps under a name of a
    /// <see cref="DxfDBDictionary"/>, read and written as the file holds them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its <see cref="Data"/> is what AutoCAD gives as a <c>ResultBuffer</c>: the groups after its subclass marker and its
    /// cloning flag (280), each a code and the text the file prints, <c>new DxfTypedValue(1, "B-12")</c> a text and
    /// <c>new DxfTypedValue(40, "2.5")</c> a real. Made in code and added to a dictionary,
    /// <c>db.NamedObjectsDictionary.SetAt("GH_PLATE", new DxfXrecord { Data = new[] { new DxfTypedValue(1, "B-12"), new DxfTypedValue(40, "12.5") } })</c>
    /// writes an XRECORD of two groups owned by the named object dictionary, as AutoCAD writes one.
    /// </para>
    /// <para>
    /// The groups 102 of the data are the data's own: the overrides AutoCAD keeps for a layer in a viewport are XRECORDs of
    /// groups such as <c>102 {ADSK_LYR_COLOR_OVERRIDE</c>, <c>335</c> the viewport, <c>420</c> the colour, <c>102 }</c>.
    /// </para>
    /// </remarks>
    public sealed class DxfXrecord : DxfDBObject, IEnumerable<DxfTypedValue>
    {
        private List<DxfTypedValue> _made = new List<DxfTypedValue>();

        /// <summary>
        /// Initializes an XRECORD made in code, with no data, to be added to a dictionary by <see cref="DxfDBDictionary.SetAt"/>,
        /// as AutoCAD's <c>Xrecord()</c> is.
        /// </summary>
        public DxfXrecord()
        {
        }

        internal DxfXrecord(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the data of the XRECORD, as AutoCAD's <c>Data</c>: its groups after the subclass marker AcDbXrecord and,
        /// from R2000 on, the cloning flag (280), up to its extended data; each text (codes 1 to 9 and 300 to 309) with its escapes
        /// (<c>\U+00E9</c>) read as letters.
        /// </summary>
        /// <value>
        /// Set, the groups in the place of those it has, each one the same as the one in its place keeping its bytes; data the
        /// same as it has changes nothing. A text of letters the file's code page has not is written with their escapes.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the groups set are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a group set is of a code an XRECORD does not hold: 0, 5, 100, 105, 999
        /// and the codes of extended data, 1000 on; or the groups 102 set do not open and close in pairs.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the XRECORD is erased.</exception>
        public IReadOnlyList<DxfTypedValue> Data
        {
            get
            {
                if (Item == null)
                {
                    return _made.AsReadOnly();
                }

                return Item.File.TailOf(Item, DataStart()).AsReadOnly();
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                // The data it has changes nothing, whatever the file holds.
                if (Same(value, Data))
                {
                    return;
                }

                List<KeyValuePair<int, string>> groups = Checked(value, nameof(value));

                if (Item == null)
                {
                    _made = new List<DxfTypedValue>(value);
                    return;
                }

                DxfFile file = InFile("be given data");
                file.ReplaceTail(Item, DataStart(), groups);
            }
        }

        /// <summary>
        /// Adds groups at the end of the data, as AutoCAD's <c>Append</c> adds those of a result buffer; none changes nothing.
        /// </summary>
        /// <param name="data">The groups.</param>
        /// <exception cref="ArgumentNullException">Thrown when the groups are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a group is of a code an XRECORD does not hold, as <see cref="Data"/> says.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the XRECORD is erased.</exception>
        public void Append(IEnumerable<DxfTypedValue> data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var all = new List<DxfTypedValue>(Data);
            all.AddRange(data);
            Data = all;
        }

        /// <summary>Gets an enumerator over the groups of the data, as AutoCAD's <c>GetEnumerator</c>.</summary>
        /// <returns>The enumerator, over the data as it is when it is asked for.</returns>
        public IEnumerator<DxfTypedValue> GetEnumerator() => Data.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Erases the XRECORD, as AutoCAD's <c>Erase</c>: it is taken out of the file, and the entry of the dictionary that
        /// names it with it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the XRECORD is in no file, or is erased.</exception>
        public override void Erase() => DxfDBDictionary.EraseFromOwner(this, InFile("be erased"));

        /// <summary>Gives the XRECORD as a line for debugging: its type, its handle and how many groups its data has.</summary>
        /// <returns>The line, such as <c>XRECORD 2F, 3 groups</c>.</returns>
        public override string ToString() => "XRECORD " + (Handle ?? string.Empty) + ", " + Data.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " groups";

        /// <summary>The groups an XRECORD made in code is written with, after its subclass marker and cloning flag.</summary>
        internal IReadOnlyList<DxfTypedValue> MadeData => _made;

        /// <summary>
        /// Where the data begins among the groups of the record, as <see cref="DxfItem.Groups"/> counts them: after the subclass
        /// marker AcDbXrecord, and the cloning flag (280) that follows it from R2000 on.
        /// </summary>
        private int DataStart()
        {
            IReadOnlyList<DxfTypedValue> groups = Item.File.GroupsOf(Item);
            bool braced = false;

            for (int i = 0; i < groups.Count; i++)
            {
                DxfTypedValue group = groups[i];

                if (group.TypeCode == 102)
                {
                    braced = group.Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && group.TypeCode == 100 && group.Value.Trim() == "AcDbXrecord")
                {
                    return i + 1 < groups.Count && groups[i + 1].TypeCode == 280 && Item.File.Supports(1015) ? i + 2 : i + 1;
                }
                else if (group.TypeCode == 1001)
                {
                    return i;
                }
            }

            return groups.Count;
        }

        /// <summary>The groups as they are written, each checked to be one an XRECORD holds.</summary>
        private static List<KeyValuePair<int, string>> Checked(IReadOnlyList<DxfTypedValue> data, string parameter)
        {
            if (data == null)
            {
                throw new ArgumentNullException(parameter);
            }

            var groups = new List<KeyValuePair<int, string>>(data.Count);
            int open = 0;

            foreach (DxfTypedValue group in data)
            {
                int code = group.TypeCode;

                if (group.Value == null || code <= 0 || code == 5 || code == 100 || code == 105 || code >= 999)
                {
                    throw new ArgumentException("An XRECORD holds groups of codes 1 to 998 but 5, 100 and 105: " + group + " is not one.", parameter);
                }

                if (code == 102)
                {
                    bool opens = group.Value.Trim().StartsWith("{", StringComparison.Ordinal);
                    open += opens ? 1 : -1;

                    if (open < 0 || open > 1 || (!opens && group.Value.Trim() != "}"))
                    {
                        throw new ArgumentException("The groups 102 of an XRECORD's data open a list ('{NAME') and close it ('}') in pairs, one at a time.", parameter);
                    }
                }

                groups.Add(new KeyValuePair<int, string>(code, group.Value));
            }

            if (open != 0)
            {
                throw new ArgumentException("A group 102 of the XRECORD's data opens a list it does not close.", parameter);
            }

            return groups;
        }

        /// <summary>Whether two runs of groups are the same, code for code and text for text.</summary>
        private static bool Same(IReadOnlyList<DxfTypedValue> a, IReadOnlyList<DxfTypedValue> b)
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
    }
}
