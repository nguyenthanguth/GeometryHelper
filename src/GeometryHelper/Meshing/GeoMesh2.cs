using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// A closed shape of the plane broken into faces that share their corners: triangles, the cells of a grid, strips or
    /// convex pieces, as <see cref="MeshKind"/> says.
    /// <para>
    /// Every face is a simple polygon with no hole, running counter-clockwise, given by the indexes of its corners in
    /// <see cref="Vertices"/>. The faces lie within the shape's material and cover it without overlapping, holes left
    /// open, and meet edge to edge: a corner of one face standing on the side of another is a corner of that one too, so
    /// two faces side by side share the same edge, run the two ways round. Such a corner may leave a face with three
    /// corners in a row. The mesh is immutable.
    /// </para>
    /// </summary>
    public sealed class GeoMesh2
    {
        private readonly GeoPoint2[] _vertices;
        private readonly int[][] _faces;
        private readonly bool[] _whole;
        private readonly Tolerance _tolerance;
        private int[][] _adjacent;

        /// <summary>
        /// Initializes a mesh from its vertices and faces, taken as they are.
        /// </summary>
        /// <param name="kind">The kind of faces.</param>
        /// <param name="vertices">The places the faces share.</param>
        /// <param name="faces">Each face as the indexes of its corners, counter-clockwise.</param>
        /// <param name="whole">For each face, whether it is a whole cell of a grid.</param>
        /// <param name="tolerance">The tolerance the mesh was made within, which breaking its faces into triangles reads.</param>
        internal GeoMesh2(MeshKind kind, GeoPoint2[] vertices, int[][] faces, bool[] whole, Tolerance tolerance)
        {
            Kind = kind;
            _vertices = vertices;
            _faces = faces;
            _whole = whole;
            _tolerance = tolerance;

            double area = 0.0;

            foreach (int[] face in faces)
            {
                area += SignedArea(vertices, face);
            }

            Area = area;
        }

        /// <summary>
        /// Gets the kind of faces the mesh was made of.
        /// </summary>
        public MeshKind Kind { get; }

        /// <summary>
        /// The places the faces share, as the mesh holds them, for the meshes built on this one; not to be changed.
        /// </summary>
        internal GeoPoint2[] VertexArray => _vertices;

        /// <summary>
        /// The faces as the indexes of their corners, as the mesh holds them, for the meshes built on this one; not to be
        /// changed.
        /// </summary>
        internal int[][] FaceArray => _faces;

        /// <summary>
        /// The same faces on vertices standing somewhere else, in the same order, counter-clockwise still.
        /// </summary>
        internal GeoMesh2 WithVertices(GeoPoint2[] vertices) => new GeoMesh2(Kind, vertices, _faces, _whole, _tolerance);

        /// <summary>
        /// Gets the places the faces share, each once.
        /// </summary>
        public IReadOnlyList<GeoPoint2> Vertices => _vertices;

        /// <summary>
        /// Gets how many places the faces share.
        /// </summary>
        public int VertexCount => _vertices.Length;

        /// <summary>
        /// Gets how many faces there are.
        /// </summary>
        public int FaceCount => _faces.Length;

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
        public int[] GetFaceIndices(int index) => (int[])Face(index).Clone();

        /// <summary>
        /// Gets a face as a polygon, counter-clockwise, with every corner it has, three in a row among them.
        /// </summary>
        /// <param name="index">The face.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such face.</exception>
        public GeoPolygon2 GetFace(int index) => ToPolygon(Face(index));

        /// <summary>
        /// Gets every face as a polygon, counter-clockwise, in the order of their indexes.
        /// </summary>
        public GeoPolygon2[] GetFaces()
        {
            var faces = new GeoPolygon2[_faces.Length];

            for (int i = 0; i < faces.Length; i++)
            {
                faces[i] = ToPolygon(_faces[i]);
            }

            return faces;
        }

        /// <summary>
        /// Gets whether a face is a whole cell of a grid, which neither the boundary nor a hole cuts by more than the point
        /// tolerance.
        /// </summary>
        /// <param name="index">The face.</param>
        /// <returns>true for a whole cell; false for a cut one, and for every face of a mesh that is not a grid.</returns>
        /// <remarks>
        /// A cell the boundary cuts by no more than the tolerance counts as whole, as a panel a hair short is a whole panel,
        /// and keeps the shape it was cut to, so that it stays within the material.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such face.</exception>
        public bool IsWhole(int index)
        {
            Face(index);
            return _whole[index];
        }

        /// <summary>
        /// Gets every edge of the mesh once, whether one face has it or two.
        /// </summary>
        /// <returns>The edges, in the order the faces first come to them, each running as its first face runs.</returns>
        public GeoLine2[] GetEdges()
        {
            var seen = new HashSet<(int, int)>();
            var edges = new List<GeoLine2>();

            foreach (int[] face in _faces)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Length];

                    if (seen.Add(a < b ? (a, b) : (b, a)))
                    {
                        edges.Add(new GeoLine2(_vertices[a], _vertices[b]));
                    }
                }
            }

            return edges.ToArray();
        }

        /// <summary>
        /// Gets the edges only one face has: the shape's boundary and the rims of its holes, and for a grid with joints
        /// the outline of every cell.
        /// </summary>
        /// <returns>The edges, each running as its face runs, so that the face lies on its left.</returns>
        public GeoLine2[] GetBoundaryEdges()
        {
            var directed = new HashSet<(int, int)>();

            foreach (int[] face in _faces)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    directed.Add((face[i], face[(i + 1) % face.Length]));
                }
            }

            var edges = new List<GeoLine2>();

            foreach (int[] face in _faces)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Length];

                    if (!directed.Contains((b, a)))
                    {
                        edges.Add(new GeoLine2(_vertices[a], _vertices[b]));
                    }
                }
            }

            return edges.ToArray();
        }

        /// <summary>
        /// Gets the faces that share an edge with a face.
        /// </summary>
        /// <param name="index">The face.</param>
        /// <returns>
        /// The neighbours, in ascending order; none for a face of a grid with joints, which touches no other.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such face.</exception>
        public int[] GetAdjacentFaces(int index)
        {
            Face(index);

            if (_adjacent == null)
            {
                _adjacent = BuildAdjacency();
            }

            return (int[])_adjacent[index].Clone();
        }

        /// <summary>
        /// Breaks every face into triangles on its own corners, counter-clockwise.
        /// </summary>
        /// <returns>
        /// The triangles, face by face, covering the faces exactly. Every corner of a face is a corner of one of them, three
        /// in a row included, so the triangles of two faces side by side meet edge to edge as the faces do.
        /// </returns>
        public GeoTriangle2[] ToTriangles()
        {
            var triangles = new List<GeoTriangle2>(_faces.Length * 2);

            foreach (int[] face in _faces)
            {
                AddTriangles(face, triangles);
            }

            return triangles.ToArray();
        }

        /// <summary>
        /// Breaks one face into triangles on its own corners, counter-clockwise, as <see cref="ToTriangles"/> breaks every
        /// face.
        /// </summary>
        internal void AddTriangles(int[] face, List<GeoTriangle2> triangles)
        {
            if (face.Length == 3)
            {
                triangles.Add(new GeoTriangle2(_vertices[face[0]], _vertices[face[1]], _vertices[face[2]]));
            }
            else if (face.Length == 4 && TurnsLeftAtEveryCorner(face))
            {
                // A whole cell, or any four corners that turn the same way at each: either diagonal lies within.
                triangles.Add(new GeoTriangle2(_vertices[face[0]], _vertices[face[1]], _vertices[face[2]]));
                triangles.Add(new GeoTriangle2(_vertices[face[0]], _vertices[face[2]], _vertices[face[3]]));
            }
            else if (!FaceEars.TryTriangulate(_vertices, face, triangles))
            {
                // No ear to clip, as a face rounding has left touching itself has none: the surface's triangles, those of
                // any area.
                foreach (GeoTriangle2 triangle in Triangulation2.Triangulate(new GeoFace2(ToPolygon(face)), _tolerance))
                {
                    if (triangle.SignedArea > 0.0)
                    {
                        triangles.Add(triangle);
                    }
                }
            }
        }

        /// <summary>
        /// Whether a face turns right at none of its corners by more than rounding, so that a fan from any corner covers it;
        /// corners in a row, where the face beside has a corner on its side, are allowed.
        /// </summary>
        internal bool IsConvex(int[] face)
        {
            double size = 0.0;

            for (int i = 1; i < face.Length; i++)
            {
                size = Math.Max(size, _vertices[face[i]].DistanceTo(_vertices[face[0]]));
            }

            for (int i = 0; i < face.Length; i++)
            {
                GeoPoint2 previous = _vertices[face[(i + face.Length - 1) % face.Length]];
                GeoPoint2 corner = _vertices[face[i]];
                GeoPoint2 next = _vertices[face[(i + 1) % face.Length]];
                double turn = (corner.X - previous.X) * (next.Y - corner.Y) - (corner.Y - previous.Y) * (next.X - corner.X);

                if (turn < -1E-12 * size * (previous.DistanceTo(corner) + corner.DistanceTo(next)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Moves the mesh by a vector.
        /// </summary>
        public GeoMesh2 Translate(GeoVector2 vector)
        {
            var moved = new GeoPoint2[_vertices.Length];

            for (int i = 0; i < moved.Length; i++)
            {
                moved[i] = _vertices[i].Add(vector);
            }

            return new GeoMesh2(Kind, moved, _faces, _whole, _tolerance);
        }

        /// <summary>
        /// Turns the mesh about a point.
        /// </summary>
        /// <param name="angleRad">The angle, counter-clockwise.</param>
        /// <param name="center">The point it turns about.</param>
        public GeoMesh2 RotateBy(double angleRad, GeoPoint2 center) => TransformBy(GeoTransform2.Rotation(center, angleRad));

        /// <summary>
        /// Transforms the mesh.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <returns>
        /// The mesh transformed, its faces still counter-clockwise: a transformation that mirrors turns each face round.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoMesh2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            var moved = new GeoPoint2[_vertices.Length];

            for (int i = 0; i < moved.Length; i++)
            {
                moved[i] = transform.Transform(_vertices[i]);
            }

            int[][] faces = _faces;

            if (transform.GetDeterminant() < 0.0)
            {
                faces = new int[_faces.Length][];

                for (int i = 0; i < faces.Length; i++)
                {
                    faces[i] = (int[])_faces[i].Clone();
                    Array.Reverse(faces[i]);
                }
            }

            return new GeoMesh2(Kind, moved, faces, _whole, _tolerance);
        }

        /// <summary>
        /// Describes the mesh.
        /// </summary>
        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "GeoMesh2[{0}, Vertices: {1}, Faces: {2}, Area: {3}]", Kind, VertexCount, FaceCount, Area);

        private int[] Face(int index)
        {
            if (index < 0 || index >= _faces.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "There is no such face.");
            }

            return _faces[index];
        }

        private GeoPolygon2 ToPolygon(int[] face)
        {
            var corners = new GeoPoint2[face.Length];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = _vertices[face[i]];
            }

            // The corners are apart already; filtering them again against the global tolerance could drop one.
            return new GeoPolygon2(corners, corners.Length);
        }

        private int[][] BuildAdjacency()
        {
            var owners = new Dictionary<(int, int), List<int>>();

            for (int f = 0; f < _faces.Length; f++)
            {
                int[] face = _faces[f];

                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Length];
                    (int, int) key = a < b ? (a, b) : (b, a);

                    if (!owners.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>(2);
                        owners.Add(key, list);
                    }

                    list.Add(f);
                }
            }

            var adjacent = new SortedSet<int>[_faces.Length];

            for (int f = 0; f < adjacent.Length; f++)
            {
                adjacent[f] = new SortedSet<int>();
            }

            foreach (List<int> sharing in owners.Values)
            {
                foreach (int f in sharing)
                {
                    foreach (int g in sharing)
                    {
                        if (f != g)
                        {
                            adjacent[f].Add(g);
                        }
                    }
                }
            }

            var result = new int[_faces.Length][];

            for (int f = 0; f < result.Length; f++)
            {
                result[f] = new int[adjacent[f].Count];
                adjacent[f].CopyTo(result[f]);
            }

            return result;
        }

        /// <summary>
        /// Whether a face turns left at every corner by more than rounding, so that a diagonal leaves two triangles of area.
        /// </summary>
        private bool TurnsLeftAtEveryCorner(int[] face)
        {
            double size = 0.0;

            for (int i = 1; i < face.Length; i++)
            {
                size = Math.Max(size, _vertices[face[i]].DistanceTo(_vertices[face[0]]));
            }

            for (int i = 0; i < face.Length; i++)
            {
                GeoPoint2 previous = _vertices[face[(i + face.Length - 1) % face.Length]];
                GeoPoint2 corner = _vertices[face[i]];
                GeoPoint2 next = _vertices[face[(i + 1) % face.Length]];
                double turn = (corner.X - previous.X) * (next.Y - corner.Y) - (corner.Y - previous.Y) * (next.X - corner.X);

                if (!(turn > 1E-12 * size * (previous.DistanceTo(corner) + corner.DistanceTo(next))))
                {
                    return false;
                }
            }

            return true;
        }

        private static double SignedArea(GeoPoint2[] vertices, int[] face)
        {
            GeoPoint2 reference = vertices[face[0]];
            double twice = 0.0;

            for (int i = 1; i + 1 < face.Length; i++)
            {
                GeoPoint2 a = vertices[face[i]];
                GeoPoint2 b = vertices[face[i + 1]];
                twice += (a.X - reference.X) * (b.Y - reference.Y) - (b.X - reference.X) * (a.Y - reference.Y);
            }

            return twice * 0.5;
        }
    }
}
