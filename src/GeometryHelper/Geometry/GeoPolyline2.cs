using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a 2D polyline: an open chain of connected line segments.
    /// <para>
    /// A polyline is always a path, never a region. Geometry that closes back on itself is a
    /// <see cref="GeoPolygon2"/>, which is where area, containment and winding live; <see cref="ToPolygon"/>
    /// converts a chain whose shape is meant to enclose something.
    /// </para>
    /// </summary>
    public sealed partial class GeoPolyline2 : IEquatable<GeoPolyline2>
    {
        private readonly GeoPoint2[] _vertices;

        /// <summary>
        /// Gets the read-only list of vertices of the polyline.
        /// </summary>
        public IReadOnlyList<GeoPoint2> Vertices => _vertices;

        /// <summary>
        /// Gets the number of vertices.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets the number of line segment edges in the polyline, which is always one less than the
        /// number of vertices: the chain stops at the last vertex instead of running back to the first.
        /// </summary>
        public int EdgeCount => _vertices.Length - 1;

        /// <summary>
        /// Gets the total cumulative length of all segments in the polyline.
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
        /// Gets the point half way along the polyline, which can be used to probe the location of the whole polyline.
        /// </summary>
        public GeoPoint2 MidPoint => GetPointAtDistance(Length * 0.5);

        /// <summary>
        /// Initializes a new GeoPolyline2 instance from a collection of vertices.
        /// Consecutive duplicate vertices are automatically filtered out.
        /// </summary>
        /// <param name="vertices">The sequence of vertices.</param>
        public GeoPolyline2(IEnumerable<GeoPoint2> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));

            List<GeoPoint2> list = new List<GeoPoint2>();
            foreach (var pt in vertices)
            {
                if (list.Count == 0 || !list[list.Count - 1].IsEqualTo(pt))
                {
                    list.Add(pt);
                }
            }

            if (list.Count < 2)
            {
                throw new ArgumentException("A polyline must have at least 2 distinct vertices.");
            }

            _vertices = list.ToArray();
        }

        /// <summary>
        /// Initializes a new GeoPolyline2 from parameter vertices.
        /// </summary>
        /// <param name="vertices">The sequence of vertices.</param>
        public GeoPolyline2(params GeoPoint2[] vertices)
            : this((IEnumerable<GeoPoint2>)vertices)
        {
        }

        /// <summary>
        /// Initializes a polyline from vertices that have already been filtered and validated.
        /// </summary>
        /// <param name="validatedVertices">Source array, already free of consecutive duplicates.</param>
        /// <param name="count">Number of leading entries to copy.</param>
        /// <remarks>
        /// Clone and <see cref="Core.Splition2"/> use this instead of the public constructor. The
        /// public one re-filters vertices against Tolerance.Global, so a clone taken after that global was
        /// widened could silently come back with fewer vertices than the original, or fail validation
        /// outright. Splitting has the same need for a different reason: it receives an explicit tolerance
        /// from its caller and must not have its results re-filtered against an unrelated global one.
        /// <para>
        /// What this skips is the tolerance dependent work: filtering duplicates. The vertex count is
        /// still checked, because it costs nothing, reads no global state, and is what lets EdgeCount
        /// subtract one without guarding the result.
        /// </para>
        /// </remarks>
        internal GeoPolyline2(GeoPoint2[] validatedVertices, int count)
        {
            if (count < 2)
            {
                throw new ArgumentException("A polyline must have at least 2 distinct vertices.", nameof(count));
            }

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
        /// Gets the line segment edge at a given index.
        /// </summary>
        /// <param name="index">The 0-based edge index.</param>
        /// <returns>The line segment edge.</returns>
        public GeoLine2 GetEdgeAt(int index)
        {
            if (index < 0 || index >= EdgeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return new GeoLine2(_vertices[index], _vertices[(index + 1) % _vertices.Length]);
        }

        /// <summary>
        /// Enumerates all line segment edges of the polyline.
        /// </summary>
        public IEnumerable<GeoLine2> GetEdges()
        {
            for (int i = 0; i < EdgeCount; i++)
            {
                yield return GetEdgeAt(i);
            }
        }

        /// <summary>
        /// Gets the point at a normalized parameter along this polyline, where 0 is the first vertex and 1
        /// is the end. Values outside [0, 1] are clamped to the endpoints.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Parametrization2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point on this polyline closest to the supplied point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Parametrization2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at an arc length measured from the first vertex of this polyline.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Parametrization2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from the first vertex of this polyline to the point on it closest to the
        /// supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Parametrization2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the first vertex of this polyline to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the first vertex of this polyline.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization2.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Reverses the direction of the polyline.
        /// </summary>
        /// <returns>A new reversed GeoPolyline2.</returns>
        public GeoPolyline2 Reverse()
        {
            GeoPoint2[] rev = new GeoPoint2[_vertices.Length];
            Array.Copy(_vertices, rev, _vertices.Length);
            Array.Reverse(rev);
            return new GeoPolyline2(rev);
        }

        /// <summary>
        /// Creates a copy of this polyline holding its own vertex array.
        /// </summary>
        /// <returns>A new GeoPolyline2 independent of this one.</returns>
        public GeoPolyline2 Clone() => new GeoPolyline2(_vertices, _vertices.Length);

        /// <summary>
        /// Translates the polyline by a displacement vector.
        /// </summary>
        /// <param name="vector">The displacement vector.</param>
        /// <returns>A new translated GeoPolyline2.</returns>
        public GeoPolyline2 Translate(GeoVector2 vector)
        {
            GeoPoint2[] moved = new GeoPoint2[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
            {
                moved[i] = _vertices[i].Add(vector);
            }
            return new GeoPolyline2(moved);
        }

        /// <summary>
        /// Rotates the polyline around a center point by an angle in radians (counter-clockwise).
        /// </summary>
        /// <param name="angleRad">Rotation angle in radians.</param>
        /// <param name="center">Center of rotation.</param>
        /// <returns>A new rotated GeoPolyline2.</returns>
        public GeoPolyline2 RotateBy(double angleRad, GeoPoint2 center)
        {
            GeoPoint2[] rotated = new GeoPoint2[_vertices.Length];
            for (int i = 0; i < _vertices.Length; i++)
            {
                rotated[i] = _vertices[i].RotateBy(angleRad, center);
            }
            return new GeoPolyline2(rotated);
        }

        /// <summary>
        /// Converts this polyline into a solid 2D GeoPolygon2.
        /// The chain is closed by connecting the last vertex back to the first.
        /// </summary>
        /// <returns>A new GeoPolygon2 instance.</returns>
        public GeoPolygon2 ToPolygon() => new GeoPolygon2(_vertices);

        /// <summary>
        /// Checks whether a point lies on this polyline using default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => Containment2.IsPointOn(this, point);

        /// <summary>
        /// Checks whether a point lies on this polyline within tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => Containment2.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Classifies the location of a point relative to this polyline (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point, Tolerance.Global);

        /// <summary>
        /// Classifies the location of a point relative to this polyline (Inside, OutSide, or OnSide) within tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        /// <summary>
        /// Splits this polyline at a point lying on it, using the default tolerance.
        /// </summary>
        /// <param name="point">The point to split at, which must lie on this polyline.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint2 point, out GeoPolyline2 first, out GeoPolyline2 second)
            => Splition2.TrySplitBy(this, point, out first, out second, Tolerance.Global);

        /// <summary>
        /// Splits this polyline at a point lying on it, within tolerance.
        /// </summary>
        /// <param name="point">The point to split at, which must lie on this polyline.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint2 point, out GeoPolyline2 first, out GeoPolyline2 second, Tolerance tolerance)
            => Splition2.TrySplitBy(this, point, out first, out second, tolerance);

        /// <summary>
        /// Splits this polyline everywhere a cutting line segment crosses it, using the default tolerance.
        /// </summary>
        /// <param name="cutter">The cutting line segment.</param>
        /// <param name="pieces">The pieces in order along this polyline if split succeeds.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoLine2 cutter, out GeoPolyline2[] pieces)
            => Splition2.TrySplitBy(this, cutter, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits this polyline everywhere a cutting line segment crosses it, within tolerance.
        /// </summary>
        /// <param name="cutter">The cutting line segment.</param>
        /// <param name="pieces">The pieces in order along this polyline if split succeeds.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoLine2 cutter, out GeoPolyline2[] pieces, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Splits this polyline everywhere a list of points lies on it, using the default tolerance.
        /// </summary>
        /// <param name="points">The points to split at.</param>
        /// <param name="pieces">The resulting pieces in order along this polyline.</param>
        /// <returns>true if this polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint2[] points, out GeoPolyline2[] pieces)
            => Splition2.TrySplitBy(this, points, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits this polyline everywhere a list of points lies on it, within tolerance.
        /// </summary>
        /// <param name="points">The points to split at.</param>
        /// <param name="pieces">The resulting pieces in order along this polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if this polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint2[] points, out GeoPolyline2[] pieces, Tolerance tolerance)
            => Splition2.TrySplitBy(this, points, out pieces, tolerance);

        /// <summary>
        /// Splits this polyline everywhere a list of cutting line segments crosses it, using the default tolerance.
        /// </summary>
        /// <param name="cutters">The cutting line segments.</param>
        /// <param name="pieces">The resulting pieces in order along this polyline.</param>
        /// <returns>true if this polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoLine2[] cutters, out GeoPolyline2[] pieces)
            => Splition2.TrySplitBy(this, cutters, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits this polyline everywhere a list of cutting line segments crosses it, within tolerance.
        /// </summary>
        /// <param name="cutters">The cutting line segments.</param>
        /// <param name="pieces">The resulting pieces in order along this polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if this polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoLine2[] cutters, out GeoPolyline2[] pieces, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutters, out pieces, tolerance);

        /// <summary>
        /// Splits this polyline everywhere a list of cutting polylines crosses it, using the default tolerance.
        /// </summary>
        /// <param name="cutters">The cutting polylines.</param>
        /// <param name="pieces">The resulting pieces in order along this polyline.</param>
        /// <returns>true if this polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolyline2[] cutters, out GeoPolyline2[] pieces)
            => Splition2.TrySplitBy(this, cutters, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits this polyline everywhere a list of cutting polylines crosses it, within tolerance.
        /// </summary>
        /// <param name="cutters">The cutting polylines.</param>
        /// <param name="pieces">The resulting pieces in order along this polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if this polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolyline2[] cutters, out GeoPolyline2[] pieces, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutters, out pieces, tolerance);

        /// <summary>
        /// Splits this polyline everywhere a list of polygon boundaries crosses it, separating the parts that fall
        /// inside the polygons from those that fall outside, using the default tolerance.
        /// </summary>
        /// <param name="cutters">The polygons to split against.</param>
        /// <param name="inside">The sub-polylines lying inside the polygons, in order along this polyline.</param>
        /// <param name="outside">The sub-polylines lying outside the polygons, in order along this polyline.</param>
        /// <returns>true if this polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolygon2[] cutters, out GeoPolyline2[] inside, out GeoPolyline2[] outside)
            => Splition2.TrySplitBy(this, cutters, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Splits this polyline everywhere a list of polygon boundaries crosses it, separating the parts that fall
        /// inside the polygons from those that fall outside, within tolerance.
        /// </summary>
        /// <param name="cutters">The polygons to split against.</param>
        /// <param name="inside">The sub-polylines lying inside the polygons, in order along this polyline.</param>
        /// <param name="outside">The sub-polylines lying outside the polygons, in order along this polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if this polyline was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolygon2[] cutters, out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutters, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this polyline against a polygon, sorting the sub-polylines by which side of its boundary
        /// they fall on, using the default tolerance.
        /// </summary>
        /// <param name="cutter">The polygon to split against.</param>
        /// <param name="inside">The sub-polylines lying inside the polygon, in order along this polyline.</param>
        /// <param name="outside">The sub-polylines lying outside the polygon, in order along this polyline.</param>
        /// <returns>true if the polygon boundary crosses this polyline; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolygon2 cutter, out GeoPolyline2[] inside, out GeoPolyline2[] outside)
            => Splition2.TrySplitBy(this, cutter, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Splits this polyline against a polygon, sorting the sub-polylines by which side of its boundary
        /// they fall on, within tolerance.
        /// </summary>
        /// <param name="cutter">The polygon to split against.</param>
        /// <param name="inside">The sub-polylines lying inside the polygon, in order along this polyline.</param>
        /// <param name="outside">The sub-polylines lying outside the polygon, in order along this polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygon boundary crosses this polyline; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolygon2 cutter, out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this polyline at an arc length measured from its first vertex, using the default
        /// tolerance.
        /// </summary>
        /// <param name="distance">Arc length from the first vertex.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public bool TrySplitAtDistance(double distance, out GeoPolyline2 first, out GeoPolyline2 second)
            => Splition2.TrySplitAtDistance(this, distance, out first, out second, Tolerance.Global);

        /// <summary>
        /// Splits this polyline at an arc length measured from its first vertex, within tolerance.
        /// </summary>
        /// <param name="distance">Arc length from the first vertex.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polyline was split; otherwise, false.</returns>
        public bool TrySplitAtDistance(double distance, out GeoPolyline2 first, out GeoPolyline2 second, Tolerance tolerance)
            => Splition2.TrySplitAtDistance(this, distance, out first, out second, tolerance);

        /// <summary>
        /// Splits this polyline at several arc lengths measured from its first vertex, using the default
        /// tolerance.
        /// </summary>
        /// <param name="distances">Arc lengths from the first vertex, in any order.</param>
        /// <returns>The pieces in order along this polyline.</returns>
        public GeoPolyline2[] SplitAtDistances(IEnumerable<double> distances)
            => Splition2.SplitAtDistances(this, distances, Tolerance.Global);

        /// <summary>
        /// Splits this polyline at several arc lengths measured from its first vertex, within tolerance.
        /// </summary>
        /// <param name="distances">Arc lengths from the first vertex, in any order.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces in order along this polyline.</returns>
        public GeoPolyline2[] SplitAtDistances(IEnumerable<double> distances, Tolerance tolerance)
            => Splition2.SplitAtDistances(this, distances, tolerance);

        /// <summary>
        /// Translates a polyline by a vector.
        /// </summary>
        public static GeoPolyline2 operator +(GeoPolyline2 polyline, GeoVector2 vector) => polyline.Translate(vector);

        /// <summary>
        /// Gets the curve parallel to this polyline at a distance to its left, with sharp corners, using
        /// default tolerance. A negative distance goes to the right. See
        /// <see cref="Offset2.Offset(GeoPolyline2, double, OffsetOptions, Tolerance)"/>.
        /// </summary>
        /// <param name="distance">How far to move it, to the left of its direction when positive.</param>
        /// <returns>The pieces of the parallel curve, in order along the polyline; usually one.</returns>
        public GeoPolyline2[] Offset(double distance) => Offset2.Offset(this, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to this polyline at a distance to its left, with sharp corners, within
        /// tolerance. A negative distance goes to the right.
        /// </summary>
        public GeoPolyline2[] Offset(double distance, Tolerance tolerance) => Offset2.Offset(this, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Gets the curve parallel to this polyline at a distance to its left, with the given corners, using
        /// default tolerance. A negative distance goes to the right.
        /// </summary>
        public GeoPolyline2[] Offset(double distance, OffsetJoin join) => Offset2.Offset(this, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to this polyline at a distance to its left, with the given corners, within
        /// tolerance. A negative distance goes to the right.
        /// </summary>
        public GeoPolyline2[] Offset(double distance, OffsetJoin join, Tolerance tolerance) => Offset2.Offset(this, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Gets the curve parallel to this polyline at a distance to its left, as the options say, using
        /// default tolerance. A negative distance goes to the right.
        /// </summary>
        public GeoPolyline2[] Offset(double distance, OffsetOptions options) => Offset2.Offset(this, distance, options, Tolerance.Global);

        /// <summary>
        /// Gets the curve parallel to this polyline at a distance to its left, as the options say, within
        /// tolerance. A negative distance goes to the right.
        /// </summary>
        public GeoPolyline2[] Offset(double distance, OffsetOptions options, Tolerance tolerance) => Offset2.Offset(this, distance, options, tolerance);

        /// <summary>
        /// Translates a polyline backwards by a vector.
        /// </summary>
        public static GeoPolyline2 operator -(GeoPolyline2 polyline, GeoVector2 vector) => polyline.Translate(-vector);

        /// <summary>
        /// Indicates whether the current polyline is equal to another polyline.
        /// </summary>
        public bool Equals(GeoPolyline2 other)
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
        public override bool Equals(object obj) => obj is GeoPolyline2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
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
        /// Compares two GeoPolyline2 instances for equality.
        /// </summary>
        public static bool operator ==(GeoPolyline2 left, GeoPolyline2 right) => Equals(left, right);

        /// <summary>
        /// Compares two GeoPolyline2 instances for inequality.
        /// </summary>
        public static bool operator !=(GeoPolyline2 left, GeoPolyline2 right) => !Equals(left, right);

        /// <summary>
        /// Returns the string representation of the polyline.
        /// </summary>
        public override string ToString() => $"GeoPolyline2[{VertexCount} vertices, Length:{Length:0.###}]";

        /// <summary>
        /// Puts this chain of the plane back into space, on the plane of a frame.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <returns>The chain in space.</returns>
        public GeoPolyline3 ToPolyline3(GeoCoordinateSystem3 frame) => PlanarMap.ToPolyline3(frame, this);

        /// <summary>
        /// Applies a transformation to this chain.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed chain.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoPolyline2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Determines whether another chain runs through the same points, within the default tolerance.
        /// </summary>
        /// <param name="other">The chain to compare with.</param>
        /// <returns>true if the chains are the same within tolerance; otherwise, false.</returns>
        public bool IsEqualTo(GeoPolyline2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another chain runs through the same points, within a tolerance.
        /// </summary>
        /// <param name="other">The chain to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the chains are the same within tolerance; otherwise, false.</returns>
        /// <remarks>
        /// A chain has ends, so it is compared vertex by vertex from its start. A chain and the same chain
        /// reversed are not equal; compare one against <c>other.Reverse()</c> when either direction will do.
        /// </remarks>
        public bool IsEqualTo(GeoPolyline2 other, Tolerance tolerance)
        {
            if (other is null || _vertices.Length != other._vertices.Length)
            {
                return false;
            }

            for (int i = 0; i < _vertices.Length; i++)
            {
                if (!_vertices[i].IsEqualTo(other._vertices[i], tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Chamfers every corner that has room for it, cutting the same distance along both edges.
        /// </summary>
        /// <param name="distance">How far back along each edge the cut is measured.</param>
        /// <returns>The chamfered chain.</returns>
        public GeoPolyline2 Chamfer(double distance) => Corner2.Chamfer(this, distance);

        /// <summary>
        /// Chamfers every corner that has room for it, cutting the same distance along both edges, within a tolerance.
        /// </summary>
        public GeoPolyline2 Chamfer(double distance, Tolerance tolerance) => Corner2.Chamfer(this, distance, tolerance);

        /// <summary>
        /// Chamfers every corner that has room for it.
        /// </summary>
        /// <param name="distance1">How far back along the edge running into each corner.</param>
        /// <param name="distance2">How far back along the edge running out of it.</param>
        /// <returns>The chamfered chain.</returns>
        public GeoPolyline2 Chamfer(double distance1, double distance2) => Corner2.Chamfer(this, distance1, distance2);

        /// <summary>
        /// Chamfers every corner that has room for it, within a tolerance.
        /// </summary>
        public GeoPolyline2 Chamfer(double distance1, double distance2, Tolerance tolerance) => Corner2.Chamfer(this, distance1, distance2, tolerance);

        /// <summary>
        /// Chamfers one corner, using the default tolerance.
        /// </summary>
        /// <returns>true if the corner had room for the cut; otherwise, false.</returns>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolyline2 result) => Corner2.TryChamferAt(this, index, distance1, distance2, out result);

        /// <summary>
        /// Chamfers one corner, within a tolerance.
        /// </summary>
        /// <returns>true if the corner had room for the cut; otherwise, false.</returns>
        public bool TryChamferAt(int index, double distance1, double distance2, out GeoPolyline2 result, Tolerance tolerance) => Corner2.TryChamferAt(this, index, distance1, distance2, out result, tolerance);
    }
}
