using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An arc or an edge in the plane under a skew is moved through where its ends and its middle land only where it lands
    /// within the global point tolerance of where the transformation puts the rest of it, as under a stretch.
    /// </summary>
    /// <remarks>
    /// A skew that keeps the two axes equally long was taken through the three points unasked: a turn of 0.7529 radians, a
    /// stretch of 1 + 4.29E-9 along X and a shear of 9.28E-5, scaled by 55.57, keeps them one length to the rounding of the
    /// matrix, and took the arc of radius 9 069.6 to an arc 9.16 from where its points land. Every map that is not a turn, a
    /// shift, a mirror or an even scaling is judged by where the arc lands now. The landing is read from the moved start, so
    /// that an edge bowing a hair off its chord, its radius past the reach of its digits, is not refused for want of them.
    /// </remarks>
    public class ArcTransform2FaultLN4SkewTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// The middle of an edge read across its chord, half the chord times the bulge off the chord's middle, as an edge bowing
        /// a hair is read: from its centre, 2.5E13 away, it is known only to some thousandths.
        /// </summary>
        private static GeoPoint2 Middle(GeoEdge2 edge)
        {
            GeoVector2 chord = edge.StartPoint.GetVectorTo(edge.EndPoint);

            return edge.StartPoint.Add(chord.Multiply(0.5)).Add(new GeoVector2(chord.Y, -chord.X).Multiply(edge.Bulge * 0.5));
        }

        /// <summary>A turn of 0.7529 radians of a stretch of 1 + 4.29E-9 along X and a shear of 9.28E-5, scaled by 55.57.</summary>
        private static GeoTransform2 EqualLengthSkew()
        {
            double turn = 0.7529, stretch = 1 + 4.29E-9, shear = 9.28E-5, scale = 55.57;
            double cos = Math.Cos(turn), sin = Math.Sin(turn);

            return new GeoTransform2(new double[,]
            {
                { scale * cos * stretch, scale * (cos * shear - sin), 0 },
                { scale * sin * stretch, scale * (sin * shear + cos), 0 },
                { 0, 0, 1 },
            });
        }

        /// <summary>The skew that sends Y to (0.000001, 1): a circle of radius 100 under it lands 0.0001 off.</summary>
        private static readonly GeoTransform2 SlightSkew = new GeoTransform2(new double[,]
        {
            { 1, 0.000001, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 },
        });

        [Fact]
        public void TheEqualLengthSkew_KeepsBothAxesOneLength()
        {
            GeoTransform2 skew = EqualLengthSkew();
            double alongX = skew.Transform(GeoVector2.XAxis).Length;
            double alongY = skew.Transform(GeoVector2.YAxis).Length;

            Assert.True(Math.Abs(alongX - alongY) < 1E-9 * alongX);
        }

        [Fact]
        public void AnArcThatWouldLandFarOffUnderAnEqualLengthSkew_IsRefused()
        {
            var arc = new GeoArc2(new GeoPoint2(410.78, 802.70), 9069.6, 0.8545, 0.8545 - 2.1125, true);
            var edge = new GeoEdge2(arc.StartPoint, arc.EndPoint, arc.Bulge);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(EqualLengthSkew()));
            Assert.Throws<InvalidOperationException>(() => edge.TransformBy(EqualLengthSkew()));
        }

        [Fact]
        public void AChainOfArcsThatWouldLandFarOff_IsRefused()
        {
            var arc = new GeoArc2(new GeoPoint2(410.78, 802.70), 9069.6, 0.8545, 0.8545 - 2.1125, true);
            var chain = new GeoPolylineArc2(new[] { new GeoEdge2(arc.StartPoint, arc.EndPoint, arc.Bulge) });

            Assert.Throws<InvalidOperationException>(() => chain.TransformBy(EqualLengthSkew()));
        }

        [Fact]
        public void AnArcUnderASlightSkew_RunsThroughWhereItsEndsAndItsMiddleLand()
        {
            var arc = new GeoArc2(new GeoPoint2(10, 20), 100, 0.5, 2.0);

            GeoArc2 moved = arc.TransformBy(SlightSkew);

            Assert.Equal(0.0, moved.StartPoint.DistanceTo(SlightSkew.Transform(arc.StartPoint)), 9);
            Assert.Equal(0.0, moved.EndPoint.DistanceTo(SlightSkew.Transform(arc.EndPoint)), 9);
            Assert.Equal(0.0, moved.DistanceTo(SlightSkew.Transform(arc.MidPoint), Tight), 9);
            Assert.Equal(100.0, moved.Radius, 3);
        }

        [Fact]
        public void AnArcUnderASlightSkewScaledBy1000_IsRefused()
        {
            // It lands 0.0000069 off; scaled by 1 000 it would land 0.0069 off.
            var arc = new GeoArc2(new GeoPoint2(10, 20), 100, 0.5, 2.0);
            var edge = new GeoEdge2(arc.StartPoint, arc.EndPoint, arc.Bulge);
            GeoTransform2 scaled = SlightSkew.Multiply(GeoTransform2.Scaling(1000));

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(scaled));
            Assert.Throws<InvalidOperationException>(() => edge.TransformBy(scaled));
        }

        [Theory]
        [InlineData(1E-11)]
        [InlineData(1E-12)]
        [InlineData(1E-13)]
        public void AnEdgeBowingAHairUnderATurnRoundedToSixPlaces_Moves(double bulge)
        {
            // 100 long and bowing by 1E-12, its radius is 2.5E13: read from its centre, it stood thousandths off where it
            // lands, and was refused.
            var rounded = new GeoTransform2(new double[,]
            {
                { 0.764842, -0.644217, 1000 },
                { 0.644218, 0.764843, 2000 },
                { 0, 0, 1 },
            });
            var edge = new GeoEdge2(new GeoPoint2(10, 20), new GeoPoint2(70, 100), bulge);

            GeoEdge2 moved = edge.TransformBy(rounded);

            Assert.Equal(0.0, moved.StartPoint.DistanceTo(rounded.Transform(edge.StartPoint)), 12);
            Assert.Equal(0.0, moved.EndPoint.DistanceTo(rounded.Transform(edge.EndPoint)), 12);
            Assert.True(Middle(moved).DistanceTo(rounded.Transform(Middle(edge))) < 1E-9);
        }
    }
}
