using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Tables in the window (C18a) with every dialog stubbed: TABLE through the Insert Table dialog's view model, the cell opened
    /// for typing with Tab moving on, the cells of a table selected with the Table Cell tab, the Table Style dialogs, and the
    /// dialogs and the tab laid out offscreen as the design draws them.
    /// </summary>
    public sealed class TableDialogTests
    {
        private static MainViewModel Opened(out CadDocument document)
        {
            var model = new MainViewModel();
            document = CadDocument.From(CadTemplate.CreateMetric(), "tables.dxf");
            model.OpenDocument(document);
            return model;
        }

        /// <summary>A table of three columns 40 wide, its title, header and two rows of data, made by TABLE at the origin through the stubbed dialog.</summary>
        private static string Table(MainViewModel model, CadDocument document)
        {
            model.ShowInsertTable = dialog =>
            {
                dialog.Columns = "3";
                dialog.ColumnWidth = "40";
                dialog.DataRows = "2";
                dialog.Ok();
                return true;
            };
            model.Execute("TABLE");
            model.Execute("0,0");
            model.Escape();
            return document.ModelSpace.Block.OfType<DxfTable>().Single().Handle;
        }

        [Fact]
        public void Table_ShowsTheInsertTableDialog_ThenOpensTheTitleInTheEditor_AndTabMovesOn()
        {
            MainViewModel model = Opened(out CadDocument document);
            InsertTableViewModel shown = null;
            model.ShowInsertTable = dialog =>
            {
                shown = dialog;
                Assert.Equal(new[] { "Standard" }, dialog.TableStyles.ToArray());
                Assert.Equal("5", dialog.Columns);
                Assert.Equal("63.5", dialog.ColumnWidth);
                Assert.True(dialog.ByPoint);
                dialog.DataRows = "0";
                Assert.False(dialog.IsValid);
                Assert.False(dialog.OkCommand.CanExecute(null));
                dialog.DataRows = "3";
                dialog.Ok();
                return true;
            };

            model.Execute("TB");
            Assert.NotNull(shown);
            Assert.Equal("Specify insertion point:", model.Session.PromptText);
            model.Execute("10,20");

            Assert.True(model.IsMTextEditorShown);
            Assert.True(model.TextEditor.Edit.IsTableCell);
            Assert.True(model.TextEditorTab.IsTabSelected);
            model.TextEditor.Editor.Type("PARTS");
            model.TextEditor.Edit.Move = CadTableCellMove.Next;
            model.CloseTextEditor(true);

            Assert.Equal(1, model.TextEditor.Edit.TableRow);
            model.TextEditor.Editor.Type("Mark");
            model.CloseTextEditor(true);
            Assert.False(model.IsCommandRunning);

            DxfTable table = document.ModelSpace.Block.OfType<DxfTable>().Single();
            Assert.Equal(5, table.NumRows);
            Assert.Equal("PARTS", table.TextString(0, 0));
            Assert.Equal("Mark", table.TextString(1, 0));
        }

        [Fact]
        public void AClickInACell_SelectsIt_WithTheTableCellTab_AndItsButtonsAndKeysEditTheTable()
        {
            MainViewModel model = Opened(out CadDocument document);
            string handle = Table(model, document);

            // The data cell B3: from x 40 to 80, and under the title (11) and the header (9) 9 high.
            Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));
            TableCellViewModel cell = model.TableCell;
            Assert.Equal("B3", cell.CellName);
            Assert.Equal("Data", cell.CellStyleName);
            Assert.Equal("Top Center", cell.AlignmentName);
            Assert.True(model.TableCellTab.IsTabVisible);
            Assert.True(model.TableCellTab.IsTabSelected);
            Assert.False(cell.CanMerge);

            Assert.True(model.TableCellKey("Right", true));
            Assert.Equal("B3:C3", model.TableCell.CellName);
            Assert.True(model.TableCell.CanMerge);
            model.TableCell.MergeAllCommand.Execute(null);
            Assert.True(((DxfTable)document.FindEntity(handle)).IsMergedCell(2, 2, out _));

            model.TableCell.InsertBelowCommand.Execute(null);
            Assert.Equal(5, ((DxfTable)document.FindEntity(handle)).NumRows);
            model.TableCell.Fill = model.TableCell.FillChoices.Single(f => f.Name == "Color 8");
            Assert.Equal("Color 8", model.TableCell.Fill.Name);
            model.TableCell.AlignCommand.Execute("Middle Left");
            Assert.Equal("Middle Left", model.TableCell.AlignmentName);

            Assert.True(CadTableEditor.SetText(document, handle, 2, 1, "HEA300").Succeeded);
            Assert.True(model.TableCellKey("Delete", false));
            Assert.Equal(string.Empty, ((DxfTable)document.FindEntity(handle)).TextString(2, 1));

            Assert.True(model.TableCellKey("Escape", false));
            Assert.Null(model.TableCell);
            Assert.False(model.TableCellTab.IsTabVisible);
            Assert.False(model.SelectTableCellAt(new GeoPoint2(500, 500), false));

            // Undone, the table loses the row put in, and a cell selected of a row gone is let go.
            Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));
            Assert.True(model.TableCellKey("F2", false));
            Assert.True(model.IsMTextEditorShown);
            Assert.Equal(2, model.TextEditor.Edit.TableRow);
            model.Escape();
        }

        [Fact]
        public void TableStyle_AsksTheWindow_AndTheDialogsMakeAndModifyAStyle()
        {
            MainViewModel model = Opened(out CadDocument document);
            var asked = new List<string>();
            model.StyleDialogRequested += (s, name) => asked.Add(name);
            model.Execute("TS");
            Assert.Equal(new[] { "TABLESTYLE" }, asked);

            TableStyleManagerViewModel manager = model.CreateTableStyleManager();
            Assert.Equal(new[] { "Standard" }, manager.Styles.Select(s => s.Name).ToArray());
            Assert.Equal("current", manager.Styles[0].Hint);
            Assert.False(manager.CanDelete);

            TableStyleModifyViewModel modified = null;
            manager.AskNewStyle = (offered, names) =>
            {
                Assert.Equal("Copy of Standard", offered);
                return Tuple.Create("GH_SCHEDULE", "Standard");
            };
            manager.ShowModify = dialog =>
            {
                modified = dialog;
                Assert.Equal("New Table Style: GH_SCHEDULE", dialog.Title);
                dialog.CellStyle = "Header";
                dialog.Fill = dialog.Fills.Single(f => f.Name == "Color 8");
                dialog.Tab = "Text";
                dialog.TextHeight = "3.5";
                dialog.Tab = "Borders";
                dialog.BorderColor = dialog.ColorChoices.Single(c => c.Name == "Red");
                dialog.BorderCommand.Execute("Outside");
                dialog.Direction = "Up";
                dialog.Ok();
                return true;
            };

            manager.NewCommand.Execute(null);
            Assert.NotNull(modified);
            var style = (DxfTableStyle)document.Database.TableStyleDictionary["GH_SCHEDULE"];
            Assert.Equal(3.5, style.TextHeight(DxfRowType.HeaderRow));
            Assert.Equal(8, style.BackgroundColor(DxfRowType.HeaderRow).ColorIndex);
            Assert.Equal(1, style.GridColor(DxfGridLineType.HorizontalTop, DxfRowType.HeaderRow).ColorIndex);
            Assert.Equal(DxfFlowDirection.BottomToTop, style.FlowDirection);
            Assert.Equal("GH_SCHEDULE", manager.SelectedStyle.Name);
            Assert.True(manager.CanDelete);

            // A drawing made new lists no DICTIONARYVAR: Set Current says so and the style stays as it was.
            manager.SetCurrent();
            Assert.Equal("Standard", manager.CurrentStyle);
            Assert.Contains("cannot be made current", manager.Message);

            manager.Delete();
            Assert.Equal(new[] { "Standard" }, manager.Styles.Select(s => s.Name).ToArray());
        }

        [Fact]
        public void TheDialogsAndTheTableCellTab_AreLaidOutAsTheDesignDrawsThem()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                MainViewModel model = Opened(out CadDocument document);
                Table(model, document);
                Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));

                var workspace = new CadWorkspace { DataContext = model };
                model.ShowInsertTable = dialog => true;
                Layout(workspace, 1600, 960);
                TableCellRibbon ribbon = Descendants(workspace).OfType<TableCellRibbon>().Single();
                List<Button> buttons = Descendants(ribbon).OfType<Button>().ToList();
                string[] greyed = { "Match Cell", "Cell Locking ▾", "Data Format ▾", "Block", "Field", "Formula ▾", "Manage Cell Contents", "Link Cell", "Download from Source" };

                foreach (string name in greyed)
                {
                    Assert.False(buttons.Single(b => name.Equals(b.Content)).IsEnabled, name);
                }

                foreach (string name in new[] { "Insert Above", "Insert Below", "Delete Row(s)", "Insert Left", "Insert Right", "Delete Column(s)", "Unmerge Cells", "Cell Borders ▾" })
                {
                    Assert.True(buttons.Single(b => name.Equals(b.Content)).IsEnabled, name);
                }

                Assert.False(buttons.Single(b => "Merge Cells ▾".Equals(b.Content)).IsEnabled);
                Assert.Contains(Descendants(workspace).OfType<ToggleButton>(), t => "Table Cell".Equals(t.Content) && t.Visibility == Visibility.Visible);

                var manager = new TableStyleWindow(model.CreateTableStyleManager());
                var insert = new InsertTableWindow(new InsertTableViewModel(new CadInsertTableRequest(document)));
                var modify = new TableStyleModifyWindow(new TableStyleModifyViewModel(document, "Standard", false));

                foreach ((Window window, FrameworkElement body) in new (Window, FrameworkElement)[] { (manager, manager.Body), (insert, insert.Body), (modify, modify.Body) })
                {
                    object context = window.DataContext;
                    window.Content = null;
                    body.DataContext = context;
                    Layout(body, window.Width, window.Height);
                    Assert.Single(Descendants(body).OfType<TableStylePreview>().Where(p => p.Settings != null).Take(1));
                    window.Close();
                }

                Button close = Descendants(manager.Body).OfType<Button>().Single(b => "Close".Equals(b.Content));
                Assert.Same(manager.FindResource("GhCloseButton"), close.Style);
                Assert.Contains(Descendants(insert.Body).OfType<RadioButton>(), r => "From a data link".Equals(r.Content) && !r.IsEnabled);
            });
        }

        private static void Layout(FrameworkElement element, double width, double height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}
