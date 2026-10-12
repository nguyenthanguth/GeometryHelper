using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;
using static GeometryHelper.UnitTest.Plane.Ray2Shapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The two extras of the ray in the plane: laying a ray in space out in a frame and back (D14), and the crossings of
    /// a ray with the edges a hierarchy holds (D15).
    /// </summary>
    /// <remarks>
    /// The frame stands at (10, 20, 30) with its axes along world Y and Z, so its normal is world X and a point's local
    /// coordinates are its Y and Z less 20 and 30: every number below is read off by hand. The hierarchy's answers are
    /// held to two others: the shape's own crossings with the ray, and the hierarchy's crossings with the segment D9 cuts,
    /// from the origin out past the corners of the hierarchy's box by a reach of max(1, 100 times the point tolerance),
    /// worked out here from <see cref="GeoBvh2.Bounds"/> without the ray's code.
    /// </remarks>
    public class Ray2PlanarMapAndBvhTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoCoordinateSystem3 Upright() => new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), GeoVector3.YAxis, GeoVector3.ZAxis);

        private static void AssertNear(GeoPoint2 expected, GeoPoint2 actual, double within = 1E-12)
        {
            Assert.True(expected.DistanceTo(actual) <= within, actual + " is not " + expected);
        }

        private static void AssertNear(GeoPoint3 expected, GeoPoint3 actual, double within = 1E-12)
        {
            Assert.True(expected.DistanceTo(actual) <= within, actual + " is not " + expected);
        }

        private static void AssertNear(GeoVector2 expected, GeoVector2 actual, double within = 1E-12)
        {
            Assert.InRange(actual.X - expected.X, -within, within);
            Assert.InRange(actual.Y - expected.Y, -within, within);
        }

        private static void AssertNear(GeoVector3 expected, GeoVector3 actual, double within = 1E-12)
        {
            Assert.InRange(actual.X - expected.X, -within, within);
            Assert.InRange(actual.Y - expected.Y, -within, within);
            Assert.InRange(actual.Z - expected.Z, -within, within);
        }

        private static void AssertSamePoints(GeoPoint2[] expected, GeoPoint2[] actual, double within = 1E-9)
        {
            Assert.Equal(expected.Length, actual.Length);

            foreach (GeoPoint2 point in expected)
            {
                Assert.Contains(actual, p => p.DistanceTo(point) <= within);
            }
        }

        #region Laid out in a frame and back (D14)

        [Fact]
        public void ARayInSpace_IsLaidOutByItsOriginAsAPointAndItsDirectionAsAVector()
        {
            // From (15, 25, 37) along (1, 3, 4): the origin is (25 - 20, 37 - 30) = (5, 7) in the frame, and the direction
            // drops its part along X, leaving (3, 4), made unit length: (0.6, 0.8).
            var ray = new GeoRay3(new GeoPoint3(15, 25, 37), new GeoVector3(1, 3, 4));

            GeoRay2 flat = PlanarMap.ProjectToRay2(Upright(), ray);

            AssertNear(new GeoPoint2(5, 7), flat.Origin);
            AssertNear(new GeoVector2(0.6, 0.8), flat.Direction, 1E-15);
            Assert.Equal(flat, ray.ProjectToRay2(Upright()));
        }

        [Fact]
        public void ARayInThePlane_IsPutBackOnThePlaneOfTheFrame()
        {
            // (5, 7) along (0.6, 0.8) in the frame is (10, 25, 37) along (0, 0.6, 0.8) in space.
            var flat = new GeoRay2(new GeoPoint2(5, 7), new GeoVector2(3, 4));

            GeoRay3 ray = PlanarMap.ToRay3(Upright(), flat);

            AssertNear(new GeoPoint3(10, 25, 37), ray.Origin);
            AssertNear(new GeoVector3(0, 0.6, 0.8), ray.Direction, 1E-15);
            Assert.Equal(ray, flat.ToRay3(Upright()));
        }

        [Fact]
        public void ARayLaidOutAndPutBack_IsTheRayItWas_WhenItLayInThePlane()
        {
            var inPlane = new GeoRay3(new GeoPoint3(10, -4, 81), new GeoVector3(0, 1, 2));
            var flat = new GeoRay2(new GeoPoint2(-3, 9), new GeoVector2(1, 2));

            Assert.True(PlanarMap.ToRay3(Upright(), PlanarMap.ProjectToRay2(Upright(), inPlane)).IsEqualTo(inPlane, new Tolerance(1E-12, 1E-12)));
            Assert.True(PlanarMap.ProjectToRay2(Upright(), PlanarMap.ToRay3(Upright(), flat)).IsEqualTo(flat, new Tolerance(1E-12, 1E-12)));
        }

        [Fact]
        public void ARayAlongTheFramesNormal_HasNoDirectionInThePlane_AndIsRefused()
        {
            // Along world X, either way, the direction lays out as (0, 0).
            var along = new GeoRay3(new GeoPoint3(15, 25, 37), GeoVector3.XAxis);
            var back = new GeoRay3(new GeoPoint3(15, 25, 37), GeoVector3.XAxis.Negate());

            Assert.Throws<ArgumentException>(() => PlanarMap.ProjectToRay2(Upright(), along));
            Assert.Throws<ArgumentException>(() => PlanarMap.ProjectToRay2(Upright(), back));
            Assert.Throws<ArgumentException>(() => along.ProjectToRay2(Upright()));
        }

        #endregion

        #region The crossings of a hierarchy (D15)

        /// <summary>
        /// The segment D9 cuts for the ray against the hierarchy: from the origin to the point R along, where R is the
        /// furthest reach of the box's corners along the ray, at least nought, plus max(1, 100 times the point tolerance).
        /// </summary>
        private static GeoLine2 Cut(GeoRay2 ray, GeoVector2 unit, GeoBvh2 index, Tolerance tolerance)
        {
            double far = index.Bounds.GetVertices().Max(c => (c.X - ray.Origin.X) * unit.X + (c.Y - ray.Origin.Y) * unit.Y);
            double reach = Math.Max(0.0, far) + Math.Max(1.0, 100 * tolerance.EqualPoint);

            return new GeoLine2(ray.Origin, new GeoPoint2(ray.Origin.X + reach * unit.X, ray.Origin.Y + reach * unit.Y));
        }

        [Fact]
        public void RaysFromInsideACog_CrossItWhereTheCogItselfAndTheCutSegmentSay()
        {
            // Twenty-one rays from (3, -2), inside the cog of 24 corners reaching 100, at angles that pass no corner.
            GeoPolygonArc2 cog = Cog(24, 100);
            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);

            for (int k = 0; k < 21; k++)
            {
                double angle = 0.05 + k * 0.29;
                GeoVector2 unit = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                var ray = new GeoRay2(new GeoPoint2(3, -2), unit);

                GeoPoint2[] found = index.GetIntersections(ray, Strict);

                Assert.NotEmpty(found);
                AssertSamePoints(cog.GetIntersections(ray, Strict), found);
                AssertSamePoints(index.GetIntersections(Cut(ray, unit, index, Strict), Strict), found);
            }
        }

        [Fact]
        public void RaysFromOutsideACog_CrossItWhereTheCogItselfSays_AndPointingAwayCrossNothing()
        {
            // From x = -300 along X at heights 13.7 apart: some miss, some cross; turned round, none reach it.
            GeoPolygonArc2 cog = Cog(18, 100);
            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);
            int crossing = 0;

            for (double y = -130.3; y < 130; y += 13.7)
            {
                var ray = new GeoRay2(new GeoPoint2(-300, y), GeoVector2.XAxis);
                GeoPoint2[] found = index.GetIntersections(ray, Strict);

                AssertSamePoints(cog.GetIntersections(ray, Strict), found);
                AssertSamePoints(index.GetIntersections(Cut(ray, GeoVector2.XAxis, index, Strict), Strict), found);
                Assert.Empty(index.GetIntersections(ray.Reverse(), Strict));
                crossing += found.Length > 0 ? 1 : 0;
            }

            Assert.InRange(crossing, 10, 18);
        }

        [Fact]
        public void TheSquaresAndThePlatesHierarchies_CrossARayWhereTheShapesThemselvesDo()
        {
            var ray = new GeoRay2(new GeoPoint2(-50, 50), GeoVector2.XAxis);

            AssertSamePoints(new[] { new GeoPoint2(0, 50), new GeoPoint2(100, 50) }, GeoBvh2.FromPolygon(Square()).GetIntersections(ray, Strict));
            AssertSamePoints(Square().GetIntersections(ray, Strict), GeoBvh2.FromPolygon(Square()).GetIntersections(ray, Strict));
            AssertSamePoints(Plate().GetIntersections(ray, Strict), GeoBvh2.FromFace(Plate()).GetIntersections(ray, Strict));
            AssertSamePoints(Ell().GetIntersections(new GeoRay2(new GeoPoint2(20, -50), new GeoVector2(1, 1)), Strict), GeoBvh2.FromPolyline(Ell()).GetIntersections(new GeoRay2(new GeoPoint2(20, -50), new GeoVector2(1, 1)), Strict));
        }

        [Fact]
        public void ACogFarAlongTheRay_IsStillReached()
        {
            // The cog moved 5000 along X: the cut reaches past its box, so both its crossings at y = 0 are found.
            GeoPolygonArc2 cog = Cog(24, 100).Translate(new GeoVector2(5000, 0));
            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);
            var ray = new GeoRay2(new GeoPoint2(0, 0.5), GeoVector2.XAxis);

            GeoPoint2[] found = index.GetIntersections(ray, Strict);

            Assert.Equal(2, found.Length);
            AssertSamePoints(cog.GetIntersections(ray, Strict), found);
        }

        [Fact]
        public void AnEmptyHierarchy_CrossesNoRay()
        {
            var empty = new GeoBvh2(new GeoEdge2[0]);
            var ray = new GeoRay2(new GeoPoint2(0, 0), GeoVector2.XAxis);

            Assert.Empty(empty.GetIntersections(ray, Strict));
            Assert.Empty(empty.GetIntersections(ray));
        }

        [Fact]
        public void TheHierarchysFormWithoutATolerance_AnswersAsTheGlobalToleranceDoes()
        {
            GeoBvh2 index = GeoBvh2.FromPolygonArc(Cog(24, 100));

            foreach (GeoRay2 ray in new[] { new GeoRay2(new GeoPoint2(3, -2), new GeoVector2(1, 2)), new GeoRay2(new GeoPoint2(-300, 7), GeoVector2.XAxis) })
            {
                Assert.Equal(index.GetIntersections(ray, Tolerance.Global), index.GetIntersections(ray));
            }
        }

        #endregion
    }
}
