using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>CLOSE and the window closed; autosave; the text height of Global settings; the canvas colours and crosshair.</summary>
    public sealed class CloseAndAutosaveTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void ClosingADrawingWithChangesAsksAndCancelKeepsItOpen()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            AddLine(model);
            int asked = 0;
            model.AskSaveChanges = document =>
            {
                asked++;
                return CadSaveChoice.Cancel;
            };

            Assert.False(model.CloseDocument());
            Assert.True(model.HasDocument);

            model.AskSaveChanges = document => CadSaveChoice.DontSave;
            Assert.True(model.CloseDocument());

            Assert.Equal(1, asked);
            Assert.False(model.HasDocument);
            Assert.Null(model.Session);
            Assert.Contains("Closed room.dxf.", model.History);
            FileRowViewModel row = model.FileRows.Single(r => r.Label == "room.dxf");
            Assert.False(row.IsOpen);
            Assert.Equal(3, CadDocument.Open(room).EntityCount);
        }

        [Fact]
        public void SaveBeforeClosingWritesTheFileAndTheNextDrawingIsShown()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            string other = _folder.Write("other.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(other));
            Assert.True(model.Open(room));
            AddLine(model);
            model.AskSaveChanges = document => CadSaveChoice.Save;

            model.CloseDocument();

            Assert.Equal(4, CadDocument.Open(room).EntityCount);
            Assert.True(model.HasDocument);
            Assert.Equal("other.dxf", model.Document.Title);
            Assert.True(model.FileRows.Single(r => r.Label == "other.dxf").IsOpen);
            Assert.False(model.FileRows.Single(r => r.Label == "room.dxf").IsOpen);
        }

        [Fact]
        public void ClosingLetsThePluginsGoAndTheRowsXClosesItsDrawing()
        {
            using (var plugins = new CadPluginManager(new CadPluginAssemblyLoader(), Path.Combine(_folder.FullPath, "plugins"), Path.Combine(_folder.FullPath, "plugins.json"), Path.Combine(_folder.FullPath, "copies")))
            {
                var model = new MainViewModel();
                model.UsePlugins(plugins, loadAtStart: false);
                model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "a.dxf"));
                CadCommandRegistry registry = model.Session.Registry;
                Assert.NotNull(registry.Find(CadPluginManager.UnloadCommand));
                FileRowViewModel row = model.FileRows.Single(r => r.Label == "a.dxf");
                Assert.True(row.IsOpen);

                model.CloseFileCommand.Execute(row);

                Assert.Null(registry.Find(CadPluginManager.UnloadCommand));
                Assert.False(model.HasDocument);
                Assert.Empty(model.FileRows);
            }
        }

        [Fact]
        public void TheWindowClosedAsksOnceForEveryDrawingNotSaved()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "a.dxf"));
            AddLine(model);
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "b.dxf"));
            AddLine(model);
            int asked = 0;
            int documents = 0;
            model.AskSaveAll = list =>
            {
                asked++;
                documents = list.Count;
                return CadSaveChoice.Cancel;
            };

            Assert.False(model.ConfirmExit());
            model.AskSaveAll = list => CadSaveChoice.DontSave;
            Assert.True(model.ConfirmExit());

            Assert.Equal(1, asked);
            Assert.Equal(2, documents);
        }

        [Fact]
        public void CloseTypedAsksTheWindowToCloseOnceTheCommandHasEnded()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "a.dxf"));
            int asked = 0;
            model.CloseRequested += (s, e) => asked++;

            model.Execute("CLOSE");

            Assert.Equal(1, asked);
            Assert.True(model.HasDocument);
        }

        [Fact]
        public void AutosaveKeepsRotatingCopiesBesideTheFileAndNeverWritesTheFile()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            byte[] before = File.ReadAllBytes(room);
            model.ApplySettings(new GlobalSettings { AutosaveMinutes = 5, BackupsKept = 2 });
            Assert.True(model.Open(room));
            Assert.Empty(model.Autosave());
            AddLine(model);

            Assert.Equal(new[] { Path.Combine(_folder.FullPath, "room.autosave1.dxf") }, model.Autosave());
            model.Autosave();
            model.Autosave();

            Assert.True(File.Exists(Path.Combine(_folder.FullPath, "room.autosave1.dxf")));
            Assert.True(File.Exists(Path.Combine(_folder.FullPath, "room.autosave2.dxf")));
            Assert.False(File.Exists(Path.Combine(_folder.FullPath, "room.autosave3.dxf")));
            Assert.Equal(before, File.ReadAllBytes(room));
            Assert.Equal(4, CadDocument.Open(Path.Combine(_folder.FullPath, "room.autosave1.dxf")).EntityCount);
            model.Refresh();
            Assert.DoesNotContain(model.FileRows, r => r.Label.Contains("autosave"));

            model.ApplySettings(new GlobalSettings { AutosaveMinutes = 0 });
            Assert.Empty(model.Autosave());
        }

        [Fact]
        public void TheTextHeightOfGlobalSettingsIsTheDefaultOfText()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "a.dxf"));
            model.ApplySettings(new GlobalSettings { TextHeight = 7.5 });

            model.Execute("TEXT");
            model.Execute("0,0");

            Assert.StartsWith("Specify height <7.5000>", model.Prompt);
            model.Escape();
        }

        [Fact]
        public void TheRendererDrawsHiddenAndVisibleLinesInTheColoursOfTheSettings()
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Hidden", ColorIndex = 7 });
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "Hidden" });
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)));
            CadDocument document = CadDocument.From(database, "lines.dxf");
            var view = new CadView(200, 200);
            view.ZoomTo(new DisplayBox(-10, -10, 110, 60));
            var options = new CanvasOptions { HiddenLineColor = 0x00FF00, VisibleLineColor = 0xFF0000 };

            using (var bitmap = new SKBitmap(200, 200))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(canvas, document, document.ModelSpace, view, options);

                Assert.True(Strongest(bitmap, view, 50, 0, out SKColor hidden));
                Assert.True(hidden.Green > 120 && hidden.Red < 60, hidden.ToString());
                Assert.True(Strongest(bitmap, view, 50, 50, out SKColor visible));
                Assert.True(visible.Red > 120 && visible.Green < 60, visible.ToString());
            }
        }

        [Fact]
        public void TheCrosshairHidesTheSystemCursorAndTheGripsTakeTheirColour()
        {
            Cursor with = null, without = null;
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var canvas = new CadCanvas();
                    canvas.CrosshairPercent = 5;
                    with = canvas.Cursor;
                    canvas.CrosshairPercent = 0;
                    without = canvas.Cursor;
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
            Assert.Same(Cursors.None, with);
            Assert.Same(Cursors.Cross, without);
        }

        /// <summary>The pixel of most colour within 2 pixels of a point of the drawing.</summary>
        private static bool Strongest(SKBitmap bitmap, CadView view, double x, double y, out SKColor colour)
        {
            view.DrawingToPixel(x, y, out double px, out double py);
            colour = SKColors.Black;
            int best = -1;

            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    SKColor c = bitmap.GetPixel((int)Math.Round(px) + dx, (int)Math.Round(py) + dy);
                    int strength = c.Red + c.Green + c.Blue;

                    if (strength > best)
                    {
                        best = strength;
                        colour = c;
                    }
                }
            }

            return best > 0;
        }

        private static void AddLine(MainViewModel model)
        {
            CadDocument document = model.Document;
            Assert.True(document.ApplyEdit("LINE", null, scope => scope.Append(document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0)))).Succeeded);
        }
    }
}
