using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A rectangle thinner than the default tolerance keeps its four sides against an arc, a chain and a loop, as it
    /// does against every straight shape: it is neither refused nor read as a segment.
    /// </summary>
    public class ThinRectangleAgainstCurvesTests
    {
        private const double Gap = 0.00175;

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        // 0.0005 wide and 10 tall, its left side at x = 20.00175, 0.00175 clear of the curves below at x = 20.
        private static GeoRectangle2 Thin() => new GeoRectangle2(new GeoPoint2(20.002, 0), 0.0005, 10, 0);

        private static GeoArc2 Arc() => new GeoArc2(new GeoPoint2(0, 0), 20, -Math.PI / 4.0, Math.PI / 4.0);

        private static GeoPolylineArc2 Chain() => new GeoPolylineArc2(
            new[] { new GeoPoint2(10, -10), new GeoPoint2(20, 0), new GeoPoint2(10, 10) },
            new[] { 0.0, 0.0, 0.0 });

        private static GeoPolygonArc2 Loop() => new GeoPolygonArc2(
            new[] { new GeoPoint2(0, -10), new GeoPoint2(20, -10), new GeoPoint2(20, 10), new GeoPoint2(0, 10) },
            new[] { 0.0, 0.0, 0.0, 0.0 });

        [Fact]
        public void ARectangleHalfAThousandthWide_IsMeasuredToAnArcAChainAndALoopBySides_AtTheDefaultTolerance()
        {
            GeoRectangle2 rect = Thin();
            GeoArc2 arc = Arc();
            GeoPolylineArc2 chain = Chain();
            GeoPolygonArc2 loop = Loop();

            Assert.InRange(rect.DistanceTo(arc), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(arc.DistanceTo(rect), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(rect.DistanceTo(chain), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(chain.DistanceTo(rect), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(rect.DistanceTo(loop), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(loop.DistanceTo(rect), Gap - 1E-9, Gap + 1E-9);
        }

        [Fact]
        public void ARectangleHalfAThousandthWide_IsMeasuredToAnArcAChainAndALoopBySides_AtAnExplicitTolerance()
        {
            GeoRectangle2 rect = Thin();
            GeoArc2 arc = Arc();
            GeoPolylineArc2 chain = Chain();
            GeoPolygonArc2 loop = Loop();
            Tolerance t = At(0.002);

            Assert.InRange(rect.DistanceTo(arc, t), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(arc.DistanceTo(rect, t), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(rect.DistanceTo(chain, t), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(chain.DistanceTo(rect, t), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(rect.DistanceTo(loop, t), Gap - 1E-9, Gap + 1E-9);
            Assert.InRange(loop.DistanceTo(rect, t), Gap - 1E-9, Gap + 1E-9);
        }

        [Fact]
        public void ARectangleHalfAThousandthWide_CollidesWithAnArcAChainAndALoopExactlyWhereItIsWithinTheTolerance()
        {
            GeoRectangle2 rect = Thin();
            GeoArc2 arc = Arc();
            GeoPolylineArc2 chain = Chain();
            GeoPolygonArc2 loop = Loop();
            Tolerance near = At(0.002);
            Tolerance far = At(0.0015);

            Assert.True(rect.CollidesWith(arc, near));
            Assert.True(arc.CollidesWith(rect, near));
            Assert.True(rect.CollidesWith(chain, near));
            Assert.True(chain.CollidesWith(rect, near));
            Assert.True(rect.CollidesWith(loop, near));
            Assert.True(loop.CollidesWith(rect, near));

            Assert.False(rect.CollidesWith(arc, far));
            Assert.False(arc.CollidesWith(rect, far));
            Assert.False(rect.CollidesWith(chain, far));
            Assert.False(chain.CollidesWith(rect, far));
            Assert.False(rect.CollidesWith(loop, far));
            Assert.False(loop.CollidesWith(rect, far));
        }

        [Fact]
        public void ARectangleHalfAThousandthWide_CrossingAnArcAChainAndALoop_IsNoughtFromThemAndCollides()
        {
            // Moved 0.002 left, the rectangle spans x from 19.99975 to 20.00025 and the curves pass through it.
            var rect = new GeoRectangle2(new GeoPoint2(20, 0), 0.0005, 10, 0);
            Tolerance t = At(1E-3);

            Assert.Equal(0.0, rect.DistanceTo(Arc(), t));
            Assert.Equal(0.0, rect.DistanceTo(Chain(), t));
            Assert.Equal(0.0, Loop().DistanceTo(rect, t));
            Assert.True(rect.CollidesWith(Arc(), t));
            Assert.True(Chain().CollidesWith(rect, t));
            Assert.True(rect.CollidesWith(Loop(), t));
        }
    }
}
