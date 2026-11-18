using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The other names a TrueType family has in its own name table (Korean, Japanese, Chinese ...): read from the real fonts of
    /// the machine, so each test passes quietly where the font is not installed.
    /// </summary>
    public sealed class TrueTypeLocalNamesTests
    {
        private static bool Has(string family) =>
            CadTrueTypeFonts.Families.Contains(family, StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void MalgunGothic_HasItsKoreanNameAfterTheEnglishOne()
        {
            if (!Has("Malgun Gothic"))
            {
                return;
            }

            Assert.Contains("맑은 고딕", CadTrueTypeFonts.LocalNamesOf("Malgun Gothic"));
        }

        [Fact]
        public void MsGothic_HasItsJapaneseNameInFullWidthLetters()
        {
            if (!Has("MS Gothic"))
            {
                return;
            }

            Assert.Contains("ＭＳ ゴシック", CadTrueTypeFonts.LocalNamesOf("MS Gothic"));
        }

        [Fact]
        public void ALatinFamilyWithOneName_HasNoOtherName()
        {
            if (!Has("Arial"))
            {
                return;
            }

            Assert.Empty(CadTrueTypeFonts.LocalNamesOf("Arial"));
        }

        [Fact]
        public void TheFamilyIsFoundWhateverTheCaseOfItsName()
        {
            if (!Has("Malgun Gothic"))
            {
                return;
            }

            Assert.Equal(
                CadTrueTypeFonts.LocalNamesOf("Malgun Gothic"),
                CadTrueTypeFonts.LocalNamesOf("MALGUN gothic")
            );
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("GhNoSuchFontFamily")]
        [InlineData("arial.ttf")]
        public void ANameThatIsNoFamily_HasNoOtherName(string family)
        {
            Assert.Empty(CadTrueTypeFonts.LocalNamesOf(family));
        }

        [Fact]
        public void EveryFamilysOtherNames_AreDistinct_NeverTheEnglishNameAndTheSameOnEachRead()
        {
            foreach (string family in CadTrueTypeFonts.Families)
            {
                var names = CadTrueTypeFonts.LocalNamesOf(family);

                Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
                Assert.DoesNotContain(family, names, StringComparer.OrdinalIgnoreCase);
                Assert.All(names, n => Assert.False(string.IsNullOrWhiteSpace(n), family));
                Assert.Equal(names, CadTrueTypeFonts.LocalNamesOf(family));
            }
        }

        [Fact]
        public void TheFamiliesAndTheirFilesAreAsBefore()
        {
            if (!Has("Arial"))
            {
                return;
            }

            Assert.Equal("arial.ttf", CadTrueTypeFonts.FileOf("Arial"), ignoreCase: true);
            Assert.Equal("Arial", CadTrueTypeFonts.FamilyOf("arial.ttf"));
            Assert.True(File.Exists(CadTrueTypeFonts.PathOf("Arial", false, false)));
        }
    }
}
