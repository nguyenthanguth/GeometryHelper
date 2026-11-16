using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// XCLIP's Clipdepth on an array of blocks (MINSERT) inside the clipped block, as AutoCAD 2021 draws it (PNGOUT in plan): the
    /// planes go through the placing of the array and the shift of each cell, as through that of a single INSERT.
    /// </summary>
    public class XClipNestedArrayTests
    {
        [Fact]
        public void AnArrayInsideTheBlock_IsCutByThePlanes_InEachOfItsCells()
        {
            // Probe (plan, red): GHB holds a line rising from (0, 0, 0) to (100, 0, 100); GHA holds an array of it at (0, 0, 5),
            // scaled by 2, turned 90 degrees, of 2 columns 150 apart and 2 rows 60 apart, so that the line of each cell runs
            // along y from the cell's corner, at x 0 and -60, y 0 and 150, rising 2 for each 1 of its length; GHA is inserted at
            // the origin with the planes front 20 and back -10, and each line is left for its first 15 (height 5 + 2 x is 20 at
            // x 7.5 of the block): AutoCAD 2021 showed y 0 to 15.2 and 149.4 to 165.2 on both.
            var database = new DxfDatabase("AC1027");
            var inner = new DxfBlockTableRecord { Name = "GHB" };
            database.BlockTable.Add(inner);
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 100)));
            var outer = new DxfBlockTableRecord { Name = "GHA" };
            database.BlockTable.Add(outer);
            var array = new DxfMInsertBlock(new GeoPoint3(0, 0, 5), inner, 2, 2, 150, 60)
            {
                ScaleFactors = new GeoVector3(2, 2, 2),
                Rotation = Math.PI / 2.0,
            };
            outer.AppendEntity(array);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), outer);
            database.ModelSpace.AppendEntity(insert);
            string handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipnestedarray.dxf");
            CadCommandSession session = Session(document);
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", P(-200, -200), P(300, 300));
            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "Clipdepth",
                "Distance",
                20.0.ToString(CultureInfo.InvariantCulture),
                "Distance",
                (-10.0).ToString(CultureInfo.InvariantCulture)
            );

            List<DisplayBox> boxes = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .Select(i => i.Bounds)
                .OrderBy(b => b.MinX)
                .ThenBy(b => b.MinY)
                .ToList();

            Assert.Equal(4, boxes.Count);
            Assert.Equal(-60.0, boxes[0].MinX, 3);
            Assert.Equal(0.0, boxes[0].MinY, 3);
            Assert.Equal(15.0, boxes[0].MaxY, 3);
            Assert.Equal(-60.0, boxes[1].MinX, 3);
            Assert.Equal(150.0, boxes[1].MinY, 3);
            Assert.Equal(165.0, boxes[1].MaxY, 3);
            Assert.Equal(0.0, boxes[2].MinX, 3);
            Assert.Equal(0.0, boxes[2].MinY, 3);
            Assert.Equal(15.0, boxes[2].MaxY, 3);
            Assert.Equal(0.0, boxes[3].MinX, 3);
            Assert.Equal(150.0, boxes[3].MinY, 3);
            Assert.Equal(165.0, boxes[3].MaxY, 3);
        }

        /// <summary>
        /// A block GHB of a line along x from 0 to 100 at height 0, put in GHA as an array of 1 column by 3 rows 40 apart whose
        /// plane is tilted 45 degrees about X (its normal (0, -s, s), its y axis rising to (0, s, s)), GHA inserted at the origin.
        /// </summary>
        private static CadDocument TiltedArray(out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var inner = new DxfBlockTableRecord { Name = "GHB" };
            database.BlockTable.Add(inner);
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            var outer = new DxfBlockTableRecord { Name = "GHA" };
            database.BlockTable.Add(outer);
            double s = Math.Sqrt(0.5);
            outer.AppendEntity(
                new DxfMInsertBlock(new GeoPoint3(0, 0, 0), inner, 1, 3, 0, 40) { Normal = new GeoVector3(0, -s, s) }
            );
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), outer);
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(database, "xclipnestedtilted.dxf");
        }

        private static List<double> RowsOf(CadDocument document, string handle) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .Select(i => i.Bounds.MinY)
                .OrderBy(y => y)
                .ToList();

        [Fact]
        public void AnArrayOfATiltedPlane_StandsItsRowsApartAlongTheTiltedAxis_InThePlan()
        {
            // Probe (plan, red): the rows of the tilted array stand at y 0.2, 28.1 and 56.5, 40 cos 45 = 28.28 apart where the
            // plane of the array shows them, not 40.
            CadDocument document = TiltedArray(out string handle);

            List<double> rows = RowsOf(document, handle);

            Assert.Equal(3, rows.Count);
            Assert.Equal(0.0, rows[0], 3);
            Assert.Equal(40.0 * Math.Sqrt(0.5), rows[1], 3);
            Assert.Equal(80.0 * Math.Sqrt(0.5), rows[2], 3);
        }

        [Fact]
        public void AnArrayOfATiltedPlane_IsCutByThePlanes_AtTheHeightOfEachRow()
        {
            // Probe (plan, red): with the planes front 20 and back -10 only the first row is left (the rows stand at height 0,
            // 28.28 and 56.57, the others above the front plane).
            CadDocument document = TiltedArray(out string handle);
            CadCommandSession session = Session(document);
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", P(-200, -200), P(300, 300));
            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "Clipdepth",
                "Distance",
                20.0.ToString(CultureInfo.InvariantCulture),
                "Distance",
                (-10.0).ToString(CultureInfo.InvariantCulture)
            );

            List<double> rows = RowsOf(document, handle);

            Assert.Single(rows);
            Assert.Equal(0.0, rows[0], 3);
        }
    }
}
