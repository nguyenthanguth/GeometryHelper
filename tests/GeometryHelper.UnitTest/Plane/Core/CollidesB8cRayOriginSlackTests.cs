using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two rays collide exactly when they come within the point tolerance of each other, and measure nought exactly where
    /// they truly cross, at or ahead of both origins.
    /// </summary>
    /// <remarks>
    /// The crossing of the two carriers counted up to the point tolerance behind either origin, so two origins up to twice
    /// the tolerance apart touched: the ray from (0.0009, 0) along X and the one leaving 0.0009 along 120 degrees from the
    /// origin, whose carriers cross at the origin, 0.0009 behind each, are 0.00156 apart, and collided at a thousandth and
    /// measured nought. TryIntersectWith keeps its slack, since it places the crossing.
    /// </remarks>
    public class CollidesB8cRayOriginSlackTests
    {
        /// <summary>The distance of the two origins, 0.0009 sqrt 3.</summary>
        private const double OriginGap = 0.0015588457268119896;

        /// <summary>The ray from (0.0009, 0) along X.</summary>
        private static GeoRay2 AlongX() => new GeoRay2(P(0.0009, 0), U(1, 0));

        /// <summary>The ray from 0.0009 along 120 degrees, leaving the origin that way.</summary>
        private static GeoRay2 Leaving()
        {
            GeoVector2 direction = U(Math.Cos(2 * Math.PI / 3), Math.Sin(2 * Math.PI / 3));

            return new GeoRay2(P(0.0009 * direction.X, 0.0009 * direction.Y), direction);
        }

        [Fact]
        public void TwoOriginsOneAndAHalfThousandthsApart_DoNotCollideAtAThousandth()
        {
            GeoRay2 a = AlongX(), b = Leaving();

            Assert.False(Collision2.CollidesWith(a, b, Thousandth));
            Assert.False(Collision2.CollidesWith(b, a, Thousandth));
            Assert.False(a.CollidesWith(b, Thousandth));
            Assert.True(a.CollidesWith(b, new GeometryHelper.Tolerance(2E-3, 1E-5)));
        }

        [Fact]
        public void TwoOriginsOneAndAHalfThousandthsApart_MeasureTheirGapAndAreJoinedOriginToOrigin()
        {
            GeoRay2 a = AlongX(), b = Leaving();

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.Equal(OriginGap, Distance2.DistanceTo(a, b, tolerance), 1E-12);
                Assert.Equal(OriginGap, Distance2.DistanceTo(b, a, tolerance), 1E-12);

                GeoLine2 line = Projection2.GetShortestLineTo(a, b, tolerance);
                Assert.Equal(OriginGap, line.Length, 1E-12);
                Assert.Equal(0.0, line.StartPoint.DistanceTo(a.Origin), 12);
                Assert.Equal(0.0, line.EndPoint.DistanceTo(b.Origin), 12);
            }
        }

        [Fact]
        public void TheCrossingTest_KeepsItsSlackBehindTheOrigins()
        {
            // A guard: the carriers cross at the origin, 0.0009 behind each; the point is placed on the first ray, at its
            // origin.
            Assert.True(Intersection2.TryIntersectWith(AlongX(), Leaving(), out GeoPoint2 at, Thousandth));
            Assert.Equal(0.0009, at.X, 12);
            Assert.Equal(0.0, at.Y, 12);
        }

        [Fact]
        public void TwoRaysTrulyCrossing_StillCollideAndMeasureNought()
        {
            // A guard: along X from the origin and along Y from (50, -50), crossing at (50, 0).
            var a = new GeoRay2(P(0, 0), U(1, 0));
            var b = new GeoRay2(P(50, -50), U(0, 1));

            Assert.True(a.CollidesWith(b, Thousandth));
            Assert.Equal(0.0, a.DistanceTo(b, Thousandth));
            Assert.Equal(0.0, Projection2.GetShortestLineTo(a, b, Thousandth).Length);
        }

        [Fact]
        public void ARayStartingOnAnother_TouchesItThereAndMeasuresNought()
        {
            // A guard: the ray up from (5, 0) starts on the ray along X from the origin.
            var a = new GeoRay2(P(0, 0), U(1, 0));
            var b = new GeoRay2(P(5, 0), U(0, 1));

            Assert.True(a.CollidesWith(b, Thousandth));
            Assert.True(b.CollidesWith(a, Thousandth));
            Assert.Equal(0.0, a.DistanceTo(b, Thousandth));
            Assert.Equal(0.0, b.DistanceTo(a, Thousandth));
            Assert.Equal(0.0, Projection2.GetShortestLineTo(a, b, Thousandth).StartPoint.DistanceTo(P(5, 0)), 12);
        }

        [Fact]
        public void TwoRaysAtHalfADegreeCrossingFarOut_StillMeasureNought()
        {
            // A guard: from (0, 5) along (1, -0.005) the ray reaches the X axis a thousand along, at half a degree.
            var a = new GeoRay2(P(0, 0), U(1, 0));
            var b = new GeoRay2(P(0, 5), U(1, -0.005));

            Assert.True(a.CollidesWith(b, Thousandth));
            Assert.Equal(0.0, a.DistanceTo(b, Thousandth));
            Assert.Equal(0.0, Projection2.GetShortestLineTo(a, b, Thousandth).Length);
        }
    }
}
