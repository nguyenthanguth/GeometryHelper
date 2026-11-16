using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The object snap override of the window ahead of a corner of a window or a point of a fence, typed or clicked, and the
    /// word the command line writes after it: "to" for PERpendicular, NEArest, TANgent and PARallel ("_per to"), "of" after the
    /// others ("_end of"), as AutoCAD 2021 writes them.
    /// </summary>
    public sealed class TypedSnapCornerInterfaceTests
    {
        /// <summary>A line from (0, 0) to (100, 0) and a short one from (70, 1) to (80, 1), the grid snap off.</summary>
        private static MainViewModel Open()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(70, 1, 0), new GeoPoint3(80, 1, 0)));
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "corners.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
            model.Session.SnapAperture = 1.3;
            return model;
        }

        [Theory]
        [InlineData(CadSnapModes.Perpendicular, "per", "to")]
        [InlineData(CadSnapModes.Nearest, "nea", "to")]
        [InlineData(CadSnapModes.Tangent, "tan", "to")]
        [InlineData(CadSnapModes.Parallel, "par", "to")]
        [InlineData(CadSnapModes.Endpoint, "end", "of")]
        [InlineData(CadSnapModes.Midpoint, "mid", "of")]
        [InlineData(CadSnapModes.Center, "cen", "of")]
        [InlineData(CadSnapModes.Quadrant, "qua", "of")]
        [InlineData(CadSnapModes.Node, "nod", "of")]
        [InlineData(CadSnapModes.Insertion, "ins", "of")]
        [InlineData(CadSnapModes.Intersection, "int", "of")]
        [InlineData(CadSnapModes.Extension, "ext", "of")]
        [InlineData(CadSnapModes.ApparentIntersection, "app", "of")]
        [InlineData(CadSnapModes.GeometricCenter, "gce", "of")]
        public void TheOverrideIsWrittenWithToOrOf_AsAutoCadWritesIt(CadSnapModes mode, string keyword, string word)
        {
            MainViewModel model = Open();
            model.StartCommand("LINE");

            Assert.True(model.OverrideSnap(mode));

            Assert.Equal("Specify first point: _" + keyword + " " + word, model.History.Last());
        }

        [Fact]
        public void EndTypedAtTheCornerOfAWindow_SnapsTheCorner()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");
            model.Execute("W");

            model.Execute("END");
            model.Execute("60,1");

            Assert.Equal(100.0, model.Session.LastPoint.X, 9);
            Assert.StartsWith("Specify opposite corner", model.Prompt);
        }

        [Fact]
        public void EndTypedAtTheCornerOfAWindow_WithNothingNear_IsAnInvalidWindowSpecification()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");
            model.Execute("W");

            model.Execute("END");
            model.Execute("60,5");

            Assert.Equal(
                new[] { "No Endpoint found for specified point.", "Invalid window specification." },
                model.History.Skip(model.History.Count - 2)
            );
            Assert.Equal("Specify first corner:", model.Prompt);
            Assert.Null(model.Snap.Override);
        }

        [Fact]
        public void EndTypedAtAFencePoint_WithNothingNear_IsAnInvalidResponse()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");
            model.Execute("F");

            model.Execute("END");
            model.Execute("60,5");

            Assert.Equal(
                new[] { "No Endpoint found for specified point.", "Invalid response." },
                model.History.Skip(model.History.Count - 2)
            );
            Assert.Equal("Specify first fence point or pick/drag cursor:", model.Prompt);
        }

        [Fact]
        public void ALoneNumberAtTheCornerOfAWindowOnTheCommandLine_IsAPointThatFarFromTheLastPoint()
        {
            MainViewModel model = Open();
            model.StartCommand("ERASE");

            // "W 1" is W then 1: the corner is 1 from the last point (the origin) along the band.
            model.Execute("W 1");

            Assert.StartsWith("Specify opposite corner", model.Prompt);
            Assert.Equal(1.0, model.Session.LastPoint.X, 9);
        }

        [Fact]
        public void OnTheCanvas_EndChosenBeforeTheOppositeCornerOfTheImpliedWindow_SnapsTheClick()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.StartCommand("ERASE");
                    canvas.View.ZoomTo(new DisplayBox(-50, -50, 150, 100));
                    Click(canvas, At(canvas, 69, 40));
                    Assert.True(model.Session.IsWindowOpen);

                    // The click lies off the line, 1.8 from its end at 4 pixels to the unit: the aperture of 10 pixels (2.5 units)
                    // reaches it, and the corner is the end (100, 0): the window holds the short line, as it would not from (99, 1.5).
                    Assert.True(model.OverrideSnap(CadSnapModes.Endpoint));
                    Click(canvas, At(canvas, 99, 1.5));

                    Assert.Equal("1 found", model.History.Last());
                    Assert.Single(model.Session.Gathering);
                }
            );
        }

        [Fact]
        public void OnTheCanvas_EndChosenBeforeAClickOnNothingAtSelectObjects_SnapsTheFirstCornerOfTheImpliedWindow()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.StartCommand("ERASE");
                    canvas.View.ZoomTo(new DisplayBox(-50, -50, 150, 100));

                    Assert.True(model.OverrideSnap(CadSnapModes.Endpoint));
                    Click(canvas, At(canvas, 99, 1.5));

                    Assert.True(model.Session.IsWindowOpen);
                    Assert.Equal(100.0, model.Session.Prompt.Band.Points[0].X, 6);
                    Assert.Equal(0.0, model.Session.Prompt.Band.Points[0].Y, 6);
                    Assert.Null(model.Snap.Override);
                }
            );
        }

        [Fact]
        public void OnTheCanvas_AClickAtTheImpliedWindowWithNoOverride_IsNotSnapped()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.StartCommand("ERASE");
                    canvas.View.ZoomTo(new DisplayBox(-50, -50, 150, 100));
                    Click(canvas, At(canvas, 69, 40));

                    Click(canvas, At(canvas, 99, 1.5));

                    Assert.Equal("0 found", model.History.Last());
                }
            );
        }

        private static void OnCanvas(Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    MainViewModel model = Open();
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                    };
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.SelectionChanged += (s, e) => model.RefreshHistory();
                    canvas.Measure(new Size(800, 600));
                    canvas.Arrange(new Rect(0, 0, 800, 600));
                    canvas.RenderNow();
                    test(model, canvas);
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
    }
}
