using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A TrueType family the drawing names that the machine has not (a Korean drawing's 돋움체 on a machine without Gulim and
    /// Dotum) is drawn in a TrueType font, as AutoCAD draws it through the font mapping of Windows, so that its letters (the
    /// Korean ones, which the presenter's own single-stroke font has not) are drawn; FONTALT's SHX font stands for a missing
    /// SHX font alone.
    /// </summary>
    public sealed class MissingTrueTypeFamilyTests
    {
        private const string Korean = "한글 도면";

        private static CadDocument Drawing(string family, params DxfEntity[] entities)
        {
            var database = new DxfDatabase("AC1027");
            var style = new DxfTextStyleTableRecord { Name = "KR" };
            style.Font = new DxfFontDescriptor(family, false, false, 0, 34);
            database.TextStyleTable.Add(style);

            foreach (DxfEntity entity in entities)
            {
                database.ModelSpace.AppendEntity(entity);

                if (entity is DxfDBText text)
                {
                    text.TextStyleName = "KR";
                }
                else if (entity is DxfMText mtext)
                {
                    mtext.TextStyleName = "KR";
                }
            }

            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null, f => f == "Arial" || f == "Malgun Gothic");
            return document;
        }

        private static DxfDBText Text(string words) =>
            new DxfDBText
            {
                TextString = words,
                Position = new GeoPoint3(0, 0, 0),
                Height = 5.0,
            };

        private static DxfMText MText(string contents) =>
            new DxfMText
            {
                Contents = contents,
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 5.0,
            };

        private static DisplayText2[] Shown(CadDocument document) =>
            document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().ToArray();

        [Fact]
        public void AStyleOfAFamilyTheMachineHasNot_IsDrawnInATrueTypeFont_NotInTheSingleStrokeFont()
        {
            CadDocument document = Drawing("DotumChe", Text(Korean));

            DisplayText2 shown = Assert.Single(Shown(document));

            Assert.Null(shown.Strokes);
            Assert.NotEqual(CadFonts.BuiltInName, shown.FontName);
        }

        [Fact]
        public void AnMTextRunOfAFamilyTheMachineHasNot_IsDrawnInATrueTypeFont_NotInTheSingleStrokeFont()
        {
            CadDocument document = Drawing("Arial", MText(@"{\fGulim|b0|i0|c129|p50;" + Korean + "}"));

            DisplayText2[] shown = Shown(document);

            Assert.NotEmpty(shown);
            Assert.All(shown, t => Assert.Null(t.Strokes));
            Assert.All(shown, t => Assert.NotEqual(CadFonts.BuiltInName, t.FontName));
        }

        [Fact]
        public void AFamilyNotFound_IsSaidOnce_AndTheStylesOwnFamilyNameIsKept()
        {
            CadDocument document = Drawing("DotumChe", Text(Korean), Text("abc"));
            var said = new List<string>();
            document.FontMissing += (s, e) => said.Add(e.Message);

            Shown(document);

            Assert.Equal(
                new[] { "Font DotumChe not found: using the drawing font." },
                said.Where(m => m.Contains("DotumChe"))
            );
            Assert.Equal("DotumChe", document.Database.TextStyleTable["KR"].Font.TypeFace);
        }

        [Fact]
        public void ATrueTypeFileNotFound_IsDrawnInATrueTypeFont_ASingleStrokeFontOnlyForAMissingShx()
        {
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "KR", FileName = "gulim.ttc" });
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "OLD", FileName = "romans.shx" });
            DxfDBText ttf = Text(Korean);
            ttf.TextStyleName = "KR";
            DxfDBText shx = Text("ABC");
            shx.TextStyleName = "OLD";
            database.ModelSpace.AppendEntity(ttf);
            database.ModelSpace.AppendEntity(shx);
            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null, f => f == "Arial");

            DisplayText2[] shown = Shown(document);

            Assert.Null(shown[0].Strokes);
            Assert.NotNull(shown[1].Strokes);
            Assert.Equal(CadFonts.BuiltInName, shown[1].FontName);
        }
    }
}
