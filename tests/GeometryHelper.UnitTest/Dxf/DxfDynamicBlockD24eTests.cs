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
    /// The last dynamic blocks made from code (D24e): a stretch of an arc held by one end, as AutoCAD 2021 stretched one in
    /// plugin v20; moves and stretches of a distance multiplier and an angle offset, and moves listing their own parameter's
    /// grip, as AutoCAD's point sample and the user's drawings do; array actions of a linear and an XY parameter; a lookup
    /// parameter with its table; alignment and base point parameters; and a flip refused in a block of other actions, as
    /// AutoCAD 2021 did not flip an INSERT of such a block itself in plugin v20b. Each written number for number as AutoCAD's
    /// own saved samples (ACadSharp's samples saved by AutoCAD 2021), read back as a property and set on an INSERT.
    /// </summary>
    public class DxfDynamicBlockD24eTests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        // AutoCAD printed its arcs to six decimals (plugin v20, 24d-k).
        private static readonly Tolerance Printed = new Tolerance(1E-6, 1E-9);

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

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static double ValueOf(DxfBlockReference insert, string name) => Assert.IsType<double>(Property(insert, name).Value);

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

        /// <summary>Each edge of the graph of a block, after its nodes: its kind (94), the indices of the nodes it runs from and to, and the edge it pairs with (its last link).</summary>
        private static List<int[]> Edges(DxfBlockTableRecord block)
        {
            List<int> numbers = GraphNumbers(block);
            var edges = new List<int[]>();

            for (int at = 4 + Nodes(block).Count * 14; at + 19 < numbers.Count; at += 20)
            {
                edges.Add(new[] { numbers[at + 5], numbers[at + 7], numbers[at + 9], numbers[at + 19] });
            }

            return edges;
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

        /// <summary>
        /// Adds a handle to what an action works on (its 330s after the count 71 of AcDbBlockAction), as AutoCAD lists a grip in
        /// a move's selection: the shape of AutoCAD's point sample and of the user's drawings, which the library does not write.
        /// </summary>
        private static void Select(DxfItem action, string handle)
        {
            DxfFile file = action.File;
            var groups = file.TailOf(action, 0).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            int marker = groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbBlockAction");
            int count = groups.FindIndex(marker, g => g.Key == 71);
            int last = groups.FindLastIndex(g => g.Key == 330);
            groups[count] = new KeyValuePair<int, string>(71, (int.Parse(groups[count].Value.Trim(), CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture));
            groups.Insert(last + 1, new KeyValuePair<int, string>(330, handle));
            file.ReplaceTail(action, 0, groups);
        }

        // ---- 1. an arc stretched by one end ----------------------------------------------------------------------------------

        /// <summary>
        /// The block of plugin v20's 24d-k: a rectangle 100 by 50 stretched by its right edge in the frame (95, -10)-(130, 60),
        /// an arc of the radius 10 about (90, 25) from 0 to 90 degrees held by its start (100, 25), and an arc of the radius 20
        /// about (90, 25) from 270 to 0 degrees held by its end (110, 25).
        /// </summary>
        private static DxfDatabase ArcEnds(out DxfBlockTableRecord block, out DxfArc small, out DxfArc large)
        {
            DxfDatabase db = Panel(out block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            small = Add(block, new DxfArc(new GeoPoint3(90, 25, 0), 10, 0, Math.PI / 2));
            large = Add(block, new DxfArc(new GeoPoint3(90, 25, 0), 20, 1.5 * Math.PI, 0));
            block.AddStretchAction(width, new GeoPoint2(95, -10), new GeoPoint2(130, 60), new DxfEntity[] { lines[0], lines[1], lines[2], small, large });
            return db;
        }

        [Theory]
        [InlineData(150.0, 94.280944, -124.314337, 159.371843, 94.734827, 120.928105, 116.024756)]
        [InlineData(200.0, 97.99356, -487.070842, 522.132034, 98.561888, 323.628674, 318.743687)]
        public void AnArcHeldByOneEnd_HasThatEndMovedAndKeepsTheHeightOfItsMiddle_AsAutoCadStretchedItInV20(double width, double x1, double y1, double r1, double x2, double y2, double r2)
        {
            DxfDatabase db = ArcEnds(out _, out _, out _);

            Property(TheInsert(db), "Width").Value = width;

            List<DxfArc> arcs = Shown<DxfArc>(TheInsert(Reopened(db)));
            Assert.True(arcs[0].Center.IsEqualTo(new GeoPoint3(x1, y1, 0), Printed));
            Assert.Equal(r1, arcs[0].Radius, 1E-6);
            Assert.True(At(arcs[0].StartPoint, width, 25));
            Assert.True(At(arcs[0].EndPoint, 90, 35));
            Assert.True(arcs[1].Center.IsEqualTo(new GeoPoint3(x2, y2, 0), Printed));
            Assert.Equal(r2, arcs[1].Radius, 1E-6);
            Assert.True(At(arcs[1].StartPoint, 90, 5));
            Assert.True(At(arcs[1].EndPoint, width + 10, 25));

            // The chord height of the small arc, 10 - sqrt(50), is kept.
            DxfArc bent = arcs[0];
            GeoPoint3 middle = bent.GetPointAtParameter((bent.StartParam + bent.EndParam) / 2);
            double chord = bent.StartPoint.GetVectorTo(bent.EndPoint).Length;
            Assert.Equal(10 - Math.Sqrt(50), bent.Radius - Math.Sqrt(bent.Radius * bent.Radius - chord * chord / 4), 1E-9);
            Assert.True(middle.Y > 30);
        }

        [Fact]
        public void AnArcHeldByOneEnd_IsWrittenByThatEndOnly_AsAutoCadNumbersItsStartNoughtAndItsEndOne()
        {
            DxfDatabase db = ArcEnds(out DxfBlockTableRecord block, out DxfArc small, out DxfArc large);

            DxfItem stretch = Nodes(block).Last();
            List<string> held = Of(stretch, "AcDbBlockStretchAction", 331);
            List<string> indices = Of(stretch, "AcDbBlockStretchAction", 94);
            Assert.Equal(new[] { small.Handle, large.Handle }, held.Skip(3));
            Assert.Equal(new[] { "0", "1" }, indices.Skip(indices.Count - 2));
            Assert.Equal(new[] { "1", "1" }, Of(stretch, "AcDbBlockStretchAction", 74).Skip(3));
        }

        [Fact]
        public void AnArcFacingDownHeldByOneEnd_KeepsItsSenseAndTheHeightOfItsMiddle()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);

            // Seen from above, the arc runs clockwise from (90, 35) over (97.07, 32.07) to (100, 25), held by its end.
            DxfArc down = Add(block, new DxfArc(new GeoPoint3(90, 25, 0), new GeoVector3(0, 0, -1), 10, Math.PI / 2, Math.PI));
            Assert.True(At(down.StartPoint, 90, 35));
            Assert.True(At(down.EndPoint, 100, 25));
            block.AddStretchAction(width, new GeoPoint2(95, -10), new GeoPoint2(130, 60), new[] { down });

            Property(TheInsert(db), "Width").Value = 200.0;

            DxfArc shown = Assert.Single(Shown<DxfArc>(TheInsert(Reopened(db))));
            Assert.Equal(-1.0, shown.Normal.Z, Close);
            Assert.True(At(shown.StartPoint, 90, 35));
            Assert.True(At(shown.EndPoint, 200, 25));
            Assert.Equal(522.132034, shown.Radius, 1E-6);
            GeoPoint3 middle = shown.GetPointAtParameter((shown.StartParam + shown.EndParam) / 2);
            Assert.True(middle.IsEqualTo(new GeoPoint3(145.265173, 32.916904, 0), Printed));
        }

        [Fact]
        public void ATiltedArcHeldByOneEnd_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            DxfArc tilted = Add(block, new DxfArc(new GeoPoint3(100, 25, 0), new GeoVector3(1, 0, 1), 10, 0, Math.PI / 2));
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => block.AddStretchAction(width, new GeoPoint2(95, -10), new GeoPoint2(130, 60), new[] { tilted }));
            Assert.Equal(before, db.ToText());
        }

        // ---- 2. distance multipliers, angle offsets, and a move of its own grip ---------------------------------------------

        [Fact]
        public void AMoveOfTheMultiplierHalf_KeepsItsCircleInTheMiddle_AndIsWrittenWith140And141()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            DxfCircle middle = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5));
            block.AddMoveAction(width, new[] { middle }, 0.5, 0);

            Property(TheInsert(db), "Width").Value = 200.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 100, 25));
            Assert.True(Is(Shown<DxfLine>(back)[0], 200, 0, 200, 50));
            DxfItem move = Nodes(back.DynamicBlockTableRecord).Last();
            Assert.Equal(new[] { "0.5" }, Of(move, "AcDbBlockMoveAction", 140));
            Assert.Equal(new[] { "0.0" }, Of(move, "AcDbBlockMoveAction", 141));
        }

        [Fact]
        public void AnAngleOffsetOfAQuarterTurn_TurnsTheMoveAlongY()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            DxfCircle tag = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5));
            block.AddMoveAction(width, new[] { tag }, 1, Math.PI / 2);

            Property(TheInsert(db), "Width").Value = 200.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 50, 125));
            Assert.Equal(new[] { "1.0" }, Of(Nodes(back.DynamicBlockTableRecord).Last(), "AcDbBlockMoveAction", 140));
            Assert.Equal(Math.PI / 2, double.Parse(Of(Nodes(back.DynamicBlockTableRecord).Last(), "AcDbBlockMoveAction", 141).Single(), CultureInfo.InvariantCulture), Close);
        }

        [Fact]
        public void AStretchOfTheMultiplierHalf_MovesTheRightEdgeHalfAsFar()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3), 0.5, 0);

            Property(TheInsert(db), "Width").Value = 200.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(200.0, ValueOf(back, "Width"), Close);
            Assert.True(Is(Shown<DxfLine>(back)[0], 150, 0, 150, 50));
            Assert.Equal(new[] { "0.5" }, Of(Nodes(back.DynamicBlockTableRecord).Last(), "AcDbBlockStretchAction", 140));
        }

        [Fact]
        public void TheOverloadsOfNoMultiplier_WriteWhatTheMultiplierOneAndTheOffsetNoughtWrite()
        {
            string Written(bool factors)
            {
                DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);

                if (factors)
                {
                    block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3), 1, 0);
                    block.AddMoveAction(width, lines.Skip(3), 1, 0);
                }
                else
                {
                    block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
                    block.AddMoveAction(width, lines.Skip(3));
                }

                return string.Join("|", Nodes(block).Skip(7).Select(n => string.Join(",", n.Groups.Where(g => g.TypeCode != 5 && g.TypeCode != 330).Select(g => g.TypeCode + "=" + g.Value))));
            }

            Assert.Equal(Written(false), Written(true));
            Assert.Contains("140=1.0", Written(true));
        }

        [Fact]
        public void AMoveListingItsOwnPointGrip_AsAutoCadsPointSampleDoes_IsWorkedOut()
        {
            DxfDatabase db = Drawing("GH_TAGGED", out DxfBlockTableRecord block);
            DxfCircle circle = Add(block, new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5));
            DxfBlockPointParameter tag = block.AddPointParameter("Tag", null, new GeoPoint3(80, 20, 0));
            block.AddMoveAction(tag, new[] { circle });
            List<DxfItem> nodes = Nodes(block);
            Select(nodes.Last(), nodes[1].Handle);
            Assert.Equal(new[] { circle.Handle, nodes[1].Handle }, Of(Nodes(block).Last(), "AcDbBlockAction", 330));

            Property(TheInsert(db), "Tag X").Value = 120.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(120.0, ValueOf(back, "Tag X"), Close);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 120, 20));
        }

        [Fact]
        public void AMoveOfTheMultiplierHalfListingItsOwnEndGrip_AsTheUsersDrawingDoes_MovesHalfAsFar()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            DxfCircle middle = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5));
            block.AddMoveAction(width, new[] { middle }, 0.5, 0);
            List<DxfItem> nodes = Nodes(block);
            Assert.Equal("BLOCKLINEARGRIP", nodes[1].Type);
            Select(nodes.Last(), nodes[1].Handle);

            Property(TheInsert(db), "Width").Value = 300.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 150, 25));
        }

        [Fact]
        public void AMoveListingAnotherParametersGrip_IsStillNotWorkedOut()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            DxfCircle middle = Add(block, new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5));
            DxfBlockPointParameter tag = block.AddPointParameter("Tag", null, new GeoPoint3(50, 25, 0));
            block.AddMoveAction(width, new[] { middle });
            List<DxfItem> nodes = Nodes(block);
            DxfItem grip = nodes.First(n => n.Type == "BLOCKXYGRIP");
            Select(nodes.Last(), grip.Handle);

            Assert.Throws<NotSupportedException>(() => Property(TheInsert(db), "Width").Value = 200.0);
            Assert.Equal("GH_PANEL", TheInsert(db).Name);
        }

        [Fact]
        public void AMultiplierOrAnOffsetNotFinite_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            string before = db.ToText();

            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddMoveAction(width, lines.Take(1), double.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddMoveAction(width, lines.Take(1), 1, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(1), double.NaN, 0));
            Assert.Equal(before, db.ToText());
        }

        // ---- 3. array actions ------------------------------------------------------------------------------------------------

        [Fact]
        public void AnArrayOfALinearParameter_CopiesThePostAtEachSpacingAlongIt()
        {
            DxfDatabase db = Drawing("GH_FENCE", out DxfBlockTableRecord block);
            DxfLine rail = Add(block, new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(100, 10, 0)));
            DxfCircle post = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockLinearParameter length = block.AddLinearParameter("Length", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            block.AddStretchAction(length, new GeoPoint2(90, 0), new GeoPoint2(110, 20), new[] { rail });
            block.AddArrayAction(length, new[] { post }, 60);

            Property(TheInsert(db), "Length").Value = 250.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(250.0, ValueOf(back, "Length"), Close);
            Assert.Equal(new[] { 0.0, 60, 120, 180 }, Shown<DxfCircle>(back).Select(c => c.Center.X));
            Assert.All(Shown<DxfCircle>(back), c => Assert.Equal(0.0, c.Center.Y, Close));
            Assert.True(Is(Assert.Single(Shown<DxfLine>(back)), 0, 10, 250, 10));

            // Each copy keeps the post's tag, as AutoCAD's copies of the XY sample keep theirs.
            string tag = post.GetXDataForApplication("AcDbBlockRepETag").Single(g => g.TypeCode == 1071).Value;
            Assert.All(Shown<DxfCircle>(back), c => Assert.Equal(tag, c.GetXDataForApplication("AcDbBlockRepETag").Single(g => g.TypeCode == 1071).Value));

            Property(back, "Length").Value = 119.0;
            Assert.Single(Shown<DxfCircle>(back));
        }

        [Fact]
        public void AnArrayAction_IsLaidOutAsAutoCadSavedTheOneOfItsXYSample()
        {
            DxfDatabase db = Drawing("GH_PLATE", out DxfBlockTableRecord block);
            DxfCircle bolt = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockXYParameter xy = block.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(40, 30, 0));
            block.AddArrayAction(xy, new[] { bolt }, 25, 20);

            // The sample's array: the entity, the connections Base, End, UpdatedBase, UpdatedEnd, the row offset then the column offset.
            DxfItem array = Nodes(block).Last();
            Assert.Equal("BLOCKARRAYACTION", array.Type);
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 70, 71, 330, 1010, 1020, 1030, 100, 92, 93, 94, 95, 301, 302, 303, 304, 140, 141 }, Codes(array));
            Assert.Equal(new[] { "Array" }, Of(array, "AcDbBlockElement", 300));
            Assert.Equal(new[] { "Base" }, Of(array, "AcDbBlockArrayAction", 301));
            Assert.Equal(new[] { "UpdatedEnd" }, Of(array, "AcDbBlockArrayAction", 304));
            Assert.Equal(new[] { "20" }, Of(array, "AcDbBlockArrayAction", 140));
            Assert.Equal(new[] { "25" }, Of(array, "AcDbBlockArrayAction", 141));

            // The sample's edge from the XY parameter to the array carries four connections.
            List<int> graph = GraphNumbers(block);
            Assert.Equal(new[] { 92, 12, 93, 0, 94, 4, 91, 0, 91, 13, 92, -1, 92, -1, 92, 10, 92, -1, 92, -1 }, graph.Skip(graph.Count - 20));

            // Width 100 and Depth 70: four columns of 25 and three rows of 20; the array keeps no record, as the sample's.
            DxfBlockReference insert = TheInsert(db);
            Property(insert, "Width").Value = 100.0;
            Property(insert, "Depth").Value = 70.0;
            DxfBlockReference back = TheInsert(Reopened(db));
            List<GeoPoint3> bolts = Shown<DxfCircle>(back).Select(c => c.Center).ToList();
            Assert.Equal(12, bolts.Count);

            foreach (double x in new[] { 0.0, 25, 50, 75 })
            {
                foreach (double y in new[] { 0.0, 20, 40 })
                {
                    Assert.Contains(bolts, b => At(b, x, y));
                }
            }

            Assert.Equal(new[] { "1" }, Cache(back).Keys);
        }

        [Fact]
        public void AnArrayOfAnXYParameter_RunsTheWayTheEndPointLies_AsInAutoCadsSample()
        {
            // The sample's parameter from (0, 5) to (5, 0) and its offsets: rows of 20 down, columns of 25 along x; AutoCAD drew
            // four columns and five rows for 100.59 by 104.30.
            DxfDatabase db = Drawing("GH_GRID", out DxfBlockTableRecord block);
            DxfCircle dot = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockXYParameter xy = block.AddXYParameter("X Distance1", null, "Y Distance1", null, new GeoPoint3(0, 5, 0), new GeoPoint3(5, 0, 0));
            block.AddArrayAction(xy, new[] { dot }, 25, 20);

            DxfBlockReference insert = TheInsert(db);
            Property(insert, "X Distance1").Value = 100.59269849759175;
            Property(insert, "Y Distance1").Value = 104.303888075734;

            List<GeoPoint3> dots = Shown<DxfCircle>(TheInsert(Reopened(db))).Select(c => c.Center).ToList();
            Assert.Equal(20, dots.Count);
            Assert.Equal(new[] { 0.0, 25, 50, 75 }, dots.Select(d => d.X).Distinct().OrderBy(x => x));
            Assert.Equal(new[] { -80.0, -60, -40, -20, 0 }, dots.Select(d => d.Y).Distinct().OrderBy(y => y));
        }

        [Fact]
        public void WhatAnArrayDoesNotTake_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            DxfCircle post = Add(block, new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5));
            string before = db.ToText();

            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddArrayAction(width, new[] { post }, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddArrayAction(width, new[] { post }, double.NaN));
            Assert.Throws<NotSupportedException>(() => block.AddArrayAction(width, new[] { lines[0] }, 30));
            Assert.Equal(before, db.ToText());

            block.AddArrayAction(width, new[] { post }, 30);
            string arrayed = db.ToText();
            Assert.Throws<NotSupportedException>(() => block.AddMoveAction(width, new[] { post }));
            Assert.Equal(arrayed, db.ToText());
        }

        // ---- 4. a lookup parameter -------------------------------------------------------------------------------------------

        /// <summary>The panel of a Width stretching its right edge and a Height stretching its top, looked up by Size: Small 100 by 50, Large 200 by 80.</summary>
        private static DxfDatabase Sized(out DxfBlockTableRecord block)
        {
            DxfDatabase db = Panel(out block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            DxfBlockLinearParameter height = block.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 20, DxfBlockParameterValueSet.List(new[] { 50.0, 80 }));
            block.AddStretchAction(height, new GeoPoint2(-10, 40), new GeoPoint2(110, 60), new[] { lines[0], lines[2], lines[3] });
            block.AddLookupParameter("Size", "Panel size", new GeoPoint3(50, -20, 0), new[] { DxfBlockLookupInput.Distance(width), DxfBlockLookupInput.Distance(height) },
                new[] { new DxfBlockLookupRow("Small", 100, 50), new DxfBlockLookupRow("Large", 200, 80) });
            return db;
        }

        [Fact]
        public void ALookupParameter_SetsTheWidthAndTheHeightOfItsRow_AndShowsTheRowOfTheirValues()
        {
            DxfDatabase db = Sized(out _);
            DxfBlockReference insert = TheInsert(db);
            DxfDynamicBlockReferenceProperty size = Property(insert, "Size");
            Assert.Equal(new[] { "Width", "Height", "Size" }, insert.DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
            Assert.Equal("Panel size", size.Description);
            Assert.False(size.ReadOnly);
            Assert.Equal(5, size.PropertyTypeCode);

            size.Value = "large";

            DxfDatabase reopened = Reopened(db);
            DxfBlockReference back = TheInsert(reopened);
            Assert.Equal("Large", Property(back, "Size").Value);
            Assert.Equal(new object[] { "Small", "Large" }, Property(back, "Size").GetAllowedValues());
            Assert.Equal(200.0, ValueOf(back, "Width"), Close);
            Assert.Equal(80.0, ValueOf(back, "Height"), Close);
            Assert.True(Is(Shown<DxfLine>(back)[0], 200, 0, 200, 80));

            Property(back, "Width").Value = 150.0;
            Assert.Equal("Custom", Property(back, "Size").Value);
            Property(back, "Width").Value = 100.0;
            Property(back, "Height").Value = 50.0;
            Assert.Equal("Small", Property(TheInsert(Reopened(reopened)), "Size").Value);
        }

        [Fact]
        public void ALookupParameterAndItsAction_AreLaidOutAsAutoCadSavedThemInItsSample()
        {
            DxfDatabase db = Sized(out DxfBlockTableRecord block);
            List<DxfItem> nodes = Nodes(block);

            // Width (7 nodes), its stretch, Height (7), its stretch, then the lookup's five: parameter, grip, two components, action.
            Assert.Equal(new[] { "BLOCKLOOKUPPARAMETER", "BLOCKLOOKUPGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKLOOKUPACTION" }, nodes.Skip(16).Select(n => n.Type));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 280, 281, 100, 1010, 1020, 1030, 93, 170, 171, 100, 303, 304, 94 }, Codes(nodes[16]));
            Assert.Equal(new[] { "Lookup" }, Of(nodes[16], "AcDbBlockElement", 300));
            Assert.Equal(new[] { "21" }, Of(nodes[16], "AcDbBlockLookUpParameter", 94));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 91, 92, 1010, 1020, 1030, 280, 93, 100 }, Codes(nodes[17]));
            Assert.Equal(new[] { "UpdatedX" }, Of(nodes[18], "AcDbBlockGripExpr", 300));
            Assert.Equal(new[] { "Lookup1" }, Of(nodes[20], "AcDbBlockElement", 300));

            // The table: two rows of three columns, row by row; the inputs of reals labelled Custom, the lookup of text looking back.
            DxfItem action = nodes[20];
            Assert.Equal(new[] { "2" }, Of(action, "AcDbBlockLookupAction", 92));
            Assert.Equal(new[] { "3" }, Of(action, "AcDbBlockLookupAction", 93));
            Assert.Equal(new[] { "100", "50", "Small", "200", "80", "Large" }, Of(action, "AcDbBlockLookupAction", 302));
            Assert.Equal(new[] { "1", "9", "17" }, Of(action, "AcDbBlockLookupAction", 94));
            Assert.Equal(new[] { "40", "40", "1" }, Of(action, "AcDbBlockLookupAction", 95));
            Assert.Equal(new[] { "2", "2", "0" }, Of(action, "AcDbBlockLookupAction", 96));
            Assert.Equal(new[] { "0", "0", "1", "1" }, Of(action, "AcDbBlockLookupAction", 282).Concat(Of(action, "AcDbBlockLookupAction", 280)));
            Assert.Equal(new[] { "0", "0", "1" }, Of(action, "AcDbBlockLookupAction", 281));
            Assert.Equal(new[] { "UpdatedDistance", "UpdatedDistance", "lookupString" }, Of(action, "AcDbBlockLookupAction", 304));
            Assert.Equal(new[] { "Custom", "Custom", "Custom" }, Of(action, "AcDbBlockLookupAction", 305));

            // The sample's edges: the grip to the parameter, the parameter to its components, then a pair each way between the
            // action and each parameter it reads or sets (the input's first), each edge naming its pair as its last link.
            int[][] lookupEdges =
            {
                new[] { 1, 17, 16, -1 }, new[] { 1, 16, 18, -1 }, new[] { 1, 16, 19, -1 },
                new[] { 1, 0, 20, 18 }, new[] { 1, 20, 0, 17 }, new[] { 1, 8, 20, 20 }, new[] { 1, 20, 8, 19 }, new[] { 1, 20, 16, 22 }, new[] { 1, 16, 20, 21 },
            };
            Assert.Equal(lookupEdges, Edges(block).Skip(14));
            Assert.All(Edges(block).Take(14), e => Assert.Equal(-1, e[3]));

            // An INSERT set keeps the label in its cache, by the parameter's node, as AutoCAD keeps it; the action keeps none.
            Property(TheInsert(db), "Size").Value = "Large";
            Dictionary<string, List<string>> cache = Cache(TheInsert(Reopened(db)));
            Assert.Equal(new[] { "1071 18605567", "1071 25317787", "70 25", "70 104", "10 50", "20 -20", "30 0", "1 Large" }, cache["17"]);
            Assert.False(cache.ContainsKey("21"));
        }

        [Fact]
        public void ALookupOfAPointsXAndY_MovesTheTag_AndPairsOneEdgeOfTwoConnections()
        {
            DxfDatabase db = Drawing("GH_TAG", out DxfBlockTableRecord block);
            DxfCircle circle = Add(block, new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5));
            DxfBlockPointParameter tag = block.AddPointParameter("Tag", null, new GeoPoint3(80, 20, 0));
            block.AddMoveAction(tag, new[] { circle });
            block.AddLookupParameter("Corner", null, new GeoPoint3(0, -10, 0), new[] { DxfBlockLookupInput.X(tag), DxfBlockLookupInput.Y(tag) },
                new[] { new DxfBlockLookupRow("Low", 80, 20), new DxfBlockLookupRow("High", 80, 45) });

            Property(TheInsert(db), "Corner").Value = "High";

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(45.0, ValueOf(back, "Tag Y"), Close);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(back)).Center, 80, 45));
            Assert.Equal(new[] { "UpdatedX", "UpdatedY", "lookupString" }, Of(Nodes(block).Last(), "AcDbBlockLookupAction", 304));

            // The pair between the point and the action carries two connections, as the sample's.
            Assert.Equal(new[] { new[] { 2, 0, 9, 8 }, new[] { 2, 9, 0, 7 }, new[] { 1, 9, 5, 10 }, new[] { 1, 5, 9, 9 } }, Edges(block).Skip(7));
        }

        [Fact]
        public void WhatALookupDoesNotTake_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out _);
            DxfDatabase other = Panel(out _, out DxfBlockLinearParameter foreign, out _);
            DxfBlockLookupInput[] inputs = { DxfBlockLookupInput.Distance(width) };
            var at = new GeoPoint3(0, -20, 0);
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, at, inputs, new DxfBlockLookupRow[0]));
            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, at, new DxfBlockLookupInput[0], new[] { new DxfBlockLookupRow("A") }));
            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, at, inputs, new[] { new DxfBlockLookupRow("A", 100), new DxfBlockLookupRow("a", 200) }));
            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, at, inputs, new[] { new DxfBlockLookupRow("custom", 100) }));
            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, at, inputs, new[] { new DxfBlockLookupRow("A", 100, 50) }));
            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, at, inputs, new[] { new DxfBlockLookupRow("A", 0) }));
            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, at, new[] { DxfBlockLookupInput.Distance(width), DxfBlockLookupInput.Distance(width) }, new[] { new DxfBlockLookupRow("A", 1, 2) }));
            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, at, new[] { DxfBlockLookupInput.Distance(foreign) }, new[] { new DxfBlockLookupRow("A", 100) }));
            Assert.Throws<ArgumentNullException>(() => block.AddLookupParameter("Size", null, at, null, new[] { new DxfBlockLookupRow("A", 100) }));
            Assert.Throws<ArgumentException>(() => new DxfBlockLookupRow(" ", 1));
            Assert.Throws<ArgumentException>(() => new DxfBlockLookupRow("A", double.NaN));
            Assert.Equal(before, db.ToText());

            DxfDatabase sized = Sized(out _);
            DxfDynamicBlockReferenceProperty size = Property(TheInsert(sized), "Size");
            string written = sized.ToText();
            Assert.Throws<ArgumentException>(() => size.Value = "Huge");
            Assert.Throws<ArgumentException>(() => size.Value = "Custom");
            Assert.Throws<ArgumentException>(() => size.Value = 200.0);
            Assert.Equal(written, sized.ToText());
        }

        // ---- 5. alignment and base point parameters --------------------------------------------------------------------------

        [Fact]
        public void AnAlignmentParameter_IsLaidOutAsAutoCadSavedTheOneOfItsSample_AndGivesNoProperty()
        {
            DxfDatabase db = Drawing("GH_PIPE", out DxfBlockTableRecord block);
            Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)));
            block.AddAlignmentParameter(new GeoPoint3(5, 5, 0), new GeoPoint3(19, 13, 0));

            List<DxfItem> nodes = Nodes(block);
            Assert.Equal(new[] { "BLOCKALIGNMENTPARAMETER", "BLOCKALIGNMENTGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT" }, nodes.Select(n => n.Type));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 280, 281, 100, 1010, 1020, 1030, 1011, 1021, 1031, 170, 91, 91, 91, 91, 171, 172, 173, 174, 177, 100, 280 }, Codes(nodes[0]));
            Assert.Equal(new[] { "Alignment" }, Of(nodes[0], "AcDbBlockElement", 300));
            Assert.Equal(new[] { "1" }, Of(nodes[0], "AcDbBlockAlignmentParameter", 280));
            Assert.DoesNotContain(nodes[0].Groups, g => g.TypeCode == 1001);
            Assert.Equal(new[] { "14", "8", "0" }, Of(nodes[1], "AcDbBlockAlignmentGrip", 140).Concat(Of(nodes[1], "AcDbBlockAlignmentGrip", 141)).Concat(Of(nodes[1], "AcDbBlockAlignmentGrip", 142)));
            Assert.Equal(new[] { "UpdatedBaseX" }, Of(nodes[2], "AcDbBlockGripExpr", 300));

            // The sample's graph: the grip to the parameter first, then the parameter to each component.
            Assert.Equal(new[] { 96, 4, 97, 4 }, GraphNumbers(block).Take(4));
            List<int> graph = GraphNumbers(block);
            Assert.Equal(new[] { 92, 0, 93, 0, 94, 1, 91, 1, 91, 0 }, graph.Skip(4 + 4 * 14).Take(10));

            Assert.Empty(TheInsert(Reopened(db)).DynamicBlockReferencePropertyCollection);
            Assert.True(TheInsert(Reopened(db)).IsDynamicBlock);
            Assert.Throws<InvalidOperationException>(() => block.AddAlignmentParameter(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
        }

        [Fact]
        public void ABasePointParameter_IsLaidOutAsAutoCadSavedTheOneOfItsSample_AndAStretchBesideItIsWorkedOut()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            block.AddBasePointParameter(new GeoPoint3(5, 5, 0));
            block.AddAlignmentParameter(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

            DxfItem basePoint = Nodes(block)[8];
            Assert.Equal("BLOCKBASEPOINTPARAMETER", basePoint.Type);
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 280, 281, 100, 1010, 1020, 1030, 93, 170, 171, 100, 1011, 1021, 1031, 1012, 1022, 1032 }, Codes(basePoint));
            Assert.Equal(new[] { "Base Point" }, Of(basePoint, "AcDbBlockElement", 300));
            Assert.Equal(new[] { "0" }, Of(basePoint, "AcDbBlock1PtParameter", 93));
            Assert.Throws<InvalidOperationException>(() => block.AddBasePointParameter(new GeoPoint3(0, 0, 0)));

            DxfBlockReference insert = TheInsert(db);
            Assert.Equal(new[] { "Width" }, insert.DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
            Property(insert, "Width").Value = 200.0;
            Assert.True(Is(Shown<DxfLine>(TheInsert(Reopened(db)))[0], 200, 0, 200, 50));
        }

        // ---- a flip with other actions ---------------------------------------------------------------------------------------

        [Fact]
        public void AFlipInABlockOfAStretch_IsRefusedEitherWayRound_AsAutoCadDidNotFlipTheBlocksOwnInsertInV20b()
        {
            // Plugin v20b, 24d-i: the stretch of the right edge and the flip of a mark and a circle the stretch does not hold;
            // AutoCAD flipped an INSERT the library had set, but set to 1 on the INSERT of the block itself it read back 0.
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfBlockLinearParameter width, out DxfLine[] lines);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            DxfLine mark = Add(block, new DxfLine(new GeoPoint3(20, 10, 0), new GeoPoint3(20, 40, 0)));
            DxfBlockFlipParameter hand = block.AddFlipParameter("Hand", null, new GeoPoint3(50, 0, 0), new GeoPoint3(50, 50, 0));
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => block.AddFlipAction(hand, new[] { mark }));
            Assert.Equal(before, db.ToText());

            DxfDatabase door = Drawing("GH_DOOR", out DxfBlockTableRecord leafBlock);
            DxfLine leaf = Add(leafBlock, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0)));
            DxfCircle knob = Add(leafBlock, new DxfCircle(new GeoPoint3(80, 5, 0), GeoVector3.ZAxis, 2));
            leafBlock.AddFlipAction(leafBlock.AddFlipParameter("Hand", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)), new[] { leaf });
            DxfBlockLinearParameter reach = leafBlock.AddLinearParameter("Reach", null, new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0), -10, null);
            DxfBlockRotationParameter turn = leafBlock.AddRotationParameter("Turn", null, new GeoPoint3(80, 5, 0), 10, 0);
            string flipped = door.ToText();

            Assert.Throws<NotSupportedException>(() => leafBlock.AddMoveAction(reach, new[] { knob }));
            Assert.Throws<NotSupportedException>(() => leafBlock.AddStretchAction(reach, new GeoPoint2(70, -10), new GeoPoint2(100, 10), new[] { knob }));
            Assert.Throws<NotSupportedException>(() => leafBlock.AddScaleAction(reach, new[] { knob }));
            Assert.Throws<NotSupportedException>(() => leafBlock.AddRotateAction(turn, new[] { knob }));
            Assert.Throws<NotSupportedException>(() => leafBlock.AddArrayAction(reach, new[] { knob }, 30));
            Assert.Equal(flipped, door.ToText());

            // A flip alone, as AutoCAD flipped both ways in v20 (24d-g), and a second flip beside it, are written.
            leafBlock.AddFlipAction(leafBlock.AddFlipParameter("Side", null, new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)), new[] { knob });
            Property(TheInsert(door), "Side").Value = (short)1;
            Assert.True(At(Assert.Single(Shown<DxfCircle>(TheInsert(Reopened(door)))).Center, 80, -5));
        }

        // ---- the guide's examples ------------------------------------------------------------------------------------------

        [Fact]
        public void TheGuidesExampleOfEachNewKind_RunsFromNothing_AndReadsBackInR2018()
        {
            var db = new DxfDatabase("AC1032");

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

            // The guide's examples, as they stand there.
            DxfBlockTableRecord bend = Block("BEND");
            var edge = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            var swing = new DxfArc(new GeoPoint3(90, 25, 0), 10, 0, Math.PI / 2);
            bend.AppendEntity(edge);
            bend.AppendEntity(swing);
            DxfBlockLinearParameter width = bend.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            bend.AddStretchAction(width, new GeoPoint2(95, -10), new GeoPoint2(130, 60), new DxfEntity[] { edge, swing });

            DxfBlockTableRecord centred = Block("CENTRED");
            var right = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            var mark = new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5);
            centred.AppendEntity(right);
            centred.AppendEntity(mark);
            DxfBlockLinearParameter span = centred.AddLinearParameter("Span", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            centred.AddStretchAction(span, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right });
            centred.AddMoveAction(span, new[] { mark }, 0.5, 0);

            DxfBlockTableRecord fence = Block("FENCE");
            var post = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
            fence.AppendEntity(post);
            DxfBlockLinearParameter length = fence.AddLinearParameter("Length", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            fence.AddArrayAction(length, new[] { post }, 60);

            DxfBlockTableRecord plate = Block("PLATE");
            var bolt = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
            plate.AppendEntity(bolt);
            DxfBlockXYParameter grid = plate.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(40, 30, 0));
            plate.AddArrayAction(grid, new[] { bolt }, 25, 20);

            DxfBlockTableRecord sized = Block("SIZED");
            var top = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0));
            var side = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            sized.AppendEntity(top);
            sized.AppendEntity(side);
            DxfBlockLinearParameter w = sized.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            sized.AddStretchAction(w, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { top, side });
            DxfBlockLinearParameter h = sized.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 20, null);
            sized.AddStretchAction(h, new GeoPoint2(-10, 40), new GeoPoint2(110, 60), new[] { top, side });
            sized.AddLookupParameter("Size", "Panel size", new GeoPoint3(50, -20, 0),
                new[] { DxfBlockLookupInput.Distance(w), DxfBlockLookupInput.Distance(h) },
                new[] { new DxfBlockLookupRow("Small", 100, 50), new DxfBlockLookupRow("Large", 200, 80) });

            DxfBlockTableRecord pipe = Block("PIPE");
            pipe.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)));
            pipe.AddAlignmentParameter(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0));
            pipe.AddBasePointParameter(new GeoPoint3(0, 0, 0));

            Place(bend, 0, 0).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 200.0;
            Place(centred, 0, 200).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Span").Value = 200.0;
            Place(fence, 0, 400).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Length").Value = 250.0;
            DxfBlockReference sheet = Place(plate, 300, 400);
            sheet.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 100.0;
            sheet.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Depth").Value = 70.0;
            Place(sized, 600, 400).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Size").Value = "Large";
            Place(pipe, 900, 400);

            // What the guide says each shows, read back from the text of the drawing.
            DxfDatabase back = Reopened(db);
            Assert.Equal("AC1032", back.Version);
            List<DxfBlockReference> read = back.ModelSpace.OfType<DxfBlockReference>().ToList();
            DxfArc bent = Assert.Single(Shown<DxfArc>(read[0]));
            Assert.True(At(bent.StartPoint, 200, 25));
            Assert.True(At(bent.EndPoint, 90, 35));
            Assert.Equal(522.132034, bent.Radius, 1E-6);
            Assert.True(At(Assert.Single(Shown<DxfCircle>(read[1])).Center, 100, 25));
            Assert.Equal(new[] { 0.0, 60, 120, 180 }, Shown<DxfCircle>(read[2]).Select(c => c.Center.X));
            Assert.Equal(12, Shown<DxfCircle>(read[3]).Count);
            Assert.Equal("Large", Property(read[4], "Size").Value);
            Assert.Equal(200.0, ValueOf(read[4], "Width"), Close);
            Assert.Equal(80.0, ValueOf(read[4], "Height"), Close);
            Assert.Empty(read[5].DynamicBlockReferencePropertyCollection);
            Assert.True(read[5].IsDynamicBlock);
            Assert.Equal(back.ToText(), DxfDatabase.Parse(back.ToText()).ToText());
        }
    }
}
