using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The distance between points and straight shapes is nought only where they truly meet, and otherwise the exact
    /// gap, whichever way round it is asked and whichever end of a segment comes near. It used to read nought from the
    /// crossing test, which asks whether a segment starts within the point tolerance of a region: a segment starting
    /// 0.0005 off the square 100 by 100 was nought away at a tolerance of 0.001, and the same segment reversed 0.0005.
    /// A rectangle kept its band per axis, so a segment 0.00085 off its corner both ways, 0.0012 away, was nought too,
    /// and collided; a point 0.0005 off a polygon, a triangle, a loop or a face was nought.
    /// </summary>
    public class Distance2ExactTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>The gap is exact to rounding: 4E-14 of the largest coordinate here, 200, with room.</summary>
        private const double Allowance = 1E-11;

        private static GeoPolygon2 Box(double left, double bottom, double right, double top) => new GeoPolygon2(
            new GeoPoint2(left, bottom), new GeoPoint2(right, bottom), new GeoPoint2(right, top), new GeoPoint2(left, top));

        /// <summary>The square 100 by 100 with a corner at the origin.</summary>
        private static GeoPolygon2 Square() => Box(0, 0, 100, 100);

        /// <summary>The same square as a loop of straight edges.</summary>
        private static GeoPolygonArc2 StraightLoop() => new GeoPolygonArc2(Square().Vertices, new[] { 0.0, 0.0, 0.0, 0.0 });

        /// <summary>The square 100 by 100 with a hole 20 by 20 in its middle.</summary>
        private static GeoFace2 Plate() => new GeoFace2(Square(), new[] { Box(40, 40, 60, 60) });

        /// <summary>The triangle (0, 0), (100, 0), (0, 100).</summary>
        private static GeoTriangle2 Corner() => new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(0, 100));

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector);

        /// <summary>
        /// Asks a member that takes no tolerance at the test's own. It reads the tolerance in force on this thread, so it is
        /// asked inside a scope of that tolerance, which reaches no other thread; Tolerance.Global is never set.
        /// </summary>
        private static double Within(Tolerance tolerance, Func<double> distance)
        {
            using (Tolerance.Use(tolerance))
            {
                return distance();
            }
        }

        private static void AssertGap(double expected, double actual)
        {
            Assert.InRange(actual, expected - Allowance, expected + Allowance);
        }

        /// <summary>Both ways round the distance is the gap, and both ways round they collide exactly where it is within the tolerance.</summary>
        private static void AssertMeasured(double gap, Tolerance tolerance, (double There, double Back, bool Collides, bool CollidesBack) m)
        {
            AssertGap(gap, m.There);
            AssertGap(gap, m.Back);
            Assert.Equal(gap <= tolerance.EqualPoint, m.Collides);
            Assert.Equal(gap <= tolerance.EqualPoint, m.CollidesBack);
        }

        #region F1: a straight shape whose near end is the one it starts from

        public static IEnumerable<object[]> HalfAToleranceOffCases()
        {
            foreach (double equalPoint in new[] { 1E-3, 1E-4 })
            {
                foreach (string kind in new[]
                {
                    "segment starting off the side", "segment ending off the side", "segment along the side",
                    "ray starting off the side", "ray along the side", "polyline starting off the side",
                    "polyline ending off the side", "polygon beside it", "triangle with a corner off the side",
                    "polygon starting off the rectangle", "segment starting off the rectangle", "segment starting off the straight loop",
                    "segment starting off the triangle",
                })
                {
                    yield return new object[] { kind, equalPoint };
                }
            }
        }

        /// <summary>
        /// The distance both ways round and the collision both ways round, for a shape whose nearest point stands half the
        /// tolerance off the side x = 100 of the square (or off the side y = 0 of the triangle). Returns the gap too: the
        /// double nearest 100 + tolerance / 2, less 100, which is exact.
        /// </summary>
        private static (double Gap, (double, double, bool, bool) Measured) HalfAToleranceOff(string kind, Tolerance t)
        {
            double x = 100 + t.EqualPoint / 2;
            double gap = x - 100;
            GeoPolygon2 square = Square();

            switch (kind)
            {
                case "segment starting off the side":
                {
                    var line = new GeoLine2(new GeoPoint2(x, 50), new GeoPoint2(150, 50));
                    return (gap, (Within(t, () => square.DistanceTo(line)), Within(t, () => line.DistanceTo(square)), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                }

                case "segment ending off the side":
                {
                    var line = new GeoLine2(new GeoPoint2(150, 50), new GeoPoint2(x, 50));
                    return (gap, (Within(t, () => square.DistanceTo(line)), Within(t, () => line.DistanceTo(square)), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                }

                case "segment along the side":
                {
                    var line = new GeoLine2(new GeoPoint2(x, 20), new GeoPoint2(x, 80));
                    return (gap, (Within(t, () => square.DistanceTo(line)), Within(t, () => line.DistanceTo(square)), square.CollidesWith(line, t), line.CollidesWith(square, t)));
                }

                case "ray starting off the side":
                {
                    var ray = new GeoRay2(new GeoPoint2(x, 50), GeoVector2.XAxis);
                    return (gap, (square.DistanceTo(ray, t), ray.DistanceTo(square, t), square.CollidesWith(ray, t), ray.CollidesWith(square, t)));
                }

                case "ray along the side":
                {
                    var ray = new GeoRay2(new GeoPoint2(x, 20), GeoVector2.YAxis);
                    return (gap, (square.DistanceTo(ray, t), ray.DistanceTo(square, t), square.CollidesWith(ray, t), ray.CollidesWith(square, t)));
                }

                case "polyline starting off the side":
                {
                    var chain = new GeoPolyline2(new GeoPoint2(x, 50), new GeoPoint2(150, 50), new GeoPoint2(150, 80));
                    return (gap, (Within(t, () => square.DistanceTo(chain)), Within(t, () => chain.DistanceTo(square)), square.CollidesWith(chain, t), chain.CollidesWith(square, t)));
                }

                case "polyline ending off the side":
                {
                    var chain = new GeoPolyline2(new GeoPoint2(150, 80), new GeoPoint2(150, 50), new GeoPoint2(x, 50));
                    return (gap, (Within(t, () => square.DistanceTo(chain)), Within(t, () => chain.DistanceTo(square)), square.CollidesWith(chain, t), chain.CollidesWith(square, t)));
                }

                case "polygon beside it":
                {
                    GeoPolygon2 other = Box(x, 0, 200, 100);
                    return (gap, (Within(t, () => square.DistanceTo(other)), Within(t, () => other.DistanceTo(square)), square.CollidesWith(other, t), other.CollidesWith(square, t)));
                }

                case "triangle with a corner off the side":
                {
                    var tri = new GeoTriangle2(new GeoPoint2(x, 50), new GeoPoint2(150, 20), new GeoPoint2(150, 80));
                    return (gap, (Within(t, () => square.DistanceTo(tri)), Within(t, () => tri.DistanceTo(square)), square.CollidesWith(tri, t), tri.CollidesWith(square, t)));
                }

                case "polygon starting off the rectangle":
                {
                    var rect = new GeoRectangle2(0, 0, 100, 100);
                    var polygon = new GeoPolygon2(new GeoPoint2(x, 50), new GeoPoint2(150, 20), new GeoPoint2(150, 80));
                    return (gap, (Within(t, () => rect.DistanceTo(polygon)), Within(t, () => polygon.DistanceTo(rect)), rect.CollidesWith(polygon, t), polygon.CollidesWith(rect, t)));
                }

                case "segment starting off the rectangle":
                {
                    var rect = new GeoRectangle2(0, 0, 100, 100);
                    var line = new GeoLine2(new GeoPoint2(x, 50), new GeoPoint2(150, 50));
                    return (gap, (Within(t, () => rect.DistanceTo(line)), Within(t, () => line.DistanceTo(rect)), rect.CollidesWith(line, t), line.CollidesWith(rect, t)));
                }

                case "segment starting off the straight loop":
                {
                    GeoPolygonArc2 loop = StraightLoop();
                    var line = new GeoLine2(new GeoPoint2(x, 50), new GeoPoint2(150, 50));
                    return (gap, (loop.DistanceTo(line, t), line.DistanceTo(loop, t), loop.CollidesWith(line, t), line.CollidesWith(loop, t)));
                }

                default:
                {
                    // Off the triangle's side y = 0, running away from it.
                    GeoTriangle2 tri = Corner();
                    var line = new GeoLine2(new GeoPoint2(50, -gap), new GeoPoint2(50, -50));
                    return (gap, (Within(t, () => tri.DistanceTo(line)), Within(t, () => line.DistanceTo(tri)), tri.CollidesWith(line, t), line.CollidesWith(tri, t)));
                }
            }
        }

        [Theory]
        [MemberData(nameof(HalfAToleranceOffCases))]
        public void AStraightShapeHalfAToleranceOff_IsThatFarEitherWayRoundAndCollides(string kind, double equalPoint)
        {
            Tolerance t = At(equalPoint);
            (double gap, var measured) = HalfAToleranceOff(kind, t);

            AssertMeasured(gap, t, measured);
        }

        #endregion

        #region F2: a rectangle's corner

        /// <summary>
        /// The distance and the collision both ways round, for a shape whose tip stands 0.00085 off the corner (100, 100)
        /// of the rectangle 100 by 100 in x and in y: 0.0012021 away, past the tolerance 0.001. The tip is where a segment,
        /// a ray, a polyline, a polygon and a triangle start, except the segment that ends there.
        /// </summary>
        private static (double, double, bool, bool) OffTheRectangleCorner(string kind, out double gap)
        {
            var rect = new GeoRectangle2(0, 0, 100, 100);
            double d = 100.00085 - 100;
            gap = d * Math.Sqrt(2);
            var tip = new GeoPoint2(100 + d, 100 + d);
            var far = new GeoPoint2(150, 150);
            var left = new GeoPoint2(tip.X + 3, tip.Y + 9);
            var right = new GeoPoint2(tip.X + 9, tip.Y + 3);
            Tolerance t = Tol;

            switch (kind)
            {
                case "segment starting there":
                {
                    var line = new GeoLine2(tip, far);
                    return (Within(t, () => rect.DistanceTo(line)), Within(t, () => line.DistanceTo(rect)), rect.CollidesWith(line, t), line.CollidesWith(rect, t));
                }

                case "segment ending there":
                {
                    var line = new GeoLine2(far, tip);
                    return (Within(t, () => rect.DistanceTo(line)), Within(t, () => line.DistanceTo(rect)), rect.CollidesWith(line, t), line.CollidesWith(rect, t));
                }

                case "ray":
                {
                    var ray = new GeoRay2(tip, new GeoVector2(1, 2));
                    return (rect.DistanceTo(ray, t), ray.DistanceTo(rect, t), rect.CollidesWith(ray, t), ray.CollidesWith(rect, t));
                }

                case "polyline":
                {
                    var chain = new GeoPolyline2(left, tip, right);
                    return (Within(t, () => rect.DistanceTo(chain)), Within(t, () => chain.DistanceTo(rect)), rect.CollidesWith(chain, t), chain.CollidesWith(rect, t));
                }

                case "polygon":
                {
                    var polygon = new GeoPolygon2(tip, right, left);
                    return (Within(t, () => rect.DistanceTo(polygon)), Within(t, () => polygon.DistanceTo(rect)), rect.CollidesWith(polygon, t), polygon.CollidesWith(rect, t));
                }

                case "triangle":
                {
                    var tri = new GeoTriangle2(tip, right, left);
                    return (Within(t, () => rect.DistanceTo(tri)), Within(t, () => tri.DistanceTo(rect)), rect.CollidesWith(tri, t), tri.CollidesWith(rect, t));
                }

                default:
                {
                    // Corner to corner; its corner is tip + 25 - 25, within a rounding of the tip.
                    var other = new GeoRectangle2(tip.X, tip.Y, 50, 50);
                    return (Within(t, () => rect.DistanceTo(other)), Within(t, () => other.DistanceTo(rect)), rect.CollidesWith(other, t), other.CollidesWith(rect, t));
                }
            }
        }

        [Theory]
        [InlineData("segment starting there")]
        [InlineData("segment ending there")]
        [InlineData("ray")]
        [InlineData("polyline")]
        [InlineData("polygon")]
        [InlineData("triangle")]
        [InlineData("rectangle")]
        public void AShapeOneAndAFifthTolerancesOffARectangleCorner_IsThatFarAndDoesNotCollide(string kind)
        {
            var measured = OffTheRectangleCorner(kind, out double gap);

            AssertMeasured(gap, Tol, measured);
        }

        #endregion

        #region F5: a point

        public static IEnumerable<object[]> PointCases()
        {
            foreach (double equalPoint in new[] { 1E-3, 1E-4 })
            {
                foreach (string kind in new[] { "polygon", "triangle", "straight loop", "face outline", "face hole" })
                {
                    yield return new object[] { kind, equalPoint };
                }
            }
        }

        /// <summary>
        /// The distance both ways round from a shape to a point half the tolerance off it: off the side x = 100, off the
        /// triangle's side y = 0, or inside the plate's hole, off its rim x = 40. No member says whether a point collides.
        /// </summary>
        private static (double There, double Back) PointHalfAToleranceOff(string kind, Tolerance t, out double gap)
        {
            double x = 100 + t.EqualPoint / 2;
            gap = x - 100;
            var off = new GeoPoint2(x, 50);

            switch (kind)
            {
                case "polygon":
                {
                    GeoPolygon2 square = Square();
                    return (Within(t, () => square.DistanceTo(off)), Within(t, () => off.DistanceTo(square)));
                }

                case "triangle":
                {
                    GeoTriangle2 tri = Corner();
                    var below = new GeoPoint2(50, -gap);
                    return (Within(t, () => tri.DistanceTo(below)), Within(t, () => below.DistanceTo(tri)));
                }

                case "straight loop":
                    return (StraightLoop().DistanceTo(off, t), off.DistanceTo(StraightLoop(), t));

                case "face outline":
                    return (Plate().DistanceTo(off, t), off.DistanceTo(Plate(), t));

                default:
                {
                    double inHole = 40 + t.EqualPoint / 2;
                    gap = inHole - 40;
                    var p = new GeoPoint2(inHole, 50);
                    return (Plate().DistanceTo(p, t), p.DistanceTo(Plate(), t));
                }
            }
        }

        [Theory]
        [MemberData(nameof(PointCases))]
        public void APointHalfAToleranceOff_IsThatFarEitherWayRound(string kind, double equalPoint)
        {
            (double there, double back) = PointHalfAToleranceOff(kind, At(equalPoint), out double gap);

            AssertGap(gap, there);
            AssertGap(gap, back);
        }

        #endregion

        #region F8: a rectangle and a face

        [Fact]
        public void AFaceOneAndAFifthTolerancesOffARectangleCorner_DoesNotCollide()
        {
            // The face's corner 0.00085 off the corner (100, 100) in x and in y: 0.0012021 away, past the tolerance. Its
            // distance is already exact; the collision kept the rectangle's band per axis.
            var rect = new GeoRectangle2(0, 0, 100, 100);
            double d = 100.00085 - 100;
            var face = new GeoFace2(Box(100 + d, 100 + d, 200, 200));
            double gap = d * Math.Sqrt(2);

            AssertMeasured(gap, Tol, (rect.DistanceTo(face, Tol), face.DistanceTo(rect, Tol), rect.CollidesWith(face, Tol), face.CollidesWith(rect, Tol)));
        }

        #endregion

        #region Controls: what already reads right and must stay so

        public static IEnumerable<object[]> ControlCases()
        {
            yield return new object[] { "segment starting on the side", 0.0 };
            yield return new object[] { "segment crossing the side", 0.0 };
            yield return new object[] { "segment starting five tolerances off", 100.005 - 100 };
            yield return new object[] { "segment inside the square", 0.0 };
            yield return new object[] { "rectangle five tolerances beside it", 100.005 - 100 };
            yield return new object[] { "rectangle half a tolerance beside it", 100.0005 - 100 };
            yield return new object[] { "segment in the plate's hole", 5.0 };
            yield return new object[] { "face in the plate's hole", 5.0 };
            yield return new object[] { "rectangle holding the plate", 0.0 };
            yield return new object[] { "face beside the rectangle", 100.0005 - 100 };
        }

        private static (double, double, bool, bool) Control(string kind)
        {
            GeoPolygon2 square = Square();
            GeoFace2 plate = Plate();
            Tolerance t = Tol;

            switch (kind)
            {
                case "segment starting on the side": return AgainstSquare(new GeoLine2(new GeoPoint2(100, 50), new GeoPoint2(150, 50)));
                case "segment crossing the side": return AgainstSquare(new GeoLine2(new GeoPoint2(50, 50), new GeoPoint2(150, 50)));
                case "segment starting five tolerances off": return AgainstSquare(new GeoLine2(new GeoPoint2(100.005, 50), new GeoPoint2(150, 50)));
                case "segment inside the square": return AgainstSquare(new GeoLine2(new GeoPoint2(20, 20), new GeoPoint2(30, 40)));

                case "rectangle five tolerances beside it":
                case "rectangle half a tolerance beside it":
                {
                    // Its left side is x + 25 - 25, within a rounding of x.
                    var rect = new GeoRectangle2(kind.StartsWith("rectangle five", StringComparison.Ordinal) ? 100.005 : 100.0005, 20, 50, 60);
                    return (Within(t, () => square.DistanceTo(rect)), Within(t, () => rect.DistanceTo(square)), square.CollidesWith(rect, t), rect.CollidesWith(square, t));
                }

                case "segment in the plate's hole":
                {
                    // From (45, 45) to (55, 55): its ends are 5 from the rim's sides.
                    var line = new GeoLine2(new GeoPoint2(45, 45), new GeoPoint2(55, 55));
                    return (plate.DistanceTo(line, t), line.DistanceTo(plate, t), plate.CollidesWith(line, t), line.CollidesWith(plate, t));
                }

                case "face in the plate's hole":
                {
                    var insert = new GeoFace2(Box(45, 45, 55, 55));
                    return (plate.DistanceTo(insert, t), insert.DistanceTo(plate, t), plate.CollidesWith(insert, t), insert.CollidesWith(plate, t));
                }

                case "rectangle holding the plate":
                {
                    var rect = new GeoRectangle2(-10, -10, 120, 120);
                    return (plate.DistanceTo(rect, t), rect.DistanceTo(plate, t), plate.CollidesWith(rect, t), rect.CollidesWith(plate, t));
                }

                default:
                {
                    // Side to side, 0.0005 apart: within the tolerance, so it collides, and the distance is the gap.
                    var rect = new GeoRectangle2(0, 0, 100, 100);
                    var face = new GeoFace2(Box(100.0005, 0, 200, 100));
                    return (rect.DistanceTo(face, t), face.DistanceTo(rect, t), rect.CollidesWith(face, t), face.CollidesWith(rect, t));
                }
            }

            (double, double, bool, bool) AgainstSquare(GeoLine2 line) =>
                (Within(t, () => square.DistanceTo(line)), Within(t, () => line.DistanceTo(square)), square.CollidesWith(line, t), line.CollidesWith(square, t));
        }

        [Theory]
        [MemberData(nameof(ControlCases))]
        public void ATouchACrossingAHoldAWideGapOrAHole_ReadsAsItDidAndCollidesWithinTheTolerance(string kind, double gap)
        {
            AssertMeasured(gap, Tol, Control(kind));
        }

        [Theory]
        [InlineData(100.0, 50.0, 0.0)]
        [InlineData(20.0, 50.0, 0.0)]
        [InlineData(50.0, 50.0, 10.0)]
        [InlineData(100.005, 50.0, 0.005)]
        public void APointOnTheRimOnTheMaterialInTheHoleOrFiveTolerancesOut_IsAsFarAsItIs(double x, double y, double gap)
        {
            // On the plate's outline, on its material, in the middle of its hole (10 from the rim), and five tolerances out
            // (100.005 less 100 is 0.005 to within 3E-15).
            var p = new GeoPoint2(x, y);

            AssertGap(gap, Plate().DistanceTo(p, Tol));
            AssertGap(gap, p.DistanceTo(Plate(), Tol));
        }

        #endregion

        #region The convention kept: a segment shorter than the tolerance is read as its start

        [Fact]
        public void ASegmentShorterThanTheTolerance_IsStillMeasuredFromItsStart()
        {
            // 0.0005 long, upright at x = -0.0003 across the end (0, 0) of a segment along X: its nearest point is 0.0003
            // away, but a segment shorter than EqualPoint is read as its start (-0.0003, 0.00025), 0.00039051 away. Item 4b
            // keeps this; the test is here so that it does not move unnoticed.
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(60, 0));
            var stub = new GeoLine2(new GeoPoint2(-0.0003, 0.00025), new GeoPoint2(-0.0003, -0.00025));
            double fromStart = Math.Sqrt(0.0003 * 0.0003 + 0.00025 * 0.00025);

            AssertGap(fromStart, line.DistanceTo(stub, Tol));
            AssertGap(fromStart, stub.DistanceTo(line, Tol));
        }

        #endregion
    }
}
