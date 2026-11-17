using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The data links of a drawing as DATALINK and the table menus work them: a link to a CSV file made in the drawing's
    /// ACAD_DATALINK dictionary as AutoCAD 2021 writes the link of its Excel adapter, a table's cells linked to it, filled from
    /// the file and grown or shrunk to its size when it is updated, written back to it, detached, renamed and deleted, each one
    /// undo step, and the link read again from DXF and from DWG.
    /// </summary>
    public sealed class DataLinkTests
    {
        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-dl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static DxfDatabase NewDatabase(string version = "AC1032")
        {
            var database = new DxfDatabase(version);

            if (database.TableStyleDictionary == null)
            {
                database.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            database.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            return database;
        }

        /// <summary>A drawing with a table of 3 rows and 2 columns at the origin, in the folder given as the drawing's own.</summary>
        private static CadDocument Drawing(out DxfTable table, string version = "AC1032")
        {
            DxfDatabase database = NewDatabase(version);
            table = new DxfTable(new GeoPoint3(0, 0, 0), 3, 2, 9.0, 30.0);
            database.ModelSpace.AppendEntity(table);
            return CadDocument.From(database, "plan.dxf");
        }

        private static string Csv(string folder, string name, string text)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        private static CadDataLinkSettings Settings(string name, string file) =>
            new CadDataLinkSettings { Name = name, File = file };

        private static string Text(DxfTable table, int row, int column) => table.TextString(row, column);

        [Fact]
        public void ALinkToACsvFile_IsMadeAsOneUndoStep_AndListedWithWhatAutoCadKeepsOfAnExcelLink()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "Mark,Qty\r\nB-12,2\r\n");
            CadDocument document = Drawing(out _);

            CadEditResult made = CadDataLinks.Create(document, Settings("Parts", file));

            Assert.True(made.Succeeded);
            CadDataLinkInfo info = Assert.Single(CadDataLinks.List(document));
            Assert.Equal("Parts", info.Name);
            Assert.Equal("AcExcel", info.AdapterId);
            Assert.Equal(file + "!parts", info.ConnectionString);
            Assert.Equal("Data Link\nParts\n" + file + "\nLink details: Entire sheet: parts", info.ToolTip);
            Assert.Equal(1 | 0x40000 | 0x80000 | 0x100000, info.UpdateOption);
            Assert.Equal(0, info.Tables);

            Assert.True(document.Undo());
            Assert.Empty(CadDataLinks.List(document));
        }

        [Fact]
        public void TheDataLinkIsWrittenInTheOrderOfGroupsAutoCad2021Writes_AndItsCustomDataNamesTheThreeValuesOfTheExcelAdapter()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "a\r\n");
            CadDocument document = Drawing(out _);
            CadDataLinks.Create(document, Settings("Parts", file));

            string text = document.Database.ToText();
            int at = text.IndexOf("  0\nDATALINK\n", StringComparison.Ordinal);
            string record = text.Substring(at, text.IndexOf("  0\n", at + 10, StringComparison.Ordinal) - at);

            Assert.Contains("100\nAcDbDataLink\n  1\nAcExcel\n300\n\n301\nData Link^JParts^J", record);
            Assert.Contains("\n 90\n0\n 91\n1835009\n 92\n1\n170\n", record);
            Assert.Contains(
                "\n 93\n0\n304\n\n 94\n0\n360\n0\n305\nCUSTOMDATA\n  1\nDATAMAP_BEGIN\n 90\n3\n300\nACEXCEL_UPDATEOPTIONS\n301\nDATAMAP_VALUE\n 93\n2\n 90\n1\n 91\n1835009\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n",
                record
            );
            Assert.Contains(
                "300\nACEXCEL_CONNECTION_STRING\n301\nDATAMAP_VALUE\n 93\n2\n 90\n4\n  1\n" + file + "!parts\n",
                record
            );
            Assert.Contains("300\nACEXCEL_SOURCEDATE\n301\nDATAMAP_VALUE\n 93\n2\n 90\n8\n 92\n16\n310\n", record);
            Assert.EndsWith("309\nDATAMAP_END\n", record);
            Assert.Contains("  0\nDATALINK\n", text);

            // The class is listed as AutoCAD 2021 lists it, flags 1152.
            Assert.Contains("  0\nCLASS\n  1\nDATALINK\n  2\nAcDbDataLink\n  3\nObjectDBX Classes\n 90\n1152\n", text);

            // The dictionary names the link by a hard owner (360), as AutoCAD's ACAD_DATALINK does.
            Assert.True(document.Database.DataLinkDictionary.TreatElementsAsHard);
        }

        [Fact]
        public void ANameInUse_AnInvalidName_AMissingFile_AndAnOldDrawing_AreRefusedAndWriteNothing()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "a\r\n");
            CadDocument document = Drawing(out _);
            CadDataLinks.Create(document, Settings("Parts", file));
            string before = document.Database.ToText();

            CadEditResult duplicate = CadDataLinks.Create(document, Settings("PARTS", file));
            Assert.True(duplicate.Refused);
            Assert.Equal("Data link name in use.", duplicate.Message);
            Assert.Equal("Invalid data link name.", CadDataLinks.Create(document, Settings("  ", file)).Message);
            Assert.Equal("Invalid data link name.", CadDataLinks.Create(document, Settings("a/b", file)).Message);
            Assert.Contains(
                "cannot be found",
                CadDataLinks.Create(document, Settings("Gone", Path.Combine(folder, "none.csv"))).Message
            );
            Assert.Contains(
                "Invalid Range",
                CadDataLinks
                    .Create(
                        document,
                        new CadDataLinkSettings
                        {
                            Name = "Bad",
                            File = file,
                            RangeKind = CadDataLinkRangeKind.CellRange,
                            Range = "A1:",
                        }
                    )
                    .Message
            );
            Assert.Equal(before, document.Database.ToText());

            CadDocument old = Drawing(out _, "AC1015");
            CadEditResult refused = CadDataLinks.Create(old, Settings("Parts", file));
            Assert.Equal("Data links need a drawing of AutoCAD 2007 or later.", refused.Message);
            Assert.False(CadDataLinks.CanHold(old));
        }

        [Fact]
        public void ARangeOfASheet_AndANamedRange_AreWordedAsTheExcelAdapterWordsThem()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "a,b,c\r\n1,2,3\r\n");
            CadDocument document = Drawing(out _);

            CadDataLinks.Create(
                document,
                new CadDataLinkSettings
                {
                    Name = "R",
                    File = file,
                    RangeKind = CadDataLinkRangeKind.CellRange,
                    Range = "A1:B2",
                }
            );
            CadDataLinkInfo link = CadDataLinks.List(document).Single();

            Assert.Equal(file + "!parts!A1:B2", link.ConnectionString);
            Assert.EndsWith("Link details: Range: parts!A1:B2", link.ToolTip);
            Assert.Equal(file + "\nRange: parts!A1:B2", link.Details);

            Assert.Equal(
                "Data link name in use.",
                CadDataLinks.Create(document, new CadDataLinkSettings { Name = "r", File = file }).Message
            );
        }

        [Fact]
        public void LinkingACell_PutsTheFilesDataInTheTable_GrowsItToHoldIt_AndTiesTheLinkToTheTable()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "Mark,Qty,Note\r\nB-12,2,\"Nut, M8\"\r\nB-13,3,\r\n");
            CadDocument document = Drawing(out DxfTable table);
            CadDataLinks.Create(document, Settings("Parts", file));

            CadEditResult linked = CadDataLinks.LinkCells(document, table.Handle, 1, 0, "Parts");

            Assert.True(linked.Succeeded);
            DxfTable now = (DxfTable)document.FindEntity(table.Handle);
            Assert.Equal((4, 3), (now.NumRows, now.NumColumns));
            Assert.Equal("Mark", Text(now, 1, 0));
            Assert.Equal("Nut, M8", Text(now, 2, 2));
            Assert.Equal("B-13", Text(now, 3, 0));
            Assert.Equal("", Text(now, 0, 0));

            DxfDataLink link = now.GetDataLink(1, 0);
            Assert.Equal("Parts", link.Name);
            Assert.Null(now.GetDataLink(0, 0));
            DxfCellRange range = now.GetDataLinkRange(3, 2);
            Assert.Equal((1, 0, 3, 2), (range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn));
            Assert.True(now.IsLinked(1, 0));
            Assert.Equal(new[] { now.Handle }, link.GetTargets().Select(t => t.Handle));
            Assert.Equal(1, CadDataLinks.List(document).Single().Tables);

            link.GetUpdateStatus(out DxfUpdateDirection direction, out DateTime when, out string message);
            Assert.Equal(DxfUpdateDirection.SourceToData, direction);
            Assert.True((DateTime.Now - when).TotalMinutes < 5);
            Assert.Equal("", message);

            // One undo step takes the data, the new rows and the link back.
            Assert.True(document.Undo());
            DxfTable before = (DxfTable)document.FindEntity(table.Handle);
            Assert.Equal((3, 2), (before.NumRows, before.NumColumns));
            Assert.Null(before.GetDataLink(1, 0));
            Assert.Single(CadDataLinks.List(document));
        }

        [Fact]
        public void LinkingCellsThatAreLinkedAlready_ATableOfAnOldDrawing_AndALinkThatIsNotThere_AreRefused()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "a,b\r\n");
            CadDocument document = Drawing(out DxfTable table);
            CadDataLinks.Create(document, Settings("Parts", file));
            CadDataLinks.LinkCells(document, table.Handle, 0, 0, "Parts");

            CadEditResult again = CadDataLinks.LinkCells(document, table.Handle, 0, 0, "Parts");

            Assert.True(again.Refused);
            Assert.Contains("linked to data already", again.Message);
            Assert.Equal(
                "Data link not found.",
                CadDataLinks.LinkCells(document, table.Handle, 2, 0, "Nothing").Message
            );
            Assert.Contains("is not a table", CadDataLinks.LinkCells(document, "FFFF", 0, 0, "Parts").Message);
        }

        [Fact]
        public void UpdatingALink_GrowsAndShrinksTheTableToTheNewSource_KeepingTheCellsOutsideItsRange()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "Mark,Qty\r\nB-12,2\r\n");
            CadDocument document = Drawing(out DxfTable table);
            table.SetTextString(0, 0, "Title");
            CadDataLinks.Create(document, Settings("Parts", file));
            CadDataLinks.LinkCells(document, table.Handle, 1, 0, "Parts");
            DxfDataLink link = CadDataLinks.Find(document, "Parts");

            File.WriteAllText(file, "Mark,Qty,Note\r\nB-12,2,x\r\nB-13,3,y\r\nB-14,4,z\r\n");
            CadDataLinkRun grown = CadDataLinks.Update(document, new[] { link });

            Assert.Equal((1, 0), (grown.Succeeded, grown.Failed));
            DxfTable now = (DxfTable)document.FindEntity(table.Handle);
            Assert.Equal((5, 3), (now.NumRows, now.NumColumns));
            Assert.Equal("Title", Text(now, 0, 0));
            Assert.Equal("B-14", Text(now, 4, 0));
            Assert.Equal("z", Text(now, 4, 2));
            DxfCellRange range = now.GetDataLinkRange(4, 2);
            Assert.Equal((1, 0, 4, 2), (range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn));

            File.WriteAllText(file, "Mark\r\nB-99\r\n");
            CadDataLinkRun shrunk = CadDataLinks.Update(document, new[] { CadDataLinks.Find(document, "Parts") });

            Assert.Equal(1, shrunk.Succeeded);
            DxfTable least = (DxfTable)document.FindEntity(table.Handle);
            Assert.Equal((3, 1), (least.NumRows, least.NumColumns));
            Assert.Equal("Title", Text(least, 0, 0));
            Assert.Equal("B-99", Text(least, 2, 0));
            Assert.Equal(
                (1, 0, 2, 0),
                (
                    least.GetDataLinkRange(2, 0).TopRow,
                    least.GetDataLinkRange(2, 0).LeftColumn,
                    least.GetDataLinkRange(2, 0).BottomRow,
                    least.GetDataLinkRange(2, 0).RightColumn
                )
            );

            // One undo step for each update.
            Assert.True(document.Undo());
            Assert.Equal(
                (5, 3),
                (
                    ((DxfTable)document.FindEntity(table.Handle)).NumRows,
                    ((DxfTable)document.FindEntity(table.Handle)).NumColumns
                )
            );
        }

        [Fact]
        public void ALinkWhoseSourceIsGone_IsCountedFailedWithItsMessage_ItsTableIsLeftAndItsStatusSaysSo()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "Mark\r\nB-12\r\n");
            CadDocument document = Drawing(out DxfTable table);
            CadDataLinks.Create(document, Settings("Parts", file));
            CadDataLinks.LinkCells(document, table.Handle, 0, 0, "Parts");
            File.Delete(file);

            CadDataLinkRun run = CadDataLinks.Update(document, new[] { CadDataLinks.Find(document, "Parts") });

            Assert.Equal((0, 1), (run.Succeeded, run.Failed));
            Assert.Contains("Parts: The file", Assert.Single(run.Messages));
            DxfTable now = (DxfTable)document.FindEntity(table.Handle);
            Assert.Equal("B-12", Text(now, 1, 0));
            int status = CadDataLinks.Find(document, "Parts").GetUpdateStatus(out _, out _, out string message);
            Assert.Equal(1, status);
            Assert.Contains("cannot be found", message);
        }

        [Fact]
        public void WritingALinkToItsSource_PutsTheTablesCellsInTheFile_OnlyWhereTheLinkAllowsIt()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "Mark,Qty,Keep\r\nB-12,2,k\r\n");
            CadDocument document = Drawing(out DxfTable table);
            CadDataLinks.Create(
                document,
                new CadDataLinkSettings
                {
                    Name = "Parts",
                    File = file,
                    RangeKind = CadDataLinkRangeKind.CellRange,
                    Range = "A1:B2",
                }
            );
            CadDataLinks.Create(
                document,
                new CadDataLinkSettings
                {
                    Name = "Locked",
                    File = file,
                    AllowWritingToSource = false,
                }
            );
            CadDataLinks.LinkCells(document, table.Handle, 0, 0, "Parts");
            DxfTable linked = (DxfTable)document.FindEntity(table.Handle);
            CadTableEditor.SetText(document, table.Handle, 1, 1, "Washer");
            CadTableEditor.SetText(document, table.Handle, 1, 0, "N-7");

            CadDataLinkRun run = CadDataLinks.Write(
                document,
                new[] { CadDataLinks.Find(document, "Parts"), CadDataLinks.Find(document, "Locked") }
            );

            Assert.Equal((1, 0, 1), (run.Succeeded, run.Failed, run.NotAllowed));
            Assert.Equal("Mark,Qty,Keep\r\nN-7,Washer,k\r\n", File.ReadAllText(file));
            CadDataLinks.Find(document, "Parts").GetUpdateStatus(out DxfUpdateDirection direction, out _, out _);
            Assert.Equal(DxfUpdateDirection.DataToSource, direction);
        }

        [Fact]
        public void DetachingTheLink_LeavesTheCellsAsTheyAre_KeepsTheLinkInTheManager_AndLetsItBeDeleted()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "Mark\r\nB-12\r\n");
            CadDocument document = Drawing(out DxfTable table);
            CadDataLinks.Create(document, Settings("Parts", file));
            CadDataLinks.LinkCells(document, table.Handle, 0, 0, "Parts");

            CadEditResult inUse = CadDataLinks.Delete(document, "Parts");
            Assert.True(inUse.Refused);
            Assert.Equal("The data link is being used and cannot be deleted.", inUse.Message);

            Assert.True(CadDataLinks.Detach(document, table.Handle, 1, 0).Succeeded);
            DxfTable now = (DxfTable)document.FindEntity(table.Handle);
            Assert.Null(now.GetDataLink(1, 0));
            Assert.Equal("B-12", Text(now, 1, 0));
            Assert.Equal(0, CadDataLinks.List(document).Single().Tables);
            Assert.Contains("is not linked", CadDataLinks.Detach(document, table.Handle, 1, 0).Message);

            Assert.True(CadDataLinks.Delete(document, "Parts").Succeeded);
            Assert.Empty(CadDataLinks.List(document));
            Assert.True(document.Undo());
            Assert.Single(CadDataLinks.List(document));
        }

        [Fact]
        public void RenamingALink_KeepsItsTables_RefusesANameInUse_AndEditingItRewritesItsSource()
        {
            string folder = Folder();
            string first = Csv(folder, "parts.csv", "Mark\r\nB-12\r\n");
            string second = Csv(folder, "bolts.csv", "Bolt\r\nM8\r\n");
            CadDocument document = Drawing(out DxfTable table);
            CadDataLinks.Create(document, Settings("Parts", first));
            CadDataLinks.Create(document, Settings("Bolts", second));
            CadDataLinks.LinkCells(document, table.Handle, 0, 0, "Parts");

            Assert.Equal("Data link name in use.", CadDataLinks.Rename(document, "Parts", "bolts").Message);
            Assert.True(CadDataLinks.Rename(document, "Parts", "Fasteners").Succeeded);
            Assert.Equal("Fasteners", ((DxfTable)document.FindEntity(table.Handle)).GetDataLink(0, 0).Name);

            CadEditResult edited = CadDataLinks.Edit(
                document,
                "Fasteners",
                new CadDataLinkSettings
                {
                    Name = "Fasteners",
                    File = second,
                    UseExcelFormatting = false,
                    AllowWritingToSource = false,
                }
            );
            Assert.True(edited.Succeeded);
            CadDataLinkInfo link = CadDataLinks.List(document).Single(l => l.Name == "Fasteners");
            Assert.Equal(second + "!bolts", link.ConnectionString);
            Assert.Equal(1 | 0x40000 | 0x80000 | CadDataLinks.SkipFormat, link.UpdateOption);
            Assert.Equal(1, link.Tables);
            Assert.Equal("Data link Nothing does not exist.", CadDataLinks.Rename(document, "Nothing", "x").Message);
        }

        [Fact]
        public void TheWorkbooksAlignmentAndFill_GoToTheTable_UnlessTheLinkSkipsItsFormat()
        {
            // The cell B5 of the workbook is right aligned, filled yellow, its words red.
            string path = DataLinkSpreadsheetTests.Workbook(Folder());
            CadDocument document = Drawing(out DxfTable table);
            CadDataLinks.Create(
                document,
                new CadDataLinkSettings
                {
                    Name = "Look",
                    File = path,
                    RangeKind = CadDataLinkRangeKind.CellRange,
                    Sheet = "Parts",
                    Range = "B5",
                }
            );
            CadDataLinks.Create(
                document,
                new CadDataLinkSettings
                {
                    Name = "Plain",
                    File = path,
                    RangeKind = CadDataLinkRangeKind.CellRange,
                    Sheet = "Parts",
                    Range = "B5",
                    UseExcelFormatting = false,
                }
            );

            CadEditResult looked = CadDataLinks.LinkCells(document, table.Handle, 0, 0, "Look");
            Assert.True(looked.Succeeded, looked.Message);
            CadEditResult plain = CadDataLinks.LinkCells(document, table.Handle, 2, 0, "Plain");
            Assert.True(plain.Succeeded, plain.Message);

            DxfTable now = (DxfTable)document.FindEntity(table.Handle);
            Assert.Equal("Total", Text(now, 0, 0));
            Assert.Equal(DxfCellAlignment.MiddleRight, now.Alignment(0, 0));
            Assert.Equal(255, now.BackgroundColor(0, 0).Red);
            Assert.Equal(255, now.BackgroundColor(0, 0).Green);
            Assert.Equal(0, now.BackgroundColor(0, 0).Blue);
            Assert.Equal(255, now.ContentColor(0, 0).Red);
            Assert.Equal("Total", Text(now, 2, 0));
            Assert.Null(now.Alignment(2, 0));
        }

        [Fact]
        public void ALinkAndItsTable_ReadAgainFromDxf_AndFromDwg_WithTheDataTheyHeld()
        {
            string folder = Folder();
            string file = Csv(folder, "parts.csv", "Mark,Qty\r\nB-12,2\r\n");
            CadDocument document = Drawing(out DxfTable table);
            CadDataLinks.Create(document, Settings("Parts", file));
            CadDataLinks.LinkCells(document, table.Handle, 0, 0, "Parts");

            DxfDatabase dxf = DxfDatabase.Parse(document.Database.ToText());
            DxfTable fromDxf = dxf.ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal("Parts", fromDxf.GetDataLink(1, 1).Name);
            Assert.Equal("AcExcel", fromDxf.GetDataLink(0, 0).DataAdapterId);
            Assert.Equal(2, fromDxf.GetDataLink(0, 0).GetTargets().Count + 1);
            Assert.Equal("B-12", fromDxf.TextString(1, 0));

            string dwg = Path.Combine(folder, "plan.dwg");
            DwgWriter.Write(document.Database, dwg);
            DxfDatabase reopened = DwgReader.Open(dwg);
            DxfTable fromDwg = reopened.ModelSpace.OfType<DxfTable>().Single();
            DxfDataLink link = fromDwg.GetDataLink(0, 0);

            Assert.Equal("Parts", link.Name);
            Assert.Equal("AcExcel", link.DataAdapterId);
            Assert.Equal(file + "!parts", link.ConnectionString);
            Assert.Equal(1 | 0x40000 | 0x80000 | 0x100000, link.UpdateOption);
            Assert.True(link.IsValid);
            Assert.Equal("Mark", fromDwg.TextString(0, 0));
            Assert.Single(link.GetTargets());
        }

        [Fact]
        public void ATableAndItsLinkSaved_OpenedAsADocument_KeepTheirRelativePathAndUpdateFromTheDrawingsFolder()
        {
            string folder = Folder();
            Csv(folder, "parts.csv", "Mark\r\nB-12\r\n");
            CadDocument drawing = Drawing(out DxfTable table);
            string path = Path.Combine(folder, "plan.dxf");
            drawing.Database.Save(path);
            CadDocument document = CadDocument.Open(path);
            DxfTable opened = document.Database.ModelSpace.OfType<DxfTable>().Single();

            CadEditResult made = CadDataLinks.Create(
                document,
                new CadDataLinkSettings
                {
                    Name = "Parts",
                    File = Path.Combine(folder, "parts.csv"),
                    PathType = CadDataLinkPathType.RelativePath,
                }
            );

            Assert.True(made.Succeeded);
            Assert.Equal("parts.csv!parts", CadDataLinks.List(document).Single().ConnectionString);
            Assert.True(CadDataLinks.LinkCells(document, opened.Handle, 0, 0, "Parts").Succeeded);
            Assert.Equal("B-12", ((DxfTable)document.FindEntity(opened.Handle)).TextString(1, 0));

            // No path: the name alone, looked for in the drawing's folder.
            CadDataLinks.Create(
                document,
                new CadDataLinkSettings
                {
                    Name = "Alone",
                    File = Path.Combine(folder, "parts.csv"),
                    PathType = CadDataLinkPathType.NoPath,
                }
            );
            Assert.Equal(
                "parts.csv!parts",
                CadDataLinks.List(document).Single(l => l.Name == "Alone").ConnectionString
            );
            Assert.Equal(
                "..\\x\\parts.csv",
                CadDataLinks.RelativePath(Path.Combine(folder, "a"), Path.Combine(folder, "x", "parts.csv"))
            );
        }
    }
}
