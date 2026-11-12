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
    /// The system variables the window keeps (OSMODE, the status bar's toggles, PICKBOX, APERTURE, LWDISPLAY,
    /// GRIPDYNCOLOR), set at once with no undo step; and the grip modes from the canvas: a grip clicked asks STRETCH, a
    /// click stretches it, Space goes on to MOVE, whose ghost follows the cursor.
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
            model.SnapModes =
                CadSnapModes.Endpoint | CadSnapModes.Center | CadSnapModes.Intersection | CadSnapModes.Extension;

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
            Assert.Equal("Enter new value for SNAPMODE <0>:", model.Prompt);
            model.Execute("1");
            model.Execute("AUTOSNAP");
            Assert.Equal("Enter new value for AUTOSNAP <63>:", model.Prompt);
            model.Execute("47");
            model.Execute("POLARANG");
            Assert.Equal("Enter new value for POLARANG <90>:", model.Prompt);
            model.Execute("45");
            model.Execute("DYNMODE");
            model.Execute("0");

            Assert.True(model.Toggles.Single(t => t.Name == "ORTHO").IsOn);
            Assert.True(model.Snap.Ortho);
            Assert.True(model.GridOn);
            Assert.True(model.Snap.GridSnap);
            Assert.True(model.Snap.Polar);
            Assert.False(model.Snap.Tracking);
            Assert.Equal(Math.PI / 4.0, model.Snap.PolarAngle, 12);
            Assert.False(model.DynamicInput.IsOn);

            // ORTHOMODE, GRIDMODE and SNAPMODE typed are undo steps, as AutoCAD's U takes them back; the rest are the window's.
            Assert.True(model.Document.CanUndo);

            // The window's ORTHO answers ORTHOMODE, and is written to the drawing's $ORTHOMODE too, as AutoCAD saves it there.
            Assert.True(model.Document.Database.Orthomode);
        }

        [Fact]
        public void Autosnap_SetsTheMarkerTheTipsAndTheMagnet_ByItsBits_AsAutoCad()
        {
            MainViewModel model = Room();

            model.Execute("AUTOSNAP");
            model.Execute("0");
            Assert.False(model.Snap.ShowsMarker);
            Assert.False(model.Snap.ShowsSnapTip);
            Assert.False(model.Snap.Magnet);
            Assert.False(model.Snap.ShowsTrackingTip);
            Assert.False(model.Toggles.Single(t => t.Name == "POLAR").IsOn);
            Assert.False(model.Toggles.Single(t => t.Name == "OTRACK").IsOn);

            model.Execute("AUTOSNAP");
            Assert.Equal("Enter new value for AUTOSNAP <0>:", model.Prompt);
            model.Execute("37");
            Assert.True(model.Snap.ShowsMarker);
            Assert.False(model.Snap.ShowsSnapTip);
            Assert.True(model.Snap.Magnet);
            Assert.True(model.Snap.ShowsTrackingTip);
            model.Execute("AUTOSNAP");
            Assert.Equal("Enter new value for AUTOSNAP <37>:", model.Prompt);
        }

        [Fact]
        public void Polarmode_TracksOnThePolarAnglesBy2_AcquiresWithShiftBy8_AndIsKeptInTheSettingsFile()
        {
            MainViewModel model = Room();
            model.SettingsPath = System.IO.Path.Combine(_folder.FullPath, "settings.json");
            model.SettingsChanged += (s, e) => model.Settings.Save(model.SettingsPath);
            Assert.False(model.Snap.TracksPolarAngles);

            model.Execute("POLARMODE");
            Assert.Equal("Enter new value for POLARMODE <0>:", model.Prompt);
            model.Execute("16");
            Assert.Equal("Requires an integer between 0 and 15.", model.History.Last());
            model.Execute("10");

            Assert.True(model.Snap.TracksPolarAngles);
            Assert.True(model.Snap.AcquiresWithShift);
            GlobalSettings saved = GlobalSettings.Load(model.SettingsPath);
            Assert.Equal(10, saved.PolarMode);
            Assert.Contains("\"polarMode\": 10", System.IO.File.ReadAllText(model.SettingsPath));

            var again = new MainViewModel();
            again.ApplySettings(saved);
            Assert.True(again.Snap.TracksPolarAngles);
            Assert.True(again.Snap.AcquiresWithShift);
        }

        [Fact]
        public void Polaraddang_TakesUpToTenAngles_KeptAsTheDrawingWritesAngles_AndHeldUnderPolarmode4()
        {
            MainViewModel model = Room();
            model.SettingsPath = System.IO.Path.Combine(_folder.FullPath, "settings.json");
            model.SettingsChanged += (s, e) => model.Settings.Save(model.SettingsPath);

            model.Execute("POLARADDANG");
            Assert.Equal("Enter new value for POLARADDANG, or . for none <\"\">:", model.Prompt);
            model.Execute("abc");
            Assert.Equal("Cannot set POLARADDANG to that value.", model.History.Last());
            Assert.Equal(string.Empty, model.Settings.PolarAddedAngles);

            // As AutoCAD keeps them: from nought up to a whole turn, to the places of AUPREC, set to 2 in this drawing.
            model.Document.Database.Auprec = 2;
            model.Execute("POLARADDANG");
            model.Execute("33.5;-10");
            Assert.Equal("33.50;350.00", model.Settings.PolarAddedAngles);
            Assert.Empty(model.Snap.AdditionalPolarAngles);

            model.Execute("POLARMODE");
            model.Execute("5");
            Assert.True(model.Snap.PolarFromLastSegment);
            Assert.Equal(
                new[] { 33.5, 350.0 },
                model.Snap.AdditionalPolarAngles.Select(a => Math.Round(a * 180.0 / Math.PI, 9))
            );
            Assert.Contains("\"polarAddedAngles\": \"33.50;350.00\"", System.IO.File.ReadAllText(model.SettingsPath));

            var again = new MainViewModel();
            again.ApplySettings(GlobalSettings.Load(model.SettingsPath));
            Assert.Equal(2, again.Snap.AdditionalPolarAngles.Count);
            Assert.True(again.Snap.PolarFromLastSegment);

            model.Execute("POLARADDANG");
            Assert.Equal("Enter new value for POLARADDANG, or . for none <\"33.50;350.00\">:", model.Prompt);
            model.Execute(".");
            Assert.Equal(string.Empty, model.Settings.PolarAddedAngles);
            Assert.Empty(model.Snap.AdditionalPolarAngles);
        }

        [Fact]
        public void TheSnapsCountPolarAnglesFromTheLineDrawnLast()
        {
            MainViewModel model = Room();
            Assert.Null(model.Snap.LastSegment);

            model.Execute("LINE");
            model.Execute("0,0");
            model.Execute("30,40");

            GeoVector2 way = model.Snap.LastSegment.Value;
            Assert.Equal(0.6, way.X, 12);
            Assert.Equal(0.8, way.Y, 12);
        }

        [Fact]
        public void Dynmode_KeepsWhichInputIsOn_ANegativeValueWhileDynIsOff_AndF12BringsItBack()
        {
            MainViewModel model = Room();

            model.Execute("DYNMODE");
            model.Execute("-2");
            Assert.False(model.DynamicInput.IsOn);
            model.Execute("DYNMODE");
            Assert.Equal("Enter new value for DYNMODE <-2>:", model.Prompt);
            model.Execute("-2");

            model.DynamicInput.IsOn = true;
            model.Execute("DYNMODE");
            Assert.Equal("Enter new value for DYNMODE <2>:", model.Prompt);
            model.Execute("0");
            Assert.False(model.DynamicInput.IsOn);

            model.DynamicInput.IsOn = true;
            model.Execute("DYNMODE");
            Assert.Equal("Enter new value for DYNMODE <3>:", model.Prompt);
        }

        [Fact]
        public void Gripdyncolor_ColoursTheCustomGrips_KeptInTheSettingsAndNotInTheDrawing()
        {
            int before = -1,
                after = -1;
            Exception failure = null;
            MainViewModel model = Room();
            var thread = new Thread(() =>
            {
                try
                {
                    var workspace = new CadWorkspace { DataContext = model };
                    model.ApplySettings(model.Settings);
                    before = workspace.DrawingCanvas.Options.CustomGripColor;

                    // AutoCAD 2021 starts at 140 and takes 1 to 255, and writes no GRIPDYNCOLOR into a drawing (plugin GhDb1).
                    model.Execute("GRIPDYNCOLOR");
                    Assert.Equal("Enter new value for GRIPDYNCOLOR <140>:", model.Prompt);
                    model.Execute("0");
                    Assert.Equal("Requires an integer between 1 and 255.", model.History.Last());
                    model.Execute("1");
                    after = workspace.DrawingCanvas.Options.CustomGripColor;
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
            Assert.Equal(0x00BFFF, before);
            Assert.Equal(0xFF0000, after);
            Assert.Equal(1, model.Settings.GripDynamicColor);
            Assert.Equal(140, new GlobalSettings().GripDynamicColor);
            Assert.Null(model.Document.Database.GetHeader("$GRIPDYNCOLOR"));
            Assert.False(model.Document.CanUndo);
        }

        [Fact]
        public void PickaddZero_MakesTheObjectClickedTheSelection_AndShiftAddsToIt()
        {
            MainViewModel model = Room();
            CadDocument document = model.Document;
            model.Execute("PICKADD");
            model.Execute("0");
            Assert.Equal(0, document.PickAdd);

            // The bottom wall runs along y 0, the top one along y 3000, both from x 0 to 4000.
            string bottom = CadPicking.Pick(document, document.ModelSpace, 2000, 0, 1.0, false);
            string top = CadPicking.Pick(document, document.ModelSpace, 2000, 3000, 1.0, false);
            Assert.Equal(new[] { top }, document.Selection.Handles);

            CadPicking.Pick(document, document.ModelSpace, 2000, 0, 1.0, true);
            Assert.Equal(
                new[] { bottom, top }.OrderBy(h => h, StringComparer.OrdinalIgnoreCase),
                document.Selection.Handles.OrderBy(h => h, StringComparer.OrdinalIgnoreCase)
            );
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
            Assert.Equal(2, applied);
            Assert.False(model.Document.CanUndo);

            // LWDISPLAY with a drawing open is the drawing's, kept in its $LWDISPLAY as AutoCAD keeps it, not in the settings.
            Assert.True(model.Settings.ShowLineWeights);
            Assert.False(model.ShowLineWeights);
            Assert.Equal("0", model.Document.Database.GetHeader("$LWDISPLAY").Single().Value.Trim());
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

            // Three values of the settings set, and each of the four names typed counted as a command used, which the settings
            // file keeps too; LWDISPLAY with a drawing open goes into the drawing, not the settings.
            Assert.Equal(7, raised);
            Assert.Equal(1, model.Settings.CommandUses["POLARANG"]);
            Assert.Equal(Math.PI / 6.0, model.Snap.PolarAngle, 12);
            GlobalSettings saved = GlobalSettings.Load(model.SettingsPath);
            Assert.Equal(9, saved.PickboxPixels);
            Assert.Equal(14, saved.AperturePixels);
            Assert.True(saved.ShowLineWeights);
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
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                    };
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
