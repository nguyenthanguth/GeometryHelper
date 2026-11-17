using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Annotative text: the Annotative box of the Text Style dialog (the style's own flag) and the Annotative button of the MTEXT
    /// editor's Style panel, which makes an MTEXT being made annotative, drawn at the height its paper gives it at the annotation
    /// scale; an MTEXT already made, or a drawing older than AutoCAD 2007, keeps the button grey with the reason.
    /// </summary>
    public sealed class AnnotativeTextTests
    {
        private static MainViewModel Open()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(CadTemplate.CreateMetric(), "notes.dxf"), "notes.dxf");
            return model;
        }

        private static DxfMText NewMText(MainViewModel model, System.Action<TextEditorViewModel> inEditor)
        {
            model.StartCommand("MTEXT");
            model.Execute("0,100");
            model.Execute("200,40");
            TextEditorViewModel editor = model.TextEditor;
            editor.Editor.Type("NOTES");
            inEditor(editor);
            editor.CloseCommand.Execute(null);
            return model.Document.ModelSpace.Block.OfType<DxfMText>().Single();
        }

        [Fact]
        public void TheTextStyleDialog_MakesAStyleAnnotative_AndTakesItBack()
        {
            MainViewModel model = Open();
            CadTextStyleSettings settings = CadTextStyleSettings.Of(model.Document.Database.TextStyleTable["Standard"]);
            Assert.False(settings.Annotative);

            settings.Annotative = true;
            Assert.True(CadTextStyleEditor.Apply(model.Document, "Standard", settings).Succeeded);

            Assert.Equal(DxfAnnotativeStates.True, model.Document.Database.TextStyleTable["Standard"].Annotative);
            Assert.True(CadTextStyleSettings.Of(model.Document.Database.TextStyleTable["Standard"]).Annotative);

            settings.Annotative = false;
            Assert.True(CadTextStyleEditor.Apply(model.Document, "Standard", settings).Succeeded);
            Assert.Equal(DxfAnnotativeStates.False, model.Document.Database.TextStyleTable["Standard"].Annotative);

            Assert.True(model.Document.Undo());
            Assert.Equal(DxfAnnotativeStates.True, model.Document.Database.TextStyleTable["Standard"].Annotative);
        }

        [Fact]
        public void TheAnnotativeBox_ChangesTheStyleDialogsSettings_AndMarksItDirty()
        {
            MainViewModel model = Open();
            var dialog = new TextStyleViewModel(model.Document);

            Assert.True(dialog.IsAnnotativeEnabled);
            Assert.False(dialog.Annotative);
            dialog.Annotative = true;
            Assert.True(dialog.IsDirty);
        }

        [Fact]
        public void TheAnnotativeButton_OfAnMTextBeingMade_MakesItAnnotativeAtTheAnnotationScale()
        {
            MainViewModel model = Open();
            Assert.True(model.Document.Database.TextStyleTable.Has("Standard"));
            string before = model.Document.Database.Cannoscale;

            DxfMText made = NewMText(model, editor =>
            {
                Assert.True(editor.CanSetAnnotative);
                Assert.False(editor.IsAnnotative);
                editor.AnnotativeCommand.Execute(null);
                Assert.True(editor.IsAnnotative);
                Assert.True(editor.IsModified);
            });

            Assert.Equal(DxfAnnotativeStates.True, made.Annotative);
            Assert.Equal(before, model.Document.Database.Cannoscale);
        }

        [Fact]
        public void TheAnnotativeButton_PressedTwice_LeavesTheMTextAsItWas()
        {
            MainViewModel model = Open();

            DxfMText made = NewMText(model, editor =>
            {
                editor.AnnotativeCommand.Execute(null);
                editor.AnnotativeCommand.Execute(null);
                Assert.False(editor.IsAnnotative);
            });

            Assert.Equal(DxfAnnotativeStates.False, made.Annotative);
        }

        [Fact]
        public void AnMTextMadeInAnAnnotativeStyle_StartsWithTheButtonOn_AndItCanBeSwitchedOff()
        {
            MainViewModel model = Open();
            CadTextStyleSettings settings = CadTextStyleSettings.Of(model.Document.Database.TextStyleTable["Standard"]);
            settings.Annotative = true;
            Assert.True(CadTextStyleEditor.Apply(model.Document, "Standard", settings).Succeeded);

            DxfMText made = NewMText(model, editor =>
            {
                Assert.True(editor.IsAnnotative);
                editor.AnnotativeCommand.Execute(null);
                Assert.False(editor.IsAnnotative);
            });

            Assert.Equal(DxfAnnotativeStates.False, made.Annotative);
        }

        [Fact]
        public void TheAnnotativeButton_IsGreyForAnMTextAlreadyMade_WithTheReason()
        {
            var model = new MainViewModel();
            var mtext = new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Width = 55.0,
                Contents = "Plate",
            };
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(mtext);
            model.OpenDocument(CadDocument.From(database, "notes.dxf"), "notes.dxf");
            Assert.True(model.EditTextAt(mtext.Handle));

            TextEditorViewModel editor = model.TextEditor;

            Assert.False(editor.CanSetAnnotative);
            Assert.False(editor.AnnotativeCommand.CanExecute(null));
            Assert.Contains("AIOBJECTSCALEADD", editor.AnnotativeToolTip, System.StringComparison.Ordinal);
        }
    }
}
