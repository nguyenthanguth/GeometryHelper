using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// APPLOAD's Look in as AutoCAD 2021's Load/Unload Applications has it: the place list (Desktop, the user's folders, This
    /// PC, the drives, the chain of the folder shown), This PC, a name typed in File name, the keys, the Size column, what is
    /// remembered and the files dropped on Loaded Applications. The places come from a fake source, so that no test depends on
    /// the drives of the machine.
    /// </summary>
    public sealed partial class AppLoadTests
    {
        /// <summary>Places of a made-up machine: Desktop, a user folder with Documents and Downloads, and three drives.</summary>
        private sealed class FakePlaces : IAppLoadPlaceSource
        {
            public string Desktop { get; set; }

            public string UserFolder { get; set; }

            public string Documents { get; set; }

            public string Downloads { get; set; }

            public List<AppLoadDrive> DriveList { get; } = new List<AppLoadDrive>();

            public IReadOnlyList<AppLoadDrive> Drives() => DriveList;
        }

        private FakePlaces Places()
        {
            string home = Path.Combine(_folder.FullPath, "User");
            var places = new FakePlaces
            {
                Desktop = Directory.CreateDirectory(Path.Combine(_folder.FullPath, "Desktop")).FullName,
                UserFolder = Directory.CreateDirectory(home).FullName,
                Documents = Directory.CreateDirectory(Path.Combine(home, "Documents")).FullName,
                Downloads = Directory.CreateDirectory(Path.Combine(home, "Downloads")).FullName,
            };
            places.DriveList.Add(new AppLoadDrive(Path.GetPathRoot(_folder.FullPath), "System", DriveType.Fixed));
            places.DriveList.Add(new AppLoadDrive(@"Q:\", string.Empty, DriveType.CDRom));
            places.DriveList.Add(new AppLoadDrive(@"R:\", string.Empty, DriveType.Network));
            return places;
        }

        private AppLoadViewModel Dialog(string folder = null, FakePlaces places = null) =>
            new AppLoadViewModel(Opened(s => s.AppLoadFolder = folder ?? Browsed), places ?? Places());

        private static List<string[]> Boxes(AppLoadViewModel dialog)
        {
            var boxes = new List<string[]>();
            dialog.ShowMessage = (title, message, detail) => boxes.Add(new[] { title, message, detail });
            return boxes;
        }

        // ---- Look in: the places

        [Fact]
        public void ThePlaces_AreDesktopTheUserFoldersThisPcAndTheDrives_WithTheChainOfTheFolderShownUnderItsDrive()
        {
            AppLoadViewModel dialog = Dialog();
            string root = Path.GetPathRoot(Browsed);
            string[] chain = Browsed
                .Substring(root.Length)
                .Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            IReadOnlyList<AppLoadPlace> rows = dialog.Places;

            Assert.Equal(
                new[] { "Desktop", "User", "Documents", "Downloads", "This PC" },
                rows.Take(5).Select(r => r.Name)
            );
            Assert.Equal(new[] { 0, 1, 1, 1, 1 }, rows.Take(5).Select(r => r.Level));
            Assert.Equal(
                new[]
                {
                    AppLoadPlaceKind.Desktop,
                    AppLoadPlaceKind.UserFolder,
                    AppLoadPlaceKind.UserFolder,
                    AppLoadPlaceKind.UserFolder,
                    AppLoadPlaceKind.ThisPc,
                },
                rows.Take(5).Select(r => r.Kind)
            );
            Assert.Null(rows[4].Path);
            Assert.Equal(dialog.Place.Name, rows.Single(r => r.IsCurrent).Name);

            List<AppLoadPlace> drives = rows.Skip(5).Where(r => r.Kind == AppLoadPlaceKind.Drive).ToList();
            Assert.Equal(3, drives.Count);
            Assert.All(drives, d => Assert.Equal(2, d.Level));
            Assert.Equal(
                drives.Select(d => d.Path).OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
                drives.Select(d => d.Path)
            );
            Assert.Contains("System (" + root.Substring(0, 2) + ")", drives.Select(d => d.Name));
            Assert.Contains("DVD Drive (Q:)", drives.Select(d => d.Name));
            Assert.Contains("Network Drive (R:)", drives.Select(d => d.Name));

            int own = rows.ToList().FindIndex(r => r.Kind == AppLoadPlaceKind.Drive && r.Path == root);
            List<AppLoadPlace> below = rows.Skip(own + 1).Take(chain.Length).ToList();
            Assert.Equal(chain, below.Select(r => r.Name));
            Assert.Equal(Enumerable.Range(3, chain.Length), below.Select(r => r.Level));
            Assert.All(below, r => Assert.Equal(AppLoadPlaceKind.Folder, r.Kind));
            Assert.Equal(Browsed, below.Last().Path);
            Assert.True(below.Last().IsCurrent);
            Assert.Single(rows, r => r.IsCurrent);
        }

        [Fact]
        public void AFolderInsideDocuments_HangsUnderDocuments_AndOneInsideTheUserFolder_UnderTheUserFolder()
        {
            FakePlaces places = Places();
            string lisp = Directory.CreateDirectory(Path.Combine(places.Documents, "Lisp")).FullName;
            string roaming = Directory
                .CreateDirectory(Path.Combine(places.UserFolder, "AppData", "Roaming", "X"))
                .FullName;

            IReadOnlyList<AppLoadPlace> underDocuments = Dialog(lisp, places).Places;
            IReadOnlyList<AppLoadPlace> underUser = Dialog(roaming, places).Places;

            Assert.Equal(
                new[] { "Desktop", "User", "Documents", "Lisp", "Downloads", "This PC" },
                underDocuments.Take(6).Select(r => r.Name)
            );
            Assert.Equal(new[] { 0, 1, 1, 2, 1, 1 }, underDocuments.Take(6).Select(r => r.Level));
            Assert.True(underDocuments[3].IsCurrent);
            Assert.Equal(lisp, underDocuments[3].Path);
            Assert.DoesNotContain(underDocuments, r => r.Kind == AppLoadPlaceKind.Folder && r.Level >= 3);

            Assert.Equal(
                new[] { "Desktop", "User", "AppData", "Roaming", "X", "Documents", "Downloads", "This PC" },
                underUser.Take(8).Select(r => r.Name)
            );
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 1, 1, 1 }, underUser.Take(8).Select(r => r.Level));
            Assert.Equal(roaming, underUser.Single(r => r.IsCurrent).Path);
        }

        [Fact]
        public void ADriveRootShown_IsItsOwnRowChosen_WithNothingUnderIt_AndTheUserFoldersThatAreNotThereAreLeftOut()
        {
            FakePlaces places = Places();
            string root = Path.GetPathRoot(Browsed);
            places.Downloads = null;
            places.UserFolder = null;
            places.Documents = null;

            AppLoadViewModel dialog = Dialog(root, places);

            AppLoadPlace chosen = Assert.Single(dialog.Places, r => r.IsCurrent);
            Assert.Equal(AppLoadPlaceKind.Drive, chosen.Kind);
            Assert.Equal(root, chosen.Path);
            Assert.Equal(2, chosen.Level);
            Assert.Equal(new[] { "Desktop", "This PC" }, dialog.Places.Take(2).Select(r => r.Name));
            Assert.DoesNotContain(dialog.Places, r => r.Kind == AppLoadPlaceKind.Folder);
            Assert.Same(chosen, dialog.Place);
        }

        [Fact]
        public void TheDriveNames_AreLabelThenLetter_OrTheWordsOfItsKind()
        {
            Assert.Equal("Data (D:)", new AppLoadDrive(@"D:\", "Data", DriveType.Fixed).Name);
            Assert.Equal("Local Disk (C:)", new AppLoadDrive(@"C:\", "", DriveType.Fixed).Name);
            Assert.Equal("DVD Drive (E:)", new AppLoadDrive(@"E:\", "", DriveType.CDRom).Name);
            Assert.Equal("Network Drive (S:)", new AppLoadDrive(@"S:\", "", DriveType.Network).Name);
            Assert.Equal("Local Disk", new AppLoadDrive(@"C:\", "", DriveType.Fixed).TypeName);
            Assert.Equal("CD Drive", new AppLoadDrive(@"E:\", "", DriveType.CDRom).TypeName);
            Assert.Equal("Network Drive", new AppLoadDrive(@"S:\", "", DriveType.Network).TypeName);
        }

        [Fact]
        public void ChoosingAPlace_OpensIt_BackRecordsTheFolderLeft_AndChoosingTheDriveShownOpensNothing()
        {
            FakePlaces places = Places();
            AppLoadViewModel dialog = Dialog(Browsed, places);
            AppLoadPlace documents = dialog.Places.Single(r => r.Name == "Documents");

            dialog.Place = documents;

            Assert.Equal(places.Documents, dialog.Folder);
            Assert.Same(dialog.Places.Single(r => r.IsCurrent), dialog.Place);
            Assert.Equal("Documents", dialog.Place.Name);
            Assert.True(dialog.CanBack);

            string root = Path.GetPathRoot(Browsed);
            dialog.Place = dialog.Places.Single(r => r.Kind == AppLoadPlaceKind.Drive && r.Path == root);

            Assert.Equal(root, dialog.Folder);
            Assert.Equal(AppLoadPlaceKind.Drive, dialog.Place.Kind);

            dialog.Back();
            Assert.Equal(places.Documents, dialog.Folder);

            dialog.Place = null;
            Assert.Equal(places.Documents, dialog.Folder);
        }

        // ---- This PC

        [Fact]
        public void ThisPc_ListsTheDrivesByTheirKind_WithNoDateAndNoSize_UpIsGreyed_LoadWaitsForAText()
        {
            AppLoadViewModel dialog = Dialog();

            dialog.Place = dialog.Places.Single(r => r.Kind == AppLoadPlaceKind.ThisPc);

            Assert.True(dialog.IsThisPc);
            Assert.Null(dialog.Folder);
            Assert.Equal(AppLoadPlaceKind.ThisPc, dialog.Place.Kind);
            Assert.Equal("This PC", dialog.Place.Name);
            Assert.False(dialog.CanUp);
            Assert.True(dialog.CanBack);
            Assert.All(dialog.Entries, e => Assert.True(e.IsFolder));
            Assert.All(dialog.Entries, e => Assert.Equal(string.Empty, e.Modified));
            Assert.All(dialog.Entries, e => Assert.Equal(string.Empty, e.Size));
            Assert.Equal(
                dialog.Places.Where(r => r.Kind == AppLoadPlaceKind.Drive).Select(r => r.Name),
                dialog.Entries.Select(e => e.Name)
            );
            Assert.Equal(
                new[] { "Local Disk", "CD Drive", "Network Drive" }.OrderBy(t => t, StringComparer.Ordinal),
                dialog.Entries.Select(e => e.Type).OrderBy(t => t, StringComparer.Ordinal)
            );
            Assert.False(dialog.CanLoad);

            dialog.Up();
            Assert.True(dialog.IsThisPc);

            dialog.FileName = "tools.lsp";
            Assert.True(dialog.CanLoad);
        }

        [Fact]
        public void UpFromADriveRoot_GoesToThisPc_AndBackWorksAcrossIt()
        {
            string root = Path.GetPathRoot(Browsed);
            AppLoadViewModel dialog = Dialog(root);
            Assert.True(dialog.CanUp);

            dialog.Up();

            Assert.True(dialog.IsThisPc);
            Assert.Equal("This PC", dialog.Place.Name);

            dialog.Activate(dialog.Entries.Single(e => e.Path == root));
            Assert.Equal(root, dialog.Folder);
            Assert.False(dialog.IsThisPc);

            dialog.Back();
            Assert.True(dialog.IsThisPc);

            dialog.Back();
            Assert.Equal(root, dialog.Folder);
        }

        // ---- File name: a folder, a wildcard, a name that is not there

        [Fact]
        public void EnterWithAFolder_FullRelativeOrUp_OpensItEmptiesTheBoxAndLoadsNothing()
        {
            string sub = Directory.CreateDirectory(Path.Combine(Browsed, "sub")).FullName;
            string deeper = Directory.CreateDirectory(Path.Combine(sub, "deeper")).FullName;
            Lisp("tools.lsp", "(setq ghtools 1)");
            MainViewModel model = Opened();
            var dialog = new AppLoadViewModel(model, Places());
            var boxes = Boxes(dialog);

            dialog.Enter(sub);
            Assert.Equal(sub, dialog.Folder);
            Assert.Equal(string.Empty, dialog.FileName);

            dialog.Enter("deeper");
            Assert.Equal(deeper, dialog.Folder);
            Assert.Equal(string.Empty, dialog.FileName);

            dialog.Enter("..");
            Assert.Equal(sub, dialog.Folder);

            dialog.Enter(@"..\sub\deeper\");
            Assert.Equal(deeper, dialog.Folder);
            Assert.Equal(string.Empty, dialog.FileName);
            Assert.True(dialog.CanBack);

            Assert.Empty(dialog.LoadedRows);
            Assert.Empty(dialog.HistoryRows);
            Assert.Equal(string.Empty, dialog.Status);
            Assert.Empty(boxes);
            Assert.Null(model.Session.Lisp.GetValue("GHTOOLS"));
        }

        [Fact]
        public void TheLoadButtonWithAFolderTyped_OpensItToo()
        {
            string sub = Directory.CreateDirectory(Path.Combine(Browsed, "sub")).FullName;
            AppLoadViewModel dialog = Dialog();
            dialog.FileName = "sub";

            dialog.Load();

            Assert.Equal(sub, dialog.Folder);
            Assert.Equal(string.Empty, dialog.FileName);
            Assert.Empty(dialog.HistoryRows);
        }

        [Fact]
        public void EnterWithAWildcard_FiltersTheListKeepingTheFolders_TheTextStays_AndTheTypeChangedOrTheFolderEndsIt()
        {
            string sub = Directory.CreateDirectory(Path.Combine(Browsed, "sub")).FullName;
            Lisp("a.txt", "x");
            Lisp("b.lsp", "(setq ghb 1)");
            Lisp("c.dll", "x");
            AppLoadViewModel dialog = Dialog();
            Assert.Equal(new[] { "sub", "b.lsp" }, dialog.Entries.Select(e => e.Name));

            dialog.Enter("*.TXT");

            Assert.Equal(new[] { "sub", "a.txt" }, dialog.Entries.Select(e => e.Name));
            Assert.Equal("*.TXT", dialog.FileName);
            Assert.Same(dialog.FileTypes[0], dialog.FileType);
            Assert.Empty(dialog.LoadedRows);

            dialog.Enter("c?.dll");
            Assert.Equal(new[] { "sub" }, dialog.Entries.Select(e => e.Name));

            dialog.Enter("?.dll");
            Assert.Equal(new[] { "sub", "c.dll" }, dialog.Entries.Select(e => e.Name));

            dialog.FileType = dialog.FileTypes[1];
            Assert.Equal(new[] { "sub", "c.dll" }, dialog.Entries.Select(e => e.Name));
            dialog.FileType = dialog.FileTypes[0];
            Assert.Equal(new[] { "sub", "b.lsp" }, dialog.Entries.Select(e => e.Name));

            dialog.Enter("a*");
            Assert.Equal(new[] { "sub", "a.txt" }, dialog.Entries.Select(e => e.Name));
            dialog.OpenFolder(sub);
            dialog.Back();
            Assert.Equal(new[] { "sub", "b.lsp" }, dialog.Entries.Select(e => e.Name));
        }

        [Fact]
        public void AWildcardThatIsAFilesOfTypePattern_ChoosesThatEntry_CaseIgnored_AndTheTextStays()
        {
            Lisp("a.txt", "x");
            Lisp("b.lsp", "x");
            Lisp("c.dll", "x");
            AppLoadViewModel dialog = Dialog();

            dialog.Enter("*.dll");
            Assert.Same(dialog.FileTypes[1], dialog.FileType);
            Assert.Equal(new[] { "c.dll" }, dialog.Entries.Select(e => e.Name));
            Assert.Equal("*.dll", dialog.FileName);

            dialog.Enter("*.*");
            Assert.Same(dialog.FileTypes[2], dialog.FileType);
            Assert.Equal(new[] { "a.txt", "b.lsp", "c.dll" }, dialog.Entries.Select(e => e.Name));

            dialog.Enter("*.LSP");
            Assert.Same(dialog.FileTypes[0], dialog.FileType);
            Assert.Equal(new[] { "b.lsp" }, dialog.Entries.Select(e => e.Name));
            Assert.Equal("*.LSP", dialog.FileName);
        }

        [Fact]
        public void AFolderThatIsNotThere_ShowsTheErrorBox_AndAPathEndingInABackslashSaysThePathDoesNotExist()
        {
            Lisp("b.lsp", "x");
            AppLoadViewModel dialog = Dialog();
            var boxes = Boxes(dialog);
            string[] before = dialog.Entries.Select(e => e.Name).ToArray();

            dialog.Enter(@"D:\Nowhere");

            string[] error = Assert.Single(boxes);
            Assert.Equal(new[] { "Error", "The following file was not found:", @"D:\Nowhere" }, error);
            Assert.Equal(@"D:\Nowhere", dialog.FileName);
            Assert.Equal(before, dialog.Entries.Select(e => e.Name));
            Assert.Equal(Browsed, dialog.Folder);
            Assert.Empty(dialog.LoadedRows);
            Assert.Empty(dialog.HistoryRows);

            boxes.Clear();
            dialog.Enter(@"D:\Nowhere\");

            string[] path = Assert.Single(boxes);
            Assert.Equal(
                new[]
                {
                    "Load/Unload Applications",
                    "Path does not exist. Check the path and try again.",
                    @"D:\Nowhere\",
                },
                path
            );
            Assert.Equal(@"D:\Nowhere\", dialog.FileName);
            Assert.Equal(Browsed, dialog.Folder);
        }

        [Fact]
        public void AMissingFileInTheFolderShown_ShowsTheErrorBoxWithItsFullPath_AndNothingIsLoaded()
        {
            Lisp("b.lsp", "(setq ghb 1)");
            AppLoadViewModel dialog = Dialog();
            var boxes = Boxes(dialog);

            dialog.Enter("nothere.lsp");

            Assert.Equal(
                new[] { "Error", "The following file was not found:", Path.Combine(Browsed, "nothere.lsp") },
                Assert.Single(boxes)
            );
            Assert.Equal("nothere.lsp", dialog.FileName);
            Assert.Empty(dialog.LoadedRows);
            Assert.Equal(string.Empty, dialog.Status);
        }

        [Fact]
        public void SeveralQuotedNames_WithOneMissing_ShowTheBoxForTheFirstMissingOneAndLoadNothing()
        {
            Lisp("a.lsp", "(setq gha 1)");
            Lisp("c.lsp", "(setq ghc 1)");
            AppLoadViewModel dialog = Dialog();
            var boxes = Boxes(dialog);

            dialog.Enter("\"a.lsp\" \"gone1.lsp\" \"c.lsp\" \"gone2.lsp\"");

            string[] box = Assert.Single(boxes);
            Assert.Equal(Path.Combine(Browsed, "gone1.lsp"), box[2]);
            Assert.Empty(dialog.LoadedRows);
            Assert.Empty(dialog.HistoryRows);
        }

        [Fact]
        public void AWindowWithNoMessageBox_SaysTheMissingNameInAmberInTheFooter()
        {
            AppLoadViewModel dialog = Dialog();

            dialog.Enter("nothere.lsp");

            Assert.Equal(AppLoadStatusKind.Refused, dialog.StatusKind);
            Assert.Contains("The following file was not found:", dialog.Status);
            Assert.Contains("nothere.lsp", dialog.Status);
        }

        [Fact]
        public void ATypedPathAtThisPc_OpensAFolder_AndARelativeNameThereIsNotFound()
        {
            AppLoadViewModel dialog = Dialog();
            var boxes = Boxes(dialog);
            dialog.Place = dialog.Places.Single(r => r.Kind == AppLoadPlaceKind.ThisPc);

            dialog.Enter("tools.lsp");
            Assert.Single(boxes);
            Assert.True(dialog.IsThisPc);

            dialog.Enter(Browsed);
            Assert.Equal(Browsed, dialog.Folder);
        }

        // ---- What is remembered

        [Fact]
        public void TheFolderIsKeptOnlyByALoadFromIt_AMovingAboutNeverWritesIt()
        {
            string sub = Directory.CreateDirectory(Path.Combine(Browsed, "sub")).FullName;
            File.WriteAllText(Path.Combine(sub, "inside.lsp"), "(setq ghinside 1)");
            MainViewModel model = Opened();
            var dialog = new AppLoadViewModel(model, Places());

            dialog.OpenFolder(sub);
            dialog.Up();
            dialog.Place = dialog.Places.Single(r => r.Kind == AppLoadPlaceKind.ThisPc);

            Assert.Equal(Browsed, model.Settings.AppLoadFolder);
            Assert.Equal(string.Empty, Saved().AppLoadFolder);

            dialog.Enter(sub);
            dialog.Enter("inside.lsp");

            Assert.Equal(sub, model.Settings.AppLoadFolder);
            Assert.Equal(sub, Saved().AppLoadFolder);
        }

        [Fact]
        public void ALoadFromThisPcWithAFullPath_LeavesTheFolderKept()
        {
            string tools = Lisp("tools.lsp", "(setq ghtools 1)");
            MainViewModel model = Opened();
            var dialog = new AppLoadViewModel(model, Places());
            dialog.Place = dialog.Places.Single(r => r.Kind == AppLoadPlaceKind.ThisPc);

            dialog.Enter(tools);

            Assert.Equal("tools.lsp successfully loaded.", dialog.Status);
            Assert.Equal(Browsed, model.Settings.AppLoadFolder);
        }

        [Fact]
        public void FilesOfType_IsKeptTheMomentItChanges_AndTheDialogOpensedAgainShowsIt()
        {
            MainViewModel model = Opened();
            var first = new AppLoadViewModel(model, Places());
            Assert.Same(first.FileTypes[0], first.FileType);
            Assert.Equal("lsp", model.Settings.AppLoadFileType);

            first.FileType = first.FileTypes[1];
            Assert.Equal("dll", Saved().AppLoadFileType);
            Assert.Equal(first.FileTypes[1].Label, new AppLoadViewModel(model, Places()).FileType.Label);

            first.FileType = first.FileTypes[2];
            Assert.Equal("all", Saved().AppLoadFileType);
            Assert.Equal(first.FileTypes[2].Label, new AppLoadViewModel(model, Places()).FileType.Label);

            first.FileType = first.FileTypes[0];
            Assert.Equal("lsp", Saved().AppLoadFileType);
        }

        [Fact]
        public void AFilesOfTypeNobodyKnows_InTheSettingsFile_IsTakenForAutoLisp()
        {
            MainViewModel model = Opened(s => s.AppLoadFileType = "  ALL ");
            Assert.Equal("all", model.Settings.AppLoadFileType);

            model = Opened(s => s.AppLoadFileType = "weird");
            Assert.Equal("lsp", model.Settings.AppLoadFileType);
            Assert.Equal("AutoLISP Files (*.lsp)", new AppLoadViewModel(model, Places()).FileType.Label);
        }

        // ---- Columns

        [Fact]
        public void TheSizeColumn_IsInKilobytesRoundedUp_AndEmptyForAFolder()
        {
            Directory.CreateDirectory(Path.Combine(Browsed, "sub"));
            File.WriteAllBytes(Lisp("empty.lsp", ""), new byte[0]);
            File.WriteAllBytes(Path.Combine(Browsed, "one.lsp"), new byte[1]);
            File.WriteAllBytes(Path.Combine(Browsed, "kilo.lsp"), new byte[1024]);
            File.WriteAllBytes(Path.Combine(Browsed, "kilo1.lsp"), new byte[1025]);
            File.WriteAllBytes(Path.Combine(Browsed, "big.lsp"), new byte[5 * 1024 * 1024]);
            AppLoadViewModel dialog = Dialog();

            Dictionary<string, string> sizes = dialog.Entries.ToDictionary(e => e.Name, e => e.Size);

            Assert.Equal(string.Empty, sizes["sub"]);
            Assert.Equal("0 KB", sizes["empty.lsp"]);
            Assert.Equal("1 KB", sizes["one.lsp"]);
            Assert.Equal("1 KB", sizes["kilo.lsp"]);
            Assert.Equal("2 KB", sizes["kilo1.lsp"]);
            Assert.Equal("5120 KB", sizes["big.lsp"]);
        }

        // ---- Files dropped on Loaded Applications

        [Fact]
        public void TwoLspFilesDroppedOnLoadedApplications_LoadAtOnceInTheOrderOfTheDrop_AndTheFooterCountsThem()
        {
            string a = Lisp("a.lsp", "(princ \"[a]\")");
            string b = Lisp("b.lsp", "(princ \"[b]\")");
            MainViewModel model = Opened();
            var dialog = new AppLoadViewModel(model, Places());

            dialog.LoadDropped(new[] { b, a });

            Assert.Equal("Successfully loaded 2 files.", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Loaded, dialog.StatusKind);
            Assert.Equal(new[] { b, a }, dialog.LoadedRows.Select(r => r.FullPath));
            Assert.Equal(new[] { a, b }, dialog.HistoryRows.Select(r => r.FullPath));
            Assert.Equal(
                new[] { "[b]", "b.lsp successfully loaded.", "[a]", "a.lsp successfully loaded." },
                model.History.Skip(model.History.Count - 4)
            );
            Assert.Equal(Browsed, model.Settings.AppLoadFolder);
        }

        [Fact]
        public void OneFileDropped_ShowsItsOwnLine_AndAFailureTheLineOfTheLastFailure()
        {
            string a = Lisp("a.lsp", "(setq gha 1)");
            string bad = Lisp("bad.lsp", "(car 1)");
            string worse = Lisp("worse.lsp", "(cdr 1)");
            string notes = Lisp("notes.txt", "x");
            var dialog = new AppLoadViewModel(Opened(), Places());

            dialog.LoadDropped(new[] { a });
            Assert.Equal("a.lsp successfully loaded.", dialog.Status);

            dialog.LoadDropped(new[] { a, bad, worse, notes });

            Assert.Equal("notes.txt is not an AutoLISP file or a plugin.", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Refused, dialog.StatusKind);

            dialog.LoadDropped(new[] { a, bad, worse });

            Assert.Equal("worse.lsp was not loaded: ; error: bad argument type: consp 1", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Failed, dialog.StatusKind);
        }

        [Fact]
        public void AFolderDropped_OrADropOnTheHistoryList_LoadsNothing()
        {
            string a = Lisp("a.lsp", "(setq gha 1)");
            string sub = Directory.CreateDirectory(Path.Combine(Browsed, "sub")).FullName;
            var dialog = new AppLoadViewModel(Opened(), Places());

            dialog.LoadDropped(new[] { sub });
            Assert.Empty(dialog.LoadedRows);
            Assert.Equal(string.Empty, dialog.Status);

            dialog.IsHistoryTab = true;
            dialog.LoadDropped(new[] { a });
            dialog.IsHistoryTab = false;

            Assert.Empty(dialog.LoadedRows);
            Assert.Empty(dialog.HistoryRows);
        }

        // ---- The window

        private static AppLoadWindow KeyWindow(AppLoadViewModel dialog)
        {
            var window = new AppLoadWindow(dialog);
            Layout(window.Body, 720, 600);
            return window;
        }

        private static IEnumerable<DependencyObject> Visual(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject deeper in Visual(child))
                {
                    yield return deeper;
                }
            }
        }

        [Fact]
        public void TheLookInBox_ShowsTheIconAndTheNameOfTheFolder_NotItsPath_AndThePathAsItsTooltip()
        {
            Lisp("tools.lsp", "(setq ghl 1)");
            AppLoadViewModel dialog = Dialog();

            OnSta(() =>
            {
                AppLoadWindow window = KeyWindow(dialog);
                var look = (ComboBox)Named(window.Body)["Look in"];

                Assert.Equal(dialog.Places.Count, look.Items.Count);
                Assert.Same(dialog.Place, look.SelectedItem);
                Assert.Equal(Browsed, look.ToolTip);

                List<string> words = Visual(look).OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("lisp", words);
                Assert.DoesNotContain(Browsed, words);
                Assert.Contains(Visual(look), d => d is IconView);

                dialog.Place = dialog.Places.Single(r => r.Kind == AppLoadPlaceKind.ThisPc);
                Layout(window.Body, 720, 600);

                Assert.Same(dialog.Place, look.SelectedItem);
                Assert.Equal("This PC", look.ToolTip);
                Assert.Contains("This PC", Visual(look).OfType<TextBlock>().Select(t => t.Text));
            });
        }

        [Fact]
        public void ChoosingARowOfTheLookInBox_OpensIt()
        {
            FakePlaces places = Places();
            AppLoadViewModel dialog = Dialog(Browsed, places);

            OnSta(() =>
            {
                AppLoadWindow window = KeyWindow(dialog);
                var look = (ComboBox)Named(window.Body)["Look in"];

                // A choice made by the user keeps the binding that makes the box follow the model.
                look.SetCurrentValue(
                    System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
                    look.Items.Cast<AppLoadPlace>().Single(p => p.Name == "Documents")
                );
                Layout(window.Body, 720, 600);

                Assert.Equal(places.Documents, dialog.Folder);
                Assert.Equal("Documents", ((AppLoadPlace)look.SelectedItem).Name);
                Assert.True(((AppLoadPlace)look.SelectedItem).IsCurrent);
            });
        }

        [Fact]
        public void TheFileListHasTheColumnsNameDateModifiedTypeAndSize_InThatOrder()
        {
            Lisp("tools.lsp", "(setq ghl 1)");
            AppLoadViewModel dialog = Dialog();

            OnSta(() =>
            {
                AppLoadWindow window = KeyWindow(dialog);
                var files = (ListBox)Named(window.Body)["Files"];
                string[] heads = { "Name", "Date modified", "Type", "Size" };
                double listTop = Where(window.Body, files).Y;

                List<TextBlock> inRow = Visual(window.Body)
                    .OfType<TextBlock>()
                    .Where(t => heads.Contains(t.Text) && t.FontWeight == FontWeights.SemiBold)
                    .Where(t => Where(window.Body, t).Y < listTop)
                    .OrderBy(t => Where(window.Body, t).X)
                    .ToList();

                Assert.Equal(heads, inRow.Select(t => t.Text));
            });
        }

        private static Point Where(FrameworkElement body, FrameworkElement element) =>
            element.TransformToAncestor(body).Transform(new Point(0, 0));

        [Fact]
        public void TheKeys_BackspaceInTheListUp_AltUpUp_AltLeftBack_F5Refresh_F4AndAltDownOpenLookIn()
        {
            string sub = Directory.CreateDirectory(Path.Combine(Browsed, "sub")).FullName;
            Lisp("tools.lsp", "(setq ghtools 1)");
            AppLoadViewModel dialog = Dialog(sub);

            OnSta(() =>
            {
                AppLoadWindow window = KeyWindow(dialog);
                var list = (ListBox)Named(window.Body)["Files"];
                var name = (TextBox)Named(window.Body)["File name"];
                var look = (ComboBox)Named(window.Body)["Look in"];

                Assert.False(window.HandleKey(Key.Back, false, name));
                Assert.Equal(sub, dialog.Folder);

                Assert.True(window.HandleKey(Key.Back, false, list));
                Assert.Equal(Browsed, dialog.Folder);

                Assert.True(window.HandleKey(Key.Left, true, name));
                Assert.Equal(sub, dialog.Folder);

                Assert.True(window.HandleKey(Key.Up, true, name));
                Assert.Equal(Browsed, dialog.Folder);

                File.WriteAllText(Path.Combine(Browsed, "fresh.lsp"), "x");
                Assert.DoesNotContain(dialog.Entries, e => e.Name == "fresh.lsp");
                Assert.True(window.HandleKey(Key.F5, false, name));
                Assert.Contains(dialog.Entries, e => e.Name == "fresh.lsp");

                Assert.False(look.IsDropDownOpen);
                Assert.True(window.HandleKey(Key.F4, false, list));
                Assert.True(window.HandleKey(Key.Down, true, list));
                Assert.False(window.HandleKey(Key.F4, false, look));
                Assert.False(window.HandleKey(Key.Down, true, look));
                Assert.False(window.HandleKey(Key.F4, false, Named(window.Body)["Files of type"]));

                Assert.False(window.HandleKey(Key.A, false, list));
            });
        }

        [Fact]
        public void EnterInTheList_OpensAFolder_AndLoadsTheFilesChosen()
        {
            string sub = Directory.CreateDirectory(Path.Combine(Browsed, "sub")).FullName;
            string tools = Lisp("tools.lsp", "(setq ghtools 1)");
            AppLoadViewModel dialog = Dialog();

            OnSta(() =>
            {
                AppLoadWindow window = KeyWindow(dialog);
                var list = (ListBox)Named(window.Body)["Files"];

                list.SelectedItem = list.Items.Cast<AppLoadEntry>().Single(e => e.Name == "sub");
                Assert.True(window.HandleKey(Key.Enter, false, list));
                Assert.Equal(sub, dialog.Folder);
                Assert.Empty(dialog.LoadedRows);

                dialog.Back();
                Layout(window.Body, 720, 600);
                list.SelectedItem = list.Items.Cast<AppLoadEntry>().Single(e => e.Name == "tools.lsp");
                Assert.True(window.HandleKey(Key.Enter, false, list));

                Assert.Equal(new[] { tools }, dialog.LoadedRows.Select(r => r.FullPath));
            });
        }

        [Fact]
        public void TheFileList_FindsByTypedLettersOnTheName_AndTheLoadedListTakesDroppedFiles()
        {
            AppLoadViewModel dialog = Dialog();

            OnSta(() =>
            {
                AppLoadWindow window = KeyWindow(dialog);
                var files = (ListBox)Named(window.Body)["Files"];
                var applications = (ListBox)Named(window.Body)["Applications"];

                Assert.Equal("Name", System.Windows.Controls.TextSearch.GetTextPath(files));
                Assert.True(files.IsTextSearchEnabled);
                Assert.True(applications.AllowDrop);
            });
        }

        [Fact]
        public void EveryFramelessWindowThatResizes_DropsTheFrameOfTheSystem_SoNoLightLineRunsAlongItsTopEdge()
        {
            MainViewModel model = Opened();

            OnSta(() =>
            {
                Window[] resizable =
                {
                    new AppLoadWindow(new AppLoadViewModel(model, Places())),
                    new TextWindow(new TextWindowViewModel(model.History)),
                    new ToolPalettesWindow(new ToolPalettesViewModel(model)),
                    new ParametersPaletteWindow(new ParametersPaletteViewModel()),
                };

                foreach (Window window in resizable)
                {
                    Assert.Equal(WindowStyle.None, window.WindowStyle);
                    Assert.NotEqual(ResizeMode.NoResize, window.ResizeMode);

                    System.Windows.Shell.WindowChrome chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(
                        window
                    );

                    Assert.NotNull(chrome);
                    Assert.Equal(new Thickness(0), chrome.GlassFrameThickness);
                    Assert.Equal(0, chrome.CaptionHeight);
                    Assert.True(chrome.ResizeBorderThickness.Top > 0);
                }

                var question = new CadQuestionWindow("Error", "x", "y", "OK", null);
                Assert.Equal(ResizeMode.NoResize, question.ResizeMode);
            });
        }
    }
}
