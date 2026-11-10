using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What the Modify Table Style dialog sets of one cell style of a table style, the title's, the header's or the data's:
    /// its fill, alignment, text style, height and colour, and the lineweight, colour and showing of its six grid lines, top,
    /// inside and bottom across and left, inside and right down.
    /// </summary>
    /// <remarks>The data of AutoCAD's Standard in millimetres is 4.5 high in Standard, top centre, by block, unfilled, every line drawn by block.</remarks>
    public sealed class CadTableCellStyleSettings
    {
        /// <summary>Gets or sets the name of the text style.</summary>
        public string TextStyle { get; set; } = "Standard";

        /// <summary>Gets or sets the height of the text.</summary>
        public double TextHeight { get; set; } = 4.5;

        /// <summary>Gets or sets the colour of the text: of the index, by layer or by block.</summary>
        public DxfColor TextColor { get; set; } = DxfColor.ByBlock;

        /// <summary>Gets or sets the colour the cells are filled with; null for none.</summary>
        public DxfColor FillColor { get; set; }

        /// <summary>Gets or sets where the content of a cell stands.</summary>
        public DxfCellAlignment Alignment { get; set; } = DxfCellAlignment.TopCenter;

        /// <summary>Gets the lineweight of each of the six lines, in the order of <see cref="DxfGridLineType"/>'s flags.</summary>
        public DxfLineWeight[] LineWeights { get; } = Enumerable.Repeat(DxfLineWeight.ByBlock, 6).ToArray();

        /// <summary>Gets the colour of each of the six lines.</summary>
        public DxfColor[] LineColors { get; } = Enumerable.Repeat(DxfColor.ByBlock, 6).ToArray();

        /// <summary>Gets whether each of the six lines is drawn.</summary>
        public bool[] LinesShown { get; } = Enumerable.Repeat(true, 6).ToArray();

        /// <summary>Gets whether two settings write the same cell style.</summary>
        /// <param name="other">The other.</param>
        /// <returns>True where every value is the same, the text style's letters of either case the same.</returns>
        public bool SameAs(CadTableCellStyleSettings other)
        {
            if (other == null || !string.Equals(TextStyle, other.TextStyle, StringComparison.OrdinalIgnoreCase) || !TextHeight.Equals(other.TextHeight)
                || !Same(TextColor, other.TextColor) || !Same(FillColor, other.FillColor) || Alignment != other.Alignment)
            {
                return false;
            }

            for (int line = 0; line < 6; line++)
            {
                if (LineWeights[line] != other.LineWeights[line] || !Same(LineColors[line], other.LineColors[line]) || LinesShown[line] != other.LinesShown[line])
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool Same(DxfColor a, DxfColor b) => a == null ? b == null : b != null && a.ColorMethod == b.ColorMethod && a.ColorIndex == b.ColorIndex;
    }

    /// <summary>
    /// What the Modify Table Style dialog sets of a table style: its description, which way its rows run, the margins of its
    /// cells, and its three cell styles, the title, the header and the data.
    /// </summary>
    /// <remarks>
    /// The margins are the style's own, alike for every cell style, as the file of R2000 to R2018 the library writes holds them:
    /// a margin given in the General tab of any cell style is the style's.
    /// </remarks>
    public sealed class CadTableStyleSettings
    {
        /// <summary>Gets or sets the description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets which way the rows run from the title: down, or up.</summary>
        public DxfFlowDirection FlowDirection { get; set; } = DxfFlowDirection.TopToBottom;

        /// <summary>Gets or sets the margin left and right of a cell's content.</summary>
        public double HorizontalMargin { get; set; } = 1.5;

        /// <summary>Gets or sets the margin above and below a cell's content.</summary>
        public double VerticalMargin { get; set; } = 1.5;

        /// <summary>Gets the title's cell style.</summary>
        public CadTableCellStyleSettings Title { get; } = new CadTableCellStyleSettings { TextHeight = 6.0, Alignment = DxfCellAlignment.MiddleCenter };

        /// <summary>Gets the header's cell style.</summary>
        public CadTableCellStyleSettings Header { get; } = new CadTableCellStyleSettings { Alignment = DxfCellAlignment.MiddleCenter };

        /// <summary>Gets the data's cell style.</summary>
        public CadTableCellStyleSettings Data { get; } = new CadTableCellStyleSettings();

        /// <summary>Gets the cell style of a kind of row.</summary>
        /// <param name="rowType">The kind: the title, the header or the data.</param>
        /// <returns>Its settings.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of the three.</exception>
        public CadTableCellStyleSettings Cell(DxfRowType rowType)
        {
            switch (rowType)
            {
                case DxfRowType.TitleRow:
                    return Title;
                case DxfRowType.HeaderRow:
                    return Header;
                case DxfRowType.DataRow:
                    return Data;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rowType), rowType, "A cell style is the title's, the header's or the data's.");
            }
        }

        /// <summary>Reads the settings of a style.</summary>
        /// <param name="style">The style.</param>
        /// <returns>Its settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        public static CadTableStyleSettings Of(DxfTableStyle style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            var settings = new CadTableStyleSettings
            {
                Description = style.Description,
                FlowDirection = style.FlowDirection,
                HorizontalMargin = style.HorizontalCellMargin,
                VerticalMargin = style.VerticalCellMargin,
            };

            foreach (DxfRowType kind in Kinds)
            {
                CadTableCellStyleSettings cell = settings.Cell(kind);
                cell.TextStyle = style.TextStyleName(kind);
                cell.TextHeight = style.TextHeight(kind);
                cell.TextColor = style.Color(kind);
                cell.FillColor = style.IsBackgroundColorNone(kind) ? null : style.BackgroundColor(kind);
                cell.Alignment = style.Alignment(kind);

                for (int line = 0; line < 6; line++)
                {
                    var type = (DxfGridLineType)(1 << line);
                    cell.LineWeights[line] = style.GridLineWeight(type, kind);
                    cell.LineColors[line] = style.GridColor(type, kind);
                    cell.LinesShown[line] = style.GridVisibility(type, kind);
                }
            }

            return settings;
        }

        /// <summary>Gets whether two settings write the same style.</summary>
        /// <param name="other">The other.</param>
        /// <returns>True where every value is the same.</returns>
        public bool SameAs(CadTableStyleSettings other)
            => other != null && string.Equals(Description ?? string.Empty, other.Description ?? string.Empty, StringComparison.Ordinal) && FlowDirection == other.FlowDirection
                && HorizontalMargin.Equals(other.HorizontalMargin) && VerticalMargin.Equals(other.VerticalMargin)
                && Title.SameAs(other.Title) && Header.SameAs(other.Header) && Data.SameAs(other.Data);

        /// <summary>The three kinds of row, in the order the dialog lists their cell styles: the data, the header, the title.</summary>
        internal static readonly DxfRowType[] Kinds = { DxfRowType.DataRow, DxfRowType.HeaderRow, DxfRowType.TitleRow };
    }

    /// <summary>
    /// The edits of the table styles, as AutoCAD's Table Style dialog (TABLESTYLE) makes them: each one undo step through
    /// <see cref="CadDocument.ApplyEdit"/>, refused where AutoCAD refuses it. A style changed draws anew every table written in it.
    /// </summary>
    public static class CadTableStyleEditor
    {
        /// <summary>
        /// Gets the table styles of a drawing, by name, letters of either case alike first: the entries of its dictionary
        /// ACAD_TABLESTYLE that are styles; none where it has none.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The names.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static IReadOnlyList<string> Styles(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            DxfDBDictionary styles = database.TableStyleDictionary;
            var names = new List<string>();

            if (styles != null)
            {
                foreach (DxfDBDictionaryEntry entry in styles)
                {
                    if (entry.Value is DxfTableStyle)
                    {
                        names.Add(entry.Key);
                    }
                }
            }

            names.Sort((a, b) =>
            {
                int byCase = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                return byCase != 0 ? byCase : string.CompareOrdinal(a, b);
            });
            return names;
        }

        /// <summary>Gets the style of a name; null where the drawing has none.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The name, letters of either case the same.</param>
        /// <returns>The style, or null.</returns>
        public static DxfTableStyle Find(DxfDatabase database, string name)
        {
            DxfDBDictionary styles = database?.TableStyleDictionary;
            return styles != null && name != null && styles.Contains(name.Trim()) ? styles[name.Trim()] as DxfTableStyle : null;
        }

        /// <summary>Gets the name of the current table style (CTABLESTYLE), as the dictionary spells it; Standard where the drawing names none it has.</summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The name.</returns>
        public static string Current(DxfDatabase database)
        {
            string name;

            try
            {
                name = database.Tablestyle;
            }
            catch (Exception)
            {
                name = null;
            }

            DxfDBDictionary styles = database.TableStyleDictionary;
            return name != null && styles != null && styles.Contains(name) ? styles.NameAt(styles[name]) : "Standard";
        }

        /// <summary>
        /// Gets how many tables are written in a style: the tables of model space, the layouts and the blocks that name it, as
        /// AutoCAD counts what keeps a style from being deleted.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>The number of tables.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static int CountUses(DxfDatabase database, string name)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            int count = 0;

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    count += entity is DxfTable table && string.Equals(table.TableStyleName, name, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                }
            }

            return count;
        }

        /// <summary>
        /// Makes a style current (CTABLESTYLE), as Set Current does. Refused where the drawing has no such style, or cannot hold
        /// the variable: a drawing with no CLASSES section to list DICTIONARYVAR in.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetCurrent(CadDocument document, string name)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            DxfTableStyle style = Find(document.Database, name);

            if (style == null)
            {
                return CadEditResult.Refuse("The drawing has no table style \"" + name + "\".");
            }

            string set = style.Name;
            string old = Current(document.Database);

            if (string.Equals(old, set, StringComparison.Ordinal))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            CadEditResult result = document.ApplyEdit("TABLESTYLE", new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "CTABLESTYLE", old, set) }, s => s.Database.Tablestyle = set);
            return result.Succeeded ? result : CadEditResult.Refuse("\"" + set + "\" cannot be made current: " + result.Message);
        }

        /// <summary>
        /// Adds a style, as New of the Table Style dialog: a copy of the settings of another (Start With), under a name no style
        /// of the drawing has, letters of either case the same; the dictionary ACAD_TABLESTYLE made where the drawing has none,
        /// and the named object dictionary first where the drawing was written without it.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The new name, as GH_SCHEDULE.</param>
        /// <param name="from">The style whose settings it takes; AutoCAD's Standard of the drawing's units where null or none.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Add(CadDocument document, string name, string from)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return CadEditResult.Refuse("A table style needs a name.");
            }

            if (trimmed.IndexOfAny(new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' }) >= 0)
            {
                return CadEditResult.Refuse("The name \"" + trimmed + "\" has a letter AutoCAD does not take in a name.");
            }

            if (Find(document.Database, trimmed) != null)
            {
                return CadEditResult.Refuse("Table style \"" + trimmed + "\" already exists.");
            }

            DxfTableStyle source = from == null ? null : Find(document.Database, from);
            CadTableStyleSettings settings = source == null ? Standard(document.Database) : CadTableStyleSettings.Of(source);
            document.EnsureNamedObjectsDictionary();
            return document.ApplyEdit("TABLESTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New table style", null, trimmed) }, s =>
            {
                var style = new DxfTableStyle();
                Dictionary(s.Database).SetAt(trimmed, style);
                Write(style, settings);
            });
        }

        /// <summary>
        /// Writes the settings into a style, as OK of the Modify Table Style dialog, and draws anew every table written in it.
        /// Nothing is written, and nothing refused, where they are the style's already.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>What came of it; refused for a text height not above nought, a margin below nought, or a text style the drawing has not.</returns>
        public static CadEditResult Apply(CadDocument document, string name, CadTableStyleSettings settings)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            DxfTableStyle style = Find(document.Database, name);

            if (style == null)
            {
                return CadEditResult.Refuse("The drawing has no table style \"" + name + "\".");
            }

            if (!(settings.HorizontalMargin >= 0.0) || !(settings.VerticalMargin >= 0.0) || double.IsInfinity(settings.HorizontalMargin) || double.IsInfinity(settings.VerticalMargin))
            {
                return CadEditResult.Refuse("Margins must be numbers of nought or more.");
            }

            foreach (DxfRowType kind in CadTableStyleSettings.Kinds)
            {
                CadTableCellStyleSettings cell = settings.Cell(kind);

                if (!(cell.TextHeight > 0.0) || double.IsInfinity(cell.TextHeight))
                {
                    return CadEditResult.Refuse("Text height must be a number above nought.");
                }

                if (!document.Database.TextStyleTable.Has(cell.TextStyle ?? string.Empty))
                {
                    return CadEditResult.Refuse("The drawing has no text style \"" + cell.TextStyle + "\".");
                }
            }

            if (CadTableStyleSettings.Of(style).SameAs(settings))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            string set = style.Name;
            var planned = new List<CadEditRecord> { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Table style " + set, null, set) };
            List<string> tables = Tables(document.Database, set);
            planned.AddRange(tables.Select(handle => new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Table style", set, set)));
            return document.ApplyEdit("TABLESTYLE", planned, s =>
            {
                Write(Find(s.Database, set), settings);

                foreach (string handle in tables)
                {
                    if (!s.IsHandled(handle) && s.Document.FindEntity(handle) is DxfTable table)
                    {
                        table.RecomputeTableBlock();
                    }
                }
            });
        }

        /// <summary>
        /// Deletes a style, as Delete of the Table Style dialog: refused for Standard, the current style and a style a table is
        /// written in, as AutoCAD greys Delete for them.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Delete(CadDocument document, string name)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string reason = WhyNotDeleted(document.Database, name);

            if (reason != null)
            {
                return CadEditResult.Refuse(reason);
            }

            string set = Find(document.Database, name).Name;
            return document.ApplyEdit("TABLESTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete table style", set, null) }, s => s.Database.TableStyleDictionary.Remove(set));
        }

        /// <summary>Gets why a style may not be deleted, as Delete is greyed: Standard, the current style, a style a table uses; null where it may.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>The reason, or null.</returns>
        public static string WhyNotDeleted(DxfDatabase database, string name)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            DxfTableStyle style = Find(database, name);

            if (style == null)
            {
                return "The drawing has no table style \"" + name + "\".";
            }

            if (string.Equals(style.Name, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                return "The Standard table style cannot be deleted.";
            }

            if (string.Equals(style.Name, Current(database), StringComparison.OrdinalIgnoreCase))
            {
                return "The current table style cannot be deleted.";
            }

            int uses = CountUses(database, style.Name);
            return uses > 0 ? "\"" + style.Name + "\" is in use by " + uses + (uses == 1 ? " table" : " tables") + " and cannot be deleted." : null;
        }

        /// <summary>
        /// Adds AutoCAD's Standard, of the drawing's units, where the drawing has no table style of that name, as AutoCAD's
        /// drawing always has it; one undo step, nothing where it has one.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult AddStandard(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (Find(document.Database, "Standard") != null)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            document.EnsureNamedObjectsDictionary();
            return document.ApplyEdit("TABLESTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New table style", null, "Standard") }, s => EnsureStandard(s.Database));
        }

        /// <summary>Gets the settings of AutoCAD's Standard in a drawing's units: millimetres (title 6, header and data 4.5, margins 1.5), else inches (0.25, 0.18 and 0.06).</summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The settings.</returns>
        public static CadTableStyleSettings Standard(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var settings = new CadTableStyleSettings();

            if (database.Measurement != DxfMeasurementValue.Metric)
            {
                settings.HorizontalMargin = 0.06;
                settings.VerticalMargin = 0.06;
                settings.Title.TextHeight = 0.25;
                settings.Header.TextHeight = 0.18;
                settings.Data.TextHeight = 0.18;
            }

            return settings;
        }

        /// <summary>Adds Standard, of the drawing's units, where the drawing has no table style of that name; within an edit.</summary>
        internal static void EnsureStandard(DxfDatabase database)
        {
            if (Find(database, "Standard") != null)
            {
                return;
            }

            var style = new DxfTableStyle();
            Dictionary(database).SetAt("Standard", style);
            Write(style, Standard(database));
        }

        /// <summary>The dictionary of table styles, made where the drawing has none.</summary>
        private static DxfDBDictionary Dictionary(DxfDatabase database)
        {
            if (database.TableStyleDictionary == null)
            {
                database.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            return database.TableStyleDictionary;
        }

        /// <summary>The handles of the tables written in a style, in the order of the blocks and their entities.</summary>
        private static List<string> Tables(DxfDatabase database, string name)
        {
            var handles = new List<string>();

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfTable table && string.Equals(table.TableStyleName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        handles.Add(table.Handle);
                    }
                }
            }

            return handles;
        }

        private static void Write(DxfTableStyle style, CadTableStyleSettings settings)
        {
            style.Description = settings.Description ?? string.Empty;
            style.FlowDirection = settings.FlowDirection;
            style.HorizontalCellMargin = settings.HorizontalMargin;
            style.VerticalCellMargin = settings.VerticalMargin;

            foreach (DxfRowType kind in CadTableStyleSettings.Kinds)
            {
                CadTableCellStyleSettings cell = settings.Cell(kind);
                style.SetTextStyle(cell.TextStyle, kind);
                style.SetTextHeight(cell.TextHeight, kind);
                style.SetColor(cell.TextColor ?? DxfColor.ByBlock, kind);
                style.SetAlignment(cell.Alignment, kind);

                if (cell.FillColor == null)
                {
                    style.SetBackgroundColorNone(true, kind);
                }
                else
                {
                    style.SetBackgroundColor(cell.FillColor, kind);
                }

                for (int line = 0; line < 6; line++)
                {
                    var type = (DxfGridLineType)(1 << line);
                    style.SetGridLineWeight(cell.LineWeights[line], type, kind);
                    style.SetGridColor(cell.LineColors[line] ?? DxfColor.ByBlock, type, kind);
                    style.SetGridVisibility(cell.LinesShown[line], type, kind);
                }
            }
        }
    }
}
