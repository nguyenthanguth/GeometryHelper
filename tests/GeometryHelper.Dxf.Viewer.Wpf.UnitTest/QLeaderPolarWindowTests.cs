using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Polar Tracking tab of Drafting Settings past its increment (additional angles, New and Delete, how tracking runs
    /// and how angles are measured), the tip polar tracking shows by the cursor, and QLEADER's Leader Settings, each driven
    /// through its view model with no window shown.
    /// </summary>
    public sealed class QLeaderPolarWindowTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private MainViewModel Room()
        {
            var model = new MainViewModel();
            Assert.True(
                model.Open(_folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _)))
            );
            return model;
        }

        // ---- Drafting Settings, Polar Tracking

        [Fact]
        public void PolarTab_ListsAdditionalAngles_NewKeepsOnlyAnAngle_EscDropsIt_AndDeleteTakesTheOneChosen()
        {
            MainViewModel model = Room();
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.PolarTrackingTab);

            // POLARMODE 0: absolute, orthogonal tracking, no additional angles; New and Delete greyed.
            Assert.True(dialog.IsAbsolute);
            Assert.True(dialog.IsTrackingOrthogonally);
            Assert.False(dialog.IsAdditionalAngles);
            Assert.Empty(dialog.AdditionalAngles);
            Assert.False(dialog.CanAddAngle);
            Assert.False(dialog.CanDeleteAngle);

            dialog.IsAdditionalAngles = true;
            Assert.True(dialog.CanAddAngle);

            // "15a" is not an angle: the row stays open with the reason said.
            dialog.NewAngleRow();
            Assert.True(dialog.IsTypingAngle);
            Assert.False(dialog.CanAddAngle);
            dialog.NewAngle = "15a";
            Assert.False(dialog.KeepNewAngle());
            Assert.True(dialog.IsTypingAngle);
            Assert.Equal(
                "Not an angle. Type a number in the drawing’s angle units, for example 15 or 22.5.",
                dialog.AngleError
            );

            // OK while it stays refused takes nothing.
            Assert.False(dialog.Apply());
            Assert.Equal("An additional angle is not an angle.", dialog.Notice);

            dialog.NewAngle = "34";
            Assert.True(dialog.KeepNewAngle());
            Assert.False(dialog.IsTypingAngle);
            Assert.Equal(string.Empty, dialog.AngleError);

            foreach (string angle in new[] { "120", "350" })
            {
                dialog.NewAngleRow();
                dialog.NewAngle = angle;
                Assert.True(dialog.KeepNewAngle());
            }

            // Esc drops the row; Delete takes out the one chosen.
            dialog.NewAngleRow();
            dialog.NewAngle = "15";
            dialog.DropNewAngle();
            Assert.Equal(new[] { "34", "120", "350" }, dialog.AdditionalAngles);
            dialog.SelectedAdditionalAngle = "120";
            Assert.True(dialog.CanDeleteAngle);
            dialog.DeleteAngle();
            Assert.Equal(new[] { "34", "350" }, dialog.AdditionalAngles);
            Assert.Null(dialog.SelectedAdditionalAngle);
        }

        [Fact]
        public void PolarTab_Apply_SetsPolarmodeAndPolaraddang_KeepingBit8_AndTheSnapsFollow()
        {
            MainViewModel model = Room();
            model.Document.Database.Auprec = 0;
            model.Execute("POLARMODE");
            model.Execute("8");
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.PolarTrackingTab);
            dialog.IsAdditionalAngles = true;
            dialog.NewAngleRow();
            dialog.NewAngle = "34";
            dialog.KeepNewAngle();
            dialog.NewAngleRow();
            dialog.NewAngle = "350";
            dialog.KeepNewAngle();
            dialog.IsRelativeToLastSegment = true;
            dialog.IsTrackingAllPolar = true;

            Assert.True(dialog.Apply());

            Assert.Equal(15, model.Settings.PolarMode);
            Assert.Equal("34;350", model.Settings.PolarAddedAngles);
            Assert.True(model.Snap.PolarFromLastSegment);
            Assert.True(model.Snap.TracksPolarAngles);
            Assert.True(model.Snap.AcquiresWithShift);
            Assert.Equal(
                new[] { 34.0, 350.0 },
                model.Snap.AdditionalPolarAngles.Select(a => Math.Round(a * 180.0 / Math.PI, 9))
            );

            // Opened again, the tab reads them back; Absolute and Additional angles off write POLARMODE 10.
            DraftingSettingsViewModel again = model.CreateDraftingSettings(DraftingSettingsViewModel.PolarTrackingTab);
            Assert.True(again.IsRelativeToLastSegment);
            Assert.True(again.IsTrackingAllPolar);
            Assert.True(again.IsAdditionalAngles);
            Assert.Equal(new[] { "34", "350" }, again.AdditionalAngles);
            again.IsAbsolute = true;
            again.IsAdditionalAngles = false;
            Assert.True(again.Apply());
            Assert.Equal(10, model.Settings.PolarMode);
            Assert.Empty(model.Snap.AdditionalPolarAngles);
            Assert.False(model.Snap.PolarFromLastSegment);
        }

        [Fact]
        public void PolarTab_TakesTenAdditionalAnglesAtMost()
        {
            MainViewModel model = Room();
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.PolarTrackingTab);
            dialog.IsAdditionalAngles = true;

            for (int i = 1; i <= 10; i++)
            {
                Assert.True(dialog.CanAddAngle);
                dialog.NewAngleRow();
                dialog.NewAngle = (i * 7).ToString(System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(dialog.KeepNewAngle());
            }

            Assert.False(dialog.CanAddAngle);
            Assert.True(dialog.Apply());
            Assert.Equal(10, model.Settings.PolarAddedAngles.Split(';').Length);
        }

        // ---- The polar tip

        [Fact]
        public void PolarTip_GivesDistanceAndAngleInTheDrawingsUnits_AndRelativePolarFromTheLastSegment()
        {
            MainViewModel model = Room();
            DxfDatabase db = model.Document.Database;
            db.Lunits = 2;
            db.Luprec = 4;
            db.Aunits = 0;
            db.Auprec = 0;
            ObjectSnap snap = model.Snap;
            snap.PolarFromLastSegment = false;

            Assert.Equal(
                "Polar: 183.8478 < 45°",
                CadCanvas.PolarTipText(new GeoPoint2(10, 10), new GeoPoint2(140, 140), model.Session, snap)
            );

            // POLARMODE 1 with a last segment at 30 degrees: the cursor at 120 degrees is 90 from it.
            snap.PolarFromLastSegment = true;
            snap.LastSegment = new GeoVector2(Math.Cos(Math.PI / 6.0), Math.Sin(Math.PI / 6.0));
            GeoPoint2 target = new GeoPoint2(
                100.0 * Math.Cos(2.0 * Math.PI / 3.0),
                100.0 * Math.Sin(2.0 * Math.PI / 3.0)
            );
            Assert.Equal(
                "Relative Polar: 100.0000 < 90°",
                CadCanvas.PolarTipText(GeoPoint2.Origin, target, model.Session, snap)
            );

            // With no last segment the angle is absolute, and the tip says Polar.
            snap.LastSegment = null;
            Assert.Equal(
                "Polar: 100.0000 < 120°",
                CadCanvas.PolarTipText(GeoPoint2.Origin, target, model.Session, snap)
            );
        }

        [Fact]
        public void TheCanvasTip_WhilePolarHoldsTheCursor_IsThePolarTip()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    MainViewModel model = Room();
                    model.Document.Database.Aunits = 0;
                    model.Document.Database.Auprec = 0;
                    model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
                    model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                    model.Toggles.Single(t => t.Name == "ORTHO").IsOn = false;
                    model.Toggles.Single(t => t.Name == "POLAR").IsOn = true;
                    model.Execute("POLARANG");
                    model.Execute("45");
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                    };
                    canvas.Measure(new Size(800, 600));
                    canvas.Arrange(new Rect(0, 0, 800, 600));
                    canvas.RenderNow();
                    model.StartCommand("LINE");
                    CanvasMouseTests.Click(canvas, CanvasMouseTests.At(canvas, 500, 500));
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, CanvasMouseTests.At(canvas, 1500, 1510));

                    Assert.StartsWith("Polar: ", canvas.PromptTip);
                    Assert.EndsWith(" < 45°", canvas.PromptTip);
                    canvas.Dispose();
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

        // ---- Leader Settings

        [Fact]
        public void LeaderSettings_GreysWhatAutoCadGreys_RefusesAMaximumBelowTwo_AndOkWritesTheDrawing()
        {
            MainViewModel model = Room();
            LeaderSettingsViewModel shown = null;
            model.ShowLeaderSettings = dialog =>
            {
                shown = dialog;
                Assert.True(dialog.IsAnnotationTab);
                Assert.True(dialog.IsMText);
                Assert.False(dialog.IsTolerance);
                Assert.True(dialog.PromptForWidth);
                Assert.True(dialog.IsReuseNone);
                Assert.Equal("Closed filled", dialog.Arrow);
                Assert.Equal("Any angle", dialog.FirstSegment);
                Assert.Equal("3", dialog.MaxPoints);
                Assert.True(dialog.AttachmentRows[1].IsRight);
                Assert.True(dialog.AttachmentRows[3].IsLeft);

                // Another type than MText greys the Attachment tab.
                dialog.IsNoAnnotation = true;
                dialog.Tab = LeaderSettingsViewModel.AttachmentTab;
                Assert.False(dialog.IsAttachmentTab);
                dialog.IsMText = true;
                dialog.Tab = LeaderSettingsViewModel.AttachmentTab;
                Assert.True(dialog.IsAttachmentTab);

                dialog.AttachmentRows[0].IsRight = true;
                Assert.False(dialog.AttachmentRows[1].IsRight);
                Assert.Equal(CadQLeaderAttachment.TopOfTopLine, dialog.RightSide);
                dialog.Underline = true;
                Assert.False(dialog.AreRowsEnabled);

                dialog.NoLimit = true;
                Assert.False(dialog.IsMaximumEnabled);
                dialog.NoLimit = false;
                dialog.MaxPoints = "1";
                Assert.False(dialog.Ok());
                Assert.Equal("The maximum number of points must be a whole number, 2 at least.", dialog.Notice);
                dialog.FewerPoints();
                Assert.Equal("2", dialog.MaxPoints);
                dialog.MorePoints();
                dialog.MorePoints();
                Assert.Equal("4", dialog.MaxPoints);

                dialog.IsSpline = true;
                dialog.Arrow = "Dot";
                dialog.SecondSegment = "45°";
                dialog.FrameText = true;
                Assert.True(dialog.Ok());
                return true;
            };

            model.StartCommand("QLEADER");
            model.Execute("S");

            Assert.NotNull(shown);
            Assert.Equal("Specify first leader point, or [Settings] <Settings>:", model.Prompt);
            model.Escape();
            CadQLeaderSettings read = CadQLeaderSettings.Read(model.Document.Database);
            Assert.Equal(CadQLeaderAttachment.TopOfTopLine, read.RightSide);
            Assert.True(read.Underline);
            Assert.Equal(4, read.MaxPoints);
            Assert.True(read.Spline);
            Assert.True(read.FrameText);
            Assert.Equal(CadQLeaderAngle.FortyFive, read.SecondSegment);
            Assert.Equal(2, read.Tab);
            Assert.Equal("_Dot", model.Document.Database.Dimldrblk?.Name);

            // Cancel writes nothing.
            model.ShowLeaderSettings = dialog =>
            {
                dialog.IsNoAnnotation = true;
                dialog.Cancel();
                return true;
            };
            model.StartCommand("QLEADER");
            model.Execute("S");
            model.Escape();
            Assert.Equal(CadQLeaderAnnotation.MText, CadQLeaderSettings.Read(model.Document.Database).Annotation);
        }

        // ---- The windows

        [Fact]
        public void LeaderSettings_IsMadeOverItsViewModelOnEachTab_AsTheScreenshotModeDrawsIt()
        {
            OnWindows(model =>
            {
                var windows = new List<Window>();

                // One dialog for each tab, as the screenshot mode makes one.
                foreach (string tab in new[] { "Annotation", "Leader Line & Arrow", "Attachment" })
                {
                    LeaderSettingsViewModel shown = null;
                    model.ShowLeaderSettings = d =>
                    {
                        shown = d;
                        return true;
                    };
                    model.StartCommand("QLEADER");
                    model.Execute("S");
                    model.Escape();
                    shown.Tab = tab;
                    windows.Add(new LeaderSettingsWindow(shown));
                }

                return windows;
            });
        }

        [Fact]
        public void ThePolarTab_IsMadeOverItsViewModel_ANewAngleBeingTyped()
        {
            OnWindows(model =>
            {
                DraftingSettingsViewModel polar = model.CreateDraftingSettings(
                    DraftingSettingsViewModel.PolarTrackingTab
                );
                polar.IsAdditionalAngles = true;
                polar.NewAngleRow();
                return new List<Window> { new DraftingSettingsWindow(polar) };
            });
        }

        /// <summary>Makes windows on an STA thread over the room, each one's content measured and laid out as a screenshot draws it.</summary>
        private void OnWindows(Func<MainViewModel, List<Window>> make)
        {
            string path = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var model = new MainViewModel();
                    Assert.True(model.Open(path));

                    foreach (Window window in make(model))
                    {
                        var body = (FrameworkElement)window.Content;
                        window.Content = null;
                        body.DataContext = window.DataContext;
                        body.Measure(new Size(window.Width, window.Height));
                        body.Arrange(new Rect(0, 0, window.Width, window.Height));
                        body.UpdateLayout();
                        Assert.True(body.ActualWidth > 0, window.Title);
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
}
