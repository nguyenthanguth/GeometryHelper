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
    /// The Parametric tab, the Constraint Settings dialog and the Parameters Manager palette: their view models (what the boxes
    /// hold, what OK hands back, the rows and how an edit of one reaches the drawing), the commands driven through the main
    /// view model with the windows stubbed, and the windows laid out offscreen.
    /// </summary>
    public sealed class ParametricTabTests
    {
        private static MainViewModel LineOfTen(out DxfLine line)
        {
            var database = new DxfDatabase("AC1027");
            line = new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(10, 1, 0) };
            database.ModelSpace.AppendEntity(line);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "plan.dxf"), "plan.dxf");
            return model;
        }

        private static void Constrain(MainViewModel model)
        {
            model.StartCommand("DCHORIZONTAL");
            model.Session.Feed(new GeoPoint2(1, 0.1));
            model.Session.Feed(new GeoPoint2(9, 0.9));
            model.Session.Feed(new GeoPoint2(5, 4));
            model.Enter();
        }

        [Fact]
        public void TheParametricTab_StandsBetweenAnnotateAndViewAsAutoCadsDoes()
        {
            var model = new MainViewModel();

            Assert.Equal("Parametric", model.ParametricTab.TabHeader);
            Assert.IsType<ParametricRibbonViewModel>(model.ParametricTab.TabContent);
            model.SelectTabCommand.Execute(model.ParametricTab);
            Assert.True(model.ParametricTab.IsTabSelected);
            Assert.False(model.ViewTab.IsTabSelected);
        }

        [Fact]
        public void ShowAllAndHideAll_ShowAndHideTheBarsOfTheWorkingSpace()
        {
            MainViewModel model = LineOfTen(out DxfLine line);
            model.StartCommand("GCHORIZONTAL");
            model.Session.Feed(new GeoPoint2(5, 0.5));
            var ribbon = (ParametricRibbonViewModel)model.ParametricTab.TabContent;
            var settings = new CadConstraintSettings();
            CadDocument document = model.Document;
            document.ConstraintBars.EndCommand();

            Assert.Empty(document.ConstraintBars.BarsFor(document.ModelSpace, new string[0], settings));
            ribbon.ShowAllCommand.Execute(null);
            Assert.Single(document.ConstraintBars.BarsFor(document.ModelSpace, new string[0], settings));
            ribbon.HideAllCommand.Execute(null);
            Assert.Empty(document.ConstraintBars.BarsFor(document.ModelSpace, new[] { line.Handle }, settings));
        }

        [Fact]
        public void TheSettingsDialog_OpensOnTheSessionsSettings_AndOkHandsBackWhatTheBoxesHold()
        {
            var request = new CadConstraintSettingsRequest(new CadConstraintSettings(), 1);
            var dialog = new ConstraintSettingsViewModel(request);

            Assert.True(dialog.IsDimensionalTab);
            Assert.Equal(12, dialog.BarTypes.Count);
            Assert.Equal(2, dialog.NameFormat);
            Assert.True(dialog.Dynamic);
            Assert.Equal("0.01", dialog.DistanceText);
            Assert.Equal("1", dialog.AngleText);
            Assert.Equal(CadConstraintType.Horizontal, dialog.Rules[0].Type);
            Assert.True(dialog.ShowBarsAfterApplying);
            Assert.True(dialog.ShowBarsOfSelection);

            dialog.Annotational = true;
            dialog.NameFormat = 0;
            dialog.DistanceText = "0.5";
            dialog.AngleText = "3";
            dialog.ShowBarsAfterApplying = false;
            dialog.InferConstraints = true;
            dialog.BarTypes.Single(t => t.Type == CadConstraintType.Equal).Shown = false;
            dialog.SelectedRule = dialog.Rules[1];
            dialog.MoveUpCommand.Execute(null);
            dialog.Rules.Single(r => r.Type == CadConstraintType.Fix).Enabled = true;
            dialog.Commit();

            CadConstraintSettings s = request.Settings;
            Assert.Equal(CadDialogAction.Ok, request.Action);
            Assert.Equal(1, s.Form);
            Assert.Equal(0, s.NameFormat);
            Assert.Equal(0.5, s.AutoDistance);
            Assert.Equal(3.0, s.AutoAngle);
            Assert.Equal(1, s.BarDisplay);
            Assert.True(s.Infer);
            Assert.Equal(4095 & ~(1 << (int)CadConstraintType.Equal), s.BarMode);
            Assert.Equal(CadConstraintType.Vertical, s.AutoRules[0].Type);
            Assert.True(s.AutoRules.Single(r => r.Type == CadConstraintType.Fix).Enabled);
        }

        [Fact]
        public void TheSettingsDialog_RefusesATolerance_ThatIsNotANumber()
        {
            var dialog = new ConstraintSettingsViewModel(
                new CadConstraintSettingsRequest(new CadConstraintSettings(), 2)
            );

            Assert.True(dialog.IsAutoConstrainTab);
            Assert.True(dialog.IsValid);
            dialog.DistanceText = "near";
            Assert.False(dialog.IsValid);
            Assert.Contains("distance", dialog.Message, StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => dialog.Commit());
        }

        [Fact]
        public void ConstraintSettings_IsShownThroughTheWorkspace_AndOkTakesTheSettings()
        {
            MainViewModel model = LineOfTen(out _);
            model.ShowConstraintSettings = dialog =>
            {
                dialog.Annotational = true;
                return true;
            };

            model.StartCommand("CONSTRAINTSETTINGS");

            Assert.Equal(1, model.Session.Constraints.Form);
        }

        [Fact]
        public void ConstraintSettings_WithNoWindow_LeavesTheSettingsAlone()
        {
            MainViewModel model = LineOfTen(out _);

            model.StartCommand("CONSTRAINTSETTINGS");

            Assert.Equal(0, model.Session.Constraints.Form);
        }

        [Fact]
        public void TheParametersPalette_ListsTheParameters_AndAnEditedExpressionMovesTheLine()
        {
            MainViewModel model = LineOfTen(out DxfLine line);
            Constrain(model);
            var palette = new ParametersPaletteViewModel { Document = model.Document };

            ParameterRowViewModel row = Assert.Single(palette.Rows);
            Assert.Equal("d1", row.Name);
            Assert.Equal("10", row.Expression);
            Assert.False(row.IsUser);
            Assert.StartsWith("10", row.Value, StringComparison.Ordinal);

            row.Expression = "20";

            Assert.Equal(20.0, line.EndPoint.X, 6);
            Assert.Equal("20", palette.Rows[0].Expression);
            Assert.Equal(string.Empty, palette.Status);
        }

        [Fact]
        public void TheParametersPalette_SaysWhyAnEditIsRefused_AndPutsTheRowBack()
        {
            MainViewModel model = LineOfTen(out _);
            Constrain(model);
            var palette = new ParametersPaletteViewModel { Document = model.Document };

            palette.Rows[0].Expression = "nothing*2";

            Assert.Contains("nothing", palette.Status, StringComparison.Ordinal);
            Assert.Equal("10", palette.Rows[0].Expression);
        }

        [Fact]
        public void TheParametersPalette_MakesAndDeletesUserParameters_AndFollowsTheDocument()
        {
            MainViewModel model = LineOfTen(out _);
            Constrain(model);
            var palette = new ParametersPaletteViewModel { Document = model.Document };

            palette.NewCommand.Execute(null);
            Assert.Equal(new[] { "d1", "user1" }, palette.Rows.Select(r => r.Name).ToArray());
            Assert.True(palette.Rows[1].IsUser);

            palette.Selected = palette.Rows[1];
            palette.DeleteCommand.Execute(null);
            Assert.Single(palette.Rows);

            palette.Selected = palette.Rows[0];
            Assert.False(palette.DeleteCommand.CanExecute(null));
            Assert.True(model.Document.Undo());
            Assert.Equal(2, palette.Rows.Count);
        }

        [Fact]
        public void Parameters_AsksTheWorkspaceToShowThePalette_AndParametersCloseToClose()
        {
            MainViewModel model = LineOfTen(out _);
            Constrain(model);
            var shown = new List<bool>();
            model.ShowParameters = (palette, show) => shown.Add(show);

            model.StartCommand("PARAMETERS");
            Assert.Equal(new[] { true }, shown.ToArray());
            Assert.True(model.Session.ParametersShown);

            model.StartCommand("PARAMETERSCLOSE");
            Assert.Equal(new[] { true, false }, shown.ToArray());
            Assert.False(model.Session.ParametersShown);
        }

        [Fact]
        public void TheWindows_AreLaidOutOffscreen_WithTheirTabsAndBoxes()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                var dialog = new ConstraintSettingsWindow(
                    new ConstraintSettingsViewModel(new CadConstraintSettingsRequest(new CadConstraintSettings(), 0))
                );
                Assert.Equal("Constraint Settings", dialog.Title);
                FrameworkElement body = dialog.Body;
                object context = dialog.DataContext;
                dialog.Content = null;
                body.DataContext = context;
                body.Measure(new Size(560, 520));
                body.Arrange(new Rect(0, 0, 560, 520));
                body.UpdateLayout();

                List<string> boxes = Descendants(body).OfType<CheckBox>().Select(c => c.Content as string).ToList();
                Assert.Contains("Horizontal", boxes);
                Assert.Contains("Symmetric", boxes);
                Assert.Contains("Show constraint bars after applying constraints to selected objects", boxes);
                Assert.Equal(
                    new[] { "Geometric", "Dimensional", "AutoConstrain" },
                    Descendants(body).OfType<RadioButton>().Select(r => r.Content as string).Take(3).ToArray()
                );

                var palette = new ParametersPaletteWindow(new ParametersPaletteViewModel());
                Assert.Equal("Parameters Manager", palette.Title);
                FrameworkElement pbody = palette.Body;
                object pcontext = palette.DataContext;
                palette.Content = null;
                pbody.DataContext = pcontext;
                pbody.Measure(new Size(520, 380));
                pbody.Arrange(new Rect(0, 0, 520, 380));
                pbody.UpdateLayout();
                Assert.Contains(Descendants(pbody).OfType<TextBlock>(), t => t.Text == "Expression");
                Assert.Contains(Descendants(pbody).OfType<Button>(), b =>
                    (b.Content as string) == "New User Parameter"
                );
            });
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject deeper in Descendants(child))
                {
                    yield return deeper;
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
