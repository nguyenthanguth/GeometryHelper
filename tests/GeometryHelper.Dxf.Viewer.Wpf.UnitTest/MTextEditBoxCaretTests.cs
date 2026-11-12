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
    /// <summary>
    /// The caret at a line that breaks between two ideographs, with no space to stand on: End puts it at the end of the line,
    /// though its place is the first of the next line, and Home, Up and Down go on from the line it is drawn on.
    /// </summary>
    public class MTextEditBoxCaretTests
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
        private static CadMTextEditBox Opened(string contents, out MainViewModel model)
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
        public void EndAtALineBrokenBetweenIdeographs_StaysOnThatLine_ForHomeAndUp()
        {
            Sta(() =>
            {
                // Thirty ideographs in a frame 10 wide: three lines or more, each broken between two of them.
                CadMTextEditBox box = Opened(
                    new string(Enumerable.Range(0x4E00, 30).Select(c => (char)c).ToArray()),
                    out MainViewModel model
                );
                CadMTextEditor editor = model.TextEditor.Editor;
                CadMTextEditorLayout layout = box.TextLayout;
                Assert.True(layout.LineCount >= 3, layout.LineCount + " lines");
                int second = layout.LineStart(1);
                int third = layout.LineStart(2);

                // End on the first line: the place after its last ideograph, the first of the second line.
                Assert.True(box.HandleKey(Key.Home, ModifierKeys.Control));
                Assert.True(box.HandleKey(Key.End, ModifierKeys.None));
                Assert.Equal(second, editor.Caret);

                // Home from there goes to the start of the first line, the line the caret is drawn at the end of.
                Assert.True(box.HandleKey(Key.Home, ModifierKeys.None));
                Assert.Equal(0, editor.Caret);

                // End on the second line, then Up: back on the first line, not the second.
                editor.MoveTo(second + 1);
                Assert.True(box.HandleKey(Key.End, ModifierKeys.None));
                Assert.Equal(third, editor.Caret);
                Assert.True(box.HandleKey(Key.Up, ModifierKeys.None));
                Assert.Equal(second, editor.Caret);
                Assert.True(box.HandleKey(Key.Home, ModifierKeys.None));
                Assert.Equal(0, editor.Caret);

                // Typing moves it off the end of the line: its place's line again.
                Assert.True(box.HandleKey(Key.Home, ModifierKeys.Control));
                Assert.True(box.HandleKey(Key.End, ModifierKeys.None));
                editor.Type("A");
                Assert.True(box.HandleKey(Key.Home, ModifierKeys.None));
                Assert.Equal(box.TextLayout.LineStart(box.TextLayout.LineOfPlace(editor.Caret)), editor.Caret);
            });
        }
    }
}
