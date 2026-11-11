using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The window's side of the presenter's own data: the built-in font first in the Text Style dialog's list, NEW starting
    /// from the metric template, and AutoCAD's arrowheads, Closed and Origin indicator 2 among them, offered by the Dimension
    /// Style dialog and written as their blocks.
    /// </summary>
    public sealed class FontTemplateAndArrowTests
    {
        private static MainViewModel Opened(out CadDocument document)
        {
            var model = new MainViewModel();
            document = CadDocument.From(new DxfDatabase("AC1027"), "styles.dxf");
            model.OpenDocument(document);
            return model;
        }

        [Fact]
        public void TheBuiltInFontIsFirstInTheList_AndChosenIsWrittenAsGhsimplexShx()
        {
            MainViewModel model = Opened(out CadDocument document);
            TextStyleViewModel dialog = model.CreateTextStyle();

            FontItemViewModel first = dialog.FontNames[0];
            Assert.Equal("GeometryHelper Simplex (built-in)", first.Name);
            Assert.Equal(TextStyleViewModel.BuiltInFont, first.Name);
            Assert.False(first.IsTrueType);
            Assert.Single(dialog.FontNames, f => f.Name == TextStyleViewModel.BuiltInFont);

            dialog.FontName = TextStyleViewModel.BuiltInFont;

            Assert.False(dialog.IsTrueType);
            Assert.True(dialog.IsVerticalEnabled);
            Assert.True(dialog.Apply());
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            Assert.Equal("ghsimplex.shx", standard.FileName);
            Assert.Equal(string.Empty, standard.Font.TypeFace);

            TextStyleViewModel again = model.CreateTextStyle();
            Assert.Equal(TextStyleViewModel.BuiltInFont, again.FontName);
            Assert.Equal(TextStyleViewModel.BuiltInFont, again.FontNames[0].Name);
            Assert.False(again.IsDirty);
        }

        [Fact]
        public void NewStartsFromTheMetricTemplate_InMillimetresAndIso25()
        {
            var model = new MainViewModel();

            model.Execute("NEW");

            DxfDatabase database = model.Document.Database;
            Assert.Equal(DxfMeasurementValue.Metric, database.Measurement);
            Assert.NotNull(database.GetHeader("$MEASUREMENT"));
            Assert.Equal("ISO-25", database.Dimstyle);
            Assert.True(database.DimStyleTable.Has("ISO-25"));
            Assert.Equal(2.5, database.DimStyleTable["ISO-25"].Dimasz);
            Assert.False(model.Document.IsModified);
        }

        [Fact]
        public void TheDimensionStyleDialog_OffersAutoCadsTwentyArrowheads_AndClosedIsWrittenAsItsBlock()
        {
            MainViewModel model = Opened(out CadDocument document);
            DimStyleManagerViewModel manager = model.CreateDimStyleManager();
            string start = manager.SelectedStyle.Name;
            DimStyleEditViewModel editor = manager.CreateModify();
            editor.Tab = "Symbols and arrows";
            DimFieldViewModel first = editor.Fields.Single(f => f.Label == "First");

            Assert.Equal(CadArrowheads.Names, first.Choices.Select(c => c.Label).Take(20));
            Assert.Contains(first.Choices, c => c.Label == "Closed");
            Assert.Contains(editor.Fields.Single(f => f.Label == "Leader").Choices, c =>
                c.Label == "Origin indicator 2"
            );

            first.SelectedChoice = first.Choices.Single(c => c.Label == "Closed");
            Assert.True(editor.Ok());

            DxfDimStyleTableRecord style = document.Database.DimStyleTable[start];
            Assert.Equal("_Closed", style.Dimblk.Name);
            Assert.Equal(4, style.Dimblk.Count());
        }
    }
}
