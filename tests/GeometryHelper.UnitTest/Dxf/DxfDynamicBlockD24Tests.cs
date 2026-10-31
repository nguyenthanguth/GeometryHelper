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
    /// A dynamic block made in code (D24b): a linear parameter and a stretch action given to a block of the R2018 drawing of
    /// ezdxf, written as AutoCAD writes them, saved, and read back as AutoCAD 2021 read the same block written by a script
    /// (plugin v17): dynamic, its property, its value set and its allowed values, its graph laid out as AutoCAD laid it out.
    /// </summary>
    public class DxfDynamicBlockD24Tests
    {
        private const double Close = 1E-9;

        /// <summary>
        /// The drawing with the block GH_PANEL, a rectangle 100 by 50 of four lines (the right edge, the bottom, the top, the left
        /// edge, in that order), and an INSERT of it at the origin; nothing dynamic yet.
        /// </summary>
        private static DxfDatabase Panel(out DxfBlockTableRecord block, out DxfLine[] lines)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            lines = new[]
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            return db;
        }

        /// <summary>The panel given Width from (0, 0) to (100, 0) and the stretch of its right edge by the frame (90, -10)-(110, 60).</summary>
        private static DxfDatabase Stretched(DxfBlockParameterValueSet set)
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfLine[] lines);
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", "Overall width of the panel", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, set);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            return db;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().Single(r => r.Name == "GH_PANEL");

        private static DxfDBObject GraphOf(DxfDatabase db) => db.BlockTable["GH_PANEL"].ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");

        /// <summary>The groups of the graph's record after its subclass marker.</summary>
        private static List<DxfTypedValue> GraphGroups(DxfDatabase db)
        {
            DxfDBObject graph = GraphOf(db);
            List<DxfTypedValue> groups = graph.Item.File.TailOf(graph.Item, 0);
            return groups.Skip(groups.FindIndex(g => g.TypeCode == 100 && g.Value == "AcDbEvalGraph") + 1).ToList();
        }

        /// <summary>The objects of the graph's nodes, in its order.</summary>
        private static List<DxfItem> Nodes(DxfDatabase db)
        {
            DxfDBObject graph = GraphOf(db);
            return GraphGroups(db).Where(g => g.TypeCode == 360).Select(g => graph.Item.File.ObjectItem(g.Value)).ToList();
        }

        private static string TypeOf(DxfItem node) => node.Type;

        /// <summary>The values of the groups of a code after a subclass marker, up to the next marker.</summary>
        private static List<string> Of(DxfItem node, string marker, int code)
        {
            IReadOnlyList<DxfTypedValue> record = node.Groups;
            var values = new List<string>();
            bool inside = false;

            foreach (DxfTypedValue group in record)
            {
                if (group.TypeCode == 100 || group.TypeCode == 1001)
                {
                    inside = group.TypeCode == 100 && group.Value == marker;
                }
                else if (inside && group.TypeCode == code)
                {
                    values.Add(group.Value.Trim());
                }
            }

            return values;
        }

        [Fact]
        public void ARectangleGivenAWidthAndAStretch_ReadsBackAsADynamicBlockOfThatWidth()
        {
            DxfDatabase db = Reopened(Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10)));

            DxfBlockTableRecord block = db.BlockTable["GH_PANEL"];
            Assert.True(block.IsDynamicBlock);
            DxfBlockReference insert = TheInsert(db);
            Assert.True(insert.IsDynamicBlock);
            Assert.Equal("GH_PANEL", insert.DynamicBlockTableRecord.Name);

            DxfDynamicBlockReferenceProperty width = Assert.Single(insert.DynamicBlockReferencePropertyCollection);
            Assert.Equal("Width", width.PropertyName);
            Assert.Equal(100.0, Assert.IsType<double>(width.Value), Close);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Distance, width.UnitsType);
            Assert.Equal("Overall width of the panel", width.Description);
            Assert.False(width.ReadOnly);
            Assert.Empty(width.GetAllowedValues());

            // The value set as AutoCAD 2021 read it: the flags 7, from 50 to 300 by 10.
            DxfItem parameter = Nodes(db)[0];
            Assert.Equal("BLOCKLINEARPARAMETER", TypeOf(parameter));
            Assert.Equal(new[] { "7" }, Of(parameter, "AcDbBlockLinearParameter", 96));
            Assert.Equal(new[] { "50" }, Of(parameter, "AcDbBlockLinearParameter", 141));
            Assert.Equal(new[] { "300" }, Of(parameter, "AcDbBlockLinearParameter", 142));
            Assert.Equal(new[] { "10" }, Of(parameter, "AcDbBlockLinearParameter", 143));
            Assert.Equal(new[] { "-20" }, Of(parameter, "AcDbBlockLinearParameter", 140));
        }

        [Fact]
        public void AListIsWrittenUnderTheFlags8_FromTheLeastUp_AndGivenBackAsTheAllowedValues()
        {
            DxfDatabase db = Reopened(Stretched(DxfBlockParameterValueSet.List(new[] { 300.0, 50, 100, 150, 200, 250, 100.0004 })));

            DxfDynamicBlockReferenceProperty width = Assert.Single(TheInsert(db).DynamicBlockReferencePropertyCollection);
            Assert.Equal(new object[] { 50.0, 100.0, 150.0, 200.0, 250.0, 300.0 }, width.GetAllowedValues());
            Assert.Equal(new[] { "8" }, Of(Nodes(db)[0], "AcDbBlockLinearParameter", 96));
            Assert.Equal(new[] { "6" }, Of(Nodes(db)[0], "AcDbBlockLinearParameter", 175));
        }

        [Fact]
        public void NoValueSetIsWrittenAsAutoCadWritesNone()
        {
            DxfDatabase db = Reopened(Stretched(null));

            Assert.Empty(Assert.Single(TheInsert(db).DynamicBlockReferencePropertyCollection).GetAllowedValues());
            DxfItem parameter = Nodes(db)[0];
            Assert.Equal(new[] { "1" }, Of(parameter, "AcDbBlockLinearParameter", 96));
            Assert.Equal(new[] { "0" }, Of(parameter, "AcDbBlockLinearParameter", 175));
        }

        [Fact]
        public void TheGraphIsLaidOutAsAutoCadSavedTheSameBlock()
        {
            // AutoCAD 2021's own save of the block (v17, 24a-a): eight nodes, seven edges, every number as below; only the
            // handles (360) are its own.
            int[] autocad =
            {
                96, 8, 97, 8,
                91, 0, 93, 32, 95, 1, 92, 2, 92, 5, 92, 0, 92, 6,
                91, 1, 93, 32, 95, 2, 92, -1, 92, -1, 92, 2, 92, 2,
                91, 2, 93, 32, 95, 3, 92, 0, 92, 0, 92, -1, 92, -1,
                91, 3, 93, 32, 95, 4, 92, 1, 92, 1, 92, -1, 92, -1,
                91, 4, 93, 32, 95, 5, 92, -1, 92, -1, 92, 5, 92, 5,
                91, 5, 93, 32, 95, 6, 92, 3, 92, 3, 92, -1, 92, -1,
                91, 6, 93, 32, 95, 7, 92, 4, 92, 4, 92, -1, 92, -1,
                91, 7, 93, 32, 95, 8, 92, 6, 92, 6, 92, -1, 92, -1,
                92, 0, 93, 0, 94, 1, 91, 0, 91, 2, 92, -1, 92, -1, 92, -1, 92, 1, 92, -1,
                92, 1, 93, 0, 94, 1, 91, 0, 91, 3, 92, -1, 92, -1, 92, 0, 92, 3, 92, -1,
                92, 2, 93, 0, 94, 2, 91, 1, 91, 0, 92, -1, 92, 5, 92, -1, 92, -1, 92, -1,
                92, 3, 93, 0, 94, 1, 91, 0, 91, 5, 92, -1, 92, -1, 92, 1, 92, 4, 92, -1,
                92, 4, 93, 0, 94, 1, 91, 0, 91, 6, 92, -1, 92, -1, 92, 3, 92, 6, 92, -1,
                92, 5, 93, 0, 94, 2, 91, 4, 91, 0, 92, 2, 92, -1, 92, -1, 92, -1, 92, -1,
                92, 6, 93, 0, 94, 2, 91, 0, 91, 7, 92, -1, 92, -1, 92, 4, 92, -1, 92, -1,
            };

            List<DxfTypedValue> groups = GraphGroups(Reopened(Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10))));
            var numbers = new List<int>();

            foreach (DxfTypedValue group in groups.Where(g => g.TypeCode != 360))
            {
                numbers.Add(group.TypeCode);
                numbers.Add(int.Parse(group.Value.Trim(), CultureInfo.InvariantCulture));
            }

            Assert.Equal(autocad, numbers);
            Assert.Equal(
                new[] { "BLOCKLINEARPARAMETER", "BLOCKLINEARGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKLINEARGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKSTRETCHACTION" },
                Nodes(Reopened(Stretched(null))).Select(TypeOf));
        }

        [Fact]
        public void TheStretchHoldsThePointsInsideItsFrame_TheRightEdgeWholeAndTheRightEndsOfTheBottomAndTop()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfLine[] lines);
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);

            // The corners given the other way round are written the least first; the left edge has no point in the frame and
            // is refused, so it is not given.
            block.AddStretchAction(width, new GeoPoint2(110, 60), new GeoPoint2(90, -10), lines.Take(3));
            DxfItem action = Nodes(Reopened(db))[7];

            Assert.Equal("BLOCKSTRETCHACTION", TypeOf(action));
            Assert.Equal(new[] { lines[0].Handle, lines[1].Handle, lines[2].Handle }, Of(action, "AcDbBlockAction", 330));
            Assert.Equal(new[] { "1" }, Of(action, "AcDbBlockStretchAction", 92));
            Assert.Equal(new[] { "EndXDelta" }, Of(action, "AcDbBlockStretchAction", 301));
            Assert.Equal(new[] { "90", "110" }, Of(action, "AcDbBlockStretchAction", 1011));
            Assert.Equal(new[] { "-10", "60" }, Of(action, "AcDbBlockStretchAction", 1021));
            Assert.Equal(new[] { lines[0].Handle, lines[1].Handle, lines[2].Handle }, Of(action, "AcDbBlockStretchAction", 331));
            Assert.Equal(new[] { "2", "1", "1" }, Of(action, "AcDbBlockStretchAction", 74));
            Assert.Equal(new[] { "0", "1", "1", "1" }, Of(action, "AcDbBlockStretchAction", 94));
        }

        [Fact]
        public void APolylineHoldsItsVerticesInsideTheFrame()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var outline = new DxfPolyline();
            outline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            outline.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            outline.AddVertexAt(2, new GeoPoint2(100, 50), 0, 0, 0);
            outline.AddVertexAt(3, new GeoPoint2(0, 50), 0, 0, 0);
            outline.Closed = true;
            block.AppendEntity(outline);

            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new DxfEntity[] { outline });

            DxfItem action = Nodes(Reopened(db))[7];
            Assert.Equal(new[] { "2" }, Of(action, "AcDbBlockStretchAction", 74));
            Assert.Equal(new[] { "1", "2" }, Of(action, "AcDbBlockStretchAction", 94));
        }

        [Fact]
        public void TwoParametersEachWithAStretch_GiveTwoPropertiesInTheirOrder_AndNamesAsAutoCadNumbersThem()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfLine[] lines);
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10));
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            DxfBlockLinearParameter height = block.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 20, DxfBlockParameterValueSet.List(new[] { 50.0, 80 }));
            block.AddStretchAction(height, new GeoPoint2(-10, 40), new GeoPoint2(110, 60), new[] { lines[0], lines[2], lines[3] });

            DxfDatabase back = Reopened(db);
            IReadOnlyList<DxfDynamicBlockReferenceProperty> properties = TheInsert(back).DynamicBlockReferencePropertyCollection;
            Assert.Equal(new[] { "Width", "Height" }, properties.Select(p => p.PropertyName));
            Assert.Equal(100.0, (double)properties[0].Value, Close);
            Assert.Equal(50.0, (double)properties[1].Value, Close);
            Assert.Equal(new object[] { 50.0, 80.0 }, properties[1].GetAllowedValues());

            List<DxfItem> nodes = Nodes(back);
            Assert.Equal(16, nodes.Count);
            Assert.Equal(new[] { "Linear", "Stretch", "Linear1", "Stretch1" }, nodes.Where(n => TypeOf(n) == "BLOCKLINEARPARAMETER" || TypeOf(n) == "BLOCKSTRETCHACTION").Select(n => Of(n, "AcDbBlockElement", 300).Single()));
            Assert.Equal(Enumerable.Range(1, 16).Select(i => i.ToString(CultureInfo.InvariantCulture)), nodes.Select(n => Of(n, "AcDbEvalExpr", 90).Single()));

            // The second stretch follows the second parameter (node 9): the right edge's top, the top whole, the left edge's top.
            DxfItem second = nodes[15];
            Assert.Equal(new[] { "9" }, Of(second, "AcDbBlockStretchAction", 92));
            Assert.Equal(new[] { "1", "0", "1", "0" }, Of(second, "AcDbBlockStretchAction", 94));

            List<DxfTypedValue> graph = GraphGroups(back);
            Assert.Equal("16", graph[0].Value.Trim());
            Assert.Equal(14, graph.Count(g => g.TypeCode == 94));
        }

        [Fact]
        public void TheBlockRecordAndEveryEntityAreTagged_AndTheClassesListed()
        {
            DxfDatabase db = Reopened(Stretched(null));
            DxfBlockTableRecord block = db.BlockTable["GH_PANEL"];

            Assert.Equal("GH_PANEL", block.GetXDataForApplication("AcDbDynamicBlockTrueName")[1].Value);
            Assert.True(Guid.TryParse(block.GetXDataForApplication("AcDbDynamicBlockGUID")[1].Value, out _));
            Assert.Equal("4", block.GetXDataForApplication("AcDbBlockRepETag")[2].Value.Trim());

            int tag = 0;

            foreach (DxfEntity entity in block)
            {
                IReadOnlyList<DxfTypedValue> data = entity.GetXDataForApplication("AcDbBlockRepETag");
                Assert.Equal((tag++).ToString(CultureInfo.InvariantCulture), data[2].Value.Trim());
                Assert.Equal(entity.Handle, data[3].Value);
            }

            Assert.True(block.ExtensionDictionary.Contains("AcDbDynamicBlockRoundTripPurgePreventer"));
            string text = db.ToText();

            foreach (string type in new[] { "ACAD_EVALUATION_GRAPH", "BLOCKLINEARPARAMETER", "BLOCKLINEARGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKSTRETCHACTION", "ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION" })
            {
                Assert.Contains("  0\nCLASS\n  1\n" + type + "\n", text.Replace("\r\n", "\n"), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheDrawingSavedAndReopened_IsSavedAgainByteForByte()
        {
            string once = Reopened(Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10))).ToText();
            DxfDatabase again = DxfDatabase.Parse(once);

            Assert.Equal(once, again.ToText());
            Assert.Equal(100.0, (double)Assert.Single(TheInsert(again).DynamicBlockReferencePropertyCollection).Value, Close);
        }

        [Fact]
        public void WhatCannotBeWritten_IsRefusedAndNothingIsWritten()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfLine[] lines);
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0.0005, 0, 0), -20, null));
            Assert.Throws<ArgumentException>(() => block.AddLinearParameter(" ", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null));
            Assert.Throws<ArgumentException>(() => block.AddLinearParameter("Width", "two\nlines", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null));
            Assert.Throws<ArgumentException>(() => block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.List(new[] { 50.0, 150 })));
            Assert.Throws<ArgumentException>(() => block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(55, 300, 10)));
            Assert.Throws<ArgumentException>(() => block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(150, 300, 10)));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), double.NaN, null));
            Assert.Throws<InvalidOperationException>(() => db.ModelSpace.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null));
            Assert.Equal(before, db.ToText());

            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            string written = db.ToText();
            var circle = new DxfCircle(new GeoPoint3(100, 25, 0), new GeoVector3(0, 0, 1), 5);
            block.AppendEntity(circle);
            var stray = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            db.ModelSpace.AppendEntity(stray);
            written = db.ToText();

            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines));
            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new DxfEntity[] { lines[0], circle }));
            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new DxfEntity[] { stray }));
            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(90.0005, 60), lines.Take(1)));
            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new DxfEntity[0]));
            Assert.Equal(written, db.ToText());

            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            var old = new DxfBlockTableRecord { Name = "OLD", Origin = new GeoPoint3(0, 0, 0) };
            r2000.BlockTable.Add(old);
            old.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            Assert.Throws<InvalidOperationException>(() => old.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null));
        }

        [Fact]
        public void AValueSetIsCheckedAsItIsMade()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DxfBlockParameterValueSet.Increments(-1, 300, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => DxfBlockParameterValueSet.Increments(300, 300, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => DxfBlockParameterValueSet.Increments(50, 300, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DxfBlockParameterValueSet.List(new[] { 50.0, double.NaN }));
            Assert.Throws<ArgumentException>(() => DxfBlockParameterValueSet.List(new double[0]));
            Assert.Throws<ArgumentNullException>(() => DxfBlockParameterValueSet.List(null));
        }
    }
}
