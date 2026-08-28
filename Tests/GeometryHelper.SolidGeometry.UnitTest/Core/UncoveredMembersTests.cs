using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// The public members an audit found no test naming. Each is checked against something that reaches
    /// the same answer another way, rather than against a number written out by hand.
    /// </summary>
    public class UncoveredMembersTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        [Fact]
        public void ABoxHandsBackTheAxesAndExtentsItWasBuiltWith()
        {
            var box = new GeoObb3(new GeoPoint3(3, -4, 5), 10, 20, 30);

            // Index zero, one and two are X, Y and Z, and the extent is half the size.
            Assert.True(box.GetAxisAt(0).IsEqualTo(box.AxisX, Tol));
            Assert.True(box.GetAxisAt(1).IsEqualTo(box.AxisY, Tol));
            Assert.True(box.GetAxisAt(2).IsEqualTo(box.AxisZ, Tol));

            Assert.Equal(box.ExtentX, box.GetExtentAt(0), 9);
            Assert.Equal(box.ExtentY, box.GetExtentAt(1), 9);
            Assert.Equal(box.ExtentZ, box.GetExtentAt(2), 9);

            Assert.Equal(5.0, box.GetExtentAt(0), 9);
            Assert.Equal(10.0, box.GetExtentAt(1), 9);
            Assert.Equal(15.0, box.GetExtentAt(2), 9);

            // A turned box hands back the axes it was turned onto.
            var turned = new GeoObb3(GeoPoint3.Origin, 2, 2, 2,
                new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0));

            Assert.True(turned.GetAxisAt(0).IsUnitLength(Tol));
            Assert.True(turned.GetAxisAt(0).IsCodirectionalTo(new GeoVector3(1, 1, 0), Tol));
        }

        [Fact]
        public void AnIndexOutsideTheThreeAxesIsRefused()
        {
            var box = new GeoObb3(GeoPoint3.Origin, 1, 1, 1);

            Assert.Throws<ArgumentOutOfRangeException>(() => box.GetAxisAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => box.GetAxisAt(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => box.GetExtentAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => box.GetExtentAt(3));
        }

        [Fact]
        public void ADiscHandsBackTheSamePointItsProjectionDoes()
        {
            var disc = new GeoCircle3(new GeoPoint3(1, 2, 3), GeoVector3.ZAxis, 5.0);

            foreach (GeoPoint3 probe in new[]
            {
                new GeoPoint3(20, 2, 3),      // out beyond the rim
                new GeoPoint3(1, 2, 9),       // straight above the centre
                new GeoPoint3(2, 3, 7),       // above, but off centre
                new GeoPoint3(1, 2, 3)        // the centre itself
            })
            {
                GeoPoint3 onSurface = disc.GetClosestPointOnSurface(probe);

                // It is the filled disc, so a point above the middle comes straight down rather than out
                // to the rim. That is what Projection3.ProjectToDisc means, and the two must agree.
                Assert.True(onSurface.IsEqualTo(Projection3.ProjectToDisc(disc, probe), Tol));

                // And it is really the nearest point on the disc.
                Assert.Equal(Distance3.DistanceTo(disc, probe), onSurface.DistanceTo(probe), 9);

                // It lies on the plane of the disc and no further out than its rim.
                Assert.True(Math.Abs(disc.GetPlane().SignedDistanceTo(onSurface)) <= Tol.EqualPlanar);
                Assert.True(onSurface.DistanceTo(disc.Center) <= disc.Radius + Tol.EqualPoint);
            }
        }

        [Fact]
        public void ASegmentSpansAPlaneWithAnyDirectionNotAlongIt()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            GeoPlane3 plane = line.GetPlaneWith(GeoVector3.YAxis);

            // Both ends of the segment lie on the plane it spans, and so does a point along the
            // direction it was given.
            Assert.True(Math.Abs(plane.SignedDistanceTo(line.StartPoint)) <= Tol.EqualPlanar);
            Assert.True(Math.Abs(plane.SignedDistanceTo(line.EndPoint)) <= Tol.EqualPlanar);
            Assert.True(Math.Abs(plane.SignedDistanceTo(new GeoPoint3(5, 7, 0))) <= Tol.EqualPlanar);

            // The normal is square to both the segment and the direction.
            Assert.True(plane.Normal.IsPerpendicularTo(line.Direction, Tol));
            Assert.True(plane.Normal.IsPerpendicularTo(GeoVector3.YAxis, Tol));

            // A direction along the segment spans no plane at all: the cross product is the zero vector
            // and there is no normal to build one from.
            ArgumentException refused = Assert.Throws<ArgumentException>(() => line.GetPlaneWith(GeoVector3.XAxis));
            Assert.Contains("non-zero length", refused.Message);
        }

        [Fact]
        public void TheSideOfAPlaneAgreesWithTheSignOfTheDistance()
        {
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 4), GeoVector3.ZAxis);

            Assert.Equal(PlaneSide.Above, Containment3.GetSide(plane, new GeoPoint3(1, 1, 9), Tol));
            Assert.Equal(PlaneSide.Below, Containment3.GetSide(plane, new GeoPoint3(1, 1, 0), Tol));
            Assert.Equal(PlaneSide.On, Containment3.GetSide(plane, new GeoPoint3(1, 1, 4), Tol));

            // Within the planar tolerance still counts as on it.
            Assert.Equal(PlaneSide.On,
                         Containment3.GetSide(plane, new GeoPoint3(1, 1, 4 + Tolerance.DefaultEqualPlanar / 2.0), Tol));

            Random rng = new Random(2468);

            for (int t = 0; t < 200; t++)
            {
                var probe = new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));

                PlaneSide side = Containment3.GetSide(plane, probe, Tol);
                double signed = plane.SignedDistanceTo(probe);

                // The side and the sign of the distance are the same fact told two ways.
                if (side == PlaneSide.Above) { Assert.True(signed > Tol.EqualPlanar); }
                else if (side == PlaneSide.Below) { Assert.True(signed < -Tol.EqualPlanar); }
                else { Assert.True(Math.Abs(signed) <= Tol.EqualPlanar); }
            }
        }

        [Fact]
        public void CodirectionalIsParallelWithTheSameSense()
        {
            var along = new GeoVector3(2, 0, 0);
            var same = new GeoVector3(7, 0, 0);
            var against = new GeoVector3(-3, 0, 0);
            var across = new GeoVector3(0, 5, 0);

            Assert.True(Parallel3.IsCodirectional(along, same, Tol));
            Assert.False(Parallel3.IsCodirectional(along, against, Tol));
            Assert.False(Parallel3.IsCodirectional(along, across, Tol));

            // Anti-parallel is still parallel, which is the whole difference between the two questions.
            Assert.True(Parallel3.IsParallel(along, against, Tol));

            // The static form and the instance form must answer alike.
            Assert.Equal(Parallel3.IsCodirectional(along, same, Tol), along.IsCodirectionalTo(same, Tol));
            Assert.Equal(Parallel3.IsCodirectional(along, against, Tol), along.IsCodirectionalTo(against, Tol));

            // A zero-length vector points nowhere, so it is codirectional with nothing.
            Assert.False(Parallel3.IsCodirectional(along, GeoVector3.Zero, Tol));
            Assert.False(Parallel3.IsCodirectional(GeoVector3.Zero, GeoVector3.Zero, Tol));
        }
    }
}
