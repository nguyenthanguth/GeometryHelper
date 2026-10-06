using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The shortest segment from an ellipse to other shapes, and the edge of a shape nearest an ellipse. As the circle's,
    /// the segment leaves the rim and lands on the other shape's boundary (Projection2.cs:741-745, 1093-1113): two shapes
    /// that cross are joined by a segment of no length at their first crossing (Projection2.cs:557-560, 757-760,
    /// Arc2.cs:443-446), and one lying inside the other is joined rim to boundary, with a length, though DistanceTo calls
    /// it nought (GeoCircle2.Projection.cs:52-58). The nearest edge is the one least far from the ellipse read as a
    /// region, the first of equals winning (ClosestEdge2.cs:37-61, 97-102), so a shape inside the ellipse names its first
    /// edge. Lengths are held against the oracle's gap between the rim and the other shape.
    /// </summary>
    public class Ellipse2ProjectionTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoEllipse2 Upright() => Ellipse2Oracle.Upright(300, 100);

        private static GeoEllipse2 Tilted() => Ellipse2Oracle.Tilted();

        private static double RimToSegments(GeoEllipse2 ellipse, IEnumerable<GeoLine2> lines)
        {
            GeoLine2[] all = lines.ToArray();
            return Ellipse2Oracle.RimTo(ellipse, p => all.Min(line => Ellipse2Oracle.PointToSegment(p, line)));
        }

        private static double PointToEdge(GeoPoint2 point, GeoEdge2 edge) => edge.IsArc
            ? Ellipse2Oracle.PointToArc(point, edge.ToArc())
            : Ellipse2Oracle.PointToSegment(point, edge.ToLine());

        // The segment leaves the rim, lands on the other shape, and is as long as the gap between them.
        private static void AssertJoins(GeoEllipse2 ellipse, GeoLine2 shortest, Func<GeoPoint2, double> toOther, double gap)
        {
            Assert.InRange(Ellipse2Oracle.GapToRim(ellipse, shortest.StartPoint), 0.0, 1E-9);
            Assert.InRange(toOther(shortest.EndPoint), 0.0, 1E-9);
            Assert.Equal(gap, shortest.Length, 9);
        }

        #region Segments

        [Fact]
        public void TheShortestLineToASegmentApart_LeavesTheRimAndLandsOnTheSegment()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, 100, 250, 450, -50);

            AssertJoins(ellipse, ellipse.GetShortestLineTo(line, Strict), p => Ellipse2Oracle.PointToSegment(p, line), RimToSegments(ellipse, new[] { line }));
        }

        [Fact]
        public void TheShortestLineToACrossingSegment_IsThePointOfTheFirstCrossing()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, 400, 50, -400, 50);
            GeoLine2 shortest = ellipse.GetShortestLineTo(line, Strict);

            Assert.Equal(0.0, shortest.Length);
            Assert.Equal(ellipse.GetIntersections(line, Strict)[0], shortest.StartPoint);
        }

        [Fact]
        public void TheShortestLineToASegmentInside_RunsFromItsNearerEndOutToTheRim()
        {
            // From (-100, 0), 93.54 from the rim, to (200, 30), nearer it: the line runs from the rim in to (200, 30), and
            // is as long as that end's gap, though the segment is nought from the ellipse as a region. The circle takes the
            // end furthest from its centre (Projection2.cs:572-578), the one nearer its rim.
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, -100, 0, 200, 30);
            GeoLine2 shortest = ellipse.GetShortestLineTo(line, Strict);

            Assert.Equal(0.0, ellipse.DistanceTo(line, Strict));
            AssertJoins(ellipse, shortest, p => p.DistanceTo(line.EndPoint), Ellipse2Oracle.ClosestDistance(300, 100, 200, 30));
        }

        [Fact]
        public void TheShortestLineAboveTheFlankOfAThinEllipse_RunsStraightUpFromTheTop()
        {
            // 1 000 by 1, the segment y = 1.5: the gap along the flank grows only as x² / 2 000 000, so the foot is pinned
            // to a thousandth, the length to rounding.
            GeoEllipse2 thin = Ellipse2Oracle.Upright(1000, 1);
            GeoLine2 shortest = thin.GetShortestLineTo(Ellipse2Oracle.FrameLine(thin, -200, 1.5, 200, 1.5), Strict);

            Assert.Equal(0.5, shortest.Length, 9);
            Assert.InRange(shortest.StartPoint.DistanceTo(Ellipse2Oracle.FromFrame(thin, 0, 1)), 0.0, 1E-3);
            Assert.InRange(shortest.EndPoint.DistanceTo(Ellipse2Oracle.FromFrame(thin, 0, 1.5)), 0.0, 1E-3);
        }

        #endregion

        #region Straight shapes

        [Fact]
        public void TheShortestLineToAStraightShapeApart_IsAsLongAsTheGapToItsNearestEdge()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoPolyline2 polyline = Ellipse2Oracle.FramePolyline(ellipse, -400, 300, -150, 150, 150, 150, 500, 120);
            GeoPolygon2 polygon = Ellipse2Oracle.FramePolygon(ellipse, 330, -40, 420, 40, 330, 120);
            var rectangle = new GeoRectangle2(Ellipse2Oracle.FromFrame(ellipse, 0, 200), 120, 60, Math.PI / 6);
            GeoPoint2[] corners = Ellipse2Oracle.FramePoints(ellipse, -380, 40, -330, -90, -450, -20);
            var triangle = new GeoTriangle2(corners[0], corners[1], corners[2]);
            GeoLine2[] triangleEdges = { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) };

            AssertJoins(ellipse, ellipse.GetShortestLineTo(polyline, Strict), p => polyline.GetEdges().Min(e => Ellipse2Oracle.PointToSegment(p, e)), RimToSegments(ellipse, polyline.GetEdges()));
            AssertJoins(ellipse, ellipse.GetShortestLineTo(polygon, Strict), p => Ellipse2Oracle.PointToOutline(p, polygon), RimToSegments(ellipse, polygon.GetEdges()));
            AssertJoins(ellipse, ellipse.GetShortestLineTo(rectangle, Strict), p => Ellipse2Oracle.PointToOutline(p, rectangle.ToPolygon()), RimToSegments(ellipse, rectangle.GetEdges()));
            AssertJoins(ellipse, ellipse.GetShortestLineTo(triangle, Strict), p => triangleEdges.Min(e => Ellipse2Oracle.PointToSegment(p, e)), RimToSegments(ellipse, triangleEdges));
        }

        [Fact]
        public void APolygonInsideTheEllipse_IsJoinedOutToTheRim_ThoughItIsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoPolygon2 inside = Ellipse2Oracle.FramePolygon(ellipse, -50, -20, 50, -20, 0, 40);
            GeoLine2 shortest = ellipse.GetShortestLineTo(inside, Strict);

            Assert.Equal(0.0, ellipse.DistanceTo(inside, Strict));
            Assert.InRange(shortest.Length, 50.0, 100.0);
            AssertJoins(ellipse, shortest, p => Ellipse2Oracle.PointToOutline(p, inside), RimToSegments(ellipse, inside.GetEdges()));
        }

        [Fact]
        public void AnEllipseInsideAPolygon_IsJoinedOutToThePolygon()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoPolygon2 round = Ellipse2Oracle.FramePolygon(ellipse, -400, -200, 400, -200, 400, 160, -400, 160);

            AssertJoins(ellipse, ellipse.GetShortestLineTo(round, Strict), p => Ellipse2Oracle.PointToOutline(p, round), 60.0);
        }

        [Fact]
        public void TheShortestLineToAPolygonAcrossTheRim_IsItsFirstCrossingInTheCirclesOrder()
        {
            // The first edge that meets the rim gives the line, at its first crossing along it (Projection2.cs:1093-1113;
            // A8): the bottom edge of the band, drawn from the right, at t = 330°, the first of GetIntersections.
            GeoEllipse2 ellipse = Tilted();
            GeoPolygon2 band = Ellipse2Oracle.FramePolygon(ellipse, 400, -50, -400, -50, -400, 50, 400, 50);
            GeoLine2 shortest = ellipse.GetShortestLineTo(band, Strict);

            Assert.Equal(0.0, shortest.Length);
            Assert.InRange(shortest.StartPoint.DistanceTo(Ellipse2Oracle.Rim(ellipse, 11 * Math.PI / 6)), 0.0, 1E-9);
            Assert.Equal(ellipse.GetIntersections(band, Strict)[0], shortest.StartPoint);
        }

        [Fact]
        public void FromInsideAHoleOfAFace_TheShortestLineLandsOnTheHole()
        {
            // The hole, 400 by 160 about the centre, is 60 from the top; the outline is hundreds away (Face2.cs:305-325).
            GeoEllipse2 ellipse = Upright();
            GeoPolygon2 outline = Ellipse2Oracle.FramePolygon(ellipse, -2000, -2000, 2000, -2000, 2000, 2000, -2000, 2000);
            GeoPolygon2 hole = Ellipse2Oracle.FramePolygon(ellipse, -400, -200, 400, -200, 400, 160, -400, 160);
            var face = new GeoFace2(outline, new[] { hole });

            AssertJoins(ellipse, ellipse.GetShortestLineTo(face, Strict), p => Ellipse2Oracle.PointToOutline(p, hole), 60.0);
        }

        [Fact]
        public void TheShortestLineToAnEdge_IsTheOneToItsSegmentOrItsArc()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, 100, 250, 450, -50);
            GeoArc2 arc = Ellipse2Oracle.FrameArc(ellipse, 0, 250, 50, Math.PI, 2 * Math.PI);

            Assert.Equal(ellipse.GetShortestLineTo(line, Strict), ellipse.GetShortestLineTo(new GeoEdge2(line.StartPoint, line.EndPoint), Strict));
            AssertJoins(ellipse, ellipse.GetShortestLineTo(new GeoEdge2(arc), Strict), p => Ellipse2Oracle.PointToArc(p, arc), 100.0);
        }

        #endregion

        #region Circles and arcs

        [Fact]
        public void TheShortestLineToACircleAbove_RunsFromTheTopToTheBottomOfTheCircle()
        {
            // The gap is least, and flat, at the top: the ends are pinned to a ten-thousandth, the length to rounding.
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 shortest = ellipse.GetShortestLineTo(Ellipse2Oracle.FrameCircle(ellipse, 0, 250, 50), Strict);

            Assert.InRange(shortest.StartPoint.DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100)), 0.0, 1E-4);
            Assert.InRange(shortest.EndPoint.DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 200)), 0.0, 1E-4);
            Assert.Equal(100.0, shortest.Length, 9);
        }

        [Fact]
        public void ACircleInside_IsJoinedFromTheRimPointNearestItsCentre()
        {
            // Radius 30 about (50, 0): its centre is 98.43 from the rim, at the two points mirrored across the major axis,
            // so 68.43 from rim to rim (Projection2.cs:776-781 for two circles).
            GeoEllipse2 ellipse = Upright();
            GeoCircle2 circle = Ellipse2Oracle.FrameCircle(ellipse, 50, 0, 30);

            AssertJoins(ellipse, ellipse.GetShortestLineTo(circle, Strict), p => Math.Abs(p.DistanceTo(circle.Center) - 30), Ellipse2Oracle.ClosestDistance(300, 100, 50, 0) - 30);
        }

        [Fact]
        public void ACircleAboutTheCentre_IsJoinedFromTheEndOfTheMinorAxisAtNinetyDegrees()
        {
            // Radius 50 about the centre: the rim is nearest it at both ends of the minor axis, and the line starts at the
            // one at t = 90°, (0, 100), as the closest point of the centre does (amendment A1), running 50 down to (0, 50).
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 shortest = ellipse.GetShortestLineTo(Ellipse2Oracle.FrameCircle(ellipse, 0, 0, 50), Strict);

            Assert.InRange(shortest.StartPoint.DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100)), 0.0, 1E-9);
            Assert.InRange(shortest.EndPoint.DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 50)), 0.0, 1E-9);
        }

        [Fact]
        public void AnEllipseInsideACircle_IsJoinedFromItsRimPointFarthestFromTheCentre()
        {
            // Radius 400 about (-50, 0): the rim is farthest from that centre at the tip (300, 0), 350 off, so the line runs
            // from the tip to (350, 0), 50 long, on the same side of both, as two nested circles are joined
            // (Projection2.cs:782-787). The nearest rim point to the centre would give a line 290 long. The ends sit where
            // the gap is flat, so they are pinned to a ten-thousandth, the length to rounding.
            GeoEllipse2 ellipse = Upright();
            GeoLine2 shortest = ellipse.GetShortestLineTo(Ellipse2Oracle.FrameCircle(ellipse, -50, 0, 400), Strict);

            Assert.InRange(shortest.StartPoint.DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 300, 0)), 0.0, 1E-4);
            Assert.InRange(shortest.EndPoint.DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 350, 0)), 0.0, 1E-4);
            Assert.Equal(50.0, shortest.Length, 9);
        }

        [Fact]
        public void TheShortestLineToACrossingCircle_IsThePointOfTheFirstCrossing()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoCircle2 circle = Ellipse2Oracle.FrameCircle(ellipse, 0, 0, 200);
            GeoLine2 shortest = ellipse.GetShortestLineTo(circle, Strict);

            Assert.Equal(0.0, shortest.Length);
            Assert.Equal(ellipse.GetIntersections(circle, Strict)[0], shortest.StartPoint);
        }

        [Fact]
        public void AnArcApart_AndAnArcInside_AreJoinedRimToArc()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoArc2 facing = Ellipse2Oracle.FrameArc(ellipse, 0, 250, 50, Math.PI, 2 * Math.PI);
            GeoArc2 inside = Ellipse2Oracle.FrameArc(ellipse, 0, 0, 60, 0.3, 2.0);

            AssertJoins(ellipse, ellipse.GetShortestLineTo(facing, Strict), p => Ellipse2Oracle.PointToArc(p, facing), 100.0);
            AssertJoins(ellipse, ellipse.GetShortestLineTo(inside, Strict), p => Ellipse2Oracle.PointToArc(p, inside), Ellipse2Oracle.RimTo(ellipse, p => Ellipse2Oracle.PointToArc(p, inside)));
        }

        [Fact]
        public void TheShortestLineToACrossingArc_IsThePointOfTheFirstCrossing()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoArc2 upper = Ellipse2Oracle.FrameArc(ellipse, 0, 0, 200, 0.0, Math.PI);
            GeoLine2 shortest = ellipse.GetShortestLineTo(upper, Strict);

            Assert.Equal(0.0, shortest.Length);
            Assert.Equal(ellipse.GetIntersections(upper, Strict)[0], shortest.StartPoint);
        }

        #endregion

        #region Curved chains

        [Fact]
        public void TheShortestLineToACurvedChainApart_IsAsLongAsTheGapToItsNearestEdge()
        {
            GeoEllipse2 ellipse = Tilted();
            var chain = new GeoPolylineArc2(Ellipse2Oracle.FramePoints(ellipse, -300, 160, -60, 140, 60, 140, 200, 220), new[] { 0.0, -1.0, 0.4, 0.0 });
            GeoEdge2[] edges = chain.GetEdges().ToArray();

            AssertJoins(ellipse, ellipse.GetShortestLineTo(chain, Strict), p => edges.Min(e => PointToEdge(p, e)), Ellipse2Oracle.RimTo(ellipse, p => edges.Min(e => PointToEdge(p, e))));
        }

        [Fact]
        public void AnEllipseInsideACurvedLoop_IsJoinedOutToTheLoop()
        {
            GeoEllipse2 ellipse = Tilted();
            var round = new GeoPolygonArc2(Ellipse2Oracle.FramePoints(ellipse, -400, -200, 400, -200, 400, 200, -400, 200), new[] { 0.2, 0.0, 0.2, 0.0 });
            GeoEdge2[] edges = round.GetEdges().ToArray();

            AssertJoins(ellipse, ellipse.GetShortestLineTo(round, Strict), p => edges.Min(e => PointToEdge(p, e)), Ellipse2Oracle.RimTo(ellipse, p => edges.Min(e => PointToEdge(p, e))));
        }

        #endregion

        #region Ellipses

        [Fact]
        public void TheShortestLineToACopyAbove_RunsFromTheTopToItsBottom()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 shortest = ellipse.GetShortestLineTo(Ellipse2Oracle.FrameEllipse(ellipse, 0, 250, 0, 300, 100), Strict);

            Assert.InRange(shortest.StartPoint.DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100)), 0.0, 1E-3);
            Assert.InRange(shortest.EndPoint.DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 150)), 0.0, 1E-3);
            Assert.Equal(50.0, shortest.Length, 9);
        }

        [Fact]
        public void AnEllipseInsideAnother_IsJoinedRimToRim()
        {
            // 200 by 50 turned 0.2 about the same centre: nought apart as regions, joined across the gap between the rims.
            GeoEllipse2 ellipse = Tilted();
            GeoEllipse2 inner = Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.2, 200, 50);
            GeoLine2 shortest = ellipse.GetShortestLineTo(inner, Strict);

            Assert.Equal(0.0, ellipse.DistanceTo(inner, Strict));
            AssertJoins(ellipse, shortest, p => Ellipse2Oracle.GapToRim(inner, p), Ellipse2Oracle.RimToRim(ellipse, inner));
        }

        [Fact]
        public void TheShortestLineBetweenTwoEllipsesApart_JoinsTheirRimsAcrossTheGap()
        {
            // The far pair of 510.7 by 95.6 and 98.2 by 0.37, 362.528 apart (the second row of the distance table).
            var first = new GeoEllipse2(new GeoPoint2(-354.102, -14.719), new GeoVector2(Math.Cos(4.597074), Math.Sin(4.597074)), 510.745, 95.5613);
            var second = new GeoEllipse2(new GeoPoint2(-395.9041, -918.101), new GeoVector2(Math.Cos(0.499146), Math.Sin(0.499146)), 98.244, 0.3696);

            AssertJoins(first, first.GetShortestLineTo(second, Strict), p => Ellipse2Oracle.GapToRim(second, p), 362.52764221318999);
        }

        [Fact]
        public void TheShortestLineToACrossingEllipse_IsThePointOfTheFirstCrossing()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoEllipse2 turned = Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, Math.PI / 2, 300, 100);
            GeoLine2 shortest = ellipse.GetShortestLineTo(turned, Strict);

            Assert.Equal(0.0, shortest.Length);
            Assert.Equal(ellipse.GetIntersections(turned, Strict)[0], shortest.StartPoint);
        }

        #endregion

        #region The nearest edge

        [Fact]
        public void TheClosestEdgeOfAShapeApart_IsTheEdgeNearestTheRim()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoPolygon2 polygon = Ellipse2Oracle.FramePolygon(ellipse, 330, -40, 420, 40, 330, 120);
            GeoPolyline2 polyline = Ellipse2Oracle.FramePolyline(ellipse, -400, 300, -150, 150, 150, 150, 500, 120);
            var rectangle = new GeoRectangle2(Ellipse2Oracle.FromFrame(ellipse, 0, 200), 120, 60, Math.PI / 6);
            GeoPoint2[] corners = Ellipse2Oracle.FramePoints(ellipse, -380, 40, -330, -90, -450, -20);
            var triangle = new GeoTriangle2(corners[0], corners[1], corners[2]);

            Func<GeoLine2, double> gap = line => RimToSegments(ellipse, new[] { line });
            Assert.Equal(polygon.GetEdges().OrderBy(gap).First(), ellipse.GetClosestEdge(polygon, Strict));
            Assert.Equal(polyline.GetEdges().OrderBy(gap).First(), ellipse.GetClosestEdge(polyline, Strict));
            Assert.Equal(rectangle.GetEdges().OrderBy(gap).First(), ellipse.GetClosestEdge(rectangle, Strict));
            Assert.Equal(new[] { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) }.OrderBy(gap).First(), ellipse.GetClosestEdge(triangle, Strict));
        }

        [Fact]
        public void APolygonInsideTheEllipse_NamesItsFirstEdge()
        {
            // Every edge is nought from the ellipse as a region, and the first of equals wins (ClosestEdge2.cs:37-61).
            GeoEllipse2 ellipse = Tilted();
            GeoPolygon2 inside = Ellipse2Oracle.FramePolygon(ellipse, 50, -20, 0, 40, -50, -20);

            Assert.Equal(inside.GetEdgeAt(0), ellipse.GetClosestEdge(inside, Strict));
        }

        [Fact]
        public void APolygonAcrossTheRim_NamesTheFirstEdgeThatMeetsIt()
        {
            // The first edge, from (500, 200) to (500, -200), is 200 beyond the tip; the second, on to (0, -50) inside,
            // crosses the rim, and so does the third, up through the top: the second is named.
            GeoEllipse2 ellipse = Upright();
            GeoPolygon2 polygon = Ellipse2Oracle.FramePolygon(ellipse, 500, 200, 500, -200, 0, -50, 0, 200);

            Assert.Equal(polygon.GetEdgeAt(1), ellipse.GetClosestEdge(polygon, Strict));
        }

        [Fact]
        public void TheClosestEdgeOfACurvedLoop_IsTheArcFacingTheTip()
        {
            // The square from x = 350 to 450 beside the tip, its left side bowed 12 towards the tip by the bulge 0.3.
            GeoEllipse2 ellipse = Upright();
            var loop = new GeoPolygonArc2(Ellipse2Oracle.FramePoints(ellipse, 350, -40, 450, -40, 450, 40, 350, 40), new[] { 0.0, 0.5, 0.0, 0.3 });
            GeoEdge2 closest = ellipse.GetClosestEdge(loop, Strict);

            Assert.Equal(loop.GetEdges().ElementAt(3), closest);
            Assert.True(closest.IsArc);
        }

        [Fact]
        public void TheClosestEdgeOfACurvedChain_IsTheHalfTurnNearestTheTop()
        {
            // Above the top: a straight edge from (-300, 160) to (-60, 180), a half turn from there to (60, 180) bowed down
            // to (0, 120), 20 above the top, and a shallow arc on to (200, 220), every point of which is further off.
            GeoEllipse2 ellipse = Upright();
            var chain = new GeoPolylineArc2(Ellipse2Oracle.FramePoints(ellipse, -300, 160, -60, 180, 60, 180, 200, 220), new[] { 0.0, 1.0, 0.4, 0.0 });
            GeoEdge2 closest = ellipse.GetClosestEdge(chain, Strict);

            Assert.Equal(chain.GetEdges().ElementAt(1), closest);
            Assert.Equal(20.0, ellipse.DistanceTo(closest, Strict), 9);
        }

        #endregion
    }
}
