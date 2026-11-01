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
    /// The dynamic blocks of D25b: the ties of the value sets of distances, a stretch and a move tied to a polar parameter, and
    /// lookup columns of an XY, a polar and a rotation parameter. Each value below is AutoCAD 2021's, as it set the library's
    /// files in plugin D25b (358 ties of lists, two sessions alike), to 1E-9.
    /// </summary>
    public class DxfDynamicBlockD25bTests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        private static readonly double[] Six = { 50, 100, 150, 200, 250, 300 };

        private static double Deg(double degrees) => degrees * Math.PI / 180.0;

        private static DxfDatabase Drawing(string name, out DxfBlockTableRecord block)
        {
            var db = new DxfDatabase("AC1024");
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

        private static DxfBlockReference Place(DxfDatabase db, DxfBlockTableRecord block, double rotation = 0, double scale = 1)
        {
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block) { Rotation = rotation, ScaleFactors = new GeoVector3(scale, scale, scale) };
            db.ModelSpace.AppendEntity(insert);
            return insert;
        }

        private static DxfPolyline Poly(params double[] xy)
        {
            var polyline = new DxfPolyline();

            for (int i = 0; i < xy.Length / 2; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(xy[2 * i], xy[2 * i + 1]), 0, 0, 0);
            }

            return polyline;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static object ValueOf(DxfBlockReference insert, string name) => Property(insert, name).Value;

        private static List<DxfEntity> Shown(DxfBlockReference insert) => insert.BlockTableRecord.Cast<DxfEntity>().ToList();

        private static bool At(GeoPoint3 point, double x, double y) => point.IsEqualTo(new GeoPoint3(x, y, 0), Exact);

        private static bool Is(DxfEntity entity, double x1, double y1, double x2, double y2)
        {
            var line = (DxfLine)entity;
            return At(line.StartPoint, x1, y1) && At(line.EndPoint, x2, y2);
        }

        /// <summary>The objects of the graph's nodes of a block, in its order.</summary>
        private static List<DxfItem> Nodes(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            return graph.Item.Groups.Where(g => g.TypeCode == 360).Select(g => graph.Item.File.ObjectItem(g.Value)).ToList();
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

        /// <summary>The distance an INSERT reads after each value is set in turn, read again from the drawing written out.</summary>
        private static double After(DxfDatabase db, string name, params double[] values)
        {
            foreach (double value in values)
            {
                Property(TheInsert(db), name).Value = value;
            }

            return Assert.IsType<double>(ValueOf(TheInsert(Reopened(db)), name));
        }

        // ---- 1. the ties of a list, from the block's own value -----------------------------------------------------------------

        /// <summary>The panel of plugin D25b: a right edge and a top stretched by Width from (0, 0, 0) to (own, 0, 0), of the set given.</summary>
        private static DxfDatabase Panel(double own, DxfBlockParameterValueSet set, out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_PANEL", out block);
            DxfLine edge = Add(block, new DxfLine(new GeoPoint3(own, 0, 0), new GeoPoint3(own, 50, 0)));
            DxfLine top = Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(own, 50, 0)));
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(own, 0, 0), -20, set);
            block.AddStretchAction(width, new GeoPoint2(own - 5, -10), new GeoPoint2(own + 5, 60), new[] { edge, top });
            Place(db, block);
            return db;
        }

        [Theory]
        [InlineData(100.0, 300.0, 75.0, 100.0)]
        [InlineData(100.0, 50.0, 125.0, 100.0)]
        [InlineData(100.0, 50.0, 175.0, 150.0)]
        [InlineData(200.0, 300.0, 175.0, 200.0)]
        [InlineData(200.0, 50.0, 225.0, 200.0)]
        [InlineData(200.0, 100.0, 75.0, 50.0)]
        [InlineData(200.0, 50.0, 275.0, 250.0)]
        [InlineData(300.0, 300.0, 75.0, 50.0)]
        [InlineData(300.0, 50.0, 275.0, 300.0)]
        [InlineData(50.0, 300.0, 75.0, 50.0)]
        [InlineData(50.0, 50.0, 125.0, 100.0)]
        public void ALinearListTie_GoesToTheBlocksOwnValueWhereItIsOneOfTheTwo_ElseToTheLesser(double own, double from, double asked, double taken)
        {
            DxfDatabase db = Panel(own, DxfBlockParameterValueSet.List(Six), out _);

            double read = own == from ? After(db, "Width", asked) : After(db, "Width", from, asked);

            Assert.Equal(taken, read, Close);
            Assert.True(Is(Shown(TheInsert(Reopened(db)))[0], taken, 0, taken, 50));
        }

        [Theory]
        [InlineData(new[] { 20.0, 60, 100 }, 60.0, 40.0, 60.0)]
        [InlineData(new[] { 20.0, 60, 100 }, 60.0, 80.0, 60.0)]
        [InlineData(new[] { 10.0, 30, 50, 70 }, 70.0, 20.0, 10.0)]
        [InlineData(new[] { 10.0, 30, 50, 70 }, 70.0, 40.0, 30.0)]
        [InlineData(new[] { 10.0, 30, 50, 70 }, 70.0, 60.0, 70.0)]
        [InlineData(new[] { 30.0, 50, 90, 170 }, 90.0, 40.0, 30.0)]
        [InlineData(new[] { 30.0, 50, 90, 170 }, 90.0, 70.0, 90.0)]
        [InlineData(new[] { 30.0, 50, 90, 170 }, 90.0, 130.0, 90.0)]
        [InlineData(new[] { 100.0, 140, 180, 220, 260 }, 180.0, 120.0, 100.0)]
        [InlineData(new[] { 100.0, 140, 180, 220, 260 }, 180.0, 160.0, 180.0)]
        [InlineData(new[] { 100.0, 140, 180, 220, 260 }, 180.0, 200.0, 180.0)]
        [InlineData(new[] { 100.0, 140, 180, 220, 260 }, 180.0, 240.0, 220.0)]
        public void ATieOfAListOfThreeToFiveValues_EvenOrUneven_FollowsTheSameRule(double[] list, double own, double asked, double taken)
        {
            DxfDatabase db = Panel(own, DxfBlockParameterValueSet.List(list), out _);

            Assert.Equal(taken, After(db, "Width", list[list.Length - 1], asked), Close);
        }

        [Fact]
        public void TheValueBeforeATie_ChangesNothing_AsAutoCadWorkedEverySetOutFromTheBlocksOwnValue()
        {
            foreach (double before in Six)
            {
                Assert.Equal(200.0, After(Panel(200, DxfBlockParameterValueSet.List(Six), out _), "Width", before, 175), Close);
                Assert.Equal(100.0, After(Panel(200, DxfBlockParameterValueSet.List(Six), out _), "Width", before, 125), Close);
            }
        }

        [Fact]
        public void AListWrittenFromTheGreatestDown_TakesATieToTheOneWrittenFirst_ButTheBlocksOwn()
        {
            DxfDatabase db = Panel(200, DxfBlockParameterValueSet.List(Six), out DxfBlockTableRecord block);
            DxfItem linear = Nodes(block)[0];
            DxfFile file = linear.File;
            var groups = file.TailOf(linear, 0).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            List<int> listed = Enumerable.Range(0, groups.Count).Where(i => groups[i].Key == 144).ToList();
            List<string> values = listed.Select(i => groups[i].Value).Reverse().ToList();

            for (int i = 0; i < listed.Count; i++)
            {
                groups[listed[i]] = new KeyValuePair<int, string>(144, values[i]);
            }

            file.ReplaceTail(linear, 0, groups);
            DxfDatabase written = Reopened(db);

            // AutoCAD 2021 took the ties of the list 300, 250 ... 50 to the greater, the block's own 200 where it was one of the two.
            Assert.Equal(100.0, After(written, "Width", 75), Close);
            Assert.Equal(150.0, After(written, "Width", 125), Close);
            Assert.Equal(200.0, After(written, "Width", 175), Close);
            Assert.Equal(200.0, After(written, "Width", 225), Close);
            Assert.Equal(300.0, After(written, "Width", 275), Close);
        }

        [Theory]
        [InlineData(1.0, 150.0, 200.0)]
        [InlineData(1.0, 250.0, 200.0)]
        [InlineData(1.0, 350.0, 300.0)]
        [InlineData(2.5, 450.0, 500.0)]
        [InlineData(2.5, 150.0, 100.0)]
        [InlineData(2.5, 550.0, 500.0)]
        public void AnInsertScaledBy2_TakesItsTiesToTheBlocksOwnValueScaled(double ownOverHundred, double asked, double taken)
        {
            DxfDatabase db = Panel(100 * ownOverHundred, DxfBlockParameterValueSet.List(Six), out DxfBlockTableRecord block);
            TheInsert(db).Erase();
            Place(db, block, Deg(30), 2);

            Assert.Equal(taken, After(db, "Width", asked), Close);
        }

        [Theory]
        [InlineData("Depth", 65.0, 50.0)]
        [InlineData("Depth", 100.0, 80.0)]
        [InlineData("Depth", 135.0, 150.0)]
        [InlineData("Width", 75.0, 50.0)]
        [InlineData("Width", 125.0, 100.0)]
        [InlineData("Width", 225.0, 250.0)]
        [InlineData("Width", 275.0, 250.0)]
        public void AnXYListTie_FollowsTheRuleOfALinearOne(string name, double asked, double taken)
        {
            DxfDatabase db = Drawing("GH_XY", out DxfBlockTableRecord block);
            DxfPolyline outline = Add(block, Poly(0, 0, 250, 0, 250, 150, 0, 150));
            outline.Closed = true;
            DxfBlockXYParameter xy = block.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(250, 150, 0),
                DxfBlockParameterValueSet.List(Six), DxfBlockParameterValueSet.List(new[] { 50.0, 80, 120, 150 }));
            block.AddStretchAction(xy, new GeoPoint2(245, 145), new GeoPoint2(255, 155), new[] { outline });
            Place(db, block);

            Assert.Equal(taken, After(db, name, name == "Width" ? 300 : 50, asked), Close);
        }

        [Theory]
        [InlineData(150.0, 75.0, 50.0)]
        [InlineData(150.0, 125.0, 150.0)]
        [InlineData(150.0, 175.0, 150.0)]
        [InlineData(200.0, 75.0, 50.0)]
        [InlineData(200.0, 125.0, 100.0)]
        [InlineData(200.0, 175.0, 200.0)]
        public void APolarListTie_FollowsTheRuleOfALinearOne(double own, double asked, double taken)
        {
            DxfDatabase db = Drawing("GH_POINTER", out DxfBlockTableRecord block);
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(own, 0, 0)));
            DxfBlockPolarParameter polar = block.AddPolarParameter("Length", null, "Angle", null, new GeoPoint3(0, 0, 0), new GeoPoint3(own, 0, 0), -20,
                DxfBlockParameterValueSet.List(new[] { 50.0, 100, 150, 200 }), null);
            block.AddPolarStretchAction(polar, new GeoPoint2(own - 5, -5), new GeoPoint2(own + 5, 5), new[] { line }, new DxfEntity[0]);
            Place(db, block);

            Assert.Equal(taken, After(db, "Length", 50, asked), Close);
            Assert.True(Is(Shown(TheInsert(db))[0], 0, 0, taken, 0));
        }

        [Theory]
        [InlineData(95.0, 90.0)]
        [InlineData(105.0, 100.0)]
        [InlineData(85.0, 80.0)]
        [InlineData(195.0, 190.0)]
        public void AnIncrementTie_GoesToTheStepBelow_EvenWhereTheBlocksOwnIsTheStepAbove(double asked, double taken)
        {
            Assert.Equal(taken, After(Panel(100, DxfBlockParameterValueSet.Increments(50, 300, 10), out _), "Width", 300, asked), Close);
        }

        // ---- 2. a stretch and a move tied to a polar parameter ---------------------------------------------------------------

        /// <summary>
        /// The pointer of plugin D25b: a shaft from (0, 0) to (100, 0) its polar stretch holds by the tip; a circle at the tip and a
        /// text at (95, 3) moved by its end point, a circle at (50, 0) moved half as far, a circle at (100, 20) moved turned a quarter
        /// turn, a right edge (100, 30)-(100, 50) and a top (0, 50)-(100, 50) stretched by the end point, and a polyline (60, -10),
        /// (100, -10), (100, -20) whose last vertex a stretch of the multiplier 0.5 holds.
        /// </summary>
        private static DxfDatabase Moved(out DxfBlockTableRecord block, out DxfBlockPolarParameter polar)
        {
            DxfDatabase db = Drawing("GH_PMOVE", out block);
            DxfLine shaft = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfCircle tip = Add(block, new DxfCircle(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 5));
            DxfDBText label = Add(block, new DxfDBText { Position = new GeoPoint3(95, 3, 0), Height = 2.5, TextString = "A" });
            DxfCircle half = Add(block, new DxfCircle(new GeoPoint3(50, 0, 0), GeoVector3.ZAxis, 3));
            DxfCircle turned = Add(block, new DxfCircle(new GeoPoint3(100, 20, 0), GeoVector3.ZAxis, 2));
            DxfLine right = Add(block, new DxfLine(new GeoPoint3(100, 30, 0), new GeoPoint3(100, 50, 0)));
            DxfLine top = Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)));
            DxfPolyline under = Add(block, Poly(60, -10, 100, -10, 100, -20));
            polar = block.AddPolarParameter("Length", null, "Angle", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null, null);
            block.AddPolarStretchAction(polar, new GeoPoint2(90, -5), new GeoPoint2(110, 5), new DxfEntity[] { shaft }, new DxfEntity[0]);
            block.AddMoveAction(polar, new DxfEntity[] { tip, label });
            block.AddMoveAction(polar, new DxfEntity[] { half }, 0.5, 0);
            block.AddMoveAction(polar, new DxfEntity[] { turned }, 1, Math.PI / 2);
            block.AddStretchAction(polar, new GeoPoint2(90, 25), new GeoPoint2(110, 60), new DxfEntity[] { right, top });
            block.AddStretchAction(polar, new GeoPoint2(90, -25), new GeoPoint2(110, -15), new DxfEntity[] { under }, 0.5, 0);
            Place(db, block);
            return db;
        }

        [Fact]
        public void MovesAndStretchesOfAPolarParameter_FollowItsEndPointUnturned_AsAutoCad2021DrewThem()
        {
            DxfDatabase db = Moved(out _, out _);

            Property(TheInsert(db), "Length").Value = 150.0;
            List<DxfEntity> shown = Shown(TheInsert(Reopened(db)));
            Assert.True(Is(shown[0], 0, 0, 150, 0));
            Assert.True(At(((DxfCircle)shown[1]).Center, 150, 0));
            Assert.True(At(((DxfDBText)shown[2]).Position, 145, 3));
            Assert.True(At(((DxfCircle)shown[3]).Center, 75, 0));
            Assert.True(At(((DxfCircle)shown[4]).Center, 100, 70));
            Assert.True(Is(shown[5], 150, 30, 150, 50));
            Assert.True(Is(shown[6], 0, 50, 150, 50));
            Assert.True(At(((DxfPolyline)shown[7]).GetPoint3dAt(2), 125, -20) && At(((DxfPolyline)shown[7]).GetPoint3dAt(1), 100, -10));

            Property(TheInsert(db), "Angle").Value = Math.PI / 2;
            shown = Shown(TheInsert(Reopened(db)));
            Assert.True(Is(shown[0], 0, 0, 0, 150));
            Assert.True(At(((DxfCircle)shown[1]).Center, 0, 150));
            Assert.True(At(((DxfDBText)shown[2]).Position, -5, 153));
            Assert.Equal(0.0, ((DxfDBText)shown[2]).Rotation, Close);
            Assert.True(At(((DxfCircle)shown[3]).Center, 0, 75));
            Assert.True(At(((DxfCircle)shown[4]).Center, -50, -80));
            Assert.True(Is(shown[5], 0, 180, 0, 200));
            Assert.True(Is(shown[6], 0, 50, 0, 200));
            Assert.True(At(((DxfPolyline)shown[7]).GetPoint3dAt(2), 50, 55));
        }

        [Fact]
        public void APolarParameterTurnedAndThenShortened_IsWorkedOutFromTheBlocksOwnValues()
        {
            DxfDatabase db = Moved(out _, out _);
            DxfBlockReference insert = TheInsert(db);

            Property(insert, "Angle").Value = 3 * Math.PI / 4;
            Property(insert, "Length").Value = 80.0;
            List<DxfEntity> shown = Shown(TheInsert(Reopened(db)));
            double r = 80 / Math.Sqrt(2);
            Assert.True(At(((DxfCircle)shown[1]).Center, -r, r));
            Assert.True(At(((DxfCircle)shown[3]).Center, -r / 2, r / 2));
            Assert.True(At(((DxfCircle)shown[4]).Center, 43.431457505076168, -136.5685424949238));
            Assert.True(Is(shown[6], 0, 50, -r, 50 + r));
            Assert.True(At(((DxfPolyline)shown[7]).GetPoint3dAt(2), 21.715728752538091, 8.2842712474619091));

            Property(insert, "Angle").Value = Deg(200);
            shown = Shown(TheInsert(Reopened(db)));
            Assert.True(At(((DxfCircle)shown[1]).Center, -75.175409662872696, -27.361611466053489));
            Assert.True(At(((DxfCircle)shown[4]).Center, 127.36161146605347, -155.17540966287268));
        }

        [Fact]
        public void AMoveAndAStretchOfAPolarParameter_AreWrittenAsThoseOfALinearOne_ByItsEndPoint()
        {
            Moved(out DxfBlockTableRecord block, out DxfBlockPolarParameter polar);

            List<DxfItem> nodes = Nodes(block);
            string id = Of(nodes[0], "AcDbEvalExpr", 90).Single();
            DxfItem move = nodes.First(n => n.Type == "BLOCKMOVEACTION");
            DxfItem stretch = nodes.First(n => n.Type == "BLOCKSTRETCHACTION");

            Assert.Equal(polar.Handle, nodes[0].Handle);
            Assert.Equal(new[] { id }, Of(move, "AcDbBlockMoveAction", 92));
            Assert.Equal(new[] { "EndXDelta" }, Of(move, "AcDbBlockMoveAction", 301));
            Assert.Equal(new[] { "EndYDelta" }, Of(move, "AcDbBlockMoveAction", 302));
            Assert.Equal(new[] { id }, Of(stretch, "AcDbBlockStretchAction", 93));
            Assert.Equal(new[] { "EndXDelta" }, Of(stretch, "AcDbBlockStretchAction", 301));
            Assert.Equal(new[] { "Move", "Move1", "Move2" }, nodes.Where(n => n.Type == "BLOCKMOVEACTION").Select(n => Of(n, "AcDbBlockElement", 300).Single()));
        }

        [Fact]
        public void AStretchAloneOfAPolarParameterOfValueSets_TakesItsValuesToThem_AsAutoCad2021Did()
        {
            DxfDatabase db = Drawing("GH_PSTRETCH", out DxfBlockTableRecord block);
            DxfLine right = Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)));
            DxfLine bottom = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfLine top = Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)));
            Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)));
            DxfBlockPolarParameter polar = block.AddPolarParameter("Reach", "How far", "Turn", "Which way", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 20,
                DxfBlockParameterValueSet.Increments(50, 300, 10), DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12));
            block.AddStretchAction(polar, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new DxfEntity[] { right, bottom, top });
            DxfBlockReference insert = Place(db, block);

            Property(insert, "Reach").Value = 150.0;
            Property(insert, "Turn").Value = Math.PI / 2;
            List<DxfEntity> shown = Shown(TheInsert(Reopened(db)));
            Assert.True(Is(shown[0], 0, 150, 0, 200));
            Assert.True(Is(shown[1], 0, 0, 0, 150));
            Assert.True(Is(shown[2], 0, 50, 0, 200));
            Assert.True(Is(shown[3], 0, 0, 0, 50));

            Property(insert, "Turn").Value = Deg(40);
            Property(insert, "Reach").Value = 77.0;
            Assert.Equal(80.0, (double)ValueOf(insert, "Reach"), Close);
            Assert.Equal(Math.PI / 4, (double)ValueOf(insert, "Turn"), Close);
            shown = Shown(TheInsert(Reopened(db)));
            Assert.True(Is(shown[0], 56.568542494923797, 56.568542494923797, 56.568542494923797, 106.5685424949238));
        }

        [Fact]
        public void AMoveOfAPolarParameter_IsRefusedOnWhatItsPolarStretchWorksOn_AndNothingIsWritten()
        {
            DxfDatabase db = Drawing("GH_ONE", out DxfBlockTableRecord block);
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfCircle tip = Add(block, new DxfCircle(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 5));
            DxfBlockPolarParameter polar = block.AddPolarParameter("Length", null, "Angle", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 0, null, null);
            block.AddPolarStretchAction(polar, new GeoPoint2(90, -10), new GeoPoint2(110, 10), new[] { line }, new DxfEntity[0]);
            string before = db.ToText();

            Assert.Throws<NotSupportedException>(() => block.AddMoveAction(polar, new[] { line }));
            Assert.Throws<NotSupportedException>(() => block.AddStretchAction(polar, new GeoPoint2(90, -10), new GeoPoint2(110, 10), new[] { line }));
            Assert.Throws<ArgumentNullException>(() => block.AddMoveAction((DxfBlockPolarParameter)null, new[] { tip }));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddMoveAction(polar, new[] { tip }, double.NaN, 0));
            Assert.Equal(before, db.ToText());

            block.AddMoveAction(polar, new[] { tip });
            Property(Place(db, block), "Length").Value = 120.0;
            Assert.True(At(((DxfCircle)Shown(db.ModelSpace.OfType<DxfBlockReference>().Last())[1]).Center, 120, 0));
        }

        // ---- 3. lookup columns of an XY, a polar and a rotation parameter -------------------------------------------------------

        private static DxfDatabase Looked(out DxfBlockTableRecord xyBlock, out DxfBlockTableRecord polarBlock, out DxfBlockTableRecord rotationBlock)
        {
            DxfDatabase db = Drawing("GH_LXY", out xyBlock);
            DxfPolyline outline = Add(xyBlock, Poly(0, 0, 100, 0, 100, 50, 0, 50));
            outline.Closed = true;
            DxfBlockXYParameter xy = xyBlock.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0));
            xyBlock.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { outline });
            xyBlock.AddLookupParameter("Size", null, new GeoPoint3(50, -20, 0), new[] { DxfBlockLookupInput.X(xy), DxfBlockLookupInput.Y(xy) },
                new[] { new DxfBlockLookupRow("Small", 100, 50), new DxfBlockLookupRow("Large", 200, 80), new DxfBlockLookupRow("Deep", 100, 120) });

            polarBlock = new DxfBlockTableRecord { Name = "GH_LPOLAR", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(polarBlock);
            DxfLine line = Add(polarBlock, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfBlockPolarParameter polar = polarBlock.AddPolarParameter("Length", null, "Angle", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null, null);
            polarBlock.AddPolarStretchAction(polar, new GeoPoint2(90, -5), new GeoPoint2(110, 5), new DxfEntity[] { line }, new DxfEntity[0]);
            polarBlock.AddLookupParameter("Pick", null, new GeoPoint3(50, -30, 0), new[] { DxfBlockLookupInput.Distance(polar), DxfBlockLookupInput.Angle(polar) },
                new[] { new DxfBlockLookupRow("A", 100, 0), new DxfBlockLookupRow("B", 150, Math.PI / 2), new DxfBlockLookupRow("C", 80, Math.PI / 4) });

            rotationBlock = new DxfBlockTableRecord { Name = "GH_LROT", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(rotationBlock);
            DxfLine needle = Add(rotationBlock, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0)));
            DxfBlockRotationParameter angle = rotationBlock.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 30, 0);
            rotationBlock.AddRotateAction(angle, new[] { needle });
            rotationBlock.AddLookupParameter("Way", null, new GeoPoint3(0, -20, 0), new[] { DxfBlockLookupInput.Angle(angle) },
                new[] { new DxfBlockLookupRow("East", 0), new DxfBlockLookupRow("North", Math.PI / 2), new DxfBlockLookupRow("West", Math.PI) });
            return db;
        }

        [Fact]
        public void ALookupOfAnXYParametersDistances_SetsAndReadsThemAsAutoCad2021Did()
        {
            DxfDatabase db = Looked(out DxfBlockTableRecord block, out _, out _);
            DxfBlockReference insert = Place(db, block);
            DxfBlockReference scaled = Place(db, block, Deg(30), 2);

            Assert.Equal("Small", ValueOf(insert, "Size"));
            Assert.Equal(new object[] { "Small", "Large", "Deep" }, Property(insert, "Size").GetAllowedValues());
            Property(insert, "Size").Value = "Large";
            Assert.Equal(200.0, (double)ValueOf(insert, "Width"), Close);
            Assert.Equal(80.0, (double)ValueOf(insert, "Depth"), Close);
            Assert.True(At(((DxfPolyline)Shown(insert)[0]).GetPoint3dAt(2), 200, 80));
            Property(insert, "Width").Value = 150.0;
            Assert.Equal("Custom", ValueOf(insert, "Size"));
            Property(insert, "Width").Value = 100.0;
            Property(insert, "Depth").Value = 120.0;
            Assert.Equal("Deep", ValueOf(TheInsert(Reopened(db)), "Size"));
            Property(insert, "Size").Value = "Small";
            Assert.Equal(50.0, (double)ValueOf(insert, "Depth"), Close);

            Property(scaled, "Size").Value = "Large";
            Assert.Equal(400.0, (double)ValueOf(scaled, "Width"), Close);
            Assert.Equal(160.0, (double)ValueOf(scaled, "Depth"), Close);
        }

        [Fact]
        public void ALookupOfAPolarParametersDistanceAndAngle_SetsAndReadsThemAsAutoCad2021Did()
        {
            DxfDatabase db = Looked(out _, out DxfBlockTableRecord block, out _);
            DxfBlockReference insert = Place(db, block);
            DxfBlockReference other = Place(db, block);

            Assert.Equal("A", ValueOf(insert, "Pick"));
            Property(insert, "Pick").Value = "B";
            Assert.Equal(150.0, (double)ValueOf(insert, "Length"), Close);
            Assert.Equal(Math.PI / 2, (double)ValueOf(insert, "Angle"), Close);
            Assert.True(Is(Shown(insert)[0], 0, 0, 0, 150));
            Property(insert, "Length").Value = 80.0;
            Assert.Equal("Custom", ValueOf(insert, "Pick"));
            Property(insert, "Angle").Value = Math.PI / 4;
            Assert.Equal("C", ValueOf(insert, "Pick"));
            Property(insert, "Pick").Value = "A";
            Assert.Equal(100.0, (double)ValueOf(insert, "Length"), Close);
            Assert.Equal(0.0, (double)ValueOf(insert, "Angle"), Close);
            Property(insert, "Angle").Value = Math.PI / 2;
            Assert.Equal("Custom", ValueOf(TheInsert(Reopened(db)), "Pick"));

            Property(other, "Length").Value = 150.0;
            Assert.Equal("Custom", ValueOf(other, "Pick"));
            Property(other, "Angle").Value = Math.PI / 2;
            Assert.Equal("B", ValueOf(other, "Pick"));
        }

        [Fact]
        public void ALookupOfARotationParametersAngle_SetsAndReadsItAsAutoCad2021Did()
        {
            DxfDatabase db = Looked(out _, out _, out DxfBlockTableRecord block);
            DxfBlockReference insert = Place(db, block);

            Assert.Equal("East", ValueOf(insert, "Way"));
            Property(insert, "Way").Value = "North";
            Assert.Equal(Math.PI / 2, (double)ValueOf(insert, "Angle"), Close);
            Assert.True(Is(Shown(insert)[0], 0, 0, 0, 30));
            Property(insert, "Angle").Value = Math.PI;
            Assert.Equal("West", ValueOf(insert, "Way"));
            Property(insert, "Angle").Value = 1.0;
            Assert.Equal("Custom", ValueOf(TheInsert(Reopened(db)), "Way"));
            Assert.Equal(new object[] { "East", "North", "West", "Custom" }, Property(insert, "Way").GetAllowedValues());
            Property(insert, "Way").Value = "East";
            Assert.Equal(0.0, (double)ValueOf(insert, "Angle"), Close);
            Assert.True(Is(Shown(insert)[0], 0, 0, 30, 0));
        }

        [Fact]
        public void TheColumnsOfAnXYAPolarAndARotationParameter_AreWrittenOfTheirConnectionsAndUnits()
        {
            Looked(out DxfBlockTableRecord xyBlock, out DxfBlockTableRecord polarBlock, out DxfBlockTableRecord rotationBlock);

            DxfItem xy = Nodes(xyBlock).Last(n => n.Type == "BLOCKLOOKUPACTION");
            DxfItem polar = Nodes(polarBlock).Last(n => n.Type == "BLOCKLOOKUPACTION");
            DxfItem rotation = Nodes(rotationBlock).Last(n => n.Type == "BLOCKLOOKUPACTION");

            Assert.Equal(new[] { "UpdatedDistanceX", "UpdatedDistanceY", "lookupString" }, Of(xy, "AcDbBlockLookupAction", 304));
            Assert.Equal(new[] { "2", "2", "0" }, Of(xy, "AcDbBlockLookupAction", 96));
            Assert.Equal(new[] { "UpdatedDistance", "UpdatedAngle", "lookupString" }, Of(polar, "AcDbBlockLookupAction", 304));
            Assert.Equal(new[] { "2", "1", "0" }, Of(polar, "AcDbBlockLookupAction", 96));
            Assert.Equal(new[] { "UpdatedAngle", "lookupString" }, Of(rotation, "AcDbBlockLookupAction", 304));
            Assert.Equal(new[] { "1", "0" }, Of(rotation, "AcDbBlockLookupAction", 96));
            Assert.Contains(Of(rotation, "AcDbBlockLookupAction", 302), v => v.StartsWith("3.14159", StringComparison.Ordinal)
                && Math.Abs(double.Parse(v, CultureInfo.InvariantCulture) - Math.PI) <= Close);
        }

        [Fact]
        public void ARowOutsideAnInputsValueSet_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Drawing("GH_ONE", out DxfBlockTableRecord block);
            DxfLine needle = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0)));
            DxfBlockRotationParameter angle = block.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 30, 0, DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12));
            block.AddRotateAction(angle, new[] { needle });
            DxfPolyline outline = Add(block, Poly(0, 0, 100, 0, 100, 50, 0, 50));
            DxfBlockXYParameter xy = block.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0));
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { outline });
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Way", null, new GeoPoint3(0, 0, 0), new[] { DxfBlockLookupInput.Angle(angle) },
                new[] { new DxfBlockLookupRow("Odd", Deg(10)) }));
            Assert.Throws<ArgumentException>(() => block.AddLookupParameter("Size", null, new GeoPoint3(0, 0, 0), new[] { DxfBlockLookupInput.X(xy) },
                new[] { new DxfBlockLookupRow("Flat", 0) }));
            Assert.Throws<ArgumentNullException>(() => DxfBlockLookupInput.Angle((DxfBlockRotationParameter)null));
            Assert.Equal(before, db.ToText());

            // A turn of 2 pi less a step, or of a whole turn, is of the set; a whole turn sets nought, which no row of 2 pi
            // matches, as AutoCAD 2021 read Custom (plugin D25b).
            block.AddLookupParameter("Way", null, new GeoPoint3(0, 0, 0), new[] { DxfBlockLookupInput.Angle(angle) },
                new[] { new DxfBlockLookupRow("Back", Deg(345)), new DxfBlockLookupRow("Round", 2 * Math.PI) });
            DxfBlockReference insert = Place(db, block);
            Assert.Equal("Custom", ValueOf(insert, "Way"));
            Property(insert, "Way").Value = "Round";
            Assert.Equal(0.0, (double)ValueOf(insert, "Angle"), Close);
            Assert.Equal("Custom", ValueOf(insert, "Way"));
            Property(insert, "Way").Value = "Back";
            Assert.Equal(Deg(345), (double)ValueOf(insert, "Angle"), Close);
            Assert.Equal("Back", ValueOf(TheInsert(Reopened(db)), "Way"));
        }

        [Fact]
        public void ALookupSet_ReadsTheFirstRowOfItsValues_AsAutoCad2021ReadIt()
        {
            DxfDatabase db = Drawing("GH_DUP", out DxfBlockTableRecord block);
            DxfPolyline outline = Add(block, Poly(0, 0, 100, 0, 100, 50, 0, 50));
            outline.Closed = true;
            DxfBlockXYParameter xy = block.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0));
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { outline });
            block.AddLookupParameter("Size", null, new GeoPoint3(50, -20, 0), new[] { DxfBlockLookupInput.X(xy), DxfBlockLookupInput.Y(xy) },
                new[] { new DxfBlockLookupRow("Small", 100, 50), new DxfBlockLookupRow("Twin", 100, 50), new DxfBlockLookupRow("Large", 200, 80), new DxfBlockLookupRow("Big", 200, 80) });
            DxfBlockTableRecord turning = new DxfBlockTableRecord { Name = "GH_DOWN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(turning);
            DxfLine needle = Add(turning, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0)));
            DxfBlockRotationParameter angle = turning.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 30, 0);
            turning.AddRotateAction(angle, new[] { needle });
            turning.AddLookupParameter("Way", null, new GeoPoint3(0, -20, 0), new[] { DxfBlockLookupInput.Angle(angle) },
                new[] { new DxfBlockLookupRow("East", 0), new DxfBlockLookupRow("Zero", 0), new DxfBlockLookupRow("Down", -Math.PI / 2), new DxfBlockLookupRow("South", 1.5 * Math.PI) });
            DxfBlockReference plate = Place(db, block);
            DxfBlockReference dial = Place(db, turning);

            Property(plate, "Size").Value = "Twin";
            Assert.Equal("Small", ValueOf(plate, "Size"));
            Property(plate, "Size").Value = "Big";
            Assert.Equal("Large", ValueOf(TheInsert(Reopened(db)), "Size"));
            Assert.True(At(((DxfPolyline)Shown(plate)[0]).GetPoint3dAt(2), 200, 80));

            Property(dial, "Way").Value = "Zero";
            Assert.Equal("East", ValueOf(dial, "Way"));
            Property(dial, "Way").Value = "Down";
            Assert.Equal(1.5 * Math.PI, (double)ValueOf(dial, "Angle"), Close);
            Assert.Equal("South", ValueOf(dial, "Way"));
        }
    }
}
