using System;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an edge is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoEdge2
    {
        /// <summary>
        /// Gets the distance from the edge to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));

        /// <summary>
        /// Gets the distance from the edge to a straight segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => DistanceTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a straight segment, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Core.ArcChain2.Gap(this, line, tolerance);

        /// <summary>
        /// Gets the distance from the edge to another edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to another edge, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// Four cases, and each is answered exactly rather than by cutting the arcs up: segment to segment,
        /// segment to arc either way round, and arc to arc.
        /// </remarks>
        public double DistanceTo(GeoEdge2 other, Tolerance tolerance) => Core.ArcChain2.Gap(this, other, tolerance);

        /// <summary>
        /// Gets the distance from the edge to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => DistanceTo(circle, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a circle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance)
        {
            // The circle is the disc it bounds, whichever the edge is: a straight edge goes the way a segment
            // does, a curved one the way its arc does.
            return Core.ArcChain2.Straight(this, out GeoLine2 straight)
                ? Core.Distance2.DistanceTo(circle, straight, tolerance)
                : Core.Arc2.DistanceTo(ToArc(), circle, tolerance);
        }

        /// <summary>
        /// Gets the distance from the edge to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => DistanceTo(arc, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The arc is taken as it is rather than as an edge, so one sweeping a whole turn is measured
        /// against too.
        /// </remarks>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Core.ArcChain2.Gap(this, arc, tolerance);

        /// <summary>
        /// Gets the distance from this edge to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => (poly ?? throw new ArgumentNullException(nameof(poly))).DistanceTo(this);

        /// <summary>
        /// Gets the distance from this edge to a curved loop.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => DistanceTo(loop, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this edge to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => (loop ?? throw new ArgumentNullException(nameof(loop))).DistanceTo(this, tolerance);

        /// <summary>
        /// Gets the distance from this edge to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => (polyline ?? throw new ArgumentNullException(nameof(polyline))).DistanceTo(this);

        /// <summary>
        /// Gets the distance from this edge to a curved chain.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => DistanceTo(chain, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this edge to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => (chain ?? throw new ArgumentNullException(nameof(chain))).DistanceTo(this, tolerance);

        /// <summary>
        /// Gets the distance from this edge to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => rect.DistanceTo(this);

        #region Triangles

        /// <summary>
        /// Gets the distance from this edge to a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle2 triangle) => Triangle2.DistanceTo(triangle, this);

        #endregion

        /// <summary>
        /// Calculates the shortest distance from this edge to the material of a face: the same answer as
        /// <see cref="GeoFace2.DistanceTo(GeoEdge2)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this edge is nought from it where the two
        /// meet, and as far as the nearest rim where it lies wholly in a hole. The straight edge from (40, 50) to
        /// (60, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its
        /// middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face) => Face2.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance from this edge to the material of a face, within a tolerance: the same
        /// answer as <see cref="GeoFace2.DistanceTo(GeoEdge2, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this edge is nought from it where the two
        /// meet, and as far as the nearest rim where it lies wholly in a hole. The straight edge from (40, 50) to
        /// (60, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its
        /// middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Face2.DistanceTo(face, this, tolerance);

        #region Rays

        /// <summary>
        /// Gets the distance from the edge to a ray.
        /// </summary>
        public double DistanceTo(GeoRay2 ray) => DistanceTo(ray, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a ray, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRay2 ray, Tolerance tolerance)
        {
            return Core.ArcChain2.Straight(this, out GeoLine2 straight)
                ? Core.Distance2.DistanceTo(ray, straight, tolerance)
                : Core.Arc2.DistanceTo(ToArc(), ray, tolerance);
        }

        #endregion
    }
}
