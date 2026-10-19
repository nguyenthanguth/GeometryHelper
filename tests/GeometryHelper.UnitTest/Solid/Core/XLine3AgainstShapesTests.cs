using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Shapes;
using static GeometryHelper.UnitTest.Solid.XLine3Probe;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The infinite line in space asked the five queries about each of the shapes it meets, from either side, with the numbers
    /// worked out by hand: a line through the shape, one beside it, one along a side or touching an end, one starting a million
    /// away and running through it, and where the shape has a start or an end, one that would only touch it if the line
    /// stopped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pair is held to what a segment is held to, read for a line (XL-4): a closed shape is filled for the distance and the
    /// collision; the crossings are those of the surface or the boundary; the shortest line runs boundary to boundary, so it
    /// has no length where the line runs through the shape. Asked from either side the two say the same: the same distance and
    /// the same collision to the bit, the crossings as one set, and a shortest line as long as the distance that starts on the
    /// line when it leaves the line and ends on it when it lands on it. Each form without a tolerance answers, to the bit, what
    /// the form with <see cref="Tolerance.Global"/> answers. Where a member is missing the case fails with its name, since the
    /// members are found by reflection.
    /// </para>
    /// <para>
    /// The curved shapes are not measured (an arc, a circle, an ellipse, an edge, a curved chain or loop), as they are not for a
    /// ray or a segment: for them the distance in the table says only whether they collide. Half a thousandth and two
    /// thousandths sit either side of the point tolerance, a thousandth, a factor of two clear of it. The shapes are those of
    /// <see cref="XLine3Shapes"/>.
    /// </para>
    /// </remarks>
    public class XLine3AgainstShapesTests
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

        private static bool OnTheLine(GeoXLine3 line, GeoPoint3 point)
        {
            double off = line.Direction.CrossProduct(line.Origin.GetVectorTo(point)).Length;

            return off <= 1E-8;
        }

        private static void AssertSameSet(GeoPoint3[] expected, GeoPoint3[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " crossings, " + expected.Length + " expected: " + string.Join(" ", actual));

            foreach (GeoPoint3 point in expected)
            {
                Assert.True(actual.Any(p => p.DistanceTo(point) <= 1E-6), what + ": no crossing at " + point + " among " + string.Join(" ", actual));
            }
        }

        private static void Verify(string name, object shape, GeoXLine3 line, double distance, GeoPoint3[] crossings)
        {
            bool isPoint = shape is GeoPoint3;
            bool hasDistance = Answers(line, "DistanceTo", shape);
            bool hasCrossings = Answers(line, "GetIntersections", shape);
            bool hasTry = HasTry(line, shape);

            // A question the line asks of the shape, the shape asks of the line, and the other way about.
            foreach (string query in new[] { "DistanceTo", "CollidesWith", "GetIntersections", "GetShortestLineTo" })
            {
                if (query == "CollidesWith" && isPoint)
                {
                    continue;
                }

                Assert.True(Answers(line, query, shape) == Answers(shape, query, line), name + " " + query + " is one-sided");
            }

            Assert.True(hasTry == HasTry(shape, line), name + " TryIntersectWith is one-sided");

            // Distance: as worked out, the same from both sides to the bit, and the plain form is the global one.
            if (hasDistance)
            {
                double fromLine = Distance(line, shape, Strict);
                double fromShape = Distance(shape, line, Strict);

                Assert.InRange(fromLine, distance - 1E-9, distance + 1E-9);
                Assert.Equal(fromLine, fromShape);
                Assert.Equal(Distance(line, shape, Tolerance.Global), Distance(line, shape));
                Assert.Equal(Distance(shape, line, Tolerance.Global), Distance(shape, line));
            }

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
                GeoPoint3[] fromLineSide = Crossings(line, shape, Strict);
                GeoPoint3[] fromShapeSide = Crossings(shape, line, Strict);

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
                bool tried = Try(line, shape, out GeoPoint3[] points, Strict);
                bool triedBack = Try(shape, line, out GeoPoint3[] pointsBack, Strict);

                Assert.Equal(crossings.Length > 0, tried);
                Assert.Equal(crossings.Length > 0, triedBack);
                AssertSameSet(crossings, points, name + " Try from the line");
                AssertSameSet(crossings, pointsBack, name + " Try from the shape");
                Assert.Equal(tried, Try(line, shape, out GeoPoint3[] plain));
                Assert.Equal(points, plain);
            }

            // Shortest line: as long as the distance; leaving the line it starts on it, landing on the line it ends on it.
            if (Answers(line, "GetShortestLineTo", shape))
            {
                GeoLine3 leaving = Shortest(line, shape, Strict);
                GeoLine3 landing = Shortest(shape, line, Strict);

                // Within the point tolerance of the shape but not on it, the segments' own shortest line may keep its slack and be of no
                // length where the distance keeps the gap, as it does for a segment.
                double least = distance > 0 && distance <= Strict.EqualPoint ? 0.0 : distance - 1E-9;

                // A line starting within the planar tolerance of a plane touches it, as a ray does, and is joined to it by the
                // perpendicular from its origin where the distance reads nought.
                double slack = shape is GeoPlane3 && distance == 0.0 ? Strict.EqualPlanar : 0.0;

                Assert.InRange(leaving.Length, least, distance + 1E-9 + slack);
                Assert.InRange(landing.Length, least, distance + 1E-9 + slack);
                Assert.True(OnTheLine(line, leaving.StartPoint), name + ": the shortest line leaving the line starts off it at " + leaving.StartPoint);
                Assert.True(OnTheLine(line, landing.EndPoint), name + ": the shortest line landing on the line ends off it at " + landing.EndPoint);
                Assert.Equal(leaving, landing.Reverse());
                Assert.Equal(Shortest(line, shape, Tolerance.Global), Shortest(line, shape));
                Assert.Equal(Shortest(shape, line, Tolerance.Global), Shortest(shape, line));
            }
        }

        private static void Add(Dictionary<string, Action> checks, string shapeName, object shape, params (string label, GeoXLine3 line, double distance, GeoPoint3[] crossings)[] cases)
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

        private static GeoPoint3[] Pts(params double[] xyz)
        {
            var points = new GeoPoint3[xyz.Length / 3];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = P(xyz[3 * i], xyz[3 * i + 1], xyz[3 * i + 2]);
            }

            return points;
        }

        #endregion

        #region The table

        private static Dictionary<string, Action> Build()
        {
            var checks = new Dictionary<string, Action>(StringComparer.Ordinal);

            // The box 100 by 100 by 20 with a corner at the origin, as a box, an oriented box and a solid.
            var box = new (string, GeoXLine3, double, GeoPoint3[])[]
            {
                ("through", X(-50, 50, 10, 1, 0, 0), 0, Pts(0, 50, 10, 100, 50, 10)),
                ("from inside, straight up", X(50, 50, 10, 0, 0, 1), 0, Pts(50, 50, 0, 50, 50, 20)),
                ("beside", X(-50, 150, 10, 1, 0, 0), 50, Pts()),
                ("beside, turned round", X(500, 150, 10, -1, 0, 0), 50, Pts()),
                ("ten above the top", X(-50, 50, 30, 1, 0, 0), 10, Pts()),
                ("half a thousandth above the top", X(-50, 50, 20.0005, 1, 0, 0), 0.0005, null),
                ("two thousandths above the top", X(-50, 50, 20.002, 1, 0, 0), 0.002, Pts()),
                ("along the top", X(-50, 50, 20, 1, 0, 0), 0, null),
                ("touching a corner", X(100, 100, 20, 1, -1, 0), 0, null),
                ("starting a million along", X(1E6, 50, 10, 1, 0, 0), 0, Pts(0, 50, 10, 100, 50, 10)),
                ("skew, past a top edge", X(120, 0, 40, 0, 1, 0), 20 * Math.Sqrt(2), Pts()),
                ("skew, diagonal in the mid plane", X(150, 150, 10, 1, -1, 0), 100 / Math.Sqrt(2), Pts()),
            };

            Add(checks, "Aabb3", Aabb(), box);
            Add(checks, "Obb3", Obb(), box);
            Add(checks, "Solid3", SolidBox(), box);

            // The square in the floor.
            var flat = new (string, GeoXLine3, double, GeoPoint3[])[]
            {
                ("in its plane, through", X(-50, 50, 0, 1, 0, 0), 0, Pts()),
                ("piercing it, from above", X(50, 50, 10, 0, 0, -1), 0, Pts(50, 50, 0)),
                ("piercing it, from below", X(50, 50, -10, 0, 0, 1), 0, Pts(50, 50, 0)),
                ("beside it, upright", X(150, 50, 10, 0, 0, 1), 50, Pts()),
                ("ten above, running along it", X(-50, 50, 10, 1, 0, 0), 10, Pts()),
                ("half a thousandth above", X(-50, 50, 0.0005, 1, 0, 0), 0.0005, Pts()),
                ("two thousandths above", X(-50, 50, 0.002, 1, 0, 0), 0.002, Pts()),
                ("crossing the plane outside it", X(200, 50, 10, -1, 0, -1), 45 * Math.Sqrt(2), Pts()),
                ("starting a million along, above", X(1E6, 50, 10, -1, 0, 0), 10, Pts()),
                ("piercing it from a million up", X(50, 50, 1E6, 0, 0, -1), 0, Pts(50, 50, 0)),
            };

            Add(checks, "Polygon3", Square(), flat);

            Add(checks, "Triangle3", Triangle(),
                ("piercing it", X(20, 20, 10, 0, 0, -1), 0, Pts(20, 20, 0)),
                ("in its plane, beside", X(-50, -30, 0, 1, 0, 0), 30, Pts()),
                ("in its plane, beside the hypotenuse", X(100, 100, 0, -1, 1, 0), 100 / Math.Sqrt(2), Pts()),
                ("in its plane, through", X(-50, 20, 0, 1, 0, 0), 0, Pts()),
                ("touching a corner", X(100, 0, 0, 1, 1, 0), 0, null),
                ("piercing it from a million up", X(20, 20, 1E6, 0, 0, -1), 0, Pts(20, 20, 0)),
                ("ten above, running along it", X(-50, 20, 10, 1, 0, 0), 10, Pts()));

            // The square with the square from (30, 30) to (70, 70) cut out of it.
            Add(checks, "Face3", Plate(),
                ("through the hole, upright", X(50, 50, 10, 0, 0, 1), 20, Pts()),
                ("piercing the material", X(20, 20, 10, 0, 0, 1), 0, Pts(20, 20, 0)),
                ("beside", X(150, 50, 10, 0, 0, 1), 50, Pts()),
                ("in its plane, through the hole", X(-50, 50, 0, 1, 0, 0), 0, Pts()),
                ("piercing the material from a million up", X(20, 20, 1E6, 0, 0, -1), 0, Pts(20, 20, 0)));

            Add(checks, "Plane3", Floor(),
                ("crossing, slanted", X(0, 0, 10, 1, 0, -1), 0, Pts(10, 0, 0)),
                ("crossing, upright", X(5, 5, 10, 0, 0, 1), 0, Pts(5, 5, 0)),
                ("running along it, seven above", X(0, 0, 7, 1, 0, 0), 7, Pts()),
                ("lying in it", X(0, 0, 0, 1, 0, 0), 0, Pts()),
                ("half a thousandth above", X(0, 0, 0.0005, 1, 0, 0), 0, Pts()),
                ("two thousandths above", X(0, 0, 0.002, 1, 0, 0), 0.002, Pts()),
                ("a ten-thousandth of a radian off level", X(0, 0, 7, 1, 0, -0.0001), 0, Pts(70000, 0, 0)));

            Add(checks, "Circle3", Circle(),
                ("through, in its plane", X(-100, 30, 0, 1, 0, 0), 0, Pts(-40, 30, 0, 40, 30, 0)),
                ("beside, in its plane", X(-100, 80, 0, 1, 0, 0), 30, Pts()),
                ("tangent", X(-100, 50, 0, 1, 0, 0), 0, null),
                ("through the middle of the disc", X(0, 0, -10, 0, 0, 1), 50, Pts()),
                ("piercing the rim", X(50, 0, -10, 0, 0, 1), 0, Pts(50, 0, 0)),
                ("four tenths above the rim", X(-100, 50.4, 0, 1, 0, 0), 0.4, Pts()),
                ("starting half a million along", X(5E5, 0, 0, -1, 0, 0), 0, Pts(50, 0, 0, -50, 0, 0)));

            Add(checks, "Arc3", Arc(),
                ("through, in its plane", X(-100, 30, 0, 1, 0, 0), 0, Pts(-40, 30, 0, 40, 30, 0)),
                ("below it, in its plane", X(-100, -30, 0, 1, 0, 0), 30, Pts()),
                ("piercing it at the top", X(0, 50, -10, 0, 0, 1), 0, Pts(0, 50, 0)),
                ("piercing the half of the circle it does not have", X(0, -50, -10, 0, 0, 1), 50 * Math.Sqrt(2), Pts()));

            Add(checks, "Ellipse3", Oval(),
                ("through, in its plane", X(-150, 30, 0, 1, 0, 0), 0, Pts(-80, 30, 0, 80, 30, 0)),
                ("beside, in its plane", X(-150, 80, 0, 1, 0, 0), 30, Pts()),
                ("piercing the rim at an end of the long axis", X(100, 0, -5, 0, 0, 1), 0, Pts(100, 0, 0)),
                ("through the middle of the disc", X(0, 0, -10, 0, 0, 1), 50, Pts()));

            Add(checks, "Polyline3", Ell(),
                ("piercing the bottom", X(50, 0, -10, 0, 0, 1), 0, Pts(50, 0, 0)),
                ("piercing the upright", X(100, 50, -10, 0, 0, 1), 0, Pts(100, 50, 0)),
                ("beside the bottom, in its plane", X(-50, -30, 0, 1, 0, 0), 30, Pts()),
                ("skew, ten over the bottom", X(50, -200, 10, 0, 1, 0), 10, Pts()),
                ("piercing the bottom from a million up", X(50, 0, 1E6, 0, 0, -1), 0, Pts(50, 0, 0)));

            Add(checks, "PolylineArc3", Hook(),
                ("piercing the bow", X(150, 50, -10, 0, 0, 1), 0, Pts(150, 50, 0)),
                ("across the bow, in its plane", X(125, -200, 0, 0, 1, 0), 0, Pts(125, 50 - Math.Sqrt(1875), 0, 125, 50 + Math.Sqrt(1875), 0)),
                ("beside the bow, in its plane", X(200, -200, 0, 0, 1, 0), 50, Pts()),
                ("below, in its plane", X(-50, -30, 0, 1, 0, 0), 30, Pts()));

            Add(checks, "PolygonArc3", Slot(),
                ("through the round ends, in its plane", X(-300, 25, 0, 1, 0, 0), 0, Pts(-25, 25, 0, 225, 25, 0)),
                ("across the middle, in its plane", X(100, -200, 0, 0, 1, 0), 0, Pts(100, 0, 0, 100, 50, 0)),
                ("above, in its plane", X(-300, 80, 0, 1, 0, 0), 30, Pts()),
                ("piercing the side", X(100, 0, -10, 0, 0, 1), 0, Pts(100, 0, 0)));

            var segment = Segment();

            Add(checks, "Line3", segment,
                ("across", X(0, 0, 0, 1, 0, 0), 0, Pts(50, 0, 0)),
                ("skew, twenty over it", X(0, 0, 20, 1, 0, 0), 20, Pts()),
                ("past its end", X(0, 20, 0, 1, 0, 0), 10, Pts()),
                ("touching an end", X(0, 10, 0, 1, 0, 0), 0, null),
                ("along it", X(50, -300, 0, 0, 1, 0), 0, Pts()),
                ("beside it", X(60, -300, 0, 0, 1, 0), 10, Pts()));

            Add(checks, "Edge3: straight", new GeoEdge3(P(50, -10, 0), P(50, 10, 0)),
                ("across", X(0, 0, 0, 1, 0, 0), 0, Pts(50, 0, 0)),
                ("past its end", X(0, 20, 0, 1, 0, 0), 10, Pts()),
                ("beside it", X(60, -300, 0, 0, 1, 0), 10, Pts()));

            Add(checks, "Edge3: curved", new GeoEdge3(Arc()),
                ("through, in its plane", X(-100, 30, 0, 1, 0, 0), 0, Pts(-40, 30, 0, 40, 30, 0)),
                ("below it, in its plane", X(-100, -30, 0, 1, 0, 0), 30, Pts()));

            Add(checks, "Ray3: standing over the line", new GeoRay3(P(50, 30, 40), V(0, 1, 0)),
                ("the ray runs away", X(0, 0, 0, 1, 0, 0), 50, Pts()),
                ("the line turned round", X(500, 0, 0, -1, 0, 0), 50, Pts()));

            Add(checks, "Ray3: running towards the line", new GeoRay3(P(50, 30, 40), V(0, -1, 0)),
                ("reaching the common perpendicular", X(0, 0, 0, 1, 0, 0), 40, Pts()));

            Add(checks, "Ray3: running down", new GeoRay3(P(50, 30, 0), V(0, -1, 0)),
                ("crossing", X(0, 0, 0, 1, 0, 0), 0, Pts(50, 0, 0)));

            Add(checks, "Ray3: along the line", new GeoRay3(P(100, 0, 0), V(1, 0, 0)),
                ("on it", X(0, 0, 0, 1, 0, 0), 0, Pts()),
                ("on it, turned round", X(0, 0, 0, -1, 0, 0), 0, Pts()));

            Add(checks, "Ray3: beside the line", new GeoRay3(P(100, 30, 0), V(1, 0, 0)),
                ("parallel", X(0, 0, 0, 1, 0, 0), 30, Pts()));

            Add(checks, "XLine3", X(0, 30, 0, -1, 0, 0),
                ("parallel, apart", X(0, 0, 0, 1, 0, 0), 30, Pts()));

            Add(checks, "XLine3: skew", X(0, 0, 30, 0, 1, 0),
                ("thirty over it", X(0, 0, 0, 1, 0, 0), 30, Pts()));

            Add(checks, "XLine3: across", X(50, -50, 0, 0, 1, 0),
                ("crossing", X(0, 0, 0, 1, 0, 0), 0, Pts(50, 0, 0)));

            Add(checks, "XLine3: the same line", X(500, 0, 0, -1, 0, 0),
                ("the line turned round", X(0, 0, 0, 1, 0, 0), 0, Pts()));

            Add(checks, "Point3: off the line, ahead", P(50, 40, 30),
                ("ahead", X(0, 0, 0, 1, 0, 0), 50, null));

            Add(checks, "Point3: behind the origin", P(-30, 40, 30),
                ("behind", X(0, 0, 0, 1, 0, 0), 50, null));

            Add(checks, "Point3: on the line", P(7, 0, 0),
                ("on it", X(0, 0, 0, 1, 0, 0), 0, null));

            return checks;
        }

        #endregion
    }
}
