using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The four edges of a cell, as AutoCAD's <c>CellEdgeMasks</c> flags them, each with its own colour (69, 65, 66, 68),
    /// lineweight (279, 275, 276, 278) and showing (289, 285, 286, 288) among the cell's overrides.
    /// </summary>
    [Flags]
    public enum DxfCellEdgeMasks
    {
        /// <summary>The top edge, 1.</summary>
        TopMask = 1,

        /// <summary>The right edge, 2.</summary>
        RightMask = 2,

        /// <summary>The bottom edge, 4.</summary>
        BottomMask = 4,

        /// <summary>The left edge, 8.</summary>
        LeftMask = 8,
    }
}
