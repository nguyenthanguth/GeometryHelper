using System;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The numbers behind an ellipse, each read on the ellipse scaled so that its major radius is one and its minor
    /// radius is the ratio of the two, which keeps every one of them between nought and one whatever the drawing measures.
    /// </summary>
    public static partial class Ellipse2
    {
        private const double HalfPi = Math.PI * 0.5;

        /// <summary>
        /// Gets the perimeter of the ellipse with a major radius of one, by the arithmetic-geometric mean of Gauss and
        /// Kummer.
        /// </summary>
        /// <param name="ratio">The minor radius over the major one, above nought and no more than one.</param>
        /// <remarks>
        /// The mean of one and the ratio converges quadratically, so a ratio of a thousandth takes six rounds. Each round
        /// halves the gap between the two and adds twice the weight of the last round's square of that gap.
        /// </remarks>
        internal static double UnitPerimeter(double ratio)
        {
            double mean = 1.0;
            double geometric = ratio;
            double sum = 0.5 * (1.0 - ratio) * (1.0 + ratio);
            double weight = 0.5;

            // The gap stalls at a unit of rounding once the two means agree, so the rounds are capped too.
            for (int round = 0; round < 64; round++)
            {
                double gap = 0.5 * (mean - geometric);
                double next = 0.5 * (mean + geometric);
                geometric = Math.Sqrt(mean * geometric);
                mean = next;
                weight *= 2.0;
                sum += weight * gap * gap;

                if (!(gap > 1E-16 * mean))
                {
                    break;
                }
            }

            return 2.0 * Math.PI * ((1.0 - sum) / mean);
        }

        /// <summary>
        /// Gets the length along the rim of the ellipse with a major radius of one, from the end of its major axis to an
        /// eccentric angle within the first quarter.
        /// </summary>
        /// <param name="ratio">The minor radius over the major one.</param>
        /// <param name="angle">The eccentric angle, from nought to a quarter turn.</param>
        /// <remarks>
        /// The rim runs at a speed of the root of sin² + ratio² cos², which is the ratio times the root of 1 - m sin² with
        /// m = 1 - 1/ratio², so the length is the ratio times the incomplete integral E(angle | m) of the second kind.
        /// Written with Carlson's R_F and R_D and their arguments scaled by ratio², both terms are positive and none of
        /// them is large, however thin the ellipse.
        /// </remarks>
        internal static double UnitArc(double ratio, double angle)
        {
            double sin = Math.Sin(angle);

            if (!(sin > 0.0))
            {
                return 0.0;
            }

            double cos = Math.Cos(angle);
            double ratioSquared = ratio * ratio;
            double x = ratioSquared * cos * cos;

            Carlson(x, x + sin * sin, ratioSquared, out double rf, out double rd);

            return ratioSquared * sin * (rf + (1.0 - ratioSquared) / 3.0 * sin * sin * rd);
        }

        /// <summary>
        /// Gets the eccentric angle within the first quarter at which <see cref="UnitArc(double, double)"/> reaches a
        /// length, by Newton's method kept inside a bracket that halves whenever a step would leave it.
        /// </summary>
        /// <param name="ratio">The minor radius over the major one.</param>
        /// <param name="length">The length along the rim from the end of the major axis, no more than a quarter of the perimeter.</param>
        internal static double UnitAngleAtArc(double ratio, double length)
        {
            if (!(length > 0.0))
            {
                return 0.0;
            }

            double quarter = UnitArc(ratio, HalfPi);

            if (!(length < quarter))
            {
                return HalfPi;
            }

            double low = 0.0;
            double high = HalfPi;
            double angle = HalfPi * length / quarter;

            for (int step = 0; step < 100; step++)
            {
                double miss = UnitArc(ratio, angle) - length;

                if (miss > 0.0)
                {
                    high = angle;
                }
                else if (miss < 0.0)
                {
                    low = angle;
                }
                else
                {
                    return angle;
                }

                double sin = Math.Sin(angle);
                double cos = Math.Cos(angle);
                double next = angle - miss / Math.Sqrt(sin * sin + ratio * ratio * cos * cos);

                if (!(next > low && next < high))
                {
                    next = 0.5 * (low + high);
                }

                if (Math.Abs(next - angle) <= 4E-16 * HalfPi || !(high - low > 4E-16 * HalfPi))
                {
                    return next;
                }

                angle = next;
            }

            return angle;
        }

        /// <summary>
        /// Gets Carlson's symmetric integrals R_F(x, y, z) and R_D(x, y, z) together, by the duplication they share.
        /// </summary>
        /// <remarks>
        /// Each round moves the three arguments a quarter of the way towards each other and leaves both integrals as they
        /// were; once the arguments agree to a few thousandths, a short series in their spread gives each to the last
        /// digit. The rounds stop when both series are that close, the tighter one being R_D's.
        /// </remarks>
        internal static void Carlson(double x, double y, double z, out double rf, out double rd)
        {
            const double LimitF = 0.0025;
            const double LimitD = 0.0015;

            double sum = 0.0;
            double factor = 1.0;
            double meanF;
            double meanD;
            double dxF, dyF, dzF;
            double dxD, dyD, dzD;

            for (int round = 0; ; round++)
            {
                double sqrtX = Math.Sqrt(x);
                double sqrtY = Math.Sqrt(y);
                double sqrtZ = Math.Sqrt(z);
                double lambda = sqrtX * (sqrtY + sqrtZ) + sqrtY * sqrtZ;

                sum += factor / (sqrtZ * (z + lambda));
                factor *= 0.25;
                x = 0.25 * (x + lambda);
                y = 0.25 * (y + lambda);
                z = 0.25 * (z + lambda);

                meanF = (x + y + z) / 3.0;
                dxF = (meanF - x) / meanF;
                dyF = (meanF - y) / meanF;
                dzF = (meanF - z) / meanF;

                meanD = 0.2 * (x + y + 3.0 * z);
                dxD = (meanD - x) / meanD;
                dyD = (meanD - y) / meanD;
                dzD = (meanD - z) / meanD;

                bool closeF = Math.Max(Math.Abs(dxF), Math.Max(Math.Abs(dyF), Math.Abs(dzF))) <= LimitF;
                bool closeD = Math.Max(Math.Abs(dxD), Math.Max(Math.Abs(dyD), Math.Abs(dzD))) <= LimitD;

                // A NaN never comes close, so the rounds are capped as well; a thousand-to-one spread takes about ten.
                if ((closeF && closeD) || round >= 200)
                {
                    break;
                }
            }

            double e2 = dxF * dyF - dzF * dzF;
            double e3 = dxF * dyF * dzF;
            rf = (1.0 + (e2 / 24.0 - 0.1 - 3.0 / 44.0 * e3) * e2 + e3 / 14.0) / Math.Sqrt(meanF);

            const double C1 = 3.0 / 14.0;
            const double C2 = 1.0 / 6.0;
            const double C3 = 9.0 / 22.0;
            const double C4 = 3.0 / 26.0;
            const double C5 = 0.25 * C3;
            const double C6 = 1.5 * C4;

            double ea = dxD * dyD;
            double eb = dzD * dzD;
            double ec = ea - eb;
            double ed = ea - 6.0 * eb;
            double ee = ed + ec + ec;
            rd = 3.0 * sum + factor * (1.0 + ed * (-C1 + C5 * ed - C6 * dzD * ee) + dzD * (C2 * ee + dzD * (-C3 * ec + dzD * C4 * ea)))
                / (meanD * Math.Sqrt(meanD));
        }

        /// <summary>
        /// Gets the point of the rim of the ellipse with a major radius of one nearest a point in its first quadrant, by
        /// Eberly's robust method ("Distance from a Point to an Ellipse, an Ellipsoid, or a Hyperellipsoid").
        /// </summary>
        /// <param name="ratio">The minor radius over the major one.</param>
        /// <param name="along">How far the point lies along the major axis, nought or more.</param>
        /// <param name="across">How far the point lies along the minor axis, nought or more.</param>
        /// <param name="cos">The cosine of the eccentric angle of the nearest point.</param>
        /// <param name="sin">The sine of the eccentric angle of the nearest point.</param>
        /// <remarks>
        /// <para>
        /// The nearest point is (along / (1 + s ratio²), across / (1 + s)) for the one root s of the equation saying it
        /// lies on the rim. The root is found by halving a bracket, which cannot fail where Newton's method can; it is
        /// written in the denominator 1 + s rather than in s, so that a point a hair off the major axis, whose denominator
        /// is tiny, still finds its root to the last digit. Where the bracket spans more than a factor of two it is halved
        /// at its geometric middle, so a root a long way out takes a few rounds more rather than a thousand.
        /// </para>
        /// <para>
        /// A point on the major axis, inside, nearer the centre than the centre of curvature of the end of the axis, is
        /// nearest two points mirrored across it; the one on the positive side is taken. The centre is nearest the two
        /// ends of the minor axis, and the positive one is taken; a circle has every point of its rim as near, and the same
        /// point is taken, the end of the minor axis, where Eberly's method left alone would give the end of the major.
        /// </para>
        /// </remarks>
        internal static void UnitClosest(double ratio, double along, double across, out double cos, out double sin)
        {
            if (across > 0.0)
            {
                double z0 = along;
                double z1 = across / ratio;
                double g = z0 * z0 + z1 * z1 - 1.0;

                if (g == 0.0)
                {
                    cos = z0;
                    sin = z1;
                    return;
                }

                double ratioSquared = ratio * ratio;
                double n0 = z0 / ratioSquared;
                double shift = (1.0 - ratio) * (1.0 + ratio) / ratioSquared;
                double low = g < 0.0 ? z1 : 1.0;
                double high = g < 0.0 ? 1.0 : Math.Sqrt(n0 * n0 + z1 * z1);

                for (int round = 0; round < 4096; round++)
                {
                    double middle = high > 2.0 * low ? Math.Sqrt(low * high) : 0.5 * (low + high);

                    if (!(middle > low && middle < high))
                    {
                        break;
                    }

                    double r0 = n0 / (middle + shift);
                    double r1 = z1 / middle;
                    double miss = r0 * r0 + r1 * r1 - 1.0;

                    if (miss > 0.0)
                    {
                        low = middle;
                    }
                    else if (miss < 0.0)
                    {
                        high = middle;
                    }
                    else
                    {
                        low = middle;
                        high = middle;
                        break;
                    }
                }

                double root = 0.5 * (low + high);
                cos = n0 / (root + shift);
                sin = z1 / root;
                return;
            }

            double spread = (1.0 - ratio) * (1.0 + ratio);

            // The centre takes the end of the minor axis whatever the ratio, a circle's included, so that the answer does
            // not jump between radii equal and radii a hair apart.
            if (!(along > 0.0))
            {
                cos = 0.0;
                sin = 1.0;
            }
            else if (along < spread)
            {
                cos = along / spread;
                sin = Math.Sqrt((1.0 - cos) * (1.0 + cos));
            }
            else
            {
                cos = 1.0;
                sin = 0.0;
            }
        }
    }
}
