using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Home as AutoCAD 2021's, found in the window drawn offscreen: its ten panels in AutoCAD's order (Draw, Modify, Annotation,
    /// Layers, Block, Properties, Groups, Utilities, Clipboard, View), each panel's controls in order and which of them are
    /// icon-only tiles, the split buttons and what their menus hold, the title "Name ▾" and the slide-out of the eight panels
    /// that have one, the launcher of Properties, the sizes the panels take at AutoCAD's six ribbon widths, and the common
    /// metrics (panel 98 high under an 18-high title, large buttons 72 high, tiles 24 by 22, rows 24 apart). The Layers panel
    /// is the business of RibbonLayersPanelTests: here only its place and its size are read. A strip width is the width the
    /// RibbonStrip gets, which is the window's less the 24 pixels the window gives to what is around it.
    /// </summary>
    public sealed class RibbonHomePanelsTests
    {
        // A strip so wide that no panel of Home shrinks (the panels need about 1 900 at AutoCAD's widths).
        private const double Full = 3000;

        // The metrics of D1305 at 100 %; the panel's 98 is D1313 (AutoCAD measured y 44 to 142: body 80 over a title of 18), the
        // design board draws 76 + 18 = 94.
        private const double PanelHeight = 98;
        private const double TitleBand = 18;
        private const double LargeHeight = 72;
        private const double TileWidth = 24;
        private const double TileHeight = 22;
        private const double Pitch = 24;

        // A split tile is the icon and its arrow in 35 (D1315, design-notes.md "Tile"): AutoCAD's own is 36, one pixel shared.
        private const double SplitTileWidth = 35;

        private static readonly string[] HomeOrder =
        {
            "Draw",
            "Modify",
            "Annotation",
            "Layers",
            "Block",
            "Properties",
            "Groups",
            "Utilities",
            "Clipboard",
            "View",
        };

        // D1308 as D1316 reordered it, one panel and one size per step; the strip reads it parted by semicolons.
        private const string HomeSteps =
            "Block:Medium; Properties:Medium; Modify:Medium; Annotation:Medium; Layers:Medium; View:Collapsed; Clipboard:Collapsed; "
            + "Utilities:Collapsed; Groups:Collapsed; Properties:Collapsed; Block:Collapsed; Layers:Collapsed; Annotation:Collapsed";

        #region What each panel holds (name|command; an empty part is not checked; "combo" is a combo box)

        // The body of each panel, columns from left to right, each column from top to bottom.
        private static readonly string[] DrawBody =
        {
            "Line|LINE",
            "Polyline|PLINE",
            "Circle|CIRCLE",
            "Arc|ARC",
            "|RECTANG",
            "|ELLIPSE",
            "|HATCH",
        };

        private static readonly string[] ModifyBody =
        {
            "Move|MOVE",
            "Copy|COPY",
            "Stretch|STRETCH",
            "Rotate|ROTATE",
            "Mirror|MIRROR",
            "Scale|SCALE",
            "Trim|TRIM",
            "Fillet|FILLET",
            "Array|ARRAYRECT",
            "Erase|ERASE",
            "Explode|EXPLODE",
            "Offset|OFFSET",
        };

        private static readonly string[] AnnotationBody =
        {
            "Text|MTEXT",
            "Dimension|DIM",
            "Linear|DIMLINEAR",
            "Leader|MLEADER",
            "Table|TABLE",
        };

        private static readonly string[] BlockBody =
        {
            "Insert|INSERT",
            "Create|BLOCK",
            "Edit|BEDIT",
            "Edit Attributes|EATTEDIT",
        };

        private static readonly string[] GroupsBody = { "Group|GROUP", "Ungroup|UNGROUP", "Group Edit|GROUPEDIT", "|" };

        private static readonly string[] UtilitiesBody =
        {
            "Measure|MEASUREGEOM",
            "Quick Select|QSELECT",
            "Select All|",
            "Quick Calculator|QUICKCALC",
        };

        private static readonly string[] ClipboardBody =
        {
            "Paste|PASTECLIP",
            "Cut|CUTCLIP",
            "Copy Clip|COPYCLIP",
            "Copy with Base Point|COPYBASE",
        };

        private static readonly string[] ViewBody = { "Zoom Extents|", "Pan|" };

        // The slide-outs, rows from top to bottom, each row from left to right.
        private static readonly string[][] DrawSlideOut =
        {
            new[]
            {
                "Spline Fit|SPLINE",
                "Spline CV|SPLINE",
                "Construction Line|XLINE",
                "Ray|RAY",
                "Multiple Points|POINT",
                "Divide|DIVIDE",
                "Measure|MEASURE",
            },
            // Helix is left out where the presenter has no HELIX (the pin has none); a Helix, where there is one, is not read here.
            new[] { "Region|REGION", "Wipeout|WIPEOUT", "Donut|DONUT", "|REVCLOUD" },
        };

        private static readonly string[][] ModifySlideOut =
        {
            new[]
            {
                "Set to ByLayer|SETBYLAYER",
                "Change Space|CHSPACE",
                "Lengthen|LENGTHEN",
                "Edit Polyline|PEDIT",
                "Edit Spline|SPLINEDIT",
                "Edit Hatch|HATCHEDIT",
                "Edit Array|ARRAYEDIT",
            },
            new[]
            {
                "Align|ALIGN",
                "Break|BREAK",
                "Break at Point|BREAKATPOINT",
                "Join|JOIN",
                "Reverse|REVERSE",
                "Delete Duplicate Objects|OVERKILL",
                "|DRAWORDER",
            },
        };

        private static readonly string[][] AnnotationSlideOut =
        {
            new[] { "Text Style|STYLE", "combo" },
            new[] { "Dimension Style|DIMSTYLE", "combo" },
            new[] { "Multileader Style|MLEADERSTYLE", "combo" },
            new[] { "Table Style|TABLESTYLE", "combo" },
        };

        private static readonly string[][] BlockSlideOut =
        {
            new[]
            {
                "Define Attributes|ATTDEF",
                "Manage Attributes|BATTMAN",
                "Synchronize Attributes|ATTSYNC",
                "|ATTDISP",
                "Set Base Point|BASE",
            },
        };

        private static readonly string[][] GroupsSlideOut = { new[] { "Group Manager|CLASSICGROUP" } };

        private static readonly string[][] UtilitiesSlideOut =
        {
            new[] { "ID Point|ID" },
            new[] { "Point Style|PTYPE" },
        };

        #endregion

        #region The panels and their order

        [Fact]
        public void HomeShowsTenPanelsInAutoCadsOrder_BlockStandingBetweenLayersAndProperties_AndNoneShrinksWhenTheStripIsWide()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                List<RibbonPanel> panels = Panels(strip);

                Assert.Equal(HomeOrder, panels.Select(p => p.Title));
                Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
                Assert.Equal(0, strip.StepsTaken);
            });
        }

        [Fact]
        public void TheStepsOfHomeAreAutoCads_BlockPropertiesModifyAnnotationAndLayersMedium_ThenViewDownToGroupsCollapsed_ThenPropertiesBlockLayersAndAnnotation()
        {
            OnHome(Full, (model, workspace, strip) => Assert.Equal(Squeeze(HomeSteps), Squeeze(strip.Steps)));
        }

        [Theory]
        [InlineData("Draw")]
        [InlineData("Modify")]
        [InlineData("Annotation")]
        [InlineData("Layers")]
        [InlineData("Block")]
        [InlineData("Properties")]
        [InlineData("Groups")]
        [InlineData("Utilities")]
        public void ThePanelTitleReadsTheNameAndADownArrow_AndOpensASlideOut(string title)
        {
            OnHome(Full, (model, workspace, strip) =>
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
                Assert.NotNull(((Popup)panel.Template.FindName("PART_Popup", panel)).Child);
            });
        }

        [Theory]
        [InlineData("Clipboard")]
        [InlineData("View")]
        public void ClipboardAndViewShowTheirPlainTitle_WithNoSlideOut(string title)
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel panel = Panel(strip, title);

                Assert.Null(panel.SlideOut);
                Assert.Null(panel.Header);
                Assert.DoesNotContain(Descendants(panel).OfType<ToggleButton>(), t => Equals(t.Content, title + " ▾"));
                Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == title);
            });
        }

        [Fact]
        public void OnlyPropertiesHasALauncher_AndItStartsPropertiesWhichShowsThePalette()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                Assert.All(Panels(strip).Where(p => p.Title != "Properties"), p => Assert.Null(p.Launcher));

                RibbonPanel properties = Panel(strip, "Properties");
                Assert.NotNull(properties.Launcher);
                Assert.Contains("PROPERTIES", Assert.IsType<string>(properties.LauncherToolTip));

                // The palette is hidden first, so that showing it is the launcher's doing.
                model.IsPropertiesPanelShown = false;
                int before = model.History.Count;
                Assert.True(properties.Launcher.CanExecute(null));
                properties.Launcher.Execute(null);

                Assert.Contains("Command: PROPERTIES", model.History.Skip(before));
                Assert.True(model.IsPropertiesPanelShown);
                model.Escape();
            });
        }

        [Fact]
        public void EveryCommandHomeStartsIsOneThePresenterHas_ThoseItLacksAreLeftOutNotDrawn()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                var words = new SortedSet<string>(StringComparer.Ordinal);

                foreach (RibbonPanel panel in Panels(strip))
                {
                    if (panel.SlideOut != null)
                    {
                        OpenSlideOut(panel);
                    }

                    foreach (DependencyObject root in new DependencyObject[] { panel.Body, panel.SlideOut })
                    {
                        if (root == null)
                        {
                            continue;
                        }

                        foreach (ButtonBase button in Descendants(root).OfType<ButtonBase>())
                        {
                            words.Add(CommandWord(button));

                            if (button.ContextMenu != null)
                            {
                                foreach (MenuItem item in MenuOf(button))
                                {
                                    words.Add(((item.CommandParameter as string) ?? string.Empty).Split(' ')[0]);
                                }
                            }
                        }
                    }
                }

                words.Remove(string.Empty);
                string[] unknown = words.Where(w => model.Session.Registry.Find(w) == null).ToArray();
                Assert.True(unknown.Length == 0, "No such command: " + string.Join(", ", unknown));
                Assert.Contains("CIRCLE", words);
            });
        }

        #endregion

        #region The bodies, panel by panel

        [Fact]
        public void TheBodyOfDrawIsLinePolylineCircleArcAsLargeButtons_ThenAColumnOfThreeIconOnlySplitsRectangleEllipseHatch()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel draw = Panel(strip, "Draw");
                List<FrameworkElement> controls = BodyControls(draw);

                AssertControls(DrawBody, controls);

                // Circle and Arc are split large buttons; Rectangle, Ellipse and Hatch are icon-only splits in one column.
                Assert.All(controls.Skip(2).Take(2), c => Assert.NotNull(SplitArrowOf((ButtonBase)c)));
                foreach (FrameworkElement tile in controls.Skip(4))
                {
                    Assert.NotNull(SplitArrowOf((ButtonBase)tile));
                    Assert.False(HasText(tile));
                    Assert.Equal(Left(controls[4], draw.Body), Left(tile, draw.Body), 0.5);
                }
            });
        }

        [Fact]
        public void TheBodyOfModifyIsAutoCadsThreeColumnsWithTextThenErasExplodeOffsetAsTiles_WithNoFourthRowAndNoDrawOrder()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel modify = Panel(strip, "Modify");
                List<FrameworkElement> controls = BodyControls(modify);

                AssertControls(ModifyBody, controls);
                Assert.All(controls.Take(9), c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                Assert.All(controls.Skip(9), c => Assert.False(HasText(c), NameOf(c) + " shows text."));

                // Three rows, not four: nothing stands lower than the third row of the first column.
                double third = Top(controls[2], modify.Body);
                Assert.All(controls, c =>
                    Assert.True(Top(c, modify.Body) <= third + 0.5, NameOf(c) + " is below row 3.")
                );

                // The title does not drop a menu: the slide-out is behind a toggle button, "Modify ▾".
                Assert.IsAssignableFrom<ToggleButton>(modify.Header);
            });
        }

        [Fact]
        public void TheBodyOfAnnotationIsTextAndDimensionAsLargeButtons_ThenLinearLeaderAndTableWithText()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel annotation = Panel(strip, "Annotation");
                List<FrameworkElement> controls = BodyControls(annotation);

                AssertControls(AnnotationBody, controls);
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[0]));
                Assert.All(controls.Skip(2), c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[2]));
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[3]));
            });
        }

        [Fact]
        public void TheBodyOfBlockIsAnInsertLargeSplit_ThenCreateEditAndEditAttributesWithText()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel block = Panel(strip, "Block");
                List<FrameworkElement> controls = BodyControls(block);

                AssertControls(BlockBody, controls);
                Assert.All(controls.Skip(1), c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[0]));
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[3]));
            });
        }

        [Fact]
        public void TheBodyOfPropertiesIsMatchPropertiesLarge_ThenObjectColorLineweightAndLinetypeCombos_EachWithItsIcon()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel properties = Panel(strip, "Properties");
                List<ComboBox> combos = Descendants(properties.Body)
                    .OfType<ComboBox>()
                    .OrderBy(c => Top(c, properties.Body))
                    .ToList();
                ButtonBase match = Assert.Single(Descendants(properties.Body).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "MATCHPROP"
                );

                Assert.Equal("Match Properties", NameOf(match));
                Assert.Equal(3, combos.Count);
                Assert.Contains("olo", Words(combos[0]));
                Assert.Contains("ineweight", Words(combos[1]));
                Assert.Contains("inetype", Words(combos[2]));

                // The combo stands right of Match Properties, in rows 24 apart from the top of the body, 22 high.
                Assert.All(combos, c =>
                    Assert.True(Left(c, properties.Body) > Left(match, properties.Body) + match.ActualWidth - 0.5)
                );

                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(TileHeight, combos[i].ActualHeight, 0.5);
                    Assert.Equal(Top(combos[0], properties.Body) + (i * Pitch), Top(combos[i], properties.Body), 0.5);

                    // Its icon stands in the 20-wide slot to the left of the 180-wide box, 200 together.
                    IconView icon = Descendants(properties.Body)
                        .OfType<IconView>()
                        .First(v =>
                            Math.Abs(CenterY(v, properties.Body) - CenterY(combos[i], properties.Body)) < 2
                            && Left(v, properties.Body) < Left(combos[i], properties.Body)
                        );
                    Assert.Equal(180.0, combos[i].ActualWidth, 0.5);
                    Assert.True(Left(icon, properties.Body) >= Left(combos[i], properties.Body) - 20.5);
                    Assert.True(
                        Left(icon, properties.Body) + icon.ActualWidth <= Left(combos[i], properties.Body) + 0.5
                    );
                }
            });
        }

        [Fact]
        public void TheBodyOfGroupsIsGroupLarge_ThenUngroupGroupEditAndGroupSelectionAsOneColumnOfTiles()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel groups = Panel(strip, "Groups");
                List<FrameworkElement> controls = BodyControls(groups);

                AssertControls(GroupsBody, controls);
                Assert.All(controls.Skip(1), c => Assert.False(HasText(c), NameOf(c) + " shows text."));
                Assert.IsAssignableFrom<ToggleButton>(controls[3]);
                Assert.All(controls.Skip(2), c =>
                    Assert.Equal(Left(controls[1], groups.Body), Left(c, groups.Body), 0.5)
                );
            });
        }

        [Fact]
        public void TheBodyOfUtilitiesIsAMeasureLargeSplit_ThenQuickSelectSelectAllAndQuickCalculatorAsTiles()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel utilities = Panel(strip, "Utilities");
                List<FrameworkElement> controls = BodyControls(utilities);

                AssertControls(UtilitiesBody, controls);
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[0]));
                Assert.All(controls.Skip(1), c => Assert.False(HasText(c), NameOf(c) + " shows text."));
                Assert.All(controls.Skip(2), c =>
                    Assert.Equal(Left(controls[1], utilities.Body), Left(c, utilities.Body), 0.5)
                );
            });
        }

        [Fact]
        public void TheBodyOfClipboardIsAPasteLargeSplit_ThenCutCopyClipAndCopyWithBasePointAsTiles()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel clipboard = Panel(strip, "Clipboard");
                List<FrameworkElement> controls = BodyControls(clipboard);

                AssertControls(ClipboardBody, controls);
                Assert.NotNull(SplitArrowOf((ButtonBase)controls[0]));
                Assert.All(controls.Skip(1), c => Assert.False(HasText(c), NameOf(c) + " shows text."));
                Assert.All(controls.Skip(2), c =>
                    Assert.Equal(Left(controls[1], clipboard.Body), Left(c, clipboard.Body), 0.5)
                );
            });
        }

        [Fact]
        public void TheBodyOfViewIsOurZoomExtentsAndPanAsLargeButtons_RunningTheModelsZoomExtentsAndPan()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel view = Panel(strip, "View");
                List<FrameworkElement> controls = BodyControls(view);

                AssertControls(ViewBody, controls);
                Assert.Same(model.ZoomExtentsCommand, ((ButtonBase)controls[0]).Command);
                Assert.Same(model.PanCommand, ((ButtonBase)controls[1]).Command);
                Assert.All(controls, c => Assert.Equal(LargeHeight, c.ActualHeight, 0.5));
            });
        }

        [Fact]
        public void EveryButtonOfTheBodiesStartsItsCommand_ThroughStartCommandOrStartCommandWithOption()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                foreach (
                    RibbonPanel panel in Panels(strip)
                        .Where(p => p.Title != "Layers" && p.Title != "View" && p.Title != "Properties")
                )
                {
                    foreach (
                        ButtonBase button in BodyControls(panel)
                            .OfType<ButtonBase>()
                            .Where(b => CommandWord(b).Length > 0)
                    )
                    {
                        Assert.Contains(
                            button.Command,
                            new[] { model.StartCommandCommand, model.StartCommandWithOptionCommand }
                        );
                        Assert.True(button.IsEnabled, CommandWord(button) + " is not enabled.");
                        Assert.True(
                            button.Command.CanExecute(button.CommandParameter),
                            CommandWord(button) + " cannot run."
                        );
                    }
                }
            });
        }

        #endregion

        #region The split buttons and what their menus hold

        [Theory]
        [InlineData("Draw", "CIRCLE", "Circle")]
        [InlineData("Draw", "ARC", "Arc")]
        public void TheCircleAndArcSplits_KeepTheirLabelStatic_WhileTheirTopRunsTheItemPickedLast(
            string panelTitle,
            string command,
            string label
        )
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel panel = Panel(strip, panelTitle);
                ButtonBase main = Assert.Single(BodyControls(panel).OfType<ButtonBase>(), b =>
                    CommandWord(b) == command
                );
                ButtonBase arrow = SplitArrowOf(main);
                Assert.NotNull(arrow);
                MenuItem last = MenuOf(arrow).Last();

                Assert.Equal(label + "▾", Label(main.Parent));

                Pick(last);
                model.Escape();

                Assert.Equal(label + "▾", Label(main.Parent));
                Assert.Equal(last.CommandParameter, main.CommandParameter);
            });
        }

        [Fact]
        public void TheCircleSplitHoldsCenterRadiusAndDiameter_ThenTwoAndThreePoint_ThenTanTanRadiusAndTanTanTan_EachStartingCircle()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                ButtonBase main = Assert.Single(BodyControls(Panel(strip, "Draw")).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "CIRCLE"
                );

                AssertMenu(
                    model,
                    SplitArrowOf(main),
                    new[]
                    {
                        new[] { "Center Radius", "Center Diameter" },
                        new[] { "2-Point", "3-Point" },
                        new[] { "Tan Tan Radius", "Tan Tan Tan" },
                    },
                    "CIRCLE"
                );
            });
        }

        [Fact]
        public void TheArcSplitHoldsThreePointThenThreeStartCenterThreeStartEndThreeCenterStartAndContinue_EachStartingArc()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                ButtonBase main = Assert.Single(BodyControls(Panel(strip, "Draw")).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "ARC"
                );

                AssertMenu(
                    model,
                    SplitArrowOf(main),
                    new[]
                    {
                        new[] { "3-Point" },
                        new[] { "Start Center End", "Start Center Angle", "Start Center Length" },
                        new[] { "Start End Angle", "Start End Direction", "Start End Radius" },
                        new[] { "Center Start End", "Center Start Angle", "Center Start Length" },
                        new[] { "Continue" },
                    },
                    "ARC"
                );
            });
        }

        [Fact]
        public void TheRectangleSplitHoldsRectangleAndPolygon_AndTheEllipseSplitCenterAndAxisEndThenEllipticalArc()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                List<ButtonBase> body = BodyControls(Panel(strip, "Draw")).OfType<ButtonBase>().ToList();

                AssertMenu(
                    model,
                    SplitArrowOf(Assert.Single(body, b => CommandWord(b) == "RECTANG")),
                    new[] { new[] { "Rectangle", "Polygon" } },
                    null,
                    new[] { "RECTANG", "POLYGON" }
                );
                AssertMenu(
                    model,
                    SplitArrowOf(Assert.Single(body, b => CommandWord(b) == "ELLIPSE")),
                    new[] { new[] { "Center", "Axis End" }, new[] { "Elliptical Arc" } },
                    "ELLIPSE"
                );
                AssertMenu(
                    model,
                    SplitArrowOf(Assert.Single(body, b => CommandWord(b) == "HATCH")),
                    new[] { new[] { "Hatch", "Gradient", "Boundary" } },
                    null,
                    new[] { "HATCH", "GRADIENT", "BOUNDARY" }
                );
            });
        }

        [Fact]
        public void TheFilletSplitGainsBlend_AfterFilletAndChamfer()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                ButtonBase fillet = Assert.Single(BodyControls(Panel(strip, "Modify")).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "FILLET"
                );
                List<MenuItem> items = MenuOf(SplitArrowOf(fillet));

                Assert.Equal(
                    new[] { "FILLET", "CHAMFER", "BLEND" },
                    items.Select(i => ((string)i.CommandParameter).Split(' ')[0])
                );
                Assert.StartsWith("Blend", Norm(items[2]));
            });
        }

        [Fact]
        public void TheTextSplitHoldsMultilineTextThenSingleLine_AndKeepsTheLabelText()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel annotation = Panel(strip, "Annotation");
                ButtonBase text = BodyControls(annotation).OfType<ButtonBase>().First();

                AssertMenu(
                    model,
                    SplitArrowOf(text),
                    new[] { new[] { "Multiline Text", "Single Line" } },
                    null,
                    new[] { "MTEXT", "TEXT" }
                );

                Pick(MenuOf(SplitArrowOf(text)).Last());
                model.Escape();

                Assert.Equal("Text▾", Label(text.Parent));
            });
        }

        [Fact]
        public void TheLinearSplitHoldsEightDimensionsInThreeGroups_AndTheLeaderSplitLeaderThenAddAndRemoveThenAlignAndCollect()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                List<ButtonBase> body = BodyControls(Panel(strip, "Annotation")).OfType<ButtonBase>().ToList();

                AssertMenu(
                    model,
                    SplitArrowOf(Assert.Single(body, b => CommandWord(b) == "DIMLINEAR")),
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
                AssertMenu(
                    model,
                    SplitArrowOf(Assert.Single(body, b => CommandWord(b) == "MLEADER")),
                    new[] { new[] { "Leader" }, new[] { "Add Leader", "Remove Leader" }, new[] { "Align", "Collect" } },
                    null,
                    new[] { "MLEADER", "MLEADEREDIT", "MLEADEREDIT", "MLEADERALIGN", "MLEADERCOLLECT" }
                );
            });
        }

        [Fact]
        public void TheMeasureSplitHoldsQuickDistanceRadiusAngleAndArea_EachStartingMeasuregeom()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                ButtonBase measure = BodyControls(Panel(strip, "Utilities")).OfType<ButtonBase>().First();

                // Volume is left out: the presenter measures in two dimensions.
                AssertMenu(
                    model,
                    SplitArrowOf(measure),
                    new[] { new[] { "Quick", "Distance", "Radius", "Angle", "Area" } },
                    "MEASUREGEOM"
                );
            });
        }

        [Fact]
        public void ThePasteSplitHoldsPasteAsBlockToOriginalCoordinatesAndSpecial_AndItsTopAlwaysPastes()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                ButtonBase paste = BodyControls(Panel(strip, "Clipboard")).OfType<ButtonBase>().First();
                List<MenuItem> items = MenuOf(SplitArrowOf(paste));

                Assert.Equal(
                    new[] { "Paste", "Paste as Block", "Paste to Original Coordinates", "Paste Special" },
                    items.Select(Norm)
                );
                Assert.Equal(
                    new[] { "PASTECLIP", "PASTEBLOCK", "PASTEORIG", "PASTESPEC" },
                    items.Select(i => (string)i.CommandParameter)
                );

                // The design board (RibbonHome, "No follow") has the top always paste, as AutoCAD's split (SplitNoFollow).
                Pick(items[1]);
                model.Escape();

                Assert.Equal("PASTECLIP", paste.CommandParameter);
                Assert.Equal("Paste▾", Label(paste.Parent));
            });
        }

        [Fact]
        public void TheEditAttributesSplitHoldsSingleAndMultiple_StartingEatteditAndMinusAttedit()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                ButtonBase edit = BodyControls(Panel(strip, "Block")).OfType<ButtonBase>().Last();

                AssertMenu(
                    model,
                    SplitArrowOf(edit),
                    new[] { new[] { "Single", "Multiple" } },
                    null,
                    new[] { "EATTEDIT", "-ATTEDIT" }
                );
            });
        }

        [Fact]
        public void TheInsertSplitOfBlockOpensTheGalleryOfBlocks_AsInsertsOwnDoes()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                ButtonBase insert = BodyControls(Panel(strip, "Block")).OfType<ButtonBase>().First();
                ButtonBase arrow = SplitArrowOf(insert);

                Assert.Equal("Insert▾", Label(insert.Parent));
                Assert.True(arrow.Command != null || arrow.ContextMenu != null, "The arrow of Insert opens nothing.");
            });
        }

        #endregion

        #region The slide-outs

        [Fact]
        public void TheSlideOutOfDrawHoldsSplineFitToMeasureInARow_ThenRegionWipeoutDonutAndRevisionCloud_AsTiles()
        {
            OnHome(Full, (model, workspace, strip) =>
                AssertSlideOut(Panel(strip, "Draw"), DrawSlideOut, c => CommandWord(c) != "HELIX")
            );
        }

        [Fact]
        public void TheRevisionCloudTileHoldsRectangularPolygonalAndFreehand_AsASplit()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel draw = Panel(strip, "Draw");
                OpenSlideOut(draw);
                ButtonBase cloud = Assert.Single(Descendants(draw.SlideOut).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "REVCLOUD"
                );

                AssertMenu(
                    model,
                    SplitArrowOf(cloud),
                    new[] { new[] { "Rectangular", "Polygonal", "Freehand" } },
                    "REVCLOUD"
                );
            });
        }

        [Fact]
        public void TheSlideOutOfModifyHoldsSetToByLayerToEditArrayInARow_ThenAlignToDrawOrder_AsTiles()
        {
            OnHome(Full, (model, workspace, strip) => AssertSlideOut(Panel(strip, "Modify"), ModifySlideOut, null));
        }

        [Fact]
        public void TheDrawOrderTileOfModifyHoldsItsFourThenTheAnnotationOnes_AndItsFirstItemsStartTheCommandsTheyDidBefore()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel modify = Panel(strip, "Modify");
                OpenSlideOut(modify);
                ButtonBase order = Assert.Single(Descendants(modify.SlideOut).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "DRAWORDER"
                );
                List<object> items = ((ContextMenu)SplitArrowOf(order).ContextMenu).Items.Cast<object>().ToList();
                List<MenuItem> menu = MenuOf(SplitArrowOf(order));

                // AutoCAD's nine items, those we have: the first five run what the Modify panel's split ran, in the same order.
                Assert.Equal(
                    new[] { "DRAWORDER F", "DRAWORDER B", "DRAWORDER A", "DRAWORDER U" },
                    menu.Take(4).Select(i => (string)i.CommandParameter)
                );
                Assert.Equal("TEXTTOFRONT T", menu[4].CommandParameter);
                Assert.IsType<Separator>(items[4]);
                Assert.Same(model.StartCommandWithOptionCommand, order.Command);
            });
        }

        [Fact]
        public void TheSlideOutOfAnnotationHoldsFourRows_ATileAndACombo_ForTextDimensionMultileaderAndTableStyle()
        {
            OnHome(Full, (model, workspace, strip) =>
                AssertSlideOut(Panel(strip, "Annotation"), AnnotationSlideOut, null)
            );
        }

        [Fact]
        public void TheSlideOutOfBlockHoldsDefineManageSynchronizeAttributeDisplayAndSetBasePoint_AsOneRowOfTiles()
        {
            OnHome(Full, (model, workspace, strip) => AssertSlideOut(Panel(strip, "Block"), BlockSlideOut, null));
        }

        [Fact]
        public void TheAttributeDisplayTileHoldsNormalOnAndOff_StartingAttdisp()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel block = Panel(strip, "Block");
                OpenSlideOut(block);
                ButtonBase display = Assert.Single(Descendants(block.SlideOut).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "ATTDISP"
                );

                AssertMenu(model, SplitArrowOf(display), new[] { new[] { "Normal", "On", "Off" } }, "ATTDISP");
            });
        }

        [Fact]
        public void TheSlideOutOfPropertiesHoldsAPlotStyleComboGreyedForAColourDependentDrawing_ATransparencyControl_AndListWithText()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel properties = Panel(strip, "Properties");
                OpenSlideOut(properties);
                List<FrameworkElement> all = Descendants(properties.SlideOut).OfType<FrameworkElement>().ToList();

                // Plot Style: the drawing has no named plot style table, so the box is greyed, as AutoCAD's.
                ComboBox plot = Assert.Single(all.OfType<ComboBox>(), c => Words(c).Contains("lot"));
                Assert.False(plot.IsEnabled);

                // Transparency of new objects: a box or a slider, named for screen readers or by its tooltip.
                Assert.Contains(all.Where(e => e is Slider || e is TextBox || (e is ComboBox && e != plot)), e =>
                    Words(e).Contains("ransparency")
                );

                ButtonBase list = Assert.Single(all.OfType<ButtonBase>(), b => CommandWord(b) == "LIST");
                Assert.Equal("List", NameOf(list));
                Assert.True(HasText(list));
                Assert.True(list.IsEnabled);

                // Plot Style over Transparency over List, rows 24 apart.
                FrameworkElement transparency = all.First(e =>
                    (e is Slider || e is TextBox || (e is ComboBox && e != plot)) && Words(e).Contains("ransparency")
                );
                Assert.Equal(Pitch, Top(transparency, properties.SlideOut) - Top(plot, properties.SlideOut), 0.5);
                Assert.Equal(Pitch, Top(list, properties.SlideOut) - Top(transparency, properties.SlideOut), 0.5);
            });
        }

        [Fact]
        public void TheSlideOutOfGroupsHoldsGroupManagerWithText()
        {
            OnHome(Full, (model, workspace, strip) => AssertSlideOut(Panel(strip, "Groups"), GroupsSlideOut, null));
        }

        [Fact]
        public void TheSlideOutOfUtilitiesHoldsIdPointAndPointStyleWithText_OneUnderTheOther()
        {
            OnHome(Full, (model, workspace, strip) =>
                AssertSlideOut(Panel(strip, "Utilities"), UtilitiesSlideOut, null)
            );
        }

        #endregion

        #region The common metrics

        [Fact]
        public void EveryPanelIs98HighAndTheStripWithIt()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                Assert.Equal(PanelHeight, strip.ActualHeight, 0.5);
                Assert.All(Panels(strip), p => Assert.Equal(PanelHeight, p.ActualHeight, 0.5));
            });
        }

        [Fact]
        public void TheTitleBandOfEveryPanelIs18High_UnderItsBody()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                foreach (RibbonPanel panel in Panels(strip))
                {
                    var inline = (Decorator)panel.Template.FindName("PART_Inline", panel);
                    double bodyBottom = inline.TranslatePoint(new Point(0, inline.ActualHeight), panel).Y;

                    Assert.Equal(TitleBand, panel.ActualHeight - bodyBottom, 0.5);
                }
            });
        }

        [Fact]
        public void TheLargeButtonsOfHomeAre72High_ASplitOneWithItsArrowIncluded()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                // Line, Polyline, Circle, Arc; Text, Dimension; Insert; Match Properties; Group; Measure; Paste; Zoom Extents, Pan.
                var large = new List<(RibbonPanel Panel, int Index)>
                {
                    (Panel(strip, "Draw"), 0),
                    (Panel(strip, "Draw"), 1),
                    (Panel(strip, "Draw"), 2),
                    (Panel(strip, "Draw"), 3),
                    (Panel(strip, "Annotation"), 0),
                    (Panel(strip, "Annotation"), 1),
                    (Panel(strip, "Block"), 0),
                    (Panel(strip, "Groups"), 0),
                    (Panel(strip, "Utilities"), 0),
                    (Panel(strip, "Clipboard"), 0),
                    (Panel(strip, "View"), 0),
                    (Panel(strip, "View"), 1),
                };

                foreach ((RibbonPanel panel, int index) in large)
                {
                    var button = (ButtonBase)BodyControls(panel)[index];

                    Assert.Equal(LargeHeight, LargeHeightOf(button), 0.5);
                }

                ButtonBase match = Assert.Single(Descendants(Panel(strip, "Properties").Body).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "MATCHPROP"
                );
                Assert.Equal(LargeHeight, match.ActualHeight, 0.5);
            });
        }

        [Fact]
        public void TheIconAndTheFirstLabelLineOfEveryLargeButtonStandAtOneHeight_PlainOrSplit()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel draw = Panel(strip, "Draw");
                List<FrameworkElement> body = BodyControls(draw);
                var line = (ButtonBase)body[0];
                var circle = (ButtonBase)body[2];
                ButtonBase circleArrow = SplitArrowOf(circle);

                double iconTop = Top(Descendants(line).OfType<IconView>().Single(), draw.Body);
                double labelTop = Top(Descendants(line).OfType<TextBlock>().Single(t => t.Text == "Line"), draw.Body);

                // A split: its icon is the top button's, its label the first line of the arrow's.
                Assert.Equal(iconTop, Top(Descendants(circle).OfType<IconView>().Single(), draw.Body), 0.5);
                Assert.Equal(
                    labelTop,
                    Top(Descendants(circleArrow).OfType<TextBlock>().First(t => t.Text == "Circle"), draw.Body),
                    0.5
                );

                // And the others, plain or split, of every panel stand there too.
                foreach (
                    (string title, int index) in new[]
                    {
                        ("Draw", 1),
                        ("Draw", 3),
                        ("Annotation", 0),
                        ("Annotation", 1),
                        ("Block", 0),
                        ("Groups", 0),
                        ("Utilities", 0),
                        ("Clipboard", 0),
                        ("View", 0),
                        ("View", 1),
                    }
                )
                {
                    RibbonPanel panel = Panel(strip, title);
                    var button = (ButtonBase)BodyControls(panel)[index];
                    ButtonBase arrow = SplitArrowOf(button);
                    DependencyObject labelRoot = arrow ?? button;
                    double icon = Top(Descendants(button).OfType<IconView>().First(), panel.Body);
                    double label = Top(
                        Descendants(labelRoot).OfType<TextBlock>().First(t => !string.IsNullOrWhiteSpace(t.Text)),
                        panel.Body
                    );

                    Assert.True(
                        Math.Abs(icon - iconTop) <= 0.5,
                        title + " " + index + ": its icon is at " + icon + ", not " + iconTop + "."
                    );
                    Assert.True(
                        Math.Abs(label - labelTop) <= 0.5,
                        title + " " + index + ": its label is at " + label + ", not " + labelTop + "."
                    );
                }

                ButtonBase match = Assert.Single(Descendants(Panel(strip, "Properties").Body).OfType<ButtonBase>(), b =>
                    CommandWord(b) == "MATCHPROP"
                );
                Assert.Equal(
                    iconTop,
                    Top(Descendants(match).OfType<IconView>().First(), Panel(strip, "Properties").Body),
                    0.5
                );
                Assert.Equal(
                    labelTop,
                    Top(
                        Descendants(match).OfType<TextBlock>().First(t => !string.IsNullOrWhiteSpace(t.Text)),
                        Panel(strip, "Properties").Body
                    ),
                    0.5
                );
            });
        }

        [Fact]
        public void TheIconOnlyTilesOfHomeAre24By22_ThoseOfAColumnOneUnderTheOther24Apart()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                foreach (
                    (string title, int first) in new[]
                    {
                        ("Modify", 9),
                        ("Groups", 1),
                        ("Utilities", 1),
                        ("Clipboard", 1),
                    }
                )
                {
                    RibbonPanel panel = Panel(strip, title);
                    List<FrameworkElement> tiles = BodyControls(panel).Skip(first).ToList();

                    Assert.Equal(3, tiles.Count);

                    for (int i = 0; i < 3; i++)
                    {
                        Assert.Equal(TileWidth, tiles[i].ActualWidth, 0.5);
                        Assert.Equal(TileHeight, tiles[i].ActualHeight, 0.5);
                        Assert.Equal(Top(tiles[0], panel.Body) + (i * Pitch), Top(tiles[i], panel.Body), 0.5);
                        Assert.False(HasText(tiles[i]));
                        Assert.False(
                            string.IsNullOrEmpty(AutomationProperties.GetName(tiles[i])),
                            title + " tile " + i + " has no name."
                        );
                        Assert.False(
                            string.IsNullOrEmpty(tiles[i].ToolTip as string),
                            title + " tile " + i + " has no tooltip."
                        );
                    }
                }
            });
        }

        [Fact]
        public void ASplitTileOfDrawIsIconOnlyAnd35By22_ThreeOfThemOneUnderTheOther24Apart()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                RibbonPanel draw = Panel(strip, "Draw");
                List<FrameworkElement> tiles = BodyControls(draw).Skip(4).ToList();

                for (int i = 0; i < 3; i++)
                {
                    ButtonBase arrow = SplitArrowOf((ButtonBase)tiles[i]);
                    Assert.NotNull(arrow);
                    Rect tile = Union(tiles[i], arrow, draw.Body);

                    Assert.Equal(SplitTileWidth, tile.Width, 0.5);
                    Assert.Equal(TileHeight, tile.Height, 0.5);
                    Assert.Equal(Top(tiles[0], draw.Body) + (i * Pitch), tile.Top, 0.5);
                }
            });
        }

        [Fact]
        public void TheSmallButtonsWithTextAre22HighInRows24Apart_ThoseOfModifyAnnotationAndBlock()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                foreach (
                    (string title, int first, int count) in new[]
                    {
                        ("Modify", 0, 3),
                        ("Modify", 3, 3),
                        ("Modify", 6, 3),
                        ("Annotation", 2, 3),
                        ("Block", 1, 3),
                    }
                )
                {
                    RibbonPanel panel = Panel(strip, title);
                    List<FrameworkElement> column = BodyControls(panel).Skip(first).Take(count).ToList();

                    for (int i = 0; i < count; i++)
                    {
                        Assert.Equal(TileHeight, column[i].ActualHeight, 0.5);
                        Assert.Equal(Top(column[0], panel.Body) + (i * Pitch), Top(column[i], panel.Body), 0.5);
                    }
                }
            });
        }

        [Fact]
        public void TheLabelOfALargeButtonRunsToAtMostTwoLines_NeverBrokenInsideAWord()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                foreach (RibbonPanel panel in Panels(strip).Where(p => p.Title != "Layers"))
                {
                    foreach (
                        TextBlock label in Descendants(panel.Body)
                            .OfType<ButtonBase>()
                            .Where(b =>
                                b.ActualHeight >= 30
                                || (
                                    IsArrow(b)
                                    && VisualTreeHelper.GetParent(b) is Panel p
                                    && ((FrameworkElement)p.Children[0]).ActualHeight >= 30
                                )
                            )
                            .SelectMany(b => Descendants(b).OfType<TextBlock>())
                            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                    )
                    {
                        var one = new TextBlock
                        {
                            Text = "Ag",
                            FontFamily = label.FontFamily,
                            FontSize = label.FontSize,
                            FontWeight = label.FontWeight,
                            FontStyle = label.FontStyle,
                        };
                        one.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                        Assert.True(
                            label.ActualHeight <= (2 * one.DesiredSize.Height) + 1,
                            "\"" + label.Text + "\" runs to more than two lines."
                        );

                        foreach (string word in label.Text.Split(' ', '/'))
                        {
                            var single = new TextBlock
                            {
                                Text = word,
                                FontFamily = label.FontFamily,
                                FontSize = label.FontSize,
                                FontWeight = label.FontWeight,
                                FontStyle = label.FontStyle,
                            };
                            single.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                            Assert.True(
                                single.DesiredSize.Width <= label.ActualWidth + 1.0,
                                "\"" + word + "\" of \"" + label.Text + "\" is broken."
                            );
                        }
                    }
                }
            });
        }

        [Fact]
        public void TheTilesOfTheSlideOutsAre24By22AndTheTextOnesAre22High_RowsOfASlideOut24Apart()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                foreach (
                    RibbonPanel panel in new[] { "Draw", "Modify", "Annotation", "Block", "Utilities" }.Select(t =>
                        Panel(strip, t)
                    )
                )
                {
                    OpenSlideOut(panel);
                    List<FrameworkElement> controls = SlideOutControls(panel);

                    foreach (FrameworkElement c in controls)
                    {
                        Assert.Equal(TileHeight, c.ActualHeight, 0.5);

                        if (c is ButtonBase tile && !HasText(tile) && SplitArrowOf(tile) == null)
                        {
                            Assert.Equal(TileWidth, tile.ActualWidth, 0.5);
                        }
                    }

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

        // F full, M medium, C collapsed, in the order of HomeOrder. D1318: AutoCAD's widest and narrowest ribbons are asserted
        // exactly (nothing shrinks; Draw full, Modify medium and every other panel collapsed). At a width between, the number of
        // steps taken lies between AutoCAD's at that width and AutoCAD's at the next narrower one: our Home is about 80 wider than
        // AutoCAD's (our font, our View panel of 95 against Base of 62), so a step comes a row early, never in another order.
        [Fact]
        public void AtAutoCadsWidestAndNarrowestRibbons_HomeIsAllFullOrAllShrunkAsAutoCads()
        {
            OnHome(1904, (model, workspace, strip) =>
            {
                Assert.Equal("FFFFFFFFFF", Sizes(strip));
                Assert.Equal(0, strip.StepsTaken);
            });
            OnHome(884, (model, workspace, strip) =>
            {
                Assert.Equal("FMCCCCCCCC", Sizes(strip));
                Assert.Equal(13, strip.StepsTaken);
            });
        }

        [Theory]
        [InlineData(1584, 2, 4)]
        [InlineData(1424, 4, 10)]
        [InlineData(1264, 10, 12)]
        [InlineData(1084, 12, 13)]
        public void AtAMiddleWidth_HomeTakesAutoCadsStepsOrUpToTheNextWidthsAhead(double width, int low, int high)
        {
            OnHome(width, (model, workspace, strip) => Assert.InRange(strip.StepsTaken, low, high));
        }

        private static string Sizes(RibbonStrip strip) =>
            new string(
                Panels(strip)
                    .Select(p =>
                        p.Size == RibbonPanelSize.Full ? 'F'
                        : p.Size == RibbonPanelSize.Medium ? 'M'
                        : 'C'
                    )
                    .ToArray()
            );

        [Fact]
        public void NarrowingTheStripPixelByPixel_TakesTheStepsInAutoCadsOrder_DrawNeverShrinksAndModifyNeverCollapses()
        {
            OnHome(Full, (model, workspace, strip) =>
            {
                List<RibbonPanel> panels = Panels(strip);
                var steps = Squeeze(HomeSteps)
                    .Split(';')
                    .Select(s =>
                        (
                            Title: s.Split(':')[0],
                            Size: (RibbonPanelSize)Enum.Parse(typeof(RibbonPanelSize), s.Split(':')[1])
                        )
                    )
                    .ToList();

                // The widest strip at which each step has been taken, found by narrowing one pixel at a time from 1 920.
                var taken = new Dictionary<int, double>();
                var last = panels.ToDictionary(p => p.Title, p => RibbonPanelSize.Full);

                for (double width = 1920; width >= 700; width--)
                {
                    strip.Measure(new Size(width, 960));

                    foreach (RibbonPanel panel in panels)
                    {
                        Assert.True(panel.Size >= last[panel.Title], panel.Title + " grew back at " + width + ".");

                        for (int k = 0; k < steps.Count; k++)
                        {
                            if (steps[k].Title == panel.Title && panel.Size >= steps[k].Size && !taken.ContainsKey(k))
                            {
                                taken[k] = width;
                            }
                        }

                        last[panel.Title] = panel.Size;
                    }

                    Assert.Equal(RibbonPanelSize.Full, panels[0].Size);
                    Assert.NotEqual(RibbonPanelSize.Collapsed, panels[1].Size);
                }

                // Every step is taken above 700, and one never before the step ahead of it.
                Assert.Equal(steps.Count, taken.Count);

                for (int k = 1; k < steps.Count; k++)
                {
                    Assert.True(
                        taken[k] <= taken[k - 1],
                        steps[k].Title
                            + ":"
                            + steps[k].Size
                            + " ("
                            + taken[k]
                            + ") came before "
                            + steps[k - 1].Title
                            + ":"
                            + steps[k - 1].Size
                            + " ("
                            + taken[k - 1]
                            + ")."
                    );
                }
            });
        }

        [Fact]
        public void InAMediumPanel_TheSmallButtonsOfModifyAnnotationAndBlockShowNoTextAndKeepTheirNames_TheLargeOnesKeepTheirLabel()
        {
            OnHome(1264, (model, workspace, strip) =>
            {
                foreach (
                    (string title, string[] keys, int first) in new[]
                    {
                        ("Modify", ModifyBody, 0),
                        ("Annotation", AnnotationBody, 2),
                        ("Block", BlockBody, 1),
                    }
                )
                {
                    RibbonPanel panel = Panel(strip, title);
                    Assert.Equal(RibbonPanelSize.Medium, panel.Size);

                    List<FrameworkElement> controls = BodyControls(panel);
                    AssertControls(keys, controls);

                    foreach (FrameworkElement small in controls.Skip(first))
                    {
                        Assert.False(HasText(small), NameOf(small) + " shows text in a medium panel.");
                        Assert.False(
                            string.IsNullOrEmpty(AutomationProperties.GetName(small)),
                            NameOf(small) + " has no name."
                        );
                        Assert.Equal(TileHeight, small.ActualHeight, 0.5);
                    }

                    // The panel's large buttons still read: Dimension of Annotation by its text, Insert of Block by its ▾ label.
                    foreach (FrameworkElement large in controls.Take(first))
                    {
                        Assert.True(
                            HasText(large) || SplitArrowOf((ButtonBase)large) != null,
                            NameOf(large) + " lost its label."
                        );
                    }
                }
            });
        }

        #endregion

        #region Finding things in the window

        /// <summary>The drawing open in the window drawn offscreen, laid out so that Home's strip is a width wide, on a thread of its own.</summary>
        private static void OnHome(double stripWidth, Action<MainViewModel, CadWorkspace, RibbonStrip> check)
        {
            using (var folder = new TestFolder())
            {
                string file = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
                        _ = PackUriHelper.UriSchemePack;
                        var model = new MainViewModel();
                        Assert.True(model.Open(file));
                        var workspace = new CadWorkspace { DataContext = model };
                        // The workspace shows these dialogs in windows; a pick here answers each with Cancel instead.
                        model.ShowBoundaryCreation = dialog => false;
                        model.ShowCheckSpelling = dialog => { };
                        model.ShowViewports = dialog => false;

                        try
                        {
                            // The window gives its strip a width less what is around it: read at a wide window, then laid out again.
                            Layout(workspace, 3200);
                            RibbonStrip strip = Descendants(workspace)
                                .OfType<RibbonStrip>()
                                .Single(s => ReferenceEquals(s.DataContext, model));
                            double around = 3200 - strip.ActualWidth;
                            Layout(workspace, stripWidth + around);
                            Pump(workspace);
                            check(model, workspace, strip);
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

        private static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 960));
            element.Arrange(new Rect(0, 0, width, 960));
            element.UpdateLayout();
        }

        private static void Pump(DispatcherObject element) =>
            element.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

        private static string Squeeze(string text) => text.Replace(" ", string.Empty);

        private static List<RibbonPanel> Panels(RibbonStrip strip) => strip.Children.OfType<RibbonPanel>().ToList();

        private static RibbonPanel Panel(RibbonStrip strip, string title) =>
            Assert.Single(Panels(strip), p => p.Title == title);

        /// <summary>The slide-out dropped under the panel by its title, laid out, so that the positions of its controls can be read.</summary>
        private static void OpenSlideOut(RibbonPanel panel)
        {
            Assert.NotNull(panel.SlideOut);

            if (panel.IsDropDownOpen)
            {
                return;
            }

            var popup = (Popup)panel.Template.FindName("PART_Popup", panel);
            panel.IsDropDownOpen = true;
            Pump(popup);
            Assert.True(panel.IsDropDownOpen);
            popup.Child.Measure(new Size(2000, 960));
            popup.Child.Arrange(new Rect(0, 0, popup.Child.DesiredSize.Width, popup.Child.DesiredSize.Height));
            ((FrameworkElement)popup.Child).UpdateLayout();
        }

        /// <summary>
        /// The controls of a part of a panel: its buttons, toggles and combo boxes, not the arrow of a split nor what is inside a
        /// combo box.
        /// </summary>
        private static List<FrameworkElement> ControlsOf(DependencyObject root) =>
            Descendants(root)
                .OfType<FrameworkElement>()
                .Where(e => ((e is ButtonBase b && !IsArrow(b)) || e is ComboBox) && !InsideCompound(e))
                .ToList();

        /// <summary>The body's controls in reading order of its columns: left to right, each column top to bottom.</summary>
        private static List<FrameworkElement> BodyControls(RibbonPanel panel)
        {
            var columns = new List<List<FrameworkElement>>();

            foreach (
                FrameworkElement control in ControlsOf(panel.Body)
                    .OrderBy(c => Left(c, panel.Body))
                    .ThenBy(c => Top(c, panel.Body))
            )
            {
                List<FrameworkElement> column = columns.LastOrDefault();

                if (column == null || Left(control, panel.Body) - Left(column[0], panel.Body) > 8)
                {
                    columns.Add(column = new List<FrameworkElement>());
                }

                column.Add(control);
            }

            return columns.SelectMany(c => c.OrderBy(e => Top(e, panel.Body))).ToList();
        }

        private static List<FrameworkElement> SlideOutControls(RibbonPanel panel) => ControlsOf(panel.SlideOut);

        /// <summary>Controls in rows, top to bottom, each row left to right.</summary>
        private static List<List<FrameworkElement>> Rows(List<FrameworkElement> controls, UIElement relativeTo)
        {
            var rows = new List<List<FrameworkElement>>();

            foreach (
                FrameworkElement control in controls.OrderBy(c => Top(c, relativeTo)).ThenBy(c => Left(c, relativeTo))
            )
            {
                List<FrameworkElement> row = rows.LastOrDefault();

                if (row == null || Top(control, relativeTo) - Top(row[0], relativeTo) > 6)
                {
                    rows.Add(row = new List<FrameworkElement>());
                }

                row.Add(control);
            }

            return rows.Select(r => r.OrderBy(c => Left(c, relativeTo)).ToList()).ToList();
        }

        private static void AssertSlideOut(RibbonPanel panel, string[][] expected, Func<FrameworkElement, bool> keep)
        {
            OpenSlideOut(panel);
            List<FrameworkElement> controls = SlideOutControls(panel);

            if (keep != null)
            {
                controls = controls.Where(keep).ToList();
            }

            List<List<FrameworkElement>> rows = Rows(controls, panel.SlideOut);

            Assert.Equal(expected.Length, rows.Count);

            for (int r = 0; r < expected.Length; r++)
            {
                AssertControls(expected[r], rows[r]);
            }
        }

        /// <summary>Controls against "name|command" lines: where a part is empty the control's own is taken, so that it is not read.</summary>
        private static void AssertControls(string[] expected, List<FrameworkElement> actual)
        {
            Assert.Equal(expected.Length, actual.Count);
            Assert.Equal(expected.Select((line, i) => Wanted(line, actual[i])), actual.Select(Describe));
        }

        private static string Wanted(string line, FrameworkElement control)
        {
            if (line == "combo")
            {
                return line;
            }

            string[] parts = line.Split('|');
            ButtonBase button = control as ButtonBase;
            return (parts[0].Length > 0 ? parts[0] : NameOf(control))
                + "|"
                + (parts[1].Length > 0 ? parts[1] : CommandWord(button));
        }

        private static string Describe(FrameworkElement control) =>
            control is ComboBox ? "combo" : NameOf(control) + "|" + CommandWord(control as ButtonBase);

        /// <summary>The first word of the command a button starts: CIRCLE of "CIRCLE 2P"; empty where it starts none by name.</summary>
        private static string CommandWord(FrameworkElement control) =>
            control is ButtonBase button
                ? ((button.CommandParameter as string) ?? string.Empty).Split(' ')[0]
                : string.Empty;

        /// <summary>The words a control is known by: its name for screen readers, or its text where it has no other.</summary>
        private static string NameOf(FrameworkElement control)
        {
            string name = AutomationProperties.GetName(control);

            if (string.IsNullOrEmpty(name) && control is ContentControl content)
            {
                name = content.Content as string;
            }

            return name ?? string.Empty;
        }

        /// <summary>Everything a control says of itself, for finding what it is: its name, its tooltip, its content.</summary>
        private static string Words(FrameworkElement control) =>
            NameOf(control)
            + " "
            + (control.ToolTip as string)
            + " "
            + (AutomationProperties.GetHelpText(control) ?? string.Empty);

        private static bool HasText(FrameworkElement control) =>
            Descendants(control).OfType<TextBlock>().Any(t => !string.IsNullOrWhiteSpace(t.Text));

        /// <summary>The label a split shows, its texts run together without spaces: "Circle▾", whether the ▾ stands on a line of its own or not.</summary>
        private static string Label(DependencyObject split) =>
            string.Concat(
                    Descendants(split).OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t))
                )
                .Replace(" ", string.Empty);

        /// <summary>The arrow of a split: the second child of the panel holding the button, as every split of the ribbon is built.</summary>
        private static ButtonBase SplitArrowOf(ButtonBase main) =>
            VisualTreeHelper.GetParent(main) is Panel panel
            && panel.Children.Count == 2
            && ReferenceEquals(panel.Children[0], main)
            && panel.Children[1] is ButtonBase arrow
            && IsArrow(arrow)
                ? arrow
                : null;

        private static bool IsArrow(ButtonBase button) =>
            button.CommandParameter == null
            && (
                button.ContextMenu != null
                || (AutomationProperties.GetName(button) ?? string.Empty).EndsWith("gallery", StringComparison.Ordinal)
            );

        private static bool InsideCompound(DependencyObject element)
        {
            for (
                DependencyObject at = VisualTreeHelper.GetParent(element);
                at != null;
                at = VisualTreeHelper.GetParent(at)
            )
            {
                if (at is ComboBox || at is Slider || at is TextBoxBase || at is ScrollBar)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The height of a large button: its own, or for a split the frame round its top and its arrow.</summary>
        private static double LargeHeightOf(ButtonBase button)
        {
            ButtonBase arrow = SplitArrowOf(button);

            if (arrow == null)
            {
                return button.ActualHeight;
            }

            var panel = (FrameworkElement)VisualTreeHelper.GetParent(button);
            return VisualTreeHelper.GetParent(panel) is Border frame ? frame.ActualHeight : panel.ActualHeight;
        }

        private static Rect Union(FrameworkElement first, FrameworkElement second, UIElement relativeTo)
        {
            var a = new Rect(
                first.TranslatePoint(new Point(0, 0), relativeTo),
                new Size(first.ActualWidth, first.ActualHeight)
            );
            var b = new Rect(
                second.TranslatePoint(new Point(0, 0), relativeTo),
                new Size(second.ActualWidth, second.ActualHeight)
            );
            return Rect.Union(a, b);
        }

        /// <summary>The menu of a split's arrow checked: its items in groups parted by separators, and the command each starts.</summary>
        private static void AssertMenu(
            MainViewModel model,
            ButtonBase arrow,
            string[][] groups,
            string command,
            string[] commands = null
        )
        {
            Assert.NotNull(arrow);
            ContextMenu menu = arrow.ContextMenu;
            menu.PlacementTarget = arrow;
            Pump(arrow);

            var actual = new List<List<MenuItem>> { new List<MenuItem>() };

            foreach (object item in menu.Items)
            {
                if (item is Separator)
                {
                    actual.Add(new List<MenuItem>());
                }
                else
                {
                    actual.Last().Add((MenuItem)item);
                }
            }

            Assert.Equal(groups, actual.Select(g => g.Select(Norm).ToArray()));

            List<MenuItem> flat = actual.SelectMany(g => g).ToList();
            string[] words = flat.Select(i => ((string)i.CommandParameter ?? string.Empty).Split(' ')[0]).ToArray();
            Assert.Equal(commands ?? flat.Select(i => command).ToArray(), words);

            // Each item, picked, starts its command; Esc ends it.
            foreach (MenuItem item in flat)
            {
                int before = model.History.Count;

                Pick(item);

                Assert.Contains(
                    "Command: " + ((string)item.CommandParameter).Split(' ')[0],
                    model.History.Skip(before)
                );
                Assert.DoesNotContain(model.History.Skip(before), l =>
                    l.StartsWith("Unknown command", StringComparison.Ordinal)
                );
                model.Escape();
            }
        }

        /// <summary>The name of a menu item, as the spec writes it: no commas, no ellipsis, one space between words.</summary>
        private static string Norm(MenuItem item) =>
            string.Join(
                " ",
                ((string)item.Header ?? string.Empty)
                    .Replace(",", string.Empty)
                    .Replace("…", string.Empty)
                    .Replace("...", string.Empty)
                    .Replace("Blend Curves", "Blend")
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            );

        /// <summary>The items of a button's menu, the menu taking the window's model from the button, its items bound at the dispatcher's next turn.</summary>
        private static List<MenuItem> MenuOf(ButtonBase opener)
        {
            ContextMenu menu = opener.ContextMenu;
            menu.PlacementTarget = opener;
            Pump(opener);
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
            Pump(item);
        }

        private static double Left(FrameworkElement element, UIElement relativeTo) =>
            element.TranslatePoint(new Point(0, 0), relativeTo).X;

        private static double Top(FrameworkElement element, UIElement relativeTo) =>
            element.TranslatePoint(new Point(0, 0), relativeTo).Y;

        private static double CenterY(FrameworkElement element, UIElement relativeTo) =>
            Top(element, relativeTo) + (element.ActualHeight / 2);

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

        #endregion
    }
}
