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
    /// The infinite line in the plane asked the five queries about each of the fifteen shapes it meets, from either side,
    /// with the numbers worked out by hand: a line through the shape, one beside it, one along a side or touching an end, one
    /// starting a million away and running through it, and where the shape has a start or an end, one that would only touch
    /// it if the line stopped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pair is held to what a segment is held to, read for a line (XL-4): a closed shape is filled for the distance and
    /// the collision; the crossings are those of the boundary; the shortest line runs boundary to boundary, so it has no
    /// length where the line runs through the shape. Asked from either side the two say the same: the same distance and
    /// the same collision to the bit, the crossings as one set, and a shortest line as long as the distance that starts on
    /// the line when it leaves the line and ends on it when it lands on it. Each form without a tolerance answers, to the
    /// bit, what the form with <see cref="Tolerance.Global"/> answers. Where a member is missing the case fails with its
    /// name, since the members are found by reflection.
    /// </para>
    /// <para>
    /// Half a thousandth and two thousandths sit either side of the point tolerance, a thousandth, a factor of two clear of
    /// it. The shapes are those of <see cref="Ray2Shapes"/>.
    /// </para>
    /// </remarks>
    public class XLine2AgainstShapesTests
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
        public void TheLineAndTheShape_AgreeFromEitherSide_OnTheNumbersWorkedOutByHand(string row)
        {
            Checks[row]();
        }

        #region Judging

        private static GeoXLine2 X(double x, double y, double dx, double dy) => new GeoXLine2(P(x, y), V(dx, dy));

        private static bool OnTheLine(GeoXLine2 line, GeoPoint2 point)
        {
            double off = line.Direction.X * (point.Y - line.Origin.Y) - line.Direction.Y * (point.X - line.Origin.X);

            return Math.Abs(off) <= 1E-8;
        }

        private static void AssertSameSet(GeoPoint2[] expected, GeoPoint2[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " crossings, " + expected.Length + " expected: " + string.Join(" ", actual));

            foreach (GeoPoint2 point in expected)
            {
                Assert.True(actual.Any(p => p.DistanceTo(point) <= 1E-6), what + ": no crossing at " + point + " among " + string.Join(" ", actual));
            }
        }

        private static void Verify(string name, object shape, GeoXLine2 line, double distance, GeoPoint2[] crossings)
        {
            bool isPoint = shape is GeoPoint2;
            bool hasCrossings = Answers(line, "GetIntersections", shape);
            bool hasTry = HasTry(line, shape);

            // A question the line asks of the shape, the shape asks of the line, and the other way about.
            foreach (string query in new[] { "DistanceTo", "CollidesWith", "GetIntersections", "GetShortestLineTo" })
            {
                if (query == "CollidesWith" && isPoint)
                {
                    continue;
                }

                Assert.True(Answers(line, query, shape) == Answers(shape, query, line) || (query == "CollidesWith" && isPoint), name + " " + query + " is one-sided");
            }

            Assert.True(hasTry == HasTry(shape, line), name + " TryIntersectWith is one-sided");

            // Distance: as worked out, the same from both sides to the bit, and the plain form is the global one.
            double fromLine = Distance(line, shape, Strict);
            double fromShape = Distance(shape, line, Strict);

            Assert.InRange(fromLine, distance - 1E-9, distance + 1E-9);
            Assert.Equal(fromLine, fromShape);
            Assert.Equal(Distance(line, shape, Tolerance.Global), Distance(line, shape));
            Assert.Equal(Distance(shape, line, Tolerance.Global), Distance(shape, line));

            // Collision: the distance being within the point tolerance, the same from both sides.
            if (!isPoint)
            {
                bool collides = distance <= Strict.EqualPoint;

                Assert.Equal(collides, Collides(line, shape, Strict));
                Assert.Equal(collides, Collides(shape, line, Strict));
                Assert.Equal(Collides(line, shape, Tolerance.Global), Collides(line, shape));
                Assert.Equal(Collides(shape, line, Tolerance.Global), Collides(shape, line));
            }

            // Crossings: as worked out where it is given, one set from both sides, the plain form the global one.
            if (hasCrossings)
            {
                GeoPoint2[] fromLineSide = Crossings(line, shape, Strict);
                GeoPoint2[] fromShapeSide = Crossings(shape, line, Strict);

                if (crossings != null)
                {
                    AssertSameSet(crossings, fromLineSide, name + " from the line");
                    AssertSameSet(crossings, fromShapeSide, name + " from the shape");
                }

                AssertSameSet(fromLineSide, fromShapeSide, name + " both sides");
                Assert.Equal(Crossings(line, shape, Tolerance.Global), Crossings(line, shape));
                Assert.Equal(Crossings(shape, line, Tolerance.Global), Crossings(shape, line));
            }

            if (hasTry && crossings != null)
            {
                bool tried = Try(line, shape, out GeoPoint2[] points, Strict);
                bool triedBack = Try(shape, line, out GeoPoint2[] pointsBack, Strict);

                Assert.Equal(crossings.Length > 0, tried);
                Assert.Equal(crossings.Length > 0, triedBack);
                AssertSameSet(crossings, points, name + " Try from the line");
                AssertSameSet(crossings, pointsBack, name + " Try from the shape");
                Assert.Equal(tried, Try(line, shape, out GeoPoint2[] plain));
                Assert.Equal(points, plain);
            }

            // Shortest line: as long as the distance; leaving the line it starts on it, landing on the line it ends on it.
            GeoLine2 leaving = Shortest(line, shape, Strict);
            GeoLine2 landing = Shortest(shape, line, Strict);

            // Within the point tolerance of the shape but not on it, the segments' own shortest line may keep its slack and be of no
            // length where the distance keeps the gap, as it does for a segment.
            double least = distance > 0 && distance <= Strict.EqualPoint ? 0.0 : distance - 1E-9;

            Assert.InRange(leaving.Length, least, distance + 1E-9);
            Assert.InRange(landing.Length, least, distance + 1E-9);
            Assert.True(OnTheLine(line, leaving.StartPoint), name + ": the shortest line leaving the line starts off it at " + leaving.StartPoint);
            Assert.True(OnTheLine(line, landing.EndPoint), name + ": the shortest line landing on the line ends off it at " + landing.EndPoint);
            Assert.Equal(Shortest(line, shape, Tolerance.Global), Shortest(line, shape));
            Assert.Equal(Shortest(shape, line, Tolerance.Global), Shortest(shape, line));
        }

        private static void Add(Dictionary<string, Action> checks, string shapeName, object shape, params (string label, GeoXLine2 line, double distance, GeoPoint2[] crossings)[] cases)
        {
            foreach (var c in cases)
            {
                string row = shapeName + ": " + c.label;
                var line = c.line;
                var crossings = c.crossings;
                var distance = c.distance;

                Names.Add(row);
                checks.Add(row, () => Verify(row, shape, line, distance, crossings));
            }
        }

        private static GeoPoint2[] Pts(params double[] xy)
        {
            var points = new GeoPoint2[xy.Length / 2];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = P(xy[2 * i], xy[2 * i + 1]);
            }

            return points;
        }

        #endregion

        #region The table

        private static Dictionary<string, Action> Build()
        {
            var checks = new Dictionary<string, Action>(StringComparer.Ordinal);

            // The square 100 by 100 with a corner at the origin, as a polygon and as a rectangle.
            var square = new (string, GeoXLine2, double, GeoPoint2[])[]
            {
                ("through", X(-50, 50, 1, 0), 0, Pts(0, 50, 100, 50)),
                ("from inside", X(50, 50, 0, 1), 0, Pts(50, 0, 50, 100)),
                ("beside", X(-50, 150, 1, 0), 50, Pts()),
                ("beside, turned round", X(500, 150, -1, 0), 50, Pts()),
                ("along a side", X(-50, 0, 1, 0), 0, null),
                ("half a thousandth off a side", X(-50, 100.0005, 1, 0), 0.0005, null),
                ("two thousandths off a side", X(-50, 100.002, 1, 0), 0.002, Pts()),
                ("touching a corner", X(100, 100, 1, -1), 0, null),
                ("starting a million along", X(1E6, 50, 1, 0), 0, Pts(0, 50, 100, 50)),
                ("through two corners", X(-50, 150, 1, -1), 0, Pts(0, 100, 100, 0)),
            };

            Add(checks, "Polygon2", Square(), square);
            Add(checks, "Rectangle2", Rectangle(), square);

            Add(checks, "Triangle2", Triangle(),
                ("through", X(-50, 20, 1, 0), 0, Pts(0, 20, 80, 20)),
                ("beside", X(-50, -30, 1, 0), 30, Pts()),
                ("beside, along the hypotenuse", X(150, 50, -1, 1), 100 / Math.Sqrt(2), Pts()),
                ("touching a corner", X(100, 0, 1, 1), 0, null),
                ("starting a million along", X(1E6, 20, 1, 0), 0, Pts(0, 20, 80, 20)));

            Add(checks, "Face2", Plate(),
                ("through the hole", X(-50, 50, 1, 0), 0, Pts(0, 50, 30, 50, 70, 50, 100, 50)),
                ("from the hole", X(50, 50, 0, 1), 0, Pts(50, 0, 50, 30, 50, 70, 50, 100)),
                ("beside", X(-50, 150, 1, 0), 50, Pts()),
                ("starting a million along", X(1E6, 50, 1, 0), 0, Pts(0, 50, 30, 50, 70, 50, 100, 50)));

            Add(checks, "Circle2", Circle(),
                ("through", X(-100, 30, 1, 0), 0, Pts(-40, 30, 40, 30)),
                ("beside", X(-100, 80, 1, 0), 30, Pts()),
                ("tangent", X(-100, 50, 1, 0), 0, null),
                ("from the centre", X(0, 0, 0, 1), 0, Pts(0, -50, 0, 50)),
                ("starting half a million along", X(5E5, 0, 1, 0), 0, Pts(-50, 0, 50, 0)),
                ("four tenths above", X(-100, 50.4, 1, 0), 0.4, Pts()));

            Add(checks, "Arc2", Arc(),
                ("through", X(-100, 30, 1, 0), 0, Pts(-40, 30, 40, 30)),
                ("beside", X(-100, -30, 1, 0), 30, Pts()),
                ("touching an end", X(-100, 0, 1, 0), 0, null),
                ("four tenths above", X(-100, 50.4, 1, 0), 0.4, Pts()),
                ("beside, left of it", X(-100, -200, 0, 1), 50, Pts()));

            Add(checks, "Arc2: bulging past its ends", RightHalf(),
                ("through the bulge", X(-100, 0, 1, 0), 0, Pts(50, 0)),
                ("below it", X(-100, -60, 1, 0), 10, Pts()));

            Add(checks, "Ellipse2", Oval(),
                ("through", X(-150, 30, 1, 0), 0, Pts(-80, 30, 80, 30)),
                ("beside", X(-150, 80, 1, 0), 30, Pts()),
                ("beside its end", X(150, -200, 0, 1), 50, Pts()),
                ("from the centre", X(0, 0, 1, 0), 0, Pts(-100, 0, 100, 0)),
                ("tangent", X(-150, 50, 1, 0), 0, null));

            Add(checks, "Polyline2", Ell(),
                ("across the bottom", X(50, -200, 0, 1), 0, Pts(50, 0)),
                ("across the upright", X(-50, 50, 1, 0), 0, Pts(100, 50)),
                ("below", X(-50, -30, 1, 0), 30, Pts()),
                ("left of it", X(-30, -50, 0, 1), 30, Pts()),
                ("touching an end", X(-50, 100, 1, 0), 0, null),
                ("starting a million along", X(1E6, 50, 1, 0), 0, Pts(100, 50)));

            Add(checks, "PolylineArc2", Hook(),
                ("through the bowed upright", X(-50, 50, 1, 0), 0, Pts(150, 50)),
                ("across the bow", X(125, -200, 0, 1), 0, Pts(125, 50 - Math.Sqrt(1875), 125, 50 + Math.Sqrt(1875))),
                ("beside the bow", X(200, -200, 0, 1), 50, Pts()),
                ("below", X(-50, -30, 1, 0), 30, Pts()));

            Add(checks, "PolygonArc2", Slot(),
                ("through the round ends", X(-300, 25, 1, 0), 0, Pts(-25, 25, 225, 25)),
                ("across the middle", X(100, -200, 0, 1), 0, Pts(100, 0, 100, 50)),
                ("above", X(-300, 80, 1, 0), 30, Pts()),
                ("left of the round end", X(-60, -200, 0, 1), 35, Pts()),
                ("below", X(-300, -25, 1, 0), 25, Pts()));

            var segment = new GeoLine2(P(50, -10), P(50, 10));

            Add(checks, "Line2", segment,
                ("across", X(0, 0, 1, 0), 0, Pts(50, 0)),
                ("above", X(0, 20, 1, 0), 10, Pts()),
                ("touching an end", X(0, 10, 1, 0), 0, null),
                ("along it", X(50, -300, 0, 1), 0, Pts()),
                ("beside it", X(60, -300, 0, 1), 10, Pts()));

            Add(checks, "Edge2: straight", new GeoEdge2(P(50, -10), P(50, 10)),
                ("across", X(0, 0, 1, 0), 0, Pts(50, 0)),
                ("above", X(0, 20, 1, 0), 10, Pts()),
                ("beside it", X(60, -300, 0, 1), 10, Pts()));

            Add(checks, "Edge2: curved", new GeoEdge2(Arc()),
                ("through", X(-100, 30, 1, 0), 0, Pts(-40, 30, 40, 30)),
                ("beside", X(-100, -30, 1, 0), 30, Pts()));

            Add(checks, "Ray2: standing over the line", new GeoRay2(P(50, 30), V(0, 1)),
                ("the ray runs away", X(0, 0, 1, 0), 30, Pts()),
                ("the line turned round", X(500, 0, -1, 0), 30, Pts()));

            Add(checks, "Ray2: running down", new GeoRay2(P(50, 30), V(0, -1)),
                ("crossing", X(0, 0, 1, 0), 0, Pts(50, 0)));

            Add(checks, "Ray2: along the line", new GeoRay2(P(100, 0), V(1, 0)),
                ("on it", X(0, 0, 1, 0), 0, Pts()),
                ("on it, turned round", X(0, 0, -1, 0), 0, Pts()));

            Add(checks, "Ray2: beside the line", new GeoRay2(P(100, 30), V(1, 0)),
                ("parallel", X(0, 0, 1, 0), 30, Pts()));

            Add(checks, "XLine2", X(0, 30, -1, 0),
                ("parallel, apart", X(0, 0, 1, 0), 30, Pts()));

            Add(checks, "XLine2: across", X(50, -50, 0, 1),
                ("crossing", X(0, 0, 1, 0), 0, Pts(50, 0)));

            Add(checks, "XLine2: the same line", X(500, 0, -1, 0),
                ("the line turned round", X(0, 0, 1, 0), 0, Pts()));

            Add(checks, "Point2: off the line", P(50, 40),
                ("ahead", X(0, 0, 1, 0), 40, null));

            Add(checks, "Point2: behind the origin", P(-30, 40),
                ("behind", X(0, 0, 1, 0), 40, null));

            Add(checks, "Point2: on the line", P(7, 0),
                ("on it", X(0, 0, 1, 0), 0, null));

            return checks;
        }

        #endregion
    }
}
