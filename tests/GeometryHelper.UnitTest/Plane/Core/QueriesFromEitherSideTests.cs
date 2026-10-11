using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Common;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The face, the rectangle and the triangle could each say where they meet an arc, a circle, an edge, a
    /// segment, a polygon, a curved loop, a polyline or a curved chain, and none of those could say it back.
    /// The twenty-four <c>TryIntersectWith</c> added on the other side are one line each, asking the region,
    /// so each is held to the region's answer exactly: the same bool, and the same points in the same order.
    /// </summary>
    /// <remarks>
    /// The cases are a table, not written out pair by pair. The three regions share their bottom edge, y = 0
    /// from x = 0 to 100, and hold the square from (20, 5) to (40, 25): the face is the rectangle 100 by 60
    /// with a hole from (60, 10) to (90, 40), and the triangle runs (0, 0), (100, 0), (0, 100). Every curve
    /// is drawn once about the origin, crossing y = 0 there, resting on it from below, or small enough to
    /// sit inside, and moved into place. The gap of 0.4 is clear of both tolerances, a thousandth and 1, so
    /// a member handing the wrong one on answers differently.
    /// </remarks>
    public class QueriesFromEitherSideTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static readonly MirrorTable Table = Build();

        public static TheoryData<string> Rows() => Table.Names();

        public static TheoryData<string> Members() => Table.Members();

        [Theory]
        [MemberData(nameof(Rows))]
        public void AShapeAsksTheRegionWhereTheyMeetAndGivesItsAnswerExactly(string row)
        {
            Table.Check(row);
        }

        [Theory]
        [MemberData(nameof(Members))]
        public void TheCasesOfEachMemberDoNotAllGiveTheRegionTheSameAnswer(string member)
        {
            // A member answering the same whatever it is asked must not pass for one that asks.
            Assert.InRange(Table.DistinctAnswers(member), 2, int.MaxValue);
        }

        private static MirrorTable Build()
        {
            var table = new MirrorTable(Strict, Loose);

            Try(table, Arcs(), Face(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Circles(), Face(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Edges(), Face(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Lines(), Face(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Polygons(), Face(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Loops(), Face(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Polylines(), Face(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Chains(), Face(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));

            Try(table, Arcs(), Rectangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Circles(), Rectangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Edges(), Rectangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Lines(), Rectangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Polygons(), Rectangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Loops(), Rectangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Polylines(), Rectangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Chains(), Rectangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));

            Try(table, Arcs(), Triangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Circles(), Triangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Edges(), Triangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Lines(), Triangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Polygons(), Triangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Loops(), Triangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Polylines(), Triangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));
            Try(table, Chains(), Triangle(), (a, b) => (a.TryIntersectWith(b, out GeoPoint2[] p), p), (a, b, t) => (a.TryIntersectWith(b, out GeoPoint2[] p, t), p), (a, b) => (b.TryIntersectWith(a, out GeoPoint2[] p), p), (a, b, t) => (b.TryIntersectWith(a, out GeoPoint2[] p, t), p));

            return table;
        }

        private static void Try<TA, TB>(
            MirrorTable table,
            IEnumerable<(string Case, TA Shape)> asked,
            TB region,
            Func<TA, TB, (bool, GeoPoint2[])> mine,
            Func<TA, TB, Tolerance, (bool, GeoPoint2[])> mineWithin,
            Func<TA, TB, (bool, GeoPoint2[])> theirs,
            Func<TA, TB, Tolerance, (bool, GeoPoint2[])> theirsWithin)
        {
            table.Add(
                typeof(TA).Name + ".TryIntersectWith(" + typeof(TB).Name + ")",
                asked.Select(c => (c.Case, c.Shape, region)),
                mine,
                mineWithin,
                theirs,
                theirsWithin,
                MirrorTable.Same);
        }

        #region Regions sharing the bottom edge y = 0, x from 0 to 100

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Box(double x0, double y0, double x1, double y1) => new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        private static GeoFace2 Face() => new GeoFace2(Box(0, 0, 100, 60), new[] { Box(60, 10, 90, 40) });

        private static GeoRectangle2 Rectangle() => new GeoRectangle2(P(50, 30), 100, 60);

        private static GeoTriangle2 Triangle() => new GeoTriangle2(P(0, 0), P(100, 0), P(0, 100));

        #endregion

        #region Curves about the origin, moved against the regions

        /// <summary>
        /// The cases every curve is asked about: one drawn crossing y = 0 at the origin moved to (30, 0); one
        /// resting on y = 0 from below at the origin moved there, 0.4 below and 50 below; and a small one
        /// moved to (30, 15), inside all three, and to (73, 24), in the face's hole and across the long side
        /// of the triangle.
        /// </summary>
        private static IEnumerable<(string Case, T Shape)> Cases<T>(Func<T, GeoVector2, T> move, T crossing, T resting, T small, params (string Case, T Shape)[] more)
        {
            yield return ("crossing the bottom", move(crossing, new GeoVector2(30, 0)));
            yield return ("touching the bottom from below", move(resting, new GeoVector2(30, 0)));
            yield return ("0.4 below the bottom", move(resting, new GeoVector2(30, -0.4)));
            yield return ("50 below the bottom", move(resting, new GeoVector2(30, -50)));
            yield return ("inside all three", move(small, new GeoVector2(30, 15)));
            yield return ("in the face's hole and across the triangle's long side", move(small, new GeoVector2(73, 24)));

            foreach ((string Case, T Shape) c in more)
            {
                yield return c;
            }
        }

        private static IEnumerable<(string Case, GeoArc2 Shape)> Arcs() => Cases(
            (a, v) => a.Translate(v),
            new GeoArc2(P(0, -5), 10, 0, Math.PI),
            new GeoArc2(P(0, -10), 10, Math.PI / 4, 3 * Math.PI / 4),
            new GeoArc2(P(0, 0), 5, 0, Math.PI),
            // Rising from (-38, -12) to 25 above (50, 0) and down again: in through the left side, through
            // the hole, out through the right side or the long side.
            ("across, out of both sides", new GeoArc2(P(50, -100), 125, Math.PI / 4, 3 * Math.PI / 4)));

        private static IEnumerable<(string Case, GeoCircle2 Shape)> Circles() => Cases(
            (c, v) => c.Translate(v),
            new GeoCircle2(P(0, 0), 10),
            new GeoCircle2(P(0, -10), 10),
            new GeoCircle2(P(0, 0), 5),
            ("around all three", new GeoCircle2(P(50, 30), 200)));

        private static IEnumerable<(string Case, GeoEdge2 Shape)> Edges() => Cases(
            (e, v) => e.Translate(v),
            new GeoEdge2(new GeoArc2(P(0, -5), 10, 0, Math.PI)),
            new GeoEdge2(new GeoArc2(P(0, -10), 10, Math.PI / 4, 3 * Math.PI / 4)),
            new GeoEdge2(P(-10, -5), P(10, 5)),
            ("straight up through the bottom", new GeoEdge2(P(30, -20), P(30, 20))),
            ("straight across, out of both sides", new GeoEdge2(P(-10, 20), P(110, 20))));

        private static IEnumerable<(string Case, GeoLine2 Shape)> Lines() => Cases(
            (l, v) => l.Translate(v),
            new GeoLine2(P(0, -20), P(0, 20)),
            new GeoLine2(P(0, -20), P(0, 0)),
            new GeoLine2(P(-10, -5), P(10, 5)),
            ("across, out of both sides and through the hole", new GeoLine2(P(-10, 20), P(110, 20))));

        private static IEnumerable<(string Case, GeoPolygon2 Shape)> Polygons() => Cases(
            (p, v) => p.Translate(v),
            Box(-10, -10, 10, 10),
            new GeoPolygon2(P(0, 0), P(-10, -10), P(0, -20), P(10, -10)),
            Box(-5, -5, 5, 5),
            ("around all three", Box(-50, -50, 200, 200)));

        private static IEnumerable<(string Case, GeoPolygonArc2 Shape)> Loops() => Cases(
            (l, v) => l.Translate(v),
            new GeoPolygonArc2(new[] { P(-10, -10), P(10, -10), P(10, 10), P(-10, 10) }, new[] { 0.0, 0.5, 0.0, 0.0 }),
            new GeoPolygonArc2(new[] { P(0, 0), P(-10, -10), P(0, -20), P(10, -10) }, new[] { 0.0, 0.0, 0.3, 0.0 }),
            new GeoPolygonArc2(new[] { P(-5, -5), P(5, -5), P(5, 5), P(-5, 5) }, new[] { 0.3, 0.0, 0.0, 0.0 }),
            ("around all three", new GeoPolygonArc2(new[] { P(-50, -50), P(200, -50), P(200, 200), P(-50, 200) }, new[] { 0.0, 0.2, 0.0, 0.0 })));

        private static IEnumerable<(string Case, GeoPolyline2 Shape)> Polylines() => Cases(
            (p, v) => p.Translate(v),
            // Up and down through the bottom three times, at x = 15, 25 and 35 once moved.
            new GeoPolyline2(P(-20, -10), P(-10, 10), P(0, -10), P(10, 10)),
            new GeoPolyline2(P(-20, -20), P(0, 0), P(20, -20)),
            new GeoPolyline2(P(-10, -5), P(0, 5), P(10, -5)),
            ("across, out of both sides and through the hole", new GeoPolyline2(P(-10, 20), P(110, 20), P(110, -30))));

        private static IEnumerable<(string Case, GeoPolylineArc2 Shape)> Chains() => Cases(
            (c, v) => c.Translate(v),
            new GeoPolylineArc2(new[] { P(-20, -10), P(0, 10), P(20, -10) }, new[] { 0.4, -0.4 }),
            new GeoPolylineArc2(new[] { P(-20, -20), P(0, 0), P(20, -20), P(40, -20) }, new[] { 0.0, 0.0, 1.0 }),
            new GeoPolylineArc2(new[] { P(-10, -5), P(0, 5), P(10, -5) }, new[] { 0.3, 0.0 }),
            ("across, out of both sides and through the hole", new GeoPolylineArc2(new[] { P(-10, 20), P(110, 20), P(110, -30) }, new[] { 0.1, 0.0 })));

        #endregion
    }
}
