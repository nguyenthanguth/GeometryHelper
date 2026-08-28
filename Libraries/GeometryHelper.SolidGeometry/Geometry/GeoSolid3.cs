using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;

namespace GeometryHelper.SolidGeometry.Geometry
{
    /// <summary>
    /// Represents a 3D solid body as the set of faces bounding it, with optional openings carved out.
    /// <para>
    /// The faces are expected to be wound so their normals point out of the body, and to close it without
    /// gaps. Neither is enforced, because checking costs a full pass over every edge and most solids come
    /// from a modeller that already guarantees it; <see cref="IsClosed()"/> is there for the ones that do
    /// not. What the guarantees buy is <see cref="Volume"/> and <see cref="Contains(GeoPoint3)"/>: both
    /// read the body as a filled region and neither means anything for a shell with a hole in it.
    /// </para>
    /// <para>
    /// An opening is a whole solid subtracted from this one — a duct through a slab, a recess in a
    /// footing. It is not the same thing as a hole in a <see cref="GeoFace3"/>, which is flat and belongs
    /// to a single face.
    /// </para>
    /// </summary>
    public sealed class GeoSolid3 : IEquatable<GeoSolid3>
    {
        private readonly GeoFace3[] _faces;
        private readonly GeoSolid3[] _openings;

        /// <summary>
        /// Gets the read-only list of faces bounding the solid.
        /// </summary>
        public IReadOnlyList<GeoFace3> Faces => _faces;

        /// <summary>
        /// Gets the read-only list of solids carved out of this one.
        /// </summary>
        public IReadOnlyList<GeoSolid3> Openings => _openings;

        /// <summary>
        /// Initializes a solid from its bounding faces.
        /// </summary>
        /// <param name="faces">The faces bounding the solid; at least four are needed to close a volume.</param>
        /// <exception cref="ArgumentNullException">Thrown when the sequence is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than four faces are given, or one is null.</exception>
        public GeoSolid3(IEnumerable<GeoFace3> faces)
            : this(faces, null)
        {
        }

        /// <summary>
        /// Initializes a solid from its bounding faces and the solids carved out of it.
        /// </summary>
        /// <param name="faces">The faces bounding the solid.</param>
        /// <param name="openings">The solids to subtract; null is read as none.</param>
        public GeoSolid3(IEnumerable<GeoFace3> faces, IEnumerable<GeoSolid3> openings)
        {
            if (faces == null)
            {
                throw new ArgumentNullException(nameof(faces));
            }

            List<GeoFace3> keptFaces = new List<GeoFace3>();

            foreach (GeoFace3 face in faces)
            {
                if (face == null)
                {
                    throw new ArgumentException("A solid cannot carry a null face.", nameof(faces));
                }

                keptFaces.Add(face);
            }

            // Four is the smallest number of flat faces that can enclose a volume, as a tetrahedron does.
            if (keptFaces.Count < 4)
            {
                throw new ArgumentException("A solid must have at least 4 faces to enclose a volume.", nameof(faces));
            }

            List<GeoSolid3> keptOpenings = new List<GeoSolid3>();

            if (openings != null)
            {
                foreach (GeoSolid3 opening in openings)
                {
                    if (opening == null)
                    {
                        throw new ArgumentException("A solid cannot carry a null opening.", nameof(openings));
                    }

                    keptOpenings.Add(opening);
                }
            }

            _faces = keptFaces.ToArray();
            _openings = keptOpenings.ToArray();
        }

        /// <summary>
        /// Initializes a solid directly from face arguments.
        /// </summary>
        public GeoSolid3(params GeoFace3[] faces)
            : this((IEnumerable<GeoFace3>)faces, null)
        {
        }

        /// <summary>
        /// Creates a copy of this solid, faces and openings included.
        /// </summary>
        public GeoSolid3 Clone()
        {
            GeoFace3[] faces = new GeoFace3[_faces.Length];
            for (int i = 0; i < _faces.Length; i++)
            {
                faces[i] = _faces[i].Clone();
            }

            GeoSolid3[] openings = new GeoSolid3[_openings.Length];
            for (int i = 0; i < _openings.Length; i++)
            {
                openings[i] = _openings[i].Clone();
            }

            return new GeoSolid3(faces, openings);
        }

        /// <summary>
        /// Gets this solid with the given openings carved out of it.
        /// </summary>
        /// <param name="openings">The solids to subtract, added to any this solid already carries.</param>
        public GeoSolid3 WithOpenings(IEnumerable<GeoSolid3> openings)
        {
            if (openings == null)
            {
                throw new ArgumentNullException(nameof(openings));
            }

            List<GeoSolid3> combined = new List<GeoSolid3>(_openings);
            combined.AddRange(openings);

            return new GeoSolid3(_faces, combined);
        }

        #region Measurements

        /// <summary>
        /// Gets the total area of the bounding faces, without counting the openings.
        /// </summary>
        public double SurfaceArea
        {
            get
            {
                double total = 0.0;

                foreach (GeoFace3 face in _faces)
                {
                    total += face.Area;
                }

                return total;
            }
        }

        /// <summary>
        /// Gets the gross volume enclosed by the bounding faces, ignoring the openings.
        /// </summary>
        /// <remarks>
        /// This is the divergence theorem: the volume of a closed body equals a sum over its surface, and
        /// for a flat-faced body that sum is one signed tetrahedron per triangle, taken back to the world
        /// origin. Where the origin sits makes no difference to the total as long as the surface closes —
        /// the parts outside the body cancel — which is why no reference point has to be chosen. The
        /// result is reported unsigned so that faces wound inwards give the same answer as faces wound
        /// outwards; a shell that does not close gives a number with no meaning either way.
        /// </remarks>
        public double Volume => Math.Abs(GetSignedVolume());

        /// <summary>
        /// Gets the volume left after subtracting every opening, reading each opening whole.
        /// </summary>
        /// <remarks>
        /// This is a sum, not a subtraction of shapes: the volume of every opening is taken off in full,
        /// whether or not all of it lies in the body. That makes it cheap and exact for an opening that
        /// sits inside the body, and an overestimate of what has been removed for one that pokes out —
        /// which is how a through-hole is usually drawn, deliberately overshooting so that it clears the
        /// far face. Two openings that overlap each other are counted twice for the same reason.
        /// <para>
        /// Use <see cref="GetNetVolume()"/> where either of those can happen. It cuts the openings out of
        /// the body properly and measures what is left, at the cost of doing the cutting.
        /// </para>
        /// </remarks>
        public double NetVolume
        {
            get
            {
                double volume = Volume;

                foreach (GeoSolid3 opening in _openings)
                {
                    volume -= opening.Volume;
                }

                return Math.Max(0.0, volume);
            }
        }

        /// <summary>
        /// Gets the volume of the material actually left once every opening is cut out, using the default
        /// tolerance.
        /// </summary>
        public double GetNetVolume() => GetNetVolume(Tolerance.Global);

        /// <summary>
        /// Gets the volume of the material actually left once every opening is cut out, within a
        /// tolerance.
        /// </summary>
        /// <param name="tolerance">The tolerance the cutting is carried out with.</param>
        /// <returns>The volume of the body with its openings removed, or zero when nothing is left.</returns>
        /// <remarks>
        /// Unlike <see cref="NetVolume"/> this removes the openings as shapes rather than as numbers, so
        /// the part of an opening reaching outside the body costs nothing and two openings overlapping
        /// each other are not counted twice. The openings are taken out one after another, so each is
        /// measured against what the ones before it left.
        /// <para>
        /// The cutting is a full boolean subtraction per opening, which is why this is a method and
        /// <see cref="NetVolume"/> is a property. An opening that cannot be cut out is taken whole, which
        /// falls back to what <see cref="NetVolume"/> would have said for it rather than abandoning the
        /// measurement.
        /// </para>
        /// </remarks>
        public double GetNetVolume(Tolerance tolerance)
        {
            if (_openings.Length == 0)
            {
                return Volume;
            }

            // The gross body: the openings are what is being cut out, so they must not also be carried
            // along as openings of the thing being cut.
            GeoSolid3 remaining = new GeoSolid3(_faces);
            double deducted = 0.0;

            foreach (GeoSolid3 opening in _openings)
            {
                if (Boolean3.TrySubtract(remaining, opening, out GeoSolid3 cut, tolerance))
                {
                    remaining = cut;
                    continue;
                }

                // The subtraction gave nothing back: either the opening swallowed what was left of the
                // body, or the cut could not be resolved. Taking the opening off whole covers both, since
                // a body swallowed by its own opening lands on zero once the clamp below is applied.
                deducted += opening.Volume;
            }

            return Math.Max(0.0, remaining.Volume - deducted);
        }

        /// <summary>
        /// Gets the signed volume enclosed by the bounding faces. It is positive when the face normals
        /// point outwards and negative when they point inwards.
        /// </summary>
        public double GetSignedVolume()
        {
            GeoPoint3 apex = GetReferencePoint();
            double total = 0.0;

            foreach (GeoFace3 face in _faces)
            {
                total += SumTetrahedra(face.Triangulate(), apex);

                // A hole in a face is material that is not there, so its contribution is removed. The
                // holes are wound the same way as the boundary, so subtracting is what reverses them.
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    total -= SumTetrahedra(hole.Triangulate(), apex);
                }
            }

            return total;
        }

        /// <summary>
        /// Gets a point on the body for the tetrahedra to be measured from.
        /// </summary>
        /// <remarks>
        /// The sum works out the same wherever the apex is put, because the parts of each tetrahedron
        /// reaching outside the body cancel. That is true of the arithmetic, not of the floating point:
        /// a body a kilometre from the origin measured against the origin builds tetrahedra of about
        /// 1e18 that have to cancel down to a volume of about 100, and around three per cent of the
        /// answer is lost in the cancelling. Measuring from a corner of the body instead keeps every
        /// tetrahedron the size of the body itself, and there is nothing left to cancel away.
        /// </remarks>
        private GeoPoint3 GetReferencePoint() => _faces[0].Boundary[0];

        /// <summary>
        /// Sums the signed volumes of the tetrahedra spanning a given apex and each triangle.
        /// </summary>
        private static double SumTetrahedra(GeoTriangle3[] triangles, GeoPoint3 apex)
        {
            double total = 0.0;

            foreach (GeoTriangle3 triangle in triangles)
            {
                GeoVector3 a = apex.GetVectorTo(triangle.A);
                GeoVector3 b = apex.GetVectorTo(triangle.B);
                GeoVector3 c = apex.GetVectorTo(triangle.C);

                total += a.TripleProduct(b, c) / 6.0;
            }

            return total;
        }

        /// <summary>
        /// Gets the centroid of the solid, ignoring the openings.
        /// </summary>
        /// <remarks>
        /// Each surface triangle is taken with the world origin as the apex of a tetrahedron, exactly as
        /// <see cref="GetSignedVolume"/> does, and the centroids of those tetrahedra are averaged by
        /// signed volume. The parts reaching outside the body carry negative volume and cancel, so the
        /// answer does not depend on where the origin is. A body with no volume has no centroid to
        /// average, and the centroid of its vertices comes back instead.
        /// </remarks>
        public GeoPoint3 Centroid
        {
            get
            {
                // Measured from a corner of the body rather than from the world origin, for the reason
                // GetReferencePoint gives: far from the origin the sum otherwise cancels away its own
                // accuracy. The offset is added back at the end.
                GeoPoint3 apex = GetReferencePoint();

                double totalVolume = 0.0;
                double x = 0.0;
                double y = 0.0;
                double z = 0.0;

                foreach (GeoFace3 face in _faces)
                {
                    foreach (GeoTriangle3 triangle in face.Triangulate())
                    {
                        GeoVector3 a = apex.GetVectorTo(triangle.A);
                        GeoVector3 b = apex.GetVectorTo(triangle.B);
                        GeoVector3 c = apex.GetVectorTo(triangle.C);

                        double volume = a.TripleProduct(b, c) / 6.0;

                        // The fourth vertex of each tetrahedron is the apex, which sits at the origin of
                        // these vectors, so it adds nothing and the centroid is a quarter of the way
                        // along the other three.
                        totalVolume += volume;
                        x += volume * (a.X + b.X + c.X) * 0.25;
                        y += volume * (a.Y + b.Y + c.Y) * 0.25;
                        z += volume * (a.Z + b.Z + c.Z) * 0.25;
                    }
                }

                // The total is a volume, so it is judged against a length tolerance cubed. Against
                // double.Epsilon only an exactly zero total would fall back, and a shell that nearly
                // closes on itself can leave one small enough to overflow the division yet far above
                // that.
                double point = Tolerance.Global.EqualPoint;
                double volumeEpsilon = point * point * point;

                if (Math.Abs(totalVolume) <= volumeEpsilon)
                {
                    return GetVertexAverage();
                }

                return apex.Add(new GeoVector3(x / totalVolume, y / totalVolume, z / totalVolume));
            }
        }

        /// <summary>
        /// Gets the plain average of every boundary vertex, the fallback for a body with no volume.
        /// </summary>
        private GeoPoint3 GetVertexAverage()
        {
            double x = 0.0;
            double y = 0.0;
            double z = 0.0;
            int count = 0;

            foreach (GeoFace3 face in _faces)
            {
                foreach (GeoPoint3 vertex in face.Boundary.Vertices)
                {
                    x += vertex.X;
                    y += vertex.Y;
                    z += vertex.Z;
                    count++;
                }
            }

            return count == 0 ? GeoPoint3.Origin : new GeoPoint3(x / count, y / count, z / count);
        }

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this solid.
        /// </summary>
        public GeoAabb3 GetAabb()
        {
            GeoAabb3 box = GeoAabb3.Empty;

            foreach (GeoFace3 face in _faces)
            {
                box = box.Union(face.GetAabb());
            }

            return box;
        }

        /// <summary>
        /// Breaks every face into triangles, giving the surface of the solid as a triangle mesh, using
        /// the default tolerance.
        /// </summary>
        public GeoTriangle3[] Triangulate() => Triangulate(Tolerance.Global);

        /// <summary>
        /// Breaks every face into triangles, giving the surface of the solid as a triangle mesh, within a
        /// tolerance.
        /// </summary>
        /// <param name="tolerance">The tolerance deciding what counts as a degenerate triangle.</param>
        /// <remarks>
        /// Every triangle lies within the material of the face it came from, so the mesh describes the
        /// surface and nothing more: a concave face is followed rather than spanned, and a hole is left
        /// open. That is what lets clash detection, ray casting and body-to-body distance read this mesh
        /// as the boundary of the solid.
        /// <para>
        /// The openings are not meshed. They are whole bodies subtracted from this one rather than part
        /// of its surface, and each carries its own faces to mesh if that is what is wanted.
        /// </para>
        /// </remarks>
        public GeoTriangle3[] Triangulate(Tolerance tolerance)
        {
            List<GeoTriangle3> triangles = new List<GeoTriangle3>();

            foreach (GeoFace3 face in _faces)
            {
                triangles.AddRange(face.TriangulateSurface(tolerance));
            }

            return triangles.ToArray();
        }

        #endregion

        #region Queries

        /// <summary>
        /// Checks whether the boundary closes without gaps, using the default tolerance.
        /// </summary>
        public bool IsClosed() => IsClosed(Tolerance.Global);

        /// <summary>
        /// Checks whether the boundary closes without gaps, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A closed surface has every boundary edge shared by exactly two faces. One face on an edge means
        /// an open rim; three or more means the surface branches, which is not a solid boundary either.
        /// Hole rims count as edges like any other, since in a closed body they are shared with the surface
        /// lining the cavity behind them. Vertices are matched through a spatial grid rather than by
        /// scanning what has been seen so far, which keeps the cost linear in the number of vertices
        /// instead of quadratic.
        /// </remarks>
        public bool IsClosed(Tolerance tolerance)
        {
            VertexWelder welder = new VertexWelder(tolerance);
            Dictionary<long, int> edgeCounts = new Dictionary<long, int>();

            foreach (GeoFace3 face in _faces)
            {
                CountRingEdges(face.Boundary, welder, edgeCounts);

                // The rim of a hole is boundary too, and in a closed body it is shared with whatever
                // surface lines the cavity: the wall of a shaft through a plate meets the plate along the
                // rims at each end. Leaving the rims out would report every hollow body as open.
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    CountRingEdges(hole, welder, edgeCounts);
                }
            }

            if (edgeCounts.Count == 0)
            {
                return false;
            }

            foreach (int count in edgeCounts.Values)
            {
                if (count != 2)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tallies how many times each edge of one ring is used, matching vertices within a tolerance.
        /// </summary>
        private static void CountRingEdges(GeoPolygon3 ring, VertexWelder welder, Dictionary<long, int> edgeCounts)
        {
            for (int i = 0; i < ring.EdgeCount; i++)
            {
                GeoLine3 edge = ring.GetEdgeAt(i);

                int from = welder.GetIndex(edge.StartPoint);
                int to = welder.GetIndex(edge.EndPoint);

                if (from == to)
                {
                    continue;
                }

                // The key ignores direction, because the two faces meeting on an edge traverse it in
                // opposite directions when they are wound consistently.
                long key = from < to ? ((long)from << 32) | (uint)to : ((long)to << 32) | (uint)from;

                edgeCounts.TryGetValue(key, out int count);
                edgeCounts[key] = count + 1;
            }
        }

        /// <summary>
        /// Locates a point relative to this solid, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Containment3.Locate(this, point);

        /// <summary>
        /// Locates a point relative to this solid, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => Containment3.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this solid holds a point, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Containment3.Contains(this, point);

        /// <summary>
        /// Checks whether this solid holds a point, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Containment3.Contains(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to a point. A point inside is at distance zero.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Applies a transformation to every face and opening.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoSolid3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoFace3[] faces = new GeoFace3[_faces.Length];
            for (int i = 0; i < _faces.Length; i++)
            {
                faces[i] = _faces[i].TransformBy(transform);
            }

            GeoSolid3[] openings = new GeoSolid3[_openings.Length];
            for (int i = 0; i < _openings.Length; i++)
            {
                openings[i] = _openings[i].TransformBy(transform);
            }

            return new GeoSolid3(faces, openings);
        }

        /// <summary>
        /// Splits this solid by a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The body may be concave and its faces may carry holes. Openings are carried into whichever
        /// half they fall in, and one straddling the plane is cut along with the body.
        /// </remarks>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoSolid3 above, out GeoSolid3 below) => Splition3.TrySplitBy(this, cutter, out above, out below);

        /// <summary>
        /// Splits this solid by a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The body may be concave and its faces may carry holes. Openings are carried into whichever
        /// half they fall in, and one straddling the plane is cut along with the body.
        /// </remarks>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoSolid3 above, out GeoSolid3 below, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out above, out below, tolerance);

        /// <summary>
        /// Joins this solid with another, using the default tolerance.
        /// </summary>
        public bool TryUnion(GeoSolid3 other, out GeoSolid3 result) => Boolean3.TryUnion(this, other, out result);

        /// <summary>
        /// Joins this solid with another, within a tolerance.
        /// </summary>
        public bool TryUnion(GeoSolid3 other, out GeoSolid3 result, Tolerance tolerance) => Boolean3.TryUnion(this, other, out result, tolerance);

        /// <summary>
        /// Gets the part this solid shares with another, using the default tolerance.
        /// </summary>
        public bool TryIntersect(GeoSolid3 other, out GeoSolid3 result) => Boolean3.TryIntersect(this, other, out result);

        /// <summary>
        /// Gets the part this solid shares with another, within a tolerance.
        /// </summary>
        public bool TryIntersect(GeoSolid3 other, out GeoSolid3 result, Tolerance tolerance) => Boolean3.TryIntersect(this, other, out result, tolerance);

        /// <summary>
        /// Takes another solid out of this one, using the default tolerance.
        /// </summary>
        public bool TrySubtract(GeoSolid3 tool, out GeoSolid3 result) => Boolean3.TrySubtract(this, tool, out result);

        /// <summary>
        /// Takes another solid out of this one, within a tolerance.
        /// </summary>
        public bool TrySubtract(GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance) => Boolean3.TrySubtract(this, tool, out result, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another solid has exactly the same faces and openings, in the same order.
        /// </summary>
        public bool Equals(GeoSolid3 other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_faces.Length != other._faces.Length || _openings.Length != other._openings.Length)
            {
                return false;
            }

            for (int i = 0; i < _faces.Length; i++)
            {
                if (!_faces[i].Equals(other._faces[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < _openings.Length; i++)
            {
                if (!_openings[i].Equals(other._openings[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current solid.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoSolid3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                foreach (GeoFace3 face in _faces)
                {
                    hash = hash * 31 + face.GetHashCode();
                }

                foreach (GeoSolid3 opening in _openings)
                {
                    hash = hash * 31 + opening.GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Compares whether this solid equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoSolid3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this solid equals another within a tolerance, ignoring the order the faces are
        /// listed in.
        /// </summary>
        /// <remarks>
        /// The order of faces on a solid carries no meaning — the same box built starting from a different
        /// face is the same box — so the faces are matched up rather than compared position by position.
        /// The matching is greedy and quadratic, which is fine for the face counts a flat-faced solid has.
        /// </remarks>
        public bool IsEqualTo(GeoSolid3 other, Tolerance tolerance)
        {
            if (other is null || _faces.Length != other._faces.Length || _openings.Length != other._openings.Length)
            {
                return false;
            }

            bool[] matchedFaces = new bool[_faces.Length];

            foreach (GeoFace3 face in _faces)
            {
                bool found = false;

                for (int i = 0; i < other._faces.Length; i++)
                {
                    if (!matchedFaces[i] && face.IsEqualTo(other._faces[i], tolerance))
                    {
                        matchedFaces[i] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            bool[] matchedOpenings = new bool[_openings.Length];

            foreach (GeoSolid3 opening in _openings)
            {
                bool found = false;

                for (int i = 0; i < other._openings.Length; i++)
                {
                    if (!matchedOpenings[i] && opening.IsEqualTo(other._openings[i], tolerance))
                    {
                        matchedOpenings[i] = true;
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
        /// Returns a string that represents the current solid.
        /// </summary>
        public override string ToString()
        {
            double volume = Volume;
            double net = volume;

            foreach (GeoSolid3 opening in _openings)
            {
                net -= opening.Volume;
            }

            return $"Solid3(Faces: {_faces.Length}, Volume: {volume:0.###}, NetVolume: {Math.Max(0.0, net):0.###})";
        }
    }
}
