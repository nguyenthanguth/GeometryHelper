using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The style forms as the design boards lay them out, with no window: the seven tabs of the Modify dialog with each field's
    /// variable and greyed state, the variables the design reads its own way (zeros left out, the alternate units' placement),
    /// the descriptions and notices, the Manager's list, and the style boxes of the Annotate tab.
    /// </summary>
    public sealed class StyleFormsDesignTests
    {
        [Fact]
        public void TheModifyDialog_HasTheDesignsSevenTabs_EachFieldNamingItsVariable()
        {
            MainViewModel model = Opened(out _);
            DimStyleEditViewModel modify = model.CreateDimStyleManager().CreateModify();

            Assert.Equal(new[] { "Lines", "Symbols and arrows", "Text", "Fit", "Primary units", "Alternate units", "Tolerances" }, modify.Tabs);
            Assert.Equal("Modify dimension style: Standard", modify.Title);
            Assert.Equal("Preview of Standard", modify.PreviewTitle);
            Assert.Equal(
                new[] { "Dimension lines", "Colour", "Linetype", "Lineweight", "Extend beyond ticks", "Baseline spacing", "Suppress dim line 1", "Suppress dim line 2",
                    "Extension lines", "Colour", "Linetype ext line 1", "Linetype ext line 2", "Lineweight", "Suppress ext line 1", "Suppress ext line 2",
                    "Extend beyond dim lines", "Offset from origin", "Fixed length extension lines", "Length" },
                modify.FieldsOf("Lines").Select(f => f.Label));
            Assert.Equal(
                new[] { "DIMCLRD", "DIMLTYPE", "DIMLWD", "DIMDLE", "DIMDLI", "DIMSD1", "DIMSD2", "DIMCLRE", "DIMLTEX1", "DIMLTEX2", "DIMLWE", "DIMSE1", "DIMSE2", "DIMEXE", "DIMEXO", "DIMFXLON", "DIMFXL" },
                modify.FieldsOf("Lines").Where(f => f.Kind != DimFieldKind.Header).Select(f => f.VariableText));
            Assert.Equal("DIMTIH · DIMTOH", modify.FieldsOf("Text").Single(f => f.Label == "Alignment").VariableText);
            Assert.Equal("DIMGAP<0", modify.FieldsOf("Text").Single(f => f.Label == "Draw frame around text").VariableText);
            Assert.True(modify.FieldsOf("Text").Single(f => f.Variable == "DIMTXSTY").HasTextStyleButton);
            Assert.Equal(0xFFFFFF, modify.FieldsOf("Lines").First(f => f.Variable == "DIMCLRD").SwatchRgb);
        }

        [Fact]
        public void AFieldIsGreyed_WhereTheValuesMakeItMeanNothing_AndComesBackWhenTheyDo()
        {
            MainViewModel model = Opened(out _);
            DimStyleEditViewModel modify = model.CreateDimStyleManager().CreateModify();
            DimFieldViewModel length = modify.FieldsOf("Lines").Single(f => f.Variable == "DIMFXL");
            DimFieldViewModel fraction = modify.FieldsOf("Primary units").Single(f => f.Variable == "DIMFRAC");
            DimFieldViewModel multiplier = modify.FieldsOf("Alternate units").Single(f => f.Variable == "DIMALTF");

            Assert.False(length.IsEnabled);
            Assert.False(fraction.IsEnabled);
            Assert.False(multiplier.IsEnabled);

            modify.FieldsOf("Lines").Single(f => f.Variable == "DIMFXLON").IsChecked = true;
            DimFieldViewModel unit = modify.FieldsOf("Primary units").Single(f => f.Variable == "DIMLUNIT");
            unit.SelectedChoice = unit.Choices.Single(c => c.Label == "Fractional");
            modify.FieldsOf("Alternate units").Single(f => f.Variable == "DIMALT").IsChecked = true;

            Assert.True(length.IsEnabled);
            Assert.True(fraction.IsEnabled);
            Assert.True(multiplier.IsEnabled);
        }

        [Fact]
        public void ZerosAndThePlacementOfAlternateUnits_AreWrittenIntoTheirVariables_AsTheDesignReadsThem()
        {
            MainViewModel model = Opened(out CadDocument document);
            DimStyleEditViewModel modify = model.CreateDimStyleManager().CreateModify();
            DimFieldViewModel zeros = modify.FieldsOf("Primary units").Single(f => f.Variable == "DIMZIN");
            DimFieldViewModel affix = modify.FieldsOf("Primary units").Single(f => f.Variable == "DIMPOST");
            DimFieldViewModel placement = modify.FieldsOf("Alternate units").Single(f => f.Label == "Alternate units shown");
            modify.FieldsOf("Alternate units").Single(f => f.Variable == "DIMALT").IsChecked = true;

            Assert.Equal(new[] { "None", "Leading", "Trailing", "Leading and trailing" }, zeros.Choices.Select(c => c.Label));
            zeros.SelectedChoice = zeros.Choices.Single(c => c.Label == "Trailing");
            affix.Text = "<> mm";
            placement.SelectedChoice = placement.Choices.Single(c => c.Label == "Below primary value");

            Assert.Equal("<> mm", affix.Text);
            Assert.Equal("DIMPOST \\X", placement.VariableText);
            modify.Tab = "alternate units";
            Assert.Equal("Standard · alternate units on: decimal, 2 places, multiplier 25.4, shown below the primary value.", modify.Description);
            Assert.True(modify.Ok());
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            Assert.Equal(8, style.Dimzin & 12);
            Assert.Equal("<> mm\\X", style.Dimpost);
            Assert.True(style.Dimalt);
        }

        [Fact]
        public void TheManager_ListsItsStylesWithTheirMarks_DescribesTheOneSelected_AndSaysWhatOkDoes()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.True(CadDimStyleEditor.Add(document, "GH_MODEL_10", "Standard", null).Succeeded);
            model.Execute("DLI");
            model.Execute("0,0");
            model.Execute("300,0");
            model.Execute("150,-30");
            DimStyleManagerViewModel manager = model.CreateDimStyleManager();

            Assert.Equal("Dimension style manager · styles.dxf", manager.Title);
            Assert.Equal("Current: Standard", manager.CurrentStyleText);
            Assert.Equal("2 styles · double-click a style to modify it", manager.Footer);
            Assert.Equal(new[] { false, true }, manager.Styles.Select(s => s.IsModel));
            Assert.True(manager.HasNotice);
            Assert.False(manager.IsNoticeWarning);
            Assert.Equal("Standard · arrows closed filled 0.18 · text 0.18 centred on the line · overall scale 1 · Used by 1 dimension.", manager.Description);
            Assert.Equal(
                "The preview redraws as a value changes. OK writes the style to the file and redraws the 1 dimension that uses it, as one undo step.",
                manager.CreateModify().Notice);

            Assert.False(manager.Delete());
            Assert.True(manager.IsNoticeWarning);
        }

        [Fact]
        public void TheAnnotateStyleBoxes_TickTheCurrentStyle_MarkAModelDrawingsStyle_AndEndInTheDialogsItem()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.True(CadDimStyleEditor.Add(document, "GH_MODEL_10", "Standard", null).Succeeded);
            model.CurrentDimStyle = "GH_MODEL_10";

            IReadOnlyList<StyleBoxItemViewModel> items = model.DimStyleItems;

            Assert.Equal(new[] { "Standard", "GH_MODEL_10", "Dimension style…" }, items.Select(i => i.Name));
            Assert.Equal(new[] { false, true, false }, items.Select(i => i.IsCurrent));
            Assert.Equal(new[] { string.Empty, "model", string.Empty }, items.Select(i => i.Hint));
            Assert.True(items.Last().IsDialog);
            Assert.Equal("Text style…", model.TextStyleItems.Last().Name);
            Assert.Equal("Multileader style…", model.MLeaderStyleItems.Last().Name);
        }

        [Fact]
        public void TheTextStyleDialog_ShowsEachStylesFont_AndAChangeNotAppliedIsNoticed()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.True(CadTextStyleEditor.Add(document, "GH_NOTES", "Standard").Succeeded);
            TextStyleViewModel dialog = model.CreateTextStyle();

            Assert.Equal("Text style · styles.dxf", dialog.Title);
            Assert.Equal("Current style: Standard · 2 styles", dialog.Footer);
            Assert.Equal(new[] { "txt.shx", "txt.shx" }, dialog.Styles.Select(s => s.Detail));
            Assert.False(dialog.HasNotice);

            dialog.SelectedStyle = dialog.Styles.Single(s => s.Name == "GH_NOTES");
            dialog.WidthFactor = "0.8";

            Assert.Equal("Preview · GH_NOTES", dialog.PreviewTitle);
            Assert.Equal("Unsaved changes to GH_NOTES. Apply redraws the 0 texts that use it, as one undo step.", dialog.Notice);
            Assert.True(dialog.IsNoticeWarning);
            Assert.True(dialog.Apply());
            Assert.False(dialog.HasNotice);
        }

        private static MainViewModel Opened(out CadDocument document)
        {
            var model = new MainViewModel();
            document = CadDocument.From(new DxfDatabase("AC1027"), "styles.dxf");
            model.OpenDocument(document);
            return model;
        }
    }
}
