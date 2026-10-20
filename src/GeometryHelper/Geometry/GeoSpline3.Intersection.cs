using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a spline crosses another shape in space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The crossings are those of the surface or the outline of a shape, in order along the spline from its start: a flat shape is
    /// pierced where the spline passes through it as well as crossed at its outline, a disc, a circle or an ellipse, is crossed only at
    /// its rim, a body only at its skin, and a curved loop at its outline. In space two curves seldom meet, so a place is where the distance of
    /// the spline from the shape is least within the point tolerance, as it is in the plane: a crossing is a place where that distance comes to
    /// nought, and a spline that passes a ten-thousandth of a millimetre from a line is at a place as a spline that crosses it is. A touch
    /// is told from a near miss by the tolerance alone: the line 0.0005 over the top of the ring of radius 50 touches it at (0, 50, 0) and
    /// the line 0.002 over it touches nothing. A stationary point of the distance that is a greatest is not a place, nor is a point on a
    /// slope, and one that is a least with no curvature, a spline over a line that is tangent to it and a thousandth above its plane, or
    /// the cubic y = x³ / 100 under the line along x half a thousandth above its plane, is a place all the same (the distance has risen by
    /// more than the digits can tell a thousandth, a hundredth or a tenth of the size of the spline away, inside the piece, and has fallen
    /// nowhere on the way); a least that curves up is one only where the distance falls neither way. Places no further apart than the point
    /// tolerance, one from the next, are one place, as are two near touches next to each other along the spline within ten times that with
    /// no point between them farther from the shape than at either by more than four snaps (the snap is 1E-13 of the size of the numbers);
    /// two crossings are one place only when they are within the tolerance of each other, and a least of the distance within the tolerance
    /// of a crossing is left out, so that it does not join two.
    /// </para>
    /// <para>
    /// A segment, a ray or a line without ends is measured by the two signed parts of the spline's offset square to it, and a piece of the
    /// spline in a plane through the line by its signed offset in that plane, as a spline and a line of the plane are: a crossing at a
    /// millionth of a radian is a crossing, nought from the line, and the cubic y = x³ / 100 crosses the line y = 0.0001 once, at x = 0.21544,
    /// its inflection, where the offset stops and goes on, being no place. A plane is measured by the signed offset, so a dip of the spline a
    /// few snaps below it between two crossings is a greatest of the distance and no place.
    /// </para>
    /// <para>
    /// Where the spline lies along the shape for a length, to rounding, no place is given along the stretch: a spline in a plane crosses
    /// the plane nowhere, and one on a ball crosses its surface nowhere. Against an arc, a circle, an ellipse or another spline, the same is
    /// so where the two stay within the point tolerance of each other, level, for a quarter of the size of the smaller; see
    /// <see cref="DistanceTo(GeoSpline3)"/>. A crossing inside such a stretch is still a place: two splines in one plane, to the snap, are
    /// worked out in that plane, where the offset of the one from the other has a sign, and give the places the plane gives; an arc, a circle,
    /// an ellipse, an edge or a chain of edges in the plane of the spline is laid flat with it the same way, so a circle spline of radius 100
    /// moved (0.0003, 0.0005) along its plane crosses its circle three times, as in the plane. Two curves that share no plane can still cross,
    /// as a helix crosses a straight spline along its cylinder once a turn, and inside a stretch along each other a place is a least of the
    /// distance where the two would cross if brought into one plane: a polyline spline and its copy moved 0.0005 along its plane cross at three
    /// corners, and lifted 0.0002 off the plane as well they come nearest, 0.0002 apart, at the same three.
    /// </para>
    /// </remarks>
    public sealed partial class GeoSpline3
    {
        /// <summary>
        /// Gets the points where this spline crosses a line segment, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoLine3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, line), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a line segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections) => TryIntersectWith(line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses a ray, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoRay3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => GetIntersections(ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, ray), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections) => TryIntersectWith(ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ray, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoXLine3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, xline), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections) => TryIntersectWith(xline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(xline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses a plane, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPlane3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => GetIntersections(plane, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, plane), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a plane, using the default tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections) => TryIntersectWith(plane, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(plane, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses a polyline, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPolyline3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => GetIntersections(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, polyline), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections) => TryIntersectWith(polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline pierces or crosses the outline of a polygon, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPolygon3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => GetIntersections(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline pierces or crosses the outline of a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, polygon), tolerance);

        /// <summary>
        /// Tries to find where this spline pierces or crosses the outline of a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections) => TryIntersectWith(polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline pierces or crosses the outline of a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline pierces or crosses the outline of a triangle, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoTriangle3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => GetIntersections(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline pierces or crosses the outline of a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, triangle), tolerance);

        /// <summary>
        /// Tries to find where this spline pierces or crosses the outline of a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections) => TryIntersectWith(triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline pierces or crosses the outline of a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(triangle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline pierces or crosses the outline of a face, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoFace3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => GetIntersections(face, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline pierces or crosses the outline of a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, face), tolerance);

        /// <summary>
        /// Tries to find where this spline pierces or crosses the outline of a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections) => TryIntersectWith(face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline pierces or crosses the outline of a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(face, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the skin of an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoAabb3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => GetIntersections(box, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the skin of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, box), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the skin of an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections) => TryIntersectWith(box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the skin of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the skin of an oriented box, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoObb3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => GetIntersections(box, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the skin of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, box), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the skin of an oriented box, using the default tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections) => TryIntersectWith(box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the skin of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool TryIntersectWith(GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the skin of a solid, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoSolid3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => GetIntersections(solid, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the skin of a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place, and the openings of the solid the spline can reach within it are cut in.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, solid, tolerance, false), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the skin of a solid, using the default tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections) => TryIntersectWith(solid, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the skin of a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool TryIntersectWith(GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(solid, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the skin of a ball, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoSphere3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoSphere3 sphere) => GetIntersections(sphere, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the skin of a ball, within a tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoSphere3 sphere, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, sphere), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the skin of a ball, using the default tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoSphere3 sphere, out GeoPoint3[] intersections) => TryIntersectWith(sphere, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the skin of a ball, within a tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoSphere3 sphere, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(sphere, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses an arc, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoArc3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => GetIntersections(arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, arc), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the rim of a circle, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoCircle3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => GetIntersections(circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the rim of a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, circle), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the rim of a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the rim of a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the rim of an ellipse, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoEllipse3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoEllipse3 ellipse) => GetIntersections(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the rim of an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, ellipse), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the rim of an ellipse, using the default tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEllipse3 ellipse, out GeoPoint3[] intersections) => TryIntersectWith(ellipse, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the rim of an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEllipse3 ellipse, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses an edge, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoEdge3, Tolerance)"/>.</returns>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, edge), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections) => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(edge, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPolylineArc3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => GetIntersections(chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, chain), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections) => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where this spline crosses the outline of a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along the spline from its start; see <see cref="GetIntersections(GeoPolygonArc3, Tolerance)"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => GetIntersections(loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses the outline of a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, as a point of the spline, in order along it from its start: where it crosses, where it touches
        /// without crossing, and where an end of the spline or of the shape comes within the point tolerance of the other. Where the spline runs
        /// along the shape for a length it is not crossing it, and no point is given along that length.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance) => Spline3.GetIntersections(this, Spline3.Of(this, loop), tolerance);

        /// <summary>
        /// Tries to find where this spline crosses the outline of a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections) => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses the outline of a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, tolerance);

            return intersections.Length > 0;
        }
    }
}
