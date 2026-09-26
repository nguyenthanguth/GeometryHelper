using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A closed loop whose edges may be arcs: vertices with a bulge against each of them, the last one
    /// joining back to the first.
    /// <para>
    /// It is the arc-carrying counterpart of <see cref="GeoPolygon2"/>, and the same division holds as
    /// between <see cref="GeoPolylineArc2"/> and <see cref="GeoPolyline2"/>: this type says the shape may
    /// curve, and <see cref="Flatten()"/> is the one place where curves become chords.
    /// </para>
    /// </summary>
    public sealed partial class GeoPolygonArc2 : IEquatable<GeoPolygonArc2>
    {
        private readonly GeoPoint2[] _vertices;
        private readonly double[] _bulges;

        /// <summary>
        /// Gets the vertices of the loop, in order.
        /// </summary>
        public IReadOnlyList<GeoPoint2> Vertices => _vertices;

        /// <summary>
        /// Gets the bulge of each edge: the one at an index belongs to the edge leaving that vertex, and the
        /// last belongs to the edge closing the loop.
        /// </summary>
        public IReadOnlyList<double> Bulges => _bulges;

        /// <summary>
        /// Initializes a loop of straight edges.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least three distinct ones.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than three distinct vertices are given.</exception>
        public GeoPolygonArc2(IEnumerable<GeoPoint2> vertices)
            : this(vertices, null)
        {
        }

        /// <summary>
        /// Initializes a loop whose edges may be arcs.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least three distinct ones.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex; null or short means the rest are straight.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than three distinct vertices are given.</exception>
        public GeoPolygonArc2(IEnumerable<GeoPoint2> vertices, IEnumerable<double> bulges)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));

            var kept = new List<GeoPoint2>();
            var keptBulges = new List<double>();
            var given = bulges == null ? new List<double>() : new List<double>(bulges);

            int index = 0;

            foreach (GeoPoint2 vertex in vertices)
            {
                double bulge = index < given.Count ? given[index] : 0.0;
                index++;

                if (kept.Count > 0 && kept[kept.Count - 1].IsEqualTo(vertex))
                {
                    continue;
                }

                kept.Add(vertex);
                keptBulges.Add(bulge);
            }

            // A loop closes itself, so a repeated first vertex at the end says nothing the loop does not
            // already say. Its bulge, though, belongs to the closing edge.
            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0]))
            {
                keptBulges[kept.Count - 2] = keptBulges[kept.Count - 2] != 0.0 ? keptBulges[kept.Count - 2] : keptBulges[kept.Count - 1];
                kept.RemoveAt(kept.Count - 1);
                keptBulges.RemoveAt(keptBulges.Count - 1);
            }

            if (kept.Count < 3)
            {
                throw new ArgumentException("A loop needs at least 3 distinct vertices.", nameof(vertices));
            }

            _vertices = kept.ToArray();
            _bulges = keptBulges.ToArray();
        }

        /// <summary>
        /// Initializes a loop from a straight polygon, with no bulges.
        /// </summary>
        /// <param name="polygon">The straight polygon to widen.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPolygonArc2(GeoPolygon2 polygon)
            : this((polygon ?? throw new ArgumentNullException(nameof(polygon))).Vertices, null)
        {
        }

        /// <summary>
        /// Gets the vertex at an index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the loop.</exception>
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
        /// Gets the number of vertices of the loop.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets the number of edges of the loop, the same as its vertices because it closes.
        /// </summary>
        public int EdgeCount => _vertices.Length;

        /// <summary>
        /// Gets a value indicating whether any edge of the loop is an arc.
        /// </summary>
        public bool HasArcs
        {
            get
            {
                foreach (double bulge in _bulges)
                {
                    if (bulge != 0.0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the length round the loop, measured along its arcs rather than across them.
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
        /// Gets the signed area the loop encloses, positive when its vertices run counter-clockwise.
        /// </summary>
        /// <remarks>
        /// The answer is exact rather than an approximation of the arcs: it is the area of the straight
        /// loop through the vertices, with the piece each arc adds beyond its chord counted in, positive
        /// where the arc bulges to the left of that chord and negative where it bulges to the right.
        /// </remarks>
        public double SignedArea
        {
            get
            {
                double twice = 0.0;

                for (int i = 0; i < _vertices.Length; i++)
                {
                    GeoPoint2 current = _vertices[i];
                    GeoPoint2 next = _vertices[(i + 1) % _vertices.Length];

                    twice += current.X * next.Y - next.X * current.Y;
                }

                double area = twice * 0.5;

                for (int i = 0; i < EdgeCount; i++)
                {
                    if (_bulges[i] == 0.0)
                    {
                        continue;
                    }

                    GeoArc2 arc = GetEdgeAt(i).ToArc();
                    double swept = arc.SweptAngle;

                    area += arc.Radius * arc.Radius * 0.5 * (swept - Math.Sin(swept));
                }

                return area;
            }
        }

        /// <summary>
        /// Gets the area the loop encloses, whichever way round it runs.
        /// </summary>
        public double Area => Math.Abs(SignedArea);

        /// <summary>
        /// Gets a value indicating whether the vertices of the loop run clockwise.
        /// </summary>
        public bool IsClockwise => SignedArea < 0.0;

        /// <summary>
        /// Gets the bulge of the edge leaving a vertex.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the loop.</exception>
        public double GetBulgeAt(int index)
        {
            if (index < 0 || index >= _vertices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _bulges[index];
        }

        /// <summary>
        /// Gets the edge at an index; the last one closes the loop.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the loop.</exception>
        public GeoEdge2 GetEdgeAt(int index)
        {
            if (index < 0 || index >= EdgeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new GeoEdge2(_vertices[index], _vertices[(index + 1) % _vertices.Length], _bulges[index]);
        }

        /// <summary>
        /// Enumerates the edges of the loop, in order, ending with the one that closes it.
        /// </summary>
        public IEnumerable<GeoEdge2> GetEdges()
        {
            for (int i = 0; i < EdgeCount; i++)
            {
                yield return GetEdgeAt(i);
            }
        }

        /// <summary>
        /// Approximates the loop as a straight polygon, cutting each arc finely enough that it strays no
        /// further than the automatic share of its radius.
        /// </summary>
        /// <returns>The straight polygon, which every operation over regions can read.</returns>
        public GeoPolygon2 Flatten() => Flatten(0.0);

        /// <summary>
        /// Approximates the loop as a straight polygon, cutting each arc so that it strays no further than a
        /// chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The straight polygon.</returns>
        /// <remarks>
        /// The chords of an arc lie inside it, so a loop bulging outward encloses a little less once it is
        /// flattened, and one bulging inward a little more.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public GeoPolygon2 Flatten(double chordTolerance)
        {
            var points = new List<GeoPoint2>(VertexCount * 2);

            for (int i = 0; i < EdgeCount; i++)
            {
                GetEdgeAt(i).AppendFlattened(points, chordTolerance);
            }

            // The walk comes back to where it started, and a polygon closes itself.
            if (points.Count > 1 && points[points.Count - 1].IsEqualTo(points[0]))
            {
                points.RemoveAt(points.Count - 1);
            }

            return new GeoPolygon2(points);
        }

        /// <summary>
        /// Gets the loop running the other way round.
        /// </summary>
        public GeoPolygonArc2 Reverse()
        {
            var vertices = new List<GeoPoint2>(VertexCount);
            var bulges = new List<double>(VertexCount);

            for (int i = VertexCount - 1; i >= 0; i--)
            {
                vertices.Add(_vertices[i]);

                // Walking the other way, the edge leaving this vertex is the one that arrived at it.
                bulges.Add(-_bulges[(i - 1 + VertexCount) % VertexCount]);
            }

            return new GeoPolygonArc2(vertices, bulges);
        }

        /// <summary>
        /// Gets the loop moved by a vector.
        /// </summary>
        /// <param name="vector">How far to move it.</param>
        /// <returns>The moved loop, its arcs unchanged.</returns>
        public GeoPolygonArc2 Translate(GeoVector2 vector)
        {
            var moved = new GeoPoint2[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = _vertices[i].Add(vector);
            }

            return new GeoPolygonArc2(moved, _bulges);
        }

        /// <summary>
        /// Gets the loop turned about a point.
        /// </summary>
        /// <param name="angleRad">How far to turn it, in radians, counter-clockwise.</param>
        /// <param name="center">The point to turn it about.</param>
        /// <returns>The turned loop, its arcs unchanged.</returns>
        public GeoPolygonArc2 RotateBy(double angleRad, GeoPoint2 center)
        {
            var turned = new GeoPoint2[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                turned[i] = _vertices[i].RotateBy(angleRad, center);
            }

            return new GeoPolygonArc2(turned, _bulges);
        }

        /// <summary>
        /// Gets the loop under a transformation.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed loop.</returns>
        /// <remarks>
        /// Mirroring turns every arc the other way, so the bulges change sign, and the mirrored vertices
        /// trace the opposite winding, so a loop that ran counter-clockwise comes back clockwise. A
        /// transformation that scales the axes differently would make an arc part of an ellipse, and is
        /// refused.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation would stretch an arc into part of an ellipse.</exception>
        public GeoPolygonArc2 TransformBy(GeoTransform2 transform)
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

            var vertices = new List<GeoPoint2>(edges.Count);
            var bulges = new List<double>(edges.Count);

            foreach (GeoEdge2 edge in edges)
            {
                vertices.Add(edge.StartPoint);
                bulges.Add(edge.Bulge);
            }

            return new GeoPolygonArc2(vertices, bulges);
        }

        /// <summary>
        /// Creates a copy of this loop holding its own arrays.
        /// </summary>
        public GeoPolygonArc2 Clone() => new GeoPolygonArc2(_vertices, _bulges);

        /// <summary>
        /// Chamfers every corner that has room for it, cutting the same distance along both edges.
        /// </summary>
        public GeoPolygonArc2 Chamfer(double distance) => Corner2.Chamfer(this, distance, distance, Tolerance.Global);

        /// <summary>
        /// Chamfers every corner that has room for it, within a tolerance.
        /// </summary>
        public GeoPolygonArc2 Chamfer(double distance1, double distance2, Tolerance tolerance) => Corner2.Chamfer(this, distance1, distance2, tolerance);

        /// <summary>
        /// Rounds every corner that has room for it with an arc of a given radius.
        /// </summary>
        public GeoPolygonArc2 Fillet(double radius) => Corner2.Fillet(this, radius, Tolerance.Global);

        /// <summary>
        /// Rounds every corner that has room for it with an arc of a given radius, within a tolerance.
        /// </summary>
        public GeoPolygonArc2 Fillet(double radius, Tolerance tolerance) => Corner2.Fillet(this, radius, tolerance);

        /// <summary>
        /// Determines whether another loop holds exactly the same vertices and bulges.
        /// </summary>
        public bool Equals(GeoPolygonArc2 other)
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
        /// Determines whether the specified object is an equal loop.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoPolygonArc2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this loop.
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
        /// Determines whether another loop draws the same shape, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPolygonArc2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another loop draws the same shape, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Where a loop starts is not part of the shape it encloses, so the comparison tries every rotation
        /// of the other loop. The direction it runs in is part of the shape.
        /// </remarks>
        public bool IsEqualTo(GeoPolygonArc2 other, Tolerance tolerance)
        {
            if (other is null || EdgeCount != other.EdgeCount)
            {
                return false;
            }

            int count = EdgeCount;

            for (int shift = 0; shift < count; shift++)
            {
                bool matched = true;

                for (int i = 0; i < count; i++)
                {
                    if (!GetEdgeAt(i).IsEqualTo(other.GetEdgeAt((shift + i) % count), tolerance))
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
        /// Describes the loop.
        /// </summary>
        public override string ToString()
        {
            return $"GeoPolygonArc2[{VertexCount} vertices, {EdgeCount} edges, Length:{Length:0.###}]";
        }
        #region Asking and measuring

        /// <summary>
        /// Gets the point at a normalized parameter round the loop, where 0 is its start and 1 its end.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Parametrization2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the point a distance round the loop.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Parametrization2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the distance round the loop at a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at a distance round the loop.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization2.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Gets the normalized parameter of the point of the loop nearest a point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Parametrization2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the normalized parameter of the point of the loop nearest a point, within a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point, Tolerance tolerance) => Parametrization2.GetParameterAtPoint(this, point, tolerance);

        /// <summary>
        /// Gets how far round the loop the point nearest a point lies.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Parametrization2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets how far round the loop the point nearest a point lies, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point, Tolerance tolerance) => Parametrization2.GetDistanceAtPoint(this, point, tolerance);

        /// <summary>
        /// Determines whether a point lies on the loop.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => Containment2.IsPointOn(this, point);

        /// <summary>
        /// Determines whether a point lies on the loop, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => Containment2.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Says where a point lies against the loop.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point);

        /// <summary>
        /// Says where a point lies against the loop, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        /// <summary>
        /// Determines whether the loop encloses a point, counting its boundary as inside.
        /// </summary>
        public bool Contains(GeoPoint2 point) => Containment2.Contains(this, point);

        /// <summary>
        /// Determines whether the loop encloses a point, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint2 point, Tolerance tolerance) => Containment2.Contains(this, point, tolerance);

        #endregion

        #region Cutting, measuring and converting

        /// <summary>
        /// Gets the geometric centroid of the loop.
        /// </summary>
        /// <remarks>
        /// Exact rather than an approximation of the arcs: the centroid of the straight loop through the
        /// vertices, weighted against the centroid of each piece an arc cuts off its own chord.
        /// </remarks>
        public GeoPoint2 Centroid
        {
            get
            {
                var chords = new GeoPoint2[_vertices.Length];

                for (int i = 0; i < _vertices.Length; i++)
                {
                    chords[i] = _vertices[i];
                }

                var straight = new GeoPolygon2(chords);

                double weight = straight.SignedArea;
                double x = straight.Centroid.X * weight;
                double y = straight.Centroid.Y * weight;

                for (int i = 0; i < EdgeCount; i++)
                {
                    if (_bulges[i] == 0.0)
                    {
                        continue;
                    }

                    GeoArc2 arc = GetEdgeAt(i).ToArc();
                    double swept = arc.SweptAngle;
                    double area = arc.Radius * arc.Radius * 0.5 * (swept - Math.Sin(swept));

                    if (Math.Abs(area) <= double.Epsilon)
                    {
                        continue;
                    }

                    // The centroid of a circular segment lies on the line from the centre through the
                    // middle of the arc, this far along it.
                    double reach = 4.0 * arc.Radius * Math.Pow(Math.Sin(swept * 0.5), 3.0) / (3.0 * (swept - Math.Sin(swept)));

                    GeoVector2 along = arc.Center.GetVectorTo(arc.MidPoint);

                    if (!along.TryGetNormal(out GeoVector2 unit))
                    {
                        continue;
                    }

                    x += (arc.Center.X + unit.X * reach) * area;
                    y += (arc.Center.Y + unit.Y * reach) * area;
                    weight += area;
                }

                return Math.Abs(weight) <= double.Epsilon
                    ? straight.Centroid
                    : new GeoPoint2(x / weight, y / weight);
            }
        }

        /// <summary>
        /// Determines whether the loop crosses itself nowhere.
        /// </summary>
        public bool IsSimple() => IsSimple(Tolerance.Global);

        /// <summary>
        /// Determines whether the loop crosses itself nowhere, within a tolerance.
        /// </summary>
        /// <returns>true when no edge meets another except where two neighbours share their vertex.</returns>
        /// <remarks>
        /// The check is exact on the arcs, so a loop whose chords would cross but whose arcs do not is
        /// reported simple, and one whose arcs bulge into each other is not. A loop is not checked when it
        /// is built, because the check costs more than the building does.
        /// </remarks>
        public bool IsSimple(Tolerance tolerance)
        {
            int count = EdgeCount;

            for (int i = 0; i < count; i++)
            {
                GeoEdge2 one = GetEdgeAt(i);

                for (int j = i + 1; j < count; j++)
                {
                    GeoEdge2 other = GetEdgeAt(j);
                    bool neighbours = j == i + 1 || (i == 0 && j == count - 1);

                    foreach (GeoPoint2 meeting in one.GetIntersections(other, tolerance))
                    {
                        if (!neighbours)
                        {
                            return false;
                        }

                        // Neighbours are allowed to meet at the vertex they share, and nowhere else.
                        if (!meeting.IsEqualTo(one.EndPoint, tolerance) && !meeting.IsEqualTo(one.StartPoint, tolerance))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Cuts the loop wherever it meets a straight segment.
        /// </summary>
        public bool TrySplitBy(GeoLine2 cutter, out GeoPolylineArc2[] pieces) => Splition2.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Cuts the loop wherever it meets a straight segment, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoLine2 cutter, out GeoPolylineArc2[] pieces, Tolerance tolerance) => Splition2.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Cuts the loop wherever it meets a straight loop.
        /// </summary>
        public bool TrySplitBy(GeoPolygon2 cutter, out GeoPolylineArc2[] pieces) => Splition2.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Cuts the loop at distances round it.
        /// </summary>
        public GeoPolylineArc2[] SplitAtDistances(IEnumerable<double> distances) => Splition2.SplitAtDistances(this, distances);

        /// <summary>
        /// Cuts the loop at distances round it, within a tolerance.
        /// </summary>
        public GeoPolylineArc2[] SplitAtDistances(IEnumerable<double> distances, Tolerance tolerance) => Splition2.SplitAtDistances(this, distances, tolerance);

        /// <summary>
        /// Gets the loop opened up into a chain, with its first vertex repeated at the end.
        /// </summary>
        /// <returns>The chain, with every arc kept.</returns>
        public GeoPolylineArc2 ToPolylineArc2()
        {
            var vertices = new List<GeoPoint2>(_vertices.Length + 1);
            var bulges = new List<double>(_vertices.Length + 1);

            for (int i = 0; i < _vertices.Length; i++)
            {
                vertices.Add(_vertices[i]);
                bulges.Add(_bulges[i]);
            }

            vertices.Add(_vertices[0]);
            bulges.Add(0.0);

            return new GeoPolylineArc2(vertices, bulges);
        }

        /// <summary>
        /// Determines whether two loops hold exactly the same vertices and bulges.
        /// </summary>
        public static bool operator ==(GeoPolygonArc2 left, GeoPolygonArc2 right) => left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Determines whether two loops hold different vertices or bulges.
        /// </summary>
        public static bool operator !=(GeoPolygonArc2 left, GeoPolygonArc2 right) => !(left == right);

        #endregion

        #region Corners and shorthands

        /// <summary>
        /// Chamfers one corner of the loop, using the default tolerance.
        /// </summary>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolygonArc2 result)
            => Corner2.TryChamferAt(this, index, distance1, distance2, out result);

        /// <summary>
        /// Chamfers one corner of the loop, within a tolerance.
        /// </summary>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolygonArc2 result, Tolerance tolerance)
            => Corner2.TryChamferAt(this, index, distance1, distance2, out result, tolerance);

        /// <summary>
        /// Translates a loop by a vector.
        /// </summary>
        public static GeoPolygonArc2 operator +(GeoPolygonArc2 loop, GeoVector2 vector) => loop.Translate(vector);

        /// <summary>
        /// Translates a loop against a vector.
        /// </summary>
        public static GeoPolygonArc2 operator -(GeoPolygonArc2 loop, GeoVector2 vector) => loop.Translate(-vector);

        #endregion

        /// <summary>
        /// Offsets the loop, keeping its arcs as arcs.
        /// </summary>
        /// <param name="distance">How far to move it: outward when positive, inward when negative.</param>
        /// <returns>The offset loops.</returns>
        public GeoPolygonArc2[] Offset(double distance) => Offset2.Offset(this, distance);

        /// <summary>
        /// Offsets the loop, within a tolerance.
        /// </summary>
        public GeoPolygonArc2[] Offset(double distance, Tolerance tolerance) => Offset2.Offset(this, distance, tolerance);

        /// <summary>
        /// Offsets the loop, filling opened corners a given way.
        /// </summary>
        public GeoPolygonArc2[] Offset(double distance, OffsetJoin join) => Offset2.Offset(this, distance, join);

        /// <summary>
        /// Offsets the loop, filling opened corners a given way, within a tolerance.
        /// </summary>
        public GeoPolygonArc2[] Offset(double distance, OffsetJoin join, Tolerance tolerance) => Offset2.Offset(this, distance, join, tolerance);

        /// <summary>
        /// Offsets the loop, with options.
        /// </summary>
        public GeoPolygonArc2[] Offset(double distance, OffsetOptions options) => Offset2.Offset(this, distance, options);

        /// <summary>
        /// Offsets the loop, with options, within a tolerance.
        /// </summary>
        public GeoPolygonArc2[] Offset(double distance, OffsetOptions options, Tolerance tolerance) => Offset2.Offset(this, distance, options, tolerance);

        /// <summary>
        /// Gets this loop and another together, with the arcs cut into straight pieces first.
        /// </summary>
        public GeoFace2[] Union(GeoPolygonArc2 other) => Boolean2.Union(this, other);

        /// <summary>
        /// Gets this loop and a straight one together.
        /// </summary>
        public GeoFace2[] Union(GeoPolygon2 other) => Boolean2.Union(this, other);

        /// <summary>
        /// Gets what this loop and another both cover, with the arcs cut into straight pieces first.
        /// </summary>
        public GeoFace2[] Intersect(GeoPolygonArc2 other) => Boolean2.Intersect(this, other);

        /// <summary>
        /// Gets what this loop and a straight one both cover.
        /// </summary>
        public GeoFace2[] Intersect(GeoPolygon2 other) => Boolean2.Intersect(this, other);

        /// <summary>
        /// Gets this loop with another taken out of it, with the arcs cut into straight pieces first.
        /// </summary>
        public GeoFace2[] Subtract(GeoPolygonArc2 tool) => Boolean2.Subtract(this, tool);

        /// <summary>
        /// Gets this loop with a straight one taken out of it.
        /// </summary>
        public GeoFace2[] Subtract(GeoPolygon2 tool) => Boolean2.Subtract(this, tool);

        /// <summary>
        /// Gets what this loop covers and another does not, and the other way round.
        /// </summary>
        public GeoFace2[] Xor(GeoPolygonArc2 other) => Boolean2.Xor(this, other);

        /// <summary>
        /// Gets what this loop covers and a straight one does not, and the other way round.
        /// </summary>
        public GeoFace2[] Xor(GeoPolygon2 other) => Boolean2.Xor(this, other);

        /// <summary>
        /// Lays the loop out in a plane in space, cutting its arcs into straight pieces.
        /// </summary>
        /// <param name="frame">The frame of the plane to lay it in.</param>
        /// <returns>The loop in space, straight throughout.</returns>
        /// <remarks>
        /// There is no arc-carrying shape in space for it to become, so the arcs are cut as
        /// <see cref="Flatten()"/> cuts them, and the straight type says so.
        /// </remarks>
        public GeoPolygon3 ToPolygon3(GeoCoordinateSystem3 frame) => Flatten().ToPolygon3(frame);

        /// <summary>
        /// Lays the loop out in a plane in space, cutting its arcs no further than a chord tolerance from
        /// the curve.
        /// </summary>
        /// <param name="frame">The frame of the plane to lay it in.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The loop in space, straight throughout.</returns>
        public GeoPolygon3 ToPolygon3(GeoCoordinateSystem3 frame, double chordTolerance) => Flatten(chordTolerance).ToPolygon3(frame);

        /// <summary>
        /// Rounds the corners of the loop, each by its own radius.
        /// </summary>
        /// <param name="radii">One radius per vertex, read the way <see cref="GetBulgeAt"/> is read; zero leaves that corner alone.</param>
        /// <returns>The filleted loop.</returns>
        /// <remarks>
        /// Where two neighbouring corners together ask for more than the edge between them is long, the
        /// one taking more of it gives way, so a corner asking for a large radius yields to a small one
        /// rather than the other way round. A list shorter than the loop leaves the rest alone.
        /// </remarks>
        public GeoPolygonArc2 Fillet(IReadOnlyList<double> radii) => Corner2.Fillet(this, radii);

        /// <summary>
        /// Rounds the corners of the loop, each by its own radius, within a tolerance.
        /// </summary>
        public GeoPolygonArc2 Fillet(IReadOnlyList<double> radii, Tolerance tolerance) => Corner2.Fillet(this, radii, tolerance);

        /// <summary>
        /// Rounds one corner of the loop, using the default tolerance.
        /// </summary>
        /// <param name="index">Which vertex to round.</param>
        /// <param name="radius">The radius of the arc to put there.</param>
        /// <param name="result">The filleted loop, or the loop unchanged when the method returns false.</param>
        /// <returns>true if the corner had room for the arc; otherwise, false.</returns>
        public bool TryFilletAt(int index, double radius, out GeoPolygonArc2 result)
            => Corner2.TryFilletAt(this, index, radius, out result);

        /// <summary>
        /// Rounds one corner of the loop, within a tolerance.
        /// </summary>
        public bool TryFilletAt(int index, double radius, out GeoPolygonArc2 result, Tolerance tolerance)
            => Corner2.TryFilletAt(this, index, radius, out result, tolerance);

        /// <summary>
        /// Builds a spatial index over the edges of this loop, arcs and all, for asking it many questions.
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
        public Spatial.GeoBvh2 BuildIndex() => Spatial.GeoBvh2.FromPolygonArc(this);

    }
}
