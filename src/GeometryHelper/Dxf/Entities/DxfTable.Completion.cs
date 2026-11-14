using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The TABLECONTENT, TABLEGEOMETRY and round trip AutoCAD 2021 makes of a table when it reads the file, made when the
    /// drawing is saved (<see cref="DxfDatabase.Save(string)"/>), so that AutoCAD has nothing to build or mend.
    /// </summary>
    /// <remarks>
    /// AutoCAD gives every table of a file that holds none its extension dictionary, with <c>ACAD_XREC_ROUNDTRIP</c> and in it
    /// the XRECORD of <c>ACAD_ROUNDTRIP_2008_TABLE_ENTITY</c>: the TABLECONTENT of the table's columns and rows, each cell with
    /// its content (value, format) and its look (alignment, fill, the lines it sets), and the TABLEGEOMETRY of its cells. What a
    /// cell holds is read from the cell's own groups as AutoCAD reads them.
    /// </remarks>
    public sealed partial class DxfTable
    {
        // A cell's flags of overrides as AutoCAD's own content format names them (the 90 of its CONTENTFORMAT): the kind and the
        // format of the value, the turn, the text colour, style and height.
        private const int ContentKind = 3;
        private const int ContentRotation = 4;
        private const int ContentColour = 32;
        private const int ContentTextStyle = 64;
        private const int ContentHeight = 128;

        // And the 91 of its table format: the alignment and the fill.
        private const int FormatAlignment = 16;
        private const int FormatFill = 512;

        // The edges of a cell (top 0, right 1, bottom 2, left 3) in the order AutoCAD lists the lines of a cell in its TABLEFORMAT:
        // the top, the bottom, the left and the right, each by the number of its line (95).
        private static readonly int[] ListedEdges = { 0, 2, 3, 1 };
        private static readonly int[] ListedLines = { 1, 4, 8, 32 };

        /// <summary>
        /// Whether AutoCAD 2021 builds a TABLECONTENT for the table when it reads the file: a table of a drawing of R2007 or later
        /// that has no extension dictionary.
        /// </summary>
        internal bool NeedsContent =>
            Item != null && !IsErased && Item.File.Supports(1021) && Item.File.ExtensionDictionaryOf(Item) == null;

        /// <summary>
        /// Makes the table's extension dictionary, its XRECORD of <c>ACAD_ROUNDTRIP_2008_TABLE_ENTITY</c>, its TABLECONTENT and
        /// its TABLEGEOMETRY as AutoCAD 2021 makes them, lists the table and its style in ACDB_RECOMPOSE_DATA and makes the
        /// classes. A table whose cells hold more than text and values AutoCAD keeps in its content (a block, a field, a link),
        /// or that holds overrides of its own, is left as it is.
        /// </summary>
        /// <returns>Whether the table was given them.</returns>
        internal bool MakeContent()
        {
            if (!NeedsContent)
            {
                return false;
            }

            Layout layout = Read();

            if (!Holds(layout) || TableStyleOverrides().Length > 0)
            {
                return false;
            }

            string style = StyleHandle();
            List<KeyValuePair<int, string>> content = FullContentGroups(layout, style);

            if (content == null)
            {
                return false;
            }

            DxfFile file = Item.File;
            NormalizeEdges(layout);
            MakeExtensionDictionary(file, Item, false);
            DxfItem record = ExtensionDictionary.Add("ACAD_XREC_ROUNDTRIP", "XRECORD", b =>
            {
                b.Group(100, "AcDbXrecord");
                b.Group(280, "1");
                b.Group(102, "ACAD_ROUNDTRIP_2008_TABLE_ENTITY");
                b.Group(360, "0");
                b.Group(70, "2");
                b.Group(90, "1");
                b.Group(10, "0.0");
                b.Group(20, "0.0");
                b.Group(30, "0.0");
                b.Group(90, "0");
                b.Group(90, layout.Columns.ToString(CultureInfo.InvariantCulture));
                b.Group(361, "0");
            });
            DxfItem made = file.AddObject("TABLECONTENT", null, b =>
            {
                b.Group(330, record.Handle);
                b.Group(100, "AcDbLinkedData");
                b.Group(1, string.Empty);
                b.Group(300, string.Empty);
                b.Group(100, "AcDbLinkedTableData");

                foreach (KeyValuePair<int, string> group in content)
                {
                    b.Group(group.Key, group.Value);
                }
            });
            DxfItem geometry = file.AddObject("TABLEGEOMETRY", null, b =>
            {
                b.Group(330, record.Handle);
                b.Group(100, "AcDbTableGeometry");
                b.Group(90, layout.Rows.ToString(CultureInfo.InvariantCulture));
                b.Group(91, layout.Columns.ToString(CultureInfo.InvariantCulture));
                b.Group(92, (layout.Rows * layout.Columns).ToString(CultureInfo.InvariantCulture));

                for (int k = 0; k < layout.Rows * layout.Columns; k++)
                {
                    b.Group(93, "0");
                    b.Group(40, "0.0");
                    b.Group(41, "0.0");
                    b.Group(330, "0");
                    b.Group(94, "0");
                }
            });

            // The XRECORD's 360 and 361 made the TABLECONTENT's and the TABLEGEOMETRY's, their handles known now.
            IReadOnlyList<DxfTypedValue> now = file.GroupsOf(record);
            int at = 0;

            while (at < now.Count && !(now[at].TypeCode == 360 && now[at].Value.Trim() == "0"))
            {
                at++;
            }

            List<KeyValuePair<int, string>> tail = file.TailOf(record, at)
                .ConvertAll(g => new KeyValuePair<int, string>(g.TypeCode, g.Value));
            tail[0] = new KeyValuePair<int, string>(360, made.Handle);
            tail[tail.Count - 1] = new KeyValuePair<int, string>(361, geometry.Handle);
            file.ReplaceTail(record, at, tail);
            Recompose(style);
            file.AddClass("TABLECONTENT", "AcDbTableContent", 1152);
            file.AddClass("TABLEGEOMETRY", "AcDbTableGeometry", 1152);
            file.CountClassBy("TABLECONTENT", 1);
            file.CountClassBy("TABLEGEOMETRY", 1);
            return true;
        }

        /// <summary>
        /// The groups of a TABLECONTENT for the table as it stands, from the count of its columns on, as AutoCAD 2021 builds it
        /// from an ACAD_TABLE it reads: each column of its width, each row of its kind (1 the title, 2 the header, 3 data) and
        /// height and of one cell for each column, each cell with the checksum of its text, its content, its look and its lines;
        /// the merged cells; the table style. Null where a cell is not one the content is built for here.
        /// </summary>
        private List<KeyValuePair<int, string>> FullContentGroups(Layout layout, string style)
        {
            var groups = new List<KeyValuePair<int, string>> { G(90, layout.Columns) };
            string[][][] edges = ContentEdges(layout);

            foreach (int place in layout.ColumnAt)
            {
                groups.AddRange(ColumnGroups(ContentNumber(layout.Groups.List[place].Value)));
            }

            groups.Add(G(91, layout.Rows));
            var merges = new List<int[]>();

            for (int r = 0; r < layout.Rows; r++)
            {
                var cells = new List<ContentGridCell>();

                for (int c = 0; c < layout.Columns; c++)
                {
                    CellView cell = layout.CellAt(r, c);
                    List<KeyValuePair<int, string>> made = FullCellGroups(cell, edges[r * layout.Columns + c]);

                    if (made == null)
                    {
                        return null;
                    }

                    cells.Add(new ContentGridCell(made));
                    int wide = cell.Whole(175, 0);
                    int high = cell.Whole(176, 0);

                    if (wide > 1 || high > 1)
                    {
                        merges.Add(
                            new[]
                            {
                                r,
                                c,
                                Math.Min(layout.Rows - 1, r + Math.Max(1, high) - 1),
                                Math.Min(layout.Columns - 1, c + Math.Max(1, wide) - 1),
                            }
                        );
                    }
                }

                DxfRowType type = RowType(r);
                string kind =
                    type == DxfRowType.TitleRow ? "1"
                    : type == DxfRowType.HeaderRow ? "2"
                    : "3";
                var row = new ContentRow(
                    new List<KeyValuePair<int, string>>
                    {
                        new KeyValuePair<int, string>(301, "ROW"),
                        new KeyValuePair<int, string>(1, "LINKEDTABLEDATAROW_BEGIN"),
                    },
                    null,
                    cells,
                    RowTail(kind, ContentNumber(layout.Groups.List[layout.RowAt[r]].Value))
                );
                row.AddTo(groups);
            }

            groups.Add(G(92, 0));
            groups.Add(new KeyValuePair<int, string>(100, "AcDbFormattedTableData"));
            groups.Add(new KeyValuePair<int, string>(300, "TABLEFORMAT"));
            groups.Add(new KeyValuePair<int, string>(1, "TABLEFORMAT_BEGIN"));
            groups.Add(G(90, 4));
            groups.Add(G(170, 0));
            groups.Add(new KeyValuePair<int, string>(309, "TABLEFORMAT_END"));
            ContentGrid.AddMerges(groups, merges);
            groups.Add(new KeyValuePair<int, string>(100, "AcDbTableContent"));
            groups.Add(new KeyValuePair<int, string>(340, style));
            return groups;
        }

        private static KeyValuePair<int, string> G(int code, string value) =>
            new KeyValuePair<int, string>(code, value);

        /// <summary>
        /// The groups of one cell of the TABLECONTENT as AutoCAD 2021 builds it from the cell of the ACAD_TABLE: its checksum, its
        /// content (the cell's value as it stands, and its format: the kind of the value, its turn, text colour, text style and
        /// height) unless a merged block hides the cell, its look (alignment, fill) and the lines it sets. Null where the cell is a
        /// block, a field or a link, or holds no value, which the content is not built for here.
        /// </summary>
        private List<KeyValuePair<int, string>> FullCellGroups(CellView cell, string[][] edges)
        {
            if (
                cell.Whole(171, 1) != (int)DxfTableCellType.TextCell
                || cell.Whole(92, 0) != 0
                || cell.Head(344) != null
                || cell.ValueFrom < 0
                || cell.ValueTo < 0
            )
            {
                return null;
            }

            int flags = cell.Flags ?? 0;
            bool hidden = cell.Whole(173, 0) == 1;
            string text = DxfReader.Decode(cell.Text());
            List<KeyValuePair<int, string>> groups = CellGroups(hidden ? "0.0" : Checksum(text));
            groups.RemoveAt(groups.FindIndex(g => g.Key == 95));
            int format = groups.FindIndex(g => g.Key == 1 && g.Value == "FORMATTEDTABLEDATACELL_BEGIN");
            int end = groups.FindIndex(g => g.Key == 309 && g.Value == "FORMATTEDTABLEDATACELL_END");
            groups.RemoveRange(format, end + 1 - format);
            var content = new List<KeyValuePair<int, string>>();

            if (hidden)
            {
                content.Add(G(95, 0));
            }
            else
            {
                content.Add(G(95, 1));
                content.Add(G(302, "CONTENT"));
                content.Add(G(1, "CELLCONTENT_BEGIN"));
                content.Add(G(90, 1));
                content.Add(G(300, "VALUE"));
                IReadOnlyList<KeyValuePair<int, string>> value = cell.Groups.List;

                for (int i = cell.ValueFrom + 1; i <= cell.ValueTo; i++)
                {
                    content.Add(value[i]);
                }

                content.Add(G(91, 0));
                content.Add(G(309, "CELLCONTENT_END"));
                content.Add(G(1, "FORMATTEDCELLCONTENT_BEGIN"));
                content.Add(G(170, 1));
                content.Add(G(300, "CONTENTFORMAT"));
                content.Add(G(1, "CONTENTFORMAT_BEGIN"));
                AddContentFormat(content, cell, flags);
                content.Add(G(309, "CONTENTFORMAT_END"));
                content.Add(G(309, "FORMATTEDCELLCONTENT_END"));
            }

            int none = groups.FindIndex(g => g.Key == 92 && g.Value.Trim() == "0");
            groups.InsertRange(none + 1, content);
            end = groups.FindIndex(g => g.Key == 309 && g.Value == "LINKEDTABLEDATACELL_END");
            var look = new List<KeyValuePair<int, string>>();
            AddCellFormat(look, cell, edges);
            groups.InsertRange(end + 1, look);
            return groups;
        }

        /// <summary>The groups of the CONTENTFORMAT of a cell's content: its flags, the unit, kind and format of its value, its turn, colour, text style and height.</summary>
        private void AddContentFormat(List<KeyValuePair<int, string>> content, CellView cell, int flags)
        {
            int mask = (flags & 0x40000) != 0 ? ContentKind : 0;
            string turn = cell.Head(145);
            double angle = 0.0;
            bool turned = turn != null && Numbers.TryRead(turn, out angle) && Guard.IsFinite(angle) && angle != 0.0;
            string colour = cell.Override(64, 0x08);
            string textStyle = cell.Override(7, 0x10);
            string height = cell.Override(140, 0x20);
            mask |= turned ? ContentRotation : 0;
            mask |= colour != null ? ContentColour : 0;
            mask |= textStyle != null ? ContentTextStyle : 0;
            mask |= height != null ? ContentHeight : 0;
            string kind = "512";
            string unit = "0";
            string pattern = string.Empty;

            for (int i = cell.ValueFrom + 1; i < cell.ValueTo; i++)
            {
                KeyValuePair<int, string> group = cell.Groups.List[i];

                if (group.Key == 90)
                {
                    kind = group.Value.Trim() == "0" ? "512" : group.Value.Trim();
                }
                else if (group.Key == 94)
                {
                    unit = group.Value.Trim();
                }
                else if (group.Key == 300)
                {
                    pattern = group.Value;
                }
            }

            content.Add(G(90, mask));
            content.Add(G(91, unit));
            content.Add(G(92, kind));
            content.Add(G(93, 0));
            content.Add(G(300, pattern));
            content.Add(G(40, turned ? turn.Trim() : "0.0"));
            content.Add(G(140, "1.0"));
            content.Add(G(94, 1));
            content.Add(G(62, colour != null ? colour.Trim() : "0"));
            content.Add(G(340, textStyle != null ? TextStyleHandle(textStyle) : "0"));
            content.Add(G(144, height != null ? ContentNumber(height) : "0.18"));
        }

        /// <summary>The handle of the text style of a name in the table's drawing; nought where it has none.</summary>
        private string TextStyleHandle(string name)
        {
            DxfTextStyleTable styles = Item.File.Database.TextStyleTable;
            string key = DxfReader.Decode(name.Trim());
            return styles.Has(key) ? styles[key].Handle ?? "0" : "0";
        }

        /// <summary>
        /// The groups of the table format of a cell (FORMATTEDTABLEDATACELL): the flags of its own look (the alignment and the
        /// fill), the fill, the content format with the alignment, and the lines the cell sets, each with the properties it
        /// gives, as AutoCAD 2021 builds them from the groups of the cell.
        /// </summary>
        private void AddCellFormat(List<KeyValuePair<int, string>> look, CellView cell, string[][] edges)
        {
            string align = cell.Override(170, 0x01);
            string fill = cell.Override(283, 0x02);
            string colour = cell.Override(63, 0x04);
            int mask = align != null ? FormatAlignment : 0;
            int background = 257;

            if (colour != null || fill != null)
            {
                bool none = fill != null && fill.Trim() == "1";
                mask |= FormatFill;
                background = none || colour == null ? 257 : ColourIndex(colour);
            }

            look.Add(G(1, "FORMATTEDTABLEDATACELL_BEGIN"));
            look.Add(G(300, "CELLTABLEFORMAT"));
            look.Add(G(1, "TABLEFORMAT_BEGIN"));
            look.Add(G(90, 1));
            look.Add(G(170, 1));
            look.Add(G(91, mask));
            look.Add(G(92, 0));
            look.Add(G(62, background));
            look.Add(G(93, 1));
            look.Add(G(300, "CONTENTFORMAT"));
            look.Add(G(1, "CONTENTFORMAT_BEGIN"));
            look.Add(G(90, 0));
            look.Add(G(91, 0));
            look.Add(G(92, 512));
            look.Add(G(93, 0));
            look.Add(G(300, string.Empty));
            look.Add(G(40, "0.0"));
            look.Add(G(140, "1.0"));
            look.Add(G(94, align != null ? align.Trim() : "1"));
            look.Add(G(62, 0));
            look.Add(G(340, "0"));
            look.Add(G(144, "0.18"));
            look.Add(G(309, "CONTENTFORMAT_END"));
            look.Add(G(171, 0));

            var lines = new List<KeyValuePair<int, string>>();
            int count = 0;

            // The lines in the order AutoCAD lists them, top, bottom, left and right, each by the number of the line (95).
            for (int k = 0; k < 4; k++)
            {
                int edge = ListedEdges[k];
                string[] held = edges[edge];

                if (held == null)
                {
                    continue;
                }

                int mask2 = (held[0] != null ? 8 : 0) | (held[1] != null ? 2 : 0) | (held[2] != null ? 16 : 0);
                count++;
                lines.Add(G(95, ListedLines[k]));
                lines.Add(G(302, "GRIDFORMAT"));
                lines.Add(G(1, "GRIDFORMAT_BEGIN"));
                lines.Add(G(90, mask2));
                lines.Add(G(91, 1));
                lines.Add(G(62, held[0] != null ? WholeOf(held[0]) : 0));
                lines.Add(G(92, held[1] != null ? WholeOf(held[1]) : 0));
                lines.Add(G(340, "0"));
                lines.Add(G(93, held[2] != null && WholeOf(held[2]) == 0 ? 1 : 0));
                lines.Add(G(40, "0.045"));
                lines.Add(G(309, "GRIDFORMAT_END"));
            }

            look.Add(G(94, count));
            look.AddRange(lines);
            look.Add(G(309, "TABLEFORMAT_END"));
            look.Add(G(309, "FORMATTEDTABLEDATACELL_END"));
        }

        /// <summary>
        /// The lines of each cell as AutoCAD 2021 lists them in the TABLECONTENT it builds, by cell (row by row), then edge (top 0,
        /// right 1, bottom 2, left 3): each the texts of its colour, lineweight and showing (null for one not given), null where
        /// the cell lists no line there. A line is listed once, with the cell above or left of it: where both cells hold it the
        /// line is the bottom or right of the cell above or left, its properties those of the cell below or right of it, which
        /// AutoCAD reads the line from; a line one cell alone holds is listed with that cell.
        /// </summary>
        private string[][][] ContentEdges(Layout layout)
        {
            int rows = layout.Rows;
            int columns = layout.Columns;
            var own = new string[rows * columns][][];
            var hidden = new bool[rows * columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    CellView cell = layout.CellAt(r, c);
                    var held = new string[4][];
                    hidden[r * columns + c] = cell.Whole(173, 0) == 1;

                    for (int edge = 0; cell.Whole(172, 0) != 0 && edge < 4; edge++)
                    {
                        if (cell.HoldsEdge(edge))
                        {
                            held[edge] = new[]
                            {
                                cell.EdgeText(edge, 0),
                                cell.EdgeText(edge, 1),
                                cell.EdgeText(edge, 2),
                            };
                        }
                    }

                    own[r * columns + c] = held;
                }
            }

            var listed = new string[rows * columns][][];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    string[][] mine = own[r * columns + c];
                    var made = new string[4][];
                    string[][] above = r > 0 ? own[(r - 1) * columns + c] : null;
                    string[][] below = r + 1 < rows ? own[(r + 1) * columns + c] : null;
                    string[][] left = c > 0 ? own[r * columns + c - 1] : null;
                    string[][] right = c + 1 < columns ? own[r * columns + c + 1] : null;
                    bool inside = hidden[r * columns + c];
                    made[0] =
                        above != null && (above[2] != null || inside || hidden[(r - 1) * columns + c]) ? null : mine[0];
                    made[3] =
                        left != null && (left[1] != null || inside || hidden[r * columns + c - 1]) ? null : mine[3];
                    made[2] =
                        mine[2] != null && below != null && below[0] != null ? below[0]
                        : below != null && hidden[(r + 1) * columns + c] && !inside ? null
                        : mine[2];
                    made[1] =
                        mine[1] != null && right != null && right[3] != null ? right[3]
                        : right != null && hidden[r * columns + c + 1] && !inside ? null
                        : mine[1];
                    listed[r * columns + c] = made;
                }
            }

            return listed;
        }

        // The flags of the lines of a cell (colour, lineweight and showing of its four edges), as one mask.
        private static int EdgeBits =>
            CellView.EdgeFlags(0) | CellView.EdgeFlags(1) | CellView.EdgeFlags(2) | CellView.EdgeFlags(3);

        /// <summary>
        /// Writes the lines of the table's cells as AutoCAD 2021 writes them when it has read the table: the line between two
        /// cells is the top or left of the cell below or right of it, read as <see cref="EdgeOverride"/> reads it, and the bottom
        /// and right of the cells on the table's rim stay with them; each line holds all three properties, its flags set, its
        /// lineweight and colour written (by block where it had none) and its showing written where it is hidden. A line
        /// between rows of two kinds that the style of the cell below leaves as its default takes the style of the cell above's
        /// bottom: the line under a red title is the header's top, red. A cell is written anew where its lines change.
        /// </summary>
        private void NormalizeEdges(Layout layout)
        {
            int rows = layout.Rows;
            int columns = layout.Columns;
            DxfTableStyle style = Item.File.Database.TryGetObject(StyleHandle(), out DxfDBObject found)
                ? found as DxfTableStyle
                : null;
            var kinds = new DxfRowType[rows];

            for (int r = 0; r < rows; r++)
            {
                kinds[r] = RowType(r);
            }

            var wanted = new string[rows * columns][][];

            // A cell that sets no line of its own, beside none that does, and not at the top of a row of another kind, reads none.
            var sets = new bool[rows * columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    sets[r * columns + c] = layout.CellAt(r, c).Whole(172, 0) != 0;
                }
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var edges = new string[4][];
                    bool aside =
                        sets[r * columns + c]
                        || (r > 0 && sets[(r - 1) * columns + c])
                        || (r + 1 < rows && sets[(r + 1) * columns + c])
                        || (c > 0 && sets[r * columns + c - 1])
                        || (c + 1 < columns && sets[r * columns + c + 1]);
                    bool boundary = r > 0 && style != null && kinds[r] != kinds[r - 1];

                    if (!aside && !boundary)
                    {
                        wanted[r * columns + c] = edges;
                        continue;
                    }

                    for (int edge = 0; edge < 4; edge++)
                    {
                        bool kept =
                            edge == 0 || edge == 3 || (edge == 2 && r == rows - 1) || (edge == 1 && c == columns - 1);

                        if (!kept)
                        {
                            continue;
                        }

                        var texts = new[]
                        {
                            EdgeOverride(r, c, edge, 0),
                            EdgeOverride(r, c, edge, 1),
                            EdgeOverride(r, c, edge, 2),
                        };

                        if (edge == 0 && r > 0 && style != null && kinds[r] != kinds[r - 1])
                        {
                            LineBetweenKinds(style, kinds[r - 1], kinds[r], texts);
                        }

                        edges[edge] = IsDefaultLine(texts) ? null : texts;
                    }

                    wanted[r * columns + c] = edges;
                }
            }

            var made = new List<KeyValuePair<int, string>>[layout.CellStart.Count];
            bool any = false;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    CellView cell = layout.CellAt(r, c);
                    string[][] edges = wanted[r * columns + c];
                    bool has = cell.Whole(172, 0) != 0 || ((cell.Flags ?? 0) & EdgeBits) != 0;
                    bool wants = edges[0] != null || edges[1] != null || edges[2] != null || edges[3] != null;

                    bool fill = cell.Override(63, 0x04) != null && cell.Override(283, 0x02) == null;

                    if (!has && !wants && !fill)
                    {
                        continue;
                    }

                    CellView apart = View(Slice(cell));
                    apart.RemoveOverrides(EdgeBits);

                    // A cell that gives a colour for its background is filled with it: AutoCAD writes the fill (283 of 0) beside it.
                    if (fill)
                    {
                        apart.SetOverride(283, "0", 91);
                    }

                    int bits = 0;

                    for (int edge = 0; edge < 4; edge++)
                    {
                        string[] texts = edges[edge];

                        if (texts == null)
                        {
                            continue;
                        }

                        bits |= 1 << edge;
                        apart.SetOverride(EdgeCodes[1][edge], texts[1] ?? "-2", 91);
                        apart.SetOverride(EdgeCodes[0][edge], texts[0] ?? "0", 91);

                        if (texts[2] != null && WholeOf(texts[2]) == 0)
                        {
                            apart.SetOverride(EdgeCodes[2][edge], "0", 91);
                        }

                        apart.SetFlags((apart.Flags ?? 0) | Layout.CellOverrides[EdgeCodes[2][edge]], 91);
                    }

                    apart.SetHead(172, bits.ToString(CultureInfo.InvariantCulture));
                    made[r * columns + c] = apart.Groups.List;
                    any = true;
                }
            }

            if (any)
            {
                Splice(layout, made);
            }
        }

        /// <summary>Whether a line holds no property that differs from the default: colour by block, lineweight by block, drawn. AutoCAD keeps no such line.</summary>
        private static bool IsDefaultLine(string[] texts) =>
            (texts[0] == null || WholeOf(texts[0]) == 0)
            && (texts[1] == null || WholeOf(texts[1]) == -2)
            && (texts[2] == null || WholeOf(texts[2]) != 0);

        /// <summary>
        /// Gives a line between the rows of two kinds the properties AutoCAD 2021 takes from the style: those of the line the
        /// cell below leaves as its default (colour by block, lineweight by block, drawn) take those of the bottom of the kind
        /// above where they are not, and the line is written only where they are.
        /// </summary>
        private static void LineBetweenKinds(DxfTableStyle style, DxfRowType above, DxfRowType below, string[] texts)
        {
            if (
                texts[0] == null
                && style.GridColor(DxfGridLineType.HorizontalTop, below).ColorIndex == 0
                && style.GridColor(DxfGridLineType.HorizontalBottom, above).ColorIndex != 0
            )
            {
                texts[0] = style
                    .GridColor(DxfGridLineType.HorizontalBottom, above)
                    .ColorIndex.ToString(CultureInfo.InvariantCulture);
            }

            DxfLineWeight lower = style.GridLineWeight(DxfGridLineType.HorizontalTop, below);
            DxfLineWeight upper = style.GridLineWeight(DxfGridLineType.HorizontalBottom, above);

            if (texts[1] == null && lower == DxfLineWeight.ByBlock && upper != DxfLineWeight.ByBlock)
            {
                texts[1] = ((int)upper).ToString(CultureInfo.InvariantCulture);
            }

            if (
                texts[2] == null
                && style.GridVisibility(DxfGridLineType.HorizontalTop, below)
                && !style.GridVisibility(DxfGridLineType.HorizontalBottom, above)
            )
            {
                texts[2] = "0";
            }
        }

        private static int WholeOf(string text) =>
            int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        private static int ColourIndex(string text) => WholeOf(text);

        /// <summary>
        /// Gives an object the extension dictionary AutoCAD 2021 gives a table or a table style it builds the round trip of: an
        /// empty DICTIONARY owned hard by the object (280 of 1) that names it as its owner but not as its reactor, the group
        /// <c>{ACAD_XDICTIONARY</c> in the object's record, first of a style, as AutoCAD writes them.
        /// </summary>
        internal static void MakeExtensionDictionary(DxfFile file, DxfItem owner, bool beforeReactors)
        {
            DxfItem made = file.AddObject("DICTIONARY", null, b =>
            {
                b.Group(330, owner.Handle);
                b.Group(100, "AcDbDictionary");
                b.Group(280, "1");
                b.Group(281, "1");
            });
            file.SetExtensionDictionary(owner, made.Handle, beforeReactors);
        }

        /// <summary>The handle of the table style the table names or takes (the drawing's Standard); null where there is none.</summary>
        internal string StyleHandleOf()
        {
            string handle = StyleHandle();
            return handle == "0" ? null : handle;
        }
    }
}
