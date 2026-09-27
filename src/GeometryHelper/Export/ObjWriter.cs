using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Geometry;

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
