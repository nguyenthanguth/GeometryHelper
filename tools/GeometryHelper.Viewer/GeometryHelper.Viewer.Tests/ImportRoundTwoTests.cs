using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Geometry;
using GeometryHelper.Viewer.Scripting;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What the second review of the readers and the clash check asked for: SVG read as a stream, numbers that cannot be read, the extrusion of a 2D polyline,
    /// MTEXT, the cap of the clash findings, and the sample that writes the library's files.
    /// </summary>
    public class ImportRoundTwoTests
    {
        private static string G(params object[] pairs)
        {
            var text = new StringBuilder();

            for (int i = 0; i < pairs.Length; i += 2)
            {
                text.Append(pairs[i]).Append('\n').Append(Convert.ToString(pairs[i + 1], CultureInfo.InvariantCulture)).Append('\n');
            }

            return text.ToString();
        }

        private static Scene ReadDxf(string entities)
            => SceneImport.ReadText(G(0, "SECTION", 2, "ENTITIES") + entities + G(0, "ENDSEC", 0, "EOF"), "dxf");

        private static GeoSolid3 Plate(double x, double y, double z, double sx, double sy, double sz)
            => GeoSolid3.Extrude(
                new GeoPolygon3(new GeoPoint3(x, y, z), new GeoPoint3(x + sx, y, z), new GeoPoint3(x + sx, y + sy, z), new GeoPoint3(x, y + sy, z)),
                new GeoVector3(0, 0, sz));

        #region SVG

        private static string Nested(int depth)
            => "<svg xmlns=\"http://www.w3.org/2000/svg\">" + string.Concat(Enumerable.Repeat("<g>", depth)) + "<circle cx=\"1\" cy=\"2\" r=\"3\" stroke=\"red\"/>" + string.Concat(Enumerable.Repeat("</g>", depth)) + "</svg>";

        [Fact]
        public void APictureNestedTooDeep_IsRefused_AndOneAtTheLimitIsRead()
        {
            Assert.Single(SceneImport.ReadText(Nested(SvgReader.MaxDepth - 2), "svg").Items);

            FormatException e = Assert.Throws<FormatException>(() => SceneImport.ReadText(Nested(SvgReader.MaxDepth + 50), "svg"));
            Assert.Contains("nested more than 256", e.Message);
            Assert.Throws<FormatException>(() => SceneImport.ReadText(Nested(100000), "svg"));
        }

        [Fact]
        public void ATextNestedTooDeep_IsRefusedToo()
        {
            string deep = "<svg xmlns=\"http://www.w3.org/2000/svg\"><text transform=\"translate(1 2)\">" + string.Concat(Enumerable.Repeat("<tspan>", 400)) + "x" + string.Concat(Enumerable.Repeat("</tspan>", 400)) + "</text></svg>";
            Assert.Throws<FormatException>(() => SceneImport.ReadText(deep, "svg"));
        }

        [Fact]
        public void AnSvgOfManyElements_IsReadAsAStream_AndStopsWhenCancelled()
        {
            var text = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\">");

            for (int i = 0; i < 20000; i++)
            {
                text.Append("<circle cx=\"").Append(i).Append("\" cy=\"0\" r=\"1\" stroke=\"red\"/>");
            }

            text.Append("</svg>");

            Assert.Equal(20000, SceneImport.ReadText(text.ToString(), "svg").Items.Count);

            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.Throws<OperationCanceledException>(() => SceneImport.ReadText(text.ToString(), "svg", null, source.Token));
            }
        }

        [Fact]
        public void TheWordsOfAText_AreAllThatIsInIt_HoweverItIsMarkedUp()
        {
            Scene scene = SceneImport.ReadText("<svg xmlns=\"http://www.w3.org/2000/svg\"><text transform=\"translate(1 2) scale(1,-1)\" font-size=\"3\" fill=\"red\">one <tspan>two</tspan> &lt;three&gt;</text></svg>", "svg");

            Assert.Equal("one two <three>", Imp.Only(scene).Data.GetProperty("text").GetString());
        }

        [Fact]
        public void ANumberThatCannotBeRead_LeavesThePathOut_AndNeverBecomesNought()
        {
            Scene scene = SceneImport.ReadText("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M 0 0 L 1e999 5\" stroke=\"black\"/><path d=\"M 0 0 L 5 5\" stroke=\"black\"/></svg>", "svg");

            SceneItem item = Assert.Single(scene.Items);
            Imp.SameBits(new[] { 0.0, 0, 5, 5 }, Imp.Of(item));
            Assert.Contains(scene.LoadWarnings, w => w.Contains("the number '1e999' cannot be read as a finite number"));
        }

        [Fact]
        public void AnArcOfAWholeTurn_IsWrittenAsACircle_AndReadBackAsOne()
        {
            Scene scene = SceneImport.ReadText(new GeometryHelper.Export.SvgWriter().Add(new GeoArc2(new GeoPoint2(0, 0), 10, 0.0, 2.0 * Math.PI), "black").ToString(), "svg");

            SceneItem item = Assert.Single(scene.Items);
            Assert.Equal("GeoCircle2", item.Kind);
            Imp.SameBits(new[] { 0.0, 0.0, 10.0 }, Imp.Of(item));
            Assert.Empty(scene.LoadWarnings);
        }

        [Fact]
        public void APathOfAnArcThatEndsWhereItStarts_IsLeftOutAndSaid()
        {
            Scene scene = SceneImport.ReadText("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M 10 0 A 10 10 0 1 1 10 0\" stroke=\"black\" fill=\"none\"/></svg>", "svg");

            Assert.Empty(scene.Items);
            Assert.Contains(scene.LoadWarnings, w => w.Contains("an arc that ends where it starts"));
        }

        [Theory]
        [InlineData(50.0, 1E-6)]
        [InlineData(50.0, 1E-3)]
        [InlineData(1000.0, 1E-5)]
        public void AnArcNearAHalfCircle_IsReadWithinWhatSixPlacesCanTell(double radius, double gap)
        {
            // Near a half circle the sweep cannot be told from the radius, the chord and six places: the middle of the arc is where the arc is to within
            // radius times sqrt(2 x 1.5E-6 / radius), which is 0.012 at 50 and 0.055 at 1 000, and the reading is a half circle where the arc is within that.
            var arc = new GeoArc2(new GeoPoint2(10, 20), radius, 0.3, 0.3 + Math.PI - gap);
            Scene scene = SceneImport.ReadText(new GeometryHelper.Export.SvgWriter().Add(arc, "black").ToString(), "svg");
            var back = (GeoArc2)scene.Rebuild(Imp.Only(scene), out _);
            GeoPoint2 Mid(GeoArc2 a) => new GeoPoint2(a.Center.X + (a.Radius * Math.Cos(a.StartAngle + (a.SweptAngle / 2.0))), a.Center.Y + (a.Radius * Math.Sin(a.StartAngle + (a.SweptAngle / 2.0))));

            double allowed = radius * Math.Sqrt(2.0 * 1.5E-6 / radius) * 2.0 + 1E-5;
            Assert.True(Mid(arc).DistanceTo(Mid(back)) <= allowed, "the middle moved " + Mid(arc).DistanceTo(Mid(back)));
        }

        #endregion

        #region DXF numbers

        [Fact]
        public void ANumberThatCannotBeRead_LeavesTheEntityOutAndSaysSo_NeverBecomingNought()
        {
            string good = G(0, "POINT", 8, "0", 10, 1, 20, 2);
            string entities = G(0, "LWPOLYLINE", 8, "0", 90, 2, 70, 0, 10, "abc", 20, 5, 10, 3, 20, 4)
                + G(0, "CIRCLE", 8, "0", 10, 0, 20, 0, 40, "1E999")
                + G(0, "LINE", 8, "0", 10, 0, 20, 0, 11, "x", 21, 1)
                + G(0, "POLYLINE", 8, "0", 70, 0) + G(0, "VERTEX", 10, 0, 20, 0) + G(0, "VERTEX", 10, 1, 20, "1.2.3") + G(0, "SEQEND")
                + G(0, "ARC", 8, "0", 10, 0, 20, 0, 40, 1, 50, 0, 51, 90, 62, "red")
                + good;
            Scene scene = ReadDxf(entities);

            // A colour that cannot be read is of the look, not the shape: the ARC is read without it, and said.
            Assert.Equal(new[] { "GeoArc2", "GeoPoint2" }, scene.Items.Select(i => i.Kind).ToArray());
            Assert.Null(scene.Items[0].Color);
            Assert.Contains(scene.LoadWarnings, w => w.Contains("1 LWPOLYLINE entity(ies) are not read"));
            Assert.Contains(scene.LoadWarnings, w => w.Contains("1 CIRCLE entity(ies) are not read"));
            Assert.Contains(scene.LoadWarnings, w => w.Contains("1 LINE entity(ies) are not read"));
            Assert.Contains(scene.LoadWarnings, w => w.Contains("1 POLYLINE entity(ies) are not read"));
            Assert.Contains(scene.LoadWarnings, w => w.Contains("1 group of the colour, lineweight, true colour or thickness of an entity could not be read"));
        }

        #endregion

        #region DXF polylines in an extrusion

        [Fact]
        public void ATwoDimensionalPolylineInAnExtrusion_IsTakenToTheWorld_OnTheFloorAsAChainOfThePlane()
        {
            string polyline = G(0, "POLYLINE", 8, "0", 70, 0, 210, 0, 220, 0, 230, -1) + G(0, "VERTEX", 10, 1, 20, 2) + G(0, "VERTEX", 10, 3, 20, 4) + G(0, "SEQEND");
            SceneItem item = Imp.Only(ReadDxf(polyline));

            // The extrusion straight down turns x over and keeps the chain on the floor.
            Assert.Equal("GeoPolyline2", item.Kind);
            Imp.SameBits(new[] { -1.0, 2, -3, 4 }, Imp.Of(item));
        }

        [Fact]
        public void ATwoDimensionalPolylineWithBulgesAndAnElevation_InAnExtrusion_IsAChainWithArcsAboutItsNormal()
        {
            string polyline = G(0, "POLYLINE", 8, "0", 30, 5, 70, 1, 210, 0, 220, 0, 230, -1)
                + G(0, "VERTEX", 10, 1, 20, 2, 42, 0.5) + G(0, "VERTEX", 10, 3, 20, 4) + G(0, "VERTEX", 10, 5, 20, 1) + G(0, "SEQEND");
            SceneItem item = Imp.Only(ReadDxf(polyline));

            Assert.Equal("GeoPolygonArc3", item.Kind);

            // The elevation is along the extrusion: z = 5 in the object's coordinates is z = -5 in the world. Only the arc edge has a
            // normal; the straight edges have none.
            Imp.SameBits(new[] { -1.0, 2, -5, -3, 4, -5, -5, 1, -5, 0.5, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, 0 }, Imp.Of(item));
        }

        [Fact]
        public void ALightweightPolylineInAnExtrusion_IsTakenToTheWorldToo_AndOneOnTheFloorIsNot()
        {
            string tilted = G(0, "LWPOLYLINE", 8, "0", 90, 2, 70, 0, 38, 2, 210, 0, 220, 0, 230, -1, 10, 1, 20, 2, 10, 3, 20, 4);
            string floor = G(0, "LWPOLYLINE", 8, "0", 90, 2, 70, 0, 10, 1, 20, 2, 10, 3, 20, 4);
            Scene scene = ReadDxf(tilted + floor);

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(new[] { "GeoPolyline3", "GeoPolyline2" }, scene.Items.Select(i => i.Kind).ToArray());
            Imp.SameBits(new[] { -1.0, 2, -2, -3, 4, -2 }, Imp.Of(scene.Items[0]));
        }

        #endregion

        #region MTEXT

        // The rules that take the formatting out of an MTEXT moved into the library with its reading of MTEXT; their cases are
        // DxfReaderTextTests.TheFormattingOfAnMText_GoesWithoutTakingAnyWordWithIt.

        [Fact]
        public void AnMText_IsTheWordsInItsLines_AndItsEscapesAreDecoded()
        {
            string mtext = G(0, "MTEXT", 8, "t", 10, 1, 20, 2, 30, 0, 40, 2, 3, "\\Lline one\\l\\P", 1, "line \\U+00E9");
            Assert.Equal("line one\nline \u00E9", Imp.Only(ReadDxf(mtext)).Data.GetProperty("text").GetString());
        }

        #endregion

        #region The clash findings

        private static Scene SlabWithBolts(bool slabFirst)
        {
            var writer = new SceneWriter("slab");

            if (slabFirst)
            {
                writer.Add(Plate(0, 0, 0, 3000, 100, 20), name: "slab");
            }

            for (int i = 0; i < 300; i++)
            {
                writer.Add(Plate(i * 10, 40, 10, 5, 5, 30), name: "bolt " + i.ToString(CultureInfo.InvariantCulture));
            }

            if (!slabFirst)
            {
                writer.Add(Plate(0, 0, 0, 3000, 100, 20), name: "slab");
            }

            return writer.Scene;
        }

        [Fact]
        public void TheCapOfTheClashFindings_IsOneForTheWholeRun_AndDoesNotDependOnTheOrderOfTheScene()
        {
            var kept = new List<string>();

            foreach (bool slabFirst in new[] { true, false })
            {
                Scene scene = SlabWithBolts(slabFirst);
                DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Clash);
                DiagnosticFinding[] hard = Diag.Of(report, DiagnosticKind.HardClash).ToArray();
                DiagnosticFinding note = Assert.Single(Diag.Of(report, DiagnosticKind.Note));

                Assert.Equal(200, hard.Length);
                Assert.Contains("100 more pairs of bodies are not listed: only the first 200 findings", note.Message);
                kept.Add(string.Join(",", hard.Select(f => Math.Round(f.Size, 6).ToString("R", CultureInfo.InvariantCulture) + "@" + Math.Round(f.At.X, 6).ToString("R", CultureInfo.InvariantCulture)).OrderBy(x => x, StringComparer.Ordinal)));
            }

            // The first 200 in order of place are the bolts at x = 2.5 to 1992.5, whichever way the scene is ordered.
            Assert.Equal(kept[0], kept[1]);
        }

        [Fact]
        public void TheCapCanBeAskedFor_AndCountsTheRest()
        {
            Scene scene = SlabWithBolts(true);
            Primitives primitives = Primitives.Build(scene);
            DiagnosticReport report = SceneDiagnostics.Run(scene, primitives, null, null, new DiagnosticOptions { Checks = DiagnosticChecks.Clash, MaxClashFindings = 5 }, CancellationToken.None);

            Assert.Equal(5, Diag.Of(report, DiagnosticKind.HardClash).Count());
            Assert.Contains("295 more pairs", Assert.Single(Diag.Of(report, DiagnosticKind.Note)).Message);
        }

        [Fact]
        public void WithOneItemSelected_OnlyTheItemsNearItAreRebuilt()
        {
            var writer = new SceneWriter("near");
            writer.Add(Plate(0, 0, 0, 10, 10, 10), name: "a");
            writer.Add(Plate(5, 0, 0, 10, 10, 10), name: "b");
            Scene scene = writer.Scene;

            // A body far away whose faces the library refuses: its outline is drawn, so it has a place, and rebuilt it would be a note.
            scene.Items.Add(Diag.Raw("GeoSolid3", "{\"faces\":[{\"outer\":[[5000,0,0],[5001,0,0]],\"holes\":[]}]}", "far and bad"));

            DiagnosticReport one = Diag.Run(scene, DiagnosticChecks.Clash, true, 0);
            DiagnosticReport all = Diag.Run(scene, DiagnosticChecks.Clash, true);

            Assert.Single(Diag.Of(one, DiagnosticKind.HardClash));
            Assert.DoesNotContain(one.Findings, f => f.ItemName == "far and bad");
            Assert.Single(Diag.Of(all, DiagnosticKind.HardClash));
            Assert.Contains(all.Findings, f => f.Kind == DiagnosticKind.Note && f.ItemName == "far and bad");
        }

        #endregion

        #region The sample

        [Fact]
        public async Task AScriptKnowsItsOwnFolder_SoTheSampleWritesBesideItselfWhereverItIsRun()
        {
            using (var folder = new TempFolder())
            {
                string script = folder.Combine("where.csx");
                File.WriteAllText(script, "string HereFile([System.Runtime.CompilerServices.CallerFilePath] string path = \"\") => path;\nNote(HereFile());\n");

                ScriptRunResult result = await ScriptHost.RunAsync(File.ReadAllText(script), script, line => { }, CancellationToken.None);

                Assert.True(result.Succeeded, string.Join("\n", result.Errors));
                Assert.Equal(Path.GetFullPath(script), Assert.Single(result.Notes));
            }
        }

        #endregion
    }
}
