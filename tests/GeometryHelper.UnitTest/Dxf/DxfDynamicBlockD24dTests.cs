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
    /// The other dynamic blocks AutoCAD makes, made from code (D24d): move and scale actions of a linear parameter, stretches of
    /// arcs, circles, texts and INSERTs, a point parameter with a move, a rotation parameter with a rotate, a flip parameter with
    /// a flip, an XY parameter with a stretch and a move; each written number for number as AutoCAD's own saved samples (ACadSharp's
    /// samples saved by AutoCAD 2021), read back as a property and set on an INSERT as AutoCAD sets it. And the tie rule of an
    /// increment set AutoCAD 2021 showed in plugin v19: a value half way between two steps goes to the step below.
    /// </summary>
    public class DxfDynamicBlockD24dTests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        /// <summary>A new drawing of R2010 with an empty block of the name and an INSERT of it at the origin.</summary>
        private static DxfDatabase Drawing(string name, out DxfBlockTableRecord block, string version = "AC1024")
        {
            var db = new DxfDatabase(version);
            block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            return db;
        }

        /// <summary>Appends entities to a block, in order, and gives them back.</summary>
        private static T Add<T>(DxfBlockTableRecord block, T entity)
            where T : DxfEntity
        {
            block.AppendEntity(entity);
            return entity;
        }

        /// <summary>
        /// The rectangle 100 by 50 of four lines (the right edge, the bottom, the top, the left edge), with Width from (0, 0) to
        /// (100, 0) of the value set given and the stretch of its right edge by the frame (90, -10)-(110, 60).
        /// </summary>
        private static DxfDatabase Panel(DxfBlockParameterValueSet set, out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines)
        {
            DxfDatabase db = Drawing("GH_PANEL", out block);
            lines = new[]
            {
                Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0))),
                Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0))),
            };
            width = block.AddLinearParameter("Width", "Overall width", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, set);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            return db;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static double ValueOf(DxfBlockReference insert, string name) => Assert.IsType<double>(Property(insert, name).Value);

        /// <summary>The entities of a type the INSERT shows, in the order of the block it places.</summary>
        private static List<T> Shown<T>(DxfBlockReference insert) => insert.BlockTableRecord.OfType<T>().ToList();

        private static bool Is(DxfLine line, double x1, double y1, double x2, double y2)
            => line.StartPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), Exact) && line.EndPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), Exact);

        private static bool At(GeoPoint3 point, double x, double y) => point.IsEqualTo(new GeoPoint3(x, y, 0), Exact);

        /// <summary>A text of the height 2.5 standing at a point, left on its baseline.</summary>
        private static DxfDBText Text(double x, double y, string words) => new DxfDBText { Position = new GeoPoint3(x, y, 0), TextString = words, Height = 2.5 };

        /// <summary>The objects of the graph's nodes of a block, in its order.</summary>
        private static List<DxfItem> Nodes(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            return graph.Item.Groups.Where(g => g.TypeCode == 360).Select(g => graph.Item.File.ObjectItem(g.Value)).ToList();
        }

        /// <summary>The numbers of the graph's record after its subclass marker, but the handles of its nodes.</summary>
        private static List<int> GraphNumbers(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            List<DxfTypedValue> groups = graph.Item.File.TailOf(graph.Item, 0);
            var numbers = new List<int>();

            foreach (DxfTypedValue group in groups.Skip(groups.FindIndex(g => g.TypeCode == 100 && g.Value == "AcDbEvalGraph") + 1).Where(g => g.TypeCode != 360))
            {
                numbers.Add(group.TypeCode);
                numbers.Add(int.Parse(group.Value.Trim(), CultureInfo.InvariantCulture));
            }

            return numbers;
        }

        /// <summary>The codes of a node's record from its AcDbEvalExpr up to its extended data, as the AutoCAD samples are compared.</summary>
        private static int[] Codes(DxfItem node)
        {
            List<DxfTypedValue> groups = node.Groups.ToList();
            int from = groups.FindIndex(g => g.TypeCode == 100 && g.Value == "AcDbEvalExpr");
            int to = groups.FindIndex(from, g => g.TypeCode == 1001);
            return groups.Skip(from).Take((to < 0 ? groups.Count : to) - from).Select(g => (int)g.TypeCode).ToArray();
        }

        /// <summary>The values of the groups of a code after a subclass marker, up to the next marker.</summary>
        private static List<string> Of(DxfItem node, string marker, int code)
        {
            var values = new List<string>();
            bool inside = false;

            foreach (DxfTypedValue group in node.Groups)
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

        /// <summary>The XRECORDs AutoCAD reads an INSERT's values from, by their node ids, as code and value.</summary>
        private static Dictionary<string, List<string>> Cache(DxfBlockReference insert)
        {
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var data = (DxfDBDictionary)((DxfDBDictionary)representation.GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            return data.ToDictionary(e => e.Key, e => ((DxfXrecord)e.Value).Data.Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim()).ToList());
        }

        // ---- the tie rule of an increment set --------------------------------------------------------------------------------

        [Theory]
        [InlineData(250.0, 85.0, 80.0)]
        [InlineData(80.0, 65.0, 60.0)]
        [InlineData(60.0, 75.0, 70.0)]
        [InlineData(100.0, 75.0, 70.0)]
        [InlineData(100.0, 75.0004, 70.0)]
        [InlineData(100.0, 75.002, 80.0)]
        [InlineData(100.0, 84.998, 80.0)]
        [InlineData(100.0, 86.0, 90.0)]
        public void AValueHalfWayBetweenTwoSteps_GoesToTheStepBelow_AsAutoCadTookItInV19(double from, double given, double taken)
        {
            DxfDatabase db = Panel(DxfBlockParameterValueSet.Increments(50, 300, 10), out _, out _, out _);
            DxfDynamicBlockReferenceProperty width = Property(TheInsert(db), "Width");
            width.Value = from;

            width.Value = given;

            Assert.Equal(taken, (double)width.Value, Close);
            Assert.Equal(taken, ValueOf(TheInsert(Reopened(db)), "Width"), Close);
        }

        // ---- move and scale actions of a linear parameter --------------------------------------------------------------------

        [Fact]
        public void AMoveOfALinearParameter_MovesItsCircleWithTheEndPoint_AndIsWrittenAsAutoCadWritesAMove()
        {
            DxfDatabase db = Panel(null, out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            DxfCircle tag = Add(block, new DxfCircle(new GeoPoint3(90, 25, 0), GeoVector3.ZAxis, 5));
            block.AddMoveAction(width, new[] { tag });

            Property(TheInsert(db), "Width").Value = 200.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(200.0, ValueOf(back, "Width"), Close);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 190, 25));
            Assert.True(Is(Shown<DxfLine>(back)[0], 200, 0, 200, 50));
            Assert.True(At(block.OfType<DxfCircle>().Single().Center, 90, 25));

            // The move as AutoCAD saved the move of its point sample: one entity, its connections, multiplier 1, no angle, XY type 0.
            DxfItem move = Nodes(back.DynamicBlockTableRecord).Last();
            Assert.Equal("BLOCKMOVEACTION", move.Type);
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 70, 71, 330, 1010, 1020, 1030, 100, 92, 301, 93, 302, 140, 141, 280 }, Codes(move));
            Assert.Equal(new[] { "Move" }, Of(move, "AcDbBlockElement", 300));
            Assert.Equal(new[] { "1" }, Of(move, "AcDbBlockMoveAction", 92));
            Assert.Equal(new[] { "EndXDelta" }, Of(move, "AcDbBlockMoveAction", 301));
            Assert.Equal(new[] { "EndYDelta" }, Of(move, "AcDbBlockMoveAction", 302));
            Assert.Equal(new[] { "1.0" }, Of(move, "AcDbBlockMoveAction", 140));
            Assert.Equal(new[] { "0" }, Of(move, "AcDbBlockMoveAction", 280));
            Assert.Equal(new[] { tag.Handle }, Of(move, "AcDbBlockAction", 330));

            // The edge from the parameter to the move carries two connections (94 of 2), as the sample's.
            List<int> graph = GraphNumbers(back.DynamicBlockTableRecord);
            Assert.Equal(new[] { 92, 7, 93, 0, 94, 2, 91, 0, 91, 8 }, graph.Skip(graph.Count - 20).Take(10));
            Assert.Equal(new[] { "1071 1553528", "1071 2094574", "70 25", "70 104", "40 0" }, Cache(back)["9"]);
        }

        [Fact]
        public void AScaleOfALinearParameter_ScalesItsCircleAboutTheBasePoint_AndIsWrittenAsAutoCadWritesAScale()
        {
            DxfDatabase db = Drawing("GH_DISC", out DxfBlockTableRecord block);
            DxfCircle disc = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 25));
            DxfBlockLinearParameter size = block.AddLinearParameter("Size", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            block.AddScaleAction(size, new[] { disc });

            Property(TheInsert(db), "Size").Value = 200.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(200.0, ValueOf(back, "Size"), Close);
            DxfCircle shown = Assert.Single(Shown<DxfCircle>(back));
            Assert.True(At(shown.Center, 100, 50));
            Assert.Equal(50.0, shown.Radius, Close);

            DxfItem scale = Nodes(back.DynamicBlockTableRecord).Last();
            Assert.Equal("BLOCKSCALEACTION", scale.Type);
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 70, 71, 330, 1010, 1020, 1030, 100, 92, 93, 301, 302, 1011, 1021, 1031, 280, 1012, 1022, 1032, 100, 94, 95, 96, 303, 304, 305 }, Codes(scale));
            Assert.Equal(new[] { "UpdatedBaseX" }, Of(scale, "AcDbBlockActionWithBasePt", 301));
            Assert.Equal(new[] { "1" }, Of(scale, "AcDbBlockActionWithBasePt", 280));
            Assert.Equal(new[] { "Scale" }, Of(scale, "AcDbBlockScaleAction", 303));
            Assert.Equal(new[] { "YScale" }, Of(scale, "AcDbBlockScaleAction", 305));

            // The sample's graph: the edge to the scale carries five connections.
            List<int> graph = GraphNumbers(back.DynamicBlockTableRecord);
            Assert.Equal(new[] { 92, 6, 93, 0, 94, 5, 91, 0, 91, 7 }, graph.Skip(graph.Count - 20).Take(10));
            Assert.Equal(new[] { "1071 2551693", "1071 3435815", "70 25", "70 104", "10 0", "20 0", "30 0" }, Cache(back)["8"]);
        }

        // ---- stretches of more kinds of entity -------------------------------------------------------------------------------

        [Fact]
        public void AStretchFrameHoldingAnArcACircleATextAMultilineTextAndAnInsert_MovesEachWhole()
        {
            DxfDatabase db = Panel(null, out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            var bolt = new DxfBlockTableRecord { Name = "GH_BOLT", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(bolt);
            bolt.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 2));
            DxfArc arc = Add(block, new DxfArc(new GeoPoint3(95, 25, 0), 4, 0, Math.PI));
            DxfCircle circle = Add(block, new DxfCircle(new GeoPoint3(95, 10, 0), GeoVector3.ZAxis, 3));
            DxfDBText text = Add(block, Text(92, 40, "W"));
            DxfMText note = Add(block, new DxfMText { Location = new GeoPoint3(93, 45, 0), Contents = "N", TextHeight = 2.5 });
            DxfBlockReference insert = Add(block, new DxfBlockReference(new GeoPoint3(98, 30, 0), bolt));
            block.AddStretchAction(width, new GeoPoint2(88, -10), new GeoPoint2(110, 60), new DxfEntity[] { arc, circle, text, note, insert });

            Property(TheInsert(db), "Width").Value = 200.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.True(Is(Shown<DxfLine>(back)[0], 200, 0, 200, 50));
            DxfArc arcShown = Assert.Single(Shown<DxfArc>(back));
            Assert.True(At(arcShown.Center, 195, 25));
            Assert.Equal(4.0, arcShown.Radius, Close);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 195, 10));
            Assert.True(At(Assert.Single(Shown<DxfDBText>(back)).Position, 192, 40));
            Assert.True(At(Assert.Single(Shown<DxfMText>(back)).Location, 193, 45));
            Assert.True(At(Assert.Single(Shown<DxfBlockReference>(back)).Position, 198, 30));

            // The points the stretch numbers: the arc's two ends, the one point of the others, as AutoCAD's files write them.
            DxfItem stretch = Nodes(back.DynamicBlockTableRecord).Last();
            List<string> indices = Of(stretch, "AcDbBlockStretchAction", 94);
            Assert.Equal(new[] { "0", "1", "0", "0", "0", "0" }, indices);
            Assert.Equal(new[] { "2", "1", "1", "1", "1" }, Of(stretch, "AcDbBlockStretchAction", 74));
        }

        [Fact]
        public void AJustifiedText_IsHeldByItsAlignmentPoint()
        {
            DxfDatabase db = Panel(null, out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            DxfDBText label = Add(block, Text(60, 25, "R"));
            label.HorizontalMode = DxfTextHorizontalMode.TextRight;
            label.AlignmentPoint = new GeoPoint3(95, 25, 0);

            // Its position stands outside the frame, its alignment point inside: it is held, and moves whole.
            block.AddStretchAction(width, new GeoPoint2(90, 20), new GeoPoint2(110, 30), new[] { label });
            Property(TheInsert(db), "Width").Value = 150.0;

            DxfDBText shown = Assert.Single(Shown<DxfDBText>(TheInsert(Reopened(db))));
            Assert.True(At(shown.AlignmentPoint, 145, 25));
            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(55, 20), new GeoPoint2(65, 30), new[] { label }));
        }

        [Fact]
        public void WhatAStretchDoesNotTake_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Panel(null, out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            DxfArc arc = Add(block, new DxfArc(new GeoPoint3(95, 25, 0), new GeoVector3(1, 0, 1), 10, 0, Math.PI));
            DxfDBText fitted = Add(block, Text(92, 40, "F"));
            fitted.HorizontalMode = DxfTextHorizontalMode.TextFit;
            fitted.AlignmentPoint = new GeoPoint3(98, 40, 0);
            var definition = Add(block, new DxfAttributeDefinition(new GeoPoint3(95, 5, 0), "x", "TAG", "Tag?", null));
            string before = db.ToText();

            // The tilted arc's end (95, 15) is in the frame, its start (95, 35) is not: an arc held by one end lies flat (D24e).
            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 30), new[] { arc }));
            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(80, -10), new GeoPoint2(110, 60), new[] { fitted }));
            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(80, -10), new GeoPoint2(110, 60), new[] { definition }));
            Assert.Equal(before, db.ToText());
        }

        // ---- a point parameter and a move ------------------------------------------------------------------------------------

        /// <summary>A rectangle 100 by 50 of a closed polyline, with a tag circle of the radius 5 at (80, 20) moved by the point parameter Tag at its centre.</summary>
        private static DxfDatabase Tagged(out DxfBlockTableRecord block, out DxfBlockPointParameter tag, out DxfCircle circle)
        {
            DxfDatabase db = Drawing("GH_TAGGED", out block);
            var frame = new DxfPolyline();
            frame.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            frame.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            frame.AddVertexAt(2, new GeoPoint2(100, 50), 0, 0, 0);
            frame.AddVertexAt(3, new GeoPoint2(0, 50), 0, 0, 0);
            frame.Closed = true;
            Add(block, frame);
            circle = Add(block, new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5));
            tag = block.AddPointParameter("Tag", "Where the tag stands", new GeoPoint3(80, 20, 0));
            block.AddMoveAction(tag, new[] { circle });
            return db;
        }

        [Fact]
        public void APointParameter_GivesTheInsertItsXAndY_AndASetMovesTheCircleWithIt()
        {
            DxfDatabase db = Tagged(out _, out DxfBlockPointParameter tag, out _);
            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(new[] { "Tag X", "Tag Y" }, insert.DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
            Assert.Equal(80.0, ValueOf(insert, "Tag X"), Close);
            Assert.Equal(20.0, ValueOf(insert, "Tag Y"), Close);
            Assert.Equal("Where the tag stands", Property(insert, "Tag Y").Description);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Distance, Property(insert, "Tag X").UnitsType);
            Assert.Equal("Tag", tag.Name);

            Property(insert, "Tag X").Value = 120.0;
            Property(insert, "Tag Y").Value = 35;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(120.0, ValueOf(back, "Tag X"), Close);
            Assert.Equal(35.0, ValueOf(back, "Tag Y"), Close);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 120, 35));
            Assert.Equal(new[] { "1071 11324608", "1071 15407465", "70 25", "70 104", "10 120", "20 35", "30 0" }, Cache(back)["1"]);
            Assert.Equal(new[] { "1071 1553528", "1071 2094574", "70 25", "70 104", "40 0" }, Cache(back)["5"]);
        }

        [Fact]
        public void APointParameterAndItsMove_AreLaidOutAsAutoCadSavedThemInItsSample()
        {
            DxfDatabase db = Reopened(Tagged(out _, out _, out _));
            DxfBlockTableRecord block = db.BlockTable["GH_TAGGED"];
            List<DxfItem> nodes = Nodes(block);

            Assert.Equal(new[] { "BLOCKPOINTPARAMETER", "BLOCKXYGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKMOVEACTION" }, nodes.Select(n => n.Type));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 280, 281, 100, 1010, 1020, 1030, 93, 170, 91, 301, 171, 92, 302, 100, 303, 304, 1011, 1021, 1031 }, Codes(nodes[0]));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 91, 92, 1010, 1020, 1030, 280, 93, 100 }, Codes(nodes[1]));
            Assert.Equal(new[] { 100, 90, 98, 99, 1, 70, 140, 100, 91, 300 }, Codes(nodes[2]));
            Assert.Equal(new[] { "2" }, Of(nodes[0], "AcDbBlock1PtParameter", 93));
            Assert.Equal(new[] { "UpdatedX" }, Of(nodes[2], "AcDbBlockGripExpr", 300));
            Assert.Equal(new[] { "XDelta" }, Of(nodes[4], "AcDbBlockMoveAction", 301));
            Assert.Equal(new[] { "YDelta" }, Of(nodes[4], "AcDbBlockMoveAction", 302));

            // AutoCAD's save of its point sample, number for number but the handles.
            int[] autocad =
            {
                96, 5, 97, 5,
                91, 0, 93, 32, 95, 1, 92, 2, 92, 2, 92, 0, 92, 3,
                91, 1, 93, 32, 95, 2, 92, -1, 92, -1, 92, 2, 92, 2,
                91, 2, 93, 32, 95, 3, 92, 0, 92, 0, 92, -1, 92, -1,
                91, 3, 93, 32, 95, 4, 92, 1, 92, 1, 92, -1, 92, -1,
                91, 4, 93, 32, 95, 5, 92, 3, 92, 3, 92, -1, 92, -1,
                92, 0, 93, 0, 94, 1, 91, 0, 91, 2, 92, -1, 92, -1, 92, -1, 92, 1, 92, -1,
                92, 1, 93, 0, 94, 1, 91, 0, 91, 3, 92, -1, 92, -1, 92, 0, 92, 3, 92, -1,
                92, 2, 93, 0, 94, 2, 91, 1, 91, 0, 92, -1, 92, -1, 92, -1, 92, -1, 92, -1,
                92, 3, 93, 0, 94, 2, 91, 0, 91, 4, 92, -1, 92, -1, 92, 1, 92, -1, 92, -1,
            };
            Assert.Equal(autocad, GraphNumbers(block));
            string text = db.ToText().Replace("\r\n", "\n");
            Assert.Contains("  0\nCLASS\n  1\nBLOCKPOINTPARAMETER\n  2\nAcDbBlockPointParameter\n", text, StringComparison.Ordinal);
            Assert.Contains("  0\nCLASS\n  1\nBLOCKXYGRIP\n  2\nAcDbBlockXYGrip\n", text, StringComparison.Ordinal);
            Assert.Contains("  0\nCLASS\n  1\nBLOCKMOVEACTION\n  2\nAcDbBlockMoveAction\n", text, StringComparison.Ordinal);
        }

        // ---- a rotation parameter and a rotate -------------------------------------------------------------------------------

        /// <summary>An arrow, a line from (50, 25) to (80, 25), turned by the rotation parameter Angle about (50, 25), of the radius 30 and the angle nought.</summary>
        private static DxfDatabase Arrow(out DxfBlockTableRecord block, out DxfBlockRotationParameter angle)
        {
            DxfDatabase db = Drawing("GH_ARROW", out block);
            DxfLine arrow = Add(block, new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0)));
            angle = block.AddRotationParameter("Angle", "Turn of the arrow", new GeoPoint3(50, 25, 0), 30, 0);
            block.AddRotateAction(angle, new[] { arrow });
            return db;
        }

        [Fact]
        public void ARotationParameter_GivesTheInsertItsAngle_AndASetTurnsTheArrowAboutTheBasePoint()
        {
            DxfDatabase db = Arrow(out _, out _);
            DxfDynamicBlockReferenceProperty angle = Property(TheInsert(db), "Angle");
            Assert.Equal(0.0, Assert.IsType<double>(angle.Value), Close);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Angular, angle.UnitsType);
            Assert.Equal("Turn of the arrow", angle.Description);

            angle.Value = Math.PI / 2;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(Math.PI / 2, ValueOf(back, "Angle"), Close);
            Assert.True(Is(Assert.Single(Shown<DxfLine>(back)), 50, 25, 50, 55));

            // A quarter turn back is taken into a turn, as the property reads it.
            Property(back, "Angle").Value = -Math.PI / 2;
            Assert.Equal(3 * Math.PI / 2, ValueOf(back, "Angle"), Close);
            Assert.True(Is(Assert.Single(Shown<DxfLine>(back)), 50, 25, 50, -5));
            Dictionary<string, List<string>> cache = Cache(back);
            Assert.Equal(new[] { "1071 50242585", "1071 68333042", "70 25", "70 104", "10 50", "20 25", "30 0" }, cache["1"].Take(7));
            Assert.Equal(new[] { "10 0", "20 0", "30 -1", "10 80", "20 25", "30 0" }, cache["1"].Skip(10));
            Assert.Equal(new[] { "1071 4195796", "1071 5656503", "70 25", "70 104", "10 50", "20 25", "30 0" }, cache["5"]);
        }

        [Fact]
        public void ARotationParameterAndItsRotate_AreLaidOutAsAutoCadSavedThemInItsSample()
        {
            DxfDatabase db = Reopened(Arrow(out _, out _));
            DxfBlockTableRecord block = db.BlockTable["GH_ARROW"];
            List<DxfItem> nodes = Nodes(block);

            Assert.Equal(new[] { "BLOCKROTATIONPARAMETER", "BLOCKROTATIONGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKROTATEACTION" }, nodes.Select(n => n.Type));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 280, 281, 100, 1010, 1020, 1030, 1011, 1021, 1031, 170, 91, 91, 91, 91, 171, 172, 173, 94, 303, 174, 95, 304, 177, 100, 305, 306, 1011, 1021, 1031, 140, 307, 96, 141, 142, 143, 175 }, Codes(nodes[0]));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 91, 92, 1010, 1020, 1030, 280, 93, 100 }, Codes(nodes[1]));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 70, 71, 330, 1010, 1020, 1030, 100, 92, 93, 301, 302, 1011, 1021, 1031, 280, 1012, 1022, 1032, 100, 94, 303 }, Codes(nodes[4]));
            Assert.Equal(new[] { "3" }, Of(nodes[0], "AcDbBlockRotationParameter", 96));
            Assert.Equal(new[] { "6.283185307179586" }, Of(nodes[0], "AcDbBlockRotationParameter", 142));
            Assert.Equal(new[] { "80", "25", "0" }, new[] { 1011, 1021, 1031 }.Select(c => Of(nodes[0], "AcDbBlockRotationParameter", c).Single()));
            Assert.Equal(new[] { "0" }, Of(nodes[1], "AcDbBlockGrip", 280));
            Assert.Equal(new[] { "UpdatedEndX" }, Of(nodes[2], "AcDbBlockGripExpr", 300));
            Assert.Equal(new[] { "AngleDelta" }, Of(nodes[4], "AcDbBlockRotationAction", 303));

            // AutoCAD's save of its rotation sample, number for number but the handles: as the point's, the edge to the
            // rotate carrying three connections.
            int[] autocad =
            {
                96, 5, 97, 5,
                91, 0, 93, 32, 95, 1, 92, 2, 92, 2, 92, 0, 92, 3,
                91, 1, 93, 32, 95, 2, 92, -1, 92, -1, 92, 2, 92, 2,
                91, 2, 93, 32, 95, 3, 92, 0, 92, 0, 92, -1, 92, -1,
                91, 3, 93, 32, 95, 4, 92, 1, 92, 1, 92, -1, 92, -1,
                91, 4, 93, 32, 95, 5, 92, 3, 92, 3, 92, -1, 92, -1,
                92, 0, 93, 0, 94, 1, 91, 0, 91, 2, 92, -1, 92, -1, 92, -1, 92, 1, 92, -1,
                92, 1, 93, 0, 94, 1, 91, 0, 91, 3, 92, -1, 92, -1, 92, 0, 92, 3, 92, -1,
                92, 2, 93, 0, 94, 2, 91, 1, 91, 0, 92, -1, 92, -1, 92, -1, 92, -1, 92, -1,
                92, 3, 93, 0, 94, 3, 91, 0, 91, 4, 92, -1, 92, -1, 92, 1, 92, -1, 92, -1,
            };
            Assert.Equal(autocad, GraphNumbers(block));
        }

        // ---- a flip parameter and a flip -------------------------------------------------------------------------------------

        /// <summary>A door leaf, a line from (0, 0) to (90, 0), flipped by the flip parameter Hand about the y axis, from (0, 0) to (0, 50).</summary>
        private static DxfDatabase Door(out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_DOOR", out block);
            DxfLine leaf = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0)));
            DxfBlockFlipParameter hand = block.AddFlipParameter("Hand", "Which way the door opens", new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));
            block.AddFlipAction(hand, new[] { leaf });
            return db;
        }

        [Fact]
        public void AFlipParameter_GivesTheInsertItsState_AndASetMirrorsTheLeafAboutTheLine()
        {
            DxfDatabase db = Door(out _);
            DxfDynamicBlockReferenceProperty hand = Property(TheInsert(db), "Hand");
            Assert.Equal((short)0, Assert.IsType<short>(hand.Value));
            Assert.Equal(new object[] { (short)0, (short)1 }, hand.GetAllowedValues());

            hand.Value = (short)1;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal((short)1, Property(back, "Hand").Value);
            Assert.True(Is(Assert.Single(Shown<DxfLine>(back)), 0, 0, -90, 0));
            Dictionary<string, List<string>> cache = Cache(back);
            Assert.Equal(new[] { "1071 6887019", "1071 9372877", "70 25", "70 104", "10 0", "20 0", "30 0", "10 0", "20 50", "30 0", "10 0", "20 0", "30 -1", "70 1" }, cache["1"]);
            Assert.Equal(new[] { "1071 575085", "1071 763560", "70 25", "70 104", "70 0", "70 1" }, cache["2"]);
            Assert.Equal(new[] { "1", "2" }, cache.Keys.OrderBy(k => k, StringComparer.Ordinal));

            // Back to 0, the leaf is as the block has it; a whole number is taken as a state.
            Property(back, "Hand").Value = 0;
            Assert.Equal((short)0, Property(back, "Hand").Value);
            Assert.True(Is(Assert.Single(Shown<DxfLine>(back)), 0, 0, 90, 0));
        }

        [Fact]
        public void AFlipParameterAndItsFlip_AreLaidOutAsAutoCadSavedThemInItsSample()
        {
            DxfDatabase db = Reopened(Door(out _));
            DxfBlockTableRecord block = db.BlockTable["GH_DOOR"];
            List<DxfItem> nodes = Nodes(block);

            Assert.Equal(new[] { "BLOCKFLIPPARAMETER", "BLOCKFLIPGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKFLIPACTION" }, nodes.Select(n => n.Type));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 280, 281, 100, 1010, 1020, 1030, 1011, 1021, 1031, 170, 91, 91, 91, 91, 171, 172, 173, 174, 177, 100, 305, 306, 307, 308, 1012, 1022, 1032, 309, 96 }, Codes(nodes[0]));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 91, 92, 1010, 1020, 1030, 280, 93, 100, 140, 141, 142, 93 }, Codes(nodes[1]));
            Assert.Equal(new[] { 100, 90, 98, 99, 1, 70, 70, 100, 91, 300 }, Codes(nodes[2]));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 70, 71, 330, 1010, 1020, 1030, 100, 92, 93, 94, 95, 301, 302, 303, 304 }, Codes(nodes[5]));
            Assert.Equal(new[] { "Not flipped" }, Of(nodes[0], "AcDbBlockFlipParameter", 307));
            Assert.Equal(new[] { "Flipped" }, Of(nodes[0], "AcDbBlockFlipParameter", 308));
            Assert.Equal(new[] { "UpdatedFlip" }, Of(nodes[0], "AcDbBlockFlipParameter", 309));
            Assert.Equal(new[] { "2" }, Of(nodes[0], "AcDbBlockFlipParameter", 96));
            Assert.Equal(new[] { "4" }, Of(nodes[1], "AcDbBlockGrip", 91));
            Assert.Equal(new[] { "5" }, Of(nodes[1], "AcDbBlockGrip", 92));
            Assert.Equal(new[] { "3" }, Of(nodes[1], "AcDbBlockFlipGrip", 93));
            Assert.Equal(new[] { "70", "0" }, Of(nodes[2], "AcDbEvalExpr", 70));
            Assert.Equal(new[] { "Flip1" }, Of(nodes[5], "AcDbBlockElement", 300));

            // AutoCAD's save of its flip sample, number for number but the handles, and the action's id: the sample's is 7, a
            // node 6 having been taken out in AutoCAD.
            int[] autocad =
            {
                96, 6, 97, 6,
                91, 0, 93, 32, 95, 1, 92, 3, 92, 3, 92, 0, 92, 4,
                91, 1, 93, 32, 95, 2, 92, -1, 92, -1, 92, 3, 92, 3,
                91, 2, 93, 32, 95, 3, 92, 2, 92, 2, 92, -1, 92, -1,
                91, 3, 93, 32, 95, 4, 92, 0, 92, 0, 92, -1, 92, -1,
                91, 4, 93, 32, 95, 5, 92, 1, 92, 1, 92, -1, 92, -1,
                91, 5, 93, 32, 95, 6, 92, 4, 92, 4, 92, -1, 92, -1,
                92, 0, 93, 0, 94, 1, 91, 0, 91, 3, 92, -1, 92, -1, 92, -1, 92, 1, 92, -1,
                92, 1, 93, 0, 94, 1, 91, 0, 91, 4, 92, -1, 92, -1, 92, 0, 92, 2, 92, -1,
                92, 2, 93, 0, 94, 1, 91, 0, 91, 2, 92, -1, 92, -1, 92, 1, 92, 4, 92, -1,
                92, 3, 93, 0, 94, 1, 91, 1, 91, 0, 92, -1, 92, -1, 92, -1, 92, -1, 92, -1,
                92, 4, 93, 0, 94, 4, 91, 0, 91, 5, 92, -1, 92, -1, 92, 2, 92, -1, 92, -1,
            };
            Assert.Equal(autocad, GraphNumbers(block));
        }

        // ---- an XY parameter with a stretch and a move -----------------------------------------------------------------------

        /// <summary>
        /// A closed polyline rectangle 100 by 50 with an XY parameter Width and Depth from (0, 0) to (100, 50), a stretch of its top
        /// right corner by the frame (90, 40)-(110, 60), and a move of a circle at that corner.
        /// </summary>
        private static DxfDatabase Plate(out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_PLATE", out block);
            var outline = new DxfPolyline();
            outline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            outline.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            outline.AddVertexAt(2, new GeoPoint2(100, 50), 0, 0, 0);
            outline.AddVertexAt(3, new GeoPoint2(0, 50), 0, 0, 0);
            outline.Closed = true;
            Add(block, outline);
            DxfCircle hole = Add(block, new DxfCircle(new GeoPoint3(100, 50, 0), GeoVector3.ZAxis, 4));
            DxfBlockXYParameter xy = block.AddXYParameter("Width", "Along x", "Depth", "Along y", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0));
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { outline });
            block.AddMoveAction(xy, new[] { hole });
            return db;
        }

        [Fact]
        public void AnXYParameter_GivesTheInsertXThenY_AndSetsMoveTheCornerAndTheCircleBothWays()
        {
            DxfDatabase db = Plate(out _);
            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(new[] { "Width", "Depth" }, insert.DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
            Assert.Equal(100.0, ValueOf(insert, "Width"), Close);
            Assert.Equal(50.0, ValueOf(insert, "Depth"), Close);
            Assert.Equal("Along x", Property(insert, "Width").Description);
            Assert.Equal("Along y", Property(insert, "Depth").Description);

            Property(insert, "Width").Value = 160.0;
            Property(insert, "Depth").Value = 80.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(160.0, ValueOf(back, "Width"), Close);
            Assert.Equal(80.0, ValueOf(back, "Depth"), Close);
            DxfPolyline outline = Assert.Single(Shown<DxfPolyline>(back));
            Assert.True(At(outline.GetPoint3dAt(1), 100, 0));
            Assert.True(At(outline.GetPoint3dAt(2), 160, 80));
            Assert.True(At(outline.GetPoint3dAt(3), 0, 50));
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 160, 80));
            Assert.Equal(new[] { "1071 2550941", "1071 3467982", "70 25", "70 104", "10 0", "20 0", "30 0", "10 160", "20 80", "30 0", "10 0", "20 0", "30 -1" }, Cache(back)["1"]);
        }

        [Fact]
        public void AnXYParameter_IsLaidOutAsAutoCadSavedTheOneOfItsSample()
        {
            DxfDatabase db = Reopened(Plate(out _));
            DxfBlockTableRecord block = db.BlockTable["GH_PLATE"];
            List<DxfItem> nodes = Nodes(block);

            Assert.Equal(15, nodes.Count);
            Assert.Equal(new[] { "XY", "End Grip", "Base Grip", "BaseX-EndY Grip", "BaseY-EndX Grip" }, new[] { 0, 1, 4, 7, 10 }.Select(i => Of(nodes[i], "AcDbBlockElement", 300).Single()));
            Assert.Equal(new[] { "UpdatedEndX", "UpdatedEndY", "UpdatedBaseX", "UpdatedBaseY", "UpdatedBaseX", "UpdatedEndY", "UpdatedEndX", "UpdatedBaseY" }, new[] { 2, 3, 5, 6, 8, 9, 11, 12 }.Select(i => Of(nodes[i], "AcDbBlockGripExpr", 300).Single()));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 280, 281, 100, 1010, 1020, 1030, 1011, 1021, 1031, 170, 91, 91, 91, 91, 171, 92, 301, 92, 301, 172, 93, 302, 93, 302, 173, 94, 303, 94, 303, 174, 95, 304, 95, 304, 177, 100, 305, 306, 307, 308, 141, 140, 309, 97, 146, 147, 148, 176, 410, 96, 142, 143, 144, 175 }, Codes(nodes[0]));
            Assert.Equal(new[] { "5", "2", "8", "11" }, Of(nodes[0], "AcDbBlock2PtParameter", 91));
            Assert.Equal(new[] { "5", "8" }, Of(nodes[0], "AcDbBlock2PtParameter", 92));
            Assert.Equal(new[] { "5", "11" }, Of(nodes[0], "AcDbBlock2PtParameter", 93));
            Assert.Equal(new[] { "2", "11" }, Of(nodes[0], "AcDbBlock2PtParameter", 94));
            Assert.Equal(new[] { "2", "8" }, Of(nodes[0], "AcDbBlock2PtParameter", 95));
            Assert.Equal(new[] { "Depth" }, Of(nodes[0], "AcDbBlockXYParameter", 305));
            Assert.Equal(new[] { "Width" }, Of(nodes[0], "AcDbBlockXYParameter", 306));
            Assert.Equal(new[] { "Along y" }, Of(nodes[0], "AcDbBlockXYParameter", 307));
            Assert.Equal(new[] { "Along x" }, Of(nodes[0], "AcDbBlockXYParameter", 308));
            Assert.Equal(new[] { "0", "50" }, new[] { 1010, 1020 }.Select(c => Of(nodes[7], "AcDbBlockGrip", c).Single()));
            Assert.Equal(new[] { "100", "0" }, new[] { 1010, 1020 }.Select(c => Of(nodes[10], "AcDbBlockGrip", c).Single()));

            // AutoCAD's save of its XY sample: thirteen nodes of the parameter and its grips, twelve edges, each grip to the
            // parameter carrying two connections; then the stretch and the move.
            List<int> graph = GraphNumbers(block);
            int[] autocadEdges =
            {
                92, 0, 93, 0, 94, 1, 91, 0, 91, 2, 92, -1, 92, -1, 92, -1, 92, 1, 92, -1,
                92, 1, 93, 0, 94, 1, 91, 0, 91, 3, 92, -1, 92, -1, 92, 0, 92, 3, 92, -1,
                92, 2, 93, 0, 94, 2, 91, 1, 91, 0, 92, -1, 92, 5, 92, -1, 92, -1, 92, -1,
                92, 3, 93, 0, 94, 1, 91, 0, 91, 5, 92, -1, 92, -1, 92, 1, 92, 4, 92, -1,
                92, 4, 93, 0, 94, 1, 91, 0, 91, 6, 92, -1, 92, -1, 92, 3, 92, 6, 92, -1,
                92, 5, 93, 0, 94, 2, 91, 4, 91, 0, 92, 2, 92, 8, 92, -1, 92, -1, 92, -1,
                92, 6, 93, 0, 94, 1, 91, 0, 91, 8, 92, -1, 92, -1, 92, 4, 92, 7, 92, -1,
                92, 7, 93, 0, 94, 1, 91, 0, 91, 9, 92, -1, 92, -1, 92, 6, 92, 9, 92, -1,
                92, 8, 93, 0, 94, 2, 91, 7, 91, 0, 92, 5, 92, 11, 92, -1, 92, -1, 92, -1,
                92, 9, 93, 0, 94, 1, 91, 0, 91, 11, 92, -1, 92, -1, 92, 7, 92, 10, 92, -1,
                92, 10, 93, 0, 94, 1, 91, 0, 91, 12, 92, -1, 92, -1, 92, 9, 92, 12, 92, -1,
                92, 11, 93, 0, 94, 2, 91, 10, 91, 0, 92, 8, 92, -1, 92, -1, 92, -1, 92, -1,
            };
            Assert.Equal(autocadEdges, graph.Skip(4 + 15 * 14).Take(12 * 20));
        }

        // ---- the order of the actions ----------------------------------------------------------------------------------------

        [Fact]
        public void AnEntityBothFlippedAndStretchedOrMoved_IsRefusedEitherWayRound_AsAutoCadWorkedItOutFromTheLastState()
        {
            // AutoCAD 2021 stretched the panel's edges, flipped about the y axis, to x = -250 for a Width of 150 set after 200,
            // and would not flip the panel it stretched (plugin v20, 24d-i): no value of such a block tells its shape.
            DxfDatabase db = Panel(null, out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            DxfBlockFlipParameter hand = block.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));
            DxfCircle tag = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5));
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => block.AddFlipAction(hand, lines));
            Assert.Throws<NotSupportedException>(() => block.AddFlipAction(hand, new[] { lines[3], lines[0] }));
            Assert.Equal(before, db.ToText());

            // The left edge and a circle held by no stretch are refused too since D24e: AutoCAD 2021 did not flip an INSERT of
            // the block itself where the flip and the stretch held other entities (plugin v20b, 24d-i).
            Assert.Throws<NotSupportedException>(() => block.AddFlipAction(hand, new DxfEntity[] { lines[3], tag }));
            Assert.Equal(before, db.ToText());
        }

        // ---- the base point of a scale and a rotate --------------------------------------------------------------------------

        [Fact]
        public void AScaleAndARotate_AreWrittenWith1012Nought_AsAutoCadAddsItToTheBasePoint()
        {
            // AutoCAD 2021 turned the arrow about (100, 50), its base point (50, 25) and the 1012 of (50, 25) added, when the
            // library wrote the base point there (plugin v20, 24d-f); AutoCAD's samples write 1012 nought.
            DxfDatabase db = Arrow(out DxfBlockTableRecord block, out _);
            DxfCircle disc = Add(block, new DxfCircle(new GeoPoint3(70, 35, 0), GeoVector3.ZAxis, 5));
            DxfBlockLinearParameter size = block.AddLinearParameter("Size", null, new GeoPoint3(60, 30, 0), new GeoPoint3(90, 30, 0), -20, null);
            block.AddScaleAction(size, new[] { disc });

            foreach (DxfItem action in Nodes(Reopened(db).BlockTable["GH_ARROW"]).Where(n => n.Type == "BLOCKROTATEACTION" || n.Type == "BLOCKSCALEACTION"))
            {
                Assert.Equal(new[] { "0", "0", "0" }, new[] { 1012, 1022, 1032 }.Select(c => Of(action, "AcDbBlockActionWithBasePt", c).Single()));
                Assert.Equal(new[] { "0", "0", "0" }, new[] { 1011, 1021, 1031 }.Select(c => Of(action, "AcDbBlockActionWithBasePt", c).Single()));
                Assert.Equal(new[] { "1" }, Of(action, "AcDbBlockActionWithBasePt", 280));
            }

            // The library turns and scales about the parameter's base point.
            Property(TheInsert(db), "Angle").Value = Math.PI;
            Property(TheInsert(db), "Size").Value = 60.0;
            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.True(Is(Assert.Single(Shown<DxfLine>(back)), 50, 25, 20, 25));
            DxfCircle scaled = Assert.Single(Shown<DxfCircle>(back));
            Assert.True(At(scaled.Center, 80, 40));
            Assert.Equal(10.0, scaled.Radius, Close);
        }

        // ---- the tie rule of a list ------------------------------------------------------------------------------------------

        [Theory]
        [InlineData(250.0, 300.0, 125.0, 100.0)]
        [InlineData(250.0, 50.0, 225.0, 250.0)]
        [InlineData(250.0, 250.0, 75.0, 50.0)]
        [InlineData(250.0, 300.0, 275.0, 250.0)]
        [InlineData(100.0, 300.0, 175.0, 150.0)]
        [InlineData(100.0, 50.0, 75.0, 100.0)]
        [InlineData(100.0, 200.0, 125.0, 100.0)]
        [InlineData(250.0, 250.0, 125.0, 100.0)]
        public void AValueHalfWayBetweenTwoValuesOfAList_GoesBeyondItTheWayItChanges_AsAutoCadTookMostTiesInV20(double own, double from, double given, double taken)
        {
            DxfDatabase db = Drawing("GH_LIST", out DxfBlockTableRecord block);
            DxfLine right = Add(block, new DxfLine(new GeoPoint3(own, 0, 0), new GeoPoint3(own, 50, 0)));
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(own, 0, 0), -20, DxfBlockParameterValueSet.List(new[] { 50.0, 100, 150, 200, 250, 300 }));
            block.AddStretchAction(width, new GeoPoint2(own - 10, -10), new GeoPoint2(own + 10, 60), new[] { right });
            DxfDynamicBlockReferenceProperty property = Property(TheInsert(db), "Width");

            if (from != own)
            {
                property.Value = from;
            }

            property.Value = given;

            Assert.Equal(taken, (double)property.Value, Close);
            Assert.True(Is(Assert.Single(Shown<DxfLine>(TheInsert(Reopened(db)))), taken, 0, taken, 50));
        }
        // ---- refusals --------------------------------------------------------------------------------------------------------

        [Fact]
        public void ParametersThatCannotBeWritten_AreRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Drawing("GH_EMPTY", out DxfBlockTableRecord block);
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            string before = db.ToText();

            Assert.Throws<ArgumentNullException>(() => block.AddPointParameter(null, null, new GeoPoint3(0, 0, 0)));
            Assert.Throws<ArgumentException>(() => block.AddPointParameter(" ", null, new GeoPoint3(0, 0, 0)));
            Assert.Throws<ArgumentException>(() => block.AddPointParameter("Tag", "two\nlines", new GeoPoint3(0, 0, 0)));
            Assert.Throws<ArgumentException>(() => block.AddPointParameter("Tag", null, new GeoPoint3(double.NaN, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 0.0005, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 10, double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => block.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0.0004, 0, 5)));
            Assert.Throws<ArgumentException>(() => block.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0.0004, 0)));
            Assert.Throws<ArgumentNullException>(() => block.AddXYParameter("Width", null, null, null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0)));
            Assert.Equal(before, db.ToText());

            // An action given an entity of another block, an attribute definition or none, is refused before it is written.
            DxfBlockPointParameter tag = block.AddPointParameter("Tag", null, new GeoPoint3(0, 0, 0));
            string withTag = db.ToText();
            var other = new DxfBlockTableRecord { Name = "GH_OTHER" };
            db.BlockTable.Add(other);
            DxfLine stranger = Add(other, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            var definition = Add(block, new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "x", "TAG", "Tag?", null));
            string withOther = db.ToText();
            Assert.Throws<ArgumentException>(() => block.AddMoveAction(tag, new[] { stranger }));
            Assert.Throws<ArgumentException>(() => block.AddMoveAction(tag, new DxfEntity[] { definition }));
            Assert.Throws<ArgumentException>(() => block.AddMoveAction(tag, new DxfEntity[0]));
            Assert.Throws<ArgumentNullException>(() => block.AddMoveAction(tag, new DxfEntity[] { null }));
            Assert.Throws<ArgumentNullException>(() => block.AddMoveAction((DxfBlockPointParameter)null, new[] { line }));
            Assert.Throws<ArgumentException>(() => other.AddMoveAction(tag, new[] { stranger }));
            Assert.Equal(withOther, db.ToText());
            Assert.NotEqual(withTag, withOther);
        }

        [Fact]
        public void ValuesThatCannotBeSet_AreRefused_AndNothingIsWritten()
        {
            DxfDatabase door = Door(out _);
            DxfDynamicBlockReferenceProperty hand = Property(TheInsert(door), "Hand");
            string before = door.ToText();
            Assert.Throws<ArgumentOutOfRangeException>(() => hand.Value = 2);
            Assert.Throws<ArgumentException>(() => hand.Value = "1");
            Assert.Throws<ArgumentException>(() => hand.Value = 1.0);
            Assert.Equal(before, door.ToText());

            DxfDatabase plate = Plate(out _);
            DxfDynamicBlockReferenceProperty width = Property(TheInsert(plate), "Width");
            string plateBefore = plate.ToText();
            Assert.Throws<ArgumentOutOfRangeException>(() => width.Value = 0.0);
            Assert.Throws<ArgumentException>(() => width.Value = double.NaN);
            Assert.Equal(plateBefore, plate.ToText());

            DxfDatabase arrow = Arrow(out _, out _);
            string arrowBefore = arrow.ToText();
            Assert.Throws<ArgumentException>(() => Property(TheInsert(arrow), "Angle").Value = double.PositiveInfinity);
            Assert.Equal(arrowBefore, arrow.ToText());
        }

        // ---- the guide's examples ------------------------------------------------------------------------------------------

        [Fact]
        public void TheGuidesExampleOfEachKind_RunsFromNothing_AndReadsBackInR2018()
        {
            // The guide's panel, in a drawing of R2018.
            var db = new DxfDatabase("AC1032");
            var panel = new DxfBlockTableRecord { Name = "PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(panel);
            var right = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            panel.AppendEntity(right);
            DxfBlockLinearParameter width = panel.AddLinearParameter("Width", "Overall width", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10));
            panel.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right });

            // The guide's example of each kind, as it stands there.
            DxfBlockTableRecord Block(string name)
            {
                var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
                db.BlockTable.Add(block);
                return block;
            }

            DxfBlockReference Place(DxfBlockTableRecord block, double x, double y)
            {
                var placed = new DxfBlockReference(new GeoPoint3(x, y, 0), block);
                db.ModelSpace.AppendEntity(placed);
                return placed;
            }

            DxfBlockTableRecord slide = Block("SLIDE");
            var tagCircle = new DxfCircle(new GeoPoint3(90, 25, 0), GeoVector3.ZAxis, 5);
            slide.AppendEntity(tagCircle);
            DxfBlockLinearParameter length = slide.AddLinearParameter("Length", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            slide.AddMoveAction(length, new[] { tagCircle });

            DxfBlockTableRecord disc = Block("DISC");
            var round = new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 25);
            disc.AppendEntity(round);
            DxfBlockLinearParameter size = disc.AddLinearParameter("Size", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            disc.AddScaleAction(size, new[] { round });

            DxfBlockTableRecord tagged = Block("TAGGED");
            var tag = new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5);
            tagged.AppendEntity(tag);
            DxfBlockPointParameter where = tagged.AddPointParameter("Tag", "Where the tag stands", new GeoPoint3(80, 20, 0));
            tagged.AddMoveAction(where, new[] { tag });

            DxfBlockTableRecord arrowBlock = Block("ARROW");
            var arrow = new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0));
            arrowBlock.AppendEntity(arrow);
            DxfBlockRotationParameter angle = arrowBlock.AddRotationParameter("Angle", "Turn of the arrow", new GeoPoint3(50, 25, 0), 30, 0);
            arrowBlock.AddRotateAction(angle, new[] { arrow });

            DxfBlockTableRecord door = Block("DOOR");
            var leaf = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0));
            door.AppendEntity(leaf);
            DxfBlockFlipParameter hand = door.AddFlipParameter("Hand", "Which way the door opens", new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));
            door.AddFlipAction(hand, new[] { leaf });

            DxfBlockTableRecord plate = Block("PLATE");
            var outline = new DxfPolyline();
            outline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            outline.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            outline.AddVertexAt(2, new GeoPoint2(100, 50), 0, 0, 0);
            outline.AddVertexAt(3, new GeoPoint2(0, 50), 0, 0, 0);
            outline.Closed = true;
            var hole = new DxfCircle(new GeoPoint3(100, 50, 0), GeoVector3.ZAxis, 4);
            plate.AppendEntity(outline);
            plate.AppendEntity(hole);
            DxfBlockXYParameter xy = plate.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0));
            plate.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { outline });
            plate.AddMoveAction(xy, new[] { hole });

            Place(slide, 0, 150).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Length").Value = 200.0;
            Place(disc, 0, 300).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Size").Value = 200.0;
            Place(tagged, 300, 300).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Tag X").Value = 120.0;
            Place(arrowBlock, 600, 300).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Angle").Value = Math.PI / 2;
            Place(door, 900, 300).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Hand").Value = (short)1;
            DxfBlockReference sheet = Place(plate, 1200, 300);
            sheet.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 160.0;
            sheet.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Depth").Value = 80.0;

            // What the guide says each shows, read back from the text of the drawing.
            DxfDatabase back = Reopened(db);
            Assert.Equal("AC1032", back.Version);
            List<DxfBlockReference> read = back.ModelSpace.OfType<DxfBlockReference>().ToList();
            Assert.Equal(6, read.Count);
            Assert.Equal(200.0, ValueOf(read[0], "Length"), Close);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(read[0])).Center, 190, 25));
            DxfCircle doubled = Assert.Single(Shown<DxfCircle>(read[1]));
            Assert.True(At(doubled.Center, 100, 50));
            Assert.Equal(50.0, doubled.Radius, Close);
            Assert.Equal(120.0, ValueOf(read[2], "Tag X"), Close);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(read[2])).Center, 120, 20));
            Assert.Equal(Math.PI / 2, ValueOf(read[3], "Angle"), Close);
            Assert.True(Is(Assert.Single(Shown<DxfLine>(read[3])), 50, 25, 50, 55));
            Assert.Equal((short)1, Property(read[4], "Hand").Value);
            Assert.True(Is(Assert.Single(Shown<DxfLine>(read[4])), 0, 0, -90, 0));
            Assert.Equal(new[] { 160.0, 80.0 }, new[] { ValueOf(read[5], "Width"), ValueOf(read[5], "Depth") });
            Assert.True(At(Assert.Single(Shown<DxfPolyline>(read[5])).GetPoint3dAt(2), 160, 80));
            Assert.True(At(Assert.Single(Shown<DxfCircle>(read[5])).Center, 160, 80));
            Assert.Equal(back.ToText(), DxfDatabase.Parse(back.ToText()).ToText());
        }

        [Fact]
        public void AnArcFlipped_IsTheMirrorOfItsSwing()
        {
            DxfDatabase db = Drawing("GH_SWING", out DxfBlockTableRecord block);
            DxfArc swing = Add(block, new DxfArc(new GeoPoint3(0, 0, 0), 90, 0, Math.PI / 2));
            block.AddFlipAction(block.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)), new[] { swing });

            Property(TheInsert(db), "Hand").Value = (short)1;

            DxfArc shown = Assert.Single(Shown<DxfArc>(TheInsert(Reopened(db))));
            Assert.True(At(shown.Center, 0, 0));
            Assert.Equal(90.0, shown.Radius, Close);
            GeoPoint3[] ends = { shown.StartPoint, shown.EndPoint };
            Assert.Contains(ends, p => At(p, -90, 0));
            Assert.Contains(ends, p => At(p, 0, 90));
            Assert.True(At(shown.GetPointAtDist(shown.GetDistanceAtParameter(shown.EndParam) / 2), -90 * Math.Sqrt(0.5), 90 * Math.Sqrt(0.5)));
        }
    }
}