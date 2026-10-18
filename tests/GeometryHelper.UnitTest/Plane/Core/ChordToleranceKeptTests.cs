using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Flattening an arc keeps its promise that no piece strays further than the chord tolerance, however fine the tolerance
    /// and however large the radius.
    /// </summary>
    /// <remarks>
    /// The cut used to stop at 4 096 pieces a turn, so an arc sweeping less than 2 pi / 4 096 came back as its chord whatever
    /// tolerance was asked: the bottom of a rectangle 100 by 50 bowed 0.015 flattened at 1E-4 to the bare chord and lost its
    /// segment of 1.00000002. Where the cap did not bind the counts are those of before; where it did, the count is the one
    /// the tolerance needs, and a tolerance that needs more than 1 048 576 pieces for one arc is refused.
    /// </remarks>
    public class ChordToleranceKeptTests
    {
        private static readonly Tolerance Thousandth = new Tolerance(1E-3, 1E-5);

        /// <summary>The rectangle 100 by 50 from the origin whose bottom bows 0.015 down: bulge 0.0003, radius 83 333.34.</summary>
        private static GeoPolygonArc2 RectangleWithANearlyFlatBottom()
            => Loop(new[] { P(0, 0), P(100, 0), P(100, 50), P(0, 50) }, 0.0003);

        /// <summary>How far inside the circle the middle of the worst chord of a run of points on it lies.</summary>
        private static double WorstSag(IReadOnlyList<GeoPoint2> points, GeoPoint2 center, double radius)
        {
            double worst = 0.0;

            for (int i = 1; i < points.Count; i++)
            {
                double mx = 0.5 * (points[i - 1].X + points[i].X) - center.X;
                double my = 0.5 * (points[i - 1].Y + points[i].Y) - center.Y;
                worst = Math.Max(worst, radius - Math.Sqrt(mx * mx + my * my));
            }

            return worst;
        }

        /// <summary>How far inside the circle the middle of the worst chord of a run of points on it lies.</summary>
        private static double WorstSag(IReadOnlyList<GeoPoint3> points, GeoPoint3 center, double radius)
        {
            double worst = 0.0;

            for (int i = 1; i < points.Count; i++)
            {
                double mx = 0.5 * (points[i - 1].X + points[i].X) - center.X;
                double my = 0.5 * (points[i - 1].Y + points[i].Y) - center.Y;
                double mz = 0.5 * (points[i - 1].Z + points[i].Z) - center.Z;
                worst = Math.Max(worst, radius - Math.Sqrt(mx * mx + my * my + mz * mz));
            }

            return worst;
        }

        [Fact]
        public void ANearlyFlatArc_IsFlattenedWithinTheChordTolerance()
        {
            // The bottom of the rectangle sweeps 0.0012 radians on a radius of 83 333.34, sagging 0.015 at (50, -0.015).
            // Cut within 1E-4 it needs 13 pieces, r (1 - cos(a / 2)) <= 1E-4; the 4 096-a-turn cap allows one, the chord,
            // which passes 0.015 from the arc's middle and leaves out its segment of 1.00000002.
            GeoPolygonArc2 loop = RectangleWithANearlyFlatBottom();
            GeoPolygon2 flat = loop.Flatten(1E-4);

            Assert.True(DistanceTo(PiecesOf(flat), 50.0, -0.015) <= 1E-4);

            // The chords lie inside the arc, by at most 1E-4 along its 100.000006.
            Assert.InRange(flat.Area, 5001.00000002 - 0.01, 5001.00000002);
        }

        [Fact]
        public void ANearlyFlatArcInAChain_IsFlattenedIntoThirteenPieces()
        {
            // The same bottom as a chain of its own: 13 pieces, 14 points, none of them further than 1E-4 from the arc.
            GeoPolylineArc2 chain = Chain(new[] { P(0, 0), P(100, 0) }, 0.0003);
            GeoPolyline2 flat = chain.Flatten(1E-4);

            Assert.Equal(14, flat.VertexCount);
            Assert.True(DistanceTo(PiecesOf(flat), 50.0, -0.015) <= 1E-4);
            Assert.True(flat[0].IsEqualTo(P(0, 0), Thousandth));
            Assert.True(flat[13].IsEqualTo(P(100, 0), Thousandth));
        }

        [Fact]
        public void AQuarterArcOfRadiusFiftyThousandCutWithinOneTenThousandth_HasNoChordFurtherThanThat()
        {
            // A quarter turn on 50 000 needs ceil((pi / 2) / (4 asin(sqrt(1E-4 / 100 000)))) = 12 419 pieces within 1E-4;
            // the cap allowed 1 024, which strays about 0.0147.
            var arc = new GeoArc2(P(0, 0), 50000.0, 0.0, Math.PI / 2.0);
            GeoPolyline2 flat = arc.ToPolylineByChordTolerance(1E-4);

            Assert.InRange(flat.VertexCount - 1, 12418, 12420);
            Assert.True(WorstSag(flat.Vertices, P(0, 0), 50000.0) <= 1E-4 + 1E-9);
        }

        [Fact]
        public void AQuarterArcInSpaceOfRadiusFiftyThousandCutWithinOneTenThousandth_HasNoChordFurtherThanThat()
        {
            var arc = new GeoArc3(P(1, 2, 3), new GeoVector3(0, 0, 1), 50000.0, 0.0, Math.PI / 2.0);
            GeoPolyline3 flat = arc.ToPolylineByChordTolerance(1E-4);

            Assert.InRange(flat.VertexCount - 1, 12418, 12420);
            Assert.True(WorstSag(flat.Vertices, P(1, 2, 3), 50000.0) <= 1E-4 + 1E-9);
        }

        [Fact]
        public void ABendOfRadiusFiftyThousandInAChainInSpace_IsCutWithinTheChordTolerance()
        {
            // A straight leg of 10, then the quarter arc of radius 50 000 about (10, 50 000, 0) leaving it along X and turning
            // towards +Y: ToPolyline3 cuts each bend by its own radius, so the bend is the 12 419 pieces of the arc alone.
            var center = P(10, 50000, 0);
            var bend = new GeoEdge3(P(10, 0, 0), P(50010, 50000, 0), Math.Tan(Math.PI / 8.0), new GeoVector3(0, 0, 1));
            var chain = new GeoPolylineArc3(new[] { new GeoEdge3(P(0, 0, 0), P(10, 0, 0)), bend });

            GeoPolyline3 flat = chain.ToPolyline3(1E-4);

            Assert.InRange(flat.VertexCount - 2, 12418, 12420);
            Assert.True(flat[0].IsEqualTo(P(0, 0, 0), Thousandth));
            Assert.True(flat[1].IsEqualTo(P(10, 0, 0), Thousandth));
            Assert.True(WorstSag(new List<GeoPoint3>(flat.Vertices).GetRange(1, flat.VertexCount - 1), center, 50000.0) <= 1E-4 + 1E-9);
        }

        [Fact]
        public void ALoopInSpaceWithANearlyFlatSide_IsCutWithinTheChordTolerance()
        {
            // The rectangle 100 by 50 in the plane Z = 7, its bottom bowed by the bulge 0.0003: 13 pieces within 1E-4.
            var loop = new GeoPolygonArc3(
                new[] { P(0, 0, 7), P(100, 0, 7), P(100, 50, 7), P(0, 50, 7) },
                new[] { 0.0003, 0.0, 0.0, 0.0 },
                new[] { new GeoVector3(0, 0, 1), new GeoVector3(0, 0, 1), new GeoVector3(0, 0, 1), new GeoVector3(0, 0, 1) });

            GeoPolygon3 flat = loop.ToPolygon3(1E-4);

            Assert.Equal(16, flat.VertexCount);
        }

        [Fact]
        public void AnArcCutWhereTheCapDidNotBind_KeepsTheCountItHad()
        {
            // A quarter turn of radius 100 within 0.05 is 20 pieces, a long way under the cap: the chain, the loop and the arc
            // cut it the same way as before, into the count Tessellation gives.
            int pieces = Tessellation.SegmentsForChordTolerance(100.0, Math.PI / 2.0, 0.05);
            GeoPolylineArc2 chain = Chain(new[] { P(100, 0), P(0, 100) }, Math.Tan(Math.PI / 8.0));

            Assert.Equal(pieces + 1, chain.Flatten(0.05).VertexCount);
            Assert.Equal(pieces + 1, new GeoArc2(P(0, 0), 100.0, 0.0, Math.PI / 2.0).ToPolylineByChordTolerance(0.05).VertexCount);
            Assert.Equal(pieces + 1, new GeoArc3(P(0, 0, 0), new GeoVector3(0, 0, 1), 100.0, 0.0, Math.PI / 2.0).ToPolylineByChordTolerance(0.05).VertexCount);
        }

        [Fact]
        public void AnArcNeedingJustUnderTheLimit_IsCutIntoThatManyPieces()
        {
            // A half turn of radius 50 000 within 6.2E-8 needs about a million pieces, each 0.157 long.
            var arc = new GeoArc2(P(0, 0), 50000.0, 0.0, Math.PI);
            GeoPolyline2 flat = arc.ToPolylineByChordTolerance(6.2E-8);

            Assert.InRange(flat.VertexCount - 1, 990000, 1048576);
        }

        [Fact]
        public void AnArcNeedingMoreThanTheLimit_IsRefused()
        {
            // A half turn of radius 50 000 within 1E-10 would need about 25 million pieces.
            var arc = new GeoArc2(P(0, 0), 50000.0, 0.0, Math.PI);
            var arc3 = new GeoArc3(P(0, 0, 0), new GeoVector3(0, 0, 1), 50000.0, 0.0, Math.PI);

            AssertRefused(() => arc.ToPolylineByChordTolerance(1E-10));
            AssertRefused(() => arc3.ToPolylineByChordTolerance(1E-10));
            AssertRefused(() => Chain(new[] { P(-50000, 0), P(50000, 0) }, 1.0).Flatten(1E-10));
            AssertRefused(() => Loop(new[] { P(-50000, 0), P(50000, 0), P(0, -60000) }, 1.0, 0.0, 0.0).Flatten(1E-10));
            AssertRefused(() => new GeoPolylineArc3(new[] { new GeoEdge3(arc3) }).ToPolyline3(1E-10));
        }

        [Fact]
        public void ACircleOfRadiusFiftyThousandCutWithinAHundredth_HasNoChordFurtherThanThat()
        {
            // 4 968 chords of about 63 keep it within 0.01; the cap of 4 096 a turn strayed 0.0147.
            var circle = new GeoCircle2(P(3, 4), 50000.0);
            GeoPolygon2 polygon = circle.ToPolygonByChordTolerance(0.01);
            GeoPolyline2 chain = circle.ToPolylineByChordTolerance(0.01);

            Assert.InRange(polygon.VertexCount, 4960, 4975);
            Assert.True(WorstSag(polygon.Vertices, P(3, 4), 50000.0) <= 0.01 + 1E-9);
            Assert.Equal(polygon.VertexCount + 1, chain.VertexCount);
        }

        [Fact]
        public void ACircleInSpaceOfRadiusFiftyThousandCutWithinAHundredth_HasNoChordFurtherThanThat()
        {
            var circle = new GeoCircle3(P(3, 4, 5), new GeoVector3(0, 0, 1), 50000.0);
            GeoPolygon3 polygon = circle.ToPolygonByChordTolerance(0.01);
            GeoPolyline3 chain = circle.ToPolylineByChordTolerance(0.01);

            Assert.InRange(polygon.VertexCount, 4960, 4975);
            Assert.True(WorstSag(polygon.Vertices, P(3, 4, 5), 50000.0) <= 0.01 + 1E-9);
            Assert.Equal(polygon.VertexCount + 1, chain.VertexCount);
        }

        [Fact]
        public void AnEllipseOfRadii50000And25000CutWithinAThousandth_HasNoChordFurtherThanThat()
        {
            // The walk by the bend of the rim takes 12 920 corners, fewer than the 15 708 that an even spread in the angle would
            // (2 pi / root (8 t / a)): a multiple of four with a corner at each end of both axes. The cap of 4 096 corners
            // strayed 0.0147.
            var ellipse = new GeoEllipse2(P(0, 0), GeometryHelper.Geometry.GeoVector2.XAxis, 50000.0, 25000.0);
            GeoPolygon2 polygon = ellipse.ToPolygonByChordTolerance(0.001);

            Assert.InRange(polygon.VertexCount, 12900, 12940);
            Assert.Equal(0, polygon.VertexCount % 4);
            // The corners are placed to the rounding of coordinates of this size, about 1E-11, which the measured sag shows as 1E-8 of the tolerance.
            Assert.InRange(Ellipse2Oracle.LargestSag(ellipse, polygon), 0.0, 0.001 * (1 + 1E-6));
        }

        [Fact]
        public void ACylinderSideOfRadiusFiftyThousandCutWithinAHundredth_HasNoChordFurtherThanThat()
        {
            // The bar of a pipe of radius 50 000: 4 968 sides, where the cap gave 4 096 straying 0.0147.
            var path = new GeoPolyline3(P(0, 0, 0), P(0, 0, 10));
            GeoSolid3 bar = GeoSolid3.Pipe(path, 50000.0, 0.01);
            int sides = Tessellation.SegmentsForArc(50000.0, 2 * Math.PI, 0.01);

            Assert.InRange(sides, 4960, 4975);
            Assert.True(bar.IsClosed());

            double expected = 0.5 * sides * 50000.0 * 50000.0 * Math.Sin(2.0 * Math.PI / sides) * 10.0;
            Assert.InRange(bar.GetVolume(), expected * (1 - 1E-9), expected * (1 + 1E-9));
        }

        [Fact]
        public void ACircleOrEllipseNeedingMoreThanTheLimit_IsRefused()
        {
            // A circle of 50 000 within 1E-10 would take about 25 million corners, an ellipse of 50 000 by 25 000 the same.
            var circle = new GeoCircle2(P(0, 0), 50000.0);
            var circle3 = new GeoCircle3(P(0, 0, 0), new GeoVector3(0, 0, 1), 50000.0);
            var ellipse = new GeoEllipse2(P(0, 0), GeometryHelper.Geometry.GeoVector2.XAxis, 50000.0, 25000.0);

            AssertRefused(() => circle.ToPolygonByChordTolerance(1E-10));
            AssertRefused(() => circle.ToPolylineByChordTolerance(1E-10));
            AssertRefused(() => circle3.ToPolygonByChordTolerance(1E-10));
            AssertRefused(() => circle3.ToPolylineByChordTolerance(1E-10));
            AssertRefused(() => ellipse.ToPolygonByChordTolerance(1E-10));
            AssertRefused(() => ellipse.ToPolylineByChordTolerance(1E-10));
            AssertRefused(() => GeoSolid3.Pipe(new GeoPolyline3(P(0, 0, 0), P(0, 0, 10)), 50000.0, 1E-10));
        }

        private static void AssertRefused(Action flatten)
        {
            var thrown = Assert.Throws<ArgumentOutOfRangeException>(flatten);

            Assert.Equal("chordTolerance", thrown.ParamName);
            Assert.Contains("1048576", thrown.Message);
        }
    }
}
