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
    /// Dynamic blocks in use, the fourth round of follow-ups (plugin GhDb4a, AutoCAD 2021): a polar base grip dragged after an
    /// end grip, a stretch that follows a base point kept there at a value typed after, and the end grip of a linear
    /// parameter dragged in a block that keeps a history of its grip edits.
    /// </summary>
    public class DynamicBlockFollowUp4Tests
    {
        /// <summary>
        /// A drawing with the block GH_POINTER, two lines from (0, 0) to (100, 0) and from (100, 0) to (90, 5) held by the end
        /// of a polar parameter from (0, 0) to (100, 0), and, where asked, a box and an edge above it stretched by the end of
        /// a linear parameter Width from (0, 100) to (100, 100); inserted at (1 000, 500).
        /// </summary>
        private static CadDocument Pointer(out string handle, bool width = false)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_POINTER", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var tip = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(90, 5, 0));
            block.AppendEntity(line);
            block.AppendEntity(tip);
            DxfBlockPolarParameter polar = block.AddPolarParameter(
                "Length",
                null,
                "Angle",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                null,
                null
            );
            block.AddPolarStretchAction(
                polar,
                new GeoPoint2(90, -10),
                new GeoPoint2(110, 10),
                new DxfEntity[] { line, tip },
                new DxfEntity[0]
            );

            if (width)
            {
                var box = new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0));
                var edge = new DxfLine(new GeoPoint3(100, 100, 0), new GeoPoint3(100, 150, 0));
                block.AppendEntity(box);
                block.AppendEntity(edge);
                DxfBlockLinearParameter linear = block.AddLinearParameter(
                    "Width",
                    null,
                    new GeoPoint3(0, 100, 0),
                    new GeoPoint3(100, 100, 0),
                    20,
                    null
                );
                block.AddStretchAction(linear, new GeoPoint2(90, 90), new GeoPoint2(110, 160), new[] { box, edge });
            }

            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(db, "pointer.dxf");
        }

        /// <summary>A panel 100 by 50 of a linear parameter Width from (0, 0) to (100, 0), its left edge held by a stretch that follows the base point, inserted at (1 000, 500).</summary>
        private static CadDocument Panel(out string handle)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var right = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            var top = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0));
            var bottom = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var left = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));

            foreach (DxfLine line in new[] { right, top, bottom, left })
            {
                block.AppendEntity(line);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                null
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right, top, bottom });
            block.AddStretchAction(width, new GeoPoint2(-10, -10), new GeoPoint2(10, 60), new[] { left, top, bottom });
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;

            string[] lines = db.ToText().Replace("\r\n", "\n").Split('\n');
            int seen = -1;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != "BLOCKSTRETCHACTION" || ++seen != 1)
                {
                    continue;
                }

                for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                {
                    lines[j + 1] =
                        lines[j + 1].Trim() == "EndXDelta" ? "BaseXDelta"
                        : lines[j + 1].Trim() == "EndYDelta" ? "BaseYDelta"
                        : lines[j + 1];
                }
            }

            return CadDocument.From(DxfDatabase.Parse(string.Join("\n", lines)), "panel.dxf");
        }

        private static DxfBlockReference Insert(CadDocument document, string handle) =>
            Entity<DxfBlockReference>(document, handle);

        private static double ValueOf(CadDocument document, string handle, string name) =>
            (double)
                Insert(document, handle)
                    .DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == name)
                    .Value;

        private static bool Has(CadDocument document, string handle, double x1, double y1, double x2, double y2) =>
            Insert(document, handle)
                .BlockTableRecord.OfType<DxfLine>()
                .Any(l =>
                    l.StartPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), Tight)
                    && l.EndPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), Tight)
                );

        /// <summary>The names of the records of the INSERT's history of grip edits (300 after each 1071); null where it keeps none.</summary>
        private static List<string> History(CadDocument document, string handle)
        {
            DxfBlockReference insert = Insert(document, handle);
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var cache = (DxfDBDictionary)representation.GetAt("AppDataCache");

            return cache.Contains("ACAD_ENHANCEDBLOCKHISTORY")
                ? ((DxfXrecord)cache.GetAt("ACAD_ENHANCEDBLOCKHISTORY"))
                    .Data.Where(g => g.TypeCode == 300)
                    .Select(g => g.Value.Trim())
                    .ToList()
                : null;
        }

        [Fact]
        public void ThePolarBaseGripDraggedAfterTheEndGrip_IsWorkedOutFromTheEdits_AndEachDragIsOneUndoStep()
        {
            // AutoCAD 2021: the end grip to block point (0, 50), the base grip by 30 and the end grip to (35, 0) left the first
            // line from (45.4349, 25.7248) to (35, 0) and the tip from there to (28.9976, 9.4324); refused before.
            CadDocument document = Pointer(out string handle);
            Assert.True(document.GripDrag(handle, 0, P(1000, 550)).Succeeded);

            CadEditResult base30 = document.GripDrag(handle, 1, P(1030, 500));
            Assert.True(base30.Succeeded, base30.Message);
            Assert.True(document.GripDrag(handle, 0, P(1035, 500)).Succeeded);

            Assert.Equal(5.0, ValueOf(document, handle, "Length"), 9);
            Assert.True(FirstLineAsAutoCadPrintedIt(document, handle));
            Assert.Equal("GRIP", document.NextUndo.Name);
            document.Undo();
            Assert.Equal(Math.Sqrt(30 * 30 + 50 * 50), ValueOf(document, handle, "Length"), 9);
        }

        /// <summary>The first line, from where AutoCAD printed it (to four places) to the grip's end.</summary>
        private static bool FirstLineAsAutoCadPrintedIt(CadDocument document, string handle) =>
            Insert(document, handle)
                .BlockTableRecord.OfType<DxfLine>()
                .Any(l =>
                    Math.Abs(l.StartPoint.X - 45.4349) < 1E-4
                    && Math.Abs(l.StartPoint.Y - 25.7248) < 1E-4
                    && Math.Abs(l.EndPoint.X - 35) < 1E-9
                    && Math.Abs(l.EndPoint.Y) < 1E-9
                );

        [Fact]
        public void ALinearBaseGripDraggedBy30_ThenWidth150Typed_KeepsTheLeftEdgeAtTheBasePoint()
        {
            // AutoCAD 2021 (in memory, after DXF and after DWG): the base grip by 30, then Width 150 set: the end point at 180,
            // the left edge still at x 30.
            CadDocument document = Panel(out string handle);
            Assert.True(document.GripDrag(handle, 1, P(1030, 500)).Succeeded);

            CadEditResult typed = CadDynamicBlocks.SetProperty(document, new[] { handle }, "Width", "150");

            Assert.True(typed.Succeeded, typed.Message);
            Assert.Equal(150.0, ValueOf(document, handle, "Width"), 9);
            Assert.True(Has(document, handle, 180, 0, 180, 50));
            Assert.True(Has(document, handle, 30, 0, 30, 50));
            Assert.True(Has(document, handle, 30, 0, 180, 0));
        }

        [Fact]
        public void TheEndGripOfALinearParameter_DraggedInAPointer_TakesTheDistance_AndWritesAGripLoc()
        {
            // AutoCAD 2021: the box's end grip by (30, 0) read Width 130, the edge went to x 130, and the history kept a GRIPLOC
            // of the grip, where a typed value would have kept a record of Width.
            CadDocument document = Pointer(out string handle, true);
            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));
            Assert.Equal(3, grips.Count);
            Near(P(1100, 600), grips[2].Point);

            Assert.True(document.GripDrag(handle, 2, P(1130, 600)).Succeeded);

            Assert.Equal(130.0, ValueOf(document, handle, "Width"), 9);
            Assert.True(Has(document, handle, 0, 100, 130, 100));
            Assert.True(Has(document, handle, 130, 100, 130, 150));
            Assert.Equal(new[] { "GRIPLOC" }, History(document, handle));
        }
    }
}
