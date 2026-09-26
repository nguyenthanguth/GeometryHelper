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
    /// Separating a body into the pieces of material that do not touch, and the boolean bug it fixed.
    /// </summary>
    /// <remarks>
    /// The booleans judge each cell by one point of it. A plane lying along the wall of a hole does not cut
    /// the strip either side of the hole, so that strip came out as one cell in two pieces, and subtracting a
    /// box that overlapped the hole threw away the far piece too: a plate with two overlapping holes measured
    /// 168 000 where it is 180 000. Every cell is now one piece before it is judged.
    /// </remarks>
    public class ShellsTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Together(params GeoSolid3[] parts)
            => new GeoSolid3(parts.SelectMany(part => part.Faces));

        [Fact]
        public void SubtractingABoxThatOverlapsAnExistingHoleKeepsTheMaterialFarFromIt()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20);

            Assert.True(Boolean3.TrySubtract(plate, Box(30, 40, -1, 60, 60, 21), out GeoSolid3 frame));
            Assert.Equal(188000.0, frame.Volume, 3);

            // The second hole overlaps the first by ten, so it takes away twenty by twenty by twenty more.
            Assert.True(Boolean3.TrySubtract(frame, Box(50, 40, -1, 80, 60, 21), out GeoSolid3 slotted));
            Assert.Equal(180000.0, slotted.Volume, 3);

            // The strip left of the old hole is material, which is exactly what the bug threw away.
            Assert.Equal(PointLocation.Inside, slotted.Locate(new GeoPoint3(20, 50, 10)));
            Assert.Equal(PointLocation.OutSide, slotted.Locate(new GeoPoint3(55, 50, 10)));
            Assert.Equal(PointLocation.OutSide, slotted.Locate(new GeoPoint3(70, 50, 10)));

            // And the net volume, which subtracts one opening at a time, agrees now.
            GeoSolid3 withOpenings = plate.WithOpenings(new[] { Box(30, 40, -1, 60, 60, 21), Box(50, 40, -1, 80, 60, 21) });

            Assert.Equal(180000.0, withOpenings.GetNetVolume(), 3);
        }

        [Fact]
        public void TwoBlocksThatNeverMeetAreTwoPieces()
        {
            GeoSolid3 two = Together(Box(0, 0, 0, 30, 100, 30), Box(70, 0, 0, 100, 100, 30));

            var pieces = Shells3.Split(two, Loose);

            Assert.Equal(2, pieces.Count);
            Assert.Equal(two.Volume, pieces.Sum(piece => piece.Volume), 6);
            Assert.All(pieces, piece => Assert.Equal(6, piece.Faces.Count));
        }

        [Fact]
        public void ABodyThatIsOnePieceComesBackAsItIs()
        {
            GeoSolid3 block = Box(0, 0, 0, 100, 100, 100);

            var pieces = Shells3.Split(block, Loose);

            Assert.Single(pieces);
            Assert.Same(block, pieces[0]);

            // A body read out of the booleans, with its faces merged and so edges that no longer meet end to
            // end, is still one piece: edges are matched by overlap along a line, not by their end points.
            Assert.True(Boolean3.TryUnion(Box(0, 0, 0, 100, 100, 20), Box(0, 0, 20, 50, 100, 40), out GeoSolid3 step));
            Assert.Single(Shells3.Split(step, Loose));
        }

        [Fact]
        public void TwoBlocksTouchingAlongAnEdgeShareNoMaterialAndStayTwo()
        {
            // Four faces meet round the one line, and the two wedges of material there are kept apart by two
            // wedges of empty space.
            GeoSolid3 two = Together(Box(0, 0, 0, 100, 100, 100), Box(100, 100, 0, 200, 200, 100));

            var pieces = Shells3.Split(two, Loose);

            Assert.Equal(2, pieces.Count);
            Assert.All(pieces, piece => Assert.Equal(1000000.0, piece.Volume, 3));
        }

        [Fact]
        public void TwoBlocksTouchingAtACornerStayTwo()
        {
            GeoSolid3 two = Together(Box(0, 0, 0, 100, 100, 100), Box(100, 100, 100, 200, 200, 200));

            Assert.Equal(2, Shells3.Split(two, Loose).Count);
        }

        [Fact]
        public void ACavityBelongsToThePieceAroundItAndIsNotAPieceOfItsOwn()
        {
            // A hollow block: the outer shell and, inside it, a shell wound inwards.
            GeoSolid3 outer = Box(0, 0, 0, 100, 100, 100);
            GeoSolid3 inner = Box(25, 25, 25, 75, 75, 75);
            var cavityFaces = inner.Faces.Select(face => new GeoFace3(face.Boundary.Flip()));
            var hollow = new GeoSolid3(outer.Faces.Concat(cavityFaces));

            Assert.Equal(1000000.0 - 125000.0, hollow.Volume, 3);

            var pieces = Shells3.Split(hollow, Loose);

            Assert.Single(pieces);
            Assert.Equal(hollow.Volume, pieces[0].Volume, 3);

            // A hollow block beside a plain one is two pieces, and the cavity stays with the hollow one.
            GeoSolid3 beside = Box(500, 0, 0, 600, 100, 100);
            var both = new GeoSolid3(hollow.Faces.Concat(beside.Faces));

            var two = Shells3.Split(both, Loose);

            Assert.Equal(2, two.Count);
            Assert.Contains(two, piece => Math.Abs(piece.Volume - hollow.Volume) < 1E-3);
            Assert.Contains(two, piece => Math.Abs(piece.Volume - 1000000.0) < 1E-3);
        }

        [Fact]
        public void EachOpeningGoesWithThePiecesItReaches()
        {
            var hole = Box(10, 40, -1, 20, 60, 31);
            GeoSolid3 two = Together(Box(0, 0, 0, 30, 100, 30), Box(70, 0, 0, 100, 100, 30)).WithOpenings(new[] { hole });

            var pieces = Shells3.Split(two, Loose);

            Assert.Equal(2, pieces.Count);
            Assert.Single(pieces, piece => piece.Openings.Count == 1);
            Assert.Single(pieces, piece => piece.Openings.Count == 0);
        }
    }
}
