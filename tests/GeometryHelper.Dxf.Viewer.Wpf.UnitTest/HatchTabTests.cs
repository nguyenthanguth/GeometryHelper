using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The contextual Hatch Creation tab (shown while HATCH runs, HPDLGMODE 2) and Hatch Editor tab (shown while hatches are
    /// selected): their values those of the settings of the next hatch or of the hatch, each value written at once (one undo step
    /// named HATCHEDIT on a hatch), the buttons of the Boundaries and Origin panels running the commands of AutoCAD's own words.
    /// </summary>
    public sealed class HatchTabTests
    {
        /// <summary>
        /// A drawing of R2013 with layers A and B, a plate 100 by 60 of four lines on the layer 0 and a second rectangle of four
        /// lines 100 further along; the first is hatched on layer A where <paramref name="hatched"/>.
        /// </summary>
        private static MainViewModel Plate(bool hatched, out List<DxfLine> second)
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "A" });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "B" });
            second = new List<DxfLine>();

            foreach (double x in new[] { 0.0, 200.0 })
            {
                GeoPoint3[] corners =
                {
                    new GeoPoint3(x, 0, 0),
                    new GeoPoint3(x + 100, 0, 0),
                    new GeoPoint3(x + 100, 60, 0),
                    new GeoPoint3(x, 60, 0),
                };

                for (int i = 0; i < 4; i++)
                {
                    var line = new DxfLine(corners[i], corners[(i + 1) % 4]);
                    database.ModelSpace.AppendEntity(line);

                    if (x > 0)
                    {
                        second.Add(line);
                    }
                }
            }

            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "plate.dxf"));
            model.Document.HatchSettings.Layer = "A";

            if (hatched)
            {
                model.Execute("HATCH");
                model.Execute("5,5");
                model.Execute(string.Empty);
            }

            return model;
        }

        private static DxfHatch OnlyHatch(MainViewModel model) =>
            model.Document.ModelSpace.Block.OfType<DxfHatch>().Single();

        private static void Select(MainViewModel model, params string[] handles) =>
            model.Document.Selection.Set(handles);

        #region The Hatch Creation tab

        [Fact]
        public void TheHatchCreationTab_IsShownWhileHatchRuns_AndHiddenWhenItEnds()
        {
            MainViewModel model = Plate(false, out _);
            Assert.False(model.HatchCreationTab.IsTabVisible);
            Assert.Null(model.HatchTab);

            model.Execute("HATCH");

            Assert.True(model.HatchCreationTab.IsTabVisible);
            Assert.Equal("Hatch Creation", model.HatchCreationTab.TabHeader);
            Assert.Same(model.HatchCreationTab, model.SelectedTab);
            HatchTabViewModel tab = model.HatchTab;
            Assert.True(tab.IsCreation);
            Assert.Equal("Hatch Creation", tab.Title);
            Assert.Equal("Close Hatch Creation", tab.CloseText);
            Assert.Equal("Pattern", tab.Type);
            Assert.Equal("ANSI31", tab.PatternChoice);
            Assert.Equal("0", tab.Angle);
            Assert.Equal("1", tab.Scale);
            Assert.True(tab.Associative);

            model.Escape();

            Assert.False(model.HatchCreationTab.IsTabVisible);
            Assert.Null(model.HatchTab);
            Assert.Null(model.SelectedTab);
        }

        [Fact]
        public void TheHatchCreationTab_IsShownForGradientToo_ButNotAtHpdlgmodeOneOrZero()
        {
            MainViewModel model = Plate(false, out _);
            model.Execute("GRADIENT");
            Assert.True(model.HatchCreationTab.IsTabVisible);
            model.Escape();

            foreach (string mode in new[] { "1", "0" })
            {
                model.Execute("HPDLGMODE");
                model.Execute(mode);
                model.Execute("HATCH");
                Assert.False(model.HatchCreationTab.IsTabVisible);
                model.Escape();
            }

            model.Execute("HPDLGMODE");
            model.Execute("2");
            model.Execute("HATCH");
            Assert.True(model.HatchCreationTab.IsTabVisible);
        }

        [Fact]
        public void AValueChangedInTheHatchCreationTab_IsTheSettingOfTheNextHatchAtOnce()
        {
            MainViewModel model = Plate(false, out _);
            model.Execute("HATCH");
            HatchTabViewModel tab = model.HatchTab;
            CadHatchSettings next = model.Document.HatchSettings;

            tab.Angle = "30";
            tab.Scale = "2.5";
            tab.PatternChoice = "ANSI32";
            tab.Layer = "B";
            tab.Transparency = "50";
            tab.Associative = false;
            tab.Annotative = true;
            tab.GapTolerance = "3";

            Assert.Same(next, model.Document.HatchSettings);
            Assert.Equal(30.0, next.Angle, 9);
            Assert.Equal(2.5, next.Scale, 9);
            Assert.Equal("ANSI32", next.PatternName);
            Assert.Equal("B", next.Layer);
            Assert.Equal(127, next.Transparency.Value.Alpha);
            Assert.False(next.Associative);
            Assert.True(next.Annotative);
            Assert.Equal(3.0, next.GapTolerance, 9);
            Assert.Equal("30", tab.Angle);
            Assert.Equal("50", tab.Transparency);
            Assert.Equal("ANSI32", tab.PatternChoice);

            // The variables of AutoCAD read what the tab wrote.
            model.Escape();
            model.Execute("HPANG");
            Assert.Equal("Enter new value for HPANG <30>:", model.Prompt);
        }

        [Fact]
        public void TheTypeOfTheHatchCreationTab_ChangesWhatTheGalleryAndTheFieldsShow()
        {
            MainViewModel model = Plate(false, out _);
            model.Execute("HATCH");
            HatchTabViewModel tab = model.HatchTab;
            Assert.True(tab.IsPattern);
            Assert.True(tab.HasScale);
            Assert.Contains("ANSI31", tab.PatternChoices);

            tab.Type = "Gradient";
            Assert.True(tab.IsGradient);
            Assert.False(tab.HasScale);
            Assert.Equal(CadHatchSettings.GradientNames, tab.PatternChoices);
            tab.PatternChoice = "GR_CYLIN";
            Assert.Equal("GR_CYLIN", model.Document.HatchSettings.GradientName);

            tab.Type = "User defined";
            Assert.True(tab.IsUserDefined);
            tab.Spacing = "4";
            tab.Double = true;
            Assert.Equal(4.0, model.Document.HatchSettings.Spacing, 9);
            Assert.True(model.Document.HatchSettings.Double);

            tab.Type = "Solid";
            Assert.False(tab.HasAngle);
            Assert.False(tab.HasBackground);

            tab.Type = "Pattern";
            tab.PatternChoice = "ACAD_ISO02W100";
            Assert.True(tab.IsIso);
            tab.IsoPenWidth = "0.50 mm";
            Assert.Equal(0.5, model.Document.HatchSettings.Scale, 9);
        }

        [Fact]
        public void ANumberTheHatchCreationTabCannotTake_IsSaidAndLeavesTheSettingsAlone()
        {
            MainViewModel model = Plate(false, out _);
            model.Execute("HATCH");
            HatchTabViewModel tab = model.HatchTab;

            tab.Angle = "ten";
            Assert.Equal("The angle is a number.", tab.Message);
            Assert.Equal(0.0, model.Document.HatchSettings.Angle, 9);
            tab.Scale = "0";
            Assert.Equal("The scale is a number above nought.", tab.Message);
            Assert.Equal(1.0, model.Document.HatchSettings.Scale, 9);
            tab.GapTolerance = "6000";
            Assert.Equal("The gap tolerance is 0 to 5000.", tab.Message);
            Assert.Equal(0.0, model.Document.HatchSettings.GapTolerance, 9);
            tab.Transparency = "95";
            Assert.Equal("The transparency is 0 to 90, ByLayer or ByBlock.", tab.Message);

            tab.Angle = "15";
            Assert.Equal(string.Empty, tab.Message);
        }

        [Fact]
        public void TheOptionsOfTheHatchCreationTab_AreThoseHatchKeeps()
        {
            MainViewModel model = Plate(false, out _);
            model.Execute("HATCH");
            HatchTabViewModel tab = model.HatchTab;
            Assert.Equal("Normal Island Detection", tab.IslandDetection);
            Assert.Equal("Send behind boundary", tab.DrawOrder);

            tab.IslandDetection = "No Island Detection";
            tab.SeparateHatches = true;
            tab.RetainBoundaries = true;
            tab.DrawOrder = "Send to back";
            tab.StoreAsDefaultOrigin = true;

            Assert.Equal(0.0, model.Session.Remembered["hatch.islands"]);
            Assert.Equal(1.0, model.Session.Remembered["hatch.separate"]);
            Assert.Equal(1.0, model.Session.Remembered["hatch.retain"]);
            Assert.Equal(1.0, model.Session.Remembered["hatch.order"]);
            Assert.Equal(1.0, model.Session.Remembered["hatch.origin.store"]);

            tab.IslandDetection = "Outer Island Detection";
            Assert.Equal(1.0, model.Session.Remembered["hatch.islands"]);
            Assert.Equal(DxfHatchStyle.Outer, model.Document.HatchSettings.IslandStyle);
            Assert.Equal("Outer Island Detection", tab.IslandDetection);
        }

        [Fact]
        public void TheCloseButtonOfTheHatchCreationTab_MakesTheHatchOfTheAreasPicked_WithTheValuesOfTheTab()
        {
            MainViewModel model = Plate(false, out _);
            model.Execute("HATCH");
            HatchTabViewModel tab = model.HatchTab;
            tab.Angle = "45";
            tab.Layer = "B";
            model.Execute("5,5");

            tab.CloseCommand.Execute(null);

            DxfHatch hatch = OnlyHatch(model);
            Assert.Equal(45.0, hatch.PatternAngle * 180.0 / Math.PI, 9);
            Assert.Equal("B", hatch.Layer);
            Assert.False(model.HatchCreationTab.IsTabVisible);
        }

        [Fact]
        public void TheBoundaryButtonsOfTheHatchCreationTab_AnswerHatchWithItsOwnKeywords()
        {
            MainViewModel model = Plate(false, out List<DxfLine> second);
            model.Execute("HATCH");
            HatchTabViewModel tab = model.HatchTab;
            Assert.True(tab.CanEditBoundaries);

            tab.SelectCommand.Execute(null);
            Assert.Equal("Select objects:", model.Prompt);
            model.Escape();

            model.Execute("HATCH");
            tab = model.HatchTab;
            model.Execute("5,5");
            model.Execute("205,5");
            tab.RemoveCommand.Execute(null);
            Assert.Equal("Select objects or [Add boundaries]:", model.Prompt);
            Assert.NotEmpty(second);
        }

        [Fact]
        public void TheOriginButtonsOfTheHatchCreationTab_AskTheOriginOfHatch()
        {
            MainViewModel model = Plate(false, out _);
            model.Execute("HATCH");
            HatchTabViewModel tab = model.HatchTab;

            tab.SetOriginCommand.Execute(null);
            Assert.Equal("Select point:", model.Prompt);
            model.Execute("10,5");
            Assert.StartsWith("Store as default origin?", model.Prompt, StringComparison.Ordinal);
            model.Execute("Y");
            Assert.Equal(10.0, model.Document.HatchSettings.OriginX, 9);
            Assert.Equal(5.0, model.Document.HatchSettings.OriginY, 9);
            Assert.Equal("10, 5", tab.OriginText);

            tab.OriginCornerCommand.Execute("C");
            Assert.Equal(CadHatchOriginMode.Center, model.Document.HatchSettings.OriginMode);
            tab.UseCurrentOriginCommand.Execute(null);
            Assert.Equal(CadHatchOriginMode.Current, model.Document.HatchSettings.OriginMode);
        }

        #endregion

        #region The Hatch Editor tab

        [Fact]
        public void TheHatchEditorTab_IsShownWhileAHatchIsSelected_AndHiddenWhenNothingIs()
        {
            MainViewModel model = Plate(true, out List<DxfLine> second);
            DxfHatch hatch = OnlyHatch(model);
            Assert.False(model.HatchEditorTab.IsTabVisible);

            Select(model, hatch.Handle);

            Assert.True(model.HatchEditorTab.IsTabVisible);
            Assert.False(model.HatchCreationTab.IsTabVisible);
            Assert.Equal("Hatch Editor", model.HatchEditorTab.TabHeader);
            Assert.Same(model.HatchEditorTab, model.SelectedTab);
            HatchTabViewModel tab = model.HatchTab;
            Assert.True(tab.IsEditor);
            Assert.Equal("Close Hatch Editor", tab.CloseText);
            Assert.Equal(new[] { hatch.Handle }, tab.Handles.ToArray());
            Assert.Equal("Pattern", tab.Type);
            Assert.Equal("ANSI31", tab.PatternChoice);
            Assert.Equal("A", tab.Layer);
            Assert.True(tab.Associative);
            Assert.True(tab.CanEditBoundaries);

            // Other objects selected with it do not hide it; another object alone does.
            Select(model, hatch.Handle, second[0].Handle);
            Assert.True(model.HatchEditorTab.IsTabVisible);
            Select(model, second[0].Handle);
            Assert.False(model.HatchEditorTab.IsTabVisible);
            Assert.Null(model.HatchTab);

            Select(model, hatch.Handle);
            model.Document.Selection.Clear();
            Assert.False(model.HatchEditorTab.IsTabVisible);
        }

        [Fact]
        public void AValueChangedInTheHatchEditorTab_ChangesTheHatch_OneUndoStepNamedHatchEdit()
        {
            MainViewModel model = Plate(true, out _);
            Select(model, OnlyHatch(model).Handle);
            HatchTabViewModel tab = model.HatchTab;

            tab.Angle = "30";
            Assert.Equal(30.0, OnlyHatch(model).PatternAngle * 180.0 / Math.PI, 9);
            Assert.Equal("HATCHEDIT", model.Document.NextUndo.Name);

            tab.Scale = "2";
            Assert.Equal(2.0, OnlyHatch(model).PatternScale, 9);

            tab.PatternChoice = "ANSI32";
            Assert.Equal("ANSI32", OnlyHatch(model).PatternName);
            Assert.Equal("ANSI32", tab.PatternChoice);

            tab.Layer = "B";
            Assert.Equal("B", OnlyHatch(model).Layer);

            tab.Transparency = "50";
            Assert.Equal(127, OnlyHatch(model).Transparency.Alpha);
            Assert.Equal("50", tab.Transparency);

            tab.Type = "Solid";
            Assert.True(OnlyHatch(model).IsSolidFill);

            tab.Type = "Gradient";
            Assert.True(OnlyHatch(model).IsGradient);

            // The tab stays over the hatch through every change, and each is one step the undo takes back.
            Assert.True(model.HatchEditorTab.IsTabVisible);
            Assert.Single(model.HatchTab.Handles);
            model.Document.Undo();
            Assert.True(OnlyHatch(model).IsSolidFill);
            model.Document.Undo();
            Assert.False(OnlyHatch(model).IsSolidFill);
            Assert.False(OnlyHatch(model).IsGradient);
        }

        [Fact]
        public void TheHatchEditorTab_KeepsTheOriginOfTheHatch_WhenAnotherValueIsChanged()
        {
            MainViewModel model = Plate(true, out _);
            CadHatchOrigin.MoveTo(OnlyHatch(model), new GeoPoint2(7, 3));
            Select(model, OnlyHatch(model).Handle);

            model.HatchTab.Scale = "2";

            Assert.Equal(7.0, CadHatchOrigin.Of(OnlyHatch(model)).X, 6);
            Assert.Equal(3.0, CadHatchOrigin.Of(OnlyHatch(model)).Y, 6);
        }

        [Fact]
        public void TheHatchEditorTab_ChangesEveryHatchSelected()
        {
            MainViewModel model = Plate(true, out _);
            model.Execute("HATCH");
            model.Execute("205,5");
            model.Execute(string.Empty);
            string[] handles = model.Document.ModelSpace.Block.OfType<DxfHatch>().Select(h => h.Handle).ToArray();
            Assert.Equal(2, handles.Length);
            Select(model, handles);
            HatchTabViewModel tab = model.HatchTab;
            Assert.False(tab.CanEditBoundaries);

            tab.Angle = "60";

            foreach (DxfHatch hatch in model.Document.ModelSpace.Block.OfType<DxfHatch>())
            {
                Assert.Equal(60.0, hatch.PatternAngle * 180.0 / Math.PI, 9);
            }
        }

        [Fact]
        public void TheColorOfTheHatchEditorTab_IsPickedInTheSelectColorDialog_AndTheBackgroundCanBeTakenOff()
        {
            MainViewModel model = Plate(true, out _);
            Select(model, OnlyHatch(model).Handle);
            HatchTabViewModel tab = model.HatchTab;
            model.PickColor = dialog =>
            {
                dialog.Red = 10;
                dialog.Green = 20;
                dialog.Blue = 30;
                return true;
            };

            tab.PickColorCommand.Execute(null);
            Assert.Equal(0x0A141E, OnlyHatch(model).Color.ColorValue);
            Assert.Equal("10,20,30", tab.ColorText);
            Assert.Equal(0x0A141E, tab.ColorRgb);

            tab.PickBackgroundCommand.Execute(null);
            Assert.Equal(0x0A141E, OnlyHatch(model).BackgroundColor.ColorValue);
            Assert.Equal("10,20,30", tab.BackgroundText);

            tab.NoBackgroundCommand.Execute(null);
            Assert.True(OnlyHatch(model).BackgroundColor.IsNone);
            Assert.Equal("None", tab.BackgroundText);
        }

        [Fact]
        public void TheAssociativeBoxOfTheHatchEditorTab_LetsGoOfTheObjects_AndAsksForThemAgain()
        {
            MainViewModel model = Plate(true, out _);
            Select(model, OnlyHatch(model).Handle);
            HatchTabViewModel tab = model.HatchTab;

            tab.Associative = false;
            Assert.False(OnlyHatch(model).Associative);
            Assert.False(tab.Associative);
            Assert.False(tab.DisplayBoundaryCommand.CanExecute(null));

            tab.Associative = true;
            Assert.Equal("Specify internal point or [Select objects]:", model.Prompt);
            model.Execute("5,5");
            model.Execute(string.Empty);

            Assert.True(OnlyHatch(model).Associative);
            Assert.True(model.HatchEditorTab.IsTabVisible);
            Assert.True(model.HatchTab.DisplayBoundaryCommand.CanExecute(null));
        }

        [Fact]
        public void TheRecreateButton_RunsRecreateBoundary_AndTheHatchStaysSelectedWithItsTab()
        {
            MainViewModel model = Plate(true, out _);
            string handle = OnlyHatch(model).Handle;
            Select(model, handle);
            HatchTabViewModel tab = model.HatchTab;

            tab.RecreateCommand.Execute(null);

            Assert.Equal("Enter type of boundary object [Region/Polyline] <Polyline>:", model.Prompt);
            Assert.True(model.HatchEditorTab.IsTabVisible);
            model.Execute(string.Empty);
            Assert.StartsWith("Reassociate hatch with new boundary?", model.Prompt, StringComparison.Ordinal);
            model.Execute("Y");

            Assert.False(model.Session.IsRunning);
            Assert.Single(model.Document.ModelSpace.Block.OfType<DxfPolyline>());
            Assert.Contains(handle, model.Document.Selection.Handles);
            Assert.True(model.HatchEditorTab.IsTabVisible);
            DxfEntity followed = Assert.Single(OnlyHatch(model).GetAssociatedObjects());
            Assert.IsType<DxfPolyline>(followed);
        }

        [Fact]
        public void ThePickPointsSelectAndRemoveButtons_RunTheOptionsOfHatcheditOnTheHatch()
        {
            MainViewModel model = Plate(true, out List<DxfLine> second);
            string handle = OnlyHatch(model).Handle;
            Select(model, handle);
            HatchTabViewModel tab = model.HatchTab;

            tab.PickPointsCommand.Execute(null);
            Assert.Equal("Specify internal point or [Select objects]:", model.Prompt);
            model.Execute("205,5");
            model.Execute(string.Empty);
            Assert.Equal(2, OnlyHatch(model).NumberOfLoops);
            Assert.True(model.HatchEditorTab.IsTabVisible);

            tab = model.HatchTab;
            tab.RemoveCommand.Execute(null);
            Assert.Equal("Select objects:", model.Prompt);
            model.Escape();
            Assert.Contains(handle, model.Document.Selection.Handles);

            tab = model.HatchTab;
            tab.SelectCommand.Execute(null);
            Assert.Equal("Select objects:", model.Prompt);
            Assert.NotEmpty(second);
        }

        [Fact]
        public void TheDisplayBoundaryObjectsButton_SelectsTheObjectsTheHatchFollows_AndTheTabStays()
        {
            MainViewModel model = Plate(true, out List<DxfLine> second);
            DxfHatch hatch = OnlyHatch(model);
            Select(model, hatch.Handle);
            HatchTabViewModel tab = model.HatchTab;
            Assert.True(tab.DisplayBoundaryCommand.CanExecute(null));

            tab.DisplayBoundaryCommand.Execute(null);

            Assert.Equal(5, model.Document.Selection.Count);
            Assert.Contains(hatch.Handle, model.Document.Selection.Handles);
            Assert.DoesNotContain(second[0].Handle, model.Document.Selection.Handles);
            Assert.True(model.HatchEditorTab.IsTabVisible);
        }

        [Fact]
        public void TheOriginButtonsOfTheHatchEditorTab_LayThePatternFromAPointOrACorner()
        {
            MainViewModel model = Plate(true, out _);
            Select(model, OnlyHatch(model).Handle);
            HatchTabViewModel tab = model.HatchTab;

            tab.SetOriginCommand.Execute(null);
            Assert.Equal("Select point:", model.Prompt);
            model.Execute("10,5");
            Assert.Equal(10.0, CadHatchOrigin.Of(OnlyHatch(model)).X, 6);
            Assert.Equal(5.0, CadHatchOrigin.Of(OnlyHatch(model)).Y, 6);

            tab = model.HatchTab;
            tab.OriginCornerCommand.Execute("R");
            Assert.Equal(100.0, CadHatchOrigin.Of(OnlyHatch(model)).X, 6);
            Assert.Equal(0.0, CadHatchOrigin.Of(OnlyHatch(model)).Y, 6);
            Assert.True(model.HatchEditorTab.IsTabVisible);
        }

        [Fact]
        public void TheCloseButtonOfTheHatchEditorTab_LetsGoOfTheHatch()
        {
            MainViewModel model = Plate(true, out _);
            Select(model, OnlyHatch(model).Handle);

            model.HatchTab.CloseCommand.Execute(null);

            Assert.False(model.HatchEditorTab.IsTabVisible);
            Assert.Equal(0, model.Document.Selection.Count);
        }

        [Fact]
        public void TheSettingsButton_ShowsTheHatchDialogOfHatchedit_WhereAWindowShowsIt()
        {
            MainViewModel model = Plate(true, out _);
            Select(model, OnlyHatch(model).Handle);
            bool shown = false;
            model.ShowHatchSettings = dialog =>
            {
                shown = dialog.Title == "Hatch Editor";
                return false;
            };

            model.HatchTab.SettingsCommand.Execute(null);

            Assert.True(shown);
            Assert.True(model.HatchEditorTab.IsTabVisible);
        }

        #endregion

        #region In the window

        [Fact]
        public void TheHatchTabs_AreLaidOutInTheWindow_WithTheirPanelsAndButtons()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                MainViewModel model = Plate(true, out _);
                Select(model, OnlyHatch(model).Handle);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace, 2200, 960);

                HatchRibbon ribbon = Descendants(workspace).OfType<HatchRibbon>().Single();
                List<Button> buttons = Descendants(ribbon).OfType<Button>().ToList();

                foreach (
                    string name in new[]
                    {
                        "Pick Points",
                        "Select",
                        "Remove",
                        "Recreate",
                        "Display Boundary Objects",
                        "Set Origin",
                        "Close Hatch Editor",
                    }
                )
                {
                    Button button = buttons.Single(b => name.Equals(b.Content));
                    Assert.True(button.IsEnabled, name);
                    Assert.Equal(Visibility.Visible, button.Visibility);
                }

                foreach (string title in new[] { "Boundaries", "Pattern", "Properties", "Origin", "Options", "Close" })
                {
                    Assert.Contains(Descendants(ribbon).OfType<TextBlock>(), t => title.Equals(t.Text));
                }

                Assert.Contains(Descendants(workspace).OfType<System.Windows.Controls.Primitives.ToggleButton>(), t =>
                    "Hatch Editor".Equals(t.Content) && t.Visibility == Visibility.Visible
                );

                // The Hatch Creation tab has no Recreate and no Display Boundary Objects, and closes the creation.
                model.Document.Selection.Clear();
                model.Execute("HATCH");
                Layout(workspace, 2200, 960);
                ribbon = Descendants(workspace).OfType<HatchRibbon>().Single();
                buttons = Descendants(ribbon).OfType<Button>().ToList();
                Assert.Equal(Visibility.Collapsed, buttons.Single(b => "Recreate".Equals(b.Content)).Visibility);
                Assert.Equal(
                    Visibility.Collapsed,
                    buttons.Single(b => "Display Boundary Objects".Equals(b.Content)).Visibility
                );
                Assert.Contains(buttons, b => "Close Hatch Creation".Equals(b.Content));
            });
        }

        private static void Layout(FrameworkElement element, double width, double height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
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

        #endregion
    }
}
