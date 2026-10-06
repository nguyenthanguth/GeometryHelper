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
    /// How far an ellipse is from other shapes. An ellipse is a region, as a circle is, so a shape touching it, crossing
    /// its rim or lying inside it is nought away (Distance2.cs:94-155 for the circle against a point, a segment, a circle,
    /// a rectangle and a polygon; Distance2.cs:446-458 and 776-800 for chains; Arc2.cs:171-174 for an arc), and a closed
    /// shape round it is nought away too. Otherwise the distance is the gap between the rim and the nearest part of the
    /// other shape. The expected gaps come from closed-form distances to segments and arcs minimised round a dense sample
    /// of the rim (<see cref="Ellipse2Oracle.RimTo"/>), from the construction of
    /// the shape where it is placed a known distance off, and for ellipse against ellipse from a table measured at 40
    /// digits. None of the cases sits within the point tolerance of touching: a shape is either clearly apart or clearly
    /// meeting, so no answer here turns on how a tolerance is read.
    /// </summary>
    public class Ellipse2DistanceTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoEllipse2 Upright() => Ellipse2Oracle.Upright(300, 100);

        private static GeoEllipse2 Tilted() => Ellipse2Oracle.Tilted();

        // The gap from the rim to a set of segments and arcs, by the oracle.
        private static double RimToSegments(GeoEllipse2 ellipse, params GeoLine2[] lines) =>
            Ellipse2Oracle.RimTo(ellipse, p => lines.Min(line => Ellipse2Oracle.PointToSegment(p, line)));

        private static double RimToEdges(GeoEllipse2 ellipse, IEnumerable<GeoEdge2> edges)
        {
            GeoEdge2[] all = edges.ToArray();
            return Ellipse2Oracle.RimTo(ellipse, p => all.Min(edge => edge.IsArc
                ? Ellipse2Oracle.PointToArc(p, edge.ToArc())
                : Ellipse2Oracle.PointToSegment(p, edge.ToLine())));
        }

        #region Points

        [Fact]
        public void APointOutside_IsAsFarAsItsGapToTheRim()
        {
            // 7.5 out along the normal at t = 1.1 of the tilted 300 by 100.
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2 point = Ellipse2Oracle.OffRim(ellipse, 1.1, 7.5);

            Assert.Equal(7.5, ellipse.DistanceTo(point, Strict), 9);
            Assert.Equal(7.5, ellipse.SignedDistanceTo(point, Strict), 9);
        }

        [Fact]
        public void APointInside_IsNoughtAway_AndItsSignedDistanceIsItsGapNegated()
        {
            // 7.5 in along the normal at t = 1.1, where the rim's radius of curvature is 664, so its gap is 7.5; the centre's
            // gap is the minor radius, 100.
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2 point = Ellipse2Oracle.OffRim(ellipse, 1.1, -7.5);

            Assert.Equal(0.0, ellipse.DistanceTo(point, Strict));
            Assert.Equal(-7.5, ellipse.SignedDistanceTo(point, Strict), 9);
            Assert.Equal(0.0, ellipse.DistanceTo(ellipse.Center, Strict));
            Assert.Equal(-100.0, ellipse.SignedDistanceTo(ellipse.Center, Strict), 9);
        }

        [Fact]
        public void APointHalfAThousandthOutside_IsThatFarAway_ButOnTheRimWhenSigned()
        {
            // The circle's DistanceTo has no band (Distance2.cs:94-97): half a thousandth out is half a thousandth away.
            // SignedDistanceTo reads Locate, and a point Locate puts on the rim is nought (Distance2.Signed.cs:50-55,
            // 177-190).
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2 point = Ellipse2Oracle.OffRim(ellipse, 1.1, 0.0005);

            Assert.Equal(0.0005, ellipse.DistanceTo(point, Strict), 12);
            Assert.Equal(0.0, ellipse.SignedDistanceTo(point, Strict));
        }

        #endregion

        #region Segments and polylines

        [Fact]
        public void ASegmentAboveTheTop_IsNearestWhereTheRimRunsParallelToIt()
        {
            // y = 175 from x = -200 to 200: its ends are 102 from the rim, its middle 75 from the top.
            GeoEllipse2 ellipse = Upright();

            Assert.Equal(75.0, ellipse.DistanceTo(Ellipse2Oracle.FrameLine(ellipse, -200, 175, 200, 175), Strict), 9);
        }

        [Fact]
        public void AnObliqueSegmentOutside_IsNearestWhereTheRimRunsParallelToIt()
        {
            // From (100, 250) to (450, -50) in the frame: the rim runs parallel to it at tan t = 7/18, t = 0.3705, and the
            // foot of that point falls inside the segment, nearer than either end.
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, 100, 250, 450, -50);
            double expected = RimToSegments(ellipse, line);

            Assert.InRange(expected, 40.0, 50.0);
            Assert.Equal(expected, ellipse.DistanceTo(line, Strict), 9);
        }

        [Fact]
        public void ASegmentRunningAwayFromTheTip_IsNearestAtItsEnd()
        {
            // From (350, 0) on the major axis outward: its start is 50 from the tip.
            GeoEllipse2 ellipse = Tilted();

            Assert.Equal(50.0, ellipse.DistanceTo(Ellipse2Oracle.FrameLine(ellipse, 350, 0, 600, 100), Strict), 9);
        }

        [Fact]
        public void ASegmentInside_OrAcrossTheRim_IsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameLine(ellipse, -100, 0, 100, 20), Strict));
            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameLine(ellipse, 0, 0, 500, 300), Strict));
        }

        [Fact]
        public void AThinEllipse_IsMeasuredToItsFlankAndToItsTip()
        {
            // 1 000 by 1: half a unit above the flank, and half a unit beyond the tip, whose radius of curvature is 0.001.
            // The steep segment from (1 000.6, -2) to (999.8, 2) passes 0.2 beyond the tip; the rim runs parallel to it at
            // tan t = 0.0002, on the tip, and the foot of that point falls inside the segment: 0.196 off.
            GeoEllipse2 thin = Ellipse2Oracle.Upright(1000, 1);
            GeoLine2 steep = Ellipse2Oracle.FrameLine(thin, 1000.6, -2, 999.8, 2);
            double expected = RimToSegments(thin, steep);

            Assert.Equal(0.5, thin.DistanceTo(Ellipse2Oracle.FrameLine(thin, -200, 1.5, 200, 1.5), Strict), 9);
            Assert.Equal(0.5, thin.DistanceTo(Ellipse2Oracle.FrameLine(thin, 1000.5, -3, 1000.5, 3), Strict), 9);
            Assert.InRange(expected, 0.19, 0.2);
            Assert.Equal(expected, thin.DistanceTo(steep, Strict), 9);
        }

        [Fact]
        public void APolyline_IsAsFarAsItsNearestEdge_AndOneInsideIsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoPolyline2 apart = Ellipse2Oracle.FramePolyline(ellipse, -400, 300, -150, 150, 150, 150, 500, 120);
            GeoPolyline2 inside = Ellipse2Oracle.FramePolyline(ellipse, -100, 0, 0, 50, 100, 0);

            Assert.Equal(RimToSegments(ellipse, apart.GetEdges().ToArray()), ellipse.DistanceTo(apart, Strict), 9);
            Assert.Equal(0.0, ellipse.DistanceTo(inside, Strict));
        }

        #endregion

        #region Closed straight shapes

        [Fact]
        public void APolygonApart_IsAsFarAsItsNearestEdge()
        {
            // Its side x = 330 faces the tip (300, 0), where the rim's tangent is upright: 30 off.
            GeoEllipse2 ellipse = Tilted();
            GeoPolygon2 polygon = Ellipse2Oracle.FramePolygon(ellipse, 330, -40, 420, 40, 330, 120);

            Assert.Equal(30.0, RimToSegments(ellipse, polygon.GetEdges().ToArray()), 9);
            Assert.Equal(30.0, ellipse.DistanceTo(polygon, Strict), 9);
        }

        [Fact]
        public void APolygonRoundTheEllipse_AndOneInsideIt_AreNoughtAway()
        {
            // Both regions: the ellipse inside the polygon, or the polygon inside the ellipse, overlap (Distance2.cs:146-155).
            GeoEllipse2 ellipse = Tilted();

            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FramePolygon(ellipse, -400, -200, 400, -200, 400, 200, -400, 200), Strict));
            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FramePolygon(ellipse, -50, -20, 50, -20, 0, 40), Strict));
        }

        [Fact]
        public void ARectangleApart_IsAsFarAsItsNearestSide_AndOneRoundTheEllipseIsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();
            var apart = new GeoRectangle2(Ellipse2Oracle.FromFrame(ellipse, 0, 200), 120, 60, Math.PI / 6);
            var round = new GeoRectangle2(ellipse.Center, 800, 800, 0.0);

            Assert.Equal(RimToSegments(ellipse, apart.GetEdges()), ellipse.DistanceTo(apart, Strict), 9);
            Assert.Equal(0.0, ellipse.DistanceTo(round, Strict));
        }

        [Fact]
        public void ATriangleApart_IsAsFarAsItsNearestSide_AndOneRoundTheEllipseIsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoPoint2[] apart = Ellipse2Oracle.FramePoints(ellipse, -380, 40, -330, -90, -450, -20);
            GeoPoint2[] round = Ellipse2Oracle.FramePoints(ellipse, -1000, -400, 1000, -400, 0, 900);
            var triangle = new GeoTriangle2(apart[0], apart[1], apart[2]);

            Assert.Equal(RimToSegments(ellipse, triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2)), ellipse.DistanceTo(triangle, Strict), 9);
            Assert.Equal(0.0, ellipse.DistanceTo(new GeoTriangle2(round[0], round[1], round[2]), Strict));
        }

        [Fact]
        public void AnEdge_IsMeasuredAsTheSegmentOrTheArcItIs()
        {
            GeoEllipse2 ellipse = Tilted();
            GeoLine2 line = Ellipse2Oracle.FrameLine(ellipse, 100, 250, 450, -50);
            GeoArc2 arc = Ellipse2Oracle.FrameArc(ellipse, 0, 250, 50, Math.PI, 2 * Math.PI);

            Assert.Equal(ellipse.DistanceTo(line, Strict), ellipse.DistanceTo(new GeoEdge2(line.StartPoint, line.EndPoint), Strict), 12);
            Assert.Equal(100.0, ellipse.DistanceTo(new GeoEdge2(arc), Strict), 9);
        }

        #endregion

        #region Circles and arcs

        [Fact]
        public void ACircleApart_IsItsCentresGapLessItsRadius()
        {
            // About (0, 250) with radius 50: its centre is 150 from the top, so 100. About the point 80 out along the normal
            // at t = 2.5, with radius 30: 50.
            GeoEllipse2 ellipse = Tilted();

            Assert.Equal(100.0, ellipse.DistanceTo(Ellipse2Oracle.FrameCircle(ellipse, 0, 250, 50), Strict), 9);
            Assert.Equal(50.0, ellipse.DistanceTo(new GeoCircle2(Ellipse2Oracle.OffRim(ellipse, 2.5, 80), 30), Strict), 9);
        }

        [Fact]
        public void ACircleInside_RoundTheEllipse_OrAcrossTheRim_IsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameCircle(ellipse, 50, 0, 30), Strict));
            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameCircle(ellipse, 0, 0, 500), Strict));
            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameCircle(ellipse, 0, 130, 50), Strict));
        }

        [Fact]
        public void AnArcFacingTheRim_IsAsFarAsItsNearestPoint_AndOneFacingAwayAsFarAsItsEnds()
        {
            // The lower half of the circle about (0, 250) of radius 50 reaches down to (0, 200), 100 from the top; the upper
            // half is nearest at its ends (±50, 250).
            GeoEllipse2 ellipse = Tilted();
            GeoArc2 facing = Ellipse2Oracle.FrameArc(ellipse, 0, 250, 50, Math.PI, 2 * Math.PI);
            GeoArc2 away = Ellipse2Oracle.FrameArc(ellipse, 0, 250, 50, 0.0, Math.PI);

            Assert.Equal(100.0, ellipse.DistanceTo(facing, Strict), 9);
            Assert.Equal(Ellipse2Oracle.RimTo(ellipse, p => Ellipse2Oracle.PointToArc(p, away)), ellipse.DistanceTo(away, Strict), 9);
        }

        [Fact]
        public void AnArcInside_OrAcrossTheRim_IsNoughtAway()
        {
            // The disc is a region, as a circle's is to an arc (Arc2.cs:171-174).
            GeoEllipse2 ellipse = Tilted();

            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameArc(ellipse, 0, 0, 60, 0.3, 2.0), Strict));
            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameArc(ellipse, 0, 0, 200, 0.3, 2.0), Strict));
        }

        #endregion

        #region Curved chains

        [Fact]
        public void ACurvedChainApart_IsAsFarAsItsNearestEdge()
        {
            // A straight edge, then a half turn bulging away, then a quarter turn back: all above the top.
            GeoEllipse2 ellipse = Tilted();
            var chain = new GeoPolylineArc2(Ellipse2Oracle.FramePoints(ellipse, -300, 160, -60, 140, 60, 140, 200, 220), new[] { 0.0, -1.0, 0.4, 0.0 });

            Assert.Equal(RimToEdges(ellipse, chain.GetEdges()), ellipse.DistanceTo(chain, Strict), 9);
        }

        [Fact]
        public void ACurvedLoopApart_IsAsFarAsItsNearestEdge_AndOneRoundTheEllipseIsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();
            var apart = new GeoPolygonArc2(Ellipse2Oracle.FramePoints(ellipse, 350, -40, 450, -40, 450, 40, 350, 40), new[] { 0.0, 0.5, 0.0, 0.3 });
            var round = new GeoPolygonArc2(Ellipse2Oracle.FramePoints(ellipse, -400, -200, 400, -200, 400, 200, -400, 200), new[] { 0.2, 0.0, 0.2, 0.0 });

            Assert.Equal(RimToEdges(ellipse, apart.GetEdges()), ellipse.DistanceTo(apart, Strict), 9);
            Assert.Equal(0.0, ellipse.DistanceTo(round, Strict));
        }

        [Fact]
        public void ACurvedChainInside_IsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();
            var inside = new GeoPolylineArc2(Ellipse2Oracle.FramePoints(ellipse, -100, 0, 0, 30, 100, 0), new[] { 0.3, -0.3, 0.0 });

            Assert.Equal(0.0, ellipse.DistanceTo(inside, Strict));
        }

        #endregion

        #region Ellipses

        [Fact]
        public void ACopyAbove_IsMeasuredTopToBottom()
        {
            // Moved up 250: its bottom (0, 150) is 50 above this top (0, 100), and the gap only widens to the sides.
            GeoEllipse2 ellipse = Tilted();

            Assert.Equal(50.0, ellipse.DistanceTo(Ellipse2Oracle.FrameEllipse(ellipse, 0, 250, 0, 300, 100), Strict), 9);
        }

        [Fact]
        public void AnEllipseInside_RoundTheEllipse_AcrossTheRim_OrTouchingIt_IsNoughtAway()
        {
            GeoEllipse2 ellipse = Tilted();

            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.2, 200, 50), Strict));
            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameEllipse(ellipse, 0, 0, 0.0, 400, 300), Strict));
            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameEllipse(ellipse, 100, 0, 0.0, 300, 100), Strict));
            Assert.Equal(0.0, ellipse.DistanceTo(Ellipse2Oracle.FrameEllipse(ellipse, 0, 200, 0.0, 300, 100), Strict));
        }

        // Fifty pairs apart: a and b of each, the turn of its major axis from X, the centre, then the distance between them
        // measured at 40 digits (numpy over 4 096 by 4 096 samples of the two rims, the best pairs polished by mpmath's
        // findroot on the common normal). The sizes run over major radii 10 to 1 000 and ratios a / b 1 to 1 000, by
        // golden-ratio steps. A third are far apart (centres further than the two major radii), a third have the tip of
        // the second a set gap above the flank of the first, a third were walked in until the gap was a tenth to ten.
        private static readonly double[,] Pairs =
        {
            { 172.214, 9.8498, 3.626753, -118.034, 328.427, 11.59, 1.1373, 5.197549, -107.9461, 309.2917, 0.191784142174582 }, // tip to flank, exact
            { 510.745, 95.5613, 4.597074, -354.102, -14.719, 98.244, 0.3696, 0.499146, -395.9041, -918.101, 362.52764221318999 }, // far
            { 87.957, 0.9413, 1.940641, 27.864, -186.292, 28.603, 21.0667, 3.511437, -0.9443, -197.4605, 1.3531681995017585 }, // tip to flank, exact
            { 15.147, 9.2709, 5.567394, 409.83, -357.864, 832.737, 120.0848, 2.926305, 217.6369, -449.8832, 2.5908290713685871 }, // near
            { 260.86, 9.1319, 2.910962, -208.204, 470.563, 242.443, 6.8452, 0.998292, -934.8748, 537.9576, 399.73294378797556 }, // far
            { 44.924, 0.0899, 0.25453, 173.762, 298.99, 70.585, 0.3902, 1.825326, 153.5627, 376.628, 9.5477324953911812 }, // tip to flank, exact
            { 133.233, 0.8727, 1.22485, -62.306, -44.156, 598.294, 126.7883, 1.497438, 80.8242, 981.0113, 307.80079864618765 }, // far
            { 22.945, 8.5957, 4.851603, 319.66, -215.729, 174.187, 7.2273, 6.422399, 501.3414, -190.2718, 0.67356219292459151 }, // tip to flank, exact
            { 395.137, 8.4664, 2.195171, -298.374, -387.302, 50.713, 0.412, 3.924597, -240.1965, -365.798, 1.2457706565981946 }, // near
            { 68.048, 0.0834, 5.821924, 83.592, 441.125, 14.765, 0.0235, 1.996584, 195.9247, 420.1078, 50.623948204227778 }, // far
            { 11.719, 0.8214, 3.165492, 465.558, 269.553, 429.854, 133.8659, 4.736288, 475.9633, -165.751, 4.7529439144543048 }, // tip to flank, exact
            { 201.814, 0.8091, 0.509059, -152.476, 97.98, 125.148, 7.6308, 4.423743, -122.3641, 3.2637, 9.0539996053923913 }, // near
            { 34.755, 7.9691, 4.135812, 229.49, -73.593, 36.435, 0.435, 2.49573, 207.5492, -166.7958, 52.849421523083783 }, // far
            { 598.532, 7.8494, 1.47938, -388.544, -245.166, 10.608, 0.0248, 3.050176, -407.2582, -243.4504, 0.33527317330907049 }, // tip to flank, exact
            { 103.076, 77.3154, 5.106133, -6.578, -416.739, 308.836, 141.3387, 4.922889, -219.3506, -473.6995, 0.62853125478930418 }, // near
            { 17.751, 0.7615, 2.449701, 375.388, 411.688, 89.914, 8.0567, 2.994876, 239.7403, 450.3183, 35.912893004960116 }, // far
            { 305.697, 0.7501, 6.076454, -242.646, 240.115, 26.178, 0.4593, 7.64725, -236.633, 268.7853, 2.3659654585197982 }, // tip to flank, exact
            { 52.645, 7.3883, 3.420021, 139.32, 68.542, 762.136, 2.6179, 5.422035, 114.6751, 181.2181, 4.2115066679481594 }, // near
            { 906.625, 7.2773, 0.763589, -478.714, -103.03, 221.888, 149.2283, 3.494021, -21.0762, 1261.1066, 613.24705218048525 }, // far
            { 156.133, 71.6803, 4.390342, -96.748, -274.603, 64.6, 8.5064, 5.961138, 32.6844, -317.7898, 0.16692739579586784 }, // tip to flank, exact
            { 26.888, 0.706, 1.73391, 285.218, -446.176, 18.808, 0.4849, 5.921181, 302.1935, -443.2779, 0.28484662378877465 }, // near
            { 463.054, 0.6954, 5.360663, -332.816, 382.251, 547.569, 2.764, 3.993167, 835.5875, -70.8214, 723.07202796697306 }, // far
            { 79.744, 6.8498, 2.70423, 49.15, 210.678, 159.419, 157.5585, 4.275026, -21.7724, 58.9929, 1.1777777009522208 }, // tip to flank, exact
            { 13.733, 0.0675, 0.047798, 431.116, 39.105, 46.413, 8.9813, 0.137141, 432.5947, 27.1289, 2.2588483889567713 }, // near
            { 236.502, 66.4563, 3.674551, -186.918, -132.468, 13.513, 0.512, 4.492313, -308.7058, -408.0223, 134.03244854926001 }, // far
            { 40.729, 0.6546, 1.018119, 195.048, -304.041, 393.41, 2.9183, 2.588915, -147.4217, -92.8072, 8.3098694690736677 }, // tip to flank, exact
            { 120.792, 6.3506, 1.988439, -41.02, 352.814, 33.346, 9.4826, 4.991459, -202.4229, 433.2637, 105.85145966401298 }, // far
            { 20.802, 0.0626, 5.615192, 340.946, 181.241, 970.845, 54.054, 7.185988, 942.7011, 943.9278, 0.58627814346126868 }, // tip to flank, exact
            { 358.241, 61.6131, 2.95876, -277.088, 9.668, 282.652, 3.0812, 1.135433, -287.3661, 347.9609, 0.54659413457032989 }, // near
            { 61.694, 0.6069, 0.302328, 104.878, -161.905, 82.291, 0.1756, 5.490605, 184.4632, -19.1701, 101.9620506863839 }, // far
            { 10.625, 5.9779, 3.929081, 486.844, -333.478, 23.958, 10.0119, 5.499877, 510.9873, -357.5206, 4.1367510510990753 }, // tip to flank, exact
            { 182.97, 5.8878, 1.272648, -131.19, 494.949, 697.52, 57.0715, 1.634579, -2.8803, 492.9129, 7.7695802538732404 }, // near
            { 31.51, 0.058, 4.899401, 250.776, 323.376, 203.076, 3.2532, 5.989751, 470.1047, 187.4276, 50.004121202102568 }, // far
            { 542.644, 57.1228, 2.242969, -367.258, 151.804, 59.123, 0.1854, 3.813765, -458.4453, 79.2375, 0.29183599601313529 }, // tip to flank, exact
            { 93.451, 0.5626, 5.869722, 14.708, -19.769, 17.213, 10.5708, 2.133725, 13.7729, -34.8065, 0.55976609997266297 }, // near
            { 16.094, 5.5421, 3.21329, 396.674, -191.342, 501.145, 60.2574, 0.205712, 85.1267, -645.6376, 335.68927982093549 }, // far
            { 277.153, 5.4587, 0.556857, -221.36, -362.915, 145.903, 3.4348, 2.127653, -302.4461, -232.6728, 2.0592447045948783 }, // tip to flank, exact
            { 47.73, 0.0538, 4.18361, 160.606, 465.512, 42.478, 0.1958, 2.632871, 110.004, 471.0161, 3.9493256654526613 }, // near
            { 821.968, 52.9598, 1.527178, -457.428, 293.939, 12.367, 11.1609, 0.704858, -1142.9257, 812.2064, 652.36791999453502 }, // far
            { 141.554, 0.5216, 5.153931, -75.462, 122.366, 360.056, 63.6211, 6.724727, 250.6654, 276.5151, 0.14532145297573806 }, // tip to flank, exact
            { 24.378, 5.1382, 2.497499, 306.504, -49.207, 104.826, 3.6266, 3.132017, 309.438, -30.3791, 0.26862306770373548 }, // near
            { 419.816, 5.0609, 6.124252, -311.529, -220.779, 30.519, 0.2067, 1.204004, 118.8861, 296.9174, 546.34042835002165 }, // far
            { 72.298, 49.8488, 3.467819, 70.437, -392.352, 888.535, 1.1784, 5.038615, 371.4897, -1282.2151, 1.0250912283589546 }, // tip to flank, exact
            { 12.451, 0.491, 0.811387, 452.403, 436.075, 258.688, 67.1725, 3.631163, 557.6785, 414.644, 1.9135076474368141 }, // near
            { 214.419, 0.4836, 4.43814, -165.631, 264.502, 75.314, 3.829, 1.70315, 146.7947, -20.6783, 347.59170534323686 }, // far
            { 36.926, 4.7636, 1.781708, 216.335, 92.929, 21.927, 0.2183, 3.352504, 183.1635, 85.8271, 7.2326279693375399 }, // tip to flank, exact
            { 109.513, 46.2158, 2.752028, -19.733, -250.217, 185.858, 70.922, 2.202295, -317.5593, -547.6382, 285.23919291475536 }, // far
            { 18.86, 0.4552, 0.095596, 362.233, -421.79, 54.111, 4.0428, 1.666392, 356.9759, -366.965, 0.51027161365558826 }, // tip to flank, exact
            { 324.79, 0.4484, 3.722349, -255.801, 406.638, 15.754, 0.2305, 4.629455, -272.4259, 411.659, 0.4610592837273332 }, // near
            { 55.933, 4.4164, 1.065917, 126.165, 235.065, 458.656, 1.3136, 2.701441, -355.3341, 764.0095, 299.43139103390689 }, // far
        };

        [Fact]
        public void FiftyPairsApart_AreMeasuredToOnePartInAThousandMillion()
        {
            for (int i = 0; i < Pairs.GetLength(0); i++)
            {
                GeoEllipse2 first = Pair(i, 0);
                GeoEllipse2 second = Pair(i, 5);
                double expected = Pairs[i, 10];

                Assert.InRange(Math.Abs(first.DistanceTo(second, Strict) - expected) / expected, 0.0, 1E-9);
            }
        }

        private static GeoEllipse2 Pair(int row, int from) => Ellipse(Pairs, row, from);

        private static GeoEllipse2 Ellipse(double[,] table, int row, int from) => new GeoEllipse2(
            new GeoPoint2(table[row, from + 3], table[row, from + 4]),
            new GeoVector2(Math.Cos(table[row, from + 2]), Math.Sin(table[row, from + 2])),
            table[row, from],
            table[row, from + 1]);

        // Six pairs from the random pairs of the gate (20 000, seed 20261006, the tester's harness), each got wrong by a
        // weaker search than the spec's: the first four by 8 even starts in t instead of 32, the fifth by Brent run to a
        // relative 1E-15 in t, where its last probes either side change the gap by less than its rounding and a tie stops
        // it short of the minimum, the sixth by Brent's answer left unpolished, asked from the second ellipse. All are a
        // thin ellipse beside another, nearly parallel or tip to flank, 0.017 to 49 apart. Laid out as Pairs; the
        // distances measured at 40 digits.
        private static readonly double[,] HardPairs =
        {
            { 2.8215718986189766, 0.0941853793187044, 4.450886282494872, 364.5090990478732, -200.50125273732772, 3.746342201454932, 0.33703122691773935, 0.3090617614243276, 361.5951358653745, -198.99938320041935, 0.017376579621510285 }, // 8 starts: 3.1 out
            { 4.774784738950862, 0.14858673592710794, 2.9842125619055517, 904.4545483661184, -598.9797073266709, 20.17896902882272, 0.13474736064543746, 1.4794344200628156, 898.0732374441947, -618.6608525349162, 0.25673551239600294 }, // 8 starts: 0.35 out
            { 75.01122769481064, 1.4157875013869208, 4.991849564339547, 370.51962938292854, 944.0887414317353, 78.55031296477384, 4.350598280997618, 0.5585619451460029, 461.1514945335682, 923.226463815949, 5.1868257514272326 }, // 8 starts: 0.25 out
            { 418.6438794710619, 7.914492671675847, 0.8827984257184408, 58.72873515237143, -771.4295672132921, 158.01488608938558, 12.85271173816618, 2.4754009795529095, -26.68693247109971, -1205.2256107787944, 48.693162351766889 }, // 8 starts: 0.11 out
            { 12.192910198527516, 0.12891554127905994, 0.6722663575041332, -240.70874647785877, 942.3973909450931, 1.8164414255514054, 0.005071054056443066, 0.6833538339552431, -242.1895914922851, 945.8121622038072, 3.4445578035064036 }, // Brent at 1E-15: 3.2E-4 out
            { 4.98478728091435, 0.9580417079430381, 1.8426789828785324, 668.8860291466547, 697.2006110317118, 456.9067115368165, 8.947891024997796, 6.1893722810142915, 670.2650086407747, 711.1743415576148, 0.4258793566547067 }, // Brent unpolished: 1.6E-9 out, asked from the second
        };

        [Fact]
        public void PairsAWeakerSearchGetsWrong_AreMeasuredToOnePartInAThousandMillion_FromEitherSide()
        {
            for (int i = 0; i < HardPairs.GetLength(0); i++)
            {
                GeoEllipse2 first = Ellipse(HardPairs, i, 0);
                GeoEllipse2 second = Ellipse(HardPairs, i, 5);
                double expected = HardPairs[i, 10];

                Assert.InRange(Math.Abs(first.DistanceTo(second, Strict) - expected) / expected, 0.0, 1E-9);
                Assert.InRange(Math.Abs(second.DistanceTo(first, Strict) - expected) / expected, 0.0, 1E-9);
            }
        }

        [Fact]
        public void TheDistanceBetweenTwoEllipses_IsTheSameAskedFromEither()
        {
            for (int i = 0; i < Pairs.GetLength(0); i += 7)
            {
                GeoEllipse2 first = Pair(i, 0);
                GeoEllipse2 second = Pair(i, 5);

                Assert.InRange(Math.Abs(first.DistanceTo(second, Strict) - second.DistanceTo(first, Strict)) / Pairs[i, 10], 0.0, 1E-9);
            }
        }

        #endregion
    }
}
