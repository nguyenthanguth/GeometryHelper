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
    /// body as the mesh of where its material ends, a ball as the mesh of its surface, a face as its triangles, a chain as a line, a point as a dot. A
    /// polygon that turns the same way at every corner is one face, any other its triangles; a circle, an arc or an
    /// ellipse is a line of chords no further from the curve than the chord tolerance asked for, a chord shorter than the
    /// point tolerance written all the same. Coordinates are written exactly, as a text that reads back to the very double on .NET Framework
    /// and on .NET 10 alike (the digits may differ between the two, a negative nought is -0 on both), so what is looked at is
    /// what was computed, however far from the origin.
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
        public ObjWriter Add(GeoSolid3 solid, string name = null) => Add(solid, Tolerance.Global, name);

        /// <summary>
        /// Adds a body: the mesh of where its material ends, openings cut in, triangulated within a tolerance.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="tolerance">The tolerance the faces are triangulated within, in place of the global one.</param>
        /// <param name="name">What the viewer calls it; a number when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <remarks>
        /// A slab 100 by 100 and 0.0008 thick, built within 1E-5, is meshed in its 12 triangles within 1E-5; at the global
        /// tolerance its four sides, 0.0008 high, count as too thin and are dropped, and the 4 triangles of its two big faces
        /// are what is left.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public ObjWriter Add(GeoSolid3 solid, Tolerance tolerance, string name = null)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return Add(solid.TriangulateSurface(tolerance), name);
        }

        /// <summary>
        /// Adds a face, holes left open.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public ObjWriter Add(GeoFace3 face, string name = null) => Add(face, Tolerance.Global, name);

        /// <summary>
        /// Adds a face, holes left open, triangulated within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance the face is triangulated within, in place of the global one.</param>
        /// <param name="name">What the viewer calls it; a number when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public ObjWriter Add(GeoFace3 face, Tolerance tolerance, string name = null)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Add(face.TriangulateSurface(tolerance), name);
        }

        /// <summary>
        /// Adds a polygon: one face of its own corners when it turns the same way at every one, as a hexagon does, and
        /// its triangles when it does not, as an L does.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public ObjWriter Add(GeoPolygon3 polygon, string name = null) => Add(polygon, Tolerance.Global, name);

        /// <summary>
        /// Adds a polygon as <see cref="Add(GeoPolygon3, string)"/> does, its triangles, where it has them, cut within a
        /// tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance the triangles are cut within, in place of the global one.</param>
        /// <param name="name">What the viewer calls it; a number when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <remarks>
        /// An L-shaped polygon 100 by 100 with a square of 50 taken from a corner is 4 triangles covering 7 500, wound as the
        /// polygon is. Written as one face, a viewer would fan it from its first corner over the notch. A polygon that turns
        /// the same way at every corner but goes twice round, as a pentagram does, is not one face either.
        /// <para>
        /// The triangles are cut within the tolerance, so a polygon that is not one face and is thinner than the tolerance
        /// comes out as an object with no face at all, by design: an L-shaped polygon with arms 0.0004 wide, built within
        /// 1E-5, is empty at the global tolerance and 4 triangles within 1E-5, where an equally thin strip, which is one face,
        /// is written at either.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public ObjWriter Add(GeoPolygon3 polygon, Tolerance tolerance, string name = null)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (!TurnsOneWay(polygon))
            {
                return Add(new GeoFace3(polygon, null, tolerance).TriangulateSurface(tolerance), name);
            }

            Begin(name);

            int first = _vertices + 1;

            foreach (GeoPoint3 corner in polygon.Vertices)
            {
                Vertex(corner);
            }

            _text.Append('f');

            for (int i = first; i <= _vertices; i++)
            {
                _text.Append(' ').Append(i);
            }

            _text.Append('\n');
            return this;
        }

        /// <summary>
        /// Adds a point, which a viewer shows as a dot.
        /// </summary>
        public ObjWriter Add(GeoPoint3 point, string name = null)
        {
            Begin(name);
            Vertex(point);
            _text.Append("p ").Append(_vertices).Append('\n');
            return this;
        }

        /// <summary>
        /// Adds a triangle as a face of its own.
        /// </summary>
        public ObjWriter Add(GeoTriangle3 triangle, string name = null) => Add(new[] { triangle }, name);

        /// <summary>
        /// Adds a box as its twelve triangles.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty, or flat on some axis.</exception>
        public ObjWriter Add(GeoAabb3 box, string name = null) => Add(box.ToObb(), name);

        /// <summary>
        /// Adds an oriented box as its twelve triangles.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is flat on some axis.</exception>
        public ObjWriter Add(GeoObb3 box, string name = null)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return Add(box.ToSolid(), name);
        }

        /// <summary>
        /// Adds one cell of a body cut into a grid, the mesh of its material.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <param name="name">What the object's name starts with; "cell" when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <remarks>
        /// Named as <see cref="Add(GeoCellGrid3, string)"/> names it, <c>name_i_j_k</c>, and meshed at the global tolerance:
        /// a cell cut within a tighter one is better added through its grid.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the cell is null.</exception>
        public ObjWriter Add(GeoCell3 cell, string name = null)
        {
            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell));
            }

            return Add(cell.Solid.Triangulate(), CellLabel(string.IsNullOrWhiteSpace(name) ? "cell" : name, cell));
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
                // At the tolerance the cells were cut within, which a face thinner than the global one is not too thin for.
                Add(cell.Solid.Triangulate(grid.Tolerance), CellLabel(prefix, cell));
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
        /// <remarks>
        /// <para>
        /// The tolerance is any number above nought, <b>infinity included</b>, which cuts each bend into one chord; the
        /// <see cref="Wkt"/> writers and the other chord overloads here refuse infinity.
        /// </para>
        /// <para>
        /// On ordinary input the points are those of <see cref="GeoPolylineArc3.ToPolyline3(double)"/>: the same bends cut
        /// into the same number of chords at the same places. Four things differ from it. The end of each arc is the edge's own
        /// end point, where the library's last chord, worked out from the circle's angle, may miss it by a rounding; and an arc so
        /// shallow that its circle cannot be placed, a bulge of 1E-200 having a centre that is not a number, is read as its chord.
        /// An edge or a chord shorter than the
        /// point tolerance is kept, where <c>ToPolyline3</c> drops a point within that tolerance of the one before. The count
        /// is worked out here from the arc sine, as the library works it out: a bend of radius 50 000 within 0.001 is
        /// 3 927 chords to a quarter turn, none more than 0.001 inside the bend. And at a knife-edge count the exact one may be a chord more than the library's, never
        /// one fewer. No bend is cut into more than 1 048 576 chords, the chords of all of them together likewise.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is nought, negative or not a number, or would need more than 1 048 576 chords.
        /// </exception>
        public ObjWriter Add(GeoPolylineArc3 chain, double chordTolerance, string name = null)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            // As the member has always checked it: infinity is a tolerance, and gives one chord to a bend.
            if (double.IsNaN(chordTolerance) || chordTolerance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(chordTolerance), "A chord tolerance has to be a positive number.");
            }

            return Line(Chords.Of(chain, chordTolerance), name);
        }

        /// <summary>
        /// Adds a loop with bends as a closed line, each bend cut into chords no further than a tolerance from it.
        /// </summary>
        /// <remarks>
        /// The line comes back to where it started, as OBJ has no closed line: its last vertex is its first. Each bend is cut
        /// on its own radius, so a long bend of a large radius is within the tolerance too: a loop with corners of radius
        /// 50 000, within 0.001, has 3 927 chords to a quarter turn of each. The chords of all its bends together are no more
        /// than 1 048 576.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public ObjWriter Add(GeoPolygonArc3 loop, double chordTolerance, string name = null)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            Chords.Require(chordTolerance);
            return Line(Chords.Of(loop, chordTolerance), name);
        }

        /// <summary>
        /// Adds an arc as a line, cut into chords no further than a tolerance from it.
        /// </summary>
        /// <remarks>
        /// The line runs from the start of the arc to its end, whichever way the arc turns. A quarter of a circle of radius
        /// 100 cut within 0.1 is 18 chords of about 8.7. The count is
        /// worked out from the arc sine: a quarter of radius 50 000 within 0.001 is
        /// 3 927 chords, none more than 0.001 inside the arc. No curve is cut into more than 1 048 576 chords.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public ObjWriter Add(GeoArc3 arc, double chordTolerance, string name = null)
        {
            Chords.Require(chordTolerance);
            return Line(Chords.Of(arc, chordTolerance), name);
        }

        /// <summary>
        /// Adds a circle as a closed line, cut into chords no further than a tolerance from it.
        /// </summary>
        /// <remarks>
        /// A circle of radius 100 cut within 0.1 is a line of 71 chords whose last vertex is its first. OBJ has no circle,
        /// and a viewer shows the line, not a disc. The count is worked out from the arc sine: a
        /// circle of radius 50 000 within 0.001 is 15 708 chords, none more than 0.001 inside it. No curve is cut into more
        /// than 1 048 576 chords.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public ObjWriter Add(GeoCircle3 circle, double chordTolerance, string name = null)
        {
            Chords.Require(chordTolerance);
            return Line(Chords.Of(circle, chordTolerance), name);
        }

        /// <summary>
        /// Adds an ellipse as a closed line, cut into chords no further than a tolerance from it.
        /// </summary>
        /// <remarks>
        /// The chords are spaced by the bend of the rim for up to 4 096 of them a turn, and evenly in the angle
        /// past that: an ellipse of radii 50 000 and 25 000 within 0.001 is 15 708 chords. No curve is cut into
        /// more than 1 048 576 chords.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public ObjWriter Add(GeoEllipse3 ellipse, double chordTolerance, string name = null)
        {
            Chords.Require(chordTolerance);
            return Line(Chords.Of(ellipse, chordTolerance), name);
        }

        /// <summary>
        /// Adds a ball as the mesh of its surface, cut into triangles no further than a tolerance from it.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="chordTolerance">The largest gap allowed between a triangle and the sphere, in millimetres.</param>
        /// <param name="name">What the viewer calls it; a number when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <remarks>
        /// The mesh is <see cref="GeoSphere3.TriangulateSurface(double)"/>, as a body is written as its triangles: a ball of radius 50
        /// within 0.1 is 3 380 triangles, every corner on the sphere, wound outwards. A tolerance that needs more than 1 048 576
        /// triangles is refused; radius 50 within 0.0001 would need three million.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the ball is not valid, as a <c>default</c> one is.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 triangles.
        /// </exception>
        public ObjWriter Add(GeoSphere3 sphere, double chordTolerance, string name = null)
        {
            Chords.Require(chordTolerance);
            return Add(sphere.TriangulateSurface(chordTolerance), name);
        }

        /// <summary>
        /// Adds a ray as a line from its origin, for as far as a length: a ray has no end of its own.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="length">How far along the ray the line runs.</param>
        /// <param name="name">What the viewer calls it; a number when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="ArgumentException">Thrown when the ray has no direction, as a <c>default</c> one has.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is negative, NaN or infinite.</exception>
        public ObjWriter Add(GeoRay3 ray, double length, string name = null)
        {
            Chords.Require(ray);
            return Add(ray.ToLine(length), name);
        }

        /// <summary>
        /// Adds a line without ends as a line of a length, centred on its origin: the line has no end of its own.
        /// </summary>
        /// <param name="xline">The line.</param>
        /// <param name="length">How long the drawn line is: half of it either side of the origin.</param>
        /// <param name="name">What the viewer calls it; a number when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <remarks>
        /// The line through (1, 2, 3) along Z, drawn 50 long, runs from (1, 2, -22) to (1, 2, 28).
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the line has no direction, as a <c>default</c> one has.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is negative, NaN or infinite.</exception>
        public ObjWriter Add(GeoXLine3 xline, double length, string name = null) => Add(Chords.Segment(xline, length), name);

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

        private static string CellLabel(string prefix, GeoCell3 cell)
        {
            string label = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}_{3}", prefix, cell.I, cell.J, cell.K);

            if (cell.Piece > 0)
            {
                label += "_" + cell.Piece.ToString(CultureInfo.InvariantCulture);
            }

            return label;
        }

        /// <summary>
        /// Whether a polygon turns the same way at every corner, by more than rounding, and turns once round in all, so that a
        /// viewer fanning it from its first corner covers it exactly; corners in a row are allowed. A pentagram turns the same
        /// way at each of its five corners and twice round, and is not one; nor is a polygon with a needle in it, whose tip
        /// goes out and comes back on itself.
        /// </summary>
        private static bool TurnsOneWay(GeoPolygon3 polygon)
        {
            IReadOnlyList<GeoPoint3> corners = polygon.Vertices;
            double size = 0.0;
            double turning = 0.0;

            for (int i = 1; i < corners.Count; i++)
            {
                size = Math.Max(size, corners[i].DistanceTo(corners[0]));
            }

            for (int i = 0; i < corners.Count; i++)
            {
                GeoPoint3 previous = corners[(i + corners.Count - 1) % corners.Count];
                GeoPoint3 corner = corners[i];
                GeoPoint3 next = corners[(i + 1) % corners.Count];
                GeoVector3 arriving = previous.GetVectorTo(corner);
                GeoVector3 leaving = corner.GetVectorTo(next);
                double turn = arriving.CrossProduct(leaving).DotProduct(polygon.Normal);

                double threshold = 1E-12 * size * (previous.DistanceTo(corner) + corner.DistanceTo(next));
                double along = arriving.DotProduct(leaving);

                // A turn to the right, or a reversal that is not a turn either way: the tip of a needle goes out and comes back
                // on itself, and a viewer fanning the polygon would cover what lies beside it.
                if (turn < -threshold || (along < 0.0 && Math.Abs(turn) <= threshold))
                {
                    return false;
                }

                turning += Math.Atan2(turn, along);
            }

            return Math.Abs(Math.Abs(turning) - Math.PI * 2.0) <= 1E-9 * Math.PI * 2.0;
        }

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
                .Append(Numbers.Exact(point.X)).Append(' ')
                .Append(Numbers.Exact(point.Y)).Append(' ')
                .Append(Numbers.Exact(point.Z)).Append('\n');
        }
    }
}
