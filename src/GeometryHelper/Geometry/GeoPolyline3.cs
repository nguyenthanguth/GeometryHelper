using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents an open polygonal chain of 3D points.
    /// <para>
    /// A polyline is always an open chain: it never joins its last vertex back to its first, and it
    /// encloses nothing. Geometry meant to enclose an area is a <see cref="GeoPolygon3"/>, and
    /// <see cref="ToPolygon"/> converts between them. That is why a polyline offers <c>IsPointOn</c> but
    /// no <c>Contains</c>: a curve has no interior for a point to be inside of.
    /// </para>
    /// <para>
    /// The vertices need not be coplanar. A polyline may wander freely through space, which is what
    /// separates it from a polygon and is why it carries no normal and no area.
    /// </para>
    /// </summary>
    public sealed partial class GeoPolyline3 : IEquatable<GeoPolyline3>
    {
        private readonly GeoPoint3[] _vertices;

        /// <summary>
        /// Gets the read-only list of vertices defining the polyline.
        /// </summary>
        public IReadOnlyList<GeoPoint3> Vertices => _vertices;

        /// <summary>
        /// Gets the number of vertices.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets the number of segments, one fewer than the number of vertices.
        /// </summary>
        public int EdgeCount => _vertices.Length - 1;

        /// <summary>
        /// Gets the total length of the polyline.
        /// </summary>
        public double Length { get; }

        /// <summary>
        /// Initializes a new polyline from a sequence of vertices.
        /// </summary>
        /// <param name="vertices">The vertices, in order along the chain.</param>
        /// <exception cref="ArgumentNullException">Thrown when the sequence is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct vertices remain.</exception>
        /// <remarks>
        /// Consecutive duplicates within tolerance are dropped, so no zero-length segment survives.
        /// Comparing exactly instead would let a pair of vertices a nanometre apart through and leave a
        /// segment with no usable direction in the middle of the chain.
        /// </remarks>
        public GeoPolyline3(IEnumerable<GeoPoint3> vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            List<GeoPoint3> kept = new List<GeoPoint3>();

            foreach (GeoPoint3 vertex in vertices)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].IsEqualTo(vertex))
                {
                    kept.Add(vertex);
                }
            }

            if (kept.Count < 2)
            {
                throw new ArgumentException("A polyline must have at least 2 distinct vertices.", nameof(vertices));
            }

            _vertices = kept.ToArray();
            Length = MeasureLength(_vertices);
        }

        /// <summary>
        /// Initializes a new polyline directly from vertex arguments.
        /// </summary>
        public GeoPolyline3(params GeoPoint3[] vertices)
            : this((IEnumerable<GeoPoint3>)vertices)
        {
        }

        /// <summary>
        /// Initializes a polyline from vertices that have already been filtered and validated.
        /// </summary>
        /// <remarks>
        /// <see cref="Clone"/> and the operations that rebuild a polyline use this instead of the public
        /// constructor. The public one re-filters against <see cref="Tolerance.Global"/>, so a copy taken
        /// after that global was widened could come back with fewer vertices than the original, or fail
        /// validation outright. <see cref="Core.Offset3"/> uses it because it has already cleaned its chains
        /// against the tolerance its caller passed.
        /// </remarks>
        internal GeoPolyline3(GeoPoint3[] validatedVertices, bool takeOwnership)
        {
            _vertices = takeOwnership ? validatedVertices : (GeoPoint3[])validatedVertices.Clone();
            Length = MeasureLength(_vertices);
        }

        /// <summary>
        /// Measures the total length of a vertex chain.
        /// </summary>
        private static double MeasureLength(GeoPoint3[] vertices)
        {
            double total = 0.0;

            for (int i = 0; i < vertices.Length - 1; i++)
            {
                total += vertices[i].DistanceTo(vertices[i + 1]);
            }

            return total;
        }

        /// <summary>
        /// Creates a copy of this polyline.
        /// </summary>
        public GeoPolyline3 Clone() => new GeoPolyline3(_vertices, false);

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
        /// Gets the first vertex, where the chain starts.
        /// </summary>
        public GeoPoint3 StartPoint => _vertices[0];

        /// <summary>
        /// Gets the last vertex, where the chain ends.
        /// </summary>
        public GeoPoint3 EndPoint => _vertices[_vertices.Length - 1];

        /// <summary>
        /// Gets the segment at a given index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public GeoLine3 GetEdgeAt(int index)
        {
            if (index < 0 || index >= EdgeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new GeoLine3(_vertices[index], _vertices[index + 1]);
        }

        /// <summary>
        /// Gets every segment of the polyline, in order.
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
        /// Gets the polyline running the other way.
        /// </summary>
        public GeoPolyline3 Reverse()
        {
            GeoPoint3[] reversed = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                reversed[i] = _vertices[_vertices.Length - 1 - i];
            }

            return new GeoPolyline3(reversed, true);
        }

        /// <summary>
        /// Gets the polygon that closes this chain, joining its last vertex back to its first.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the chain has fewer than three distinct vertices or is not planar, since a polygon
        /// requires both.
        /// </exception>
        public GeoPolygon3 ToPolygon() => new GeoPolygon3(_vertices);

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this polyline.
        /// </summary>
        public GeoAabb3 GetAabb() => GeoAabb3.FromPoints(_vertices);

        /// <summary>
        /// Moves the chain by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The chain in its new place.</returns>
        public GeoPolyline3 Translate(GeoVector3 vector) => TransformBy(GeoTransform3.Translation(vector));

        /// <summary>
        /// Applies a transformation to every vertex.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoPolyline3 TransformBy(GeoTransform3 transform)
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

            // The public constructor is right here rather than the private one: a transformation that
            // scales down can bring two vertices within tolerance of each other, and the result should
            // then be a shorter chain rather than one carrying a zero-length segment.
            return new GeoPolyline3(moved);
        }

        #region Planarity

        /// <summary>
        /// Checks whether every vertex lies on a common plane, using the default tolerance.
        /// </summary>
        public bool IsPlanar() => IsPlanar(Tolerance.Global);

        /// <summary>
        /// Checks whether every vertex lies on a common plane, within a tolerance.
        /// </summary>
        public bool IsPlanar(Tolerance tolerance) => TryGetPlane(out _, tolerance);

        /// <summary>
        /// Tries to get the plane every vertex lies on, using the default tolerance.
        /// </summary>
        public bool TryGetPlane(out GeoPlane3 plane) => TryGetPlane(out plane, Tolerance.Global);

        /// <summary>
        /// Tries to get the plane every vertex lies on, within a tolerance.
        /// </summary>
        /// <param name="plane">The carrier plane when the method returns true.</param>
        /// <param name="tolerance">The tolerance; the planar threshold decides how flat is flat enough.</param>
        /// <returns>false when the vertices are collinear or do not share a plane.</returns>
        /// <remarks>
        /// Collinear vertices are refused rather than given an arbitrary plane through them: a straight
        /// chain lies on infinitely many planes and there is no ground for preferring one.
        /// </remarks>
        public bool TryGetPlane(out GeoPlane3 plane, Tolerance tolerance)
        {
            plane = default;

            GeoVector3 normal = Newell.GetAreaVector(_vertices);

            if (!normal.TryGetNormal(out GeoVector3 unitNormal, tolerance))
            {
                return false;
            }

            GeoPlane3 candidate = new GeoPlane3(_vertices[0], unitNormal);

            if (!candidate.ContainsAll(_vertices, tolerance))
            {
                return false;
            }

            plane = candidate;
            return true;
        }

        #endregion

        #region Parametrization and queries

        /// <summary>
        /// Gets the point at a normalized parameter along the polyline. Values outside [0, 1] clamp to the
        /// ends, because an open chain has no single direction to extend along.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => Parametrization3.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the point at an arc length measured from the start of the polyline.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => Parametrization3.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the normalized parameter of the point on this polyline closest to the supplied point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => Parametrization3.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the start of this polyline to the point on it closest to the supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => Parametrization3.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the start of this polyline to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization3.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start of this polyline.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization3.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Checks whether a point lies on this polyline, using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => Containment3.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on this polyline, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Containment3.IsPointOn(this, point, tolerance);

        #endregion

        #region Splitting

        /// <summary>
        /// Splits this polyline at an arc length from its start, using the default tolerance.
        /// </summary>
        public bool TrySplitAtDistance(double distance, out GeoPolyline3[] pieces) => Splition3.TrySplitAtDistance(this, distance, out pieces);

        /// <summary>
        /// Splits this polyline at an arc length from its start, within a tolerance.
        /// </summary>
        public bool TrySplitAtDistance(double distance, out GeoPolyline3[] pieces, Tolerance tolerance) => Splition3.TrySplitAtDistance(this, distance, out pieces, tolerance);

        /// <summary>
        /// Splits this polyline at a point on it, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPoint3 point, out GeoPolyline3[] pieces) => Splition3.TrySplitBy(this, point, out pieces);

        /// <summary>
        /// Splits this polyline at a point on it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPoint3 point, out GeoPolyline3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, point, out pieces, tolerance);

        /// <summary>
        /// Splits this polyline wherever a plane crosses it, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolyline3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this polyline wherever a plane crosses it, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolyline3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Splits this polyline at several arc lengths at once, using the default tolerance.
        /// </summary>
        public GeoPolyline3[] SplitAtDistances(IEnumerable<double> distances) => Splition3.SplitAtDistances(this, distances);

        /// <summary>
        /// Splits this polyline at several arc lengths at once, within a tolerance.
        /// </summary>
        public GeoPolyline3[] SplitAtDistances(IEnumerable<double> distances, Tolerance tolerance) => Splition3.SplitAtDistances(this, distances, tolerance);

        /// <summary>
        /// Splits this polyline by a solid, sorting the pieces into those inside it and those outside,
        /// using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this polyline by a solid, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this polyline by several solids taken together as their union, using the default
        /// tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside) => Splition3.TrySplitBy(this, cutters, out inside, out outside);

        /// <summary>
        /// Splits this polyline by several solids taken together as their union, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoSolid3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutters, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this polyline by an oriented box, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this polyline by an oriented box, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this polyline by an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside) => Splition3.TrySplitBy(this, cutter, out inside, out outside);

        /// <summary>
        /// Splits this polyline by an axis-aligned box, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3 cutter, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this polyline by several oriented boxes taken together as their union, using the default
        /// tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside) => Splition3.TrySplitBy(this, cutters, out inside, out outside);

        /// <summary>
        /// Splits this polyline by several oriented boxes taken together as their union, within a
        /// tolerance.
        /// </summary>
        public bool TrySplitBy(GeoObb3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutters, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this polyline by several axis-aligned boxes taken together as their union, using the
        /// default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside) => Splition3.TrySplitBy(this, cutters, out inside, out outside);

        /// <summary>
        /// Splits this polyline by several axis-aligned boxes taken together as their union, within a
        /// tolerance.
        /// </summary>
        public bool TrySplitBy(GeoAabb3[] cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Tolerance tolerance) => Splition3.TrySplitBy(this, cutters, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this chain by a plane and sorts the pieces by side, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolyline3[] above, out GeoPolyline3[] below) => Splition3.TrySplitBy(this, cutter, out above, out below);

        /// <summary>
        /// Splits this chain by a plane and sorts the pieces by side, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoPolyline3[] above, out GeoPolyline3[] below, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out above, out below, tolerance);

        /// <summary>
        /// Splits this chain wherever it passes through a polygon, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolygon3 cutter, out GeoPolyline3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this chain wherever it passes through a polygon, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPolygon3 cutter, out GeoPolyline3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Splits this chain wherever it passes through a face, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoFace3 cutter, out GeoPolyline3[] pieces) => Splition3.TrySplitBy(this, cutter, out pieces);

        /// <summary>
        /// Splits this chain wherever it passes through a face, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoFace3 cutter, out GeoPolyline3[] pieces, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Gets the curve parallel to this flat polyline at a distance to its left within its plane, with sharp
        /// corners, using the default tolerance. See
        /// <see cref="Offset3.OffsetInPlane(GeoPolyline3, double, GeoVector3, OffsetOptions, Tolerance)"/>.
        /// </summary>
        /// <param name="distance">How far to move it, to its left seen from the tip of the normal when positive.</param>
        /// <param name="planeNormal">Which side of the plane it is seen from.</param>
        public GeoPolyline3[] OffsetInPlane(double distance, GeoVector3 planeNormal) => Offset3.OffsetInPlane(this, distance, planeNormal);

        /// <summary>
        /// Gets the curve parallel to this flat polyline at a distance to its left within its plane, with sharp
        /// corners, within a tolerance.
        /// </summary>
        public GeoPolyline3[] OffsetInPlane(double distance, GeoVector3 planeNormal, Tolerance tolerance) => Offset3.OffsetInPlane(this, distance, planeNormal, tolerance);

        /// <summary>
        /// Gets the curve parallel to this flat polyline at a distance to its left within its plane, with the
        /// given corners, using the default tolerance.
        /// </summary>
        public GeoPolyline3[] OffsetInPlane(double distance, GeoVector3 planeNormal, OffsetJoin join) => Offset3.OffsetInPlane(this, distance, planeNormal, join);

        /// <summary>
        /// Gets the curve parallel to this flat polyline at a distance to its left within its plane, with the
        /// given corners, within a tolerance.
        /// </summary>
        public GeoPolyline3[] OffsetInPlane(double distance, GeoVector3 planeNormal, OffsetJoin join, Tolerance tolerance) => Offset3.OffsetInPlane(this, distance, planeNormal, join, tolerance);

        /// <summary>
        /// Gets the curve parallel to this flat polyline at a distance to its left within its plane, as the
        /// options say, using the default tolerance.
        /// </summary>
        public GeoPolyline3[] OffsetInPlane(double distance, GeoVector3 planeNormal, OffsetOptions options) => Offset3.OffsetInPlane(this, distance, planeNormal, options);

        /// <summary>
        /// Gets the curve parallel to this flat polyline at a distance to its left within its plane, as the
        /// options say, within a tolerance.
        /// </summary>
        public GeoPolyline3[] OffsetInPlane(double distance, GeoVector3 planeNormal, OffsetOptions options, Tolerance tolerance) => Offset3.OffsetInPlane(this, distance, planeNormal, options, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another polyline has exactly the same vertices, in the same order.
        /// </summary>
        public bool Equals(GeoPolyline3 other)
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
        /// Determines whether the specified object is equal to the current polyline.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoPolyline3 other && Equals(other);

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
        /// Compares whether this polyline equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoPolyline3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this polyline equals another within a tolerance, ignoring which way round the
        /// chain runs.
        /// </summary>
        public bool IsEqualTo(GeoPolyline3 other, Tolerance tolerance)
        {
            if (other is null || _vertices.Length != other._vertices.Length)
            {
                return false;
            }

            bool forward = true;
            bool backward = true;

            for (int i = 0; i < _vertices.Length; i++)
            {
                if (forward && !_vertices[i].IsEqualTo(other._vertices[i], tolerance))
                {
                    forward = false;
                }

                if (backward && !_vertices[i].IsEqualTo(other._vertices[_vertices.Length - 1 - i], tolerance))
                {
                    backward = false;
                }

                if (!forward && !backward)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        /// <summary>
        /// Returns a string that represents the current polyline.
        /// </summary>
        public override string ToString() => $"Polyline3(Vertices: {VertexCount}, Length: {Length:0.###})";

        /// <summary>
        /// Lays this chain out in a frame, dropping each vertex's distance from the plane of that frame.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The chain in the plane of the frame.</returns>
        public GeoPolyline2 ProjectToPolyline2(GeoCoordinateSystem3 frame) => PlanarMap.ProjectToPolyline2(frame, this);
    }
}
