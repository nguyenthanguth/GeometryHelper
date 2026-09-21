using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Converts polylines, circles, and bounding boxes between AutoCAD and GeometryHelper.
    /// </summary>
    public static class PolylineConvert
    {
        /// <summary>
        /// Converts a AutoCAD Extents2d to a GeometryHelper 2D rectangle.
        /// </summary>
        /// <param name="extents">The AutoCAD 2D bounding box to convert.</param>
        /// <returns>The converted <see cref="GeoRectangle2"/>.</returns>
        public static GeoRectangle2 ToGeoRectangle2(this Extents2d extents)
        {
            double width = extents.MaxPoint.X - extents.MinPoint.X;
            double height = extents.MaxPoint.Y - extents.MinPoint.Y;
            var center = new GeoPoint2((extents.MinPoint.X + extents.MaxPoint.X) / 2.0, (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
            return new GeoRectangle2(center, width, height, 0.0);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Extents2d to a list of GeometryHelper 2D rectangles.
        /// </summary>
        /// <param name="extentsList">The sequence of AutoCAD 2D bounding boxes to convert.</param>
        /// <returns>A list of converted <see cref="GeoRectangle2"/>.</returns>
        public static List<GeoRectangle2> ToGeoRectangle2(this IEnumerable<Extents2d> extentsList) => extentsList.Select(ToGeoRectangle2).ToList();

        /// <summary>
        /// Converts a AutoCAD Extents3d to a GeometryHelper 2D rectangle (discarding the Z coordinate).
        /// </summary>
        /// <param name="extents">The AutoCAD 3D bounding box to convert.</param>
        /// <returns>The converted <see cref="GeoRectangle2"/>.</returns>
        public static GeoRectangle2 ToGeoRectangle2(this Extents3d extents)
        {
            double width = extents.MaxPoint.X - extents.MinPoint.X;
            double height = extents.MaxPoint.Y - extents.MinPoint.Y;
            var center = new GeoPoint2((extents.MinPoint.X + extents.MaxPoint.X) / 2.0, (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
            return new GeoRectangle2(center, width, height, 0.0);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Extents3d to a list of GeometryHelper 2D rectangles.
        /// </summary>
        /// <param name="extentsList">The sequence of AutoCAD 3D bounding boxes to convert.</param>
        /// <returns>A list of converted <see cref="GeoRectangle2"/>.</returns>
        public static List<GeoRectangle2> ToGeoRectangle2(this IEnumerable<Extents3d> extentsList) => extentsList.Select(ToGeoRectangle2).ToList();


        /// <summary>
        /// Counts the segments of an AutoCAD polyline that are arcs.
        /// </summary>
        /// <remarks>
        /// An AutoCAD polyline carries a bulge per vertex, and a vertex with a bulge is joined to the next
        /// one by an arc rather than by a straight segment. A closed polyline has as many segments as
        /// vertices; an open one has one fewer, and the bulge stored against its last vertex belongs to no
        /// segment.
        /// </remarks>
        private static int CountArcs(Polyline polyline)
        {
            int segments = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
            int arcs = 0;

            for (int i = 0; i < segments; i++)
            {
                if (polyline.GetBulgeAt(i) != 0.0)
                {
                    arcs++;
                }
            }

            return arcs;
        }

        /// <summary>
        /// Reads the vertices of an AutoCAD polyline, with the bulge of the segment leaving each one.
        /// </summary>
        private static void ReadVertices(Polyline polyline, out List<GeoPoint2> points, out List<double> bulges)
        {
            points = new List<GeoPoint2>(polyline.NumberOfVertices);
            bulges = new List<double>(polyline.NumberOfVertices);

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                points.Add(polyline.GetPoint2dAt(i).ToGeoPoint2());
                bulges.Add(polyline.GetBulgeAt(i));
            }
        }

        /// <summary>
        /// Converts an AutoCAD Polyline to a GeometryHelper 2D chain, keeping its arcs.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <returns>The converted <see cref="GeoPolylineArc2"/>, bulge for bulge.</returns>
        /// <remarks>
        /// This is the reading that loses nothing: the bulge AutoCAD stores against each vertex is the bulge
        /// of the edge leaving it, and <see cref="GeoPolylineArc2"/> holds it the same way, so the round trip
        /// through <see cref="ToAcadPolyline(GeoPolylineArc2)"/> is exact. A closed polyline comes back as a
        /// chain that returns to where it began; <see cref="ToGeoPolygonArc2(Polyline)"/> reads it as a loop instead.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static GeoPolylineArc2 ToGeoPolylineArc2(this Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            ReadVertices(polyline, out List<GeoPoint2> points, out List<double> bulges);

            if (polyline.Closed && points.Count > 0)
            {
                points.Add(points[0]);
                bulges.Add(0.0);
            }
            else if (bulges.Count > 0)
            {
                // The bulge against the last vertex of an open polyline belongs to no segment.
                bulges[bulges.Count - 1] = 0.0;
            }

            return new GeoPolylineArc2(points, bulges);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Polylines to a list of GeometryHelper 2D chains, keeping their arcs.
        /// </summary>
        /// <param name="polylines">The sequence of AutoCAD Polylines to convert.</param>
        /// <returns>A list of converted <see cref="GeoPolylineArc2"/>.</returns>
        public static List<GeoPolylineArc2> ToGeoPolylineArc2(this IEnumerable<Polyline> polylines) => polylines.Select(ToGeoPolylineArc2).ToList();

        /// <summary>
        /// Converts an AutoCAD Polyline to a GeometryHelper 2D loop, keeping its arcs.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <returns>The converted <see cref="GeoPolygonArc2"/>, bulge for bulge.</returns>
        /// <remarks>
        /// A loop closes itself, so the last vertex is joined back to the first whether or not the polyline
        /// said it was closed, and the bulge stored against that last vertex is the bulge of the closing
        /// edge.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static GeoPolygonArc2 ToGeoPolygonArc2(this Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            ReadVertices(polyline, out List<GeoPoint2> points, out List<double> bulges);

            return new GeoPolygonArc2(points, bulges);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Polylines to a list of GeometryHelper 2D loops, keeping their arcs.
        /// </summary>
        /// <param name="polylines">The sequence of AutoCAD Polylines to convert.</param>
        /// <returns>A list of converted <see cref="GeoPolygonArc2"/>.</returns>
        public static List<GeoPolygonArc2> ToGeoPolygonArc2(this IEnumerable<Polyline> polylines) => polylines.Select(ToGeoPolygonArc2).ToList();

        /// <summary>
        /// Converts a AutoCAD Polyline to a GeometryHelper 2D polyline, cutting any arcs into straight
        /// pieces.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <returns>The converted <see cref="GeoPolyline2"/>.</returns>
        /// <remarks>
        /// <see cref="GeoPolyline2"/> is a chain of straight segments, so an arc has to become several of
        /// them. Each is cut finely enough to stray no further from its arc than the automatic share of the
        /// radius, about a fifth of a percent, and how many were cut is written to
        /// <see cref="GeometryHelperLog"/>. Use <see cref="ToGeoPolyline2(Polyline, double)"/> to say how
        /// fine, <see cref="ToGeoPolylineArc2(Polyline)"/> to keep the arcs, or
        /// <see cref="TryToGeoPolyline2(Polyline, out GeoPolyline2)"/> to refuse a polyline that has any.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static GeoPolyline2 ToGeoPolyline2(this Polyline polyline) => polyline.ToGeoPolyline2(0.0);

        /// <summary>
        /// Converts a AutoCAD Polyline to a GeometryHelper 2D polyline, cutting any arcs into straight pieces
        /// no further than a chord tolerance from the curve.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The converted <see cref="GeoPolyline2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public static GeoPolyline2 ToGeoPolyline2(this Polyline polyline, double chordTolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            GeoPolylineArc2 chain = polyline.ToGeoPolylineArc2();

            if (!chain.HasArcs)
            {
                return chain.Flatten();
            }

            GeoPolyline2 flat = chain.Flatten(chordTolerance);

            GeometryHelperLog.Debug(
                $"The AutoCAD polyline has {CountArcs(polyline)} arc segment(s); they were cut into straight pieces, " +
                $"{chain.VertexCount} vertices becoming {flat.VertexCount}.");

            return flat;
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Polylines to a list of GeometryHelper 2D polylines.
        /// </summary>
        /// <param name="polylines">The sequence of AutoCAD Polylines to convert.</param>
        /// <returns>A list of converted <see cref="GeoPolyline2"/>.</returns>
        public static List<GeoPolyline2> ToGeoPolyline2(this IEnumerable<Polyline> polylines) => polylines.Select(polyline => polyline.ToGeoPolyline2()).ToList();

        /// <summary>
        /// Converts a AutoCAD Polyline to a GeometryHelper 2D polygon, cutting any arcs into straight pieces.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <returns>The converted <see cref="GeoPolygon2"/>.</returns>
        /// <remarks>
        /// The arcs are cut as <see cref="ToGeoPolyline2(Polyline)"/> cuts them. Their chords lie inside
        /// them, so a shape bulging outward encloses a little less than the drawing does and one bulging
        /// inward a little more.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static GeoPolygon2 ToGeoPolygon2(this Polyline polyline) => polyline.ToGeoPolygon2(0.0);

        /// <summary>
        /// Converts a AutoCAD Polyline to a GeometryHelper 2D polygon, cutting any arcs into straight pieces
        /// no further than a chord tolerance from the curve.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc it replaces, in drawing units. Zero picks the automatic share of each radius.</param>
        /// <returns>The converted <see cref="GeoPolygon2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public static GeoPolygon2 ToGeoPolygon2(this Polyline polyline, double chordTolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            GeoPolygonArc2 loop = polyline.ToGeoPolygonArc2();

            if (!loop.HasArcs)
            {
                return loop.Flatten();
            }

            GeoPolygon2 flat = loop.Flatten(chordTolerance);

            GeometryHelperLog.Debug(
                $"The AutoCAD polyline has {CountArcs(polyline)} arc segment(s); they were cut into straight pieces, " +
                $"{loop.VertexCount} vertices becoming {flat.VertexCount}.");

            return flat;
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Polylines to a list of GeometryHelper 2D polygons.
        /// </summary>
        /// <param name="polylines">The sequence of AutoCAD Polylines to convert.</param>
        /// <returns>A list of converted <see cref="GeoPolygon2"/>.</returns>
        public static List<GeoPolygon2> ToGeoPolygon2(this IEnumerable<Polyline> polylines) => polylines.Select(polyline => polyline.ToGeoPolygon2()).ToList();

        /// <summary>
        /// Tries to convert a AutoCAD Polyline to a GeometryHelper 2D polyline without approximating
        /// anything.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <param name="result">The converted <see cref="GeoPolyline2"/>, or null when the polyline carries an arc.</param>
        /// <returns>true when the polyline was straight throughout; false when it has an arc, which a straight chain cannot hold.</returns>
        public static bool TryToGeoPolyline2(this Polyline polyline, out GeoPolyline2 result)
        {
            result = null;

            if (polyline == null || CountArcs(polyline) > 0)
            {
                return false;
            }

            result = polyline.ToGeoPolyline2();
            return true;
        }

        /// <summary>
        /// Tries to convert a AutoCAD Polyline to a GeometryHelper 2D polygon without approximating anything.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <param name="result">The converted <see cref="GeoPolygon2"/>, or null when the polyline carries an arc.</param>
        /// <returns>true when the polyline was straight throughout; false when it has an arc, which a straight polygon cannot hold.</returns>
        public static bool TryToGeoPolygon2(this Polyline polyline, out GeoPolygon2 result)
        {
            result = null;

            if (polyline == null || CountArcs(polyline) > 0)
            {
                return false;
            }

            result = polyline.ToGeoPolygon2();
            return true;
        }


        /// <summary>
        /// Converts a AutoCAD Circle to a GeometryHelper 2D circle.
        /// </summary>
        /// <param name="circle">The AutoCAD Circle to convert.</param>
        /// <returns>The converted <see cref="GeoCircle2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="circle"/> is null.</exception>
        public static GeoCircle2 ToGeoCircle2(this Circle circle)
        {
            if (circle == null) throw new ArgumentNullException(nameof(circle));
            return new GeoCircle2(circle.Center.ToGeoPoint2(), circle.Radius);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Circles to a list of GeometryHelper 2D circles.
        /// </summary>
        /// <param name="circles">The sequence of AutoCAD Circles to convert.</param>
        /// <returns>A list of converted <see cref="GeoCircle2"/>.</returns>
        public static List<GeoCircle2> ToGeoCircle2(this IEnumerable<Circle> circles) => circles.Select(ToGeoCircle2).ToList();

        /// <summary>
        /// Converts a GeometryHelper 2D polyline to a AutoCAD Polyline.
        /// </summary>
        /// <param name="geoPolyline">The GeometryHelper 2D polyline to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Polyline"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="geoPolyline"/> is null.</exception>
        public static Polyline ToAcadPolyline(this GeoPolyline2 geoPolyline)
        {
            if (geoPolyline == null) throw new ArgumentNullException(nameof(geoPolyline));
            var result = new Polyline();
            for (int i = 0; i < geoPolyline.VertexCount; i++)
            {
                result.AddVertexAt(i, new Point2d(geoPolyline.Vertices[i].X, geoPolyline.Vertices[i].Y), 0.0, 0.0, 0.0);
            }
            return result;
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper 2D polylines to a list of AutoCAD Polylines.
        /// </summary>
        /// <param name="polylines">The sequence of GeometryHelper 2D polylines to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Polyline"/>.</returns>
        public static List<Polyline> ToAcadPolyline(this IEnumerable<GeoPolyline2> polylines) => polylines.Select(ToAcadPolyline).ToList();

        /// <summary>
        /// Converts a GeometryHelper 2D polygon to a closed AutoCAD Polyline.
        /// </summary>
        /// <param name="geoPolygon">The GeometryHelper 2D polygon to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Polyline"/>, closed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="geoPolygon"/> is null.</exception>
        public static Polyline ToAcadPolyline(this GeoPolygon2 geoPolygon)
        {
            if (geoPolygon == null) throw new ArgumentNullException(nameof(geoPolygon));

            var result = new Polyline();

            for (int i = 0; i < geoPolygon.VertexCount; i++)
            {
                result.AddVertexAt(i, new Point2d(geoPolygon.Vertices[i].X, geoPolygon.Vertices[i].Y), 0.0, 0.0, 0.0);
            }

            result.Closed = true;
            return result;
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper 2D polygons to a list of closed AutoCAD Polylines.
        /// </summary>
        /// <param name="polygons">The sequence of GeometryHelper 2D polygons to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Polyline"/>.</returns>
        public static List<Polyline> ToAcadPolyline(this IEnumerable<GeoPolygon2> polygons) => polygons.Select(ToAcadPolyline).ToList();

        /// <summary>
        /// Converts a GeometryHelper 2D chain that may curve to an AutoCAD Polyline, arcs and all.
        /// </summary>
        /// <param name="chain">The GeometryHelper 2D chain to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Polyline"/>.</returns>
        /// <remarks>
        /// The bulge of each edge is written against the vertex it leaves, which is where AutoCAD keeps it,
        /// so nothing is approximated and reading the result back with <see cref="ToGeoPolylineArc2(Polyline)"/> gives
        /// the chain that went in.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="chain"/> is null.</exception>
        public static Polyline ToAcadPolyline(this GeoPolylineArc2 chain)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            var result = new Polyline();

            for (int i = 0; i < chain.VertexCount; i++)
            {
                double bulge = i < chain.EdgeCount ? chain.GetBulgeAt(i) : 0.0;

                result.AddVertexAt(i, new Point2d(chain[i].X, chain[i].Y), bulge, 0.0, 0.0);
            }

            return result;
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper 2D chains that may curve to a list of AutoCAD Polylines.
        /// </summary>
        /// <param name="chains">The sequence of GeometryHelper 2D chains to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Polyline"/>.</returns>
        public static List<Polyline> ToAcadPolyline(this IEnumerable<GeoPolylineArc2> chains) => chains.Select(ToAcadPolyline).ToList();

        /// <summary>
        /// Converts a GeometryHelper 2D loop that may curve to a closed AutoCAD Polyline, arcs and all.
        /// </summary>
        /// <param name="loop">The GeometryHelper 2D loop to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Polyline"/>, closed.</returns>
        /// <remarks>
        /// The loop closes itself, so the bulge of its closing edge is written against its last vertex, as
        /// AutoCAD writes the closing segment of a closed polyline.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="loop"/> is null.</exception>
        public static Polyline ToAcadPolyline(this GeoPolygonArc2 loop)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            var result = new Polyline();

            for (int i = 0; i < loop.VertexCount; i++)
            {
                result.AddVertexAt(i, new Point2d(loop[i].X, loop[i].Y), loop.GetBulgeAt(i), 0.0, 0.0);
            }

            result.Closed = true;
            return result;
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper 2D loops that may curve to a list of closed AutoCAD
        /// Polylines.
        /// </summary>
        /// <param name="loops">The sequence of GeometryHelper 2D loops to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Polyline"/>.</returns>
        public static List<Polyline> ToAcadPolyline(this IEnumerable<GeoPolygonArc2> loops) => loops.Select(ToAcadPolyline).ToList();


        /// <summary>
        /// Tries to convert a 4-vertex closed AutoCAD Polyline to a GeometryHelper 2D rectangle.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <param name="rect">The converted <see cref="GeoRectangle2"/>.</param>
        /// <returns>true if the conversion succeeded; false otherwise.</returns>
        public static bool TryToGeoRectangle2(this Polyline polyline, out GeoRectangle2 rect)
        {
            rect = default(GeoRectangle2);
            if (polyline == null) return false;

            if (polyline.NumberOfVertices != 4 || !polyline.Closed)
            {
                return false;
            }

            Point2d p0 = polyline.GetPoint2dAt(0);
            Point2d p1 = polyline.GetPoint2dAt(1);
            Point2d p2 = polyline.GetPoint2dAt(2);
            Point2d p3 = polyline.GetPoint2dAt(3);

            double d01 = p0.GetDistanceTo(p1);
            double d12 = p1.GetDistanceTo(p2);
            double d23 = p2.GetDistanceTo(p3);
            double d30 = p3.GetDistanceTo(p0);

            const double tol = 1e-4;
            if (Math.Abs(d01 - d23) > tol || Math.Abs(d12 - d30) > tol)
            {
                return false;
            }

            Vector2d v01 = p1 - p0;
            Vector2d v12 = p2 - p1;
            double dot = v01.X * v12.X + v01.Y * v12.Y;
            if (Math.Abs(dot) > tol * v01.Length * v12.Length)
            {
                return false;
            }

            Point2d centerAcad = new Point2d((p0.X + p2.X) / 2.0, (p0.Y + p2.Y) / 2.0);
            GeoPoint2 center = centerAcad.ToGeoPoint2();

            double width = d01;
            double height = d12;
            double angle = Math.Atan2(v01.Y, v01.X);

            rect = new GeoRectangle2(center, width, height, angle);
            return true;
        }
    }
}
