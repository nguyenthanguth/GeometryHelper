using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The index over the edges of a shape in the plane. It is only worth anything if it answers exactly
    /// what the plain walk over every edge answers, so that is what every test here checks it against.
    /// </summary>
    public class GeoBvh2Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-8, 1E-8);

        /// <summary>
        /// A ring of many small shapes, so that the tree has something to prune.
        /// </summary>
        private static GeoPolygonArc2 Cog(int teeth, double radius)
        {
            var vertices = new List<GeoPoint2>();
            var bulges = new List<double>();

            for (int i = 0; i < teeth; i++)
            {
                double angle = i * Math.PI * 2.0 / teeth;
                double reach = i % 2 == 0 ? radius : radius * 0.7;

                vertices.Add(new GeoPoint2(reach * Math.Cos(angle), reach * Math.Sin(angle)));
                bulges.Add(i % 3 == 0 ? 0.3 : 0.0);
            }

            return new GeoPolygonArc2(vertices, bulges);
        }

        [Fact]
        public void TheNearestPointIsTheOneAPlainWalkFinds()
        {
            GeoPolygonArc2 cog = Cog(24, 100.0);
            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);

            Assert.Equal(cog.EdgeCount, index.EdgeCount);

            for (double x = -160; x <= 160; x += 11.5)
            {
                for (double y = -160; y <= 160; y += 11.5)
                {
                    var point = new GeoPoint2(x, y);

                    // The index measures to the edges, so a point inside the cog is measured out to the
                    // boundary rather than called nought as the region itself would call it.
                    GeoPoint2 onBoundary = cog.GetClosestPointOnBoundary(point);

                    Assert.Equal(point.DistanceTo(onBoundary), index.DistanceTo(point), 9);
                    Assert.True(index.GetClosestPoint(point).IsEqualTo(onBoundary, Tight));
                }
            }
        }

        [Fact]
        public void TheCrossingsAreTheOnesAPlainWalkFinds()
        {
            GeoPolygonArc2 cog = Cog(18, 100.0);
            GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);

            for (double angle = 0.0; angle < Math.PI; angle += 0.17)
            {
                var knife = new GeoLine2(
                    new GeoPoint2(-200 * Math.Cos(angle), -200 * Math.Sin(angle)),
                    new GeoPoint2(200 * Math.Cos(angle), 200 * Math.Sin(angle)));

                GeoPoint2[] fromIndex = index.GetIntersections(knife);
                GeoPoint2[] fromWalk = cog.GetIntersections(knife);

                Assert.Equal(fromWalk.Length, fromIndex.Length);

                foreach (GeoPoint2 one in fromWalk)
                {
                    Assert.Contains(fromIndex, other => other.IsEqualTo(one, Tight));
                }
            }
        }

        [Fact]
        public void TheBoxRoundItHoldsEveryEdgeIncludingWhereTheArcsBulge()
        {
            var slot = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 1.0 });

            GeoRectangle2 bounds = GeoBvh2.FromPolygonArc(slot).Bounds;

            // The round ends reach 25 beyond the chords, and the box has to know that.
            Assert.Equal(250.0, bounds.Width, 8);
            Assert.Equal(50.0, bounds.Height, 8);
            Assert.True(bounds.Center.IsEqualTo(new GeoPoint2(100, 25), Tight));

            // Every point of the shape is inside it.
            for (double t = 0.0; t <= 1.0; t += 0.004)
            {
                Assert.True(Containment2.Contains(bounds, slot.GetPointAtParameter(t), new Tolerance(1E-6, 1E-6)));
            }
        }

        [Fact]
        public void TwoIndexesMeasureAndMeetLikeTheShapesThemselves()
        {
            GeoPolygonArc2 first = Cog(12, 60.0);
            GeoPolygonArc2 apart = first.Translate(new GeoVector2(400, 0));
            GeoPolygonArc2 touching = first.Translate(new GeoVector2(90, 0));

            GeoBvh2 one = GeoBvh2.FromPolygonArc(first);

            Assert.Equal(first.DistanceTo(apart), one.DistanceTo(GeoBvh2.FromPolygonArc(apart)), 8);
            Assert.False(one.CollidesWith(GeoBvh2.FromPolygonArc(apart)));

            Assert.Equal(0.0, one.DistanceTo(GeoBvh2.FromPolygonArc(touching)), 8);
            Assert.True(one.CollidesWith(GeoBvh2.FromPolygonArc(touching)));

            // The straight shapes go in as readily as the curved ones.
            var plate = new GeoPolygon2(
                new GeoPoint2(300, -20), new GeoPoint2(340, -20), new GeoPoint2(340, 20), new GeoPoint2(300, 20));

            Assert.Equal(first.DistanceTo(plate), one.DistanceTo(GeoBvh2.FromPolygon(plate)), 8);
        }

        [Fact]
        public void AStraightChainGoesInAndComesBackUnchanged()
        {
            var chain = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(200, 100));

            GeoBvh2 index = GeoBvh2.FromPolyline(chain);

            Assert.Equal(3, index.EdgeCount);
            Assert.DoesNotContain(index.Edges, edge => edge.IsArc);

            var point = new GeoPoint2(150, 0);

            Assert.Equal(chain.DistanceTo(point), index.DistanceTo(point), 9);
            Assert.Contains("GeoBvh2", index.ToString());

            GeoPolylineArc2 curved = new GeoPolylineArc2(chain);
            Assert.Equal(curved.EdgeCount, GeoBvh2.FromPolylineArc(curved).EdgeCount);
        }

        [Fact]
        public void NothingIndexedIsRefusedRatherThanAnswered()
        {
            var empty = new GeoBvh2(new GeoEdge2[0]);

            Assert.Equal(0, empty.EdgeCount);
            Assert.Empty(empty.GetIntersections(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 1))));
            Assert.Throws<InvalidOperationException>(() => empty.Bounds);
            Assert.Throws<InvalidOperationException>(() => empty.GetClosestPoint(GeoPoint2.Origin));
            Assert.Equal(double.PositiveInfinity, empty.DistanceTo(empty));

            Assert.Throws<ArgumentNullException>(() => new GeoBvh2(null));
            Assert.Throws<ArgumentNullException>(() => GeoBvh2.FromPolygonArc(null));
            Assert.Throws<ArgumentNullException>(() => GeoBvh2.FromPolylineArc(null));
            Assert.Throws<ArgumentNullException>(() => GeoBvh2.FromPolygon(null));
            Assert.Throws<ArgumentNullException>(() => GeoBvh2.FromPolyline(null));
            Assert.Throws<ArgumentNullException>(() => empty.DistanceTo((GeoBvh2)null));
            Assert.Throws<ArgumentNullException>(() => empty.CollidesWith(null));
        }
    }
}
