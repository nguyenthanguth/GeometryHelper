using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class DiagnosticsTests
    {
        private const DiagnosticChecks Edges = DiagnosticChecks.Edges;

        // The edges of a body are read twice, by the library and by the viewer's own count, and the two have to agree on what they find.
        public static IEnumerable<object[]> BothWays => new[] { new object[] { true }, new object[] { false } };

        #region Edges of a body

        [Theory]
        [MemberData(nameof(BothWays))]
        public void ABoxWithItsTopLeftOut_HasFourOpenEdgesRed(bool library)
        {
            Scene scene = new SceneWriter().Add(Diag.OpenBox(), name: "lidless").Scene;

            DiagnosticReport report = Diag.Run(scene, Edges, library);

            Assert.Equal(4, Diag.Count(report, DiagnosticKind.OpenEdge));
            Assert.Equal(0, Diag.Count(report, DiagnosticKind.NonManifoldEdge) + Diag.Count(report, DiagnosticKind.InconsistentWinding));
            Assert.All(Diag.Of(report, DiagnosticKind.OpenEdge), f =>
            {
                Assert.True(f.IsFault);
                Assert.Equal(10.0, f.Size, 9);
                Assert.Equal(10.0, f.At.Z, 9);
            });
            Assert.Equal(4, Diag.Strokes(report, DiagnosticColors.Open));
        }

        [Fact]
        public void TheOpenEdgesOfABox_NameTheLibraryMemberThatGaveThem_WhenTheLibraryIsAsked()
        {
            Scene scene = new SceneWriter().Add(Diag.OpenBox(), name: "lidless").Scene;

            DiagnosticFinding asked = Diag.Of(Diag.Run(scene, Edges, true), DiagnosticKind.OpenEdge).First();
            DiagnosticFinding own = Diag.Of(Diag.Run(scene, Edges, false), DiagnosticKind.OpenEdge).First();

            Assert.Contains("GeoSolid3.Validate()", asked.Source);
            Assert.Contains("viewer", own.Source);
        }

        [Theory]
        [MemberData(nameof(BothWays))]
        public void AClosedBox_HasNoEdgeFault(bool library)
        {
            Scene scene = new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false), name: "box").Scene;

            DiagnosticReport report = Diag.Run(scene, Edges | DiagnosticChecks.Report, library);

            Assert.DoesNotContain(report.Findings, f => f.IsFault);
        }

        [Theory]
        [MemberData(nameof(BothWays))]
        public void ABoxWithOneFaceTurnedOver_HasFourInconsistentWindingEdgesOrange(bool library)
        {
            Scene scene = new SceneWriter().Add(Diag.TurnedBox(), name: "turned").Scene;

            DiagnosticReport report = Diag.Run(scene, Edges, library);

            Assert.Equal(4, Diag.Count(report, DiagnosticKind.InconsistentWinding));
            Assert.Equal(0, Diag.Count(report, DiagnosticKind.OpenEdge));
            Assert.Equal(0, Diag.Count(report, DiagnosticKind.NonManifoldEdge));
            Assert.All(Diag.Of(report, DiagnosticKind.InconsistentWinding), f => Assert.Equal(10.0, f.Size, 9));
            Assert.Equal(4, Diag.Strokes(report, DiagnosticColors.Winding));
        }

        [Theory]
        [MemberData(nameof(BothWays))]
        public void ThreeFacesOnOneEdge_AreOneNonManifoldEdgeMagenta_AndTheFinHasThreeOpenEdges(bool library)
        {
            Scene scene = new SceneWriter().Add(Diag.FinnedBox(), name: "finned").Scene;

            DiagnosticReport report = Diag.Run(scene, Edges, library);

            DiagnosticFinding fin = Assert.Single(Diag.Of(report, DiagnosticKind.NonManifoldEdge));
            Assert.Equal(5.0, fin.At.X, 9);
            Assert.Equal(0.0, fin.At.Y, 9);
            Assert.Equal(10.0, fin.At.Z, 9);
            Assert.Equal(3, Diag.Count(report, DiagnosticKind.OpenEdge));
            Assert.Equal(0, Diag.Count(report, DiagnosticKind.InconsistentWinding));
            Assert.Equal(1, Diag.Strokes(report, DiagnosticColors.NonManifold));
        }

        [Fact]
        public void TheLibraryAndTheViewersCount_FindTheSameEdgesInTheSameOrder()
        {
            foreach (GeoSolid3 body in new[] { Diag.OpenBox(), Diag.TurnedBox(), Diag.FinnedBox() })
            {
                Scene scene = new SceneWriter().Add(body, name: "body").Scene;
                var library = Diag.Run(scene, Edges, true).Findings.Select(f => (f.Kind, f.At, f.Size)).ToList();
                var own = Diag.Run(scene, Edges, false).Findings.Select(f => (f.Kind, f.At, f.Size)).ToList();

                Assert.Equal(library, own);
            }
        }

        [Fact]
        public void ABodyWoundInwards_IsReportedNotValidWithTheSignOfItsVolume()
        {
            GeoSolid3 box = SampleShapes.Box(30, 20, 10, false);
            GeoSolid3 inside = new GeoSolid3(box.Faces.Select(f => new GeoFace3(new GeoPolygon3(f.Boundary.Vertices.Reverse()), null)));
            Scene scene = new SceneWriter().Add(inside, name: "inside out").Scene;

            DiagnosticFinding report = Assert.Single(Diag.Of(Diag.Run(scene, DiagnosticChecks.Report), DiagnosticKind.Report));

            Assert.True(report.IsFault);
            Assert.Contains("not valid", report.Message);
            Assert.Contains("wound inwards", report.Message);
            Assert.Equal(-6000.0, report.Size, 6);
            Assert.Contains("Validate()", report.Source);
        }

        [Fact]
        public void AClosedBox_IsReportedValidWithItsVolume()
        {
            Scene scene = new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false), name: "box").Scene;

            DiagnosticFinding report = Assert.Single(Diag.Of(Diag.Run(scene, DiagnosticChecks.Report), DiagnosticKind.Report));

            Assert.False(report.IsFault);
            Assert.Contains("says valid", report.Message);
            Assert.Contains("IsClosed() yes", report.Message);
            Assert.Contains("GetVolume() 6000", report.Message);
        }

        [Fact]
        public void TheOpeningsOfABody_AreCheckedAsBodiesOfTheirOwn()
        {
            GeoSolid3 plate = SampleShapes.Box(30, 20, 10, true);
            Scene scene = new SceneWriter().Add(plate, name: "plate").Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Report);

            Assert.Equal(2, Diag.Count(report, DiagnosticKind.Report));
            Assert.Contains(report.Findings, f => f.Message.StartsWith("opening 1:", StringComparison.Ordinal));
        }

        #endregion

        #region Meshes

        [Fact]
        public void TheBoundaryOfAMesh_IsOpenEdgesThatAreNotFaults_AndMatchesTheLibrary()
        {
            GeoMesh3 mesh = new GeoPolygon3(SampleShapes.LShape().Select(p => new GeoPoint3(p.X, p.Y, 7.5))).ToMesh(MeshKind.Convex);
            Scene scene = new SceneWriter().Add(mesh, name: "plate").Scene;

            DiagnosticReport report = Diag.Run(scene, Edges);

            Assert.Equal(mesh.GetBoundaryEdges().Length, Diag.Count(report, DiagnosticKind.OpenEdge));
            Assert.All(report.Findings, f => Assert.False(f.IsFault));
            Assert.Contains("viewer", report.Findings[0].Source);
        }

        [Fact]
        public void TwoTrianglesOfAMeshSharingTheirEdgeTheSameWay_AreOneInconsistentWinding()
        {
            string points = Diag.Pts3(0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0);
            SceneItem ok = Diag.Raw("GeoMesh3", "{\"kind\":\"Convex\",\"points\":" + points + ",\"faces\":[[0,1,2],[0,2,3]]}", "ok");
            SceneItem flipped = Diag.Raw("GeoMesh3", "{\"kind\":\"Convex\",\"points\":" + points + ",\"faces\":[[0,1,2],[2,3,0]]}", "rotated");
            SceneItem sameWay = Diag.Raw("GeoMesh3", "{\"kind\":\"Convex\",\"points\":" + points + ",\"faces\":[[0,1,2],[0,3,2]]}", "same way 2");

            Assert.Equal(0, Diag.Count(Diag.Run(Diag.SceneOf(ok), Edges), DiagnosticKind.InconsistentWinding));
            Assert.Equal(0, Diag.Count(Diag.Run(Diag.SceneOf(flipped), Edges), DiagnosticKind.InconsistentWinding));

            DiagnosticReport report = Diag.Run(Diag.SceneOf(sameWay), Edges);

            Assert.Equal(1, Diag.Count(report, DiagnosticKind.InconsistentWinding));
            Assert.Equal(4, Diag.Count(report, DiagnosticKind.OpenEdge));
        }

        [Fact]
        public void TwoVerticesOfAMeshWithinEqualPointThatAreNotJoined_AreADuplicateAndTheMeshStaysOpenNowhereElse()
        {
            // Two triangles that meet at corners a ten-thousandth apart, each with its own vertex there.
            string points = Diag.Pts3(0, 0, 0, 1, 0, 0, 0, 1, 0, 1.0001, 0, 0, 1.0001, 1, 0, 0.0001, 1, 0);
            SceneItem mesh = Diag.Raw("GeoMesh3", "{\"kind\":\"Convex\",\"points\":" + points + ",\"faces\":[[0,1,2],[3,4,5]]}", "two");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(mesh), DiagnosticChecks.Duplicates);

            Assert.Equal(2, Diag.Count(report, DiagnosticKind.DuplicateVertex));
            Assert.Contains(report.Findings, f => f.Message.Contains("vertices 2 and 4") && f.Message.Contains("are not joined"));
        }

        #endregion

        #region Loops

        [Theory]
        [MemberData(nameof(BothWays))]
        public void ABowTieLoop_HasOneCrossingAtItsExactPoint(bool library)
        {
            Scene scene = new SceneWriter().Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(2, 2), new GeoPoint2(2, 0), new GeoPoint2(0, 2)), name: "bow tie").Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Crossings | DiagnosticChecks.Slivers, library);

            DiagnosticFinding crossing = Assert.Single(report.Findings);
            Assert.Equal(DiagnosticKind.SelfCrossing, crossing.Kind);
            Assert.True(crossing.IsFault);
            Assert.True((crossing.At - new Vec3(1, 1, 0)).Length < 1E-12, crossing.At.ToString());
            Assert.Contains("cross", crossing.Message);
            Assert.Single(report.Overlay.Dots);

            if (library)
            {
                Assert.Contains("GeoPolygon2.IsSimple(tolerance) agrees", crossing.Source);
            }
        }

        [Fact]
        public void ASimpleLoop_HasNoCrossing_AndAnAreaOfNoSignOfASliver()
        {
            Scene scene = new SceneWriter().Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(2, 0), new GeoPoint2(2, 2), new GeoPoint2(0, 2)), name: "square").Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Crossings | DiagnosticChecks.Slivers | DiagnosticChecks.Needles | DiagnosticChecks.Duplicates);

            Assert.Empty(report.Findings);
        }

        [Fact]
        public void TwoArcsThatBulgeIntoEachOtherAcrossAnUncrossedChordLoop_CrossAtTwoPlaces()
        {
            // The chords make a rectangle 10 by 6; the arcs of the bottom and the top edges each rise 4 into it (a bulge of 0.8 on a chord of
            // 10) and meet where the circle of radius 5.125 is 3 up, 3.041 either side of the middle.
            SceneItem item = Diag.Raw("GeoPolygonArc2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 6, 0, 6) + ",\"bulges\":[-0.8,0,-0.8,0]}", "arcs");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(item), DiagnosticChecks.Crossings);

            Assert.Equal(2, Diag.Count(report, DiagnosticKind.SelfCrossing));
            Assert.All(report.Findings, f => Assert.Equal(3.0, f.At.Y, 9));
            Assert.Equal(10.0, report.Findings.Sum(f => f.At.X), 9);
            Assert.All(report.Findings, f => Assert.Equal(9.25, (f.At.X - 5.0) * (f.At.X - 5.0), 6));
            Assert.Contains("GeoPolygonArc2.IsSimple(tolerance)", report.Findings[0].Source);
        }

        [Fact]
        public void ACrossingThatIsATouchOfAVertexOnAnEdge_IsSaidToTouch()
        {
            // The vertex (5, 0.0005) stands half a thousandth above the bottom edge: within EqualPoint of it, so it meets it.
            SceneItem item = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 5, 0.0005, 0, 10) + "}", "touch");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(item), DiagnosticChecks.Crossings);

            DiagnosticFinding touch = Assert.Single(report.Findings);
            Assert.Contains("touch", touch.Message);
            Assert.Equal(5.0, touch.At.X, 3);
        }

        [Theory]
        [MemberData(nameof(BothWays))]
        public void ANeedleSpike_IsOneNeedleAtItsTip(bool library)
        {
            // A square 10 by 10 with a spike 20 high and 0.02 wide at its base standing on the top edge.
            SceneItem item = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 5.01, 10, 5, 30, 4.99, 10, 0, 10) + "}", "spiked");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(item), DiagnosticChecks.Needles | DiagnosticChecks.Crossings, library);

            DiagnosticFinding needle = Assert.Single(report.Findings);
            Assert.Equal(DiagnosticKind.Needle, needle.Kind);
            Assert.Equal(new Vec3(5, 30, 0), needle.At);
            Assert.InRange(needle.Size, 179.9, 180.0);
            Assert.Contains("vertex 5", needle.Message);
        }

        [Fact]
        public void ASpikeTwoTenthsWideAtItsBase_IsNotANeedle_AndOneTwoHundredthsWideIs()
        {
            Func<double, DiagnosticReport> run = half => Diag.Run(
                Diag.SceneOf(Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 5 + half, 10, 5, 30, 5 - half, 10, 0, 10) + "}", "spiked")),
                DiagnosticChecks.Needles);

            Assert.Empty(run(0.1).Findings);
            Assert.Single(run(0.01).Findings);
        }

        [Fact]
        public void AVertexACloseBeforeAnother_IsOneDuplicateVertex_AndNotACrossing()
        {
            // The corner (10, 0) is given twice, 0.0004 apart: closer than EqualPoint, 0.001.
            SceneItem item = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 0.0004, 10, 10, 0, 10) + "}", "doubled");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(item), DiagnosticChecks.Duplicates | DiagnosticChecks.Crossings | DiagnosticChecks.Needles);

            DiagnosticFinding dup = Assert.Single(report.Findings);
            Assert.Equal(DiagnosticKind.DuplicateVertex, dup.Kind);
            Assert.Equal(0.0004, dup.Size, 12);
            Assert.Contains("vertices 2 and 3 (neighbours)", dup.Message);
        }

        [Fact]
        public void AVertexExactlyAtEqualPointFromAnother_IsADuplicate_AndOneJustBeyondIsNot()
        {
            // 0.001 is not representable; the pair is made from the same double so that the distance is the tolerance bit for bit.
            double tol = new Scene().EqualPoint;
            SceneItem within = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, tol, 10, 10, 0, 10) + "}", "within");
            SceneItem beyond = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, tol * 1.01, 10, 10, 0, 10) + "}", "beyond");

            Assert.Equal(1, Diag.Count(Diag.Run(Diag.SceneOf(within), DiagnosticChecks.Duplicates), DiagnosticKind.DuplicateVertex));
            Assert.Equal(0, Diag.Count(Diag.Run(Diag.SceneOf(beyond), DiagnosticChecks.Duplicates), DiagnosticKind.DuplicateVertex));
        }

        [Fact]
        public void ADuplicateRepeatedAtTheEndOfAClosedLoop_IsFoundBetweenTheLastAndTheFirst()
        {
            SceneItem item = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 0, 10, 0, 0) + "}", "closed twice");

            DiagnosticFinding dup = Assert.Single(Diag.Run(Diag.SceneOf(item), DiagnosticChecks.Duplicates).Findings);

            Assert.Contains("vertices 1 and 5 (neighbours)", dup.Message);
            Assert.Equal(0.0, dup.Size);
        }

        [Fact]
        public void ASliverTriangle_IsOneSliverAndAFatOneIsNone()
        {
            SceneItem sliver = Diag.Raw("GeoTriangle2", "{\"points\":" + Diag.Pts2(0, 0, 100, 0, 50, 0.0005) + "}", "sliver");
            SceneItem fat = Diag.Raw("GeoTriangle2", "{\"points\":" + Diag.Pts2(0, 0, 100, 0, 50, 5) + "}", "fat");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(sliver, fat), DiagnosticChecks.Slivers);

            DiagnosticFinding f = Assert.Single(report.Findings);
            Assert.Equal(DiagnosticKind.Sliver, f.Kind);
            Assert.Equal("sliver", f.ItemName);
            Assert.Equal(0.025, f.Size, 12);
            Assert.Single(report.Overlay.Strokes);
        }

        [Fact]
        public void ALoopWithAreaJustUnderEqualPointTimesItsLongestEdge_IsASliver_AndJustOverIsNot()
        {
            // A rectangle 100 by w has area 100 w and a longest edge of 100: a sliver when w is under EqualPoint.
            double tol = new Scene().EqualPoint;
            SceneItem under = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 100, 0, 100, tol * 0.99, 0, tol * 0.99) + "}", "under");
            SceneItem over = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 100, 0, 100, tol * 1.01, 0, tol * 1.01) + "}", "over");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(under, over), DiagnosticChecks.Slivers);

            Assert.Equal("under", Assert.Single(report.Findings).ItemName);
        }

        [Fact]
        public void ALoopOfACircularSegmentWithAnArc_HasItsArcsInItsArea()
        {
            // Two arcs back to back make a lens: a bulge of 0.5 on a chord of 100 on each side, so it is no sliver though the chords have no area.
            SceneItem lens = Diag.Raw("GeoPolygonArc2", "{\"points\":" + Diag.Pts2(0, 0, 100, 0) + ",\"bulges\":[0.5,0.5]}", "lens");

            Assert.Empty(Diag.Run(Diag.SceneOf(lens), DiagnosticChecks.Slivers).Findings);
        }

        [Fact]
        public void ATwoVertexLoopOfNoArcs_IsLeftAloneWithoutAnError()
        {
            SceneItem item = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 100, 0) + "}", "flat");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(item), DiagnosticChecks.All & ~DiagnosticChecks.Gap);

            Assert.DoesNotContain(report.Findings, f => f.Kind == DiagnosticKind.SelfCrossing || f.Kind == DiagnosticKind.Sliver);
        }

        [Fact]
        public void TheWindingOfALoop_IsSaidAndDrawnAsArrowsInTheDirectionItRuns()
        {
            SceneItem ccw = Diag.Polygon2("ccw", 0, 0, 10, 0, 10, 10, 0, 10);
            SceneItem cw = Diag.Polygon2("cw", 0, 0, 0, 10, 10, 10, 10, 0);

            DiagnosticReport report = Diag.Run(Diag.SceneOf(ccw, cw), DiagnosticChecks.Winding);

            Assert.Contains("counter-clockwise", report.Findings.Single(f => f.ItemName == "ccw").Message);
            Assert.Contains("clockwise (signed area -100)", report.Findings.Single(f => f.ItemName == "cw").Message);
            Assert.Contains("GeoPolygon2.SignedArea", report.Findings[0].Source);
            Assert.True(report.Overlay.Strokes.Count >= 8);
            Assert.All(report.Overlay.Strokes, s => Assert.Equal(DiagnosticColors.Direction, s.Color));
        }

        [Fact]
        public void AHoleThatRunsTheSameWayAsItsOutline_IsAFactTheLibraryReads_NotAFault()
        {
            SceneItem good = Diag.Raw("GeoFace2", "{\"outer\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 0, 10) + ",\"holes\":[" + Diag.Pts2(2, 2, 2, 4, 4, 4, 4, 2) + "]}", "good");
            SceneItem bad = Diag.Raw("GeoFace2", "{\"outer\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 0, 10) + ",\"holes\":[" + Diag.Pts2(2, 2, 4, 2, 4, 4, 2, 4) + "]}", "bad");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(good, bad), DiagnosticChecks.Winding);

            Assert.DoesNotContain(report.Findings, f => f.IsFault);
            Assert.Contains(report.Findings, f => f.ItemName == "bad" && f.Message.Contains("a hole runs the same way as its outline"));
            Assert.DoesNotContain(report.Findings, f => f.ItemName == "good" && f.Message.Contains("same way"));
        }

        [Fact]
        public void AFaceWithACornerOffItsPlane_IsNotFlatByTheAmountItIsOff()
        {
            SceneItem flat = Diag.Face3("flat", 0, 0, 0, 10, 0, 0, 10, 10, 0, 0, 10, 0);
            SceneItem warped = Diag.Face3("warped", 0, 0, 0, 10, 0, 0, 10, 10, 0.01, 0, 10, 0);

            DiagnosticReport report = Diag.Run(Diag.SceneOf(flat, warped), DiagnosticChecks.Flatness);

            DiagnosticFinding f = Assert.Single(report.Findings);
            Assert.Equal("warped", f.ItemName);
            Assert.Equal(DiagnosticKind.NotFlat, f.Kind);
            Assert.Equal(0.005, f.Size, 6);
        }

        [Fact]
        public void ABodyFaceNotFlat_IsFoundByTheLibraryOrByTheViewer()
        {
            // A box 10 by 10 by 10 whose top face has one corner 0.01 up; the faces are given as the scene holds them.
            string top = Diag.Pts3(0, 0, 10, 10, 0, 10, 10, 10, 10.01, 0, 10, 10);
            string bottom = Diag.Pts3(0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0, 0);
            string front = Diag.Pts3(0, 0, 0, 10, 0, 0, 10, 0, 10, 0, 0, 10);
            string back = Diag.Pts3(0, 10, 0, 0, 10, 10, 10, 10, 10.01, 10, 10, 0);
            string left = Diag.Pts3(0, 0, 0, 0, 0, 10, 0, 10, 10, 0, 10, 0);
            string right = Diag.Pts3(10, 0, 0, 10, 10, 0, 10, 10, 10.01, 10, 0, 10);
            string faces = string.Join(",", new[] { top, bottom, front, back, left, right }.Select(p => "{\"outer\":" + p + ",\"holes\":[]}"));
            SceneItem item = Diag.Raw("GeoSolid3", "{\"faces\":[" + faces + "]}", "warped box");

            DiagnosticReport own = Diag.Run(Diag.SceneOf(item), DiagnosticChecks.Flatness, false);

            Assert.True(Diag.Count(own, DiagnosticKind.NotFlat) >= 1);
            Assert.All(Diag.Of(own, DiagnosticKind.NotFlat), f => Assert.True(f.Size > 0.001));
        }

        [Fact]
        public void TheNormalOfAFaceOfABody_IsDrawnAsAnArrowOutwardsOnAnOutwardWoundBox()
        {
            Scene scene = new SceneWriter().Add(SampleShapes.Box(10, 10, 10, false), name: "box").Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Normals);

            DiagnosticFinding f = Assert.Single(report.Findings);
            Assert.Contains("6 face normals", f.Message);
            Assert.Equal(12, report.Overlay.Strokes.Count);

            // Every shaft starts at the middle of a face and ends outside the box.
            foreach (DiagnosticStroke shaft in report.Overlay.Strokes.Where(s => s.Line.Points.Length == 2))
            {
                Vec3 end = shaft.Line.Points[1];
                bool outside = end.X < 0 || end.X > 10 || end.Y < 0 || end.Y > 10 || end.Z < 0 || end.Z > 10;
                Assert.True(outside, end.ToString());
            }
        }

        #endregion

        #region Gap, spheres, pieces

        [Fact]
        public void TheGapBetweenTwoItems_IsTheLibrarysNumberAndItsShortestSegment()
        {
            SceneItem a = Diag.Polygon2("left", 0, 0, 10, 0, 10, 10, 0, 10);
            SceneItem b = Diag.Polygon2("right", 10.5, 0, 20.5, 0, 20.5, 10, 10.5, 10);

            DiagnosticReport report = Diag.Run(Diag.SceneOf(a, b), DiagnosticChecks.Gap);

            DiagnosticFinding gap = Assert.Single(Diag.Of(report, DiagnosticKind.Gap));
            Assert.Equal(0.5, gap.Size, 12);
            Assert.Contains("DistanceTo", gap.Source);
            Assert.NotNull(report.Gap);
            Assert.Equal(10.0, report.Gap.From.X, 12);
            Assert.Equal(10.5, report.Gap.To.X, 12);
            Assert.Equal(0.5, (report.Gap.To - report.Gap.From).Length, 12);
        }

        [Fact]
        public void TheGapCheckOnOtherThanTwoItems_SaysSoAndDrawsNothing()
        {
            Scene scene = Diag.SceneOf(Diag.Polygon2("a", 0, 0, 1, 0, 1, 1), Diag.Polygon2("b", 5, 0, 6, 0, 6, 1), Diag.Polygon2("c", 9, 0, 10, 0, 10, 1));

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Gap);

            DiagnosticFinding note = Assert.Single(report.Findings);
            Assert.Equal(DiagnosticKind.Note, note.Kind);
            Assert.Contains("exactly two", note.Message);
            Assert.Null(report.Gap);
            Assert.True(report.Overlay.IsEmpty);
        }

        [Theory]
        [InlineData(0.0034, 0.5, 1E4)]
        [InlineData(0.0034, 0.0005, 10.0)]
        [InlineData(0.0034, 0.00005, 1.0)]
        [InlineData(3.0, 0.05, 1.0)]
        [InlineData(0.5, 0.1, 10.0)]
        [InlineData(1E-9, 1.0, 1E6)]
        [InlineData(0.0, 1.0, 1.0)]
        [InlineData(20.0, 1.0, 1.0)]
        [InlineData(12.0, 1.0, 10.0)]
        [InlineData(13.0, 1.0, 1.0)]
        [InlineData(1.0, 0.0, 1.0)]
        public void TheScaleOfTheMagnifier_IsThePowerOfTenThatBringsTheGapNearestToFortyPixels(double distance, double pixelSize, double expected)
        {
            Assert.Equal(expected, GapMagnifier.ChooseScale(distance, pixelSize));
        }

        [Fact]
        public void TheMagnifier_DrawsTheGapScaledAboutItsMiddleAndLabelsItsTrueNumber()
        {
            SceneItem a = Diag.Polygon2("left", 0, 0, 10, 0, 10, 10, 0, 10);
            SceneItem b = Diag.Polygon2("right", 10.0034, 0, 20, 0, 20, 10, 10.0034, 10);
            Scene scene = Diag.SceneOf(a, b);
            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Gap);
            ExactGeometry exact = ExactGeometry.Build(scene, Primitives.Build(scene));

            // A pixel covers 0.5 mm: the gap is a hundredth of a pixel.
            GapLens lens = GapMagnifier.Build(exact, report.Gap, 0.5);

            Assert.Equal(1E4, lens.Scale);
            Assert.Contains("gap 0.0034", lens.Text);
            Assert.Contains("x10000", lens.Text);
            DiagnosticStroke gapLine = lens.Overlay.Strokes.Single(s => s.Color == DiagnosticColors.Gap);
            Assert.Equal(0.0034 * 1E4, (gapLine.Line.Points[1] - gapLine.Line.Points[0]).Length, 6);
            Assert.True(lens.Overlay.Strokes.Count > 1);
            Assert.Equal(0.0034 / 0.5, lens.Pixels, 9);
        }

        [Fact]
        public void TheMagnifierOfAGapAlreadyBigEnough_DrawsTheGapAsItIs()
        {
            SceneItem a = Diag.Polygon2("left", 0, 0, 10, 0, 10, 10, 0, 10);
            SceneItem b = Diag.Polygon2("right", 20, 0, 30, 0, 30, 10, 20, 10);
            Scene scene = Diag.SceneOf(a, b);
            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Gap);

            GapLens lens = GapMagnifier.Build(ExactGeometry.Build(scene, Primitives.Build(scene)), report.Gap, 0.1);

            Assert.Equal(1.0, lens.Scale);
            DiagnosticStroke gapLine = Assert.Single(lens.Overlay.Strokes);
            Assert.Equal(10.0, (gapLine.Line.Points[1] - gapLine.Line.Points[0]).Length, 9);
        }

        [Fact]
        public void TheToleranceSpheres_AreOneToAVertexAtEqualPointAndCapped()
        {
            SceneItem triangle = Diag.Polygon2("t", 0, 0, 10, 0, 5, 8);
            var many = new List<SceneItem>();

            for (int i = 0; i < 10; i++)
            {
                many.Add(Diag.Polygon2("p" + i, i * 100, 0, i * 100 + 10, 0, i * 100 + 10, 10, i * 100, 10));
            }

            Scene scene = Diag.SceneOf(new[] { triangle }.Concat(many).ToArray());
            Primitives primitives = Primitives.Build(scene);
            var options = new DiagnosticOptions { Checks = DiagnosticChecks.Spheres, MaxSpheres = 20 };

            DiagnosticReport report = SceneDiagnostics.Run(scene, primitives, null, null, options, CancellationToken.None);

            // 3 + 10 x 4 = 43 vertices; the first 20, in item order, are drawn as three circles each.
            Assert.Equal(11, report.Findings.Count);
            Assert.Equal(60, report.Overlay.Strokes.Count);
            Assert.All(report.Overlay.Strokes, s => Assert.Equal(DiagnosticColors.Sphere, s.Color));
            Assert.Contains("all are drawn", report.Findings[0].Message);
            Assert.Contains("the first", report.Findings[5].Message);
            double radius = (report.Overlay.Strokes[0].Line.Points[0] - new Vec3(0, 0, 0)).Length;
            Assert.Equal(scene.EqualPoint, radius, 12);
        }

        [Fact]
        public void ThePieces_AreColouredOneByOneWithTheirNumberAndAreaInTheOrderOfTheScene()
        {
            SceneItem one = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 1, 0, 1, 1, 0, 1) + "}", "p1", "pieces");
            SceneItem two = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(5, 0, 7, 0, 7, 2, 5, 2) + "}", "p2", "pieces");
            SceneItem three = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(10, 0, 13, 0, 13, 3, 10, 3) + "}", "p3", "pieces");

            DiagnosticReport report = Diag.Run(Diag.SceneOf(one, two, three), DiagnosticChecks.Pieces);

            Assert.Equal(new[] { 1.0, 4.0, 9.0 }, report.Findings.Select(f => f.Size).ToArray());
            Assert.Equal(new[] { "p1", "p2", "p3" }, report.Findings.Select(f => f.ItemName).ToArray());
            Assert.Equal(3, report.Overlay.Strokes.Select(s => s.Color).Distinct().Count());
            Assert.Equal(new[] { "#1  area 1", "#2  area 4", "#3  area 9" }, report.Overlay.Labels.Select(l => l.Text).ToArray());
            Assert.All(report.Findings, f => Assert.False(f.IsFault));
        }

        #endregion

        #region Order, cancelling, and what is left alone

        [Fact]
        public void TheSameRunTwice_GivesTheSameFindingsInTheSameOrder()
        {
            Scene scene = MixedScene();

            string first = Dump(Diag.Run(scene, DiagnosticChecks.All));
            string second = Dump(Diag.Run(scene, DiagnosticChecks.All));

            Assert.True(first.Length > 0);
            Assert.Equal(first, second);
        }

        [Fact]
        public void TheOrderOfTheItemsInTheScene_DoesNotChangeWhatEachItemFinds()
        {
            SceneItem[] items = MixedScene().Items.ToArray();
            var reversed = items.AsEnumerable().Reverse().Select(i => Diag.Raw(i.Kind, i.Data.GetRawText(), i.Name)).ToArray();

            var forward = Diag.Run(Diag.SceneOf(items.Select(i => Diag.Raw(i.Kind, i.Data.GetRawText(), i.Name)).ToArray()), DiagnosticChecks.All & ~DiagnosticChecks.Gap & ~DiagnosticChecks.Pieces & ~DiagnosticChecks.Clash);
            var backward = Diag.Run(Diag.SceneOf(reversed), DiagnosticChecks.All & ~DiagnosticChecks.Gap & ~DiagnosticChecks.Pieces & ~DiagnosticChecks.Clash);

            Assert.Equal(PerItem(forward), PerItem(backward));
        }

        [Fact]
        public void TheStartingVertexOfALoop_DoesNotChangeItsFindings()
        {
            double[] bowTie = { 0, 0, 2, 2, 2, 0, 0, 2 };
            double[] spiked = { 0, 0, 10, 0, 10, 10, 5.01, 10, 5, 30, 4.99, 10, 0, 10 };

            foreach (double[] xy in new[] { bowTie, spiked })
            {
                int n = xy.Length / 2;
                var reports = new List<string>();

                for (int shift = 0; shift < n; shift++)
                {
                    var rotated = new double[xy.Length];

                    for (int i = 0; i < n; i++)
                    {
                        rotated[2 * i] = xy[2 * ((i + shift) % n)];
                        rotated[2 * i + 1] = xy[2 * ((i + shift) % n) + 1];
                    }

                    DiagnosticReport report = Diag.Run(Diag.SceneOf(Diag.Polygon2("loop", rotated)), DiagnosticChecks.Crossings | DiagnosticChecks.Needles | DiagnosticChecks.Duplicates | DiagnosticChecks.Slivers);
                    reports.Add(string.Join(";", report.Findings.Select(f => f.Kind + "@" + f.At + "/" + f.Size.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
                }

                Assert.Single(reports.Distinct());
            }
        }

        [Fact]
        public void TheOrderOfTheFacesOfABody_DoesNotChangeWhereItsEdgesAreOpen()
        {
            GeoSolid3 open = Diag.OpenBox();
            var reports = new List<string>();

            foreach (bool library in new[] { true, false })
            {
                for (int shift = 0; shift < open.Faces.Count; shift++)
                {
                    GeoSolid3 turned = new GeoSolid3(open.Faces.Skip(shift).Concat(open.Faces.Take(shift)));
                    Scene scene = new SceneWriter().Add(turned, name: "box").Scene;
                    DiagnosticReport report = Diag.Run(scene, Edges, library);
                    reports.Add(string.Join(";", report.Findings.Select(f => f.Kind + "@" + f.At + "/" + f.Size.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
                }
            }

            Assert.Single(reports.Distinct());
        }

        [Fact]
        public void ACancelledRun_ThrowsAndLeavesNoReport()
        {
            Scene scene = MixedScene();
            Primitives primitives = Primitives.Build(scene);
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();

                Assert.Throws<OperationCanceledException>(() => SceneDiagnostics.Run(scene, primitives, null, null, new DiagnosticOptions { Checks = DiagnosticChecks.All }, source.Token));
            }
        }

        [Fact]
        public void TheSelectionIsWhatIsChecked_AndNothingSelectedChecksEverything()
        {
            Scene scene = MixedScene();

            DiagnosticReport one = Diag.Run(scene, DiagnosticChecks.Duplicates | DiagnosticChecks.Needles | DiagnosticChecks.Crossings, true, 0);
            DiagnosticReport all = Diag.Run(scene, DiagnosticChecks.Duplicates | DiagnosticChecks.Needles | DiagnosticChecks.Crossings);

            Assert.Equal(new[] { 0 }, one.Items.ToArray());
            Assert.Equal(Enumerable.Range(0, scene.Items.Count).ToArray(), all.Items.ToArray());
            Assert.All(one.Findings, f => Assert.Equal(0, f.ItemIndex));
            Assert.True(all.Findings.Count > one.Findings.Count);
        }

        [Fact]
        public void ALabelAndAKindTheViewerDoesNotKnow_AreLeftAlone()
        {
            Scene scene = new SceneWriter().Text(new GeoPoint3(1, 2, 3), "here").Scene;
            scene.Items.Add(Diag.Raw("GeoUnknown9", "{}", "mystery"));

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.All & ~DiagnosticChecks.Gap & ~DiagnosticChecks.Clash);

            Assert.Empty(report.Findings);
        }

        [Fact]
        public void AnItemWhoseNumbersAreWrong_IsSaidSoAsANoteAndTheOthersAreStillChecked()
        {
            SceneItem broken = Diag.Raw("GeoPolygon2", "{\"nope\":1}", "broken");
            SceneItem bowTie = Diag.Polygon2("bow tie", 0, 0, 2, 2, 2, 0, 0, 2);

            DiagnosticReport report = Diag.Run(Diag.SceneOf(broken, bowTie), DiagnosticChecks.Crossings);

            Assert.Equal(DiagnosticKind.Note, report.Findings[0].Kind);
            Assert.Equal("broken", report.Findings[0].ItemName);
            Assert.Equal(DiagnosticKind.SelfCrossing, report.Findings[1].Kind);
        }

        [Fact]
        public void MoreFindingsOfAKindThanTheCap_AreCountedInANoteAndTheirDrawingIsLeftOff()
        {
            // A chain of 30 vertices all within EqualPoint of the next: 29 neighbours and more pairs, with the cap at 5.
            var xy = new List<double>();

            for (int i = 0; i < 30; i++)
            {
                xy.Add(i * 0.0001);
                xy.Add(0.0);
            }

            xy.Add(50);
            xy.Add(50);
            Scene scene = Diag.SceneOf(Diag.Polygon2("dense", xy.ToArray()));
            Primitives primitives = Primitives.Build(scene);

            DiagnosticReport report = SceneDiagnostics.Run(scene, primitives, null, null, new DiagnosticOptions { Checks = DiagnosticChecks.Duplicates, MaxFindingsPerKind = 5 }, CancellationToken.None);

            Assert.Equal(5, Diag.Count(report, DiagnosticKind.DuplicateVertex));
            DiagnosticFinding note = Assert.Single(Diag.Of(report, DiagnosticKind.Note));
            Assert.Contains("more of DuplicateVertex are not listed", note.Message);
            Assert.Equal(5, report.Overlay.Dots.Count);
        }

        [Fact]
        public void TheFindingsAreNumberedFromOneInTheOrderTheyAreListed()
        {
            DiagnosticReport report = Diag.Run(MixedScene(), DiagnosticChecks.All);

            Assert.Equal(Enumerable.Range(1, report.Findings.Count).ToArray(), report.Findings.Select(f => f.Number).ToArray());
            Assert.StartsWith("#1 ", report.Findings[0].ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSpotOfAFinding_IsNeverSmallerThanTwentyTimesEqualPoint()
        {
            DiagnosticReport report = Diag.Run(MixedScene(), DiagnosticChecks.All);
            double tol = new Scene().EqualPoint;

            Assert.All(report.Findings.Where(f => f.Kind != DiagnosticKind.Note), f => Assert.True(f.Spot.Size.X >= 40.0 * tol - 1E-12, f.ToString()));
        }

        #endregion

        #region The sweep

        [Fact]
        public void TheClosePairsOfPoints_AreThoseABruteForceFindsInTheSameOrder()
        {
            var random = new Random(11);
            var points = new Vec3[400];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vec3(random.NextDouble() * 20.0, random.NextDouble() * 20.0, random.NextDouble() * 3.0);
            }

            var expected = new List<(int, int)>();

            for (int i = 0; i < points.Length; i++)
            {
                for (int j = i + 1; j < points.Length; j++)
                {
                    if ((points[i] - points[j]).Length <= 1.5)
                    {
                        expected.Add((i, j));
                    }
                }
            }

            var found = Sweep.ClosePairs(points, 1.5, CancellationToken.None).Select(p => (p.First, p.Second)).ToList();

            Assert.True(expected.Count > 50);
            Assert.Equal(expected, found);
        }

        [Fact]
        public void PointsOnALongThinLine_AreSweptAlongTheLongAxisAndNotQuadratically()
        {
            // 200 000 points on a line along Y, all at one X: a sweep along X would compare every pair, 2E10 of them.
            var points = new Vec3[200000];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vec3(7.0, i * 1.0, 0.0);
            }

            var clock = System.Diagnostics.Stopwatch.StartNew();
            var found = Sweep.ClosePairs(points, 0.5, CancellationToken.None);
            clock.Stop();

            Assert.Empty(found);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), clock.Elapsed.ToString());
        }

        [Fact]
        public void TheOverlappingBoxes_AreThoseABruteForceFinds()
        {
            var random = new Random(5);
            int count = 300;
            var lo = new double[3 * count];
            var hi = new double[3 * count];

            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    double a = random.NextDouble() * 50.0;
                    lo[3 * i + k] = a;
                    hi[3 * i + k] = a + random.NextDouble() * 4.0;
                }
            }

            var expected = new HashSet<(int, int)>();

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    bool overlap = true;

                    for (int k = 0; k < 3; k++)
                    {
                        overlap &= lo[3 * j + k] <= hi[3 * i + k] + 0.25 && lo[3 * i + k] <= hi[3 * j + k] + 0.25;
                    }

                    if (overlap)
                    {
                        expected.Add((i, j));
                    }
                }
            }

            var found = new List<(int, int)>();
            Sweep.Pairs(lo, hi, count, 0.25, (a, b) => found.Add((a, b)), CancellationToken.None);

            Assert.True(expected.Count > 20);
            Assert.Equal(expected.OrderBy(p => p.Item1).ThenBy(p => p.Item2).ToList(), found.OrderBy(p => p.Item1).ThenBy(p => p.Item2).ToList());
        }

        [Fact]
        public void ALongThinLoopWithManyEdges_IsCheckedForCrossingsInShortOrder()
        {
            // A zigzag along Y 100 000 long and 1 wide, which crosses itself once at the end.
            var xy = new List<double>();

            for (int i = 0; i < 20000; i++)
            {
                xy.Add(i % 2 == 0 ? 0.0 : 1.0);
                xy.Add(i * 5.0);
            }

            xy.Add(0.5);
            xy.Add(-10.0);
            Scene scene = Diag.SceneOf(Diag.Raw("GeoPolyline2", "{\"points\":" + Diag.Pts2(xy.ToArray()) + "}", "zigzag"));
            var clock = System.Diagnostics.Stopwatch.StartNew();

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Crossings);

            clock.Stop();
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), clock.Elapsed.ToString());
            Assert.True(Diag.Count(report, DiagnosticKind.SelfCrossing) >= 1);
        }

        #endregion

        #region More shapes and the tolerance of the scene

        [Fact]
        public void ABowTieInSpace_CrossesAtItsExactPointToo()
        {
            Scene scene = new SceneWriter().Add(new GeoPolygon3(new GeoPoint3(0, 0, 5), new GeoPoint3(3, 2, 5), new GeoPoint3(3, 0, 5), new GeoPoint3(0, 3, 5)), name: "bow tie 3D").Scene;

            DiagnosticFinding crossing = Assert.Single(Diag.Run(scene, DiagnosticChecks.Crossings).Findings);

            Assert.True((crossing.At - new Vec3(1.8, 1.2, 5)).Length < 1E-12, crossing.At.ToString());
            Assert.Contains("GeoPolygon3.IsSimple(tolerance)", crossing.Source);
        }

        [Fact]
        public void TheToleranceOfTheScene_SetsHowCloseTwoVerticesHaveToBeToBeADuplicate()
        {
            SceneItem item = Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 0.004, 10, 10, 0, 10) + "}", "four thousandths");
            Scene fine = Diag.SceneOf(item);
            Scene coarse = Diag.SceneOf(item);
            coarse.EqualPoint = 0.01;

            Assert.Empty(Diag.Run(fine, DiagnosticChecks.Duplicates).Findings);
            DiagnosticFinding found = Assert.Single(Diag.Run(coarse, DiagnosticChecks.Duplicates).Findings);
            Assert.Contains("within EqualPoint 0.01", found.Message);
        }

        [Fact]
        public void ABodyTheLibraryIsNotAsked_GivesNoReportAndStillHasItsOpenEdges()
        {
            Scene scene = new SceneWriter().Add(Diag.OpenBox(), name: "lidless").Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Report | DiagnosticChecks.Edges, false);

            Assert.Equal(0, Diag.Count(report, DiagnosticKind.Report));
            Assert.Equal(4, Diag.Count(report, DiagnosticKind.OpenEdge));
        }

        [Fact]
        public void AGridMeshOfTwentyThousandTriangles_IsReadForItsEdgesQuickly_AndItsBoundaryIsFourHundredEdges()
        {
            const int N = 100;
            var points = new List<double>();

            for (int j = 0; j <= N; j++)
            {
                for (int i = 0; i <= N; i++)
                {
                    points.Add(i);
                    points.Add(j);
                    points.Add(0);
                }
            }

            var faces = new List<string>();

            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    int a = j * (N + 1) + i;
                    int b = a + 1;
                    int c = b + (N + 1);
                    int d = a + (N + 1);
                    faces.Add("[" + a + "," + b + "," + c + "]");
                    faces.Add("[" + a + "," + c + "," + d + "]");
                }
            }

            Scene scene = Diag.SceneOf(Diag.Raw("GeoMesh3", "{\"kind\":\"Convex\",\"points\":" + Diag.Pts3(points.ToArray()) + ",\"faces\":[" + string.Join(",", faces) + "]}", "grid"));
            var clock = System.Diagnostics.Stopwatch.StartNew();

            var options = new DiagnosticOptions { Checks = DiagnosticChecks.Edges | DiagnosticChecks.Duplicates | DiagnosticChecks.Slivers | DiagnosticChecks.Needles, MaxFindingsPerKind = 1000 };
            DiagnosticReport report = SceneDiagnostics.Run(scene, Primitives.Build(scene), null, null, options, CancellationToken.None);

            clock.Stop();
            Assert.Equal(4 * N, Diag.Count(report, DiagnosticKind.OpenEdge));
            Assert.Equal(0, report.FaultCount);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), clock.Elapsed.ToString());
        }

        [Fact]
        public void TheFindingsOfAThousandItems_AreCappedPerItem_AndTheReportStaysInOrderOfTheItems()
        {
            var items = new List<SceneItem>();

            for (int i = 0; i < 1000; i++)
            {
                items.Add(Diag.Polygon2("bow " + i, i * 10, 0, i * 10 + 2, 2, i * 10 + 2, 0, i * 10, 2));
            }

            DiagnosticReport report = Diag.Run(Diag.SceneOf(items.ToArray()), DiagnosticChecks.Crossings);

            Assert.Equal(1000, report.Findings.Count);
            Assert.Equal(Enumerable.Range(0, 1000).ToArray(), report.Findings.Select(f => f.ItemIndex).ToArray());
            Assert.Equal(new Vec3(1, 1, 0), report.Findings[0].At);
            Assert.Equal(new Vec3(9991, 1, 0), report.Findings[999].At);
        }

        #endregion

        #region Helpers

        private static Scene MixedScene()
            => Diag.SceneOf(
                Diag.Polygon2("bow tie", 0, 0, 2, 2, 2, 0, 0, 2),
                Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 10, 5.01, 10, 5, 30, 4.99, 10, 0, 10) + "}", "spiked"),
                Diag.Raw("GeoPolygon2", "{\"points\":" + Diag.Pts2(0, 0, 10, 0, 10, 0.0004, 10, 10, 0, 10) + "}", "doubled"),
                Diag.Raw("GeoTriangle2", "{\"points\":" + Diag.Pts2(0, 0, 100, 0, 50, 0.0005) + "}", "sliver"),
                new SceneWriter().Add(Diag.OpenBox(), name: "lidless").Scene.Items[0],
                new SceneWriter().Add(Diag.FinnedBox(), name: "finned").Scene.Items[0],
                new SceneWriter().Add(Diag.TurnedBox(), name: "turned").Scene.Items[0],
                new SceneWriter().Add(SampleShapes.Box(30, 20, 10, false), name: "box").Scene.Items[0]);

        private static string Dump(DiagnosticReport report)
            => string.Join("\n", report.Findings.Select(f => f.Number + "|" + f + "|" + f.At + "|" + f.Size.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" + f.Spot));

        /// <summary>
        /// What each item found, by the name of the item, without the numbers that depend on the order of the scene.
        /// </summary>
        private static List<string> PerItem(DiagnosticReport report)
            => report.Findings
                .Select(f => f.ItemName + "|" + f.Check + "|" + f.Kind + "|" + f.Message + "|" + f.At + "|" + f.Size.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

        #endregion
    }
}
