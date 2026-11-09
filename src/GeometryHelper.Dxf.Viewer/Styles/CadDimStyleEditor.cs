using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The edits of the dimension style table, as AutoCAD's Dimension Style Manager (DIMSTYLE) makes them: each one undo step
    /// through <see cref="CadDocument.ApplyEdit"/>, refused where AutoCAD refuses it. A style's variables travel as values by
    /// their names (<c>Dimasz</c>, <c>Dimtxt</c>), the names of the library's <see cref="DxfDimStyleTableRecord"/>; an
    /// arrowhead as its name in <see cref="CadArrowheads.Names"/> (Closed filled for none), a text style or a linetype as its
    /// name, null for none.
    /// </summary>
    /// <remarks>
    /// A style changed draws again the dimensions drawn in it, each from its points as the DIM commands make it, but those an
    /// owner keeps (a model drawing's) and those whose kind the commands do not make (ordinate, arc length, jogged), which keep
    /// their pictures until AutoCAD next draws them.
    /// </remarks>
    public static class CadDimStyleEditor
    {
        private static readonly Lazy<IReadOnlyList<PropertyInfo>> StyleProperties = new Lazy<IReadOnlyList<PropertyInfo>>(() => PropertiesOf(typeof(DxfDimStyleTableRecord)));

        private static readonly Lazy<IReadOnlyList<PropertyInfo>> DimensionProperties = new Lazy<IReadOnlyList<PropertyInfo>>(
            () => PropertiesOf(typeof(DxfDimension)).Where(p => StyleProperties.Value.Any(s => s.Name == p.Name)).ToList());

        /// <summary>Gets the names of the variables a style has, as the library names them, in ordinal order: Dimadec to Dimzin.</summary>
        public static IReadOnlyList<string> VariableNames => StyleProperties.Value.Select(p => p.Name).ToList();

        /// <summary>Gets the name of the current dimension style (<c>$DIMSTYLE</c>), as the table spells it; the first style where the header names none the drawing has.</summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The name; null where the drawing has no dimension style.</returns>
        public static string Current(DxfDatabase database)
        {
            string name;

            try
            {
                name = database.Dimstyle;
            }
            catch (Exception)
            {
                name = null;
            }

            if (!string.IsNullOrEmpty(name) && database.DimStyleTable.Has(name))
            {
                return database.DimStyleTable[name].Name;
            }

            return database.DimStyleTable.Has("Standard") ? database.DimStyleTable["Standard"].Name : database.DimStyleTable.Select(s => s.Name).FirstOrDefault();
        }

        /// <summary>
        /// Gets the styles the Manager lists: those of the table but the ones AutoCAD keeps for one kind of dimension of
        /// another (<c>ISO-25$0</c>), in the order of the table.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The styles.</returns>
        public static IReadOnlyList<DxfDimStyleTableRecord> Styles(DxfDatabase database)
            => database.DimStyleTable.Where(s => !string.IsNullOrEmpty(s.Name) && s.Name.IndexOf('$') < 0).ToList();

        /// <summary>Gets whether a style is one a model drawing makes (its name starting GH_MODEL), which an update makes again from the drawing settings.</summary>
        /// <param name="name">The style's name.</param>
        /// <returns>True for GH_MODEL_10 and its like.</returns>
        public static bool IsModelStyle(string name) => name != null && name.StartsWith("GH_MODEL", StringComparison.OrdinalIgnoreCase);

        /// <summary>Reads the variables of a style, or of the dimension style data a dimension or the header gives, by their names.</summary>
        /// <param name="style">The style.</param>
        /// <returns>The values, in ordinal order of the names; a variable that cannot be read left out.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        public static SortedDictionary<string, object> Read(DxfDimStyleTableRecord style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            var values = new SortedDictionary<string, object>(StringComparer.Ordinal);

            foreach (PropertyInfo property in StyleProperties.Value)
            {
                try
                {
                    object value = Named(property.GetValue(style));
                    values[property.Name] = value == null && property.PropertyType == typeof(DxfBlockTableRecord) ? CadArrowheads.ClosedFilled : value;
                }
                catch (TargetInvocationException)
                {
                    // A variable the file's version has not: left out.
                }
            }

            return values;
        }

        /// <summary>
        /// Gets the variables in which two sets of values differ, as the Manager's Compare lists them: each name with the two
        /// values as text, in ordinal order of the names.
        /// </summary>
        /// <param name="first">The values of one style.</param>
        /// <param name="second">The values of the other.</param>
        /// <returns>The differences: the name, the first's value, the second's.</returns>
        public static IReadOnlyList<KeyValuePair<string, string[]>> Compare(IDictionary<string, object> first, IDictionary<string, object> second)
        {
            var differences = new List<KeyValuePair<string, string[]>>();

            foreach (string name in first.Keys.Union(second.Keys).OrderBy(n => n, StringComparer.Ordinal))
            {
                first.TryGetValue(name, out object a);
                second.TryGetValue(name, out object b);
                string ta = Text(a);
                string tb = Text(b);

                if (!string.Equals(ta, tb, StringComparison.Ordinal))
                {
                    differences.Add(new KeyValuePair<string, string[]>(name, new[] { ta, tb }));
                }
            }

            return differences;
        }

        /// <summary>Gives a value of a variable as the Manager shows it: a number invariant to 15 figures, a colour as AutoCAD names it, none as an empty text.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The text.</returns>
        public static string Text(object value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case double number:
                    return number.ToString("G15", CultureInfo.InvariantCulture);
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        /// <summary>
        /// Makes a style current (<c>$DIMSTYLE</c>), as Set Current and the style box do, its variables given to the header as
        /// AutoCAD gives them, so that the header's dimension variables are the style's.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetCurrent(CadDocument document, string name)
        {
            DxfDimStyleTableRecord style = Find(document, name, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            string set = style.Name;
            string old = Current(document.Database);
            return document.ApplyEdit("DIMSTYLE", new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$DIMSTYLE", old, set) }, s =>
            {
                s.Database.Dimstyle = set;
                s.Database.SetDimstyleData(s.Database.DimStyleTable[set]);
            });
        }

        /// <summary>Adds a style, as the Manager's New: a copy of another's variables (Start With), under a name no style of the drawing has.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The new name, as "Copy of ISO-25".</param>
        /// <param name="from">The style it starts with.</param>
        /// <param name="values">The variables it is given over those it starts with, as the New dialog's Continue sets them; null for none.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Add(CadDocument document, string name, string from, IDictionary<string, object> values)
        {
            DxfDimStyleTableRecord source = Find(document, from, out CadEditResult refused);

            if (source == null)
            {
                return refused;
            }

            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0 || trimmed.IndexOf('$') >= 0)
            {
                return CadEditResult.Refuse("A dimension style needs a name, with no $ in it.");
            }

            if (document.Database.DimStyleTable.Has(trimmed))
            {
                return CadEditResult.Refuse("Dimension style \"" + trimmed + "\" already exists.");
            }

            SortedDictionary<string, object> start = Read(source);

            foreach (KeyValuePair<string, object> value in values ?? new Dictionary<string, object>())
            {
                start[value.Key] = value.Value;
            }

            return document.ApplyEdit("DIMSTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New dimension style", null, trimmed) }, s =>
            {
                s.Database.DimStyleTable.Add(new DxfDimStyleTableRecord { Name = trimmed });
                Write(s.Database, s.Database.DimStyleTable[trimmed], start);
            });
        }

        /// <summary>
        /// Writes variables into a style, as OK of the Manager's Modify: each that differs, then the dimensions drawn in it drawn
        /// again (see the remarks), and the header given them where the style is current. Nothing is written where none differs.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <param name="values">The variables by name.</param>
        /// <returns>What came of it; failed, nothing written, where a value is one AutoCAD does not take.</returns>
        public static CadEditResult Apply(CadDocument document, string name, IDictionary<string, object> values)
        {
            DxfDimStyleTableRecord style = Find(document, name, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            SortedDictionary<string, object> now = Read(style);

            if (Compare(now, values.Where(v => now.ContainsKey(v.Key)).ToDictionary(v => v.Key, v => v.Value)).Count == 0)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            string set = style.Name;
            bool current = string.Equals(set, Current(document.Database), StringComparison.OrdinalIgnoreCase);
            return document.ApplyEdit("DIMSTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Dimension style " + set, null, null) }, s =>
            {
                DxfDimStyleTableRecord target = s.Database.DimStyleTable[set];
                Write(s.Database, target, values);

                if (current)
                {
                    s.Database.SetDimstyleData(target);
                }

                foreach (DxfDimension dimension in Dimensions(s.Document, d => IsDrawnIn(d, set)))
                {
                    CadCommands.Redraw(dimension);
                }
            });
        }

        /// <summary>
        /// Writes variables as overrides of the dimensions given, as AutoCAD's DIMOVERRIDE writes them into a dimension's
        /// extended data (ACAD, DSTYLE), each dimension then drawn again; the style is left as it is. A dimension an owner keeps is
        /// refused through the document's edit gate.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The dimensions.</param>
        /// <param name="values">The variables that differ from their style, by name.</param>
        /// <returns>What came of it; refused where no dimension is given.</returns>
        public static CadEditResult Override(CadDocument document, IReadOnlyCollection<string> handles, IDictionary<string, object> values)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            List<string> dimensions = (handles ?? Array.Empty<string>()).Where(h => document.FindEntity(h) is DxfDimension).OrderBy(h => h, StringComparer.Ordinal).ToList();

            if (dimensions.Count == 0)
            {
                return CadEditResult.Refuse("Select the dimensions to override first: an override is written into each dimension selected.");
            }

            var planned = dimensions.Select(h => new CadEditRecord(CadEditRecordKind.PropertyChanged, h, "Dimension style overrides", null, null)).ToArray();
            return document.ApplyEdit("DIMOVERRIDE", planned, s =>
            {
                foreach (string handle in dimensions)
                {
                    if (s.IsHandled(handle))
                    {
                        continue;
                    }

                    var dimension = (DxfDimension)s.Document.FindEntity(handle);
                    SetAll(s.Database, DimensionProperties.Value, dimension, values);
                    CadCommands.Redraw(dimension);
                }
            });
        }

        /// <summary>Deletes a style, as the Manager's Delete: refused for Standard, the current style and a style dimensions are drawn in, saying how many.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Delete(CadDocument document, string name)
        {
            DxfDimStyleTableRecord style = Find(document, name, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            string set = style.Name;

            if (string.Equals(set, "Standard", StringComparison.OrdinalIgnoreCase) || string.Equals(set, Current(document.Database), StringComparison.OrdinalIgnoreCase))
            {
                return CadEditResult.Refuse("Standard and the current style cannot be deleted.");
            }

            int uses = CountUses(document, set);

            if (uses > 0)
            {
                return CadEditResult.Refuse("Style \"" + set + "\" is in use by " + uses + (uses == 1 ? " dimension" : " dimensions") + ", and cannot be deleted.");
            }

            return document.ApplyEdit("DIMSTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete dimension style", set, null) }, s => s.Database.DimStyleTable[set].Erase());
        }

        /// <summary>Renames a style with the dimensions drawn in it, as the Manager's Rename; Standard keeps its name.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Rename(CadDocument document, string name, string newName)
        {
            DxfDimStyleTableRecord style = Find(document, name, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            string set = style.Name;
            string trimmed = (newName ?? string.Empty).Trim();

            if (string.Equals(set, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                return CadEditResult.Refuse("The Standard style cannot be renamed.");
            }

            if (trimmed.Length == 0 || trimmed.IndexOf('$') >= 0)
            {
                return CadEditResult.Refuse("A dimension style needs a name, with no $ in it.");
            }

            if (!string.Equals(trimmed, set, StringComparison.OrdinalIgnoreCase) && document.Database.DimStyleTable.Has(trimmed))
            {
                return CadEditResult.Refuse("Dimension style \"" + trimmed + "\" already exists.");
            }

            return document.ApplyEdit("DIMSTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Rename dimension style", set, trimmed) }, s => s.Database.DimStyleTable[set].Name = trimmed);
        }

        /// <summary>Gets how many dimensions are drawn in a style, or in a style AutoCAD keeps for one kind of them (ISO-25$0), in every space and block.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>The number.</returns>
        public static int CountUses(CadDocument document, string name) => Dimensions(document, d => IsDrawnIn(d, name)).Count;

        /// <summary>
        /// Makes the sample dimensions of the Manager's preview: a drawing of its own holding the style's variables (the text
        /// style copied by its name and font, the arrowheads' blocks made), a part 32 by 20 with a linear dimension across its
        /// top, a circle of radius 12 with its radius and an angle of 60 degrees, drawn as the DIM commands draw them.
        /// </summary>
        /// <param name="source">The drawing the style's text styles are taken from.</param>
        /// <param name="values">The style's variables by name.</param>
        /// <returns>The drawing of the samples.</returns>
        public static DxfDatabase Samples(DxfDatabase source, IDictionary<string, object> values)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var database = new DxfDatabase("AC1027");

            if (values.TryGetValue("Dimtxsty", out object textStyle) && textStyle is string styleName && source.TextStyleTable.Has(styleName) && !database.TextStyleTable.Has(styleName))
            {
                DxfTextStyleTableRecord from = source.TextStyleTable[styleName];
                database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = from.Name, FileName = from.FileName, BigFontFileName = from.BigFontFileName, XScale = from.XScale, ObliquingAngle = from.ObliquingAngle });
                database.TextStyleTable[from.Name].Font = from.Font;
            }

            var copy = new DxfDimStyleTableRecord { Name = "Preview" };
            database.DimStyleTable.Add(copy);
            Write(database, database.DimStyleTable["Preview"], values.Where(v => !v.Key.StartsWith("Dimltype", StringComparison.Ordinal) && !v.Key.StartsWith("Dimltex", StringComparison.Ordinal)).ToDictionary(v => v.Key, v => v.Value));
            CadCommands.DrawSamples(database, database.DimStyleTable["Preview"]);
            return database;
        }

        /// <summary>Writes variables into a style of a drawing, each that differs, a name of an arrowhead, text style or linetype taken to its record there.</summary>
        internal static void Write(DxfDatabase database, DxfDimStyleTableRecord style, IDictionary<string, object> values)
            => SetAll(database, StyleProperties.Value, style, values);

        private static void SetAll(DxfDatabase database, IReadOnlyList<PropertyInfo> properties, object target, IDictionary<string, object> values)
        {
            foreach (PropertyInfo property in properties)
            {
                if (!values.TryGetValue(property.Name, out object value))
                {
                    continue;
                }

                object now;

                try
                {
                    now = Named(property.GetValue(target));
                    now = now == null && property.PropertyType == typeof(DxfBlockTableRecord) ? CadArrowheads.ClosedFilled : now;
                }
                catch (TargetInvocationException)
                {
                    continue;
                }

                if (string.Equals(Text(now), Text(value), StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    property.SetValue(target, Record(database, property.PropertyType, value));
                }
                catch (TargetInvocationException exception)
                {
                    throw exception.InnerException ?? exception;
                }
            }
        }

        /// <summary>The record a name of a reference stands for in a drawing: an arrowhead's block made where it is AutoCAD's, a text style or a linetype found; the value itself for one not a reference.</summary>
        private static object Record(DxfDatabase database, Type type, object value)
        {
            if (type == typeof(DxfBlockTableRecord))
            {
                return value is string arrow ? CadArrowheads.Ensure(database, arrow) : null;
            }

            if (type == typeof(DxfTextStyleTableRecord))
            {
                return value is string style && database.TextStyleTable.Has(style) ? database.TextStyleTable[style] : null;
            }

            if (type == typeof(DxfLinetypeTableRecord))
            {
                return value is string linetype && database.LinetypeTable.Has(linetype) ? database.LinetypeTable[linetype] : null;
            }

            return value;
        }

        /// <summary>A value as the Manager keeps it: a record by its name, an arrowhead's block by the arrowhead's name.</summary>
        private static object Named(object value)
        {
            switch (value)
            {
                case DxfBlockTableRecord block:
                    return CadArrowheads.NameOf(block);
                case DxfSymbolTableRecord record:
                    return record.Name;
                default:
                    return value;
            }
        }

        /// <summary>Whether a dimension is drawn in a style, or in one AutoCAD keeps for a kind of dimension of it (ISO-25$0 of ISO-25).</summary>
        private static bool IsDrawnIn(DxfDimension dimension, string style)
        {
            string name = dimension.DimensionStyleName ?? string.Empty;
            return string.Equals(name, style, StringComparison.OrdinalIgnoreCase)
                || (name.Length > style.Length + 1 && name.StartsWith(style, StringComparison.OrdinalIgnoreCase) && name[style.Length] == '$');
        }

        /// <summary>The dimensions of every space and block a test takes, those an owner keeps left out, in the order of the blocks.</summary>
        private static List<DxfDimension> Dimensions(CadDocument document, Func<DxfDimension, bool> test)
        {
            var found = new List<DxfDimension>();

            foreach (DxfBlockTableRecord block in document.Database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfDimension dimension && test(dimension) && document.Owners.GetEditRule(dimension) == CadEditRule.Free)
                    {
                        found.Add(dimension);
                    }
                }
            }

            return found;
        }

        private static IReadOnlyList<PropertyInfo> PropertiesOf(Type type)
            => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name.StartsWith("Dim", StringComparison.Ordinal) && !p.Name.StartsWith("Dimension", StringComparison.Ordinal) && p.Name != "DimBlock" && p.Name != "DimBlockPosition"
                    && p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

        private static DxfDimStyleTableRecord Find(CadDocument document, string name, out CadEditResult refused)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            refused = null;

            if (string.IsNullOrEmpty(name) || !document.Database.DimStyleTable.Has(name))
            {
                refused = CadEditResult.Refuse("Dimension style \"" + name + "\" not found.");
                return null;
            }

            return document.Database.DimStyleTable[name];
        }
    }
}
