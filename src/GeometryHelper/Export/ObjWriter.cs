using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Writes geometry in space as a Wavefront OBJ file, to look at in any 3D viewer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every shape added becomes an object of its own, named as given, so a viewer lists them separately: a
    /// body as the mesh of where its material ends, a face as its triangles, a chain as a line. Coordinates are
    /// written exactly, so what is looked at is what was computed, however far from the origin.
    /// </para>
    /// <code>
    /// new ObjWriter().Add(plate, "plate").Add(bolt, "bolt").Save("clash.obj");
    /// </code>
    /// </remarks>
    public sealed class ObjWriter
    {
        private readonly StringBuilder _text = new StringBuilder();
        private int _vertices;
        private int _objects;

        /// <summary>
        /// Adds a body: the mesh of where its material ends, openings cut in.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="name">What the viewer calls it; a number when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public ObjWriter Add(GeoSolid3 solid, string name = null)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return Add(solid.TriangulateSurface(), name);
        }

        /// <summary>
        /// Adds a face, holes left open.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public ObjWriter Add(GeoFace3 face, string name = null)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Add(face.TriangulateSurface(), name);
        }

        /// <summary>
        /// Adds a mesh of triangles.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the triangles are null.</exception>
        public ObjWriter Add(IEnumerable<GeoTriangle3> triangles, string name = null)
        {
            if (triangles == null)
            {
                throw new ArgumentNullException(nameof(triangles));
            }

            Begin(name);

            foreach (GeoTriangle3 triangle in triangles)
            {
                Vertex(triangle.A);
                Vertex(triangle.B);
                Vertex(triangle.C);
                _text.Append("f ").Append(_vertices - 2).Append(' ').Append(_vertices - 1).Append(' ').Append(_vertices).Append('\n');
            }

            return this;
        }

        /// <summary>
        /// Adds the mesh of a flat shape, its faces sharing their vertices: a face that turns right at none of its corners as
        /// one polygon, so that a viewer shows the cells of a grid as they are, and any other as its triangles.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the mesh is null.</exception>
        public ObjWriter Add(GeoMesh3 mesh, string name = null)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            Begin(name);

            int first = _vertices + 1;

            foreach (GeoPoint3 vertex in mesh.Vertices)
            {
                Vertex(vertex);
            }

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                if (mesh.IsConvex(f))
                {
                    _text.Append('f');

                    foreach (int corner in mesh.GetFaceIndices(f))
                    {
                        _text.Append(' ').Append(first + corner);
                    }

                    _text.Append('\n');
                    continue;
                }

                // A viewer fans a polygon from its first corner, which covers what a face turning right at a corner does not
                // hold; its own triangles cover it exactly.
                foreach (GeoTriangle3 triangle in mesh.GetFaceTriangles(f))
                {
                    Vertex(triangle.A);
                    Vertex(triangle.B);
                    Vertex(triangle.C);
                    _text.Append("f ").Append(_vertices - 2).Append(' ').Append(_vertices - 1).Append(' ').Append(_vertices).Append('\n');
                }
            }

            return this;
        }

        /// <summary>
        /// Adds the cells of a body, each an object of its own named after its indexes, so that a viewer lists them and shows
        /// or hides them one by one.
        /// </summary>
        /// <param name="grid">The cells.</param>
        /// <param name="name">What the objects' names start with; "cell" when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the grid is null.</exception>
        /// <remarks>
        /// A cell is written as the mesh of its body, <c>name_i_j_k</c>, and a second or later piece of one cell with its
        /// number after, <c>name_i_j_k_p</c>.
        /// </remarks>
        public ObjWriter Add(GeoCellGrid3 grid, string name = null)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            string prefix = string.IsNullOrWhiteSpace(name) ? "cell" : name;

            foreach (GeoCell3 cell in grid.Cells)
            {
                string label = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}_{3}", prefix, cell.I, cell.J, cell.K);

                if (cell.Piece > 0)
                {
                    label += "_" + cell.Piece.ToString(CultureInfo.InvariantCulture);
                }

                Add(cell.Solid.Triangulate(), label);
            }

            return this;
        }

        /// <summary>
        /// Adds a chain as a line through its vertices.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public ObjWriter Add(GeoPolyline3 polyline, string name = null)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return Line(polyline.Vertices, name);
        }

        /// <summary>
        /// Adds a chain with bends as a line, each bend cut into chords no further than a tolerance from it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public ObjWriter Add(GeoPolylineArc3 chain, double chordTolerance, string name = null)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Line(chain.ToPolyline3(chordTolerance).Vertices, name);
        }

        /// <summary>
        /// Adds a segment as a line.
        /// </summary>
        public ObjWriter Add(GeoLine3 line, string name = null) => Line(new[] { line.StartPoint, line.EndPoint }, name);

        /// <summary>
        /// Gets the file as text.
        /// </summary>
        public override string ToString() => "# GeometryHelper\n" + _text;

        /// <summary>
        /// Writes the file to a writer.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the writer is null.</exception>
        public void WriteTo(TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.Write(ToString());
        }

        /// <summary>
        /// Writes the file to a path, replacing whatever is there.
        /// </summary>
        public void Save(string path) => File.WriteAllText(path, ToString(), new UTF8Encoding(false));

        private ObjWriter Line(IReadOnlyList<GeoPoint3> points, string name)
        {
            Begin(name);

            int first = _vertices + 1;

            foreach (GeoPoint3 point in points)
            {
                Vertex(point);
            }

            _text.Append('l');

            for (int i = first; i <= _vertices; i++)
            {
                _text.Append(' ').Append(i);
            }

            _text.Append('\n');
            return this;
        }

        private void Begin(string name)
        {
            _objects++;
            _text.Append("o ").Append(string.IsNullOrWhiteSpace(name) ? "shape" + _objects : name.Replace(' ', '_')).Append('\n');
        }

        private void Vertex(GeoPoint3 point)
        {
            _vertices++;
            _text.Append("v ")
                .Append(point.X.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                .Append(point.Y.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                .Append(point.Z.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        }
    }
}
