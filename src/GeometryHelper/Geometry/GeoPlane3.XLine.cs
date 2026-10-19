using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A plane asked about an infinite line: whether they touch, how far apart they are, where they cross and where the shortest
    /// segment between them runs; and the line two planes share.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        #region XLines

        /// <summary>
        /// Tries to find the line where this plane meets another plane, as an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="other">The other plane.</param>
        /// <param name="intersection">The line the two planes share, when the method returns true.</param>
        /// <remarks>
        /// <see cref="TryIntersectWith(GeoPlane3, out GeoRay3)"/> gives the same line as a ray, which names it and can only
        /// stand for it; this gives it as the infinite line it is.
        /// </remarks>
        public bool TryGetIntersectionLine(GeoPlane3 other, out GeoXLine3 intersection) => TryGetIntersectionLine(other, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find the line where this plane meets another plane, as an infinite line, within a tolerance.
        /// </summary>
        /// <param name="other">The other plane.</param>
        /// <param name="intersection">The line the two planes share, when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the planes meet in a line; otherwise, false.</returns>
        /// <remarks>
        /// Parallel planes are refused whether they are apart, in which case they never meet, or coincident, in which case they
        /// meet everywhere. The floor and the wall x = 100 meet in the line along Y through (100, 0, 0), whichever way round they
        /// are asked: its origin is the point of the line nearest the world origin, and its direction the cross product of the
        /// two normals, so asked of the wall first the line runs the other way.
        /// </remarks>
        public bool TryGetIntersectionLine(GeoPlane3 other, out GeoXLine3 intersection, Tolerance tolerance) => Intersection3.TryGetIntersectionLine(this, other, out intersection, tolerance);

        /// <summary>
        /// Determines whether this plane touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this plane touches an infinite line, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance) => Collision3.CollidesWith(xline, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this plane to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought where the line starts within the planar tolerance of the plane or crosses it where a double can place the crossing, otherwise the height of its origin over it.</returns>
        public double DistanceTo(GeoXLine3 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this plane to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought where the line starts within the planar tolerance of the plane or crosses it where a double can place the crossing, otherwise the height of its origin over it.</returns>
        public double DistanceTo(GeoXLine3 xline, Tolerance tolerance) => Distance3.DistanceTo(xline, this, tolerance);

        /// <summary>
        /// Gets the point where an infinite line crosses this plane, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the point where an infinite line crosses this plane, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance) => Intersection3.GetIntersections(xline, this, tolerance);

        /// <summary>
        /// Tries to find the point where an infinite line crosses this plane, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3 intersection) => TryIntersectWith(xline, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find the point where an infinite line crosses this plane, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(xline, this, out intersection, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this plane and landing on an infinite line, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this plane and landing on an infinite line, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline, Tolerance tolerance) => Projection3.GetShortestLineTo(xline, this, tolerance).Reverse();

        #endregion
    }
}
