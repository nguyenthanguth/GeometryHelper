using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The MTEXT editor's frame sets its words by the display's layout: the lines the canvas draws, and the caret moving with them as they wrap.</summary>
    public class MTextEditBoxTests
    {
        private static void Sta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
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

        /// <summary>An MTEXT 2.5 high and 10 wide, its words opened in the editor's frame at 4 pixels a unit.</summary>
        private static CadMTextEditBox Opened(string contents, out MainViewModel model, out DisplayText2 drawn)
        {
            var database = new DxfDatabase("AC1027");
            var mtext = new DxfMText
            {
                Location = new GeoPoint3(100, 50, 0),
                TextHeight = 2.5,
                Width = 10.0,
                Contents = contents,
            };
            database.ModelSpace.AppendEntity(mtext);
            CadDocument document = CadDocument.From(database, "notes.dxf");
            model = new MainViewModel();
            model.OpenDocument(document, "notes.dxf");
            drawn = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();
            Assert.True(model.EditTextAt(mtext.Handle));
            var box = new CadMTextEditBox
            {
                Document = document,
                Model = model.TextEditor,
                PixelsPerUnit = 4.0,
            };
            box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return box;
        }

        [Fact]
        public void TheFrame_BreaksTheLinesAndPutsTheBaselinesWhereTheCanvasDrawsThem()
        {
            Sta(() =>
            {
                CadMTextEditBox box = Opened("AAAA BBBB CCCC", out _, out DisplayText2 drawn);

                // Each word is wider than half of 10: one a line, as the canvas draws them, each part on its line's baseline,
                // the drawn parts 100, 50 off from the frame's, which has the MTEXT's location at nought.
                DisplayText2[] lines = drawn.Parts.OfType<DisplayText2>().ToArray();
                CadMTextEditorLayout layout = box.TextLayout;
                Assert.Equal(new[] { "AAAA", "BBBB", "CCCC" }, lines.Select(t => t.Text).ToArray());
                Assert.Equal(3, layout.LineCount);

                for (int n = 0; n < 3; n++)
                {
                    Assert.Equal(lines[n].Y - 50.0, layout.BaselineOf(n), 9);
                    Assert.Equal(lines[n].X - 100.0, layout.XOf(layout.LineStart(n)), 9);
                }

                // The frame hangs by the top left of the MTEXT: the attachment point at the top left of the box, inside the pad.
                Assert.Equal(CadMTextEditBox.Pad + ((0.0 - layout.Bounds[0]) * 4.0), box.Anchor.X, 9);
                Assert.Equal(
                    CadMTextEditBox.RulerHeight + CadMTextEditBox.Pad + ((layout.Bounds[3] - 0.0) * 4.0),
                    box.Anchor.Y,
                    9
                );
            });
        }

        [Fact]
        public void TypingPastTheWidth_MovesTheCaretToTheNextLine_WhereHomeAndUpFollowTheLines()
        {
            Sta(() =>
            {
                CadMTextEditBox box = Opened("AAAA BBBB", out MainViewModel model, out _);
                CadMTextEditor editor = model.TextEditor.Editor;
                Assert.True(box.HandleKey(Key.End, ModifierKeys.Control));
                Assert.Equal(1, box.TextLayout.LineOfPlace(editor.Caret));

                // A third word does not fit beside the second: it goes to a third line, and the caret with it.
                editor.Type(" CC");
                Assert.Equal(3, box.TextLayout.LineCount);
                Assert.Equal(2, box.TextLayout.LineOfPlace(editor.Caret));

                Assert.True(box.HandleKey(Key.Home, ModifierKeys.None));
                Assert.Equal(10, editor.Caret);
                Assert.True(box.HandleKey(Key.Up, ModifierKeys.None));
                Assert.Equal(5, editor.Caret);
                Assert.True(box.HandleKey(Key.End, ModifierKeys.None));
                Assert.Equal(9, editor.Caret);
            });
        }
    }
}
