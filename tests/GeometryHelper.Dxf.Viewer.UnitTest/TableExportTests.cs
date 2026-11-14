using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// TABLEEXPORT as AutoCAD 2021 runs it: "Select a table:" (asked again, "Object selected is not a table.", for any other
    /// object), the Export Data file, and the table written as comma separated values, the text of each cell as it shows.
    /// </summary>
    public sealed class TableExportTests
    {
        private static DxfDatabase Drawing()
        {
            var database = new DxfDatabase("AC1032");

            if (database.TableStyleDictionary == null)
            {
                database.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            database.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            return database;
        }

        private static DxfTable Table(DxfDatabase database, int rows, int columns)
        {
            var table = new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 9.0, 30.0);
            database.ModelSpace.AppendEntity(table);
            return table;
        }

        private static string Folder() => Path.Combine(Path.GetTempPath(), "gh-tx-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void ACellWithACommaOrAQuote_IsQuotedAndTheQuoteDoubled_EachRowOnItsLine()
        {
            DxfDatabase database = Drawing();
            DxfTable table = Table(database, 3, 2);
            table.SetTextString(0, 0, "Title, with comma");
            table.SetTextString(1, 0, "Bolt");
            table.SetTextString(1, 1, "say \"hi\"");
            table.SetTextString(2, 0, "Nut");
            table.SetTextString(2, 1, "3.50");

            string csv = CadTableCsv.Of(table);

            Assert.Equal("\"Title, with comma\",\r\nBolt,\"say \"\"hi\"\"\"\r\nNut,3.50\r\n", csv);
        }

        [Fact]
        public void TheCodesOfTheLook_AreTakenOff_AStackIsItsFraction_AndALineBreakIsKeptInsideQuotes()
        {
            DxfDatabase database = Drawing();
            DxfTable table = Table(database, 2, 3);
            table.SetTextString(1, 0, "{\\fArial|b1;Bold} plain");
            table.SetTextString(1, 1, "one\\Ptwo");
            table.SetTextString(1, 2, "tail \\S1/2; frac");

            Assert.Equal(",,\r\nBold plain,\"one\ntwo\",tail 1/2 frac\r\n", CadTableCsv.Of(table));
        }

        [Fact]
        public void AMergedBlock_GivesItsTextOnceInItsFirstField_TheOthersEmpty()
        {
            DxfDatabase database = Drawing();
            DxfTable table = Table(database, 2, 3);
            table.SetTextString(0, 0, "Title");
            table.MergeCells(new DxfCellRange(0, 0, 0, 2));
            table.SetTextString(1, 0, "a");
            table.SetTextString(1, 2, "c");

            Assert.Equal("Title,,\r\na,,c\r\n", CadTableCsv.Of(table));
        }

        [Fact]
        public void TheTablePicked_AndAFileTyped_AreWrittenAsCsv_WithNoUndoStepAndNoMessage()
        {
            DxfDatabase database = Drawing();
            DxfTable table = Table(database, 3, 2);
            table.SetTextString(1, 0, "Name");
            table.SetTextString(1, 1, "Qty");
            table.SetTextString(2, 0, "Nut, M8");
            table.SetTextString(2, 1, "12");
            CadDocument document = CadDocument.From(database, "t.dxf");
            string folder = Folder();
            Directory.CreateDirectory(folder);

            try
            {
                string file = Path.Combine(folder, "bom");
                CadCommandSession session = Session(document);
                Run(session, "TABLEEXPORT");
                Assert.Equal("Select a table:", session.PromptText);
                Run(session, (object)new[] { table.Handle });
                Assert.StartsWith("Enter file name <", session.PromptText);
                Run(session, file);

                Assert.False(session.IsRunning);
                Assert.Equal(",\r\nName,Qty\r\n\"Nut, M8\",12\r\n", File.ReadAllText(file + ".csv", Encoding.Default));
                Assert.Null(document.NextUndo);
                Assert.DoesNotContain(session.History, line => line.Contains("open file"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void AnythingButATable_IsRefused_AndTheTableAskedForAgain()
        {
            DxfDatabase database = Drawing();
            DxfTable table = Table(database, 1, 1);
            var line = new DxfLine(new GeoPoint3(50, 50, 0), new GeoPoint3(60, 50, 0));
            database.ModelSpace.AppendEntity(line);
            CadDocument document = CadDocument.From(database, "t.dxf");
            CadCommandSession session = Session(document);
            Run(session, "TABLEEXPORT", new[] { line.Handle });

            Assert.Contains("Object selected is not a table.", session.History);
            Assert.True(session.IsRunning);
            Assert.Equal("Select a table:", session.PromptText);
        }

        [Fact]
        public void AFileThatCannotBeWritten_IsSaidAsAutoCadSaysIt()
        {
            DxfDatabase database = Drawing();
            DxfTable table = Table(database, 1, 1);
            CadDocument document = CadDocument.From(database, "t.dxf");
            CadCommandSession session = Session(document);
            Run(session, "TABLEEXPORT", new[] { table.Handle }, Path.Combine(Folder(), "missing", "x.csv"));

            Assert.False(session.IsRunning);
            Assert.Contains("Can't  open file.", session.History);
        }
    }
}
