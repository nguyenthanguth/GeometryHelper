using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The system variables the window keeps (OSMODE, the status bar's toggles, PICKBOX, APERTURE, LWDISPLAY), set at once with
    /// no undo step; and the grip modes from the canvas: a grip clicked asks STRETCH, a click stretches it, Space goes on to
    /// MOVE, whose ghost follows the cursor.
    /// </summary>
    public sealed class SystemVariableAndGripWindowTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private static MainViewModel Room()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(TestFiles.Room(out _, out _), "room.dxf"));
            return model;
        }

        [Fact]
        public void Osmode_ReadsTheSnapsTicked_AndSetTicksThem_ItsBit16384SwitchingOsnapOff()
        {
            MainViewModel model = Room();
            model.SnapModes = CadSnapModes.Endpoint | CadSnapModes.Center | CadSnapModes.Intersection | CadSnapModes.Extension;

            model.Execute("OSMODE");
            Assert.Equal("Enter new value for OSMODE <4133>:", model.Prompt);
            model.Execute("40000");
            Assert.Equal("Requires an integer between 0 and 32767.", model.History.Last());
            model.Execute("16391");

            Assert.Equal(CadSnapModes.Endpoint | CadSnapModes.Midpoint | CadSnapModes.Center, model.SnapModes);
            Assert.Equal(model.SnapModes, model.Settings.Snaps);
            Assert.False(model.Toggles.Single(t => t.Name == "OSNAP").IsOn);
            Assert.False(model.Document.CanUndo);

            model.Execute("SETVAR");
            model.Execute("OSMODE");
            Assert.Equal("Enter new value for OSMODE <16391>:", model.Prompt);
            model.Execute("7");
            Assert.True(model.Toggles.Single(t => t.Name == "OSNAP").IsOn);
        }

        [Fact]
        public void TheToggleVariablesSwitchTheStatusBar_AndPolarangTheIncrement()
        {
            MainViewModel model = Room();

            model.Execute("ORTHOMODE");
            Assert.Equal("Enter new value for ORTHOMODE <0>:", model.Prompt);
            model.Execute("1");
            model.Execute("GRIDMODE");
            model.Execute("1");
            model.Execute("SNAPMODE");
            model.Execute("0");
            model.Execute("AUTOSNAP");
            Assert.Equal("Enter new value for AUTOSNAP <55>:", model.Prompt);
            model.Execute("47");
            model.Execute("POLARANG");
            Assert.Equal("Enter new value for POLARANG <90>:", model.Prompt);
            model.Execute("45");
            model.Execute("DYNMODE");
            model.Execute("0");

            Assert.True(model.Toggles.Single(t => t.Name == "ORTHO").IsOn);
            Assert.True(model.Snap.Ortho);
            Assert.True(model.GridOn);
            Assert.False(model.Snap.GridSnap);
            Assert.True(model.Snap.Polar);
            Assert.False(model.Snap.Tracking);
            Assert.Equal(Math.PI / 4.0, model.Snap.PolarAngle, 12);
            Assert.False(model.DynamicInput.IsOn);
            Assert.False(model.Document.CanUndo);

            // The drawing's $ORTHOMODE is left as it was: the window's ORTHO answers ORTHOMODE.
            Assert.False(model.Document.Database.Orthomode);
        }

        [Fact]
        public void PickboxApertureAndLwdisplay_SetTheSettingsInUse_AndTheWindowIsToldAsWhenSettingsAreApplied()
        {
            MainViewModel model = Room();
            int applied = 0;
            model.SettingsApplied += (s, e) => applied++;

            model.Execute("PICKBOX");
            Assert.Equal("Enter new value for PICKBOX <5>:", model.Prompt);
            model.Execute("0");
            Assert.Equal("Requires an integer between 1 and 50.", model.History.Last());
            model.Execute("8");
            model.Execute("APERTURE");
            model.Execute("12");
            model.Execute("LWDISPLAY");
            model.Execute("0");

            Assert.Equal(8, model.Settings.PickboxPixels);
            Assert.Equal(12, model.Settings.AperturePixels);
            Assert.False(model.Settings.ShowLineWeights);
            Assert.Equal(3, applied);
            Assert.False(model.Document.CanUndo);
        }

        [Fact]
        public void PickboxApertureLwdisplayAndPolarang_AreKeptInTheSettingsFile_AsDynAndOsmodeAre()
        {
            MainViewModel model = Room();
            model.SettingsPath = System.IO.Path.Combine(_folder.FullPath, "settings.json");
            int raised = 0;

            // The window saves the settings on SettingsChanged, as CadAppHost does.
            model.SettingsChanged += (s, e) =>
            {
                raised++;
                model.Settings.Save(model.SettingsPath);
            };

            model.Execute("PICKBOX");
            model.Execute("9");
            model.Execute("APERTURE");
            model.Execute("14");
            model.Execute("LWDISPLAY");
            model.Execute("0");
            model.Execute("POLARANG");
            model.Execute("400");
            Assert.Equal("Value must be at most 360.", model.History.Last());
            model.Execute("30");

            Assert.Equal(4, raised);
            Assert.Equal(Math.PI / 6.0, model.Snap.PolarAngle, 12);
            GlobalSettings saved = GlobalSettings.Load(model.SettingsPath);
            Assert.Equal(9, saved.PickboxPixels);
            Assert.Equal(14, saved.AperturePixels);
            Assert.False(saved.ShowLineWeights);
            Assert.Equal(30.0, saved.PolarAngle);

            // Read back by a new window, the polar increment is the snaps' again; Drafting Settings' increment is kept the same way.
            var again = new MainViewModel();
            again.ApplySettings(saved);
            Assert.Equal(Math.PI / 6.0, again.Snap.PolarAngle, 12);

            DraftingSettingsViewModel dialog = model.CreateDraftingSettings("Polar Tracking");
            dialog.IncrementAngle = "15";
            Assert.True(dialog.Apply());
            Assert.Equal(15.0, GlobalSettings.Load(model.SettingsPath).PolarAngle);
        }

        [Fact]
        public void AGripClicked_AsksStretch_AClickStretchesIt_AndSpaceGoesOnToMoveWhoseGhostFollows()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var model = new MainViewModel();
                    model.Open(_folder.Write("room.dxf", TestFiles.Room(out string bottom, out _)));
                    model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
                    model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                    var canvas = new CadCanvas { Document = model.Document, Space = model.Space, View = model.View, Session = model.Session, Snap = model.Snap };
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.Measure(new Size(800, 600));
                    canvas.Arrange(new Rect(0, 0, 800, 600));
                    canvas.RenderNow();

                    Click(canvas, At(canvas, 1000, 0));
                    canvas.RenderNow();
                    Assert.Equal(3, canvas.GripCount);

                    // A click on the grip at the line's start, let go where it was pressed: the grip edit waits for a point.
                    Click(canvas, At(canvas, 0, 0));
                    Assert.True(model.IsCommandRunning);
                    Assert.Equal("Specify stretch point or [Base point/Copy/Undo/eXit]:", model.Prompt);
                    Assert.Contains("** STRETCH **", model.History);
                    canvas.RenderNow();
                    Assert.Equal(3, canvas.GripCount);

                    Click(canvas, At(canvas, 0, -500));
                    var line = (DxfLine)model.Document.FindEntity(bottom);
                    Assert.Equal(-500.0, line.StartPoint.Y, 6);
                    Assert.False(model.IsCommandRunning);

                    // Again, then Space: MOVE, the ghost of the line drawn moved with the cursor.
                    canvas.RenderNow();
                    Click(canvas, At(canvas, 0, -500));
                    model.Execute(string.Empty);
                    Assert.Equal("Specify move point or [Base point/Copy/Undo/eXit]:", model.Prompt);
                    Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 300, -500));
                    Assert.NotNull(CadGhost.Transform(model.Session, new GeometryHelper.Geometry.GeoPoint2(300, -500)));
                    Click(canvas, At(canvas, 300, -500));

                    line = (DxfLine)model.Document.FindEntity(bottom);
                    Assert.Equal(300.0, line.StartPoint.X, 6);
                    Assert.Equal(4300.0, line.EndPoint.X, 6);
                    Assert.True(model.Document.Undo());
                    Assert.Equal(0.0, ((DxfLine)model.Document.FindEntity(bottom)).StartPoint.X, 6);
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
