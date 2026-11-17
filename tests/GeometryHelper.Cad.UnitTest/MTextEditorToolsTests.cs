using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The MTEXT editor's tools beyond its formatting: Find and Replace with AutoCAD 2021's five options, AutoCAPS, Match
    /// Text Formatting, and the words of Import Text put in at the caret.
    /// </summary>
    public class MTextEditorToolsTests
    {
        private static readonly string Folder = TestRun.Folder("mtext-tools");

        private static CadMTextEditor Editor(string contents) => new CadMTextEditor(contents, 2.5, "Arial");

        [Fact]
        public void FindNext_IgnoresCapitalsUntilMatchCaseIsSet_AndGoesRoundToTheStart()
        {
            CadMTextEditor editor = Editor("Plate plate PLATE");
            var options = new CadMTextFindOptions();

            Assert.True(editor.FindNext("plate", options));
            Assert.Equal((0, 5), (editor.SelectionStart, editor.SelectionLength));
            Assert.True(editor.FindNext("plate", options));
            Assert.Equal(6, editor.SelectionStart);
            Assert.True(editor.FindNext("plate", options));
            Assert.Equal(12, editor.SelectionStart);
            Assert.True(editor.FindNext("plate", options));
            Assert.Equal(0, editor.SelectionStart);

            options.MatchCase = true;
            editor.MoveTo(0);
            Assert.True(editor.FindNext("plate", options));
            Assert.Equal(6, editor.SelectionStart);
            Assert.True(editor.FindNext("plate", options));
            Assert.Equal(6, editor.SelectionStart);
            Assert.False(editor.FindNext("PLATES", options));
        }

        [Fact]
        public void WholeWords_FindsBoltButNotBolted()
        {
            CadMTextEditor editor = Editor("bolted, bolt.");

            Assert.True(editor.FindNext("bolt", new CadMTextFindOptions { WholeWords = true }));

            Assert.Equal((8, 4), (editor.SelectionStart, editor.SelectionLength));
        }

        [Fact]
        public void Wildcards_FindM2AndOneMoreCharacter_AndAnUnreadablePatternFindsNothing()
        {
            CadMTextEditor editor = Editor("M20 M24 N20");
            var options = new CadMTextFindOptions { UseWildcards = true };

            Assert.True(editor.FindNext("M2?", options));
            Assert.Equal((0, 3), (editor.SelectionStart, editor.SelectionLength));
            Assert.True(editor.FindNext("M2?", options));
            Assert.Equal(4, editor.SelectionStart);
            Assert.False(editor.FindNext("[", options));
        }

        [Fact]
        public void Diacritics_AreMatchedOnlyWhenAsked()
        {
            CadMTextEditor editor = Editor("caf\u00E9 au lait");

            Assert.True(editor.FindNext("cafe", new CadMTextFindOptions()));
            Assert.Equal((0, 4), (editor.SelectionStart, editor.SelectionLength));

            editor.MoveTo(0);
            Assert.False(editor.FindNext("cafe", new CadMTextFindOptions { MatchDiacritics = true }));
            Assert.True(editor.FindNext("caf\u00E9", new CadMTextFindOptions { MatchDiacritics = true }));
        }

        [Fact]
        public void HalfAndFullWidthForms_AreMatchedOnlyWhenAsked()
        {
            CadMTextEditor editor = Editor("\uFF21\uFF22\uFF23 ABC");

            Assert.True(editor.FindNext("ABC", new CadMTextFindOptions { MatchCase = true }));
            Assert.Equal(0, editor.SelectionStart);

            editor.MoveTo(0);
            Assert.True(
                editor.FindNext("ABC", new CadMTextFindOptions { MatchCase = true, MatchHalfFullWidth = true })
            );
            Assert.Equal(4, editor.SelectionStart);
        }

        [Fact]
        public void AStack_CountsAsOnePlace_SoTheMatchAfterItIsSelectedAtItsPlace()
        {
            CadMTextEditor editor = Editor(@"\S1/2; bolt");

            Assert.True(editor.FindNext("bolt", new CadMTextFindOptions()));

            Assert.Equal((2, 4), (editor.SelectionStart, editor.SelectionLength));
            Assert.Equal("bolt", editor.SelectedText);
        }

        [Fact]
        public void Replace_FirstSelectsTheMatch_ThenReplacesItAndSelectsTheNext_InOneUndoStepEach()
        {
            CadMTextEditor editor = Editor("bolt and bolt");
            var options = new CadMTextFindOptions();

            Assert.False(editor.Replace("bolt", "nut", options));
            Assert.Equal((0, 4), (editor.SelectionStart, editor.SelectionLength));

            Assert.True(editor.Replace("bolt", "nut", options));
            Assert.Equal("nut and bolt", editor.Contents);
            Assert.Equal((8, 4), (editor.SelectionStart, editor.SelectionLength));

            Assert.True(editor.Replace("bolt", "nut", options));
            Assert.Equal("nut and nut", editor.Contents);

            Assert.True(editor.Undo());
            Assert.Equal("nut and bolt", editor.Contents);
        }

        [Fact]
        public void ReplaceAll_TakesWildcardsAndKeepsTheLookOfTheFirstLetter_AndTheTextPutInIsNotSearchedAgain()
        {
            CadMTextEditor editor = Editor(@"M20 {\C1;M24} aaa");

            Assert.Equal(2, editor.ReplaceAll("M2?", "Bolt", new CadMTextFindOptions { UseWildcards = true }));
            Assert.Equal(@"Bolt {\C1;Bolt} aaa", editor.Contents);

            Assert.Equal(3, editor.ReplaceAll("a", "aa", new CadMTextFindOptions()));
            Assert.Equal(@"Bolt {\C1;Bolt} aaaaaa", editor.Contents);

            Assert.True(editor.Undo());
            Assert.Equal(@"Bolt {\C1;Bolt} aaa", editor.Contents);
        }

        [Fact]
        public void TheFindOfPlainCapitalsAndTheOldOverloads_StillWork()
        {
            CadMTextEditor editor = Editor("Bolt bolt");

            Assert.True(editor.FindNext("bolt", true));
            Assert.Equal(5, editor.SelectionStart);
            Assert.Equal(2, editor.ReplaceAll("BOLT", "nut", false));
            Assert.Equal("nut nut", editor.Contents);
        }

        [Fact]
        public void AutoCaps_TurnsLettersTypedIntoCapitals_AndLeavesTheOnesThereAlone()
        {
            CadMTextEditor editor = Editor("abc");
            editor.MoveTo(3);

            editor.AutoCaps = true;
            editor.Type("def");
            Assert.Equal("abcDEF", editor.Contents);

            editor.AutoCaps = false;
            editor.Type("ghi");
            Assert.Equal("abcDEFghi", editor.Contents);
        }

        [Fact]
        public void MatchTextFormatting_GivesTheLookOfTheFirstSelectionToTheNext_ThenEnds()
        {
            CadMTextEditor editor = Editor(@"A{\C1;B}CD");
            editor.Select(1, 1);

            editor.BeginMatch();
            Assert.True(editor.IsMatching);

            // Nothing is selected yet, so the brush waits.
            editor.MoveTo(3);
            Assert.False(editor.ApplyMatch());
            Assert.True(editor.IsMatching);

            editor.Select(2, 2);
            Assert.True(editor.ApplyMatch());

            Assert.False(editor.IsMatching);
            Assert.Equal(@"A{\C1;BCD}", editor.Contents);
            Assert.True(editor.Undo());
            Assert.Equal(@"A{\C1;B}CD", editor.Contents);
        }

        [Fact]
        public void CancelMatch_EndsTheBrushWithNothingGiven()
        {
            CadMTextEditor editor = Editor(@"A{\C1;B}CD");
            editor.Select(1, 1);
            editor.BeginMatch();

            editor.CancelMatch();
            editor.Select(2, 2);

            Assert.False(editor.ApplyMatch());
            Assert.Equal(@"A{\C1;B}CD", editor.Contents);
        }

        [Fact]
        public void APlainTextFile_IsInsertedAtTheCaret_EachLineAParagraph_InTheLookTypedThere()
        {
            string path = Path.Combine(Folder, "notes.txt");
            File.WriteAllText(path, "one\r\ntwo\nthree");
            CadMTextEditor editor = Editor("AB");
            editor.MoveTo(1);
            editor.SetItalic(true);

            editor.InsertContent(CadMTextImport.FromFile(path, 2.5));

            Assert.Equal("Aone\ntwo\nthreeB", editor.Text);
            Assert.True(editor.LookAt(1).Italic);
            Assert.True(editor.LookAt(5).Italic);
            Assert.False(editor.LookAt(0).Italic);
            Assert.Equal(3, editor.ParagraphCount);
        }

        [Fact]
        public void APlainTextFile_ReplacesTheSelection_InCapitalsWhileAutoCapsIsOn_AndOneUndoTakesItBack()
        {
            string path = Path.Combine(Folder, "caps.txt");
            File.WriteAllText(path, "new words");
            CadMTextEditor editor = Editor("old text");
            editor.Select(4, 4);
            editor.AutoCaps = true;

            editor.InsertContent(CadMTextImport.FromFile(path, 2.5));

            Assert.Equal("old NEW WORDS", editor.Contents);
            Assert.True(editor.Undo());
            Assert.Equal("old text", editor.Contents);
        }

        [Fact]
        public void ATextFileInLatin1OrUtf8WithABom_IsReadByItsLetters()
        {
            string latin = Path.Combine(Folder, "latin.txt");
            File.WriteAllBytes(latin, new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x93 });
            string bom = Path.Combine(Folder, "bom.txt");
            File.WriteAllBytes(bom, new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xA9, 0x41 });

            Assert.Equal("caf\u00E9\u201C", CadMTextImport.FromFile(latin, 2.5).PlainText);
            Assert.Equal("\u00E9A", CadMTextImport.FromFile(bom, 2.5).PlainText);
        }

        [Fact]
        public void AFileOver32Kilobytes_IsRefused_AndOneOfExactly32IsTaken()
        {
            string big = Path.Combine(Folder, "big.txt");
            File.WriteAllText(big, new string('a', CadMTextImport.MaxBytes + 1));
            string edge = Path.Combine(Folder, "edge.txt");
            File.WriteAllText(edge, new string('b', CadMTextImport.MaxBytes));

            var refused = Assert.Throws<IOException>(() => CadMTextImport.FromFile(big, 2.5));

            Assert.Contains("32 KB", refused.Message);
            Assert.Equal(CadMTextImport.MaxBytes, CadMTextImport.FromFile(edge, 2.5).PlainText.Length);
        }

        private const string Rtf =
            @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fswiss Arial;}{\f1\fmodern Courier New;}}"
            + @"{\colortbl ;\red255\green0\blue0;}{\*\generator Test;}"
            + @"\pard\f0\fs24 Plain \b bold\b0  \i italic\i0  \ul under\ulnone\par"
            + @"{\cf1\f1\fs48 big red mono}\par caf\'e9 \u8364? \tab end\par}";

        [Fact]
        public void AnRtf_KeepsBoldItalicUnderlineFontColourAndSize_AndTheParagraphs()
        {
            CadMTextContent content = CadMTextImport.FromRtf(Rtf, 2.5);

            Assert.Equal(4, content.Paragraphs.Count);
            CadMTextRun[] first = content.Paragraphs[0].Runs.ToArray();
            Assert.Equal("Plain ", first[0].Text);
            Assert.True(first[0].Look.IsPlain);
            Assert.Equal("bold", first[1].Text);
            Assert.True(first[1].Look.Bold);
            Assert.Equal("italic", first[3].Text);
            Assert.True(first[3].Look.Italic);
            Assert.Equal("under", first[5].Text);
            Assert.True(first[5].Look.Underline);

            CadMTextRun mono = content.Paragraphs[1].Runs.Single();
            Assert.Equal("big red mono", mono.Text);
            Assert.Equal("Courier New", mono.Look.Font);
            Assert.Equal(0xFF0000, mono.Look.TrueColor);
            Assert.Equal(5.0, mono.Look.Height);

            Assert.Equal("caf\u00E9 \u20AC \tend", content.Paragraphs[2].Text);
            Assert.DoesNotContain("Test", content.PlainText);
            Assert.DoesNotContain("Arial", content.PlainText);
        }

        [Fact]
        public void AnRtfFile_IsRecognisedByItsStart_WhateverItsName_AndInsertedWithItsLooks()
        {
            string path = Path.Combine(Folder, "styled.txt");
            File.WriteAllText(path, Rtf);
            CadMTextEditor editor = Editor("AB");
            editor.MoveTo(2);

            editor.InsertContent(CadMTextImport.FromFile(path, 2.5));

            Assert.StartsWith("ABPlain bold", editor.Text);
            Assert.True(editor.LookAt(8).Bold);
            Assert.Equal(4, editor.ParagraphCount);
            Assert.True(editor.Undo());
            Assert.Equal("AB", editor.Contents);
        }

        [Fact]
        public void TableAndPictureGroupsOfAnRtf_AreDropped_AndTheWordsOfAHyperlinkStay()
        {
            string rtf =
                @"{\rtf1{\fonttbl{\f0 Arial;}}{\stylesheet{\s1 Heading;}}{\pict\wmetafile8 0102}"
                + @"Go to {\field{\*\fldinst HYPERLINK ""x""}{\fldrslt site}} now{\header skip}\par}";

            Assert.Equal("Go to site now\n", CadMTextImport.FromRtf(rtf, 2.5).PlainText);
        }
    }
}
