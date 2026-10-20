using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Probe;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A spline in space asked the five queries about each of the shapes it meets, from either side, with the numbers worked out by
    /// hand: a spline through the shape, one beside it, one along a side or touching an end of it, one a thousandth off, and where the
    /// shape is a body, one inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three splines carry the cases. The ring is the circle of radius 50 about the origin in the floor as
    /// <see cref="GeoSpline3.FromCircle"/> makes it, from (50, 0, 0) counter-clockwise, so that its crossings come in the order of the
    /// angle. The arch is the cubic with the control points (0, 0, 0), (100, 0, 150), (200, 0, 150), (300, 0, 0), which is the parabola
    /// z = x (300 - x) / 200 standing in the plane y = 0 with its top at (150, 0, 112.5). The corner is the polyline (0, 0, 0),
    /// (100, 0, 0), (100, 50, 0) as a spline of degree one.
    /// </para>
    /// <para>
    /// A pair is held to what the other shapes in space are held to (rule R): a flat shape, a triangle, a polygon, a face, is filled, and
    /// a circle or an ellipse is the disc it is for the distance, so a spline that pierces one, or lies in it, is nought from it; the
    /// crossings of a disc are those of its rim, of a flat shape those of its surface and its outline; a body, a box, a solid or a ball,
    /// is material, so a spline inside is nought from it and its shortest segment runs out to the skin; a curved chain and a curved loop
    /// are the paths they are. A collision is a distance of no more than the point tolerance, exactly, and the planar tolerance is never
    /// read. Asked from either side the two say the same to the bit: the same distance, the same collision, the same crossings in the
    /// same order, and a shortest segment turned round. Where a member is missing the case fails with its name, since the members are
    /// found by reflection.
    /// </para>
    /// <para>
    /// Half a thousandth and two thousandths sit either side of the point tolerance, a thousandth, a factor of two clear of it.
    /// </para>
    /// </remarks>
    public class Spline3AgainstShapesTests
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
        public void TheSplineAndTheShapeInSpace_AgreeFromEitherSide_OnTheNumbersWorkedOutByHand(string row)
        {
            Checks[row]();
        }

        #region Fixtures

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoVector3 V(double x, double y, double z) => new GeoVector3(x, y, z).Normalize();

        private static GeoVector3 X => GeoVector3.XAxis;

        private static GeoVector3 Y => GeoVector3.YAxis;

        private static GeoVector3 Z => GeoVector3.ZAxis;

        /// <summary>The circle of radius 50 about the origin in the floor, from (50, 0, 0) counter-clockwise: 0.25 of the domain is (0, 50, 0). Made in the plane and laid in the floor, since a circle in space starts where its own frame says.</summary>
        private static GeoSpline3 Ring() => GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 50)).ToSpline3(GeoCoordinateSystem3.Global);

        /// <summary>A circle of radius r about the origin in the floor, from (r, 0, 0) counter-clockwise.</summary>
        private static GeoSpline3 Round(double r) => GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), r)).ToSpline3(GeoCoordinateSystem3.Global);

        /// <summary>The parabola z = x (300 - x) / 200 in the plane y = 0, from (0, 0, 0) to (300, 0, 0), a cubic: x = 300 t, z = 450 t (1 - t).</summary>
        private static GeoSpline3 Arch() => new GeoSpline3(3, new[] { P(0, 0, 0), P(100, 0, 150), P(200, 0, 150), P(300, 0, 0) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

        /// <summary>The polyline (0, 0, 0), (100, 0, 0), (100, 50, 0) as a spline of degree one.</summary>
        private static GeoSpline3 Corner() => GeoSpline3.FromPolyline(new GeoPolyline3(P(0, 0, 0), P(100, 0, 0), P(100, 50, 0)));

        private static GeoPoint3[] Pts(params double[] xyz)
        {
            var points = new GeoPoint3[xyz.Length / 3];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = P(xyz[3 * i], xyz[3 * i + 1], xyz[3 * i + 2]);
            }

            return points;
        }

        private static double Root(double value) => Math.Sqrt(value);

        private static GeoPolygon3 Poly(params double[] xyz) => new GeoPolygon3(Pts(xyz));

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(P(0.5 * (x0 + x1), 0.5 * (y0 + y1), 0.5 * (z0 + z1)), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        #endregion

        #region Judging

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static void AssertSamePoints(GeoPoint3[] expected, GeoPoint3[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " points " + string.Join(" ", actual) + ", " + expected.Length + " expected " + string.Join(" ", expected));

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i].DistanceTo(actual[i]) <= 1E-6, what + ": point " + i + " is " + actual[i] + ", " + expected[i] + " expected");
            }
        }

        private static void AssertSameBits(GeoPoint3[] expected, GeoPoint3[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " points and " + expected.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(Bits(expected[i].X) == Bits(actual[i].X) && Bits(expected[i].Y) == Bits(actual[i].Y) && Bits(expected[i].Z) == Bits(actual[i].Z), what + ": point " + i + " " + expected[i] + " and " + actual[i]);
            }
        }

        private static bool SameBits(GeoPoint3 a, GeoPoint3 b) => Bits(a.X) == Bits(b.X) && Bits(a.Y) == Bits(b.Y) && Bits(a.Z) == Bits(b.Z);

        private static void Verify(string name, GeoSpline3 spline, object shape, double distance, GeoPoint3[] crossings, double? shortest)
        {
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
            GeoPoint3[] fromSplineSide = Crossings(spline, shape, Strict);
            GeoPoint3[] fromShapeSide = Crossings(shape, spline, Strict);

            if (crossings != null)
            {
                AssertSamePoints(crossings, fromSplineSide, name + " from the spline");
            }

            AssertSameBits(fromSplineSide, fromShapeSide, name + " both sides");
            AssertSameBits(Crossings(spline, shape, Tolerance.Global), Crossings(spline, shape), name + " plain form");
            AssertSameBits(Crossings(shape, spline, Tolerance.Global), Crossings(shape, spline), name + " plain form, the shape's");

            // TryIntersectWith says whether there are any, and gives them.
            bool tried = Try(spline, shape, out GeoPoint3[] points, Strict);
            bool triedBack = Try(shape, spline, out GeoPoint3[] pointsBack, Strict);

            Assert.Equal(fromSplineSide.Length > 0, tried);
            Assert.Equal(fromSplineSide.Length > 0, triedBack);
            AssertSameBits(fromSplineSide, points, name + " Try");
            AssertSameBits(fromSplineSide, pointsBack, name + " Try from the shape");
            Assert.Equal(tried, Try(spline, shape, out GeoPoint3[] plain));
            AssertSameBits(points, plain, name + " Try, plain form");

            // Shortest segment: leaves the spline and lands on the shape, as long as the distance (or as the gap to the skin of a body the
            // spline lies in); the shape gives the same segment turned round, to the bit.
            GeoLine3 leaving = Shortest(spline, shape, Strict);
            GeoLine3 landing = Shortest(shape, spline, Strict);
            double length = shortest ?? distance;

            Assert.InRange(leaving.Length, length - 1E-9, length + 1E-9);
            Assert.True(SameBits(leaving.StartPoint, landing.EndPoint) && SameBits(leaving.EndPoint, landing.StartPoint), name + ": the segments are not the same turned round");
            Assert.True(spline.DistanceTo(leaving.StartPoint) <= 1E-9 * Math.Max(1.0, Math.Abs(leaving.StartPoint.X) + Math.Abs(leaving.StartPoint.Y) + Math.Abs(leaving.StartPoint.Z)), name + ": the shortest segment starts off the spline at " + leaving.StartPoint);
            Assert.Equal(Bits(Shortest(spline, shape, Tolerance.Global).Length), Bits(Shortest(spline, shape).Length));
            Assert.Equal(Bits(Shortest(shape, spline, Tolerance.Global).Length), Bits(Shortest(shape, spline).Length));
        }

        private static void Add(Dictionary<string, Action> checks, string shapeName, object shape, params (string label, GeoSpline3 spline, double distance, GeoPoint3[] crossings, double? shortest)[] cases)
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
            GeoSpline3 ring = Ring();
            GeoSpline3 arch = Arch();
            GeoSpline3 corner = Corner();
            double at30 = Root(2500 - 900); // 40: where the ring meets y = 30 and x = 30
            double at10 = Root(2500 - 100); // 48.9898: where the ring meets y = 10
            double cross = 50.0 * Root(5.0); // the arch meets z = 50 at x = 150 -+ 50 sqrt 5

            // A segment: the ring is crossed twice by y = 30, touches y = 50 at the top, and ends of the segment can lie on it.
            Add(checks, "Line3", new GeoLine3(P(-100, 30, 0), P(100, 30, 0)),
                ("the ring through it", ring, 0, Pts(at30, 30, 0, -at30, 30, 0), null));
            Add(checks, "Line3: stopping inside the ring", new GeoLine3(P(-100, 30, 0), P(-30, 30, 0)),
                ("only one crossing is reached", ring, 0, Pts(-at30, 30, 0), null));
            Add(checks, "Line3: beside the ring", new GeoLine3(P(60, -100, 0), P(60, 100, 0)),
                ("ten from it", ring, 10, Pts(), null));
            Add(checks, "Line3: inside the ring", new GeoLine3(P(-20, 0, 0), P(20, 0, 0)),
                ("thirty from either side, the lower knot first", ring, 30, Pts(), null));
            Add(checks, "Line3: touching the top", new GeoLine3(P(-100, 50, 0), P(100, 50, 0)),
                ("tangent at (0, 50, 0)", ring, 0, Pts(0, 50, 0), null));
            Add(checks, "Line3: half a thousandth over the top", new GeoLine3(P(-100, 50, 0.0005), P(100, 50, 0.0005)),
                ("touches within the point tolerance, at the top", ring, 0.0005, Pts(0, 50, 0), null));
            Add(checks, "Line3: two thousandths over the top", new GeoLine3(P(-100, 50, 0.002), P(100, 50, 0.002)),
                ("does not touch", ring, 0.002, Pts(), null));
            Add(checks, "Line3: an end on the ring", new GeoLine3(P(50, 0, 0), P(120, 0, 0)),
                ("starts on the start of the ring, which is its end too, one place", ring, 0, Pts(50, 0, 0), null));
            Add(checks, "Line3: up through the ring", new GeoLine3(P(40, 30, -10), P(40, 30, 10)),
                ("pierces the floor on the ring", ring, 0, Pts(40, 30, 0), null));
            Add(checks, "Line3: through the middle", new GeoLine3(P(0, 0, -10), P(0, 0, 10)),
                ("fifty from every point of the ring: the lowest knot, the start, first", ring, 50, Pts(), null));
            Add(checks, "Line3: lifted over the ring", new GeoLine3(P(-100, 30, 5), P(100, 30, 5)),
                ("five above the two places the ring meets y = 30, the lower knot first", ring, 5, Pts(), null));
            Add(checks, "Line3: across the arch", new GeoLine3(P(150, -50, 112.5), P(150, 50, 112.5)),
                ("through the top", arch, 0, Pts(150, 0, 112.5), null));
            Add(checks, "Line3: beside the arch", new GeoLine3(P(150, 10, 50), P(150, 10, 200)),
                ("ten from the top, square to the plane of the arch", arch, 10, Pts(), null));
            Add(checks, "Line3: ending on the arch's end", new GeoLine3(P(300, 0, 0), P(400, 0, 0)),
                ("meets the end of the arch", arch, 0, Pts(300, 0, 0), null));
            Add(checks, "Line3: along the first leg", new GeoLine3(P(-50, 0, 0), P(150, 0, 0)),
                ("the corner leaves the line at (100, 0, 0); the leg along it is not crossing it", corner, 0, Pts(100, 0, 0), 0));
            Add(checks, "Line3: along part of the first leg", new GeoLine3(P(20, 0, 0), P(60, 0, 0)),
                ("inside the leg: nought, collides, no crossing", corner, 0, Pts(), 0));

            // A ray.
            Add(checks, "Ray3", new GeoRay3(P(100, 10, 0), -X),
                ("running left through the ring", ring, 0, Pts(at10, 10, 0, -at10, 10, 0), null));
            Add(checks, "Ray3: pointing away", new GeoRay3(P(60, 10, 0), X),
                ("starts outside and runs off", ring, Root(3700) - 50, Pts(), null));
            Add(checks, "Ray3: from inside", new GeoRay3(P(0, 10, 0), X),
                ("leaves the ring once", ring, 0, Pts(at10, 10, 0), null));
            Add(checks, "Ray3: down on the arch", new GeoRay3(P(150, 0, 300), -Z),
                ("comes down on the top", arch, 0, Pts(150, 0, 112.5), null));
            Add(checks, "Ray3: up from the arch", new GeoRay3(P(150, 0, 120), Z),
                ("starts 7.5 over the top and runs away", arch, 7.5, Pts(), null));
            Add(checks, "Ray3: over the ring", new GeoRay3(P(0, 0, 10), X),
                ("ten above the start of the ring, in the plane x-z", ring, 10, Pts(), null));

            // A line without ends.
            Add(checks, "XLine3", new GeoXLine3(P(-100, 30, 0), X),
                ("through the ring", ring, 0, Pts(at30, 30, 0, -at30, 30, 0), null));
            Add(checks, "XLine3: turned round", new GeoXLine3(P(500, 30, 0), -X),
                ("through the ring, from the other end", ring, 0, Pts(at30, 30, 0, -at30, 30, 0), null));
            Add(checks, "XLine3: beside the ring", new GeoXLine3(P(-100, 80, 0), X),
                ("thirty over the top", ring, 30, Pts(), null));
            Add(checks, "XLine3: touching the top", new GeoXLine3(P(-100, 50, 0), X),
                ("tangent", ring, 0, Pts(0, 50, 0), null));
            Add(checks, "XLine3: far along", new GeoXLine3(P(1E6, 30, 0), -X),
                ("through the ring from a million away", ring, 0, Pts(at30, 30, 0, -at30, 30, 0), null));
            Add(checks, "XLine3: across the arch", new GeoXLine3(P(-50, 0, 100), X),
                ("at z = 100: x = 100 and x = 200", arch, 0, Pts(100, 0, 100, 200, 0, 100), null));
            Add(checks, "XLine3: touching the arch's top", new GeoXLine3(P(-50, 0, 112.5), X),
                ("tangent at (150, 0, 112.5)", arch, 0, Pts(150, 0, 112.5), null));
            Add(checks, "XLine3: half a unit over the arch", new GeoXLine3(P(-50, 0, 113), X),
                ("0.5 over the top", arch, 0.5, Pts(), null));
            Add(checks, "XLine3: along the first leg", new GeoXLine3(P(-50, 0, 0), X),
                ("the line carries the first leg: nought, and one crossing where the corner leaves it", corner, 0, Pts(100, 0, 0), 0));
            Add(checks, "XLine3: skew over the ring", new GeoXLine3(P(30, 0, 7), Y),
                ("seven above the two places the ring has x = 30, the lower knot first", ring, 7, Pts(), null));

            // A plane.
            Add(checks, "Plane3: standing across the ring", new GeoPlane3(P(30, 0, 0), X),
                ("x = 30 cuts the ring at (30, 40, 0) and (30, -40, 0)", ring, 0, Pts(30, 40, 0, 30, -40, 0), null));
            Add(checks, "Plane3: above the ring", new GeoPlane3(P(0, 0, 5), Z),
                ("five above every point of the ring, the start first", ring, 5, Pts(), null));
            Add(checks, "Plane3: the floor", new GeoPlane3(P(0, 0, 0), Z),
                ("the ring lies in it: nought, collides, no crossing", ring, 0, Pts(), 0));
            Add(checks, "Plane3: touching the top", new GeoPlane3(P(0, 50, 0), Y),
                ("y = 50 touches the ring at (0, 50, 0)", ring, 0, Pts(0, 50, 0), null));
            Add(checks, "Plane3: half a thousandth past the top", new GeoPlane3(P(0, 50.0005, 0), Y),
                ("touches within the point tolerance, at the top", ring, 0.0005, Pts(0, 50, 0), null));
            Add(checks, "Plane3: two thousandths past the top", new GeoPlane3(P(0, 50.002, 0), Y),
                ("does not touch", ring, 0.002, Pts(), null));
            Add(checks, "Plane3: slanted through the ring", new GeoPlane3(P(0, 0, 0), V(1, 0, 1)),
                ("x + z = 0 cuts the floor along x = 0: (0, 50, 0) then (0, -50, 0)", ring, 0, Pts(0, 50, 0, 0, -50, 0), null));
            Add(checks, "Plane3: tilted every way", new GeoPlane3(P(0, 0, 0), V(1, 1, 1)),
                ("x + y + z = 0 cuts the floor along y = -x", ring, 0, Pts(-50 / Root(2), 50 / Root(2), 0, 50 / Root(2), -50 / Root(2), 0), null));
            Add(checks, "Plane3: standing across the arch", new GeoPlane3(P(150, 0, 0), X),
                ("x = 150 cuts the arch once, at the top", arch, 0, Pts(150, 0, 112.5), null));
            Add(checks, "Plane3: through the arch", new GeoPlane3(P(0, 0, 100), Z),
                ("z = 100 cuts the arch at x = 100 and x = 200", arch, 0, Pts(100, 0, 100, 200, 0, 100), null));
            Add(checks, "Plane3: touching the arch's top", new GeoPlane3(P(0, 0, 112.5), Z),
                ("tangent at (150, 0, 112.5)", arch, 0, Pts(150, 0, 112.5), null));
            Add(checks, "Plane3: half a unit over the arch", new GeoPlane3(P(0, 0, 113), Z),
                ("0.5 over the top", arch, 0.5, Pts(), null));

            // A polyline.
            Add(checks, "Polyline3", new GeoPolyline3(P(-100, -30, 0), P(100, -30, 0), P(100, 30, 0)),
                ("through the bottom of the ring", ring, 0, Pts(-at30, -30, 0, at30, -30, 0), null));
            Add(checks, "Polyline3: round the ring at thirty", new GeoPolyline3(P(-80, -80, 0), P(-80, 80, 0), P(80, 80, 0)),
                ("thirty from the top and from the left", ring, 30, Pts(), null));
            Add(checks, "Polyline3: round the ring, four up", new GeoPolyline3(P(-80, -80, 4), P(-80, 80, 4), P(80, 80, 4)),
                ("thirty across and four up from the left side", ring, Root(900 + 16), Pts(), null));
            Add(checks, "Polyline3: across the arch", new GeoPolyline3(P(50, 0, 0), P(50, 0, 200), P(250, 0, 200), P(250, 0, 0)),
                ("up a side, over, and down", arch, 0, Pts(50, 0, 62.5, 250, 0, 62.5), null));

            // A polygon and a triangle: flat and filled, so a spline lying in one, or piercing it, is nought from it.
            GeoPolygon3 square = Poly(0, 0, 0, 100, 0, 0, 100, 100, 0, 0, 100, 0);

            Add(checks, "Polygon3", square,
                ("the ring through its bottom and its left side; the arc between them lies in it", ring, 0, Pts(50, 0, 0, 0, 50, 0), 0));
            Add(checks, "Polygon3: round the ring", Poly(-100, -100, 0, 100, -100, 0, 100, 100, 0, -100, 100, 0),
                ("the ring lies in it: nought, and no length to the shortest segment", ring, 0, Pts(), 0));
            Add(checks, "Polygon3: five over the ring", Poly(0, 0, 5, 100, 0, 5, 100, 100, 5, 0, 100, 5),
                ("five above the arc of the ring that lies under it, the start first", ring, 5, Pts(), null));
            Add(checks, "Polygon3: beside the ring", Poly(100, 0, 0, 200, 0, 0, 200, 100, 0, 100, 100, 0),
                ("50 from the ring", ring, 50, Pts(), null));
            Add(checks, "Polygon3: standing across the ring", Poly(-100, 30, -10, 100, 30, -10, 100, 30, 10, -100, 30, 10),
                ("the ring pierces it at (40, 30, 0) and (-40, 30, 0)", ring, 0, Pts(at30, 30, 0, -at30, 30, 0), 0));
            Add(checks, "Polygon3: standing across half the ring", Poly(0, 30, -10, 100, 30, -10, 100, 30, 10, 0, 30, 10),
                ("the ring pierces it only at (40, 30, 0)", ring, 0, Pts(at30, 30, 0), 0));
            Add(checks, "Polygon3: across the arch", Poly(0, -10, 50, 300, -10, 50, 300, 10, 50, 0, 10, 50),
                ("the arch pierces z = 50 at x = 150 -+ 50 sqrt 5", arch, 0, Pts(150 - cross, 0, 50, 150 + cross, 0, 50), 0));
            Add(checks, "Triangle3", new GeoTriangle3(P(0, 0, 0), P(100, 0, 0), P(0, 100, 0)),
                ("the ring through its bottom and its left side, not its slope", ring, 0, Pts(50, 0, 0, 0, 50, 0), 0));
            Add(checks, "Triangle3: beside the ring", new GeoTriangle3(P(60, 60, 0), P(160, 60, 0), P(60, 160, 0)),
                ("the corner (60, 60, 0) is 84.85 from the centre", ring, Root(7200) - 50, Pts(), null));
            Add(checks, "Triangle3: standing across the arch", new GeoTriangle3(P(150, -20, 50), P(150, 20, 50), P(150, 0, 200)),
                ("the arch pierces it at the top", arch, 0, Pts(150, 0, 112.5), 0));

            // A face: the material is the square less the hole.
            GeoFace3 plate = new GeoFace3(Poly(-100, -100, 0, 100, -100, 0, 100, 100, 0, -100, 100, 0), new[] { Poly(-20, -20, 0, 20, -20, 0, 20, 20, 0, -20, 20, 0) });

            Add(checks, "Face3", plate,
                ("the ring lies in the material: nought, and no length to the shortest segment", ring, 0, Pts(), 0));
            Add(checks, "Face3: in the hole", plate,
                ("a ring of radius 5 in the hole of 40 by 40 is 15 from its rim, the start first", Round(5), 15, Pts(), null));
            Add(checks, "Face3: through the hole's rim", plate,
                ("a ring of radius 25 crosses the rim of the hole eight times", Round(25), 0,
                    Pts(20, 15, 0, 15, 20, 0, -15, 20, 0, -20, 15, 0, -20, -15, 0, -15, -20, 0, 15, -20, 0, 20, -15, 0), 0));
            Add(checks, "Face3: standing across the ring", new GeoFace3(Poly(-100, 30, -30, 100, 30, -30, 100, 30, 30, -100, 30, 30), new[] { Poly(-20, 30, -10, 20, 30, -10, 20, 30, 10, -20, 30, 10) }),
                ("the ring pierces the material at (40, 30, 0) and (-40, 30, 0), clear of the hole", ring, 0, Pts(at30, 30, 0, -at30, 30, 0), 0));
            Add(checks, "Face3: standing, the ring through the hole", new GeoFace3(Poly(-100, 0, -30, 100, 0, -30, 100, 0, 30, -100, 0, 30), new[] { Poly(40, 0, -10, 60, 0, -10, 60, 0, 10, 40, 0, 10) }),
                ("the ring pierces the plane y = 0 at (50, 0, 0) in the middle of the hole, 10 from its rim, and at (-50, 0, 0) in the material", ring, 0, Pts(-50, 0, 0), 0));

            // A box, an oriented box and a solid: material, so a spline inside is nought from it and 10 from the skin.
            Add(checks, "Aabb3", new GeoAabb3(P(0, 0, -10), P(100, 100, 10)),
                ("the ring through the face y = 0 and the face x = 0, the arc between them inside", ring, 0, Pts(50, 0, 0, 0, 50, 0), 0));
            Add(checks, "Aabb3: round the ring", new GeoAabb3(P(-100, -100, -10), P(100, 100, 10)),
                ("the ring is inside: nought, and 10 to the floor and the roof", ring, 0, Pts(), 10));
            Add(checks, "Aabb3: beside the ring", new GeoAabb3(P(100, -10, -10), P(150, 10, 10)),
                ("50 from the ring", ring, 50, Pts(), null));
            Add(checks, "Aabb3: over the ring", new GeoAabb3(P(-10, -10, 20), P(10, 10, 40)),
                ("the corner (10, 10, 20) is nearest the ring at 45 degrees, 41.06 away, as is each of the other three corners; the lowest first", ring, Root(3100 - 1000 * Root(2)), Pts(), null));
            Add(checks, "Aabb3: over the arch", new GeoAabb3(P(100, -10, 100), P(200, 10, 150)),
                ("the arch enters through the face x = 100 and leaves through x = 200, both at z = 100", arch, 0, Pts(100, 0, 100, 200, 0, 100), 0));
            Add(checks, "Obb3: turned on its end", new GeoObb3(P(80, 0, 0), 20, 20, 20, V(1, 1, 0), V(-1, 1, 0)),
                ("the corner (80 - 10 sqrt 2, 0, 0) is 30 - 10 sqrt 2 from the ring", ring, 30 - 10 * Root(2), Pts(), null));
            Add(checks, "Obb3: round the ring", new GeoObb3(P(0, 0, 0), 200, 200, 40, V(Root(3), 1, 0), V(-1, Root(3), 0)),
                ("the ring is inside a box turned 30 degrees: nought, and 20 to the floor and the roof", ring, 0, Pts(), 20));
            Add(checks, "Solid3", Box(0, 0, -10, 100, 100, 10),
                ("the ring through its face y = 0 and its face x = 0, as for the box", ring, 0, Pts(50, 0, 0, 0, 50, 0), 0));
            Add(checks, "Solid3: round the ring", Box(-100, -100, -10, 100, 100, 10),
                ("the ring is inside: nought, and 10 to the skin", ring, 0, Pts(), 10));
            Add(checks, "Solid3: under the arch", Box(-50, -10, -30, 350, 10, -10),
                ("the box's roof is 10 under both ends of the arch, the start first", arch, 10, Pts(), null));

            // A plate with an opening: the opening is no material, so a spline through it is 10 from the walls and one through the plate crosses both faces.
            GeoSolid3 plate3 = Box(-50, -50, -5, 50, 50, 5).WithOpenings(new[] { Box(-10, -10, -6, 10, 10, 6) });

            Add(checks, "Solid3: a plate with an opening", plate3,
                ("a line down the middle of the opening is 10 from its walls all the way, the lowest first", GeoSpline3.FromPolyline(new GeoPolyline3(P(0, 0, -20), P(0, 0, 20))), 10, Pts(), null));
            Add(checks, "Solid3: a plate with an opening, through the material", plate3,
                ("a line down at x = 30 pierces the floor and the roof of the plate", GeoSpline3.FromPolyline(new GeoPolyline3(P(30, 0, -20), P(30, 0, 20))), 0, Pts(30, 0, -5, 30, 0, 5), 0));

            // A ball.
            Add(checks, "Sphere3: inside the ring", new GeoSphere3(P(0, 0, 0), 30),
                ("twenty off the ring everywhere, the start first", ring, 20, Pts(), null));
            Add(checks, "Sphere3: round the ring", new GeoSphere3(P(0, 0, 0), 60),
                ("the ring is inside: nought, and 10 to the shell", ring, 0, Pts(), 10));
            Add(checks, "Sphere3: the ring on its shell", new GeoSphere3(P(0, 0, 0), 50),
                ("the ring lies on the surface: nought, no crossing", ring, 0, Pts(), 0));
            Add(checks, "Sphere3: through the ring", new GeoSphere3(P(50, 0, 0), 10),
                ("the ring enters the ball of radius 10 about its start and leaves it: (49, 9.95, 0) and (49, -9.95, 0)", ring, 0, Pts(49, Root(99), 0, 49, -Root(99), 0), 0));
            Add(checks, "Sphere3: over the arch", new GeoSphere3(P(150, 0, 150), 30),
                ("37.5 from the centre to the top, less the radius", arch, 7.5, Pts(), null));
            Add(checks, "Sphere3: touching the top", new GeoSphere3(P(150, 0, 142.5), 30),
                ("tangent from above at (150, 0, 112.5)", arch, 0, Pts(150, 0, 112.5), null));

            // An arc, a circle, an ellipse: curved. A circle and an ellipse are discs for the distance and rims for the crossings.
            Add(checks, "Arc3", new GeoArc3(P(60, 0, 0), Z, 80, 0, Math.PI),
                ("the half of the circle of radius 80 about (60, 0, 0) that lies left of it, which an arc about Z turns to from +Y through -X, crosses the ring where the two circles do", ring, 0, Pts(-2.5, Root(2500 - 6.25), 0, -2.5, -Root(2500 - 6.25), 0), null));
            Add(checks, "Arc3: level with the ring", new GeoArc3(P(0, 0, 0), Z, 80, 0, Math.PI / 2),
                ("thirty from the ring everywhere: the start first", ring, 30, Pts(), null));
            Add(checks, "Circle3", new GeoCircle3(P(60, 0, 0), Z, 30),
                ("the two circles meet at x = 43.33", ring, 0, Pts(130.0 / 3.0, Root(2500 - 130.0 * 130.0 / 9.0), 0, 130.0 / 3.0, -Root(2500 - 130.0 * 130.0 / 9.0), 0), null));
            Add(checks, "Circle3: round the ring", new GeoCircle3(P(0, 0, 0), Z, 100),
                ("the ring lies in the disc: nought, no length to the shortest segment, no crossing", ring, 0, Pts(), 0));
            Add(checks, "Circle3: beside the ring", new GeoCircle3(P(200, 0, 0), Z, 50),
                ("100 apart", ring, 100, Pts(), null));
            Add(checks, "Circle3: touching the ring", new GeoCircle3(P(100, 0, 0), Z, 50),
                ("tangent at (50, 0, 0)", ring, 0, Pts(50, 0, 0), null));
            Add(checks, "Circle3: standing, the ring through its disc", new GeoCircle3(P(50, 0, 5), Y, 10),
                ("the ring pierces the disc at (50, 0, 0) and meets no rim: nought, collides, no crossing", ring, 0, Pts(), 0));
            Add(checks, "Circle3: standing clear of the ring", new GeoCircle3(P(50, 0, 20), Y, 10),
                ("the disc's lowest point (50, 0, 10) is 10 over the ring", ring, 10, Pts(), null));
            Add(checks, "Ellipse3", new GeoEllipse3(P(0, 0, 0), Z, X, 100, 20),
                ("the ring crosses the long thin ellipse four times", ring, 0, Pts(Root(2187.5), Root(312.5), 0, -Root(2187.5), Root(312.5), 0, -Root(2187.5), -Root(312.5), 0, Root(2187.5), -Root(312.5), 0), null));
            Add(checks, "Ellipse3: inside the ring", new GeoEllipse3(P(0, 0, 0), Z, X, 30, 20),
                ("20 from the end of the ellipse", ring, 20, Pts(), null));
            Add(checks, "Ellipse3: round the ring", new GeoEllipse3(P(0, 0, 0), Z, X, 100, 60),
                ("the ring lies in the disc of the ellipse", ring, 0, Pts(), 0));

            // An edge, a chain, a loop.
            Add(checks, "Edge3: straight", new GeoEdge3(P(60, -100, 0), P(60, 100, 0)),
                ("ten from the ring", ring, 10, Pts(), null));
            Add(checks, "Edge3: curved", new GeoEdge3(new GeoArc3(P(60, 0, 0), Z, 80, 0, Math.PI)),
                ("crosses the ring twice", ring, 0, Pts(-2.5, Root(2500 - 6.25), 0, -2.5, -Root(2500 - 6.25), 0), null));
            Add(checks, "PolylineArc3: a half circle far off", new GeoPolylineArc3(new[] { P(-100, 0, 0), P(100, 0, 0) }, new[] { 1.0 }, new[] { Z }),
                ("a half circle of radius 100 about the origin: 50 from the ring", ring, 50, Pts(), null));
            Add(checks, "PolygonArc3: a loop is its outline", new GeoPolygonArc3(new[] { P(-100, -100, 0), P(100, -100, 0), P(100, 100, 0), P(-100, 100, 0) }),
                ("the ring inside the square outline is 50 from it, not nought: a curved loop is not filled", ring, 50, Pts(), null));

            // Another spline.
            var twin = new GeoSpline3(3, new[] { P(0, 0, 100), P(100, 0, -50), P(200, 0, -50), P(300, 0, 100) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

            Add(checks, "Spline3: crossing", twin,
                ("the arch and its upside down twin cross at z = 50", arch, 0, Pts(150 - cross, 0, 50, 150 + cross, 0, 50), null));
            Add(checks, "Spline3: apart", arch + new GeoVector3(0, 0, 20),
                ("a copy 20 over it is not 20 away: its start is 11.3358552330596 from the arch", arch, 11.3358552330596365, Pts(), null));
            Add(checks, "Spline3: the same", arch.Clone(),
                ("a copy lies along it: nought, collides, no crossing", arch, 0, Pts(), 0));
            Add(checks, "Spline3: two lines crossing", GeoSpline3.FromPolyline(new GeoPolyline3(P(0, 100, 0), P(100, 0, 100))),
                ("the diagonals cross at (50, 50, 50)", GeoSpline3.FromPolyline(new GeoPolyline3(P(0, 0, 0), P(100, 100, 100))), 0, Pts(50, 50, 50), null));
            Add(checks, "Spline3: two lines passing", GeoSpline3.FromPolyline(new GeoPolyline3(P(50, -50, 10), P(50, 50, 10))),
                ("one along X and one along Y ten above it: ten apart, the lower knot first", GeoSpline3.FromPolyline(new GeoPolyline3(P(0, 0, 0), P(100, 0, 0))), 10, Pts(), null));
            Add(checks, "Spline3: a circle round the arch's top", GeoSpline3.FromCircle(new GeoCircle3(P(150, 0, 155), Y, 40)),
                ("the circle of radius 40 about (150, 0, 155) in the plane of the arch is 2.5 over its top", arch, 2.5, Pts(), null));

            return checks;
        }

        #endregion
    }
}
