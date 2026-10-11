using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// A handful of checks that a question can be put from either side of a pair and comes back the same: the same
    /// bool, the same distance, the same points in the same order, and the shortest line turned round. The full guard
    /// and mirror suites live elsewhere; these only show the wiring holds.
    /// </summary>
    public class QueryEitherSideSmokeTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        [Fact]
        public void AStraightChainAndATriangleAnswerTheSameFromEitherSide()
        {
            // A chain in z = 0 along x to (400, 0), then up to (400, 200), through a triangle standing across x = 200.
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0));
            var triangle = new GeoTriangle3(new GeoPoint3(200, -100, -100), new GeoPoint3(200, 100, -100), new GeoPoint3(200, 0, 100));

            Assert.Equal(chain.CollidesWith(triangle, Tol), triangle.CollidesWith(chain, Tol));
            Assert.Equal(chain.GetIntersections(triangle, Tol), triangle.GetIntersections(chain, Tol));
            Assert.Equal(chain.TryIntersectWith(triangle, out GeoPoint3[] fromChain, Tol), triangle.TryIntersectWith(chain, out GeoPoint3[] fromTriangle, Tol));
            Assert.Equal(fromChain, fromTriangle);
            Assert.Single(fromTriangle);
        }

        [Fact]
        public void ASegmentMeetsABentEdgeWhereTheEdgeMeetsIt()
        {
            // A half circle of radius 50 from (0, 0, 0) to (100, 0, 0), bulging in z = 0, cut by a segment along x = 50.
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1.0, GeoVector3.ZAxis);
            var line = new GeoLine3(new GeoPoint3(50, -100, 0), new GeoPoint3(50, 100, 0));

            Assert.Equal(edge.CollidesWith(line, Tol), line.CollidesWith(edge, Tol));
            Assert.Equal(edge.GetIntersections(line, Tol), line.GetIntersections(edge, Tol));
            Assert.Single(line.GetIntersections(edge, Tol));
        }

        [Fact]
        public void APointIsAsFarFromACurvedChainAsTheChainIsFromIt()
        {
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0) });
            var point = new GeoPoint3(150, 30, 40);

            Assert.Equal(chain.DistanceTo(point, Tol), point.DistanceTo(chain, Tol));
            Assert.Equal(chain.DistanceTo(point), point.DistanceTo(chain));
        }

        [Fact]
        public void TheShortestLineToACurvedLoopIsTheLoopsLineTurnedRound()
        {
            // A square 100 by 100 in z = 0, and a point 40 above a place 30 outside its right side.
            var loop = new GeoPolygonArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0) });
            var point = new GeoPoint3(130, 50, 40);

            GeoLine3 fromLoop = loop.GetShortestLineTo(point, Tol);
            GeoLine3 fromPoint = point.GetShortestLineTo(loop, Tol);

            Assert.Equal(fromLoop.EndPoint, fromPoint.StartPoint);
            Assert.Equal(fromLoop.StartPoint, fromPoint.EndPoint);
            Assert.Equal(50.0, fromPoint.Length, 9);
        }

        [Fact]
        public void AnArcMeetsAFaceAtTheSamePointsFromEitherSide()
        {
            // A square 100 by 100 with a quarter circle of radius 100 round its corner at the origin running across it.
            var face = new GeoFace2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100)));
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, Math.PI / 2);

            Assert.Equal(face.TryIntersectWith(arc, out GeoPoint2[] fromFace, Tol), arc.TryIntersectWith(face, out GeoPoint2[] fromArc, Tol));
            Assert.Equal(fromFace, fromArc);
            Assert.NotEmpty(fromArc);
        }

        [Fact]
        public void ANullFaceIsRefusedByName()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, Math.PI / 2);

            ArgumentNullException refused = Assert.Throws<ArgumentNullException>(() => arc.TryIntersectWith((GeoFace2)null, out GeoPoint2[] _, Tol));

            Assert.Equal("face", refused.ParamName);
        }

        [Fact]
        public void TheShortestLineFromACurveToAPointStartsOnItAndIsAsLongAsTheDistance()
        {
            var point = new GeoPoint2(130, 40);
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100));
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) }, new[] { 0.0, 0.5, 0.0 });
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1.0);

            GeoLine2 fromLine = line.GetShortestLineTo(point, Tol);
            Assert.Equal(new GeoPoint2(100, 0), fromLine.StartPoint);
            Assert.Equal(point, fromLine.EndPoint);
            Assert.Equal(line.DistanceTo(point), fromLine.Length);
            Assert.Equal(50.0, fromLine.Length, 12);

            GeoLine2 fromPolyline = polyline.GetShortestLineTo(point, Tol);
            Assert.Equal(polyline.GetClosestPointOnBoundary(point), fromPolyline.StartPoint);
            Assert.Equal(polyline.DistanceTo(point), fromPolyline.Length);

            GeoLine2 fromChain = chain.GetShortestLineTo(point, Tol);
            Assert.Equal(chain.GetClosestPointOnBoundary(point, Tol), fromChain.StartPoint);
            Assert.Equal(chain.DistanceTo(point, Tol), fromChain.Length);

            GeoLine2 fromEdge = edge.GetShortestLineTo(point, Tol);
            Assert.Equal(edge.GetClosestPointOnBoundary(point, Tol), fromEdge.StartPoint);
            Assert.Equal(edge.DistanceTo(point, Tol), fromEdge.Length);

            // From the point's side, each is the same line turned round.
            Assert.Equal(fromLine.Reverse(), point.GetShortestLineTo(line, Tol));
            Assert.Equal(fromPolyline.Reverse(), point.GetShortestLineTo(polyline, Tol));
            Assert.Equal(fromChain.Reverse(), point.GetShortestLineTo(chain, Tol));
            Assert.Equal(fromEdge.Reverse(), point.GetShortestLineTo(edge, Tol));
        }

        [Fact]
        public void TheShortestLineFromARegionRunsFromItsBoundaryEvenForAPointInside()
        {
            // The square 100 by 100 on the origin: (50, 30) is 30 inside its bottom edge, (50, -40) is 40 below it.
            var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));
            var inside = new GeoPoint2(50, 30);
            var outside = new GeoPoint2(50, -40);

            GeoLine2 fromInside = square.GetShortestLineTo(inside, Tol);
            Assert.Equal(new GeoPoint2(50, 0), fromInside.StartPoint);
            Assert.Equal(30.0, fromInside.Length, 12);
            Assert.Equal(0.0, square.DistanceTo(inside));

            GeoLine2 fromOutside = square.GetShortestLineTo(outside, Tol);
            Assert.Equal(square.DistanceTo(outside), fromOutside.Length);
            Assert.Equal(fromOutside.Reverse(), outside.GetShortestLineTo(square, Tol));

            // The centre of a circle goes where the circle's own nearest point goes.
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 50);
            GeoLine2 fromCentre = circle.GetShortestLineTo(new GeoPoint2(0, 0), Tol);
            Assert.Equal(circle.GetClosestPointOnBoundary(new GeoPoint2(0, 0)), fromCentre.StartPoint);
            Assert.Equal(50.0, fromCentre.Length, 12);
        }

        [Fact]
        public void APointOnACurveGetsALineOfNoLength()
        {
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            var onIt = new GeoPoint2(40, 0);

            GeoLine2 found = line.GetShortestLineTo(onIt, Tol);

            Assert.Equal(0.0, found.Length);
            Assert.Equal(onIt, found.StartPoint);
        }
    }
}
