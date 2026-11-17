using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Missing Fonts dialog: the SHX files not found as before, and below them, read only, the TrueType families the
    /// drawing's styles and MTEXTs name that the machine has not, each with what it is drawn in; opened by TrueType fonts alone
    /// too; and its switch in the Text and fonts page of Global settings. Each is driven with no window.
    /// </summary>
    public sealed class MissingFontsDialogTests
    {
        private const string NoSuchFamily = "GhNoSuchFontFamily";
        private const string SwitchLabel = "Show the missing fonts dialog on opening a drawing";

        private static DxfDatabase Drawing(bool shx, params string[] families)
        {
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable["Standard"].FileName = CadFonts.BuiltInName + ".shx";

            if (shx)
            {
                database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Dims", FileName = "romand.shx" });
            }

            foreach (string family in families)
            {
                database.TextStyleTable.Add(
                    new DxfTextStyleTableRecord
                    {
                        Name = "S-" + family,
                        Font = new DxfFontDescriptor(family, false, false, 0, 34),
                    }
                );
            }

            return database;
        }

        private static MainViewModel Model(TestFolder folder) =>
            new MainViewModel { SettingsPath = Path.Combine(folder.FullPath, "settings.json") };

        [Fact]
        public void ADrawingWithAMissingShxFileAndAMissingFamily_ShowsBothLists_TitledMissingFonts()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Model(folder);
                int shown = 0;
                model.ShowMissingShx = dialog =>
                {
                    shown++;
                    Assert.Equal("Missing Fonts · plan.dxf", dialog.Title);
                    Assert.True(dialog.HasShx);
                    Assert.True(dialog.HasTrueType);
                    Assert.Equal("romand.shx", Assert.Single(dialog.Rows).File);
                    MissingTrueTypeRowViewModel row = Assert.Single(dialog.TrueTypeRows);
                    Assert.Equal(NoSuchFamily, row.Family);
                    Assert.Equal("Arial, letters it lacks from another font", row.DrawnIn);
                    Assert.Equal(
                        "This drawing uses 1 TrueType font that is not installed on this machine. Its text is drawn in the replacement shown.",
                        dialog.TrueTypeExplanation
                    );
                    return true;
                };

                Assert.True(model.Open(folder.Write("plan.dxf", Drawing(true, NoSuchFamily))));

                Assert.Equal(1, shown);
            }
        }

        [Fact]
        public void ADrawingWithOnlyAMissingFamily_OpensTheDialog_WithTheTrueTypeListAlone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Model(folder);
                int shown = 0;
                model.ShowMissingShx = dialog =>
                {
                    shown++;
                    Assert.Equal("Missing Fonts · plan.dxf", dialog.Title);
                    Assert.False(dialog.HasShx);
                    Assert.Empty(dialog.Rows);
                    Assert.Equal(NoSuchFamily, Assert.Single(dialog.TrueTypeRows).Family);

                    // OK changes nothing for a font that has no replacement to choose.
                    Assert.Empty(dialog.Apply());
                    return true;
                };

                Assert.True(model.Open(folder.Write("plan.dxf", Drawing(false, NoSuchFamily, "Arial"))));

                Assert.Equal(1, shown);
                Assert.Empty(model.Document.Fonts.Substitutions);
            }
        }

        [Fact]
        public void ADrawingWithOnlyAMissingShxFile_KeepsTheTitleAndHasNoTrueTypeList()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Model(folder);
                int shown = 0;
                model.ShowMissingShx = dialog =>
                {
                    shown++;
                    Assert.Equal("Missing SHX files · plan.dxf", dialog.Title);
                    Assert.True(dialog.HasShx);
                    Assert.False(dialog.HasTrueType);
                    Assert.Empty(dialog.TrueTypeRows);
                    return false;
                };

                Assert.True(model.Open(folder.Write("plan.dxf", Drawing(true))));

                Assert.Equal(1, shown);
            }
        }

        [Fact]
        public void InstalledFamilies_AndLocalizedNamesThatResolve_AreNotListed_AndNoDialogOpens()
        {
            // Arial is on every Windows machine; its Korean neighbour is asked by the name Windows lists it by, 맑은 고딕.
            string korean = SkiaCanvasRenderer.HasFamily("Malgun Gothic") ? "맑은 고딕" : "Arial";

            using (var folder = new TestFolder())
            {
                MainViewModel model = Model(folder);
                int shown = 0;
                model.ShowMissingShx = dialog =>
                {
                    shown++;
                    return true;
                };

                Assert.True(model.Open(folder.Write("plan.dxf", Drawing(false, "Arial", korean))));

                Assert.Equal(0, shown);
                Assert.Empty(model.Document.FindMissingTrueTypeFonts());
            }
        }

        [Fact]
        public void TheDialogIsNotShown_ForAMissingFamily_WhenGlobalSettingsTurnedItOff()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Model(folder);
                model.Settings.ShowMissingShxDialog = false;
                int shown = 0;
                model.ShowMissingShx = dialog =>
                {
                    shown++;
                    return true;
                };

                Assert.True(model.Open(folder.Write("plan.dxf", Drawing(false, NoSuchFamily))));

                Assert.Equal(0, shown);
            }
        }

        [Fact]
        public void DoNotShowAgain_OnTheTrueTypeListAlone_TurnsTheSettingOff_AndSavesIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Model(folder);
                model.ShowMissingShx = dialog =>
                {
                    dialog.DoNotShowAgain = true;
                    return true;
                };

                Assert.True(model.Open(folder.Write("plan.dxf", Drawing(false, NoSuchFamily))));

                Assert.False(model.Settings.ShowMissingShxDialog);
                Assert.False(GlobalSettings.Load(model.SettingsPath).ShowMissingShxDialog);
            }
        }

        [Fact]
        public void TheSwitchInTheFontsSectionOfGlobalSettings_TurnsTheDialogBackOn_AndIsSavedUnderTheOldKey()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Model(folder);
                model.Settings.ShowMissingShxDialog = false;

                GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Text and fonts");
                GlobalSettingRow row = dialog.Row("Text and fonts", SwitchLabel);

                Assert.Equal(GlobalSettingKind.Toggle, row.Kind);
                Assert.False(row.IsOn);
                Assert.Contains(dialog.Sections, s => s.Title == "Font not found" && s.Rows.Contains(row));

                row.IsOn = true;
                Assert.True(dialog.Ok());

                Assert.True(model.Settings.ShowMissingShxDialog);
                string saved = File.ReadAllText(model.SettingsPath);
                Assert.Contains("\"showMissingShxDialog\": true", saved);
                Assert.True(GlobalSettings.Load(model.SettingsPath).ShowMissingShxDialog);

                // The dialog opened next shows it on, and off again is saved as false.
                GlobalSettingsViewModel next = model.CreateGlobalSettings("Text and fonts");
                GlobalSettingRow again = next.Row("Text and fonts", SwitchLabel);
                Assert.True(again.IsOn);
                again.IsOn = false;
                Assert.True(next.Ok());
                Assert.False(GlobalSettings.Load(model.SettingsPath).ShowMissingShxDialog);
            }
        }

        [Fact]
        public void TheDialogWindowIsLaidOutOnAStaThread_WithBothListsAndWithTheTrueTypeListAlone()
        {
            Exception failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    foreach (bool shx in new[] { true, false })
                    {
                        CadDocument document = CadDocument.From(Drawing(shx, NoSuchFamily), "plan.dxf");
                        document.Fonts = new GlobalSettings().ToFonts(f => f == "Arial");
                        var window = new MissingShxWindow(
                            new MissingShxViewModel(
                                document,
                                document.FindMissingFonts(),
                                document.FindMissingTrueTypeFonts(),
                                "Arial"
                            )
                        );
                        window.Body.Measure(new System.Windows.Size(window.Width, double.PositiveInfinity));

                        Assert.Equal("Missing Fonts · plan.dxf", window.Title);
                        Assert.True(window.Body.DesiredSize.Height > 100.0);
                        Assert.Equal(shx, window.Model.HasShx);
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}
