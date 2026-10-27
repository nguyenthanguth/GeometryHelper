using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A whole turn in space keeps every point of its circle, whatever angle it starts at. Where a point falls along an arc
    /// was read off the arc laid flat, built from its start angle and its start angle plus its sweep; for a whole turn the
    /// two came back a hair more than a turn apart at some starts, and the flat arc wrapped that to a sweep of nothing. A
    /// point on the circle was then carried to the start, and the whole turn of radius 100 from 1.8 radians read the point
    /// half a turn round 200 from it.
    /// </summary>
    /// <remarks>
    /// Every arc here is a whole turn of radius 100 about (1000, -2000, 500), counter-clockwise, in the plan or tilted 30
    /// degrees about X. The starts 1.72, 1.8 and 2.05 radians are three of the 875 starts of a thousandth of a radian each
    /// that wrapped. Clockwise whole turns did not.
    /// </remarks>
    public class Arc3FaultG1gWholeTurnTests
    {
        private static readonly GeoPoint3 Centre = new GeoPoint3(1000, -2000, 500);

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        private static GeoArc3 WholeTurn(double start, double degrees)
        {
            double tilt = degrees * Math.PI / 180.0;

            return new GeoArc3(Centre, new GeoVector3(0, -Math.Sin(tilt), Math.Cos(tilt)), 100, start, start);
        }

        [Theory]
        [InlineData(1.72, 0.0, 0.0)]
        [InlineData(1.8, 0.0, 0.0)]
        [InlineData(2.05, 0.0, 0.0)]
        [InlineData(1.8, 30.0, 0.0)]
        [InlineData(1.8, 0.0, 1E-3)]
        [InlineData(2.05, 30.0, 1E-2)]
        public void APointOnTheCircleOfAWholeTurn_IsOnItAndItsOwnClosestPoint(double start, double degrees, double equalPoint)
        {
            // Each eighth of the way round. Carried to the start, the point half a turn round read 200 away. A point worked out
            // to the last digit is on the arc within a thousandth at the least, as no tolerance at all asks more than a rounding
            // can give.
            Tolerance t = At(equalPoint);
            Tolerance onIt = At(Math.Max(equalPoint, 1E-3));
            GeoArc3 arc = WholeTurn(start, degrees);

            Assert.Equal(2.0 * Math.PI, arc.SweptAngle);

            for (int eighth = 1; eighth < 8; eighth++)
            {
                GeoPoint3 point = arc.GetPointAtParameter(eighth / 8.0);

                Assert.InRange(arc.DistanceTo(point, t), 0.0, 1E-9);
                Assert.InRange(point.DistanceTo(arc, t), 0.0, 1E-9);
                Assert.True(arc.IsPointOn(point, onIt), $"{eighth} eighths round");
                Assert.InRange(arc.GetClosestPointOnBoundary(point, t).DistanceTo(point), 0.0, 1E-9);
            }
        }

        [Theory]
        [InlineData(1.72, 0.0)]
        [InlineData(1.8, 30.0)]
        [InlineData(2.05, 0.0)]
        public void TheParameterOfAPointOfAWholeTurn_ComesBackAsItWentIn(double start, double degrees)
        {
            // It came back as nought, the start, for every point.
            Tolerance t = At(1E-3);
            GeoArc3 arc = WholeTurn(start, degrees);

            for (int eighth = 1; eighth < 8; eighth++)
            {
                double parameter = eighth / 8.0;
                GeoPoint3 point = arc.GetPointAtParameter(parameter);
                double back = arc.GetParameterAtPoint(point, t);

                Assert.InRange(back, parameter - 1E-12, parameter + 1E-12);
                Assert.InRange(arc.GetPointAtParameter(back).DistanceTo(point), 0.0, 1E-9);
            }
        }

        [Theory]
        [InlineData(1.72, 0.0)]
        [InlineData(1.8, 30.0)]
        [InlineData(2.05, 0.0)]
        public void AWholeTurnCutAtAPointAQuarterRound_ComesApartThere(double start, double degrees)
        {
            // A quarter of the circle, 50 pi long, and three quarters, 150 pi. The point was read as the start, so the arc was
            // left whole.
            Tolerance t = At(1E-3);
            GeoArc3 arc = WholeTurn(start, degrees);
            GeoPoint3 quarter = arc.GetPointAtParameter(0.25);

            Assert.True(arc.TrySplitAt(quarter, out GeoArc3[] pieces, t));
            Assert.Equal(2, pieces.Length);
            Assert.InRange(pieces[0].EndPoint.DistanceTo(quarter), 0.0, 1E-9);
            Assert.InRange(pieces[1].StartPoint.DistanceTo(quarter), 0.0, 1E-9);
            Assert.InRange(pieces[0].Length, 50.0 * Math.PI - 1E-9, 50.0 * Math.PI + 1E-9);
            Assert.InRange(pieces[1].Length, 150.0 * Math.PI - 1E-9, 150.0 * Math.PI + 1E-9);
        }
    }
}
