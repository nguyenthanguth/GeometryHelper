using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The object items of the canvas menus the presenter now has commands for, enabled and wired as AutoCAD 2021's menu file
    /// wires them: Remove Style Overrides of a dimension runs DIMOVERRIDE's Clear on the selection, the spline menu runs
    /// SPLINEDIT's options, Viewport Clip VPCLIP and the image menu's Adjust IMAGEADJUST; and the Recent Input of the Command
    /// menu in the drawing's units, with the selection sets answered at Select objects.
    /// </summary>
    public sealed class CanvasMenuFollowUps2Tests
    {
        private static MainViewModel Open(DxfDatabase database)
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "menus2.dxf"));
            return model;
        }

        private static CadMenuItemViewModel Item(IEnumerable<CadMenuItemViewModel> items, string header) =>
            items.Single(i => i.Header == header);

        private static string[] Headers(IEnumerable<CadMenuItemViewModel> items) =>
            items.Select(i => i.Header).ToArray();

        private static GeoPoint3 P3(double x, double y) => new GeoPoint3(x, y, 0);

        private static DxfSpline FitSpline(DxfDatabase database)
        {
            var spline = new DxfSpline();
            database.ModelSpace.AppendEntity(spline);
            spline.FitData = new DxfFitData(new[] { P3(0, 0), P3(10, 10), P3(20, 0), P3(30, 10), P3(40, 0) }, 3, 0.0);
            return spline;
        }

        [Fact]
        public void RemoveStyleOverridesOfADimension_RunsDimoverridesClearOnTheSelection()
        {
            var database = new DxfDatabase("AC1027");
            MainViewModel model = Open(database);
            model.Execute("DIMLINEAR");
            model.Execute("0,0");
            model.Execute("1000,0");
            model.Execute("500,-300");
            DxfDimension dimension = model.Document.ModelSpace.Block.OfType<DxfDimension>().Single();
            model.Document.Selection.Set(new[] { dimension.Handle });
            int style = model.Document.Database.DimStyleTable[dimension.DimensionStyleName].Dimdec;
            Item(Item(model.CanvasMenu(), "Precision").Items, "0.00").Command.Execute(null);
            Assert.Equal(2, ((DxfDimension)model.Document.FindEntity(dimension.Handle)).Dimdec);

            CadMenuItemViewModel remove = Item(model.CanvasMenu(), "Remove Style Overrides");
            Assert.True(remove.IsEnabled);
            remove.Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            Assert.Equal(style, ((DxfDimension)model.Document.FindEntity(dimension.Handle)).Dimdec);
        }

        [Fact]
        public void ASplineSelected_OffersItsMenuWithTheItemsSplineeditBuilds()
        {
            var database = new DxfDatabase("AC1027");
            DxfSpline spline = FitSpline(database);
            MainViewModel model = Open(database);
            model.Document.Selection.Set(new[] { spline.Handle });

            IReadOnlyList<CadMenuItemViewModel> items = Item(model.CanvasMenu(), "Spline").Items;

            Assert.All(items.Where(i => !i.IsSeparator && i.Header != "3D Edit Bar"), item =>
                Assert.True(item.IsEnabled, item.Header)
            );
            Assert.False(Item(items, "3D Edit Bar").IsEnabled);

            // Convert to Polyline answers the precision with its default and ends: the spline is replaced.
            Item(items, "Convert to Polyline").Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            Assert.DoesNotContain(model.Document.ModelSpace.Block, e => e is DxfSpline && !e.IsErased);
            Assert.Single(model.Document.ModelSpace.Block.OfType<DxfPolyline>(), p => !p.IsErased);
        }

        [Fact]
        public void ASplineMenusReverseDirectionAndDisplayControlVertices_TurnTheSplineRoundAndPurgeItsFitData()
        {
            var database = new DxfDatabase("AC1027");
            DxfSpline spline = FitSpline(database);
            MainViewModel model = Open(database);
            model.Document.Selection.Set(new[] { spline.Handle });
            GeoPoint3 start = spline.StartPoint;

            Item(Item(model.CanvasMenu(), "Spline").Items, "Reverse Direction").Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            var turned = (DxfSpline)model.Document.FindEntity(spline.Handle);
            Assert.True(turned.EndPoint.IsEqualTo(start, new Tolerance(1E-6, 1E-6)));

            Item(Item(model.CanvasMenu(), "Spline").Items, "Display Control Vertices").Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            Assert.False(((DxfSpline)model.Document.FindEntity(spline.Handle)).HasFitData);
        }

        [Fact]
        public void ASplineMenusAddFitPoint_StartsSplineeditAtItsFitDataAdd()
        {
            var database = new DxfDatabase("AC1027");
            DxfSpline spline = FitSpline(database);
            MainViewModel model = Open(database);
            model.Document.Selection.Set(new[] { spline.Handle });

            Item(Item(model.CanvasMenu(), "Spline").Items, "Add Fit Point").Command.Execute(null);

            Assert.True(model.IsCommandRunning);
            Assert.Equal("Specify existing fit point on spline <exit>:", model.Session.PromptText);
        }

        [Fact]
        public void AViewportMenusViewportClip_IsEnabled_WhereTheDrawingHasVpclip_AndMaximizeViewportStaysGrey()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.OpenDocument(
                    CadDocument.From(TestFiles.RoomWithViewport(folder, out _, out string viewport), "sheet.dxf")
                );
                model.Document.CurrentSpace = model.Document.Spaces.First(s => !s.IsModel);
                model.Document.Selection.Set(new[] { viewport });

                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

                Assert.True(Item(items, "Viewport Clip").IsEnabled);
                Assert.False(Item(items, "Maximize Viewport").IsEnabled);

                Item(items, "Viewport Clip").Command.Execute(null);

                Assert.True(model.IsCommandRunning);
                Assert.Equal("VPCLIP", model.Session.Current.Name);
            }
        }

        [Fact]
        public void AnImageMenusAdjust_RunsImageadjustOnTheImageSelected()
        {
            var database = new DxfDatabase("AC1027");
            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(database);
            var definition = new DxfRasterImageDef("logo.png", new GeoVector2(40, 30));
            images.SetAt("logo", definition);
            var image = new DxfRasterImage(definition);
            image.SetOrientation(P3(0, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 30, 0));
            database.ModelSpace.AppendEntity(image);
            MainViewModel model = Open(database);
            model.ShowImageAdjust = dialog =>
            {
                dialog.Brightness = 60;
                return true;
            };
            model.Document.Selection.Set(new[] { image.Handle });

            CadMenuItemViewModel adjust = Item(Item(model.CanvasMenu(), "Image").Items, "Adjust");
            Assert.True(adjust.IsEnabled);
            adjust.Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            Assert.Equal(
                (byte)60,
                model.Document.ModelSpace.Block.OfType<DxfRasterImage>().Single(i => !i.IsErased).Brightness
            );
        }

        [Fact]
        public void TheCommandMenusRecentInput_ShowsThePointsAndNumbersInTheDrawingsUnits()
        {
            var database = new DxfDatabase("AC1027");
            MainViewModel model = Open(database);
            model.StartCommand("LINE");
            model.Execute("0,0");
            model.Execute("1000,0");
            model.Execute("@0,50.5");

            CadMenuItemViewModel recent = Item(model.CanvasMenu(), "Recent Input");

            Assert.Equal(new[] { "@0.0000,50.5000", "1000.0000,0.0000", "0.0000,0.0000" }, Headers(recent.Items));

            Item(recent.Items, "1000.0000,0.0000").Command.Execute(null);

            Assert.True(model.IsCommandRunning);
            Assert.Contains("Specify next point or [Close/Undo]: 1000.0000,0.0000", model.History);
        }

        [Fact]
        public void TheCommandMenusRecentInput_AtSelectObjects_OffersTheSelectionSetsAsAutoCadNamesThem()
        {
            var database = new DxfDatabase("AC1027");
            var first = new DxfLine(P3(0, 0), P3(100, 0));
            var second = new DxfLine(P3(0, 50), P3(100, 50));
            var third = new DxfLine(P3(0, 100), P3(100, 100));
            database.ModelSpace.AppendEntity(first);
            database.ModelSpace.AppendEntity(second);
            database.ModelSpace.AppendEntity(third);
            MainViewModel model = Open(database);

            // With 4 the Command menu shows at Select objects, which offers no options of its own.
            model.Settings.ShortcutMenu = 15;
            model.StartCommand("MOVE");
            model.Session.FeedSelection(new[] { first.Handle, second.Handle });
            model.Session.Enter();
            model.Escape();
            model.StartCommand("MOVE");

            CadMenuItemViewModel recent = Item(model.CanvasMenu(), "Recent Input");

            Assert.Equal(new[] { "Selection set (2 items)" }, Headers(recent.Items));
            Item(recent.Items, "Selection set (2 items)").Command.Execute(null);
            model.Session.Enter();

            Assert.True(model.IsCommandRunning);
            Assert.StartsWith("Specify base point", model.Session.PromptText, StringComparison.Ordinal);
        }
    }
}
