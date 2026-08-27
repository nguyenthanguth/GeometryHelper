using System.Collections.Generic;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers what happens to the openings of a solid when it is cut or combined. An opening is material
    /// that is not there, so losing one turns a duct back into concrete without anything to report it.
    /// </summary>
    public class OpeningsTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            GeoPoint3[] baseCcw =
            {
                new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y0, z0),
                new GeoPoint3(x1, y1, z0), new GeoPoint3(x0, y1, z0)
            };

            List<GeoFace3> faces = new List<GeoFace3>();
            GeoPoint3[] top = new GeoPoint3[4];
            GeoPoint3[] bottomReversed = new GeoPoint3[4];

            for (int i = 0; i < 4; i++)
            {
                top[i] = new GeoPoint3(baseCcw[i].X, baseCcw[i].Y, z1);
                bottomReversed[i] = baseCcw[3 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                faces.Add(new GeoFace3(new GeoPolygon3(baseCcw[i], baseCcw[j], top[j], top[i])));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A 10 x 10 x 2 slab with a 2 x 2 duct through it. The duct runs from z = -1 to z = 3, poking out
        /// at both ends, which is how a through-hole is drawn so that it clears the far face.
        /// </summary>
        private static GeoSolid3 SlabWithDuct()
        {
            return Box(0, 0, 0, 10, 10, 2).WithOpenings(new[] { Box(4, 4, -1, 6, 6, 3) });
        }

        private static readonly GeoPoint3 InsideTheDuct = new GeoPoint3(5, 5, 1);

        #region Splitting

        [Fact]
        public void SplitByPlane_CarriesTheOpeningIntoBothHalves()
        {
            GeoSolid3 slab = SlabWithDuct();
            GeoPlane3 halfway = new GeoPlane3(new GeoPoint3(0, 0, 1), GeoVector3.ZAxis);

            Assert.True(slab.TrySplitBy(halfway, out GeoSolid3 above, out GeoSolid3 below));

            Assert.Single(above.Openings);
            Assert.Single(below.Openings);
        }

        [Fact]
        public void SplitByPlane_LeavesTheDuctEmptyInBothHalves()
        {
            GeoSolid3 slab = SlabWithDuct();
            GeoPlane3 halfway = new GeoPlane3(new GeoPoint3(0, 0, 1), GeoVector3.ZAxis);

            Assert.True(slab.TrySplitBy(halfway, out GeoSolid3 above, out GeoSolid3 below));

            Assert.Equal(PointLocation.OutSide, above.Locate(new GeoPoint3(5, 5, 1.5)));
            Assert.Equal(PointLocation.OutSide, below.Locate(new GeoPoint3(5, 5, 0.5)));

            // The material beside the duct is still material.
            Assert.Equal(PointLocation.Inside, above.Locate(new GeoPoint3(1, 1, 1.5)));
            Assert.Equal(PointLocation.Inside, below.Locate(new GeoPoint3(1, 1, 0.5)));
        }

        [Fact]
        public void SplitByPlane_KeepsBothHalvesClosed()
        {
            GeoSolid3 slab = SlabWithDuct();
            GeoPlane3 halfway = new GeoPlane3(new GeoPoint3(0, 0, 1), GeoVector3.ZAxis);

            Assert.True(slab.TrySplitBy(halfway, out GeoSolid3 above, out GeoSolid3 below));

            Assert.True(above.IsClosed());
            Assert.True(below.IsClosed());
            Assert.Equal(slab.Volume, above.Volume + below.Volume, 9);
        }

        [Fact]
        public void SplitByPlane_GivesAnOpeningTheCutMissesToTheSideItSitsOn()
        {
            // The recess sits wholly in the upper half, so the cut does not touch it.
            GeoSolid3 slab = Box(0, 0, 0, 10, 10, 4).WithOpenings(new[] { Box(4, 4, 3, 6, 6, 5) });
            GeoPlane3 halfway = new GeoPlane3(new GeoPoint3(0, 0, 2), GeoVector3.ZAxis);

            Assert.True(slab.TrySplitBy(halfway, out GeoSolid3 above, out GeoSolid3 below));

            Assert.Single(above.Openings);
            Assert.Empty(below.Openings);

            Assert.Equal(PointLocation.OutSide, above.Locate(new GeoPoint3(5, 5, 3.5)));
            Assert.Equal(PointLocation.Inside, below.Locate(new GeoPoint3(5, 5, 1.0)));
        }

        [Fact]
        public void SplitByPlane_OnASolidWithoutOpenings_IsUnchanged()
        {
            GeoSolid3 slab = Box(0, 0, 0, 10, 10, 2);
            GeoPlane3 halfway = new GeoPlane3(new GeoPoint3(0, 0, 1), GeoVector3.ZAxis);

            Assert.True(slab.TrySplitBy(halfway, out GeoSolid3 above, out GeoSolid3 below));

            Assert.Empty(above.Openings);
            Assert.Empty(below.Openings);
            Assert.Equal(100.0, above.Volume, 9);
            Assert.Equal(100.0, below.Volume, 9);
        }

        #endregion

        #region Boolean operations

        [Fact]
        public void Subtract_KeepsTheDuctOfTheSubject()
        {
            GeoSolid3 slab = SlabWithDuct();
            GeoSolid3 tool = Box(-1, -1, 0.5, 1, 1, 1.5);

            Assert.True(Boolean3.TrySubtract(slab, tool, out GeoSolid3 result));

            Assert.Equal(PointLocation.OutSide, result.Locate(InsideTheDuct));
            Assert.Equal(PointLocation.Inside, result.Locate(new GeoPoint3(8, 8, 1)));
        }

        [Fact]
        public void Subtract_CarvesTheDuctIntoTheGeometryRatherThanKeepingItAnOpening()
        {
            GeoSolid3 slab = SlabWithDuct();
            GeoSolid3 tool = Box(-1, -1, 0.5, 1, 1, 1.5);

            Assert.True(Boolean3.TrySubtract(slab, tool, out GeoSolid3 result));

            // The cavity is real geometry now, so the gross volume already accounts for it: the slab is
            // 200, the duct removes 8 and the tool removes 1.
            Assert.Empty(result.Openings);
            Assert.Equal(191.0, result.Volume, 9);
        }

        [Fact]
        public void Intersect_LeavesTheDuctOutOfTheSharedPart()
        {
            GeoSolid3 slab = SlabWithDuct();
            GeoSolid3 window = Box(3, 3, 0, 7, 7, 2);

            Assert.True(Boolean3.TryIntersect(slab, window, out GeoSolid3 result));

            Assert.Equal(PointLocation.OutSide, result.Locate(InsideTheDuct));

            // A 4 x 4 x 2 block of the slab, less the 2 x 2 x 2 the duct takes out of it.
            Assert.Equal(24.0, result.Volume, 9);
        }

        [Fact]
        public void Union_KeepsTheDuctWhereTheBodiesOverlap()
        {
            GeoSolid3 slab = SlabWithDuct();
            GeoSolid3 other = Box(8, 0, 0, 14, 10, 2);

            Assert.True(Boolean3.TryUnion(slab, other, out GeoSolid3 result));

            Assert.Equal(PointLocation.OutSide, result.Locate(InsideTheDuct));

            // 14 x 10 x 2 across the two bodies, less the duct.
            Assert.Equal(272.0, result.Volume, 9);
        }

        [Fact]
        public void Union_OfBodiesTooFarApartToMeet_KeepsBothSetsOfOpenings()
        {
            GeoSolid3 slab = SlabWithDuct();
            GeoSolid3 other = Box(100, 0, 0, 110, 10, 2).WithOpenings(new[] { Box(104, 4, -1, 106, 6, 3) });

            Assert.True(Boolean3.TryUnion(slab, other, out GeoSolid3 result));

            Assert.Equal(2, result.Openings.Count);
            Assert.Equal(PointLocation.OutSide, result.Locate(InsideTheDuct));
            Assert.Equal(PointLocation.OutSide, result.Locate(new GeoPoint3(105, 5, 1)));
        }

        [Fact]
        public void Subtract_OnSolidsWithoutOpenings_IsUnchanged()
        {
            GeoSolid3 slab = Box(0, 0, 0, 10, 10, 2);
            GeoSolid3 tool = Box(-1, -1, 0.5, 1, 1, 1.5);

            Assert.True(Boolean3.TrySubtract(slab, tool, out GeoSolid3 result));

            Assert.Equal(199.0, result.Volume, 9);
        }

        #endregion

        #region Net volume

        [Fact]
        public void NetVolume_TakesAProtrudingOpeningOffWhole()
        {
            // Documented behaviour: the opening is 2 x 2 x 4 and all of it is deducted, though only
            // 2 x 2 x 2 of it lies in the slab.
            Assert.Equal(184.0, SlabWithDuct().NetVolume, 9);
        }

        [Fact]
        public void GetNetVolume_ClipsAProtrudingOpeningToTheBody()
        {
            Assert.Equal(192.0, SlabWithDuct().GetNetVolume(), 9);
        }

        [Fact]
        public void GetNetVolume_AgreesWithNetVolume_WhenTheOpeningSitsInsideTheBody()
        {
            GeoSolid3 slab = Box(0, 0, 0, 10, 10, 4).WithOpenings(new[] { Box(4, 4, 1, 6, 6, 3) });

            Assert.Equal(392.0, slab.NetVolume, 9);
            Assert.Equal(392.0, slab.GetNetVolume(), 9);
        }

        [Fact]
        public void GetNetVolume_CountsOverlappingOpeningsOnce()
        {
            GeoSolid3 slab = Box(0, 0, 0, 10, 10, 4).WithOpenings(new[]
            {
                Box(2, 2, 1, 6, 6, 3),
                Box(4, 4, 1, 8, 8, 3)
            });

            // Two 4 x 4 x 2 openings sharing a 2 x 2 x 2 corner: 32 + 32 - 8 = 56 is really removed.
            Assert.Equal(400.0 - 56.0, slab.GetNetVolume(), 9);

            // The cheap sum double-counts the shared corner.
            Assert.Equal(400.0 - 64.0, slab.NetVolume, 9);
        }

        [Fact]
        public void GetNetVolume_WithoutOpenings_IsTheGrossVolume()
        {
            GeoSolid3 slab = Box(0, 0, 0, 10, 10, 2);

            Assert.Equal(slab.Volume, slab.GetNetVolume(), 9);
            Assert.Equal(200.0, slab.GetNetVolume(), 9);
        }

        #endregion
    }
}
