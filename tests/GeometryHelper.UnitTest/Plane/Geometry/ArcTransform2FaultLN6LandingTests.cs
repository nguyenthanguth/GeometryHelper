using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An arc in the plane is moved only where it lands within the global point tolerance of where the transformation puts it
    /// everywhere along it, not only at the sixteen points asked, and an arc, a circle or a rectangle under a transformation
    /// round to the rounding of its numbers is judged by how far it lands too.
    /// </summary>
    /// <remarks>
    /// Asked at every sixteenth of its sweep, the arc of radius 87.66 from 0.2 radians sweeping 1.51, under X stretched by
    /// 1 + 1.793E-6 and scaled by 84.1, landed 0.0010114 off between two of the points, and was moved. And a transformation
    /// whose axes are one length and square to each other to a share of 1E-9 moved any arc and any circle whole: X stretched
    /// by a share of 4E-10 and scaled by 1 000 took the arc of radius 1E5 to one 0.04 off.
    /// </remarks>
    public class ArcTransform2FaultLN6LandingTests
    {
        private static GeoTransform2 Stretch(double scale, double share)
            => new GeoTransform2(new double[,] { { scale * (1 + share), 0, 5 }, { 0, scale, 7 }, { 0, 0, 1 } });

        /// <summary>The farthest any of 4 097 points of the arc, moved, stands off the moved arc's circle.</summary>
        private static double Worst(GeoArc2 arc, GeoTransform2 map, GeoArc2 moved)
        {
            double worst = 0.0;

            for (int i = 0; i <= 4096; i++)
            {
                GeoPoint2 point = map.Transform(arc.GetPointAtParameter(i / 4096.0));
                worst = Math.Max(worst, Math.Abs(point.DistanceTo(moved.Center) - moved.Radius));
            }

            return worst;
        }

        [Fact]
        public void AnArcLandingOffBetweenTheSixteenPoints_IsRefused()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 87.66, 0.2, 0.2 + 1.51, false);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(Stretch(84.1, 1.793E-6)));
            Assert.Throws<InvalidOperationException>(() => new GeoEdge2(arc).TransformBy(Stretch(84.1, 1.793E-6)));
        }

        [Fact]
        public void TheSameArcStretchedLess_StillMovesWithinTheTolerance()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 87.66, 0.2, 0.2 + 1.51, false);
            GeoTransform2 map = Stretch(84.1, 1.5E-6);

            Assert.True(Worst(arc, map, arc.TransformBy(map)) <= 1E-3);
        }

        [Theory]
        [InlineData(3.0)]
        [InlineData(6.0)]
        public void AnArcOfRadius1E5UnderARoundTransformationScaledByAThousand_IsRefusedWhereItWouldLandOff(double sweep)
        {
            var arc = new GeoArc2(new GeoPoint2(3, 4), 1E5, 0.3, 0.3 + sweep, false);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(Stretch(1000, 4E-10)));
        }

        [Fact]
        public void AShortArcOfRadius1E5UnderTheSame_GoesThroughWhereItsEndsAndMiddleLand()
        {
            var arc = new GeoArc2(new GeoPoint2(3, 4), 1E5, 0.3, 0.3 + 1.0, false);
            GeoTransform2 map = Stretch(1000, 4E-10);

            GeoArc2 moved = arc.TransformBy(map);

            Assert.True(Worst(arc, map, moved) <= 1E-3);
        }

        [Fact]
        public void ACircleAndARectangleUnderTheSame_AreRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Stretch(1000, 4E-10).Transform(new GeoCircle2(new GeoPoint2(3, 4), 1E5)));
            Assert.Throws<InvalidOperationException>(() => Stretch(1000, 4E-10).Transform(new GeoRectangle2(new GeoPoint2(0, 0), 2E8, 1E8, 0.3)));
        }

        [Fact]
        public void AnEvenScalingByAThousand_StillMovesAnyArcAndCircleWhole()
        {
            var arc = new GeoArc2(new GeoPoint2(3, 4), 1E5, 0.3, 0.3 + 6.0, false);
            GeoTransform2 map = Stretch(1000, 0.0);

            GeoArc2 moved = arc.TransformBy(map);

            Assert.Equal(1E8, moved.Radius, 6);
            Assert.True(Worst(arc, map, moved) <= 1E-6);
            Assert.Equal(1E8, map.Transform(new GeoCircle2(new GeoPoint2(3, 4), 1E5)).Radius, 6);
        }

        [Fact]
        public void AnArcMovedWholeWhoseStartLandsOff_IsRefusedOrLandsWithinTheTolerance()
        {
            // Moved whole, its start was never asked.
            var arc = new GeoArc2(new GeoPoint2(3, 4), 2.63E6, 1.77, 2.77, false);
            GeoTransform2 map = Stretch(1, 4E-10);
            GeoArc2 moved;

            try
            {
                moved = arc.TransformBy(map);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            Assert.True(Worst(arc, map, moved) <= 1E-3);
        }
    }
}
