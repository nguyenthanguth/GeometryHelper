using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The clipboard between the window's drawings, Paste Special, the TEXT edit box and the MTEXT editor with its Text Editor tab, driven through the view model.</summary>
    public class ClipboardAndTextEditorTests
    {
        private static CadDocument Drawing(string name, params DxfEntity[] entities)
        {
            var database = new DxfDatabase("AC1027");

            foreach (DxfEntity entity in entities)
            {
                database.ModelSpace.AppendEntity(entity);
            }

            return CadDocument.From(database, name);
        }

        private static DxfLine Line(double x0, double y0, double x1, double y1)
            => new DxfLine { StartPoint = new GeoPoint3(x0, y0, 0), EndPoint = new GeoPoint3(x1, y1, 0) };

        [Fact]
        public void ObjectsCopiedInOneDrawing_ArePastedIntoAnother_ThroughTheWindowsOneClipboard()
        {
            var model = new MainViewModel();
            CadDocument first = Drawing("first.dxf", Line(0, 0, 100, 0));
            CadDocument second = Drawing("second.dxf");
            model.OpenDocument(first, "first.dxf");
            model.Execute("COPYCLIP");
            model.Execute("ALL");
            model.Enter();

            model.OpenDocument(second, "second.dxf");
            model.Execute("PASTECLIP");
            model.Execute("500,500");

            DxfLine pasted = Assert.IsType<DxfLine>(Assert.Single(second.ModelSpace.Block.Cast<DxfEntity>()));
            Assert.Equal(500.0, pasted.StartPoint.X, 9);
            Assert.Equal(600.0, pasted.EndPoint.X, 9);
            Assert.True(second.IsModified);
        }

        [Fact]
        public void PasteSpecial_AsksTheDialog_AndPastesAsTheWayChosen()
        {
            var model = new MainViewModel();
            CadDocument document = Drawing("plan.dxf", Line(0, 0, 100, 0));
            model.OpenDocument(document, "plan.dxf");
            model.Execute("COPYCLIP");
            model.Execute("ALL");
            model.Enter();
            PasteSpecialViewModel shown = null;
            model.ShowPasteSpecial = dialog =>
            {
                shown = dialog;
                return "PASTEBLOCK";
            };

            model.StartCommand("PASTESPEC");
            model.Execute("0,300");

            Assert.Equal("1 object copied from plan.dxf · Model", shown.SourceDetail);
            Assert.False(shown.Formats.Single(f => f.Name == "Image (PNG)").IsEnabled);
            DxfBlockReference insert = Assert.Single(document.ModelSpace.Block.Cast<DxfEntity>().OfType<DxfBlockReference>());
            Assert.StartsWith("A$C", insert.Name);
        }

        [Fact]
        public void PasteSpecialOfText_OffersTheTextFormatsOnly()
        {
            var dialog = new PasteSpecialViewModel(new CadClipboardData(null, "NOTE"));

            Assert.Equal("Multiline text (MTEXT)", dialog.Selected.Name);
            Assert.Equal("TEXT", dialog.Answer);
            Assert.False(dialog.Formats[0].IsEnabled);
            dialog.Selected = dialog.Formats[1];
            Assert.Equal("TEXT", dialog.Answer);
        }

        [Fact]
        public void ADoubleClickOnAText_OpensItsEditBox_AndEnterWritesTheWords()
        {
            var model = new MainViewModel();
            var text = new DxfDBText { Position = new GeoPoint3(10, 20, 0), Height = 5, TextString = "ROOM 1" };
            CadDocument document = Drawing("rooms.dxf", text);
            model.OpenDocument(document, "rooms.dxf");

            Assert.True(model.EditTextAt(text.Handle));
            Assert.True(model.IsTextEditShown);
            Assert.Equal("ROOM 1", model.InPlaceEdit.Value);

            model.CommitInPlace("ROOM 2");

            Assert.Equal("ROOM 2", text.TextString);
            Assert.False(model.IsTextEditShown);
            Assert.Equal("Select an annotation object or [Undo/Mode]:", model.Prompt);
            model.Escape();
            Assert.True(model.Undo());
            Assert.Equal("ROOM 1", ((DxfDBText)document.FindEntity(text.Handle)).TextString);
        }

        [Fact]
        public void TheMTextCommand_OpensTheEditorAndItsTab_AndCloseWritesTheCodes()
        {
            var model = new MainViewModel();
            CadDocument document = Drawing("notes.dxf");
            model.OpenDocument(document, "notes.dxf");

            model.StartCommand("MTEXT");
            model.Execute("0,100");
            model.Execute("200,40");

            Assert.True(model.IsMTextEditorShown);
            Assert.True(model.TextEditorTab.IsTabVisible);
            Assert.Same(model.TextEditorTab, model.SelectedTab);
            TextEditorViewModel editor = model.TextEditor;
            editor.Editor.Type("NOTES");
            editor.Editor.SelectAll();
            editor.SelectedColor = editor.Colors.Single(c => c.Name == "Red");
            editor.ToggleCommand.Execute("Underline");
            editor.CloseCommand.Execute(null);

            DxfMText mtext = Assert.IsType<DxfMText>(Assert.Single(document.ModelSpace.Block.Cast<DxfEntity>()));
            Assert.Equal(@"{\C1;\LNOTES\l}", mtext.Contents);
            Assert.False(model.IsMTextEditorShown);
            Assert.False(model.TextEditorTab.IsTabVisible);
            Assert.True(model.IsHomeTabSelected);
            Assert.Equal("MTEXT", document.NextUndo.Name);
        }

        [Fact]
        public void EscInTheMTextEditor_AsksWhereThereAreChanges_AndDontSaveKeepsTheMText()
        {
            var model = new MainViewModel();
            var mtext = new DxfMText { Location = new GeoPoint3(0, 0, 0), TextHeight = 2.5, Contents = "PLATE" };
            CadDocument document = Drawing("plate.dxf", mtext);
            model.OpenDocument(document, "plate.dxf");
            int asked = 0;
            bool? answer = null;
            model.AskKeepMTextChanges = () =>
            {
                asked++;
                return answer;
            };

            Assert.True(model.EditTextAt(mtext.Handle));
            model.TextEditor.Editor.MoveTo(5);
            model.TextEditor.Editor.Type(" 10");
            model.EscapeTextEditor();
            Assert.Equal(1, asked);
            Assert.True(model.IsMTextEditorShown);

            answer = false;
            model.EscapeTextEditor();

            Assert.Equal(2, asked);
            Assert.False(model.IsMTextEditorShown);
            Assert.Equal("PLATE", ((DxfMText)document.FindEntity(mtext.Handle)).Contents);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TheJustificationAndTheWidthTheEditorSet_AreWrittenWithTheWordsInOneStep()
        {
            var model = new MainViewModel();
            var mtext = new DxfMText { Location = new GeoPoint3(0, 0, 0), TextHeight = 2.5, Width = 50, Contents = "PLATE" };
            CadDocument document = Drawing("plate.dxf", mtext);
            model.OpenDocument(document, "plate.dxf");
            Assert.True(model.EditTextAt(mtext.Handle));
            TextEditorViewModel editor = model.TextEditor;

            editor.Menu("Justification").Single(i => i.Header == "Middle Center MC").Command.Execute(null);
            editor.Width = 80.0;
            editor.CloseCommand.Execute(null);

            DxfMText written = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, written.Attachment);
            Assert.Equal(80.0, written.Width, 9);
            Assert.Equal("PLATE", written.Contents);
            Assert.True(document.Undo());
            Assert.Equal(50.0, ((DxfMText)document.FindEntity(mtext.Handle)).Width, 9);
        }
    }
}
