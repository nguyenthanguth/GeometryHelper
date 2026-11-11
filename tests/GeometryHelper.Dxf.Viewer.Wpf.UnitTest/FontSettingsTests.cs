using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The font settings: their defaults, the JSON file, the Fonts page of Global settings, and the fonts given to the drawings open.</summary>
    public sealed class FontSettingsTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void TheFontsDefaultToNoFolderAndToTheBuiltInFontForAFontNotFound()
        {
            var settings = new GlobalSettings();

            Assert.Empty(settings.FontSearchFolders);
            Assert.DoesNotContain(settings.ToFonts().Folders, f =>
                f.IndexOf("Autodesk", StringComparison.OrdinalIgnoreCase) >= 0
            );
            Assert.Equal(CadFonts.BuiltInName, settings.ReplacementFont);
            Assert.Equal(string.Empty, settings.FontMapFile);
            Assert.Empty(settings.FontSubstitutions);
        }

        [Fact]
        public void ASavedListThatHoldsAutoCadsFontsFolderIsKeptAsTheUserSavedIt()
        {
            const string autoCad = @"C:\Program Files\Autodesk\AutoCAD 2021\Fonts";
            string path = Path.Combine(_folder.FullPath, "settings.json");
            new GlobalSettings { FontSearchFolders = new[] { autoCad }, ReplacementFont = "simplex.shx" }.Save(path);

            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(new[] { autoCad }, read.FontSearchFolders);
            Assert.Equal("simplex.shx", read.ReplacementFont);
        }

        [Fact]
        public void AFontMappingFileIsReadIntoTheTable_AndTheTableWinsForAFontBothName()
        {
            string map = Path.Combine(_folder.FullPath, "mine.fmp");
            File.WriteAllText(map, "romans.shx;isocp.shx\ntxt;simplex.shx\n");
            var settings = new GlobalSettings { FontMapFile = " " + map + " " };
            settings.SetFontSubstitutions(new[] { new KeyValuePair<string, string>("txt", "romans.shx") });
            string path = Path.Combine(_folder.FullPath, "settings.json");
            settings.Save(path);

            CadFonts fonts = GlobalSettings.Load(path).ToFonts();

            Assert.Equal(map, GlobalSettings.Load(path).FontMapFile);
            Assert.Equal(new[] { "romans.shx", "txt" }, fonts.Substitutions.Keys);
            Assert.Equal("isocp.shx", fonts.Substitutions["romans.shx"]);
            Assert.Equal("romans.shx", fonts.Substitutions["txt"]);
        }

        [Fact]
        public void TheFontSettingsWrittenReadBackTheSame_BlankFoldersLeftOutAndAFolderGivenTwiceKeptOnce()
        {
            var settings = new GlobalSettings
            {
                FontSearchFolders = new[] { @"D:\Fonts", " ", @"d:\fonts", @"E:\Shared ""fonts""" },
                ReplacementFont = " txt.shx ",
            };
            settings.SetFontSubstitutions(
                new[]
                {
                    new KeyValuePair<string, string>("romans.shx", "isocp.shx"),
                    new KeyValuePair<string, string>("Arial", "simplex"),
                    new KeyValuePair<string, string>(" ", "x"),
                }
            );
            string path = Path.Combine(_folder.FullPath, "settings.json");

            settings.Save(path);
            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(new[] { @"D:\Fonts", @"E:\Shared ""fonts""" }, read.FontSearchFolders);
            Assert.Equal("txt.shx", read.ReplacementFont);
            Assert.Equal(new[] { "Arial", "romans.shx" }, read.FontSubstitutions.Keys);
            Assert.Equal("isocp.shx", read.FontSubstitutions["ROMANS.SHX"]);
            Assert.Equal(settings.ToJson(), read.ToJson());

            // A file that empties the folders keeps them empty.
            settings.FontSearchFolders = Array.Empty<string>();
            settings.Save(path);
            Assert.Empty(GlobalSettings.Load(path).FontSearchFolders);
        }

        [Fact]
        public void TheFontsPageEditsTheFoldersTheReplacementAndTheTable_AndApplyTakesThem()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Fonts");

            Assert.True(dialog.IsFontsPage);
            Assert.Equal(model.Settings.FontSearchFolders, dialog.FontFolders.Select(r => r.Folder));
            dialog.AddFontFolderCommand.Execute(@"D:\OldFonts");
            dialog.AddFontFolderCommand.Execute(@"D:\MyFonts");
            dialog.AddFontFolderCommand.Execute(null);
            Assert.Equal(string.Empty, dialog.FontFolders.Last().Folder);
            dialog.RemoveFontFolderCommand.Execute(dialog.FontFolders[0]);
            dialog.AddFontSubstitutionCommand.Execute(null);
            dialog.FontSubstitutions[0].Font = "romans.shx";
            dialog.FontSubstitutions[0].DrawnIn = "isocp.shx";
            dialog.AddFontSubstitutionCommand.Execute(null);
            dialog.Row("Fonts", "Replacement font (FONTALT)").Value = "txt.shx";

            Assert.True(dialog.Apply());

            Assert.Equal(new[] { @"D:\MyFonts" }, model.Settings.FontSearchFolders);
            Assert.Equal("txt.shx", model.Settings.ReplacementFont);
            Assert.Equal("isocp.shx", Assert.Single(model.Settings.FontSubstitutions).Value);

            dialog.Reset();
            Assert.Empty(dialog.FontSubstitutions);
            Assert.Empty(dialog.FontFolders);
        }

        [Fact]
        public void ADrawingOpenIsGivenTheFontsAndAFontNotFoundIsSaidOnTheCommandLine()
        {
            var model = new MainViewModel();
            var settings = new GlobalSettings
            {
                FontSearchFolders = new[] { _folder.FullPath },
                ReplacementFont = "nofont.shx",
            };
            model.ApplySettings(settings);
            var database = new DxfDatabase("AC1018");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_ROMANS", FileName = "romans.shx" });
            var text = new DxfDBText
            {
                TextString = "A",
                Position = new GeoPoint3(0, 0, 0),
                Height = 5,
            };
            database.ModelSpace.AppendEntity(text);
            text.TextStyleName = "GH_ROMANS";
            CadDocument document = CadDocument.From(database);

            model.OpenDocument(document);
            document.GetDisplay(document.ModelSpace);

            Assert.Equal(new[] { _folder.FullPath }, document.Fonts.Folders);
            Assert.Equal("nofont.shx", document.Fonts.Replacement);
            Assert.Contains(
                "Missing SHX files: romans.shx drawn in the drawing font; txt drawn in the drawing font.",
                model.History
            );

            model.ApplySettings(
                new GlobalSettings { FontSearchFolders = Array.Empty<string>(), ReplacementFont = string.Empty }
            );
            Assert.True(document.Fonts.IsOff);
        }
    }
}
