using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What the diagnostics must not call a fault: valid output of the library read the way the library reads it.
    /// </summary>
    public class DiagnosticsRound2Tests
    {
        public static IEnumerable<object[]> BothWays => new[] { new object[] { true }, new object[] { false } };

        private static GeoSolid3 Box(double x0, double y0, double z0, double x, double y, double z)
        {
            var floor = new GeoPolygon3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x0 + x, y0, z0), new GeoPoint3(x0 + x, y0 + y, z0), new GeoPoint3(x0, y0 + y, z0));
            return GeoSolid3.Extrude(floor, new GeoVector3(0, 0, z));
        }

        private static string FaceJson(params double[] xyz) => "{\"outer\":" + Diag.Pts3(xyz) + ",\"holes\":[]}";

        /// <summary>
        /// A box 10 by 10 by 10 written as the faces a scene holds, with its top cut in two at x = 5 and the right half of the top as wide as asked.
        /// </summary>
        private static SceneItem SplitTopBox(double rightEdge)
            => Diag.Raw(
                "GeoSolid3",
                "{\"faces\":[" + string.Join(
                    ",",
                    FaceJson(0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0, 0),
                    FaceJson(0, 0, 10, 5, 0, 10, 5, 10, 10, 0, 10, 10),
                    FaceJson(5, 0, 10, rightEdge, 0, 10, rightEdge, 10, 10, 5, 10, 10),
                    FaceJson(0, 0, 0, 10, 0, 0, 10, 0, 10, 0, 0, 10),
                    FaceJson(0, 10, 0, 0, 10, 10, 10, 10, 10, 10, 10, 0),
                    FaceJson(0, 0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0),
                    FaceJson(10, 0, 0, 10, 10, 0, 10, 10, 10, 10, 0, 10)) + "]}",
                "split top");

        private static GeoSolid3 Pyramid(int n)
        {
            GeoPoint3[] ring = Enumerable.Range(0, n).Select(i => new GeoPoint3(1000 * Math.Cos(2 * Math.PI * i / n), 1000 * Math.Sin(2 * Math.PI * i / n), 0)).ToArray();
            var apex = new GeoPoint3(0, 0, 500);
            var faces = new List<GeoFace3> { new GeoFace3(new GeoPolygon3(Enumerable.Reverse(ring)), null) };

            for (int i = 0; i < n; i++)
            {
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { ring[i], ring[(i + 1) % n], apex }), null));
            }

            return new GeoSolid3(faces);
        }

        #region Duplicates

        [Fact]
        public void CornersOfTwoFacesThatDifferByRounding_AreOneCorner_NotADuplicate()
        {
            // The corner (10, 0, 10) of the front face is 1E-14 from the same corner of the top face: 5 units in the last place of 10.
            double e = 1E-14;
            string top = FaceJson(0, 0, 10, 10 + e, 0, 10, 10, 10, 10, 0, 10, 10);
            string bottom = FaceJson(0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0, 0);
            string front = FaceJson(0, 0, 0, 10, 0, 0, 10, 0, 10, 0, 0, 10);
            string back = FaceJson(0, 10, 0, 0, 10, 10, 10, 10, 10, 10, 10, 0);
            string left = FaceJson(0, 0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0);
            string right = FaceJson(10, 0, 0, 10, 10, 0, 10, 10, 10, 10, 0, 10);
            SceneItem box = Diag.Raw("GeoSolid3", "{\"faces\":[" + string.Join(",", top, bottom, front, back, left, right) + "]}", "rounded");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(box), DiagnosticChecks.Duplicates, false);

            Assert.Empty(report.Findings);
        }

        [Fact]
        public void CornersFourTenThousandthsApart_AreADuplicate_AFaultAlone_AndAFactOnABodyTheLibraryCallsValid()
        {
            string top = FaceJson(0, 0, 10, 10.0004, 0, 10, 10, 10, 10, 0, 10, 10);
            string bottom = FaceJson(0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0, 0);
            string front = FaceJson(0, 0, 0, 10, 0, 0, 10, 0, 10, 0, 0, 10);
            string back = FaceJson(0, 10, 0, 0, 10, 10, 10, 10, 10, 10, 10, 0);
            string left = FaceJson(0, 0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0);
            string right = FaceJson(10, 0, 0, 10, 10, 0, 10, 10, 10, 10, 0, 10);
            SceneItem box = Diag.Raw("GeoSolid3", "{\"faces\":[" + string.Join(",", top, bottom, front, back, left, right) + "]}", "apart");
            Scene scene = Diag.SceneOf(box);

            DiagnosticFinding alone = Assert.Single(Diag.Run(scene, DiagnosticChecks.Duplicates, false).Findings);
            DiagnosticReport asked = Diag.Run(scene, DiagnosticChecks.Duplicates | DiagnosticChecks.Report, true);

            Assert.True(alone.IsFault);
            Assert.Equal(0.0004, alone.Size, 9);
            Assert.False(Diag.Of(asked, DiagnosticKind.DuplicateVertex).Single().IsFault);
            Assert.Contains("the library welds them", Diag.Of(asked, DiagnosticKind.DuplicateVertex).Single().Message);
        }

        [Fact]
        public void ABodyOf300BooleansThatTheLibraryCallsValid_HasNoDuplicateFault()
        {
            var rnd = new Random(12345);
            Tolerance tol = Tolerance.Default;
            int bodies = 0;
            int faults = 0;

            for (int n = 0; n < 300; n++)
            {
                GeoSolid3 a = Box(0, 0, 0, 100 + rnd.Next(100), 100 + rnd.Next(100), 50 + rnd.Next(50));
                GeoSolid3 b = Box(-20 + rnd.NextDouble() * 100, -20 + rnd.NextDouble() * 100, -10 + rnd.NextDouble() * 60, 20 + rnd.NextDouble() * 80, 20 + rnd.NextDouble() * 80, 20 + rnd.NextDouble() * 80);

                if (n % 2 == 0)
                {
                    b = b.TransformBy(GeoTransform3.RotationAxis(b.GetAabb().Center, new GeoVector3(rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble() + 0.1), rnd.NextDouble() * 3));
                }

                GeoSolid3 r = null;
                bool ok = (n % 3) == 0 ? a.TrySubtract(b, out r, tol) : (n % 3) == 1 ? a.TryUnion(b, out r, tol) : (r = a.Intersect(b, tol).FirstOrDefault()) != null;

                if (!ok || r == null || !r.Validate(tol).IsValid)
                {
                    continue;
                }

                bodies++;
                DiagnosticReport report = Diag.Run(new SceneWriter().Add(r, name: "b" + n).Scene, DiagnosticChecks.Duplicates | DiagnosticChecks.Report, true);
                faults += report.Findings.Count(f => f.IsFault);
            }

            Assert.True(bodies > 200, bodies.ToString());
            Assert.Equal(0, faults);
        }

        [Fact]
        public void ACornerThatSixteenThousandFacesShare_IsOnePointToTheSweep_AndTheCheckIsQuick()
        {
            Scene scene = new SceneWriter().Add(Pyramid(16000), name: "cone").Scene;
            Primitives primitives = Primitives.Build(scene);
            var clock = System.Diagnostics.Stopwatch.StartNew();

            DiagnosticReport report = SceneDiagnostics.Run(scene, primitives, null, null, new DiagnosticOptions { Checks = DiagnosticChecks.Duplicates, UseLibrary = false }, CancellationToken.None);

            clock.Stop();
            Assert.Empty(report.Findings);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1.0), clock.Elapsed.ToString());
        }

        #endregion

        #region Holes, flatness, notes

        [Fact]
        public void ATwistedQuad_IsNotFlatByTheDistanceTheLibraryMeasuresFromThePlaneThroughItsFirstCorner()
        {
            double dz = 0.0035;
            SceneItem quad = Diag.Face3("twisted", 0, 0, 0, 10, 0, 0, 10, 10, dz, 0, 10, 0);

            DiagnosticFinding f = Assert.Single(Diag.Run(Diag.SceneOf(quad), DiagnosticChecks.Flatness).Findings);

            // The plane through (0,0,0) with the right-hand normal (-dz/20, -dz/20, 1): the corner (10,0,0) is dz/2 off it.
            Assert.Equal(dz / 2.0, f.Size, 6);
            Assert.Contains("first corner", f.Message);
            Assert.Contains("first corner", f.Source);
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, dz), new GeoPoint3(0, 10, 0) }, Tolerance.Default));
        }

        [Fact]
        public void ABodyTheLibraryRefusesForAFaceOffFlat_NamesTheFaceThatMadeItRefuse()
        {
            double dz = 0.0035;
            string body = "{\"faces\":[" + string.Join(
                ",",
                FaceJson(0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0, 0),
                FaceJson(0, 0, 10, 10, 0, 10, 10, 10, 10 + dz, 0, 10, 10),
                FaceJson(0, 0, 0, 10, 0, 0, 10, 0, 10, 0, 0, 10),
                FaceJson(10, 0, 0, 10, 10, 0, 10, 10, 10 + dz, 10, 0, 10),
                FaceJson(10, 10, 0, 0, 10, 0, 0, 10, 10, 10, 10, 10 + dz),
                FaceJson(0, 10, 0, 0, 0, 0, 0, 0, 10, 0, 10, 10)) + "]}";

            // Flatness is not ticked: the body is refused, and the face that made it refuse is named all the same.
            DiagnosticReport report = Diag.Run(Diag.SceneOf(Diag.Raw("GeoSolid3", body, "warped box")), DiagnosticChecks.Edges | DiagnosticChecks.Report, true);

            Assert.Contains(report.Findings, f => f.Kind == DiagnosticKind.Note && f.Message.Contains("refused the body"));
            DiagnosticFinding flat = Assert.Single(Diag.Of(report, DiagnosticKind.NotFlat));
            Assert.Contains("face 2", flat.Message);
            Assert.Contains("the library refuses a face like this", flat.Message);
            Assert.Equal(dz / 2.0, flat.Size, 6);
        }

        [Fact]
        public void AHoleThatCrossesItsOutline_IsTwoCrossings_AndOneInsideIsNone()
        {
            SceneItem crossing = Diag.Raw("GeoFace2", "{\"outer\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 0, 10) + ",\"holes\":[" + Diag.Pts2(8, 8, 12, 8, 12, 12, 8, 12) + "]}", "crossing");
            SceneItem inside = Diag.Raw("GeoFace2", "{\"outer\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 0, 10) + ",\"holes\":[" + Diag.Pts2(2, 2, 4, 2, 4, 4, 2, 4) + "]}", "inside");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(crossing, inside), DiagnosticChecks.Crossings);

            Assert.Equal(2, report.Findings.Count);
            Assert.All(report.Findings, f =>
            {
                Assert.Equal("crossing", f.ItemName);
                Assert.Contains("hole 1", f.Message);
                Assert.Contains("outer", f.Message);
                Assert.Contains("cross at", f.Message);
            });
            Assert.Equal(new[] { new Vec3(8, 10, 0), new Vec3(10, 8, 0) }, report.Findings.Select(f => f.At).OrderBy(p => p.X).ToArray());
        }

        [Fact]
        public void TwoHolesThatTouchEachOther_AreATouch()
        {
            SceneItem item = Diag.Raw("GeoFace2", "{\"outer\":" + Diag.Pts2(0, 0, 20, 0, 20, 20, 0, 20) + ",\"holes\":[" + Diag.Pts2(2, 2, 6, 2, 6, 6, 2, 6) + "," + Diag.Pts2(6, 4, 10, 4, 10, 8, 6, 8) + "]}", "touching holes");

            // The holes share the stretch of x = 6 from y = 4 to y = 6: a corner of each stands on the other.
            DiagnosticReport report = Diag.Run(Diag.SceneOf(item), DiagnosticChecks.Crossings);

            Assert.Equal(new[] { new Vec3(6, 4, 0), new Vec3(6, 6, 0) }, report.Findings.Select(f => f.At).OrderBy(p => p.Y).ToArray());
            Assert.All(report.Findings, f =>
            {
                Assert.Contains("hole 1", f.Message);
                Assert.Contains("hole 2", f.Message);
                Assert.Contains("touch", f.Message);
            });
        }

        [Fact]
        public void ASliverFaceOfABody_SaysTheLibrarysRule_AverageWidthUnderEqualPoint()
        {
            // A plate 100 by 100 and 0.0004 thick: each side face is 100 by 0.0004, 0.0004 wide on average.
            double z = 0.0004;
            SceneItem plate = Diag.Raw(
                "GeoSolid3",
                "{\"faces\":[" + string.Join(
                    ",",
                    FaceJson(0, 0, 0, 0, 100, 0, 100, 100, 0, 100, 0, 0),
                    FaceJson(0, 0, z, 100, 0, z, 100, 100, z, 0, 100, z),
                    FaceJson(0, 0, 0, 100, 0, 0, 100, 0, z, 0, 0, z),
                    FaceJson(0, 100, 0, 0, 100, z, 100, 100, z, 100, 100, 0),
                    FaceJson(0, 0, 0, 0, 0, z, 0, 100, z, 0, 100, 0),
                    FaceJson(100, 0, 0, 100, 100, 0, 100, 100, z, 100, 0, z)) + "]}",
                "plate");

            DiagnosticFinding f = Diag.Run(Diag.SceneOf(plate), DiagnosticChecks.Slivers).Findings.First(x => x.Kind == DiagnosticKind.Sliver);

            Assert.Contains("twice its area over its perimeter", f.Message);
            Assert.Contains("at most half of EqualPoint times its perimeter", f.Message);
            Assert.Equal(0.0004, f.Size, 6);
        }

        #endregion

        #region Edges

        [Theory]
        [MemberData(nameof(BothWays))]
        public void TwoBlocksMeetingAlongAnEdge_AreANonManifoldEdgeMagenta_ThatIsAFactOnAValidBody(bool library)
        {
            GeoSolid3 a = Box(0, 0, 0, 10, 10, 10);
            GeoSolid3 b = Box(10, 10, 0, 10, 10, 10);
            Scene scene = new SceneWriter().Add(new GeoSolid3(a.Faces.Concat(b.Faces)), name: "kissing").Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Edges | DiagnosticChecks.Report, library);

            DiagnosticFinding edge = Assert.Single(Diag.Of(report, DiagnosticKind.NonManifoldEdge));
            Assert.False(edge.IsFault);
            Assert.Equal(10.0, edge.Size, 9);
            Assert.DoesNotContain(report.Findings, f => f.IsFault);
            Assert.Equal(1, Diag.Strokes(report, DiagnosticColors.NonManifold));
        }

        [Fact]
        public void ATopCutInTwo_IsTwoTJunctionsWithNoOpenEdge_InTheViewersCountAsInTheLibrarys()
        {
            Scene scene = Diag.SceneOf(SplitTopBox(10.0));

            DiagnosticReport own = Diag.Run(scene, DiagnosticChecks.Edges | DiagnosticChecks.Report, false);
            DiagnosticReport library = Diag.Run(scene, DiagnosticChecks.Edges | DiagnosticChecks.Report, true);

            Assert.Equal(0, Diag.Count(own, DiagnosticKind.OpenEdge));
            Assert.Equal(2, Diag.Count(own, DiagnosticKind.TJunction));
            Assert.DoesNotContain(own.Findings, f => f.IsFault);
            Assert.All(Diag.Of(own, DiagnosticKind.TJunction), f => Assert.Equal(10.0, f.Size, 9));
            Assert.Equal(2, Diag.Strokes(own, DiagnosticColors.TJunction));
            Assert.DoesNotContain(library.Findings, f => f.IsFault);
            Assert.Contains("says valid", Diag.Of(library, DiagnosticKind.Report).Single().Message);
        }

        [Fact]
        public void ATopCutInTwoThatLeavesAStripUncovered_StaysOpen_InBothReadings()
        {
            Scene scene = Diag.SceneOf(SplitTopBox(9.0));

            DiagnosticReport own = Diag.Run(scene, DiagnosticChecks.Edges, false);
            DiagnosticReport library = Diag.Run(scene, DiagnosticChecks.Edges, true);

            Assert.True(Diag.Count(own, DiagnosticKind.OpenEdge) > 0);
            Assert.Equal(0, Diag.Count(own, DiagnosticKind.TJunction));
            Assert.True(Diag.Count(library, DiagnosticKind.OpenEdge) > 0);
        }

        [Fact]
        public void ABodyOfFourHundredGridUnionsAndCuts_HasNoOpenEdgeFaultInTheViewersCount_AndAgreesWithValidate()
        {
            var rnd = new Random(777);
            Tolerance tol = Tolerance.Default;
            int bodies = 0;
            int disagree = 0;
            int ownFaults = 0;

            for (int n = 0; n < 400; n++)
            {
                GeoSolid3 a = Box(0, 0, 0, 10 * (5 + rnd.Next(5)), 10 * (5 + rnd.Next(5)), 10 * (3 + rnd.Next(5)));
                GeoSolid3 b = Box(10 * rnd.Next(-2, 8), 10 * rnd.Next(-2, 8), 10 * rnd.Next(-2, 6), 10 * (1 + rnd.Next(6)), 10 * (1 + rnd.Next(6)), 10 * (1 + rnd.Next(9)));
                GeoSolid3 r;
                bool ok = n % 2 == 0 ? a.TrySubtract(b, out r, tol) : a.TryUnion(b, out r, tol);

                if (!ok || r == null)
                {
                    continue;
                }

                if (n % 3 == 0)
                {
                    GeoSolid3 c = Box(10 * rnd.Next(0, 8), 10 * rnd.Next(0, 8), -5, 10 * (1 + rnd.Next(3)), 10 * (1 + rnd.Next(3)), 200);

                    if (r.TrySubtract(c, out GeoSolid3 r2, tol) && r2 != null)
                    {
                        r = r2;
                    }
                }

                bodies++;
                Scene scene = new SceneWriter().Add(r, name: "b" + n).Scene;
                int library = Diag.Run(scene, DiagnosticChecks.Edges, true).Findings.Count(f => f.Check == DiagnosticChecks.Edges && f.IsFault);
                int own = Diag.Run(scene, DiagnosticChecks.Edges, false).Findings.Count(f => f.Check == DiagnosticChecks.Edges && f.IsFault);
                ownFaults += own;

                if ((library == 0) != (own == 0))
                {
                    disagree++;
                }
            }

            Assert.True(bodies > 300, bodies.ToString());
            Assert.Equal(0, ownFaults);
            Assert.Equal(0, disagree);
        }

        [Theory]
        [MemberData(nameof(BothWays))]
        public void SideFacesCutAtThirdsUnderATopWholeAndATopCopyCutInHalves_AreFourOddStretches_AsValidateReadsThem(bool library)
        {
            // Along each top edge the side face runs one way over 3 pieces, and two tops run the other: 1 one way against 2 the other on every
            // piece, the same three faces on all of them, so one stretch of 3 faces a side. Covering the edge end to end is not meeting it.
            double a = 20.0 / 3.0;
            double b = 40.0 / 3.0;
            SceneItem box = Diag.Raw(
                "GeoSolid3",
                "{\"faces\":[" + string.Join(
                    ",",
                    FaceJson(0, 0, 0, 0, 20, 0, 20, 20, 0, 20, 0, 0),
                    FaceJson(0, 0, 20, 20, 0, 20, 20, 20, 20, 0, 20, 20),
                    FaceJson(0, 0, 20, 10, 0, 20, 20, 0, 20, 20, 10, 20, 20, 20, 20, 10, 20, 20, 0, 20, 20, 0, 10, 20),
                    FaceJson(0, 0, 0, 20, 0, 0, 20, 0, 20, b, 0, 20, a, 0, 20, 0, 0, 20),
                    FaceJson(0, 20, 0, 0, 20, 20, a, 20, 20, b, 20, 20, 20, 20, 20, 20, 20, 0),
                    FaceJson(0, 0, 0, 0, 0, 20, 0, a, 20, 0, b, 20, 0, 20, 20, 0, 20, 0),
                    FaceJson(20, 0, 0, 20, 20, 0, 20, 20, 20, 20, b, 20, 20, a, 20, 20, 0, 20)) + "]}",
                "double top");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(box), DiagnosticChecks.Edges | DiagnosticChecks.Report, library);

            Assert.Equal(4, Diag.Count(report, DiagnosticKind.NonManifoldEdge));
            Assert.Equal(0, Diag.Count(report, DiagnosticKind.TJunction));
            Assert.True(Diag.Count(report, DiagnosticKind.OpenEdge) == 0, string.Join(" | ", Diag.Of(report, DiagnosticKind.OpenEdge)));
            Assert.All(Diag.Of(report, DiagnosticKind.NonManifoldEdge), f =>
            {
                Assert.True(f.IsFault);
                Assert.Equal(20.0, f.Size, 9);
                Assert.Contains("used by 3 faces", f.Message);
            });

            if (library)
            {
                Assert.Contains("not valid", Diag.Of(report, DiagnosticKind.Report).Single().Message);
            }
        }

        [Fact]
        public void TwoFacesRunningTheSameWayAlongALineCutAtItsMiddle_AreSameWayStretches_NotOpenOnes()
        {
            // Front face of a box with its top edge cut at the middle, and a second top face wound the same way as the front's own neighbour:
            // along the front top edge, two pieces each run twice the same way.
            string front = FaceJson(0, 0, 0, 10, 0, 0, 10, 0, 10, 5, 0, 10, 0, 0, 10);
            string again = FaceJson(10, 0, 10, 5, 0, 10, 0, 0, 10, 0, 5, 12);
            string other = FaceJson(10, 0, 10, 0, 0, 10, 0, 5, 12);
            string sideways = FaceJson(0, 0, 0, 0, 0, 10, 0, 5, 12);
            Scene scene = Diag.SceneOf(Diag.Raw("GeoSolid3", "{\"faces\":[" + string.Join(",", front, again, other, sideways) + "]}", "same way"));

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Edges, false);

            Assert.Contains(report.Findings, f => f.Kind == DiagnosticKind.InconsistentWinding && f.Message.Contains("run the same way"));
        }

        [Fact]
        public void AtAToleranceOfNought_AnArcJoinedToASegmentStillMeetsOnlyWhereTheyJoin()
        {
            foreach (double bulge in new[] { 0.1, 0.3, 0.55, -0.4, 0.9 })
            {
                SceneItem item = Diag.Raw("GeoPolygonArc2", "{\"points\":" + Diag.Pts2(0, 0, 40, 0, 40, 10, 0, 10) + ",\"bulges\":[" + Diag.N(bulge) + ",0,0,0]}", "arc");
                Scene scene = Diag.SceneOf(item);
                scene.EqualPoint = 0.0;

                Assert.Empty(Diag.Run(scene, DiagnosticChecks.Crossings, false).Findings);
            }
        }

        [Fact]
        public void NeedleFactsOfAnItem_AreOneLineWithTheCountAndTheNarrowest_AndFaultsStayListed()
        {
            Scene scene = new SceneWriter().Add(Pyramid(4000), name: "cone").Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Needles, false);

            DiagnosticFinding line = Assert.Single(report.Findings);
            Assert.False(line.IsFault);
            Assert.Contains("the narrowest is", line.Message);
            Assert.Contains("4000 vertices", line.Message);
        }

        [Fact]
        public void ASliverLoopIsReadAsNeedlesAtItsCorners_WhenTheSliversCheckIsNotOn()
        {
            SceneItem sliver = Diag.Raw("GeoTriangle2", "{\"points\":" + Diag.Pts2(60, -60, 90, -60, 75, -59.9995) + "}", "sliver");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(sliver), DiagnosticChecks.Needles);

            Assert.Equal(2, Diag.Count(report, DiagnosticKind.Needle));
        }

        [Fact]
        public void TheVerdictOnDuplicates_DoesNotDependOnWhichOtherChecksAreTicked()
        {
            string top = FaceJson(0, 0, 10, 10.0004, 0, 10, 10, 10, 10, 0, 10, 10);
            string bottom = FaceJson(0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0, 0);
            string front = FaceJson(0, 0, 0, 10, 0, 0, 10, 0, 10, 0, 0, 10);
            string back = FaceJson(0, 10, 0, 0, 10, 10, 10, 10, 10, 10, 10, 0);
            string left = FaceJson(0, 0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0);
            string right = FaceJson(10, 0, 0, 10, 10, 0, 10, 10, 10, 10, 0, 10);
            Scene scene = Diag.SceneOf(Diag.Raw("GeoSolid3", "{\"faces\":[" + string.Join(",", top, bottom, front, back, left, right) + "]}", "apart"));

            DiagnosticReport alone = Diag.Run(scene, DiagnosticChecks.Duplicates, true);
            DiagnosticReport with = Diag.Run(scene, DiagnosticChecks.Duplicates | DiagnosticChecks.Report, true);

            Assert.False(Diag.Of(alone, DiagnosticKind.DuplicateVertex).Single().IsFault);
            Assert.False(Diag.Of(with, DiagnosticKind.DuplicateVertex).Single().IsFault);
        }
        #endregion

        #region Needles

        [Fact]
        public void ASpikeIsAFaultOnlyWhereItIsNoWiderThanEqualPointAtItsEnd_AndAFactWhereItIsWiderThough_ItTurnsBackJustAsFar()
        {
            Func<double, DiagnosticFinding> run = half => Assert.Single(Diag.Run(
                Diag.SceneOf(Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 5 + half, 10, 5, 30, 5 - half, 10, 0, 10) + "}", "spiked")),
                DiagnosticChecks.Needles).Findings);

            DiagnosticFinding wide = run(0.01);
            DiagnosticFinding thin = run(0.0004);

            Assert.Equal(DiagnosticKind.Needle, wide.Kind);
            Assert.False(wide.IsFault);
            Assert.Contains("wider than EqualPoint", wide.Message);
            Assert.True(thin.IsFault);
            Assert.Contains("no wider than EqualPoint", thin.Message);
        }

        [Fact]
        public void ThinTrianglesOfAConeTheLibraryCallsValid_AreNeedleFactsAndNoFault()
        {
            // 4 000 faces round an apex, each 1.57 wide at the base and 1 118 long: they turn back by 179.92 degrees at the apex and are 1.57 wide.
            Scene scene = new SceneWriter().Add(Pyramid(4000), name: "cone").Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Needles, false);

            Assert.NotEmpty(report.Findings);
            Assert.DoesNotContain(report.Findings, f => f.IsFault);
        }

        [Fact]
        public void ASliverTriangle_IsOneSliverAndNotAlsoTwoNeedles()
        {
            SceneItem sliver = Diag.Raw("GeoTriangle2", "{\"points\":" + Diag.Pts2(60, -60, 90, -60, 75, -59.9995) + "}", "sliver");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(sliver), DiagnosticChecks.Needles | DiagnosticChecks.Slivers);

            Assert.Equal(DiagnosticKind.Sliver, Assert.Single(report.Findings).Kind);
        }

        [Fact]
        public void ATipGivenTwice_IsStillOneNeedle()
        {
            SceneItem item = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 5, 20, 5, 20, 5, 10, 5.0001, 10, 10, 0, 10) + "}", "doubled tip");

            DiagnosticFinding needle = Assert.Single(Diag.Run(Diag.SceneOf(item), DiagnosticChecks.Needles).Findings);

            Assert.Equal(DiagnosticKind.Needle, needle.Kind);
            Assert.True(needle.IsFault);
            Assert.Equal(new Vec3(20, 5, 0), needle.At);
            Assert.Contains("vertex 4", needle.Message);
        }

        #endregion

        #region Crossings

        [Fact]
        public void AnArcTangentToAnotherEdge_IsATangentTouch_AndOneThatCrossesIsACrossing()
        {
            // The arc from (0,0) to (10,0) bulges down by 1 (a bulge of 0.2 on a chord of 10): the edge along y = -1 touches it at (5,-1), or
            // along y = -0.9 cuts it in two places.
            Func<double, DiagnosticReport> run = y => Diag.Run(
                Diag.SceneOf(Diag.Raw("GeoPolygonArc2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 12, 0, 12, y, -2, y, -2, 0) + ",\"bulges\":[0.2,0,0,0,0,0]}", "arc")),
                DiagnosticChecks.Crossings);

            DiagnosticReport tangent = run(-1.0);
            DiagnosticReport cutting = run(-0.9);

            DiagnosticFinding touch = Assert.Single(tangent.Findings);
            Assert.Contains("touch tangentially", touch.Message);
            Assert.Equal(5.0, touch.At.X, 3);
            Assert.True(touch.IsFault);
            Assert.Equal(2, cutting.Findings.Count);
            Assert.All(cutting.Findings, f => Assert.Contains("cross at", f.Message));
        }

        [Fact]
        public void PointsEachWithinEqualPointOfTheNext_AreOnePlaceWhateverOrderTheyComeIn()
        {
            // Hits at x = 0, 0.0009 and 0.0018: the ends are 0.0018 apart, more than EqualPoint 0.001, but each is within it of the next.
            Scene scene = Diag.SceneOf(Diag.Polygon2("p", 0, 0, 1, 0, 0, 1));
            var run = new DiagnosticRun(scene, Primitives.Build(scene), null, new DiagnosticOptions(), CancellationToken.None);
            var hits = new[] { new Vec3(0.0018, 0, 0), new Vec3(0, 0, 0), new Vec3(0.0009, 0, 0), new Vec3(5, 0, 0), new Vec3(5.0005, 0, 0) };

            int[] cluster = run.Cluster(hits, 0.001);

            Assert.Equal(new[] { 0, 0, 0, 3, 3 }, cluster);
        }
        #endregion

        #region The lens

        [Fact]
        public void TheLensOfACircle_IsCutFromTheExactCurveFineEnoughForItsScale()
        {
            SceneItem a = Diag.Raw("GeoCircle2", "{\"center\":[0,0],\"radius\":2}", "left");
            SceneItem b = Diag.Raw("GeoCircle2", "{\"center\":[4.06,0],\"radius\":2}", "right");
            Scene scene = Diag.SceneOf(a, b);
            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Gap);
            Assert.NotNull(report.Gap);
            ExactGeometry exact = ExactGeometry.Build(scene, Primitives.Build(scene));

            // A pixel is 0.3 mm: the gap, 0.06, is 0.2 pixel, and the lens draws it 100 times larger, 20 pixels.
            GapLens lens = GapMagnifier.Build(exact, report.Gap, 0.3);

            Assert.Equal(100.0, lens.Scale);
            DiagnosticStroke arc = lens.Overlay.Strokes.Where(s => s.Color == "#7A3E9D").OrderByDescending(s => s.Line.Points.Length).First();
            Assert.True(arc.Line.Points.Length >= 6, arc.Line.Points.Length.ToString());
            Vec3 middle = (report.Gap.From + report.Gap.To) * 0.5;
            double allowed = 0.1 * 0.3 / 100.0;

            for (int k = 0; k + 1 < arc.Line.Points.Length; k++)
            {
                Vec3 p = middle + (arc.Line.Points[k] - middle) * (1.0 / 100.0);
                Vec3 q = middle + (arc.Line.Points[k + 1] - middle) * (1.0 / 100.0);

                // Both ends are on the circle, and the middle of the chord is no further inside it than a tenth of a pixel once scaled.
                Assert.Equal(2.0, p.Length, 9);
                Assert.InRange(2.0 - ((p + q) * 0.5).Length, -1E-9, allowed * 1.05);
            }
        }

        #endregion
    }
}
