using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A plane crossing a body where it is thinner than the point tolerance: the two sides of the section stand within the
    /// tolerance of each other, and their edges cancel as one edge run both ways would. The halves would be open by a
    /// sliver no cap closes, so the body is not split; and a plane parting a body at a pinch, its edges one edge run both
    /// ways but for the rounding, still splits it wherever the body lies.
    /// </summary>
    public class ThinSectionTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(P(x0, y0, z0), P(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>A wedge 3000 wide along X rising from an edge at y = 0 to 300 high at y = 6000, its faces triangles.</summary>
        private static GeoSolid3 Wedge()
        {
            GeoSolid3 prism = GeoSolid3.Extrude(new GeoPolygon3(P(0, 0, 0), P(0, 6000, 0), P(0, 6000, 300)), new GeoVector3(3000, 0, 0), Tolerance);
            return new GeoSolid3(prism.Triangulate(Tolerance).Select(t => new GeoFace3(new GeoPolygon3(t.A, t.B, t.C))));
        }

        [Fact]
        public void AWedgeCutWhereItIsThinnerThanThePointToleranceIsNotSplitOpen()
        {
            // At y = 0.1 the wedge is 0.005 thick: the section's long sides cancel, and the halves would have no cap.
            GeoSolid3 wedge = Wedge();

            if (wedge.TrySplitBy(new GeoPlane3(P(0, 0.1, 0), GeoVector3.YAxis), out GeoSolid3 above, out GeoSolid3 below, Tolerance))
            {
                Assert.Equal(wedge.Volume, above.Volume + below.Volume, 1);
            }
        }

        [Fact]
        public void TakingABoxEndingWhereTheWedgeIsThinOutOfItLeavesItsVolume()
        {
            GeoSolid3 wedge = Wedge();

            Assert.True(Boolean3.TrySubtract(wedge, Box(-100, -100, -100, 3100, 0.1, 400), out GeoSolid3 rest, Tolerance));

            // The tip the box takes holds 0.75; left on, it is within the tolerance of the section the box ends at.
            Assert.True(Math.Abs(wedge.Volume - rest.Volume) <= 1.0, $"the rest holds {rest.Volume:R} of {wedge.Volume:R}");
        }

        [Fact]
        public void ATubeWithAWallThinnerThanThePointToleranceIsNotSplitOpen()
        {
            // A square tube 100 across with a wall 0.005 thick: its section is a ring whose two sides cancel.
            var outer = new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 100, 0), P(0, 100, 0));
            var inner = new GeoPolygon3(P(0.005, 0.005, 0), P(99.995, 0.005, 0), P(99.995, 99.995, 0), P(0.005, 99.995, 0));
            GeoSolid3 tube = GeoSolid3.Extrude(new GeoFace3(outer, new[] { inner }), new GeoVector3(0, 0, 50), Tolerance);
            Assert.True(tube.IsClosed(Tolerance));

            if (tube.TrySplitBy(new GeoPlane3(P(0, 0, 25), GeoVector3.ZAxis), out GeoSolid3 above, out GeoSolid3 below, Tolerance))
            {
                Assert.Equal(tube.Volume, above.Volume + below.Volume, 3);
            }
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        [InlineData(3E7)]
        public void APlaneThroughAPinchSplitsTheBodyWhereverItLies(double far)
        {
            // Two triangular prisms meeting along an edge, turned and moved out: the edges the plane leaves there are one edge
            // run both ways, apart only by the rounding of the points the cut put on it.
            var section = new GeoPolygon3(P(0, -1000, 0), P(500, 0, 0), P(100, 900, 0), P(500, 1000, 0), P(500, -1000, 0));
            GeoSolid3 body = GeoSolid3.Extrude(section, new GeoVector3(0, 0, 600), Tolerance);
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(far, -0.4 * far, 0.1 * far))
                .Multiply(GeoTransform3.RotationAxis(P(0, 0, 0), new GeoVector3(0.3, -0.7, 0.2), 1.1));
            GeoSolid3 moved = body.TransformBy(move);

            Assert.True(moved.TrySplitBy(new GeoPlane3(P(0, 0, 0), GeoVector3.YAxis).TransformBy(move), out GeoSolid3 above, out GeoSolid3 below, Tolerance));
            Assert.Equal(1.0, above.Volume / 1.2E8, 9);
            Assert.Equal(1.0, below.Volume / 1.5E8, 9);
        }
    }
}
