using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Format of the Modify Table Style dialog, whose … button opens the Table Cell Format dialog and keeps its answer in the
    /// cell style shown: Data, Header or Title, written with the style when OK is pressed, one undo step.
    /// </summary>
    public sealed class TableStyleFormatTests
    {
        private static CadDocument Opened()
        {
            var model = new MainViewModel();
            var document = CadDocument.From(CadTemplate.CreateMetric(), "tables.dxf");
            model.OpenDocument(document);

            // A table made by TABLE gives a drawing made new its Standard table style.
            model.ShowInsertTable = dialog =>
            {
                dialog.Ok();
                return true;
            };
            model.Execute("TABLE");
            model.Execute("0,0");
            model.Escape();
            return document;
        }

        [Fact]
        public void TheFormatButton_IsGreyWhereTheWindowShowsNoFormatDialog()
        {
            var modify = new TableStyleModifyViewModel(Opened(), "Standard", false);

            Assert.False(modify.FormatCommand.CanExecute(null));
            Assert.Equal("General", modify.FormatText);
        }

        [Fact]
        public void TheFormatChosen_IsShown_KeptPerCellStyle_AndWrittenWhenOkIsPressed()
        {
            CadDocument document = Opened();
            var modify = new TableStyleModifyViewModel(document, "Standard", false);
            modify.ShowDataFormat = dialog =>
            {
                dialog.DataType = DataFormatViewModel.Kinds.Single(k => k.Name == "Currency");
                dialog.Ok();
                return true;
            };

            modify.CellStyle = "Data";
            Assert.True(modify.FormatCommand.CanExecute(null));
            modify.FormatCommand.Execute(null);

            Assert.StartsWith("Currency", modify.FormatText, System.StringComparison.Ordinal);
            modify.CellStyle = "Header";
            Assert.Equal("General", modify.FormatText);
            modify.CellStyle = "Data";
            Assert.StartsWith("Currency", modify.FormatText, System.StringComparison.Ordinal);

            modify.Ok();

            var style = (DxfTableStyle)document.Database.TableStyleDictionary["Standard"];
            Assert.Equal(DxfDataType.Double, style.DataType(DxfRowType.DataRow).DataType);
            Assert.Equal(DxfUnitType.Currency, style.DataType(DxfRowType.DataRow).UnitType);
            Assert.NotEqual(string.Empty, style.Format(DxfRowType.DataRow));
            Assert.Equal(DxfDataType.General, style.DataType(DxfRowType.HeaderRow).DataType);

            Assert.True(document.Undo());
            style = (DxfTableStyle)document.Database.TableStyleDictionary["Standard"];
            Assert.Equal(DxfDataType.General, style.DataType(DxfRowType.DataRow).DataType);
        }

        [Fact]
        public void ACancelledFormatDialog_ChangesNothing()
        {
            var modify = new TableStyleModifyViewModel(Opened(), "Standard", false);
            modify.ShowDataFormat = dialog =>
            {
                dialog.DataType = DataFormatViewModel.Kinds.Single(k => k.Name == "Currency");
                return false;
            };

            modify.FormatCommand.Execute(null);

            Assert.Equal("General", modify.FormatText);
        }
    }
}
