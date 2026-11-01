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
    /// Dynamic blocks drawn in AutoCAD's Block Editor as users draw them (D24g): stretches and moves following a parameter's
    /// base point (BaseXDelta, BaseYDelta) besides its end point, moves that carry other linear parameters along (their node
    /// ids listed by 91 after the count 70 of the action, the parameters and their grips among what it moves), moves of a
    /// distance multiplier of 0.5 listing their own parameter's grip, and a point parameter's move holding attribute
    /// definitions, texts and lines, whose ATTRIBs on the INSERT follow. The blocks are built here as look-alikes of such a
    /// drawing: a frame whose Depth stretches its bottom and carries Bottom down, Top and Bottom stretching its corners, a
    /// circle kept in the middle, and a Mark moving a note.
    /// </summary>
    public class DxfDynamicBlockD24gTests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        // ---- building the look-alike ---------------------------------------------------------------------------------------

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

        /// <summary>The closed outline of the frame: its corners top left (0, 60), top right (100, 60), bottom right (100, 0) and bottom left (0, 0), in that order.</summary>
        private static DxfPolyline Outline(DxfBlockTableRecord block)
        {
            var outline = new DxfPolyline();
            outline.AddVertexAt(0, new GeoPoint2(0, 60), 0, 0, 0);
            outline.AddVertexAt(1, new GeoPoint2(100, 60), 0, 0, 0);
            outline.AddVertexAt(2, new GeoPoint2(100, 0), 0, 0, 0);
            outline.AddVertexAt(3, new GeoPoint2(0, 0), 0, 0, 0);
            outline.Closed = true;
            return Add(block, outline);
        }

        /// <summary>The nodes of a block's evaluation graph of a type, in the order of the graph.</summary>
        private static List<DxfItem> Nodes(DxfBlockTableRecord block, string type)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            return graph.Item.Groups.Where(g => g.TypeCode == 360).Select(g => graph.Item.File.ObjectItem(g.Value)).Where(n => n.Type == type).ToList();
        }

        private static string IdOf(DxfItem node) => node.Groups.First(g => g.TypeCode == 90).Value.Trim();

        /// <summary>The handle of the grip of a parameter of two points standing at the point given.</summary>
        private static string GripOf(DxfBlockTableRecord block, GeoPoint3 at)
        {
            DxfItem grip = Nodes(block, "BLOCKLINEARGRIP").Concat(Nodes(block, "BLOCKXYGRIP")).First(n =>
            {
                double x = double.Parse(n.Groups.First(g => g.TypeCode == 1010).Value, CultureInfo.InvariantCulture);
                double y = double.Parse(n.Groups.First(g => g.TypeCode == 1020).Value, CultureInfo.InvariantCulture);
                return new GeoPoint3(x, y, 0).IsEqualTo(at, Exact);
            });
            return grip.Handle;
        }

        /// <summary>Rewrites the groups of a node, as AutoCAD's Block Editor writes what the library's API does not.</summary>
        private static void Rewrite(DxfItem node, Func<List<KeyValuePair<int, string>>, List<KeyValuePair<int, string>>> change)
        {
            DxfFile file = node.File;
            List<KeyValuePair<int, string>> groups = file.TailOf(node, 0).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            file.ReplaceTail(node, 0, change(groups));
        }

        /// <summary>Makes a stretch or a move follow its parameter's base point, BaseXDelta and BaseYDelta, as AutoCAD writes one tied to the base grip.</summary>
        private static void ByBase(DxfItem action) => Rewrite(action, groups => groups.Select(g =>
            g.Key == 301 && g.Value == "EndXDelta" ? new KeyValuePair<int, string>(301, "BaseXDelta")
            : g.Key == 302 && g.Value == "EndYDelta" ? new KeyValuePair<int, string>(302, "BaseYDelta") : g).ToList());

        /// <summary>
        /// Makes a move hold what is given instead of its entities, as AutoCAD writes a move whose selection holds other
        /// parameters: the count of the parameters (70) and their node ids (91), then the count (71) and the handles (330).
        /// </summary>
        private static void Holding(DxfItem move, IEnumerable<string> parameters, IEnumerable<string> handles) => Rewrite(move, groups =>
        {
            int at = groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbBlockAction");
            int end = groups.FindIndex(at, g => g.Key == 1010);
            var held = new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(70, parameters.Count().ToString(CultureInfo.InvariantCulture)) };
            held.AddRange(parameters.Select(p => new KeyValuePair<int, string>(91, p)));
            held.Add(new KeyValuePair<int, string>(71, handles.Count().ToString(CultureInfo.InvariantCulture)));
            held.AddRange(handles.Select(h => new KeyValuePair<int, string>(330, h)));
            return groups.Take(at + 1).Concat(held).Concat(groups.Skip(end)).ToList();
        });

        /// <summary>
        /// The frame: Depth from (40, 60) down to (40, 0) stretching the bottom corners by its end point and the top corners by
        /// its base point, a circle at (40, 30) moved half way by each, the move by the end point listing Depth's end grip too;
        /// Top from (0, 60) to (100, 60) stretching the top left corner by its base point and the top right by its end point,
        /// Bottom from (0, 0) to (100, 0) the bottom left and the bottom right the same way; a move by Depth's base point
        /// carrying Top, with Top's grips and Depth's base grip, and a move by Depth's end point carrying Bottom alone.
        /// </summary>
        private static DxfDatabase Frame(out DxfBlockTableRecord block, out DxfPolyline outline, out DxfCircle middle)
        {
            DxfDatabase db = Drawing("GH_FRAME", out block);
            outline = Outline(block);
            middle = Add(block, new DxfCircle(new GeoPoint3(40, 30, 0), GeoVector3.ZAxis, 3));
            DxfBlockLinearParameter depth = block.AddLinearParameter("Depth", null, new GeoPoint3(40, 60, 0), new GeoPoint3(40, 0, 0), -20, null);
            block.AddStretchAction(depth, new GeoPoint2(-10, -10), new GeoPoint2(110, 10), new[] { outline });
            block.AddStretchAction(depth, new GeoPoint2(-10, 50), new GeoPoint2(110, 70), new[] { outline });
            block.AddMoveAction(depth, new[] { middle }, 0.5, 0);
            block.AddMoveAction(depth, new[] { middle }, 0.5, 0);
            DxfBlockLinearParameter top = block.AddLinearParameter("Top", null, new GeoPoint3(0, 60, 0), new GeoPoint3(100, 60, 0), 20, null);
            block.AddStretchAction(top, new GeoPoint2(-10, 50), new GeoPoint2(10, 70), new[] { outline });
            block.AddStretchAction(top, new GeoPoint2(90, 50), new GeoPoint2(110, 70), new[] { outline });
            block.AddMoveAction(depth, new[] { middle });
            DxfBlockLinearParameter bottom = block.AddLinearParameter("Bottom", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            block.AddStretchAction(bottom, new GeoPoint2(-10, -10), new GeoPoint2(10, 10), new[] { outline });
            block.AddStretchAction(bottom, new GeoPoint2(90, -10), new GeoPoint2(110, 10), new[] { outline });
            block.AddMoveAction(depth, new[] { middle });

            List<DxfItem> stretches = Nodes(block, "BLOCKSTRETCHACTION");
            List<DxfItem> moves = Nodes(block, "BLOCKMOVEACTION");
            ByBase(stretches[1]);
            ByBase(stretches[2]);
            ByBase(stretches[4]);
            Holding(moves[0], new string[0], new[] { GripOf(block, depth.EndPoint), middle.Handle });
            ByBase(moves[1]);
            Holding(moves[2], new[] { IdOf(Nodes(block, "BLOCKLINEARPARAMETER")[1]) }, new[] { GripOf(block, top.BasePoint), GripOf(block, top.EndPoint), GripOf(block, depth.BasePoint), top.Handle });
            ByBase(moves[2]);
            Holding(moves[3], new[] { IdOf(Nodes(block, "BLOCKLINEARPARAMETER")[2]) }, new[] { bottom.Handle });
            return db;
        }

        /// <summary>
        /// A note: Mark at (20, 30), whose move holds its own grip, a text, a line and two attribute definitions, NOTE at
        /// (25, 35) on its baseline and SIZE centred on (20, 40), locked in the block as AutoCAD's Block Editor asks of an
        /// attribute an action holds; a third, KEEP at (0, -10), no action holds.
        /// </summary>
        private static DxfDatabase Note(out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_NOTE", out block);
            DxfDBText text = Add(block, new DxfDBText { Position = new GeoPoint3(20, 25, 0), Height = 2.5, TextString = "M" });
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(15, 30, 0), new GeoPoint3(25, 30, 0)));
            DxfAttributeDefinition note = Add(block, new DxfAttributeDefinition(new GeoPoint3(25, 35, 0), "-", "NOTE", "Note", null) { Height = 2.5, LockPositionInBlock = true });
            DxfAttributeDefinition size = Add(block, new DxfAttributeDefinition(new GeoPoint3(20, 40, 0), "100", "SIZE", "Size", null) { Height = 2.5, LockPositionInBlock = true });
            size.HorizontalMode = DxfTextHorizontalMode.TextCenter;
            size.AlignmentPoint = new GeoPoint3(20, 40, 0);
            Add(block, new DxfAttributeDefinition(new GeoPoint3(0, -10, 0), "K", "KEEP", "Keep", null) { Height = 2.5 });
            DxfBlockPointParameter mark = block.AddPointParameter("Mark", null, new GeoPoint3(20, 30, 0));
            block.AddMoveAction(mark, new DxfEntity[] { text, line });
            DxfItem move = Nodes(block, "BLOCKMOVEACTION").Single();
            Holding(move, new string[0], new[] { GripOf(block, new GeoPoint3(20, 30, 0)), note.Handle, text.Handle, size.Handle, line.Handle });
            return db;
        }

        /// <summary>Gives an INSERT an ATTRIB of each attribute definition of its block, filled from it where it stands.</summary>
        private static void Attributes(DxfBlockReference insert)
        {
            foreach (DxfAttributeDefinition definition in insert.BlockTableRecord.OfType<DxfAttributeDefinition>().ToList())
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                insert.AttributeCollection.AppendAttribute(attribute);
            }
        }

        // ---- reading back ----------------------------------------------------------------------------------------------------

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static double ValueOf(DxfBlockReference insert, string name) => Assert.IsType<double>(Property(insert, name).Value);

        private static bool At(GeoPoint3 point, double x, double y) => point.IsEqualTo(new GeoPoint3(x, y, 0), Exact);

        /// <summary>The corners of the outline the INSERT shows, in its order.</summary>
        private static GeoPoint3[] Corners(DxfBlockReference insert)
        {
            DxfPolyline shown = Assert.Single(insert.BlockTableRecord.OfType<DxfPolyline>());
            return Enumerable.Range(0, shown.NumberOfVertices).Select(shown.GetPoint3dAt).ToArray();
        }

        private static void Shows(DxfBlockReference insert, params double[] xy)
        {
            GeoPoint3[] corners = Corners(insert);
            Assert.Equal(xy.Length / 2, corners.Length);

            for (int i = 0; i < corners.Length; i++)
            {
                Assert.True(At(corners[i], xy[2 * i], xy[2 * i + 1]), "corner " + i + " is " + corners[i] + ", not (" + xy[2 * i] + ", " + xy[2 * i + 1] + ")");
            }
        }

        /// <summary>The base point and the end point the INSERT keeps of a parameter in its cache.</summary>
        private static GeoPoint3[] Kept(DxfBlockReference insert, DxfBlockTableRecord block, int linear)
        {
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var data = (DxfDBDictionary)((DxfDBDictionary)representation.GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            DxfItem record = data.GetAt(IdOf(Nodes(insert.DynamicBlockTableRecord, "BLOCKLINEARPARAMETER")[linear])).Item;
            List<double> numbers = record.File.TailOf(record, 0).Where(g => g.TypeCode == 10 || g.TypeCode == 20).Select(g => double.Parse(g.Value, CultureInfo.InvariantCulture)).ToList();
            return new[] { new GeoPoint3(numbers[0], numbers[1], 0), new GeoPoint3(numbers[2], numbers[3], 0) };
        }

        private static DxfAttributeReference Attribute(DxfBlockReference insert, string tag) => insert.AttributeCollection.Single(a => a.Tag == tag);

        // ---- the base grip -------------------------------------------------------------------------------------------------

        [Fact]
        public void AStretchAndAMoveFollowingTheBaseGrip_AreWorkedOut_TheBasePointStayingWhereItIsAsAValueIsSet()
        {
            // A value set moves a linear parameter's end point only, so a stretch or a move of its base point moves nothing:
            // Depth 100 takes the bottom corners down to y -40, the top ones stay at 60, and the circle moves half way, to
            // (40, 10), by the move of the multiplier 0.5 following the end point; the one following the base point is still.
            DxfDatabase db = Drawing("GH_DEPTH", out DxfBlockTableRecord block);
            DxfPolyline outline = Outline(block);
            DxfCircle middle = Add(block, new DxfCircle(new GeoPoint3(40, 30, 0), GeoVector3.ZAxis, 3));
            DxfBlockLinearParameter depth = block.AddLinearParameter("Depth", null, new GeoPoint3(40, 60, 0), new GeoPoint3(40, 0, 0), -20, null);
            block.AddStretchAction(depth, new GeoPoint2(-10, -10), new GeoPoint2(110, 10), new[] { outline });
            block.AddStretchAction(depth, new GeoPoint2(-10, 50), new GeoPoint2(110, 70), new[] { outline });
            block.AddMoveAction(depth, new[] { middle }, 0.5, 0);
            block.AddMoveAction(depth, new[] { middle }, 0.5, 0);
            ByBase(Nodes(block, "BLOCKSTRETCHACTION")[1]);
            ByBase(Nodes(block, "BLOCKMOVEACTION")[1]);
            DxfDatabase reopened = Reopened(db);

            Property(TheInsert(reopened), "Depth").Value = 100.0;

            DxfBlockReference back = TheInsert(Reopened(reopened));
            Assert.Equal(100.0, ValueOf(back, "Depth"), Close);
            Shows(back, 0, 60, 100, 60, 100, -40, 0, -40);
            Assert.True(At(Assert.Single(back.BlockTableRecord.OfType<DxfCircle>()).Center, 40, 10));
        }

        // ---- moves that carry other parameters -----------------------------------------------------------------------------

        [Fact]
        public void AMoveCarryingAnotherParameter_TakesItsPointsAlong_KeepingItsDistanceAndLeavingItsStretchesStill()
        {
            // Depth 100 moves its end point 40 down: the bottom corners follow by the stretch of Depth's end point, and the move
            // of that end point carries Bottom from (0, 0)-(100, 0) to (0, -40)-(100, -40), still 100 long. Top, carried by the
            // move of Depth's base point, which a value set does not move, keeps (0, 60)-(100, 60). Bottom's own stretches do
            // not follow its being carried: the bottom corners move once.
            DxfDatabase db = Reopened(Frame(out _, out _, out _));

            Property(TheInsert(db), "Depth").Value = 100.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(new[] { 100.0, 100, 100 }, new[] { ValueOf(back, "Depth"), ValueOf(back, "Top"), ValueOf(back, "Bottom") });
            Shows(back, 0, 60, 100, 60, 100, -40, 0, -40);
            Assert.True(At(Assert.Single(back.BlockTableRecord.OfType<DxfCircle>()).Center, 40, 10));
            GeoPoint3[] bottom = Kept(back, back.DynamicBlockTableRecord, 2);
            Assert.True(At(bottom[0], 0, -40) && At(bottom[1], 100, -40));
            GeoPoint3[] top = Kept(back, back.DynamicBlockTableRecord, 1);
            Assert.True(At(top[0], 0, 60) && At(top[1], 100, 60));
        }

        [Fact]
        public void AParameterACarriedMoveTook_IsSetAgainFromWhereItWasCarried_AndCarriedOnByTheNextSet()
        {
            // After Depth 100, Bottom keeps its base point at (0, -40), where the move carried it, not the block's own (0, 0):
            // that is no grip edit, and Bottom 150 takes the bottom right corner to (150, -40). Depth 80 then carries Bottom up
            // to y -20 with its 150 kept; Top 130 takes the top right corner to (130, 60).
            DxfDatabase db = Reopened(Frame(out _, out _, out _));
            Property(TheInsert(db), "Depth").Value = 100.0;
            db = Reopened(db);

            Property(TheInsert(db), "Bottom").Value = 150.0;
            db = Reopened(db);
            Shows(TheInsert(db), 0, 60, 100, 60, 150, -40, 0, -40);

            Property(TheInsert(db), "Depth").Value = 80.0;
            db = Reopened(db);
            Property(TheInsert(db), "Top").Value = 130.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(new[] { 80.0, 130, 150 }, new[] { ValueOf(back, "Depth"), ValueOf(back, "Top"), ValueOf(back, "Bottom") });
            Shows(back, 0, 60, 130, 60, 150, -20, 0, -20);
            Assert.True(At(Assert.Single(back.BlockTableRecord.OfType<DxfCircle>()).Center, 40, 20));
            GeoPoint3[] bottom = Kept(back, back.DynamicBlockTableRecord, 2);
            Assert.True(At(bottom[0], 0, -20) && At(bottom[1], 150, -20));
        }

        [Fact]
        public void AnInsertOfTheFrameItself_IsSetPropertyByProperty_AsTheBlockDrawsIt()
        {
            // An INSERT of the block itself, no cache saved, at (500, 200) and twice the size: Top 400 is 200 in the block.
            DxfDatabase db = Frame(out DxfBlockTableRecord block, out _, out _);
            var insert = new DxfBlockReference(new GeoPoint3(500, 200, 0), block) { ScaleFactors = new GeoVector3(2, 2, 2) };
            db.ModelSpace.AppendEntity(insert);
            db = Reopened(db);
            DxfBlockReference placed = db.ModelSpace.OfType<DxfBlockReference>().Last();

            Property(placed, "Top").Value = 400.0;
            Property(placed, "Bottom").Value = 300.0;
            Property(placed, "Depth").Value = 200.0;

            DxfBlockReference back = Reopened(db).ModelSpace.OfType<DxfBlockReference>().Last();
            Assert.Equal(new[] { 200.0, 400, 300 }, new[] { ValueOf(back, "Depth"), ValueOf(back, "Top"), ValueOf(back, "Bottom") });
            Shows(back, 0, 60, 200, 60, 150, -40, 0, -40);
        }

        // ---- attributes a move holds ---------------------------------------------------------------------------------------

        [Fact]
        public void AnAttributeDefinitionAMoveHolds_TakesTheInsertsAttributeAlong_ByTheMoveThePlacingGives()
        {
            // An INSERT at (1000, 500), turned a quarter turn and twice the size: Mark X reads 40 (20 in the block). Set to 60,
            // Mark moves 10 along x in the block, 20 along y in the world: the ATTDEFs NOTE and SIZE of the anonymous block go to
            // (35, 35) and (30, 40), and the INSERT's ATTRIBs NOTE and SIZE move by (0, 20), SIZE's alignment point with it;
            // KEEP, which no action holds, stays.
            DxfDatabase db = Note(out DxfBlockTableRecord block);
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block) { ScaleFactors = new GeoVector3(2, 2, 2), Rotation = Math.PI / 2 };
            db.ModelSpace.AppendEntity(insert);
            Attributes(insert);
            db = Reopened(db);
            DxfBlockReference placed = db.ModelSpace.OfType<DxfBlockReference>().Last();
            GeoPoint3 note = Attribute(placed, "NOTE").Position;
            GeoPoint3 size = Attribute(placed, "SIZE").AlignmentPoint;
            GeoPoint3 keep = Attribute(placed, "KEEP").Position;
            Assert.True(At(note, 930, 550));

            Property(placed, "Mark X").Value = 60.0;

            DxfBlockReference back = Reopened(db).ModelSpace.OfType<DxfBlockReference>().Last();
            Assert.Equal(60.0, ValueOf(back, "Mark X"), Close);
            Assert.True(At(back.BlockTableRecord.OfType<DxfAttributeDefinition>().Single(d => d.Tag == "NOTE").Position, 35, 35));
            Assert.True(At(back.BlockTableRecord.OfType<DxfAttributeDefinition>().Single(d => d.Tag == "SIZE").AlignmentPoint, 30, 40));
            Assert.True(At(Attribute(back, "NOTE").Position, note.X, note.Y + 20));
            Assert.True(At(Attribute(back, "SIZE").AlignmentPoint, size.X, size.Y + 20));
            Assert.True(At(Attribute(back, "KEEP").Position, keep.X, keep.Y));
            Assert.True(At(Assert.Single(back.BlockTableRecord.OfType<DxfLine>()).StartPoint, 25, 30));
        }

        [Fact]
        public void AnAttributeFollowsTheChangeOfItsMove_SoAMarkSetBackTakesItBackWhereItWas()
        {
            // Mark Y 50 then back to 30 on an INSERT unturned at the origin: SIZE's alignment point goes to (20, 60), then back
            // to (20, 40); NOTE, which the user moved by hand to (70, 35) before, is moved by the same changes, to (70, 55) and
            // back.
            DxfDatabase db = Note(out _);
            DxfBlockReference insert = TheInsert(db);
            Attributes(insert);
            Attribute(insert, "NOTE").Position = new GeoPoint3(70, 35, 0);
            db = Reopened(db);

            Property(TheInsert(db), "Mark Y").Value = 50.0;
            db = Reopened(db);
            Assert.True(At(Attribute(TheInsert(db), "SIZE").AlignmentPoint, 20, 60));
            Assert.True(At(Attribute(TheInsert(db), "NOTE").Position, 70, 55));

            Property(TheInsert(db), "Mark Y").Value = 30.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.True(At(Attribute(back, "SIZE").AlignmentPoint, 20, 40));
            Assert.True(At(Attribute(back, "NOTE").Position, 70, 35));
        }

        // ---- the guide's examples, copied together -------------------------------------------------------------------------

        [Fact]
        public void TheGuidesExamplesPastedTogether_RunWithoutAClashOfNames_AndEachShowsWhatTheGuideSays()
        {
            // Every example of the guide's dynamic blocks, as it stands there, one after another in the guide's order, their
            // db.Save left out: the first a whole program, each after it a block of its own whose names are its own, so that an
            // AI pasting them together gets no clash of a name or of a block's name.
            var db = new DxfDatabase("AC1024");                              // a new drawing of R2010, in millimetres
            var panel = new DxfBlockTableRecord { Name = "PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(panel);
            var right = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            var bottom = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var top = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0));
            var left = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0));
            foreach (DxfLine line in new[] { right, bottom, top, left })
            {
                panel.AppendEntity(line);
            }

            DxfBlockLinearParameter width = panel.AddLinearParameter("Width", "Overall width", new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10));
            panel.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right, bottom, top });
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), panel);
            db.ModelSpace.AppendEntity(insert);
            insert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 200.0;

            {
                var sign = new DxfBlockTableRecord { Name = "SIGN", Origin = new GeoPoint3(0, 0, 0) };
                db.BlockTable.Add(sign);
                var frame = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0));
                var cross = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0));
                var tick = new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0));
                foreach (DxfLine signLine in new[] { frame, cross, tick })
                {
                    sign.AppendEntity(signLine);
                }

                sign.AddVisibilityParameter("Mark", "What the sign shows", new GeoPoint3(20, -10, 0), new[]
                {
                    new DxfBlockVisibilityState("Cross", new DxfEntity[] { cross }),
                    new DxfBlockVisibilityState("Tick", new DxfEntity[] { tick }),
                    new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
                });
                var mark = new DxfBlockReference(new GeoPoint3(200, 0, 0), sign);
                db.ModelSpace.AppendEntity(mark);
                mark.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Mark").Value = "Tick";
            }

            {
                // Each example adds a block to the drawing above; Block makes it, Place puts an INSERT of it in model space.
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

                // Move: a tag circle follows the end point of Length; Length 200 puts its centre at (190, 25).
                DxfBlockTableRecord slide = Block("SLIDE");
                var tagCircle = new DxfCircle(new GeoPoint3(90, 25, 0), GeoVector3.ZAxis, 5);
                slide.AppendEntity(tagCircle);
                DxfBlockLinearParameter length = slide.AddLinearParameter("Length", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
                slide.AddMoveAction(length, new[] { tagCircle });

                // Scale: a disc of the radius 25 at (50, 25) scaled about the origin by Size over 100; Size 200 doubles it, about (100, 50).
                DxfBlockTableRecord disc = Block("DISC");
                var round = new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 25);
                disc.AppendEntity(round);
                DxfBlockLinearParameter size = disc.AddLinearParameter("Size", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
                disc.AddScaleAction(size, new[] { round });

                // Point and move: Tag X and Tag Y move the tag anywhere; Tag X 120 puts its centre at (120, 20).
                DxfBlockTableRecord tagged = Block("TAGGED");
                var tag = new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5);
                tagged.AppendEntity(tag);
                DxfBlockPointParameter where = tagged.AddPointParameter("Tag", "Where the tag stands", new GeoPoint3(80, 20, 0));
                tagged.AddMoveAction(where, new[] { tag });

                // Rotation and rotate: Angle, in radians, turns the arrow about its tail; Math.PI / 2 makes it point along y, to (50, 55).
                DxfBlockTableRecord arrowBlock = Block("ARROW");
                var arrow = new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0));
                arrowBlock.AppendEntity(arrow);
                DxfBlockRotationParameter angle = arrowBlock.AddRotationParameter("Angle", "Turn of the arrow", new GeoPoint3(50, 25, 0), 30, 0);
                arrowBlock.AddRotateAction(angle, new[] { arrow });

                // Flip and flip: Hand 1 mirrors the leaf about the y axis, from (0, 0)-(90, 0) to (0, 0)-(-90, 0).
                DxfBlockTableRecord door = Block("DOOR");
                var leaf = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0));
                door.AppendEntity(leaf);
                DxfBlockFlipParameter hand = door.AddFlipParameter("Hand", "Which way the door opens", new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));
                door.AddFlipAction(hand, new[] { leaf });

                // XY: Width and Depth move the top right corner of the plate and the hole on it both ways; 160 by 80 takes them to (160, 80).
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
            }

            {
                // This example's own helpers: AddBlock makes a block of the drawing above, PlaceAt puts an INSERT of it in model space.
                DxfBlockTableRecord AddBlock(string name)
                {
                    var made = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
                    db.BlockTable.Add(made);
                    return made;
                }

                DxfBlockReference PlaceAt(DxfBlockTableRecord block, double x, double y)
                {
                    var placedAt = new DxfBlockReference(new GeoPoint3(x, y, 0), block);
                    db.ModelSpace.AppendEntity(placedAt);
                    return placedAt;
                }

                // Stretch of an arc by one end: its start (100, 25) is in the frame, its end (90, 35) is not; Width 200 moves the start to
                // (200, 25) and the arc keeps the height of its middle off its chord, 2.929, its radius becoming 522.13.
                DxfBlockTableRecord bend = AddBlock("BEND");
                var edge = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
                var swing = new DxfArc(new GeoPoint3(90, 25, 0), 10, 0, Math.PI / 2);
                bend.AppendEntity(edge);
                bend.AppendEntity(swing);
                DxfBlockLinearParameter bendWidth = bend.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
                bend.AddStretchAction(bendWidth, new GeoPoint2(95, -10), new GeoPoint2(130, 60), new DxfEntity[] { edge, swing });

                // Distance multiplier: the mark moves half as far as the end point, staying in the middle; Span 200 puts it at (100, 25).
                DxfBlockTableRecord centred = AddBlock("CENTRED");
                var centredEdge = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
                var centredMark = new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5);
                centred.AppendEntity(centredEdge);
                centred.AppendEntity(centredMark);
                DxfBlockLinearParameter span = centred.AddLinearParameter("Span", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
                centred.AddStretchAction(span, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { centredEdge });
                centred.AddMoveAction(span, new[] { centredMark }, 0.5, 0);

                // Array of a linear parameter: a post every 60 along Length; AutoCAD set Length 250 and drew four, at x = 0, 60, 120 and 180.
                DxfBlockTableRecord fence = AddBlock("FENCE");
                var post = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
                fence.AppendEntity(post);
                DxfBlockLinearParameter fenceLength = fence.AddLinearParameter("Length", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
                fence.AddArrayAction(fenceLength, new[] { post }, 60);

                // Array of an XY parameter: columns every 25 along x, rows every 20 along y; Width 100 and Depth 70 show 4 by 3 bolts.
                DxfBlockTableRecord bolted = AddBlock("BOLTS");
                var bolt = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
                bolted.AppendEntity(bolt);
                DxfBlockXYParameter grid = bolted.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(40, 30, 0));
                bolted.AddArrayAction(grid, new[] { bolt }, 25, 20);

                // Lookup: Size sets Width and Height by the row of its label; Large gives 200 by 80, and Width 150 then reads Custom.
                DxfBlockTableRecord sized = AddBlock("SIZED");
                var sizedTop = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0));
                var sizedSide = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
                sized.AppendEntity(sizedTop);
                sized.AppendEntity(sizedSide);
                DxfBlockLinearParameter w = sized.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
                sized.AddStretchAction(w, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { sizedTop, sizedSide });
                DxfBlockLinearParameter h = sized.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 20, null);
                sized.AddStretchAction(h, new GeoPoint2(-10, 40), new GeoPoint2(110, 60), new[] { sizedTop, sizedSide });
                sized.AddLookupParameter("Size", "Panel size", new GeoPoint3(50, -20, 0),
                    new[] { DxfBlockLookupInput.Distance(w), DxfBlockLookupInput.Distance(h) },
                    new[] { new DxfBlockLookupRow("Small", 100, 50), new DxfBlockLookupRow("Large", 200, 80) });

                // Alignment and base point: grips AutoCAD uses when the block is placed; they give no property.
                DxfBlockTableRecord pipe = AddBlock("PIPE");
                pipe.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)));
                pipe.AddAlignmentParameter(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0));
                pipe.AddBasePointParameter(new GeoPoint3(0, 0, 0));

                PlaceAt(bend, 0, 0).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 200.0;
                PlaceAt(centred, 0, 200).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Span").Value = 200.0;
                PlaceAt(fence, 0, 400).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Length").Value = 250.0;
                DxfBlockReference boltedInsert = PlaceAt(bolted, 300, 400);
                boltedInsert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 100.0;
                boltedInsert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Depth").Value = 70.0;
                PlaceAt(sized, 600, 400).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Size").Value = "Large";
                PlaceAt(pipe, 900, 400);
            }

            DxfDatabase back = Reopened(db);
            List<DxfBlockReference> read = back.ModelSpace.OfType<DxfBlockReference>().ToList();
            Assert.Equal(14, read.Count);
            Assert.Equal(new[] { "PANEL", "SIGN", "SLIDE", "DISC", "TAGGED", "ARROW", "DOOR", "PLATE", "BEND", "CENTRED", "FENCE", "BOLTS", "SIZED", "PIPE" }, read.Select(r => r.DynamicBlockTableRecord.Name));
            Assert.Equal(200.0, ValueOf(read[0], "Width"), Close);
            Assert.Equal("Tick", Property(read[1], "Mark").Value);
            Assert.Equal(new[] { 160.0, 80.0 }, new[] { ValueOf(read[7], "Width"), ValueOf(read[7], "Depth") });
            Assert.Equal(new[] { 100.0, 70.0 }, new[] { ValueOf(read[11], "Width"), ValueOf(read[11], "Depth") });
            Assert.Equal(12, read[11].BlockTableRecord.OfType<DxfCircle>().Count());
            Assert.Equal("Large", Property(read[12], "Size").Value);
        }

        // ---- what stays refused --------------------------------------------------------------------------------------------

        [Fact]
        public void ACarriedParameterThatChainsItsActions_IsNotSet_AsNoSampleShowsTheOrderAutoCadTakes()
        {
            // Top made to chain its actions (281 of 1): carried by Depth's base point, it would stretch the corners it holds
            // again. AutoCAD's samples show no such block, so the library sets none of its properties, and writes nothing.
            DxfDatabase db = Frame(out DxfBlockTableRecord block, out _, out _);
            Rewrite(Nodes(block, "BLOCKLINEARPARAMETER")[1], groups =>
            {
                int at = groups.FindIndex(g => g.Key == 281);
                groups[at] = new KeyValuePair<int, string>(281, "1");
                return groups;
            });
            db = Reopened(db);
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => Property(TheInsert(db), "Depth").Value = 100.0);
            Assert.Throws<NotSupportedException>(() => Property(TheInsert(db), "Top").Value = 120.0);
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AMoveHoldingAGripOfAnotherParameterButNotTheParameter_IsNotSet()
        {
            // The move of Depth's end point made to hold Bottom's end grip alone: that would move one end of Bottom, an edit no
            // sample shows, so nothing is set.
            DxfDatabase db = Frame(out DxfBlockTableRecord block, out _, out _);
            Holding(Nodes(block, "BLOCKMOVEACTION")[3], new string[0], new[] { GripOf(block, new GeoPoint3(100, 0, 0)) });
            db = Reopened(db);
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => Property(TheInsert(db), "Depth").Value = 100.0);
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AMoveCarryingAPointParameter_IsNotSet()
        {
            // Only linear parameters are carried: a point parameter carried by a move would read its position from where the
            // move left it, and is not worked out.
            DxfDatabase db = Note(out DxfBlockTableRecord block);
            DxfBlockLinearParameter reach = block.AddLinearParameter("Reach", null, new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0), -20, null);
            DxfLine rule = Add(block, new DxfLine(new GeoPoint3(0, -5, 0), new GeoPoint3(50, -5, 0)));
            block.AddMoveAction(reach, new[] { rule });
            DxfItem point = Nodes(block, "BLOCKPOINTPARAMETER").Single();
            Holding(Nodes(block, "BLOCKMOVEACTION").Last(), new[] { IdOf(point) }, new[] { point.Handle });
            db = Reopened(db);
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => Property(TheInsert(db), "Reach").Value = 80.0);
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void ABasePointAGripMovedBeyondWhereAMoveCarriedIt_IsStillNotSet()
        {
            // Bottom's base point kept at (5, -40) after Depth 100, 5 along x from where the move carried it: a grip moved it,
            // and the INSERT is not set, as before (D24f).
            DxfDatabase db = Reopened(Frame(out _, out _, out _));
            Property(TheInsert(db), "Depth").Value = 100.0;
            DxfBlockReference insert = TheInsert(db);
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var data = (DxfDBDictionary)((DxfDBDictionary)representation.GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            DxfItem record = data.GetAt(IdOf(Nodes(insert.DynamicBlockTableRecord, "BLOCKLINEARPARAMETER")[2])).Item;
            Rewrite(record, groups =>
            {
                int at = groups.FindIndex(g => g.Key == 10);
                groups[at] = new KeyValuePair<int, string>(10, "5.0");
                return groups;
            });
            db = Reopened(db);
            string before = db.ToText();

            Assert.Equal(95.0, ValueOf(TheInsert(db), "Bottom"), Close);
            Assert.Throws<NotSupportedException>(() => Property(TheInsert(db), "Depth").Value = 80.0);
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void TwoAttributeDefinitionsOfOneTagMovedApart_AreNotSet()
        {
            // An ATTRIB is told to its ATTDEF by its tag: two ATTDEFs NOTE, one moved by Mark and one not, leave the INSERT's
            // NOTE without one move to follow, and nothing is set.
            DxfDatabase db = Note(out DxfBlockTableRecord block);
            Add(block, new DxfAttributeDefinition(new GeoPoint3(0, -20, 0), "-", "NOTE", "Note", null) { Height = 2.5 });
            Attributes(TheInsert(db));
            db = Reopened(db);
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => Property(TheInsert(db), "Mark X").Value = 30.0);
            Assert.Equal(before, db.ToText());
        }
    }
}
