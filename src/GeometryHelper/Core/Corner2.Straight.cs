using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Rounding the corners of a chain of straight pieces in the plane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chamfering a <see cref="GeoPolyline2"/> or a <see cref="GeoPolygon2"/> was offered and rounding them was
    /// not, though a straight chain is exactly what a fillet is usually asked of and space had it all along
    /// through <see cref="Corner3.Fillet(GeoPolyline3, double)"/>. The plane was behind space here, the reverse
    /// of everywhere else.
    /// </para>
    /// <para>
    /// A rounded corner is an arc, so a straight chain cannot hold the answer and the return type is the curved
    /// one — <see cref="GeoPolylineArc2"/> for a chain, <see cref="GeoPolygonArc2"/> for a loop — which is the
    /// same shape of answer space gives. Nothing new is worked out here: the chain is read as a curved one with
    /// every bulge at nought, and the rounding that already exists does the rest.
    /// </para>
    /// </remarks>
    public static partial class Corner2
    {
        #region Rounding a straight chain

        /// <summary>
        /// Rounds every corner of a straight chain by the same radius.
        /// </summary>
        public static GeoPolylineArc2 Fillet(GeoPolyline2 polyline, double radius) => Fillet(polyline, radius, Tolerance.Global);

        /// <summary>
        /// Rounds every corner of a straight chain by the same radius, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="radius">The radius to round every corner by.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain with its corners rounded; a corner with too little edge to give is left alone.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPolylineArc2 Fillet(GeoPolyline2 polyline, double radius, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return Fillet(new GeoPolylineArc2(polyline), radius, tolerance);
        }

        /// <summary>
        /// Rounds the corners of a straight chain, one radius each.
        /// </summary>
        public static GeoPolylineArc2 Fillet(GeoPolyline2 polyline, IReadOnlyList<double> radii) => Fillet(polyline, radii, Tolerance.Global);

        /// <summary>
        /// Rounds the corners of a straight chain, one radius each, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="radii">A radius per corner; nought leaves that corner square.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain with the corners the radii asked for rounded.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the list of radii is null.</exception>
        public static GeoPolylineArc2 Fillet(GeoPolyline2 polyline, IReadOnlyList<double> radii, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return Fillet(new GeoPolylineArc2(polyline), radii, tolerance);
        }

        /// <summary>
        /// Rounds one corner of a straight chain.
        /// </summary>
        public static bool TryFilletAt(GeoPolyline2 polyline, int index, double radius, out GeoPolylineArc2 result)
            => TryFilletAt(polyline, index, radius, out result, Tolerance.Global);

        /// <summary>
        /// Rounds one corner of a straight chain, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="index">Which vertex to round; the two ends of a chain are not corners.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="result">The rounded chain, or the chain unchanged where the corner would not take it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the corner was rounded; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryFilletAt(GeoPolyline2 polyline, int index, double radius, out GeoPolylineArc2 result, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return TryFilletAt(new GeoPolylineArc2(polyline), index, radius, out result, tolerance);
        }

        #endregion

        #region Rounding a straight loop

        /// <summary>
        /// Rounds every corner of a straight loop by the same radius.
        /// </summary>
        public static GeoPolygonArc2 Fillet(GeoPolygon2 polygon, double radius) => Fillet(polygon, radius, Tolerance.Global);

        /// <summary>
        /// Rounds every corner of a straight loop by the same radius, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="radius">The radius to round every corner by.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The loop with its corners rounded; a corner with too little edge to give is left alone.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPolygonArc2 Fillet(GeoPolygon2 polygon, double radius, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Fillet(new GeoPolygonArc2(polygon), radius, tolerance);
        }

        /// <summary>
        /// Rounds the corners of a straight loop, one radius each.
        /// </summary>
        public static GeoPolygonArc2 Fillet(GeoPolygon2 polygon, IReadOnlyList<double> radii) => Fillet(polygon, radii, Tolerance.Global);

        /// <summary>
        /// Rounds the corners of a straight loop, one radius each, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="radii">A radius per corner; nought leaves that corner square.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The loop with the corners the radii asked for rounded.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the list of radii is null.</exception>
        public static GeoPolygonArc2 Fillet(GeoPolygon2 polygon, IReadOnlyList<double> radii, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Fillet(new GeoPolygonArc2(polygon), radii, tolerance);
        }

        /// <summary>
        /// Rounds one corner of a straight loop.
        /// </summary>
        public static bool TryFilletAt(GeoPolygon2 polygon, int index, double radius, out GeoPolygonArc2 result)
            => TryFilletAt(polygon, index, radius, out result, Tolerance.Global);

        /// <summary>
        /// Rounds one corner of a straight loop, within a tolerance.
        /// </summary>
        /// <param name="polygon">The loop.</param>
        /// <param name="index">Which vertex to round; every vertex of a loop is a corner.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="result">The rounded loop, or the loop unchanged where the corner would not take it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the corner was rounded; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryFilletAt(GeoPolygon2 polygon, int index, double radius, out GeoPolygonArc2 result, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return TryFilletAt(new GeoPolygonArc2(polygon), index, radius, out result, tolerance);
        }

        #endregion
    }
}
