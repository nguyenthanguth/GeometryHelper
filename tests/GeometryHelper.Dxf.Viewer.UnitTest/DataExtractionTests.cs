using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Data extraction as the presenter runs it: the object types and properties of a drawing, rows combined with their count,
    /// filtered and sorted, the settings kept in a .dxe file, the result written to .xlsx, .csv and .txt files, inserted as a
    /// table whose cells are linked to the extraction's data link, and updated by DATALINKUPDATE when the drawing changes; and
    /// the commands -DATAEXTRACTION and DATAEXTRACTION that run it.
    /// </summary>
    public sealed class DataExtractionTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gh-dx-" + Guid.NewGuid().ToString("N"));

        public DataExtractionTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        private string At(string name) => Path.Combine(_folder, name);

        private static DxfBlockTableRecord Block(DxfDatabase db, string name, params string[] tags)
        {
            var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5.0));
            double y = 0.0;

            foreach (string tag in tags)
            {
                block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, y, 0), string.Empty, tag, tag, null));
                y -= 4.0;
            }

            return block;
        }

        private static DxfBlockReference Insert(
            DxfDatabase db,
            DxfBlockTableRecord block,
            double x,
            params string[] values
        )
        {
            var insert = new DxfBlockReference(new GeoPoint3(x, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            int i = 0;

            foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>())
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                attribute.TextString = values[i++];
                insert.AttributeCollection.AppendAttribute(attribute);
            }

            return insert;
        }

        /// <summary>
        /// A drawing saved as parts.dxf in the temporary folder and opened: three lines (two alike, 100 long on layer 0, and one
        /// 50 long on STEEL), a circle of radius 10, ITEM blocks (NAME, PRICE) Bolt 1.5 twice and Nut 12, a PLATE block (NOTE)
        /// once, and a block PLAIN with no attribute that is defined and not inserted.
        /// </summary>
        private CadDocument Parts()
        {
            DxfDatabase db = CadTemplate.CreateMetric();
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "STEEL" });
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(50, 10, 0)) { Layer = "STEEL" }
            );
            db.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(200, 0, 0), new GeoVector3(0, 0, 1), 10.0));
            DxfBlockTableRecord item = Block(db, "ITEM", "NAME", "PRICE");
            DxfBlockTableRecord plate = Block(db, "PLATE", "NOTE");
            Block(db, "PLAIN");
            Insert(db, item, 10, "Bolt", "1.5");
            Insert(db, item, 20, "Bolt", "1.5");
            Insert(db, item, 30, "Nut", "12");
            Insert(db, plate, 40, "thin");
            string path = At("parts.dxf");
            db.Save(path);
            return CadDocument.Open(path);
        }

        private static CadExtractionSettings Items() =>
            new CadExtractionSettings
            {
                SelectedTypes = new List<string> { "ITEM" },
                SelectedProperties = new List<string> { "Attribute/NAME", "Attribute/PRICE" },
            };

        private static string[] Row(CadSheet sheet, int row) =>
            Enumerable.Range(0, sheet.ColumnCount).Select(c => sheet[row, c].Text).ToArray();

        [Fact]
        public void TheTypesOfADrawing_ListEveryKindAndEveryBlockWithHowManyThereAre_BlocksNotInsertedToo()
        {
            CadDocument document = Parts();

            IReadOnlyList<CadExtractionType> types = CadExtraction.Types(
                document,
                new CadExtractionSettings(),
                out IReadOnlyList<string> missing
            );

            Assert.Empty(missing);
            Assert.Equal(new[] { "Circle", "ITEM", "Line", "PLAIN", "PLATE" }, types.Select(t => t.Name).ToArray());
            Assert.Equal(new[] { 5, 3, 3, 0, 1 }, types.Select(t => t.Count).ToArray());
            Assert.True(types.Single(t => t.Name == "ITEM").IsBlock);
            Assert.True(types.Single(t => t.Name == "ITEM").HasAttributes);
            Assert.False(types.Single(t => t.Name == "PLAIN").HasAttributes);
            Assert.False(types.Single(t => t.Name == "Line").IsBlock);
        }

        [Fact]
        public void TheDisplayOptions_ListBlocksOnly_NonBlocksOnly_BlocksWithAttributesOnly_OrWhatIsInUse()
        {
            CadDocument document = Parts();

            string[] Names(CadExtractionSettings settings) =>
                CadExtraction.Types(document, settings, out _).Select(t => t.Name).ToArray();

            Assert.Equal(
                new[] { "ITEM", "PLAIN", "PLATE" },
                Names(new CadExtractionSettings { Display = CadExtractionDisplay.BlocksOnly })
            );
            Assert.Equal(
                new[] { "Circle", "Line" },
                Names(new CadExtractionSettings { Display = CadExtractionDisplay.NonBlocksOnly })
            );
            Assert.Equal(
                new[] { "ITEM", "PLATE" },
                Names(
                    new CadExtractionSettings
                    {
                        Display = CadExtractionDisplay.BlocksOnly,
                        BlocksWithAttributesOnly = true,
                    }
                )
            );
            Assert.Equal(
                new[] { "Circle", "ITEM", "Line", "PLATE" },
                Names(new CadExtractionSettings { UsedOnly = true })
            );
        }

        [Fact]
        public void TheProperties_AreByCategory_WithTheAttributesOfTheBlocksChosen_AndWithoutACategoryLeftOut()
        {
            CadDocument document = Parts();

            IReadOnlyList<CadExtractionProperty> properties = CadExtraction.Properties(
                document,
                new CadExtractionSettings
                {
                    SelectedTypes = new List<string> { "ITEM", "Line" },
                }
            );
            string[] keys = properties.Select(p => p.Key).ToArray();

            Assert.Contains("Attribute/NAME", keys);
            Assert.Contains("Attribute/PRICE", keys);
            Assert.DoesNotContain("Attribute/NOTE", keys);
            Assert.Contains("General/Layer", keys);
            Assert.Contains("Geometry/Length", keys);
            Assert.Contains("Geometry/Position X", keys);
            Assert.Contains("Drawing/File Name", keys);
            Assert.True(Array.IndexOf(keys, "Attribute/NAME") < Array.IndexOf(keys, "General/Layer"));
            Assert.True(Array.IndexOf(keys, "General/Layer") < Array.IndexOf(keys, "Geometry/Length"));

            var without = new CadExtractionSettings { SelectedTypes = new List<string> { "Line" } };
            without.UnselectedCategories.AddRange(new[] { "Drawing", "Misc", "Geometry" });
            Assert.All(CadExtraction.Properties(document, without), p => Assert.Equal("General", p.Category));
        }

        [Fact]
        public void IdenticalRows_AreCombinedWithTheirCount_InTheOrderTheyWereReadIn()
        {
            CadDocument document = Parts();

            CadExtractionResult result = CadExtraction.Run(document, Items());

            Assert.Equal(new[] { "Count", "Name", "Attribute/NAME", "Attribute/PRICE" }, result.Columns);
            Assert.Equal(new[] { "Count", "Name", "NAME", "PRICE" }, Row(result.Sheet, 0));
            Assert.Equal(3, result.Sheet.RowCount);
            Assert.Equal(new[] { "2", "ITEM", "Bolt", "1.5" }, Row(result.Sheet, 1));
            Assert.Equal(new[] { "1", "ITEM", "Nut", "12" }, Row(result.Sheet, 2));
            Assert.Equal(CadSheetCellKind.Number, result.Sheet[1, 0].Kind);
            Assert.Equal(3, result.Objects);
        }

        [Fact]
        public void WithoutCombining_EveryObjectIsARow_AndTheCountAndNameColumnsCanBeLeftOut()
        {
            CadDocument document = Parts();
            CadExtractionSettings settings = Items();
            settings.CombineIdenticalRows = false;
            settings.ShowCount = false;
            settings.ShowName = false;

            CadExtractionResult result = CadExtraction.Run(document, settings);

            Assert.Equal(new[] { "NAME", "PRICE" }, Row(result.Sheet, 0));
            Assert.Equal(4, result.Sheet.RowCount);
            Assert.Equal(new[] { "Bolt", "1.5" }, Row(result.Sheet, 1));
            Assert.Equal(new[] { "Bolt", "1.5" }, Row(result.Sheet, 2));
        }

        [Fact]
        public void TheLinesOfADrawing_ShowTheirGeometry_ToFourPlaces_AndAreSortedAndFiltered()
        {
            CadDocument document = Parts();
            var settings = new CadExtractionSettings
            {
                SelectedTypes = new List<string> { "Line" },
                SelectedProperties = new List<string> { "General/Layer", "Geometry/Length" },
            };
            settings.Sorts.Add(new CadExtractionSort("Geometry/Length", false));

            CadExtractionResult ascending = CadExtraction.Run(document, settings);

            Assert.Equal(new[] { "1", "Line", "STEEL", "50" }, Row(ascending.Sheet, 1));
            Assert.Equal(new[] { "2", "Line", "0", "100" }, Row(ascending.Sheet, 2));

            settings.Sorts.Clear();
            settings.Sorts.Add(new CadExtractionSort("Count", true));
            Assert.Equal("2", CadExtraction.Run(document, settings).Sheet[1, 0].Text);

            settings.Filters.Add(new CadExtractionFilter("Geometry/Length", CadFilterCondition.Less, "60"));
            CadExtractionResult filtered = CadExtraction.Run(document, settings);
            Assert.Equal(2, filtered.Sheet.RowCount);
            Assert.Equal("50", filtered.Sheet[1, 3].Text);

            settings.Filters.Clear();
            settings.Filters.Add(new CadExtractionFilter("Geometry/Length", CadFilterCondition.Between, "40", "100"));
            Assert.Equal(3, CadExtraction.Run(document, settings).Sheet.RowCount);
            settings.Filters.Clear();
            settings.Filters.Add(new CadExtractionFilter("General/Layer", CadFilterCondition.BeginsWith, "st"));
            Assert.Equal(2, CadExtraction.Run(document, settings).Sheet.RowCount);
            settings.Filters.Clear();
            settings.Filters.Add(new CadExtractionFilter("Name", CadFilterCondition.NotEqual, "line"));
            Assert.Equal(1, CadExtraction.Run(document, settings).Sheet.RowCount);
        }

        [Fact]
        public void OnlyTheObjectsSelected_AreExtracted_AndAColumnRenamedIsHeadedWithItsNewName()
        {
            CadDocument document = Parts();
            DxfBlockReference nut = document
                .Database.ModelSpace.OfType<DxfBlockReference>()
                .First(b => b.AttributeCollection.Any(a => a.TextString == "Nut"));
            CadExtractionSettings settings = Items();
            settings.SelectedHandles = new List<string> { nut.Handle };
            settings.ColumnNames["Attribute/PRICE"] = "Cost";
            settings.ColumnNames["Count"] = "Qty";

            CadExtractionResult result = CadExtraction.Run(document, settings);

            Assert.Equal(new[] { "Qty", "Name", "NAME", "Cost" }, Row(result.Sheet, 0));
            Assert.Equal(2, result.Sheet.RowCount);
            Assert.Equal(new[] { "1", "ITEM", "Nut", "12" }, Row(result.Sheet, 1));

            settings.UsePropertyNamesAsHeaders = true;
            Assert.Equal("Cost\nPRICE", CadExtraction.Run(document, settings).Sheet[0, 3].Text);
        }

        [Fact]
        public void TheColumnOrderOfTheSettings_IsKept_AndColumnsLeftOutOfItFollow()
        {
            CadDocument document = Parts();
            CadExtractionSettings settings = Items();
            settings.ColumnOrder = new List<string> { "Attribute/PRICE", "Count" };

            Assert.Equal(
                new[] { "PRICE", "Count", "Name", "NAME" },
                Row(CadExtraction.Run(document, settings).Sheet, 0)
            );
        }

        [Fact]
        public void OtherDrawingFiles_AreSourcesToo_AndAFileThatCannotBeOpenedIsToldAndSkipped()
        {
            CadDocument document = Parts();
            CadExtractionSettings settings = Items();
            settings.Files.Add(At("parts.dxf"));
            settings.Files.Add(At("gone.dxf"));
            File.WriteAllText(At("bad.dxf"), "not a drawing");
            settings.Files.Add(At("bad.dxf"));

            CadExtractionResult result = CadExtraction.Run(document, settings);

            Assert.Equal(new[] { "4", "ITEM", "Bolt", "1.5" }, Row(result.Sheet, 1));
            Assert.Equal(new[] { At("gone.dxf"), At("bad.dxf") }, result.MissingFiles);

            settings.IncludeCurrentDrawing = false;
            Assert.Equal("2", CadExtraction.Run(document, settings).Sheet[1, 0].Text);

            Directory.CreateDirectory(At("more"));
            File.Copy(At("parts.dxf"), At("more\\a.dxf"));
            File.Copy(At("parts.dxf"), At("more\\b.dxf"));
            var folder = new CadExtractionSettings
            {
                SelectedTypes = new List<string> { "Circle" },
                SelectedProperties = new List<string> { "General/Layer" },
                IncludeCurrentDrawing = false,
            };
            folder.Folders.Add(At("more"));
            Assert.Equal("10", CadExtraction.Run(document, folder).Sheet[1, 0].Text);
        }

        [Fact]
        public void TheSettings_AreKeptInADxeFile_AndReadBackWhole()
        {
            var settings = new CadExtractionSettings
            {
                IncludeCurrentDrawing = false,
                SelectedHandles = new List<string> { "1A", "2B" },
                ExtractFromBlocks = false,
                ModelSpaceOnly = true,
                Display = CadExtractionDisplay.BlocksOnly,
                BlocksWithAttributesOnly = true,
                SelectedTypes = new List<string> { "ITEM", "Line <1>" },
                SelectedProperties = new List<string> { "Attribute/NAME", "General/Layer" },
                CombineIdenticalRows = false,
                ShowCount = false,
                ColumnOrder = new List<string> { "Name", "General/Layer" },
                OutputToFile = true,
                OutputFile = @"C:\out\parts.xlsx",
                TableStyle = "Parts",
                Title = "Parts & \"bolts\"",
                UsePropertyNamesAsHeaders = true,
            };
            settings.Files.Add(@"C:\a\b.dwg");
            settings.Folders.Add(@"C:\a");
            settings.UnselectedCategories.Add("Misc");
            settings.ColumnNames["Attribute/NAME"] = "Part";
            settings.ColumnNames["Count"] = "Qty";
            settings.Sorts.Add(new CadExtractionSort("Name", true));
            settings.Filters.Add(new CadExtractionFilter("General/Layer", CadFilterCondition.Between, "A", "M"));

            string path = At("settings.dxe");
            settings.Save(path);
            CadExtractionSettings back = CadExtractionSettings.Load(path);

            Assert.Equal(settings.ToXml().ToString(), back.ToXml().ToString());
            Assert.Equal(new[] { "1A", "2B" }, back.SelectedHandles);
            Assert.False(back.IncludeCurrentDrawing);
            Assert.Equal("Part", back.ColumnNames["Attribute/NAME"]);
            Assert.Equal("Parts & \"bolts\"", back.Title);
            Assert.Equal(CadFilterCondition.Between, back.Filters.Single().Condition);
            Assert.Equal("M", back.Filters.Single().Second);
            Assert.False(File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.Equal(settings.ToXml().ToString(), settings.Clone().ToXml().ToString());
        }

        [Fact]
        public void ADxeThatIsNotThePresentersOwn_IsRefusedInWords()
        {
            File.WriteAllBytes(At("acad.dxe"), new byte[] { 0, 1, 0, 0, 0, 255, 255, 255, 255 });
            File.WriteAllText(At("other.dxe"), "<Something/>");

            Assert.Contains(
                "not a data extraction file",
                Assert.Throws<InvalidDataException>(() => CadExtractionSettings.Load(At("acad.dxe"))).Message
            );
            Assert.Contains(
                "a .dxe that AutoCAD wrote is not read",
                Assert.Throws<InvalidDataException>(() => CadExtractionSettings.Load(At("other.dxe"))).Message
            );
        }

        [Fact]
        public void TheResult_IsWrittenToAnXlsxFileAWorkbookReadsBack_ACsvFile_AndATabSeparatedFile()
        {
            CadDocument document = Parts();
            CadExtractionResult result = CadExtraction.Run(document, Items());

            CadExtraction.WriteFile(At("out.xlsx"), result.Sheet);
            CadExtraction.WriteFile(At("out.csv"), result.Sheet);
            CadExtraction.WriteFile(At("out.txt"), result.Sheet);

            CadSheet book = CadWorkbook.Open(At("out.xlsx")).ReadSheet("Sheet1");
            Assert.Equal(CadSheetCellKind.Number, book[1, 0].Kind);
            Assert.Equal(2.0, book[1, 0].Number);
            Assert.Equal("Bolt", book[1, 2].Text);
            Assert.Equal(
                "Count,Name,NAME,PRICE\r\n2,ITEM,Bolt,1.5\r\n1,ITEM,Nut,12\r\n",
                File.ReadAllText(At("out.csv"))
            );
            Assert.Equal(
                "Count\tName\tNAME\tPRICE\r\n2\tITEM\tBolt\t1.5\r\n1\tITEM\tNut\t12\r\n",
                File.ReadAllText(At("out.txt"))
            );
            Assert.Throws<NotSupportedException>(() => CadExtraction.WriteFile(At("out.xls"), result.Sheet));
            Assert.Throws<NotSupportedException>(() => CadExtraction.WriteFile(At("out.mdb"), result.Sheet));
        }

        /// <summary>Runs -DATAEXTRACTION through to the insertion point for the ITEM blocks, answering as the test says.</summary>
        private CadCommandSession Typed(CadDocument document, params object[] after)
        {
            CadCommandSession session = Run(Session(document), "-DATAEXTRACTION");
            Assert.Equal("Enter name of an existing data extraction file or [New] <New>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Extract from [Drawing/Selection] <Drawing>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal(
                "Enter the object types to extract, separated by commas, or [All/?] <All>:",
                session.PromptText
            );
            Run(session, "item");
            Assert.Equal("Enter the properties to extract, separated by commas, or [All/?] <All>:", session.PromptText);
            Run(session, "name, price");
            Assert.Equal("Combine identical rows [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter, Enter, Enter);
            Assert.Equal(
                "Enter the column to sort by (Count, Name or a property) or [None] <None>:",
                session.PromptText
            );
            Run(session, after);
            return session;
        }

        [Fact]
        public void TheCommandLine_AsksWhatToExtract_WritesTheDxeAndTheFile_AndInsertsATableOfTheDataLinkedToIt()
        {
            CadDocument document = Parts();
            CadCommandSession session = Typed(document, "Count", "Yes");

            Assert.Equal("Insert data extraction table into drawing [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter a title for the table:", session.PromptText);
            Run(session, "Parts list");
            Assert.Equal("Enter an output file name (.xlsx, .csv, .txt) or [None] <None>:", session.PromptText);
            Run(session, At("out.csv"));
            Assert.Equal("Enter a name for the data extraction file <" + At("parts.dxe") + ">:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify insertion point:", session.PromptText);
            Assert.Contains("3 objects extracted.", session.History);
            Assert.True(File.Exists(At("parts.dxe")));
            Assert.Equal(
                "Count,Name,NAME,PRICE\r\n2,ITEM,Bolt,1.5\r\n1,ITEM,Nut,12\r\n",
                File.ReadAllText(At("out.csv"))
            );

            Run(session, P(10, 10));

            Assert.False(session.IsRunning, session.PromptText);
            DxfTable table = document.Database.ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal((4, 4), (table.NumRows, table.NumColumns));
            Assert.Equal("Parts list", table.TextString(0, 0));
            Assert.Equal(new DxfCellRange(0, 0, 0, 3), table.GetMergeRange(0, 2));
            Assert.Equal("Count", table.TextString(1, 0));
            Assert.Equal("Bolt", table.TextString(2, 2));
            Assert.Equal("12", table.TextString(3, 3));
            Assert.Null(table.GetDataLink(0, 0));

            DxfDataLink link = table.GetDataLink(2, 1);
            Assert.Equal("Data Extraction - parts.dxe", link.Name);
            Assert.Equal("Autodesk.AutoCAD.DataExtraction.DxDataLinkAdapter", link.DataAdapterId);
            Assert.Equal("parts.dxe", link.ConnectionString);
            Assert.Equal(5, (int)link.DataLinkOption);
            Assert.Equal(0x60000, link.UpdateOption);
            Assert.Equal(
                (1, 0, 3, 3),
                (
                    table.GetDataLinkRange(2, 1).TopRow,
                    table.GetDataLinkRange(2, 1).LeftColumn,
                    table.GetDataLinkRange(2, 1).BottomRow,
                    table.GetDataLinkRange(2, 1).RightColumn
                )
            );

            // The table, its link and its settings are one undo step.
            Assert.True(document.Undo());
            Assert.Empty(document.Database.ModelSpace.OfType<DxfTable>());
        }

        [Fact]
        public void TheTableOfAnExtraction_IsUpdatedByDataLinkUpdate_WhenTheDrawingChanges()
        {
            CadDocument document = Parts();
            CadCommandSession session = Typed(document, Enter, Enter, "Parts list", Enter, Enter, P(0, 100));
            Assert.False(session.IsRunning, session.PromptText);
            Assert.Equal("Data Extraction - parts.dxe", Assert.Single(CadDataLinks.List(document)).Name);

            Insert(document.Database, document.Database.BlockTable["ITEM"], 50, "Washer", "0.5");
            Insert(document.Database, document.Database.BlockTable["ITEM"], 60, "Bolt", "1.5");
            session = Run(Session(document), "DATALINKUPDATE");
            Assert.Equal("Select an option [Update data link/Write data link] <Update>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Select objects or [Data link/all data linKs]:", session.PromptText);
            Run(session, "K");

            Assert.False(session.IsRunning, session.PromptText);
            Assert.Contains("1 data link(s) updated successfully.", session.History);
            DxfTable table = document.Database.ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal((5, 4), (table.NumRows, table.NumColumns));
            Assert.Equal("Parts list", table.TextString(0, 0));
            Assert.Equal("3", table.TextString(2, 0));
            Assert.Equal("Washer", table.TextString(4, 2));
            Assert.Equal(
                (1, 0, 4, 3),
                (
                    table.GetDataLinkRange(4, 3).TopRow,
                    table.GetDataLinkRange(4, 3).LeftColumn,
                    table.GetDataLinkRange(4, 3).BottomRow,
                    table.GetDataLinkRange(4, 3).RightColumn
                )
            );
        }

        [Fact]
        public void WithTheDxeFileGone_TheUpdateIsToldFailedAndTheTableKeepsItsData()
        {
            CadDocument document = Parts();
            Typed(document, Enter, Enter, Enter, Enter, Enter, P(0, 100));
            File.Delete(At("parts.dxe"));

            CadCommandSession session = Run(
                Session(document),
                "DATALINKUPDATE",
                Enter,
                "D",
                "Data Extraction - parts.dxe"
            );

            Assert.False(session.IsRunning, session.PromptText);
            Assert.Contains("1 data link(s) could not be updated.", session.History);
            Assert.Equal("Bolt", document.Database.ModelSpace.OfType<DxfTable>().Single().TextString(2, 2));
        }

        [Fact]
        public void AnExistingDxeNamedAtTheFirstQuestion_IsReadAndItsSettingsKept()
        {
            CadDocument document = Parts();
            CadExtractionSettings settings = Items();
            settings.InsertTable = false;
            settings.Save(At("items.dxe"));

            CadCommandSession session = Run(Session(document), "-DATAEXTRACTION", "items");
            Assert.Equal(
                "Enter the object types to extract, separated by commas, or [All/?] <ITEM>:",
                session.PromptText
            );
            Run(session, Enter);
            Assert.Equal(
                "Enter the properties to extract, separated by commas, or [All/?] <Attribute/NAME,Attribute/PRICE>:",
                session.PromptText
            );
            Run(session, Enter, Enter, Enter, Enter, Enter, Enter);
            Assert.Equal("Enter an output file name (.xlsx, .csv, .txt) or [None] <None>:", session.PromptText);

            Assert.Equal("Enter an output file name (.xlsx, .csv, .txt) or [None] <None>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter a name for the data extraction file <" + At("items.dxe") + ">:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning, session.PromptText);
            Assert.Empty(document.Database.ModelSpace.OfType<DxfTable>());
            Assert.Contains("3 objects extracted.", session.History);
        }

        [Fact]
        public void ATypeOrAPropertyThatIsNotThere_IsToldAndAskedAgain_AndTheQuestionMarkListsThem()
        {
            CadDocument document = Parts();
            CadCommandSession session = Run(Session(document), "-DATAEXTRACTION", Enter, Enter, "?");

            Assert.Contains("  ITEM (3)", session.History);
            Assert.Contains("  PLAIN (0)", session.History);
            Run(session, "nothing");
            Assert.Equal("Object type \"nothing\" not found.", session.History.Last());
            Run(session, "ITEM", "?");
            Assert.Contains("  Attribute/NAME", session.History);
            Run(session, "bogus");
            Assert.Equal("Property \"bogus\" not found.", session.History.Last());
            Assert.Equal("Enter the properties to extract, separated by commas, or [All/?] <All>:", session.PromptText);
        }

        [Fact]
        public void TheWizard_ShownByTheWindow_GivesItsSettings_AndSelectObjectsClosesItForTheSelectionAndShowsItAgain()
        {
            CadDocument document = Parts();
            CadCommandSession session = Session(document);
            int shown = 0;
            string nut = document
                .Database.ModelSpace.OfType<DxfBlockReference>()
                .First(b => b.AttributeCollection.Any(a => a.TextString == "Nut"))
                .Handle;
            session.UiRequested += (sender, e) =>
            {
                if (e.Payload is CadDataExtractionRequest request)
                {
                    shown++;
                    e.Handled = true;

                    if (shown == 1)
                    {
                        request.IsSelecting = true;
                        return;
                    }

                    Assert.Equal(new[] { nut }, request.Settings.SelectedHandles);
                    request.Settings.SelectedTypes = new List<string> { "ITEM" };
                    request.Settings.SelectedProperties = new List<string> { "Attribute/NAME" };
                    request.Settings.InsertTable = false;
                    request.Settings.OutputToFile = true;
                    request.Settings.OutputFile = At("nut.csv");
                    request.DxeFile = At("nut.dxe");
                    request.Action = CadDialogAction.Ok;
                }
            };

            Run(session, "DATAEXTRACTION");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { nut }, Enter);

            Assert.False(session.IsRunning, session.PromptText);
            Assert.Equal(2, shown);
            Assert.Equal("Count,Name,NAME\r\n1,ITEM,Nut\r\n", File.ReadAllText(At("nut.csv")));
            Assert.Equal(new[] { nut }, CadExtractionSettings.Load(At("nut.dxe")).SelectedHandles);
        }

        [Fact]
        public void EattextIsAnAliasOfDataExtraction_AndCancelWritesNothing()
        {
            CadDocument document = Parts();
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Payload is CadDataExtractionRequest request)
                {
                    e.Handled = true;
                    request.Action = CadDialogAction.Cancel;
                }
            };

            Run(session, "EATTEXT");

            Assert.False(session.IsRunning, session.PromptText);
            Assert.False(File.Exists(At("parts.dxe")));
            Assert.Empty(document.Database.ModelSpace.OfType<DxfTable>());
        }

        [Fact]
        public void ADrawingOfR2000_InsertsTheExtractionAsATableWithoutALink_AndSaysSo()
        {
            var db = new DxfDatabase("AC1015");
            db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            string path = At("old.dxf");
            db.Save(path);
            CadDocument document = CadDocument.Open(path);
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Payload is CadDataExtractionRequest request)
                {
                    e.Handled = true;
                    request.Settings.SelectedTypes = new List<string> { "Line" };
                    request.Settings.SelectedProperties = new List<string> { "Geometry/Length" };
                    request.DxeFile = At("old.dxe");
                    request.Action = CadDialogAction.Ok;
                }
            };

            Run(session, "DATAEXTRACTION", P(0, 0));

            DxfTable table = document.Database.ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal("100", table.TextString(2, 2));
            Assert.False(CadDataLinks.CanHold(document));
            Assert.Null(table.GetDataLink(2, 2));
            Assert.Contains(
                "The drawing's format has no data links: the table was inserted without one.",
                session.History
            );
        }
    }
}
