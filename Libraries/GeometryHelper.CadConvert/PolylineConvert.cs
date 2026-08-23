using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper.PlaneGeometry.Geometry;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Converts polylines, circles, and bounding boxes between AutoCAD and GeometryHelper.
    /// </summary>
    public static class PolylineConvert
    {
        /// <summary>
        /// Converts a AutoCAD Extents2d to a SolidGeometry 2D rectangle.
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
        /// Converts a sequence of AutoCAD Extents2d to a list of SolidGeometry 2D rectangles.
        /// </summary>
        /// <param name="extentsList">The sequence of AutoCAD 2D bounding boxes to convert.</param>
        /// <returns>A list of converted <see cref="GeoRectangle2"/>.</returns>
        public static List<GeoRectangle2> ToGeoRectangle2(this IEnumerable<Extents2d> extentsList) => extentsList.Select(ToGeoRectangle2).ToList();

        /// <summary>
        /// Converts a AutoCAD Extents3d to a SolidGeometry 2D rectangle (discarding the Z coordinate).
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
        /// Converts a sequence of AutoCAD Extents3d to a list of SolidGeometry 2D rectangles.
        /// </summary>
        /// <param name="extentsList">The sequence of AutoCAD 3D bounding boxes to convert.</param>
        /// <returns>A list of converted <see cref="GeoRectangle2"/>.</returns>
        public static List<GeoRectangle2> ToGeoRectangle2(this IEnumerable<Extents3d> extentsList) => extentsList.Select(ToGeoRectangle2).ToList();

        /// <summary>
        /// Converts a AutoCAD Polyline to a SolidGeometry 2D polyline.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <returns>The converted <see cref="GeoPolyline2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static GeoPolyline2 ToGeoPolyline2(this Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            var points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                points.Add(polyline.GetPoint2dAt(i).ToGeoPoint2());
            }
            if (polyline.Closed && points.Count > 0)
            {
                points.Add(points[0]);
            }
            return new GeoPolyline2(points);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Polylines to a list of SolidGeometry 2D polylines.
        /// </summary>
        /// <param name="polylines">The sequence of AutoCAD Polylines to convert.</param>
        /// <returns>A list of converted <see cref="GeoPolyline2"/>.</returns>
        public static List<GeoPolyline2> ToGeoPolyline2(this IEnumerable<Polyline> polylines) => polylines.Select(ToGeoPolyline2).ToList();

        /// <summary>
        /// Converts a AutoCAD Polyline to a SolidGeometry 2D polygon.
        /// </summary>
        /// <param name="polyline">The AutoCAD Polyline to convert.</param>
        /// <returns>The converted <see cref="GeoPolygon2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static GeoPolygon2 ToGeoPolygon2(this Polyline polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            var points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                points.Add(polyline.GetPoint2dAt(i).ToGeoPoint2());
            }
            return new GeoPolygon2(points);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Polylines to a list of SolidGeometry 2D polygons.
        /// </summary>
        /// <param name="polylines">The sequence of AutoCAD Polylines to convert.</param>
        /// <returns>A list of converted <see cref="GeoPolygon2"/>.</returns>
        public static List<GeoPolygon2> ToGeoPolygon2(this IEnumerable<Polyline> polylines) => polylines.Select(ToGeoPolygon2).ToList();

        /// <summary>
        /// Converts a AutoCAD Circle to a SolidGeometry 2D circle.
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
        /// Converts a sequence of AutoCAD Circles to a list of SolidGeometry 2D circles.
        /// </summary>
        /// <param name="circles">The sequence of AutoCAD Circles to convert.</param>
        /// <returns>A list of converted <see cref="GeoCircle2"/>.</returns>
        public static List<GeoCircle2> ToGeoCircle2(this IEnumerable<Circle> circles) => circles.Select(ToGeoCircle2).ToList();

        /// <summary>
        /// Converts a SolidGeometry 2D polyline to a AutoCAD Polyline.
        /// </summary>
        /// <param name="geoPolyline">The SolidGeometry 2D polyline to convert.</param>
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
        /// Converts a sequence of SolidGeometry 2D polylines to a list of AutoCAD Polylines.
        /// </summary>
        /// <param name="polylines">The sequence of SolidGeometry 2D polylines to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Polyline"/>.</returns>
        public static List<Polyline> ToAcadPolyline(this IEnumerable<GeoPolyline2> polylines) => polylines.Select(ToAcadPolyline).ToList();

        /// <summary>
        /// Tries to convert a 4-vertex closed AutoCAD Polyline to a SolidGeometry 2D rectangle.
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
