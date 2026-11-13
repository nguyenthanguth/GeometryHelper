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
    /// The block properties tables of D26: written, read and set as AutoCAD 2021 read and set the library's tables and its own
    /// Parking sample (plugin D26, rounds 2, 4 and 6, 0 lines differing), and the history of grip edits an INSERT keeps.
    /// </summary>
    public class DxfDynamicBlockD26Tests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        private static T Add<T>(DxfBlockTableRecord block, T entity)
            where T : DxfEntity
        {
            block.AppendEntity(entity);
            return entity;
        }

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static List<DxfBlockReference> Inserts(DxfDatabase db) =>
            db.ModelSpace.OfType<DxfBlockReference>().ToList();

        private static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) =>
            insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static object ValueOf(DxfBlockReference insert, string name) => Property(insert, name).Value;

        private static bool At(GeoPoint3 point, double x, double y) => point.IsEqualTo(new GeoPoint3(x, y, 0), Exact);

        /// <summary>The objects of the graph's nodes of a block, in its order.</summary>
        private static List<DxfItem> Nodes(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            return graph
                .Item.Groups.Where(g => g.TypeCode == 360)
                .Select(g => graph.Item.File.ObjectItem(g.Value))
                .ToList();
        }

        /// <summary>
        /// The plate of plugin D26: 100 by 50, Width stretching its right edge and Height its top, the attribute MARK of P100,
        /// and, where asked, the table of Width, Height and MARK of the rows (100, 50, P100), (200, 80, P200) and (150, 60, P150);
        /// an INSERT of an ATTRIB MARK of X1, and one scaled by 2 of none.
        /// </summary>
        private static DxfDatabase Plate(
            bool table,
            out DxfBlockTableRecord block,
            out DxfBlockLinearParameter width,
            out DxfBlockLinearParameter height,
            out DxfAttributeDefinition mark
        )
        {
            var db = new DxfDatabase("AC1024");
            block = new DxfBlockTableRecord { Name = "PLATE", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine bottom = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfLine right = Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)));
            DxfLine top = Add(block, new DxfLine(new GeoPoint3(100, 50, 0), new GeoPoint3(0, 50, 0)));
            DxfLine left = Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)));
            mark = Add(
                block,
                new DxfAttributeDefinition(new GeoPoint3(10, 10, 0), "P100", "MARK", "Mark", null) { Height = 5 }
            );
            width = block.AddLinearParameter(
                "Width",
                string.Empty,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -10,
                null
            );
            block.AddStretchAction(
                width,
                new GeoPoint2(90, -5),
                new GeoPoint2(110, 55),
                new DxfEntity[] { bottom, right, top }
            );
            height = block.AddLinearParameter(
                "Height",
                string.Empty,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0),
                10,
                null
            );
            block.AddStretchAction(
                height,
                new GeoPoint2(-5, 40),
                new GeoPoint2(105, 55),
                new DxfEntity[] { right, top, left }
            );

            if (table)
            {
                block.AddBlockPropertiesTable(new GeoPoint3(0, -20, 0), Columns(width, height, mark), Rows());
            }

            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            insert.AttributeCollection.AppendAttribute(
                new DxfAttributeReference(new GeoPoint3(10, 10, 0), "X1", "MARK", null) { Height = 5 }
            );
            db.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(400, 0, 0), block) { ScaleFactors = new GeoVector3(2, 2, 2) }
            );
            return db;
        }

        private static DxfDatabase Plate() => Plate(true, out _, out _, out _, out _);

        private static DxfBlockPropertiesTableColumn[] Columns(
            DxfBlockLinearParameter width,
            DxfBlockLinearParameter height,
            DxfAttributeDefinition mark
        ) =>
            new[]
            {
                DxfBlockPropertiesTableColumn.Distance(width),
                DxfBlockPropertiesTableColumn.Distance(height),
                DxfBlockPropertiesTableColumn.Attribute(mark),
            };

        private static DxfBlockPropertiesTableRow[] Rows() =>
            new[]
            {
                new DxfBlockPropertiesTableRow(100.0, 50.0, "P100"),
                new DxfBlockPropertiesTableRow(200.0, 80.0, "P200"),
                new DxfBlockPropertiesTableRow(150.0, 60.0, "P150"),
            };

        // ---- 1. written as AutoCAD keeps a table -------------------------------------------------------------------------------

        [Fact]
        public void ATable_IsWrittenAsFourNodes_TheTableItsGripAndTheGripsComponents_AndNamedOnTheGraph()
        {
            Plate(true, out DxfBlockTableRecord block, out _, out _, out _);
            List<DxfItem> nodes = Nodes(block);

            Assert.Equal(
                new[]
                {
                    "BLOCKPROPERTIESTABLE",
                    "BLOCKPROPERTIESTABLEGRIP",
                    "BLOCKGRIPLOCATIONCOMPONENT",
                    "BLOCKGRIPLOCATIONCOMPONENT",
                },
                nodes.Skip(nodes.Count - 4).Select(n => n.Type)
            );
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            Assert.Equal(
                new[] { "AcadBPTGraphNodeId", "17" },
                graph.GetXDataForApplication("AcadBPTGraphNodeId").Select(g => g.Value.Trim())
            );
        }

        [Fact]
        public void AColumn_NamesItsParameterOrItsAttributeDefinition_AndTheRowsHoldRealsAndTexts()
        {
            Plate(
                true,
                out DxfBlockTableRecord block,
                out DxfBlockLinearParameter width,
                out _,
                out DxfAttributeDefinition mark
            );
            DxfItem table = Nodes(block).First(n => n.Type == "BLOCKPROPERTIESTABLE");
            List<DxfTypedValue> own = table
                .Groups.SkipWhile(g => !(g.TypeCode == 100 && g.Value == "AcDbBlockPropertiesTable"))
                .Skip(1)
                .ToList();

            Assert.Equal("Block Table1", own.First(g => g.TypeCode == 300).Value);
            Assert.Equal(
                new[] { width.Handle, mark.Handle },
                own.Where(g => g.TypeCode == 340 && g.Value.Trim() != "0")
                    .Select(g => g.Value.Trim())
                    .Where((h, i) => i != 1)
            );
            Assert.Equal(
                new[] { "UpdatedDistance", "UpdatedDistance", string.Empty },
                own.Where(g => g.TypeCode == 301).Skip(1).Select(g => g.Value.Trim())
            );
            Assert.Equal(
                new[] { "P100", "P200", "P150" },
                own.Where(g => g.TypeCode == 300).Skip(4).Select(g => g.Value)
            );
            Assert.Contains(table.Handle, mark.Item.Groups.Where(g => g.TypeCode == 330).Select(g => g.Value.Trim()));
        }

        [Fact]
        public void ATableReadBack_GivesItsNameColumnsAndRows()
        {
            DxfDatabase db = Plate(true, out _, out _, out _, out _);
            DxfBlockPropertiesTable table = Reopened(db)
                .BlockTable.Cast<DxfBlockTableRecord>()
                .First(b => b.Name == "PLATE")
                .BlockPropertiesTable;

            Assert.Equal("Block Table1", table.Name);
            Assert.False(table.MustMatch);
            Assert.Equal(new[] { "Width", "Height", "MARK" }, table.ColumnNames);
            Assert.Equal(new object[] { 200.0, 80.0, "P200" }, table.Rows[1].Values);
            Assert.Null(new DxfDatabase("AC1024").BlockTable.Cast<DxfBlockTableRecord>().First().BlockPropertiesTable);
        }

        // ---- 2. read and set as AutoCAD 2021 read and set them ---------------------------------------------------------------

        [Fact]
        public void AnInsert_ShowsBlockTable1_TheRowOfItsValues_AfterItsOtherProperties()
        {
            DxfBlockReference insert = Inserts(Reopened(Plate()))[0];

            Assert.Equal(
                new[] { "Width", "Height", "Block Table1" },
                insert.DynamicBlockReferencePropertyCollection.Where(p => p.Show).Select(p => p.PropertyName)
            );
            DxfDynamicBlockReferenceProperty table = Property(insert, "Block Table1");
            Assert.Equal((short)0, table.Value);
            Assert.Equal(3, table.PropertyTypeCode);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.NoUnits, table.UnitsType);
            Assert.False(table.ReadOnly);
            Assert.Empty(table.GetAllowedValues());
        }

        [Fact]
        public void ARowSet_SetsItsDistancesAndTheAttribute_AsAutoCadSetRow1()
        {
            DxfDatabase db = Plate();
            Property(Inserts(db)[0], "Block Table1").Value = (short)1;

            DxfBlockReference insert = Inserts(Reopened(db))[0];
            Assert.Equal(200.0, (double)ValueOf(insert, "Width"), Close);
            Assert.Equal(80.0, (double)ValueOf(insert, "Height"), Close);
            Assert.Equal((short)1, ValueOf(insert, "Block Table1"));
            Assert.Equal("P200", insert.AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);
            var right = (DxfLine)insert.BlockTableRecord.Cast<DxfEntity>().ElementAt(1);
            Assert.True(At(right.StartPoint, 200, 0) && At(right.EndPoint, 200, 80));
        }

        [Fact]
        public void ADistanceSetOffTheRows_ReadsMinusOne_AndBackOnARowReadsIt_TheAttributeNotMatched()
        {
            DxfDatabase db = Plate();
            DxfBlockReference insert = Inserts(db)[0];

            Property(insert, "Block Table1").Value = (short)1;
            Property(insert, "Width").Value = 120.0;
            Assert.Equal((short)-1, ValueOf(insert, "Block Table1"));
            Assert.Equal("P200", insert.AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);

            Property(insert, "Block Table1").Value = (short)0;
            Property(insert, "Height").Value = 80.0;
            Property(insert, "Width").Value = 200.0;

            Assert.Equal((short)1, ValueOf(Inserts(Reopened(db))[0], "Block Table1"));
            Assert.Equal(
                "P100",
                Inserts(Reopened(db))[0].AttributeCollection.Cast<DxfAttributeReference>().Single().TextString
            );
        }

        [Theory]
        [InlineData(3)]
        [InlineData(-1)]
        public void ARowOutsideTheTable_IsRefused_AsAutoCadRefusedIt(int row)
        {
            DxfDatabase db = Plate();
            DxfBlockReference insert = Inserts(db)[1];
            Property(insert, "Block Table1").Value = 2;

            Assert.Throws<ArgumentOutOfRangeException>(() => Property(insert, "Block Table1").Value = row);
            Assert.Equal((short)2, ValueOf(Inserts(Reopened(db))[1], "Block Table1"));
            Assert.Throws<ArgumentException>(() => Property(insert, "Block Table1").Value = 1.0);
        }

        [Fact]
        public void AnInsertScaledByTwo_MatchesAndSetsTheRowsInTheBlocksUnits()
        {
            DxfDatabase db = Plate();
            DxfBlockReference scaled = Inserts(db)[1];

            Property(scaled, "Width").Value = 400.0;
            Assert.Equal((short)-1, ValueOf(scaled, "Block Table1"));
            Property(scaled, "Block Table1").Value = (short)1;

            DxfBlockReference back = Inserts(Reopened(db))[1];
            Assert.Equal(400.0, (double)ValueOf(back, "Width"), Close);
            Assert.Equal(160.0, (double)ValueOf(back, "Height"), Close);
            Assert.Equal((short)1, ValueOf(back, "Block Table1"));
        }

        // ---- 3. refused ------------------------------------------------------------------------------------------------------

        [Fact]
        public void ATableOfARowNotOfOneValueForEachColumn_OrOfTheWrongKind_OrTwice_IsRefused()
        {
            Plate(
                false,
                out DxfBlockTableRecord block,
                out DxfBlockLinearParameter width,
                out DxfBlockLinearParameter height,
                out DxfAttributeDefinition mark
            );
            DxfBlockPropertiesTableColumn[] columns = Columns(width, height, mark);

            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(100.0, 50.0) }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(100.0, "50", "P") }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(100.0, 50.0, 1.0) }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(0.0, 50.0, "P") }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[]
                    {
                        new DxfBlockPropertiesTableRow(100.0, 50.0, "P"),
                        new DxfBlockPropertiesTableRow(100.0, 50.0, "P"),
                    }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    new[] { columns[0], columns[0] },
                    new[] { new DxfBlockPropertiesTableRow(100.0, 100.0) }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(new GeoPoint3(0, 0, 0), columns, new DxfBlockPropertiesTableRow[0])
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(100.0, null, "P") }
                )
            );
            Assert.Throws<ArgumentNullException>(() => new DxfBlockPropertiesTableRow(null));
            Assert.Throws<ArgumentException>(() => new DxfBlockPropertiesTableRow(100, 50.0));
            Assert.DoesNotContain(Nodes(block), n => n.Type == "BLOCKPROPERTIESTABLE");
        }

        [Fact]
        public void ASecondTable_IsRefused_AsAutoCadGivesABlockOne()
        {
            Plate(
                true,
                out DxfBlockTableRecord block,
                out DxfBlockLinearParameter width,
                out DxfBlockLinearParameter height,
                out DxfAttributeDefinition mark
            );

            Assert.Throws<InvalidOperationException>(() =>
                block.AddBlockPropertiesTable(new GeoPoint3(0, 0, 0), Columns(width, height, mark), Rows())
            );
        }

        [Fact]
        public void AConstantAttributeDefinition_OrOneOfAnotherBlock_IsRefusedAsAColumn()
        {
            DxfDatabase db = Plate(
                false,
                out DxfBlockTableRecord block,
                out DxfBlockLinearParameter width,
                out _,
                out DxfAttributeDefinition mark
            );
            mark.Constant = true;
            var other = new DxfBlockTableRecord { Name = "OTHER", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(other);
            DxfAttributeDefinition elsewhere = Add(
                other,
                new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), "A", "TAG", "Tag", null)
            );

            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    new[]
                    {
                        DxfBlockPropertiesTableColumn.Distance(width),
                        DxfBlockPropertiesTableColumn.Attribute(mark),
                    },
                    new[] { new DxfBlockPropertiesTableRow(100.0, "A") }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    new[]
                    {
                        DxfBlockPropertiesTableColumn.Distance(width),
                        DxfBlockPropertiesTableColumn.Attribute(elsewhere),
                    },
                    new[] { new DxfBlockPropertiesTableRow(100.0, "A") }
                )
            );
        }

        // ---- 4. the history of grip edits --------------------------------------------------------------------------------

        [Fact]
        public void AnInsertOfNoHistory_IsGivenNone_AsAutoCadGaveNone()
        {
            DxfDatabase db = Plate();
            DxfBlockReference insert = Inserts(db)[0];
            Property(insert, "Block Table1").Value = (short)2;
            Property(insert, "Width").Value = 120.0;

            var cache = (DxfDBDictionary)
                ((DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation")).GetAt("AppDataCache");
            Assert.Equal(new[] { "ACAD_ENHANCEDBLOCKDATA" }, cache.Select(e => e.Key));
            Assert.DoesNotContain(((DxfDBDictionary)cache.GetAt("ACAD_ENHANCEDBLOCKDATA")).Select(e => e.Key), k =>
                k == "17"
            );
        }

        [Fact]
        public void AHistoryKept_IsGivenTheRowSetAndTheDistanceSet_AsAutoCadAddedThemToItsParkingSample()
        {
            DxfDatabase db = Plate();
            DxfBlockReference insert = Inserts(db)[0];
            Property(insert, "Width").Value = 100.0;
            var cache = (DxfDBDictionary)
                ((DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation")).GetAt("AppDataCache");
            cache.Add("ACAD_ENHANCEDBLOCKHISTORY", "XRECORD", b =>
            {
                b.Group(100, "AcDbXrecord");
                b.Group(280, "1");
                b.Group(1070, "3");
            });

            Property(insert, "Block Table1").Value = (short)2;
            Property(insert, "Width").Value = 120.0;

            var history = (DxfXrecord)
                (
                    (DxfDBDictionary)
                        (
                            (DxfDBDictionary)
                                Inserts(Reopened(db))[0].ExtensionDictionary.GetAt("AcDbBlockRepresentation")
                        ).GetAt("AppDataCache")
                ).GetAt("ACAD_ENHANCEDBLOCKHISTORY");
            Assert.Equal(new short[] { 1070, 1071, 300, 70, 1071, 300, 40 }, history.Data.Select(g => g.TypeCode));
            Assert.Equal(
                new[] { "3", "17", "Block Table1", "2", "1", "Width" },
                history.Data.Take(6).Select(g => g.Value.Trim())
            );
            Assert.Equal(120.0, double.Parse(history.Data[6].Value, CultureInfo.InvariantCulture));
        }
    }
}
