using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Where the rim of an ellipse meets other shapes. The answers are the circle's, read for an ellipse: a closed shape is
    /// met on its boundary (Intersection2.cs:540-562 for a polygon, Face2.cs:285-300 for a face and its holes), a crossing
    /// within the point tolerance of the end of a segment or an arc counts (Intersection2.cs:849-857, Arc2.cs:559-595), a
    /// point two edges share is given once (Intersection2.cs:865-875, ArcChain2.cs:324-335), two crossings of one pair
    /// whose half chord is within the tolerance are one, as two circles' are (Intersection2.cs:326-330; amendment A9), and
    /// a segment within the tolerance of the rim, by its gap or by its depth, meets it once at the foot on the segment, as
    /// the circle's tangent band gives it (Intersection2.cs:257-262; A10). A touch of two curves is the midpoint of the
    /// rim point where the gap is least and the nearest point of the other curve (A10). TryIntersectWith says whether
    /// there is any (Intersection2.cs:225-273). The order
    /// is the circle's where the other shape is straight or a chain (amendment A8): along a segment (Intersection2.cs:
    /// 265-266), edge by edge (Intersection2.cs:548-562, ArcChain2.cs:451-468), the outline then the holes of a face
    /// (Face2.cs:285-300); against a circle, an arc or an ellipse the points run by the eccentric angle t of this
    /// ellipse. The expected points are worked out without the type: by hand for
    /// the symmetric cases, checked at 40 digits, and otherwise by the sign changes of the other shape's equation round a
    /// dense sample of the rim (<see cref="Ellipse2Oracle.RimCrossings"/>).
    /// </summary>
    public class Ellipse2IntersectionTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Tenth = new Tolerance(0.1, 1E-5);

        // The upright 300 by 100 about (40, -25): its frame is the drawing's, moved, so that every number below is exact.
        private static GeoEllipse2 Upright() => Ellipse2Oracle.Upright(300, 100);

        private static GeoEllipse2 Tilted() => Ellipse2Oracle.Tilted();

        private static void AssertPoints(GeoPoint2[] expected, GeoPoint2[] actual, double within)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.InRange(expected[i].DistanceTo(actual[i]), 0.0, within);
            }
        }

        // Every expected point is matched by one of the answers, whatever their order.
        private static void AssertSamePoints(GeoPoint2[] expected, GeoPoint2[] actual, double within)
        {
            Assert.Equal(expected.Length, actual.Length);
            foreach (GeoPoint2 point in expected)
            {
                Assert.Contains(actual, p => p.DistanceTo(point) <= within);
            }
        }

        private static GeoPoint2[] RimAt(GeoEllipse2 ellipse, params double[] angles) => angles.Select(t => Ellipse2Oracle.Rim(ellipse, t)).ToArray();

        // One point, where the touch is expected, and within half the tolerance of 0.001 of the rim.
        private static void AssertTouch(GeoEllipse2 ellipse, GeoPoint2[] found, GeoPoint2 at)
        {
            Assert.Single(found);
            Assert.InRange(found[0].DistanceTo(at), 0.0, 1E-4);
            Assert.InRange(Ellipse2Oracle.GapToRim(ellipse, found[0]), 0.0, 5E-4);
        }

        // The height of the upright 300 by 100's rim above its centre at x = h.
        private static double RimHeight(double h) => 100.0 * Math.Sqrt(1.0 - h * h / 90000.0);

        private static void AssertRunsByT(GeoEllipse2 ellipse, GeoPoint2[] points)
        {
            for (int i = 1; i < points.Length; i++)
            {
                Assert.True(Ellipse2Oracle.AngleOf(ellipse, points[i - 1]) < Ellipse2Oracle.AngleOf(ellipse, points[i]), "the crossings run by t");
            }
        }

        #region Segments

        [Fact]
        public void ASegmentAcrossTheTiltedEllipse_CrossesTheRimAtBothRootsOfTheQuadratic()
        {
            // Along y = 50 in the frame the rim is at x = ±300 cos 30° = ±259.808, the points at t = 30° and 150°.
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, 400, 50, -400, 50);
            GeoPoint2[] expected = RimAt(ellipse, Math.PI / 6, 5 * Math.PI / 6);

            AssertPoints(expected, ellipse.GetIntersections(line, Strict), 1E-9);

            Assert.True(ellipse.TryIntersectWith(line, out GeoPoint2[] found, Strict));
            AssertPoints(expected, found, 1E-9);
        }

        [Fact]
        public void TheCrossingsOfASegment_RunAlongIt_WhicheverWayItIsDrawn()
        {
            // The same two points as above, the segment drawn from the left: t = 150° comes first, as the circle reports
            // them along the segment (Intersection2.cs:265-266; A8).
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, -400, 50, 400, 50);

            AssertPoints(RimAt(ellipse, 5 * Math.PI / 6, Math.PI / 6), ellipse.GetIntersections(line, Strict), 1E-9);
        }

        [Fact]
        public void ASegmentAlongTheTangentAtTheTop_MeetsTheRimOnceAtTheEndOfTheMinorAxis()
        {
            // y = 100 in the frame touches the rim at t = 90° and nowhere else; the two roots of the quadratic are one.
            GeoEllipse2 ellipse = Tilted();

            AssertTouch(ellipse, ellipse.GetIntersections(Ellipse2Oracle.FrameLine(ellipse, -200, 100, 200, 100), Strict), Ellipse2Oracle.Rim(ellipse, Math.PI / 2));
        }

        [Fact]
        public void ASegmentFourTenThousandthsAboveTheTop_TouchesOnce_AtItsFootOnTheSegment()
        {
            // A gap of 0.0004 is inside the point tolerance of 0.001, as a circle's tangent band is (Intersection2.cs:
            // 257-262): one point, the foot on the segment above the end of the minor axis, (0, 100.0004) in the frame.
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, -200, 100.0004, 200, 100.0004);

            AssertTouch(ellipse, ellipse.GetIntersections(line, Strict), Ellipse2Oracle.FromFrame(ellipse, 0, 100.0004));
            Assert.InRange(Ellipse2Oracle.PointToSegment(ellipse.GetIntersections(line, Strict)[0], line), 0.0, 1E-9);
            Assert.True(ellipse.TryIntersectWith(line, out _, Strict));
        }

        [Fact]
        public void ASegmentTwoThousandthsAboveTheTop_DoesNotMeetTheRim()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, -200, 100.002, 200, 100.002);

            Assert.Empty(ellipse.GetIntersections(line, Strict));
            Assert.False(ellipse.TryIntersectWith(line, out GeoPoint2[] found, Strict));
            Assert.Empty(found);
        }

        [Fact]
        public void ASegmentAlongTheTangentAtTheTipOfAThinEllipse_IsReadInDrawingUnits()
        {
            // 1 000 by 1: the line x = 1 000.0004 misses the tip by 0.0004 and touches it, at the foot (1 000.0004, 0);
            // x = 1 000.002 misses by 0.002 and does not. Read in the unit frame (u = x / a) both gaps would be under a
            // millionth, and both would touch.
            GeoEllipse2 thin = Ellipse2Oracle.Upright(1000, 1);

            AssertTouch(thin, thin.GetIntersections(Ellipse2Oracle.FrameLine(thin, 1000.0004, -3, 1000.0004, 3), Strict), Ellipse2Oracle.FromFrame(thin, 1000.0004, 0));

            Assert.Empty(thin.GetIntersections(Ellipse2Oracle.FrameLine(thin, 1000.002, -3, 1000.002, 3), Strict));
        }

        [Fact]
        public void ASegmentStoppingHalfAThousandthShortOfTheRim_MeetsItAtItsOwnEnd()
        {
            // From the centre up to 99.9995, 0.0005 short of the top: the crossing lies past the end by less than the point
            // tolerance and is put on the end, as the circle does (Intersection2.cs:849-857). Two thousandths short, none.
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 shortOfIt = Ellipse2Oracle.FrameLine(ellipse, 0, 0, 0, 99.9995);

            GeoPoint2[] found = ellipse.GetIntersections(shortOfIt, Strict);
            Assert.Single(found);
            Assert.InRange(found[0].DistanceTo(shortOfIt.EndPoint), 0.0, 1E-9);

            Assert.Empty(ellipse.GetIntersections(Ellipse2Oracle.FrameLine(ellipse, 0, 0, 0, 99.998), Strict));
        }

        [Fact]
        public void ASegmentOfNoLength_MeetsTheRimOnlyWhereItLiesOnIt()
        {
            // As the circle's (Intersection2.cs:230-240): the one point, if it is on the rim.
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2 on = Ellipse2Oracle.Rim(ellipse, 1.1);
            GeoPoint2 off = Ellipse2Oracle.OffRim(ellipse, 1.1, 0.01);

            AssertPoints(new[] { on }, ellipse.GetIntersections(new GeoLine2(on, on), Strict), 1E-9);
            Assert.Empty(ellipse.GetIntersections(new GeoLine2(off, off), Strict));
        }

        [Fact]
        public void AThinEllipseCutAcrossHalfWayOut_IsCrossedOnBothSides()
        {
            // 1 000 by 1, cut by x = 500 drawn upwards: the rim is at y = -0.866 then 0.866 along it, t = 300° and 60°.
            GeoEllipse2 thin = Ellipse2Oracle.Upright(1000, 1);

            AssertPoints(
                RimAt(thin, 5 * Math.PI / 3, Math.PI / 3),
                thin.GetIntersections(Ellipse2Oracle.FrameLine(thin, 500, -5, 500, 5), Strict),
                1E-9);
        }

        [Fact]
        public void ASegmentCuttingTheTopShallowerThanTheTolerance_MeetsItOnce_AsACircleDoes()
        {
            // Along y = 100 √(1 - h² / 90 000), through the rim at x = ±h = ±0.0015: a cut 1.25E-9 deep. The circle's band
            // is read by depth (Intersection2.cs:257-262; A10), so a circle of radius 100 cut 1.25E-9 deep gives one point,
            // its foot, though the half chord, 0.0005, is not within the tolerance; the ellipse, its half chord 0.0015, gives
            // one too, the foot (0, y) on the segment.
            GeoEllipse2 ellipse = Upright();
            var circle = new GeoCircle2(new GeoPoint2(40, -25), 100);
            var circleCut = new GeoLine2(new GeoPoint2(-160, 75 - 1.25E-9), new GeoPoint2(240, 75 - 1.25E-9));
            Assert.Single(circle.GetIntersections(circleCut, Strict));

            AssertTouch(
                ellipse,
                ellipse.GetIntersections(Ellipse2Oracle.FrameLine(ellipse, -200, RimHeight(0.0015), 200, RimHeight(0.0015)), Strict),
                Ellipse2Oracle.FromFrame(ellipse, 0, RimHeight(0.0015)));
        }

        [Fact]
        public void ASegmentCuttingTheTopAHundredthDeep_CrossesItTwice_AlongIt()
        {
            // y = 99.99 crosses the rim at x = ±300 √(1 - 0.9999²) = ±4.2424, clear of the band.
            GeoEllipse2 ellipse = Upright();
            double x = 300 * Math.Sqrt(1 - 0.9999 * 0.9999);

            AssertPoints(
                Ellipse2Oracle.FramePoints(ellipse, -x, 99.99, x, 99.99),
                ellipse.GetIntersections(Ellipse2Oracle.FrameLine(ellipse, -200, 99.99, 200, 99.99), Strict),
                1E-9);
        }

        #endregion

        #region Polylines and closed shapes

        [Fact]
        public void APolylineCrossingTheRimFourTimes_GivesTheFourPoints()
        {
            // Along y = 50, down the outside, and back along y = -50: t = 30°, 150°, 210° and 330°.
            GeoEllipse2 ellipse = Tilted();
            GeoPolyline2 polyline = Ellipse2Oracle.FramePolyline(ellipse, 400, 50, -400, 50, -400, -50, 400, -50);

            AssertPoints(
                RimAt(ellipse, Math.PI / 6, 5 * Math.PI / 6, 7 * Math.PI / 6, 11 * Math.PI / 6),
                ellipse.GetIntersections(polyline, Strict),
                1E-9);
        }

        [Fact]
        public void AVertexOfAPolylineOnTheRim_IsReportedOnce()
        {
            // From the centre out to the rim at t = 60°, and on out: the two edges share the crossing, which comes back once
            // (Intersection2.cs:865-875).
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2 onRim = Ellipse2Oracle.Rim(ellipse, Math.PI / 3);
            var polyline = new GeoPolyline2(ellipse.Center, onRim, Ellipse2Oracle.FromFrame(ellipse, 400, 200));

            AssertPoints(new[] { onRim }, ellipse.GetIntersections(polyline, Strict), 1E-6);
        }

        [Fact]
        public void TheCrossingsOfAPolygon_RunEdgeByEdge_InItsOwnOrder()
        {
            // The band |y| ≤ 50 of the frame, drawn from the bottom right: its bottom edge meets the rim at t = 330° then
            // 210°, its top edge at 150° then 30°, the order the circle gives (Intersection2.cs:548-562; A8), not by t.
            GeoEllipse2 ellipse = Tilted();
            GeoPolygon2 band = Ellipse2Oracle.FramePolygon(ellipse, 400, -50, -400, -50, -400, 50, 400, 50);

            AssertPoints(RimAt(ellipse, 11 * Math.PI / 6, 7 * Math.PI / 6, 5 * Math.PI / 6, Math.PI / 6), ellipse.GetIntersections(band, Strict), 1E-9);
        }

        [Fact]
        public void ARectangleOverTheRim_IsCrossedWhereItsSidesMeetIt()
        {
            // An 80 by 60 rectangle, turned 0.3, about the rim point at t = 2: four sides, two of them across the rim.
            GeoEllipse2 ellipse = Tilted();
            var rectangle = new GeoRectangle2(Ellipse2Oracle.Rim(ellipse, 2.0), 80, 60, 0.3);
            GeoPoint2[] expected = Ellipse2Oracle.RimCrossings(ellipse, Ellipse2Oracle.SideOfConvex(rectangle.GetVertices()));

            Assert.Equal(2, expected.Length);
            AssertSamePoints(expected, ellipse.GetIntersections(rectangle, Strict), 1E-9);
        }

        [Fact]
        public void ATriangleOverTheRim_IsCrossedOnItsSides()
        {
            // Its corners all outside, each side a chord of the rim: six crossings. The order is not asked here.
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2[] corners = Ellipse2Oracle.FramePoints(ellipse, 20, 150, -330, -70, 350, -60);
            var triangle = new GeoTriangle2(corners[0], corners[1], corners[2]);
            GeoPoint2[] expected = Ellipse2Oracle.RimCrossings(ellipse, Ellipse2Oracle.SideOfConvex(corners));

            Assert.Equal(6, expected.Length);
            AssertSamePoints(expected, ellipse.GetIntersections(triangle, Strict), 1E-9);
        }

        [Fact]
        public void ATriangleOfNoWidth_IsCrossedAsTheSegmentItSpans()
        {
            // Its three corners on y = 50: read as the segment between the two furthest apart (Triangle2.cs:37-60).
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2[] corners = Ellipse2Oracle.FramePoints(ellipse, 400, 50, -400, 50, 0, 50);

            AssertPoints(
                RimAt(ellipse, Math.PI / 6, 5 * Math.PI / 6),
                ellipse.GetIntersections(new GeoTriangle2(corners[0], corners[1], corners[2]), Strict),
                1E-9);
        }

        [Fact]
        public void AFace_IsCrossedOnItsOutline_ThenOnTheRimOfItsHole()
        {
            // The outline's left side, drawn down from (-200, 300), meets the rim at y = 74.536 then -74.536 (t = 131.8°,
            // 228.2°); the hole, from x = 250 to 350 and y = -20 to 20, drawn from its bottom left, is met on its bottom then
            // its top at x = 293.939 (t = 348.5°, 11.5°). The outline's come first, then the hole's (Face2.cs:285-300; A8).
            GeoEllipse2 ellipse = Upright();
            GeoPolygon2 outline = Ellipse2Oracle.FramePolygon(ellipse, -200, -300, 600, -300, 600, 300, -200, 300);
            GeoPolygon2 hole = Ellipse2Oracle.FramePolygon(ellipse, 250, -20, 350, -20, 350, 20, 250, 20);
            var face = new GeoFace2(outline, new[] { hole });
            double side = Math.Acos(-2.0 / 3.0);
            double top = Math.Asin(0.2);

            AssertPoints(RimAt(ellipse, side, 2 * Math.PI - side, 2 * Math.PI - top, top), ellipse.GetIntersections(face, Strict), 1E-9);
        }

        [Fact]
        public void AnEllipseInAHoleOfAFace_OrRoundTheHole_CrossesNothing()
        {
            GeoEllipse2 ellipse = Upright();
            GeoPolygon2 outline = Ellipse2Oracle.FramePolygon(ellipse, -2000, -2000, 2000, -2000, 2000, 2000, -2000, 2000);
            GeoPolygon2 bigHole = Ellipse2Oracle.FramePolygon(ellipse, -400, -200, 400, -200, 400, 200, -400, 200);
            GeoPolygon2 smallHole = Ellipse2Oracle.FramePolygon(ellipse, -50, -20, 50, -20, 50, 20, -50, 20);

            Assert.Empty(ellipse.GetIntersections(new GeoFace2(outline, new[] { bigHole }), Strict));
            Assert.Empty(ellipse.GetIntersections(new GeoFace2(outline, new[] { smallHole }), Strict));
        }

        [Fact]
        public void AnEdge_IsCrossedAsTheSegmentOrTheArcItIs()
        {
            GeoEllipse2 ellipse = Upright();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, 400, 50, -400, 50);
            GeoArc2 arc = Ellipse2Oracle.FrameArc(ellipse, 0, 0, 200, 0.0, Math.PI);

            AssertPoints(ellipse.GetIntersections(line, Strict), ellipse.GetIntersections(new GeoEdge2(line.StartPoint, line.EndPoint), Strict), 1E-12);
            AssertPoints(ellipse.GetIntersections(arc, Strict), ellipse.GetIntersections(new GeoEdge2(arc), Strict), 1E-9);

            Assert.True(ellipse.TryIntersectWith(new GeoEdge2(arc), out GeoPoint2[] found, Strict));
            Assert.Equal(2, found.Length);
        }

        #endregion

        #region Circles and arcs

        [Fact]
        public void ACircleApart_IsNotCrossed()
        {
            GeoEllipse2 ellipse = Upright();
            GeoCircle2 circle = Ellipse2Oracle.FrameCircle(ellipse, 0, 300, 50);

            Assert.Empty(ellipse.GetIntersections(circle, Strict));
            Assert.False(ellipse.TryIntersectWith(circle, out GeoPoint2[] found, Strict));
            Assert.Empty(found);
        }

        [Fact]
        public void ACircleTouchingTheTopFromOutside_MeetsTheRimOnce()
        {
            // About (0, 150) with radius 50: it touches the end of the minor axis, a double root of the quartic, caught as
            // the least of the gap and given once.
            GeoEllipse2 ellipse = Upright();

            AssertTouch(ellipse, ellipse.GetIntersections(Ellipse2Oracle.FrameCircle(ellipse, 0, 150, 50), Strict), Ellipse2Oracle.FromFrame(ellipse, 0, 100));
        }

        [Fact]
        public void ACircleFourTenThousandthsAboveTheTop_TouchesOnce_AndTwoThousandthsAbove_DoesNot()
        {
            // The touch is the midpoint of the rim's top (0, 100) and the circle's bottom (0, 100.0004) (A10).
            GeoEllipse2 ellipse = Upright();

            AssertTouch(ellipse, ellipse.GetIntersections(Ellipse2Oracle.FrameCircle(ellipse, 0, 150.0004, 50), Strict), Ellipse2Oracle.FromFrame(ellipse, 0, 100.0002));

            Assert.Empty(ellipse.GetIntersections(Ellipse2Oracle.FrameCircle(ellipse, 0, 150.002, 50), Strict));
        }

        [Fact]
        public void ACircleOverTheTip_CrossesTheRimTwice()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoCircle2 circle = Ellipse2Oracle.FrameCircle(ellipse, 300, 0, 50);
            GeoPoint2[] expected = Ellipse2Oracle.RimCrossings(ellipse, Ellipse2Oracle.SideOfCircle(circle.Center, circle.Radius));

            Assert.Equal(2, expected.Length);
            AssertPoints(expected, ellipse.GetIntersections(circle, Strict), 1E-9);
        }

        [Fact]
        public void ACircleTangentUnderTheTopAndCrossingBelow_MeetsTheRimThreeTimes()
        {
            // About (0, -100) with radius 200: through the top (0, 100), where it curves harder than the rim (radius 200
            // against 900) and touches it from inside, and across the rim at y = -75, x = ±198.431 (t = 228.6° and 311.4°).
            // Put x² = 90 000 - 9y² into the circle and (y - 100)(y + 75) = 0, the first a double root.
            GeoEllipse2 ellipse = Upright();
            GeoPoint2[] found = ellipse.GetIntersections(Ellipse2Oracle.FrameCircle(ellipse, 0, -100, 200), Strict);

            Assert.Equal(3, found.Length);
            Assert.InRange(found[0].DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100)), 0.0, 1E-4);
            AssertPoints(RimAt(ellipse, 3.9896547325712742, 5.4351232281981055), found.Skip(1).ToArray(), 1E-9);
        }

        [Fact]
        public void AConcentricCircleBetweenTheRadii_CrossesTheRimFourTimes()
        {
            // Radius 200: a² cos² t + b² sin² t = 200² gives cos² t = 3/8, t0 = 0.911738.
            GeoEllipse2 ellipse = Tilted();
            const double t0 = 0.91173829096848764;
            GeoCircle2 circle = Ellipse2Oracle.FrameCircle(ellipse, 0, 0, 200);

            AssertPoints(RimAt(ellipse, t0, Math.PI - t0, Math.PI + t0, 2 * Math.PI - t0), ellipse.GetIntersections(circle, Strict), 1E-9);
            Assert.True(ellipse.TryIntersectWith(circle, out GeoPoint2[] found, Strict));
            Assert.Equal(4, found.Length);
        }

        [Fact]
        public void AConcentricCircleInside_OrRoundTheEllipse_IsNotCrossed()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.Empty(ellipse.GetIntersections(Ellipse2Oracle.FrameCircle(ellipse, 0, 0, 50), Strict));
            Assert.Empty(ellipse.GetIntersections(Ellipse2Oracle.FrameCircle(ellipse, 0, 0, 400), Strict));
        }

        [Fact]
        public void TheEllipseOfACircle_DoesNotCrossThatCircle()
        {
            // A circle against itself has no crossings (Intersection2.cs:310-316); its ellipse agrees.
            var circle = new GeoCircle2(new GeoPoint2(40, -25), 100);
            GeoEllipse2 ellipse = GeoEllipse2.FromCircle(circle);

            Assert.Empty(ellipse.GetIntersections(circle, Strict));
            Assert.False(ellipse.TryIntersectWith(circle, out _, Strict));
        }

        [Fact]
        public void ACircleCuttingTheTopWithAHalfChordOfEightTenThousandths_CrossesOnce_AndOneOfFifteen_Twice()
        {
            // The circle of radius 50 through the rim points (±h, y(h)), its centre straight above at y(h) + √(2 500 - h²):
            // it curves harder than the rim and dips 3.4E-9 or 1.2E-8 below the top. Merged within the tolerance (A9), the
            // two crossings sorted by t otherwise: the one at x = +h first.
            GeoEllipse2 ellipse = Upright();
            Func<double, GeoCircle2> through = h => Ellipse2Oracle.FrameCircle(ellipse, 0, RimHeight(h) + Math.Sqrt(2500 - h * h), 50);

            GeoPoint2[] once = ellipse.GetIntersections(through(0.0008), Strict);
            Assert.Single(once);
            Assert.InRange(once[0].DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100)), 0.0, 1E-3);

            AssertPoints(Ellipse2Oracle.FramePoints(ellipse, 0.0015, RimHeight(0.0015), -0.0015, RimHeight(0.0015)), ellipse.GetIntersections(through(0.0015), Strict), 1E-6);
        }

        [Fact]
        public void ACopyCuttingTheTopWithAHalfChordOfEightTenThousandths_CrossesOnce_AndOneOfFifteen_Twice()
        {
            // The same ellipse moved up to 2 y(h), its bottom through (±h, y(h)): it dips 7.1E-10 or 2.5E-9 below the top.
            GeoEllipse2 ellipse = Upright();
            Func<double, GeoEllipse2> through = h => Ellipse2Oracle.FrameEllipse(ellipse, 0, 2 * RimHeight(h), 0, 300, 100);

            GeoPoint2[] once = ellipse.GetIntersections(through(0.0008), Strict);
            Assert.Single(once);
            Assert.InRange(once[0].DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100)), 0.0, 1E-3);

            AssertPoints(Ellipse2Oracle.FramePoints(ellipse, 0.0015, RimHeight(0.0015), -0.0015, RimHeight(0.0015)), ellipse.GetIntersections(through(0.0015), Strict), 1E-6);
        }

        [Fact]
        public void AnArc_KeepsOnlyTheCrossingsItReaches()
        {
            // The upper half of the concentric circle of radius 200 holds two of its four crossings, t0 and 180° - t0.
            GeoEllipse2 ellipse = Tilted();
            const double t0 = 0.91173829096848764;
            GeoArc2 upper = Ellipse2Oracle.FrameArc(ellipse, 0, 0, 200, 0.0, Math.PI);

            AssertPoints(RimAt(ellipse, t0, Math.PI - t0), ellipse.GetIntersections(upper, Strict), 1E-9);
            Assert.True(ellipse.TryIntersectWith(upper, out GeoPoint2[] found, Strict));
            Assert.Equal(2, found.Length);
        }

        [Fact]
        public void ACrossingAHundredthPastTheEndOfAnArc_IsLeftOut_ButKeptWithinATenth()
        {
            // The crossing at t0 lies at 0.406378 round the circle of radius 200 seen from its centre. An arc stopping
            // 0.00005 short of that, 0.01 along the circle, misses it at a tolerance of 0.001 and holds it at 0.1, as the
            // arc's own reach is asked as a distance (Arc2.cs:576-583); one stopping 0.01 past it holds it either way.
            GeoEllipse2 ellipse = Tilted();
            const double polar = 0.40637778068433033;
            const double t0 = 0.91173829096848764;
            GeoArc2 shortOfIt = Ellipse2Oracle.FrameArc(ellipse, 0, 0, 200, -0.3, polar - 0.01 / 200);
            GeoArc2 pastIt = Ellipse2Oracle.FrameArc(ellipse, 0, 0, 200, -0.3, polar + 0.01 / 200);

            Assert.Empty(ellipse.GetIntersections(shortOfIt, Strict));
            AssertPoints(RimAt(ellipse, t0), ellipse.GetIntersections(shortOfIt, Tenth), 1E-9);
            AssertPoints(RimAt(ellipse, t0), ellipse.GetIntersections(pastIt, Strict), 1E-9);
        }

        [Fact]
        public void AnArcOfTheTouchingCircle_TouchesOnlyWhenItReachesTheTouch()
        {
            // The circle about (0, 150) of radius 50 touches the top at the bottom of the circle: its lower half reaches
            // the touch, its upper half does not.
            GeoEllipse2 ellipse = Upright();

            AssertTouch(ellipse, ellipse.GetIntersections(Ellipse2Oracle.FrameArc(ellipse, 0, 150, 50, Math.PI, 2 * Math.PI), Strict), Ellipse2Oracle.FromFrame(ellipse, 0, 100));

            Assert.Empty(ellipse.GetIntersections(Ellipse2Oracle.FrameArc(ellipse, 0, 150, 50, 0.0, Math.PI), Strict));
        }

        #endregion

        #region Curved chains

        // Up the minor axis from (0, -300) to (0, 300), then round the right half of the circle of radius 300 about the
        // centre back down: the straight edge crosses the rim at (0, -100) then (0, 100), and the arc, as wide as the major
        // axis, touches the rim from outside at its tip (300, 0), where the rim curves far harder (radius 33.3 against
        // 300). Edge by edge, as the circle's chain (ArcChain2.cs:451-468; A8).
        private static GeoPoint2[] ChainMeetings(GeoEllipse2 ellipse) => new[]
        {
            Ellipse2Oracle.FromFrame(ellipse, 0, -100), Ellipse2Oracle.FromFrame(ellipse, 0, 100), Ellipse2Oracle.FromFrame(ellipse, 300, 0),
        };

        [Fact]
        public void AChainOfASegmentAndAnArc_IsMetOnBoth()
        {
            GeoEllipse2 ellipse = Upright();
            GeoPoint2[] ends = Ellipse2Oracle.FramePoints(ellipse, 0, -300, 0, 300);
            var chain = new GeoPolylineArc2(new[] { new GeoEdge2(ends[0], ends[1]), new GeoEdge2(ends[1], ends[0], -1.0) });

            AssertPoints(ChainMeetings(ellipse), ellipse.GetIntersections(chain, Strict), 1E-4);
            Assert.True(ellipse.TryIntersectWith(chain, out GeoPoint2[] found, Strict));
            Assert.Equal(3, found.Length);
        }

        [Fact]
        public void ALoopOfASegmentAndAnArc_IsMetOnBoth()
        {
            // The same D, closed, its arc in two quarters meeting at the tip: the touch, a corner both quarters share, is
            // given once. A quarter turn clockwise has the bulge -tan 22.5°.
            GeoEllipse2 ellipse = Upright();
            double quarter = -Math.Tan(Math.PI / 8);
            var loop = new GeoPolygonArc2(Ellipse2Oracle.FramePoints(ellipse, 0, -300, 0, 300, 300, 0), new[] { 0.0, quarter, quarter });

            AssertPoints(ChainMeetings(ellipse), ellipse.GetIntersections(loop, Strict), 1E-4);
            Assert.True(ellipse.TryIntersectWith(loop, out GeoPoint2[] found, Strict));
            Assert.Equal(3, found.Length);
        }

        #endregion

        #region Ellipses

        [Fact]
        public void AnEllipseApart_IsNotCrossed()
        {
            GeoEllipse2 ellipse = Upright();
            GeoEllipse2 other = Ellipse2Oracle.FrameEllipse(ellipse, 1000, 0, 0.3, 200, 50);

            Assert.Empty(ellipse.GetIntersections(other, Strict));
            Assert.False(ellipse.TryIntersectWith(other, out GeoPoint2[] found, Strict));
            Assert.Empty(found);
        }

        [Fact]
        public void ACopyStackedOnTop_TouchesTheRimOnce()
        {
            // The same ellipse moved up 200: top to bottom at (0, 100), a double root. Beside it the gap opens only as
            // x² / 900, so a thousandth off for |x| < 0.95: the touch is one point, at the least of the gap.
            GeoEllipse2 ellipse = Upright();

            AssertTouch(ellipse, ellipse.GetIntersections(Ellipse2Oracle.FrameEllipse(ellipse, 0, 200, 0, 300, 100), Strict), Ellipse2Oracle.FromFrame(ellipse, 0, 100));
        }

        [Fact]
        public void ACopyFourTenThousandthsAboveTouching_TouchesOnce_AndTwoThousandthsAbove_DoesNot()
        {
            // The touch is the midpoint of this top (0, 100) and the copy's bottom (0, 100.0004) (A10).
            GeoEllipse2 ellipse = Upright();

            AssertTouch(ellipse, ellipse.GetIntersections(Ellipse2Oracle.FrameEllipse(ellipse, 0, 200.0004, 0, 300, 100), Strict), Ellipse2Oracle.FromFrame(ellipse, 0, 100.0002));

            Assert.Empty(ellipse.GetIntersections(Ellipse2Oracle.FrameEllipse(ellipse, 0, 200.002, 0, 300, 100), Strict));
        }

        [Fact]
        public void ACopyMovedAlongTheMajorAxis_CrossesTwice()
        {
            // Moved 100: they cross at x = 50, cos t = 1/6, t = 1.403348 and 2π - 1.403348.
            GeoEllipse2 ellipse = Tilted();
            const double t = 1.4033482475752073;

            AssertPoints(RimAt(ellipse, t, 2 * Math.PI - t), ellipse.GetIntersections(Ellipse2Oracle.FrameEllipse(ellipse, 100, 0, 0, 300, 100), Strict), 1E-9);
        }

        [Fact]
        public void AWiderEllipseTangentUnderTheTop_MeetsTheRimThreeTimes()
        {
            // 320 by 150 about (0, -50): its top is the top of this one, where it curves harder (radius 683 against 900)
            // and touches from inside; its sides cross the rim at y = 2.297702, x = ±299.920798, near the tips. With
            // x² = 90 000 (1 - y² / 10 000) the other's equation is a quadratic in y with the roots 2.297702 and 100.
            GeoEllipse2 ellipse = Upright();
            GeoPoint2[] found = ellipse.GetIntersections(Ellipse2Oracle.FrameEllipse(ellipse, 0, -50, 0, 320, 150), Strict);

            Assert.Equal(3, found.Length);
            Assert.InRange(found[0].DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 299.92079800734094, 2.2977022977022977)), 0.0, 1E-9);
            Assert.InRange(found[1].DistanceTo(Ellipse2Oracle.FromFrame(ellipse, 0, 100)), 0.0, 1E-4);
            Assert.InRange(found[2].DistanceTo(Ellipse2Oracle.FromFrame(ellipse, -299.92079800734094, 2.2977022977022977)), 0.0, 1E-9);
        }

        [Fact]
        public void TheEllipseTurnedAQuarterAboutItsCentre_CrossesFourTimes()
        {
            // 300 by 100 against 100 by 300: x² = y² = 9 000, the points (±94.868, ±94.868), t = atan 3 = 1.249046 round.
            GeoEllipse2 ellipse = Tilted();
            const double t = 1.2490457723982544;
            GeoEllipse2 turned = Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, Math.PI / 2, 300, 100);

            GeoPoint2[] found = ellipse.GetIntersections(turned, Strict);

            AssertPoints(RimAt(ellipse, t, Math.PI - t, Math.PI + t, 2 * Math.PI - t), found, 1E-9);
            Assert.True(ellipse.TryIntersectWith(turned, out GeoPoint2[] tried, Strict));
            AssertPoints(found, tried, 0.0);
        }

        [Fact]
        public void AConcentricSmallerEllipse_AndTheSameEllipse_AreNotCrossed()
        {
            // One inside the other has no crossings; the same ellipse has none either, as a circle has none with itself
            // (Intersection2.cs:310-316).
            GeoEllipse2 ellipse = Tilted();

            Assert.Empty(ellipse.GetIntersections(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.2, 200, 50), Strict));
            Assert.Empty(ellipse.GetIntersections(ellipse.Clone(), Strict));
            Assert.False(ellipse.TryIntersectWith(ellipse.Clone(), out _, Strict));
        }

        [Fact]
        public void TwoThinEllipsesCrossingAtRightAngles_CrossFourTimes()
        {
            // 1 000 by 1 and the same turned a quarter: x² = y² = 1 / (1 + 1E-6), the points (±0.9999995, ±0.9999995), at
            // t = atan 1000 = 1.569796 round the first. They are 1.4 apart: four points, not one.
            GeoEllipse2 thin = Ellipse2Oracle.Upright(1000, 1);
            const double t = 1.5697963271282298;

            AssertPoints(
                RimAt(thin, t, Math.PI - t, Math.PI + t, 2 * Math.PI - t),
                thin.GetIntersections(Ellipse2Oracle.FrameEllipse(thin, 0, 0, Math.PI / 2, 1000, 1), Strict),
                1E-9);
        }

        [Fact]
        public void TwoThinEllipsesAHundredthOfARadianApart_CrossFourTimes()
        {
            // 1 000 by 1 and the same turned 0.01 about the centre: they cross 196 out along the bisector, at a shallow
            // angle, and twice near the centre, across the thin way.
            GeoEllipse2 thin = Ellipse2Oracle.Upright(1000, 1);
            GeoEllipse2 turned = Ellipse2Oracle.FrameEllipse(thin, 0, 0, 0.01, 1000, 1);
            GeoPoint2[] expected = Ellipse2Oracle.RimCrossings(thin, Ellipse2Oracle.SideOfEllipse(turned));

            Assert.Equal(4, expected.Length);
            AssertPoints(expected, thin.GetIntersections(turned, Strict), 1E-6);
        }

        [Fact]
        public void TwoCopiesAHundredthApartAcrossTheMajorAxis_CrossOnlyNearTheTwoTips()
        {
            // Moved 0.01 along the minor axis: the rims are a hundredth apart along most of their length and cross where
            // y = 0.005, at x = ±299.9999996, at an angle of 3E-4 between them. Two points, 600 apart, whatever the
            // quartic makes of its near-double roots there.
            GeoEllipse2 ellipse = Upright();
            const double x = 299.99999962500000;

            AssertPoints(
                new[] { Ellipse2Oracle.FromFrame(ellipse, x, 0.005), Ellipse2Oracle.FromFrame(ellipse, -x, 0.005) },
                ellipse.GetIntersections(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0.01, 0, 300, 100), Strict),
                1E-6);
        }

        [Fact]
        public void ACopyMovedFourTenThousandths_IsTheSameEllipse_AndCrossesNothing()
        {
            // Within the point tolerance of 0.001 it is the same ellipse (IsEqualTo), and a circle has no crossings with
            // the same circle within the tolerance (Intersection2.cs:310-316).
            GeoEllipse2 ellipse = Tilted();
            GeoEllipse2 moved = Ellipse2Oracle.FrameEllipse(ellipse, 0, 0.0004, 0, 300, 100);

            Assert.True(ellipse.IsEqualTo(moved, Strict));
            Assert.Empty(ellipse.GetIntersections(moved, Strict));
        }

        [Fact]
        public void TheCrossingsOfTwoEllipses_AreTheSamePointsAskedFromEither()
        {
            // Each answer runs by the t of the ellipse asked, so the order differs; the points do not.
            GeoEllipse2 ellipse = Tilted();
            GeoEllipse2 other = Ellipse2Oracle.FrameEllipse(ellipse, 60, 40, 1.0, 250, 70);
            GeoPoint2[] forward = ellipse.GetIntersections(other, Strict);

            Assert.Equal(4, forward.Length);
            AssertSamePoints(forward, other.GetIntersections(ellipse, Strict), 1E-9);
            AssertRunsByT(other, other.GetIntersections(ellipse, Strict));
        }

        #endregion

        #region The same call twice, and the input

        [Fact]
        public void AskedTwice_TheCrossingsAreTheSame_AndThePolygonIsLeftAsItWas()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoPolygon2 band = Ellipse2Oracle.FramePolygon(ellipse, 400, -50, -400, -50, -400, 50, 400, 50);
            GeoPoint2[] corners = band.Vertices.ToArray();
            GeoEllipse2 turned = Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, Math.PI / 2, 300, 100);
            GeoCircle2 circle = Ellipse2Oracle.FrameCircle(ellipse, 0, -100, 200);

            Assert.Equal(ellipse.GetIntersections(band, Strict), ellipse.GetIntersections(band, Strict));
            Assert.Equal(ellipse.GetIntersections(turned, Strict), ellipse.GetIntersections(turned, Strict));
            Assert.Equal(ellipse.GetIntersections(circle, Strict), ellipse.GetIntersections(circle, Strict));
            Assert.Equal(corners, band.Vertices.ToArray());
        }

        #endregion
    }
}
