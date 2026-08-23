using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Core
{
    public class IntersectionTests
    {
        #region Line - Line Tests

        [Fact]
        public void LineLine_PerpendicularAndSkew()
        {
            var l1 = new GeoLine2(new GeoPoint2(0, 5), new GeoPoint2(10, 5));

            // Perpendicular at (5, 5)
            var lPerp = new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 10));
            var ptPerp = Intersection2.GetIntersection(l1, lPerp);
            Assert.NotNull(ptPerp);
            Assert.True(ptPerp.Value.IsEqualTo(new GeoPoint2(5, 5)));

            // Diagonal skew at (5, 5)
            var lDiag = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 10));
            var ptDiag = Intersection2.GetIntersection(l1, lDiag);
            Assert.NotNull(ptDiag);
            Assert.True(ptDiag.Value.IsEqualTo(new GeoPoint2(5, 5)));
        }

        [Fact]
        public void LineLine_T_JunctionAndEndpoints()
        {
            var l1 = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            // T-junction at (5, 0)
            var lT = new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 5));
            var ptT = Intersection2.GetIntersection(l1, lT);
            Assert.NotNull(ptT);
            Assert.True(ptT.Value.IsEqualTo(new GeoPoint2(5, 0)));

            // Endpoint touching at (10, 0)
            var lEnd = new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(20, 10));
            var ptEnd = Intersection2.GetIntersection(l1, lEnd);
            Assert.NotNull(ptEnd);
            Assert.True(ptEnd.Value.IsEqualTo(new GeoPoint2(10, 0)));
        }

        [Fact]
        public void LineLine_ParallelAndCollinear_NoIntersection()
        {
            var l1 = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            // Parallel2 line
            var lParallel = new GeoLine2(new GeoPoint2(0, 5), new GeoPoint2(10, 5));
            Assert.Null(Intersection2.GetIntersection(l1, lParallel));

            // Disjoint skew line
            var lDisjoint = new GeoLine2(new GeoPoint2(20, 20), new GeoPoint2(30, 30));
            Assert.Null(Intersection2.GetIntersection(l1, lDisjoint));
        }

        #endregion

        #region Circle - Line Tests

        [Fact]
        public void CircleLine_SecantAndTangent()
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 5);

            // Secant line with 2 intersection points at (-3, 4) and (3, 4)
            var lSecant = new GeoLine2(new GeoPoint2(-10, 4), new GeoPoint2(10, 4));
            var ptsSecant = Intersection2.GetIntersections(circle, lSecant);
            Assert.Equal(2, ptsSecant.Length);
            Assert.Equal(4.0, ptsSecant[0].Y, 3);
            Assert.Equal(4.0, ptsSecant[1].Y, 3);
            Assert.Equal(3.0, Math.Abs(ptsSecant[0].X), 3);

            // Tangent line with 1 intersection point at (0, 5)
            var lTangent = new GeoLine2(new GeoPoint2(-10, 5), new GeoPoint2(10, 5));
            var ptsTangent = Intersection2.GetIntersections(circle, lTangent);
            Assert.Single(ptsTangent);
            Assert.True(ptsTangent[0].IsEqualTo(new GeoPoint2(0, 5)));
        }

        [Fact]
        public void CircleLine_DiameterAndDisjoint()
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 5);

            // Diametrical line passing through center
            var lDiam = new GeoLine2(new GeoPoint2(-10, 0), new GeoPoint2(10, 0));
            var ptsDiam = Intersection2.GetIntersections(circle, lDiam);
            Assert.Equal(2, ptsDiam.Length);

            // Disjoint line
            var lDisjoint = new GeoLine2(new GeoPoint2(-10, 10), new GeoPoint2(10, 10));
            Assert.Empty(Intersection2.GetIntersections(circle, lDisjoint));
        }

        #endregion

        #region Circle - Circle Tests

        [Fact]
        public void CircleCircle_OverlappingAndTangentExt()
        {
            var c1 = new GeoCircle2(new GeoPoint2(0, 0), 5);

            // Overlapping with 2 intersection points
            var cOverlap = new GeoCircle2(new GeoPoint2(6, 0), 5);
            var ptsOverlap = Intersection2.GetIntersections(c1, cOverlap);
            Assert.Equal(2, ptsOverlap.Length);
            Assert.Equal(3.0, ptsOverlap[0].X, 3);
            Assert.Equal(3.0, ptsOverlap[1].X, 3);

            // Tangent externally with 1 intersection point at (5, 0)
            var cTangentExt = new GeoCircle2(new GeoPoint2(10, 0), 5);
            var ptsTangentExt = Intersection2.GetIntersections(c1, cTangentExt);
            Assert.Single(ptsTangentExt);
            Assert.True(ptsTangentExt[0].IsEqualTo(new GeoPoint2(5, 0)));
        }

        [Fact]
        public void CircleCircle_TangentIntAndDisjoint()
        {
            var cOuter = new GeoCircle2(new GeoPoint2(0, 0), 10);

            // Tangent internally with 1 intersection point at (10, 0)
            var cTangentInt = new GeoCircle2(new GeoPoint2(5, 0), 5);
            var ptsTangentInt = Intersection2.GetIntersections(cOuter, cTangentInt);
            Assert.Single(ptsTangentInt);
            Assert.True(ptsTangentInt[0].IsEqualTo(new GeoPoint2(10, 0)));

            // Concentric (0 intersection points)
            var cConcentric = new GeoCircle2(new GeoPoint2(0, 0), 5);
            Assert.Empty(Intersection2.GetIntersections(cOuter, cConcentric));

            // Completely disjoint
            var cDisjoint = new GeoCircle2(new GeoPoint2(30, 0), 5);
            Assert.Empty(Intersection2.GetIntersections(cOuter, cDisjoint));
        }

        #endregion

        #region Rectangle - Line Tests

        [Fact]
        public void RectangleLine_CrossingAndEndpointInside()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 20, 20, 0);

            // Line passing through (2 intersection points: (-10, 0) and (10, 0))
            var lCrossing = new GeoLine2(new GeoPoint2(-20, 0), new GeoPoint2(20, 0));
            var ptsCrossing = Intersection2.GetIntersections(rect, lCrossing);
            Assert.Equal(2, ptsCrossing.Length);

            // Line starting inside center and ending outside (1 point at (10, 0))
            var lHalf = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(20, 0));
            var ptsHalf = Intersection2.GetIntersections(rect, lHalf);
            Assert.Single(ptsHalf);
            Assert.True(ptsHalf[0].IsEqualTo(new GeoPoint2(10, 0)));
        }

        [Fact]
        public void RectangleLine_CornerTouchAndDisjoint()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 20, 20, 0);

            // Corner touching at (10, 10)
            var lCorner = new GeoLine2(new GeoPoint2(10, 10), new GeoPoint2(20, 20));
            var ptsCorner = Intersection2.GetIntersections(rect, lCorner);
            Assert.NotEmpty(ptsCorner);

            // Disjoint line
            var lDisjoint = new GeoLine2(new GeoPoint2(20, 20), new GeoPoint2(30, 20));
            Assert.Empty(Intersection2.GetIntersections(rect, lDisjoint));
        }

        #endregion

        #region Polygon & Polyline Tests

        [Fact]
        public void PolygonLine_ConvexAndConcaveCrossings()
        {
            var poly = new GeoPolygon2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(20, 0),
                new GeoPoint2(20, 20),
                new GeoPoint2(0, 20)
            });

            // Line crossing through polygon (2 points)
            var lCross = new GeoLine2(new GeoPoint2(10, -5), new GeoPoint2(10, 25));
            var ptsCross = Intersection2.GetIntersections(poly, lCross);
            Assert.Equal(2, ptsCross.Length);

            // Line disjoint
            var lDisjoint = new GeoLine2(new GeoPoint2(30, 0), new GeoPoint2(30, 20));
            Assert.Empty(Intersection2.GetIntersections(poly, lDisjoint));
        }

        [Fact]
        public void PolylineLine_CrossingMultipleSegments()
        {
            var pl = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10),
                new GeoPoint2(20, 10)
            });

            // Line crossing first horizontal segment at (5, 0)
            var l1 = new GeoLine2(new GeoPoint2(5, -5), new GeoPoint2(5, 5));
            var pts1 = Intersection2.GetIntersections(pl, l1);
            Assert.Single(pts1);
            Assert.True(pts1[0].IsEqualTo(new GeoPoint2(5, 0)));

            // Line crossing second horizontal segment at (15, 10)
            var l2 = new GeoLine2(new GeoPoint2(15, 5), new GeoPoint2(15, 15));
            var pts2 = Intersection2.GetIntersections(pl, l2);
            Assert.Single(pts2);
            Assert.True(pts2[0].IsEqualTo(new GeoPoint2(15, 10)));
        }

        [Fact]
        public void PolylinePolyline_ZigzagIntersections()
        {
            var pl1 = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 10),
                new GeoPoint2(20, 0)
            });

            var pl2 = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 10),
                new GeoPoint2(10, 0),
                new GeoPoint2(20, 10)
            });

            // Crosses twice: at (5, 5) and (15, 5)
            var pts = Intersection2.GetIntersections(pl1, pl2);
            Assert.Equal(2, pts.Length);
            Assert.True(pts[0].IsEqualTo(new GeoPoint2(5, 5)) || pts[1].IsEqualTo(new GeoPoint2(5, 5)));
            Assert.True(pts[0].IsEqualTo(new GeoPoint2(15, 5)) || pts[1].IsEqualTo(new GeoPoint2(15, 5)));
        }

        #endregion
        #region Scale Invariance Regression

        [Theory]
        [InlineData(1e-3)]
        [InlineData(1.0)]
        [InlineData(1e3)]
        [InlineData(1e6)]
        public void LineLine_PerpendicularCross_IsFoundAtEveryScale(double scale)
        {
            // The same configuration scaled up and down must give the same answer. Comparing the raw
            // cross product against a length tolerance used to make the small versions report no hit.
            var horizontal = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10 * scale, 0));
            var vertical = new GeoLine2(new GeoPoint2(5 * scale, -5 * scale), new GeoPoint2(5 * scale, 5 * scale));

            Assert.True(Intersection2.TryIntersectWith(horizontal, vertical, out GeoPoint2 hit));
            Assert.True(hit.IsEqualTo(new GeoPoint2(5 * scale, 0), new Tolerance(1e-6 * scale, 1e-6 * scale)));
        }

        [Fact]
        public void LineLine_EndpointSlack_IsMeasuredAsADistanceNotAParameter()
        {
            var longSegment = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100000, 0));

            // 5 units short of the start: far outside EqualPoint, so there is no intersection.
            var wellBefore = new GeoLine2(new GeoPoint2(-5, -5), new GeoPoint2(-5, 5));
            Assert.False(Intersection2.TryIntersectWith(longSegment, wellBefore, out _));

            // Half of EqualPoint short of the start: still counts as touching the endpoint.
            var justBefore = new GeoLine2(new GeoPoint2(-5e-5, -5), new GeoPoint2(-5e-5, 5));
            Assert.True(Intersection2.TryIntersectWith(longSegment, justBefore, out _));
        }

        [Fact]
        public void LineLine_NearlyParallelSegments_AgreeWithParallelIsParallel()
        {
            // Two long segments meeting at well under a degree. Whatever the verdict, the two operations
            // must not contradict each other by calling them parallel and intersecting at the same time.
            var a = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1000, 0));
            var b = new GeoLine2(new GeoPoint2(0, -1), new GeoPoint2(1000, 1));

            bool parallel = Parallel2.IsParallel(a, b);
            bool intersects = Intersection2.TryIntersectWith(a, b, out _);

            Assert.False(parallel && intersects);
        }

        [Fact]
        public void LineLine_DegenerateSegment_DoesNotIntersect()
        {
            var degenerate = new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 0));
            var crossing = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

            Assert.False(Intersection2.TryIntersectWith(degenerate, crossing, out _));
        }

        [Theory]
        [InlineData(1e-2)]
        [InlineData(1.0)]
        [InlineData(1e3)]
        public void CircleLine_SecantTangentAndMiss_AreClassifiedAtEveryScale(double scale)
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 10 * scale);

            var secant = new GeoLine2(new GeoPoint2(-20 * scale, 5 * scale), new GeoPoint2(20 * scale, 5 * scale));
            var tangent = new GeoLine2(new GeoPoint2(-20 * scale, 10 * scale), new GeoPoint2(20 * scale, 10 * scale));
            var miss = new GeoLine2(new GeoPoint2(-20 * scale, 15 * scale), new GeoPoint2(20 * scale, 15 * scale));

            Assert.Equal(2, Intersection2.GetIntersections(circle, secant).Length);
            Assert.Single(Intersection2.GetIntersections(circle, tangent));
            Assert.Empty(Intersection2.GetIntersections(circle, miss));
        }

        #endregion
    }
}
