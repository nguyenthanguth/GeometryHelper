using System;
using CommonGeometry;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for checking parallelism, perpendicularity and coplanarity between 3D
    /// entities.
    /// <para>
    /// Every method takes degenerate input to mean "no answer" and returns false for it. A zero-length
    /// vector has no direction, so it is neither parallel nor perpendicular to anything, and saying so
    /// keeps a caller from reading a confident true out of geometry that carries no information.
    /// </para>
    /// </summary>
    public static class Parallel3
    {
        #region Vector - Vector

        /// <summary>
        /// Checks whether two vectors are parallel or anti-parallel, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoVector3 v1, GeoVector3 v2) => IsParallel(v1, v2, Tolerance.Global);

        /// <summary>
        /// Checks whether two vectors are parallel or anti-parallel, within an angular tolerance.
        /// </summary>
        /// <param name="v1">The first vector.</param>
        /// <param name="v2">The second vector.</param>
        /// <param name="tolerance">The tolerance carrying the angular threshold.</param>
        /// <returns>true if the vectors lie along a common line within tolerance; otherwise, false.</returns>
        public static bool IsParallel(GeoVector3 v1, GeoVector3 v2, Tolerance tolerance)
        {
            if (!v1.TryGetNormal(out GeoVector3 u1, tolerance) || !v2.TryGetNormal(out GeoVector3 u2, tolerance))
            {
                return false;
            }

            // Acos loses precision exactly where this test lives: near parallel the dot product sits at
            // ~1 and Acos amplifies its rounding error. Atan2(|cross|, |dot|) is stable across the whole
            // range, which is the same reasoning GeoVector3.GetAngleTo documents. Taking the absolute
            // value of the dot folds the anti-parallel case onto the parallel one.
            double angle = Math.Atan2(u1.CrossProduct(u2).Length, Math.Abs(u1.DotProduct(u2)));

            return angle <= tolerance.EqualAngleRad;
        }

        /// <summary>
        /// Checks whether two vectors point the same way, using the default tolerance.
        /// </summary>
        public static bool IsCodirectional(GeoVector3 v1, GeoVector3 v2) => IsCodirectional(v1, v2, Tolerance.Global);

        /// <summary>
        /// Checks whether two vectors point the same way, within an angular tolerance.
        /// </summary>
        /// <remarks>
        /// This is <see cref="IsParallel(GeoVector3, GeoVector3, Tolerance)"/> with the anti-parallel case
        /// excluded, which is the question to ask when comparing surface normals or travel directions.
        /// </remarks>
        public static bool IsCodirectional(GeoVector3 v1, GeoVector3 v2, Tolerance tolerance)
        {
            if (!v1.TryGetNormal(out GeoVector3 u1, tolerance) || !v2.TryGetNormal(out GeoVector3 u2, tolerance))
            {
                return false;
            }

            double angle = Math.Atan2(u1.CrossProduct(u2).Length, u1.DotProduct(u2));

            return angle <= tolerance.EqualAngleRad;
        }

        /// <summary>
        /// Checks whether two vectors are perpendicular, using the default tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoVector3 v1, GeoVector3 v2) => IsPerpendicular(v1, v2, Tolerance.Global);

        /// <summary>
        /// Checks whether two vectors are perpendicular, within an angular tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoVector3 v1, GeoVector3 v2, Tolerance tolerance)
        {
            if (!v1.TryGetNormal(out GeoVector3 u1, tolerance) || !v2.TryGetNormal(out GeoVector3 u2, tolerance))
            {
                return false;
            }

            // For unit vectors the dot product is the cosine of the angle, so it equals the sine of the
            // deviation from a right angle. Comparing against the cached sine of the angular threshold
            // avoids an Acos per call inside the nested loops this sits in.
            return Math.Abs(u1.DotProduct(u2)) <= tolerance.EqualAngleSin;
        }

        /// <summary>
        /// Checks whether three vectors lie in a common plane through the origin, using the default tolerance.
        /// </summary>
        public static bool IsCoplanar(GeoVector3 v1, GeoVector3 v2, GeoVector3 v3) => IsCoplanar(v1, v2, v3, Tolerance.Global);

        /// <summary>
        /// Checks whether three vectors lie in a common plane through the origin, within an angular tolerance.
        /// </summary>
        /// <remarks>
        /// The test is the scalar triple product of the three normalized vectors, which is the volume of
        /// the unit parallelepiped they span and therefore zero exactly when they share a plane.
        /// Normalizing first is what makes the threshold an angle rather than a volume, so the answer does
        /// not change when the inputs are scaled.
        /// </remarks>
        public static bool IsCoplanar(GeoVector3 v1, GeoVector3 v2, GeoVector3 v3, Tolerance tolerance)
        {
            if (!v1.TryGetNormal(out GeoVector3 u1, tolerance) ||
                !v2.TryGetNormal(out GeoVector3 u2, tolerance) ||
                !v3.TryGetNormal(out GeoVector3 u3, tolerance))
            {
                return false;
            }

            return Math.Abs(u1.TripleProduct(u2, u3)) <= tolerance.EqualAngleSin;
        }

        #endregion

        #region Line - Line

        /// <summary>
        /// Checks whether two line segments are parallel, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoLine3 line1, GeoLine3 line2) => IsParallel(line1, line2, Tolerance.Global);

        /// <summary>
        /// Checks whether two line segments are parallel, within an angular tolerance.
        /// </summary>
        public static bool IsParallel(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
        {
            return IsParallel(line1.Direction, line2.Direction, tolerance);
        }

        /// <summary>
        /// Checks whether two line segments are perpendicular, using the default tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoLine3 line1, GeoLine3 line2) => IsPerpendicular(line1, line2, Tolerance.Global);

        /// <summary>
        /// Checks whether two line segments are perpendicular, within an angular tolerance.
        /// </summary>
        /// <remarks>
        /// Two segments in space can be perpendicular in direction without ever meeting. This reports on
        /// direction alone, as the name says; use <c>Intersection3</c> to ask whether they touch.
        /// </remarks>
        public static bool IsPerpendicular(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
        {
            return IsPerpendicular(line1.Direction, line2.Direction, tolerance);
        }

        /// <summary>
        /// Checks whether two line segments lie on a common plane, using the default tolerance.
        /// </summary>
        public static bool IsCoplanar(GeoLine3 line1, GeoLine3 line2) => IsCoplanar(line1, line2, Tolerance.Global);

        /// <summary>
        /// Checks whether two line segments lie on a common plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Parallel segments always share a plane and are reported as coplanar without further test.
        /// Otherwise the two directions span a plane, and the segments share it when the vector between
        /// their start points has no component along that plane's normal. The threshold is
        /// <see cref="Tolerance.EqualPlanar"/>, since what is measured is the gap between the two carrier
        /// lines, a distance.
        /// </remarks>
        public static bool IsCoplanar(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
        {
            if (!line1.Direction.TryGetNormal(out GeoVector3 u1, tolerance) ||
                !line2.Direction.TryGetNormal(out GeoVector3 u2, tolerance))
            {
                return false;
            }

            GeoVector3 cross = u1.CrossProduct(u2);

            if (!cross.TryGetNormal(out GeoVector3 normal, tolerance))
            {
                // The directions are parallel, so a plane containing both always exists.
                return true;
            }

            double gap = line1.StartPoint.GetVectorTo(line2.StartPoint).DotProduct(normal);

            return Math.Abs(gap) <= tolerance.EqualPlanar;
        }

        #endregion

        #region Line - Vector

        /// <summary>
        /// Checks whether a line segment runs parallel to a vector, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoLine3 line, GeoVector3 vector) => IsParallel(line, vector, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment runs parallel to a vector, within an angular tolerance.
        /// </summary>
        public static bool IsParallel(GeoLine3 line, GeoVector3 vector, Tolerance tolerance)
        {
            return IsParallel(line.Direction, vector, tolerance);
        }

        /// <summary>
        /// Checks whether a line segment is perpendicular to a vector, using the default tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoLine3 line, GeoVector3 vector) => IsPerpendicular(line, vector, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment is perpendicular to a vector, within an angular tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoLine3 line, GeoVector3 vector, Tolerance tolerance)
        {
            return IsPerpendicular(line.Direction, vector, tolerance);
        }

        #endregion

        #region Line - Plane

        /// <summary>
        /// Checks whether a line segment runs parallel to a plane, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoLine3 line, GeoPlane3 plane) => IsParallel(line, plane, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment runs parallel to a plane, within an angular tolerance.
        /// </summary>
        /// <remarks>
        /// A line is parallel to a plane when its direction is perpendicular to the plane normal, which
        /// is why the two words swap places between this method and its implementation. A line lying in
        /// the plane satisfies this too: parallelism here is about direction, not about separation.
        /// </remarks>
        public static bool IsParallel(GeoLine3 line, GeoPlane3 plane, Tolerance tolerance)
        {
            return IsPerpendicular(line.Direction, plane.Normal, tolerance);
        }

        /// <summary>
        /// Checks whether a line segment stands perpendicular to a plane, using the default tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoLine3 line, GeoPlane3 plane) => IsPerpendicular(line, plane, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment stands perpendicular to a plane, within an angular tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoLine3 line, GeoPlane3 plane, Tolerance tolerance)
        {
            return IsParallel(line.Direction, plane.Normal, tolerance);
        }

        #endregion

        #region Ray - Plane

        /// <summary>
        /// Checks whether a ray runs parallel to a plane, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoRay3 ray, GeoPlane3 plane) => IsParallel(ray, plane, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray runs parallel to a plane, within an angular tolerance.
        /// </summary>
        public static bool IsParallel(GeoRay3 ray, GeoPlane3 plane, Tolerance tolerance)
        {
            return IsPerpendicular(ray.Direction, plane.Normal, tolerance);
        }

        /// <summary>
        /// Checks whether a ray stands perpendicular to a plane, using the default tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoRay3 ray, GeoPlane3 plane) => IsPerpendicular(ray, plane, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray stands perpendicular to a plane, within an angular tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoRay3 ray, GeoPlane3 plane, Tolerance tolerance)
        {
            return IsParallel(ray.Direction, plane.Normal, tolerance);
        }

        #endregion

        #region Plane - Plane

        /// <summary>
        /// Checks whether two planes are parallel, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoPlane3 plane1, GeoPlane3 plane2) => IsParallel(plane1, plane2, Tolerance.Global);

        /// <summary>
        /// Checks whether two planes are parallel, within an angular tolerance.
        /// </summary>
        /// <remarks>
        /// Two planes facing opposite ways are still parallel, so this holds for a plane and its flip.
        /// It also holds for a plane and itself, and says nothing about how far apart they are.
        /// </remarks>
        public static bool IsParallel(GeoPlane3 plane1, GeoPlane3 plane2, Tolerance tolerance)
        {
            return IsParallel(plane1.Normal, plane2.Normal, tolerance);
        }

        /// <summary>
        /// Checks whether two planes meet at right angles, using the default tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoPlane3 plane1, GeoPlane3 plane2) => IsPerpendicular(plane1, plane2, Tolerance.Global);

        /// <summary>
        /// Checks whether two planes meet at right angles, within an angular tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoPlane3 plane1, GeoPlane3 plane2, Tolerance tolerance)
        {
            return IsPerpendicular(plane1.Normal, plane2.Normal, tolerance);
        }

        #endregion

        #region Planar regions

        /// <summary>
        /// Checks whether a polygon lies parallel to a plane, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoPolygon3 polygon, GeoPlane3 plane) => IsParallel(polygon, plane, Tolerance.Global);

        /// <summary>
        /// Checks whether a polygon lies parallel to a plane, within an angular tolerance.
        /// </summary>
        /// <remarks>
        /// A region lying in the plane satisfies this too: parallelism is about orientation, not about
        /// separation. Two regions facing opposite ways are still parallel.
        /// </remarks>
        public static bool IsParallel(GeoPolygon3 polygon, GeoPlane3 plane, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return IsParallel(polygon.Normal, plane.Normal, tolerance);
        }

        /// <summary>
        /// Checks whether a polygon stands perpendicular to a plane, using the default tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoPolygon3 polygon, GeoPlane3 plane) => IsPerpendicular(polygon, plane, Tolerance.Global);

        /// <summary>
        /// Checks whether a polygon stands perpendicular to a plane, within an angular tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoPolygon3 polygon, GeoPlane3 plane, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return IsPerpendicular(polygon.Normal, plane.Normal, tolerance);
        }

        /// <summary>
        /// Checks whether two polygons lie parallel to each other, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoPolygon3 polygon1, GeoPolygon3 polygon2) => IsParallel(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Checks whether two polygons lie parallel to each other, within an angular tolerance.
        /// </summary>
        public static bool IsParallel(GeoPolygon3 polygon1, GeoPolygon3 polygon2, Tolerance tolerance)
        {
            if (polygon1 == null)
            {
                throw new ArgumentNullException(nameof(polygon1));
            }

            if (polygon2 == null)
            {
                throw new ArgumentNullException(nameof(polygon2));
            }

            return IsParallel(polygon1.Normal, polygon2.Normal, tolerance);
        }

        /// <summary>
        /// Checks whether two polygons meet at right angles, using the default tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoPolygon3 polygon1, GeoPolygon3 polygon2) => IsPerpendicular(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Checks whether two polygons meet at right angles, within an angular tolerance.
        /// </summary>
        public static bool IsPerpendicular(GeoPolygon3 polygon1, GeoPolygon3 polygon2, Tolerance tolerance)
        {
            if (polygon1 == null)
            {
                throw new ArgumentNullException(nameof(polygon1));
            }

            if (polygon2 == null)
            {
                throw new ArgumentNullException(nameof(polygon2));
            }

            return IsPerpendicular(polygon1.Normal, polygon2.Normal, tolerance);
        }

        /// <summary>
        /// Checks whether two polygons lie on one common plane, using the default tolerance.
        /// </summary>
        public static bool IsCoplanar(GeoPolygon3 polygon1, GeoPolygon3 polygon2) => IsCoplanar(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Checks whether two polygons lie on one common plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Being parallel is not enough: two parallel regions a metre apart share no plane. The second has
        /// to lie on the plane of the first as well, which is the distance the planar threshold measures.
        /// A region and one facing the other way are coplanar, since they occupy the same flat place.
        /// </remarks>
        public static bool IsCoplanar(GeoPolygon3 polygon1, GeoPolygon3 polygon2, Tolerance tolerance)
        {
            if (!IsParallel(polygon1, polygon2, tolerance))
            {
                return false;
            }

            return polygon1.GetPlane().ContainsAll(polygon2.Vertices, tolerance);
        }

        /// <summary>
        /// Checks whether a face lies parallel to a plane, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoFace3 face, GeoPlane3 plane) => IsParallel(face, plane, Tolerance.Global);

        /// <summary>
        /// Checks whether a face lies parallel to a plane, within an angular tolerance.
        /// </summary>
        public static bool IsParallel(GeoFace3 face, GeoPlane3 plane, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return IsParallel(face.Boundary, plane, tolerance);
        }

        /// <summary>
        /// Checks whether two faces lie on one common plane, using the default tolerance.
        /// </summary>
        public static bool IsCoplanar(GeoFace3 face1, GeoFace3 face2) => IsCoplanar(face1, face2, Tolerance.Global);

        /// <summary>
        /// Checks whether two faces lie on one common plane, within a tolerance.
        /// </summary>
        public static bool IsCoplanar(GeoFace3 face1, GeoFace3 face2, Tolerance tolerance)
        {
            if (face1 == null)
            {
                throw new ArgumentNullException(nameof(face1));
            }

            if (face2 == null)
            {
                throw new ArgumentNullException(nameof(face2));
            }

            return IsCoplanar(face1.Boundary, face2.Boundary, tolerance);
        }

        #endregion
    }
}
