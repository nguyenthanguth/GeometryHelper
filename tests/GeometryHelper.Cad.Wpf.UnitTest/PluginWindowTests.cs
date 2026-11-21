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
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// What a command plugin adds to the window besides its commands, with the sample plugin of the tests loaded as a user's
    /// plugin is (tab "Circles", contextual tab "Circle", a panel on the Plugins tab, the group "Circle tools" of Properties, a
    /// DRAWINGS button and row menu, canvas menu items) and, where a test needs a button that throws or a tab of seven buttons,
    /// a plugin the test writes itself. The drawing is two columns and a wall: circles of diameter 200 and 100 and a line.
    /// </summary>
    public sealed class PluginWindowTests : IDisposable
    {
        private readonly PluginWindowKit _kit = new PluginWindowKit();

        public void Dispose() => _kit.Dispose();

        private static string[] Headers(IEnumerable<CadMenuItemViewModel> items) =>
            items.Select(i => i.IsSeparator ? string.Empty : i.Header).ToArray();

        // The group a plugin adds is the last one shown, so a label the presenter's own rows share ("Diameter") is the plugin's.
        private static PropertyRowViewModel RowOf(MainViewModel model, string label) =>
            model.PropertyGroups.Last().Rows.Single(r => r.Label == label);

        private static PluginButtonViewModel ButtonOf(PluginPanelViewModel panel, string text) =>
            panel.Columns.SelectMany(c => c.Items).OfType<PluginButtonViewModel>().Single(b => b.Text == text);

        private static PluginPanelViewModel PanelOf(MainViewModel model, string tab, string title) =>
            (
                (PluginTabViewModel)
                    model.PluginTabs.Concat(model.PluginContextTabs).Single(t => t.TabHeader == tab).TabContent
            ).Panels.Single(p => p.Title == title);

        private static string[] Shapes(PluginPanelViewModel panel) =>
            panel.Columns.Select(c => string.Join("+", c.Items.Select(i => i.Text))).ToArray();

        #region The tabs

        [Fact]
        public void TheSampleAddsTheCirclesTabWithItsNumberPanel_AndTheCircleTabWithItsMarkPanel()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            Assert.Empty(model.PluginTabs);
            Assert.Empty(model.PluginContextTabs);

            _kit.LoadSample(model);

            Assert.Equal("Circles", Assert.Single(model.PluginTabs).TabHeader);
            Assert.Equal("Circle", Assert.Single(model.PluginContextTabs).TabHeader);
            Assert.Equal(
                new[] { "Number" },
                ((PluginTabViewModel)model.PluginTabs[0].TabContent).Panels.Select(p => p.Title)
            );
            Assert.Equal(
                new[] { "Mark" },
                ((PluginTabViewModel)model.PluginContextTabs[0].TabContent).Panels.Select(p => p.Title)
            );
        }

        [Fact]
        public void TheNumberPanel_HoldsTheLargeButtonAloneAndTheCountClearMarksAndSizesButtonsInOneColumn()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);

            PluginPanelViewModel number = PanelOf(model, "Circles", "Number");

            Assert.Equal(new[] { "Number circles", "Count+Clear marks+Sizes" }, Shapes(number));
            PluginItemViewModel large = number.Columns[0].Items.Single();
            Assert.True(large.IsLarge);
            Assert.Equal(32.0, large.IconSlot);
            Assert.NotNull(large.IconGeometry);
            Assert.Null(large.IconImage);
            Assert.All(number.Columns[1].Items, i => Assert.False(i.IsLarge));
            Assert.All(number.Columns[1].Items, i => Assert.Equal(16.0, i.IconSlot));

            // The sample's PNG icon is a picture in the three standard items.
            Assert.All(number.Columns[1].Items, i => Assert.NotNull(i.IconImage));
            Assert.IsType<PluginSplitViewModel>(number.Columns[1].Items[2]);
        }

        [Fact]
        public void ALargeButtonsTooltip_SaysItsTitleItsCommandItsBodyAndThePluginsName()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);

            PluginButtonViewModel button = ButtonOf(PanelOf(model, "Circles", "Number"), "Number circles");

            Assert.Equal("NUMCIRCLES2", button.Command);
            Assert.Equal("Number circles (NUMCIRCLES2)", button.ToolTipInfo.Title);
            Assert.Equal("Writes a number at the centre of each circle selected.", button.ToolTipInfo.Body);
            Assert.Equal(PluginWindowKit.SampleName, button.ToolTipInfo.Plugin);
        }

        [Fact]
        public void StandardButtonsStackInColumnsOfThree_AndALargeOneEndsTheRunBeforeIt()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadHook(model, host =>
            {
                var seven = new CadRibbonPanel("Seven");
                var mixed = new CadRibbonPanel("Mixed");

                for (int i = 1; i <= 7; i++)
                {
                    seven.Items.Add(new CadRibbonButton("S" + i, c => { }));
                }

                mixed.Items.Add(new CadRibbonButton("A", c => { }));
                mixed.Items.Add(new CadRibbonButton("B", c => { }));
                mixed.Items.Add(new CadRibbonButton("L", c => { }) { Size = CadRibbonItemSize.Large });
                mixed.Items.Add(new CadRibbonButton("C", c => { }));
                mixed.Items.Add(new CadRibbonButton("D", c => { }));
                mixed.Items.Add(new CadRibbonButton("E", c => { }));
                mixed.Items.Add(new CadRibbonButton("F", c => { }));
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(seven);
                tab.Panels.Add(mixed);
                host.AddRibbonTab(tab);
            });

            // Seven standard buttons make 3, 3 and 1. A, B, a large L, then C D E F make 2, 1 large, 3 and 1.
            Assert.Equal(new[] { "S1+S2+S3", "S4+S5+S6", "S7" }, Shapes(PanelOf(model, "Hook", "Seven")));
            Assert.Equal(new[] { "A+B", "L", "C+D+E", "F" }, Shapes(PanelOf(model, "Hook", "Mixed")));
        }

        [Fact]
        public void ABigPanelOfIcons_ShowsEachIconAloneWhereTheTextIsOff_AndItsTextAsTheTooltipTitle()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Icons");
                panel.Items.Add(
                    new CadRibbonButton("Previous", c => { })
                    {
                        Icon = CadPluginIcon.FromPathData("M16 4 L8 12 L16 20"),
                        ShowText = false,
                    }
                );
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });

            PluginButtonViewModel button = ButtonOf(PanelOf(model, "Hook", "Icons"), "Previous");

            Assert.False(button.ShowText);
            Assert.Equal("Previous", button.ToolTipInfo.Title);
            Assert.NotNull(button.IconGeometry);
        }

        [Fact]
        public void AnIconOfBytesThatAreNoPicture_IsReplacedByThePresentersPluginIcon_AndTheButtonStillWorks()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            int ran = 0;
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Broken");
                panel.Items.Add(
                    new CadRibbonButton("Ghost", c => ran++)
                    {
                        Icon = CadPluginIcon.FromImage(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
                    }
                );
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });

            PluginButtonViewModel button = ButtonOf(PanelOf(model, "Hook", "Broken"), "Ghost");

            Assert.NotNull(button.IconGeometry);
            Assert.Null(button.IconImage);
            button.RunCommand.Execute(null);
            Assert.Equal(1, ran);
        }

        [Fact]
        public void ThePluginTab_IsGoneOnceThePluginIsUnloaded_HomeIsShownAgain_AndItIsBackOnReload()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.SelectedTab = model.PluginTabs[0];
            Assert.True(model.PluginTabs[0].IsTabSelected);
            Assert.False(model.IsHomeTabSelected);

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);

            Assert.Empty(model.PluginTabs);
            Assert.Empty(model.PluginContextTabs);
            Assert.Null(model.SelectedTab);
            Assert.True(model.IsHomeTabSelected);

            _kit.LoadSample(model);

            Assert.Equal("Circles", Assert.Single(model.PluginTabs).TabHeader);
            Assert.True(model.IsHomeTabSelected);
        }

        [Fact]
        public void TwoPluginsTabs_AreInTheOrderThePluginsWereLoaded_AndAReloadMakesAPluginTheLast()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            _kit.LoadHook(model, host => host.AddRibbonTab(new CadRibbonTab("Hook", "Hook")));

            Assert.Equal(new[] { "Circles", "Hook" }, model.PluginTabs.Select(t => t.TabHeader));

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);
            _kit.LoadSample(model);

            Assert.Equal(new[] { "Hook", "Circles" }, model.PluginTabs.Select(t => t.TabHeader));
        }

        #endregion

        #region The contextual tab

        [Fact]
        public void TheCircleTab_IsShownAndSelectedForACircleAlone_AndGoneForALine()
        {
            MainViewModel model = _kit.Opened(out string first, out string second, out string wall);
            _kit.LoadSample(model);
            CadWorkspaceExtension circle = model.PluginContextTabs.Single();
            Assert.False(circle.IsTabVisible);

            model.Document.Selection.Set(new[] { first });

            Assert.True(circle.IsTabVisible);
            Assert.Same(circle, model.SelectedTab);
            Assert.True(model.IsPluginContextTabSelected);

            model.Document.Selection.Set(new[] { wall });

            Assert.False(circle.IsTabVisible);
            Assert.True(model.IsHomeTabSelected);
            Assert.False(model.IsPluginContextTabSelected);

            model.Document.Selection.Set(new[] { second });
            Assert.True(circle.IsTabVisible);
            model.Document.Selection.Clear();
            Assert.False(circle.IsTabVisible);
        }

        [Fact]
        public void ACircleAndALineTogether_DoNotShowTheCircleTab()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out string wall);
            _kit.LoadSample(model);

            model.Document.Selection.Set(new[] { first, wall });

            Assert.False(model.PluginContextTabs.Single().IsTabVisible);
            Assert.True(model.IsHomeTabSelected);
        }

        [Fact]
        public void TheCircleTab_IsNotTakenBackToTheForegroundWhileTheSelectionIsStillCircles()
        {
            MainViewModel model = _kit.Opened(out string first, out string second, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            Assert.True(model.IsPluginContextTabSelected);

            // The user goes Home; another circle is picked: the tab stays shown, as it did, and Home stays where it is.
            model.SelectedTab = null;
            model.Document.Selection.Set(new[] { first, second });

            Assert.True(model.PluginContextTabs.Single().IsTabVisible);
            Assert.True(model.IsHomeTabSelected);
        }

        [Fact]
        public void UnloadingWhileTheCircleTabIsShown_GivesHomeBack_AndNoTabIsLeftHidden()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            Assert.True(model.IsPluginContextTabSelected);

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);

            Assert.Empty(model.PluginContextTabs);
            Assert.True(model.IsHomeTabSelected);
            Assert.False(model.IsPluginContextTabSelected);
        }

        [Fact]
        public void AShowWhenThatThrows_HidesTheTab_AndTheSelectionStillWorks()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadHook(model, host =>
                host.AddRibbonTab(
                    new CadRibbonTab("Hook", "Hook") { ShowWhen = c => throw new InvalidOperationException("No.") }
                )
            );

            model.Document.Selection.Set(new[] { first });

            Assert.False(model.PluginContextTabs.Single().IsTabVisible);
            Assert.True(model.IsHomeTabSelected);
            Assert.Equal(1, model.Document.Selection.Count);
        }

        #endregion

        #region The Plugins tab

        [Fact]
        public void ThePluginsTab_HoldsTheCircleToolsPanel_AndTheCommandStillHasItsButton()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            Assert.Empty(model.PluginRibbon.Panels);

            _kit.LoadSample(model);

            PluginPanelViewModel panel = Assert.Single(model.PluginRibbon.Panels);
            Assert.Equal("Circle tools", panel.Title);
            Assert.Equal(new[] { "Count" }, Shapes(panel));
            Assert.Equal("Count the circles of the drawing", ButtonOf(panel, "Count").ToolTipInfo.Title);
            Assert.Equal(new[] { "NUMCIRCLES2" }, model.PluginRibbon.Commands.Select(b => b.Name));
        }

        [Fact]
        public void APluginThatShowsItsOwnButtons_GetsNoneInCommandPlugins_AndKeepsItsCommand()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);

            _kit.LoadHook(model, host => host.ShowsCommandButtons = false);

            Assert.Empty(model.PluginRibbon.Commands);
            Assert.NotNull(model.Session.Registry.Find("HOOKCMD"));
        }

        [Fact]
        public void APluginThatShowsItsCommandButtons_HasHookCmdInCommandPlugins()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);

            _kit.LoadHook(model, host => { });

            Assert.Equal(new[] { "HOOKCMD" }, model.PluginRibbon.Commands.Select(b => b.Name));
        }

        #endregion

        #region The buttons

        [Fact]
        public void CountAndTheCommandButton_AreOnWhileADrawingIsOpen_AndClearMarksIsOnAlways()
        {
            MainViewModel model = _kit.Model();
            _kit.LoadSample(model);
            PluginPanelViewModel number = PanelOf(model, "Circles", "Number");

            Assert.False(ButtonOf(number, "Count").IsEnabled);
            Assert.False(ButtonOf(number, "Number circles").IsEnabled);
            Assert.True(ButtonOf(number, "Clear marks").IsEnabled);

            model.OpenDocument(CadDocument.From(TestFiles.Columns(out _, out _, out _), "columns.dxf"));

            Assert.True(ButtonOf(number, "Count").IsEnabled);
            Assert.True(ButtonOf(number, "Number circles").IsEnabled);
            Assert.True(ButtonOf(number, "Clear marks").IsEnabled);
        }

        [Fact]
        public void TheSizesInTheSplit_AreOnOnlyWhileCirclesAreSelected()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out string wall);
            _kit.LoadSample(model);
            var sizes = (PluginSplitViewModel)PanelOf(model, "Circles", "Number").Columns[1].Items[2];

            Assert.All(sizes.Buttons, b => Assert.False(b.IsEnabled));

            model.Document.Selection.Set(new[] { first });
            Assert.All(sizes.Buttons, b => Assert.True(b.IsEnabled));

            model.Document.Selection.Set(new[] { wall });
            Assert.All(sizes.Buttons, b => Assert.False(b.IsEnabled));
        }

        [Fact]
        public void ACallbackButton_RunsAtOnce_WithTheDrawingShown()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);

            ButtonOf(PanelOf(model, "Circles", "Number"), "Count").RunCommand.Execute(null);

            Assert.Equal("2 circles in Model.", model.History.Last());
        }

        [Fact]
        public void ACommandButton_StartsItsCommandAtItsFirstPrompt_AndCancelsACommandRunningFirst()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.Execute("LINE");
            Assert.StartsWith("Specify first point", model.Prompt);

            ButtonOf(PanelOf(model, "Circles", "Number"), "Number circles").RunCommand.Execute(null);

            Assert.StartsWith("Select objects", model.Prompt);
            Assert.True(model.IsCommandRunning);
        }

        [Fact]
        public void ACallbackThatThrows_WritesThePluginsNameAndTheFirstLineOfTheMessage()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Bad");
                panel.Items.Add(
                    new CadRibbonButton("Bolt", c =>
                        throw new InvalidOperationException("The bolt is gone.\r\nsecond line")
                    )
                );
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });

            ButtonOf(PanelOf(model, "Hook", "Bad"), "Bolt").RunCommand.Execute(null);

            Assert.Equal("HookPlugin: The bolt is gone.", model.History.Last());
            Assert.True(model.HasDocument);
        }

        [Fact]
        public void ACanRunThatThrows_GivesAGreyButton_AndTheOtherButtonsAreStillAsked()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Two");
                panel.Items.Add(
                    new CadRibbonButton("Broken", c => { }) { CanRun = c => throw new InvalidOperationException("No.") }
                );
                panel.Items.Add(new CadRibbonButton("Fine", c => { }) { CanRun = c => c.Selected.Count == 1 });
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });
            PluginPanelViewModel two = PanelOf(model, "Hook", "Two");

            model.Document.Selection.Set(new[] { first });

            Assert.False(ButtonOf(two, "Broken").IsEnabled);
            Assert.True(ButtonOf(two, "Fine").IsEnabled);
        }

        [Fact]
        public void ACallbackThatReadsTheSelection_IsGivenTheHandlesInOrdinalOrder()
        {
            MainViewModel model = _kit.Opened(out string first, out string second, out _);
            var seen = new List<string>();
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Read");
                panel.Items.Add(new CadRibbonButton("Read", c => seen.AddRange(c.Selected)));
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });
            model.Document.Selection.Set(new[] { second, first });

            ButtonOf(PanelOf(model, "Hook", "Read"), "Read").RunCommand.Execute(null);

            Assert.Equal(new[] { first, second }.OrderBy(h => h, StringComparer.OrdinalIgnoreCase), seen);
        }

        [Fact]
        public void TheSplit_ShowsTheButtonUsedLast_AndRunsItOnItsTop()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            var sizes = (PluginSplitViewModel)PanelOf(model, "Circles", "Number").Columns[1].Items[2];
            Assert.Equal("Diameter 10", sizes.Top.Text);

            sizes.Buttons[2].RunCommand.Execute(null);

            Assert.Equal("Diameter 50", sizes.Top.Text);
            Assert.Equal(50.0, ((DxfCircle)model.Document.FindEntity(first)).Diameter, 6);

            // The top runs the one used last: Diameter 50 again, no change, then Diameter 20 from the list.
            sizes.Top.RunCommand.Execute(null);
            sizes.Buttons[1].RunCommand.Execute(null);
            Assert.Equal("Diameter 20", sizes.Top.Text);
            Assert.Equal(20.0, ((DxfCircle)model.Document.FindEntity(first)).Diameter, 6);

            // One undo step for the last size: the diameter is 50 again.
            Assert.True(model.Undo());
            Assert.Equal(50.0, ((DxfCircle)model.Document.FindEntity(first)).Diameter, 6);
        }

        [Fact]
        public void ASplitThatDoesNotRememberTheLast_KeepsTheFirstOnTop()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Split");
                panel.Items.Add(
                    new CadRibbonSplitButton(
                        "Pick",
                        new[] { new CadRibbonButton("One", c => { }), new CadRibbonButton("Two", c => { }) }
                    )
                    {
                        RemembersLast = false,
                    }
                );
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });
            var split = (PluginSplitViewModel)PanelOf(model, "Hook", "Split").Columns[0].Items[0];

            split.Buttons[1].RunCommand.Execute(null);

            Assert.Equal("One", split.Top.Text);
        }

        [Fact]
        public void ThePluginsTabPanelButton_RunsItsCallbackToo()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);

            ButtonOf(model.PluginRibbon.Panels.Single(), "Count").RunCommand.Execute(null);

            Assert.Equal("2 circles in Model.", model.History.Last());
        }

        #endregion

        #region Properties

        [Fact]
        public void ACircleSelected_ShowsTheCircleToolsGroupLast_WithTheRowsOfTheSample()
        {
            MainViewModel model = _kit.Opened(out string first, out string second, out _);
            _kit.LoadSample(model);

            model.Document.Selection.Set(new[] { first, second });

            PropertyGroupViewModel group = model.PropertyGroups.Last();
            Assert.Equal("Circle tools", group.Title);
            Assert.Equal(
                new[] { "Number of circles", "Diameter", "Mark", "Mark color", "Mark layer" },
                group.Rows.Select(r => r.Label)
            );
            Assert.Equal("2", group.Rows[0].Value);
            Assert.True(group.Rows[0].IsReadOnly);
            Assert.Equal("No", group.Rows[2].Value);
            Assert.Equal("Red", group.Rows[3].Value);
            Assert.Equal("Mark", group.Rows[4].Value);
        }

        [Fact]
        public void TheRowsKinds_AreATypedLengthACheckBoxAndTheThreeListsOfTheWindow()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);

            model.Document.Selection.Set(new[] { first });

            PropertyRowViewModel diameter = RowOf(model, "Diameter");
            Assert.True(diameter.IsTyped);
            Assert.False(diameter.HasOptions);
            Assert.False(diameter.IsReadOnly);
            Assert.StartsWith("200", diameter.Value);
            PropertyRowViewModel mark = RowOf(model, "Mark");
            Assert.True(mark.IsCheck);
            Assert.False(mark.IsOn);
            Assert.True(RowOf(model, "Mark color").HasOptions);
            Assert.True(RowOf(model, "Mark layer").HasOptions);
            Assert.True(RowOf(model, "Number of circles").IsReadOnly);
            Assert.False(RowOf(model, "Number of circles").HasOptions);
        }

        [Fact]
        public void TheColourRow_OffersTheWindowsColourListEndingWithSelectColor_AndTheLayerRowTheDrawingsLayers()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);

            model.Document.Selection.Set(new[] { first });

            Assert.Equal(
                new[]
                {
                    "ByLayer",
                    "ByBlock",
                    "Red",
                    "Yellow",
                    "Green",
                    "Cyan",
                    "Blue",
                    "Magenta",
                    "White",
                    "Select Color...",
                },
                RowOf(model, "Mark color").Options
            );
            string[] layers = RowOf(model, "Mark layer").Options.OrderBy(l => l, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "0", "COLUMNS", "Mark", "WALLS" }, layers);
        }

        [Fact]
        public void AColourTypedAsAnIndex_GoesToTheSetterAsThatColour_AndTheRowReadsBackItsName()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });

            RowOf(model, "Mark color").Value = "3";

            Assert.Equal("Green", RowOf(model, "Mark color").Value);
        }

        [Fact]
        public void AColourThatIsNoColour_IsRefusedInThePresentersWords_AndTheRowKeepsItsValue()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });

            RowOf(model, "Mark color").Value = "purple";

            Assert.Equal("Red", RowOf(model, "Mark color").Value);
            Assert.Equal(
                "A colour is ByLayer, ByBlock, a name from Red to White, an index 1 to 255, or red, green and blue as 255,128,0.",
                model.History.Last()
            );
        }

        [Fact]
        public void SelectColorOnTheColourRow_AsksTheWindowsDialog_AndGivesTheChosenColourToTheSetter()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            SelectColorViewModel asked = null;

            // The window shows its Select Color dialog through PickColor; the test answers as the dialog's OK would, green.
            model.PickColor = dialog =>
            {
                asked = dialog;
                dialog.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
                return true;
            };
            RowOf(model, "Mark color").Value = MainViewModel.SelectColorOption;

            Assert.NotNull(asked);
            Assert.Equal("Green", RowOf(model, "Mark color").Value);
        }

        [Fact]
        public void SelectColorWhereTheWindowGivesNoDialog_SaysSo_AndTheRowKeepsItsColour()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            Assert.Null(model.PickColor);

            RowOf(model, "Mark color").Value = MainViewModel.SelectColorOption;

            Assert.Equal("Red", RowOf(model, "Mark color").Value);
            Assert.Equal("The Select Color dialog is shown by the window.", model.History.Last());
        }

        [Fact]
        public void TheLayerRow_GivesTheLayerChosenToTheSetter_AndWritesThePluginsOwnWordsForOneItRefuses()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });

            RowOf(model, "Mark layer").Value = "COLUMNS";
            Assert.Equal("COLUMNS", RowOf(model, "Mark layer").Value);

            RowOf(model, "Mark layer").Value = "Ghost";

            Assert.Equal("COLUMNS", RowOf(model, "Mark layer").Value);
            Assert.Equal("Circle tools: the drawing has no layer Ghost.", model.History.Last());
        }

        [Fact]
        public void TheCheckRow_IsATickedBoxThatTheSampleKeeps_AcrossTheNextReadingOfTheRows()
        {
            MainViewModel model = _kit.Opened(out string first, out string second, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });

            RowOf(model, "Mark").IsOn = true;
            model.Document.Selection.Set(new[] { second });

            Assert.True(RowOf(model, "Mark").IsOn);
            Assert.Equal("Yes", RowOf(model, "Mark").Value);
        }

        [Fact]
        public void ADiameterTyped_ChangesEveryCircleSelected_InOneUndoStep()
        {
            MainViewModel model = _kit.Opened(out string first, out string second, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first, second });

            RowOf(model, "Diameter").Value = "60";

            Assert.Equal(60.0, ((DxfCircle)model.Document.FindEntity(first)).Diameter, 6);
            Assert.Equal(60.0, ((DxfCircle)model.Document.FindEntity(second)).Diameter, 6);
            Assert.StartsWith("60", RowOf(model, "Diameter").Value);

            Assert.True(model.Undo());

            Assert.Equal(200.0, ((DxfCircle)model.Document.FindEntity(first)).Diameter, 6);
            Assert.Equal(100.0, ((DxfCircle)model.Document.FindEntity(second)).Diameter, 6);
        }

        [Fact]
        public void ADiameterThatIsNoNumber_IsRefusedWithTheLabel_TheValueStays_AndNoCircleChanges()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            string before = RowOf(model, "Diameter").Value;

            RowOf(model, "Diameter").Value = "abc";

            Assert.Equal(before, RowOf(model, "Diameter").Value);
            Assert.Equal("Diameter is a number.", model.History.Last());
            Assert.Equal(200.0, ((DxfCircle)model.Document.FindEntity(first)).Diameter, 6);
            Assert.False(model.Document.CanUndo);
        }

        [Fact]
        public void ALineSelected_ShowsNoCircleToolsGroup_AndNeitherDoesACircleWithALine_NorNothing()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out string wall);
            _kit.LoadSample(model);

            model.Document.Selection.Set(new[] { wall });
            Assert.DoesNotContain(model.PropertyGroups, g => g.Title == "Circle tools");

            model.Document.Selection.Set(new[] { first, wall });
            Assert.DoesNotContain(model.PropertyGroups, g => g.Title == "Circle tools");

            model.Document.Selection.Clear();
            Assert.DoesNotContain(model.PropertyGroups, g => g.Title == "Circle tools");
        }

        [Fact]
        public void TheOtherKinds_AreTypedWithTheDocumentsUnits_AndAPointIsThreeLengthRows()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            var numbers = new List<double>();
            var angles = new List<double>();
            var points = new List<GeoPoint3>();
            CadDocument document = model.Document;
            _kit.LoadHook(model, host => host.AddProperties(request =>
                {
                    var rows = new List<CadPropertyRow> { CadPropertyRow.Number("Count", 3, v =>
                        {
                            numbers.Add(v);
                            return null;
                        }), CadPropertyRow.Angle("Turn", Math.PI / 4, request.Document, v =>
                        {
                            angles.Add(v);
                            return null;
                        }) };
                    rows.AddRange(CadPropertyRow.Point("Anchor", new GeoPoint3(10, 20, 30), request.Document, v =>
                        {
                            points.Add(v);
                            return null;
                        }));
                    var groups = new List<CadPropertyGroup>(request.Shown.Groups)
                    {
                        new CadPropertyGroup("Hook", rows),
                    };
                    return new CadSelectionInfo(request.Shown.Label, groups);
                }));

            document.Selection.Set(new[] { first });

            Assert.Equal(
                new[] { "Count", "Turn", "Anchor X", "Anchor Y", "Anchor Z" },
                model.PropertyGroups.Last().Rows.Select(r => r.Label)
            );
            RowOf(model, "Count").Value = "12.5";
            RowOf(model, "Turn").Value = "90";
            RowOf(model, "Anchor Y").Value = "25";

            Assert.Equal(new[] { 12.5 }, numbers);
            Assert.Equal(Math.PI / 2, Assert.Single(angles), 9);
            GeoPoint3 moved = Assert.Single(points);
            Assert.Equal(10.0, moved.X, 9);
            Assert.Equal(25.0, moved.Y, 9);
            Assert.Equal(30.0, moved.Z, 9);
        }

        [Fact]
        public void ANumberAnAngleAndACoordinateThatAreNoNumbers_AreRefusedWithTheirLabelsAndTheSettersNeverCalled()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            int called = 0;
            _kit.LoadHook(model, host => host.AddProperties(request =>
                {
                    Func<double, string> count = v =>
                    {
                        called++;
                        return null;
                    };
                    var rows = new List<CadPropertyRow>
                    {
                        CadPropertyRow.Number("Count", 3, count),
                        CadPropertyRow.Angle("Turn", 0.5, request.Document, count),
                    };
                    rows.AddRange(CadPropertyRow.Point("Anchor", new GeoPoint3(1, 2, 3), request.Document, v =>
                        {
                            called++;
                            return null;
                        }));
                    var groups = new List<CadPropertyGroup>(request.Shown.Groups)
                    {
                        new CadPropertyGroup("Hook", rows),
                    };
                    return new CadSelectionInfo(request.Shown.Label, groups);
                }));
            model.Document.Selection.Set(new[] { first });

            RowOf(model, "Count").Value = "many";
            Assert.Equal("Count is a number.", model.History.Last());
            RowOf(model, "Turn").Value = "right";
            Assert.Equal("Turn is a number.", model.History.Last());
            RowOf(model, "Anchor Z").Value = "up";
            Assert.Equal("Anchor Z is a number.", model.History.Last());

            Assert.Equal(0, called);
            Assert.Equal("3", RowOf(model, "Count").Value);
        }

        [Fact]
        public void TheLinetypeAndLineweightRows_OfferTheWindowsOwnLists()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            var weights = new List<DxfLineWeight>();
            _kit.LoadHook(model, host => host.AddProperties(request =>
                {
                    var rows = new List<CadPropertyRow>
                    {
                        CadPropertyRow.Linetype("Style", "ByLayer", t => null),
                        CadPropertyRow.Lineweight("Pen", DxfLineWeight.ByLayer, w =>
                        {
                            weights.Add(w);
                            return null;
                        }),
                    };
                    var groups = new List<CadPropertyGroup>(request.Shown.Groups)
                    {
                        new CadPropertyGroup("Hook", rows),
                    };
                    return new CadSelectionInfo(request.Shown.Label, groups);
                }));
            model.Document.Selection.Set(new[] { first });

            IReadOnlyList<string> styles = RowOf(model, "Style").Options;
            IReadOnlyList<string> pens = RowOf(model, "Pen").Options;

            Assert.Equal(new[] { "ByLayer", "ByBlock" }, styles.Take(2));
            Assert.Contains("Continuous", styles);
            Assert.Equal(new[] { "ByLayer", "ByBlock", "Default", "0.00 mm", "0.05 mm" }, pens.Take(5));
            Assert.Contains("0.25 mm", pens);

            RowOf(model, "Pen").Value = "0.25 mm";

            Assert.Equal(DxfLineWeight.LineWeight025, Assert.Single(weights));
        }

        [Fact]
        public void AReadOnlyCheckOfAPlugin_StaysATextRowOfYesOrNo()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadHook(model, host => host.AddProperties(request =>
                {
                    var rows = new List<CadPropertyRow> { CadPropertyRow.Check("Linked", true, null) };
                    var groups = new List<CadPropertyGroup>(request.Shown.Groups)
                    {
                        new CadPropertyGroup("Hook", rows),
                    };
                    return new CadSelectionInfo(request.Shown.Label, groups);
                }));
            model.Document.Selection.Set(new[] { first });

            PropertyRowViewModel linked = RowOf(model, "Linked");

            Assert.True(linked.IsReadOnly);
            Assert.False(linked.IsCheck);
            Assert.Equal("Yes", linked.Value);
        }

        [Fact]
        public void AProviderThatThrows_IsPassedOverForThatReading_AndTheSamplesRowsAreStillShown()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadHook(model, host =>
                host.AddProperties(request => throw new InvalidOperationException("No rows."))
            );
            _kit.LoadSample(model);

            model.Document.Selection.Set(new[] { first });

            Assert.Equal("Circle tools", model.PropertyGroups.Last().Title);
            Assert.Contains(model.PropertyGroups, g => g.Title != "Circle tools");
        }

        [Fact]
        public void ALaterPluginsProvider_IsGivenWhatAnEarlierPluginShowed_TheHandlesInOrderAndTheBannerItMayChange()
        {
            MainViewModel model = _kit.Opened(out string first, out string second, out _);
            _kit.LoadSample(model);
            var titles = new List<string>();
            var handles = new List<string>();
            _kit.LoadHook(model, host => host.AddProperties(request =>
                {
                    titles.AddRange(request.Shown.Groups.Select(g => g.Title));
                    handles.AddRange(request.Handles);
                    request.Banner = "Circles are marked.";
                    return null;
                }));

            model.Document.Selection.Set(new[] { second, first });

            Assert.Contains("Circle tools", titles);
            Assert.Equal(new[] { first, second }.OrderBy(h => h, StringComparer.OrdinalIgnoreCase), handles.Take(2));
            Assert.Equal("Circles are marked.", model.PropertiesBanner);
        }

        #endregion

        #region DRAWINGS

        [Fact]
        public void TheDrawingsButton_IsFirstInTheHeader_BeforeAnExtensionsButton()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            model.AddExtension(new ButtonExtension());

            _kit.LoadSample(model);

            Assert.Equal(
                new[] { "Count the circles", "Refresh the drawings" },
                model.FileListButtons.Select(b => b.ToolTip)
            );
            Assert.Equal(PluginWindowKit.SampleName, model.FileListButtons[0].PluginName);
            Assert.Null(model.FileListButtons[1].PluginName);
            Assert.NotNull(model.FileListButtons[0].Icon);
        }

        [Fact]
        public void TheDrawingsButton_IsOnOnlyWithADrawing_AndRunsTheCallback()
        {
            MainViewModel model = _kit.Model();
            _kit.LoadSample(model);
            CadExtensionButton button = model.FileListButtons.Single();
            Assert.False(button.Command.CanExecute(null));

            model.OpenDocument(CadDocument.From(TestFiles.Columns(out _, out _, out _), "columns.dxf"));

            Assert.True(button.Command.CanExecute(null));
            button.Command.Execute(null);
            Assert.Equal("2 circles in Model.", model.History.Last());
        }

        [Fact]
        public void TheRowMenu_HasThePresentersItems_ASeparator_ThenCircleReportWithItsSubmenuAndIncludeInBatch()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            string[] plain = Headers(model.FileRowMenu(model.FileRows.Single(r => !r.IsHeader)));
            _kit.LoadSample(model);

            IReadOnlyList<CadMenuItemViewModel> menu = model.FileRowMenu(model.FileRows.Single(r => !r.IsHeader));

            Assert.Equal(plain.Concat(new[] { string.Empty, "Circle report", "Include in batch" }), Headers(menu));
            CadMenuItemViewModel report = menu.Single(i => i.Header == "Circle report");
            Assert.Equal(new[] { "Count circles", "Write report" }, Headers(report.Items));
            Assert.True(report.Items[0].IsEnabled);
            Assert.False(report.Items[1].IsEnabled);
            Assert.True(menu.Single(i => i.Header == "Include in batch").IsCheckable);
            Assert.False(menu.Single(i => i.Header == "Include in batch").IsChecked);
        }

        [Fact]
        public void IncludeInBatch_IsTickedOnceChosen_BecauseTheMenuIsAskedAgainEachTimeItOpens()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            FileRowViewModel row = model.FileRows.Single(r => !r.IsHeader);

            model.FileRowMenu(row).Single(i => i.Header == "Include in batch").Command.Execute(null);

            Assert.True(model.FileRowMenu(row).Single(i => i.Header == "Include in batch").IsChecked);
        }

        [Fact]
        public void CountCirclesOnARow_RunsTheCallbackOnTheDrawingShown()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            CadMenuItemViewModel report = model
                .FileRowMenu(model.FileRows.Single(r => !r.IsHeader))
                .Single(i => i.Header == "Circle report");

            report.Items[0].Command.Execute(null);

            Assert.Equal("2 circles in Model.", model.History.Last());
        }

        [Fact]
        public void ARowOfADrawingNotOpen_IsAskedWithNoDocument_SoCountCirclesIsGrey()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            string other = _kit.Folder.Write("other.dxf", TestFiles.Columns(out _, out _, out _));
            Assert.True(model.Open(other));
            Assert.True(model.CloseDocument());
            _kit.LoadSample(model);
            FileRowViewModel closed = model.FileRows.Single(r => r.Label == "other.dxf");
            Assert.False(closed.IsOpen);

            CadMenuItemViewModel report = model.FileRowMenu(closed).Single(i => i.Header == "Circle report");

            Assert.False(report.Items[0].IsEnabled);
        }

        [Fact]
        public void AFolderHeaderRow_IsNotAskedAndHasNoMenu_WhileAFileRowIsAskedOnceEachTimeItsMenuOpens()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            var paths = new List<string>();
            _kit.LoadHook(model, host => host.AddDrawingsMenu(request =>
                {
                    paths.Add(request.Path);
                    return new[] { new CadPluginMenuItem("Hook item", c => { }) };
                }));
            FileRowViewModel header = model.FileRows.Single(r => r.IsHeader);
            FileRowViewModel file = model.FileRows.Single(r => !r.IsHeader);

            Assert.Empty(model.FileRowMenu(header));
            Assert.Empty(paths);

            model.FileRowMenu(file);
            model.FileRowMenu(file);

            Assert.Equal(new[] { file.Path, file.Path }, paths);
        }

        #endregion

        #region The canvas menu

        [Fact]
        public void ACircleSelected_PutsNumberTheseAndCircleToolsBeforeThePresentersItemsAndASeparatorAfterThem()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            model.Document.Selection.Set(new[] { first });
            string[] plain = Headers(model.CanvasMenu());
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });

            IReadOnlyList<CadMenuItemViewModel> menu = model.CanvasMenu();

            Assert.Equal(new[] { "Number these", "Circle tools", string.Empty }.Concat(plain), Headers(menu));
        }

        [Fact]
        public void ALineSelected_GetsNoPluginItems_AndNeitherDoesAnEmptySelection()
        {
            MainViewModel model = _kit.Opened(out _, out _, out string wall);
            model.Document.Selection.Set(new[] { wall });
            string[] plain = Headers(model.CanvasMenu());
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { wall });

            Assert.Equal(plain, Headers(model.CanvasMenu()));

            model.Document.Selection.Clear();
            Assert.DoesNotContain(model.CanvasMenu() ?? new CadMenuItemViewModel[0], i => i.Header == "Number these");
        }

        [Fact]
        public void TheCircleToolsSubmenu_TicksMarkOnceChosen_AndClearMarksIsGreyUntilThen()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });

            CadMenuItemViewModel tools = model.CanvasMenu().Single(i => i.Header == "Circle tools");
            Assert.Equal(new[] { "Mark", "Clear marks" }, Headers(tools.Items));
            Assert.True(tools.Items[0].IsCheckable);
            Assert.False(tools.Items[0].IsChecked);
            Assert.False(tools.Items[1].IsEnabled);

            tools.Items[0].Command.Execute(null);
            tools = model.CanvasMenu().Single(i => i.Header == "Circle tools");

            Assert.True(tools.Items[0].IsChecked);
            Assert.True(tools.Items[1].IsEnabled);
        }

        [Fact]
        public void NumberThese_StartsNumcircles2_AndTheSelectionIsNumberedFromOne()
        {
            MainViewModel model = _kit.Opened(out string first, out string second, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first, second });

            model.CanvasMenu().Single(i => i.Header == "Number these").Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            Assert.Equal("Numbered 2 circles.", model.History.Last());
            Assert.Equal(
                new[] { "1", "2" },
                model
                    .Document.ModelSpace.Block.OfType<DxfDBText>()
                    .Select(t => t.TextString)
                    .OrderBy(t => t, StringComparer.Ordinal)
            );
        }

        [Fact]
        public void WhileACommandRuns_NoPluginItemIsAsked()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            int asked = 0;
            _kit.LoadHook(model, host => host.AddCanvasMenu(context =>
                {
                    asked++;
                    return new[] { new CadPluginMenuItem("Hook item", c => { }) };
                }));
            model.Document.Selection.Set(new[] { first });
            model.Execute("LINE");
            asked = 0;

            IReadOnlyList<CadMenuItemViewModel> menu = model.CanvasMenu();

            Assert.Equal(0, asked);
            Assert.DoesNotContain(menu ?? new CadMenuItemViewModel[0], i => i.Header == "Hook item");
        }

        [Fact]
        public void AProviderOfTheCanvasMenuThatThrows_IsPassedOver_AndAnotherPluginsItemsStay()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadHook(model, host =>
                host.AddCanvasMenu(context => throw new InvalidOperationException("No menu."))
            );
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });

            IReadOnlyList<CadMenuItemViewModel> menu = model.CanvasMenu();

            Assert.Contains(menu, i => i.Header == "Number these");
        }

        #endregion

        #region Unloading

        [Fact]
        public void AfterUnloading_TheCircleToolsGroupIsGoneFromTheRowsAlreadyShown()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            Assert.Contains(model.PropertyGroups, g => g.Title == "Circle tools");

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);

            Assert.DoesNotContain(model.PropertyGroups, g => g.Title == "Circle tools");
        }

        [Fact]
        public void AfterUnloading_NoDrawingsButtonRowItemOrCanvasItemOfThePluginIsLeft()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            Assert.Single(model.FileListButtons);

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);

            Assert.Empty(model.FileListButtons);
            Assert.DoesNotContain(model.FileRowMenu(model.FileRows.Single(r => !r.IsHeader)), i =>
                i.Header == "Circle report" || i.Header == "Include in batch"
            );
            model.Document.Selection.Set(new[] { first });
            Assert.DoesNotContain(model.CanvasMenu(), i => i.Header == "Number these" || i.Header == "Circle tools");
        }

        [Fact]
        public void AfterUnloading_ThePluginsTabPanelAndTheCommandButtonAreGone_AndTheCommandIsNotKnownAnyMore()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            Assert.Single(model.PluginRibbon.Panels);

            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);
            model.Document.Selection.Set(new[] { first });

            Assert.Empty(model.PluginRibbon.Panels);
            Assert.Empty(model.PluginRibbon.Commands);
            Assert.Null(model.Session.Registry.Find("NUMCIRCLES2"));
        }

        [Fact]
        public void AfterReloading_TheTabsThePanelTheButtonTheMenusAndTheGroupAreAllBack()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            Assert.True(model.UnloadPlugin(PluginWindowKit.SampleName).Succeeded);

            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });

            Assert.Equal("Circles", Assert.Single(model.PluginTabs).TabHeader);
            Assert.True(model.PluginContextTabs.Single().IsTabVisible);
            Assert.Single(model.PluginRibbon.Panels);
            Assert.Single(model.FileListButtons);
            Assert.Contains(model.PropertyGroups, g => g.Title == "Circle tools");
            Assert.Contains(model.CanvasMenu(), i => i.Header == "Number these");
            Assert.Contains(model.FileRowMenu(model.FileRows.Single(r => !r.IsHeader)), i =>
                i.Header == "Circle report"
            );
        }

        [Fact]
        public void ThePluginsTerminate_RunsOnceWhenItIsUnloaded()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            int terminated = 0;
            _kit.LoadHook(model, host => { }, () => terminated++);

            Assert.True(model.UnloadPlugin(PluginWindowKit.HookName).Succeeded);

            Assert.Equal(1, terminated);
        }

        #endregion

        #region Harder cases

        [Fact]
        public void LoadingOrUnloadingAnotherPlugin_KeepsTheCirclesTabShown_AndTheSplitsLastChoice()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            model.SelectedTab = model.PluginTabs[0];
            CadWorkspaceExtension circles = model.PluginTabs[0];
            var sizes = (PluginSplitViewModel)PanelOf(model, "Circles", "Number").Columns[1].Items[2];
            sizes.Buttons[2].RunCommand.Execute(null);

            _kit.LoadHook(model, host => host.AddRibbonTab(new CadRibbonTab("Hook", "Hook")));

            Assert.Same(circles, model.SelectedTab);
            Assert.Equal(
                "Diameter 50",
                ((PluginSplitViewModel)PanelOf(model, "Circles", "Number").Columns[1].Items[2]).Top.Text
            );

            Assert.True(model.UnloadPlugin(PluginWindowKit.HookName).Succeeded);

            Assert.Same(circles, model.SelectedTab);
        }

        [Fact]
        public void ASecondPluginsTabWithTheSameId_IsLeftOut_AndTheFirstOnesStays()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            var clash = new CadRibbonTab("Circles", "Impostor");
            clash.Panels.Add(new CadRibbonPanel("Other"));

            _kit.LoadHook(model, host => host.AddRibbonTab(clash));

            Assert.Equal(new[] { "Circles" }, model.PluginTabs.Select(t => t.TabHeader));
        }

        [Fact]
        public void ANullPanelAndANullItem_AreSkipped_AndAnEmptyTabAndAnEmptyPanelAreShown()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Half");
                panel.Items.Add(null);
                panel.Items.Add(new CadRibbonButton("Only", c => { }));
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(null);
                tab.Panels.Add(panel);
                tab.Panels.Add(new CadRibbonPanel("Empty"));
                host.AddRibbonTab(tab);
                host.AddRibbonTab(new CadRibbonTab("Bare", "Bare"));
            });

            Assert.Equal(new[] { "Hook", "Bare" }, model.PluginTabs.Select(t => t.TabHeader));
            Assert.Equal(new[] { "Only" }, Shapes(PanelOf(model, "Hook", "Half")));
            Assert.Empty(PanelOf(model, "Hook", "Empty").Columns);
            Assert.Empty(((PluginTabViewModel)model.PluginTabs[1].TabContent).Panels);
        }

        [Fact]
        public void APathThatIsNoPath_IsReplacedByThePresentersPluginIcon_AndTheButtonStillWorks()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            int ran = 0;
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Odd");
                panel.Items.Add(
                    new CadRibbonButton("Odd", c => ran++) { Icon = CadPluginIcon.FromPathData("not a path") }
                );
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });

            PluginButtonViewModel button = ButtonOf(PanelOf(model, "Hook", "Odd"), "Odd");

            Assert.NotNull(button.IconGeometry);
            button.RunCommand.Execute(null);
            Assert.Equal(1, ran);
        }

        [Fact]
        public void ARasterIconSmallerThanItsRoom_IsScaledUpPixelByPixel_AndALargerOneSmoothly()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            byte[] small = Png(16);
            byte[] big = Png(64);
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Pictures");
                panel.Items.Add(
                    new CadRibbonButton("Small large", c => { })
                    {
                        Size = CadRibbonItemSize.Large,
                        Icon = CadPluginIcon.FromImage(small),
                    }
                );
                panel.Items.Add(new CadRibbonButton("Big standard", c => { }) { Icon = CadPluginIcon.FromImage(big) });
                panel.Items.Add(
                    new CadRibbonButton("Small standard", c => { }) { Icon = CadPluginIcon.FromImage(small) }
                );
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });
            PluginPanelViewModel pictures = PanelOf(model, "Hook", "Pictures");

            // 16 pixels into a room of 32 is scaled up pixel by pixel; 64 into 16 is scaled down smoothly; 16 into 16 smoothly too.
            Assert.Equal(BitmapScalingMode.NearestNeighbor, ButtonOf(pictures, "Small large").ImageScaling);
            Assert.Equal(BitmapScalingMode.Fant, ButtonOf(pictures, "Big standard").ImageScaling);
            Assert.Equal(BitmapScalingMode.Fant, ButtonOf(pictures, "Small standard").ImageScaling);
            Assert.Equal(64, ButtonOf(pictures, "Big standard").IconImage.PixelWidth);
        }

        [Fact]
        public void AButtonThatSaysNoWhileACommandRuns_IsGreyedByTheCommandAndOnAgainWhenItEnds()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadHook(model, host =>
            {
                var panel = new CadRibbonPanel("Idle");
                panel.Items.Add(new CadRibbonButton("Idle only", c => { }) { CanRun = c => !c.IsCommandRunning });
                var tab = new CadRibbonTab("Hook", "Hook");
                tab.Panels.Add(panel);
                host.AddRibbonTab(tab);
            });
            PluginButtonViewModel button = ButtonOf(PanelOf(model, "Hook", "Idle"), "Idle only");
            Assert.True(button.IsEnabled);

            model.Execute("LINE");
            Assert.False(button.IsEnabled);

            model.Escape();
            Assert.True(button.IsEnabled);
        }

        [Fact]
        public void ClosingTheLastDrawing_GreysCountAndTheDrawingsButton_AndHidesTheCircleTab()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            PluginButtonViewModel count = ButtonOf(PanelOf(model, "Circles", "Number"), "Count");
            Assert.True(count.IsEnabled);
            Assert.True(model.IsPluginContextTabSelected);

            Assert.True(model.CloseDocument());

            Assert.False(model.HasDocument);
            Assert.False(count.IsEnabled);
            Assert.False(model.FileListButtons.Single().Command.CanExecute(null));
            Assert.False(model.PluginContextTabs.Single().IsTabVisible);
            Assert.True(model.IsHomeTabSelected);
        }

        [Fact]
        public void SwitchingToADrawingWithNothingSelected_HidesTheCircleTab_AndSwitchingBackBringsItBack()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            string other = _kit.Folder.Write("other.dxf", TestFiles.Columns(out _, out _, out _));
            string columns = model.FileRows.Single(r => !r.IsHeader).Path;
            _kit.LoadSample(model);
            model.Document.Selection.Set(new[] { first });
            Assert.True(model.IsPluginContextTabSelected);

            Assert.True(model.Open(other));

            Assert.False(model.PluginContextTabs.Single().IsTabVisible);
            Assert.True(model.IsHomeTabSelected);

            Assert.True(model.Open(columns));

            Assert.True(model.PluginContextTabs.Single().IsTabVisible);
        }

        [Fact]
        public void AProviderIsAskedWithNoHandlesWhenNothingIsSelected_AndMayAddItsRows()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            int handles = -1;
            _kit.LoadHook(model, host => host.AddProperties(request =>
                {
                    handles = request.Handles.Count;
                    var rows = new List<CadPropertyRow> { new CadPropertyRow("Drawing note", "None", true) };
                    var groups = new List<CadPropertyGroup>(request.Shown.Groups)
                    {
                        new CadPropertyGroup("Always", rows),
                    };
                    return new CadSelectionInfo(request.Shown.Label, groups);
                }));

            model.Document.Selection.Set(new[] { first });
            model.Document.Selection.Clear();

            Assert.Equal(0, handles);
            Assert.Equal("Always", model.PropertyGroups.Last().Title);
        }

        /// <summary>A PNG of a clear square <paramref name="side"/> pixels across.</summary>
        private static byte[] Png(int side)
        {
            var pixels = new byte[side * side * 4];
            BitmapSource source = BitmapSource.Create(side, side, 96, 96, PixelFormats.Bgra32, null, pixels, side * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        #endregion

        #region On the workspace

        private sealed class ButtonExtension : CadWorkspaceExtension
        {
            public override IReadOnlyList<CadExtensionButton> FileListButtons { get; } =
                new[]
                {
                    new CadExtensionButton(
                        System.Windows.Media.Geometry.Parse("M0 0L10 10"),
                        "Refresh the drawings",
                        () => { }
                    ),
                };
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        private static Rect Box(FrameworkElement element, FrameworkElement root) =>
            element
                .TransformToAncestor(root)
                .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

        private static FrameworkElement Named(CadWorkspace workspace, string name) =>
            Descendants(workspace)
                .OfType<FrameworkElement>()
                .First(e => e.ActualWidth > 0 && System.Windows.Automation.AutomationProperties.GetName(e) == name);

        private static ToggleButton TabHeader(CadWorkspace workspace, string text) =>
            Descendants(workspace).OfType<ToggleButton>().First(b => b.ActualWidth > 0 && Equals(b.Content, text));

        /// <summary>Builds the workspace over the model on a thread of its own, laid out offscreen at 1 600 by 960, and runs the check.</summary>
        private static void OnWorkspace(MainViewModel model, Action<CadWorkspace> check)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;
                    var workspace = new CadWorkspace { DataContext = model };

                    try
                    {
                        workspace.Measure(new Size(1600, 960));
                        workspace.Arrange(new Rect(0, 0, 1600, 960));
                        workspace.UpdateLayout();
                        check(workspace);
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

        [Fact]
        public void TheTabStrip_ListsCirclesAfterPluginsAndBeforeSettings_AndTheCircleTabAfterThemWhenShown()
        {
            MainViewModel model = _kit.Opened(out string first, out _, out _);
            _kit.LoadSample(model);

            OnWorkspace(model, workspace =>
            {
                double plugins = Box(TabHeader(workspace, "Plugins"), workspace).Left;
                double circles = Box(TabHeader(workspace, "Circles"), workspace).Left;
                double settings = Box(TabHeader(workspace, "Settings"), workspace).Left;
                Assert.True(plugins < circles && circles < settings, plugins + " < " + circles + " < " + settings);
                Assert.DoesNotContain(Descendants(workspace).OfType<ToggleButton>(), b =>
                    b.ActualWidth > 0 && Equals(b.Content, "Circle")
                );

                model.Document.Selection.Set(new[] { first });
                workspace.UpdateLayout();

                Assert.True(Box(TabHeader(workspace, "Circle"), workspace).Left > settings);
            });
        }

        [Fact]
        public void TheCirclesTab_DrawsTheLargeButtonLeftOfThreeStandardOnesStackedTopDown()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.SelectedTab = model.PluginTabs[0];

            OnWorkspace(model, workspace =>
            {
                Rect large = Box(Named(workspace, "Number circles"), workspace);
                Rect count = Box(Named(workspace, "Count"), workspace);
                Rect clear = Box(Named(workspace, "Clear marks"), workspace);
                Rect sizes = Box(Named(workspace, "Diameter 10"), workspace);

                Assert.True(large.Right <= count.Left + 0.5);
                Assert.Equal(count.Left, clear.Left, 1);
                Assert.True(count.Bottom <= clear.Top + 0.5 && clear.Bottom <= sizes.Top + 0.5);
                Assert.True(large.Height > count.Height * 2);
            });
        }

        [Fact]
        public void ThePluginsTab_DrawsTheCircleToolsPanelBetweenCommandPluginsAndManage()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);
            model.SelectedTab = model.PluginsTab;

            OnWorkspace(model, workspace =>
            {
                double Left(string title) =>
                    Box(
                        Descendants(workspace).OfType<TextBlock>().First(t => t.ActualWidth > 0 && t.Text == title),
                        workspace
                    ).Left;

                double commands = Left("Command plugins");
                double tools = Left("Circle tools");
                double manage = Left("Manage");

                Assert.True(commands < tools && tools < manage, commands + " < " + tools + " < " + manage);
            });
        }

        [Fact]
        public void TheDrawingsHeader_DrawsThePluginsButtonLeftOfRefreshAndOpen()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadSample(model);

            OnWorkspace(model, workspace =>
            {
                Rect plugin = Box(Named(workspace, "Count the circles"), workspace);
                Rect refresh = Box(
                    Descendants(workspace).OfType<Button>().First(b => Equals(b.ToolTip, "Look for new files")),
                    workspace
                );
                Rect open = Box(
                    Descendants(workspace).OfType<Button>().First(b => Equals(b.ToolTip, "Open a DXF (Ctrl+O)")),
                    workspace
                );

                Assert.True(plugin.Right <= refresh.Left + 0.5 && plugin.Right <= open.Left + 0.5);
                Assert.Equal(refresh.Top, plugin.Top, 1);
            });
        }

        #endregion
    }
}
