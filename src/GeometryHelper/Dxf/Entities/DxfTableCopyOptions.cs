using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How cells are copied from one table into another, as AutoCAD's <c>TableCopyOptions</c> flags them;
    /// <see cref="DxfTable.CopyFrom(DxfTable, DxfTableCopyOptions, DxfCellRange, DxfCellRange)"/> takes <see cref="None"/>,
    /// <see cref="ExpandOrContractTable"/>, <see cref="SkipContent"/>, <see cref="SkipFormat"/>, <see cref="SkipMerges"/>,
    /// <see cref="SkipField"/>, <see cref="SkipFormula"/>, <see cref="SkipDataLink"/>, <see cref="TableCopyRowHeight"/> and
    /// <see cref="TableCopyColumnWidth"/>, and refuses the others, which ask for what a
    /// file of these versions has no groups for (cell styles, cell states) or for rules of AutoCAD's own.
    /// </summary>
    [Flags]
    public enum DxfTableCopyOptions
    {
        /// <summary>The content, the format and the merges of the cells, 0.</summary>
        None = 0,

        /// <summary>The table grown to hold what is copied, 1: the library grows a table, and never shrinks one.</summary>
        ExpandOrContractTable = 1,

        /// <summary>The content left out, 2.</summary>
        SkipContent = 2,

        /// <summary>The values left out, 4.</summary>
        SkipValue = 4,

        /// <summary>The fields left out, 8.</summary>
        SkipField = 8,

        /// <summary>The formulas left out, 16.</summary>
        SkipFormula = 16,

        /// <summary>The blocks left out, 32.</summary>
        SkipBlock = 32,

        /// <summary>The data links left out, 64.</summary>
        SkipDataLink = 64,

        /// <summary>The cells of the title and the header left out, 128.</summary>
        SkipLabelCell = 128,

        /// <summary>The cells of data left out, 256.</summary>
        SkipDataCell = 256,

        /// <summary>The format, the cell's own look, left out, 512.</summary>
        SkipFormat = 512,

        /// <summary>The cell styles left out, 1024.</summary>
        SkipCellStyle = 1024,

        /// <summary>The format of the cell style turned into overrides, 2048.</summary>
        ConvertFormatToOverrides = 2048,

        /// <summary>The cell states left out, 4096.</summary>
        SkipCellState = 4096,

        /// <summary>The format of the content left out, 8192.</summary>
        SkipContentFormat = 8192,

        /// <summary>A format of the content unlike the target's left out, 16384.</summary>
        SkipDissimilarContentFormat = 16384,

        /// <summary>The heights and widths left out, 32768.</summary>
        SkipGeometry = 32768,

        /// <summary>The merges left out, 65536.</summary>
        SkipMerges = 65536,

        /// <summary>The target filled by the source repeated, 131072.</summary>
        FillTarget = 131072,

        /// <summary>A locked content written over, 262144.</summary>
        OverwriteReadOnlyContent = 262144,

        /// <summary>A locked format written over, 524288.</summary>
        OverwriteReadOnlyFormat = 524288,

        /// <summary>A content changed since its data link was updated written over, 1048576.</summary>
        OverwriteContentModifiedAfterUpdate = 1048576,

        /// <summary>A format changed since its data link was updated written over, 2097152.</summary>
        OverwriteFormatModifiedAfterUpdate = 2097152,

        /// <summary>Only a content changed since its data link was updated, 4194304.</summary>
        OnlyContentModifiedAfterUpdate = 4194304,

        /// <summary>Only a format changed since its data link was updated, 8388608.</summary>
        OnlyFormatModifiedAfterUpdate = 8388608,

        /// <summary>The heights of the rows copied, 16777216.</summary>
        TableCopyRowHeight = 16777216,

        /// <summary>The widths of the columns copied, 33554432.</summary>
        TableCopyColumnWidth = 33554432,
    }
}
