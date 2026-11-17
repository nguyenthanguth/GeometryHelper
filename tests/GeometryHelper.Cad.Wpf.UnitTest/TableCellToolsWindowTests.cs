using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Table Cell tab's Cut, Copy and Paste, Match Cell, Cell Locking, Cell Borders, Data Format, Insert (Block, Field,
    /// Formula) and Cell Styles, through the view models with every dialog stubbed, on a table of three columns 40 wide, its
    /// title, header and two rows of data made by TABLE at the origin.
    /// </summary>
    public sealed class TableCellToolsWindowTests
    {
        private static MainViewModel Opened(out CadDocument document)
        {
            var model = new MainViewModel();
            document = CadDocument.From(CadTemplate.CreateMetric(), "tables.dxf");
            model.OpenDocument(document);
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
            return model;
        }

        private static DxfTable TableOf(MainViewModel model) =>
            model.Document.ModelSpace.Block.OfType<DxfTable>().Single();

        private static GeoPoint2 Middle(MainViewModel model, int row, int column)
        {
            IReadOnlyList<GeoPoint3> c = TableOf(model).GetCellExtents(row, column, true);
            return new GeoPoint2((c[0].X + c[2].X) / 2.0, (c[0].Y + c[2].Y) / 2.0);
        }

        private static void Select(MainViewModel model, int row, int column)
        {
            Assert.True(model.SelectTableCellAt(Middle(model, row, column), false));
        }

        private static string Words(MainViewModel model, int row, int column) => TableOf(model).TextString(row, column);

        [Fact]
        public void CtrlC_ThenCtrlV_CopiesCells_AndCtrlXTakesTheirContents()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.True(CadTableEditor.SetText(document, TableOf(model).Handle, 2, 0, "HEA300").Succeeded);
            Assert.True(
                CadTableEditor
                    .SetAlignment(
                        document,
                        TableOf(model).Handle,
                        new DxfCellRange(2, 0, 2, 0),
                        DxfCellAlignment.MiddleRight
                    )
                    .Succeeded
            );

            // Paste with nothing held is not taken, for PASTECLIP to take.
            Select(model, 2, 0);
            Assert.False(model.TableCellKey("Paste", false));
            Assert.False(model.TableCell.PasteCommand.CanExecute(null));

            Assert.True(model.TableCellKey("Copy", false));
            Assert.NotNull(model.TableCellClip);
            Assert.Equal("A3", model.TableCellClip.Name);
            Assert.True(model.TableCell.PasteCommand.CanExecute(null));

            Select(model, 3, 2);
            Assert.True(model.TableCellKey("Paste", false));
            Assert.Equal("HEA300", Words(model, 3, 2));
            Assert.Equal(DxfCellAlignment.MiddleRight, TableOf(model).Alignment(3, 2));
            Assert.Equal("HEA300", Words(model, 2, 0));

            Select(model, 2, 0);
            Assert.True(model.TableCellKey("Cut", false));
            Assert.Equal(string.Empty, Words(model, 2, 0));
            Assert.True(model.TableCellClip.IsCut);
            Select(model, 3, 0);
            Assert.True(model.TableCellKey("Paste", false));
            Assert.Equal("HEA300", Words(model, 3, 0));

            Assert.True(document.Undo());
            Assert.Equal(string.Empty, Words(model, 3, 0));
        }

        [Fact]
        public void TheLockingMenu_LocksCells_AndALockedCellIsNotOpenedForTyping()
        {
            MainViewModel model = Opened(out CadDocument document);
            Select(model, 2, 1);
            Assert.Equal("Unlocked", model.TableCell.LockName);

            model.TableCell.LockCommand.Execute("Content Locked");
            Assert.Equal("Content Locked", model.TableCell.LockName);
            Assert.Equal(DxfCellState.ContentLocked, TableOf(model).GetCellState(2, 1));
            CadMenuItemViewModel locking = model.TableCell.Menu().Single(m => m.Header == "Locking");
            Assert.Equal(4, locking.Items.Count);
            Assert.True(locking.Items.Single(i => i.Header == "Content Locked").IsChecked);

            // F2 starts TABLEDIT, which says the cell is locked and opens no editor; the cells are let go with the command.
            Assert.False(model.TableCellKey("F2", false));
            Assert.False(model.IsMTextEditorShown);
            Select(model, 2, 1);
            model.TableCell.ClearContentsCommand.Execute(null);
            Assert.False(model.IsMTextEditorShown);

            model.TableCell.LockCommand.Execute("Content and Format Locked");
            Assert.Equal("Content and Format Locked", model.TableCell.LockName);
            model.TableCell.AlignCommand.Execute("Top Left");
            Assert.NotEqual("Top Left", model.TableCell.AlignmentName);

            model.TableCell.LockCommand.Execute("Unlocked");
            model.TableCell.AlignCommand.Execute("Top Left");
            Assert.Equal("Top Left", model.TableCell.AlignmentName);
        }

        [Fact]
        public void CellBorders_TheDialogGivesTheLinesChosenTheirLineweightAndColour_AndNoBorderHidesThemAll()
        {
            MainViewModel model = Opened(out CadDocument document);
            Select(model, 2, 0);
            model.TableCellKey("Right", true);
            CellBordersViewModel shown = null;
            model.ShowCellBorders = dialog =>
            {
                shown = dialog;
                Assert.Contains("ByLayer", dialog.Linetypes);
                Assert.False(dialog.IsValid);
                dialog.Outside = true;
                dialog.LineWeight = "0.35 mm";
                dialog.Color = dialog.ColorChoices.Single(c => c.Name == "Red");
                Assert.True(dialog.IsValid);
                dialog.Ok();
                return true;
            };

            model.TableCell.BordersDialogCommand.Execute(null);

            Assert.NotNull(shown);
            DxfTable table = TableOf(model);
            Assert.Equal(DxfLineWeight.LineWeight035, table.GridLineWeight(2, 0, DxfCellEdgeMasks.TopMask));
            Assert.Equal(1, table.GridColor(2, 1, DxfCellEdgeMasks.BottomMask).ColorIndex);
            Assert.True(table.GridVisibility(2, 0, DxfCellEdgeMasks.LeftMask));

            model.ShowCellBorders = dialog =>
            {
                dialog.NoBorder = true;
                dialog.Ok();
                return true;
            };
            model.TableCell.BordersDialogCommand.Execute(null);
            Assert.False(TableOf(model).GridVisibility(2, 0, DxfCellEdgeMasks.TopMask));
            Assert.False(TableOf(model).GridVisibility(2, 1, DxfCellEdgeMasks.RightMask));
        }

        [Fact]
        public void TheDataFormatDialog_WritesTheFormatStringOfItsOptions_AndShowsASampleOfIt()
        {
            var money = new DataFormatViewModel("A3", DxfDataType.Double, DxfUnitType.Unitless, string.Empty)
            {
                DataType = DataFormatViewModel.Kinds.Single(k => k.Name == "Currency"),
            };
            Assert.Equal("%lu2%pr2%ps[$,]%th44", money.Format);
            Assert.Equal("$1,234.57", money.Sample);

            money.Prefix = "EUR ";
            money.Thousand = "Space";
            money.Decimal = "Comma";
            Assert.Equal("%lu2%pr2%ps[EUR ,]%ds44%th32", money.Format);

            var percent = new DataFormatViewModel("A3", DxfDataType.Double, DxfUnitType.Unitless, string.Empty)
            {
                DataType = DataFormatViewModel.Kinds.Single(k => k.Name == "Percentage"),
            };
            Assert.Equal("%lu2%pr0%ps[,%]%ct8[100]", percent.Format);
            Assert.Equal("50%", percent.Sample);

            var angle = new DataFormatViewModel("A3", DxfDataType.Double, DxfUnitType.Angle, "%au1%pr0");
            Assert.Equal("Angle", angle.DataType.Name);
            Assert.Equal("Deg/Min/Sec", angle.AngleUnit);
            Assert.Equal(0, angle.Precision);
            Assert.Equal("%au1%pr0", angle.Format);

            var text = new DataFormatViewModel("A3", DxfDataType.String, DxfUnitType.Unitless, string.Empty);
            Assert.Equal("Text", text.DataType.Name);
            Assert.Equal(string.Empty, text.Format);
            Assert.False(text.HasPrecision);

            var date = new DataFormatViewModel("A3", DxfDataType.Date, DxfUnitType.Unitless, "%X");
            Assert.Equal("Time", date.DateFormat);
            Assert.Equal("%X", date.Format);
            Assert.True(date.HasDate);
        }

        [Fact]
        public void DataFormat_FromTheMenuOrTheDialog_GivesTheCellsAKindAndAFormat()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.True(CadTableEditor.SetText(document, TableOf(model).Handle, 2, 0, "1234.5").Succeeded);
            Select(model, 2, 0);

            model.TableCell.DataFormatCommand.Execute("Currency");
            Assert.Equal(DxfUnitType.Currency, TableOf(model).UnitType(2, 0));
            Assert.Equal("$1,234.50", Words(model, 2, 0));

            model.ShowDataFormat = dialog =>
            {
                Assert.Equal("Currency", dialog.DataType.Name);
                dialog.DataType = DataFormatViewModel.Kinds.Single(k => k.Name == "Decimal Number");
                dialog.Precision = 1;
                dialog.Thousand = "None";
                dialog.Ok();
                return true;
            };
            model.TableCell.DataFormatCommand.Execute(null);
            Assert.Equal("1234.5", Words(model, 2, 0));
            Assert.Equal("%lu2%pr1", TableOf(model).Format(2, 0));
        }

        [Fact]
        public void InsertBlock_TheDialogPutsABlockInTheCell_AndInsertField_TheFieldTheDialogMade()
        {
            MainViewModel model = Opened(out CadDocument document);
            DxfBlockTableRecord box = new DxfBlockTableRecord { Name = "BOX", Origin = new GeoPoint3(0, 0, 0) };
            document.Database.BlockTable.Add(box);
            box.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            Select(model, 2, 1);
            model.ShowInsertTableBlock = dialog =>
            {
                Assert.Contains("BOX", dialog.Blocks);
                Assert.DoesNotContain(dialog.Blocks, b => b.StartsWith("*", StringComparison.Ordinal));
                dialog.Name = "BOX";
                dialog.AutoFit = false;
                dialog.Scale = "2";
                dialog.Rotation = "90";
                dialog.Ok();
                return true;
            };

            model.TableCell.InsertBlockCommand.Execute(null);

            DxfTable table = TableOf(model);
            Assert.Equal(DxfTableCellType.BlockCell, table.CellType(2, 1));
            Assert.Equal("BOX", table.GetBlockTableRecord(2, 1).Name);
            Assert.Equal(2.0, table.BlockScale(2, 1), 9);
            Assert.Equal(Math.PI / 2.0, table.BlockRotation(2, 1), 9);

            Select(model, 3, 1);
            model.ShowField = dialog =>
            {
                dialog.Category = "Document";
                dialog.SelectedName = dialog.Names.Single(n => n.Name == "Filename");
                dialog.Close(CadFieldAction.Ok);
                return true;
            };
            model.TableCell.InsertFieldCommand.Execute(null);
            Assert.StartsWith("\\AcVar Filename", TableOf(model).GetFieldCode(3, 1, 0));
            Assert.Equal("tables.dxf", Words(model, 3, 1));
        }

        [Fact]
        public void InsertFormula_SumAsksForTheCornersOfARange_ThenTheCellShowsTheSum()
        {
            MainViewModel model = Opened(out CadDocument document);
            string handle = TableOf(model).Handle;
            Assert.True(CadTableEditor.SetText(document, handle, 2, 0, "3").Succeeded);
            Assert.True(CadTableEditor.SetText(document, handle, 3, 0, "4").Succeeded);
            Select(model, 4 - 1, 2);

            model.TableCell.InsertFormulaCommand.Execute("Sum");
            Assert.Equal("Select first corner of table cell range:", model.Session.PromptText);
            GeoPoint2 first = Middle(model, 2, 0);
            GeoPoint2 second = Middle(model, 3, 0);
            model.Execute(
                first.X.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ","
                    + first.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            model.Execute(
                second.X.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ","
                    + second.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );

            DxfTable table = TableOf(model);
            Assert.Equal("=Sum(A3:A4)", table.GetFormula(3, 2, 0));
            Assert.Equal(7, Convert.ToDouble(table.GetValue(3, 2, 0)), 9);
        }

        [Fact]
        public void TheCellStylesBox_ListsTheStylesCellStyles_GivesTheCellsOne_AndCreateAndManageMakeAndDeleteThem()
        {
            MainViewModel model = Opened(out CadDocument document);
            Select(model, 3, 1);
            TableCellViewModel cell = model.TableCell;
            Assert.Equal("Data", cell.CellStyle);
            Assert.Equal(
                new[] { "Title", "Header", "Data", TableCellViewModel.CreateNewStyle, TableCellViewModel.ManageStyles },
                cell.CellStyleChoices.ToArray()
            );

            model.ShowCreateCellStyle = dialog =>
            {
                Assert.Equal("Data", dialog.StartWith);
                dialog.Name = "Heavy";
                dialog.StartWith = "Header";
                dialog.Ok();
                return true;
            };
            cell.CellStyle = TableCellViewModel.CreateNewStyle;
            Assert.Contains("Heavy", cell.CellStyleChoices);

            cell.CellStyle = "Heavy";
            Assert.Equal("Heavy", cell.CellStyle);
            Assert.Equal("Heavy", model.TableCell.CellStyleName);
            Assert.Equal(DxfCellAlignment.MiddleCenter, TableOf(model).Alignment(3, 1));

            ManageCellStylesViewModel manage = null;
            model.ShowManageCellStyles = dialog =>
            {
                manage = dialog;
                Assert.Equal(new[] { "Title", "Header", "Data", "Heavy" }, dialog.Styles.ToArray());
                dialog.Selected = "Data";
                Assert.False(dialog.CanChange);
                dialog.Delete();
                Assert.NotEqual(string.Empty, dialog.Message);
                dialog.Selected = "Heavy";
                dialog.NewName = "Bold";
                dialog.Rename();
                Assert.Equal(new[] { "Title", "Header", "Data", "Bold" }, dialog.Styles.ToArray());
                dialog.Delete();
                Assert.Equal(new[] { "Title", "Header", "Data" }, dialog.Styles.ToArray());
                return true;
            };
            cell.CellStyle = TableCellViewModel.ManageStyles;
            Assert.NotNull(manage);
            Assert.Equal(new[] { "Title", "Header", "Data" }, CadTableEditor.CellStyleNames(TableOf(model)).ToArray());
        }

        [Fact]
        public void MatchCell_FromTheTab_StartsTheCommandWithTheCellSelectedAsItsSource()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.True(
                CadTableEditor
                    .SetAlignment(
                        document,
                        TableOf(model).Handle,
                        new DxfCellRange(2, 0, 2, 0),
                        DxfCellAlignment.BottomRight
                    )
                    .Succeeded
            );
            Select(model, 2, 0);

            model.TableCell.MatchCellCommand.Execute(null);
            Assert.Equal("Select destination cell:", model.Session.PromptText);
            GeoPoint2 target = Middle(model, 3, 2);
            model.Execute(
                target.X.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ","
                    + target.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            model.Escape();

            Assert.Equal(DxfCellAlignment.BottomRight, TableOf(model).Alignment(3, 2));
        }
    }
}
