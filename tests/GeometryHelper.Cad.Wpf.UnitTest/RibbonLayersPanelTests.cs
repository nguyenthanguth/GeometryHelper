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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Home › Layers as AutoCAD 2021's, found in the window drawn offscreen: the body (Layer Properties, the layer box, and
    /// two rows of five tiles), the layer box drawing each layer as three state glyphs that a click toggles with the list
    /// left open, the title "Layers ▾" with its slide-out (the Layer State box and seven tiles), and the medium panel with
    /// the texts of Make Current and Match Layer gone. The drawing has the layers 0 (current), WALLS, COLUMNS (off, frozen
    /// and locked), ALONE (off only) and KEYED (locked only); its pixel sizes are AutoCAD's at 100 %.
    /// </summary>
    public sealed class RibbonLayersPanelTests
    {
        // Home just fits 2 300 pixels with the old Layers panel, so a wider one would shrink a panel there; at 3 200 every panel
        // of Home is drawn full.
        private const double Wide = 3200;

        // The shown name of each glyph the layer box draws, as AutomationProperties.Name of the glyph's element.
        private static readonly string[] GlyphNames =
        {
            "BulbOn",
            "BulbOff",
            "Sun",
            "Snowflake",
            "LockOpen",
            "LockClosed",
        };

        // Row 2 of the body, then row 3, each tile with the command it starts; the last of each row has its text.
        private static readonly (string Name, string Command)[] RowTwo =
        {
            ("Off", "LAYOFF"),
            ("Isolate", "LAYISO"),
            ("Freeze", "LAYFRZ"),
            ("Lock", "LAYLCK"),
            ("Make Current", "LAYMCUR"),
        };

        private static readonly (string Name, string Command)[] RowThree =
        {
            ("Turn All Layers On", "LAYON"),
            ("Unisolate", "LAYUNISO"),
            ("Thaw All Layers", "LAYTHW"),
            ("Unlock", "LAYULK"),
            ("Match Layer", "LAYMCH"),
        };

        // The tiles of the slide-out, in AutoCAD's order.
        private static readonly (string Name, string Command)[] SlideOutTiles =
        {
            ("Previous", "LAYERP"),
            ("Change to Current Layer", "LAYCUR"),
            ("Copy Objects to New Layer", "COPYTOLAYER"),
            ("Layer Walk", "LAYWALK"),
            ("VP Freeze in All Viewports except Current", "LAYVPI"),
            ("Merge", "LAYMRG"),
            ("Delete", "LAYDEL"),
        };

        private const string Title = "Layers ▾";
        private const string Unsaved = "Unsaved Layer State";
        private const string CurrentRefused = "Cannot freeze layer \"0\".  It is the CURRENT layer.";

        #region The drawing of the Layers panel

        /// <summary>
        /// The room (layers 0, WALLS, COLUMNS) with 0 current, COLUMNS off, frozen and locked, ALONE (green) off and nothing
        /// else, and KEYED (blue) locked and nothing else: every state glyph on its own and the three together.
        /// </summary>
        private static DxfDatabase Layers()
        {
            DxfDatabase db = TestFiles.Room(out _, out _);
            db.Clayer = "0";
            DxfLayerTableRecord columns = db.LayerTable["COLUMNS"];
            columns.IsOff = true;
            columns.IsFrozen = true;
            columns.IsLocked = true;
            db.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "ALONE",
                    ColorIndex = 3,
                    IsOff = true,
                }
            );
            db.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "KEYED",
                    ColorIndex = 5,
                    IsLocked = true,
                }
            );
            return db;
        }

        #endregion

        [Fact]
        public void TheBodyHoldsLayerPropertiesTheLayerBoxAndTenTiles_InTwoRowsOfFiveUnderTheBox_AsAutoCadsLayers()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                List<Button> buttons = BodyButtons(panel);

                Assert.Equal(
                    new[] { "Layer Properties" }
                        .Concat(RowTwo.Concat(RowThree).Select(t => t.Name))
                        .OrderBy(n => n, StringComparer.Ordinal),
                    buttons.Select(NameOf).OrderBy(n => n, StringComparer.Ordinal)
                );
                var box = (ComboBox)workspace.FindName("LayerBox");
                Button big = Named(buttons, "Layer Properties");

                // Column 1 is Layer Properties, column 2 holds the box over the rows of tiles.
                Assert.Same(model.LayerManagerCommand, big.Command);
                Assert.True(Left(big, panel) + big.ActualWidth <= Left(box, panel) + 0.5);
                Assert.Equal(Left(box, panel), Left(Named(buttons, "Off"), panel), 0.5);

                // Each row's tiles stand left to right in the order given, level with one another; row 3 is under row 2 column by
                // column, and the box is above them both.
                for (int column = 0; column < 5; column++)
                {
                    Button upper = Named(buttons, RowTwo[column].Name);
                    Button lower = Named(buttons, RowThree[column].Name);
                    Assert.Equal(Top(Named(buttons, "Off"), panel), Top(upper, panel), 0.5);
                    Assert.Equal(Top(Named(buttons, "Turn All Layers On"), panel), Top(lower, panel), 0.5);
                    Assert.Equal(Left(upper, panel), Left(lower, panel), 0.5);

                    if (column > 0)
                    {
                        Assert.True(Left(Named(buttons, RowTwo[column - 1].Name), panel) < Left(upper, panel));
                    }
                }

                Assert.True(Top(box, panel) < Top(Named(buttons, "Off"), panel));
                Assert.True(Top(Named(buttons, "Off"), panel) < Top(Named(buttons, "Turn All Layers On"), panel));
            });
        }

        [Fact]
        public void EightOfTheTilesShowOnlyTheirIcon_NamedForScreenReadersAndTheTooltip_AndMakeCurrentAndMatchLayerKeepTheirText()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                List<Button> buttons = BodyButtons(panel);

                // Off, Isolate, Freeze, Lock and the four of row 3 that have no text: AutoCAD's icon-only tiles.
                foreach ((string name, string command) in RowTwo.Take(4).Concat(RowThree.Take(4)))
                {
                    Button tile = Named(buttons, name);
                    Assert.Equal(name, AutomationProperties.GetName(tile));
                    Assert.Equal(name + " (" + command + ")", tile.ToolTip);
                    Assert.DoesNotContain(Descendants(tile).OfType<TextBlock>(), t => !string.IsNullOrEmpty(t.Text));
                    Assert.Single(Descendants(tile).OfType<IconView>());
                }

                foreach ((string name, string command) in new[] { RowTwo[4], RowThree[4] })
                {
                    Button tile = Named(buttons, name);
                    Assert.Contains(Descendants(tile).OfType<TextBlock>(), t => t.Text == name);
                    Assert.Single(Descendants(tile).OfType<IconView>());
                    Assert.Contains("(" + command + ")", (string)tile.ToolTip);
                }
            });
        }

        [Fact]
        public void EveryTileOfTheBodyStartsItsCommandThroughStartCommand_AndLayerPropertiesOpensTheLayerManager()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                List<Button> buttons = BodyButtons(panel);

                foreach ((string name, string command) in RowTwo.Concat(RowThree))
                {
                    Button tile = Named(buttons, name);
                    Assert.Same(model.StartCommandCommand, tile.Command);
                    Assert.Equal(command, tile.CommandParameter);
                    Press(model, tile.IsEnabled, () => tile.Command.Execute(tile.CommandParameter), command);
                }

                // The Layer manager is modal, so only its wiring is checked: it is the LAYER command of the model.
                Assert.Same(model.LayerManagerCommand, Named(buttons, "Layer Properties").Command);
            });
        }

        [Fact]
        public void TheTilesAre24By22AndTheBoxIs208By22_Rows24Apart_AndLayerPropertiesIs72High_AsAutoCadAt100Percent()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                List<Button> buttons = BodyButtons(panel);
                var box = (ComboBox)workspace.FindName("LayerBox");

                foreach ((string name, _) in RowTwo.Take(4).Concat(RowThree.Take(4)))
                {
                    Button tile = Named(buttons, name);
                    Assert.Equal(24.0, tile.ActualWidth, 0.5);
                    Assert.Equal(22.0, tile.ActualHeight, 0.5);
                }

                Assert.Equal(22.0, Named(buttons, "Make Current").ActualHeight, 0.5);
                Assert.Equal(22.0, Named(buttons, "Match Layer").ActualHeight, 0.5);
                Assert.Equal(208.0, box.ActualWidth, 0.5);
                Assert.Equal(22.0, box.ActualHeight, 0.5);
                Assert.Equal(72.0, Named(buttons, "Layer Properties").ActualHeight, 0.5);
                Assert.Equal(24.0, Top(Named(buttons, "Off"), panel) - Top(box, panel), 0.5);
                Assert.Equal(
                    24.0,
                    Top(Named(buttons, "Turn All Layers On"), panel) - Top(Named(buttons, "Off"), panel),
                    0.5
                );
            });
        }

        [Fact]
        public void PreviousIsNotInTheBody_ItIsInTheSlideOutOnly()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                Assert.DoesNotContain(BodyButtons(panel), b => "LAYERP".Equals(b.CommandParameter));
                Assert.Contains(SlideOutButtons(panel), b => "LAYERP".Equals(b.CommandParameter));
            });
        }

        [Theory]
        [InlineData("0", "BulbOn", "Sun", "LockOpen")]
        [InlineData("WALLS", "BulbOn", "Sun", "LockOpen")]
        [InlineData("COLUMNS", "BulbOff", "Snowflake", "LockClosed")]
        [InlineData("ALONE", "BulbOff", "Sun", "LockOpen")]
        [InlineData("KEYED", "BulbOn", "Sun", "LockClosed")]
        public void TheOpenListDrawsEachLayerWithTheGlyphsOfItsOwnThreeStates_BeforeItsName(
            string layer,
            string onOff,
            string frozen,
            string locked
        )
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                var box = (ComboBox)workspace.FindName("LayerBox");
                FrameworkElement item = ItemOf(box, layer);

                Assert.Equal(new[] { onOff, frozen, locked }, GlyphsOf(item).Select(AutomationProperties.GetName));

                // Name after the glyphs, not before them: the three glyphs, then the colour, then the name.
                TextBlock name = Descendants(item).OfType<TextBlock>().First(t => t.Text == layer);
                Assert.True(
                    Left(name, item) >= Left(GlyphsOf(item).Last(), item) + GlyphsOf(item).Last().ActualWidth - 0.5
                );
            });
        }

        [Fact]
        public void TheClosedBoxShowsTheCurrentLayerZeroWithTheLitBulbTheSunAndTheOpenPadlock()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                var box = (ComboBox)workspace.FindName("LayerBox");

                Assert.False(box.IsDropDownOpen);
                Assert.Equal(new[] { "BulbOn", "Sun", "LockOpen" }, GlyphsOf(box).Select(AutomationProperties.GetName));
            });
        }

        [Fact]
        public void LockingTheCurrentLayer_ShowsTheClosedPadlockInTheClosedBoxAtOnce()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                var box = (ComboBox)workspace.FindName("LayerBox");

                Assert.True(CadLayerEditor.SetLocked(model.Document, "0", true).Succeeded);
                Layout(workspace, Wide);

                Assert.Equal(
                    new[] { "BulbOn", "Sun", "LockClosed" },
                    GlyphsOf(box).Select(AutomationProperties.GetName)
                );
            });
        }

        [Fact]
        public void AClickOnTheFreezeGlyphOfWalls_FreezesWalls_LeavesZeroCurrent_AndLeavesTheListOpen()
        {
            OnLayers(
                Wide,
                (model, workspace, panel) =>
                {
                    var box = (ComboBox)workspace.FindName("LayerBox");

                    Click(Glyph(box, "WALLS", 1));

                    Assert.True(model.Document.Database.LayerTable["WALLS"].IsFrozen);
                    Assert.False(model.Document.Database.LayerTable["WALLS"].IsOff);
                    Assert.False(model.Document.Database.LayerTable["WALLS"].IsLocked);
                    Assert.Equal("0", model.Document.Database.Clayer);
                    Assert.Equal("0", model.CurrentLayer.Name);
                    Assert.True(box.IsDropDownOpen, "The list of layers closed after the click.");

                    // The list drew itself again: the glyph of WALLS is the snowflake now.
                    Assert.Equal("Snowflake", AutomationProperties.GetName(Glyph(box, "WALLS", 1)));
                },
                loaded: true
            );
        }

        [Fact]
        public void AClickOnTheFreezeGlyphOfWalls_IsOneUndoStep_ThatUndoThawsAgain()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                var box = (ComboBox)workspace.FindName("LayerBox");
                CadUndoStep before = model.Document.NextUndo;

                Click(Glyph(box, "WALLS", 1));

                Assert.True(model.Document.Database.LayerTable["WALLS"].IsFrozen);
                Assert.NotSame(before, model.Document.NextUndo);
                Assert.True(model.Undo());
                Assert.False(model.Document.Database.LayerTable["WALLS"].IsFrozen);
                Assert.Same(before, model.Document.NextUndo);
            });
        }

        [Fact]
        public void AClickOnTheFreezeGlyphOfWalls_IsTakenBackByLayerp_AsTheLayerManagersEditsAre()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                var box = (ComboBox)workspace.FindName("LayerBox");

                Click(Glyph(box, "WALLS", 1));
                Assert.True(model.Document.Database.LayerTable["WALLS"].IsFrozen);

                model.Execute("LAYERP");

                Assert.False(model.Document.Database.LayerTable["WALLS"].IsFrozen);
                Assert.Equal("0", model.Document.Database.Clayer);
            });
        }

        [Fact]
        public void AClickOnTheBulbGlyphOfWalls_TurnsWallsOff_AndAClickOnTheBulbOfTheCurrentLayerTurnsItOffToo()
        {
            OnLayers(
                Wide,
                (model, workspace, panel) =>
                {
                    var box = (ComboBox)workspace.FindName("LayerBox");

                    Click(Glyph(box, "WALLS", 0));
                    Assert.True(model.Document.Database.LayerTable["WALLS"].IsOff);
                    Assert.False(model.Document.Database.LayerTable["WALLS"].IsFrozen);

                    // AutoCAD's box turns the current layer off without a word.
                    int before = model.History.Count;
                    Click(Glyph(box, "0", 0));
                    Assert.True(model.Document.Database.LayerTable["0"].IsOff);
                    Assert.Equal("0", model.Document.Database.Clayer);
                    Assert.DoesNotContain(model.History.Skip(before), l =>
                        l.StartsWith("Cannot", StringComparison.Ordinal)
                    );
                    Assert.True(box.IsDropDownOpen, "The list of layers closed after the click.");
                },
                loaded: true
            );
        }

        [Fact]
        public void AClickOnThePadlockGlyphOfWallsLocksIt_AndAClickOnTheSnowflakeOfColumnsThawsItLeavingItOffAndLocked()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                var box = (ComboBox)workspace.FindName("LayerBox");

                Click(Glyph(box, "WALLS", 2));
                Assert.True(model.Document.Database.LayerTable["WALLS"].IsLocked);
                Assert.False(model.Document.Database.LayerTable["WALLS"].IsFrozen);

                Click(Glyph(box, "COLUMNS", 1));
                DxfLayerTableRecord columns = model.Document.Database.LayerTable["COLUMNS"];
                Assert.False(columns.IsFrozen);
                Assert.True(columns.IsOff);
                Assert.True(columns.IsLocked);
                Assert.Equal("0", model.Document.Database.Clayer);
            });
        }

        [Fact]
        public void AClickOnTheFreezeGlyphOfTheCurrentLayer_IsRefusedInTheLayerManagersWords_AndChangesNothing()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                var box = (ComboBox)workspace.FindName("LayerBox");
                CadUndoStep before = model.Document.NextUndo;
                int lines = model.History.Count;

                Click(Glyph(box, "0", 1));

                Assert.Contains(CurrentRefused, model.History.Skip(lines));
                Assert.False(model.Document.Database.LayerTable["0"].IsFrozen);
                Assert.Same(before, model.Document.NextUndo);
                Assert.Equal("0", model.CurrentLayer.Name);
                Assert.Equal("Sun", AutomationProperties.GetName(Glyph(box, "0", 1)));
            });
        }

        [Fact]
        public void TheTitleReadsLayersAndADownArrow_AndOpensTheSlideOutThatHoldsTheLayerStateBoxOverSevenTiles()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                ToggleButton title = TitleOf(panel);
                Assert.Equal("Layers", panel.Title);

                OpenSlideOut(panel, title);

                List<Button> tiles = SlideOutButtons(panel);
                ComboBox states = Assert.Single(Descendants(panel.SlideOut).OfType<ComboBox>());
                Assert.Equal(
                    SlideOutTiles.Select(t => t.Name),
                    tiles.OrderBy(b => Left(b, panel.SlideOut)).Select(NameOf)
                );
                Assert.Equal(209.0, states.ActualWidth, 0.5);

                // One row of tiles, level with one another, under the Layer State box.
                foreach (Button tile in tiles)
                {
                    Assert.Equal(Top(tiles[0], panel.SlideOut), Top(tile, panel.SlideOut), 0.5);
                    Assert.True(Top(states, panel.SlideOut) < Top(tile, panel.SlideOut));
                    Assert.Equal(24.0, tile.ActualWidth, 0.5);
                    Assert.Equal(22.0, tile.ActualHeight, 0.5);
                }
            });
        }

        [Fact]
        public void TheSlideOutTilesNameTheirCommandInTheTooltip_AndStartIt()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                // Layer Walk would show its dialog: answered with Cancel.
                model.ShowLayerWalk = dialog => false;
                OpenSlideOut(panel, TitleOf(panel));
                List<Button> tiles = SlideOutButtons(panel);

                foreach ((string name, string command) in SlideOutTiles)
                {
                    Button tile = Named(tiles, name);
                    Assert.Equal(name, AutomationProperties.GetName(tile));
                    Assert.Equal(name + " (" + command + ")", tile.ToolTip);
                    Assert.Same(model.StartCommandCommand, tile.Command);
                    Assert.Equal(command, tile.CommandParameter);
                    Assert.DoesNotContain(Descendants(tile).OfType<TextBlock>(), t => !string.IsNullOrEmpty(t.Text));
                    Press(model, tile.IsEnabled, () => tile.Command.Execute(tile.CommandParameter), command);
                }
            });
        }

        [Fact]
        public void TheLayerStateBoxListsTheDrawingsStatesByName_ThenNewLayerStateAndManageLayerStates()
        {
            OnLayers(
                Wide,
                (model, workspace, panel) =>
                {
                    ComboBox states = OpenStates(panel);

                    // Saved with ALONE off and with it on: Elevation is the state the drawing is in. The list is in ordinal order of
                    // the names; an "Unsaved Layer State" entry, where the box keeps one, is not a state.
                    Assert.Equal(
                        new[] { "Elevation", "Plan", "New Layer State...", "Manage Layer States..." },
                        states.Items.Cast<string>().Where(i => i != Unsaved).Select(Dots)
                    );
                    Assert.Equal("Elevation", states.SelectedItem);
                },
                model =>
                {
                    Run(model, "-LAYER;stAte;Save;Plan;;;");
                    Assert.True(CadLayerEditor.SetOn(model.Document, "ALONE", true).Succeeded);
                    Run(model, "-LAYER;stAte;Save;Elevation;;;");
                }
            );
        }

        [Fact]
        public void PickingAStateInTheLayerStateBox_RestoresItsLayersAsOneUndoStep()
        {
            OnLayers(
                Wide,
                (model, workspace, panel) =>
                {
                    // Plan has ALONE off and WALLS thawed; the drawing has ALONE on (Elevation) and WALLS frozen since.
                    Assert.True(CadLayerEditor.SetFrozen(model.Document, "WALLS", true).Succeeded);
                    CadUndoStep before = model.Document.NextUndo;
                    ComboBox states = OpenStates(panel);

                    states.SelectedItem = "Plan";
                    Pump(states);

                    Assert.True(model.Document.Database.LayerTable["ALONE"].IsOff);
                    Assert.False(model.Document.Database.LayerTable["WALLS"].IsFrozen);
                    Assert.NotSame(before, model.Document.NextUndo);

                    Assert.True(model.Undo());

                    Assert.False(model.Document.Database.LayerTable["ALONE"].IsOff);
                    Assert.True(model.Document.Database.LayerTable["WALLS"].IsFrozen);
                    Assert.Same(before, model.Document.NextUndo);
                },
                model =>
                {
                    Run(model, "-LAYER;stAte;Save;Plan;;;");
                    Assert.True(CadLayerEditor.SetOn(model.Document, "ALONE", true).Succeeded);
                    Run(model, "-LAYER;stAte;Save;Elevation;;;");
                }
            );
        }

        [Fact]
        public void WhereNoStateMatchesTheLayers_TheLayerStateBoxShowsUnsavedLayerState()
        {
            OnLayers(
                Wide,
                (model, workspace, panel) =>
                {
                    // Plan was saved with WALLS thawed; WALLS is frozen now, so no state is the drawing.
                    Assert.True(CadLayerEditor.SetFrozen(model.Document, "WALLS", true).Succeeded);
                    ComboBox states = OpenStates(panel);

                    Assert.Equal(Unsaved, states.SelectedItem);
                },
                model => Run(model, "-LAYER;stAte;Save;Plan;;;")
            );
        }

        [Fact]
        public void ADrawingWithNoStates_ShowsUnsavedLayerState_AndOnlyTheTwoCommandsToPick()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                ComboBox states = OpenStates(panel);

                Assert.Equal(Unsaved, states.SelectedItem);
                Assert.Equal(
                    new[] { "New Layer State...", "Manage Layer States..." },
                    states.Items.Cast<string>().Where(i => i != Unsaved).Select(Dots)
                );
            });
        }

        [Theory]
        [InlineData("New Layer State...")]
        [InlineData("Manage Layer States...")]
        public void PickingTheEndOfTheLayerStateBox_StartsLayerstate_AndTheBoxGoesBackToTheStateTheDrawingIs(
            string item
        )
        {
            OnLayers(
                Wide,
                (model, workspace, panel) =>
                {
                    // LAYERSTATE would show the Layer States Manager: answered with Close.
                    model.ShowLayerStates = dialog => false;
                    ComboBox states = OpenStates(panel);
                    int before = model.History.Count;
                    object pick = states.Items.Cast<string>().Single(i => Dots(i) == item);

                    states.SelectedItem = pick;
                    Pump(states);

                    Assert.Contains("Command: LAYERSTATE", model.History.Skip(before));
                    Assert.Equal("Plan", states.SelectedItem);
                    model.Escape();
                },
                model => Run(model, "-LAYER;stAte;Save;Plan;;;")
            );
        }

        [Fact]
        public void InAMediumPanel_MakeCurrentAndMatchLayerShowNoText_AndTheTenTilesAndTheBoxStay()
        {
            OnLayers(Wide, (model, workspace, panel) =>
            {
                // Home's steps are AutoCAD's order, so the width at which Layers goes medium is found by narrowing the window.
                double width = Wide;

                while (panel.Size == RibbonPanelSize.Full)
                {
                    width -= 10;
                    Assert.True(width > 800, "Layers never went medium.");
                    Layout(workspace, width);
                }

                Assert.Equal(RibbonPanelSize.Medium, panel.Size);
                List<Button> buttons = BodyButtons(panel);
                var box = (ComboBox)workspace.FindName("LayerBox");

                Assert.Equal(11, buttons.Count);

                foreach ((string name, _) in RowTwo.Concat(RowThree))
                {
                    Button tile = Named(buttons, name);
                    Assert.Equal(name, AutomationProperties.GetName(tile));
                    Assert.DoesNotContain(Descendants(tile).OfType<TextBlock>(), t => !string.IsNullOrEmpty(t.Text));
                    Assert.Single(Descendants(tile).OfType<IconView>());
                    Assert.Equal(24.0, tile.ActualWidth, 0.5);
                    Assert.Equal(22.0, tile.ActualHeight, 0.5);
                }

                Assert.Equal(208.0, box.ActualWidth, 0.5);
            });
        }

        #region Finding things in the window

        /// <summary>The drawing open in the window drawn offscreen at a width, on a thread of its own, and a check on Home's Layers panel.</summary>
        private static void OnLayers(
            double width,
            Action<MainViewModel, CadWorkspace, RibbonPanel> check,
            Action<MainViewModel> before = null,
            bool loaded = false
        )
        {
            using (var folder = new TestFolder())
            {
                string file = folder.Write("layers.dxf", Layers());
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
                        _ = PackUriHelper.UriSchemePack;
                        var model = new MainViewModel();
                        Assert.True(model.Open(file));
                        before?.Invoke(model);
                        var workspace = new CadWorkspace { DataContext = model };
                        HwndSource host = null;

                        try
                        {
                            // The workspace shows these dialogs in windows; the tests never open one.
                            model.ShowLayerStates = dialog => false;
                            model.ShowLayerWalk = dialog => false;

                            if (loaded)
                            {
                                // A window of messages, never shown, makes the workspace Loaded, which a combo box needs to open its list.
                                host = new HwndSource(
                                    new HwndSourceParameters("RibbonLayersPanelTests")
                                    {
                                        ParentWindow = new IntPtr(-3),
                                        WindowStyle = 0,
                                        Width = (int)width,
                                        Height = 960,
                                    }
                                )
                                {
                                    RootVisual = workspace,
                                };
                            }

                            Layout(workspace, width);
                            Pump(workspace);
                            RibbonPanel panel = Descendants(workspace)
                                .OfType<RibbonStrip>()
                                .Single(s => ReferenceEquals(s.DataContext, model))
                                .Children.OfType<RibbonPanel>()
                                .Single(p => p.Title == "Layers");
                            check(model, workspace, panel);
                        }
                        finally
                        {
                            host?.Dispose();
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

        private static void Run(MainViewModel model, string lines)
        {
            foreach (string line in lines.Split(';'))
            {
                model.Execute(line);
            }
        }

        private static void Pump(DispatcherObject element) =>
            element.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

        /// <summary>The buttons of the panel's body, not those inside the layer box (its glyphs are buttons too).</summary>
        private static List<Button> BodyButtons(RibbonPanel panel)
        {
            ComboBox box = Assert.Single(Descendants(panel.Body).OfType<ComboBox>());
            return Descendants(panel.Body).OfType<Button>().Where(b => !IsWithin(b, box)).ToList();
        }

        private static List<Button> SlideOutButtons(RibbonPanel panel)
        {
            OpenSlideOut(panel, TitleOf(panel));
            return Descendants(panel.SlideOut).OfType<Button>().ToList();
        }

        /// <summary>The slide-out dropped under the panel by its title, laid out, so that the positions of its tiles can be read.</summary>
        private static void OpenSlideOut(RibbonPanel panel, ToggleButton title)
        {
            if (panel.IsDropDownOpen)
            {
                return;
            }

            var popup = (Popup)panel.Template.FindName("PART_Popup", panel);
            title.IsChecked = true;
            Pump(popup);
            Assert.True(panel.IsDropDownOpen);
            popup.Child.Measure(new Size(2000, 960));
            popup.Child.Arrange(new Rect(0, 0, popup.Child.DesiredSize.Width, popup.Child.DesiredSize.Height));
            ((FrameworkElement)popup.Child).UpdateLayout();
        }

        private static ComboBox OpenStates(RibbonPanel panel)
        {
            OpenSlideOut(panel, TitleOf(panel));
            return Assert.Single(Descendants(panel.SlideOut).OfType<ComboBox>());
        }

        /// <summary>The container of a layer in the open list of the layer box, its template made, so that its glyphs can be read.</summary>
        private static FrameworkElement ItemOf(ComboBox box, string layer)
        {
            box.IsDropDownOpen = true;
            Pump(box);
            var popup = (Popup)box.Template.FindName("PART_Popup", box);
            popup.Child.Measure(new Size(600, 600));
            popup.Child.Arrange(new Rect(0, 0, popup.Child.DesiredSize.Width, popup.Child.DesiredSize.Height));
            ((FrameworkElement)popup.Child).UpdateLayout();
            var model = (MainViewModel)box.DataContext;
            var item = (FrameworkElement)
                box.ItemContainerGenerator.ContainerFromItem(model.Layers.Single(l => l.Name == layer));
            Assert.NotNull(item);
            item.UpdateLayout();

            // The row is drawn: its name is there, so that a missing glyph is the feature's, not the list's.
            Assert.Contains(Descendants(item).OfType<TextBlock>(), t => t.Text == layer);
            return item;
        }

        /// <summary>The glyphs an item or the closed box draws, left to right: the outermost elements named as a glyph.</summary>
        private static List<FrameworkElement> GlyphsOf(FrameworkElement root)
        {
            List<FrameworkElement> found = Descendants(root)
                .OfType<FrameworkElement>()
                .Where(e => GlyphNames.Contains(AutomationProperties.GetName(e)))
                .ToList();
            return found
                .Where(e => !found.Any(o => !ReferenceEquals(o, e) && IsWithin(e, o)))
                .OrderBy(e => Left(e, root))
                .ToList();
        }

        /// <summary>One of the three glyphs of a layer in the open list: 0 on or off, 1 frozen or thawed, 2 locked or unlocked.</summary>
        private static FrameworkElement Glyph(ComboBox box, string layer, int index)
        {
            List<FrameworkElement> glyphs = GlyphsOf(ItemOf(box, layer));
            Assert.Equal(3, glyphs.Count);
            return glyphs[index];
        }

        /// <summary>A glyph clicked as the mouse clicks it: the button's Click raised and its command run.</summary>
        private static void Click(FrameworkElement glyph)
        {
            Assert.IsAssignableFrom<ButtonBase>(glyph);
            MethodInfo click = typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
            click.Invoke(glyph, null);
            Pump(glyph);
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

        /// <summary>The words a button is known by: its name for screen readers, or its text where it has no other.</summary>
        private static string NameOf(Button button)
        {
            string name = AutomationProperties.GetName(button);
            return string.IsNullOrEmpty(name) ? button.Content as string : name;
        }

        /// <summary>The panel's title as a button, "Layers ▾", which opens the slide-out.</summary>
        private static ToggleButton TitleOf(RibbonPanel panel) =>
            Assert.Single(Descendants(panel).OfType<ToggleButton>(), t => Equals(t.Content, Title));

        private static Button Named(IEnumerable<Button> buttons, string name) =>
            Assert.Single(buttons, b => NameOf(b) == name);

        private static string Dots(string text) => text.Replace("…", "...");

        private static double Left(FrameworkElement element, UIElement relativeTo) =>
            element.TranslatePoint(new Point(0, 0), relativeTo).X;

        private static double Top(FrameworkElement element, UIElement relativeTo) =>
            element.TranslatePoint(new Point(0, 0), relativeTo).Y;

        private static bool IsWithin(DependencyObject element, DependencyObject ancestor)
        {
            for (DependencyObject at = element; at != null; at = VisualTreeHelper.GetParent(at))
            {
                if (ReferenceEquals(at, ancestor))
                {
                    return true;
                }
            }

            return false;
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

        #endregion
    }
}
