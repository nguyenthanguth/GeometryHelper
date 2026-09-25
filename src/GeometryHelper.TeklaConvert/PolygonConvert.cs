using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert Tekla model polygons to GeometryHelper geometry.
    /// </summary>
    /// <remarks>
    /// A Tekla <c>Polygon</c> is a run of points and nothing more — it carries no flag saying whether it
    /// closes — so it converts to a <see cref="GeoPolyline3"/> unless it is asked for a loop. It is the shape
    /// every kind of reinforcement is set out by, which is why the reading lives here rather than being
    /// written out again for each of them.
    /// </remarks>
    public static class PolygonConvert
    {
        /// <summary>
        /// Gets the points of a Tekla polygon.
        /// </summary>
        /// <param name="polygon">The Tekla polygon.</param>
        /// <returns>Its points, in order; anything in the list that is not a point is passed over.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polygon"/> is null.</exception>
        public static List<TSG.Point> ToTeklaPoints(this TSM.Polygon polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return PointConvert.ReadTeklaPoints(polygon.Points, nameof(polygon));
        }

        /// <summary>
        /// Converts a Tekla polygon to a GeometryHelper chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polygon"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct points are given.</exception>
        public static GeoPolyline3 ToGeoPolyline3(this TSM.Polygon polygon)
        {
            return polygon.ToTeklaPoints().ToGeoPolyline3();
        }

        /// <summary>
        /// Converts a Tekla polygon to a GeometryHelper loop.
        /// </summary>
        public static GeoPolygon3 ToGeoPolygon3(this TSM.Polygon polygon) => polygon.ToGeoPolygon3(Tolerance.Global);

        /// <summary>
        /// Converts a Tekla polygon to a GeometryHelper loop, within a tolerance.
        /// </summary>
        /// <param name="polygon">The Tekla polygon.</param>
        /// <param name="tolerance">The tolerance; the planar threshold decides how flat is flat enough.</param>
        /// <returns>The loop.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polygon"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than three distinct points are given, or they do not lie in one plane.</exception>
        /// <remarks>
        /// A loop encloses an area, so its points have to be flat. Tekla coordinates run large and a run typed
        /// in by hand is rarely flat to the last decimal, which is why the tolerance is worth giving.
        /// </remarks>
        public static GeoPolygon3 ToGeoPolygon3(this TSM.Polygon polygon, Tolerance tolerance)
        {
            return polygon.ToTeklaPoints().ToGeoPolygon3(tolerance);
        }

        /// <summary>
        /// Converts a Tekla polygon to a chain that may curve, bending it at each turn.
        /// </summary>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSM.Polygon polygon, IList<double> bendingRadii)
            => polygon.ToGeoPolylineArc3(bendingRadii, Tolerance.Global);

        /// <summary>
        /// Converts a Tekla polygon to a chain that may curve, bending it at each turn, within a tolerance.
        /// </summary>
        /// <param name="polygon">The Tekla polygon the thing is set out by.</param>
        /// <param name="bendingRadii">A radius for each bend, in order, the first belonging to the first point that turns.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain: straight runs with a tangent arc at every bend that had room for one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polygon"/> is null.</exception>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSM.Polygon polygon, IList<double> bendingRadii, Tolerance tolerance)
        {
            return polygon.ToTeklaPoints().ToGeoPolylineArc3(bendingRadii, tolerance);
        }

        /// <summary>
        /// Converts a Tekla polygon to a closed loop that may curve, bending it at each corner.
        /// </summary>
        public static GeoPolygonArc3 ToGeoPolygonArc3(this TSM.Polygon polygon, double bendingRadius)
            => polygon.ToGeoPolygonArc3(bendingRadius, Tolerance.Global);

        /// <summary>
        /// Converts a Tekla polygon to a closed loop that may curve, bending it at each corner, within a tolerance.
        /// </summary>
        /// <param name="polygon">The Tekla polygon the loop is set out by.</param>
        /// <param name="bendingRadius">The radius of every corner; nought leaves them all square.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The loop with its corners rounded.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polygon"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the points are not flat, since a loop encloses an area.</exception>
        /// <remarks>
        /// This is the shape a closed tie is: set out by its corners, bent to a radius. The corner closing the
        /// loop is rounded like every other, which is what tells a loop from a chain whose ends happen to meet.
        /// </remarks>
        public static GeoPolygonArc3 ToGeoPolygonArc3(this TSM.Polygon polygon, double bendingRadius, Tolerance tolerance)
        {
            var loop = new GeoPolygonArc3(polygon.ToGeoPolygon3(tolerance));

            return bendingRadius > 0.0 ? loop.Fillet(bendingRadius, tolerance) : loop;
        }

        /// <summary>
        /// Converts a GeometryHelper chain to a Tekla polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static TSM.Polygon ToTeklaPolygon(this GeoPolyline3 polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return Built(polyline.Vertices);
        }

        /// <summary>
        /// Converts a GeometryHelper loop to a Tekla polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polygon"/> is null.</exception>
        public static TSM.Polygon ToTeklaPolygon(this GeoPolygon3 polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return Built(polygon.Vertices);
        }

        /// <summary>
        /// Builds a Tekla polygon from a run of points.
        /// </summary>
        private static TSM.Polygon Built(IReadOnlyList<GeoPoint3> vertices)
        {
            var polygon = new TSM.Polygon();

            foreach (GeoPoint3 vertex in vertices)
            {
                polygon.Points.Add(vertex.ToTeklaPoint());
            }

            return polygon;
        }
    }
}
