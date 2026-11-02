using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How <see cref="DxfTable.Fill(DxfCellRange, DxfCellRange, DxfTableFillOptions)"/> fills cells from others, as AutoCAD's
    /// <c>TableFillOptions</c> flags it: down the columns, or along the rows with <see cref="Row"/>; the content, the format or
    /// both copied, or numbers carried on in a series with <see cref="GenerateSeries"/>; none of the three fills nothing.
    /// </summary>
    [Flags]
    public enum DxfTableFillOptions
    {
        /// <summary>Down the columns and nothing copied, 0: AutoCAD 2021 filled nothing of this alone (D33 f01).</summary>
        None = 0,

        /// <summary>Along the rows, 1.</summary>
        Row = 1,

        /// <summary>The series the other way, 2: back from the source's first number, 10 and 20 filled down to -20, -10, 0; a copy unchanged.</summary>
        Reverse = 2,

        /// <summary>Numbers carried on in a series, 4.</summary>
        GenerateSeries = 4,

        /// <summary>The content alone, 8, winning over a series.</summary>
        CopyContent = 8,

        /// <summary>The format alone, 16: the look and the kind of value.</summary>
        CopyFormat = 16,

        /// <summary>A locked content written over, 32: no cell is locked as the library reads a file.</summary>
        OverwriteReadOnlyContent = 32,

        /// <summary>A locked format written over, 64: no cell is locked as the library reads a file.</summary>
        OverwriteReadOnlyFormat = 64,
    }
}
