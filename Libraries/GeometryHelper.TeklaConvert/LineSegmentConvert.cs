using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Geometry;
using GeometryHelper.SolidGeometry.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using TSS = Tekla.Structures.Solid;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert line segments between Tekla Structures and GeometryHelper.SolidGeometry.
    /// </summary>
    public static class LineSegmentConvert
    {
        /// <summary>
        /// Converts a Tekla line segment to a PlaneGeometry 2D line segment.
        /// </summary>
        /// <param name="segment">The Tekla line segment to convert.</param>
        /// <returns>The converted <see cref="GeoLine2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segment"/> is null.</exception>
        public static GeoLine2 ToGeoLine2(this TSG.LineSegment segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            return new GeoLine2(segment.StartPoint.ToGeoPoint2(), segment.EndPoint.ToGeoPoint2());
        }

        /// <summary>
        /// Converts a Tekla line segment to a SolidGeometry line segment.
        /// </summary>
        /// <param name="segment">The Tekla line segment to convert.</param>
        /// <returns>The converted <see cref="GeoLine3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segment"/> is null.</exception>
        public static GeoLine3 ToGeoLine3(this TSG.LineSegment segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            return new GeoLine3(segment.StartPoint.ToGeoPoint3(), segment.EndPoint.ToGeoPoint3());
        }

        /// <summary>
        /// Converts a SolidGeometry line segment to a Tekla line segment.
        /// </summary>
        /// <param name="line">The SolidGeometry line segment to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.LineSegment"/>.</returns>
        public static TSG.LineSegment ToTeklaLineSegment(this GeoLine3 line)
        {
            return new TSG.LineSegment(line.StartPoint.ToTeklaPoint(), line.EndPoint.ToTeklaPoint());
        }

        /// <summary>
        /// Converts a PlaneGeometry 2D line segment to a Tekla line segment (with Z = 0).
        /// </summary>
        /// <param name="line">The PlaneGeometry 2D line segment to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.LineSegment"/>.</returns>
        public static TSG.LineSegment ToTeklaLineSegment(this GeoLine2 line)
        {
            return new TSG.LineSegment(line.StartPoint.ToTeklaPoint(), line.EndPoint.ToTeklaPoint());
        }

        /// <summary>
        /// Converts a sequence of Tekla line segments to a list of SolidGeometry line segments.
        /// </summary>
        /// <param name="segments">The sequence of Tekla line segments to convert.</param>
        /// <returns>A list of converted <see cref="GeoLine3"/>.</returns>
        public static List<GeoLine3> ToGeoLine3(this IEnumerable<TSG.LineSegment> segments) => segments.Select(ToGeoLine3).ToList();

        /// <summary>
        /// Converts a sequence of Tekla line segments to a list of PlaneGeometry 2D line segments.
        /// </summary>
        /// <param name="segments">The sequence of Tekla line segments to convert.</param>
        /// <returns>A list of converted <see cref="GeoLine2"/>.</returns>
        public static List<GeoLine2> ToGeoLine2(this IEnumerable<TSG.LineSegment> segments) => segments.Select(ToGeoLine2).ToList();

        /// <summary>
        /// Converts a sequence of SolidGeometry line segments to a list of Tekla line segments.
        /// </summary>
        /// <param name="lines">The sequence of SolidGeometry line segments to convert.</param>
        /// <returns>A list of converted Tekla <see cref="TSG.LineSegment"/>.</returns>
        public static List<TSG.LineSegment> ToTeklaLineSegment(this IEnumerable<GeoLine3> lines) => lines.Select(ToTeklaLineSegment).ToList();

        /// <summary>
        /// Converts a sequence of PlaneGeometry 2D line segments to a list of Tekla line segments.
        /// </summary>
        /// <param name="lines">The sequence of PlaneGeometry 2D line segments to convert.</param>
        /// <returns>A list of converted Tekla <see cref="TSG.LineSegment"/>.</returns>
        public static List<TSG.LineSegment> ToTeklaLineSegment(this IEnumerable<GeoLine2> lines) => lines.Select(ToTeklaLineSegment).ToList();
    }
}
