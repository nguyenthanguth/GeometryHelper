using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The pick index hands an XLINE or a RAY to a pick, a snap or a selection only where it passes through or near the box
    /// asked about, so that lines without end cost about what lines do; what is picked, snapped and selected stays the same.
    /// </summary>
    public class XlineRayIndexTests
    {
        private static string Xline(double x, double y, double dx, double dy, string handle = null)
            => TestDrawings.Entity("XLINE", "0", 256, handle, 100, "AcDbXline", 10, x, 20, y, 30, 0.0, 11, dx, 21, dy, 31, 0.0);

        private static string Ray(double x, double y, double dx, double dy, string handle = null)
            => TestDrawings.Entity("RAY", "0", 256, handle, 100, "AcDbRay", 10, x, 20, y, 30, 0.0, 11, dx, 21, dy, 31, 0.0);

        private static string Line(double x0, double y0, double x1, double y1, string handle)
            => TestDrawings.Entity("LINE", "0", 256, handle, 100, "AcDbLine", 10, x0, 20, y0, 30, 0.0, 11, x1, 21, y1, 31, 0.0);

        private static PickIndex Index(params string[] entities)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "rays.dxf");
            return document.GetPickIndex(document.ModelSpace);
        }

        [Fact]
        public void ItemsNear_LeavesOutAnXlineThatPassesTheBoxBy_AndKeepsOneThatCrossesIt()
        {
            // An xline along (1, 1) through the origin passes (5001, 4990) about 7.8 off and runs through (5001, 5000).
            PickIndex index = Index(Xline(0, 0, 1, 1, "B1"), Line(0, 0, 10, 0, "A1"));

            Assert.Empty(index.ItemsNear(new DisplayBox(5000, 4989, 5002, 4991)));
            Assert.Equal("B1", Assert.Single(index.ItemsNear(new DisplayBox(5000, 4999, 5002, 5001))).Handle);
        }

        [Fact]
        public void AnXlineOfABlock_StillOffersTheInsertionPoint_ThoughItPassesFarFromTheCursor()
        {
            // The block's one object is an xline along (1, 1) through (300, 0); inserted at (100, 100) it passes far from it.
            DxfDatabase database = TestDrawings.Drawing(null, TestDrawings.Block("K", Xline(300, 0, 1, 1)), TestDrawings.Insert("K", 100, 100, handle: "C1"));
            CadDocument document = CadDocument.From(database, "test.dxf");
            PickIndex index = document.GetPickIndex(document.ModelSpace);

            CadSnapResult result = new ObjectSnap(Tight) { Modes = CadSnapModes.Insertion }.Snap(index, P(100.3, 100.2), 1.0);

            Assert.Equal(CadSnapKind.Insertion, result.Kind);
            Assert.Equal("C1", result.Handle);
            Near(P(100, 100), result.Point, 1E-9);
        }

        [Fact]
        public void PicksAndCrossingsAmongTwoHundredXlinesAndRays_AreThoseFoundByLookingAtEveryItem()
        {
            var random = new Random(17);
            var entities = new List<string>();

            for (int k = 0; k < 200; k++)
            {
                double x = random.NextDouble() * 1000.0;
                double y = random.NextDouble() * 1000.0;
                double a = k % 10 == 3 ? 0.0 : k % 10 == 7 ? Math.PI / 2.0 : random.NextDouble() * Math.PI * 2.0;
                string handle = (0x100 + k).ToString("X");
                entities.Add(k % 2 == 0 ? Xline(x, y, Math.Cos(a), Math.Sin(a), handle) : Ray(x, y, Math.Cos(a), Math.Sin(a), handle));
            }

            for (int k = 0; k < 400; k++)
            {
                double x = random.NextDouble() * 1000.0;
                double y = random.NextDouble() * 1000.0;
                entities.Add(Line(x, y, x + 15.0, y + 8.0, (0x400 + k).ToString("X")));
            }

            PickIndex index = Index(entities.ToArray());
            IReadOnlyList<DisplayItem2> items = index.List.Items;

            for (int m = 0; m < 2000; m++)
            {
                double x = random.NextDouble() * 1400.0 - 200.0;
                double y = random.NextDouble() * 1400.0 - 200.0;
                double tolerance = 0.1 + random.NextDouble() * 5.0;

                var picked = new SortedSet<string>(index.Pick(x, y, tolerance), StringComparer.Ordinal);
                var near = new SortedSet<string>(items.Where(i => i.DistanceTo(x, y) <= tolerance).Select(i => i.Handle), StringComparer.Ordinal);
                Assert.Equal(near, picked);

                var box = new DisplayBox(x, y, x + tolerance * 4.0, y + tolerance * 3.0);
                var crossed = new SortedSet<string>(items.Where(i => i.Touches(box)).Select(i => i.Handle), StringComparer.Ordinal);
                Assert.Equal(crossed, new SortedSet<string>(index.Crossing(box), StringComparer.Ordinal));
            }
        }
    }
}
