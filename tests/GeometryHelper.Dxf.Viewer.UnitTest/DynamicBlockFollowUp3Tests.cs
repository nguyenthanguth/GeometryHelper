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
    /// Dynamic blocks in use, the third round of follow-ups (plugin GhDy3a, AutoCAD 2021): the base grip of a polar parameter,
    /// the insertion grip left out where a custom grip stands on it, the distances a base grip leaves taken to the value set,
    /// and the Origin of each parameter kept out of Properties.
    /// </summary>
    public class DynamicBlockFollowUp3Tests
    {
        /// <summary>
        /// A drawing with the block GH_POINTER, two lines from (0, 0) to (100, 0) and from (100, 0) to (90, 5) held by the end
        /// of a polar parameter from (0, 0) to (100, 0), inserted at (1 000, 500).
        /// </summary>
        private static CadDocument Pointer(out string handle, bool sets = false)
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
                sets ? DxfBlockParameterValueSet.Increments(50, 300, 10) : null,
                sets ? DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12) : null
            );
            block.AddPolarStretchAction(
                polar,
                new GeoPoint2(90, -10),
                new GeoPoint2(110, 10),
                new DxfEntity[] { line, tip },
                new DxfEntity[0]
            );
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(db, "pointer.dxf");
        }

        /// <summary>A circle moved by a point parameter at the block's origin, inserted at (1 000, 500).</summary>
        private static CadDocument Spot(out string handle)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_SPOT", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var dot = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
            block.AppendEntity(dot);
            DxfBlockPointParameter anchor = block.AddPointParameter("Anchor", null, new GeoPoint3(0, 0, 0));
            block.AddMoveAction(anchor, new DxfEntity[] { dot });
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(db, "spot.dxf");
        }

        /// <summary>
        /// A panel 100 by 50 of a linear parameter Width from (0, 0) to (100, 0) of 50 to 300 by 10, its left edge held by a
        /// stretch that follows the base point (BaseXDelta, BaseYDelta), inserted at (1 000, 500).
        /// </summary>
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
                DxfBlockParameterValueSet.Increments(50, 300, 10)
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

        private static GeoPoint3 OriginOf(CadDocument document, string handle) =>
            (GeoPoint3)
                Insert(document, handle)
                    .DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Origin")
                    .Value;

        [Fact]
        public void APolarPointer_ListsItsEndAndItsBaseGrip_AndNoInsertionGripWhereTheBaseGripStands()
        {
            // AutoCAD 2021: a pointer of one polar parameter at (1 000, 500) gave two grips, (1 100, 500) and (1 000, 500).
            CadDocument document = Pointer(out string handle);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));

            Assert.Equal(2, grips.Count);
            Assert.All(grips, g => Assert.NotNull(g.Custom));
            Near(P(1100, 500), grips[0].Point);
            Near(P(1000, 500), grips[1].Point);
        }

        [Fact]
        public void ThePolarBaseGripDragged_MovesTheBasePoint_TheEndStaying_AndTheInsertionGripAppears()
        {
            // AutoCAD 2021: the base grip by (30, 10) read Origin (30, 10), Length 81.39 and Angle 354.3 degrees, left the two
            // lines where they were, and then listed three grips: the end (1 100, 500), the base (1 030, 510), the insertion.
            CadDocument document = Pointer(out string handle);

            Assert.True(document.GripDrag(handle, 1, P(1030, 510)).Succeeded);

            Assert.Equal(Math.Sqrt(70 * 70 + 10 * 10), ValueOf(document, handle, "Length"), 9);
            Assert.True(OriginOf(document, handle).IsEqualTo(new GeoPoint3(30, 10, 0), Tight));
            IReadOnlyList<DxfLine> lines = Insert(document, handle).BlockTableRecord.OfType<DxfLine>().ToList();
            Assert.Contains(lines, l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(0, 0, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Tight)
            );
            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));
            Assert.Equal(3, grips.Count);
            Near(P(1100, 500), grips[0].Point);
            Near(P(1030, 510), grips[1].Point);
            Assert.Equal(CadGripKind.Insertion, grips[2].Kind);
            Assert.Equal("GRIP", document.NextUndo.Name);
        }

        [Fact]
        public void ThePolarEndGripDraggedAfterTheBaseMoved_TurnsTheLinesAboutTheNewBase_AndIsOneUndoStep()
        {
            // AutoCAD 2021, distances of 50 to 300 by 10 and angles every 15 degrees: the base grip by (20, 0), then the end grip
            // to 50 at 45 degrees from the new base point: the first line runs (5.8579, -14.1421) to (55.3553, 35.3553).
            CadDocument document = Pointer(out string handle, true);
            Assert.True(document.GripDrag(handle, 1, P(1020, 500)).Succeeded);
            double half = 50.0 / Math.Sqrt(2.0);

            Assert.True(document.GripDrag(handle, 0, P(1020 + half, 500 + half)).Succeeded);

            Assert.Equal(50.0, ValueOf(document, handle, "Length"), 9);
            Assert.Equal(Math.PI / 4, ValueOf(document, handle, "Angle"), 9);
            Assert.Contains(Insert(document, handle).BlockTableRecord.OfType<DxfLine>(), l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(20 - 20 / Math.Sqrt(2.0), -20 / Math.Sqrt(2.0), 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(20 + half, half, 0), Tight)
            );
            Assert.Equal("GRIP", document.NextUndo.Name);
            document.Undo();
            Assert.Equal(80.0, ValueOf(document, handle, "Length"), 9);
        }

        [Fact]
        public void APointParameterAtTheBlocksOrigin_ListsItsGripAlone_AsAutoCadDid()
        {
            // AutoCAD 2021: a spot moved by a point parameter at the origin of a block inserted at (1 000, 500) gave one grip;
            // dragged by (20, 10) it gave two, the spot at (1 020, 510) and the insertion point.
            CadDocument document = Spot(out string handle);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));
            Assert.Single(grips);
            Assert.Equal(CadGripKind.CustomPoint, grips[0].Kind);
            Assert.True(document.GripDrag(handle, 0, P(1020, 510)).Succeeded);

            grips = CadGrips.Of(Insert(document, handle));
            Assert.Equal(2, grips.Count);
            Near(P(1020, 510), grips[0].Point);
            Assert.Equal(CadGripKind.Insertion, grips[1].Kind);
        }

        [Fact]
        public void TheOrigin_IsKeptOutOfTheCustomGroup_AsAutoCadKeptItOutOfProperties()
        {
            CadDocument document = Pointer(out string handle);

            CadPropertyGroup group = CadProperties
                .Describe(document, document.ModelSpace, new[] { handle })
                .Groups.Single(g => g.Title == CadProperties.CustomGroup);

            Assert.Equal(new[] { "Length", "Angle" }, group.Rows.Select(r => r.Label));
        }

        [Fact]
        public void ALinearBaseGripDragged_TakesTheWidthToItsIncrement_AsAutoCadDid()
        {
            // AutoCAD 2021, Width 50 to 300 by 10: the base grip by 33 read Width 70 with the base point at 30, by 60 Width 50.
            CadDocument document = Panel(out string handle);
            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));
            Assert.Equal(2, grips.Count);
            Near(P(1100, 500), grips[0].Point);
            Near(P(1000, 500), grips[1].Point);

            Assert.True(document.GripDrag(handle, 1, P(1033, 500)).Succeeded);

            Assert.Equal(70.0, ValueOf(document, handle, "Width"), 9);
            Assert.True(OriginOf(document, handle).IsEqualTo(new GeoPoint3(30, 0, 0), Tight));
            Near(P(1030, 500), CadGrips.Of(Insert(document, handle))[1].Point);

            document = Panel(out handle);
            Assert.True(document.GripDrag(handle, 1, P(1060, 500)).Succeeded);
            Assert.Equal(50.0, ValueOf(document, handle, "Width"), 9);
        }
    }
}
