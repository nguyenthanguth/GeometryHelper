using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Common;
using Xunit;
using static GeometryHelper.UnitTest.Solid.MeetingInSpaceOracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// How far apart straight and flat shapes in space are, and the shortest line between them, asked from either side:
    /// two flat regions, a segment, a chain and a ray against a face or a polygon, a plane against a bounded shape and a
    /// ray, two rays, a ray and a chain, a face and a body, and the boxes, which give the distance only, through their body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cases are a table. Each row names its numbers: the distance worked by hand, and, where the spec fixes it, the
    /// point the line starts and ends at. The fixtures sit within 60 of the origin, so the length is held to the
    /// distance within 1E-12 of a size of 100, and an end to its shape within 1E-9.
    /// </para>
    /// <para>
    /// What fixes a point. Two shapes that meet are nought apart, and are joined by a line of no length at their first
    /// point of contact in the order their crossings come in (<c>GetIntersections</c>, decision 11 of the groups C and D
    /// spec). Shapes that do not meet are joined by the least of the lines from each edge of either to the other. A plane
    /// is joined from the nearest vertex, the first of those equally near, and a shape crossing or touching it at the
    /// first point of contact edge by edge, a shape lying in it at its first vertex, as the plane against a chain is
    /// (<c>Projection3.Chains.cs:349-445</c>). Two regions in one plane are joined at the first contact the edge walk finds,
    /// the inner region's corner where one lies wholly on the other (amendment E7). A body runs surface to surface for the
    /// line and is read solid through for the distance, by the solid against a polygon's own reading of where they meet
    /// (<c>Projection3.Solid.cs:346-380</c>, <c>Distance3.cs:478-509</c>, amendment E7). Where a whole run of pairs is as near as
    /// any, the row names no point and only the length and the ends are held.
    /// </para>
    /// <para>
    /// The tolerance band (amendments D1 and D5). A line is never snapped: a shape lying level within the point tolerance
    /// keeps its true gap, 0.0004, though the distance reads nought where the two collide. A shape coming end-on at a
    /// region or a body and stopping within the tolerance is read as crossing it, as the segment to a triangle always has
    /// been (<c>Intersection3.cs:202-240</c>, <c>Projection3.Solid.cs:42-60</c>): the line of no length then sits at that
    /// end, up to the tolerance off the other. A plane is read exactly, as the plane against a chain is
    /// (<c>Projection3.Chains.cs:385-407</c>; <c>ShortestLinesInSpaceTests.cs:258-261</c>), so a shape 0.0004 from it,
    /// end-on or level, is joined by its true gap. Each band row has a twin at 0.004, clear of the band.
    /// </para>
    /// <para>
    /// Every row is also held to a dense-sampling oracle of its own arithmetic: points 0.25 apart over every region and
    /// along its rings, 0.1 apart along every segment, chain and ray, each measured in closed form to the other shape. No
    /// sampled pair may be nearer than the line, and the line may be no shorter than the nearest pair less the reach of
    /// the samples. A box's distance is also held to its body's: the box read as <c>ToSolid()</c>, or as
    /// <c>ToObb().ToSolid()</c> for an aligned one, measured by the members that already take a body (decision 4).
    /// </para>
    /// </remarks>
    public class DistancesInSpaceTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>1E-12 of a size of 100: every fixture lies within 60 of the origin.</summary>
        private const double Bound = 1E-12 * 100.0;

        /// <summary>How far an end may sit off its shape.</summary>
        private const double OnBound = 1E-9;

        /// <summary>How far an end may sit off its shape where a contact is read within the point tolerance of 1E-3.</summary>
        private const double OnBand = 1E-3 + OnBound;

        /// <summary>How far apart the samples along a segment, a chain and a ray are.</summary>
        private const double CurveSpacing = 0.1;

        /// <summary>How far along a ray it is sampled: past every fixture, which all lie within 60 of the origin.</summary>
        private const double RayReach = 200.0;

        private static readonly GeoVector3 X = GeoVector3.XAxis;

        private static readonly GeoVector3 Y = GeoVector3.YAxis;

        private static readonly GeoVector3 Z = GeoVector3.ZAxis;

        /// <summary>20 + 10 sqrt(2): how far the corner edges of <see cref="Turned"/> reach along x and y from the origin.</summary>
        private static readonly double Reach = 20.0 + 10.0 * Math.Sqrt(2.0);

        /// <summary>
        /// 10 sqrt(2) - 10: how far (30, 30, z) stands off the side of <see cref="Turned"/> facing it, the side 10 from the
        /// centre along (1, 1, 0) and the point 20 / sqrt(2) along it. The box's aligned bounds reach to (34.14, 34.14), so a
        /// box read by them would hold the point.
        /// </summary>
        private static readonly double OffASide = 10.0 * Math.Sqrt(2.0) - 10.0;

        private static readonly Lazy<List<Row>> Table = new Lazy<List<Row>>(Build);

        public static TheoryData<string> Rows() => Names(Table.Value);

        public static TheoryData<string> RowsWithALine() => Names(Table.Value.Where(r => r.Line != null));

        public static TheoryData<string> RowsWithAnOtherSide() => Names(Table.Value.Where(r => r.Line != null && !(r.OneMember && r.Tie)));

        public static TheoryData<string> ContactRows() => Names(Table.Value.Where(r => r.Line != null && r.FirstCrossing));

        public static TheoryData<string> SampledRows() => Names(Table.Value.Where(r => r.SampledDistance != null));

        public static TheoryData<string> BoxRows() => Names(Table.Value.Where(r => r.Body != null));

        [Theory]
        [MemberData(nameof(Rows))]
        public void TheDistanceIsAsWorkedByHandAndTheSameFromEitherSide(string name)
        {
            Row row = Find(name);

            // The fixture first, through the collision the library already gives: shapes that touch, cross or lie one in
            // the other collide and the rest do not, but in the band, where the reading of the collision decides.
            bool collides = row.Collides(Within);

            if (!row.Band && row.CollisionHolds)
            {
                Assert.Equal(row.Contact || row.Inside, collides);
            }

            double expected = row.Contact || row.Inside || (row.Band && collides) ? 0.0 : row.Expected;
            double distance = row.Distance(Within);

            Assert.Equal(expected, distance, Bound);
            Assert.Equal(distance, row.DistanceBack(Within), Bound);

            // The form without a tolerance is the one under the global tolerance, read and never set.
            MirrorTable.Same(row.Distance(Tolerance.Global), row.PlainDistance());
            MirrorTable.Same(row.DistanceBack(Tolerance.Global), row.PlainDistanceBack());
        }

        [Theory]
        [MemberData(nameof(BoxRows))]
        public void ABoxIsAsFarAsItsBody(string name)
        {
            Row row = Find(name);

            // The body's distance, through the members that already take one; a box is one line over it (decision 4).
            double body = row.Body(Within);

            Assert.Equal(body, row.Distance(Within), Bound);
            Assert.Equal(body, row.DistanceBack(Within), Bound);

            if (row.Band)
            {
                // Within the point tolerance the body reads nought or the true gap, whichever it reads; the box is the same.
                Assert.InRange(body, 0.0, row.Expected + Bound);
            }
        }

        [Theory]
        [MemberData(nameof(RowsWithALine))]
        public void TheLineLeavesTheShapeAskedLandsOnTheOtherAndIsAsLongAsTheDistance(string name)
        {
            Row row = Find(name);
            double distance = row.Distance(Within);
            GeoLine3 line = row.Line(Within);

            Assert.InRange(row.OffAsked(line.StartPoint), 0.0, row.On);
            Assert.InRange(row.OffArgument(line.EndPoint), 0.0, row.On);

            // As long as the distance; inside a body, as the depth out to the nearest face; within the point tolerance of
            // touching, as long as the true gap, since a line is never snapped.
            Assert.Equal(row.Inside || row.Band ? row.Expected : distance, line.Length, Bound);

            if (row.Start.HasValue)
            {
                Near(row.Start.Value, line.StartPoint, row.On);
            }

            if (row.End.HasValue)
            {
                Near(row.End.Value, line.EndPoint, row.On);
            }

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
                // Two shapes of one kind: the same member asked the other way, with one nearest pair to find.
                Near(line.EndPoint, back.StartPoint, row.On);
                Near(line.StartPoint, back.EndPoint, row.On);
                return;
            }

            MirrorTable.Same(line.Reverse(), back);
            MirrorTable.Same(row.PlainLine().Reverse(), row.PlainBack());
        }

        [Theory]
        [MemberData(nameof(ContactRows))]
        public void AContactIsJoinedAtTheFirstCrossingFromEitherSide(string name)
        {
            Row row = Find(name);

            // The fixture first: the crossings the library already gives, and the first of them where the row works it.
            GeoPoint3[] crossings = row.Crossings(Within);
            GeoPoint3[] crossingsBack = row.CrossingsBack(Within);

            Assert.NotEmpty(crossings);
            Assert.NotEmpty(crossingsBack);

            if (row.Start.HasValue)
            {
                Near(row.Start.Value, crossings[0], row.On);
            }

            GeoLine3 line = row.Line(Within);
            GeoLine3 back = row.Back(Within);

            Assert.Equal(0.0, line.Length);
            Assert.Equal(0.0, back.Length);
            Near(crossings[0], line.StartPoint, row.On);
            Near(crossingsBack[0], back.StartPoint, row.On);
        }

        [Theory]
        [MemberData(nameof(SampledRows))]
        public void NoPairOfDenseSamplesIsNearerThanTheLineOrTheDistance(string name)
        {
            Row row = Find(name);
            (double nearest, double reach) = row.SampledDistance();

            // The fixture first: the samples find the gap worked by hand, or next to none where the two meet.
            if (row.Contact)
            {
                Assert.InRange(nearest, 0.0, reach);
            }
            else if (!row.Inside)
            {
                Assert.InRange(row.Expected, nearest - reach, nearest + Bound);
            }

            Assert.InRange(row.Distance(Within), nearest - reach, nearest + Bound);

            if (row.Line != null)
            {
                (double surface, double surfaceReach) = row.SampledLine();
                Assert.InRange(row.Line(Within).Length, surface - surfaceReach, surface + Bound);
            }
        }

        [Fact]
        public void ARayAgainstAChainWithACornerNotANumberIsNotANumberAway()
        {
            // A NaN is carried through, as the chain distances carry it (amendment D7, Projection3.Chains.cs:458-467): the
            // first edge is 50 from the ray, and a strict comparison would drop the NaN of the second and answer 50.
            var tolerance = new Tolerance(1E-3, 1E-5);
            var ray = new GeoRay3(GeoPoint3.Origin, X);
            var chain = new GeoPolyline3(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0), new GeoPoint3(double.NaN, 0, 0));

            Assert.True(double.IsNaN(ray.DistanceTo(chain, tolerance)));
            Assert.True(double.IsNaN(chain.DistanceTo(ray, tolerance)));
        }

        #region The table

        private static List<Row> Build()
        {
            var rows = new List<Row>();

            // Two flat regions. The region asked is the plate, a face with a hole, or the L, a polygon; the other is a
            // small kite, a dart or a holed kite (MeetingInSpaceOracle, "Shapes for the distances"). Every region asked has
            // its edge x = 0 from (0, 0) to (0, 40) and material over x from 4 to 10, y from 3 to 7.
            AddPair(rows, Face, Face, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                FlatCases(Plate(), HoledKite).Concat(HoleCases(Plate(), HoledKite)).ToArray());

            AddPair(rows, Face, Polygon, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                FlatCases(Plate(), Kite).Concat(HoleCases(Plate(), Kite)).ToArray());

            AddPair(rows, Face, Triangle, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                FlatCases(Plate(), Dart).Concat(HoleCases(Plate(), Dart)).ToArray());

            AddPair(rows, Polygon, Polygon, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                FlatCases(Ell(), Kite).Concat(NotchCases(Ell(), Kite)).ToArray());

            AddPair(rows, Polygon, Triangle, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                FlatCases(Ell(), Dart).Concat(NotchCases(Ell(), Dart)).ToArray());

            // A segment against the plate: the new primitive, read by the face's material (decision 11).
            AddPair(rows, Face, Line, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                Contact("crossing the material", Plate(), L(5, 5, -4, 5, 5, 6), P(5, 5, 0)),
                Contact("ending on the material", Plate(), L(5, 5, 9, 5, 5, 0), P(5, 5, 0)),
                InPlane(Lying("lying in its plane across the outline", Plate(), L(35, 5, 0, 50, 5, 0))),
                Gap("straight down from its nearer end", Plate(), L(5, 5, 11, 5, 5, 3), 3, P(5, 5, 0), P(5, 5, 3)),
                // Through the hole from (15, 15) to (25, 25) at (20, 22): 3 from its rim at y = 25, 5 from x = 15 and 25.
                Gap("through the hole, clear of its rim", Plate(), L(20, 22, -4, 20, 22, 6), 3, P(20, 25, 0), P(20, 22, 0)),
                Contact("through the rim of the hole", Plate(), L(20, 25, -4, 20, 25, 6), P(20, 25, 0)),
                // Level over the hole at a height of 4, 3 in from its rim at y = 25: 5.
                Tied(Gap("level over the hole", Plate(), L(18, 22, 4, 22, 22, 4), 5)),
                Gap("beyond the outline in its plane", Plate(), L(46, 5, 0, 50, 5, 0), 6, P(40, 5, 0), P(46, 5, 0)),
                // Upright at (42, -3): 2 and 3 off the corner (40, 0, 0), the square root of 13.
                Gap("upright, past a corner", Plate(), L(42, -3, -4, 42, -3, 4), Math.Sqrt(13), P(40, 0, 0), P(42, -3, 0)),
                Tied(Gap("level over the material", Plate(), L(2, 3, 5, 8, 3, 5), 5)),
                InBand(Tied(Gap("level four ten-thousandths above, within the tolerance", Plate(), L(2, 3, 0.0004, 8, 3, 0.0004), 0.0004))),
                Tied(Gap("level four thousandths above, clear of the tolerance", Plate(), L(2, 3, 0.004, 8, 3, 0.004), 0.004)),
                EndOn(Contact("end-on, stopping four ten-thousandths above, within the tolerance", Plate(), L(5, 5, 0.0004, 5, 5, 9), P(5, 5, 0.0004))),
                Gap("end-on, stopping four thousandths above, clear of the tolerance", Plate(), L(5, 5, 0.004, 5, 5, 9), 0.004, P(5, 5, 0), P(5, 5, 0.004)));

            AddPair(rows, Face, Polyline, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                Contact("pierced by an edge", Plate(), PL(P(5, 5, -4), P(5, 5, 6), P(15, 5, 6)), P(5, 5, 0)),
                Contact("with a vertex on the material", Plate(), PL(P(5, 5, 9), P(5, 5, 0), P(15, 5, 9)), P(5, 5, 0)),
                Gap("threading the hole", Plate(), PL(P(20, 22, -4), P(20, 22, 6), P(21, 22, 6)), 3, P(20, 25, 0), P(20, 22, 0)),
                Gap("straight down to its nearest vertex", Plate(), PL(P(5, 5, 11), P(5, 5, 3), P(9, 5, 11)), 3, P(5, 5, 0), P(5, 5, 3)),
                Gap("beyond the outline in its plane", Plate(), PL(P(46, 5, 0), P(50, 5, 0), P(50, 20, 0)), 6, P(40, 5, 0), P(46, 5, 0)),
                Tied(Gap("level over the material", Plate(), PL(P(2, 3, 5), P(8, 3, 5), P(8, 6, 5)), 5)),
                InBand(Tied(Gap("level four ten-thousandths above, within the tolerance", Plate(), PL(P(2, 3, 0.0004), P(8, 3, 0.0004), P(8, 6, 0.0004)), 0.0004))),
                EndOn(Contact("end-on, stopping four ten-thousandths above, within the tolerance", Plate(), PL(P(5, 5, 0.0004), P(5, 5, 9), P(9, 5, 9)), P(5, 5, 0.0004))),
                Gap("end-on, stopping four thousandths above, clear of the tolerance", Plate(), PL(P(5, 5, 0.004), P(5, 5, 9), P(9, 5, 9)), 0.004, P(5, 5, 0), P(5, 5, 0.004)));

            // A ray takes the place of an edge (decision 11).
            AddPair(rows, Face, Ray, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                Contact("crossing the material", Plate(), R(5, 5, -4, Z), P(5, 5, 0)),
                Contact("starting on the material", Plate(), R(5, 5, 0, Z), P(5, 5, 0)),
                InPlane(Lying("lying in its plane, running in across the outline", Plate(), R(50, 5, 0, X.Negate()))),
                Gap("pointing away above the material", Plate(), R(5, 5, 3, Z), 3, P(5, 5, 0), P(5, 5, 3)),
                Gap("through the hole, clear of its rim", Plate(), R(20, 22, -4, Z), 3, P(20, 25, 0), P(20, 22, 0)),
                Contact("through the rim of the hole", Plate(), R(20, 25, -4, Z), P(20, 25, 0)),
                Gap("beyond the outline, pointing away", Plate(), R(46, 5, 0, X), 6, P(40, 5, 0), P(46, 5, 0)),
                Gap("upright, past a corner", Plate(), R(42, -3, -4, Z), Math.Sqrt(13), P(40, 0, 0), P(42, -3, 0)),
                Tied(Gap("level over the material", Plate(), R(2, 3, 5, X), 5)),
                InBand(Tied(Gap("level four ten-thousandths above, within the tolerance", Plate(), R(2, 3, 0.0004, X), 0.0004))),
                EndOn(Contact("end-on, starting four ten-thousandths above, within the tolerance", Plate(), R(5, 5, 0.0004, Z), P(5, 5, 0.0004))),
                Gap("end-on, starting four thousandths above, clear of the tolerance", Plate(), R(5, 5, 0.004, Z), 0.004, P(5, 5, 0), P(5, 5, 0.004)));

            AddPair(rows, Polygon, Ray, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                Contact("crossing the surface", Ell(), R(5, 5, -4, Z), P(5, 5, 0)),
                Contact("starting on the surface", Ell(), R(5, 5, 0, Z), P(5, 5, 0)),
                Gap("pointing away above the surface", Ell(), R(5, 5, 3, Z), 3, P(5, 5, 0), P(5, 5, 3)),
                // Over the notch of the L: 5 across to its edge at y = 10 and 12 down, so 13.
                Gap("over the notch, pointing away", Ell(), R(25, 15, 12, Z), 13, P(25, 10, 0), P(25, 15, 12)),
                Gap("beyond the outline, pointing away", Ell(), R(46, 5, 0, X), 6, P(40, 5, 0), P(46, 5, 0)),
                Gap("upright, past a corner", Ell(), R(42, -3, -4, Z), Math.Sqrt(13), P(40, 0, 0), P(42, -3, 0)),
                Tied(Gap("level over the surface", Ell(), R(2, 3, 5, X), 5)),
                InBand(Tied(Gap("level four ten-thousandths above, within the tolerance", Ell(), R(2, 3, 0.0004, X), 0.0004))),
                EndOn(Contact("end-on, starting four ten-thousandths above, within the tolerance", Ell(), R(5, 5, 0.0004, Z), P(5, 5, 0.0004))),
                Gap("end-on, starting four thousandths above, clear of the tolerance", Ell(), R(5, 5, 0.004, Z), 0.004, P(5, 5, 0), P(5, 5, 0.004)));

            // The plane z = 0, or one standing, asked of bounded shapes and a ray. The plane is asked, so its line leaves it.
            AddPair(rows, Plane, Polygon, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                // Its bottom edge is under the plane; its second edge, up from (15, 5, -4) to (15, 5, 6), is the first through.
                Contact("crossing, at its second edge", Floor(), PG(P(5, 5, -4), P(15, 5, -4), P(15, 5, 6), P(5, 5, 6)), P(15, 5, 0)),
                // Heights 0, 6, 12 and 6: a plane through the four.
                Contact("touching at a vertex", Floor(), PG(P(5, 5, 0), P(15, 5, 6), P(15, 15, 12), P(5, 15, 6)), P(5, 5, 0)),
                Lying("lying in the plane, so at its first vertex", Floor(), Square(0, 0, 10, 10), P(0, 0, 0)),
                // Heights 9, 3, 6 and 12 on z = 9 - 0.6 x + 0.3 y.
                Gap("apart, from its nearest vertex", Floor(), PG(P(0, 0, 9), P(10, 0, 3), P(10, 10, 6), P(0, 10, 12)), 3, P(10, 0, 0), P(10, 0, 3)),
                Gap("apart, with two vertices as near, from the first", Floor(), PG(P(0, 0, 9), P(10, 0, 3), P(20, 0, 3), P(30, 0, 9)), 3, P(10, 0, 0), P(10, 0, 3)),
                Gap("parallel, from its first vertex", Floor(), Square(0, 0, 10, 10, 7), 7, P(0, 0, 0), P(0, 0, 7)),
                Gap("below, from its first vertex", Floor(), Square(0, 0, 10, 10, -4), 4, P(0, 0, 0), P(0, 0, -4)),
                InBand(Gap("slanting up from an edge four ten-thousandths above, within the tolerance", Floor(), PG(P(0, 0, 0.0004), P(10, 0, 6.0004), P(10, 10, 6.0004), P(0, 10, 0.0004)), 0.0004, P(0, 0, 0), P(0, 0, 0.0004))),
                InBand(Gap("level four ten-thousandths above, within the tolerance", Floor(), Square(0, 0, 10, 10, 0.0004), 0.0004, P(0, 0, 0), P(0, 0, 0.0004))),
                Gap("level four thousandths above, clear of the tolerance", Floor(), Square(0, 0, 10, 10, 0.004), 0.004, P(0, 0, 0), P(0, 0, 0.004)));

            AddPair(rows, Plane, Triangle, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                // Up from -4 to 6 along its first edge: through four tenths of the way, at (9, 5, 0).
                Contact("crossing, at its first edge", Floor(), Tri(P(5, 5, -4), P(15, 5, 6), P(5, 15, 6)), P(9, 5, 0)),
                Contact("touching at a vertex", Floor(), Tri(P(5, 5, 0), P(15, 5, 6), P(5, 15, 6)), P(5, 5, 0)),
                Lying("lying in the plane, so at its first vertex", Floor(), Tri(P(0, 0, 0), P(10, 0, 0), P(0, 10, 0)), P(0, 0, 0)),
                Gap("apart, from its nearest vertex", Floor(), Tri(P(0, 0, 9), P(10, 0, 3), P(0, 10, 6)), 3, P(10, 0, 0), P(10, 0, 3)),
                Gap("parallel, from its first vertex", Floor(), Tri(P(0, 0, 7), P(10, 0, 7), P(0, 10, 7)), 7, P(0, 0, 0), P(0, 0, 7)),
                InBand(Gap("slanting up from a vertex four ten-thousandths above, within the tolerance", Floor(), Tri(P(0, 0, 0.0004), P(10, 0, 6), P(0, 10, 6)), 0.0004, P(0, 0, 0), P(0, 0, 0.0004))),
                Gap("slanting up from a vertex four thousandths above, clear of the tolerance", Floor(), Tri(P(0, 0, 0.004), P(10, 0, 6), P(0, 10, 6)), 0.004, P(0, 0, 0), P(0, 0, 0.004)));

            AddPair(rows, Plane, Face, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                // x = 20 crosses the outline's first edge, from (0, 0, 0) to (40, 0, 0), at (20, 0, 0).
                Contact("standing across it, at the outline's first edge", Wall(20), Plate(), P(20, 0, 0)),
                Contact("through its first corner only", new GeoPlane3(P(0, 0, 0), new GeoVector3(1, 1, 0)), Plate(), P(0, 0, 0)),
                Lying("lying in the plane, so at its first vertex", Floor(), Plate(), P(0, 0, 0)),
                // x = 50 is 10 from the outline's corners (40, 0, 0) and (40, 40, 0), the first of which is taken; the hole's are 25 off.
                Gap("standing apart, from the first of two nearest corners", Wall(50), Plate(), 10, P(50, 0, 0), P(40, 0, 0)),
                Gap("parallel, from its first vertex", Level(3), Plate(), 3, P(0, 0, 3), P(0, 0, 0)),
                InBand(Gap("level four ten-thousandths above, within the tolerance", Level(0.0004), Plate(), 0.0004, P(0, 0, 0.0004), P(0, 0, 0))),
                InBand(Gap("standing four ten-thousandths off its corners, within the tolerance", Wall(40.0004), Plate(), 0.0004, P(40.0004, 0, 0), P(40, 0, 0))),
                Gap("standing four thousandths off its corners, clear of the tolerance", Wall(40.004), Plate(), 0.004, P(40.004, 0, 0), P(40, 0, 0)));

            AddPair(rows, Plane, Ray, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                Contact("running down through it", Floor(), R(5, 5, 4, Z.Negate()), P(5, 5, 0)),
                Lying("starting on it, pointing away", Floor(), R(5, 5, 0, Z), P(5, 5, 0)),
                Lying("lying in it, so at its origin", Floor(), R(5, 5, 0, X), P(5, 5, 0)),
                Gap("slanting away, from its origin", Floor(), R(5, 5, 4, new GeoVector3(1, 0, 1)), 4, P(5, 5, 0), P(5, 5, 4)),
                Gap("parallel, from its origin", Floor(), R(5, 5, 4, X), 4, P(5, 5, 0), P(5, 5, 4)),
                Gap("below, pointing down", Floor(), R(5, 5, -4, Z.Negate()), 4, P(5, 5, 0), P(5, 5, -4)),
                InBand(Gap("starting four ten-thousandths above, pointing away, within the tolerance", Floor(), R(5, 5, 0.0004, Z), 0.0004, P(5, 5, 0), P(5, 5, 0.0004))),
                InBand(Gap("level four ten-thousandths above, within the tolerance", Floor(), R(5, 5, 0.0004, X), 0.0004, P(5, 5, 0), P(5, 5, 0.0004))),
                Gap("starting four thousandths above, pointing away, clear of the tolerance", Floor(), R(5, 5, 0.004, Z), 0.004, P(5, 5, 0), P(5, 5, 0.004)));

            // Two rays: the first runs from the origin along x.
            AddPair(rows, Ray, Ray, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                Contact("crossing", Beam(), R(10, -5, 0, Y), P(10, 0, 0)),
                Contact("starting on it", Beam(), R(10, 0, 0, Y), P(10, 0, 0)),
                Gap("passing over", Beam(), R(10, -5, 4, Y), 4, P(10, 0, 0), P(10, 0, 4)),
                // Its nearest point to the first is behind the origin, so the origin is taken: 6 back and 8 up, 10.
                Gap("passing behind the origin", Beam(), R(-6, -5, 8, Y), 10, P(0, 0, 0), P(-6, 0, 8)),
                Tied(Gap("parallel beside it", Beam(), R(5, 6, 8, X), 10)),
                Gap("parallel, pointing away behind it", Beam(), R(-6, 0, 8, X.Negate()), 10, P(0, 0, 0), P(-6, 0, 8)),
                Tied(Lying("overlapping along one line", Beam(), R(10, 0, 0, X))),
                Gap("along one line, pointing away", Beam(), R(-10, 0, 0, X.Negate()), 10, P(0, 0, 0), P(-10, 0, 0)),
                InBand(Gap("passing four ten-thousandths over, within the tolerance", Beam(), R(10, -5, 0.0004, Y), 0.0004, P(10, 0, 0), P(10, 0, 0.0004))),
                InBand(Gap("starting four ten-thousandths beside it, pointing away, within the tolerance", Beam(), R(10, 0.0004, 0, Y), 0.0004, P(10, 0, 0), P(10, 0.0004, 0))),
                Gap("passing four thousandths over, clear of the tolerance", Beam(), R(10, -5, 0.004, Y), 0.004, P(10, 0, 0), P(10, 0, 0.004)));

            AddPair(rows, Ray, Polyline, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                Contact("crossing its first edge", Beam(), PL(P(10, -5, 0), P(10, 5, 0), P(14, 9, 0)), P(10, 0, 0)),
                Contact("with its last vertex on it", Beam(), PL(P(2, -10, 0), P(6, -6, 0), P(10, 0, 0)), P(10, 0, 0)),
                Gap("passing over", Beam(), PL(P(10, -5, 4), P(10, 5, 4), P(14, 9, 4)), 4, P(10, 0, 0), P(10, 0, 4)),
                Gap("behind the origin", Beam(), PL(P(-6, -5, 8), P(-6, 5, 8)), 10, P(0, 0, 0), P(-6, 0, 8)),
                Tied(Gap("parallel", Beam(), PL(P(2, 6, 8), P(12, 6, 8)), 10)),
                InBand(Gap("passing four ten-thousandths over, within the tolerance", Beam(), PL(P(10, -5, 0.0004), P(10, 5, 0.0004)), 0.0004, P(10, 0, 0), P(10, 0, 0.0004))),
                InBand(Gap("end-on, stopping four ten-thousandths beside it, within the tolerance", Beam(), PL(P(10, 9, 0), P(10, 0.0004, 0)), 0.0004, P(10, 0, 0), P(10, 0.0004, 0))),
                Gap("passing four thousandths over, clear of the tolerance", Beam(), PL(P(10, -5, 0.004), P(10, 5, 0.004)), 0.004, P(10, 0, 0), P(10, 0, 0.004)));

            // A face against the box from the origin to (40, 40, 20), read solid through for the distance and surface to
            // surface for the line.
            AddPair(rows, Face, Solid, (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => a.GetShortestLineTo(b), (b, a, t) => b.GetShortestLineTo(a, t), (b, a) => b.GetShortestLineTo(a),
                (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                Reaches, (a, b, t) => a.GetIntersections(b, t), (b, a, t) => b.GetIntersections(a, t),
                Contact("cutting through the body", F(Across(20, -10, 5, 50, 15)), Block()),
                Lying("resting on its top", F(Square(10, 10, 30, 30, 20)), Block()),
                Gap("beside a side, from its nearest corner", F(PG(P(50, 10, 10), P(60, 10, 10), P(60, 20, 10))), Block(), 10, P(50, 10, 10), P(40, 10, 10)),
                Tied(Gap("parallel over its top", F(Square(10, 10, 30, 30, 26)), Block(), 6)),
                // z = 4 + (x - 10) / 10 + (y - 10) / 10: corners 4, 6, 8 and 6 up, every one at least 10 in from the sides,
                // so the nearest face is the bottom, 4 under (10, 10, 4).
                Depth("inside the body, out to the nearest face", F(PG(P(10, 10, 4), P(30, 10, 6), P(30, 30, 8), P(10, 30, 6))), Block(), 4, P(10, 10, 4), P(10, 10, 0)),
                // A frame at mid height, its hole 5 wider than the body on every side.
                Tied(Gap("its hole round the body", Holed(Square(-10, -10, 50, 50, 10), Square(-5, -5, 45, 45, 10)), Block(), 5)),
                InBand(Tied(Gap("level four ten-thousandths over its top, within the tolerance", F(Square(10, 10, 30, 30, 20.0004)), Block(), 0.0004))),
                Tied(Gap("level four thousandths over its top, clear of the tolerance", F(Square(10, 10, 30, 30, 20.004)), Block(), 0.004)),
                EndOn(Contact("end-on, its tip four ten-thousandths over the top, within the tolerance", HoledKite(P(20, 20, 20.0004), Z, Y), Block(), P(20, 20, 20.0004)), false),
                // The opening from (10, 10) to (30, 30) runs right through; the square from (15, 15) to (25, 25) at mid height
                // is 5 from each of its walls. The opening is read (Material3.Whole), so the body's raw faces do not count.
                Opening(Tied(Gap("in an opening, clear of its walls", F(Square(15, 15, 25, 25, 10)), Pierced(), 5))),
                // Standing in x = 20 from y = 15 to 25, through the opening from below the body to above it: 5 from the walls
                // at y = 10 and 30, though it crosses the raw top and bottom faces that run across the opening.
                Opening(Tied(Gap("standing through an opening, clear of its walls", F(Upright(20, 15, -5, 25, 25)), Pierced(), 5))));

            // The boxes: the distance only, through the body. The aligned box runs from the origin to (40, 40, 20); the turned
            // box is the cube 20 on a side about (20, 20, 10), turned 45 degrees about z, its corner edges 10 sqrt(2) out.
            AddBoxPair(rows, Aligned, Face, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => Reaches(b, a.ToObb().ToSolid(), t), (a, b, t) => b.DistanceTo(a.ToObb().ToSolid(), t),
                Gap("its hole round the box", Box(), Holed(Square(-10, -10, 50, 50, 10), Square(-5, -5, 45, 45, 10)), 5),
                Gap("beside a side", Box(), F(PG(P(50, 10, 10), P(60, 10, 10), P(60, 20, 10))), 10),
                Contact("resting on its top", Box(), F(Square(10, 10, 30, 30, 20))),
                Contact("inside it", Box(), F(PG(P(10, 10, 4), P(30, 10, 6), P(30, 30, 8), P(10, 30, 6)))),
                InBand(Gap("level four ten-thousandths over its top, within the tolerance", Box(), F(Square(10, 10, 30, 30, 20.0004)), 0.0004)),
                Gap("level four thousandths over its top, clear of the tolerance", Box(), F(Square(10, 10, 30, 30, 20.004)), 0.004));

            AddBoxPair(rows, Aligned, Line, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(b, a.ToObb().ToSolid(), t),
                Gap("beside a side", Box(), L(50, 10, 10, 60, 10, 10), 10),
                Contact("through it", Box(), L(20, 20, -5, 20, 20, 30)),
                Contact("inside it", Box(), L(10, 10, 5, 20, 20, 10)),
                // Level with y, 5 out past x = 40 and 10 over z = 20: the square root of 125.
                Gap("past its top edge", Box(), L(45, -5, 30, 45, 5, 30), Math.Sqrt(125)),
                InBand(Gap("four ten-thousandths beside a side, within the tolerance", Box(), L(40.0004, 10, 5, 40.0004, 30, 5), 0.0004)),
                Gap("four thousandths beside a side, clear of the tolerance", Box(), L(40.004, 10, 5, 40.004, 30, 5), 0.004));

            AddBoxPair(rows, Aligned, Turned, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(a.ToObb(), b, t),
                // Turned about (70, 20, 10), its nearest corner edge is at x = 70 - 10 sqrt(2).
                Gap("apart, a corner edge towards a side", Box(), Cube(70), 30 - 10 * Math.Sqrt(2)),
                // About (60, 60, 10), a side faces the corner edge (40, 40) of the box, (40 - 10 sqrt(2)) / sqrt(2) off it.
                Gap("apart, a side towards a corner edge", Box(), Cube(60, 60), 20 * Math.Sqrt(2) - 10),
                Contact("overlapping", Box(), Cube(40)),
                InBand(Gap("a corner edge four ten-thousandths off a side, within the tolerance", Box(), Cube(40 + 10 * Math.Sqrt(2) + 0.0004), 0.0004)),
                Gap("a corner edge four thousandths off a side, clear of the tolerance", Box(), Cube(40 + 10 * Math.Sqrt(2) + 0.004), 0.004));

            AddBoxPair(rows, Aligned, Plane, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(a.ToObb().ToSolid(), b, t),
                Gap("parallel over its top", Box(), Level(26), 6),
                Contact("through it", Box(), Level(10)),
                Contact("on its top", Box(), Level(20)),
                // x + y + z = 106 is 6 / sqrt(3) from the corner (40, 40, 20).
                Gap("slanting past a corner", Box(), new GeoPlane3(P(42, 42, 22), new GeoVector3(1, 1, 1)), 6 / Math.Sqrt(3)),
                InBand(Gap("four ten-thousandths over its top, within the tolerance", Box(), Level(20.0004), 0.0004)),
                Gap("four thousandths over its top, clear of the tolerance", Box(), Level(20.004), 0.004));

            AddBoxPair(rows, Aligned, Polygon, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(a.ToObb().ToSolid(), b, t),
                Gap("beside a side", Box(), PG(P(50, 10, 10), P(60, 10, 10), P(60, 20, 10)), 10),
                Contact("resting on its top", Box(), Square(10, 10, 30, 30, 20)),
                Contact("inside it", Box(), PG(P(10, 10, 4), P(30, 10, 6), P(30, 30, 8), P(10, 30, 6))),
                Gap("parallel over its top", Box(), Square(10, 10, 30, 30, 26), 6),
                InBand(Gap("level four ten-thousandths over its top, within the tolerance", Box(), Square(10, 10, 30, 30, 20.0004), 0.0004)),
                Gap("level four thousandths over its top, clear of the tolerance", Box(), Square(10, 10, 30, 30, 20.004), 0.004));

            AddBoxPair(rows, Aligned, Ray, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(b, a.ToObb().ToSolid(), t),
                Gap("pointing away from a side", Box(), R(50, 10, 10, X), 10),
                Contact("pointing at a side", Box(), R(50, 10, 10, X.Negate())),
                Contact("starting inside it", Box(), R(20, 20, 10, X)),
                InBand(Gap("starting four ten-thousandths off a side, pointing away, within the tolerance", Box(), R(40.0004, 10, 10, X), 0.0004)),
                Gap("starting four thousandths off a side, pointing away, clear of the tolerance", Box(), R(40.004, 10, 10, X), 0.004));

            AddBoxPair(rows, Aligned, Triangle, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(a.ToObb().ToSolid(), b, t),
                Gap("beside a side", Box(), Tri(P(50, 10, 10), P(60, 10, 10), P(60, 20, 10)), 10),
                Contact("through it", Box(), Tri(P(20, 20, -5), P(30, 20, 30), P(10, 20, 30))),
                Contact("inside it", Box(), Tri(P(10, 10, 4), P(30, 10, 6), P(10, 30, 6))),
                InBand(Gap("level four ten-thousandths over its top, within the tolerance", Box(), Tri(P(10, 10, 20.0004), P(30, 10, 20.0004), P(10, 30, 20.0004)), 0.0004)),
                Gap("level four thousandths over its top, clear of the tolerance", Box(), Tri(P(10, 10, 20.004), P(30, 10, 20.004), P(10, 30, 20.004)), 0.004));

            AddBoxPair(rows, Turned, Face, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => Reaches(b, a.ToSolid(), t), (a, b, t) => b.DistanceTo(a.ToSolid(), t),
                // The hole's rim at x = 2 is 20 - 10 sqrt(2) - 2 from the corner edge at x = 20 - 10 sqrt(2).
                Gap("its hole round the box", Cube(20), Holed(Square(-10, -10, 50, 50, 10), Square(2, 2, 38, 38, 10)), 18 - 10 * Math.Sqrt(2)),
                Gap("beside a side, from its nearest corner", Cube(20), F(PG(P(30, 30, 5), P(40, 30, 5), P(30, 40, 5))), OffASide),
                Gap("standing beside a corner edge", Cube(20), F(Upright(50, 10, 5, 30, 15)), 50 - Reach),
                Contact("cutting through it", Cube(20), F(Upright(20, -10, 5, 50, 15))),
                InBand(Gap("standing four ten-thousandths off a corner edge, within the tolerance", Cube(20), F(Upright(Reach + 0.0004, 10, 5, 30, 15)), 0.0004)),
                Gap("standing four thousandths off a corner edge, clear of the tolerance", Cube(20), F(Upright(Reach + 0.004, 10, 5, 30, 15)), 0.004));

            AddBoxPair(rows, Turned, Line, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(b, a.ToSolid(), t),
                Gap("beside a corner edge", Cube(20), L(50, 20, 5, 60, 20, 5), 50 - Reach),
                Gap("leaving a side square to it", Cube(20), L(30, 30, 5, 40, 40, 5), OffASide),
                Contact("through it", Cube(20), L(20, 20, -5, 20, 20, 30)),
                InBand(Gap("end-on, stopping four ten-thousandths off a corner edge, within the tolerance", Cube(20), L(Reach + 0.0004, 20, 5, 60, 20, 5), 0.0004)),
                Gap("end-on, stopping four thousandths off a corner edge, clear of the tolerance", Cube(20), L(Reach + 0.004, 20, 5, 60, 20, 5), 0.004));

            AddBoxPair(rows, Turned, Plane, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(a.ToSolid(), b, t),
                Gap("standing beside a corner edge", Cube(20), Wall(50), 50 - Reach),
                Gap("parallel to a side", Cube(20), new GeoPlane3(P(30, 30, 0), new GeoVector3(1, 1, 0)), OffASide),
                Contact("standing through it", Cube(20), Wall(20)),
                InBand(Gap("standing four ten-thousandths off a corner edge, within the tolerance", Cube(20), Wall(Reach + 0.0004), 0.0004)),
                Gap("standing four thousandths off a corner edge, clear of the tolerance", Cube(20), Wall(Reach + 0.004), 0.004));

            AddBoxPair(rows, Turned, Polygon, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(a.ToSolid(), b, t),
                Gap("standing beside a corner edge", Cube(20), Upright(50, 10, 5, 30, 15), 50 - Reach),
                Gap("parallel to a side", Cube(20), PG(P(30, 30, 5), P(35, 25, 5), P(35, 25, 15), P(30, 30, 15)), OffASide),
                Contact("cutting through it", Cube(20), Upright(20, -10, 5, 50, 15)),
                InBand(Gap("standing four ten-thousandths off a corner edge, within the tolerance", Cube(20), Upright(Reach + 0.0004, 10, 5, 30, 15), 0.0004)),
                Gap("standing four thousandths off a corner edge, clear of the tolerance", Cube(20), Upright(Reach + 0.004, 10, 5, 30, 15), 0.004));

            AddBoxPair(rows, Turned, Ray, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(b, a.ToSolid(), t),
                Gap("pointing away from a corner edge", Cube(20), R(50, 20, 5, X), 50 - Reach),
                Gap("leaving a side square to it", Cube(20), R(30, 30, 5, new GeoVector3(1, 1, 0)), OffASide),
                Contact("pointing at a corner edge", Cube(20), R(50, 20, 5, X.Negate())),
                InBand(Gap("starting four ten-thousandths off a corner edge, pointing away, within the tolerance", Cube(20), R(Reach + 0.0004, 20, 5, X), 0.0004)),
                Gap("starting four thousandths off a corner edge, pointing away, clear of the tolerance", Cube(20), R(Reach + 0.004, 20, 5, X), 0.004));

            AddBoxPair(rows, Turned, Triangle, (a, b, t) => a.DistanceTo(b, t), (a, b) => a.DistanceTo(b), (b, a, t) => b.DistanceTo(a, t), (b, a) => b.DistanceTo(a),
                (a, b, t) => a.CollidesWith(b, t), (a, b, t) => Distance3.DistanceTo(a.ToSolid(), b, t),
                Gap("its tip towards a corner edge", Cube(20), Tri(P(50, 20, 5), P(60, 15, 5), P(60, 25, 5)), 50 - Reach),
                Gap("beside a side, from its nearest corner", Cube(20), Tri(P(30, 30, 5), P(40, 30, 5), P(30, 40, 5)), OffASide),
                Contact("through it", Cube(20), Tri(P(20, 20, -5), P(30, 20, 30), P(10, 20, 30))),
                InBand(Gap("its tip four ten-thousandths off a corner edge, within the tolerance", Cube(20), Tri(P(Reach + 0.0004, 20, 5), P(60, 15, 5), P(60, 25, 5)), 0.0004)),
                Gap("its tip four thousandths off a corner edge, clear of the tolerance", Cube(20), Tri(P(Reach + 0.004, 20, 5), P(60, 15, 5), P(60, 25, 5)), 0.004));

            return rows;
        }

        /// <summary>
        /// The cases every pair of flat regions is asked: a probe of one kind, built by <paramref name="probe"/> from its tip
        /// and its frame, set about a region with its edge x = 0 and material over x from 4 to 10, y from 3 to 7.
        /// </summary>
        private static IEnumerable<Case<TA, TB>> FlatCases<TA, TB>(TA region, Func<GeoPoint3, GeoVector3, GeoVector3, TB> probe)
        {
            // In the region's plane, its tip 4 short of the edge x = 0, pointing at it.
            yield return Gap("apart in its plane, its tip towards an edge", region, probe(P(-4, 5, 0), X.Negate(), Y), 4, P(0, 5, 0), P(-4, 5, 0));
            yield return Tied(Gap("parallel over the material", region, probe(P(4, 5, 5), X, Y), 5));

            // Leaning away past the edge: its tip 3 out and 4 up from (0, 5, 0), so 5.
            yield return Gap("leaning away past an edge, from its tip", region, probe(P(-3, 5, 4), new GeoVector3(-1, 0, 1), Y), 5, P(0, 5, 0), P(-3, 5, 4));
            yield return Contact("standing on its tip on the material", region, probe(P(5, 5, 0), Z, Y), P(5, 5, 0));

            // Standing through the material in the plane x = 5: its first edge, from its tip (5, 5, -3) to (5, 3, 1), is
            // the first through, three quarters of the way along.
            yield return Contact("standing through the material", region, probe(P(5, 5, -3), Z, Y), P(5, 3.5, 0));
            // In one plane the line sits at the first contact the edge walk finds (amendment E7): lying wholly on the
            // material, the inner region's corner, its tip, whose foot is itself; lying across the edge x = 0, where the
            // first edge of the probe, from its tip (-2, 5, 0) to (2, 3, 0), crosses that edge, at (0, 4, 0).
            yield return Lying("lying on the material in its plane", region, probe(P(4, 5, 0), X, Y), P(4, 5, 0));
            yield return Lying("lying across an edge in its plane", region, probe(P(-2, 5, 0), X, Y), P(0, 4, 0));
            yield return InBand(Tied(Gap("level four ten-thousandths above, within the tolerance", region, probe(P(4, 5, 0.0004), X, Y), 0.0004)));
            yield return Tied(Gap("level four thousandths above, clear of the tolerance", region, probe(P(4, 5, 0.004), X, Y), 0.004));
            yield return EndOn(Contact("end-on, its tip four ten-thousandths above, within the tolerance", region, probe(P(5, 5, 0.0004), Z, Y), P(5, 5, 0.0004)));
            yield return Gap("end-on, its tip four thousandths above, clear of the tolerance", region, probe(P(5, 5, 0.004), Z, Y), 0.004, P(5, 5, 0), P(5, 5, 0.004));
        }

        /// <summary>The cases a face asks about its hole from (15, 15) to (25, 25).</summary>
        private static IEnumerable<Case<TA, TB>> HoleCases<TA, TB>(TA face, Func<GeoPoint3, GeoVector3, GeoVector3, TB> probe)
        {
            // Over the hole at a height of 3, its tip 1 in from the rim at x = 15: the square root of 10. Its far corners
            // are 3 in from the other three sides of the rim, so the square root of 18 away.
            yield return Gap("over the hole", face, probe(P(16, 20, 3), X, Y), Math.Sqrt(10), P(15, 20, 0), P(16, 20, 3));

            // Standing in the plane x = 20 through the hole, crossing its plane from y = 15.5 to 18.5. Its first edge, from
            // (20, 17, -3) to (20, 15, 1), passes the rim at y = 15 nearest at four fifths of the way, (20, 15.4, 0.2): the
            // square root of 0.4 squared and 0.2 squared, 0.2.
            yield return Gap("standing through the hole, clear of its rim", face, probe(P(20, 17, -3), Z, Y), Math.Sqrt(0.2), P(20, 15, 0), P(20, 15.4, 0.2));
            yield return Contact("leaning into the hole from its rim", face, probe(P(20, 15, 0), new GeoVector3(0, 1, 1), X), P(20, 15, 0));
        }

        /// <summary>The case the L asks about its notch from (10, 10) to (40, 40).</summary>
        private static IEnumerable<Case<TA, TB>> NotchCases<TA, TB>(TA ell, Func<GeoPoint3, GeoVector3, GeoVector3, TB> probe)
        {
            // In the notch, its tip 4 from the edge at x = 10; its nearest corner to the edge at y = 10 is 8 off it.
            yield return Gap("in the notch in its plane", ell, probe(P(14, 20, 0), X, Y), 4, P(10, 20, 0), P(14, 20, 0));
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
            internal Func<double> PlainDistance;
            internal Func<Tolerance, double> DistanceBack;
            internal Func<double> PlainDistanceBack;
            internal Func<Tolerance, bool> Collides;
            internal Func<Tolerance, GeoPoint3[]> Crossings;
            internal Func<Tolerance, GeoPoint3[]> CrossingsBack;
            internal Func<Tolerance, double> Body;
            internal Func<GeoPoint3, double> OffAsked;
            internal Func<GeoPoint3, double> OffArgument;
            internal Func<(double Nearest, double Reach)> SampledDistance;
            internal Func<(double Nearest, double Reach)> SampledLine;
            internal double Expected;
            internal bool Contact;
            internal bool FirstCrossing;
            internal bool Inside;
            internal bool Band;
            internal bool Tie;
            internal bool OneMember;
            internal bool CollisionHolds;
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
            internal bool Contact;
            internal bool FirstCrossing;
            internal bool Inside;
            internal bool Band;
            internal bool Tie;
            internal bool CollisionHolds = true;
            internal bool Opening;
            internal double On = OnBound;
            internal GeoPoint3? Start;
            internal GeoPoint3? End;
        }

        /// <summary>
        /// How a shape is held to: how far a point is from it as its distance reads it, and as its line does where the two
        /// differ, a body's surface; and its samples, where it has any.
        /// </summary>
        private sealed class Kit<T>
        {
            internal Func<T, Func<GeoPoint3, double>> Exact;
            internal Func<T, Func<GeoPoint3, double>> Surface;
            internal Func<T, (GeoPoint3[] Points, double Reach)> Samples;
        }

        private static Case<TA, TB> Gap<TA, TB>(string name, TA asked, TB argument, double expected, GeoPoint3? start = null, GeoPoint3? end = null)
            => new Case<TA, TB> { Name = name, Asked = asked, Argument = argument, Expected = expected, Start = start, End = end };

        /// <summary>A contact at one point, joined there by a line of no length: the first crossing.</summary>
        private static Case<TA, TB> Contact<TA, TB>(string name, TA asked, TB argument, GeoPoint3? at = null)
            => new Case<TA, TB> { Name = name, Asked = asked, Argument = argument, Contact = true, FirstCrossing = at.HasValue, Start = at, End = at };

        /// <summary>A contact along a stretch, joined at the first of its crossings, which the row does not work by hand.</summary>
        private static Case<TA, TB> Crossing<TA, TB>(string name, TA asked, TB argument)
            => new Case<TA, TB> { Name = name, Asked = asked, Argument = argument, Contact = true, FirstCrossing = true };

        /// <summary>
        /// A contact with no crossing to be first: a shape lying in the other's plane, or on it, joined at the point given,
        /// or, with none given, anywhere the two share.
        /// </summary>
        private static Case<TA, TB> Lying<TA, TB>(string name, TA asked, TB argument, GeoPoint3? at = null)
            => new Case<TA, TB> { Name = name, Asked = asked, Argument = argument, Contact = true, Start = at, End = at };

        private static Case<TA, TB> Depth<TA, TB>(string name, TA asked, TB argument, double depth, GeoPoint3 start, GeoPoint3 end)
            => new Case<TA, TB> { Name = name, Asked = asked, Argument = argument, Expected = depth, Inside = true, Start = start, End = end };

        /// <summary>A case whose nearest pairs are a whole run, so that no point is fixed.</summary>
        private static Case<TA, TB> Tied<TA, TB>(Case<TA, TB> c)
        {
            c.Tie = true;
            return c;
        }

        /// <summary>
        /// A case within the point tolerance of touching: the distance reads nought where the two collide, and the line
        /// keeps its true length (amendments D1 and D5).
        /// </summary>
        private static Case<TA, TB> InBand<TA, TB>(Case<TA, TB> c)
        {
            c.Band = true;
            return c;
        }

        /// <summary>
        /// A contact read end-on within the point tolerance (amendment D5): its line of no length sits at the end, up to
        /// the tolerance off the other shape. <paramref name="firstCrossing"/> says whether that end is held to the first
        /// crossing too.
        /// </summary>
        private static Case<TA, TB> EndOn<TA, TB>(Case<TA, TB> c, bool firstCrossing = true)
        {
            c.On = OnBand;
            c.FirstCrossing = firstCrossing;
            return c;
        }

        /// <summary>
        /// A segment or a ray lying in a face's plane across its material. Their collision with a face reads the crossing
        /// of its plane, which refuses a shape lying in it (Intersection3.cs:719-741 and :221-224), so it says they do not
        /// collide, and is not held as a fixture; the distance reads the edges and is nought.
        /// </summary>
        private static Case<TA, TB> InPlane<TA, TB>(Case<TA, TB> c)
        {
            c.CollisionHolds = false;
            return c;
        }

        /// <summary>A case beside an opening of <see cref="Pierced"/>, held by its own kit.</summary>
        private static Case<TA, TB> Opening<TA, TB>(Case<TA, TB> c)
        {
            c.Opening = true;
            return c;
        }

        /// <summary>
        /// Whether a face reaches a body as the solid against a polygon reads it: one of the triangles inside the face is
        /// nought from the material, openings included (Distance3.cs:434-509). The face and the boxes against a face read
        /// this, and not <c>CollidesWith(face, solid)</c>, which tests the outline against the raw faces running across an
        /// opening (amendments E4 and E7; Collision3.cs:519 through :569).
        /// </summary>
        private static bool Reaches(GeoFace3 face, GeoSolid3 solid, Tolerance tolerance)
            => face.TriangulateSurface(tolerance).Any(piece => Distance3.DistanceTo(solid, piece, tolerance) == 0.0);

        private static void AddPair<TA, TB>(
            List<Row> rows,
            Kit<TA> asked,
            Kit<TB> argument,
            Func<TA, TB, Tolerance, GeoLine3> line,
            Func<TA, TB, GeoLine3> plainLine,
            Func<TB, TA, Tolerance, GeoLine3> back,
            Func<TB, TA, GeoLine3> plainBack,
            Func<TA, TB, Tolerance, double> distance,
            Func<TA, TB, double> plainDistance,
            Func<TB, TA, Tolerance, double> distanceBack,
            Func<TB, TA, double> plainDistanceBack,
            Func<TA, TB, Tolerance, bool> collides,
            Func<TA, TB, Tolerance, GeoPoint3[]> crossings,
            Func<TB, TA, Tolerance, GeoPoint3[]> crossingsBack,
            params Case<TA, TB>[] cases)
        {
            foreach (Case<TA, TB> c in cases)
            {
                Kit<TB> other = c.Opening ? (Kit<TB>)(object)PiercedKit : argument;
                Row row = Common(asked, other, c, distance, plainDistance, distanceBack, plainDistanceBack, collides);

                row.Line = t => line(c.Asked, c.Argument, t);
                row.PlainLine = () => plainLine(c.Asked, c.Argument);
                row.Back = t => back(c.Argument, c.Asked, t);
                row.PlainBack = () => plainBack(c.Argument, c.Asked);
                row.Crossings = t => crossings(c.Asked, c.Argument, t);
                row.CrossingsBack = t => crossingsBack(c.Argument, c.Asked, t);
                row.SampledLine = () => Oracle(asked, c.Asked, other, c.Argument, true);
                rows.Add(row);
            }
        }

        private static void AddBoxPair<TA, TB>(
            List<Row> rows,
            Kit<TA> asked,
            Kit<TB> argument,
            Func<TA, TB, Tolerance, double> distance,
            Func<TA, TB, double> plainDistance,
            Func<TB, TA, Tolerance, double> distanceBack,
            Func<TB, TA, double> plainDistanceBack,
            Func<TA, TB, Tolerance, bool> collides,
            Func<TA, TB, Tolerance, double> body,
            params Case<TA, TB>[] cases)
        {
            foreach (Case<TA, TB> c in cases)
            {
                Row row = Common(asked, argument, c, distance, plainDistance, distanceBack, plainDistanceBack, collides);

                row.Body = t => body(c.Asked, c.Argument, t);

                if (asked.Samples == null && argument.Samples == null)
                {
                    // Two boxes, or a box and a plane, have nothing to sample: the distance is held by hand and to the body.
                    row.SampledDistance = null;
                }

                rows.Add(row);
            }
        }

        private static Row Common<TA, TB>(
            Kit<TA> asked,
            Kit<TB> argument,
            Case<TA, TB> c,
            Func<TA, TB, Tolerance, double> distance,
            Func<TA, TB, double> plainDistance,
            Func<TB, TA, Tolerance, double> distanceBack,
            Func<TB, TA, double> plainDistanceBack,
            Func<TA, TB, Tolerance, bool> collides)
        {
            Func<GeoPoint3, double> offAsked = (asked.Surface ?? asked.Exact)(c.Asked);
            Func<GeoPoint3, double> offArgument = (argument.Surface ?? argument.Exact)(c.Argument);

            return new Row
            {
                Name = typeof(TA).Name + "-" + typeof(TB).Name + ": " + c.Name,
                Distance = t => distance(c.Asked, c.Argument, t),
                PlainDistance = () => plainDistance(c.Asked, c.Argument),
                DistanceBack = t => distanceBack(c.Argument, c.Asked, t),
                PlainDistanceBack = () => plainDistanceBack(c.Argument, c.Asked),
                Collides = t => collides(c.Asked, c.Argument, t),
                OffAsked = offAsked,
                OffArgument = offArgument,
                SampledDistance = () => Oracle(asked, c.Asked, argument, c.Argument, false),
                Expected = c.Expected,
                Contact = c.Contact,
                FirstCrossing = c.FirstCrossing,
                Inside = c.Inside,
                Band = c.Band,
                Tie = c.Tie,
                OneMember = typeof(TA) == typeof(TB),
                CollisionHolds = c.CollisionHolds,
                On = c.On,
                Start = c.Start,
                End = c.End
            };
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

        private static void Near(GeoPoint3 expected, GeoPoint3 actual, double within)
        {
            Assert.InRange(expected.DistanceTo(actual), 0.0, within);
        }

        #endregion

        #region The oracle

        /// <summary>
        /// The nearest pair of the samples of either shape measured in closed form to the other, with how much nearer the
        /// true pair can be: the least of the reaches of the samples used. <paramref name="surface"/> reads a body's
        /// surface, as its line does, rather than its material.
        /// </summary>
        private static (double Nearest, double Reach) Oracle<TA, TB>(Kit<TA> asked, TA a, Kit<TB> argument, TB b, bool surface)
        {
            Func<GeoPoint3, double> toA = (surface ? asked.Surface ?? asked.Exact : asked.Exact)(a);
            Func<GeoPoint3, double> toB = (surface ? argument.Surface ?? argument.Exact : argument.Exact)(b);
            double nearest = double.MaxValue;
            double reach = double.MaxValue;

            if (asked.Samples != null)
            {
                (GeoPoint3[] points, double r) = asked.Samples(a);
                nearest = Math.Min(nearest, points.Min(toB));
                reach = Math.Min(reach, r);
            }

            if (argument.Samples != null)
            {
                (GeoPoint3[] points, double r) = argument.Samples(b);
                nearest = Math.Min(nearest, points.Min(toA));
                reach = Math.Min(reach, r);
            }

            return (nearest, reach);
        }

        /// <summary>Points along a run of segments, at most <see cref="CurveSpacing"/> apart, the vertices included.</summary>
        private static (GeoPoint3[] Points, double Reach) AlongSegments(IReadOnlyList<GeoPoint3> vertices)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                points.AddRange(Along(vertices[i], vertices[i + 1], CurveSpacing));
            }

            return (points.ToArray(), CurveSpacing / 2);
        }

        private static double ToChain(IReadOnlyList<GeoPoint3> vertices, GeoPoint3 p)
        {
            double best = double.MaxValue;

            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                best = Math.Min(best, ToSegment(p, vertices[i], vertices[i + 1]));
            }

            return best;
        }

        #endregion

        #region Kits: how each shape is held to

        private static Kit<TFlat> FlatKit<TFlat>(Func<TFlat, Flat> of) => new Kit<TFlat>
        {
            Exact = s =>
            {
                Flat f = of(s);
                return p => MeetingInSpaceOracle.DistanceTo(f, p);
            },
            Samples = s => (SamplePoints(of(s)).ToArray(), Spacing)
        };

        private static readonly Kit<GeoFace3> Face = FlatKit<GeoFace3>(Of);

        private static readonly Kit<GeoPolygon3> Polygon = FlatKit<GeoPolygon3>(Of);

        private static readonly Kit<GeoTriangle3> Triangle = FlatKit<GeoTriangle3>(Of);

        private static readonly Kit<GeoLine3> Line = new Kit<GeoLine3>
        {
            Exact = s => p => ToSegment(p, s.StartPoint, s.EndPoint),
            Samples = s => AlongSegments(new[] { s.StartPoint, s.EndPoint })
        };

        private static readonly Kit<GeoPolyline3> Polyline = new Kit<GeoPolyline3>
        {
            Exact = s => p => ToChain(s.Vertices, p),
            Samples = s => AlongSegments(s.Vertices)
        };

        private static readonly Kit<GeoRay3> Ray = new Kit<GeoRay3>
        {
            Exact = s => p => ToRay(p, s.Origin, s.Direction),
            Samples = s => AlongSegments(new[] { s.Origin, s.Origin.Add(s.Direction.Normalize().Multiply(RayReach)) })
        };

        private static readonly Kit<GeoPlane3> Plane = new Kit<GeoPlane3> { Exact = s => p => Math.Abs(Height(p, s)) };

        /// <summary>Every body of the face rows but the pierced one is <see cref="Block"/>, so it is measured in closed form.</summary>
        private static readonly Kit<GeoSolid3> Solid = new Kit<GeoSolid3>
        {
            Exact = s => p => ToBox(p, P(20, 20, 10), X, Y, new[] { 20.0, 20.0, 10.0 }),
            Surface = s => p => ToBoxSurface(p, P(0, 0, 0), P(40, 40, 20))
        };

        /// <summary>
        /// <see cref="Pierced"/>, held only where its rows go: within the opening's footprint, whose walls at x and y = 10
        /// and 30, from z = 0 to 20, are the nearest material to any point there.
        /// </summary>
        private static readonly Kit<GeoSolid3> PiercedKit = new Kit<GeoSolid3>
        {
            Exact = s => p =>
            {
                double across = new[] { p.X - 10, 30 - p.X, p.Y - 10, 30 - p.Y }.Min();
                double up = Math.Max(0.0, Math.Max(-p.Z, p.Z - 20));
                return Math.Sqrt(across * across + up * up);
            }
        };

        private static readonly Kit<GeoAabb3> Aligned = new Kit<GeoAabb3>
        {
            Exact = s => p => ToBox(p, s.Center, X, Y, new[] { s.SizeX / 2, s.SizeY / 2, s.SizeZ / 2 })
        };

        private static readonly Kit<GeoObb3> Turned = new Kit<GeoObb3>
        {
            Exact = s => p => ToBox(p, s.CoordinateSystem.Origin, s.CoordinateSystem.XAxis, s.CoordinateSystem.YAxis, new[] { s.ExtentX, s.ExtentY, s.ExtentZ })
        };

        #endregion

        #region Shapes

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoLine3 L(double x0, double y0, double z0, double x1, double y1, double z1) => new GeoLine3(P(x0, y0, z0), P(x1, y1, z1));

        private static GeoPolyline3 PL(params GeoPoint3[] vertices) => new GeoPolyline3(vertices);

        private static GeoPolygon3 PG(params GeoPoint3[] vertices) => new GeoPolygon3(vertices);

        private static GeoFace3 F(GeoPolygon3 outline) => new GeoFace3(outline);

        private static GeoRay3 R(double x, double y, double z, GeoVector3 direction) => new GeoRay3(P(x, y, z), direction);

        /// <summary>The ray from the origin along x.</summary>
        private static GeoRay3 Beam() => R(0, 0, 0, X);

        private static GeoPlane3 Floor() => new GeoPlane3(P(0, 0, 0), Z);

        private static GeoPlane3 Level(double z) => new GeoPlane3(P(0, 0, z), Z);

        /// <summary>The plane x = <paramref name="x"/>.</summary>
        private static GeoPlane3 Wall(double x) => new GeoPlane3(P(x, 0, 0), X);

        /// <summary>A box from the origin to (40, 40, 20).</summary>
        private static GeoSolid3 Block() => MeetingInSpaceOracle.Block(P(0, 0, 0), P(40, 40, 20));

        /// <summary><see cref="Block"/> with the opening from (10, 10) to (30, 30) cut right through it.</summary>
        private static GeoSolid3 Pierced() => Body(P(0, 0, 0), P(40, 40, 20), (P(10, 10, -1), P(30, 30, 21))).Solid;

        /// <summary>The aligned box from the origin to (40, 40, 20).</summary>
        private static GeoAabb3 Box() => new GeoAabb3(P(0, 0, 0), P(40, 40, 20));

        /// <summary>The cube 20 on a side about (<paramref name="x"/>, <paramref name="y"/>, 10), turned 45 degrees about z.</summary>
        private static GeoObb3 Cube(double x, double y = 20) => new GeoObb3(P(x, y, 10), 20, 20, 20, new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0));

        #endregion
    }
}
