using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Common;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The shortest line in space wherever the distance was already answered: the point against eleven shapes,
    /// and the ellipse against a point; the segment against a plane, a polygon and a chain; the plane against
    /// a chain and a body; the polygon against a chain and a body; and the chain against a chain, a body and a
    /// triangle. Each line leaves the shape asked and lands on the other, both ends on their shapes, and it is
    /// as long as the distance, but inside a body, where it runs out to the nearest face.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cases are a table. Each row names its numbers: the distance worked by hand, and, where the spec
    /// fixes it, the point the line starts and ends at. The fixtures sit within 60 of the origin, so the
    /// length is held to the distance within 1E-12 of a size of 100, and an end to its shape within 1E-9.
    /// </para>
    /// <para>
    /// What fixes a point: a flat region is read as its DistanceTo reads it, so a point over a polygon goes
    /// straight down; a shape that crosses or touches a plane gets a zero-length line at its first contact in
    /// its own order; one lying parallel to a plane is measured from its first point; a tie goes to the first
    /// edge. Where a whole run of pairs is as near as any, as for a segment lying parallel over a surface, the
    /// row names no point and only the length and the ends are held.
    /// </para>
    /// <para>
    /// The polygon and chain rows are also held to a dense-sampling oracle: points 0.1 apart along every
    /// curve and 0.2 apart over every region, the plane and the box measured in closed form. No sampled pair
    /// may be nearer than the line, and the line may be no shorter than the nearest pair less the reach of
    /// the samples.
    /// </para>
    /// </remarks>
    public class ShortestLinesInSpaceTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>1E-12 of a size of 100: every fixture lies within 60 of the origin.</summary>
        private const double Bound = 1E-12 * 100.0;

        /// <summary>How far an end may sit off its shape.</summary>
        private const double OnBound = 1E-9;

        private const double CurveSpacing = 0.1;

        private const double RegionSpacing = 0.2;

        private static readonly GeoPoint3 O = GeoPoint3.Origin;

        private static readonly GeoVector3 Up = GeoVector3.ZAxis;

        /// <summary>Built on first use, once every kit below is in place.</summary>
        private static readonly Lazy<List<Row>> Table = new Lazy<List<Row>>(Build);

        public static TheoryData<string> Rows() => Names(Table.Value);

        public static TheoryData<string> RowsWithAnOtherSide() => Names(Table.Value.Where(r => r.Back != null && !(r.OneMember && r.Tie)));

        public static TheoryData<string> SampledRows() => Names(Table.Value.Where(r => r.Sampled != null));

        [Theory]
        [MemberData(nameof(Rows))]
        public void TheLineLeavesTheShapeAskedLandsOnTheOtherAndIsAsLongAsTheDistance(string name)
        {
            Row row = Find(name);

            // The fixture first, through the distance the library already gives: the number worked by hand,
            // or nought for a shape inside a body, which DistanceTo reads as solid through.
            double distance = row.Distance(Within);
            Assert.Equal(row.Inside || row.Band ? 0.0 : row.Expected, distance, Bound);

            GeoLine3 line = row.Line(Within);

            Assert.InRange(row.OffAsked(line.StartPoint), 0.0, row.On);
            Assert.InRange(row.OffArgument(line.EndPoint), 0.0, row.On);

            // As long as the distance, or, inside a body, as the depth out to the nearest face; within the point tolerance
            // of touching, where the distance reads nought, as long as the true gap, since a line is never snapped.
            Assert.Equal(row.Inside || row.Band ? row.Expected : distance, line.Length, Bound);

            if (row.Start.HasValue)
            {
                Near(row.Start.Value, line.StartPoint);
            }

            if (row.End.HasValue)
            {
                Near(row.End.Value, line.EndPoint);
            }

            // The form without a tolerance is the one under the global tolerance.
            MirrorTable.Same(row.Line(Tolerance.Global), row.PlainLine());
        }

        [Theory]
        [MemberData(nameof(RowsWithAnOtherSide))]
        public void TheOtherSideGivesTheSameLineTurnedRound(string name)
        {
            Row row = Find(name);
            GeoLine3 line = row.Line(Within);
            GeoLine3 back = row.Back(Within);

            if (row.OneMember)
            {
                // Two chains: the same member asked the other way, with one nearest pair to find.
                Near(line.EndPoint, back.StartPoint);
                Near(line.StartPoint, back.EndPoint);
                return;
            }

            MirrorTable.Same(line.Reverse(), back);
            MirrorTable.Same(row.PlainLine().Reverse(), row.PlainBack());
        }

        [Theory]
        [MemberData(nameof(SampledRows))]
        public void NoPairOfDenseSamplesIsNearerThanTheLine(string name)
        {
            Row row = Find(name);
            (double nearest, double reach) = row.Sampled();

            // The oracle against the distance already given, for every row but a body's inside.
            if (!row.Inside)
            {
                Assert.InRange(row.Distance(Within), nearest - reach, nearest + Bound);
            }

            Assert.InRange(row.Line(Within).Length, nearest - reach, nearest + Bound);
        }

        #region The table

        private static List<Row> Build()
        {
            var rows = new List<Row>();

            // A point against each shape. The point's line leaves the point; the shape's leaves the shape.
            AddPair(rows, Point, Arc, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("on the arc", P(0, 10, 0), Bow(), P(0, 10, 0)),
                // Radius 10 about the origin, so 25 out over the middle is 15 off the rim.
                Gap("beyond the rim over the middle", P(0, 25, 0), Bow(), 15, P(0, 25, 0), P(0, 10, 0)),
                // 6 in from the rim and 3 up: the square root of 36 + 9.
                Gap("inside the circle and above its plane", P(0, 4, 3), Bow(), Math.Sqrt(45), P(0, 4, 3), P(0, 10, 0)),
                // The nearest point of the whole circle is off the half kept, so it is the end at (10, 0, 0): 15 and 5 away.
                Gap("past an end", P(25, -5, 0), Bow(), Math.Sqrt(250), P(25, -5, 0), P(10, 0, 0)));

            AddPair(rows, Point, Circle, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b),
                Contact("on the disc", P(3, 4, 0), Disc(), P(3, 4, 0)),
                // The disc is filled: straight down 12 onto (3, 4, 0), 5 from the centre of a disc of radius 10.
                Gap("above the disc", P(3, 4, 12), Disc(), 12, P(3, 4, 12), P(3, 4, 0)),
                // 6 out from the rim and 8 up: 10.
                Gap("beyond the rim", P(16, 0, 8), Disc(), 10, P(16, 0, 8), P(10, 0, 0)));

            AddPair(rows, Point, Edge, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("on the straight edge", P(13, 0, 0), StraightEdge(), P(13, 0, 0)),
                Gap("beside the straight edge", P(20, 6, 8), StraightEdge(), 10, P(20, 6, 8), P(20, 0, 0)),
                Gap("past the end of the straight edge", P(46, 8, 0), StraightEdge(), 10, P(46, 8, 0), P(40, 0, 0)),
                Contact("on the bent edge", P(0, 10, 0), BowEdge(), P(0, 10, 0)),
                Gap("beyond the bent edge", P(0, 25, 0), BowEdge(), 15, P(0, 25, 0), P(0, 10, 0)));

            AddPair(rows, Point, Face, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("on the material", P(5, 5, 0), Plate(), P(5, 5, 0)),
                Gap("above the material", P(5, 5, 9), Plate(), 9, P(5, 5, 9), P(5, 5, 0)),
                // Over the hole from (15, 15) to (25, 25): 3 across to its rim at y = 25 and 4 down, so 5.
                Gap("above the hole", P(20, 22, 4), Plate(), 5, P(20, 22, 4), P(20, 25, 0)),
                Gap("beyond the outline", P(46, 20, 8), Plate(), 10, P(46, 20, 8), P(40, 20, 0)));

            AddPair(rows, Point, Line, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b),
                Contact("on the segment", P(13, 0, 0), Rod(), P(13, 0, 0)),
                Gap("beside the segment", P(20, 6, 8), Rod(), 10, P(20, 6, 8), P(20, 0, 0)),
                Gap("before the start", P(-6, 0, 8), Rod(), 10, P(-6, 0, 8), P(0, 0, 0)));

            AddPair(rows, Point, Plane, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b),
                Contact("on the plane", P(3, 4, 0), Floor(), P(3, 4, 0)),
                Gap("above", P(3, 4, 12), Floor(), 12, P(3, 4, 12), P(3, 4, 0)),
                Gap("below", P(3, 4, -5), Floor(), 5, P(3, 4, -5), P(3, 4, 0)));

            AddPair(rows, Point, Polygon, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b),
                Contact("on the surface", P(5, 5, 0), Ell(), P(5, 5, 0)),
                Gap("above the surface", P(5, 5, 7), Ell(), 7, P(5, 5, 7), P(5, 5, 0)),
                // Over the notch of the L: 5 across to the edge at y = 10 and 12 up, so 13.
                Gap("above the notch", P(25, 15, 12), Ell(), 13, P(25, 15, 12), P(25, 10, 0)),
                Gap("beyond the outline", P(46, 5, 8), Ell(), 10, P(46, 5, 8), P(40, 5, 0)),
                // In the notch, 15 from the edge at y = 10 (the third) and 15 from the edge at x = 10 (the fourth).
                Gap("as near two edges, so on the first", P(25, 25, 0), Ell(), 15, P(25, 25, 0), P(25, 10, 0)));

            AddPair(rows, Point, Polyline, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b),
                Contact("on the chain", P(30, 20, 0), Zig(), P(30, 20, 0)),
                Gap("beside the first edge", P(10, 6, 8), Zig(), 10, P(10, 6, 8), P(10, 0, 0)),
                // 10 from the second edge at x = 20 and 10 from the third at y = 20.
                Gap("as near two edges, so on the first", P(30, 10, 0), Zig(), 10, P(30, 10, 0), P(20, 10, 0)),
                Gap("past the last vertex", P(46, 28, 0), Zig(), 10, P(46, 28, 0), P(40, 20, 0)));

            AddPair(rows, Point, Chain, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("on the bend", P(0, -10, 0), Hook(), P(0, -10, 0)),
                Gap("beside the straight run", P(-20, 6, 8), Hook(), 10, P(-20, 6, 8), P(-20, 0, 0)),
                Gap("below the bend", P(0, -25, 0), Hook(), 15, P(0, -25, 0), P(0, -10, 0)));

            AddPair(rows, Point, Ray, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b),
                Contact("on the ray", P(13, 0, 0), Beam(), P(13, 0, 0)),
                Gap("beside the ray", P(20, 6, 8), Beam(), 10, P(20, 6, 8), P(20, 0, 0)),
                Gap("behind the origin", P(-6, 0, 8), Beam(), 10, P(-6, 0, 8), P(0, 0, 0)));

            AddPair(rows, Point, Triangle, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b),
                Contact("on the surface", P(5, 5, 0), Tri(), P(5, 5, 0)),
                Gap("above the surface", P(5, 5, 9), Tri(), 9, P(5, 5, 9), P(5, 5, 0)),
                // The long edge runs x + y = 40, so (30, 30) is 20 / sqrt(2) * 2 = sqrt(200) from (20, 20).
                Gap("beyond the long edge", P(30, 30, 0), Tri(), Math.Sqrt(200), P(30, 30, 0), P(20, 20, 0)),
                Gap("beyond a corner", P(-6, -8, 0), Tri(), 10, P(-6, -8, 0), P(0, 0, 0)));

            // The ellipse is asked; nothing takes an ellipse, so there is no other side.
            AddPair(rows, Ellipse, Point, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), null, null, (a, b, t) => a.DistanceTo(b),
                Contact("on the disc", Oval(), P(5, 2, 0), P(5, 2, 0)),
                Gap("above the disc", Oval(), P(5, 2, 7), 7, P(5, 2, 0), P(5, 2, 7)),
                // Major radius 20 along x: 10 beyond its end.
                Gap("beyond the end of the major axis", Oval(), P(30, 0, 0), 10, P(20, 0, 0), P(30, 0, 0)));

            // The segment against the plane z = 0, a polygon and a chain.
            AddPair(rows, Line, Plane, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b),
                // From z = -4 to 6: through the plane four tenths of the way along.
                Contact("crossing", L(5, 5, -4, 5, 5, 6), Floor(), P(5, 5, 0)),
                Contact("touching at its start", L(5, 5, 0, 9, 8, 7), Floor(), P(5, 5, 0)),
                Contact("lying in the plane, so at its start", L(2, 3, 0, 12, 3, 0), Floor(), P(2, 3, 0)),
                Gap("slanting away, from its nearer end", L(10, 0, 9, 0, 0, 3), Floor(), 3, P(0, 0, 3), P(0, 0, 0)),
                Gap("parallel, from its start", L(2, 3, 5, 12, 3, 5), Floor(), 5, P(2, 3, 5), P(2, 3, 0)),
                Gap("below", L(0, 0, -4, 10, 0, -6), Floor(), 4, P(0, 0, -4), P(0, 0, 0)));

            AddPair(rows, Line, Polygon, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("crossing the surface", L(5, 5, -4, 5, 5, 6), Ell(), P(5, 5, 0)),
                Contact("ending on the surface", L(5, 5, 9, 5, 5, 0), Ell(), P(5, 5, 0)),
                Contact("lying in its plane across the outline", L(35, 5, 0, 50, 5, 0), Ell()),
                Gap("straight down from its nearer end", L(5, 5, 11, 5, 5, 3), Ell(), 3, P(5, 5, 3), P(5, 5, 0)),
                Gap("above the notch", L(25, 15, 12, 30, 30, 12), Ell(), 13, P(25, 15, 12), P(25, 10, 0)),
                Gap("beyond the outline", L(46, 5, 0, 50, 5, 0), Ell(), 6, P(46, 5, 0), P(40, 5, 0)),
                Tied(Gap("parallel above the surface", L(2, 3, 5, 8, 3, 5), Ell(), 5)));

            AddPair(rows, Line, Polyline, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("crossing the first edge", L(10, -5, 0, 10, 5, 0), Zig(), P(10, 0, 0)),
                Contact("ending on the last edge", L(30, 30, 0, 30, 20, 0), Zig(), P(30, 20, 0)),
                Gap("passing over", L(10, -5, 4, 10, 5, 4), Zig(), 4, P(10, 0, 4), P(10, 0, 0)),
                Gap("apart, from the last vertex", L(46, 28, 0, 56, 40, 0), Zig(), 10, P(46, 28, 0), P(40, 20, 0)),
                Tied(Gap("parallel to the first edge", L(2, 6, 8, 12, 6, 8), Zig(), 10)));

            // The plane z = 0 against a chain, and planes against a box 40 by 40 by 20 on the origin.
            AddPair(rows, Plane, Polyline, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                // Down through z = 0 at (5, 0, 0) on the first edge and back up at (15, 0, 0) on the second.
                Contact("crossing twice, so at the first edge's crossing", Floor(), PL(P(0, 0, 5), P(10, 0, -5), P(20, 0, 5)), P(5, 0, 0)),
                Contact("touching at a vertex", Floor(), PL(P(0, 0, 5), P(10, 0, 0), P(20, 0, 5)), P(10, 0, 0)),
                Contact("lying in the plane, so at its first vertex", Floor(), PL(P(2, 3, 0), P(12, 3, 0), P(12, 13, 0)), P(2, 3, 0)),
                // The second and third vertices are both 3 up; the first of them is taken.
                Gap("apart, with two vertices as near, from the first", Floor(), PL(P(0, 0, 9), P(10, 0, 3), P(20, 0, 3), P(30, 0, 9)), 3, P(10, 0, 0), P(10, 0, 3)),
                Gap("parallel, from its first vertex", Floor(), PL(P(0, 0, 7), P(10, 0, 7), P(10, 10, 7)), 7, P(0, 0, 0), P(0, 0, 7)),
                Gap("below", Floor(), PL(P(0, 0, -4), P(10, 0, -6)), 4, P(0, 0, 0), P(0, 0, -4)),
                // DistanceTo reads a chain within the point tolerance, a thousandth, as touching: 0.0004 is
                // within it and 0.004 clear of it, so the pair sits either side of the band. The line is not snapped.
                InBand(Gap("four ten-thousandths above, within the tolerance", Floor(), PL(P(0, 0, 0.0004), P(10, 0, 6)), 0.0004, P(0, 0, 0), P(0, 0, 0.0004))),
                Gap("four thousandths above, clear of the tolerance", Floor(), PL(P(0, 0, 0.004), P(10, 0, 6)), 0.004, P(0, 0, 0), P(0, 0, 0.004)));

            AddPair(rows, Plane, Solid, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("crossing the body", Level(10), Block()),
                Contact("on its top face", Level(20), Block()),
                // x + y + z = 100 meets the box at its corner (40, 40, 20) and nowhere else.
                Contact("through one corner only", new GeoPlane3(P(40, 40, 20), new GeoVector3(1, 1, 1)), Block(), P(40, 40, 20)),
                Tied(Gap("parallel above its top", Level(26), Block(), 6)),
                // x + y + z = 106 is 6 / sqrt(3) from that corner, whose foot is (42, 42, 22).
                Gap("slanting past one corner", new GeoPlane3(P(42, 42, 22), new GeoVector3(1, 1, 1)), Block(), 6 / Math.Sqrt(3), P(42, 42, 22), P(40, 40, 20)),
                InBand(Tied(Gap("four ten-thousandths above its top, within the tolerance", Level(20.0004), Block(), 0.0004))),
                Tied(Gap("four thousandths above its top, clear of the tolerance", Level(20.004), Block(), 0.004)));

            // The L-shaped polygon against chains, and polygons against the box.
            AddPair(rows, Polygon, Polyline, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("pierced by an edge", Ell(), PL(P(5, 5, -4), P(5, 5, 6), P(15, 5, 6)), P(5, 5, 0)),
                Contact("with a vertex on the surface", Ell(), PL(P(5, 5, 9), P(5, 5, 0), P(15, 5, 9)), P(5, 5, 0)),
                Gap("straight down to its nearest vertex", Ell(), PL(P(5, 5, 11), P(5, 5, 3), P(9, 5, 11)), 3, P(5, 5, 0), P(5, 5, 3)),
                Gap("above the notch", Ell(), PL(P(25, 15, 12), P(30, 30, 12), P(38, 32, 12)), 13, P(25, 10, 0), P(25, 15, 12)),
                Gap("beyond the outline in its plane", Ell(), PL(P(46, 5, 0), P(50, 5, 0), P(50, 20, 0)), 6, P(40, 5, 0), P(46, 5, 0)),
                Tied(Gap("parallel above the surface", Ell(), PL(P(2, 3, 5), P(8, 3, 5), P(8, 6, 5)), 5)));

            AddPair(rows, Polygon, Solid, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("cutting through the body", PG(P(20, -10, 5), P(20, 50, 5), P(20, 50, 15), P(20, -10, 15)), Block()),
                Contact("resting on its top", PG(P(10, 10, 20), P(30, 10, 20), P(30, 30, 20), P(10, 30, 20)), Block()),
                Gap("beside a face, from its nearest corner", PG(P(50, 10, 10), P(60, 10, 10), P(60, 20, 10)), Block(), 10, P(50, 10, 10), P(40, 10, 10)),
                Tied(Gap("parallel above its top", PG(P(10, 10, 26), P(30, 10, 26), P(30, 30, 26), P(10, 30, 26)), Block(), 6)),
                // z = 4 + (x - 10) / 10 + (y - 10) / 10: corners 4, 6, 8 and 6 up, every one at least 10 in
                // from the sides, so the nearest face is the bottom, 4 under (10, 10, 4).
                Depth("inside the body, out to the nearest face", PG(P(10, 10, 4), P(30, 10, 6), P(30, 30, 8), P(10, 30, 6)), Block(), 4, P(10, 10, 4), P(10, 10, 0)));

            // Chains against a chain, the box and a triangle.
            AddPair(rows, Polyline, Polyline, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("crossing once", Zig(), PL(P(10, -5, 0), P(10, 5, 0), P(14, 9, 0)), P(10, 0, 0)),
                Contact("ending on it", Zig(), PL(P(2, -10, 0), P(6, -6, 0), P(10, 0, 0)), P(10, 0, 0)),
                Gap("passing over", Zig(), PL(P(10, -5, 4), P(10, 5, 4), P(14, 9, 4)), 4, P(10, 0, 0), P(10, 0, 4)),
                Tied(Gap("parallel to the first edge", Zig(), PL(P(2, 6, 8), P(12, 6, 8)), 10)));

            AddPair(rows, Polyline, Solid, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("passing through the body", PL(P(20, 20, -5), P(20, 20, 30), P(25, 20, 30)), Block()),
                Contact("with a vertex on its top", PL(P(20, 20, 30), P(20, 20, 20), P(25, 25, 30)), Block(), P(20, 20, 20)),
                Gap("beside a face", PL(P(50, 10, 10), P(60, 10, 10), P(60, 20, 15)), Block(), 10, P(50, 10, 10), P(40, 10, 10)),
                Tied(Gap("parallel above its top", PL(P(2, 3, 26), P(12, 3, 26)), Block(), 6)),
                // Vertices 4, 8 and 8 in from their nearest faces: the first, 4 over the bottom.
                Depth("inside the body, out to the nearest face", PL(P(10, 10, 4), P(30, 10, 8), P(30, 30, 12)), Block(), 4, P(10, 10, 4), P(10, 10, 0)));

            AddPair(rows, Polyline, Triangle, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a), (a, b, t) => a.DistanceTo(b, t),
                Contact("piercing it", PL(P(5, 5, -4), P(5, 5, 6), P(9, 5, 6)), Tri(), P(5, 5, 0)),
                Contact("with a vertex on the surface", PL(P(5, 5, 9), P(5, 5, 0), P(9, 5, 9)), Tri(), P(5, 5, 0)),
                Gap("straight down", PL(P(5, 5, 11), P(5, 5, 3), P(9, 5, 11)), Tri(), 3, P(5, 5, 3), P(5, 5, 0)),
                Gap("beyond the long edge", PL(P(30, 30, 0), P(40, 40, 0)), Tri(), Math.Sqrt(200), P(30, 30, 0), P(20, 20, 0)),
                Tied(Gap("parallel above the surface", PL(P(2, 3, 5), P(8, 3, 5)), Tri(), 5)));

            return rows;
        }

        #endregion

        #region Rows and cases

        private sealed class Row
        {
            internal string Name;
            internal Func<Tolerance, GeoLine3> Line;
            internal Func<GeoLine3> PlainLine;
            internal Func<Tolerance, GeoLine3> Back;
            internal Func<GeoLine3> PlainBack;
            internal Func<Tolerance, double> Distance;
            internal Func<GeoPoint3, double> OffAsked;
            internal Func<GeoPoint3, double> OffArgument;
            internal Func<(double Nearest, double Reach)> Sampled;
            internal double Expected;
            internal bool Inside;
            internal bool Band;
            internal bool Tie;
            internal bool OneMember;
            internal double On;
            internal GeoPoint3? Start;
            internal GeoPoint3? End;
        }

        private sealed class Case<TA, TB>
        {
            internal string Name;
            internal TA Asked;
            internal TB Argument;
            internal double Expected;
            internal bool Inside;
            internal bool Band;
            internal bool Tie;
            internal double On = OnBound;
            internal GeoPoint3? Start;
            internal GeoPoint3? End;
        }

        /// <summary>How a shape is held to: how far a point is off it, and its samples or its closed form.</summary>
        private sealed class Kit<T>
        {
            internal Func<T, GeoPoint3, double> Off;
            internal Func<T, (GeoPoint3[] Points, double Reach)> Samples;
            internal Func<T, Func<GeoPoint3, double>> Exact;
            internal bool ChainOrRegion;
        }

        private static Case<TA, TB> Gap<TA, TB>(string name, TA asked, TB argument, double expected, GeoPoint3? start = null, GeoPoint3? end = null)
            => new Case<TA, TB> { Name = name, Asked = asked, Argument = argument, Expected = expected, Start = start, End = end };

        private static Case<TA, TB> Contact<TA, TB>(string name, TA asked, TB argument, GeoPoint3? at = null)
            => new Case<TA, TB> { Name = name, Asked = asked, Argument = argument, Expected = 0.0, Start = at, End = at };

        private static Case<TA, TB> Depth<TA, TB>(string name, TA asked, TB argument, double depth, GeoPoint3 start, GeoPoint3 end)
            => new Case<TA, TB> { Name = name, Asked = asked, Argument = argument, Expected = depth, Inside = true, Start = start, End = end };

        /// <summary>A case whose nearest pairs are a whole run, so that no point is fixed.</summary>
        private static Case<TA, TB> Tied<TA, TB>(Case<TA, TB> c)
        {
            c.Tie = true;
            return c;
        }

        /// <summary>
        /// A case within the point tolerance of touching, which DistanceTo reads as nought; the line is not snapped and
        /// keeps its true length (amendment D1).
        /// </summary>
        private static Case<TA, TB> InBand<TA, TB>(Case<TA, TB> c)
        {
            c.Band = true;
            return c;
        }

        /// <summary>A case read within a tolerance, whose ends may sit that far off their shapes.</summary>
        private static Case<TA, TB> Loose<TA, TB>(Case<TA, TB> c, double on)
        {
            c.On = on;
            c.Tie = true;
            return c;
        }

        private static void AddPair<TA, TB>(
            List<Row> rows,
            Kit<TA> asked,
            Kit<TB> argument,
            Func<TA, TB, Tolerance, GeoLine3> line,
            Func<TA, TB, GeoLine3> plainLine,
            Func<TB, TA, Tolerance, GeoLine3> back,
            Func<TB, TA, GeoLine3> plainBack,
            Func<TA, TB, Tolerance, double> distance,
            params Case<TA, TB>[] cases)
        {
            bool sampled = asked.ChainOrRegion || argument.ChainOrRegion;

            foreach (Case<TA, TB> c in cases)
            {
                rows.Add(new Row
                {
                    Name = typeof(TA).Name + "-" + typeof(TB).Name + ": " + c.Name,
                    Line = t => line(c.Asked, c.Argument, t),
                    PlainLine = () => plainLine(c.Asked, c.Argument),
                    Back = back == null ? null : (Func<Tolerance, GeoLine3>)(t => back(c.Argument, c.Asked, t)),
                    PlainBack = plainBack == null ? null : (Func<GeoLine3>)(() => plainBack(c.Argument, c.Asked)),
                    Distance = t => distance(c.Asked, c.Argument, t),
                    OffAsked = p => asked.Off(c.Asked, p),
                    OffArgument = p => argument.Off(c.Argument, p),
                    Sampled = sampled ? (Func<(double, double)>)(() => Oracle(asked, c.Asked, argument, c.Argument)) : null,
                    Expected = c.Expected,
                    Inside = c.Inside,
                    Band = c.Band,
                    Tie = c.Tie,
                    OneMember = typeof(TA) == typeof(TB),
                    On = c.On,
                    Start = c.Start,
                    End = c.End
                });
            }
        }

        private static Row Find(string name) => Table.Value.Single(r => r.Name == name);

        private static TheoryData<string> Names(IEnumerable<Row> rows)
        {
            var data = new TheoryData<string>();

            foreach (Row row in rows)
            {
                data.Add(row.Name);
            }

            return data;
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.InRange(expected.DistanceTo(actual), 0.0, OnBound);
        }

        #endregion

        #region The oracle

        /// <summary>
        /// The nearest pair of samples of two shapes, or of samples of one measured to the other in closed
        /// form, with how much nearer the true pair can be.
        /// </summary>
        private static (double Nearest, double Reach) Oracle<TA, TB>(Kit<TA> asked, TA a, Kit<TB> argument, TB b)
        {
            if (asked.Exact != null && argument.Samples != null)
            {
                (GeoPoint3[] points, double reach) = argument.Samples(b);
                Func<GeoPoint3, double> exact = asked.Exact(a);
                return (points.Min(exact), reach);
            }

            if (argument.Exact != null && asked.Samples != null)
            {
                (GeoPoint3[] points, double reach) = asked.Samples(a);
                Func<GeoPoint3, double> exact = argument.Exact(b);
                return (points.Min(exact), reach);
            }

            (GeoPoint3[] ours, double ourReach) = asked.Samples(a);
            (GeoPoint3[] theirs, double theirReach) = argument.Samples(b);
            double nearest = double.MaxValue;

            foreach (GeoPoint3 p in ours)
            {
                foreach (GeoPoint3 q in theirs)
                {
                    double dx = p.X - q.X, dy = p.Y - q.Y, dz = p.Z - q.Z;
                    nearest = Math.Min(nearest, dx * dx + dy * dy + dz * dz);
                }
            }

            return (Math.Sqrt(nearest), ourReach + theirReach);
        }

        /// <summary>Points along a run of segments, at most <see cref="CurveSpacing"/> apart, the vertices included.</summary>
        private static (GeoPoint3[] Points, double Reach) AlongSegments(IReadOnlyList<GeoPoint3> vertices)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                GeoPoint3 s = vertices[i], e = vertices[i + 1];
                int n = Math.Max(1, (int)Math.Ceiling(s.DistanceTo(e) / CurveSpacing));

                for (int k = 0; k < n; k++)
                {
                    double f = (double)k / n;
                    points.Add(new GeoPoint3(s.X + (e.X - s.X) * f, s.Y + (e.Y - s.Y) * f, s.Z + (e.Z - s.Z) * f));
                }
            }

            points.Add(vertices[vertices.Count - 1]);
            return (points.ToArray(), CurveSpacing / 2);
        }

        /// <summary>
        /// Points over a flat polygon, filled: its rim, and a grid <see cref="RegionSpacing"/> apart in its
        /// plane kept where an even-odd count of its own puts them inside.
        /// </summary>
        private static (GeoPoint3[] Points, double Reach) OverRegion(IReadOnlyList<GeoPoint3> corners)
        {
            List<GeoPoint3> ring = corners.ToList();
            ring.Add(corners[0]);
            var points = new List<GeoPoint3>(AlongSegments(ring).Points);

            GeoPoint3 origin = corners[0];
            GeoVector3 u = origin.GetVectorTo(corners[1]).Normalize();
            GeoVector3 normal = Normal(corners);
            GeoVector3 w = normal.CrossProduct(u).Normalize();

            double[] us = corners.Select(c => origin.GetVectorTo(c).DotProduct(u)).ToArray();
            double[] ws = corners.Select(c => origin.GetVectorTo(c).DotProduct(w)).ToArray();

            for (double x = us.Min(); x <= us.Max(); x += RegionSpacing)
            {
                for (double y = ws.Min(); y <= ws.Max(); y += RegionSpacing)
                {
                    if (InsideByCount(us, ws, x, y))
                    {
                        points.Add(origin.Add(u.Multiply(x)).Add(w.Multiply(y)));
                    }
                }
            }

            // A point near a slanting rim can be up to a diagonal and a half from the grid point kept nearest it.
            return (points.ToArray(), 1.5 * RegionSpacing);
        }

        private static GeoVector3 Normal(IReadOnlyList<GeoPoint3> corners)
        {
            double nx = 0, ny = 0, nz = 0;

            for (int i = 0; i < corners.Count; i++)
            {
                GeoPoint3 p = corners[i], q = corners[(i + 1) % corners.Count];
                nx += (p.Y - q.Y) * (p.Z + q.Z);
                ny += (p.Z - q.Z) * (p.X + q.X);
                nz += (p.X - q.X) * (p.Y + q.Y);
            }

            return new GeoVector3(nx, ny, nz).Normalize();
        }

        private static bool InsideByCount(double[] xs, double[] ys, double x, double y)
        {
            bool inside = false;

            for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
            {
                if ((ys[i] > y) != (ys[j] > y) && x < (xs[j] - xs[i]) * (y - ys[i]) / (ys[j] - ys[i]) + xs[i])
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>How far a point is from the surface of the box the body was made from: out to it, or in to its nearest face.</summary>
        private static Func<GeoPoint3, double> BoxSurface(GeoPoint3 min, GeoPoint3 max)
        {
            return p =>
            {
                double dx = Math.Max(Math.Max(min.X - p.X, 0), p.X - max.X);
                double dy = Math.Max(Math.Max(min.Y - p.Y, 0), p.Y - max.Y);
                double dz = Math.Max(Math.Max(min.Z - p.Z, 0), p.Z - max.Z);

                if (dx > 0 || dy > 0 || dz > 0)
                {
                    return Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }

                return new[] { p.X - min.X, max.X - p.X, p.Y - min.Y, max.Y - p.Y, p.Z - min.Z, max.Z - p.Z }.Min();
            };
        }

        #endregion

        #region Kits: how each shape is held to

        private static readonly Kit<GeoPoint3> Point = new Kit<GeoPoint3>
        {
            Off = (s, p) => s.DistanceTo(p),
            Samples = s => (new[] { s }, 0.0),
            Exact = s => p => s.DistanceTo(p)
        };

        private static readonly Kit<GeoArc3> Arc = new Kit<GeoArc3> { Off = (s, p) => s.DistanceTo(p) };

        private static readonly Kit<GeoCircle3> Circle = new Kit<GeoCircle3> { Off = (s, p) => s.DistanceTo(p) };

        private static readonly Kit<GeoEllipse3> Ellipse = new Kit<GeoEllipse3> { Off = (s, p) => s.DistanceTo(p) };

        private static readonly Kit<GeoEdge3> Edge = new Kit<GeoEdge3> { Off = (s, p) => s.DistanceTo(p) };

        private static readonly Kit<GeoFace3> Face = new Kit<GeoFace3> { Off = (s, p) => s.DistanceTo(p, Within) };

        private static readonly Kit<GeoLine3> Line = new Kit<GeoLine3>
        {
            Off = (s, p) => s.DistanceTo(p),
            Samples = s => AlongSegments(new[] { s.StartPoint, s.EndPoint })
        };

        private static readonly Kit<GeoPlane3> Plane = new Kit<GeoPlane3>
        {
            Off = (s, p) => Math.Abs(s.SignedDistanceTo(p)),
            Exact = s => p => Math.Abs(s.Origin.GetVectorTo(p).DotProduct(s.Normal.Normalize()))
        };

        private static readonly Kit<GeoPolygon3> Polygon = new Kit<GeoPolygon3>
        {
            Off = (s, p) => s.DistanceTo(p),
            Samples = s => OverRegion(s.Vertices),
            ChainOrRegion = true
        };

        private static readonly Kit<GeoPolyline3> Polyline = new Kit<GeoPolyline3>
        {
            Off = (s, p) => s.DistanceTo(p),
            Samples = s => AlongSegments(s.Vertices),
            ChainOrRegion = true
        };

        private static readonly Kit<GeoPolylineArc3> Chain = new Kit<GeoPolylineArc3> { Off = (s, p) => s.DistanceTo(p) };

        private static readonly Kit<GeoRay3> Ray = new Kit<GeoRay3> { Off = (s, p) => s.DistanceTo(p) };

        private static readonly Kit<GeoTriangle3> Triangle = new Kit<GeoTriangle3>
        {
            Off = (s, p) => s.DistanceTo(p),
            Samples = s => OverRegion(new[] { s.A, s.B, s.C })
        };

        /// <summary>Every body here is <see cref="Block"/>, so its surface is measured in closed form.</summary>
        private static readonly Kit<GeoSolid3> Solid = new Kit<GeoSolid3>
        {
            Off = (s, p) => Math.Abs(s.SignedDistanceTo(p, Within)),
            Exact = s => BoxSurface(P(0, 0, 0), P(40, 40, 20))
        };

        #endregion

        #region Shapes

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoLine3 L(double x0, double y0, double z0, double x1, double y1, double z1) => new GeoLine3(P(x0, y0, z0), P(x1, y1, z1));

        private static GeoPolyline3 PL(params GeoPoint3[] vertices) => new GeoPolyline3(vertices);

        private static GeoPolygon3 PG(params GeoPoint3[] vertices) => new GeoPolygon3(vertices);

        private static GeoPolygon3 Square(double x0, double y0, double x1, double y1) => PG(P(x0, y0, 0), P(x1, y0, 0), P(x1, y1, 0), P(x0, y1, 0));

        /// <summary>The half circle of radius 10 about the origin in z = 0 on the side y &gt;= 0, taken from the edge.</summary>
        private static GeoArc3 Bow() => BowEdge().ToArc();

        /// <summary>The same half circle as an edge, from (10, 0, 0) over (0, 10, 0) to (-10, 0, 0).</summary>
        private static GeoEdge3 BowEdge() => new GeoEdge3(P(10, 0, 0), P(-10, 0, 0), 1.0, Up);

        private static GeoEdge3 StraightEdge() => new GeoEdge3(P(0, 0, 0), P(40, 0, 0));

        /// <summary>A disc of radius 10 about the origin in z = 0.</summary>
        private static GeoCircle3 Disc() => new GeoCircle3(O, Up, 10);

        /// <summary>An ellipse 40 by 20 about the origin in z = 0, its major axis along x.</summary>
        private static GeoEllipse3 Oval() => new GeoEllipse3(O, Up, GeoVector3.XAxis, 20, 10);

        /// <summary>A square 40 by 40 in z = 0 with a hole from (15, 15) to (25, 25).</summary>
        private static GeoFace3 Plate() => new GeoFace3(Square(0, 0, 40, 40), new[] { Square(15, 15, 25, 25) });

        private static GeoLine3 Rod() => L(0, 0, 0, 40, 0, 0);

        private static GeoRay3 Beam() => new GeoRay3(O, GeoVector3.XAxis);

        private static GeoPlane3 Floor() => new GeoPlane3(O, Up);

        private static GeoPlane3 Level(double z) => new GeoPlane3(P(0, 0, z), Up);

        /// <summary>An L in z = 0, 40 by 40 with the square from (10, 10) to (40, 40) notched out.</summary>
        private static GeoPolygon3 Ell() => PG(P(0, 0, 0), P(40, 0, 0), P(40, 10, 0), P(10, 10, 0), P(10, 40, 0), P(0, 40, 0));

        /// <summary>A chain along x to 20, up y to 20, and along x again to 40, in z = 0.</summary>
        private static GeoPolyline3 Zig() => PL(P(0, 0, 0), P(20, 0, 0), P(20, 20, 0), P(40, 20, 0));

        /// <summary>A run from (-30, 0, 0) to (-10, 0, 0), then the half circle of radius 10 under the origin to (10, 0, 0).</summary>
        private static GeoPolylineArc3 Hook() => new GeoPolylineArc3(new[] { P(-30, 0, 0), P(-10, 0, 0), P(10, 0, 0) }, new[] { 0.0, 1.0 }, new[] { Up, Up });

        private static GeoTriangle3 Tri() => new GeoTriangle3(P(0, 0, 0), P(40, 0, 0), P(0, 40, 0));

        /// <summary>A box from the origin to (40, 40, 20).</summary>
        private static GeoSolid3 Block() => new GeoAabb3(P(0, 0, 0), P(40, 40, 20)).ToObb().ToSolid();

        #endregion
    }
}
