using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>Small follow-ups of the batch 4 commands in the window: HATCHEDIT's Click to set new origin, and -OSNAP's short names.</summary>
    public class SmallFollowUpsWindowTests
    {
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

        [Fact]
        public void HatchEdit_ClickToSetNewOrigin_ClosesTheDialog_AsksThePoint_AndShowsTheDialogAgainWithIt()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel();
                    Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                    model.Document.SupportFolders = Array.Empty<string>();
                    model.ShowHatchSettings = dialog => true;
                    model.Execute("H");
                    model.Execute("T");
                    model.Execute("2000,1500");
                    model.Execute(string.Empty);
                    DxfHatch hatch = model.Document.Database.ModelSpace.OfType<DxfHatch>().Single();
                    Assert.Equal(0.0, hatch.GetPatternDefinitionAt(0).BaseX);

                    var shown = new List<HatchSettingsViewModel>();
                    model.ShowHatchSettings = dialog =>
                    {
                        shown.Add(dialog);

                        if (shown.Count == 1)
                        {
                            Assert.Equal("Hatch Editor", dialog.Title);
                            Assert.True(dialog.CanPickOrigin);
                            dialog.Scale = "2";
                            dialog.ClickToSetOrigin();
                            return false;
                        }

                        Assert.Same(shown[0], dialog);
                        Assert.True(dialog.IsSpecifiedOrigin);
                        Assert.Equal("12.5", dialog.OriginX);
                        Assert.Equal("40", dialog.OriginY);
                        Assert.Equal("2", dialog.Scale);
                        return true;
                    };

                    model.Execute("HE");
                    model.Session.FeedSelection(new[] { hatch.Handle });
                    Assert.Equal("Specify origin point:", model.Prompt);
                    model.Execute("12.5,40");

                    Assert.Equal(2, shown.Count);
                    Assert.False(model.IsCommandRunning);
                    DxfHatch edited = model.Document.Database.ModelSpace.OfType<DxfHatch>().Single();
                    Assert.Equal(2.0, edited.PatternScale);
                    Assert.NotEqual(0.0, edited.GetPatternDefinitionAt(0).BaseX);
                    Assert.Equal("HATCHEDIT", model.Document.NextUndo.Name);
                }
            });
        }

        [Fact]
        public void HatchEdit_CancelAfterThePick_ChangesNothing()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel();
                    Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                    model.Document.SupportFolders = Array.Empty<string>();
                    model.ShowHatchSettings = dialog => true;
                    model.Execute("H");
                    model.Execute("T");
                    model.Execute("2000,1500");
                    model.Execute(string.Empty);
                    DxfHatch hatch = model.Document.Database.ModelSpace.OfType<DxfHatch>().Single();
                    int shown = 0;
                    model.ShowHatchSettings = dialog =>
                    {
                        shown++;

                        if (shown == 1)
                        {
                            dialog.ClickToSetOrigin();
                        }

                        return false;
                    };

                    model.Execute("HE");
                    model.Session.FeedSelection(new[] { hatch.Handle });
                    model.Execute("12.5,40");

                    Assert.Equal(2, shown);
                    Assert.False(model.IsCommandRunning);
                    Assert.Equal("HATCH", model.Document.NextUndo.Name);
                }
            });
        }

        [Theory]
        [InlineData(CadSnapModes.Nearest, "Near")]
        [InlineData(CadSnapModes.GeometricCenter, "Gcen")]
        [InlineData(CadSnapModes.ApparentIntersection, "App Int")]
        [InlineData(CadSnapModes.Intersection | CadSnapModes.ApparentIntersection, "Int,App Int")]
        public void MinusOsnap_NamesTheRarerSnapsAsAutoCad2021Does(CadSnapModes modes, string said)
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "a.dxf"));
            model.SnapModes = modes;

            model.Execute("-OSNAP");

            Assert.Contains("Current osnap modes: " + said, model.History);
            model.Execute(string.Empty);
        }
    }
}
