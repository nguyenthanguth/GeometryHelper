using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc in space moved by a vector is the same arc in its new place. It was built again from its start angle and its
    /// start angle plus its sweep, counter-clockwise, so a clockwise arc came back as the rest of its circle: the arc of
    /// radius 100 from 0.5 radians clockwise to -0.5 moved as the arc from 0.5 counter-clockwise round to -0.5, a sweep of
    /// 302.7 degrees where it had 57.3. A whole turn could come back a hair more than a turn apart and wrap to a sweep of
    /// nothing, as the flat arc of a whole turn did.
    /// </summary>
    /// <remarks>
    /// Every arc here is of radius 100 about (1000, -2000, 500), in the plan or tilted 30 degrees about X, and is moved by
    /// (10, 20, 30). The starts 1.72, 1.8 and 2.05 radians are three that wrapped.
    /// </remarks>
    public class Arc3FaultG1iTranslateTests
    {
        private static readonly GeoPoint3 Centre = new GeoPoint3(1000, -2000, 500);

        private static readonly GeoVector3 By = new GeoVector3(10, 20, 30);

        private static readonly Tolerance Tight = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static GeoVector3 Tilted(double degrees)
        {
            double tilt = degrees * Math.PI / 180.0;

            return new GeoVector3(0, -Math.Sin(tilt), Math.Cos(tilt));
        }

        /// <summary>Asserts that one arc is another moved by <see cref="By"/>, point for point, every eighth of the way.</summary>
        private static void AssertMoved(GeoArc3 arc, GeoArc3 moved)
        {
            for (int eighth = 0; eighth <= 8; eighth++)
            {
                GeoPoint3 expected = arc.GetPointAtParameter(eighth / 8.0).Add(By);

                Assert.True(moved.GetPointAtParameter(eighth / 8.0).IsEqualTo(expected, Tight), $"{eighth} eighths along");
            }
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void AClockwiseArcMoved_KeepsItsSweepItsWayRoundAndItsPoints(double degrees)
        {
            // It came back the 302.7 degrees from 0.5 radians counter-clockwise round to -0.5.
            var arc = new GeoArc3(Centre, Tilted(degrees), 100, 0.5, -0.5, true);

            GeoArc3 moved = arc.Translate(By);

            Assert.True(moved.IsClockwise);
            Assert.Equal(arc.SweptAngle, moved.SweptAngle);
            Assert.InRange(moved.Length, 100.0 - 1E-9, 100.0 + 1E-9);
            Assert.True(moved.StartPoint.IsEqualTo(arc.StartPoint.Add(By), Tight));
            Assert.True(moved.EndPoint.IsEqualTo(arc.EndPoint.Add(By), Tight));
            Assert.True(moved.MidPoint.IsEqualTo(arc.MidPoint.Add(By), Tight));
            AssertMoved(arc, moved);
        }

        [Theory]
        [InlineData(1.72, 0.0, false)]
        [InlineData(1.8, 0.0, false)]
        [InlineData(2.05, 30.0, false)]
        [InlineData(1.8, 0.0, true)]
        [InlineData(2.05, 30.0, true)]
        public void AWholeTurnMoved_StaysAWholeTurn(double start, double degrees, bool clockwise)
        {
            // Counter-clockwise from these starts it wrapped to a sweep of about 1E-15, a point; clockwise it turned the other
            // way round.
            var arc = new GeoArc3(Centre, Tilted(degrees), 100, start, start, clockwise);

            GeoArc3 moved = arc.Translate(By);

            Assert.Equal(arc.SweptAngle, moved.SweptAngle);
            Assert.Equal(clockwise, moved.IsClockwise);
            Assert.InRange(moved.Length, 200.0 * Math.PI - 1E-9, 200.0 * Math.PI + 1E-9);
            AssertMoved(arc, moved);
        }

        [Theory]
        [InlineData(0.5, 2.0)]
        [InlineData(4.0, 1.5)]
        public void ACounterClockwiseArcMoved_IsItsCopyWithTheCentreMoved(double start, double sweep)
        {
            // The copy holds the arc's own normal, radius, start and sweep. Built again, the normal (0, -3, 1), already of
            // unit length, was normalized a second time and came back a last digit off.
            var arc = new GeoArc3(Centre, new GeoVector3(0, -3, 1), 100, start, start + sweep);
            GeoArc3 copy = arc.Clone();

            GeoArc3 moved = arc.Translate(By);

            Assert.Equal(copy.Center.Add(By), moved.Center);
            Assert.Equal(copy.Normal, moved.Normal);
            Assert.Equal(copy.Radius, moved.Radius);
            Assert.Equal(copy.StartAngle, moved.StartAngle);
            Assert.Equal(copy.SweptAngle, moved.SweptAngle);
            Assert.True(moved.IsEqualTo(copy.TransformBy(GeoTransform3.Translation(By)), Tight));
            AssertMoved(arc, moved);
        }
    }
}
