using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The distance from an ellipse is nought only where it truly meets the other shape, and otherwise the exact gap. From
    /// a polyline, a curved chain, a curved loop and a face it used to read nought wherever the nearest pair came within
    /// the point tolerance: a polyline starting 0.0005 off the end of the ellipse 60 by 25 was nought away at a tolerance of
    /// 0.001, though a segment there was 0.0005 away. The other shapes cannot be asked about an ellipse; it is asked from
    /// the ellipse, and only two ellipses are asked both ways round.
    /// </summary>
    public class Ellipse2ExactTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>The gap is exact to rounding: 4E-14 of the largest coordinate here, 200, with room.</summary>
        private const double Allowance = 1E-11;

        private static GeoPolygon2 Box(double left, double bottom, double right, double top) => new GeoPolygon2(
            new GeoPoint2(left, bottom), new GeoPoint2(right, bottom), new GeoPoint2(right, top), new GeoPoint2(left, top));

        /// <summary>The ellipse 60 by 25 about the origin, its major axis along X: its ends are (±60, 0) and (0, ±25).</summary>
        private static GeoEllipse2 Oval() => new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 60, 25);

        /// <summary>The ellipse 60 by 0.5 about the origin, along X.</summary>
        private static GeoEllipse2 Thin() => new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 60, 0.5);

        /// <summary>The ellipse 60 by 25 about (10, -5), turned 0.35 rad.</summary>
        private static GeoEllipse2 Turned() => new GeoEllipse2(new GeoPoint2(10, -5), new GeoVector2(Math.Cos(0.35), Math.Sin(0.35)), 60, 25);

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector);

        private static void AssertGap(double expected, double actual)
        {
            Assert.InRange(actual, expected - Allowance, expected + Allowance);
        }

        /// <summary>The distance from the ellipse is the gap, and the ellipse collides exactly where it is within the tolerance.</summary>
        private static void AssertMeasured(Tolerance tolerance, double gap, double distance, bool collides)
        {
            AssertGap(gap, distance);
            Assert.Equal(gap <= tolerance.EqualPoint, collides);
        }

        /// <summary>
        /// A site on the rim of an ellipse and its outward normal there, and the point h out along that normal: its nearest
        /// point of the rim is the site, so it is h away. At the eccentric angle t the site is the centre plus a cos t
        /// along the axis and b sin t across it, and the normal leans as (cos t / a, sin t / b).
        /// </summary>
        private static (GeoPoint2 Out, GeoVector2 Normal) OffTheRim(GeoEllipse2 ellipse, double t, double h)
        {
            GeoVector2 u = ellipse.MajorAxis;
            var v = new GeoVector2(-u.Y, u.X);
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double nx = Math.Cos(t) / a;
            double ny = Math.Sin(t) / b;
            double length = Math.Sqrt(nx * nx + ny * ny);
            nx /= length;
            ny /= length;
            var normal = new GeoVector2(nx * u.X + ny * v.X, nx * u.Y + ny * v.Y);
            double x = a * Math.Cos(t);
            double y = b * Math.Sin(t);
            var site = new GeoPoint2(ellipse.Center.X + x * u.X + y * v.X, ellipse.Center.Y + x * u.Y + y * v.Y);
            return (new GeoPoint2(site.X + h * normal.X, site.Y + h * normal.Y), normal);
        }

        /// <summary>
        /// The distance from the ellipse to a shape of the kind that starts h out from the rim at the eccentric angle t and
        /// runs away along the normal there, and whether the ellipse collides with it. The polyline and the chain go 40 out,
        /// then 20 aside (the chain's second edge bulging 0.3); the loop and the face are the triangle with that start as
        /// its tip and its other corners 40 out and 20 either side (the loop's far side bulging 0.3).
        /// </summary>
        private static (double Distance, bool Collides) FromTheEllipse(GeoEllipse2 ellipse, string kind, double t, double h, Tolerance tol)
        {
            (GeoPoint2 start, GeoVector2 n) = OffTheRim(ellipse, t, h);
            var side = new GeoVector2(-n.Y, n.X);
            var far = new GeoPoint2(start.X + 40 * n.X, start.Y + 40 * n.Y);
            var left = new GeoPoint2(far.X + 20 * side.X, far.Y + 20 * side.Y);
            var right = new GeoPoint2(far.X - 20 * side.X, far.Y - 20 * side.Y);

            switch (kind)
            {
                case "polyline":
                {
                    var chain = new GeoPolyline2(start, far, left);
                    return (ellipse.DistanceTo(chain, tol), ellipse.CollidesWith(chain, tol));
                }

                case "chain":
                {
                    var chain = new GeoPolylineArc2(new[] { start, far, left }, new[] { 0, 0.3, 0 });
                    return (ellipse.DistanceTo(chain, tol), ellipse.CollidesWith(chain, tol));
                }

                case "loop":
                {
                    var loop = new GeoPolygonArc2(new[] { start, right, left }, new[] { 0, 0.3, 0 });
                    return (ellipse.DistanceTo(loop, tol), ellipse.CollidesWith(loop, tol));
                }

                case "face":
                {
                    var face = new GeoFace2(new GeoPolygon2(start, right, left));
                    return (ellipse.DistanceTo(face, tol), ellipse.CollidesWith(face, tol));
                }

                case "segment":
                {
                    var line = new GeoLine2(start, far);
                    return (ellipse.DistanceTo(line, tol), ellipse.CollidesWith(line, tol));
                }

                default:
                {
                    var polygon = new GeoPolygon2(start, right, left);
                    return (ellipse.DistanceTo(polygon, tol), ellipse.CollidesWith(polygon, tol));
                }
            }
        }

        private static GeoEllipse2 Named(string ellipse) =>
            ellipse == "oval" ? Oval() : ellipse == "thin" ? Thin() : Turned();

        #region F4: a polyline, a chain, a loop or a face half a tolerance off the rim

        public static IEnumerable<object[]> HalfAToleranceOffCases()
        {
            // The end of the major axis (t = 0), the end of the minor one (t = pi / 2) and a flank (t = 0.9).
            foreach (string kind in new[] { "polyline", "chain", "loop", "face" })
            {
                foreach (double t in new[] { 0, Math.PI / 2, 0.9 })
                {
                    yield return new object[] { "oval", kind, t, 1E-3 };
                }
            }

            yield return new object[] { "oval", "polyline", 0.0, 1E-4 };
            yield return new object[] { "oval", "face", 0.9, 1E-4 };
            yield return new object[] { "thin", "polyline", 0.0, 1E-3 };
            yield return new object[] { "thin", "polyline", Math.PI / 2, 1E-3 };
            yield return new object[] { "thin", "face", 0.9, 1E-3 };
            yield return new object[] { "turned", "polyline", 0.0, 1E-3 };
            yield return new object[] { "turned", "chain", 0.9, 1E-3 };
            yield return new object[] { "turned", "loop", Math.PI / 2, 1E-3 };
        }

        [Theory]
        [MemberData(nameof(HalfAToleranceOffCases))]
        public void AShapeStartingHalfAToleranceOffTheRim_IsThatFarAndCollides(string ellipse, string kind, double t, double equalPoint)
        {
            Tolerance tol = At(equalPoint);
            double h = tol.EqualPoint / 2;
            (double distance, bool collides) = FromTheEllipse(Named(ellipse), kind, t, h, tol);

            AssertMeasured(tol, h, distance, collides);
        }

        [Theory]
        [InlineData(1E-3)]
        [InlineData(1E-4)]
        public void AnEllipseInAHoleHalfAToleranceFromItsRim_IsThatFarFromTheFace(double equalPoint)
        {
            // The hole runs from x = -70 to x = 60 + h and from y = -40 to 40: its right side is h off the end (60, 0), the
            // others 10 or more from the rim.
            Tolerance tol = At(equalPoint);
            double right = 60 + tol.EqualPoint / 2;
            var plate = new GeoFace2(Box(-100, -100, 100, 100), new[] { Box(-70, -40, right, 40) });
            GeoEllipse2 oval = Oval();

            AssertMeasured(tol, right - 60, oval.DistanceTo(plate, tol), oval.CollidesWith(plate, tol));
        }

        #endregion

        #region Controls: exact already, and must stay so

        [Theory]
        [InlineData("segment")]
        [InlineData("polygon")]
        public void AStraightShapeStartingHalfAToleranceOffTheRim_IsThatFar(string kind)
        {
            // From the flank (t = 0.9), as the F4 cases are.
            double h = Tol.EqualPoint / 2;
            (double distance, bool collides) = FromTheEllipse(Oval(), kind, 0.9, h, Tol);

            AssertMeasured(Tol, h, distance, collides);
        }

        [Theory]
        [InlineData("rectangle")]
        [InlineData("circle")]
        [InlineData("arc")]
        [InlineData("ellipse")]
        public void AShapeHalfAToleranceBeyondTheEnd_IsThatFar(string kind)
        {
            // Each has its nearest point at (60 + h, 0), facing the end (60, 0).
            double x = 60 + Tol.EqualPoint / 2;
            double gap = x - 60;
            GeoEllipse2 oval = Oval();

            switch (kind)
            {
                case "rectangle":
                {
                    // Its left side is x + 20 - 20, within a rounding of x.
                    var rect = new GeoRectangle2(x, -10, 40, 20);
                    AssertMeasured(Tol, gap, oval.DistanceTo(rect, Tol), oval.CollidesWith(rect, Tol));
                    break;
                }

                case "circle":
                {
                    var circle = new GeoCircle2(new GeoPoint2(x + 10, 0), 10);
                    AssertMeasured(Tol, gap, oval.DistanceTo(circle, Tol), oval.CollidesWith(circle, Tol));
                    break;
                }

                case "arc":
                {
                    // The left half of that circle, which faces the ellipse.
                    var arc = new GeoArc2(new GeoPoint2(x + 10, 0), 10, Math.PI / 2, 3 * Math.PI / 2);
                    AssertMeasured(Tol, gap, oval.DistanceTo(arc, Tol), oval.CollidesWith(arc, Tol));
                    break;
                }

                default:
                {
                    // The ellipse 30 by 10 about (x + 30, 0), asked both ways round.
                    var other = new GeoEllipse2(new GeoPoint2(x + 30, 0), GeoVector2.XAxis, 30, 10);
                    AssertMeasured(Tol, gap, oval.DistanceTo(other, Tol), oval.CollidesWith(other, Tol));
                    AssertMeasured(Tol, gap, other.DistanceTo(oval, Tol), other.CollidesWith(oval, Tol));
                    break;
                }
            }
        }

        [Theory]
        [InlineData(60.0, 0.0, 0.0)]
        [InlineData(0.0, 25.0, 0.0)]
        [InlineData(0.0, -25.0, 0.0)]
        [InlineData(59.9995, 0.0, 0.0)]
        [InlineData(60.0005, 0.0, 0.0005)]
        public void APointOnTheRimIsInside_AndOneJustOutsideIsItsGapAway(double x, double y, double expected)
        {
            // The ends of the axes lie on the rim exactly; 60.0005 less 60 is 0.0005 to within 3E-15.
            AssertGap(expected, Oval().DistanceTo(new GeoPoint2(x, y), Tol));
        }

        [Theory]
        [InlineData("segment tangent at the end", 0.0)]
        [InlineData("segment crossing it", 0.0)]
        [InlineData("polyline five tolerances off", 0.005)]
        [InlineData("chain five tolerances off", 0.005)]
        [InlineData("loop holding it", 0.0)]
        [InlineData("face holding it", 0.0)]
        [InlineData("polyline inside it", 0.0)]
        public void ATangencyACrossingAWideGapOrAHold_ReadsAsItDidAndCollidesWithinTheTolerance(string kind, double expected)
        {
            // Five tolerances off is 60.005 less 60, 0.005 to within 4E-15.
            GeoEllipse2 oval = Oval();
            var start = new GeoPoint2(60.005, 0);

            switch (kind)
            {
                case "segment tangent at the end":
                {
                    var line = new GeoLine2(new GeoPoint2(60, -10), new GeoPoint2(60, 10));
                    AssertMeasured(Tol, expected, oval.DistanceTo(line, Tol), oval.CollidesWith(line, Tol));
                    break;
                }

                case "segment crossing it":
                {
                    var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
                    AssertMeasured(Tol, expected, oval.DistanceTo(line, Tol), oval.CollidesWith(line, Tol));
                    break;
                }

                case "polyline five tolerances off":
                {
                    var chain = new GeoPolyline2(start, new GeoPoint2(100, 0), new GeoPoint2(100, 20));
                    AssertMeasured(Tol, expected, oval.DistanceTo(chain, Tol), oval.CollidesWith(chain, Tol));
                    break;
                }

                case "chain five tolerances off":
                {
                    var chain = new GeoPolylineArc2(new[] { start, new GeoPoint2(100, 0), new GeoPoint2(100, 20) }, new[] { 0, 0.3, 0 });
                    AssertMeasured(Tol, expected, oval.DistanceTo(chain, Tol), oval.CollidesWith(chain, Tol));
                    break;
                }

                case "loop holding it":
                {
                    // Its right side 0.0005 off the end (60, 0): held all the same.
                    var loop = new GeoPolygonArc2(new[] { new GeoPoint2(-70, -40), new GeoPoint2(60.0005, -40), new GeoPoint2(60.0005, 40), new GeoPoint2(-70, 40) }, new[] { 0, 0, 0.3, 0 });
                    AssertMeasured(Tol, expected, oval.DistanceTo(loop, Tol), oval.CollidesWith(loop, Tol));
                    break;
                }

                case "face holding it":
                {
                    var face = new GeoFace2(Box(-70, -40, 60.0005, 40));
                    AssertMeasured(Tol, expected, oval.DistanceTo(face, Tol), oval.CollidesWith(face, Tol));
                    break;
                }

                default:
                {
                    var inside = new GeoPolyline2(new GeoPoint2(-10, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 5));
                    AssertMeasured(Tol, expected, oval.DistanceTo(inside, Tol), oval.CollidesWith(inside, Tol));
                    break;
                }
            }
        }

        #endregion
    }
}
