namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Where the content of a cell of a table stands in it, as AutoCAD's <c>CellAlignment</c> numbers it: group 170 of the cell.
    /// </summary>
    public enum DxfCellAlignment
    {
        /// <summary>The top left corner, 1.</summary>
        TopLeft = 1,

        /// <summary>The middle of the top, 2.</summary>
        TopCenter = 2,

        /// <summary>The top right corner, 3.</summary>
        TopRight = 3,

        /// <summary>The middle of the left side, 4.</summary>
        MiddleLeft = 4,

        /// <summary>The middle, 5.</summary>
        MiddleCenter = 5,

        /// <summary>The middle of the right side, 6.</summary>
        MiddleRight = 6,

        /// <summary>The bottom left corner, 7.</summary>
        BottomLeft = 7,

        /// <summary>The middle of the bottom, 8.</summary>
        BottomCenter = 8,

        /// <summary>The bottom right corner, 9.</summary>
        BottomRight = 9,
    }
}
