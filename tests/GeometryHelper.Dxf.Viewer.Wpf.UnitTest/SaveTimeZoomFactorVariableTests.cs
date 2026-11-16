using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// SAVETIME and ZOOMFACTOR, the two variables of the window's Global settings: the autosave interval in minutes and the
    /// step of a notch of the wheel, 60 being the 1.5 Global settings starts at.
    /// </summary>
    public sealed class SaveTimeZoomFactorVariableTests
    {
        private static MainViewModel Room()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(TestFiles.Room(out _, out _), "room.dxf"));
            return model;
        }

        [Fact]
        public void Savetime_IsTheAutosaveInterval_ZeroToSixHundred_CutToWhatGlobalSettingsKeeps_WithNoUndoStep()
        {
            MainViewModel model = Room();

            model.Execute("SAVETIME");
            Assert.Equal("Enter new value for SAVETIME <10>:", model.Prompt);
            model.Execute("601");
            Assert.Equal("Requires an integer between 0 and 600.", model.History.Last());
            model.Execute("25");

            Assert.Equal(25, model.Settings.AutosaveMinutes);
            Assert.False(model.Document.CanUndo);

            model.Execute("SAVETIME");
            model.Execute("0");
            Assert.Equal(0, model.Settings.AutosaveMinutes);

            // Global settings keeps four hours at most.
            model.Execute("SAVETIME");
            model.Execute("600");
            Assert.Equal(240, model.Settings.AutosaveMinutes);
        }

        [Fact]
        public void Zoomfactor_IsTheWheelStep_ThreeToOneHundred_SixtyBeingTheOneAndAHalfGlobalSettingsStartsAt()
        {
            MainViewModel model = Room();
            int applied = 0;
            model.SettingsApplied += (s, e) => applied++;

            model.Execute("ZOOMFACTOR");
            Assert.Equal("Enter new value for ZOOMFACTOR <60>:", model.Prompt);
            model.Execute("2");
            Assert.Equal("Requires an integer between 3 and 100.", model.History.Last());
            model.Execute("90");

            Assert.Equal(1.75, model.Settings.WheelZoomFactor, 9);
            Assert.Equal(1, applied);
            Assert.False(model.Document.CanUndo);

            model.Execute("ZOOMFACTOR");
            Assert.Equal("Enter new value for ZOOMFACTOR <90>:", model.Prompt);
            model.Execute("60");
            Assert.Equal(1.5, model.Settings.WheelZoomFactor, 9);

            // A step Global settings holds that ZOOMFACTOR cannot reach is read as the nearest it can.
            model.Settings.WheelZoomFactor = 3.0;
            model.Execute("ZOOMFACTOR");
            Assert.Equal("Enter new value for ZOOMFACTOR <100>:", model.Prompt);
        }
    }
}
