using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The External References palette and the attach dialogs of C18b, driven with no window through their view models, and
    /// laid out on a thread of their own to see the design's parts: the orange-red close cross, the Attach menu of two kinds
    /// offered and six greyed, the right-click menu with Bind greyed.
    /// </summary>
    public class XrefWindowTests
    {
        /// <summary>A drawing of a grid line on layer GRID and a circle, saved as xref\grid.dxf.</summary>
        private static string Grid(TestFolder folder)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID" });
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" });
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0));
            Directory.CreateDirectory(Path.Combine(folder.FullPath, "xref"));
            return folder.Write(Path.Combine("xref", "grid.dxf"), database);
        }

        /// <summary>A PNG of 40 by 30 pixels, a red disc on transparent, saved as logo.png.</summary>
        private static string Logo(TestFolder folder)
        {
            string path = Path.Combine(folder.FullPath, "logo.png");

            using (var bitmap = new SKBitmap(new SKImageInfo(40, 30, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
            using (var canvas = new SKCanvas(bitmap))
            using (var red = new SKPaint { Color = SKColors.Red })
            {
                bitmap.Erase(SKColors.Transparent);
                canvas.DrawCircle(20, 15, 10, red);

                using (SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                {
                    File.WriteAllBytes(path, data.ToArray());
                }
            }

            return path;
        }

        /// <summary>The window's view model over plan-level2.dxf with grid attached at the origin and the logo at (10, 20).</summary>
        private static MainViewModel Plan(TestFolder folder, out string grid, out string logo)
        {
            grid = Grid(folder);
            logo = Logo(folder);
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan-level2.dxf", CadTemplate.CreateMetric())));
            model.Execute("-XREF");
            model.Execute("A");
            model.Execute(grid);
            model.Execute("0,0");
            model.Execute(string.Empty);
            model.Execute(string.Empty);
            model.Execute(string.Empty);
            model.Execute("-IMAGE");
            model.Execute("A");
            model.Execute(logo);
            model.Execute("10,20");
            model.Execute(string.Empty);
            model.Execute(string.Empty);
            return model;
        }

        [Fact]
        public void ThePalette_ListsTheDrawingThenItsReferences_AndUnloadsReloadsChangesThePathAndDetachesThroughTheCommands()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out string grid, out _);
                ExternalReferencesViewModel palette = null;
                model.ShowExternalReferences = shown => palette = shown;

                model.Execute("ER");

                Assert.NotNull(palette);
                Assert.Equal(new[] { "plan-level2", "grid", "logo" }, palette.Rows.Select(r => r.Name).ToArray());
                Assert.Equal(new[] { "Opened", "Loaded", "Loaded" }, palette.Rows.Select(r => r.StatusText).ToArray());
                Assert.Equal(new[] { "Current", "Attach", "PNG" }, palette.Rows.Select(r => r.TypeName).ToArray());
                Assert.EndsWith(" KB", palette.Rows[1].SizeText, StringComparison.Ordinal);

                palette.Selected = palette.Rows[1];
                Dictionary<string, string> details = palette.Details.ToDictionary(d => d.Key, d => d.Value);
                Assert.Equal(".\\xref\\grid.dxf", details["Saved Path"]);
                Assert.Equal(Path.GetFullPath(grid), details["Found At"]);
                Assert.Equal("Relative path", details["Path type"]);
                Assert.True(palette.OpenCommand.CanExecute(null));

                palette.UnloadCommand.Execute(null);
                Assert.Equal("Unloaded", palette.Rows[1].StatusText);
                palette.ReloadCommand.Execute(null);
                Assert.Equal("Loaded", palette.Rows[1].StatusText);

                palette.MakeAbsoluteCommand.Execute(null);
                Assert.Equal(Path.GetFullPath(grid), model.Document.Database.BlockTable["grid"].PathName);
                Assert.Equal("Full path", palette.Details.Single(d => d.Key == "Path type").Value);

                palette.Selected = palette.Rows[2];
                palette.IsPreview = true;
                Assert.NotNull(palette.Preview);
                palette.UnloadCommand.Execute(null);
                Assert.Equal("Unloaded", palette.Rows[2].StatusText);

                palette.Selected = palette.Rows[1];
                palette.DetachCommand.Execute(null);
                Assert.Equal(new[] { "plan-level2", "logo" }, palette.Rows.Select(r => r.Name).ToArray());
                Assert.False(model.Document.Database.BlockTable.Has("grid"));
                Assert.Equal("-XREF", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void Xopen_OpensTheDrawingOfTheXrefPicked_InATabOfItsOwn()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out string grid, out _);
                CadDocument host = model.Document;

                model.Execute("XOPEN");
                model.Execute("500,0");

                Assert.NotSame(host, model.Document);
                Assert.Equal(Path.GetFullPath(grid), model.Document.FileName, StringComparer.OrdinalIgnoreCase);
                Assert.Single(model.Document.Database.LayerTable, l => l.Name == "GRID");
            }
        }

        [Fact]
        public void TheAttachDialog_ReadsTheFileChosen_AndOkAttachesItAtThePointTyped_AsOverlay()
        {
            using (var folder = new TestFolder())
            {
                string grid = Grid(folder);
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", CadTemplate.CreateMetric())));
                model.ShowAttachReference = dialog =>
                {
                    Assert.Equal("Attach External Reference", dialog.Title);
                    Assert.True(dialog.IsXref);
                    Assert.False(dialog.CanOk);
                    dialog.FileName = grid;
                    Assert.Equal("grid", dialog.Name);
                    Assert.Equal(".\\xref\\grid.dxf", dialog.SavedPath);
                    Assert.Equal(Path.GetFullPath(grid), dialog.FoundIn);
                    Assert.Equal("Block Unit", dialog.UnitTitle);
                    Assert.Equal("Millimeters", dialog.UnitText);
                    Assert.Equal("1.0000", dialog.UnitSecond);
                    Assert.NotNull(dialog.Preview);
                    dialog.PathType = "Full path";
                    Assert.Equal(Path.GetFullPath(grid), dialog.SavedPath);
                    dialog.IsOverlay = true;
                    dialog.SpecifyPointOnScreen = false;
                    dialog.X = "100";
                    dialog.Y = "200";
                    dialog.ScaleX = "2";
                    Assert.Equal("2", dialog.ScaleY);
                    Assert.True(dialog.CanOk);
                    dialog.Close(CadDialogAction.Ok);
                    return true;
                };

                model.Execute("XA");

                DxfBlockTableRecord block = model.Document.Database.BlockTable["grid"];
                Assert.True(block.IsFromOverlayReference);
                Assert.Equal(Path.GetFullPath(grid), block.PathName);
                DxfBlockReference insert = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfBlockReference>());
                Assert.Equal(new GeoPoint3(100, 200, 0), insert.Position);
                Assert.Equal(2.0, insert.ScaleFactors.X, 12);
                Assert.Equal("XATTACH", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void TheImageDialog_ShowsThePicturesSizeAndResolution()
        {
            using (var folder = new TestFolder())
            {
                string logo = Logo(folder);
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", CadTemplate.CreateMetric())));
                var dialog = new AttachReferenceViewModel(new CadAttachRequest(model.Document, CadAttachKind.Image)) { FileName = logo };

                Assert.Equal("Attach Image", dialog.Title);
                Assert.Equal("Resolution", dialog.UnitTitle);
                Assert.Equal("Scale:", dialog.ScaleLabel);
                Assert.Equal("40 × 30 px · width 1 unit", dialog.UnitSecond);
                Assert.NotNull(dialog.Preview);

                dialog.FileName = Path.Combine(folder.FullPath, "plan-level2.dxf");
                Assert.Equal("Only PNG, JPEG and BMP pictures are attached.", dialog.Error);
                Assert.False(dialog.CanOk);
            }
        }

        [Fact]
        public void ThePaletteAndTheDialog_AreDrawnAsTheDesign_TheCrossOrangeRed_TheOtherKindsGreyed_AndBindGreyed()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder, out _, out _);
                    var palette = new ExternalReferencesWindow(new ExternalReferencesViewModel(model));
                    var dialog = new AttachReferenceWindow(new AttachReferenceViewModel(new CadAttachRequest(model.Document, CadAttachKind.Image)));

                    foreach ((Window window, FrameworkElement body) in new (Window, FrameworkElement)[] { (palette, palette.Body), (dialog, dialog.Body) })
                    {
                        Style title = (Style)window.FindResource("GhTitleCloseButton");
                        object context = window.DataContext;
                        window.Content = null;
                        body.DataContext = context;
                        Layout(body);
                        Button cross = Assert.Single(Descendants(body).OfType<Button>(), b => b.Style == title);
                        Assert.Equal(Color.FromRgb(0xF2, 0x6B, 0x3A), ((SolidColorBrush)Descendants(cross).OfType<IconView>().Single().Stroke).Color);
                        window.Close();
                    }

                    List<string> texts = Descendants(palette.Body).OfType<TextBlock>().Select(t => t.Text).ToList();
                    Assert.Contains("EXTERNAL REFERENCES", texts);
                    Assert.Contains("File References", texts);
                    Assert.Contains("Reference Name", texts);

                    var attach = (ContextMenu)palette.FindResource("AttachMenu");
                    List<MenuItem> items = attach.Items.OfType<MenuItem>().ToList();
                    Assert.Equal(8, items.Count);
                    Assert.Equal(6, items.Count(i => !i.IsEnabled && ((string)i.Header).EndsWith("not offered", StringComparison.Ordinal)));

                    var rowStyle = (Style)Descendants(palette.Body).OfType<ListBox>().Single().ItemContainerStyle;
                    var rowMenu = (ContextMenu)rowStyle.Setters.OfType<Setter>().Single(s => s.Property == FrameworkElement.ContextMenuProperty).Value;
                    Assert.Equal(new[] { "Open", "Attach…", "Unload", "Reload", "Detach", "Bind…" }, rowMenu.Items.OfType<MenuItem>().Select(i => (string)i.Header).ToArray());
                    Assert.False(rowMenu.Items.OfType<MenuItem>().Last().IsEnabled);

                    List<string> dialogTexts = Descendants(dialog.Body).OfType<TextBlock>().Select(t => t.Text).ToList();
                    Assert.Contains("Attach Image", dialogTexts);
                    Assert.Contains("Resolution", dialogTexts);
                }
            });
        }

        private static void Layout(FrameworkElement element)
        {
            element.Measure(new Size(1000, 800));
            element.Arrange(new Rect(0, 0, 1000, 800));
            element.UpdateLayout();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

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
    }
}
