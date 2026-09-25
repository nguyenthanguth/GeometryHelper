using System;
using System.Collections;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using TSG = Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert polylines between Tekla Structures and GeometryHelper.
    /// </summary>
    /// <remarks>
    /// A Tekla <c>PolyLine</c> is a run of points and nothing more, so it converts to a
    /// <see cref="GeoPolyline3"/> as it stands. Where those points are the set-out of something that bends —
    /// a reinforcing bar, most of all — <see cref="ToGeoPolylineArc3(TSG.PolyLine, IList{double})"/> puts the
    /// arcs in, because the thing itself is the polyline with a tangent arc at every bend and is shorter than
    /// its set-out.
    /// </remarks>
    public static class PolyLineConvert
    {
        /// <summary>
        /// Converts a Tekla polyline to a GeometryHelper chain.
        /// </summary>
        /// <param name="shape">The Tekla polyline to convert.</param>
        /// <returns>The converted <see cref="GeoPolyline3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="shape"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct points are given.</exception>
        public static GeoPolyline3 ToGeoPolyline3(this TSG.PolyLine shape)
        {
            return ReadPoints(shape).ToGeoPolyline3();
        }

        /// <summary>
        /// Converts a Tekla polyline to a GeometryHelper 2D chain, dropping the Z coordinate.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="shape"/> is null.</exception>
        public static GeoPolyline2 ToGeoPolyline2(this TSG.PolyLine shape)
        {
            return ReadPoints(shape).ToGeoPolyline2();
        }

        /// <summary>
        /// Converts a Tekla polyline to a chain that may curve, bending it at each turn.
        /// </summary>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSG.PolyLine shape, IList<double> bendingRadii)
            => shape.ToGeoPolylineArc3(bendingRadii, Tolerance.Global);

        /// <summary>
        /// Converts a Tekla polyline to a chain that may curve, bending it at each turn, within a tolerance.
        /// </summary>
        /// <param name="shape">The Tekla polyline the thing is set out by.</param>
        /// <param name="bendingRadii">
        /// A radius for each bend, in order, the first belonging to the first point that turns; see
        /// <see cref="PointConvert.ToGeoPolylineArc3(IEnumerable{TSG.Point}, IList{double}, Tolerance)"/> for
        /// how the list is read.
        /// </param>
        /// <param name="tolerance">The tolerance; Tekla coordinates run large, so this is worth giving.</param>
        /// <returns>The chain: straight runs with a tangent arc at every bend that had room for one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="shape"/> is null.</exception>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSG.PolyLine shape, IList<double> bendingRadii, Tolerance tolerance)
        {
            return ReadPoints(shape).ToGeoPolylineArc3(bendingRadii, tolerance);
        }

        /// <summary>
        /// Converts a GeometryHelper chain to a Tekla polyline.
        /// </summary>
        /// <param name="polyline">The chain to convert.</param>
        /// <returns>The converted Tekla polyline.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static TSG.PolyLine ToTeklaPolyLine(this GeoPolyline3 polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return new TSG.PolyLine(new ArrayList(polyline.Vertices.ToTeklaPoint()));
        }

        /// <summary>
        /// Converts a chain that may curve to a Tekla polyline, following its arcs to a given accuracy.
        /// </summary>
        /// <param name="chain">The chain to convert.</param>
        /// <param name="chordTolerance">How far the straight pieces may fall inside an arc.</param>
        /// <returns>The converted Tekla polyline.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="chain"/> is null.</exception>
        /// <remarks>
        /// A Tekla polyline holds points and no bulges, so a curve cannot survive the trip. The accuracy sits
        /// in the call rather than being chosen here, which is the same bargain
        /// <see cref="GeoPolylineArc3.ToPolyline3(double)"/> offers.
        /// </remarks>
        public static TSG.PolyLine ToTeklaPolyLine(this GeoPolylineArc3 chain, double chordTolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return chain.ToPolyline3(chordTolerance).ToTeklaPolyLine();
        }

        /// <summary>
        /// Reads the points of a Tekla polyline, which are held in an untyped list.
        /// </summary>
        private static List<TSG.Point> ReadPoints(TSG.PolyLine shape)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));

            return PointConvert.ReadTeklaPoints(shape.Points, nameof(shape));
        }
    }
}
