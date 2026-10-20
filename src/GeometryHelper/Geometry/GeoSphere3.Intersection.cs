using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where the surface of a ball is crossed by another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the surface that is crossed, not the ball: a line, a ray or a segment crosses it in no point, one where it grazes it, or two, a curve in as many
    /// points as it meets it, and a flat shape where its boundary does. A line or a curve that lies wholly inside the ball collides with it and crosses it
    /// nowhere, and a curve that lies on the surface itself touches it along its length and crosses it nowhere, as two arcs of one circle cross nowhere. The points of a line, a ray or a segment run along it, those of a curve along the curve, and those of a chain or a boundary in the order of its edges,
    /// a corner on the surface once. A line that passes within the point tolerance outside the surface touches it at its foot, and one whose two crossings are
    /// no further apart than the point tolerance touches it at the one.
    /// </para>
    /// <para>
    /// A plane or another ball is cut in a circle, which is not a list of points: <see cref="TryGetIntersectionCircle(GeoPlane3, out GeoCircle3)"/> gives it,
    /// and <see cref="TryGetTangentPoint(GeoPlane3, out GeoPoint3)"/> the one point where they only touch. Touching is read within the point tolerance, so two shells that cross within that tolerance of each other, or a plane that cuts less than that deep, touch at a point though the circle they truly share can be a few units across: sqrt(2 R tol), 4.5 for a radius R of 10 000 at the default tolerance. A body or a box is asked whether it touches the ball,
    /// and where their surfaces cross is the clash's question.
    /// </para>
    /// </remarks>
    public readonly partial struct GeoSphere3
    {
        /// <summary>
        /// Gets the points where the surface of this ball crosses a segment, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a segment, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <remarks>
        /// The segment from (0, -100, 0) to (0, 100, 0) crosses the surface of the ball of radius 50 about the origin at (0, -50, 0) and (0, 50, 0), in that order. The segment from (-10, 0, 0) to (10, 0, 0) lies wholly inside, collides with the ball and crosses it nowhere; one that ends 0.0005 short of the surface touches it, at its end.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            Sphere3.Cross(found, this, line, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections) => TryIntersectWith(line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses a ray, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => GetIntersections(ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a ray, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <remarks>
        /// The ray from (100, 0, 0) along -X crosses the surface of the ball of radius 50 about the origin at (50, 0, 0) and (-50, 0, 0), in that order; the ray from (10, 0, 0) along X starts inside and crosses once, at (50, 0, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            Sphere3.Cross(found, this, ray.Origin, ray.Direction, 0.0, double.PositiveInfinity, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections) => TryIntersectWith(ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ray, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses an infinite line, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses an infinite line, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <remarks>
        /// The line through (0, 30, 0) along X crosses the surface of the ball of radius 50 about the origin at (-40, 30, 0) and (40, 30, 0), in order along the line; the line through (0, 50, 0) along X grazes it and gives the one point (0, 50, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            Sphere3.Cross(found, this, xline.Origin, xline.Direction, double.NegativeInfinity, double.PositiveInfinity, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections) => TryIntersectWith(xline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(xline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses an arc, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => GetIntersections(arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses an arc, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <remarks>
        /// The half of the circle of radius 30 about (50, 0, 0) in the floor that runs from (80, 0, 0) over (50, 30, 0) to (20, 0, 0) crosses the surface of the ball of radius 50 about the origin once, at (41, 28.62, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance)
        {
            return Sphere3.Cross(this, arc, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections) => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses a circle, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => GetIntersections(circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a circle, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <remarks>
        /// The crossings are those of the rim: the circle of radius 30 about (50, 0, 0) in the floor crosses the surface of the ball of radius 50 about the origin at (41, 28.62, 0) and (41, -28.62, 0), and the circle of radius 30 about the origin lies inside the ball, collides with it and crosses it nowhere.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance)
        {
            return Sphere3.Cross(this, circle, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections) => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses an ellipse, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEllipse3 ellipse) => GetIntersections(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses an ellipse, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <remarks>
        /// The ellipse of radii 100 and 40 about the origin in the floor, its long axis along X, crosses the surface of the ball of radius 50 about the origin at the four points (+/-32.73, +/-37.80, 0).
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, Tolerance tolerance)
        {
            return Sphere3.Cross(this, ellipse, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses an ellipse, using the default tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEllipse3 ellipse, out GeoPoint3[] intersections) => TryIntersectWith(ellipse, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEllipse3 ellipse, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses an edge, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses an edge, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        public GeoPoint3[] GetIntersections(GeoEdge3 edge, Tolerance tolerance)
        {
            return Sphere3.Cross(this, edge, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections) => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(edge, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline) => GetIntersections(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a polyline, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolyline3 polyline, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return Sphere3.Cross(this, polyline.Vertices, false, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections) => TryIntersectWith(polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline3 polyline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => GetIntersections(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a polygon, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <remarks>
        /// It is the boundary of the polygon that is crossed: the square from (-100, -100, 0) to (100, 100, 0) holds the ball in its middle and its sides are all further than 50 away, so it collides with the ball and crosses its surface nowhere; the rectangle from (-30, -100, 0) to (30, 100, 0) crosses it at (30, -40, 0), (30, 40, 0), (-30, 40, 0) and (-30, -40, 0), in the order its sides are met.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Sphere3.Cross(this, polygon.Vertices, true, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections) => TryIntersectWith(polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool TryIntersectWith(GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain) => GetIntersections(chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a curved chain, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Sphere3.Cross(this, chain.GetEdges(), false, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections) => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc3 chain, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop) => GetIntersections(loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a curved loop, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return Sphere3.Cross(this, loop.GetEdges(), true, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections) => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc3 loop, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses a triangle, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => GetIntersections(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a triangle, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance)
        {
            return Sphere3.Cross(this, new[] { triangle.A, triangle.B, triangle.C }, true, tolerance);
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections) => TryIntersectWith(triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(triangle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the surface of this ball crosses a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => GetIntersections(face, Tolerance.Global);

        /// <summary>
        /// Gets the points where the surface of this ball crosses a face, within a tolerance.
        /// </summary>
        /// <returns>The crossings, none where they do not meet or where the shape lies inside the ball.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            var found = new List<GeoPoint3>(Sphere3.Cross(this, face.Boundary.Vertices, true, tolerance));

            foreach (GeoPolygon3 hole in face.Holes)
            {
                found.AddRange(Sphere3.Cross(this, hole.Vertices, true, tolerance));
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the surface of this ball crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections) => TryIntersectWith(face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the surface of this ball crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(face, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Tries to find the circle a plane cuts from the surface of this ball, using the default tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="circle">The circle when the method returns true, in the plane, about the foot of the centre on it; default otherwise.</param>
        public bool TryGetIntersectionCircle(GeoPlane3 plane, out GeoCircle3 circle) => TryGetIntersectionCircle(plane, out circle, Tolerance.Global);

        /// <summary>
        /// Tries to find the circle a plane cuts from the surface of this ball, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="circle">The circle when the method returns true, in the plane, about the foot of the centre on it; default otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the plane cuts the surface deeper than the point tolerance; false where it only touches it, which <see cref="TryGetTangentPoint(GeoPlane3, out GeoPoint3, Tolerance)"/> reads, or misses it.</returns>
        /// <remarks>
        /// The floor cuts the ball of radius 50 about (0, 0, 30) in the circle of radius 40 about the origin, its normal that of the floor, and the plane 80 above the floor in none; the ball of radius 50 about (0, 0, 50) only touches the floor.
        /// </remarks>
        public bool TryGetIntersectionCircle(GeoPlane3 plane, out GeoCircle3 circle, Tolerance tolerance)
        {
            return Sphere3.CutByPlane(this, plane, tolerance, out circle, out _) == Sphere3.Meeting.Circle;
        }

        /// <summary>
        /// Tries to find the point where a plane only touches the surface of this ball, using the default tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="point">The foot of the centre on the plane when the method returns true; default otherwise.</param>
        public bool TryGetTangentPoint(GeoPlane3 plane, out GeoPoint3 point) => TryGetTangentPoint(plane, out point, Tolerance.Global);

        /// <summary>
        /// Tries to find the point where a plane only touches the surface of this ball, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="point">The foot of the centre on the plane when the method returns true; default otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the plane stands within the point tolerance of the surface, either side of it, and does not cut it deeper.</returns>
        /// <remarks>
        /// The ball of radius 50 about (0, 0, 50) touches the floor at the origin, and so does the one about (0, 0, 50.0005), which stands 0.0005 clear of it; the one about (0, 0, 50.002) does not.
        /// </remarks>
        public bool TryGetTangentPoint(GeoPlane3 plane, out GeoPoint3 point, Tolerance tolerance)
        {
            return Sphere3.CutByPlane(this, plane, tolerance, out _, out point) == Sphere3.Meeting.Point;
        }

        /// <summary>
        /// Tries to find the circle the surfaces of this ball and another share, using the default tolerance.
        /// </summary>
        /// <param name="other">The other ball.</param>
        /// <param name="circle">The circle when the method returns true; default otherwise.</param>
        public bool TryGetIntersectionCircle(GeoSphere3 other, out GeoCircle3 circle) => TryGetIntersectionCircle(other, out circle, Tolerance.Global);

        /// <summary>
        /// Tries to find the circle the surfaces of this ball and another share, within a tolerance.
        /// </summary>
        /// <param name="other">The other ball.</param>
        /// <param name="circle">The circle when the method returns true: in the plane the two surfaces share, its normal along the line from the centre of this ball to the other; default otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the surfaces cross in a circle; false where they touch at a point, which <see cref="TryGetTangentPoint(GeoSphere3, out GeoPoint3, Tolerance)"/> reads, where one lies inside the other or they are apart, and where their centres are within the point tolerance of each other.</returns>
        /// <remarks>
        /// The ball of radius 50 about the origin and the ball of radius 30 about (50, 0, 0) cross in the circle of radius 28.62 about (41, 0, 0), square to X. The circle is the same whichever ball is asked, to the bit, but for its normal, which points from the ball asked to the other.
        /// </remarks>
        public bool TryGetIntersectionCircle(GeoSphere3 other, out GeoCircle3 circle, Tolerance tolerance)
        {
            return Sphere3.CutBySphere(this, other, tolerance, out circle, out _) == Sphere3.Meeting.Circle;
        }

        /// <summary>
        /// Tries to find the point where the surfaces of this ball and another only touch, using the default tolerance.
        /// </summary>
        /// <param name="other">The other ball.</param>
        /// <param name="point">The point of contact when the method returns true; default otherwise.</param>
        public bool TryGetTangentPoint(GeoSphere3 other, out GeoPoint3 point) => TryGetTangentPoint(other, out point, Tolerance.Global);

        /// <summary>
        /// Tries to find the point where the surfaces of this ball and another only touch, within a tolerance.
        /// </summary>
        /// <param name="other">The other ball.</param>
        /// <param name="point">The point of contact when the method returns true; default otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the balls touch from outside, or one touches the other from inside, within the point tolerance; the point is halfway between the two surfaces on the line of their centres, so it is within the tolerance of both.</returns>
        /// <remarks>
        /// The ball of radius 50 about the origin touches the ball of radius 30 about (80, 0, 0) at (50, 0, 0), and the ball of radius 20 about (30, 0, 0), from inside, at (50, 0, 0).
        /// </remarks>
        public bool TryGetTangentPoint(GeoSphere3 other, out GeoPoint3 point, Tolerance tolerance)
        {
            return Sphere3.CutBySphere(this, other, tolerance, out _, out point) == Sphere3.Meeting.Point;
        }
    }
}
