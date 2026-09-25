using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// An open chain in space whose pieces may be arcs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The twin of <see cref="GeoPolylineArc2"/>, laid out the way a drawing holds one: vertices, with the
    /// bulge of the edge <em>leaving</em> each, and now the plane that bulge is read in as well. A chain
    /// like this is what a reinforcing bar is: straight runs with a tangent arc at every bend, and the
    /// bends need not all lie in one plane.
    /// </para>
    /// <para>
    /// Coplanarity is <b>not</b> required, exactly as <see cref="GeoPolyline3"/> requires none. A chain
    /// that does happen to be flat says so through <see cref="IsPlanar()"/>, and a closed loop of arcs that
    /// is flat belongs in <see cref="GeoPolygonArc3"/> instead, which enforces it and can therefore answer
    /// about area and about what is inside.
    /// </para>
    /// <para>
    /// What is exact here is what can be worked out on the curve itself: its length, walking along it,
    /// the point of it nearest another point, its box, moving it, turning it round, and flattening it.
    /// Measuring it against another shape in space is not, because the distance from an arc to anything but
    /// a point has no closed form once the two are not in one plane. For that, sample it with
    /// <see cref="ToPolyline3(double)"/>, which puts the accuracy in the call.
    /// </para>
    /// </remarks>
    public sealed class GeoPolylineArc3 : IEquatable<GeoPolylineArc3>
    {
        private readonly GeoPoint3[] _vertices;
        private readonly double[] _bulges;
        private readonly GeoVector3[] _normals;

        /// <summary>
        /// Gets the vertices of the chain, in order.
        /// </summary>
        public IReadOnlyList<GeoPoint3> Vertices => _vertices;

        /// <summary>
        /// Gets the bulge of each edge: the one at an index belongs to the edge leaving that vertex, so the
        /// last vertex of an open chain has none.
        /// </summary>
        public IReadOnlyList<double> Bulges => _bulges;

        /// <summary>
        /// Gets the normal each bulge is read about, laid out the way the bulges are; of no length at all
        /// where the edge is straight.
        /// </summary>
        public IReadOnlyList<GeoVector3> Normals => _normals;

        /// <summary>
        /// Initializes a chain of straight pieces.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least two, and no two neighbours in the same place.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct vertices are given.</exception>
        public GeoPolylineArc3(IEnumerable<GeoPoint3> vertices)
            : this(vertices, null, null)
        {
        }

        /// <summary>
        /// Initializes a chain whose pieces may be arcs.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least two, and no two neighbours in the same place.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex; null or short means the rest are straight, and anything past the last edge is ignored.</param>
        /// <param name="normals">The plane each bulge is read about, laid out the way the bulges are; only wanted where a bulge is not nought.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct vertices are given, or when a bulge that is not nought comes with no usable plane.</exception>
        public GeoPolylineArc3(IEnumerable<GeoPoint3> vertices, IEnumerable<double> bulges, IEnumerable<GeoVector3> normals)
            : this(vertices, bulges, normals, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes a chain whose pieces may be arcs, within a tolerance.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least two, and no two neighbours in the same place.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex; null or short means the rest are straight.</param>
        /// <param name="normals">The plane each bulge is read about; only wanted where a bulge is not nought.</param>
        /// <param name="tolerance">The tolerance, which decides when two vertices are in the same place and how square to its chord a normal must be.</param>
        public GeoPolylineArc3(
            IEnumerable<GeoPoint3> vertices,
            IEnumerable<double> bulges,
            IEnumerable<GeoVector3> normals,
            Tolerance tolerance)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));

            var kept = new List<GeoPoint3>();
            var keptBulges = new List<double>();
            var keptNormals = new List<GeoVector3>();

            var givenBulges = bulges == null ? new List<double>() : new List<double>(bulges);
            var givenNormals = normals == null ? new List<GeoVector3>() : new List<GeoVector3>(normals);

            int index = 0;

            foreach (GeoPoint3 vertex in vertices)
            {
                double bulge = index < givenBulges.Count ? givenBulges[index] : 0.0;
                GeoVector3 normal = index < givenNormals.Count ? givenNormals[index] : default(GeoVector3);
                index++;

                // Two vertices in the same place leave an edge with no length, which no arc and no segment
                // can be drawn along; the later one goes, as GeoPolyline3 drops it too.
                if (kept.Count > 0 && kept[kept.Count - 1].IsEqualTo(vertex, tolerance))
                {
                    continue;
                }

                kept.Add(vertex);
                keptBulges.Add(bulge);
                keptNormals.Add(normal);
            }

            if (kept.Count < 2)
            {
                throw new ArgumentException("A chain needs at least 2 distinct vertices.", nameof(vertices));
            }

            _vertices = kept.ToArray();
            _bulges = keptBulges.ToArray();
            _normals = keptNormals.ToArray();

            // Building every edge once here is what turns a bad plane into an exception at construction
            // rather than into a wrong answer somewhere later on.
            for (int i = 0; i < EdgeCount; i++)
            {
                GeoEdge3 edge = new GeoEdge3(_vertices[i], _vertices[i + 1], _bulges[i], _normals[i], tolerance);

                _bulges[i] = edge.Bulge;
                _normals[i] = edge.Normal;
            }
        }

        /// <summary>
        /// Initializes a chain from edges that run end to end.
        /// </summary>
        /// <param name="edges">The edges, each starting where the one before it ended.</param>
        /// <exception cref="ArgumentNullException">Thrown when the edges are null.</exception>
        /// <exception cref="ArgumentException">Thrown when no edges are given, or one does not start where the one before it ended.</exception>
        public GeoPolylineArc3(IEnumerable<GeoEdge3> edges)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));

            var vertices = new List<GeoPoint3>();
            var bulges = new List<double>();
            var normals = new List<GeoVector3>();

            foreach (GeoEdge3 edge in edges)
            {
                if (vertices.Count == 0)
                {
                    vertices.Add(edge.StartPoint);
                }
                else if (!vertices[vertices.Count - 1].IsEqualTo(edge.StartPoint))
                {
                    throw new ArgumentException("The edges of a chain must run end to end.", nameof(edges));
                }

                bulges.Add(edge.Bulge);
                normals.Add(edge.Normal);
                vertices.Add(edge.EndPoint);
            }

            if (vertices.Count < 2)
            {
                throw new ArgumentException("A chain needs at least one edge.", nameof(edges));
            }

            bulges.Add(0.0);
            normals.Add(default(GeoVector3));

            _vertices = vertices.ToArray();
            _bulges = bulges.ToArray();
            _normals = normals.ToArray();
        }

        /// <summary>
        /// Initializes a chain from a straight one, with no bulges.
        /// </summary>
        /// <param name="polyline">The straight chain to widen.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <remarks>
        /// Widening loses nothing: every edge simply carries a bulge of zero. It is <see cref="Flatten()"/>
        /// going the other way that throws the curves away.
        /// </remarks>
        public GeoPolylineArc3(GeoPolyline3 polyline)
            : this(Required(polyline).Vertices, null, null)
        {
        }

        /// <summary>
        /// Gets the number of vertices.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets the number of edges, which is one fewer than the number of vertices.
        /// </summary>
        public int EdgeCount => _vertices.Length - 1;

        /// <summary>
        /// Gets the vertex at an index.
        /// </summary>
        public GeoPoint3 this[int index] => _vertices[index];

        /// <summary>
        /// Gets the point the chain starts from.
        /// </summary>
        public GeoPoint3 StartPoint => _vertices[0];

        /// <summary>
        /// Gets the point the chain ends at.
        /// </summary>
        public GeoPoint3 EndPoint => _vertices[_vertices.Length - 1];

        /// <summary>
        /// Gets the bulge of the edge leaving a vertex.
        /// </summary>
        public double GetBulgeAt(int index) => _bulges[index];

        /// <summary>
        /// Gets the plane the bulge leaving a vertex is read about.
        /// </summary>
        public GeoVector3 GetNormalAt(int index) => _normals[index];

        /// <summary>
        /// Gets the edge at an index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no edge at that index.</exception>
        public GeoEdge3 GetEdgeAt(int index)
        {
            if (index < 0 || index >= EdgeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "The chain has no edge at that index.");
            }

            return _bulges[index] == 0.0
                ? new GeoEdge3(_vertices[index], _vertices[index + 1])
                : new GeoEdge3(_vertices[index], _vertices[index + 1], _bulges[index], _normals[index]);
        }

        /// <summary>
        /// Gets the edges of the chain, in order.
        /// </summary>
        public IEnumerable<GeoEdge3> GetEdges()
        {
            for (int i = 0; i < EdgeCount; i++)
            {
                yield return GetEdgeAt(i);
            }
        }

        /// <summary>
        /// Gets the length of the chain, measured along its arcs rather than across their chords.
        /// </summary>
        public double Length => ArcChain3.LengthOf(ArcChain3.EdgesOf(this));

        /// <summary>
        /// Gets the point a given way along the chain, from nought at the start to one at the end.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => GetPointAtDistance(GetDistanceAtParameter(parameter));

        /// <summary>
        /// Gets the point a given distance along the chain, walked along its arcs.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => ArcChain3.PointAtDistance(ArcChain3.EdgesOf(this), distance);

        /// <summary>
        /// Gets the distance along the chain at a given parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter)
        {
            double held = parameter < 0.0 ? 0.0 : parameter > 1.0 ? 1.0 : parameter;

            return held * Length;
        }

        /// <summary>
        /// Gets the parameter at a given distance along the chain.
        /// </summary>
        public double GetParameterAtDistance(double distance)
        {
            double length = Length;

            if (length <= 0.0)
            {
                return 0.0;
            }

            double held = distance < 0.0 ? 0.0 : distance > length ? length : distance;

            return held / length;
        }

        /// <summary>
        /// Gets the distance along the chain to the point on it nearest another point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => GetDistanceAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance along the chain to the point on it nearest another point, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point, Tolerance tolerance)
            => ArcChain3.DistanceAtPoint(ArcChain3.EdgesOf(this), point, tolerance);

        /// <summary>
        /// Gets the parameter of the point on the chain nearest another point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the parameter of the point on the chain nearest another point, within a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance)
            => GetParameterAtDistance(GetDistanceAtPoint(point, tolerance));

        /// <summary>
        /// Gets the point of the chain nearest another point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the chain nearest another point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Measured on the arcs, so a point sitting over the middle of a bulge is answered with a point on
        /// the bulge and not with one on the chord across it.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance)
            => ArcChain3.ClosestPoint(ArcChain3.EdgesOf(this), point, tolerance);

        /// <summary>
        /// Gets the distance from the chain to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the chain to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));

        /// <summary>
        /// Determines whether a point lies on the chain.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on the chain, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Says where a point sits relative to the chain; a curve never answers Inside.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Says where a point sits relative to the chain, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance)
            => IsPointOn(point, tolerance) ? PointLocation.OnSide : PointLocation.OutSide;

        /// <summary>
        /// Gets the smallest axis-aligned box holding the chain.
        /// </summary>
        /// <remarks>
        /// Each arc is boxed by the arc and not by its chord, so a bulge reaching out past its ends is
        /// inside the box that comes back.
        /// </remarks>
        public GeoAabb3 GetAabb()
        {
            GeoAabb3 box = GetEdgeAt(0).GetAabb();

            for (int i = 1; i < EdgeCount; i++)
            {
                box = box.Union(GetEdgeAt(i).GetAabb());
            }

            return box;
        }

        /// <summary>
        /// Determines whether the whole chain, arcs and all, lies in one plane.
        /// </summary>
        public bool IsPlanar() => IsPlanar(Tolerance.Global);

        /// <summary>
        /// Determines whether the whole chain, arcs and all, lies in one plane, within a tolerance.
        /// </summary>
        public bool IsPlanar(Tolerance tolerance) => TryGetPlane(out _, tolerance);

        /// <summary>
        /// Gets the plane the whole chain lies in, when it lies in one.
        /// </summary>
        public bool TryGetPlane(out GeoPlane3 plane) => TryGetPlane(out plane, Tolerance.Global);

        /// <summary>
        /// Gets the plane the whole chain lies in, when it lies in one, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane, when there is one.</param>
        /// <param name="tolerance">The tolerance; the planar threshold decides how flat is flat enough.</param>
        /// <returns>true if one plane holds every vertex and every arc; otherwise, false.</returns>
        /// <remarks>
        /// The vertices alone are not enough to settle it: two straight runs and the bends between them can
        /// all share a plane while an arc bulges out of it, so every arc is asked whether its own plane is
        /// the same one.
        /// </remarks>
        public bool TryGetPlane(out GeoPlane3 plane, Tolerance tolerance)
        {
            if (!new GeoPolyline3(_vertices).TryGetPlane(out plane, tolerance))
            {
                return false;
            }

            for (int i = 0; i < EdgeCount; i++)
            {
                if (_bulges[i] == 0.0)
                {
                    continue;
                }

                if (!_normals[i].IsParallelTo(plane.Normal, tolerance))
                {
                    plane = default(GeoPlane3);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the chain with its arcs thrown away, leaving the chords.
        /// </summary>
        /// <remarks>
        /// This is the one operation here that loses something, and it loses it in the obvious way: each
        /// arc becomes the straight line between its ends. <see cref="ToPolyline3(double)"/> keeps the shape
        /// to whatever accuracy is asked for instead.
        /// </remarks>
        public GeoPolyline3 Flatten() => new GeoPolyline3(_vertices);

        /// <summary>
        /// Gets the chain as a straight one following its arcs to a given accuracy.
        /// </summary>
        /// <param name="chordTolerance">How far the straight pieces may fall inside an arc.</param>
        /// <returns>A straight chain no point of which stands further than the chord tolerance from this one.</returns>
        /// <remarks>
        /// This is how to measure a curved chain against anything but a point: the distance from an arc in
        /// space to a segment, to another arc or to a body has no closed form, so the library does not
        /// pretend to one. Sampling here puts the accuracy in the call, where it can be seen.
        /// </remarks>
        public GeoPolyline3 ToPolyline3(double chordTolerance)
        {
            if (double.IsNaN(chordTolerance) || chordTolerance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(chordTolerance), "A chord tolerance has to be a positive number.");
            }

            var points = new List<GeoPoint3> { StartPoint };

            for (int i = 0; i < EdgeCount; i++)
            {
                GeoEdge3 edge = GetEdgeAt(i);

                if (!edge.IsArc)
                {
                    points.Add(edge.EndPoint);
                    continue;
                }

                GeoPolyline3 sampled = edge.ToArc().ToPolylineByChordTolerance(chordTolerance);

                for (int j = 1; j < sampled.VertexCount; j++)
                {
                    points.Add(sampled[j]);
                }
            }

            return new GeoPolyline3(points);
        }

        /// <summary>
        /// Rounds every corner of this chain by the same radius.
        /// </summary>
        /// <remarks>
        /// This is how a reinforcing bar is built: the points it turns at, and one bending radius. Each
        /// corner is rounded in the plane of its own two legs, so the bends need not share a plane, and a
        /// corner with too little edge to give, or none to turn, is left as it was.
        /// </remarks>
        public GeoPolylineArc3 Fillet(double radius) => Corner3.Fillet(this, radius);

        /// <summary>
        /// Rounds every corner of this chain by the same radius, within a tolerance.
        /// </summary>
        public GeoPolylineArc3 Fillet(double radius, Tolerance tolerance) => Corner3.Fillet(this, radius, tolerance);

        /// <summary>
        /// Rounds the corners of this chain, each by its own radius.
        /// </summary>
        /// <param name="radii">The radius wanted at each vertex, read the way the bulges are read. A radius of nought leaves that corner alone, and a short list leaves the rest of the chain alone.</param>
        public GeoPolylineArc3 Fillet(System.Collections.Generic.IReadOnlyList<double> radii) => Corner3.Fillet(this, radii);

        /// <summary>
        /// Rounds the corners of this chain, each by its own radius, within a tolerance.
        /// </summary>
        public GeoPolylineArc3 Fillet(System.Collections.Generic.IReadOnlyList<double> radii, Tolerance tolerance) => Corner3.Fillet(this, radii, tolerance);

        /// <summary>
        /// Rounds one named corner of this chain.
        /// </summary>
        public bool TryFilletAt(int index, double radius, out GeoPolylineArc3 result) => Corner3.TryFilletAt(this, index, radius, out result);

        /// <summary>
        /// Rounds one named corner of this chain, within a tolerance.
        /// </summary>
        public bool TryFilletAt(int index, double radius, out GeoPolylineArc3 result, Tolerance tolerance)
            => Corner3.TryFilletAt(this, index, radius, out result, tolerance);

        /// <summary>
        /// Gets the chain walked the other way, drawing the same curve.
        /// </summary>
        public GeoPolylineArc3 Reverse()
        {
            var edges = new List<GeoEdge3>();

            for (int i = EdgeCount - 1; i >= 0; i--)
            {
                edges.Add(GetEdgeAt(i).Reverse());
            }

            return new GeoPolylineArc3(edges);
        }

        /// <summary>
        /// Creates a copy of this chain.
        /// </summary>
        public GeoPolylineArc3 Clone() => new GeoPolylineArc3(_vertices, _bulges, _normals);

        /// <summary>
        /// Moves the chain by a vector.
        /// </summary>
        public GeoPolylineArc3 Translate(GeoVector3 vector)
        {
            var moved = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = _vertices[i].Add(vector);
            }

            return new GeoPolylineArc3(moved, _bulges, _normals);
        }

        /// <summary>
        /// Applies a transformation to the chain.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <remarks>
        /// Every edge is carried across on its own, so a transformation that mirrors turns each arc over
        /// with the rest of the chain rather than leaving it bulging the wrong way.
        /// </remarks>
        public GeoPolylineArc3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            var edges = new List<GeoEdge3>();

            for (int i = 0; i < EdgeCount; i++)
            {
                edges.Add(GetEdgeAt(i).TransformBy(transform));
            }

            return new GeoPolylineArc3(edges);
        }

        /// <summary>
        /// Determines whether this chain draws the same curve as another.
        /// </summary>
        public bool IsEqualTo(GeoPolylineArc3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this chain draws the same curve as another, within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPolylineArc3 other, Tolerance tolerance)
        {
            if (other == null || other.EdgeCount != EdgeCount)
            {
                return false;
            }

            for (int i = 0; i < EdgeCount; i++)
            {
                if (!GetEdgeAt(i).IsEqualTo(other.GetEdgeAt(i), tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Indicates whether the current chain holds the same vertices, bulges and planes as another.
        /// </summary>
        public bool Equals(GeoPolylineArc3 other)
        {
            if (other == null || other.VertexCount != VertexCount)
            {
                return false;
            }

            for (int i = 0; i < VertexCount; i++)
            {
                if (!_vertices[i].Equals(other._vertices[i]) ||
                    !_bulges[i].Equals(other._bulges[i]) ||
                    !_normals[i].Equals(other._normals[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoPolylineArc3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                for (int i = 0; i < VertexCount; i++)
                {
                    hash = (hash * 397) ^ _vertices[i].GetHashCode();
                    hash = (hash * 397) ^ _bulges[i].GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Determines whether two chains hold the same vertices, bulges and planes.
        /// </summary>
        public static bool operator ==(GeoPolylineArc3 left, GeoPolylineArc3 right)
            => left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Determines whether two chains differ.
        /// </summary>
        public static bool operator !=(GeoPolylineArc3 left, GeoPolylineArc3 right) => !(left == right);

        /// <summary>
        /// Returns a string describing the chain.
        /// </summary>
        public override string ToString()
        {
            int arcs = 0;

            for (int i = 0; i < EdgeCount; i++)
            {
                if (_bulges[i] != 0.0)
                {
                    arcs++;
                }
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "GeoPolylineArc3[{0} vertices, {1} of {2} edges curved, length {3:0.###}]",
                VertexCount,
                arcs,
                EdgeCount,
                Length);
        }

        /// <summary>
        /// Guards a chain being widened, so the null check reads before the base call rather than after it.
        /// </summary>
        private static GeoPolyline3 Required(GeoPolyline3 polyline)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return polyline;
        }
    }
}
