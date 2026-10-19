using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two infinite lines in space held to the gap between them worked out in exact arithmetic from the very doubles they are
    /// made of, whatever the angle between them: at a sine of 1E-13 to a half, at coordinates of a thousand to ten million.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gap between two lines of origins o1 and o2 and directions d1 and d2 is |(o2 - o1) . (d1 x d2)| / |d1 x d2|, the length of
    /// the common perpendicular, and where the two are parallel, the distance of one origin from the other, |(o2 - o1) x d1| / |d1|.
    /// Every double is a whole number of 2^-1074, so the arithmetic is done on whole numbers (<see cref="BigInteger"/>) and
    /// nothing is rounded but the last square root.
    /// </para>
    /// <para>
    /// The library reads two lines as parallel by rounding where their sine is below 1E-15, or is 1E-9 or less and their
    /// common perpendicular lies further along than a double can place within the point tolerance: (the larger distance along
    /// either line plus the largest coordinate of the two origins) times 2.2E-16 above the tolerance. So the oracle takes the
    /// exact gap where the library finds the perpendicular, and the nearer origin where it does not, and the library is held to
    /// it to a billionth where the sine is a millionth or more, and to what a double can place, that rounding of the reach, where
    /// it is less. Pairs within a tenth of the limit of the rule are not asked of it.
    /// </para>
    /// </remarks>
    public class XLine3ExactGapTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private const int Shift = 1100;

        private static BigInteger Whole(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;

            if (exponent == 0)
            {
                exponent++;
            }
            else
            {
                mantissa |= 1L << 52;
            }

            // value = mantissa * 2^(exponent - 1075); times 2^Shift is a whole number.
            BigInteger whole = new BigInteger(mantissa) << (exponent - 1075 + Shift);

            return negative ? -whole : whole;
        }

        private struct Whole3
        {
            internal BigInteger X, Y, Z;

            internal Whole3(double x, double y, double z)
            {
                X = Whole(x);
                Y = Whole(y);
                Z = Whole(z);
            }

            internal Whole3(BigInteger x, BigInteger y, BigInteger z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static Whole3 operator -(Whole3 a, Whole3 b) => new Whole3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

            internal BigInteger Dot(Whole3 b) => X * b.X + Y * b.Y + Z * b.Z;

            internal Whole3 Cross(Whole3 b) => new Whole3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        }

        /// <summary>The square root of a ratio of whole numbers, to a double, whatever their size.</summary>
        private static double SqrtOfRatio(BigInteger numerator, BigInteger denominator, int shiftDifference)
        {
            // numerator / (denominator * 2^shiftDifference), as a double: scaled to carry 128 bits, the divisor first.
            BigInteger scaled = (numerator << 128) / (denominator << shiftDifference);

            return Math.Sqrt((double)scaled / Math.Pow(2.0, 128));
        }

        /// <summary>The exact gap between two lines at a real angle: the length of their common perpendicular.</summary>
        private static double ExactGap(GeoXLine3 a, GeoXLine3 b, out double sine)
        {
            var d1 = new Whole3(a.Direction.X, a.Direction.Y, a.Direction.Z);
            var d2 = new Whole3(b.Direction.X, b.Direction.Y, b.Direction.Z);
            Whole3 r = new Whole3(b.Origin.X, b.Origin.Y, b.Origin.Z) - new Whole3(a.Origin.X, a.Origin.Y, a.Origin.Z);
            Whole3 n = d1.Cross(d2);
            BigInteger nn = n.Dot(n);
            BigInteger rn = r.Dot(n);

            // d1 and d2 are scaled by 2^S, so n is by 2^2S and r by 2^S: (r.n)^2 / n.n is a square of lengths, scaled by 2^(2S + 4S - 4S) = 2^2S.
            BigInteger dd1 = d1.Dot(d1);
            BigInteger dd2 = d2.Dot(d2);

            sine = nn.IsZero ? 0.0 : Math.Sqrt((double)((nn << 128) / (dd1 * dd2)) / Math.Pow(2.0, 128));
            return nn.IsZero ? double.NaN : SqrtOfRatio(rn * rn, nn, 2 * Shift);
        }

        /// <summary>How far along each line its nearest point to the other stands, exactly.</summary>
        private static void ExactAlong(GeoXLine3 a, GeoXLine3 b, out double first, out double second)
        {
            var d1 = new Whole3(a.Direction.X, a.Direction.Y, a.Direction.Z);
            var d2 = new Whole3(b.Direction.X, b.Direction.Y, b.Direction.Z);
            Whole3 r = new Whole3(b.Origin.X, b.Origin.Y, b.Origin.Z) - new Whole3(a.Origin.X, a.Origin.Y, a.Origin.Z);
            Whole3 n = d1.Cross(d2);
            BigInteger nn = n.Dot(n);

            // (r x d2) . n is scaled by 2^S, 2^S and 2^2S: 2^4S, as n . n is, so the ratio is of lengths and scaled by nothing.
            first = (double)((r.Cross(d2).Dot(n) << 128) / nn) / Math.Pow(2.0, 128);
            second = (double)((r.Cross(d1).Dot(n) << 128) / nn) / Math.Pow(2.0, 128);
        }

        /// <summary>The exact distance of the origin of one line from the other.</summary>
        private static double ExactOriginGap(GeoXLine3 line, GeoPoint3 point)
        {
            var d = new Whole3(line.Direction.X, line.Direction.Y, line.Direction.Z);
            Whole3 r = new Whole3(point.X, point.Y, point.Z) - new Whole3(line.Origin.X, line.Origin.Y, line.Origin.Z);
            BigInteger cross = r.Cross(d).Dot(r.Cross(d));
            BigInteger length = d.Dot(d);

            // |r x d|^2 / |d|^2, with r by 2^S and d by 2^S in both: scale 2^(4S - 2S) = 2^2S.
            return SqrtOfRatio(cross, length, 2 * Shift);
        }

        [Theory]
        [InlineData(1E3)]
        [InlineData(1E6)]
        public void ALineAndAPlaneAtAnyAngle_CrossWhereTheExactArithmeticPutsThem(double scale)
        {
            var random = new Random(77 + (int)Math.Log10(scale));
            var failures = new List<string>();
            int asked = 0;

            for (int i = 0; i < 6000; i++)
            {
                double angle = Math.Pow(10, -12 + 11.7 * random.NextDouble());
                var normal = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);
                var plane = new GeoPlane3(new GeoPoint3(scale * random.NextDouble(), scale * random.NextDouble(), scale * random.NextDouble()), normal);
                GeoVector3 axis = plane.Normal.CrossProduct(new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5));

                if (axis.Length < 1E-3)
                {
                    continue;
                }

                // A direction a given angle off the plane: square to the normal, then turned the angle towards it.
                GeoVector3 flat = axis.Divide(axis.Length);
                GeoVector3 direction = flat.Multiply(Math.Cos(angle)).Add(plane.Normal.Multiply(Math.Sin(angle)));
                var line = new GeoXLine3(new GeoPoint3(plane.Origin.X + (random.NextDouble() - 0.5) * 20, plane.Origin.Y + (random.NextDouble() - 0.5) * 20, plane.Origin.Z + (random.NextDouble() - 0.5) * 20), direction);

                // The exact distance along: the height of the plane's origin over the line's, per the climb, the origins' difference taken exactly.
                Whole3 n = new Whole3(plane.Normal.X, plane.Normal.Y, plane.Normal.Z);
                Whole3 d = new Whole3(line.Direction.X, line.Direction.Y, line.Direction.Z);
                Whole3 r = new Whole3(plane.Origin.X, plane.Origin.Y, plane.Origin.Z) - new Whole3(line.Origin.X, line.Origin.Y, line.Origin.Z);
                BigInteger climb = d.Dot(n);

                if (climb.IsZero)
                {
                    continue;
                }

                double expected = (double)((r.Dot(n) << 128) / climb) / Math.Pow(2.0, 128);
                double sine = Math.Abs((double)((climb << 128) / (BigInteger.One << (2 * Shift))) / Math.Pow(2.0, 128));
                double coordinate = new[] { line.Origin.X, line.Origin.Y, line.Origin.Z, plane.Origin.X, plane.Origin.Y, plane.Origin.Z }.Max(Math.Abs);
                double ratio = (Math.Abs(expected) + coordinate) * 2.2204460492503131E-16 / Strict.EqualPoint;

                if (sine <= 1E-15 || (sine <= 1E-9 && ratio > 0.5) || Math.Abs(sine) <= 1E-12 && Math.Abs(plane.SignedDistanceTo(line.Origin)) <= Strict.EqualPlanar)
                {
                    continue;
                }

                asked++;

                bool crosses = XLine3Probe_TryCross(line, plane, out double along);
                double slack = Math.Max(1E-9, 1E-9 * Math.Abs(expected)) + 16 * scale * 2.2204460492503131E-16 / Math.Max(sine, 1E-300);

                if (!crosses || Math.Abs(along - expected) > slack)
                {
                    failures.Add("sine " + sine + ": along " + along + " (" + crosses + ") against " + expected + "\n   " + line + "\n   " + plane);
                }
            }

            Assert.True(failures.Count == 0, failures.Count + " pairs went wrong:\n" + string.Join("\n", failures.Take(6)));
            Assert.True(asked > 3000, asked + " pairs were asked: the run is too thin");
        }

        private static bool XLine3Probe_TryCross(GeoXLine3 line, GeoPlane3 plane, out double along)
            => GeometryHelper.Core.XLine3.TryCrossPlane(line, plane, Strict, out along);

        [Fact]
        public void TheExactOracleItself_GivesThirtyForTheSkewLinesAndFiveForTheDirectionsOffAMicroradian()
        {
            Assert.Equal(30.0, ExactGap(new GeoXLine3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis), new GeoXLine3(new GeoPoint3(7, 3, 30), GeoVector3.YAxis), out double sine), 12);
            Assert.Equal(1.0, sine, 12);
            Assert.Equal(5.0, ExactGap(new GeoXLine3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis), new GeoXLine3(new GeoPoint3(0, 0, 5), new GeoVector3(1, 1E-9, 0)), out double tiny), 12);
            Assert.Equal(1E-9, tiny, 15);
            Assert.Equal(5.0, ExactOriginGap(new GeoXLine3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis), new GeoPoint3(40, 3, 4)), 12);
        }

        [Theory]
        [InlineData(1E3)]
        [InlineData(1E5)]
        [InlineData(1E7)]
        public void TwoLinesOfAnyAngle_AreAsFarApartAsTheirExactGap_OrAsTheNearerOriginWhereTheyAreParallelByRounding(double scale)
        {
            var random = new Random(1989 + (int)Math.Log10(scale));
            var failures = new List<string>();
            int common = 0;
            int parallel = 0;

            for (int i = 0; i < 6000; i++)
            {
                double angle = Math.Pow(10, -13 + 12.7 * random.NextDouble());
                var d = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);
                var a = new GeoXLine3(new GeoPoint3(scale * random.NextDouble(), scale * random.NextDouble(), scale * random.NextDouble()), d);

                // The second line tilted from the first by the angle, about an axis square to it, and its origin anywhere within a scale of it.
                GeoVector3 axis = a.Direction.CrossProduct(new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5));

                if (axis.Length < 1E-3)
                {
                    continue;
                }

                GeoVector3 tilted = a.Direction.Multiply(Math.Cos(angle)).Add(axis.Divide(axis.Length).Multiply(Math.Sin(angle)));
                var b = new GeoXLine3(new GeoPoint3(a.Origin.X + (random.NextDouble() - 0.5) * 100, a.Origin.Y + (random.NextDouble() - 0.5) * 100, a.Origin.Z + (random.NextDouble() - 0.5) * 100), tilted);

                double gap = ExactGap(a, b, out double sine);

                if (double.IsNaN(gap))
                {
                    continue;
                }

                // How far along either line the common perpendicular stands, from the exact arithmetic, and what the library reads the
                // reach to be: that, plus the largest coordinate of the two origins.
                ExactAlong(a, b, out double alongFirst, out double alongSecond);

                double coordinate = new[] { a.Origin.X, a.Origin.Y, a.Origin.Z, b.Origin.X, b.Origin.Y, b.Origin.Z }.Max(Math.Abs);
                double reach = Math.Max(Math.Abs(alongFirst), Math.Abs(alongSecond)) + coordinate;
                double ratio = reach * 2.2204460492503131E-16 / Strict.EqualPoint;

                // Pairs within an eighth of the limit of the rule are not asked of it: they hang on the last digit of a coordinate.
                if (sine > 1E-15 && sine <= 1E-9 && Math.Abs(Math.Log10(ratio)) < 0.05)
                {
                    continue;
                }

                bool placeable = sine > 1E-15 && (sine > 1E-9 || ratio <= 1.0);
                double expected = placeable ? gap : Math.Min(ExactOriginGap(a, b.Origin), ExactOriginGap(b, a.Origin));

                common += placeable ? 1 : 0;
                parallel += placeable ? 0 : 1;

                double fromA = a.DistanceTo(b, Strict);
                double fromB = b.DistanceTo(a, Strict);
                double slack = sine > 1E-6 ? 1E-9 * (1 + scale * 1E-3) : Math.Max(1E-9, 8 * reach * 2.2204460492503131E-16);

                // Where the lines have a common perpendicular the library places, its feet are the exact ones to the offset over the
                // sine, and the shortest line between the two is as long as the distance to what a double can place them to.
                if (placeable)
                {
                    bool bridged = XLine3.TryBridge(a, b, Strict, out double footA, out double footB, out _);
                    double footSlack = 1E-9 * (Math.Abs(alongFirst) + 1.0) + 256 * 2.2204460492503131E-16 * 100.0 / sine;
                    double shortest = a.GetShortestLineTo(b, Strict).Length;

                    if (!bridged || Math.Abs(footA - alongFirst) > footSlack || Math.Abs(footB - alongSecond) > footSlack
                        || Math.Abs(shortest - expected) > slack + 8 * reach * 2.2204460492503131E-16)
                    {
                        failures.Add("sine " + sine + " scale " + scale + ": feet " + footA + ", " + footB + " against " + alongFirst + ", " + alongSecond + ", shortest " + shortest + " against " + expected);
                    }
                }

                if (fromA != fromB || Math.Abs(fromA - expected) > slack)
                {
                    failures.Add("sine " + sine + " scale " + scale + ": " + fromA + " (" + fromB + ") against " + expected + "\n   " + a + "\n   " + b);
                }
            }

            Assert.True(failures.Count == 0, failures.Count + " pairs went wrong:\n" + string.Join("\n", failures.Take(6)));
            Assert.True(common > 1000 && parallel > 100, common + " pairs at a real angle and " + parallel + " parallel by rounding were asked: the run is too thin");
        }
    }
}
