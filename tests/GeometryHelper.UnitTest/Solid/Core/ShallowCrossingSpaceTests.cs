using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Shapes crossing at a shallow angle in space cross where they cross.
    /// </summary>
    /// <remarks>
    /// Every crossing test here refused as parallel what met at under the angle tolerance, a whole degree,
    /// however large the shapes: two members, a member and a plane, an arc and a plane, a face and the plane
    /// cutting it. The last one reached the booleans — a corner of one box poking five hundredths into
    /// another through a face turned under a degree from it was never cut off, and the union counted it
    /// twice. The ray cast behind Locate lost a crossing the same way, so a point inside a long member
    /// lying under a degree off one of the cast directions was reported outside it.
    /// </remarks>
    public class ShallowCrossingSpaceTests
    {
        private const double HalfDegree = Math.PI / 360.0;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoLine3 Member(double length, double angle, double z)
        {
            var half = new GeoVector3(Math.Cos(angle) * length / 2, Math.Sin(angle) * length / 2, 0);

            return new GeoLine3(new GeoPoint3(-half.X, -half.Y, z), new GeoPoint3(half.X, half.Y, z));
        }

        [Theory]
        [InlineData(0.9)]
        [InlineData(0.5)]
        [InlineData(0.05)]
        public void TenMetreMembersCrossWhereverTheyCross(double angleDegrees)
        {
            GeoTransform3 anywhere = GeoTransform3.RotationAxis(new GeoPoint3(100, 200, 300), new GeoVector3(1, 2, 3), 0.7);
            GeoLine3 first = Member(10000, 0, 0).TransformBy(anywhere);
            GeoLine3 second = Member(10000, angleDegrees * Math.PI / 180.0, 0).TransformBy(anywhere);

            GeoPoint3? crossing = first.GetIntersection(second);

            Assert.True(crossing.HasValue);
            Assert.True(crossing.Value.DistanceTo(anywhere.Transform(GeoPoint3.Origin)) < 1E-6);
            Assert.True(first.CollidesWith(second));
        }

        [Fact]
        public void NearlyParallelMembersOneAboveTheOtherDoNotCross()
        {
            GeoLine3 first = Member(10000, 0, 0);
            GeoLine3 above = Member(10000, HalfDegree, 1);

            Assert.False(first.TryIntersectWith(above, out _));
        }

        [Fact]
        public void AMemberCrossingAPlaneAtHalfADegreeCrossesIt()
        {
            var ground = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);
            var sloping = new GeoLine3(
                new GeoPoint3(-5000 * Math.Cos(HalfDegree), 0, -5000 * Math.Sin(HalfDegree)),
                new GeoPoint3(5000 * Math.Cos(HalfDegree), 0, 5000 * Math.Sin(HalfDegree)));

            Assert.True(sloping.TryIntersectWith(ground, out GeoPoint3 crossing));
            Assert.True(crossing.DistanceTo(GeoPoint3.Origin) < 1E-6);

            // Climbing less than the tolerance over its whole length, it lies in the plane: no single point.
            var level = new GeoLine3(new GeoPoint3(-5000, 0, 0), new GeoPoint3(5000, 0, 5E-5));
            Assert.False(level.TryIntersectWith(ground, out _));
        }

        [Fact]
        public void ARayComingDownAtHalfADegreeReachesThePlane()
        {
            var ground = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);
            var ray = new GeoRay3(new GeoPoint3(0, 0, 10), new GeoVector3(Math.Cos(HalfDegree), 0, -Math.Sin(HalfDegree)));

            Assert.True(ray.TryIntersectWith(ground, out GeoPoint3 crossing));
            Assert.Equal(10.0 / Math.Tan(HalfDegree), crossing.X, 6);
            Assert.Equal(0.0, crossing.Z, 9);

            // Starting in the plane and running along it, the ray meets it over a stretch, not at a point.
            var along = new GeoRay3(GeoPoint3.Origin, new GeoVector3(Math.Cos(HalfDegree), 0, Math.Sin(HalfDegree)));
            Assert.False(along.TryIntersectWith(ground, out _));
        }

        [Fact]
        public void AnArcTiltedHalfADegreeCrossesThePlaneTwice()
        {
            GeoPoint3 At(double degrees)
            {
                double t = degrees * Math.PI / 180.0;
                return new GeoPoint3(1000 * Math.Cos(t), 1000 * Math.Sin(t) * Math.Cos(HalfDegree), 1000 * Math.Sin(t) * Math.Sin(HalfDegree));
            }

            GeoArc3 arc = GeoArc3.FromThreePoints(At(-60), At(90), At(240));
            GeoPoint3[] crossings = arc.GetIntersections(new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis));

            Assert.Equal(2, crossings.Length);
            Assert.Contains(crossings, p => p.DistanceTo(new GeoPoint3(1000, 0, 0)) < 1E-6);
            Assert.Contains(crossings, p => p.DistanceTo(new GeoPoint3(-1000, 0, 0)) < 1E-6);
        }

        [Fact]
        public void APointInsideALongMemberLyingJustOffACastDirectionIsInside()
        {
            // A ten-metre member laid along the first direction Locate casts its ray in, then turned half a
            // degree off it: the ray from a point near one end leaves through a long face at half a degree.
            var diagonal = new GeoVector3(1, 1, 1).Normalize();
            GeoVector3 axis = GeoVector3.XAxis.CrossProduct(diagonal).Normalize();
            double onto = Math.Acos(GeoVector3.XAxis.DotProduct(diagonal));
            GeoTransform3 lay = GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, onto);
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, HalfDegree);

            GeoSolid3 member = Box(-5000, -50, -50, 5000, 50, 50).TransformBy(lay).TransformBy(turn);
            GeoPoint3 inside = turn.Transform(lay.Transform(new GeoPoint3(-4900, 10, 5)));

            Assert.Equal(PointLocation.Inside, member.Locate(inside));
        }

        [Fact]
        public void ACornerPokingThroughAFaceTurnedUnderADegreeIsCutOff()
        {
            // Box B leans on box A's face at x = 50 with one corner five hundredths through it, and three faces
            // of B meet A's plane at under a degree. The corner was never cut off, and the union counted it
            // twice.
            GeoSolid3 a = Box(30, 30, 10, 50, 60, 40);
            GeoSolid3 b = Box(50, 30, 30, 80, 40, 50).TransformBy(GeoTransform3.RotationAxis(
                new GeoPoint3(65, 35, 40),
                new GeoVector3(-0.16476136965898858, -0.79640185544099751, 0.12238379455748193),
                1.2022325912500884));

            Assert.True(a.TryIntersect(b, out GeoSolid3 shared));
            Assert.True(a.TryUnion(b, out GeoSolid3 union));
            Assert.True(b.TrySubtract(a, out GeoSolid3 rest));

            Assert.Equal(a.GetVolume() + b.GetVolume() - shared.GetVolume(), union.GetVolume(), 6);
            Assert.Equal(b.GetVolume() - shared.GetVolume(), rest.GetVolume(), 6);
        }

        [Fact]
        public void RandomPairsOfBoxesKeepTheirVolumes()
        {
            // The sequence of InnerSheetTests: half the boxes turned about a random axis, corners on a grid of
            // ten. Pair 201 is the corner above.
            var rng = new Random(20260927);
            int Whole(int low, int high) => rng.Next(low, high + 1);
            double Real(double low, double high) => low + (high - low) * rng.NextDouble();

            GeoSolid3 Next()
            {
                int x0 = Whole(0, 6) * 10, y0 = Whole(0, 6) * 10, z0 = Whole(0, 6) * 10;
                int sx = Whole(1, 5) * 10, sy = Whole(1, 5) * 10, sz = Whole(1, 5) * 10;
                GeoSolid3 box = Box(x0, y0, z0, x0 + sx, y0 + sy, z0 + sz);

                if (rng.NextDouble() < 0.5)
                {
                    return box;
                }

                var centre = new GeoPoint3(x0 + sx / 2.0, y0 + sy / 2.0, z0 + sz / 2.0);
                var axis = new GeoVector3(Real(-1, 1), Real(-1, 1), Real(-1, 1) + 0.01);

                return box.TransformBy(GeoTransform3.RotationAxis(centre, axis, Real(0.05, 1.5)));
            }

            for (int k = 0; k < 250; k++)
            {
                GeoSolid3 a = Next();
                GeoSolid3 b = Next();
                double both = a.TryIntersect(b, out GeoSolid3 shared) ? shared.GetVolume() : 0.0;
                double slack = 1E-7 * (a.GetVolume() + b.GetVolume());

                Assert.True(a.TryUnion(b, out GeoSolid3 union), $"pair {k}");
                Assert.True(Math.Abs(union.GetVolume() + both - a.GetVolume() - b.GetVolume()) < slack, $"pair {k}: union");
                Assert.True(Math.Abs((a.TrySubtract(b, out GeoSolid3 ab) ? ab.GetVolume() : 0.0) - (a.GetVolume() - both)) < slack, $"pair {k}: a - b");
                Assert.True(Math.Abs((b.TrySubtract(a, out GeoSolid3 ba) ? ba.GetVolume() : 0.0) - (b.GetVolume() - both)) < slack, $"pair {k}: b - a");
                Assert.True(Math.Abs(a.Intersect(b).Sum(piece => piece.GetVolume()) - both) < slack, $"pair {k}: pieces");
            }
        }
    }
}
