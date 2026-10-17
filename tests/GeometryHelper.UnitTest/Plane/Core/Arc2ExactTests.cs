using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The distance between arcs, circles, curved chains and curved loops, and between them and straight shapes, is
    /// nought only where they truly meet, and otherwise the exact gap, whichever way round it is asked. It used to read
    /// nought from the crossing tests, which keep a band of the point tolerance: a segment 0.0005 above the top of an arc
    /// of radius 50 was nought away at a tolerance of 0.001, two arcs whose ends stood 0.0015 apart were too, and two arcs
    /// of one radius crossing just inside their ends were 2.5E-9 apart.
    /// </summary>
    public class Arc2ExactTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>The gap is exact to rounding: 4E-14 of the largest coordinate or radius here, 200, with room.</summary>
        private const double Allowance = 1E-11;

        /// <summary>The same for the edge with a bulge of 1E-4, whose radius is 150 000: 4E-14 of that.</summary>
        private const double FlatAllowance = 6E-9;

        private static GeoPolygon2 Box(double left, double bottom, double right, double top) => new GeoPolygon2(
            new GeoPoint2(left, bottom), new GeoPoint2(right, bottom), new GeoPoint2(right, top), new GeoPoint2(left, top));

        /// <summary>The square 100 by 100 with a corner at the origin.</summary>
        private static GeoPolygon2 Square() => Box(0, 0, 100, 100);

        /// <summary>The upper half of the circle of radius 50 about the origin: its top is (0, 50), its ends (50, 0) and (-50, 0).</summary>
        private static GeoArc2 Dome() => new GeoArc2(new GeoPoint2(0, 0), 50, 0, Math.PI);

        /// <summary>The square with its right side bowed out by a bulge of 0.5: the side's middle stands at (125, 50).</summary>
        private static GeoPolygonArc2 BowedLoop() => new GeoPolygonArc2(
            new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) }, new[] { 0, 0.5, 0, 0 });

        /// <summary>That bowed side alone, as a chain: from (100, 0) to (100, 100) through (125, 50).</summary>
        private static GeoPolylineArc2 BowedChain() => new GeoPolylineArc2(new[] { new GeoPoint2(100, 0), new GeoPoint2(100, 100) }, new[] { 0.5, 0 });

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

        private static void AssertGap(double expected, double actual, double allowance = Allowance)
        {
            Assert.InRange(actual, expected - allowance, expected + allowance);
        }

        /// <summary>Both ways round the distance is the gap, and both ways round they collide exactly where it is within the tolerance.</summary>
        private static void AssertMeasured(Tolerance tolerance, (double Gap, double There, double Back, bool Collides, bool CollidesBack) m, double allowance = Allowance)
        {
            AssertGap(m.Gap, m.There, allowance);
            AssertGap(m.Gap, m.Back, allowance);
            Assert.Equal(m.Gap <= tolerance.EqualPoint, m.Collides);
            Assert.Equal(m.Gap <= tolerance.EqualPoint, m.CollidesBack);
        }

        private static IEnumerable<object[]> AtBothTolerances(params string[] kinds)
        {
            foreach (double equalPoint in new[] { 1E-3, 1E-4 })
            {
                foreach (string kind in kinds)
                {
                    yield return new object[] { kind, equalPoint };
                }
            }
        }

        #region F3: an arc and a segment, two arcs

        public static IEnumerable<object[]> SegmentOffTheDomeCases() => AtBothTolerances(
            "segment above the top", "segment starting off the top", "segment starting off the end", "segment just inside the top");

        [Theory]
        [MemberData(nameof(SegmentOffTheDomeCases))]
        public void ASegmentHalfAToleranceOffAnArc_IsThatFarEitherWayRoundAndCollides(string kind, double equalPoint)
        {
            Tolerance t = At(equalPoint);
            double h = t.EqualPoint / 2;
            double top = 50 + h;
            GeoArc2 dome = Dome();
            GeoLine2 line;
            double gap;

            switch (kind)
            {
                case "segment above the top":
                    line = new GeoLine2(new GeoPoint2(-10, top), new GeoPoint2(10, top));
                    gap = top - 50;
                    break;

                case "segment starting off the top":
                    line = new GeoLine2(new GeoPoint2(0, top), new GeoPoint2(0, 100));
                    gap = top - 50;
                    break;

                case "segment starting off the end":
                    // Below the end (50, 0) and running down, away from the half circle above.
                    line = new GeoLine2(new GeoPoint2(50, -h), new GeoPoint2(50, -50));
                    gap = h;
                    break;

                default:
                    // h under the top, reaching sqrt(50 h) either way: its ends, its farthest points from the centre, are at a
                    // radius of sqrt(2500 - 50 h + h^2), about 50 - h / 2, so it lies inside the circle, half that again from
                    // the arc. Any wider and it would cross the circle (at h = 5E-5 a stub 0.2 long does).
                    double reach = Math.Sqrt(50 * h);
                    line = new GeoLine2(new GeoPoint2(-reach, 50 - h), new GeoPoint2(reach, 50 - h));
                    gap = 50 - Math.Sqrt(reach * reach + (50 - h) * (50 - h));
                    break;
            }

            AssertMeasured(t, (gap, dome.DistanceTo(line, t), line.DistanceTo(dome, t), dome.CollidesWith(line, t), line.CollidesWith(dome, t)));
        }

        [Theory]
        [InlineData("tops facing", 0.5)]
        [InlineData("starts facing", 0.5)]
        [InlineData("starts facing", 1.5)]
        [InlineData("starts facing", 1.9)]
        public void TwoArcsAGapApart_AreThatFarAndCollideOnlyWithinTheTolerance(string kind, double tolerances)
        {
            double g = tolerances * Tol.EqualPoint;
            GeoArc2 a;
            GeoArc2 b;
            double gap;

            if (kind == "tops facing")
            {
                // The dome, and the lower half of the circle of radius 50 about (0, 100 + g): its bottom is g above the top.
                double c = 100 + g;
                a = Dome();
                b = new GeoArc2(new GeoPoint2(0, c), 50, Math.PI, 2 * Math.PI);
                gap = c - 100;
            }
            else
            {
                // The arc of radius 50 from 0.35 to 2.6 rad, and the same turned a half turn about its start and moved g back
                // along the tangent there: the two leave their starts, g apart, in opposite directions.
                a = new GeoArc2(new GeoPoint2(0, 0), 50, 0.35, 2.6);
                var start = new GeoPoint2(50 * Math.Cos(0.35), 50 * Math.Sin(0.35));
                var back = new GeoVector2(Math.Sin(0.35), -Math.Cos(0.35));
                b = new GeoArc2(new GeoPoint2(2 * start.X + g * back.X, 2 * start.Y + g * back.Y), 50, 0.35 + Math.PI, 2.6 + Math.PI);
                gap = g;
            }

            AssertMeasured(Tol, (gap, a.DistanceTo(b, Tol), b.DistanceTo(a, Tol), a.CollidesWith(b, Tol), b.CollidesWith(a, Tol)));
        }

        #endregion

        #region F6: a circle and a polyline, a chain or a loop

        public static IEnumerable<object[]> CircleCases() => AtBothTolerances("polyline", "chain", "loop");

        [Theory]
        [MemberData(nameof(CircleCases))]
        public void AShapeStartingHalfAToleranceOffACircle_IsThatFarEitherWayRoundAndCollides(string kind, double equalPoint)
        {
            // The circle of radius 40 about the origin; the shape starts at (40 + h, 0) and runs out, away from it.
            Tolerance t = At(equalPoint);
            double x = 40 + t.EqualPoint / 2;
            double gap = x - 40;
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 40);
            var start = new GeoPoint2(x, 0);

            switch (kind)
            {
                case "polyline":
                {
                    var chain = new GeoPolyline2(start, new GeoPoint2(80, 0), new GeoPoint2(80, 30));
                    AssertMeasured(t, (gap, Within(t, () => circle.DistanceTo(chain)), Within(t, () => chain.DistanceTo(circle)), circle.CollidesWith(chain, t), chain.CollidesWith(circle, t)));
                    break;
                }

                case "chain":
                {
                    var chain = new GeoPolylineArc2(new[] { start, new GeoPoint2(80, 0), new GeoPoint2(80, 30) }, new[] { 0, 0.3, 0 });
                    AssertMeasured(t, (gap, circle.DistanceTo(chain, t), chain.DistanceTo(circle, t), circle.CollidesWith(chain, t), chain.CollidesWith(circle, t)));
                    break;
                }

                default:
                {
                    var loop = new GeoPolygonArc2(new[] { start, new GeoPoint2(80, -20), new GeoPoint2(80, 20) }, new[] { 0, 0.3, 0 });
                    AssertMeasured(t, (gap, circle.DistanceTo(loop, t), loop.DistanceTo(circle, t), circle.CollidesWith(loop, t), loop.CollidesWith(circle, t)));
                    break;
                }
            }
        }

        #endregion

        #region F7: two arcs a hair apart that cross

        [Fact]
        public void TwoArcsOfOneRadiusCrossingJustInsideTheirEnds_AreNoughtApart()
        {
            // Radius 50, centres 0.0005 apart: the circles cross at 0.350005 rad on the first arc, which starts at 0.35, and
            // at 0.349995 rad on the second, which ends at 0.35 (6.633 rad): 5E-6 rad, 0.00025, inside both. They meet.
            var a = new GeoArc2(new GeoPoint2(0, 0), 50, 0.35, 2.6);
            var b = new GeoArc2(new GeoPoint2(-0.00017144890372772567, 0.0004696863564236895), 50, 4.383185307179587, 6.633185307179587);

            AssertMeasured(Tol, (0.0, a.DistanceTo(b, Tol), b.DistanceTo(a, Tol), a.CollidesWith(b, Tol), b.CollidesWith(a, Tol)));
        }

        #endregion

        #region Curved loops and chains

        /// <summary>The point half the tolerance beyond the middle (125, 50) of the bowed side.</summary>
        private static double BeyondTheBow(Tolerance t) => 125 + t.EqualPoint / 2;

        [Theory]
        [InlineData("loop", "segment")]
        [InlineData("loop", "ray")]
        [InlineData("loop", "polyline")]
        [InlineData("loop", "polygon")]
        [InlineData("loop", "triangle")]
        [InlineData("loop", "rectangle")]
        [InlineData("chain", "segment")]
        [InlineData("chain", "ray")]
        [InlineData("chain", "polyline")]
        [InlineData("chain", "polygon")]
        [InlineData("chain", "triangle")]
        [InlineData("chain", "rectangle")]
        public void AStraightShapeStartingHalfAToleranceOffABow_IsThatFarEitherWayRoundAndCollides(string curve, string kind)
        {
            Tolerance t = Tol;
            double x = BeyondTheBow(t);
            double gap = x - 125;
            var start = new GeoPoint2(x, 50);
            var upper = new GeoPoint2(175, 80);
            var lower = new GeoPoint2(175, 20);
            GeoPolygonArc2 loop = BowedLoop();
            GeoPolylineArc2 chain = BowedChain();
            bool isLoop = curve == "loop";

            switch (kind)
            {
                case "segment":
                {
                    var line = new GeoLine2(start, new GeoPoint2(175, 50));
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(line, t), line.DistanceTo(loop, t), loop.CollidesWith(line, t), line.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(line, t), line.DistanceTo(chain, t), chain.CollidesWith(line, t), line.CollidesWith(chain, t)));
                    break;
                }

                case "ray":
                {
                    var ray = new GeoRay2(start, GeoVector2.XAxis);
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(ray, t), ray.DistanceTo(loop, t), loop.CollidesWith(ray, t), ray.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(ray, t), ray.DistanceTo(chain, t), chain.CollidesWith(ray, t), ray.CollidesWith(chain, t)));
                    break;
                }

                case "polyline":
                {
                    var other = new GeoPolyline2(start, new GeoPoint2(175, 50), upper);
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }

                case "polygon":
                {
                    var other = new GeoPolygon2(start, lower, upper);
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }

                case "triangle":
                {
                    var other = new GeoTriangle2(start, lower, upper);
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }

                default:
                {
                    // Its left side is x + 25 - 25, within a rounding of x.
                    var other = new GeoRectangle2(x, 40, 50, 20);
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }
            }
        }

        [Theory]
        [InlineData("loop", "square")]
        [InlineData("loop", "rectangle")]
        [InlineData("loop", "triangle")]
        [InlineData("loop", "segment")]
        [InlineData("loop", "polyline")]
        [InlineData("loop", "ray")]
        [InlineData("chain", "square")]
        [InlineData("chain", "rectangle")]
        [InlineData("chain", "triangle")]
        [InlineData("chain", "segment")]
        [InlineData("chain", "polyline")]
        [InlineData("chain", "ray")]
        public void ACurvedShapeStartingHalfAToleranceOffAStraightOne_IsThatFarEitherWayRoundAndCollides(string curve, string target)
        {
            // The curved shape starts at (100 + h, 50) and runs away from the side x = 100 that each target has, on an arc
            // bowing away too: through (150, 20) and (150, 80), its first edge to (150, 20) bulging -0.4, inward for the
            // counter-clockwise loop (whose side between (150, 20) and (150, 80) bulges out by 0.4).
            Tolerance t = Tol;
            double x = 100 + t.EqualPoint / 2;
            double gap = x - 100;
            var start = new GeoPoint2(x, 50);
            var lower = new GeoPoint2(150, 20);
            var upper = new GeoPoint2(150, 80);
            var loop = new GeoPolygonArc2(new[] { start, lower, upper }, new[] { -0.4, 0.4, 0 });
            var chain = new GeoPolylineArc2(new[] { start, lower, upper }, new[] { -0.4, 0, 0 });
            bool isLoop = curve == "loop";

            switch (target)
            {
                case "square":
                {
                    GeoPolygon2 other = Square();
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }

                case "rectangle":
                {
                    var other = new GeoRectangle2(0, 0, 100, 100);
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }

                case "triangle":
                {
                    var other = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100));
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }

                case "segment":
                {
                    var other = new GeoLine2(new GeoPoint2(100, 0), new GeoPoint2(100, 100));
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }

                case "polyline":
                {
                    var other = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100));
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }

                default:
                {
                    // Down the line x = 100 from (100, 100), for ever.
                    var other = new GeoRay2(new GeoPoint2(100, 100), new GeoVector2(0, -1));
                    AssertMeasured(t, isLoop
                        ? (gap, loop.DistanceTo(other, t), other.DistanceTo(loop, t), loop.CollidesWith(other, t), other.CollidesWith(loop, t))
                        : (gap, chain.DistanceTo(other, t), other.DistanceTo(chain, t), chain.CollidesWith(other, t), other.CollidesWith(chain, t)));
                    break;
                }
            }
        }

        [Theory]
        [InlineData("loop off the bowed loop")]
        [InlineData("chain off the bowed loop")]
        [InlineData("chain off the bowed chain")]
        [InlineData("arc off the bowed loop")]
        public void ACurvedShapeStartingHalfAToleranceOffABow_IsThatFarEitherWayRoundAndCollides(string kind)
        {
            // Each starts at (125 + h, 50), beyond the middle of the bowed side, and runs away from it.
            Tolerance t = Tol;
            double x = BeyondTheBow(t);
            double gap = x - 125;
            var start = new GeoPoint2(x, 50);
            var lower = new GeoPoint2(175, 20);
            var upper = new GeoPoint2(175, 80);
            GeoPolygonArc2 bowed = BowedLoop();

            switch (kind)
            {
                case "loop off the bowed loop":
                {
                    var loop = new GeoPolygonArc2(new[] { start, lower, upper }, new[] { 0, 0.4, 0 });
                    AssertMeasured(t, (gap, bowed.DistanceTo(loop, t), loop.DistanceTo(bowed, t), bowed.CollidesWith(loop, t), loop.CollidesWith(bowed, t)));
                    break;
                }

                case "chain off the bowed loop":
                {
                    var chain = new GeoPolylineArc2(new[] { start, lower, upper }, new[] { -0.4, 0, 0 });
                    AssertMeasured(t, (gap, bowed.DistanceTo(chain, t), chain.DistanceTo(bowed, t), bowed.CollidesWith(chain, t), chain.CollidesWith(bowed, t)));
                    break;
                }

                case "chain off the bowed chain":
                {
                    GeoPolylineArc2 side = BowedChain();
                    var chain = new GeoPolylineArc2(new[] { start, lower, upper }, new[] { -0.4, 0, 0 });
                    AssertMeasured(t, (gap, side.DistanceTo(chain, t), chain.DistanceTo(side, t), side.CollidesWith(chain, t), chain.CollidesWith(side, t)));
                    break;
                }

                default:
                {
                    // Radius 50 about (x, 100), from straight below its centre round to its right: it leaves the start along X
                    // and curves up, away from the bow.
                    var arc = new GeoArc2(new GeoPoint2(x, 100), 50, -Math.PI / 2, 0);
                    AssertMeasured(t, (gap, bowed.DistanceTo(arc, t), arc.DistanceTo(bowed, t), bowed.CollidesWith(arc, t), arc.CollidesWith(bowed, t)));
                    break;
                }
            }
        }

        #endregion

        #region The S1 review's bands: a small circle by a rectangle or a polygon, an arc by the square

        [Fact]
        public void ACircleOfRadiusATenthOfTheToleranceOffARectangleCorner_IsThatFarAndDoesNotCollide()
        {
            // Its centre 0.00085 off the corner (100, 100) in x and in y: 0.0012021 from the corner, 0.0011021 from its rim.
            var rect = new GeoRectangle2(0, 0, 100, 100);
            double d = 100.00085 - 100;
            var circle = new GeoCircle2(new GeoPoint2(100 + d, 100 + d), 1E-4);
            double gap = d * Math.Sqrt(2) - 1E-4;

            AssertMeasured(Tol, (gap, Within(Tol, () => circle.DistanceTo(rect)), Within(Tol, () => rect.DistanceTo(circle)), circle.CollidesWith(rect, Tol), rect.CollidesWith(circle, Tol)));
        }

        [Theory]
        [InlineData("rectangle")]
        [InlineData("polygon")]
        public void ACircleOfRadiusATenthOfTheToleranceHalfAToleranceOffASide_IsItsRimsGapAway(string kind)
        {
            // Its centre 0.0005 off the side x = 100, its rim 0.0004 off.
            double x = 100.0005;
            var circle = new GeoCircle2(new GeoPoint2(x, 50), 1E-4);
            double gap = (x - 100) - 1E-4;

            if (kind == "rectangle")
            {
                var rect = new GeoRectangle2(0, 0, 100, 100);
                AssertMeasured(Tol, (gap, Within(Tol, () => circle.DistanceTo(rect)), Within(Tol, () => rect.DistanceTo(circle)), circle.CollidesWith(rect, Tol), rect.CollidesWith(circle, Tol)));
            }
            else
            {
                GeoPolygon2 square = Square();
                AssertMeasured(Tol, (gap, Within(Tol, () => circle.DistanceTo(square)), Within(Tol, () => square.DistanceTo(circle)), circle.CollidesWith(square, Tol), square.CollidesWith(circle, Tol)));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AnArcStartingOrEndingHalfAToleranceOffTheSquare_IsThatFarEitherWayRound(bool reversed)
        {
            // Radius 50 about (100.0005, 100), from straight below its centre round to its right, bowing away from the square;
            // or the same arc run the other way, ending there.
            double x = 100.0005;
            var arc = reversed
                ? new GeoArc2(new GeoPoint2(x, 100), 50, 0, -Math.PI / 2, true)
                : new GeoArc2(new GeoPoint2(x, 100), 50, -Math.PI / 2, 0);
            GeoPolygon2 square = Square();

            AssertMeasured(Tol, (x - 100, square.DistanceTo(arc, Tol), arc.DistanceTo(square, Tol), square.CollidesWith(arc, Tol), arc.CollidesWith(square, Tol)));
        }

        #endregion

        #region Edges that are arcs, the flat one too

        [Theory]
        [InlineData(0.4, "segment")]
        [InlineData(0.4, "polygon")]
        [InlineData(1E-4, "segment")]
        [InlineData(1E-4, "polygon")]
        public void AShapeStartingHalfAToleranceBelowACurvedEdge_IsThatFarEitherWayRound(double bulge, string kind)
        {
            // The edge from (0, 0) to (60, 0) bulging counter-clockwise bows below its chord, to (30, -30 bulge): 12 down
            // for a bulge of 0.4, 0.003 for 1E-4, whose radius is 150 000. The shape starts half a tolerance below that.
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(60, 0), bulge);
            double bottom = -30 * bulge;
            double y = bottom - Tol.EqualPoint / 2;
            double gap = bottom - y;
            var start = new GeoPoint2(30, y);
            double allowance = bulge < 0.01 ? FlatAllowance : Allowance;

            if (kind == "segment")
            {
                var line = new GeoLine2(start, new GeoPoint2(30, y - 50));
                AssertMeasured(Tol, (gap, edge.DistanceTo(line, Tol), line.DistanceTo(edge, Tol), edge.CollidesWith(line, Tol), line.CollidesWith(edge, Tol)), allowance);
            }
            else
            {
                var polygon = new GeoPolygon2(start, new GeoPoint2(0, y - 50), new GeoPoint2(60, y - 50));
                AssertMeasured(Tol, (gap, Within(Tol, () => edge.DistanceTo(polygon)), Within(Tol, () => polygon.DistanceTo(edge)), edge.CollidesWith(polygon, Tol), polygon.CollidesWith(edge, Tol)), allowance);
            }
        }

        #endregion

        #region Controls: what already reads right and must stay so

        [Theory]
        [InlineData("segment tangent to the top", 0.0)]
        [InlineData("segment crossing the arc", 0.0)]
        [InlineData("segment five tolerances above the top", 0.005)]
        [InlineData("arcs touching top to top", 0.0)]
        [InlineData("circle holding a polyline", 0.0)]
        [InlineData("circle holding a curved loop", 0.0)]
        [InlineData("ray five tolerances beyond the bow", 0.005)]
        public void ATangencyACrossingAWideGapOrAHold_ReadsAsItDidAndCollidesWithinTheTolerance(string kind, double expected)
        {
            // A wide gap is 50.005 less 50 or 125.005 less 125, 0.005 to within 1E-14.
            Tolerance t = Tol;
            GeoArc2 dome = Dome();
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 40);

            switch (kind)
            {
                case "segment tangent to the top":
                {
                    var line = new GeoLine2(new GeoPoint2(-10, 50), new GeoPoint2(10, 50));
                    AssertMeasured(t, (expected, dome.DistanceTo(line, t), line.DistanceTo(dome, t), dome.CollidesWith(line, t), line.CollidesWith(dome, t)));
                    break;
                }

                case "segment crossing the arc":
                {
                    var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0, 100));
                    AssertMeasured(t, (expected, dome.DistanceTo(line, t), line.DistanceTo(dome, t), dome.CollidesWith(line, t), line.CollidesWith(dome, t)));
                    break;
                }

                case "segment five tolerances above the top":
                {
                    var line = new GeoLine2(new GeoPoint2(-10, 50.005), new GeoPoint2(10, 50.005));
                    AssertMeasured(t, (expected, dome.DistanceTo(line, t), line.DistanceTo(dome, t), dome.CollidesWith(line, t), line.CollidesWith(dome, t)));
                    break;
                }

                case "arcs touching top to top":
                {
                    var below = new GeoArc2(new GeoPoint2(0, 100), 50, Math.PI, 2 * Math.PI);
                    AssertMeasured(t, (expected, dome.DistanceTo(below, t), below.DistanceTo(dome, t), dome.CollidesWith(below, t), below.CollidesWith(dome, t)));
                    break;
                }

                case "circle holding a polyline":
                {
                    var inside = new GeoPolyline2(new GeoPoint2(-10, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 5));
                    AssertMeasured(t, (expected, Within(t, () => circle.DistanceTo(inside)), Within(t, () => inside.DistanceTo(circle)), circle.CollidesWith(inside, t), inside.CollidesWith(circle, t)));
                    break;
                }

                case "circle holding a curved loop":
                {
                    var inside = new GeoPolygonArc2(new[] { new GeoPoint2(-10, -5), new GeoPoint2(10, -5), new GeoPoint2(10, 5), new GeoPoint2(-10, 5) }, new[] { 0, 0.5, 0, 0.5 });
                    AssertMeasured(t, (expected, circle.DistanceTo(inside, t), inside.DistanceTo(circle, t), circle.CollidesWith(inside, t), inside.CollidesWith(circle, t)));
                    break;
                }

                default:
                {
                    var ray = new GeoRay2(new GeoPoint2(125.005, 50), GeoVector2.XAxis);
                    GeoPolygonArc2 loop = BowedLoop();
                    AssertMeasured(t, (expected, loop.DistanceTo(ray, t), ray.DistanceTo(loop, t), loop.CollidesWith(ray, t), ray.CollidesWith(loop, t)));
                    break;
                }
            }
        }

        #endregion
    }
}
