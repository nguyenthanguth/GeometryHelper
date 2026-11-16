using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A point modifier chosen from the Shift+right-click menu where AutoCAD 2021 does not take it: the command line writes
    /// the prompt and the word as the menu sends it, then AutoCAD's refusal, as typing the word does.
    /// </summary>
    public sealed class PointModifierRefusalInterfaceTests
    {
        [Fact]
        public void TemporaryTrackPointChosenAtFromsBasePoint_IsRefusedOnTheCommandLine()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"));
            model.StartCommand("LINE");
            model.Execute("0,0");
            model.SnapOverrideMenu().Single(i => i.Header == "From").Command.Execute(null);
            int before = model.History.Count;

            model.SnapOverrideMenu().Single(i => i.Header == "Temporary track point").Command.Execute(null);

            Assert.Equal(
                new[] { "Base point: _tt", "Point or option keyword required." },
                model.History.Skip(before).Take(2)
            );
            Assert.Equal("Base point:", model.Prompt);
        }

        [Fact]
        public void MidBetweenChosenInsideMidBetween_IsRefused_AndTheFirstPromptIsAskedAgain()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"));
            model.StartCommand("LINE");
            model.Execute("0,0");
            model.SnapOverrideMenu().Single(i => i.Header == "Mid Between 2 Points").Command.Execute(null);
            Assert.Equal("First point of mid:", model.Prompt);

            model.SnapOverrideMenu().Single(i => i.Header == "Mid Between 2 Points").Command.Execute(null);

            Assert.Contains("First point of mid: _m2p", model.History);
            Assert.Equal("Point or option keyword required.", model.History.Last());
            Assert.Equal("First point of mid:", model.Prompt);
        }

        [Fact]
        public void AModifierChosenWhereNoPointIsAsked_IsAnsweredByThePromptWaiting()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"));
            model.StartCommand("POLYGON");

            Assert.False(model.ModifyPoint("_tt"));

            Assert.Contains("Enter number of sides <4>: _tt", model.History);
            Assert.Equal("Requires an integer between 3 and 1024.", model.History.Last());
            Assert.Equal("Enter number of sides <4>:", model.Prompt);
        }

        [Fact]
        public void AModifierChosenWhereNoCommandRuns_SaysNothing()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"));
            int before = model.History.Count;

            Assert.False(model.ModifyPoint("_tt"));

            Assert.Equal(before, model.History.Count);
        }
    }
}
