using System;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether an arc moved under a transformation lands within a tolerance of where the transformation puts its points
    /// everywhere along it, not only where it is asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A point of the arc moved, read from the moved start across the arc, is <c>p cos φ + q sin φ - w</c>: the two axes of
    /// its plane the radius long, carried by the transformation, the angle round the old centre, and where the start was.
    /// Against the circle the moved arc lies on, of radius <c>r</c>, the centre <c>c</c> across from the moved start, the
    /// square of the point's distance from the centre less the square of the radius is <c>g = |a|² - 2 a·c</c>, a sum of
    /// cosines and sines of the angle and of twice it, and the point stands <c>g / (√(r² + g) + r)</c> off the circle, which
    /// grows with <c>g</c>: the worst of a stretch is at the greatest or the least <c>g</c>. Its second derivative is no more
    /// than the bend, the sizes of its two halves, one and four times over, so between two points asked a share <c>w</c> of
    /// the sweep apart <c>g</c> strays from the line between them by no more than the bend times the square of the angle
    /// over eight. A stretch that cannot reach the tolerance so is settled; one that can is halved and its middle asked,
    /// until it is settled or the middle lands off. The height off the moved plane, in space, is a sum of a cosine and a
    /// sine, and is bounded alike.
    /// </para>
    /// <para>
    /// Asked at sixteen points only, a stretched arc of radius 12.7 scaled by 87 landed 0.0010043 off between two of them
    /// at a tolerance of 0.001, and was moved.
    /// </para>
    /// </remarks>
    internal static class ArcLanding
    {
        /// <summary>
        /// How many times a stretch is halved at the most: a share of the sweep of 2^-50 of a sixteenth, past which the bound
        /// is the rounding of the numbers.
        /// </summary>
        private const int Deepest = 50;

        /// <summary>
        /// Gets how far, at the most, the second derivative of <c>g</c> reaches, for the axes of the plane carried and the
        /// radius long, <c>p</c> (<paramref name="px"/>, <paramref name="py"/>, <paramref name="pz"/>) and <c>q</c>
        /// (<paramref name="qx"/>, <paramref name="qy"/>, <paramref name="qz"/>), the start at <paramref name="start"/> radians
        /// round the old centre and the moved centre <paramref name="cx"/>, <paramref name="cy"/>, <paramref name="cz"/> across
        /// from the moved start, each in the moved plane.
        /// </summary>
        internal static double Bend(
            double px, double py, double pz, double qx, double qy, double qz, double start, double cx, double cy, double cz)
        {
            double cos = Math.Cos(start), sin = Math.Sin(start);

            // The moved start, read from where the old centre lands, and the moved centre read from there.
            double ox = px * cos + qx * sin + cx;
            double oy = py * cos + qy * sin + cy;
            double oz = pz * cos + qz * sin + cz;

            double first = 2.0 * Hypot(px * ox + py * oy + pz * oz, qx * ox + qy * oy + qz * oz);
            double second = Hypot(0.5 * (px * px + py * py + pz * pz - qx * qx - qy * qy - qz * qz), px * qx + py * qy + pz * qz);

            return first + 4.0 * second;
        }

        /// <summary>
        /// Determines whether the arc plainly lands within a tolerance everywhere between sixteen points already asked and
        /// found within it: the most any point asked stands off, and the most a sixteenth of the sweep lets it stray between
        /// two, together no further than the tolerance, which settles it without the stretches being asked one by one.
        /// </summary>
        /// <param name="heights">The heights at the sixteen points and both ends; noughts in the plane.</param>
        /// <param name="gaps">The <c>g</c> at the sixteen points and both ends.</param>
        /// <param name="sweep">The angle the arc sweeps, in radians.</param>
        /// <param name="radius">The radius of the moved circle, as the gaps are read from it.</param>
        /// <param name="bend">The bound of the second derivative of <c>g</c> (<see cref="Bend"/>).</param>
        /// <param name="rise">The bound of the second derivative of the height; nought in the plane.</param>
        /// <param name="tolerance">How far off a point may land.</param>
        internal static bool Plainly(double[] heights, double[] gaps, double sweep, double radius, double bend, double rise, double tolerance)
        {
            double highest = 0.0, most = double.MinValue, least = double.MaxValue;

            for (int i = 0; i <= 16; i++)
            {
                highest = Math.Max(highest, Math.Abs(heights[i]));
                most = Math.Max(most, gaps[i]);
                least = Math.Min(least, gaps[i]);
            }

            double angle = sweep / 16.0;
            double square = angle * angle * 0.125;
            double height = highest + rise * square;
            double gap = Math.Max(Off(most + bend * square, radius), -Off(least - bend * square, radius));

            return Math.Sqrt(height * height + gap * gap) <= tolerance;
        }

        /// <summary>
        /// Determines whether the arc lands within a tolerance everywhere between sixteen points already asked and found within
        /// it, from the start, at nought, to the end, at sixteen.
        /// </summary>
        /// <param name="at">How high off the moved plane and with what <c>g</c> the point a share of the sweep along lands.</param>
        /// <param name="heights">The heights at the sixteen points and both ends; noughts in the plane.</param>
        /// <param name="gaps">The <c>g</c> at the sixteen points and both ends.</param>
        /// <param name="sweep">The angle the arc sweeps, in radians.</param>
        /// <param name="radius">The radius of the moved circle, as the gaps are read from it.</param>
        /// <param name="bend">The bound of the second derivative of <c>g</c> (<see cref="Bend"/>).</param>
        /// <param name="rise">The bound of the second derivative of the height; nought in the plane.</param>
        /// <param name="tolerance">How far off a point may land.</param>
        internal static bool Holds(Func<double, (double Height, double Gap)> at, double[] heights, double[] gaps, double sweep, double radius, double bend, double rise, double tolerance)
        {
            for (int i = 0; i < 16; i++)
            {
                if (!Holds(at, i / 16.0, (i + 1) / 16.0, heights[i], heights[i + 1], gaps[i], gaps[i + 1], Math.Abs(sweep), radius, bend, rise, tolerance, 0))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Holds(
            Func<double, (double Height, double Gap)> at, double from, double to, double heightFrom, double heightTo, double gapFrom, double gapTo,
            double sweep, double radius, double bend, double rise, double tolerance, int depth)
        {
            double angle = sweep * (to - from);
            double square = angle * angle * 0.125;
            double height = Math.Max(Math.Abs(heightFrom), Math.Abs(heightTo)) + rise * square;
            double most = Off(Math.Max(gapFrom, gapTo) + bend * square, radius);
            double least = Off(Math.Min(gapFrom, gapTo) - bend * square, radius);
            double gap = Math.Max(most, -least);

            // Written as what a stretch that can reach the tolerance is, so that one of NaNs passes, as a point of NaNs does.
            if (!(Math.Sqrt(height * height + gap * gap) > tolerance) || depth >= Deepest)
            {
                return true;
            }

            double middle = 0.5 * (from + to);
            (double h, double g) = at(middle);

            if (Math.Sqrt(h * h + Off(g, radius) * Off(g, radius)) > tolerance)
            {
                return false;
            }

            return Holds(at, from, middle, heightFrom, h, gapFrom, g, sweep, radius, bend, rise, tolerance, depth + 1)
                && Holds(at, middle, to, h, heightTo, g, gapTo, sweep, radius, bend, rise, tolerance, depth + 1);
        }

        /// <summary>
        /// Gets how far a point stands off a circle of a radius, from the square of its distance from the centre less the
        /// square of the radius: no further inside than the centre.
        /// </summary>
        private static double Off(double gap, double radius)
            => gap <= -radius * radius ? -radius : gap / (Math.Sqrt(radius * radius + gap) + radius);

        private static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
    }
}
