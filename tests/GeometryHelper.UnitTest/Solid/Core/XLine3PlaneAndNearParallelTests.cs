using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Shapes;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The line against a plane where the planar tolerance is not the point tolerance and where the tilt is rounding, and the
    /// shortest line between two lines that run nearly side by side.
    /// </summary>
    /// <remarks>
    /// A line starting within the planar tolerance of a plane touches it, as a ray does: nought from it, collides with it, and
    /// names no crossing where it lies in it. A line that starts further off crosses it only where a double can place the
    /// crossing, so one 100 above the floor and tilted 1E-11 reads 100 from it, which its own ray, reaching the floor at 1E13
    /// along, reads as nought.
    /// </remarks>
    public class XLine3PlaneAndNearParallelTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Wide = new Tolerance(1E-3, 1E-5, Math.PI / 180, 5E-2);

        private static double Off(GeoXLine3 line, GeoPoint3 point) => line.Direction.CrossProduct(line.Origin.GetVectorTo(point)).Length;

        [Fact]
        public void TheDistanceToAPlane_NeverFallsAsTheLineIsRaised_AtAnyTiltAndAnyTolerance()
        {
            var random = new Random(12);
            double[] tilts = { 0.0, 1E-14, 1E-13, 1E-12, 1E-11, 1E-10, 1E-9, 1E-7, 1E-4, 0.3 };
            double[] heights = { 0.0, 3E-4, 9E-4, 2E-3, 0.04, 0.06, 1, 7, 100, 1000 };
            var tolerances = new[] { Strict, Wide, new Tolerance(1E-3, 1E-5, Math.PI / 180, 1E-6), Tolerance.Default };
            int wrong = 0;

            foreach (Tolerance tolerance in tolerances)
            {
                foreach (double tilt in tilts)
                {
                    for (int trial = 0; trial < 50; trial++)
                    {
                        var normal = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalize();
                        var plane = new GeoPlane3(new GeoPoint3(random.NextDouble() * 1000, random.NextDouble() * 1000, random.NextDouble() * 1000), normal);
                        GeoVector3 flat = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).CrossProduct(plane.Normal).Normalize();
                        GeoVector3 direction = flat.Multiply(Math.Sqrt(1 - tilt * tilt)).Add(plane.Normal.Multiply(tilt));
                        GeoPoint3 foot = plane.Origin.Add(plane.Normal.CrossProduct(flat).Multiply(random.NextDouble() * 1000));
                        double previous = -1.0;

                        foreach (double height in heights)
                        {
                            var line = new GeoXLine3(foot.Add(plane.Normal.Multiply(height)), direction);
                            double distance = line.DistanceTo(plane, tolerance);

                            wrong += distance < previous - 1E-9 ? 1 : 0;
                            wrong += BitConverter.DoubleToInt64Bits(distance) != BitConverter.DoubleToInt64Bits(plane.DistanceTo(line, tolerance)) ? 1 : 0;

                            // Within the planar tolerance of the plane it touches, and anywhere it comes within the point tolerance.
                            double exact = Math.Abs(plane.SignedDistanceTo(line.Origin));

                            wrong += exact <= tolerance.EqualPlanar && (distance != 0.0 || !line.CollidesWith(plane, tolerance)) ? 1 : 0;
                            wrong += line.CollidesWith(plane, tolerance) != (distance <= tolerance.EqualPoint) ? 1 : 0;
                            previous = distance;
                        }
                    }
                }
            }

            Assert.Equal(0, wrong);
        }

        [Fact]
        public void ALineAFortiethAboveThePlane_RunningAlongIt_TouchesItWherThePlanarToleranceIsFiveHundredths()
        {
            // The heights and the answers of the table the review drew, at a tilt of a hair and none.
            var floor = Floor();

            foreach (double tilt in new[] { 0.0, 1E-13 })
            {
                var direction = new GeoVector3(1, 0, tilt);

                Assert.Equal(0.0, new GeoXLine3(new GeoPoint3(0, 0, 0.002), direction).DistanceTo(floor, Wide));
                Assert.Equal(0.0, new GeoXLine3(new GeoPoint3(0, 0, 0.04), direction).DistanceTo(floor, Wide));
                Assert.True(new GeoXLine3(new GeoPoint3(0, 0, 0.04), direction).CollidesWith(floor, Wide));
                Assert.Equal(tilt == 0.0 ? 0.06 : 0.0, new GeoXLine3(new GeoPoint3(0, 0, 0.06), direction).DistanceTo(floor, Wide), 12);
            }

            Assert.Equal(1.0, new GeoXLine3(new GeoPoint3(0, 0, 1), new GeoVector3(1, 0, 0)).DistanceTo(floor, Wide));
            Assert.False(new GeoXLine3(new GeoPoint3(0, 0, 1), new GeoVector3(1, 0, 0)).CollidesWith(floor, Wide));

            // It names no crossing where it lies in the plane.
            Assert.False(new GeoXLine3(new GeoPoint3(0, 0, 0.04), GeoVector3.XAxis).TryIntersectWith(floor, out _, Wide));
        }

        [Fact]
        public void ALineAHundredAboveAndTiltedARoundingOff_ReadsAHundredFromThePlane_WhereItsRayReadsNought()
        {
            // The line crosses the floor at 1E13 along, which a double cannot place within a thousandth; its ray ahead of the origin
            // runs down to it, and reads the crossing at any distance.
            var line = new GeoXLine3(new GeoPoint3(0, 0, 100), new GeoVector3(1, 0, -1E-11));

            Assert.Equal(100.0, line.DistanceTo(Floor(), Strict), 9);
            Assert.False(line.CollidesWith(Floor(), Strict));
            Assert.False(line.TryIntersectWith(Floor(), out _, Strict));
            Assert.Equal(0.0, line.ToRay().DistanceTo(Floor(), Strict));
            Assert.True(line.ToRay().CollidesWith(Floor(), Strict));

            // At 1E-9 the crossing is 1E11 along, which it can place, and the line reads nought.
            Assert.Equal(0.0, new GeoXLine3(new GeoPoint3(0, 0, 100), new GeoVector3(1, 0, -1E-9)).DistanceTo(Floor(), Strict));
        }

        [Theory]
        [InlineData(1E-13)]
        [InlineData(1E-12)]
        [InlineData(1E-11)]
        [InlineData(1E-10)]
        [InlineData(1E-9)]
        [InlineData(1E-6)]
        public void TwoLinesAHundredApartWhoseCommonPerpendicularStandsAtTheirOrigins_AreJoinedBySegmentsAsLongAsTheirDistance(double sine)
        {
            var random = new Random(3);

            for (int i = 0; i < 400; i++)
            {
                double scale = i % 2 == 0 ? 1E3 : 1E6;
                var a = new GeoXLine3(new GeoPoint3((random.NextDouble() - 0.5) * scale, (random.NextDouble() - 0.5) * scale, (random.NextDouble() - 0.5) * scale), new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5));
                GeoVector3 across = a.Direction.CrossProduct(new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5)).Normalize();
                GeoVector3 mutual = a.Direction.CrossProduct(across).Normalize();

                // The second is turned from the first about `across` by the sine, and stands a hundred off along the common perpendicular.
                var b = new GeoXLine3(a.Origin.Add(mutual.Multiply(100.0)), a.Direction.Multiply(Math.Sqrt(1 - sine * sine)).Add(across.Multiply(sine)));
                double distance = a.DistanceTo(b, Strict);
                GeoLine3 forward = a.GetShortestLineTo(b, Strict);
                GeoLine3 backward = b.GetShortestLineTo(a, Strict);

                // The turned line is the sine only to the rounding of its components, so its true gap is a hair under a hundred below 1E-9.
                double rounding = sine < 1E-9 ? 1E-4 : 1E-8;

                Assert.InRange(distance, 100.0 - rounding, 100.0 + 1E-8);

                // The segment is as long as the distance to what a double can place its feet to: a rounding of the larger distance along
                // the lines, plus the largest coordinate, which the rule that reads two lines parallel by rounding holds below the tolerance.
                Assert.True(XLine3.TryBridge(a, b, Strict, out double alongA, out double alongB, out _));

                double reach = Math.Max(Math.Abs(alongA), Math.Abs(alongB)) + 1E6;
                double within = 1E-9 + 8 * reach * 2.2204460492503131E-16;

                Assert.InRange(forward.Length, distance - within, distance + within);
                Assert.InRange(backward.Length, distance - within, distance + within);
                Assert.True(Off(a, forward.StartPoint) <= 1E-7 + within && Off(b, forward.EndPoint) <= 1E-7 + within, "the feet are off the lines at sine " + sine);
                Assert.Equal(forward.StartPoint, backward.EndPoint);
                Assert.Equal(forward.EndPoint, backward.StartPoint);
            }
        }

        [Theory]
        [InlineData(1E-13)]
        [InlineData(1E-12)]
        [InlineData(1E-11)]
        [InlineData(1E-10)]
        [InlineData(1E-9)]
        public void TwoLinesThatCrossATenthousandthApartAtAHairOfAnAngle_AreJoinedBySegmentsOfNoLength(double sine)
        {
            var random = new Random(4);

            for (int i = 0; i < 400; i++)
            {
                var a = new GeoXLine3(new GeoPoint3((random.NextDouble() - 0.5) * 1E3, (random.NextDouble() - 0.5) * 1E3, (random.NextDouble() - 0.5) * 1E3), new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5));
                GeoVector3 across = a.Direction.CrossProduct(new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5)).Normalize();

                // Their origins 1E-4 apart along `across`, in the plane of both: they cross 1E-4 / sine along, up to 1E9.
                var b = new GeoXLine3(a.Origin.Add(across.Multiply(1E-4)), a.Direction.Multiply(Math.Sqrt(1 - sine * sine)).Add(across.Multiply(sine)));
                double reach = 1E-4 / sine + 500;
                double allowed = 1E-9 + 8 * reach * 2.2204460492503131E-16;

                Assert.InRange(a.DistanceTo(b, Strict), 0.0, allowed);
                Assert.InRange(a.GetShortestLineTo(b, Strict).Length, 0.0, allowed);
                Assert.InRange(b.GetShortestLineTo(a, Strict).Length, 0.0, allowed);
            }
        }
    }
}
