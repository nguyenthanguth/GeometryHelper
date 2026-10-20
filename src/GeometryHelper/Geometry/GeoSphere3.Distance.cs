using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a ball is from another shape, and how deep inside it a point sits.
    /// </summary>
    /// <remarks>
    /// The gap is the distance from the centre of the ball to the shape less the radius, and nought where the shape reaches into the ball. A closed shape
    /// is read as filled and a curve as a curve, as the shape's own distance to a point reads it: a point inside the ball, a body that holds its centre and a
    /// plane through it are all nought from it. A curved loop is the exception among the closed shapes: it is read by its outline, as its own distance to a point is, so a ball in the middle of a large curved loop is apart from it. A ball of radius 50 about (0, 0, 100) is 50 from the floor and 150 from a ball of radius 10 about (0, 0, -110).
    /// </remarks>
    public readonly partial struct GeoSphere3
    {
        /// <summary>
        /// Calculates the shortest distance from this ball to a point.
        /// </summary>
        /// <returns>Nought where a point reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The point (200, 0, 0) is 150 from the ball of radius 50 about the origin, and (30, 0, 0), inside it, is nought.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point)
        {
            return Sphere3.Reach(this, Sphere3.FromCenter(this, point));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a segment, using the default tolerance.
        /// </summary>
        /// <returns>Nought where a segment reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine3 line) => DistanceTo(line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to a segment, within a tolerance.
        /// </summary>
        /// <returns>Nought where a segment reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The segment from (100, -100, 0) to (100, 100, 0) is 50 from the ball of radius 50 about the origin; the one from (0, -100, 0) to (0, 100, 0) passes through it and is nought.
        /// </remarks>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance)
        {
            return Sphere3.Reach(this, Distance3.DistanceTo(line, Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a ray.
        /// </summary>
        /// <returns>Nought where a ray reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The ray from (100, 0, 0) along X runs away from the ball of radius 50 about the origin and is 50 from it; along -X it runs through it and is nought.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray)
        {
            return Sphere3.Reach(this, Distance3.DistanceTo(ray, Center));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought where an infinite line reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought where an infinite line reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The line through (0, 80, 0) along X is 30 from the ball of radius 50 about the origin, and the line through (0, 30, 0) is nought from it, whichever way each runs.
        /// </remarks>
        public double DistanceTo(GeoXLine3 xline, Tolerance tolerance)
        {
            return Sphere3.Reach(this, xline.DistanceTo(Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to an arc, using the default tolerance.
        /// </summary>
        /// <returns>Nought where an arc reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoArc3 arc) => DistanceTo(arc, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to an arc, within a tolerance.
        /// </summary>
        /// <returns>Nought where an arc reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The quarter of the circle of radius 100 about the origin in the floor, from (100, 0, 0) to (0, 100, 0), is 50 from the ball of radius 50 about the origin.
        /// </remarks>
        public double DistanceTo(GeoArc3 arc, Tolerance tolerance)
        {
            return Sphere3.Reach(this, arc.DistanceTo(Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a circle, using the default tolerance.
        /// </summary>
        /// <returns>Nought where a circle reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoCircle3 circle) => DistanceTo(circle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to a circle, within a tolerance.
        /// </summary>
        /// <returns>Nought where a circle reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A circle is read as the disc it is, as its own distance to a point reads it: the circle of radius 30 about (0, 0, 100) in a plane parallel to the floor is 50 from the ball of radius 50 about the origin, straight below it, and the circle of radius 30 about the origin in the floor is nought from it.
        /// </remarks>
        public double DistanceTo(GeoCircle3 circle, Tolerance tolerance)
        {
            return Sphere3.Reach(this, Distance3.DistanceTo(circle, Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to an ellipse, using the default tolerance.
        /// </summary>
        /// <returns>Nought where an ellipse reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEllipse3 ellipse) => DistanceTo(ellipse, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to an ellipse, within a tolerance.
        /// </summary>
        /// <returns>Nought where an ellipse reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// An ellipse is read as the disc it is, as a circle is.
        /// </remarks>
        public double DistanceTo(GeoEllipse3 ellipse, Tolerance tolerance)
        {
            return Sphere3.Reach(this, Ellipse3.DistanceTo(ellipse, Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to an edge, using the default tolerance.
        /// </summary>
        /// <returns>Nought where an edge reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoEdge3 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to an edge, within a tolerance.
        /// </summary>
        /// <returns>Nought where an edge reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// An edge is measured as the segment or the arc it is.
        /// </remarks>
        public double DistanceTo(GeoEdge3 edge, Tolerance tolerance)
        {
            return Sphere3.Reach(this, edge.DistanceTo(Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>Nought where a polyline reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline) => DistanceTo(polyline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to a polyline, within a tolerance.
        /// </summary>
        /// <returns>Nought where a polyline reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A polyline is a curve, measured to its path, so the chain from (-100, 60, 0) to (100, 60, 0) is 10 from the ball of radius 50 about the origin.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return Sphere3.Reach(this, Distance3.DistanceTo(polyline, Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a polygon, using the default tolerance.
        /// </summary>
        /// <returns>Nought where a polygon reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => DistanceTo(polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to a polygon, within a tolerance.
        /// </summary>
        /// <returns>Nought where a polygon reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A polygon is read as a filled surface: the square from (-100, -100, 0) to (100, 100, 0) in the floor holds the centre of the ball of radius 50 about the origin and is nought from it, and the square 100 above it is 50 from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Sphere3.Reach(this, Distance3.DistanceTo(polygon, Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a curved chain, using the default tolerance.
        /// </summary>
        /// <returns>Nought where a curved chain reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public double DistanceTo(GeoPolylineArc3 chain) => DistanceTo(chain, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to a curved chain, within a tolerance.
        /// </summary>
        /// <returns>Nought where a curved chain reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A curved chain is a curve, measured to its path.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public double DistanceTo(GeoPolylineArc3 chain, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Sphere3.Reach(this, chain.DistanceTo(Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a curved loop, using the default tolerance.
        /// </summary>
        /// <returns>Nought where a curved loop reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public double DistanceTo(GeoPolygonArc3 loop) => DistanceTo(loop, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to a curved loop, within a tolerance.
        /// </summary>
        /// <returns>Nought where a curved loop reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A curved loop is measured to its boundary, as its own distance to a point is.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public double DistanceTo(GeoPolygonArc3 loop, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return Sphere3.Reach(this, loop.DistanceTo(Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a triangle.
        /// </summary>
        /// <returns>Nought where a triangle reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A triangle is read as a filled surface: the triangle (0, 0, 100), (100, 0, 100), (0, 100, 100) is 100 from the centre of the ball of radius 50 about the origin, so 50 from the ball.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle)
        {
            return Sphere3.Reach(this, Distance3.DistanceTo(triangle, Center));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a face, using the default tolerance.
        /// </summary>
        /// <returns>Nought where a face reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => DistanceTo(face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to a face, within a tolerance.
        /// </summary>
        /// <returns>Nought where a face reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A hole is not material: the square 200 by 200 about the origin with a hole 40 by 40 about the middle is nought from the ball of radius 50 about the origin, whose centre is in the hole, since the rim of the hole lies 20 from the centre, inside the ball.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Sphere3.Reach(this, Distance3.DistanceTo(face, Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a plane.
        /// </summary>
        /// <returns>Nought where a plane reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The floor through the centre cuts the ball of radius 50 about the origin and is nought from it; the plane 80 above is 30 from it.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane)
        {
            return Sphere3.Reach(this, Distance3.DistanceTo(plane, Center));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to an axis-aligned box.
        /// </summary>
        /// <returns>Nought where an axis-aligned box reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The box from (100, -10, -10) to (200, 10, 10) is 50 from the ball of radius 50 about the origin. An empty box is infinitely far from everything.
        /// </remarks>
        public double DistanceTo(GeoAabb3 box)
        {
            return Sphere3.Reach(this, box.DistanceTo(Center));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to an oriented box.
        /// </summary>
        /// <returns>Nought where an oriented box reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The box of 100 by 20 by 20 centred on (150, 0, 0) is 50 from the ball of radius 50 about the origin.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return Sphere3.Reach(this, Distance3.DistanceTo(box, Center));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to a solid, using the default tolerance.
        /// </summary>
        /// <returns>Nought where a solid reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public double DistanceTo(GeoSolid3 solid) => DistanceTo(solid, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ball to a solid, within a tolerance.
        /// </summary>
        /// <returns>Nought where a solid reaches the ball, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// A solid is read as its material: a body that holds the centre of the ball is nought from it, and the box from (100, -10, -10) to (200, 10, 10) is 50 from the ball of radius 50 about the origin.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return Sphere3.Reach(this, Distance3.DistanceTo(solid, Center, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance from this ball to another ball.
        /// </summary>
        /// <returns>Nought where the two touch or overlap, otherwise the gap, which is the distance between the centres less the two radii.</returns>
        /// <remarks>
        /// The ball of radius 50 about (200, 0, 0) is 140 from the ball of radius 10 about the origin, 200 less 60, whichever way round it is asked, and nought from one of radius 160 about the origin, which overlaps it.
        /// </remarks>
        public double DistanceTo(GeoSphere3 other) => Math.Max(0.0, Sphere3.FromCenter(this, other.Center) - (Radius + other.Radius));

        /// <summary>
        /// Calculates the distance from this ball to a point, negative for a point inside it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the surface, whichever side of it the point is on, and the sign says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint3)"/> reads this ball as filled and so answers nothing at all for a point inside, which is the one place the two part company. For the ball of radius 50 about the origin, (20, 0, 0) is -30 and (80, 0, 0) is 30.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint3 point) => SignedDistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from this ball to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Nought where <see cref="Locate(GeoPoint3, Tolerance)"/> says the point is on the surface, so that the two agree.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint3 point, Tolerance tolerance) => Sphere3.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the distance from the surface of this ball to a point, whichever side of it the point is on.
        /// </summary>
        /// <remarks>
        /// For the ball of radius 50 about the origin, (20, 0, 0) and (80, 0, 0) are both 30 from the surface; the centre is 50.
        /// </remarks>
        public double DistanceToSurface(GeoPoint3 point) => Math.Abs(Sphere3.FromCenter(this, point) - Radius);
    }
}
