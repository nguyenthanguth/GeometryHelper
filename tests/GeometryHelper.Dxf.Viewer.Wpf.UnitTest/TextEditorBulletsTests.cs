using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Text Editor tab's Bullets and Numbering menu and the Bullets and Lists menu of the editor's right-click, to the design
    /// TextEditorBullets: AutoCAD 2021's items in its order, each list wired to <see cref="CadMTextEditor.SetNumbering"/>, the
    /// ribbon's ticks on the labels every selected paragraph has, and Start, Continue and the two Allow items grey.
    /// </summary>
    public sealed class TextEditorBulletsTests
    {
        private static readonly string[] Items =
        {
            "Off",
            "Numbered",
            "Lettered",
            "Bulleted",
            string.Empty,
            "Start",
            "Continue",
            string.Empty,
            "Allow Auto Bullets and Numbering",
            "Allow Bullets and Lists",
        };

        private static TextEditorViewModel Edited(string contents, out CadDocument document)
        {
            var model = new MainViewModel();
            var database = new DxfDatabase("AC1027");
            var mtext = new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Width = 55.0,
                Contents = contents,
            };
            database.ModelSpace.AppendEntity(mtext);
            document = CadDocument.From(database, "notes.dxf");
            model.OpenDocument(document, "notes.dxf");
            Assert.True(model.EditTextAt(mtext.Handle));
            return model.TextEditor;
        }

        private static CadMenuItemViewModel Item(IEnumerable<CadMenuItemViewModel> menu, string header) =>
            menu.Single(i => i.Header == header);

        /// <summary>The headers of the items ticked.</summary>
        private static string[] Ticked(IEnumerable<CadMenuItemViewModel> menu) =>
            menu.Where(i => i.IsChecked).Select(i => i.Header).ToArray();

        [Fact]
        public void TheBulletsMenu_IsAutoCadsItems_OffTickedForPlainWords_StartContinueAndAllowGrey()
        {
            TextEditorViewModel editor = Edited("Plate\\PBeam", out _);
            IReadOnlyList<CadMenuItemViewModel> menu = editor.Menu("Bullets");

            Assert.Equal(Items, menu.Select(i => i.Header).ToArray());
            Assert.Equal(new[] { "Off", "Allow Bullets and Lists" }, Ticked(menu));
            Assert.All(menu.Take(4), i => Assert.True(i.IsEnabled));
            Assert.All(menu.Take(4), i => Assert.True(i.IsCheckable));
            Assert.Equal(new[] { "Lowercase", "Uppercase" }, Item(menu, "Lettered").Items.Select(i => i.Header));

            foreach (string grey in Items.Skip(5).Where(h => h.Length > 0))
            {
                Assert.False(Item(menu, grey).IsEnabled);
                Assert.Equal("Not built yet.", Item(menu, grey).ToolTip);
            }

            Assert.True(Item(menu, "Allow Auto Bullets and Numbering").IsCheckable);
            Assert.False(Item(menu, "Allow Auto Bullets and Numbering").IsChecked);
        }

        [Fact]
        public void APick_LabelsTheSelectedParagraphs_OneUndoStep_AndTheMenuTicksItAfter()
        {
            TextEditorViewModel editor = Edited("Plate\\PBeam", out _);
            editor.Editor.SelectAll();

            Item(editor.Menu("Bullets"), "Numbered").Command.Execute(null);
            Assert.Equal("\\pxi-3,l3,t3;1.\tPlate\\P2.\tBeam", editor.Editor.Contents);
            Assert.Equal(new[] { "Numbered", "Allow Bullets and Lists" }, Ticked(editor.Menu("Bullets")));

            editor.Editor.SelectAll();
            Item(Item(editor.Menu("Bullets"), "Lettered").Items, "Uppercase").Command.Execute(null);
            Assert.Equal("\\pxi-3,l3,t3;A.\tPlate\\PB.\tBeam", editor.Editor.Contents);
            IReadOnlyList<CadMenuItemViewModel> menu = editor.Menu("Bullets");
            Assert.Equal(new[] { "Lettered", "Allow Bullets and Lists" }, Ticked(menu));
            Assert.Equal(new[] { "Uppercase" }, Ticked(Item(menu, "Lettered").Items));

            editor.Editor.SelectAll();
            Item(editor.Menu("Bullets"), "Bulleted").Command.Execute(null);
            Assert.Equal(
                "\\pxi-3,l3,t3;{\\fSymbol|b0|i0|c2|p18;·\t}Plate\\P{\\fSymbol|b0|i0|c2|p18;·\t}Beam",
                editor.Editor.Contents
            );
            Assert.Equal(new[] { "Bulleted", "Allow Bullets and Lists" }, Ticked(editor.Menu("Bullets")));

            editor.Editor.SelectAll();
            Item(editor.Menu("Bullets"), "Off").Command.Execute(null);
            Assert.Equal("Plate\\PBeam", editor.Editor.Contents);

            Assert.True(editor.Editor.Undo());
            Assert.True(editor.Editor.Undo());
            Assert.Equal("\\pxi-3,l3,t3;A.\tPlate\\PB.\tBeam", editor.Editor.Contents);
        }

        [Fact]
        public void ParagraphsOfMixedLabels_TickNothing_AndTheCaretsParagraphAloneIsLabelled()
        {
            TextEditorViewModel editor = Edited("Plate\\PBeam", out _);

            // The caret in "Beam": the second paragraph alone numbered.
            editor.Editor.MoveTo(8);
            Item(editor.Menu("Bullets"), "Numbered").Command.Execute(null);
            Assert.Equal("Plate\\P\\pxi-3,l3,t3;1.\tBeam", editor.Editor.Contents);

            editor.Editor.SelectAll();
            IReadOnlyList<CadMenuItemViewModel> menu = editor.Menu("Bullets");
            Assert.Equal(new[] { "Allow Bullets and Lists" }, Ticked(menu));
            Assert.Empty(Ticked(Item(menu, "Lettered").Items));
        }

        [Fact]
        public void TheEditorsBulletsAndLists_HasTheSameItems_ListsUnticked_TheTwoAllowItemsCheckable()
        {
            TextEditorViewModel editor = Edited("Plate\\PBeam", out _);
            CadMenuItemViewModel lists = Item(editor.Menu("Editor"), "Bullets and Lists");

            Assert.True(lists.IsEnabled);
            Assert.Equal(Items, lists.Items.Select(i => i.Header).ToArray());
            Assert.All(lists.Items.Take(4), i => Assert.False(i.IsCheckable));
            Assert.Equal(new[] { "Allow Bullets and Lists" }, Ticked(lists.Items));
            Assert.True(Item(lists.Items, "Allow Auto Bullets and Numbering").IsCheckable);
            Assert.All(Item(lists.Items, "Lettered").Items, i => Assert.False(i.IsCheckable));
            Assert.False(Item(lists.Items, "Start").IsEnabled);

            editor.Editor.SelectAll();
            Item(Item(lists.Items, "Lettered").Items, "Lowercase").Command.Execute(null);
            Assert.Equal("\\pxi-3,l3,t3;a.\tPlate\\Pb.\tBeam", editor.Editor.Contents);

            // Still no tick on the lists of the right-click menu, as AutoCAD's.
            lists = Item(editor.Menu("Editor"), "Bullets and Lists");
            Assert.Equal(new[] { "Allow Bullets and Lists" }, Ticked(lists.Items));
        }
    }
}
