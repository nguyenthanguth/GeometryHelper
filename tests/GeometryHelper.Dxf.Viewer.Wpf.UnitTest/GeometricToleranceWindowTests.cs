using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Geometric Tolerance dialog of TOLERANCE, LEADER and QLEADER, its Symbol and Material Condition dialogs, Tolerance
    /// in Leader Settings, and Select Custom Arrow Block, to boards GeomTolerance, GdtSymbol, GdtMaterial, QLeaderSettings and
    /// SelectArrowBlock (v124). Driven through the view models; the windows laid out with none shown.
    /// </summary>
    public sealed class GeometricToleranceWindowTests : IDisposable
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

        [Fact]
        public void Tolerance_OpensTheDialogEmpty_AndOkPutsTheFrameWherePicked()
        {
            MainViewModel model = Room();
            GeometricToleranceViewModel shown = null;
            model.ShowGeometricTolerance = dialog =>
            {
                shown = dialog;

                // Every box empty when it opens, as AutoCAD 2021 opens it.
                Assert.All(dialog.Rows, r => Assert.Null(r.Symbol));
                Assert.Equal(string.Empty, dialog.Rows[0].Tolerance1.Value);
                Assert.False(dialog.ProjectedZone);

                // Board GeomToleranceFilled: the position, diameter 0.1 at MMC, datums A, B and C.
                ToleranceRowViewModel row = dialog.Rows[0];
                row.Symbol = 'j';
                Assert.Equal("Position", row.SymbolGlyph.Name);
                row.Tolerance1.Diameter = true;
                row.Tolerance1.Value = "0.1";
                row.Tolerance1.Material = CadMaterialCondition.Maximum;
                Assert.Equal("M", row.Tolerance1.MaterialGlyph.Letter);
                row.Datums[0].Letter = "A";
                row.Datums[1].Letter = "B";
                row.Datums[2].Letter = "C";
                dialog.Ok();
                return true;
            };

            model.Execute("TOLERANCE");
            Assert.NotNull(shown);
            model.Execute("10,20");

            DxfTolerance frame = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfTolerance>());
            Assert.Equal("{\\Fgdt;j}%%v{\\Fgdt;n}0.1{\\Fgdt;m}%%v%%vA%%vB%%vC", frame.Text);
            Assert.True(frame.Location.IsEqualTo(new GeometryHelper.Geometry.GeoPoint3(10, 20, 0), Tolerance.Default));
        }

        [Fact]
        public void Cancel_MakesNoFrame()
        {
            MainViewModel model = Room();
            model.ShowGeometricTolerance = dialog =>
            {
                dialog.Rows[0].Symbol = 'c';
                dialog.Cancel();
                return true;
            };

            model.Execute("TOLERANCE");
            Assert.Empty(model.Document.ModelSpace.Block.OfType<DxfTolerance>());
        }

        [Fact]
        public void ThePickers_OfferAutoCadsSymbolsAndConditions_InItsOrder()
        {
            Assert.Equal(
                new[]
                {
                    "Position",
                    "Concentricity",
                    "Symmetry",
                    "Parallelism",
                    "Perpendicularity",
                    "Angularity",
                    "Cylindricity",
                    "Flatness",
                    "Circularity",
                    "Straightness",
                    "Profile of a surface",
                    "Profile of a line",
                    "Circular runout",
                    "Total runout",
                    "Blank",
                },
                GeometricToleranceViewModel.Symbols.Select(s => s.Name)
            );
            Assert.Equal(
                new char?[] { 'm', 'l', 's', null },
                GeometricToleranceViewModel.Materials.Select(GeometricToleranceViewModel.CodeOf)
            );
        }

        [Fact]
        public void LeaderSettings_TakesTolerance_AndOkWritesIt()
        {
            MainViewModel model = Room();
            model.ShowLeaderSettings = dialog =>
            {
                dialog.IsTolerance = true;
                Assert.True(dialog.IsTolerance);
                Assert.False(dialog.IsMText);

                // The Attachment tab greyed, as with any type other than MText.
                dialog.Tab = LeaderSettingsViewModel.AttachmentTab;
                Assert.False(dialog.IsAttachmentTab);
                Assert.True(dialog.Ok());
                return true;
            };

            model.StartCommand("QLEADER");
            model.Execute("S");
            model.Escape();
            Assert.Equal(CadQLeaderAnnotation.Tolerance, CadQLeaderSettings.Read(model.Document.Database).Annotation);
        }

        [Fact]
        public void TheDialogs_AreLaidOut_AtTheirBoardsSizes()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    MainViewModel model = Room();
                    GeometricToleranceViewModel tolerance = null;
                    model.ShowGeometricTolerance = d =>
                    {
                        tolerance = d;
                        return true;
                    };
                    model.Execute("TOLERANCE");
                    tolerance.Rows[0].Symbol = 'j';
                    tolerance.ProjectedZone = true;

                    LeaderSettingsViewModel leader = null;
                    model.ShowLeaderSettings = d =>
                    {
                        leader = d;
                        return true;
                    };
                    model.StartCommand("QLEADER");
                    model.Execute("S");
                    model.Escape();
                    leader.Arrow = LeaderSettingsViewModel.UserArrowEntry;

                    foreach (
                        Window window in new Window[]
                        {
                            new GeometricToleranceWindow(tolerance),
                            new GdtSymbolWindow('j'),
                            new GdtMaterialWindow(CadMaterialCondition.Least),
                            new SelectArrowBlockWindow(leader),
                        }
                    )
                    {
                        var body = (FrameworkElement)window.Content;
                        window.Content = null;
                        body.DataContext = window.DataContext;
                        body.Measure(new Size(window.Width, window.Height));
                        body.Arrange(new Rect(0, 0, window.Width, window.Height));
                        body.UpdateLayout();
                        Assert.True(body.ActualWidth > 0);
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
