using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// One body per region two bodies share, and the separate pieces of one body.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoSolid3.TryIntersect(GeoSolid3, out GeoSolid3)"/> hands the whole shared region back as one
    /// body, which is right for its volume and says nothing about where each clash is. The pieces here add up to
    /// that same volume, which is the check that nothing was lost or counted twice.
    /// </remarks>
    public class SolidPiecesTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        [Fact]
        public void ABeamThroughTwoPlatesClashesTwice()
        {
            Assert.True(Box(0, 0, 0, 30, 100, 30).TryUnion(Box(70, 0, 0, 100, 100, 30), out GeoSolid3 plates));

            GeoSolid3 beam = Box(-10, 40, 10, 110, 60, 20);

            GeoSolid3[] clashes = plates.Intersect(beam);

            Assert.Equal(2, clashes.Length);
            Assert.All(clashes, clash => Assert.Equal(30.0 * 20 * 10, clash.Volume, 3));

            // The same volume as the one-body answer, split where it really is split.
            Assert.True(plates.TryIntersect(beam, out GeoSolid3 whole));
            Assert.Equal(whole.Volume, clashes.Sum(clash => clash.Volume), 3);

            // Each clash sits in its own plate.
            Assert.Single(clashes, clash => clash.GetAabb().Max.X <= 30.0 + 1E-6);
            Assert.Single(clashes, clash => clash.GetAabb().Min.X >= 70.0 - 1E-6);
        }

        [Fact]
        public void OneOverlapIsOnePiece()
        {
            GeoSolid3[] shared = Box(0, 0, 0, 100, 100, 100).Intersect(Box(50, 50, 50, 150, 150, 150));

            Assert.Single(shared);
            Assert.Equal(50.0 * 50 * 50, shared[0].Volume, 3);
        }

        [Fact]
        public void NoSharedVolumeIsNoPiece()
        {
            GeoSolid3 block = Box(0, 0, 0, 100, 100, 100);

            // Apart, touching on a face, an edge or a corner: no volume in common.
            Assert.Empty(block.Intersect(Box(500, 0, 0, 600, 100, 100)));
            Assert.Empty(block.Intersect(Box(100, 0, 0, 200, 100, 100)));
            Assert.Empty(block.Intersect(Box(100, 100, 0, 200, 200, 100)));
            Assert.Empty(block.Intersect(Box(100, 100, 100, 200, 200, 200)));

            // A pin through a bolt hole, clear of every wall, shares none of the plate either.
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });

            Assert.Empty(plate.Intersect(Box(45, 45, -50, 55, 55, 50)));
        }

        [Fact]
        public void TwoRegionsMeetingOnlyAlongAnEdgeAreTwoPieces()
        {
            // Two blocks of a body that touch along one edge, and a probe covering both corners where they meet.
            var body = new GeoSolid3(Box(0, 0, 0, 100, 100, 100).Faces.Concat(Box(100, 100, 0, 200, 200, 100).Faces));
            GeoSolid3 probe = Box(50, 50, 20, 150, 150, 80);

            GeoSolid3[] shared = body.Intersect(probe);

            Assert.Equal(2, shared.Length);
            Assert.All(shared, piece => Assert.Equal(50.0 * 50 * 60, piece.Volume, 3));
        }

        [Fact]
        public void ABodyCanBeSplitIntoItsPieces()
        {
            Assert.True(Box(0, 0, 0, 30, 100, 30).TryUnion(Box(70, 0, 0, 100, 100, 30), out GeoSolid3 two));

            GeoSolid3[] pieces = two.SplitShells();

            Assert.Equal(2, pieces.Length);
            Assert.Equal(two.Volume, pieces.Sum(piece => piece.Volume), 3);

            // A body in one piece comes back alone.
            Assert.Single(Box(0, 0, 0, 10, 10, 10).SplitShells());

            // A hollow body is one piece: its cavity belongs to it.
            GeoSolid3 hollow = Box(0, 0, 0, 100, 100, 100).WithOpenings(new[] { Box(25, 25, 25, 75, 75, 75) });

            Assert.True(hollow.TryCutOpenings(out GeoSolid3 cut));
            Assert.Single(cut.SplitShells());
            Assert.Equal(1000000.0 - 125000.0, cut.SplitShells()[0].Volume, 3);
        }

        [Fact]
        public void EveryWayInTakesAToleranceAndNothingIsAskedOfNothing()
        {
            GeoSolid3 block = Box(0, 0, 0, 100, 100, 100);
            GeoSolid3 other = Box(50, 50, 50, 150, 150, 150);

            Assert.Equal(block.Intersect(other).Length, block.Intersect(other, Tolerance.Global).Length);
            Assert.Equal(block.SplitShells().Length, block.SplitShells(Tolerance.Global).Length);

            Assert.Throws<ArgumentNullException>(() => Boolean3.SplitShells(null));
            Assert.Throws<ArgumentNullException>(() => Boolean3.Intersect(null, other));
            Assert.Throws<ArgumentNullException>(() => Boolean3.Intersect(block, null));
        }
    }
}
