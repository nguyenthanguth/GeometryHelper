namespace GeometryHelper.Packing
{
    /// <summary>
    /// A point of a sheet, as <see cref="Sheet.PlaceCorner(SheetCorner, GeometryHelper.Geometry.GeoPoint2)"/> puts it
    /// where it is asked: one of the four corners, or the middle.
    /// </summary>
    public enum SheetCorner
    {
        /// <summary>The lower left corner, the sheet's <see cref="Sheet.Origin"/>.</summary>
        LowerLeft,

        /// <summary>The lower right corner.</summary>
        LowerRight,

        /// <summary>The upper left corner.</summary>
        UpperLeft,

        /// <summary>The upper right corner.</summary>
        UpperRight,

        /// <summary>The middle of the sheet.</summary>
        Center,
    }
}
