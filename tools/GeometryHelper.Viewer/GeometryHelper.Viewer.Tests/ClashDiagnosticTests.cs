using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GeometryHelper.Clash;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The clash check of the diagnostics: the library's Clash3.Find asked about the pairs of bodies whose boxes meet, with the numbers it gives.
    /// </summary>
    public class ClashDiagnosticTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>
        /// A box with one corner at the given place, built the way the sample shapes are.
        /// </summary>
        private static GeoSolid3 Box(double x, double y, double z, double sx, double sy, double sz)
        {
            var floor = new GeoPolygon3(new GeoPoint3(x, y, z), new GeoPoint3(x + sx, y, z), new GeoPoint3(x + sx, y + sy, z), new GeoPoint3(x, y + sy, z));
            return GeoSolid3.Extrude(floor, new GeoVector3(0, 0, sz));
        }

        private static Scene SceneOf(params GeoSolid3[] bodies)
        {
            var writer = new SceneWriter("clash");

            for (int i = 0; i < bodies.Length; i++)
            {
                writer.Add(bodies[i], layer: "steel", name: "part " + (i + 1).ToString(CultureInfo.InvariantCulture));
            }

            return writer.Scene;
        }

        private static DiagnosticReport Run(Scene scene, params int[] items) => Diag.Run(scene, DiagnosticChecks.Clash, true, items);

        private static string Dump(DiagnosticReport report)
            => string.Join("\n", report.Findings.Select(f => f.ToString() + " @" + f.At + " size " + f.Size.ToString("R", CultureInfo.InvariantCulture) + " other " + f.OtherItemIndex + " strokes " + f.Drawing.Strokes.Count));

        [Fact]
        public void TwoBoxesThatOverlap_ClashWithTheVolumeTheyShare_AndHowDeepTheyGo()
        {
            // 0 to 10 each way, and 5 to 15 along X: they share 5 by 10 by 10.
            Scene scene = SceneOf(Box(0, 0, 0, 10, 10, 10), Box(5, 0, 0, 10, 10, 10));
            DiagnosticReport report = Run(scene);
            DiagnosticFinding clash = Assert.Single(Diag.Of(report, DiagnosticKind.HardClash));

            Assert.True(clash.IsFault);
            Assert.Equal(DiagnosticChecks.Clash, clash.Check);
            Assert.Equal(0, clash.ItemIndex);
            Assert.Equal(1, clash.OtherItemIndex);
            Assert.Equal("part 1", clash.ItemName);
            Assert.Equal(500.0, clash.Size, 6);
            Assert.Equal(7.5, clash.At.X, 6);
            Assert.Equal(5.0, clash.At.Y, 6);
            Assert.Equal(5.0, clash.At.Z, 6);
            Assert.Contains("item 1 'part 1' and item 2 'part 2' clash", clash.Message);
            Assert.Contains("a volume of 500", clash.Message);
            Assert.Contains("1 piece", clash.Message);
            Assert.Contains("5 deep", clash.Message);
            Assert.Contains("Clash3.Find", clash.Source);
            Assert.Empty(Diag.Of(report, DiagnosticKind.TouchingBodies));
            Assert.Empty(Diag.Of(report, DiagnosticKind.Note));
        }

        [Fact]
        public void TheNumbersOfAFinding_AreTheOnesTheLibraryGives()
        {
            GeoSolid3 a = Box(0, 0, 0, 10, 10, 10);
            GeoSolid3 b = Box(5, 3, 2, 10, 10, 10);
            ClashResult expected = Clash3.Find(new[] { a }, new[] { b }, new ClashOptions(0.0, true, 1), Tol).Single();
            DiagnosticFinding clash = Assert.Single(Diag.Of(Run(SceneOf(a, b)), DiagnosticKind.HardClash));

            Assert.Equal(ClashKind.Hard, expected.Kind);
            Assert.Equal(expected.Volume, clash.Size);
            Assert.Equal(expected.Location.X, clash.At.X);
            Assert.Equal(expected.Location.Y, clash.At.Y);
            Assert.Equal(expected.Location.Z, clash.At.Z);
            Assert.Contains(expected.Depth.ToString("G10", CultureInfo.InvariantCulture) + " deep", clash.Message);
            Assert.Contains(expected.Volume.ToString("G10", CultureInfo.InvariantCulture), clash.Message);
            Assert.Equal(5.0 * 7.0 * 8.0, clash.Size, 6);
        }

        [Fact]
        public void TheSharedVolume_IsOutlinedAndMarked_AndNothingElseIsDrawn()
        {
            DiagnosticReport report = Run(SceneOf(Box(0, 0, 0, 10, 10, 10), Box(5, 0, 0, 10, 10, 10)));
            DiagnosticFinding clash = Assert.Single(report.Findings, f => f.Kind == DiagnosticKind.HardClash);

            // The shared volume is a box of six faces, each outlined once; the place is a dot.
            Assert.Equal(6, Diag.Strokes(report, DiagnosticColors.Clash));
            Assert.Equal(report.Overlay.Strokes.Count, Diag.Strokes(report, DiagnosticColors.Clash));
            Assert.Single(report.Overlay.Dots);
            Assert.Equal(clash.At, report.Overlay.Dots[0].At);
            Assert.True(clash.Spot.Max.X - clash.Spot.Min.X >= 10.0, "the spot to zoom to takes in the whole of the shared volume");
        }

        [Fact]
        public void TwoBoxesThatTouchFaceToFace_AreTouching_WithTheAreaTheyShare()
        {
            DiagnosticReport report = Run(SceneOf(Box(0, 0, 0, 10, 10, 10), Box(10, 0, 0, 10, 10, 10)));
            DiagnosticFinding touch = Assert.Single(Diag.Of(report, DiagnosticKind.TouchingBodies));

            Assert.False(touch.IsFault);
            Assert.Empty(Diag.Of(report, DiagnosticKind.HardClash));
            Assert.Equal(100.0, touch.Size, 6);
            Assert.Equal(10.0, touch.At.X, 6);
            Assert.Equal(5.0, touch.At.Y, 6);
            Assert.Equal(5.0, touch.At.Z, 6);
            Assert.Contains("touch face to face over 100", touch.Message);
            Assert.Equal(1, touch.OtherItemIndex);
            Assert.Equal(1, Diag.Strokes(report, DiagnosticColors.Touch));
        }

        [Fact]
        public void TwoBoxesThatMeetOnlyAlongAnEdge_TouchWithNoAreaShared()
        {
            DiagnosticReport report = Run(SceneOf(Box(0, 0, 0, 10, 10, 10), Box(10, 10, 0, 10, 10, 10)));
            DiagnosticFinding touch = Assert.Single(Diag.Of(report, DiagnosticKind.TouchingBodies));

            Assert.Equal(0.0, touch.Size);
            Assert.Contains("along an edge or at a corner", touch.Message);
        }

        [Fact]
        public void TwoBoxesApart_HaveNothingToReport_OfTheirBoxesMeetingOrNot()
        {
            // The second pair has boxes that meet and bodies that do not: an L-shaped body and a cube in its notch.
            DiagnosticReport apart = Run(SceneOf(Box(0, 0, 0, 10, 10, 10), Box(20, 0, 0, 10, 10, 10)));
            Assert.Empty(apart.Findings);

            GeoSolid3 low = Box(0, 0, 0, 30, 10, 10);
            GeoSolid3 tall = Box(0, 0, 10, 10, 10, 20);
            GeoSolid3 inTheNotch = Box(15, 2, 10, 5, 5, 5);
            DiagnosticReport notch = Run(SceneOf(low, tall, inTheNotch));

            // The cube stands on the low part (touching it face to face) and clear of the tall one.
            Assert.Empty(Diag.Of(notch, DiagnosticKind.HardClash));
            Assert.Equal(2, Diag.Of(notch, DiagnosticKind.TouchingBodies).Count());
        }

        [Fact]
        public void OnlyThePairsThatClashAreFound_AmongSeveral()
        {
            Scene scene = SceneOf(
                Box(0, 0, 0, 10, 10, 10),
                Box(5, 0, 0, 10, 10, 10),
                Box(100, 0, 0, 10, 10, 10),
                Box(105, 5, 5, 10, 10, 10));
            DiagnosticReport report = Run(scene);

            Assert.Equal(new[] { Tuple.Create(0, 1), Tuple.Create(2, 3) }, Diag.Of(report, DiagnosticKind.HardClash).Select(f => Tuple.Create(f.ItemIndex, f.OtherItemIndex)).OrderBy(t => t.Item1).ToArray());
            Assert.Equal(new[] { 500.0, 5.0 * 5.0 * 5.0 }, Diag.Of(report, DiagnosticKind.HardClash).OrderBy(f => f.ItemIndex).Select(f => Math.Round(f.Size, 6)).ToArray());
        }

        [Fact]
        public void OneItemSelected_IsCheckedAgainstTheRestOfTheScene_AndTwoOrMoreAgainstEachOther()
        {
            // A overlaps B, and B overlaps C, but A and C are apart.
            Scene scene = SceneOf(Box(0, 0, 0, 10, 10, 10), Box(5, 0, 0, 10, 10, 10), Box(12, 0, 0, 10, 10, 10));

            Assert.Equal(new[] { Tuple.Create(0, 1), Tuple.Create(1, 2) }, Pairs(Run(scene)));
            Assert.Equal(new[] { Tuple.Create(0, 1) }, Pairs(Run(scene, 0)));
            Assert.Equal(new[] { Tuple.Create(0, 1), Tuple.Create(1, 2) }, Pairs(Run(scene, 1)));
            Assert.Equal(new[] { Tuple.Create(1, 2) }, Pairs(Run(scene, 1, 2)));
            Assert.Empty(Pairs(Run(scene, 0, 2)));
        }

        private static Tuple<int, int>[] Pairs(DiagnosticReport report)
            => Diag.Of(report, DiagnosticKind.HardClash).Select(f => Tuple.Create(f.ItemIndex, f.OtherItemIndex)).OrderBy(t => t.Item1).ThenBy(t => t.Item2).ToArray();

        [Fact]
        public void AFindingIsNumberedAndOrdered_AndTheSameWheneverItIsAsked()
        {
            Scene scene = SceneOf(
                Box(0, 0, 0, 10, 10, 10),
                Box(5, 0, 0, 10, 10, 10),
                Box(8, 4, 4, 10, 10, 10),
                Box(40, 0, 0, 10, 10, 10),
                Box(45, 5, 0, 10, 10, 10));

            string first = Dump(Run(scene));
            string second = Dump(Run(scene));

            Assert.Equal(first, second);
            Assert.Equal(new[] { 1, 2, 3, 4 }, Run(scene).Findings.Select(f => f.Number).ToArray());
        }

        [Fact]
        public void TheSceneInTheOtherOrder_GivesTheSamePairsAndNumbers()
        {
            GeoSolid3[] bodies =
            {
                Box(0, 0, 0, 10, 10, 10),
                Box(5, 0, 0, 10, 10, 10),
                Box(8, 4, 4, 10, 10, 10),
                Box(40, 0, 0, 10, 10, 10),
                Box(45, 5, 0, 10, 10, 10),
            };

            string Summary(DiagnosticReport report, Scene scene)
                => string.Join(";", report.Findings.Where(f => f.Kind == DiagnosticKind.HardClash)
                    .Select(f => string.Join("+", new[] { scene.Items[f.ItemIndex].Name, scene.Items[f.OtherItemIndex].Name }.OrderBy(n => n, StringComparer.Ordinal)) + "=" + f.Size.ToString("0.######", CultureInfo.InvariantCulture))
                    .OrderBy(s => s, StringComparer.Ordinal));

            Scene forward = SceneOf(bodies);
            var writer = new SceneWriter("reverse");

            for (int i = bodies.Length - 1; i >= 0; i--)
            {
                writer.Add(bodies[i], layer: "steel", name: "part " + (i + 1).ToString(CultureInfo.InvariantCulture));
            }

            Assert.Equal(Summary(Run(forward), forward), Summary(Run(writer.Scene), writer.Scene));
            Assert.NotEqual(string.Empty, Summary(Run(forward), forward));
        }

        [Fact]
        public void ManyBodiesInARow_AreFoundByTheirNeighboursAlone()
        {
            // 120 plates 1 thick standing 0.5 apart in X, each 2 deep, so each overlaps the next one only: 119 pairs among 7 140 whose boxes do not meet.
            var bodies = new GeoSolid3[120];

            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i] = Box(i * 1.5, 0, 0, 2, 40, 40);
            }

            DiagnosticReport report = Run(SceneOf(bodies));
            DiagnosticFinding[] clashes = Diag.Of(report, DiagnosticKind.HardClash).ToArray();

            Assert.Equal(119, clashes.Length);
            Assert.All(clashes, f => Assert.Equal(f.ItemIndex + 1, f.OtherItemIndex));
            Assert.All(clashes, f => Assert.Equal(0.5 * 40.0 * 40.0, f.Size, 6));
        }

        [Fact]
        public void PairsPastTheCap_AreNotCheckedAndTheNoteSaysHowMany()
        {
            Scene scene = SceneOf(Box(0, 0, 0, 10, 10, 10), Box(2, 0, 0, 10, 10, 10), Box(4, 0, 0, 10, 10, 10), Box(6, 0, 0, 10, 10, 10));
            Primitives primitives = Primitives.Build(scene);
            var options = new DiagnosticOptions { Checks = DiagnosticChecks.Clash, MaxClashPairs = 2 };
            DiagnosticReport report = SceneDiagnostics.Run(scene, primitives, null, null, options, CancellationToken.None);

            Assert.Equal(2, Diag.Of(report, DiagnosticKind.HardClash).Count());
            DiagnosticFinding note = Assert.Single(Diag.Of(report, DiagnosticKind.Note));
            Assert.Contains("4 of 6 pairs of bodies whose boxes meet are not checked", note.Message);
            Assert.Contains("only the first 2", note.Message);
        }

        [Fact]
        public void ARunThatIsCancelled_Stops()
        {
            Scene scene = SceneOf(Box(0, 0, 0, 10, 10, 10), Box(5, 0, 0, 10, 10, 10));
            Primitives primitives = Primitives.Build(scene);

            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.Throws<OperationCanceledException>(() => SceneDiagnostics.Run(scene, primitives, null, null, new DiagnosticOptions { Checks = DiagnosticChecks.Clash }, source.Token));
            }
        }

        [Fact]
        public void BoxesAndMeshesAreBodiesToo_AndAnythingElseIsCountedOnceAndLeftOut()
        {
            var writer = new SceneWriter("mixed");
            writer.Add(Box(0, 0, 0, 10, 10, 10), name: "solid");
            writer.Add(new GeoAabb3(new GeoPoint3(5, 0, 0), new GeoPoint3(15, 10, 10)), name: "aabb");
            writer.Add(new GeoObb3(new GeoPoint3(12, 5, 5), 10, 10, 10, new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0)), name: "obb");
            writer.Add(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0), new GeoPoint3(5, 5, 0)), name: "plate");
            writer.Add(new GeoPoint3(1, 1, 1), name: "dot");

            DiagnosticReport report = Run(writer.Scene);

            Assert.Equal(new[] { Tuple.Create(0, 1), Tuple.Create(0, 2), Tuple.Create(1, 2) }, Pairs(report));
            DiagnosticFinding note = Assert.Single(Diag.Of(report, DiagnosticKind.Note));
            Assert.Contains("2 items are not a body", note.Message);
        }

        [Fact]
        public void BodiesReadFromAnObjFile_ClashAsClosedMeshes_AndAnOpenMeshIsLeftOutAndSaid()
        {
            var obj = new ObjWriter().Add(Box(0, 0, 0, 10, 10, 10), Tol, "a").Add(Box(5, 0, 0, 10, 10, 10), Tol, "b");
            Scene scene = SceneImport.ReadText(obj.ToString(), "obj");
            DiagnosticReport report = Run(scene);
            DiagnosticFinding clash = Assert.Single(Diag.Of(report, DiagnosticKind.HardClash));

            Assert.Equal(500.0, clash.Size, 6);
            Assert.Equal("a", clash.ItemName);
            Assert.Equal(1, clash.OtherItemIndex);

            // The same two with the top of the second left out.
            string open = obj.ToString();
            var lines = open.Split('\n').ToList();
            int lastFace = lines.FindLastIndex(l => l.StartsWith("f ", StringComparison.Ordinal));
            lines.RemoveAt(lastFace);
            Scene broken = SceneImport.ReadText(string.Join("\n", lines), "obj");
            DiagnosticReport brokenReport = Run(broken);

            Assert.Empty(Diag.Of(brokenReport, DiagnosticKind.HardClash));
            Assert.Contains(Diag.Of(brokenReport, DiagnosticKind.Note), n => n.Message.Contains("the mesh is not closed"));
        }

        [Fact]
        public void BodiesReadFromADxfFile_ClashToo()
        {
            var dxf = new DxfWriter().Add(Box(0, 0, 0, 10, 10, 10), Tol, "a").Add(Box(5, 0, 0, 10, 10, 10), Tol, "b");
            Scene scene = SceneImport.ReadText(dxf.ToString(), "dxf");

            Assert.Equal(2, scene.Items.Count);
            Assert.Equal(500.0, Assert.Single(Diag.Of(Run(scene), DiagnosticKind.HardClash)).Size, 6);
        }

        [Fact]
        public void WithTheLibraryLeftOut_TheClashIsNotMade_AndSaysSo()
        {
            Scene scene = SceneOf(Box(0, 0, 0, 10, 10, 10), Box(5, 0, 0, 10, 10, 10));
            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Clash, useLibrary: false);
            DiagnosticFinding note = Assert.Single(report.Findings);

            Assert.Equal(DiagnosticKind.Note, note.Kind);
            Assert.Contains("the library's alone", note.Message);
        }

        [Fact]
        public void LessThanTwoBodies_IsSaid()
        {
            DiagnosticReport report = Run(SceneOf(Box(0, 0, 0, 10, 10, 10)));
            DiagnosticFinding note = Assert.Single(report.Findings);

            Assert.Equal(DiagnosticKind.Note, note.Kind);
            Assert.Contains("needs two bodies", note.Message);
        }

        [Fact]
        public void TheCheckIsNamedClash_IsPartOfAll_AndNotOfTheDefaults()
        {
            Assert.True(DiagnosticNames.TryParse("clash", out DiagnosticChecks clash));
            Assert.Equal(DiagnosticChecks.Clash, clash);
            Assert.NotEqual(DiagnosticChecks.None, DiagnosticChecks.All & DiagnosticChecks.Clash);
            Assert.Equal(DiagnosticChecks.None, new DiagnosticOptions().Checks & DiagnosticChecks.Clash);
            Assert.Contains(DiagnosticNames.All, pair => pair.Value == DiagnosticChecks.Clash);
        }

        [Fact]
        public void TheBodiesTheLibraryRefuses_AreLeftOutAndSaid_AndTheRestAreChecked()
        {
            Scene scene = SceneOf(Box(0, 0, 0, 10, 10, 10), Box(5, 0, 0, 10, 10, 10));

            // A solid whose data is not a solid at all.
            scene.Layers.Add(new Layer { Name = "default", Color = "#1F77B4" });
            scene.Items.Add(Diag.Raw("GeoSolid3", "{\"faces\":[]}", "empty body"));
            DiagnosticReport report = Run(scene);

            Assert.Single(Diag.Of(report, DiagnosticKind.HardClash));
            Assert.Contains(Diag.Of(report, DiagnosticKind.Note), n => n.ItemName == "empty body");
        }
    }
}
