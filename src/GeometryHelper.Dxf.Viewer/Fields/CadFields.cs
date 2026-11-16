using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a field is evaluated for, as AutoCAD's <c>FieldEvaluationContext</c>: the bits of FIELDEVAL, and on demand.</summary>
    internal static class CadFieldContexts
    {
        internal const int Open = 1;
        internal const int Save = 2;
        internal const int Plot = 4;
        internal const int Etransmit = 8;
        internal const int Regen = 16;
        internal const int Demand = 32;
    }

    /// <summary>What a field is evaluated against: the drawing, the time and the culture of its names, and what a system variable holds.</summary>
    internal sealed class CadFieldContext
    {
        /// <summary>Makes a context.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="reason">What the fields are evaluated for, one of <see cref="CadFieldContexts"/>.</param>
        internal CadFieldContext(CadDocument document, int reason = CadFieldContexts.Demand)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Reason = reason;
        }

        internal CadDocument Document { get; }

        internal int Reason { get; }

        /// <summary>Gets or sets the time <c>Date</c> shows; now until set.</summary>
        internal DateTime Now { get; set; } = DateTime.Now;

        /// <summary>Gets or sets the culture of the names of days and months and of the short date; the current culture until set.</summary>
        internal CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

        /// <summary>Gets or sets the name of the user the field Login shows.</summary>
        internal string Login { get; set; } = Environment.UserName;

        /// <summary>Gets or sets what a system variable holds as text, for the command line's session; null to read the header only.</summary>
        internal Func<string, string> Variable { get; set; }
    }

    /// <summary>
    /// The fields of a text, as AutoCAD 2021 keeps and evaluates them (its layout of FIELD checked on drawings it saved): a TEXT
    /// or MTEXT shows the words its text field came to, kept in the FIELD objects of its ACAD_FIELD dictionary with the code of
    /// each field and the value it last had. This evaluates AcVar (the date and time, the properties of the drawing, a system
    /// variable), AcObjProp (a property of an object), AcDiesel and AcExpr, writes the words and the FIELD objects, and
    /// evaluates them again, as UPDATEFIELD and FIELDEVAL do. A field is written as <c>%&lt;\AcVar Date \f "dd/MM/yyyy"&gt;%</c>
    /// among the words, and an object it reads as <c>%&lt;\_ObjId 122&gt;%</c>, the object's handle (7A) in decimal.
    /// </summary>
    internal static class CadFields
    {
        private const string NoValue = "----";
        private const string Error = "####";

        /// <summary>Whether a drawing can hold fields: AutoCAD 2004 (AC1018) and later.</summary>
        internal static bool CanHold(DxfDatabase database) =>
            string.CompareOrdinal(database.Version ?? "AC1032", "AC1018") >= 0;

        /// <summary>FIELDEVAL of a drawing: the bits of what updates its fields; 31 where it keeps none.</summary>
        internal static int Eval(DxfDatabase database)
        {
            try
            {
                return int.TryParse(
                    database.Variable("FIELDEVAL"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value
                )
                    ? value
                    : 31;
            }
            catch (InvalidOperationException)
            {
                return 31;
            }
        }

        /// <summary>Sets FIELDEVAL in a drawing, as AutoCAD keeps it: a variable of the dictionary of variables; 31, its start, kept as it is.</summary>
        internal static void SetEval(DxfDatabase database, int value) =>
            database.SetVariable("FIELDEVAL", value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Whether an object is one whose words can hold a field: a TEXT, an attribute or an MTEXT.</summary>
        internal static bool IsText(DxfEntity entity) => entity is DxfDBText || entity is DxfMText;

        /// <summary>The words of a TEXT or an MTEXT as they are kept: what the field last came to.</summary>
        internal static string Words(DxfEntity entity) =>
            entity is DxfMText mtext ? mtext.Contents ?? string.Empty
            : entity is DxfDBText text ? text.TextString ?? string.Empty
            : string.Empty;

        private static void SetWords(DxfEntity entity, string words)
        {
            if (entity is DxfMText mtext)
            {
                mtext.Contents = words;
            }
            else if (entity is DxfDBText text)
            {
                text.TextString = words;
            }
        }

        /// <summary>
        /// Gives a TEXT or an MTEXT other words as the Properties panel does: the words are plain words, so that a field the text
        /// had is taken out.
        /// </summary>
        internal static void ReplaceWords(DxfEntity entity, string words)
        {
            if (Has(entity))
            {
                entity.RemoveField();
            }

            SetWords(entity, words);
        }

        /// <summary>Whether a TEXT or an MTEXT holds a field.</summary>
        internal static bool Has(DxfEntity entity)
        {
            try
            {
                return IsText(entity) && entity.GetField() != null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        // ---- Codes ----

        /// <summary>
        /// The words of a text with the code of each field in its place, as AutoCAD's <c>getTextWithFieldCodes</c>: an object a
        /// field reads as <c>%&lt;\_ObjId 122&gt;%</c>, the handle in decimal. The words as they are where there is no field.
        /// </summary>
        /// <param name="entity">The TEXT or MTEXT.</param>
        /// <returns>The words with the codes.</returns>
        internal static string CodesOf(DxfEntity entity)
        {
            DxfField field = Has(entity) ? entity.GetField() : null;
            return field == null ? Words(entity) : Codes(field.ToDraft(), true);
        }

        /// <summary>The code of a field draft: a text field as its words with its fields in their markers, any other field as its code in its markers.</summary>
        private static string Codes(DxfFieldDraft draft, bool top)
        {
            string code = draft.Code;
            var text = new StringBuilder();
            int at = 0;

            while (at < code.Length)
            {
                int open = code.IndexOf("%<\\_", at, StringComparison.Ordinal);
                int close = open < 0 ? -1 : code.IndexOf(">%", open, StringComparison.Ordinal);

                if (close < 0)
                {
                    text.Append(code, at, code.Length - at);
                    break;
                }

                text.Append(code, at, open - at);
                string inner = code.Substring(open + 4, close - open - 4);
                int number = inner.IndexOf(' ');
                string kind = number < 0 ? inner : inner.Substring(0, number);
                int index = -1;

                if (number >= 0)
                {
                    int.TryParse(
                        inner.Substring(number + 1),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out index
                    );
                }

                if (kind == "FldIdx" && index >= 0 && index < draft.Children.Count)
                {
                    text.Append(Codes(draft.Children[index], false));
                }
                else if (kind == "ObjIdx" && index >= 0 && index < draft.Objects.Count)
                {
                    text.Append("%<\\_ObjId ").Append(Decimal(draft.Objects[index])).Append(">%");
                }
                else
                {
                    text.Append(code, open, close + 2 - open);
                }

                at = close + 2;
            }

            return top && draft.Evaluator == "_text" ? text.ToString() : "%<" + text + ">%";
        }

        private static string Decimal(string handle) =>
            ulong.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : handle;

        private static string Hex(string number) =>
            ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)
                ? value.ToString("X", CultureInfo.InvariantCulture)
                : "0";

        /// <summary>
        /// The words each field of a text shows, in the order of the fields in its codes: for the editor to draw each field as
        /// the words it came to, without evaluating it.
        /// </summary>
        /// <param name="entity">The TEXT or MTEXT.</param>
        /// <returns>The words of each field; empty where it has none.</returns>
        internal static IReadOnlyList<string> ShownOf(DxfEntity entity)
        {
            var shown = new List<string>();
            DxfField field = Has(entity) ? entity.GetField() : null;

            if (field == null)
            {
                return shown;
            }

            string code = field.GetFieldCode();
            DxfField[] children = field.GetChildren();

            if (!field.IsTextField)
            {
                shown.Add(field.ShownText());
                return shown;
            }

            int at = 0;

            while (at < code.Length)
            {
                int open = code.IndexOf("%<\\_FldIdx ", at, StringComparison.Ordinal);
                int close = open < 0 ? -1 : code.IndexOf(">%", open, StringComparison.Ordinal);

                if (close < 0)
                {
                    break;
                }

                int index = -1;
                int.TryParse(
                    code.Substring(open + 11, close - open - 11),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out index
                );
                shown.Add(
                    index >= 0 && index < children.Length && children[index] != null
                        ? children[index].ShownText()
                        : string.Empty
                );
                at = close + 2;
            }

            return shown;
        }

        // ---- Making ----

        /// <summary>
        /// Reads words with fields in them into a text field to be evaluated: each <c>%&lt;\Evaluator ...&gt;%</c> a child, the
        /// code of the words with each as <c>%&lt;\_FldIdx n&gt;%</c>, an object a field names as <c>%&lt;\_ObjId 122&gt;%</c>
        /// an object of the field, <c>%&lt;\_ObjIdx n&gt;%</c> in its code. Null where the words hold no field.
        /// </summary>
        /// <param name="text">The words with their field codes.</param>
        /// <returns>The text field, not yet evaluated; null for none.</returns>
        internal static DxfFieldDraft Parse(string text)
        {
            var draft = new DxfFieldDraft { Evaluator = "_text" };
            var code = new StringBuilder();
            int at = 0;

            while (at < text.Length)
            {
                int open = text.IndexOf("%<", at, StringComparison.Ordinal);
                int close = open < 0 ? -1 : Closing(text, open);

                if (close < 0)
                {
                    code.Append(text, at, text.Length - at);
                    break;
                }

                code.Append(text, at, open - at);
                string inner = text.Substring(open + 2, close - open - 2);

                if (inner.Length < 2 || inner[0] != '\\' || !char.IsLetter(inner[1]))
                {
                    // Not a field: a marker of no evaluator stays among the words.
                    code.Append(text, open, close + 2 - open);
                }
                else
                {
                    DxfFieldDraft child = Child(inner);
                    code.Append("%<\\_FldIdx ")
                        .Append(draft.Children.Count.ToString(CultureInfo.InvariantCulture))
                        .Append(">%");
                    draft.Children.Add(child);
                }

                at = close + 2;
            }

            if (draft.Children.Count == 0)
            {
                return null;
            }

            draft.Code = code.ToString();
            return draft;
        }

        /// <summary>One field of the code of a text: its evaluator, its code with the objects it names turned to <c>%&lt;\_ObjIdx n&gt;%</c>, and the handles of those objects.</summary>
        private static DxfFieldDraft Child(string inner)
        {
            var child = new DxfFieldDraft();
            int end = 1;

            while (end < inner.Length && (char.IsLetterOrDigit(inner[end]) || inner[end] == '_'))
            {
                end++;
            }

            child.Evaluator = inner.Substring(1, end - 1);
            var code = new StringBuilder();
            int at = 0;

            while (at < inner.Length)
            {
                int open = inner.IndexOf("%<\\_ObjId ", at, StringComparison.Ordinal);
                int close = open < 0 ? -1 : inner.IndexOf(">%", open, StringComparison.Ordinal);

                if (close < 0)
                {
                    code.Append(inner, at, inner.Length - at);
                    break;
                }

                code.Append(inner, at, open - at);
                string number = inner.Substring(open + 10, close - open - 10).Trim();
                code.Append("%<\\_ObjIdx ")
                    .Append(child.Objects.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(">%");
                child.Objects.Add(Hex(number));
                at = close + 2;
            }

            child.Code = code.ToString();
            child.Format = DxfField.FormatOf(child.Code);
            return child;
        }

        /// <summary>Where the marker that closes the one opened at an index stands, markers inside it counted; -1 where none does.</summary>
        private static int Closing(string text, int open)
        {
            int depth = 0;
            int i = open;

            while (i + 1 < text.Length)
            {
                if (text[i] == '%' && text[i + 1] == '<')
                {
                    depth++;
                    i += 2;
                }
                else if (text[i] == '>' && text[i + 1] == '%')
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

        /// <summary>
        /// Gives a TEXT or MTEXT words with fields in them, as the Field dialog and the text editor give them: the fields
        /// evaluated, written with their FIELD objects, the words the fields came to written as its words; words with no field
        /// in them are written as they are, and a field the text had is taken out.
        /// </summary>
        /// <param name="entity">The TEXT or MTEXT.</param>
        /// <param name="codes">The words with the codes of their fields.</param>
        /// <param name="context">What the fields are evaluated against.</param>
        /// <returns>Whether the text holds a field now; false where its drawing cannot hold one, the words then written as plain words with the fields evaluated.</returns>
        internal static bool Set(DxfEntity entity, string codes, CadFieldContext context)
        {
            DxfFieldDraft draft = Parse(codes ?? string.Empty);

            if (draft == null)
            {
                if (Has(entity))
                {
                    entity.RemoveField();
                }

                SetWords(entity, codes ?? string.Empty);
                return false;
            }

            string words = Evaluate(draft, context, entity is DxfMText);

            if (!CanHold(context.Document.Database))
            {
                SetWords(entity, words);
                return false;
            }

            entity.SetField("TEXT", draft);
            SetWords(entity, words);
            return true;
        }

        /// <summary>
        /// Evaluates the fields of a TEXT or MTEXT again and writes what they come to, as UPDATEFIELD does: a field that cannot
        /// be evaluated shows ####. The FIELD objects are written anew.
        /// </summary>
        /// <param name="entity">The TEXT or MTEXT.</param>
        /// <param name="context">What the fields are evaluated against.</param>
        /// <returns>Whether the words changed.</returns>
        internal static bool Update(DxfEntity entity, CadFieldContext context) => Update(entity, context, out _, out _);

        /// <summary>
        /// Evaluates the fields of a TEXT or MTEXT again as <see cref="Update(DxfEntity, CadFieldContext)"/> does, counting them.
        /// </summary>
        /// <param name="entity">The TEXT or MTEXT.</param>
        /// <param name="context">What the fields are evaluated against.</param>
        /// <param name="found">How many fields the words hold.</param>
        /// <param name="updated">How many of them were evaluated.</param>
        /// <returns>Whether the words changed.</returns>
        internal static bool Update(DxfEntity entity, CadFieldContext context, out int found, out int updated)
        {
            DxfField field = Has(entity) ? entity.GetField() : null;
            found = 0;
            updated = 0;

            if (field == null)
            {
                return false;
            }

            DxfFieldDraft draft = field.ToDraft();
            string was = Words(entity);
            string words = Evaluate(draft, context, entity is DxfMText);
            entity.SetField("TEXT", draft);
            SetWords(entity, words);
            List<DxfFieldDraft> fields =
                draft.Evaluator == "_text" ? draft.Children : new List<DxfFieldDraft> { draft };
            found = fields.Count;

            foreach (DxfFieldDraft one in fields)
            {
                if (one.Status == 2)
                {
                    updated++;
                }
            }

            return !string.Equals(words, was, StringComparison.Ordinal);
        }

        /// <summary>
        /// Updates the fields of every text of a drawing for a reason (FIELDEVAL's open, save, plot or regen): those whose
        /// evaluation option holds it, which the drawing's FIELDEVAL allows, found by the drawing's list of fields. Nothing is
        /// done where the drawing has no list or cannot hold fields; a field that cannot be evaluated keeps what it showed.
        /// </summary>
        /// <param name="document">The drawing.</param>
        /// <param name="reason">One of <see cref="CadFieldContexts"/>.</param>
        /// <returns>How many texts changed.</returns>
        internal static int UpdateAll(CadDocument document, int reason)
        {
            DxfDatabase database = document.Database;

            if (!CanHold(database) || (Eval(database) & reason) == 0)
            {
                return 0;
            }

            var changed = 0;

            foreach (string handle in TextsWithFields(document))
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity == null || !Has(entity))
                {
                    continue;
                }

                DxfFieldDraft draft = entity.GetField().ToDraft();

                if (!Holds(draft, reason))
                {
                    continue;
                }

                try
                {
                    if (Update(entity, new CadFieldContext(document, reason)))
                    {
                        changed++;
                    }
                }
                catch (InvalidOperationException exception)
                {
                    GeometryHelperLog.Warn("CadFields: the fields of " + handle + " were not updated.", exception);
                }
            }

            // The formulas and fields of the cells of tables, worked out from the cells and the drawing.
            foreach (string handle in TablesWithFields(document))
            {
                if (document.FindEntity(handle) is DxfTable table)
                {
                    try
                    {
                        if (CadTableEditor.UpdateFields(document, table, reason) > 0)
                        {
                            CadTableEditor.Redraw(table, document);
                            changed++;
                        }
                    }
                    catch (Exception exception)
                        when (exception is InvalidOperationException || exception is ArgumentException)
                    {
                        GeometryHelperLog.Warn(
                            "CadFields: the fields of the table " + handle + " were not updated.",
                            exception
                        );
                    }
                }
            }

            return changed;
        }

        /// <summary>Whether any field of a draft is evaluated for a reason.</summary>
        private static bool Holds(DxfFieldDraft draft, int reason)
        {
            if (draft.Children.Count == 0)
            {
                return (draft.Option & reason) != 0;
            }

            foreach (DxfFieldDraft child in draft.Children)
            {
                if (Holds(child, reason))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The handles of the TEXTs and MTEXTs that hold a field, in the order of the drawing's list of fields.</summary>
        internal static List<string> TextsWithFields(CadDocument document)
        {
            var handles = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DxfFile file = document.Database.File;
            string root = file.KeptRootDictionaryHandle();
            DxfItem rootItem = root == null ? null : file.ObjectItem(root);

            if (
                rootItem == null
                || !(DxfDBObject.OfObject(rootItem) is DxfDBDictionary dictionary)
                || !dictionary.Contains("ACAD_FIELDLIST")
            )
            {
                return handles;
            }

            DxfDBObject list = dictionary.GetAt("ACAD_FIELDLIST");

            if (list?.Item == null || list.Item.Type != "FIELDLIST")
            {
                return handles;
            }

            foreach (DxfTypedValue group in file.ObjectGroupsOf(list.Item))
            {
                if (group.TypeCode != 330)
                {
                    continue;
                }

                DxfItem field = file.ObjectItem(group.Value);

                if (field == null || field.IsRemoved || field.Type != "FIELD")
                {
                    continue;
                }

                // A field of a text: owned by the dictionary ACAD_FIELD of its extension dictionary, owned by the text.
                DxfItem fields = OwnerItem(file, field);
                DxfItem extension = fields == null || fields.Type != "DICTIONARY" ? null : OwnerItem(file, fields);
                string owner = extension == null || extension.Type != "DICTIONARY" ? null : file.OwnerOf(extension);

                if (owner != null && seen.Add(owner))
                {
                    handles.Add(owner);
                }
            }

            return handles;
        }

        /// <summary>The handles of the tables that own a field (those of their cells' formulas and fields), in the order of the drawing's list of fields.</summary>
        internal static List<string> TablesWithFields(CadDocument document)
        {
            var handles = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DxfFile file = document.Database.File;
            string root = file.KeptRootDictionaryHandle();
            DxfItem rootItem = root == null ? null : file.ObjectItem(root);

            if (
                rootItem == null
                || !(DxfDBObject.OfObject(rootItem) is DxfDBDictionary dictionary)
                || !dictionary.Contains("ACAD_FIELDLIST")
            )
            {
                return handles;
            }

            DxfDBObject list = dictionary.GetAt("ACAD_FIELDLIST");

            if (list?.Item == null || list.Item.Type != "FIELDLIST")
            {
                return handles;
            }

            foreach (DxfTypedValue group in file.ObjectGroupsOf(list.Item))
            {
                DxfItem field = group.TypeCode == 330 ? file.ObjectItem(group.Value) : null;
                DxfItem owner =
                    field == null || field.IsRemoved || field.Type != "FIELD" ? null : OwnerItem(file, field);

                if (owner != null && owner.Type == "ACAD_TABLE" && owner.Handle != null && seen.Add(owner.Handle))
                {
                    handles.Add(owner.Handle);
                }
            }

            return handles;
        }

        private static DxfItem OwnerItem(DxfFile file, DxfItem item)
        {
            string owner = file.OwnerOf(item);
            return owner == null ? null : file.ObjectItem(owner);
        }

        // ---- Evaluation ----

        /// <summary>
        /// Evaluates a text field and the fields it holds: each child's value, state and status, the words the code comes to
        /// (each field's words in its place, escaped as an MTEXT's words are for an MTEXT) and the checksum of them kept by the text
        /// field. Written into the draft.
        /// </summary>
        /// <param name="draft">The text field, or one field.</param>
        /// <param name="context">What it is evaluated against.</param>
        /// <param name="mtext">Whether the words are an MTEXT's, whose backslashes and braces are escaped.</param>
        /// <returns>The words.</returns>
        internal static string Evaluate(DxfFieldDraft draft, CadFieldContext context, bool mtext)
        {
            foreach (DxfFieldDraft child in draft.Children)
            {
                EvaluateOne(child, context);
            }

            if (draft.Evaluator != "_text")
            {
                EvaluateOne(draft, context);
                return draft.Shown;
            }

            string code = draft.Code;
            var text = new StringBuilder();
            int at = 0;

            while (at < code.Length)
            {
                int open = code.IndexOf("%<\\_FldIdx ", at, StringComparison.Ordinal);
                int close = open < 0 ? -1 : code.IndexOf(">%", open, StringComparison.Ordinal);

                if (close < 0)
                {
                    text.Append(code, at, code.Length - at);
                    break;
                }

                text.Append(code, at, open - at);

                if (
                    int.TryParse(
                        code.Substring(open + 11, close - open - 11),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int index
                    )
                    && index < draft.Children.Count
                )
                {
                    string shown = draft.Children[index].Shown;
                    text.Append(mtext ? CadMTextContent.Escape(shown) : shown);
                }
                else
                {
                    text.Append(code, open, close + 2 - open);
                }

                at = close + 2;
            }

            string words = text.ToString();
            // AutoCAD writes the state of the text field of an MTEXT with its modified bit set (13), that of a TEXT without (9).
            draft.State = mtext ? 13 : 9;
            draft.Status = 2;
            draft.Error = 0;
            draft.Data.Clear();
            draft.Data.Add(
                new KeyValuePair<string, DxfFieldValue>(
                    DxfField.ChecksumKey,
                    DxfFieldValue.Checksum(DxfField.Checksum(words))
                )
            );
            draft.Value = new DxfFieldValue { Flags = 3 };
            draft.Shown = string.Empty;
            return words;
        }

        /// <summary>Evaluates one field: its words, its value, and its state, status and error.</summary>
        private static void EvaluateOne(DxfFieldDraft field, CadFieldContext context)
        {
            field.Status = 2;
            field.Error = 0;
            field.Message = string.Empty;
            field.Data.Clear();
            string format = DxfField.FormatOf(field.Code);
            field.Format = format;

            switch (field.Evaluator)
            {
                case "AcVar":
                    Variable(field, format, context);
                    break;
                case "AcObjProp":
                    ObjectProperty(field, format, context);
                    break;
                case "AcDiesel":
                    Diesel(field, format, context);
                    break;
                case "AcExpr":
                    Formula(field, format);
                    break;
                default:
                    Fail(field, format, 4, 0, 41);
                    break;
            }
        }

        /// <summary>A field that came to nothing: its status and error, the words ####.</summary>
        private static void Fail(DxfFieldDraft field, string format, int status, int error, int state)
        {
            field.Status = status;
            field.Error = error;
            field.State = state;
            field.Value = DxfFieldValue.None(format);
            field.Shown = Error;
        }

        /// <summary>A field with nothing to show, as the date of a drawing never saved: ----.</summary>
        private static void Nothing(DxfFieldDraft field, string format)
        {
            field.State = 43;
            field.Value = DxfFieldValue.None(format);
            field.Shown = NoValue;
        }

        /// <summary>A field that came to a value and the words it shows.</summary>
        private static void Done(DxfFieldDraft field, string format, DxfFieldValue value, string shown)
        {
            value.Flags = 4;
            value.Format = format;
            value.Shown = shown;
            field.State = 59;
            field.Value = value;
            field.Shown = shown;
        }

        private static void Text(DxfFieldDraft field, string format, string text)
        {
            CadFieldFormat parsed = CadFieldFormat.Parse(format);
            Done(field, format, new DxfFieldValue { Type = 4, Text = text }, parsed.Words(text));
        }

        // The name of an AcVar field: what follows the evaluator up to a blank or a switch.
        private static string NameOf(string code, string evaluator)
        {
            string rest = code.Substring(Math.Min(code.Length, evaluator.Length + 1)).TrimStart();
            int end = 0;

            while (end < rest.Length && !char.IsWhiteSpace(rest[end]) && rest[end] != '\\')
            {
                end++;
            }

            return rest.Substring(0, end);
        }

        private static void Variable(DxfFieldDraft field, string format, CadFieldContext context)
        {
            string name = NameOf(field.Code, "\\AcVar");
            field.Data.Add(new KeyValuePair<string, DxfFieldValue>("Variable", DxfFieldValue.Named(name)));
            CadDocument document = context.Document;
            DxfDatabase database = document.Database;
            DxfDatabaseSummaryInfo info = database.SummaryInfo;
            CadFieldFormat parsed = CadFieldFormat.Parse(format);

            switch (name.ToUpperInvariant())
            {
                case "DATE":
                    Done(
                        field,
                        format,
                        new DxfFieldValue { Type = 8, Date = context.Now },
                        CadFieldFormat.Date(context.Now, format, context.Culture)
                    );
                    return;
                case "CREATEDATE":
                    DateTime created = database.Tdcreate;

                    if (created == default(DateTime))
                    {
                        Nothing(field, format);
                    }
                    else
                    {
                        Done(
                            field,
                            format,
                            new DxfFieldValue { Type = 8, Date = created },
                            CadFieldFormat.Date(created, format, context.Culture)
                        );
                    }

                    return;
                case "SAVEDATE":
                    DateTime saved = database.Tdupdate;

                    if (saved == default(DateTime))
                    {
                        Nothing(field, format);
                    }
                    else
                    {
                        Done(
                            field,
                            format,
                            new DxfFieldValue { Type = 8, Date = saved },
                            CadFieldFormat.Date(saved, format, context.Culture)
                        );
                    }

                    return;
                case "PLOTDATE":
                case "PAPERSIZE":
                case "PLOTSTYLETABLE":
                case "PLOTDEVICENAME":
                case "PAGESETUPNAME":
                case "PLOTORIENTATION":
                case "PLOTSCALE":
                    Plot(field, name.ToUpperInvariant(), format, context);
                    return;
                case "FILENAME":
                    FileName(field, format, context, parsed);
                    return;
                case "FILESIZE":
                    string path = document.FileName;

                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        Nothing(field, format);
                        return;
                    }

                    long size = new FileInfo(path).Length;
                    Done(
                        field,
                        format,
                        new DxfFieldValue { Type = 1, Long = (int)Math.Min(size, int.MaxValue) },
                        parsed.IsNumeric ? parsed.Number(size, false) : size.ToString(CultureInfo.InvariantCulture)
                    );
                    return;
                case "AUTHOR":
                    Text(field, format, info.Author ?? string.Empty);
                    return;
                case "TITLE":
                    Text(field, format, info.Title ?? string.Empty);
                    return;
                case "SUBJECT":
                    Text(field, format, info.Subject ?? string.Empty);
                    return;
                case "KEYWORDS":
                    Text(field, format, info.Keywords ?? string.Empty);
                    return;
                case "COMMENTS":
                    Text(field, format, info.Comments ?? string.Empty);
                    return;
                case "HYPERLINKBASE":
                    Text(field, format, info.HyperlinkBase ?? string.Empty);
                    return;
                case "LASTSAVEDBY":
                    Text(field, format, info.LastSavedBy ?? string.Empty);
                    return;
                case "REVISIONNUMBER":
                    Text(field, format, info.RevisionNumber ?? string.Empty);
                    return;
                case "LOGIN":
                    Text(field, format, context.Login ?? string.Empty);
                    return;
            }

            string value = SystemVariable(name, context);

            if (value == null)
            {
                Fail(field, format, 64, 3, 43);
                return;
            }

            Text(field, format, value);
        }

        /// <summary>
        /// The value of a system variable as a field shows it: the session's where there is one (a real to six places), else the
        /// header's, the current tab and the drawing's name; null where there is none.
        /// </summary>
        internal static string SystemVariable(string name, CadFieldContext context)
        {
            string text = context.Variable?.Invoke(name);

            if (text != null)
            {
                return text;
            }

            switch (name.ToUpperInvariant())
            {
                case "CTAB":
                    return context.Document.CurrentSpace?.Name ?? "Model";
                case "DWGNAME":
                    return string.IsNullOrEmpty(context.Document.FileName)
                        ? "Drawing1.dwg"
                        : Path.GetFileName(context.Document.FileName);
                case "DWGPREFIX":
                    return string.IsNullOrEmpty(context.Document.FileName)
                        ? string.Empty
                        : Path.GetDirectoryName(context.Document.FileName) + Path.DirectorySeparatorChar;
            }

            IReadOnlyList<DxfTypedValue> header = context.Document.Database.File.HeaderOf(
                "$" + name.ToUpperInvariant()
            );

            if (header == null || header.Count == 0)
            {
                return null;
            }

            if (header.Count >= 2 && header[0].TypeCode == 10)
            {
                var parts = new List<string>();

                foreach (DxfTypedValue group in header)
                {
                    parts.Add(Real(group.Value));
                }

                return string.Join(", ", parts);
            }

            DxfTypedValue first = header[0];
            return first.TypeCode >= 38 && first.TypeCode <= 59 ? Real(first.Value) : first.Value.Trim();
        }

        private static string Real(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? number.ToString("F6", CultureInfo.InvariantCulture)
                : value;

        /// <summary>The name of the file: its drive, path, name and extension as the format's %fn says, empty parts left out; ---- where none is left.</summary>
        private static void FileName(DxfFieldDraft field, string format, CadFieldContext context, CadFieldFormat parsed)
        {
            string path = context.Document.FileName;
            string drive = string.Empty;
            string directory = string.Empty;
            string name = "Drawing1";
            string extension = "dwg";

            if (!string.IsNullOrEmpty(path))
            {
                string root = Path.GetPathRoot(path) ?? string.Empty;
                string folder = Path.GetDirectoryName(path) ?? string.Empty;
                drive =
                    root.EndsWith("\\", StringComparison.Ordinal) && root.Length == 3
                        ? root.Substring(0, 2)
                        : root.TrimEnd('\\');
                string below = folder.Length > root.Length ? folder.Substring(root.Length).TrimEnd('\\') : string.Empty;
                directory = (root.Length > 0 ? "\\" : string.Empty) + below + (below.Length > 0 ? "\\" : string.Empty);
                name = Path.GetFileNameWithoutExtension(path);
                extension = Path.GetExtension(path).TrimStart('.');
            }

            int parts = parsed.FileParts;
            var text = new StringBuilder();

            if ((parts & 8) != 0)
            {
                text.Append(drive);
            }

            if ((parts & 1) != 0)
            {
                text.Append(directory);
            }

            if ((parts & 2) != 0)
            {
                text.Append(name);
            }

            if ((parts & 4) != 0 && extension.Length > 0)
            {
                text.Append((parts & 2) != 0 ? "." : string.Empty).Append(extension);
            }

            if (text.Length == 0)
            {
                Nothing(field, format);
                return;
            }

            Done(field, format, new DxfFieldValue { Type = 4, Text = text.ToString() }, parsed.Words(text.ToString()));
        }

        /// <summary>The variables of the page setup of the layout worked in; ---- where it has none.</summary>
        private static void Plot(DxfFieldDraft field, string name, string format, CadFieldContext context)
        {
            DxfLayout layout = context.Document.CurrentSpace?.Layout;
            string value = null;

            if (name != "PLOTDATE" && layout != null)
            {
                switch (name)
                {
                    case "PAGESETUPNAME":
                        value = layout.PlotSettingsName;
                        break;
                    case "PLOTDEVICENAME":
                        value = string.IsNullOrEmpty(layout.PlotConfigurationName)
                            ? "None"
                            : layout.PlotConfigurationName;
                        break;
                    case "PLOTSTYLETABLE":
                        value = layout.CurrentStyleSheet;
                        break;
                    case "PAPERSIZE":
                        value = layout.CanonicalMediaName;
                        break;
                    case "PLOTORIENTATION":
                        GeoPoint2 paper = layout.PlotPaperSize;
                        bool turned =
                            layout.PlotRotation == DxfPlotRotation.Degrees090
                            || layout.PlotRotation == DxfPlotRotation.Degrees270;
                        value = (paper.X > paper.Y) ^ turned ? "Landscape" : "Portrait";
                        break;
                    default:
                        value = "1.000000";
                        break;
                }
            }

            if (string.IsNullOrEmpty(value))
            {
                Nothing(field, format);
                return;
            }

            Text(field, format, value);
        }

        private static void ObjectProperty(DxfFieldDraft field, string format, CadFieldContext context)
        {
            // Object(%<\_ObjIdx 0>%).Name
            string code = field.Code;
            int close = code.IndexOf(").", StringComparison.Ordinal);
            int open = code.IndexOf("%<\\_ObjIdx ", StringComparison.Ordinal);
            int mark = open < 0 ? -1 : code.IndexOf(">%", open, StringComparison.Ordinal);
            int index = -1;

            if (open >= 0 && mark > open)
            {
                int.TryParse(
                    code.Substring(open + 11, mark - open - 11),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out index
                );
            }

            if (close < 0 || index < 0 || index >= field.Objects.Count)
            {
                Fail(field, format, 8, 0, 43);
                return;
            }

            string rest = code.Substring(close + 2);
            int end = 0;

            while (end < rest.Length && !char.IsWhiteSpace(rest[end]) && rest[end] != '\\')
            {
                end++;
            }

            string property = rest.Substring(0, end);
            string handle = field.Objects[index];
            field.Data.Add(new KeyValuePair<string, DxfFieldValue>("ObjectPropertyId", DxfFieldValue.Object(handle)));
            field.Data.Add(
                new KeyValuePair<string, DxfFieldValue>("ObjectPropertyName", DxfFieldValue.Named(property))
            );
            DxfEntity entity = context.Document.FindEntity(handle);
            CadFieldProperty found = entity == null ? null : CadFieldProperties.Find(entity, property);

            if (found == null)
            {
                Fail(field, format, 64, 3, 43);
                return;
            }

            object value;

            try
            {
                value = found.Read(entity);
            }
            catch (Exception exception)
                when (exception is InvalidOperationException
                    || exception is ArgumentException
                    || exception is NullReferenceException
                )
            {
                Fail(field, format, 64, 3, 43);
                return;
            }

            CadFieldFormat parsed = CadFieldFormat.Parse(format);

            switch (found.Kind)
            {
                case CadFieldKind.Text:
                    Done(
                        field,
                        format,
                        new DxfFieldValue { Type = 4, Text = (string)value },
                        parsed.Words((string)value)
                    );
                    break;
                case CadFieldKind.Whole:
                    int whole = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    Done(
                        field,
                        format,
                        new DxfFieldValue { Type = 1, Long = whole },
                        parsed.IsNumeric ? parsed.Number(whole, false) : whole.ToString(CultureInfo.InvariantCulture)
                    );
                    break;
                case CadFieldKind.Point:
                    var point = (GeoPoint3)value;
                    string shown = parsed.IsNumeric
                        ? parsed.Number(point.X, false)
                            + ", "
                            + parsed.Number(point.Y, false)
                            + ", "
                            + parsed.Number(point.Z, false)
                        : Six(point.X) + ", " + Six(point.Y) + ", " + Six(point.Z);
                    Done(
                        field,
                        format,
                        new DxfFieldValue
                        {
                            Type = 32,
                            X = point.X,
                            Y = point.Y,
                            Z = point.Z,
                        },
                        shown
                    );
                    break;
                default:
                    double real = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    Done(
                        field,
                        format,
                        new DxfFieldValue { Type = 2, Double = real },
                        parsed.IsNumeric ? parsed.Number(real, found.Kind == CadFieldKind.Angle) : Six(real)
                    );
                    break;
            }
        }

        private static string Six(double value) => value.ToString("F6", CultureInfo.InvariantCulture);

        private static void Diesel(DxfFieldDraft field, string format, CadFieldContext context)
        {
            string code = field.Code;
            int switchAt = code.IndexOf(" \\f \"", StringComparison.Ordinal);
            string expression = code.Substring(
                Math.Min(code.Length, "\\AcDiesel ".Length),
                Math.Max(0, (switchAt < 0 ? code.Length : switchAt) - "\\AcDiesel ".Length)
            );
            field.Data.Add(
                new KeyValuePair<string, DxfFieldValue>("DieselExpression", DxfFieldValue.Named(expression))
            );
            var diesel = new CadDiesel(
                name => SystemVariable(name, context) ?? string.Empty,
                context.Now,
                context.Culture
            );
            Text(field, format, diesel.Evaluate(expression));
        }

        private static void Formula(DxfFieldDraft field, string format)
        {
            string code = field.Code;
            int switchAt = code.IndexOf(" \\f \"", StringComparison.Ordinal);
            string expression = code.Substring(
                Math.Min(code.Length, "\\AcExpr ".Length),
                Math.Max(0, (switchAt < 0 ? code.Length : switchAt) - "\\AcExpr ".Length)
            );

            if (!CadFieldFormula.TryEvaluate(expression, out double value, out bool whole))
            {
                Fail(field, format, 8, 0, 43);
                return;
            }

            CadFieldFormat parsed = CadFieldFormat.Parse(format);

            if (whole)
            {
                Done(
                    field,
                    format,
                    new DxfFieldValue { Type = 1, Long = (int)Math.Round(value) },
                    parsed.IsNumeric
                        ? parsed.Number(value, false)
                        : Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
                );
                return;
            }

            Done(
                field,
                format,
                new DxfFieldValue { Type = 2, Double = value },
                parsed.IsNumeric ? parsed.Number(value, false) : Six(value)
            );
        }
    }
}
