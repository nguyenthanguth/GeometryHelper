using GeometryHelper.Core;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a spline is from a point, and whether a point lies on it, and how far it is from every other shape in space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A shape is read as the other curves in space read it: a flat shape, a triangle, a polygon or a face, is filled, and a spline that
    /// pierces it, or lies in it, is nought from it; a circle or an ellipse is the disc it is, as its own distance to a point reads it; a
    /// box, an oriented box, a solid or a ball is material, so a spline inside it is nought from it; a plane is a surface without ends, not
    /// a half of space; and a curved chain and a curved loop are the paths they are, a loop not filled. The spline itself is a curve, with no
    /// inside. The distance is nought only where the two truly meet, a crossing or a touch found to rounding, and otherwise the gap,
    /// measured exactly: it is never read through the tolerance, and the planar tolerance is not read at all.
    /// </para>
    /// <para>
    /// The pairs with a segment, a ray or a line without ends are solved as polynomials, to rounding: the part of a span's offset that
    /// stands square to the line is two signed polynomials in Bernstein form, both nought where the span meets the line, and a span in a
    /// plane through the line has its signed offset in that plane, as in the plane, so a crossing however shallow is nought; with a plane
    /// the signed offset of the span is one, and with a ball the squared distance from the centre less the squared radius; a flat shape is
    /// its plane, taken where the foot of the place stands over the shape, and its outline; a body is its faces and the test of its
    /// material. A place where the spline comes nearest a line out of such a plane is a stationary point of the squared offset, brought to
    /// its last digit by Newton's method on the distance, or where the distance has no curvature by a golden section down its slope. With an
    /// arc, a circle, an ellipse or another spline the nearest approach is found by cutting both in halves while their hulls are near
    /// enough to matter, nearest bound first, and bringing the nearest pair to rounding by Newton's method, as it is in the plane
    /// (see <see cref="GeoSpline2.DistanceTo(GeoSpline2, Tolerance)"/>); the hair that bounds the doubt is a billionth of the size of the
    /// shapes, and never more than a thousandth of the distance itself. Two splines in one plane, to a snap, are measured in that plane.
    /// </para>
    /// </remarks>
    public sealed partial class GeoSpline3
    {
        /// <summary>
        /// Gets the distance from this spline to a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The distance from the point to the nearest point of the curve. Nought only where the point truly lies on it,
        /// otherwise the gap, measured exactly; exact to rounding, however the curve bends.
        /// </returns>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this spline to a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>
        /// The distance from the point to the nearest point of the curve, measured exactly. The circle of radius 100 about
        /// the origin in the floor, as <see cref="FromCircle(GeoCircle3)"/> makes it, is 60 from (160, 0, 0), 100 from its
        /// own centre and 104.4 from (0, 0, 30), which is 100 across and 30 up from every point of the rim.
        /// </returns>
        /// <remarks>
        /// The nearest point is found as <see cref="GetKnotAtPoint(GeoPoint3)"/> finds it: on each span the parameters
        /// where the curve stops coming nearer, all of them, from the roots of a polynomial in Bernstein form, and the ends;
        /// whichever span cannot be nearer than the best found, by the box of its control points, is not solved. The boxes are
        /// kept in a tree, built once, so the spans near the point are reached without looking at the rest: a query costs time in
        /// the logarithm of the number of spans and the number whose boxes are near the point, as in the plane.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance)
        {
            _curve.ClosestParameter(new[] { point.X, point.Y, point.Z }, out double distance);

            return distance;
        }

        /// <summary>
        /// Determines whether a point lies on the spline, within the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on the spline, within a tolerance: within the point tolerance of its nearest point.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;

        #region Against the other shapes in space

        /// <summary>
        /// Calculates the shortest distance from this spline to a line segment, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine3 line) => DistanceTo(line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance: a line segment no longer than its point tolerance is read as its start where it does not truly meet the spline.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance)
        {
            double gap = Spline3.DistanceTo(this, Spline3.Of(this, line));

            return Stub3.ReadsAsStart(line, tolerance) && !Stub3.Meets(gap, line) ? DistanceTo(line.StartPoint, tolerance) : gap;
        }

        /// <summary>
        /// Calculates the shortest distance from this spline to a ray, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay3 ray) => DistanceTo(ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, ray));

        /// <summary>
        /// Calculates the shortest distance from this spline to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, xline));

        /// <summary>
        /// Calculates the shortest distance from this spline to a plane, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPlane3 plane) => DistanceTo(plane, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A plane is a surface without ends, not a half of space: a spline that lies off it on one side is as far from it as its nearest point.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, plane));

        /// <summary>
        /// Calculates the shortest distance from this spline to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline) => DistanceTo(polyline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, polyline));

        /// <summary>
        /// Calculates the shortest distance from this spline to a polygon, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => DistanceTo(polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A polygon is a flat region here, as every curve reads it: a spline that pierces it, or lies in it, is nought from it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, polygon));

        /// <summary>
        /// Calculates the shortest distance from this spline to a triangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 triangle) => DistanceTo(triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A triangle is a flat region here, as every curve reads it: a spline that pierces it, or lies in it, is nought from it.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, triangle));

        /// <summary>
        /// Calculates the shortest distance from this spline to a face, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => DistanceTo(face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A face is a flat region here, as every curve reads it: a spline that pierces it, or lies in it, is nought from it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, face));

        /// <summary>
        /// Calculates the shortest distance from this spline to an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoAabb3 box) => DistanceTo(box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// An axis-aligned box is material here, as every curve reads a body: a spline inside it is nought from it, though it crosses nothing.
        /// </remarks>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, box));

        /// <summary>
        /// Calculates the shortest distance from this spline to an oriented box, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public double DistanceTo(GeoObb3 box) => DistanceTo(box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// An oriented box is material here, as every curve reads a body: a spline inside it is nought from it, though it crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, box));

        /// <summary>
        /// Calculates the shortest distance from this spline to a solid, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public double DistanceTo(GeoSolid3 solid) => DistanceTo(solid, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance; read only to cut the solid's openings in, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A solid is material here, as every curve reads a body: a spline inside it is nought from it, though it crosses nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, solid, tolerance, true));

        /// <summary>
        /// Calculates the shortest distance from this spline to a ball, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoSphere3 sphere) => DistanceTo(sphere, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a ball, within a tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A ball is material here, as every curve reads a body: a spline inside it is nought from it, though it crosses nothing.
        /// </remarks>
        public double DistanceTo(GeoSphere3 sphere, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, sphere));

        /// <summary>
        /// Calculates the shortest distance from this spline to an arc, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc3 arc) => DistanceTo(arc, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc3 arc, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, arc));

        /// <summary>
        /// Calculates the shortest distance from this spline to a circle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoCircle3 circle) => DistanceTo(circle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A circle is read as the disc it is for the distance, as its own distance to a point reads it: a spline that pierces the disc, or lies in it, is nought from it, though it meets no rim.
        /// </remarks>
        public double DistanceTo(GeoCircle3 circle, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, circle));

        /// <summary>
        /// Calculates the shortest distance from this spline to an ellipse, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEllipse3 ellipse) => DistanceTo(ellipse, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// An ellipse is read as the disc it is for the distance, as its own distance to a point reads it: a spline that pierces the disc, or lies in it, is nought from it, though it meets no rim.
        /// </remarks>
        public double DistanceTo(GeoEllipse3 ellipse, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, ellipse));

        /// <summary>
        /// Calculates the shortest distance from this spline to an edge, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEdge3 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance: a straight edge no longer than its point tolerance is read as its start where it does not truly meet the spline.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEdge3 edge, Tolerance tolerance)
            => Stub3.ReadsAsStart(edge, tolerance) ? DistanceTo(edge.ToLine(), tolerance) : Spline3.DistanceTo(this, Spline3.Of(this, edge));

        /// <summary>
        /// Calculates the shortest distance from this spline to a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc3 chain) => DistanceTo(chain, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc3 chain, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, chain));

        /// <summary>
        /// Calculates the shortest distance from this spline to a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc3 loop) => DistanceTo(loop, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A curved loop is measured to its outline, as its own distance to a point is: a spline inside it is not nought from it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc3 loop, Tolerance tolerance) => Spline3.DistanceTo(this, Spline3.Of(this, loop));

        #endregion
    }
}
