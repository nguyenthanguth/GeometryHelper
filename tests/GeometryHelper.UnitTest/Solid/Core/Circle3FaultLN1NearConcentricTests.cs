using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two circles in one plane whose centres are closer than the point tolerance, but which draw further apart than it
    /// somewhere, cross where they truly cross, as in the plane; two within the tolerance of each other all round are one.
    /// </summary>
    /// <remarks>
    /// The circle of radius 1000 about the origin and the circle of radius 1000.0005 about (0.0006, 0, 0), both in the
    /// plan and as much as 1.1E-3 apart in places, were given no crossing, though they cross at (-833.33324, 552.77094, 0)
    /// and (-833.33324, -552.77094, 0). Turned into a plane tilted 30 degrees about X they cross at the same points turned
    /// with them. The circle of radius 1000.00002 about (0.0001, 0, 0) stays within 1.2E-4 of the first all round, and is one
    /// circle with it.
    /// </remarks>
    public class Circle3FaultLN1NearConcentricTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private const double CrossingX = -833.33324164696101;

        private const double CrossingY = 552.77093661494875;

        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        private static void AssertCrossings(GeoPoint3[] crossings, GeoTransform3 turn)
        {
            Assert.Equal(2, crossings.Length);

            GeoPoint3 upper = turn.Transform(new GeoPoint3(CrossingX, CrossingY, 0));
            GeoPoint3 lower = turn.Transform(new GeoPoint3(CrossingX, -CrossingY, 0));
            bool firstIsUpper = crossings[0].DistanceTo(upper) < crossings[1].DistanceTo(upper);

            Assert.Equal(0.0, (firstIsUpper ? crossings[0] : crossings[1]).DistanceTo(upper), 9);
            Assert.Equal(0.0, (firstIsUpper ? crossings[1] : crossings[0]).DistanceTo(lower), 9);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void TwoCirclesApartByMoreThanTheToleranceNearOneCentre_CrossWhereTheyTrulyCross(double degrees)
        {
            GeoTransform3 turn = GeoTransform3.RotationX(degrees * Math.PI / 180.0);
            GeoVector3 normal = turn.Transform(Up);
            var first = new GeoCircle3(turn.Transform(new GeoPoint3(0, 0, 0)), normal, 1000);
            var second = new GeoCircle3(turn.Transform(new GeoPoint3(6E-4, 0, 0)), normal, 1000.0005);

            AssertCrossings(first.GetIntersections(second, Thousandth), turn);
            AssertCrossings(second.GetIntersections(first, Thousandth), turn);
            Assert.True(first.CollidesWith(second, Thousandth));
        }

        [Fact]
        public void ArcsOnThem_CrossWhereBothHoldACrossingOfTheCircles()
        {
            var first = new GeoArc3(new GeoPoint3(0, 0, 0), Up, 1000, 0.0, Math.PI);
            var second = new GeoArc3(new GeoPoint3(6E-4, 0, 0), Up, 1000.0005, 0.0, Math.PI);

            // Which of the two crossings the half turns hold depends on where the plane's axes put angle nought.
            var expected = new System.Collections.Generic.List<GeoPoint3>();

            foreach (GeoPoint3 crossing in new[] { new GeoPoint3(CrossingX, CrossingY, 0), new GeoPoint3(CrossingX, -CrossingY, 0) })
            {
                if (first.IsPointOn(crossing, Thousandth) && second.IsPointOn(crossing, Thousandth))
                {
                    expected.Add(crossing);
                }
            }

            Assert.NotEmpty(expected);

            foreach (GeoPoint3[] found in new[] { first.GetIntersections(second, Thousandth), second.GetIntersections(first, Thousandth) })
            {
                Assert.Equal(expected.Count, found.Length);

                foreach (GeoPoint3 crossing in expected)
                {
                    Assert.Contains(found, p => p.DistanceTo(crossing) < 1E-9);
                }
            }

            Assert.True(first.CollidesWith(second, Thousandth));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void TwoCirclesWithinTheToleranceOfEachOtherAllRound_AreOneCircle(double degrees)
        {
            GeoTransform3 turn = GeoTransform3.RotationX(degrees * Math.PI / 180.0);
            GeoVector3 normal = turn.Transform(Up);
            var first = new GeoCircle3(turn.Transform(new GeoPoint3(0, 0, 0)), normal, 1000);
            var second = new GeoCircle3(turn.Transform(new GeoPoint3(1E-4, 0, 0)), normal, 1000.00002);

            Assert.Empty(first.GetIntersections(second, Thousandth));
            Assert.Empty(second.GetIntersections(first, Thousandth));
            Assert.True(first.CollidesWith(second, Thousandth));
        }

        [Fact]
        public void CirclesAboutOneCentre_StillCrossNowhere()
        {
            var first = new GeoCircle3(new GeoPoint3(0, 0, 0), Up, 1000);

            Assert.Empty(first.GetIntersections(first, Thousandth));
            Assert.Empty(first.GetIntersections(new GeoCircle3(new GeoPoint3(0, 0, 0), Up, 1000.0005), Thousandth));
            Assert.Empty(first.GetIntersections(new GeoCircle3(new GeoPoint3(0, 0, 0), Up, 999), Thousandth));
        }
    }
}
