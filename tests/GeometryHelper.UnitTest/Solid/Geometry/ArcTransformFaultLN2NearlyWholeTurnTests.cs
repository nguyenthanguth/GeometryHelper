using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc in space a hair short of a whole turn moves under a turn, a shift, a mirror or an even scaling.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoArc3.TransformBy(GeoTransform3)"/> took every arc short of a whole turn through where its ends and its
    /// middle land, and an arc sweeping a whole turn less anything from 1E-15 to 1E-6 has its ends too close to tell a chord
    /// between: a plain turn threw "Two points in the same place have no chord to bulge" or "The X and Y directions must not
    /// be parallel". A transformation that keeps the arc's plane round now moves the arc by its centre and its radius, so any
    /// sweep moves; only a skew takes it through its ends and its middle, and refuses one whose ends it brings within the point
    /// tolerance of each other.
    /// </remarks>
    public class ArcTransformFaultLN2NearlyWholeTurnTests
    {
        private static readonly GeoTransform3 Turn = GeoTransform3.RotationAxis(new GeoPoint3(5, -3, 2), new GeoVector3(0.3, 0.7, -0.2), 0.7);

        private static GeoArc3 Arc(double shortOfATurn)
            => new GeoArc3(new GeoPoint3(10, 20, 30), new GeoVector3(0.2, -0.1, 1), 100, 0.3, 0.3 + 2.0 * Math.PI - shortOfATurn);

        [Theory]
        [InlineData(1E-15)]
        [InlineData(1E-12)]
        [InlineData(1E-9)]
        [InlineData(1E-6)]
        [InlineData(1E-3)]
        public void AnArcAHairShortOfAWholeTurn_IsTurned(double shortOfATurn)
        {
            GeoArc3 arc = Arc(shortOfATurn);

            GeoArc3 moved = arc.TransformBy(Turn);

            Assert.Equal(0.0, moved.Center.DistanceTo(Turn.Transform(arc.Center)), 9);
            Assert.Equal(100.0, moved.Radius, 9);
            Assert.Equal(arc.SweptAngle, moved.SweptAngle, 12);
            Assert.Equal(0.0, moved.StartPoint.DistanceTo(Turn.Transform(arc.StartPoint)), 9);
            Assert.Equal(0.0, moved.MidPoint.DistanceTo(Turn.Transform(arc.MidPoint)), 9);
        }

        private static GeoTransform3 Rounded(Func<double, double> round)
        {
            var matrix = new double[4, 4];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    matrix[i, j] = round(Turn[i, j]);
                }
            }

            return new GeoTransform3(matrix);
        }

        [Theory]
        [InlineData(false, 1E-12)]
        [InlineData(false, 1E-9)]
        [InlineData(false, 1E-6)]
        [InlineData(false, 1E-5)]
        [InlineData(false, 1E-3)]
        [InlineData(false, 1.0)]
        [InlineData(true, 1E-9)]
        [InlineData(true, 1E-6)]
        [InlineData(true, 1E-5)]
        [InlineData(true, 1E-3)]
        public void UnderATurnRoundedToAFloatOrToSixPlaces_ItMovesHoweverShortOfAWholeTurn(bool toSixPlaces, double shortOfATurn)
        {
            // Such a turn carries a circle by the rule of the ball but is no exact turn; the arc moves at every sweep, its ends
            // within the rounding of the turn of where they land.
            GeoTransform3 rounded = Rounded(v => toSixPlaces ? Math.Round(v, 6) : (float)v);
            GeoArc3 arc = Arc(shortOfATurn);

            GeoArc3 moved = arc.TransformBy(rounded);

            Assert.True(moved.StartPoint.DistanceTo(rounded.Transform(arc.StartPoint)) < 1E-4);
            Assert.True(moved.EndPoint.DistanceTo(rounded.Transform(arc.EndPoint)) < 1E-4);
            Assert.Equal(arc.SweptAngle, moved.SweptAngle, 3);
        }

        [Fact]
        public void AMirrorAndAnEvenScaling_MoveItToo()
        {
            GeoTransform3 transform = GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 0)))
                .Multiply(GeoTransform3.Scaling(2.5))
                .Multiply(Turn);
            GeoArc3 arc = Arc(1E-9);

            GeoArc3 moved = arc.TransformBy(transform);

            Assert.Equal(250.0, moved.Radius, 9);
            Assert.Equal(0.0, moved.StartPoint.DistanceTo(transform.Transform(arc.StartPoint)), 9);
            Assert.Equal(0.0, moved.MidPoint.DistanceTo(transform.Transform(arc.MidPoint)), 9);
            Assert.Equal(0.0, moved.EndPoint.DistanceTo(transform.Transform(arc.EndPoint)), 9);
        }

        [Fact]
        public void AStretchSquareToItsPlane_MovesItToo()
        {
            var arc = new GeoArc3(new GeoPoint3(10, 20, 30), GeoVector3.ZAxis, 100, 0.3, 0.3 + 2.0 * Math.PI - 1E-9);
            GeoTransform3 stretch = GeoTransform3.Scaling(1, 1, 3);

            GeoArc3 moved = arc.TransformBy(stretch);

            Assert.Equal(0.0, moved.Center.DistanceTo(new GeoPoint3(10, 20, 90)), 9);
            Assert.Equal(arc.SweptAngle, moved.SweptAngle, 12);
        }

        [Fact]
        public void UnderASkew_ItIsRefusedAsDocumented()
        {
            var skew = new GeoTransform3(new double[,]
            {
                { 1, 0.6, 0, 0 },
                { 0, 0.8, 0, 0 },
                { 0, 0, 1, 0 },
                { 0, 0, 0, 1 },
            });
            var arc = new GeoArc3(new GeoPoint3(10, 20, 30), GeoVector3.ZAxis, 100, 0.3, 0.3 + 2.0 * Math.PI - 1E-9);
            var whole = new GeoArc3(new GeoPoint3(10, 20, 30), GeoVector3.ZAxis, 100, 0.3, 0.3);

            Assert.Throws<ArgumentException>(() => arc.TransformBy(skew));
            Assert.Throws<InvalidOperationException>(() => whole.TransformBy(skew));
        }
    }
}
