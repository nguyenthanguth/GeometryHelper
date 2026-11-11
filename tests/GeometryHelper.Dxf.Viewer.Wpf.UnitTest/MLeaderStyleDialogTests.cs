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
    /// The Multileader Style Manager (MLEADERSTYLE) and its Modify dialog, the multileader style box of the Annotate tab, and
    /// MATCHPROP's Property Settings with the source's values, every dialog stubbed or laid out offscreen as the design draws it.
    /// </summary>
    public sealed class MLeaderStyleDialogTests
    {
        private static MainViewModel Opened(out CadDocument document)
        {
            var model = new MainViewModel();
            document = CadDocument.From(CadTemplate.CreateMetric(), "plan-level2.dxf");
            model.OpenDocument(document);
            return model;
        }

        [Fact]
        public void TheManager_GivesADrawingOfNoStyleAMetricStandard_AsOneUndoStep()
        {
            MainViewModel model = Opened(out CadDocument document);
            Assert.Null(document.Database.MLeaderStyleDictionary);

            MLeaderStyleManagerViewModel manager = model.CreateMLeaderStyleManager();

            Assert.Equal(new[] { "Standard" }, manager.Styles.Select(s => s.Name));
            Assert.True(manager.Styles[0].IsCurrent);
            Assert.Equal("Current: Standard", manager.CurrentStyleText);
            Assert.Equal("Multileader style manager · plan-level2.dxf", manager.Title);
            Assert.Equal("1 style · double-click a style to modify it", manager.Footer);
            Assert.Equal(
                "Standard · straight leader, closed filled arrow 2.5 · landing 8 · MTEXT 2.5 in Standard, attached at the middle of top line · Used by 0 multileaders.",
                manager.Description
            );
            Assert.False(manager.CanDelete);
            Assert.False(manager.DeleteCommand.CanExecute(null));

            document.Undo();
            Assert.Null(document.Database.MLeaderStyleDictionary);
        }

        [Fact]
        public void New_AsksTheNameAndTheStartWithoutUseFor_ThenModifyWritesTheStyle()
        {
            MainViewModel model = Opened(out CadDocument document);
            MLeaderStyleManagerViewModel manager = model.CreateMLeaderStyleManager();
            StyleNameViewModel asked = null;
            manager.AskName = ask =>
            {
                asked = ask;
                ask.Name = "Detail notes";
                return ask.Ok() ? ask.Name : null;
            };
            MLeaderStyleEditViewModel shown = null;
            manager.ShowEditor = editor =>
            {
                shown = editor;
                editor.Tab = "Content";
                editor.FieldsOf("Content").Single(f => f.Label == "Text height").Text = "3.5";
                editor.FieldsOf("Leader Format").Single(f => f.Label == "Size").Text = "3";
                return editor.Ok();
            };

            Assert.True(manager.New());

            Assert.Equal("Create New Multileader Style", asked.Title);
            Assert.Equal("Copy of Standard", "Copy of " + asked.StartWith);
            Assert.False(asked.AsksUseFor);
            Assert.Equal("Modify multileader style: Detail notes", shown.Title);
            var style = (DxfMLeaderStyle)document.Database.MLeaderStyleDictionary["Detail notes"];
            Assert.Equal(3.5, style.TextHeight);
            Assert.Equal(3.0, style.ArrowSize);
            Assert.Equal(new[] { "Detail notes", "Standard" }, manager.Styles.Select(s => s.Name));
            Assert.Equal("Detail notes", manager.SelectedStyle.Name);
            Assert.True(manager.CanDelete);

            // A drawing made new is given the class DICTIONARYVAR as AutoCAD 2021 lists it: Set Current makes the style
            // current, and a current style is not deleted.
            Assert.True(manager.SetCurrent());
            Assert.Equal("Current: Detail notes", manager.CurrentStyleText);
            Assert.Equal("Detail notes", DxfDatabase.Parse(document.Database.ToText()).MLeaderstyle);
            Assert.False(manager.CanDelete);

            manager.SelectedStyle = manager.Styles.Single(s => s.Name == "Standard");
            Assert.True(manager.SetCurrent());
            manager.SelectedStyle = manager.Styles.Single(s => s.Name == "Detail notes");
            Assert.True(manager.Delete());
            Assert.Equal(new[] { "Standard" }, manager.Styles.Select(s => s.Name));
        }

        [Fact]
        public void TheAnnotativeBox_MakesTheStyleAnnotative_ByItsGroup296_AsAutoCad2021WritesIt()
        {
            MainViewModel model = Opened(out CadDocument document);
            MLeaderStyleEditViewModel editor = model.CreateMLeaderStyleManager().CreateModify();

            editor.FieldsOf("Leader Structure").Single(f => f.Label == "Annotative").LeadChecked = true;
            Assert.True(editor.Ok());

            var style = (DxfMLeaderStyle)document.Database.MLeaderStyleDictionary["Standard"];
            Assert.Equal(DxfAnnotativeStates.True, style.Annotative);
            Assert.True(model.CreateMLeaderStyleManager().Styles.Single(s => s.Name == "Standard").IsAnnotative);
            Assert.Equal(
                DxfAnnotativeStates.True,
                (
                    (DxfMLeaderStyle)DxfDatabase.Parse(document.Database.ToText()).MLeaderStyleDictionary["Standard"]
                ).Annotative
            );

            document.Undo();
            Assert.Equal(
                DxfAnnotativeStates.False,
                ((DxfMLeaderStyle)document.Database.MLeaderStyleDictionary["Standard"]).Annotative
            );
        }

        [Fact]
        public void TheModifyDialog_GreysWhatIsNotBuilt_AndItsBoxesGreyTheirValues()
        {
            MainViewModel model = Opened(out CadDocument document);
            MLeaderStyleEditViewModel editor = model.CreateMLeaderStyleManager().CreateModify();

            Assert.Equal(new[] { "Leader Format", "Leader Structure", "Content" }, editor.Tabs);
            MLeaderFieldViewModel type = editor.FieldsOf("Content").Single(f => f.Label == "Type");
            Assert.Equal(new[] { "Mtext", "Block", "None" }, type.Choices.Select(c => c.Label));
            Assert.False(type.Choices.Single(c => c.Label == "Block").IsEnabled);
            MLeaderFieldViewModel annotative = editor.FieldsOf("Leader Structure").Single(f => f.Label == "Annotative");
            Assert.True(annotative.IsLeadEnabled);
            Assert.False(annotative.LeadChecked);

            MLeaderFieldViewModel first = editor
                .FieldsOf("Leader Structure")
                .Single(f => f.Label == "First segment angle");
            Assert.False(first.LeadChecked);
            Assert.False(first.IsEnabled);
            first.LeadChecked = true;
            Assert.True(first.IsEnabled);
            first.SelectedChoice = first.Choices.Single(c => c.Label == "45");
            Assert.Equal(DxfAngleConstraint.Degrees045, editor.Settings.FirstSegmentAngle);

            MLeaderFieldViewModel layout = editor
                .FieldsOf("Leader Structure")
                .Single(f => f.Label == "Scale multileaders to layout");
            MLeaderFieldViewModel specify = editor.FieldsOf("Leader Structure").Single(f => f.Label == "Specify scale");
            Assert.True(specify.LeadChecked);
            layout.LeadChecked = true;
            Assert.Equal(0.0, editor.Settings.Scale);
            Assert.False(specify.IsEnabled);

            MLeaderFieldViewModel vertical = editor.FieldsOf("Content").Single(f => f.Label == "Vertical attachment");
            vertical.LeadChecked = true;
            Assert.False(editor.FieldsOf("Content").Single(f => f.Label == "Left attachment").IsShown);
            Assert.True(editor.FieldsOf("Content").Single(f => f.Label == "Top attachment").IsShown);

            Assert.True(editor.Ok());
            var style = (DxfMLeaderStyle)document.Database.MLeaderStyleDictionary["Standard"];
            Assert.Equal(DxfAngleConstraint.Degrees045, style.FirstSegmentAngleConstraint);
            Assert.Equal(0.0, style.Scale);
            Assert.Equal(DxfTextAttachmentDirection.AttachmentVertical, style.TextAttachmentDirection);
        }

        [Fact]
        public void TheMultileaderStyleBox_EndsInTheManagersItem_WhichAsksForIt()
        {
            MainViewModel model = Opened(out CadDocument document);
            string asked = null;
            model.StyleDialogRequested += (sender, name) => asked = name;

            Assert.Equal(new[] { "Multileader style…" }, model.MLeaderStyleItems.Select(i => i.Name));
            Assert.True(model.MLeaderStyleItems.Last().IsDialog);

            model.CurrentMLeaderStyle = MainViewModel.ManageMLeaderStyles;
            Assert.Equal("MLEADERSTYLE", asked);

            asked = null;
            model.Execute("MLS");
            Assert.Equal("MLEADERSTYLE", asked);
        }

        [Fact]
        public void ThePropertySettings_ShowTheSourcesValues_AndTheSourceAndThePromptAtTheFoot()
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            var wall = new DxfLine
            {
                StartPoint = new GeoPoint3(0, 0, 0),
                EndPoint = new GeoPoint3(100, 0, 0),
                Layer = "WALLS",
                LineWeight = DxfLineWeight.LineWeight025,
            };
            var other = new DxfLine { StartPoint = new GeoPoint3(0, 50, 0), EndPoint = new GeoPoint3(100, 50, 0) };
            database.ModelSpace.AppendEntity(wall);
            database.ModelSpace.AppendEntity(other);
            CadDocument document = CadDocument.From(database, "plan.dxf");
            var model = new MainViewModel();
            model.OpenDocument(document, "plan.dxf");
            MatchPropSettingsViewModel shown = null;
            model.ShowMatchPropSettings = dialog =>
            {
                shown = dialog;
                return false;
            };

            model.StartCommand("MATCHPROP");
            model.Session.FeedSelection(new[] { wall.Handle });
            model.Execute("S");

            Assert.NotNull(shown);
            Assert.Equal("ByLayer", shown.Basic.Single(b => b.Name == "Color").Value);
            Assert.Equal(0xFFFFFF, shown.Basic.Single(b => b.Name == "Color").SwatchRgb);
            Assert.Equal("WALLS", shown.Basic.Single(b => b.Name == "Layer").Value);
            Assert.Equal("ByLayer", shown.Basic.Single(b => b.Name == "Linetype").Value);
            Assert.Equal("0.25 mm", shown.Basic.Single(b => b.Name == "Lineweight").Value);
            Assert.Equal("0", shown.Basic.Single(b => b.Name == "Thickness").Value);
            Assert.False(shown.Basic.Single(b => b.Name == "Plot Style").IsEnabled);
            Assert.Null(shown.Basic.Single(b => b.Name == "Layer").SwatchRgb);
            Assert.Equal(
                "Source: Line on layer WALLS. The ticks are kept for the session (and the next MATCHPROP), as AutoCAD keeps them.",
                shown.SourceText
            );
            Assert.Equal("Select destination object(s) or [Settings]: S", shown.PromptText);
        }

        [Fact]
        public void TheDialogs_AreLaidOutAsTheDesignDrawsThem()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                MainViewModel model = Opened(out CadDocument document);
                MLeaderStyleManagerViewModel styles = model.CreateMLeaderStyleManager();
                var manager = new MLeaderStyleManagerWindow(styles);
                var modify = new MLeaderStyleEditWindow(styles.CreateModify());
                var settings = new MatchPropSettingsWindow(new MatchPropSettingsViewModel(CadMatchProperties.All));

                foreach (
                    (Window window, FrameworkElement body) in new (Window, FrameworkElement)[]
                    {
                        (manager, manager.Body),
                        (modify, modify.Body),
                        (settings, settings.Body),
                    }
                )
                {
                    Assert.Equal(
                        window is MLeaderStyleEditWindow ? 800
                            : window is MatchPropSettingsWindow ? 600
                            : 780,
                        window.Width
                    );
                    Assert.Equal(
                        window is MLeaderStyleEditWindow ? 640
                            : window is MatchPropSettingsWindow ? 480
                            : 540,
                        window.Height
                    );
                    object context = window.DataContext;
                    window.Content = null;
                    body.DataContext = context;
                    Layout(body, window.Width, window.Height);
                    window.Close();
                }

                List<Button> buttons = Descendants(manager.Body).OfType<Button>().ToList();
                Assert.Equal(
                    new[] { "Set Current", "New…", "Modify…", "Delete" },
                    buttons.Select(b => b.Content as string).Where(c => c != null && c != "Close")
                );
                Assert.Same(
                    manager.FindResource("StylePrimaryButton"),
                    buttons.Single(b => "Modify…".Equals(b.Content)).Style
                );
                Assert.False(buttons.Single(b => "Delete".Equals(b.Content)).IsEnabled);
                Assert.Contains(Descendants(manager.Body).OfType<TextBlock>(), t =>
                    t.Text == "Standard, the current style and a style in use cannot be deleted."
                );
                Assert.Contains(Descendants(modify.Body).OfType<RadioButton>(), r =>
                    "Leader Format".Equals(r.Content) && r.IsChecked == true
                );
                Assert.Contains(Descendants(modify.Body).OfType<TextBlock>(), t =>
                    t.Text == "The values are kept in the MLEADERSTYLE object of the drawing."
                );
                Assert.Contains(Descendants(settings.Body).OfType<TextBlock>(), t =>
                    t.Text == "Select destination object(s) or [Settings]: S"
                );
                Assert.Contains(Descendants(settings.Body).OfType<GroupBox>(), g =>
                    "Basic Properties".Equals(g.Header)
                );
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
    }
}
