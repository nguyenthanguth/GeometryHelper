using System;
using SolidGeometry;
using SolidGeometry.Core;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers cutting a plate along a line marked on it.
    /// </summary>
    public class SplitByCutLineTests
    {
        /// <summary>
        /// A 10 by 10 plate in the XY plane, first vertex at the origin.
        /// </summary>
        private static GeoPolygon3 MakePlate() => new GeoPolygon3(
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(10, 0, 0),
            new GeoPoint3(10, 10, 0),
            new GeoPoint3(0, 10, 0));

        [Fact]
        public void AStraightCutAcrossThePlateHalvesIt()
        {
            GeoPolyline3 cut = new GeoPolyline3(new GeoPoint3(5, 0, 0), new GeoPoint3(5, 10, 0));

            Assert.True(MakePlate().TrySplitBy(cut, out GeoPolygon3[] pieces));

            Assert.Equal(2, pieces.Length);
            Assert.Equal(50.0, pieces[0].Area, 6);
            Assert.Equal(50.0, pieces[1].Area, 6);
        }

        [Fact]
        public void ThePiecesAddUpToTheOriginal()
        {
            GeoPolygon3 plate = MakePlate();
            GeoPolyline3 cut = new GeoPolyline3(new GeoPoint3(0, 3, 0), new GeoPoint3(10, 7, 0));

            Assert.True(plate.TrySplitBy(cut, out GeoPolygon3[] pieces));

            Assert.Equal(plate.Area, pieces[0].Area + pieces[1].Area, 6);
        }

        [Fact]
        public void ABentCutIsFollowedRoundItsCorners()
        {
            GeoPolygon3 plate = MakePlate();

            // In at the bottom, along to the right, out at the top.
            GeoPolyline3 cut = new GeoPolyline3(
                new GeoPoint3(2, 0, 0),
                new GeoPoint3(2, 5, 0),
                new GeoPoint3(8, 5, 0),
                new GeoPoint3(8, 10, 0));

            Assert.True(plate.TrySplitBy(cut, out GeoPolygon3[] pieces));

            Assert.Equal(2, pieces.Length);
            Assert.Equal(plate.Area, pieces[0].Area + pieces[1].Area, 6);

            // Left of the cut: a 2 by 5 strip plus an 8 by 5 block above it.
            double smaller = Math.Min(pieces[0].Area, pieces[1].Area);
            Assert.Equal(2 * 5 + 8 * 5, smaller, 6);
        }

        [Fact]
        public void BothPiecesKeepTheOrientationOfTheSubject()
        {
            GeoPolygon3 plate = MakePlate();
            GeoPolyline3 cut = new GeoPolyline3(new GeoPoint3(5, 0, 0), new GeoPoint3(5, 10, 0));

            Assert.True(plate.TrySplitBy(cut, out GeoPolygon3[] pieces));

            foreach (GeoPolygon3 piece in pieces)
            {
                Assert.True(piece.Normal.IsCodirectionalTo(plate.Normal));
            }
        }

        [Fact]
        public void ACutStartingAtACornerStillWorks()
        {
            GeoPolygon3 plate = MakePlate();
            GeoPolyline3 diagonal = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0));

            Assert.True(plate.TrySplitBy(diagonal, out GeoPolygon3[] pieces));

            Assert.Equal(2, pieces.Length);
            Assert.Equal(50.0, pieces[0].Area, 6);
            Assert.Equal(50.0, pieces[1].Area, 6);
        }

        [Fact]
        public void ACutThatDoesNotReachTheOutlineIsRefused()
        {
            GeoPolygon3 plate = MakePlate();

            // Stops short of the far edge, so it does not separate anything.
            GeoPolyline3 stub = new GeoPolyline3(new GeoPoint3(5, 0, 0), new GeoPoint3(5, 6, 0));

            Assert.False(plate.TrySplitBy(stub, out GeoPolygon3[] pieces));
            Assert.Single(pieces);
            Assert.Equal(plate, pieces[0]);
        }

        [Fact]
        public void ACutLeavingThePlateIsRefused()
        {
            GeoPolygon3 plate = MakePlate();

            // Out through the side and back in again: this would cut the plate into more than two.
            GeoPolyline3 wandering = new GeoPolyline3(
                new GeoPoint3(2, 0, 0),
                new GeoPoint3(-5, 5, 0),
                new GeoPoint3(8, 10, 0));

            Assert.False(plate.TrySplitBy(wandering, out GeoPolygon3[] pieces));
            Assert.Single(pieces);
        }

        [Fact]
        public void ACutOffThePlaneIsRefused()
        {
            GeoPolygon3 plate = MakePlate();
            GeoPolyline3 floating = new GeoPolyline3(new GeoPoint3(5, 0, 3), new GeoPoint3(5, 10, 3));

            Assert.False(plate.TrySplitBy(floating, out _));
        }

        [Fact]
        public void ACutAlongAnEdgeSeparatesNothing()
        {
            GeoPolygon3 plate = MakePlate();
            GeoPolyline3 alongEdge = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            // Both ends are on the outline and the chain stays on it, so one side has no area.
            Assert.False(plate.TrySplitBy(alongEdge, out GeoPolygon3[] pieces));
            Assert.Single(pieces);
        }

        [Fact]
        public void AConcavePlateIsCutToo()
        {
            GeoPolygon3 lShape = new GeoPolygon3(
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 4, 0),
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(4, 10, 0),
                new GeoPoint3(0, 10, 0));

            // Straight across the foot of the L.
            GeoPolyline3 cut = new GeoPolyline3(new GeoPoint3(0, 2, 0), new GeoPoint3(10, 2, 0));

            Assert.True(lShape.TrySplitBy(cut, out GeoPolygon3[] pieces));

            Assert.Equal(2, pieces.Length);
            Assert.Equal(lShape.Area, pieces[0].Area + pieces[1].Area, 6);

            // Below the cut is a plain 10 by 2 strip.
            double smaller = Math.Min(pieces[0].Area, pieces[1].Area);
            Assert.Equal(20.0, smaller, 6);
        }

        [Fact]
        public void TheStaticAndInstanceFormsAgree()
        {
            GeoPolygon3 plate = MakePlate();
            GeoPolyline3 cut = new GeoPolyline3(new GeoPoint3(5, 0, 0), new GeoPoint3(5, 10, 0));

            bool fromStatic = Splition3.TrySplitBy(plate, cut, out GeoPolygon3[] staticPieces);
            bool fromInstance = plate.TrySplitBy(cut, out GeoPolygon3[] instancePieces);

            Assert.Equal(fromStatic, fromInstance);
            Assert.Equal(staticPieces.Length, instancePieces.Length);
        }

        [Fact]
        public void NullArgumentsAreRejected()
        {
            GeoPolygon3 plate = MakePlate();

            Assert.Throws<ArgumentNullException>(() => plate.TrySplitBy((GeoPolyline3)null, out GeoPolygon3[] _));
            Assert.Throws<ArgumentNullException>(() => Splition3.TrySplitBy(
                (GeoPolygon3)null, new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)), out _));
        }
    }
}
