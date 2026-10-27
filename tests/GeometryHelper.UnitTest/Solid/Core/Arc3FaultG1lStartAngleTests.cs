using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc in space starts at an angle from nought up to a full turn, as its start angle says, however it was made. An arc
    /// moved by a transformation and one through three points took the angle of their start as it came, which on the lower
    /// half of the circle is below nought: the whole turn from 1 radian turned 2.5 about Z started at -2.78, and the half
    /// circle from (100, 0, 0) through (0, 100, 0) to (-100, 0, 0) at -1.57. They start at 3.5 and at 4.71 now, at the same
    /// point.
    /// </summary>
    /// <remarks>
    /// Every arc here is of radius 100 about the origin, in the plan, where angles are measured from Y round towards -X.
    /// </remarks>
    public class Arc3FaultG1lStartAngleTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        private static void AssertWithinATurn(GeoArc3 arc)
        {
            Assert.InRange(arc.StartAngle, 0.0, 2.0 * Math.PI);
            Assert.NotEqual(2.0 * Math.PI, arc.StartAngle);
        }

        /// <summary>Asserts that the moved arc lies where the transformation puts the arc's points, every eighth of the way.</summary>
        private static void AssertMoved(GeoArc3 arc, GeoTransform3 transform, GeoArc3 moved)
        {
            for (int eighth = 0; eighth <= 8; eighth++)
            {
                GeoPoint3 expected = transform.Transform(arc.GetPointAtParameter(eighth / 8.0));

                Assert.True(moved.GetPointAtParameter(eighth / 8.0).IsEqualTo(expected, Tight), $"{eighth} eighths along");
            }
        }

        [Theory]
        [InlineData(1.0, 1.0, false)]
        [InlineData(1.0, 2.0, false)]
        [InlineData(1.0, 0.0, true)]
        [InlineData(1.0, 1.0, true)]
        public void AnArcTurnedPastHalfATurn_StartsWithinATurn(double start, double end, bool clockwise)
        {
            // Turned 2.5 about Z, the start at 1 radian moves to 3.5, which Atan2 read as -2.78.
            var arc = new GeoArc3(GeoPoint3.Origin, Up, 100, start, end, clockwise);
            GeoTransform3 turn = GeoTransform3.RotationZ(2.5);

            GeoArc3 moved = arc.TransformBy(turn);

            AssertWithinATurn(moved);
            Assert.Equal(Math.Abs(arc.SweptAngle), Math.Abs(moved.SweptAngle));
            AssertMoved(arc, turn, moved);
        }

        [Fact]
        public void TheHalfCircleThroughThreePointsOnItsLowerHalf_StartsWithinATurn()
        {
            // From (100, 0, 0), a quarter turn below the zero angle on Y: it started at -1.57.
            GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0), new GeoPoint3(-100, 0, 0), Tight);

            AssertWithinATurn(arc);
            Assert.InRange(arc.StartAngle, 1.5 * Math.PI - 1E-12, 1.5 * Math.PI + 1E-12);
            Assert.True(arc.StartPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Tight));
            Assert.True(arc.MidPoint.IsEqualTo(new GeoPoint3(0, 100, 0), Tight));
            Assert.True(arc.EndPoint.IsEqualTo(new GeoPoint3(-100, 0, 0), Tight));
        }

        [Fact]
        public void AnArcBuiltFromARoundingBelowNought_StartsAtNoughtNotAWholeTurn()
        {
            // -1E-17 and a turn come to a whole turn exactly, which the start was, in space and in the plane.
            var arc = new GeoArc3(GeoPoint3.Origin, Up, 100, -1E-17, 1.0);
            var flat = new GeoArc2(GeoPoint2.Origin, 100, -1E-17, 1.0);

            AssertWithinATurn(arc);
            Assert.Equal(0.0, arc.StartAngle);
            Assert.Equal(0.0, flat.StartAngle);
            Assert.InRange(arc.SweptAngle, 1.0 - 1E-15, 1.0 + 1E-15);
            Assert.InRange(flat.SweptAngle, 1.0 - 1E-15, 1.0 + 1E-15);
        }

        [Fact]
        public void AClockwiseArcCutJustAfterItsStart_HasItsSecondPieceStartWithinATurn()
        {
            // The cut 1E-17 of the way along the arc from nought sweeping -1 lies at -1E-17, which the second piece started at
            // as a whole turn.
            var arc = new GeoArc3(GeoPoint3.Origin, Up, 100, 0.0, -1.0, true);

            Assert.True(arc.TrySplitAt(1E-17, out GeoArc3[] pieces, default(Tolerance)));
            AssertWithinATurn(pieces[0]);
            AssertWithinATurn(pieces[1]);
            Assert.True(pieces[1].StartPoint.IsEqualTo(arc.StartPoint, Tight));
            Assert.True(pieces[1].EndPoint.IsEqualTo(arc.EndPoint, Tight));
        }

        [Fact]
        public void AnArcTurnedARoundingBackwards_StartsAtNoughtNotAWholeTurn()
        {
            // Turned -1E-17 about Z, the start at nought reads -1E-17, which a turn added to comes to a whole turn exactly.
            var arc = new GeoArc3(GeoPoint3.Origin, Up, 100, 0.0, 1.0);
            GeoTransform3 turn = GeoTransform3.RotationZ(-1E-17);

            GeoArc3 moved = arc.TransformBy(turn);

            AssertWithinATurn(moved);
            Assert.Equal(0.0, moved.StartAngle);
            AssertMoved(arc, turn, moved);
        }
    }
}
