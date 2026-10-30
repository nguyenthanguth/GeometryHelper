using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// How a segment you give in the plane, no longer than the point tolerance, is read: as the point at its start, too
    /// short to have a direction, except where it truly meets the other shape, as it does crossing a side or running into
    /// an arc, where it is in contact however short. Space reads it so too (see <see cref="Gap3"/>).
    /// </summary>
    /// <remarks>
    /// At a thousandth, a segment 0.0009 long pointing at a side, its start 0.0015 off and its end 0.0006 off, is 0.0015
    /// from it and does not collide; one crossing the side, its start 0.0004 off, is nought from it, and meets it where it
    /// crosses. A side of a shape is the side it is however short, and is never read so.
    /// </remarks>
    internal static class Stub2
    {
        /// <summary>
        /// Whether a tolerance reads a segment you give as the point at its start: one no longer than the point tolerance,
        /// by the same measure the projection onto a segment takes it for a point. A tolerance no coarser than the rounding
        /// of the segment's start, nought or <see cref="AsItIs"/>, reads none so, and measures every segment as it is.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static bool ReadsAsStart(GeoLine2 line, Tolerance tolerance)
        {
            double x = line.EndPoint.X - line.StartPoint.X;
            double y = line.EndPoint.Y - line.StartPoint.Y;

            // A segment plainly longer than the tolerance, the common case, is settled here; one a NaN is in, too.
            if (!(x * x + y * y <= tolerance.EqualPoint * tolerance.EqualPoint * (1.0 + 1E-14)))
            {
                return false;
            }

            return ArcChain2.ReadsAsPoint(line, tolerance) && tolerance.EqualPoint > Rounding(line);
        }

        /// <summary>
        /// Whether the gap to a segment read as its start, measured to the segment as it is, is a true meeting: no more
        /// than the rounding of its start, 1E-12 of its largest coordinate, as space reads it against a flat region or
        /// an arc.
        /// </summary>
        internal static bool Meets(double gap, GeoLine2 line) => gap <= Rounding(line);

        /// <summary>
        /// Whether the gap between two segments, one or both read as their starts, measured as they are, is a true
        /// meeting: no more than 1E-12 of the largest coordinate of the four ends, as space reads two segments.
        /// </summary>
        internal static bool Meets(double gap, GeoLine2 line, GeoLine2 other)
        {
            double far = Math.Max(
                Math.Max(Math.Max(Math.Abs(line.StartPoint.X), Math.Abs(line.StartPoint.Y)), Math.Max(Math.Abs(line.EndPoint.X), Math.Abs(line.EndPoint.Y))),
                Math.Max(Math.Max(Math.Abs(other.StartPoint.X), Math.Abs(other.StartPoint.Y)), Math.Max(Math.Abs(other.EndPoint.X), Math.Abs(other.EndPoint.Y))));

            return gap <= 1E-12 * Math.Max(1.0, far);
        }

        /// <summary>
        /// Gets the tolerance a segment read as its start is measured with as the segment it is, to find where it truly
        /// meets a shape: the one given, its point and planar thresholds brought down to the rounding of the segment's
        /// start, so that only a true crossing or touch is found, and placed where it is.
        /// </summary>
        internal static Tolerance AsItIs(GeoLine2 line, Tolerance tolerance)
        {
            double rounding = Rounding(line);

            return new Tolerance(rounding, tolerance.EqualVector, tolerance.EqualAngleRad, rounding);
        }

        /// <summary>
        /// Gets the shortest segment joining a shape and a segment read as its start: the one joining the segment as it
        /// is where that one is a true meeting, otherwise the one joining the start.
        /// </summary>
        /// <param name="line">The segment read as its start.</param>
        /// <param name="asItIs">The shortest segment to the segment as it is, found with <see cref="AsItIs"/>.</param>
        /// <param name="toStart">The shortest segment to its start, running the same way as <paramref name="asItIs"/>.</param>
        internal static GeoLine2 Nearest(GeoLine2 line, GeoLine2 asItIs, GeoLine2 toStart)
            => Meets(asItIs.Length, line) ? asItIs : toStart;

        /// <summary>
        /// The rounding of a segment's start: 1E-12 of its largest coordinate, and of one at the least.
        /// </summary>
        private static double Rounding(GeoLine2 line)
            => 1E-12 * Math.Max(1.0, Math.Max(Math.Abs(line.StartPoint.X), Math.Abs(line.StartPoint.Y)));
    }
}
