using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Text Editor tab's tools, as AutoCAD 2021's: the complete Symbol menu and its character map (Other…), Match Text
    /// Formatting, Import Text, AutoCAPS, the editor's own Find and Replace, and the Spell Check panel with the red wavy
    /// underline, the right-click suggestions and Edit Dictionaries. Each is driven through its model with no real dialog.
    /// </summary>
    public sealed class TextEditorToolsTests
    {
        private static readonly string[] SymbolNames =
        {
            "Degrees",
            "Plus/Minus",
            "Diameter",
            "Almost Equal",
            "Angle",
            "Boundary Line",
            "Centerline",
            "Delta",
            "Electrical Phase",
            "Flow Line",
            "Identity",
            "Initial Length",
            "Monument Line",
            "Not Equal",
            "Ohm",
            "Omega",
            "Plate/Property Line",
            "Subscript 2",
            "Squared",
            "Cubed",
            "Non-breaking Space",
        };

        private static void Sta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
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

        private static TextEditorViewModel Edited(string contents, out MainViewModel model, out CadDocument document)
        {
            model = new MainViewModel();
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

        private static CadMenuItemViewModel Item(IEnumerable<CadMenuItemViewModel> menu, string startsWith) =>
            menu.Single(i => i.Header.StartsWith(startsWith, StringComparison.Ordinal));

        [Fact]
        public void TheSymbolMenu_IsAutoCadsTwentyOneItemsInItsOrder_ThenOther()
        {
            TextEditorViewModel editor = Edited("Plate", out _, out _);

            IReadOnlyList<CadMenuItemViewModel> menu = editor.Menu("Symbol");

            Assert.Equal(SymbolNames.Length + 2, menu.Count);
            Assert.Equal(
                SymbolNames,
                menu.Take(SymbolNames.Length).Select(i => i.Header.Split(new[] { "    " }, StringSplitOptions.None)[0])
            );
            Assert.Equal(string.Empty, menu[SymbolNames.Length].Header);
            Assert.Equal("Other…", menu[SymbolNames.Length + 1].Header);
        }

        [Theory]
        [InlineData("Degrees", @"%%d")]
        [InlineData("Plus/Minus", @"%%p")]
        [InlineData("Diameter", @"%%c")]
        [InlineData("Almost Equal", @"\U+2248")]
        [InlineData("Angle", @"\U+2220")]
        [InlineData("Boundary Line", @"\U+E100")]
        [InlineData("Centerline", @"\U+2104")]
        [InlineData("Delta", @"\U+0394")]
        [InlineData("Electrical Phase", @"\U+0278")]
        [InlineData("Flow Line", @"\U+E101")]
        [InlineData("Identity", @"\U+2261")]
        [InlineData("Initial Length", @"\U+E200")]
        [InlineData("Monument Line", @"\U+E102")]
        [InlineData("Not Equal", @"\U+2260")]
        [InlineData("Ohm", @"\U+2126")]
        [InlineData("Omega", @"\U+03A9")]
        [InlineData("Plate/Property Line", @"\U+214A")]
        [InlineData("Subscript 2", @"\U+2082")]
        [InlineData("Squared", @"\U+00B2")]
        [InlineData("Cubed", @"\U+00B3")]
        public void EachSymbol_IsWrittenAsAutoCadWritesIt_AndShownAsItsLetter(string name, string written)
        {
            TextEditorViewModel editor = Edited("AB", out _, out _);
            editor.Editor.MoveTo(1);

            Item(editor.Menu("Symbol"), name).Command.Execute(null);

            Assert.Equal("A" + written + "B", editor.Editor.Contents);
            Assert.Equal(3, editor.Editor.Length);
            Assert.Equal(1, editor.Editor.TextAt(1).Length);
        }

        [Fact]
        public void TheNonBreakingSpace_IsTypedAsAHardSpace_AndOtherOpensTheCharacterMapOfTheLettersFont()
        {
            TextEditorViewModel editor = Edited("AB", out MainViewModel model, out _);
            editor.Editor.MoveTo(1);
            Item(editor.Menu("Symbol"), "Non-breaking Space").Command.Execute(null);
            Assert.Equal("A" + (char)0xA0 + "B", editor.Editor.Text);

            CharacterMapViewModel shown = null;
            model.ShowCharacterMap = dialog => shown = dialog;
            editor.Editor.SetFont("Courier New", false);

            Item(editor.Menu("Symbol"), "Other").Command.Execute(null);

            Assert.NotNull(shown);
            Assert.Equal("Courier New", shown.Font);
        }

        [Fact]
        public void TheCharacterMap_ListsTheGlyphsOfTheFontBySet_SelectGathersThemAndCopyPutsThemOnTheClipboard()
        {
            Sta(() =>
            {
                var map = new CharacterMapViewModel("Arial");
                string copied = null;
                map.SetClipboardText = text => copied = text;

                Assert.Equal("Arial", map.Font);
                Assert.Equal("Basic Latin", map.CharacterSet.Name);
                Assert.Equal(95, map.Cells.Count);
                Assert.Contains(map.CharacterSets, s => s.Name == "Greek and Coptic");
                Assert.DoesNotContain(map.CharacterSets, s => s.Name == "Private Use Area");

                map.CharacterSet = map.CharacterSets.Single(s => s.Name == "Greek and Coptic");
                map.Selected = map.Cells.Single(c => c.Code == 0x394);
                Assert.Equal("U+0394", map.Info);
                map.Select();
                map.Selected = map.Cells.Single(c => c.Code == 0x3A9);
                map.Select();
                Assert.Equal(char.ConvertFromUtf32(0x394) + char.ConvertFromUtf32(0x3A9), map.CharactersToCopy);

                map.Copy();
                Assert.Equal(map.CharactersToCopy, copied);
                Assert.StartsWith("Copied", map.Message);
            });
        }

        [Fact]
        public void TheCharacterMapOfAnotherFont_ListsItsOwnCharacters_AndTheMapOfAnUnknownFontIsEmpty()
        {
            Sta(() =>
            {
                var map = new CharacterMapViewModel("Arial");
                int arial = map.Cells.Count;

                map.Font = "Symbol";

                Assert.Equal("Symbol", map.Font);
                Assert.NotEmpty(map.CharacterSets);
                Assert.NotEqual(0, arial);

                var none = new CharacterMapViewModel("No Such Family 123");
                Assert.Empty(none.Cells);
                Assert.Empty(none.CharacterSets);
            });
        }

        [Fact]
        public void MatchTextFormatting_TakesTheLookOfTheSelection_AndGivesItToTheNextOne()
        {
            TextEditorViewModel editor = Edited(@"A{\C1;B}CD", out _, out _);
            editor.Editor.Select(1, 1);

            editor.MatchCommand.Execute(null);
            Assert.True(editor.IsMatching);

            editor.Editor.Select(2, 2);
            Assert.True(editor.Editor.ApplyMatch());

            Assert.False(editor.IsMatching);
            Assert.Equal(@"A{\C1;BCD}", editor.Editor.Contents);
        }

        [Fact]
        public void EscWithTheMatchBrushUp_PutsItDown_AndOnlyThenAsksAboutTheChanges()
        {
            Sta(() =>
            {
                TextEditorViewModel editor = Edited("AB CD", out _, out CadDocument document);
                var box = new CadMTextEditBox
                {
                    Document = document,
                    Model = editor,
                    PixelsPerUnit = 4.0,
                };
                bool asked = false;
                box.EscapePressed += (s, e) => asked = true;
                editor.Editor.Select(0, 2);
                editor.MatchCommand.Execute(null);

                Assert.True(box.HandleKey(Key.Escape, ModifierKeys.None));
                Assert.False(editor.IsMatching);
                Assert.False(asked);

                Assert.True(box.HandleKey(Key.Escape, ModifierKeys.None));
                Assert.True(asked);
            });
        }

        [Fact]
        public void ImportText_PutsAnRtfFileInAtTheCaretWithItsBoldAndParagraphs_OneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                string path = Path.Combine(folder.FullPath, "memo.rtf");
                File.WriteAllText(path, @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\f0 Plain \b bold\b0\par second}");
                TextEditorViewModel editor = Edited("AB", out MainViewModel model, out _);
                model.AskImportTextFile = () => path;
                editor.Editor.MoveTo(1);

                Assert.True(editor.ImportTextCommand.CanExecute(null));
                editor.ImportTextCommand.Execute(null);

                Assert.Equal("APlain bold\nsecondB", editor.Editor.Text);
                Assert.True(editor.Editor.LookAt(7).Bold);
                Assert.False(editor.Editor.LookAt(1).Bold);
                Assert.Equal(2, editor.Editor.ParagraphCount);
                Assert.True(editor.Editor.Undo());
                Assert.Equal("AB", editor.Editor.Contents);
            }
        }

        [Fact]
        public void ImportText_SaysWhenAFileIsOver32KilobytesOrIsNotThere_AndPutsNothingIn()
        {
            using (var folder = new TestFolder())
            {
                string big = Path.Combine(folder.FullPath, "big.txt");
                File.WriteAllText(big, new string('x', 40000));
                TextEditorViewModel editor = Edited("AB", out MainViewModel model, out _);

                Assert.False(editor.ImportText(big));
                Assert.False(editor.ImportText(Path.Combine(folder.FullPath, "none.txt")));
                Assert.False(editor.ImportText(null));

                Assert.Equal("AB", editor.Editor.Contents);
                Assert.Contains(model.Session.History, h => h.StartsWith("Import Text: ") && h.Contains("32 KB"));
            }
        }

        [Fact]
        public void AutoCaps_TurnsTheLettersTypedAfterItIntoCapitals()
        {
            TextEditorViewModel editor = Edited("ab", out _, out _);
            editor.Editor.MoveTo(2);

            editor.IsAutoCaps = true;
            editor.Editor.Type("cd");

            Assert.Equal("abCD", editor.Editor.Contents);
            Assert.True(editor.IsAutoCaps);
        }

        [Fact]
        public void TheFindAndReplaceDialog_OpensFromTheTabAndCtrlH_AndClosesWithTheEditor()
        {
            Sta(() =>
            {
                TextEditorViewModel editor = Edited("AB", out MainViewModel model, out CadDocument document);
                var shown = new List<TextEditorViewModel>();
                model.ShowMTextFindReplace = shown.Add;
                var box = new CadMTextEditBox { Document = document, Model = editor };
                bool closed = false;
                editor.Closed += (s, e) => closed = true;

                editor.FindReplaceCommand.Execute(null);
                Assert.True(box.HandleKey(Key.H, ModifierKeys.Control));

                Assert.Equal(2, shown.Count);
                Assert.All(shown, s => Assert.Same(editor, s));

                model.CloseTextEditor(true);
                Assert.True(closed);
            });
        }

        [Fact]
        public void TheFindAndReplaceCommands_UseTheFiveOptions_AndSayWhatTheyDid()
        {
            TextEditorViewModel editor = Edited("Bolt bolted M20 M24 bolt", out _, out _);
            editor.FindText = "bolt";
            editor.ReplaceText = "nut";
            editor.WholeWords = true;
            editor.MatchCase = true;

            editor.FindNextCommand.Execute(null);
            Assert.Equal((20, 4), (editor.Editor.SelectionStart, editor.Editor.SelectionLength));
            Assert.Equal(string.Empty, editor.FindMessage);

            editor.ReplaceCommand.Execute(null);
            Assert.Equal("Bolt bolted M20 M24 nut", editor.Editor.Contents);
            Assert.Equal("Replaced. No more matches.", editor.FindMessage);

            editor.FindText = "M2?";
            editor.UseWildcards = true;
            editor.ReplaceText = "X";
            editor.ReplaceAllCommand.Execute(null);
            Assert.Equal("Bolt bolted X X nut", editor.Editor.Contents);
            Assert.Equal("2 replaced.", editor.FindMessage);

            editor.FindText = "zzz";
            editor.FindNextCommand.Execute(null);
            Assert.Equal("\"zzz\" was not found.", editor.FindMessage);
        }

        [Fact]
        public void DiacriticsAndWidthOptions_ReachTheEditorsSearch()
        {
            TextEditorViewModel editor = Edited("caf" + (char)0xE9, out _, out _);
            editor.FindText = "cafe";

            editor.FindNextCommand.Execute(null);
            Assert.Equal(4, editor.Editor.SelectionLength);

            editor.Editor.MoveTo(0);
            editor.MatchDiacritics = true;
            editor.FindNextCommand.Execute(null);
            Assert.Equal("\"cafe\" was not found.", editor.FindMessage);
            Assert.False(editor.MatchHalfFullWidth);
        }

        [Fact]
        public void SpellCheck_IsOnFromTheStart_FindsTheWordsNotInTheDictionary_AndTheToggleTurnsItOff()
        {
            TextEditorViewModel editor = Edited("The wrold is flat teh", out _, out _);

            Assert.True(editor.IsSpellCheckOn);
            Assert.Equal(new[] { "wrold", "teh" }, editor.Misspelled.Select(w => w.Word));

            editor.IsSpellCheckOn = false;
            Assert.Empty(editor.Misspelled);

            editor.IsSpellCheckOn = true;
            Assert.Equal(2, editor.Misspelled.Count);
        }

        [Fact]
        public void TheMisspelledWords_FollowWhatIsTyped()
        {
            TextEditorViewModel editor = Edited("The wrold", out _, out _);
            Assert.Single(editor.Misspelled);

            editor.Editor.Select(4, 5);
            editor.Editor.Type("world");
            Assert.Empty(editor.Misspelled);

            editor.Editor.Type("x");
            Assert.Equal(new[] { "worldx" }, editor.Misspelled.Select(w => w.Word));
        }

        [Fact]
        public void TheRightClickOnAMisspelledWord_OffersTheWordsNearIt_AndOneReplacesIt()
        {
            TextEditorViewModel editor = Edited("The wrold is flat", out _, out _);

            IReadOnlyList<CadMenuItemViewModel> menu = editor.SpellMenu(6);

            Assert.Equal("world", menu[0].Header);
            Assert.Contains(menu, i => i.Header == "Add Word");
            Assert.Contains(menu, i => i.Header == "Ignore All");
            Assert.Equal(string.Empty, menu.Last().Header);
            Assert.Empty(editor.SpellMenu(1));

            menu[0].Command.Execute(null);

            Assert.Equal("The world is flat", editor.Editor.Contents);
            Assert.Empty(editor.Misspelled);
            Assert.True(editor.Editor.Undo());
            Assert.Equal("The wrold is flat", editor.Editor.Contents);
        }

        [Fact]
        public void AddWord_PutsTheWordInTheCustomFile_AndEveryOtherEditorKnowsItAfter_IgnoreAllLeavesItForTheSession()
        {
            using (var folder = new TestFolder())
            {
                string file = Path.Combine(folder.FullPath, "dictionary", "sample.cus");
                TextEditorViewModel editor = Edited("Tekla and Zorgle", out MainViewModel model, out _);
                model.SpellDictionaryPath = file;
                Assert.Equal(2, editor.Misspelled.Count);

                Item(editor.SpellMenu(2), "Add Word").Command.Execute(null);
                Assert.Equal(new[] { "Tekla" }, File.ReadAllLines(file));
                Assert.Equal(new[] { "Zorgle" }, editor.Misspelled.Select(w => w.Word));

                Item(editor.SpellMenu(14), "Ignore All").Command.Execute(null);
                Assert.Empty(editor.Misspelled);
                Assert.Equal(new[] { "Tekla" }, File.ReadAllLines(file));
                Assert.Equal(new[] { "Tekla" }, model.SpellDictionary.CustomWords);
            }
        }

        [Fact]
        public void EditDictionaries_AddsAndDeletesTheCustomWords_InTheFileAtOnce_AndTheEditorChecksAgain()
        {
            using (var folder = new TestFolder())
            {
                string file = Path.Combine(folder.FullPath, "sample.cus");
                File.WriteAllText(file, "Zorgle\n");
                TextEditorViewModel editor = Edited("Zorgle Blorf", out MainViewModel model, out _);
                model.SpellDictionaryPath = file;
                SpellDictionariesViewModel shown = null;
                model.ShowDictionaries = dialog =>
                {
                    shown = dialog;
                    dialog.NewWord = "Blorf";
                    dialog.Add();
                    dialog.Selected = "Zorgle";
                    dialog.Delete();
                };

                Assert.Equal(new[] { "Blorf" }, editor.Misspelled.Select(w => w.Word));
                editor.EditDictionariesCommand.Execute(null);

                Assert.Equal(new[] { "Blorf" }, File.ReadAllLines(file));
                Assert.Equal(new[] { "Blorf" }, shown.Words);
                Assert.Equal(new[] { "Zorgle" }, editor.Misspelled.Select(w => w.Word));
                Assert.Contains("words", shown.MainDictionary);
                Assert.Equal(file, shown.CustomFile);
            }
        }

        [Fact]
        public void TheDictionariesDialog_RefusesAWordAlreadyThereOrWithSpaces()
        {
            var dictionary = new CadSpellDictionary();
            var dialog = new SpellDictionariesViewModel(dictionary);
            Assert.Contains("session", dialog.CustomFile);

            dialog.NewWord = "Zorgle";
            dialog.Add();
            dialog.NewWord = "zorgle";
            dialog.Add();
            Assert.Contains("is in the dictionary already", dialog.Message);

            dialog.NewWord = "two words";
            dialog.Add();
            Assert.Contains("no spaces", dialog.Message);
            Assert.Equal(new[] { "Zorgle" }, dialog.Words);
            Assert.Equal("two words", dialog.NewWord);
        }

        [Fact]
        public void TheSpellCheckerUnderlinesInRed_InTheFrameTheWordsAreSetIn()
        {
            Sta(() =>
            {
                TextEditorViewModel editor = Edited("AAAA wrold", out _, out CadDocument document);
                var box = new CadMTextEditBox
                {
                    Document = document,
                    Model = editor,
                    PixelsPerUnit = 4.0,
                };
                box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                box.Arrange(new Rect(box.DesiredSize));

                int on = RedPixels(box);
                editor.IsSpellCheckOn = false;
                box.UpdateLayout();
                int off = RedPixels(box);

                Assert.True(on > 20, "red pixels with the check on: " + on);
                Assert.Equal(0, off);
            });
        }

        [Fact]
        public void TheThreeWindows_AreBuiltOverTheirModels_WithoutBeingShown_AndSizedToTheirBoards()
        {
            Sta(() =>
            {
                TextEditorViewModel editor = Edited("AB", out _, out _);
                var find = new MTextFindReplaceWindow(editor);
                var map = new CharacterMapWindow(new CharacterMapViewModel("Arial"));
                var dictionaries = new SpellDictionariesWindow(
                    new SpellDictionariesViewModel(new CadSpellDictionary())
                );

                foreach (Window window in new Window[] { find, map, dictionaries })
                {
                    Assert.NotNull(window.Content);
                    Assert.NotNull(window.DataContext);
                }

                Assert.Equal(470.0, find.Width);
                Assert.Equal(560.0, map.Width);
                Assert.Equal(500.0, map.Height);
                Assert.Equal(440.0, dictionaries.Width);
                Assert.Equal(420.0, dictionaries.Height);

                // Closing the editor closes the Find and Replace that is open beside it.
                bool closed = false;
                find.Closed += (s, e) => closed = true;
                editor.RaiseClosed();
                Assert.True(closed);
                map.Close();
                dictionaries.Close();
            });
        }

        private static int RedPixels(CadMTextEditBox box)
        {
            int width = (int)Math.Ceiling(box.ActualWidth);
            int height = (int)Math.Ceiling(box.ActualHeight);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(box);
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            int red = 0;

            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                if (pixels[i + 2] > 200 && pixels[i + 1] < 120 && pixels[i] < 120)
                {
                    red++;
                }
            }

            return red;
        }
    }
}
