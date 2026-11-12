using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A box flat on some axis, or no thicker than the point tolerance on two, is crossed by a triangle, a polygon, a face
    /// or a plane where its rectangle, its segment or its point is, where it was crossed nowhere; a boolean with one is
    /// refused, where it ran on a sliver body; a ray from a corner of a flat box no longer throws; and a point that is not
    /// a number is outside every box, where it was inside. A box with its body answers as before.
    /// </summary>
    public class CrossingsAndBooleansOfFlatAndThinBoxTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        /// <summary>A box 100 by 100 by 0 about (50, 50, 0): the rectangle (0, 0, 0) to (100, 100, 0).</summary>
        private static GeoObb3 Sheet() => new GeoObb3(new GeoPoint3(50, 50, 0), 100, 100, 0);

        private static GeoAabb3 SheetAabb() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 0));

        /// <summary>A box 100 by 0.000002 by 0.000002 about (50, 0, 0): the segment (0, 0, 0) to (100, 0, 0).</summary>
        private static GeoObb3 Rod() => new GeoObb3(new GeoPoint3(50, 0, 0), 100, 2E-6, 2E-6);

        private static GeoAabb3 RodAabb() =>
            new GeoAabb3(new GeoPoint3(0, -1E-6, -1E-6), new GeoPoint3(100, 1E-6, 1E-6));

        /// <summary>A box 0.000002 on every side at (50, 0, 0): the point.</summary>
        private static GeoObb3 Speck() => new GeoObb3(new GeoPoint3(50, 0, 0), 2E-6, 2E-6, 2E-6);

        /// <summary>A cube 40 on a side about the middle of the rod, which runs through it.</summary>
        private static GeoObb3 CubeAround() => new GeoObb3(new GeoPoint3(50, 0, 0), 40, 40, 40);

        private static readonly GeoPlane3 PlaneAt30 = new GeoPlane3(new GeoPoint3(30, 0, 0), new GeoVector3(1, 0, 0));

        /// <summary>A triangle standing upright in x = 30, across the sheet and the rod.</summary>
        private static readonly GeoTriangle3 TriangleAt30 = new GeoTriangle3(
            new GeoPoint3(30, -50, -50),
            new GeoPoint3(30, 300, -50),
            new GeoPoint3(30, -50, 300)
        );

        /// <summary>The square 120 by 20 standing upright in x = 30, across the sheet and the rod.</summary>
        private static readonly GeoPolygon3 SquareAt30 = new GeoPolygon3(
            new GeoPoint3(30, -10, -10),
            new GeoPoint3(30, 110, -10),
            new GeoPoint3(30, 110, 10),
            new GeoPoint3(30, -10, 10)
        );

        /// <summary>The same square with a hole 20 by 10 about (30, 50, 0) and one 4 by 4 about the rod.</summary>
        private static GeoFace3 RingAt30() =>
            new GeoFace3(
                SquareAt30,
                new[]
                {
                    new GeoPolygon3(
                        new GeoPoint3(30, 40, -5),
                        new GeoPoint3(30, 40, 5),
                        new GeoPoint3(30, 60, 5),
                        new GeoPoint3(30, 60, -5)
                    ),
                    new GeoPolygon3(
                        new GeoPoint3(30, -2, -2),
                        new GeoPoint3(30, -2, 2),
                        new GeoPoint3(30, 2, 2),
                        new GeoPoint3(30, 2, -2)
                    ),
                },
                Tol
            );

        private static void AssertPoints(GeoPoint3[] actual, params GeoPoint3[] expected)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(actual[i].IsEqualTo(expected[i], Tol), $"{actual[i]} is not {expected[i]}");
            }
        }

        [Fact]
        public void ThePlaneXIs30AndASheet100By100_CrossAtTheEndsOfTheirSegment_OnItsEdges()
        {
            AssertPoints(PlaneAt30.GetIntersections(Sheet(), Tol), new GeoPoint3(30, 100, 0), new GeoPoint3(30, 0, 0));
            AssertPoints(
                PlaneAt30.GetIntersections(SheetAabb(), Tol),
                new GeoPoint3(30, 100, 0),
                new GeoPoint3(30, 0, 0)
            );
            Assert.True(PlaneAt30.TryIntersectWith(Sheet(), out GeoPoint3[] points, Tol));
            Assert.Equal(2, points.Length);
            Assert.True(PlaneAt30.TryIntersectWith(SheetAabb(), out points, Tol));
            Assert.Equal(2, points.Length);

            // The floor, in which the sheet lies, crosses it nowhere, as it crosses a polygon lying in it.
            var floor = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));
            Assert.Empty(floor.GetIntersections(Sheet(), Tol));
            Assert.False(floor.TryIntersectWith(Sheet(), out points, Tol));
            Assert.Empty(points);
        }

        [Fact]
        public void ATriangleASquareAndAFace_CrossASheet_WhereTheyCrossItsRectangle()
        {
            GeoPolygon3 rectangle = Sheet().GetFacesWithArea()[0];

            AssertPoints(
                TriangleAt30.GetIntersections(Sheet(), Tol),
                new GeoPoint3(30, 100, 0),
                new GeoPoint3(30, 0, 0)
            );
            Assert.Equal(TriangleAt30.GetIntersections(rectangle, Tol), TriangleAt30.GetIntersections(Sheet(), Tol));
            AssertPoints(SquareAt30.GetIntersections(Sheet(), Tol), new GeoPoint3(30, 100, 0), new GeoPoint3(30, 0, 0));
            AssertPoints(
                SquareAt30.GetIntersections(SheetAabb(), Tol),
                new GeoPoint3(30, 100, 0),
                new GeoPoint3(30, 0, 0)
            );

            // The ring's holes cross the sheet where their rims pass through it, then the sheet's edges cross the ring.
            AssertPoints(
                RingAt30().GetIntersections(Sheet(), Tol),
                new GeoPoint3(30, 60, 0),
                new GeoPoint3(30, 40, 0),
                new GeoPoint3(30, 2, 0),
                new GeoPoint3(30, 100, 0)
            );
            Assert.True(RingAt30().TryIntersectWith(SheetAabb(), out GeoPoint3[] points, Tol));
            Assert.Equal(4, points.Length);
            Assert.True(TriangleAt30.TryIntersectWith(SheetAabb(), out points, Tol));
            Assert.Equal(2, points.Length);
        }

        [Fact]
        public void ARodThinnerThanTheToleranceOnTwoAxes_IsCrossedWhereItsSegmentIs()
        {
            foreach (GeoObb3 box in new[] { Rod(), RodAabb().ToObb() })
            {
                AssertPoints(PlaneAt30.GetIntersections(box, Tol), new GeoPoint3(30, 0, 0));
                AssertPoints(TriangleAt30.GetIntersections(box, Tol), new GeoPoint3(30, 0, 0));
                AssertPoints(SquareAt30.GetIntersections(box, Tol), new GeoPoint3(30, 0, 0));
                AssertPoints(new GeoFace3(SquareAt30).GetIntersections(box, Tol), new GeoPoint3(30, 0, 0));
                Assert.True(SquareAt30.TryIntersectWith(box, out GeoPoint3[] points, Tol));
                Assert.Single(points);

                // A face with a hole the rod passes through crosses it nowhere.
                Assert.Empty(RingAt30().GetIntersections(box, Tol));
                Assert.False(RingAt30().TryIntersectWith(box, out points, Tol));
            }

            AssertPoints(PlaneAt30.GetIntersections(RodAabb(), Tol), new GeoPoint3(30, 0, 0));
            AssertPoints(TriangleAt30.GetIntersections(RodAabb(), Tol), new GeoPoint3(30, 0, 0));
        }

        [Fact]
        public void ASpeckThinnerThanTheToleranceOnEveryAxis_IsCrossedAtItsCentre_WhereAPlaneMeetsIt()
        {
            var through = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));

            AssertPoints(through.GetIntersections(Speck(), Tol), new GeoPoint3(50, 0, 0));
            Assert.Empty(PlaneAt30.GetIntersections(Speck(), Tol));
            Assert.False(PlaneAt30.TryIntersectWith(Speck(), out GeoPoint3[] points, Tol));
            Assert.Empty(points);
        }

        [Fact]
        public void ABoxWithItsBody_IsCrossedAsItsBody()
        {
            var cube = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 100, 100);
            var plate = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 100, 2E-6);
            var bar = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 0.0015, 0.0015);

            foreach (GeoObb3 box in new[] { cube, plate, bar })
            {
                GeoSolid3 body = box.ToSolid();

                Assert.Equal(PlaneAt30.GetIntersections(body, Tol), PlaneAt30.GetIntersections(box, Tol));
                Assert.Equal(TriangleAt30.GetIntersections(body, Tol), TriangleAt30.GetIntersections(box, Tol));
                Assert.Equal(SquareAt30.GetIntersections(body, Tol), SquareAt30.GetIntersections(box, Tol));
                Assert.Equal(RingAt30().GetIntersections(body, Tol), RingAt30().GetIntersections(box, Tol));
                Assert.NotEmpty(PlaneAt30.GetIntersections(box, Tol));
            }
        }

        [Fact]
        public void ABooleanWithABoxThinnerThanTheToleranceOnTwoAxes_IsFalse_WithNoResult()
        {
            // The rod joined to the cube it runs through was given the cube alone, 64 000, its two ends lost.
            var wire = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 0.0005, 0.001);
            GeoSolid3 body = CubeAround().ToSolid();

            foreach (GeoObb3 box in new[] { Rod(), Speck(), wire })
            {
                Assert.False(box.TryUnion(CubeAround(), out GeoSolid3 result, Tol));
                Assert.Null(result);
                Assert.False(box.TryIntersect(CubeAround(), out result, Tol));
                Assert.Null(result);
                Assert.False(box.TrySubtract(CubeAround(), out result, Tol));
                Assert.Null(result);
                Assert.False(CubeAround().TryUnion(box, out result, Tol));
                Assert.Null(result);
                Assert.False(CubeAround().TrySubtract(box, out result, Tol));
                Assert.Null(result);
                Assert.False(box.TryUnion(body, out result, Tol));
                Assert.Null(result);
                Assert.False(body.TryUnion(box, out result, Tol));
                Assert.Null(result);
                Assert.False(body.TryIntersect(box, out result, Tol));
                Assert.Null(result);
                Assert.False(body.TrySubtract(box, out result, Tol));
                Assert.Null(result);
            }

            // A box with its body is joined as before.
            var bar = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 2, 2);
            Assert.True(bar.TryUnion(CubeAround(), out GeoSolid3 joined, Tol));
            Assert.Equal(64000.0 + 60 * 2 * 2, joined.GetVolume(Tol), 6);
        }

        [Fact]
        public void ARayFromACornerOfASheetRunningAwayFromIt_TouchesItAtItsOrigin_NotAThrow()
        {
            // It threw "A box with a zero size on some axis has no faces to build" from the test for lying along a face.
            var origin = new GeoPoint3(99.999903535244712, 99.999710837938, -0.00042464204594709073);
            var ray = new GeoRay3(
                origin,
                new GeoVector3(0.25127354252677109, 0.15559993807952849, -0.043676271356491547)
            );

            AssertPoints(ray.GetIntersections(Sheet(), Tol), origin);
            AssertPoints(ray.GetIntersections(SheetAabb(), Tol), origin);
        }

        [Fact]
        public void APointThatIsNotANumber_IsOutsideABox_AsItIsOutsideItsBody()
        {
            var cube = new GeoObb3(new GeoPoint3(0, 0, 0), 10, 10, 10);
            var nan = new GeoPoint3(double.NaN, 0, 0);

            Assert.Equal(PointLocation.OutSide, cube.ToSolid().Locate(nan, Tol));
            Assert.Equal(PointLocation.OutSide, cube.Locate(nan, Tol));
            Assert.False(cube.Contains(nan, Tol));
            Assert.Equal(PointLocation.OutSide, cube.GetAabb().Locate(nan, Tol));
            Assert.False(cube.GetAabb().Contains(nan, Tol));
            Assert.Equal(PointLocation.Inside, cube.Locate(new GeoPoint3(0, 0, 0), Tol));
            Assert.Equal(PointLocation.OnSide, cube.GetAabb().Locate(new GeoPoint3(5, 0, 0), Tol));
        }
    }
}
