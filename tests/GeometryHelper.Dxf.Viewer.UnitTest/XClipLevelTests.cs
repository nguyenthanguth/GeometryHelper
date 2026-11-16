using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A clip made in a system tilted out of the plan, over a block whose entities stand at other heights than 0, as AutoCAD
    /// 2021 draws it (PNGOUT in plan of a grid at height 50 under the boundary that showed a grid at height 0 from y 73.09 to
    /// 130.83): each entity is shown where the boundary's prism meets the plane at its own height.
    /// </summary>
    public class XClipLevelTests
    {
        private static readonly double Cos30 = Math.Cos(Math.PI / 6.0);

        private static readonly GeoTransform3 Tilted = CadXClips.WorldToClip(
            new CadUcs(new GeoPoint3(100, 50, 20), GeoVector3.XAxis, new GeoVector3(0.0, Cos30, 0.5))
        );

        /// <summary>
        /// A block GHL of three lines along x from 0 to 300, one at height 0, one at height 50 and one rising from 0 to 50,
        /// inserted at the origin and clipped to the rectangle (10, 10) to (90, 60) of the tilted system.
        /// </summary>
        private static CadDocument Clipped(out string handle, GeoPoint3 origin = default)
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHL", Origin = origin };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0)));
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 10, 50), new GeoPoint3(300, 10, 50)));
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 20, 0), new GeoPoint3(300, 20, 50)));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            insert.ClipTo(new[] { new GeoPoint2(10, 10), new GeoPoint2(90, 60) }, Tilted);
            handle = insert.Handle;
            return CadDocument.From(database, "xcliplevels.dxf");
        }

        /// <summary>The boxes the three lines are shown in, by their y.</summary>
        private static List<DisplayBox> Regions(CadDocument document, string handle) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .OrderBy(i => i.Bounds.MinY)
                .Select(i => i.BlockClip.Bounds)
                .ToList();

        [Fact]
        public void AnEntityAtAnotherHeight_IsShownWhereThePrismMeetsThatHeight()
        {
            CadDocument document = Clipped(out string handle);

            List<DisplayBox> regions = Regions(document, handle);

            // The line at height 0: the lines along the boundary's normal through (110, 58.66, 25) and (110, 101.96, 50) meet
            // it at y 73.09 and 130.83 (as before); the line at height 50 at y 58.66 - 25 * 0.5 / cos 30 = 44.23 and 101.96.
            Assert.Equal(3, regions.Count);
            Assert.Equal(50 + 10 * Cos30 + 25 * 0.5 / Cos30, regions[0].MinY, 6);
            Assert.Equal(50 + 60 * Cos30 + 50 * 0.5 / Cos30, regions[0].MaxY, 6);
            Assert.Equal(50 + 10 * Cos30 - 25 * 0.5 / Cos30, regions[1].MinY, 6);
            Assert.Equal(50 + 60 * Cos30, regions[1].MaxY, 6);
            Assert.Equal(110.0, regions[1].MinX, 6);
            Assert.Equal(190.0, regions[1].MaxX, 6);
        }

        [Fact]
        public void AnEntityThatDoesNotLieFlat_IsShownWhereThePrismMeetsTheBlocksPlane()
        {
            CadDocument document = Clipped(out string handle);

            List<DisplayBox> regions = Regions(document, handle);

            // The line rising from 0 to 50 keeps the ring of height 0: the display has no height to cut it by.
            Assert.Equal(regions[0].MinY, regions[2].MinY, 6);
            Assert.Equal(regions[0].MaxY, regions[2].MaxY, 6);
        }

        [Fact]
        public void TheHeightsAreTakenFromTheOriginOfTheBlock_WhereItHasOne()
        {
            // The same block with its origin at (0, 0, 50): its line at 50 stands in the block's own plane, so that it is shown
            // where the plane of the block is met, as the line at 0 was before.
            CadDocument document = Clipped(out string handle, new GeoPoint3(0, 0, 50));

            List<DisplayBox> regions = Regions(document, handle);

            Assert.Equal(3, regions.Count);
            Assert.Equal(regions[0].MinY - 2 * 25 * 0.5 / Cos30, regions[1].MinY, 6);
        }
    }
}
