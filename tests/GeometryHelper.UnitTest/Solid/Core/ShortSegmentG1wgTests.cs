using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A side of a shape is the side it is however short: the distance along a chain or loop to a point beside its short
    /// straight side, a side of a region shorter than the global tolerance where it crosses a body at a finer one, and a
    /// short leg of a chain, a loop or a polyline that alone crosses the surface of a body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The box is 10 by 10 by 10 standing under the floor, its top at z = 0 from (0, 0) to (10, 10). The chain from
    /// (-7, -7, 0) to the origin and on to (0.008, 0, 0) and (7, -7, 0) has a side 0.008 long, asked at a hundredth; the point
    /// (0.006, 0.02, 0) lies 0.02 from it, where the side's start is 0.0209 away and the next corner 0.0201.
    /// </para>
    /// <para>
    /// The triangle (5, 5, 0.0004), (5, 5, -0.0005), (45, 5, 10) has a side 0.0009 long standing through the top of the box,
    /// shorter than the global thousandth and longer than the billionth it is asked at. The chain from (2, 5, 0.054) down to
    /// (5, 5, 0.004), through the top to (5, 5, -0.005) and on to (8, 5, -0.055) crosses the top by its leg 0.009 long alone,
    /// asked at a hundredth: its other legs fall too gently to reach the top within the tolerance. No test sets the global
    /// tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1wgTests
    {
        private static readonly Tolerance Billionth = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static GeoSolid3 Box() => new GeoObb3(new GeoPoint3(5, 5, -5), 10, 10, 10).ToSolid();

        private static GeoPoint3[] Corners()
            => new[] { new GeoPoint3(-7, -7, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0.008, 0, 0), new GeoPoint3(7, -7, 0) };

        private static GeoPoint3[] Stepping()
            => new[] { new GeoPoint3(2, 5, 0.054), new GeoPoint3(5, 5, 0.004), new GeoPoint3(5, 5, -0.005), new GeoPoint3(8, 5, -0.055) };

        private static void AssertAt(double x, double y, double z, GeoPoint3 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
            Assert.Equal(z, actual.Z, 12);
        }

        [Fact]
        public void TheDistanceAlongAChainOrALoop_ToAPointBesideItsShortSide_IsToTheFootOnTheSide()
        {
            // Along the first side and 0.006 along the short one, where it was to the start of the side after it, 0.008 along.
            var point = new GeoPoint3(0.006, 0.02, 0);
            var chain = new GeoPolylineArc3(Corners());
            var loop = new GeoPolygonArc3(Corners());

            Assert.Equal(Math.Sqrt(98) + 0.006, chain.GetDistanceAtPoint(point, Hundredth), 12);
            Assert.Equal(Math.Sqrt(98) + 0.006, loop.GetDistanceAtPoint(point, Hundredth), 12);
            Assert.Equal((Math.Sqrt(98) + 0.006) / chain.Length, chain.GetParameterAtPoint(point, Hundredth), 12);
        }

        [Fact]
        public void ARegionWhoseSideIsShorterThanTheGlobalTolerance_CrossesABoxAtTheToleranceAsked_WhereTheSidePasses()
        {
            var a = new GeoPoint3(5, 5, 0.0004);
            var b = new GeoPoint3(5, 5, -0.0005);
            var c = new GeoPoint3(45, 5, 10);
            var polygon = new GeoPolygon3(new[] { a, b, c, new GeoPoint3(25, 5, 30) }, Billionth);

            // The short side crosses the top at (5, 5, 0), where it was given its start, 0.0004 over the top.
            AssertAt(5, 5, 0, Box().GetIntersections(new GeoTriangle3(a, b, c), Billionth)[0]);
            AssertAt(5, 5, 0, Box().GetIntersections(polygon, Billionth)[0]);
            AssertAt(5, 5, 0, Box().GetIntersections(new GeoFace3(polygon), Billionth)[0]);
        }

        [Fact]
        public void AChainOrAPolyline_CrossingTheTopOfABoxByItsShortLegAlone_CrossesItThere()
        {
            AssertAt(5, 5, 0, Assert.Single(new GeoPolylineArc3(Stepping()).GetIntersections(Box(), Hundredth)));
            AssertAt(5, 5, 0, Assert.Single(new GeoPolyline3(Stepping()).GetIntersections(Box(), Hundredth)));
        }

        [Fact]
        public void ALoop_CrossingTheTopOfABoxByItsShortSideAlone_CrossesItThereAndWhereItClimbsBackOut()
        {
            var loop = new GeoPolygonArc3(new[]
            {
                new GeoPoint3(2, 5, 0.054), new GeoPoint3(5, 5, 0.004), new GeoPoint3(5, 5, -0.005), new GeoPoint3(8, 5, -0.055), new GeoPoint3(8, 5, 3),
            });
            GeoPoint3[] crossings = loop.GetIntersections(Box(), Hundredth);

            Assert.Equal(2, crossings.Length);
            AssertAt(5, 5, 0, crossings[0]);
            AssertAt(8, 5, 0, crossings[1]);
        }

        [Fact]
        public void AChainCrossingTheTopOfABoxByItsShortLegAlone_IsCutThere()
        {
            Assert.True(new GeoPolylineArc3(Stepping()).TrySplitBy(Box(), out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside, Hundredth));
            Assert.InRange(Assert.Single(inside).StartPoint.DistanceTo(new GeoPoint3(5, 5, 0)), 0.0, 0.01);
            Assert.InRange(Assert.Single(outside).EndPoint.DistanceTo(new GeoPoint3(5, 5, 0)), 0.0, 0.01);
        }
    }
}
