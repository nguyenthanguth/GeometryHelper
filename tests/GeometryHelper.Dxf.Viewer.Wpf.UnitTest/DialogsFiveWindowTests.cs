using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The dialogs left by earlier rounds driven with no window shown: Multiline Style (MLSTYLE) with its New, Modify, Rename,
    /// Delete and Set Current, and Multiline Edit Tools (MLEDIT), each raised by its command through the view model's function.
    /// </summary>
    public class DialogsFiveWindowTests
    {
        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("dialogs5.dxf", new DxfDatabase("AC1027"))));
            return model;
        }

        private static DxfMlineStyle Current(MainViewModel model) => (DxfMlineStyle)model.Document.Database.CmlstyleID;

        [Fact]
        public void Mlstyle_NewStartsFromTheStylePicked_ModifyRenameAndSetCurrent_TakenOnOk()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                bool shown = false;
                model.ShowMlineStyle = dialog =>
                {
                    shown = true;
                    Assert.Equal(new[] { "STANDARD" }, dialog.Names);
                    Assert.Equal("STANDARD", dialog.Selected);
                    Assert.NotNull(dialog.Preview);
                    Assert.False(dialog.CanDelete);
                    Assert.False(dialog.CanRename);

                    dialog.AskNewStyle = naming =>
                    {
                        naming.Name = "standard";
                        Assert.False(naming.TryContinue());
                        Assert.Equal("Multiline style standard already exists.", naming.Message);
                        naming.Name = "Wall";
                        Assert.True(naming.TryContinue());
                        return true;
                    };
                    dialog.EditStyle = editing =>
                    {
                        Assert.Equal("New Multiline Style: WALL", editing.Title);
                        Assert.True(editing.IsEditable);
                        Assert.Equal(new[] { "0.5", "-0.5" }, editing.Elements.Select(e => e.OffsetText));
                        editing.Description = "Wall 240";
                        editing.Element = editing.Elements[0];
                        editing.Offset = "120";
                        editing.ElementColor = "Red";
                        editing.Element = editing.Elements[1];
                        editing.Offset = "-120";
                        Assert.True(editing.Add());
                        Assert.Equal(new[] { "120", "0", "-120" }, editing.Elements.Select(e => e.OffsetText));
                        editing.StartLine = true;
                        editing.FillColor = "Yellow";
                        editing.EndAngle = "5";
                        Assert.False(editing.TryAccept());
                        Assert.Equal("An angle is from 10 to 170 degrees.", editing.Message);
                        editing.EndAngle = "45";
                        return editing.TryAccept();
                    };
                    Assert.True(dialog.New());
                    Assert.Equal("WALL", dialog.Selected);
                    Assert.Equal("Wall 240", dialog.Description);

                    dialog.BeginRename();
                    Assert.True(dialog.IsRenaming);
                    dialog.RenameText = "Wall-240";
                    Assert.True(dialog.CommitRename());
                    Assert.Equal(new[] { "STANDARD", "WALL-240" }, dialog.Names);
                    dialog.SetCurrent();
                    Assert.Equal("WALL-240", dialog.CurrentName);
                    return true;
                };

                model.Execute("MLSTYLE");

                Assert.True(shown);
                DxfMlineStyle style = Current(model);
                Assert.Equal("WALL-240", style.Name);
                Assert.Equal(new[] { 120.0, 0.0, -120.0 }, style.Elements.Select(e => e.Offset));
                Assert.Equal(1, style.Elements[0].Color.ColorIndex);
                Assert.True(style.StartSquareCap);
                Assert.True(style.Filled);
                Assert.Equal(2, style.FillColor.ColorIndex);
                Assert.Equal(45.0, style.EndAngle * 180.0 / System.Math.PI, 9);
            }
        }

        [Fact]
        public void Mlstyle_ModifyOfStandard_TakesOnlyTheDescription_AndDeleteRefusesTheCurrentStyle()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowMlineStyle = dialog =>
                {
                    dialog.EditStyle = editing =>
                    {
                        Assert.False(editing.IsEditable);
                        editing.Description = "Base";
                        return editing.TryAccept();
                    };
                    Assert.True(dialog.Modify());
                    Assert.Equal("Base", dialog.Description);
                    Assert.False(dialog.Delete());
                    Assert.Contains("cannot be deleted", dialog.Message);
                    return false;
                };

                model.Execute("MLSTYLE");

                Assert.Equal("Base", Current(model).Description);
                Assert.Equal(0.5, Current(model).Elements[0].Offset);
            }
        }

        [Fact]
        public void Mlstyle_TheEditPageKeepsOneElementAndAtMostSixteen()
        {
            var page = new MlineStyleEditViewModel("New Multiline Style: A", new DxfMlineStyleSettings(), false, null);

            Assert.True(page.Delete());
            Assert.False(page.Delete());
            Assert.Equal("A multiline style has at least one element.", page.Message);

            while (page.Elements.Count < 16)
            {
                Assert.True(page.Add());
            }

            Assert.False(page.Add());
            Assert.Equal("A multiline style has at most 16 elements.", page.Message);
        }

        [Fact]
        public void Mledit_TheDialogShowsTwelveTools_AndTheToolPickedAsksItsMultilines()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.Execute("MLINE");
                model.Execute("0,0");
                model.Execute("100,0");
                model.Execute("");
                model.ShowMlineEditTools = dialog =>
                {
                    Assert.Equal(12, dialog.Tools.Count);
                    Assert.Equal("Closed Cross", dialog.Tools[0].Name);
                    Assert.Equal("Cut All", dialog.Tools[7].Name);
                    dialog.Pick("CA");
                };

                model.Execute("MLEDIT");

                Assert.Equal("Select mline:", model.Prompt);
            }
        }

        [Fact]
        public void Mledit_ClosedWithNoTool_EndsTheCommand()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                bool shown = false;
                model.ShowMlineEditTools = dialog => shown = true;

                model.Execute("MLEDIT");

                Assert.True(shown);
                Assert.False(model.IsCommandRunning);
            }
        }

        private static MainViewModel Room(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            model.Document.SupportFolders = Array.Empty<string>();
            return model;
        }

        [Fact]
        public void Hatch_TheDialogMakesAGradientOfOneColour_InTheColourLayerAndTransparencyChosen()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.Document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "A-HATCH" });
                model.ShowHatchSettings = dialog =>
                {
                    dialog.Type = "Gradient";
                    Assert.True(dialog.IsTwoColor);
                    dialog.IsOneColor = true;
                    dialog.Tint = 0.25;
                    Assert.Contains("A-HATCH", dialog.Layers);
                    dialog.Layer = "A-HATCH";
                    Assert.Equal("Use Current", dialog.HatchColor);
                    dialog.HatchColor = "Red";
                    dialog.Transparency = "Transparency value";
                    dialog.TransparencyValue = "95";
                    Assert.Equal("Requires an integer between 0 and 90.", dialog.Message);
                    dialog.TransparencyValue = "40";
                    dialog.GapTolerance = "6000";
                    Assert.Equal("Value must be between 0 and 5000.", dialog.Message);
                    dialog.GapTolerance = "3";
                    return true;
                };

                model.Execute("H");
                model.Execute("T");
                model.Execute("2000,1500");
                model.Execute(string.Empty);

                DxfHatch hatch = model.Document.Database.ModelSpace.OfType<DxfHatch>().Single();
                Assert.True(hatch.IsGradient);
                Assert.True(hatch.GradientOneColorMode);
                Assert.Equal(0.25f, hatch.ShadeTintValue);
                Assert.Equal("A-HATCH", hatch.Layer);
                Assert.Equal(1, hatch.Color.ColorIndex);
                Assert.Equal(153, hatch.Transparency.Alpha);
                Assert.Equal(3.0, model.Document.HatchSettings.GapTolerance);
            }
        }

        [Fact]
        public void Hatch_ASpecifiedOriginNotStored_LaysThisHatchAlone_AndACornerIsKept()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.ShowHatchSettings = dialog =>
                {
                    Assert.True(dialog.IsCurrentOrigin);
                    dialog.IsSpecifiedOrigin = true;
                    dialog.OriginX = "7";
                    dialog.OriginY = "9";
                    Assert.True(dialog.IsOriginOnce);
                    return true;
                };

                model.Execute("H");
                model.Execute("T");
                Assert.Equal(7.0, model.Document.HatchSettings.OriginX);
                model.Execute("2000,1500");
                model.Execute(string.Empty);

                Assert.Equal(0.0, model.Document.HatchSettings.OriginX);
                Assert.Equal(0.0, model.Document.HatchSettings.OriginY);

                model.ShowHatchSettings = dialog =>
                {
                    dialog.IsSpecifiedOrigin = true;
                    dialog.OriginX = "7";
                    dialog.StoreAsDefault = true;
                    Assert.False(dialog.IsOriginOnce);
                    return true;
                };
                model.Execute("H");
                model.Execute("T");
                model.Execute(string.Empty);
                Assert.Equal(7.0, model.Document.HatchSettings.OriginX);

                model.ShowHatchSettings = dialog =>
                {
                    dialog.DefaultToExtents = true;
                    dialog.Corner = "Top right";
                    Assert.True(dialog.IsSpecifiedOrigin);
                    Assert.False(dialog.IsOriginOnce);
                    return true;
                };
                model.Execute("H");
                model.Execute("T");
                model.Execute(string.Empty);
                Assert.Equal(CadHatchOriginMode.TopRight, model.Document.HatchSettings.OriginMode);
            }
        }

        [Fact]
        public void TheNewWindows_LoadTheirLayoutsAndLayOutWithNoWindowShown()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var page = new MlineStyleEditViewModel(
                        "New Multiline Style: A",
                        new DxfMlineStyleSettings(),
                        false,
                        null
                    );
                    var tools = new MlineEditToolsViewModel(new CadMlineEditRequest());
                    Lay(new MlineStyleEditWindow(page).Body);
                    Lay(new MlineEditToolsWindow(tools).Body);
                    CadDocument blank = CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf");
                    var hatch = new HatchSettingsViewModel(blank, new CadHatchSettings(), false);
                    Lay(new HatchSettingsWindow(hatch).Body);

                    foreach (Window window in Windows())
                    {
                        Lay((FrameworkElement)window.Content);
                    }
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>The windows whose view models need a running command, made over a request MLSTYLE raised.</summary>
        private static IEnumerable<Window> Windows()
        {
            var made = new List<Window>();

            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowMlineStyle = dialog =>
                {
                    made.Add(new MlineStyleWindow(dialog));
                    dialog.AskNewStyle = naming =>
                    {
                        made.Add(new MlineStyleNewWindow(naming));
                        return false;
                    };
                    dialog.AskLoad = loading =>
                    {
                        made.Add(new MlineStyleLoadWindow(loading));
                        return false;
                    };
                    dialog.EditStyle = editing => false;
                    dialog.New();
                    dialog.Load();
                    return false;
                };
                model.Execute("MLSTYLE");
                model.ShowXbind = dialog =>
                {
                    made.Add(new XbindWindow(dialog));
                    return false;
                };
                model.Execute("XBIND");
                model.ShowAttributeExtraction = dialog => made.Add(new AttributeExtractionWindow(dialog));
                model.Execute("ATTEXT");
                var stamp = new PlotStampViewModel(new CadPlotStamp());
                made.Add(new PlotStampWindow(stamp));
                made.Add(new PlotStampAdvancedWindow(stamp));
            }

            return made;
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
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void Plotstamp_TheDialogSetsTheSessionsStampOnOk_AdvancedOnACopy_AndAFieldOfADotIsCleared()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowPlotStamp = dialog =>
                {
                    Assert.True(dialog.DrawingName);
                    dialog.LayoutName = true;
                    dialog.UserField1 = "Level 2";
                    dialog.UserField2 = "x";
                    dialog.UserField2 = ".";
                    Assert.Equal(string.Empty, dialog.UserField2);
                    dialog.ShowAdvanced = advanced =>
                    {
                        advanced.Location = "Top Right";
                        advanced.Units = "Millimeters";
                        advanced.TextHeight = "0";
                        Assert.False(advanced.TryAccept());
                        Assert.Equal("The text height is a number above nought.", advanced.Message);
                        advanced.TextHeight = "3";
                        advanced.OffsetX = "5";
                        return advanced.TryAccept();
                    };
                    Assert.True(dialog.Advanced());
                    Assert.Equal("Top Right", dialog.Location);
                    Assert.Equal(170.0 - 6.0 - 70.0, dialog.MarkLeft);
                    Assert.Equal("3", dialog.TextHeight);
                    return dialog.TryAccept();
                };

                model.Execute("PLOTSTAMP");

                CadPlotStamp stamp = model.Session.PlotStamp;
                Assert.True(stamp.LayoutName);
                Assert.Equal("Level 2", stamp.UserField1);
                Assert.Equal(CadPlotStampCorner.TopRight, stamp.Location);
                Assert.Equal(CadPlotStampUnits.Millimeters, stamp.Units);
                Assert.Equal(3.0, stamp.TextHeight);
                Assert.Equal(5.0, stamp.OffsetX);
            }
        }

        [Fact]
        public void Plot_PlotStampOn_SwitchesTheSessionsStampOnOk_AndThePlotCarriesACopy()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                bool ok = false;
                model.ShowPlot = dialog =>
                {
                    Assert.False(dialog.PlotStampOn);
                    dialog.PlotStampOn = true;
                    Assert.True(dialog.Settings.PlotStamp.On);
                    Assert.NotSame(model.Session.PlotStamp, dialog.Settings.PlotStamp);
                    return ok;
                };

                model.Execute("PLOT");
                Assert.False(model.Session.PlotStamp.On);

                ok = true;
                model.Execute("PLOT");
                Assert.True(model.Session.PlotStamp.On);
            }
        }

        [Fact]
        public void Attext_TheDialogsDxxNeedsNoTemplate_AndWritesTheExtractNamedWithItsExtension()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowAttributeExtraction = dialog =>
                {
                    Assert.True(dialog.IsCdf);
                    Assert.Equal("Number found: all", dialog.NumberFound);
                    Assert.False(dialog.TryAccept());
                    Assert.Equal("A CDF or SDF extract needs a template file.", dialog.Message);
                    dialog.IsDxf = true;
                    Assert.False(dialog.NeedsTemplate);
                    Assert.EndsWith("dialogs5.dxx", dialog.OutputFile);
                    Assert.True(dialog.TryAccept());
                };

                model.Execute("ATTEXT");

                Assert.True(System.IO.File.Exists(System.IO.Path.Combine(folder.FullPath, "dialogs5.dxx")));
            }
        }

        [Fact]
        public void ImageAdjust_TheSlidersAreKeptFrom0To100_ResetGivesTheFactoryValues_AndOkAdjustsTheImage()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    var database = new DxfDatabase("AC1027");
                    DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(database);
                    var definition = new DxfRasterImageDef("logo.png", new Geometry.GeoVector2(40, 30));
                    images.SetAt("logo", definition);
                    var image = new DxfRasterImage(definition);
                    image.SetOrientation(
                        new Geometry.GeoPoint3(0, 0, 0),
                        new Geometry.GeoVector3(40, 0, 0),
                        new Geometry.GeoVector3(0, 30, 0)
                    );
                    database.ModelSpace.AppendEntity(image);
                    var model = new MainViewModel();
                    Assert.True(model.Open(folder.Write("image.dxf", database)));
                    string handle = model.Document.ModelSpace.Block.OfType<DxfRasterImage>().Single().Handle;
                    model.ShowImageAdjust = dialog =>
                    {
                        Lay(new ImageAdjustWindow(dialog).Body);
                        Assert.False(dialog.HasPicture);
                        Assert.Equal("1 image selected.", dialog.Caption);
                        dialog.Brightness = 150;
                        Assert.Equal(100, dialog.Brightness);
                        dialog.Reset();
                        Assert.Equal(50, dialog.Brightness);
                        dialog.Contrast = 30;
                        dialog.Fade = 20;
                        return true;
                    };

                    model.Execute("IMAGEADJUST");
                    model.Session.FeedSelection(new[] { handle });
                    model.Execute(string.Empty);

                    DxfRasterImage adjusted = model.Document.ModelSpace.Block.OfType<DxfRasterImage>().Single();
                    Assert.Equal((byte)50, adjusted.Brightness);
                    Assert.Equal((byte)30, adjusted.Contrast);
                    Assert.Equal((byte)20, adjusted.Fade);
                }
            });
        }

        [Fact]
        public void Xbind_AddListsADefinitionOnce_OrEveryOneUnderAKind_RemoveTakesItOff_AndOkBinds()
        {
            using (var folder = new TestFolder())
            {
                var grid = new DxfDatabase("AC1027");
                grid.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID" });
                grid.LayerTable.Add(new DxfLayerTableRecord { Name = "AXIS" });
                grid.ModelSpace.AppendEntity(
                    new DxfLine(new Geometry.GeoPoint3(0, 0, 0), new Geometry.GeoPoint3(100, 0, 0)) { Layer = "GRID" }
                );
                string gridPath = folder.Write("grid.dxf", grid);
                MainViewModel model = Open(folder);
                model.Execute("-XREF");
                model.Execute("A");
                model.Execute(gridPath);
                model.Execute("0,0");
                model.Execute(string.Empty);
                model.Execute(string.Empty);
                model.Execute(string.Empty);
                Assert.True(model.Document.Database.LayerTable.Has("grid|GRID"));
                model.ShowXbind = dialog =>
                {
                    Assert.Equal(new[] { "grid", "Layer", "grid|AXIS", "grid|GRID" }, dialog.Xrefs.Select(r => r.Text));
                    dialog.Picked = dialog.Xrefs[3];
                    dialog.Add();
                    dialog.Add();
                    Assert.Single(dialog.ToBind);
                    dialog.Picked = dialog.Xrefs[1];
                    dialog.Add();
                    Assert.Equal(new[] { "grid|GRID", "grid|AXIS" }, dialog.ToBind.Select(r => r.Text));
                    dialog.Chosen = dialog.ToBind[1];
                    dialog.Remove();
                    Assert.Equal(new[] { "grid|GRID" }, dialog.ToBind.Select(r => r.Text));
                    return true;
                };

                model.Execute("XBIND");

                Assert.True(model.Document.Database.LayerTable.Has("grid$0$GRID"));
                Assert.True(model.Document.Database.LayerTable.Has("grid|AXIS"));
            }
        }

        private static void Lay(FrameworkElement element)
        {
            element.Measure(new Size(2000, 2000));
            element.Arrange(new Rect(element.DesiredSize));
            Assert.True(element.DesiredSize.Width > 0);
        }
    }
}
