using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// DATALINK, DATALINKUPDATE and the table made from a data link (-TABLE's data Link, the Insert Table dialog's From a data
    /// link), word for word as AutoCAD 2021's resources give their prompts and messages: the options of the update, the
    /// selection of tables or links, what is said of each link, and one undo step for each.
    /// </summary>
    public sealed class DataLinkCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gh-dlc-" + Guid.NewGuid().ToString("N"));

        public DataLinkCommandTests()
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

        private static string[] Tail(System.Collections.Generic.IReadOnlyList<string> lines, int count) =>
            lines.Skip(Math.Max(0, lines.Count - count)).ToArray();

        /// <summary>A drawing with a table of 3 rows and 2 columns and a data link Parts to a CSV file of three rows.</summary>
        private CadDocument Linked(out DxfTable table)
        {
            var database = new DxfDatabase("AC1032");
            database.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            database.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            table = new DxfTable(new GeoPoint3(0, 0, 0), 3, 2, 9.0, 30.0);
            database.ModelSpace.AppendEntity(table);
            string path = At("plan.dxf");
            database.Save(path);
            File.WriteAllText(At("parts.csv"), "Mark,Qty\r\nB-12,2\r\nB-13,3\r\n");
            CadDocument document = CadDocument.Open(path);
            DxfTable opened = document.Database.ModelSpace.OfType<DxfTable>().Single();
            Assert.True(
                CadDataLinks
                    .Create(
                        document,
                        new CadDataLinkSettings
                        {
                            Name = "Parts",
                            File = At("parts.csv"),
                            AllowWritingToSource = true,
                        }
                    )
                    .Succeeded
            );
            Assert.True(CadDataLinks.LinkCells(document, opened.Handle, 0, 0, "Parts").Succeeded);
            table = (DxfTable)document.FindEntity(opened.Handle);
            return document;
        }

        [Fact]
        public void WithNoLinkInTheDrawing_DataLinkUpdateSaysSoInAutoCadsWords()
        {
            CadDocument document = Blank();

            CadCommandSession session = Run(Session(document), "DATALINKUPDATE");

            Assert.False(session.IsRunning);
            Assert.Equal(
                new[]
                {
                    "No data links exist in the current drawing.",
                    "To create a data link, use the DATALINK command.",
                },
                Tail(session.History, 2).ToArray()
            );
        }

        [Fact]
        public void UpdatingAllLinks_AsksTheOptionThenTheObjects_UpdatesTheTable_AndCountsTheLinks()
        {
            CadDocument document = Linked(out DxfTable table);
            File.WriteAllText(At("parts.csv"), "Mark,Qty\r\nB-12,2\r\nB-13,3\r\nB-14,4\r\n");

            CadCommandSession session = Run(Session(document), "DATALINKUPDATE");
            Assert.Equal("Select an option [Update data link/Write data link] <Update>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Select objects or [Data link/all data linKs]:", session.PromptText);
            Run(session, "K");

            Assert.False(session.IsRunning, session.PromptText);
            Assert.Equal("1 data link(s) updated successfully.", session.History.Last());
            DxfTable now = (DxfTable)document.FindEntity(table.Handle);
            Assert.Equal(4, now.NumRows);
            Assert.Equal("B-14", now.TextString(3, 0));
            Assert.True(document.Undo());
            Assert.Equal(3, ((DxfTable)document.FindEntity(table.Handle)).NumRows);
        }

        [Fact]
        public void ALinkPickedByNameOrByItsTable_IsUpdated_AndAnObjectThatIsNoLinkedTableIsCountedApart()
        {
            CadDocument document = Linked(out DxfTable table);
            File.WriteAllText(At("parts.csv"), "Mark,Qty\r\nZ-1,9\r\nZ-2,8\r\n");

            CadCommandSession session = Run(Session(document), "DATALINKUPDATE", "U", "D");
            Assert.Equal("Enter data link name or [?]:", session.PromptText);
            Run(session, "?");
            Assert.Equal(
                new[] { "Data links in Drawing:", "----------------------", "Parts" },
                Tail(session.History, 3).ToArray()
            );
            Run(session, "Nothing");
            Assert.Equal("Data link Nothing does not exist.", session.History.Last());
            Run(session, "parts");

            Assert.False(session.IsRunning);
            Assert.Equal("1 data link(s) updated successfully.", session.History.Last());
            Assert.Equal("Z-1", ((DxfTable)document.FindEntity(table.Handle)).TextString(1, 0));

            File.WriteAllText(At("parts.csv"), "Mark,Qty\r\nY-1,1\r\nY-2,2\r\n");
            string line = document.Database.ModelSpace.OfType<DxfTable>().Count().ToString();
            Assert.Equal("1", line);
            session = Run(Session(document), "DATALINKUPDATE", Enter, new[] { table.Handle }, Enter);

            Assert.False(session.IsRunning, session.PromptText);
            Assert.Contains("1 object(s) found.", session.History);
            Assert.Equal("Y-1", ((DxfTable)document.FindEntity(table.Handle)).TextString(1, 0));
        }

        [Fact]
        public void WritingLinks_PutsTheTableInTheFile_AndSaysHowManyAreWrittenOrNotAllowed()
        {
            CadDocument document = Linked(out DxfTable table);
            CadTableEditor.SetText(document, table.Handle, 1, 0, "N-1");
            CadDataLinks.Create(
                document,
                new CadDataLinkSettings
                {
                    Name = "Locked",
                    File = At("parts.csv"),
                    AllowWritingToSource = false,
                }
            );

            CadCommandSession session = Run(Session(document), "DATALINKUPDATE", "W");
            Assert.Equal("Select objects or [Data link/all data linKs]:", session.PromptText);
            Run(session, "K");

            Assert.False(session.IsRunning);
            Assert.Equal("1 data link(s) do not allow their data to be written out.", Tail(session.History, 2).First());
            Assert.Equal("1 data link(s) written out successfully.", session.History.Last());
            Assert.Equal("Mark,Qty\r\nN-1,2\r\nB-13,3\r\n", File.ReadAllText(At("parts.csv")));

            // The option chosen is offered next time.
            Assert.Equal(
                "Select an option [Update data link/Write data link] <Write>:",
                Run(session, "DATALINKUPDATE").PromptText
            );
        }

        [Fact]
        public void ALinkWhoseFileIsGone_IsCountedAsNotUpdated()
        {
            CadDocument document = Linked(out _);
            File.Delete(At("parts.csv"));

            CadCommandSession session = Run(Session(document), "DATALINKUPDATE", Enter, "K");

            Assert.False(session.IsRunning);
            Assert.Equal("1 data link(s) could not be updated.", session.History.Last());
            Assert.Contains(session.History, line => line.StartsWith("Parts: The file", StringComparison.Ordinal));
        }

        [Fact]
        public void TheDataLinkCommand_ShowsTheManagerThroughTheWindow_AndListsTheLinksWithNone()
        {
            CadDocument document = Linked(out _);
            CadCommandSession session = Session(document);
            CadDataLinkManagerRequest seen = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Payload is CadDataLinkManagerRequest request)
                {
                    seen = request;
                    e.Handled = true;
                }
            };

            Run(session, "DATALINK");

            Assert.Same(document, seen.Document);
            Assert.False(session.IsRunning);

            CadCommandSession bare = Run(Session(document), "DATALINK");
            Assert.Contains("Parts", bare.History);
            Assert.Equal("The Data Link Manager dialog is shown by the window.", bare.History.Last());
        }

        [Fact]
        public void MinusTableAsksTheDataLinkOptionOfItsColumnsPrompt_ListsTheLinks_AndMakesATableOfTheDataLinked()
        {
            CadDocument document = Linked(out DxfTable first);
            File.WriteAllText(At("parts.csv"), "Mark,Qty,Note\r\nB-12,2,x\r\nB-13,3,y\r\nB-14,4,z\r\n");

            CadCommandSession session = Run(Session(document), "-TABLE");
            Assert.Equal("Enter number of columns or [data Link] <5>:", session.PromptText);
            Run(session, "L");
            Assert.Equal("Enter the name of a data link or [?]:", session.PromptText);
            Run(session, "?");
            Assert.Equal(new[] { "Data Link Name", "---------------", "Parts" }, Tail(session.History, 3).ToArray());
            Run(session, "Nothing");
            Assert.Equal("Data link not found.", session.History.Last());
            Run(session, "Parts");
            Assert.Equal("Specify insertion point:", session.PromptText);
            Run(session, P(0, -200));

            Assert.False(session.IsRunning, session.PromptText);
            DxfTable made = document.Database.ModelSpace.OfType<DxfTable>().Single(t => t.Handle != first.Handle);
            Assert.Equal((4, 3), (made.NumRows, made.NumColumns));
            Assert.Equal("Mark", made.TextString(0, 0));
            Assert.Equal("z", made.TextString(3, 2));
            Assert.Equal("Parts", made.GetDataLink(0, 0).Name);
            Assert.Equal(new DxfCellRange(0, 0, 3, 2), made.GetDataLinkRange(3, 2));
            Assert.Equal(2, made.GetDataLink(0, 0).GetTargets().Count);

            Assert.True(document.Undo());
            Assert.Single(document.Database.ModelSpace.OfType<DxfTable>());
        }

        [Fact]
        public void MinusTableWithNoLinkInTheDrawing_SaysSoAndEnds()
        {
            CadDocument document = Blank();

            CadCommandSession session = Run(Session(document), "-TABLE", "L");

            Assert.False(session.IsRunning);
            Assert.Equal("To create a data link use the DATALINK command.", session.History.Last());
        }

        [Fact]
        public void TheInsertTableDialogFromADataLink_MakesTheTableOfItsData_AndFromDataExtractionStartsTheWizard()
        {
            CadDocument document = Linked(out DxfTable first);
            CadCommandSession session = Session(document);
            string answer = "Parts";
            session.UiRequested += (sender, e) =>
            {
                if (e.Payload is CadInsertTableRequest request)
                {
                    request.DataLink = answer;
                    request.FromDataExtraction = answer == "wizard";
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
                else if (e.Payload is CadDataExtractionRequest wizard)
                {
                    wizard.Action = CadDialogAction.Cancel;
                    e.Handled = true;
                    answer = "wizard-shown";
                }
            };

            Run(session, "TABLE");
            Assert.Equal("Specify insertion point:", session.PromptText);
            Run(session, P(0, -200));

            Assert.False(session.IsRunning);
            DxfTable made = document.Database.ModelSpace.OfType<DxfTable>().Single(t => t.Handle != first.Handle);
            Assert.Equal((3, 2), (made.NumRows, made.NumColumns));
            Assert.Equal("B-13", made.TextString(2, 0));
            Assert.NotNull(made.GetDataLink(2, 1));

            answer = "wizard";
            Run(session, "TABLE");
            Assert.False(session.IsRunning);
            Assert.Equal("wizard-shown", answer);
        }

        [Fact]
        public void ATableFromALinkThatIsGone_IsRefusedInWords()
        {
            CadDocument document = Linked(out _);
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Payload is CadInsertTableRequest request)
                {
                    request.DataLink = "Parts";
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };
            File.Delete(At("parts.csv"));

            Run(session, "TABLE");

            Assert.False(session.IsRunning);
            Assert.Contains("cannot be found", session.History.Last());
        }

        [Fact]
        public void TheCommandsAreInTheRegistry_ByTheirAutoCadNames()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            foreach (string name in new[] { "DATALINK", "DATALINKUPDATE", "DATAEXTRACTION", "-DATAEXTRACTION" })
            {
                Assert.Contains(registry.Commands, command => command.Name == name);
            }
        }
    }
}
