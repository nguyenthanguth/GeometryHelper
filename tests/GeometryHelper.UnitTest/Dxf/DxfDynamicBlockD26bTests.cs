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
    /// The dynamic-block leftovers of D26b, each as AutoCAD 2021 read or set it (plugin D26b, rounds a1 to a3, of the library's
    /// files edited where the library writes no such block, and of AutoCAD's own Dynamic Blocks samples): what makes a block
    /// dynamic, the block's own value in a list, read-only and visibility lookups, tables whose properties must match a row,
    /// the tolerance a row is read within, and a dimension that names no block in a dynamic block.
    /// </summary>
    public class DxfDynamicBlockD26bTests
    {
        private const double Close = 1E-9;

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

        private static double Distance(DxfBlockReference insert, string name) => (double)ValueOf(insert, name);

        private static short Row(DxfBlockReference insert) => (short)ValueOf(insert, "Block Table1");

        /// <summary>The objects of the graph's nodes of a block, in its order.</summary>
        private static List<DxfItem> Nodes(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            return graph
                .Item.Groups.Where(g => g.TypeCode == 360)
                .Select(g => graph.Item.File.ObjectItem(g.Value))
                .ToList();
        }

        /// <summary>The node of a type labelled so (its 305 or 301), or the first of the type for no label.</summary>
        private static DxfItem Node(DxfBlockTableRecord block, string type, string label = null) =>
            Nodes(block)
                .First(n =>
                    n.Type == type
                    && (
                        label == null || n.Groups.Any(g => (g.TypeCode == 305 || g.TypeCode == 301) && g.Value == label)
                    )
                );

        /// <summary>The id of a node (90 of AcDbEvalExpr).</summary>
        private static string IdOf(DxfItem node) => node.Groups.First(g => g.TypeCode == 90).Value.Trim();

        /// <summary>Rewrites the groups of a node by an edit of them all, as another writer may have written it.</summary>
        private static void Edit(DxfItem node, Action<List<KeyValuePair<int, string>>> edit)
        {
            DxfFile file = node.File;
            var groups = file.TailOf(node, 0).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            edit(groups);
            file.ReplaceTail(node, 0, groups);
        }

        /// <summary>Gives the value set of a parameter's node, the list after its count 175, the values given, in their order.</summary>
        private static void List(DxfItem node, params double[] values) => Edit(node, groups =>
            {
                int count = groups.FindIndex(g => g.Key == 175);
                int listed = int.Parse(groups[count].Value.Trim(), CultureInfo.InvariantCulture);
                groups.RemoveRange(count + 1, listed);
                groups[count] = new KeyValuePair<int, string>(
                    175,
                    values.Length.ToString(CultureInfo.InvariantCulture)
                );
                groups.InsertRange(
                    count + 1,
                    values.Select(v => new KeyValuePair<int, string>(
                        144,
                        v.ToString("R", CultureInfo.InvariantCulture)
                    ))
                );
            });

        /// <summary>Makes the block's properties table one its properties must match (its last 290), as AutoCAD's Block Editor sets it.</summary>
        private static void MustMatch(DxfBlockTableRecord block) => Edit(Node(block, "BLOCKPROPERTIESTABLE"), groups =>
            {
                int last = groups.FindLastIndex(g => g.Key == 93);
                Assert.Equal(290, groups[last + 1].Key);
                groups[last + 1] = new KeyValuePair<int, string>(290, "1");
            });

        /// <summary>A block 100 by 50 of a Width that stretches its right edge, and a Height its top, of the value sets given.</summary>
        private static DxfBlockTableRecord Rect(
            DxfDatabase db,
            string name,
            out DxfBlockLinearParameter width,
            out DxfBlockLinearParameter height,
            DxfBlockParameterValueSet widths = null,
            DxfBlockParameterValueSet heights = null
        )
        {
            var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine bottom = Add(block, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            DxfLine right = Add(block, new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)));
            DxfLine top = Add(block, new DxfLine(new GeoPoint3(100, 50, 0), new GeoPoint3(0, 50, 0)));
            DxfLine left = Add(block, new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)));
            width = block.AddLinearParameter(
                "Width",
                string.Empty,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -10,
                widths
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
                heights
            );
            block.AddStretchAction(
                height,
                new GeoPoint2(-5, 40),
                new GeoPoint2(105, 55),
                new DxfEntity[] { right, top, left }
            );
            return block;
        }

        private static DxfBlockReference Place(DxfDatabase db, DxfBlockTableRecord block)
        {
            var insert = new DxfBlockReference(new GeoPoint3(400 * Inserts(db).Count, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            return insert;
        }

        /// <summary>A block of Width and Height and a table of them of the rows given, its properties to match a row where asked; two INSERTs.</summary>
        private static DxfDatabase Tabled(bool mustMatch, params double[][] rows)
        {
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(
                db,
                "PLATE",
                out DxfBlockLinearParameter width,
                out DxfBlockLinearParameter height
            );
            block.AddBlockPropertiesTable(
                new GeoPoint3(0, -20, 0),
                new[] { DxfBlockPropertiesTableColumn.Distance(width), DxfBlockPropertiesTableColumn.Distance(height) },
                rows.Select(r => new DxfBlockPropertiesTableRow(r[0], r[1]))
            );

            if (mustMatch)
            {
                MustMatch(block);
            }

            Place(db, block);
            Place(db, block);
            return Reopened(db);
        }

        /// <summary>Sets a property of an INSERT and gives back its Width, Height and row.</summary>
        private static string Set(DxfBlockReference insert, string name, object value)
        {
            Property(insert, name).Value = value;
            return Distance(insert, "Width").ToString("0.######", CultureInfo.InvariantCulture)
                + " x "
                + Distance(insert, "Height").ToString("0.######", CultureInfo.InvariantCulture)
                + " row "
                + Row(insert).ToString(CultureInfo.InvariantCulture);
        }

        // ---- 1. what makes a block dynamic ---------------------------------------------------------------------------------------

        [Fact]
        public void ABlockOfAGuidButNoEvaluationGraph_IsNotDynamic_NorAreItsInserts_AsAutoCadReadItsBoreAndValveSamples()
        {
            // AutoCAD 2021 read Bore, PVI, Station and Valve of its samples, whose BLOCK_RECORDs keep AcDbDynamicBlockGUID over an
            // empty extension dictionary, as not dynamic, and so the library's DYNB once its graph was taken out (plugin D26b a1,
            // a2): the INSERT of an anonymous copy still names DYNB its dynamic block and the copy its anonymous one.
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(db, "DYNB", out _, out _);
            Place(db, block);
            Property(Place(db, block), "Width").Value = 150.0;
            Assert.NotNull(block.ExtensionDictionary.Remove("ACAD_ENHANCEDBLOCK"));
            DxfDatabase reopened = Reopened(db);
            List<DxfBlockReference> inserts = Inserts(reopened);

            Assert.NotNull(reopened.BlockTable["DYNB"].GetXDataForApplication("AcDbDynamicBlockGUID"));
            Assert.False(reopened.BlockTable["DYNB"].IsDynamicBlock);
            Assert.All(inserts, i => Assert.False(i.IsDynamicBlock));
            Assert.All(inserts, i => Assert.Empty(i.DynamicBlockReferencePropertyCollection));
            Assert.Equal("DYNB", inserts[1].DynamicBlockTableRecord.Name);
            Assert.StartsWith("*U", inserts[1].AnonymousBlockTableRecord.Name, StringComparison.Ordinal);
            Assert.Null(inserts[0].AnonymousBlockTableRecord);
        }

        [Fact]
        public void ABlockOfAnEvaluationGraphButNoGuid_IsDynamic_AndIsSet_AsAutoCadReadAndSetIt()
        {
            // AutoCAD 2021 read DYNC, whose AcDbDynamicBlockGUID was taken out and its graph kept, as dynamic, and set its Width
            // to 130 (plugin D26b a1).
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(db, "DYNC", out _, out _);
            Place(db, block);
            block.XData = new[] { new DxfTypedValue(1001, "AcDbDynamicBlockGUID") };
            DxfDatabase reopened = Reopened(db);
            DxfBlockReference insert = Inserts(reopened).Single();

            Assert.Null(reopened.BlockTable["DYNC"].GetXDataForApplication("AcDbDynamicBlockGUID"));
            Assert.True(reopened.BlockTable["DYNC"].IsDynamicBlock);
            Assert.True(insert.IsDynamicBlock);
            Assert.Equal(100.0, Distance(insert, "Width"), Close);
            Property(insert, "Width").Value = 130.0;
            Assert.Equal(130.0, Distance(Inserts(Reopened(reopened)).Single(), "Width"), Close);
        }

        // ---- 2. the block's own value in a list ----------------------------------------------------------------------------------

        /// <summary>The block AL of plugin D26b a1: Width of the list 50, 150, 200 written without its own 100, Height of 30, 50, 80.</summary>
        private static DxfDatabase Listed(out DxfBlockTableRecord block)
        {
            var db = new DxfDatabase("AC1024");
            block = Rect(
                db,
                "AL",
                out _,
                out _,
                DxfBlockParameterValueSet.List(new[] { 50.0, 100, 150, 200 }),
                DxfBlockParameterValueSet.List(new[] { 30.0, 50, 80 })
            );
            Place(db, block);
            Property(Place(db, block), "Height").Value = 80.0;
            List(Node(block, "BLOCKLINEARPARAMETER", "Width"), 50, 150, 200);
            List(Node(block, "BLOCKLINEARPARAMETER", "Height"), 30, 50);
            return Reopened(db);
        }

        [Fact]
        public void AListWrittenWithoutTheBlocksOwnValue_AllowsIt_InItsPlace_AsAutoCadReadItsDoorSizeOf750()
        {
            // AutoCAD 2021 gave Door Size of its Door sample, own 750, the allowed 600, 700, 750, 800 ... though its list lacks
            // 750, and AL's Width, own 100, 50, 100, 150, 200 (plugin D26b a1); the value an INSERT shows off its list is not
            // added: Height 80, taken out of the list, gave 30, 50.
            DxfDatabase db = Listed(out _);
            List<DxfBlockReference> inserts = Inserts(db);

            Assert.Equal(new object[] { 50.0, 100.0, 150.0, 200.0 }, Property(inserts[0], "Width").GetAllowedValues());
            Assert.Equal(80.0, Distance(inserts[1], "Height"), Close);
            Assert.Equal(new object[] { 30.0, 50.0 }, Property(inserts[1], "Height").GetAllowedValues());
        }

        [Theory]
        [InlineData(120.0, 100.0)]
        [InlineData(125.0, 100.0)]
        [InlineData(75.0, 100.0)]
        [InlineData(174.0, 150.0)]
        [InlineData(250.0, 200.0)]
        public void ADistanceSet_IsTakenToTheListWithTheBlocksOwnValue_AsAutoCadTookIt(double asked, double taken)
        {
            // Of AL's Width, own 100 and listed 50, 150, 200, AutoCAD 2021 took 120 and the ties 125 and 75 to 100, and 174 to
            // 150 (plugin D26b a1); of Door Size, own 750, 760, 725 and 775 to 750 and 650 to 600.
            DxfBlockReference insert = Inserts(Listed(out _))[0];
            Property(insert, "Width").Value = asked;
            Assert.Equal(taken, Distance(insert, "Width"), Close);
        }

        [Fact]
        public void AnInsertsValueOffTheList_IsNotTakenAsOneOfIt_AsAutoCadTookHeight70To50()
        {
            DxfBlockReference insert = Inserts(Listed(out _))[1];
            Property(insert, "Height").Value = 70.0;
            Assert.Equal(50.0, Distance(insert, "Height"), Close);
        }

        [Fact]
        public void TheBlocksOwnValue_GoesAfterTheLastValueBelowIt_OfAListWrittenDown_AsAutoCadPlacedIt()
        {
            // A list written 300, 200, 50, own 100: AutoCAD 2021 gave 300, 200, 50, 100, and took 120, 150 and 75 to 100 and 250
            // to 300, the one written first (plugin D26b a1).
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(
                db,
                "AL2",
                out _,
                out _,
                DxfBlockParameterValueSet.List(new[] { 50.0, 100, 200, 300 })
            );
            Place(db, block);
            List(Node(block, "BLOCKLINEARPARAMETER", "Width"), 300, 200, 50);
            DxfBlockReference insert = Inserts(Reopened(db))[0];

            Assert.Equal(new object[] { 300.0, 200.0, 50.0, 100.0 }, Property(insert, "Width").GetAllowedValues());

            foreach (
                double[] pair in new[]
                {
                    new[] { 120.0, 100 },
                    new[] { 150.0, 100 },
                    new[] { 75.0, 100 },
                    new[] { 250.0, 300 },
                }
            )
            {
                Property(insert, "Width").Value = pair[0];
                Assert.Equal(pair[1], Distance(insert, "Width"), Close);
            }
        }

        [Fact]
        public void AnAngleListWrittenWithoutTheBlocksOwnAngle_AllowsIt_AndTakesAnAngleToIt_AsAutoCadDid()
        {
            // Turn, own 0, of the list 90 and 180 degrees: AutoCAD 2021 gave 0, 90, 180 and took 0.3 and 315 degrees to 0 and 2.5
            // to 180 degrees (plugin D26b a1, a2).
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(db, "TURN", out _, out _);
            DxfCircle needle = Add(block, new DxfCircle(new GeoPoint3(80, 25, 0), GeoVector3.ZAxis, 5));
            DxfBlockRotationParameter turn = block.AddRotationParameter(
                "Turn",
                null,
                new GeoPoint3(50, 25, 0),
                30,
                0,
                DxfBlockParameterValueSet.List(new[] { 0.0, Math.PI / 2, Math.PI })
            );
            block.AddRotateAction(turn, new[] { needle });
            Place(db, block);
            List(Node(block, "BLOCKROTATIONPARAMETER", "Turn"), Math.PI / 2, Math.PI);
            DxfBlockReference insert = Inserts(Reopened(db))[0];

            Assert.Equal(new object[] { 0.0, Math.PI / 2, Math.PI }, Property(insert, "Turn").GetAllowedValues());

            foreach (
                double[] pair in new[] { new[] { 2.5, Math.PI }, new[] { 0.3, 0.0 }, new[] { 1.75 * Math.PI, 0.0 } }
            )
            {
                Property(insert, "Turn").Value = pair[0];
                Assert.Equal(pair[1], (double)ValueOf(insert, "Turn"), Close);
            }
        }

        // ---- 3. lookups ----------------------------------------------------------------------------------------------------------

        /// <summary>The block LK of plugin D26b a1: Width and the lookup Size of the rows Small at 100 and Large at 200.</summary>
        private static DxfDatabase Looked(out DxfBlockTableRecord block, out DxfBlockLinearParameter width)
        {
            var db = new DxfDatabase("AC1024");
            block = Rect(db, "LK", out width, out _);
            block.AddLookupParameter(
                "Size",
                null,
                new GeoPoint3(50, -20, 0),
                new[] { DxfBlockLookupInput.Distance(width) },
                new[] { new DxfBlockLookupRow("Small", 100), new DxfBlockLookupRow("Large", 200) }
            );
            Place(db, block);
            Property(Place(db, block), "Width").Value = 150.0;
            return db;
        }

        [Fact]
        public void AReadOnlyLookup_AllowsItsLabelOfNoMatchLast_MatchedOrNot_AsAutoCadReadItsRollerBearing()
        {
            // AutoCAD 2021 gave each read-only lookup of its samples (Bore Tolerance, Ball Count, Static Load ... of Roller
            // Bearing, Shoulder Diameter of Shoulder Screw) its label of no match, Custom, last of the values allowed, and so LK's
            // Size once its column looked back no more (plugin D26b a1): Small, Large, Custom at 100 and at 150.
            DxfDatabase db = Looked(out DxfBlockTableRecord block, out _);
            Edit(Node(block, "BLOCKLOOKUPACTION"), groups =>
            {
                int reverse = groups.FindLastIndex(g => g.Key == 281);
                groups[reverse] = new KeyValuePair<int, string>(281, "0");
            });
            List<DxfBlockReference> inserts = Inserts(Reopened(db));

            Assert.True(Property(inserts[0], "Size").ReadOnly);
            Assert.Equal("Small", ValueOf(inserts[0], "Size"));
            Assert.Equal(new object[] { "Small", "Large", "Custom" }, Property(inserts[0], "Size").GetAllowedValues());
            Assert.Equal("Custom", ValueOf(inserts[1], "Size"));
            Assert.Equal(new object[] { "Small", "Large", "Custom" }, Property(inserts[1], "Size").GetAllowedValues());
        }

        [Fact]
        public void ALookupOfAVisibilityState_ReadsTheRowOfTheStateShown_AsAutoCadReadPartNumberOfItsShoulderScrew()
        {
            // AutoCAD 2021 read Part Number SSI-4 and Shoulder Diameter 0.1245 of its Shoulder Screw, whose lookup looks up from
            // the length and the visibility state, where the library read Invalid Size and Custom (plugin D26 r5, D26b): the
            // state shown, B, the first, at Width 100, is the row Big's, the second.
            DxfDatabase db = Looked(out DxfBlockTableRecord block, out _);
            DxfCircle dot = Add(block, new DxfCircle(new GeoPoint3(20, 20, 0), GeoVector3.ZAxis, 2));
            block.AddVisibilityParameter(
                "Look",
                null,
                new GeoPoint3(0, 60, 0),
                new[]
                {
                    new DxfBlockVisibilityState("B", new DxfEntity[] { dot }),
                    new DxfBlockVisibilityState("A", new DxfEntity[0]),
                }
            );
            string visibility = IdOf(Node(block, "BLOCKVISIBILITYPARAMETER"));
            Edit(Node(block, "BLOCKLOOKUPACTION"), groups =>
            {
                // The rows (100, A, Small), (100, B, Big), (200, A, Large); the state's column after the width's.
                int rows = groups.FindIndex(g => g.Key == 92);
                groups[rows] = new KeyValuePair<int, string>(92, "3");
                groups[rows + 1] = new KeyValuePair<int, string>(93, "3");
                int cells = groups.FindIndex(g => g.Key == 302);
                groups.RemoveAll(g => g.Key == 302);
                groups.InsertRange(
                    cells,
                    new[] { "100", "A", "Small", "100", "B", "Big", "200", "A", "Large" }.Select(c => new KeyValuePair<
                        int,
                        string
                    >(302, c))
                );
                int label = groups.FindLastIndex(g => g.Key == 303);
                groups.InsertRange(
                    label,
                    new[]
                    {
                        new KeyValuePair<int, string>(303, string.Empty),
                        new KeyValuePair<int, string>(94, visibility),
                        new KeyValuePair<int, string>(95, "1"),
                        new KeyValuePair<int, string>(96, "0"),
                        new KeyValuePair<int, string>(282, "0"),
                        new KeyValuePair<int, string>(305, "Custom"),
                        new KeyValuePair<int, string>(281, "0"),
                        new KeyValuePair<int, string>(304, "VisibilityState"),
                    }
                );
            });
            DxfBlockReference insert = Inserts(Reopened(db))[0];

            Assert.Equal("B", ValueOf(insert, "Look"));
            Assert.Equal("Big", ValueOf(insert, "Size"));
            Assert.Equal(new object[] { "Small", "Big", "Large" }, Property(insert, "Size").GetAllowedValues());
        }

        // ---- 4. tables whose properties must match a row -------------------------------------------------------------------------

        [Fact]
        public void ADistanceSetInATableToMatch_TakesTheRowOfIt_OrSetsItAloneOverTheFirstRow_AsAutoCadSetTheLibrarysTable()
        {
            // The rows 100 x 50, 200 x 80 and 150 x 60, each column of no value twice: AutoCAD 2021 took Width 200 to the row 1,
            // Width 120, of no row, beside the first row's Height 50, Height 80 to the row 1 again, and Width 100 and 150 to the
            // rows 0 and 2 (plugin D26b a1).
            DxfBlockReference insert = Inserts(
                Tabled(true, new[] { 100.0, 50 }, new[] { 200.0, 80 }, new[] { 150.0, 60 })
            )[0];

            Assert.Equal("200 x 80 row 1", Set(insert, "Width", 200.0));
            Assert.Equal("120 x 50 row -1", Set(insert, "Width", 120.0));
            Assert.Equal("200 x 80 row 1", Set(insert, "Height", 80.0));
            Assert.Equal("100 x 50 row 0", Set(insert, "Width", 100.0));
            Assert.Equal("150 x 60 row 2", Set(insert, "Width", 150.0));
        }

        [Fact]
        public void AColumnOfAValueTwice_TakesTheRowOfTheValueOverTheFirstRow_ElseSetsItAlone_AsAutoCadDid()
        {
            // The rows 100 x 50, 200 x 80, 200 x 50 and 150 x 60, whose Width holds 200 twice and Height 50 twice: AutoCAD 2021
            // took Width 200 to 200 x 50, the row 2 (the first row's Height), Height 50 back to the row 0, and set Height 60 and
            // 80 and Width 150 alone over the first row, though a row holds each (plugin D26b a2).
            List<DxfBlockReference> inserts = Inserts(
                Tabled(true, new[] { 100.0, 50 }, new[] { 200.0, 80 }, new[] { 200.0, 50 }, new[] { 150.0, 60 })
            );

            Assert.Equal("200 x 50 row 2", Set(inserts[0], "Width", 200.0));
            Assert.Equal("100 x 50 row 0", Set(inserts[0], "Height", 50.0));
            Assert.Equal("100 x 60 row -1", Set(inserts[0], "Height", 60.0));
            Assert.Equal("100 x 80 row -1", Set(inserts[0], "Height", 80.0));
            Assert.Equal("150 x 50 row -1", Set(inserts[1], "Width", 150.0));
            Assert.Equal("100 x 55 row -1", Set(inserts[1], "Height", 55.0));
        }

        [Fact]
        public void ATableToMatchWhoseFirstRowIsNotTheBlocksOwn_SetsAValueOfNoRowOverTheFirstRow_AsAutoCadDid()
        {
            // The rows 200 x 80, 150 x 60 and 300 x 90 of a block of its own 100 x 50: AutoCAD 2021 set Width 250 beside Height
            // 80, Height 60 to the row 1, Width 400 beside 80 and Height 70 beside Width 200 (plugin D26b a3).
            DxfBlockReference insert = Inserts(
                Tabled(true, new[] { 200.0, 80 }, new[] { 150.0, 60 }, new[] { 300.0, 90 })
            )[0];

            Assert.Equal("250 x 80 row -1", Set(insert, "Width", 250.0));
            Assert.Equal("150 x 60 row 1", Set(insert, "Height", 60.0));
            Assert.Equal("400 x 80 row -1", Set(insert, "Width", 400.0));
            Assert.Equal("200 x 70 row -1", Set(insert, "Height", 70.0));
            Assert.Equal("300 x 90 row 2", Set(insert, "Width", 300.0));
        }

        [Fact]
        public void ATableToMatch_TakesAValueToItsValueSetFirst_AsAutoCadTookWidth180To200AndItsRow()
        {
            // Width of the list 100, 150, 200, 250 and the rows 100 x 50, 200 x 80 and 250 x 60: AutoCAD 2021 took 180 to 200 and
            // the row 1, set 150 beside Height 50, took 240 to 250 and the row 2 (plugin D26b a2).
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(
                db,
                "LISTED",
                out DxfBlockLinearParameter width,
                out DxfBlockLinearParameter height,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200, 250 })
            );
            block.AddBlockPropertiesTable(
                new GeoPoint3(0, -20, 0),
                new[] { DxfBlockPropertiesTableColumn.Distance(width), DxfBlockPropertiesTableColumn.Distance(height) },
                new[]
                {
                    new DxfBlockPropertiesTableRow(100.0, 50.0),
                    new DxfBlockPropertiesTableRow(200.0, 80.0),
                    new DxfBlockPropertiesTableRow(250.0, 60.0),
                }
            );
            MustMatch(block);
            Place(db, block);
            DxfBlockReference insert = Inserts(Reopened(db))[0];

            Assert.Equal("200 x 80 row 1", Set(insert, "Width", 180.0));
            Assert.Equal("150 x 50 row -1", Set(insert, "Width", 150.0));
            Assert.Equal("250 x 60 row 2", Set(insert, "Width", 240.0));
            Assert.Equal("200 x 80 row 1", Set(insert, "Height", 80.0));
            Assert.Equal("100 x 70 row -1", Set(insert, "Height", 70.0));
        }

        [Fact]
        public void ATableToMatch_LeavesTheAttributeOfItsColumn_WhenADistanceTakesARow_AsAutoCadLeftMark()
        {
            // AutoCAD 2021 left MARK P100 when Width 200 took the row of P250 (plugin D26b a2); a row set sets it, as before.
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(
                db,
                "PLATE",
                out DxfBlockLinearParameter width,
                out DxfBlockLinearParameter height
            );
            DxfAttributeDefinition mark = Add(
                block,
                new DxfAttributeDefinition(new GeoPoint3(10, 10, 0), "P100", "MARK", "Mark", null) { Height = 5 }
            );
            block.AddBlockPropertiesTable(
                new GeoPoint3(0, -20, 0),
                new[]
                {
                    DxfBlockPropertiesTableColumn.Distance(width),
                    DxfBlockPropertiesTableColumn.Distance(height),
                    DxfBlockPropertiesTableColumn.Attribute(mark),
                },
                new[]
                {
                    new DxfBlockPropertiesTableRow(100.0, 50.0, "P100"),
                    new DxfBlockPropertiesTableRow(200.0, 80.0, "P200"),
                    new DxfBlockPropertiesTableRow(200.0, 50.0, "P250"),
                }
            );
            MustMatch(block);
            DxfBlockReference placed = Place(db, block);
            placed.AttributeCollection.AppendAttribute(
                new DxfAttributeReference(new GeoPoint3(10, 10, 0), "P100", "MARK", null) { Height = 5 }
            );
            DxfBlockReference insert = Inserts(Reopened(db))[0];

            Assert.Equal("200 x 50 row 2", Set(insert, "Width", 200.0));
            Assert.Equal("P100", insert.AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);
            Property(insert, "Block Table1").Value = (short)1;
            Assert.Equal("P200", insert.AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);
        }

        // ---- 5. the tolerance a row is read within -------------------------------------------------------------------------------

        [Theory]
        [InlineData(200.0000002, -1)]
        [InlineData(200.000001, -1)]
        [InlineData(200.0001, -1)]
        [InlineData(199.9999, -1)]
        [InlineData(199.9999999, 1)]
        [InlineData(200.00000001, 1)]
        [InlineData(200.0, 1)]
        public void ARowIsReadWithinATenMillionth_AsAutoCadReadIt(double width, int row)
        {
            // AutoCAD 2021 read Block Table1 -1 at Width 200.0000002, 200.000001, 200.0001 and 199.9999 beside Height 80, and the
            // row 1 at 199.9999999, 200.00000001 and 200 (plugin D26b a3 to a5); the library read the row within 1E-3.
            DxfBlockReference insert = Inserts(Tabled(false, new[] { 100.0, 50 }, new[] { 200.0, 80 }))[0];
            Property(insert, "Height").Value = 80.0;
            Property(insert, "Width").Value = width;

            Assert.Equal(row, Row(insert));
        }

        // ---- 6. columns of the visibility state, of an angle; empty cells ------------------------------------------------------

        /// <summary>
        /// The block VIS of plugin D26b a6: Width, the rotation Turn, the visibility Look of the states A and B, the attribute
        /// MARK; the table of the rows (100, A, 0, P100) and (200, B, pi / 2, an empty cell); an INSERT of an ATTRIB MARK X1.
        /// </summary>
        private static DxfDatabase Shown(out DxfBlockTableRecord block)
        {
            var db = new DxfDatabase("AC1024");
            block = Rect(db, "VIS", out DxfBlockLinearParameter width, out _);
            DxfCircle dot = Add(block, new DxfCircle(new GeoPoint3(20, 20, 0), GeoVector3.ZAxis, 3));
            DxfCircle needle = Add(block, new DxfCircle(new GeoPoint3(70, 25, 0), GeoVector3.ZAxis, 3));
            DxfBlockRotationParameter turn = block.AddRotationParameter("Turn", null, new GeoPoint3(50, 25, 0), 20, 0);
            block.AddRotateAction(turn, new[] { needle });
            block.AddVisibilityParameter(
                "Look",
                null,
                new GeoPoint3(0, 60, 0),
                new[]
                {
                    new DxfBlockVisibilityState("A", new DxfEntity[] { dot }),
                    new DxfBlockVisibilityState("B", new DxfEntity[0]),
                }
            );
            DxfAttributeDefinition mark = Add(
                block,
                new DxfAttributeDefinition(new GeoPoint3(10, 10, 0), "P100", "MARK", "Mark", null) { Height = 5 }
            );
            block.AddBlockPropertiesTable(
                new GeoPoint3(0, -30, 0),
                new[]
                {
                    DxfBlockPropertiesTableColumn.Distance(width),
                    DxfBlockPropertiesTableColumn.Visibility(block),
                    DxfBlockPropertiesTableColumn.Angle(turn),
                    DxfBlockPropertiesTableColumn.Attribute(mark),
                },
                new[]
                {
                    new DxfBlockPropertiesTableRow(100.0, "A", 0.0, "P100"),
                    new DxfBlockPropertiesTableRow(200.0, "B", Math.PI / 2, null),
                }
            );
            DxfBlockReference insert = Place(db, block);
            insert.AttributeCollection.AppendAttribute(
                new DxfAttributeReference(new GeoPoint3(10, 10, 0), "X1", "MARK", null) { Height = 5 }
            );
            return db;
        }

        [Fact]
        public void AColumnOfTheVisibilityState_IsWrittenAsAutoCadsAddItemWroteIt_AndAnEmptyCellAndAnAngleColumnSo()
        {
            // AutoCAD 2021's AddItem gave a visibility parameter the column of the connection VisibilityState, of text cells, and
            // a rotation parameter one flagged 293 of 1 (plugin D26b a5); an empty cell is of the type -9999.
            Shown(out DxfBlockTableRecord block);
            DxfItem table = Node(block, "BLOCKPROPERTIESTABLE");
            List<DxfTypedValue> own = table
                .Groups.SkipWhile(g => !(g.TypeCode == 100 && g.Value == "AcDbBlockPropertiesTable"))
                .Skip(1)
                .ToList();

            Assert.Equal(
                new[] { "UpdatedDistance", "VisibilityState", "UpdatedAngle", string.Empty },
                own.Where(g => g.TypeCode == 301).Skip(1).Select(g => g.Value.Trim())
            );
            Assert.Equal(
                new[] { "0", "0", "1", "0" },
                own.Where(g => g.TypeCode == 293).Take(4).Select(g => g.Value.Trim())
            );
            Assert.Equal(new[] { "A", "P100", "B" }, own.Where(g => g.TypeCode == 300).Skip(5).Select(g => g.Value));
            Assert.Equal("-9999", own[own.FindLastIndex(g => g.TypeCode == 93) - 1].Value.Trim());
            Assert.Contains(
                table.Handle,
                Node(block, "BLOCKVISIBILITYPARAMETER").Groups.Where(g => g.TypeCode == 330).Select(g => g.Value.Trim())
            );
        }

        [Fact]
        public void ARowOfTheVisibilityState_ShowsItsState_AndTheStateIsMatched_AsAutoCadSetAndReadTheLibrarysTable()
        {
            // AutoCAD 2021 read the library's VIS (plugin D26b a6, 0 lines apart): the row 1 gave Width 200, Look B, Turn 90
            // degrees and MARK an empty text; Look A then read no row; Width 100 and Turn 0 the row 0 again, which gave MARK P100.
            DxfBlockReference insert = Inserts(Reopened(Shown(out _)))[0];
            DxfAttributeReference mark = insert.AttributeCollection.Cast<DxfAttributeReference>().Single();

            Property(insert, "Block Table1").Value = (short)1;
            Assert.Equal(200.0, Distance(insert, "Width"), Close);
            Assert.Equal("B", ValueOf(insert, "Look"));
            Assert.Equal(Math.PI / 2, (double)ValueOf(insert, "Turn"), Close);
            Assert.Equal(string.Empty, mark.TextString);
            Property(insert, "Look").Value = "A";
            Assert.Equal(-1, Row(insert));
            Property(insert, "Width").Value = 100.0;
            Property(insert, "Turn").Value = 0.0;
            Assert.Equal(0, Row(insert));
            Property(insert, "Block Table1").Value = (short)0;
            Assert.Equal("P100", mark.TextString);
            Assert.Equal(
                new[] { "Width", "Look", "Turn", "MARK" },
                insert.DynamicBlockTableRecord.BlockPropertiesTable.ColumnNames
            );
            Assert.Null(insert.DynamicBlockTableRecord.BlockPropertiesTable.Rows[1].Values[3]);
        }

        [Fact]
        public void AnEmptyCellOfAParameter_OrAStateOfNoName_IsRefused_AsAutoCadsAuditRefusedAnEmptyCell()
        {
            // AutoCAD 2021's audit refused a table of an empty cell of Height (eNotApplicable) and took Block Table1 away once a
            // row was set; it found no fault in an empty cell of MARK (plugin D26b a4).
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(db, "VIS", out DxfBlockLinearParameter width, out _);
            DxfCircle dot = Add(block, new DxfCircle(new GeoPoint3(20, 20, 0), GeoVector3.ZAxis, 3));
            block.AddVisibilityParameter(
                "Look",
                null,
                new GeoPoint3(0, 60, 0),
                new[]
                {
                    new DxfBlockVisibilityState("A", new DxfEntity[] { dot }),
                    new DxfBlockVisibilityState("B", new DxfEntity[0]),
                }
            );
            DxfBlockPropertiesTableColumn[] columns =
            {
                DxfBlockPropertiesTableColumn.Distance(width),
                DxfBlockPropertiesTableColumn.Visibility(block),
            };

            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(null, "A") }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(100.0, "C") }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(100.0, null) }
                )
            );
            Assert.Throws<ArgumentException>(() =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, 0, 0),
                    columns,
                    new[] { new DxfBlockPropertiesTableRow(100.0, 1.0) }
                )
            );
            Assert.DoesNotContain(Nodes(block), n => n.Type == "BLOCKPROPERTIESTABLE");

            var plain = new DxfDatabase("AC1024");
            Assert.Throws<ArgumentException>(() =>
                DxfBlockPropertiesTableColumn.Visibility(Rect(plain, "PLAIN", out _, out _))
            );
            Assert.Throws<ArgumentNullException>(() => DxfBlockPropertiesTableColumn.Visibility(null));
        }

        [Fact]
        public void AnEmptyCellOfAnAttribute_ReadFromATable_GivesTheAttributeAnEmptyText_AsAutoCadGaveMark()
        {
            // A table another writer made, whose MARK cell of the row 1 is empty: AutoCAD 2021 set the row and gave MARK an
            // empty text (plugin D26b a4), where the library left X1.
            var db = new DxfDatabase("AC1024");
            DxfBlockTableRecord block = Rect(
                db,
                "PLATE",
                out DxfBlockLinearParameter width,
                out DxfBlockLinearParameter height
            );
            DxfAttributeDefinition mark = Add(
                block,
                new DxfAttributeDefinition(new GeoPoint3(10, 10, 0), "P100", "MARK", "Mark", null) { Height = 5 }
            );
            block.AddBlockPropertiesTable(
                new GeoPoint3(0, -20, 0),
                new[]
                {
                    DxfBlockPropertiesTableColumn.Distance(width),
                    DxfBlockPropertiesTableColumn.Distance(height),
                    DxfBlockPropertiesTableColumn.Attribute(mark),
                },
                new[]
                {
                    new DxfBlockPropertiesTableRow(100.0, 50.0, "P100"),
                    new DxfBlockPropertiesTableRow(200.0, 80.0, "P200"),
                }
            );
            Edit(Node(block, "BLOCKPROPERTIESTABLE"), groups =>
            {
                int at = groups.FindIndex(g => g.Key == 300 && g.Value == "P200");
                groups.RemoveAt(at);
                groups[at - 1] = new KeyValuePair<int, string>(170, "-9999");
            });
            Place(db, block)
                .AttributeCollection.AppendAttribute(
                    new DxfAttributeReference(new GeoPoint3(10, 10, 0), "X1", "MARK", null) { Height = 5 }
                );
            DxfBlockReference insert = Inserts(Reopened(db))[0];

            Property(insert, "Block Table1").Value = (short)1;
            Assert.Equal(string.Empty, insert.AttributeCollection.Cast<DxfAttributeReference>().Single().TextString);
            Assert.Equal(1, Row(insert));
        }

        // ---- 6. a dimension that names no block ----------------------------------------------------------------------------------

        [Fact]
        public void ADynamicBlockHoldingADimensionOfNoBlock_IsSet_AndItsCopyNamesNone_AsAutoCadKeepsParkingsConstraints()
        {
            // AutoCAD's Parking sample holds dimensions of its constraints that name no block (no group 2), in the block and in
            // its anonymous copies alike; the library refused every set of its INSERT ("no block named by the dimension").
            var db = new DxfDatabase("AC1024");
            var picture = new DxfBlockTableRecord { Name = "DIMPIC", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(picture);
            DxfBlockTableRecord block = Rect(db, "PARK", out _, out _);
            var dimension = new DxfRotatedDimension(
                0.0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(50, -20, 0),
                string.Empty,
                null
            )
            {
                DimBlock = picture,
            };
            block.AppendEntity(dimension);
            Place(db, block);
            DxfFile file = dimension.Item.File;
            var groups = file.TailOf(dimension.Item, 0)
                .Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value))
                .ToList();
            Assert.Equal(1, groups.RemoveAll(g => g.Key == 2));
            file.ReplaceTail(dimension.Item, 0, groups);
            DxfDatabase reopened = Reopened(db);
            DxfBlockReference insert = Inserts(reopened)[0];

            Property(insert, "Width").Value = 150.0;
            DxfDatabase again = Reopened(reopened);
            DxfBlockReference set = Inserts(again)[0];
            Assert.Equal(150.0, Distance(set, "Width"), Close);
            DxfDimension copied = set.BlockTableRecord.OfType<DxfDimension>().Single();
            Assert.StartsWith("*U", set.BlockTableRecord.Name, StringComparison.Ordinal);
            Assert.Null(copied.DimBlock);

            // The copy is hidden, as AutoCAD keeps the dimensions of the constraints of its own copies and then read the
            // library's (plugin D26b a8); the block's own is left as it is.
            Assert.False(copied.Visible);
            Assert.True(again.BlockTable["PARK"].OfType<DxfDimension>().Single().Visible);
        }
    }
}
