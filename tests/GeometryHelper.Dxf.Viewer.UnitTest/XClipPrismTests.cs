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
    /// A clip made in a system tilted out of the plan, over a line or a curve of the block that does not lie flat, as AutoCAD
    /// 2021 draws it (PNGOUT in plan): the line or curve is cut by the boundary's prism, where its path leaves the boundary
    /// seen along the clip's normal, and not by the ring the prism makes in the plane of the block.
    /// </summary>
    public class XClipPrismTests
    {
        private static readonly double Cos30 = Math.Cos(Math.PI / 6.0);

        /// <summary>The system of the world turned 30 degrees about X: its y axis rises as it goes, to (0, cos 30, 1/2).</summary>
        private static readonly GeoTransform3 Tilted = CadXClips.WorldToClip(
            new CadUcs(new GeoPoint3(0, 0, 0), GeoVector3.XAxis, new GeoVector3(0.0, Cos30, 0.5))
        );

        /// <summary>The block GHP the caller fills, inserted at the origin and clipped to the rectangle (-50, -40) to (250, 40) of the tilted system.</summary>
        private static CadDocument Clipped(Action<DxfBlockTableRecord> fill, out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHP" };
            database.BlockTable.Add(block);
            fill(block);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            insert.ClipTo(new[] { new GeoPoint2(-50, -40), new GeoPoint2(250, 40) }, Tilted);
            handle = insert.Handle;
            return CadDocument.From(database, "xclipprism.dxf");
        }

        private static List<DisplayBox> Drawn(CadDocument document, string handle) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .Select(i => i.VisibleBounds)
                .OrderBy(b => b.MinY)
                .ThenBy(b => b.MinX)
                .ToList();

        /// <summary>How far above the boundary's plane, along the tilted y axis, a point stands: y cos 30 + z / 2.</summary>
        private static double Across(double y, double z) => y * Cos30 + z * 0.5;

        [Fact]
        public void ALineRisingAsItGoes_IsCutWhereItLeavesThePrism()
        {
            // Probe (plan, red): the line from (50, -100, 0) to (50, 100, 60) shows from y -54.2 to 24.5, where its height across
            // the boundary, -86.60 + 203.21 t, is -40 and 40; the ring of the plane at height 0 would give -46.19 to 46.19.
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfLine(new GeoPoint3(50, -100, 0), new GeoPoint3(50, 100, 60))),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            double start = Across(-100, 0);
            double slope = Across(200, 60);
            Assert.Single(boxes);
            Assert.Equal(-100 + 200 * (-40 - start) / slope, boxes[0].MinY, 3);
            Assert.Equal(-100 + 200 * (40 - start) / slope, boxes[0].MaxY, 3);
            Assert.Equal(-54.1, boxes[0].MinY, 1);
            Assert.Equal(24.6, boxes[0].MaxY, 1);
        }

        [Fact]
        public void ALineLyingFlat_IsStillShownInTheRingOfItsHeight()
        {
            // Probe (green): the flat line at height 0 from y -100 to 100 shows from -46.19 to 46.19, the ring of that plane.
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfLine(new GeoPoint3(100, -100, 0), new GeoPoint3(100, 100, 0))),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Single(boxes);
            Assert.Equal(-40.0 / Cos30, boxes[0].MinY, 3);
            Assert.Equal(40.0 / Cos30, boxes[0].MaxY, 3);
        }

        [Fact]
        public void ACircleStandingOnItsEdge_IsCutWhereItLeavesThePrism()
        {
            // The circle of radius 30 about (50, 0, 40) in the plane of Y and Z: its height across the boundary is
            // 20 + 30 cos(a - 30 degrees), over 40 where cos(a - 30) is over 2/3, which takes away the arc from -18.19 to
            // 78.19 degrees: what is left runs from y 6.11 down to -30 and back to 28.50, the y of 30 cos(a) at its two ends.
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfCircle(new GeoPoint3(50, 0, 40), GeoVector3.XAxis, 30)),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Single(boxes);
            Assert.Equal(-30.0, boxes[0].MinY, 3);
            Assert.Equal(30.0 * Math.Cos(Math.Acos(2.0 / 3.0) - Math.PI / 6.0), boxes[0].MaxY, 3);
        }

        [Fact]
        public void A3DPolyline_IsCutWhereItLeavesThePrism_ThroughItsVertex()
        {
            // The polyline (50, -100, 0), (50, 0, 40), (50, 100, 0) rises over the boundary's plane from -86.6 to 20 and on
            // to 86.6: the piece inside runs from where it passes -40, on the first segment, to where it passes 40, on the
            // second; found here by walking it finely.
            var polyline = new DxfPolyline3d(
                DxfPoly3dType.SimplePoly,
                new[] { new GeoPoint3(50, -100, 0), new GeoPoint3(50, 0, 40), new GeoPoint3(50, 100, 0) },
                false
            );
            double least = double.MaxValue;
            double most = double.MinValue;

            for (int i = 0; i <= 200000; i++)
            {
                double t = 2.0 * i / 200000;
                double y = t <= 1.0 ? -100 + 100 * t : 100 * (t - 1);
                double z = t <= 1.0 ? 40 * t : 40 * (2 - t);

                if (Math.Abs(Across(y, z)) <= 40.0)
                {
                    least = Math.Min(least, y);
                    most = Math.Max(most, y);
                }
            }

            CadDocument document = Clipped(block => block.AppendEntity(polyline), out string handle);

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Single(boxes);
            Assert.Equal(least, boxes[0].MinY, 2);
            Assert.Equal(most, boxes[0].MaxY, 2);
        }
    }
}
