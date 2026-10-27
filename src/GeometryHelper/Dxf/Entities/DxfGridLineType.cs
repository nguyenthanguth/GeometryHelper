using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The lines of a table's grid, as AutoCAD's <c>GridLineType</c> flags them: of a cell, of a block of cells or of the rows
    /// of a kind, the top and bottom lines, the left and right ones, and those inside between their rows and their columns;
    /// <see cref="OuterGridLines"/> is the four round them, 45.
    /// </summary>
    [Flags]
    public enum DxfGridLineType
    {
        /// <summary>None, 0.</summary>
        InvalidGridLine = 0,

        /// <summary>The line along the top, 1.</summary>
        HorizontalTop = 1,

        /// <summary>The lines between the rows, 2.</summary>
        HorizontalInside = 2,

        /// <summary>The line along the bottom, 4.</summary>
        HorizontalBottom = 4,

        /// <summary>The line down the left, 8.</summary>
        VerticalLeft = 8,

        /// <summary>The lines between the columns, 16.</summary>
        VerticalInside = 16,

        /// <summary>The line down the right, 32.</summary>
        VerticalRight = 32,

        /// <summary>The lines across, top, inside and bottom, 7.</summary>
        HorizontalGridLines = HorizontalTop | HorizontalInside | HorizontalBottom,

        /// <summary>The lines down, left, inside and right, 56.</summary>
        VerticalGridLines = VerticalLeft | VerticalInside | VerticalRight,

        /// <summary>The four lines round, 45.</summary>
        OuterGridLines = HorizontalTop | HorizontalBottom | VerticalLeft | VerticalRight,

        /// <summary>The lines inside, 18.</summary>
        InnerGridLines = HorizontalInside | VerticalInside,

        /// <summary>Every line, 63.</summary>
        AllGridLines = HorizontalGridLines | VerticalGridLines,
    }
}
