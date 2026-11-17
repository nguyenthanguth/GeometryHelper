using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The dialogs of data links and data extraction driven with no window shown: the Data Link Manager and the Excel data link
    /// dialog (made, changed, renamed, deleted, updated), the Select a Data Link picker, the cell menu's Data Link and the ribbon's
    /// Link Cell and Download from Source, the Insert Table dialog's From a data link, and the eight pages of the Data Extraction
    /// wizard from Begin to Finish, with the windows laid out.
    /// </summary>
    public sealed class DataLinkWindowTests
    {
        private static DxfDatabase Database()
        {
            var database = new DxfDatabase("AC1032");
            database.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            database.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(200, 0, 0), new GeoVector3(0, 0, 1), 10.0));
            return database;
        }

        private static MainViewModel Open(TestFolder folder, bool withTable = false)
        {
            DxfDatabase database = Database();

            if (withTable)
            {
                database.ModelSpace.AppendEntity(new DxfTable(new GeoPoint3(0, 100, 0), 3, 2, 9.0, 30.0));
            }

            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan.dxf", database)));
            return model;
        }

        private static string Parts(TestFolder folder) =>
            folder.WriteText("parts.csv", "Mark,Qty\r\nB-12,2\r\nB-13,3\r\n");

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static void Lay(FrameworkElement element)
        {
            element.Measure(new Size(2000, 2000));
            element.Arrange(new Rect(element.DesiredSize));
            Assert.True(element.DesiredSize.Width > 0);
        }

        [Fact]
        public void TheExcelDialog_ReadsTheFilesSheets_ShowsAPreview_AndMakesTheLinkOnOk()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string file = Parts(folder);
                var dialog = new ExcelDataLinkViewModel(model.Document, null);

                Assert.Equal("New Excel Data Link: Data Link 1", dialog.Title);
                Assert.True(dialog.CanBeRelative);
                Assert.True(dialog.IsFullPath);
                dialog.File = file;
                Assert.Equal(new[] { "parts" }, dialog.Sheets);
                Assert.Equal("parts", dialog.Sheet);
                Assert.Equal("Mark | Qty\nB-12 | 2\nB-13 | 3", dialog.Preview);
                Assert.Equal(string.Empty, dialog.Message);

                dialog.IsCellRange = true;
                dialog.Range = "A2:B3";
                Assert.Equal("B-12 | 2\nB-13 | 3", dialog.Preview);
                dialog.Range = "A2:";
                Assert.StartsWith("Invalid Range", dialog.Message);

                dialog.Range = "A1:B2";
                dialog.Name = "Parts";
                dialog.IsRelativePath = true;
                dialog.AllowWritingToSource = false;
                bool closed = false;
                dialog.CloseRequested += (s, ok) => closed = ok;
                Assert.True(dialog.TryAccept());

                Assert.True(closed);
                CadDataLinkInfo link = Assert.Single(CadDataLinks.List(model.Document));
                Assert.Equal("parts.csv!parts!A1:B2", link.ConnectionString);
                Assert.Equal(0, link.UpdateOption & CadDataLinks.AllowSourceUpdate);
            }
        }

        [Fact]
        public void TheExcelDialog_ReadsAWorkbooksNamedRanges_AndRefusesANameInUseAndAFileThatIsNoWorkbook()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string book = Path.Combine(folder.FullPath, "book.xlsx");
                var sheet = new CadSheet("S");
                sheet.Set(0, 0, CadSheetCell.OfText("x"));
                CadWorkbook.Write(book, "Prices", sheet);
                var dialog = new ExcelDataLinkViewModel(model.Document, null) { Name = "Book" };

                dialog.File = book;
                Assert.Equal(new[] { "Prices" }, dialog.Sheets);
                Assert.Empty(dialog.NamedRanges);
                Assert.True(dialog.TryAccept());

                var again = new ExcelDataLinkViewModel(model.Document, null) { Name = "book", File = book };
                Assert.False(again.TryAccept());
                Assert.Equal("Data link name in use.", again.Message);

                string broken = folder.WriteText("broken.xlsx", "not a workbook");
                again.File = broken;
                Assert.StartsWith("The file selected is not a valid Excel file", again.Message);
            }
        }

        [Fact]
        public void TheManager_ListsTheLinks_CreatesEditsRenamesAndDeletes_AndRefusesToDeleteOneInUse()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, true);
                string file = Parts(folder);
                var manager = new DataLinkManagerViewModel(model.Document);
                string created = null;
                manager.ShowExcelLink = dialog =>
                {
                    dialog.Name = created ?? "Parts";
                    dialog.File = file;
                    return dialog.TryAccept();
                };

                Assert.Equal("Data Link Manager", manager.Title);
                Assert.True(manager.CanCreate);
                Assert.Empty(manager.Links);
                manager.Create();
                Assert.Equal("Parts", Assert.Single(manager.Links).Name);
                Assert.Equal("Parts", manager.Selected.Name);
                Assert.Equal("Excel", manager.Selected.Kind);
                Assert.Equal("not used", manager.Selected.Tables);
                Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", manager.Selected.Updated);

                // A link used by a table cannot be deleted, as AutoCAD's message says.
                string handle = model.Document.Database.ModelSpace.OfType<DxfTable>().Single().Handle;
                Assert.True(CadDataLinks.LinkCells(model.Document, handle, 0, 0, "Parts").Succeeded);
                manager.Refresh(null);
                Assert.Equal("1 table(s)", manager.Selected.Tables);
                manager.Confirm = question =>
                {
                    Assert.Equal("Are you sure you want to delete \"Parts\"?", question);
                    return true;
                };
                manager.Delete();
                Assert.Equal("The data link is being used and cannot be deleted.", manager.Message);
                Assert.Single(manager.Links);

                manager.AskName = present => "Fasteners";
                manager.Rename();
                Assert.Equal("Fasteners", manager.Selected.Name);
                Assert.Equal(string.Empty, manager.Message);

                File.WriteAllText(file, "Mark\r\nZ-1\r\n");
                manager.Update();
                Assert.Equal(string.Empty, manager.Message);
                Assert.Equal("Z-1", ((DxfTable)model.Document.FindEntity(handle)).TextString(1, 0));
                Assert.NotEqual("never", manager.Selected.Updated);

                Assert.True(CadDataLinks.Detach(model.Document, handle, 1, 0).Succeeded);
                manager.Confirm = question => false;
                manager.Refresh(null);
                manager.Delete();
                Assert.Single(manager.Links);
                manager.Confirm = question => true;
                manager.Delete();
                Assert.Empty(manager.Links);
            }
        }

        [Fact]
        public void TheManagerOfADrawingOfR2000_CannotCreate_AndOpenedToPickItGivesTheLinkChosen()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                CadDataLinks.Create(model.Document, new CadDataLinkSettings { Name = "A", File = Parts(folder) });
                CadDataLinks.Create(model.Document, new CadDataLinkSettings { Name = "B", File = Parts(folder) });
                var picker = new DataLinkManagerViewModel(model.Document, true);
                bool closed = false;
                picker.CloseRequested += (s, ok) => closed = ok;

                Assert.Equal("Select a Data Link", picker.Title);
                Assert.Equal("Cancel", picker.CloseText);
                Assert.False(picker.OkCommand.CanExecute(null));
                picker.Selected = picker.Links[1];
                Assert.True(picker.OkCommand.CanExecute(null));
                picker.OkCommand.Execute(null);

                Assert.True(closed);
                Assert.Equal("B", picker.ChosenName);

                CadDocument old = CadDocument.From(new DxfDatabase("AC1015"), "old.dxf");
                Assert.False(new DataLinkManagerViewModel(old).CanCreate);
            }
        }

        [Fact]
        public void TheDatalinkCommand_ShowsTheManagerThroughTheWindowsFunction()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                CadDataLinks.Create(model.Document, new CadDataLinkSettings { Name = "Parts", File = Parts(folder) });
                DataLinkManagerViewModel shown = null;
                model.ShowDataLinkManager = dialog => shown = dialog;

                model.Execute("DATALINK");

                Assert.Equal("Parts", Assert.Single(shown.Links).Name);
                Assert.False(model.IsCommandRunning);
            }
        }

        [Fact]
        public void LinkCellInTheCellRibbon_PicksALink_PutsItsDataInFromTheCell_AndDownloadAndUploadUseTheTablesLinks()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, true);
                string file = Parts(folder);
                CadDataLinks.Create(model.Document, new CadDataLinkSettings { Name = "Parts", File = file });
                string handle = model.Document.Database.ModelSpace.OfType<DxfTable>().Single().Handle;
                var cell = new TableCellViewModel(model, handle, 1, 0);
                model.PickDataLink = () => "Parts";

                cell.LinkCellCommand.Execute(null);

                var table = (DxfTable)model.Document.FindEntity(handle);
                Assert.Equal("Mark", table.TextString(1, 0));
                Assert.Equal("Parts", table.GetDataLink(2, 1).Name);

                File.WriteAllText(file, "Mark,Qty\r\nZ-1,9\r\nZ-2,8\r\nZ-3,7\r\n");
                cell.DownloadCommand.Execute(null);
                table = (DxfTable)model.Document.FindEntity(handle);
                Assert.Equal("Z-3", table.TextString(4, 0));

                CadTableEditor.SetText(model.Document, handle, 2, 1, "99");
                cell.UploadCommand.Execute(null);
                Assert.Equal("Mark,Qty\r\nZ-1,99\r\nZ-2,8\r\nZ-3,7\r\n", File.ReadAllText(file));

                // A cell linked already cannot be linked again, and detaching leaves its data.
                Assert.False(Item(cell.Menu(), "Data Links", "Link Cell…").IsEnabled);
                Item(cell.Menu(), "Data Links", "Detach Data Link").Command.Execute(null);
                table = (DxfTable)model.Document.FindEntity(handle);
                Assert.Null(table.GetDataLink(1, 0));
                Assert.Equal("Z-1", table.TextString(2, 0));
            }
        }

        private static CadMenuItemViewModel Item(
            System.Collections.Generic.IReadOnlyList<CadMenuItemViewModel> menu,
            string parent,
            string name
        ) =>
            menu.Single(i => i.Header == parent).Items.Single(i => i.Header.StartsWith(name, StringComparison.Ordinal));

        [Fact]
        public void TheCellMenusDataLink_IsNoLongerGrey_AndNamesTheLinkItEdits()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, true);
                CadDataLinks.Create(model.Document, new CadDataLinkSettings { Name = "Parts", File = Parts(folder) });
                string handle = model.Document.Database.ModelSpace.OfType<DxfTable>().Single().Handle;
                var cell = new TableCellViewModel(model, handle, 0, 0);

                // A cell linked to nothing has Data Link…, which picks a link; a linked one has the submenu Data Links.
                CadMenuItemViewModel menu = cell.Menu().Single(i => i.Header == "Data Link…");
                Assert.True(menu.IsEnabled);
                Assert.DoesNotContain(cell.Menu(), i => i.Header == "Data Links");

                Assert.True(CadDataLinks.LinkCells(model.Document, handle, 0, 0, "Parts").Succeeded);
                string edited = null;
                model.EditDataLink = name =>
                {
                    edited = name;
                    return true;
                };
                CadMenuItemViewModel edit = Item(cell.Menu(), "Data Links", "Edit Data Link");
                Assert.Equal("Edit Data Link \"Parts\"…", edit.Header);
                edit.Command.Execute(null);
                Assert.Equal("Parts", edited);
                Assert.True(Item(cell.Menu(), "Data Links", "Update Table Data Links").IsEnabled);
            }
        }

        [Fact]
        public void TheInsertTableDialogFromADataLink_AsksNoColumnsAndRows_AndTheRequestCarriesTheLink()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                CadDataLinks.Create(model.Document, new CadDataLinkSettings { Name = "Parts", File = Parts(folder) });
                model.ShowInsertTable = dialog =>
                {
                    Assert.True(dialog.HasDataLinks);
                    Assert.Equal(new[] { "Parts" }, dialog.DataLinks);
                    Assert.True(dialog.StartFromEmpty);
                    Assert.True(dialog.SetsColumnsAndRows);
                    dialog.FromDataLink = true;
                    Assert.False(dialog.SetsColumnsAndRows);
                    Assert.False(dialog.StartFromEmpty);
                    Assert.Equal("Parts", dialog.DataLink);
                    dialog.Columns = "0";
                    Assert.True(dialog.IsValid);
                    dialog.Ok();
                    return true;
                };

                model.Execute("TABLE");
                model.Execute("0,0");

                DxfTable table = model.Document.Database.ModelSpace.OfType<DxfTable>().Single();
                Assert.Equal((3, 2), (table.NumRows, table.NumColumns));
                Assert.Equal("B-13", table.TextString(2, 0));
                Assert.Equal("Parts", table.GetDataLink(0, 0).Name);
            }
        }

        [Fact]
        public void TheInsertTableDialogOfADrawingWithNoLink_GreysFromADataLink_AndFromDataExtractionStartsTheWizard()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                DataExtractionViewModel wizard = null;
                model.ShowDataExtraction = dialog =>
                {
                    wizard = dialog;
                    dialog.Cancel();
                };
                model.ShowInsertTable = dialog =>
                {
                    Assert.False(dialog.HasDataLinks);
                    dialog.FromDataExtraction = true;
                    Assert.True(dialog.IsValid);
                    dialog.Ok();
                    return true;
                };

                model.Execute("TABLE");

                Assert.NotNull(wizard);
                Assert.False(model.IsCommandRunning);
            }
        }

        /// <summary>Drives the wizard through its pages for the lines and circle of <see cref="Database"/>, the settings it makes returned.</summary>
        private static CadExtractionSettings Wizard(DataExtractionViewModel wizard, string dxe, string csv)
        {
            Assert.Equal(DataExtractionPage.Begin, wizard.Page);
            Assert.Equal("Data Extraction - Begin (Page 1 of 8)", wizard.PageTitle);
            Assert.True(wizard.IsCreateNew);
            Assert.False(wizard.BackCommand.CanExecute(null));
            wizard.AskSaveAs = offered => dxe;
            wizard.Next();

            Assert.Equal(DataExtractionPage.DataSource, wizard.Page);
            Assert.True(wizard.IncludeCurrentDrawing);
            Assert.True(wizard.ExtractFromBlocks);
            wizard.Next();

            Assert.Equal(DataExtractionPage.SelectObjects, wizard.Page);
            Assert.Equal(new[] { "Circle", "Line" }, wizard.Types.Select(t => t.Name));
            Assert.Equal(new[] { "1", "2" }, wizard.Types.Select(t => t.Count));
            wizard.Types.Single(t => t.Name == "Circle").IsChecked = false;
            wizard.Next();

            Assert.Equal(DataExtractionPage.SelectProperties, wizard.Page);
            Assert.Equal(
                new[] { "Attribute", "Dynamic Block", "Drawing", "General", "Geometry", "Misc" },
                wizard.Categories.Select(c => c.Name)
            );
            Assert.All(wizard.Properties, p => Assert.NotEqual("Dynamic Block", p.Category));
            wizard.CheckAllProperties(false);
            wizard.Properties.Single(p => p.Key == "General/Layer").IsChecked = true;
            wizard.Properties.Single(p => p.Key == "Geometry/Length").IsChecked = true;
            wizard.Next();

            Assert.Equal(DataExtractionPage.RefineData, wizard.Page);
            Assert.Equal(new[] { "Count", "Name", "Layer", "Length" }, wizard.Columns.Select(c => c.Header));
            Assert.Equal("Count | Name | Layer | Length\n2 | Line | 0 | 100", wizard.Preview);
            wizard.Columns.Single(c => c.Key == "Count").Header = "Qty";
            wizard.CommitColumns();
            wizard.SelectedColumn = wizard.Columns.Single(c => c.Key == "General/Layer");
            wizard.MoveColumn(true);
            Assert.Equal(new[] { "Qty", "Name", "Length", "Layer" }, wizard.Columns.Select(c => c.Header));
            wizard.SortColumn = "Geometry/Length";
            wizard.SortDescending = true;
            wizard.FilterColumn = "Name";
            wizard.FilterCondition = "Equal to";
            wizard.FilterValue = "Line";
            Assert.Equal("Qty | Name | Length | Layer\n2 | Line | 100 | 0", wizard.Preview);
            wizard.Next();

            Assert.Equal(DataExtractionPage.ChooseOutput, wizard.Page);
            wizard.InsertTable = false;
            wizard.Finish();
            Assert.Equal("Choose an output: a table in the drawing, a file, or both.", wizard.Message);
            wizard.OutputFile = csv;
            wizard.OutputToFile = true;
            wizard.Next();

            Assert.Equal(DataExtractionPage.TableStyle, wizard.Page);
            wizard.Title = "Lines";
            wizard.Next();
            Assert.Equal(DataExtractionPage.Finish, wizard.Page);
            Assert.False(wizard.NextCommand.CanExecute(null));
            Assert.True(wizard.FinishCommand.CanExecute(null));
            return wizard.Settings;
        }

        [Fact]
        public void TheWizard_GoesThroughItsEightPages_AndFinishSavesTheDxeAndWritesTheFile()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string dxe = Path.Combine(folder.FullPath, "lines.dxe");
                string csv = Path.Combine(folder.FullPath, "lines.csv");
                model.ShowDataExtraction = dialog =>
                {
                    CadExtractionSettings settings = Wizard(dialog, dxe, csv);
                    Assert.Equal(new[] { "Line" }, settings.SelectedTypes);
                    Assert.Equal(new[] { "General/Layer", "Geometry/Length" }, settings.SelectedProperties);
                    Assert.Equal("Qty", settings.ColumnNames["Count"]);
                    Assert.Equal(new[] { "Count", "Name", "Geometry/Length", "General/Layer" }, settings.ColumnOrder);
                    dialog.Finish();
                };

                model.Execute("DATAEXTRACTION");

                Assert.False(model.IsCommandRunning);
                Assert.Equal("Qty,Name,Length,Layer\r\n2,Line,100,0\r\n", File.ReadAllText(csv));
                CadExtractionSettings saved = CadExtractionSettings.Load(dxe);
                Assert.Equal("Lines", saved.Title);
                Assert.Equal(CadFilterCondition.Equal, saved.Filters.Single().Condition);
                Assert.True(saved.Sorts.Single().Descending);
                Assert.Empty(model.Document.Database.ModelSpace.OfType<DxfTable>());
            }
        }

        [Fact]
        public void TheWizardsTableOutput_AsksTheInsertionPoint_AndTheTableIsLinkedToTheDxe()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string dxe = Path.Combine(folder.FullPath, "lines.dxe");
                model.ShowDataExtraction = dialog =>
                {
                    dialog.AskSaveAs = offered => dxe;
                    dialog.Next();
                    dialog.Next();
                    dialog.Next();
                    dialog.Next();
                    dialog.Next();
                    Assert.True(dialog.InsertTable);
                    dialog.Next();
                    dialog.Title = "All objects";
                    dialog.Finish();
                };

                model.Execute("DATAEXTRACTION");
                Assert.True(model.IsCommandRunning);
                Assert.Equal("Specify insertion point:", model.Session.PromptText);
                model.Execute("0,0");

                DxfTable table = model.Document.Database.ModelSpace.OfType<DxfTable>().Single();
                Assert.Equal("All objects", table.TextString(0, 0));
                Assert.Equal("Count", table.TextString(1, 0));
                Assert.Equal("plan.dxf", Path.GetFileName(model.Document.FileName));
                Assert.Equal("lines.dxe", table.GetDataLink(2, 0).ConnectionString);
                Assert.Equal("Data Extraction - lines.dxe", table.GetDataLink(2, 0).Name);
            }
        }

        [Fact]
        public void SelectObjectsOnThePage_ClosesTheWizardForThePicking_AndItIsShownAgainAtTheSamePage()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string line = model.Document.Database.ModelSpace.OfType<DxfLine>().First().Handle;
                int shown = 0;
                string dxe = Path.Combine(folder.FullPath, "picked.dxe");
                model.ShowDataExtraction = dialog =>
                {
                    shown++;

                    if (shown == 1)
                    {
                        dialog.AskSaveAs = offered => dxe;
                        dialog.Next();
                        Assert.Equal(DataExtractionPage.DataSource, dialog.Page);
                        dialog.SelectObjects();
                        return;
                    }

                    Assert.Equal(DataExtractionPage.DataSource, dialog.Page);
                    Assert.True(dialog.UseSelectedObjects);
                    Assert.Equal("1 object(s) selected", dialog.NumberSelected);
                    dialog.Next();
                    Assert.Equal(new[] { "Line" }, dialog.Types.Select(t => t.Name));
                    dialog.Cancel();
                };

                model.Execute("DATAEXTRACTION");
                Assert.True(model.IsCommandRunning);
                Assert.Equal("Select objects:", model.Session.PromptText);
                model.Session.FeedSelection(new[] { line });
                model.Session.Enter();

                Assert.Equal(2, shown);
                Assert.False(model.IsCommandRunning);
            }
        }

        [Fact]
        public void TheBeginPage_EditsAnExistingDxe_RefusesOneAutoCadWrote_AndCancelWritesNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string dxe = Path.Combine(folder.FullPath, "old.dxe");
                new CadExtractionSettings
                {
                    Title = "From file",
                    SelectedTypes = new System.Collections.Generic.List<string> { "Circle" },
                }.Save(dxe);
                string acad = Path.Combine(folder.FullPath, "acad.dxe");
                File.WriteAllBytes(acad, new byte[] { 0, 1, 0, 0, 0, 255, 255, 255, 255 });
                model.ShowDataExtraction = dialog =>
                {
                    dialog.IsEditExisting = true;
                    Assert.True(dialog.NeedsFile);
                    dialog.Next();
                    Assert.Equal("Choose a data extraction file (.dxe).", dialog.Message);
                    dialog.ExistingFile = acad;
                    dialog.Next();
                    Assert.Contains("not a data extraction file", dialog.Message);
                    Assert.Equal(DataExtractionPage.Begin, dialog.Page);
                    dialog.ExistingFile = dxe;
                    dialog.Next();
                    Assert.Equal(DataExtractionPage.DataSource, dialog.Page);
                    Assert.Equal(dxe, dialog.DxeFile);
                    Assert.Equal("From file", dialog.Title);
                    dialog.Next();
                    Assert.Equal(new[] { "Circle", "Line" }, dialog.Types.Select(t => t.Name));
                    Assert.Equal(new[] { true, false }, dialog.Types.Select(t => t.IsChecked));
                    dialog.Cancel();
                };

                model.Execute("DATAEXTRACTION");

                Assert.False(model.IsCommandRunning);
                Assert.Empty(model.Document.Database.ModelSpace.OfType<DxfTable>());
            }
        }

        [Fact]
        public void TheDisplayOptionsAndTheCategoryFilter_ReListWhatTheWizardShows()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                var request = new CadDataExtractionRequest(model.Document);
                var wizard = new DataExtractionViewModel(request) { AskSaveAs = offered => null };

                wizard.Next();
                Assert.Equal(DataExtractionPage.Begin, wizard.Page);
                wizard.AskSaveAs = offered => Path.Combine(folder.FullPath, "x");
                wizard.Next();
                Assert.EndsWith("x.dxe", wizard.DxeFile);
                wizard.Next();
                wizard.DisplayBlocksOnly = true;
                Assert.Empty(wizard.Types);
                wizard.DisplayNonBlocksOnly = true;
                Assert.Equal(new[] { "Circle", "Line" }, wizard.Types.Select(t => t.Name));
                wizard.DisplayAll = true;
                wizard.DisplayUsedOnly = true;
                wizard.Next();

                Assert.Contains(wizard.Properties, p => p.Category == "Drawing");
                wizard.Categories.Single(c => c.Name == "Drawing").IsChecked = false;
                wizard.CategoriesChanged();
                Assert.DoesNotContain(wizard.Properties, p => p.Category == "Drawing");
                wizard.Back();
                wizard.Next();
                Assert.False(wizard.Categories.Single(c => c.Name == "Drawing").IsChecked);
            }
        }

        [Fact]
        public void TheNewWindows_LoadTheirLayoutsAndLayOutWithNoWindowShown()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                CadDataLinks.Create(model.Document, new CadDataLinkSettings { Name = "Parts", File = Parts(folder) });
                string dxe = Path.Combine(folder.FullPath, "w.dxe");

                OnSta(() =>
                {
                    var excel = new ExcelDataLinkViewModel(model.Document, "Parts");
                    Lay(new ExcelDataLinkWindow(excel).Body);
                    Lay(new DataLinkManagerWindow(new DataLinkManagerViewModel(model.Document)).Body);
                    Lay(new DataLinkManagerWindow(new DataLinkManagerViewModel(model.Document, true)).Body);
                    Lay(new DataLinkNameWindow(null).Body);

                    var wizard = new DataExtractionViewModel(new CadDataExtractionRequest(model.Document));
                    var window = new DataExtractionWindow(wizard);
                    wizard.AskSaveAs = offered => dxe;

                    for (int page = 0; page < 8; page++)
                    {
                        Lay(window.Body);

                        if (page < 7)
                        {
                            wizard.Next();
                        }
                    }

                    var insert = new InsertTableViewModel(new CadInsertTableRequest(model.Document));
                    Lay(new InsertTableWindow(insert).Body);
                });
            }
        }
    }
}
