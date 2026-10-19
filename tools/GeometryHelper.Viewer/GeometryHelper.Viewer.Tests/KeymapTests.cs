using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class KeymapTests
    {
        [Fact]
        public void TheDefaultKeymap_ParsesAndEveryActionHasAGesture()
        {
            Keymap keymap = Keymap.Default();

            foreach (KeymapAction action in KeymapActions.All)
            {
                Assert.NotEmpty(keymap.GesturesOf(action.Name));
            }

            Assert.All(keymap.Bindings, binding => Assert.True(KeymapActions.IsKnown(binding.Action), binding.Action));
            Assert.Equal(KeymapActions.All.Count, keymap.Bindings.Count);
        }

        [Fact]
        public void TheDefaultKeymap_BindsNoGestureTwice()
        {
            List<string> gestures = Keymap.Default().Bindings.SelectMany(b => b.Gestures).Select(g => g.ToString()).ToList();

            Assert.Equal(gestures.Count, gestures.Distinct(StringComparer.Ordinal).Count());
        }

        [Theory]
        [InlineData("Wheel", "ZoomAtCursor")]
        [InlineData("PageUp", "ZoomIn")]
        [InlineData("PageDown", "ZoomOut")]
        [InlineData("MiddleDrag", "Pan")]
        [InlineData("Left", "PanLeft")]
        [InlineData("Ctrl+MiddleDrag", "Rotate")]
        [InlineData("Ctrl+Left", "RotateLeft")]
        [InlineData("V", "SetRotationCentre")]
        [InlineData("Shift+V", "ClearRotationCentre")]
        [InlineData("Ctrl+P", "TogglePlan")]
        [InlineData("F", "FitAll")]
        [InlineData("Shift+F", "FitSelection")]
        [InlineData("Ctrl+0", "ViewIso")]
        [InlineData("Ctrl+5", "ViewTop")]
        [InlineData("Ctrl+6", "ViewFront")]
        [InlineData("Ctrl+7", "ViewRight")]
        [InlineData("Ctrl+1", "RenderWireframe")]
        [InlineData("Ctrl+2", "RenderShaded")]
        [InlineData("Ctrl+3", "RenderShadedEdges")]
        [InlineData("Ctrl+4", "RenderTransparent")]
        [InlineData("Click", "Select")]
        [InlineData("Shift+Click", "SelectAdd")]
        [InlineData("Ctrl+Click", "SelectToggle")]
        [InlineData("Esc", "Cancel")]
        [InlineData("Ctrl+H", "HideSelected")]
        [InlineData("Ctrl+Shift+H", "ShowAll")]
        [InlineData("DoubleClick", "Properties")]
        [InlineData("Alt+Enter", "Properties")]
        [InlineData("F2", "MeasureDistance")]
        [InlineData("F12", "SnapNearest")]
        [InlineData("F5", "RunScript")]
        [InlineData("Ctrl+R", "RerunScript")]
        [InlineData("Ctrl+Shift+S", "Screenshot")]
        public void TheDefaultKeymap_FollowsTheTableOfTheSpec(string gesture, string action)
        {
            Assert.Equal(action, Keymap.Default().ActionFor(Gesture.Parse(gesture)));
        }

        [Fact]
        public void AGesture_IsTheSameWhateverTheCaseOrTheOrderOfItsModifiers()
        {
            Assert.Equal(Gesture.Parse("Ctrl+Shift+H"), Gesture.Parse("shift + CTRL + h"));
            Assert.Equal("Ctrl+Alt+Shift+Esc", Gesture.Parse("Shift+Alt+Ctrl+Escape").ToString());
            Assert.Equal(Gesture.Parse("Click"), Gesture.Parse("LeftClick"));
            Assert.Equal("LeftDrag", Gesture.Parse("leftdrag").ToString());
            Assert.Equal("MiddleDoubleClick", Gesture.Parse("MiddleDoubleClick").ToString());
            Assert.NotEqual(Gesture.Parse("Click"), Gesture.Parse("RightClick"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hyper+H")]
        [InlineData("Ctrl+Ctrl+H")]
        [InlineData("Ctrl+")]
        [InlineData("Ctrl+Banana")]
        [InlineData("F25")]
        public void AGestureThatIsNotOne_IsRefusedWithAMessage(string text)
        {
            FormatException error = Assert.Throws<FormatException>(() => Gesture.Parse(text));

            Assert.False(string.IsNullOrWhiteSpace(error.Message));
            Assert.False(Gesture.TryParse(text, out _, out string problem));
            Assert.False(string.IsNullOrWhiteSpace(problem));
        }

        [Fact]
        public void AKeymapThatBindsOneGestureTwice_IsRefused()
        {
            const string twoActions = "{\"bindings\":[{\"action\":\"Pan\",\"gestures\":[\"Ctrl+P\"]},{\"action\":\"TogglePlan\",\"gestures\":[\"ctrl+p\"]}]}";
            const string oneAction = "{\"bindings\":[{\"action\":\"Pan\",\"gestures\":[\"Ctrl+P\",\"Ctrl+P\"]}]}";
            const string actionTwice = "{\"bindings\":[{\"action\":\"Pan\",\"gestures\":[\"F1\"]},{\"action\":\"Pan\",\"gestures\":[\"F2\"]}]}";
            const string unknown = "{\"bindings\":[{\"action\":\"Levitate\",\"gestures\":[\"F1\"]}]}";

            Assert.Contains("both", Assert.Throws<FormatException>(() => Keymap.Parse(twoActions)).Message, StringComparison.Ordinal);
            Assert.Contains("twice", Assert.Throws<FormatException>(() => Keymap.Parse(oneAction)).Message, StringComparison.Ordinal);
            Assert.Contains("bound twice", Assert.Throws<FormatException>(() => Keymap.Parse(actionTwice)).Message, StringComparison.Ordinal);
            Assert.Contains("Levitate", Assert.Throws<FormatException>(() => Keymap.Parse(unknown)).Message, StringComparison.Ordinal);
            Assert.Throws<FormatException>(() => Keymap.Parse("[]"));
        }

        [Fact]
        public void TheKeymapOfTheUser_WinsForTheActionsItListsAndLeavesTheRestAlone()
        {
            string path = Path.Combine(Path.GetTempPath(), "keymap-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                // Pan moves to the right button, and takes F from FitAll, which is left with no gesture.
                File.WriteAllText(path, "{\"bindings\":[{\"action\":\"Pan\",\"gestures\":[\"RightDrag\",\"F\"]}]}");

                Keymap keymap = Keymap.Load(path, out string problem);

                Assert.Null(problem);
                Assert.Equal("Pan", keymap.ActionFor(Gesture.Parse("RightDrag")));
                Assert.Equal("Pan", keymap.ActionFor(Gesture.Parse("F")));
                Assert.Null(keymap.ActionFor(Gesture.Parse("MiddleDrag")));
                Assert.Equal("ZoomAtCursor", keymap.ActionFor(Gesture.Parse("Wheel")));
                Assert.Empty(keymap.GesturesOf("FitAll"));
                Assert.Equal("FitSelection", keymap.ActionFor(Gesture.Parse("Shift+F")));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void AUserKeymapThatDoesNotRead_FallsBackToTheDefaultAndSaysWhy()
        {
            string path = Path.Combine(Path.GetTempPath(), "keymap-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                File.WriteAllText(path, "{\"bindings\":[{\"action\":\"Pan\",\"gestures\":[\"Ctrl+Pretzel\"]}]}");

                Keymap keymap = Keymap.Load(path, out string problem);

                Assert.Contains("Pretzel", problem, StringComparison.Ordinal);
                Assert.Equal("Pan", keymap.ActionFor(Gesture.Parse("MiddleDrag")));
                Assert.Null(Keymap.Load(Path.Combine(Path.GetTempPath(), "no-such-keymap.json"), out string none).ActionFor(Gesture.Parse("Ctrl+Pretzel".Replace("Pretzel", "Z"))));
                Assert.Null(none);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void EveryActionAKeyIsBoundTo_ThatThisVersionDoesNot_IsMarkedSo()
        {
            // The second phase did the rotation centre, the box select, the measuring and the snaps; the screenshot is the fifth's.
            Assert.Equal(
                new[] { "Screenshot" },
                KeymapActions.All.Where(a => !a.Implemented).Select(a => a.Name).OrderBy(n => n));
        }
    }
}
