using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The readers on files written by hand: the forms other programs write, what is left out and said, and what is refused.
    /// </summary>
    public class ImportReaderTests
    {
        #region Helpers

        /// <summary>
        /// Groups of a DXF as code and value pairs, written one to a line.
        /// </summary>
        private static string G(params object[] pairs)
        {
            var text = new StringBuilder();

            for (int i = 0; i < pairs.Length; i += 2)
            {
                text.Append(pairs[i]).Append('\n').Append(Convert.ToString(pairs[i + 1], CultureInfo.InvariantCulture)).Append('\n');
            }

            return text.ToString();
        }

        private static string Dxf(string entities, string tables = "", string blocks = "")
            => G(0, "SECTION", 2, "TABLES") + tables + G(0, "ENDSEC", 0, "SECTION", 2, "BLOCKS") + blocks + G(0, "ENDSEC", 0, "SECTION", 2, "ENTITIES") + entities + G(0, "ENDSEC", 0, "EOF");

        private static Scene ReadDxf(string text) => SceneImport.ReadText(text, "dxf");

        private static Scene ReadObj(string text) => SceneImport.ReadText(text, "obj");

        private static Scene ReadWkt(string text) => SceneImport.ReadText(text, "wkt");

        private static Scene ReadSvg(string body)
            => SceneImport.ReadText("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 100 100\">\n<g transform=\"scale(1,-1)\">\n" + body + "\n</g>\n</svg>\n", "svg");

        private static string Layer(string name, params object[] more) => G(0, "LAYER", 2, name, 70, 0) + G(more);

        private static void HasWarning(Scene scene, string part)
            => Assert.Contains(scene.LoadWarnings, w => w.IndexOf(part, StringComparison.Ordinal) >= 0);

        #endregion

        #region DXF

        [Fact]
        public void ADxfGroupCode_MayBePaddedWithSpacesOrTabs_AndTheLinesMayEndInCarriageReturns()
        {
            string text = Dxf("  0\r\nLINE\r\n 8 \r\nwalls\r\n\t10\r\n1.5\r\n 20\r\n2.5\r\n30\r\n0\r\n11\r\n4.5\r\n21\r\n6.5\r\n31\r\n9\r\n");
            SceneItem item = Imp.Only(ReadDxf(text.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n")));

            Assert.Equal("GeoLine3", item.Kind);
            Assert.Equal("walls", item.Layer);
            Imp.SameBits(new[] { 1.5, 2.5, 0.0, 4.5, 6.5, 9.0 }, Imp.Of(item));
        }

        [Fact]
        public void TheLayersOfTheTable_GiveTheirColoursAndWhetherTheyAreShown()
        {
            string tables = G(0, "TABLE", 2, "LAYER", 70, 5)
                + Layer("red", 62, 1)
                + Layer("true", 62, 7, 420, 0x3070C0)
                + Layer("plain", 62, 7)
                + G(0, "LAYER", 2, "off", 70, 0, 62, -3)
                + G(0, "LAYER", 2, "frozen", 70, 1, 62, 5)
                + G(0, "ENDTAB");
            string entities = string.Concat(new[] { "red", "true", "plain", "off", "frozen" }.Select(name => G(0, "POINT", 8, name, 10, 1, 20, 2, 30, 3)));

            Scene scene = ReadDxf(Dxf(entities, tables));

            Layer Find(string name) => scene.FindLayer(name);

            Assert.Equal("#FF0000", Find("red").Color);
            Assert.Equal("#3070C0", Find("true").Color);
            Assert.True(Find("red").Visible && Find("true").Visible && Find("plain").Visible);
            Assert.False(Find("off").Visible);
            Assert.False(Find("frozen").Visible);
            Assert.Contains(Find("plain").Color, new[] { "#1F77B4", "#FF7F0E", "#2CA02C", "#D62728", "#9467BD", "#8C564B", "#E377C2", "#7F7F7F", "#BCBD22", "#17BECF" });
            HasWarning(scene, "the layer 'off' is turned off");
            HasWarning(scene, "the layer 'frozen' is frozen");
        }

        [Fact]
        public void TheColourOfAnEntity_IsItsOwnWhenItHasOne()
        {
            string entities = G(0, "POINT", 10, 1, 20, 1, 62, 3)
                + G(0, "POINT", 10, 2, 20, 1, 420, 0x112233)
                + G(0, "POINT", 10, 3, 20, 1, 62, 256)
                + G(0, "POINT", 10, 4, 20, 1, 62, 7)
                + G(0, "POINT", 10, 5, 20, 1);

            Scene scene = ReadDxf(Dxf(entities));

            Assert.Equal(new string[] { "#00FF00", "#112233", null, null, null }, scene.Items.Select(i => i.Color).ToArray());
        }

        [Theory]
        [InlineData(1, "#FF0000")]
        [InlineData(2, "#FFFF00")]
        [InlineData(5, "#0000FF")]
        [InlineData(8, "#414141")]
        [InlineData(10, "#FF0000")]
        [InlineData(11, "#FF7F7F")]
        [InlineData(12, "#CC0000")]
        [InlineData(30, "#FF7F00")]
        [InlineData(250, "#333333")]
        [InlineData(255, "#FFFFFF")]
        public void AnAutoCadColourIndex_IsItsColour(int index, string expected) => Assert.Equal(expected, DxfReader.AciColour(index));

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(256)]
        [InlineData(-1)]
        public void WhiteByBlockAndByLayer_AreNoColourAtAll(int index) => Assert.Null(DxfReader.AciColour(index));

        [Fact]
        public void AnEntityThisReaderDoesNotTake_IsCountedAndSaidOnce()
        {
            string entities = G(0, "SPLINE", 8, "0") + G(0, "SPLINE", 8, "0") + G(0, "HATCH", 8, "0") + G(0, "POINT", 10, 1, 20, 2);
            Scene scene = ReadDxf(Dxf(entities));

            Assert.Single(scene.Items);
            HasWarning(scene, "2 SPLINE entity(ies) are not read");
            HasWarning(scene, "1 HATCH entity(ies) are not read");
        }

        [Fact]
        public void PaperSpace_AndBlocks_AreLeftOutAndSaid()
        {
            string entities = G(0, "POINT", 10, 1, 20, 2, 67, 1) + G(0, "POINT", 10, 3, 20, 4);
            string blocks = G(0, "BLOCK", 2, "B") + G(0, "LINE", 10, 0, 20, 0, 11, 1, 21, 1) + G(0, "ENDBLK");
            Scene scene = ReadDxf(Dxf(entities, string.Empty, blocks));

            Assert.Single(scene.Items);
            HasWarning(scene, "1 entity(ies) in paper space");
            HasWarning(scene, "1 entity(ies) in the BLOCKS section");
        }

        [Fact]
        public void ARecordWithoutItsCoordinates_IsLeftOutAndSaid()
        {
            Scene scene = ReadDxf(Dxf(G(0, "LINE", 10, 1) + G(0, "CIRCLE", 10, 1, 20, 2)));

            Assert.Empty(scene.Items);
            HasWarning(scene, "a LINE without its coordinates");
            HasWarning(scene, "a CIRCLE without its coordinates");
        }

        [Fact]
        public void A3DFace_KeepsAFourthCorner_AndFoldsARepeatedOne_AndDrawsItsHiddenEdgesToo()
        {
            string quad = G(0, "3DFACE", 8, "m", 10, 0, 20, 0, 30, 0, 11, 1, 21, 0, 31, 0, 12, 1, 22, 1, 32, 0, 13, 0, 23, 1, 33, 0, 70, 3);
            string triangle = G(0, "3DFACE", 8, "m", 10, 1, 20, 0, 30, 0, 11, 2, 21, 0, 31, 0, 12, 1, 22, 1, 32, 0, 13, 1, 23, 1, 33, 0);
            Scene scene = ReadDxf(Dxf(quad + triangle));
            SceneItem item = Assert.Single(scene.Items);

            Assert.Equal("m mesh", item.Name);
            Assert.Equal(new[] { 4, 3 }, Imp.MeshFaces(item).Select(f => f.Length).ToArray());
            Assert.Equal(5, Imp.MeshPoints(item).Length);
            Assert.Empty(scene.LoadWarnings);
        }

        [Fact]
        public void APolyfaceMesh_IsAMesh_WithTheFacesItNames()
        {
            string polyline = G(0, "POLYLINE", 8, "pf", 66, 1, 70, 64, 71, 4, 72, 2);
            string vertices = string.Concat(new[] { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 1.0, 1.0 }, new[] { 0.0, 1.0 } }
                .Select(v => G(0, "VERTEX", 8, "pf", 10, v[0], 20, v[1], 30, 0, 70, 192)));
            string faces = G(0, "VERTEX", 8, "pf", 10, 0, 20, 0, 30, 0, 70, 128, 71, 1, 72, 2, 73, -3, 74, 0)
                + G(0, "VERTEX", 8, "pf", 10, 0, 20, 0, 30, 0, 70, 128, 71, 1, 72, 3, 73, 4, 74, 0);
            Scene scene = ReadDxf(Dxf(polyline + vertices + faces + G(0, "SEQEND")));
            SceneItem item = Imp.Only(scene);

            Assert.Equal("GeoMesh3", item.Kind);
            Assert.Equal(4, Imp.MeshPoints(item).Length);
            Assert.Equal(new[] { 3, 3 }, Imp.MeshFaces(item).Select(f => f.Length).ToArray());
        }

        [Fact]
        public void APolygonMesh_IsAGridOfQuads()
        {
            string polyline = G(0, "POLYLINE", 8, "pm", 66, 1, 70, 16, 71, 3, 72, 3);
            var vertices = new StringBuilder();

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    vertices.Append(G(0, "VERTEX", 8, "pm", 10, i, 20, j, 30, i * j, 70, 64));
                }
            }

            SceneItem item = Imp.Only(ReadDxf(Dxf(polyline + vertices + G(0, "SEQEND"))));

            Assert.Equal(9, Imp.MeshPoints(item).Length);
            Assert.Equal(4, Imp.MeshFaces(item).Count);
        }

        [Fact]
        public void ATwoDimensionalPolylineWithBulges_IsAChainWithArcs_AndAThreeDimensionalOneIsAClosedChain()
        {
            string flat = G(0, "POLYLINE", 8, "a", 66, 1, 70, 1)
                + G(0, "VERTEX", 10, 0, 20, 0, 42, 0.5) + G(0, "VERTEX", 10, 10, 20, 0) + G(0, "VERTEX", 10, 10, 20, 10) + G(0, "SEQEND");
            string solid = G(0, "POLYLINE", 8, "b", 66, 1, 70, 9)
                + G(0, "VERTEX", 10, 0, 20, 0, 30, 1, 70, 32) + G(0, "VERTEX", 10, 10, 20, 0, 30, 2, 70, 32) + G(0, "VERTEX", 10, 10, 20, 10, 30, 3, 70, 32) + G(0, "SEQEND");
            Scene scene = ReadDxf(Dxf(flat + solid));

            Assert.Equal(new[] { "GeoPolygonArc2", "GeoPolygon3" }, scene.Items.Select(i => i.Kind).ToArray());
            Imp.SameBits(new[] { 0.0, 0, 10, 0, 10, 10, 0.5, 0, 0 }, Imp.Of(scene.Items[0]));
            Imp.SameBits(new[] { 0.0, 0, 1, 10, 0, 2, 10, 10, 3 }, Imp.Of(scene.Items[1]));
        }

        [Fact]
        public void ALightweightPolylineAtAHeight_IsInSpace()
        {
            SceneItem item = Imp.Only(ReadDxf(Dxf(G(0, "LWPOLYLINE", 8, "a", 90, 3, 70, 0, 38, 5, 10, 0, 20, 0, 10, 4, 20, 0, 10, 4, 20, 3))));

            Assert.Equal("GeoPolyline3", item.Kind);
            Imp.SameBits(new[] { 0.0, 0, 5, 4, 0, 5, 4, 3, 5 }, Imp.Of(item));
        }

        [Fact]
        public void ACircleWithAnExtrusion_IsInSpace_ItsCentreTakenToTheWorldByTheArbitraryAxisRule()
        {
            SceneItem item = Imp.Only(ReadDxf(Dxf(G(0, "CIRCLE", 8, "a", 10, 5, 20, 7, 30, 3, 40, 2.5, 210, 0, 220, 0, 230, -1))));

            Assert.Equal("GeoCircle3", item.Kind);
            Imp.SameBits(new[] { -5.0, 7, -3, 0, 0, -1, 2.5 }, Imp.Of(item));
        }

        [Fact]
        public void AnArcWithAnExtrusion_IsAnArcInSpace_OnTheSamePoints()
        {
            string entity = G(0, "ARC", 8, "a", 10, 5, 20, 7, 30, 3, 40, 2.5, 50, 10, 51, 100, 210, 0, 220, 0, 230, -1);
            SceneItem item = Imp.Only(ReadDxf(Dxf(entity)));

            Assert.Equal("GeoArc3", item.Kind);

            var arc = (GeoArc3)item.ToGeometry();
            double a = 10.0 * Math.PI / 180.0;
            double b = 100.0 * Math.PI / 180.0;
            var start = new GeoPoint3(-(5 + (2.5 * Math.Cos(a))), 7 + (2.5 * Math.Sin(a)), -3);
            var end = new GeoPoint3(-(5 + (2.5 * Math.Cos(b))), 7 + (2.5 * Math.Sin(b)), -3);

            Assert.True(arc.StartPoint.DistanceTo(start) < 1E-9);
            Assert.True(arc.EndPoint.DistanceTo(end) < 1E-9);
            Assert.Equal(2.5, arc.Radius, 9);
        }

        [Fact]
        public void AnArcOfAnEllipse_IsLeftOutAndSaid_AndAWholeOneIsNot()
        {
            string whole = G(0, "ELLIPSE", 8, "a", 10, 0, 20, 0, 30, 0, 11, 10, 21, 0, 31, 0, 210, 0, 220, 0, 230, 1, 40, 0.5, 41, 0, 42, Math.PI * 2.0);
            string part = G(0, "ELLIPSE", 8, "a", 10, 0, 20, 0, 30, 0, 11, 10, 21, 0, 31, 0, 210, 0, 220, 0, 230, 1, 40, 0.5, 41, 0, 42, 3.0);
            Scene scene = ReadDxf(Dxf(whole + part));
            SceneItem item = Assert.Single(scene.Items);

            Assert.Equal("GeoEllipse2", item.Kind);
            Imp.SameBits(new[] { 0.0, 0, 1, 0, 10, 5 }, Imp.Of(item));
            HasWarning(scene, "only an arc of an ellipse");
        }

        [Fact]
        public void ALabel_IsText_WithItsEscapesDecoded()
        {
            string entities = G(0, "TEXT", 8, "t", 10, 1.5, 20, 2.5, 30, 0, 40, 3.5, 1, "45%%d \\U+00E9")
                + G(0, "MTEXT", 8, "t", 10, 4, 20, 5, 30, 0, 40, 2, 3, "{\\fArial|b1;first", 1, "}\\Psecond");
            Scene scene = ReadDxf(Dxf(entities));

            Assert.Equal("45\u00B0 \u00E9", scene.Items[0].Data.GetProperty("text").GetString());
            Assert.Equal(3.5, scene.Items[0].Data.GetProperty("height").GetDouble());
            Assert.Equal("first\nsecond", scene.Items[1].Data.GetProperty("text").GetString());
        }

        [Fact]
        public void ALayerTheTableDoesNotName_IsMadeWhereItIsFirstUsed_InTheOrderOfUse()
        {
            string entities = G(0, "POINT", 8, "zeta", 10, 1, 20, 1) + G(0, "POINT", 8, "alpha", 10, 2, 20, 1) + G(0, "POINT", 8, "zeta", 10, 3, 20, 1);
            Scene scene = ReadDxf(Dxf(entities));

            Assert.Equal(new[] { "zeta", "alpha" }, scene.Layers.Select(l => l.Name).ToArray());
        }

        [Fact]
        public void ItemsOfTheFile_CarryWhereTheyCameFrom()
        {
            SceneItem item = Imp.Only(ReadDxf(Dxf(G(0, "POINT", 5, "1A3", 10, 1, 20, 2))));

            Assert.Equal("DXF POINT", item.Properties["source"]);
            Assert.Equal("1A3", item.Properties["handle"]);
            Assert.True(item.AutoName);
        }

        [Fact]
        public void ABinaryDxf_AndATextThatIsNoDxf_AreRefused()
        {
            Assert.Throws<FormatException>(() => ReadDxf("AutoCAD Binary DXF\r\n\u001a\0"));

            FormatException e = Assert.Throws<FormatException>(() => ReadDxf("this is not\na dxf file\n"));
            Assert.Contains("group code", e.Message);
        }

        [Fact]
        public void ADxfThatEndsInTheMiddle_KeepsWhatWasRead_AndSaysWhereItStopped()
        {
            string text = Dxf(G(0, "POINT", 10, 1, 20, 2) + G(0, "POINT", 10, 3, 20, 4)).Replace("0\nENDSEC\n0\nEOF\n", "garbage\n");
            Scene scene = ReadDxf(text);

            Assert.Equal(2, scene.Items.Count);
            HasWarning(scene, "reading stopped");
        }

        #endregion

        #region OBJ

        [Fact]
        public void AFaceNamesItsVerticesInAnyOfTheForms_AndCountsBackFromTheLastWithANegativeNumber()
        {
            string text = "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nvt 0 0\nvn 0 0 1\nf 1/1/1 2//1 3/1 4\nf -4 -3 -2\n";
            Scene scene = ReadObj(text);
            SceneItem item = Assert.Single(scene.Items);

            Assert.Equal(new[] { 4, 3 }, Imp.MeshFaces(item).Select(f => f.Length).ToArray());
            Assert.Equal(Imp.Key(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(1, 1, 0)), Imp.Key(Imp.MeshFaces(item)[1]));
            HasWarning(scene, "1 'vt' statement(s)");
            HasWarning(scene, "1 'vn' statement(s)");
        }

        [Fact]
        public void GroupsAreLayers_AndEachGroupOrObjectIsItsOwnItem()
        {
            string text = "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\ng walls\nf 1 2 3\no wall two\nf 1 2 4\ng floor\nf 1 3 4\nusemtl steel\n";
            Scene scene = ReadObj(text);

            Assert.Equal(new[] { "walls", "wall two", "floor" }, scene.Items.Select(i => i.Name).ToArray());
            Assert.Equal(new[] { "walls", "walls", "floor" }, scene.Items.Select(i => i.Layer).ToArray());
            Assert.Equal(new[] { "walls", "floor" }, scene.Layers.Select(l => l.Name).ToArray());
            HasWarning(scene, "'usemtl'");
        }

        [Fact]
        public void AnObjectKeepsOnlyTheVerticesItsFacesName_InTheOrderTheFileHasThem()
        {
            string text = "v 0 0 0\nv 9 9 9\nv 1 0 0\nv 0 1 0\no a\nf 4 3 1\n";
            SceneItem item = Imp.Only(ReadObj(text));

            Assert.Equal(new[] { new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 1, 0) }, Imp.MeshPoints(item));
            Assert.Equal(new[] { Imp.Key(new Vec3(0, 1, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 0)) }, Imp.Keys(item));
        }

        [Fact]
        public void LinesAndPoints_AreLinesChainsAndPoints_AndAnObjectWithSeveralThingsIsSeveralItems()
        {
            string text = "v 0 0 0\nv 1 0 0\nv 1 1 0\no x\nf 1 2 3\nl 1 2\nl 1 2 3\np 3\n";
            Scene scene = ReadObj(text);

            Assert.Equal(new[] { "GeoMesh3", "GeoLine3", "GeoPolyline3", "GeoPoint3" }, scene.Items.Select(i => i.Kind).ToArray());
            Assert.Equal(new[] { "x", "x 2", "x 3", "x 4" }, scene.Items.Select(i => i.Name).ToArray());
        }

        [Fact]
        public void AStatementThatNamesAVertexThatIsNotThere_IsLeftOutAndSaid()
        {
            Scene scene = ReadObj("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\nf 1 2 9\nf 1 2\nf 1 x 3\nl 1\n");

            Assert.Single(scene.Items);
            Assert.Single(Imp.MeshFaces(scene.Items[0]));
            HasWarning(scene, "vertex 9 is not there");
            HasWarning(scene, "'x' is not a vertex number");
            HasWarning(scene, "a face with fewer than three corners");
            HasWarning(scene, "a line with fewer than two vertices");
        }

        [Fact]
        public void AVertexWithAWeightOrAColour_IsAVertex_AndTheExtraIsSaid()
        {
            Scene scene = ReadObj("v 1 2 3 1\nv 4 5 6 0.5 0.5 0.5\nv 7 8 9\np 1 2 3\n");

            Assert.Equal(3, scene.Items.Count);
            Imp.SameBits(new[] { 4.0, 5, 6 }, Imp.Of(scene.Items[1]));
            HasWarning(scene, "1 vertex(es) carry a weight or a colour");
        }

        [Fact]
        public void ALineEndingInABackslash_ContinuesOnTheNext_AndWindowsLineEndsAreFine()
        {
            Scene scene = ReadObj("v 0 0 0\r\nv 1 0 0\r\nv 0 1 0\r\nf 1 \\\r\n2 3\r\n");

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(3, Imp.MeshFaces(Assert.Single(scene.Items))[0].Length);
        }

        [Fact]
        public void AnEmptyFile_IsAnEmptyScene()
        {
            Scene scene = ReadObj("# nothing\n\n");
            Assert.Empty(scene.Items);
            Assert.Empty(scene.LoadWarnings);
        }

        [Fact]
        public void AReadThatIsCancelled_Stops()
        {
            var text = new StringBuilder();

            for (int i = 0; i < 20000; i++)
            {
                text.Append("v ").Append(i).Append(" 0 0\n");
            }

            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.Throws<OperationCanceledException>(() => SceneImport.ReadText(text.ToString(), "obj", null, source.Token));
            }
        }

        #endregion

        #region WKT

        [Fact]
        public void TheModifiers_ZAndMAndZM_AndTheFormOfTheName_AreRead()
        {
            Scene scene = ReadWkt("point z (1 2 3)\nPOINTZ(4 5 6)\nPOINT ZM (7 8 9 10)\nPOINT M (11 12 13)\nPOINT (14 15)\nPOINT (16 17 18)\nSRID=4326;POINT(19 20)");

            Assert.Equal(new[] { "GeoPoint3", "GeoPoint3", "GeoPoint3", "GeoPoint2", "GeoPoint2", "GeoPoint3", "GeoPoint2" }, scene.Items.Select(i => i.Kind).ToArray());
            Imp.SameBits(new[] { 7.0, 8, 9 }, Imp.Of(scene.Items[2]));
            Imp.SameBits(new[] { 11.0, 12 }, Imp.Of(scene.Items[3]));
        }

        [Fact]
        public void ManyGeometries_InAnyLayout_WithComments_AreManyItems()
        {
            Scene scene = ReadWkt("# a comment\nPOINT (1 2) ; LINESTRING (0 0, 1 1)\n\n-- another\nPOLYGON ((0 0, 4 0, 4 4, 0 0))\n");

            Assert.Equal(new[] { "GeoPoint2", "GeoLine2", "GeoPolygon2" }, scene.Items.Select(i => i.Kind).ToArray());
            Assert.Empty(scene.LoadWarnings);
        }

        [Fact]
        public void ACollectionAndTheMultiForms_GiveTheirMembers()
        {
            Scene scene = ReadWkt("GEOMETRYCOLLECTION (POINT (1 2), LINESTRING (0 0, 1 1, 2 0), GEOMETRYCOLLECTION (POINT (3 4)))\nMULTIPOINT (1 2, 3 4)\nMULTIPOINT ((5 6), (7 8))\nMULTILINESTRING ((0 0, 1 1), EMPTY, (2 2, 3 3, 4 2))");

            Assert.Equal(
                new[] { "GeoPoint2", "GeoPolyline2", "GeoPoint2", "GeoPoint2", "GeoPoint2", "GeoPoint2", "GeoPoint2", "GeoLine2", "GeoPolyline2" },
                scene.Items.Select(i => i.Kind).ToArray());
            Assert.Empty(scene.LoadWarnings);
        }

        [Fact]
        public void EmptyGeometries_AreNothing()
        {
            Scene scene = ReadWkt("POINT EMPTY\nPOLYGON EMPTY\nMULTIPOLYGON Z EMPTY\nPOINT (1 2)");
            Assert.Single(scene.Items);
        }

        [Fact]
        public void ANumberIsWrittenAsAnyFloatIsRead()
        {
            Scene scene = ReadWkt("POINT (1e3 -.5)\nPOINT (+2 3.)\nPOINT (1E-300 0.1)");

            Imp.SameBits(new[] { 1000.0, -0.5 }, Imp.Of(scene.Items[0]));
            Imp.SameBits(new[] { 2.0, 3.0 }, Imp.Of(scene.Items[1]));
            Imp.SameBits(new[] { 1E-300, 0.1 }, Imp.Of(scene.Items[2]));
        }

        [Fact]
        public void ACurveThisReaderDoesNotKnow_IsLeftOutAndSaid_AndTheNextGeometryIsRead()
        {
            Scene scene = ReadWkt("CIRCULARSTRING (0 0, 1 1, 2 0)\nPOINT (5 6)");

            Assert.Single(scene.Items);
            HasWarning(scene, "CIRCULARSTRING is not read");
        }

        [Fact]
        public void ARingThatDoesNotClose_IsKeptAsItIs_AndSaid()
        {
            Scene scene = ReadWkt("POLYGON ((0 0, 4 0, 4 4))");

            Assert.Equal(3, DataJson.Points(Assert.Single(scene.Items).Data, "points", 2).Length);
            HasWarning(scene, "does not return to its first point");
        }

        [Fact]
        public void ATriangleAndATin_AreATriangleAndAMesh()
        {
            Scene scene = ReadWkt("TRIANGLE ((0 0, 4 0, 0 4, 0 0))\nTIN Z (((0 0 0, 1 0 0, 0 1 0, 0 0 0)), ((1 0 0, 1 1 0, 0 1 0, 1 0 0)))");

            Assert.Equal(new[] { "GeoTriangle2", "GeoMesh3" }, scene.Items.Select(i => i.Kind).ToArray());
            Assert.Equal(4, Imp.MeshPoints(scene.Items[1]).Length);
            Assert.Equal(2, Imp.MeshFaces(scene.Items[1]).Count);
        }

        [Fact]
        public void ABrokenStatement_AfterGoodOnes_StopsTheReadingWithTheOnesBeforeKept()
        {
            Scene scene = ReadWkt("POINT (1 2)\nPOINT (3 4\nPOINT (5 6)");

            Assert.Single(scene.Items);
            HasWarning(scene, "reading stopped at line 2, column 1");
        }

        [Fact]
        public void ATextThatIsNotWkt_IsRefusedWithWhereItWentWrong()
        {
            FormatException e = Assert.Throws<FormatException>(() => ReadWkt("POINT (1 2 3 4 5)"));
            Assert.Contains("line 1", e.Message);

            Assert.Throws<FormatException>(() => ReadWkt("hello world"));
            Assert.Throws<FormatException>(() => ReadWkt("POINT (1 2\n"));
        }

        [Fact]
        public void PointsOfTwoAndOfThreeNumbers_InOneGeometry_AreRefused()
        {
            Assert.Throws<FormatException>(() => ReadWkt("LINESTRING (0 0, 1 1 1)"));
        }

        #endregion

        #region SVG

        [Fact]
        public void APathMayRunItsNumbersTogether_AndRepeatACommandWithoutItsLetter()
        {
            Scene scene = ReadSvg("<path d=\"M0,0L10,0L10,10Z\" stroke=\"black\" fill=\"none\"/>\n<path d=\"M 0 0 10 0 10 10 0 10 Z\" stroke=\"black\" fill=\"none\"/>");

            Assert.Equal(new[] { "GeoPolygon2", "GeoPolygon2" }, scene.Items.Select(i => i.Kind).ToArray());
            Imp.SameBits(new[] { 0.0, 0, 10, 0, 10, 10 }, Imp.Of(scene.Items[0]));
            Assert.Equal(8, Imp.Of(scene.Items[1]).Count);
        }

        [Fact]
        public void WhatTheWriterDoesNotWrite_IsLeftOutAndSaid()
        {
            Scene scene = ReadSvg("<rect x=\"0\" y=\"0\" width=\"5\" height=\"5\"/>\n<path d=\"M 0 0 C 1 1 2 2 3 3\" stroke=\"black\"/>\n<path d=\"M 0 0 L 5 5\" stroke=\"black\" fill=\"none\"/>");

            Assert.Single(scene.Items);
            HasWarning(scene, "<rect> is not something");
            HasWarning(scene, "the path command 'C' is not one this reader knows");
        }

        [Fact]
        public void ATransformThatIsNotTheFlipOfTheWriter_IsNotAppliedAndIsSaid()
        {
            Scene scene = ReadSvg("<circle cx=\"1\" cy=\"2\" r=\"3\" transform=\"translate(5 5)\" stroke=\"black\" fill=\"none\"/>");

            Assert.Single(scene.Items);
            HasWarning(scene, "the transform of a circle");
        }

        [Fact]
        public void AnArcWhoseRadiiDiffer_IsAnArcOfItsFirstRadius_AndSaid()
        {
            Scene scene = ReadSvg("<path d=\"M 10 0 A 10 5 0 0 1 0 10\" stroke=\"black\" fill=\"none\"/>");

            Assert.Equal("GeoArc2", Assert.Single(scene.Items).Kind);
            HasWarning(scene, "whose two radii differ");
        }

        [Fact]
        public void TheColoursOfAPicture_AreNamesHexAndRgb_AndAnUnknownOneIsSaid()
        {
            Scene scene = ReadSvg(
                "<path d=\"M 0 0 L 1 1\" stroke=\"orange\" fill=\"none\"/>\n"
                + "<path d=\"M 0 0 L 1 2\" stroke=\"#fa0\" fill=\"none\"/>\n"
                + "<path d=\"M 0 0 L 1 3\" stroke=\"rgb(1, 2, 255)\" fill=\"none\"/>\n"
                + "<path d=\"M 0 0 L 1 4\" stroke=\"nonsense\" fill=\"none\"/>\n"
                + "<path d=\"M 0 0 L 1 5\" stroke=\"none\" fill=\"none\"/>");

            Assert.Equal(new string[] { "#FFA500", "#FFAA00", "#0102FF", null, null }, scene.Items.Select(i => i.Color).ToArray());
            HasWarning(scene, "the colour 'nonsense'");
        }

        [Fact]
        public void ADrawingThatIsNotXml_OrNotSvg_OrCarriesADocumentType_IsRefused()
        {
            Assert.Throws<FormatException>(() => SceneImport.ReadText("<svg", "svg"));
            Assert.Throws<FormatException>(() => SceneImport.ReadText("<html/>", "svg"));
            Assert.Throws<FormatException>(() => SceneImport.ReadText("<!DOCTYPE svg [<!ENTITY a \"b\">]><svg xmlns=\"http://www.w3.org/2000/svg\"/>", "svg"));
        }

        #endregion

        #region Opening a file

        [Fact]
        public void TheExtensions_AreTheFourFormats_InEitherCase()
        {
            Assert.Equal(new[] { ".obj", ".dxf", ".wkt", ".svg" }, SceneImport.Extensions.ToArray());
            Assert.True(SceneImport.IsImportable("C:\\a\\b.OBJ"));
            Assert.True(SceneImport.IsImportable("x.Dxf"));
            Assert.False(SceneImport.IsImportable("x.json"));
            Assert.False(SceneImport.IsImportable("x"));
            Assert.False(SceneImport.IsImportable(null));
        }

        [Fact]
        public void AFileIsReadByItsExtension_TitledWithItsName()
        {
            using (var folder = new TempFolder())
            {
                string obj = folder.Combine("plate.obj");
                File.WriteAllText(obj, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
                string wkt = folder.Combine("one.WKT");
                File.WriteAllText(wkt, "POINT (1 2)");

                Scene a = SceneImport.ReadFile(obj);
                Scene b = SceneImport.ReadFile(wkt);

                Assert.Equal("plate.obj", a.Title);
                Assert.Equal("one.WKT", b.Title);
                Assert.Single(a.Items);
                Assert.Single(b.Items);
                Assert.Throws<FormatException>(() => SceneImport.ReadFile(folder.Combine("x.txt")));
                Assert.Throws<ArgumentNullException>(() => SceneImport.ReadFile(null));
            }
        }

        [Fact]
        public void ADxfInTheWesternCodePage_IsReadAsIt_AndOneWithAByteOrderMarkToo()
        {
            using (var folder = new TempFolder())
            {
                string entity = G(0, "POINT", 8, "Fa\u00E7ade", 10, 1, 20, 2);
                string path = folder.Combine("west.dxf");
                File.WriteAllBytes(path, Encoding.GetEncoding("iso-8859-1").GetBytes(Dxf(entity)));
                string marked = folder.Combine("marked.dxf");
                File.WriteAllBytes(marked, new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(Dxf(entity))).ToArray());

                Assert.Equal("Fa\u00E7ade", Imp.Only(SceneImport.ReadFile(path)).Layer);
                Assert.Equal("Fa\u00E7ade", Imp.Only(SceneImport.ReadFile(marked)).Layer);
            }
        }

        [Fact]
        public void AFormatThatIsNotOneOfTheFour_IsRefused()
        {
            Assert.Throws<FormatException>(() => SceneImport.ReadText("x", "stl"));
            Assert.Throws<ArgumentNullException>(() => SceneImport.ReadText(null, "obj"));
            Assert.Throws<ArgumentNullException>(() => SceneImport.ReadText("x", null));
            Assert.Equal("t", SceneImport.ReadText("v 1 2 3\np 1", ".obj", "t").Title);
        }

        [Fact]
        public void AnImportedScene_IsWrittenAsASceneFileAndReadAgainToTheBit()
        {
            Scene scene = ReadObj("v 0.1 1E-300 123456789.12345678\nv 1 0 0\nv 0 1 0\nf 1 2 3\nl 1 2\n");
            Imp.IsAValidScene(scene);
        }

        #endregion
    }
}
