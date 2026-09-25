using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A closed loop in space whose pieces may be arcs, lying flat in a plane of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Coplanarity is enforced at construction, exactly as <see cref="GeoPolygon3"/> enforces it. A loop that
    /// is not flat encloses nothing, has no area and no inside, so it is not this type; a bar bent out of one
    /// plane belongs in <see cref="GeoPolylineArc3"/>, whose ends may meet without the loop being flat.
    /// </para>
    /// <para>
    /// That one rule is what makes everything else exact. Area, centroid, what is inside, offsetting and the
    /// booleans are all worked out by laying the loop out in its own plane as a
    /// <see cref="GeoPolygonArc2"/>, answering there, and lifting the answer back. Nothing is approximated
    /// and none of the arc machinery is written twice.
    /// </para>
    /// <para>
    /// A closed stirrup with a bending radius is this type: build the loop from the points it turns at and
    /// call <see cref="Fillet(double)"/>.
    /// </para>
    /// </remarks>
    public sealed class GeoPolygonArc3 : IEquatable<GeoPolygonArc3>
    {
        private readonly GeoPoint3[] _vertices;
        private readonly double[] _bulges;
        private readonly GeoVector3[] _normals;
        private readonly GeoVector3 _planeNormal;

        /// <summary>
        /// Gets the vertices of the loop, in order; the last joins back to the first.
        /// </summary>
        public IReadOnlyList<GeoPoint3> Vertices => _vertices;

        /// <summary>
        /// Gets the bulge of each edge: the one at an index belongs to the edge leaving that vertex, and the
        /// last belongs to the edge closing the loop.
        /// </summary>
        public IReadOnlyList<double> Bulges => _bulges;

        /// <summary>
        /// Gets the normal each bulge is read about; of no length at all where the edge is straight.
        /// </summary>
        public IReadOnlyList<GeoVector3> Normals => _normals;

        /// <summary>
        /// Gets the normal of the plane the whole loop lies in.
        /// </summary>
        public GeoVector3 Normal => _planeNormal;

        /// <summary>
        /// Initializes a loop of straight pieces.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than three distinct vertices are given, or they are not coplanar.</exception>
        public GeoPolygonArc3(IEnumerable<GeoPoint3> vertices)
            : this(vertices, null, null, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes a loop whose pieces may be arcs.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least three, and no two neighbours in the same place.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex; null or short means the rest are straight.</param>
        /// <param name="normals">The plane each bulge is read about; only wanted where a bulge is not nought.</param>
        public GeoPolygonArc3(IEnumerable<GeoPoint3> vertices, IEnumerable<double> bulges, IEnumerable<GeoVector3> normals)
            : this(vertices, bulges, normals, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes a loop whose pieces may be arcs, within a tolerance.
        /// </summary>
        /// <param name="vertices">The vertices, in order; at least three, and no two neighbours in the same place.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex; null or short means the rest are straight.</param>
        /// <param name="normals">The plane each bulge is read about; only wanted where a bulge is not nought.</param>
        /// <param name="tolerance">The tolerance; the planar threshold decides how flat is flat enough.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when fewer than three distinct vertices are given, when they do not lie in one plane, or
        /// when an arc bulges out of that plane.
        /// </exception>
        public GeoPolygonArc3(
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

                if (kept.Count > 0 && kept[kept.Count - 1].IsEqualTo(vertex, tolerance))
                {
                    continue;
                }

                kept.Add(vertex);
                keptBulges.Add(bulge);
                keptNormals.Add(normal);
            }

            // The loop closes on itself, so a last vertex sitting on the first is the same repeat.
            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], tolerance))
            {
                kept.RemoveAt(kept.Count - 1);
                keptBulges.RemoveAt(keptBulges.Count - 1);
                keptNormals.RemoveAt(keptNormals.Count - 1);
            }

            if (kept.Count < 3)
            {
                throw new ArgumentException("A loop needs at least 3 distinct vertices.", nameof(vertices));
            }

            // A loop that does not lie flat encloses nothing, so it is refused where it is built.
            if (!new GeoPolyline3(kept).TryGetPlane(out GeoPlane3 plane, tolerance))
            {
                throw new ArgumentException("A closed loop of arcs has to lie in one plane; a bar bent out of one belongs in a GeoPolylineArc3.", nameof(vertices));
            }

            _vertices = kept.ToArray();
            _bulges = keptBulges.ToArray();
            _normals = keptNormals.ToArray();
            _planeNormal = plane.Normal;

            for (int i = 0; i < EdgeCount; i++)
            {
                GeoEdge3 edge = new GeoEdge3(_vertices[i], _vertices[(i + 1) % _vertices.Length], _bulges[i], _normals[i], tolerance);

                if (edge.IsArc && !edge.Normal.IsParallelTo(_planeNormal, tolerance))
                {
                    throw new ArgumentException("An arc of a closed loop has to bulge in the plane of the loop.", nameof(normals));
                }

                _bulges[i] = edge.Bulge;
                _normals[i] = edge.Normal;
            }
        }

        /// <summary>
        /// Initializes a loop from edges that run end to end and close.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the edges do not run end to end, do not close, or are not coplanar.</exception>
        public GeoPolygonArc3(IEnumerable<GeoEdge3> edges)
            : this(Walked(edges, out List<double> bulges, out List<GeoVector3> normals), bulges, normals, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes a loop from a straight one, with no bulges.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPolygonArc3(GeoPolygon3 polygon)
            : this(Required(polygon).Vertices, null, null, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes a loop by lifting one out of a frame into the plane of that frame in space.
        /// </summary>
        /// <param name="frame">The frame the loop is laid out in.</param>
        /// <param name="loop">The loop in the plane.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPolygonArc3(GeoCoordinateSystem3 frame, GeoPolygonArc2 loop)
            : this(Lifted(frame, loop, out List<double> bulges, out List<GeoVector3> normals), bulges, normals, Tolerance.Global)
        {
        }

        /// <summary>
        /// Gets the number of vertices, which is also the number of edges.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets the number of edges, which is also the number of vertices.
        /// </summary>
        public int EdgeCount => _vertices.Length;

        /// <summary>
        /// Gets the vertex at an index.
        /// </summary>
        public GeoPoint3 this[int index] => _vertices[index];

        /// <summary>
        /// Gets the bulge of the edge leaving a vertex.
        /// </summary>
        public double GetBulgeAt(int index) => _bulges[index];

        /// <summary>
        /// Gets the plane the bulge leaving a vertex is read about.
        /// </summary>
        public GeoVector3 GetNormalAt(int index) => _normals[index];

        /// <summary>
        /// Gets the edge at an index; the last closes the loop.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no edge at that index.</exception>
        public GeoEdge3 GetEdgeAt(int index)
        {
            if (index < 0 || index >= EdgeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "The loop has no edge at that index.");
            }

            GeoPoint3 start = _vertices[index];
            GeoPoint3 end = _vertices[(index + 1) % _vertices.Length];

            return _bulges[index] == 0.0 ? new GeoEdge3(start, end) : new GeoEdge3(start, end, _bulges[index], _normals[index]);
        }

        /// <summary>
        /// Gets the edges of the loop, in order.
        /// </summary>
        public IEnumerable<GeoEdge3> GetEdges()
        {
            for (int i = 0; i < EdgeCount; i++)
            {
                yield return GetEdgeAt(i);
            }
        }

        /// <summary>
        /// Gets the plane the loop lies in.
        /// </summary>
        public GeoPlane3 GetPlane() => new GeoPlane3(_vertices[0], _planeNormal);

        /// <summary>
        /// Gets the frame the loop is laid out in, which turns with the loop.
        /// </summary>
        public GeoCoordinateSystem3 GetFrame() => PlanarMap.FrameOf(_vertices, _planeNormal);

        /// <summary>
        /// Lays the loop out in its own frame.
        /// </summary>
        /// <remarks>
        /// This is where every answer about area, about what is inside, and about offsetting comes from. The
        /// frame turns with the loop, so a plate gives the same local coordinates whichever way it is
        /// oriented in the model.
        /// </remarks>
        public GeoPolygonArc2 ToPolygonArc2() => ProjectToPolygonArc2(GetFrame());

        /// <summary>
        /// Lays the loop out in a given frame.
        /// </summary>
        /// <param name="frame">The frame; hold on to it, because putting an answer back needs the same one.</param>
        public GeoPolygonArc2 ProjectToPolygonArc2(GeoCoordinateSystem3 frame)
        {
            var vertices = new List<GeoPoint2>(EdgeCount);
            var bulges = new List<double>(EdgeCount);

            for (int i = 0; i < EdgeCount; i++)
            {
                GeoEdge3 edge = GetEdgeAt(i);

                vertices.Add(PlanarMap.ProjectToPoint2(frame, edge.StartPoint));

                // An arc whose normal runs against the frame is the same arc seen from behind, so its
                // bulge turns over on the way down.
                bool facingAway = edge.IsArc && edge.Normal.DotProduct(frame.ZAxis) < 0.0;

                bulges.Add(facingAway ? -edge.Bulge : edge.Bulge);
            }

            return new GeoPolygonArc2(vertices, bulges);
        }

        /// <summary>
        /// Gets the length round the loop, measured along its arcs.
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
        /// Gets the area the loop encloses, counting each arc against its own chord.
        /// </summary>
        public double Area => ToPolygonArc2().Area;

        /// <summary>
        /// Gets the area the loop encloses, negative when it runs clockwise in its own frame.
        /// </summary>
        public double SignedArea => ToPolygonArc2().SignedArea;

        /// <summary>
        /// Gets a value indicating whether the loop runs clockwise in its own frame.
        /// </summary>
        public bool IsClockwise => ToPolygonArc2().IsClockwise;

        /// <summary>
        /// Gets the centroid of the region the loop encloses.
        /// </summary>
        public GeoPoint3 Centroid
        {
            get
            {
                GeoCoordinateSystem3 frame = GetFrame();

                return PlanarMap.ToPoint3(frame, ProjectToPolygonArc2(frame).Centroid);
            }
        }

        /// <summary>
        /// Determines whether no two edges of the loop cross.
        /// </summary>
        public bool IsSimple() => IsSimple(Tolerance.Global);

        /// <summary>
        /// Determines whether no two edges of the loop cross, within a tolerance.
        /// </summary>
        public bool IsSimple(Tolerance tolerance) => ToPolygonArc2().IsSimple(tolerance);

        /// <summary>
        /// Says where a point sits relative to the loop.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Says where a point sits relative to the loop, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A point off the plane of the loop is outside it, whatever it is over: a flat loop encloses a
        /// region of its own plane and nothing above or below it.
        /// </remarks>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            if (Math.Abs(frame.ToLocal(point).Z) > tolerance.EqualPlanar)
            {
                return PointLocation.OutSide;
            }

            return ProjectToPolygonArc2(frame).Locate(PlanarMap.ProjectToPoint2(frame, point), tolerance);
        }

        /// <summary>
        /// Determines whether the loop holds a point, on its outline or inside it.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Contains(point, Tolerance.Global);

        /// <summary>
        /// Determines whether the loop holds a point, on its outline or inside it, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Locate(point, tolerance) != PointLocation.OutSide;

        /// <summary>
        /// Gets the point of the outline nearest another point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the outline nearest another point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance)
            => ArcChain3.ClosestPoint(Edges(), point, tolerance);

        /// <summary>
        /// Gets the distance from the outline of the loop to a point.
        /// </summary>
        /// <remarks>
        /// Measured to the outline, so a point inside the loop is not nought away. That is what
        /// <see cref="Contains(GeoPoint3)"/> is for.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the outline of the loop to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));

        /// <summary>
        /// Determines whether a point lies on the outline of the loop.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on the outline of the loop, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Gets the point a given distance round the loop, walked along its arcs.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => ArcChain3.PointAtDistance(Edges(), distance);

        /// <summary>
        /// Gets the point a given way round the loop, from nought at the first vertex to one back at it.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => GetPointAtDistance(GetDistanceAtParameter(parameter));

        /// <summary>
        /// Gets the distance round the loop at a given parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter)
        {
            double held = parameter < 0.0 ? 0.0 : parameter > 1.0 ? 1.0 : parameter;

            return held * Length;
        }

        /// <summary>
        /// Gets the parameter at a given distance round the loop.
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
        /// Gets the distance round the loop to the point on it nearest another point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => GetDistanceAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance round the loop to the point on it nearest another point, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point, Tolerance tolerance) => ArcChain3.DistanceAtPoint(Edges(), point, tolerance);

        /// <summary>
        /// Gets the parameter of the point on the loop nearest another point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the parameter of the point on the loop nearest another point, within a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance) => GetParameterAtDistance(GetDistanceAtPoint(point, tolerance));

        /// <summary>
        /// Gets the smallest axis-aligned box holding the loop, arcs and all.
        /// </summary>
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
        /// Rounds every corner of the loop by the same radius.
        /// </summary>
        /// <remarks>
        /// A closed stirrup with a bending radius is this call. The loop is flat, so the rounding is done in
        /// its own plane by the same arithmetic the plane uses, and lifted back exactly.
        /// </remarks>
        public GeoPolygonArc3 Fillet(double radius) => Fillet(radius, Tolerance.Global);

        /// <summary>
        /// Rounds every corner of the loop by the same radius, within a tolerance.
        /// </summary>
        public GeoPolygonArc3 Fillet(double radius, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return new GeoPolygonArc3(frame, ProjectToPolygonArc2(frame).Fillet(radius, tolerance));
        }

        /// <summary>
        /// Rounds the corners of the loop, each by its own radius.
        /// </summary>
        public GeoPolygonArc3 Fillet(IReadOnlyList<double> radii) => Fillet(radii, Tolerance.Global);

        /// <summary>
        /// Rounds the corners of the loop, each by its own radius, within a tolerance.
        /// </summary>
        public GeoPolygonArc3 Fillet(IReadOnlyList<double> radii, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return new GeoPolygonArc3(frame, ProjectToPolygonArc2(frame).Fillet(radii, tolerance));
        }

        /// <summary>
        /// Moves the outline of the loop sideways in its own plane.
        /// </summary>
        /// <param name="distance">How far to move it: outward when positive, inward when negative.</param>
        /// <returns>The loops that come of it, which may be none when the loop is eaten away, or more than one when it is pinched in two.</returns>
        public GeoPolygonArc3[] Offset(double distance) => Offset(distance, Tolerance.Global);

        /// <summary>
        /// Moves the outline of the loop sideways in its own plane, within a tolerance.
        /// </summary>
        public GeoPolygonArc3[] Offset(double distance, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();
            GeoPolygonArc2[] moved = ProjectToPolygonArc2(frame).Offset(distance, tolerance);
            var lifted = new GeoPolygonArc3[moved.Length];

            for (int i = 0; i < moved.Length; i++)
            {
                lifted[i] = new GeoPolygonArc3(frame, moved[i]);
            }

            return lifted;
        }

        /// <summary>
        /// Gets the loop with its arcs thrown away, leaving the chords.
        /// </summary>
        public GeoPolygon3 Flatten() => new GeoPolygon3(_vertices);

        /// <summary>
        /// Gets the loop as an open chain running once round it.
        /// </summary>
        public GeoPolylineArc3 ToPolylineArc3()
        {
            var edges = new List<GeoEdge3>(EdgeCount);

            for (int i = 0; i < EdgeCount; i++)
            {
                edges.Add(GetEdgeAt(i));
            }

            return new GeoPolylineArc3(edges);
        }

        /// <summary>
        /// Gets the loop as a straight one following its arcs to a given accuracy.
        /// </summary>
        public GeoPolygon3 ToPolygon3(double chordTolerance) => ToPolylineArc3().ToPolyline3(chordTolerance).ToPolygon();

        /// <summary>
        /// Gets the loop run the other way round, drawing the same outline.
        /// </summary>
        public GeoPolygonArc3 Reverse()
        {
            GeoCoordinateSystem3 frame = GetFrame();

            return new GeoPolygonArc3(frame, ProjectToPolygonArc2(frame).Reverse());
        }

        /// <summary>
        /// Creates a copy of this loop.
        /// </summary>
        public GeoPolygonArc3 Clone() => new GeoPolygonArc3(_vertices, _bulges, _normals);

        /// <summary>
        /// Moves the loop by a vector.
        /// </summary>
        public GeoPolygonArc3 Translate(GeoVector3 vector)
        {
            var moved = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = _vertices[i].Add(vector);
            }

            return new GeoPolygonArc3(moved, _bulges, _normals);
        }

        /// <summary>
        /// Applies a transformation to the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoPolygonArc3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            var edges = new List<GeoEdge3>(EdgeCount);

            for (int i = 0; i < EdgeCount; i++)
            {
                edges.Add(GetEdgeAt(i).TransformBy(transform));
            }

            return new GeoPolygonArc3(edges);
        }

        /// <summary>
        /// Determines whether this loop draws the same outline as another.
        /// </summary>
        public bool IsEqualTo(GeoPolygonArc3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this loop draws the same outline as another, within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPolygonArc3 other, Tolerance tolerance)
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
        /// Indicates whether the current loop holds the same vertices, bulges and planes as another.
        /// </summary>
        public bool Equals(GeoPolygonArc3 other)
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
        public override bool Equals(object obj) => obj is GeoPolygonArc3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 19;

                for (int i = 0; i < VertexCount; i++)
                {
                    hash = (hash * 397) ^ _vertices[i].GetHashCode();
                    hash = (hash * 397) ^ _bulges[i].GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Determines whether two loops hold the same vertices, bulges and planes.
        /// </summary>
        public static bool operator ==(GeoPolygonArc3 left, GeoPolygonArc3 right)
            => left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Determines whether two loops differ.
        /// </summary>
        public static bool operator !=(GeoPolygonArc3 left, GeoPolygonArc3 right) => !(left == right);

        /// <summary>
        /// Returns a string describing the loop.
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
                "GeoPolygonArc3[{0} vertices, {1} curved, area {2:0.###}]",
                VertexCount,
                arcs,
                Area);
        }

        /// <summary>
        /// Gets the edges of the loop as a list, for the walking helpers.
        /// </summary>
        private List<GeoEdge3> Edges()
        {
            var edges = new List<GeoEdge3>(EdgeCount);

            for (int i = 0; i < EdgeCount; i++)
            {
                edges.Add(GetEdgeAt(i));
            }

            return edges;
        }

        /// <summary>
        /// Walks a run of edges that closes, pulling out the vertices, the bulges and the planes.
        /// </summary>
        private static List<GeoPoint3> Walked(IEnumerable<GeoEdge3> edges, out List<double> bulges, out List<GeoVector3> normals)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));

            var vertices = new List<GeoPoint3>();
            bulges = new List<double>();
            normals = new List<GeoVector3>();

            bool walked = false;
            GeoPoint3 previousEnd = default(GeoPoint3);

            foreach (GeoEdge3 edge in edges)
            {
                // A loop holds each vertex once, so it is the end of the edge before that has to meet the
                // start of this one, not the start of the edge before.
                if (walked && !previousEnd.IsEqualTo(edge.StartPoint))
                {
                    throw new ArgumentException("The edges of a loop must run end to end.", nameof(edges));
                }

                vertices.Add(edge.StartPoint);
                bulges.Add(edge.Bulge);
                normals.Add(edge.Normal);

                previousEnd = edge.EndPoint;
                walked = true;
            }

            if (vertices.Count < 3)
            {
                throw new ArgumentException("A loop needs at least 3 edges.", nameof(edges));
            }

            if (!previousEnd.IsEqualTo(vertices[0]))
            {
                throw new ArgumentException("The edges of a loop must close back on the first of them.", nameof(edges));
            }

            return vertices;
        }

        /// <summary>
        /// Lifts a loop out of a frame, pulling out the vertices, the bulges and the planes.
        /// </summary>
        private static List<GeoPoint3> Lifted(
            GeoCoordinateSystem3 frame,
            GeoPolygonArc2 loop,
            out List<double> bulges,
            out List<GeoVector3> normals)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            var vertices = new List<GeoPoint3>(loop.VertexCount);
            bulges = new List<double>(loop.VertexCount);
            normals = new List<GeoVector3>(loop.VertexCount);

            for (int i = 0; i < loop.VertexCount; i++)
            {
                vertices.Add(PlanarMap.ToPoint3(frame, loop[i]));
                bulges.Add(loop.GetBulgeAt(i));
                normals.Add(loop.GetBulgeAt(i) == 0.0 ? default(GeoVector3) : frame.ZAxis);
            }

            return vertices;
        }

        /// <summary>
        /// Guards a polygon being widened, so the null check reads before the base call rather than after it.
        /// </summary>
        private static GeoPolygon3 Required(GeoPolygon3 polygon)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return polygon;
        }
    }
}
