using GeometryHelper.Geometry;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// One polygon the packing has placed: which it is, which sheet and block it went with, and the polygon moved.
    /// </summary>
    internal readonly struct PolygonSpot
    {
        internal PolygonSpot(int group, int member, int sheet, int cluster, bool kept, GeoPolygon2 polygon)
        {
            Group = group;
            Member = member;
            Sheet = sheet;
            Cluster = cluster;
            Kept = kept;
            Polygon = polygon;
            Box = Footprint.Of(polygon);
        }

        /// <summary>Gets which group of the input the polygon belongs to.</summary>
        internal int Group { get; }

        /// <summary>Gets which polygon of the group it is.</summary>
        internal int Member { get; }

        /// <summary>Gets which sheet it went on.</summary>
        internal int Sheet { get; }

        /// <summary>Gets which block it went with, counted over the whole packing.</summary>
        internal int Cluster { get; }

        /// <summary>Gets whether its block keeps its polygons as they stood, so that they may overlap.</summary>
        internal bool Kept { get; }

        /// <summary>Gets the polygon moved.</summary>
        internal GeoPolygon2 Polygon { get; }

        /// <summary>Gets the upright box round the polygon moved.</summary>
        internal Footprint Box { get; }
    }
}
