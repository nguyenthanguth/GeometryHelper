using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc in space that touches a triangle, a polygon, a face or a box within the point tolerance without crossing it is
    /// given the point of it nearest the shape, and one lying level over the shape is given none.
    /// </summary>
    /// <remarks>
    /// An arc coming within the tolerance of a side, a rim or a corner collided and was given no point, its plane crossings
    /// lying further off: of 20 000 arcs near each shape, a third of them within 1.4E-4 of flat, 2 786 near a triangle's sides,
    /// 2 191 near a polygon's, 2 473 near a face's and 809 near a box's, of them tilted any way 1 776, 1 137, 1 403 and 354. As the box reads a segment (LN3), an isolated touch is given its nearest point, and an arc lying
    /// level over the material within the tolerance none, as a curve lying on a surface crosses it nowhere. Cutting a chain
    /// by a face or a box is unchanged: a touch cuts nothing.
    /// </remarks>
    public class ArcFlatFaultLN4TouchTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoPolygon3 Plate = new GeoPolygon3(
            new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0), new GeoPoint3(30, 20, 0), new GeoPoint3(0, 20, 0) }, Default);

        private static readonly GeoFace3 Holed = new GeoFace3(Plate, new[]
        {
            new GeoPolygon3(new[] { new GeoPoint3(10, 5, 0), new GeoPoint3(10, 15, 0), new GeoPoint3(20, 15, 0), new GeoPoint3(20, 5, 0) }, Default),
        });

        private static readonly GeoTriangle3 Corner = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0), new GeoPoint3(15, 20, 0));

        /// <summary>
        /// An arc of radius 5 standing upright in the plane y = -0.0006, dipping to (15, -0.0006, 0.0006): 0.00085 from the
        /// side y = 0, and never down to the plane.
        /// </summary>
        private static readonly GeoArc3 Dipping = GeoArc3.FromThreePoints(
            new GeoPoint3(12, -0.0006, 1.0006), new GeoPoint3(15, -0.0006, 0.0006), new GeoPoint3(18, -0.0006, 1.0006), Default);

        private static readonly GeoPoint3 Lowest = new GeoPoint3(15, -0.0006, 0.0006);

        [Fact]
        public void ATiltedArcDippingWithinTheToleranceOfASide_IsGivenItsLowestPoint()
        {
            foreach (GeoPoint3[] hits in new[]
            {
                Arc3.GetIntersections(Dipping, Plate, Default),
                Arc3.GetIntersections(Dipping, Holed, Default),
                Arc3.GetIntersections(Dipping, Corner, Default),
            })
            {
                GeoPoint3 hit = Assert.Single(hits);
                Assert.True(hit.DistanceTo(Lowest) < 1E-9);
            }

            Assert.True(Arc3.CollidesWith(Dipping, Plate, Default));
            Assert.True(Arc3.CollidesWith(Dipping, Holed, Default));
            Assert.True(Arc3.CollidesWith(Dipping, Corner, Default));
        }

        [Fact]
        public void ATiltedArcDippingWithinTheToleranceOfABoxEdge_IsGivenItsLowestPoint()
        {
            // The plate as the top of a box 30 by 20 by 10.
            var box = new GeoAabb3(new GeoPoint3(0, 0, -10), new GeoPoint3(30, 20, 0));

            Assert.True(Arc3.CollidesWith(Dipping, box, Default));
            Assert.True(Assert.Single(Arc3.GetIntersections(Dipping, box, Default)).DistanceTo(Lowest) < 1E-9);
            Assert.True(Assert.Single(Arc3.GetIntersections(Dipping, box.ToObb(), Default)).DistanceTo(Lowest) < 1E-9);
        }

        [Fact]
        public void AnArcDippingFurtherThanTheTolerance_IsGivenNone()
        {
            var clear = GeoArc3.FromThreePoints(new GeoPoint3(12, -0.0008, 1.0008), new GeoPoint3(15, -0.0008, 0.0008), new GeoPoint3(18, -0.0008, 1.0008), Default);

            Assert.False(Arc3.CollidesWith(clear, Plate, Default));
            Assert.Empty(Arc3.GetIntersections(clear, Plate, Default));
        }

        [Fact]
        public void AnArcLyingLevelOverTheMaterial_IsGivenNone()
        {
            // 0.0005 over the plate and level with it, within the tolerance all along.
            var level = new GeoArc3(new GeoPoint3(5, 10, 0.0005), GeoVector3.ZAxis, 3, 0.2, 2.0);
            var box = new GeoAabb3(new GeoPoint3(0, 0, -10), new GeoPoint3(30, 20, 0));

            Assert.True(Arc3.CollidesWith(level, Plate, Default));
            Assert.Empty(Arc3.GetIntersections(level, Plate, Default));
            Assert.Empty(Arc3.GetIntersections(level, Holed, Default));
            Assert.Empty(Arc3.GetIntersections(level, box, Default));
        }

        [Fact]
        public void AnArcInThePlaneBowingWithinTheToleranceOfASide_IsGivenItsNearestPoint()
        {
            // In the plane, outside the side y = 0, bowing to (15, -0.0006, 0) with a radius of 5: it lies along nothing.
            var beside = GeoArc3.FromThreePoints(new GeoPoint3(12, -1.0006, 0), new GeoPoint3(15, -0.0006, 0), new GeoPoint3(18, -1.0006, 0), Default);

            Assert.True(Arc3.CollidesWith(beside, Plate, Default));
            Assert.True(Assert.Single(Arc3.GetIntersections(beside, Plate, Default)).DistanceTo(new GeoPoint3(15, -0.0006, 0)) < 1E-9);
        }

        [Fact]
        public void ArcsNearASideARimOrACorner_AreGivenAPointExactlyWhereTheyCollide()
        {
            var random = new Random(29);
            int touching = 0;

            for (int i = 0; i < 2000; i++)
            {
                // Through a point within 0.002 of a side, the rim of the hole or a corner, tilted any way.
                GeoPoint3 near;
                int at = random.Next(3);
                double Jitter() => (random.NextDouble() * 2 - 1) * 0.002;

                if (at == 0)
                {
                    near = new GeoPoint3(random.NextDouble() * 30, Jitter(), Jitter());
                }
                else if (at == 1)
                {
                    near = new GeoPoint3(10 + Jitter(), 5 + random.NextDouble() * 10, Jitter());
                }
                else
                {
                    near = new GeoPoint3(30 + Jitter(), 20 + Jitter(), Jitter());
                }

                var normal = new GeoVector3(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);

                if (normal.Length < 0.1)
                {
                    continue;
                }

                double radius = 0.5 + random.NextDouble() * 20;
                double sweep = 0.1 + random.NextDouble() * 2.5;
                var probe = new GeoArc3(GeoPoint3.Origin, normal, radius, -sweep / 2, sweep / 2);
                GeoPoint3 middle = probe.MidPoint;
                var arc = new GeoArc3(new GeoPoint3(near.X - middle.X, near.Y - middle.Y, near.Z - middle.Z), normal, radius, -sweep / 2, sweep / 2);

                bool collides = Arc3.CollidesWith(arc, Holed, Default);
                GeoPoint3[] hits = Arc3.GetIntersections(arc, Holed, Default);

                Assert.Equal(collides, hits.Length > 0);

                foreach (GeoPoint3 hit in hits)
                {
                    Assert.True(Holed.DistanceTo(hit, Default) <= Default.EqualPoint * (1 + 1E-9));
                    Assert.True(arc.DistanceTo(hit, Default) <= Default.EqualPoint * (1 + 1E-9));
                }

                touching += collides ? 1 : 0;
            }

            Assert.InRange(touching, 200, 2000);
        }

        [Fact]
        public void AChainWhoseArcOnlyTouchesAFace_IsNotCut()
        {
            // The arc dips to within 0.00085 of the side and runs on: a touch, which cuts nothing.
            var chain = new GeoPolylineArc3(new[] { new GeoEdge3(Dipping) });

            Assert.False(chain.TrySplitBy(Holed, out GeoPolylineArc3[] pieces, Default));
            Assert.Single(pieces);
            Assert.Single(chain.GetIntersections(Holed, Default));
        }
    }
}
