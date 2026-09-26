using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a box is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoAabb3
    {
        /// <summary>
        /// Calculates the shortest distance from this box to a point. A point inside is at distance zero,
        /// and an empty box is infinitely far from everything.
        /// </summary>
        /// <remarks>
        /// This is what prunes a search through a tree of boxes: a box already farther away than the best
        /// answer so far cannot hold anything nearer, so the whole branch below it can be skipped without
        /// being looked at.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point)
        {
            if (IsEmpty)
            {
                return double.PositiveInfinity;
            }

            return GetClosestPointOnBoundary(point).DistanceTo(point);
        }

        /// <summary>
        /// Calculates the shortest distance between this box and another one. Boxes that overlap are at
        /// distance zero, and an empty box is infinitely far from everything.
        /// </summary>
        /// <remarks>
        /// The gap between two axis-aligned boxes separates on each axis independently, so the distance is
        /// the length of the vector of per-axis gaps. This is what prunes a traversal of two trees at once:
        /// a pair of boxes already farther apart than the best answer so far cannot hold a nearer pair.
        /// </remarks>
        public double DistanceTo(GeoAabb3 other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return double.PositiveInfinity;
            }

            double gapX = Math.Max(0.0, Math.Max(Min.X - other.Max.X, other.Min.X - Max.X));
            double gapY = Math.Max(0.0, Math.Max(Min.Y - other.Max.Y, other.Min.Y - Max.Y));
            double gapZ = Math.Max(0.0, Math.Max(Min.Z - other.Max.Z, other.Min.Z - Max.Z));

            return Math.Sqrt(gapX * gapX + gapY * gapY + gapZ * gapZ);
        }

        /// <summary>
        /// Gets the distance from this box to a polyline, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Core.Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this box to a polyline, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Core.Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this box to a solid, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Core.Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this box to a solid, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Core.Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Calculates the distance from this box to a point, negative for a point within it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the surface, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint3)"/>
        /// reads this box as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint3 point) => Core.Distance3.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this box to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint3 point, Tolerance tolerance) => Core.Distance3.SignedDistanceTo(this, point, tolerance);
    }
}
