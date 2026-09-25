using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Measuring a chain in space against the shapes around it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A chain could be measured to a point and to nothing else, which left the way out of a curved chain
    /// leading nowhere: <see cref="GeoPolylineArc3.ToPolyline3(double)"/> hands back a straight chain
    /// following the arcs as closely as asked, and there was then nothing to ask it. These are the pairs that
    /// close that gap, and every one of them is exact for the straight chain it is given.
    /// </para>
    /// <para>
    /// So the way to measure a bent bar against anything is to say how closely it should be followed and then
    /// ask the ordinary question: <c>bar.ToPolyline3(0.1).DistanceTo(slab)</c>. The accuracy sits in the call
    /// where it can be seen, which is the same bargain <see cref="GeoCircle3"/> offers.
    /// </para>
    /// </remarks>
    public static partial class Distance3
    {
        /// <summary>
        /// Calculates the shortest distance between a chain and a line segment.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoLine3 line) => DistanceTo(polyline, line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a chain and a line segment, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoLine3 line, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double best = double.MaxValue;

            foreach (GeoLine3 edge in polyline.GetEdges())
            {
                best = Math.Min(best, DistanceTo(edge, line, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between two chains.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoPolyline3 other) => DistanceTo(polyline, other, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two chains, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoPolyline3 other, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (other == null) throw new ArgumentNullException(nameof(other));

            double best = double.MaxValue;

            foreach (GeoLine3 edge in other.GetEdges())
            {
                best = Math.Min(best, DistanceTo(polyline, edge, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a chain and a triangle.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoTriangle3 triangle) => DistanceTo(polyline, triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a chain and a triangle, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double best = double.MaxValue;

            foreach (GeoLine3 edge in polyline.GetEdges())
            {
                best = Math.Min(best, DistanceTo(edge, triangle, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a chain and a polygon.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoPolygon3 polygon) => DistanceTo(polyline, polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a chain and a polygon, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            double best = double.MaxValue;

            foreach (GeoTriangle3 piece in polygon.Triangulate())
            {
                best = Math.Min(best, DistanceTo(polyline, piece, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a chain and a plane.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoPlane3 plane) => DistanceTo(polyline, plane, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a chain and a plane, within a tolerance.
        /// </summary>
        /// <returns>Zero where the chain reaches the plane or crosses it, otherwise the gap.</returns>
        /// <remarks>
        /// A plane is flat and endless, so nothing needs measuring but the vertices: the nearest point of a
        /// straight chain to a plane is always one of them, and the chain crosses the plane exactly when two
        /// of them fall on opposite sides.
        /// </remarks>
        public static double DistanceTo(GeoPolyline3 polyline, GeoPlane3 plane, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double above = double.MinValue;
            double below = double.MaxValue;

            foreach (GeoPoint3 vertex in polyline.Vertices)
            {
                double reach = plane.SignedDistanceTo(vertex);

                above = Math.Max(above, reach);
                below = Math.Min(below, reach);
            }

            if (above >= -tolerance.EqualPoint && below <= tolerance.EqualPoint)
            {
                return 0.0;
            }

            return below > 0.0 ? below : -above;
        }

        /// <summary>
        /// Calculates the shortest distance between a chain and an oriented box.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoObb3 box) => DistanceTo(polyline, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a chain and an oriented box, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoObb3 box, Tolerance tolerance)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));

            return DistanceTo(box.ToSolid(), polyline, tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance between a chain and an axis-aligned box.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoAabb3 box) => DistanceTo(polyline, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a chain and an axis-aligned box, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoAabb3 box, Tolerance tolerance)
        {
            return DistanceTo(box.ToObb().ToSolid(), polyline, tolerance);
        }
    }
}
