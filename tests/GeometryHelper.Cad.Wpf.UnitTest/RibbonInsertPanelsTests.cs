using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.RibbonTabHarness;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Insert as AutoCAD 2021's, found in the window drawn offscreen: its six panels in AutoCAD's order (Block, Block
    /// Definition, Reference, Import, Data, Linking and Extraction; Location is left out), each panel's controls in order and
    /// which of them are large, the split buttons and what their menus hold, the title "Name ▾" and the slide-out of the three
    /// panels that have one, the launcher of Reference, Import gone from Output, and the sizes the panels take as the strip
    /// narrows. The helpers are RibbonTabHarness's; a strip width is the width the RibbonStrip gets, which is the window's less
    /// what the window gives to what is around it.
    /// </summary>
    public sealed class RibbonInsertPanelsTests
    {
        private static readonly string[] InsertOrder =
        {
            "Block",
            "Block Definition",
            "Reference",
            "Import",
            "Data",
            "Linking & Extraction",
        };

        // D1332, one panel and one size per step.
        private const string InsertSteps =
            "Linking & Extraction:Medium; Data:Medium; Reference:Medium; Import:Collapsed; Data:Collapsed; Linking & Extraction:Collapsed";

        private static CadWorkspaceExtension InsertTab(MainViewModel model) => model.InsertTab;

        #region The panels and their order

        [Fact]
        public void InsertShowsSixPanelsInAutoCadsOrder_BlockBlockDefinitionReferenceImportDataAndLinking_AndNoneShrinksWhenTheStripIsWide()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                List<RibbonPanel> panels = Panels(strip);

                Assert.Equal(InsertOrder, panels.Select(p => p.Title));
                Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
                Assert.Equal(0, strip.StepsTaken);
            });
        }

        [Fact]
        public void TheStepsOfInsertAreAutoCads_LinkingDataAndReferenceMedium_ThenImportDataAndLinkingCollapsed_BlockNeverShrinking()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                Assert.Equal(Squeeze(InsertSteps), Squeeze(strip.Steps));
                AssertStepsInOrder(strip, InsertSteps, "Block", "Block Definition");
            });
        }

        [Fact]
        public void TheTitlesReadBlockBlockDefinitionAndReferenceWithADownArrow_ImportDataAndLinkingPlain()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                foreach (string title in new[] { "Block", "Block Definition", "Reference" })
                {
                    RibbonPanel panel = Panel(strip, title);

                    Assert.NotNull(panel.SlideOut);
                    ToggleButton toggle = Assert.Single(Descendants(panel).OfType<ToggleButton>(), t =>
                        Equals(t.Content, title + " ▾")
                    );
                    Assert.Same(toggle, panel.Header);

                    // A click on the title opens the slide-out: the toggle checked, the panel's drop-down open.
                    toggle.IsChecked = true;
                    Pump(panel);

                    Assert.True(panel.IsDropDownOpen);
                }

                foreach (string title in new[] { "Import", "Data", "Linking & Extraction" })
                {
                    RibbonPanel panel = Panel(strip, title);

                    Assert.Null(panel.SlideOut);
                    Assert.Null(panel.Header);
                    Assert.DoesNotContain(Descendants(panel).OfType<ToggleButton>(), t =>
                        Equals(t.Content, title + " ▾")
                    );
                    Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == title);
                }
            });
        }

        [Fact]
        public void TheLauncherOfReferenceOpensExternalReferences_AndNoOtherInsertPanelHasOne()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                Assert.All(Panels(strip).Where(p => p.Title != "Reference"), p => Assert.Null(p.Launcher));

                RibbonPanel reference = Panel(strip, "Reference");
                Assert.NotNull(reference.Launcher);
                Assert.Contains("EXTERNALREFERENCES", Assert.IsType<string>(reference.LauncherToolTip));

                int before = model.History.Count;
                Assert.True(reference.Launcher.CanExecute(null));
                reference.Launcher.Execute(null);

                Assert.Contains("Command: EXTERNALREFERENCES", model.History.Skip(before));
                model.Escape();
            });
        }

        [Fact]
        public void EveryButtonOfInsertStartsItsCommand_ThroughStartCommandOrStartCommandWithOption()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                SortedSet<string> words = AssertEveryButtonStartsItsCommand(model, strip);

                // One from each new panel, so that an empty panel does not pass.
                Assert.Contains("IMPORT", words);
                Assert.Contains("FIELD", words);
                Assert.Contains("DATALINKUPDATE", words);
            });
        }

        #endregion

        #region The bodies, panel by panel

        [Fact]
        public void TheBodyOfBlockIsInsertAndEditAttributeAsLargeSplits_EditAttributeHoldingSingleAndMultiple()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel block = Panel(strip, "Block");
                List<FrameworkElement> controls = BodyControls(block);

                AssertControls(new[] { "Insert|INSERT", "Edit Attribute|EATTEDIT" }, controls);
                Assert.All(controls, c =>
                {
                    Assert.NotNull(SplitArrowOf((ButtonBase)c));
                    Assert.Equal(LargeHeight, LargeHeightOf(c), 0.5);
                });

                // D1324: AutoCAD's two items; our third, Edit Attributes (ATTEDIT), is no longer in the split.
                AssertMenu(
                    model,
                    SplitArrowOf((ButtonBase)controls[1]),
                    new[] { new[] { "Single", "Multiple" } },
                    null,
                    new[] { "EATTEDIT", "-ATTEDIT" }
                );
            });
        }

        [Fact]
        public void TheBodyOfBlockDefinitionIsCreateBlockSplitThenDefineAttributesManageAttributesAndBlockEditor_AllLarge()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel definition = Panel(strip, "Block Definition");
                List<FrameworkElement> controls = BodyControls(definition);

                AssertControls(
                    new[]
                    {
                        "Create Block|BLOCK",
                        "Define Attributes|ATTDEF",
                        "Manage Attributes|BATTMAN",
                        "Block Editor|BEDIT",
                    },
                    controls
                );
                Assert.All(controls, c => Assert.Equal(LargeHeight, LargeHeightOf(c), 0.5));
                Assert.All(controls.Skip(1), c => Assert.Null(SplitArrowOf((ButtonBase)c)));

                AssertMenu(
                    model,
                    SplitArrowOf((ButtonBase)controls[0]),
                    new[] { new[] { "Create Block", "Write Block" } },
                    null,
                    new[] { "BLOCK", "WBLOCK" }
                );
            });
        }

        [Fact]
        public void TheBodyOfReferenceIsAttachClipAndAdjustLarge_ThenUnderlayLayersTheFramesSplitAndSnapToUnderlays()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel reference = Panel(strip, "Reference");
                List<FrameworkElement> controls = BodyControls(reference);

                // The frames and the snap are split buttons of the ribbon's make (a button and its arrow), not combo boxes.
                Assert.Equal(6, controls.Count);
                ButtonBase frames = Assert.IsAssignableFrom<ButtonBase>(controls[4]);
                ButtonBase snap = Assert.IsAssignableFrom<ButtonBase>(controls[5]);
                Assert.NotNull(SplitArrowOf(frames));
                Assert.NotNull(SplitArrowOf(snap));

                AssertControls(
                    new[]
                    {
                        "Attach|XATTACH",
                        "Clip|IMAGECLIP",
                        "Adjust|PDFADJUST",
                        "Underlay Layers|PDFLAYERS",
                        "|",
                        "|",
                    },
                    controls
                );
                Assert.All(controls.Take(3), c => Assert.Equal(LargeHeight, LargeHeightOf(c), 0.5));
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[0]));
                Assert.All(controls.Skip(3), c =>
                {
                    Assert.True(HasText(c), NameOf(c) + " shows no text.");
                    Assert.Equal(TileHeight, c.ActualHeight, 0.5);
                });

                // Three small ones under one another, 24 apart; External References and Transparency are not in the body.
                for (int i = 4; i < 6; i++)
                {
                    Assert.Equal(
                        Top(controls[3], reference.Body) + ((i - 3) * Pitch),
                        Top(controls[i], reference.Body),
                        0.5
                    );
                }

                Assert.DoesNotContain(controls.Select(CommandWord), w =>
                    w == "EXTERNALREFERENCES" || w == "TRANSPARENCY"
                );

                // D1324: Attach keeps DXF, Image and PDF; the choices the presenter lacks are not drawn greyed (D1303).
                AssertMenu(
                    model,
                    SplitArrowOf((ButtonBase)controls[0]),
                    new[] { new[] { "Attach DXF", "Attach Image", "Attach PDF" } },
                    null,
                    new[] { "XATTACH", "IMAGEATTACH", "PDFATTACH" }
                );

                // The frames drop-down: the three settings of IMAGEFRAME; the snap drop-down: on and off, PDFOSNAP.
                List<MenuItem> frameItems = MenuOf(SplitArrowOf(frames));
                Assert.Equal(
                    new[] { "Display and Plot Frames", "Display but Not Plot Frames", "Hide Frames" },
                    frameItems.Select(Norm).OrderBy(n => n, StringComparer.Ordinal)
                );
                Assert.All(frameItems, i => Assert.Equal("IMAGEFRAME", ((string)i.CommandParameter).Split(' ')[0]));
                PickEach(model, frameItems);

                List<MenuItem> snapItems = MenuOf(SplitArrowOf(snap));
                Assert.Equal(2, snapItems.Count);
                Assert.All(snapItems, i => Assert.Equal("PDFOSNAP", ((string)i.CommandParameter).Split(' ')[0]));
                PickEach(model, snapItems);
            });
        }

        [Fact]
        public void TheBodyOfImportIsImportAndCombineText_BothLarge()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel import = Panel(strip, "Import");
                List<FrameworkElement> controls = BodyControls(import);

                AssertControls(new[] { "Import|IMPORT", "Combine Text|TXT2MTXT" }, controls);
                Assert.All(controls, c =>
                {
                    Assert.Null(SplitArrowOf((ButtonBase)c));
                    Assert.Equal(LargeHeight, LargeHeightOf(c), 0.5);
                });
            });
        }

        [Fact]
        public void TheBodyOfDataIsFieldLarge_ThenUpdateFieldsOleObjectAndHyperlinkWithText()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel data = Panel(strip, "Data");
                List<FrameworkElement> controls = BodyControls(data);

                AssertControls(
                    new[] { "Field|FIELD", "Update Fields|UPDATEFIELD", "OLE Object|INSERTOBJ", "Hyperlink|HYPERLINK" },
                    controls
                );
                Assert.Equal(LargeHeight, LargeHeightOf(controls[0]), 0.5);

                for (int i = 1; i < 4; i++)
                {
                    Assert.True(HasText(controls[i]), NameOf(controls[i]) + " shows no text.");
                    Assert.Equal(TileHeight, controls[i].ActualHeight, 0.5);
                    Assert.Equal(Top(controls[1], data.Body) + ((i - 1) * Pitch), Top(controls[i], data.Body), 0.5);
                }
            });
        }

        [Fact]
        public void TheBodyOfLinkingAndExtractionIsDataLinkLarge_ThenDownloadUploadAndExtractData()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel linking = Panel(strip, "Linking & Extraction");
                List<FrameworkElement> controls = BodyControls(linking);

                AssertControls(
                    new[]
                    {
                        "Data Link|DATALINK",
                        "Download from Source|DATALINKUPDATE",
                        "Upload to Source|DATALINKUPDATE",
                        "Extract Data|DATAEXTRACTION",
                    },
                    controls
                );
                Assert.Equal(LargeHeight, LargeHeightOf(controls[0]), 0.5);
                Assert.All(controls.Skip(1), c => Assert.True(HasText(c), NameOf(c) + " shows no text."));

                // D1325: Download answers Update, Upload answers Write, through the option queue.
                var download = (ButtonBase)controls[1];
                var upload = (ButtonBase)controls[2];
                Assert.Equal("DATALINKUPDATE U", download.CommandParameter);
                Assert.Equal("DATALINKUPDATE W", upload.CommandParameter);
                Assert.Same(model.StartCommandWithOptionCommand, download.Command);
                Assert.Same(model.StartCommandWithOptionCommand, upload.Command);

                // The room has no data link: the command says so at once, whichever option it was given.
                int before = model.History.Count;
                Press(upload);

                Assert.Contains("Command: DATALINKUPDATE", model.History.Skip(before));
                Assert.Contains("No data links exist in the current drawing.", model.History.Skip(before));
                Assert.False(model.IsCommandRunning);
            });
        }

        #endregion

        #region The slide-outs

        [Fact]
        public void TheSlideOutsOfBlockBlockDefinitionAndReference_HoldAttributeDisplaySetBasePointSynchronizeAndEditReference()
        {
            OnTab(InsertTab, Full, (model, workspace, strip) =>
            {
                RibbonPanel block = Panel(strip, "Block");
                AssertSlideOut(block, new[] { new[] { "|ATTDISP" } });
                OpenSlideOut(block);
                ButtonBase display = Assert.Single(Descendants(block.SlideOut).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "ATTDISP"
                );
                AssertMenu(model, SplitArrowOf(display), new[] { new[] { "Normal", "On", "Off" } }, "ATTDISP");

                RibbonPanel definition = Panel(strip, "Block Definition");
                AssertSlideOut(definition, new[] { new[] { "Set Base Point|BASE" }, new[] { "Synchronize|ATTSYNC" } });

                RibbonPanel reference = Panel(strip, "Reference");
                AssertSlideOut(reference, new[] { new[] { "Edit Reference|REFEDIT" } });

                // Every control of a slide-out is a small button with its text, 22 high, in rows 24 apart.
                foreach (RibbonPanel panel in new[] { block, definition, reference })
                {
                    OpenSlideOut(panel);
                    List<FrameworkElement> controls = SlideOutControls(panel);

                    Assert.All(controls, c =>
                    {
                        Assert.True(HasText(c), NameOf(c) + " shows no text.");
                        Assert.Equal(TileHeight, c.ActualHeight, 0.5);
                    });

                    List<List<FrameworkElement>> rows = Rows(controls, panel.SlideOut);

                    for (int r = 1; r < rows.Count; r++)
                    {
                        Assert.Equal(Pitch, Top(rows[r][0], panel.SlideOut) - Top(rows[r - 1][0], panel.SlideOut), 0.5);
                    }
                }
            });
        }

        #endregion

        #region Shrinking at AutoCAD's ribbon widths

        // F full, M medium, C collapsed, in the order of InsertOrder. AutoCAD's widest ribbon (1 904) takes no step. Our Insert is
        // narrower than AutoCAD's (1 046 against about 1 300: our Import is 100 wide against 270, and Location is left out), so
        // at the narrower widths it takes the steps in the same order but fewer of them, and the narrowest is not all six at 884:
        // after the first three (Linking, Data and Reference medium) the strip is about 830 wide. What is asserted there is that
        // the sizes are those the steps give for the number taken, and that the strip fits.
        [Fact]
        public void AtAutoCadsWidestAndNarrowestRibbons_InsertIsAllFullOrShrunkAsAutoCads()
        {
            OnTab(InsertTab, 1904, (model, workspace, strip) =>
            {
                Assert.Equal("FFFFFF", Sizes(strip));
                Assert.Equal(0, strip.StepsTaken);
            });
            OnTab(InsertTab, 884, (model, workspace, strip) =>
            {
                Assert.Equal(After(InsertSteps, strip.StepsTaken, InsertOrder), Sizes(strip));
                Assert.True(strip.StepsTaken > 0, "Nothing shrank at 884.");
                Assert.True(
                    strip.DesiredSize.Width <= 884 + 0.01,
                    "The strip is " + strip.DesiredSize.Width + " wide."
                );
                Assert.Equal("FF", Sizes(strip).Substring(0, 2));
            });
        }

        // The most steps AutoCAD has taken at the next narrower width of its six (D1332: none at 1 904, 1 584 and 1 424; Linking at
        // 1 264; Reference, Data and Linking medium at 1 084; all six at 884): ours never takes more than that.
        [Theory]
        [InlineData(1584, 0)]
        [InlineData(1424, 1)]
        [InlineData(1264, 3)]
        [InlineData(1084, 6)]
        public void AtAMiddleWidth_InsertTakesNoMoreStepsThanAutoCadsDoesAtTheNextNarrowerWidth_AndFitsTheStrip(
            double width,
            int most
        )
        {
            OnTab(InsertTab, width, (model, workspace, strip) =>
            {
                Assert.InRange(strip.StepsTaken, 0, most);
                Assert.Equal(After(InsertSteps, strip.StepsTaken, InsertOrder), Sizes(strip));
                Assert.True(
                    strip.DesiredSize.Width <= width + 0.01,
                    "The strip is " + strip.DesiredSize.Width + " wide."
                );
            });
        }

        #endregion
    }
}
