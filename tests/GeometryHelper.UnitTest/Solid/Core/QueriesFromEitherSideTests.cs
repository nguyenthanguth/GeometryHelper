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
    /// The sixty-three questions in space one shape could answer about another and the other could not
    /// answer back: the point's distance to an arc, an edge and the curved chains; the straight chain asked
    /// by ten shapes whether, and where, it meets them; the segment and the ray against an edge and the
    /// curved chains; and the shortest line from nine shapes to a curved loop. Each new member is one line
    /// asking the other side, so each is held to that side's answer exactly: the same bool or number, the
    /// same points in the same order, and the shortest line the same one turned round.
    /// </summary>
    /// <remarks>
    /// The cases are a table, not written out pair by pair. Every fixed shape lies in or below z = 0 and
    /// passes through the origin, or for the curved loop through (0, -20, 0) on its rim; every shape it is
    /// asked about comes down onto that point from above, rests on it, stops 0.4 short of it or 50 short of
    /// it, or lies in z = 0 or inside. The gap of 0.4 is clear of both tolerances, a thousandth and 1, so a
    /// member handing the wrong one on answers differently.
    /// </remarks>
    public class QueriesFromEitherSideTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        /// <summary>A point and a plane tolerance of 1, so the gap of 0.4 closes for points and planes alike.</summary>
        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5, Tolerance.DefaultEqualAngleRad, 1.0);

        // Declared before the table, which reads them as it is built.
        private static readonly GeoPoint3 O = GeoPoint3.Origin;

        /// <summary>The point of the curved loop's rim the shapes near it come down onto.</summary>
        private static readonly GeoPoint3 Rim = new GeoPoint3(0, -20, 0);

        private static readonly GeoVector3 Up = GeoVector3.ZAxis;

        private static readonly GeoVector3 Across = GeoVector3.XAxis;

        private static readonly MirrorTable Table = Build();

        public static TheoryData<string> Rows() => Table.Names();

        public static TheoryData<string> Members() => Table.Members();

        [Theory]
        [MemberData(nameof(Rows))]
        public void AShapeAsksTheOtherSideAndGivesItsAnswerExactly(string row)
        {
            Table.Check(row);
        }

        [Theory]
        [MemberData(nameof(Members))]
        public void TheCasesOfEachMemberDoNotAllGiveTheOtherSideTheSameAnswer(string member)
        {
            // A member answering the same whatever it is asked must not pass for one that asks.
            Assert.InRange(Table.DistinctAnswers(member), 2, int.MaxValue);
        }

        private static MirrorTable Build()
        {
            var table = new MirrorTable(Strict, Loose);

            // The point's distance to the curved shapes.
            Add(table, "DistanceTo", Each(Points(O), Bow()), (a, b) => a.DistanceTo(b), (a, b, t) => a.DistanceTo(b, t), (a, b) => b.DistanceTo(a), (a, b, t) => b.DistanceTo(a, t), MirrorTable.Same);
            Add(table, "DistanceTo", Each(Points(O), BowEdge()), (a, b) => a.DistanceTo(b), (a, b, t) => a.DistanceTo(b, t), (a, b) => b.DistanceTo(a), (a, b, t) => b.DistanceTo(a, t), MirrorTable.Same);
            Add(table, "DistanceTo", Each(Points(Rim), Loop()), (a, b) => a.DistanceTo(b), (a, b, t) => a.DistanceTo(b, t), (a, b) => b.DistanceTo(a), (a, b, t) => b.DistanceTo(a, t), MirrorTable.Same);
            Add(table, "DistanceTo", Each(Points(O), Hook()), (a, b) => a.DistanceTo(b), (a, b, t) => a.DistanceTo(b, t), (a, b) => b.DistanceTo(a), (a, b, t) => b.DistanceTo(a, t), MirrorTable.Same);

            // The segment and the ray against an edge, a curved loop and a curved chain.
            Add(table, "CollidesWith", Each(Lines(O), BowEdge()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Each(Rays(O), BowEdge()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Each(Lines(Rim), Loop()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Each(Rays(Rim), Loop()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Each(Lines(O), Hook()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Each(Rays(O), Hook()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Each(Lines(O), BowEdge()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Each(Rays(O), BowEdge()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Each(Lines(Rim), Loop()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Each(Rays(Rim), Loop()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Each(Lines(O), Hook()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Each(Rays(O), Hook()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "TryIntersectWith", Each(Lines(O), BowEdge()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Each(Rays(O), BowEdge()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Each(Lines(Rim), Loop()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Each(Rays(Rim), Loop()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Each(Lines(O), Hook()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Each(Rays(O), Hook()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);

            // Ten shapes, and the body, asked about a straight chain.
            Add(table, "CollidesWith", Against(Box(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Bow(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Disc(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Pierced(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Bar(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Turned(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Level(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Square(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Beam(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "CollidesWith", Against(Wedge(), Polylines()), (a, b) => a.CollidesWith(b), (a, b, t) => a.CollidesWith(b, t), (a, b) => b.CollidesWith(a), (a, b, t) => b.CollidesWith(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Box(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Bow(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Disc(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Pierced(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Bar(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Turned(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Level(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Square(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Beam(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Body(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "GetIntersections", Against(Wedge(), Polylines()), (a, b) => a.GetIntersections(b), (a, b, t) => a.GetIntersections(b, t), (a, b) => b.GetIntersections(a), (a, b, t) => b.GetIntersections(a, t), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Box(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Bow(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Disc(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Pierced(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Bar(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Turned(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Level(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Square(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Beam(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Body(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);
            Add(table, "TryIntersectWith", Against(Wedge(), Polylines()), (a, b) => (a.TryIntersectWith(b, out GeoPoint3[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint3[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint3[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint3[] p, t), p), MirrorTable.Same);

            // Nine shapes' shortest line to a curved loop: the loop's line to them, turned round.
            Add(table, "GetShortestLineTo", Each(ArcsNearTheRim(), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);
            Add(table, "GetShortestLineTo", Each(CirclesNearTheRim(), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);
            Add(table, "GetShortestLineTo", Each(EdgesNearTheRim(), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);
            Add(table, "GetShortestLineTo", Each(FacesNearTheRim(), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);
            Add(table, "GetShortestLineTo", Each(LinesNearTheRim(), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);
            Add(table, "GetShortestLineTo", Each(Points(Rim), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);
            Add(table, "GetShortestLineTo", Each(PolygonsNearTheRim(), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);
            Add(table, "GetShortestLineTo", Each(PolylinesNearTheRim(), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);
            Add(table, "GetShortestLineTo", Each(ChainsNearTheRim(), Loop()), (a, b) => a.GetShortestLineTo(b), (a, b, t) => a.GetShortestLineTo(b, t), (a, b) => b.GetShortestLineTo(a).Reverse(), (a, b, t) => b.GetShortestLineTo(a, t).Reverse(), MirrorTable.Same);

            return table;
        }

        private static void Add<TA, TB, TR>(
            MirrorTable table,
            string query,
            IEnumerable<(string Case, TA Asked, TB Argument)> cases,
            Func<TA, TB, TR> mine,
            Func<TA, TB, Tolerance, TR> mineWithin,
            Func<TA, TB, TR> theirs,
            Func<TA, TB, Tolerance, TR> theirsWithin,
            Action<TR, TR> same)
        {
            table.Add(typeof(TA).Name + "." + query + "(" + typeof(TB).Name + ")", cases, mine, mineWithin, theirs, theirsWithin, same);
        }

        /// <summary>One shape asked about each of a list.</summary>
        private static IEnumerable<(string Case, TA Asked, TB Argument)> Against<TA, TB>(TA asked, IEnumerable<(string Case, TB Shape)> arguments)
        {
            return arguments.Select(c => (c.Case, asked, c.Shape));
        }

        /// <summary>Each of a list asked about one shape.</summary>
        private static IEnumerable<(string Case, TA Asked, TB Argument)> Each<TA, TB>(IEnumerable<(string Case, TA Shape)> asked, TB argument)
        {
            return asked.Select(c => (c.Case, c.Shape, argument));
        }

        #region Fixed shapes through the origin, in or below z = 0

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoVector3 V(double x, double y, double z) => new GeoVector3(x, y, z);

        private static GeoPolygon3 Level(double x0, double y0, double x1, double y1, double z) => new GeoPolygon3(P(x0, y0, z), P(x1, y0, z), P(x1, y1, z), P(x0, y1, z));

        /// <summary>A slab 40 by 40 by 10 whose top, z = 0, has the origin at its middle.</summary>
        private static GeoAabb3 Box() => new GeoAabb3(P(-20, -20, -10), P(20, 20, 0));

        /// <summary>The same slab turned 0.3 about z.</summary>
        private static GeoObb3 Turned() => new GeoObb3(P(0, 0, -5), 40, 40, 10, V(Math.Cos(0.3), Math.Sin(0.3), 0), V(-Math.Sin(0.3), Math.Cos(0.3), 0));

        private static GeoSolid3 Body() => Turned().ToSolid();

        private static GeoPlane3 Level() => new GeoPlane3(O, Up);

        private static GeoPolygon3 Square() => Level(-20, -20, 20, 20, 0);

        /// <summary>The square with a hole from (8, -16) to (16, -8), clear of everything but one chain.</summary>
        private static GeoFace3 Pierced() => new GeoFace3(Square(), new[] { Level(8, -16, 16, -8, 0) });

        private static GeoTriangle3 Wedge() => new GeoTriangle3(P(-20, -20, 0), P(20, -20, 0), P(0, 20, 0));

        /// <summary>A disc of radius 15 about the origin in z = 0; only one chain comes down onto its rim.</summary>
        private static GeoCircle3 Disc() => new GeoCircle3(O, Up, 15);

        /// <summary>A half circle in z = 0 about (0, -10, 0), radius 10, its middle at the origin.</summary>
        private static GeoEdge3 BowEdge() => new GeoEdge3(P(10, -10, 0), P(-10, -10, 0), 1.0, Up);

        private static GeoArc3 Bow() => BowEdge().ToArc();

        private static GeoLine3 Bar() => new GeoLine3(P(-20, 0, 0), P(20, 0, 0));

        private static GeoRay3 Beam() => new GeoRay3(P(-20, 0, 0), Across);

        /// <summary>Along y = 0 from x = -20 to 20 through the origin, then a bulge up to (20, 30, 0).</summary>
        private static GeoPolylineArc3 Hook() => new GeoPolylineArc3(new[] { P(-20, 0, 0), P(20, 0, 0), P(20, 30, 0) }, new[] { 0.0, 0.5 }, new[] { Up, Up });

        /// <summary>The square 40 by 40 in z = 0 with its right side bulging out by 10; the origin is inside.</summary>
        private static GeoPolygonArc3 Loop() => new GeoPolygonArc3(new[] { P(-20, -20, 0), P(20, -20, 0), P(20, 20, 0), P(-20, 20, 0) }, new[] { 0.0, 0.5, 0.0, 0.0 }, new[] { Up, Up, Up, Up });

        #endregion

        #region Shapes coming down onto a point from above

        private static IEnumerable<(string Case, GeoPoint3 Shape)> Points(GeoPoint3 at)
        {
            yield return ("on it", at);
            yield return ("0.4 above it", at + V(0, 0, 0.4));
            yield return ("50 above it", at + V(0, 0, 50));
            yield return ("at (0, -10, 0), the bow's centre, as near every point of the bow", P(0, -10, 0));
            yield return ("10 above the origin", P(0, 0, 10));
            yield return ("in z = 0 outside everything", P(0, -30, 0));
            yield return ("in z = 0, 0.4 outside the loop's rim", P(0, -20.4, 0));
        }

        private static IEnumerable<(string Case, GeoLine3 Shape)> Lines(GeoPoint3 at)
        {
            yield return ("down through it", new GeoLine3(at + V(0, 0, 20), at + V(0, 0, -20)));
            yield return ("ending on it", new GeoLine3(at + V(0, 0, 20), at));
            yield return ("ending 0.4 above it", new GeoLine3(at + V(0, 0, 20.4), at + V(0, 0, 0.4)));
            yield return ("50 above it", new GeoLine3(at + V(0, 0, 70), at + V(0, 0, 50)));
            yield return ("across it in z = 0", new GeoLine3(at + V(-5, -5, 0), at + V(5, 5, 0)));
            yield return ("down through (0, -10)", new GeoLine3(P(0, -10, 20), P(0, -10, -20)));
            yield return ("along y = -5 in z = 0", new GeoLine3(P(-30, -5, 0), P(30, -5, 0)));
        }

        private static IEnumerable<(string Case, GeoRay3 Shape)> Rays(GeoPoint3 at)
        {
            yield return ("down through it", new GeoRay3(at + V(0, 0, 20), -Up));
            yield return ("leaving it upwards", new GeoRay3(at, Up));
            yield return ("leaving 0.4 above it upwards", new GeoRay3(at + V(0, 0, 0.4), Up));
            yield return ("leaving 50 above it upwards", new GeoRay3(at + V(0, 0, 50), Up));
            yield return ("down onto it from 50 above", new GeoRay3(at + V(0, 0, 50), -Up));
            yield return ("across it in z = 0", new GeoRay3(at + V(-5, -5, 0), V(1, 1, 0)));
            yield return ("along y = -5 in z = 0", new GeoRay3(P(-30, -5, 0), Across));
        }

        private static IEnumerable<(string Case, GeoPolyline3 Shape)> Polylines()
        {
            yield return ("down through the origin", new GeoPolyline3(P(10, 0, 20), P(0, 0, 20), P(0, 0, -25)));
            yield return ("ending on the origin", new GeoPolyline3(P(10, 0, 20), P(0, 0, 20), O));
            yield return ("ending 0.4 above the origin", new GeoPolyline3(P(10, 0, 20.4), P(0, 0, 20.4), P(0, 0, 0.4)));
            yield return ("50 above the origin", new GeoPolyline3(P(10, 0, 70), P(0, 0, 70), P(0, 0, 50)));
            yield return ("across the origin in z = 0", new GeoPolyline3(P(-5, -5, 0), P(5, 5, 0), P(5, 10, 0)));
            yield return ("5 below z = 0, inside the slab", new GeoPolyline3(P(-5, -5, -5), P(5, -5, -5), P(5, 5, -5)));
            yield return ("through z = 0 four times along y = -5", new GeoPolyline3(P(-15, -5, 5), P(-10, -5, -5), P(-5, -5, 5), P(0, -5, -5), P(5, -5, 5)));
            yield return ("down through the face's hole", new GeoPolyline3(P(12, -12, 20), P(12, -12, -20)));
            yield return ("down through (15, 0), on the disc's rim", new GeoPolyline3(P(15, 0, 20), P(15, 0, -20)));
            yield return ("ending 0.4 above (20, 0), on the square's rim and the slab's edge", new GeoPolyline3(P(20, 0, 20), P(20, 0, 0.4)));
            yield return ("ending 0.4 outside the disc's rim in z = 0", new GeoPolyline3(P(30, 0, 0), P(15.4, 0, 0)));
            yield return ("level, 0.4 above z = 0", new GeoPolyline3(P(-30, 3, 0.4), P(30, 3, 0.4)));
        }

        #endregion

        #region Shapes near the curved loop's rim at (0, -20, 0)

        /// <summary>
        /// The cases every shape near the loop is asked about. The loop measures a shape only in its own plane
        /// and refuses one out of it, so all but the last lie in z = 0: one across the rim's bottom side, one
        /// touching it at (0, -20) from outside, that one 0.4 and 50 further out, one inside the loop, and that
        /// one lifted 10 out of the plane, which both sides must refuse alike.
        /// </summary>
        private static IEnumerable<(string Case, T Shape)> NearTheRim<T>(Func<T, GeoVector3, T> move, T across, T touching, T inside)
        {
            yield return ("across the rim", across);
            yield return ("touching the rim from outside", touching);
            yield return ("0.4 outside the rim", move(touching, V(0, -0.4, 0)));
            yield return ("50 outside the rim", move(touching, V(0, -50, 0)));
            yield return ("inside the loop", inside);
            yield return ("lifted 10 out of the loop's plane", move(inside, V(0, 0, 10)));
        }

        /// <summary>
        /// Half circles in z = 0: from (0, -30) to (0, -10) bulging to (10, -20) on the rim's bottom side and so
        /// across it; one about (0, -30) whose middle touches it at (0, -20); and one about (0, -5) inside.
        /// </summary>
        private static IEnumerable<(string Case, GeoEdge3 Shape)> EdgesNearTheRim() => NearTheRim(
            (e, v) => e.Translate(v),
            new GeoEdge3(P(0, -30, 0), P(0, -10, 0), 1.0, Up),
            new GeoEdge3(P(10, -30, 0), P(-10, -30, 0), 1.0, Up),
            new GeoEdge3(P(5, -5, 0), P(-5, -5, 0), 1.0, Up));

        private static IEnumerable<(string Case, GeoArc3 Shape)> ArcsNearTheRim() => EdgesNearTheRim().Select(c => (c.Case, c.Shape.ToArc()));

        private static IEnumerable<(string Case, GeoCircle3 Shape)> CirclesNearTheRim() => NearTheRim(
            (c, v) => c.Translate(v),
            new GeoCircle3(Rim, Up, 5),
            new GeoCircle3(P(0, -25, 0), Up, 5),
            new GeoCircle3(O, Up, 5));

        private static IEnumerable<(string Case, GeoLine3 Shape)> LinesNearTheRim() => NearTheRim(
            (l, v) => l.Translate(v),
            new GeoLine3(P(0, -30, 0), P(0, -10, 0)),
            new GeoLine3(P(0, -30, 0), Rim),
            new GeoLine3(P(-5, -5, 0), P(5, 5, 0)));

        private static IEnumerable<(string Case, GeoPolyline3 Shape)> PolylinesNearTheRim() => NearTheRim(
            (p, v) => p.Translate(v),
            new GeoPolyline3(P(5, -35, 0), P(0, -30, 0), P(0, -10, 0)),
            new GeoPolyline3(P(5, -35, 0), P(0, -30, 0), Rim),
            new GeoPolyline3(P(-5, -5, 0), P(5, 5, 0), P(5, 10, 0)));

        private static IEnumerable<(string Case, GeoPolylineArc3 Shape)> ChainsNearTheRim() => NearTheRim(
            (c, v) => c.Translate(v),
            new GeoPolylineArc3(new[] { P(0, -30, 0), P(0, -10, 0), P(10, -10, 0) }, new[] { 0.0, 0.5 }, new[] { Up, Up }),
            new GeoPolylineArc3(new[] { P(10, -30, 0), P(0, -30, 0), Rim }, new[] { 0.5, 0.0 }, new[] { Up, Up }),
            new GeoPolylineArc3(new[] { P(-5, -5, 0), P(5, 5, 0), P(5, 10, 0) }, new[] { 0.0, 0.5 }, new[] { Up, Up }));

        /// <summary>A square 10 by 10 about the rim point; a diamond whose top corner touches it; a square inside.</summary>
        private static IEnumerable<(string Case, GeoPolygon3 Shape)> PolygonsNearTheRim() => NearTheRim(
            (p, v) => p.Translate(v),
            Level(-5, -25, 5, -15, 0),
            new GeoPolygon3(Rim, P(-5, -25, 0), P(0, -30, 0), P(5, -25, 0)),
            Level(-5, -5, 5, 5, 0));

        private static IEnumerable<(string Case, GeoFace3 Shape)> FacesNearTheRim() => PolygonsNearTheRim().Select(c => (c.Case, new GeoFace3(c.Shape)));

        #endregion
    }
}
