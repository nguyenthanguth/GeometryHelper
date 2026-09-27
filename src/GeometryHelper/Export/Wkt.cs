using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Writes shapes as well-known text, which GIS tools, databases and online viewers read.
    /// </summary>
    /// <remarks>
    /// Every ring is written closed, its first point repeated at the end, as the format asks. A polygon's
    /// boundary goes counter-clockwise and its holes clockwise. Coordinates are written exactly.
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

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static T NotNull<T>(T value, string name) where T : class
            => value ?? throw new ArgumentNullException(name);
    }
}
