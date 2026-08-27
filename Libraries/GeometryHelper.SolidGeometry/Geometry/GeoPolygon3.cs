using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a flat closed polygon in 3D space.
    /// <para>
    /// Coplanarity is enforced at construction. A polygon that is not flat has no normal, no area and no
    /// interior, so accepting one would leave every property on this type quietly meaningless; the
    /// constructor refuses it instead. Geometry that wanders out of a plane is a
    /// <see cref="GeoPolyline3"/>.
    /// </para>
    /// <para>
    /// The polygon is expected to be simple — no edge crossing another — but this is not checked, because
    /// checking costs more than everything else the constructor does put together. A self-crossing
    /// polygon stays usable rather than undefined: containment reads it under the even-odd rule, so a
    /// region enclosed an even number of times counts as outside. What does not survive is area: the lobes
    /// of a figure-eight cancel, so it reports an area near zero however large those lobes are.
    /// </para>
    /// </summary>
    public sealed class GeoPolygon3 : IEquatable<GeoPolygon3>
    {
        private readonly GeoPoint3[] _vertices;

        /// <summary>
        /// Gets the read-only list of vertices defining the polygon.
        /// </summary>
        public IReadOnlyList<GeoPoint3> Vertices => _vertices;

        /// <summary>
        /// Gets the number of vertices.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets the number of edges, which for a closed loop equals the number of vertices.
        /// </summary>
        public int EdgeCount => _vertices.Length;

        /// <summary>
        /// Gets the unit normal of the polygon, following the right-hand rule around its vertex order.
        /// </summary>
        public GeoVector3 Normal { get; }

        /// <summary>
        /// Gets the area of the polygon.
        /// </summary>
        public double Area { get; }

        /// <summary>
        /// Gets the total length of the boundary.
        /// </summary>
        public double Length { get; }

        /// <summary>
        /// Initializes a new polygon from a sequence of coplanar vertices.
        /// </summary>
        /// <param name="vertices">The vertices, in order around the loop.</param>
        /// <exception cref="ArgumentNullException">Thrown when the sequence is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when fewer than three distinct vertices remain, when they are collinear, or when they do
        /// not lie on a common plane.
        /// </exception>
        /// <remarks>
        /// Consecutive duplicates within tolerance are dropped and a repeated closing vertex is removed,
        /// so the loop closes implicitly and no zero-length edge survives. The normal and area are
        /// measured once here rather than on every read: the polygon cannot change afterwards, and the
        /// Newell sum they both come from is a full pass over the vertices.
        /// </remarks>
        public GeoPolygon3(IEnumerable<GeoPoint3> vertices)
            : this(vertices, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes a new polygon from a sequence of coplanar vertices, within a tolerance.
        /// </summary>
        /// <param name="vertices">The vertices, in order around the loop.</param>
        /// <param name="tolerance">The tolerance deciding duplicate vertices and flatness.</param>
        public GeoPolygon3(IEnumerable<GeoPoint3> vertices, Tolerance tolerance)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            List<GeoPoint3> kept = new List<GeoPoint3>();

            foreach (GeoPoint3 vertex in vertices)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].IsEqualTo(vertex, tolerance))
                {
                    kept.Add(vertex);
                }
            }

            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], tolerance))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count < 3)
            {
                throw new ArgumentException("A polygon must have at least 3 distinct vertices.", nameof(vertices));
            }

            GeoPoint3[] loop = kept.ToArray();
            GeoVector3 areaVector = Newell.GetAreaVector(loop);

            // The length of this vector is the area of the loop, so the threshold it clears is
            // EqualVector read as an area rather than as a length. That is deliberate but worth knowing:
            // a loop enclosing less than that is refused however long its edges are, so a sliver a metre
            // long and a thousandth of a millimetre wide is rejected as collinear, and a genuinely tiny
            // polygon is rejected along with it. Pass a tighter tolerance where such a polygon is real.
            if (!areaVector.TryGetNormal(out GeoVector3 normal, tolerance))
            {
                throw new ArgumentException("A polygon must enclose an area; these vertices are collinear.", nameof(vertices));
            }

            GeoPlane3 carrier = new GeoPlane3(loop[0], normal);

            if (!carrier.ContainsAll(loop, tolerance))
            {
                throw new ArgumentException("A polygon must be flat; these vertices do not share a plane.", nameof(vertices));
            }

            _vertices = loop;
            Normal = normal;
            Area = areaVector.Length;
            Length = MeasurePerimeter(loop);
        }

        /// <summary>
        /// Initializes a new polygon directly from vertex arguments.
        /// </summary>
        public GeoPolygon3(params GeoPoint3[] vertices)
            : this((IEnumerable<GeoPoint3>)vertices)
        {
        }

        /// <summary>
        /// Initializes a polygon from vertices that have already been filtered and validated.
        /// </summary>
        /// <remarks>
        /// <see cref="Clone"/> and <see cref="Flip"/> use this instead of the public constructor. The
        /// public one re-filters against the global tolerance and re-checks flatness, so a copy taken
        /// after that global was tightened could fail validation that the original passed.
        /// </remarks>
        private GeoPolygon3(GeoPoint3[] validatedVertices, GeoVector3 normal, double area)
        {
            _vertices = validatedVertices;
            Normal = normal;
            Area = area;
            Length = MeasurePerimeter(validatedVertices);
        }

        /// <summary>
        /// Measures the total boundary length of a closed loop.
        /// </summary>
        private static double MeasurePerimeter(GeoPoint3[] loop)
        {
            double total = 0.0;

            for (int i = 0; i < loop.Length; i++)
            {
                total += loop[i].DistanceTo(loop[(i + 1) % loop.Length]);
            }

            return total;
        }

        /// <summary>
        /// Creates a copy of this polygon.
        /// </summary>
        public GeoPolygon3 Clone() => new GeoPolygon3((GeoPoint3[])_vertices.Clone(), Normal, Area);

        /// <summary>
        /// Gets the polygon with its vertices in the opposite order, so its normal reverses.
        /// </summary>
        public GeoPolygon3 Flip()
        {
            GeoPoint3[] reversed = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                reversed[i] = _vertices[_vertices.Length - 1 - i];
            }

            return new GeoPolygon3(reversed, Normal.Negate(), Area);
        }

        /// <summary>
        /// Gets the vertex at a given index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public GeoPoint3 this[int index]
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
        /// Gets the edge at a given index, with the last edge closing the loop.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public GeoLine3 GetEdgeAt(int index)
        {
            if (index < 0 || index >= EdgeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new GeoLine3(_vertices[index], _vertices[(index + 1) % _vertices.Length]);
        }

        /// <summary>
        /// Gets every edge of the polygon, in order around the loop.
        /// </summary>
        public GeoLine3[] GetEdges()
        {
            GeoLine3[] edges = new GeoLine3[EdgeCount];

            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = GetEdgeAt(i);
            }

            return edges;
        }

        /// <summary>
        /// Gets the plane carrying the polygon, oriented along its normal.
        /// </summary>
        public GeoPlane3 GetPlane() => new GeoPlane3(_vertices[0], Normal);

        #region Parametrization

        /// <summary>
        /// Gets the point at a normalized parameter around the boundary, starting at the first vertex. The
        /// parameter wraps, so 1.25 gives the same point as 0.25.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => Parametrization3.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the point at an arc length measured around the boundary from the first vertex.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => Parametrization3.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from the first vertex to a normalized parameter around the boundary.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization3.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured around the boundary.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization3.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Gets the normalized parameter of the point on the boundary closest to the supplied point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => Parametrization3.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the normalized parameter of the point on the boundary closest to the supplied point, within
        /// a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance) => Parametrization3.GetParameterAtPoint(this, point, tolerance);

        /// <summary>
        /// Gets the arc length from the first vertex to the point on the boundary closest to the supplied
        /// point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => Parametrization3.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the first vertex to the point on the boundary closest to the supplied
        /// point, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point, Tolerance tolerance) => Parametrization3.GetDistanceAtPoint(this, point, tolerance);

        #endregion

        /// <summary>
        /// Gets the centroid of the polygon area.
        /// </summary>
        /// <remarks>
        /// This is the centre of mass of the filled shape, not the average of the vertices. The two agree
        /// only for a shape whose vertices are spread evenly, and the average is not a useful answer for
        /// a polygon with several vertices clustered along one edge.
        /// <para>
        /// Each triangle of the fan is weighted by its area <b>signed</b> against the polygon normal, not
        /// by its plain area. For a concave polygon parts of the fan reach outside the boundary, and they
        /// are wound the other way; their negative weight is exactly what cancels them out again. Using
        /// the unsigned area would count that overhang as material and pull the answer towards it.
        /// </para>
        /// </remarks>
        public GeoPoint3 Centroid
        {
            get
            {
                double totalWeight = 0.0;
                double x = 0.0;
                double y = 0.0;
                double z = 0.0;

                foreach (GeoTriangle3 triangle in Triangulate())
                {
                    double weight = triangle.GetAreaVector().DotProduct(Normal) * 0.5;
                    GeoPoint3 center = triangle.Centroid;

                    totalWeight += weight;
                    x += center.X * weight;
                    y += center.Y * weight;
                    z += center.Z * weight;
                }

                // The weight is an area, so the threshold it is judged against has to be one too: a
                // length tolerance squared. Comparing against double.Epsilon instead would only catch a
                // weight of exactly zero, and a loop that doubles back on itself can leave one around
                // 1e-300 � small enough that dividing by it overflows, large enough to pass the test.
                double areaEpsilon = Tolerance.Global.EqualPoint * Tolerance.Global.EqualPoint;

                if (Math.Abs(totalWeight) <= areaEpsilon)
                {
                    return _vertices[0];
                }

                return new GeoPoint3(x / totalWeight, y / totalWeight, z / totalWeight);
            }
        }

        /// <summary>
        /// Breaks the polygon into triangles.
        /// </summary>
        /// <returns>The triangles covering the polygon, sharing its orientation.</returns>
        /// <remarks>
        /// The polygon is fanned from its first vertex. For a convex polygon this covers it exactly. For
        /// a concave one, some triangles reach outside the boundary and others overlap them with the
        /// opposite winding, so the signed contributions still cancel to the right total: area, centroid
        /// and volume come out correct. What a fan does not give is a set of triangles each of which lies
        /// inside the polygon, so it is not the right basis for a point-in-polygon test, and
        /// <c>Containment3</c> uses a winding count instead.
        /// </remarks>
        public GeoTriangle3[] Triangulate()
        {
            GeoTriangle3[] triangles = new GeoTriangle3[_vertices.Length - 2];

            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new GeoTriangle3(_vertices[0], _vertices[i + 1], _vertices[i + 2]);
            }

            return triangles;
        }

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this polygon.
        /// </summary>
        public GeoAabb3 GetAabb() => GeoAabb3.FromPoints(_vertices);

        /// <summary>
        /// Gets the boundary of this polygon as an open chain, with the closing vertex written out.
        /// </summary>
        public GeoPolyline3 ToPolyline()
        {
            GeoPoint3[] chain = new GeoPoint3[_vertices.Length + 1];
            Array.Copy(_vertices, chain, _vertices.Length);
            chain[_vertices.Length] = _vertices[0];

            return new GeoPolyline3(chain);
        }

        /// <summary>
        /// Applies a transformation to every vertex.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the transformation flattens the polygon so that it no longer encloses an area.
        /// </exception>
        public GeoPolygon3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoPoint3[] moved = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = transform.Transform(_vertices[i]);
            }

            // The public constructor revalidates, which is what is wanted here: a projection onto a plane
            // can collapse the polygon to a line, and a scaling can merge vertices, and both should be
            // reported rather than carried forward as a polygon with a stale normal.
            return new GeoPolygon3(moved);
        }

        #region Queries

        /// <summary>
        /// Locates a point relative to this polygon, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Containment3.Locate(this, point);

        /// <summary>
        /// Locates a point relative to this polygon, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => Containment3.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this polygon holds a point, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Containment3.Contains(this, point);

        /// <summary>
        /// Checks whether this polygon holds a point, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Containment3.Contains(this, point, tolerance);

        /// <summary>
        /// Checks whether a point lies on the boundary of this polygon, using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => Containment3.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on the boundary of this polygon, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Containment3.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a point.
        /// </summary>
        /// <remarks>
        /// The polygon counts as a filled surface, so a point above its interior is measured straight down
        /// to the surface rather than out to the nearest edge.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Gets the point on this polygon closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToPolygon(this, point);

        /// <summary>
        /// Tries to find the point where a line segment crosses this polygon, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(line, this, out intersection);

        /// <summary>
        /// Tries to find the point where a line segment crosses this polygon, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(line, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where a ray crosses this polygon, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(ray, this, out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses this polygon, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(ray, this, out intersection, tolerance);

        /// <summary>
        /// Splits this polygon by a plane, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolygon3[] above, out GeoPolygon3[] below) => Splition3.TrySplitBy(this, cutter, out above, out below);

        /// <summary>
        /// Splits this polygon by a plane, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolygon3[] above, out GeoPolygon3[] below, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out above, out below, tolerance);

        /// <summary>
        /// Splits this polygon along a chain drawn across it, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolyline3 cutLine, out GeoPolygon3[] pieces) => Splition3.TrySplitBy(this, cutLine, out pieces);

        /// <summary>
        /// Splits this polygon along a chain drawn across it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolyline3 cutLine, out GeoPolygon3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutLine, out pieces, tolerance);

        /// <summary>
        /// Splits this polygon by a solid, sorting the pieces into those inside it and those outside,
        /// using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoPolygon3[] inside, out GeoPolygon3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this polygon by a solid, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoPolygon3[] inside, out GeoPolygon3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another polygon has exactly the same vertices, in the same order.
        /// </summary>
        public bool Equals(GeoPolygon3 other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_vertices.Length != other._vertices.Length)
            {
                return false;
            }

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
        /// Determines whether the specified object is equal to the current polygon.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoPolygon3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                foreach (GeoPoint3 vertex in _vertices)
                {
                    hash = hash * 31 + vertex.GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Compares whether this polygon equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPolygon3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this polygon equals another within a tolerance.
        /// </summary>
        /// <remarks>
        /// A polygon is a loop, so the vertex a caller happened to start at carries no meaning and any
        /// rotation of the same loop counts as equal. The direction of travel does carry meaning, since
        /// it fixes which way the normal points, so a polygon and its <see cref="Flip"/> are not equal.
        /// </remarks>
        public bool IsEqualTo(GeoPolygon3 other, Tolerance tolerance)
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

        #endregion

        /// <summary>
        /// Returns a string that represents the current polygon.
        /// </summary>
        public override string ToString() => $"Polygon3(Vertices: {VertexCount}, Area: {Area:0.###})";
    }
}
