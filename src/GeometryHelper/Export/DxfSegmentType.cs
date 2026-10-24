namespace GeometryHelper.Export
{
    /// <summary>
    /// What a segment of a <see cref="DxfPolyline"/> is, as AutoCAD's <c>SegmentType</c> says: the segment that leaves the
    /// vertex of an index.
    /// </summary>
    public enum DxfSegmentType
    {
        /// <summary>A straight line from the vertex to the next.</summary>
        Line = 0,

        /// <summary>An arc from the vertex to the next, the vertex having a bulge.</summary>
        Arc = 1,

        /// <summary>A segment whose ends are the same point within the point tolerance.</summary>
        Coincident = 2,

        /// <summary>The one vertex of a polyline that has no other.</summary>
        Point = 3,

        /// <summary>No segment: the last vertex of an open polyline leaves none.</summary>
        Empty = 4,
    }
}
