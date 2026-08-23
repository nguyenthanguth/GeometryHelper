using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a flat face in 3D space: an outer boundary polygon with optional holes cut out of it.
    /// <para>
    /// A face is what a polygon becomes once it needs to carry openings. A plate with bolt holes, a wall
    /// with a window: the outer loop says where the material ends and each inner loop says where it is
    /// missing. Every loop shares one plane, which is checked at construction.
    /// </para>
    /// </summary>
    public sealed class GeoFace3 : IEquatable<GeoFace3>
    {
        private readonly GeoPolygon3[] _holes;

        /// <summary>
        /// Gets the outer boundary of the face.
        /// </summary>
        public GeoPolygon3 Boundary { get; }

        /// <summary>
        /// Gets the read-only list of holes cut out of the face.
        /// </summary>
        public IReadOnlyList<GeoPolygon3> Holes => _holes;

        /// <summary>
        /// Gets the unit normal of the face, taken from its boundary.
        /// </summary>
        public GeoVector3 Normal => Boundary.Normal;

        /// <summary>
        /// Gets the area of the face, with the area of every hole removed.
        /// </summary>
        public double Area { get; }

        /// <summary>
        /// Initializes a face with no holes.
        /// </summary>
        /// <param name="boundary">The outer boundary.</param>
        /// <exception cref="ArgumentNullException">Thrown when the boundary is null.</exception>
        public GeoFace3(GeoPolygon3 boundary)
            : this(boundary, null, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes a face with holes.
        /// </summary>
        /// <param name="boundary">The outer boundary.</param>
        /// <param name="holes">The holes; null is read as none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the boundary is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a hole does not lie on the plane of the boundary.</exception>
        public GeoFace3(GeoPolygon3 boundary, IEnumerable<GeoPolygon3> holes)
            : this(boundary, holes, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes a face with holes, within a tolerance.
        /// </summary>
        /// <param name="boundary">The outer boundary.</param>
        /// <param name="holes">The holes; null is read as none.</param>
        /// <param name="tolerance">The tolerance deciding whether the holes share the boundary plane.</param>
        public GeoFace3(GeoPolygon3 boundary, IEnumerable<GeoPolygon3> holes, Tolerance tolerance)
        {
            Boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));

            List<GeoPolygon3> kept = new List<GeoPolygon3>();

            if (holes != null)
            {
                GeoPlane3 carrier = boundary.GetPlane();

                foreach (GeoPolygon3 hole in holes)
                {
                    if (hole == null)
                    {
                        throw new ArgumentException("A face cannot carry a null hole.", nameof(holes));
                    }

                    if (!carrier.ContainsAll(hole.Vertices, tolerance))
                    {
                        throw new ArgumentException("Every hole must lie on the plane of the boundary.", nameof(holes));
                    }

                    kept.Add(hole);
                }
            }

            _holes = kept.ToArray();

            double area = boundary.Area;
            foreach (GeoPolygon3 hole in _holes)
            {
                area -= hole.Area;
            }

            // A hole reaching outside the boundary, or two holes overlapping, would drive this below zero.
            // Neither is a face this type promises to handle, and clamping keeps the value usable rather
            // than letting a negative area propagate into a solid volume.
            Area = Math.Max(0.0, area);
        }

        /// <summary>
        /// Creates a copy of this face.
        /// </summary>
        public GeoFace3 Clone()
        {
            GeoPolygon3[] copies = new GeoPolygon3[_holes.Length];

            for (int i = 0; i < _holes.Length; i++)
            {
                copies[i] = _holes[i].Clone();
            }

            return new GeoFace3(Boundary.Clone(), copies);
        }

        /// <summary>
        /// Gets the plane carrying the face, oriented along its normal.
        /// </summary>
        public GeoPlane3 GetPlane() => Boundary.GetPlane();

        /// <summary>
        /// Gets the face with its orientation reversed, holes included.
        /// </summary>
        public GeoFace3 Flip()
        {
            GeoPolygon3[] flipped = new GeoPolygon3[_holes.Length];

            for (int i = 0; i < _holes.Length; i++)
            {
                flipped[i] = _holes[i].Flip();
            }

            return new GeoFace3(Boundary.Flip(), flipped);
        }

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this face.
        /// </summary>
        /// <remarks>
        /// Only the boundary is measured. A hole never reaches outside the boundary of a well formed face,
        /// so it cannot widen the box.
        /// </remarks>
        public GeoAabb3 GetAabb() => Boundary.GetAabb();

        /// <summary>
        /// Breaks the outer boundary into triangles, ignoring the holes.
        /// </summary>
        /// <remarks>
        /// The holes are left out because a fan triangulation cannot express them. What this is good for
        /// is the signed sums — area, centroid, volume — where the holes are accounted for separately by
        /// triangulating each of them and subtracting.
        /// </remarks>
        public GeoTriangle3[] Triangulate() => Boundary.Triangulate();

        /// <summary>
        /// Applies a transformation to the boundary and every hole.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoFace3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoPolygon3[] moved = new GeoPolygon3[_holes.Length];

            for (int i = 0; i < _holes.Length; i++)
            {
                moved[i] = _holes[i].TransformBy(transform);
            }

            return new GeoFace3(Boundary.TransformBy(transform), moved);
        }

        #region Queries

        /// <summary>
        /// Locates a point relative to this face, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Containment3.Locate(this, point);

        /// <summary>
        /// Locates a point relative to this face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A point inside a hole is outside the face, and a point on the rim of a hole is on the face
        /// boundary, since the rim is as much an edge of the material as the outer loop is.
        /// </remarks>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => Containment3.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this face holds a point, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Containment3.Contains(this, point);

        /// <summary>
        /// Checks whether this face holds a point, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Containment3.Contains(this, point, tolerance);

        /// <summary>
        /// Tries to find the point where a line segment crosses this face, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(line, this, out intersection);

        /// <summary>
        /// Tries to find the point where a line segment crosses this face, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine3 line, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(line, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find the point where a ray crosses this face, using the default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection) => Intersection3.TryIntersectWith(ray, this, out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses this face, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoRay3 ray, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(ray, this, out intersection, tolerance);

        /// <summary>
        /// Splits this face by a plane, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoFace3[] above, out GeoFace3[] below) => Splition3.TrySplitBy(this, cutter, out above, out below);

        /// <summary>
        /// Splits this face by a plane, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoFace3[] above, out GeoFace3[] below, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out above, out below, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another face has exactly the same boundary and holes, in the same order.
        /// </summary>
        public bool Equals(GeoFace3 other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (!Boundary.Equals(other.Boundary) || _holes.Length != other._holes.Length)
            {
                return false;
            }

            for (int i = 0; i < _holes.Length; i++)
            {
                if (!_holes[i].Equals(other._holes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current face.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoFace3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Boundary.GetHashCode();

                foreach (GeoPolygon3 hole in _holes)
                {
                    hash = hash * 31 + hole.GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Compares whether this face equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoFace3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this face equals another within a tolerance, ignoring the order the holes are
        /// listed in.
        /// </summary>
        public bool IsEqualTo(GeoFace3 other, Tolerance tolerance)
        {
            if (other is null || _holes.Length != other._holes.Length)
            {
                return false;
            }

            if (!Boundary.IsEqualTo(other.Boundary, tolerance))
            {
                return false;
            }

            bool[] matched = new bool[_holes.Length];

            foreach (GeoPolygon3 hole in _holes)
            {
                bool found = false;

                for (int i = 0; i < other._holes.Length; i++)
                {
                    if (!matched[i] && hole.IsEqualTo(other._holes[i], tolerance))
                    {
                        matched[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        /// <summary>
        /// Returns a string that represents the current face.
        /// </summary>
        public override string ToString() => $"Face3(Area: {Area:0.###}, Holes: {_holes.Length})";
    }
}
