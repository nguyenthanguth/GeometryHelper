using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The MTEXT editor's font box where the letters under the caret are in a font the list does not carry (a family this
    /// machine lacks, a font code in other letters than the list spells it): the box lists the font and shows it, not blank.
    /// </summary>
    public sealed class MTextFontBoxTests
    {
        private static TextEditorViewModel Edited(string contents, out MainViewModel model)
        {
            var database = new DxfDatabase("AC1027");
            var mtext = new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Contents = contents,
            };
            database.ModelSpace.AppendEntity(mtext);
            model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "fonts.dxf"), "fonts.dxf");
            Assert.True(model.EditTextAt(mtext.Handle));
            model.TextEditor.Editor.MoveTo(1);
            return model.TextEditor;
        }

        [Fact]
        public void LettersInAFamilyThisMachineLacks_ShowThatFamilyInTheBox()
        {
            TextEditorViewModel editor = Edited(@"{\fGhNoSuchFontFamily|b0|i0|c129|p34;PLATE}", out _);

            Assert.Equal("GhNoSuchFontFamily", editor.Font);
            Assert.Contains("GhNoSuchFontFamily", editor.Fonts);
        }

        [Fact]
        public void LettersInAFamilyOfOtherCase_ShowTheListedSpelling_NotABlankBox()
        {
            TextEditorViewModel editor = Edited(@"{\fARIAL|b0|i0|c0|p34;PLATE}", out _);

            Assert.Contains(editor.Font, editor.Fonts);
            Assert.Equal(
                1,
                editor.Fonts.Count(f => string.Equals(f, "Arial", System.StringComparison.OrdinalIgnoreCase))
            );
        }

        [Fact]
        public void MovingTheCaretOutOfTheOddFont_DropsItFromTheList_AndPickingTheListedOnesStillEdits()
        {
            TextEditorViewModel editor = Edited(@"{\fGhNoSuchFontFamily|b0|i0|c129|p34;AB}CD", out _);
            Assert.Contains("GhNoSuchFontFamily", editor.Fonts);

            editor.Editor.MoveTo(3);

            Assert.DoesNotContain("GhNoSuchFontFamily", editor.Fonts);
            Assert.Contains(editor.Font, editor.Fonts);

            editor.Editor.SelectAll();
            editor.Font = "Arial";
            Assert.Equal("Arial", editor.Font);
        }
    }
}
