using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The base point of a linear or an XY parameter moved by a grip (plugin GhDy2a): AutoCAD 2021 read Width 80 where the corner
    /// of base x and end y of a plate 100 by 50 was dragged by (20, 10), kept the right edge at x 100 and the base point at
    /// (20, 0), and worked every later value set from it. Each number below is one AutoCAD 2021 gave on blocks built as these
    /// are, a grip dragged through <c>MoveGripPointsAt</c> or an INSERT read back from its file, the library's INSERT read by
    /// AutoCAD again with AUDIT finding no error.
    /// </summary>
    public class DxfDynamicBlockBasePointTests
    {
        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        // ---- building ----------------------------------------------------------------------------------------------------

        /// <summary>A drawing of R2018 with an empty block of the name.</summary>
        private static DxfDatabase Drawing(string name, out DxfBlockTableRecord block)
        {
            var db = new DxfDatabase("AC1032");
            block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            return db;
        }

        private static T Add<T>(DxfBlockTableRecord block, T entity)
            where T : DxfEntity
        {
            block.AppendEntity(entity);
            return entity;
        }

        private static DxfLine Line(DxfBlockTableRecord block, double x1, double y1, double x2, double y2) =>
            Add(block, new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0)));

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        /// <summary>
        /// Makes the nth action of a kind (0 the first) follow its parameter's base point, BaseXDelta and BaseYDelta, as AutoCAD's
        /// Block Editor writes one tied to the base grip, by the text of the drawing.
        /// </summary>
        private static DxfDatabase FollowingTheBase(DxfDatabase db, string type, int nth)
        {
            string[] lines = db.ToText().Replace("\r\n", "\n").Split('\n');
            int seen = -1;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != type || ++seen != nth)
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

            return DxfDatabase.Parse(string.Join("\n", lines));
        }

        /// <summary>
        /// A plate 100 by 50 inserted at (1 000, 500): its right and top edges stretched by the end point of an XY parameter from
        /// (0, 0) to (100, 50), its bottom and left edges not; given a stretch of the bottom left corner that follows the base
        /// point where asked, and a locked attribute definition each stretch holds where asked.
        /// </summary>
        private static DxfDatabase Plate(bool baseStretch, bool attributes)
        {
            DxfDatabase db = Drawing("GH_PLATE", out DxfBlockTableRecord block);
            DxfLine right = Line(block, 100, 0, 100, 50);
            DxfLine top = Line(block, 0, 50, 100, 50);
            DxfLine bottom = Line(block, 0, 0, 100, 0);
            DxfLine left = Line(block, 0, 0, 0, 50);
            var held = new List<DxfEntity> { right, top };
            var heldByBase = new List<DxfEntity> { left, bottom };

            if (attributes)
            {
                held.Add(
                    Add(
                        block,
                        new DxfAttributeDefinition(new GeoPoint3(98, 48, 0), "-", "NOTE", "Note", null)
                        {
                            Height = 2.5,
                            LockPositionInBlock = true,
                        }
                    )
                );
                heldByBase.Add(
                    Add(
                        block,
                        new DxfAttributeDefinition(new GeoPoint3(2, 2, 0), "-", "LOWER", "Lower", null)
                        {
                            Height = 2.5,
                            LockPositionInBlock = true,
                        }
                    )
                );
            }

            DxfBlockXYParameter xy = block.AddXYParameter(
                "Width",
                null,
                "Height",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 50, 0)
            );
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), held);

            if (baseStretch)
            {
                block.AddStretchAction(xy, new GeoPoint2(-10, -10), new GeoPoint2(10, 10), heldByBase);
            }

            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);

            if (attributes)
            {
                foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>().ToList())
                {
                    var attribute = new DxfAttributeReference();
                    attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                    insert.AttributeCollection.AppendAttribute(attribute);
                }
            }

            return Reopened(baseStretch ? FollowingTheBase(db, "BLOCKSTRETCHACTION", 1) : db);
        }

        /// <summary>The block of AutoCAD's XY sample: a circle at the origin, the XY parameter from (0, 5) to (5, 0), an array of columns of 25 and rows of 20, inserted at (1 000, 500).</summary>
        private static DxfDatabase Grid()
        {
            DxfDatabase db = Drawing("GH_GRID", out DxfBlockTableRecord block);
            DxfCircle dot = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockXYParameter xy = block.AddXYParameter(
                "X Distance1",
                null,
                "Y Distance1",
                null,
                new GeoPoint3(0, 5, 0),
                new GeoPoint3(5, 0, 0)
            );
            block.AddArrayAction(xy, new[] { dot }, 25, 20);
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 500, 0), block));
            return Reopened(db);
        }

        /// <summary>
        /// A panel 100 by 50 inserted at (1 000, 500): its right, top and bottom edges stretched by the end of a linear parameter
        /// Width from (0, 0) to (100, 0), a stretch of its left edge that follows the base point where asked, and a circle
        /// whose array of columns of 25 follows it where asked.
        /// </summary>
        private static DxfDatabase Panel(bool baseStretch, bool array)
        {
            DxfDatabase db = Drawing("GH_PANEL", out DxfBlockTableRecord block);
            DxfLine right = Line(block, 100, 0, 100, 50);
            DxfLine top = Line(block, 0, 50, 100, 50);
            DxfLine bottom = Line(block, 0, 0, 100, 0);
            DxfLine left = Line(block, 0, 0, 0, 50);
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                null
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right, top, bottom });

            if (baseStretch)
            {
                block.AddStretchAction(
                    width,
                    new GeoPoint2(-10, -10),
                    new GeoPoint2(10, 60),
                    new[] { left, top, bottom }
                );
            }

            if (array)
            {
                DxfCircle dot = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5));
                block.AddArrayAction(width, new[] { dot }, 25);
            }

            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 500, 0), block));
            return Reopened(baseStretch ? FollowingTheBase(db, "BLOCKSTRETCHACTION", 1) : db);
        }

        // ---- reading back ------------------------------------------------------------------------------------------------

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) =>
            insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static double ValueOf(DxfBlockReference insert, string name) =>
            Assert.IsType<double>(Property(insert, name).Value);

        private static void Set(DxfDatabase db, string name, double value) =>
            Property(TheInsert(db), name).Value = value;

        private static void MoveBase(DxfDatabase db, string name, double x, double y) =>
            Property(TheInsert(db), name).MoveBasePoint(new GeoVector3(x, y, 0));

        private static bool Has(DxfBlockReference insert, double x1, double y1, double x2, double y2) =>
            insert
                .BlockTableRecord.OfType<DxfLine>()
                .Any(l =>
                    l.StartPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), Exact)
                    && l.EndPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), Exact)
                );

        private static List<GeoPoint3> Circles(DxfBlockReference insert) =>
            insert.BlockTableRecord.OfType<DxfCircle>().Select(c => c.Center).ToList();

        /// <summary>The base point and the end point the INSERT saved of the first parameter of the type in the graph of its block.</summary>
        private static GeoPoint3[] Saved(DxfBlockReference insert, string type)
        {
            DxfDBObject graph = insert.DynamicBlockTableRecord.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            string id = graph
                .Item.Groups.Where(g => g.TypeCode == 360)
                .Select(g => graph.Item.File.ObjectItem(g.Value))
                .First(n => n.Type == type)
                .Groups.First(g => g.TypeCode == 90)
                .Value.Trim();
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var data = (DxfDBDictionary)
                ((DxfDBDictionary)representation.GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            List<DxfTypedValue> groups = ((DxfXrecord)data.GetAt(id)).Data.ToList();
            var points = new List<GeoPoint3>();

            for (int i = 0; i + 2 < groups.Count; i++)
            {
                if (groups[i].TypeCode == 10)
                {
                    points.Add(
                        new GeoPoint3(
                            double.Parse(groups[i].Value, CultureInfo.InvariantCulture),
                            double.Parse(groups[i + 1].Value, CultureInfo.InvariantCulture),
                            double.Parse(groups[i + 2].Value, CultureInfo.InvariantCulture)
                        )
                    );
                }
            }

            return new[] { points[0], points[1] };
        }

        private static bool At(GeoPoint3 point, double x, double y) => point.IsEqualTo(new GeoPoint3(x, y, 0), Exact);

        // ---- XY: a corner or the base grip dragged -------------------------------------------------------------------------

        [Fact]
        public void TheCornerOfBaseXAndEndYDraggedBy20And10_ReadsWidth80AndKeepsTheRightEdge()
        {
            // AutoCAD 2021: Width 80, Height 60, the base point (20, 0), the end point (100, 60), the right and top edges
            // stretched by the end point's 10 along y alone (grip 1 of the plate, moved by (20, 10)).
            DxfDatabase db = Plate(false, false);

            Set(db, "Height", 60);
            MoveBase(db, "Width", 20, 0);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(80.0, ValueOf(insert, "Width"), 9);
            Assert.Equal(60.0, ValueOf(insert, "Height"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 20, 0));
            Assert.True(At(saved[1], 100, 60));
            Assert.True(Has(insert, 100, 0, 100, 60));
            Assert.True(Has(insert, 0, 50, 100, 60));
            Assert.True(Has(insert, 0, 0, 100, 0));
            Assert.True(Has(insert, 0, 0, 0, 50));
        }

        [Fact]
        public void TheCornerOfEndXAndBaseYDraggedBy20And10_ReadsHeight40AndKeepsTheTopEdge()
        {
            // AutoCAD 2021: Width 120, Height 40, the base point (0, 10), the end point (120, 50) (grip 2, moved by (20, 10)).
            DxfDatabase db = Plate(false, false);

            Set(db, "Width", 120);
            MoveBase(db, "Height", 0, 10);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(120.0, ValueOf(insert, "Width"), 9);
            Assert.Equal(40.0, ValueOf(insert, "Height"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 0, 10));
            Assert.True(At(saved[1], 120, 50));
            Assert.True(Has(insert, 100, 0, 120, 50));
            Assert.True(Has(insert, 0, 50, 120, 50));
        }

        [Fact]
        public void AValueSetAfterABaseMove_KeepsTheBasePoint_AndStretchesByTheEndPoint()
        {
            // AutoCAD 2021, grip 1 by (20, 10) and then Width set to 120: the base point stays (20, 0), the end goes to (140, 60)
            // and the right and top edges go there from (100, 60); the same with the plate's Height set to 70 (end (100, 70)).
            DxfDatabase db = Plate(false, false);
            Set(db, "Height", 60);
            MoveBase(db, "Width", 20, 0);

            Set(db, "Width", 120);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(120.0, ValueOf(insert, "Width"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 20, 0));
            Assert.True(At(saved[1], 140, 60));
            Assert.True(Has(insert, 100, 0, 140, 60));
            Assert.True(Has(insert, 0, 50, 140, 60));

            Set(db, "Height", 70);
            saved = Saved(TheInsert(db), "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 20, 0));
            Assert.True(At(saved[1], 140, 70));
        }

        [Fact]
        public void TwoBaseMoves_AddUp()
        {
            // AutoCAD 2021, grip 1 by (20, 10) and then by (5, 5): Width 75, Height 65, the base point (25, 0).
            DxfDatabase db = Plate(false, false);
            Set(db, "Height", 60);
            MoveBase(db, "Width", 20, 0);

            Set(db, "Height", 65);
            MoveBase(db, "Width", 5, 0);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(75.0, ValueOf(insert, "Width"), 9);
            Assert.Equal(65.0, ValueOf(insert, "Height"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 25, 0));
            Assert.True(At(saved[1], 100, 65));
        }

        [Fact]
        public void TheBaseGripMovedBy20And10_FollowsTheStretchOfTheBasePoint_TheEndPointStaying()
        {
            // AutoCAD 2021 on a plate whose bottom left corner a stretch of the base point holds: Width 80, Height 40, the
            // base point (20, 10), the end point (100, 50); the bottom and left edges start at (20, 10), the others stay.
            DxfDatabase db = Plate(true, false);

            MoveBase(db, "Width", 20, 10);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(80.0, ValueOf(insert, "Width"), 9);
            Assert.Equal(40.0, ValueOf(insert, "Height"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 20, 10));
            Assert.True(At(saved[1], 100, 50));
            Assert.True(Has(insert, 20, 10, 100, 0));
            Assert.True(Has(insert, 20, 10, 0, 50));
            Assert.True(Has(insert, 100, 0, 100, 50));
            Assert.True(Has(insert, 0, 50, 100, 50));
        }

        [Fact]
        public void AValueSetOfAnInsertWhoseBaseMoved_LeavesTheStretchOfTheBasePointWhereTheBlockHasIt()
        {
            // AutoCAD 2021 read the plate back from its file, the base point at (20, 0) and the end point at (120, 50), and set
            // Width 150: the end point went to (170, 50) and the stretch of the base point moved nothing, the bottom and left
            // edges keeping the block's own points: a grip's move is followed while it is made, and is not kept.
            DxfDatabase db = Plate(true, false);
            MoveBase(db, "Width", 20, 0);
            db = Reopened(db);

            Set(db, "Width", 150);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(150.0, ValueOf(insert, "Width"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 20, 0));
            Assert.True(At(saved[1], 170, 50));
            Assert.True(Has(insert, 100, 0, 170, 50));
            Assert.True(Has(insert, 0, 50, 170, 50));
            Assert.True(Has(insert, 0, 0, 100, 0));
            Assert.True(Has(insert, 0, 0, 0, 50));
        }

        [Fact]
        public void TheAttributesFollowTheStretchesOfTheEndAndOfTheBasePoint()
        {
            // AutoCAD 2021, the corner of base x and end y by (20, 10): NOTE, held by the end point's stretch, moved 10 along y
            // to (1 098, 558), and LOWER, held by the base point's, 20 along x to (1 022, 502).
            DxfDatabase db = Plate(true, true);

            Set(db, "Height", 60);
            MoveBase(db, "Width", 20, 0);

            DxfBlockReference insert = TheInsert(db);
            DxfAttributeReference note = insert.AttributeCollection.Single(a => a.Tag == "NOTE");
            DxfAttributeReference lower = insert.AttributeCollection.Single(a => a.Tag == "LOWER");
            Assert.True(At(note.Position, 1098, 558));
            Assert.True(At(lower.Position, 1022, 502));
        }

        [Fact]
        public void TheAnonymousBlocksWorkedOutAnew_TakeTheStretchesOfTheBasePointToWhereItStands()
        {
            // AutoCAD 2021's UpdateAnonymousBlocks on the plate read back from its file with the base point at (20, 0): the left
            // edge from (20, 0), the attribute definition LOWER at (22, 2) and its ATTRIB at (1 022, 502), where a value set
            // left them; the end point's stretch moved NOTE to (98, 48).
            DxfDatabase db = Plate(true, true);
            MoveBase(db, "Width", 20, 0);
            db = Reopened(db);

            DxfDynamicBlockRepresentation.UpdateAnonymous(TheInsert(db).DynamicBlockTableRecord);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(80.0, ValueOf(insert, "Width"), 9);
            Assert.True(Has(insert, 20, 0, 0, 50));
            Assert.True(
                At(
                    insert.BlockTableRecord.OfType<DxfAttributeDefinition>().Single(d => d.Tag == "LOWER").Position,
                    22,
                    2
                )
            );
            Assert.True(
                At(
                    insert.BlockTableRecord.OfType<DxfAttributeDefinition>().Single(d => d.Tag == "NOTE").Position,
                    98,
                    48
                )
            );
            Assert.True(At(insert.AttributeCollection.Single(a => a.Tag == "LOWER").Position, 1022, 502));
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 20, 0));
            Assert.True(At(saved[1], 100, 50));
        }

        [Fact]
        public void AnArrayStartsWhereABaseGripTookTheBasePoint()
        {
            // AutoCAD 2021, X Distance1 100 and Y Distance1 80 (end (100, -75)) and the base grip by (10, 20): the base point
            // (10, 25), distances 90 and 100, three columns of 25 and five rows of 20 from the circle at (10, 20) down to y -60.
            DxfDatabase db = Grid();
            Set(db, "X Distance1", 100);
            Set(db, "Y Distance1", 80);

            MoveBase(db, "X Distance1", 10, 20);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(90.0, ValueOf(insert, "X Distance1"), 9);
            Assert.Equal(100.0, ValueOf(insert, "Y Distance1"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 10, 25));
            Assert.True(At(saved[1], 100, -75));
            List<GeoPoint3> dots = Circles(insert);
            Assert.Equal(15, dots.Count);

            foreach (double x in new[] { 10.0, 35.0, 60.0 })
            {
                foreach (double y in new[] { 20.0, 0.0, -20.0, -40.0, -60.0 })
                {
                    Assert.Contains(dots, d => At(d, x, y));
                }
            }
        }

        [Fact]
        public void AnArrayCornerDragged_MovesTheFirstCopyAlongTheAxisTheBasePointMoved()
        {
            // AutoCAD 2021, the corner of base x and end y by (10, 20): the end point's y from -75 to -55, the base point's x to
            // 10 (distances 90 and 60), nine circles from (10, 0) to (60, -40).
            DxfDatabase db = Grid();
            Set(db, "X Distance1", 100);
            Set(db, "Y Distance1", 80);

            Set(db, "Y Distance1", 60);
            MoveBase(db, "X Distance1", 10, 0);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(90.0, ValueOf(insert, "X Distance1"), 9);
            Assert.Equal(60.0, ValueOf(insert, "Y Distance1"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKXYPARAMETER");
            Assert.True(At(saved[0], 10, 5));
            Assert.True(At(saved[1], 100, -55));
            List<GeoPoint3> dots = Circles(insert);
            Assert.Equal(9, dots.Count);
            Assert.Contains(dots, d => At(d, 10, 0));
            Assert.Contains(dots, d => At(d, 60, -40));
        }

        // ---- linear -------------------------------------------------------------------------------------------------------

        [Fact]
        public void ALinearBasePointMoved_ReadsTheDistanceBetweenTheTwoPoints_AndKeepsItWhenAValueIsSet()
        {
            // AutoCAD 2021 read an INSERT of Width 150 whose base point was at (20, 0) as Width 130, and set Width 200 with the
            // end point at (220, 0), the base point kept, the right, top and bottom edges stretched by the end point to x 220.
            DxfDatabase db = Panel(false, false);
            Set(db, "Width", 150);

            MoveBase(db, "Width", 20, 0);

            Assert.Equal(130.0, ValueOf(TheInsert(db), "Width"), 9);
            GeoPoint3[] saved = Saved(TheInsert(db), "BLOCKLINEARPARAMETER");
            Assert.True(At(saved[0], 20, 0));
            Assert.True(At(saved[1], 150, 0));

            Set(db, "Width", 200);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(200.0, ValueOf(insert, "Width"), 9);
            saved = Saved(insert, "BLOCKLINEARPARAMETER");
            Assert.True(At(saved[0], 20, 0));
            Assert.True(At(saved[1], 220, 0));
            Assert.True(Has(insert, 220, 0, 220, 50));
            Assert.True(Has(insert, 0, 50, 220, 50));
            Assert.True(Has(insert, 0, 0, 220, 0));
        }

        [Fact]
        public void ALinearBaseGrip_ShowsWhereAStretchFollowsTheBasePoint_AndMovesTheBasePointAlongTheLine()
        {
            // AutoCAD 2021 listed the base grip of a linear parameter a stretch of its base point follows on a fresh INSERT too (it
            // stood on the insertion grip, which AutoCAD then left out: the grip at (1 000, 500) dragged by 30 read Width 70, plugin
            // GhDy3a), at (1 020, 500) once the base point moved by 20; a drag across the line moves nothing across it.
            DxfDatabase db = Panel(true, false);
            Assert.Equal(2, TheInsert(db).GetDynamicGrips().Count);

            Set(db, "Width", 150);
            Assert.Equal(2, TheInsert(db).GetDynamicGrips().Count);

            MoveBase(db, "Width", 20, 30);

            IReadOnlyList<DxfDynamicBlockGrip> grips = TheInsert(db).GetDynamicGrips();
            Assert.Equal(2, grips.Count);
            Assert.Equal(DxfDynamicBlockGripType.Linear, grips[1].GripType);
            Assert.True(At(grips[1].Position, 1020, 500));
            Assert.True(At(grips[1].BasePoint, 1020, 500));
            GeoPoint3[] saved = Saved(TheInsert(db), "BLOCKLINEARPARAMETER");
            Assert.True(At(saved[0], 20, 0));
            Assert.True(At(saved[1], 150, 0));
        }

        [Fact]
        public void ALinearBaseGripMovedBy10_FollowsTheStretchOfTheBasePoint_AndTheArray()
        {
            // AutoCAD 2021 on an INSERT whose base point sat at (20, 0): the grip by 10 took it to (30, 0), the end point staying
            // at (150, 0), the left, top and bottom edges' starts to x 10 where the block has them at 0, and the first circle of
            // an array of columns of 25 to x 60 from 50: six columns from the distance 120.
            DxfDatabase db = Panel(true, true);
            Set(db, "Width", 150);
            MoveBase(db, "Width", 20, 0);
            db = Reopened(db);

            MoveBase(db, "Width", 10, 0);

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(120.0, ValueOf(insert, "Width"), 9);
            GeoPoint3[] saved = Saved(insert, "BLOCKLINEARPARAMETER");
            Assert.True(At(saved[0], 30, 0));
            Assert.True(At(saved[1], 150, 0));
            Assert.True(Has(insert, 10, 0, 10, 50));
            Assert.True(Has(insert, 10, 50, 150, 50));
            Assert.True(Has(insert, 10, 0, 150, 0));
            Assert.Equal(4, Circles(insert).Count);
            Assert.Contains(Circles(insert), c => At(c, 60, 25));
        }

        // ---- grips, refusals and arguments ---------------------------------------------------------------------------------

        [Fact]
        public void AnXYParameterShowsItsBaseGrip_OnlyWhereAnActionFollowsTheBasePoint()
        {
            // AutoCAD 2021 gave the end, the base and the two corners of the plate whose bottom left corner a stretch of the base
            // point holds, in the order of the graph, and of an XY array (the array follows the base point) too; the plate
            // without that stretch gave the end and the two corners.
            IReadOnlyList<DxfDynamicBlockGrip> plain = TheInsert(Plate(false, false)).GetDynamicGrips();
            Assert.Equal(3, plain.Count);

            IReadOnlyList<DxfDynamicBlockGrip> grips = TheInsert(Plate(true, false)).GetDynamicGrips();
            Assert.Equal(4, grips.Count);
            Assert.True(At(grips[0].Position, 1100, 550));
            Assert.True(At(grips[1].Position, 1000, 500));
            Assert.True(At(grips[1].BasePoint, 1000, 500));
            Assert.True(At(grips[2].Position, 1000, 550));
            Assert.True(At(grips[3].Position, 1100, 500));

            IReadOnlyList<DxfDynamicBlockGrip> array = TheInsert(Grid()).GetDynamicGrips();
            Assert.Equal(4, array.Count);
            Assert.True(At(array[0].Position, 1005, 500));
            Assert.True(At(array[1].Position, 1000, 505));
        }

        [Fact]
        public void TheBasePointOfAPropertyThatIsNotALinearOrXYDistance_IsNotMoved()
        {
            DxfDatabase db = Drawing("GH_SIGN", out DxfBlockTableRecord block);
            DxfLine frame = Line(block, 0, 0, 40, 0);
            DxfLine tick = Line(block, 10, 20, 20, 10);
            block.AddVisibilityParameter(
                "Mark",
                null,
                new GeoPoint3(20, -10, 0),
                new[]
                {
                    new DxfBlockVisibilityState("Frame", new DxfEntity[] { frame }),
                    new DxfBlockVisibilityState("Tick", new DxfEntity[] { tick }),
                }
            );
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(200, 0, 0), block));
            db = Reopened(db);

            Assert.Throws<ArgumentException>(() => MoveBase(db, "Mark", 5, 0));
        }

        [Fact]
        public void ABasePointMovedPastTheEndPoint_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Plate(false, false);
            string before = db.ToText();

            Assert.Throws<ArgumentOutOfRangeException>(() => MoveBase(db, "Width", 100, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MoveBase(db, "Height", 0, 60));
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AMovedInsertReadBackFromItsFile_GivesTheSameValuesAndGrips()
        {
            // AutoCAD 2021 read the moved INSERT back from its file with the values the library gave it (AUDIT finding no error).
            DxfDatabase db = Plate(true, true);
            Set(db, "Height", 60);
            MoveBase(db, "Width", 20, 0);

            DxfDatabase back = Reopened(db);

            Assert.Equal(80.0, ValueOf(TheInsert(back), "Width"), 9);
            Assert.Equal(60.0, ValueOf(TheInsert(back), "Height"), 9);
            Assert.Equal(4, TheInsert(back).GetDynamicGrips().Count);
            Assert.True(At(TheInsert(back).GetDynamicGrips()[1].Position, 1020, 500));
        }
    }
}
