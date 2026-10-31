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
    /// A dynamic block made from nothing (D24c): a new drawing of R2010 or later, the value of a linear property set on an INSERT
    /// as AutoCAD's <c>DynamicBlockReferenceProperty.Value</c> sets it (the anonymous block of the stretched copy, the
    /// representation data and the cache AutoCAD reads), and a visibility parameter with its states, as AutoCAD 2021 wrote and
    /// read them (plugins v17 and v18, the AutoCAD-saved visibility sample of ACadSharp).
    /// </summary>
    public class DxfDynamicBlockD24cTests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A new drawing of R2010 with the block GH_PANEL, a rectangle 100 by 50 of four lines (the right edge, the bottom, the top,
        /// the left edge, in that order), and an INSERT of it at the origin.
        /// </summary>
        private static DxfDatabase Panel(out DxfBlockTableRecord block, out DxfLine[] lines)
        {
            var db = new DxfDatabase("AC1024");
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

        /// <summary>The panel given Width as <see cref="Stretched"/> does, and Height from (0, 0) to (0, 50), the list 50, 80, 120, over the top.</summary>
        private static DxfDatabase WidthAndHeight()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfLine[] lines);
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10));
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            DxfBlockLinearParameter height = block.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 20, DxfBlockParameterValueSet.List(new[] { 50.0, 80, 120 }));
            block.AddStretchAction(height, new GeoPoint2(-10, 40), new GeoPoint2(110, 60), new[] { lines[0], lines[2], lines[3] });
            return db;
        }

        /// <summary>A sign of three lines: a frame (the first), a cross (the second) and a tick (the third), with the visibility states Cross, Tick and Blank.</summary>
        private static DxfDatabase Sign(out DxfBlockTableRecord block, out DxfLine[] lines)
        {
            var db = new DxfDatabase("AC1024");
            block = new DxfBlockTableRecord { Name = "GH_SIGN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            lines = new[]
            {
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0)),
                new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            block.AddVisibilityParameter("Mark", "What the sign shows", new GeoPoint3(20, -10, 0), new[]
            {
                new DxfBlockVisibilityState("Cross", new DxfEntity[] { lines[1] }),
                new DxfBlockVisibilityState("Tick", new DxfEntity[] { lines[2] }),
                new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
            });
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            return db;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfBlockReference TheInsert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().First();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static double ValueOf(DxfBlockReference insert, string name) => Assert.IsType<double>(Property(insert, name).Value);

        /// <summary>The lines of the block the INSERT places, in its order.</summary>
        private static List<DxfLine> Shown(DxfBlockReference insert) => insert.BlockTableRecord.OfType<DxfLine>().ToList();

        private static bool Is(DxfLine line, double x1, double y1, double x2, double y2)
            => line.StartPoint.IsEqualTo(new GeoPoint3(x1, y1, 0), Exact) && line.EndPoint.IsEqualTo(new GeoPoint3(x2, y2, 0), Exact);

        /// <summary>The XRECORDs AutoCAD reads an INSERT's values from, by their node ids, in the order of the dictionary.</summary>
        private static List<KeyValuePair<string, DxfXrecord>> Cache(DxfBlockReference insert)
        {
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var cache = (DxfDBDictionary)representation.GetAt("AppDataCache");
            var data = (DxfDBDictionary)cache.GetAt("ACAD_ENHANCEDBLOCKDATA");
            return data.Select(e => new KeyValuePair<string, DxfXrecord>(e.Key, (DxfXrecord)e.Value)).ToList();
        }

        private static List<string> Words(DxfXrecord record) => record.Data.Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim()).ToList();

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

        // ---- a new drawing ---------------------------------------------------------------------------------------------------

        [Fact]
        public void ANewDrawingOfR2010_IsEmptyAndOfThatVersion()
        {
            var db = new DxfDatabase("AC1024");

            Assert.Equal("AC1024", db.Version);
            Assert.Equal("UTF-8", db.CodePage);
            Assert.Empty(db.ModelSpace);
            Assert.Empty(db.PaperSpace);
            Assert.Equal(DxfUnits.Millimeters, db.Insunits);
            Assert.Equal("AC1024", Reopened(db).Version);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("ac1032")]
        public void ANewDrawingOfEachVersionFromR2000On_IsSavedAndReadBackAsItself(string version)
        {
            var db = new DxfDatabase(version);
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfDatabase back = Reopened(db);

            Assert.Equal(version.ToUpperInvariant(), back.Version);
            Assert.True(Assert.IsType<DxfLine>(Assert.Single(back.ModelSpace)).EndPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Exact));
            Assert.Equal(back.ToText(), DxfDatabase.Parse(back.ToText()).ToText());
        }

        [Fact]
        public void ANewDrawingOfAVersionTheLibraryDoesNotMake_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new DxfDatabase(null));
            Assert.Throws<ArgumentException>(() => new DxfDatabase("AC1009"));
            Assert.Throws<ArgumentException>(() => new DxfDatabase("AC1014"));
            Assert.Throws<ArgumentException>(() => new DxfDatabase("R2010"));
            Assert.Throws<ArgumentException>(() => new DxfDatabase(string.Empty));
        }

        [Fact]
        public void ANewDrawingOfR2000_IsTheDrawingTheWriterWrites()
        {
            Assert.Equal(new DxfWriter().ToString(), new DxfDatabase("AC1015").ToText());
        }

        [Fact]
        public void TheWorkedExamplesOfTheGuideRunFromNothing_AndTheInsertsReadTheValuesTheyWereGiven()
        {
            var db = new DxfDatabase("AC1024");
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

            DxfBlockLinearParameter width = panel.AddLinearParameter("Width", "Overall width", new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10));
            panel.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right, bottom, top });
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), panel);
            db.ModelSpace.AppendEntity(insert);
            insert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 200.0;

            var sign = new DxfBlockTableRecord { Name = "SIGN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(sign);
            var frame = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0));
            var cross = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0));
            var tick = new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0));

            foreach (DxfLine line in new[] { frame, cross, tick })
            {
                sign.AppendEntity(line);
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

            DxfDatabase again = Reopened(db);
            DxfBlockReference back = TheInsert(again);
            Assert.Equal(200.0, ValueOf(back, "Width"), Close);
            Assert.Equal("PANEL", back.DynamicBlockTableRecord.Name);
            Assert.True(Is(Shown(back)[0], 200, 0, 200, 50));
            DxfBlockReference shown = again.ModelSpace.OfType<DxfBlockReference>().Last();
            Assert.Equal("Tick", Property(shown, "Mark").Value);
            Assert.Equal(new object[] { "Cross", "Tick", "Blank" }, Property(shown, "Mark").GetAllowedValues());
            Assert.Equal(new[] { true, false, true }, Shown(shown).Select(l => l.Visible));
        }

        // ---- setting a linear property ---------------------------------------------------------------------------------------

        [Fact]
        public void AWidthSetTo200_IsReadBack_AndTheInsertPlacesAnAnonymousBlockOfTheRightEdgeAt200()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            DxfBlockReference insert = TheInsert(db);
            DxfDynamicBlockReferenceProperty width = Property(insert, "Width");

            width.Value = 200.0;

            Assert.Equal(200.0, Assert.IsType<double>(width.Value), Close);
            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.True(back.IsDynamicBlock);
            Assert.Equal("GH_PANEL", back.DynamicBlockTableRecord.Name);
            Assert.Equal("*U1", back.Name);
            Assert.Equal("*U1", back.AnonymousBlockTableRecord.Name);
            Assert.True(back.BlockTableRecord.IsAnonymous);
            Assert.Equal(200.0, ValueOf(back, "Width"), Close);

            // The right edge moved whole, the right ends of the bottom and the top with it; the left edge stayed.
            List<DxfLine> shown = Shown(back);
            Assert.Equal(4, shown.Count);
            Assert.True(Is(shown[0], 200, 0, 200, 50));
            Assert.True(Is(shown[1], 0, 0, 200, 0));
            Assert.True(Is(shown[2], 0, 50, 200, 50));
            Assert.True(Is(shown[3], 0, 50, 0, 0));

            // The definition is as it was.
            Assert.True(Is(back.DynamicBlockTableRecord.OfType<DxfLine>().First(), 100, 0, 100, 50));
        }

        [Fact]
        public void TheAnonymousBlockAndTheCacheAreWrittenAsAutoCadWritesThem()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            Property(TheInsert(db), "Width").Value = 200.0;
            DxfDatabase back = Reopened(db);
            DxfBlockReference insert = TheInsert(back);
            DxfBlockTableRecord dynamic = back.BlockTable["GH_PANEL"];
            DxfBlockTableRecord anonymous = insert.BlockTableRecord;

            // The anonymous block names the dynamic one; each copy carries its source's tag and its own handle.
            IReadOnlyList<DxfTypedValue> tag = anonymous.GetXDataForApplication("AcDbBlockRepBTag");
            Assert.Equal(new[] { "1070 1", "1005 " + dynamic.Handle }, tag.Skip(1).Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim()));
            int n = 0;

            foreach (DxfEntity copy in anonymous)
            {
                IReadOnlyList<DxfTypedValue> data = copy.GetXDataForApplication("AcDbBlockRepETag");
                Assert.Equal((n++).ToString(CultureInfo.InvariantCulture), data[2].Value.Trim());
                Assert.Equal(copy.Handle, data[3].Value.Trim());
            }

            // The representation data names the dynamic block; the cache holds the parameter (node 1) and the stretch (node 8),
            // with AutoCAD's numbers, and there is no history, as AutoCAD keeps none after its own Value set.
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            DxfDBObject data2 = representation.GetAt("AcDbRepData");
            Assert.Equal("ACDB_BLOCKREPRESENTATION_DATA", data2.Item.Type);
            Assert.Contains(data2.Item.Groups, g => g.TypeCode == 340 && g.Value.Trim() == dynamic.Handle);
            var cache = (DxfDBDictionary)representation.GetAt("AppDataCache");
            Assert.Equal(new[] { "ACAD_ENHANCEDBLOCKDATA" }, cache.Select(e => e.Key));

            List<KeyValuePair<string, DxfXrecord>> records = Cache(insert);
            Assert.Equal(new[] { "1", "8" }, records.Select(r => r.Key));
            Assert.Equal(new[] { "1071 18597260", "1071 25303744", "70 25", "70 104", "10 0", "20 0", "30 0", "10 200", "20 0", "30 0", "10 0", "20 0", "30 -1" }, Words(records[0].Value));
            Assert.Equal(new[] { "1071 6895636", "1071 9291323", "70 25", "70 104", "40 0" }, Words(records[1].Value));
            Assert.Contains("  0\nCLASS\n  1\nACDB_BLOCKREPRESENTATION_DATA\n", back.ToText().Replace("\r\n", "\n"), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(75.0, 70.0)]
        [InlineData(77.0, 80.0)]
        [InlineData(400.0, 300.0)]
        [InlineData(20.0, 50.0)]
        [InlineData(200.0, 200.0)]
        public void AValueIsTakenToTheIncrementsAsAutoCadTookIt(double given, double taken)
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            DxfDynamicBlockReferenceProperty width = Property(TheInsert(db), "Width");

            width.Value = given;

            Assert.Equal(taken, (double)width.Value, Close);
            Assert.Equal(taken, ValueOf(TheInsert(Reopened(db)), "Width"), Close);
        }

        [Fact]
        public void AValueIsTakenToTheNearestOfAList_ATieToTheOneFartherFromTheValueItHad_AsAutoCadTookMostTiesInV20()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.List(new[] { 50.0, 100, 150, 200, 250, 300 }));
            DxfDynamicBlockReferenceProperty width = Property(TheInsert(db), "Width");

            // From 100, 75 (between 50 and 100) goes to 50, beyond it on the way down (AutoCAD 2021 took 100 in plugin v17, one
            // of its ties against the rule of v20); 77 and 400 go to the nearest, 100 and 300 (plugins v17, v18).
            width.Value = 75.0;
            Assert.Equal(50.0, (double)width.Value, Close);
            width.Value = 77.0;
            Assert.Equal(100.0, (double)width.Value, Close);
            width.Value = 400.0;
            Assert.Equal(300.0, (double)width.Value, Close);

            // From 300, 175 (between 150 and 200) goes to 150, the farther of the two from 300, as AutoCAD 2021 took it (v20).
            width.Value = 175.0;
            Assert.Equal(150.0, (double)width.Value, Close);

            // From 80, 100 (between 80 and 120) goes to 120 (AutoCAD 2021 took 80 in plugin v18, 24b-g, against the rule of v20).
            DxfDatabase two = WidthAndHeight();
            DxfDynamicBlockReferenceProperty height = Property(TheInsert(two), "Height");
            height.Value = 80.0;
            height.Value = 100.0;
            Assert.Equal(120.0, (double)height.Value, Close);
            height.Value = 120.0;
            Assert.Equal(120.0, (double)height.Value, Close);
        }

        [Fact]
        public void NoValueSet_TakesTheValueAsGiven()
        {
            DxfDatabase db = Stretched(null);
            DxfDynamicBlockReferenceProperty width = Property(TheInsert(db), "Width");

            width.Value = 123.25;

            Assert.Equal(123.25, (double)width.Value, Close);
            Assert.True(Is(Shown(TheInsert(Reopened(db)))[0], 123.25, 0, 123.25, 50));
        }

        [Fact]
        public void WidthAndHeightSetInTurn_EachStretchesItsOwnEdges_AndTheOtherKeepsItsValue()
        {
            DxfDatabase db = WidthAndHeight();
            DxfBlockReference insert = TheInsert(db);

            Property(insert, "Width").Value = 200.0;
            Property(insert, "Height").Value = 120.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(200.0, ValueOf(back, "Width"), Close);
            Assert.Equal(120.0, ValueOf(back, "Height"), Close);
            List<DxfLine> shown = Shown(back);
            Assert.True(Is(shown[0], 200, 0, 200, 120));
            Assert.True(Is(shown[1], 0, 0, 200, 0));
            Assert.True(Is(shown[2], 0, 120, 200, 120));
            Assert.True(Is(shown[3], 0, 120, 0, 0));
            Assert.Equal(new[] { "1", "16", "8", "9" }, Cache(back).Select(r => r.Key));
        }

        [Fact]
        public void SetAgain_TheSameAnonymousBlockIsFilledAnew()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            DxfDynamicBlockReferenceProperty width = Property(TheInsert(db), "Width");

            width.Value = 200.0;
            width.Value = 150.0;

            DxfDatabase back = Reopened(db);
            Assert.Equal(new[] { "*U1" }, back.BlockTable.Where(b => b.IsAnonymous).Select(b => b.Name));
            Assert.Equal(4, back.BlockTable["*U1"].Count());
            Assert.True(Is(Shown(TheInsert(back))[0], 150, 0, 150, 50));
            Assert.Equal(150.0, ValueOf(TheInsert(back), "Width"), Close);
        }

        [Fact]
        public void AnInsertSharingItsAnonymousBlock_IsGivenOneOfItsOwn_AndTheOtherKeepsItsShape()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            DxfBlockReference first = TheInsert(db);
            Property(first, "Width").Value = 200.0;
            var second = (DxfBlockReference)first.Clone();
            db.ModelSpace.AppendEntity(second);
            Assert.Equal("*U1", second.Name);

            Property(second, "Width").Value = 150.0;

            DxfDatabase back = Reopened(db);
            List<DxfBlockReference> inserts = back.ModelSpace.OfType<DxfBlockReference>().ToList();
            Assert.Equal("*U1", inserts[0].Name);
            Assert.Equal("*U2", inserts[1].Name);
            Assert.Equal(200.0, ValueOf(inserts[0], "Width"), Close);
            Assert.Equal(150.0, ValueOf(inserts[1], "Width"), Close);
            Assert.True(Is(Shown(inserts[0])[0], 200, 0, 200, 50));
            Assert.True(Is(Shown(inserts[1])[0], 150, 0, 150, 50));
        }

        [Fact]
        public void AHistoryTheInsertKept_IsTakenOut_AsAutoCadTookItOut()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            DxfBlockReference insert = TheInsert(db);
            Property(insert, "Width").Value = 150.0;
            var representation = (DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation");
            var cache = (DxfDBDictionary)representation.GetAt("AppDataCache");
            // The record AutoCAD keeps of a Width typed in its Properties palette (the extended codes of AutoCAD's own, which the
            // public XRECORD does not take).
            cache.Add("ACAD_ENHANCEDBLOCKHISTORY", "XRECORD", b =>
            {
                b.Group(100, "AcDbXrecord");
                b.Group(280, "1");
                b.Group(1070, "3");
                b.Group(1071, "1");
                b.Group(300, "Width");
                b.Group(40, "150.0");
            });
            Assert.Equal(new[] { "ACAD_ENHANCEDBLOCKDATA", "ACAD_ENHANCEDBLOCKHISTORY" }, cache.Select(e => e.Key));

            Property(insert, "Width").Value = 200.0;

            Assert.Equal(new[] { "ACAD_ENHANCEDBLOCKDATA" }, cache.Select(e => e.Key));
            Assert.Equal(200.0, ValueOf(TheInsert(Reopened(db)), "Width"), Close);
        }

        [Fact]
        public void AnInsertScaledByTwo_ShowsTwiceTheDistance_AndASetValueIsHalvedInTheBlock()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            DxfBlockReference insert = TheInsert(db);
            insert.ScaleFactors = new GeoVector3(2, 2, 2);
            Assert.Equal(200.0, ValueOf(insert, "Width"), Close);

            Property(insert, "Width").Value = 300.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(300.0, ValueOf(back, "Width"), Close);
            Assert.True(Is(Shown(back)[0], 150, 0, 150, 50));
        }

        [Fact]
        public void APolylineIsStretchedByItsVerticesInTheFrame()
        {
            var db = new DxfDatabase("AC1024");
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
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));

            Property(TheInsert(db), "Width").Value = 180.0;

            var shown = (DxfPolyline)Assert.Single(TheInsert(Reopened(db)).BlockTableRecord);
            Assert.True(shown.Closed);
            Assert.True(shown.GetPoint3dAt(0).IsEqualTo(new GeoPoint3(0, 0, 0), Exact));
            Assert.True(shown.GetPoint3dAt(1).IsEqualTo(new GeoPoint3(180, 0, 0), Exact));
            Assert.True(shown.GetPoint3dAt(2).IsEqualTo(new GeoPoint3(180, 50, 0), Exact));
            Assert.True(shown.GetPoint3dAt(3).IsEqualTo(new GeoPoint3(0, 50, 0), Exact));
        }

        [Fact]
        public void WhatCannotBeSet_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            DxfBlockReference insert = TheInsert(db);
            DxfDynamicBlockReferenceProperty width = Property(insert, "Width");
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => width.Value = "200");
            Assert.Throws<ArgumentNullException>(() => width.Value = null);
            Assert.Throws<ArgumentException>(() => width.Value = double.NaN);
            Assert.Equal(before, db.ToText());

            // No value set: a distance of nought or less is none.
            DxfDatabase free = Stretched(null);
            DxfDynamicBlockReferenceProperty any = Property(TheInsert(free), "Width");
            string freeBefore = free.ToText();
            Assert.Throws<ArgumentOutOfRangeException>(() => any.Value = -5.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => any.Value = 0.0);
            Assert.Equal(freeBefore, free.ToText());

            // Scaled unlike along its axes, the INSERT is no dynamic block's to AutoCAD, and is not set.
            insert.ScaleFactors = new GeoVector3(2, 3, 2);
            string scaled = db.ToText();
            Assert.Throws<InvalidOperationException>(() => Property(insert, "Width").Value = 200.0);
            Assert.Equal(scaled, db.ToText());
        }

        [Fact]
        public void AWholeNumberIsTakenAsTheDistanceItIs()
        {
            DxfDatabase db = Stretched(DxfBlockParameterValueSet.Increments(50, 300, 10));
            DxfDynamicBlockReferenceProperty width = Property(TheInsert(db), "Width");

            width.Value = 250;

            Assert.Equal(250.0, Assert.IsType<double>(width.Value), Close);
        }

        // ---- a visibility parameter ------------------------------------------------------------------------------------------

        [Fact]
        public void AVisibilityParameter_GivesTheInsertItsFirstState_AndEveryStateAllowed()
        {
            DxfDatabase back = Reopened(Sign(out _, out _));
            DxfBlockReference insert = TheInsert(back);

            Assert.True(insert.IsDynamicBlock);
            DxfDynamicBlockReferenceProperty mark = Assert.Single(insert.DynamicBlockReferencePropertyCollection);
            Assert.Equal("Mark", mark.PropertyName);
            Assert.Equal("What the sign shows", mark.Description);
            Assert.Equal("Cross", Assert.IsType<string>(mark.Value));
            Assert.Equal(new object[] { "Cross", "Tick", "Blank" }, mark.GetAllowedValues());
            Assert.Equal(5, mark.PropertyTypeCode);

            // The block is kept in its first state: the tick, of no state but the second, is hidden; the frame, of none, shows.
            List<DxfLine> lines = back.BlockTable["GH_SIGN"].OfType<DxfLine>().ToList();
            Assert.Equal(new[] { true, true, false }, lines.Select(l => l.Visible));
        }

        [Fact]
        public void AVisibilityParameter_IsWrittenAsAutoCadWroteOne()
        {
            DxfDatabase back = Reopened(Sign(out DxfBlockTableRecord made, out DxfLine[] lines));
            DxfBlockTableRecord block = back.BlockTable["GH_SIGN"];
            List<DxfItem> nodes = Nodes(block);

            Assert.Equal(new[] { "BLOCKVISIBILITYPARAMETER", "BLOCKVISIBILITYGRIP", "BLOCKGRIPLOCATIONCOMPONENT", "BLOCKGRIPLOCATIONCOMPONENT" }, nodes.Select(n => n.Type));
            DxfItem parameter = nodes[0];
            Assert.Equal(new[] { "Visibility State" }, Of(parameter, "AcDbBlockElement", 300));
            Assert.Equal(new[] { "20", "2" }, Of(parameter, "AcDbBlock1PtParameter", 1010).Concat(Of(parameter, "AcDbBlock1PtParameter", 93)));
            Assert.Equal(new[] { "Mark" }, Of(parameter, "AcDbBlockVisibilityParameter", 301));
            Assert.Equal(new[] { "What the sign shows" }, Of(parameter, "AcDbBlockVisibilityParameter", 302));
            Assert.Equal(new[] { "2" }, Of(parameter, "AcDbBlockVisibilityParameter", 93));
            Assert.Equal(new[] { lines[1].Handle, lines[2].Handle }, Of(parameter, "AcDbBlockVisibilityParameter", 331));
            Assert.Equal(new[] { "3" }, Of(parameter, "AcDbBlockVisibilityParameter", 92));
            Assert.Equal(new[] { "Cross", "Tick", "Blank" }, Of(parameter, "AcDbBlockVisibilityParameter", 303));
            Assert.Equal(new[] { "1", "1", "0" }, Of(parameter, "AcDbBlockVisibilityParameter", 94));
            Assert.Equal(new[] { lines[1].Handle, lines[2].Handle }, Of(parameter, "AcDbBlockVisibilityParameter", 332));
            Assert.Equal(new[] { "0", "0", "0" }, Of(parameter, "AcDbBlockVisibilityParameter", 95));
            Assert.Equal(new[] { "UpdatedX", "UpdatedY" }, nodes.Skip(2).Select(n => Of(n, "AcDbBlockGripExpr", 300).Single()));

            // AutoCAD's own graph of a visibility parameter (ACadSharp's sample): four nodes, three edges, every number the same.
            int[] autocad =
            {
                96, 4, 97, 4,
                91, 0, 93, 32, 95, 1, 92, 0, 92, 0, 92, 1, 92, 2,
                91, 1, 93, 32, 95, 2, 92, -1, 92, -1, 92, 0, 92, 0,
                91, 2, 93, 32, 95, 3, 92, 1, 92, 1, 92, -1, 92, -1,
                91, 3, 93, 32, 95, 4, 92, 2, 92, 2, 92, -1, 92, -1,
                92, 0, 93, 0, 94, 1, 91, 1, 91, 0, 92, -1, 92, -1, 92, -1, 92, -1, 92, -1,
                92, 1, 93, 0, 94, 1, 91, 0, 91, 2, 92, -1, 92, -1, 92, -1, 92, 2, 92, -1,
                92, 2, 93, 0, 94, 1, 91, 0, 91, 3, 92, -1, 92, -1, 92, 1, 92, -1, 92, -1,
            };

            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            List<DxfTypedValue> groups = graph.Item.File.TailOf(graph.Item, 0);
            groups = groups.Skip(groups.FindIndex(g => g.TypeCode == 100 && g.Value == "AcDbEvalGraph") + 1).ToList();
            var numbers = new List<int>();

            foreach (DxfTypedValue group in groups.Where(g => g.TypeCode != 360))
            {
                numbers.Add(group.TypeCode);
                numbers.Add(int.Parse(group.Value.Trim(), CultureInfo.InvariantCulture));
            }

            Assert.Equal(autocad, numbers);
            string text = back.ToText().Replace("\r\n", "\n");

            foreach (string type in new[] { "BLOCKVISIBILITYPARAMETER", "BLOCKVISIBILITYGRIP", "BLOCKGRIPLOCATIONCOMPONENT" })
            {
                Assert.Contains("  0\nCLASS\n  1\n" + type + "\n", text, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AStateSet_ShowsItsEntitiesAndHidesTheOthersOfTheParameter_AndIsReadBack()
        {
            DxfDatabase db = Sign(out _, out _);
            DxfDynamicBlockReferenceProperty mark = Property(TheInsert(db), "Mark");

            mark.Value = "tick";

            Assert.Equal("Tick", mark.Value);
            DxfDatabase again = Reopened(db);
            DxfBlockReference back = TheInsert(again);
            Assert.Equal("Tick", Property(back, "Mark").Value);
            Assert.Equal(new[] { true, false, true }, Shown(back).Select(l => l.Visible));
            Assert.Equal(new[] { "1071 135625452", "1071 184556386", "70 25", "70 104", "10 20", "20 -10", "30 0", "1 Tick" }, Words(Cache(back).Single().Value));

            // Set again once the drawing is read back, its anonymous block is filled anew.
            Property(back, "Mark").Value = "Blank";
            Assert.Equal(new[] { true, false, false }, Shown(back).Select(l => l.Visible));
            Assert.Equal("Blank", Property(TheInsert(Reopened(again)), "Mark").Value);
        }

        [Fact]
        public void AStateAndAWidth_AreKeptTogether()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfLine[] lines);
            DxfBlockLinearParameter width = block.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            block.AddVisibilityParameter("Sides", null, new GeoPoint3(50, 70, 0), new[]
            {
                new DxfBlockVisibilityState("All", lines),
                new DxfBlockVisibilityState("Open", lines.Skip(1).Take(2)),
            });
            DxfBlockReference insert = TheInsert(db);

            Property(insert, "Sides").Value = "Open";
            Property(insert, "Width").Value = 160.0;

            DxfBlockReference back = TheInsert(Reopened(db));
            Assert.Equal(160.0, ValueOf(back, "Width"), Close);
            Assert.Equal("Open", Property(back, "Sides").Value);
            List<DxfLine> shown = Shown(back);
            Assert.Equal(new[] { false, true, true, false }, shown.Select(l => l.Visible));
            Assert.True(Is(shown[0], 160, 0, 160, 50));
            Assert.True(Is(shown[2], 0, 50, 160, 50));
        }

        [Fact]
        public void AVisibilityParameterThatCannotBeWritten_IsRefused_AndNothingIsWritten()
        {
            DxfDatabase db = Panel(out DxfBlockTableRecord block, out DxfLine[] lines);
            var stray = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0));
            db.ModelSpace.AppendEntity(stray);
            string before = db.ToText();
            var at = new GeoPoint3(0, 0, 0);

            Assert.Throws<ArgumentNullException>(() => block.AddVisibilityParameter("V", null, at, null));
            Assert.Throws<ArgumentException>(() => block.AddVisibilityParameter("V", null, new GeoPoint3(double.NaN, 0, 0), new[] { new DxfBlockVisibilityState("A", lines) }));
            Assert.Throws<ArgumentException>(() => block.AddVisibilityParameter("V", null, at, new DxfBlockVisibilityState[0]));
            Assert.Throws<ArgumentException>(() => block.AddVisibilityParameter(" ", null, at, new[] { new DxfBlockVisibilityState("A", lines) }));
            Assert.Throws<ArgumentException>(() => block.AddVisibilityParameter("V", null, at, new[] { new DxfBlockVisibilityState("A", lines), new DxfBlockVisibilityState("a", lines) }));
            Assert.Throws<ArgumentException>(() => block.AddVisibilityParameter("V", null, at, new[] { new DxfBlockVisibilityState("A", new DxfEntity[] { stray }) }));
            Assert.Throws<ArgumentException>(() => new DxfBlockVisibilityState("two\nlines", lines));
            Assert.Throws<ArgumentNullException>(() => new DxfBlockVisibilityState("A", null));
            Assert.Throws<InvalidOperationException>(() => db.ModelSpace.AddVisibilityParameter("V", null, at, new[] { new DxfBlockVisibilityState("A", new DxfEntity[0]) }));
            Assert.Equal(before, db.ToText());

            // One visibility parameter to a block, as AutoCAD's Block Editor allows one.
            block.AddVisibilityParameter("V", null, at, new[] { new DxfBlockVisibilityState("A", lines) });
            string one = db.ToText();
            Assert.Throws<InvalidOperationException>(() => block.AddVisibilityParameter("W", null, at, new[] { new DxfBlockVisibilityState("B", lines) }));
            Assert.Equal(one, db.ToText());

            // A state the parameter has not is refused.
            DxfDynamicBlockReferenceProperty v = Property(TheInsert(db), "V");
            Assert.Throws<ArgumentException>(() => v.Value = "B");
            Assert.Throws<ArgumentException>(() => v.Value = 1.0);
            Assert.Equal(one, db.ToText());
        }
    }
}
