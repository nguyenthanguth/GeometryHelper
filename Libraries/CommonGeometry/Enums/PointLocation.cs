namespace CommonGeometry.Enums
{
    /// <summary>
    /// Represents the spatial location of a point relative to a geometric shape or boundary.
    /// <para>
    /// What counts as <see cref="Inside"/> depends on the family the shape belongs to. A volume
    /// (<c>GeoObb3</c>, <c>GeoSolid3</c>) encloses a region of space. A region
    /// (<c>GeoPolygon2</c>, <c>GeoCircle2</c>, <c>GeoPolygon3</c>, <c>GeoFace3</c>) encloses an area;
    /// in space that means a point counts as inside only when it lies on the carrier plane as well as
    /// within the boundary. A curve (<c>GeoLine2</c>, <c>GeoPolyline2</c>, <c>GeoLine3</c>,
    /// <c>GeoPolyline3</c>, <c>GeoRay3</c>) encloses nothing and never reports <see cref="Inside"/>.
    /// </para>
    /// </summary>
    public enum PointLocation
    {
        /// <summary>
        /// The point lies strictly inside the interior of the shape.
        /// </summary>
        Inside,

        /// <summary>
        /// The point lies outside the shape.
        /// </summary>
        OutSide,

        /// <summary>
        /// The point lies on the boundary (or edge / surface) of the shape within tolerance.
        /// </summary>
        OnSide
    }
}
