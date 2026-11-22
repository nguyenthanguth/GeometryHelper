using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Insert tab, as the design draws it, found in the window drawn offscreen: after the extensions' tabs and before
    /// Annotate, its buttons starting their commands, its splits showing the choice picked last, the image frame box setting
    /// IMAGEFRAME, the gallery of the drawing's blocks; and Insert, Annotate and View shrinking in the steps of the design's
    /// tables as the window narrows. No dialog is shown: each the workspace would show is stubbed.
    /// </summary>
    public class InsertRibbonTests
    {
        private const string InsertSteps =
            "Linking & Extraction:Medium; Data:Medium; Reference:Medium; Import:Collapsed; Data:Collapsed; Linking & Extraction:Collapsed";
        private const string AnnotateSteps =
            "Annotation Scaling:Medium; Tables:Collapsed; Markup:Collapsed; Annotation Scaling:Collapsed; Leaders:Collapsed; Dimensions:Collapsed";
        private const string ViewSteps =
            "Drafting:Medium; Navigate:Medium; Views:Collapsed; Drafting:Collapsed; Navigate:Collapsed; Named Views:Medium; "
            + "Model Viewports:Medium; Interface:Collapsed";

        [Fact]
        public void InsertComesAfterTheExtensionTabsAndBeforeAnnotate_AndItsButtonsStartTheirCommands()
        {
            OnWorkspace(1600, true, (model, workspace, shown) =>
            {
                ToggleButton home = Descendants(workspace).OfType<ToggleButton>().First(t => Equals(t.Content, "Home"));
                var row = (Panel)VisualTreeHelper.GetParent(home);
                List<UIElement> children = row.Children.Cast<UIElement>().ToList();
                Assert.Equal(
                    new[]
                    {
                        "Home",
                        "Insert",
                        "Annotate",
                        "Parametric",
                        "View",
                        "Manage",
                        "Output",
                        "Plugins",
                        "Settings",
                        "Text Editor",
                        "Table Cell",
                        "Array",
                        "Hatch Creation",
                        "Hatch Editor",
                        "Image",
                        "PDF Underlay",
                        "Block Editor",
                    },
                    children.OfType<ToggleButton>().Select(t => (string)t.Content)
                );
                ToggleButton insertTab = children.OfType<ToggleButton>().Single(t => Equals(t.Content, "Insert"));
                Assert.True(children.IndexOf(children.OfType<ItemsControl>().Single()) < children.IndexOf(insertTab));

                insertTab.Command.Execute(insertTab.CommandParameter);
                Assert.Same(model.InsertTab, model.SelectedTab);
                Layout(workspace, 1600);

                RibbonStrip strip = StripOf(workspace, model.InsertTab.TabContent);
                Assert.Equal(
                    new[] { "Block", "Block Definition", "Reference", "Import", "Data", "Linking & Extraction" },
                    strip.Children.OfType<RibbonPanel>().Select(p => p.Title)
                );

                List<Button> buttons = Descendants(strip)
                    .OfType<Button>()
                    .Where(b => b.CommandParameter is string)
                    .ToList();
                // The six panels of AutoCAD 2021's Insert; the frames and the snap run the setting they show, "IMAGEFRAME 1"
                // and "PDFOSNAP 1" in the room, through StartCommandWithOption.
                string[] commands =
                {
                    "INSERT",
                    "EATTEDIT",
                    "BLOCK",
                    "ATTDEF",
                    "BATTMAN",
                    "BEDIT",
                    "XATTACH",
                    "IMAGECLIP",
                    "PDFADJUST",
                    "PDFLAYERS",
                    "IMAGEFRAME 1",
                    "PDFOSNAP 1",
                    "IMPORT",
                    "TXT2MTXT",
                    "FIELD",
                    "UPDATEFIELD",
                    "INSERTOBJ",
                    "HYPERLINK",
                    "DATALINK",
                    "DATALINKUPDATE U",
                    "DATALINKUPDATE W",
                    "DATAEXTRACTION",
                };
                Assert.Equal(commands, buttons.Select(b => (string)b.CommandParameter));

                // Those whose dialogs this test stubs are pressed; the dialogs of Import and Data (IMPORT, FIELD, INSERTOBJ,
                // HYPERLINK) are not stubbed here, and RibbonInsertPanelsTests, which lets every dialog go, presses them.
                var pressed = new[]
                {
                    "INSERT",
                    "EATTEDIT",
                    "BLOCK",
                    "ATTDEF",
                    "BATTMAN",
                    "BEDIT",
                    "XATTACH",
                    "IMAGECLIP",
                    "PDFADJUST",
                    "PDFLAYERS",
                    "DATALINK",
                    "DATAEXTRACTION",
                };

                foreach (Button button in buttons.Where(b => pressed.Contains((string)b.CommandParameter)))
                {
                    Press(
                        model,
                        button.IsEnabled,
                        () => button.Command.Execute(button.CommandParameter),
                        (string)button.CommandParameter
                    );
                }

                // The launcher of Reference opens External References, which is no button of the panel.
                RibbonPanel reference = strip.Children.OfType<RibbonPanel>().Single(p => p.Title == "Reference");
                reference.Launcher.Execute(null);
                model.Escape();

                // INSERT opened the palette on Current drawing, BLOCK, ATTDEF, BEDIT and XATTACH their dialogs, DATALINK the Data
                // Link Manager, DATAEXTRACTION its wizard and the launcher the palette; BATTMAN found no block with attributes in the
                // room and opened nothing.
                Assert.Equal(
                    new[]
                    {
                        "palette Current drawing",
                        "block",
                        "attdef",
                        "bedit",
                        "attach",
                        "datalinks",
                        "extract",
                        "xref",
                    },
                    shown
                );

                // The buttons of PDF underlays: Adjust, Underlay Layers and Snap to Underlays run PDFADJUST, PDFLAYERS and PDFOSNAP.
                foreach (string name in new[] { "Adjust", "Underlay Layers", "Snap to Underlays ON" })
                {
                    Button underlay = Descendants(strip).OfType<Button>().Single(b => Equals(b.Content, name));
                    Assert.True(underlay.IsEnabled);
                    Assert.DoesNotContain("not offered", (string)underlay.ToolTip, StringComparison.Ordinal);
                }
            });
        }

        [Fact]
        public void TheSplitsOfInsert_ShowAndRunTheChoicePickedLast_NoChoiceIsDrawnGreyed()
        {
            OnWorkspace(1600, true, (model, workspace, shown) =>
            {
                model.SelectedTab = model.InsertTab;
                Layout(workspace, 1600);
                RibbonStrip strip = StripOf(workspace, model.InsertTab.TabContent);

                Button single = Descendants(strip).OfType<Button>().Single(b => "EATTEDIT".Equals(b.CommandParameter));
                List<MenuItem> attributes = MenuOf(SplitArrowOf(single));
                // AutoCAD's two items, Single and Multiple (-ATTEDIT); the label of the button stays Edit Attribute.
                Assert.Equal(new[] { "EATTEDIT", "-ATTEDIT" }, attributes.Select(i => i.CommandParameter as string));
                Assert.All(attributes, i => Assert.True(i.IsEnabled));
                Pick(attributes[1]);
                model.Escape();
                Assert.Equal("-ATTEDIT", single.CommandParameter);
                Press(model, single.IsEnabled, () => single.Command.Execute(single.CommandParameter), "-ATTEDIT");

                Button create = Descendants(strip).OfType<Button>().Single(b => "BLOCK".Equals(b.CommandParameter));
                List<MenuItem> blocks = MenuOf(SplitArrowOf(create));
                Assert.Equal(new[] { "BLOCK", "WBLOCK" }, blocks.Select(i => (string)i.CommandParameter));
                Pick(blocks[1]);
                model.Escape();
                Assert.Equal("WBLOCK", create.CommandParameter);
                Press(model, create.IsEnabled, () => create.Command.Execute(create.CommandParameter), "WBLOCK");

                Button attach = Descendants(strip).OfType<Button>().Single(b => "XATTACH".Equals(b.CommandParameter));
                List<MenuItem> attaches = MenuOf(SplitArrowOf(attach));
                // DXF, Image and PDF; the formats the presenter lacks are no longer drawn greyed.
                Assert.Equal(
                    new[] { "XATTACH", "IMAGEATTACH", "PDFATTACH" },
                    attaches.Where(i => i.IsEnabled).Select(i => (string)i.CommandParameter)
                );
                Assert.All(attaches, i => Assert.True(i.IsEnabled));
                Pick(attaches[1]);
                model.Escape();
                Assert.Equal("IMAGEATTACH", attach.CommandParameter);
                Press(model, attach.IsEnabled, () => attach.Command.Execute(attach.CommandParameter), "IMAGEATTACH");
            });
        }

        [Fact]
        public void TheFramesSplitShowsTheDrawingsImageFrame_AndAChoiceSetsIt_AsImageFrameTyped()
        {
            OnWorkspace(1600, true, (model, workspace, shown) =>
            {
                model.SelectedTab = model.InsertTab;
                Layout(workspace, 1600);
                RibbonStrip strip = StripOf(workspace, model.InsertTab.TabContent);
                Button frames = Descendants(strip)
                    .OfType<Button>()
                    .Single(b =>
                        ((b.CommandParameter as string) ?? string.Empty).StartsWith(
                            "IMAGEFRAME",
                            StringComparison.Ordinal
                        )
                    );
                Assert.True(frames.IsEnabled);
                MenuItem Choice(string header) => MenuOf(SplitArrowOf(frames)).Single(i => (string)i.Header == header);

                // No image yet: 1 shown; a choice gives the drawing its variables of images, as AutoCAD 2021 makes them, and
                // undone takes them out again. The button reads the setting and runs it as it is.
                Assert.Equal("Display and Plot Frames", frames.Content);
                Assert.Equal("IMAGEFRAME 1", frames.CommandParameter);
                Pick(Choice("Hide Frames"));
                Layout(workspace, 1600);
                Assert.Equal(DxfFrameSetting.ImageFrameOff, ImageVariables(model).ImageFrame);
                Assert.Equal("Hide Frames", model.CurrentImageFrame);
                Assert.Equal("Hide Frames", frames.Content);
                Assert.Equal("IMAGEFRAME 0", frames.CommandParameter);
                Assert.True(model.Undo());
                Assert.False(model.Document.Database.NamedObjectsDictionary.Contains("ACAD_IMAGE_VARS"));
                Assert.Equal("Display and Plot Frames", model.CurrentImageFrame);
                Assert.Equal("Display and Plot Frames", frames.Content);

                // An image attached: Hide Frames sets 0, ticked; Display but Not Plot Frames 2.
                string png = Path.Combine(Path.GetDirectoryName(model.Document.FileName), "logo.png");
                WritePng(png);
                model.RunWithAnswers("-IMAGE", "A", png, "10,20", string.Empty, string.Empty);
                Assert.False(model.IsCommandRunning);

                Pick(Choice("Hide Frames"));
                Assert.Contains("Enter new value for IMAGEFRAME <1>: 0", model.History);
                Assert.Equal(0, (int)ImageVariables(model).ImageFrame);
                Assert.Equal(new[] { true, false, false }, model.ImageFrameItems.Select(i => i.IsCurrent));
                Assert.Equal(
                    new[] { "IMAGEFRAME 0", "IMAGEFRAME 1", "IMAGEFRAME 2" },
                    model.ImageFrameItems.Select(i => i.Hint)
                );

                Pick(Choice("Display but Not Plot Frames"));
                Assert.Equal(DxfFrameSetting.ImageFrameOnNoPlot, ImageVariables(model).ImageFrame);

                // Undone, the button reads the drawing again.
                Assert.True(model.Undo());
                Layout(workspace, 1600);
                Assert.Equal("Hide Frames", frames.Content);

                // The snap reads PDFOSNAP the same way: ON in the room, as AutoCAD starts it, OFF once chosen.
                Button snap = Descendants(strip)
                    .OfType<Button>()
                    .Single(b =>
                        ((b.CommandParameter as string) ?? string.Empty).StartsWith(
                            "PDFOSNAP",
                            StringComparison.Ordinal
                        )
                    );
                Assert.Equal("Snap to Underlays ON", snap.Content);
                Pick(MenuOf(SplitArrowOf(snap)).Single(i => (string)i.Header == "Snap to Underlays OFF"));
                Layout(workspace, 1600);
                Assert.False(model.Document.PdfOsnap);
                Assert.Equal("Snap to Underlays OFF", snap.Content);
            });
        }

        [Fact]
        public void TheGalleryListsTheDrawingsBlocksByName_AndATileStartsInsertOfItWithNoPalette()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                var insert = (InsertRibbonViewModel)model.InsertTab.TabContent;
                int palettes = 0;
                model.ShowBlocksPalette = palette => ++palettes > 0;

                insert.IsGalleryOpen = true;
                List<string> before = insert.Gallery.Select(t => t.Name).ToList();

                foreach (string name in new[] { "zeta", "DOOR_900", "beta", "Alpha" })
                {
                    model.Execute("-B");
                    model.Execute(name);
                    model.Execute("0,0");
                    model.Execute(string.Empty);
                }

                Assert.Equal(before, insert.Gallery.Select(t => t.Name));
                insert.IsGalleryOpen = false;
                insert.IsGalleryOpen = true;

                // By name, letters of either case alike, as the Blocks palette lists them; the same again when read again.
                List<string> names = insert.Gallery.Select(t => t.Name).ToList();
                Assert.Equal(
                    new[] { "Alpha", "beta", "DOOR_900", "zeta" },
                    names.Where(n => new[] { "zeta", "DOOR_900", "beta", "Alpha" }.Contains(n))
                );
                Assert.Equal(
                    names
                        .OrderBy(n => n.ToUpperInvariant(), StringComparer.Ordinal)
                        .ThenBy(n => n, StringComparer.Ordinal),
                    names
                );
                Assert.True(insert.HasGallery);
                Assert.Equal("Current drawing · room.dxf", insert.GalleryTitle);
                insert.IsGalleryOpen = false;
                insert.IsGalleryOpen = true;
                Assert.Equal(names, insert.Gallery.Select(t => t.Name));

                int history = model.History.Count;
                insert.InsertBlockCommand.Execute("DOOR_900");

                Assert.False(insert.IsGalleryOpen);
                Assert.Contains("Command: INSERT", model.History.Skip(history));
                Assert.True(model.IsCommandRunning);
                model.Execute("500,500");

                Assert.False(model.IsCommandRunning);
                DxfBlockReference placed = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Last();
                Assert.Equal("DOOR_900", placed.Name);
                Assert.Equal(500.0, placed.Position.X, 9);
                Assert.Equal("DOOR_900", model.RecentBlocks[0]);
                Assert.Equal(0, palettes);
            }
        }

        [Fact]
        public void AnEmptyDrawingsGallery_HasNoTile()
        {
            var model = new MainViewModel();
            model.NewDrawing();
            var insert = (InsertRibbonViewModel)model.InsertTab.TabContent;

            insert.IsGalleryOpen = true;

            Assert.Empty(insert.Gallery);
            Assert.False(insert.HasGallery);
        }

        [Fact]
        public void RecentBlocksAndBlocksFromOtherDrawings_OpenThePaletteOnThoseTabs_AndTheTopOfInsertOnCurrentDrawing()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                var insert = (InsertRibbonViewModel)model.InsertTab.TabContent;
                var tabs = new List<string>();
                model.ShowBlocksPalette = palette =>
                {
                    tabs.Add(palette.Tab);
                    return true;
                };

                insert.OpenPaletteCommand.Execute("Recent");
                insert.OpenPaletteCommand.Execute("Other drawing");
                model.StartCommandCommand.Execute("INSERT");

                Assert.Equal(new[] { "Recent", "Other drawing", "Current drawing" }, tabs);
                Assert.False(model.IsCommandRunning);
            }
        }

        [Fact]
        public void ThePaletteWindowOpensOnTheTabItsModelAsksFor()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        _ = PackUriHelper.UriSchemePack;

                        foreach (string tab in new[] { "Current drawing", "Recent", "Other drawing" })
                        {
                            var palette = new BlocksPaletteViewModel(new CadInsertRequest(model.Document), null)
                            {
                                Tab = tab,
                            };
                            var window = new BlocksPaletteWindow(palette);
                            Assert.Equal(tab, palette.Tab);
                            Assert.Equal(tab == "Recent", window.RecentTab.IsChecked);
                            Assert.Equal(tab == "Other drawing", window.OtherTab.IsChecked);
                            window.Close();
                        }
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

        [Fact]
        public void WithNoDrawingOpen_EveryButtonOfInsertIsOff()
        {
            OnWorkspace(1600, false, (model, workspace, shown) =>
            {
                model.SelectedTab = model.InsertTab;
                Layout(workspace, 1600);
                RibbonStrip strip = StripOf(workspace, model.InsertTab.TabContent);
                List<Button> buttons = Descendants(strip)
                    .OfType<Button>()
                    .Where(b => !(b.TemplatedParent is RibbonPanel))
                    .ToList();

                Assert.Equal(28, buttons.Count);
                Assert.All(buttons, b => Assert.False(b.IsEnabled, b.Content + " is enabled."));
            });
        }

        [Fact]
        public void InsertAtTheWidthsOfTheDesign_TakesTheStepsItLists_AndNoneAt2300()
        {
            Steps(
                model => model.InsertTab,
                InsertSteps,
                new[] { "Block", "Block Definition", "Reference", "Import", "Data", "Linking & Extraction" },
                new[]
                {
                    (2300, 0),
                    (1600, 0),
                    (1280, 0),
                    // Insert as AutoCAD 2021's six panels, each as wide as its content: 1 046 wide from a window 1 070, then the
                    // steps of D1332 at 986, 910, 780, 739 and 730.
                    (1070, 0),
                    (1069, 1),
                    (986, 1),
                    (985, 2),
                    (910, 2),
                    (909, 3),
                    (780, 3),
                    (779, 4),
                    (739, 4),
                    (738, 5),
                    (730, 5),
                    (729, 6),
                    (600, 6),
                }
            );
        }

        [Fact]
        public void AnnotateAtTheWidthsOfTheDesign_TakesTheStepsItLists_AndNoneAt2300()
        {
            Steps(
                model => model.AnnotateTab,
                AnnotateSteps,
                new[] { "Text", "Dimensions", "Centerlines", "Leaders", "Tables", "Markup", "Annotation Scaling" },
                new[]
                {
                    (2300, 0),
                    // Annotate as AutoCAD 2021's seven panels, each as wide as its content: 1 550 wide from a window 1 574, then the
                    // steps of D1332 at 1 479, 1 294, 1 247, 1 206 and 995.
                    (1574, 0),
                    (1573, 1),
                    (1479, 1),
                    (1478, 2),
                    (1294, 2),
                    (1293, 3),
                    (1247, 3),
                    (1246, 4),
                    (1206, 4),
                    (1205, 5),
                    (995, 5),
                    (994, 6),
                    (700, 6),
                }
            );
        }

        [Fact]
        public void ViewAtTheWidthsOfTheDesign_TakesTheStepsItLists_AndNoneAt2300()
        {
            Steps(
                model => model.ViewTab,
                ViewSteps,
                new[]
                {
                    "Viewport Tools",
                    "Named Views",
                    "Model Viewports",
                    "Palettes",
                    "Interface",
                    "Navigate",
                    "Views",
                    "Drafting",
                },
                new[]
                {
                    (2300, 0),
                    (1280, 0),
                    // Wave 4 laid View out as AutoCAD 2021's (D1342, D1343): the tab is 1 123 wide, all full down to a window 1 147, and
                    // takes the eight steps of ViewSteps; Drafting collapsing (61 wide) is 6 wider than Drafting medium (55), so the
                    // fifth step is taken with the fourth.
                    (1147, 0),
                    (1146, 1),
                    (1076, 1),
                    (1075, 2),
                    (972, 2),
                    (971, 3),
                    (946, 3),
                    (945, 5),
                    (914, 5),
                    (913, 6),
                    (874, 6),
                    (873, 7),
                    (830, 7),
                    (829, 8),
                }
            );
        }

        [Fact]
        public void MediumAnnotateDropsTheLabelsOfAnnotationScaling_AndLeavesTheBoxesAt180()
        {
            OnWorkspace(1600, true, (model, workspace, shown) =>
            {
                model.SelectedTab = model.AnnotateTab;
                Layout(workspace, 1600);
                RibbonStrip strip = StripOf(workspace, model.AnnotateTab.TabContent);
                Assert.Equal(0, strip.StepsTaken);
                Assert.All(Descendants(strip).OfType<ComboBox>(), c => Assert.Equal(180.0, c.ActualWidth, 6));
                Button scales = Descendants(strip)
                    .OfType<Button>()
                    .Single(b => "SCALELISTEDIT".Equals(b.CommandParameter));
                Assert.Contains(Descendants(scales).OfType<TextBlock>(), t => t.Text == "Scale List");

                // The first step takes Annotation Scaling to medium: its small buttons keep their names and lose their labels.
                Layout(workspace, 1500);
                Assert.Equal(1, strip.StepsTaken);
                Assert.All(Descendants(strip).OfType<ComboBox>(), c => Assert.Equal(180.0, c.ActualWidth, 6));
                Assert.Equal("Scale List", AutomationProperties.GetName(scales));
                Assert.DoesNotContain(Descendants(scales).OfType<TextBlock>(), t => t.Text == "Scale List");
            });
        }

        /// <summary>A tab laid out at each window width given, the steps it takes and the size of each panel checked against the steps' string.</summary>
        private static void Steps(
            Func<MainViewModel, CadWorkspaceExtension> tab,
            string steps,
            string[] titles,
            (int Window, int Steps)[] widths
        )
        {
            OnWorkspace(2300, true, (model, workspace, shown) =>
            {
                model.SelectedTab = tab(model);
                Layout(workspace, 2300);
                RibbonStrip strip = StripOf(workspace, tab(model).TabContent);
                Assert.Equal(steps, strip.Steps);
                Assert.Equal(titles, strip.Children.OfType<RibbonPanel>().Select(p => p.Title));

                foreach ((int window, int taken) in widths)
                {
                    Layout(workspace, window);
                    Assert.True(
                        taken == strip.StepsTaken,
                        "At " + window + " the strip took " + strip.StepsTaken + " steps, not " + taken + "."
                    );
                    Dictionary<string, RibbonPanelSize> expected = After(steps, taken, titles);
                    Assert.Equal(
                        expected,
                        strip.Children.OfType<RibbonPanel>().ToDictionary(p => p.Title, p => p.Size)
                    );
                    Assert.True(
                        strip.DesiredSize.Width <= window - 24 + 0.01,
                        "At " + window + " the strip is " + strip.DesiredSize.Width + " wide."
                    );
                }
            });
        }

        private static Dictionary<string, RibbonPanelSize> After(string steps, int taken, string[] titles)
        {
            var sizes = titles.ToDictionary(t => t, t => RibbonPanelSize.Full);

            foreach (string step in steps.Split(';').Take(taken))
            {
                string[] parts = step.Split(':');
                sizes[parts[0].Trim()] = (RibbonPanelSize)Enum.Parse(typeof(RibbonPanelSize), parts[1].Trim());
            }

            return sizes;
        }

        /// <summary>
        /// Runs a check on the window drawn offscreen at a width on a thread of its own, a room drawing open or none, every
        /// dialog the Insert tab's commands ask for stubbed, each recorded by a word.
        /// </summary>
        private static void OnWorkspace(
            double width,
            bool open,
            Action<MainViewModel, CadWorkspace, List<string>> check
        )
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
                        _ = PackUriHelper.UriSchemePack;
                        var model = new MainViewModel();
                        Assert.True(!open || model.Open(room));
                        var workspace = new CadWorkspace { DataContext = model };
                        var shown = new List<string>();
                        model.ShowBlocksPalette = palette => Shown(shown, "palette " + palette.Tab);
                        model.ShowBlockDefinition = dialog => Shown(shown, "block");
                        model.ShowWriteBlock = dialog => Shown(shown, "wblock");
                        model.ShowAttributeDefinition = dialog => Shown(shown, "attdef");
                        model.ShowAttributeEditor = dialog => Shown(shown, "eattedit");
                        model.ShowBlockAttributeManager = dialog => Shown(shown, "battman");
                        model.ShowBlockEdit = dialog => Shown(shown, "bedit");
                        model.ShowAttachReference = dialog => Shown(shown, "attach");
                        model.ShowExternalReferences = palette => Shown(shown, "xref");
                        model.ShowDataExtraction = dialog => shown.Add("extract");
                        model.ShowDataLinkManager = dialog => shown.Add("datalinks");

                        try
                        {
                            Layout(workspace, width);
                            check(model, workspace, shown);
                        }
                        finally
                        {
                            workspace.DrawingCanvas.Dispose();
                        }
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

        private static bool Shown(List<string> shown, string what)
        {
            shown.Add(what);
            return true;
        }

        private static DxfRasterVariables ImageVariables(MainViewModel model) =>
            (DxfRasterVariables)model.Document.Database.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS");

        /// <summary>A picture 40 by 30 pixels, grey, written as a PNG.</summary>
        private static void WritePng(string path)
        {
            var pixels = Enumerable.Repeat((byte)128, 40 * 30 * 4).ToArray();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(
                BitmapFrame.Create(BitmapSource.Create(40, 30, 96, 96, PixelFormats.Bgra32, null, pixels, 40 * 4))
            );

            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 960));
            element.Arrange(new Rect(0, 0, width, 960));
            element.UpdateLayout();
        }

        private static RibbonStrip StripOf(CadWorkspace workspace, object content) =>
            Descendants(workspace).OfType<RibbonStrip>().Single(s => ReferenceEquals(s.DataContext, content));

        /// <summary>The arrow beside the button of a split, which drops its choices.</summary>
        private static Button SplitArrowOf(Button main) =>
            (Button)((Panel)VisualTreeHelper.GetParent(main)).Children[1];

        /// <summary>The items of a button's menu, the menu taking the window's model from the button, its items bound at the dispatcher's next turn.</summary>
        private static List<MenuItem> MenuOf(Button opener)
        {
            ContextMenu menu = opener.ContextMenu;
            menu.PlacementTarget = opener;
            opener.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            return menu.Items.OfType<MenuItem>().ToList();
        }

        /// <summary>An item clicked as the mouse clicks it, its Click raised and its command run.</summary>
        private static void Pick(MenuItem item)
        {
            Assert.True(item.IsEnabled, item.Header + " is not enabled.");
            MethodInfo click = typeof(MenuItem).GetMethod(
                "OnClick",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );
            click.Invoke(item, null);
            item.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        /// <summary>A button enabled, pressed, and the command it names started; then Esc.</summary>
        private static void Press(MainViewModel model, bool enabled, Action press, string command)
        {
            Assert.True(enabled, command + " is not enabled.");
            int before = model.History.Count;
            press();
            Assert.Contains("Command: " + command, model.History.Skip(before));
            Assert.DoesNotContain(model.History.Skip(before), l =>
                l.StartsWith("Unknown command", StringComparison.Ordinal)
            );
            model.Escape();
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
