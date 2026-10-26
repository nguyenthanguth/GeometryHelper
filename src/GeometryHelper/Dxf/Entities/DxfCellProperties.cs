using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The properties of a cell's content that a cell can hold of its own over its table style, as AutoCAD's
    /// <c>CellProperties</c> flags them; <see cref="DxfTable.GetOverrides(int, int, int)"/> gives those a cell of a file holds:
    /// the cell of flags 49 (177, or 91 from R2007 on) has <see cref="Alignment"/>, <see cref="TextStyle"/> and
    /// <see cref="TextHeight"/>.
    /// </summary>
    [Flags]
    public enum DxfCellProperties
    {
        /// <summary>None, 0.</summary>
        Invalid = 0,

        /// <summary>The kind of the value, 1.</summary>
        DataType = 1,

        /// <summary>The format of the value, 2.</summary>
        DataFormat = 2,

        /// <summary>The turn of the content, 4.</summary>
        Rotation = 4,

        /// <summary>The scale of a block, 8.</summary>
        Scale = 8,

        /// <summary>Where the content stands, 16.</summary>
        Alignment = 16,

        /// <summary>The colour of the content, 32.</summary>
        ContentColor = 32,

        /// <summary>The text style, 64.</summary>
        TextStyle = 64,

        /// <summary>The height of the text, 128.</summary>
        TextHeight = 128,

        /// <summary>Whether a block is fitted to the cell, 256.</summary>
        AutoScale = 256,

        /// <summary>The background: its colour, and whether it is filled, 512.</summary>
        BackgroundColor = 512,

        /// <summary>The margin on the left, 1 024.</summary>
        MarginLeft = 1024,

        /// <summary>The margin on the top, 2 048.</summary>
        MarginTop = 2048,

        /// <summary>The margin on the right, 4 096.</summary>
        MarginRight = 4096,

        /// <summary>The margin on the bottom, 8 192.</summary>
        MarginBottom = 8192,

        /// <summary>How several contents are laid out, 16 384.</summary>
        ContentLayout = 16384,

        /// <summary>Whether merging takes in every cell, 32 768.</summary>
        MergeAll = 32768,

        /// <summary>Rows that flow up, 65 536.</summary>
        FlowDirBtoT = 65536,

        /// <summary>The space between contents across, 131 072.</summary>
        MarginHorzSpacing = 131072,

        /// <summary>The space between contents down, 262 144.</summary>
        MarginVertSpacing = 262144,
    }
}
