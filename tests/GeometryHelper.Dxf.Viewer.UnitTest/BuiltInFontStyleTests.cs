using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The presenter's own font, ghsimplex, known by its name wherever a font is named: typed to -STYLE it is an SHX font, not a TrueType family.</summary>
    public class BuiltInFontStyleTests
    {
        [Fact]
        public void TheBuiltInNameIsKnownWithOrWithoutShx_InEitherCase_AndNoOtherIs()
        {
            Assert.True(CadFonts.IsBuiltIn("ghsimplex"));
            Assert.True(CadFonts.IsBuiltIn(" GHSIMPLEX.SHX "));
            Assert.False(CadFonts.IsBuiltIn("simplex.shx"));
            Assert.False(CadFonts.IsBuiltIn("ghsimplex.ttf"));
            Assert.False(CadFonts.IsBuiltIn(null));
        }

        [Fact]
        public void GhsimplexTypedToStyle_IsTheBuiltInShxFont_NotATrueTypeFamily()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "-STYLE", "GH_OWN", "ghsimplex", "0", "1", "0", "N", "N", "N");

            DxfTextStyleTableRecord style = document.Database.TextStyleTable["GH_OWN"];
            Assert.Equal("ghsimplex.shx", style.FileName);
            Assert.Equal(string.Empty, style.Font.TypeFace);
            Assert.False(session.IsRunning);
            Assert.Equal("\"GH_OWN\" is now the current text style.", session.History.Last());
        }
    }
}
