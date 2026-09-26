using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A point against a body with openings: where it is, how far it is, and the nearest place of the
    /// material to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A body keeps an opening as a whole body subtracted from it, so its faces run straight across every hole.
    /// Two things were wrong because of it. <c>Locate</c> asked the faces before the openings, so a point in
    /// the middle of a bolt hole at the level of the top face was called boundary. And the nearest point was
    /// sought on the faces and on each opening in turn, which cannot find the rim of a hole at all — it is a
    /// point of neither alone — so a point below a duct bored through a slab came out nought away.
    /// </para>
    /// <para>
    /// The fixed answers are checked against arithmetic done by hand, not against the library.
    /// </para>
    /// </remarks>
    public class MaterialPointTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>A 100 x 100 x 20 plate with a 20 x 20 hole drawn overshooting both faces by one.</summary>
        private static GeoSolid3 Plate() => Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });

        [Fact]
        public void APointOnAFaceButInAHoleIsInEmptySpace()
        {
            GeoSolid3 plate = Plate();

            // On the top face as drawn, and in the middle of the hole: the hole wins.
            Assert.Equal(PointLocation.OutSide, plate.Locate(new GeoPoint3(50, 50, 20)));
            Assert.Equal(PointLocation.OutSide, plate.Locate(new GeoPoint3(50, 50, 0)));
            Assert.False(plate.Contains(new GeoPoint3(50, 50, 20)));

            // On the rim, where the face meets the wall, is where the material ends.
            Assert.Equal(PointLocation.OnSide, plate.Locate(new GeoPoint3(40, 50, 20)));
            Assert.Equal(PointLocation.OnSide, plate.Locate(new GeoPoint3(40, 50, 10)));

            // A wall of the hole standing out past the face, where the hole overshoots, bounds nothing.
            Assert.Equal(PointLocation.OutSide, plate.Locate(new GeoPoint3(40, 50, 20.5)));

            // The rest of the face, away from the hole, is boundary as it always was.
            Assert.Equal(PointLocation.OnSide, plate.Locate(new GeoPoint3(10, 10, 20)));
        }

        [Fact]
        public void APointBelowADuctIsMeasuredToTheRimOfItsMouth()
        {
            // A cube with a duct bored right through it, overshooting both faces by ten.
            GeoSolid3 pierced = Box(0, 0, 0, 100, 100, 100)
                .WithOpenings(new[] { Box(40, 40, -10, 60, 60, 110) });

            var below = new GeoPoint3(50, 50, -10);

            // Ten below the underside and ten in from the rim: the nearest material is the rim, root two times
            // ten away. The faces alone would say ten, and asking each part in turn said nought.
            double expected = Math.Sqrt(10 * 10 + 10 * 10);

            Assert.Equal(expected, pierced.DistanceTo(below), 6);
            Assert.Equal(expected, pierced.SignedDistanceTo(below), 6);
            Assert.Equal(PointLocation.OutSide, pierced.Locate(below));

            GeoPoint3 rim = pierced.GetClosestPointOnBoundary(below);

            Assert.Equal(0.0, rim.Z, 6);
            Assert.Equal(expected, rim.DistanceTo(below), 6);
            Assert.Equal(PointLocation.OnSide, pierced.Locate(rim));
        }

        [Fact]
        public void APointInAHoleIsMeasuredToTheWallAndNotToTheFaceAcrossIt()
        {
            GeoSolid3 plate = Plate();

            // Five below the top face as drawn, ten from every wall: the faces alone would say five.
            var inHole = new GeoPoint3(50, 50, 15);

            Assert.Equal(10.0, plate.DistanceTo(inHole), 6);
            Assert.Equal(10.0, plate.SignedDistanceTo(inHole), 6);
            Assert.Equal(10.0, plate.GetClosestPointOnBoundary(inHole).DistanceTo(inHole), 6);

            // Off centre, the nearest wall is the near one.
            var offCentre = new GeoPoint3(44, 50, 10);

            Assert.Equal(4.0, plate.DistanceTo(offCentre), 6);
            Assert.True(plate.GetClosestPointOnBoundary(offCentre).IsEqualTo(new GeoPoint3(40, 50, 10), Loose));
        }

        [Fact]
        public void APointInTheMaterialBesideAHoleIsMeasuredToTheWall()
        {
            GeoSolid3 plate = Plate();

            // In the plate, two from the wall of the hole and eight from the top face.
            var beside = new GeoPoint3(38, 50, 12);

            Assert.Equal(PointLocation.Inside, plate.Locate(beside));
            Assert.Equal(0.0, plate.DistanceTo(beside), 9);
            Assert.Equal(-2.0, plate.SignedDistanceTo(beside), 6);
        }

        [Fact]
        public void FarFromEveryOpeningNothingIsCutAndTheAnswerIsTheFaces()
        {
            // Twenty holes along a plate; a point off the far end is nowhere near any of them.
            var holes = new GeoSolid3[20];

            for (int i = 0; i < holes.Length; i++)
            {
                holes[i] = Box(20 + i * 50, 40, -1, 40 + i * 50, 60, 21);
            }

            GeoSolid3 plate = Box(0, 0, 0, 1020, 100, 20).WithOpenings(holes);
            var point = new GeoPoint3(-30, 50, 10);

            Assert.Equal(30.0, plate.DistanceTo(point), 6);
            Assert.Equal(30.0, plate.SignedDistanceTo(point), 6);

            // And a point beside one hole in the middle of them is answered by that hole's wall.
            var inTheTenth = new GeoPoint3(20 + 9 * 50 + 10, 50, 10);

            Assert.Equal(10.0, plate.DistanceTo(inTheTenth), 6);
        }
    }
}
