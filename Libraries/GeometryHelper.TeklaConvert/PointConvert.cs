using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.PlaneGeometry.Geometry;
using TSG = Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Converts geometry between Tekla Structures and GeometryHelper.SolidGeometry.
    /// <para>
    /// The two libraries describe the same shapes and disagree only about names and about how much they
    /// insist on. Tekla hands back what its modeller happens to hold; SolidGeometry asks for flatness,
    /// for a closed boundary, and for normals that point out of the body. The conversions here do that
    /// checking rather than trusting it, because a body that only looks right measures wrong later and
    /// says nothing about why.
    /// </para>
    /// <para>
    /// Tekla models in millimetres with coordinates that can run to hundreds of thousands, and a face of
    /// a twelve metre member is rarely flat to the last decimal. The default tolerance is often too tight
    /// for that, so every conversion that needs one takes it, and the overloads without one read
    /// <see cref="Tolerance.Global"/> as everywhere else.
    /// </para>
    /// </summary>
    public static class PointConvert
    {
        /// <summary>
        /// Converts a Tekla point to a SolidGeometry 2D point (discarding the Z coordinate).
        /// </summary>
        /// <param name="point">The Tekla point to convert.</param>
        /// <returns>The converted <see cref="GeoPoint2"/>.</returns>
        public static GeoPoint2 ToGeoPoint2(this TSG.Point point) => new GeoPoint2(point.X, point.Y);

        /// <summary>
        /// Converts a sequence of Tekla points to a list of SolidGeometry 2D points (discarding their Z coordinates).
        /// </summary>
        /// <param name="points">The sequence of Tekla points to convert.</param>
        /// <returns>A list of converted <see cref="GeoPoint2"/>.</returns>
        public static List<GeoPoint2> ToGeoPoint2(this IEnumerable<TSG.Point> points) => points.Select(ToGeoPoint2).ToList();

        /// <summary>
        /// Converts a Tekla point to a SolidGeometry point.
        /// </summary>
        /// <param name="point">The Tekla point to convert.</param>
        /// <returns>The converted <see cref="GeoPoint3"/>.</returns>
        public static GeoPoint3 ToGeoPoint3(this TSG.Point point) => new GeoPoint3(point.X, point.Y, point.Z);

        /// <summary>
        /// Converts a sequence of Tekla points to a list of SolidGeometry points.
        /// </summary>
        /// <param name="points">The sequence of Tekla points to convert.</param>
        /// <returns>A list of converted <see cref="GeoPoint3"/>.</returns>
        public static List<GeoPoint3> ToGeoPoint3(this IEnumerable<TSG.Point> points) => points.Select(ToGeoPoint3).ToList();

        /// <summary>
        /// Converts a SolidGeometry 2D point to a Tekla point (with Z = 0).
        /// </summary>
        /// <param name="point">The SolidGeometry 2D point to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.Point"/>.</returns>
        public static TSG.Point ToTeklaPoint(this GeoPoint2 point) => new TSG.Point(point.X, point.Y, 0.0);

        /// <summary>
        /// Converts a sequence of SolidGeometry 2D points to a list of Tekla points (with Z = 0).
        /// </summary>
        /// <param name="points">The sequence of SolidGeometry 2D points to convert.</param>
        /// <returns>A list of converted Tekla <see cref="TSG.Point"/>.</returns>
        public static List<TSG.Point> ToTeklaPoint(this IEnumerable<GeoPoint2> points) => points.Select(ToTeklaPoint).ToList();

        /// <summary>
        /// Converts a SolidGeometry point to a Tekla point.
        /// </summary>
        /// <param name="point">The SolidGeometry point to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.Point"/>.</returns>
        public static TSG.Point ToTeklaPoint(this GeoPoint3 point) => new TSG.Point(point.X, point.Y, point.Z);

        /// <summary>
        /// Converts a sequence of SolidGeometry points to a list of Tekla points.
        /// </summary>
        /// <param name="points">The sequence of SolidGeometry points to convert.</param>
        /// <returns>A list of converted Tekla <see cref="TSG.Point"/>.</returns>
        public static List<TSG.Point> ToTeklaPoint(this IEnumerable<GeoPoint3> points) => points.Select(ToTeklaPoint).ToList();

        /// <summary>
        /// Converts a run of Tekla points into a chain.
        /// </summary>
        /// <param name="points">The run of Tekla points to convert.</param>
        /// <returns>The converted <see cref="GeoPolyline3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct points remain.</exception>
        public static GeoPolyline3 ToGeoPolyline3(this IEnumerable<TSG.Point> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            List<GeoPoint3> vertices = new List<GeoPoint3>();

            foreach (TSG.Point point in points)
            {
                vertices.Add(point.ToGeoPoint3());
            }

            return new GeoPolyline3(vertices);
        }

        /// <summary>
        /// Converts a run of Tekla points into a polygon.
        /// </summary>
        /// <param name="points">The run of Tekla points to convert.</param>
        /// <returns>The converted <see cref="GeoPolygon3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static GeoPolygon3 ToGeoPolygon3(this IEnumerable<TSG.Point> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            List<GeoPoint3> vertices = new List<GeoPoint3>();

            foreach (TSG.Point point in points)
            {
                vertices.Add(point.ToGeoPoint3());
            }

            return new GeoPolygon3(vertices);
        }
    }
}
