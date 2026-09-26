using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Cutting a body's openings into it, so that its faces are where its material ends.
    /// </summary>
    /// <remarks>
    /// A body keeps its openings as whole bodies subtracted from it, so a plate with a bolt hole has a top face
    /// that is a whole square. The claim here is that the cut body is the same material with no openings: the
    /// test that earns its keep is the one holding its volume to <see cref="GeoSolid3.GetNetVolume()"/>, which
    /// works the same thing out by a different road — one subtraction per opening.
    /// </remarks>
    public class CutOpeningsTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>A 100 x 100 x 20 plate with a 20 x 20 bolt hole drawn overshooting both faces, as a
        /// through-hole usually is.</summary>
        private static GeoSolid3 Plate() => Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });

        [Fact]
        public void APlateWithABoltHoleComesBackAsTheMaterialAlone()
        {
            GeoSolid3 plate = Plate();

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material));

            Assert.Empty(material.Openings);
            Assert.Equal(100.0 * 100 * 20 - 20.0 * 20 * 20, material.Volume, 6);

            // Two roads to the same material: one subtraction per opening, and one cut of them all.
            Assert.Equal(plate.GetNetVolume(), material.Volume, 6);

            // The cheap property takes the opening off whole, overshoot and all, which is exactly why it is
            // not the one to compare against.
            Assert.Equal(100.0 * 100 * 20 - 20.0 * 20 * 22, plate.NetVolume, 6);
        }

        [Fact]
        public void TheCutBodyHasTheHoleWallsAndNoFaceOverTheHole()
        {
            Assert.True(Plate().TryCutOpenings(out GeoSolid3 material));

            // A square frame: top and bottom each with a hole in them, four outer sides and four hole walls.
            Assert.Equal(10, material.Faces.Count);
            Assert.Equal(2, material.Faces.Count(face => face.Holes.Count == 1));

            // Nothing of the material is left where the hole is, and all of it is left around it.
            Assert.Equal(PointLocation.OutSide, material.Locate(new GeoPoint3(50, 50, 10)));
            Assert.Equal(PointLocation.Inside, material.Locate(new GeoPoint3(20, 20, 10)));
            Assert.Equal(PointLocation.OnSide, material.Locate(new GeoPoint3(40, 50, 10)));

            // And it answers every point exactly as the body with the opening did.
            GeoSolid3 plate = Plate();

            foreach (GeoPoint3 point in new[]
                     {
                         new GeoPoint3(50, 50, 10), new GeoPoint3(20, 20, 10), new GeoPoint3(40, 50, 10),
                         new GeoPoint3(50, 50, 20), new GeoPoint3(10, 10, 20), new GeoPoint3(500, 0, 0),
                     })
            {
                Assert.Equal(plate.Locate(point), material.Locate(point));
            }
        }

        [Fact]
        public void TheSurfaceMeshStandsForMaterialAndTheFaceMeshDoesNot()
        {
            GeoSolid3 plate = Plate();

            GeoTriangle3[] faces = plate.Triangulate();
            GeoTriangle3[] surface = plate.TriangulateSurface();

            // The face mesh covers the whole top and bottom, hole and all; the surface mesh has the hole cut
            // out of both and the four walls added: 2 x 400 fewer, 4 x 20 x 20 more.
            double faceArea = faces.Sum(t => t.Area);
            double surfaceArea = surface.Sum(t => t.Area);

            Assert.Equal(2 * 100 * 100 + 4 * 100 * 20, faceArea, 6);
            Assert.Equal(faceArea - 2 * 20 * 20 + 4 * 20 * 20, surfaceArea, 6);

            // A body without openings meshes the same either way.
            GeoSolid3 solidBlock = Box(0, 0, 0, 100, 100, 20);

            Assert.Equal(solidBlock.Triangulate().Sum(t => t.Area), solidBlock.TriangulateSurface().Sum(t => t.Area), 9);
        }

        [Fact]
        public void ABodyWithoutOpeningsIsHandedBackAsItIs()
        {
            GeoSolid3 block = Box(0, 0, 0, 100, 100, 20);

            Assert.True(block.TryCutOpenings(out GeoSolid3 same));
            Assert.Same(block, same);
        }

        [Fact]
        public void AnOpeningThatTakesAllTheMaterialLeavesNothing()
        {
            GeoSolid3 swallowed = Box(0, 0, 0, 10, 10, 10).WithOpenings(new[] { Box(-5, -5, -5, 15, 15, 15) });

            Assert.False(swallowed.TryCutOpenings(out GeoSolid3 nothing));
            Assert.Null(nothing);
            Assert.Empty(swallowed.TriangulateSurface());
        }

        [Fact]
        public void ManyHolesStayCheapBecauseEachOnlyCutsNearItself()
        {
            // Twenty holes in a row along a long plate. Sliced by every hole's planes across the whole plate
            // this would be a grid; cut locally it grows with the number of holes.
            var holes = Enumerable.Range(0, 20)
                .Select(i => Box(20 + i * 50, 40, -1, 40 + i * 50, 60, 21))
                .ToArray();

            GeoSolid3 plate = Box(0, 0, 0, 1020, 100, 20).WithOpenings(holes);

            var watch = Stopwatch.StartNew();
            Assert.True(plate.TryCutOpenings(out GeoSolid3 material));
            watch.Stop();

            Assert.Equal(1020.0 * 100 * 20 - 20 * (20.0 * 20 * 20), material.Volume, 3);
            Assert.Equal(plate.GetNetVolume(), material.Volume, 3);

            // Generous on purpose: this is a guard against the grid, not a benchmark.
            Assert.True(watch.ElapsedMilliseconds < 20000, $"took {watch.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void TwoOpeningsThatOverlapAreOneRegionOfNoMaterial()
        {
            // Two holes overlapping each other: the cheap property counts the overlap twice, the cut does not.
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[]
            {
                Box(30, 40, -1, 60, 60, 21),
                Box(50, 40, -1, 80, 60, 21),
            });

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material));

            // The two together cover 30..80 by 40..60 through the plate.
            Assert.Equal(100.0 * 100 * 20 - 50.0 * 20 * 20, material.Volume, 6);
            Assert.Equal(plate.GetNetVolume(), material.Volume, 6);
        }

        [Fact]
        public void AnOpeningWithinTheBodyLeavesACavity()
        {
            // A hollow block: an opening wholly inside it, touching no face.
            GeoSolid3 hollow = Box(0, 0, 0, 100, 100, 100).WithOpenings(new[] { Box(25, 25, 25, 75, 75, 75) });

            Assert.True(hollow.TryCutOpenings(out GeoSolid3 material));

            Assert.Equal(100.0 * 100 * 100 - 50.0 * 50 * 50, material.Volume, 6);
            Assert.Equal(PointLocation.OutSide, material.Locate(new GeoPoint3(50, 50, 50)));
            Assert.Equal(PointLocation.Inside, material.Locate(new GeoPoint3(10, 10, 10)));
        }

        [Fact]
        public void EveryWayInTakesAToleranceAndNothingIsAskedOfNothing()
        {
            GeoSolid3 plate = Plate();

            Assert.True(plate.TryCutOpenings(out GeoSolid3 a));
            Assert.True(plate.TryCutOpenings(out GeoSolid3 b, Tolerance.Global));
            Assert.Equal(a.Volume, b.Volume, 9);

            Assert.Equal(plate.TriangulateSurface().Length, plate.TriangulateSurface(Tolerance.Global).Length);

            Assert.Throws<ArgumentNullException>(() => Boolean3.TryCutOpenings(null, out _));
        }
    }
}
