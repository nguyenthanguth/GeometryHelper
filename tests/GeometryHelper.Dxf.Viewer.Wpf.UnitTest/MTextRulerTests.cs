using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The MTEXT editor's ruler, the Paragraph and Column Settings dialogs and the Text Editor tab's Line Spacing and Columns
    /// menus, driven through their models with no real dialog: every MTEXT is 2.5 high, the ruler 6 pixels to a unit, so a
    /// major tick is 5 units (30 pixels) and a minor one 1.
    /// </summary>
    public sealed class MTextRulerTests
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

        private static DxfMText Notes(string contents = "Plate\tPL10\\PBeam\tHEA200", double width = 55.0) =>
            new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Width = width,
                Contents = contents,
            };

        /// <summary>The ruler of an editor, its zero at pixel 100, the ruler's top at 0, the words' top at 22.</summary>
        private static MTextRuler Ruler(TextEditorViewModel editor) =>
            new MTextRuler(editor)
            {
                Zero = 100.0,
                FrameLeft = 94.0,
                RulerTop = 0.0,
                BoxTop = 28.0,
                Scale = 6.0,
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
        public void TicksAreEveryFiveUnitsAtSixPixels_AndAClickOnTheRulerAddsAStopAtTheNearestMinorTick()
        {
            TextEditorViewModel editor = Edited(Notes(), out _).TextEditor;
            MTextRuler ruler = Ruler(editor);
            Assert.Equal(5.0, ruler.Major, 9);
            Assert.Equal(1.0, ruler.Minor, 9);

            // 45.2 units right of the zero, in the lower half where no marker is.
            Assert.True(ruler.Press(new Point(100.0 + (45.2 * 6.0), 15.0), 0.0));

            // The caret's paragraph only: the next is put back as AutoCAD puts it back.
            Assert.Equal("\\pxt18;Plate\tPL10\\P\\ptz;Beam\tHEA200", editor.Editor.Contents);
            Assert.True(editor.Editor.Undo());
            Assert.False(editor.Editor.CanUndo);
        }

        [Fact]
        public void TheTabTypeBox_CyclesLeftCentreRightDecimal_AndSetsTheKindOfTheNextStopClicked()
        {
            TextEditorViewModel editor = Edited(Notes(), out _).TextEditor;
            MTextRuler ruler = Ruler(editor);
            Assert.Equal("Tab type: Left. Click to change.", ruler.TipOf(MTextRulerPart.TypeBox, 0, 0.0));

            foreach (char kind in "crdl")
            {
                Assert.Equal(MTextRulerPart.TypeBox, ruler.HitTest(new Point(90.0, 10.0), 0.0, out _));
                ruler.Press(new Point(90.0, 10.0), 0.0);
                Assert.Equal(kind, ruler.TabType);
            }

            ruler.Press(new Point(90.0, 10.0), 0.0);
            ruler.Press(new Point(100.0 + (20.0 * 6.0), 15.0), 0.0);
            Assert.Equal('c', editor.Editor.CurrentFormat.TabStops.Single().Kind);
        }

        [Fact]
        public void ADraggedStop_FollowsSnapped_IsOneUndoStepOnRelease_AndEscPutsItBack()
        {
            TextEditorViewModel editor = Edited(Notes("\\pxt16;Plate\tPL10"), out _).TextEditor;
            MTextRuler ruler = Ruler(editor);
            Assert.Equal("Left tab: 40.0000", ruler.TipOf(MTextRulerPart.Tab, 0, 0.0));

            Assert.True(ruler.Press(new Point(100.0 + 240.0, 15.0), 0.0));
            Assert.Equal(MTextRulerPart.Tab, ruler.Dragging);
            ruler.Move(new Point(100.0 + (30.0 * 6.0), 16.0));
            ruler.Move(new Point(100.0 + (15.1 * 6.0), 16.0));
            Assert.Equal("Left tab: 15.0000", ruler.DragTip);
            ruler.Release();

            Assert.Equal("\\pxt6;Plate\tPL10", editor.Editor.Contents);
            Assert.True(editor.Editor.Undo());
            Assert.Equal("\\pxt16;Plate\tPL10", editor.Editor.Contents);

            ruler.Press(new Point(100.0 + 240.0, 15.0), 0.0);
            ruler.Move(new Point(100.0 + 60.0, 16.0));
            Assert.True(ruler.Cancel());
            Assert.Equal("\\pxt16;Plate\tPL10", editor.Editor.Contents);
            Assert.False(editor.Editor.CanUndo);
        }

        [Fact]
        public void AStopDraggedOffTheRuler_IsRemovedOnRelease_AndBackOnItIsAMoveAgain()
        {
            TextEditorViewModel editor = Edited(Notes("\\pxt16;Plate\tPL10"), out _).TextEditor;
            MTextRuler ruler = Ruler(editor);

            ruler.Press(new Point(340.0, 15.0), 0.0);
            ruler.Move(new Point(200.0, 40.0));
            Assert.True(ruler.IsOff);
            Assert.Equal("Release to remove the tab", ruler.DragTip);
            ruler.Move(new Point(160.0, 15.0));
            Assert.False(ruler.IsOff);
            Assert.Equal(10.0, editor.Editor.CurrentFormat.TabStops.Single().Position, 9);
            ruler.Move(new Point(160.0, -20.0));
            ruler.Release();

            Assert.Equal("Plate\tPL10", editor.Editor.Contents);
            Assert.True(editor.Editor.Undo());
            Assert.Equal("\\pxt16;Plate\tPL10", editor.Editor.Contents);
        }

        [Fact]
        public void TheLowerTriangle_DragsTheHangingIndentAlone()
        {
            TextEditorViewModel editor = Edited(
                Notes("1.\tAll dimensions in millimetres unless noted."),
                out _
            ).TextEditor;
            MTextRuler ruler = Ruler(editor);

            Assert.Equal(MTextRulerPart.Hanging, ruler.HitTest(new Point(100.0, 18.0), 0.0, out _));
            Assert.Equal(MTextRulerPart.First, ruler.HitTest(new Point(100.0, 4.0), 0.0, out _));
            ruler.Press(new Point(100.0, 18.0), 0.0);
            ruler.Move(new Point(136.0, 18.0));
            Assert.Equal("Paragraph indent: 6.0000", ruler.DragTip);
            ruler.Release();

            Assert.Equal("\\pxi-2.4,l2.4;1.\tAll dimensions in millimetres unless noted.", editor.Editor.Contents);
            Assert.Equal(0.0, editor.Editor.CurrentFormat.FirstLineIndent, 9);
        }

        [Fact]
        public void TheWidthArrows_SetTheWidth_WrittenWithTheWordsInOneStepOfTheDrawing()
        {
            DxfMText mtext = Notes();
            MainViewModel model = Edited(mtext, out CadDocument document);
            TextEditorViewModel editor = model.TextEditor;
            MTextRuler ruler = Ruler(editor);

            Assert.Equal(MTextRulerPart.Width, ruler.HitTest(new Point(100.0 + 330.0 - 6.0, 11.0), 0.0, out _));
            ruler.Press(new Point(424.0, 11.0), 0.0);
            ruler.Move(new Point(100.0 + 270.4, 11.0));
            ruler.Release();
            Assert.Equal(45.0, editor.Width, 9);
            Assert.True(editor.IsModified);

            editor.CloseCommand.Execute(null);
            Assert.Equal(45.0, ((DxfMText)document.FindEntity(mtext.Handle)).Width, 9);
            Assert.True(document.Undo());
            Assert.Equal(55.0, ((DxfMText)document.FindEntity(mtext.Handle)).Width, 9);
        }

        [Fact]
        public void TheLineSpacingMenu_SetsTheParagraphsOwnMultiple_AsAutoCADWritesIt_AndClearTakesItOff()
        {
            TextEditorViewModel editor = Edited(Notes(), out _).TextEditor;
            Assert.Equal(
                new[] { "1.0x", "1.5x", "2.0x", "2.5x", string.Empty, "More…", "Clear Paragraph Spacing" },
                editor.Menu("LineSpacing").Select(i => i.Header).ToArray()
            );
            Assert.False(editor.Menu("LineSpacing").Last().IsEnabled);

            editor.Menu("LineSpacing").Single(i => i.Header == "1.5x").Command.Execute(null);

            Assert.Equal("\\pxsm1.5;Plate\tPL10\\P\\ps*;Beam\tHEA200", editor.Editor.Contents);
            Assert.True(editor.Menu("LineSpacing").Single(i => i.Header == "1.5x").IsChecked);
            editor.Menu("LineSpacing").Last().Command.Execute(null);
            Assert.Equal("Plate\tPL10\\PBeam\tHEA200", editor.Editor.Contents);
        }

        [Fact]
        public void TheColumnsMenu_TicksTheType_StaticKeepsTheTotal_AndColumnsAreWrittenOnClose()
        {
            DxfMText mtext = Notes("one two three four five six seven eight nine ten", 60.0);
            MainViewModel model = Edited(mtext, out CadDocument document);
            TextEditorViewModel editor = model.TextEditor;
            Assert.Equal(
                new[]
                {
                    "No Columns",
                    "Dynamic Columns",
                    "Static Columns",
                    string.Empty,
                    "Insert Column Break",
                    string.Empty,
                    "Column Settings…",
                },
                editor.Menu("Columns").Select(i => i.Header).ToArray()
            );
            Assert.True(editor.Menu("Columns")[0].IsChecked);
            Assert.False(editor.Menu("Columns")[4].IsEnabled);

            editor.Menu("Columns")[2].Items.Single(i => i.Header == "3").Command.Execute(null);

            CadMTextFrame frame = editor.Editor.Frame;
            Assert.Equal(DxfColumnType.StaticColumns, frame.ColumnType);
            Assert.Equal(3, frame.ColumnCount);
            Assert.Equal(12.5, frame.Gutter, 9);
            Assert.Equal(60.0, frame.Width, 9);
            Assert.Equal(35.0 / 3.0, frame.ColumnWidth, 9);
            Assert.True(editor.Menu("Columns")[2].Items.Single(i => i.Header == "3").IsChecked);
            Assert.True(editor.Menu("Columns")[4].IsEnabled);

            editor.CloseCommand.Execute(null);
            var written = (DxfMText)document.FindEntity(mtext.Handle);
            Assert.Equal(DxfColumnType.StaticColumns, written.ColumnType);
            Assert.Equal(3, written.ColumnCount);
            Assert.Equal(12.5, written.ColumnGutterWidth, 9);
        }

        [Fact]
        public void WithColumns_TheRulerDragsAColumnsWidthTheGutterTheTotalAndAColumnsHeight()
        {
            DxfMText mtext = Notes("GENERAL NOTES", 0.0);
            TextEditorViewModel editor = Edited(mtext, out _).TextEditor;
            editor.Editor.SetFrame(
                CadMTextFrame.Of(0.0, 25.0).WithColumns(DxfColumnType.StaticColumns, 40.0, 12.5, 3, false)
            );
            MTextRuler ruler = Ruler(editor);
            ruler.Columns = 3;

            // The end of the first column at 240 px, the gutter edge at 315, the total's arrows at 100 + 145 x 6.
            Assert.Equal(MTextRulerPart.ColumnWidth, ruler.HitTest(new Point(100.0 + 240.0 - 6.0, 11.0), 25.0, out _));
            Assert.Equal(MTextRulerPart.Gutter, ruler.HitTest(new Point(100.0 + 315.0, 11.0), 25.0, out _));
            Assert.Equal(MTextRulerPart.Total, ruler.HitTest(new Point(100.0 + 870.0 - 6.0, 11.0), 25.0, out _));
            Assert.Equal("Total width: 145.0000", ruler.TipOf(MTextRulerPart.Total, 0, 25.0));

            ruler.Press(new Point(100.0 + 315.0, 11.0), 25.0);
            ruler.Move(new Point(100.0 + 270.0, 11.0));
            ruler.Release();
            Assert.Equal(5.0, editor.Editor.Frame.Gutter, 9);
            Assert.Equal(130.0, editor.Editor.Frame.Width, 9);

            ruler.Press(new Point(100.0 + (130.0 * 6.0) - 6.0, 11.0), 25.0);
            ruler.Move(new Point(100.0 + (160.0 * 6.0), 11.0));
            ruler.Release();
            Assert.Equal(160.0, editor.Editor.Frame.Width, 9);
            Assert.Equal(50.0, editor.Editor.Frame.ColumnWidth, 9);

            // The height grip under the first column, 25 units (150 px) under the words' top.
            Point grip = ruler.GripAt(0, 0.0);
            Assert.Equal(MTextRulerPart.Height, ruler.HitTest(new Point(grip.X, grip.Y + 4.0), 0.0, out _));
            ruler.Press(new Point(grip.X, grip.Y + 4.0), 0.0);
            ruler.Move(new Point(grip.X, 28.0 + (15.2 * 6.0)));
            Assert.Equal("Height: 15.0000", ruler.DragTip);
            ruler.Release();
            Assert.Equal(15.0, editor.Editor.Frame.Height, 9);

            Assert.True(editor.Editor.Undo());
            Assert.Equal(25.0, editor.Editor.Frame.Height, 9);
        }

        [Fact]
        public void TheParagraphDialog_AddsStopsRefusesATakenPlace_AndOkIsOneUndoStep()
        {
            TextEditorViewModel editor = Edited(Notes(), out _).TextEditor;
            var dialog = new ParagraphViewModel(editor.Editor, 120.0);
            Assert.True(dialog.HasNoStops);
            Assert.Equal("0.0000", dialog.FirstLineText);
            Assert.Equal("1.0000x", dialog.LineSpacingText);
            Assert.False(dialog.IsLineSpacingOn);

            dialog.PositionText = "20";
            dialog.AddCommand.Execute(null);
            dialog.TabType = "Centre";
            dialog.PositionText = "45";
            dialog.AddCommand.Execute(null);
            dialog.TabType = "Decimal";
            dialog.DecimalStyle = "Comma";
            dialog.PositionText = "70";
            dialog.AddCommand.Execute(null);
            dialog.PositionText = "45";
            dialog.AddCommand.Execute(null);
            Assert.Equal("A tab stop is already at 45.0000.", dialog.Notice);
            Assert.Equal("Position", dialog.FaultBox);
            dialog.PositionText = "130";
            Assert.Equal(string.Empty, dialog.Notice);
            dialog.AddCommand.Execute(null);
            Assert.Equal("The tab position must be a number from 0 to 120.0000.", dialog.Notice);

            dialog.FirstLineText = "0";
            dialog.HangingText = "6";
            dialog.IsLineSpacingOn = true;
            dialog.LineSpacingMode = "Exactly";
            Assert.Equal("2.5000", dialog.LineSpacingText);
            dialog.LineSpacingText = "5";
            Assert.False(dialog.Ok());
            Assert.Equal("The line spacing must be a number from 0.2500 to 4.0000.", dialog.Notice);
            dialog.LineSpacingText = "3";
            Assert.True(dialog.Ok());

            Assert.Equal(
                "\\pxi-2.4,l2.4,se1.2,t8,c18,D,28;Plate\tPL10\\P\\pi0,l0,s*,tz;Beam\tHEA200",
                editor.Editor.Contents
            );
            Assert.True(editor.Editor.Undo());
            Assert.Equal("Plate\tPL10\\PBeam\tHEA200", editor.Editor.Contents);
        }

        [Fact]
        public void TheParagraphDialog_LeavesEmptyABoxThatDiffers_AndKeepsEachParagraphsOwnThere()
        {
            TextEditorViewModel editor = Edited(Notes("\\pxi2;A\\P\\pxi1;B"), out _).TextEditor;
            editor.Editor.SelectAll();
            var dialog = new ParagraphViewModel(editor.Editor, 55.0);
            Assert.Equal(string.Empty, dialog.FirstLineText);
            dialog.RightText = "5";
            Assert.True(dialog.Ok());
            Assert.Equal(5.0, editor.Editor.FormatOf(0).FirstLineIndent, 9);
            Assert.Equal(2.5, editor.Editor.FormatOf(1).FirstLineIndent, 9);
            Assert.Equal(5.0, editor.Editor.FormatOf(1).RightIndent, 9);
        }

        [Fact]
        public void ColumnSettings_EnablesTheBoxesOfItsType_TheTotalFollows_AndRefusesANumberOutOfRange()
        {
            var dialog = new ColumnSettingsViewModel(CadMTextFrame.Of(120.0), 2.5, 1);
            Assert.Equal("None", dialog.ColumnType);
            Assert.False(dialog.IsCountEnabled);
            Assert.False(dialog.IsColumnEnabled);
            Assert.True(dialog.IsTotalEnabled);
            Assert.Equal("120.0000", dialog.TotalText);

            dialog.ColumnType = "Dynamic";
            Assert.Equal("120.0000", dialog.ColumnText);
            Assert.Equal("12.5000", dialog.GutterText);
            Assert.True(dialog.IsHeightEnabled);
            Assert.False(dialog.IsTotalEnabled);
            dialog.AutoHeight = false;
            Assert.False(dialog.IsHeightEnabled);
            Assert.Equal(string.Empty, dialog.HeightText);

            dialog.ColumnType = "Static";
            Assert.Equal("2", dialog.CountText);
            dialog.ColumnText = "60";
            Assert.Equal("132.5000", dialog.TotalText);
            dialog.CountText = "0";
            Assert.False(dialog.Ok());
            Assert.Equal("The column number must be a whole number from 2 to 100.", dialog.Notice);
            Assert.Equal("Number", dialog.FaultBox);
            dialog.CountText = "3";
            Assert.Equal("205.0000", dialog.TotalText);
            dialog.HeightText = "50";
            dialog.TotalText = "145";
            Assert.Equal("40.0000", dialog.ColumnText);
            Assert.True(dialog.Ok());
            Assert.Equal(DxfColumnType.StaticColumns, dialog.Result.ColumnType);
            Assert.Equal(40.0, dialog.Result.ColumnWidth, 9);
            Assert.Equal(50.0, dialog.Result.Height, 9);
        }

        [Fact]
        public void SetMtextWidthAndHeight_RefuseWhatAutoCADRefuses_AndSetTheBoxAsOneStep()
        {
            var width = new MTextSizeViewModel(false, 55.0);
            Assert.Equal("Set Mtext Width", width.Title);
            Assert.Equal("55.0000", width.Text);
            width.Text = "0";
            Assert.False(width.Ok());
            Assert.Equal("The width must be a number above nought.", width.Notice);

            var height = new MTextSizeViewModel(true, 0.0);
            height.Text = "0";
            Assert.True(height.Ok());

            MainViewModel model = Edited(Notes(), out _);
            model.AskMTextSize = dialog =>
            {
                dialog.Text = "30";
                return dialog.Ok();
            };
            model.TextEditor.Menu("Ruler").Single(i => i.Header == "Set Mtext Height…").Command.Execute(null);
            Assert.Equal(30.0, model.TextEditor.Editor.Frame.Height, 9);
            Assert.True(model.TextEditor.Editor.Undo());
            Assert.Equal(0.0, model.TextEditor.Editor.Frame.Height, 9);
        }

        [Fact]
        public void EachNewWindow_IsBuiltOverItsModel_WithoutBeingShown()
        {
            OnSta(() =>
            {
                var editor = new CadMTextEditor("A", 2.5, CadMTextFrame.Of(55.0));
                var windows = new Window[]
                {
                    new ParagraphWindow(new ParagraphViewModel(editor, 55.0)),
                    new ColumnSettingsWindow(new ColumnSettingsViewModel(editor.Frame, 2.5, 1)),
                    new MTextSizeWindow(new MTextSizeViewModel(false, 55.0)),
                };

                foreach (Window window in windows)
                {
                    Assert.NotNull(window.Content);
                    Assert.NotNull(window.DataContext);
                    window.Close();
                }
            });
        }
    }
}
