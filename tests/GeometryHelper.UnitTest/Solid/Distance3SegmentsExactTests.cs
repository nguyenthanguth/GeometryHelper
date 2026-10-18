using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// In space, a segment and a ray are as far apart as they truly are, and segments meeting at a slant meet exactly,
    /// the same both ways round. A ray whose origin stood within the point tolerance of a segment used to be measured
    /// from its origin alone, so one starting 0.0005 below a segment and running up through it was 0.0005 away, and one
    /// passing 0.00028 off the segment's end was 0.00035 away; and a segment ending exactly on another, turned out of
    /// line by 8.3E-6 rad, was 2.7E-11 away one way round and 9.5E-16 the other.
    /// </summary>
    public class Distance3SegmentsExactTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>A plane in space: an origin, two axes in it and its normal.</summary>
        private sealed class Frame
        {
            public GeoPoint3 O;
            public GeoVector3 X;
            public GeoVector3 Y;
            public GeoVector3 N;

            /// <summary>The gap is exact to rounding: 4E-14 of the largest coordinate, 60 on the floor and 3 500 out there.</summary>
            public double Allowance;

            public GeoPoint3 P(double x, double y, double h = 0) => new GeoPoint3(
                O.X + x * X.X + y * Y.X + h * N.X, O.Y + x * X.Y + y * Y.Y + h * N.Y, O.Z + x * X.Z + y * Y.Z + h * N.Z);

            public GeoVector3 V(double x, double y, double h = 0) => new GeoVector3(
                x * X.X + y * Y.X + h * N.X, x * X.Y + y * Y.Y + h * N.Y, x * X.Z + y * Y.Z + h * N.Z);
        }

        /// <summary>The floor z = 0, or a plane turned 0.6 rad about Z and 0.8 about X, through (3000.25, -1500.5, 800.125).</summary>
        private static Frame On(string plane)
        {
            if (plane == "floor")
            {
                return new Frame { O = new GeoPoint3(0, 0, 0), X = GeoVector3.XAxis, Y = GeoVector3.YAxis, N = GeoVector3.ZAxis, Allowance = 2.4E-12 };
            }

            double ca = Math.Cos(0.6), sa = Math.Sin(0.6), cb = Math.Cos(0.8), sb = Math.Sin(0.8);
            return new Frame
            {
                O = new GeoPoint3(3000.25, -1500.5, 800.125),
                X = new GeoVector3(ca, sa, 0),
                Y = new GeoVector3(-sa * cb, ca * cb, sb),
                N = new GeoVector3(sa * sb, -ca * sb, cb),
                Allowance = 2E-10,
            };
        }

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

        private static void AssertGap(double expected, double actual, double allowance)
        {
            Assert.InRange(actual, expected - allowance, expected + allowance);
        }

        /// <summary>
        /// Both ways round the distance is the gap and the same to the bit, and both ways round they collide exactly where it
        /// is within the tolerance.
        /// </summary>
        private static void AssertMeasured(Tolerance t, double allowance, (double Gap, double There, double Back, bool Collides, bool CollidesBack) m)
        {
            AssertGap(m.Gap, m.There, allowance);
            Assert.Equal(m.There, m.Back);
            Assert.Equal(m.Gap <= t.EqualPoint, m.Collides);
            Assert.Equal(m.Gap <= t.EqualPoint, m.CollidesBack);
        }

        private static (double, double, bool, bool) Measure(GeoLine3 line, GeoRay3 ray, Tolerance t) =>
            (Within(t, () => line.DistanceTo(ray)), Within(t, () => ray.DistanceTo(line)), line.CollidesWith(ray, t), ray.CollidesWith(line, t));

        #region T8: a segment and a ray whose origin is near it

        public static IEnumerable<object[]> RayCases()
        {
            foreach (string plane in new[] { "floor", "turned" })
            {
                foreach (string kind in new[]
                {
                    "ray through the segment's end", "ray passing 0.00028 off the end", "the same 0.0003 up",
                    "ray from 0.0005 below, up through the side", "the same 0.0009 up, over the side",
                })
                {
                    yield return new object[] { kind, plane };
                }
            }
        }

        [Theory]
        [MemberData(nameof(RayCases))]
        public void ARayWhoseOriginIsWithinTheToleranceOfASegment_IsAsFarAsItsNearestPoint(string kind, string plane)
        {
            // The segment from (0, 0) to (60, 0). The rays start half a tolerance off it; what they pass is what counts.
            Frame f = On(plane);
            Tolerance t = Tol;
            var line = new GeoLine3(f.P(0, 0), f.P(60, 0));
            double h = 0.0005;
            double r = Math.Sqrt(0.5);
            double ox = 60 - h * r, oy = -h * r;
            double gap;
            GeoRay3 ray;

            switch (kind)
            {
                case "ray through the segment's end":
                    // From h below the end (60, 0) along the diagonal back from it, going up the diagonal: through the end.
                    ray = new GeoRay3(f.P(ox, oy), f.V(r, r));
                    gap = 0;
                    break;

                case "ray passing 0.00028 off the end":
                {
                    // The same origin, the ray turned 0.6 rad down from the diagonal: it passes the end h r (dx - dy) away,
                    // 0.00028, nearer than its origin (h) is.
                    double dx = Math.Cos(Math.PI / 4 - 0.6), dy = Math.Sin(Math.PI / 4 - 0.6);
                    ray = new GeoRay3(f.P(ox, oy), f.V(dx, dy));
                    gap = h * r * (dx - dy);
                    break;
                }

                case "the same 0.0003 up":
                {
                    double dx = Math.Cos(Math.PI / 4 - 0.6), dy = Math.Sin(Math.PI / 4 - 0.6);
                    ray = new GeoRay3(f.P(ox, oy, 0.0003), f.V(dx, dy));
                    double inPlane = h * r * (dx - dy);
                    gap = Math.Sqrt(inPlane * inPlane + 0.0003 * 0.0003);
                    break;
                }

                case "the same 0.0009 up, over the side":
                    // It passes straight over the segment, 0.0009 above it; its origin is 0.00103 away.
                    ray = new GeoRay3(f.P(25, -h, 0.0009), f.V(0, 1));
                    gap = 0.0009;
                    break;

                default:
                    ray = new GeoRay3(f.P(25, -h), f.V(0, 1));
                    gap = 0;
                    break;
            }

            AssertMeasured(t, f.Allowance, (gap, Measure(line, ray, t).Item1, Measure(line, ray, t).Item2, line.CollidesWith(ray, t), ray.CollidesWith(line, t)));
        }

        [Fact]
        public void ARayFromHalfACoarseToleranceBelowASegmentUpThroughIt_IsNoughtAway()
        {
            // At a tolerance of 0.1 the origin is 0.05 below; the ray crosses the segment all the same.
            var coarse = new Tolerance(0.1, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 0.1);
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(60, 0, 0));
            var ray = new GeoRay3(new GeoPoint3(25, -0.05, 0), new GeoVector3(0, 1, 0));
            (double there, double back, bool c1, bool c2) = Measure(line, ray, coarse);

            AssertMeasured(coarse, 2.4E-12, (0.0, there, back, c1, c2));
        }

        #endregion

        #region T9: segments meeting at a slant

        public static IEnumerable<object[]> SlantCases()
        {
            foreach (double slope in new[] { 1E-9, 1E-7, 1E-5, 1E-3 })
            {
                foreach (string kind in new[] { "ending on it", "crossing it", "0.0005 beside it" })
                {
                    yield return new object[] { kind, slope };
                }
            }
        }

        /// <summary>
        /// A segment 60 long along X but rising by slope, placed against the segment from (0, 0, 0) to (60, 0, 0): ending
        /// exactly on it at (0.0005, 0, 0), coming up from below; crossing it at its middle (30, 0, 0); or ending 0.0005
        /// beside it, at (0.0005, 0.0005, 0). The exact gaps are nought, nought and 0.0005.
        /// </summary>
        private static (GeoPoint3 Start, GeoPoint3 End, double Gap) Slanted(string kind, double slope)
        {
            switch (kind)
            {
                case "ending on it": return (new GeoPoint3(0.0005 - 60, 0, -60 * slope), new GeoPoint3(0.0005, 0, 0), 0.0);
                case "crossing it": return (new GeoPoint3(0, 0, -30 * slope), new GeoPoint3(60, 0, 30 * slope), 0.0);
                default: return (new GeoPoint3(0.0005 - 60, 0.0005, -60 * slope), new GeoPoint3(0.0005, 0.0005, 0), 0.0005);
            }
        }

        [Theory]
        [MemberData(nameof(SlantCases))]
        public void ASegmentAtASlantToAnother_IsExactlyAsFarAndTheSameBothWaysRound(string kind, double slope)
        {
            Tolerance t = Tol;
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(60, 0, 0));
            (GeoPoint3 s, GeoPoint3 e, double gap) = Slanted(kind, slope);
            var other = new GeoLine3(s, e);

            AssertMeasured(t, 2.4E-12, (gap, Within(t, () => line.DistanceTo(other)), Within(t, () => other.DistanceTo(line)),
                line.CollidesWith(other, t), other.CollidesWith(line, t)));
        }

        /// <summary>
        /// Shapes the audit's tilted scenes turned out of the floor by 8.3E-6 rad about the line through the end they meet at,
        /// each ending exactly on the other: (0.0005, 0, 0), (59.9995, 0, 0) and (99.9995, 30, 0) lie on the first shape. The
        /// doubles are those of the scenes, from which the rounding that tripped the distance came.
        /// </summary>
        private static object TiltedShape(string name)
        {
            switch (name)
            {
                case "segment": return new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(60, 0, 0));
                case "polyline": return new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0), new GeoPoint3(65, 30, 0), new GeoPoint3(100, 30, 0));
                case "segment ending on its start":
                    return new GeoLine3(new GeoPoint3(-59.999499997916665, 0, -0.000499999999994213), new GeoPoint3(0.0005, 0, 0));
                case "segment ending on its end":
                    return new GeoLine3(new GeoPoint3(119.99949999791667, 6.0641744252141e-50, -0.000499999999994213), new GeoPoint3(59.9995, 0, 0));
                case "polyline starting on its end":
                    return new GeoPolyline3(new GeoPoint3(59.9995, 0, 0), new GeoPoint3(99.99949999861111, 0, -0.00033333333332947535),
                        new GeoPoint3(124.99949999774306, 30, -0.0005416666666603974), new GeoPoint3(159.9994999965278, 30, -0.0008333333333236883));
                case "polyline ending on its start":
                    return new GeoPolyline3(new GeoPoint3(-99.99949999652777, -30, -0.0008333333333236883), new GeoPoint3(-59.999499997916665, -30, -0.000499999999994213),
                        new GeoPoint3(-34.99949999878472, 0, -0.0002916666666632909), new GeoPoint3(0.0005, 0, 0));
                default:
                    return new GeoPolyline3(new GeoPoint3(99.9995, 30, 0), new GeoPoint3(139.9994999986111, 30, -0.00033333333332947535),
                        new GeoPoint3(164.99949999774304, 60, -0.0005416666666603974), new GeoPoint3(199.9994999965278, 60, -0.0008333333333236883));
            }
        }

        [Theory]
        [InlineData("segment", "segment ending on its start")]
        [InlineData("segment", "segment ending on its end")]
        [InlineData("segment", "polyline starting on its end")]
        [InlineData("polyline", "polyline ending on its start")]
        [InlineData("polyline", "polyline starting on its end, the polyline's")]
        public void AShapeTiltedOutOfTheFloorEndingOnAnother_IsNoughtAwayTheSameBothWaysRound(string first, string second)
        {
            Tolerance t = Tol;
            object a = TiltedShape(first);
            object b = TiltedShape(second);
            double there, back;
            bool c1, c2;

            if (a is GeoLine3 la && b is GeoLine3 lb)
            {
                there = Within(t, () => la.DistanceTo(lb));
                back = Within(t, () => lb.DistanceTo(la));
                c1 = la.CollidesWith(lb, t);
                c2 = lb.CollidesWith(la, t);
            }
            else if (a is GeoLine3 l && b is GeoPolyline3 p)
            {
                there = l.DistanceTo(p, t);
                back = p.DistanceTo(l, t);
                c1 = l.CollidesWith(p, t);
                c2 = p.CollidesWith(l, t);
            }
            else
            {
                var pa = (GeoPolyline3)a;
                var pb = (GeoPolyline3)b;
                there = pa.DistanceTo(pb, t);
                back = pb.DistanceTo(pa, t);
                c1 = pa.CollidesWith(pb, t);
                c2 = pb.CollidesWith(pa, t);
            }

            AssertMeasured(t, 2.4E-12, (0.0, there, back, c1, c2));
        }

        #endregion

        #region Controls: what already reads right and must stay so

        [Theory]
        [InlineData("ray leaving from 0.0005 below", "floor")]
        [InlineData("ray leaving from 0.0005 below", "turned")]
        [InlineData("ray crossing from far below", "floor")]
        [InlineData("ray crossing from far below", "turned")]
        [InlineData("ray slanting up through the side from 0.0005 below", "floor")]
        [InlineData("ray slanting up through the side from 0.0005 below", "turned")]
        [InlineData("segments crossing in plan 0.0005 apart", "floor")]
        [InlineData("segments end to end 0.0012 apart", "floor")]
        [InlineData("segments touching end to end", "floor")]
        public void ARayLeavingARayCrossingOrSegmentsApartOrTouching_ReadAsTheyDid(string kind, string plane)
        {
            Frame f = On(plane);
            Tolerance t = Tol;
            var line = new GeoLine3(f.P(0, 0), f.P(60, 0));

            switch (kind)
            {
                case "ray leaving from 0.0005 below":
                {
                    var ray = new GeoRay3(f.P(25, -0.0005), f.V(0, -1));
                    (double a, double b, bool c1, bool c2) = Measure(line, ray, t);
                    AssertMeasured(t, f.Allowance, (0.0005, a, b, c1, c2));
                    break;
                }

                case "ray slanting up through the side from 0.0005 below":
                {
                    var ray = new GeoRay3(f.P(25, -0.0005), f.V(1, 2));
                    (double a, double b, bool c1, bool c2) = Measure(line, ray, t);
                    AssertMeasured(t, f.Allowance, (0.0, a, b, c1, c2));
                    break;
                }

                case "ray crossing from far below":
                {
                    var ray = new GeoRay3(f.P(25, -50), f.V(0, 1));
                    (double a, double b, bool c1, bool c2) = Measure(line, ray, t);
                    AssertMeasured(t, f.Allowance, (0.0, a, b, c1, c2));
                    break;
                }

                case "segments crossing in plan 0.0005 apart":
                {
                    var other = new GeoLine3(f.P(5, -5, 0.0005), f.P(5, 5, 0.0005));
                    AssertMeasured(t, f.Allowance, (0.0005, Within(t, () => line.DistanceTo(other)), Within(t, () => other.DistanceTo(line)),
                        line.CollidesWith(other, t), other.CollidesWith(line, t)));
                    break;
                }

                case "segments end to end 0.0012 apart":
                {
                    var other = new GeoLine3(f.P(60.0012, 0), f.P(100, 0));
                    AssertMeasured(t, f.Allowance, (60.0012 - 60, Within(t, () => line.DistanceTo(other)), Within(t, () => other.DistanceTo(line)),
                        line.CollidesWith(other, t), other.CollidesWith(line, t)));
                    break;
                }

                default:
                {
                    var other = new GeoLine3(f.P(60, 0), f.P(100, 0));
                    AssertMeasured(t, f.Allowance, (0.0, Within(t, () => line.DistanceTo(other)), Within(t, () => other.DistanceTo(line)),
                        line.CollidesWith(other, t), other.CollidesWith(line, t)));
                    break;
                }
            }
        }

        #endregion
    }
}
