using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;
using static GeometryHelper.Cad.Wpf.UnitTest.SecurityKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Files dropped on the window, as AutoCAD 2021 takes them (measured in part M, rows 1 to 14 of drop.md): a .lsp, .fas or
    /// .vlx runs the line <c>(LOAD "D:/x/u1.lsp")</c> as if typed, a .scr runs SCRIPT of the file, a .arx, .dbx or .dvb is
    /// refused, any other file gets the "Drag and Drop" box, and all of it is ignored while a command waits. Driven at the
    /// view-model level, with no real drag; the last tests raise the drop through the command line's box and onto the drawing
    /// area and the DRAWINGS list. A drawing dropped on the drawing area is inserted as a block, as AutoCAD's -INSERT takes it
    /// (rows 13 and 14); dropped on the DRAWINGS list it opens.
    /// </summary>
    public sealed class FileDropTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private string Room(string name) => _folder.Write(name, TestFiles.Room(out _, out _));

        /// <summary>A window's model on a settings file of its own with a drawing open; SECURELOAD is the window's 1, and nothing is trusted.</summary>
        private MainViewModel Opened(Action<GlobalSettings> settings = null)
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            GlobalSettings changed = model.Settings.Clone();
            changed.TrustedPaths = string.Empty;
            settings?.Invoke(changed);
            model.ApplySettings(changed);
            Assert.True(model.Open(Room("room.dxf")));
            return model;
        }

        private string RoomDwg(string name)
        {
            string path = Path.Combine(_folder.FullPath, name);
            DwgWriter.Write(TestFiles.Room(out _, out _), path);
            return path;
        }

        private static int References(MainViewModel model) =>
            model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Count();

        private static string Slashes(string path) => path.Replace('\\', '/');

        private static string[] Since(MainViewModel model, int count) => model.History.Skip(count).ToArray();

        private static int Shapes(MainViewModel model) => model.Document.ModelSpace.Block.Count();

        [Fact]
        public void ATrustedLspDropped_RunsTheTypedLoadLine_DefinesItsCommand_AndShowsTheFilesOutput()
        {
            string tools = Code(
                _folder,
                "tools.lsp",
                "(defun c:dropped () (princ \"ran\"))\n(princ \"GHM tools.lsp loaded\")\n"
            );
            MainViewModel model = Opened(s => s.TrustedPaths = _folder.FullPath + @"\...");
            int before = model.History.Count;

            model.DropFiles(new[] { tools });

            string[] written = Since(model, before);
            Assert.Equal("Command: (LOAD \"" + Slashes(tools) + "\")", written[0].TrimEnd());
            Assert.Contains("GHM tools.lsp loaded", string.Join("\n", written));
            Assert.Contains("dropped", model.Session.LispCommandNames, StringComparer.OrdinalIgnoreCase);

            // The file is among the loaded applications.
            Assert.Equal(new[] { tools }, model.LoadedLispFiles);
        }

        [Fact]
        public void ADroppedLsp_WritesWhatTheSameLineTypedWrites()
        {
            string tools = Code(_folder, "same.lsp", "(princ \"hello\")\n");
            MainViewModel dropped = Opened(s => s.SecureLoad = 0);
            MainViewModel typed = Opened(s => s.SecureLoad = 0);
            int droppedFrom = dropped.History.Count;
            int typedFrom = typed.History.Count;

            dropped.DropFiles(new[] { tools });
            typed.Execute("(LOAD \"" + Slashes(tools) + "\")");

            Assert.Equal(Since(typed, typedFrom), Since(dropped, droppedFrom));
        }

        [Fact]
        public void AnUntrustedLspAtSecureLoadOne_AsksTheQuestion_AndLoadOnceLoadsIt()
        {
            string once = Code(_folder, "once.lsp", "(princ \"GHM once loaded\")\n");
            MainViewModel model = Opened();
            var asked = new List<string>();
            model.LoadSecurity.Ask = path =>
            {
                asked.Add(path);
                return CadLoadAnswer.LoadOnce;
            };
            int before = model.History.Count;

            model.DropFiles(new[] { once });

            Assert.Equal(new[] { once }, asked);
            Assert.Contains("GHM once loaded", string.Join("\n", Since(model, before)));
        }

        [Fact]
        public void DoNotLoad_WritesTheCancelLineAfterTheEcho_AndLoadsNothing()
        {
            string refused = Code(_folder, "refused.lsp", "(defun c:refused () (princ))\n");
            MainViewModel model = Opened();
            model.LoadSecurity.Ask = path => CadLoadAnswer.DoNotLoad;
            int before = model.History.Count;

            model.DropFiles(new[] { refused });

            string[] written = Since(model, before);
            Assert.Equal("Command: (LOAD \"" + Slashes(refused) + "\")", written[0].TrimEnd());
            Assert.Equal("; error: File load canceled: " + Slashes(refused), written.Last());
            Assert.Empty(model.LoadedLispFiles);
        }

        [Theory]
        [InlineData(".fas")]
        [InlineData(".vlx")]
        public void ACompiledFileDropped_AsksTheQuestion_ThenWritesTheCompiledRefusalAsItsError(string extension)
        {
            string compiled = Code(_folder, "compiled" + extension, "not really compiled");
            MainViewModel model = Opened();
            int asked = 0;
            model.LoadSecurity.Ask = path =>
            {
                asked++;
                return CadLoadAnswer.LoadOnce;
            };
            int before = model.History.Count;

            model.DropFiles(new[] { compiled });

            string[] written = Since(model, before);
            Assert.Equal(1, asked);
            Assert.Equal("Command: (LOAD \"" + Slashes(compiled) + "\")", written[0].TrimEnd());
            Assert.Contains(written, l => l.Contains("; error:"));
        }

        [Fact]
        public void AScrDropped_RunsScriptOfTheFile_WithItsPathInQuotes_AndNoQuestion()
        {
            string script = _folder.WriteText("draw.scr", "_.LINE\n0,0\n10,10\n\n");
            MainViewModel model = Opened();
            model.LoadSecurity.Ask = path => throw new InvalidOperationException("SCRIPT is not gated.");
            int shapes = Shapes(model);
            int before = model.History.Count;

            model.DropFiles(new[] { script });

            string[] written = Since(model, before);
            Assert.Equal("Command: _SCRIPT", written[0].TrimEnd());
            Assert.Contains(written, l => l.EndsWith("\"" + script + "\"", StringComparison.Ordinal));
            Assert.Equal(shapes + 1, Shapes(model));
        }

        [Theory]
        [InlineData(".dcl")]
        [InlineData(".dll")]
        [InlineData(".ghplugin")]
        [InlineData(".txt")]
        public void AnyOtherFileDropped_RaisesTheDragAndDropBoxOnce_AndWritesNothingOnTheCommandLine(string extension)
        {
            string other = _folder.WriteText("other" + extension, "text");
            MainViewModel model = Opened();
            var boxes = new List<(string Title, string Text)>();
            model.ShowDropMessage = (title, text) => boxes.Add((title, text));
            int before = model.History.Count;

            model.DropFiles(new[] { other });

            Assert.Equal(("Drag and Drop", "No association.." + other), Assert.Single(boxes));
            Assert.Empty(Since(model, before));
        }

        [Fact]
        public void TwoFilesWithNoAssociation_GetOneBoxEach_InTheOrderOfTheDrop()
        {
            string first = _folder.WriteText("b.dll", "x");
            string second = _folder.WriteText("a.dcl", "x");
            MainViewModel model = Opened();
            var boxes = new List<string>();
            model.ShowDropMessage = (title, text) => boxes.Add(text);

            model.DropFiles(new[] { first, second });

            Assert.Equal(new[] { "No association.." + first, "No association.." + second }, boxes);
        }

        [Fact]
        public void AnArxDropped_WritesTheRefusalLineOfAppLoad()
        {
            string arx = _folder.WriteText("tools.arx", "x");
            MainViewModel model = Opened();
            int before = model.History.Count;

            model.DropFiles(new[] { arx });

            Assert.Equal(
                new[] { "tools.arx cannot be loaded: .arx, .dbx, .dvb, .fas and .vlx files are not supported." },
                Since(model, before)
            );
        }

        [Fact]
        public void ADwgDroppedOnTheDrawingArea_StartsInsertWithItsPath_AndPlacesABlockReferenceAtThePointGiven()
        {
            string other = RoomDwg("other.dwg");
            MainViewModel model = Opened();
            CadDocument shown = model.Document;
            int before = model.History.Count;

            model.DropFiles(new[] { other });

            // As AutoCAD writes it (row 13): the typed -INSERT, the path in quotes, the Units line, then it waits for the point.
            string[] written = Since(model, before);
            Assert.Equal("Command: _-INSERT", written[0].TrimEnd());
            Assert.Contains(written, l => l.Contains("\"" + other + "\""));
            Assert.Contains(written, l => l.StartsWith("Units:", StringComparison.Ordinal));
            Assert.True(model.Session.IsRunning);
            Assert.StartsWith("Specify insertion point", model.Session.Prompt.Text, StringComparison.Ordinal);
            Assert.Same(shown, model.Document);
            Assert.Equal(0, References(model));

            model.Execute("500,500");
            model.Enter();
            model.Enter();
            model.Enter();

            Assert.False(model.Session.IsRunning);
            DxfBlockReference reference = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfBlockReference>());
            Assert.Equal("other", reference.Name);
            Assert.Equal(500.0, reference.Position.X, 6);
            Assert.Equal(500.0, reference.Position.Y, 6);
            Assert.Same(shown, model.Document);
        }

        [Fact]
        public void ADxfDroppedOnTheDrawingArea_IsInsertedToo_AndEscapeCancelsIt()
        {
            string other = Room("other.dxf");
            MainViewModel model = Opened();
            int before = model.History.Count;

            model.DropFiles(new[] { other });

            Assert.Contains(Since(model, before), l => l.Contains("\"" + other + "\""));
            Assert.True(model.Session.IsRunning);

            model.Escape();

            Assert.False(model.Session.IsRunning);
            Assert.Equal(0, References(model));
        }

        [Fact]
        public void ADrawingDroppedOnTheDrawingsList_Opens_AsBefore()
        {
            string other = RoomDwg("other.dwg");
            MainViewModel model = Opened();

            model.DropFiles(new[] { other }, onDrawingList: true);

            Assert.Equal(Path.GetFullPath(other), Path.GetFullPath(model.Document.FileName));
            Assert.False(model.Session.IsRunning);
        }

        [Fact]
        public void ADrawingDroppedOnTheDrawingAreaWithNoDrawingOpen_IsOpened_AsAutoCadOpensItThen()
        {
            string other = RoomDwg("other.dwg");
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            Assert.False(model.HasDocument);

            model.DropFiles(new[] { other });

            Assert.True(model.HasDocument);
            Assert.Equal(Path.GetFullPath(other), Path.GetFullPath(model.Document.FileName));
            Assert.Equal(0, References(model));
        }

        [Fact]
        public void SeveralDrawingsDroppedOnTheDrawingArea_InsertTheFirst_AndPassTheRestOver()
        {
            string first = RoomDwg("first.dwg");
            string second = RoomDwg("second.dwg");
            MainViewModel model = Opened();
            string shown = model.Document.FileName;

            model.DropFiles(new[] { first, second });

            // One -INSERT waits, for the first; the second is passed over with no word, as a drop while a command waits is.
            Assert.True(model.Session.IsRunning);
            model.Execute("0,0");
            model.Enter();
            model.Enter();
            model.Enter();
            DxfBlockReference reference = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfBlockReference>());
            Assert.Equal("first", reference.Name);
            Assert.Equal(shown, model.Document.FileName);
        }

        [Fact]
        public void SeveralDrawingsDroppedOnTheDrawingsList_AllOpen()
        {
            string first = RoomDwg("first.dwg");
            string second = RoomDwg("second.dwg");
            MainViewModel model = Opened();
            int before = model.History.Count;

            model.DropFiles(new[] { first, second }, onDrawingList: true);

            Assert.Equal(Path.GetFullPath(second), Path.GetFullPath(model.Document.FileName));
            Assert.Contains(Since(model, before), l => l.StartsWith("Opened first.dwg", StringComparison.Ordinal));
            Assert.Contains(Since(model, before), l => l.StartsWith("Opened second.dwg", StringComparison.Ordinal));
        }

        [Fact]
        public void ADrawingDroppedOnTheDrawingAreaWhileLineWaits_IsIgnored_AndLineStillWaits()
        {
            string other = RoomDwg("other.dwg");
            MainViewModel model = Opened();
            model.StartCommand("LINE");
            model.Execute("0,0");
            Assert.True(model.Session.IsRunning);
            string prompt = model.Session.Prompt.Text;
            string shown = model.Document.FileName;
            int before = model.History.Count;

            model.DropFiles(new[] { other });

            Assert.True(model.Session.IsRunning);
            Assert.Equal(prompt, model.Session.Prompt.Text);
            Assert.Equal(shown, model.Document.FileName);
            Assert.Empty(Since(model, before));

            model.Escape();

            Assert.Equal(0, References(model));
        }

        [Fact]
        public void ADrawingDroppedOnTheDrawingsListOrTheTitleBarOrTheDrawingArea_IsRoutedByTheWorkspaceByWhereItLands()
        {
            // The file is the drawing already open, so that "opens" shows as no -INSERT started and no second window to build.
            string shown = Path.Combine(_folder.FullPath, "room.dxf");

            OnWindowThread(() =>
            {
                MainViewModel model = Opened();
                var workspace = new CadWorkspace { DataContext = model };
                workspace.Measure(new Size(1600, 960));
                workspace.Arrange(new Rect(0, 0, 1600, 960));
                workspace.UpdateLayout();
                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, new[] { shown });

                DragEventArgs onCanvas = Drag(data, DragDrop.PreviewDropEvent);
                workspace.CanvasView.RaiseEvent(onCanvas);

                Assert.True(onCanvas.Handled);
                Assert.True(model.Session.IsRunning);
                model.Escape();
                Assert.False(model.Session.IsRunning);

                DragEventArgs onList = Drag(data, DragDrop.PreviewDropEvent);
                workspace.FileListPanel.RaiseEvent(onList);

                Assert.True(onList.Handled);
                Assert.False(model.Session.IsRunning);

                DragEventArgs onTitle = Drag(data, DragDrop.PreviewDropEvent);
                workspace.TitleBar.RaiseEvent(onTitle);

                Assert.True(onTitle.Handled);
                Assert.False(model.Session.IsRunning);
            });
        }

        [Fact]
        public void SecondAndThirdDrawingsDroppedOnTheDrawingsListOfABoundWorkspace_OpenOneAfterTheOther()
        {
            string second = RoomDwg("second.dwg");
            string third = RoomDwg("third.dwg");

            OnWindowThread(() =>
            {
                MainViewModel model = Opened();
                var workspace = new CadWorkspace { DataContext = model };
                workspace.Measure(new Size(1600, 960));
                workspace.Arrange(new Rect(0, 0, 1600, 960));
                workspace.UpdateLayout();

                // The workspace answers the Missing SHX files question with a modal window, which nobody closes in a test host: a
                // drawing written with the Standard style in txt.shx opens it, and the host waits for ever. Answered here with Cancel.
                var asked = new List<string>();
                model.ShowMissingShx = dialog =>
                {
                    asked.Add(dialog.Title);
                    return false;
                };

                foreach (string file in new[] { second, third })
                {
                    var data = new DataObject();
                    data.SetData(DataFormats.FileDrop, new[] { file });
                    DragEventArgs onList = Drag(data, DragDrop.PreviewDropEvent);
                    workspace.FileListPanel.RaiseEvent(onList);
                    workspace.UpdateLayout();

                    Assert.True(onList.Handled);
                    Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(model.Document.FileName));
                    Assert.False(model.Session.IsRunning);
                }

                Assert.Equal(new[] { "Missing SHX files · second.dwg", "Missing SHX files · third.dwg" }, asked);
            });
        }

        [Fact]
        public void TwoLspFilesDropped_LoadInTheOrderGiven_AndALspAfterADrawingLoadsIntoThatDrawing()
        {
            string b = Code(_folder, "b.lsp", "(setq ghb 1)\n");
            string a = Code(_folder, "a.lsp", "(setq gha 1)\n");
            string c = Code(_folder, "c.lsp", "(setq ghc 1)\n");
            string other = Room("other.dxf");
            MainViewModel model = Opened(s => s.SecureLoad = 0);
            CadCommandSession first = model.Session;

            model.DropFiles(new[] { b, a });

            Assert.Equal(new[] { b, a }, model.LoadedLispFiles);

            // Dropped on the DRAWINGS list, where a drawing opens; on the drawing area it would be inserted (see above).
            model.DropFiles(new[] { other, c }, onDrawingList: true);

            Assert.NotSame(first, model.Session);
            Assert.Equal(new[] { c }, model.LoadedLispFiles);
            Assert.Equal(new[] { b, a }, first.LoadedLispFiles);
        }

        [Fact]
        public void FilesDroppedWhileACommandWaits_AreIgnored_AndTheCommandStillWaits()
        {
            string tools = Code(_folder, "tools.lsp", "(defun c:ignored () (princ))\n");
            string other = _folder.WriteText("other.dll", "x");
            string script = _folder.WriteText("draw.scr", "_.CIRCLE\n0,0\n5\n");
            string arx = _folder.WriteText("t.arx", "x");
            MainViewModel model = Opened(s => s.SecureLoad = 0);
            int boxes = 0;
            model.ShowDropMessage = (title, text) => boxes++;
            model.StartCommand("LINE");
            model.Execute("0,0");
            Assert.True(model.Session.IsRunning);
            string prompt = model.Session.Prompt.Text;
            int before = model.History.Count;
            int shapes = Shapes(model);

            model.DropFiles(new[] { tools, script, other, arx });

            Assert.True(model.Session.IsRunning);
            Assert.Equal(prompt, model.Session.Prompt.Text);
            Assert.Equal(0, boxes);
            Assert.Empty(Since(model, before));
            Assert.Empty(model.LoadedLispFiles);

            model.Escape();

            Assert.Empty(model.LoadedLispFiles);
            Assert.Equal(shapes, Shapes(model));
        }

        [Fact]
        public void ALspDroppedWhileARoutineWaits_IsIgnored()
        {
            string asking = Code(_folder, "asking.lsp", "(defun c:asking () (getpoint \"Pick: \"))\n");
            string tools = Code(_folder, "tools.lsp", "(setq ght 1)\n");
            MainViewModel model = Opened(s => s.SecureLoad = 0);
            model.DropFiles(new[] { asking });
            model.Execute("ASKING");
            Assert.True(model.Session.IsRunning);
            int before = model.History.Count;

            model.DropFiles(new[] { tools });

            Assert.Equal(new[] { asking }, model.LoadedLispFiles);
            Assert.Empty(Since(model, before));
        }

        [Fact]
        public void WithNoDrawingOpen_NothingHappensForTheFilesThatLoad_RunOrShowTheBox()
        {
            string tools = Code(_folder, "tools.lsp", "(princ)\n");
            string script = _folder.WriteText("draw.scr", "_.LINE\n");
            string other = _folder.WriteText("other.dll", "x");
            string arx = _folder.WriteText("t.arx", "x");
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            int boxes = 0;
            model.ShowDropMessage = (title, text) => boxes++;
            int before = model.History.Count;

            model.DropFiles(new[] { tools, script, other, arx });

            Assert.False(model.HasDocument);
            Assert.Equal(0, boxes);
            Assert.Empty(Since(model, before));
        }

        [Fact]
        public void ADropRaisedThroughTheCommandLineBox_IsTakenByTheWorkspaceOnce_AndMarkedHandled()
        {
            string tools = Code(_folder, "tools.lsp", "(princ \"through the box\")\n");

            OnWindowThread(() =>
            {
                MainViewModel model = Opened(s => s.SecureLoad = 0);
                var workspace = new CadWorkspace { DataContext = model };
                workspace.Measure(new Size(1600, 960));
                workspace.Arrange(new Rect(0, 0, 1600, 960));
                workspace.UpdateLayout();
                int before = model.History.Count;

                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, new[] { tools });
                DragEventArgs over = Drag(data, DragDrop.PreviewDragOverEvent);
                workspace.CommandBox.RaiseEvent(over);
                DragEventArgs drop = Drag(data, DragDrop.PreviewDropEvent);
                workspace.CommandBox.RaiseEvent(drop);

                Assert.True(over.Handled);
                Assert.Equal(DragDropEffects.Copy, over.Effects);
                Assert.True(drop.Handled);
                Assert.Equal(new[] { tools }, model.LoadedLispFiles);
                Assert.Single(model.History.Skip(before), l =>
                    l.StartsWith("Command: (LOAD", StringComparison.Ordinal)
                );
            });
        }

        [Fact]
        public void ADropOnThePythonConsolesTranscript_IsLeftToTheTranscript_AsBefore()
        {
            string tools = Code(_folder, "tools.lsp", "(princ \"not here\")\n");

            OnWindowThread(() =>
            {
                MainViewModel model = Opened(s => s.SecureLoad = 0);
                var workspace = new CadWorkspace { DataContext = model };
                workspace.Measure(new Size(1600, 960));
                workspace.Arrange(new Rect(0, 0, 1600, 960));
                workspace.UpdateLayout();

                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, new[] { tools });
                DragEventArgs drop = Drag(data, DragDrop.PreviewDropEvent);
                workspace.PythonPalette.TranscriptBox.RaiseEvent(drop);

                Assert.Empty(model.LoadedLispFiles);
            });
        }

        /// <summary>A drag event as the system raises it: the constructor of <see cref="DragEventArgs"/> is internal.</summary>
        private static DragEventArgs Drag(IDataObject data, RoutedEvent kind)
        {
            ConstructorInfo make = typeof(DragEventArgs)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Single();
            object[] arguments = make.GetParameters()
                .Select(p =>
                    p.ParameterType == typeof(IDataObject) ? data
                    : p.ParameterType == typeof(DragDropKeyStates) ? (object)DragDropKeyStates.None
                    : p.ParameterType == typeof(DragDropEffects) ? (object)(DragDropEffects.Copy | DragDropEffects.Move)
                    : p.ParameterType == typeof(Point) ? (object)new Point(10, 10)
                    : null
                )
                .ToArray();
            var args = (DragEventArgs)make.Invoke(arguments);
            args.RoutedEvent = kind;
            return args;
        }
    }
}
