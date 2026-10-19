using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.XLine2Probe;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The infinite line in the plane against random shapes of each of the fifteen kinds, every answer checked against a
    /// closed-form oracle that shares no code with the library, from both sides, and against the line taken as two rays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each case draws a shape and a line (anywhere near it, beside it, starting thousands of units away and running through
    /// it, or a hair off it) on a seeded generator, so a failure names its kind and its draw and repeats. The oracle
    /// (<see cref="OracleShape"/>) takes the boundary to pieces and reads the signed distance of the line over each piece in
    /// closed form: the distance is the least gap, the crossings are the roots. The distance is held to 1E-9, the
    /// collision to the distance being within a thousandth, the crossings to the same set within a millionth, the shortest
    /// line to being as long as the distance, starting on the line when it leaves the line and ending on it when it lands,
    /// and the other end on the boundary. Asked from the other side the distance and the collision are the same to the bit.
    /// </para>
    /// <para>
    /// Cases whose line passes within five thousandths of an end or a tangent of a piece are not asked the crossings, as the
    /// tolerance may rightly decide them; they are asked the distance all the same. The line is also asked as the two rays
    /// from its origin: its distance is the nearer of theirs, it collides when either does, and its crossings are theirs
    /// together.
    /// </para>
    /// </remarks>
    public class XLine2OracleFuzzTests
    {
        /// <summary>
        /// The draws per kind: 4 000, or what the environment variable XLINE_FUZZ_DRAWS says for a longer run by hand.
        /// </summary>
        private static readonly int Draws = int.TryParse(Environment.GetEnvironmentVariable("XLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 4000;

        public static TheoryData<string> Kinds()
        {
            var data = new TheoryData<string>();

            foreach (string kind in OracleShapes.Kinds)
            {
                data.Add(kind);
            }

            return data;
        }

        private static void AssertSameSet(GeoPoint2[] expected, GeoPoint2[] actual, double within, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " crossings (" + string.Join(" ", actual) + "), " + expected.Length + " expected (" + string.Join(" ", expected) + ")");

            foreach (GeoPoint2 point in expected)
            {
                Assert.True(actual.Any(p => p.DistanceTo(point) <= within), what + ": no crossing at " + point + " among " + string.Join(" ", actual) + ", the nearest " + actual.Min(p => p.DistanceTo(point)) + " off");
            }
        }

        private static bool OnTheLine(GeoXLine2 line, GeoPoint2 point, double within = 1E-7) => Math.Abs(OraclePiece.Signed(line, point)) <= within;

        [Theory]
        [MemberData(nameof(Kinds))]
        public void TheLineAgainstRandomShapes_AgreesWithTheOracle_AndFromEitherSide(string kind)
        {
            var random = new Random(20261020 + Array.IndexOf(OracleShapes.Kinds, kind));
            int touching = 0;
            int apart = 0;

            for (int draw = 0; draw < Draws; draw++)
            {
                OracleShape shape = OracleShapes.Draw(kind, random);
                GeoXLine2 line = OracleShapes.DrawLine(shape, random, draw);
                string at = kind + " draw " + draw + ": " + line + " against " + shape.Geo;

                double expected = shape.Distance(line);

                // A line crossing a nearly straight arc at a shallow angle puts its crossing a hundred times further off than the arc
                // itself is known, which on a circle of a radius of 1E11 is 4E-5.
                double crossTolerance = 1E-6 + 100 * shape.Imprecision;

                if (expected <= 1E-3)
                {
                    touching++;
                }
                else
                {
                    apart++;
                }

                // The distance, from both sides, to the bit; and the form without a tolerance as the global one.
                double distance = Distance(line, shape.Geo, Strict);

                Assert.True(Math.Abs(distance - expected) <= 1E-9 * (1.0 + expected) + shape.Imprecision, at + ": distance " + distance + ", the oracle says " + expected);
                Assert.True(Distance(shape.Geo, line, Strict).Equals(distance), at + ": distance from the shape is " + Distance(shape.Geo, line, Strict));
                Assert.True(Distance(line, shape.Geo).Equals(Distance(line, shape.Geo, Tolerance.Global)), at + ": the plain distance is not the global one");
                Assert.True(Distance(shape.Geo, line).Equals(Distance(shape.Geo, line, Tolerance.Global)), at + ": the plain distance from the shape is not the global one");

                // The same line drawn as a segment ten thousand either side of the shape, asked through the segment members, which
                // share no cut with the code's own. Not against a ray or a line: they may cross it further out than that.
                if (!(shape.Geo is GeoXLine2) && !(shape.Geo is GeoRay2))
                {
                    double centre = line.GetDistanceAtPoint(shape.Center);
                    GeoLine2 longCut = line.ToLine(centre - 10000, centre + 10000);
                    double viaSegment = Answers(longCut, "DistanceTo", shape.Geo) ? Distance(longCut, shape.Geo) : Distance(shape.Geo, longCut);

                    Assert.True(Math.Abs(viaSegment - distance) <= 1E-9 * (1.0 + distance) + shape.Imprecision, at + ": the long segment is " + viaSegment + " away");
                }

                // The collision, unless the gap is within a millionth of the tolerance.
                if (!(shape.Geo is GeoPoint2) && Math.Abs(expected - Strict.EqualPoint) > 1E-6)
                {
                    bool collides = expected <= Strict.EqualPoint;

                    Assert.True(collides == Collides(line, shape.Geo, Strict), at + ": collides is " + !collides + ", the gap being " + expected);
                    Assert.True(collides == Collides(shape.Geo, line, Strict), at + ": collides from the shape is " + !collides + ", the gap being " + expected);
                    Assert.True(Collides(line, shape.Geo) == Collides(line, shape.Geo, Tolerance.Global), at + ": the plain collision is not the global one");
                }

                // The shortest line, both ways: as long as the distance, one end on the line, the other on the boundary.
                GeoLine2 leaving = Shortest(line, shape.Geo, Strict);
                GeoLine2 landing = Shortest(shape.Geo, line, Strict);

                // Within the point tolerance of the shape but not on it, or passing within it of a corner, the segments' own shortest
                // line keeps its slack: it may be of no length where the distance keeps the gap, and sit on the shape a thousandth off
                // the line, or on the line a thousandth off the shape. An ellipse's nearest point is found numerically, and a point a hair
                // off the curve costs a hair squared in length, so there it is held to the curve within a twentieth.
                double least = expected > 0 && expected <= Strict.EqualPoint ? 0.0 : expected - 1E-8 * (1.0 + expected) - 100 * shape.Imprecision;
                bool slack = (expected > 0 && expected <= Strict.EqualPoint) || shape.Delicate(line, 2 * Strict.EqualPoint);
                double onTheCurve = (kind == "Ellipse2" ? 0.05 : slack ? Strict.EqualPoint + 1E-6 : 1E-6) + 100 * shape.Imprecision;
                double onTheLine = slack ? Strict.EqualPoint + 1E-6 : 1E-7;

                Assert.True(leaving.Length >= least && leaving.Length <= expected + 1E-8 * (1.0 + expected) + 100 * shape.Imprecision, at + ": the shortest line is " + leaving.Length + " long, the gap " + expected);
                Assert.True(landing.Length >= least && landing.Length <= expected + 1E-8 * (1.0 + expected) + 100 * shape.Imprecision, at + ": the shortest line from the shape is " + landing.Length + " long, the gap " + expected);
                Assert.True(OnTheLine(line, leaving.StartPoint, onTheLine), at + ": the shortest line leaves the line from " + leaving.StartPoint);
                Assert.True(OnTheLine(line, landing.EndPoint, onTheLine), at + ": the shortest line lands on the line at " + landing.EndPoint);
                Assert.True(shape.DistanceFromBoundary(leaving.EndPoint) <= onTheCurve, at + ": the shortest line lands off the shape at " + leaving.EndPoint + ", the gap being " + expected);
                Assert.True(shape.DistanceFromBoundary(landing.StartPoint) <= onTheCurve, at + ": the shortest line leaves the shape from " + landing.StartPoint + ", the gap being " + expected);
                Assert.True(Shortest(line, shape.Geo).Equals(Shortest(line, shape.Geo, Tolerance.Global)), at + ": the plain shortest line is not the global one");

                // From either side the crossings, and the point of a crossing with a segment, a ray or another line, are the same to
                // the bit: both sides ask the one kernel. So is the shortest line to a point, a segment, a ray or a line, turned round.
                if (Answers(line, "GetIntersections", shape.Geo))
                {
                    Assert.True(Crossings(line, shape.Geo, Strict).SequenceEqual(Crossings(shape.Geo, line, Strict)), at + ": the crossings differ to the bit from the two sides");
                }

                if (HasTry(line, shape.Geo))
                {
                    bool onePoint = Try(line, shape.Geo, out GeoPoint2[] fromLineSide, Strict);
                    bool otherPoint = Try(shape.Geo, line, out GeoPoint2[] fromShapeSide, Strict);

                    Assert.True(onePoint == otherPoint && fromLineSide.SequenceEqual(fromShapeSide), at + ": TryIntersectWith differs to the bit from the two sides");
                }

                if (shape.Geo is GeoPoint2 || shape.Geo is GeoLine2 || shape.Geo is GeoRay2 || shape.Geo is GeoXLine2)
                {
                    Assert.True(leaving.StartPoint.Equals(landing.EndPoint) && leaving.EndPoint.Equals(landing.StartPoint), at + ": the shortest lines are not each other turned round");
                }

                bool delicate = shape.Delicate(line, 5E-3);

                // The crossings, where the line is not so near an end that the tolerance rightly decides.
                GeoPoint2[] crossings = shape.Crossings(line);

                if (!delicate && !shape.Ambiguous)
                {

                    if (Answers(line, "GetIntersections", shape.Geo))
                    {
                        GeoPoint2[] fromLine = Crossings(line, shape.Geo, Strict);
                        GeoPoint2[] fromShape = Crossings(shape.Geo, line, Strict);

                        AssertSameSet(crossings, fromLine, crossTolerance, at + " crossings from the line");
                        AssertSameSet(fromLine, fromShape, crossTolerance, at + " crossings from the shape");
                        Assert.True(Crossings(line, shape.Geo).SequenceEqual(Crossings(line, shape.Geo, Tolerance.Global)), at + ": the plain crossings are not the global ones");
                    }

                    if (HasTry(line, shape.Geo))
                    {
                        bool hit = Try(line, shape.Geo, out GeoPoint2[] points, Strict);
                        bool hitBack = Try(shape.Geo, line, out GeoPoint2[] pointsBack, Strict);

                        Assert.True((crossings.Length > 0) == hit, at + ": TryIntersectWith says " + hit + ", the oracle finds " + crossings.Length);
                        Assert.True(hit == hitBack, at + ": TryIntersectWith from the shape says " + hitBack);
                        AssertSameSet(crossings, points, crossTolerance, at + " Try from the line");
                        AssertSameSet(points, pointsBack, crossTolerance, at + " Try from the shape");
                        Assert.True(Try(line, shape.Geo, out GeoPoint2[] plain) == hit && plain.SequenceEqual(points), at + ": the plain Try is not the global one");
                    }
                }

                // The line as two rays from its origin, for every shape a ray is asked about.
                if (!(shape.Geo is GeoEllipse2) && !(shape.Geo is GeoXLine2) && shape.DistanceFromBoundary(line.Origin) > 0.02)
                {
                    GeoRay2 ahead = line.ToRay();
                    GeoRay2 behind = line.Reverse().ToRay();
                    double nearer = Math.Min(Distance(ahead, shape.Geo, Strict), Distance(behind, shape.Geo, Strict));

                    Assert.True(Math.Abs(nearer - distance) <= 1E-9 * (1.0 + distance) + shape.Imprecision, at + ": the rays are " + nearer + " away together");

                    if (!(shape.Geo is GeoPoint2) && Math.Abs(expected - Strict.EqualPoint) > 1E-6)
                    {
                        Assert.True((Collides(ahead, shape.Geo, Strict) || Collides(behind, shape.Geo, Strict)) == Collides(line, shape.Geo, Strict), at + ": the rays collide differently");
                    }

                    if (!delicate && !shape.Ambiguous && Answers(line, "GetIntersections", shape.Geo))
                    {
                        var together = new List<GeoPoint2>();

                        foreach (GeoPoint2 p in Crossings(ahead, shape.Geo, Strict).Concat(Crossings(behind, shape.Geo, Strict)))
                        {
                            if (!together.Any(q => q.DistanceTo(p) < 1E-6))
                            {
                                together.Add(p);
                            }
                        }

                        AssertSameSet(together.ToArray(), Crossings(line, shape.Geo, Strict), crossTolerance, at + " crossings of the two rays");
                    }
                }
            }

            // A fuzz that never touches, or never misses, tests half of what it says it does. A line is apart from another
            // line only where the two are parallel, which the next test is for.
            if (kind != "XLine2")
            {
                Assert.True(apart >= Draws / 20, kind + ": only " + apart + " lines were apart from the shape");
            }

            if (kind != "Point2")
            {
                Assert.True(touching >= Draws / 20, kind + ": only " + touching + " lines touched the shape");
            }
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.0005)]
        [InlineData(0.002)]
        [InlineData(0.5)]
        [InlineData(30.0)]
        [InlineData(-0.0005)]
        [InlineData(-30.0)]
        public void ShapesRunningAlongTheLineAtAGap_AreThatFarFromIt_WhichWayTheyRunAndOnWhicheverSide(double gap)
        {
            // The line through (10, 20) at 30 degrees; a segment, a ray, a line and a point at the signed gap to its left, running
            // along it or against it, the segment and the ray starting behind the origin and ahead of it.
            var line = new GeoXLine2(new GeoPoint2(10, 20), new GeoVector2(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)));
            GeoVector2 along = line.Direction;
            GeoVector2 left = new GeoVector2(-along.Y, along.X);

            GeoPoint2 At(double t) => new GeoPoint2(10 + t * along.X + gap * left.X, 20 + t * along.Y + gap * left.Y);

            var parallels = new List<object>
            {
                new GeoLine2(At(-80), At(-20)),
                new GeoLine2(At(70), At(10)),
                new GeoRay2(At(-30), along),
                new GeoRay2(At(-30), -along),
                new GeoRay2(At(40), along),
                new GeoRay2(At(40), -along),
                new GeoXLine2(At(25), along),
                new GeoXLine2(At(-25), -along),
                At(-500),
                At(500),
                new GeoEdge2(At(-80), At(-20)),
            };

            double expected = Math.Abs(gap);

            foreach (object shape in parallels)
            {
                string at = shape.GetType().Name + " at " + gap;

                Assert.True(Math.Abs(Distance(line, shape, Strict) - expected) <= 1E-9, at + ": distance " + Distance(line, shape, Strict));
                Assert.True(Distance(shape, line, Strict).Equals(Distance(line, shape, Strict)), at + ": distance from the shape");

                GeoLine2 leaving = Shortest(line, shape, Strict);

                Assert.True(Math.Abs(leaving.Length - expected) <= 1E-9, at + ": shortest line " + leaving.Length);
                Assert.True(OnTheLine(line, leaving.StartPoint), at + ": shortest line starts off the line");

                if (!(shape is GeoPoint2))
                {
                    Assert.True((expected <= Strict.EqualPoint) == Collides(line, shape, Strict), at + ": collides");
                    Assert.True((expected <= Strict.EqualPoint) == Collides(shape, line, Strict), at + ": collides from the shape");
                }

                // Running along the line, a shape is crossed nowhere: parallel is refused.
                if (HasTry(line, shape))
                {
                    Assert.False(Try(line, shape, out GeoPoint2[] points, Strict), at + ": a crossing along a parallel");
                    Assert.Empty(points);
                }
            }
        }

        [Theory]
        [InlineData(1E-6, 0.0)]
        [InlineData(1E-9, 0.0)]
        [InlineData(1E-11, 0.0)]
        [InlineData(1E-13, 5.0)]
        [InlineData(1E-14, 5.0)]
        [InlineData(1E-17, 5.0)]
        [InlineData(0.0, 5.0)]
        public void TwoLinesAtATinyAngle_CrossFarAwayAndAreNoughtApart_UnlessTheyAreParallelByRounding(double tilt, double expected)
        {
            // The line along X through the origin and the one through (0, 5) tilted by a sine of 'tilt': they cross 5/tilt along,
            // so they touch, as far as a double can place the crossing within a thousandth, which is 4.5E12 (a tilt of 1E-12 is on
            // the edge of it); further out, or below a sine of 1E-15, the crossing is rounding, and they are 5 apart.
            var flat = new GeoXLine2(new GeoPoint2(0, 0), GeoVector2.XAxis);
            var tilted = new GeoXLine2(new GeoPoint2(0, 5), new GeoVector2(1, tilt));

            Assert.True(Math.Abs(Distance(flat, tilted, Strict) - expected) <= 1E-9, "distance " + Distance(flat, tilted, Strict));
            Assert.True(Distance(tilted, flat, Strict).Equals(Distance(flat, tilted, Strict)));
            Assert.Equal(expected <= Strict.EqualPoint, Collides(flat, tilted, Strict));
            Assert.Equal(expected <= Strict.EqualPoint, Collides(tilted, flat, Strict));
            Assert.Equal(expected, Shortest(flat, tilted, Strict).Length, 9);
            Assert.False(Try(flat, tilted, out _, Strict), "lines this close to parallel are refused a crossing, as two rays are");
        }
    }
}
