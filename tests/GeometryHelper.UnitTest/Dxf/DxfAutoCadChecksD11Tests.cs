using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What AutoCAD 2021 answered of the checks its plugin ran on 30 October 2026 (runs v2 and v3): two files of the library
    /// it refused to open, a LAYER copied into R2000 with no plot style and a face of a PFACE with a marker too many; the
    /// current transparency, the drawing's properties, groups emptied, a table mirrored, the last active viewport erased and
    /// a new GEODATA as it reads or writes them; and the parts its <c>Explode</c> gives of a dimension, a LEADER, a
    /// MULTILEADER, a HATCH and a MESH. Each drawing is one of ezdxf's, or built here as text, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD11Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>A record built from its groups as "code=value" apart by spaces, as the file writes them.</summary>
        private static string Rec(string type, string groups)
        {
            var text = new StringBuilder("  0\n" + type + "\n");

            foreach (string group in groups.Split(' '))
            {
                int at = group.IndexOf('=');
                text.Append(group.Substring(0, at).PadLeft(3)).Append('\n').Append(group.Substring(at + 1)).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>ezdxf's drawing of R2018 with entities of their own put last in its ENTITIES section, the handles from C00 free.</summary>
        private static DxfDatabase With(string entities)
        {
            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nC00\n");
            int end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nENTITIES\n", StringComparison.Ordinal), StringComparison.Ordinal);
            return DxfDatabase.Parse(text.Insert(end, entities));
        }

        /// <summary>The record of a type naming a value (a name or a handle) among its groups, in a text of a drawing.</summary>
        private static string RecordOf(string text, string type, string value)
            => Records(text).Single(r => r.StartsWith("  0\n" + type + "\n", StringComparison.Ordinal) && r.Contains("\n" + value + "\n"));

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
            => Assert.True(expected.DistanceTo(actual) <= 1E-5, "expected " + expected + ", got " + actual);

        private static GeoPoint3 P(double x, double y, double z = 0.0) => new GeoPoint3(x, y, z);

        #region Files AutoCAD refused

        [Theory]
        [InlineData("Defpoints")]
        [InlineData("GH_TRUECOLOR")]
        public void ALayerCopiedIntoR2000_HasThePlotStyleItNamedThere_AsAutoCadReadsNoLayerWithout(string layerName)
        {
            // A dimension on Defpoints' points and a line on a layer of the source's own, copied from R2018 into the R2000
            // DxfWriter writes: AutoCAD refused the copy, "Did not receive PlotStyleName", the 390 of each LAYER copied left out.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            var layer = new DxfLayerTableRecord { Name = "GH_TRUECOLOR" };
            source.LayerTable.Add(layer);
            var line = new DxfLine(P(0, -40), P(50, -40)) { Layer = "GH_TRUECOLOR" };
            source.ModelSpace.AppendEntity(line);
            DxfDatabase target = DxfDatabase.Parse(new DxfWriter().ToString());

            source.WblockCloneObjects(new DxfEntity[] { ByHandle<DxfEntity>(source, "4D"), line }, target.ModelSpace);

            string text = target.ToText();
            string placeholder = HandleOf(Records(text).Single(r => r.StartsWith("  0\nACDBPLACEHOLDER\n", StringComparison.Ordinal)));
            Assert.Contains("\n390\n" + placeholder + "\n", RecordOf(text, "LAYER", layerName), StringComparison.Ordinal);
            Assert.True(DxfDatabase.Parse(text).LayerTable.Has(layerName));
        }

        [Fact]
        public void ALayerCopiedIntoR2000_WhosePlotStyleTheDrawingHasNot_TakesLayerZerosPlotStyle()
        {
            // The target's plot style Normal renamed: the source's names none of its, and the copy takes layer 0's.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            string writer = new DxfWriter().ToString().Replace("\r\n", "\n");
            DxfDatabase target = DxfDatabase.Parse(writer.Replace("  3\nNormal\n350\n", "  3\nOther\n350\n"));
            string zero = RecordOf(target.ToText(), "LAYER", "0");
            string plotStyle = zero.Substring(zero.IndexOf("\n390\n", StringComparison.Ordinal) + 5).Split('\n')[0];

            source.WblockCloneObjects(new[] { ByHandle<DxfEntity>(source, "4D") }, target.ModelSpace);

            string defpoints = RecordOf(target.ToText(), "LAYER", "Defpoints");
            Assert.Contains("\n370\n-3\n390\n" + plotStyle + "\n", defpoints, StringComparison.Ordinal);
        }

        [Fact]
        public void AFaceOfAPolyfaceMeshMadeInCode_IsWrittenWithTheMarkerOfAFaceRecordAlone_AsAutoCadReadsIt()
        {
            // AutoCAD refused 58-a, "Class separator for class AcDbFaceRecord expected": the face had AcDbVertex before it.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var mesh = new DxfPolyFaceMesh();
            db.ModelSpace.AppendEntity(mesh);
            mesh.AppendVertex(new DxfPolyFaceMeshVertex(P(0, 0)));
            mesh.AppendVertex(new DxfPolyFaceMeshVertex(P(10, 0)));
            mesh.AppendVertex(new DxfPolyFaceMeshVertex(P(5, 5, 8)));
            var face = new DxfFaceRecord(1, 2, -3, 0);
            mesh.AppendFaceRecord(face);

            string record = Records(db.ToText()).Single(r => HandleOf(r) == face.Handle);

            Assert.DoesNotContain("AcDbVertex", record, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbEntity\n  8\n0\n100\nAcDbFaceRecord\n 10\n0\n 20\n0\n 30\n0\n 70\n128\n 71\n1\n 72\n2\n 73\n-3\n", record, StringComparison.Ordinal);
            DxfFaceRecord read = DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfPolyFaceMesh>().Single(m => m.Handle == mesh.Handle).OfType<DxfFaceRecord>().Single();
            Assert.Equal(3, (int)read.GetVertexAt(2));
            Assert.False(read.IsEdgeVisibleAt(2));
        }

        #endregion

        #region What AutoCAD reads and writes

        [Fact]
        public void TheCurrentTransparency_IsWrittenAsTheNumberAnEntityHolds_AndAPerCentReadsByLayer_AsAutoCadReadsThem()
        {
            // AutoCAD read the DICTIONARYVAR "61" the library wrote for the alpha 100 as BYLAYER, alpha 61: the number of a 440.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            db.Cetransparency = new DxfTransparency(100);

            string text = db.ToText();
            Assert.Contains("\n  1\n33554532\n", Records(text).Single(r => r.StartsWith("  0\nDICTIONARYVAR\n", StringComparison.Ordinal) && r.Contains("33554532")), StringComparison.Ordinal);
            Assert.Equal(new DxfTransparency(100), DxfDatabase.Parse(text).Cetransparency);
            db.Cetransparency = DxfTransparency.ByBlock;
            Assert.Equal(DxfTransparency.ByBlock, DxfDatabase.Parse(db.ToText()).Cetransparency);
            Assert.Equal(DxfTransparency.ByLayer, DxfDatabase.Parse(text.Replace("\n  1\n33554532\n", "\n  1\n61\n")).Cetransparency);
        }

        [Fact]
        public void ThePropertiesOfADrawingOfR2018_AreWrittenInTheTenGroupsAndTheHeader_WhereAutoCadReadsThem()
        {
            // AutoCAD read no custom property of the list after group 90, and the hyperlink base from $HYPERLINKBASE alone.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var builder = new DxfDatabaseSummaryInfoBuilder { Title = "Plate B-12", Author = "GeometryHelper", HyperlinkBase = "https://example.com/plates/" };
            builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("Project", "P4 checks"));
            builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("Checked by", "GH"));

            db.SummaryInfo = builder.ToDatabaseSummaryInfo();

            string text = db.ToText();
            var props = (DxfXrecord)DxfDatabase.Parse(text).NamedObjectsDictionary["DWGPROPS"];
            Assert.Equal("300 Project=P4 checks", props.Data[8].ToString());
            Assert.Equal("301 Checked by=GH", props.Data[9].ToString());
            Assert.Equal("1 https://example.com/plates/", props.Data[21].ToString());
            Assert.Equal("90 2", props.Data[22].ToString());
            Assert.Equal(23, props.Data.Count);
            Assert.Contains("$HYPERLINKBASE\n  1\nhttps://example.com/plates/\n", text, StringComparison.Ordinal);
            DxfDatabaseSummaryInfo read = DxfDatabase.Parse(text).SummaryInfo;
            Assert.Equal(new[] { "Project=P4 checks", "Checked by=GH" }, read.CustomProperties.Select(p => p.Key + "=" + p.Value));
            Assert.Equal("https://example.com/plates/", read.HyperlinkBase);
        }

        [Fact]
        public void ADrawingAutoCadSavedAsR2018_GivesThePropertiesItsHeaderHolds()
        {
            // AutoCAD 2021 saved 26-a with $TITLE and $AUTHOR after $DWGCODEPAGE, and no DWGPROPS.
            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n");
            int after = text.IndexOf("$DWGCODEPAGE\n  3\nANSI_1252\n", StringComparison.Ordinal) + "$DWGCODEPAGE\n  3\nANSI_1252\n".Length;
            DxfDatabase db = DxfDatabase.Parse(text.Insert(after, "  9\n$TITLE\n  1\nPlate B-12\n  9\n$AUTHOR\n  1\nGeometryHelper\n"));

            DxfDatabaseSummaryInfo info = db.SummaryInfo;

            Assert.Equal("Plate B-12", info.Title);
            Assert.Equal("GeometryHelper", info.Author);

            // Set, the header's texts follow, so that the two agree.
            db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder(info) { Title = "Plate B-13" }.ToDatabaseSummaryInfo();
            Assert.Contains("$TITLE\n  1\nPlate B-13\n", db.ToText(), StringComparison.Ordinal);
            Assert.Equal("Plate B-13", DxfDatabase.Parse(db.ToText()).SummaryInfo.Title);
        }

        [Fact]
        public void ANamedGroupEmptied_IsKeptEmpty_AsAutoCadKeepsIt_AndAnAnonymousOneEmptiedStillGoes()
        {
            // AutoCAD 2021 kept G_ERASE (its line erased), G_REMOVE (its line taken out) and G_NEW_EMPTY, each of no member.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var beams = (DxfGroup)db.GroupDictionary["BEAMS"];
            ByHandle<DxfLine>(db, "33").Erase();
            beams.RemoveAt(0);
            db.GroupDictionary.SetAt("G_NEW_EMPTY", new DxfGroup("made empty", true));
            ByHandle<DxfDBText>(db, "35").Erase();

            DxfDatabase again = DxfDatabase.Parse(db.ToText());

            Assert.Equal(0, ((DxfGroup)again.GroupDictionary["BEAMS"]).NumEntities);
            Assert.Equal(0, ((DxfGroup)again.GroupDictionary["G_NEW_EMPTY"]).NumEntities);
            Assert.False(again.GroupDictionary.Contains("*A1"));
        }

        [Fact]
        public void ATableMirrored_StandsReadableInTheBoxTheMirrorMakes_AsAutoCadsTransformByLeavesIt()
        {
            // 54-c: the table at (300, -100), columns 40 and 30, mirrored in the plane x = 280: AutoCAD's at (190, -100), its
            // normal (0, 0, 1) and its direction (1, 0, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();

            table.TransformBy(GeoTransform3.Mirror(new GeoPlane3(P(280, -100), new GeoVector3(1, 0, 0))));

            Near(P(190, -100), table.Position);
            Near(P(0, 0, 1), P(table.Normal.X, table.Normal.Y, table.Normal.Z));
            Near(P(1, 0, 0), P(table.Direction.X, table.Direction.Y, table.Direction.Z));
            Assert.Equal(40.0, table.ColumnWidth(0), 9);
            DxfTable read = DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfTable>().Single();
            Near(P(190, -100), read.Position);

            // Its picture is the same table moved: the top line of its block runs from x 190 to 260.
            DxfLine top = table.BlockTableRecord.OfType<DxfLine>().First();
            Near(P(0, 0), top.StartPoint);
            Near(P(70, 0), top.EndPoint);
        }

        [Fact]
        public void TheLastActiveViewportErased_LeavesTheLayoutNamingItsViewportOne_AsAutoCadDoes()
        {
            // 56: AutoCAD's LAYOUT names the viewport 1 (92) in its 331 once the last active one (94) is erased.
            string text = DxfObjectsEzdxfDrawings.R2018.Replace("330\n3B\n331\n3F\n", "330\n3B\n331\n40\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            ByHandle<DxfViewport>(db, "40").Erase();

            string layout = Records(db.ToText()).Single(r => HandleOf(r) == "3E");
            Assert.Contains("\n330\n3B\n331\n3F\n", layout, StringComparison.Ordinal);
        }

        [Fact]
        public void ANewGeoData_IsOfVersion3_AndItsClassCountsIt_AsAutoCad2021WritesThem()
        {
            // 65 and 66: AutoCAD writes 90 of 3 and lists the class GEODATA with a count (91) of 1.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var geo = new DxfGeoData { BlockTableRecord = db.ModelSpace };

            geo.PostToDb();

            string text = db.ToText();
            Assert.Contains("\n100\nAcDbGeoData\n 90\n3\n", Records(text).Single(r => HandleOf(r) == geo.Handle), StringComparison.Ordinal);
            Assert.Contains("  0\nCLASS\n  1\nGEODATA\n  2\nAcDbGeoData\n  3\nObjectDBX Classes\n 90\n4095\n 91\n1\n", text, StringComparison.Ordinal);
            Assert.NotNull(DxfDatabase.Parse(text).GeoDataObject);
        }

        #endregion

        #region What AutoCAD's Explode gives

        [Fact]
        public void ADimensionExploded_LeavesOutThePointsItMeasuresBy_AsAutoCadsExplodeDoes()
        {
            // 50: AutoCAD's Explode of D1 gave 7 parts of its block of 10, the three POINTs on Defpoints left out.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDimension dimension = db.ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == "61");
            int points = dimension.DimBlock.OfType<DxfDBPoint>().Count(p => p.Layer == "Defpoints");

            IReadOnlyList<DxfEntity> parts = dimension.Explode();

            Assert.True(points > 0);
            Assert.Equal(dimension.DimBlock.Count() - points, parts.Count);
            Assert.DoesNotContain(parts, p => p is DxfDBPoint);
        }

        [Fact]
        public void ALeaderExploded_IsItsArrowThenOnePolylineFromTheArrowsBack_InTheLeadersOwnLook()
        {
            // 62: AutoCAD gave each leader a SOLID (the right corner, the tip, the left) and one Polyline from (1.77, 1.77),
            // in the leader's own colour, true colour and lineweight, whatever DIMCLRD and DIMLWD said.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = new DxfLeader();
            leader.AppendVertex(P(0, 0));
            leader.AppendVertex(P(10, 10));
            leader.AppendVertex(P(20, 10));
            db.ModelSpace.AppendEntity(leader);
            leader.ColorIndex = 3;
            leader.TrueColor = 0x112233;
            leader.Dimasz = 2.5;
            leader.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            leader.Dimlwd = DxfLineWeight.LineWeight035;

            IReadOnlyList<DxfEntity> parts = leader.Explode();

            Assert.Equal(2, parts.Count);
            var solid = Assert.IsType<DxfSolid>(parts[0]);
            Near(P(2.062395, 1.473139), solid.GetPointAt(0));
            Near(P(0, 0), solid.GetPointAt(1));
            Near(P(1.473139, 2.062395), solid.GetPointAt(2));
            Near(P(1.473139, 2.062395), solid.GetPointAt(3));
            var line = Assert.IsType<DxfPolyline>(parts[1]);
            Assert.Equal(3, line.NumberOfVertices);
            Near(P(1.767767, 1.767767), line.GetPoint3dAt(0));
            Near(P(10, 10), line.GetPoint3dAt(1));
            Near(P(20, 10), line.GetPoint3dAt(2));
            Assert.All(parts, p => Assert.Equal(0x112233, p.TrueColor));
            Assert.All(parts, p => Assert.Equal(DxfLineWeight.ByLayer, p.LineWeight));
        }

        [Fact]
        public void ALeaderWhoseVerticesStandOffThePlaneOfItsNormal_StillExplodesIntoLines_AsAPolylineCannotHoldThem()
        {
            // The third vertex 3 above the floor of the normal (0, 0, 1): the arrow, then a LINE between each two vertices, as
            // before, the first from the arrow's back.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = new DxfLeader();
            leader.AppendVertex(P(0, 0));
            leader.AppendVertex(P(10, 10));
            leader.AppendVertex(P(20, 10, 3));
            db.ModelSpace.AppendEntity(leader);
            leader.Dimasz = 2.5;

            IReadOnlyList<DxfEntity> parts = leader.Explode();

            Assert.Equal(new[] { "DxfSolid", "DxfLine", "DxfLine" }, parts.Select(p => p.GetType().Name));
            Near(P(1.767767, 1.767767), ((DxfLine)parts[1]).StartPoint);
            Near(P(20, 10, 3), ((DxfLine)parts[2]).EndPoint);
        }

        [Fact]
        public void ASplinedLeaderExploded_IsAutoCadsSplineFromTheArrowsBack_ItsKnotsTheChords()
        {
            // 52, leader 98 moved to the origin: control points (1.77, 1.77) to (20, 10), knots 0 to 21.64.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = new DxfLeader();
            leader.AppendVertex(P(0, 0));
            leader.AppendVertex(P(10, 10));
            leader.AppendVertex(P(20, 10));
            db.ModelSpace.AppendEntity(leader);
            leader.IsSplined = true;
            leader.Dimasz = 2.5;

            IReadOnlyList<DxfEntity> parts = leader.Explode();

            Assert.IsType<DxfSolid>(parts[0]);
            DxfNurbsData data = Assert.IsType<DxfSpline>(parts[1]).NurbsData;
            GeoPoint3[] expected = { P(1.767767, 1.767767), P(4.511845, 4.511845), P(8.476311, 12.357023), P(16.666667, 10), P(20, 10) };
            Assert.Equal(expected.Length, data.GetControlPoints().Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Near(expected[i], data.GetControlPoints()[i]);
            }

            double[] knots = { 0, 0, 0, 0, 11.642136, 21.642136, 21.642136, 21.642136, 21.642136 };
            Assert.Equal(knots.Length, data.GetKnots().Count);

            for (int i = 0; i < knots.Length; i++)
            {
                Assert.Equal(knots[i], data.GetKnots()[i], 5);
            }
        }

        [Fact]
        public void AMultileaderExploded_IsItsWordsTheDoglegAndForEachLineAPolylineAndItsArrow_AsAutoCadGivesThem()
        {
            // 61, multileader 8E: MText, the dogleg Line, a Polyline and a Solid, a Spline and a Solid; BYBLOCK made the
            // multileader's own colour (256), the lines of the leader's lineweight BYBLOCK, the arrows and words BYLAYER.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var mleader = new DxfMLeader { MText = new DxfMText { Contents = "TWO LEADERS", Location = P(30, 12), TextHeight = 2.5 } };
            int first = mleader.AddLeaderLine(P(0, 0));
            db.ModelSpace.AppendEntity(mleader);
            int second = mleader.AddLeaderLine(mleader.GetLeaderIndex(first));
            mleader.AddFirstVertex(second, P(10, 26));
            mleader.AddFirstVertex(second, P(0, 22));
            mleader.SetLeaderLineType(second, DxfLeaderType.SplineLeader);

            IReadOnlyList<DxfEntity> parts = mleader.Explode();

            Assert.Equal(new[] { "DxfMText", "DxfLine", "DxfPolyline", "DxfSolid", "DxfSpline", "DxfSolid" }, parts.Select(p => p.GetType().Name));
            var dogleg = (DxfLine)parts[1];
            Near(P(30, 12), dogleg.StartPoint);
            Near(P(38, 12), dogleg.EndPoint);
            var line = (DxfPolyline)parts[2];
            Assert.Equal(2, line.NumberOfVertices);
            Near(P(3.713907, 1.485563), line.GetPoint3dAt(0));
            Near(P(30, 12), line.GetPoint3dAt(1));
            var arrow = (DxfSolid)parts[3];
            Near(P(3.961501, 0.866578), arrow.GetPointAt(0));
            Near(P(0, 0), arrow.GetPointAt(1));
            Near(P(3.466313, 2.104547), arrow.GetPointAt(2));
            DxfNurbsData spline = ((DxfSpline)parts[4]).NurbsData;
            GeoPoint3[] expected = { P(3.713907, 23.485563), P(5.809271, 24.323708), P(15.910821, 30.963532), P(21.862296, 12), P(30, 12) };

            for (int i = 0; i < expected.Length; i++)
            {
                Near(expected[i], spline.GetControlPoints()[i]);
            }

            Assert.Equal(31.183441, spline.GetKnots()[spline.GetKnots().Count - 1], 5);
            Assert.Equal(6.770330, spline.GetKnots()[4], 5);
            Assert.All(parts, p => Assert.Equal(256, p.ColorIndex));
            Assert.Equal(new[] { DxfLineWeight.ByLayer, DxfLineWeight.ByBlock, DxfLineWeight.ByBlock, DxfLineWeight.ByLayer, DxfLineWeight.ByBlock, DxfLineWeight.ByLayer }, parts.Select(p => p.LineWeight));
        }

        [Fact]
        public void AHatchOfDotsExploded_GivesNoDotOnAnEdgeItsLinesRunAlong_AsAutoCadGivesNone()
        {
            // 60, hatch 8A: DOTS 1.5875 apart in a square 30 by 20 from its corner: AutoCAD 228 dots, none on the bottom edge.
            DxfDatabase db = With(Rec("HATCH", "5=C01 330=17 100=AcDbEntity 8=0 62=1 100=AcDbHatch 10=0.0 20=0.0 30=0.0 210=0.0 220=0.0 230=1.0 2=DOTS 70=0 71=0 91=1 92=3 72=0 73=1 93=4 10=0.0 20=0.0 10=30.0 20=0.0 10=30.0 20=20.0 10=0.0 20=20.0 97=0 75=1 76=1 52=0.0 41=1.0 77=0 78=1 53=0.0 43=0.0 44=0.0 45=0.79375 46=1.5875 79=2 49=0.0 49=-1.5875 98=0"));
            var hatch = ByHandle<DxfHatch>(db, "C01");

            DxfLine[] dots = hatch.Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(228, dots.Length);
            Assert.All(dots, d => Assert.True(d.StartPoint.Y > 1.0, "a dot at " + d.StartPoint));
            Assert.Contains(dots, d => d.StartPoint.IsEqualTo(P(0, 19.05), Tol));
            Assert.Equal(228, hatch.NumberOfHatchLines);
        }

        [Fact]
        public void AHatchInTheOuterStyleExploded_IsCutByTheLoopsFlaggedOutermost_AsAutoCadCutsIt()
        {
            // 60, hatch 8B: ANSI31 in the outer style round circles of radius 7 and 3, both flagged outermost (16): AutoCAD's
            // 17 lines, one of them inside the circle of radius 3.
            DxfDatabase db = With(Rec("HATCH", "5=C01 330=17 100=AcDbEntity 8=0 62=3 100=AcDbHatch 10=0.0 20=0.0 30=0.0 210=0.0 220=0.0 230=1.0 2=ANSI31 70=0 71=0 91=3 92=3 72=0 73=1 93=4 10=40.0 20=0.0 10=70.0 20=0.0 10=70.0 20=20.0 10=40.0 20=20.0 97=0 92=16 93=1 72=2 10=55.0 20=10.0 40=7 50=0 51=360 73=1 97=0 92=16 93=1 72=2 10=55.0 20=10.0 40=3 50=0 51=360 73=1 97=0 75=1 76=1 52=0.0 41=1.0 77=0 78=1 53=45.0 43=0.0 44=0.0 45=-2.2450640303 46=2.2450640303 79=0 98=0"));
            var hatch = ByHandle<DxfHatch>(db, "C01");

            DxfLine[] lines = hatch.Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(17, lines.Length);
            GeoPoint3 centre = P(55, 10);
            Assert.Single(lines, l => P((l.StartPoint.X + l.EndPoint.X) / 2, (l.StartPoint.Y + l.EndPoint.Y) / 2).DistanceTo(centre) < 3.0);
        }

        [Fact]
        public void AMeshFaceOfFiveCorners_IsFannedFromItsFirstCorner_AndAFaceOfItsOwnColourKeepsIt_AsAutoCadExplodesThem()
        {
            // 64: the pentagon 0 1 2 3 4 gave AutoCAD's triangles (4, 3, 0), (2, 0, 3), (0, 2, 1); the face 0 given red (its
            // subentity 1) gave a red 3DFACE.
            string pentagon = Rec("MESH", "5=C01 330=17 100=AcDbEntity 8=0 100=AcDbSubDMesh 71=2 72=0 91=0 92=5 10=30 20=0 30=0 10=40 20=0 30=0 10=43 20=9 30=0 10=35 20=15 30=0 10=27 20=9 30=0 93=6 90=5 90=0 90=1 90=2 90=3 90=4 94=0 95=0 90=0");
            string coloured = Rec("MESH", "5=C02 330=17 100=AcDbEntity 8=0 100=AcDbSubDMesh 71=2 72=0 91=0 92=5 10=60 20=0 30=0 10=70 20=0 30=0 10=70 20=10 30=0 10=60 20=10 30=0 10=65 20=5 30=6 93=21 90=4 90=0 90=3 90=2 90=1 90=3 90=0 90=1 90=4 90=3 90=1 90=2 90=4 90=3 90=2 90=3 90=4 90=3 90=3 90=0 90=4 94=0 95=0 90=1 91=1 92=1 90=0 63=1");
            DxfDatabase db = With(pentagon + coloured);

            DxfFace[] faces = ByHandle<DxfUnknownEntity>(db, "C01").Explode().Cast<DxfFace>().ToArray();
            DxfFace[] red = ByHandle<DxfUnknownEntity>(db, "C02").Explode().Cast<DxfFace>().ToArray();

            Assert.Equal(3, faces.Length);
            GeoPoint3[] corners = { P(30, 0), P(40, 0), P(43, 9), P(35, 15), P(27, 9) };
            int[][] expected = { new[] { 4, 3, 0, 0 }, new[] { 2, 0, 3, 3 }, new[] { 0, 2, 1, 1 } };

            for (int f = 0; f < 3; f++)
            {
                for (short k = 0; k < 4; k++)
                {
                    Near(corners[expected[f][k]], faces[f].GetVertexAt(k));
                }
            }

            Assert.Equal(5, red.Length);
            Assert.Equal(1, red[0].ColorIndex);
            Assert.All(red.Skip(1), f => Assert.Equal(256, f.ColorIndex));
        }

        #endregion
    }
}
