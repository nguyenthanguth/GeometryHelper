using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an infinite line is from another shape in space.
    /// </summary>
    /// <remarks>
    /// A closed shape is read as filled, as <see cref="GeometryHelper.Core.Distance3"/> reads it: a line through a solid is
    /// nought from it. The line through (-50, 150, 10) along X passes 50 beside the box 100 by 100 by 20 with a corner at the
    /// origin. The curved shapes (an arc, a circle, an ellipse, an edge and the curved chains) are not measured, as they are not
    /// for a ray or a segment: the distance from a curve in space to anything but a point has no closed form, and
    /// <c>ToPolyline3(chordTolerance)</c> is the way to ask for it. They are asked whether they touch and where they cross.
    /// </remarks>
    public readonly partial struct GeoXLine3
    {
        /// <summary>
        /// Calculates the shortest distance from this infinite line to a point, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a point, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// From the line through the origin along X, the points (50, 40, 30) and (-30, 40, 30) are both 50 away: there is no
        /// start to stop the foot at.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => Distance3.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a line segment, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine3 line) => DistanceTo(line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a line segment, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a ray, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay3 ray) => DistanceTo(ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a ray, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The line through the origin along X and the ray from (50, 30, 40) along Y are 50 apart, the ray coming nearest the
        /// line at its origin; the ray from (50, 30, 40) along -Y reaches the common perpendicular, 40 long, and the ray from
        /// (50, 30, 0) along -Y crosses the line, nought away.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(this, ray, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to another infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to another infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// Two lines in space are skew unless they meet: the line through the origin along X and the one through (0, 0, 30)
        /// along Y are 30 apart, the length of their common perpendicular, and the line through (0, 0, 30) along -X is 30 from
        /// the first, whichever way it runs. Nought where they cross.
        /// </remarks>
        public double DistanceTo(GeoXLine3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a plane, using the default tolerance.
        /// </summary>
        /// <returns>Nought where the line starts within the planar tolerance of the plane or crosses it where a double can place the crossing, otherwise the height of its origin over it.</returns>
        public double DistanceTo(GeoPlane3 plane) => DistanceTo(plane, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a plane, within a tolerance.
        /// </summary>
        /// <returns>Nought where the line starts within the planar tolerance of the plane or crosses it where a double can place the crossing, otherwise the height of its origin over it.</returns>
        /// <remarks>
        /// A line that runs along the floor at a height of 7 is 7 from it; tilted by a ten-thousandth of a radian it crosses the
        /// floor 70 000 along and is nought from it. Where a double cannot place the crossing it is read as level: the line
        /// through (0, 0, 100) along (1, 0, -1E-11) is 100 from the floor, and its ray, which reaches it, nought. A line that
        /// starts within the planar tolerance of the plane is nought from it, as a ray is.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(this, plane, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a triangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 triangle) => DistanceTo(triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a triangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a polygon, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => DistanceTo(polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a polygon, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The polygon counts as a filled surface: the line through (50, 50, 10) along Z pierces the square 100 by 100 in the
        /// floor and is nought from it, and the one through (150, 50, 10) along Z is 50 from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a face, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => DistanceTo(face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a face, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A hole is not material, so the line through (45, 50, 10) along Z, through the hole 40 by 40 in the middle of the square
        /// 100 by 100 in the floor, is 15 from the face, the distance to the rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(this, face, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a solid, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public double DistanceTo(GeoSolid3 solid) => DistanceTo(solid, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a solid, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A line through the body is nought from it: the line through (-500, 50, 10) along X runs into the box 100 by 100 by 20
        /// with a corner at the origin, and the line through (-50, 150, 10) along X passes 50 from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(this, solid, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoAabb3 box) => DistanceTo(box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an axis-aligned box, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an oriented box, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoObb3 box) => DistanceTo(box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to an oriented box, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline) => DistanceTo(polyline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a polyline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);
    }
}
