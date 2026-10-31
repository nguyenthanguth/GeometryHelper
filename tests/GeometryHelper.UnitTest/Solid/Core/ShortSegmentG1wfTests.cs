using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment you give is measured against the tolerance you ask for, not the global one, and one shorter than the vector
    /// tolerance meets a shape where it truly crosses it; a straight side of a curved chain or loop, and a side of a polygon
    /// a polyline is joined to, is the side it is however short.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The segment from (x, y, 0.0004) to (x, y, -0.0005), 0.0009 long, is shorter than the global tolerance of a thousandth
    /// and longer than the billionth it is asked at: it crosses the floor at (x, y, 0), where it was read as its start. The
    /// stub from (x, y, 0.000004) to (x, y, -0.000004), 0.000008 long, is shorter than the vector tolerance: it crosses where
    /// it passes, where it was given its start.
    /// </para>
    /// <para>
    /// The chains and loops are asked at a hundredth, their short side longer than the default tolerance they are built
    /// with. The chain from (-7, -7, 0) to the origin and on to (0.008, 0, 0) ends 0.009 short of the shapes standing on
    /// x = 0.017, its short side's start 0.017 off: it touches them, where it was read as touching nothing. No test sets the
    /// global tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1wfTests
    {
        private static readonly Tolerance Billionth = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static readonly Tolerance TenMillionth = new Tolerance(1E-7, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-7);

        private static readonly Tolerance Thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);

        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static GeoLine3 Through(double x, double y) => new GeoLine3(new GeoPoint3(x, y, 0.0004), new GeoPoint3(x, y, -0.0005));

        private static GeoLine3 Tiny(double x, double y) => new GeoLine3(new GeoPoint3(x, y, 0.000004), new GeoPoint3(x, y, -0.000004));

        private static GeoPoint3[] Corners()
            => new[] { new GeoPoint3(-7, -7, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0.008, 0, 0), new GeoPoint3(7, -7, 0) };

        private static void AssertAt(double x, double y, double z, GeoPoint3 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
            Assert.Equal(z, actual.Z, 12);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_CrossesABoxAtTheToleranceAsked_WhereItPasses()
        {
            GeoSolid3 box = new GeoObb3(new GeoPoint3(5, 5, -5), 10, 10, 10).ToSolid();

            AssertAt(5, 5, 0, Assert.Single(box.GetIntersections(Through(5, 5), Billionth)));
            AssertAt(5, 5, 0, Assert.Single(Through(5, 5).GetIntersections(box, Billionth)));
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_CrossesARayAtTheToleranceAsked_WhereItPasses()
        {
            var ray = new GeoRay3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0));

            AssertAt(5, 0, 0, Assert.Single(ray.GetIntersections(Through(5, 0), Billionth)));
            Assert.Equal(0.0, ray.GetShortestLineTo(Through(5, 0), Billionth).Length, 12);
            Assert.Equal(0.0, Through(5, 0).GetShortestLineTo(ray, Billionth).Length, 12);
        }

        [Fact]
        public void AStubShorterThanTheVectorTolerance_CrossingASegment_MeetsItWhereItCrosses()
        {
            var segment = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            Assert.True(Tiny(5, 0).TryIntersectWith(segment, out GeoPoint3 crossing, Thousandth));
            AssertAt(5, 0, 0, crossing);
            Assert.True(Tiny(5, 0).TryIntersectWith(segment, out crossing, TenMillionth));
            AssertAt(5, 0, 0, crossing);
            AssertAt(5, 0, 0, Assert.Single(segment.GetIntersections(Tiny(5, 0), TenMillionth)));
        }

        [Fact]
        public void AStubShorterThanTheVectorTolerance_CrossingTheRimOfAnEllipse_MeetsItWhereItCrosses()
        {
            var ellipse = new GeoEllipse3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 6, 4);

            AssertAt(6, 0, 0, Assert.Single(ellipse.GetIntersections(Tiny(6, 0), Thousandth)));
            AssertAt(6, 0, 0, Assert.Single(ellipse.GetIntersections(Tiny(6, 0), TenMillionth)));
        }

        [Fact]
        public void AChainEndingWithinTheToleranceOfAShape_OnItsShortLastSide_TouchesIt()
        {
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(-7, -7, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0.008, 0, 0) });
            var square = new GeoPolygon3(
                new GeoPoint3(0.017, -5, -5), new GeoPoint3(0.017, 5, -5), new GeoPoint3(0.017, 5, 5), new GeoPoint3(0.017, -5, 5));
            var box = new GeoObb3(new GeoPoint3(5.017, 0, 0), 10, 10, 10);

            Assert.True(chain.CollidesWith(new GeoTriangle3(new GeoPoint3(0.017, -5, -5), new GeoPoint3(0.017, 5, -5), new GeoPoint3(0.017, 0, 5)), Hundredth));
            Assert.True(chain.CollidesWith(square, Hundredth));
            Assert.True(chain.CollidesWith(new GeoFace3(square), Hundredth));
            Assert.True(chain.CollidesWith(box, Hundredth));
            Assert.True(chain.CollidesWith(box.ToSolid(), Hundredth));
            Assert.True(chain.CollidesWith(new GeoLine3(new GeoPoint3(0.017, 0, -5), new GeoPoint3(0.017, 0, 5)), Hundredth));
            Assert.True(chain.CollidesWith(new GeoRay3(new GeoPoint3(0.017, 0, -5), new GeoVector3(0, 0, 1)), Hundredth));
            Assert.True(chain.CollidesWith(GeoArc3.FromThreePoints(new GeoPoint3(3.017, 0, -4), new GeoPoint3(0.017, 0, 0), new GeoPoint3(3.017, 0, 4), Hundredth), Hundredth));
            Assert.True(chain.CollidesWith(new GeoCircle3(new GeoPoint3(5.017, 0, 0), new GeoVector3(0, 1, 0), 5), Hundredth));
            Assert.True(chain.CollidesWith(new GeoPolylineArc3(new[] { new GeoPoint3(0.017, 0, -5), new GeoPoint3(0.017, 0, 5), new GeoPoint3(3, 3, 5) }), Hundredth));
        }

        [Fact]
        public void ALoopPassedOverTheMiddleOfItsShortSide_WithinTheTolerance_TouchesIt()
        {
            var loop = new GeoPolygonArc3(Corners());
            var chain = new GeoPolylineArc3(Corners());

            // 0.0095 over the middle of the short side, and 0.0103 from either of its ends.
            var ray = new GeoRay3(new GeoPoint3(0.004, 0.0095, -5), new GeoVector3(0, 0, 1));

            Assert.True(loop.CollidesWith(ray, Hundredth));
            Assert.True(chain.CollidesWith(ray, Hundredth));
        }

        [Fact]
        public void TheClosestPointOfAChainOrALoop_BesideItsShortSide_IsOnTheSideAsItIs()
        {
            var point = new GeoPoint3(0.006, 0.02, 0);
            var chain = new GeoPolylineArc3(Corners());
            var loop = new GeoPolygonArc3(Corners());

            AssertAt(0.006, 0, 0, chain.GetClosestPointOnBoundary(point, Hundredth));
            AssertAt(0.006, 0, 0, loop.GetClosestPointOnBoundary(point, Hundredth));
            Assert.Equal(0.02, chain.GetShortestLineTo(point, Hundredth).Length, 12);
            Assert.Equal(0.02, loop.GetShortestLineTo(point, Hundredth).Length, 12);
        }

        [Fact]
        public void APolylineBesideTheShortSideOfAPolygon_IsJoinedToTheSideAsItIs()
        {
            var polygon = new GeoPolygon3(Corners());
            var polyline = new GeoPolyline3(new GeoPoint3(0.006, 0.02, -5), new GeoPoint3(0.006, 0.02, 5), new GeoPoint3(3, 3, 5));

            Assert.Equal(0.02, polyline.GetShortestLineTo(polygon, Hundredth).Length, 12);
            Assert.Equal(0.02, polygon.GetShortestLineTo(polyline, Hundredth).Length, 12);
            Assert.Equal(polyline.DistanceTo(polygon, Hundredth), polyline.GetShortestLineTo(polygon, Hundredth).Length, 12);
        }
    }
}
