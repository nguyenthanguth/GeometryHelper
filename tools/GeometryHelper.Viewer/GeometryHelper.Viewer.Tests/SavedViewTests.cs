using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class SavedViewTests
    {
        /// <summary>
        /// The eye is a point, so its place has the rounding of its largest coordinate, which a direction over a short distance feels: a camera 0.01 from a
        /// target 10 000 from the origin has its direction to a few parts in 1E-10. The tolerance is the given one, and that.
        /// </summary>
        private static void AssertSameLook(ViewCamera expected, ViewCamera actual, double tolerance)
        {
            double carried = tolerance + 4E-16 * (expected.Target.Length + expected.Distance) / expected.Distance;

            Assert.Equal(expected.Orthographic, actual.Orthographic);
            Assert.True((expected.Target - actual.Target).Length <= tolerance * Math.Max(1.0, expected.Target.Length), "targets " + expected.Target + " and " + actual.Target);
            Assert.True(Math.Abs(expected.Distance - actual.Distance) <= carried * expected.Distance, "distances " + expected.Distance + " and " + actual.Distance);
            Assert.True((expected.Forward - actual.Forward).Length <= carried, "directions " + expected.Forward + " and " + actual.Forward + ", " + (expected.Forward - actual.Forward).Length + " apart");

            // The top of the screen turns with the azimuth, which the line of sight tells the worse the nearer it is to vertical; a thousandth of a unit sideways
            // and nearer, the top of the screen is what is used and is exact.
            double sideways = Math.Sqrt(expected.Forward.X * expected.Forward.X + expected.Forward.Y * expected.Forward.Y);
            Assert.True((expected.Up - actual.Up).Length <= carried / Math.Max(sideways, 1E-3), "ups " + expected.Up + " and " + actual.Up);
        }

        [Fact]
        public void ACameraSavedAsAView_AndTakenBack_LooksTheSameWay_ForAnyCameraAndAnyShapeOfWindow()
        {
            var random = new Random(20260519);

            for (int i = 0; i < 500; i++)
            {
                var camera = new ViewCamera
                {
                    Target = new Vec3((random.NextDouble() - 0.5) * 20000.0, (random.NextDouble() - 0.5) * 20000.0, (random.NextDouble() - 0.5) * 2000.0),
                    Distance = Math.Pow(10.0, random.NextDouble() * 7.0 - 3.0),
                    Azimuth = (random.NextDouble() - 0.5) * 12.0,
                    Elevation = (random.NextDouble() - 0.5) * Math.PI,
                    Orthographic = random.Next(2) == 0,
                };
                double aspect = 0.4 + random.NextDouble() * 3.0;

                SavedView view = SavedView.FromCamera("random " + i, camera, aspect);
                ViewCamera back = view.ToCamera(aspect);

                AssertSameLook(camera, back, 1E-12);
            }
        }

        [Theory]
        [InlineData(StandardView.Top)]
        [InlineData(StandardView.Front)]
        [InlineData(StandardView.Right)]
        [InlineData(StandardView.Iso)]
        public void TheStandardViews_AreTakenBackAsThemselves_TheTopAndBottomToo(StandardView standard)
        {
            var camera = new ViewCamera { Target = new Vec3(1234.5, -6789.25, 12.5), Distance = 250.0 };
            camera.SetView(standard);

            AssertSameLook(camera, SavedView.FromCamera("standard", camera, 1.6).ToCamera(1.6), 1E-12);

            // Straight up, from below.
            camera.Elevation = -Math.PI / 2.0;
            camera.Azimuth = 0.7;
            AssertSameLook(camera, SavedView.FromCamera("below", camera, 1.6).ToCamera(1.6), 1E-12);
        }

        [Fact]
        public void AViewLookingStraightDown_TakesTheTopOfTheScreenFromTheUpThatWasGiven()
        {
            var view = new SavedView("plan", new Vec3(0, 0, 100), default(Vec3), new Vec3(1, 0, 0), true, 50.0);

            ViewCamera camera = view.ToCamera(1.0);

            // The top of the screen points along +X: the camera came from the -X side.
            Assert.True((camera.Up - new Vec3(1, 0, 0)).Length < 1E-9, camera.Up.ToString());
            Assert.True((camera.Forward - new Vec3(0, 0, -1)).Length < 1E-9);
        }

        [Fact]
        public void AnythingBut_StraightUpOrDown_IgnoresTheUp_ForTheCameraNeverRolls()
        {
            var tilted = new SavedView("tilted", new Vec3(100, 0, 50), default(Vec3), new Vec3(5, 5, 0.1), false, 0.0);
            var plain = new SavedView("plain", new Vec3(100, 0, 50), default(Vec3), new Vec3(0, 0, 1), false, 0.0);

            AssertSameLook(plain.ToCamera(1.5), tilted.ToCamera(1.5), 1E-14);
            Assert.True(Math.Abs(tilted.ToCamera(1.5).Up.Z) > 0.0);
        }

        [Fact]
        public void TheWidthOfAnOrthographicView_IsTheWidthOnThePlaneThroughTheTarget_InAnyWindow()
        {
            var view = new SavedView("spot", new Vec3(0, -500, 0), default(Vec3), new Vec3(0, 0, 1), true, 50.0);

            foreach (double aspect in new[] { 0.5, 1.0, 1.5, 2.5 })
            {
                ViewCamera camera = view.ToCamera(aspect);
                Assert.Equal(50.0, 2.0 * camera.Distance * camera.HalfHeightRatio * aspect, 9);
            }

            // A view with no width takes the distance from the eye as its scale, in perspective always.
            ViewCamera byDistance = new SavedView("far", new Vec3(0, -500, 0), default(Vec3), new Vec3(0, 0, 1), true, 0.0).ToCamera(1.5);
            Assert.Equal(500.0, byDistance.Distance, 9);
            ViewCamera perspective = new SavedView("persp", new Vec3(0, -500, 0), default(Vec3), new Vec3(0, 0, 1), false, 50.0).ToCamera(1.5);
            Assert.Equal(500.0, perspective.Distance, 9);
            Assert.False(perspective.Orthographic);
        }

        [Fact]
        public void AViewThatIsNotOne_IsRefusedWithAMessage()
        {
            var z = new Vec3(0, 0, 1);

            Assert.Throws<ArgumentException>(() => new SavedView("", default(Vec3), z, z, false, 0.0));
            Assert.Throws<ArgumentException>(() => new SavedView("   ", default(Vec3), z, z, false, 0.0));
            Assert.Throws<ArgumentException>(() => new SavedView("same", z, z, z, false, 0.0));
            Assert.Throws<ArgumentException>(() => new SavedView("nan", default(Vec3), new Vec3(double.NaN, 0, 0), z, false, 0.0));
            Assert.Throws<ArgumentException>(() => new SavedView("narrow", default(Vec3), z, z, true, -1.0));
            Assert.Throws<ArgumentNullException>(() => SavedView.FromCamera("none", null, 1.0));
        }

        [Fact]
        public void TheViewsOfAScene_ArriveFromTheFileAsTheyWereWritten_ToTheBit()
        {
            var writer = new SceneWriter("a repro");
            writer.Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1)), layer: "x");
            writer.View("the spike", new GeoPoint3(40.1, -60.2, 30.3), new GeoPoint3(40.123456789012345, 0.1, 1E-300), new GeoVector3(0, 0, 1));
            writer.View("plan of it", new GeoPoint3(40, 0, 500), new GeoPoint3(40, 0, 0), new GeoVector3(0, 1, 0), ortho: true, width: 20.000000000000004);

            Scene read = SceneReader.Read(writer.ToJson());

            Assert.Equal(2, read.Views.Count);
            Assert.Equal(new[] { "the spike", "plan of it" }, read.Views.Select(v => v.Name));

            for (int i = 0; i < 2; i++)
            {
                SavedView made = writer.Scene.Views[i];
                SavedView got = read.Views[i];
                Assert.Equal(made.Eye, got.Eye);
                Assert.Equal(made.Target, got.Target);
                Assert.Equal(made.Up, got.Up);
                Assert.Equal(made.Orthographic, got.Orthographic);
                Assert.Equal(made.Width, got.Width);
            }

            Assert.True(read.Views[1].Orthographic);
            Assert.Equal(20.000000000000004, read.Views[1].Width);

            // And written again it is the same text.
            Assert.Equal(writer.ToJson(), new SceneWriter(read).ToJson());
        }

        [Fact]
        public void ASceneWithNoViews_IsWrittenWithoutTheList_SoItIsTheFileItAlwaysWas_AndAnOldFileReadsFine()
        {
            var writer = new SceneWriter("old");
            writer.Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1)), layer: "x", name: "line");

            string json = writer.ToJson();

            Assert.DoesNotContain("views", json, StringComparison.Ordinal);
            Scene read = SceneReader.Read(json);
            Assert.Empty(read.Views);
            Assert.Single(read.Items);
            Assert.Equal(json, new SceneWriter(read).ToJson());
        }

        [Fact]
        public void AViewOfTheSameNameReplacesTheOneBefore_AndTheOrderIsTheOrderOfTheFirst()
        {
            var writer = new SceneWriter();
            writer.View("a", new GeoPoint3(0, 0, 10), new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 0));
            writer.View("b", new GeoPoint3(0, 10, 0), new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));
            writer.View("a", new GeoPoint3(10, 0, 0), new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));

            Assert.Equal(new[] { "a", "b" }, writer.Scene.Views.Select(v => v.Name));
            Assert.Equal(new Vec3(10, 0, 0), writer.Scene.Views[0].Eye);
            Assert.Throws<ArgumentException>(() => writer.View("bad", new GeoPoint3(1, 1, 1), new GeoPoint3(1, 1, 1), new GeoVector3(0, 0, 1)));
            Assert.Equal(2, writer.Scene.Views.Count);
        }

        [Fact]
        public void AViewThatIsNotInTheFileTheWayItShouldBe_IsLeftOutWithAWarningNamingIt_AndTheRestOfTheSceneIsKept()
        {
            const string bad = "{\"format\":\"ghscene\",\"version\":1,\"views\":[{\"name\":\"ok\",\"eye\":[0,0,5],\"target\":[0,0,0],\"up\":[0,1,0]},{\"name\":\"\",\"eye\":[0,0,5],\"target\":[0,0,0],\"up\":[0,1,0]},{\"name\":\"no eye\",\"target\":[0,0,0],\"up\":[0,1,0]},{\"name\":\"on it\",\"eye\":[1,1,1],\"target\":[1,1,1],\"up\":[0,1,0]},{\"name\":\"also ok\",\"eye\":[0,5,0],\"target\":[0,0,0],\"up\":[0,0,1]}],\"items\":[]}";

            Scene scene = SceneReader.Read(bad);

            Assert.Equal(new[] { "ok", "also ok" }, scene.Views.Select(v => v.Name));
            Assert.Equal(3, scene.LoadWarnings.Count);
            Assert.Contains("view 1", scene.LoadWarnings[0], StringComparison.Ordinal);
            Assert.Contains("view 2", scene.LoadWarnings[1], StringComparison.Ordinal);
            Assert.Contains("view 3", scene.LoadWarnings[2], StringComparison.Ordinal);
            Assert.DoesNotContain("Parameter", scene.LoadWarnings[0], StringComparison.Ordinal);

            // The warnings are about reading, and are not written again.
            Assert.DoesNotContain("warning", new SceneWriter(scene).ToJson(), StringComparison.Ordinal);

            // A view with no 'ortho' and no 'width' is a view with perspective, and a scene with good views has no warning.
            Scene fine = SceneReader.Read("{\"format\":\"ghscene\",\"version\":1,\"views\":[{\"name\":\"ok\",\"eye\":[0,0,5],\"target\":[0,0,0],\"up\":[0,1,0]}],\"items\":[]}");
            Assert.False(fine.Views[0].Orthographic);
            Assert.Equal(0.0, fine.Views[0].Width);
            Assert.Empty(fine.LoadWarnings);
        }
        [Fact]
        public void AComparison_OpensAtTheViewTheBuildUnderTestSaved_ElseAtTheOthers()
        {
            Scene Make(string viewName)
            {
                var writer = new SceneWriter("side");
                writer.Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1)), layer: "x", name: "line");

                if (viewName != null)
                {
                    writer.View(viewName, new GeoPoint3(0, 0, 10), new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 0));
                }

                return writer.Scene;
            }

            Scene Compared(Scene left, Scene right) => ComparisonScene.Build(SceneComparison.Compare(left, right), left, right).Scene;

            // What the window applies is the camera the carried view gives: the same look as the view of the build under test, not just the same list.
            Scene withRight = Compared(Make("left"), Make("right"));
            ViewCamera applied = Assert.Single(withRight.Views).ToCamera(1.5);
            ViewCamera expected = new SceneWriter().View("right", new GeoPoint3(0, 0, 10), new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 0)).Scene.Views[0].ToCamera(1.5);
            Assert.Equal(expected.Target, applied.Target);
            Assert.Equal(expected.Distance, applied.Distance, 12);
            Assert.True((expected.Forward - applied.Forward).Length < 1E-12);
            Assert.True((expected.Up - applied.Up).Length < 1E-12);
            Assert.Equal(10.0, applied.Distance, 12);

            Assert.Equal("right", Assert.Single(withRight.Views).Name);
            Assert.Equal("left", Assert.Single(Compared(Make("left"), Make(null)).Views).Name);
            Assert.Empty(Compared(Make(null), Make(null)).Views);
        }

        [Fact]
        public void TheViewsOfAScene_AreWrittenInTheFileOnTheirOwnLines_BetweenTheLayersAndTheItems()
        {
            var writer = new SceneWriter("lines");
            writer.Layer("a", "#112233");
            writer.View("v", new GeoPoint3(0, 0, 10), new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 0), true, 12.5);
            writer.Add(new GeoPoint3(1, 2, 3), layer: "a");

            string[] lines = writer.ToJson().Split('\n');
            int views = Array.FindIndex(lines, l => l.StartsWith("\"views\"", StringComparison.Ordinal));
            int items = Array.FindIndex(lines, l => l.StartsWith("\"items\"", StringComparison.Ordinal));

            Assert.True(views > 0 && views < items);
            Assert.Equal("{\"name\":\"v\",\"eye\":[0,0,10],\"target\":[0,0,0],\"up\":[0,1,0],\"ortho\":true,\"width\":12.5}", lines[views + 1]);
            Assert.DoesNotContain("\r", writer.ToJson(), StringComparison.Ordinal);
        }
    }
}
