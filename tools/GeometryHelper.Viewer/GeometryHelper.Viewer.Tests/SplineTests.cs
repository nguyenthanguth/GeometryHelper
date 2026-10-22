using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The spline in the viewer (SPL-6): its kinds in a scene, its drawing by the viewer's own cut at its knots within the chord
    /// tolerance, its numbers, the snaps to its ends and to the nearest point of it, the pick of it, and the SPLINE of a DXF file.
    /// </summary>
    public class SplineTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>The chord tolerance of a scene at the default point tolerance: a tenth of it.</summary>
        private const double Chord = 1E-4;

        private static GeoSpline2 Arch() => new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 150), new GeoPoint2(200, 150), new GeoPoint2(300, 0) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

        // A cubic of four pieces with a corner: the knot 3.1 is there three times, so the curve passes through (180, 95.75) with a kink.
        private static GeoSpline2 Kinked() => new GeoSpline2(
            3,
            new[] { new GeoPoint2(10.125, -3.5), new GeoPoint2(60, 80.25), new GeoPoint2(120.5, -40), new GeoPoint2(180, 95.75), new GeoPoint2(240.25, -10), new GeoPoint2(300, 60), new GeoPoint2(355.5, 5) },
            new[] { 2.0, 2.0, 2.0, 2.0, 3.1, 3.1, 3.1, 5.0, 5.0, 5.0, 5.0 });

        private static GeoSpline3 Rational3() => new GeoSpline3(
            3,
            new[] { new GeoPoint3(13.5, -168.25, 2), new GeoPoint3(100, 0, 40), new GeoPoint3(100, 100, 0), new GeoPoint3(190, 120.75, 85.5), new GeoPoint3(220, 30, 10) },
            new[] { 0, 0, 0, 0, 0.4, 1, 1, 1, 1 },
            new[] { 1.0, 0.7, 2.5, 0.9, 1.3 });

        private static Scene Read(DxfWriter writer) => SceneImport.ReadText(writer.ToString(), "dxf");

        private static Vec3 V(GeoPoint2 p) => new Vec3(p.X, p.Y, 0.0);

        private static Vec3 V(GeoPoint3 p) => new Vec3(p.X, p.Y, p.Z);

        private static GeoPoint3 P(Vec3 v) => new GeoPoint3(v.X, v.Y, v.Z);

        /// <summary>
        /// The longest a drawn chord strays from the spline, from seven points along each chord measured by the library's distance.
        /// </summary>
        private static double Strays(Vec3[] drawn, Func<Vec3, double> distance)
        {
            double worst = 0.0;

            for (int i = 0; i + 1 < drawn.Length; i++)
            {
                for (int s = 1; s < 8; s++)
                {
                    worst = Math.Max(worst, distance(drawn[i] + (drawn[i + 1] - drawn[i]) * (s / 8.0)));
                }
            }

            return worst;
        }

        [Fact]
        public void TheKindsAreKnown_AndAWriterKeepsTheDegreeKnotsAndPoints_TheWeightsOnlyWhereTheyAreNotAllOne()
        {
            Assert.True(SceneKinds.IsKnown("GeoSpline2"));
            Assert.True(SceneKinds.IsKnown("GeoSpline3"));

            Scene scene = new SceneWriter().Add(Arch(), name: "flat").Add(Rational3(), name: "solid").Scene;

            Assert.Equal(new[] { "GeoSpline2", "GeoSpline3" }, scene.Items.Select(i => i.Kind).ToArray());
            Assert.Equal(new[] { 3.0, 0, 0, 100, 150, 200, 150, 300, 0, 0, 0, 0, 0, 1, 1, 1, 1 }, Imp.Of(scene.Items[0]));
            Assert.False(scene.Items[0].Data.TryGetProperty("weights", out _));
            Assert.Equal(new[] { 1.0, 0.7, 2.5, 0.9, 1.3 }, DataJson.Weights(scene.Items[1].Data));
        }

        [Fact]
        public void ASplineSurvivesTheJsonOfAScene_AndComesBackAsTheSameSpline()
        {
            Scene read = SceneReader.Read(new SceneWriter().Add(Kinked()).Add(Rational3()).ToJson());

            Assert.Equal(Kinked(), (GeoSpline2)read.Items[0].ToGeometry());
            Assert.Equal(Rational3(), (GeoSpline3)read.Items[1].ToGeometry());
        }

        [Fact]
        public void ASplineIsDrawnByTheViewersOwnCut_WithinTheChordTolerance_ItsEndsItsEndControlPoints()
        {
            GeoSpline2 arch = Arch();
            GeoSpline3 rational = Rational3();
            Scene scene = new SceneWriter().Add(arch).Add(rational).Scene;
            Primitives primitives = Primitives.Build(scene);

            Vec3[] flat = Assert.Single(primitives.Items[0].Polylines).Points;
            Vec3[] space = Assert.Single(primitives.Items[1].Polylines).Points;

            Assert.False(primitives.Items[0].Polylines[0].Closed);
            Assert.Equal(V(arch.StartPoint), flat[0]);
            Assert.Equal(V(arch.EndPoint), flat[flat.Length - 1]);
            Assert.Equal(V(rational.StartPoint), space[0]);
            Assert.Equal(V(rational.EndPoint), space[space.Length - 1]);

            Assert.All(flat, p => Assert.True(arch.DistanceTo(new GeoPoint2(p.X, p.Y), Tol) < 1E-9));
            Assert.All(space, p => Assert.True(rational.DistanceTo(P(p), Tol) < 1E-9));
            Assert.True(Strays(flat, p => arch.DistanceTo(new GeoPoint2(p.X, p.Y), Tol)) <= Chord);
            Assert.True(Strays(space, p => rational.DistanceTo(P(p), Tol)) <= Chord);

            // The arch is a parabola of radius 100 at its top, where a chord within 1E-4 is 0.28 long; cut in halves until each piece keeps to
            // the rule, it is 1 722 chords, where 2 degrees a piece alone, its 113 degrees of turn, would take some sixty.
            Assert.Equal(1723, flat.Length);
            Assert.Null(primitives.Items[0].Warning);
        }

        [Fact]
        public void ACircleBuiltAsASpline_IsDrawnOnItsCircle_AndAKinkIsACornerOfTheDrawing()
        {
            GeoSpline2 circle = GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(5, 5), 100));
            Scene scene = new SceneWriter().Add(circle).Add(Kinked()).Scene;
            Primitives primitives = Primitives.Build(scene);

            Vec3[] round = Assert.Single(primitives.Items[0].Polylines).Points;
            Vec3[] kinked = Assert.Single(primitives.Items[1].Polylines).Points;

            Assert.All(round, p => Assert.InRange((p - new Vec3(5, 5, 0)).Length, 100.0 - 1E-9, 100.0 + 1E-9));
            Assert.True(Strays(round, p => Math.Abs((p - new Vec3(5, 5, 0)).Length - 100.0)) <= Chord);

            // The kink is where two Bézier pieces meet, so it is drawn as a corner and not cut across.
            Assert.Contains(kinked, p => (p - new Vec3(180, 95.75, 0)).Length < 1E-9);
        }

        [Fact]
        public void TheBoxOfASpline_IsTheBoxOfTheCurve_NotOfItsControlPoints()
        {
            Primitives primitives = Primitives.Build(new SceneWriter().Add(Arch()).Scene);
            Bounds3 box = primitives.Items[0].Bounds;

            Assert.Equal(0.0, box.Min.Y);
            Assert.InRange(box.Max.Y, 112.5 - Chord, 112.5);
            Assert.Equal(300.0, box.Max.X);
        }

        [Fact]
        public void ASplineTheNumbersOfWhichAreWrong_SaysSoAndIsNotDrawn_AndTheRestIsDrawn()
        {
            // Knots that are not clamped: the library's splines start and end at their end control points.
            var writer = new SceneWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 1)));
            string json = writer.Add(Arch()).ToJson().Replace("[0,0,0,0,1,1,1,1]", "[0,0,0,0.5,1,1,1,1]");
            Scene scene = SceneReader.Read(json);
            Primitives primitives = Primitives.Build(scene);

            Assert.Contains("clamped", primitives.Items[1].Warning);
            Assert.Empty(primitives.Items[1].Polylines);
            Assert.Single(primitives.Items[0].Polylines);
        }

        [Fact]
        public void TheNumbersOfASpline_AreItsDegreeControlPointsWeightsDomainAndLength()
        {
            Scene scene = new SceneWriter().Add(Kinked()).Add(GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 1), 10))).Scene;
            Primitives primitives = Primitives.Build(scene);

            var flat = ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]).ToDictionary(p => p.Key, p => p.Value);
            var solid = ItemMeasures.Describe(scene.Items[1], scene, primitives.Items[1]).ToDictionary(p => p.Key, p => p.Value);

            Assert.Equal("3", flat["Degree"]);
            Assert.Equal("7", flat["Control points"]);
            Assert.Equal("no", flat["Rational"]);
            Assert.Equal("2 to 5", flat["Domain"]);
            Assert.Equal("no", flat["Closed"]);
            Assert.Equal(Kinked().Length.ToString("G10", CultureInfo.InvariantCulture), flat["Length"]);

            Assert.Equal("2", solid["Degree"]);
            Assert.Equal("9", solid["Control points"]);
            Assert.Equal("yes", solid["Rational"]);
            // A whole turn ends on its start exactly, so the circle is closed.
            Assert.Equal("yes", solid["Closed"]);
            Assert.Equal((2.0 * Math.PI * 10.0).ToString("G10", CultureInfo.InvariantCulture), solid["Length"]);
            Assert.Equal("(-9, -8, 3)", solid["Bounds min (library)"]);
        }

        [Fact]
        public void ASplineIsClosedInItsNumbersOnlyWhereItsEndsAreOnePointExactly()
        {
            GeoPoint2[] loop = { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100), new GeoPoint2(0, 0) };
            var closed = new GeoSpline2(2, loop, new double[] { 0, 0, 0, 1, 2, 3, 3, 3 });
            loop[4] = new GeoPoint2(0, 1E-6);
            var near = new GeoSpline2(2, loop, new double[] { 0, 0, 0, 1, 2, 3, 3, 3 });
            Scene scene = new SceneWriter().Add(closed).Add(near).Scene;

            Assert.Equal("yes", ItemMeasures.Describe(scene.Items[0], scene, null).ToDictionary(p => p.Key, p => p.Value)["Closed"]);
            Assert.Equal("no", ItemMeasures.Describe(scene.Items[1], scene, null).ToDictionary(p => p.Key, p => p.Value)["Closed"]);
        }

        [Fact]
        public void TheEndsOfASplineWhoseEndPiecesHaveNoLength_StillSnap()
        {
            // Three control points on one point make the first piece a point; so do three at the far end.
            var s = new GeoSpline2(2, new[] { new GeoPoint2(0, 0), new GeoPoint2(0, 0), new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(10, 10), new GeoPoint2(10, 10) }, new double[] { 0, 0, 0, 1, 2, 3, 4, 5, 5, 5 });
            var view = ViewFixture.Top(new SceneWriter().Add(s));

            foreach (Vec3 end in new[] { new Vec3(0, 0, 0), new Vec3(10, 10, 0) })
            {
                (double x, double y) = view.Pixel(end);
                SnapHit hit = SnapEngine.Find(view.Geometry, view.Map, x + 1.5, y - 1.0, SnapKinds.End, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null);

                Assert.NotNull(hit);
                Assert.Equal("end", hit.Name);
                Assert.Equal(end, hit.Point);
            }
        }

        [Fact]
        public void ASplineOfFortyThousandControlPoints_IsDrawnInATimeThatGrowsAsItsLength()
        {
            // Each knot is reached once by the cut into pieces, and the pieces are cut once for the count of the scene and its drawing: a cubic of
            // 40 000 points 10 apart takes under a second, where it took 71 s when each knot was inserted into the whole list.
            var random = new Random(5);
            GeoSpline2 spline = GeoSpline2.FromControlPoints(3, Enumerable.Range(0, 40000).Select(i => new GeoPoint2(i * 10.0, random.NextDouble() * 10)));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Primitives primitives = Primitives.Build(new SceneWriter().Add(spline).Scene);
            watch.Stop();

            Assert.InRange(watch.Elapsed.TotalSeconds, 0.0, 10.0);
            Vec3[] drawn = Assert.Single(primitives.Items[0].Polylines).Points;
            Assert.Equal(V(spline.EndPoint), drawn[drawn.Length - 1]);
        }

        [Fact]
        public void ASplineCutShortOfTheRule_SaysItIsABezierPieceThatIsCapped()
        {
            Primitives primitives = Primitives.Build(new SceneWriter().Add(GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 1E7))).Scene);

            Assert.Contains("a Bézier piece of the spline would take more than 65536 chords at a chord tolerance of 0.0001 and is cut in that many", primitives.Items[0].Warning);
        }

        [Fact]
        public void TheEndsOfASplineSnap_AndTheCornersBetweenItsChordsDoNot()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(Arch()));
            DrawPolyline drawn = Assert.Single(view.Primitives.Items[0].Polylines);

            (double sx, double sy) = view.Pixel(new Vec3(0, 0, 0));
            SnapHit start = SnapEngine.Find(view.Geometry, view.Map, sx + 2.0, sy - 1.0, SnapKinds.End, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null);
            (double ex, double ey) = view.Pixel(new Vec3(300, 0, 0));
            SnapHit end = SnapEngine.Find(view.Geometry, view.Map, ex - 2.0, ey - 1.0, SnapKinds.End | SnapKinds.Mid, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null);

            Assert.Equal(new Vec3(0, 0, 0), start.Point);
            Assert.Equal("end", start.Name);
            Assert.Equal(new Vec3(300, 0, 0), end.Point);

            // The top of the arch is a corner of the drawing, and neither an end nor the middle of anything.
            (double tx, double ty) = view.Pixel(drawn.Points[drawn.Points.Length / 2]);
            Assert.Null(SnapEngine.Find(view.Geometry, view.Map, tx, ty, SnapKinds.End | SnapKinds.Mid, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null));
        }

        [Fact]
        public void TheNearestPointOfASpline_IsWithinTheChordToleranceOfIt()
        {
            GeoSpline2 arch = Arch();
            var view = ViewFixture.Top(new SceneWriter().Add(arch));

            (double x, double y) = view.Pixel(V(arch.GetPointAtParameter(0.3)));
            SnapHit hit = SnapEngine.Find(view.Geometry, view.Map, x + 1.0, y + 2.0, SnapKinds.Nearest, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null);

            Assert.Equal("nearest", hit.Name);
            Assert.Equal(0, hit.ItemIndex);
            Assert.True(arch.DistanceTo(new GeoPoint2(hit.Point.X, hit.Point.Y), Tol) <= Chord);
        }

        [Fact]
        public void ASplineIsPickedOnItsLine_WithinAFewPixels_AndNotFurther()
        {
            GeoSpline2 arch = Arch();
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoLine2(new GeoPoint2(0, -100), new GeoPoint2(300, -100))).Add(arch));

            (double x, double y) = view.Pixel(V(arch.GetPointAtParameter(0.7)));
            PickHit near = PickEngine.Pick(view.Primitives, view.Geometry, view.Map, x, y + 3.0, null, true);
            PickHit far = PickEngine.Pick(view.Primitives, view.Geometry, view.Map, x, y + 80.0, null, true);

            Assert.Equal(1, near.ItemIndex);
            Assert.Equal(PickKind.Curve, near.Kind);
            Assert.True(far == null || far.ItemIndex != 1);
        }

        [Fact]
        public void ASceneOverTheCapOfPoints_CutsItsSplinesCoarser_AndSaysSo()
        {
            var writer = new SceneWriter();

            for (int i = 0; i < 20; i++)
            {
                writer.Add(Arch() + new GeoVector2(0, i * 10));
            }

            Primitives fine = Primitives.Build(writer.Scene);
            Primitives coarse = Primitives.Build(writer.Scene, 2000);
            int finePoints = fine.Items.Sum(i => i.Polylines.Sum(l => l.Points.Length));
            int coarsePoints = coarse.Items.Sum(i => i.Polylines.Sum(l => l.Points.Length));

            Assert.True(finePoints > 2000);
            Assert.InRange(coarsePoints, 20 * 2, 2000);
            Assert.Contains(coarse.Warnings, w => w.Contains("curve(s) are cut"));
        }

        // ----- DXF -----------------------------------------------------------------------------------------------------

        [Fact]
        public void ASplineOfADxfFile_ComesBackToTheBit_InThePlaneAndInSpace_WithoutAWarning()
        {
            var flat = (GeoSpline2)SampleShapes.Make("GeoSpline2", 3);
            var solid = (GeoSpline3)SampleShapes.Make("GeoSpline3", 3);

            Scene a = Read(new DxfWriter().Add(flat, "curves"));
            Scene b = Read(new DxfWriter().Add(solid, "curves"));

            Assert.Equal("GeoSpline2", Assert.Single(a.Items).Kind);
            Assert.Equal("GeoSpline3", Assert.Single(b.Items).Kind);
            Assert.Equal("curves", a.Items[0].Layer);
            Imp.SameBits(Imp.Of(flat), Imp.Of(a.Items[0]));
            Imp.SameBits(Imp.Of(solid), Imp.Of(b.Items[0]));
            Assert.Equal(flat, (GeoSpline2)a.Items[0].ToGeometry());
            Assert.Equal(solid, (GeoSpline3)b.Items[0].ToGeometry());
            Assert.Empty(a.LoadWarnings);
            Assert.Empty(b.LoadWarnings);
        }

        [Fact]
        public void EverySplineOfAFuzzComesBackFromDxfWithTheSameControlPointsKnotsAndWeights()
        {
            var random = new Random(1021);

            for (int round = 0; round < 100; round++)
            {
                int degree = 1 + random.Next(4);
                int count = degree + 1 + random.Next(5);
                double R() => (random.NextDouble() * 2.0 - 1.0) * 1E4 / 7.0;
                double start = R();
                var knots = Enumerable.Repeat(start, degree + 1).ToList();
                double at = start;

                for (int i = 0; i < count - degree - 1; i++)
                {
                    at += random.NextDouble() / 3.0 + 1E-6;
                    knots.Add(at);
                }

                knots.AddRange(Enumerable.Repeat(at + random.NextDouble() + 1E-6, degree + 1));
                double[] weights = round % 3 == 0 ? null : Enumerable.Range(0, count).Select(i => 0.1 + random.NextDouble() * 4.0).ToArray();
                var spline = new GeoSpline3(degree, Enumerable.Range(0, count).Select(i => new GeoPoint3(R(), R(), R())), knots, weights);

                SceneItem item = Assert.Single(Read(new DxfWriter().Add(spline)).Items);
                Assert.Equal(spline, (GeoSpline3)item.ToGeometry());
            }
        }

        [Fact]
        public void ASplineInSpaceLyingOnTheFloor_ComesBackInThePlane_AsAnEllipseOnItDoes()
        {
            var lying = new GeoSpline3(2, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(50, 80, 0), new GeoPoint3(100, 0, 0) }, new double[] { 0, 0, 0, 1, 1, 1 });
            var raised = new GeoSpline3(2, new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(50, 80, 5), new GeoPoint3(100, 0, 5) }, new double[] { 0, 0, 0, 1, 1, 1 });

            Assert.Equal("GeoSpline2", Assert.Single(Read(new DxfWriter().Add(lying)).Items).Kind);
            Assert.Equal("GeoSpline3", Assert.Single(Read(new DxfWriter().Add(raised)).Items).Kind);
        }

        private static string G(params object[] pairs)
        {
            var text = new StringBuilder();

            for (int i = 0; i < pairs.Length; i += 2)
            {
                text.Append(pairs[i]).Append('\n').Append(Convert.ToString(pairs[i + 1], CultureInfo.InvariantCulture)).Append('\n');
            }

            return text.ToString();
        }

        private static Scene ReadEntities(string entities)
            => SceneImport.ReadText(G(0, "SECTION", 2, "ENTITIES") + entities + G(0, "ENDSEC", 0, "EOF"), "dxf");

        [Fact]
        public void ASplineGivenByItsFitPointsAlone_IsTheSplineThroughThem_AndSaid()
        {
            // The library passes a spline through the fit points, of one degree less than the file asks where there are too few of them.
            string fit = G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 70, 8, 71, 3, 72, 0, 73, 0, 74, 3, 11, 0, 21, 0, 31, 0, 11, 50, 21, 40, 31, 0, 11, 100, 21, 0, 31, 0);
            Scene scene = ReadEntities(fit + G(0, "POINT", 10, 1, 20, 2));

            Assert.Equal(new[] { "GeoSpline2", "GeoPoint2" }, scene.Items.Select(i => i.Kind).ToArray());
            var spline = (GeoSpline2)scene.Items[0].ToGeometry();
            Assert.Equal(new GeoPoint2(0, 0), spline.StartPoint);
            Assert.Equal(new GeoPoint2(100, 0), spline.EndPoint);
            Assert.Contains(scene.LoadWarnings, w => w.Contains("1 SPLINE given by its fit points alone was read as the spline GeoSpline.Interpolate passes through them"));
        }

        [Fact]
        public void APeriodicSpline_IsReadClamped_OnTheSameCurve()
        {
            // A closed periodic cubic of AutoCAD's: its knots run evenly past both ends, not clamped.
            var groups = new List<object> { 0, "SPLINE", 8, "0", 100, "AcDbSpline", 70, 11, 71, 3, 72, 10, 73, 6, 74, 0 };

            for (int k = 0; k < 10; k++)
            {
                groups.AddRange(new object[] { 40, k - 3 });
            }

            foreach ((double x, double y) in new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 100.0), (0.0, 100.0), (0.0, 0.0), (100.0, 0.0) })
            {
                groups.AddRange(new object[] { 10, x, 20, y, 30, 0 });
            }

            Scene scene = ReadEntities(G(groups.ToArray()));

            // The library clamps its knots to the part of it AutoCAD draws, knot 0 to knot 3, which runs from (P0 + 4 P1 + P2) / 6 to
            // (P3 + 4 P4 + P5) / 6, as a uniform cubic does.
            var spline = (GeoSpline2)Imp.Only(scene).ToGeometry();
            Assert.Equal(new[] { 0.0, 0, 0, 0, 1, 2, 3, 3, 3, 3 }, spline.Knots.ToArray());
            Assert.True(spline.StartPoint.DistanceTo(new GeoPoint2(500.0 / 6.0, 100.0 / 6.0)) < 1E-9);
            Assert.True(spline.EndPoint.DistanceTo(new GeoPoint2(100.0 / 6.0, 100.0 / 6.0)) < 1E-9);
        }

        [Fact]
        public void ASplineWithTooFewWeights_IsLeftOutAndSaid()
        {
            string few = G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 70, 12, 71, 2, 72, 6, 73, 3, 74, 0, 40, 0, 40, 0, 40, 0, 40, 1, 40, 1, 40, 1, 41, 1, 41, 0.5, 10, 0, 20, 0, 30, 0, 10, 50, 20, 50, 30, 0, 10, 100, 20, 0, 30, 0);
            Scene scene = ReadEntities(few);

            Assert.Empty(scene.Items);
            Assert.Contains(scene.LoadWarnings, w => w.Contains("1 SPLINE was not read: its degree, knots, weights or fit points do not make a spline the library takes"));
        }
    }
}
