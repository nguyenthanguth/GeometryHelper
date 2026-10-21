using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An arc or an edge in the plane under a skew is the arc through where its ends and its middle land, as in space, where
    /// that arc lands within the global point tolerance of where the skew puts the rest of it, and is refused otherwise.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoArc2.TransformBy(GeoTransform2)"/> took the arc to stay round when the two axes came out of the
    /// transformation equally long, and moved it by its centre. The skew that sends X to (1, 0) and Y to (0.6, 0.8) keeps both
    /// lengths, so the arc of radius 100 about the origin from 0.5 to 2 radians came out at radius 100 about the moved centre,
    /// its start 22.7 and its end 44.6 from where they land, and the edge of bulge 0.5 from (0, 0) to (100, 0) ended 22.05
    /// off, so a chain of such edges came apart. A turn, a shift, a mirror or an even scaling still moves an arc by its centre,
    /// bit for bit as before; any other transformation takes it through where its ends and its middle land; a whole turn is
    /// refused under a skew as a circle is. Since LN4 a skew is judged by where the arc lands, as a stretch is: this skew
    /// makes an ellipse of radii 126.5 and 63.2 of the circle of radius 100, and the arc through its ends and its middle,
    /// of radius 66.09, stands far off it, so it is refused; the slight skew that sends Y to (0.000001, 1) moves it 0.0000069 off.
    /// </remarks>
    public class ArcTransform2FaultLN2SkewTests
    {
        private static readonly GeoTransform2 Skew = new GeoTransform2(new double[,]
        {
            { 1, 0.6, 0 },
            { 0, 0.8, 0 },
            { 0, 0, 1 },
        });

        private static readonly GeoTransform2 SlightSkew = new GeoTransform2(new double[,]
        {
            { 1, 0.000001, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 },
        });

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        [Fact]
        public void ASkewedArc_RunsThroughWhereItsEndsAndItsMiddleLand()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.5, 2.0);

            GeoArc2 moved = arc.TransformBy(SlightSkew);

            Assert.Equal(0.0, moved.StartPoint.DistanceTo(SlightSkew.Transform(arc.StartPoint)), 9);
            Assert.Equal(0.0, moved.EndPoint.DistanceTo(SlightSkew.Transform(arc.EndPoint)), 9);
            Assert.Equal(0.0, moved.DistanceTo(SlightSkew.Transform(arc.MidPoint), Tight), 9);
            Assert.Equal(100.0, moved.Radius, 3);
            Assert.False(moved.IsClockwise);
        }

        [Fact]
        public void ASkewedEdge_EndsWhereItsEndsLand()
        {
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 0.5);

            GeoEdge2 moved = edge.TransformBy(SlightSkew);

            Assert.Equal(new GeoPoint2(0, 0), moved.StartPoint);
            Assert.Equal(new GeoPoint2(100, 0), moved.EndPoint);
            Assert.Equal(0.0, moved.ToArc().DistanceTo(SlightSkew.Transform(edge.MidPoint), Tight), 9);
        }

        [Fact]
        public void AnArcAnEdgeAndAChainTheSkewWouldLandFarOff_AreRefused()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.5, 2.0);
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 0.5);
            var chain = new GeoPolylineArc2(
                new List<GeoPoint2> { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(150, 80) },
                new List<double> { 0.5, -0.3 });

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(Skew));
            Assert.Throws<InvalidOperationException>(() => edge.TransformBy(Skew));
            Assert.Throws<InvalidOperationException>(() => chain.TransformBy(Skew));
        }

        [Fact]
        public void AChainOfArcsUnderTheSkew_StaysJoined()
        {
            var chain = new GeoPolylineArc2(
                new List<GeoPoint2> { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(150, 80), new GeoPoint2(40, 120) },
                new List<double> { 0.5, -0.3, 0.8 });

            GeoPolylineArc2 moved = chain.TransformBy(SlightSkew);

            for (int i = 0; i < chain.Vertices.Count; i++)
            {
                Assert.Equal(0.0, moved.Vertices[i].DistanceTo(SlightSkew.Transform(chain.Vertices[i])), 9);
            }
        }

        [Fact]
        public void AMirroredSkew_TurnsTheArcTheOtherWay()
        {
            GeoTransform2 mirrored = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 0))).Multiply(SlightSkew);
            var arc = new GeoArc2(new GeoPoint2(10, -20), 50, 0.3, 2.9);

            GeoArc2 moved = arc.TransformBy(mirrored);

            Assert.True(moved.IsClockwise);
            Assert.Equal(0.0, moved.StartPoint.DistanceTo(mirrored.Transform(arc.StartPoint)), 9);
            Assert.Equal(0.0, moved.EndPoint.DistanceTo(mirrored.Transform(arc.EndPoint)), 9);
            Assert.Equal(0.0, moved.DistanceTo(mirrored.Transform(arc.MidPoint), Tight), 9);
        }

        [Fact]
        public void AWholeTurnUnderTheSkew_IsRefused()
        {
            var arc = new GeoArc2(new GeoPoint2(3, 4), 100, 0.5, 0.5);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(Skew));
        }

        [Fact]
        public void AnArcAHairShortOfAWholeTurnUnderTheSkew_IsRefused()
        {
            // Its ends land 1E-7 apart, inside the point tolerance: no chord says which way round it runs.
            var arc = new GeoArc2(new GeoPoint2(3, 4), 100, 0.5, 0.5 + 2.0 * Math.PI - 1E-9);

            Assert.Throws<ArgumentException>(() => arc.TransformBy(Skew));
        }

        [Theory]
        [InlineData(1E-12)]
        [InlineData(1E-9)]
        [InlineData(1E-6)]
        [InlineData(1E-5)]
        [InlineData(1E-4)]
        [InlineData(1E-2)]
        [InlineData(1.0)]
        public void AnArcAHairShortOfAWholeTurnUnderATurnRoundedToSixPlaces_Moves(double shortOfATurn)
        {
            // The turn of 0.7 radians with each number rounded to six places on its own: it carries a circle, by the rule of
            // the ball, but is no exact turn. However short of a whole turn the arc is, it moves, its ends within the rounding
            // of the turn of where they land.
            var rounded = new GeoTransform2(new double[,]
            {
                { 0.764842, -0.644217, 1000 },
                { 0.644218, 0.764843, 2000 },
                { 0, 0, 1 },
            });
            var arc = new GeoArc2(new GeoPoint2(10, 20), 100, 0.5, 0.5 + 2.0 * Math.PI - shortOfATurn);

            GeoArc2 moved = arc.TransformBy(rounded);

            Assert.True(moved.StartPoint.DistanceTo(rounded.Transform(arc.StartPoint)) < 1E-4);
            Assert.True(moved.EndPoint.DistanceTo(rounded.Transform(arc.EndPoint)) < 1E-4);
            Assert.Equal(arc.SweptAngle, moved.SweptAngle, 3);
        }

        [Fact]
        public void ATurnAShiftAMirrorAndAnEvenScaling_StillMoveTheArcByItsCentre()
        {
            GeoTransform2 transform = GeoTransform2.Translation(new GeoVector2(5, -7))
                .Multiply(GeoTransform2.Rotation(0.7))
                .Multiply(GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(1, 1), new GeoPoint2(3, 2))))
                .Multiply(GeoTransform2.Scaling(2.5));
            var arc = new GeoArc2(new GeoPoint2(10, 20), 100, 0.5, 0.5 + 2.0 * Math.PI - 1E-9);

            GeoArc2 moved = arc.TransformBy(transform);

            Assert.Equal(0.0, moved.Center.DistanceTo(transform.Transform(arc.Center)), 9);
            Assert.Equal(250.0, moved.Radius, 9);
            Assert.Equal(-arc.SweptAngle, moved.SweptAngle, 9);
            Assert.Equal(0.0, moved.StartPoint.DistanceTo(transform.Transform(arc.StartPoint)), 9);
        }

        [Fact]
        public void AnUnevenScaling_IsStillRefused()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.5, 2.0);
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 0.5);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(GeoTransform2.Scaling(1, 2)));
            Assert.Throws<InvalidOperationException>(() => edge.TransformBy(GeoTransform2.Scaling(1, 2)));
        }
    }
}
