using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The MTEXT editor's Background Mask dialog, as AutoCAD 2021's: Use background mask, the border offset factor from 1.0 to
    /// 5.0 and the fill colour (the drawing's background or one picked). Driven through its model with no real dialog; what it
    /// sets is shown in the editor at once and written with the words, one undo step of the drawing.
    /// </summary>
    public sealed class MTextMaskTests
    {
        private static MainViewModel Edited(DxfMText mtext, out CadDocument document)
        {
            var model = new MainViewModel();
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(mtext);
            document = CadDocument.From(database, "notes.dxf");
            model.OpenDocument(document, "notes.dxf");
            Assert.True(model.EditTextAt(mtext.Handle));
            return model;
        }

        private static DxfMText Notes() =>
            new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Width = 55.0,
                Contents = "Plate PL10",
            };

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException(failure.ToString());
            }
        }

        [Fact]
        public void TheDialog_GreysTheFactorAndTheColourWhileTheMaskIsOff_AndTheColourWhileTheDrawingBackgroundIsUsed()
        {
            var dialog = new MTextMaskViewModel(null);
            Assert.False(dialog.UseMask);
            Assert.False(dialog.IsOffsetEnabled);
            Assert.False(dialog.IsDrawingBackgroundEnabled);
            Assert.False(dialog.IsColorEnabled);
            Assert.Equal("1.5", dialog.OffsetText);

            dialog.UseMask = true;
            Assert.True(dialog.IsOffsetEnabled);
            Assert.True(dialog.IsDrawingBackgroundEnabled);
            Assert.True(dialog.IsColorEnabled);

            dialog.UseDrawingBackground = true;
            Assert.False(dialog.IsColorEnabled);
            Assert.Equal("Drawing background", dialog.ColorText);
            Assert.False(dialog.PickColorCommand.CanExecute(null));

            dialog.UseDrawingBackground = false;
            Assert.True(dialog.PickColorCommand.CanExecute(null));
            Assert.Equal("ByLayer", dialog.ColorText);
        }

        [Fact]
        public void TheDialog_RefusesAFactorOutsideOneToFive_NamesTheBox_AndGivesTheMaskOtherwise()
        {
            var dialog = new MTextMaskViewModel(null) { UseMask = true, OffsetText = "6" };
            Assert.False(dialog.Ok());
            Assert.Equal("The border offset factor must be a number from 1.0 to 5.0.", dialog.Notice);
            Assert.Equal("Offset", dialog.FaultBox);
            Assert.Null(dialog.Result);

            dialog.OffsetText = "0.9";
            Assert.False(dialog.Ok());
            dialog.OffsetText = "abc";
            Assert.False(dialog.Ok());

            dialog.OffsetText = "3";
            Assert.Equal(string.Empty, dialog.Notice);
            Assert.True(dialog.Ok());
            Assert.True(dialog.Result.UseMask);
            Assert.Equal(3.0, dialog.Result.OffsetFactor, 9);
            Assert.False(dialog.Result.UseDrawingBackground);
        }

        [Fact]
        public void TheDialog_PicksTheColourThroughSelectColor_AndKeepsTheFactorOfAMaskLeftOff()
        {
            var dialog = new MTextMaskViewModel(new CadMTextMask(true, 2.0, false, null)) { UseMask = true };
            dialog.PickColor = select =>
            {
                Assert.True(select.Original.IsByAci);
                select.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
                return true;
            };
            dialog.PickColorCommand.Execute(null);
            Assert.Equal("Index color: 3", dialog.ColorText);

            Assert.True(dialog.Ok());
            Assert.Equal(3, dialog.Result.FillColor.ColorIndex);

            dialog.UseMask = false;
            dialog.OffsetText = "9";
            Assert.True(dialog.Ok());
            Assert.False(dialog.Result.UseMask);
            Assert.Equal(2.0, dialog.Result.OffsetFactor, 9);
        }

        [Fact]
        public void TheEditor_ShowsTheMaskAtOnce_AndWritesItWithTheWordsAsOneUndoStep()
        {
            DxfMText mtext = Notes();
            MainViewModel model = Edited(mtext, out CadDocument document);
            TextEditorViewModel editor = model.TextEditor;
            Assert.False(editor.Mask.UseMask);
            Assert.True(editor.MaskCommand.CanExecute(null));

            // Without a window to show it, the dialog does nothing.
            Assert.False(editor.ShowBackgroundMask());

            model.ShowBackgroundMask = dialog =>
            {
                Assert.False(dialog.UseMask);
                dialog.UseMask = true;
                dialog.OffsetText = "2";
                dialog.PickColor = select =>
                {
                    select.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5);
                    return true;
                };
                dialog.PickColorCommand.Execute(null);
                return dialog.Ok();
            };
            Assert.True(editor.ShowBackgroundMask());
            Assert.True(editor.IsModified);
            Assert.True(editor.Mask.UseMask);

            // The editor's own layout draws the mask the dialog set, a fill among its parts.
            var layout = new CadMTextEditorLayout(model.Document, editor.Edit);
            layout.SetMask(editor.Mask);
            layout.Update(editor.Editor, editor.Attachment, editor.LineSpacing, 0xFFFFFF, 0xFFFFFF);
            Assert.Contains(layout.Parts, p => p is DisplayFill2);

            editor.CloseCommand.Execute(null);
            var written = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.True(written.BackgroundFill);
            Assert.False(written.UseBackgroundColor);
            Assert.Equal(2.0, written.BackgroundScaleFactor, 9);
            Assert.Equal(5, written.BackgroundFillColor.ColorIndex);

            Assert.True(document.Undo());
            Assert.False(((DxfMText)document.FindEntity(mtext.Handle)).BackgroundFill);
        }

        [Fact]
        public void TheEditor_OpensOnTheMaskTheMTextHas_AndTurnsItOff()
        {
            DxfMText mtext = Notes();
            mtext.BackgroundFill = true;
            mtext.UseBackgroundColor = true;
            mtext.BackgroundScaleFactor = 1.2;
            MainViewModel model = Edited(mtext, out CadDocument document);
            TextEditorViewModel editor = model.TextEditor;
            Assert.True(editor.Mask.UseMask);
            Assert.True(editor.Mask.UseDrawingBackground);
            Assert.Equal(1.2, editor.Mask.OffsetFactor, 9);

            model.ShowBackgroundMask = dialog =>
            {
                Assert.True(dialog.UseMask);
                Assert.Equal("1.2", dialog.OffsetText);
                dialog.UseMask = false;
                return dialog.Ok();
            };
            Assert.True(editor.ShowBackgroundMask());
            editor.CloseCommand.Execute(null);

            Assert.False(((DxfMText)document.FindEntity(mtext.Handle)).BackgroundFill);
        }

        [Fact]
        public void ACancelledDialog_ChangesNothing()
        {
            MainViewModel model = Edited(Notes(), out _);
            TextEditorViewModel editor = model.TextEditor;
            model.ShowBackgroundMask = dialog =>
            {
                dialog.UseMask = true;
                return false;
            };

            Assert.False(editor.ShowBackgroundMask());
            Assert.False(editor.Mask.UseMask);
            Assert.False(editor.IsModified);
        }

        [Fact]
        public void TheWindow_IsBuiltOverItsModel_WithoutBeingShown()
        {
            OnSta(() =>
            {
                var window = new MTextMaskWindow(new MTextMaskViewModel(null));
                Assert.NotNull(window.Content);
                Assert.NotNull(window.DataContext);
                Assert.NotNull(window.Model.PickColor);
                window.Close();
            });
        }
    }
}
