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
    /// The dynamic blocks of D24e put right after AutoCAD 2021 read them in plugin v21 (D24e2): the edges of a lookup action
    /// flagged as AutoCAD's lookup sample flags them, the copies of an array tagged as AutoCAD tags its own and an XY array of
    /// a count of nought copying nothing, the angle offset of a move or a stretch kept in an INSERT's cache, a flip refused
    /// beside any other parameter, a rotate or a scale refused on an entity a stretch or a move holds, and the labels of a
    /// lookup table kept exactly as AutoCAD keeps them.
    /// </summary>
    public class DxfDynamicBlockD24e2Tests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        /// <summary>A new drawing of R2010 with an empty block of the name and an INSERT of it at the origin.</summary>
        private static DxfDatabase Drawing(string name, out DxfBlockTableRecord block)
        {
            var db = new DxfDatabase("AC1024");
            block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            return db;
        }

        private static T Add<T>(DxfBlockTableRecord block, T entity)
            where T : DxfEntity
        {
            block.AppendEntity(entity);
            return entity;
        }

        /// <summary>The rectangle 100 by 50 of four lines (the right edge, the bottom, the top, the left edge) with Width from (0, 0) to (100, 0).</summary>
        private static DxfDatabase Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines)
        {
            DxfDatabase db = Drawing("GH_PANEL", out block);
            lines = new[]
            {
                Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0))),
            };
            width = block.AddLinearParameter("Width", "Overall width", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            return db;
        }

        /// <summary>The panel of a Width stretching its right edge and a Height stretching its top, looked up by Size: Small 100 by 50, Large 200 by 80.</summary>
        private static DxfDatabase Sized(out DxfBlockTableRecord block)
        {
            DxfDatabase db = Panel(out block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            DxfBlockLinearParameter height = block.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 20, null);
            block.AddStretchAction(height, new GeoPoint2(-10, 40), new GeoPoint2(110, 60), new[] { lines[0], lines[2], lines[3] });
            block.AddLookupParameter("Size", "Panel size", new GeoPoint3(50, -20, 0), new[] { DxfBlockLookupInput.Distance(width), DxfBlockLookupInput.Distance(height) },
                new[] { new DxfBlockLookupRow("Small", 100, 50), new DxfBlockLookupRow("Large", 200, 80) });
            return db;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static List<T> Shown<T>(DxfBlockReference insert) => insert.BlockTableRecord.OfType<T>().ToList();

        private static bool Is(DxfLine line, double x1, double y1, double x2, double y2)
            => line.StartPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), Exact) && line.EndPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), Exact);

        private static bool At(GeoPoint3 point, double x, double y) => point.IsEqualTo(new GeoPoint3(x, y, 0), Exact);

        /// <summary>The objects of the graph's nodes of a block, in its order.</summary>
        private static List<DxfItem> Nodes(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            return graph.Item.Groups.Where(g => g.TypeCode == 360).Select(g => graph.Item.File.ObjectItem(g.Value)).ToList();
        }

        /// <summary>Each edge of the graph of a block, after its nodes: its flags (93), its kind (94) and the indices of the nodes it runs from and to.</summary>
        private static List<int[]> Edges(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            List<DxfTypedValue> groups = graph.Item.File.TailOf(graph.Item, 0);
            List<int> numbers = groups.Skip(groups.FindIndex(g => g.TypeCode == 100 && g.Value == "AcDbEvalGraph") + 1)
                .Where(g => g.TypeCode != 360).Select(g => int.Parse(g.Value.Trim(), CultureInfo.InvariantCulture)).ToList();
            var edges = new List<int[]>();

            for (int at = 2 + Nodes(block).Count * 7; at + 9 < numbers.Count; at += 10)
            {
                edges.Add(new[] { numbers[at + 1], numbers[at + 2], numbers[at + 3], numbers[at + 4] });
            }

            return edges;
        }

        /// <summary>The XRECORDs AutoCAD reads an INSERT's values from, by their node ids, as code and value.</summary>
        private static Dictionary<string, List<string>> Cache(DxfBlockReference insert)
        {
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var data = (DxfDBDictionary)((DxfDBDictionary)representation.GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            return data.ToDictionary(e => e.Key, e => ((DxfXrecord)e.Value).Data.Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim()).ToList());
        }

        // ---- 1. the edges of a lookup action ---------------------------------------------------------------------------------

        [Fact]
        public void TheEdgesBetweenALookupActionAndItsParameters_AreFlaggedFour_AsAutoCadsLookupSampleFlagsThem()
        {
            // AutoCAD 2021 read the library's lookup blocks of D24e, whose edges were all of the flags nought, as not dynamic on
            // any INSERT (plugin v21, 24e-e); its own lookup sample flags 4 each of the eight edges between the lookup action
            // and the parameters it reads or sets, and no other.
            DxfDatabase db = Sized(out DxfBlockTableRecord block);

            List<int[]> edges = Edges(Reopened(db).BlockTable["GH_PANEL"]);
            Assert.Equal(23, edges.Count);
            Assert.All(edges.Take(17), e => Assert.Equal(0, e[0]));
            Assert.Equal(new[] { new[] { 4, 1, 0, 20 }, new[] { 4, 1, 20, 0 }, new[] { 4, 1, 8, 20 }, new[] { 4, 1, 20, 8 }, new[] { 4, 1, 20, 16 }, new[] { 4, 1, 16, 20 } }, edges.Skip(17));
            Assert.Equal("BLOCKLOOKUPACTION", Nodes(block)[20].Type);
        }

        [Fact]
        public void ALookupWrittenAfterAnother_KeepsTheFlagsOfTheEdgesBeforeIt()
        {
            // A second lookup reads the graph back and writes it anew: the first lookup's edges keep their 4.
            DxfDatabase db = Sized(out DxfBlockTableRecord block);
            DxfBlockLinearParameter reach = block.AddLinearParameter("Reach", null, new GeoPoint3(0, 0, 0), new GeoPoint3(60, 0, 0), -40, null);
            block.AddLookupParameter("Span", null, new GeoPoint3(50, -40, 0), new[] { DxfBlockLookupInput.Distance(reach) },
                new[] { new DxfBlockLookupRow("Short", 60), new DxfBlockLookupRow("Long", 90) });

            List<int[]> edges = Edges(block);
            List<DxfItem> nodes = Nodes(block);
            Assert.Equal(6 + 4, edges.Count(e => e[0] == 4));
            Assert.All(edges, e => Assert.Equal(e[0] == 4, nodes[e[2]].Type == "BLOCKLOOKUPACTION" || nodes[e[3]].Type == "BLOCKLOOKUPACTION"));
        }

        // ---- 2. arrays -------------------------------------------------------------------------------------------------------

        [Fact]
        public void EachCopyOfAnArrayButTheFirst_IsTaggedAsAnArraysCopy_AsAutoCadTagsItsOwn()
        {
            // AutoCAD 2021 threw eInvalidInput setting the INSERTs the library had set, whose copies carried the post's tag
            // only (plugin v21, 24e-c, 24e-d, 24e-s2); its own copies carry AcDbBlockArrayActionTag 1070 1 after the tag (its
            // save of 24e-c, and the XY sample's *U6), the entity itself none.
            DxfDatabase db = Drawing("GH_FENCE", out DxfBlockTableRecord block);
            DxfLine rail = Add(block, new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(100, 10, 0)));
            DxfCircle post = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockLinearParameter length = block.AddLinearParameter("Length", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            block.AddStretchAction(length, new GeoPoint2(90, 0), new GeoPoint2(110, 20), new[] { rail });
            block.AddArrayAction(length, new[] { post }, 60);

            Property(TheInsert(db), "Length").Value = 250.0;

            DxfDatabase reopened = Reopened(db);
            List<DxfCircle> posts = Shown<DxfCircle>(TheInsert(reopened));
            Assert.Equal(new[] { 0.0, 60, 120, 180 }, posts.Select(c => c.Center.X));
            Assert.Null(posts[0].GetXDataForApplication("AcDbBlockArrayActionTag"));
            Assert.Null(Assert.Single(Shown<DxfLine>(TheInsert(reopened))).GetXDataForApplication("AcDbBlockArrayActionTag"));

            foreach (DxfCircle copy in posts.Skip(1))
            {
                Assert.Equal(new[] { "1001 AcDbBlockRepETag", "1070 1", "1071 1", "1005 " + copy.Handle, "1001 AcDbBlockArrayActionTag", "1070 1" },
                    copy.XData.Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim()));
            }

            Assert.Contains("AcDbRegAppTableRecord\n  2\nAcDbBlockArrayActionTag\n", reopened.ToText().Replace("\r\n", "\n"), StringComparison.Ordinal);

            // Set again, the INSERT's own anonymous block is filled anew, its copies tagged as before.
            Property(TheInsert(reopened), "Length").Value = 130.0;
            posts = Shown<DxfCircle>(TheInsert(Reopened(reopened)));
            Assert.Equal(new[] { 0.0, 60 }, posts.Select(c => c.Center.X));
            Assert.NotNull(posts[1].GetXDataForApplication("AcDbBlockArrayActionTag"));
        }

        [Fact]
        public void AnXYArrayOfACountOfNought_CopiesNothing_AsAutoCadDrewOneCircleForEightyByFive()
        {
            // AutoCAD 2021 set the XY sample's new INSERT to 80 by 5 (columns of 25, rows of 20) and drew its one circle (plugin
            // v21, 24e-s2): rows of 20 in 5 make none, and an array of no rows copies nothing.
            DxfDatabase db = Drawing("GH_GRID", out DxfBlockTableRecord block);
            DxfCircle dot = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockXYParameter xy = block.AddXYParameter("X Distance1", null, "Y Distance1", null, new GeoPoint3(0, 5, 0), new GeoPoint3(5, 0, 0));
            block.AddArrayAction(xy, new[] { dot }, 25, 20);

            Property(TheInsert(db), "X Distance1").Value = 80.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(80.0, Assert.IsType<double>(Property(back, "X Distance1").Value), Close);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 0, 0));

            // AutoCAD drew two bolts for 60 by 30 of columns of 25 and rows of 20 (24e-d), one column for 40 by 70 and three rows.
            DxfDatabase plate = Drawing("GH_PLATE", out DxfBlockTableRecord plateBlock);
            DxfCircle bolt = Add(plateBlock, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockXYParameter grid = plateBlock.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(40, 30, 0));
            plateBlock.AddArrayAction(grid, new[] { bolt }, 25, 20);
            Property(TheInsert(plate), "Width").Value = 60.0;
            Assert.Equal(new[] { 0.0, 25 }, Shown<DxfCircle>(TheInsert(Reopened(plate))).Select(c => c.Center.X));
            Property(TheInsert(plate), "Width").Value = 40.0;
            Property(TheInsert(plate), "Depth").Value = 70.0;
            Assert.Equal(new[] { 0.0, 20, 40 }, Shown<DxfCircle>(TheInsert(Reopened(plate))).Select(c => c.Center.Y));
        }

        // ---- 3. the angle offset in the cache --------------------------------------------------------------------------------

        [Fact]
        public void TheCacheOfAMoveOrAStretch_KeepsItsAngleOffset_AsAutoCadKeptAQuarterTurnIn40()
        {
            // AutoCAD 2021 moved the circle of the offset of a quarter turn along x, to (-30, 125), from the INSERT the library
            // had set to Width 200 and written 40 nought for (plugin v21, 24e-b); its own save of the INSERT it set from the block
            // keeps the move's offset, 1.5707963267948966, in the 40 of the move's record, and nought for the others.
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            DxfLine inner = Add(block, new DxfLine(new GeoPoint3(50, 0, 0), new GeoPoint3(50, 50, 0)));
            block.AddStretchAction(width, new GeoPoint2(45, -10), new GeoPoint2(55, 60), new[] { inner }, 0.5, 0.25);
            DxfCircle middle = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5));
            block.AddMoveAction(width, new[] { middle }, 0.5, 0);
            DxfCircle turned = Add(block, new DxfCircle(new GeoPoint3(20, 25, 0), GeoVector3.ZAxis, 3));
            block.AddMoveAction(width, new[] { turned }, 1, Math.PI / 2);

            Property(TheInsert(db), "Width").Value = 200.0;

            Dictionary<string, List<string>> cache = Cache(TheInsert(Reopened(db)));
            List<string> ids = Nodes(block).Where(n => n.Type == "BLOCKSTRETCHACTION" || n.Type == "BLOCKMOVEACTION")
                .Select(n => n.Groups.First(g => g.TypeCode == 90).Value.Trim()).ToList();
            Assert.Equal(4, ids.Count);
            Assert.Equal("40 0", cache[ids[0]].Last());
            Assert.Equal("40 0.25", cache[ids[1]].Last());
            Assert.Equal("40 0", cache[ids[2]].Last());
            Assert.Equal(Math.PI / 2, double.Parse(cache[ids[3]].Last().Substring(3), CultureInfo.InvariantCulture));
            Assert.True(At(Shown<DxfCircle>(TheInsert(Reopened(db)))[1].Center, 20, 125));
        }

        // ---- 4. a flip beside another parameter -----------------------------------------------------------------------------

        [Fact]
        public void AFlipParameter_IsRefusedInABlockOfAnotherParameter_AsAutoCadDidNotFlipSuchABlockInV21()
        {
            // AutoCAD 2021 set Hand to 1 on INSERTs of a door whose block had a linear parameter of no action beside the flip,
            // and read 0 back each time (plugin v21, 24e-h, and the old 24d-i and its own save of it); a door of the flip alone
            // flipped both ways (plugin v20, 24d-g).
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out _, out _);
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => block.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)));
            Assert.Equal(before, db.ToText());

            DxfDatabase door = Drawing("GH_DOOR", out DxfBlockTableRecord doorBlock);
            DxfLine leaf = Add(doorBlock, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0)));
            DxfBlockFlipParameter hand = doorBlock.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));
            doorBlock.AddFlipAction(hand, new[] { leaf });
            string flipped = door.ToText();

            var origin = new GeoPoint3(0, 0, 0);
            Assert.Throws<NotSupportedException>(() => doorBlock.AddLinearParameter("Reach", null, origin, new GeoPoint3(90, 0, 0), -10, null));
            Assert.Throws<NotSupportedException>(() => doorBlock.AddPointParameter("Tag", null, origin));
            Assert.Throws<NotSupportedException>(() => doorBlock.AddRotationParameter("Turn", null, origin, 10, 0));
            Assert.Throws<NotSupportedException>(() => doorBlock.AddXYParameter("Width", null, "Depth", null, origin, new GeoPoint3(90, 50, 0)));
            Assert.Throws<NotSupportedException>(() => doorBlock.AddFlipParameter("Side", null, origin, new GeoPoint3(50, 0, 0)));
            Assert.Throws<NotSupportedException>(() => doorBlock.AddVisibilityParameter("Look", null, origin, new[] { new DxfBlockVisibilityState("All", new[] { leaf }) }));
            Assert.Throws<NotSupportedException>(() => doorBlock.AddAlignmentParameter(origin, new GeoPoint3(50, 0, 0)));
            Assert.Throws<NotSupportedException>(() => doorBlock.AddBasePointParameter(origin));
            Assert.Equal(flipped, door.ToText());

            // The door of the flip alone is written and flips, as AutoCAD flipped it.
            Property(TheInsert(door), "Hand").Value = (short)1;
            Assert.True(Is(Assert.Single(Shown<DxfLine>(TheInsert(Reopened(door)))), 0, 0, -90, 0));
        }

        // ---- 5. a rotate or a scale of an entity a stretch or a move holds ---------------------------------------------------

        [Fact]
        public void ARotateOrAScaleOfAnEntityAStretchOrAMoveHolds_IsRefusedEitherWayRound_AsAutoCadWorkedItFromTheLastStateInV21()
        {
            // AutoCAD 2021 stretched a shaft turned a quarter turn along x, to (0, 155) for a Reach of 150 set after 200 (plugin
            // v21, 24e-g), and scaled a line it stretched from where its last set left it: no value of such a block tells its
            // shape.
            DxfDatabase db = Drawing("GH_ROT", out DxfBlockTableRecord block);
            DxfLine shaft = Add(block, new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0)));
            DxfCircle hub = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 3));
            DxfCircle tip = Add(block, new DxfCircle(new GeoPoint3(80, 25, 0), GeoVector3.ZAxis, 2));
            DxfBlockLinearParameter reach = block.AddLinearParameter("Reach", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            DxfBlockRotationParameter angle = block.AddRotationParameter("Angle", null, new GeoPoint3(50, 25, 0), 30, 0);
            DxfBlockLinearParameter size = block.AddLinearParameter("Size", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0), 20, null);
            block.AddStretchAction(reach, new GeoPoint2(70, 15), new GeoPoint2(90, 35), new[] { shaft });
            block.AddMoveAction(reach, new[] { tip });
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => block.AddRotateAction(angle, new[] { shaft }));
            Assert.Throws<NotSupportedException>(() => block.AddScaleAction(size, new DxfEntity[] { hub, shaft }));
            Assert.Throws<NotSupportedException>(() => block.AddRotateAction(angle, new[] { tip }));
            Assert.Throws<NotSupportedException>(() => block.AddScaleAction(size, new[] { tip }));
            Assert.Equal(before, db.ToText());

            // The other way round: a turned hub refuses a stretch or a move of it, a scaled one too.
            block.AddRotateAction(angle, new[] { hub });
            string turned = db.ToText();
            Assert.Throws<NotSupportedException>(() => block.AddStretchAction(reach, new GeoPoint2(40, 15), new GeoPoint2(60, 35), new[] { hub }));
            Assert.Throws<NotSupportedException>(() => block.AddMoveAction(reach, new[] { hub }));
            Assert.Equal(turned, db.ToText());

            DxfDatabase scaled = Drawing("GH_SCL", out DxfBlockTableRecord scaledBlock);
            DxfLine line = Add(scaledBlock, new DxfLine(new GeoPoint3(20, 10, 0), new GeoPoint3(60, 10, 0)));
            DxfBlockLinearParameter w = scaledBlock.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            DxfBlockLinearParameter s = scaledBlock.AddLinearParameter("Size", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0), 20, null);
            scaledBlock.AddScaleAction(s, new[] { line });
            string scaledText = scaled.ToText();
            Assert.Throws<NotSupportedException>(() => scaledBlock.AddStretchAction(w, new GeoPoint2(50, 0), new GeoPoint2(70, 20), new[] { line }));
            Assert.Throws<NotSupportedException>(() => scaledBlock.AddMoveAction(w, new[] { line }));
            Assert.Equal(scaledText, scaled.ToText());

            // A turn and a stretch of entities apart are written and worked out, as the shaft and the hub are.
            Property(TheInsert(db), "Reach").Value = 150.0;
            Property(TheInsert(db), "Angle").Value = Math.PI / 2;
            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.True(Is(Assert.Single(Shown<DxfLine>(back)), 50, 25, 130, 25));
            Assert.True(At(Shown<DxfCircle>(back)[1].Center, 130, 25));
        }

        [Fact]
        public void AnInsertOfABlockThatTurnsAnEntityItStretches_IsNotSet_AsAutoCadWorkedItFromTheLastState()
        {
            // A block of another writer may turn the entity it stretches, as AutoCAD's Block Editor lets it: the library does
            // not work such a block out, as AutoCAD 2021 worked it out from the INSERT's last state (plugin v21, 24e-g).
            DxfDatabase db = Drawing("GH_ROT", out DxfBlockTableRecord block);
            DxfLine shaft = Add(block, new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0)));
            DxfCircle hub = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 3));
            DxfBlockLinearParameter reach = block.AddLinearParameter("Reach", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            DxfBlockRotationParameter angle = block.AddRotationParameter("Angle", null, new GeoPoint3(50, 25, 0), 30, 0);
            block.AddStretchAction(reach, new GeoPoint2(70, 15), new GeoPoint2(90, 35), new[] { shaft });
            block.AddRotateAction(angle, new[] { hub });
            DxfItem rotate = Nodes(block).Last();
            DxfFile file = rotate.File;
            var groups = file.TailOf(rotate, 0).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            int marker = groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbBlockAction");
            int count = groups.FindIndex(marker, g => g.Key == 71);
            groups[count] = new KeyValuePair<int, string>(71, "2");
            groups.Insert(groups.FindLastIndex(g => g.Key == 330) + 1, new KeyValuePair<int, string>(330, shaft.Handle));
            file.ReplaceTail(rotate, 0, groups);
            DxfDatabase reopened = Reopened(db);
            string before = reopened.ToText();

            Assert.Throws<NotSupportedException>(() => Property(TheInsert(reopened), "Reach").Value = 150.0);
            Assert.Throws<NotSupportedException>(() => Property(TheInsert(reopened), "Angle").Value = Math.PI / 2);
            Assert.Equal(before, reopened.ToText());
        }

        // ---- 6b. the lookup of an INSERT of the block itself ---------------------------------------------------------------

        [Fact]
        public void AnInsertOfTheBlockItself_ReadsTheFirstRowOfTheBlocksOwnValues_AsAutoCadReadItInV21b()
        {
            // AutoCAD 2021 read Size "Small" on the INSERTs of the block itself, of no cache, whose Width 100 and Height 50 are
            // the row Small's, and Corner "Low" for a Tag at (80, 20) (plugin v21b, 24e-e and 24e-j), where the library read
            // Custom; Custom stays for values no row holds.
            DxfDatabase db = Sized(out DxfBlockTableRecord block);
            DxfDynamicBlockReferenceProperty size = Property(TheInsert(db), "Size");
            Assert.Equal("Small", size.Value);
            Assert.Equal(new object[] { "Small", "Large" }, size.GetAllowedValues());

            DxfDatabase corner = Drawing("GH_CORNER", out DxfBlockTableRecord tagged);
            DxfCircle circle = Add(tagged, new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5));
            DxfBlockPointParameter tag = tagged.AddPointParameter("Tag", null, new GeoPoint3(80, 20, 0));
            tagged.AddMoveAction(tag, new[] { circle });
            tagged.AddLookupParameter("Corner", null, new GeoPoint3(0, -10, 0), new[] { DxfBlockLookupInput.X(tag), DxfBlockLookupInput.Y(tag) },
                new[] { new DxfBlockLookupRow("High", 80, 45), new DxfBlockLookupRow("Low", 80, 20), new DxfBlockLookupRow("Twin", 80, 20) });
            Assert.Equal("Low", Property(TheInsert(Reopened(corner)), "Corner").Value);

            // A block whose own values are no row's reads the label of no match, allowed then too.
            DxfDatabase other = Panel(out DxfBlockTableRecord panel, out DxfBlockLinearParameter width, out _);
            panel.AddLookupParameter("Size", null, new GeoPoint3(50, -20, 0), new[] { DxfBlockLookupInput.Distance(width) },
                new[] { new DxfBlockLookupRow("Small", 80), new DxfBlockLookupRow("Large", 200) });
            DxfDynamicBlockReferenceProperty unmatched = Property(TheInsert(Reopened(other)), "Size");
            Assert.Equal("Custom", unmatched.Value);
            Assert.Equal(new object[] { "Small", "Large", "Custom" }, unmatched.GetAllowedValues());

            // A set of another property of such an INSERT keeps the lookup as it reads, and its row is set back by its label.
            Property(TheInsert(db), "Width").Value = 200.0;
            Property(TheInsert(db), "Height").Value = 80.0;
            Assert.Equal("Large", Property(TheInsert(Reopened(db)), "Size").Value);
            Assert.Equal(block.Name, TheInsert(db).DynamicBlockTableRecord.Name);
        }

        // ---- 6. a label kept exactly -----------------------------------------------------------------------------------------

        [Fact]
        public void ALabelOfALookupTable_IsKeptExactly_TrailingSpaceAndAll_AsAutoCadKeepsSizeSixOfItsSample()
        {
            // AutoCAD's lookup sample labels a row "Size 6 ", and AutoCAD 2021 read it so (plugin v21, 24e-s3), where the library
            // set the INSERT and kept "Size 6" in its cache.
            DxfDatabase db = Sized(out DxfBlockTableRecord block);
            DxfItem action = Nodes(block).Last();
            DxfFile file = action.File;
            var groups = file.TailOf(action, 0).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            int small = groups.FindIndex(g => g.Key == 302 && g.Value == "Small");
            groups[small] = new KeyValuePair<int, string>(302, "Small ");
            file.ReplaceTail(action, 0, groups);
            DxfDatabase reopened = Reopened(db);

            DxfDynamicBlockReferenceProperty size = Property(TheInsert(reopened), "Size");
            Assert.Equal("Small ", size.Value);
            Assert.Equal(new object[] { "Small ", "Large" }, size.GetAllowedValues());
            size.Value = "Large";
            Property(TheInsert(reopened), "Size").Value = "Small";

            DxfDatabase third = Reopened(reopened);
            DxfBlockReference back = TheInsert(third);
            Assert.Equal("Small ", Property(back, "Size").Value);
            Assert.Equal(100.0, Assert.IsType<double>(Property(back, "Width").Value), Close);

            // Set by its inputs, the INSERT shows the row's label as the table holds it.
            Property(back, "Width").Value = 200.0;
            Property(back, "Height").Value = 80.0;
            Property(back, "Width").Value = 100.0;
            Property(back, "Height").Value = 50.0;
            Assert.Equal("Small ", Property(TheInsert(Reopened(third)), "Size").Value);
        }
    }
}
