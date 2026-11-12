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
    /// A clipped block reference as the display list and the pick index hold it: its items inside the boundary alone, the frame
    /// of the boundary drawn over them while XCLIPFRAME is not 0, a pick, a window and a crossing meeting only what is shown,
    /// the clip going with the reference when it is moved, and each cell of an array clipped.
    /// </summary>
    public class XClipDisplayTests
    {
        /// <summary>
        /// A block GHB of a square 100 by 100 and a circle of radius 20 about (50, 50), its origin (10, 20), inserted at
        /// (1000, 500): in the world the square is (990, 480) to (1090, 580) and the circle is about (1040, 530).
        /// </summary>
        private static CadDocument WithReference(out CadCommandSession session, out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHB", Origin = new GeoPoint3(10, 20, 0) };
            database.BlockTable.Add(block);
            var square = new DxfPolyline();
            square.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            square.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            square.AddVertexAt(2, new GeoPoint2(100, 100), 0, 0, 0);
            square.AddVertexAt(3, new GeoPoint2(0, 100), 0, 0, 0);
            square.Closed = true;
            block.AppendEntity(square);
            block.AppendEntity(new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 20.0));
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclip.dxf");
            session = Session(document);
            return document;
        }

        private static void ClipToRectangle(CadCommandSession session, string handle, GeoPoint2 a, GeoPoint2 b) =>
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", a, b);

        private static List<DisplayItem2> Drawn(CadDocument document, string handle) =>
            document.GetDisplay(document.ModelSpace).Items.Where(i => i.Handle == handle).ToList();

        [Fact]
        public void AReferenceClipped_IsDrawnInsideItsBoundaryAlone()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            Assert.All(Drawn(document, handle), item => Assert.Null(item.BlockClip));

            // The corner of the block 40 by 30 in from its origin, which stands at the insertion point.
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));

            List<DisplayItem2> items = Drawn(document, handle);
            List<DisplayItem2> shown = items.Where(i => !i.IsClipFrame).ToList();
            Assert.Equal(2, shown.Count);
            Assert.All(shown, item => Assert.NotNull(item.BlockClip));
            DisplayClipRegion region = shown[0].BlockClip;
            Assert.Equal(4, region.Ring.Length / 2);
            Assert.Equal(1000.0, region.Bounds.MinX, 9);
            Assert.Equal(500.0, region.Bounds.MinY, 9);
            Assert.Equal(1040.0, region.Bounds.MaxX, 9);
            Assert.Equal(530.0, region.Bounds.MaxY, 9);
            Assert.True(region.Contains(1020, 515));
            Assert.False(region.Contains(1060, 515));
        }

        [Fact]
        public void TheBoxOfTheDisplay_IsTheBoxOfWhatIsShown()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));

            DisplayBox bounds = document.GetDisplay(document.ModelSpace).Bounds;

            Assert.Equal(1000.0, bounds.MinX, 6);
            Assert.Equal(500.0, bounds.MinY, 6);
            Assert.Equal(1040.0, bounds.MaxX, 6);
            Assert.Equal(530.0, bounds.MaxY, 6);
        }

        [Fact]
        public void TheFrameOfTheBoundary_IsDrawnOverIt_UntilXclipframeIsNought_AndKeptOutOfAPlotAtTwo()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));

            DisplayPolyline2 frame = Assert.IsType<DisplayPolyline2>(
                Assert.Single(Drawn(document, handle), i => i.IsClipFrame)
            );
            Assert.True(frame.Closed);
            Assert.Null(frame.BlockClip);
            Assert.Equal(4, frame.Count);
            Assert.False(frame.IsNotPlotted);

            Run(session, "XCLIPFRAME", "2");
            frame = Assert.IsType<DisplayPolyline2>(Assert.Single(Drawn(document, handle), i => i.IsClipFrame));
            Assert.True(frame.IsNotPlotted);

            Run(session, "XCLIPFRAME", "0");
            Assert.DoesNotContain(Drawn(document, handle), i => i.IsClipFrame);
            Assert.Equal(0, document.Database.XclipFrame);
        }

        [Fact]
        public void AClipSwitchedOff_ShowsTheWholeBlock_AndNoFrame()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));
            Run(session, "XCLIP", new[] { handle }, Enter, "OFF");

            List<DisplayItem2> items = Drawn(document, handle);

            Assert.Equal(2, items.Count);
            Assert.All(items, item => Assert.Null(item.BlockClip));
            Assert.DoesNotContain(items, i => i.IsClipFrame);
            Run(session, "XCLIP", new[] { handle }, Enter, "ON");
            Assert.Contains(Drawn(document, handle), i => i.IsClipFrame);
        }

        [Fact]
        public void TheClipGoesWithTheReference_WhenItIsMoved()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));

            Run(session, "MOVE", new[] { handle }, Enter, P(0, 0), P(70, 90));

            DisplayClipRegion region = Drawn(document, handle).First(i => !i.IsClipFrame).BlockClip;
            Assert.Equal(1070.0, region.Bounds.MinX, 9);
            Assert.Equal(590.0, region.Bounds.MinY, 9);
            Assert.Equal(1110.0, region.Bounds.MaxX, 9);
            Assert.Equal(620.0, region.Bounds.MaxY, 9);
        }

        [Fact]
        public void EveryCellOfAnArray_IsClippedByItsOwnPlace()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            var database = document.Database;
            var array = new DxfMInsertBlock(new GeoPoint3(2000, 500, 0), database.BlockTable["GHB"], 2, 1, 300, 0);
            database.ModelSpace.AppendEntity(array);
            ClipToRectangle(session, array.Handle, P(2000, 500), P(2040, 530));

            List<DisplayItem2> shown = Drawn(document, array.Handle).Where(i => !i.IsClipFrame).ToList();

            Assert.Equal(4, shown.Count);
            double[] lefts = shown.Select(i => i.BlockClip.Bounds.MinX).Distinct().OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 2000.0, 2300.0 }, lefts);
        }

        [Fact]
        public void APick_FindsTheReferenceWhereItIsShown_AndNotWhereItIsClippedAway()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            double r = 20.0 * Math.Sqrt(0.5);

            // The circle about (1040, 530): its point at 225 degrees is inside the clip, the one at 45 degrees outside it.
            Assert.Contains(handle, index.Pick(1040 - r, 530 - r, 1.0));
            Assert.DoesNotContain(handle, index.Pick(1040 + r, 530 + r, 1.0));

            // The edge of the square runs outside the clip: a pick of it finds nothing.
            Assert.DoesNotContain(handle, index.Pick(990, 540, 1.0));
        }

        [Fact]
        public void AWindowAndACrossing_MeetOnlyWhatIsShown()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));
            PickIndex index = document.GetPickIndex(document.ModelSpace);

            // A window round the clip alone takes the reference, whose whole block is far larger.
            Assert.Contains(handle, index.Window(new DisplayBox(995, 495, 1045, 535)));
            Assert.DoesNotContain(handle, index.Window(new DisplayBox(1005, 495, 1045, 535)));

            // A crossing over the circle outside the clip takes nothing; over the part inside, the reference.
            double r = 20.0 * Math.Sqrt(0.5);
            Assert.DoesNotContain(
                handle,
                index.Crossing(new DisplayBox(1040 + r - 1, 530 + r - 1, 1040 + r + 1, 530 + r + 1))
            );
            Assert.Contains(
                handle,
                index.Crossing(new DisplayBox(1040 - r - 1, 530 - r - 1, 1040 - r + 1, 530 - r + 1))
            );
        }

        [Fact]
        public void AnExternalReferenceClipped_ShowsInsideItsBoundaryAlone_AndIsPickedThere()
        {
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gk",
                "xclip-" + Guid.NewGuid().ToString("N")
            );
            System.IO.Directory.CreateDirectory(folder);

            try
            {
                // grid.dxf: a line from (0, 0) to (1 000, 0), attached at the origin of host.dxf.
                DxfDatabase grid = CadTemplate.CreateMetric();
                grid.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
                grid.Save(System.IO.Path.Combine(folder, "grid.dxf"));
                DxfDatabase host = CadTemplate.CreateMetric();
                var reference = new DxfBlockReference(new GeoPoint3(0, 0, 0), host.AttachXref(".\\grid.dxf", "grid"));
                host.ModelSpace.AppendEntity(reference);
                string path = System.IO.Path.Combine(folder, "host.dxf");
                host.Save(path);
                CadDocument document = CadDocument.Open(path);
                CadCommandSession session = Session(document);
                string handle = All<DxfBlockReference>(document.ModelSpace).Single().Handle;

                ClipToRectangle(session, handle, P(200, -50), P(400, 50));

                List<DisplayItem2> shown = Drawn(document, handle).Where(i => !i.IsClipFrame).ToList();
                DisplayPolyline2 line = Assert.IsType<DisplayPolyline2>(Assert.Single(shown));
                Assert.Equal(1000.0, line.Bounds.MaxX, 9);
                Assert.Equal(200.0, line.BlockClip.Bounds.MinX, 9);
                Assert.Equal(400.0, line.BlockClip.Bounds.MaxX, 9);
                Assert.Contains(Drawn(document, handle), i => i.IsClipFrame);
                PickIndex index = document.GetPickIndex(document.ModelSpace);
                Assert.Contains(handle, index.Pick(300, 0, 1.0));
                Assert.DoesNotContain(handle, index.Pick(600, 0, 1.0));
                Assert.DoesNotContain(handle, index.Pick(100, 0, 1.0));
            }
            finally
            {
                try
                {
                    System.IO.Directory.Delete(folder, true);
                }
                catch (System.IO.IOException)
                {
                    // A file a watcher still holds is left for the system to clear.
                }
            }
        }

        [Fact]
        public void AClippedReferenceInAClippedBlock_IsShownInsideBothBoundaries()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string inner);
            DxfDatabase database = document.Database;
            DxfBlockReference nested = All<DxfBlockReference>(document.ModelSpace).Single();
            var outer = new DxfBlockTableRecord { Name = "OUTER", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(outer);

            // GHB at the origin of OUTER, clipped to its corner 40 by 30 in the coordinates of OUTER; OUTER placed at (500, 0)
            // and clipped to a window 20 wide of that corner.
            var inside = new DxfBlockReference(new GeoPoint3(0, 0, 0), database.BlockTable["GHB"]);
            outer.AppendEntity(inside);
            inside.ClipTo(new[] { new GeoPoint2(0, 0), new GeoPoint2(40, 30) }, GeoTransform3.Identity);
            var placed = new DxfBlockReference(new GeoPoint3(500, 0, 0), outer);
            database.ModelSpace.AppendEntity(placed);
            document.Regen();
            ClipToRectangle(session, placed.Handle, P(500, -10), P(520, 40));

            List<DisplayItem2> shown = Drawn(document, placed.Handle).Where(i => !i.IsClipFrame).ToList();

            Assert.NotEmpty(shown);
            Assert.All(shown, item => Assert.NotNull(item.BlockClip.Parent));
            DisplayClipRegion region = shown[0].BlockClip;
            Assert.Equal(500.0, region.Bounds.MinX, 9);
            Assert.Equal(520.0, region.Bounds.MaxX, 9);
            Assert.Equal(520.0, region.Parent.Bounds.MaxX, 9);
            Assert.Equal(540.0, Enumerable.Range(0, region.Ring.Length / 2).Max(k => region.Ring[2 * k]), 9);
            Assert.False(region.Contains(530, 10));
            Assert.True(region.Contains(510, 10));
        }
    }
}
