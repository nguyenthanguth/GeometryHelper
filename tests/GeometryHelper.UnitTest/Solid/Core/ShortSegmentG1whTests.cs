using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A short leg of a polyline is the leg it is however short where the polyline is cut by a body, and a short side of a
    /// chain or a polyline where it passes through a triangle, a polygon or a face; and a segment you give longer than the
    /// tolerance asked is cut by a body where it crosses it however much shorter than the global tolerance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The box is 10 by 10 by 10 standing under the floor, its top at z = 0 from (0, 0) to (10, 10), and the square is that
    /// top. The polyline from (2, 5, 0.054) down to (5, 5, 0.004), through the top to (5, 5, -0.005) and on to (8, 5, -0.055)
    /// crosses the top by its leg 0.009 long alone, asked at a hundredth: its other legs fall too gently to reach it within
    /// the tolerance. The level chain from (2, 5, 0.004) to (5, 5, 0.004), down through the top to (5, 5, -0.005) and on to
    /// (8, 5, -0.005) lies level over the square but for that leg, which passes through it.
    /// </para>
    /// <para>
    /// The segment from (5, 5, 0.0004) to (5, 5, -0.0005) is 0.0009 long, shorter than the global thousandth and longer than
    /// the billionth it is asked at. No test sets the global tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1whTests
    {
        private static readonly Tolerance Billionth = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static readonly GeoPoint3 Through = new GeoPoint3(5, 5, 0);

        private static GeoSolid3 Box() => new GeoObb3(new GeoPoint3(5, 5, -5), 10, 10, 10).ToSolid();

        private static GeoPolygon3 Top() => new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

        private static GeoTriangle3 TopTriangle() => new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(0, 20, 0));

        private static GeoPoint3[] Stepping()
            => new[] { new GeoPoint3(2, 5, 0.054), new GeoPoint3(5, 5, 0.004), new GeoPoint3(5, 5, -0.005), new GeoPoint3(8, 5, -0.055) };

        private static GeoPoint3[] Level()
            => new[] { new GeoPoint3(2, 5, 0.004), new GeoPoint3(5, 5, 0.004), new GeoPoint3(5, 5, -0.005), new GeoPoint3(8, 5, -0.005) };

        private static void AssertAt(double x, double y, double z, GeoPoint3 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
            Assert.Equal(z, actual.Z, 12);
        }

        [Fact]
        public void APolylineCrossingTheTopOfABoxByItsShortLegAlone_IsCutThere()
        {
            // It was not cut: the leg 0.009 long, no longer than the tolerance, crossed no face.
            var polyline = new GeoPolyline3(Stepping());

            Assert.True(polyline.TrySplitBy(Box(), out GeoPolyline3[] inside, out GeoPolyline3[] outside, Hundredth));
            Assert.InRange(Assert.Single(inside).StartPoint.DistanceTo(Through), 0.0, 0.01);
            Assert.InRange(Assert.Single(outside).EndPoint.DistanceTo(Through), 0.0, 0.01);

            Assert.True(polyline.TrySplitBy(new[] { Box() }, out inside, out outside, Hundredth));
            Assert.InRange(Assert.Single(inside).StartPoint.DistanceTo(Through), 0.0, 0.01);
            Assert.InRange(Assert.Single(outside).EndPoint.DistanceTo(Through), 0.0, 0.01);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_IsCutByABoxAtTheToleranceAsked_WhereItPasses()
        {
            // It was not cut: the place it crosses the top was measured along it at the global thousandth, at its start.
            var segment = new GeoLine3(new GeoPoint3(5, 5, 0.0004), new GeoPoint3(5, 5, -0.0005));

            Assert.True(segment.TrySplitBy(Box(), out GeoLine3[] inside, out GeoLine3[] outside, Billionth));
            AssertAt(5, 5, 0, Assert.Single(inside).StartPoint);
            AssertAt(5, 5, 0, Assert.Single(outside).EndPoint);
        }

        [Fact]
        public void AChainOrAPolylineLyingLevelOverASquare_AndPassingThroughItByAShortLeg_MeetsItWhereTheLegPasses()
        {
            // The level legs touch the square along their length and name no place; the short leg passing through it named none.
            var chain = new GeoPolylineArc3(Level());
            var polyline = new GeoPolyline3(Level());

            AssertAt(5, 5, 0, Assert.Single(chain.GetIntersections(Top(), Hundredth)));
            AssertAt(5, 5, 0, Assert.Single(chain.GetIntersections(new GeoFace3(Top()), Hundredth)));
            AssertAt(5, 5, 0, Assert.Single(chain.GetIntersections(TopTriangle(), Hundredth)));
            AssertAt(5, 5, 0, Assert.Single(polyline.GetIntersections(Top(), Hundredth)));
            AssertAt(5, 5, 0, Assert.Single(polyline.GetIntersections(new GeoFace3(Top()), Hundredth)));
            AssertAt(5, 5, 0, Assert.Single(polyline.GetIntersections(TopTriangle(), Hundredth)));
        }

        [Fact]
        public void AChainOfOneShortLeg_PassingThroughASquare_MeetsItWhereItPasses()
        {
            // Given as a segment it meets the square there; as the one side of a chain it met nothing.
            var chain = new GeoPolylineArc3(new[] { new GeoEdge3(new GeoPoint3(5, 5, 0.004), new GeoPoint3(5, 5, -0.005)) });

            AssertAt(5, 5, 0, Assert.Single(chain.GetIntersections(Top(), Hundredth)));
            AssertAt(5, 5, 0, Assert.Single(chain.GetIntersections(new GeoFace3(Top()), Hundredth)));
            AssertAt(5, 5, 0, Assert.Single(chain.GetIntersections(TopTriangle(), Hundredth)));
        }
    }
}
