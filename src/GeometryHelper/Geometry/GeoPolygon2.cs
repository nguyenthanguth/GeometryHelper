using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a 2D closed polygon.
    /// <para>
    /// A polygon is expected to be simple — no edge crossing another — but this is not checked, because
    /// checking costs more than every other thing the constructor does put together. A polygon whose
    /// edges do cross is still usable rather than undefined: containment reads it under the even-odd
    /// rule, so a region enclosed an even number of times counts as outside, and every operation built
    /// on containment follows suit. What does not survive is area. <see cref="SignedArea"/> sums
    /// lobes with their own signs, so a figure-eight reports zero however large its lobes are, and
    /// <see cref="Area"/> reports zero with it.
    /// </para>
    /// </summary>
    public sealed class GeoPolygon2 : IEquatable<GeoPolygon2>
    {
        private readonly GeoPoint2[] _vertices;

        /// <summary>
        /// Gets the read-only list of vertices of the polygon.
        /// </summary>
        public IReadOnlyList<GeoPoint2> Vertices => _vertices;

        /// <summary>
        /// Gets the number of vertices of the polygon.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets the number of edges of the polygon.
        /// </summary>
        public int EdgeCount => _vertices.Length;

        /// <summary>
        /// Gets the total perimeter length of the polygon.
        /// </summary>
        public double Length
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < EdgeCount; i++)
                {
                    total += GetEdgeAt(i).Length;
                }
                return total;
            }
        }

        /// <summary>
        /// Initializes a new polygon from a list of vertices. Duplicate vertex at the end to close the loop will be automatically removed.
        /// </summary>
        /// <param name="vertices">List of vertices.</param>
        public GeoPolygon2(IEnumerable<GeoPoint2> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));

            // Drop consecutive duplicates within tolerance so no zero-length edge survives. GeoPolyline2
            // filters the same way; comparing exactly would let (0,0),(0,0),(1,1) through as a valid
            // polygon despite the promise of distinct vertices below.
            List<GeoPoint2> list = new List<GeoPoint2>();
            foreach (var vertex in vertices)
            {
                if (list.Count == 0 || !list[list.Count - 1].IsEqualTo(vertex))
                {
                    list.Add(vertex);
                }
            }

            // Remove duplicate last vertex if it closes the loop back onto the first vertex
            while (list.Count > 1 && list[list.Count - 1].IsEqualTo(list[0]))
            {
                list.RemoveAt(list.Count - 1);
            }

            if (list.Count < 3)
            {
                throw new ArgumentException("A polygon must have at least 3 distinct vertices.");
            }

            _vertices = list.ToArray();
        }

        /// <summary>
        /// Initializes a new polygon directly from parameter vertices.
        /// </summary>
        public GeoPolygon2(params GeoPoint2[] vertices) : this((IEnumerable<GeoPoint2>)vertices)
        {
        }

        /// <summary>
        /// Initializes a polygon from vertices that have already been filtered and validated.
        /// </summary>
        /// <param name="validatedVertices">Source array, already free of consecutive duplicates.</param>
        /// <param name="count">Number of leading entries to copy.</param>
        /// <remarks>
        /// Clone uses this instead of the public constructor. The public one re-filters vertices against
        /// Tolerance.Global, so a clone taken after that global was widened could silently come back with
        /// fewer vertices than the original, or fail validation outright. <see cref="Core.Offset2"/> uses it
        /// for the same reason: it has already cleaned its loops against the tolerance its caller passed.
        /// </remarks>
        internal GeoPolygon2(GeoPoint2[] validatedVertices, int count)
        {
            _vertices = new GeoPoint2[count];
            Array.Copy(validatedVertices, _vertices, count);
        }

        /// <summary>
        /// Gets the vertex at a given index.
        /// </summary>
        public GeoPoint2 this[int index]
        {
            get
            {
                if (index < 0 || index >= _vertices.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return _vertices[index];
            }
        }

        /// <summary>
        /// Gets the line segment edge of the polygon at a given index.
        /// </summary>
        public GeoLine2 GetEdgeAt(int index)
        {
            if (index < 0 || index >= EdgeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return new GeoLine2(_vertices[index], _vertices[(index + 1) % _vertices.Length]);
        }

        /// <summary>
        /// Enumerates all edges of the polygon.
        /// </summary>
        public IEnumerable<GeoLine2> GetEdges()
        {
            for (int i = 0; i < EdgeCount; i++)
            {
                yield return GetEdgeAt(i);
            }
        }

        /// <summary>
        /// Gets the signed area of the polygon, by the shoelace formula: positive when the vertices run
        /// counter-clockwise, negative when they run clockwise.
        /// </summary>
        public double SignedArea
        {
            get
            {
                double area = 0.0;
                int n = _vertices.Length;
                for (int i = 0; i < n; i++)
                {
                    GeoPoint2 p1 = _vertices[i];
                    GeoPoint2 p2 = _vertices[(i + 1) % n];
                    area += p1.X * p2.Y - p2.X * p1.Y;
                }
                return area * 0.5;
            }
        }

        /// <summary>
        /// Gets the area the polygon encloses, whichever way its vertices run.
        /// </summary>
        public double Area => Math.Abs(SignedArea);

        /// <summary>
        /// Gets a value indicating whether the vertices of the polygon run clockwise.
        /// </summary>
        public bool IsClockwise => SignedArea < 0.0;

        /// <summary>
        /// Gets the geometric centroid of the polygon.
        /// </summary>
        public GeoPoint2 Centroid
        {
            get
            {
                double signedArea = SignedArea;
                if (Math.Abs(signedArea) <= Tolerance.Global.EqualPoint * Tolerance.Global.EqualPoint)
                {
                    // Fallback: Calculate the average of vertex coordinates if the polygon is degenerate
                    double sumX = 0.0;
                    double sumY = 0.0;
                    foreach (var v in _vertices)
                    {
                        sumX += v.X;
                        sumY += v.Y;
                    }
                    return new GeoPoint2(sumX / _vertices.Length, sumY / _vertices.Length);
                }

                double cx = 0.0;
                double cy = 0.0;
                int n = _vertices.Length;
                for (int i = 0; i < n; i++)
                {
                    GeoPoint2 p1 = _vertices[i];
                    GeoPoint2 p2 = _vertices[(i + 1) % n];
                    double factor = p1.X * p2.Y - p2.X * p1.Y;
                    cx += (p1.X + p2.X) * factor;
                    cy += (p1.Y + p2.Y) * factor;
                }

                double areaFactor = 1.0 / (6.0 * signedArea);
                return new GeoPoint2(cx * areaFactor, cy * areaFactor);
            }
        }

        /// <summary>
        /// Creates a copy of this polygon holding its own vertex array.
        /// </summary>
        /// <returns>A new GeoPolygon2 independent of this one.</returns>
        public GeoPolygon2 Clone() => new GeoPolygon2(_vertices, _vertices.Length);

        /// <summary>
        /// Converts this polygon's boundary into a closed 2D GeoPolyline2.
        /// The boundary is closed by repeating the first vertex at the end of the chain.
        /// </summary>
        /// <returns>A new GeoPolyline2 instance representing the polygon boundary.</returns>
        public GeoPolyline2 ToPolyline()
        {
            var polylineVertices = new GeoPoint2[_vertices.Length + 1];
            Array.Copy(_vertices, polylineVertices, _vertices.Length);
            polylineVertices[_vertices.Length] = _vertices[0];
            return new GeoPolyline2(polylineVertices);
        }

        /// <summary>
        /// Gets the point at a normalized parameter along this polygon perimeter, where 0 is the first vertex and 1 is the end.
        /// Values outside [0, 1] wrap around, so 1.25 is the same position as 0.25.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Parametrization2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point on this polygon perimeter closest to the supplied point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Parametrization2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at an arc length measured from the first vertex of this polygon perimeter.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Parametrization2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from the first vertex of this polygon perimeter to the point on it closest to the supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Parametrization2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the first vertex of this polygon perimeter to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the first vertex of this polygon perimeter.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization2.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Translates the polygon by a displacement vector.
        /// </summary>
        /// <param name="vector">The displacement vector.</param>
        /// <returns>A new translated GeoPolygon2.</returns>
        public GeoPolygon2 Translate(GeoVector2 vector)
        {
            GeoPoint2[] moved = new GeoPoint2[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = _vertices[i].Add(vector);
            }
            return new GeoPolygon2(moved);
        }

        /// <summary>
        /// Rotates the polygon around a center point by an angle in radians (counter-clockwise).
        /// </summary>
        /// <param name="angleRad">Rotation angle in radians.</param>
        /// <param name="center">Center of rotation.</param>
        /// <returns>A new rotated GeoPolygon2.</returns>
        public GeoPolygon2 RotateBy(double angleRad, GeoPoint2 center)
        {
            GeoPoint2[] rotated = new GeoPoint2[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
            {
                rotated[i] = _vertices[i].RotateBy(angleRad, center);
            }
            return new GeoPolygon2(rotated);
        }

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to another polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(this, line);

        /// <summary>
        /// Calculates the shortest distance from this polygon boundary to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this polygon to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Distance2.DistanceTo(this, arc);

        /// <summary>
        /// Calculates the shortest distance from this polygon to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Distance2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this polygon to an arc.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Projection2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment joining this polygon to an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Projection2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a curved loop.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(loop, this);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a curved loop.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Projection2.GetShortestLineTo(this, loop);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Projection2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a curved chain.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(chain, this);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a curved chain.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Projection2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the edge of this polygon nearest a point.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoPoint2 point) => ClosestEdge2.GetClosestEdge(this, point);

        /// <summary>
        /// Gets the edge of this polygon nearest a segment.
        /// </summary>
        /// <remarks>
        /// The answer is one of this polygon's own edges, not a segment joining the two shapes.
        /// For that, see <see cref="GetShortestLineTo(GeoLine2)"/>.
        /// </remarks>
        public GeoLine2 GetClosestEdge(GeoLine2 line) => ClosestEdge2.GetClosestEdge(this, line);

        /// <summary>
        /// Gets the edge of this polygon nearest a circle.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoCircle2 circle) => ClosestEdge2.GetClosestEdge(this, circle);

        /// <summary>
        /// Gets the closest point on the boundary of this polygon to a target point, including for points
        /// inside the polygon.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Projection2.ProjectToPolygon(this, point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on a line segment using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Projection2.GetShortestLineTo(this, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Projection2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the circumference of a circle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Projection2.GetShortestLineTo(this, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the circumference of a circle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Projection2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the boundary of a rectangle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Projection2.GetShortestLineTo(this, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the boundary of a rectangle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetShortestLineTo(this, rect, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on a polyline using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Projection2.GetShortestLineTo(this, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on a polyline within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the boundary of another polygon using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 other) => Projection2.GetShortestLineTo(this, other, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this polygon to a point on the boundary of another polygon within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 other, Tolerance tolerance) => Projection2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Checks whether the polygon contains a point using default tolerance (accepts points on the boundary).
        /// </summary>
        public bool Contains(GeoPoint2 GeoPoint2) => Contains(GeoPoint2, Tolerance.Global);

        /// <summary>
        /// Checks whether the polygon contains a point (accepts points on the boundary).
        /// Uses the Ray Casting algorithm (shoots a horizontal ray and counts intersections).
        /// </summary>
        public bool Contains(GeoPoint2 GeoPoint2, Tolerance tolerance) => Containment2.Contains(this, GeoPoint2, tolerance);

        /// <summary>
        /// Classifies the location of a point relative to this polygon (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point, Tolerance.Global);

        /// <summary>
        /// Classifies the location of a point relative to this polygon (Inside, OutSide, or OnSide) within tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether the polygon entirely contains a polyline using default tolerance.
        /// </summary>
        public bool Contains(GeoPolyline2 polyline) => Containment2.Contains(this, polyline, Tolerance.Global);

        /// <summary>
        /// Checks whether the polygon entirely contains a polyline within tolerance.
        /// </summary>
        public bool Contains(GeoPolyline2 polyline, Tolerance tolerance) => Containment2.Contains(this, polyline, tolerance);

        /// <summary>
        /// Checks whether the polygon collides with a rotated rectangle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Collision2.CollidesWith(rect, this, Tolerance.Global);

        /// <summary>
        /// Checks whether the polygon collides with a rotated rectangle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Collision2.CollidesWith(rect, this, tolerance);

        /// <summary>
        /// Checks whether the polygon collides with a line segment using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 geoLine) => Collision2.CollidesWith(this, geoLine, Tolerance.Global);

        /// <summary>
        /// Checks whether the polygon collides with a line segment within tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 geoLine, Tolerance tolerance) => Collision2.CollidesWith(this, geoLine, tolerance);

        /// <summary>
        /// Checks whether the polygon collides with another polygon using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 other) => Collision2.CollidesWith(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether the polygon collides with another polygon within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether the polygon collides with a circle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Collision2.CollidesWith(circle, this, Tolerance.Global);

        /// <summary>
        /// Checks whether the polygon collides with a circle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Collision2.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether the polygon collides with a polyline using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => Collision2.CollidesWith(polyline, this, Tolerance.Global);

        /// <summary>
        /// Checks whether the polygon collides with a polyline within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Collision2.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a line segment using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Intersection2.GetIntersections(this, line, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a line segment within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Intersection2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets all intersection points with another polygon using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 other) => Intersection2.GetIntersections(this, other, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with another polygon within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 other, Tolerance tolerance) => Intersection2.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Gets all intersection points with a rectangle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Intersection2.GetIntersections(this, rect, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a rectangle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Intersection2.GetIntersections(this, rect, tolerance);

        /// <summary>
        /// Gets all intersection points with a circle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Intersection2.GetIntersections(this, circle, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a circle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Intersection2.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets all intersection points with a polyline using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(polyline, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polyline within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this polygon is simple, using default tolerance: no edge crosses or touches another
        /// except where neighbours share their vertex.
        /// </summary>
        public bool IsSimple() => Intersection2.IsSimple(this, Tolerance.Global);

        /// <summary>
        /// Checks whether this polygon is simple, within tolerance.
        /// </summary>
        public bool IsSimple(Tolerance tolerance) => Intersection2.IsSimple(this, tolerance);

        /// <summary>
        /// Grows this polygon by a distance, or shrinks it when the distance is negative, with sharp corners,
        /// using default tolerance. See <see cref="Offset2.Offset(GeoPolygon2, double, OffsetOptions, Tolerance)"/>.
        /// </summary>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <returns>The boundary loops of the offset region: none, one, or several; holes wound the other way.</returns>
        public GeoPolygon2[] Offset(double distance) => Offset2.Offset(this, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Grows this polygon by a distance, or shrinks it when the distance is negative, with sharp corners,
        /// within tolerance.
        /// </summary>
        public GeoPolygon2[] Offset(double distance, Tolerance tolerance) => Offset2.Offset(this, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Grows this polygon by a distance, or shrinks it when the distance is negative, with the given
        /// corners, using default tolerance.
        /// </summary>
        public GeoPolygon2[] Offset(double distance, OffsetJoin join) => Offset2.Offset(this, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Grows this polygon by a distance, or shrinks it when the distance is negative, with the given
        /// corners, within tolerance.
        /// </summary>
        public GeoPolygon2[] Offset(double distance, OffsetJoin join, Tolerance tolerance) => Offset2.Offset(this, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Grows this polygon by a distance, or shrinks it when the distance is negative, as the options say,
        /// using default tolerance.
        /// </summary>
        public GeoPolygon2[] Offset(double distance, OffsetOptions options) => Offset2.Offset(this, distance, options, Tolerance.Global);

        /// <summary>
        /// Grows this polygon by a distance, or shrinks it when the distance is negative, as the options say,
        /// within tolerance.
        /// </summary>
        public GeoPolygon2[] Offset(double distance, OffsetOptions options, Tolerance tolerance) => Offset2.Offset(this, distance, options, tolerance);

        /// <summary>
        /// Gets the region covered by this polygon or another, using default tolerance. See <see cref="Boolean2"/>.
        /// </summary>
        public GeoFace2[] Union(GeoPolygon2 other) => Boolean2.Union(this, other, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by this polygon or another, within tolerance.
        /// </summary>
        public GeoFace2[] Union(GeoPolygon2 other, Tolerance tolerance) => Boolean2.Union(this, other, tolerance);

        /// <summary>
        /// Gets the region covered by both this polygon and another, using default tolerance.
        /// </summary>
        public GeoFace2[] Intersect(GeoPolygon2 other) => Boolean2.Intersect(this, other, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by both this polygon and another, within tolerance.
        /// </summary>
        public GeoFace2[] Intersect(GeoPolygon2 other, Tolerance tolerance) => Boolean2.Intersect(this, other, tolerance);

        /// <summary>
        /// Gets the region of this polygon with another taken out of it, using default tolerance.
        /// </summary>
        public GeoFace2[] Subtract(GeoPolygon2 tool) => Boolean2.Subtract(this, tool, Tolerance.Global);

        /// <summary>
        /// Gets the region of this polygon with another taken out of it, within tolerance.
        /// </summary>
        public GeoFace2[] Subtract(GeoPolygon2 tool, Tolerance tolerance) => Boolean2.Subtract(this, tool, tolerance);

        /// <summary>
        /// Gets the region of this polygon with every one of a set of polygons taken out of it, using default
        /// tolerance.
        /// </summary>
        public GeoFace2[] Subtract(IEnumerable<GeoPolygon2> tools) => Boolean2.Subtract(this, tools, Tolerance.Global);

        /// <summary>
        /// Gets the region of this polygon with every one of a set of polygons taken out of it, within tolerance.
        /// </summary>
        public GeoFace2[] Subtract(IEnumerable<GeoPolygon2> tools, Tolerance tolerance) => Boolean2.Subtract(this, tools, tolerance);

        /// <summary>
        /// Gets the region covered by exactly one of this polygon and another, using default tolerance.
        /// </summary>
        public GeoFace2[] Xor(GeoPolygon2 other) => Boolean2.Xor(this, other, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by exactly one of this polygon and another, within tolerance.
        /// </summary>
        public GeoFace2[] Xor(GeoPolygon2 other, Tolerance tolerance) => Boolean2.Xor(this, other, tolerance);

        /// <summary>
        /// Translates a polygon by a vector.
        /// </summary>
        public static GeoPolygon2 operator +(GeoPolygon2 poly, GeoVector2 vector) => poly.Translate(vector);

        /// <summary>
        /// Translates a polygon backwards by a vector.
        /// </summary>
        public static GeoPolygon2 operator -(GeoPolygon2 poly, GeoVector2 vector) => poly.Translate(-vector);

        /// <summary>
        /// Indicates whether the current polygon is equal to another polygon by comparing vertex arrays in order.
        /// </summary>
        /// <param name="other">A polygon to compare with this polygon.</param>
        /// <returns>true if the current polygon is equal to the other parameter; otherwise, false.</returns>
        public bool Equals(GeoPolygon2 other)
        {
            if (other == null || other.VertexCount != VertexCount) return false;
            for (int i = 0; i < _vertices.Length; i++)
            {
                if (!_vertices[i].Equals(other._vertices[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is GeoPolygon2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var v in _vertices)
                {
                    hash = hash * 31 + v.GetHashCode();
                }
                return hash;
            }
        }

        /// <summary>
        /// Compares two GeoPolygon2 instances for equality.
        /// </summary>
        /// <param name="left">The first polygon.</param>
        /// <param name="right">The second polygon.</param>
        /// <returns>true if they are equal; otherwise, false.</returns>
        public static bool operator ==(GeoPolygon2 left, GeoPolygon2 right) => Equals(left, right);

        /// <summary>
        /// Compares two GeoPolygon2 instances for inequality.
        /// </summary>
        /// <param name="left">The first polygon.</param>
        /// <param name="right">The second polygon.</param>
        /// <returns>true if they are not equal; otherwise, false.</returns>
        public static bool operator !=(GeoPolygon2 left, GeoPolygon2 right) => !Equals(left, right);

        /// <summary>
        /// Returns the string representation of the polygon.
        /// </summary>
        /// <returns>A string representation of vertex count and area.</returns>
        public override string ToString() => $"GeoPolygon2[{VertexCount} vertices, Area:{Area:0.###}]";

        /// <summary>
        /// Puts this polygon of the plane back into space, on the plane of a frame.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <returns>The polygon in space.</returns>
        public GeoPolygon3 ToPolygon3(GeoCoordinateSystem3 frame) => PlanarMap.ToPolygon3(frame, this);

        /// <summary>
        /// Applies a transformation to this polygon. A transformation that turns shapes round, a mirror or a negative scaling, reverses the
        /// winding: a polygon that ran counter-clockwise comes back clockwise.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed polygon.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoPolygon2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Determines whether another polygon encloses the same region, within the default tolerance.
        /// </summary>
        /// <param name="other">The polygon to compare with.</param>
        /// <returns>true if the polygons are the same within tolerance; otherwise, false.</returns>
        public bool IsEqualTo(GeoPolygon2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another polygon encloses the same region, within a tolerance.
        /// </summary>
        /// <param name="other">The polygon to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygons are the same within tolerance; otherwise, false.</returns>
        /// <remarks>
        /// Where a loop starts is not part of the shape it encloses, so the comparison tries every rotation
        /// of the other polygon before giving up. The direction it runs in is part of the shape, and two
        /// polygons tracing one region in opposite directions are not equal.
        /// </remarks>
        public bool IsEqualTo(GeoPolygon2 other, Tolerance tolerance)
        {
            if (other is null || _vertices.Length != other._vertices.Length)
            {
                return false;
            }

            int count = _vertices.Length;

            for (int shift = 0; shift < count; shift++)
            {
                bool matched = true;

                for (int i = 0; i < count; i++)
                {
                    if (!_vertices[i].IsEqualTo(other._vertices[(shift + i) % count], tolerance))
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Chamfers every corner that has room for it, cutting the same distance along both edges.
        /// </summary>
        /// <param name="distance">How far back along each edge the cut is measured.</param>
        /// <returns>The chamfered polygon.</returns>
        public GeoPolygon2 Chamfer(double distance) => Corner2.Chamfer(this, distance);

        /// <summary>
        /// Chamfers every corner that has room for it, cutting the same distance along both edges, within a tolerance.
        /// </summary>
        public GeoPolygon2 Chamfer(double distance, Tolerance tolerance) => Corner2.Chamfer(this, distance, tolerance);

        /// <summary>
        /// Chamfers every corner that has room for it.
        /// </summary>
        /// <param name="distance1">How far back along the edge running into each corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <returns>The chamfered polygon.</returns>
        public GeoPolygon2 Chamfer(double distance1, double distance2) => Corner2.Chamfer(this, distance1, distance2);

        /// <summary>
        /// Chamfers every corner that has room for it, within a tolerance.
        /// </summary>
        public GeoPolygon2 Chamfer(double distance1, double distance2, Tolerance tolerance) => Corner2.Chamfer(this, distance1, distance2, tolerance);

        /// <summary>
        /// Chamfers one corner, using the default tolerance.
        /// </summary>
        /// <returns>true if the corner had room for the cut; otherwise, false.</returns>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolygon2 result) => Corner2.TryChamferAt(this, index, distance1, distance2, out result);

        /// <summary>
        /// Chamfers one corner, within a tolerance.
        /// </summary>
        /// <returns>true if the corner had room for the cut; otherwise, false.</returns>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolygon2 result, Tolerance tolerance) => Corner2.TryChamferAt(this, index, distance1, distance2, out result, tolerance);
        /// <summary>
        /// Gets the polygon running the other way round.
        /// </summary>
        /// <returns>The same shape, walked in the other direction.</returns>
        /// <remarks>
        /// Which way a loop runs is what tells its inside from its outside to anything that reads winding,
        /// and it is what decides which way round a pair of chamfer distances goes.
        /// </remarks>
        public GeoPolygon2 Reverse()
        {
            var turned = new GeoPoint2[VertexCount];

            for (int i = 0; i < VertexCount; i++)
            {
                turned[i] = this[VertexCount - 1 - i];
            }

            return new GeoPolygon2(turned);
        }

    }
}
