using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The drawn picture of a dimension, for code that makes or changes dimensions outside the commands (a Python tool in
    /// the app, a plugin, a script over a file). The app draws the picture of a dimension it is given; this does the same.
    /// </summary>
    public static class CadDimensionPicture
    {
        /// <summary>
        /// Draws the picture of a dimension anew from its points and the variables it reads now (its overrides, else its
        /// style's), as the DIM commands make it, and writes the measurement into it. A rotated dimension of (0, 0) to (100, 0)
        /// with the line through (100, 20) gets the lines, arrows and words "100" in its own block.
        /// </summary>
        /// <param name="dimension">The dimension, in a drawing, with a block of its own (the one it was made with).</param>
        /// <returns>
        /// True when the picture was drawn; false, and nothing changed, for a kind that has no picture here or a block that is
        /// not its own.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dimension"/> is null.</exception>
        public static bool Redraw(DxfDimension dimension)
        {
            if (dimension == null)
            {
                throw new ArgumentNullException(nameof(dimension));
            }

            return CadCommands.Redraw(dimension);
        }
    }
}
