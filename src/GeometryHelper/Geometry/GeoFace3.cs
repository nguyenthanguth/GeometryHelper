using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a flat face in 3D space: an outer boundary polygon with optional holes cut out of it.
    /// <para>
    /// A face is what a polygon becomes once it needs to carry openings. A plate with bolt holes, a wall
    /// with a window: the outer loop says where the material ends and each inner loop says where it is
    /// missing. Every loop shares one plane, which is checked at construction.
    /// </para>
    /// </summary>
    public sealed partial class GeoFace3 : IEquatable<GeoFace3>
    {
        private readonly GeoPolygon3[] _holes;

        /// <summary>
        /// Gets the outer boundary of the face.
        /// </summary>
        public GeoPolygon3 Boundary { get; }

        /// <summary>
        /// Gets the read-only list of holes cut out of the face, each wound the same way as the boundary.
        /// </summary>
        /// <remarks>
        /// A hole given the other way round is turned at construction. The library holds holes that way so that
        /// area and volume come out by plain subtraction; the plane, and the booleans and offsets done in it, wind
        /// a hole against its boundary, and a face lifted from them kept that winding, so a body built of such
        /// faces added its holes to its volume instead of taking them away.
        /// </remarks>
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

                    kept.Add(hole.Normal.DotProduct(boundary.Normal) < 0.0 ? hole.Flip() : hole);
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
        /// Makes the faces that cover a boundary less its holes, from loops of corners that need not lie flat, using the
        /// default tolerance.
        /// </summary>
        /// <param name="boundary">The corners of the outer loop, in order.</param>
        /// <param name="holes">The corners of each hole, in order and wound either way; null is read as none.</param>
        /// <returns>The faces; see <see cref="FromLoops(IEnumerable{GeoPoint3}, IEnumerable{IEnumerable{GeoPoint3}}, Tolerance)"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="boundary"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a hole is null.</exception>
        public static GeoFace3[] FromLoops(IEnumerable<GeoPoint3> boundary, IEnumerable<IEnumerable<GeoPoint3>> holes)
            => FromLoops(boundary, holes, Tolerance.Global);

        /// <summary>
        /// Makes the faces that cover a boundary less its holes, from loops of corners that need not lie flat, within a
        /// tolerance.
        /// </summary>
        /// <param name="boundary">The corners of the outer loop, in order.</param>
        /// <param name="holes">The corners of each hole, in order and wound either way; null is read as none.</param>
        /// <param name="tolerance">The tolerance deciding which corners are one, which loops enclose an area, and which lie flat.</param>
        /// <returns>
        /// One face, holes and all, as the constructors would make it, when every loop lies flat on the plane of the
        /// boundary within the planar tolerance. Otherwise triangles on the corners themselves, wound as the boundary is,
        /// covering the boundary less its holes. None when the boundary encloses no area, or when, seen along its normal,
        /// it crosses itself or a hole reaches out of it.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="boundary"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a hole is null.</exception>
        /// <remarks>
        /// This is the way to read the faces of a body a modeller gives as loops of corners. A face can come a little out
        /// of flat, as a face Tekla Structures cuts can, and a polygon refuses it; left out, it is a hole in the body. Split
        /// into triangles on the corners it came with, each exactly flat, it keeps every edge it shares with its neighbours,
        /// so the body is as closed as it was given, and no corner moves. A hole enclosing no area is left out.
        /// </remarks>
        public static GeoFace3[] FromLoops(IEnumerable<GeoPoint3> boundary, IEnumerable<IEnumerable<GeoPoint3>> holes, Tolerance tolerance)
            => Loops3.ToFaces(boundary, holes, tolerance);

        /// <summary>
        /// Creates a copy of this face.
        /// </summary>
        /// <remarks>
        /// The copy is not checked again: its holes lie on the plane of its boundary within the tolerance the face was
        /// made with, which need not be the global one.
        /// </remarks>
        public GeoFace3 Clone()
        {
            GeoPolygon3[] copies = new GeoPolygon3[_holes.Length];

            for (int i = 0; i < _holes.Length; i++)
            {
                copies[i] = _holes[i].Clone();
            }

            return new GeoFace3(Boundary.Clone(), copies, Area);
        }

        /// <summary>
        /// Gets the plane carrying the face, oriented along its normal.
        /// </summary>
        public GeoPlane3 GetPlane() => Boundary.GetPlane();

        /// <summary>
        /// Gets the face with its orientation reversed, holes included.
        /// </summary>
        /// <remarks>
        /// Turning a face over changes nothing that was checked when it was made, so nothing is checked again: within
        /// the global tolerance, a face made within a wider one could otherwise not be turned over.
        /// </remarks>
        public GeoFace3 Flip()
        {
            GeoPolygon3[] flipped = new GeoPolygon3[_holes.Length];

            for (int i = 0; i < _holes.Length; i++)
            {
                flipped[i] = _holes[i].Flip();
            }

            return new GeoFace3(Boundary.Flip(), flipped, Area);
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
        /// <para>
        /// The triangles a fan produces do not all lie inside a concave face, so this is not a surface
        /// mesh. Use <see cref="TriangulateSurface()"/> for anything that reads the triangles as material.
        /// </para>
        /// </remarks>
        public GeoTriangle3[] Triangulate() => Boundary.Triangulate();

        /// <summary>
        /// Breaks the face into triangles that each lie within its material, holes included, using the
        /// default tolerance.
        /// </summary>
        public GeoTriangle3[] TriangulateSurface() => TriangulateSurface(Tolerance.Global);

        /// <summary>
        /// Breaks the face into triangles that each lie within its material, holes included, within a
        /// tolerance.
        /// </summary>
        /// <param name="tolerance">The tolerance deciding what counts as a degenerate triangle.</param>
        /// <returns>The triangles covering the face, wound to share its normal.</returns>
        /// <remarks>
        /// This is the triangulation to use wherever the mesh stands for the surface itself — ray casting,
        /// clash detection, distance to a body. Unlike <see cref="Triangulate()"/> every triangle lies
        /// inside the face, a hole is left empty rather than covered over, and a concave boundary is
        /// followed rather than spanned.
        /// <para>
        /// The triangles are clipped from the face's own corners while its rings stand apart. A face whose
        /// rings come within the point tolerance of each other (holes sharing an edge, a hole against the
        /// boundary) or cross, or whose loop the clipping cannot reduce, is cut into strips at its corners
        /// instead, its material read as the booleans read it: holes that touch or overlap are taken
        /// together, and one reaching past the boundary takes away only what it covers. Those triangles
        /// still lie within the material and leave every hole open, but they meet the face's edges at
        /// points of their own as well as at its corners. A face with nothing left of it gives none.
        /// </para>
        /// <para>
        /// Such a face used to be handed back as the fan of its boundary, laid across every hole: a slab
        /// whose pits the clipping could not get round was meshed as 5 391 m2 for its 1 836.
        /// </para>
        /// </remarks>
        public GeoTriangle3[] TriangulateSurface(Tolerance tolerance)
        {
            return EarClipping.TryTriangulateSurface(this, tolerance, out GeoTriangle3[] triangles)
                ? triangles
                : StripTriangulation.Triangulate(this, tolerance);
        }

        /// <summary>
        /// Breaks the face into faces of a kind, its grid level, using the default tolerance. A grid needs the size of its
        /// cells, which <see cref="ToMesh(Meshing.MeshOptions)"/> takes.
        /// </summary>
        /// <param name="kind">The kind of faces.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        /// <exception cref="ArgumentException">Thrown for <see cref="Meshing.MeshKind.Grid"/>, which needs the size of its cells.</exception>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshKind kind) => Meshing.Mesh3.ToMesh(this, Meshing.Mesh2.OptionsFor(kind), Meshing.MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks the face into faces as the options say, its grid level, with the second axis up the slope, using the
        /// default tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options) => Meshing.Mesh3.ToMesh(this, options, Meshing.MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks the face into faces as the options say, its grid level, with the second axis up the slope, within a
        /// tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options, Tolerance tolerance) => Meshing.Mesh3.ToMesh(this, options, Meshing.MeshPlacement3.World, tolerance);

        /// <summary>
        /// Breaks the face into faces as the options say, its grid standing as the placement says, using the default
        /// tolerance.
        /// </summary>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the plane, and where a cell starts.</param>
        /// <returns>The mesh; one with no faces when there is no area.</returns>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options, Meshing.MeshPlacement3 placement) => Meshing.Mesh3.ToMesh(this, options, placement, Tolerance.Global);

        /// <summary>
        /// Breaks the face into faces as the options say, its grid standing as the placement says, within a tolerance:
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
        /// The holes are left open. A face a hair out of flat keeps its corners where they are, and the points put on its
        /// sides stay on them; only the points inside lie on the plane.
        /// </remarks>
        public Meshing.GeoMesh3 ToMesh(Meshing.MeshOptions options, Meshing.MeshPlacement3 placement, Tolerance tolerance) => Meshing.Mesh3.ToMesh(this, options, placement, tolerance);

        /// <summary>
        /// Moves the face by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The face in its new place.</returns>
        public GeoFace3 Translate(GeoVector3 vector) => TransformBy(GeoTransform3.Translation(vector));

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

            // A move that keeps every length keeps the holes on the boundary's plane as they were; asked again, one at
            // the edge of the tolerance could land past it. See GeoPolygon3.TransformBy.
            return transform.KeepsLengths()
                ? new GeoFace3(Boundary.TransformBy(transform), moved, Area)
                : new GeoFace3(Boundary.TransformBy(transform), moved);
        }

        /// <summary>
        /// Initializes a face from a boundary and holes already known to lie in one plane and to be wound alike, and the
        /// area they leave.
        /// </summary>
        private GeoFace3(GeoPolygon3 boundary, GeoPolygon3[] holes, double area)
        {
            Boundary = boundary;
            _holes = holes;
            Area = area;
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
        /// Splits this face by a plane, using the default tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoFace3[] above, out GeoFace3[] below) => Splition3.TrySplitBy(this, cutter, out above, out below);

        /// <summary>
        /// Splits this face by a plane, within a tolerance.
        /// </summary>
        public bool TrySplitBy(GeoPlane3 cutter, out GeoFace3[] above, out GeoFace3[] below, Tolerance tolerance) => Splition3.TrySplitBy(this, cutter, out above, out below, tolerance);

        /// <summary>
        /// Grows this face within its own plane by a distance, or shrinks it when the distance is negative, with
        /// sharp corners, using the default tolerance. The holes shrink as the boundary grows. See
        /// <see cref="Offset3.Offset(GeoFace3, double, OffsetOptions, Tolerance)"/>.
        /// </summary>
        public GeoFace3[] Offset(double distance) => Offset3.Offset(this, distance);

        /// <summary>
        /// Grows this face within its own plane by a distance, or shrinks it when the distance is negative, with
        /// sharp corners, within a tolerance.
        /// </summary>
        public GeoFace3[] Offset(double distance, Tolerance tolerance) => Offset3.Offset(this, distance, tolerance);

        /// <summary>
        /// Grows this face within its own plane by a distance, or shrinks it when the distance is negative, with
        /// the given corners, using the default tolerance.
        /// </summary>
        public GeoFace3[] Offset(double distance, OffsetJoin join) => Offset3.Offset(this, distance, join);

        /// <summary>
        /// Grows this face within its own plane by a distance, or shrinks it when the distance is negative, with
        /// the given corners, within a tolerance.
        /// </summary>
        public GeoFace3[] Offset(double distance, OffsetJoin join, Tolerance tolerance) => Offset3.Offset(this, distance, join, tolerance);

        /// <summary>
        /// Grows this face within its own plane by a distance, or shrinks it when the distance is negative, as the
        /// options say, using the default tolerance.
        /// </summary>
        public GeoFace3[] Offset(double distance, OffsetOptions options) => Offset3.Offset(this, distance, options);

        /// <summary>
        /// Grows this face within its own plane by a distance, or shrinks it when the distance is negative, as the
        /// options say, within a tolerance.
        /// </summary>
        public GeoFace3[] Offset(double distance, OffsetOptions options, Tolerance tolerance) => Offset3.Offset(this, distance, options, tolerance);

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

        /// <summary>
        /// Gets the frame of this face: the frame of its boundary, so its holes are laid out with it.
        /// </summary>
        /// <returns>The frame, with its Z axis along the face normal.</returns>
        public GeoCoordinateSystem3 GetFrame() => PlanarMap.FrameOf(this);

        /// <summary>
        /// Lays this face and its holes out in a frame, dropping each vertex's distance from the plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The face in the plane of the frame, holes and all.</returns>
        public GeoFace2 ProjectToFace2(GeoCoordinateSystem3 frame) => PlanarMap.ProjectToFace2(frame, this);

        /// <summary>
        /// Gets the centroid of the face: the centroid of its boundary with its holes taken out, so a plate
        /// balances at this point whatever has been cut from it.
        /// </summary>
        /// <remarks>
        /// A face whose holes cancel its boundary has no centroid to give, and its boundary centroid is
        /// returned instead rather than a division by zero.
        /// </remarks>
        public GeoPoint3 Centroid
        {
            get
            {
                double area = Boundary.Area;
                GeoPoint3 boundaryCentroid = Boundary.Centroid;
                double x = boundaryCentroid.X * area;
                double y = boundaryCentroid.Y * area;
                double z = boundaryCentroid.Z * area;

                foreach (GeoPolygon3 hole in Holes)
                {
                    double holeArea = hole.Area;
                    GeoPoint3 holeCentroid = hole.Centroid;
                    area -= holeArea;
                    x -= holeCentroid.X * holeArea;
                    y -= holeCentroid.Y * holeArea;
                    z -= holeCentroid.Z * holeArea;
                }

                if (Math.Abs(area) <= Tolerance.Global.EqualPoint * Tolerance.Global.EqualPoint)
                {
                    return boundaryCentroid;
                }

                return new GeoPoint3(x / area, y / area, z / area);
            }
        }
        /// <summary>
        /// Builds a spatial index over the surface of this face, for asking it many questions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An index is worth building when the <b>same</b> shape is asked <b>many</b> questions. Building it
        /// costs a sort of the triangles, so it pays for itself over repeated queries and never on the first
        /// one; below a few dozen triangles the plain walk wins outright. Every shape here is immutable, so a
        /// tree stays valid for as long as the shape exists — build it once, keep it, throw it away with the
        /// shape.
        /// </para>
        /// <para>
        /// It is <c>Build</c> and not <c>Get</c> because it does work. Calling it inside a loop is slower than
        /// not having it at all, which is exactly the mistake the name is there to prevent.
        /// </para>
        /// <para>
        /// <b>The index is over triangles and the body is not.</b> <c>GetIntersections</c> on the tree reports
        /// one hit per triangle, so a ray landing on the diagonal two triangles share is named twice, while the
        /// body names each place once. Ask the body where you want places; ask the tree where you want speed
        /// and can keep clear of the edges — which is what <c>Containment3</c> does by throwing its ray again
        /// in another direction when a hit lands near one.
        /// </para>
        /// </remarks>
        /// <returns>The hierarchy; it is a snapshot and holds no reference back to this shape.</returns>
        public Spatial.GeoBvh3 BuildIndex() => BuildIndex(Tolerance.Global);

        /// <summary>
        /// Builds a spatial index over the surface of this face, within a tolerance.
        /// </summary>
        /// <param name="tolerance">The tolerance the triangulation works to.</param>
        /// <returns>The hierarchy; it is a snapshot and holds no reference back to this shape.</returns>
        public Spatial.GeoBvh3 BuildIndex(Tolerance tolerance) => Spatial.GeoBvh3.FromFace(this, tolerance);

    }
}
