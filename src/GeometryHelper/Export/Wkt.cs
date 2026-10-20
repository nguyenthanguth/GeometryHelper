using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Writes shapes as well-known text, which GIS tools, databases and online viewers read.
    /// </summary>
    /// <remarks>
    /// Every ring is written closed, its first point repeated at the end, as the format asks. A polygon's
    /// boundary goes counter-clockwise and its holes clockwise. Coordinates are written exactly, as a text that reads back to the
    /// very double on .NET Framework and on .NET 10 alike (the digits may differ between the two, a negative nought is -0 on
    /// both). A curve has no well-known text that online viewers and most tools read, so circles, arcs,
    /// ellipses and chains with arcs are written through chords, no further from the curve than the chord tolerance asked
    /// for, a chord shorter than the point tolerance written all the same, and a ray or a line without ends through a length. A body is written as a <c>POLYHEDRALSURFACE Z</c> of triangles.
    /// </remarks>
    public static class Wkt
    {
        /// <summary>Writes a point: <c>POINT (x y)</c>.</summary>
        public static string Write(GeoPoint2 point) => "POINT (" + Xy(point) + ")";

        /// <summary>Writes a point in space: <c>POINT Z (x y z)</c>.</summary>
        public static string Write(GeoPoint3 point) => "POINT Z (" + Xyz(point) + ")";

        /// <summary>Writes a segment: <c>LINESTRING (...)</c>.</summary>
        public static string Write(GeoLine2 line) => "LINESTRING (" + Xy(line.StartPoint) + ", " + Xy(line.EndPoint) + ")";

        /// <summary>Writes a segment in space: <c>LINESTRING Z (...)</c>.</summary>
        public static string Write(GeoLine3 line) => "LINESTRING Z (" + Xyz(line.StartPoint) + ", " + Xyz(line.EndPoint) + ")";

        /// <summary>Writes a chain: <c>LINESTRING (...)</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static string Write(GeoPolyline2 polyline)
            => "LINESTRING (" + Points(NotNull(polyline, nameof(polyline)).Vertices, false) + ")";

        /// <summary>Writes a chain in space: <c>LINESTRING Z (...)</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static string Write(GeoPolyline3 polyline)
            => "LINESTRING Z (" + Points(NotNull(polyline, nameof(polyline)).Vertices, false) + ")";

        /// <summary>Writes a polygon: <c>POLYGON ((...))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static string Write(GeoPolygon2 polygon)
            => "POLYGON (" + Ring(NotNull(polygon, nameof(polygon)), true) + ")";

        /// <summary>Writes a polygon in space: <c>POLYGON Z ((...))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static string Write(GeoPolygon3 polygon)
            => "POLYGON Z ((" + Points(NotNull(polygon, nameof(polygon)).Vertices, true) + "))";

        /// <summary>Writes a face with its holes: <c>POLYGON ((...), (...))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static string Write(GeoFace2 face) => "POLYGON " + Rings(NotNull(face, nameof(face)));

        /// <summary>Writes several faces: <c>MULTIPOLYGON (((...)), ((...)))</c>, such as what a boolean gives back.</summary>
        /// <remarks>
        /// Faces that share an edge parse everywhere but are not a valid multipolygon in the OGC sense, whose polygons touch at
        /// points only: PostGIS's <c>ST_IsValid</c> says false of them. What a boolean gives back is apart, and valid.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the faces or one of them are null.</exception>
        public static string Write(IEnumerable<GeoFace2> faces)
        {
            if (faces == null)
            {
                throw new ArgumentNullException(nameof(faces));
            }

            var parts = new List<string>();

            foreach (GeoFace2 face in faces)
            {
                parts.Add(Rings(NotNull(face, nameof(faces))));
            }

            return parts.Count == 0 ? "MULTIPOLYGON EMPTY" : "MULTIPOLYGON (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes a rectangle: <c>POLYGON ((...))</c>, counter-clockwise.</summary>
        public static string Write(GeoRectangle2 rectangle) => "POLYGON (" + RingOf(rectangle.GetVertices(), true) + ")";

        /// <summary>Writes a triangle: <c>POLYGON ((...))</c>, counter-clockwise whichever way it was given.</summary>
        public static string Write(GeoTriangle2 triangle) => "POLYGON (" + RingOf(new[] { triangle.A, triangle.B, triangle.C }, true) + ")";

        /// <summary>Writes a triangle in space: <c>POLYGON Z ((...))</c>, its corners in the order they were given.</summary>
        public static string Write(GeoTriangle3 triangle) => "POLYGON Z ((" + Points(new[] { triangle.A, triangle.B, triangle.C }, true) + "))";

        /// <summary>
        /// Writes a face in space with its holes: <c>POLYGON Z ((...), (...))</c>.
        /// </summary>
        /// <remarks>
        /// Space has no counter-clockwise, so it is taken about the face's normal: the boundary runs counter-clockwise about
        /// it and each hole clockwise, as for a face of the plane.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static string Write(GeoFace3 face) => "POLYGON Z " + Rings(NotNull(face, nameof(face)));

        /// <summary>Writes several faces in space: <c>MULTIPOLYGON Z (((...)), ((...)))</c>.</summary>
        /// <remarks>
        /// Faces that share an edge parse everywhere but are not a valid multipolygon in the OGC sense, whose polygons touch at
        /// points only: <c>ST_IsValid</c> says false of them.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the faces or one of them are null.</exception>
        public static string Write(IEnumerable<GeoFace3> faces)
        {
            if (faces == null)
            {
                throw new ArgumentNullException(nameof(faces));
            }

            var parts = new List<string>();

            foreach (GeoFace3 face in faces)
            {
                parts.Add(Rings(NotNull(face, nameof(faces))));
            }

            return parts.Count == 0 ? "MULTIPOLYGON Z EMPTY" : "MULTIPOLYGON Z (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes a mesh of the plane: <c>MULTIPOLYGON (((...)), ((...)))</c>, a polygon to each face.</summary>
        /// <remarks>
        /// A mesh of a grid of 4 cells is a multipolygon of 4 polygons, counter-clockwise as its faces are. The cells share
        /// their edges, so the text parses everywhere but is not a valid multipolygon in the OGC sense, whose polygons touch
        /// at points only: <c>ST_IsValid</c> says false of it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the mesh is null.</exception>
        public static string Write(GeoMesh2 mesh)
        {
            NotNull(mesh, nameof(mesh));

            var parts = new List<string>(mesh.FaceCount);

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                var corners = new List<GeoPoint2>();

                foreach (int index in mesh.GetFaceIndices(f))
                {
                    corners.Add(mesh.Vertices[index]);
                }

                parts.Add("((" + Points(corners, true) + "))");
            }

            return parts.Count == 0 ? "MULTIPOLYGON EMPTY" : "MULTIPOLYGON (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes a flat mesh in space: <c>MULTIPOLYGON Z (((...)), ((...)))</c>, a polygon to each face.</summary>
        /// <remarks>
        /// The faces share their edges, so the text parses everywhere but is not a valid multipolygon in the OGC sense, whose
        /// polygons touch at points only: <c>ST_IsValid</c> says false of it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the mesh is null.</exception>
        public static string Write(GeoMesh3 mesh)
        {
            NotNull(mesh, nameof(mesh));

            var parts = new List<string>(mesh.FaceCount);

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                var corners = new List<GeoPoint3>();

                foreach (int index in mesh.GetFaceIndices(f))
                {
                    corners.Add(mesh.Vertices[index]);
                }

                parts.Add("((" + Points(corners, true) + "))");
            }

            return parts.Count == 0 ? "MULTIPOLYGON Z EMPTY" : "MULTIPOLYGON Z (" + string.Join(", ", parts) + ")";
        }

        /// <summary>
        /// Writes a body as the triangles of where its material ends: <c>POLYHEDRALSURFACE Z (((...)), ((...)))</c>.
        /// </summary>
        /// <remarks>
        /// A box 30 by 20 by 10 is 12 triangles, wound outwards, whose signed volumes add up to 6 000. PostGIS reads the form;
        /// it is the only well-known text for a body. Openings are cut in, and the triangles are made at the global tolerance.
        /// A body the openings take all of is <c>POLYHEDRALSURFACE Z EMPTY</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public static string Write(GeoSolid3 solid)
        {
            GeoTriangle3[] triangles = NotNull(solid, nameof(solid)).TriangulateSurface();

            if (triangles.Length == 0)
            {
                return "POLYHEDRALSURFACE Z EMPTY";
            }

            var parts = new List<string>(triangles.Length);

            foreach (GeoTriangle3 triangle in triangles)
            {
                parts.Add("((" + Points(new[] { triangle.A, triangle.B, triangle.C }, true) + "))");
            }

            return "POLYHEDRALSURFACE Z (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes points: <c>MULTIPOINT ((x y), (x y))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        public static string Write(IEnumerable<GeoPoint2> points)
        {
            var parts = new List<string>();

            foreach (GeoPoint2 point in NotNull(points, nameof(points)))
            {
                parts.Add("(" + Xy(point) + ")");
            }

            return parts.Count == 0 ? "MULTIPOINT EMPTY" : "MULTIPOINT (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes points in space: <c>MULTIPOINT Z ((x y z), (x y z))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        public static string Write(IEnumerable<GeoPoint3> points)
        {
            var parts = new List<string>();

            foreach (GeoPoint3 point in NotNull(points, nameof(points)))
            {
                parts.Add("(" + Xyz(point) + ")");
            }

            return parts.Count == 0 ? "MULTIPOINT Z EMPTY" : "MULTIPOINT Z (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes segments: <c>MULTILINESTRING ((...), (...))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the segments are null.</exception>
        public static string Write(IEnumerable<GeoLine2> lines)
        {
            var parts = new List<string>();

            foreach (GeoLine2 line in NotNull(lines, nameof(lines)))
            {
                parts.Add("(" + Xy(line.StartPoint) + ", " + Xy(line.EndPoint) + ")");
            }

            return parts.Count == 0 ? "MULTILINESTRING EMPTY" : "MULTILINESTRING (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes segments in space: <c>MULTILINESTRING Z ((...), (...))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the segments are null.</exception>
        public static string Write(IEnumerable<GeoLine3> lines)
        {
            var parts = new List<string>();

            foreach (GeoLine3 line in NotNull(lines, nameof(lines)))
            {
                parts.Add("(" + Xyz(line.StartPoint) + ", " + Xyz(line.EndPoint) + ")");
            }

            return parts.Count == 0 ? "MULTILINESTRING Z EMPTY" : "MULTILINESTRING Z (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes chains: <c>MULTILINESTRING ((...), (...))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the chains or one of them are null.</exception>
        public static string Write(IEnumerable<GeoPolyline2> polylines)
        {
            var parts = new List<string>();

            foreach (GeoPolyline2 polyline in NotNull(polylines, nameof(polylines)))
            {
                parts.Add("(" + Points(NotNull(polyline, nameof(polylines)).Vertices, false) + ")");
            }

            return parts.Count == 0 ? "MULTILINESTRING EMPTY" : "MULTILINESTRING (" + string.Join(", ", parts) + ")";
        }

        /// <summary>Writes chains in space: <c>MULTILINESTRING Z ((...), (...))</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the chains or one of them are null.</exception>
        public static string Write(IEnumerable<GeoPolyline3> polylines)
        {
            var parts = new List<string>();

            foreach (GeoPolyline3 polyline in NotNull(polylines, nameof(polylines)))
            {
                parts.Add("(" + Points(NotNull(polyline, nameof(polylines)).Vertices, false) + ")");
            }

            return parts.Count == 0 ? "MULTILINESTRING Z EMPTY" : "MULTILINESTRING Z (" + string.Join(", ", parts) + ")";
        }

        /// <summary>
        /// Writes a circle as a polygon, cut into chords no further than a tolerance from it: <c>POLYGON ((...))</c>.
        /// </summary>
        /// <remarks>
        /// Well-known text has no curves that online viewers and most tools read, so a curve is written through its chords.
        /// A circle of radius 100 cut within 0.1 is a ring of 71 chords, counter-clockwise. The count is
        /// worked out from the arc sine: a circle of
        /// radius 50 000 within 0.001 is 15 708 chords, none more than 0.001 inside it. No curve is cut into more than
        /// 1 048 576 chords.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoCircle2 circle, double chordTolerance)
        {
            Chords.Require(chordTolerance);
            return "POLYGON (" + RingOf(Chords.Of(circle, chordTolerance), true) + ")";
        }

        /// <summary>
        /// Writes an arc as a chain, cut into chords no further than a tolerance from it: <c>LINESTRING (...)</c>, from the
        /// start of the arc to its end whichever way it turns.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoArc2 arc, double chordTolerance)
        {
            Chords.Require(chordTolerance);
            return "LINESTRING (" + Points(Chords.Of(arc, chordTolerance), false) + ")";
        }

        /// <summary>
        /// Writes an ellipse as a polygon, cut into chords no further than a tolerance from it: <c>POLYGON ((...))</c>,
        /// counter-clockwise.
        /// </summary>
        /// <remarks>
        /// Spaced by the bend of the rim for up to 4 096 chords a turn, and evenly in the angle past that: an ellipse of radii 50 000 and 25 000 within 0.001 is 15 708 chords. No curve is cut into more than
        /// 1 048 576 chords.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoEllipse2 ellipse, double chordTolerance)
        {
            Chords.Require(chordTolerance);
            return "POLYGON (" + RingOf(Chords.Of(ellipse, chordTolerance), true) + ")";
        }

        /// <summary>
        /// Writes a chain with arcs, each arc cut into chords no further than a tolerance from it: <c>LINESTRING (...)</c>.
        /// </summary>
        /// <remarks>
        /// Each arc is cut on its own radius, so a long bend of a large radius is within the tolerance too; the chords of all
        /// its arcs together are no more than 1 048 576.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoPolylineArc2 chain, double chordTolerance)
        {
            NotNull(chain, nameof(chain));
            Chords.Require(chordTolerance);
            return "LINESTRING (" + Points(Chords.Of(chain.GetEdges(), chordTolerance), false) + ")";
        }

        /// <summary>
        /// Writes a loop with arcs, each arc cut into chords no further than a tolerance from it: <c>POLYGON ((...))</c>,
        /// counter-clockwise.
        /// </summary>
        /// <remarks>
        /// Each arc is cut on its own radius, so a long bend of a large radius is within the tolerance too; the chords of all
        /// its arcs together are no more than 1 048 576.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoPolygonArc2 loop, double chordTolerance)
        {
            NotNull(loop, nameof(loop));
            Chords.Require(chordTolerance);

            // The walk comes back to the corner it started from, and a ring closes itself.
            List<GeoPoint2> points = Chords.Of(loop.GetEdges(), chordTolerance);
            points.RemoveAt(points.Count - 1);
            return "POLYGON (" + RingOf(points, true) + ")";
        }

        /// <summary>
        /// Writes an arc in space as a chain, cut into chords no further than a tolerance from it: <c>LINESTRING Z (...)</c>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoArc3 arc, double chordTolerance)
        {
            Chords.Require(chordTolerance);
            return "LINESTRING Z (" + Points(Chords.Of(arc, chordTolerance), false) + ")";
        }

        /// <summary>
        /// Writes a circle in space as a polygon, cut into chords no further than a tolerance from it:
        /// <c>POLYGON Z ((...))</c>, counter-clockwise about its normal.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoCircle3 circle, double chordTolerance)
        {
            Chords.Require(chordTolerance);
            return "POLYGON Z ((" + Points(Chords.Of(circle, chordTolerance), false) + "))";
        }

        /// <summary>
        /// Writes an ellipse in space as a polygon, cut into chords no further than a tolerance from it:
        /// <c>POLYGON Z ((...))</c>, counter-clockwise about its normal.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoEllipse3 ellipse, double chordTolerance)
        {
            Chords.Require(chordTolerance);
            return "POLYGON Z ((" + Points(Chords.Of(ellipse, chordTolerance), false) + "))";
        }

        /// <summary>
        /// Writes a chain with arcs in space, each arc cut into chords no further than a tolerance from it:
        /// <c>LINESTRING Z (...)</c>.
        /// </summary>
        /// <remarks>
        /// Each arc is cut on its own radius, so a long bend of a large radius is within the tolerance too; the chords of all
        /// its arcs together are no more than 1 048 576.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoPolylineArc3 chain, double chordTolerance)
        {
            NotNull(chain, nameof(chain));
            Chords.Require(chordTolerance);
            return "LINESTRING Z (" + Points(Chords.Of(chain, chordTolerance), false) + ")";
        }

        /// <summary>
        /// Writes a loop with arcs in space, each arc cut into chords no further than a tolerance from it:
        /// <c>POLYGON Z ((...))</c>.
        /// </summary>
        /// <remarks>
        /// Each arc is cut on its own radius, so a long bend of a large radius is within the tolerance too; the chords of all
        /// its arcs together are no more than 1 048 576.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or would need more than 1 048 576 chords.
        /// </exception>
        public static string Write(GeoPolygonArc3 loop, double chordTolerance)
        {
            NotNull(loop, nameof(loop));
            Chords.Require(chordTolerance);
            return "POLYGON Z ((" + Points(Chords.Of(loop, chordTolerance), false) + "))";
        }

        /// <summary>
        /// Writes a ray as a segment from its origin for a length, as a ray has no end of its own: <c>LINESTRING (...)</c>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the ray has no direction, as a <c>default</c> one has.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is negative, NaN or infinite.</exception>
        public static string Write(GeoRay2 ray, double length)
        {
            Chords.Require(ray);
            return Write(ray.ToLine(length));
        }

        /// <summary>
        /// Writes a ray in space as a segment from its origin for a length: <c>LINESTRING Z (...)</c>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the ray has no direction, as a <c>default</c> one has.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is negative, NaN or infinite.</exception>
        public static string Write(GeoRay3 ray, double length)
        {
            Chords.Require(ray);
            return Write(ray.ToLine(length));
        }

        /// <summary>
        /// Writes a line without ends in the plane as a segment of a length, centred on its origin, as it has no end of its
        /// own: <c>LINESTRING (...)</c>.
        /// </summary>
        /// <remarks>
        /// The line through (10, 20) along X, written 100 long, is <c>LINESTRING (-40 20, 60 20)</c>.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the line has no direction, as a <c>default</c> one has.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is negative, NaN or infinite.</exception>
        public static string Write(GeoXLine2 xline, double length) => Write(Chords.Segment(xline, length));

        /// <summary>
        /// Writes a line without ends in space as a segment of a length, centred on its origin: <c>LINESTRING Z (...)</c>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the line has no direction, as a <c>default</c> one has.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is negative, NaN or infinite.</exception>
        public static string Write(GeoXLine3 xline, double length) => Write(Chords.Segment(xline, length));

        private static string Rings(GeoFace3 face)
        {
            var rings = new List<string> { "(" + Points(face.Boundary.Vertices, true) + ")" };

            // The face holds its holes wound as its boundary is; the format asks them the other way round.
            foreach (GeoPolygon3 hole in face.Holes)
            {
                var corners = new List<GeoPoint3>(hole.Vertices);
                corners.Reverse();
                rings.Add("(" + Points(corners, true) + ")");
            }

            return "(" + string.Join(", ", rings) + ")";
        }

        private static string RingOf(IReadOnlyList<GeoPoint2> points, bool counterClockwise)
        {
            double twice = 0.0;

            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint2 next = points[(i + 1) % points.Count];
                twice += points[i].X * next.Y - next.X * points[i].Y;
            }

            var ordered = new List<GeoPoint2>(points);

            if ((twice > 0.0) != counterClockwise)
            {
                ordered.Reverse();
            }

            return "(" + Points(ordered, true) + ")";
        }

        private static string Rings(GeoFace2 face)
        {
            var rings = new List<string> { Ring(face.Boundary, true) };

            foreach (GeoPolygon2 hole in face.Holes)
            {
                rings.Add(Ring(hole, false));
            }

            return "(" + string.Join(", ", rings) + ")";
        }

        private static string Ring(GeoPolygon2 polygon, bool counterClockwise)
        {
            var points = new List<GeoPoint2>(polygon.Vertices);

            if ((polygon.SignedArea > 0.0) != counterClockwise)
            {
                points.Reverse();
            }

            return "(" + Points(points, true) + ")";
        }

        private static string Points(IReadOnlyList<GeoPoint2> points, bool closed)
        {
            var text = new StringBuilder();

            for (int i = 0; i < points.Count; i++)
            {
                text.Append(i == 0 ? string.Empty : ", ").Append(Xy(points[i]));
            }

            if (closed && points.Count > 0)
            {
                text.Append(", ").Append(Xy(points[0]));
            }

            return text.ToString();
        }

        private static string Points(IReadOnlyList<GeoPoint3> points, bool closed)
        {
            var text = new StringBuilder();

            for (int i = 0; i < points.Count; i++)
            {
                text.Append(i == 0 ? string.Empty : ", ").Append(Xyz(points[i]));
            }

            if (closed && points.Count > 0)
            {
                text.Append(", ").Append(Xyz(points[0]));
            }

            return text.ToString();
        }

        private static string Xy(GeoPoint2 p) => Number(p.X) + " " + Number(p.Y);

        private static string Xyz(GeoPoint3 p) => Number(p.X) + " " + Number(p.Y) + " " + Number(p.Z);

        private static string Number(double value) => Numbers.Exact(value);

        private static T NotNull<T>(T value, string name) where T : class
            => value ?? throw new ArgumentNullException(name);
    }
}
