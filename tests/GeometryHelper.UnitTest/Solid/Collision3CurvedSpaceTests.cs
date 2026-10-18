using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Curved shapes in space collide with another shape exactly where the gap between them is within the point tolerance,
    /// also where the shapes do not lie in one plane, where the other shape is a body, and where the curve is nearly a
    /// straight line or nearly tangent to the other shape.
    /// </summary>
    public class Collision3CurvedSpaceTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        // The dome: the upper half of the circle of radius 50 about the origin in the floor, from (-50, 0) over (0, 50) to (50, 0).
        private static GeoArc3 Dome() => GeoArc3.FromThreePoints(new GeoPoint3(-50, 0, 0), new GeoPoint3(0, 50, 0), new GeoPoint3(50, 0, 0));

        // A block 20 by 20 by 50 whose top is in the floor and whose side facing the dome stands at y = 50 + gap.
        private static GeoObb3 BlockUnder(double gap)
            => new GeoObb3(new GeoPoint3(0, 60 + gap, -25), 20, 20, 50, GeoVector3.XAxis, GeoVector3.YAxis);

        [Theory]
        [InlineData(0.0005, true)]
        [InlineData(0.0009, true)]
        [InlineData(0.0012, false)]
        [InlineData(0.003, false)]
        public void ASolidWhoseSideFacesTheTopOfADome_CollidesWhereTheGapIsWithinTheTolerance(double gap, bool expected)
        {
            GeoArc3 dome = Dome();
            GeoSolid3 solid = BlockUnder(gap).ToSolid();

            Assert.Equal(expected, dome.CollidesWith(solid, Tol));
            Assert.Equal(expected, solid.CollidesWith(dome, Tol));
        }

        [Theory]
        [InlineData(0.0005, true)]
        [InlineData(0.0012, false)]
        public void AnAxisAlignedBoxWhoseSideFacesTheTopOfADome_CollidesWhereTheGapIsWithinTheTolerance(double gap, bool expected)
        {
            GeoArc3 dome = Dome();
            var box = new GeoAabb3(new GeoPoint3(-10, 50 + gap, -50), new GeoPoint3(10, 70 + gap, 0));

            Assert.Equal(expected, dome.CollidesWith(box, Tol));
            Assert.Equal(expected, box.CollidesWith(dome, Tol));
        }

        [Fact]
        public void ACircleLyingInTheTopOfASolid_Collides()
        {
            GeoSolid3 solid = new GeoObb3(new GeoPoint3(0, 0, -25), 100, 100, 50, GeoVector3.XAxis, GeoVector3.YAxis).ToSolid();
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10);

            Assert.True(circle.CollidesWith(solid, Tol));
            Assert.True(solid.CollidesWith(circle, Tol));
        }

        public static System.Collections.Generic.IEnumerable<object[]> ThinBoxes()
        {
            // 100 by 100 and 0.0019 thick, a hair under the point tolerance: as an axis-aligned box from z = 0, and as an oriented one.
            yield return new object[] { new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 0.0019)) };
            yield return new object[] { new GeoObb3(new GeoPoint3(50, 50, 0.00095), 100, 100, 0.0019, GeoVector3.XAxis, GeoVector3.YAxis) };
        }

        private static bool Hit(GeoCircle3 circle, object box)
            => box is GeoAabb3 a ? circle.CollidesWith(a, Tol) : circle.CollidesWith((GeoObb3)box, Tol);

        private static bool HitBy(object box, GeoCircle3 circle)
            => box is GeoAabb3 a ? a.CollidesWith(circle, Tol) : ((GeoObb3)box).CollidesWith(circle, Tol);

        [Theory]
        [MemberData(nameof(ThinBoxes))]
        public void ACircleInsideABoxThinnerThanTheTolerance_Collides(object box)
        {
            // In the plane half way through the slab, wholly within its footprint: no face is crossed.
            var circle = new GeoCircle3(new GeoPoint3(50, 50, 0.00095), GeoVector3.ZAxis, 10);

            Assert.True(Hit(circle, box));
            Assert.True(HitBy(box, circle));
        }

        [Theory]
        [MemberData(nameof(ThinBoxes))]
        public void ACircleCrossingABoxThinnerThanTheTolerance_Collides(object box)
        {
            var circle = new GeoCircle3(new GeoPoint3(50, 50, 0), GeoVector3.YAxis, 10);

            Assert.True(Hit(circle, box));
            Assert.True(HitBy(box, circle));
        }

        [Theory]
        [MemberData(nameof(ThinBoxes))]
        public void ACircleThirtyAboveABoxThinnerThanTheTolerance_DoesNotCollide(object box)
        {
            var circle = new GeoCircle3(new GeoPoint3(50, 50, 30), GeoVector3.ZAxis, 10);

            Assert.False(Hit(circle, box));
            Assert.False(HitBy(box, circle));
        }

        [Theory]
        [InlineData(0.0095, true)]
        [InlineData(0.0105, false)]
        public void ACircleBesideABoxOnlyAFewTolerancesWide_CollidesWhereItsRimIsWithinTheTolerance(double radius, bool expected)
        {
            // 0.018 by 0.0021 by 0.0023: its end face is at x = 0.009, so the rim of a circle about its middle passes the end
            // 0.0005 off, or 0.0015 off.
            var box = new GeoObb3(GeoPoint3.Origin, 0.018, 0.0021, 0.0023, GeoVector3.XAxis, GeoVector3.YAxis);
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, radius);

            Assert.Equal(expected, circle.CollidesWith(box, Tol));
            Assert.Equal(expected, box.CollidesWith(circle, Tol));

            // A half circle through the same two points, as a chain.
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(radius, 0, 0), new GeoPoint3(-radius, 0, 0) }, new[] { 1.0, 0.0 }, new[] { GeoVector3.ZAxis, GeoVector3.ZAxis });
            Assert.Equal(expected, chain.CollidesWith(box, Tol));
            Assert.Equal(expected, box.CollidesWith(chain, Tol));
        }

        [Fact]
        public void AnArcWhollyInsideASolid_Collides()
        {
            GeoSolid3 solid = new GeoObb3(GeoPoint3.Origin, 100, 100, 100, GeoVector3.XAxis, GeoVector3.YAxis).ToSolid();
            GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(-5, 0, 0), new GeoPoint3(0, 5, 0), new GeoPoint3(5, 0, 0));

            Assert.True(arc.CollidesWith(solid, Tol));
            Assert.True(solid.CollidesWith(arc, Tol));
        }

        [Theory]
        [InlineData(20.0005, true)]
        [InlineData(20.0009, true)]
        [InlineData(20.0012, false)]
        [InlineData(20.003, false)]
        public void TwoCirclesInPerpendicularPlanes_CollideWhereTheirNearestPointsAreWithinTheTolerance(double centreX, bool expected)
        {
            // The first in the floor about the origin, the second in the plane y = 0 about (centreX, 0, 0): their nearest points
            // are (10, 0, 0) and (centreX - 10, 0, 0).
            var first = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10);
            var second = new GeoCircle3(new GeoPoint3(centreX, 0, 0), GeoVector3.YAxis, 10);

            Assert.Equal(expected, first.CollidesWith(second, Tol));
            Assert.Equal(expected, second.CollidesWith(first, Tol));
        }

        [Theory]
        [InlineData(0.0003, 0.0005, true)]
        [InlineData(0.0009, 0.0009, false)]
        [InlineData(0.0003, 0.0012, false)]
        public void AnArcAndACircleOnOneAxisInPlanesApart_CollideWhereTheirGapInSpaceIsWithinTheTolerance(double height, double plan, bool expected)
        {
            // Concentric, the circle `height` over the arc's plane and `plan` wider: the gap in space is the root of the two squared.
            GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(50, 0, 0), new GeoPoint3(0, 50, 0), new GeoPoint3(-50, 0, 0));
            var circle = new GeoCircle3(new GeoPoint3(0, 0, height), GeoVector3.ZAxis, 50 + plan);

            Assert.Equal(expected, arc.CollidesWith(circle, Tol));
            Assert.Equal(expected, circle.CollidesWith(arc, Tol));
        }

        [Theory]
        [InlineData(0.005, true)]
        [InlineData(0.0005, true)]
        [InlineData(0.0, true)]
        [InlineData(-0.0005, true)]
        [InlineData(-0.0012, false)]
        public void ACircleGrazingASegmentsLine_CollidesWhereItCrossesOrComesWithinTheTolerance(double top, bool expected)
        {
            // The circle of radius 40 whose top stands `top` over the line y = 0, at x = 25: it crosses the line twice, a little
            // apart, when the top is over it, and stands clear of it by the shortfall when it is not.
            var circle = new GeoCircle3(new GeoPoint3(25, top - 40, 0), GeoVector3.ZAxis, 40);
            var segment = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var ray = new GeoRay3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis);

            Assert.Equal(expected, circle.CollidesWith(segment, Tol));
            Assert.Equal(expected, segment.CollidesWith(circle, Tol));
            Assert.Equal(expected, circle.CollidesWith(ray, Tol));
            Assert.Equal(expected, ray.CollidesWith(circle, Tol));
        }

        [Theory]
        [InlineData(0.0005, true)]
        [InlineData(0.0012, false)]
        public void AnArcBowingFiveBillionthsOfAMillimetre_IsReadAsItsChord(double off, bool expected)
        {
            // 100 long, bulging a ten-billionth, which bows it 5E-9: its chord is the segment, and a segment starting `off` beyond its end reads as that far.
            GeoPolylineArc3 chain = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0) },
                new[] { 1E-10, 0.0 },
                new[] { GeoVector3.ZAxis, GeoVector3.ZAxis });
            var line = new GeoLine3(new GeoPoint3(100 + off, 0, 0), new GeoPoint3(150, 0, 0));

            Assert.Equal(expected, chain.CollidesWith(line, Tol));
            Assert.Equal(expected, line.CollidesWith(chain, Tol));
        }

        [Theory]
        [InlineData(-5.0, true)]
        [InlineData(0.0, true)]
        [InlineData(0.0005, true)]
        [InlineData(0.0012, false)]
        [InlineData(0.2, false)]
        public void TwoArcsOfOneCircle_CollideWhereTheirSweepsOverlapOrTheirEndsStandWithinTheTolerance(double gapAlongTheCircle, bool expected)
        {
            // The first runs a half radian round the circle of radius 100; the second starts the given distance past its end
            // (before it, where negative) and runs on 0.4 radians.
            const double radius = 100.0;
            double begin = 0.5 + gapAlongTheCircle / radius;
            var first = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, radius, 0.0, 0.5);
            var second = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, radius, begin, begin + 0.4);

            Assert.Equal(expected, first.CollidesWith(second, Tol));
            Assert.Equal(expected, second.CollidesWith(first, Tol));
        }

        [Theory]
        [InlineData(20.0, true)]
        [InlineData(20.0005, true)]
        [InlineData(20.0012, false)]
        [InlineData(19.9995, true)]
        [InlineData(19.9988, true)]
        [InlineData(19.99, true)]
        public void TwoCirclesInOnePlaneOfRadius10_CollideWhereTheyTouchCrossOrStandWithinTheTolerance(double centreX, bool expected)
        {
            var first = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10);
            var second = new GeoCircle3(new GeoPoint3(centreX, 0, 0), GeoVector3.ZAxis, 10);

            Assert.Equal(expected, first.CollidesWith(second, Tol));
            Assert.Equal(expected, second.CollidesWith(first, Tol));
        }

        [Theory]
        [InlineData(0.00099, 0.0, true)]
        [InlineData(0.00101, 0.0, false)]
        [InlineData(-0.00099, 0.0, true)]
        [InlineData(0.0009, 0.0004, true)]
        [InlineData(0.0010, 0.0004, false)]
        public void ASegmentAlongTheTangentOfAnArcOfRadius111_CollidesWhereItsGapIsWithinTheTolerance(double off, double lift, bool expected)
        {
            // The arc of radius 111.5 from angle 0 to 0.3 about the origin; at angle 0.15 a segment 60 long runs along the tangent, `off` out from the
            // circle (in, where negative: it then crosses) and `lift` over its plane. The roots of the distance along the arc stand
            // a thousandth of a radian apart, which is what a bend or a fillet beside a straight bar looks like.
            const double radius = 111.5;
            var arc = GeoArc3.FromThreePoints(new GeoPoint3(radius, 0, 0), new GeoPoint3(radius * Math.Cos(0.15), radius * Math.Sin(0.15), 0), new GeoPoint3(radius * Math.Cos(0.3), radius * Math.Sin(0.3), 0));
            double angle = 0.15;
            var radial = new GeoVector3(Math.Cos(angle), Math.Sin(angle), 0);
            var tangent = new GeoVector3(-Math.Sin(angle), Math.Cos(angle), 0);
            GeoPoint3 middle = new GeoPoint3(radial.X * (radius + off), radial.Y * (radius + off), lift);
            var segment = new GeoLine3(middle.Add(tangent.Multiply(-30)), middle.Add(tangent.Multiply(30)));

            Assert.Equal(expected, arc.CollidesWith(segment, Tol));
            Assert.Equal(expected, segment.CollidesWith(arc, Tol));
        }

        [Theory]
        [InlineData(190.0)]
        [InlineData(1108.0)]
        [InlineData(2000.0)]
        [InlineData(10000.0)]
        public void ASegmentAlongTheTangentHalfATurnRound_CollidesWhereItsLiftIsWithinTheTolerance(double radius)
        {
            // The circle of the given radius about a centre up to 10 000 from the origin, in a plane turned every way; the segment runs
            // along the tangent at the point opposite the arc's start, lifted over the plane by delta. There the distance along the arc
            // has a triple root, and the quartic one of its roots at infinity.
            var random = new Random(1);

            for (int repetition = 0; repetition < 300; repetition++)
            {
                var normal = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalize();
                var centre = new GeoPoint3((random.NextDouble() - 0.5) * 2E4, (random.NextDouble() - 0.5) * 2E4, (random.NextDouble() - 0.5) * 2E4);
                new GeoPlane3(centre, normal).GetAxes(out GeoVector3 u, out GeoVector3 v);

                var arc = new GeoArc3(centre, normal, radius, 0.0, 2.0 * Math.PI - 1E-3);

                foreach (double lift in new[] { 0.0, 0.0005, 0.0009, 0.0011, 0.003 })
                {
                    GeoPoint3 foot = centre.Add(u.Multiply(-radius)).Add(normal.Multiply(lift));
                    var segment = new GeoLine3(foot.Add(v.Multiply(-0.5 * radius)), foot.Add(v.Multiply(0.5 * radius)));
                    bool expected = lift <= Tol.EqualPoint;

                    Assert.True(expected == arc.CollidesWith(segment, Tol), $"radius {radius}, lift {lift}, repetition {repetition}: arc against the segment");
                    Assert.True(expected == segment.CollidesWith(arc, Tol), $"radius {radius}, lift {lift}, repetition {repetition}: segment against the arc");
                }
            }
        }

        public static System.Collections.Generic.IEnumerable<object[]> BesideTheHalfTurnCases()
        {
            // Circles of radius 68 000 to 82 000 about centres up to 10 000 from the origin, a segment along the tangent a hair off half
            // a turn round (within 1E-6 rad) and tilted a few millionths out of the plane, its distance a little under 0.001: the roots
            // of the distance along the arc stand there a hair from z = infinity, where the quartic is known loosely.
            // Each row: centre, normal, radius, start and end angle, the segment's ends, then the least distance reached (found by
            // searching the arc's points) and a tolerance between that and what the build before read.
            yield return new object[]
            {
                new[] { 7428.5946681297355, -3351.2199918512351, -7446.0695858328, 0.613076050060039, 0.46543649217737182, 0.63836245863334085,
                        68086.1054574754, 0, 6.2831853071795862, 63720.302683779082, -23075.795406024939, -47126.667208967119,
                        49104.955028085256, 17048.070137295646, -62344.8275028526 },
                0.00089624418837998927, 0.000898,
            };
            yield return new object[]
            {
                new[] { 7475.1606013044538, 259.6223681511467, 902.27316641354525, -0.23582832345988572, -0.857927056238411, -0.45644952407489009,
                        81690.21612081585, 0, 6.2831853071795862, 7346.7811453907834, 38656.813167308908, -71201.45850905808,
                        7510.5877292288287, 38621.719095767592, -71220.129856501386 },
                0.000994443464834369, 0.000998,
            };
            yield return new object[]
            {
                new[] { -197.81151329996584, -8545.3179471871426, 5278.67856215624, 0.50536437852301563, 0.65235282894321078, 0.56483858888166982,
                        76383.428373700837, 2.81639685224992, 9.0995821594295059, -5754.916393081282, -56083.749669919365, 65154.529687670271,
                        1683.9539615500832, -59377.318743665368, 62302.799791588164 },
                0.00089942959517132063, 0.0008997,
            };
        }

        [Theory]
        [MemberData(nameof(BesideTheHalfTurnCases))]
        public void ASegmentBesideTheHalfTurnOfAHugeArc_CollidesExactlyWhereItsDistanceIsWithinTheTolerance(double[] v, double reached, double between)
        {
            var arc = new GeoArc3(new GeoPoint3(v[0], v[1], v[2]), new GeoVector3(v[3], v[4], v[5]), v[6], v[7], v[8]);
            var segment = new GeoLine3(new GeoPoint3(v[9], v[10], v[11]), new GeoPoint3(v[12], v[13], v[14]));

            // At a tolerance between what the arc reaches and what the build before read, the arc collides; a hair under what it
            // reaches, it does not.
            var over = new Tolerance(between, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, between);
            var under = new Tolerance(reached - 1E-7, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, reached - 1E-7);

            Assert.True(arc.CollidesWith(segment, over));
            Assert.True(segment.CollidesWith(arc, over));
            Assert.False(arc.CollidesWith(segment, under));
            Assert.False(segment.CollidesWith(arc, under));
        }

        [Theory]
        [InlineData(190.0)]
        [InlineData(2000.0)]
        [InlineData(5000.0)]
        public void ASegmentTouchingACircleInItsPlane_IsNoFurtherThanTheFineToleranceFromIt(double radius)
        {
            // Touching, the gap is nought; a tolerance of a ten-millionth is what the loose root of a triple one used to overshoot.
            var fine = new Tolerance(1E-7, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-7);
            var random = new Random(3);

            for (int repetition = 0; repetition < 40; repetition++)
            {
                var normal = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalize();
                var centre = new GeoPoint3((random.NextDouble() - 0.5) * 2E4, (random.NextDouble() - 0.5) * 2E4, (random.NextDouble() - 0.5) * 2E4);
                new GeoPlane3(centre, normal).GetAxes(out GeoVector3 u, out GeoVector3 v);
                double angle = random.NextDouble() * 2.0 * Math.PI;
                var circle = new GeoCircle3(centre, normal, radius);
                GeoPoint3 touch = centre.Add(u.Multiply(radius * Math.Cos(angle))).Add(v.Multiply(radius * Math.Sin(angle)));
                GeoVector3 tangent = u.Multiply(-Math.Sin(angle)).Add(v.Multiply(Math.Cos(angle)));
                var segment = new GeoLine3(touch.Add(tangent.Multiply(-0.5 * radius)), touch.Add(tangent.Multiply(0.5 * radius)));

                Assert.True(circle.CollidesWith(segment, fine), $"radius {radius}, repetition {repetition}: circle against the segment");
                Assert.True(segment.CollidesWith(circle, fine), $"radius {radius}, repetition {repetition}: segment against the circle");
            }
        }

        [Theory]
        [InlineData(1E-9, 20.0005, true)]
        [InlineData(1E-9, 20.0012, false)]
        [InlineData(1E-6, 20.0012, false)]
        [InlineData(1E-4, 20.0005, true)]
        public void TwoCirclesNearlyOnOneAxis_CollideWhereTheirGapIsWithinTheTolerance(double tilt, double radius, bool expected)
        {
            // Circles of radius 20 and `radius` about one centre, the second turned `tilt` radians off the first's plane: the
            // distance between them stays near-constant all the way round, which is the pair a proof by halving settles slowest.
            var first = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 20);
            var second = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(tilt, 0, 1), radius);

            Assert.Equal(expected, first.CollidesWith(second, Tol));
            Assert.Equal(expected, second.CollidesWith(first, Tol));
        }

        [Theory]
        [InlineData(1E-9, 0.0, 20.0012, false)]
        [InlineData(1E-9, 0.0, 20.0009, true)]
        [InlineData(1E-9, 1E-6, 20.0012, false)]
        [InlineData(1E-7, 1E-6, 20.0009, true)]
        public void TwoPartialArcsNearlyOnOneAxis_CollideWhereTheirGapIsWithinTheTolerance(double tilt, double off, double radius, bool expected)
        {
            // Three quarters of a circle of radius 20 and a hair off the same axis, wider by `radius` - 20 and overlapping it in angle.
            var first = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 20, 0.2, 5.0);
            var second = new GeoArc3(new GeoPoint3(off, 0, 0), new GeoVector3(tilt, 0, 1), radius, 0.5, 4.0);

            Assert.Equal(expected, first.CollidesWith(second, Tol));
            Assert.Equal(expected, second.CollidesWith(first, Tol));
        }

        [Fact]
        public void TwoLinkedCircles_DoNotCollide()
        {
            // The second passes through the disc of the first and round its rim without touching it.
            var first = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 10);
            var second = new GeoCircle3(new GeoPoint3(10, 0, 0), GeoVector3.YAxis, 3);

            Assert.False(first.CollidesWith(second, Tol));
            Assert.False(second.CollidesWith(first, Tol));
        }

        [Fact]
        public void ASegmentNoLongerThanTheToleranceIsReadAsItsStart_UnlessItTrulyMeetsTheArc()
        {
            GeoArc3 dome = Dome();

            // Starting 0.0013 off the arc's end and running 0.0005 toward it, it comes within 0.0008: read as its start it does not collide.
            var towards = new GeoLine3(new GeoPoint3(50.0013, 0, 0), new GeoPoint3(50.0008, 0, 0));
            Assert.False(dome.CollidesWith(towards, Tol));

            // The same segment starting at the near end is 0.0008 off.
            var away = new GeoLine3(new GeoPoint3(50.0008, 0, 0), new GeoPoint3(50.0013, 0, 0));
            Assert.True(dome.CollidesWith(away, Tol));

            // One that lies across the arc is nought from it however short.
            var across = new GeoLine3(new GeoPoint3(0, 50.0004, 0), new GeoPoint3(0, 49.9996, 0));
            Assert.True(dome.CollidesWith(across, Tol));
        }
    }
}
