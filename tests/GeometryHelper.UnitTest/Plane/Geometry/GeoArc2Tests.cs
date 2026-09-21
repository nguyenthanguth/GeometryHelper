using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The arc as a shape: how it is built, what it measures, and that the two ways of naming one — three
    /// points, and a bulge across a chord — agree with the angles it is held as.
    /// </summary>
    public class GeoArc2Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>A quarter turn counter-clockwise, from (10, 0) up to (0, 10) about the origin.</summary>
        private static GeoArc2 Quarter() => new GeoArc2(GeoPoint2.Origin, 10.0, 0.0, Math.PI / 2.0);

        [Fact]
        public void AnArcKnowsWhereItStartsEndsAndPasses()
        {
            GeoArc2 arc = Quarter();

            Assert.True(arc.StartPoint.IsEqualTo(new GeoPoint2(10, 0), Tight));
            Assert.True(arc.EndPoint.IsEqualTo(new GeoPoint2(0, 10), Tight));
            Assert.True(arc.MidPoint.IsEqualTo(new GeoPoint2(10 / Math.Sqrt(2.0), 10 / Math.Sqrt(2.0)), Tight));

            Assert.Equal(Math.PI / 2.0, arc.SweptAngle, 12);
            Assert.Equal(Math.PI / 2.0 * 10.0, arc.Length, 12);
            Assert.False(arc.IsClockwise);
            Assert.Equal(10.0, arc.GetCircle().Radius, 12);
            Assert.Equal(Math.Sqrt(200.0), arc.GetChord().Length, 9);
        }

        [Fact]
        public void TheSameTwoAnglesEitherWayRoundGiveTheTwoPartsOfTheCircle()
        {
            GeoArc2 counter = new GeoArc2(GeoPoint2.Origin, 10.0, 0.0, Math.PI / 2.0);
            GeoArc2 clock = new GeoArc2(GeoPoint2.Origin, 10.0, 0.0, Math.PI / 2.0, true);

            Assert.Equal(Math.PI / 2.0, counter.SweptAngle, 12);
            Assert.Equal(-(Math.PI * 2.0 - Math.PI / 2.0), clock.SweptAngle, 12);
            Assert.True(clock.IsClockwise);

            // Together they draw the whole circle.
            Assert.Equal(Math.PI * 2.0 * 10.0, counter.Length + clock.Length, 9);

            // Both start and end in the same places.
            Assert.True(counter.StartPoint.IsEqualTo(clock.StartPoint, Tight));
            Assert.True(counter.EndPoint.IsEqualTo(clock.EndPoint, Tight));
        }

        [Fact]
        public void EqualAnglesMeanAWholeTurn()
        {
            GeoArc2 full = new GeoArc2(GeoPoint2.Origin, 5.0, 1.0, 1.0);

            Assert.Equal(Math.PI * 2.0, full.SweptAngle, 12);
            Assert.Equal(Math.PI * 2.0 * 5.0, full.Length, 9);
            Assert.True(full.StartPoint.IsEqualTo(full.EndPoint, Tight));
        }

        [Fact]
        public void FromThreePoints_PassesThroughAllThree()
        {
            var start = new GeoPoint2(10, 0);
            var middle = new GeoPoint2(0, 10);
            var end = new GeoPoint2(-10, 0);

            GeoArc2 arc = GeoArc2.FromThreePoints(start, middle, end);

            Assert.True(arc.Center.IsEqualTo(GeoPoint2.Origin, new Tolerance(1E-9, 1E-9)));
            Assert.Equal(10.0, arc.Radius, 9);
            Assert.True(arc.StartPoint.IsEqualTo(start, Tight));
            Assert.True(arc.EndPoint.IsEqualTo(end, Tight));
            Assert.True(arc.MidPoint.IsEqualTo(middle, Tight));
            Assert.False(arc.IsClockwise);
        }

        [Fact]
        public void FromThreePoints_FollowsTheMiddlePointAroundEitherWay()
        {
            var start = new GeoPoint2(10, 0);
            var end = new GeoPoint2(-10, 0);

            // The middle above the chord: counter-clockwise, the short way over the top.
            GeoArc2 over = GeoArc2.FromThreePoints(start, new GeoPoint2(0, 10), end);
            Assert.False(over.IsClockwise);
            Assert.True(over.MidPoint.Y > 0);

            // The middle below it: clockwise, under the bottom.
            GeoArc2 under = GeoArc2.FromThreePoints(start, new GeoPoint2(0, -10), end);
            Assert.True(under.IsClockwise);
            Assert.True(under.MidPoint.Y < 0);

            // A middle that is nearly at one end gives the long way round.
            GeoArc2 long_ = GeoArc2.FromThreePoints(start, new GeoPoint2(0, -10), new GeoPoint2(10.0 * Math.Cos(0.2), 10.0 * Math.Sin(0.2)));
            Assert.True(Math.Abs(long_.SweptAngle) > Math.PI);
        }

        [Fact]
        public void FromThreePoints_RefusesPointsOnOneLine()
        {
            Assert.Throws<InvalidOperationException>(() => GeoArc2.FromThreePoints(
                new GeoPoint2(0, 0), new GeoPoint2(5, 5), new GeoPoint2(10, 10)));
        }

        [Theory]
        [InlineData(1.0)]            // a half turn counter-clockwise
        [InlineData(-1.0)]           // a half turn clockwise
        [InlineData(0.4142135623730951)]   // a quarter turn: tan(22.5 degrees)
        [InlineData(-0.2)]
        [InlineData(2.5)]            // more than a half turn
        public void FromBulge_AgreesWithTheBulgeItReportsBack(double bulge)
        {
            var start = new GeoPoint2(3, 4);
            var end = new GeoPoint2(23, 4);

            GeoArc2 arc = GeoArc2.FromBulge(start, end, bulge);

            Assert.True(arc.StartPoint.IsEqualTo(start, new Tolerance(1E-9, 1E-9)));
            Assert.True(arc.EndPoint.IsEqualTo(end, new Tolerance(1E-9, 1E-9)));

            // The number it gives back is the number it was built from: the round trip AutoCAD needs.
            Assert.Equal(bulge, arc.Bulge, 9);
            Assert.Equal(bulge < 0.0, arc.IsClockwise);
        }

        [Fact]
        public void FromBulge_OfOneIsAHalfTurnThroughTheRightSide()
        {
            var start = new GeoPoint2(0, 0);
            var end = new GeoPoint2(10, 0);

            GeoArc2 up = GeoArc2.FromBulge(start, end, 1.0);

            Assert.Equal(Math.PI, up.SweptAngle, 9);
            Assert.Equal(5.0, up.Radius, 9);
            Assert.True(up.Center.IsEqualTo(new GeoPoint2(5, 0), new Tolerance(1E-9, 1E-9)));

            // Counter-clockwise from (0,0) to (10,0) passes below the chord.
            Assert.True(up.MidPoint.IsEqualTo(new GeoPoint2(5, -5), new Tolerance(1E-9, 1E-9)));

            // The opposite bulge is its mirror.
            GeoArc2 down = GeoArc2.FromBulge(start, end, -1.0);
            Assert.True(down.MidPoint.IsEqualTo(new GeoPoint2(5, 5), new Tolerance(1E-9, 1E-9)));
        }

        [Fact]
        public void FromBulge_RefusesWhatIsNotAnArc()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoArc2.FromBulge(new GeoPoint2(0, 0), new GeoPoint2(10, 0), 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoArc2.FromBulge(new GeoPoint2(0, 0), new GeoPoint2(10, 0), double.NaN));
            Assert.Throws<InvalidOperationException>(() => GeoArc2.FromBulge(new GeoPoint2(4, 4), new GeoPoint2(4, 4), 0.5));
        }

        [Fact]
        public void ThreePointsAndABulgeDescribeTheSameArc()
        {
            var start = new GeoPoint2(0, 0);
            var end = new GeoPoint2(10, 0);

            GeoArc2 byBulge = GeoArc2.FromBulge(start, end, 0.5);
            GeoArc2 byPoints = GeoArc2.FromThreePoints(start, byBulge.MidPoint, end);

            Assert.True(byBulge.IsEqualTo(byPoints, new Tolerance(1E-8, 1E-8)));
            Assert.Equal(byBulge.Bulge, byPoints.Bulge, 8);
        }

        [Fact]
        public void ParametersRunFromTheStartToTheEnd()
        {
            GeoArc2 arc = Quarter();

            Assert.True(arc.GetPointAtParameter(0.0).IsEqualTo(arc.StartPoint, Tight));
            Assert.True(arc.GetPointAtParameter(1.0).IsEqualTo(arc.EndPoint, Tight));
            Assert.True(arc.GetPointAtParameter(0.5).IsEqualTo(arc.MidPoint, Tight));
            Assert.True(arc.GetPointAtDistance(arc.Length * 0.5).IsEqualTo(arc.MidPoint, Tight));

            Assert.Equal(0.5, arc.GetParameterAtPoint(arc.MidPoint), 9);
            Assert.Equal(arc.Length * 0.5, arc.GetDistanceAtPoint(arc.MidPoint), 9);
            Assert.Equal(0.25, arc.GetParameterAtDistance(arc.Length * 0.25), 9);
            Assert.Equal(arc.Length * 0.25, arc.GetDistanceAtParameter(0.25), 9);

            // A point off the arc is answered with the nearer of its two ends.
            Assert.Equal(1.0, arc.GetParameterAtPoint(new GeoPoint2(-10, 1)), 9);
            Assert.Equal(0.0, arc.GetParameterAtPoint(new GeoPoint2(1, -10)), 9);
            Assert.Equal(0.0, arc.GetParameterAtPoint(arc.Center), 9);
        }

        [Fact]
        public void ReversingSwapsTheEndsAndTheDirection()
        {
            GeoArc2 arc = Quarter();
            GeoArc2 back = arc.Reverse();

            Assert.True(back.StartPoint.IsEqualTo(arc.EndPoint, Tight));
            Assert.True(back.EndPoint.IsEqualTo(arc.StartPoint, Tight));
            Assert.True(back.MidPoint.IsEqualTo(arc.MidPoint, Tight));
            Assert.Equal(arc.Length, back.Length, 9);
            Assert.NotEqual(arc.IsClockwise, back.IsClockwise);
            Assert.Equal(-arc.Bulge, back.Bulge, 9);

            // Which way a curve runs is part of it.
            Assert.False(arc.IsEqualTo(back));
            Assert.True(arc.IsEqualTo(back.Reverse()));
        }

        [Fact]
        public void OffsettingAnArcGivesAConcentricArc()
        {
            GeoArc2 arc = Quarter();

            Assert.True(arc.TryOffset(5.0, out GeoArc2 wider));
            Assert.Equal(15.0, wider.Radius, 12);
            Assert.True(wider.Center.IsEqualTo(arc.Center, Tight));
            Assert.Equal(arc.SweptAngle, wider.SweptAngle, 12);

            Assert.True(arc.TryOffset(-5.0, out GeoArc2 tighter));
            Assert.Equal(5.0, tighter.Radius, 12);

            // Inward by more than the radius would turn it inside out.
            Assert.False(arc.TryOffset(-10.0, out GeoArc2 refused));
            Assert.True(refused.IsEqualTo(arc));
        }

        [Fact]
        public void MovingTurningAndTransformingKeepItAnArc()
        {
            GeoArc2 arc = Quarter();

            Assert.True(arc.Translate(new GeoVector2(3, -4)).StartPoint.IsEqualTo(new GeoPoint2(13, -4), Tight));
            Assert.Equal(arc.Length, arc.Translate(new GeoVector2(3, -4)).Length, 9);

            GeoArc2 turned = arc.RotateBy(Math.PI / 2.0, GeoPoint2.Origin);
            Assert.True(turned.StartPoint.IsEqualTo(new GeoPoint2(0, 10), Tight));
            Assert.Equal(arc.Length, turned.Length, 9);

            // A uniform scaling multiplies the radius and the length with it.
            GeoArc2 scaled = arc.TransformBy(GeoTransform2.Scaling(3.0));
            Assert.Equal(30.0, scaled.Radius, 9);
            Assert.Equal(arc.Length * 3.0, scaled.Length, 9);

            // A mirror reverses the way it sweeps but keeps its ends and its length.
            GeoArc2 mirrored = arc.TransformBy(GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 0))));
            Assert.NotEqual(arc.IsClockwise, mirrored.IsClockwise);
            Assert.Equal(arc.Length, mirrored.Length, 9);
            Assert.True(mirrored.StartPoint.IsEqualTo(new GeoPoint2(10, 0), new Tolerance(1E-8, 1E-8)));
            Assert.True(mirrored.EndPoint.IsEqualTo(new GeoPoint2(0, -10), new Tolerance(1E-8, 1E-8)));

            // Stretched unevenly it would be part of an ellipse, which is refused.
            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(GeoTransform2.Scaling(2.0, 3.0)));
        }

        [Fact]
        public void AnArcCutsItselfIntoStraightPiecesTheSameThreeWays()
        {
            GeoArc2 arc = new GeoArc2(GeoPoint2.Origin, 1000.0, 0.0, Math.PI / 2.0);

            GeoPolyline2 automatic = arc.ToPolyline();
            Assert.True(automatic[0].IsEqualTo(arc.StartPoint, Tight));
            Assert.True(automatic[automatic.VertexCount - 1].IsEqualTo(arc.EndPoint, Tight));
            Assert.InRange(automatic.VertexCount, 10, 20);   // a quarter of about fifty

            GeoPolyline2 spaced = arc.ToPolylineBySpacing(100.0);
            Assert.True(arc.Length / (spaced.VertexCount - 1) <= 100.0 + 1E-9);

            Assert.Equal(9, arc.ToPolyline(8).VertexCount);
            Assert.True(arc.ToPolylineByChordTolerance(0.5).VertexCount > automatic.VertexCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => arc.ToPolyline(0));
        }

        [Fact]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoArc2(GeoPoint2.Origin, 0.0, 0.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoArc2(GeoPoint2.Origin, -5.0, 0.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoArc2(GeoPoint2.Origin, 5.0, double.NaN, 1.0));
            Assert.Throws<ArgumentNullException>(() => Quarter().TransformBy(null));
        }

        [Fact]
        public void EqualityComparesTheCurveExactlyAndWithinTolerance()
        {
            GeoArc2 arc = Quarter();

            Assert.Equal(arc, arc.Clone());
            Assert.Equal(arc.GetHashCode(), arc.Clone().GetHashCode());
            Assert.True(arc == arc.Clone());
            Assert.False(arc != arc.Clone());
            Assert.False(arc.Equals("not an arc"));

            GeoArc2 nudged = new GeoArc2(new GeoPoint2(1E-7, 0), 10.0, 0.0, Math.PI / 2.0);
            Assert.True(arc.IsEqualTo(nudged));
            Assert.False(arc.IsEqualTo(nudged, Tight));
            Assert.NotEqual(arc, nudged);

            Assert.Contains("GeoArc2", arc.ToString());
        }
    }
}
