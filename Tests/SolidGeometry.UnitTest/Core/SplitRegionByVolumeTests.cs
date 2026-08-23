using System;
using System.Collections.Generic;
using SolidGeometry;
using SolidGeometry.Core;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers cutting a plate against a body: which part of it is embedded and which stands clear.
    /// </summary>
    public class SplitRegionByVolumeTests
    {
        private static GeoSolid3 MakeBoxSolid(GeoPoint3 min, GeoPoint3 max) =>
            new GeoAabb3(min, max).ToObb().ToSolid();

        private static double TotalArea(GeoPolygon3[] pieces)
        {
            double total = 0.0;

            foreach (GeoPolygon3 piece in pieces)
            {
                total += piece.Area;
            }

            return total;
        }

        /// <summary>
        /// A 20 by 20 plate lying in the plane z = 5, spanning -5 to 15 on both axes.
        /// </summary>
        private static GeoPolygon3 MakePlate() => new GeoPolygon3(
            new GeoPoint3(-5, -5, 5),
            new GeoPoint3(15, -5, 5),
            new GeoPoint3(15, 15, 5),
            new GeoPoint3(-5, 15, 5));

        [Fact]
        public void APlateThroughABoxIsDividedAtItsWalls()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPolygon3 plate = MakePlate();

            Assert.True(plate.TrySplitBy(cube, out GeoPolygon3[] inside, out GeoPolygon3[] outside));

            // The embedded part is the 10 by 10 footprint of the box.
            Assert.Equal(100.0, TotalArea(inside), 6);
            Assert.Equal(400.0 - 100.0, TotalArea(outside), 6);
        }

        [Fact]
        public void ThePiecesCoverTheSubjectExactly()
        {
            GeoSolid3 cube = MakeBoxSolid(new GeoPoint3(2, 3, 0), new GeoPoint3(9, 11, 10));
            GeoPolygon3 plate = MakePlate();

            Assert.True(plate.TrySplitBy(cube, out GeoPolygon3[] inside, out GeoPolygon3[] outside));

            Assert.Equal(plate.Area, TotalArea(inside) + TotalArea(outside), 6);
            Assert.Equal(7.0 * 8.0, TotalArea(inside), 6);
        }

        [Fact]
        public void EveryPieceLandsOnTheSideItsInteriorBelongsTo()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPolygon3 plate = MakePlate();

            Assert.True(plate.TrySplitBy(cube, out GeoPolygon3[] inside, out GeoPolygon3[] outside));

            foreach (GeoPolygon3 piece in inside)
            {
                Assert.True(cube.Contains(piece.Centroid));
            }

            foreach (GeoPolygon3 piece in outside)
            {
                Assert.False(cube.Contains(piece.Centroid));
            }
        }

        [Fact]
        public void EveryPieceKeepsTheOrientationOfTheSubject()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPolygon3 plate = MakePlate();

            Assert.True(plate.TrySplitBy(cube, out GeoPolygon3[] inside, out GeoPolygon3[] outside));

            foreach (GeoPolygon3 piece in inside)
            {
                Assert.True(piece.Normal.IsCodirectionalTo(plate.Normal));
            }

            foreach (GeoPolygon3 piece in outside)
            {
                Assert.True(piece.Normal.IsCodirectionalTo(plate.Normal));
            }
        }

        [Fact]
        public void APlateClearOfTheBodyIsNotCut()
        {
            GeoSolid3 farAway = MakeBoxSolid(new GeoPoint3(100, 100, 0), new GeoPoint3(110, 110, 10));
            GeoPolygon3 plate = MakePlate();

            Assert.False(plate.TrySplitBy(farAway, out GeoPolygon3[] inside, out GeoPolygon3[] outside));
            Assert.Empty(inside);
            Assert.Single(outside);
            Assert.Equal(plate.Area, outside[0].Area, 6);
        }

        [Fact]
        public void APlateWhollyInsideTheBodyIsNotCutEither()
        {
            GeoSolid3 big = MakeBoxSolid(new GeoPoint3(-50, -50, 0), new GeoPoint3(50, 50, 10));
            GeoPolygon3 plate = MakePlate();

            Assert.False(plate.TrySplitBy(big, out GeoPolygon3[] inside, out GeoPolygon3[] outside));
            Assert.Empty(outside);
            Assert.Equal(plate.Area, TotalArea(inside), 6);
        }

        [Fact]
        public void APlateMissingTheBodyInDepthIsNotCut()
        {
            // The body sits well below the plane the plate lies in.
            GeoSolid3 below = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 1));
            GeoPolygon3 plate = MakePlate();

            Assert.False(plate.TrySplitBy(below, out GeoPolygon3[] inside, out GeoPolygon3[] outside));
            Assert.Empty(inside);
            Assert.Single(outside);
        }

        [Fact]
        public void AConcaveBodyCutsThePlateToItsOwnShape()
        {
            // An L-shaped prism spanning the plate's plane.
            GeoSolid3 lShape = MakePrism(new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 4, 0),
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(4, 10, 0),
                new GeoPoint3(0, 10, 0)
            }, 10.0);

            GeoPolygon3 plate = MakePlate();

            Assert.True(plate.TrySplitBy(lShape, out GeoPolygon3[] inside, out GeoPolygon3[] outside));

            // The embedded part is the L footprint: 10 by 4 plus 4 by 6.
            Assert.Equal(64.0, TotalArea(inside), 6);
            Assert.Equal(plate.Area, TotalArea(inside) + TotalArea(outside), 6);
        }

        [Fact]
        public void ABodyWithAnOpeningLeavesTheVoidOutside()
        {
            GeoSolid3 slab = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoSolid3 duct = MakeBoxSolid(new GeoPoint3(3, 3, 0), new GeoPoint3(7, 7, 10));
            GeoSolid3 pierced = slab.WithOpenings(new[] { duct });

            GeoPolygon3 plate = MakePlate();

            Assert.True(plate.TrySplitBy(pierced, out GeoPolygon3[] inside, out GeoPolygon3[] outside));

            // The footprint of the material is the 10 by 10 block less the 4 by 4 shaft.
            Assert.Equal(100.0 - 16.0, TotalArea(inside), 6);
            Assert.Equal(plate.Area, TotalArea(inside) + TotalArea(outside), 6);
        }

        [Fact]
        public void TheStaticAndInstanceFormsAgree()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPolygon3 plate = MakePlate();

            bool fromStatic = Splition3.TrySplitBy(plate, cube, out GeoPolygon3[] staticInside, out _);
            bool fromInstance = plate.TrySplitBy(cube, out GeoPolygon3[] instanceInside, out _);

            Assert.Equal(fromStatic, fromInstance);
            Assert.Equal(TotalArea(staticInside), TotalArea(instanceInside), 6);
        }

        [Fact]
        public void NullArgumentsAreRejected()
        {
            GeoPolygon3 plate = MakePlate();

            Assert.Throws<ArgumentNullException>(() => plate.TrySplitBy((GeoSolid3)null, out GeoPolygon3[] _, out GeoPolygon3[] _));
        }

        /// <summary>
        /// Builds a closed prism from a profile walked counter-clockwise seen from +Z.
        /// </summary>
        private static GeoSolid3 MakePrism(GeoPoint3[] profile, double height)
        {
            GeoPoint3[] top = new GeoPoint3[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                top[i] = profile[i].Add(new GeoVector3(0, 0, height));
            }

            List<GeoFace3> faces = new List<GeoFace3>();

            GeoPoint3[] bottomReversed = new GeoPoint3[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                bottomReversed[i] = profile[profile.Length - 1 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < profile.Length; i++)
            {
                int next = (i + 1) % profile.Length;
                faces.Add(new GeoFace3(new GeoPolygon3(profile[i], profile[next], top[next], top[i])));
            }

            return new GeoSolid3(faces);
        }
    }
}
