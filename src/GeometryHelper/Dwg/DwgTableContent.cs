using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The content of a table from R2010 as its DWG holds it (AcDbTableContent, AcDbFormattedTableData,
    /// AcDbLinkedTableData): its columns, rows and cells, each with a cell style of overrides, its merged ranges and its
    /// table style; and, of the ACAD_TABLE, its break data. Read and written by the Open Design Specification for .dwg files
    /// (20.4.96 to 20.4.101), the field orders checked on the tables AutoCAD 2021 writes.
    /// </summary>
    internal sealed class DwgTableContent
    {
        /// <summary>The name of the linked data.</summary>
        public string Name = string.Empty;

        /// <summary>The description of the linked data.</summary>
        public string Description = string.Empty;

        /// <summary>The columns, left to right.</summary>
        public readonly List<DwgTableLine> Columns = new List<DwgTableLine>();

        /// <summary>The rows, top to bottom.</summary>
        public readonly List<DwgTableLine> Rows = new List<DwgTableLine>();

        /// <summary>The overrides of the whole table.</summary>
        public DwgCellStyle Style = new DwgCellStyle { Type = 4 };

        /// <summary>The merged ranges, each its top row, left column, bottom row and right column.</summary>
        public readonly List<int[]> Merged = new List<int[]>();

        /// <summary>The handle of the table style.</summary>
        public ulong TableStyle;
    }

    /// <summary>A column or a row of a table: its custom data, its overrides, its cell style and its size; a row's cells.</summary>
    internal sealed class DwgTableLine
    {
        /// <summary>The name of a column.</summary>
        public string Name = string.Empty;

        /// <summary>The custom data, a 32-bit number.</summary>
        public int Custom;

        /// <summary>The items of custom data, each a name and a value.</summary>
        public readonly List<KeyValuePair<string, DwgCellValue>> Items = new List<KeyValuePair<string, DwgCellValue>>();

        /// <summary>The overrides.</summary>
        public DwgCellStyle Style = new DwgCellStyle();

        /// <summary>The cell style it takes, 0 for none.</summary>
        public int StyleId;

        /// <summary>The width of a column or the height of a row.</summary>
        public double Size;

        /// <summary>The cells of a row, left to right.</summary>
        public readonly List<DwgTableCell> Cells = new List<DwgTableCell>();
    }

    /// <summary>A cell of a table: its state, tooltip, contents, overrides and cell style.</summary>
    internal sealed class DwgTableCell
    {
        /// <summary>The state flags: locked, read only, linked and the like.</summary>
        public int State;

        /// <summary>The tooltip.</summary>
        public string Tooltip = string.Empty;

        /// <summary>The custom data, a 32-bit number.</summary>
        public int Custom;

        /// <summary>The items of custom data, each a name and a value, such as the check sum AutoCAD keeps for a round trip.</summary>
        public readonly List<KeyValuePair<string, DwgCellValue>> Items = new List<KeyValuePair<string, DwgCellValue>>();

        /// <summary>The contents, each a value, a field or a block.</summary>
        public readonly List<DwgCellContent> Contents = new List<DwgCellContent>();

        /// <summary>The overrides.</summary>
        public DwgCellStyle Style = new DwgCellStyle();

        /// <summary>The cell style it takes, 0 for none.</summary>
        public int StyleId;
    }

    /// <summary>One content of a cell: a value (1), a field (2) or a block (4), with its format overrides.</summary>
    internal sealed class DwgCellContent
    {
        /// <summary>The kind: 1 a value, 2 a field, 4 a block.</summary>
        public int Type;

        /// <summary>The value of a value content.</summary>
        public DwgCellValue Value = new DwgCellValue();

        /// <summary>The field or the block record of the content.</summary>
        public ulong Handle;

        /// <summary>The values of the attributes of a block content: the ATTDEF, the text, the index.</summary>
        public readonly List<object[]> Attributes = new List<object[]>();

        /// <summary>The format overrides of the content; null for none.</summary>
        public DwgContentFormat Format;
    }

    /// <summary>A value of a cell: its flags, its type, the value, its unit type, its format and the text it shows.</summary>
    internal sealed class DwgCellValue
    {
        /// <summary>The flags; 1 where the value is of the general kind and holds nothing.</summary>
        public int Flags = 1;

        /// <summary>The type: 0 unknown, 1 a long, 2 a double, 4 a string, 512 general.</summary>
        public int Type;

        /// <summary>A long value.</summary>
        public int Long;

        /// <summary>A double value.</summary>
        public double Double;

        /// <summary>The bytes of a value of a kind kept whole, a date of a field.</summary>
        public byte[] Bytes;

        /// <summary>Of a value of a field written by the DXF of R2004 or before: set where it has no flags, unit or text.</summary>
        public bool Old;

        /// <summary>A string value.</summary>
        public string Text = string.Empty;

        /// <summary>The unit type: 0 none, 1 a distance, 2 an angle, 4 an area, 8 a volume.</summary>
        public int Unit;

        /// <summary>The format string.</summary>
        public string Format = string.Empty;

        /// <summary>The text the value shows.</summary>
        public string Shown = string.Empty;
    }

    /// <summary>The format of a content (20.4.101.3), which a cell style extends.</summary>
    internal sealed class DwgContentFormat
    {
        /// <summary>The properties overridden.</summary>
        public int Overrides;

        /// <summary>The property flags (auto scale).</summary>
        public int Flags;

        /// <summary>The data type of the value.</summary>
        public int DataType;

        /// <summary>The unit type of the value.</summary>
        public int UnitType;

        /// <summary>The format string of the value.</summary>
        public string FormatString = string.Empty;

        /// <summary>The rotation.</summary>
        public double Rotation;

        /// <summary>The block scale.</summary>
        public double Scale = 1.0;

        /// <summary>The alignment, 1 top left to 9 bottom right.</summary>
        public int Alignment;

        /// <summary>The colour of the content, as its one number.</summary>
        public int Colour = unchecked((int)0xC1000000);

        /// <summary>The text style.</summary>
        public ulong TextStyle;

        /// <summary>The text height.</summary>
        public double Height;
    }

    /// <summary>A cell style (20.4.101.4): its type and, where it has data, its overrides, colours, margins and borders.</summary>
    internal sealed class DwgCellStyle
    {
        /// <summary>The type: 1 a cell, 2 a row, 3 a column, 4 formatted table data, 5 a table.</summary>
        public int Type = 1;

        /// <summary>Whether the style holds data.</summary>
        public bool HasData;

        /// <summary>The properties overridden.</summary>
        public int Overrides;

        /// <summary>The merge flags.</summary>
        public int Merge;

        /// <summary>The background colour, as its one number.</summary>
        public int Background = unchecked((int)0xC8000000);

        /// <summary>How the contents are laid out.</summary>
        public int Layout = 1;

        /// <summary>The format of the content.</summary>
        public DwgContentFormat Format = new DwgContentFormat();

        /// <summary>The margin flags; 1 where margins follow.</summary>
        public int MarginFlags;

        /// <summary>The margins: vertical, horizontal, bottom, right, horizontal spacing, vertical spacing.</summary>
        public double[] Margins = new double[6];

        /// <summary>The borders present.</summary>
        public readonly List<DwgCellBorder> Borders = new List<DwgCellBorder>();
    }

    /// <summary>A border of a cell style: its edge, overrides, type, colour, lineweight, linetype and visibility.</summary>
    internal sealed class DwgCellBorder
    {
        /// <summary>The edge: 1 top, 2 right, 4 bottom, 8 left, 0x10 inside vertical, 0x20 inside horizontal.</summary>
        public int Edge;

        /// <summary>The properties overridden.</summary>
        public int Overrides;

        /// <summary>The type: 1 single, 2 double.</summary>
        public int Type = 1;

        /// <summary>The colour, as its one number.</summary>
        public int Colour = unchecked((int)0xC0000000);

        /// <summary>The lineweight.</summary>
        public int LineWeight = -2;

        /// <summary>The linetype.</summary>
        public ulong Linetype;

        /// <summary>1 where the border is hidden.</summary>
        public int Invisible;

        /// <summary>The spacing of a double line, 0.045 where AutoCAD 2021 gives none of its own.</summary>
        public double Spacing = 0.045;
    }
}
