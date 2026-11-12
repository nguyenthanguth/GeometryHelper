using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The words of the presenter's questions held to what AutoCAD 2021 says (its task dialogs, found in AcTaskDialogs and
    /// acadres, and its Edit Drawing Scales in AcScaleList), and Edit Drawing Scales' Reset with its three default lists and
    /// its Delete over several scales chosen at once.
    /// </summary>
    public sealed class QuestionTextsTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private MainViewModel Opened()
        {
            var db = new DxfDatabase("AC1027");
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            var model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("plan-" + Guid.NewGuid().ToString("N") + ".dxf", db)));
            model.Execute("-SCALELISTEDIT");
            model.Execute("R");
            model.Execute("Y");
            model.Execute("E");
            return model;
        }

        private static ScaleRowViewModel Row(ScaleListEditViewModel dialog, string name) =>
            dialog.Scales.Single(s => s.Name == name);

        [Fact]
        public void TheQuestionsOfThePresenter_SayWhatAutoCAD2021Says()
        {
            // Purge - Confirm Purge (TaskDialog 164 of AcTaskDialogs).
            Assert.Equal("Purge - Confirm Purge", CadQuestionTexts.PurgeTitle);
            Assert.Equal("Purge this item", CadQuestionTexts.PurgeThisItem);
            Assert.Equal("Skip this item", CadQuestionTexts.SkipThisItem);
            Assert.Equal(
                "You are about to purge layer TMP. What do you want to do?",
                CadQuestionTexts.PurgeQuestion("layer", "TMP")
            );

            // Layer - Overwrite Layer State (TaskDialog 188).
            Assert.Equal("Layer - Overwrite Layer State", CadQuestionTexts.OverwriteLayerStateTitle);
            Assert.Equal(
                "Do you want to overwrite Plan with the drawing's current layer state?",
                CadQuestionTexts.OverwriteLayerState("Plan")
            );

            // Block Attribute Manager Alert (BattmanRes 60 and 18).
            Assert.Equal("Block Attribute Manager Alert", CadQuestionTexts.AttributeChangesTitle);
            Assert.Equal(
                "The attributes in the current block have been changed.",
                CadQuestionTexts.AttributeChangesLine
            );
            Assert.Equal("Do you want to save these changes?", CadQuestionTexts.AttributeChangesQuestion);

            // Block - Redefine Block (TaskDialog 128).
            Assert.Equal("Block - Redefine Block", CadQuestionTexts.RedefineBlockTitle);
            Assert.Equal(
                "\"GB\" is already defined as a block in this drawing, would you like to redefine this block reference?",
                CadQuestionTexts.RedefineBlockQuestion("GB")
            );
            Assert.Equal("Redefine block", CadQuestionTexts.RedefineBlock);
            Assert.Equal("Don't redefine \"GB\"", CadQuestionTexts.KeepBlock("GB"));
            Assert.Equal("All instances of this block will be updated.", CadQuestionTexts.RedefineBlockDetail);
            Assert.Equal("No changes will be made to the block or drawing.", CadQuestionTexts.KeepBlockDetail);

            // %ObjectName% - Already Exists (TaskDialog S8) for a view.
            Assert.Equal("View - Already Exists", CadQuestionTexts.ViewExistsTitle);
            Assert.Equal(
                "Stair core already exists.  Do you want to replace it?",
                CadQuestionTexts.ViewExists("Stair core")
            );
            Assert.Equal("Replace", CadQuestionTexts.ReplaceView);

            // Layer - Rename (TaskDialog 88).
            Assert.Equal("Layer - Rename", CadQuestionTexts.RenameRefusedTitle);
            Assert.Equal("The selected layer cannot be renamed.", CadQuestionTexts.RenameRefused);
            Assert.Equal(
                "The following layers cannot be renamed:\n Layer 0\n Xref-dependent layers",
                CadQuestionTexts.RenameRefusedDetail
            );

            // The messages of AutoCAD's own boxes: WBLOCK (acblockRes 66) and the Text Style dialog (acadres 9019).
            Assert.Equal(
                "C:\\blocks\\gb.dwg already exists.  Do you want to replace it?",
                CadQuestionTexts.FileExists("C:\\blocks\\gb.dwg")
            );
            Assert.Equal(
                "The current style has been modified.\nDo you want to save your changes?",
                CadQuestionTexts.TextStyleModified
            );

            // Edit Drawing Scales: the strings of AcScaleList (IDS_DEL_REFSCALE_MSG, deleteLast, statusVaries).
            Assert.Equal(
                "Scales in use cannot be deleted. \r\nDelete selected scales not in use?",
                ScaleListEditViewModel.DeleteInUseQuestion
            );
            Assert.Equal("The scale list must contain at least one scale.", ScaleListEditViewModel.DeleteLastMessage);
            Assert.Equal("Varies: multiple scales selected.", ScaleListEditViewModel.Varies);
        }

        [Fact]
        public void ResetOffersTheMetricTheImperialAndTheBothLists_AndRestoresTheOneChosen()
        {
            MainViewModel model = Opened();
            var checkedLists = new List<string>();

            model.ShowScaleList = dialog =>
            {
                // Metric and Imperial: the 17 metric scales, then the 16 imperial ones that are not 1:1.
                dialog.ConfirmReset = () => ScaleListResetChoice.MetricAndImperial;
                Assert.True(dialog.Reset());
                Assert.Equal(33, dialog.Scales.Count);
                Assert.Equal("1:1", dialog.Scales[0].Name);
                Assert.Equal("100:1", dialog.Scales[16].Name);
                Assert.Equal("1/128\" = 1'-0\"", dialog.Scales[17].Name);
                Assert.Equal("1'-0\" = 1'-0\"", dialog.Scales[32].Name);
                Assert.Single(dialog.Scales, s => s.Name == "1:1");
                checkedLists.Add("both");

                // Imperial: 1:1 is kept, the metric scales no object points at go, the imperial ones come.
                dialog.ConfirmReset = () => ScaleListResetChoice.Imperial;
                Assert.True(dialog.Reset());
                Assert.Equal(17, dialog.Scales.Count);
                Assert.Equal("1:1", dialog.Scales[0].Name);
                Assert.Equal("1/128\" = 1'-0\"", dialog.Scales[1].Name);
                Assert.DoesNotContain(dialog.Scales, s => s.Name == "1:50");
                checkedLists.Add("imperial");

                // Metric brings the metric ones back whatever the drawing's own default list is.
                dialog.ConfirmReset = () => ScaleListResetChoice.Metric;
                Assert.True(dialog.Reset());
                Assert.Equal(17, dialog.Scales.Count);
                Assert.Contains(dialog.Scales, s => s.Name == "1:50");
                Assert.DoesNotContain(dialog.Scales, s => s.Name.IndexOf('\'') >= 0);
                checkedLists.Add("metric");

                // Cancel leaves the list as it is.
                dialog.ConfirmReset = () => ScaleListResetChoice.Cancel;
                Assert.False(dialog.Reset());
                Assert.Equal(17, dialog.Scales.Count);
                dialog.Cancel();
                return true;
            };
            model.Execute("SCALELISTEDIT");

            Assert.Equal(new[] { "both", "imperial", "metric" }, checkedLists);
        }

        [Fact]
        public void SeveralScalesChosen_AreDeletedTogether_AndTheNextOneStandsWhereTheFirstWas()
        {
            MainViewModel model = Opened();

            model.ShowScaleList = dialog =>
            {
                dialog.Choose(new[] { Row(dialog, "1:2"), Row(dialog, "1:5"), Row(dialog, "1:4") });

                // In the order of the list, not the order given; several: no Edit, no Move, the units say they vary.
                Assert.Equal(new[] { "1:2", "1:4", "1:5" }, dialog.Chosen.Select(s => s.Name));
                Assert.False(dialog.CanEdit);
                Assert.False(dialog.CanMoveUp);
                Assert.False(dialog.CanMoveDown);
                Assert.True(dialog.CanDelete);
                Assert.Equal("Varies: multiple scales selected.", dialog.Sentence);
                Assert.Equal(string.Empty, dialog.PaperUnits);
                Assert.False(dialog.Edit());

                Assert.True(dialog.Delete());
                Assert.Equal(14, dialog.Scales.Count);
                Assert.DoesNotContain(dialog.Scales, s => s.Name == "1:2" || s.Name == "1:4" || s.Name == "1:5");
                Assert.Equal("1:8", dialog.Selected.Name);
                Assert.Equal(new[] { "1:8" }, dialog.Chosen.Select(s => s.Name));
                Assert.Equal("14 scales", dialog.Summary);

                // One scale chosen again: Edit and Move are live.
                dialog.Choose(new[] { Row(dialog, "1:10") });
                Assert.True(dialog.CanEdit);
                Assert.True(dialog.CanMoveUp);
                dialog.Cancel();
                return true;
            };
            model.Execute("SCALELISTEDIT");
        }

        [Fact]
        public void AChoiceThatHoldsAScaleInUse_AsksFirst_AndOnYesDeletesTheOthersOnly()
        {
            MainViewModel model = Opened();
            int asked = 0;

            model.ShowScaleList = dialog =>
            {
                // 1:1 is the unit scale: alone, Delete is greyed and asks nothing.
                dialog.ConfirmDeleteInUse = () =>
                {
                    asked++;
                    return false;
                };
                dialog.Choose(new[] { Row(dialog, "1:1") });
                Assert.False(dialog.CanDelete);
                Assert.False(dialog.Delete());
                Assert.Equal(0, asked);

                // With 1:10 and 1:20 chosen too, Delete is live; No takes nothing out.
                dialog.Choose(new[] { Row(dialog, "1:1"), Row(dialog, "1:10"), Row(dialog, "1:20") });
                Assert.True(dialog.CanDelete);
                Assert.False(dialog.Delete());
                Assert.Equal(1, asked);
                Assert.Equal(17, dialog.Scales.Count);

                // Yes takes out 1:10 and 1:20 and keeps 1:1, which stands where the first of the choice stood.
                dialog.ConfirmDeleteInUse = () =>
                {
                    asked++;
                    return true;
                };
                Assert.True(dialog.Delete());
                Assert.Equal(2, asked);
                Assert.Equal(15, dialog.Scales.Count);
                Assert.Equal("1:1", dialog.Scales[0].Name);
                Assert.Equal("1:1", dialog.Selected.Name);
                dialog.Cancel();
                return true;
            };
            model.Execute("SCALELISTEDIT");
        }

        [Fact]
        public void DeletingEveryScale_SaysTheListMustContainOne_AndTakesNothingOut()
        {
            MainViewModel model = Opened();
            var said = new List<string>();

            model.ShowScaleList = dialog =>
            {
                // A list with the unit scale left out of the dialog: no scale is one the drawing keeps.
                dialog.Scales.RemoveAt(0);
                dialog.ShowMessage = said.Add;
                dialog.ConfirmDeleteInUse = () => throw new InvalidOperationException("nothing is in use");
                dialog.Choose(dialog.Scales.ToList());
                Assert.True(dialog.CanDelete);

                Assert.False(dialog.Delete());
                Assert.Equal(new[] { "The scale list must contain at least one scale." }, said);
                Assert.Equal(16, dialog.Scales.Count);

                // Every scale but one: that is allowed.
                dialog.Choose(dialog.Scales.Skip(1).ToList());
                Assert.True(dialog.Delete());
                Assert.Single(dialog.Scales);
                dialog.Cancel();
                return true;
            };
            model.Execute("SCALELISTEDIT");
        }

        [Fact]
        public void TheListOfTheWindow_ChoosesSeveralScales_AndTheResetQuestionHasFourButtons()
        {
            MainViewModel model = Opened();
            ScaleListEditViewModel shown = null;
            model.ShowScaleList = dialog =>
            {
                shown = dialog;
                dialog.Cancel();
                return true;
            };
            model.Execute("SCALELISTEDIT");
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var window = new ScaleListEditWindow(shown);
                    var body = (System.Windows.FrameworkElement)window.Content;
                    body.Measure(new System.Windows.Size(560, 500));
                    body.Arrange(new System.Windows.Rect(0, 0, 560, 500));
                    body.UpdateLayout();
                    var list = (ListBox)window.FindName("List");
                    Assert.Equal(SelectionMode.Extended, list.SelectionMode);
                    list.SelectedItems.Clear();
                    list.SelectedItems.Add(shown.Scales[2]);
                    list.SelectedItems.Add(shown.Scales[4]);
                    Assert.Equal(new[] { "1:4", "1:8" }, shown.Chosen.Select(s => s.Name));
                    Assert.True(shown.CanDelete);
                    Assert.False(shown.CanEdit);
                    window.Close();

                    CadQuestionWindow reset = CadQuestionWindow.ForChoice(
                        ScaleListEditViewModel.ResetTitle,
                        ScaleListEditViewModel.ResetQuestion,
                        ScaleListEditViewModel.ResetDetail,
                        ScaleListEditViewModel.ResetMetric,
                        ScaleListEditViewModel.ResetImperial,
                        ScaleListEditViewModel.ResetMetricAndImperial,
                        "Cancel"
                    );
                    var buttons = ((System.Windows.Controls.Panel)reset.FindName("Buttons"))
                        .Children.OfType<Button>()
                        .Select(b => (string)b.Content)
                        .ToArray();
                    Assert.Equal(
                        new[] { "Metric scales", "Imperial scales", "Metric and Imperial scales", "Cancel" },
                        buttons
                    );
                    reset.Close();
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
