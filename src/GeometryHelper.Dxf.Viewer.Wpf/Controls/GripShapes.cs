using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The sizes of the grips the canvas draws at a grip size (AutoCAD's GRIPSIZE), in device-independent pixels. The square
    /// of a plain grip is measured from AutoCAD 2021 at 96 DPI: 15 across at the default size of 5. The bar of a polyline's
    /// segment middle is 22 by 6 there; AutoCAD does not say how it grows, so it keeps those proportions.
    /// </summary>
    internal static class GripShapes
    {
        /// <summary>The side of a plain grip's square at a grip size: <c>2 × size + 5</c>, 7 at 1, 15 at 5, 25 at 10.</summary>
        internal static double Square(int gripSize) => 2.0 * gripSize + 5.0;

        /// <summary>
        /// The long side of the bar at a grip size: 22 at 5 (the square's side times 22/15, rounded), 10 at 1, 31 at 8, 43 at 12.
        /// </summary>
        internal static double BarLength(int gripSize) =>
            Math.Round(Square(gripSize) * 22.0 / 15.0, MidpointRounding.AwayFromZero);

        /// <summary>
        /// The short side of the bar at a grip size: 6 at 5 (the square's side times 6/15, rounded), at least 3; 4 at 3, 8 at 8.
        /// </summary>
        internal static double BarThickness(int gripSize) =>
            Math.Max(3.0, Math.Round(Square(gripSize) * 6.0 / 15.0, MidpointRounding.AwayFromZero));
    }
}
