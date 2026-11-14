using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The dialogs of rounds 5 to 9 in the app's screenshot mode (<c>--dialog</c>): each made over a sample drawing as the
    /// mode makes it, laid out in its window, with no control clipped or empty, and the picture written by the app itself.
    /// </summary>
    public class AppDialogsTests
    {
        /// <summary>
        /// A drawing of walls and notes with a text that misspells two words, three annotative texts at 1:20, 1:50 and 1:100, a
        /// picture, two named views and a viewport on its Layout1; its reference, grid.dxf, is written beside it.
        /// </summary>
        private static string Sample(TestFolder folder)
        {
            DxfDatabase grid = CadTemplate.CreateMetric();
            grid.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID" });
            grid.LayerTable.Add(new DxfLayerTableRecord { Name = "AXIS" });
            grid.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(3000, 0, 0)) { Layer = "GRID" }
            );
            folder.Write("grid.dxf", grid);

            DxfDatabase db = CadTemplate.CreateMetric();
            db.ViewTable.Add(
                new DxfViewTableRecord
                {
                    Name = "ENTRANCE",
                    CenterPoint = new GeoPoint2(1000, 500),
                    Height = 1200,
                    Width = 2000,
                }
            );

            foreach (string name in new[] { "WALLS", "DOORS", "TEXT" })
            {
                db.LayerTable.Add(new DxfLayerTableRecord { Name = name });
            }

            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(6000, 0, 0)) { Layer = "WALLS" }
            );
            db.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(500, 3400, 0),
                    TextString = "Teh plate is fixd to the grating",
                    Height = 120,
                    Layer = "TEXT",
                }
            );
            DxfObjectContextCollection list = db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");

            foreach (string name in new[] { "1:1", "1:5", "1:10", "1:20", "1:50", "1:100" })
            {
                if (list.GetContext(name) == null)
                {
                    list.AddContext(
                        new DxfAnnotationScale
                        {
                            Name = name,
                            PaperUnits = 1,
                            DrawingUnits = double.Parse(name.Substring(2)),
                        }
                    );
                }
            }

            var shown = new[] { new[] { "1:20", "1:50" }, new[] { "1:50" }, new[] { "1:50", "1:100" } };

            for (int i = 0; i < 3; i++)
            {
                var text = new DxfDBText
                {
                    Position = new GeoPoint3(500, 2400 - 300 * i, 0),
                    TextString = "NOTE " + "ABC"[i],
                    Height = 2.5,
                };
                db.ModelSpace.AppendEntity(text);
                db.Cannoscale = shown[i][0];
                text.Annotative = DxfAnnotativeStates.True;

                foreach (string name in shown[i].Skip(1))
                {
                    text.AddContext((DxfAnnotationScale)list.GetContext(name));
                }
            }

            db.Cannoscale = "1:1";
            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(db);
            var definition = new DxfRasterImageDef("logo.png", new GeoVector2(200, 150));
            images.SetAt("logo", definition);
            var image = new DxfRasterImage(definition);
            image.SetOrientation(new GeoPoint3(4200, 2600, 0), new GeoVector3(1200, 0, 0), new GeoVector3(0, 900, 0));
            db.ModelSpace.AppendEntity(image);

            DxfLayout layout = db.Layouts.First(l => l.LayoutName == "Layout1");
            layout.Initialize();
            var frame = new DxfViewport
            {
                CenterPoint = new GeoPoint3(150, 100, 0),
                Width = 200,
                Height = 150,
                ViewHeight = 150 * 40.0,
                ViewCenter = new GeoPoint2(3000, 2000),
            };
            layout.BlockTableRecord.AppendEntity(frame);
            frame.On = true;
            return folder.Write("sample.dxf", db);
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;
                    test();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject deeper in Descendants(child))
                {
                    yield return deeper;
                }
            }
        }

        /// <summary>Makes a dialog as the screenshot mode does and lays its content out at the size of its window.</summary>
        private static FrameworkElement Made(
            TestFolder folder,
            string name,
            string run,
            out Window window,
            out double needed
        )
        {
            var model = new MainViewModel();
            Assert.True(model.Open(Sample(folder)), name);
            Assert.True(CadAppDialogs.Knows(name), name);
            Assert.True(CadAppDialogs.TryCreate(model, name, ref run, out window, out FrameworkElement root), name);
            return LaidOut(window, root, out needed);
        }

        /// <summary>Takes the content of a dialog's window out as the screenshot mode does, and lays it out at the window's size.</summary>
        private static FrameworkElement LaidOut(Window window, FrameworkElement root, out double needed)
        {
            CadAppHost.TakeOut(window, root);
            root.DataContext = window.DataContext;

            // What the content needs of a window as wide as this one, with all the height it asks for.
            root.Measure(new Size(window.Width, double.PositiveInfinity));
            needed = root.DesiredSize.Height;
            root.Width = window.Width;
            root.Height = window.Height;
            root.Measure(new Size(window.Width, window.Height));
            root.Arrange(new Rect(0, 0, window.Width, window.Height));
            root.UpdateLayout();
            return root;
        }

        private static string RunFor(string name, TestFolder folder) =>
            name == "xbind" || name == "rename"
                ? "-XREF;A;" + Path.Combine(folder.FullPath, "grid.dxf") + ";0,0;;;"
                : null;

        [Fact]
        public void EveryDialog_IsMadeOverTheSampleDrawing_AndDrawnInItsWindowWithNothingClipped()
        {
            using (var folder = new TestFolder())
            {
                var clipped = new List<string>();
                OnSta(() =>
                {
                    foreach (string name in CadAppDialogs.Names)
                    {
                        FrameworkElement root = Made(
                            folder,
                            name,
                            RunFor(name, folder),
                            out Window window,
                            out double needed
                        );
                        Assert.True(root.ActualWidth > 0 && root.ActualHeight > 0, name);

                        if (needed > window.Height + 0.5)
                        {
                            clipped.Add(name + " needs " + Math.Ceiling(needed) + " of " + window.Height);
                        }

                        window.Close();
                    }
                });

                Assert.Empty(clipped);
            }
        }

        [Fact]
        public void EveryDialog_ShowsTheWordsOfItsCheckBoxes_ItsEditableBoxesAndItsIcons()
        {
            using (var folder = new TestFolder())
            {
                var empty = new List<string>();
                OnSta(() =>
                {
                    foreach (string name in CadAppDialogs.Names)
                    {
                        FrameworkElement root = Made(folder, name, RunFor(name, folder), out Window window, out _);
                        empty.AddRange(Emptied(name, root));
                        empty.AddRange(WhiteLists(name, root));
                        window.Close();
                    }
                });

                Assert.Empty(empty);
            }
        }

        /// <summary>What a laid out dialog shows empty: a check box with no words, an editable box with no place to type, an icon cut.</summary>
        private static List<string> Emptied(string name, FrameworkElement root)
        {
            var empty = new List<string>();

            foreach (DependencyObject child in Descendants(root))
            {
                if (child is CheckBox box && box.Content != null && box.ActualWidth > 0)
                {
                    ContentPresenter words = Descendants(box).OfType<ContentPresenter>().FirstOrDefault();

                    if (words == null || words.ActualWidth < 2)
                    {
                        empty.Add(name + ": the check box " + box.Content + " shows no words");
                    }
                }
                else if (child is ComboBox combo && combo.IsEditable && combo.ActualWidth > 0)
                {
                    var typed = combo.Template.FindName("PART_EditableTextBox", combo) as TextBox;

                    if (typed == null || typed.Visibility != Visibility.Visible)
                    {
                        empty.Add(name + ": the editable box " + combo.Text + " has no place to type in");
                    }
                    else if (typed.Text != combo.Text)
                    {
                        empty.Add(name + ": the editable box shows " + typed.Text + " for " + combo.Text);
                    }
                }
                else if (child is IconView icon && icon.ActualWidth > 0 && icon.ActualWidth < icon.Size - 0.5)
                {
                    empty.Add(name + ": an icon " + icon.Size + " wide is given " + icon.ActualWidth);
                }
            }

            return empty;
        }

        /// <summary>
        /// The lists of a laid out dialog that are drawn white at their middle: a disabled list of the system is white, so a
        /// list that is off in a dark dialog shows as a white box.
        /// </summary>
        private static List<string> WhiteLists(string name, FrameworkElement root)
        {
            var white = new List<string>();
            List<ListBox> lists = Descendants(root)
                .OfType<ListBox>()
                .Where(l => l.ActualWidth > 8 && l.ActualHeight > 8)
                .ToList();

            if (lists.Count == 0)
            {
                return white;
            }

            int width = (int)Math.Ceiling(root.ActualWidth);
            int height = (int)Math.Ceiling(root.ActualHeight);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);

            foreach (ListBox list in lists)
            {
                Rect box = list.TransformToAncestor(root)
                    .TransformBounds(new Rect(0, 0, list.ActualWidth, list.ActualHeight));
                var pixel = new byte[4];
                bitmap.CopyPixels(
                    new Int32Rect((int)(box.Left + box.Width / 2), (int)(box.Top + box.Height / 2), 1, 1),
                    pixel,
                    4,
                    0
                );

                if (Math.Min(pixel[0], Math.Min(pixel[1], pixel[2])) > 200)
                {
                    white.Add(
                        name
                            + ": the list "
                            + System.Windows.Automation.AutomationProperties.GetName(list)
                            + " is white"
                    );
                }
            }

            return white;
        }

        /// <summary>
        /// What a laid out dialog draws with the pale scroll bar of the system: the pixels of each scroll bar showing that are
        /// nearly white, which the dark bar of the theme has none of.
        /// </summary>
        private static List<string> PaleScrollBars(string name, FrameworkElement root, ref int seen)
        {
            var pale = new List<string>();
            List<ScrollBar> bars = Descendants(root)
                .OfType<ScrollBar>()
                .Where(b => b.ActualWidth > 0 && b.ActualHeight > 0)
                .ToList();

            if (bars.Count == 0)
            {
                return pale;
            }

            int width = (int)Math.Ceiling(root.ActualWidth);
            int height = (int)Math.Ceiling(root.ActualHeight);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);

            foreach (ScrollBar bar in bars)
            {
                seen++;
                Rect box = bar.TransformToAncestor(root)
                    .TransformBounds(new Rect(0, 0, bar.ActualWidth, bar.ActualHeight));
                int count = 0;

                for (int y = (int)box.Top; y < Math.Min(height, (int)Math.Ceiling(box.Bottom)); y++)
                {
                    for (int x = (int)box.Left; x < Math.Min(width, (int)Math.Ceiling(box.Right)); x++)
                    {
                        int i = (y * width + x) * 4;

                        if (Math.Min(pixels[i], Math.Min(pixels[i + 1], pixels[i + 2])) > 200)
                        {
                            count++;
                        }
                    }
                }

                if (count > 0)
                {
                    pale.Add(
                        name
                            + ": a scroll bar "
                            + (int)box.Width
                            + " by "
                            + (int)box.Height
                            + " has "
                            + count
                            + " pale pixels"
                    );
                }
            }

            return pale;
        }

        [Fact]
        public void TheListsOfTheDialogs_ScrollWithTheDarkBarOfTheTheme_NotThePaleOneOfTheSystem()
        {
            using (var folder = new TestFolder())
            {
                var pale = new List<string>();
                var withBars = new List<string>();
                OnSta(() =>
                {
                    foreach (string name in CadAppDialogs.Names)
                    {
                        FrameworkElement root = Made(folder, name, RunFor(name, folder), out Window window, out _);
                        int seen = 0;
                        pale.AddRange(PaleScrollBars(name, root, ref seen));

                        if (seen > 0)
                        {
                            withBars.Add(name);
                        }

                        window.Close();
                    }
                });

                // Rename's list of kinds and Lineweight Settings' list of weights are longer than their boxes.
                Assert.Contains("rename", withBars);
                Assert.Contains("lweight", withBars);
                Assert.Empty(pale);
            }
        }

        [Fact]
        public void TheOldDialogs_FindAttachAndNewLayerState_AreMadeWithTheirRowsFilled()
        {
            using (var folder = new TestFolder())
            {
                OnSta(() =>
                {
                    FrameworkElement findRoot = Made(folder, "find", null, out Window findWindow, out _);
                    var finding = (FindReplaceViewModel)findWindow.DataContext;
                    Assert.Equal("plate", finding.FindWhat);
                    Assert.Single(finding.Results);

                    // The results list is on the dark of the theme, its header and its row picked, not the white of the system.
                    ListView results = Descendants(findRoot).OfType<ListView>().First();
                    Rect over = results.TransformToAncestor(findRoot).TransformBounds(new Rect(0, 0, 4, 4));
                    var bitmap = new RenderTargetBitmap(
                        (int)findRoot.ActualWidth,
                        (int)findRoot.ActualHeight,
                        96,
                        96,
                        PixelFormats.Pbgra32
                    );
                    bitmap.Render(findRoot);
                    var header = new byte[4];
                    bitmap.CopyPixels(new Int32Rect((int)over.Left + 1, (int)over.Top + 1, 1, 1), header, 4, 0);
                    Assert.True(
                        header[0] < 100 && header[1] < 100 && header[2] < 100,
                        "The header of the results is " + header[2] + "," + header[1] + "," + header[0] + "."
                    );
                    var row = new byte[4];
                    bitmap.CopyPixels(new Int32Rect((int)over.Left + 1, (int)over.Top + 30, 1, 1), row, 4, 0);
                    Assert.True(
                        row[0] < 160 && row[1] < 160 && row[2] < 160,
                        "The row picked is " + row[2] + "," + row[1] + "," + row[0] + "."
                    );

                    Made(folder, "attach", null, out Window attachWindow, out _);
                    var attaching = (AttachReferenceViewModel)attachWindow.DataContext;
                    Assert.Equal("grid", attaching.Name);
                    Assert.True(attaching.CanOk);

                    FrameworkElement stateRoot = Made(folder, "layerstate-new", null, out Window stateWindow, out _);
                    var naming = (NewLayerStateViewModel)stateWindow.DataContext;
                    Assert.Equal("Layer state 1", naming.Name);
                    Assert.NotEmpty(naming.Description);

                    // The description has the room left under its label, not the one line of its words.
                    TextBox description = Descendants(stateRoot)
                        .OfType<TextBox>()
                        .First(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Description");
                    Assert.True(
                        description.ActualHeight > 60,
                        "The description is " + description.ActualHeight + " high."
                    );
                });
            }
        }

        [Fact]
        public void DraftingSettings_OnEachOfItsTabs_ShowsTheWordsOfItsCheckBoxes_ItsEditableBoxesAndItsIcons()
        {
            using (var folder = new TestFolder())
            {
                var empty = new List<string>();
                var clipped = new List<string>();
                OnSta(() =>
                {
                    var model = new MainViewModel();
                    Assert.True(model.Open(Sample(folder)));

                    foreach (string tab in model.CreateDraftingSettings().Tabs)
                    {
                        var window = new DraftingSettingsWindow(model.CreateDraftingSettings(tab));
                        FrameworkElement root = LaidOut(window, window.Body, out double needed);
                        empty.AddRange(Emptied("dsettings:" + tab, root));
                        empty.AddRange(WhiteLists("dsettings:" + tab, root));

                        if (needed > window.Height + 0.5)
                        {
                            clipped.Add(tab + " needs " + Math.Ceiling(needed) + " of " + window.Height);
                        }

                        window.Close();
                    }
                });

                Assert.Empty(empty);
                Assert.Empty(clipped);
            }
        }

        [Fact]
        public void NamedViewports_RenameBox_ShowsASixteenCharacterNameInFull()
        {
            using (var folder = new TestFolder())
            {
                double extent = 0;
                double room = 0;
                string text = null;
                OnSta(() =>
                {
                    FrameworkElement root = Made(folder, "viewports-named", null, out Window window, out _);
                    TextBox box = Descendants(root)
                        .OfType<TextBox>()
                        .First(t =>
                            System.Windows.Automation.AutomationProperties.GetName(t) == "Name" && t.ActualWidth > 0
                        );
                    text = box.Text;
                    var host = (ScrollViewer)box.Template.FindName("PART_ContentHost", box);
                    extent = host.ExtentWidth;
                    room = host.ViewportWidth;
                    window.Close();
                });

                Assert.Equal("Plan and section", text);
                Assert.True(extent <= room + 0.5, "The name is " + extent + " wide in a box with " + room + ".");
            }
        }

        [Fact]
        public void TemplateOptions_GivesTheDescriptionTheRoomLeft_AtItsTop()
        {
            using (var folder = new TestFolder())
            {
                double height = 0;
                double textTop = 0;
                OnSta(() =>
                {
                    FrameworkElement root = Made(folder, "templateoptions", null, out Window window, out _);
                    TextBox description = Descendants(root)
                        .OfType<TextBox>()
                        .First(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Description");
                    height = description.ActualHeight;
                    var host = (ScrollViewer)description.Template.FindName("PART_ContentHost", description);
                    textTop = host.TranslatePoint(new Point(0, 0), description).Y;
                    window.Close();
                });

                Assert.True(height > 120, "The description is " + height + " high.");
                Assert.True(textTop < 10, "Its words start " + textTop + " down.");
            }
        }

        [Fact]
        public void TheSampleStates_ShowWhatTheirDialogsAreFor()
        {
            using (var folder = new TestFolder())
            {
                OnSta(() =>
                {
                    Made(folder, "mlstyle", null, out Window listWindow, out _);
                    var styles = (MlineStyleViewModel)listWindow.DataContext;
                    Assert.Equal(new[] { "STANDARD", "WALL-240" }, styles.Names);
                    Assert.Equal("WALL-240", styles.Selected);

                    Made(folder, "mlstyle-modify", null, out Window pageWindow, out _);
                    var editing = (MlineStyleEditViewModel)pageWindow.DataContext;
                    Assert.Equal("Modify Multiline Style: WALL-240", editing.Title);
                    Assert.Equal(new[] { "120", "0", "-120" }, editing.Elements.Select(e => e.OffsetText));

                    Made(folder, "mlstyle-new", null, out Window nameWindow, out _);
                    Assert.Equal("Partition", ((MlineStyleNewViewModel)nameWindow.DataContext).Name);

                    Made(folder, "mlstyle-load", null, out Window loadWindow, out _);
                    Assert.Equal(new[] { "WALL-240" }, ((MlineStyleLoadViewModel)loadWindow.DataContext).Names);

                    Made(folder, "spell", null, out Window spellWindow, out _);
                    var spelling = (CheckSpellingViewModel)spellWindow.DataContext;
                    Assert.Equal("Teh", spelling.Word);
                    Assert.Contains("The", spelling.Suggestions);

                    Made(folder, "objectscale", null, out Window scaleWindow, out _);
                    var scales = (ObjectScaleViewModel)scaleWindow.DataContext;
                    Assert.Equal(3, scales.ObjectCount);
                    Assert.Equal(new[] { "1:20", "1:50", "1:100" }, scales.Scales.Select(s => s.Name));

                    Made(folder, "objectscale-add", null, out Window addWindow, out _);
                    var picker = (ObjectScaleAddViewModel)addWindow.DataContext;
                    Assert.Equal(new[] { "1:1", "1:5", "1:10" }, picker.Scales.Select(s => s.Name));
                    Assert.Equal(new[] { "1:1", "1:5" }, picker.PickedNames);

                    Made(folder, "imageadjust", null, out Window imageWindow, out _);
                    Assert.Contains("1 image", ((ImageAdjustViewModel)imageWindow.DataContext).Caption);

                    Made(folder, "xbind", RunFor("xbind", folder), out Window bindWindow, out _);
                    var binding = (XbindViewModel)bindWindow.DataContext;
                    Assert.Equal(new[] { "grid|AXIS", "grid|GRID" }, binding.ToBind.Select(n => n.Name));

                    Made(folder, "viewports-named", null, out Window portsWindow, out _);
                    var ports = (ViewportsViewModel)portsWindow.DataContext;
                    Assert.True(ports.IsNamedTab);
                    Assert.Equal(new[] { "Plan and section", "Quad" }, ports.Names);

                    Made(folder, "filter", null, out Window filterWindow, out _);
                    Assert.Equal(2, ((SelectionFiltersViewModel)filterWindow.DataContext).Lines.Count);

                    Made(folder, "rename", RunFor("rename", folder), out Window renameWindow, out _);
                    var renaming = (RenameViewModel)renameWindow.DataContext;
                    Assert.Equal("Layers", renaming.Kind);
                    Assert.Single(renaming.Renames);
                });
            }
        }

        [Fact]
        public void TheAppsScreenshot_DrawsAnyOfThem_AndRefusesWhereTheDrawingLacksWhatItIsFor()
        {
            using (var folder = new TestFolder())
            {
                string sample = Sample(folder);
                string png = Path.Combine(folder.FullPath, "mlstyle.png");

                (int code, string output) = AppHostTests.RunApp("--screenshot", png, sample, "--dialog", "mlstyle");
                Assert.Equal(0, code);
                Assert.Contains("(640 by 520)", output);
                Assert.True(new FileInfo(png).Length > 4000);

                // A drawing with no picture has none to adjust; with no drawing at all, nothing to take the dialog from.
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                (int none, string noImage) = AppHostTests.RunApp(
                    "--screenshot",
                    Path.Combine(folder.FullPath, "none.png"),
                    room,
                    "--dialog",
                    "imageadjust"
                );
                Assert.Equal(2, none);
                Assert.Contains("--dialog imageadjust: the space shown has no image", noImage);
                Assert.False(File.Exists(Path.Combine(folder.FullPath, "none.png")));

                (int blank, string noDrawing) = AppHostTests.RunApp(
                    "--screenshot",
                    Path.Combine(folder.FullPath, "blank.png"),
                    "--dialog",
                    "rename"
                );
                Assert.Equal(2, blank);
                Assert.Contains("--dialog rename: needs a drawing.", noDrawing);
            }
        }
    }
}
