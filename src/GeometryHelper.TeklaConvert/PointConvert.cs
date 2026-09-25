using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using TSG = Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Converts geometry between Tekla Structures and GeometryHelper.
    /// <para>
    /// The two libraries describe the same shapes and disagree only about names and about how much they
    /// insist on. Tekla hands back what its modeller happens to hold; GeometryHelper asks for flatness,
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
        /// Converts a Tekla point to a GeometryHelper 2D point (discarding the Z coordinate).
        /// </summary>
        /// <param name="point">The Tekla point to convert.</param>
        /// <returns>The converted <see cref="GeoPoint2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="point"/> is null.</exception>
        public static GeoPoint2 ToGeoPoint2(this TSG.Point point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            return new GeoPoint2(point.X, point.Y);
        }

        /// <summary>
        /// Converts a sequence of Tekla points to a list of GeometryHelper 2D points (discarding their Z coordinates).
        /// </summary>
        /// <param name="points">The sequence of Tekla points to convert.</param>
        /// <returns>A list of converted <see cref="GeoPoint2"/>.</returns>
        public static List<GeoPoint2> ToGeoPoint2(this IEnumerable<TSG.Point> points) => points.Select(ToGeoPoint2).ToList();

        /// <summary>
        /// Converts a Tekla point to a GeometryHelper point.
        /// </summary>
        /// <param name="point">The Tekla point to convert.</param>
        /// <returns>The converted <see cref="GeoPoint3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="point"/> is null.</exception>
        public static GeoPoint3 ToGeoPoint3(this TSG.Point point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            return new GeoPoint3(point.X, point.Y, point.Z);
        }

        /// <summary>
        /// Converts a sequence of Tekla points to a list of GeometryHelper points.
        /// </summary>
        /// <param name="points">The sequence of Tekla points to convert.</param>
        /// <returns>A list of converted <see cref="GeoPoint3"/>.</returns>
        public static List<GeoPoint3> ToGeoPoint3(this IEnumerable<TSG.Point> points) => points.Select(ToGeoPoint3).ToList();

        /// <summary>
        /// Converts a GeometryHelper 2D point to a Tekla point (with Z = 0).
        /// </summary>
        /// <param name="point">The GeometryHelper 2D point to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.Point"/>.</returns>
        public static TSG.Point ToTeklaPoint(this GeoPoint2 point) => new TSG.Point(point.X, point.Y, 0.0);

        /// <summary>
        /// Converts a sequence of GeometryHelper 2D points to a list of Tekla points (with Z = 0).
        /// </summary>
        /// <param name="points">The sequence of GeometryHelper 2D points to convert.</param>
        /// <returns>A list of converted Tekla <see cref="TSG.Point"/>.</returns>
        public static List<TSG.Point> ToTeklaPoint(this IEnumerable<GeoPoint2> points) => points.Select(ToTeklaPoint).ToList();

        /// <summary>
        /// Converts a GeometryHelper point to a Tekla point.
        /// </summary>
        /// <param name="point">The GeometryHelper point to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.Point"/>.</returns>
        public static TSG.Point ToTeklaPoint(this GeoPoint3 point) => new TSG.Point(point.X, point.Y, point.Z);

        /// <summary>
        /// Converts a sequence of GeometryHelper points to a list of Tekla points.
        /// </summary>
        /// <param name="points">The sequence of GeometryHelper points to convert.</param>
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

            return new GeoPolyline3(points.ToGeoPoint3());
        }

        /// <summary>
        /// Converts a run of Tekla points into a chain that may curve, bending it at each turn.
        /// </summary>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this IEnumerable<TSG.Point> points, IList<double> bendingRadii)
            => points.ToGeoPolylineArc3(bendingRadii, Tolerance.Global);

        /// <summary>
        /// Converts a run of Tekla points into a chain that may curve, bending it at each turn, within a tolerance.
        /// </summary>
        /// <param name="points">The points the thing is set out by, in order; at least two.</param>
        /// <param name="bendingRadii">
        /// A radius for each bend, in order, the first belonging to the first point that turns. A list as long
        /// as the points is read the same way, with the entries for the two ends ignored, because nothing
        /// turns at either end. A radius of nought, a short list, or no list at all leaves that bend square.
        /// </param>
        /// <param name="tolerance">The tolerance; Tekla coordinates run large, so this is worth giving.</param>
        /// <returns>The chain: straight runs with a tangent arc at every bend that had room for one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct points are given, or one of them is null.</exception>
        /// <remarks>
        /// This is what a set-out becomes: a reinforcing bar is not the polyline it is typed as, it is that
        /// polyline with a tangent arc at every bend, so it is shorter than its set-out and does not pass
        /// through its own corners. A bend with too little straight run either side to fit its radius is left
        /// square rather than forced, and where two bends want more of the run between them than it is long,
        /// the one taking more of it gives way. Both are reported through <c>GeometryHelperLog</c> at debug
        /// level.
        /// </remarks>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this IEnumerable<TSG.Point> points, IList<double> bendingRadii, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var chain = new GeoPolyline3(Required(points));

            if (bendingRadii == null || bendingRadii.Count == 0)
            {
                return new GeoPolylineArc3(chain);
            }

            return chain.Fillet(ByVertex(bendingRadii, chain.VertexCount), tolerance);
        }

        /// <summary>
        /// Converts a run of Tekla points into a 2D chain (discarding Z coordinates).
        /// </summary>
        /// <param name="points">The run of Tekla points to convert.</param>
        /// <returns>The converted <see cref="GeoPolyline2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct points remain.</exception>
        public static GeoPolyline2 ToGeoPolyline2(this IEnumerable<TSG.Point> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            return new GeoPolyline2(points.ToGeoPoint2());
        }

        /// <summary>
        /// Converts a run of Tekla points into a polygon.
        /// </summary>
        /// <param name="points">The run of Tekla points to convert.</param>
        /// <returns>The converted <see cref="GeoPolygon3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than three distinct vertices remain, when they are collinear, or when they do not lie on a common plane.</exception>
        public static GeoPolygon3 ToGeoPolygon3(this IEnumerable<TSG.Point> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            return new GeoPolygon3(points.ToGeoPoint3());
        }

        /// <summary>
        /// Converts a run of Tekla points into a polygon, within a tolerance.
        /// </summary>
        /// <param name="points">The run of Tekla points to convert.</param>
        /// <param name="tolerance">The tolerance deciding duplicate vertices and flatness.</param>
        /// <returns>The converted <see cref="GeoPolygon3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than three distinct vertices remain, when they are collinear, or when they do not lie on a common plane.</exception>
        public static GeoPolygon3 ToGeoPolygon3(this IEnumerable<TSG.Point> points, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            return new GeoPolygon3(points.ToGeoPoint3(), tolerance);
        }

        /// <summary>
        /// Converts a run of Tekla points into a 2D polygon (discarding Z coordinates).
        /// </summary>
        /// <param name="points">The run of Tekla points to convert.</param>
        /// <returns>The converted <see cref="GeoPolygon2"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than three distinct vertices remain.</exception>
        public static GeoPolygon2 ToGeoPolygon2(this IEnumerable<TSG.Point> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            return new GeoPolygon2(points.ToGeoPoint2());
        }

        /// <summary>
        /// Reads a Tekla list of points, which is untyped and may hold anything.
        /// </summary>
        /// <remarks>
        /// Tekla keeps points in an <c>ArrayList</c>, so anything that is not a point is passed over rather
        /// than stopping the read. A list that is not there at all is a different matter and is refused.
        /// </remarks>
        internal static List<TSG.Point> ReadTeklaPoints(System.Collections.ArrayList points, string name)
        {
            if (points == null)
            {
                throw new ArgumentException("There are no points to read.", name);
            }

            var kept = new List<TSG.Point>(points.Count);

            foreach (object item in points)
            {
                if (item is TSG.Point point)
                {
                    kept.Add(point);
                }
            }

            return kept;
        }

        /// <summary>
        /// Reads a Tekla list of radii, which is untyped and may hold nothing at all.
        /// </summary>
        /// <remarks>
        /// A radius that is not a positive number leaves that bend square rather than stopping the read.
        /// </remarks>
        internal static List<double> ReadTeklaRadii(System.Collections.ArrayList radii)
        {
            var kept = new List<double>();

            if (radii == null)
            {
                return kept;
            }

            foreach (object item in radii)
            {
                kept.Add(item is double radius && !double.IsNaN(radius) && radius > 0.0 ? radius : 0.0);
            }

            return kept;
        }

        /// <summary>
        /// Gets the points of a run, refusing one that is not there.
        /// </summary>
        private static List<GeoPoint3> Required(IEnumerable<TSG.Point> points)
        {
            var kept = new List<GeoPoint3>();

            foreach (TSG.Point point in points)
            {
                if (point == null)
                {
                    throw new ArgumentException("A chain cannot be set out through a point that is not there.", nameof(points));
                }

                kept.Add(point.ToGeoPoint3());
            }

            return kept;
        }

        /// <summary>
        /// Gets the radius wanted at each vertex, from a list of radii given one per bend.
        /// </summary>
        /// <remarks>
        /// Tekla gives a radius for each bend, and a run of n points has n - 2 of them, so the first bend
        /// belongs to the second point. A list already as long as the points is taken as it is, because that
        /// is the layout GeometryHelper uses and a caller who has built one should not have it shifted
        /// underneath them.
        /// </remarks>
        private static double[] ByVertex(IList<double> bendingRadii, int vertexCount)
        {
            if (bendingRadii.Count >= vertexCount)
            {
                var asGiven = new double[bendingRadii.Count];

                for (int i = 0; i < bendingRadii.Count; i++)
                {
                    asGiven[i] = bendingRadii[i];
                }

                return asGiven;
            }

            var byVertex = new double[vertexCount];

            for (int i = 0; i < bendingRadii.Count && i + 1 < vertexCount; i++)
            {
                byVertex[i + 1] = bendingRadii[i];
            }

            return byVertex;
        }
    }
}
