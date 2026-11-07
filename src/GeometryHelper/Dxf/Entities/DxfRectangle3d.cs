using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The four corners of a frame in world coordinates, as AutoCAD's <c>Rectangle3d</c>: where an OLE object is shown
    /// (<see cref="DxfOle2Frame.Position3d"/>). The corners are as the drawing holds them, not checked to be square: a picture
    /// 83.58 by 30.67 laid along x from (15.72, 12.19, 0) has its upper left corner at (15.72, 42.85, 0) and its lower right at
    /// (99.29, 12.19, 0).
    /// </summary>
    public readonly struct DxfRectangle3d
    {
        /// <summary>
        /// Initializes the corners, in the order of AutoCAD's <c>Rectangle3d(upperLeft, upperRight, lowerLeft, lowerRight)</c>:
        /// the two upper ones, then the two lower ones, each from the left.
        /// </summary>
        /// <param name="upperLeft">The upper left corner.</param>
        /// <param name="upperRight">The upper right corner.</param>
        /// <param name="lowerLeft">The lower left corner.</param>
        /// <param name="lowerRight">The lower right corner.</param>
        public DxfRectangle3d(GeoPoint3 upperLeft, GeoPoint3 upperRight, GeoPoint3 lowerLeft, GeoPoint3 lowerRight)
        {
            UpperLeft = upperLeft;
            UpperRight = upperRight;
            LowerLeft = lowerLeft;
            LowerRight = lowerRight;
        }

        /// <summary>Gets the upper left corner, as AutoCAD's <c>UpperLeft</c>.</summary>
        public GeoPoint3 UpperLeft { get; }

        /// <summary>Gets the upper right corner, as AutoCAD's <c>UpperRight</c>.</summary>
        public GeoPoint3 UpperRight { get; }

        /// <summary>Gets the lower left corner, as AutoCAD's <c>LowerLeft</c>.</summary>
        public GeoPoint3 LowerLeft { get; }

        /// <summary>Gets the lower right corner, as AutoCAD's <c>LowerRight</c>.</summary>
        public GeoPoint3 LowerRight { get; }

        /// <summary>Gives the corners as a line for debugging, the upper two, then the lower two.</summary>
        /// <returns>The line, such as <c>(0, 30, 0), (80, 30, 0), (0, 0, 0), (80, 0, 0)</c>.</returns>
        public override string ToString() => UpperLeft + ", " + UpperRight + ", " + LowerLeft + ", " + LowerRight;
    }
}
