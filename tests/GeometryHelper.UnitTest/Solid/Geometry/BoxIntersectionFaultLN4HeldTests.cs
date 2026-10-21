using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment held within two slabs of a box it runs outside of is given, for a crossing read off the third slab that
    /// stands further than the point tolerance from the box, the point of it nearest the box.
    /// </summary>
    /// <remarks>
    /// A segment that barely moves along an axis is held within that pair of faces where it runs no further outside than the
    /// tolerance, and its crossings are read off the other slabs. Held on two axes, beside an edge of the box, such a crossing
    /// stands up to the tolerance off each of the two faces, so up to 1.4 times it off the edge: 3 of 100 000 segments near the
    /// box 10 by 10 by 10 were given a point 0.00106 off it, and 14 rays. Such a crossing is the point of the segment nearest the
    /// box now; the cutting of a segment or a chain by a box is unchanged.
    /// </remarks>
    public class BoxIntersectionFaultLN4HeldTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoAabb3 Box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10));

        // Beside the edge along Z at the origin, 0.0009 off both faces at its start and 0.0004 off at its end, rising through
        // the bottom face's plane.
        private static readonly GeoLine3 Beside = new GeoLine3(new GeoPoint3(-0.0009, -0.0009, -0.0004), new GeoPoint3(-0.0004, -0.0004, 0.0016));

        [Fact]
        public void ACrossingReadBesideAnEdge_IsThePointOfTheSegmentNearestTheBox()
        {
            // Where it rises through z = 0 it stands at (-0.0008, -0.0008, 0), 0.00113 off the box; its end is 0.00057 off.
            Assert.True(Box.CollidesWith(Beside, Default));

            GeoPoint3 hit = Assert.Single(Box.GetIntersections(Beside, Default));

            Assert.Equal(0.0, hit.DistanceTo(Beside.EndPoint), 12);
            Assert.Equal(0.0, Assert.Single(Box.ToObb().GetIntersections(Beside, Default)).DistanceTo(Beside.EndPoint), 12);
            Assert.Equal(0.0, Assert.Single(Box.GetIntersections(new GeoLine3(Beside.EndPoint, Beside.StartPoint), Default)).DistanceTo(Beside.EndPoint), 12);
        }

        [Fact]
        public void EveryCrossingOfARayAndAChainBesideAnEdge_StandsWithinTheTolerance()
        {
            var ray = new GeoRay3(Beside.StartPoint, Beside.Direction);
            var chain = new GeoPolyline3(new[] { Beside.StartPoint, Beside.EndPoint, new GeoPoint3(-50, -50, 0.0016) });

            foreach (GeoPoint3 hit in Box.GetIntersections(ray, Default))
            {
                Assert.True(Box.ToObb().DistanceTo(hit) <= Default.EqualPoint);
            }

            foreach (GeoPoint3 hit in Box.GetIntersections(chain, Default))
            {
                Assert.True(Box.ToObb().DistanceTo(hit) <= Default.EqualPoint);
            }

            Assert.NotEmpty(Box.GetIntersections(chain, Default));
        }
    }
}
