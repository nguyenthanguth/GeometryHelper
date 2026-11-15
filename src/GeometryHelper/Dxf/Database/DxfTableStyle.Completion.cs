using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfTableStyle
    {
        // The lines of a cell style in the order AutoCAD 2021 writes them, each by the number its cells give it (95).
        private static readonly DxfGridLineType[] MapLines =
        {
            DxfGridLineType.HorizontalTop,
            DxfGridLineType.HorizontalInside,
            DxfGridLineType.HorizontalBottom,
            DxfGridLineType.VerticalLeft,
            DxfGridLineType.VerticalInside,
            DxfGridLineType.VerticalRight,
        };

        /// <summary>The name of the entry of a style's extension dictionary that holds its map of cell styles.</summary>
        internal const string CellStyleMapName = "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP";

        /// <summary>
        /// Whether the style is one AutoCAD 2021 gives a map of its cell styles when it reads it: a style of a drawing of R2007
        /// or later that has no extension dictionary yet.
        /// </summary>
        internal bool NeedsCellStyleMap =>
            Item != null
            && IsResident
            && !IsErased
            && Item.File.Supports(1021)
            && Item.File.ExtensionDictionaryOf(Item) == null;

        /// <summary>
        /// Gives the style the round trip AutoCAD 2021 makes of it when it reads the file (and which it reads the cell styles back
        /// from): an extension dictionary holding <c>ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP</c>, a CELLSTYLEMAP of the
        /// title, the header and the data, each its table format (fill, margins, the six lines) and its content format (text
        /// height, alignment, colour, text style), the names <c>_TITLE</c>, <c>_HEADER</c> and <c>_DATA</c> after them. A style
        /// the library holds is listed in the drawing's ACDB_RECOMPOSE_DATA, and the class CELLSTYLEMAP made.
        /// </summary>
        /// <returns>Whether it was made: false where the style needs none.</returns>
        internal bool MakeCellStyleMap()
        {
            if (!NeedsCellStyleMap)
            {
                return false;
            }

            DxfFile file = Item.File;
            List<KeyValuePair<int, string>> groups = CellStyleMapGroups();
            DxfTable.MakeExtensionDictionary(file, Item, true);
            ExtensionDictionary.Add(CellStyleMapName, "CELLSTYLEMAP", b =>
            {
                b.Group(100, "AcDbCellStyleMap");

                foreach (KeyValuePair<int, string> group in groups)
                {
                    b.Group(group.Key, group.Value);
                }
            });
            file.AddClass("CELLSTYLEMAP", "AcDbCellStyleMap", 1152);
            file.CountClassBy("CELLSTYLEMAP", 1);
            DxfTable.ListForRecompose(file, Handle, null);
            return true;
        }

        private static KeyValuePair<int, string> G(int code, string value) =>
            new KeyValuePair<int, string>(code, value);

        private static KeyValuePair<int, string> G(int code, int value) =>
            new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// The groups of the CELLSTYLEMAP after its subclass marker, as AutoCAD 2021 writes them for a style: the count of cell
        /// styles (3), then for the title, the header and the data its table format and its name.
        /// </summary>
        private List<KeyValuePair<int, string>> CellStyleMapGroups()
        {
            var groups = new List<KeyValuePair<int, string>> { G(90, 3) };
            DxfRowType[] kinds = { DxfRowType.TitleRow, DxfRowType.HeaderRow, DxfRowType.DataRow };
            string[] names = { "_TITLE", "_HEADER", "_DATA" };

            for (int k = 0; k < 3; k++)
            {
                AddCellStyle(groups, kinds[k]);
                groups.Add(G(1, "CELLSTYLE_BEGIN"));
                groups.Add(G(90, k + 1));
                groups.Add(G(91, k == 2 ? 2 : 1));
                groups.Add(G(300, names[k]));
                groups.Add(G(309, "CELLSTYLE_END"));
            }

            return groups;
        }

        private void AddCellStyle(List<KeyValuePair<int, string>> groups, DxfRowType kind)
        {
            Model model = Peek();
            int cell = Kind(kind, nameof(kind));
            string margin = DxfTable.AsReal(Math.Abs(VerticalCellMargin));
            string side = DxfTable.AsReal(Math.Abs(HorizontalCellMargin));
            DxfFile file = Item.File;
            string textStyle = TextStyleName(kind);
            string textHandle =
                !string.IsNullOrEmpty(textStyle) && file.Database.TextStyleTable.Has(textStyle)
                    ? file.Database.TextStyleTable[textStyle].Handle ?? "0"
                    : "0";

            groups.Add(G(300, "CELLSTYLE"));
            groups.Add(G(1, "TABLEFORMAT_BEGIN"));
            groups.Add(G(90, 5));
            groups.Add(G(170, 1));
            groups.Add(G(91, 0));
            groups.Add(G(92, kind == DxfRowType.TitleRow ? 32768 : 0));
            groups.Add(G(62, IsBackgroundColorNone(kind) ? 257 : BackgroundColor(kind).ColorIndex));
            groups.Add(G(93, 1));
            groups.Add(G(300, "CONTENTFORMAT"));
            groups.Add(G(1, "CONTENTFORMAT_BEGIN"));
            groups.Add(G(90, 0));
            groups.Add(G(91, Whole(model.Cell(cell, 91), 0)));
            groups.Add(G(92, Whole(model.Cell(cell, 90), 512)));
            groups.Add(G(93, 0));
            groups.Add(G(300, model.Cell(cell, 1) ?? string.Empty));
            groups.Add(G(40, "0.0"));
            groups.Add(G(140, "1.0"));
            groups.Add(G(94, (int)Alignment(kind)));
            groups.Add(G(62, Color(kind).ColorIndex));
            groups.Add(G(340, textHandle));
            groups.Add(G(144, DxfTable.AsReal(TextHeight(kind))));
            groups.Add(G(309, "CONTENTFORMAT_END"));
            groups.Add(G(171, 1));
            groups.Add(G(301, "MARGIN"));
            groups.Add(G(1, "CELLMARGIN_BEGIN"));
            groups.Add(G(40, margin));
            groups.Add(G(40, side));
            groups.Add(G(40, margin));
            groups.Add(G(40, side));
            groups.Add(G(40, "0.18"));
            groups.Add(G(40, "0.18"));
            groups.Add(G(309, "CELLMARGIN_END"));
            groups.Add(G(94, MapLines.Length));

            foreach (DxfGridLineType line in MapLines)
            {
                groups.Add(G(95, (int)line));
                groups.Add(G(302, "GRIDFORMAT"));
                groups.Add(G(1, "GRIDFORMAT_BEGIN"));
                groups.Add(G(90, 0));
                groups.Add(G(91, 1));
                groups.Add(G(62, GridColor(line, kind).ColorIndex));
                groups.Add(G(92, (int)GridLineWeight(line, kind)));
                groups.Add(G(340, "0"));
                groups.Add(G(93, GridVisibility(line, kind) ? 0 : 1));
                groups.Add(G(40, "0.045"));
                groups.Add(G(309, "GRIDFORMAT_END"));
            }

            groups.Add(G(309, "TABLEFORMAT_END"));
        }
    }
}
