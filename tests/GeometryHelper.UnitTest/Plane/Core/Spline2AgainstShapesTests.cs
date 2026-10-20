using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.Ray2Shapes;
using static GeometryHelper.UnitTest.Plane.XLine2Probe;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A spline in the plane asked the five queries about each of the shapes it meets, from either side, with the numbers worked
    /// out by hand: a spline through the shape, one beside it, one along a side or touching an end of it, one a thousandth off,
    /// and where the shape is closed, one inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three splines carry the cases. The ring is the circle of radius 50 about the origin as <see cref="GeoSpline2.FromCircle"/>
    /// makes it, from (50, 0) counter-clockwise, so that its crossings come in the order of the angle. The arch is the cubic with
    /// the control points (0, 0), (100, 150), (200, 150), (300, 0), which is the parabola y = x (300 - x) / 200 with its top at
    /// (150, 112.5). The corner is the polyline (0, 0), (100, 0), (100, 50) as a spline of degree one.
    /// </para>
    /// <para>
    /// A pair is held to what the other shapes are held to (rule R): a closed shape is filled for the distance and the collision, so
    /// a spline inside one is nought from it; the crossings are those of the boundary, in order along the spline from its start; the
    /// shortest segment runs from the spline to the boundary, so it is of the length of the gap out to the boundary where the
    /// spline lies inside; a collision is a distance of no more than the point tolerance, exactly. Asked from either side the two say
    /// the same to the bit: the same distance, the same collision, the same crossings in the same order, and a shortest segment
    /// turned round. Each form without a tolerance answers, to the bit, what the form with <see cref="Tolerance.Global"/> answers.
    /// Where a member is missing the case fails with its name, since the members are found by reflection.
    /// </para>
    /// <para>
    /// Half a thousandth and two thousandths sit either side of the point tolerance, a thousandth, a factor of two clear of it.
    /// </para>
    /// </remarks>
    public class Spline2AgainstShapesTests
    {
        private static readonly List<string> Names = new List<string>();

        private static readonly Dictionary<string, Action> Checks = Build();

        public static TheoryData<string> Rows()
        {
            var data = new TheoryData<string>();

            foreach (string name in Names)
            {
                data.Add(name);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void TheSplineAndTheShape_AgreeFromEitherSide_OnTheNumbersWorkedOutByHand(string row)
        {
            Checks[row]();
        }

        #region Fixtures

        /// <summary>The circle of radius 50 about the origin, from (50, 0) counter-clockwise: 0.25 of the domain is (0, 50).</summary>
        private static GeoSpline2 Ring() => GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 50));

        /// <summary>The parabola y = x (300 - x) / 200 from (0, 0) to (300, 0), a cubic: x = 300 t, y = 450 t (1 - t).</summary>
        private static GeoSpline2 Arch() => new GeoSpline2(3, new[] { P(0, 0), P(100, 150), P(200, 150), P(300, 0) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

        /// <summary>The polyline (0, 0), (100, 0), (100, 50) as a spline of degree one.</summary>
        private static GeoSpline2 Corner() => GeoSpline2.FromPolyline(new GeoPolyline2(P(0, 0), P(100, 0), P(100, 50)));

        private static GeoSpline2 Round(double x, double y, double r) => GeoSpline2.FromCircle(new GeoCircle2(P(x, y), r));

        private static GeoPoint2[] Pts(params double[] xy)
        {
            var points = new GeoPoint2[xy.Length / 2];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = P(xy[2 * i], xy[2 * i + 1]);
            }

            return points;
        }

        private static double Root(double value) => Math.Sqrt(value);

        #endregion

        #region Judging

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static void AssertSamePoints(GeoPoint2[] expected, GeoPoint2[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " points " + string.Join(" ", actual) + ", " + expected.Length + " expected");

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i].DistanceTo(actual[i]) <= 1E-6, what + ": point " + i + " is " + actual[i] + ", " + expected[i] + " expected");
            }
        }

        private static void AssertSameBits(GeoPoint2[] expected, GeoPoint2[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " points and " + expected.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(Bits(expected[i].X) == Bits(actual[i].X) && Bits(expected[i].Y) == Bits(actual[i].Y), what + ": point " + i + " " + expected[i] + " and " + actual[i]);
            }
        }

        private static void Verify(string name, GeoSpline2 spline, object shape, double distance, GeoPoint2[] crossings, double? shortest)
        {
            bool isPoint = shape is GeoPoint2;

            // A question the spline asks of the shape, the shape asks of the spline, and the other way about, with and without a tolerance.
            foreach (string query in new[] { "DistanceTo", "CollidesWith", "GetIntersections", "GetShortestLineTo" })
            {
                Assert.True(Answers(spline, query, shape), name + ": the spline does not answer " + query);
                Assert.True(Answers(shape, query, spline), name + ": the shape does not answer " + query);
            }

            Assert.True(HasTry(spline, shape) && HasTry(shape, spline), name + ": TryIntersectWith is missing from one side");

            // Distance: as worked out, the same from both sides to the bit, and the plain form is the global one.
            double fromSpline = Distance(spline, shape, Strict);
            double fromShape = Distance(shape, spline, Strict);

            Assert.InRange(fromSpline, distance - 1E-9, distance + 1E-9);
            Assert.Equal(Bits(fromSpline), Bits(fromShape));
            Assert.Equal(Bits(Distance(spline, shape, Tolerance.Global)), Bits(Distance(spline, shape)));
            Assert.Equal(Bits(Distance(shape, spline, Tolerance.Global)), Bits(Distance(shape, spline)));

            // Collision: the distance being within the point tolerance, exactly, the same from both sides.
            bool collides = fromSpline <= Strict.EqualPoint;

            Assert.Equal(distance <= Strict.EqualPoint, collides);
            Assert.Equal(collides, Collides(spline, shape, Strict));
            Assert.Equal(collides, Collides(shape, spline, Strict));
            Assert.Equal(Collides(spline, shape, Tolerance.Global), Collides(spline, shape));
            Assert.Equal(Collides(shape, spline, Tolerance.Global), Collides(shape, spline));

            // Crossings: as worked out, the same points in the same order from both sides, the plain form the global one.
            GeoPoint2[] fromSplineSide = Crossings(spline, shape, Strict);
            GeoPoint2[] fromShapeSide = Crossings(shape, spline, Strict);

            if (crossings != null)
            {
                AssertSamePoints(crossings, fromSplineSide, name + " from the spline");
            }

            AssertSameBits(fromSplineSide, fromShapeSide, name + " both sides");
            AssertSameBits(Crossings(spline, shape, Tolerance.Global), Crossings(spline, shape), name + " plain form");
            AssertSameBits(Crossings(shape, spline, Tolerance.Global), Crossings(shape, spline), name + " plain form, the shape's");

            // TryIntersectWith says whether there are any, and gives them.
            bool tried = Try(spline, shape, out GeoPoint2[] points, Strict);
            bool triedBack = Try(shape, spline, out GeoPoint2[] pointsBack, Strict);

            Assert.Equal(fromSplineSide.Length > 0, tried);
            Assert.Equal(fromSplineSide.Length > 0, triedBack);
            AssertSameBits(fromSplineSide, points, name + " Try");
            AssertSameBits(fromSplineSide, pointsBack, name + " Try from the shape");
            Assert.Equal(tried, Try(spline, shape, out GeoPoint2[] plain));
            AssertSameBits(points, plain, name + " Try, plain form");

            // Shortest segment: leaves the spline and lands on the shape, as long as the distance (or as the gap to the boundary
            // for a spline inside a closed shape); the shape gives the same segment turned round, to the bit.
            GeoLine2 leaving = Shortest(spline, shape, Strict);
            GeoLine2 landing = Shortest(shape, spline, Strict);
            double length = shortest ?? distance;

            Assert.InRange(leaving.Length, length - 1E-9, length + 1E-9);
            Assert.True(Bits(leaving.StartPoint.X) == Bits(landing.EndPoint.X) && Bits(leaving.StartPoint.Y) == Bits(landing.EndPoint.Y), name + ": the segments are not the same turned round");
            Assert.True(Bits(leaving.EndPoint.X) == Bits(landing.StartPoint.X) && Bits(leaving.EndPoint.Y) == Bits(landing.StartPoint.Y), name + ": the segments are not the same turned round");
            Assert.True(spline.DistanceTo(leaving.StartPoint) <= 1E-9 * Math.Max(1.0, Math.Abs(leaving.StartPoint.X) + Math.Abs(leaving.StartPoint.Y)), name + ": the shortest segment starts off the spline at " + leaving.StartPoint);
            Assert.Equal(Bits(Shortest(spline, shape, Tolerance.Global).Length), Bits(Shortest(spline, shape).Length));
            Assert.Equal(Bits(Shortest(shape, spline, Tolerance.Global).Length), Bits(Shortest(shape, spline).Length));

            _ = isPoint;
        }

        private static void Add(Dictionary<string, Action> checks, string shapeName, object shape, params (string label, GeoSpline2 spline, double distance, GeoPoint2[] crossings, double? shortest)[] cases)
        {
            foreach (var c in cases)
            {
                string row = shapeName + ": " + c.label;
                var spline = c.spline;
                var crossings = c.crossings;
                var distance = c.distance;
                var shortest = c.shortest;

                Names.Add(row);
                checks.Add(row, () => Verify(row, spline, shape, distance, crossings, shortest));
            }
        }

        #endregion

        #region The table

        private static Dictionary<string, Action> Build()
        {
            var checks = new Dictionary<string, Action>(StringComparer.Ordinal);
            GeoSpline2 ring = Ring();
            GeoSpline2 arch = Arch();
            GeoSpline2 corner = Corner();
            double at30 = Root(2500 - 900); // 40: where the ring meets y = 30 and x = 30
            double at10 = Root(2500 - 100); // 48.9898: where the ring meets y = 10

            // A segment: the ring is crossed twice by y = 30, touches y = 50 at the top, and ends of the segment can lie on it.
            Add(checks, "Line2", new GeoLine2(P(-100, 30), P(100, 30)),
                ("the ring through it", ring, 0, Pts(at30, 30, -at30, 30), null));
            Add(checks, "Line2: stopping inside the ring", new GeoLine2(P(-100, 30), P(-30, 30)),
                ("only one crossing is reached", ring, 0, Pts(-at30, 30), null));
            Add(checks, "Line2: beside the ring", new GeoLine2(P(60, -100), P(60, 100)),
                ("ten from it", ring, 10, Pts(), null));
            Add(checks, "Line2: inside the ring", new GeoLine2(P(-20, 0), P(20, 0)),
                ("thirty from either side, the lower knot first", ring, 30, Pts(), null));
            Add(checks, "Line2: touching the top", new GeoLine2(P(-100, 50), P(100, 50)),
                ("tangent at (0, 50)", ring, 0, Pts(0, 50), null));
            Add(checks, "Line2: half a thousandth over the top", new GeoLine2(P(-100, 50.0005), P(100, 50.0005)),
                ("touches within the point tolerance, at the top", ring, 0.0005, Pts(0, 50), null));
            Add(checks, "Line2: two thousandths over the top", new GeoLine2(P(-100, 50.002), P(100, 50.002)),
                ("does not touch", ring, 0.002, Pts(), null));
            Add(checks, "Line2: an end on the ring", new GeoLine2(P(50, 0), P(120, 0)),
                ("starts on the start of the ring, which is its end too, one place", ring, 0, Pts(50, 0), null));
            Add(checks, "Line2: along the first leg", new GeoLine2(P(-50, 0), P(150, 0)),
                ("the corner leaves the line at (100, 0); the leg along it is not crossing it", corner, 0, Pts(100, 0), 0));
            Add(checks, "Line2: along part of the first leg", new GeoLine2(P(20, 0), P(60, 0)),
                ("inside the leg: nought, collides, no crossing", corner, 0, Pts(), 0));
            Add(checks, "Line2: across the arch", new GeoLine2(P(150, 50), P(150, 200)),
                ("through the top", arch, 0, Pts(150, 112.5), null));
            Add(checks, "Line2: above the arch", new GeoLine2(P(0, 200), P(300, 200)),
                ("87.5 over the top", arch, 87.5, Pts(), null));
            Add(checks, "Line2: ending on the arch's end", new GeoLine2(P(300, 0), P(400, 0)),
                ("meets the end of the arch", arch, 0, Pts(300, 0), null));

            // A ray.
            Add(checks, "Ray2", new GeoRay2(P(100, 10), U(-1, 0)),
                ("running left through the ring", ring, 0, Pts(at10, 10, -at10, 10), null));
            Add(checks, "Ray2: pointing away", new GeoRay2(P(60, 10), U(1, 0)),
                ("starts outside and runs off", ring, Root(3700) - 50, Pts(), null));
            Add(checks, "Ray2: from inside", new GeoRay2(P(0, 10), U(1, 0)),
                ("leaves the ring once", ring, 0, Pts(at10, 10), null));
            Add(checks, "Ray2: along the arch's axis", new GeoRay2(P(150, 300), U(0, -1)),
                ("comes down on the top", arch, 0, Pts(150, 112.5), null));
            Add(checks, "Ray2: up from the arch", new GeoRay2(P(150, 120), U(0, 1)),
                ("starts 7.5 over the top and runs away", arch, 7.5, Pts(), null));

            // A line without ends.
            Add(checks, "XLine2", new GeoXLine2(P(-100, 30), U(1, 0)),
                ("through the ring", ring, 0, Pts(at30, 30, -at30, 30), null));
            Add(checks, "XLine2: turned round", new GeoXLine2(P(500, 30), U(-1, 0)),
                ("through the ring, from the other end", ring, 0, Pts(at30, 30, -at30, 30), null));
            Add(checks, "XLine2: beside the ring", new GeoXLine2(P(-100, 80), U(1, 0)),
                ("thirty over the top", ring, 30, Pts(), null));
            Add(checks, "XLine2: touching the top", new GeoXLine2(P(-100, 50), U(1, 0)),
                ("tangent", ring, 0, Pts(0, 50), null));
            Add(checks, "XLine2: far along", new GeoXLine2(P(1E6, 30), U(-1, 0)),
                ("through the ring from a million away", ring, 0, Pts(at30, 30, -at30, 30), null));
            Add(checks, "XLine2: across the arch", new GeoXLine2(P(-50, 100), U(1, 0)),
                ("at 100: x = 100 and x = 200", arch, 0, Pts(100, 100, 200, 100), null));
            Add(checks, "XLine2: touching the arch's top", new GeoXLine2(P(-50, 112.5), U(1, 0)),
                ("tangent at (150, 112.5)", arch, 0, Pts(150, 112.5), null));
            Add(checks, "XLine2: half a unit over the arch", new GeoXLine2(P(-50, 113), U(1, 0)),
                ("0.5 over the top", arch, 0.5, Pts(), null));
            Add(checks, "XLine2: along the first leg", new GeoXLine2(P(-50, 0), U(1, 0)),
                ("the line carries the first leg: nought, and one crossing where the corner leaves it", corner, 0, Pts(100, 0), 0));

            // A polyline.
            Add(checks, "Polyline2", new GeoPolyline2(P(-100, -30), P(100, -30), P(100, 30)),
                ("through the bottom of the ring", ring, 0, Pts(-at30, -30, at30, -30), null));
            Add(checks, "Polyline2: round the ring at thirty", new GeoPolyline2(P(-80, -80), P(-80, 80), P(80, 80)),
                ("thirty from the top and from the left", ring, 30, Pts(), null));
            Add(checks, "Polyline2: across the arch", new GeoPolyline2(P(50, 0), P(50, 200), P(250, 200), P(250, 0)),
                ("up a side, over, and down", arch, 0, Pts(50, 62.5, 250, 62.5), null));

            // A polygon, a rectangle, a triangle: closed, so a spline inside is nought from them.
            GeoPolygon2 square = Square();

            Add(checks, "Polygon2", square,
                ("the ring through its bottom and its left side", ring, 0, Pts(50, 0, 0, 50), null));
            Add(checks, "Polygon2: round the ring", new GeoPolygon2(P(-100, -100), P(100, -100), P(100, 100), P(-100, 100)),
                ("the ring is inside: nought, and 50 to the outline", ring, 0, Pts(), 50));
            Add(checks, "Polygon2: beside the ring", new GeoPolygon2(P(100, 0), P(200, 0), P(200, 100), P(100, 100)),
                ("50 from the ring", ring, 50, Pts(), null));
            Add(checks, "Polygon2: over the arch", new GeoPolygon2(P(100, 150), P(200, 150), P(200, 250), P(100, 250)),
                ("37.5 under its bottom edge, from the top of the arch", arch, 37.5, Pts(), null));
            Add(checks, "Polygon2: round the arch", new GeoPolygon2(P(-50, -50), P(350, -50), P(350, 150), P(-50, 150)),
                ("the arch is inside: nought, and 37.5 to the top edge", arch, 0, Pts(), 37.5));
            Add(checks, "Rectangle2", Rectangle(),
                ("the ring through its bottom and its left side", ring, 0, Pts(50, 0, 0, 50), null));
            Add(checks, "Rectangle2: round the ring", new GeoRectangle2(P(0, 0), 200, 200),
                ("the ring is inside", ring, 0, Pts(), 50));
            Add(checks, "Triangle2", Triangle(),
                ("the ring through its bottom and its left side, not its slope", ring, 0, Pts(50, 0, 0, 50), null));
            Add(checks, "Triangle2: beside the ring", new GeoTriangle2(P(60, 60), P(160, 60), P(60, 160)),
                ("the corner (60, 60) is 84.85 from the centre", ring, Root(7200) - 50, Pts(), null));

            // A face: the material is the square less the hole.
            Add(checks, "Face2", Plate(),
                ("the ring through the outline and the hole", ring, 0, Pts(50, 0, 40, 30, 30, 40, 0, 50), null));
            Add(checks, "Face2: in the hole", Plate(),
                ("a circle of radius 5 in the hole of 40 by 40 is 15 from its rim", Round(50, 50, 5), 15, Pts(), null));
            Add(checks, "Face2: in the material", Plate(),
                ("a circle of radius 5 in the material, 10 from the outline: nought from the face, and 10 to its rim", Round(15, 15, 5), 0, Pts(), 10));

            // An arc, a circle, an ellipse: all curved.
            Add(checks, "Arc2", new GeoArc2(P(60, 0), 80, Math.PI / 2, 3 * Math.PI / 2),
                ("crosses the ring where the two circles do", ring, 0, Pts(-2.5, Root(2500 - 6.25), -2.5, -Root(2500 - 6.25)), null));
            Add(checks, "Arc2: along the ring", Arc(),
                ("lies along it: nought, collides, no crossing", ring, 0, Pts(), 0));
            Add(checks, "Arc2: far from the ring", new GeoArc2(P(150, 0), 20, Math.PI / 2, Math.PI),
                ("the arc's nearest point is (130, 0)", ring, 80, Pts(), null));
            Add(checks, "Circle2", new GeoCircle2(P(60, 0), 30),
                ("the two circles meet at x = 43.33", ring, 0, Pts(130.0 / 3.0, Root(2500 - 130.0 * 130.0 / 9.0), 130.0 / 3.0, -Root(2500 - 130.0 * 130.0 / 9.0)), null));
            Add(checks, "Circle2: round the ring", new GeoCircle2(P(0, 0), 100),
                ("the ring is inside: nought, and 50 to the rim", ring, 0, Pts(), 50));
            Add(checks, "Circle2: beside the ring", new GeoCircle2(P(200, 0), 50),
                ("100 apart", ring, 100, Pts(), null));
            Add(checks, "Circle2: touching the ring", new GeoCircle2(P(100, 0), 50),
                ("tangent at (50, 0)", ring, 0, Pts(50, 0), null));
            Add(checks, "Ellipse2", new GeoEllipse2(P(0, 0), GeoVector2.XAxis, 100, 20),
                ("the ring crosses the long thin ellipse four times", ring, 0, Pts(Root(2187.5), Root(312.5), -Root(2187.5), Root(312.5), -Root(2187.5), -Root(312.5), Root(2187.5), -Root(312.5)), null));
            Add(checks, "Ellipse2: inside the ring", new GeoEllipse2(P(0, 0), GeoVector2.XAxis, 30, 20),
                ("20 from the end of the ellipse", ring, 20, Pts(), null));
            Add(checks, "Ellipse2: round the ring", Oval(),
                ("the ring is inside and touches the ellipse top and bottom", ring, 0, Pts(0, 50, 0, -50), 0));

            // An edge, a chain, a loop.
            Add(checks, "Edge2: straight", new GeoEdge2(P(60, -100), P(60, 100)),
                ("ten from the ring", ring, 10, Pts(), null));
            Add(checks, "Edge2: curved", new GeoEdge2(new GeoArc2(P(60, 0), 80, Math.PI / 2, 3 * Math.PI / 2)),
                ("crosses the ring twice", ring, 0, Pts(-2.5, Root(2500 - 6.25), -2.5, -Root(2500 - 6.25)), null));
            Add(checks, "PolylineArc2", Hook(),
                ("the straight edge crosses the ring at (50, 0); the bowed one is far", ring, 0, Pts(50, 0), null));
            Add(checks, "PolylineArc2: a half circle far off", new GeoPolylineArc2(new[] { P(150, -50), P(150, 50) }, new[] { 1.0 }),
                ("nearest at its ends (150, 50) and (150, -50): 158.11 from the origin", ring, Root(150 * 150 + 50 * 50) - 50, Pts(), null));
            Add(checks, "PolygonArc2", Slot(),
                ("the ring crosses the bottom at (50, 0) and touches the round end at the top", ring, 0, Pts(50, 0, 0, 50), null));

            // Another spline.
            var flipped = new GeoSpline2(3, new[] { P(0, 100), P(100, -50), P(200, -50), P(300, 100) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });
            double cross = 50.0 * Root(5.0); // the arch and its twin meet where t (1 - t) = 1/9: x = 150 -+ 50 sqrt 5, at height 50

            Add(checks, "Spline2: crossing", flipped,
                ("the arch and its upside down twin cross where 450 t (1 - t) = 100 - 450 t (1 - t)", arch, 0, Pts(150 - cross, 50, 150 + cross, 50), null));
            Add(checks, "Spline2: apart", arch + new GeoVector2(0, 20),
                ("a copy 20 over it is not 20 away: its start is 11.3358552330596 from the arch, at t = 0.0308", arch, 11.3358552330596365, Pts(), null));
            Add(checks, "Spline2: the same", arch.Clone(),
                ("a copy lies along it: nought, collides, no crossing", arch, 0, Pts(), 0));
            Add(checks, "Spline2: a circle through the arch", Round(150, 60, 40),
                ("the circle of radius 40 about (150, 60) is 12.5 under the top", arch, 12.5, Pts(), null));

            return checks;
        }

        #endregion
    }
}
