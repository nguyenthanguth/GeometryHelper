using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
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
    public sealed partial class GeoPolygon3 : IEquatable<GeoPolygon3>
    {
        private readonly GeoPoint3[] _vertices;
        private readonly GeoAabb3 _box;

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
        /// <para>
        /// Collinear means an area within the vector tolerance and no width: twice the area over the span, the longest
        /// side of three corners or the diagonal of the box of more, within the point tolerance. A square 0.002 by 0.002,
        /// of 4E-6 square units, is a polygon at the default tolerance, where it was refused; a triangle 0.003 long and
        /// 0.0006 high is still refused, and one 100 long and 0.0005 high, of 0.025 square units, still kept.
        /// </para>
        /// </remarks>
        public GeoPolygon3(IEnumerable<GeoPoint3> vertices)
            : this(vertices, Tolerance.Global) { }

        /// <summary>
        /// Initializes a new polygon from a sequence of coplanar vertices, within a tolerance.
        /// </summary>
        /// <param name="vertices">The vertices, in order around the loop.</param>
        /// <param name="tolerance">The tolerance deciding duplicate vertices and flatness.</param>
        public GeoPolygon3(IEnumerable<GeoPoint3> vertices, Tolerance tolerance)
            : this(vertices, tolerance, tolerance) { }

        /// <summary>
        /// Initializes a polygon whose vertices go only where they land within one tolerance of the one kept before them,
        /// or of the first at the end, checked flat and enclosing an area within another.
        /// </summary>
        /// <remarks>
        /// For the corners of a shape that stand apart already, a chain closed or a polygon transformed: at a merging
        /// tolerance of nought only a vertex on the very same point as the one before it goes, so a side shorter than the
        /// global tolerance stays, while the loop is still refused where it is not flat or encloses no area.
        /// </remarks>
        internal GeoPolygon3(IEnumerable<GeoPoint3> vertices, Tolerance merge, Tolerance tolerance)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            List<GeoPoint3> kept = new List<GeoPoint3>();

            foreach (GeoPoint3 vertex in vertices)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].IsEqualTo(vertex, merge))
                {
                    kept.Add(vertex);
                }
            }

            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], merge))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count < 3)
            {
                throw new ArgumentException("A polygon must have at least 3 distinct vertices.", nameof(vertices));
            }

            GeoPoint3[] loop = kept.ToArray();
            GeoVector3 areaVector = Newell.GetAreaVector(loop);

            // The length of this vector is the area of the loop, so the threshold it clears is EqualVector read as an area
            // rather than as a length. A loop enclosing less than that is kept all the same where it has width, as a
            // triangle has (GeoTriangle3.IsDegenerate): a polygon 0.002 by 0.002, of 4E-6 square units, is a polygon.
            if (
                !areaVector.TryGetNormal(out GeoVector3 normal, tolerance)
                && !TryGetNormalOfWidth(loop, areaVector, tolerance, out normal)
            )
            {
                throw new ArgumentException(
                    "A polygon must enclose an area; these vertices are collinear.",
                    nameof(vertices)
                );
            }

            GeoPlane3 carrier = new GeoPlane3(loop[0], normal);

            // Three corners share a plane whatever they are, so only a longer loop is measured against one. Measured, three
            // stand off the plane through them by rounding, which a planar tolerance of nought refuses.
            if (loop.Length > 3 && !carrier.ContainsAll(loop, tolerance))
            {
                throw new ArgumentException(
                    "A polygon must be flat; these vertices do not share a plane.",
                    nameof(vertices)
                );
            }

            _vertices = loop;
            _box = GeoAabb3.FromPoints(loop);
            Normal = normal;
            Area = areaVector.Length;
            Length = MeasurePerimeter(loop);
        }

        /// <summary>
        /// Initializes a new polygon directly from vertex arguments.
        /// </summary>
        public GeoPolygon3(params GeoPoint3[] vertices)
            : this((IEnumerable<GeoPoint3>)vertices) { }

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
            _box = GeoAabb3.FromPoints(validatedVertices);
            Normal = normal;
            Area = area;
            Length = MeasurePerimeter(validatedVertices);
        }

        /// <summary>
        /// Gives a polygon of corners already apart, with the normal and area they are known to have, checking nothing.
        /// </summary>
        /// <remarks>
        /// For the faces of a mesh, which keep slivers a tolerance would refuse and are known to be flat: what the public
        /// constructor would measure is measured already. And for the cap of a cut, which lies in the cutting plane and
        /// faces along its normal, where the normal of its own corners can stand off it.
        /// </remarks>
        internal static GeoPolygon3 FromValidated(GeoPoint3[] validatedVertices, GeoVector3 normal, double area) =>
            new GeoPolygon3(validatedVertices, normal, area);

        /// <summary>
        /// Gets the normal of a loop whose area is within the vector tolerance but which has width: twice its area over its
        /// span, the longest side of three corners or the diagonal of the box of more, beyond the point tolerance, and its
        /// area vector clear of the rounding of the sums it comes from.
        /// </summary>
        private static bool TryGetNormalOfWidth(
            GeoPoint3[] loop,
            GeoVector3 areaVector,
            Tolerance tolerance,
            out GeoVector3 normal
        )
        {
            normal = GeoVector3.Zero;

            double spanSquared;

            if (loop.Length == 3)
            {
                spanSquared = Math.Max(
                    loop[0].GetDistanceSquaredTo(loop[1]),
                    Math.Max(loop[1].GetDistanceSquaredTo(loop[2]), loop[2].GetDistanceSquaredTo(loop[0]))
                );
            }
            else
            {
                GeoAabb3 box = GeoAabb3.FromPoints(loop);
                spanSquared = box.Min.GetDistanceSquaredTo(box.Max);
            }

            double area = areaVector.Length;

            // Written as what the loop has to have, so that one of NaNs has no width.
            if (
                !(2.0 * area > tolerance.EqualPoint * Math.Sqrt(spanSquared))
                || !(area > WidthRounding * spanSquared)
                || double.IsInfinity(area)
            )
            {
                return false;
            }

            normal = new GeoVector3(areaVector.X / area, areaVector.Y / area, areaVector.Z / area);
            return true;
        }

        /// <summary>
        /// The share of the span squared within which the area of a loop is the rounding of the sums it comes from.
        /// </summary>
        private const double WidthRounding = 1E-15;

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
        public double GetDistanceAtParameter(double parameter) =>
            Parametrization3.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured around the boundary.
        /// </summary>
        public double GetParameterAtDistance(double distance) =>
            Parametrization3.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Gets the normalized parameter of the point on the boundary closest to the supplied point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => Parametrization3.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the normalized parameter of the point on the boundary closest to the supplied point, within
        /// a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance) =>
            Parametrization3.GetParameterAtPoint(this, point, tolerance);

        /// <summary>
        /// Gets the arc length from the first vertex to the point on the boundary closest to the supplied
        /// point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => Parametrization3.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the first vertex to the point on the boundary closest to the supplied
        /// point, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point, Tolerance tolerance) =>
            Parametrization3.GetDistanceAtPoint(this, point, tolerance);

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

                // The weight is an area, so the threshold it is judged against has to be one too: the rounding of the
                // sums an area comes from. Comparing against double.Epsilon instead would only catch a weight of
                // exactly zero, and a loop that doubles back on itself can leave one around 1e-300, small enough that
                // dividing by it overflows, large enough to pass the test.
                if (Math.Abs(totalWeight) <= GetAreaRoundingAtMost() && Math.Abs(totalWeight) <= GetAreaRounding())
                {
                    return _vertices[0];
                }

                return new GeoPoint3(x / totalWeight, y / totalWeight, z / totalWeight);
            }
        }

        /// <summary>
        /// Gets how far the area can stand off by the rounding of the sums it comes from, the fan of
        /// <see cref="Centroid"/> and the Newell sum of <see cref="Area"/> alike: an area no larger than this is none.
        /// </summary>
        internal double GetAreaRounding()
        {
            GeoPoint3 reference = _vertices[0];
            double products = 0.0;
            double run = 0.0;
            int n = _vertices.Length;

            for (int i = 0; i < n; i++)
            {
                GeoPoint3 from = _vertices[i];
                GeoPoint3 to = _vertices[(i + 1) % n];
                double x1 = from.X - reference.X;
                double y1 = from.Y - reference.Y;
                double z1 = from.Z - reference.Z;
                double x2 = to.X - reference.X;
                double y2 = to.Y - reference.Y;
                double z2 = to.Z - reference.Z;
                products +=
                    Math.Abs(y1 * z2)
                    + Math.Abs(z1 * y2)
                    + Math.Abs(z1 * x2)
                    + Math.Abs(x1 * z2)
                    + Math.Abs(x1 * y2)
                    + Math.Abs(y1 * x2);
                run += Math.Abs(x2 - x1) + Math.Abs(y2 - y1) + Math.Abs(z2 - z1);
            }

            // The Newell sum works from the coordinates themselves, so their rounding counts once a corner, every
            // corner within the run round the polygon of the first.
            double far = Math.Max(Math.Abs(reference.X), Math.Max(Math.Abs(reference.Y), Math.Abs(reference.Z))) + run;

            return 0.5 * GeoPolygon2.HalfUlp * (n + 4) * (products + 2.0 * far * run);
        }

        /// <summary>
        /// Gets no less than <see cref="GetAreaRounding"/>, read from the box rather than from every corner: an area
        /// larger than this is more than rounding, and the corners need not be walked to say so.
        /// </summary>
        internal double GetAreaRoundingAtMost()
        {
            double span = Math.Max(_box.Max.X - _box.Min.X, Math.Max(_box.Max.Y - _box.Min.Y, _box.Max.Z - _box.Min.Z));
            double far = Math.Max(
                Math.Max(Math.Abs(_box.Min.X), Math.Abs(_box.Max.X)),
                Math.Max(
                    Math.Max(Math.Abs(_box.Min.Y), Math.Abs(_box.Max.Y)),
                    Math.Max(Math.Abs(_box.Min.Z), Math.Abs(_box.Max.Z))
                )
            );
            int n = _vertices.Length;

            // No coordinate measured from the first corner is longer than the span, so no product is larger than its
            // square and no side longer than three spans; twice that over, so the rounding of the box's own sums is
            // covered too.
            double run = 3.0 * n * span;

            return GeoPolygon2.HalfUlp * (n + 4) * (6.0 * n * span * span + 2.0 * (far + run) * run);
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
        /// Breaks the polygon into faces of a kind, its grid level, using the default tolerance. A grid needs the size of its
        /// cells, which <see cref="ToMesh(Meshing.MeshOptions)"/> takes.
        /// </summary>
        /// <param name="kind">The kind of faces.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        /// <exception cref="ArgumentException">Thrown for <see cref="Meshing.MeshKind.Grid"/>, which needs the size of its cells.</exception>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshKind kind) =>
            Meshing.Mesh3.ToMesh(this, Meshing.Mesh2.OptionsFor(kind), Meshing.MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks the polygon into faces as the options say, its grid level, with the second axis up the slope, using the
        /// default tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options) =>
            Meshing.Mesh3.ToMesh(this, options, Meshing.MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks the polygon into faces as the options say, its grid level, with the second axis up the slope, within a
        /// tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options, Tolerance tolerance) =>
            Meshing.Mesh3.ToMesh(this, options, Meshing.MeshPlacement3.World, tolerance);

        /// <summary>
        /// Breaks the polygon into faces as the options say, its grid standing as the placement says, using the default
        /// tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the plane, and where a cell starts.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options, Meshing.MeshPlacement3 placement) =>
            Meshing.Mesh3.ToMesh(this, options, placement, Tolerance.Global);

        /// <summary>
        /// Breaks the polygon into faces as the options say, its grid standing as the placement says, within a tolerance:
        /// triangles, the cells of a grid, strips or convex pieces, each a simple polygon with no hole, counter-clockwise about
        /// the normal, meeting its neighbours edge to edge.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the plane, and where a cell starts.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the options or the placement are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the options give an origin of the plane, a grid's cell is no larger than the point tolerance, the grid
        /// would lay more cells over the shape than a mesh may have, or the placement's direction stands square to the plane.
        /// </exception>
        /// <remarks>
        /// A polygon crossing itself is read as <see cref="GeoPolygon2.MakeValid()"/> reads it, laid out in its plane.
        /// </remarks>
        public Meshing.GeoMesh3 ToMesh(
            Meshing.MeshOptions options,
            Meshing.MeshPlacement3 placement,
            Tolerance tolerance
        ) => Meshing.Mesh3.ToMesh(this, options, placement, tolerance);

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this polygon.
        /// </summary>
        /// <remarks>
        /// Measured once, when the polygon is made, as its normal and area are: the polygon cannot change, and
        /// the box is asked for over and over — by every face of a body, every time a point is placed against
        /// it or a plane cuts it.
        /// </remarks>
        public GeoAabb3 GetAabb() => _box;

        /// <summary>
        /// Gets the boundary of this polygon as an open chain, with the closing vertex written out.
        /// </summary>
        /// <remarks>
        /// The chain runs side for side, however short a side: at a millionth, the triangle (0, 0, 0), (10, 0, 0),
        /// (10, 0, 10) with every corner cut by 0.0003 is a chain of seven vertices, where it was put back together within
        /// the global tolerance of a thousandth and came back four.
        /// </remarks>
        public GeoPolyline3 ToPolyline()
        {
            GeoPoint3[] chain = new GeoPoint3[_vertices.Length + 1];
            Array.Copy(_vertices, chain, _vertices.Length);
            chain[_vertices.Length] = _vertices[0];

            // The polygon's own sides, every one apart already.
            return new GeoPolyline3(chain, true);
        }

        /// <summary>
        /// Moves the polygon by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The polygon in its new place.</returns>
        public GeoPolygon3 Translate(GeoVector3 vector) => TransformBy(GeoTransform3.Translation(vector));

        /// <summary>
        /// Applies a transformation to every vertex.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the transformation flattens the polygon so that it no longer encloses an area.
        /// </exception>
        /// <remarks>
        /// The polygon is transformed side for side, however short a side: at a millionth, the triangle (0, 0, 0),
        /// (10, 0, 0), (10, 0, 10) with every corner cut by 0.0003 keeps its six corners scaled by 2 or sheared, where it was
        /// put back together within the global tolerance of a thousandth and came back three. Only a corner the
        /// transformation lands on the very same point as the one before it goes. An even scaling, turned, moved or
        /// mirrored besides, carries the polygon as a move does, its area scaled: a square 100 across with a corner 0.0009
        /// off flat scaled by 10 is kept, where it was refused as not flat, and the cut triangle shrunk by a ten-thousandth
        /// keeps its six corners, 0.0007 wide. A transformation that stretches one way more than another or shears has the
        /// polygon checked flat and enclosing an area within the global tolerance loosened as far as the transformation
        /// stretches any direction: off flat within ten times the tolerance under a stretch by 10, and wide within half of
        /// it under a scaling by 0.2, 0.25 and 0.5, which keeps a square 0.003 across as one 0.0006 by 0.00075. Neither is made finer than the global tolerance, so what was kept
        /// before is kept still. A projection that flattens the polygon to a line is still refused.
        /// </remarks>
        public GeoPolygon3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return TransformBy(transform, transform.ReadCarrying());
        }

        /// <summary>
        /// Applies a transformation whose carrying is read already, as a face or a body reads it once for all its polygons.
        /// </summary>
        internal GeoPolygon3 TransformBy(GeoTransform3 transform, in GeoTransform3.Carrying carrying)
        {
            GeoPoint3[] moved = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = transform.Transform(_vertices[i]);
            }

            // A move that keeps every length — a turn, a shift, a mirror — keeps the polygon as flat as it was and
            // its area whole, so it is carried over rather than measured again: measured again, a polygon at the
            // very edge of the planar tolerance, as a modeller's cut leaves one, could land a rounding past it at
            // its new place and be refused, and the body it belongs to with it. A girder's face a hundredth off
            // flat was, moved 600 m out.
            if (carrying.Kind == GeoTransform3.Carrying.Rigid)
            {
                GeoVector3 area = Newell.GetAreaVector(moved);

                return new GeoPolygon3(moved, area.Divide(area.Length), Area);
            }

            // An even scaling, turned, shifted or mirrored besides, scales every length alike: what lay flat within a
            // tolerance and had width lies flat and has width within the tolerance scaled with it, so it is carried as a move
            // is, its area scaled. Measured again within the global tolerance, a top 0.0009 off flat scaled by 10 was refused
            // as not flat, and a sliver 0.00005 wide built at a millionth and scaled by 0.1 as enclosing no area.
            if (carrying.Kind == GeoTransform3.Carrying.Even)
            {
                GeoVector3 area = Newell.GetAreaVector(moved);

                return new GeoPolygon3(moved, area.Divide(area.Length), Area * carrying.AreaScale);
            }

            // Anything else is measured again, which is what is wanted there: a projection onto a plane can collapse the
            // polygon to a line, and that should be reported rather than carried forward as a polygon with a stale normal.
            // It is measured within the global tolerance stretched as far as the transformation stretches any direction,
            // as far as it can carry a corner off the plane. The corners stand apart already, however near; only two the
            // transformation lands on the very same point become one.
            return new GeoPolygon3(moved, default(Tolerance), carrying.Stretched);
        }

        /// <summary>
        /// Moves the polygon coordinate by coordinate, the offset's X, Y and Z added to each corner's own, with no matrix: a
        /// corner moved by an offset it is exactly apart from comes out exact, and moved back comes out as it was given.
        /// </summary>
        /// <param name="offset">How far to move it, and which way.</param>
        /// <param name="measure">
        /// Whether the normal and the area are measured again on the corners moved, as a polygon brought in from far out
        /// reads them finer; or kept, as one measured near the origin and moved back out keeps them.
        /// </param>
        internal GeoPolygon3 MovedBy(GeoVector3 offset, bool measure)
        {
            GeoPoint3[] moved = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = _vertices[i].Add(offset);
            }

            if (measure)
            {
                GeoVector3 area = Newell.GetAreaVector(moved);
                double length = area.Length;

                if (length > 0.0)
                {
                    return new GeoPolygon3(moved, area.Divide(length), length);
                }
            }

            return new GeoPolygon3(moved, Normal, Area);
        }

        #region Queries

        /// <summary>
        /// Locates a point relative to this polygon, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Containment3.Locate(this, point);

        /// <summary>
        /// Locates a point relative to this polygon, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) =>
            Containment3.Locate(this, point, tolerance);

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
        /// Splits this polygon by a plane, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolygon3[] above, out GeoPolygon3[] below) =>
            Splition3.TrySplitBy(this, cutter, out above, out below);

        /// <summary>
        /// Splits this polygon by a plane, within a tolerance.
        /// </summary>
        public bool TrySplitBy(
            GeoPlane3 cutter,
            out GeoPolygon3[] above,
            out GeoPolygon3[] below,
            Tolerance tolerance
        ) => Splition3.TrySplitBy(this, cutter, out above, out below, tolerance);

        /// <summary>
        /// Splits this polygon along a chain drawn across it, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolyline3 cutLine, out GeoPolygon3[] pieces) =>
            Splition3.TrySplitBy(this, cutLine, out pieces);

        /// <summary>
        /// Splits this polygon along a chain drawn across it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolyline3 cutLine, out GeoPolygon3[] pieces, Tolerance tolerance) =>
            Splition3.TrySplitBy(this, cutLine, out pieces, tolerance);

        /// <summary>
        /// Splits this polygon by a solid, sorting the pieces into those inside it and those outside,
        /// using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoPolygon3[] inside, out GeoPolygon3[] outside) =>
            Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this polygon by a solid, within a tolerance.
        /// </summary>
        public bool TrySplitBy(
            GeoSolid3 cutter,
            out GeoPolygon3[] inside,
            out GeoPolygon3[] outside,
            Tolerance tolerance
        ) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Checks whether this polygon is simple, using the default tolerance: no edge crosses or touches another
        /// except where neighbours share their vertex.
        /// </summary>
        public bool IsSimple() => Intersection3.IsSimple(this);

        /// <summary>
        /// Checks whether this polygon is simple, within a tolerance.
        /// </summary>
        public bool IsSimple(Tolerance tolerance) => Intersection3.IsSimple(this, tolerance);

        #endregion

        #region Shaping

        /// <summary>
        /// Rounds every corner of the loop by the same radius.
        /// </summary>
        /// <remarks>
        /// A rounded corner is an arc, so the answer is a <see cref="GeoPolygonArc3"/>: a <see cref="GeoPolygon3"/> cannot hold it.
        /// </remarks>
        public GeoPolygonArc3 Fillet(double radius) => Corner3.Fillet(this, radius);

        /// <summary>
        /// Rounds every corner of the loop by the same radius, within a tolerance.
        /// </summary>
        public GeoPolygonArc3 Fillet(double radius, Tolerance tolerance) => Corner3.Fillet(this, radius, tolerance);

        /// <summary>
        /// Rounds the corners of the loop, one radius each.
        /// </summary>
        public GeoPolygonArc3 Fillet(System.Collections.Generic.IReadOnlyList<double> radii) =>
            Corner3.Fillet(this, radii);

        /// <summary>
        /// Rounds the corners of the loop, one radius each, within a tolerance.
        /// </summary>
        public GeoPolygonArc3 Fillet(System.Collections.Generic.IReadOnlyList<double> radii, Tolerance tolerance) =>
            Corner3.Fillet(this, radii, tolerance);

        /// <summary>
        /// Rounds one corner of the loop; every vertex of a loop is a corner.
        /// </summary>
        public bool TryFilletAt(int index, double radius, out GeoPolygonArc3 result) =>
            Corner3.TryFilletAt(this, index, radius, out result);

        /// <summary>
        /// Rounds one corner of the loop within a tolerance; every vertex of a loop is a corner.
        /// </summary>
        public bool TryFilletAt(int index, double radius, out GeoPolygonArc3 result, Tolerance tolerance) =>
            Corner3.TryFilletAt(this, index, radius, out result, tolerance);

        /// <summary>
        /// Cuts every corner of the loop back by the same distance.
        /// </summary>
        /// <remarks>A loop is flat, so cutting its corners keeps it flat and the answer is a loop of the same sort.</remarks>
        public GeoPolygon3 Chamfer(double distance) => Corner3.Chamfer(this, distance);

        /// <summary>
        /// Cuts every corner of the loop back by the same distance, within a tolerance.
        /// </summary>
        public GeoPolygon3 Chamfer(double distance, Tolerance tolerance) => Corner3.Chamfer(this, distance, tolerance);

        /// <summary>
        /// Cuts every corner of the loop back, by one distance along the way in and another along the way out.
        /// </summary>
        public GeoPolygon3 Chamfer(double distance1, double distance2) => Corner3.Chamfer(this, distance1, distance2);

        /// <summary>
        /// Cuts every corner of the loop back, by one distance along the way in and another along the way out,
        /// within a tolerance.
        /// </summary>
        public GeoPolygon3 Chamfer(double distance1, double distance2, Tolerance tolerance) =>
            Corner3.Chamfer(this, distance1, distance2, tolerance);

        /// <summary>
        /// Cuts one corner of the loop back; every vertex of a loop is a corner.
        /// </summary>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolygon3 result) =>
            Corner3.TryChamferAt(this, index, distance1, distance2, out result);

        /// <summary>
        /// Cuts one corner of the loop back within a tolerance; every vertex of a loop is a corner.
        /// </summary>
        public bool TryChamferAt(
            int index,
            double distance1,
            double distance2,
            out GeoPolygon3 result,
            Tolerance tolerance
        ) => Corner3.TryChamferAt(this, index, distance1, distance2, out result, tolerance);

        #endregion

        #region Offsetting
        /// <summary>
        /// Grows this polygon within its own plane by a distance, or shrinks it when the distance is negative,
        /// with sharp corners, using the default tolerance. See
        /// <see cref="Offset3.Offset(GeoPolygon3, double, OffsetOptions, Tolerance)"/>.
        /// </summary>
        /// <param name="distance">How far the boundary moves: outward when positive, inward when negative.</param>
        /// <returns>The boundary loops of the offset region: none, one, or several; holes facing the other way.</returns>
        public GeoPolygon3[] Offset(double distance) => Offset3.Offset(this, distance);

        /// <summary>
        /// Grows this polygon within its own plane by a distance, or shrinks it when the distance is negative,
        /// with sharp corners, within a tolerance.
        /// </summary>
        public GeoPolygon3[] Offset(double distance, Tolerance tolerance) => Offset3.Offset(this, distance, tolerance);

        /// <summary>
        /// Grows this polygon within its own plane by a distance, or shrinks it when the distance is negative,
        /// with the given corners, using the default tolerance.
        /// </summary>
        public GeoPolygon3[] Offset(double distance, OffsetJoin join) => Offset3.Offset(this, distance, join);

        /// <summary>
        /// Grows this polygon within its own plane by a distance, or shrinks it when the distance is negative,
        /// with the given corners, within a tolerance.
        /// </summary>
        public GeoPolygon3[] Offset(double distance, OffsetJoin join, Tolerance tolerance) =>
            Offset3.Offset(this, distance, join, tolerance);

        /// <summary>
        /// Grows this polygon within its own plane by a distance, or shrinks it when the distance is negative, as
        /// the options say, using the default tolerance.
        /// </summary>
        public GeoPolygon3[] Offset(double distance, OffsetOptions options) => Offset3.Offset(this, distance, options);

        /// <summary>
        /// Grows this polygon within its own plane by a distance, or shrinks it when the distance is negative, as
        /// the options say, within a tolerance.
        /// </summary>
        public GeoPolygon3[] Offset(double distance, OffsetOptions options, Tolerance tolerance) =>
            Offset3.Offset(this, distance, options, tolerance);

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

        /// <summary>
        /// Gets the frame of this polygon: its plane, with the origin at the first vertex and the first axis
        /// along the first edge that has a direction, so the frame turns with the polygon.
        /// </summary>
        /// <returns>The frame, with its Z axis along the polygon normal.</returns>
        public GeoCoordinateSystem3 GetFrame() => PlanarMap.FrameOf(this);

        /// <summary>
        /// Lays this polygon out in a frame, dropping each vertex's distance from the plane of that frame.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The polygon in the plane of the frame.</returns>
        public GeoPolygon2 ProjectToPolygon2(GeoCoordinateSystem3 frame) => PlanarMap.ProjectToPolygon2(frame, this);

        /// <summary>
        /// Gets the polygon running the other way round.
        /// </summary>
        /// <returns>The same shape, walked in the other direction.</returns>
        /// <remarks>
        /// Which way a loop runs is what tells its inside from its outside to anything that reads winding,
        /// and it is what decides which way round a pair of chamfer distances goes.
        /// <para>
        /// The polygon is reversed corner for corner, however short a side, and however far off flat the tolerance it was
        /// built within let it lie: at a millionth, the triangle (0, 0, 0), (10, 0, 0), (10, 0, 10) with every corner cut by
        /// 0.0003 reverses to its six corners, where it was put back together within the global tolerance of a thousandth
        /// and came back three, and a square 100 across built at a hundredth with one corner 0.005 off the plane of the
        /// others reverses, where it was refused as not flat.
        /// </para>
        /// </remarks>
        public GeoPolygon3 Reverse()
        {
            var turned = new GeoPoint3[VertexCount];

            for (int i = 0; i < VertexCount; i++)
            {
                turned[i] = this[VertexCount - 1 - i];
            }

            // The same corners, walked the other way: there is nothing to merge and nothing to check. The normal and the
            // area are those of the corners walked this way, as the constructor measures them; a loop that measures no
            // area keeps its own, turned over.
            GeoVector3 areaVector = Newell.GetAreaVector(turned);
            double area = areaVector.Length;

            return area > 0.0
                ? new GeoPolygon3(turned, areaVector.Divide(area), area)
                : new GeoPolygon3(turned, Normal.Negate(), Area);
        }
    }
}
