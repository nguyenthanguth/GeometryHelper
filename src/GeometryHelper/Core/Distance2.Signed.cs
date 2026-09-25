using System;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// How far a point stands from a closed shape, and on which side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DistanceTo(GeoPolygon2, GeoPoint2)"/> reads a closed shape as a filled region, so a point
    /// anywhere inside one is nought away and how far in it sits cannot be recovered from the answer.
    /// <c>SignedDistanceTo</c> keeps that depth: the magnitude is the distance to the **boundary**, whichever
    /// side of it the point is on, and the sign says which side.
    /// </para>
    /// <para>
    /// The rule is tied to <see cref="Containment2.Locate(GeoPolygon2, GeoPoint2)"/> and is the whole of the
    /// definition: <b>negative</b> where <c>Locate</c> answers <see cref="PointLocation.Inside"/>,
    /// <b>nought</b> where it answers <see cref="PointLocation.OnSide"/>, and <b>positive</b> where it
    /// answers <see cref="PointLocation.OutSide"/>. So
    /// <c>Math.Abs(shape.SignedDistanceTo(point))</c> is the distance out to the outline, and
    /// <c>shape.SignedDistanceTo(point) &lt; 0.0</c> says the same as <c>Contains</c> does, bar the boundary.
    /// </para>
    /// <para>
    /// Within the tolerance band around the boundary the sign is not worth reading, because the answer there
    /// is nought either way and which side of nought it falls on turns on rounding. That is what a tolerance
    /// means everywhere else in this library too.
    /// </para>
    /// <para>
    /// The library already carried this idea for a flat surface, in
    /// <see cref="GeoPlane3.SignedDistanceTo(GeoPoint3)"/>, and names it the same way it names
    /// <see cref="GeoPolygon2.SignedArea"/> beside <see cref="GeoPolygon2.Area"/>.
    /// </para>
    /// </remarks>
    public static partial class Distance2
    {
        /// <summary>
        /// Calculates the distance from a circle to a point, negative for a point inside it.
        /// </summary>
        public static double SignedDistanceTo(GeoCircle2 circle, GeoPoint2 point) => SignedDistanceTo(circle, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from a circle to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The distance out to the circumference, negative when the point lies within it.</returns>
        public static double SignedDistanceTo(GeoCircle2 circle, GeoPoint2 point, Tolerance tolerance)
        {
            return Signed(
                point.DistanceTo(Projection2.ProjectToCircle(circle, point, tolerance)),
                Containment2.Locate(circle, point, tolerance));
        }

        /// <summary>
        /// Calculates the distance from a rectangle to a point, negative for a point inside it.
        /// </summary>
        public static double SignedDistanceTo(GeoRectangle2 rect, GeoPoint2 point) => SignedDistanceTo(rect, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from a rectangle to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        /// <returns>The distance out to the nearest side, negative when the point lies within the rectangle.</returns>
        public static double SignedDistanceTo(GeoRectangle2 rect, GeoPoint2 point, Tolerance tolerance)
        {
            return Signed(
                point.DistanceTo(Projection2.ProjectToRectangle(rect, point)),
                Containment2.Locate(rect, point, tolerance));
        }

        /// <summary>
        /// Calculates the distance from a polygon to a point, negative for a point inside it.
        /// </summary>
        public static double SignedDistanceTo(GeoPolygon2 poly, GeoPoint2 point) => SignedDistanceTo(poly, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from a polygon to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        /// <returns>The distance out to the nearest edge, negative when the point lies within the polygon.</returns>
        /// <remarks>
        /// A polygon that is not simple is read under the even-odd rule, the same as everywhere else, so a
        /// lobe doubled back over the rest of the shape counts as outside and takes a positive sign.
        /// </remarks>
        public static double SignedDistanceTo(GeoPolygon2 poly, GeoPoint2 point, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return Signed(
                point.DistanceTo(Projection2.ProjectToPolygon(poly, point, tolerance)),
                Containment2.Locate(poly, point, tolerance));
        }

        /// <summary>
        /// Calculates the distance from a loop that may curve to a point, negative for a point inside it.
        /// </summary>
        public static double SignedDistanceTo(GeoPolygonArc2 loop, GeoPoint2 point) => SignedDistanceTo(loop, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from a loop that may curve to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        /// <returns>The distance out to the nearest edge, measured on the arcs, negative when the point lies within the loop.</returns>
        public static double SignedDistanceTo(GeoPolygonArc2 loop, GeoPoint2 point, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Signed(
                point.DistanceTo(Projection2.ProjectToPolygonArc(loop, point, tolerance)),
                Containment2.Locate(loop, point, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from a face to a point.
        /// </summary>
        public static double DistanceTo(GeoFace2 face, GeoPoint2 point) => DistanceTo(face, point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a face to a point, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Nought for a point on the material, otherwise the distance to the nearest edge of the face.</returns>
        /// <remarks>
        /// A face is a filled region with holes in it, so this reads it the way the rest of the class reads a
        /// polygon: a point on the material is nought away. A point in a hole is off the material, and is
        /// measured to the rim it sits in rather than out to the outline.
        /// </remarks>
        public static double DistanceTo(GeoFace2 face, GeoPoint2 point, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            if (Containment2.Contains(face, point, tolerance))
            {
                return 0.0;
            }

            return Math.Abs(SignedDistanceTo(face, point, tolerance));
        }

        /// <summary>
        /// Calculates the distance from a face to a point, negative for a point on the material.
        /// </summary>
        public static double SignedDistanceTo(GeoFace2 face, GeoPoint2 point) => SignedDistanceTo(face, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from a face to a point, negative for a point on the material, within a tolerance.
        /// </summary>
        /// <returns>The distance out to the nearest edge, negative when the point lies on the material.</returns>
        /// <remarks>
        /// The boundary of a face is its outline and the rim of every hole in it, so a point sitting in a
        /// hole is outside the face and is measured to the rim it sits in rather than out to the outline.
        /// </remarks>
        public static double SignedDistanceTo(GeoFace2 face, GeoPoint2 point, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            double reach = point.DistanceTo(Projection2.ProjectToPolygon(face.Boundary, point, tolerance));

            foreach (GeoPolygon2 hole in face.Holes)
            {
                reach = Math.Min(reach, point.DistanceTo(Projection2.ProjectToPolygon(hole, point, tolerance)));
            }

            return Signed(reach, Containment2.Locate(face, point, tolerance));
        }

        /// <summary>
        /// Turns a distance to a boundary into a signed one, by where the point sits.
        /// </summary>
        /// <remarks>
        /// Only <see cref="PointLocation.Inside"/> turns the sign over. A point the tolerance calls
        /// <see cref="PointLocation.OnSide"/> keeps a positive sign, which costs nothing, because the
        /// distance it is carrying is nought to within that same tolerance.
        /// </remarks>
        private static double Signed(double reach, PointLocation where)
        {
            return where == PointLocation.Inside ? -reach : reach;
        }
    }
}
