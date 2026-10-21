using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An arc in the plane and its circle are carried or refused alike, by how far they would land from where the
    /// transformation puts their points, at any size.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoArc2.TransformBy(GeoTransform2)"/> and <see cref="GeoEdge2.TransformBy(GeoTransform2)"/> compared the
    /// lengths of the two axes outright with the global vector tolerance, a number with no size, and the circle read them as a
    /// share of the stretch: under a turn rounded to six places unevenly and scaled by 1 000 the circle of radius 0.25 was carried
    /// and every arc on it threw, and a share lets a true stretch through at a large scale, where the shape lands visibly off.
    /// Both now ask how far the shape would land from where the transformation puts it, against the global point tolerance.
    /// </remarks>
    public class ArcTransform2FaultLN3EvenScalingTests
    {
        private static readonly GeoTransform2 Rounded = new GeoTransform2(new double[,]
        {
            { 0.764842, -0.644217, 1000 },
            { 0.644218, 0.764843, 2000 },
            { 0, 0, 1 },
        });

        private static readonly GeoTransform2 RoundedTimes1000 = Rounded.Multiply(GeoTransform2.Scaling(1000));

        private static readonly GeoPoint2 Centre = new GeoPoint2(10, 20);

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        [Fact]
        public void TheAxesOfARoundedTurnScaled1000_Are1Point2EMinus4Apart()
        {
            double alongX = RoundedTimes1000.Transform(GeoVector2.XAxis).Length;
            double alongY = RoundedTimes1000.Transform(GeoVector2.YAxis).Length;

            Assert.Equal(1.2E-4, Math.Abs(alongX - alongY), 5);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E-3)]
        [InlineData(1.0)]
        public void ACircleOfRadiusAQuarterAndItsArcs_MoveAlike(double shortBy)
        {
            var circle = new GeoCircle2(Centre, 0.25);
            var arc = new GeoArc2(Centre, 0.25, 0.5, 0.5 + 2 * Math.PI - shortBy);

            GeoCircle2 movedCircle = RoundedTimes1000.Transform(circle);
            GeoArc2 moved = arc.TransformBy(RoundedTimes1000);

            Assert.Equal(250.0, movedCircle.Radius, 1);
            Assert.Equal(0.0, moved.StartPoint.DistanceTo(RoundedTimes1000.Transform(arc.StartPoint)), 3);
            Assert.Equal(0.0, moved.DistanceTo(RoundedTimes1000.Transform(arc.MidPoint), Tight), 3);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E-3)]
        [InlineData(1.0)]
        public void ACircleOfRadius100AndItsArcs_AreRefusedAlike(double shortBy)
        {
            // It would land up to 0.076 off where the transformation puts it.
            var arc = new GeoArc2(Centre, 100, 0.5, 0.5 + 2 * Math.PI - shortBy);

            Assert.Throws<InvalidOperationException>(() => RoundedTimes1000.Transform(new GeoCircle2(Centre, 100)));
            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(RoundedTimes1000));
        }

        [Fact]
        public void AStretchOf3EMinus6Scaled1000_IsRefusedWhereTheArcWouldLand0Point017Off()
        {
            var stretch = new GeoTransform2(new double[,] { { (1 + 3E-6) * 1000, 0, 0 }, { 0, 1000, 0 }, { 0, 0, 1 } });
            var arc = new GeoArc2(Centre, 100, 0.5, 2.0);

            Assert.Throws<InvalidOperationException>(() => stretch.Transform(new GeoCircle2(Centre, 100)));
            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(stretch));
            Assert.Throws<InvalidOperationException>(() => new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 0.5).TransformBy(stretch));
        }

        [Fact]
        public void AStretchOf3EMinus5AtAThousandthOfTheScale_MovesBoth()
        {
            // 3E-6 off at the most, well within the tolerance.
            var stretch = new GeoTransform2(new double[,] { { (1 + 3E-5) * 0.001, 0, 0 }, { 0, 0.001, 0 }, { 0, 0, 1 } });
            var arc = new GeoArc2(Centre, 100, 0.5, 2.0);

            Assert.Equal(0.1, stretch.Transform(new GeoCircle2(Centre, 100)).Radius, 4);
            Assert.Equal(0.0, arc.TransformBy(stretch).EndPoint.DistanceTo(stretch.Transform(arc.EndPoint)), 9);
        }

        [Fact]
        public void ATurnRoundedToSixPlacesOnItsOwnScaled1E5_MovesTheArcWhole()
        {
            double c = Math.Round(Math.Cos(0.7), 6), s = Math.Round(Math.Sin(0.7), 6);
            var map = new GeoTransform2(new double[,] { { c * 1E5, -s * 1E5, 0 }, { s * 1E5, c * 1E5, 0 }, { 0, 0, 1 } });
            var arc = new GeoArc2(Centre, 100, 0.5, 2.0);

            Assert.Equal(map.Transform(new GeoCircle2(Centre, 100)).Radius, arc.TransformBy(map).Radius);
        }
    }
}
