using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Common;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A face in the plane measured by its material: the outline with the holes taken out. It is nought from
    /// whatever collides with it, as <c>CollidesWith</c> already reads collision, and otherwise the gap between
    /// the boundaries; and two faces collide where their materials overlap or touch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The face is 100 by 60 with two holes: one from (10, 10) to (30, 50), and one from (60, 20) to (80, 40),
    /// so the material between them is a strip from x = 30 to x = 60. Each of the eleven shapes and the ellipse
    /// is put on that strip, 4 inside the first hole, touching that hole's rim from inside it, outside the
    /// outline, and round the whole face. A shape with an inside then holds the face and collides with it; a
    /// curve round it does not, and is the gap away. Every distance is worked by hand in its row.
    /// </para>
    /// <para>
    /// Each distance is held three ways: to the number in the row, within 1E-12 of a size of 200; to what the
    /// spec builds it from, <c>CollidesWith ? 0 : GetShortestLineTo(...).Length</c>, exactly; and to an oracle
    /// of its own, the nearest pair of points 0.25 apart along both boundaries, nought where a boolean of the
    /// face with the shape's region leaves area or, for a curve, a sample falls in the material by an even-odd
    /// count of the test's own.
    /// </para>
    /// </remarks>
    public class FaceMaterialTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>1E-12 of a size of 200: every shape lies within 150 of the origin.</summary>
        private const double Bound = 1E-12 * 200.0;

        private const double OnBound = 1E-9;

        private const double Spacing = 0.25;

        /// <summary>The gap from a ring of radius 70 about (50, 30) in to the corners of the face, 70 - sqrt(3400).</summary>
        private static readonly double RoundTheCorners = 70 - Math.Sqrt(3400);

        private static readonly Lazy<List<Row>> Table = new Lazy<List<Row>>(Build);

        private static readonly Lazy<List<FacePair>> Pairs = new Lazy<List<FacePair>>(BuildPairs);

        public static TheoryData<string> Rows() => Names(Table.Value.Select(r => r.Name));

        public static TheoryData<string> RowsWithAnOtherSide() => Names(Table.Value.Where(r => r.OtherDistance != null).Select(r => r.Name));

        public static TheoryData<string> FacePairs() => Names(Pairs.Value.Select(p => p.Name));

        public static TheoryData<string> PinnedFacePairs() => Names(Pairs.Value.Where(p => p.Start.HasValue).Select(p => p.Name));

        #region A face against each shape

        [Theory]
        [MemberData(nameof(Rows))]
        public void TheFaceIsNoughtFromWhatMeetsItsMaterialAndOtherwiseTheGapBetweenTheBoundaries(string name)
        {
            Row row = Table.Value.Single(r => r.Name == name);

            // The fixture first, through what the face already answers: whether it collides, and, where it
            // does not, the gap its shortest line spans.
            bool collides = row.Collides(Plate(), Within);
            Assert.Equal(row.Expected == 0.0, collides);

            if (!collides)
            {
                Assert.Equal(row.Expected, row.Line(Plate(), Within).Length, Bound);
            }

            double distance = row.Distance(Plate(), Within);

            Assert.Equal(row.Expected, distance, Bound);
            Assert.Equal(collides ? 0.0 : row.Line(Plate(), Within).Length, distance);

            // The holes given the other way round change nothing.
            Assert.Equal(distance, row.Distance(PlateHolesSwapped(), Within), Bound);

            // The form without a tolerance is the one under the global tolerance.
            Assert.Equal(row.Distance(Plate(), Tolerance.Global), row.PlainDistance(Plate()));
        }

        [Theory]
        [MemberData(nameof(RowsWithAnOtherSide))]
        public void EachShapeGivesTheFaceTheDistanceTheFaceGivesIt(string name)
        {
            Row row = Table.Value.Single(r => r.Name == name);

            if (row.Faces)
            {
                // Two faces: the same member asked the other way, the same distance to within rounding.
                Assert.Equal(row.Distance(Plate(), Within), row.OtherDistance(Plate(), Within), Bound);
                return;
            }

            Assert.Equal(row.Distance(Plate(), Within), row.OtherDistance(Plate(), Within));
            Assert.Equal(row.PlainDistance(Plate()), row.PlainOtherDistance(Plate()));
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void TheDistanceAgreesWithTheNearestDenseSamplesAndTheCollisionTheBooleansSee(string name)
        {
            Row row = Table.Value.Single(r => r.Name == name);
            GeoFace2 face = Plate();

            (bool meets, double nearest) = Oracle(face, row.Boundary(), row.Region(), Within);
            double distance = row.Distance(face, Within);

            if (meets)
            {
                Assert.Equal(0.0, distance);
            }
            else
            {
                Assert.InRange(distance, nearest - Spacing, nearest + Bound);
            }
        }

        #endregion

        #region A face against a face

        [Theory]
        [MemberData(nameof(FacePairs))]
        public void TwoFacesCollideWhereTheirMaterialsOverlapOrTouchAndNotAcrossAHole(string name)
        {
            FacePair pair = Pairs.Value.Single(p => p.Name == name);
            bool expected = pair.Expected == 0.0;

            Assert.Equal(expected, pair.First.CollidesWith(pair.Second, Within));
            Assert.Equal(expected, pair.Second.CollidesWith(pair.First, Within));
            Assert.Equal(pair.First.CollidesWith(pair.Second, Tolerance.Global), pair.First.CollidesWith(pair.Second));
        }

        [Theory]
        [MemberData(nameof(FacePairs))]
        public void TwoFacesAreNoughtApartWhereTheyCollideAndOtherwiseTheGapBetweenTheirRings(string name)
        {
            FacePair pair = Pairs.Value.Single(p => p.Name == name);

            double distance = pair.First.DistanceTo(pair.Second, Within);

            Assert.Equal(pair.Expected, distance, Bound);
            Assert.Equal(pair.Expected, pair.Second.DistanceTo(pair.First, Within), Bound);
            Assert.Equal(pair.First.CollidesWith(pair.Second, Within) ? 0.0 : pair.First.GetShortestLineTo(pair.Second, Within).Length, distance);

            (bool meets, double nearest) = Oracle(pair.First, Rings(pair.Second), pair.Second, Within);

            if (meets)
            {
                Assert.Equal(0.0, distance);
            }
            else
            {
                Assert.InRange(distance, nearest - Spacing, nearest + Bound);
            }
        }

        [Theory]
        [MemberData(nameof(FacePairs))]
        public void TheShortestLineBetweenTwoFacesRunsFromARingOfOneToARingOfTheOther(string name)
        {
            FacePair pair = Pairs.Value.Single(p => p.Name == name);
            GeoLine2 line = pair.First.GetShortestLineTo(pair.Second, Within);

            // As short as the nearest pair of rings, outline and holes of each, whichever face holds the other.
            double nearest = RingsOf(pair.First).SelectMany(r => RingsOf(pair.Second), (r, s) => r.GetShortestLineTo(s, Within).Length).Min();

            Assert.Equal(nearest, line.Length, Bound);
            Assert.InRange(RingsOf(pair.First).Min(r => r.GetClosestPointOnBoundary(line.StartPoint).DistanceTo(line.StartPoint)), 0.0, OnBound);
            Assert.InRange(RingsOf(pair.Second).Min(r => r.GetClosestPointOnBoundary(line.EndPoint).DistanceTo(line.EndPoint)), 0.0, OnBound);

            Assert.Equal(nearest, pair.Second.GetShortestLineTo(pair.First, Within).Length, Bound);
            MirrorTable.Same(pair.First.GetShortestLineTo(pair.Second, Tolerance.Global), pair.First.GetShortestLineTo(pair.Second));
        }

        [Theory]
        [MemberData(nameof(PinnedFacePairs))]
        public void ATieBetweenRingsGoesToTheFirstRingPair(string name)
        {
            FacePair pair = Pairs.Value.Single(p => p.Name == name);
            GeoLine2 line = pair.First.GetShortestLineTo(pair.Second, Within);

            Assert.InRange(pair.Start.Value.DistanceTo(line.StartPoint), 0.0, OnBound);
            Assert.InRange(pair.End.Value.DistanceTo(line.EndPoint), 0.0, OnBound);
        }

        [Theory]
        [MemberData(nameof(FacePairs))]
        public void TheCrossingsOfTwoFacesAreTheCrossingsOfTheirRingsInOrder(string name)
        {
            FacePair pair = Pairs.Value.Single(p => p.Name == name);

            // The outline and then the holes of the first, each against the outline and then the holes of the second.
            GeoPoint2[] expected = RingsOf(pair.First).SelectMany(r => RingsOf(pair.Second), (r, s) => r.GetIntersections(s, Within)).SelectMany(p => p).ToArray();
            GeoPoint2[] crossings = pair.First.GetIntersections(pair.Second, Within);

            Assert.Equal(pair.Crossings, crossings.Length);
            MirrorTable.Same(expected, crossings);
            MirrorTable.Same(pair.First.GetIntersections(pair.Second, Tolerance.Global), pair.First.GetIntersections(pair.Second));

            GeoPoint2[] back = RingsOf(pair.Second).SelectMany(r => RingsOf(pair.First), (r, s) => r.GetIntersections(s, Within)).SelectMany(p => p).ToArray();
            MirrorTable.Same(back, pair.Second.GetIntersections(pair.First, Within));
        }

        #endregion

        #region The table

        private static List<Row> Build()
        {
            var rows = new List<Row>();

            Add(rows, "GeoArc2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Curve(s.GetPointAtParameter, s.Length, false, true), s => null,
                Case("on the material", new GeoArc2(P(45, 30), 5, 0, Math.PI), 0),
                // The left half of a circle of radius 4 about (18, 30) reaches x = 14, 4 from the rim at x = 10.
                Case("in a hole", new GeoArc2(P(18, 30), 4, Math.PI / 2, 3 * Math.PI / 2), 4),
                Case("touching the hole's rim from inside it", new GeoArc2(P(14, 30), 4, Math.PI / 2, 3 * Math.PI / 2), 0),
                Case("outside the outline", new GeoArc2(P(115, 30), 5, Math.PI / 2, 3 * Math.PI / 2), 10),
                // Radius 70 about the middle of the face, 0 to 1.9 pi: past all four corners, sqrt(3400) out.
                Case("round the whole face", new GeoArc2(P(50, 30), 70, 0, 1.9 * Math.PI), RoundTheCorners));

            Add(rows, "GeoCircle2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Curve(d => P(s.Center.X + s.Radius * Math.Cos(d / s.Radius), s.Center.Y + s.Radius * Math.Sin(d / s.Radius)), s.Length, true, false), s => Polygon(Curve(d => P(s.Center.X + s.Radius * Math.Cos(d / s.Radius), s.Center.Y + s.Radius * Math.Sin(d / s.Radius)), s.Length, true, false)),
                Case("on the material", new GeoCircle2(P(45, 30), 5), 0),
                Case("in a hole", new GeoCircle2(P(18, 30), 4), 4),
                Case("touching the hole's rim from inside it", new GeoCircle2(P(14, 30), 4), 0),
                Case("outside the outline", new GeoCircle2(P(115, 30), 5), 10),
                Case("holding the whole face", new GeoCircle2(P(50, 30), 70), 0));

            Add(rows, "GeoEdge2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Curve(d => s.GetPointAtParameter(d / s.Length), s.Length, false, false), s => null,
                Case("on the material", new GeoEdge2(P(40, 30), P(50, 30)), 0),
                Case("in a hole", new GeoEdge2(P(14, 20), P(14, 40)), 4),
                // Turning clockwise from (14, 26) to (14, 34), so through (10, 30) on the rim.
                Case("touching the hole's rim from inside it", new GeoEdge2(P(14, 26), P(14, 34), -1), 0),
                Case("outside the outline", new GeoEdge2(P(110, 20), P(110, 40)), 10),
                // The top half of the ring of radius 70, past the two top corners.
                Case("round the top of the face", new GeoEdge2(P(120, 30), P(-20, 30), 1), RoundTheCorners));

            Add(rows, "GeoFace2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Rings(s), s => s,
                Case("on the material", new GeoFace2(Box(40, 25, 50, 35)), 0),
                Case("in a hole", new GeoFace2(Box(14, 14, 26, 46)), 4),
                Case("touching the hole's rim from inside it", new GeoFace2(Box(10, 20, 20, 30)), 0),
                Case("outside the outline", new GeoFace2(Box(110, 0, 130, 60)), 10),
                Case("holding the whole face", new GeoFace2(Box(-20, -20, 120, 80)), 0));

            Add(rows, "GeoLine2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Segments(false, s.StartPoint, s.EndPoint), s => null,
                Case("on the material", new GeoLine2(40, 30, 50, 30), 0),
                Case("in a hole", new GeoLine2(14, 20, 14, 40), 4),
                Case("touching the hole's rim from inside it", new GeoLine2(10, 30, 20, 30), 0),
                Case("outside the outline", new GeoLine2(110, 20, 110, 40), 10),
                Case("running past the whole face", new GeoLine2(-50, -10, 150, -10), 10));

            Add(rows, "GeoPolygon2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Segments(true, s.Vertices.ToArray()), s => s,
                Case("on the material", Box(40, 25, 50, 35), 0),
                Case("in a hole", Box(14, 14, 26, 46), 4),
                Case("touching the hole's rim from inside it", Box(10, 20, 20, 30), 0),
                Case("outside the outline", Box(110, 0, 130, 60), 10),
                Case("holding the whole face", Box(-10, -10, 110, 70), 0));

            Add(rows, "GeoPolygonArc2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Curve(s.GetPointAtDistance, s.Length, true, false), s => Polygon(Curve(s.GetPointAtDistance, s.Length, true, false)),
                Case("on the material", Loop(Corners(40, 25, 50, 35), 0, 0, 0.2, 0), 0),
                // The left side, from (14, 46) down to (14, 14), bowed out by 32 / 2 * 0.125 = 2 to x = 12.
                Case("in a hole", Loop(Corners(14, 14, 26, 46), 0, 0, 0, 0.125), 2),
                Case("touching the hole's rim from inside it", Loop(Corners(10, 20, 20, 30), 0, 0.3, 0, 0), 0),
                // The left side, from (110, 60) down to (110, 0), bowed out by 60 / 2 * 0.2 = 6 to x = 104.
                Case("outside the outline", Loop(Corners(110, 0, 130, 60), 0, 0, 0, 0.2), 4),
                Case("holding the whole face", Loop(Corners(-10, -10, 110, 70), 0.1, 0, 0, 0), 0));

            Add(rows, "GeoPolyline2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Segments(false, s.Vertices.ToArray()), s => null,
                Case("on the material", new GeoPolyline2(P(40, 30), P(45, 32), P(50, 30)), 0),
                Case("in a hole", new GeoPolyline2(P(14, 20), P(14, 40), P(20, 40)), 4),
                Case("touching the hole's rim from inside it", new GeoPolyline2(P(20, 30), P(10, 30), P(15, 40)), 0),
                Case("outside the outline", new GeoPolyline2(P(110, 20), P(110, 40), P(120, 40)), 10),
                Case("round the whole face", new GeoPolyline2(P(-10, -10), P(110, -10), P(110, 70), P(-10, 70), P(-10, -10)), 10));

            Add(rows, "GeoPolylineArc2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Curve(s.GetPointAtDistance, s.Length, false, false), s => null,
                Case("on the material", Chain(new[] { P(40, 30), P(50, 30) }, 0.2), 0),
                // Down from (14, 40) to (14, 20) turning counter-clockwise, bowed out by 20 / 2 * 0.2 = 2 to x = 12.
                Case("in a hole", Chain(new[] { P(14, 40), P(14, 20) }, 0.2), 2),
                // Up from (14, 26) to (14, 34) turning clockwise, a half circle through (10, 30).
                Case("touching the hole's rim from inside it", Chain(new[] { P(14, 26), P(14, 34) }, -1), 0),
                // Down from (110, 40) to (110, 20), bowed out by 20 / 2 * 0.4 = 4 to x = 106.
                Case("outside the outline", Chain(new[] { P(110, 40), P(110, 20) }, 0.4), 6),
                Case("round the whole face", Chain(new[] { P(120, 30), P(-20, 30), P(120, 30) }, 1, 1), RoundTheCorners));

            Add(rows, "GeoRectangle2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Segments(true, s.ToPolygon().Vertices.ToArray()), s => s.ToPolygon(),
                Case("on the material", new GeoRectangle2(40, 25, 10, 10), 0),
                Case("in a hole", new GeoRectangle2(14, 14, 12, 32), 4),
                Case("touching the hole's rim from inside it", new GeoRectangle2(10, 20, 10, 10), 0),
                Case("outside the outline", new GeoRectangle2(110, 0, 20, 60), 10),
                Case("holding the whole face", new GeoRectangle2(-10, -10, 120, 80), 0));

            Add(rows, "GeoTriangle2", (f, s, t) => f.DistanceTo(s, t), f => s => f.DistanceTo(s), (s, f, t) => s.DistanceTo(f, t), s => f => s.DistanceTo(f), (f, s, t) => f.CollidesWith(s, t), (f, s, t) => f.GetShortestLineTo(s, t), s => Segments(true, s.A, s.B, s.C), s => s.ToPolygon(),
                Case("on the material", new GeoTriangle2(P(40, 25), P(50, 25), P(45, 35)), 0),
                Case("in a hole", new GeoTriangle2(P(14, 14), P(26, 14), P(14, 46)), 4),
                Case("touching the hole's rim from inside it", new GeoTriangle2(P(10, 20), P(20, 25), P(10, 30)), 0),
                Case("outside the outline", new GeoTriangle2(P(110, 0), P(130, 30), P(110, 60)), 10),
                // Its sides pass at least 5 outside each corner of the face.
                Case("holding the whole face", new GeoTriangle2(P(-50, -10), P(150, -10), P(50, 150)), 0));

            // The ellipse is asked: nothing goes on the face for it.
            Add(rows, "GeoEllipse2", (f, s, t) => s.DistanceTo(f, t), f => s => s.DistanceTo(f), null, null, (f, s, t) => s.CollidesWith(f, t), (f, s, t) => s.GetShortestLineTo(f, t).Reverse(), s => Curve(s.GetPointAtDistance, s.Length, true, false), s => Polygon(Curve(s.GetPointAtDistance, s.Length, true, false)),
                Case("on the material", new GeoEllipse2(P(45, 30), new GeoVector2(1, 0), 4, 2), 0),
                // Major radius 6 upright, minor 4 across: from x = 14 to 22, 4 from the rim at x = 10.
                Case("in a hole", new GeoEllipse2(P(18, 30), new GeoVector2(0, 1), 6, 4), 4),
                Case("touching the hole's rim from inside it", new GeoEllipse2(P(14, 30), new GeoVector2(0, 1), 6, 4), 0),
                Case("outside the outline", new GeoEllipse2(P(115, 30), new GeoVector2(1, 0), 5, 3), 10),
                // (0, 0) is 0.39 + 0.36 = 0.75 of the way out, so every corner is inside.
                Case("holding the whole face", new GeoEllipse2(P(50, 30), new GeoVector2(1, 0), 80, 50), 0));

            return rows;
        }

        private static List<FacePair> BuildPairs()
        {
            GeoFace2 plate = Plate();

            return new List<FacePair>
            {
                // Across the top right corner: the outline meets the square at (100, 50) and (90, 60).
                Pair("overlapping a corner", plate, new GeoFace2(Box(90, 50, 120, 80)), 0, 2),
                Pair("in a hole of the first", plate, new GeoFace2(Box(14, 14, 26, 46)), 4, 0),
                // The plate lies in the hole of a frame, 10 clear of it all round.
                Pair("the first in the hole of the second", plate, Frame(-20, -20, 120, 80, -10, -10, 110, 70), 10, 0),
                // 10 from the first hole's rim at x = 30 and 10 from the second's at x = 60; 25 from the outline.
                // The first hole is the earlier ring, so the line runs from (30, 30), the middle of the run.
                Pinned(Pair("on the material of the first, as near two holes", plate, new GeoFace2(Box(40, 25, 50, 35)), 0, 0), P(30, 30), P(40, 30)),
                Pair("the first on the material of the second", plate, new GeoFace2(Box(-20, -20, 120, 80)), 0, 0),
                Pair("touching the outline", plate, new GeoFace2(Box(100, 20, 120, 40)), 0, 2),
                Pair("touching a hole's rim from inside it", plate, new GeoFace2(Box(10, 20, 20, 30)), 0, 2),
                // Across the first hole's rim at x = 30, from (30, 25) to (30, 35).
                Pair("across a hole's rim", plate, new GeoFace2(Box(25, 25, 35, 35)), 0, 2),
                Pair("apart", plate, new GeoFace2(Box(110, 0, 130, 60)), 10, 0),
                // A frame whose hole, from (5, 5) to (95, 55), lies inside the plate's outline: no ring crosses
                // another, yet the two materials share the band from 0 to 5 all round.
                Pair("frames sharing a band with no ring crossing", plate, Frame(-10, -10, 110, 70, 5, 5, 95, 55), 0, 0),
                Pair("the first in the hole of a frame, 5 clear", plate, Frame(-10, -10, 110, 70, -5, -5, 105, 65), 5, 0),
                // A frame round the strip between the holes, its own hole 5 wide.
                Pair("a frame on the material", plate, Frame(35, 20, 55, 40, 40, 25, 50, 35), 0, 0),
                // A frame from (-6, 15) to (16, 45) with a hole from (-3, 20) to (13, 40): each of its rings
                // crosses the outline at x = 0 and the first hole's rim at x = 10, twice each, so the eight
                // crossings come from four pairs of rings and their order shows which ring was walked first.
                Pair("a frame across the outline and a hole, its own hole across both", plate, Frame(-6, 15, 16, 45, -3, 20, 13, 40), 0, 8)
            };
        }

        #endregion

        #region Rows

        private sealed class Row
        {
            internal string Name;
            internal bool Faces;
            internal double Expected;
            internal Func<GeoFace2, Tolerance, double> Distance;
            internal Func<GeoFace2, double> PlainDistance;
            internal Func<GeoFace2, Tolerance, double> OtherDistance;
            internal Func<GeoFace2, double> PlainOtherDistance;
            internal Func<GeoFace2, Tolerance, bool> Collides;
            internal Func<GeoFace2, Tolerance, GeoLine2> Line;
            internal Func<GeoPoint2[]> Boundary;
            internal Func<object> Region;
        }

        private sealed class FacePair
        {
            internal string Name;
            internal GeoFace2 First;
            internal GeoFace2 Second;
            internal double Expected;
            internal int Crossings;
            internal GeoPoint2? Start;
            internal GeoPoint2? End;
        }

        private static (string Name, T Shape, double Expected) Case<T>(string name, T shape, double expected) => (name, shape, expected);

        private static void Add<T>(
            List<Row> rows,
            string type,
            Func<GeoFace2, T, Tolerance, double> distance,
            Func<GeoFace2, Func<T, double>> plainDistance,
            Func<T, GeoFace2, Tolerance, double> otherDistance,
            Func<T, Func<GeoFace2, double>> plainOtherDistance,
            Func<GeoFace2, T, Tolerance, bool> collides,
            Func<GeoFace2, T, Tolerance, GeoLine2> line,
            Func<T, GeoPoint2[]> boundary,
            Func<T, object> region,
            params (string Name, T Shape, double Expected)[] cases)
        {
            foreach ((string Name, T Shape, double Expected) c in cases)
            {
                rows.Add(new Row
                {
                    Name = type + ": " + c.Name,
                    Faces = typeof(T) == typeof(GeoFace2),
                    Expected = c.Expected,
                    Distance = (f, t) => distance(f, c.Shape, t),
                    PlainDistance = f => plainDistance(f)(c.Shape),
                    OtherDistance = otherDistance == null ? null : (Func<GeoFace2, Tolerance, double>)((f, t) => otherDistance(c.Shape, f, t)),
                    PlainOtherDistance = plainOtherDistance == null ? null : (Func<GeoFace2, double>)(f => plainOtherDistance(c.Shape)(f)),
                    Collides = (f, t) => collides(f, c.Shape, t),
                    Line = (f, t) => line(f, c.Shape, t),
                    Boundary = () => boundary(c.Shape),
                    Region = () => region(c.Shape)
                });
            }
        }

        private static FacePair Pair(string name, GeoFace2 first, GeoFace2 second, double expected, int crossings)
            => new FacePair { Name = "GeoFace2-GeoFace2: " + name, First = first, Second = second, Expected = expected, Crossings = crossings };

        private static FacePair Pinned(FacePair pair, GeoPoint2 start, GeoPoint2 end)
        {
            pair.Start = start;
            pair.End = end;
            return pair;
        }

        private static TheoryData<string> Names(IEnumerable<string> names)
        {
            var data = new TheoryData<string>();

            foreach (string name in names)
            {
                data.Add(name);
            }

            return data;
        }

        #endregion

        #region The oracle

        /// <summary>
        /// Whether a shape meets the material of a face, and the nearest pair of samples of their boundaries.
        /// A shape with a region meets the material where a boolean of the two leaves area; a curve meets it
        /// where one of its samples falls in the material by an even-odd count over every ring.
        /// </summary>
        private static (bool Meets, double Nearest) Oracle(GeoFace2 face, GeoPoint2[] boundary, object region, Tolerance tolerance)
        {
            GeoPoint2[] rim = Rings(face);
            double nearest = double.MaxValue;

            foreach (GeoPoint2 p in rim)
            {
                foreach (GeoPoint2 q in boundary)
                {
                    double dx = p.X - q.X, dy = p.Y - q.Y;
                    nearest = Math.Min(nearest, dx * dx + dy * dy);
                }
            }

            bool meets;

            switch (region)
            {
                case GeoFace2 other:
                    meets = Boolean2.Intersect(face, other, tolerance).Sum(f => f.Area) > 1E-6;
                    break;
                case GeoPolygon2 polygon:
                    meets = Boolean2.Intersect(face, new GeoFace2(polygon), tolerance).Sum(f => f.Area) > 1E-6;
                    break;
                default:
                    meets = boundary.Any(q => InMaterial(face, q));
                    break;
            }

            return (meets, Math.Sqrt(nearest));
        }

        private static bool InMaterial(GeoFace2 face, GeoPoint2 point)
        {
            return RingsOf(face).Count(ring => InsideByCount(ring.Vertices, point)) % 2 == 1;
        }

        private static bool InsideByCount(IReadOnlyList<GeoPoint2> ring, GeoPoint2 point)
        {
            bool inside = false;

            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                if ((ring[i].Y > point.Y) != (ring[j].Y > point.Y)
                    && point.X < (ring[j].X - ring[i].X) * (point.Y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Points along a curve given by the point at a distance along it, at most <see cref="Spacing"/> apart.</summary>
        private static GeoPoint2[] Curve(Func<double, GeoPoint2> at, double length, bool closed, bool byParameter)
        {
            int n = (int)Math.Ceiling(length / Spacing);
            int last = closed ? n - 1 : n;
            var points = new GeoPoint2[last + 1];

            for (int i = 0; i <= last; i++)
            {
                points[i] = byParameter ? at((double)i / n) : at(length * i / n);
            }

            return points;
        }

        /// <summary>Points along straight segments through the corners given, closed back to the first where asked.</summary>
        private static GeoPoint2[] Segments(bool closed, params GeoPoint2[] corners)
        {
            var points = new List<GeoPoint2>();
            int edges = closed ? corners.Length : corners.Length - 1;

            for (int i = 0; i < edges; i++)
            {
                GeoPoint2 s = corners[i], e = corners[(i + 1) % corners.Length];
                int n = Math.Max(1, (int)Math.Ceiling(s.DistanceTo(e) / Spacing));

                for (int k = 0; k < n; k++)
                {
                    points.Add(P(s.X + (e.X - s.X) * k / n, s.Y + (e.Y - s.Y) * k / n));
                }
            }

            if (!closed)
            {
                points.Add(corners[corners.Length - 1]);
            }

            return points.ToArray();
        }

        private static GeoPolygon2 Polygon(GeoPoint2[] ring) => new GeoPolygon2(ring);

        private static IEnumerable<GeoPolygon2> RingsOf(GeoFace2 face) => new[] { face.Boundary }.Concat(face.Holes);

        private static GeoPoint2[] Rings(GeoFace2 face) => RingsOf(face).SelectMany(r => Segments(true, r.Vertices.ToArray())).ToArray();

        #endregion

        #region Shapes

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPoint2[] Corners(double left, double bottom, double right, double top)
            => new[] { P(left, bottom), P(right, bottom), P(right, top), P(left, top) };

        private static GeoPolygon2 Box(double left, double bottom, double right, double top) => new GeoPolygon2(Corners(left, bottom, right, top));

        private static GeoFace2 Frame(double left, double bottom, double right, double top, double holeLeft, double holeBottom, double holeRight, double holeTop)
            => new GeoFace2(Box(left, bottom, right, top), new[] { Box(holeLeft, holeBottom, holeRight, holeTop) });

        /// <summary>A loop round the corners given, counter-clockwise, with a bulge on each side in turn.</summary>
        private static GeoPolygonArc2 Loop(GeoPoint2[] corners, params double[] bulges) => new GeoPolygonArc2(corners, bulges);

        private static GeoPolylineArc2 Chain(GeoPoint2[] vertices, params double[] bulges) => new GeoPolylineArc2(vertices, bulges);

        /// <summary>The face: 100 by 60, a hole from (10, 10) to (30, 50) and a hole from (60, 20) to (80, 40).</summary>
        private static GeoFace2 Plate() => new GeoFace2(Box(0, 0, 100, 60), new[] { Box(10, 10, 30, 50), Box(60, 20, 80, 40) });

        private static GeoFace2 PlateHolesSwapped() => new GeoFace2(Box(0, 0, 100, 60), new[] { Box(60, 20, 80, 40), Box(10, 10, 30, 50) });

        #endregion
    }
}
