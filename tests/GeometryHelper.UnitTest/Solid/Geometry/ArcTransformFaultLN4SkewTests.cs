using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc or an edge in space under a skew or a stretch of its plane is moved through where its ends and its middle land
    /// only where it lands within the global point tolerance of where the transformation puts the rest of it.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoArc3.TransformBy(GeoTransform3)"/> judged the landing only where the axes of the arc's plane came out of
    /// unequal length, so the turn of 0.7529 radians of a stretch of 1 + 4.29E-9 and a shear of 9.28E-5, scaled by 55.57, took
    /// the arc of radius 9 069.6 to one 9.16 from where its points land; <see cref="GeoEdge3.TransformBy(GeoTransform3)"/>
    /// judged nothing, and took an edge stretched to twice along a diagonal of its plane to an arc that stands for no part of
    /// the ellipse it became. Both are judged by where they land now, for every map that is not a turn, a shift, a mirror or an
    /// even scaling of the plane.
    /// </remarks>
    public class ArcTransformFaultLN4SkewTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// The middle of an edge read across its chord, half the chord times the bulge off the chord's middle on the side it bows
        /// to, as an edge bowing a hair is read: from its centre, 2.5E13 away, it is known only to some thousandths.
        /// </summary>
        private static GeoPoint3 Middle(GeoEdge3 edge)
        {
            GeoVector3 chord = edge.StartPoint.GetVectorTo(edge.EndPoint);
            GeoVector3 side = chord.CrossProduct(edge.Normal).Normalize();

            return edge.StartPoint.Add(chord.Multiply(0.5)).Add(side.Multiply(edge.Bulge * chord.Length * 0.5));
        }

        /// <summary>The map of the plane ArcTransform2FaultLN4SkewTests names, carried into space with Z scaled alike.</summary>
        private static GeoTransform3 EqualLengthSkew()
        {
            double turn = 0.7529, stretch = 1 + 4.29E-9, shear = 9.28E-5, scale = 55.57;
            double cos = Math.Cos(turn), sin = Math.Sin(turn);

            return new GeoTransform3(new double[,]
            {
                { scale * cos * stretch, scale * (cos * shear - sin), 0, 0 },
                { scale * sin * stretch, scale * (sin * shear + cos), 0, 0 },
                { 0, 0, scale, 0 },
                { 0, 0, 0, 1 },
            });
        }

        private static GeoArc3 Wide() => new GeoArc3(new GeoPoint3(410.78, 802.70, 0), GeoVector3.ZAxis, 9069.6, 0.8545, 0.8545 - 2.1125, true);

        [Fact]
        public void AnArcThatWouldLandFarOffUnderAnEqualLengthSkew_IsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Wide().TransformBy(EqualLengthSkew()));
        }

        [Fact]
        public void AnEdgeThatWouldLandFarOffUnderAnEqualLengthSkew_IsRefused()
        {
            GeoArc3 arc = Wide();
            var edge = new GeoEdge3(arc.StartPoint, arc.EndPoint, Math.Tan(arc.SweptAngle / 4), arc.Normal);

            Assert.Throws<InvalidOperationException>(() => edge.TransformBy(EqualLengthSkew()));
            Assert.Throws<InvalidOperationException>(() => new GeoPolylineArc3(new[] { edge }).TransformBy(EqualLengthSkew()));
        }

        [Fact]
        public void AnEdgeUnderASlightShear_RunsThroughWhereItsEndsAndItsMiddleLand()
        {
            // The shear of 1E-6 of Y along X: the edge of radius 10 lands some 8E-7 off, inside the tolerance.
            var shear = new GeoTransform3(new double[,]
            {
                { 1, 0.000001, 0, 0 },
                { 0, 1, 0, 0 },
                { 0, 0, 1, 0 },
                { 0, 0, 0, 1 },
            });
            var edge = new GeoEdge3(GeoPoint3.Origin, new GeoPoint3(10, 0, 0), 0.5, GeoVector3.ZAxis);

            GeoEdge3 moved = edge.TransformBy(shear);

            Assert.True(moved.EndPoint.DistanceTo(shear.Transform(edge.EndPoint)) < 1E-12);
            Assert.True(moved.DistanceTo(shear.Transform(edge.MidPoint), Tight) < 1E-9);
        }

        [Theory]
        [InlineData(1E-11)]
        [InlineData(1E-12)]
        [InlineData(1E-13)]
        public void AnArcBowingAHairUnderATurnRoundedToSixPlaces_Moves(double bulge)
        {
            // The turn of 0.7 radians about (0.3, 0.7, -0.2), each number rounded to six places. An edge 100 long bowing by
            // 1E-12 has a radius of 2.5E13: read from its centre, its arc stood thousandths off where it lands, and was refused.
            // An arc that large places its own points only to some thousandths, so it is asked here only to move.
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoVector3(0.3, 0.7, -0.2), 0.7);
            var rounded = new double[4, 4];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    rounded[i, j] = Math.Round(turn[i, j], 6);
                }
            }

            var transform = new GeoTransform3(rounded);
            var edge = new GeoEdge3(new GeoPoint3(10, 20, 30), new GeoPoint3(70, 100, 30), bulge, GeoVector3.ZAxis);

            GeoEdge3 moved = edge.TransformBy(transform);
            GeoArc3 arc = edge.ToArc().TransformBy(transform);

            Assert.True(moved.EndPoint.DistanceTo(transform.Transform(edge.EndPoint)) < 1E-9);
            Assert.True(Middle(moved).DistanceTo(transform.Transform(Middle(edge))) < 1E-9);
            Assert.True(arc.Radius > 1E12);
        }
    }
}
