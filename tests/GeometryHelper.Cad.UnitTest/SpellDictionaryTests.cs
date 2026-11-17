using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The built-in English dictionary (the SCOWL lists, about 90 000 words) and the custom dictionary of the MTEXT editor's
    /// Spell Check panel: which words it knows, the words it offers for a misspelled one, the misspelled words of an editor
    /// found by place, and Add Word and Delete written to a file of words one to a line.
    /// </summary>
    public class SpellDictionaryTests
    {
        private static readonly string Folder = TestRun.Folder("spell-dictionary");

        [Fact]
        public void TheBuiltInDictionary_HoldsTensOfThousandsOfWords()
        {
            Assert.InRange(CadSpellDictionary.BuiltInCount, 80000, 200000);
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("Plates")]
        [InlineData("colour")]
        [InlineData("color")]
        [InlineData("galvanised")]
        [InlineData("centreline")]
        [InlineData("don't")]
        [InlineData("bolted")]
        [InlineData("tolerances")]
        [InlineData("Wednesday")]
        public void CommonWordsOfBothSpellingsAndDraftingTerms_AreKnown(string word)
        {
            Assert.False(new CadSpellDictionary().IsMisspelled(word));
        }

        [Theory]
        [InlineData("wrold")]
        [InlineData("Helo")]
        [InlineData("recieve")]
        [InlineData("thiss")]
        [InlineData("plaet")]
        public void MisspelledWords_AreFound(string word)
        {
            Assert.True(new CadSpellDictionary().IsMisspelled(word));
        }

        [Theory]
        [InlineData("wrold", "world")]
        [InlineData("Helo", "Hello")]
        [InlineData("recieve", "receive")]
        [InlineData("plaet", "plate")]
        [InlineData("frist", "first")]
        public void TheFirstSuggestion_IsTheWordMeant_ByCommonnessAndKindOfSlip(string word, string meant)
        {
            IReadOnlyList<string> suggestions = new CadSpellDictionary().Suggest(word);

            Assert.Equal(meant, suggestions[0]);
            Assert.InRange(suggestions.Count, 1, 5);
        }

        [Fact]
        public void TheSuggestions_AreTheSameEveryTime_AndTakeTheCaseOfTheWord()
        {
            var dictionary = new CadSpellDictionary();

            Assert.Equal(dictionary.Suggest("Wrold"), dictionary.Suggest("Wrold"));
            Assert.All(dictionary.Suggest("Wrold"), s => Assert.True(char.IsUpper(s[0])));
            Assert.All(dictionary.Suggest("wrold"), s => Assert.True(char.IsLower(s[0])));
        }

        [Fact]
        public void SuggestingForAWordIsFast_ThoughTheDictionaryHoldsTensOfThousands()
        {
            var dictionary = new CadSpellDictionary();
            dictionary.Suggest("warmup");
            var clock = System.Diagnostics.Stopwatch.StartNew();

            dictionary.Suggest("confguration");

            Assert.InRange(clock.ElapsedMilliseconds, 0, 2000);
        }

        [Fact]
        public void Check_FindsTheMisspelledWordsByPlace_AStackAndASymbolEndingAWord()
        {
            var editor = new CadMTextEditor(@"The wrold is {\C1;teh} 1\S1/2;plaet%%dx CAPS a1", 2.5, "Arial");

            IReadOnlyList<CadMisspelledWord> found = new CadSpellDictionary().Check(editor);

            Assert.Equal(new[] { "wrold", "teh", "plaet" }, found.Select(w => w.Word));
            Assert.Equal(new[] { 4, 13, 19 }, found.Select(w => w.Start));
            Assert.All(found, w =>
                Assert.Equal(w.Word, string.Concat(Enumerable.Range(w.Start, w.Length).Select(editor.TextAt)))
            );
        }

        [Fact]
        public void Check_PassesOverCapitalsDigitsAndOneLetterWords_UntilTheSettingsSayOtherwise()
        {
            var editor = new CadMTextEditor("BOLTZ abc1 x Wrold", 2.5, "Arial");
            var dictionary = new CadSpellDictionary();

            Assert.Equal(new[] { "Wrold" }, dictionary.Check(editor).Select(w => w.Word));

            dictionary.Settings = new CadSpellSettings { IgnoreCapitalized = true, IgnoreUppercase = false };
            Assert.Equal(new[] { "BOLTZ" }, dictionary.Check(editor).Select(w => w.Word));
        }

        [Fact]
        public void AWordWithQuotesAroundIt_IsCheckedWithoutThem()
        {
            var editor = new CadMTextEditor("'wrold' don't", 2.5, "Arial");

            CadMisspelledWord only = new CadSpellDictionary().Check(editor).Single();

            Assert.Equal((1, 5, "wrold"), (only.Start, only.Length, only.Word));
        }

        [Fact]
        public void IgnoreAll_LeavesTheWordAlone_ForAsLongAsTheDictionaryLives()
        {
            var editor = new CadMTextEditor("wrold wrold", 2.5, "Arial");
            var dictionary = new CadSpellDictionary();
            Assert.Equal(2, dictionary.Check(editor).Count);

            dictionary.IgnoreAll("WROLD");

            Assert.Empty(dictionary.Check(editor));
            Assert.True(new CadSpellDictionary().IsMisspelled("wrold"));
        }

        [Fact]
        public void AddWord_AppendsToTheFile_MakingItAndItsFolder_AndANewDictionaryReadsItBack()
        {
            string path = Path.Combine(Folder, "new-folder", "sample.cus");
            var dictionary = new CadSpellDictionary(path);

            Assert.Null(dictionary.Add("Tekla"));
            Assert.Null(dictionary.Add("tekla"));
            Assert.Null(dictionary.Add("  Dxf  "));

            Assert.Equal(new[] { "Tekla", "Dxf" }, File.ReadAllLines(path));
            Assert.False(dictionary.IsMisspelled("TEKLA"));
            Assert.False(new CadSpellDictionary(path).IsMisspelled("dxf"));
            Assert.Equal(new[] { "Dxf", "Tekla" }, dictionary.CustomWords);
        }

        [Fact]
        public void Remove_TakesTheWordOutOfTheFile_AndLeavesTheOtherLinesAsTheyWere()
        {
            string path = Path.Combine(Folder, "keep.cus");
            File.WriteAllText(path, "# office words\nZorgle\nplinth\nZorgle2\n");
            var dictionary = new CadSpellDictionary(path);
            Assert.False(dictionary.IsMisspelled("zorgle"));

            Assert.Null(dictionary.Remove("zorgle"));

            Assert.Equal(new[] { "# office words", "plinth", "Zorgle2" }, File.ReadAllLines(path));
            Assert.True(dictionary.IsMisspelled("zorgle"));
            Assert.Null(dictionary.Remove("never-there"));
        }

        [Fact]
        public void ADictionaryOnAFileThatCannotBeWritten_SaysSo_AndKeepsTheWordForTheSession()
        {
            string blocker = Path.Combine(Folder, "a-file");
            File.WriteAllText(blocker, "x");
            var dictionary = new CadSpellDictionary(Path.Combine(blocker, "sample.cus"));

            Assert.Equal("Cannot update custom dictionary.", dictionary.Add("Zorgle"));

            Assert.False(dictionary.IsMisspelled("Zorgle"));
        }

        [Fact]
        public void ADictionaryWithNoFile_KeepsItsWordsForTheSessionOnly()
        {
            var dictionary = new CadSpellDictionary();

            Assert.Null(dictionary.Add("Zorgle"));

            Assert.False(dictionary.IsMisspelled("zorgle"));
            Assert.True(new CadSpellDictionary().IsMisspelled("zorgle"));
        }

        [Fact]
        public void SpellStartsWithTheSessionsDictionaryFile_AndAddMakesIt()
        {
            string path = Path.Combine(Folder, "from-session", "sample.cus");
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Text(0, 0, 5, "Helo Tekla", handle: "T1")),
                "texts.dxf"
            );
            CadCommandSession session = Session(document);
            session.SpellDictionaryPath = path;

            Run(session, "SPELL", Enter);
            Run(session, "Add");
            Run(session, "Add");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "Helo", "Tekla" }, File.ReadAllLines(path));

            CadCommandSession again = Session(document);
            again.SpellDictionaryPath = path;
            Run(again, "SPELL", Enter);
            Assert.DoesNotContain(again.History, h => h.StartsWith("Not in dictionary"));
        }
    }
}
