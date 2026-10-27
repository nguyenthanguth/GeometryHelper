using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The properties of a line of a table's grid, as AutoCAD's <c>GridProperties</c> flags them; a file of these versions
    /// has groups for <see cref="LineWeight"/>, <see cref="Color"/> and <see cref="Visibility"/>, and none for the others.
    /// </summary>
    [Flags]
    public enum DxfGridProperties
    {
        /// <summary>None, 0.</summary>
        Invalid = 0,

        /// <summary>Whether the line is single or double, 1: no group of a file of these versions holds it.</summary>
        LineStyle = 1,

        /// <summary>The lineweight, 2.</summary>
        LineWeight = 2,

        /// <summary>The linetype, 4: no group of a file of these versions holds it.</summary>
        Linetype = 4,

        /// <summary>The colour, 8.</summary>
        Color = 8,

        /// <summary>Whether the line is drawn, 16.</summary>
        Visibility = 16,

        /// <summary>The spacing of a double line, 32: no group of a file of these versions holds it.</summary>
        DoubleLineSpacing = 32,
    }
}
