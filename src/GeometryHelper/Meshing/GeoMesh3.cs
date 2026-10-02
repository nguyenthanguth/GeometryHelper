using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// A flat shape of space broken into faces that share their corners: triangles, the cells of a grid, strips or convex
    /// pieces, as <see cref="MeshKind"/> says.
    /// <para>
    /// The mesh is a <see cref="GeoMesh2"/> standing in a plane of space. Every face is a simple polygon with no hole,
    /// running counter-clockwise seen from the side <see cref="Normal"/> points to, given by the indexes of its corners in
    /// <see cref="Vertices"/>; the faces lie within the shape's material, cover it without overlapping, holes left open, and
    /// meet edge to edge. The shape's own corners are vertices exactly where the shape has them. <see cref="Frame"/> is the
    /// plane it was laid out in, with its X axis along the grid's first axis, and <see cref="ToMesh2"/> gives the faces
    /// drawn in it, as an elevation of a wall is drawn. The mesh is immutable.
    /// </para>
    /// </summary>
    public sealed class GeoMesh3
    {
        private readonly GeoMesh2 _flat;
        private readonly GeoPoint3[] _vertices;
        private Dictionary<GeoPoint2, int> _byPlace;

        /// <summary>
        /// Initializes a mesh from its faces laid out in a frame and the places of its vertices in space, taken as they are.
        /// </summary>
        /// <param name="flat">The faces laid out in the frame; its vertices are where those of space stand in it.</param>
        /// <param name="vertices">Where each vertex of the flat mesh stands in space, in the same order.</param>
        /// <param name="frame">The frame the faces were laid out in.</param>
        internal GeoMesh3(GeoMesh2 flat, GeoPoint3[] vertices, GeoCoordinateSystem3 frame)
        {
            _flat = flat;
            _vertices = vertices;
            Frame = frame;

            double area = 0.0;

            foreach (int[] face in flat.FaceArray)
            {
                area += AreaOf(face);
            }

            Area = area;
        }

        /// <summary>
        /// Gets the kind of faces the mesh was made of.
        /// </summary>
        public MeshKind Kind => _flat.Kind;

        /// <summary>
        /// Gets the plane the faces were laid out in: its Z axis is <see cref="Normal"/>, its X axis the grid's first axis,
        /// or the way strips run, and its origin a corner of the shape.
        /// </summary>
        public GeoCoordinateSystem3 Frame { get; }

        /// <summary>
        /// Gets the normal of the shape, the side the faces are seen counter-clockwise from.
        /// </summary>
        public GeoVector3 Normal => Frame.ZAxis;

        /// <summary>
        /// Gets the places the faces share, each once.
        /// </summary>
        public IReadOnlyList<GeoPoint3> Vertices => _vertices;

        /// <summary>
        /// Gets how many places the faces share.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets how many faces there are.
        /// </summary>
        public int FaceCount => _flat.FaceCount;

        /// <summary>
        /// Gets the area the faces cover together: the shape's material, less the joints of a grid.
        /// </summary>
        public double Area { get; }

        /// <summary>
        /// Gets the indexes in <see cref="Vertices"/> of the corners of a face, counter-clockwise.
        /// </summary>
        /// <param name="index">The face.</param>
        /// <returns>A copy of the indexes, which changing does not change the mesh.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such face.</exception>
        public int[] GetFaceIndices(int index) => _flat.GetFaceIndices(index);

        /// <summary>
        /// Gets a face as a polygon of space, counter-clockwise about the normal, with every corner it has, three in a row
        /// among them.
        /// </summary>
        /// <param name="index">The face.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such face.</exception>
        public GeoPolygon3 GetFace(int index)
        {
            Check(index);
            return ToPolygon(_flat.FaceArray[index]);
        }

        /// <summary>
        /// Gets every face as a polygon of space, counter-clockwise about the normal, in the order of their indexes.
        /// </summary>
        public GeoPolygon3[] GetFaces()
        {
            int[][] faces = _flat.FaceArray;
            var polygons = new GeoPolygon3[faces.Length];

            for (int i = 0; i < polygons.Length; i++)
            {
                polygons[i] = ToPolygon(faces[i]);
            }

            return polygons;
        }

        /// <summary>
        /// Gets whether a face is a whole cell of a grid, which neither the boundary nor a hole cuts by more than the point
        /// tolerance.
        /// </summary>
        /// <param name="index">The face.</param>
        /// <returns>true for a whole cell; false for a cut one, and for every face of a mesh that is not a grid.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such face.</exception>
        public bool IsWhole(int index) => _flat.IsWhole(index);

        /// <summary>
        /// Gets every edge of the mesh once, whether one face has it or two.
        /// </summary>
        /// <returns>The edges, in the order the faces first come to them, each running as its first face runs.</returns>
        public GeoLine3[] GetEdges()
        {
            var seen = new HashSet<(int, int)>();
            var edges = new List<GeoLine3>();

            foreach (int[] face in _flat.FaceArray)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Length];

                    if (seen.Add(a < b ? (a, b) : (b, a)))
                    {
                        edges.Add(new GeoLine3(_vertices[a], _vertices[b]));
                    }
                }
            }

            return edges.ToArray();
        }

        /// <summary>
        /// Gets the edges only one face has: the shape's boundary and the rims of its holes, and for a grid with joints the
        /// outline of every cell.
        /// </summary>
        /// <returns>The edges, each running as its face runs, so that the face lies on its left seen from the normal.</returns>
        public GeoLine3[] GetBoundaryEdges()
        {
            var directed = new HashSet<(int, int)>();

            foreach (int[] face in _flat.FaceArray)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    directed.Add((face[i], face[(i + 1) % face.Length]));
                }
            }

            var edges = new List<GeoLine3>();

            foreach (int[] face in _flat.FaceArray)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Length];

                    if (!directed.Contains((b, a)))
                    {
                        edges.Add(new GeoLine3(_vertices[a], _vertices[b]));
                    }
                }
            }

            return edges.ToArray();
        }

        /// <summary>
        /// Gets the faces that share an edge with a face.
        /// </summary>
        /// <param name="index">The face.</param>
        /// <returns>The neighbours, in ascending order; none for a face of a grid with joints, which touches no other.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such face.</exception>
        public int[] GetAdjacentFaces(int index) => _flat.GetAdjacentFaces(index);

        /// <summary>
        /// Breaks every face into triangles on its own corners, counter-clockwise about the normal.
        /// </summary>
        /// <returns>
        /// The triangles, face by face, covering the faces exactly. Every corner of a face is a corner of one of them, three
        /// in a row included, so the triangles of two faces side by side meet edge to edge as the faces do.
        /// </returns>
        public GeoTriangle3[] ToTriangles()
        {
            GeoTriangle2[] flat = _flat.ToTriangles();
            var triangles = new GeoTriangle3[flat.Length];

            for (int i = 0; i < flat.Length; i++)
            {
                triangles[i] = new GeoTriangle3(InSpace(flat[i].A), InSpace(flat[i].B), InSpace(flat[i].C));
            }

            return triangles;
        }

        /// <summary>
        /// Whether a face turns right at none of its corners, so that a fan from any corner covers it.
        /// </summary>
        internal bool IsConvex(int index) => _flat.IsConvex(_flat.FaceArray[index]);

        /// <summary>
        /// The triangles of one face, as <see cref="ToTriangles"/> gives them.
        /// </summary>
        internal GeoTriangle3[] GetFaceTriangles(int index)
        {
            var flat = new List<GeoTriangle2>();
            _flat.AddTriangles(_flat.FaceArray[index], flat);
            var triangles = new GeoTriangle3[flat.Count];

            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new GeoTriangle3(InSpace(flat[i].A), InSpace(flat[i].B), InSpace(flat[i].C));
            }

            return triangles;
        }

        /// <summary>
        /// Gets the mesh laid out in its <see cref="Frame"/>: the same faces with the same indexes, drawn in the plane as an
        /// elevation is, the grid's first axis along X.
        /// </summary>
        public GeoMesh2 ToMesh2() => _flat;

        /// <summary>
        /// Moves the mesh by a vector.
        /// </summary>
        public GeoMesh3 Translate(GeoVector3 vector)
        {
            var moved = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < moved.Length; i++)
            {
                moved[i] = _vertices[i].Add(vector);
            }

            return new GeoMesh3(_flat, moved, Frame.WithOrigin(Frame.Origin.Add(vector)));
        }

        /// <summary>
        /// Transforms the mesh.
        /// </summary>
        /// <param name="transform">The transformation; it must not flatten the plane of the mesh into a line.</param>
        /// <returns>
        /// The mesh transformed, its faces still counter-clockwise about its normal: a transformation that mirrors turns the
        /// normal round, as it turns that of a polygon. The faces are laid out again in the frame transformed, so that a
        /// scaling stretches them there too.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation flattens the plane of the mesh into a line.</exception>
        public GeoMesh3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            var moved = new GeoPoint3[_vertices.Length];

            for (int i = 0; i < moved.Length; i++)
            {
                moved[i] = transform.Transform(_vertices[i]);
            }

            // Built from the transformed X and Y axes, the frame keeps its right hand, so that a mirror turns its Z axis
            // round with the faces' winding and they still run counter-clockwise about it. The axes are brought back to unit
            // length first: as a scaling of a drawing leaves them, shorter than the global vector tolerance, the frame
            // refused them.
            GeoVector3 x = transform.Transform(Frame.XAxis);
            GeoVector3 y = transform.Transform(Frame.YAxis);
            double lx = x.Length;
            double ly = y.Length;

            if (!(lx > 0.0) || !(ly > 0.0) || double.IsInfinity(lx) || double.IsInfinity(ly))
            {
                throw new ArgumentException("The transformation flattens the plane of the mesh into a line.", nameof(transform));
            }

            x = x.Divide(lx);
            y = y.Divide(ly);
            GeoVector3 z = x.CrossProduct(y);
            double lz = z.Length;

            if (!(lz > 1E-9))
            {
                throw new ArgumentException("The transformation flattens the plane of the mesh into a line.", nameof(transform));
            }

            GeoCoordinateSystem3 frame = new GeoCoordinateSystem3(transform.Transform(Frame.Origin), x, z.Divide(lz).CrossProduct(x));
            var flat = new GeoPoint2[moved.Length];

            for (int i = 0; i < flat.Length; i++)
            {
                GeoPoint3 local = frame.ToLocal(moved[i]);
                flat[i] = new GeoPoint2(local.X, local.Y);
            }

            return new GeoMesh3(_flat.WithVertices(flat), moved, frame);
        }

        /// <summary>
        /// Describes the mesh.
        /// </summary>
        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "GeoMesh3[{0}, Vertices: {1}, Faces: {2}, Area: {3}]", Kind, VertexCount, FaceCount, Area);

        private void Check(int index)
        {
            if (index < 0 || index >= _flat.FaceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "There is no such face.");
            }
        }

        private GeoPolygon3 ToPolygon(int[] face)
        {
            var corners = new GeoPoint3[face.Length];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = _vertices[face[i]];
            }

            // The corners are apart already; filtering them again against the global tolerance could drop one, and a sliver
            // the mesh keeps would be refused as enclosing no area.
            return GeoPolygon3.FromValidated(corners, Normal, AreaOf(face));
        }

        /// <summary>
        /// The area of a face, measured in space from its first corner, so that a face far out costs no precision.
        /// </summary>
        private double AreaOf(int[] face)
        {
            GeoPoint3 reference = _vertices[face[0]];
            GeoVector3 twice = GeoVector3.Zero;

            for (int i = 1; i + 1 < face.Length; i++)
            {
                twice = twice.Add(reference.GetVectorTo(_vertices[face[i]]).CrossProduct(reference.GetVectorTo(_vertices[face[i + 1]])));
            }

            return 0.5 * twice.DotProduct(Normal);
        }

        /// <summary>
        /// Where a point of the flat mesh stands in space: the vertex it is, or for a point the faces' triangles add, the
        /// point of the plane.
        /// </summary>
        private GeoPoint3 InSpace(GeoPoint2 point)
        {
            if (_byPlace == null)
            {
                var byPlace = new Dictionary<GeoPoint2, int>(_vertices.Length);
                GeoPoint2[] flat = _flat.VertexArray;

                for (int i = 0; i < flat.Length; i++)
                {
                    if (!byPlace.ContainsKey(flat[i]))
                    {
                        byPlace.Add(flat[i], i);
                    }
                }

                _byPlace = byPlace;
            }

            return _byPlace.TryGetValue(point, out int index)
                ? _vertices[index]
                : Frame.ToGlobal(new GeoPoint3(point.X, point.Y, 0.0));
        }
    }
}
