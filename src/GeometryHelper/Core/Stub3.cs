using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// How a segment you give in space, no longer than the point tolerance, is read by the shortest lines and by the
    /// distances that do not go through <see cref="Gap3"/>: as the point at its start, too short to have a direction,
    /// except where it truly meets the other shape, as it does passing through a surface or crossing a segment, where it
    /// is in contact however short: as <see cref="Gap3"/> reads it, and as <see cref="Stub2"/> reads one in the plane.
    /// </summary>
    /// <remarks>
    /// At a thousandth, a segment 0.0009 long pointing down at a square in the floor, its start 0.0015 above it and its end
    /// 0.0006, is joined to it from its start, by a line 0.0015 long; one passing through it, its start 0.0004 above, is
    /// joined to it by a line of no length where it passes through. A side of a shape is the side it is however short, and
    /// is never read so.
    /// </remarks>
    internal static class Stub3
    {
        /// <summary>
        /// Whether a tolerance reads a segment you give as the point at its start: one no longer than the point tolerance,
        /// by the measure <see cref="Gap3"/> takes. A tolerance no coarser than the rounding of the segment's start, nought
        /// or <see cref="AsItIs(GeoLine3, Tolerance)"/>, reads none so, and measures every segment as it is.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static bool ReadsAsStart(GeoLine3 line, Tolerance tolerance) => ReadsAsStart(line.StartPoint, line.EndPoint, tolerance);

        /// <summary>
        /// Whether a tolerance reads the segment you give from one point to another as the point at its start.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static bool ReadsAsStart(GeoPoint3 from, GeoPoint3 to, Tolerance tolerance)
            => from.GetDistanceSquaredTo(to) <= tolerance.EqualPoint * tolerance.EqualPoint && tolerance.EqualPoint > Rounding(from);

        /// <summary>
        /// Whether a straight edge you give is read as the point at its start, as a segment is; an edge that is an arc
        /// never is.
        /// </summary>
        internal static bool ReadsAsStart(GeoEdge3 edge, Tolerance tolerance)
            => !edge.IsArc && ReadsAsStart(edge.StartPoint, edge.EndPoint, tolerance);

        /// <summary>
        /// Whether the gap to a segment read as its start, measured to the segment as it is, is a true meeting: no more
        /// than the rounding of its start, 1E-12 of its largest coordinate, as <see cref="Gap3"/> reads it against a flat
        /// region.
        /// </summary>
        internal static bool Meets(double gap, GeoLine3 line) => gap <= Rounding(line.StartPoint);

        /// <summary>
        /// Whether the gap between two segments, one or both read as their starts, measured as they are, is a true
        /// meeting: no more than 1E-12 of the largest coordinate of the four ends, as <see cref="Gap3"/> reads two segments.
        /// </summary>
        internal static bool Meets(double gap, GeoLine3 line, GeoLine3 other)
            => gap <= 1E-12 * Math.Max(1.0, Math.Max(Far(line.StartPoint, line.EndPoint), Far(other.StartPoint, other.EndPoint)));

        /// <summary>
        /// Gets the tolerance a segment read as its start is measured with as the segment it is, to find where it truly
        /// meets a shape: the one given, its point and planar thresholds brought down to the rounding of the segment's
        /// start.
        /// </summary>
        internal static Tolerance AsItIs(GeoLine3 line, Tolerance tolerance) => AsItIs(Rounding(line.StartPoint), tolerance);

        /// <summary>
        /// Gets the tolerance two segments, one or both read as their starts, are measured with as they are: the one given,
        /// its point and planar thresholds brought down to the rounding of the four ends.
        /// </summary>
        internal static Tolerance AsItIs(GeoLine3 line, GeoLine3 other, Tolerance tolerance)
            => AsItIs(1E-12 * Math.Max(1.0, Math.Max(Far(line.StartPoint, line.EndPoint), Far(other.StartPoint, other.EndPoint))), tolerance);

        /// <summary>
        /// Gets the shortest segment joining a shape and a segment read as its start: the one joining the segment as it
        /// is where that one is a true meeting, otherwise the one joining the start.
        /// </summary>
        /// <param name="line">The segment read as its start.</param>
        /// <param name="asItIs">The shortest segment to the segment as it is, found with <see cref="AsItIs(GeoLine3, Tolerance)"/>.</param>
        /// <param name="toStart">The shortest segment to its start, running the same way as <paramref name="asItIs"/>.</param>
        internal static GeoLine3 Nearest(GeoLine3 line, GeoLine3 asItIs, GeoLine3 toStart)
            => Meets(asItIs.Length, line) ? asItIs : toStart;

        private static Tolerance AsItIs(double rounding, Tolerance tolerance)
            => new Tolerance(rounding, tolerance.EqualVector, tolerance.EqualAngleRad, rounding);

        /// <summary>
        /// The rounding of a point: 1E-12 of its largest coordinate, and of one at the least.
        /// </summary>
        private static double Rounding(GeoPoint3 point)
            => 1E-12 * Math.Max(1.0, Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z))));

        private static double Far(GeoPoint3 a, GeoPoint3 b)
            => Math.Max(
                Math.Max(Math.Abs(a.X), Math.Max(Math.Abs(a.Y), Math.Abs(a.Z))),
                Math.Max(Math.Abs(b.X), Math.Max(Math.Abs(b.Y), Math.Abs(b.Z))));
    }
}
