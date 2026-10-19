using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The views saved in the window, which a script run again must not lose, and the gate that keeps work started for one holding of the elements of a view out of the next.
    /// </summary>
    public class ViewKeepingTests
    {
        private static SavedView View(string name, double x = 1.0) => new SavedView(name, new Vec3(x, -60.1, 30.7), new Vec3(x, 0.1, 0.3), new Vec3(0, 0, 1), name.StartsWith("o", StringComparison.Ordinal), 20.123456789012345);

        [Fact]
        public void TheFileOfAScript_IsBesideItAndNamedForIt()
        {
            Assert.Equal(Path.Combine("a", "box.csx.views.json"), SavedViewsFile.PathFor(Path.Combine("a", "box.csx")));
            Assert.Throws<ArgumentException>(() => SavedViewsFile.PathFor(" "));
        }

        [Fact]
        public void ViewsKeptInAFile_ComeBackToTheBit_InTheOrderTheyWereKept()
        {
            using (var folder = new TempFolder())
            {
                string path = folder.Combine("s.csx.views.json");
                var warnings = new List<string>();

                SavedViewsFile.Keep(path, View("first", 0.1 + 0.2), warnings);
                SavedViewsFile.Keep(path, View("ortho second", 1E-300), warnings);
                SavedViewsFile.Keep(path, View("first", 7.0), warnings);

                List<SavedView> back = SavedViewsFile.Read(path, warnings);

                Assert.Empty(warnings);
                Assert.Equal(new[] { "first", "ortho second" }, back.Select(v => v.Name));
                Assert.Equal(7.0, back[0].Eye.X);
                Assert.Equal(1E-300, back[1].Eye.X);
                Assert.True(back[1].Orthographic);
                Assert.Equal(20.123456789012345, back[1].Width);
                Assert.Equal(BitConverter.DoubleToInt64Bits(-60.1), BitConverter.DoubleToInt64Bits(back[0].Eye.Y));
            }
        }

        [Fact]
        public void ASceneMadeAgainByAScript_GetsBackTheViewsSavedInTheWindow_AfterItsOwn()
        {
            using (var folder = new TempFolder())
            {
                string path = folder.Combine("s.csx.views.json");
                var warnings = new List<string>();
                SavedViewsFile.Keep(path, View("mine"), warnings);
                SavedViewsFile.Keep(path, View("the spike", 99.0), warnings);

                // The script makes its scene again, with two views of its own; one has a name the window saved a view under.
                SceneWriter writer = new SceneWriter().Add(new GeoPoint3(0, 0, 0));
                writer.View("the spike", new GeoPoint3(1, -60, 30), new GeoPoint3(1, 0, 0), new GeoVector3(0, 0, 1));
                writer.View("plan", new GeoPoint3(0, 0, 100), new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 0));
                Scene scene = writer.Scene;

                SavedViewsFile.Merge(scene, SavedViewsFile.Read(path, warnings));

                // The scene opens at its first view, the script's, and the view of the window with the same name takes its place there.
                Assert.Equal(new[] { "the spike", "plan", "mine" }, scene.Views.Select(v => v.Name));
                Assert.Equal(99.0, scene.Views[0].Eye.X);
            }
        }

        [Fact]
        public void ANoFile_GivesNoViews_AndAFileThatIsNotJson_IsSaidSoAndLeavesNone()
        {
            using (var folder = new TempFolder())
            {
                var warnings = new List<string>();

                Assert.Empty(SavedViewsFile.Read(folder.Combine("none.views.json"), warnings));
                Assert.Empty(warnings);

                string bad = folder.Combine("bad.views.json");
                File.WriteAllText(bad, "{ not json");
                Assert.Empty(SavedViewsFile.Read(bad, warnings));
                Assert.Contains("could not be read", Assert.Single(warnings));
            }
        }

        [Fact]
        public void KeepingAViewInAFileThatCannotBeRead_LeavesTheFileAsItWas_AndSaysSo()
        {
            using (var folder = new TempFolder())
            {
                string path = folder.Combine("s.csx.views.json");
                SavedViewsFile.Write(path, new[] { View("a"), View("b") });
                string good = File.ReadAllText(path);

                // A file cut short, as a writer stopped half way leaves it: views a and b are in it still, to be mended by hand.
                string cut = good.Substring(0, good.Length - 5);
                File.WriteAllText(path, cut);
                var warnings = new List<string>();

                bool kept = SavedViewsFile.Keep(path, View("c"), warnings);

                Assert.False(kept);
                Assert.Equal(cut, File.ReadAllText(path));
                Assert.Contains(warnings, w => w.Contains("could not be read"));
                Assert.Contains(warnings, w => w.Contains("was left as it is") && w.Contains("'c'"));
                Assert.Equal(new[] { Path.GetFileName(path) }, Directory.GetFiles(folder.Path).Select(Path.GetFileName));
            }
        }

        [Fact]
        public void KeepingAViewInAFileThatIsJsonButNotAFileOfViews_LeavesItAsItWas()
        {
            using (var folder = new TempFolder())
            {
                string path = folder.Combine("s.csx.views.json");
                File.WriteAllText(path, "{\"something\":\"else\"}");

                Assert.False(SavedViewsFile.Keep(path, View("c"), new List<string>()));
                Assert.Equal("{\"something\":\"else\"}", File.ReadAllText(path));
            }
        }

        [Fact]
        public void KeepingAViewInAFileThatIsMended_WritesItAfterTheOthers_AndLeavesNoTemporaryFileBeside()
        {
            using (var folder = new TempFolder())
            {
                string path = folder.Combine("s.csx.views.json");
                SavedViewsFile.Write(path, new[] { View("a"), View("b") });

                Assert.True(SavedViewsFile.Keep(path, View("c"), new List<string>()));

                Assert.Equal(new[] { "a", "b", "c" }, SavedViewsFile.Read(path, new List<string>()).Select(v => v.Name));
                Assert.Equal(new[] { Path.GetFileName(path) }, Directory.GetFiles(folder.Path).Select(Path.GetFileName));
            }
        }

        [Fact]
        public void AViewInTheFileThatIsNotOne_IsLeftOutWithItsNumber_AndTheOthersAreRead()
        {
            using (var folder = new TempFolder())
            {
                string path = folder.Combine("s.csx.views.json");
                File.WriteAllText(path, "{\"views\":[{\"name\":\"\",\"eye\":[0,0,1],\"target\":[0,0,0],\"up\":[0,1,0]},{\"name\":\"ok\",\"eye\":[0,0,1],\"target\":[0,0,0],\"up\":[0,1,0]}]}");
                var warnings = new List<string>();

                List<SavedView> views = SavedViewsFile.Read(path, warnings);

                Assert.Equal("ok", Assert.Single(views).Name);
                Assert.Contains("view 0 of s.csx.views.json is not a view", Assert.Single(warnings));
            }
        }

        [Fact]
        public void ASceneFileWrittenWithViews_IsTheSameTextAsBeforeTheFileOfViewsWasMade()
        {
            // The views of a scene and the views of a file are written by one routine: a scene file with views is read back to the bit.
            SceneWriter writer = new SceneWriter().Add(new GeoPoint3(0, 0, 0));
            writer.View("a", new GeoPoint3(0.1, 0.2, 0.3), new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 1), true, 12.5);

            Scene back = SceneReader.Read(writer.ToJson());

            SavedView view = Assert.Single(back.Views);
            Assert.Equal(0.1, view.Eye.X);
            Assert.True(view.Orthographic);
            Assert.Equal(12.5, view.Width);
        }

        [Fact]
        public void WorkStartedBeforeAViewLetGo_IsNotCurrentOnceItHasTakenTheElementsAgain()
        {
            var gate = new AttachGate();

            Assert.False(gate.IsAttached);
            Assert.True(gate.Attach());
            int ticket = gate.Ticket;
            Assert.True(gate.IsCurrent(ticket));

            // Let go, and taken again before the work comes back: the elements the work is for are gone, and these are new.
            Assert.True(gate.Detach());
            Assert.False(gate.IsCurrent(ticket));
            Assert.True(gate.Attach());
            Assert.False(gate.IsCurrent(ticket));
            Assert.True(gate.IsCurrent(gate.Ticket));
        }

        [Fact]
        public void TakingTheElementsTwice_OrLettingGoOfNone_ChangesNothing()
        {
            var gate = new AttachGate();

            Assert.False(gate.Detach());
            Assert.True(gate.Attach());
            int ticket = gate.Ticket;
            Assert.False(gate.Attach());
            Assert.True(gate.IsCurrent(ticket));
            Assert.True(gate.Detach());
            Assert.False(gate.Detach());
            Assert.False(gate.IsCurrent(ticket));
            Assert.False(gate.IsCurrent(gate.Ticket));
        }
    }
}
