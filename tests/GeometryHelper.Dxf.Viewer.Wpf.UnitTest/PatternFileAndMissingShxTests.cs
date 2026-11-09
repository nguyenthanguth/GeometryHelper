using System;
using System.IO;
using System.Linq;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The pattern file of the Hatch dialog, the file box of Load or Reload Linetypes and the Missing SHX files dialog, as the
    /// design boards of 2026-11-09 lay them out, each driven with no window.
    /// </summary>
    public class PatternFileAndMissingShxTests
    {
        private const string Steel = "*ANSI31,Not the built-in one\n45, 0,0, 0,1\n*GRATING-30,Grating at 30 degrees\n30, 0,0, 0,5\n120, 0,0, 0,5\n*MINE,Mine\n0, 0,0, 0,5\n";

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static MainViewModel Room(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            model.Document.SupportFolders = Array.Empty<string>();
            return model;
        }

        [Fact]
        public void TheHatchDialogStartsOnTheBuiltInPatterns_WithBuiltInOffAndTheCountUnderTheBox()
        {
            OnSta(() =>
            {
                var dialog = new HatchSettingsViewModel(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"), new CadHatchSettings(), false);

                Assert.True(dialog.IsBuiltInFile);
                Assert.Null(dialog.PatternFile);
                Assert.Equal("Built-in (GeometryHelper.pat)", dialog.PatternFileText);
                Assert.Equal("48 built-in patterns.", dialog.PatternFileHint);
                Assert.False(dialog.BuiltInCommand.CanExecute(null));
                Assert.Equal(48, dialog.Patterns.Count);
                Assert.Equal("ANSI Iron, Brick, Stone masonry", dialog.PatternCaption);
            });
        }

        [Fact]
        public void APatFileChosenByBrowse_AddsItsNewNamesAfterTheBuiltInOnes_AndItsPatternHatchesFromTheFile()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    string pat = folder.WriteText("steel-works.pat", Steel);
                    MainViewModel model = Room(folder);
                    model.ShowHatchSettings = dialog =>
                    {
                        dialog.BrowsePatternFile = () => pat;
                        dialog.BrowseCommand.Execute(null);

                        Assert.False(dialog.IsBuiltInFile);
                        Assert.Equal(pat, dialog.PatternFileText);
                        Assert.Equal("The 48 built-in patterns, then 2 from this file; a built-in name wins.", dialog.PatternFileHint);
                        Assert.True(dialog.BuiltInCommand.CanExecute(null));
                        Assert.Equal(new[] { "GRATING-30", "MINE" }, dialog.Patterns.Skip(48).Select(p => p.Name).ToArray());

                        // ANSI31 stays the built-in one: a built-in name wins.
                        Assert.Equal("ANSI31", dialog.Pattern.Name);
                        Assert.Null(dialog.Pattern.File);

                        dialog.Pattern = dialog.Patterns.Single(p => p.Name == "GRATING-30");
                        Assert.Equal("GRATING-30 · from steel-works.pat", dialog.PatternCaption);
                        Assert.Equal(pat, dialog.Settings.PatternFile);
                        return true;
                    };

                    model.Execute("H");
                    model.Execute("T");
                    model.Execute("2000,1500");
                    model.Execute(string.Empty);

                    DxfHatch hatch = model.Document.Database.ModelSpace.OfType<DxfHatch>().Single();
                    Assert.Equal("GRATING-30", hatch.PatternName);
                    Assert.Equal(DxfHatchPatternType.CustomDefined, hatch.PatternType);
                    Assert.Equal(2, hatch.NumberOfPatternDefinitions);
                    Assert.Equal(pat, model.HatchPatternFile);
                }
            });
        }

        [Fact]
        public void ThePatternFileIsRememberedForTheSession_AndBuiltInGoesBackToANSI31()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    string pat = folder.WriteText("steel-works.pat", Steel);
                    MainViewModel model = Room(folder);
                    model.ShowHatchSettings = dialog =>
                    {
                        dialog.BrowsePatternFile = () => pat;
                        dialog.Browse();
                        return true;
                    };
                    model.Execute("H");
                    model.Execute("T");
                    model.Execute(string.Empty);
                    model.Execute(string.Empty);

                    // The next dialog of the session starts on the file; the settings still name the built-in ANSI31.
                    HatchSettingsViewModel next = model.CreateHatchSettings();
                    Assert.Equal(pat, next.PatternFile);
                    Assert.Equal("ANSI31", next.Pattern.Name);
                    Assert.Equal(50, next.Patterns.Count);

                    next.Pattern = next.Patterns.Single(p => p.Name == "MINE");
                    next.BuiltInCommand.Execute(null);
                    Assert.True(next.IsBuiltInFile);
                    Assert.Equal(48, next.Patterns.Count);
                    Assert.Equal("ANSI31", next.Pattern.Name);
                    Assert.Null(next.Settings.PatternFile);

                    // A file that cannot be read is said, the built-in patterns still offered.
                    next.PatternFile = Path.Combine(folder.FullPath, "none.pat");
                    Assert.Equal(48, next.Patterns.Count);
                    Assert.StartsWith("Cannot read pattern file", next.Message);
                }
            });
        }

        [Fact]
        public void LoadLinetypesListsTheBuiltInSet_ThenAFileChosenByBrowse_AndLoadsBothInOneStep()
        {
            using (var folder = new TestFolder())
            {
                string lin = folder.WriteText("site.lin", "*DASHED,Not the built-in one\nA,1,-1\n*FENCE,Fence ---x---x---\nA,10,-2,0,-2\n");
                MainViewModel model = Room(folder);
                var load = new LoadLinetypesViewModel(model.Document);

                Assert.True(load.IsBuiltIn);
                Assert.Equal(LoadLinetypesViewModel.BuiltInText, load.FileText);
                Assert.Equal("Built-in (GeometryHelper.lin)", load.FileText);
                Assert.False(load.BuiltInCommand.CanExecute(null));
                Assert.Equal(39, load.Rows.Count);

                load.BrowseFile = () => lin;
                load.BrowseCommand.Execute(null);

                Assert.False(load.IsBuiltIn);
                Assert.Equal(lin, load.FileText);
                Assert.True(load.BuiltInCommand.CanExecute(null));
                Assert.Equal(40, load.Rows.Count);
                Assert.Equal("FENCE", load.Rows.Last().Name);
                Assert.Same(CadLinetypeFile.BuiltIn.Single(l => l.Name == "DASHED"), load.Rows.Single(r => r.Name == "DASHED").Definition);

                load.Rows.Single(r => r.Name == "DASHED").IsChosen = true;
                load.Rows.Single(r => r.Name == "FENCE").IsChosen = true;
                CadLinetypeLoad done = CadLinetypes.Load(model.Document, load.Chosen, load.FilePath);

                Assert.Equal(new[] { "DASHED", "FENCE" }, done.Loaded.ToArray());
                Assert.Equal("LINETYPE", model.Document.NextUndo.Name);
                Assert.Equal(7.5, model.Document.Database.LinetypeTable["DASHED"].PatternLength, 9);
                Assert.Equal(14.0, model.Document.Database.LinetypeTable["FENCE"].PatternLength, 9);

                load.BuiltInCommand.Execute(null);
                Assert.True(load.IsBuiltIn);
                Assert.Equal(39, load.Rows.Count);
            }
        }

        [Fact]
        public void TheLinetypeManagerLoadsABuiltInLinetypeFromItsDefinition_WhenAFileIsChosenBeside()
        {
            using (var folder = new TestFolder())
            {
                string lin = folder.WriteText("site.lin", "*FENCE,Fence ---x---x---\nA,10,-2,0,-2\n");
                MainViewModel model = Room(folder);
                LinetypeManagerViewModel manager = model.CreateLinetypeManager();
                manager.PickLinetypes = dialog =>
                {
                    dialog.BrowseFile = () => lin;
                    dialog.Browse();
                    dialog.Rows.Single(r => r.Name == "HIDDEN").IsChosen = true;
                    dialog.Rows.Single(r => r.Name == "FENCE").IsChosen = true;
                    return true;
                };

                manager.Load();
                Assert.True(manager.Apply().Succeeded);

                Assert.True(model.Document.Database.LinetypeTable.Has("HIDDEN"));
                Assert.True(model.Document.Database.LinetypeTable.Has("FENCE"));
                Assert.Equal(string.Empty, manager.Message);
            }
        }

        [Fact]
        public void MissingShxFilesIsShownOnceOnOpen_AndOkDrawsAFileInTheReplacementChosen_KeptThroughGlobalSettings()
        {
            using (var folder = new TestFolder())
            {
                var database = new DxfDatabase("AC1027");
                database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Dims", FileName = "romand.shx" });
                database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "TITLE", FileName = "romand" });
                database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "HZ", FileName = "ghsimplex.shx", BigFontFileName = "hztxt.shx" });
                string path = folder.Write("plan-level2.dxf", database);
                string mine = folder.WriteText("mine.shx", "not a font");
                var model = new MainViewModel { SettingsPath = Path.Combine(folder.FullPath, "settings.json") };
                int shown = 0;
                model.ShowMissingShx = dialog =>
                {
                    shown++;
                    Assert.Equal("Missing SHX files · plan-level2.dxf", dialog.Title);
                    Assert.True(dialog.Ignore);
                    MissingShxRowViewModel romand = dialog.Rows.Single(r => r.File == "romand.shx");
                    MissingShxRowViewModel big = dialog.Rows.Single(r => r.File == "hztxt.shx");
                    Assert.Equal("Dims, TITLE", romand.Styles);
                    Assert.False(romand.IsBigFont);
                    Assert.Equal("ghsimplex", romand.DrawnInText);
                    Assert.True(romand.IsDrawnByFontAlt);
                    Assert.True(big.IsBigFont);
                    Assert.Equal("HZ", big.Styles);
                    Assert.Equal("question marks", big.DrawnInText);
                    Assert.StartsWith("This drawing uses " + dialog.Rows.Count + " SHX font files that are not in the font search folders", dialog.Explanation);
                    Assert.Equal("Text stays in the FONTALT replacement, GeometryHelper Simplex; a big font's letters are drawn as question marks.", dialog.IgnoreDetail);

                    // The list: the heading, ghsimplex first, then the line and Browse…; ghsimplex chosen, as it draws the file now.
                    Assert.Equal(MissingShxOptionKind.Heading, romand.Options[0].Kind);
                    Assert.Equal("ghsimplex", romand.Options[1].Name);
                    Assert.Equal(MissingShxOptionKind.Browse, romand.Options.Last().Kind);
                    Assert.Same(romand.Options[1], romand.Replacement);

                    dialog.Replace = true;
                    dialog.BrowseFont = () => mine;
                    romand.Replacement = romand.Options.Last();
                    Assert.Equal("mine.shx", romand.Replacement.Name);
                    Assert.Equal(mine, romand.Replacement.Font);

                    // The heading is not taken.
                    romand.Replacement = romand.Options[0];
                    Assert.Equal("mine.shx", romand.Replacement.Name);
                    dialog.DoNotShowAgain = true;
                    return true;
                };

                Assert.True(model.Open(path));

                Assert.Equal(1, shown);
                Assert.Equal(mine, model.Document.Fonts.Substitutions["romand"]);
                Assert.False(model.Settings.ShowMissingShxDialog);
                Assert.False(GlobalSettings.Load(model.SettingsPath).ShowMissingShxDialog);

                // Global settings applied again keep the replacement the dialog chose for the drawing.
                model.ApplySettings(model.Settings);
                Assert.Equal(mine, model.Document.Fonts.Substitutions["romand"]);

                // Turned off, the dialog is not shown for the next drawing.
                Assert.True(model.Open(folder.Write("second.dxf", database)));
                Assert.Equal(1, shown);
            }
        }

        [Fact]
        public void CancelOfMissingShxFiles_KeepsTheFontAltReplacement_AndTheDialogOn()
        {
            using (var folder = new TestFolder())
            {
                var database = new DxfDatabase("AC1027");
                database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Dims", FileName = "romand.shx" });
                var model = new MainViewModel { SettingsPath = Path.Combine(folder.FullPath, "settings.json") };
                model.ShowMissingShx = dialog =>
                {
                    dialog.Replace = true;
                    dialog.DoNotShowAgain = true;
                    dialog.Rows.Single(r => r.File == "romand.shx").Replacement = dialog.Rows.Single(r => r.File == "romand.shx").Options[1];
                    return false;
                };

                Assert.True(model.Open(folder.Write("plan.dxf", database)));

                Assert.Empty(model.Document.Fonts.Substitutions);
                Assert.True(model.Settings.ShowMissingShxDialog);
                Assert.False(File.Exists(model.SettingsPath));
            }
        }

        [Fact]
        public void ShowMissingShxDialogIsSavedAndReadBack_OnByDefault()
        {
            using (var folder = new TestFolder())
            {
                Assert.True(new GlobalSettings().ShowMissingShxDialog);
                string path = Path.Combine(folder.FullPath, "settings.json");
                new GlobalSettings { ShowMissingShxDialog = false }.Save(path);

                Assert.Contains("\"showMissingShxDialog\": false", File.ReadAllText(path));
                Assert.False(GlobalSettings.Load(path).ShowMissingShxDialog);
            }
        }
    }
}
