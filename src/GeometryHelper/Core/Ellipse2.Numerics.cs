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

        /// <summary>
        /// A function of the eccentric angle made of a constant and the first two harmonics:
        /// c0 + c1 cos t + s1 sin t + c2 cos 2t + s2 sin 2t.
        /// </summary>
        /// <remarks>
        /// The square of the distance from a point of the rim to a fixed point, and the equation of a circle or of another
        /// ellipse read along the rim, are all of this form. Written in z = e^{it} and multiplied by z², it is a polynomial of
        /// degree four, so it turns at most four times round the rim and crosses nought at most four times.
        /// </remarks>
        internal readonly struct Wave
        {
            internal Wave(double c0, double c1, double s1, double c2, double s2)
            {
                C0 = c0;
                C1 = c1;
                S1 = s1;
                C2 = c2;
                S2 = s2;
            }

            internal double C0 { get; }

            internal double C1 { get; }

            internal double S1 { get; }

            internal double C2 { get; }

            internal double S2 { get; }

            /// <summary>
            /// Gets the rate of change of the function along the angle, which is of the same form.
            /// </summary>
            internal Wave Slope => new Wave(0.0, S1, -C1, 2.0 * S2, -2.0 * C2);

            /// <summary>
            /// Gets the sum of the sizes of the terms, the scale rounding is measured against.
            /// </summary>
            internal double Size => Math.Abs(C0) + Math.Abs(C1) + Math.Abs(S1) + Math.Abs(C2) + Math.Abs(S2);

            /// <summary>
            /// Gets the value at an angle.
            /// </summary>
            internal double At(double angle)
            {
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);

                return C0 + C1 * cos + S1 * sin + C2 * ((cos - sin) * (cos + sin)) + S2 * (2.0 * sin * cos);
            }

            /// <summary>
            /// Gets the value at an angle and the rate of change there, from one cosine and one sine.
            /// </summary>
            internal double At(double angle, out double rate)
            {
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                double cos2 = (cos - sin) * (cos + sin);
                double sin2 = 2.0 * sin * cos;

                rate = S1 * cos - C1 * sin + 2.0 * (S2 * cos2 - C2 * sin2);
                return C0 + C1 * cos + S1 * sin + C2 * cos2 + S2 * sin2;
            }
        }

        /// <summary>
        /// Gets the angles at which a function of two harmonics turns, its maxima and minima, sorted from nought up to a full
        /// turn.
        /// </summary>
        /// <param name="wave">The function.</param>
        /// <param name="turns">Four places for the angles.</param>
        /// <returns>How many angles were found: none for a function that does not change at all, else two to four.</returns>
        /// <remarks>
        /// The slope is a function of the same form; its roots are the arguments of the roots of a polynomial of degree four in
        /// z = e^{it}, found together by the Aberth–Ehrlich iteration, which converges for every root at once from any start
        /// and needs no bracket. A root of the slope is a root of that polynomial on the unit circle, so each argument is a
        /// candidate; Newton's method on the slope itself then polishes it to the last digit. A slope whose second harmonic is
        /// rounding next to the rest is read as a polynomial of degree two, which it then is.
        /// </remarks>
        internal static int Turns(Wave wave, double[] turns)
        {
            Wave slope = wave.Slope;
            double second = Math.Sqrt(slope.C2 * slope.C2 + slope.S2 * slope.S2);
            double first = Math.Sqrt(slope.C1 * slope.C1 + slope.S1 * slope.S1);
            double scale = Math.Max(second, first);

            if (!(scale > 1E-14 * wave.Size))
            {
                return 0;
            }

            var re = new double[4];
            var im = new double[4];
            int count;

            if (second > 1E-13 * scale)
            {
                // z² times the slope: (c2 - i s2)/2 z⁴ + (c1 - i s1)/2 z³ + c0 z² + (c1 + i s1)/2 z + (c2 + i s2)/2, made monic.
                double leadRe = 0.5 * slope.C2;
                double leadIm = -0.5 * slope.S2;
                double leadNorm = leadRe * leadRe + leadIm * leadIm;

                Divide(0.5 * slope.C1, -0.5 * slope.S1, leadRe, leadIm, leadNorm, out double c3Re, out double c3Im);
                Divide(slope.C0, 0.0, leadRe, leadIm, leadNorm, out double c2Re, out double c2Im);
                Divide(0.5 * slope.C1, 0.5 * slope.S1, leadRe, leadIm, leadNorm, out double c1Re, out double c1Im);
                Divide(0.5 * slope.C2, 0.5 * slope.S2, leadRe, leadIm, leadNorm, out double c0Re, out double c0Im);

                Aberth(c3Re, c3Im, c2Re, c2Im, c1Re, c1Im, c0Re, c0Im, re, im);
                count = 4;
            }
            else
            {
                // z times the first harmonic and the constant: (c1 - i s1)/2 z² + c0 z + (c1 + i s1)/2, made monic.
                double leadRe = 0.5 * slope.C1;
                double leadIm = -0.5 * slope.S1;
                double leadNorm = leadRe * leadRe + leadIm * leadIm;

                Divide(slope.C0, 0.0, leadRe, leadIm, leadNorm, out double bRe, out double bIm);
                Divide(0.5 * slope.C1, 0.5 * slope.S1, leadRe, leadIm, leadNorm, out double cRe, out double cIm);

                // The root of b² - 4c, the larger root first and the other from the product of the two, which keeps its digits.
                double dRe = bRe * bRe - bIm * bIm - 4.0 * cRe;
                double dIm = 2.0 * bRe * bIm - 4.0 * cIm;
                double size = Math.Sqrt(dRe * dRe + dIm * dIm);
                double rootRe = Math.Sqrt(Math.Max(0.0, 0.5 * (size + dRe)));
                double rootIm = (dIm < 0.0 ? -1.0 : 1.0) * Math.Sqrt(Math.Max(0.0, 0.5 * (size - dRe)));

                if (bRe * rootRe + bIm * rootIm < 0.0)
                {
                    rootRe = -rootRe;
                    rootIm = -rootIm;
                }

                re[0] = -0.5 * (bRe + rootRe);
                im[0] = -0.5 * (bIm + rootIm);
                double norm = re[0] * re[0] + im[0] * im[0];

                if (norm > 0.0)
                {
                    Divide(cRe, cIm, re[0], im[0], norm, out re[1], out im[1]);
                }

                count = 2;
            }

            int found = 0;

            for (int i = 0; i < count; i++)
            {
                if (!(re[i] != 0.0 || im[i] != 0.0) || double.IsNaN(re[i]) || double.IsNaN(im[i]))
                {
                    continue;
                }

                double angle = Polish(slope, Math.Atan2(im[i], re[i]));
                angle -= FullTurn * Math.Floor(angle / FullTurn);

                if (!(angle < FullTurn))
                {
                    angle = 0.0;
                }

                turns[found++] = angle;
            }

            Array.Sort(turns, 0, found);

            // Two roots that polished onto the same angle are one turn of the function, counted once.
            int kept = 0;

            for (int i = 0; i < found; i++)
            {
                if (kept == 0 || turns[i] - turns[kept - 1] > 1E-12)
                {
                    turns[kept++] = turns[i];
                }
            }

            if (kept > 1 && turns[0] + FullTurn - turns[kept - 1] <= 1E-12)
            {
                kept--;
            }

            return kept;
        }

        /// <summary>
        /// Divides one complex number by another whose squared size is given.
        /// </summary>
        private static void Divide(double re, double im, double byRe, double byIm, double byNorm, out double quotientRe, out double quotientIm)
        {
            quotientRe = (re * byRe + im * byIm) / byNorm;
            quotientIm = (im * byRe - re * byIm) / byNorm;
        }

        /// <summary>
        /// Finds the four roots of the monic polynomial z⁴ + c3 z³ + c2 z² + c1 z + c0 together.
        /// </summary>
        /// <remarks>
        /// Each round moves every estimate by Newton's step corrected for the pull of the others, which keeps two estimates
        /// from settling on the same root; the starts are spread round the unit circle, where the roots of these polynomials
        /// lie in pairs z and 1/z̄. A double root is reached more slowly, to half the digits, which is all a turning point
        /// needs: it only brackets the crossings either side of it, each of which is then found to the last digit.
        /// </remarks>
        private static void Aberth(
            double c3Re, double c3Im, double c2Re, double c2Im, double c1Re, double c1Im, double c0Re, double c0Im, double[] re, double[] im)
        {
            for (int k = 0; k < 4; k++)
            {
                double radius = 1.0 + 0.1 * k;
                double angle = 0.4 + 0.5 * Math.PI * k;
                re[k] = radius * Math.Cos(angle);
                im[k] = radius * Math.Sin(angle);
            }

            for (int round = 0; round < 100; round++)
            {
                double moved = 0.0;

                for (int k = 0; k < 4; k++)
                {
                    double zRe = re[k];
                    double zIm = im[k];

                    // The value and the slope of the polynomial, by Horner's rule.
                    double pRe = zRe + c3Re;
                    double pIm = zIm + c3Im;
                    double t = pRe * zRe - pIm * zIm + c2Re;
                    pIm = pRe * zIm + pIm * zRe + c2Im;
                    pRe = t;
                    t = pRe * zRe - pIm * zIm + c1Re;
                    pIm = pRe * zIm + pIm * zRe + c1Im;
                    pRe = t;
                    t = pRe * zRe - pIm * zIm + c0Re;
                    pIm = pRe * zIm + pIm * zRe + c0Im;
                    pRe = t;

                    if (pRe == 0.0 && pIm == 0.0)
                    {
                        continue;
                    }

                    double dRe = 4.0 * zRe + 3.0 * c3Re;
                    double dIm = 4.0 * zIm + 3.0 * c3Im;
                    t = dRe * zRe - dIm * zIm + 2.0 * c2Re;
                    dIm = dRe * zIm + dIm * zRe + 2.0 * c2Im;
                    dRe = t;
                    t = dRe * zRe - dIm * zIm + c1Re;
                    dIm = dRe * zIm + dIm * zRe + c1Im;
                    dRe = t;

                    double pullRe = 0.0;
                    double pullIm = 0.0;

                    for (int j = 0; j < 4; j++)
                    {
                        double aRe = zRe - re[j];
                        double aIm = zIm - im[j];
                        double aNorm = aRe * aRe + aIm * aIm;

                        if (j != k && aNorm > 0.0)
                        {
                            pullRe += aRe / aNorm;
                            pullIm -= aIm / aNorm;
                        }
                    }

                    double dNorm = dRe * dRe + dIm * dIm;
                    double stepRe;
                    double stepIm;

                    if (!(dNorm > 0.0))
                    {
                        stepRe = 1E-8 * (1.0 + Math.Sqrt(zRe * zRe + zIm * zIm));
                        stepIm = 0.0;
                    }
                    else
                    {
                        Divide(pRe, pIm, dRe, dIm, dNorm, out double ratioRe, out double ratioIm);

                        double denRe = 1.0 - (ratioRe * pullRe - ratioIm * pullIm);
                        double denIm = -(ratioRe * pullIm + ratioIm * pullRe);
                        double denNorm = denRe * denRe + denIm * denIm;

                        if (denNorm > 0.0)
                        {
                            Divide(ratioRe, ratioIm, denRe, denIm, denNorm, out stepRe, out stepIm);
                        }
                        else
                        {
                            stepRe = ratioRe;
                            stepIm = ratioIm;
                        }
                    }

                    re[k] = zRe - stepRe;
                    im[k] = zIm - stepIm;

                    double size = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                    moved = Math.Max(moved, Math.Sqrt(stepRe * stepRe + stepIm * stepIm) / Math.Max(1.0, size));
                }

                if (!(moved > 1E-15))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Polishes an angle at which a function is nought by Newton's method on the function; a start the steps carry far
        /// away, or do not bring down, is kept as it was.
        /// </summary>
        private static double Polish(Wave wave, double angle)
        {
            double start = angle;

            for (int step = 0; step < 4; step++)
            {
                double value = wave.At(angle, out double rate);

                if (!(Math.Abs(rate) > 0.0))
                {
                    break;
                }

                double move = value / rate;

                if (!(Math.Abs(move) <= 0.1))
                {
                    return start;
                }

                angle -= move;

                if (Math.Abs(move) <= 1E-14 * (1.0 + Math.Abs(angle)))
                {
                    return angle;
                }
            }

            return Math.Abs(wave.At(angle)) <= Math.Abs(wave.At(start)) ? angle : start;
        }

        /// <summary>
        /// Finds the one angle between two at which a function that runs one way between them is nought, the function taking
        /// opposite signs at the two ends.
        /// </summary>
        /// <param name="wave">The function.</param>
        /// <param name="low">The lower end of the bracket.</param>
        /// <param name="high">The upper end of the bracket.</param>
        /// <param name="atLow">The value at the lower end.</param>
        /// <param name="atHigh">The value at the upper end.</param>
        /// <remarks>
        /// Newton's method from the point where the chord between the ends crosses nought, kept inside the bracket, which it
        /// halves whenever a step would leave it, so the answer is right to the last digit and never lost.
        /// </remarks>
        internal static double Root(Wave wave, double low, double high, double atLow, double atHigh) => Root(wave, low, high, atLow, atHigh, double.NaN);

        /// <summary>
        /// Finds the one angle between two at which a function that runs one way between them is nought, starting from an
        /// angle known to lie near it.
        /// </summary>
        /// <param name="wave">The function.</param>
        /// <param name="low">The lower end of the bracket.</param>
        /// <param name="high">The upper end of the bracket.</param>
        /// <param name="atLow">The value at the lower end.</param>
        /// <param name="atHigh">The value at the upper end.</param>
        /// <param name="start">Where to start, or not a number for the point where the chord between the ends crosses nought.</param>
        internal static double Root(Wave wave, double low, double high, double atLow, double atHigh, double start)
        {
            bool lowBelow = atLow < 0.0;
            double angle = start > low && start < high ? start : low + (high - low) * (atLow / (atLow - atHigh));

            if (!(angle > low && angle < high))
            {
                angle = 0.5 * (low + high);
            }

            for (int step = 0; step < 200; step++)
            {
                double value = wave.At(angle, out double rate);

                if (value == 0.0)
                {
                    return angle;
                }

                if ((value < 0.0) == lowBelow)
                {
                    low = angle;
                }
                else
                {
                    high = angle;
                }

                double next = rate != 0.0 ? angle - value / rate : 0.5 * (low + high);

                // A step this short has reached the root: the next would be rounding, and might land on an end of the
                // bracket, which would send it back to halving.
                if (Math.Abs(next - angle) <= 1E-13 * (1.0 + Math.Abs(angle)))
                {
                    return next >= low && next <= high ? next : angle;
                }

                if (!(next > low && next < high))
                {
                    next = 0.5 * (low + high);
                }

                if (!(high - low > 4E-16 * (1.0 + Math.Abs(angle))))
                {
                    return next;
                }

                angle = next;
            }

            return angle;
        }

        /// <summary>
        /// Finds the least value of a function between two angles, by Brent's method: golden sections where a parabola through
        /// the last three points cannot be trusted, the parabola's vertex where it can.
        /// </summary>
        /// <param name="measure">The function.</param>
        /// <param name="low">The lower end of the bracket.</param>
        /// <param name="start">A point inside the bracket to start from, no higher than the ends if one is known.</param>
        /// <param name="high">The upper end of the bracket.</param>
        /// <param name="least">The least value found.</param>
        /// <returns>The angle of the least value found.</returns>
        internal static double Least(Func<double, double> measure, double low, double start, double high, out double least)
        {
            const double Golden = 0.3819660112501051;

            double a = low;
            double b = high;
            double x = start;
            double w = start;
            double v = start;
            double fx = measure(x);
            double fw = fx;
            double fv = fx;
            double d = 0.0;
            double e = 0.0;

            for (int round = 0; round < 200; round++)
            {
                double middle = 0.5 * (a + b);
                double tol1 = 1E-11 * (1.0 + Math.Abs(x));
                double tol2 = 2.0 * tol1;

                if (Math.Abs(x - middle) <= tol2 - 0.5 * (b - a))
                {
                    break;
                }

                bool golden = true;

                if (Math.Abs(e) > tol1)
                {
                    double r = (x - w) * (fx - fv);
                    double q = (x - v) * (fx - fw);
                    double p = (x - v) * q - (x - w) * r;
                    q = 2.0 * (q - r);

                    if (q > 0.0)
                    {
                        p = -p;
                    }
                    else
                    {
                        q = -q;
                    }

                    if (Math.Abs(p) < Math.Abs(0.5 * q * e) && p > q * (a - x) && p < q * (b - x))
                    {
                        e = d;
                        d = p / q;
                        double u0 = x + d;

                        if (u0 - a < tol2 || b - u0 < tol2)
                        {
                            d = x < middle ? tol1 : -tol1;
                        }

                        golden = false;
                    }
                }

                if (golden)
                {
                    e = x < middle ? b - x : a - x;
                    d = Golden * e;
                }

                double u = Math.Abs(d) >= tol1 ? x + d : x + (d > 0.0 ? tol1 : -tol1);
                double fu = measure(u);

                if (fu <= fx)
                {
                    if (u < x)
                    {
                        b = x;
                    }
                    else
                    {
                        a = x;
                    }

                    v = w;
                    fv = fw;
                    w = x;
                    fw = fx;
                    x = u;
                    fx = fu;
                }
                else
                {
                    if (u < x)
                    {
                        a = u;
                    }
                    else
                    {
                        b = u;
                    }

                    if (fu <= fw || w == x)
                    {
                        v = w;
                        fv = fw;
                        w = u;
                        fw = fu;
                    }
                    else if (fu <= fv || v == x || v == w)
                    {
                        v = u;
                        fv = fu;
                    }
                }
            }

            least = fx;
            return x;
        }
    }
}
