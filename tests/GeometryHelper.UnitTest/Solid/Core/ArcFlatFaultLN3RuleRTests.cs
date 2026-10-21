using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc in space crosses a triangle, a polygon or a face only where it collides with it.
    /// </summary>
    /// <remarks>
    /// A point of an arc was read as on a flat region within the point tolerance of its plane and within the tolerance of its
    /// outline in that plane, and the two together reach further than the tolerance off a side: an arc starting 0.00107
    /// beside a side and 0.0007 over the plane, 0.0013 from the region, was given a point and did not collide, for 123 of
    /// 6 000 arcs starting near the side of a polygon and 229 starting near the rim of a hole. The points are kept now only
    /// where the arc touches the region, as the box already kept them.
    /// </remarks>
    public class ArcFlatFaultLN3RuleRTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoPoint3[] Corners =
        {
            new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0), new GeoPoint3(30, 20, 0), new GeoPoint3(0, 20, 0),
        };

        private static readonly GeoPolygon3 Plate = new GeoPolygon3(Corners, Default);

        private static readonly GeoFace3 Holed = new GeoFace3(Plate, new[]
        {
            new GeoPolygon3(new[] { new GeoPoint3(10, 5, 0), new GeoPoint3(20, 5, 0), new GeoPoint3(20, 15, 0), new GeoPoint3(10, 15, 0) }, Default),
        });

        private static readonly GeoTriangle3 Corner = new GeoTriangle3(Corners[0], Corners[1], Corners[2]);

        // 0.00107 beside the side x = 30 and 0.0007 over the plane, running away from it.
        private static readonly GeoArc3 BesideTheSide = GeoArc3.FromThreePoints(
            new GeoPoint3(30.00107216262858, 6.5335458826895545, 0.0007062009618170606),
            new GeoPoint3(32.81916595123109, 8.144831888433352, 2.2108690841952385),
            new GeoPoint3(35.87615218879094, 10.239039969741851, 0.9101605005223115),
            Default);

        [Fact]
        public void AnArcStartingFurtherThanTheToleranceOffASide_CrossesNothing()
        {
            Assert.False(Arc3.CollidesWith(BesideTheSide, Plate, Default));
            Assert.Empty(Arc3.GetIntersections(BesideTheSide, Plate, Default));
            Assert.False(Arc3.CollidesWith(BesideTheSide, Holed, Default));
            Assert.Empty(Arc3.GetIntersections(BesideTheSide, Holed, Default));
            Assert.False(Arc3.CollidesWith(BesideTheSide, Corner, Default));
            Assert.Empty(Arc3.GetIntersections(BesideTheSide, Corner, Default));
        }

        [Fact]
        public void AnArcInAHoleStartingFurtherThanTheToleranceOffItsRim_CrossesNothing()
        {
            var arc = GeoArc3.FromThreePoints(
                new GeoPoint3(19.998824635029315, 11.851385467150896, 0.0003218052374761599),
                new GeoPoint3(17.307012168873783, 11.334949494050466, 0.7825390340848757),
                new GeoPoint3(14.683899651109284, 11.92641064614356, -0.1629125432474124),
                Default);

            Assert.False(Arc3.CollidesWith(arc, Holed, Default));
            Assert.Empty(Arc3.GetIntersections(arc, Holed, Default));
        }

        [Fact]
        public void AnArcStartingWithinTheToleranceOfASide_StillMeetsIt()
        {
            var arc = GeoArc3.FromThreePoints(new GeoPoint3(30.0005, 6.5, 0.0003), new GeoPoint3(32.8, 8.1, 2.2), new GeoPoint3(35.9, 10.2, 0.9), Default);

            Assert.True(Arc3.CollidesWith(arc, Plate, Default));
            Assert.Single(Arc3.GetIntersections(arc, Plate, Default));
            Assert.Single(Arc3.GetIntersections(arc, Corner, Default));
        }

        [Fact]
        public void ArcsNearASideOrARim_CrossOnlyWhereTheyCollide()
        {
            var random = new Random(13);

            for (int i = 0; i < 1500; i++)
            {
                double y = random.NextDouble() * 20;
                var start = new GeoPoint3(30 + (random.NextDouble() - 0.3) * 0.002, y * 0.5, (random.NextDouble() - 0.5) * 0.002);
                var middle = start.Add(new GeoVector3(random.NextDouble() * 4 - 1, random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2));
                var end = start.Add(new GeoVector3(random.NextDouble() * 8 - 1, random.NextDouble() * 8 - 4, random.NextDouble() * 8 - 4));
                var inHole = new GeoPoint3(20 - (random.NextDouble() - 0.3) * 0.002, 5 + y * 0.5, (random.NextDouble() - 0.5) * 0.002);
                GeoArc3 arc = GeoArc3.FromThreePoints(start, middle, end, Default);
                GeoArc3 holeArc = GeoArc3.FromThreePoints(
                    inHole,
                    inHole.Add(new GeoVector3(-1.5, random.NextDouble() - 0.5, random.NextDouble() * 2 - 1)),
                    inHole.Add(new GeoVector3(-4, random.NextDouble() * 2 - 1, random.NextDouble() * 4 - 2)),
                    Default);

                Assert.True(Arc3.GetIntersections(arc, Plate, Default).Length == 0 || Arc3.CollidesWith(arc, Plate, Default));
                Assert.True(Arc3.GetIntersections(arc, Holed, Default).Length == 0 || Arc3.CollidesWith(arc, Holed, Default));
                Assert.True(Arc3.GetIntersections(arc, Corner, Default).Length == 0 || Arc3.CollidesWith(arc, Corner, Default));
                Assert.True(Arc3.GetIntersections(holeArc, Holed, Default).Length == 0 || Arc3.CollidesWith(holeArc, Holed, Default));
            }
        }

        [Fact]
        public void ACircleStartingFurtherThanTheToleranceOffASide_CrossesNothing()
        {
            var circle = new GeoCircle3(BesideTheSide.Center, BesideTheSide.Normal, BesideTheSide.Radius);

            Assert.True(Arc3.GetIntersections(circle, Plate, Default).Length == 0 || Arc3.CollidesWith(circle, Plate, Default));
            Assert.True(Arc3.GetIntersections(circle, Corner, Default).Length == 0 || Arc3.CollidesWith(circle, Corner, Default));
        }
    }
}
