using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The TrueType families and files a drawing names that the machine has not, as the Missing Fonts dialog lists them: those
    /// of its text styles and those an MTEXT's font codes switch to, each once, in the order of the names.
    /// </summary>
    public sealed class MissingTrueTypeListTests
    {
        private const string NoSuchFamily = "GhNoSuchFontFamily";

        private static bool Installed(string family) =>
            family == "Arial" || family == "Malgun Gothic" || family == "맑은 고딕";

        // The Standard style names the presenter's own font, so that no SHX file is missing but those a test adds.
        private static DxfDatabase NewDrawing()
        {
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable["Standard"].FileName = CadFonts.BuiltInName + ".shx";
            return database;
        }

        private static DxfMText MText(string contents) =>
            new DxfMText
            {
                Contents = contents,
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 5.0,
            };

        private static DxfTextStyleTableRecord FamilyStyle(string name, string family) =>
            new DxfTextStyleTableRecord { Name = name, Font = new DxfFontDescriptor(family, false, false, 0, 34) };

        private static CadDocument With(DxfDatabase database, string replacement = CadFonts.BuiltInName)
        {
            CadDocument document = CadDocument.From(database, "plan.dxf");
            document.Fonts = new CadFonts(null, replacement, null, Installed);
            return document;
        }

        [Fact]
        public void AStyleOfAFamilyTheMachineHasNot_IsListed_AnInstalledOneIsNot()
        {
            DxfDatabase database = NewDrawing();
            database.TextStyleTable.Add(FamilyStyle("KR", NoSuchFamily));
            database.TextStyleTable.Add(FamilyStyle("Plain", "Arial"));

            Assert.Equal(new[] { NoSuchFamily }, With(database).FindMissingTrueTypeFonts());
        }

        [Fact]
        public void ALocalizedFamilyNameThatResolves_IsNotListed()
        {
            DxfDatabase database = NewDrawing();
            database.TextStyleTable.Add(FamilyStyle("KR", "맑은 고딕"));

            Assert.Empty(With(database).FindMissingTrueTypeFonts());
        }

        [Fact]
        public void ATrueTypeFileTheMachineHasNot_IsListedByItsFileName()
        {
            DxfDatabase database = NewDrawing();
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "KR", FileName = "ghnosuchfont.ttc" });

            Assert.Equal(new[] { "ghnosuchfont.ttc" }, With(database).FindMissingTrueTypeFonts());
        }

        [Fact]
        public void AMissingShxFont_IsNotListed_AsATrueTypeFont()
        {
            DxfDatabase database = NewDrawing();
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "OLD", FileName = "romand.shx" });
            CadDocument document = With(database);

            Assert.Empty(document.FindMissingTrueTypeFonts());
            Assert.Single(document.FindMissingFonts());
        }

        [Fact]
        public void AnMTextFontCode_NamesAFamily_InModelSpaceOrInABlock_EachOnceWhateverItsCase()
        {
            DxfDatabase database = NewDrawing();
            database.ModelSpace.AppendEntity(MText(@"{\fGhZeta|b0|i0|c129|p50;abc} {\fArial|b0|i0|c0|p34;def}"));
            var block = new DxfBlockTableRecord { Name = "TITLEBLOCK" };
            database.BlockTable.Add(block);
            block.AppendEntity(MText(@"{\fghzeta|b1|i0|c129|p50;abc}"));
            block.AppendEntity(MText(@"\fGhAlpha|b0|i0|c0|p34;abc"));
            block.AppendEntity(MText(@"no codes here"));

            Assert.Equal(new[] { "GhAlpha", "GhZeta" }, With(database).FindMissingTrueTypeFonts());
        }

        [Fact]
        public void AnMTextFontCode_OfAnShxFile_OrAFamilyTheMachineHas_IsNotListed()
        {
            DxfDatabase database = NewDrawing();
            database.ModelSpace.AppendEntity(MText(@"{\Fromans.shx|c0;abc} {\fMalgun Gothic|b0|i0|c129|p50;def}"));

            Assert.Empty(With(database).FindMissingTrueTypeFonts());
        }

        [Fact]
        public void TheSameFamilyInAStyleAndInAnMTextCode_IsListedOnce_AndTheListIsSorted()
        {
            DxfDatabase database = NewDrawing();
            database.TextStyleTable.Add(FamilyStyle("B", "GhBeta"));
            database.TextStyleTable.Add(FamilyStyle("A", "GhAlpha"));
            database.ModelSpace.AppendEntity(MText(@"{\fGHBETA|b0|i0|c0|p34;abc}"));

            // GhBeta and GHBETA are one family: the spelling first in ordinal order is kept.
            Assert.Equal(new[] { "GhAlpha", "GHBETA" }, With(database).FindMissingTrueTypeFonts());
        }

        [Fact]
        public void WithFontsOff_NothingIsListed()
        {
            DxfDatabase database = NewDrawing();
            database.TextStyleTable.Add(FamilyStyle("KR", NoSuchFamily));
            CadDocument document = CadDocument.From(database, "plan.dxf");

            Assert.Empty(document.FindMissingTrueTypeFonts());
        }
    }
}
