using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.RibbonTabHarness;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Annotate as AutoCAD 2021's, found in the window drawn offscreen: its seven panels in AutoCAD's order (Text, Dimensions,
    /// Centerlines, Leaders, Tables, Markup, Annotation Scaling), each panel's controls in order, the split buttons and what their
    /// menus hold, the slide-outs of Text and Dimensions, the launchers, the Find box and the Text Layer combo, the Multileader
    /// style box of a drawing with no style, and the sizes the panels take as the strip narrows. The helpers are
    /// RibbonTabHarness's.
    /// </summary>
    public sealed class RibbonAnnotatePanelsTests
    {
        private static readonly string[] AnnotateOrder =
        {
            "Text",
            "Dimensions",
            "Centerlines",
            "Leaders",
            "Tables",
            "Markup",
            "Annotation Scaling",
        };

        // D1332, one panel and one size per step. Text and Centerlines never shrink.
        private const string AnnotateSteps =
            "Annotation Scaling:Medium; Tables:Collapsed; Markup:Collapsed; Annotation Scaling:Collapsed; Leaders:Collapsed; Dimensions:Collapsed";

        private static CadWorkspaceExtension AnnotateTab(MainViewModel model) => model.AnnotateTab;

        #region The panels and their order

        [Fact]
        public void AnnotateShowsSevenPanelsInAutoCadsOrder_TextDimensionsCenterlinesLeadersTablesMarkupAndAnnotationScaling()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                List<RibbonPanel> panels = Panels(strip);

                Assert.Equal(AnnotateOrder, panels.Select(p => p.Title));
                Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
                Assert.Equal(0, strip.StepsTaken);
            });
        }

        [Fact]
        public void TheStepsOfAnnotateAreAutoCads_AnnotationScalingMediumThenTablesMarkupScalingLeadersAndDimensionsCollapsed_TextAndCenterlinesNeverShrinking()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                Assert.Equal(Squeeze(AnnotateSteps), Squeeze(strip.Steps));
                AssertStepsInOrder(strip, AnnotateSteps, "Text", "Centerlines");
            });
        }

        [Fact]
        public void TheTitlesOfTextAndDimensionsReadADownArrow_TheOtherAnnotatePanelsPlain()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                foreach (string title in new[] { "Text", "Dimensions" })
                {
                    RibbonPanel panel = Panel(strip, title);

                    Assert.NotNull(panel.SlideOut);
                    ToggleButton toggle = Assert.Single(Descendants(panel).OfType<ToggleButton>(), t =>
                        Equals(t.Content, title + " ▾")
                    );
                    Assert.Same(toggle, panel.Header);
                }

                foreach (string title in new[] { "Centerlines", "Leaders", "Tables", "Markup", "Annotation Scaling" })
                {
                    RibbonPanel panel = Panel(strip, title);

                    Assert.Null(panel.SlideOut);
                    Assert.Null(panel.Header);
                    Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == title);
                }
            });
        }

        [Fact]
        public void TheLaunchersOfTextDimensionsLeadersAndTables_StartStyleDimstyleMleaderstyleAndTablestyle()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                Assert.All(
                    Panels(strip)
                        .Where(p => p.Title == "Centerlines" || p.Title == "Markup" || p.Title == "Annotation Scaling"),
                    p => Assert.Null(p.Launcher)
                );

                foreach (
                    (string title, string command) in new[]
                    {
                        ("Text", "STYLE"),
                        ("Dimensions", "DIMSTYLE"),
                        ("Leaders", "MLEADERSTYLE"),
                        ("Tables", "TABLESTYLE"),
                    }
                )
                {
                    RibbonPanel panel = Panel(strip, title);

                    Assert.NotNull(panel.Launcher);
                    Assert.Contains(command, Assert.IsType<string>(panel.LauncherToolTip));

                    int before = model.History.Count;
                    Assert.True(panel.Launcher.CanExecute(null));
                    panel.Launcher.Execute(null);

                    Assert.Contains("Command: " + command, model.History.Skip(before));
                    model.Escape();
                }
            });
        }

        [Fact]
        public void EveryButtonOfAnnotateStartsItsCommand_ThroughStartCommandOrStartCommandWithOption()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                SortedSet<string> words = AssertEveryButtonStartsItsCommand(model, strip);

                // One from each new panel, so that an empty panel does not pass.
                Assert.Contains("CENTERMARK", words);
                Assert.Contains("TABLE", words);
                Assert.Contains("WIPEOUT", words);
                Assert.Contains("AIOBJECTSCALEADD", words);
                Assert.Contains("DIMTEDIT", words);
            });
        }

        #endregion

        #region Text

        [Fact]
        public void TheBodyOfTextIsMultilineTextSplit_ThenCheckSpellingTextAlignAndJustifyAsTiles_ThenStyleFindAndHeight()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel text = Panel(strip, "Text");
                List<ButtonBase> buttons = BodyControls(text)
                    .OfType<ButtonBase>()
                    .Where(b => !Compact(Words(b)).Contains("find"))
                    .ToList();

                AssertControls(
                    new[]
                    {
                        "Multiline Text|MTEXT",
                        "Check Spelling|SPELL",
                        "Text Align|TEXTALIGN",
                        "Justify|JUSTIFYTEXT",
                    },
                    buttons.Cast<FrameworkElement>().ToList()
                );
                Assert.Equal(LargeHeight, LargeHeightOf(buttons[0]), 0.5);
                Assert.NotNull(SplitArrowOf(buttons[0]));
                AssertMenu(
                    model,
                    SplitArrowOf(buttons[0]),
                    new[] { new[] { "Multiline Text", "Single Line" } },
                    null,
                    new[] { "MTEXT", "TEXT" }
                );

                // The three tiles stand in one column, 24 by 22, 24 apart, with no text.
                for (int i = 1; i < 4; i++)
                {
                    Assert.False(HasText(buttons[i]), NameOf(buttons[i]) + " shows text.");
                    Assert.Equal(TileWidth, buttons[i].ActualWidth, 0.5);
                    Assert.Equal(TileHeight, buttons[i].ActualHeight, 0.5);
                    Assert.Equal(Top(buttons[1], text.Body) + ((i - 1) * Pitch), Top(buttons[i], text.Body), 0.5);
                }

                // Then a column of the Text Style combo, the Find box and the Text Height box, 22 high, 24 apart.
                List<FrameworkElement> inputs = Descendants(text.Body)
                    .OfType<FrameworkElement>()
                    .Where(e => (e is ComboBox || e is TextBox) && !InsideCompound(e))
                    .OrderBy(e => Top(e, text.Body))
                    .ToList();

                Assert.Equal(3, inputs.Count);
                Assert.IsType<ComboBox>(inputs[0]);
                Assert.Contains("tyle", Words(inputs[0]));
                Assert.IsType<TextBox>(inputs[1]);
                Assert.Contains("ind", Words(inputs[1]));
                Assert.Contains("eight", Words(inputs[2]));

                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(TileHeight, inputs[i].ActualHeight, 0.5);
                    Assert.Equal(Top(inputs[0], text.Body) + (i * Pitch), Top(inputs[i], text.Body), 0.5);
                }
            });
        }

        [Fact]
        public void TypingWordsInTheFindTextBoxAndEnter_StartsFindWithThemInFindWhat()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel text = Panel(strip, "Text");
                TextBox box = Assert.Single(Descendants(text.Body).OfType<TextBox>(), b =>
                    !InsideCompound(b) && Words(b).Contains("ind")
                );
                FindReplaceViewModel finding = null;
                model.ShowFindReplace = dialog => finding = dialog;
                int before = model.History.Count;

                using (
                    var source = new HwndSource(
                        new HwndSourceParameters("RibbonAnnotatePanelsTests")
                        {
                            ParentWindow = new IntPtr(-3),
                            WindowStyle = 0,
                            Width = 2200,
                            Height = 960,
                        }
                    )
                    {
                        RootVisual = workspace,
                    }
                )
                {
                    Layout(workspace, 2200);
                    box.Focus();
                    Keyboard.Focus(box);
                    box.Text = "door frame";

                    // The key as the keyboard sends it: tunnelled first, then bubbled where no one has taken it.
                    var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent,
                    };
                    box.RaiseEvent(preview);

                    if (!preview.Handled)
                    {
                        box.RaiseEvent(
                            new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
                            {
                                RoutedEvent = Keyboard.KeyDownEvent,
                            }
                        );
                    }
                }

                Assert.Contains("Command: FIND", model.History.Skip(before));
                Assert.NotNull(finding);
                Assert.Equal("door frame", finding.FindWhat);
            });
        }

        [Fact]
        public void TheSlideOutOfTextHoldsScaleWithText_AndATextLayerComboOfUseCurrentThenTheLayers()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel text = Panel(strip, "Text");

                AssertSlideOut(text, new[] { new[] { "Scale|SCALETEXT" }, new[] { "combo" } });
                ComboBox layers = Assert.Single(SlideOutControls(text).OfType<ComboBox>());
                Assert.Contains("ayer", Words(layers));
                Assert.True(HasText(SlideOutControls(text).OfType<ButtonBase>().Single()));

                // "Use Current", then every layer of the drawing.
                List<string> items = ItemTexts(layers);
                Assert.Equal("usecurrent", Compact(items[0]));
                Assert.Equal(
                    model.Document.Database.LayerTable.Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal),
                    items.Skip(1).OrderBy(n => n, StringComparer.Ordinal)
                );
            });
        }

        // TEXT is run through the session, with the point, 2.5 high, 0 turned and "Hi" (the room has WALLS current and COLUMNS).
        [Fact]
        public void PickingALayerInTheTextLayerCombo_PutsTheNextSingleLineTextOnIt()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel text = Panel(strip, "Text");
                OpenSlideOut(text);
                ComboBox layers = Assert.Single(SlideOutControls(text).OfType<ComboBox>());
                Assert.Equal("WALLS", model.Document.Database.Clayer);

                layers.SelectedItem = layers.Items.Cast<object>().Single(i => ItemText(i) == "COLUMNS");
                Pump(layers);
                model.Execute("TEXTLAYER");
                Assert.Equal("Enter new value for TEXTLAYER, or . for use current <\"COLUMNS\">:", model.Prompt);
                model.Escape();

                WriteText(model, 0);

                // On COLUMNS, and the current layer is where it was.
                Assert.Equal("COLUMNS", model.Document.Database.ModelSpace.OfType<DxfDBText>().Last().Layer);
                Assert.Equal("WALLS", model.Document.Database.Clayer);

                // Use Current puts it back: the next text is on the current layer.
                layers.SelectedItem = layers.Items.Cast<object>().First(i => Compact(ItemText(i)) == "usecurrent");
                Pump(layers);
                WriteText(model, 100);

                Assert.Equal(2, model.Document.Database.ModelSpace.OfType<DxfDBText>().Count());
                Assert.Equal("WALLS", model.Document.Database.ModelSpace.OfType<DxfDBText>().Last().Layer);
            });
        }

        private static void WriteText(MainViewModel model, double y)
        {
            CadCommandSession session = model.Session;
            session.Feed("TEXT");
            session.Feed(new GeoPoint2(0, y));
            session.Feed("2.5");
            session.Feed("0");
            session.Feed("Hi");
            session.Enter();
            Assert.False(session.IsRunning, session.PromptText);
        }

        #endregion

        #region Dimensions

        [Fact]
        public void TheBodyOfDimensionsIsDimensionLarge_ThenStyleAndThreeTiles_LayerAndThreeTiles_ThenLinearQuickAndContinue()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel dimensions = Panel(strip, "Dimensions");
                var large = (ButtonBase)BodyControls(dimensions).First();

                Assert.Equal("dimension|DIM", Compact(NameOf(large)) + "|" + CommandWord(large));
                Assert.Equal(LargeHeight, LargeHeightOf(large), 0.5);

                List<List<FrameworkElement>> rows = SmallRows(dimensions);

                Assert.Equal(3, rows.Count);
                AssertControls(
                    new[] { "combo", "Break|DIMBREAK", "Adjust Space|DIMSPACE", "Jog Line|DIMJOGLINE" },
                    rows[0]
                );
                AssertControls(
                    new[] { "combo", "Inspect|DIMINSPECT", "Update|-DIMSTYLE", "Reassociate|DIMREASSOCIATE" },
                    rows[1]
                );
                AssertControls(new[] { "Linear|DIMLINEAR", "Quick|QDIM", "Continue|DIMCONTINUE" }, rows[2]);

                Assert.Contains("tyle", Words(rows[0][0]));
                Assert.Contains("ayer", Words(rows[1][0]));

                // Six tiles of 24 by 22 with no text; Update runs -DIMSTYLE with its Apply.
                foreach (FrameworkElement tile in rows[0].Skip(1).Concat(rows[1].Skip(1)))
                {
                    Assert.False(HasText(tile), NameOf(tile) + " shows text.");
                    Assert.Equal(TileWidth, tile.ActualWidth, 0.5);
                    Assert.Equal(TileHeight, tile.ActualHeight, 0.5);
                }

                Assert.Equal("-DIMSTYLE A", ((ButtonBase)rows[1][2]).CommandParameter);

                // The third row has its text, Linear and Continue as splits, and stands 24 under the second.
                Assert.All(rows[2], c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                Assert.NotNull(SplitArrowOf((ButtonBase)rows[2][0]));
                Assert.Null(SplitArrowOf((ButtonBase)rows[2][1]));
                Assert.NotNull(SplitArrowOf((ButtonBase)rows[2][2]));

                for (int r = 1; r < 3; r++)
                {
                    Assert.Equal(Pitch, Top(rows[r][0], dimensions.Body) - Top(rows[r - 1][0], dimensions.Body), 0.5);
                }

                // The Linear split: Home's eight dimensions in three groups.
                AssertMenu(
                    model,
                    SplitArrowOf((ButtonBase)rows[2][0]),
                    new[]
                    {
                        new[] { "Linear", "Aligned", "Angular" },
                        new[] { "Arc Length", "Radius", "Diameter" },
                        new[] { "Ordinate", "Jogged" },
                    },
                    null,
                    new[]
                    {
                        "DIMLINEAR",
                        "DIMALIGNED",
                        "DIMANGULAR",
                        "DIMARC",
                        "DIMRADIUS",
                        "DIMDIAMETER",
                        "DIMORDINATE",
                        "DIMJOGGED",
                    }
                );
            });
        }

        [Fact]
        public void TheSlideOutOfDimensionsHoldsToleranceObliqueTextAngleTheThreeJustifiesAndOverride_AsTiles()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel dimensions = Panel(strip, "Dimensions");

                AssertSlideOut(
                    dimensions,
                    new[]
                    {
                        new[]
                        {
                            "Tolerance|TOLERANCE",
                            "Oblique|DIMEDIT",
                            "Text Angle|DIMEDIT",
                            "Left Justify|DIMTEDIT",
                            "Center Justify|DIMTEDIT",
                            "Right Justify|DIMTEDIT",
                            "Override|DIMOVERRIDE",
                        },
                    }
                );

                List<FrameworkElement> tiles = SlideOutControls(dimensions)
                    .OrderBy(c => Left(c, dimensions.SlideOut))
                    .ToList();

                foreach (FrameworkElement tile in tiles)
                {
                    Assert.False(HasText(tile), NameOf(tile) + " shows text.");
                    Assert.Equal(TileWidth, tile.ActualWidth, 0.5);
                    Assert.Equal(TileHeight, tile.ActualHeight, 0.5);
                    Assert.False(string.IsNullOrEmpty(tile.ToolTip as string), NameOf(tile) + " has no tooltip.");
                }

                // D1325: the justifies leave the dimension to be picked ("\") and then answer Left, Center or Right.
                Assert.Equal(
                    new[] { "DIMTEDIT \\ L", "DIMTEDIT \\ C", "DIMTEDIT \\ R" },
                    tiles.Skip(3).Take(3).Select(t => ((ButtonBase)t).CommandParameter)
                );
                Assert.All(tiles.Skip(3).Take(3), t =>
                    Assert.Same(model.StartCommandWithOptionCommand, ((ButtonBase)t).Command)
                );
            });
        }

        // A dimension 100 long with its text at 50: Left puts the words left of 50 and inside the line, Center back at 50, Right
        // right of 50.
        [Fact]
        public void TheJustifyTilesOfTheSlideOutOfDimensions_StartDimteditAndAnswerLeftCenterAndRightOnceADimensionIsPicked()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                model.Execute("DIMLINEAR");
                model.Execute("0,0");
                model.Execute("100,0");
                model.Execute("50,-40");
                Assert.False(model.IsCommandRunning);
                string handle = Assert.Single(model.Document.Database.ModelSpace.OfType<DxfRotatedDimension>()).Handle;

                RibbonPanel dimensions = Panel(strip, "Dimensions");
                OpenSlideOut(dimensions);
                List<FrameworkElement> tiles = SlideOutControls(dimensions)
                    .OrderBy(c => Left(c, dimensions.SlideOut))
                    .ToList();
                Assert.Equal(7, tiles.Count);
                var left = (ButtonBase)tiles[3];
                var center = (ButtonBase)tiles[4];
                var right = (ButtonBase)tiles[5];

                foreach (
                    (ButtonBase tile, Func<double, bool> where) in new (ButtonBase, Func<double, bool>)[]
                    {
                        (left, x => x > 0 && x < 49),
                        (center, x => Math.Abs(x - 50) < 1),
                        (right, x => x > 51 && x < 100),
                    }
                )
                {
                    Press(tile);

                    // The command waits for the dimension, which is the user's.
                    Assert.True(model.IsCommandRunning);
                    Assert.Equal("Select dimension:", model.Prompt);

                    model.Session.FeedSelection(new[] { handle });

                    Assert.False(model.IsCommandRunning, model.Prompt);
                    double x = ((DxfRotatedDimension)model.Document.FindEntity(handle)).TextPosition.X;
                    Assert.True(where(x), NameOf(tile) + " left the words at x = " + x + ".");
                }
            });
        }

        #endregion

        #region Centerlines, Leaders, Tables, Markup and Annotation Scaling

        [Fact]
        public void TheBodyOfCenterlinesIsCenterMarkAndCenterline_BothLarge()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                List<FrameworkElement> controls = BodyControls(Panel(strip, "Centerlines"));

                AssertControls(new[] { "Center Mark|CENTERMARK", "Centerline|CENTERLINE" }, controls);
                Assert.All(controls, c =>
                {
                    Assert.Null(SplitArrowOf((ButtonBase)c));
                    Assert.Equal(LargeHeight, LargeHeightOf(c), 0.5);
                });
            });
        }

        [Fact]
        public void TheBodyOfLeadersIsMultileaderLarge_ThenStyle_ThenAlignAndAddLeader_ThenCollectAndRemoveLeader()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel leaders = Panel(strip, "Leaders");
                var large = (ButtonBase)BodyControls(leaders).First();

                Assert.Equal("multileader|MLEADER", Compact(NameOf(large)) + "|" + CommandWord(large));
                Assert.Equal(LargeHeight, LargeHeightOf(large), 0.5);

                List<List<FrameworkElement>> rows = SmallRows(leaders);

                Assert.Equal(3, rows.Count);
                AssertControls(new[] { "combo" }, rows[0]);
                AssertControls(new[] { "Align|MLEADERALIGN", "Add Leader|MLEADEREDIT" }, rows[1]);
                AssertControls(new[] { "Collect|MLEADERCOLLECT", "Remove Leader|MLEADEREDIT" }, rows[2]);

                Assert.Contains("ultileader", Words(rows[0][0]));
                Assert.False(HasText(rows[1][0]), NameOf(rows[1][0]) + " shows text.");
                Assert.False(HasText(rows[2][0]), NameOf(rows[2][0]) + " shows text.");
                Assert.True(HasText(rows[1][1]), "Add Leader shows no text.");
                Assert.True(HasText(rows[2][1]), "Remove Leader shows no text.");

                // D1325: Remove Leader answers the Remove leaders option of MLEADEREDIT through the option queue.
                var remove = (ButtonBase)rows[2][1];
                Assert.Equal("MLEADEREDIT R", remove.CommandParameter);
                Assert.Same(model.StartCommandWithOptionCommand, remove.Command);
            });
        }

        [Fact]
        public void TheBodyOfTablesIsTableLarge_ThenStyle_ThenDownloadAndExtractData_ThenUploadAndLinkData()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel tables = Panel(strip, "Tables");
                var large = (ButtonBase)BodyControls(tables).First();

                Assert.Equal("table|TABLE", Compact(NameOf(large)) + "|" + CommandWord(large));
                Assert.Equal(LargeHeight, LargeHeightOf(large), 0.5);

                List<List<FrameworkElement>> rows = SmallRows(tables);

                Assert.Equal(3, rows.Count);
                AssertControls(new[] { "combo" }, rows[0]);
                AssertControls(new[] { "Download from Source|DATALINKUPDATE", "Extract Data|DATAEXTRACTION" }, rows[1]);
                AssertControls(new[] { "Upload to Source|DATALINKUPDATE", "Link Data|DATALINK" }, rows[2]);

                Assert.Contains("able", Words(rows[0][0]));
                Assert.False(HasText(rows[1][0]), "Download from Source shows text.");
                Assert.False(HasText(rows[2][0]), "Upload to Source shows text.");
                Assert.True(HasText(rows[1][1]), "Extract Data shows no text.");
                Assert.True(HasText(rows[2][1]), "Link Data shows no text.");

                // Download answers Update, Upload answers Write, as Insert's Linking & Extraction does.
                Assert.Equal("DATALINKUPDATE U", ((ButtonBase)rows[1][0]).CommandParameter);
                Assert.Equal("DATALINKUPDATE W", ((ButtonBase)rows[2][0]).CommandParameter);
            });
        }

        [Fact]
        public void TheBodyOfMarkupIsWipeoutAndRevisionCloudSplit_TheSplitHoldingRectangularPolygonalAndFreehand()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                List<FrameworkElement> controls = BodyControls(Panel(strip, "Markup"));

                AssertControls(new[] { "Wipeout|WIPEOUT", "Revision Cloud|REVCLOUD" }, controls);
                Assert.All(controls, c => Assert.Equal(LargeHeight, LargeHeightOf(c), 0.5));
                Assert.Null(SplitArrowOf((ButtonBase)controls[0]));

                AssertMenu(
                    model,
                    SplitArrowOf((ButtonBase)controls[1]),
                    new[] { new[] { "Rectangular", "Polygonal", "Freehand" } },
                    "REVCLOUD"
                );
            });
        }

        [Fact]
        public void TheBodyOfAnnotationScalingIsAddCurrentScaleSplit_ThenScaleListAndAddDeleteScales()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel scaling = Panel(strip, "Annotation Scaling");
                List<FrameworkElement> controls = BodyControls(scaling);

                AssertControls(
                    new[]
                    {
                        "Add Current Scale|AIOBJECTSCALEADD",
                        "Scale List|SCALELISTEDIT",
                        "Add/Delete Scales|OBJECTSCALE",
                    },
                    controls
                );
                Assert.Equal(LargeHeight, LargeHeightOf(controls[0]), 0.5);
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[0]));

                for (int i = 1; i < 3; i++)
                {
                    Assert.True(HasText(controls[i]), NameOf(controls[i]) + " shows no text.");
                    Assert.Equal(TileHeight, controls[i].ActualHeight, 0.5);
                    Assert.Equal(
                        Top(controls[1], scaling.Body) + ((i - 1) * Pitch),
                        Top(controls[i], scaling.Body),
                        0.5
                    );
                }

                AssertMenu(
                    model,
                    SplitArrowOf((ButtonBase)controls[0]),
                    new[] { new[] { "Add Current Scale", "Remove Current Scale" } },
                    null,
                    new[] { "AIOBJECTSCALEADD", "AIOBJECTSCALEREMOVE" }
                );
            });
        }

        // The room has no multileader style, as a blank drawing has none: AutoCAD shows Standard in the box and lists it (P8).
        [Fact]
        public void TheMultileaderStyleComboOfABlankDrawingShowsStandard_AsAutoCadsDoes()
        {
            OnTab(AnnotateTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel leaders = Panel(strip, "Leaders");
                ComboBox style = Assert.Single(Descendants(leaders.Body).OfType<ComboBox>(), c =>
                    Words(c).Contains("ultileader")
                );

                Assert.Contains("Standard", ItemTexts(style));
                Assert.NotNull(style.SelectedItem);
                Assert.Equal("Standard", ItemText(style.SelectedItem));

                // The model says the same, whatever the box does with it.
                Assert.Contains("Standard", model.MLeaderStyleItems.Select(i => i.Name));
                Assert.Equal("Standard", model.CurrentMLeaderStyle);
            });
        }

        #endregion

        #region Shrinking at AutoCAD's ribbon widths

        // F full, M medium, C collapsed, in the order of AnnotateOrder. AutoCAD's widest ribbon (1 904) takes no step and its
        // narrowest (884) takes all six: Dimensions, Leaders, Tables, Markup and Annotation Scaling collapsed, Text and
        // Centerlines full. Our Annotate is a little narrower than AutoCAD's (1 559 against about 1 600), so at the widths between
        // it takes the same steps in the same order, and a step later where the strip still holds it.
        [Fact]
        public void AtAutoCadsWidestAndNarrowestRibbons_AnnotateIsAllFullOrShrunkAsAutoCads()
        {
            OnTab(AnnotateTab, 1904, (model, workspace, strip) =>
            {
                Assert.Equal("FFFFFFF", Sizes(strip));
                Assert.Equal(0, strip.StepsTaken);
            });
            OnTab(AnnotateTab, 884, (model, workspace, strip) =>
            {
                Assert.Equal("FCFCCCC", Sizes(strip));
                Assert.Equal(6, strip.StepsTaken);
            });
        }

        // The most steps AutoCAD has taken at the next narrower width of its six (D1332: one at 1 584, four at 1 424 and at 1 264,
        // five at 1 084, six at 884): ours never takes more than that.
        [Theory]
        [InlineData(1584, 4)]
        [InlineData(1424, 4)]
        [InlineData(1264, 5)]
        [InlineData(1084, 6)]
        public void AtAMiddleWidth_AnnotateTakesNoMoreStepsThanAutoCadsDoesAtTheNextNarrowerWidth_AndFitsTheStrip(
            double width,
            int most
        )
        {
            OnTab(AnnotateTab, width, (model, workspace, strip) =>
            {
                Assert.InRange(strip.StepsTaken, 0, most);
                Assert.Equal(After(AnnotateSteps, strip.StepsTaken, AnnotateOrder), Sizes(strip));
                Assert.True(
                    strip.DesiredSize.Width <= width + 0.01,
                    "The strip is " + strip.DesiredSize.Width + " wide."
                );
            });
        }

        #endregion
    }
}
