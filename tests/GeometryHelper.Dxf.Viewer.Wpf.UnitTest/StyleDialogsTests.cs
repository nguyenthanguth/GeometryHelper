using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The style forms of C10 with no window: the Text Style dialog, the Dimension Style Manager with its New, Modify,
    /// Override and Compare dialogs, the style boxes of the ribbon, and STYLE and DIMSTYLE asking the window.
    /// </summary>
    public sealed class StyleDialogsTests
    {
        [Fact]
        public void TheTextStyleDialog_ListsTheStyles_AndApplyWritesAChangeAsOneUndoStep()
        {
            MainViewModel model = Opened(out CadDocument document);
            string before = document.Database.ToText();
            TextStyleViewModel dialog = model.CreateTextStyle();

            Assert.Equal(new[] { "Standard" }, dialog.Styles.Select(s => s.Name));
            Assert.Equal("Current text style:  Standard", dialog.CurrentStyleText);
            Assert.False(dialog.IsDirty);
            Assert.False(dialog.ApplyCommand.CanExecute(null));

            dialog.WidthFactor = "0.75";
            dialog.ObliqueAngle = "15";
            dialog.Backwards = true;
            dialog.UpsideDown = true;

            Assert.True(dialog.IsDirty);
            Assert.True(dialog.Apply());
            Assert.False(dialog.IsDirty);
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            Assert.Equal(0.75, standard.XScale, 12);
            Assert.Equal(15.0, standard.ObliquingAngle * 180.0 / Math.PI, 9);
            Assert.Equal(6, standard.FlagBits);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void NewOffersStyle1_RefusesATakenName_AndDeleteRefusesAStyleInUse()
        {
            MainViewModel model = Opened(out CadDocument document);
            TextStyleViewModel dialog = model.CreateTextStyle();
            var offered = new List<string>();
            dialog.AskName = (title, name) =>
            {
                offered.Add(name);
                return name;
            };

            dialog.NewCommand.Execute(null);
            Assert.False(dialog.New("STYLE1"));

            Assert.Equal(new[] { "style1" }, offered);
            Assert.Equal("Text style \"STYLE1\" already exists.", dialog.Notice);
            Assert.Equal(new[] { "Standard", "style1" }, dialog.Styles.Select(s => s.Name));
            Assert.Equal("style1", dialog.SelectedStyle.Name);

            Assert.True(dialog.SetCurrent());
            model.Execute("DT");
            model.Execute("0,0");
            model.Execute("2.5");
            model.Execute("0");
            model.Execute("PLATE");
            model.Execute(string.Empty);
            Assert.True(CadTextStyleEditor.SetCurrent(document, "Standard").Succeeded);
            dialog = model.CreateTextStyle();
            dialog.SelectedStyle = dialog.Styles.Single(s => s.Name == "style1");

            Assert.False(dialog.Delete());
            Assert.Equal("Style \"style1\" is in use by 1 text, and cannot be deleted.", dialog.Notice);
            dialog.Filter = TextStyleViewModel.StylesInUse;
            Assert.Equal(new[] { "Standard", "style1" }, dialog.Styles.Select(s => s.Name));
        }

        [Fact]
        public void AChangeNotApplied_IsAskedAbout_YesAppliesIt_NoDropsIt_CancelStays()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.True(CadTextStyleEditor.Add(document, "GH_NOTES", "Standard").Succeeded);
            TextStyleViewModel dialog = model.CreateTextStyle();
            dialog.SelectedStyle = dialog.Styles.Single(s => s.Name == "Standard");
            bool? answer = null;
            var asked = new List<string>();
            dialog.AskSave = question =>
            {
                asked.Add(question);
                return answer;
            };

            dialog.Height = "3.5";
            dialog.SelectedStyle = dialog.Styles.Single(s => s.Name == "GH_NOTES");
            Assert.Equal("Standard", dialog.SelectedStyle.Name);
            Assert.False(dialog.Close());

            answer = false;
            dialog.SelectedStyle = dialog.Styles.Single(s => s.Name == "GH_NOTES");
            Assert.Equal("GH_NOTES", dialog.SelectedStyle.Name);
            Assert.Equal(0.0, document.Database.TextStyleTable["Standard"].TextSize);

            answer = true;
            dialog.Height = "5";
            bool closed = false;
            dialog.CloseRequested += (s, ok) => closed = true;
            Assert.True(dialog.Close());

            Assert.True(closed);
            Assert.Equal(5.0, document.Database.TextStyleTable["GH_NOTES"].TextSize);
            Assert.Equal(4, asked.Count);
            Assert.Equal("The current style has been modified. Do you want to save your changes?", asked[0]);
        }

        [Fact]
        public void ATrueTypeFontChosen_IsWrittenWithItsFileAndBoldItalic_AndVerticalIsNotOffered()
        {
            MainViewModel model = Opened(out CadDocument document);
            TextStyleViewModel dialog = model.CreateTextStyle();
            Assert.Contains(dialog.FontNames, f => f.IsTrueType && f.Name == "Arial");

            dialog.FontName = "Arial";
            dialog.FontStyle = "Bold Italic";

            Assert.True(dialog.IsTrueType);
            Assert.False(dialog.IsVerticalEnabled);
            Assert.True(dialog.Apply());
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            Assert.Equal(CadTrueTypeFonts.FileOf("Arial"), standard.FileName);
            Assert.Equal(new DxfFontDescriptor("Arial", true, true, 0, 34), standard.Font);
            Assert.Contains("1000\nArial\n1071\n50331682\n", document.Database.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheManager_MakesANewStyleThroughItsDialog_ModifiesIt_AndComparesItWithItsStart()
        {
            MainViewModel model = Opened(out CadDocument document);
            DimStyleManagerViewModel manager = model.CreateDimStyleManager();
            string start = manager.SelectedStyle.Name;
            manager.AskName = ask =>
            {
                Assert.Equal("Copy of " + start, ask.Name);
                Assert.True(ask.AsksStartWith);
                ask.Name = "GH_25";
                return ask.Ok() ? ask.Name : null;
            };
            manager.ShowEditor = editor =>
            {
                Assert.Equal("New dimension style: GH_25", editor.Title);
                editor.Tab = "Symbols and arrows";
                editor.Fields.Single(f => f.Label == "First").SelectedChoice = editor
                    .Fields.Single(f => f.Label == "First")
                    .Choices.Single(c => c.Label == "Architectural tick");
                editor.Fields.Single(f => f.Label == "Arrow size").Text = "3";
                editor.Tab = "Primary units";
                editor.Fields.Single(f => f.Label == "Precision" && f.Variable == "DIMDEC").SelectedChoice = editor
                    .Fields.Single(f => f.Variable == "DIMDEC")
                    .Choices[1];
                return editor.Ok();
            };

            manager.NewCommand.Execute(null);

            Assert.Equal("GH_25", manager.SelectedStyle.Name);
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["GH_25"];
            Assert.Equal("_ArchTick", style.Dimblk.Name);
            Assert.Equal(3.0, style.Dimasz);
            Assert.Equal(1, style.Dimdec);
            DimStyleCompareViewModel compare = manager.CreateCompare();
            compare.Second = start;
            Assert.Equal(new[] { "DIMASZ", "DIMBLK", "DIMBLK1", "DIMBLK2", "DIMDEC" }, compare.Rows.Select(r => r[0]));
            Assert.Equal("Found 5 differences:", compare.Summary);

            DimStyleEditViewModel modify = manager.CreateModify();
            modify.Tab = "Text";
            modify.Fields.Single(f => f.Label == "Draw frame around text").IsChecked = true;
            Assert.True(modify.Ok());
            Assert.True(document.Database.DimStyleTable["GH_25"].Dimgap < 0.0);
        }

        [Fact]
        public void AModelDrawingsStyle_IsNoticed_AndOverrideWritesIntoTheDimensionsSelected()
        {
            MainViewModel model = Opened(out CadDocument document);
            string start = CadDimStyleEditor.Current(document.Database);
            Assert.True(CadDimStyleEditor.Add(document, "GH_MODEL_10", start, null).Succeeded);
            model.Execute("DLI");
            model.Execute("0,0");
            model.Execute("300,0");
            model.Execute("150,-30");
            string handle = document.Database.ModelSpace.OfType<DxfRotatedDimension>().Single().Handle;
            DimStyleManagerViewModel manager = model.CreateDimStyleManager();

            manager.SelectedStyle = manager.Styles.Single(s => s.Name == "GH_MODEL_10");
            Assert.Equal(
                "A style named GH_MODEL_… is made by the model drawing. Changing it is a hand edit that an update keeps; its settings remake the rest.",
                manager.Notice
            );
            Assert.True(manager.SelectedStyle.IsModel);
            Assert.Equal(manager.Notice, manager.CreateModify().Notice);

            DimStyleEditViewModel nothingSelected = manager.CreateOverride();
            Assert.StartsWith("Select dimensions before Override", nothingSelected.Notice, StringComparison.Ordinal);
            document.Selection.Add(new[] { handle });
            DimStyleEditViewModel editor = manager.CreateOverride();
            Assert.Equal("Override current style: " + start, editor.Title);
            editor.Tab = "Symbols and arrows";
            editor.Fields.Single(f => f.Label == "Arrow size").Text = "6";
            Assert.True(editor.Ok());

            Assert.Equal(6.0, ((DxfRotatedDimension)document.FindEntity(handle)).Dimasz);
            Assert.NotEqual(6.0, document.Database.DimStyleTable[start].Dimasz);
        }

        [Fact]
        public void TheStyleBoxes_ShowAndSetTheCurrentStyles_TheirLastItemAsksForTheDialog()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.True(CadTextStyleEditor.Add(document, "GH_NOTES", "Standard").Succeeded);
            var asked = new List<string>();
            model.StyleDialogRequested += (s, name) => asked.Add(name);

            Assert.Equal(new[] { "Standard", "GH_NOTES", MainViewModel.ManageTextStyles }, model.TextStyleNames);
            model.CurrentTextStyle = "GH_NOTES";
            Assert.Equal("GH_NOTES", document.Database.Textstyle);
            model.CurrentTextStyle = MainViewModel.ManageTextStyles;
            Assert.Equal("GH_NOTES", model.CurrentTextStyle);
            model.CurrentDimStyle = MainViewModel.ManageDimensionStyles;
            model.TextHeight = "3.5";
            Assert.Equal(3.5, model.Session.Remembered["text.height"]);
            model.TextHeight = "-1";
            Assert.Equal(3.5, model.Session.Remembered["text.height"]);

            model.Execute("ST");
            model.Execute("DDIM");

            Assert.Equal(new[] { "STYLE", "DIMSTYLE", "STYLE", "DIMSTYLE" }, asked);
            Assert.True(document.Undo());
            Assert.Equal("Standard", model.CurrentTextStyle);
        }

        [Fact]
        public void ThePreviews_AreDrawn()
        {
            MainViewModel model = Opened(out _);

            Assert.NotNull(model.CreateTextStyle().Preview);
            Assert.NotNull(model.CreateDimStyleManager().Preview);
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
