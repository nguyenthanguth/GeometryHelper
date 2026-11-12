using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Global settings as the maintainer asked on 2026-11-12: the keys of a shortcut taken by pressing them, acad.pgp imported,
    /// a wheel zoom of 1.5, the model categories gone from the presenter's dialog, the UI font chosen from the installed
    /// families, and Folder preference holding the folders alone.
    /// </summary>
    public sealed class GlobalSettingsPagesTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void ANotchOfTheWheelZoomsOneAndAHalfTimesByDefault_AndAFileSavedWithOnePointTwoKeepsIt()
        {
            Assert.Equal(1.5, new GlobalSettings().WheelZoomFactor);
            Assert.Equal(
                "1.5",
                new MainViewModel().CreateGlobalSettings().Row("Canvas and cursor", "Wheel zoom factor").Value
            );

            string path = Path.Combine(_folder.FullPath, "settings.json");
            File.WriteAllText(path, "{ \"canvas\": { \"wheelZoomFactor\": 1.2 } }");
            Assert.Equal(1.2, GlobalSettings.Load(path).WheelZoomFactor);

            File.WriteAllText(path, "{ \"canvas\": { \"pickboxPixels\": 7 } }");
            Assert.Equal(1.5, GlobalSettings.Load(path).WheelZoomFactor);
        }

        [Fact]
        public void TheCategoriesAreEight_DrawingDefaultsDataSourcesAndDrawingTextGone()
        {
            GlobalSettingsViewModel dialog = new MainViewModel().CreateGlobalSettings("Text and fonts");

            Assert.Equal(
                new[]
                {
                    "Appearance",
                    "Canvas and cursor",
                    "Units and rounding",
                    "Layers, colours and lines",
                    "Text and fonts",
                    "Folder preference",
                    "Shortcuts",
                    "Save and export",
                },
                dialog.Categories.Select(c => c.Name)
            );
            Assert.Equal(
                new[] { "Interface", "Hidden messages", "Font not found", "Font mapping" },
                dialog.Sections.Select(s => s.Title)
            );
            Assert.Null(dialog.Row("Text and fonts", "Text height"));
            Assert.Null(dialog.Row("Text and fonts", "Width factor"));
            Assert.Null(dialog.Row("Drawing defaults", "Preset"));
            Assert.Null(dialog.Row("Data sources", "Check every"));
        }

        [Fact]
        public void AFileWithTheModelDrawingSettings_StillReadsThem_AndOkKeepsThemInTheFile()
        {
            string path = Path.Combine(_folder.FullPath, "settings.json");
            File.WriteAllText(
                path,
                "{ \"drawingDefaults\": { \"preset\": \"Part\", \"sheetSize\": \"A3\", \"orientation\": \"Portrait\" }, "
                    + "\"text\": { \"drawingFont\": \"Consolas\", \"textHeight\": 3.5, \"titleTextHeight\": 7, \"widthFactor\": 0.9 }, "
                    + "\"dataSources\": { \"checkForModelChanges\": false, \"checkEverySeconds\": 60 } }"
            );
            var model = new MainViewModel { SettingsPath = path };
            model.ApplySettings(GlobalSettings.Load(path));
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Text and fonts");
            dialog.Row("Text and fonts", "Replacement font (FONTALT)").Value = "txt.shx";

            Assert.True(dialog.Ok());

            GlobalSettings read = GlobalSettings.Load(path);
            Assert.Equal("Part", read.DefaultPreset);
            Assert.Equal("A3", read.DefaultSheetSize);
            Assert.Equal("Portrait", read.DefaultOrientation);
            Assert.Equal("Consolas", read.DrawingFont);
            Assert.Equal(3.5, read.TextHeight);
            Assert.Equal(7.0, read.TitleTextHeight);
            Assert.Equal(0.9, read.WidthFactor);
            Assert.False(read.CheckForModelChanges);
            Assert.Equal(60, read.CheckEverySeconds);
            Assert.Equal("txt.shx", read.ReplacementFont);
        }

        [Fact]
        public void FolderPreferenceHoldsTheFoldersAlone_TheFontNotFoundAndMappingAreOnTextAndFonts()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Folder preference");

            Assert.True(dialog.IsFolderPreferencePage);
            Assert.False(dialog.IsTextAndFontsPage);
            Assert.Empty(dialog.Sections);

            dialog.SelectedCategory = dialog.Categories.Single(c => c.Name == "Text and fonts");
            Assert.False(dialog.IsFolderPreferencePage);
            Assert.True(dialog.IsTextAndFontsPage);
            Assert.NotNull(dialog.Row("Text and fonts", "Replacement font (FONTALT)"));
            Assert.NotNull(dialog.Row("Text and fonts", "Font mapping file (FONTMAP)"));
            Assert.Equal("Arial", dialog.Row("Text and fonts", "Where it is not found either").Value);
        }

        [Fact]
        public void TheUiFontIsChosenFromTheInstalledFamilies_OrTyped()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Text and fonts");
            GlobalSettingRow row = dialog.Row("Text and fonts", "UI font");

            Assert.Equal(GlobalSettingKind.Font, row.Kind);
            Assert.True(row.IsFont);
            Assert.Equal("Inter", row.Value);
            Assert.Contains("Arial", row.Options);
            Assert.Equal(row.Options.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), row.Options);
            Assert.Equal(row.Options.Count, row.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            row.Value = "Consolas";
            Assert.True(dialog.Apply());
            Assert.Equal("Consolas", model.Settings.UiFont);
        }

        [Fact]
        public void TheTextAndFontsPageDrawsTheUiFontAsAnEditableBoxOfTheFamilies()
        {
            ComboBox font = null;
            int items = 0;
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var window = new GlobalSettingsWindow(new MainViewModel().CreateGlobalSettings("Text and fonts"));
                    FrameworkElement body = window.Body;
                    window.Content = null;
                    body.DataContext = window.Model;
                    body.Measure(new Size(840, 600));
                    body.Arrange(new Rect(0, 0, 840, 600));
                    body.UpdateLayout();
                    font = Descendants(body)
                        .OfType<ComboBox>()
                        .FirstOrDefault(c => c.Visibility == Visibility.Visible && c.IsEditable);
                    items = font?.Items.Count ?? 0;
                    window.Close();
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
            Assert.NotNull(font);
            Assert.True(items > 5, items + " families");
        }

        [Fact]
        public void AKeysCellTakesTheKeysPressed_BackspaceClears_EscGivesTheOldOnesBack()
        {
            GlobalSettingsViewModel dialog = new MainViewModel().CreateGlobalSettings("Shortcuts");
            ShortcutRowViewModel move = dialog.Shortcuts.Single(r => r.Command == "MOVE");
            ShortcutRowViewModel regen = dialog.Shortcuts.Single(r => r.Command == "REGEN");

            move.BeginKeyCapture();
            Assert.True(move.IsCapturingKeys);
            Assert.True(move.PressKey(Key.D, ModifierKeys.Control | ModifierKeys.Shift));
            Assert.Equal("Ctrl+Shift+D", move.Keys);

            // A modifier alone waits; a letter or Shift and a letter alone is typing, not a shortcut, and is not taken.
            Assert.True(move.PressKey(Key.LeftCtrl, ModifierKeys.Control));
            Assert.True(move.PressKey(Key.A, ModifierKeys.None));
            Assert.True(move.PressKey(Key.A, ModifierKeys.Shift));
            Assert.Equal("Ctrl+Shift+D", move.Keys);

            // Tab leaves the cell.
            Assert.False(move.PressKey(Key.Tab, ModifierKeys.None));

            Assert.True(move.PressKey(Key.D1, ModifierKeys.Control));
            Assert.Equal("Ctrl+1", move.Keys);
            Assert.Contains(dialog.Shortcuts, r => r.Conflict == "Ctrl+1 is already used by MOVE");
            Assert.True(dialog.HasErrors);

            Assert.True(move.PressKey(Key.Back, ModifierKeys.None));
            Assert.Equal(string.Empty, move.Keys);
            Assert.False(dialog.HasErrors);

            Assert.True(move.PressKey(Key.F5, ModifierKeys.None));
            Assert.Equal("F5", move.Keys);
            Assert.True(move.PressKey(Key.Delete, ModifierKeys.None));
            Assert.Equal(string.Empty, move.Keys);

            Assert.True(move.PressKey(Key.Escape, ModifierKeys.None));
            Assert.False(move.IsCapturingKeys);
            Assert.Equal(string.Empty, move.Keys);

            // Esc gives back the keys the cell had when it was entered.
            regen.Keys = "F6";
            regen.BeginKeyCapture();
            Assert.True(regen.PressKey(Key.S, ModifierKeys.Alt));
            Assert.Equal("Alt+S", regen.Keys);
            Assert.True(regen.PressKey(Key.Escape, ModifierKeys.None));
            Assert.Equal("F6", regen.Keys);

            // A cell not entered takes nothing.
            Assert.False(regen.PressKey(Key.F7, ModifierKeys.None));
            Assert.Equal("F6", regen.Keys);
        }

        [Fact]
        public void APgpFileIsReadAsAutoCadReadsItsAliases()
        {
            string[] lines =
            {
                ";  Program Parameters File For AutoCAD",
                "; External command format:",
                "NOTEPAD,   START NOTEPAD, 1,*File to edit: ,",
                "",
                "   ",
                "3A,        *3DARRAY",
                "a,         *arc     ; an alias written small",
                "-LA,       *-LAYER",
                "ZZ , * ZOOM,",
                "no comma here",
                "X Y,       *XLINE",
                "CO,        COPY",
                ",          *LINE",
                "ZZ,        *PAN",
            };

            List<KeyValuePair<string, string>> aliases = GlobalSettingsViewModel.ReadPgp(lines, out int unread);

            Assert.Equal(
                new[]
                {
                    new KeyValuePair<string, string>("3A", "3DARRAY"),
                    new KeyValuePair<string, string>("A", "ARC"),
                    new KeyValuePair<string, string>("-LA", "-LAYER"),
                    new KeyValuePair<string, string>("ZZ", "ZOOM"),
                    new KeyValuePair<string, string>("ZZ", "PAN"),
                },
                aliases
            );
            Assert.Equal(4, unread);
        }

        [Fact]
        public void ImportAcadPgpMergesItsAliases_TheFilesWin_AndSaysHowMany()
        {
            string path = Path.Combine(_folder.FullPath, "acad.pgp");
            File.WriteAllText(
                path,
                "; my aliases\n\nZZ, *ZOOM\nL, *PLINE\nC, *CIRCLE\nQQ, *NOSUCHCOMMAND\nodd line\n-LA, *-LAYER\n"
            );
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Shortcuts");
            string zoom = dialog.Shortcuts.Single(r => r.Command == "ZOOM").Aliases;

            dialog.BrowsePgpFile = () => null;
            dialog.ImportPgpCommand.Execute(null);
            Assert.Equal(zoom, dialog.Shortcuts.Single(r => r.Command == "ZOOM").Aliases);

            dialog.BrowsePgpFile = () => path;
            dialog.ImportPgpCommand.Execute(null);

            Assert.Equal(
                "acad.pgp: 1 alias added, 1 changed, 2 unchanged; 1 skipped, its command not in this app; 1 line not read. Apply or OK takes them.",
                dialog.Notice
            );
            Assert.Contains("ZZ", dialog.Shortcuts.Single(r => r.Command == "ZOOM").AliasList);
            Assert.Contains("L", dialog.Shortcuts.Single(r => r.Command == "PLINE").AliasList);
            Assert.DoesNotContain("L", dialog.Shortcuts.Single(r => r.Command == "LINE").AliasList);
            Assert.DoesNotContain(dialog.Shortcuts, r => r.Command == "NOSUCHCOMMAND");
            Assert.False(dialog.HasErrors);

            Assert.True(dialog.Apply());
            Assert.Equal("PLINE", model.Settings.Aliases["L"]);
            Assert.Equal("ZOOM", model.Settings.Aliases["ZZ"]);
        }

        [Fact]
        public void APgpFileThatDoesNotReadIsSaid_AndNothingChanges()
        {
            GlobalSettingsViewModel dialog = new MainViewModel().CreateGlobalSettings("Shortcuts");

            Assert.False(dialog.ImportPgp(Path.Combine(_folder.FullPath, "none.pgp")));

            Assert.StartsWith("none.pgp was not read: ", dialog.Notice, StringComparison.Ordinal);
            Assert.Equal("LINE", dialog.Shortcuts.Single(r => r.AliasList.Contains("L")).Command);
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }
    }
}
