using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A field of a DXF file, as AutoCAD's <c>Field</c>: a FIELD of the OBJECTS section, the code AutoCAD evaluates to the words
    /// a text shows, kept in the extension dictionary of the object it belongs to, in its dictionary ACAD_FIELD, and reached by
    /// <see cref="DxfDBObject.GetField()"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A text with fields in it holds a text field, of the evaluator <c>_text</c>, whose code is the text with each field standing
    /// as <c>%&lt;\_FldIdx 0&gt;%</c>, <c>%&lt;\_FldIdx 1&gt;%</c> and on, the fields its children in that order (90 and 360). The
    /// TEXT that reads Drawn 27/10/2026, the date a field, has the text field of the code <c>Drawn %&lt;\_FldIdx 0&gt;%</c> and one
    /// child, of the evaluator AcVar and the code <c>\AcVar Date \f "dd/MM/yyyy"</c>, and its
    /// <see cref="DxfDBText.GetTextWithFieldCodes"/> gives <c>Drawn %&lt;\AcVar Date \f "dd/MM/yyyy"&gt;%</c>.
    /// </para>
    /// <para>
    /// The library does not evaluate a field: the words of the text are those AutoCAD wrote when it last evaluated it, and
    /// <see cref="GetStringValue"/> the value it kept. A field made in code, <c>new DxfField("Drawn %&lt;\\AcVar Date&gt;%", true)</c>,
    /// and given to a text by <see cref="DxfDBObject.SetField(DxfField)"/>, is written in the groups the DXF reference gives a
    /// FIELD, marked not evaluated (95 of 1) and to be evaluated whenever AutoCAD evaluates its fields (91 of 63), the text keeping
    /// its words until then. That layout has not been read by AutoCAD itself yet: the state (94 of 0), the empty value's flag
    /// (93 of 1) and, before R2007, the format (group 4 between 331 and 91, which libredwg reads in a DWG of that version and the
    /// library does not write) are the library's reading of the reference and of libredwg, to be checked on a drawing AutoCAD saved.
    /// </para>
    /// </remarks>
    public sealed class DxfField : DxfDBObject
    {
        // The evaluator of a text with fields in it.
        private const string TextEvaluator = "_text";

        private readonly string _madeEvaluator;
        private readonly string _madeCode;
        private readonly List<DxfField> _madeChildren = new List<DxfField>();

        /// <summary>
        /// Initializes a field made in code of the code of one field, as AutoCAD's <c>Field(fieldCode)</c>: the code in its
        /// markers, <c>%&lt;\AcVar Date \f "dd/MM/yyyy"&gt;%</c>, its evaluator the word after the backslash, AcVar, its code what
        /// the markers hold; to be given to an object by <see cref="DxfDBObject.SetField(DxfField)"/>.
        /// </summary>
        /// <param name="fieldCode">The code of the field, in its markers <c>%&lt;</c> and <c>&gt;%</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the code is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the code has a line break in it, is not one field in its markers, or
        /// names no evaluator after the backslash it begins with.</exception>
        public DxfField(string fieldCode)
            : this(fieldCode, false)
        {
        }

        /// <summary>
        /// Initializes a field made in code, as AutoCAD's <c>Field(fieldCode, textField)</c>: of one field as
        /// <see cref="DxfField(string)"/>, or, as a text field, of a text with fields in it, each field in its markers, its
        /// evaluator <c>_text</c>: <c>new DxfField("Drawn %&lt;\\AcVar Date&gt;% by %&lt;\\AcVar Login&gt;%", true)</c> is the
        /// text field of the code <c>Drawn %&lt;\_FldIdx 0&gt;% by %&lt;\_FldIdx 1&gt;%</c> with the two fields as its children. A
        /// marker inside a field, as the object of <c>%&lt;\AcObjProp Object(%&lt;\_ObjId 2130&gt;%).Area&gt;%</c>, is part of that
        /// field's code, kept as written: the library names no object for it (97 of 0 and no 331 written), so that AutoCAD cannot
        /// find the object such a field reads, and shows it as one it could not evaluate.
        /// </summary>
        /// <param name="fieldCode">The code: one field in its markers, or a text with fields in it.</param>
        /// <param name="textField">Whether the code is a text with fields in it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the code is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the code has a line break in it or a field marker it does not close,
        /// or, of one field, is not one field in its markers or names no evaluator.</exception>
        public DxfField(string fieldCode, bool textField)
        {
            if (fieldCode == null)
            {
                throw new ArgumentNullException(nameof(fieldCode));
            }

            if (fieldCode.IndexOf('\n') >= 0 || fieldCode.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("The code of a field is written on one line: it has no line break in it.", nameof(fieldCode));
            }

            if (!textField)
            {
                string field = fieldCode.Trim();

                if (!field.StartsWith("%<", StringComparison.Ordinal) || Closing(field, 0) != field.Length - 2)
                {
                    throw new ArgumentException("The code of one field is that field in its markers, as %<\\AcVar Date>%: a text with fields in it is a text field, new DxfField(code, true).", nameof(fieldCode));
                }

                string inner = field.Substring(2, field.Length - 4);
                int end = 1;

                while (end < inner.Length && (char.IsLetterOrDigit(inner[end]) || inner[end] == '_'))
                {
                    end++;
                }

                if (inner.Length == 0 || inner[0] != '\\' || end == 1)
                {
                    throw new ArgumentException("The code of a field begins with its evaluator after a backslash, as \\AcVar Date: '" + fieldCode + "' names none.", nameof(fieldCode));
                }

                _madeEvaluator = inner.Substring(1, end - 1);
                _madeCode = inner;
                return;
            }

            var code = new StringBuilder(fieldCode.Length);
            int at = 0;

            while (at < fieldCode.Length)
            {
                int open = fieldCode.IndexOf("%<", at, StringComparison.Ordinal);

                if (open < 0)
                {
                    code.Append(fieldCode, at, fieldCode.Length - at);
                    break;
                }

                int close = Closing(fieldCode, open);

                if (close < 0)
                {
                    throw new ArgumentException("The code '" + fieldCode + "' opens a field with %< that it does not close with >%.", nameof(fieldCode));
                }

                code.Append(fieldCode, at, open - at);
                code.Append(Placeholder(_madeChildren.Count));
                _madeChildren.Add(new DxfField(fieldCode.Substring(open, close + 2 - open), false));
                at = close + 2;
            }

            _madeEvaluator = TextEvaluator;
            _madeCode = code.ToString();
        }

        internal DxfField(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets the evaluator of the field (group 1), as AutoCAD's <c>EvaluatorId</c>: <c>_text</c> for a text with fields in it,
        /// AcVar for a variable of the drawing, AcObjProp for a property of an object, AcExpr for a formula; empty where the
        /// record gives none.
        /// </summary>
        public string EvaluatorId => Item == null ? _madeEvaluator : Text(Own(), 1) ?? string.Empty;

        /// <summary>
        /// Gets whether the field is a text with fields in it, as AutoCAD's <c>IsTextField</c>: its evaluator <c>_text</c>.
        /// </summary>
        public bool IsTextField => string.Equals(EvaluatorId, TextEvaluator, StringComparison.Ordinal);

        /// <summary>
        /// Gets the code of the field (group 2, and the groups 3 that carry it on past 250 characters), as AutoCAD's
        /// <c>GetFieldCode()</c>: as the file holds it, a child standing as <c>%&lt;\_FldIdx 0&gt;%</c>, the object of a property
        /// as <c>%&lt;\_ObjIdx 0&gt;%</c>; <c>\AcVar Date \f "dd/MM/yyyy"</c> for a date, <c>Drawn %&lt;\_FldIdx 0&gt;%</c> for a
        /// text with it in it. Empty where the record gives none.
        /// </summary>
        /// <returns>The code.</returns>
        public string GetFieldCode()
        {
            if (Item == null)
            {
                return _madeCode;
            }

            var code = new StringBuilder();

            foreach (DxfTypedValue group in Own())
            {
                if (group.TypeCode == 90)
                {
                    break;
                }

                if (group.TypeCode == 2 || group.TypeCode == 3)
                {
                    code.Append(group.Value);
                }
            }

            return code.ToString();
        }

        /// <summary>
        /// Gets the fields of the field, as AutoCAD's <c>GetChildren</c>: those its groups 360 name, in their order, each the
        /// field <c>%&lt;\_FldIdx&gt;%</c> of its index stands for; null in the place of a handle that names no FIELD of the file,
        /// so that each keeps the index its placeholder gives it.
        /// </summary>
        /// <returns>The fields, a new array on each call, null in the place of a handle that names no FIELD; empty where it has none.</returns>
        public DxfField[] GetChildren()
        {
            if (Item == null)
            {
                return _madeChildren.ToArray();
            }

            var children = new List<DxfField>();

            foreach (DxfTypedValue group in Own())
            {
                if (group.TypeCode == 360)
                {
                    DxfItem child = Item.File.ObjectItem(group.Value);
                    children.Add(child == null ? null : OfObject(child) as DxfField);
                }
            }

            return children.ToArray();
        }

        /// <summary>Whether the field, or a field it holds at any depth, is in a file already, as one given to an object is.</summary>
        internal bool AnyInFile()
        {
            if (Item != null)
            {
                return true;
            }

            foreach (DxfField child in _madeChildren)
            {
                if (child.AnyInFile())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the value AutoCAD kept for the field when it last evaluated it, as AutoCAD's <c>GetStringValue</c>: the text of its
        /// cache, the value after <c>7 ACFD_FIELD_VALUE</c>, as it was shown (302), or, where it has none, its text (1); empty
        /// where the field has no value kept, as one made in code.
        /// </summary>
        /// <remarks>
        /// Which group holds the value is the library's reading of the DXF reference and of libredwg, not yet checked on a drawing
        /// AutoCAD saved: an empty 302 is taken before a 1 that has words, and the 301 after the cache, which the reference calls
        /// the format and libredwg the value as text, is not read.
        /// </remarks>
        /// <returns>The value, as a text.</returns>
        public string GetStringValue()
        {
            if (Item == null)
            {
                return string.Empty;
            }

            List<DxfTypedValue> own = Own();
            int start = own.FindIndex(g => g.TypeCode == 7 && g.Value.Trim() == "ACFD_FIELD_VALUE");
            string text = null;

            for (int i = start + 1; start >= 0 && i < own.Count; i++)
            {
                int code = own[i].TypeCode;

                if (code == 304 || code == 301 || code == 6 || code == 7)
                {
                    break;
                }

                if (code == 302)
                {
                    return own[i].Value;
                }

                if (code == 1 && text == null)
                {
                    text = own[i].Value;
                }
            }

            return text ?? string.Empty;
        }

        /// <summary>Gives the field as a line for debugging: its type, its handle, its evaluator and its code.</summary>
        /// <returns>The line, such as <c>FIELD 2F AcVar \AcVar Date</c>.</returns>
        public override string ToString() => "FIELD " + (Handle ?? string.Empty) + " " + EvaluatorId + " " + GetFieldCode();

        /// <summary>
        /// The text with the codes of a field in place: the code of a text field, of one field that code in its markers, each
        /// child's placeholder <c>%&lt;\_FldIdx n&gt;%</c> standing as the child in its markers, at every depth; a placeholder of
        /// no child kept as written, and one of a field among those it stands inside, as in a file whose fields own each other, too.
        /// </summary>
        internal static string WithCodes(DxfField field)
        {
            var inside = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (field.Handle != null)
            {
                inside.Add(field.Handle);
            }

            return WithCodes(field, inside, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), true);
        }

        /// <summary>
        /// <see cref="WithCodes(DxfField)"/> with the handles of the fields it stands inside, and the text of each field of the
        /// file worked out so far, so that a field two others name is worked out once.
        /// </summary>
        private static string WithCodes(DxfField field, HashSet<string> inside, Dictionary<string, string> done, bool top)
        {
            if (!top && field.Handle != null && done.TryGetValue(field.Handle, out string known))
            {
                return known;
            }

            string code = field.GetFieldCode();
            DxfField[] children = field.GetChildren();
            var text = new StringBuilder(code.Length);
            int at = 0;

            while (at < code.Length)
            {
                int open = code.IndexOf(@"%<\_FldIdx ", at, StringComparison.Ordinal);
                int close = open < 0 ? -1 : code.IndexOf(">%", open, StringComparison.Ordinal);

                if (close < 0)
                {
                    text.Append(code, at, code.Length - at);
                    break;
                }

                text.Append(code, at, open - at);
                string index = code.Substring(open + 11, close - open - 11);

                if (int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n < children.Length && children[n] != null && (children[n].Handle == null || inside.Add(children[n].Handle)))
                {
                    text.Append(WithCodes(children[n], inside, done, false));

                    if (children[n].Handle != null)
                    {
                        inside.Remove(children[n].Handle);
                    }
                }
                else
                {
                    text.Append(code, open, close + 2 - open);
                }

                at = close + 2;
            }

            string result = top && field.IsTextField ? text.ToString() : "%<" + text + ">%";

            if (!top && field.Handle != null)
            {
                done[field.Handle] = result;
            }

            return result;
        }

        /// <summary>
        /// Writes a field made in code into a dictionary of fields under a name, with its children, in the layout of
        /// <see cref="Made"/>: the field in the entry's place, owned by the dictionary, each child after it owned by its parent and
        /// named by it (360). The FIELD of the name, where there is one, is erased with its children.
        /// </summary>
        internal static void Write(DxfDBDictionary fields, string key, DxfField made)
        {
            DxfFile file = fields.Item.File;
            file.AddClass("FIELD", "AcDbField", 1152);
            EraseOld(fields, key);
            WriteTree(file, Made(made, file.Supports(1021)), groups => fields.Add(key, "FIELD", groups));
        }

        /// <summary>
        /// Writes a copy of a field of the file into a dictionary of fields under a name, with its children, as AutoCAD copies
        /// one: every group of its own part kept, its children copied after it and named by the copies' handles. The FIELD of
        /// the name, where there is one, is erased with its children first.
        /// </summary>
        internal static void WriteCopy(DxfDBDictionary fields, string key, DxfField source)
        {
            DxfFile file = fields.Item.File;
            // Read whole before the old field goes.
            Node node = Copied(source, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source.Handle });
            file.AddClass("FIELD", "AcDbField", 1152);
            EraseOld(fields, key);
            WriteTree(file, node, groups => fields.Add(key, "FIELD", groups));
        }

        /// <summary>
        /// Erases the fields a field owns, at every depth, from the deepest up, as AutoCAD erases a field's children with it:
        /// each a FIELD its groups 360 name whose owner (330) it is, each taken once.
        /// </summary>
        internal static void EraseChildren(DxfField field) => EraseChildren(field.Item.File, field.Item, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { field.Handle });

        private static void EraseChildren(DxfFile file, DxfItem item, HashSet<string> reached)
        {
            foreach (DxfTypedValue group in file.ObjectGroupsOf(item))
            {
                if (group.TypeCode != 360)
                {
                    continue;
                }

                DxfItem child = file.ObjectItem(group.Value);

                if (child != null && child.Type == "FIELD" && string.Equals(file.OwnerOf(child), item.Handle, StringComparison.OrdinalIgnoreCase) && reached.Add(child.Handle))
                {
                    EraseChildren(file, child, reached);
                    file.RemoveRecord(child);
                }
            }
        }

        /// <summary>Erases the children of the FIELD of a name of a dictionary of fields, where it has one, before the entry is given another.</summary>
        private static void EraseOld(DxfDBDictionary fields, string key)
        {
            if (fields.Contains(key) && fields.GetAt(key) is DxfField old && !old.IsErased)
            {
                EraseChildren(old);
            }
        }

        /// <summary>
        /// Writes a field and its children: the field by the way given, its groups after its marker AcDbField with a
        /// placeholder for each child's handle; then each child, owned by it; then the handles in the placeholders' places.
        /// </summary>
        private static DxfItem WriteTree(DxfFile file, Node node, Func<Action<DxfFile.Builder>, DxfItem> add)
        {
            DxfItem item = add(b =>
            {
                b.Group(100, "AcDbField");

                foreach (KeyValuePair<int, string> group in node.Groups)
                {
                    b.Group(group.Key, group.Value);
                }
            });

            node.Made?.Bind(item);
            var handles = new List<string>(node.Children.Count);

            foreach (Node child in node.Children)
            {
                handles.Add(WriteTree(file, child, groups => file.AddObject("FIELD", item.Handle, groups)).Handle);
            }

            IReadOnlyList<DxfTypedValue> written = file.GroupsOf(item);
            int k = 0;

            for (int i = 0; i < written.Count && k < handles.Count; i++)
            {
                if (written[i].TypeCode == 360)
                {
                    file.SetValueAt(item, i, handles[k++]);
                }
            }

            return item;
        }

        /// <summary>
        /// The groups of a field made in code, after its marker, in the order the DXF reference gives them, unchecked in AutoCAD
        /// as <see cref="DxfField"/> says: its evaluator
        /// (1), its code (2, then pieces of 250 characters in 3), its children (90, a 360 for each), no objects (97), evaluated
        /// automatically (91 of 63), filed in full (92), in no state yet (94), not evaluated (95 of 1, 96 of 0, 300 of nothing),
        /// no data (93), and its cache ACFD_FIELD_VALUE empty: from R2007 on a value of no type flagged empty, closed by
        /// ACVALUE_END, before it a type unknown (90 of 0); then the text it was shown as, none (301, 98).
        /// </summary>
        private static Node Made(DxfField field, bool r2007)
        {
            var node = new Node { Made = field };
            string code = field._madeCode;
            node.Groups.Add(G(1, field._madeEvaluator));
            node.Groups.AddRange(CodeGroups(code));
            node.Groups.Add(G(90, field._madeChildren.Count.ToString(CultureInfo.InvariantCulture)));

            foreach (DxfField child in field._madeChildren)
            {
                node.Groups.Add(G(360, "0"));
                node.Children.Add(Made(child, r2007));
            }

            node.Groups.Add(G(97, "0"));
            node.Groups.Add(G(91, "63"));
            node.Groups.Add(G(92, "0"));
            node.Groups.Add(G(94, "0"));
            node.Groups.Add(G(95, "1"));
            node.Groups.Add(G(96, "0"));
            node.Groups.Add(G(300, string.Empty));
            node.Groups.Add(G(93, "0"));
            node.Groups.Add(G(7, "ACFD_FIELD_VALUE"));

            if (r2007)
            {
                node.Groups.Add(G(93, "1"));
                node.Groups.Add(G(90, "0"));
                node.Groups.Add(G(94, "0"));
                node.Groups.Add(G(300, string.Empty));
                node.Groups.Add(G(302, string.Empty));
                node.Groups.Add(G(304, "ACVALUE_END"));
            }
            else
            {
                node.Groups.Add(G(90, "0"));
            }

            node.Groups.Add(G(301, string.Empty));
            node.Groups.Add(G(98, "0"));
            return node;
        }

        /// <summary>
        /// The groups of a field of the file to be copied, after its marker, as the file holds them, with its children's: a
        /// child's 360 a placeholder for the copy's handle; a 360 that names no FIELD, or a field copied already, as one a
        /// field names twice or one that stands inside itself, left out, so that each FIELD is copied once whatever names it;
        /// the count of children (90) made to agree and the placeholders of the code numbered anew, the children kept counting
        /// from nought in their order and the placeholder of one left out standing past them, for no child.
        /// </summary>
        private static Node Copied(DxfField source, HashSet<string> reached)
        {
            var node = new Node();
            DxfFile file = source.Item.File;
            var slots = new List<int>();
            int count = -1;
            int code = -1;

            foreach (DxfTypedValue group in source.Own())
            {
                if (group.TypeCode == 360)
                {
                    DxfItem child = file.ObjectItem(group.Value);

                    if (child == null || child.Type != "FIELD" || !reached.Add(child.Handle))
                    {
                        slots.Add(-1);
                        continue;
                    }

                    slots.Add(node.Children.Count);
                    node.Groups.Add(G(360, "0"));
                    node.Children.Add(Copied((DxfField)OfObject(child), reached));
                    continue;
                }

                if (group.TypeCode == 90 && count < 0)
                {
                    count = node.Groups.Count;
                }

                if (count < 0 && (group.TypeCode == 2 || group.TypeCode == 3))
                {
                    code = code < 0 ? node.Groups.Count : code;
                }

                node.Groups.Add(G(group.TypeCode, group.Value));
            }

            if (count >= 0)
            {
                node.Groups[count] = G(90, node.Children.Count.ToString(CultureInfo.InvariantCulture));
            }

            if (code >= 0 && slots.Contains(-1))
            {
                int end = count >= 0 ? count : node.Groups.Count;
                var pieces = new StringBuilder();
                var rest = new List<KeyValuePair<int, string>>();

                for (int i = code; i < end; i++)
                {
                    if (node.Groups[i].Key == 2 || node.Groups[i].Key == 3)
                    {
                        pieces.Append(node.Groups[i].Value);
                    }
                    else
                    {
                        rest.Add(node.Groups[i]);
                    }
                }

                var groups = new List<KeyValuePair<int, string>>(node.Groups.GetRange(0, code));
                groups.AddRange(CodeGroups(Renumbered(pieces.ToString(), slots, node.Children.Count)));
                groups.AddRange(rest);
                groups.AddRange(node.Groups.GetRange(end, node.Groups.Count - end));
                node.Groups.Clear();
                node.Groups.AddRange(groups);
            }

            return node;
        }

        /// <summary>
        /// A code with its placeholders numbered anew: that of a child kept its new index, that of one left out its old index
        /// past the children kept, so that it stands for none; one past every child as it was.
        /// </summary>
        private static string Renumbered(string code, List<int> slots, int kept)
        {
            var text = new StringBuilder(code.Length);
            int at = 0;

            while (at < code.Length)
            {
                int open = code.IndexOf(@"%<\_FldIdx ", at, StringComparison.Ordinal);
                int close = open < 0 ? -1 : code.IndexOf(">%", open, StringComparison.Ordinal);

                if (close < 0)
                {
                    text.Append(code, at, code.Length - at);
                    break;
                }

                text.Append(code, at, open - at);

                if (int.TryParse(code.Substring(open + 11, close - open - 11), NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n < slots.Count)
                {
                    text.Append(Placeholder(slots[n] >= 0 ? slots[n] : kept + n));
                }
                else
                {
                    text.Append(code, open, close + 2 - open);
                }

                at = close + 2;
            }

            return text.ToString();
        }

        /// <summary>The groups a code is written in: its first 250 characters in a 2, the rest in pieces of 250 in 3s.</summary>
        private static List<KeyValuePair<int, string>> CodeGroups(string code)
        {
            var groups = new List<KeyValuePair<int, string>> { G(2, code.Length > 250 ? code.Substring(0, 250) : code) };

            for (int from = 250; from < code.Length; from += 250)
            {
                groups.Add(G(3, code.Substring(from, Math.Min(250, code.Length - from))));
            }

            return groups;
        }

        /// <summary>The groups of the field's own part, after its marker AcDbField and up to its extended data, each text with its escapes read as letters.</summary>
        private List<DxfTypedValue> Own()
        {
            IReadOnlyList<DxfTypedValue> groups = Item.File.GroupsOf(Item);
            bool braced = false;
            int from = groups.Count;

            for (int i = 0; i < groups.Count; i++)
            {
                DxfTypedValue group = groups[i];

                if (group.TypeCode == 102)
                {
                    braced = group.Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && group.TypeCode == 100 && group.Value.Trim() == "AcDbField")
                {
                    from = i + 1;
                    break;
                }
            }

            return Item.File.TailOf(Item, from);
        }

        /// <summary>The text of the first group of a code; null where there is none.</summary>
        private static string Text(List<DxfTypedValue> groups, int code)
        {
            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>Where the marker <c>&gt;%</c> that closes the field opened at an index stands, fields inside it counted; -1 where none does.</summary>
        private static int Closing(string code, int open)
        {
            int depth = 0;
            int i = open;

            while (i + 1 < code.Length)
            {
                if (code[i] == '%' && code[i + 1] == '<')
                {
                    depth++;
                    i += 2;
                }
                else if (code[i] == '>' && code[i + 1] == '%')
                {
                    if (--depth == 0)
                    {
                        return i;
                    }

                    i += 2;
                }
                else
                {
                    i++;
                }
            }

            return -1;
        }

        /// <summary>The placeholder of a child in the code of its text field, <c>%&lt;\_FldIdx 0&gt;%</c>.</summary>
        private static string Placeholder(int index) => @"%<\_FldIdx " + index.ToString(CultureInfo.InvariantCulture) + ">%";

        private static KeyValuePair<int, string> G(int code, string value) => new KeyValuePair<int, string>(code, value);

        /// <summary>A field to be written: its groups after its marker, a 360 for each child in its children's order, and the field made in code it stands for, where it is one.</summary>
        private sealed class Node
        {
            internal List<KeyValuePair<int, string>> Groups { get; } = new List<KeyValuePair<int, string>>();

            internal List<Node> Children { get; } = new List<Node>();

            internal DxfField Made { get; set; }
        }
    }
}
