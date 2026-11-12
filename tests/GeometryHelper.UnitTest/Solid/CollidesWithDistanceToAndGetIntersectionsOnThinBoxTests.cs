using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A box no thicker than the point tolerance on two axes is read as the segment along the third, and on all three as
    /// its centre, as a box flat on those axes is read: its body, four slivers and two ends below the tolerance, was
    /// measured +Infinity from a cube 20 over it and missed a solid it ran through. A box thinner than the tolerance on one
    /// axis alone, or thicker on two, keeps its body.
    /// </summary>
    public class CollidesWithDistanceToAndGetIntersectionsOnThinBoxTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        /// <summary>A box 100 by 0.000002 by 0.000002 about (50, 0, 0): the segment (0, 0, 0) to (100, 0, 0).</summary>
        private static GeoObb3 Rod() => new GeoObb3(new GeoPoint3(50, 0, 0), 100, 2E-6, 2E-6);

        /// <summary>The same rod as an axis-aligned box.</summary>
        private static GeoAabb3 RodAabb() =>
            new GeoAabb3(new GeoPoint3(0, -1E-6, -1E-6), new GeoPoint3(100, 1E-6, 1E-6));

        /// <summary>A box 100 by 0.0005 by 0.001 about (50, 0, 0), turned a quarter about X: thin to the tolerance itself.</summary>
        private static GeoObb3 Wire() =>
            new GeoObb3(new GeoPoint3(50, 0, 0), 100, 0.0005, 0.001, new GeoVector3(1, 0, 0), new GeoVector3(0, 0, 1));

        /// <summary>A box 0.000002 on every side at (50, 0, 0): the point.</summary>
        private static GeoObb3 Speck() => new GeoObb3(new GeoPoint3(50, 0, 0), 2E-6, 2E-6, 2E-6);

        /// <summary>A cube 40 on a side standing 20 over the rod.</summary>
        private static GeoObb3 CubeAbove() => new GeoObb3(new GeoPoint3(50, 0, 40), 40, 40, 40);

        /// <summary>A cube 40 on a side about the middle of the rod, which runs through it.</summary>
        private static GeoObb3 CubeAround() => new GeoObb3(new GeoPoint3(50, 0, 0), 40, 40, 40);

        /// <summary>A triangle lying 20 over the rod.</summary>
        private static readonly GeoTriangle3 TriangleAbove = new GeoTriangle3(
            new GeoPoint3(10, -10, 20),
            new GeoPoint3(90, -10, 20),
            new GeoPoint3(10, 30, 20)
        );

        /// <summary>The square 120 by 20 lying 20 over the rod.</summary>
        private static readonly GeoPolygon3 SquareAbove = new GeoPolygon3(
            new GeoPoint3(-10, -10, 20),
            new GeoPoint3(110, -10, 20),
            new GeoPoint3(110, 10, 20),
            new GeoPoint3(-10, 10, 20)
        );

        /// <summary>A square standing upright in x = 30, across the rod.</summary>
        private static readonly GeoPolygon3 SquareAcross = new GeoPolygon3(
            new GeoPoint3(30, -10, -10),
            new GeoPoint3(30, 10, -10),
            new GeoPoint3(30, 10, 10),
            new GeoPoint3(30, -10, 10)
        );

        private static readonly GeoPlane3 PlaneAbove = new GeoPlane3(new GeoPoint3(0, 0, 20), new GeoVector3(0, 0, 1));

        private static readonly GeoPlane3 PlaneAcross = new GeoPlane3(new GeoPoint3(30, 0, 0), new GeoVector3(1, 0, 0));

        [Fact]
        public void ABoxThinnerThanTheToleranceOnTwoAxes_IsMeasuredToItsSegment()
        {
            GeoSolid3 above = CubeAbove().ToSolid();

            foreach (GeoObb3 box in new[] { Rod(), RodAabb().ToObb(), Wire() })
            {
                Assert.Equal(20.0, above.DistanceTo(box, Tol), 9);
                Assert.Equal(20.0, box.DistanceTo(CubeAbove(), Tol), 9);
                Assert.Equal(20.0, CubeAbove().DistanceTo(box, Tol), 9);
                Assert.Equal(20.0, TriangleAbove.DistanceTo(box, Tol), 9);
                Assert.Equal(20.0, SquareAbove.DistanceTo(box, Tol), 9);
                Assert.Equal(20.0, new GeoFace3(SquareAbove).DistanceTo(box, Tol), 9);
                Assert.Equal(20.0, PlaneAbove.DistanceTo(box, Tol), 9);
            }

            Assert.Equal(20.0, above.DistanceTo(RodAabb(), Tol), 9);
        }

        [Fact]
        public void ABoxThinnerThanTheToleranceOnTwoAxes_TouchesWhatItsSegmentTouches()
        {
            GeoSolid3 around = CubeAround().ToSolid();

            foreach (GeoObb3 box in new[] { Rod(), RodAabb().ToObb(), Wire() })
            {
                Assert.True(box.CollidesWith(around, Tol));
                Assert.Equal(0.0, around.DistanceTo(box, Tol));
                Assert.True(SquareAcross.CollidesWith(box, Tol));
                Assert.True(new GeoFace3(SquareAcross).CollidesWith(box, Tol));
                Assert.True(PlaneAcross.CollidesWith(box, Tol));
                Assert.False(box.CollidesWith(CubeAbove().ToSolid(), Tol));
                Assert.False(TriangleAbove.CollidesWith(box, Tol));
            }

            Assert.True(RodAabb().CollidesWith(around, Tol));
        }

        [Fact]
        public void ABoxThinnerThanTheToleranceOnAllThreeAxes_IsMeasuredToItsPoint()
        {
            Assert.Equal(20.0, CubeAbove().ToSolid().DistanceTo(Speck(), Tol), 9);
            Assert.Equal(20.0, CubeAbove().DistanceTo(Speck(), Tol), 9);
            Assert.True(Speck().CollidesWith(CubeAround().ToSolid(), Tol));
            Assert.Equal(0.0, CubeAround().ToSolid().DistanceTo(Speck(), Tol));
        }

        [Fact]
        public void ABoxThinnerThanTheToleranceOnTwoAxes_IsCrossedWhereItsSegmentIs()
        {
            foreach (GeoObb3 box in new[] { Rod(), Wire() })
            {
                Assert.True(SquareAcross.GetIntersections(box, Tol)[0].IsEqualTo(new GeoPoint3(30, 0, 0), Tol));
                Assert.True(PlaneAcross.GetIntersections(box, Tol)[0].IsEqualTo(new GeoPoint3(30, 0, 0), Tol));
                Assert.True(SquareAcross.TryIntersectWith(box, out GeoPoint3[] points, Tol));
                Assert.Single(points);
            }

            // The speck at (50, 0, 0) is its centre, which the square and the plane in x = 30 miss.
            Assert.Empty(SquareAcross.GetIntersections(Speck(), Tol));
            Assert.Empty(PlaneAcross.GetIntersections(Speck(), Tol));
            Assert.Single(PlaneAcross.GetIntersections(RodAabb(), Tol));
        }

        [Fact]
        public void ABoxThinOnOneAxisOrThickerThanTheToleranceOnTwo_KeepsItsBody()
        {
            var plate = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 100, 2E-6);
            var bar = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 0.0015, 0.0015);

            foreach (GeoObb3 box in new[] { plate, bar })
            {
                Assert.NotEmpty(PlaneAcross.GetIntersections(box, Tol));
                Assert.True(box.CollidesWith(CubeAround().ToSolid(), Tol));
                Assert.Equal(20.0 - box.SizeZ / 2, CubeAbove().ToSolid().DistanceTo(box, Tol), 9);
            }
        }
    }
}
