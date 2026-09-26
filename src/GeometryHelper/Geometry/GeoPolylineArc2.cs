using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// An open chain whose pieces may be arcs: vertices with a bulge against each of them, which is exactly
    /// how AutoCAD holds a polyline.
    /// <para>
    /// It is the arc-carrying counterpart of <see cref="GeoPolyline2"/>. The straight one is not going
    /// anywhere: most chains have no arcs at all, and everything the library does with regions reads
    /// straight edges, so a chain that carries arcs says so in its type. <see cref="Flatten()"/> crosses
    /// from here to there, and that crossing is where exactness ends — it is the only place arcs become
    /// chords, and it asks for the tolerance that decides how close the chords come.
    /// </para>
    /// </summary>
    public sealed partial class GeoPolylineArc2 : IEquatable<GeoPolylineArc2>
    {
        private readonly GeoPoint2[] _vertices;
        private readonly double[] _bulges;

        /// <summary>
        /// Gets the vertices of the chain, in order.
        /// </summary>
        public IReadOnlyList<GeoPoint2> Vertices => _vertices;

        /// <summary>
        /// Gets the bulge of each edge: the one at an index belongs to the edge leaving that vertex, so the
        /// last vertex of an open chain has none.
        /// </summary>
        public IReadOnlyList<double> Bulges => _bulges;

        /// <summary>
        /// Initializes a chain of straight pieces.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least two, and no two neighbours in the same place.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct vertices are given.</exception>
        public GeoPolylineArc2(IEnumerable<GeoPoint2> vertices)
            : this(vertices, null)
        {
        }

        /// <summary>
        /// Initializes a chain whose pieces may be arcs.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least two, and no two neighbours in the same place.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex; null or short means the rest are straight, and anything past the last edge is ignored.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct vertices are given.</exception>
        public GeoPolylineArc2(IEnumerable<GeoPoint2> vertices, IEnumerable<double> bulges)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));

            List<GeoPoint2> kept = new List<GeoPoint2>();
            List<double> keptBulges = new List<double>();
            List<double> given = bulges == null ? new List<double>() : new List<double>(bulges);

            int index = 0;

            foreach (GeoPoint2 vertex in vertices)
            {
                double bulge = index < given.Count ? given[index] : 0.0;
                index++;

                // Two vertices in the same place leave an edge with no length, which no arc and no segment
                // can be drawn along; the later one goes, as GeoPolyline2 drops it too.
                if (kept.Count > 0 && kept[kept.Count - 1].IsEqualTo(vertex))
                {
                    continue;
                }

                kept.Add(vertex);
                keptBulges.Add(bulge);
            }

            if (kept.Count < 2)
            {
                throw new ArgumentException("A chain needs at least 2 distinct vertices.", nameof(vertices));
            }

            _vertices = kept.ToArray();
            _bulges = keptBulges.ToArray();
        }

        /// <summary>
        /// Initializes a chain from edges that run end to end.
        /// </summary>
        /// <param name="edges">The edges, each starting where the one before it ended.</param>
        /// <exception cref="ArgumentNullException">Thrown when the edges are null.</exception>
        /// <exception cref="ArgumentException">Thrown when no edges are given, or one does not start where the one before it ended.</exception>
        public GeoPolylineArc2(IEnumerable<GeoEdge2> edges)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));

            var vertices = new List<GeoPoint2>();
            var bulges = new List<double>();

            foreach (GeoEdge2 edge in edges)
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
                vertices.Add(edge.EndPoint);
            }

            if (vertices.Count < 2)
            {
                throw new ArgumentException("A chain needs at least one edge.", nameof(edges));
            }

            bulges.Add(0.0);

            _vertices = vertices.ToArray();
            _bulges = bulges.ToArray();
        }

        /// <summary>
        /// Initializes a chain from a straight one, with no bulges.
        /// </summary>
        /// <param name="polyline">The straight chain to widen.</param>
        /// <remarks>
        /// Widening loses nothing: every edge simply carries a bulge of zero. It is
        /// <see cref="Flatten()"/> going the other way that approximates.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPolylineArc2(GeoPolyline2 polyline)
            : this((polyline ?? throw new ArgumentNullException(nameof(polyline))).Vertices, null)
        {
        }

        /// <summary>
        /// Gets the vertex at an index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the chain.</exception>
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
        /// Gets the number of vertices of the chain.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets the number of edges of the chain, one fewer than its vertices.
        /// </summary>
        public int EdgeCount => _vertices.Length - 1;

        /// <summary>
        /// Gets a value indicating whether any edge of the chain is an arc.
        /// </summary>
        public bool HasArcs
        {
            get
            {
                for (int i = 0; i < EdgeCount; i++)
                {
                    if (_bulges[i] != 0.0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the length of the chain, measured along its arcs rather than across them.
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
        /// Gets the bulge of the edge leaving a vertex.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the chain.</exception>
        public double GetBulgeAt(int index)
        {
            if (index < 0 || index >= _vertices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _bulges[index];
        }

        /// <summary>
        /// Gets the edge at an index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the edges of the chain.</exception>
        public GeoEdge2 GetEdgeAt(int index)
        {
            if (index < 0 || index >= EdgeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new GeoEdge2(_vertices[index], _vertices[index + 1], _bulges[index]);
        }

        /// <summary>
        /// Enumerates the edges of the chain, in order.
        /// </summary>
        public IEnumerable<GeoEdge2> GetEdges()
        {
            for (int i = 0; i < EdgeCount; i++)
            {
                yield return GetEdgeAt(i);
            }
        }

        /// <summary>
        /// Approximates the chain as a straight one, cutting each arc finely enough that it strays no
        /// further than the automatic share of its radius.
        /// </summary>
        /// <returns>The straight chain, which every operation over regions can read.</returns>
        /// <remarks>
        /// This is where exactness ends. A chain with no arcs comes across whole; one with arcs comes
        /// across as chords, as close as the tolerance says and no closer.
        /// </remarks>
        public GeoPolyline2 Flatten() => Flatten(0.0);

        /// <summary>
        /// Approximates the chain as a straight one, cutting each arc so that it strays no further than a
        /// chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The straight chain.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public GeoPolyline2 Flatten(double chordTolerance)
        {
            var points = new List<GeoPoint2>(VertexCount * 2);

            for (int i = 0; i < EdgeCount; i++)
            {
                GetEdgeAt(i).AppendFlattened(points, chordTolerance);
            }

            return new GeoPolyline2(points);
        }

        /// <summary>
        /// Gets the chain running the other way, from its end to its start.
        /// </summary>
        public GeoPolylineArc2 Reverse()
        {
            var edges = new List<GeoEdge2>(EdgeCount);

            for (int i = EdgeCount - 1; i >= 0; i--)
            {
                edges.Add(GetEdgeAt(i).Reverse());
            }

            return new GeoPolylineArc2(edges);
        }

        /// <summary>
        /// Gets the chain moved by a vector.
        /// </summary>
        /// <param name="vector">How far to move it.</param>
        /// <returns>The moved chain, its arcs unchanged.</returns>
        public GeoPolylineArc2 Translate(GeoVector2 vector)
        {
            var moved = new GeoPoint2[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = _vertices[i].Add(vector);
            }

            return new GeoPolylineArc2(moved, _bulges);
        }

        /// <summary>
        /// Gets the chain turned about a point.
        /// </summary>
        /// <param name="angleRad">How far to turn it, in radians, counter-clockwise.</param>
        /// <param name="center">The point to turn it about.</param>
        /// <returns>The turned chain, its arcs unchanged.</returns>
        public GeoPolylineArc2 RotateBy(double angleRad, GeoPoint2 center)
        {
            var turned = new GeoPoint2[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                turned[i] = _vertices[i].RotateBy(angleRad, center);
            }

            return new GeoPolylineArc2(turned, _bulges);
        }

        /// <summary>
        /// Gets the chain under a transformation.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed chain.</returns>
        /// <remarks>
        /// Mirroring turns every arc the other way, so the bulges change sign; the chain still starts
        /// where its start point went. A transformation that scales the axes differently would make an arc
        /// part of an ellipse, and is refused.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation would stretch an arc into part of an ellipse.</exception>
        public GeoPolylineArc2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            var edges = new List<GeoEdge2>(EdgeCount);

            for (int i = 0; i < EdgeCount; i++)
            {
                edges.Add(GetEdgeAt(i).TransformBy(transform));
            }

            return new GeoPolylineArc2(edges);
        }

        /// <summary>
        /// Creates a copy of this chain holding its own arrays.
        /// </summary>
        public GeoPolylineArc2 Clone() => new GeoPolylineArc2(_vertices, _bulges);

        /// <summary>
        /// Chamfers every corner that has room for it, cutting the same distance along both edges.
        /// </summary>
        /// <param name="distance">How far back along each edge the cut is measured.</param>
        /// <returns>The chamfered chain; corners between two straight edges are cut and the rest are left alone.</returns>
        public GeoPolylineArc2 Chamfer(double distance) => Corner2.Chamfer(this, distance, distance, Tolerance.Global);

        /// <summary>
        /// Chamfers every corner that has room for it, within a tolerance.
        /// </summary>
        public GeoPolylineArc2 Chamfer(double distance1, double distance2, Tolerance tolerance) => Corner2.Chamfer(this, distance1, distance2, tolerance);

        /// <summary>
        /// Rounds every corner that has room for it with an arc of a given radius.
        /// </summary>
        /// <param name="radius">The radius of the arcs.</param>
        /// <returns>The filleted chain; corners between two straight edges are rounded and the rest are left alone.</returns>
        public GeoPolylineArc2 Fillet(double radius) => Corner2.Fillet(this, radius, Tolerance.Global);

        /// <summary>
        /// Rounds every corner that has room for it with an arc of a given radius, within a tolerance.
        /// </summary>
        public GeoPolylineArc2 Fillet(double radius, Tolerance tolerance) => Corner2.Fillet(this, radius, tolerance);

        /// <summary>
        /// Determines whether another chain holds exactly the same vertices and bulges.
        /// </summary>
        public bool Equals(GeoPolylineArc2 other)
        {
            if (other is null || _vertices.Length != other._vertices.Length)
            {
                return false;
            }

            for (int i = 0; i < _vertices.Length; i++)
            {
                if (!_vertices[i].Equals(other._vertices[i]) || !_bulges[i].Equals(other._bulges[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified object is an equal chain.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoPolylineArc2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this chain.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                for (int i = 0; i < _vertices.Length; i++)
                {
                    hash = hash * 397 ^ _vertices[i].GetHashCode();
                    hash = hash * 397 ^ _bulges[i].GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Determines whether another chain draws the same curve, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPolylineArc2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another chain draws the same curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A chain has ends, so it is compared from its start. A chain and the same chain reversed are not
        /// equal; compare one against <c>other.Reverse()</c> when either direction will do.
        /// </remarks>
        public bool IsEqualTo(GeoPolylineArc2 other, Tolerance tolerance)
        {
            if (other is null || EdgeCount != other.EdgeCount)
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
        /// Describes the chain.
        /// </summary>
        public override string ToString()
        {
            return $"GeoPolylineArc2[{VertexCount} vertices, {EdgeCount} edges, Length:{Length:0.###}]";
        }
        #region Asking and measuring

        /// <summary>
        /// Gets the point at a normalized parameter along the chain, where 0 is its start and 1 its end.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Parametrization2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the point a distance along the chain.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Parametrization2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the distance along the chain at a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at a distance along the chain.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization2.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Gets the normalized parameter of the point of the chain nearest a point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Parametrization2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the normalized parameter of the point of the chain nearest a point, within a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point, Tolerance tolerance) => Parametrization2.GetParameterAtPoint(this, point, tolerance);

        /// <summary>
        /// Gets how far along the chain the point nearest a point lies.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Parametrization2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets how far along the chain the point nearest a point lies, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point, Tolerance tolerance) => Parametrization2.GetDistanceAtPoint(this, point, tolerance);

        /// <summary>
        /// Determines whether a point lies on the chain.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => Containment2.IsPointOn(this, point);

        /// <summary>
        /// Determines whether a point lies on the chain, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => Containment2.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Says where a point lies against the chain.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point);

        /// <summary>
        /// Says where a point lies against the chain, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        #endregion

        #region Cutting and converting

        /// <summary>
        /// Cuts the chain in two at a distance along it.
        /// </summary>
        public bool TrySplitAtDistance(double distance, out GeoPolylineArc2 first, out GeoPolylineArc2 second)
            => Splition2.TrySplitAtDistance(this, distance, out first, out second);

        /// <summary>
        /// Cuts the chain in two at a distance along it, within a tolerance.
        /// </summary>
        public bool TrySplitAtDistance(double distance, out GeoPolylineArc2 first, out GeoPolylineArc2 second, Tolerance tolerance)
            => Splition2.TrySplitAtDistance(this, distance, out first, out second, tolerance);

        /// <summary>
        /// Cuts the chain in two at a point on it.
        /// </summary>
        public bool TrySplitBy(GeoPoint2 point, out GeoPolylineArc2 first, out GeoPolylineArc2 second)
            => Splition2.TrySplitBy(this, point, out first, out second);

        /// <summary>
        /// Cuts the chain in two at a point on it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPoint2 point, out GeoPolylineArc2 first, out GeoPolylineArc2 second, Tolerance tolerance)
            => Splition2.TrySplitBy(this, point, out first, out second, tolerance);

        /// <summary>
        /// Cuts the chain wherever it meets a straight segment.
        /// </summary>
        public bool TrySplitBy(GeoLine2 cutter, out GeoPolylineArc2[] pieces) => Splition2.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Cuts the chain wherever it meets a straight segment, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoLine2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance) => Splition2.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Cuts the chain wherever it meets an arc.
        /// </summary>
        public bool TrySplitBy(GeoArc2 cutter, out GeoPolylineArc2[] pieces) => Splition2.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Cuts the chain wherever it meets an arc, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoArc2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance) => Splition2.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Cuts the chain wherever it meets a straight loop.
        /// </summary>
        public bool TrySplitBy(GeoPolygon2 cutter, out GeoPolylineArc2[] pieces) => Splition2.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Cuts the chain wherever it meets a loop that may curve.
        /// </summary>
        public bool TrySplitBy(GeoPolygonArc2 cutter, out GeoPolylineArc2[] pieces) => Splition2.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Cuts the chain at points on it.
        /// </summary>
        public bool TrySplitBy(GeoPoint2[] points, out GeoPolylineArc2[] pieces) => Splition2.TrySplitBy(this, points, out pieces);

        /// <summary>
        /// Cuts the chain at distances along it.
        /// </summary>
        public GeoPolylineArc2[] SplitAtDistances(IEnumerable<double> distances) => Splition2.SplitAtDistances(this, distances);

        /// <summary>
        /// Cuts the chain at distances along it, within a tolerance.
        /// </summary>
        public GeoPolylineArc2[] SplitAtDistances(IEnumerable<double> distances, Tolerance tolerance) => Splition2.SplitAtDistances(this, distances, tolerance);

        /// <summary>
        /// Gets the loop that closes the chain, joining its last vertex back to its first.
        /// </summary>
        /// <returns>The loop, with every arc kept.</returns>
        /// <remarks>
        /// A chain whose ends already meet gives a loop with that repeated vertex dropped, because a loop
        /// closes itself.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the chain has too few distinct vertices to close.</exception>
        public GeoPolygonArc2 ToPolygonArc2()
        {
            try
            {
                return new GeoPolygonArc2(_vertices, _bulges);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("This chain has too few distinct vertices to close into a loop.", exception);
            }
        }

        /// <summary>
        /// Determines whether two chains hold exactly the same vertices and bulges.
        /// </summary>
        public static bool operator ==(GeoPolylineArc2 left, GeoPolylineArc2 right) => left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Determines whether two chains hold different vertices or bulges.
        /// </summary>
        public static bool operator !=(GeoPolylineArc2 left, GeoPolylineArc2 right) => !(left == right);

        #endregion

        #region Corners and shorthands

        /// <summary>
        /// Chamfers one corner of the chain, using the default tolerance.
        /// </summary>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolylineArc2 result)
            => Corner2.TryChamferAt(this, index, distance1, distance2, out result);

        /// <summary>
        /// Chamfers one corner of the chain, within a tolerance.
        /// </summary>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolylineArc2 result, Tolerance tolerance)
            => Corner2.TryChamferAt(this, index, distance1, distance2, out result, tolerance);

        /// <summary>
        /// Translates a chain by a vector.
        /// </summary>
        public static GeoPolylineArc2 operator +(GeoPolylineArc2 chain, GeoVector2 vector) => chain.Translate(vector);

        /// <summary>
        /// Translates a chain against a vector.
        /// </summary>
        public static GeoPolylineArc2 operator -(GeoPolylineArc2 chain, GeoVector2 vector) => chain.Translate(-vector);

        #endregion

        /// <summary>
        /// Offsets the chain, keeping its arcs as arcs.
        /// </summary>
        /// <param name="distance">How far to move it: to the left of the way it runs when positive.</param>
        /// <returns>The offset chains.</returns>
        public GeoPolylineArc2[] Offset(double distance) => Offset2.Offset(this, distance);

        /// <summary>
        /// Offsets the chain, within a tolerance.
        /// </summary>
        public GeoPolylineArc2[] Offset(double distance, Tolerance tolerance) => Offset2.Offset(this, distance, tolerance);

        /// <summary>
        /// Offsets the chain, filling opened corners a given way.
        /// </summary>
        public GeoPolylineArc2[] Offset(double distance, OffsetJoin join) => Offset2.Offset(this, distance, join);

        /// <summary>
        /// Offsets the chain, filling opened corners a given way, within a tolerance.
        /// </summary>
        public GeoPolylineArc2[] Offset(double distance, OffsetJoin join, Tolerance tolerance) => Offset2.Offset(this, distance, join, tolerance);

        /// <summary>
        /// Offsets the chain, with options.
        /// </summary>
        public GeoPolylineArc2[] Offset(double distance, OffsetOptions options) => Offset2.Offset(this, distance, options);

        /// <summary>
        /// Offsets the chain, with options, within a tolerance.
        /// </summary>
        public GeoPolylineArc2[] Offset(double distance, OffsetOptions options, Tolerance tolerance) => Offset2.Offset(this, distance, options, tolerance);

        /// <summary>
        /// Lays the chain out in a plane in space, cutting its arcs into straight pieces.
        /// </summary>
        /// <param name="frame">The frame of the plane to lay it in.</param>
        /// <returns>The chain in space, straight throughout.</returns>
        /// <remarks>
        /// There is no arc-carrying shape in space for it to become, so the arcs are cut as
        /// <see cref="Flatten()"/> cuts them, and the straight type says so.
        /// </remarks>
        public GeoPolyline3 ToPolyline3(GeoCoordinateSystem3 frame) => Flatten().ToPolyline3(frame);

        /// <summary>
        /// Lays the chain out in a plane in space, cutting its arcs no further than a chord tolerance from
        /// the curve.
        /// </summary>
        /// <param name="frame">The frame of the plane to lay it in.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The chain in space, straight throughout.</returns>
        public GeoPolyline3 ToPolyline3(GeoCoordinateSystem3 frame, double chordTolerance) => Flatten(chordTolerance).ToPolyline3(frame);

        /// <summary>
        /// Rounds the corners of the chain, each by its own radius.
        /// </summary>
        /// <param name="radii">One radius per vertex, read the way <see cref="GetBulgeAt"/> is read; zero leaves that corner alone; the two ends have no corner to round.</param>
        /// <returns>The filleted chain.</returns>
        /// <remarks>
        /// Where two neighbouring corners together ask for more than the edge between them is long, the
        /// one taking more of it gives way, so a corner asking for a large radius yields to a small one
        /// rather than the other way round. A list shorter than the chain leaves the rest alone.
        /// </remarks>
        public GeoPolylineArc2 Fillet(IReadOnlyList<double> radii) => Corner2.Fillet(this, radii);

        /// <summary>
        /// Rounds the corners of the chain, each by its own radius, within a tolerance.
        /// </summary>
        public GeoPolylineArc2 Fillet(IReadOnlyList<double> radii, Tolerance tolerance) => Corner2.Fillet(this, radii, tolerance);

        /// <summary>
        /// Rounds one corner of the chain, using the default tolerance.
        /// </summary>
        /// <param name="index">Which vertex to round; the two ends have no corner to round.</param>
        /// <param name="radius">The radius of the arc to put there.</param>
        /// <param name="result">The filleted chain, or the chain unchanged when the method returns false.</param>
        /// <returns>true if the corner had room for the arc; otherwise, false.</returns>
        public bool TryFilletAt(int index, double radius, out GeoPolylineArc2 result)
            => Corner2.TryFilletAt(this, index, radius, out result);

        /// <summary>
        /// Rounds one corner of the chain, within a tolerance.
        /// </summary>
        public bool TryFilletAt(int index, double radius, out GeoPolylineArc2 result, Tolerance tolerance)
            => Corner2.TryFilletAt(this, index, radius, out result, tolerance);

        #region Extending and trimming

        /// <summary>
        /// Lengthens the chain along its end leg, round if that leg curves, keeping its radius.
        /// </summary>
        /// <remarks>
        /// Every other leg is untouched, so the bends and their radii survive. A chain is carried outwards only;
        /// the splitting family shortens one, and <c>TryTrimTo</c> below is that cut with the end named rather
        /// than the piece.
        /// </remarks>
        public GeoPolylineArc2 Extend(double distance, LineEnd end) => Lengthen2.Extend(this, distance, end);

        /// <summary>
        /// Lengthens the chain along its end leg, within a tolerance.
        /// </summary>
        public GeoPolylineArc2 Extend(double distance, LineEnd end, Tolerance tolerance) => Lengthen2.Extend(this, distance, end, tolerance);

        /// <summary>
        /// Lengthens the chain along its end leg until the whole chain is a given length.
        /// </summary>
        public GeoPolylineArc2 ExtendToLength(double length, LineEnd end) => Lengthen2.ExtendToLength(this, length, end);

        /// <summary>
        /// Lengthens the chain along its end leg until the whole chain is a given length, within a tolerance.
        /// </summary>
        public GeoPolylineArc2 ExtendToLength(double length, LineEnd end, Tolerance tolerance) => Lengthen2.ExtendToLength(this, length, end, tolerance);

        /// <summary>
        /// Shortens the chain at one end back to a point on it.
        /// </summary>
        public bool TryTrimTo(GeoPoint2 point, LineEnd end, out GeoPolylineArc2 result) => Lengthen2.TryTrimTo(this, point, end, out result);

        /// <summary>
        /// Shortens the chain at one end back to a point on it, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPoint2 point, LineEnd end, out GeoPolylineArc2 result, Tolerance tolerance)
            => Lengthen2.TryTrimTo(this, point, end, out result, tolerance);

        #endregion

        /// <summary>
        /// Builds a spatial index over the edges of this chain, arcs and all, for asking it many questions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An index is worth building when the <b>same</b> shape is asked <b>many</b> questions. Building it
        /// costs a sort of the edges, so it pays for itself over repeated queries and never on the first
        /// one; below a few dozen edges the plain walk wins outright. Every shape here is immutable, so a
        /// tree stays valid for as long as the shape exists — build it once, keep it, throw it away with the
        /// shape.
        /// </para>
        /// <para>
        /// It is <c>Build</c> and not <c>Get</c> because it does work. Calling it inside a loop is slower than
        /// not having it at all, which is exactly the mistake the name is there to prevent.
        /// </para>
        /// </remarks>
        /// <returns>The hierarchy; it is a snapshot and holds no reference back to this shape.</returns>
        public Spatial.GeoBvh2 BuildIndex() => Spatial.GeoBvh2.FromPolylineArc(this);

    }
}
