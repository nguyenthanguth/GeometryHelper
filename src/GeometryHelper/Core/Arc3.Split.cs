using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Cutting an arc in space in two.
    /// </summary>
    /// <remarks>
    /// An arc in the plane could be cut at a parameter or at a point and an arc in space could not, though a
    /// whole chain of them could be. Nothing new is worked out: the centre, the radius and the plane all stay,
    /// only the sweep is shared out between the two pieces, so the pieces put back end to end draw exactly what
    /// went in.
    /// </remarks>
    public static partial class Arc3
    {
        /// <summary>
        /// Cuts an arc at a normalized parameter.
        /// </summary>
        public static bool TrySplitAt(GeoArc3 arc, double parameter, out GeoArc3[] pieces) => TrySplitAt(arc, parameter, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts an arc at a normalized parameter, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc to cut.</param>
        /// <param name="parameter">Where to cut it, from 0 at its start to 1 at its end.</param>
        /// <param name="pieces">The pieces in order along the arc, or the arc whole when the method returns false.</param>
        /// <param name="tolerance">The tolerance: a cut within it of either end leaves the arc whole.</param>
        /// <returns>true when the arc was cut in two; otherwise, false.</returns>
        public static bool TrySplitAt(GeoArc3 arc, double parameter, out GeoArc3[] pieces, Tolerance tolerance)
        {
            double atEnds = tolerance.EqualPoint / Math.Max(arc.Length, tolerance.EqualPoint);

            if (double.IsNaN(parameter) || parameter <= atEnds || parameter >= 1.0 - atEnds)
            {
                pieces = new[] { arc };
                return false;
            }

            double cutAngle = arc.StartAngle + arc.SweptAngle * parameter;

            pieces = new[]
            {
                new GeoArc3(arc.Center, arc.Normal, arc.Radius, arc.StartAngle, cutAngle, arc.IsClockwise),
                new GeoArc3(arc.Center, arc.Normal, arc.Radius, cutAngle, arc.StartAngle + arc.SweptAngle, arc.IsClockwise)
            };

            return true;
        }

        /// <summary>
        /// Cuts an arc at the point of it nearest a point.
        /// </summary>
        public static bool TrySplitAt(GeoArc3 arc, GeoPoint3 point, out GeoArc3[] pieces) => TrySplitAt(arc, point, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts an arc at the point of it nearest a point, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc to cut.</param>
        /// <param name="point">Where to cut it; a point off the arc cuts at the point of the arc nearest it.</param>
        /// <param name="pieces">The pieces in order along the arc, or the arc whole when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the arc was cut in two; otherwise, false.</returns>
        /// <remarks>
        /// A point off the arc is answered by the place of the arc nearest it, the same way the plane answers,
        /// and a point off the plane by what stands under it: only the direction from the centre decides.
        /// </remarks>
        public static bool TrySplitAt(GeoArc3 arc, GeoPoint3 point, out GeoArc3[] pieces, Tolerance tolerance)
            => TrySplitAt(arc, arc.GetParameterAtPoint(point, tolerance), out pieces, tolerance);

        /// <summary>
        /// Cuts an arc at an arc length measured from its start.
        /// </summary>
        public static bool TrySplitAtDistance(GeoArc3 arc, double distance, out GeoArc3[] pieces)
            => TrySplitAtDistance(arc, distance, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts an arc at an arc length measured from its start, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc to cut.</param>
        /// <param name="distance">How far along to cut, measured along the curve and not across the chord.</param>
        /// <param name="pieces">The pieces in order along the arc, or the arc whole when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the arc was cut in two; otherwise, false.</returns>
        public static bool TrySplitAtDistance(GeoArc3 arc, double distance, out GeoArc3[] pieces, Tolerance tolerance)
            => TrySplitAt(arc, distance / Math.Max(arc.Length, tolerance.EqualPoint), out pieces, tolerance);
    }
}
