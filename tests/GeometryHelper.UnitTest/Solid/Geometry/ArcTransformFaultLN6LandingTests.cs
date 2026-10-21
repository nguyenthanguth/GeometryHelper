using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc in space is moved only where it lands within the global point tolerance of where the transformation puts it
    /// everywhere along it, and an arc, a circle or a ball under a transformation round to the rounding of its numbers is
    /// judged by how far it lands too.
    /// </summary>
    /// <remarks>
    /// Asked at every sixteenth of its sweep, the arc of radius 82.39 in the plane with normal (0, 0.6, 0.8), from 1.46
    /// radians sweeping 3.48 back, under X stretched by 1 + 2.13E-7 and scaled by 97.4, landed 0.0010133 off between two of
    /// the points, and was moved. A transformation whose axes are one length and square to each other to a share of 1E-9
    /// moved any arc, circle and ball whole: X stretched by a share of 4E-10 and scaled by 1 000 took the arc of radius 1E5
    /// to one 0.04 off, and so the circle and the ball.
    /// </remarks>
    public class ArcTransformFaultLN6LandingTests
    {
        private static readonly GeoVector3 Tilt = new GeoVector3(0, 0.6, 0.8);

        private static GeoTransform3 Stretch(double scale, double share)
            => new GeoTransform3(new double[,] { { scale * (1 + share), 0, 0, 5 }, { 0, scale, 0, 7 }, { 0, 0, scale, 9 }, { 0, 0, 0, 1 } });

        /// <summary>The farthest any of 4 097 points of the arc, moved, stands off the moved arc's circle.</summary>
        private static double Worst(GeoArc3 arc, GeoTransform3 map, GeoArc3 moved)
        {
            double worst = 0.0;
            GeoVector3 normal = moved.Normal;

            for (int i = 0; i <= 4096; i++)
            {
                GeoVector3 from = moved.Center.GetVectorTo(map.Transform(arc.GetPointAtParameter(i / 4096.0)));
                double height = from.DotProduct(normal);
                double off = from.Subtract(normal.Multiply(height)).Length - moved.Radius;
                worst = Math.Max(worst, Math.Sqrt(height * height + off * off));
            }

            return worst;
        }

        [Fact]
        public void AnArcLandingOffBetweenTheSixteenPoints_IsRefused()
        {
            var arc = new GeoArc3(new GeoPoint3(0, 0, 0), Tilt, 82.39, 1.46, 1.46 - 3.48, true);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(Stretch(97.4, 2.13E-7)));
            Assert.Throws<InvalidOperationException>(() => new GeoEdge3(arc).TransformBy(Stretch(97.4, 2.13E-7)));
        }

        [Fact]
        public void TheSameArcStretchedLess_StillMovesWithinTheTolerance()
        {
            var arc = new GeoArc3(new GeoPoint3(0, 0, 0), Tilt, 82.39, 1.46, 1.46 - 3.48, true);
            GeoTransform3 map = Stretch(97.4, 1.8E-7);

            Assert.True(Worst(arc, map, arc.TransformBy(map)) <= 1E-3);
        }

        [Fact]
        public void AnArcOfRadius1E5UnderARoundTransformationScaledByAThousand_IsRefusedWhereItWouldLandOff()
        {
            var arc = new GeoArc3(new GeoPoint3(3, 4, 5), GeoVector3.ZAxis, 1E5, 0.3, 6.3, false);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(Stretch(1000, 4E-10)));
            Assert.Throws<InvalidOperationException>(() => new GeoEdge3(arc).TransformBy(Stretch(1000, 4E-10)));
        }

        [Fact]
        public void AShortArcOfRadius1E5UnderTheSame_LandsWithinTheTolerance()
        {
            var arc = new GeoArc3(new GeoPoint3(3, 4, 5), GeoVector3.ZAxis, 1E5, 0.3, 1.3, false);
            GeoTransform3 map = Stretch(1000, 4E-10);

            Assert.True(Worst(arc, map, arc.TransformBy(map)) <= 1E-3);
            Assert.True(Worst(arc, map, new GeoEdge3(arc).TransformBy(map).ToArc()) <= 1E-3);
        }

        [Fact]
        public void ACircleAndABallUnderTheSame_AreRefused()
        {
            Assert.Throws<InvalidOperationException>(() => new GeoCircle3(new GeoPoint3(3, 4, 5), GeoVector3.ZAxis, 1E5).TransformBy(Stretch(1000, 4E-10)));
            Assert.Throws<InvalidOperationException>(() => new GeoSphere3(new GeoPoint3(3, 4, 5), 1E5).TransformBy(Stretch(1000, 4E-10)));
            Assert.Throws<InvalidOperationException>(() => new GeoSphere3(new GeoPoint3(3, 4, 5), 1E8).TransformBy(Stretch(1, 4E-10)));
        }

        [Fact]
        public void AnEvenScalingByAThousand_StillMovesAnyArcCircleAndBallWhole()
        {
            var arc = new GeoArc3(new GeoPoint3(3, 4, 5), GeoVector3.ZAxis, 1E5, 0.3, 6.3, false);
            GeoTransform3 map = Stretch(1000, 0.0);

            Assert.True(Worst(arc, map, arc.TransformBy(map)) <= 1E-6);
            Assert.Equal(1E8, new GeoCircle3(new GeoPoint3(3, 4, 5), GeoVector3.ZAxis, 1E5).TransformBy(map).Radius, 6);
            Assert.Equal(1E8, new GeoSphere3(new GeoPoint3(3, 4, 5), 1E5).TransformBy(map).Radius, 6);
        }

        [Fact]
        public void AnArcMovedWholeWhoseStartLandsOff_IsRefusedOrLandsWithinTheTolerance()
        {
            // Moved whole, its start was never asked, and landed 0.0010108 off.
            var arc = new GeoArc3(new GeoPoint3(3, 4, 5), GeoVector3.ZAxis, 2.63E6, 1.77, 2.77, false);
            GeoTransform3 map = Stretch(1, 4E-10);
            GeoArc3 moved;

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
