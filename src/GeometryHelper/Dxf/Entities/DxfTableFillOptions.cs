using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How <see cref="DxfTable.Fill(DxfCellRange, DxfCellRange, DxfTableFillOptions)"/> fills cells from others, as AutoCAD's
    /// <c>TableFillOptions</c> flags it: down the columns, or along the rows with <see cref="Row"/>; the cells copied, or their
    /// numbers carried on in a series with <see cref="GenerateSeries"/>.
    /// </summary>
    [Flags]
    public enum DxfTableFillOptions
    {
        /// <summary>Down the columns, the content and the format copied, 0.</summary>
        None = 0,

        /// <summary>Along the rows, 1.</summary>
        Row = 1,

        /// <summary>The series the other way, 2: 10 and 20 filled down to 10, 0, -10 from the source's end.</summary>
        Reverse = 2,

        /// <summary>Numbers carried on in a series, 4.</summary>
        GenerateSeries = 4,

        /// <summary>The content alone, 8.</summary>
        CopyContent = 8,

        /// <summary>The format alone, 16.</summary>
        CopyFormat = 16,

        /// <summary>A locked content written over, 32: no cell is locked as the library reads a file.</summary>
        OverwriteReadOnlyContent = 32,

        /// <summary>A locked format written over, 64: no cell is locked as the library reads a file.</summary>
        OverwriteReadOnlyFormat = 64,
    }
}
