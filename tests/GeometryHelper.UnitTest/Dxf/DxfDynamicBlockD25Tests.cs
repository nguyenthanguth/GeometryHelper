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
    /// The dynamic blocks of D25: a polar parameter with its polar stretch, and the value sets of a rotation, an XY and a
    /// polar parameter. Each is written as AutoCAD saved its own samples (ACadSharp's polar sample and AutoCAD's Annotation
    /// sample), and set as AutoCAD 2021 set the library's files in plugin D25: the geometry below is AutoCAD's, to 1E-9.
    /// </summary>
    public class DxfDynamicBlockD25Tests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        private static double Deg(double degrees) => degrees * Math.PI / 180.0;

        /// <summary>A new drawing of R2010 with an empty block of the name.</summary>
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

        private static DxfBlockReference Place(DxfDatabase db, DxfBlockTableRecord block)
        {
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
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

        /// <summary>
        /// The pointer of plugin D25: a line from (0, 0) to (100, 0), a circle of the radius 5 at its tip, a text at (95, 3), a
        /// polyline from (50, -5) to (100, -5), a tick from (20, 0) to (20, 10) to turn only, a line (0, 30)-(30, 30) of no action
        /// and an arc of the radius 3 about the tip from 0 to 180 degrees; Length and Angle from (0, 0, 0) to (100, 0, 0), and a
        /// polar stretch of the frame (90, -10)-(110, 10) over the tip.
        /// </summary>
        private static DxfDatabase Pointer(DxfBlockParameterValueSet distances, DxfBlockParameterValueSet angles, out DxfBlockTableRecord block, out DxfBlockPolarParameter polar, out DxfEntity tick)
        {
            DxfDatabase db = Drawing("GH_POINTER", out block);
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfCircle tip = Add(block, new DxfCircle(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 5));
            DxfDBText text = Add(block, new DxfDBText { Position = new GeoPoint3(95, 3, 0), Height = 2.5, TextString = "A" });
            DxfPolyline under = Add(block, Poly(50, -5, 100, -5));
            tick = Add(block, new DxfLine(new GeoPoint3(20, 0, 0), new GeoPoint3(20, 10, 0)));
            Add(block, new DxfLine(new GeoPoint3(0, 30, 0), new GeoPoint3(30, 30, 0)));
            DxfArc arc = Add(block, new DxfArc(new GeoPoint3(100, 0, 0), 3, 0, Math.PI));
            polar = block.AddPolarParameter("Length", "Length of the pointer", "Angle", "Angle of the pointer", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, distances, angles);
            block.AddPolarStretchAction(polar, new GeoPoint2(90, -10), new GeoPoint2(110, 10), new DxfEntity[] { line, tip, text, under, arc }, new[] { tick });
            Place(db, block);
            return db;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static double ValueOf(DxfBlockReference insert, string name) => Assert.IsType<double>(Property(insert, name).Value);

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

        /// <summary>Each edge of the graph of a block, after its nodes: its kind (94) and the indices of the nodes it runs from and to.</summary>
        private static List<int[]> Edges(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            List<DxfTypedValue> groups = graph.Item.File.TailOf(graph.Item, 0);
            List<int> numbers = groups.Skip(groups.FindIndex(g => g.TypeCode == 100 && g.Value == "AcDbEvalGraph") + 1).Where(g => g.TypeCode != 360)
                .Select(g => int.Parse(g.Value.Trim(), CultureInfo.InvariantCulture)).ToList();
            var edges = new List<int[]>();

            for (int at = 2 + Nodes(block).Count * 7; at + 9 < numbers.Count; at += 10)
            {
                edges.Add(new[] { numbers[at + 2], numbers[at + 3], numbers[at + 4] });
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

        /// <summary>Puts a handle in the selection of an action (a 330 after the count 71 of AcDbBlockAction), as another writer may list it.</summary>
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

        /// <summary>The XRECORDs AutoCAD reads an INSERT's values from, by their node ids, as code and value.</summary>
        private static Dictionary<string, List<string>> Cache(DxfBlockReference insert)
        {
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var data = (DxfDBDictionary)((DxfDBDictionary)representation.GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            return data.ToDictionary(e => e.Key, e => ((DxfXrecord)e.Value).Data.Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim()).ToList());
        }

        // ---- 1. the polar parameter and its polar stretch, as written ---------------------------------------------------------

        [Fact]
        public void APolarParameter_GivesEveryInsertItsDistanceAndThenItsAngle()
        {
            DxfDatabase db = Pointer(null, null, out DxfBlockTableRecord block, out DxfBlockPolarParameter polar, out _);

            List<DxfDynamicBlockReferenceProperty> properties = TheInsert(Reopened(db)).DynamicBlockReferencePropertyCollection.ToList();

            Assert.Equal(new[] { "Length", "Angle" }, properties.Select(p => p.PropertyName));
            Assert.Equal(100.0, (double)properties[0].Value, Close);
            Assert.Equal(0.0, (double)properties[1].Value, Close);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Distance, properties[0].UnitsType);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Angular, properties[1].UnitsType);
            Assert.Equal("Angle of the pointer", properties[1].Description);
            Assert.Empty(properties[0].GetAllowedValues());
            Assert.Equal(100.0, polar.Distance, Close);
            Assert.Equal(0.0, polar.Angle, Close);
            Assert.Equal(polar.Handle, Nodes(block)[0].Handle);
        }

        [Fact]
        public void APolarParameterAndItsStretch_AreWrittenCodeForCodeAsAutoCadSavedTheSample()
        {
            DxfDatabase db = Drawing("GH_ONE", out DxfBlockTableRecord block);
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfBlockPolarParameter polar = block.AddPolarParameter("Distance1", null, "Angle1", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1, null, null);
            block.AddPolarStretchAction(polar, new GeoPoint2(90, -10), new GeoPoint2(110, 10), new[] { line }, new DxfEntity[0]);

            // ACadSharp's polar sample as AutoCAD saved it: the parameter, its end grip, two components, its base grip, two
            // components and the polar stretch, of the edges 1, 1, 2, 1, 1, 2 and 6 between the same nodes.
            List<DxfItem> nodes = Nodes(block);
            Assert.Equal(new[] { "BLOCKPOLARPARAMETER", "BLOCKPOLARGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKPOLARGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKPOLARSTRETCHACTION" }, nodes.Select(n => n.Type));
            Assert.Equal(new[]
            {
                100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 280, 281, 100, 1010, 1020, 1030, 1011, 1021, 1031, 170, 91, 91, 91, 91, 171, 92, 301, 172, 93, 302, 173, 94, 303,
                174, 95, 304, 177, 100, 305, 306, 307, 308, 140, 309, 96, 141, 142, 143, 175, 410, 97, 145, 146, 147, 176,
            }, Codes(nodes[0]));
            Assert.Equal(new[] { 100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 91, 92, 1010, 1020, 1030, 280, 93, 100 }, Codes(nodes[1]));
            Assert.Equal(new[]
            {
                100, 90, 98, 99, 100, 300, 98, 99, 1071, 100, 70, 71, 330, 1010, 1020, 1030, 100, 92, 301, 93, 302, 94, 303, 95, 304, 96, 305, 97, 306, 73, 1011, 1021, 1011,
                1021, 72, 74, 332, 75, 76, 78, 141, 140, 77,
            }, Codes(nodes[7]));
            Assert.Equal(new[] { new[] { 1, 0, 2 }, new[] { 1, 0, 3 }, new[] { 2, 1, 0 }, new[] { 1, 0, 5 }, new[] { 1, 0, 6 }, new[] { 2, 4, 0 }, new[] { 6, 0, 7 } }, Edges(block));
            Assert.Equal(new[] { "Polar", "End Grip", "Base Grip", "Polar Stretch" }, new[] { 0, 1, 4, 7 }.Select(i => Of(nodes[i], "AcDbBlockElement", 300).Single()));
            Assert.Equal(new[] { "EndXDelta", "EndYDelta", "Base", "End", "UpdatedBase", "UpdatedEnd" }, new[] { 301, 302, 303, 304, 305, 306 }.Select(c => Of(nodes[7], "AcDbBlockPolarStretchAction", c).Single()));
            Assert.Equal(new[] { "1" }, Of(nodes[7], "AcDbBlockPolarStretchAction", 76));
            Assert.Equal(new[] { "1", "3" }, new[] { 96, 97 }.Select(c => Of(nodes[0], "AcDbBlockPolarParameter", c).Single()));
            string text = db.ToText().Replace("\r\n", "\n");
            Assert.Contains("  0\nCLASS\n  1\nBLOCKPOLARSTRETCHACTION\n  2\nAcDbBlockPolarStretchAction\n", text, StringComparison.Ordinal);
            Assert.Contains("  0\nCLASS\n  1\nBLOCKPOLARGRIP\n  2\nAcDbBlockPolarGrip\n", text, StringComparison.Ordinal);
            Assert.Contains("  0\nCLASS\n  1\nBLOCKPOLARPARAMETER\n  2\nAcDbBlockPolarParameter\n", text, StringComparison.Ordinal);
        }

        [Fact]
        public void AnEntityToTurnOnly_IsNamedAmongThe331sAndNotInTheSelection()
        {
            Pointer(null, null, out DxfBlockTableRecord block, out _, out DxfEntity tick);

            DxfItem stretch = Nodes(block).Last();

            Assert.DoesNotContain(tick.Handle, Of(stretch, "AcDbBlockAction", 330));
            Assert.Equal(new[] { tick.Handle }, Of(stretch, "AcDbBlockPolarStretchAction", 331));
            Assert.Equal(new[] { "1" }, Of(stretch, "AcDbBlockPolarStretchAction", 72));
            Assert.Equal(new[] { "5" }, Of(stretch, "AcDbBlockAction", 71));
        }

        // ---- 2. the polar stretch, as AutoCAD 2021 set it ---------------------------------------------------------------------

        [Fact]
        public void APointerSetToLengthsAndAngles_TurnsAndStretchesAsAutoCad2021Drew()
        {
            DxfDatabase db = Pointer(null, null, out _, out _, out _);
            DxfBlockReference insert = TheInsert(db);

            Property(insert, "Length").Value = 150.0;
            List<DxfEntity> shown = Shown(insert);
            Assert.True(Is(shown[0], 0, 0, 150, 0));
            Assert.True(At(((DxfCircle)shown[1]).Center, 150, 0));
            Assert.True(At(((DxfDBText)shown[2]).Position, 145, 3));
            Assert.True(At(((DxfPolyline)shown[3]).GetPoint3dAt(0), 50, -5) && At(((DxfPolyline)shown[3]).GetPoint3dAt(1), 150, -5));
            Assert.True(Is(shown[4], 20, 0, 20, 10));

            Property(insert, "Angle").Value = Math.PI / 2;
            shown = Shown(insert);
            Assert.True(Is(shown[0], 0, 0, 0, 150));
            Assert.True(At(((DxfCircle)shown[1]).Center, 0, 150));
            Assert.True(At(((DxfDBText)shown[2]).Position, -3, 145));
            Assert.Equal(Math.PI / 2, ((DxfDBText)shown[2]).Rotation, Close);
            Assert.True(At(((DxfPolyline)shown[3]).GetPoint3dAt(0), 5, 50) && At(((DxfPolyline)shown[3]).GetPoint3dAt(1), 5, 150));
            Assert.True(Is(shown[4], 0, 20, -10, 20));
            Assert.True(Is(shown[5], 0, 30, 30, 30));
            Assert.True(At(((DxfArc)shown[6]).Center, 0, 150));
            Assert.Equal(Math.PI / 2, ((DxfArc)shown[6]).StartAngle, Close);
            Assert.Equal(1.5 * Math.PI, ((DxfArc)shown[6]).EndAngle, Close);

            Property(insert, "Length").Value = 80.0;
            Property(insert, "Angle").Value = Math.PI / 4;
            DxfBlockReference back = TheInsert(Reopened(db));
            shown = Shown(back);
            Assert.Equal(80.0, ValueOf(back, "Length"), Close);
            Assert.Equal(Math.PI / 4, ValueOf(back, "Angle"), Close);
            Assert.True(Is(shown[0], 0, 0, 56.568542494923797, 56.568542494923847));
            Assert.True(At(((DxfDBText)shown[2]).Position, 50.911688245431421, 55.154328932550747));
            Assert.True(At(((DxfPolyline)shown[3]).GetPoint3dAt(0), 38.890872965260122, 31.819805153394654));
            Assert.True(At(((DxfPolyline)shown[3]).GetPoint3dAt(1), 60.104076400856535, 53.033008588991109));
            Assert.True(Is(shown[4], 14.142135623730951, 14.142135623730955, 7.0710678118654711, 21.21320343559643));
        }

        [Theory]
        [InlineData(-Math.PI / 4, 70.710678118654741, -70.710678118654769, 31.819805153394633, -38.890872965260122, 14.142135623730947, -14.142135623730955)]
        [InlineData(4.0, -65.364362086361197, -75.680249530792835, -36.466193519720235, -34.571906661078351, -13.072872417272238, -15.136049906158563)]
        public void AnAngleSet_TurnsThePointerAboutItsBasePoint_AsAutoCad2021Drew(double angle, double tipX, double tipY, double underX, double underY, double tickX, double tickY)
        {
            DxfDatabase db = Pointer(null, null, out _, out _, out _);

            Property(TheInsert(db), "Angle").Value = angle;

            List<DxfEntity> shown = Shown(TheInsert(Reopened(db)));
            Assert.True(Is(shown[0], 0, 0, tipX, tipY));
            Assert.True(At(((DxfPolyline)shown[3]).GetPoint3dAt(0), underX, underY));
            Assert.True(At(((DxfLine)shown[4]).StartPoint, tickX, tickY));
        }

        [Fact]
        public void AnInsertScaledByTwo_TakesItsLengthInItsOwnUnits()
        {
            DxfDatabase db = Pointer(null, null, out _, out _, out _);
            DxfBlockReference insert = TheInsert(db);
            insert.ScaleFactors = new GeoVector3(2, 2, 2);
            insert.Rotation = Deg(30);

            Assert.Equal(200.0, ValueOf(insert, "Length"), Close);
            Property(insert, "Length").Value = 300.0;
            Property(insert, "Angle").Value = 1.0;

            List<DxfEntity> shown = Shown(insert);
            Assert.Equal(300.0, ValueOf(insert, "Length"), Close);
            Assert.True(Is(shown[0], 0, 0, 81.045345880220964, 126.22064772118448));
            Assert.True(Is(shown[4], 10.806046117362795, 16.829419696157931, 2.3913362692838298, 22.23244275483933));
        }

        [Fact]
        public void AnEntityTheSelectionNamesToo_TurnsAndMovesWhole_AsAutoCad2021DrewIt()
        {
            DxfDatabase db = Pointer(null, null, out DxfBlockTableRecord block, out _, out DxfEntity tick);
            Select(Nodes(block).Last(), tick.Handle);

            Property(TheInsert(db), "Length").Value = 150.0;
            Assert.True(Is(Shown(TheInsert(db))[4], 70, 0, 70, 10));

            Property(TheInsert(db), "Angle").Value = Math.PI / 2;
            Assert.True(Is(Shown(TheInsert(db))[4], 0, 70, -10, 70));
        }

        [Fact]
        public void APolarStretchListingItsParametersGrip_AsAutoCadsAnnotationSampleDoes_LeavesTheGripOut()
        {
            DxfDatabase db = Pointer(null, null, out DxfBlockTableRecord block, out _, out _);
            List<DxfItem> nodes = Nodes(block);
            Select(nodes.Last(), nodes[1].Handle);

            Property(TheInsert(db), "Angle").Value = Math.PI / 2;

            Assert.True(Is(Shown(TheInsert(db))[0], 0, 0, 0, 100));
            Assert.True(Is(Shown(TheInsert(db))[4], 0, 20, -10, 20));
        }

        [Fact]
        public void TheCache_KeepsThePolarRecordsAsAutoCad2021WroteThem()
        {
            DxfDatabase db = Pointer(null, null, out DxfBlockTableRecord block, out DxfBlockPolarParameter polar, out _);

            Property(TheInsert(db), "Angle").Value = Math.PI / 2;

            Dictionary<string, List<string>> cache = Cache(TheInsert(Reopened(db)));
            List<string> record = cache["1"];
            Assert.Equal(new[] { "1071 11323277", "1071 15401870", "70 25", "70 104", "10 0", "20 0", "30 0" }, record.Take(7));
            Assert.Equal(100.0, double.Parse(record[8].Substring(3), CultureInfo.InvariantCulture), Close);
            Assert.Equal(new[] { "10 0", "20 0", "30 -1", "10 10", "20 0", "30 0" }, record.Skip(10));
            Assert.Equal(new[] { "1071 82519608", "1071 111249311", "70 25", "70 104", "40 0" }, cache["8"]);
            Assert.Equal(new[] { "1", "8" }, cache.Keys.OrderBy(k => k, StringComparer.Ordinal));
        }

        // ---- 3. what a polar parameter and a polar stretch refuse --------------------------------------------------------------

        [Fact]
        public void WhatAPolarParameterCannotBe_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Drawing("GH_ONE", out DxfBlockTableRecord block);
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            var origin = new GeoPoint3(0, 0, 0);
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => block.AddPolarParameter("Length", null, "Angle", null, origin, new GeoPoint3(0.0005, 0, 50), 0, null, null));
            Assert.Throws<ArgumentException>(() => block.AddPolarParameter("Length", null, "Angle", null, origin, new GeoPoint3(100, 0, 1), 0, null, null));
            Assert.Throws<ArgumentException>(() => block.AddPolarParameter(" ", null, "Angle", null, origin, new GeoPoint3(100, 0, 0), 0, null, null));
            Assert.Throws<ArgumentException>(() => block.AddPolarParameter("Length", null, "Angle", null, origin, new GeoPoint3(100, 0, 0), 0, DxfBlockParameterValueSet.List(new[] { 50.0, 150 }), null));
            Assert.Throws<ArgumentException>(() => block.AddPolarParameter("Length", null, "Angle", null, origin, new GeoPoint3(100, 0, 0), 0, null, DxfBlockParameterValueSet.Increments(0, 3 * Math.PI, Math.PI)));
            Assert.Throws<ArgumentException>(() => block.AddPolarParameter("Length", null, "Angle", null, origin, new GeoPoint3(100, 0, 0), 0, null, DxfBlockParameterValueSet.List(new[] { Math.PI / 2 })));
            Assert.Throws<ArgumentOutOfRangeException>(() => block.AddPolarParameter("Length", null, "Angle", null, origin, new GeoPoint3(100, 0, 0), double.NaN, null, null));
            Assert.Throws<ArgumentNullException>(() => block.AddPolarParameter("Length", null, null, null, origin, new GeoPoint3(100, 0, 0), 0, null, null));
            Assert.Equal(before, db.ToText());

            DxfBlockPolarParameter polar = block.AddPolarParameter("Length", null, "Angle", null, origin, new GeoPoint3(100, 0, 0), 0, null, DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 2));
            Assert.Equal(0.0, polar.Angle);
        }

        [Fact]
        public void WhatAPolarStretchCannotHold_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Drawing("GH_ONE", out DxfBlockTableRecord block);
            DxfLine line = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfLine far = Add(block, new DxfLine(new GeoPoint3(0, 30, 0), new GeoPoint3(30, 30, 0)));
            DxfArc half = Add(block, new DxfArc(new GeoPoint3(95, 0, 0), 10, 0, Math.PI));
            DxfBlockPolarParameter polar = block.AddPolarParameter("Length", null, "Angle", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 0, null, null);
            var low = new GeoPoint2(90, -10);
            var high = new GeoPoint2(110, 10);
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => block.AddPolarStretchAction(polar, low, high, new[] { far }, new DxfEntity[0]));
            Assert.Throws<ArgumentException>(() => block.AddPolarStretchAction(polar, low, high, new DxfEntity[] { half }, new DxfEntity[0]));
            Assert.Throws<ArgumentException>(() => block.AddPolarStretchAction(polar, low, high, new[] { line }, new[] { line }));
            Assert.Throws<ArgumentException>(() => block.AddPolarStretchAction(polar, low, high, new DxfEntity[0], new[] { far }));
            Assert.Throws<ArgumentException>(() => block.AddPolarStretchAction(polar, low, new GeoPoint2(90.0005, 10), new[] { line }, new DxfEntity[0]));
            Assert.Throws<ArgumentNullException>(() => block.AddPolarStretchAction(polar, low, high, new[] { line }, null));
            Assert.Equal(before, db.ToText());

            // An entity a polar stretch turns is worked on by no other action, either way round.
            block.AddPolarStretchAction(polar, low, high, new[] { line }, new[] { far });
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0), 0, null);
            string written = db.ToText();
            Assert.Throws<NotSupportedException>(() => block.AddMoveAction(width, new[] { far }));
            Assert.Throws<NotSupportedException>(() => block.AddStretchAction(width, new GeoPoint2(-10, -10), new GeoPoint2(10, 10), new[] { line }));
            Assert.Equal(written, db.ToText());

            DxfLine other = Add(block, new DxfLine(new GeoPoint3(100, -5, 0), new GeoPoint3(100, 5, 0)));
            block.AddMoveAction(width, new[] { other });
            Assert.Throws<NotSupportedException>(() => block.AddPolarStretchAction(polar, low, high, new[] { other }, new DxfEntity[0]));
        }

        // ---- 4. value sets of a rotation parameter ----------------------------------------------------------------------------

        /// <summary>The arrow of plugin D25: a line from (50, 25) at the angle given, 30 long, turned by Angle about (50, 25).</summary>
        private static DxfDatabase Arrow(DxfBlockParameterValueSet set, double own, out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_ARROW", out block);
            DxfLine arrow = Add(block, new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(50 + 30 * Math.Cos(own), 25 + 30 * Math.Sin(own), 0)));
            DxfBlockRotationParameter angle = block.AddRotationParameter("Angle", "Turn of the arrow", new GeoPoint3(50, 25, 0), 30, own, set);
            block.AddRotateAction(angle, new[] { arrow });
            Place(db, block);
            return db;
        }

        private static double AngleAfter(DxfDatabase db, string name, params double[] degrees)
        {
            foreach (double value in degrees)
            {
                Property(TheInsert(db), name).Value = Deg(value);
            }

            return ValueOf(TheInsert(Reopened(db)), name) * 180.0 / Math.PI;
        }

        [Theory]
        [InlineData(40.0, 45.0)]
        [InlineData(370.0, 15.0)]
        [InlineData(-10.0, 345.0)]
        [InlineData(22.5, 15.0)]
        [InlineData(352.5, 345.0)]
        public void ARotationOfStepsOf15Degrees_TakesAnAngleToTheNearestStep_ATieToTheStepBehind(double asked, double taken)
        {
            DxfDatabase db = Arrow(DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12), 0, out _);

            Assert.Equal(taken, AngleAfter(db, "Angle", asked), 1E-9);
        }

        [Theory]
        [InlineData(0, 90, 120.0, 90.0)]
        [InlineData(0, 90, 300.0, 0.0)]
        [InlineData(0, 90, 200.0, 90.0)]
        [InlineData(0, 90, 340.0, 0.0)]
        [InlineData(0, 90, 50.0, 45.0)]
        [InlineData(30, 150, 0.0, 30.0)]
        [InlineData(30, 150, 200.0, 150.0)]
        [InlineData(30, 150, 15.0, 30.0)]
        [InlineData(30, 150, 350.0, 30.0)]
        [InlineData(30, 150, 160.0, 150.0)]
        public void ARotationOfAMinimumAndAMaximum_TakesAnAngleOutsideThemToTheNearerRoundTheCircle_AsAutoCad2021Did(double minimum, double maximum, double asked, double taken)
        {
            DxfDatabase db = Arrow(DxfBlockParameterValueSet.Increments(Deg(minimum), Deg(maximum), minimum == 0 ? Math.PI / 12 : Math.PI / 6), Deg(minimum), out _);

            Assert.Equal(taken, AngleAfter(db, "Angle", asked), 1E-9);
        }

        [Fact]
        public void ARotationOfAList_TakesAnAngleToTheNearestRoundTheCircle_AsAutoCad2021Did()
        {
            DxfBlockParameterValueSet quarters = DxfBlockParameterValueSet.List(new[] { 0, Math.PI / 2, Math.PI, 1.5 * Math.PI });
            Assert.Equal(0.0, AngleAfter(Arrow(quarters, 0, out _), "Angle", 338), 1E-9);
            Assert.Equal(0.0, AngleAfter(Arrow(quarters, 0, out _), "Angle", 45), 1E-9);
            Assert.Equal(270.0, AngleAfter(Arrow(quarters, 0, out _), "Angle", 315), 1E-9);
            Assert.Equal(180.0, AngleAfter(Arrow(quarters, 0, out _), "Angle", 225), 1E-9);

            DxfBlockParameterValueSet two = DxfBlockParameterValueSet.List(new[] { Math.PI / 4, 0.75 * Math.PI });
            Assert.Equal(45.0, AngleAfter(Arrow(two, Math.PI / 4, out _), "Angle", 0), 1E-9);
            Assert.Equal(45.0, AngleAfter(Arrow(two, Math.PI / 4, out _), "Angle", 300), 1E-9);
            Assert.Equal(135.0, AngleAfter(Arrow(two, Math.PI / 4, out _), "Angle", 180), 1E-9);
            Assert.Equal(135.0, AngleAfter(Arrow(two, Math.PI / 4, out _), "Angle", 180, 270), 1E-9);

            DxfDatabase db = Arrow(quarters, 0, out _);
            Assert.Equal(new object[] { 0.0, Math.PI / 2, Math.PI, 1.5 * Math.PI }, Property(TheInsert(db), "Angle").GetAllowedValues());
        }

        [Fact]
        public void ARotationsValueSet_IsWrittenAsALinearParametersIs()
        {
            Arrow(DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12), 0, out DxfBlockTableRecord stepped);
            Arrow(DxfBlockParameterValueSet.List(new[] { 0, Math.PI / 2 }), 0, out DxfBlockTableRecord listed);
            Arrow(null, 0, out DxfBlockTableRecord free);

            DxfItem steps = Nodes(stepped)[0], list = Nodes(listed)[0], none = Nodes(free)[0];
            Assert.Equal(new[] { "7" }, Of(steps, "AcDbBlockRotationParameter", 96));
            Assert.Equal(Math.PI / 12, double.Parse(Of(steps, "AcDbBlockRotationParameter", 143).Single(), CultureInfo.InvariantCulture));
            Assert.Equal(2 * Math.PI, double.Parse(Of(steps, "AcDbBlockRotationParameter", 142).Single(), CultureInfo.InvariantCulture));
            Assert.Equal(new[] { "8" }, Of(list, "AcDbBlockRotationParameter", 96));
            Assert.Equal(new[] { "2" }, Of(list, "AcDbBlockRotationParameter", 175));
            Assert.Equal(2, Of(list, "AcDbBlockRotationParameter", 144).Count);
            Assert.Equal(new[] { "3", "0.0", "0.0", "0" }, new[] { 96, 141, 143, 175 }.Select(c => Of(none, "AcDbBlockRotationParameter", c).Single()));
        }

        [Fact]
        public void AnAngleOfAValueSetBeyondATurn_OrNotOfIt_IsRefused()
        {
            DxfDatabase db = Drawing("GH_ARROW", out DxfBlockTableRecord block);
            Add(block, new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0)));
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => block.AddRotationParameter("Angle", null, new GeoPoint3(50, 25, 0), 30, 0, DxfBlockParameterValueSet.List(new[] { 0, 7.0 })));
            Assert.Throws<ArgumentException>(() => block.AddRotationParameter("Angle", null, new GeoPoint3(50, 25, 0), 30, 0.1, DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12)));
            Assert.Equal(before, db.ToText());

            // A whole turn holds an angle of nought, and an angle of a turn is nought.
            block.AddRotationParameter("Angle", null, new GeoPoint3(50, 25, 0), 30, 2 * Math.PI, DxfBlockParameterValueSet.Increments(Math.PI / 2, 2 * Math.PI, Math.PI / 2));
        }

        [Fact]
        public void AListOfValues_MayHoldNought()
        {
            DxfBlockParameterValueSet set = DxfBlockParameterValueSet.List(new[] { Math.PI, 0 });

            Assert.Equal(new[] { 0, Math.PI }, set.Values);
            Assert.Throws<ArgumentOutOfRangeException>(() => DxfBlockParameterValueSet.List(new[] { -1.0 }));
        }

        // ---- 5. value sets of an XY parameter ---------------------------------------------------------------------------------

        /// <summary>The plate of plugin D25: a polyline 100 by 50 stretched by its top right corner, Width by 50 to 300 in steps of 10, Depth of 50, 80 and 120.</summary>
        private static DxfDatabase Plate(out DxfBlockTableRecord block)
        {
            DxfDatabase db = Drawing("GH_PLATE", out block);
            DxfPolyline outline = Add(block, Poly(0, 0, 100, 0, 100, 50, 0, 50));
            outline.Closed = true;
            DxfBlockXYParameter xy = block.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0),
                DxfBlockParameterValueSet.Increments(50, 300, 10), DxfBlockParameterValueSet.List(new[] { 50.0, 80, 120 }));
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { outline });
            Place(db, block);
            return db;
        }

        private static double DistanceAfter(DxfDatabase db, string name, params double[] values)
        {
            foreach (double value in values)
            {
                Property(TheInsert(db), name).Value = value;
            }

            return ValueOf(TheInsert(Reopened(db)), name);
        }

        [Theory]
        [InlineData("Width", 75.0, 70.0)]
        [InlineData("Width", 77.0, 80.0)]
        [InlineData("Width", 400.0, 300.0)]
        [InlineData("Width", 205.0, 200.0)]
        [InlineData("Depth", 10.0, 50.0)]
        [InlineData("Depth", 65.0, 50.0)]
        [InlineData("Depth", 100.0, 80.0)]
        public void AnXYParameterOfValueSets_TakesEachDistanceToItsSet_AsAutoCad2021Did(string name, double asked, double taken)
        {
            Assert.Equal(taken, DistanceAfter(Plate(out _), name, asked), Close);
        }

        [Theory]
        [InlineData(80.0)]
        [InlineData(120.0)]
        public void ATieOfAnXYList_GoesToTheValueNearerTheBlocksOwn_FromAnyValueBefore(double before)
        {
            Assert.Equal(50.0, DistanceAfter(Plate(out _), "Depth", before, 65), Close);
            Assert.Equal(80.0, DistanceAfter(Plate(out _), "Depth", before, 100), Close);
        }

        [Fact]
        public void AnXYParametersValueSets_AreWrittenTheSetOfYFirst()
        {
            DxfDatabase db = Plate(out DxfBlockTableRecord block);
            DxfItem xy = Nodes(block)[0];

            Assert.Equal(new[] { "8" }, Of(xy, "AcDbBlockXYParameter", 97));
            Assert.Equal(new[] { "3" }, Of(xy, "AcDbBlockXYParameter", 176));
            Assert.Equal(new[] { "50", "80", "120" }, Of(xy, "AcDbBlockXYParameter", 149));
            Assert.Equal(new[] { "7" }, Of(xy, "AcDbBlockXYParameter", 96));
            Assert.Equal(new[] { "50", "300", "10" }, new[] { 142, 143, 144 }.Select(c => Of(xy, "AcDbBlockXYParameter", c).Single()));
            Assert.Equal(new object[] { 50.0, 80.0, 120.0 }, Property(TheInsert(db), "Depth").GetAllowedValues());

            Property(TheInsert(db), "Width").Value = 160.0;
            Property(TheInsert(db), "Depth").Value = 120.0;
            var outline = (DxfPolyline)Shown(TheInsert(Reopened(db)))[0];
            Assert.True(At(outline.GetPoint3dAt(2), 160, 120));
        }

        // ---- 6. value sets of a polar parameter -------------------------------------------------------------------------------

        [Theory]
        [InlineData("Length", 75.0, 70.0)]
        [InlineData("Length", 85.0, 80.0)]
        [InlineData("Length", 400.0, 300.0)]
        [InlineData("Angle", 40.0, 45.0)]
        [InlineData("Angle", -10.0, 345.0)]
        public void APolarParameterOfIncrements_TakesItsDistanceAndItsAngleToThem_AsAutoCad2021Did(string name, double asked, double taken)
        {
            DxfDatabase db = Pointer(DxfBlockParameterValueSet.Increments(50, 300, 10), DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12), out _, out _, out _);

            Assert.Equal(taken, name == "Angle" ? AngleAfter(db, name, asked) : DistanceAfter(db, name, asked), 1E-9);
        }

        [Theory]
        [InlineData(100.0, 75.0, 100.0)]
        [InlineData(50.0, 125.0, 100.0)]
        [InlineData(200.0, 175.0, 150.0)]
        [InlineData(150.0, 25.0, 50.0)]
        public void APolarParameterOfAList_TakesATieToTheDistanceNearerTheBlocksOwn_AsAutoCad2021Did(double before, double asked, double taken)
        {
            DxfDatabase db = Pointer(DxfBlockParameterValueSet.List(new[] { 50.0, 100, 150, 200 }), DxfBlockParameterValueSet.List(new[] { 0, Math.PI / 2, Math.PI, 1.5 * Math.PI }), out _, out _, out _);

            Assert.Equal(taken, DistanceAfter(db, "Length", before, asked), Close);
            Assert.Equal(0.0, AngleAfter(db, "Angle", 338), 1E-9);
            Assert.Equal(90.0, AngleAfter(db, "Angle", 100), 1E-9);
            Assert.True(At(((DxfLine)Shown(TheInsert(db))[0]).EndPoint, 0, taken));
        }
    }
}
