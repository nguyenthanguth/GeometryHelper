using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A loop that may curve reads a point near the chord of a bowed edge by the true boundary, not by the chord.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Whether a curved loop holds a point was read from the straight loop through its vertices, within the tolerance,
    /// turned inside out once for every piece an arc cuts off its chord that holds the point, where the piece's test
    /// left out every point within the tolerance of the chord. A point within the tolerance of a chord that is not the
    /// boundary was therefore inside the straight loop and in no piece, whichever way the arc bowed.
    /// </para>
    /// <para>
    /// The square from the origin to (100, 100) with its top bowed 20 inwards (bulge -0.4) bounds nothing above the arc
    /// through (50, 80), on the circle of radius 72.5 about (50, 152.5). The point (50, 99.95) lies 0.05 under the chord
    /// and 19.95 above the arc, outside; at a tolerance of 0.1 it was inside and nought away. A point is now on the side
    /// within the tolerance of the true boundary only, and otherwise in or out by the exact rule.
    /// </para>
    /// </remarks>
    public class SegmentFaultB2BowedEdgeTests
    {
        private static Tolerance Of(double equalPoint) => new Tolerance(equalPoint, 1E-5);

        [Fact]
        public void APointFiveHundredthsUnderTheChordOfAnInwardBow_IsOutsideAtATenth()
        {
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);
            GeoPoint2 point = P(50, 99.95);

            Assert.False(loop.Contains(point, Tenth));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(loop, point, Tenth));
            Assert.Equal(19.95, loop.DistanceTo(point, Tenth), 9);
        }

        [Theory]
        [InlineData(1E-3, 99.9995, 19.9995)]
        [InlineData(1E-4, 99.99995, 19.99995)]
        public void APointHalfTheToleranceUnderTheChordOfAnInwardBow_IsOutside(double equalPoint, double y, double distance)
        {
            // Half the tolerance under the chord, as (50, 99.95) is at 0.1; the arc is y - 80 below it.
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);
            GeoPoint2 point = P(50, y);

            Assert.False(loop.Contains(point, Of(equalPoint)));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(loop, point, Of(equalPoint)));
            Assert.Equal(distance, loop.DistanceTo(point, Of(equalPoint)), 9);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-4)]
        public void APointFiveHundredthsUnderTheChordOfAnInwardBow_IsOutsideAtTighterTolerances(double equalPoint)
        {
            // 0.05 under the chord is beyond these tolerances of it.
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);
            GeoPoint2 point = P(50, 99.95);

            Assert.False(loop.Contains(point, Of(equalPoint)));
            Assert.Equal(19.95, loop.DistanceTo(point, Of(equalPoint)), 9);
        }

        [Theory]
        [InlineData(0.1)]
        [InlineData(1E-3)]
        public void APointExactlyOnTheChordOfAnInwardBow_IsOutside(double equalPoint)
        {
            // (50, 100) is on the chord, which is not the boundary, and 20 above the arc.
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);
            GeoPoint2 point = P(50, 100);

            Assert.False(loop.Contains(point, Of(equalPoint)));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(loop, point, Of(equalPoint)));
            Assert.Equal(20.0, loop.DistanceTo(point, Of(equalPoint)), 9);
        }

        [Theory]
        [InlineData(99.95)]
        [InlineData(100.0)]
        [InlineData(100.05)]
        public void PointsNearTheChordOfAnOutwardBow_AreInsideAtATenth(double y)
        {
            // Bowed 20 outwards, the top bounds the cap up to (50, 120): the chord runs through the inside, 20 from the arc.
            GeoPolygonArc2 loop = SquareWithBowedTop(0.4);
            GeoPoint2 point = P(50, y);

            Assert.True(loop.Contains(point, Tenth));
            Assert.Equal(PointLocation.Inside, Containment2.Locate(loop, point, Tenth));
            Assert.Equal(0.0, loop.DistanceTo(point, Tenth));
        }

        [Fact]
        public void APointInTheCapOfAnOutwardBow_IsInside()
        {
            // (50, 110) is 10 above the chord and 10 under the arc.
            GeoPolygonArc2 loop = SquareWithBowedTop(0.4);

            Assert.Equal(PointLocation.Inside, Containment2.Locate(loop, P(50, 110), Thousandth));
        }

        [Fact]
        public void APointBetweenTheChordAndTheArcOfAnInwardBow_IsOutsideByItsGap()
        {
            // (50, 90) is 10 under the chord and 10 above the arc.
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);

            Assert.Equal(PointLocation.OutSide, Containment2.Locate(loop, P(50, 90), Thousandth));
            Assert.Equal(10.0, loop.DistanceTo(P(50, 90), Thousandth), 9);
        }

        [Fact]
        public void APointUnderTheArcOfAnInwardBow_IsInside()
        {
            // (50, 70) is 10 under the arc, inside what the loop bounds.
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);

            Assert.Equal(PointLocation.Inside, Containment2.Locate(loop, P(50, 70), Thousandth));
        }

        [Fact]
        public void APointWithinTheToleranceOfTheArc_IsOnTheSide()
        {
            // Half a thousandth above the bottom of the inward bow, (50, 80).
            GeoPolygonArc2 loop = SquareWithBowedTop(-0.4);

            Assert.Equal(PointLocation.OnSide, Containment2.Locate(loop, P(50, 80.0005), Thousandth));
        }
    }
}
